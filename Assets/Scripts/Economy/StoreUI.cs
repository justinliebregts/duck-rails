using System;
using System.Collections.Generic;
using Game.Audio;
using Photon.Pun;
using UnityEngine;
using UnityEngine.Events;

public enum StorePurchaseResult { Bought, Equipped, NotEnoughCoins, Failed, Busy, CoolingDown, Unlocked }

// Static, client-defined store. Owned items equip instantly; unowned items start a coin buy or Meta checkout.
// Bundle items open their bundle's checkout until it's owned; after that, pulling them is free.
public class StoreUI : MonoBehaviour
{
    [SerializeField] StoreCatalog catalogOverride;
    [Tooltip("Played at the buyer's head, for everyone.")]
    [SerializeField] SoundEvent buySucceedSound;
    [SerializeField] SoundEvent buyFailSound;
    [Tooltip("Fired with the item key after a non-durable (consumable) coin purchase succeeds.")]
    public UnityEvent<string> onConsumablePurchased;

    public event Action OnStateChanged;

    // Shared by every wall, so a respawned wall in the same room keeps the wait.
    static readonly Dictionary<string, float> cooldownEnds = new Dictionary<string, float>();
    static readonly List<string> expired = new List<string>();
    static string cooldownRoom;

    bool busy;
    int lastCooldownTick = -1;

    public StoreCatalog Catalog => catalogOverride != null ? catalogOverride : (GameConfig.Current != null ? GameConfig.Current.storeCatalog : null);
    public bool Busy => busy;
    /// <summary>True while a Meta checkout is open. The wall lets go of the item when that modal takes focus.</summary>
    public bool CheckingOut { get; private set; }

    void OnEnable()
    {
        DuckzWallet.Ensure().OnChanged += RaiseChanged;
        EquippedLoadout.Ensure().OnEquipped += OnEquipped;
        DuckzIap.Ensure().OnPricesChanged += RaiseChanged;
    }

    void OnDisable()
    {
        if (DuckzWallet.Instance != null)
            DuckzWallet.Instance.OnChanged -= RaiseChanged;
        if (EquippedLoadout.Instance != null)
            EquippedLoadout.Instance.OnEquipped -= OnEquipped;
        if (DuckzIap.Instance != null)
            DuckzIap.Instance.OnPricesChanged -= RaiseChanged;
    }

    // Labels count down once a second while anything is cooling down.
    void Update()
    {
        if (cooldownEnds.Count == 0)
            return;
        int tick = Mathf.FloorToInt(Time.realtimeSinceStartup);
        if (tick == lastCooldownTick)
            return;
        lastCooldownTick = tick;

        ResetCooldownsIfRoomChanged();
        expired.Clear();
        foreach (KeyValuePair<string, float> cooldown in cooldownEnds)
            if (cooldown.Value <= Time.realtimeSinceStartup)
                expired.Add(cooldown.Key);
        foreach (string key in expired)
        {
            cooldownEnds.Remove(key);
            DuckzLog.Log($"[Store] '{key}' can be pulled off the wall again");
        }
        RaiseChanged();
    }

    void OnEquipped(StoreItemType type, StoreItem item) => RaiseChanged();
    void RaiseChanged() => OnStateChanged?.Invoke();

    public StoreItem Find(string key) => Catalog != null ? Catalog.Find(key) : null;

    public bool IsOwned(StoreItem item)
    {
        if (item == null)
            return false;
        if (item.IsDefault)
            return true;
        if (item.InBundle)
            return DuckzWallet.Ensure().OwnsSku(item.bundleSku);
        return IsDurable(item) && DuckzWallet.Ensure().Owns(item.key);
    }

    public bool IsEquipped(StoreItem item) => EquippedLoadout.Ensure().IsEquipped(item);

    /// <summary>Coin price from duckz_economy.json once the store list has loaded. Catalog price until then.</summary>
    public int Price(StoreItem item)
    {
        if (item != null && DuckzWallet.Ensure().TryListed(item.key, out int price, out _))
            return price;
        return item != null ? item.price : 0;
    }

    public bool IsDurable(StoreItem item)
    {
        if (item == null)
            return false;
        if (item.payment == StorePayment.Iap)
            return true;
        if (DuckzWallet.Ensure().TryListed(item.key, out _, out bool durable))
            return durable;
        return item.durable;
    }

