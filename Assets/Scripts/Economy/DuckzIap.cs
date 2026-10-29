using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Platform;
using Oculus.Platform.Models;
using UnityEngine;

// Durable Meta Quest IAPs. Ownership is recorded server-side via /duckz/iap/grant, which verifies with Meta.
// Purchases are read from Meta online, or from the device cache when offline, at sign-in and again when the app
// resumes, so something bought outside the app (e.g. the Store on the phone) unlocks without a restart.
public class DuckzIap : MonoBehaviour
{
    const float WalletWaitSeconds = 120f;
    const float ResumeRecheckSeconds = 60f;

    public static DuckzIap Instance { get; private set; }

    /// <summary>True once purchases have been read from Meta (or Meta was unavailable) at least once.</summary>
    public bool RestoreFinished { get; private set; }
    /// <summary>Durable SKUs Meta says this account owns, as of the last restore or checkout.</summary>
    public IReadOnlyCollection<string> OwnedSkus => owned;
    public event Action OnPricesChanged;

    readonly HashSet<string> owned = new HashSet<string>();
    readonly Dictionary<string, string> prices = new Dictionary<string, string>();
    bool restoreRequested;
    bool restoring;
    bool restoreQueued;
    bool loadingPrices;
    bool pricesLoaded;
    float lastCheck = float.NegativeInfinity;

    static StoreCatalog Catalog => GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;

