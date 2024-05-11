﻿using GrayMint.Common.Utils;
using VpnHood.Common.Messaging;

namespace VpnHood.AccessServer.Dtos.AccessTokens;
public class AccessTokenUpdateParams
{
    public Patch<string>? AccessTokenName { get; set; }

    public Patch<Guid>? ServerFarmId { get; set; }

    public Patch<DateTime?>? ExpirationTime { get; set; }

    public Patch<int>? Lifetime { get; set; }

    public Patch<int>? MaxDevice { get; set; }

    public Patch<long>? MaxTraffic { get; set; }
    public Patch<bool>? IsEnabled { get; set; }
    public Patch<AdShow>? AdShow { get; set; }
    public Patch<string>? Description { get; set; }
}