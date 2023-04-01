﻿using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using VpnHood.AccessServer.Dtos;
using VpnHood.AccessServer.Report.Dtos;
using VpnHood.AccessServer.Report.Persistence;
using VpnHood.AccessServer.Utils;

namespace VpnHood.AccessServer.Report.Services;

public class UsageReportService
{
    private const int SmallCacheLength = 50;
    private readonly VhReportContext _vhReportContext;
    private readonly IMemoryCache _memoryCache;
    private readonly ReportServiceOptions _options;

    public UsageReportService(
        VhReportContext vhReportContext,
        IMemoryCache memoryCache,
        IOptions<ReportServiceOptions> options)
    {
        _vhReportContext = vhReportContext;
        _memoryCache = memoryCache;
        _options = options.Value;
    }

    private static DateTime ToUtcWithKind(DateTime dateTime)
    {
        return DateTime.SpecifyKind(dateTime, DateTimeKind.Utc);
    }

    private static DateTime? ToUtcWithKind(DateTime? dateTime)
    {
        return dateTime != null ? ToUtcWithKind(dateTime.Value) : null;
    }

    public async Task<Usage> GetUsage(Guid projectId,
        DateTime usageBeginTime, DateTime? usageEndTime = null,
        Guid? serverFarmId = null, Guid? serverId = null, Guid? deviceId = null)
    {
        usageBeginTime = ToUtcWithKind(usageBeginTime);
        usageEndTime = ToUtcWithKind(usageEndTime);

        // check cache
        var cacheKey = AccessUtil.GenerateCacheKey($"project_usage_{projectId}_{serverFarmId}_{serverId}_{deviceId}",
            usageBeginTime, usageEndTime, out var cacheExpiration);
        if (cacheKey != null && _memoryCache.TryGetValue(cacheKey, out Usage? cacheRes) && cacheRes != null)
            return cacheRes;

        // select and order
        await using var transReport = await _vhReportContext.WithNoLockTransaction();
        var query = _vhReportContext.AccessUsages
            .Where(accessUsage =>
                (accessUsage.ProjectId == projectId) &&
                (accessUsage.CreatedTime >= usageBeginTime) &&
                (serverId == null || accessUsage.ServerId == serverId) &&
                (deviceId == null || accessUsage.DeviceId == deviceId) &&
                (serverFarmId == null || accessUsage.ServerId == serverFarmId) &&
                (usageEndTime == null || accessUsage.CreatedTime <= usageEndTime))
            .GroupBy(accessUsage => accessUsage.DeviceId)
            .Select(g => new
            {
                SentTraffic = g.Sum(y => y.SentTraffic),
                ReceivedTraffic = g.Sum(y => y.ReceivedTraffic),
            })
            .GroupBy(g => true)
            .Select(g => new Usage
            {
                DeviceCount = g.Count(),
                SentTraffic = g.Sum(y => y.SentTraffic),
                ReceivedTraffic = g.Sum(y => y.ReceivedTraffic),
            });


        var res = await query
            .AsNoTracking()
            .SingleOrDefaultAsync() ?? new Usage { ServerCount = 0, DeviceCount = 0 };

        // update cache
        if (cacheKey != null && cacheExpiration != null)
            _memoryCache.Set(cacheKey, res, cacheExpiration.Value);

        return res;
    }

