#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Installs the survey camp and winch from the full-block Canyon art study.</summary>
public static class RailCanyonSurvey
{
    public const string Name = "Canyon survey works";
    const string Folder = RailCanyon.Folder + "/Survey/";
    const string Model = Folder + "Canyon Survey.fbx";
    const string MaterialPath = Folder + "Canyon Survey Atlas.mat";

    [MenuItem("Duck Rails/Authoring/Canyon/Install finished survey works")]
    public static void Install()
    {
        var content = PrefabUtility.LoadPrefabContents(RailCanyon.PrefabPath);
        try
        {
            var shell = RailBlock.FindEnvironment(content.transform, RailCanyon.RootName);
            if (!shell) throw new InvalidOperationException("Canyon shell is missing.");
            if (shell.Find(Name)) return;
            Apply(shell);
            PrefabUtility.SaveAsPrefabAsset(content, RailCanyon.PrefabPath, out var saved);
            if (!saved) throw new IOException("Could not save the Canyon block.");
        }
        finally { PrefabUtility.UnloadPrefabContents(content); }
        AssetDatabase.SaveAssets();
    }

    public static void Apply(Transform shell)
    {
        var atlas = Lit(MaterialPath, Folder + "Canyon Survey Atlas.png", Color.white);
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.isReadable = true;
        importer.generateSecondaryUV = true;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Canyon Survey Atlas"), atlas);
        importer.SaveAndReimport();

        var works = new GameObject(Name).transform;
        works.SetParent(shell, false);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model), works);
        // Convert the Blender south-facing FBX basis to the authored +Z station distance.
        model.transform.localRotation = Quaternion.Euler(0, 180, 0);
        PrefabUtility.UnpackPrefabInstance(model, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        foreach (var renderer in model.GetComponentsInChildren<MeshRenderer>())
        {
            var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
            if (renderer.sharedMaterial != atlas || !mesh || mesh.uv2.Length != mesh.vertexCount)
                throw new InvalidOperationException("Missing atlas or lightmap UVs: " + renderer.name);
            renderer.gameObject.layer = 6;
            if (renderer.name.EndsWith("_solid", StringComparison.Ordinal))
                renderer.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh;
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.scaleInLightmap = SurveyLightmapScale(renderer.name);
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
        }
        ApplyGeology(shell);
        ApplyBallast(shell);
        AddSign(works, "SURVEY CAMP", new Vector3(14, 2.43f, 14.82f), 4.1f, .65f);
        AddSign(works, "WINCH", new Vector3(-37, 2.976f, 134.16f), 5.8f, .68f);
        foreach(var ground in new[]{new Vector3(-37,0,133.8f),new(-28,0,23),new(17,0,26),
                    new(-32,0,126),new(29,0,118),new(-23,0,25),new(32,0,135)})
            AddLight(works,"Ground lantern",ground+Vector3.up*.73f);
        foreach(var landing in RailCanyon.Landings)
            AddLight(works,"Plateau lantern",landing+new Vector3(3.7f,.73f,1));
        AddRiverLanterns(works);
        AddEntranceLanterns(works);
        AddRimFenceCollisionAndTrapLight(works);
        RailCanyon.InstallMineReverb(shell);
        RailCanyon.InstallGorgeReverb(shell);
        RailCanyonContactAudio.Apply(shell);
    }

    public static float SurveyLightmapScale(string meshName)
    {
        string group=meshName.Split('_')[0];
        return group switch
        {
            "Sandstone talus" or "Rim sandstone shelves" or "Descent sandstone" or
            "Winch mine boulders" or "South rim scatter" or "Weathered rim outcrops" or "Creek stones" or
            "Shaft ledges and fallen rock" => 1.4f,
            _ => .38f
        };
    }

    public static void AddRimFenceCollisionAndTrapLight(Transform works)
    {
        var wood=AssetDatabase.LoadAssetAtPath<Game.Audio.SurfaceType>("Assets/Audio/Surfaces/Wood.asset");
        foreach(float x in new[]{-47f,47f})
        {
            var fence=new GameObject(x<0?"West timber fence collision":"East timber fence collision");
            fence.transform.SetParent(works,false);
            fence.transform.localPosition=new Vector3(x,1,80);
            fence.layer=6;
            fence.AddComponent<BoxCollider>().size=new Vector3(.2f,2,160);
            fence.AddComponent<Game.Audio.AudioSurface>().type=wood;
        }
        AddLight(works,"South trap landing lantern",new Vector3(-13.45f,-17.27f,39.15f),
            1.45f,5.5f,new Color(.4f,.68f,.86f));
    }

    public static void AddEntranceLanterns(Transform works)
    {
        var iron = AssetDatabase.LoadAssetAtPath<Material>(RailCanyon.Folder + "/Bridge iron.mat");
        var glass = AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/FarmSilos/Polish/Warm lantern glass.mat");
        foreach (var (x, start) in new[] { (-27, 89), (14, 85) })
        {
            string name = x < 0 ? "West mine entrance lantern" : "East mine entrance lantern";
            if (works.Find(name)) continue;
            int lip = Enumerable.Range(start, 20).First(z => RailCanyon.Covered(x, z));
            var anchor = new GameObject(name).transform;
            anchor.SetParent(works, false);
            anchor.localPosition = new Vector3(x, RailCanyon.CaveFloor(x, lip) + 2.7f, lip + .09f);
            void Part(string label, Vector3 center, Vector3 size, Material material)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = label;
                go.transform.SetParent(anchor, false);
                go.transform.localPosition = center;
                go.transform.localScale = size;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            }
            Part("Iron hanger", new Vector3(0,-.06f,0), new Vector3(.05f,.22f,.05f), iron);
            Part("Hood", new Vector3(0,-.18f,0), new Vector3(.38f,.08f,.38f), iron);
            Part("Amber glass", new Vector3(0,-.34f,0), new Vector3(.26f,.26f,.26f), glass);
            Part("Base", new Vector3(0,-.5f,0), new Vector3(.32f,.07f,.32f), iron);
            foreach (float dx in new[]{-.14f,.14f})
                foreach (float dz in new[]{-.14f,.14f})
                    Part("Cage bar", new Vector3(dx,-.34f,dz), new Vector3(.028f,.27f,.028f), iron);
            AddLight(anchor, "Warm entrance light", new Vector3(0,-.34f,-.27f), 6, 7);
        }
    }

    public static void AddRiverLanterns(Transform works)
    {
        foreach(var (z, offset) in new[]{(51,1.24f),(78,1.18f),(103,-1.18f)})
        {
            var position=new Vector3(RailCanyon.RiverTunnelX(z)+offset,-17.27f,z);
            AddLight(works,"Under-river lantern "+z,position,2.4f,7,new Color(.38f,.65f,.94f));
        }
        foreach(var (x, offset) in new[]{(-20,-1f),(3,1f)})
        {
            float z=RailCanyon.GalleryZ(x)+offset;
            var position=new Vector3(x,RailCanyon.CaveFloor(x,z)+.73f,z);
            AddLight(works,"Gallery lantern "+x,position,2,6,new Color(.38f,.75f,.78f));
        }
    }

    static Material Lit(string path, string texturePath, Color tint)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (!texture) throw new FileNotFoundException(texturePath);
        m.SetTexture("_BaseMap", texture);
        m.SetColor("_BaseColor", tint);
        m.SetFloat("_Smoothness", .07f);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void ApplyGeology(Transform root)
    {
        var rock = Lit(Folder + "Bedded sandstone.mat", Folder + "Sandstone beds.png", Color.white);
        var rim = Lit(Folder + "Ochre rim.mat", Folder + "Survey sand.png", Color.white);
        var filters = root.Find("Terraced canyon and hollow caves").GetComponentsInChildren<MeshFilter>();
        string path = Folder + "Stratified surface meshes.asset";
        var saved = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().ToDictionary(m => m.name);
        bool hasFile = File.Exists(path);
        foreach (var f in filters)
        {
            var renderer = f.GetComponent<MeshRenderer>();
            if (f.name.StartsWith("Rim ", StringComparison.Ordinal)) { renderer.sharedMaterial = rim; continue; }
            var src = f.GetComponent<MeshCollider>().sharedMesh;
            if (!saved.TryGetValue(src.name, out var bedded))
            {
                // Original render triangles and UV2 charts stay intact. X/Z drift gives the beds
                // a shallow local dip across the horizontal landings as well as the cliff walls.
                bedded = new Mesh();
                bedded.name = src.name;

                if (!hasFile) { AssetDatabase.CreateAsset(bedded, path); hasFile = true; }
                else AssetDatabase.AddObjectToAsset(bedded, path);
                saved.Add(src.name, bedded);
            }
            CopyBeddedSurface(src,bedded);
            f.sharedMesh = bedded;
            renderer.sharedMaterial = rock;
        }
    }

    public static void CopyBeddedSurface(Mesh source, Mesh bedded)
    {
        RailCanyon.CopySurfaceMesh(source,bedded);
        bedded.uv = source.vertices.Select(p => new Vector2(p.x * .17f + p.z * .13f,
            p.y * .43f + .075f * Mathf.Sin(p.x * .63f + p.z * .39f))).ToArray();
        EditorUtility.SetDirty(bedded);
    }

    static void ApplyBallast(Transform shell)
    {
        // The original rim and ballast tops share the same plane. Raise the visual
        // surface below the sleepers while leaving the flat rail colliders at zero.
        var trestle = shell.Find("Rickety railway trestle");
        var south = trestle.Find("South ballast support").GetComponent<MeshFilter>();
        var north = trestle.Find("North ballast support").GetComponent<MeshFilter>();
        const string path = Folder + "Ballast crown.asset";
        var crown = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!crown)
        {
            crown = Object.Instantiate(south.sharedMesh);
            crown.name = "Ballast crown";
            crown.vertices = crown.vertices.Select(v => v + Vector3.up * .015f).ToArray();
            crown.RecalculateBounds();
            AssetDatabase.CreateAsset(crown, path);
        }
        var material = Lit(Folder + "Track ballast.mat", Folder + "Survey sand.png", new Color(.76f, .79f, .8f));
        foreach (var filter in new[] { south, north })
        {
            filter.sharedMesh = crown;
            filter.GetComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    static void AddSign(Transform works, string text, Vector3 position, float width, float height)
    {
        var go = new GameObject(text);
        go.transform.SetParent(works, false);
        go.transform.localPosition = position;
        var label = go.AddComponent<TextMeshPro>();
        label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
        label.fontSharedMaterial = label.font.material;
        label.rectTransform.sizeDelta = new Vector2(width, height);
        label.enableAutoSizing = true;
        label.fontSizeMin = 1;
        label.fontSizeMax = 16;
        label.alignment = TextAlignmentOptions.Center;
        label.text = text;
        label.color = new Color(1, .90f, .72f);
        label.ForceMeshUpdate();
    }

    static void AddLight(Transform parent, string name, Vector3 position, float intensity=22, float range=13, Color? color=null)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = position;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point;
        l.lightmapBakeType = LightmapBakeType.Baked;
        l.range = range;
        l.intensity = intensity;
        l.color = color ?? new Color(1, .77f, .52f);
        l.shadows = LightShadows.Soft;
    }
}
#endif

