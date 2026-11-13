#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

// Creates assets only when absent, preserving replacement art and hand-authored placement.
public static class RailBundleSetup
{
    public const string Folder = "Assets/DuckRails/Economy/Bundles/";
    const string Module = "Assets/DuckRails/Compound/SubwayV2/Modules/05 Walk-through supplies store.prefab";
    public static readonly string[] Names = { "Quackbear", "Huggy", "ScrapDuck" };
    public static readonly string[] Keys = { "quackbear", "huggy", "scrap_duck" };
    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    static void Ref(Object target, string field, Object value)
    { var s = new SerializedObject(target); s.FindProperty(field).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    static void Text(Object target, string field, string value)
    { var s = new SerializedObject(target); s.FindProperty(field).stringValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    static GameObject Copy(string source, string path)
    {
        if (Load<GameObject>(path) == null && !AssetDatabase.CopyAsset(source, path)) throw new Exception("Cannot copy " + source);
        return Load<GameObject>(path);
    }
    static GameObject Skin(string name)
    {
        string path = Folder + "Skin_" + name + ".prefab";
        if (Load<GameObject>(path) != null) return Load<GameObject>(path);
        string source = "Assets/Skins/DuckSkin_" + name + "/";
        var root = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(source + "DuckSkin_" + name + ".fbx"));
        try
        {
            root.name = "Skin_" + name;
            var head = root.GetComponentsInChildren<Transform>(true).Single(t => t.name == "head");
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.sharedMaterial = Load<Material>(source + "DuckSkin_" + name + ".mat");
            foreach (string part in new[] { "Eyes", "Lips" })
            {
                var face = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(source + part + "_" + name + ".fbx"));
                // FBX face roots retain their authored model-space transforms before attaching to the head.
                face.transform.SetParent(root.transform, false);
                face.transform.SetParent(head, true);
                var material = Load<Material>(source + part + "_" + name + ".mat") ?? Load<Material>(source + "DuckSkin_" + name + ".mat");
                foreach (var renderer in face.GetComponentsInChildren<Renderer>(true)) renderer.sharedMaterial = material;
            }
            return PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { Object.DestroyImmediate(root); }
    }
    static TMP_Text Label(Transform parent, string text, Vector3 position, float size = .65f)
    {
        var go = new GameObject(text); go.transform.SetParent(parent, false); go.transform.localPosition = position;
        var label = go.AddComponent<TextMeshPro>(); label.font = Load<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
        label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(1.05f, .22f); return label;
    }
    static void Display(Transform parent, StoreUI store, StoreItem item, StoreWallItem template, Vector3 position)
    {
        var go = Object.Instantiate(template.gameObject, parent); go.name = item.key;
        go.transform.localPosition = position; go.transform.localRotation = Quaternion.identity;
        var wall = go.GetComponent<StoreWallItem>(); Ref(wall, "store", store); Text(wall, "itemKey", item.key);
        Ref(wall, "label", Label(parent, "", position + new Vector3(0, -.23f, -.03f), .55f));
        Ref(wall, "line", null); Ref(wall, "grabSurface", null); Ref(wall, "pullSurface", null);
        // Keep weapon/bag template pose and mesh alignment; replace avatar with the assembled skin.
        if (item.type == StoreItemType.Skin && item.skinModel != null)
        {
            while (go.transform.childCount > 0) Object.DestroyImmediate(go.transform.GetChild(0).gameObject);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(item.skinModel, go.transform);
            model.transform.localPosition = Vector3.zero; model.transform.localRotation = Quaternion.identity;
            model.transform.localScale = Vector3.one * .35f;
        }
        FitDisplay(wall, item);
    }
    static void FitDisplay(StoreWallItem wall, StoreItem item)
    {
        var root = wall.transform;
        foreach (Transform child in root)
        {
            if (item.type == StoreItemType.Skin) child.localRotation = Quaternion.Euler(0, 180, 0) * child.localRotation;
            if (item.prefab != null) child.localScale *= .7f;
        }
        Bounds bounds = new Bounds(); bool first = true;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true).Where(r => !(r is LineRenderer)))
            for (int i = 0; i < 8; i++)
            {
                Vector3 p = root.InverseTransformPoint(r.transform.TransformPoint(r.localBounds.center + Vector3.Scale(r.localBounds.extents, new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
                if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
            }
        foreach (Transform child in root) child.localPosition -= bounds.center;
        foreach (var collider in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        var box = root.gameObject.AddComponent<BoxCollider>(); box.size = bounds.size;
        Ref(wall, "pullSurface", box);
    }
    [MenuItem("Duck Rails/Bundles/Install character bundles")]
    public static void Install()
    {
        if (Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying) throw new Exception("Duck Rails Edit Mode required");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var catalog = Load<StoreCatalog>(Folder + "RailBundleCatalog.asset");
        if (catalog == null)
        {
            catalog = Object.Instantiate(Load<StoreCatalog>("Assets/DuckRails/Economy/RailStoreCatalog.asset"));
            catalog.name = "RailBundleCatalog";
            catalog.items.Add(new StoreItem { key = "skin_duck_default", type = StoreItemType.Skin });
            for (int i = 0; i < Names.Length; i++)
            {
                string n = Names[i], k = Keys[i], sku = "duckrails_" + k + "_bundle";
                var bag = Copy("Assets/Prefabs/Bags/BagModel_Basic.prefab", Folder + "BagModel_" + n + ".prefab");
                var weapon = Copy("Assets/DuckRails/Economy/RailShopWeaponFryPan.prefab", Folder + "Weapon_" + n + ".prefab");
                // Distinct root names are Photon spawn IDs, independent of future mesh replacements.
                var instance = PrefabUtility.LoadPrefabContents(AssetDatabase.GetAssetPath(weapon));
                try { instance.name = "RailBundleWeapon" + n; PrefabUtility.SaveAsPrefabAsset(instance, AssetDatabase.GetAssetPath(weapon)); }
                finally { PrefabUtility.UnloadPrefabContents(instance); }
                catalog.bundles.Add(new StoreBundle { sku = sku, displayName = (n == "ScrapDuck" ? "Scrap Duck" : n) + " Bundle", displayPrice = "" });
                catalog.items.Add(new StoreItem { key = "skin_duck_" + k, type = StoreItemType.Skin, skinId = (byte)(4+i), skinModel = Skin(n), bundleSku = sku });
                catalog.items.Add(new StoreItem { key = "bag_" + k, type = StoreItemType.Bag, bagId = (byte)(5+i), bagModel = bag, durable = false, bundleSku = sku, pullCooldown = 120 });
                catalog.items.Add(new StoreItem { key = "weapon_" + k, prefab = weapon, durable = false, bundleSku = sku, pullCooldown = 120 });
            }
            AssetDatabase.CreateAsset(catalog, Folder + "RailBundleCatalog.asset");
        }
        Ref(Load<GameConfig>("Assets/Resources/GameConfig.asset"), "storeCatalog", catalog);
        string managerPath = "Assets/Prefabs/Manager/GameManager.prefab";
        var manager = PrefabUtility.LoadPrefabContents(managerPath);
        try
        {
            var s = new SerializedObject(manager.GetComponentInChildren<PhotonPreparePool>(true)); var array = s.FindProperty("prefabs");
            foreach (var item in catalog.items.Where(x => x.InBundle && x.prefab != null))
                if (!Enumerable.Range(0, array.arraySize).Any(i => array.GetArrayElementAtIndex(i).objectReferenceValue == item.prefab))
                { int i = array.arraySize++; array.GetArrayElementAtIndex(i).objectReferenceValue = item.prefab; }
            s.ApplyModifiedPropertiesWithoutUndo(); PrefabUtility.SaveAsPrefabAsset(manager, managerPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(manager); }
        string wallPath = Folder + "RailBundleWall.prefab";
        if (Load<GameObject>(wallPath) == null)
        {
            var source = Load<GameObject>("Assets/Prefabs/Interface/Store.prefab");
            var root = new GameObject("RailBundleWall");
            try
            {
                var store = root.AddComponent<StoreUI>(); EditorUtility.CopySerialized(source.GetComponent<StoreUI>(), store);
                Ref(store, "catalogOverride", catalog); store.onConsumablePurchased = new UnityEvent<string>();
                var templates = source.GetComponentsInChildren<StoreWallItem>(true);
                StoreWallItem Template(string key) => templates.Single(w => new SerializedObject(w).FindProperty("itemKey").stringValue == key);
                for (int i = 0; i < 3; i++)
                {
                    float x = (i-1)*1.08f;
                    var title = Label(root.transform, catalog.bundles[i].displayName, new Vector3(x, 2.12f, 0));
                    var bundle = title.gameObject.AddComponent<StoreBundleLabel>(); Ref(bundle, "store", store); Ref(bundle, "label", title); Text(bundle, "bundleSku", catalog.bundles[i].sku);
                    Display(root.transform, store, catalog.Find("skin_duck_"+Keys[i]), Template("skin_duck_rambro"), new Vector3(x,1.5f,0));
                    Display(root.transform, store, catalog.Find("bag_"+Keys[i]), Template("bag_basic"), new Vector3(x,.94f,0));
                    Display(root.transform, store, catalog.Find("weapon_"+Keys[i]), Template("weapon_fry_pan"), new Vector3(x,.38f,0));
                }
                Display(root.transform, store, catalog.Find("skin_duck_default"), Template("skin_duck_default"), new Vector3(-2.1f,1.15f,0));
                Label(root.transform, "Default Duck", new Vector3(-2.1f,1.7f,0));
                PrefabUtility.SaveAsPrefabAsset(root, wallPath);
            }
            finally { Object.DestroyImmediate(root); }
            MatchDuckzLayout();
        }
        var module = PrefabUtility.LoadPrefabContents(Module);
        try
        {
            if (module.transform.Find("RailBundleWall") == null)
            {
                var wall = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(wallPath), module.transform);
                wall.transform.localPosition = new Vector3(.7f,0,.95f);
                // Hide only rear shelf filler; retain existing shelving and side stock.
                var rear = module.transform.Find("Salvaged shelving");
                foreach (Transform child in rear) if (child.name.StartsWith("Placeholder stock")) child.gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(module, Module);
            }
        }
        finally { PrefabUtility.UnloadPrefabContents(module); }
        AssetDatabase.SaveAssets(); Debug.Log("Rail bundles installed; gameplay and platform checkout queued for playtest.");
    }

    [MenuItem("Duck Rails/Bundles/Match Duckz horizontal layout")]
    public static void MatchDuckzLayout()
    {
        if (Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying)
            throw new Exception("Duck Rails Edit Mode required");
        // This inherited Store prefab is byte-identical to the inspected Duckz source.
        var source = Load<GameObject>("Assets/Prefabs/Interface/Store.prefab");
        var template = source.GetComponentsInChildren<Transform>(true).Single(t => t.name == "GorillaBundle");
        string path = Folder + "RailBundleWall.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var catalog = Load<StoreCatalog>(Folder + "RailBundleCatalog.asset");
            for (int i = 0; i < Keys.Length; i++)
            {
                var origin = new Vector3((i - 1) * 1.6f, 1.78f, 0);
                var title = root.GetComponentsInChildren<StoreBundleLabel>(true).Single(b =>
                    new SerializedObject(b).FindProperty("bundleSku").stringValue == catalog.bundles[i].sku);
                CopyLabel(template.Find("GorillaBundleTitle").GetComponent<TMP_Text>(), title.GetComponent<TMP_Text>(), origin, catalog.bundles[i].displayName);
                foreach (var pair in new[] { ("weapon_", "GorillaKnifeDisplay"), ("bag_", "BagGorillaDisplay"), ("skin_duck_", "GorillaDisplay") })
                {
                    var display = root.transform.Find(pair.Item1 + Keys[i]).GetComponent<StoreWallItem>();
                    var original = template.Find(pair.Item2).GetComponent<StoreWallItem>();
                    // Rails visuals were centered when authored. Match the Duckz visual center,
                    // rather than carrying over offsets for its different weapon/bag meshes.
                    var bounds = new Bounds(); bool first = true;
                    foreach (var r in original.GetComponentsInChildren<Renderer>(true).Where(r => !(r is LineRenderer)))
                        for (int corner = 0; corner < 8; corner++)
                        {
                            var p = template.InverseTransformPoint(r.transform.TransformPoint(r.localBounds.center + Vector3.Scale(r.localBounds.extents,
                                new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1))));
                            if (first) { bounds = new Bounds(p, Vector3.zero); first = false; } else bounds.Encapsulate(p);
                        }
                    display.transform.localPosition = origin + bounds.center;
                    var label = (TMP_Text)new SerializedObject(display).FindProperty("label").objectReferenceValue;
                    var originalLabel = (TMP_Text)new SerializedObject(original).FindProperty("label").objectReferenceValue;
                    CopyLabel(originalLabel, label, origin, "");
                    label.name = display.name + " Countdown";
                }
            }
            // Default duck stays separate from the three horizontal bundles.
            var defaultDisplay = root.transform.Find("skin_duck_default");
            defaultDisplay.localPosition = new Vector3(-3.1f,1.15f,0);
            var defaultLabel = (TMP_Text)new SerializedObject(defaultDisplay.GetComponent<StoreWallItem>()).FindProperty("label").objectReferenceValue;
            defaultLabel.transform.localPosition = new Vector3(-3.1f,.92f,0);
            root.transform.Find("Default Duck").localPosition = new Vector3(-3.1f,1.7f,0);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void CopyLabel(TMP_Text source, TMP_Text target, Vector3 origin, string text)
    {
        EditorUtility.CopySerialized(source, target);
        target.font = Load<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset"); target.text = text;
        var rect = target.rectTransform; var original = source.rectTransform;
        rect.anchorMin = original.anchorMin; rect.anchorMax = original.anchorMax; rect.pivot = original.pivot;
        rect.sizeDelta = original.sizeDelta; rect.localScale = original.localScale;
        rect.localRotation = original.localRotation; rect.localPosition = origin + original.localPosition;
    }
}
#endif
