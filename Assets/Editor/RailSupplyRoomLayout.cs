#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Location overrides deliberately retain the shared catalog and helicopter layout.
public static class RailSupplyRoomLayout
{
    const string Compound = "Assets/DuckRails/Compound/SubwayTerminal/SubwayTerminal.prefab";
    const string Station = "Assets/DuckRails/Economy/RailShop.prefab";
    const string Supports = "Supply wall fixtures";
    static string Key(StoreWallItem item) => new SerializedObject(item).FindProperty("itemKey").stringValue;
    static TMP_Text Label(StoreWallItem item) => (TMP_Text)new SerializedObject(item).FindProperty("label").objectReferenceValue;
    static void Record(Transform t) => PrefabUtility.RecordPrefabInstancePropertyModifications(t);

    // Mesh bounds, excluding tether lines and text. Transform into the placement frame.
    public static Bounds VisualBounds(Transform item, Transform frame)
    {
        bool any = false; var bounds = new Bounds();
        foreach (var renderer in item.GetComponentsInChildren<Renderer>())
        {
            if (renderer is LineRenderer || renderer.GetComponent<TMP_Text>()) continue;
            var filter = renderer.GetComponent<MeshFilter>();
            var skin = renderer as SkinnedMeshRenderer;
            var mesh = skin ? skin.sharedMesh : filter ? filter.sharedMesh : null;
            if (!mesh) continue;
            Mesh posed = null;
            if (skin) { posed = new Mesh(); skin.BakeMesh(posed); mesh = posed; }
            foreach (var vertex in mesh.vertices)
            {
                var p = frame.InverseTransformPoint(renderer.transform.TransformPoint(vertex));
                if (!any) { bounds = new Bounds(p, Vector3.zero); any = true; } else bounds.Encapsulate(p);
            }
            if (posed) Object.DestroyImmediate(posed);
        }
        if (!any) throw new Exception("No display mesh: " + item.name);
        return bounds;
    }

