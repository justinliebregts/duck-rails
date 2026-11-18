using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Edit Mode checks for carrying a player's loadout between worlds and restoring it after a quit, crash or
/// disconnect. Covers what can be proven without a network: every carried/bagged/worn item resolves to a
/// Photon prefab on every client, restore spawn data parses, and carried items keep their prefab keys.
/// The live behaviour is covered by <see cref="RailLoadoutLiveChecks"/>.
/// </summary>
public static class RailLoadoutPersistenceChecks
{
    const string GameManagerPath = "Assets/Prefabs/Manager/GameManager.prefab";
    const string CompoundTag = "rails-compound-purchase-v1";

    [MenuItem("Duck Rails/Tests/Loadout Persistence (Edit Mode)")]
    public static void Run() => Debug.Log(RunChecks());

    /// <summary>Throws on the first failure; returns the PASS summary.</summary>
    public static string RunChecks()
    {
        if (EditorApplication.isPlaying || PhotonNetwork.IsConnected)
            throw new InvalidOperationException("Run loadout persistence checks in Edit Mode, disconnected from Photon.");
        var passed = new List<string>();
        var previousPool = PhotonNetwork.PrefabPool;
        var cache = (Dictionary<string, GameObject>)typeof(BagObject)
            .GetField("prefabCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        var savedCache = new Dictionary<string, GameObject>(cache);
        try
        {
            VerifyEveryClientResolvesCarriedItems(cache, passed);
            VerifyEquipmentRestoreData(passed);
            VerifyCarryKeys(passed);
            return "RailLoadoutPersistenceChecks PASS (" + passed.Count + "):\n- " + string.Join("\n- ", passed);
        }
        finally
        {
            if (previousPool != null) PhotonNetwork.PrefabPool = previousPool;
            cache.Clear();
            foreach (var pair in savedCache) cache[pair.Key] = pair.Value;
        }
    }

    // A carried item, bag slot or saved head item is respawned by prefab key, and every other client must
    // resolve that same name. Remote clients only know names registered in their pool (GameManager's
    // PhotonPreparePool in both gameplay scenes) or loadable from the Resources root.
    static void VerifyEveryClientResolvesCarriedItems(Dictionary<string, GameObject> cache, List<string> passed)
    {
        var gameManager = AssetDatabase.LoadAssetAtPath<GameObject>(GameManagerPath);
        Require(gameManager != null, "Missing " + GameManagerPath);
        var preparePool = gameManager.GetComponentInChildren<PhotonPreparePool>(true);
        Require(preparePool != null, "GameManager has no PhotonPreparePool");
        var registered = ((GameObject[])typeof(PhotonPreparePool)
            .GetField("prefabs", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(preparePool))
            .Where(p => p != null).ToArray();
        var names = new HashSet<string>(registered.Select(p => p.name));
        Require(names.Count == registered.Length, "GameManager pool has two prefabs with the same name");

        foreach (string scene in new[] { "Assets/Scenes/Compound.unity", "Assets/Scenes/Expedition.unity" })
        {
            string text = System.IO.File.ReadAllText(scene);
            Require(text.Contains(AssetDatabase.AssetPathToGUID(GameManagerPath)), scene + " does not contain GameManager");
            Require(!text.Contains("propertyPath: prefabs.Array"), scene + " overrides the GameManager pool list");
        }
        passed.Add("GameManager (with its Photon pool, no scene overrides) is in Compound and Expedition");

        var required = new List<GameObject>();
        foreach (var prefab in Resources.LoadAll<GameObject>("Rails"))
            if (prefab.GetComponent<PhotonGrabbable>() != null) required.Add(prefab);
        required.Add(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Economy/RailGoldBar.prefab"));
        var catalog = AssetDatabase.LoadAssetAtPath<StoreCatalog>(AssetDatabase.GUIDToAssetPath("f8715b21462b9e040be8172d3a8bd938"));
        Require(catalog != null, "Store catalog missing");
        foreach (var item in catalog.items)
            if (item.prefab != null && item.prefab.GetComponent<PhotonGrabbable>() != null) required.Add(item.prefab);
        Require(required.Count >= 10, "Expected at least ten carriable item prefabs, found " + required.Count);

        var missing = required.Where(p => p == null || !names.Contains(p.name)).Select(p => p != null ? p.name : "null").ToArray();
        Require(missing.Length == 0, "Not registered with Photon on every client: " + string.Join(", ", missing));
        passed.Add(required.Count + " carriable/baggable/wearable item prefabs are registered by name on every client: " +
            string.Join(", ", required.Select(p => p.name).Distinct().OrderBy(n => n)));

        // The same names must resolve through BagObject.ResolvePrefab, which carry, bag slots and head
        // restore use, starting from an empty cache as a freshly launched client would.
        var pool = new DefaultPool();
        PhotonNetwork.PrefabPool = pool;
        cache.Clear();
        foreach (var prefab in registered) PhotonPreparePool.Register(prefab);
        foreach (var prefab in required)
            Require(BagObject.ResolvePrefab(prefab.name) == prefab, "ResolvePrefab(" + prefab.name + ") did not return its prefab");
        passed.Add("BagObject.ResolvePrefab returns each of them from a fresh client's pool");

        foreach (string key in new[] { "RailShopItemHelmet", "RailShopItemNightVisionGoggles", "RailHelmet", "RailGoggles" })
        {
            var prefab = BagObject.ResolvePrefab(key);
            Require(prefab != null && prefab.GetComponent<HeadEquipment>() != null, key + " is not restorable head equipment");
        }
        passed.Add("Shop and train helmet/goggles keys resolve to HeadEquipment for restore");
    }

    // EquipmentInventory restores saved head gear through RoomRpc.SpawnEquipment. In Compound the request adds a
    // purchase lifetime after the actor so the Compound's leftover cleanup keeps it; both forms must restore.
    static void VerifyEquipmentRestoreData(List<string> passed)
    {
        var dataField = typeof(PhotonView).GetField("instantiationDataField", BindingFlags.Instance | BindingFlags.NonPublic);
        var restoreActor = typeof(HeadEquipment).GetField("restoreActor", BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (string path in new[] { "Assets/Resources/Rails/RailHelmet.prefab", "Assets/DuckRails/Economy/RailShopItemNightVisionGoggles.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, "Missing " + path);
            var cases = new (object[] data, int expected, string label)[]
            {
                (new object[] { "equipment-restore-v1", 7 }, 7, "plain"),
                (new object[] { "equipment-restore-v1", 8, new object[] { CompoundTag, "12345", "run", "user" } }, 8, "Compound lifetime"),
                (new object[] { "something-else", 9 }, 0, "foreign tag"),
                (new object[] { "equipment-restore-v1" }, 0, "truncated"),
            };
            foreach (var c in cases)
            {
                var go = UnityEngine.Object.Instantiate(prefab);
                try
                {
                    var view = go.GetComponent<PhotonView>();
                    var head = go.GetComponent<HeadEquipment>();
                    // Edit Mode instances do not run Awake, which caches the view.
                    typeof(HeadEquipment).GetField("photonView", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(head, view);
                    dataField.SetValue(view, c.data);
                    head.OnPhotonInstantiate(default);
                    int actor = (int)restoreActor.GetValue(head);
                    Require(actor == c.expected, prefab.name + " " + c.label + " restore data gave actor " + actor + ", expected " + c.expected);
                    if (c.label == "Compound lifetime")
                        Require(RailCargo.IsCompoundPurchase(view), prefab.name + " Compound restore is not recognised as a Compound lifetime");
                    if (c.label == "plain")
                        Require(!RailCargo.IsCompoundPurchase(view), prefab.name + " expedition restore was treated as a Compound purchase");
                }
                finally { UnityEngine.Object.DestroyImmediate(go); }
            }
        }
        passed.Add("Head restore data: plain and Compound-lifetime forms restore to the wearer; foreign/truncated data is ignored; only the Compound form is kept by Compound cleanup");
    }

    static void VerifyCarryKeys(List<string> passed)
    {
        var key = typeof(RailHeldCarry).GetMethod("PrefabKey", BindingFlags.Static | BindingFlags.NonPublic);
        foreach (var (name, expected) in new[] { ("RailCoal(Clone)", "RailCoal"), ("ItemHelmet (2)", "ItemHelmet"),
            ("RailShopItemFlashlight", "RailShopItemFlashlight"), ("RailGoldBar (1)(Clone)", "RailGoldBar") })
        {
            string actual = (string)key.Invoke(null, new object[] { name });
            Require(actual == expected, "Carry key for '" + name + "' was '" + actual + "'");
        }
        Require(!RailHeldCarry.RemovingWorn, "RemovingWorn left set");
        RailHeldCarry.CaptureForTransition(); // Offline: must do nothing and leave no state behind.
        Require(!RailHeldCarry.RemovingWorn, "RemovingWorn left set after an offline capture");
        passed.Add("Carried items keep their prefab keys; capture is a no-op offline and never leaves RemovingWorn set");
    }

    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
}
