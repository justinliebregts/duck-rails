#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

/// <summary>Installs Duckz shop interactions without rebuilding existing train or block art.</summary>
public static class RailEconomyBuilder
{
    public const string Folder = "Assets/DuckRails/Economy/";
    public static readonly string[] Keys = { "weapon_nail_bat", "weapon_fry_pan", "bag_basic", "item_helmet", "item_flashlight", "item_walkie_talkie", "item_night_vision_goggles" };

    static T Asset<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    static void Reference(Object obj, string property, Object value)
    { var s = new SerializedObject(obj); s.FindProperty(property).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    static Transform Child(string name, Transform parent, Vector3 position)
    { var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = position; return go.transform; }
    static TMP_Text Label(Transform parent, string text, Vector3 position, Vector2 size, float font)
    {
        var t = Child(text.Split('\n')[0], parent, position).gameObject.AddComponent<TextMeshPro>();
        t.text = text; t.fontSize = font; t.alignment = TextAlignmentOptions.Center; t.rectTransform.sizeDelta = size; return t;
    }
    static void Box(Transform parent, string name, Vector3 position, Vector3 size)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 6;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = Asset<Material>("Assets/DuckRails/Materials/Station.mat");
    }
    static GameObject CargoVariant(GameObject source, string name)
    {
        string path = Folder + name + ".prefab";
        if (Asset<GameObject>(path) != null) return Asset<GameObject>(path);
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source); go.name = name;
        try
        {
            var cargo = go.GetComponent<RailCargo>() ?? go.AddComponent<RailCargo>();
            var view = go.GetComponent<PhotonView>();
            view.Synchronization = ViewSynchronization.Unreliable;
            if (!view.ObservedComponents.Contains(cargo)) view.ObservedComponents.Add(cargo);
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { Object.DestroyImmediate(go); }
    }
    static StoreCatalog Catalog()
    {
        string path = Folder + "RailStoreCatalog.asset";
        var catalog = Asset<StoreCatalog>(path);
        if (catalog != null) return catalog;
        catalog = Object.Instantiate(Asset<StoreCatalog>("Assets/Resources/StoreCatalog.asset"));
        catalog.name = "RailStoreCatalog"; catalog.items = catalog.items.Where(i => Keys.Contains(i.key)).ToList(); catalog.bundles.Clear();
        foreach (var item in catalog.items)
        {
            item.durable = false; item.bundleSku = ""; item.pullCooldown = 0;
            if (item.prefab != null) item.prefab = CargoVariant(item.prefab, "RailShop" + item.prefab.name);
        }
        AssetDatabase.CreateAsset(catalog, path); return catalog;
    }
    static GameObject ReceiverPrefab()
    {
        string path = Folder + "GoldExchange.prefab";
        if (Asset<GameObject>(path) != null) return Asset<GameObject>(path);
        var source = Asset<GameObject>("Assets/Prefabs/Gameplay/CrateGoldBar.prefab");
        var go = (GameObject)PrefabUtility.InstantiatePrefab(source);
        go.name = "Gold Exchange";
        try
        {
            Object.DestroyImmediate(go.GetComponent<CrateGoldBar>());
            var r = go.AddComponent<RailGoldReceiver>();
            r.receiverId = 1;
            r.coins = go.GetComponent<CoinShower>(); r.intake = go.GetComponent<BoxCollider>(); r.intake.isTrigger = true;
            r.label = Label(go.transform, "SELL GOLD\n+100 coins per bar", new Vector3(0, 2.1f, -.2f), new Vector2(3, .7f), 1.1f);
            return PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { Object.DestroyImmediate(go); }
    }
    static GameObject Shop(StoreCatalog catalog)
    {
        string path = Folder + "RailShop.prefab";
        if (Asset<GameObject>(path) != null) return Asset<GameObject>(path);
        var source = Asset<GameObject>("Assets/Prefabs/Interface/Store.prefab");
        var root = new GameObject("Rail Shop");
        try
        {
            var store = root.AddComponent<StoreUI>(); EditorUtility.CopySerialized(source.GetComponent<StoreUI>(), store);
            Reference(store, "catalogOverride", catalog); store.onConsumablePurchased = new UnityEvent<string>();
            Box(root.transform, "Shop wall", new Vector3(0, 1.4f, .6f), new Vector3(11, 2.8f, .3f));
            Box(root.transform, "Shop roof", new Vector3(0, 3, -.6f), new Vector3(12, .2f, 3));
            Label(root.transform, "SUPPLIES\nGrab • Pull • Buy", new Vector3(0, 2.4f, .35f), new Vector2(10, 1), 2);
            var walls = source.GetComponentsInChildren<StoreWallItem>(true);
            for (int i = 0; i < Keys.Length; i++)
            {
                var original = walls.Single(w => new SerializedObject(w).FindProperty("itemKey").stringValue == Keys[i]);
                var display = Object.Instantiate(original.gameObject, root.transform).GetComponent<StoreWallItem>();
                display.transform.localPosition = new Vector3((i - 3) * 1.45f, .75f, 0);
                Reference(display, "store", store);
                // The source label is a sibling in Store.prefab, not part of the cloned display.
                // Let StoreWallItem create a local shelf label at its actual placement.
                Reference(display, "label", null);
                foreach (var price in display.GetComponentsInChildren<StorePriceLabel>(true))
                { var so = new SerializedObject(price); var prop = so.FindProperty("store"); if (prop != null) { prop.objectReferenceValue = store; so.ApplyModifiedPropertiesWithoutUndo(); } }
            }
            var exchange = (GameObject)PrefabUtility.InstantiatePrefab(ReceiverPrefab(), root.transform);
            exchange.transform.localPosition = new Vector3(6.2f, 0, -.5f);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }
    public static void AddGold(RailBlock block)
    {
        foreach (var variation in block.variations)
        {
            if (variation.coal == null || variation.coal.Length < 5) continue;
            if (variation.gold != null && variation.gold.Length > 0) continue;
            // Existing coal positions are authored collision-cleared locations on both sides.
            variation.gold = new[] { 1, 4 }.Select(i => {
                var coal = variation.coal[i]; var t = Child("GoldSpawner " + i, coal.parent, coal.localPosition + Vector3.up * .35f);
                t.gameObject.AddComponent<RailGoldSpawner>(); return t;
            }).ToArray();
        }
    }
    public static void InstallScene(RailRoute route)
    {
        Directory.CreateDirectory(Folder);
        var catalog = Catalog(); var shop = Shop(catalog);
        var station = route.startingStation.transform;
        if (station.Find("Departure Shop") == null)
        {
            var start = (GameObject)PrefabUtility.InstantiatePrefab(shop, station);
            start.name = "Departure Shop"; start.transform.localPosition = new Vector3(-22, 0, 80);
            start.transform.localRotation = Quaternion.Euler(0, -90, 0);
            start.GetComponentInChildren<RailGoldReceiver>().receiverId = 1;
        }
        if (route.midpointBlock == null)
        {
            var middle = (GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>(RailHelipadPreview.Path), route.transform);
            middle.name = "Midpoint Helipad"; middle.transform.position = new Vector3(0, 0, RailRoute.StartZ + 2 * 160);
            route.midpointBlock = middle.GetComponent<RailBlock>();
        }
        route.expedition.goldPrefab = CargoVariant(Asset<GameObject>("Assets/Prefabs/Items/ItemGoldBar.prefab"), "RailGoldBar");
        route.expedition.supplies = route.expedition.supplies.Concat(catalog.items.Where(i => i.prefab != null).Select(i => i.prefab)).Distinct().ToArray();
    }
    [MenuItem("Duck Rails/Authoring/Install Gold Economy")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var file in Directory.GetFiles("Assets/DuckRails/Blocks", "*.prefab"))
            {
                var blockRoot = PrefabUtility.LoadPrefabContents(file);
                try { AddGold(blockRoot.GetComponent<RailBlock>()); PrefabUtility.SaveAsPrefabAsset(blockRoot, file); }
                finally { PrefabUtility.UnloadPrefabContents(blockRoot); }
            }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
            InstallScene(Object.FindFirstObjectByType<RailRoute>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Debug.Log("Gold economy installed: seven items per shop and two gold markers per variation.");
    }
}
#endif
