﻿using System;
using VpnHood.Common.Net;

namespace VpnHood.Client.App;

public class UserSettings
{
    public bool LogToFile { get; set; } = false;
    public bool LogVerbose { get; set; } = true;
    // ReSharper disable once UnusedMember.Global
    public string CultureName { get; set; } = "en";
    public Guid? DefaultClientProfileId { get; set; }
    public int MaxReconnectCount { get; set; } = 3;
    public int MaxDatagramChannelCount { get; set; } = new ClientOptions().MaxDatagramChannelCount;
    public bool TunnelClientCountry { get; set; } = true;
    public string[]? IpGroupFilters { get; set; }
    public FilterMode IpGroupFiltersMode { get; set; } = FilterMode.All;
    public IpRange[]? CustomIpRanges { get; set; }
    public string[]? AppFilters { get; set; }
    public FilterMode AppFiltersMode { get; set; } = FilterMode.All;
    public bool UseUdpChannel { get; set; } = new ClientOptions().UseUdpChannel;
    public bool IncludeLocalNetwork { get; set; } = new ClientOptions().IncludeLocalNetwork;
    public IpRange[]? PacketCaptureIpRanges { get; set; }
    public FilterMode PacketCaptureIpRangesFilterMode { get; set; }
}