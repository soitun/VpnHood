﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VpnHood.AccessServer.Caches;
using VpnHood.AccessServer.Models;

namespace VpnHood.AccessServer.Persistence;

public class VhAgentRepo(VhContext vhContext, ILogger<VhAgentRepo> logger)
{
    public async Task<CacheInitView> GetInitView(DateTime minServerUsedTime)
    {
        vhContext.Database.SetCommandTimeout(TimeSpan.FromMinutes(5));

        // Statuses
        logger.LogTrace("Loading the recent server status, farms and projects ...");
        var statuses = await vhContext.ServerStatuses
            .Where(x => x.IsLast && x.CreatedTime > minServerUsedTime)
            .Where(x => !x.Server!.IsDeleted)
            .Select(x => new
            {
                Server = new ServerCache
                {
                    ProjectId = x.ProjectId,
                    ServerId = x.ServerId,
                    ServerName = x.Server!.ServerName,
                    ServerFarmId = x.Server!.ServerFarmId,
                    Version = x.Server!.Version,
                    LastConfigError = x.Server!.LastConfigError,
                    LastConfigCode = x.Server!.LastConfigCode,
                    ConfigCode = x.Server!.ConfigCode,
                    ConfigureTime = x.Server!.ConfigureTime,
                    IsEnabled = x.Server!.IsEnabled,
                    AuthorizationCode = x.Server!.AuthorizationCode,
                    AccessPoints = x.Server.AccessPoints.ToArray(),
                    ServerFarmName = x.Server.ServerFarm!.ServerFarmName,
                    ServerProfileId = x.Server.ServerFarm!.ServerProfileId,
                    ServerStatus = x
                },
                Farm = new ServerFarmCache
                {
                    ProjectId = x.Server.ServerFarm.ProjectId,
                    ServerFarmId = x.Server.ServerFarm.ServerFarmId,
                },
                Project = new ProjectCache
                {
                    GaMeasurementId = x.Server.Project!.GaMeasurementId,
                    ProjectId = x.Server.ProjectId,
                    GaApiSecret = x.Server.Project.GaApiSecret
                }
            })
            .AsNoTracking()
            .ToArrayAsync();

        // Sessions
        logger.LogTrace("Loading the sessions and accesses ...");
        var sessions = await vhContext.Sessions
            .Where(session => !session.IsArchived)
            .Select(x => new
            {
                Session = new SessionCache
                {
                    ProjectId = x.ProjectId,
                    SessionId = x.SessionId,
                    AccessId = x.AccessId,
                    ServerId = x.ServerId,
                    DeviceId = x.DeviceId,
                    ExtraData = x.ExtraData,
                    CreatedTime = x.CreatedTime,
                    LastUsedTime = x.LastUsedTime,
                    ClientVersion = x.ClientVersion,
                    EndTime = x.EndTime,
                    ErrorCode = x.ErrorCode,
                    ErrorMessage = x.ErrorMessage,
                    SessionKey = x.SessionKey,
                    SuppressedBy = x.SuppressedBy,
                    SuppressedTo = x.SuppressedTo,
                    IsArchived = x.IsArchived,
                    ClientId = x.Device!.ClientId,
                    UserAgent = x.Device.UserAgent,
                    Country = x.Device.Country,
                    DeviceIp = x.DeviceIp,
                },
                Access = new AccessCache
                {
                    AccessId = x.AccessId,
                    ExpirationTime = x.Access!.AccessToken!.ExpirationTime,
                    DeviceId = x.Access.DeviceId,
                    LastUsedTime = x.Access.LastUsedTime,
                    Description = x.Access.Description,
                    LastCycleSentTraffic = x.Access.LastCycleSentTraffic,
                    LastCycleReceivedTraffic = x.Access.LastCycleReceivedTraffic,
                    LastCycleTraffic = x.Access.LastCycleTraffic,
                    TotalSentTraffic = x.Access.TotalSentTraffic,
                    TotalReceivedTraffic = x.Access.TotalReceivedTraffic,
                    TotalTraffic = x.Access.TotalTraffic,
                    CycleTraffic = x.Access.CycleTraffic,
                    AccessTokenId = x.Access.AccessTokenId,
                    CreatedTime = x.Access.CreatedTime,
                    AccessTokenSupportCode = x.Access.AccessToken.SupportCode,
                    AccessTokenName = x.Access.AccessToken.AccessTokenName,
                    MaxDevice = x.Access.AccessToken.MaxDevice,
                    MaxTraffic = x.Access.AccessToken.MaxTraffic,
                    IsPublic = x.Access.AccessToken.IsPublic,
                }
            })
            .AsNoTracking()
            .ToArrayAsync();

        var ret = new CacheInitView
        {
            Servers = statuses.Select(x => x.Server).ToArray(),
            Farms = statuses.Select(x => x.Farm).DistinctBy(x => x.ServerFarmId).ToArray(),
            Projects = statuses.Select(x => x.Project).DistinctBy(x => x.ProjectId).ToArray(),
            Sessions = sessions.Select(x => x.Session).ToArray(),
            Accesses = sessions.Select(x => x.Access).DistinctBy(x => x.AccessId).ToArray(),
        };

        return ret;
    }

