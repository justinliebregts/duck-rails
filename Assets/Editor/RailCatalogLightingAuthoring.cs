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

/// <summary>Small repeatable lighting samples for the complete authored block catalogue.</summary>
[InitializeOnLoad]
public static class RailCatalogLightingAuthoring
{
    const string Root = "Assets/DuckRails/Lighting/Blocks";
    const string Key = "DuckRails.CatalogLighting.Batch";
    static string[] paths;
    static int index;
    static bool waiting;
    static string reopenScene;
    static double next;
    public static string Status { get; private set; } = "Idle";
    static RailCatalogLightingAuthoring() { EditorApplication.update += Tick; }
    public static string[] Prefabs => AssetDatabase.FindAssets("t:Prefab",new[]{RailWeek3Builder.Folder})
        .Select(AssetDatabase.GUIDToAssetPath).Where(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<RailBlock>()!=null).OrderBy(p=>p).ToArray();
    static string Folder(string prefab) => Root+"/"+Path.GetFileNameWithoutExtension(prefab);
    static string BakePath(string prefab) => Folder(prefab)+"/"+Path.GetFileNameWithoutExtension(prefab)+"-bake.unity";
    static string Payload(string prefab) => Folder(prefab)+"/"+Path.GetFileNameWithoutExtension(prefab)+"-Lighting.unity";
    static bool Excluded(Transform t,Transform root)
    {
        if(t.GetComponentInParent<Helicopter>(true)!=null) return true;
        for(var p=t;p!=null&&p!=root;p=p.parent)
            if(p.name=="Bridge hinge" || p.name.StartsWith("Spawn variation") || p.name=="Far bank" || p.name=="Exit rail" || p.name=="DAWN")return true;
        return false;
    }
    [MenuItem("Duck Rails/Lighting/All Blocks/Bake Missing Samples")]
    public static void StartBatch() => StartBatch(false);

    [MenuItem("Duck Rails/Lighting/All Blocks/Rebake All Blocks")]
    public static void RebakeAll() => StartBatch(true);

    // Resume an interrupted art-pass bake without rebaking already exported blocks.
    public static void RebakeFrom(string firstPrefab)
    {
        var remaining=Prefabs.SkipWhile(p=>p!=firstPrefab).ToArray();
        if(remaining.Length==0)throw new ArgumentException("Unknown block prefab",nameof(firstPrefab));
        StartBatch(true);
        paths=remaining;
        Status="Queued "+paths.Length+" remaining blocks";
        File.WriteAllText("output/block-lighting/bake-progress.txt",Status+"\n");
    }

