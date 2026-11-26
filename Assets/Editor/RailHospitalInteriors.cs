#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Installs the furnished interiors (ArtSource/HospitalInteriors/build_interiors.py) into the playable hospital shell:
/// one atlas-textured mesh per room, box colliders, CutePixel room signs and baked room lights. The rough blockout
/// furniture it replaces is deactivated, not deleted. Re-running replaces only the generated interiors group.
/// </summary>
public static class RailHospitalInteriors
{
    public const string RootName = "Finished hospital interiors";
    const string Folder = RailHospital.Folder + "/Interiors";
    const string ModelPath = Folder + "/Hospital Interiors.fbx";
    const string DataPath = "ArtSource/HospitalInteriors/interiors.json";
    const string CompoundMaterials = "Assets/DuckRails/Compound/SubwayTerminal/Materials/";
    const int Environment = 6;
    // Pale precast tone shared with the campus exterior, replacing the blockout's green on shell walls.
    static readonly Color WallPlaster = new(1f, .98f, .93f);
    const string PlasterTexture = RailHospital.Folder + "/Textures/Hospital plaster.png";

    static readonly (string source, string asset)[] Remap =
    {
        ("Quackville Town atlas", "Subway Town atlas"), ("Quackville Industrial atlas", "Subway Industrial atlas"),
        ("Subway lamp lens", "Hospital lamp lens"), ("Subway night glow", "Hospital night glow"), ("Subway tunnel void", "Hospital dark screen"),
        ("Subway warm bulb", "Subway warm bulb"),
    };

    // Rough blockout furniture and fixtures superseded by the interiors.
    static readonly HashSet<string> HospitalRetired = new()
    {
        "Patient bed", "Operating table", "Reception desk", "North supplies shelving", "Imaging scanner base", "Scanner arch", "Nurses station desk",
    };
    static readonly HashSet<string> StreetRetired = new() { "Counter", "Shelves", "Baked hospital light", "Light fixture" };
    public static readonly string[] StreetBuildings = { "Clinic", "Pharmacy", "Laundry utilities" };

    [Serializable] class LightInfo { public string name; public float[] pos; public float intensity, range; public float[] color; }
    [Serializable] class LabelInfo { public string text; public float[] pos; public float yaw, width, height; }
    [Serializable] class BoxInfo { public float[] c, s; public float yaw; public string room; }
    [Serializable] class Data { public LightInfo[] lights; public LabelInfo[] labels; public BoxInfo[] boxes; [NonSerialized] public float[][] fixtures; }

    static Vector3 P(float[] v) => new(v[0], v[1], v[2]);

