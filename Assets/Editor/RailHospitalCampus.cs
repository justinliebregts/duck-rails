#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
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

    [Serializable] class Palette { public Swatch[] materials; public Label[] labels; public Fixture[] fixtures; }
    [Serializable] class Swatch { public string name, original; }
    [Serializable] class Label { public string text; public float x, y, z, size, yaw; }
    [Serializable] class Fixture { public string name; public float x, y, z, power; public float[] color; }

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
        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
        foreach (var data in palette.labels)
        {
            var go = new GameObject(data.text);
            go.transform.SetParent(campus, false);
            go.transform.localPosition = new Vector3(data.x, data.y, data.z);
            go.transform.localRotation = Quaternion.Euler(0, data.yaw, 0);
            var label = go.AddComponent<TextMeshPro>();
            label.font = font;
            label.fontSharedMaterial = font.material;
            label.text = data.text;
            label.alignment = TextAlignmentOptions.Center;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.rectTransform.sizeDelta = new Vector2(26, 3);
            label.fontSize = data.size * 6;
            label.color = new Color(1f, .89f, .70f);
            label.ForceMeshUpdate();
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
            light.range = data.power > 600 ? 17 : 11;
            light.intensity = data.power * .043f;
            light.color = new Color(data.color[0], data.color[1], data.color[2]);
        }
    }
}
#endif
