﻿using Android.Runtime;
using VpnHood.App.Client;
using VpnHood.AppLib;
using VpnHood.AppLib.Droid.Common;
using VpnHood.AppLib.Droid.Common.Constants;
using VpnHood.Core.Client.Device.Droid.Utils;

namespace VpnHood.App.Connect.Droid.Web;

[Application(
    Label = AppConfigs.AppName,
    Icon = AndroidAppConstants.Icon,
    Banner = AndroidAppConstants.Banner,
    NetworkSecurityConfig = AndroidAppConstants.NetworkSecurityConfig,
    SupportsRtl = AndroidAppConstants.SupportsRtl,
    AllowBackup = AndroidAppConstants.AllowBackup)]
public class App(IntPtr javaReference, JniHandleOwnership transfer)
    : VpnHoodAndroidApp(javaReference, transfer)
{
    protected override AppOptions CreateAppOptions()
    {
        var appConfigs = AppConfigs.Load();

        // load app settings and resources
        var resources = ConnectAppResources.Resources;
        resources.Strings.AppName = AppConfigs.AppName;

        return new AppOptions(appId: PackageName!, "VpnHoodConnect", AppConfigs.IsDebugMode) {
            DeviceId = AndroidUtil.GetDeviceId(this), //this will be hashed using AppId
            AccessKeys = [appConfigs.DefaultAccessKey],
            Resources = resources,
            UiName = "VpnHoodConnect",
            IsAddAccessKeySupported = false,
            UpdateInfoUrl = appConfigs.UpdateInfoUrl,
            AllowEndPointTracker = appConfigs.AllowEndPointTracker,
            Ga4MeasurementId = appConfigs.Ga4MeasurementId,
            AdjustForSystemBars = false
        };
    }
}