#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RailStationLightingAuthoring
{
    public const string Folder = "Assets/DuckRails/Lighting/StartingStation";
    public const string BakeScene = Folder + "/StartingStation-bake.unity";
    public const string PayloadScene = Folder + "/StartingStationLighting.unity";
    public const string Output = "output/starting-station-lighting";

    [MenuItem("Duck Rails/Lighting/Starting Station/4 Create Offline Proof Scene")]
    public static void PrepareProof()
    {
        if(EditorApplication.isPlaying || Lightmapping.isRunning) throw new InvalidOperationException("Exit Play Mode/bake first.");
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.045f,.055f,.075f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;
        RenderSettings.fogColor=new Color(.018f,.024f,.033f);RenderSettings.skybox=null;
        var proof=new GameObject("Offline block lighting proof").AddComponent<RailStationLightingProof>();
        // EditorPlaySignIn already excludes scenes containing a LoginFlow, even inactive.
        // Keep this sentinel inactive so neither login nor any gameplay session starts.
        var loginGuard=new GameObject("Offline proof — suppress automatic editor sign-in");
        loginGuard.SetActive(false);loginGuard.AddComponent<LoginFlow>();
        proof.stationPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailStartingStationBuilder.PrefabPath);
        proof.trainVisual=CopyVisual("Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab","Train visual");
        proof.duckVisual=CopyVisual("Assets/Prefabs/Player/DuckPlayer.prefab","Duck visual");
        proof.itemVisual=CopyVisual("Assets/Resources/Rails/RailCoal.prefab","Loose coal visual");
        proof.trainVisual.position=new Vector3(0,1,80);proof.duckVisual.position=new Vector3(7,.85f,80);proof.itemVisual.position=new Vector3(6,1.3f,80);
        // This scene deliberately has no login, networking, locomotion or gameplay behaviours.
        EditorSceneManager.SaveScene(scene,Folder+"/StartingStationProof.unity");
    }
    static Transform CopyVisual(string path,string name)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        var transforms=new Dictionary<Transform,Transform>();
        Transform Copy(Transform original,Transform parent)
        {
            var copy=new GameObject(original.name).transform;copy.SetParent(parent,false);
            copy.localPosition=original.localPosition;copy.localRotation=original.localRotation;copy.localScale=original.localScale;
            transforms.Add(original,copy);
            foreach(Transform child in original)Copy(child,copy);
            return copy;
        }
        var root=Copy(source.transform,null);root.name=name;
        foreach(var entry in transforms)
        {
            var mesh=entry.Key.GetComponent<MeshFilter>();var renderer=entry.Key.GetComponent<MeshRenderer>();
            if(mesh!=null && renderer!=null){entry.Value.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh.sharedMesh;var r=entry.Value.gameObject.AddComponent<MeshRenderer>();r.sharedMaterials=renderer.sharedMaterials;r.lightProbeUsage=LightProbeUsage.BlendProbes;}
            var skin=entry.Key.GetComponent<SkinnedMeshRenderer>();
            if(skin!=null){var r=entry.Value.gameObject.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=skin.sharedMesh;r.sharedMaterials=skin.sharedMaterials;r.bones=skin.bones.Select(b=>b!=null&&transforms.ContainsKey(b)?transforms[b]:null).ToArray();r.rootBone=skin.rootBone!=null?transforms[skin.rootBone]:null;r.localBounds=skin.localBounds;r.updateWhenOffscreen=true;r.lightProbeUsage=LightProbeUsage.BlendProbes;}
        }
        return root;
    }

    [MenuItem("Duck Rails/Lighting/Starting Station/1 Create Bake Scene")]
    public static void Prepare()
    {
        if (EditorApplication.isPlaying || Lightmapping.isRunning) throw new InvalidOperationException("Use Edit Mode with no bake running.");
        if (File.Exists(BakeScene)) throw new InvalidOperationException("Bake scene exists. Open it to edit and rebake.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save open scene edits first.");
        Directory.CreateDirectory(Folder);
        AssetDatabase.Refresh();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(RailStartingStationBuilder.PrefabPath);
        var root = (GameObject)PrefabUtility.InstantiatePrefab(source);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var meshes = new Dictionary<Mesh, Mesh>();
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            var filter = renderer.GetComponent<MeshFilter>();
            if (filter == null || renderer.sharedMaterial == null || !renderer.sharedMaterial.shader.name.StartsWith("Universal Render Pipeline/")) continue;
            var original = filter.sharedMesh;
            if (!meshes.TryGetValue(original, out var mesh))
            {
                mesh = Object.Instantiate(original); mesh.name = original.name + " Station UV";
                if (!Unwrapping.GenerateSecondaryUVSet(mesh)) throw new InvalidOperationException("Unwrap failed.");
                AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(Folder + "/" + mesh.name + ".asset"));
                meshes.Add(original, mesh);
            }
            filter.sharedMesh = mesh;
            renderer.receiveGI = ReceiveGI.Lightmaps;
            renderer.scaleInLightmap = renderer.name == "Station ground" ? .25f : 1;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject, StaticEditorFlags.ContributeGI);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);
            PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
        }
        var lights = new GameObject("Baked station fixtures"); lights.transform.SetParent(root.transform, false);
        for (int side = -1; side <= 1; side += 2)
            foreach (float z in new[] { 68f, 80f, 92f })
                Spot(lights.transform, "Warm platform fixture", new Vector3(side * 7, 4, z), new Vector3(side * 5, .8f, z), new Color(1,.77f,.48f), 45, 13);
        foreach (float z in new[] { 73f, 87f })
            Spot(lights.transform, "Hall fixture", new Vector3(18, 3.9f, z), new Vector3(17,.5f,z), new Color(.65f,.8f,1), 55, 13);
        var lighting = root.AddComponent<RailBakedBlockLighting>();
        lighting.lightingScene = PayloadScene;
        PrefabUtility.ApplyPrefabInstance(root, InteractionMode.AutomatedAction);
        var probes = new GameObject("Station baked probes").AddComponent<LightProbeGroup>();
        var positions = new List<Vector3>();
        Physics.SyncTransforms();
        for (float x = -28; x <= 28; x += 7)
            for (float z = 44; z <= 116; z += 6)
                foreach (float y in new[] { 1.2f, 2.5f, 5.5f })
                {
                    var p = new Vector3(x,y,z);
                    if (!Physics.CheckSphere(p,.15f,1<<6,QueryTriggerInteraction.Ignore)) positions.Add(p);
                }
        // Dark boundary probes keep the station's baked illumination local to this block.
        foreach(float z in new[]{1f,25f,140f,159f})
            foreach(float x in new[]{-40f,0,40f})
                foreach(float y in new[]{.5f,6f}) positions.Add(new Vector3(x,y,z));
        probes.probePositions = positions.ToArray();
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.045f,.055f,.075f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = new Color(.018f,.024f,.033f);
        RenderSettings.fogDensity = .012f;
        var settings = new LightingSettings { name = "Starting Station Lighting", bakedGI = true, realtimeGI = false,
            lightmapper = LightingSettings.Lightmapper.ProgressiveCPU, lightmapResolution = 8, lightmapMaxSize = 1024,
            directionalityMode = LightmapsMode.NonDirectional, directSampleCount = 32, indirectSampleCount = 128,
            environmentSampleCount = 64, maxBounces = 4, indirectScale = 1.5f };
        AssetDatabase.CreateAsset(settings, Folder + "/StartingStation.lighting");
        Lightmapping.lightingSettings = settings;
        AssetDatabase.SaveAssets();
        EditorSceneManager.SaveScene(scene, BakeScene);
        Debug.Log("Station authoring scene ready: " + positions.Count + " probes. Bake then export.");
    }
    static void Spot(Transform parent, string name, Vector3 position, Vector3 target, Color color, float intensity, float range)
    {
        var go = new GameObject(name); go.transform.SetParent(parent,false);
        go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
        var light = go.AddComponent<Light>(); light.type=LightType.Spot; light.lightmapBakeType=LightmapBakeType.Baked;
        light.color=color; light.intensity=intensity; light.range=range; light.spotAngle=110; light.innerSpotAngle=65; light.shadows=LightShadows.Soft;
    }
    [MenuItem("Duck Rails/Lighting/Starting Station/2 Bake")]
    public static void Bake()
    {
        if (EditorApplication.isPlaying || SceneManager.GetActiveScene().path != BakeScene) throw new InvalidOperationException("Open StartingStation-bake in Edit Mode.");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
        if (!Lightmapping.BakeAsync()) throw new InvalidOperationException("Bake did not start.");
    }
    [MenuItem("Duck Rails/Lighting/Starting Station/3 Export Lighting Companion")]
    public static void Export()
    {
        var scene = SceneManager.GetActiveScene();
        if(EditorApplication.isPlaying || scene.path != BakeScene || Lightmapping.isRunning || Lightmapping.lightingDataAsset == null)
            throw new InvalidOperationException("Finish the station bake first.");
        var root = Object.FindFirstObjectByType<RailBakedBlockLighting>();
        root.bindings = root.GetComponentsInChildren<MeshRenderer>()
            .Where(r=>r.lightmapIndex >= 0 && r.lightmapIndex < LightmapSettings.lightmaps.Length)
            .Select(r=>new RailBakedBlockLighting.Binding { renderer=r, color=LightmapSettings.lightmaps[r.lightmapIndex].lightmapColor, scaleOffset=r.lightmapScaleOffset }).ToArray();
        if(root.bindings.Length==0 || LightmapSettings.lightProbes==null || LightmapSettings.lightProbes.count==0)
            throw new InvalidOperationException("Bake lacks lightmaps or probes.");
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);
        PrefabUtility.ApplyPrefabInstance(root.gameObject,InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(scene);
        // Retain baked native data but ship no duplicate renderers, colliders or gameplay scripts.
        EditorSceneManager.SaveScene(scene,PayloadScene,true);
        scene=EditorSceneManager.OpenScene(PayloadScene);
        foreach(var go in scene.GetRootGameObjects()) if(go.GetComponent<LightProbeGroup>()==null) Object.DestroyImmediate(go);
        EditorSceneManager.SaveScene(scene);
        var list=EditorBuildSettings.scenes.ToList();
        if(!list.Any(s=>s.path==PayloadScene)) list.Add(new EditorBuildSettingsScene(PayloadScene,true));
        EditorBuildSettings.scenes=list.ToArray();
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene(BakeScene);
        Debug.Log("Exported station prefab lightmap bindings and probe-only companion scene.");
    }
}
#endif