    /// <summary>Seconds until this item can be pulled off the wall again; 0 when it can.</summary>
    public float CooldownRemaining(StoreItem item)
    {
        if (item == null || item.pullCooldown <= 0f)
            return 0f;
        ResetCooldownsIfRoomChanged();
        return cooldownEnds.TryGetValue(item.key, out float end) ? Mathf.Max(0f, end - Time.realtimeSinceStartup) : 0f;
    }

    void StartCooldown(StoreItem item)
    {
        if (item.pullCooldown <= 0f)
            return;
        ResetCooldownsIfRoomChanged();
        cooldownEnds[item.key] = Time.realtimeSinceStartup + item.pullCooldown;
        DuckzLog.Log($"[Store] '{item.key}' cooling down for {item.pullCooldown:0}s");
    }

    static void ResetCooldownsIfRoomChanged()
    {
        string room = PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.Name : null;
        if (room == cooldownRoom)
            return;
        if (cooldownEnds.Count > 0)
            DuckzLog.Log($"[Store] Room changed ('{cooldownRoom}' to '{room}'), clearing {cooldownEnds.Count} wall cooldowns");
        cooldownEnds.Clear();
        cooldownRoom = room;
    }

    static string FormatCooldown(float seconds)
    {
        int s = Mathf.CeilToInt(seconds);
        return $"{s / 60}:{s % 60:00}";
    }

    /// <summary>Meta's localized price for a SKU, the catalog's fallback until Meta answers, or "".</summary>
    public string IapPrice(string sku)
    {
        if (DuckzIap.Instance != null && DuckzIap.Instance.TryGetPrice(sku, out string price))
            return price;
        StoreBundle bundle = Catalog != null ? Catalog.FindBundle(sku) : null;
        return bundle != null && !string.IsNullOrEmpty(bundle.displayPrice) ? bundle.displayPrice : "";
    }

    /// <summary>Shared text over a bundle's items: "Rambro Bundle $14.99", then "Rambro Bundle Owned".</summary>
    public string BundleLabel(string sku)
    {
        StoreBundle bundle = Catalog != null ? Catalog.FindBundle(sku) : null;
        string name = bundle != null && !string.IsNullOrEmpty(bundle.displayName) ? bundle.displayName : sku;
        string suffix = DuckzWallet.Ensure().OwnsSku(sku) ? "Owned" : IapPrice(sku);
        return string.IsNullOrEmpty(suffix) ? name : name + " " + suffix;
    }

    /// <summary>
    /// Shelf text. Repeatable coin items, bags included, show the price. Skins show Equipped while worn and Owned
    /// when pulling them would equip for free. Bundle items are blank (the bundle label shows the price), except
    /// Equipped on a worn skin and the countdown while an item cools down.
    /// </summary>
    public string StatusLabel(StoreItem item)
    {
        if (item == null)
            return "";
        float cooldown = CooldownRemaining(item);
        if (cooldown > 0f)
            return FormatCooldown(cooldown);
        if (item.InBundle)
            return IsOwned(item) && IsEquipped(item) ? "Equipped" : "";
        if (!item.IsWearable)
            return item.payment == StorePayment.Iap ? IapOrBuy(item.key) : Price(item).ToString();
        if (IsEquipped(item))
            return "Equipped";
        if (IsOwned(item))
            return "Owned";
        if (item.payment == StorePayment.Iap)
            return IapOrBuy(item.key);
        return Price(item).ToString();
    }

    string IapOrBuy(string sku)
    {
        string price = IapPrice(sku);
        return string.IsNullOrEmpty(price) ? "Buy" : price;
    }

    /// <summary>True if starting a purchase of this item could succeed right now (owned, IAP, or enough coins).</summary>
    public bool CanAfford(StoreItem item)
    {
        return item != null && (IsOwned(item) || item.InBundle || item.payment == StorePayment.Iap || DuckzWallet.Ensure().Coins >= Price(item));
    }