    public async Task<ServerStatusHistory[]> GetServersStatusHistory(Guid projectId,
        DateTime usageBeginTime, DateTime? usageEndTime = null, Guid? serverId = null)
    {
        usageBeginTime = ToUtcWithKind(usageBeginTime);
        usageEndTime = ToUtcWithKind(usageEndTime);
        usageEndTime ??= DateTime.UtcNow;

        // no lock
        await using var transReport = await _vhReportContext.WithNoLockTransaction();

        // check cache
        var cacheKey = AccessUtil.GenerateCacheKey($"project_usage_{projectId}_{serverId}",
            usageBeginTime, usageEndTime, out var cacheExpiration);
        if (cacheKey != null && _memoryCache.TryGetValue(cacheKey, out ServerStatusHistory[]? cacheRes) && cacheRes != null)
            return cacheRes;

        // go back to the time that ensure all servers sent their status
        usageEndTime = usageEndTime.Value.Subtract(_options.ServerUpdateStatusInterval * 2).Subtract(TimeSpan.FromMinutes(5));
        var step1 = Math.Max(5, _options.ServerUpdateStatusInterval.TotalMinutes);
        var step2 = (int)Math.Max(step1, (usageEndTime.Value - usageBeginTime).TotalMinutes / 12 / step1);
        var baseTime = usageBeginTime;


        // per server in status interval
        var serverStatuses = _vhReportContext.ServerStatuses
            .Where(x =>
                x.ProjectId == projectId &&
                (serverId == null || x.ServerId == serverId) &&
                x.CreatedTime >= usageBeginTime &&
                x.CreatedTime <= usageEndTime)
            .GroupBy(serverStatus => new
            {
                //Minutes = (long)(VhReportContext.DateDiffMinute(baseTime, serverStatus.CreatedTime) / step1),
                //Minutes = (long)(EF.Functions.DateDiffMinute(baseTime, serverStatus.CreatedTime) / step1),
                Minutes = (long)(serverStatus.CreatedTime - baseTime).TotalMinutes / step1,
                serverStatus.ServerId
            })
            .Select(g => new
            {
                g.Key.Minutes,
                g.Key.ServerId,
                SessionCount = g.Max(x => x.SessionCount),
                TunnelTransferSpeed = g.Max(x => x.TunnelReceiveSpeed + x.TunnelSendSpeed),
            });

        // sum of max in status interval
        var serverStatuses2 = serverStatuses
            .GroupBy(x => x.Minutes)
            .Select(g => new
            {
                Minutes = g.Key,
                SessionCount = g.Sum(x => x.SessionCount),
                TunnelTransferSpeed = g.Sum(x => x.TunnelTransferSpeed),
                // ServerCount = g.Count() 
            });

        // scale down and find max
        var totalStatuses = serverStatuses2
            .GroupBy(x => (int)(x.Minutes / step2))
            .Select(g =>
                new ServerStatusHistory
                {
                    Time = baseTime.AddMinutes(g.Key * step2 * step1),
                    SessionCount = g.Max(y => y.SessionCount),
                    TunnelTransferSpeed = g.Max(y => y.TunnelTransferSpeed),
                    // ServerCount = g.Max(y=>y.ServerCount) 
                })
            .OrderBy(x => x.Time);

        var res = await totalStatuses.ToListAsync();

        // add missed step
        var stepSize = step2 * step1;
        var stepCount = (int)((usageEndTime - usageBeginTime).Value.TotalMinutes / stepSize) + 1;
        for (var i = 0; i < stepCount; i++)
        {
            var time = usageBeginTime.AddMinutes(i * stepSize);
            if (res.Count <= i || res[i].Time != time)
                res.Insert(i, new ServerStatusHistory { Time = time });
        }

        // update cache
        if (cacheKey != null && cacheExpiration != null)
            _memoryCache.Set(cacheKey, res, cacheExpiration.Value);

        return res.ToArray();
    }


    public async Task<Dictionary<Guid, Usage>> GetAccessTokensUsage(Guid projectId, Guid[]? accessTokenIds = null, Guid? serverFarmId = null,
        DateTime? usageBeginTime = null, DateTime? usageEndTime = null)
    {
        usageBeginTime = ToUtcWithKind(usageBeginTime);
        usageEndTime = ToUtcWithKind(usageEndTime);
        var cacheKey = AccessUtil.GenerateCacheKey($"accessToken_usage_{projectId}_{serverFarmId}",
            usageBeginTime, usageEndTime, out var cacheExpiration);

        // look from big cache
        if (cacheKey != null && _memoryCache.TryGetValue(cacheKey, out Dictionary<Guid, Usage>? usages) && usages != null)
        {
            // filter result by given accessTokenIds
            if (accessTokenIds != null)
                usages = usages.Where(x => accessTokenIds.Contains(x.Key))
                    .ToDictionary(p => p.Key, p => p.Value);

            return usages;

        }

        // look for small cache
        var queryAccessTokenIds = accessTokenIds is { Length: <= SmallCacheLength } ? accessTokenIds : null;
        if (queryAccessTokenIds != null)
        {
            cacheKey = AccessUtil.GenerateCacheKey(
                $"accessToken_usage_{projectId}_{serverFarmId}_{string.Join(',', queryAccessTokenIds)}",
                usageBeginTime, usageEndTime, out _);

            if (cacheKey != null && _memoryCache.TryGetValue(cacheKey, out usages) && usages != null)
                return usages;
        }

        // run the hard query
        await using var transReport = await _vhReportContext.WithNoLockTransaction();
        var usagesQuery = _vhReportContext.AccessUsages
            .Where(accessUsage =>
                (accessUsage.ProjectId == projectId) &&
                (serverFarmId == null || accessUsage.ServerFarmId == serverFarmId) &&
                (queryAccessTokenIds == null || queryAccessTokenIds.Contains(accessUsage.AccessTokenId)) &&
                (accessUsage.CreatedTime >= usageBeginTime) &&
                (usageEndTime == null || accessUsage.CreatedTime <= usageEndTime))
            .GroupBy(accessUsage => accessUsage.AccessTokenId)
            .Select(g => new
            {
                AccessTokenId = g.Key,
                Usage = new Usage
                {
                    SentTraffic = g.Sum(y => y.SentTraffic),
                    ReceivedTraffic = g.Sum(y => y.ReceivedTraffic),
                    DeviceCount = g.Select(y => y.DeviceId).Distinct().Count(),
                    AccessTokenCount = 1,
                }
            });

        usages = await usagesQuery
            .AsNoTracking()
            .ToDictionaryAsync(x => x.AccessTokenId, x => x.Usage);

        // update cache
        if (cacheKey != null && cacheExpiration != null)
            _memoryCache.Set(cacheKey, usages, cacheExpiration.Value);

        // filter result by given accessTokenIds
        if (accessTokenIds != null)
            usages = usages.Where(x => accessTokenIds.Contains(x.Key))
                .ToDictionary(p => p.Key, p => p.Value);

        return usages;
    }

