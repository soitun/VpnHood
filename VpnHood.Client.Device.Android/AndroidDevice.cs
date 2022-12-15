﻿#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Android.App;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.OS;
using static Android.Graphics.Bitmap;

namespace VpnHood.Client.Device.Android
{
    public class AndroidDevice : IDevice
    {
        private readonly EventWaitHandle _grantPermissionWaitHandle = new(false, EventResetMode.AutoReset);
        private readonly EventWaitHandle _serviceWaitHandle = new(false, EventResetMode.AutoReset);
        private IPacketCapture? _packetCapture;
        private bool _permissionGranted;

        public AndroidDevice()
        {
            if (Current != null)
                throw new InvalidOperationException($"Only one {nameof(AndroidDevice)} can be created!");
            Current = this;
        }

        public static AndroidDevice? Current { get; private set; }

        public event EventHandler? OnStartAsService;

        public string OperatingSystemInfo => $"{Build.Manufacturer}: {Build.Model}, Android: {Build.VERSION.Release}";

        public DeviceAppInfo[] InstalledApps
        {
            get
            {
                var deviceAppInfos = new List<DeviceAppInfo>();
                var packageManager = Application.Context.PackageManager ??
                                     throw new Exception("Could not acquire PackageManager!");
                var intent = new Intent(Intent.ActionMain);
                intent.AddCategory(Intent.CategoryLauncher);
                var resolveInfoList = packageManager.QueryIntentActivities(intent, 0);
                foreach (var resolveInfo in resolveInfoList)
                {
                    if (resolveInfo.ActivityInfo == null)
                        continue;

                    var appName = resolveInfo.LoadLabel(packageManager);
                    var appId = resolveInfo.ActivityInfo.PackageName;
                    var icon = resolveInfo.LoadIcon(packageManager);
                    if (appName is "" or null || appId is "" or null || icon == null)
                        continue;

                    var deviceAppInfo = new DeviceAppInfo(
                        appId,
                        appName,
                        EncodeToBase64(icon, 100)
                    );
                    deviceAppInfos.Add(deviceAppInfo);
                }

                return deviceAppInfos.ToArray();
            }
        }

        public bool IsExcludeAppsSupported => true;

        public bool IsIncludeAppsSupported => true;

        public Task<IPacketCapture> CreatePacketCapture()
        {
            return Task.Run(() =>
            {
                // Grant for permission if OnRequestVpnPermission is registered otherwise let service throw the error
                if (OnRequestVpnPermission != null)
                {
                    _permissionGranted = false;
                    OnRequestVpnPermission.Invoke(this, EventArgs.Empty);
                    _grantPermissionWaitHandle.WaitOne(10000);
                    if (!_permissionGranted)
                        throw new Exception("Could not grant VPN permission in the given time!");
                }

                StartService();
                _serviceWaitHandle.WaitOne(10000);
                if (_packetCapture == null)
                    throw new Exception("Could not start VpnService in the given time!");

                return Task.FromResult(_packetCapture);
            });
        }

        public event EventHandler? OnRequestVpnPermission;

        private static string EncodeToBase64(Drawable drawable, int quality)
        {
            var bitmap = DrawableToBitmap(drawable);
            var stream = new MemoryStream();
            if (!bitmap.Compress(CompressFormat.Png, quality, stream))
                throw new Exception("Could not compress bitmap to png!");
            return Convert.ToBase64String(stream.ToArray());
        }

        private static Bitmap DrawableToBitmap(Drawable drawable)
        {
            if (drawable is BitmapDrawable {Bitmap: { }} drawable1)
                return drawable1.Bitmap;

            //var bitmap = CreateBitmap(drawable.IntrinsicWidth, drawable.IntrinsicHeight, Config.Argb8888);
            var bitmap = CreateBitmap(32, 32, Config.Argb8888!)!;
            var canvas = new Canvas(bitmap);
            drawable.SetBounds(0, 0, canvas.Width, canvas.Height);
            drawable.Draw(canvas);

            return bitmap;
        }

        public void VpnPermissionGranted()
        {
            _permissionGranted = true;
            _grantPermissionWaitHandle.Set();
        }

        public void VpnPermissionRejected()
        {
            _grantPermissionWaitHandle.Set();
        }

        internal void OnServiceStartCommand(IPacketCapture packetCapture, Intent? intent)
        {
            _packetCapture = packetCapture;
            _serviceWaitHandle.Set();

            // fire AutoCreate for always on
            var manual = intent?.GetBooleanExtra("manual", false) ?? false;
            if (!manual)
                OnStartAsService?.Invoke(this, EventArgs.Empty);
        }

        private void StartService()
        {
            var intent = new Intent(Application.Context, typeof(AndroidPacketCapture));
            intent.PutExtra("manual", true);
            Application.Context.StartService(intent.SetAction("connect"));
        }
    }
}