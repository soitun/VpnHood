﻿namespace VpnHood.AccessServer;

public class AppOptions
{
    public const string AgentHttpClientName = "AgentHttpClient";
    public TimeSpan? AutoMaintenanceInterval { get; set; }
    public Uri AgentUrl { get; set; } = default!;
    public Uri? AgentUrlPrivate { get; set; }
    public string AgentSystemAuthorization { get; set; } = default!;
    public TimeSpan ServerUpdateStatusInterval { get; set; } = TimeSpan.FromSeconds(60); 
    public TimeSpan LostServerThreshold => ServerUpdateStatusInterval * 3; 
}