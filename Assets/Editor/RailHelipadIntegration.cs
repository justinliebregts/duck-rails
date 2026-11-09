#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RailHelipadIntegration
{
    [MenuItem("Duck Rails/Helipad/Rebake Navigation")]
    public static void RebakeNavigation()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Use Edit Mode outside a lighting bake.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty) throw new InvalidOperationException("Save the prefab first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        foreach (var s in setup)
            if (s.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty)
                throw new InvalidOperationException("Save open scenes first.");
        var stagePath = stage == null ? null : stage.assetPath;
        StageUtility.GoToMainStage();
        try { RailFullAreaNavigation.BakeOne(RailHelipadPreview.Path); }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
        }
    }

    public static void InstallAndBakeNavigation()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying)throw new Exception("Wrong project or Play Mode");
        StageUtility.GoToMainStage();
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
        string old="Assets/DuckRails/Blocks/midpoint-helipad-preview.prefab";
        if(AssetDatabase.LoadAssetAtPath<GameObject>(old)!=null){string error=AssetDatabase.MoveAsset(old,RailHelipadPreview.Path);if(error!="")throw new Exception(error);}
        var contents=PrefabUtility.LoadPrefabContents(RailHelipadPreview.Path);
        try{
            contents.name="Midpoint Helipad";var b=contents.GetComponent<RailBlock>();b.id=RailRoute.MidpointId;b.displayName="Helipad Supply Stop";b.tags=new[]{"shop","helipad"};
            var mount=contents.transform.Find("Env/Parked helicopter");mount.localRotation=Quaternion.Euler(0,180,0);
            foreach(var light in contents.GetComponentsInChildren<Light>(true))if(light.GetComponentInParent<Helicopter>()==null){light.intensity=12;light.range=8;}
            var baker=b.GetComponentInChildren<CrawlGraphBaker>()??new GameObject("Authored crawlgraph settings").AddComponent<CrawlGraphBaker>();baker.transform.SetParent(contents.transform,false);
            var example=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/convenience-store-v1.prefab").GetComponent<RailBlock>();var graph=CrawlGraph.Load(example.graph);
            var lo=new Vector3(-50,-3,-2);for(int axis=0;axis<3;axis++)lo[axis]=graph.Origin[axis]+Mathf.Floor((lo[axis]-graph.Origin[axis])/graph.CellSize)*graph.CellSize;
            var size=new Vector3(112,32,80);baker.transform.localPosition=lo+size*.5f;baker.size=size;baker.cellSize=graph.CellSize;baker.agentRadius=.25f;baker.surfaceMask=1<<6;baker.allowCeilings=false;baker.minIslandNodes=10;baker.seeds=Array.Empty<Transform>();baker.drawNodes=false;
            const string graphPath="Assets/DuckRails/Blocks/midpoint-helipad-v1.bytes";if(!File.Exists(graphPath))File.WriteAllBytes(graphPath,new byte[8]);AssetDatabase.ImportAsset(graphPath);baker.graphAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(graphPath);b.graph=baker.graphAsset;
            PrefabUtility.SaveAsPrefabAsset(contents,RailHelipadPreview.Path);
        }finally{PrefabUtility.UnloadPrefabContents(contents);}
        var route=Object.FindFirstObjectByType<RailRoute>();
        var obsolete=route.transform.Find("Midpoint Shop");if(obsolete!=null)Object.DestroyImmediate(obsolete.gameObject);
        if(route.midpointBlock==null){var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RailHelipadPreview.Path),route.transform);go.name="Midpoint Helipad";route.midpointBlock=go.GetComponent<RailBlock>();}
        route.midpointBlock.transform.SetPositionAndRotation(new Vector3(0,0,470),Quaternion.identity);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        // Old size-only prefab is no longer a route asset. Preserve source RailShop for departure.
        AssetDatabase.DeleteAsset("Assets/DuckRails/Blocks/midpoint-shop-preview.prefab");
        Directory.CreateDirectory("docs/validation/helipad");
        RailFullAreaNavigation.BakeOne(RailHelipadPreview.Path);
        EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
        Debug.Log("Installed dedicated helipad; removed old scene shop and size preview; navigation baked.");
    }
    public static void StartLighting()
    {
        StageUtility.GoToMainStage();RailCatalogLightingAuthoring.Prepare(RailHelipadPreview.Path);RailCatalogLightingAuthoring.BakeOpen();
    }
}
#endif
