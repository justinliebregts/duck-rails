#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RailCatalogLightingChecks
{
    const string Root="Assets/DuckRails/Lighting/Blocks";
    [MenuItem("Duck Rails/Lighting/All Blocks/Install Finale Lighting")]
    public static void InstallFinale()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Use Edit Mode.");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save scene edits first.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
        var route=Object.FindFirstObjectByType<RailRoute>();var target=route.finaleRoot;
        var source=AssetDatabase.LoadAssetAtPath<GameObject>(RailWeek3Builder.Folder+"/bridge-finale-v1.prefab").GetComponent<RailBakedBlockLighting>();
        Transform Find(Transform original)
        {
            var chain=new List<Transform>();for(var p=original;p!=source.transform;p=p.parent)chain.Add(p);
            var current=target;
            for(int i=chain.Count-1;i>=0;i--)
            {
                var part=chain[i];int ordinal=part.parent.Cast<Transform>().TakeWhile(t=>t!=part).Count(t=>t.name==part.name);
                current=current.Cast<Transform>().Where(t=>t.name==part.name).ElementAtOrDefault(ordinal);
                if(current==null)throw new InvalidOperationException("Finale geometry no longer matches bake: "+part.name);
            }
            return current;
        }
        var bindings=source.bindings.Select(b=>{
            var match=Find(b.renderer.transform);var renderer=match.GetComponent<Renderer>();
            match.GetComponent<MeshFilter>().sharedMesh=b.renderer.GetComponent<MeshFilter>().sharedMesh;
            ((MeshRenderer)renderer).receiveGI=ReceiveGI.Lightmaps;
            return new RailBakedBlockLighting.Binding{renderer=renderer,color=b.color,scaleOffset=b.scaleOffset};}).ToArray();
        var targetLighting=target.GetComponent<RailBakedBlockLighting>()??target.gameObject.AddComponent<RailBakedBlockLighting>();
        targetLighting.bindings=bindings;targetLighting.lightingScene=source.lightingScene;
        if(RailBlock.FindEnvironment(target, "Baked block sample lights")==null)Object.Instantiate(RailBlock.FindEnvironment(source.transform, "Baked block sample lights").gameObject,target,false).name="Baked block sample lights";
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Debug.Log("Finale lighting installed without replacing bridge or interaction references.");
    }
    [MenuItem("Duck Rails/Lighting/All Blocks/Create Catalogue Proof Scene")]
    public static void PrepareProof()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Use Edit Mode.");
        var prefabs=RailCatalogLightingAuthoring.Prefabs.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<RailBlock>()).ToArray();
        var catalog=prefabs.Where(b=>b.id!="bridge-finale-v1").ToArray();
        var expected=new float[catalog.Length];
        for(int i=0;i<catalog.Length;i++)
        {
            EditorSceneManager.OpenScene(Root+"/"+catalog[i].id+"/Bake.unity");
            LightProbes.GetInterpolatedProbe(new Vector3(-26,2,61),null,out var sh);expected[i]=sh[0,0];
        }
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=new Color(.045f,.055f,.075f);
        RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.012f;RenderSettings.fogColor=new Color(.018f,.024f,.033f);RenderSettings.skybox=null;
        var guard=new GameObject("Offline sign-in guard");guard.SetActive(false);guard.AddComponent<LoginFlow>();
        var proof=new GameObject("Full catalogue lighting proof").AddComponent<RailCatalogLightingProof>();
        proof.catalog=catalog;proof.expectedRedL0=expected;proof.stationPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailStartingStationBuilder.PrefabPath).GetComponent<RailBlock>();proof.finalePrefab=prefabs.Single(b=>b.id=="bridge-finale-v1");
        var seen=new HashSet<string>();var seeds=new List<int>();
        while(seen.Count<catalog.Length)
        {
            int best=Enumerable.Range(0,1000).OrderByDescending(s=>RailRoute.Choose("proof",catalog,s).blocks.Count(id=>!seen.Contains(id))).First();
            seeds.Add(best);foreach(var id in RailRoute.Choose("proof",catalog,best).blocks)seen.Add(id);
        }
        proof.seeds=seeds.ToArray();EditorSceneManager.SaveScene(scene,Root+"/CatalogueProof.unity");
        Debug.Log("Prepared "+seeds.Count+" route rounds covering all "+catalog.Length+" blocks.");
    }
    [MenuItem("Duck Rails/Lighting/All Blocks/Validate Exported Catalogue")]
    public static void Validate()
    {
        var lines=new List<string>();
        foreach(var path in RailCatalogLightingAuthoring.Prefabs)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var l=root.GetComponent<RailBakedBlockLighting>();
            if(l==null||l.bindings.Length==0||l.bindings.Any(b=>b.renderer==null||b.color==null)||!EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==l.lightingScene))throw new InvalidOperationException("Incomplete export: "+path);
            bool depot = RailBlock.FindEnvironment(root.transform, RailDepotGreybox.RootName) != null;
            bool motel = RailBlock.FindEnvironment(root.transform, RailMotelGreybox.RootName) != null;
            bool campground = RailBlock.FindEnvironment(root.transform, RailCampgroundGreybox.RootName) != null;
            bool store = RailBlock.FindEnvironment(root.transform, RailConvenienceStore.RootName) != null;
            bool farm = RailBlock.FindEnvironment(root.transform, RailFarmSilos.RootName) != null;
            bool mine = RailBlock.FindEnvironment(root.transform, RailQuarryMine.RootName) != null;
            bool canyon = RailBlock.FindEnvironment(root.transform, RailCanyon.RootName) != null;
            bool station = RailBlock.FindEnvironment(root.transform, RailAbandonedStation.RootName) != null;
            bool skyscraper = RailBlock.FindEnvironment(root.transform, RailSkyscraper.RootName) != null;
            bool hospital = RailBlock.FindEnvironment(root.transform, RailHospital.RootName) != null;
            bool yard = RailBlock.FindEnvironment(root.transform, RailContainerYard.RootName) != null;
            bool plant = RailBlock.FindEnvironment(root.transform, RailNuclearPlant.RootName) != null;
            int expected = plant ? RailNuclearPlant.LightCount : yard ? RailContainerYard.LightCount : hospital ? RailHospital.LightCount : depot ? 39 : motel ? 30 : campground ? 11 : store ? RailConvenienceStore.LightCount : farm ? RailFarmSilos.LightCount : mine ? RailQuarryMine.LightCount : canyon ? RailCanyon.LightCount : station ? RailAbandonedStation.LightCount : skyscraper ? RailSkyscraper.LightCount : 4;
            var fixtures = plant || yard || hospital || campground || farm || mine || canyon || station || skyscraper ? root.transform : RailBlock.FindEnvironment(root.transform, depot ? RailDepotGreybox.RootName : motel ? RailMotelGreybox.RootName : store ? RailConvenienceStore.RootName : "Baked block sample lights");
            if(fixtures == null || fixtures.GetComponentsInChildren<Light>().Count(light=>light.lightmapBakeType==LightmapBakeType.Baked)!=expected)throw new InvalidOperationException("Expected " + expected + " baked lights: "+path);
            lines.Add("PASS "+root.name+": " + expected + " lights; "+l.bindings.Length+" static receiver bindings; companion in build.");
        }
        Directory.CreateDirectory("output/block-lighting");File.WriteAllLines("output/block-lighting/export-checks.txt",lines);Debug.Log(string.Join("\n",lines));
    }
}
#endif
