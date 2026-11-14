#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class RailMotelFullAtlasBenchmark
{
    public const string Folder="Assets/DuckRails/MotelFullAtlasBenchmark";
    public const string Prefab=Folder+"/Motel-FullMap-Comparison.prefab";
    public static bool Building;
    internal static int Substitutions;
    static Transform Resolve(Transform root,string key){foreach(var index in key.Split('/'))root=root.GetChild(int.Parse(index));return root;}
    [MenuItem("Duck Rails/Tests/Quest Performance/Prepare Motel Full Map Comparison")]
    public static void Prepare()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying)throw new Exception("Requires Duck Rails Edit Mode");
        if(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab))throw new Exception("Benchmark already prepared; refusing overwrite");
        const string source="Assets/DuckRails/Blocks/roadside-motel-v1.prefab";
        var payload=JsonUtility.FromJson<RailMotelFullAtlasReview.Payload>(File.ReadAllText("ArtSource/MotelAtlas/unity-meshes.json"));
        if(payload.sourceHash!=AssetDatabase.GetAssetDependencyHash(source).ToString())throw new Exception("Production source changed since atlas export");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        if(!AssetDatabase.CopyAsset(source,Prefab))throw new IOException("Could not clone baseline");
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var owner=root.GetComponent<RailBakedBlockLighting>();var bindings=owner.bindings.ToList();
            var opt=root.GetComponent<RailSceneryChunks>();
            var atlasSources=payload.sourceKeys.Select(k=>Resolve(root.transform,k).GetComponent<MeshRenderer>()).ToHashSet();
            // Capture the exact saved production consolidation, with no re-meshing.
            foreach(var renderer in opt.sources)renderer.enabled=false;
            opt.chunkRoot.SetActive(true);bindings.AddRange(opt.chunkLighting);
            var control=opt.chunkRoot.GetComponentsInChildren<Renderer>(true)
                .Concat(atlasSources.Where(r=>r.enabled)).ToArray();
            UnityEngine.Object.DestroyImmediate(opt);
            var review=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/MotelAtlasReview/Motel-FullAtlas-Review.prefab");
            var sourceVisual=review.transform.Find("Blender full Motel - atlas review");
            var atlas=UnityEngine.Object.Instantiate(sourceVisual.gameObject,root.transform,false);
            atlas.name="Full-map atlas comparison";atlas.SetActive(false);
            var originalChunks=sourceVisual.GetComponentsInChildren<MeshRenderer>(true);
            var clonedChunks=atlas.GetComponentsInChildren<MeshRenderer>(true);
            var reviewBindings=review.GetComponent<RailBakedBlockLighting>().bindings;
            for(int i=0;i<originalChunks.Length;i++)
            {
                var binding=reviewBindings.Single(b=>b.renderer==originalChunks[i]);binding.renderer=clonedChunks[i];bindings.Add(binding);
            }
            var selector=root.AddComponent<RailMotelFullAtlasComparison>();selector.controlOnly=control;selector.atlas=atlas;
            owner.bindings=bindings.ToArray();
            if(control.Length!=757 || clonedChunks.Length!=305 || bindings.Any(b=>!b.renderer||!b.color))throw new Exception("Unexpected fixture counts/bindings");
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("docs/validation/motel-full-atlas");
            File.WriteAllText("docs/validation/motel-full-atlas/benchmark-setup.txt",
                "Exact production meshes/materials for control: 698 chunks + 69 originals = 767 active.\nAtlas: 305 chunks + 10 common originals = 315 active.\n757 control-only renderers; 10 common original renderers; both alternatives resident.\nOriginal collision/gameplay/lighting companion retained; 2967 lightmap bindings.\nBuild-only catalogue substitution; saved production assets untouched.\n");
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("Duck Rails/Tests/Quest Performance/Build Motel Full Map Comparison")]
    public static void Build()
    {
        if(RailMotelWingBenchmark.Building)throw new Exception("Conflicting comparison scope");
        Substitutions=0;Building=true;
        File.WriteAllText("docs/validation/motel-full-atlas/build-substitution.txt","Started full-map comparison build\n");
        try {RailQuestPerformanceBuild.Build();}
        finally {Building=false;}
    }
}
public sealed class RailMotelFullAtlasBuildScene : IProcessSceneWithReport, IPostprocessBuildWithReport
{
    public int callbackOrder=>101;
    public void OnProcessScene(Scene scene,BuildReport report)
    {
        if(!RailMotelFullAtlasBenchmark.Building)return;
        if(report==null||(report.summary.options&BuildOptions.Development)==0)throw new BuildFailedException("Full-map comparison requires explicit development build");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailMotelFullAtlasBenchmark.Prefab);
        if(!prefab)throw new BuildFailedException("Prepare full-map comparison first");
        var block=prefab.GetComponent<RailBlock>();
        foreach(var route in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<RailRoute>(true)))
            for(int i=0;i<route.catalog.Length;i++)if(route.catalog[i]&&route.catalog[i].id==block.id)
            {
                route.catalog[i]=block;RailMotelFullAtlasBenchmark.Substitutions++;
                File.AppendAllText("docs/validation/motel-full-atlas/build-substitution.txt",scene.path+" -> "+RailMotelFullAtlasBenchmark.Prefab+"\n");
            }
    }
    public void OnPostprocessBuild(BuildReport report)
    {
        if(!RailMotelFullAtlasBenchmark.Building)return;
        if(RailMotelFullAtlasBenchmark.Substitutions==0)throw new BuildFailedException("Full-map fixture was not injected; reject cached build");
        File.AppendAllText("docs/validation/motel-full-atlas/build-substitution.txt","PASS substitutions="+RailMotelFullAtlasBenchmark.Substitutions+" buildGuid="+report.summary.guid+"\n");
    }
}
#endif