    public async Task<Dictionary<Guid, TrafficUsage>> GetDevicesUsage(Guid projectId,
        Guid[]? deviceIds = null, Guid? accessTokenId = null, Guid? serverFarmId = null,
        DateTime? usageBeginTime = null, DateTime? usageEndTime = null)
    {
        usageBeginTime = ToUtcWithKind(usageBeginTime);
        usageEndTime = ToUtcWithKind(usageEndTime);

        var cacheKey = AccessUtil.GenerateCacheKey($"device_usage_{projectId}_{accessTokenId}_{serverFarmId}",
            usageBeginTime, usageEndTime, out var cacheExpiration);

        // look from big cache
        if (cacheKey != null && _memoryCache.TryGetValue(cacheKey, out Dictionary<Guid, TrafficUsage>? usages) && usages != null)
        {
            // filter result by given deviceIds
            if (deviceIds != null)
                usages = usages.Where(x => deviceIds.Contains(x.Key))
                    .ToDictionary(p => p.Key, p => p.Value);

            return usages;
        }

        // look for small cache
        await using var transReport = await _vhReportContext.WithNoLockTransaction();
        var queryDeviceIds = deviceIds is { Length: <= SmallCacheLength } ? deviceIds : null;
        if (queryDeviceIds != null)
        {
            cacheKey = AccessUtil.GenerateCacheKey(
                $"device_usage_{projectId}_{accessTokenId}_{serverFarmId}_{string.Join(',', queryDeviceIds)}",
                usageBeginTime, usageEndTime, out _);

            if (cacheKey != null && _memoryCache.TryGetValue(cacheKey, out usages) && usages != null)
                return usages;
        }

        // run the hard query
        var usagesQuery =
            from accessUsage in _vhReportContext.AccessUsages
            where
                (accessUsage.ProjectId == projectId) &&
                (serverFarmId == null || accessUsage.ServerFarmId == serverFarmId) &&
                (queryDeviceIds == null || queryDeviceIds.Contains(accessUsage.DeviceId)) &&
                (accessUsage.CreatedTime >= usageBeginTime) &&
                (usageEndTime == null || accessUsage.CreatedTime <= usageEndTime)
            group new { accessUsage } by (Guid?)accessUsage.DeviceId
            into g
            select new
            {
                DeviceId = g.Key,
                Usage = g.Key != null
                    ? new TrafficUsage
                    {
                        SentTraffic = g.Sum(y => y.accessUsage.SentTraffic),
                        ReceivedTraffic = g.Sum(y => y.accessUsage.ReceivedTraffic),
                        LastUsedTime = g.Max(y => y.accessUsage.CreatedTime),
                    }
                    : null
            };

        usages = await usagesQuery
            .AsNoTracking()
            .ToDictionaryAsync(x => x.DeviceId!.Value, x => x.Usage);

        // update cache
        if (cacheKey != null && cacheExpiration != null)
            _memoryCache.Set(cacheKey, usages, cacheExpiration.Value);

        // filter result by given deviceIds
        if (deviceIds != null)
            usages = usages.Where(x => deviceIds.Contains(x.Key))
                .ToDictionary(p => p.Key, p => p.Value);

        return usages;
    }
}