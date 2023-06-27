﻿using VpnHood.Common.Utils;

namespace VpnHood.AccessServer.Dtos;

public class ProjectUpdateParams
{
    public Patch<string?>? ProjectName { get; set; }
    public Patch<string?>? GaMeasurementId { get; set; }
    public Patch<string?>? GaApiSecret { get; set; }
    public Patch<int>? MaxTcpCount { get; set; }
}
