﻿using System.Globalization;
using VpnHood.Client.App.Abstractions;

namespace VpnHood.Client.App;

internal class AppAppCultureService(VpnHoodApp vpnHoodApp) : IAppCultureService
{
    public string[] SystemCultures => [CultureInfo.InstalledUICulture.Name];
    public string[] AvailableCultures { get; set; } = [];
    public string[] SelectedCultures
    {
        get => vpnHoodApp.UserSettings.CultureCode != null ? [vpnHoodApp.UserSettings.CultureCode] : [];
        set => vpnHoodApp.UserSettings.CultureCode = value.FirstOrDefault();
    }
}