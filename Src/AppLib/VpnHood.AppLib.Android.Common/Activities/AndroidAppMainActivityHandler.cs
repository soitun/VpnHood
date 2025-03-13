﻿using Android.Content;
using Android.Content.Res;
using Android.Runtime;
using Android.Views;
using VpnHood.AppLib.ClientProfiles;
using VpnHood.Core.Client.Device.Droid;
using VpnHood.Core.Client.Device.Droid.ActivityEvents;
using VpnHood.Core.Client.Device.Droid.Utils;
using VpnHood.Core.Client.Device.UiContexts;
using VpnHood.Core.Toolkit.Utils;
using Permission = Android.Content.PM.Permission;

namespace VpnHood.AppLib.Droid.Common.Activities;

public class AndroidAppMainActivityHandler
{
    private readonly string[] _accessKeySchemes;
    private readonly string[] _accessKeyMimes;
    protected IActivityEvent ActivityEvent { get; }

    public AndroidAppMainActivityHandler(IActivityEvent activityEvent, AndroidMainActivityOptions options)
    {
        ActivityEvent = activityEvent;
        _accessKeySchemes = options.AccessKeySchemes;
        _accessKeyMimes = options.AccessKeyMimes;

        activityEvent.CreateEvent += (_, args) => OnCreate(args.SavedInstanceState);
        activityEvent.NewIntentEvent += (_, args) => OnNewIntent(args.Intent);
        activityEvent.RequestPermissionsResultEvent += (_, args) =>
            OnRequestPermissionsResult(args.RequestCode, args.Permissions, args.GrantResults);
        activityEvent.ActivityResultEvent +=
            (_, args) => OnActivityResult(args.RequestCode, args.ResultCode, args.Data);
        activityEvent.KeyDownEvent += (_, args) => args.IsHandled = OnKeyDown(args.KeyCode, args.KeyEvent);
        activityEvent.PauseEvent += (_, _) => OnPause();
        activityEvent.ResumeEvent += (_, _) => OnResume();
        activityEvent.DestroyEvent += (_, _) => OnDestroy();
        activityEvent.ConfigurationChangedEvent += (_, args) => OnConfigurationChanged(args);
    }

    protected virtual void OnCreate(Bundle? savedInstanceState)
    {
        AppUiContext.Context = new AndroidUiContext(ActivityEvent);

        // initialize the window
        InitWindow();

        // process intent
        ProcessIntent(ActivityEvent.Activity.Intent);
    }

    private void InitWindow()
    {
        // set window colors such as status bar and navigation bar
        var backgroundColor = VpnHoodApp.Instance.Resources.Colors.WindowBackgroundColor?.ToAndroidColor();
        if (backgroundColor != null) {
            VhUtils.TryInvoke("SetStatusBarColor", () =>
                ActivityEvent.Activity.Window?.SetStatusBarColor(backgroundColor.Value));
            
            VhUtils.TryInvoke("SetNavigationBarColor", () =>
                ActivityEvent.Activity.Window?.SetNavigationBarColor(backgroundColor.Value));
        }

        // set window insets listener
        if (OperatingSystem.IsAndroidVersionAtLeast(30))
            ActivityEvent.Activity.Window?.DecorView.SetOnApplyWindowInsetsListener(new WebViewWindowInsetsListener());

    }

    protected virtual bool OnNewIntent(Intent? intent)
    {
        return ProcessIntent(intent);
    }

    private bool ProcessIntent(Intent? intent)
    {
        if (intent?.Data == null || ActivityEvent.Activity.ContentResolver == null)
            return false;

        // try to add the access key
        try {
            var uri = intent.Data;
            if (_accessKeySchemes.Contains(uri.Scheme, StringComparer.OrdinalIgnoreCase)) {
                ImportAccessKey(uri.ToString()!);
                return true;
            }

            // check mime
            var mimeType = ActivityEvent.Activity.ContentResolver.GetType(uri);
            if (!_accessKeyMimes.Contains(mimeType, StringComparer.OrdinalIgnoreCase)) {
                Toast.MakeText(ActivityEvent.Activity, VpnHoodApp.Instance.Resources.Strings.MsgUnsupportedContent,
                    ToastLength.Long)?.Show();
                return false;
            }

            // open stream
            using var inputStream = ActivityEvent.Activity.ContentResolver.OpenInputStream(uri);
            if (inputStream == null)
                throw new Exception("Can not open the intent file stream.");

            // read string into buffer maximum 5k
            var buffer = new byte[5 * 1024];
            var length = inputStream.Read(buffer);
            using var memoryStream = new MemoryStream(buffer, 0, length);
            var streamReader = new StreamReader(memoryStream);
            var accessKey = streamReader.ReadToEnd();

            ImportAccessKey(accessKey);
        }
        catch {
            Toast.MakeText(ActivityEvent.Activity, VpnHoodApp.Instance.Resources.Strings.MsgCantReadAccessKey,
                ToastLength.Long)?.Show();
        }

        return true;
    }

    protected void ImportAccessKey(string accessKey)
    {
        var profiles = VpnHoodApp.Instance.ClientProfileService.List();
        var profileInfo = VpnHoodApp.Instance.ClientProfileService.ImportAccessKey(accessKey).ToInfo();
        VpnHoodApp.Instance.UserSettings.ClientProfileId = profileInfo.ClientProfileId;

        var isNew = profiles.Any(x => x.ClientProfileId == profileInfo.ClientProfileId);
        var message = isNew
            ? string.Format(VpnHoodApp.Instance.Resources.Strings.MsgAccessKeyAdded, profileInfo.ClientProfileName)
            : string.Format(VpnHoodApp.Instance.Resources.Strings.MsgAccessKeyUpdated, profileInfo.ClientProfileName);

        Toast.MakeText(ActivityEvent.Activity, message, ToastLength.Long)?.Show();
    }

    protected virtual void OnRequestPermissionsResult(int requestCode, string[] permissions,
        [GeneratedEnum] Permission[] grantResults)
    {
    }

    protected virtual bool OnKeyDown([GeneratedEnum] Keycode keyCode, KeyEvent? e)
    {
        return false;
    }

    protected virtual void OnActivityResult(int requestCode, [GeneratedEnum] Result resultCode, Intent? data)
    {
    }

    protected virtual void OnConfigurationChanged(Configuration args)
    {
        VpnHoodApp.Instance.UpdateUi();
    }

    protected virtual void OnResume()
    {
    }

    protected virtual void OnPause()
    {
    }

    protected virtual void OnDestroy()
    {
        AppUiContext.Context = null;
    }
}