using System;
using System.Collections;
using UnityEngine;

// The equipped avatar. Saved on device per account via UserPlayerPrefs so it applies instantly at startup.
// It is also saved on the server (POST duckz/loadout) so it follows the account to other headsets and reinstalls.
// Weapons are not equipped. Bags are world objects (BagObject); the one on your back is saved by BagInventory.
public class EquippedLoadout : MonoBehaviour
{
    const string KeyPrefix = "loadout.";
    // Set while the device's choice hasn't reached the server; the device wins until it does.
    const string PendingSyncKey = "loadout.pendingSync";

    public static EquippedLoadout Instance { get; private set; }

    public event Action<StoreItemType, StoreItem> OnEquipped;

    bool loaded;
    bool syncing;
    bool resyncQueued;

    public static EquippedLoadout Ensure()
    {
        if (Instance != null)
            return Instance;
        return new GameObject(nameof(EquippedLoadout)).AddComponent<EquippedLoadout>();
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

    void Start()
    {
        if (UserPlayerPrefs.IsReady)
            Load();
        else
            UserPlayerPrefs.UserReady += OnUserPlayerPrefsReady;

        // Wallet lives on GameManager in Quackville, so Login has none yet.
        DuckzWallet wallet = DuckzWallet.Ensure();
        if (wallet != null)
            wallet.OnChanged += OnWalletChanged;
        else
            StartCoroutine(SubscribeWhenWalletExists());
    }

    IEnumerator SubscribeWhenWalletExists()
    {
        DuckzWallet wallet;
        while ((wallet = DuckzWallet.Ensure()) == null)
            yield return null;
        wallet.OnChanged += OnWalletChanged;
    }

    void OnDestroy()
    {
        UserPlayerPrefs.UserReady -= OnUserPlayerPrefsReady;
        if (DuckzWallet.Instance != null)
            DuckzWallet.Instance.OnChanged -= OnWalletChanged;
        if (Instance == this)
            Instance = null;
    }

    void OnUserPlayerPrefsReady()
    {
        UserPlayerPrefs.UserReady -= OnUserPlayerPrefsReady;
        Load();
    }

    void Load()
    {
        loaded = true;
        if (UserPlayerPrefs.IsReady)
        {
            UserPlayerPrefs.DeleteKey(KeyPrefix + StoreItemType.Weapon);
            UserPlayerPrefs.Save();
        }
        DuckzLog.Log($"[Loadout] Loaded from device: skin '{GetKey(StoreItemType.Skin)}', pending server save {PendingSync}");
        Apply(StoreItemType.Skin, Get(StoreItemType.Skin));
        OnWalletChanged();
    }

    public string GetKey(StoreItemType type)
    {
        return UserPlayerPrefs.IsReady ? UserPlayerPrefs.GetString(KeyPrefix + type, "") : "";
    }

    public StoreItem Get(StoreItemType type)
    {
        string key = GetKey(type);
        StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
        return string.IsNullOrEmpty(key) || catalog == null ? null : catalog.Find(key);
    }

    public bool IsEquipped(StoreItem item)
    {
        if (item == null || !item.IsWearable)
            return false;
        if (item.IsDefault)
            return Get(item.type) == null;
        return GetKey(item.type) == item.key;
    }

    public void Equip(StoreItem item)
    {
        if (item == null || !item.IsWearable)
            return;
        if (item.IsDefault)
        {
            Unequip(item.type);
            return;
        }
        DuckzLog.Log($"[Loadout] Equip '{item.key}' (skin {item.skinId})");
        SaveLocal(item.key, true);
        Apply(item.type, item);
        PushToServer();
    }

    public void Unequip(StoreItemType type)
    {
        if (type != StoreItemType.Skin)
            return;
        DuckzLog.Log($"[Loadout] Unequip skin '{GetKey(StoreItemType.Skin)}'");
        SaveLocal("", true);
        Apply(type, null);
        PushToServer();
    }

    void SaveLocal(string skinKey, bool pendingSync)
    {
        if (!UserPlayerPrefs.IsReady)
            return;
        if (string.IsNullOrEmpty(skinKey))
            UserPlayerPrefs.DeleteKey(KeyPrefix + StoreItemType.Skin);
        else
            UserPlayerPrefs.SetString(KeyPrefix + StoreItemType.Skin, skinKey);
        if (pendingSync)
            UserPlayerPrefs.SetInt(PendingSyncKey, 1);
        else
            UserPlayerPrefs.DeleteKey(PendingSyncKey);
        UserPlayerPrefs.Save();
    }

    bool PendingSync => UserPlayerPrefs.IsReady && UserPlayerPrefs.GetInt(PendingSyncKey, 0) == 1;

    void OnWalletChanged()
    {
        SyncWithServer();
        Validate();
    }

    void SyncWithServer()
    {
        DuckzWallet wallet = DuckzWallet.Instance;
        if (!loaded || wallet == null || !wallet.Loaded || !UserPlayerPrefs.IsReady)
            return;

        string local = GetKey(StoreItemType.Skin);
        if (PendingSync)
        {
            DuckzLog.Log($"[Loadout] Sync: device '{local}' not yet saved on server ({(wallet.ServerHasLoadout ? wallet.ServerEquippedSkin : "none saved")}), pushing");
            PushToServer();
            return;
        }

        // Nothing saved on the server yet: upload what this headset already had.
        if (!wallet.ServerHasLoadout)
        {
            DuckzLog.Log($"[Loadout] Sync: server has no loadout, device '{local}'{(string.IsNullOrEmpty(local) ? ", nothing to upload" : ", uploading")}");
            if (!string.IsNullOrEmpty(local))
            {
                SaveLocal(local, true);
                PushToServer();
            }
            return;
        }

        string server = wallet.ServerEquippedSkin;
        if (server == local)
        {
            DuckzLog.Log($"[Loadout] Sync: device and server agree on '{local}'");
            return;
        }
        if (string.IsNullOrEmpty(server))
        {
            DuckzLog.Log($"[Loadout] Sync: server says default duck, replacing device '{local}'");
            SaveLocal("", false);
            Apply(StoreItemType.Skin, null);
            return;
        }

        StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
        StoreItem item = catalog != null ? catalog.Find(server) : null;
        if (item == null || item.type != StoreItemType.Skin || !wallet.Owns(server))
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice1))
                    logNotice1.Write($"[Loadout] Sync: ignoring server skin '{server}' (in catalog {item != null}, is skin {item != null && item.type == StoreItemType.Skin}, owned {wallet.Owns(server)}), keeping device '{local}'");
            }
            return;
        }
        DuckzLog.Log($"[Loadout] Sync: adopting server skin '{server}' over device '{local}'");
        SaveLocal(server, false);
        Apply(StoreItemType.Skin, item);
    }

    void PushToServer()
    {
        DuckzWallet wallet = DuckzWallet.Instance;
        if (wallet == null || !UserPlayerPrefs.IsReady)
            return;
        if (syncing)
        {
            DuckzLog.Log("[Loadout] Server save already in flight, will save again after it");
            resyncQueued = true;
            return;
        }

        syncing = true;
        string key = GetKey(StoreItemType.Skin);
        DuckzLog.Log($"[Loadout] Saving '{key}' to server");
        wallet.SaveLoadout(key, ok =>
        {
            syncing = false;
            DuckzLog.Log($"[Loadout] Server save '{key}' {(ok ? "succeeded" : "failed, will retry on next wallet refresh")}");
            if (ok && GetKey(StoreItemType.Skin) == key && UserPlayerPrefs.IsReady)
            {
                UserPlayerPrefs.DeleteKey(PendingSyncKey);
                UserPlayerPrefs.Save();
            }
            if (resyncQueued)
            {
                resyncQueued = false;
                if (PendingSync)
                    PushToServer();
            }
        });
    }

    // Anything saved as equipped that the account no longer owns (e.g. refunded) falls back to default.
    void Validate()
    {
        DuckzWallet wallet = DuckzWallet.Instance;
        if (!loaded || wallet == null || !wallet.Loaded)
            return;
        if (DuckzIap.Instance != null && !DuckzIap.Instance.RestoreFinished)
            return;
        string key = GetKey(StoreItemType.Skin);
        if (!string.IsNullOrEmpty(key) && !wallet.Owns(key))
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice2))
                    logNotice2.Write($"[Loadout] Equipped skin '{key}' is not owned by this account, reverting to default");
            }
            Unequip(StoreItemType.Skin);
        }
    }

    void Apply(StoreItemType type, StoreItem item)
    {
        if (type == StoreItemType.Skin)
        {
            DuckzLog.Log($"[Loadout] Wearing '{(item != null ? item.key : "default")}', Photon skin property = {(item != null ? item.skinId : 0)}");
            PlayerProps.SetSkin(item != null ? item.skinId : (byte)0);
        }
        OnEquipped?.Invoke(type, item);
    }
}
