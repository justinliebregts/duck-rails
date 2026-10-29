using System;
using System.Collections;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

public class DuckzWallet : MonoBehaviour
{
    public enum BuyStatus { Bought, AlreadyOwned, InsufficientCoins, Failed }

    [Serializable] class WalletData { public int coins; public string[] entitlements; public string[] purchases; public string equipped_skin; public bool has_loadout; }
    [Serializable] class LoadoutBody { public string skin_key; }
    [Serializable] class LoadoutData { public string skin_key; }
    [Serializable] class ServerStoreItemData { public string key; public int price; public bool durable; }
    [Serializable] class ServerStoreList { public ServerStoreItemData[] items; }
    struct ListedItem { public int price; public bool durable; }
    [Serializable] class DepositBody { public string request_id; public int bars; }
    [Serializable] class DepositData { public int coins; public int credited; public bool idempotent; }
    [Serializable] class BuyBody { public string request_id; public string item_key; }
    [Serializable] class BuyData { public int coins; public string item_key; public bool already_owned; public bool idempotent; }
    [Serializable] class GrantBody { public string sku; public string platform_txn_id; }
    [Serializable] class GrantData { public string[] entitlements; public bool idempotent; }

    const float DepositBatchSeconds = 0.5f;
    const int MaxBarsPerRequest = 12;
    const string PendingDepositsKey = "duckz.pendingDeposits";

    public static DuckzWallet Instance { get; private set; }

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public int Coins { get; private set; }
    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public bool Loaded { get; private set; }
    /// <summary>Equipped skin key saved on the server ("" = default duck), as of the last wallet load.</summary>
    public string ServerEquippedSkin { get; private set; } = "";
    /// <summary>False until this account has saved a loadout on the server.</summary>
    public bool ServerHasLoadout { get; private set; }
    public event Action OnChanged;

    readonly HashSet<string> entitlements = new HashSet<string>();
    readonly HashSet<string> purchases = new HashSet<string>();
    readonly HashSet<string> platformOwned = new HashSet<string>();
    readonly Dictionary<string, ListedItem> listed = new Dictionary<string, ListedItem>();

    int batchedBars;
    Coroutine batchRoutine;
    bool flushingPending;