    static void Box(Transform parent, string name, Vector3 position, Vector3 size, Quaternion rotation)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 6;
        go.transform.SetParent(parent, false); go.transform.localPosition = position;
        go.transform.localRotation = rotation; go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/Metal.mat");
        // Probe-lit additions: no stale lightmap indices or edits to the room's bake.
        GameObjectUtility.SetStaticEditorFlags(go, 0);
    }

    static void Place(Transform frame, StoreWallItem item, Vector3 wall, float yaw)
    {
        var label = Label(item);
        var rotation = Quaternion.Euler(0, yaw, 0);
        // TMP faces -Z. Preserve each model's original orientation relative to its price label.
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(RailSharedStoreSetup.Path)
            .GetComponentsInChildren<StoreWallItem>().Single(i => Key(i) == Key(item));
        item.transform.rotation = frame.rotation * rotation * Quaternion.Inverse(Label(source).transform.rotation) * source.transform.rotation;
        if (Key(item) == "weapon_nail_bat" || Key(item) == "weapon_fry_pan")
            item.transform.rotation = Quaternion.AngleAxis(90, frame.rotation * rotation * Vector3.forward) * item.transform.rotation;
        var b = VisualBounds(item.transform, frame);
        Vector3 inward = rotation * Vector3.back;
        float depth = Mathf.Abs(inward.x) > .5f ? b.size.x : b.size.z;
        var center = wall + inward * (depth / 2 + .07f);
        center.y = .25f + b.extents.y;
        item.transform.position += frame.TransformVector(center - b.center);
        Record(item.transform);
        float labelDepth = Mathf.Max(.34f, depth + .14f);
        // Low wall displays need no stands; retain readable labels underneath.
        label.transform.SetPositionAndRotation(frame.TransformPoint(wall + inward * (labelDepth + .012f) + Vector3.up * .145f), frame.rotation * rotation);
        Record(label.transform);
    }

    [MenuItem("Duck Rails/Store/Fit low supplies in Compound and Starting Station")]
    public static void Apply()
    {
        if (Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying)
            throw new Exception("Duck Rails Edit Mode required");
        Fit(Compound, true); Fit(Station, false);
        RailSharedStoreSetup.Validate();
    }

    [MenuItem("Duck Rails/Store/Fit low supplies in Starting Station only")]
    public static void ApplyStation()
    {
        if (Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying)
            throw new Exception("Duck Rails Edit Mode required");
        Fit(Station, false);
        RailSharedStoreSetup.Validate();
    }

    static void Fit(string path, bool compound)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var store = root.transform.Find("Shared Store");
            // Use the terminal frame in Compound and the shared-store frame at the station.
            var frame = compound ? root.transform : store;
            var old = frame.Find(Supports); if (old) Object.DestroyImmediate(old.gameObject);
            var legacy = frame.Find("Low supply display stands"); if (legacy) Object.DestroyImmediate(legacy.gameObject);
            var stands = new GameObject(Supports).transform; stands.SetParent(frame, false);
            var items = store.GetComponentsInChildren<StoreWallItem>().ToDictionary(Key);
            void Put(string key, float x, float z, float yaw) => Place(frame, items[key], new Vector3(x,0,z), yaw);
            if (compound)
            {
                // East wall: three weapon/bag/avatar groups. West entrance and north passage stay open.
                string[] groups = {"quackbear", "huggy", "scrap_duck"};
                for(int i=0;i<3;i++)
                {
                    float z = -2.05f + i * 2.2f;
                    Put("weapon_"+groups[i],8.70f,z,90); Put("bag_"+groups[i],8.70f,z+.55f,90); Put("skin_duck_"+groups[i],8.70f,z+1.25f,90);
                }
                Put("weapon_nail_bat",4.9f,2.25f,270); Put("weapon_fry_pan",4.9f,3.25f,270);
                Put("bag_basic",4.9f,4.0f,270); Put("skin_duck_default",4.9f,4.85f,270);
                Put("item_walkie_talkie",6.96f,5.42f,0); Put("item_flashlight",7.42f,5.42f,0);
                Put("item_night_vision_goggles",7.92f,5.42f,0); Put("item_helmet",8.43f,5.42f,0);
            }
            else
            {
                // Origin is (-19.02, floor, 85.38). Wrap bundles onto the unused south wall,
                // freeing the west wall for the larger standard supplies.
                Put("weapon_quackbear",.3f,-5.16f,180);
                Put("bag_quackbear",1.15f,-5.16f,180);
                Put("skin_duck_quackbear",2.05f,-5.16f,180);
                string[] groups = {"huggy", "scrap_duck"};
                for(int i=0;i<2;i++)
                {
                    float z = -4.0f + i * 2.75f;
                    Put("weapon_"+groups[i],-1.16f,z,270); Put("bag_"+groups[i],-1.16f,z+.75f,270); Put("skin_duck_"+groups[i],-1.16f,z+1.65f,270);
                }
                Put("skin_duck_default",-1.16f,1.65f,270);
                Put("weapon_nail_bat",-1.16f,3.0f,270);
                Put("weapon_fry_pan",-1.16f,4.45f,270);
                string[] keys = {"bag_basic","item_walkie_talkie","item_flashlight","item_night_vision_goggles","item_helmet"};
                float[] xs = {.1f,1.15f,2.2f,3.25f,4.4f};
                for(int i=0;i<keys.Length;i++) Put(keys[i],xs[i],6.40f,0);
            }
            foreach(var title in store.GetComponentsInChildren<StoreBundleLabel>())
            {
                var groupItems = title.transform.parent.GetComponentsInChildren<StoreWallItem>();
                var label = Label(groupItems.Single(i=>Key(i).StartsWith("bag_")));
                var p = frame.InverseTransformPoint(label.transform.position); p.y=.85f;
                title.transform.SetPositionAndRotation(frame.TransformPoint(p),label.transform.rotation); Record(title.transform);
            }
            // Keep the station's northeast barrel/pallet dressing clear.
            var tv=store.Find("SecurityTV"); var cam=store.Find("SecurityCam");
            var tvPosition=compound?new Vector3(8.35f,.5f,4.6f):new Vector3(-.82f,.5f,5.5f);
            var camPosition=compound?new Vector3(8.45f,1.45f,4.8f):new Vector3(-1.0f,1.45f,5.5f);
            tv.position=frame.TransformPoint(tvPosition); cam.position=frame.TransformPoint(camPosition);
            var originalStore=AssetDatabase.LoadAssetAtPath<GameObject>(RailSharedStoreSetup.Path).transform;
            tv.rotation=frame.rotation*Quaternion.Euler(0,compound?0:270,0)*originalStore.Find("SecurityTV").localRotation;
            tv.localScale=originalStore.Find("SecurityTV").localScale*.65f;
            cam.rotation=frame.rotation*Quaternion.Euler(0,compound?0:270,0)*originalStore.Find("SecurityCam").localRotation;
            var tvBounds=VisualBounds(tv,frame);
            tv.position+=frame.TransformVector(Vector3.up*(.4f-tvBounds.min.y));
            var camBounds=VisualBounds(cam,frame);
            float wallX=compound?8.85f:-1.30f;
            float backX=compound?camBounds.max.x:camBounds.min.x;
            Box(stands,"Security camera wall bracket",new Vector3((backX+wallX)/2,camBounds.center.y,camBounds.center.z),new Vector3(Mathf.Max(.06f,Mathf.Abs(wallX-backX)),.06f,.08f),Quaternion.identity);
            Record(tv); Record(cam);
            PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
