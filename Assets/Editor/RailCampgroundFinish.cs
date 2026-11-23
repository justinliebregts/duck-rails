#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Adds the approved Pine Lake exterior to the existing walkable campground and bake scene.</summary>
public static class RailCampgroundFinish
{
    public const string RootName = "Pine Lake finish";
    public const int LightCount = 19;
    public const string Folder = RailCampgroundGreybox.Folder + "/Finish";
    const string ModelPath = Folder + "/Lakeside finish.fbx";
    const string AtlasPath = Folder + "/Lakeside atlas.mat";

    [Serializable] class Layout { public Label[] labels; public Fixture[] fixtures; }
    [Serializable] class Label { public string text; public float x, y, z, size; }
    [Serializable] class Fixture { public string name; public float x, y, z, power; }

    [MenuItem("Duck Rails/Authoring/Campground/Install approved Pine Lake exterior")]
    public static void Install()
    {
        if (EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish the current play session or bake before installing the exterior.");
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(RailCampgroundGreybox.PrefabPath);
        if (RailBlock.FindEnvironment(template.transform, RailCampgroundGreybox.RootName + "/" + RootName))
        {
            Debug.Log("The Pine Lake exterior is already installed.");
            return;
        }
        PrepareAtlas();
        var prefab = PrefabUtility.LoadPrefabContents(RailCampgroundGreybox.PrefabPath);
        try
        {
            var source = RailBlock.FindEnvironment(prefab.transform,RailCampgroundGreybox.RootName);
            if (!source) throw new InvalidOperationException("The campground terrain shell is missing.");
            FinishShellSurfaces(source);
            var additions = new GameObject(RootName).transform;
            additions.SetParent(source, false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, additions);
            instance.transform.localRotation = Quaternion.Euler(0,180,0);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                if (!mesh || !renderer.sharedMaterial || mesh.uv2.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Missing finish mesh or lighting UVs on " + renderer.name);
                var go = renderer.gameObject;
                go.layer = 6;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                renderer.receiveGI = ReceiveGI.Lightmaps;
                renderer.scaleInLightmap = .5f;
                if (go.name.EndsWith("_solid", StringComparison.Ordinal)) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
            var layout = JsonUtility.FromJson<Layout>(File.ReadAllText("ArtSource/CampgroundFinish/fixtures.json"));
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            foreach (var text in source.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
                text.ForceMeshUpdate();
            }
            foreach (var sign in layout.labels)
            {
                var go = new GameObject(sign.text);
                go.transform.SetParent(additions, false);
                go.transform.localPosition = new Vector3(sign.x, sign.y, sign.z);
                var label = go.AddComponent<TextMeshPro>();
                label.font = font;
                label.fontSharedMaterial = font.material;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                label.rectTransform.sizeDelta = new Vector2(4.1f,1.15f);
                label.enableAutoSizing = true;
                label.fontSizeMin = 1;
                label.fontSizeMax = 6;
                label.text = sign.text;
                label.color = new Color(1,.9f,.72f);
                label.ForceMeshUpdate();
            }
            foreach (var fixture in layout.fixtures)
            {
                var go = new GameObject(fixture.name);
                go.transform.SetParent(additions, false);
                go.transform.localPosition = new Vector3(fixture.x,fixture.y,fixture.z);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.color = fixture.name == "Embers" ? new Color(1,.48f,.25f) : new Color(1,.78f,.55f);
                light.intensity = fixture.power * .041f;
                light.range = fixture.name == "Embers" ? 6 : 10;
                light.shadows = LightShadows.Soft;
            }
            PrefabUtility.SaveAsPrefabAsset(prefab,RailCampgroundGreybox.PrefabPath,out bool success);
            if (!success) throw new IOException("Could not save the Lakeside Campground prefab.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
    }

    static void FinishShellSurfaces(Transform shell)
    {
        var variants = new System.Collections.Generic.Dictionary<Material, Material>();
        foreach (var renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
        {
            var surfaces = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var original = surfaces[i];
                if (!original || !AssetDatabase.GetAssetPath(original).StartsWith(RailCampgroundGreybox.Folder + "/Quackville ")) continue;
                if (!variants.TryGetValue(original, out var finished))
                {
                    string name = original.name.Substring("Quackville ".Length);
                    string path = Folder + "/Surfaces/Pine Lake " + name;
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png");
                    if (!texture) throw new InvalidOperationException("Missing Pine Lake finish texture " + name);
                    finished = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
                    if (!finished) { finished = new Material(original); AssetDatabase.CreateAsset(finished,path + ".mat"); }
                    finished.SetTexture("_BaseMap", texture);
                    finished.SetColor("_BaseColor", Color.white);
                    finished.SetFloat("_Smoothness",name == "lake water" ? .28f : .16f);
                    EditorUtility.SetDirty(finished);
                    variants.Add(original, finished);
                }
                surfaces[i] = finished;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = surfaces;
        }
    }
    static void PrepareAtlas()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasPath);
        if (!atlas)
        {
            atlas = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(atlas, AtlasPath);
        }
        atlas.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Campground color.png"));
        atlas.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Campground emission.png"));
        atlas.SetColor("_EmissionColor", Color.white * 3.5f);
        atlas.SetFloat("_Smoothness", .12f);
        atlas.EnableKeyword("_EMISSION");
        atlas.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(atlas);
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        importer.importAnimation = false;
        importer.importLights = false;
        importer.importCameras = false;
        importer.isReadable = true;
        importer.generateSecondaryUV = true;
        importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),"Lakeside atlas"),atlas);
        importer.SaveAndReimport();
    }
}
#endif
