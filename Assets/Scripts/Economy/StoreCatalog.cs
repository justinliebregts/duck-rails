using System;
using System.Collections.Generic;
using Game.Audio;
using UnityEngine;
using UnityEngine.Serialization;

public enum StorePayment { Coins, Iap }
public enum StoreItemType { None, Skin, Weapon, Bag }

[Serializable]
public class StoreItem
{
    [Tooltip("Coin items: must match store_items[].key in the backend duckz_economy.json. IAP items: the Meta SKU, listed in iap_skus.")]
    public string key;
    public StorePayment payment = StorePayment.Coins;
    [Tooltip("Shown only until GET /duckz/store returns. duckz_economy.json is the price that is displayed and charged.")]
    public int price;
    [Tooltip("Durable items are owned forever. IAP items are always durable. Repeatable goods, like the nail bat, are not.")]
    public bool durable = true;
    [Tooltip("Skin is an avatar. Bag spawns a bag into the hand. Anything else is a normal item. Only avatars are saved as equipped.")]
    [FormerlySerializedAs("slot")]
    public StoreItemType type = StoreItemType.None;
    [Tooltip("Skin slot: id sent through PlayerProps.SetSkin. 0 is the default duck: everyone owns it, it is never sold by the server, and pulling it takes off the current skin.")]
    public byte skinId;
    [Tooltip("Skin model (an FBX or prefab) rigged to Playermodel_Armature. Its body mesh replaces the duck's. " +
             "Objects placed under its bones are copied onto the duck. If those include Eye_L/Eye_R/Lip_T/Lip_B they replace the default eyes and lips.")]
    public GameObject skinModel;
    [Tooltip("Optional. When set on a skin that keeps the default face, Eye_L and Eye_R use this material instead of Eyes_Default.")]
    public Material eyeMaterial;
    [Tooltip("Bag: id networked with each bag to pick its look. Unique per bag, never 0.")]
    public byte bagId;
    [Tooltip("Bag look: a prefab with children named Mesh, Slots (holding the BagSlots) and GrabLeft/GrabRight, laid out like BagModel_Basic.prefab. " +
             "Its slot count is the bag's size.")]
    public GameObject bagModel;
    [Header("Bag Sound Overrides")]
    [Tooltip("Optional bag sounds. Empty fields use the shared bag prefab's original sounds.")]
    public SoundEvent bagGrabOverride;
    public SoundEvent bagStowOverride;
    public SoundEvent bagDropOverride;
    public SoundEvent bagHoverOverride;
    public SoundEvent bagPutOverride;
    [Tooltip("Object spawned into the hand when this item is pulled off the store wall.")]
    public GameObject prefab;
    [Tooltip("Meta SKU of the bundle that unlocks this item, listed under bundles. Bundle items are never sold for coins: " +
             "pulling one opens the bundle checkout until it's owned, then pulling it is free.")]
    public string bundleSku;
    [Tooltip("Seconds before this item can be pulled off the wall again. Per player, and reset when the room changes. 0 = no wait.")]
    public float pullCooldown;

    public bool InBundle => !string.IsNullOrEmpty(bundleSku);
    public bool IsDurable => payment == StorePayment.Iap || durable;
    public bool IsDefaultSkin => type == StoreItemType.Skin && skinId == 0;
    /// <summary>Free and owned by everyone: the default skin.</summary>
    public bool IsDefault => IsDefaultSkin;
    /// <summary>Worn rather than held: skins. Saved as equipped.</summary>
    public bool IsWearable => type == StoreItemType.Skin;
}

/// <summary>A durable Meta IAP that unlocks the store items whose bundleSku matches.</summary>
[Serializable]
public class StoreBundle
{
    [Tooltip("Durable Meta SKU. Must match iap_skus[].sku in the backend duckz_economy.json, which lists the same items.")]
    public string sku;
    [Tooltip("Shown on the wall, e.g. \"Rambro Bundle\".")]
    public string displayName;
    [Tooltip("Shown only until Meta returns the real, localized price, e.g. \"$14.99\".")]
    public string displayPrice;
}

[CreateAssetMenu(fileName = "StoreCatalog", menuName = "DuckZ/Store Catalog")]
public class StoreCatalog : ScriptableObject
{
    public List<StoreItem> items = new List<StoreItem>();
    public List<StoreBundle> bundles = new List<StoreBundle>();

    public StoreItem Find(string key)
    {
        foreach (StoreItem item in items)
            if (item.key == key)
                return item;
        return null;
    }

    public GameObject FindSkinModel(byte skinId)
    {
        StoreItem item = FindSkin(skinId);
        return item != null ? item.skinModel : null;
    }

    public StoreItem FindSkin(byte skinId)
    {
        if (skinId == 0)
            return null;
        foreach (StoreItem item in items)
            if (item.type == StoreItemType.Skin && item.skinId == skinId)
                return item;
        return null;
    }

    public GameObject FindBagModel(byte bagId)
    {
        if (bagId == 0)
            return null;
        foreach (StoreItem item in items)
            if (item.type == StoreItemType.Bag && item.bagId == bagId && item.bagModel != null)
                return item.bagModel;
        return null;
    }

    public StoreItem FindBag(byte bagId)
    {
        if (bagId == 0) return null;
        foreach (StoreItem item in items)
            if (item.type == StoreItemType.Bag && item.bagId == bagId)
                return item;
        return null;
    }

    public StoreBundle FindBundle(string sku)
    {
        foreach (StoreBundle bundle in bundles)
            if (bundle.sku == sku)
                return bundle;
        return null;
    }

    public bool IsIapSku(string sku)
    {
        if (FindBundle(sku) != null)
            return true;
        StoreItem item = Find(sku);
        return item != null && item.payment == StorePayment.Iap;
    }

    /// <summary>Every Meta SKU the store sells: bundles and single IAP items.</summary>
    public List<string> IapSkus()
    {
        var skus = new List<string>();
        foreach (StoreBundle bundle in bundles)
            if (!string.IsNullOrEmpty(bundle.sku))
                skus.Add(bundle.sku);
        foreach (StoreItem item in items)
            if (item.payment == StorePayment.Iap && !string.IsNullOrEmpty(item.key) && !skus.Contains(item.key))
                skus.Add(item.key);
        return skus;
    }
}
