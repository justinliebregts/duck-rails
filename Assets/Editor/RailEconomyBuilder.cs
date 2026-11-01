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
    const string TrainPath = "Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab";

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
    static GameObject ReceiverPrefab(bool safe, bool rebuild = false)
    {
        string path = Folder + (safe ? "TrainSafe" : "GoldExchange") + ".prefab";
        if (!rebuild && Asset<GameObject>(path) != null) return Asset<GameObject>(path);
        var source = Asset<GameObject>("Assets/Prefabs/Gameplay/CrateGoldBar.prefab");
        var go = safe ? new GameObject("Train Safe") : (GameObject)PrefabUtility.InstantiatePrefab(source);
        go.name = safe ? "Train Safe" : "Gold Exchange";
        try
        {
            if (safe)
            {
                Box(go.transform, "Safe floor", new Vector3(0, .06f, 0), new Vector3(1.1f, .12f, 1));
                Box(go.transform, "Safe roof", new Vector3(0, .94f, 0), new Vector3(1.1f, .12f, 1));
                Box(go.transform, "Safe back", new Vector3(0, .5f, .45f), new Vector3(1.1f, 1, .1f));
                foreach (float x in new[] { -.5f, .5f }) Box(go.transform, "Safe side", new Vector3(x, .5f, 0), new Vector3(.1f, 1, 1));
                Box(go.transform, "Safe front below slot", new Vector3(0, .25f, -.45f), new Vector3(1.1f, .5f, .1f));
                Box(go.transform, "Safe front above slot", new Vector3(0, .85f, -.45f), new Vector3(1.1f, .3f, .1f));
                var trigger = go.AddComponent<BoxCollider>(); trigger.center = new Vector3(0, .6f, 0); trigger.size = new Vector3(.85f, .3f, .65f);
                var shower = go.AddComponent<CoinShower>(); EditorUtility.CopySerialized(source.GetComponent<CoinShower>(), shower);
                Reference(shower, "spawnPoint", Child("Coin source", go.transform, new Vector3(0, 1.1f, 0)));
            }
            else Object.DestroyImmediate(go.GetComponent<CrateGoldBar>());
            var r = go.AddComponent<RailGoldReceiver>(); r.isSafe = safe;
            r.coins = go.GetComponent<CoinShower>(); r.intake = go.GetComponent<BoxCollider>(); r.intake.isTrigger = true;
            r.label = Label(go.transform, safe ? "SAFE 0 GOLD BARS\nDeposit only • Paid on escape" : "SELL GOLD\nEveryone gets coins", new Vector3(0, safe ? 1.3f : 2.1f, -.2f), new Vector2(3, .7f), 1.1f);
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
                display.transform.localPosition = new Vector3((i - 3) * 1.45f, 1.1f, 0);
                Reference(display, "store", store);
                foreach (var price in display.GetComponentsInChildren<StorePriceLabel>(true))
                { var so = new SerializedObject(price); var prop = so.FindProperty("store"); if (prop != null) { prop.objectReferenceValue = store; so.ApplyModifiedPropertiesWithoutUndo(); } }
            }
            var exchange = (GameObject)PrefabUtility.InstantiatePrefab(ReceiverPrefab(false), root.transform);
            exchange.transform.localPosition = new Vector3(6.2f, 0, -.5f);
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }
    [MenuItem("Duck Rails/Authoring/Rebuild Prototype Safe")]
    public static void RebuildSafe()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        ReceiverPrefab(true, true);
        var train = PrefabUtility.LoadPrefabContents(TrainPath);
        try
        {
            var safe = train.GetComponentInChildren<RailGoldReceiver>();
            safe.transform.localPosition = new Vector3(1.5f, 0, -2.2f);
            safe.transform.localRotation = Quaternion.Euler(0, 90, 0); safe.transform.localScale = Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(train, TrainPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(train); }
        AssetDatabase.SaveAssets();
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
        if (route.midpointShop == null)
        {
            var middle = (GameObject)PrefabUtility.InstantiatePrefab(shop, route.transform);
            middle.name = "Midpoint Shop"; middle.transform.position = new Vector3(-16, 0, RailRoute.StartZ + 2 * 160 + 148);
            middle.transform.rotation = Quaternion.Euler(0, -90, 0);
            middle.GetComponentInChildren<RailGoldReceiver>().receiverId = 2; route.midpointShop = middle.transform;
        }
        route.expedition.goldPrefab = CargoVariant(Asset<GameObject>("Assets/Prefabs/Items/ItemGoldBar.prefab"), "RailGoldBar");
        route.expedition.supplies = route.expedition.supplies.Concat(catalog.items.Where(i => i.prefab != null).Select(i => i.prefab)).Distinct().ToArray();
        var train = Object.FindFirstObjectByType<RailTrain>();
        if (train.GetComponentInChildren<RailGoldReceiver>() == null)
        {
            var safe = (GameObject)PrefabUtility.InstantiatePrefab(ReceiverPrefab(true), train.transform);
            safe.transform.localPosition = new Vector3(1.5f, 0, -2.2f); safe.transform.localRotation = Quaternion.Euler(0, 90, 0);
        }
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
            var train = PrefabUtility.LoadPrefabContents(TrainPath);
            try
            {
                if (train.GetComponentInChildren<RailGoldReceiver>() == null)
                {
                    var safe = (GameObject)PrefabUtility.InstantiatePrefab(ReceiverPrefab(true), train.transform);
                    safe.transform.localPosition = new Vector3(1.5f, 0, -2.2f); safe.transform.localRotation = Quaternion.Euler(0, 90, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(train, TrainPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(train); }
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
            InstallScene(Object.FindFirstObjectByType<RailRoute>());
            EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        Debug.Log("Gold economy installed: seven items per shop, two gold markers per variation, one-way train safe.");
    }
}
#endif