    public Task<ServerCache[]> GetServers(Guid[]? serverIds = null)
    {
        return vhContext.Servers
            .Where(x => serverIds == null || serverIds.Contains(x.ServerId))
            .Include(x => x.ServerStatuses!.Where(y => y.IsLast == true))
            .Select(x => new ServerCache
            {
                ProjectId = x.ProjectId,
                ServerId = x.ServerId,
                ServerFarmId = x.ServerFarmId,
                ServerName = x.ServerName,
                Version = x.Version,
                LastConfigError = x.LastConfigError,
                LastConfigCode = x.LastConfigCode,
                ConfigCode = x.ConfigCode,
                ConfigureTime = x.ConfigureTime,
                IsEnabled = x.IsEnabled,
                AuthorizationCode = x.AuthorizationCode,
                AccessPoints = x.AccessPoints.ToArray(),
                ServerFarmName = x.ServerFarm!.ServerFarmName,
                ServerProfileId = x.ServerFarm!.ServerProfileId,
                ServerStatus = x.ServerStatuses!.FirstOrDefault(),
            })
            .AsNoTracking()
            .ToArrayAsync();
    }


    public async Task<ServerCache> GetServer(Guid serverId)
    {
        var server = await GetServers([serverId]);
        return server.Single();
    }

    public Task<ServerFarmCache> GetFarm(Guid farmId)
    {
        return vhContext.ServerFarms
            .Where(farm => farm.ServerFarmId == farmId)
            .Select(farm => new ServerFarmCache
            {
                ProjectId = farm.ProjectId,
                ServerFarmId = farm.ServerFarmId
            })
            .AsNoTracking()
            .SingleAsync();
    }

    public Task<ProjectCache> GetProject(Guid projectId)
    {
        return vhContext.Projects
            .Where(project => project.ProjectId == projectId)
            .Select(project => new ProjectCache
            {
                ProjectId = project.ProjectId,
                GaMeasurementId = project.GaMeasurementId,
                GaApiSecret = project.GaApiSecret
            })
            .AsNoTracking()
            .SingleAsync();
    }