    public static DuckzIap Ensure()
    {
        if (Instance != null)
            return Instance;
        return new GameObject(nameof(DuckzIap)).AddComponent<DuckzIap>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnApplicationPause(bool paused)
    {
        if (!paused)
            OnResumed();
    }

    void OnApplicationFocus(bool focused)
    {
        if (focused)
            OnResumed();
    }

    void OnResumed()
    {
        if (!restoreRequested)
        {
            DuckzLog.Log("[Iap] App resumed before sign-in, so the purchase check waits until then");
            return;
        }
        float wait = ResumeRecheckSeconds - (Time.realtimeSinceStartup - lastCheck);
        if (wait > 0f)
        {
            DuckzLog.Log($"[Iap] App resumed, next purchase check in {wait:0}s");
            return;
        }
        DuckzLog.Log("[Iap] App resumed, checking Meta for purchases made outside the app");
        RestoreDurablePurchases();
    }

    /// <summary>Localized price from Meta, e.g. "$14.99". False until GetProductsBySKU has answered.</summary>
    public bool TryGetPrice(string sku, out string price)
    {
        price = null;
        return !string.IsNullOrEmpty(sku) && prices.TryGetValue(sku, out price) && !string.IsNullOrEmpty(price);
    }

    public bool Owns(string sku) => !string.IsNullOrEmpty(sku) && owned.Contains(sku);

    public void Purchase(string sku, Action<bool> completed)
    {
        StartCoroutine(PurchaseRoutine(sku, completed));
    }

    IEnumerator PurchaseRoutine(string sku, Action<bool> completed)
    {
        if (!Core.IsInitialized())
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice1, sku))
                    logNotice1.Write($"[Iap] Checkout for '{sku}' skipped: Meta Platform isn't initialized");
            }
            DuckzAnalytics.IapFailed(sku, "platform_not_initialized");
            completed?.Invoke(false);
            yield break;
        }

        DuckzLog.Log($"[Iap] Opening checkout for '{sku}'");
        Message<Purchase> message = null;
        yield return Await(() => IAP.LaunchCheckoutFlow(sku), m => message = m);

        if (message == null || message.IsError || message.Data == null)
        {
            if (!IsUserCancellation(message) && DuckzLog.AllowWarning(out var logNotice2, sku))
                logNotice2.Write($"[Iap] Checkout for '{sku}' failed ({ErrorText(message)}). Reading purchases again in case it was already bought.");
            // Meta refuses a second checkout for a durable SKU; if that's why, a fresh read unlocks it.
            RestoreDurablePurchases();
            DuckzAnalytics.IapFailed(sku, ErrorText(message));
            completed?.Invoke(false);
            yield break;
        }

        string bought = message.Data.Sku;
        DuckzLog.Log($"[Iap] Bought '{bought}' (purchase {message.Data.ID}), granting on server");
        if (owned.Add(bought))
        {
            DuckzWallet wallet = DuckzWallet.Instance;
            if (wallet != null)
                wallet.MarkPlatformOwned(bought);
        }
        StoreBundle bundle = Catalog != null ? Catalog.FindBundle(bought) : null;
        int cents = DuckzAnalytics.ParsePriceCents(bundle != null ? bundle.displayPrice : null);
        if (cents <= 0)
            cents = 999;
        DuckzAnalytics.IapPurchased(bought, cents);
        completed?.Invoke(true);

        // Meta already owns it; if this grant fails, the next restore retries it.
        yield return GrantRoutine(bought, message.Data.ID);
    }

    /// <summary>Reads purchases from Meta and loads prices. Called at sign-in, and again on resume.</summary>
    public void RestoreDurablePurchases()
    {
        restoreRequested = true;
        lastCheck = Time.realtimeSinceStartup;
        DuckzLog.Log($"[Iap] Restore starting. Meta Platform initialized: {Core.IsInitialized()}, prices loaded: {pricesLoaded}");
        if (!pricesLoaded && !loadingPrices)
            StartCoroutine(LoadPricesRoutine());
        if (restoring)
        {
            DuckzLog.Log("[Iap] A restore is already running, this one waits for it");
            restoreQueued = true;
            return;
        }
        StartCoroutine(RestoreRoutine());
    }

    IEnumerator RestoreRoutine()
    {
        restoring = true;
        do
        {
            restoreQueued = false;
            yield return RestoreOnce();
        } while (restoreQueued);
        restoring = false;
    }

    IEnumerator RestoreOnce()
    {
        if (!Core.IsInitialized())
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice3))
                    logNotice3.Write("[Iap] Restore skipped: Meta Platform isn't initialized");
            }
            RestoreFinished = true;
            yield break;
        }

        List<Purchase> purchases = null;
        string source = "Meta online";
        yield return FetchPurchases(source, IAP.GetViewerPurchases, list => purchases = list);
        if (purchases == null)
        {
            source = "device cache";
            {
                if (DuckzLog.AllowWarning(out var logNotice4))
                    logNotice4.Write("[Iap] Online purchase read failed, trying the device cache");
            }
            yield return FetchPurchases(source, IAP.GetViewerPurchasesDurableCache, list => purchases = list);
        }
        if (purchases == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice5))
                    logNotice5.Write($"[Iap] Couldn't read purchases from Meta or the device cache, keeping [{string.Join(", ", owned)}]");
            }
            RestoreFinished = true;
            yield break;
        }

        StoreCatalog catalog = Catalog;
        if (catalog == null)
            {
                if (DuckzLog.AllowWarning(out var logNotice6))
                    logNotice6.Write("[Iap] Store catalog isn't loaded, so every Meta purchase is kept");
            }
        var known = new List<Purchase>();
        owned.Clear();
        foreach (Purchase purchase in purchases)
        {
            if (purchase == null || string.IsNullOrEmpty(purchase.Sku))
                continue;
            if (catalog != null && !catalog.IsIapSku(purchase.Sku))
            {
                DuckzLog.Log($"[Iap] Ignoring purchase '{purchase.Sku}': it isn't a bundle or IAP item in the store catalog");
                continue;
            }
            known.Add(purchase);
            owned.Add(purchase.Sku);
        }
        DuckzLog.Log($"[Iap] Purchases kept from {source}: [{string.Join(", ", owned)}]");
        RestoreFinished = true;

        DuckzWallet wallet = DuckzWallet.Instance;
        if (wallet != null)
            wallet.SetPlatformOwned(owned);

        if (known.Count == 0)
            yield break;
        float deadline = Time.realtimeSinceStartup + WalletWaitSeconds;
        while ((DuckzWallet.Instance == null || !DuckzWallet.Instance.Loaded) && Time.realtimeSinceStartup < deadline)
            yield return null;
        wallet = DuckzWallet.Instance;
        if (wallet == null || !wallet.Loaded)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice7))
                    logNotice7.Write("[Iap] Wallet never loaded, server grants wait for the next restore");
            }
            yield break;
        }

        foreach (Purchase purchase in known)
        {
            if (wallet.ServerHasEntitlement(purchase.Sku))
            {
                DuckzLog.Log($"[Iap] Server already has '{purchase.Sku}', no grant needed");
                continue;
            }
            DuckzLog.Log($"[Iap] Server doesn't have '{purchase.Sku}' yet, granting (purchase {purchase.ID})");
            yield return GrantRoutine(purchase.Sku, purchase.ID);
        }
    }

    // Online reads come in pages; null means the read failed.
    static IEnumerator FetchPurchases(string source, Func<Request<PurchaseList>> first, Action<List<Purchase>> done)
    {
        DuckzLog.Log($"[Iap] Reading purchases from {source}");
        var all = new List<Purchase>();
        Message<PurchaseList> message = null;
        yield return Await(first, m => message = m);
        while (true)
        {
            if (message == null || message.IsError || message.Data == null)
            {
                {
                    if (DuckzLog.AllowWarning(out var logNotice8, source))
                        logNotice8.Write($"[Iap] Purchase read from {source} failed ({ErrorText(message)})");
                }
                done(null);
                yield break;
            }
            PurchaseList page = message.Data;
            foreach (Purchase purchase in page)
                all.Add(purchase);
            if (!page.HasNextPage)
                break;
            yield return Await(() => IAP.GetNextPurchaseListPage(page), m => message = m);
        }
        done(all);
    }

    IEnumerator LoadPricesRoutine()
    {
        StoreCatalog catalog = Catalog;
        List<string> skus = catalog != null ? catalog.IapSkus() : new List<string>();
        if (!Core.IsInitialized())
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice9))
                    logNotice9.Write("[Iap] Price lookup skipped: Meta Platform isn't initialized. The wall shows the catalog price.");
            }
            yield break;
        }
        if (catalog == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice10))
                    logNotice10.Write("[Iap] Price lookup skipped: the store catalog isn't loaded");
            }
            yield break;
        }
        if (skus.Count == 0)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice11))
                    logNotice11.Write("[Iap] Price lookup skipped: the store catalog has no bundles or IAP items");
            }
            yield break;
        }

        DuckzLog.Log($"[Iap] Asking Meta for prices: [{string.Join(", ", skus)}]");
        loadingPrices = true;
        Message<ProductList> message = null;
        yield return Await(() => IAP.GetProductsBySKU(skus.ToArray()), m => message = m);
        loadingPrices = false;
        if (message == null || message.IsError || message.Data == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice12))
                    logNotice12.Write($"[Iap] Price lookup for [{string.Join(", ", skus)}] failed, showing catalog prices ({ErrorText(message)})");
            }
            yield break;
        }

        foreach (Product product in message.Data)
            if (product != null && !string.IsNullOrEmpty(product.Sku))
                prices[product.Sku] = product.FormattedPrice;
        bool complete = true;
        foreach (string sku in skus)
            if (!prices.ContainsKey(sku) || string.IsNullOrEmpty(prices[sku]))
            {
                complete = false;
                {
                    if (DuckzLog.AllowWarning(out var logNotice13, sku))
                        logNotice13.Write($"[Iap] Meta returned no price for '{sku}'. Check the SKU exists and this account can see it. Asking again on the next restore.");
                }
            }
        pricesLoaded = complete;
