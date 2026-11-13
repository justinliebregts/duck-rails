#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using Object = UnityEngine.Object;

public static class RailSharedStoreSetup
{
    public const string Path = "Assets/DuckRails/Economy/RailSharedStore.prefab";
    const string Module = "Assets/DuckRails/Compound/SubwayV2/Modules/05 Walk-through supplies store.prefab";
    static T Load<T>(string path) where T : Object => AssetDatabase.LoadAssetAtPath<T>(path);
    static void Ref(Object target, string key, Object value)
    { var s = new SerializedObject(target); s.FindProperty(key).objectReferenceValue = value; s.ApplyModifiedPropertiesWithoutUndo(); }
    static string Key(StoreWallItem wall) => new SerializedObject(wall).FindProperty("itemKey").stringValue;
    static Transform Find(Transform root, string name) => root.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    static void Edit(string path, Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root,path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static GameObject Mount(Transform parent, Vector3 position, Quaternion rotation)
    {
        var existing = parent.Find("Shared Store");
        if (existing != null) return existing.gameObject;
        var store = (GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Path), parent);
        store.name = "Shared Store"; store.transform.localPosition = position; store.transform.localRotation = rotation;
        return store;
    }
    [MenuItem("Duck Rails/Store/Install shared store in all three locations")]
    public static void Install()
    {
        if (Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying) throw new Exception("Duck Rails Edit Mode required");
        var catalog = Load<StoreCatalog>(RailBundleSetup.Folder + "RailBundleCatalog.asset");
        if (Load<GameObject>(Path) == null)
        {
            var root = Object.Instantiate(Load<GameObject>("Assets/Prefabs/Interface/Store.prefab"));
            root.name = "RailSharedStore";
            try
            {
                var store = root.GetComponent<StoreUI>(); Ref(store,"catalogOverride",catalog);
                store.onConsumablePurchased = new UnityEvent<string>();
                var bundleWall = Load<GameObject>(RailBundleSetup.Folder + "RailBundleWall.prefab");
                string[] oldNames = { "RambroBundle", "NinjaBundle", "GorillaBundle" };
                for (int i = 0; i < 3; i++)
                {
                    var old = Find(root.transform,oldNames[i]);
                    var group = new GameObject(RailBundleSetup.Names[i] + " Bundle").transform;
                    group.SetParent(root.transform,false); group.localPosition=old.localPosition; group.localRotation=old.localRotation;
                    Object.DestroyImmediate(old.gameObject);
                    var origin = new Vector3((i-1)*1.6f,1.78f,0);
                    var titleSource = bundleWall.GetComponentsInChildren<StoreBundleLabel>(true).Single(b =>
                        new SerializedObject(b).FindProperty("bundleSku").stringValue == catalog.bundles[i].sku);
                    var title = Object.Instantiate(titleSource,group); title.transform.localPosition-=origin; Ref(title,"store",store);
                    foreach (string prefix in new[] { "weapon_", "bag_", "skin_duck_" })
                    {
                        var original=bundleWall.GetComponentsInChildren<StoreWallItem>(true).Single(w=>Key(w)==prefix+RailBundleSetup.Keys[i]);
                        var display=Object.Instantiate(original,group); display.name=original.name; display.transform.localPosition-=origin;
                        var oldLabel=(TMP_Text)new SerializedObject(original).FindProperty("label").objectReferenceValue;
                        var label=Object.Instantiate(oldLabel,group); label.name=oldLabel.name; label.transform.localPosition-=origin;
                        Ref(display,"store",store); Ref(display,"label",label);
                    }
                }
                foreach (var display in root.GetComponentsInChildren<StoreWallItem>(true)) Ref(display,"store",store);
                var heli=Load<GameObject>(RailParkedHelicopterSetup.VariantPath);
                var camera=Object.Instantiate(Find(heli.transform,"SecurityCam").gameObject,root.transform); camera.name="SecurityCam";
                var tv=Object.Instantiate(Find(heli.transform,"SecurityTV").gameObject,root.transform); tv.name="SecurityTV";
                Ref(tv.GetComponentInChildren<HelicopterMirror>(true),"cameraMount",Find(camera.transform,"SecurityCamera"));
                foreach(var label in root.GetComponentsInChildren<TMP_Text>(true))
                { label.font=Load<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset"); label.fontSharedMaterial=label.font.material; }
                PrefabUtility.SaveAsPrefabAsset(root,Path);
            }
            finally { Object.DestroyImmediate(root); }
        }
        // Register standard Rails cargo as well as bundle weapons before any remote spawn in Compound.
        Edit("Assets/Prefabs/Manager/GameManager.prefab", root =>
        {
            var s=new SerializedObject(root.GetComponentInChildren<PhotonPreparePool>(true)); var a=s.FindProperty("prefabs");
            foreach(var item in catalog.items.Where(i=>i.prefab!=null))
                if(!Enumerable.Range(0,a.arraySize).Any(i=>a.GetArrayElementAtIndex(i).objectReferenceValue==item.prefab))
                { int index=a.arraySize++; a.GetArrayElementAtIndex(index).objectReferenceValue=item.prefab; }
            s.ApplyModifiedPropertiesWithoutUndo();
        });
        Edit(RailParkedHelicopterSetup.VariantPath, root =>
        {
            // Preserve original authored objects as inactive references, avoiding nested prefab deletion overrides.
            root.transform.Find("Store").gameObject.SetActive(false);
            root.transform.Find("SecurityCam").gameObject.SetActive(false); root.transform.Find("SecurityTV").gameObject.SetActive(false);
            Mount(root.transform,Vector3.zero,Quaternion.identity);
        });
        Edit("Assets/DuckRails/Economy/RailShop.prefab",root =>
        {
            root.GetComponent<StoreUI>().enabled=false;
            var shared=root.transform.Find("Shared Store");
            foreach(var display in root.GetComponentsInChildren<StoreWallItem>(true).Where(d=>shared==null || !d.transform.IsChildOf(shared))) display.gameObject.SetActive(false);
            Mount(root.transform,new Vector3(-3.25f,0,0),Quaternion.Euler(0,90,0));
        });
        Edit(Module,root =>
        {
            var old=root.transform.Find("RailBundleWall"); if(old!=null)old.gameObject.SetActive(false);
            var store=Mount(root.transform,new Vector3(-3.25f,0,0),Quaternion.Euler(0,90,0));
            // Full-size store; the user will adjust the compound around any overlap.
            store.transform.localScale=Vector3.one;
        });
        // Disable only the earlier scene-added bundle wall; do not resave unrelated scene overrides.
        const string scene="Assets/Scenes/Expedition.unity";
        string yaml=File.ReadAllText(scene);
        const string name="      value: Departure Bundles\n      objectReference: {fileID: 0}";
        string normalized=yaml.Replace("\r\n","\n");
        int at=normalized.IndexOf(name,StringComparison.Ordinal);
        if(at<0)throw new Exception("Missing Departure Bundles scene instance");
        int start=normalized.LastIndexOf("--- !u!1001",at,StringComparison.Ordinal);
        int end=normalized.IndexOf("--- !u!",at,StringComparison.Ordinal);
        var section=normalized.Substring(start,end-start);
        var guid=AssetDatabase.AssetPathToGUID(RailBundleSetup.Folder+"RailBundleWall.prefab");
        string inactive=$"    - target: {{fileID: 8693453070661136769, guid: {guid}, type: 3}}\n      propertyPath: m_IsActive\n      value: 0\n      objectReference: {{fileID: 0}}\n";
        if(!section.Contains(inactive))
        { section=section.Replace("    m_RemovedComponents: []",inactive+"    m_RemovedComponents: []"); normalized=normalized.Substring(0,start)+section+normalized.Substring(end); File.WriteAllText(scene,normalized); }
        AssetDatabase.SaveAssets(); AssetDatabase.ImportAsset(scene);
        Validate();
    }
    [MenuItem("Duck Rails/Store/Validate shared store")]
    public static void Validate()
    {
        var root=Load<GameObject>(Path); var store=root.GetComponent<StoreUI>(); var items=root.GetComponentsInChildren<StoreWallItem>();
        if(items.Length!=17||items.Select(Key).Distinct().Count()!=17)throw new Exception("Expected seven supplies, nine bundle items and default duck");
        foreach(var display in items)
        {
            var s=new SerializedObject(display); var item=store.Find(Key(display)); var label=s.FindProperty("label").objectReferenceValue as TMP_Text;
            if(item==null||s.FindProperty("store").objectReferenceValue!=store||label==null||!label.transform.IsChildOf(root.transform))throw new Exception("Store binding");
            if(RailEconomyBuilder.Keys.Contains(item.key)&&label.GetComponentInChildren<StorePriceCoin>(true)==null)throw new Exception("Missing coin price");
            if(item.InBundle&&item.pullCooldown!=(item.IsWearable?0:120))throw new Exception("Bundle cooldown");
        }
        var mirror=root.GetComponentInChildren<HelicopterMirror>();
        var mount=(Transform)new SerializedObject(mirror).FindProperty("cameraMount").objectReferenceValue;
        if(mount==null||!mount.IsChildOf(root.transform.Find("SecurityCam")))throw new Exception("Foreign camera mount");
        foreach(string path in new[]{Module,"Assets/DuckRails/Economy/RailShop.prefab",RailParkedHelicopterSetup.VariantPath})
        {
            var location=Load<GameObject>(path); var shared=location.transform.Find("Shared Store");
            if(shared==null||PrefabUtility.GetCorrespondingObjectFromSource(shared.gameObject)!=root)throw new Exception("Shared prefab missing at "+path);
            if(location.GetComponentsInChildren<StoreWallItem>().Length!=17||location.GetComponentsInChildren<HelicopterMirror>().Length!=1)throw new Exception("Duplicate/missing content at "+path);
        }
        Debug.Log("Shared store checks pass: three instances, 17 items each, seven price coins, three bundles, one locally bound SecurityCam/TV each.");
    }
}
#endif