    public async Task<AccessCache> GetAccess(Guid accessId)
    {
        return await vhContext.Accesses
            .Where(x => x.AccessId == accessId)
            .Select(x => new AccessCache
            {
                AccessId = x.AccessId,
                DeviceId = x.DeviceId,
                LastUsedTime = x.LastUsedTime,
                Description = x.Description,
                LastCycleSentTraffic = x.LastCycleSentTraffic,
                LastCycleReceivedTraffic = x.LastCycleReceivedTraffic,
                LastCycleTraffic = x.LastCycleTraffic,
                TotalSentTraffic = x.TotalSentTraffic,
                TotalReceivedTraffic = x.TotalReceivedTraffic,
                TotalTraffic = x.TotalTraffic,
                CycleTraffic = x.CycleTraffic,
                AccessTokenId = x.AccessTokenId,
                CreatedTime = x.CreatedTime,
                ExpirationTime = x.AccessToken!.ExpirationTime,
                AccessTokenSupportCode = x.AccessToken.SupportCode,
                AccessTokenName = x.AccessToken.AccessTokenName,
                MaxDevice = x.AccessToken.MaxDevice,
                MaxTraffic = x.AccessToken.MaxTraffic,
                IsPublic = x.AccessToken.IsPublic,
            })
            .AsNoTracking()
            .SingleAsync();
    }

    public async Task<AccessCache?> GetAccessOrDefault(Guid accessTokenId, Guid? deviceId)
    {
        return await vhContext.Accesses
            .Where(x => x.AccessTokenId == accessTokenId && x.DeviceId == deviceId)
            .Select(x => new AccessCache
            {
                AccessId = x.AccessId,
                DeviceId = x.DeviceId,
                LastUsedTime = x.LastUsedTime,
                Description = x.Description,
                LastCycleSentTraffic = x.LastCycleSentTraffic,
                LastCycleReceivedTraffic = x.LastCycleReceivedTraffic,
                LastCycleTraffic = x.LastCycleTraffic,
                TotalSentTraffic = x.TotalSentTraffic,
                TotalReceivedTraffic = x.TotalReceivedTraffic,
                TotalTraffic = x.TotalTraffic,
                CycleTraffic = x.CycleTraffic,
                AccessTokenId = x.AccessTokenId,
                CreatedTime = x.CreatedTime,
                ExpirationTime = x.AccessToken!.ExpirationTime,
                AccessTokenSupportCode = x.AccessToken.SupportCode,
                AccessTokenName = x.AccessToken.AccessTokenName,
                MaxDevice = x.AccessToken.MaxDevice,
                MaxTraffic = x.AccessToken.MaxTraffic,
                IsPublic = x.AccessToken.IsPublic,
            })
            .AsNoTracking()
            .SingleOrDefaultAsync();
    }

    public async Task<AccessCache> AddNewAccess(Guid accessTokenId, Guid? deviceId)
    {
        var accessToken = await vhContext.AccessTokens
            .Where(x => x.AccessTokenId == accessTokenId)
            .SingleAsync();

        var access = new AccessCache
        {
            AccessId = Guid.NewGuid(),
            AccessTokenId = accessTokenId,
            DeviceId = deviceId,
            CreatedTime = DateTime.UtcNow,
            LastUsedTime = DateTime.UtcNow,
            Description = null,
            LastCycleSentTraffic = 0,
            LastCycleReceivedTraffic = 0,
            LastCycleTraffic = 0,
            TotalSentTraffic = 0,
            TotalReceivedTraffic = 0,
            TotalTraffic = 0,
            CycleTraffic = 0,
            ExpirationTime = accessToken.ExpirationTime,
            AccessTokenSupportCode = accessToken.SupportCode,
            AccessTokenName = accessToken.AccessTokenName,
            MaxDevice = accessToken.MaxDevice,
            MaxTraffic = accessToken.MaxTraffic,
            IsPublic = accessToken.IsPublic
        };

        await vhContext.Accesses.AddAsync(access.ToModel());
        return access;
    }

