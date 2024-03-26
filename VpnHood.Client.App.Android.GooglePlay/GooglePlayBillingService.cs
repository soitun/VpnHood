using Android.BillingClient.Api;
using Microsoft.Extensions.Logging;
using Org.Apache.Http.Authentication;
using VpnHood.Client.App.Abstractions;
using VpnHood.Common.Logging;

namespace VpnHood.Client.App.Droid.GooglePlay;

public class GooglePlayBillingService: IAppBillingService
{
    private readonly BillingClient _billingClient;
    private readonly Activity _activity;
    private readonly IAppAuthenticationService _authenticationService;
    private ProductDetails? _productDetails;
    private IList<ProductDetails.SubscriptionOfferDetails>? _subscriptionOfferDetails;
    private TaskCompletionSource<string>? _taskCompletionSource;
    private GooglePlayBillingService(Activity activity, IAppAuthenticationService authenticationService)
    {
        var builder = BillingClient.NewBuilder(activity);
        builder.SetListener(PurchasesUpdatedListener);
        _billingClient = builder.EnablePendingPurchases().Build();
        _activity = activity;
        _authenticationService = authenticationService;
    }

    public static GooglePlayBillingService Create(Activity activity, IAppAuthenticationService authenticationService)
    {
        return new GooglePlayBillingService(activity, authenticationService);
    }

    private void PurchasesUpdatedListener(BillingResult billingResult, IList<Purchase> purchases)
    {
        switch (billingResult.ResponseCode)
        {
            case BillingResponseCode.Ok:
                if (purchases.Any())
                    _taskCompletionSource?.TrySetResult(purchases.First().OrderId);
                else
                    _taskCompletionSource?.TrySetException(new Exception("There is no any order."));
                break;
            case BillingResponseCode.UserCancelled:
                _taskCompletionSource?.TrySetCanceled();
                break;  
            default:
                _taskCompletionSource?.TrySetException(CreateBillingResultException(billingResult));
                break;
        }
    }

    public async Task<SubscriptionPlan[]> GetSubscriptionPlans()
    {
        await EnsureConnected();

        // Check if the purchase subscription is supported on the user's device
        try
        {
            var isDeviceSupportSubscription = _billingClient.IsFeatureSupported(BillingClient.FeatureType.Subscriptions);
            if (isDeviceSupportSubscription.ResponseCode == BillingResponseCode.FeatureNotSupported)
                throw new Exception("Subscription feature is not supported on this device.");
        }
        catch (Exception ex)
        {
            VhLogger.Instance.LogError(ex, "Could not check supported feature with google play.");
            throw;
        }

        // Set list of the created products in the GooglePlay.
        var productDetailsParams = QueryProductDetailsParams.NewBuilder()
            .SetProductList([
                QueryProductDetailsParams.Product.NewBuilder()
                    .SetProductId("general_subscription")
                    .SetProductType(BillingClient.ProductType.Subs)
                    .Build()
            ])
            .Build();

        // Get products list from GooglePlay.
        try
        {
            var response = await _billingClient.QueryProductDetailsAsync(productDetailsParams);
            if (response.Result.ResponseCode != BillingResponseCode.Ok) throw new Exception($"Could not get products from google play. BillingResponseCode: {response.Result.ResponseCode}");
            if (!response.ProductDetails.Any()) throw new Exception($"Product list is empty. ProductList: {response.ProductDetails}");

            var productDetails = response.ProductDetails.First();
            _productDetails = productDetails;

            var plans = productDetails.GetSubscriptionOfferDetails();
            _subscriptionOfferDetails = plans;

            var subscriptionPlans = plans
                .Where(plan => plan.PricingPhases.PricingPhaseList.Any())
                .Select(plan => new SubscriptionPlan
                {
                    SubscriptionPlanId = plan.BasePlanId,
                    PlanPrice = plan.PricingPhases.PricingPhaseList.First().FormattedPrice
                })
                .ToArray();

            return subscriptionPlans;
        }
        catch (Exception ex)
        {
            VhLogger.Instance.LogError(ex, "Could not get products from google play.");
            throw;
        }
    }

    public async Task<string> Purchase(string planId)
    {
        await EnsureConnected();

        if (_authenticationService.UserId == null)
            throw new AuthenticationException();

        var offerToken = _subscriptionOfferDetails == null 
            ? throw new NullReferenceException("Could not found subscription offer details.") 
            : _subscriptionOfferDetails
            .Where(x => x.BasePlanId == planId)
            .Select(x => x.OfferToken)
            .Single();

        var productDetailsParam = BillingFlowParams.ProductDetailsParams.NewBuilder()
            .SetProductDetails(_productDetails ?? throw new NullReferenceException("Could not found product details."))
            .SetOfferToken(offerToken)
            .Build();

        var billingFlowParams = BillingFlowParams.NewBuilder()
            .SetObfuscatedAccountId(_authenticationService.UserId)
            .SetProductDetailsParamsList([productDetailsParam])
            .Build();


        var billingResult = _billingClient.LaunchBillingFlow(_activity, billingFlowParams);

        if (billingResult.ResponseCode != BillingResponseCode.Ok)
            throw CreateBillingResultException(billingResult);

        try
        {
            _taskCompletionSource = new TaskCompletionSource<string>();
            var orderId = await _taskCompletionSource.Task;
            return orderId;
        }
        catch (Exception ex)
        {
            VhLogger.Instance.LogError(ex, "Could not get order id from google play LaunchBillingFlow.");
            throw;
        }
    }

    private async Task EnsureConnected()
    {
        if (_billingClient.IsReady)
         return;

        try
        {
            var billingResult = await _billingClient.StartConnectionAsync();

            if (billingResult.ResponseCode != BillingResponseCode.Ok)
                throw new Exception(billingResult.DebugMessage);
        }
        catch (Exception ex)
        {
            VhLogger.Instance.LogError(ex, "Could not start connection to google play.");
            throw;
        }
    }

    public void Dispose()
    {
        _billingClient.Dispose();
    }

    private static Exception CreateBillingResultException(BillingResult billingResult)
    {
        if (billingResult.ResponseCode == BillingResponseCode.Ok)
            throw new InvalidOperationException("Response code should be not OK.");

        return new Exception(billingResult.DebugMessage)
        {
            Data = { { "ResponseCode", billingResult.ResponseCode } }
        };
    }
}