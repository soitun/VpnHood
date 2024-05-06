﻿using System.Text.Json;
using VpnHood.Client.App.Abstractions;
using VpnHood.Common.Logging;
using VpnHood.Common.Utils;

namespace VpnHood.Client.App.Services;

internal class AppAccountService(VpnHoodApp vpnHoodApp, IAppAccountService accountService)
    : IAppAccountService
{
    private AppAccount? _appAccount;
    private string AppAccountFilePath => Path.Combine(vpnHoodApp.StorageFolderPath, "account", "account.json");

    public IAppAuthenticationService Authentication { get; } =
        new AppAuthenticationService(vpnHoodApp, accountService.Authentication);

    public IAppBillingService? Billing { get; } = accountService.Billing != null
        ? new AppBillingService(vpnHoodApp, accountService.Billing) : null;

    public async Task<AppAccount?> GetAccount()
    {
        if (Authentication.UserId == null)
            return null;

        // Get from local cache
        _appAccount ??= VhUtil.JsonDeserializeFile<AppAccount>(AppAccountFilePath, logger: VhLogger.Instance);
        if (_appAccount != null)
            return _appAccount;

        // Update cache from server and update local cache
        _appAccount = await accountService.GetAccount();
        Directory.CreateDirectory(Path.GetDirectoryName(AppAccountFilePath)!);
        await File.WriteAllTextAsync(AppAccountFilePath, JsonSerializer.Serialize(_appAccount));

        return _appAccount;
    }

    public void ClearCache()
    {
        _appAccount = null;
    }

    public Task<bool> IsSubscriptionOrderProcessed(string providerOrderId)
    {
        return accountService.IsSubscriptionOrderProcessed(providerOrderId);
    }

    public Task<string[]> GetAccessKeys(string subscriptionId)
    {
        return accountService.GetAccessKeys(subscriptionId);
    }
}