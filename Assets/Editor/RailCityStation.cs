#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>
/// City starting station v3: the Blender model from ArtSource/StartingStation/build_station.py.
/// 1 builds the prefab from the FBX and station_data.json (colliders, guards, baked lights, sign labels).
/// 2 installs it in the station bake scene beside the disabled greybox, regenerates probes and starts a bake.
/// Finish with Duck Rails > Lighting > Starting Station > 3 Export Lighting Companion.
/// </summary>
public static class RailCityStation
{
    public const string Folder = "Assets/DuckRails/StartingStationV3";
    public const string Fbx = Folder + "/StartingStationV3.fbx";
    const string SuppliesDoorBulbFbx = Folder + "/SuppliesDoorBulb.fbx";
    public const string Prefab = Folder + "/StartingStationV3.prefab";
    public const string DataPath = "ArtSource/StartingStation/station_data.json";
    public const string RootName = "City station v3";
    const string CompoundMaterials = "Assets/DuckRails/Compound/SubwayTerminal/Materials/";
    const int Environment = 6;

    [Serializable] class LightInfo { public string name; public float[] pos; public string kind; public float[] dir; }
    [Serializable] class LabelInfo { public string text; public float[] pos; public float[] normal; public float height; public float width; }
    [Serializable] class GuardInfo { public float[] a; public float[] b; public float h; }
    [Serializable] class Data { public LightInfo[] lights; public LabelInfo[] labels; public GuardInfo[] guards; }

    static Vector3 P(float[] v) => new Vector3(v[0], v[1], v[2]);

    static readonly (string source, string asset)[] Remap =
    {
        ("Quackville Town atlas", "Subway Town atlas"), ("Quackville Industrial atlas", "Subway Industrial atlas"),
        ("Subway lamp lens", "Subway lamp lens"), ("Subway night glow", "Subway night glow"), ("Subway tunnel void", "Subway tunnel void"),
        ("Subway warm bulb", "Subway warm bulb"), ("Subway mesh fence", "Subway mesh fence"),
    };