    /// <summary>Equips an owned item, or buys an unowned one. done is always called exactly once.</summary>
    public void Purchase(string key, Action<StorePurchaseResult> done)
    {
        StoreItem item = Find(key);
        if (item == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice1))
                    logNotice1.Write($"[Store] Pulled '{key}' but it isn't in the store catalog");
            }
            done?.Invoke(StorePurchaseResult.Failed);
            return;
        }
        float cooldown = CooldownRemaining(item);
        DuckzLog.Log($"[Store] Pulled '{key}': type {item.type}, " +
                  $"bundle '{item.bundleSku}', cooldown {cooldown:0}s, busy {busy}");
        if (busy)
        {
            done?.Invoke(StorePurchaseResult.Busy);
            return;
        }
        if (cooldown > 0f)
        {
            PlaySound(buyFailSound);
            done?.Invoke(StorePurchaseResult.CoolingDown);
            return;
        }

        if (item.InBundle)
        {
            PurchaseBundleItem(item, done);
            return;
        }

        // Skins equip without buying when owned, or when already worn.
        if (item.IsWearable && (IsOwned(item) || IsEquipped(item)))
        {
            EquippedLoadout.Ensure().Equip(item);
            done?.Invoke(StorePurchaseResult.Equipped);
            return;
        }

        if (!CanAfford(item))
        {
            PlaySound(buyFailSound);
            DuckzAnalytics.StorePurchase(item.key, StorePurchaseResult.NotEnoughCoins);
            done?.Invoke(StorePurchaseResult.NotEnoughCoins);
            return;
        }

        busy = true;
        if (item.payment == StorePayment.Iap)
        {
            BeginCheckout(item, item.key, done);
            return;
        }

        DuckzWallet.Ensure().Buy(item.key, status =>
        {
            StorePurchaseResult result = status switch
            {
                DuckzWallet.BuyStatus.Bought => StorePurchaseResult.Bought,
                DuckzWallet.BuyStatus.AlreadyOwned => StorePurchaseResult.Bought,
                DuckzWallet.BuyStatus.InsufficientCoins => StorePurchaseResult.NotEnoughCoins,
                _ => StorePurchaseResult.Failed,
            };
            Finish(item, result, done);
        });
    }

    // Owned bundle: the item is free. Not owned: the bundle's checkout. The item stays on the wall either way.
    void PurchaseBundleItem(StoreItem item, Action<StorePurchaseResult> done)
    {
        if (IsOwned(item))
        {
            DuckzLog.Log($"[Store] '{item.key}' is owned through '{item.bundleSku}', taking it for free");
            TakeBundleItem(item, StorePurchaseResult.Equipped, done);
            return;
        }

        DuckzLog.Log($"[Store] '{item.key}' is part of '{item.bundleSku}', which isn't owned: opening its checkout");
        BeginCheckout(item, item.bundleSku, done);
    }

    // The checkout unlocks the item. It is not taken off the wall; the player pulls it again once it's owned.
    void BeginCheckout(StoreItem item, string sku, Action<StorePurchaseResult> done)
    {
        CheckingOut = true;
        busy = true;
        DuckzIap.Ensure().Purchase(sku, ok =>
        {
            CheckingOut = false;
            busy = false;
            DuckzLog.Log($"[Store] Checkout for '{sku}' from '{item.key}' finished: {(ok ? "bought, pull it off the wall to take it" : "not bought")}");
            if (ok)
                RoomRpc.PlaySound(buySucceedSound, BuyerHead());
            RaiseChanged();
            StorePurchaseResult result = ok ? StorePurchaseResult.Unlocked : StorePurchaseResult.Failed;
            DuckzAnalytics.StorePurchase(sku, result);
            done?.Invoke(result);
        });
    }

    void TakeBundleItem(StoreItem item, StorePurchaseResult result, Action<StorePurchaseResult> done)
    {
        if (item.IsWearable)
            EquippedLoadout.Ensure().Equip(item);
        StartCooldown(item);
        RaiseChanged();
        done?.Invoke(result);
    }

    void Finish(StoreItem item, StorePurchaseResult result, Action<StorePurchaseResult> done)
    {
        busy = false;
        DuckzLog.Log($"[Store] Purchase of '{item.key}' finished: {result}");
        bool ok = result == StorePurchaseResult.Bought;
        if (ok)
            RoomRpc.PlaySound(buySucceedSound, BuyerHead());
        else if (result == StorePurchaseResult.NotEnoughCoins)
            PlaySound(buyFailSound);
        if (ok)
        {
            StartCooldown(item);
            if (item.IsWearable)
                EquippedLoadout.Ensure().Equip(item);
            else if (!IsDurable(item) && item.type != StoreItemType.Bag)
                onConsumablePurchased?.Invoke(item.key);
        }
        RaiseChanged();
        DuckzAnalytics.StorePurchase(item.key, result);
        done?.Invoke(result);
    }

    Vector3 BuyerHead()
    {
        PhotonPlayer local = PhotonPlayer.Local;
        if (local != null && local.TryGetHeadPosition(out Vector3 head))
            return head;
        return Camera.main != null ? Camera.main.transform.position : transform.position;
    }

    void PlaySound(SoundEvent sound)
    {
        if (sound != null && AudioManager.instance != null)
            AudioManager.instance.Play(sound, transform.position);
    }
}
