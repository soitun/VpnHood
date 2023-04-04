﻿using System;

namespace VpnHood.AccessServer.Dtos;

public class ServerStatusEx
{
    public int SessionCount { get; set; }
    public int TcpConnectionCount { get; set; }
    public int UdpConnectionCount { get; set; }
    public long? AvailableMemory { get; set; }
    public int? CpuUsage { get; set; }
    public int ThreadCount { get; set; }
    public long TunnelSendSpeed { get; set; }
    public long TunnelReceiveSpeed { get; set; }
    public DateTime CreatedTime { get; set; }
}
