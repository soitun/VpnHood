﻿using VpnHood.Common.Utils;

namespace VpnHood.AccessServer.Dtos.ServerFarm;

public class ServerFarmUpdateParams
{
    public Patch<string>? ServerFarmName { get; set; }
    public Patch<Guid>? ServerProfileId { get; set; }
    public Patch<bool>? UseHostName { get; set; }
    public Patch<Uri>? TokenUrl { get; set; }
    public Patch<byte[]>? Secret { get; set; }
    public Patch<bool>? PushTokenToClient { get; set; }
}