    static Data ReadData()
    {
        var json = File.ReadAllText(DataPath);
        var data = JsonUtility.FromJson<Data>(json);
        // JsonUtility cannot read nested arrays; parse the fixture triples by hand.
        int at = json.IndexOf("\"fixtures\"", StringComparison.Ordinal);
        var list = new List<float[]>();
        if (at >= 0)
        {
            var nums = json.Substring(at).Split(new[] { '[', ']', ',', '\n', '\r', ' ', ':', '}', '"' }, StringSplitOptions.RemoveEmptyEntries)
                .Skip(1).Select(t => float.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            for (int i = 0; i + 2 < nums.Length; i += 3) list.Add(new[] { nums[i], nums[i + 1], nums[i + 2] });
        }
        data.fixtures = list.ToArray();
        return data;
    }

    static void PrepareModel()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate);
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath) ?? throw new InvalidOperationException("Missing " + ModelPath + "; run the Blender build with --export.");
        importer.importCameras = false; importer.importLights = false; importer.importAnimation = false;
        importer.animationType = ModelImporterAnimationType.None;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.generateSecondaryUV = true;
        importer.secondaryUVMarginMethod = ModelImporterSecondaryUVMarginMethod.Calculate;
        importer.secondaryUVMinLightmapResolution = 4; importer.secondaryUVMinObjectScale = 1;
        importer.isReadable = false;
        foreach (var (source, asset) in Remap)
        {
            var folder = asset.StartsWith("Hospital ",StringComparison.Ordinal) ? Folder + "/" : CompoundMaterials;
            var material = AssetDatabase.LoadAssetAtPath<Material>(folder + asset + ".mat") ?? throw new InvalidOperationException("Missing material " + asset);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), source), material);
        }
        importer.SaveAndReimport();
    }

    [MenuItem("Duck Rails/Authoring/Hospital/Install finished interiors")]
    public static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Use Edit Mode.");
        RailHospital.Guard();
        PrepareModel();
        var data = ReadData();
        var contents = PrefabUtility.LoadPrefabContents(RailHospital.PrefabPath);
        try
        {
            var root = RailBlock.FindEnvironment(contents.transform, RailHospital.RootName) ?? throw new InvalidOperationException("Missing hospital shell.");
            Apply(root, data);
            PrefabUtility.SaveAsPrefabAsset(contents, RailHospital.PrefabPath, out bool saved);
            if (!saved) throw new IOException("Could not save Hospital District.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        RepaintShellWalls(AssetDatabase.LoadAssetAtPath<Material>(RailHospital.Folder + "/Hospital green.mat"));
        AssetDatabase.SaveAssets();
        Debug.Log($"Hospital interiors installed: {data.boxes.Length} colliders, {data.labels.Length} signs, {data.lights.Length} lights.");
    }

    /// <summary>The shell walls keep their material asset; it changes from rusty green paint to pale plaster.</summary>
    public static void RepaintShellWalls(Material walls)
    {
        if (!walls) throw new InvalidOperationException("Missing hospital wall material.");
        PreparePlasterTexture();
        var plaster = AssetDatabase.LoadAssetAtPath<Texture2D>(PlasterTexture) ?? throw new InvalidOperationException("Missing " + PlasterTexture);
        walls.SetTexture("_BaseMap", plaster);
        if (walls.HasProperty("_MainTex")) walls.SetTexture("_MainTex", plaster);
        walls.SetColor("_BaseColor", WallPlaster);
        if (walls.HasProperty("_Color")) walls.SetColor("_Color", WallPlaster);
        EditorUtility.SetDirty(walls);
    }

    static readonly (string name, Vector3 eye, Vector3 target)[] ReviewViews =
    {
        ("er", new(26, 1.3f, 64.5f), new(14, .8f, 57)), ("reception", new(17, 1.3f, 68), new(12, .9f, 85)),
        ("ward", new(35, 1.3f, 93), new(40, .8f, 79)), ("imaging", new(35, 1.5f, 66), new(40, .9f, 71)),
        ("supplies", new(26, 1.3f, 96), new(14, 1f, 102)), ("corridor", new(20, 1.3f, 68.5f), new(20, 1.1f, 92)),
        ("surgery2", new(21, 5.4f, 64.5f), new(14, 4.8f, 58)), ("surgery1", new(17, 5.4f, 77.5f), new(12, 4.8f, 69)),
        ("nurses", new(17.5f, 5.4f, 92), new(12, 4.9f, 82)), ("recovery", new(34, 5.4f, 95.5f), new(14, 4.8f, 103)),
        ("patient", new(35, 5.4f, 65), new(41, 4.8f, 75)), ("clinic", new(-15, 1.3f, 92.5f), new(-30, .9f, 92)),
        ("exam", new(-31.5f, 1.3f, 93.8f), new(-38, .8f, 91)), ("pharmacy", new(-15, 1.3f, 66.5f), new(-30, .9f, 66)),
        ("dispensary", new(-29.8f, 1.4f, 74f), new(-36, .9f, 61)), ("laundry", new(-17, 1.3f, 136), new(-36, .8f, 138)),
    };

    /// <summary>Neutral-lit review captures (temporary directional fill, no lightmaps) in an isolated preview scene.
    /// preview: build the interiors onto a throwaway copy of the shell instead of showing the saved prefab.</summary>
    public static void CaptureReview(string folder, string prefix, bool preview)
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        Directory.CreateDirectory(folder);
        try
        {
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RailHospital.PrefabPath), scene);
            if (preview)
            {
                PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                ApplyTo(RailBlock.FindEnvironment(inst.transform, RailHospital.RootName));
                var walls = AssetDatabase.LoadAssetAtPath<Material>(RailHospital.Folder + "/Hospital green.mat");
                var copy = new Material(walls);
                RepaintShellWallsCopy(copy);
                foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true)) if (r.sharedMaterial == walls) r.sharedMaterial = copy;
            }
            foreach (var r in inst.GetComponentsInChildren<MeshRenderer>(true)) r.lightmapIndex = -1;
            foreach (var t in inst.GetComponentsInChildren<TMP_Text>(true)) t.ForceMeshUpdate();
            GameObject Add(string n) { var g = new GameObject(n); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(g, scene); return g; }
            var cam = Add("Review camera").AddComponent<Camera>();
            cam.scene = scene; cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(.14f, .18f, .2f); cam.farClipPlane = 400;
            var key = Add("Review key").AddComponent<Light>(); key.type = LightType.Directional; key.intensity = 1.1f; key.transform.rotation = Quaternion.Euler(60, 30, 0);
            var fill = Add("Review fill").AddComponent<Light>(); fill.type = LightType.Directional; fill.intensity = .6f; fill.transform.rotation = Quaternion.Euler(30, 210, 0);
            void Shot(string n, int w, int h)
            {
                var rt = new RenderTexture(w, h, 24); var old = RenderTexture.active; var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
                try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply(); File.WriteAllBytes(Path.Combine(folder, prefix + n + ".png"), tex.EncodeToPNG()); }
                finally { cam.targetTexture = null; RenderTexture.active = old; Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); }
            }
            cam.orthographic = true; cam.nearClipPlane = .01f; cam.transform.rotation = Quaternion.Euler(90, 0, 0);
            foreach (var (n, pos, half, aspect) in new[] { ("cut-west-clinic-pharmacy", new Vector3(-27, 3.3f, 80), 22f, 1.6f), ("cut-laundry", new Vector3(-28, 3.3f, 136), 11f, 1.6f),
                ("cut-hospital-ground", new Vector3(26, 3.4f, 80), 27f, .8f), ("cut-hospital-upper", new Vector3(26, 7.4f, 80), 27f, .8f) })
            { cam.orthographicSize = half; cam.transform.position = pos; Shot(n, (int)(1000 * aspect), 1000); }
            cam.orthographic = false; cam.fieldOfView = 75; cam.nearClipPlane = .05f;
            foreach (var (n, eye, target) in ReviewViews)
            { cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye)); Shot("eye-" + n, 1200, 800); }
        }
        finally { UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene); }
    }

    /// <summary>The plaster cell of the Quackville Town atlas, cropped to a tiling texture with the atlas filtering.</summary>
    static void PreparePlasterTexture()
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(PlasterTexture) ?? throw new InvalidOperationException("Missing " + PlasterTexture);
        if (importer.filterMode == FilterMode.Point && importer.wrapMode == TextureWrapMode.Repeat && importer.mipmapEnabled) return;
        importer.filterMode = FilterMode.Point;
        importer.wrapMode = TextureWrapMode.Repeat;
        importer.mipmapEnabled = true;
        importer.textureCompression = TextureImporterCompression.Compressed;
        importer.SaveAndReimport();
    }

    static void RepaintShellWallsCopy(Material copy)
    {
        PreparePlasterTexture();
        var plaster = AssetDatabase.LoadAssetAtPath<Texture2D>(PlasterTexture);
        copy.SetTexture("_BaseMap", plaster);
        copy.SetColor("_BaseColor", WallPlaster);
    }

    /// <summary>Builds the interiors onto an unpacked shell instance (e.g. in a preview scene) without saving the prefab.</summary>
    public static void ApplyTo(Transform root)
    {
        PrepareModel();
        Apply(root, ReadData());
    }

    static void Apply(Transform root, Data data)
    {
        var previous = root.Find(RootName);
        if (previous) Object.DestroyImmediate(previous.gameObject);

        var hospital = root.Find("Hospital");
        var fixtures = data.fixtures.Select(P).ToArray();
        foreach (var floorName in new[] { "Ground floor", "Upper surgery floor" })
        {
            var floor = hospital.Find(floorName);
            foreach (Transform child in floor)
            {
                if (HospitalRetired.Contains(child.name)) child.gameObject.SetActive(false);
                // A shell fixture box is replaced when a ceiling troffer now sits at the same light.
                if (child.name == "Light fixture" && fixtures.Any(f => Mathf.Abs(f.y - child.localPosition.y) < 1.2f && new Vector2(f.x - child.localPosition.x, f.z - child.localPosition.z).magnitude < 1.0f))
                    child.gameObject.SetActive(false);
            }
        }
        foreach (var name in StreetBuildings)
            foreach (Transform child in root.Find(name))
                if (StreetRetired.Contains(child.name)) child.gameObject.SetActive(false);

        var group = new GameObject(RootName).transform;
        group.SetParent(root, false);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, group);
        visual.name = "Rooms";
        visual.transform.localRotation = Quaternion.Euler(0, 180, 0);
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
        {
            var go = renderer.gameObject;
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (!mesh || mesh.uv2.Length != mesh.vertexCount || renderer.sharedMaterials.Any(m => !m))
                throw new InvalidOperationException("Incomplete interior mesh " + go.name);
            go.layer = Environment;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.scaleInLightmap = 1f;
        }

        var physics = new GameObject("Physics").transform;
        physics.SetParent(group, false);
        var rooms = new Dictionary<string, Transform>();
        foreach (var box in data.boxes)
        {
            if (!rooms.TryGetValue(box.room, out var parent))
            {
                parent = new GameObject(box.room).transform;
                parent.SetParent(physics, false);
                rooms[box.room] = parent;
            }
            var go = new GameObject("Furniture collider") { layer = Environment };
            go.transform.SetParent(parent, false);
            go.transform.localPosition = P(box.c);
            go.transform.localRotation = Quaternion.Euler(0, box.yaw, 0);
            go.AddComponent<BoxCollider>().size = P(box.s);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        var signs = new GameObject("Room signs").transform;
        signs.SetParent(group, false);
        foreach (var info in data.labels)
        {
            var go = new GameObject(info.text);
            go.transform.SetParent(signs, false);
            go.transform.localPosition = P(info.pos);
            go.transform.localRotation = Quaternion.Euler(0, info.yaw, 0);
            RailHospitalCampus.AddMeshSign(go, info.text, false);
        }

        var lights = new GameObject("Room lights").transform;
        lights.SetParent(group, false);
        foreach (var info in data.lights)
        {
            var go = new GameObject("Baked interior light " + info.name);
            go.transform.SetParent(lights, false);
            go.transform.localPosition = P(info.pos);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.shadows = LightShadows.Soft;
            light.range = info.range;
            light.intensity = info.intensity;
            light.color = new Color(info.color[0], info.color[1], info.color[2]);
        }
    }
}
#endif
