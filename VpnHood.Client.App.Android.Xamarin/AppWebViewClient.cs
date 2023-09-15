﻿#nullable enable
using System;
using Android.Webkit;
using Xamarin.Essentials;

namespace VpnHood.Client.App.Droid;

internal class AppWebViewClient : WebViewClient
{
    public bool BrowseLinkInExternalBrowser { get; set; } = false;
    public event EventHandler? PageLoaded;


    // used for Window.Open such as SendReport
    public override bool ShouldOverrideUrlLoading(WebView? webView, IWebResourceRequest? request)
    {
        if (webView == null || request?.Url == null)
            return false;

        var options = new BrowserLaunchOptions
        {
            TitleMode = BrowserTitleMode.Hide,
            LaunchMode = BrowseLinkInExternalBrowser
                ? BrowserLaunchMode.External
                : BrowserLaunchMode.SystemPreferred
        };
        Browser. OpenAsync(request.Url.ToString(), options);
        return true;
    }

    public override void OnPageFinished(WebView? view, string? url)
    {
        base.OnPageFinished(view, url);
        PageLoaded?.Invoke(this, EventArgs.Empty);
    }
}