    public void UpdateSession(SessionCache session)
    {
        var model = new SessionModel
        {
            SessionId = session.SessionId,
            ProjectId = session.ProjectId,
            AccessId = session.AccessId,
            ServerId = session.ServerId,
            DeviceId = session.DeviceId,
            ExtraData = session.ExtraData,
            CreatedTime = session.CreatedTime,
            LastUsedTime = session.LastUsedTime,
            ClientVersion = session.ClientVersion,
            EndTime = session.EndTime,
            ErrorCode = session.ErrorCode,
            ErrorMessage = session.ErrorMessage,
            SessionKey = session.SessionKey,
            SuppressedBy = session.SuppressedBy,
            SuppressedTo = session.SuppressedTo,
            IsArchived = session.IsArchived,
            Country = session.Country,
            DeviceIp = session.DeviceIp,
        };

        var entry = vhContext.Sessions.Attach(model);
        entry.Property(x => x.LastUsedTime).IsModified = true;
        entry.Property(x => x.EndTime).IsModified = true;
        entry.Property(x => x.SuppressedTo).IsModified = true;
        entry.Property(x => x.SuppressedBy).IsModified = true;
        entry.Property(x => x.ErrorMessage).IsModified = true;
        entry.Property(x => x.ErrorCode).IsModified = true;
        entry.Property(x => x.IsArchived).IsModified = true;
    }

    public void UpdateAccess(AccessCache access)
    {
        var model = new AccessModel
        {
            AccessId = access.AccessId,
            AccessTokenId = access.AccessTokenId,
            DeviceId = access.DeviceId,
            CreatedTime = access.CreatedTime,
            LastUsedTime = access.LastUsedTime,
            LastCycleReceivedTraffic = access.LastCycleReceivedTraffic,
            LastCycleSentTraffic = access.LastCycleSentTraffic,
            LastCycleTraffic = access.LastCycleTraffic,
            TotalReceivedTraffic = access.TotalReceivedTraffic,
            TotalSentTraffic = access.TotalSentTraffic,
            TotalTraffic = access.TotalTraffic,
            CycleTraffic = access.CycleTraffic,
            Description = access.Description
        };

        var entry = vhContext.Accesses.Attach(model);
        entry.Property(x => x.LastUsedTime).IsModified = true;
        entry.Property(x => x.TotalReceivedTraffic).IsModified = true;
        entry.Property(x => x.TotalSentTraffic).IsModified = true;
    }

    public async Task<SessionModel> AddSession(SessionCache session)
    {
        var entry = await vhContext.Sessions.AddAsync(session.ToModel());
        return entry.Entity;
    }

    public async Task AddAccessUsages(AccessUsageModel[] sessionUsages)
    {
        await vhContext.AccessUsages.AddRangeAsync(sessionUsages);
    }

    public Task SaveChangesAsync()
    {
        return vhContext.SaveChangesAsync();
    }

    public Task<ServerModel> ServerGet(Guid projectId, Guid serverId,
        bool includeFarm = false, bool includeFarmProfile = false)
    {
        var query = vhContext.Servers
            .Where(x => x.ProjectId == projectId && !x.IsDeleted)
            .Where(x => x.ServerId == serverId);

        if (includeFarm) query = query.Include(server => server.ServerFarm);
        if (includeFarmProfile) query = query.Include(server => server.ServerFarm!.ServerProfile);

        return query.SingleAsync();
    }

    public Task<ServerFarmModel> ServerFarmGet(Guid projectId, Guid serverFarmId, 
        bool includeServersAndAccessPoints = false, bool includeCertificate = false, bool includeServerProfile = true)
    {
        var query = vhContext.ServerFarms
            .Where(farm => farm.ProjectId == projectId && !farm.IsDeleted)
            .Where(farm => farm.ServerFarmId == serverFarmId);

        if (includeServerProfile)
            query = query.Include(x => x.ServerProfile);

        if (includeCertificate)
            query = query.Include(x => x.Certificate);

        if (includeServersAndAccessPoints)
            query = query
                .Include(farm => farm.Servers!.Where(server => !server.IsDeleted))
                .ThenInclude(server => server.AccessPoints)
                .AsSplitQuery();

        return query.SingleAsync();
    }


    public ValueTask<ServerModel?> FindServerAsync(Guid serverServerId)
    {
        return vhContext.Servers.FindAsync(serverServerId);
    }
}