﻿using System;

namespace VpnHood.AccessServer;

public static class QuotaConstants
{
    public static int ProjectCount { get; set; } = 2;
    public static int ServerCount { get; set; } = 10;
    public static int AccessTokenCount { get; set; } = 2000;
    public static int ServerFarmCount { get; set; } = 100;
    public static int AccessPointCount { get; set; } = 10;
    public static int CertificateCount { get; set; } = ServerFarmCount;
    public static TimeSpan UsageQueryTimeSpanFree { get; set; } = TimeSpan.FromDays(33);
    public static TimeSpan UsageQueryTimeSpanPremium { get; set; } = TimeSpan.FromDays(33 * 6);
}