    [MenuItem("Duck Rails/Authoring/Station/City station v3/1 Build prefab from FBX")]
    public static void BuildPrefab()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Use Edit Mode.");
        AssetDatabase.ImportAsset(Fbx, ImportAssetOptions.ForceUpdate);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Fbx) ?? throw new InvalidOperationException("Missing " + Fbx);
        importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.generateSecondaryUV = true;
        importer.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
        importer.secondaryUVMinLightmapResolution = 8; importer.secondaryUVMinObjectScale = 1;
        foreach (var (source, asset) in Remap)
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(CompoundMaterials + asset + ".mat") ?? throw new InvalidOperationException("Missing material " + asset);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), material);
        }
        importer.SaveAndReimport();

        var data = JsonUtility.FromJson<Data>(File.ReadAllText(DataPath));
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset") ?? throw new InvalidOperationException("Missing CutePixel SDF");
        var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
        try
        {
            root.name = RootName;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var go = renderer.gameObject; go.layer = Environment;
                bool wire = go.name == "Station_WireMesh", backdrop = go.name == "City_Backdrop", deck = go.name == "City_Deck";
                // The FBX street deck and inward-facing station ceiling both sit at Y=9.
                // Coplanar opposite faces invalidate bake texels below (large square patches).
                // Keep the scenic, collider-free deck above the ceiling; retain the interior.
                if (deck) go.transform.localPosition += Vector3.up * .25f;
                if (wire || backdrop)
                {
                    renderer.receiveGI = ReceiveGI.LightProbes; renderer.shadowCastingMode = ShadowCastingMode.Off;
                    if (backdrop) renderer.lightProbeUsage = LightProbeUsage.Off;   // dark silhouettes on scene ambient, not the yard lamps
                    GameObjectUtility.SetStaticEditorFlags(go, 0);
                }
                else
                {
                    renderer.receiveGI = ReceiveGI.Lightmaps;
                    renderer.scaleInLightmap = deck ? .02f : go.name == "Portal_Wall" || go.name == "Yard" ? .35f : go.name == "Tunnel" ? .5f : 1f;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
                    if (!deck) go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
                }
            }
            AddSuppliesDoorBulb(root.transform);
            var guards = new GameObject("Invisible guards").transform; guards.SetParent(root.transform, false);
            foreach (var g in data.guards)
            {
                Vector3 a = P(g.a), b = P(g.b);
                var go = new GameObject("Guard"); go.layer = Environment; go.transform.SetParent(guards, false);
                go.transform.SetPositionAndRotation((a + b) / 2, Quaternion.LookRotation(b - a));
                var box = go.AddComponent<BoxCollider>(); box.center = new Vector3(0, g.h / 2, 0); box.size = new Vector3(.12f, g.h, (b - a).magnitude);
            }
            var lights = new GameObject("Baked lights").transform; lights.SetParent(root.transform, false);
            foreach (var l in data.lights)
            {
                var go = new GameObject(l.name); go.transform.SetParent(lights, false); go.transform.localPosition = P(l.pos);
                var light = go.AddComponent<Light>(); light.lightmapBakeType = LightmapBakeType.Baked; light.shadows = LightShadows.Soft;
                // Mostly neutral floodlights (the Compound's fixture) with a few warm accents: bulbs and the camp work lamp.
                Color neutral = new Color(.86f, .93f, 1), warm = new Color(1, .7f, .4f);
                if (l.kind == "bulb") { light.type = LightType.Point; light.color = warm; light.intensity = 7; light.range = 8; continue; }
                light.type = LightType.Spot; go.transform.localRotation = Quaternion.LookRotation(P(l.dir));
                light.color = l.kind == "warm" || l.kind == "warm_tripod" ? warm : neutral; light.spotAngle = 130; light.innerSpotAngle = 70;
                switch (l.kind)
                {
                    case "small": light.intensity = 55; light.range = 14; break;
                    case "yard": light.intensity = 300; light.range = 45; break;
                    case "warm": light.intensity = 100; light.range = 14; light.spotAngle = 105; break;
                    case "warm_tripod": light.intensity = 24; light.range = 7; light.spotAngle = 105; break;
                    default: light.intensity = 150; light.range = 24; break;
                }
            }
            var labels = new GameObject("Sign labels").transform; labels.SetParent(root.transform, false);
            foreach (var l in data.labels)
            {
                var go = new GameObject("Label " + l.text); go.transform.SetParent(labels, false);
                go.transform.SetPositionAndRotation(P(l.pos), Quaternion.LookRotation(-P(l.normal)));
                var text = go.AddComponent<TextMeshPro>(); text.font = font; text.text = l.text; text.alignment = TextAlignmentOptions.Center;
                text.color = new Color(.91f, .87f, .75f); text.enableAutoSizing = true; text.fontSizeMin = .5f; text.fontSizeMax = 40; text.textWrappingMode = TextWrappingModes.NoWrap;
                text.rectTransform.sizeDelta = new Vector2(l.width, l.height);
                go.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            RailStationMeshSigns.Convert(root);
            Directory.CreateDirectory(Folder);
            PrefabUtility.SaveAsPrefabAsset(root, Prefab);
        }
        finally { Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Debug.Log($"City station prefab built: {data.lights.Length} lights, {data.labels.Length} labels, {data.guards.Length} guards.");
    }

    static bool InStairMass(Vector3 p)
    {
        if (Mathf.Abs(p.x) < 10.1f || Mathf.Abs(p.x) > 13 || p.z < 71 || p.z > 82.32f) return false;
        float top = p.z > 77.56f ? 1 + (82.32f - p.z) / .34f * .17f : p.z > 75.76f ? 3.55f : 3.55f + (75.76f - p.z) / .34f * .17f;
        return p.y < top + .4f;
    }

    static List<Vector3> Probes()
    {
        var points = new List<Vector3>();
        void Grid(float x0, float x1, float dx, float z0, float z1, float dz, params float[] ys)
        {
            for (float x = x0; x <= x1 + .01f; x += dx) for (float z = z0; z <= z1 + .01f; z += dz) foreach (float y in ys)
            {
                var p = new Vector3(x, y, z);
                if (Mathf.Abs(x) < 2.9f && y < 1.2f) p.y = .6f;
                if (!InStairMass(p) && !(p.z < 71.3f && p.y > 5.4f && p.y < 6.5f)) points.Add(p);
            }
        }
        Grid(-12, 12, 3, 69.5f, 90.5f, 3, 1.5f, 3.2f, 5.2f, 7.4f);           // hall
        Grid(-19.6f, -14.2f, 2.7f, 81, 91, 2.5f, 1.5f, 3.2f);                // supplies
        Grid(14.2f, 17.6f, 1.7f, 80.8f, 86, 2.6f, 1.5f, 3.1f);               // washroom
        Grid(14.2f, 17.6f, 1.7f, 88, 91.2f, 1.6f, 1.5f, 3.1f);               // office
        Grid(-3, 3, 3, 94, 138, 4, .7f, 3, 5.6f);                            // tunnel
        Grid(-44, 44, 8, 142, 158, 5.3f, .6f, 2.6f, 7);                      // yard
        foreach (float z in new[] { 1f, 60f }) foreach (float x in new[] { -4f, 0, 4f }) points.Add(new Vector3(x, 2, z));
        return points;
    }

    static void AddSuppliesDoorBulb(Transform station)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(SuppliesDoorBulbFbx)
            ?? throw new InvalidOperationException("Missing " + SuppliesDoorBulbFbx);
        var fixture = (GameObject)PrefabUtility.InstantiatePrefab(model, station);
        fixture.name = "Supplies door bulb mesh";
        var renderer = fixture.GetComponentInChildren<MeshRenderer>();
        renderer.sharedMaterials = renderer.sharedMaterials.Select(material =>
        {
            var asset = Remap.Single(entry => entry.source == material.name).asset;
            return AssetDatabase.LoadAssetAtPath<Material>(CompoundMaterials + asset + ".mat")
                ?? throw new InvalidOperationException("Missing material " + asset);
        }).ToArray();
    }

    [MenuItem("Duck Rails/Authoring/Station/City station v3/2 Install in bake scene and bake")]
    public static void Install()
    {
        if (EditorApplication.isPlaying || Lightmapping.isRunning) throw new InvalidOperationException("Exit Play Mode and wait for any bake.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab) ?? throw new InvalidOperationException("Build the prefab first.");
        var scene = EditorSceneManager.OpenScene(RailStationLightingAuthoring.BakeScene);
        var block = Object.FindFirstObjectByType<RailBlock>();
        if (block == null || block.id != "starting-station-v1") throw new InvalidOperationException("Wrong station.");
        var env = block.transform.Find("Env") ?? throw new InvalidOperationException("Station has no Env child.");
        var greybox = env.Find(RailUndergroundStation.RootName);
        if (greybox) greybox.gameObject.SetActive(false);                       // kept, not deleted
        var old = env.Find(RootName); if (old) Object.DestroyImmediate(old.gameObject);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, env);
        instance.name = RootName; instance.transform.localPosition = Vector3.zero; instance.transform.localRotation = Quaternion.identity;
        var store = block.transform.Find("Future Store Area"); if (store) store.localPosition = new Vector3(-16.9f, 1, 86);
        var lighting = block.GetComponent<RailBakedBlockLighting>(); lighting.bindings = Array.Empty<RailBakedBlockLighting.Binding>();
        PrefabUtility.RecordPrefabInstancePropertyModifications(lighting);
        PrefabUtility.ApplyPrefabInstance(block.gameObject, InteractionMode.AutomatedAction);
        var probes = Object.FindFirstObjectByType<LightProbeGroup>() ?? throw new InvalidOperationException("Bake scene has no probe group.");
        probes.probePositions = Probes().ToArray();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        RailStationLightingAuthoring.Bake();
        Debug.Log($"City station installed with {probes.probePositions.Length} probes; bake started.");
    }
}
#endif
