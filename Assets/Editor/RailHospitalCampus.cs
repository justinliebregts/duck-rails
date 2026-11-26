#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Installs the approved exterior around the existing playable hospital shell.</summary>
public static class RailHospitalCampus
{
    public const string RootName = "Finished hospital campus";
    const string Folder = RailHospital.Folder + "/Campus";
    const string ModelPath = Folder + "/Hospital Campus.fbx";
    const string MaterialPath = Folder + "/Hospital Campus Atlas.mat";

    [Serializable] class Palette { public Swatch[] materials; public Label[] labels; public Fixture[] fixtures; public Shrub[] shrubs; }
    [Serializable] class Swatch { public string name, original; }
    [Serializable] class Label { public string text; public float x, y, z, size, yaw; }
    [Serializable] class Fixture { public string name; public float x, y, z, power, range; public float[] color; }

    [Serializable] class Shrub { public float x, y, z, radius; }

    internal static void AddMeshSign(GameObject go, string text, bool exterior)
    {
        const string signs = RailHospital.Folder + "/Signs/";
        string meshName = (exterior ? "Campus - " : "Room - ") + text;
        var mesh = AssetDatabase.LoadAllAssetsAtPath(signs + "Hospital lettering.asset")
            .OfType<Mesh>().Single(m => m.name == meshName);
        string finish = exterior ? (text == "EMERGENCY" ? "Emergency coral" : "Exterior ivory") : "Interior ivory";
        var material = AssetDatabase.LoadAssetAtPath<Material>(signs + finish + ".mat");
        if (!material) throw new InvalidOperationException("Missing hospital sign material " + finish);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.receiveGI = ReceiveGI.Lightmaps;
        GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI);
    }

    static void AddBushRustle(Transform campus, Shrub[] shrubs)
    {
        var sound = AssetDatabase.LoadAssetAtPath<SoundEvent>("Assets/Audio/Events/Impact/Foliage_CornRustle.asset");
        var surface = AssetDatabase.LoadAssetAtPath<SurfaceType>("Assets/Audio/Surfaces/Foliage.asset");
        if (!sound || !surface || shrubs == null || shrubs.Length == 0)
            throw new InvalidOperationException("Missing hospital bushes or their foliage audio");
        var parent = new GameObject("Bush rustle").transform;
        parent.SetParent(campus, false);
        foreach (var group in shrubs.GroupBy(s => new Vector3Int(Mathf.FloorToInt(s.x / 8),
                     Mathf.RoundToInt(s.radius * 10), Mathf.FloorToInt(s.z / 8))))
        {
            var volume = new GameObject("Bushes " + group.Key).AddComponent<FoliageVolume>();
            volume.transform.SetParent(parent, false);
            volume.surface = surface;
            volume.rustle = sound;
            volume.radius = group.Max(s => s.radius);
            // FoliageRustle samples the listener's head when its body walks through low plants.
            volume.stalks = group.Select(s => new Vector4(s.x, s.y, s.z, 2.1f)).ToArray();
            var bounds = new Bounds((Vector3)volume.stalks[0], Vector3.zero);
            foreach (var stalk in volume.stalks)
            {
                bounds.Encapsulate((Vector3)stalk - Vector3.up * .1f);
                bounds.Encapsulate((Vector3)stalk + Vector3.up * stalk.w);
            }
            bounds.Expand(new Vector3(2 * volume.radius, 0, 2 * volume.radius));
            volume.bounds = bounds;
        }
    }

    static Palette ReadPalette() => JsonUtility.FromJson<Palette>(File.ReadAllText("ArtSource/HospitalCampus/campus.json"));

    static void PrepareModel(Palette palette)
    {
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        if (!material)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Campus color.png"));
        material.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Campus emission.png"));
        material.SetColor("_EmissionColor", Color.white * 3.5f);
        material.SetFloat("_Smoothness", .16f);
        material.EnableKeyword("_EMISSION");
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(material);

        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.importAnimation = false;
        importer.importCameras = false;
        importer.importLights = false;
        importer.generateSecondaryUV = true;
        importer.isReadable = true;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), "Hospital Campus Atlas"), material);
        foreach (var swatch in palette.materials.Where(s => !string.IsNullOrEmpty(s.original)))
        {
            var source = AssetDatabase.LoadAssetAtPath<Material>(swatch.original);
            if (!source) throw new InvalidOperationException("Missing campus prop material " + swatch.original);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), swatch.name), source);
        }
        importer.SaveAndReimport();
    }

    [MenuItem("Duck Rails/Authoring/Hospital/Install finished campus exterior")]
    public static void Install()
    {
        var contents = PrefabUtility.LoadPrefabContents(RailHospital.PrefabPath);
        try
        {
            var root = RailBlock.FindEnvironment(contents.transform, RailHospital.RootName);
            if (!root) throw new InvalidOperationException("Missing hospital gameplay shell.");
            if (root.Find(RootName)) throw new InvalidOperationException("The finished campus is already installed. Edit the existing model or prefab to preserve hand edits.");
            var palette = ReadPalette();
            PrepareModel(palette);
            Apply(root, palette);
            PrefabUtility.SaveAsPrefabAsset(contents, RailHospital.PrefabPath, out bool saved);
            if (!saved) throw new IOException("Could not save Hospital District.");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        AssetDatabase.SaveAssets();
    }

    // Also used when the original authoring command creates a fresh shell.
    public static void AddToNewShell(Transform root)
    {
        var palette = ReadPalette();
        PrepareModel(palette);
        Apply(root, palette);
    }

    static void Apply(Transform root, Palette palette)
    {
        var retain = new HashSet<string> { "Flat district ground", "Rail ballast", "Sleeper", "Rail", "Hospital", "Pharmacy", "Clinic", "Laundry utilities" };
        foreach (Transform child in root)
            if (!retain.Contains(child.name)) child.gameObject.SetActive(false);

        var hospital = root.Find("Hospital");
        var replaced = new HashSet<string> { "ER canopy", "Canopy support", "Sign HOSPITAL", "Sign EMERGENCY", "Tree trunk", "Tree crown", "Baked hospital light", "Light fixture" };
        foreach (Transform child in hospital)
            if (replaced.Contains(child.name)) child.gameObject.SetActive(false);
        var oldPlant = new HashSet<string> { "Water tank", "HVAC housing", "HVAC fan", "HVAC grille", "Vent duct", "Vent cap" };
        foreach (Transform child in hospital.Find("Accessible roof"))
            if (oldPlant.Contains(child.name)) child.gameObject.SetActive(false);

        var campus = new GameObject(RootName).transform;
        campus.SetParent(root, false);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(model, campus);
        visual.transform.localRotation = Quaternion.Euler(0, 180, 0);
        // Unpack only the model instance. Mesh/material references remain linked to the FBX.
        PrefabUtility.UnpackPrefabInstance(visual, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
        foreach (var renderer in visual.GetComponentsInChildren<MeshRenderer>())
        {
            var go = renderer.gameObject;
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            if (!renderer.sharedMaterial || !mesh || mesh.uv2.Length != mesh.vertexCount)
                throw new InvalidOperationException("Incomplete campus mesh " + go.name);
            go.layer = 6;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.scaleInLightmap = .40f;
            if (go.name.EndsWith("_solid", StringComparison.Ordinal) ||
                go.name.StartsWith("Scattered car", StringComparison.Ordinal) ||
                (go.name.StartsWith("Large court pine", StringComparison.Ordinal) && mesh.name.StartsWith("Trunk", StringComparison.Ordinal)))
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }
        AddBushRustle(campus, palette.shrubs);
        foreach (var data in palette.labels)
        {
            var go = new GameObject(data.text);
            go.transform.SetParent(campus, false);
            go.transform.localPosition = new Vector3(data.x, data.y, data.z);
            go.transform.localRotation = Quaternion.Euler(0, data.yaw, 0);
            if (data.yaw == -90) go.transform.localScale = Vector3.one * 1.35f;
            AddMeshSign(go, data.text, true);
        }
        foreach (var data in palette.fixtures)
        {
            var go = new GameObject(data.name);
            go.transform.SetParent(campus, false);
            go.transform.localPosition = new Vector3(data.x, data.y, data.z);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.shadows = LightShadows.Soft;
            light.range = data.range > 0 ? data.range : (data.power > 600 ? 17 : 11);
            light.intensity = data.power * .043f;
            light.color = new Color(data.color[0], data.color[1], data.color[2]);
        }
        FinishShell(root);
    }

    // Also used in-place on the hand-edited district, without reinstalling it.
    public static void FinishShell(Transform root)
    {
        var foundations = new[] { new Rect(-5,0,10,160), new Rect(10,55,32,50),
            new Rect(-40,57,26,19), new Rect(-40,82,26,21), new Rect(-40,126,24,20) };
        foreach (Transform tile in root.Cast<Transform>().Where(t => t.name == "Flat district ground"))
        {
            var p = tile.localPosition;
            var size = tile.localScale;
            var rect = new Rect(p.x-size.x/2,p.z-size.z/2,size.x,size.z);
            if (foundations.Any(r => r.Overlaps(rect))) tile.gameObject.SetActive(false);
        }
        foreach (Transform t in root.Find("Laundry utilities"))
            if ((t.name == "Window pier" || t.name == "Low window sill" || t.name == "Window lintel") &&
                Mathf.Abs(t.localPosition.z-126) < .01f) t.gameObject.SetActive(false);

        var hospital = root.Find("Hospital");
        foreach (var t in hospital.GetComponentsInChildren<Transform>().Where(t => t.name == "Stair rail"))
            t.gameObject.SetActive(false);
        var steel = AssetDatabase.LoadAssetAtPath<Material>(RailHospital.Folder + "/Scaffold steel.mat");
        foreach (var t in hospital.GetComponentsInChildren<Transform>().Where(t => t.name == "Light fixture").ToArray())
        {
            var p = t.localPosition;
            var light = t.parent.GetComponentsInChildren<Light>().OrderBy(l => (l.transform.localPosition-p).sqrMagnitude).First();
            if (t.parent.name == "Accessible roof" && p.y < 10)
            {
                // Keep the freestanding roof light clear of the east circulation lane.
                if (Mathf.Abs(p.x-32) < .01f)
                {
                    p += new Vector3(1.25f,0,.75f);
                    t.localPosition = p;
                    light.transform.localPosition = p - Vector3.up*.14f;
                }
                if (!t.parent.Find("Bollard at " + p.x))
                {
                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = "Bollard at " + p.x;
                    post.transform.SetParent(t.parent,false);
                    float bottom = 8, top = p.y - t.localScale.y/2;
                    post.transform.localPosition = new Vector3(p.x,(bottom+top)/2,p.z);
                    post.transform.localScale = new Vector3(.14f,top-bottom,.14f);
                    post.layer = 6;
                    post.GetComponent<MeshRenderer>().sharedMaterial = steel;
                    GameObjectUtility.SetStaticEditorFlags(post,StaticEditorFlags.ContributeGI);
                    TagSurface(post.GetComponent<Collider>(),"Metal");
                }
            }
            else if (t.parent.name == "Accessible roof" || Mathf.Abs(p.x-39) < .01f)
            {
                if (t.parent.name == "Accessible roof") p.y = 10.90f-t.localScale.y/2;
                else { p.x = 41.48f; p.y = (t.parent.name == "Ground floor" ? 3.7f : 7.7f)-t.localScale.y/2; }
                t.localPosition = p;
                light.transform.localPosition = p - Vector3.up*.14f;
            }
        }
        TagWalkableSurfaces(root);
    }

    public static void TagWalkableSurfaces(Transform root)
    {
        foreach (var collider in root.GetComponentsInChildren<Collider>())
        {
            string n = collider.name;
            string type = null;
            if (n == "Flat district ground" || n.StartsWith("Cut district turf_")) type = "Grass";
            else if (n == "Rail ballast" || n == "Courtyard paving" || n.StartsWith("Garden soil_") || n.StartsWith("Planter soil_")) type = "Dirt";
            else if (n.StartsWith("Stair steelwork_") || n.StartsWith("Laundry sheet roof_") || n == "Roof") type = "Metal";
            else if (n.StartsWith("Campus asphalt_") || n.StartsWith("Campus sidewalks_") ||
                n.StartsWith("Garden paths_") || n.StartsWith("Shelter platform_") || n.StartsWith("Planter curb_") || n.StartsWith("Roof membrane_") ||
                n.StartsWith("Floor slab ") || n == "Floor" || n == "Turn landing" ||
                n == "Ascending tread" || n == "Returning tread") type = "Stone";
            if (type != null) TagSurface(collider,type);
        }
    }

    static void TagSurface(Collider collider,string name)
    {
        var surface = collider.GetComponent<AudioSurface>() ?? collider.gameObject.AddComponent<AudioSurface>();
        if (!surface.type) surface.type = AssetDatabase.LoadAssetAtPath<SurfaceType>("Assets/Audio/Surfaces/" + name + ".asset");
    }
}
#endif