    static void StartBatch(bool rebakeAll)
    {
        if(EditorApplication.isPlaying || Lightmapping.isRunning || paths!=null)throw new InvalidOperationException("Finish Play Mode or current bake first.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits first.");
        paths=Prefabs.Where(p=>rebakeAll || !File.Exists(Payload(p))).ToArray();index=0;waiting=false;next=0;reopenScene=null;
        Directory.CreateDirectory("output/block-lighting");
        Status="Queued "+paths.Length+" blocks";
        File.WriteAllText("output/block-lighting/bake-progress.txt",Status+"\n");
        SessionState.SetBool(Key,true);
    }
    static void Tick()
    {
        if(paths==null || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.timeSinceStartup<next)return;
        next=EditorApplication.timeSinceStartup+1;
        try
        {
            if(waiting)
            {
                if(Lightmapping.isRunning)return;
                try { Export(paths[index]); }
                finally
                {
                    if(!string.IsNullOrEmpty(reopenScene))
                    {
                        var scenePath=reopenScene;reopenScene=null;
                        EditorSceneManager.OpenScene(scenePath);
                    }
                }
                File.AppendAllText("output/block-lighting/bake-progress.txt","EXPORTED "+Path.GetFileNameWithoutExtension(paths[index])+"\n");
                waiting=false;index++;next=EditorApplication.timeSinceStartup+2;return;
            }
            if(index==paths.Length)
            {
                Status="Complete — "+paths.Length+" block lighting samples exported";paths=null;SessionState.SetBool(Key,false);
                File.AppendAllText("output/block-lighting/bake-progress.txt",Status+"\n");Debug.Log(Status);return;
            }
            Prepare(paths[index]);
            Status="Baking "+(index+1)+"/"+paths.Length+": "+Path.GetFileNameWithoutExtension(paths[index]);
            File.AppendAllText("output/block-lighting/bake-progress.txt",Status+"\n");
            if(!Lightmapping.BakeAsync())throw new InvalidOperationException("Unity did not start bake.");
            waiting=true;
        }
        catch(Exception ex){Status="FAILED "+ex;paths=null;SessionState.SetBool(Key,false);File.AppendAllText("output/block-lighting/bake-progress.txt",Status+"\n");Debug.LogException(ex);}
    }
    public static void Prepare(string path)
    {
        if(File.Exists(BakePath(path))){EditorSceneManager.OpenScene(BakePath(path));return;}
        Directory.CreateDirectory(Folder(path));AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
        root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
        bool finale=root.GetComponent<RailBlock>().id=="bridge-finale-v1";
        var meshes=new Dictionary<Mesh,Mesh>();
        foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
        {
            if(Excluded(renderer.transform,root.transform))continue;
            var filter=renderer.GetComponent<MeshFilter>();
            if(filter==null||filter.sharedMesh==null||renderer.sharedMaterial==null||!renderer.sharedMaterial.shader.name.StartsWith("Universal Render Pipeline/"))continue;
            if(!meshes.TryGetValue(filter.sharedMesh,out var mesh))
            {
                var original=filter.sharedMesh;mesh=Object.Instantiate(original);mesh.name=original.name+" Block UV";
                if(!Unwrapping.GenerateSecondaryUVSet(mesh))throw new InvalidOperationException("UV unwrap failed");
                AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Folder(path)+"/"+mesh.name+".asset"));meshes.Add(original,mesh);
            }
            filter.sharedMesh=mesh;renderer.receiveGI=ReceiveGI.Lightmaps;
            renderer.scaleInLightmap=renderer.name.ToLowerInvariant().Contains("ground")||renderer.name=="Finale approach"?.25f:1;
            GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,StaticEditorFlags.ContributeGI);
            PrefabUtility.RecordPrefabInstancePropertyModifications(filter);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);PrefabUtility.RecordPrefabInstancePropertyModifications(renderer.gameObject);
        }
        var fixtures=new GameObject("Baked block sample lights");fixtures.transform.SetParent(root.transform,false);
        if(root.GetComponent<RailBlock>().id == RailRoute.MidpointId) { /* Use authored pad and hut fixtures. */ }
        else if(!finale)
        {
            Spot(fixtures.transform,"Left near shelter",new Vector3(-24,3.65f,62),new Vector3(-24,.5f,62),true);
            Spot(fixtures.transform,"Left far shelter",new Vector3(-24,3.65f,98),new Vector3(-24,.5f,98),true);
            Spot(fixtures.transform,"Right near work light",new Vector3(13,4.5f,60),new Vector3(26,1,68),false);
            Spot(fixtures.transform,"Right far work light",new Vector3(13,4.5f,98),new Vector3(27,1,100),false);
        }
        else
        {
            Spot(fixtures.transform,"Bridge warning approach",new Vector3(-5,5,48),new Vector3(0,0,62),true);
            Spot(fixtures.transform,"Bridge control work light",new Vector3(12,5,65),new Vector3(18,1,75),false);
            Spot(fixtures.transform,"Bridge threshold",new Vector3(5,5,74),new Vector3(0,1,80),true);
            Spot(fixtures.transform,"Tunnel entrance work light",new Vector3(-5,6,125),new Vector3(0,1,132),false);
        }
        var lighting=root.GetComponent<RailBakedBlockLighting>()??root.AddComponent<RailBakedBlockLighting>();lighting.lightingScene=Payload(path);
        PrefabUtility.ApplyPrefabInstance(root,InteractionMode.AutomatedAction);
        // Keep the moving bridge and dawn-side pieces out of both lightmaps and baked shadows.
        foreach(var r in root.GetComponentsInChildren<Renderer>())if(Excluded(r.transform,root.transform))r.enabled=false;
        var probes=new GameObject("Block baked probes").AddComponent<LightProbeGroup>();var points=new List<Vector3>();Physics.SyncTransforms();
        float farEdge=root.GetComponent<RailBlock>().length-1;
        for(float z=1;z<=farEdge;z+=10)
            foreach(float x in new[]{-40f,-26f,-12f,0f,12f,26f,40f})
                foreach(float y in new[]{.6f,2f,5.5f})
                {var p=new Vector3(x,y,z);if(!Physics.CheckSphere(p,.15f,1<<6,QueryTriggerInteraction.Ignore))points.Add(p);}
        // Explicit far edge, separated from the adjacent block's entry probes.
        foreach(float x in new[]{-40f,0f,40f})foreach(float y in new[]{.6f,5.5f})points.Add(new Vector3(x,y,farEdge));
        probes.probePositions=points.ToArray();
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.045f,.055f,.075f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;RenderSettings.fogColor=new Color(.018f,.024f,.033f);
        var settings=new LightingSettings {name="Block Lighting",bakedGI=true,realtimeGI=false,lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,
            lightmapResolution=8,lightmapMaxSize=1024,directionalityMode=LightmapsMode.NonDirectional,directSampleCount=32,indirectSampleCount=128,
            environmentSampleCount=64,maxBounces=4,indirectScale=1.5f};
        AssetDatabase.CreateAsset(settings,Folder(path)+"/Lighting.lighting");Lightmapping.lightingSettings=settings;
        EditorSceneManager.SaveScene(scene,BakePath(path));AssetDatabase.SaveAssets();
    }
    static void Spot(Transform parent,string name,Vector3 position,Vector3 target,bool warm)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.SetPositionAndRotation(position,Quaternion.LookRotation(target-position));
        var l=go.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.shadows=LightShadows.Soft;
        l.color=warm?new Color(1,.76f,.48f):new Color(.65f,.8f,1);l.intensity=warm?42:75;l.range=warm?13:24;l.spotAngle=105;l.innerSpotAngle=60;
    }
    static void Export(string path)
    {
        if(Lightmapping.lightingDataAsset==null)throw new InvalidOperationException("Missing bake data");
        var root=Object.FindFirstObjectByType<RailBakedBlockLighting>();
        var bindings=root.GetComponentsInChildren<MeshRenderer>().Where(r=>!Excluded(r.transform,root.transform)&&r.lightmapIndex>=0&&r.lightmapIndex<LightmapSettings.lightmaps.Length)
            .Select(r=>new RailBakedBlockLighting.Binding{renderer=r,color=LightmapSettings.lightmaps[r.lightmapIndex].lightmapColor,scaleOffset=r.lightmapScaleOffset}).ToArray();
        if(bindings.Length==0||LightmapSettings.lightProbes==null||LightmapSettings.lightProbes.count==0)throw new InvalidOperationException("Missing receivers/probes");
        // Restore authoring-only visibility overrides before applying prefab data.
        foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(Excluded(renderer.transform,root.transform))PrefabUtility.RevertObjectOverride(renderer,InteractionMode.AutomatedAction);
        // Reverting a renderer can refresh its prefab instance; write bindings afterwards.
        root.bindings=bindings;
        PrefabUtility.RecordPrefabInstancePropertyModifications(root);PrefabUtility.ApplyPrefabInstance(root.gameObject,InteractionMode.AutomatedAction);
        if(AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<RailBakedBlockLighting>().bindings.Length!=bindings.Length)
            throw new InvalidOperationException("Prefab binding export did not persist: "+path);
        // Reapply bake-only exclusions to the saved authoring scene for future rebakes.
        foreach(var renderer in root.GetComponentsInChildren<Renderer>())if(Excluded(renderer.transform,root.transform))renderer.enabled=false;
        var scene=SceneManager.GetActiveScene();EditorSceneManager.SaveScene(scene);
        EditorSceneManager.SaveScene(scene,Payload(path),true);scene=EditorSceneManager.OpenScene(Payload(path));
        foreach(var go in scene.GetRootGameObjects())if(go.GetComponent<LightProbeGroup>()==null)Object.DestroyImmediate(go);
        EditorSceneManager.SaveScene(scene);
        var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==Payload(path)))scenes.Add(new EditorBuildSettingsScene(Payload(path),true));
        EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();
    }

    [MenuItem("Duck Rails/Lighting/All Blocks/Re-export Existing Bakes")]
    public static void Reexport()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning||paths!=null)throw new InvalidOperationException("Finish Play Mode/bake first.");
        foreach(var path in Prefabs){EditorSceneManager.OpenScene(BakePath(path));Export(path);}
    }

    [MenuItem("Duck Rails/Lighting/All Blocks/Bake Open Block")]
    public static void BakeOpen() => BakeOpen(false);

    [MenuItem("Duck Rails/Lighting/All Blocks/Bake Open Block And Keep Open")]
    public static void BakeOpenAndKeepOpen() => BakeOpen(true);

    static void BakeOpen(bool keepOpen)
    {
        var path=Prefabs.FirstOrDefault(p=>BakePath(p)==SceneManager.GetActiveScene().path);
        if(path==null||EditorApplication.isPlaying||Lightmapping.isRunning||paths!=null)throw new InvalidOperationException("Open a block Bake scene in Edit Mode and finish any current bake first.");
        if(!EditorSceneManager.SaveScene(SceneManager.GetActiveScene()))throw new InvalidOperationException("Could not save the block Bake scene.");
        Directory.CreateDirectory("output/block-lighting");
        reopenScene=keepOpen?SceneManager.GetActiveScene().path:null;
        paths=new[]{path};index=0;waiting=false;next=0;
    }
}
#endif
