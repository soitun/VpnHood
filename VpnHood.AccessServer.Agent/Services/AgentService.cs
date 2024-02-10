﻿using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using VpnHood.AccessServer.Caches;
using VpnHood.AccessServer.Dtos;
using VpnHood.AccessServer.Models;
using VpnHood.AccessServer.Persistence;
using VpnHood.AccessServer.Utils;
using VpnHood.Common.Messaging;
using VpnHood.Common.Utils;
using VpnHood.Server.Access;
using VpnHood.Server.Access.Configurations;
using VpnHood.Server.Access.Messaging;
using SessionOptions = VpnHood.Server.Access.Configurations.SessionOptions;

namespace VpnHood.AccessServer.Agent.Services;

public class AgentService(
    ILogger<AgentService> logger,
    IOptions<AgentOptions> agentOptions,
    CacheService cacheService,
    SessionService sessionService,
    VhAgentRepo vhAgentRepo)
{
    private readonly AgentOptions _agentOptions = agentOptions.Value;

    public async Task<ServerCache> GetServer(Guid serverId)
    {
        var server = await cacheService.GetServer(serverId);
        return server;
    }

    public async Task<SessionResponseEx> CreateSession(Guid serverId, SessionRequestEx sessionRequestEx)
    {
        var server = await GetServer(serverId);
        return await sessionService.CreateSession(server, sessionRequestEx);
    }

    [HttpGet("sessions/{sessionId}")]
    public async Task<SessionResponseEx> GetSession(Guid serverId, uint sessionId, string hostEndPoint, string? clientIp)
    {
        var server = await GetServer(serverId);
        return await sessionService.GetSession(server, sessionId, hostEndPoint, clientIp);
    }

    [HttpPost("sessions/{sessionId}/usage")]
    public async Task<SessionResponseBase> AddSessionUsage(Guid serverId, uint sessionId, bool closeSession, Traffic traffic)
    {
        var server = await GetServer(serverId);
        return await sessionService.AddUsage(server, sessionId, traffic, closeSession);
    }

    [HttpGet("certificates/{hostEndPoint}")]
    public async Task<byte[]> GetCertificate(Guid serverId, string hostEndPoint)
    {
        var server = await GetServer(serverId);
        logger.LogInformation(AccessEventId.Server, "Get certificate. ServerId: {ServerId}, HostEndPoint: {HostEndPoint}",
            server.ServerId, hostEndPoint);

        var serverFarm = await vhAgentRepo.ServerFarmGet(server.ProjectId, server.ServerFarmId, includeCertificate: true );
        return serverFarm.Certificate!.RawData;
    }

    private async Task CheckServerVersion(ServerCache server, string? version)
    {
        if (!string.IsNullOrEmpty(version) && Version.Parse(version) >= ServerUtil.MinServerVersion)
            return;

        var errorMessage = $"Your server version is not supported. Please update your server. MinSupportedVersion: {ServerUtil.MinServerVersion}";
        if (server.LastConfigError != errorMessage)
        {
            // update db & cache
            var serverModel = await vhAgentRepo.FindServerAsync(server.ServerId) ?? throw new KeyNotFoundException($"Could not find Server! ServerId: {server.ServerId}");
            serverModel.LastConfigError = errorMessage;
            await vhAgentRepo.SaveChangesAsync();
            await cacheService.InvalidateServer(serverModel.ServerId);

        }
        throw new NotSupportedException(errorMessage);
    }

    [HttpPost("status")]
    public async Task<ServerCommand> UpdateServerStatus(Guid serverId, ServerStatus serverStatus)
    {
        var server = await GetServer(serverId);
        await CheckServerVersion(server, server.Version);
        UpdateServerStatus(server, serverStatus, false);

        // remove LastConfigCode if server send its status
        if (server.LastConfigCode?.ToString() != serverStatus.ConfigCode || server.LastConfigError != serverStatus.ConfigError)
        {
            logger.LogInformation(AccessEventId.Server,
                "Updating a server's LastConfigCode. ServerId: {ServerId}, ConfigCode: {ConfigCode}",
                server.ServerId, serverStatus.ConfigCode);

            // update db & cache
            var serverUpdate = await vhAgentRepo.FindServerAsync(server.ServerId) ?? throw new KeyNotFoundException($"Could not find Server! ServerId: {server.ServerId}");
            serverUpdate.LastConfigError = serverStatus.ConfigError;
            serverUpdate.LastConfigCode = !string.IsNullOrEmpty(serverStatus.ConfigCode) ? Guid.Parse(serverStatus.ConfigCode) : null;
            serverUpdate.ConfigureTime = DateTime.UtcNow;
            await vhAgentRepo.SaveChangesAsync();
            await cacheService.InvalidateServer(serverUpdate.ServerId);
        }

        var ret = new ServerCommand(server.ConfigCode.ToString());
        return ret;
    }

    [HttpPost("configure")]
    public async Task<ServerConfig> ConfigureServer(Guid serverId, ServerInfo serverInfo)
    {
        // first use cache make sure not use db for old versions
        var server = await GetServer(serverId);
        logger.Log(LogLevel.Information, AccessEventId.Server,
            "Configuring a Server. ServerId: {ServerId}, Version: {Version}",
            server.ServerId, serverInfo.Version);

        // check version
        await CheckServerVersion(server, serverInfo.Version.ToString());
        UpdateServerStatus(server, serverInfo.Status, true);
        
        // ready for update
        var serverModel = await vhAgentRepo.ServerGet(server.ProjectId, serverId, includeFarm: true, includeFarmProfile: true);
        var serverFarmModel = serverModel.ServerFarm!;
        var serverProfileModel = serverModel.ServerFarm!.ServerProfile!;

        // update cache
        serverModel.EnvironmentVersion = serverInfo.EnvironmentVersion.ToString();
        serverModel.OsInfo = serverInfo.OsInfo;
        serverModel.MachineName = serverInfo.MachineName;
        serverModel.ConfigureTime = DateTime.UtcNow;
        serverModel.TotalMemory = serverInfo.TotalMemory ?? 0;
        serverModel.LogicalCoreCount = serverInfo.LogicalCoreCount;
        serverModel.Version = serverInfo.Version.ToString();

        // calculate access points
        if (serverModel.AutoConfigure)
        {
            var serverFarm = await vhAgentRepo.ServerFarmGet(server.ProjectId, serverModel.ServerFarmId, 
                includeServersAndAccessPoints: true, includeCertificate: true);
            var accessPoints = BuildServerAccessPoints(serverModel.ServerId, serverFarm.Servers!, serverInfo);

            // check if access points has been changed, then update host token and access points
            if (JsonSerializer.Serialize(accessPoints) != JsonSerializer.Serialize(serverModel.AccessPoints))
            {
                serverModel.AccessPoints = accessPoints;
                FarmTokenBuilder.UpdateIfChanged(serverFarm);
            }
        }

        // update if there is any change & update cache
        await vhAgentRepo.SaveChangesAsync();
        await cacheService.InvalidateServer(server.ServerId);

        // update cache
        var serverConfig = GetServerConfig(serverModel, serverFarmModel, serverProfileModel);
        return serverConfig;
    }

    private ServerConfig GetServerConfig(ServerModel serverModel, ServerFarmModel serverFarmModel, ServerProfileModel serverProfileModel)
    {
        var tcpEndPoints = serverModel.AccessPoints
            .Where(accessPoint => accessPoint.IsListen)
            .Select(accessPoint => new IPEndPoint(accessPoint.IpAddress, accessPoint.TcpPort))
            .ToArray();

        var udpEndPoints = serverModel.AccessPoints
            .Where(accessPoint => accessPoint is { IsListen: true, UdpPort: > 0 })
            .Select(accessPoint => new IPEndPoint(accessPoint.IpAddress, accessPoint.UdpPort))
            .ToArray();

        // defaults
        var serverConfig = new ServerConfig
        {
            TrackingOptions = new TrackingOptions
            {
                TrackTcp = true,
                TrackUdp = true,
                TrackIcmp = true,
                TrackClientIp = true,
                TrackLocalPort = true,
                TrackDestinationPort = true,
                TrackDestinationIp = true
            },
            SessionOptions = new SessionOptions
            {
                TcpBufferSize = ServerUtil.GetBestTcpBufferSize(serverModel.TotalMemory),
            },
            ServerSecret = serverFarmModel.Secret
        };

        // merge with profile
        var serverProfileConfigJson = serverProfileModel.ServerConfig;
        if (!string.IsNullOrEmpty(serverProfileConfigJson))
        {
            try
            {
                var serverProfileConfig = VhUtil.JsonDeserialize<ServerConfig>(serverProfileConfigJson);
                serverConfig.Merge(serverProfileConfig);
            }
            catch (Exception ex)
            {
                logger.LogError(AccessEventId.Server, ex, "Could not deserialize ServerProfile's ServerConfig.");
            }
        }

        // enforced items
        serverConfig.Merge(new ServerConfig
        {
            TcpEndPoints = tcpEndPoints,
            UdpEndPoints = udpEndPoints,
            UpdateStatusInterval = _agentOptions.ServerUpdateStatusInterval,
            SessionOptions = new SessionOptions
            {
                Timeout = _agentOptions.SessionTemporaryTimeout,
                SyncInterval = _agentOptions.SessionSyncInterval,
                SyncCacheSize = _agentOptions.SyncCacheSize
            }
        });
        serverConfig.ConfigCode = serverModel.ConfigCode.ToString(); // merge does not apply this

        return serverConfig;
    }

    private static int GetBestUdpPort(IReadOnlyCollection<AccessPointModel> oldAccessPoints,
        IPAddress ipAddress, int udpPortV4, int udpPortV6)
    {
        // find previous value
        var res = oldAccessPoints.FirstOrDefault(x => x.IpAddress.Equals(ipAddress))?.UdpPort;
        if (res != null && res != 0)
            return res.Value;

        // find from other previous ip of same family
        res = oldAccessPoints.FirstOrDefault(x => x.IpAddress.AddressFamily.Equals(ipAddress.AddressFamily))?.UdpPort;
        if (res != null && res != 0)
            return res.Value;

        // use preferred value
        var preferredValue = ipAddress.AddressFamily == AddressFamily.InterNetworkV6 ? udpPortV6 : udpPortV4;
        return preferredValue;
    }

    private static IEnumerable<IPAddress> GetMissedServerPublicIps(
        IEnumerable<AccessPointModel> oldAccessPoints,
        ServerInfo serverInfo,
        AddressFamily addressFamily)
    {
        if (serverInfo.PrivateIpAddresses.All(x => x.AddressFamily != addressFamily) || // there is no private IP anymore
            serverInfo.PublicIpAddresses.Any(x => x.AddressFamily == addressFamily)) // there is no problem because server could report its public IP
            return Array.Empty<IPAddress>();

        return oldAccessPoints
            .Where(x => x.IsPublic && x.IpAddress.AddressFamily == addressFamily)
            .Select(x => x.IpAddress);
    }

    private static List<AccessPointModel> BuildServerAccessPoints(Guid serverId, ICollection<ServerModel> farmServers, ServerInfo serverInfo)
    {
        // all old PublicInToken AccessPoints in the same farm
        var oldTokenAccessPoints = farmServers
            .SelectMany(serverModel => serverModel.AccessPoints)
            .Where(accessPoint => accessPoint.AccessPointMode == AccessPointMode.PublicInToken)
            .ToArray();

        // server
        var server = farmServers.Single(x => x.ServerId == serverId);

        // prepare server addresses
        var privateIpAddresses = serverInfo.PrivateIpAddresses;
        var publicIpAddresses = serverInfo.PublicIpAddresses.ToList();
        publicIpAddresses.AddRange(GetMissedServerPublicIps(server.AccessPoints, serverInfo, AddressFamily.InterNetwork));
        publicIpAddresses.AddRange(GetMissedServerPublicIps(server.AccessPoints, serverInfo, AddressFamily.InterNetworkV6));

        // create private addresses
        var accessPoints = privateIpAddresses
            .Distinct()
            .Where(ipAddress => !publicIpAddresses.Any(x => x.Equals(ipAddress)))
            .Select(ipAddress => new AccessPointModel
            {
                AccessPointMode = AccessPointMode.Private,
                IsListen = true,
                IpAddress = ipAddress,
                TcpPort = 443,
                UdpPort = GetBestUdpPort(oldTokenAccessPoints, ipAddress, serverInfo.FreeUdpPortV4, serverInfo.FreeUdpPortV6)
            })
            .ToList();

        // create public addresses and try to save last publicInToken state
        accessPoints
            .AddRange(publicIpAddresses
            .Distinct()
            .Select(ipAddress => new AccessPointModel
            {
                AccessPointMode = oldTokenAccessPoints.Any(x => x.IpAddress.Equals(ipAddress))
                    ? AccessPointMode.PublicInToken // prefer last value
                    : AccessPointMode.Public,
                IsListen = privateIpAddresses.Any(x => x.Equals(ipAddress)),
                IpAddress = ipAddress,
                TcpPort = 443,
                UdpPort = GetBestUdpPort(oldTokenAccessPoints, ipAddress, serverInfo.FreeUdpPortV4, serverInfo.FreeUdpPortV6)
            }));

        // has other server in the farm offer any PublicInToken
        var hasOtherServerOwnPublicToken = farmServers.Any(x =>
            x.ServerId != serverId &&
            x.AccessPoints.Any(accessPoint => accessPoint.AccessPointMode == AccessPointMode.PublicInToken));

        // make sure at least one PublicInToken is selected
        if (!hasOtherServerOwnPublicToken)
        {
            SelectAccessPointAsPublicInToken(accessPoints, AddressFamily.InterNetwork);
            SelectAccessPointAsPublicInToken(accessPoints, AddressFamily.InterNetworkV6);
        }

        return accessPoints.ToList();
    }
    private static void SelectAccessPointAsPublicInToken(ICollection<AccessPointModel> accessPoints, AddressFamily addressFamily)
    {
        if (accessPoints.Any(x => x.AccessPointMode == AccessPointMode.PublicInToken && x.IpAddress.AddressFamily == addressFamily))
            return; // already set

        var firstPublic = accessPoints.FirstOrDefault(x =>
            x.AccessPointMode == AccessPointMode.Public &&
            x.IpAddress.AddressFamily == addressFamily);

        if (firstPublic == null)
            return; // not public found to select as PublicInToken

        accessPoints.Remove(firstPublic);
        accessPoints.Add(new AccessPointModel
        {
            AccessPointMode = AccessPointMode.PublicInToken,
            IsListen = firstPublic.IsListen,
            IpAddress = firstPublic.IpAddress,
            TcpPort = firstPublic.TcpPort,
            UdpPort = firstPublic.UdpPort
        });
    }

    private static void UpdateServerStatus(ServerCache server, ServerStatus serverStatus, bool isConfigure)
    {
        server.ServerStatus = new ServerStatusModel
        {
            ServerStatusId = 0,
            ProjectId = server.ProjectId,
            ServerId = server.ServerId,
            IsConfigure = isConfigure,
            IsLast = true,
            CreatedTime = DateTime.UtcNow,
            AvailableMemory = serverStatus.AvailableMemory,
            CpuUsage = (byte?)serverStatus.CpuUsage,
            TcpConnectionCount = serverStatus.TcpConnectionCount,
            UdpConnectionCount = serverStatus.UdpConnectionCount,
            SessionCount = serverStatus.SessionCount,
            ThreadCount = serverStatus.ThreadCount,
            TunnelSendSpeed = serverStatus.TunnelSpeed.Sent,
            TunnelReceiveSpeed = serverStatus.TunnelSpeed.Received,
        };
    }
}
