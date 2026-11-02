#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Scene-owned first lighting sample. Rebuilding Compound requires reapplying and rebaking.
public static class RailCompoundLightingPass
{
    const string Folder = "Assets/DuckRails/Compound/LightingSample";
    public const string Output = "output/compound-lighting";

    [MenuItem("Duck Rails/Compound/Lighting/Apply Sample Setup")]
    public static void Setup()
    {
        var scene = SceneManager.GetActiveScene();
        if (EditorApplication.isPlaying || scene.path != "Assets/Scenes/Compound.unity")
            throw new InvalidOperationException("Open Compound in Edit Mode first.");
        if (GameObject.Find("Compound baked lighting sample") != null)
            throw new InvalidOperationException("Sample already installed. Edit the existing lights/probes and rebake.");
        Directory.CreateDirectory("tmp/compound-lighting");
        EditorSceneManager.SaveScene(scene, "tmp/compound-lighting/Compound-before-lighting-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        Capture("before");
        Directory.CreateDirectory(Folder + "/Meshes");
        AssetDatabase.Refresh();
        var root = GameObject.Find("Compound Geometry/SubwayCompound");
        if (root == null) throw new InvalidOperationException("Subway compound missing.");
        var copies = new Dictionary<Mesh, Mesh>();
        int receivers = 0;
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null || renderer.sharedMaterial == null ||
                renderer.sharedMaterial.shader.name != "Universal Render Pipeline/Lit") continue;
            Undo.RecordObject(renderer, "Prepare baked receiver");
            Undo.RecordObject(filter, "Assign lightmap UV mesh");
            var source = filter.sharedMesh;
            if (!copies.TryGetValue(source, out var mesh))
            {
                mesh = UnityEngine.Object.Instantiate(source);
                mesh.name = source.name + " Lightmap UV";
                if (!Unwrapping.GenerateSecondaryUVSet(mesh)) throw new InvalidOperationException("UV unwrap failed: " + source.name);
                AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Folder + "/Meshes/" + mesh.name + ".asset"));
                copies.Add(source, mesh);
            }
            filter.sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) | StaticEditorFlags.ContributeGI);
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.scaleInLightmap = renderer.name.Contains("ceiling") || renderer.name.Contains("track") ? .5f : 1;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
            receivers++;
        }
        foreach (var light in root.GetComponentsInChildren<Light>())
        {
            Undo.RecordObject(light, "Bake compound fixture");
            light.lightmapBakeType = LightmapBakeType.Baked;
            light.shadows = LightShadows.Soft;
            light.bounceIntensity = 1;
            // Existing fixture placement and user-authored intensity are retained.
            PrefabUtility.RecordPrefabInstancePropertyModifications(light);
        }
        var settings = new LightingSettings { name = "Compound Sample Lighting" };
        settings.bakedGI = true;
        settings.realtimeGI = false;
        settings.lightmapper = LightingSettings.Lightmapper.ProgressiveCPU;
        settings.lightmapResolution = 12;
        settings.lightmapMaxSize = 1024;
        settings.directionalityMode = LightmapsMode.NonDirectional;
        settings.directSampleCount = 32;
        settings.indirectSampleCount = 128;
        settings.environmentSampleCount = 64;
        settings.indirectScale = 1.5f;
        settings.maxBounces = 4;
        AssetDatabase.CreateAsset(settings, Folder + "/CompoundSample.lighting");
        Lightmapping.lightingSettings = settings;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.075f, .085f, .11f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.018f, .024f, .033f);
        RenderSettings.fogDensity = .018f;
        var sample = new GameObject("Compound baked lighting sample");
        Undo.RegisterCreatedObjectUndo(sample, "Create lighting sample");
        var probes = sample.AddComponent<LightProbeGroup>();
        var positions = new List<Vector3>();
        Physics.SyncTransforms();
        int mask = LayerMask.GetMask("Environment");
        for (float x = -8; x <= 8; x += 2)
            for (float z = -10; z <= 8; z += 2)
                foreach (float y in new[] { .3f, 1.2f, 2.4f, 4.5f })
                {
                    var p = new Vector3(x, y, z);
                    if (!Physics.CheckSphere(p, .12f, mask, QueryTriggerInteraction.Ignore)) positions.Add(p);
                }
        probes.probePositions = positions.ToArray();
        var reflectionObject = new GameObject("Baked concourse reflection");
        reflectionObject.transform.SetParent(sample.transform);
        reflectionObject.transform.position = new Vector3(0, 2, 0);
        var reflection = reflectionObject.AddComponent<ReflectionProbe>();
        reflection.mode = ReflectionProbeMode.Baked;
        reflection.resolution = 128;
        reflection.size = new Vector3(18, 7, 24);
        reflection.center = new Vector3(0, 1.5f, -1);
        reflection.boxProjection = true;
        reflection.clearFlags = ReflectionProbeClearFlags.SolidColor;
        reflection.backgroundColor = RenderSettings.fogColor;
        reflection.nearClipPlane = .15f;
        reflection.farClipPlane = 40;
        reflection.intensity = .65f;
        ApplyLensMaterial(root);
        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Compound sample prepared: " + receivers + " receivers, " + copies.Count + " UV meshes, " + positions.Count + " probes. Bake via Lighting window or Bake Sample menu.");
    }

    public static void ApplyLensMaterial(GameObject root)
    {
        const string path = Folder + "/Emissive work-light lens.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(AssetDatabase.LoadAssetAtPath<Material>(
                "Assets/DuckRails/Compound/SubwayV2/Materials/Searchlight lens.mat"));
            material.name = "Emissive work-light lens";
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", new Color(1.3f, 1.5f, 1.7f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            AssetDatabase.CreateAsset(material, path);
        }
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
            if (renderer.name == "Bright lens")
            {
                Undo.RecordObject(renderer, "Light fixture lens");
                renderer.sharedMaterial = material;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            }
    }

    [MenuItem("Duck Rails/Compound/Lighting/Bake Sample")]
    public static void Bake()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != "Assets/Scenes/Compound.unity")
            throw new InvalidOperationException("Open Compound in Edit Mode first.");
        if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Unity did not start the bake.");
    }

    [MenuItem("Duck Rails/Compound/Lighting/Capture Sample")]
    public static void CaptureAfter() => Capture("after");

    public static void Capture(string prefix)
    {
        Directory.CreateDirectory(Output);
        Render(prefix + "-forward", new Vector3(0, 2.2f, -7.8f), new Vector3(0, 2.5f, 3), 85);
        Render(prefix + "-low-eye", new Vector3(0, .5f, -3.5f), new Vector3(0, 1.1f, 5), 90);
        Render(prefix + "-info", new Vector3(-2, 1.2f, .8f), new Vector3(-5.65f, 1.3f, 3.4f), 75);
    }

    static void Render(string name, Vector3 position, Vector3 target, float fov)
    {
        var go = new GameObject("Temporary lighting review camera", typeof(Camera));
        var camera = go.GetComponent<Camera>();
        var rt = new RenderTexture(1280, 800, 24);
        var tex = new Texture2D(1280, 800, TextureFormat.RGB24, false);
        var previous = RenderTexture.active;
        try
        {
            go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(target - position));
            camera.fieldOfView = fov;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 60;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = RenderSettings.fogColor;
            camera.targetTexture = rt;
            camera.Render();
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 800), 0, 0);
            tex.Apply();
            File.WriteAllBytes(Output + "/" + name + ".png", tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = previous;
            camera.targetTexture = null;
            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(rt);
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
#endif
