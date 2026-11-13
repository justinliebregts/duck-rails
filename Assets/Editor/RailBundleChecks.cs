#if UNITY_EDITOR
using System;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RailBundleChecks
{
    [MenuItem("Duck Rails/Bundles/Check character bundles")]
    public static void Run()
    {
        if (EditorApplication.isPlaying) throw new Exception("Edit Mode required");
        int count = 0;
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); count++; }
        var catalog = AssetDatabase.LoadAssetAtPath<StoreCatalog>(RailBundleSetup.Folder + "RailBundleCatalog.asset");
        Check(GameConfig.Current.storeCatalog == catalog, "Global catalog binding");
        Check(catalog.items.Count == 17 && catalog.bundles.Count == 3, "Seven supplies, default avatar, nine bundle items");
        Check(catalog.items.Select(i => i.key).Distinct().Count() == 17, "Unique keys");
        var manager = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/GameManager.prefab");
        var pool = new SerializedObject(manager.GetComponentInChildren<PhotonPreparePool>(true)).FindProperty("prefabs");
        var wall = AssetDatabase.LoadAssetAtPath<GameObject>(RailBundleSetup.Folder + "RailBundleWall.prefab");
        Check(wall.GetComponentsInChildren<StoreWallItem>(true).Length == 10, "Nine bundle pulls and default duck");
        foreach (var text in wall.GetComponentsInChildren<TMP_Text>(true)) Check(text.font.name == "CutePixel SDF", "CutePixel label");
        foreach (var display in wall.GetComponentsInChildren<StoreWallItem>(true))
        {
            var s = new SerializedObject(display);
            Check(catalog.Find(s.FindProperty("itemKey").stringValue) != null, "Display catalog key");
            Check(s.FindProperty("store").objectReferenceValue == wall.GetComponent<StoreUI>(), "Display store binding");
            Check(display.GetComponent<Rigidbody>().isKinematic && display.GetComponent<Collider>() != null, "Pull physics");
        }
        foreach (string key in RailBundleSetup.Keys)
        {
            var skin = catalog.Find("skin_duck_" + key); var bag = catalog.Find("bag_" + key); var weapon = catalog.Find("weapon_" + key);
            Check(skin.pullCooldown == 0 && bag.pullCooldown == 120 && weapon.pullCooldown == 120, "Cooldown settings");
            Check(new[] { skin, bag, weapon }.All(i => i.bundleSku == "duckrails_" + key + "_bundle"), "Shared SKU");
            Check(catalog.FindBagModel(bag.bagId) == bag.bagModel, "Network bag lookup");
            Check(bag.bagModel.GetComponentsInChildren<BagSlot>(true).Length == 4, "Four-slot placeholder");
            Check(weapon.prefab.GetComponent<RailCargo>() != null, "Moving-train cargo");
            var view = weapon.prefab.GetComponent<PhotonView>();
            Check(view.ObservedComponents.Contains(weapon.prefab.GetComponent<RailCargo>()), "Cargo observed by Photon");
            Check(Enumerable.Range(0, pool.arraySize).Any(i => pool.GetArrayElementAtIndex(i).objectReferenceValue == weapon.prefab), "Remote spawn registration");
            foreach (var renderer in skin.skinModel.GetComponentsInChildren<Renderer>(true))
                Check(renderer.sharedMaterials.All(m => m != null), "Skin materials");
            foreach (string face in new[] { "Eye_L", "Eye_R", "Lip_T", "Lip_B" })
                Check(skin.skinModel.GetComponentsInChildren<Transform>(true).Any(t => t.name == face && t.GetComponentsInParent<Transform>(true).Any(p => p.name == "head")), "Face bound to head");
        }
        Check(catalog.items.Where(i => i.type == StoreItemType.Skin).Select(i => i.skinId).Distinct().Count() == 4, "Unique skin IDs");
        Check(catalog.items.Where(i => i.type == StoreItemType.Bag).Select(i => i.bagId).Distinct().Count() == 4, "Unique bag IDs");
        Check(catalog.items.Where(i => i.InBundle && i.prefab != null).Select(i => i.prefab.name).Distinct().Count() == 3, "Unique Photon names");
        var go = new GameObject("Bundle cooldown check"); go.SetActive(false);
        var field = typeof(StoreUI).GetField("cooldownEnds", BindingFlags.Static | BindingFlags.NonPublic);
        var timers = (System.Collections.Generic.Dictionary<string, float>)field.GetValue(null);
        var saved = timers.ToArray();
        var roomField = typeof(StoreUI).GetField("cooldownRoom", BindingFlags.Static | BindingFlags.NonPublic);
        var room = roomField.GetValue(null);
        try
        {
            timers.Clear(); var store = go.AddComponent<StoreUI>();
            var bag = catalog.Find("bag_quackbear"); var weapon = catalog.Find("weapon_quackbear");
            typeof(StoreUI).GetMethod("StartCooldown", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(store, new object[] { bag });
            Check(store.CooldownRemaining(bag) > 119 && store.CooldownRemaining(bag) <= 120, "Bag starts 120-second timer");
            Check(store.CooldownRemaining(weapon) == 0, "Weapon timer independent from bag");
            Check(store.CooldownRemaining(catalog.Find("skin_duck_quackbear")) == 0, "Avatar remains available");
            timers[bag.key] = Time.realtimeSinceStartup - 1;
            Check(store.CooldownRemaining(bag) == 0, "Expired timer permits next pull");
        }
        finally { Object.DestroyImmediate(go); timers.Clear(); foreach (var pair in saved) timers.Add(pair.Key, pair.Value); roomField.SetValue(null, room); }
        Debug.Log($"Rail bundles: {count} EditMode checks passed. Live checkout, pull/spawn, peers and headset remain queued.");
    }
}
#endif