#if DUCKZ_DIAGNOSTICS
        var shown = new List<string>();
        foreach (KeyValuePair<string, string> price in prices)
            shown.Add(price.Key + " " + price.Value);
        DuckzLog.Log($"[Iap] Prices from Meta: [{string.Join(", ", shown)}]");
#endif
        OnPricesChanged?.Invoke();
    }

    static IEnumerator GrantRoutine(string sku, string purchaseId)
    {
        bool done = false;
        DuckzWallet wallet = DuckzWallet.Instance;
        if (wallet == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice14, sku))
                    logNotice14.Write($"[Iap] Can't grant '{sku}' (purchase {purchaseId}): the wallet isn't loaded. The next restore retries it.");
            }
            yield break;
        }
        wallet.GrantIap(sku, purchaseId, ok =>
        {
            DuckzLog.Log($"[Iap] Server grant '{sku}' {(ok ? "succeeded" : "failed, will retry on the next restore")}");
            done = true;
        });
        while (!done)
            yield return null;
    }

    static IEnumerator Await<T>(Func<Request<T>> start, Action<Message<T>> done)
    {
        Message<T> message = null;
        bool finished = false;
        try
        {
            Request<T> request = start();
            if (request == null)
            {
                done(null);
                yield break;
            }
            request.OnComplete(m => { message = m; finished = true; });
        }
        catch (Exception exception)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice15))
                    logNotice15.Write("[Iap] Meta request failed to start: " + exception.Message);
            }
            done(null);
            yield break;
        }

        while (!finished)
            yield return null;
        done(message);
    }

    [Serializable] sealed class CheckoutError { public string category; }

    // Documented by the installed Meta IAP SDK. Unknown/malformed errors remain visible.
    static bool IsUserCancellation<T>(Message<T> message)
    {
        string error = message != null && message.IsError ? message.GetError()?.Message : null;
        if (string.IsNullOrEmpty(error) || !error.TrimStart().StartsWith("{", StringComparison.Ordinal))
            return false;
        try { return JsonUtility.FromJson<CheckoutError>(error)?.category == "user_canceled"; }
        catch (ArgumentException) { return false; }
    }

    static string ErrorText<T>(Message<T> message)
    {
        if (message == null)
            return "no reply";
        Error error = message.GetError();
        if (error == null)
            return message.Data == null ? "empty reply" : "ok";
        return $"code {error.Code}, http {error.HttpCode}, {error.Message}";
    }
}