    public static DuckzWallet Ensure()
    {
        if (Instance != null)
            return Instance;
        return FindFirstObjectByType<DuckzWallet>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice1))
                    logNotice1.Write("Duplicate DuckzWallet removed.", this);
            }
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Start()
    {
        // Sign-in reads Meta purchases before this object exists.
        if (DuckzIap.Instance != null && DuckzIap.Instance.RestoreFinished)
            SetPlatformOwned(DuckzIap.Instance.OwnedSkus);
        StartCoroutine(LoadWhenReady());
    }

    // Sign-in finishes in the login scene, before this object exists. Pull the saved balance
    // once the session is here, including after Quackville loads.
    IEnumerator LoadWhenReady()
    {
        while (!DuckzApiClient.IsReady)
            yield return null;
        Refresh();
    }

    /// <summary>Owned through a coin purchase or an IAP. Bundle items count only through their bundle's SKU.</summary>
    public bool Owns(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;
        StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
        StoreItem item = catalog != null ? catalog.Find(key) : null;
        if (item != null && item.InBundle)
            return OwnsSku(item.bundleSku);
        return entitlements.Contains(key) || purchases.Contains(key) || platformOwned.Contains(key);
    }

    /// <summary>A durable Meta SKU the server has granted, or Meta reports on this device.</summary>
    public bool OwnsSku(string sku) => !string.IsNullOrEmpty(sku) && (entitlements.Contains(sku) || platformOwned.Contains(sku));

    // Price and durable flag from duckz_economy.json, once GET /duckz/store has loaded.
    public bool TryListed(string key, out int price, out bool durable)
    {
        if (!string.IsNullOrEmpty(key) && listed.TryGetValue(key, out ListedItem item))
        {
            price = item.price;
            durable = item.durable;
            return true;
        }
        price = 0;
        durable = false;
        return false;
    }

    public void Refresh()
    {
        StartCoroutine(RefreshRoutine());
    }

    IEnumerator RefreshRoutine()
    {
        if (!DuckzApiClient.IsReady)
            yield break;

        yield return FetchStore();

        DuckzApiClient.Response<WalletData> response = null;
        yield return DuckzApiClient.Get<WalletData>("duckz/wallet", r => response = r);
        if (!response.Success || response.Data == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice2))
                    logNotice2.Write("Wallet fetch failed: " + response.Message);
            }
            yield break;
        }

        Coins = response.Data.coins;
        entitlements.Clear();
        purchases.Clear();
        AddAll(entitlements, response.Data.entitlements);
        AddAll(purchases, response.Data.purchases);
        ServerEquippedSkin = response.Data.equipped_skin ?? "";
        ServerHasLoadout = response.Data.has_loadout;
        Loaded = true;
        DuckzLog.Log($"[DuckzWallet] Balance {Coins} coins, entitlements [{string.Join(", ", entitlements)}], purchases [{string.Join(", ", purchases)}], " +
                  $"loadout {(ServerHasLoadout ? $"'{ServerEquippedSkin}'" : "none saved")}");
        OnChanged?.Invoke();

        yield return FlushPendingDeposits();
    }

    IEnumerator FetchStore()
    {
        DuckzApiClient.Response<ServerStoreList> response = null;
        yield return DuckzApiClient.Get<ServerStoreList>("duckz/store", r => response = r);
        if (response == null || !response.Success || response.Data == null || response.Data.items == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice3))
                    logNotice3.Write("Store list fetch failed: " + (response != null ? response.Message : ""));
            }
            yield break;
        }

        listed.Clear();
        foreach (ServerStoreItemData item in response.Data.items)
        {
            if (item == null || string.IsNullOrEmpty(item.key))
                continue;
            listed[item.key] = new ListedItem { price = item.price, durable = item.durable };
        }
        DuckzLog.Log($"[DuckzWallet] Store list {listed.Count} items [{string.Join(", ", listed.Keys)}]");
        OnChanged?.Invoke();
    }

    // Bars that land within DepositBatchSeconds are sent as one request.
    public void Deposit(int bars)
    {
        if (bars <= 0)
            return;
        batchedBars += bars;
        if (batchRoutine == null)
            batchRoutine = StartCoroutine(DepositBatchRoutine());
    }

    IEnumerator DepositBatchRoutine()
    {
        yield return new WaitForSecondsRealtime(DepositBatchSeconds);
        int bars = batchedBars;
        batchedBars = 0;
        batchRoutine = null;

        while (bars > 0)
        {
            int chunk = Mathf.Min(bars, MaxBarsPerRequest);
            bars -= chunk;
            string requestId = Guid.NewGuid().ToString("N");
            AddPendingDeposit(requestId, chunk);
            yield return SendDeposit(requestId, chunk);
        }
    }

    IEnumerator SendDeposit(string requestId, int bars)
    {
        DuckzApiClient.Response<DepositData> response = null;
        yield return DuckzApiClient.PostSigned<DepositData>("duckz/coins/deposit",
            new DepositBody { request_id = requestId, bars = bars }, r => response = r);

        if (response.Success && response.Data != null)
        {
            RemovePendingDeposit(requestId);
            Coins = response.Data.coins;
            DuckzLog.Log($"[DuckzWallet] Deposited {bars} bars, balance {Coins} coins");
            DuckzAnalytics.GoldDeposited(bars);
            OnChanged?.Invoke();
            yield break;
        }

        bool transient = response.Code == 0 || response.Code >= 500 || response.Code == 401;
        if (!transient)
            RemovePendingDeposit(requestId);
        {
            if (DuckzLog.AllowWarning(out var logNotice4))
                logNotice4.Write($"Deposit of {bars} bars failed ({response.Code}): {response.Message}");
        }
        DuckzAnalytics.GoldDepositFailed(bars, response.Code, response.Message);
    }

    IEnumerator FlushPendingDeposits()
    {
        if (flushingPending || !UserPlayerPrefs.IsReady)
            yield break;
        flushingPending = true;
        foreach (string entry in UserPlayerPrefs.GetStringArray(PendingDepositsKey))
        {
            if (TryParsePending(entry, out string requestId, out int bars))
                yield return SendDeposit(requestId, bars);
            else
                RemovePendingEntry(entry);
        }
        flushingPending = false;
    }

    public void Buy(string itemKey, Action<BuyStatus> completed)
    {
        StartCoroutine(BuyRoutine(itemKey, completed));
    }

    IEnumerator BuyRoutine(string itemKey, Action<BuyStatus> completed)
    {
        DuckzApiClient.Response<BuyData> response = null;
        yield return DuckzApiClient.PostSigned<BuyData>("duckz/store/buy",
            new BuyBody { request_id = Guid.NewGuid().ToString("N"), item_key = itemKey }, r => response = r);

        BuyStatus status;
        // Error replies have no data, but JsonUtility still builds a BuyData with coins = 0.
        if (response.Success && response.Data != null)
        {
            Coins = response.Data.coins;
            purchases.Add(itemKey);
            status = response.Data.already_owned ? BuyStatus.AlreadyOwned : BuyStatus.Bought;
            DuckzLog.Log($"[DuckzWallet] Buy {itemKey}: {status}, balance {Coins} coins");
        }
        else if (response.Code == 402)
            status = BuyStatus.InsufficientCoins;
        else
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice5, itemKey))
                    logNotice5.Write($"Buy {itemKey} failed ({response.Code}): {response.Message}");
            }
            status = BuyStatus.Failed;
        }

        OnChanged?.Invoke();
        completed?.Invoke(status);
    }

    public void GrantIap(string sku, string platformTxnId, Action<bool> completed)
    {
        StartCoroutine(GrantIapRoutine(sku, platformTxnId, completed));
    }

    IEnumerator GrantIapRoutine(string sku, string platformTxnId, Action<bool> completed)
    {
        DuckzApiClient.Response<GrantData> response = null;
        yield return DuckzApiClient.PostSigned<GrantData>("duckz/iap/grant",
            new GrantBody { sku = sku, platform_txn_id = platformTxnId }, r => response = r);

        if (response.Success && response.Data != null)
        {
            entitlements.Clear();
            AddAll(entitlements, response.Data.entitlements);
            entitlements.Add(sku);
            DuckzLog.Log($"[DuckzWallet] IAP grant '{sku}' ok, entitlements [{string.Join(", ", entitlements)}]");
            OnChanged?.Invoke();
            completed?.Invoke(true);
            yield break;
        }

        {
            if (DuckzLog.AllowWarning(out var logNotice6, sku))
                logNotice6.Write($"[DuckzWallet] IAP grant '{sku}' failed ({response.Code}): {response.Message}");
        }
        DuckzAnalytics.IapGrantFailed(sku, response.Code, response.Message);
        completed?.Invoke(false);
    }

    /// <summary>Saves the equipped skin on the server. completed gets false on failure, so the caller can retry later.</summary>
    public void SaveLoadout(string skinKey, Action<bool> completed)
    {
        StartCoroutine(SaveLoadoutRoutine(skinKey ?? "", completed));
    }

    IEnumerator SaveLoadoutRoutine(string skinKey, Action<bool> completed)
    {
        if (!DuckzApiClient.IsReady)
        {
            completed?.Invoke(false);
            yield break;
        }

        DuckzApiClient.Response<LoadoutData> response = null;
        yield return DuckzApiClient.PostSigned<LoadoutData>("duckz/loadout", new LoadoutBody { skin_key = skinKey }, r => response = r);
        if (response.Success)
        {
            ServerEquippedSkin = skinKey;
            ServerHasLoadout = true;
            completed?.Invoke(true);
            yield break;
        }

        {
            if (DuckzLog.AllowWarning(out var logNotice7, skinKey))
                logNotice7.Write($"Loadout save '{skinKey}' failed ({response.Code}): {response.Message}");
        }
        completed?.Invoke(false);
    }

    // Durable purchases Meta reports locally; counted as owned even before the server confirms.
    public void SetPlatformOwned(IEnumerable<string> skus)
    {
        platformOwned.Clear();
        foreach (string sku in skus)
            platformOwned.Add(sku);
        OnChanged?.Invoke();
    }

    public void MarkPlatformOwned(string sku)
    {
        if (platformOwned.Add(sku))
            OnChanged?.Invoke();
    }

    public bool ServerHasEntitlement(string sku) => entitlements.Contains(sku);

    static void AddAll(HashSet<string> set, string[] values)
    {
        if (values == null)
            return;
        foreach (string v in values)
            if (!string.IsNullOrEmpty(v))
                set.Add(v);
    }

    static bool TryParsePending(string entry, out string requestId, out int bars)
    {
        requestId = null;
        bars = 0;
        if (string.IsNullOrEmpty(entry))
            return false;
        int split = entry.IndexOf(':');
        if (split <= 0)
            return false;
        requestId = entry.Substring(0, split);
        return int.TryParse(entry.Substring(split + 1), out bars) && bars > 0;
    }

    static void AddPendingDeposit(string requestId, int bars)
    {
        if (!UserPlayerPrefs.IsReady)
            return;
        var list = new List<string>(UserPlayerPrefs.GetStringArray(PendingDepositsKey)) { requestId + ":" + bars };
        UserPlayerPrefs.SetStringArray(PendingDepositsKey, list.ToArray());
        UserPlayerPrefs.Save();
    }

    static void RemovePendingDeposit(string requestId)
    {
        if (!UserPlayerPrefs.IsReady)
            return;
        var list = new List<string>(UserPlayerPrefs.GetStringArray(PendingDepositsKey));
        list.RemoveAll(e => e.StartsWith(requestId + ":", StringComparison.Ordinal));
        UserPlayerPrefs.SetStringArray(PendingDepositsKey, list.ToArray());
        UserPlayerPrefs.Save();
    }

    static void RemovePendingEntry(string entry)
    {
        var list = new List<string>(UserPlayerPrefs.GetStringArray(PendingDepositsKey));
        list.Remove(entry);
        UserPlayerPrefs.SetStringArray(PendingDepositsKey, list.ToArray());
    }

    [FoldoutGroup("Debug"), ValueDropdown(nameof(CoinItemKeys))]
    [SerializeField] string debugBuyKey;

    [FoldoutGroup("Debug"), Button, DisableInEditorMode]
    void RefreshWallet() => Refresh();

    [FoldoutGroup("Debug"), Button("Deposit 1 bar"), DisableInEditorMode]
    void DebugDepositBar() => Deposit(1);

    [FoldoutGroup("Debug"), Button("Buy with coins"), DisableInEditorMode]
    void DebugBuy()
    {
        if (string.IsNullOrEmpty(debugBuyKey))
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice8))
                    logNotice8.Write("Pick a coin item first.");
            }
            return;
        }

        StoreUI store = FindFirstObjectByType<StoreUI>();
        if (store != null)
        {
            store.Purchase(debugBuyKey, result => DuckzLog.Log("[DuckzWallet] " + debugBuyKey + ": " + result));
            return;
        }

        Buy(debugBuyKey, result => DuckzLog.Log("[DuckzWallet] " + debugBuyKey + ": " + result));
    }

    IEnumerable<string> CoinItemKeys()
    {
        StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
        if (catalog == null || catalog.items == null)
            yield break;
        foreach (StoreItem item in catalog.items)
            if (item != null && item.payment == StorePayment.Coins && !item.InBundle && !string.IsNullOrEmpty(item.key))
                yield return item.key;
    }
}
