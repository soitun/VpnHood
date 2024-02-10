﻿using VpnHood.Server.Access.Configurations;
using SessionOptions = VpnHood.Server.Access.Configurations;

namespace VpnHood.AccessServer.Agent;

public class AgentOptions
{
    public static readonly Version MinClientVersion = Version.Parse("2.3.289");
    public static readonly Version MinServerVersion = Version.Parse("3.0.411");

    public TimeSpan ServerUpdateStatusInterval { get; set; } = new ServerConfig().UpdateStatusIntervalValue;
    public TimeSpan LostServerThreshold => ServerUpdateStatusInterval * 3;
    public TimeSpan SessionSyncInterval { get; set; } = new SessionOptions.SessionOptions().SyncIntervalValue;
    public TimeSpan SessionTemporaryTimeout { get; set; } = new SessionOptions.SessionOptions().TimeoutValue;
    public long SyncCacheSize { get; set; } = new SessionOptions.SessionOptions().SyncCacheSizeValue;
    public TimeSpan SessionPermanentlyTimeout { get; set; } = TimeSpan.FromDays(2);
    public TimeSpan SaveCacheInterval { get; set; } = TimeSpan.FromMinutes(5);
    public string SystemAuthorizationCode { get; set; } = "";
    public bool AllowRedirect { get; set; } = true;
}