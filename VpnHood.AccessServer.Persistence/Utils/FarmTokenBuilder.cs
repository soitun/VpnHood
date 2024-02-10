﻿using System.Net;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using VpnHood.AccessServer.Persistence.Enums;
using VpnHood.AccessServer.Persistence.Models;
using VpnHood.Common;
using VpnHood.Common.Utils;

namespace VpnHood.AccessServer.Persistence.Utils;

public static class FarmTokenBuilder
{
    public static bool UpdateIfChanged(ServerFarmModel serverFarm)
    {
        // build new host token
        var farmTokenNew = Build(serverFarm);

        // check for change
        if (!string.IsNullOrEmpty(serverFarm.TokenJson))
        {
            var farmTokenOld = JsonSerializer.Deserialize<ServerToken>(serverFarm.TokenJson);
            if (farmTokenOld != null && !farmTokenOld.IsTokenUpdated(farmTokenNew))
                return false;
        }

        // update host token
        serverFarm.TokenJson = JsonSerializer.Serialize(farmTokenNew);
        return true;
    }

    public static ServerToken Build(ServerFarmModel serverFarm)
    {
        ArgumentNullException.ThrowIfNull(serverFarm.Servers);
        ArgumentNullException.ThrowIfNull(serverFarm.Certificate);

        return Build(serverFarm, new X509Certificate2(serverFarm.Certificate.RawData));
    }

    private static ServerToken Build(ServerFarmModel serverFarm, X509Certificate2 x509Certificate)
    {
        // find all public accessPoints 
        var accessPoints = serverFarm.Servers!
            .SelectMany(server => server.AccessPoints)
            .Where(accessPoint => accessPoint.AccessPointMode == AccessPointMode.PublicInToken)
            .ToArray();

        // find all token tcp port
        var hostPort = 0;
        if (serverFarm.UseHostName)
        {
            var hostPorts = accessPoints.DistinctBy(x => x.TcpPort).ToArray();
            hostPort = hostPorts.FirstOrDefault()?.TcpPort ?? 443;
        }

        // create token
        var serverToken = new ServerToken
        {
            CertificateHash = x509Certificate.GetCertHash(),
            HostName = x509Certificate.GetNameInfo(X509NameType.DnsName, false),
            HostEndPoints = accessPoints.Select(accessPoint => new IPEndPoint(accessPoint.IpAddress, accessPoint.TcpPort)).ToArray(),
            Secret = serverFarm.Secret,
            HostPort = hostPort,
            IsValidHostName = serverFarm.UseHostName,
            Url = serverFarm.TokenUrl,
            CreatedTime = VhUtil.RemoveMilliseconds(DateTime.UtcNow),
        };

        return serverToken;
    }

    private static bool CanUse(ServerToken serverToken)
    {
        return serverToken is { IsValidHostName: true, HostPort: > 0 } || 
               serverToken.HostEndPoints?.Length > 0;
    }

    public static ServerToken? TryGetUsableToken(ServerFarmModel serverFarm)
    {
        if (string.IsNullOrEmpty(serverFarm.TokenJson))
            return null;

        var serverToken = JsonSerializer.Deserialize<ServerToken>(serverFarm.TokenJson);
        return serverToken != null && CanUse(serverToken) ? serverToken : null;
    }

    public static ServerToken GetUsableToken(ServerFarmModel serverFarm)
    {
        return TryGetUsableToken(serverFarm) 
               ?? throw new InvalidOperationException("The Farm has not been configured or it does not have at least a server with a PublicInToken access points.");
    }
}