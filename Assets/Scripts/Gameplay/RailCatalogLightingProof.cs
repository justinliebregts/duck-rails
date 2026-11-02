#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Exercises the actual route builder without session/login dependencies.</summary>
public sealed class RailCatalogLightingProof : MonoBehaviour
{
    public RailBlock[] catalog;
    public RailBlock stationPrefab, finalePrefab;
    public int[] seeds;
    public Vector3 samplePosition = new Vector3(-26,2,61);
    public float[] expectedRedL0;
    public string Result { get; private set; } = "Running";
    readonly StringBuilder log = new StringBuilder();
    const string Output="output/block-lighting";
    void Check(bool ok,string message)
    {
        log.AppendLine((ok?"PASS ":"FAIL ")+message);File.WriteAllText(Output+"/runtime-proof.txt",log.ToString());
        if(!ok){Result="FAIL "+message;throw new InvalidOperationException(message);}
    }
    int ProbeCount => Enumerable.Range(0,SceneManager.sceneCount).Sum(i=>LightProbes.GetSharedLightProbesForScene(SceneManager.GetSceneAt(i))?.countSelf??0);
    int PayloadCount => Enumerable.Range(0,SceneManager.sceneCount).Count(i=>SceneManager.GetSceneAt(i).path.Contains("/Lighting/")&&SceneManager.GetSceneAt(i).path.EndsWith("Lighting.unity"));
    public IEnumerator Start()
    {
        Directory.CreateDirectory(Output);int baseMaps=LightmapSettings.lightmaps.Length;
        Check(ProbeCount==0 && PayloadCount==0,"Clean initial lighting registry");
        var station=Instantiate(stationPrefab,new Vector3(0,0,-10),Quaternion.identity);
        var finale=Instantiate(finalePrefab,Vector3.zero,Quaternion.identity);
        foreach(var behaviour in finale.GetComponentsInChildren<MonoBehaviour>())
            if(!(behaviour is RailBlock)&&!(behaviour is RailBakedBlockLighting))behaviour.enabled=false;
        var routeObject=new GameObject("Actual route builder under test");var route=routeObject.AddComponent<RailRoute>();
        var data=new GameObject("Inactive gameplay data for route build");data.SetActive(false);
        route.expedition=data.AddComponent<RailExpedition>();route.manager=data.AddComponent<Game.AI.EnemyManager>();
        route.catalog=catalog;route.startingStation=station;route.finale=finale;route.finaleRoot=finale.transform;
        var seen=new HashSet<string>();
        for(int round=0;round<seeds.Length;round++)
        {
            var selected=RailRoute.Choose("lighting-proof-"+round,catalog,seeds[round]);
            route.Build(selected);
            Check(!route.Ready,"Route waits for all new lighting loads, round "+round);
            float end=Time.realtimeSinceStartup+120;
            while(!route.Ready && Time.realtimeSinceStartup<end)yield return null;
            Check(route.Ready,"All seven lighting owners ready, round "+round);
            // Previous blocks are destroyed at end-of-frame; wait for their serialized unloads too.
            end=Time.realtimeSinceStartup+120;
            while(PayloadCount!=7 && Time.realtimeSinceStartup<end)yield return null;
            Check(PayloadCount==7,"Exactly seven payloads after route replacement, round "+round);
            var owners=route.Blocks.Concat(new[]{station,finale}).Select(b=>b.GetComponent<RailBakedBlockLighting>()).ToArray();
            var maps=LightmapSettings.lightmaps;
            foreach(var owner in owners)
            {
                Check(owner!=null && owner.Ready && owner.bindings.Length>0 && owner.bindings.All(b=>b.renderer!=null && b.renderer.lightmapIndex>=0 && b.renderer.lightmapIndex<maps.Length && maps[b.renderer.lightmapIndex].lightmapColor==b.color && b.renderer.lightmapScaleOffset==b.scaleOffset),"Atlas bindings valid for "+owner.name);
            }
            foreach(var block in route.Blocks)
            {
                int index=Array.FindIndex(catalog,b=>b.id==block.id);
                LightProbes.GetInterpolatedProbe(block.transform.position+samplePosition,null,out var sh);
                Check(Mathf.Abs(sh[0,0]-expectedRedL0[index])<.01f,"Translated baked sample matches authoring data: "+block.id);
                if(seen.Add(block.id))RailStationLightingProof.CaptureTo(Output,block.id,block.transform.position+new Vector3(-10,2.5f,48),block.transform.position+new Vector3(-24,1.5f,64));
            }
            var hinge=finale.transform.Find("Bridge hinge");
            Check(hinge!=null&&!owners.Last().bindings.Any(b=>b.renderer.transform.IsChildOf(hinge)),"Moving bridge excluded from static lightmaps");
            var initial=finale.transform.position;LightProbes.GetInterpolatedProbe(initial+new Vector3(0,2,71),null,out var before);
            finale.transform.position+=Vector3.forward*8;
            Check(!route.Ready,"Finale translation temporarily closes readiness gate");
            end=Time.realtimeSinceStartup+15;while(!route.Ready&&Time.realtimeSinceStartup<end)yield return null;
            LightProbes.GetInterpolatedProbe(finale.transform.position+new Vector3(0,2,71),null,out var after);
            Check(route.Ready&&Mathf.Abs(before[0,0]-after[0,0])<.01f,"Finale native probes follow translated placement");
            finale.transform.position=initial;
            end=Time.realtimeSinceStartup+15;while(!route.Ready&&Time.realtimeSinceStartup<end)yield return null;
            Check(route.Ready,"Finale placement restored");
            log.AppendLine("INFO round "+round+": maps="+LightmapSettings.lightmaps.Length+", probes="+ProbeCount+", seed="+seeds[round]);
        }
        Check(seen.Count==12,"All twelve scavenging blocks covered across assembled routes");
        Destroy(routeObject);Destroy(station.gameObject);Destroy(finale.gameObject);Destroy(data);
        float cleanup=Time.realtimeSinceStartup+120;
        do{yield return null;}while((PayloadCount!=0||ProbeCount!=0)&&Time.realtimeSinceStartup<cleanup);
        Check(PayloadCount==0&&ProbeCount==0&&LightmapSettings.lightmaps.Length==baseMaps,"Final teardown restores scenes, probes and lightmaps to baseline");
        Result="PASS";log.AppendLine("PASS Full catalogue offline Play Mode workflow. No multiplayer/headset acceptance claimed.");
        File.WriteAllText(Output+"/runtime-proof.txt",log.ToString());Debug.Log(log.ToString());
    }
}
#endif
