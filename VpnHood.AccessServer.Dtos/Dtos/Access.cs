﻿namespace VpnHood.AccessServer.Dtos;

public class Access
{
    public Guid AccessId { get; set; }
    public Guid AccessTokenId { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastUsedTime { get; set; }
    public string? Description { get; set; }
    public long CycleSentTraffic { get; set; }
    public long CycleReceivedTraffic { get; set; }
    public long CycleTraffic { get; set; }
    public long TotalSentTraffic { get; set; }
    public long TotalReceivedTraffic { get; set; }
    public long TotalTraffic { get; set; }
}