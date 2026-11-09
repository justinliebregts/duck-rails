#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RailHelipadRouteChecks
{
    public static void Run()
    {
        int checks=0;
        void Check(bool ok,string message){if(!ok)throw new Exception(message);checks++;}
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>(RailHelipadPreview.Path);
        var block=asset.GetComponent<RailBlock>();var source=CrawlGraph.Load(block.graph);
        // 220m deliberately shifts the original cluster grid by 12 cells.
        var merged=RailGraphAssembly.Combine(new[]{source,source},new[]{Vector3.zero,Vector3.forward*220});
        Check(merged.NodeCount==2*source.NodeCount && merged.EdgeCount==2*source.EdgeCount && merged.ClusterCount==2*source.ClusterCount,"Authored topology changed");
        for(int n=0;n<merged.NodeCount;n++)
        {
            int chunk=merged.ChunkOfKey(merged.NodeKey[n]);
            Check(n>=merged.ChunkStart[chunk]&&n<merged.ChunkStart[chunk+1],"Node chunk index");
            merged.CoordsOf(merged.NodeKey[n],out int x,out int y,out int z);
            Check(merged.FindNodeAtCell(x,y,z)==n,"Translated exact-cell lookup");
            int owner=merged.NodeCluster[n]/source.ClusterCount;
            for(int e=merged.EdgeStart[n];e<merged.EdgeStart[n+1];e++)Check(merged.NodeCluster[merged.EdgeTarget[e]]/source.ClusterCount==owner,"Cross-block edge introduced");
        }
        var random=UnityEngine.Random.state;
        try{
            UnityEngine.Random.InitState(9817);int split=0;
            for(int c=0;c<merged.ClusterCount;c++)
            {
                var chunks=Enumerable.Range(0,merged.NodeCount).Where(n=>merged.NodeCluster[n]==c).Select(n=>merged.ChunkOfKey(merged.NodeKey[n])).Distinct().ToArray();
                var seen=new HashSet<int>();
                for(int i=0;i<128;i++){int n=merged.RandomNodeInCluster(c);Check(n>=0&&merged.NodeCluster[n]==c,"Cluster member sampling");seen.Add(merged.ChunkOfKey(merged.NodeKey[n]));}
                if(chunks.Length>1){split++;Check(seen.Count>1,"Sampling stranded in representative chunk");}
            }
            Check(split>0,"Shifted cluster case not exercised");
        }finally{UnityEngine.Random.state=random;}
        bool rejected=false;try{RailGraphAssembly.Combine(new[]{source,source},new[]{Vector3.zero,new Vector3(0,0,220.25f)});}catch(ArgumentException){rejected=true;}Check(rejected,"Fractional-cell shift must reject");
        var setup=EditorSceneManager.GetSceneManagerSetup();
        try{
            EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");var route=Object.FindFirstObjectByType<RailRoute>();
            Check(route.midpointBlock!=null&&route.midpointBlock.id==RailRoute.MidpointId,"Scene helipad reference");
            Check(route.transform.Find("Midpoint Shop")==null,"Old midpoint remains");
            Check(Object.FindObjectsByType<RailGoldReceiver>(FindObjectsInactive.Include,FindObjectsSortMode.None).Count(r=>r.receiverId==2)==1,"Duplicate midpoint receiver");
            Check(route.midpointBlock.GetComponent<RailBakedBlockLighting>().bindings.Length>0,"Lighting missing");
            Check(Vector3.Dot(route.midpointBlock.transform.Find("Env/Parked helicopter").forward,Vector3.back)>.99f,"Helicopter faces wrong way");
            var state=RailRoute.ChooseNormal("helipad-check",route.catalog,27);var copy=JsonUtility.FromJson<RailRouteState>(JsonUtility.ToJson(state));
            RailRoute.Validate(copy,route.catalog);Check(copy.layoutVersion==RailRoute.LayoutVersion,"Retained version");
            var scene=EditorSceneManager.NewPreviewScene();
            try{
                var go=new GameObject("Route fixture");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);var test=go.AddComponent<RailRoute>();test.catalog=route.catalog;
                var data=new GameObject("Inactive gameplay");data.transform.SetParent(go.transform);data.SetActive(false);test.expedition=data.AddComponent<RailExpedition>();test.manager=data.AddComponent<Game.AI.EnemyManager>();
                test.startingStation=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/StartingStation.prefab"),scene)).GetComponent<RailBlock>();
                test.finale=((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/bridge-finale-v1.prefab"),scene)).GetComponent<RailBlock>();test.finaleRoot=test.finale.transform;
                test.midpointBlock=((GameObject)PrefabUtility.InstantiatePrefab(asset,scene)).GetComponent<RailBlock>();test.Build(copy);
                var travel=test.TravelBlocks.ToArray();Check(travel.Length==5&&travel[2]==test.midpointBlock,"Travel order");
                float distance=RailRoute.StationDepartureLength;
                foreach(var b in travel){foreach(float local in new[]{.01f,b.TrackLength*.5f,b.TrackLength-.01f}){test.Sample(distance+local,out var actual,out var rot);b.Sample(local,out var expected,out var er);Check(Vector3.Distance(actual,expected+Vector3.up)<.002f,"Train sampling "+b.id);Check(Quaternion.Angle(rot,er)<.02f,"Train heading");}distance+=b.TrackLength;}
                Check(Mathf.Abs(distance-test.FinaleStart)<.002f,"Finale distance");
                for(int i=1;i<travel.Length;i++)Check(Mathf.Abs(travel[i-1].transform.position.z+travel[i-1].length-travel[i].transform.position.z)<.001f,"Block seam");
                var visibility=go.AddComponent<RailBlockVisibility>();visibility.Rebuild(test);visibility.ApplyViewerPosition(test.midpointBlock.transform.position+Vector3.forward*30);
                Check(!test.midpointBlock.transform.Find("Env/Ground 96 x 60").GetComponent<Renderer>().forceRenderingOff,"Helipad hidden at shop");
                Check(travel[0].GetComponentsInChildren<Renderer>().Any(r=>r.forceRenderingOff),"Distant block remained visible");
                Check(test.TryBlockSkip(test.midpointBlock.transform.position+Vector3.forward*20,out var skip)&&Mathf.Abs(skip.z-test.midpointBlock.transform.position.z-80)<.001f,"60m block skip");
                copy.layoutVersion=0;rejected=false;try{RailRoute.Validate(copy,test.catalog);}catch(InvalidOperationException){rejected=true;}Check(rejected,"Old retained layout accepted");
            }finally{EditorSceneManager.ClosePreviewScene(scene);}
        }finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        Directory.CreateDirectory("docs/validation/helipad");File.WriteAllText("docs/validation/helipad/route-checks.txt","PASS "+checks+" assertions: shifted graph topology/queries/cluster sampling, retained layout, scene receiver uniqueness, train sampling/seams, visibility and skip.\n");Debug.Log("HELIPAD ROUTE: "+checks+" assertions passed");
    }
}
#endif
