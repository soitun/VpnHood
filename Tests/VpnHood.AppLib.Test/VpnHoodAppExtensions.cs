﻿using System.Net;
using VpnHood.AppLib.Dtos;
using VpnHood.Core.Common.IpLocations;
using VpnHood.Core.Common.Tokens;
using VpnHood.Core.Toolkit.Utils;

namespace VpnHood.AppLib.Test;

public static class VpnHoodAppExtensions
{
    public static AppSessionStatus GetSessionStatus(this VpnHoodApp app)
    {
        app.ForceUpdateState().Wait();
        return app.State.SessionStatus ?? throw new InvalidOperationException("Session has not been initialized yet");
    }

    public static Task WaitForState(this VpnHoodApp app, AppConnectionState connectionSate, int timeout = 5000)
    {
        return VhTestUtil.AssertEqualsWait(connectionSate, () => app.State.ConnectionState,
            "App state didn't reach the expected value.", timeout);
    }

    public static Task Connect(this VpnHoodApp app, 
        Guid? clientProfileId, 
        ConnectPlanId planId = ConnectPlanId.Normal, 
        bool diagnose = false,
        CancellationToken cancellationToken = default)
    {
        return app.Connect(new ConnectOptions {
            ClientProfileId = clientProfileId,
            PlanId = planId,
            Diagnose = diagnose
        }, cancellationToken);
    }

    public static void UpdateClientCountry(this VpnHoodApp app, string countryCode)
    {
        app.SettingsService.AppSettings.ClientIpLocation = new IpLocation {
            IpAddress = IPAddress.Parse("1.2.3.4"), // Dummy IP address
            CountryCode = countryCode,
            CountryName = VhUtils.TryGetCountryName(countryCode) ?? "Unknown",
            CityName = null,
            RegionName = null
        };
    }
}