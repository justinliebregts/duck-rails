#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Scoped November 17 placement pass against the current fortress, never a geometry rebuild.</summary>
public static class RailFinaleRefresh
{
    const string Evidence = "output/bridge-finale-refresh";
    static RailBlock Block()
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying ||
            SceneManager.GetActiveScene().path != RailFinaleFort.BakeScene) throw new Exception("Open Duck Rails finale bake scene in Edit Mode");
        return Object.FindFirstObjectByType<RailBlock>();
    }
    // Surface-height hints keep rays below ceilings. Each region is an authored exploration destination.
    static readonly (string name, Vector3[] points)[] Regions = {
        ("West parade terrace", new[]{new Vector3(-16,2,38),new Vector3(-24,2,37),new Vector3(-30,2,44),new Vector3(-24,2,48)}),
        ("West bunker interior", new[]{new Vector3(-16,0,63),new Vector3(-26,0,67),new Vector3(-16,0,72),new Vector3(-30,0,61)}),
        ("West bunker roof", new[]{new Vector3(-16,4,56),new Vector3(-30,4,60),new Vector3(-16,4,61),new Vector3(-29,4,69)}),
        ("West tower and upper deck", new[]{new Vector3(-35,8,75),new Vector3(-32,8,72),new Vector3(-28,8,74),new Vector3(-22,8,69)}),
        ("East loading terrace", new[]{new Vector3(16,2,36),new Vector3(23,2,38),new Vector3(27,2,44),new Vector3(31,2,36)}),
        ("East corner bastion", new[]{new Vector3(37,4,33),new Vector3(43,4,37),new Vector3(42,4,45),new Vector3(36,4,49)}),
        ("East depot decks", new[]{new Vector3(21,4,52),new Vector3(29,4,59),new Vector3(33,4,73),new Vector3(26,4,74)}),
        ("East colonnade and tower", new[]{new Vector3(16,0,71),new Vector3(26,0,72),new Vector3(34,8,65),new Vector3(23,8,67.2f)}),
        ("Yard climb blocks", new[]{new Vector3(-9.5f,.6f,49),new Vector3(-10.5f,1.8f,58),new Vector3(9.5f,1.2f,51),new Vector3(10,1.8f,61)})
    };

    static bool Support(RailBlock b, Vector3 hint, Bounds envelope, out Vector3 p)
    {
        p = default;
        var physics = b.gameObject.scene.GetPhysicsScene();
        if (!physics.Raycast(hint + Vector3.up * .75f, Vector3.down, out var hit, 1.5f, 1 << 6, QueryTriggerInteraction.Ignore) ||
            !hit.transform.IsChildOf(b.transform) || hit.normal.y < .95f) return false;
        p = hit.point + Vector3.up * (.05f - envelope.min.y);
        var overlap = new Collider[16];
        if (physics.OverlapBox(p + envelope.center, envelope.extents, overlap, Quaternion.identity, 1 << 6, QueryTriggerInteraction.Ignore) != 0) return false;
        foreach (float x in new[]{-1f,1f}) foreach(float z in new[]{-1f,1f})
            if (!physics.Raycast(new Vector3(p.x+x*envelope.extents.x,hit.point.y+.2f,p.z+z*envelope.extents.z), Vector3.down,
                out var corner,.35f,1<<6,QueryTriggerInteraction.Ignore) || Mathf.Abs(corner.point.y-hit.point.y)>.08f) return false;
        return true;
    }
    public static void Place(bool apply)
    {
        var b=Block(); Physics.SyncTransforms();
        var report=new List<string>(); var planned=new List<(Transform marker,Vector3 p,string name)>();
        // Retain eight approach truck-bed candidates where they still have valid support, then spread through the new fort.
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var markers=RailSpawnPool.Candidates(b,kind); var envelope=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            int next=0;
            foreach(var t in markers.Where(t=>t.position.z<28).Take(8))
                if(Support(b,t.position+Vector3.up*envelope.min.y,envelope,out var p))
                { planned.Add((markers[next++],p,kind+" approach truck bed "+next)); }
            // Round robin destinations: the first few regions must not consume the whole pool.
            for(int pass=0;pass<4&&next<markers.Length;pass++) foreach(var region in Regions)
            {
                if(next>=markers.Length)break;
                var hint=region.points[(pass+(kind==RailSpawnKind.Gold?1:0))%region.points.Length];
                // Distinct coal/gold spots on the same destination, tested against actual geometry.
                if(kind==RailSpawnKind.Gold)hint.x+=.9f;
                if(!Support(b,hint,envelope,out var p))continue;
                if(planned.Any(q=>Vector3.Distance(q.p,p)<(q.name.StartsWith(kind.ToString())?1.8f:.8f)))continue;
                planned.Add((markers[next++],p,kind+" "+next.ToString("00")+" "+region.name));
            }
            if(next!=markers.Length)throw new Exception(kind+" only placed "+next+"/"+markers.Length);
        }
        foreach(var q in planned)report.Add(q.name+" "+q.p.ToString("F3"));
        Directory.CreateDirectory(Evidence); File.WriteAllLines(Evidence+"/placement-plan.txt",report);
        if(!apply){Debug.Log("Validated dry plan: "+planned.Count+" loot markers");return;}
        foreach(var q in planned)
        {
            Undo.RecordObject(q.marker,"Redistribute finale loot"); Undo.RecordObject(q.marker.gameObject,"Name finale loot");
            q.marker.position=q.p;q.marker.name=q.name;
            PrefabUtility.RecordPrefabInstancePropertyModifications(q.marker);
            PrefabUtility.RecordPrefabInstancePropertyModifications(q.marker.gameObject);
        }
        // An independent third defender: west upper deck, clear of the first tower defender and control.
        var waveHint=new Vector3(-22,8,74);
        if(!Support(b,waveHint,new Bounds(Vector3.up*.48f,new Vector3(.84f,.96f,.84f)),out var wave))throw new Exception("Invalid reinforcement support");
        if(b.bridgeReinforcements.Length==0)
        {
            var go=RailSpawnerPrefabs.Instantiate(RailSpawnKind.Enemy,b.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go,"Bridge midpoint reinforcement");
            go.name="Bridge midpoint reinforcement";go.transform.SetParent(b.transform,false);
            b.bridgeReinforcements=new[]{go.transform};
        }
        b.bridgeReinforcements[0].position=wave;
        PrefabUtility.RecordPrefabInstancePropertyModifications(b.bridgeReinforcements[0]);
        PrefabUtility.RecordPrefabInstancePropertyModifications(b);
        EditorUtility.SetDirty(b);EditorSceneManager.MarkSceneDirty(b.gameObject.scene);
        EditorSceneManager.SaveScene(b.gameObject.scene);
        PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);
        EditorSceneManager.SaveScene(b.gameObject.scene);
        Debug.Log("Installed "+planned.Count+" redistributed loot candidates and one separate midpoint marker");
    }

    public static void Check()
    {
        var b=Block();Physics.SyncTransforms();int count=0;
        var lines=new List<string>();
        void Check(bool value,string text){if(!value)throw new Exception(text);count++;}
        var graph=CrawlGraph.Load(b.graph);
        var solids=b.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();
        var solid=solids.OfType<BoxCollider>().First(c=>c.size.x>2&&c.size.z>2);
        Check(b.gameObject.scene.GetPhysicsScene().OverlapBox(solid.bounds.center,Vector3.one*.01f,new Collider[16],Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)>0,"Overlap query positive control");
        var button=RailBlock.FindEnvironment(b.transform,"Lower bridge button");
        var goal=button.position+Vector3.back*1.4f;
        int end=graph.FindNearestGroundNode(goal,1f);Check(end>=0,"Control approach ground node");
        var finder=new CrawlPathfinder(graph,graph.NodeCount){CorridorClusters=graph.ClusterCount,DirectSearchDistance=1000};
        var runtimeFinder=new CrawlPathfinder(graph);
        var path=new int[graph.NodeCount];
        foreach(var t in RailSpawnPool.Available(b,RailSpawnKind.Enemy).Concat(b.bridgeReinforcements))
        {
            int start=graph.FindNearestGroundNode(t.position,.8f);Check(start>=0,t.name+" nearby ground");
            Check(b.enemyBounds.Contains(t.position),t.name+" inside enemy bounds");
            Check(Support(b,t.position,new Bounds(Vector3.up*.48f,new Vector3(.84f,.96f,.84f)),out var supported),t.name+" body support and clearance");
            Check(finder.FindPath(start,end,path,out var n,out var reached,true)&&reached,t.name+" full ground path to controls");
            lines.Add(t.name+": complete ground path, "+n+" nodes");
            int current=start,segments=0;
            while(current!=end&&segments++<64)
            {
                Check(runtimeFinder.FindPath(current,end,path,out var length,out var complete,true)&&length>0,t.name+" runtime-budget path segment");
                int next=path[length-1];Check(next!=current||complete,t.name+" runtime-budget forward progress");current=next;
            }
            Check(current==end,t.name+" runtime-budget segmented route reaches controls");
            lines.Add(t.name+": runtime 4,000-expansion / 3-cluster route reaches controls in "+segments+" segments");
        }
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var envelope=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            foreach(var t in RailSpawnPool.Candidates(b,kind))
            {
                Check(Support(b,t.position+Vector3.up*envelope.min.y,envelope,out var p)&&Vector3.Distance(p,t.position)<.02f,t.name+" support and envelope clearance");
                Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,t.name+" nested marker reference");
            }
            for(int seed=0;seed<100;seed++)Check(RailSpawnPool.Select(b,kind,seed).Length==(kind==RailSpawnKind.Coal?6:2),kind+" selection "+seed);
        }
        var lighting=b.GetComponent<RailBakedBlockLighting>();
        foreach(var binding in lighting.bindings)Check(binding.renderer&&binding.color,"Valid baked renderer/texture reference");
        Check(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==lighting.lightingScene),"Lighting companion enabled in build");
        var so=new SerializedObject(lighting);Check(so.FindProperty("bakedFixtures").arraySize==b.GetComponentsInChildren<Light>(true).Count(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Build baked fixture capture complete");
        var state=new RailRunState{bridgeElapsed=29.9};state.UpdateBridgeReinforcements(100,2);Check(!state.bridgeReinforcementsTriggered,"No wave before thirty seconds");
        state.bridgeElapsed=30;state.UpdateBridgeReinforcements(100,2);Check(state.bridgeReinforcementCount==1&&state.BridgeProgress(100)==.5f,"One extra defender at half of sixty seconds");
        state=JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(state));state.UpdateBridgeReinforcements(900,20);Check(state.bridgeReinforcementCount==1,"Handoff/resume does not recalculate wave");
        Check(!new RailRunState().bridgeReinforcementsTriggered,"Replay resets wave");
        var paused=new RailRunState{bridgeElapsed=29};paused.UpdateBridgeReinforcements(10000,2);
        Check(!paused.bridgeReinforcementsTriggered,"Idle wall-clock time cannot trigger wave");
        paused.UpdateBridgeHold(10000,10001);paused.UpdateBridgeReinforcements(10001,2);
        Check(paused.bridgeReinforcementCount==1,"Accumulated hold crossing triggers wave");
        var ended=new RailRunState{bridgeElapsed=30,finished=true};ended.UpdateBridgeReinforcements(100,2);
        Check(!ended.bridgeReinforcementsTriggered,"Finished run cannot trigger wave");
        Check(RailBridgeEncounter.IsDefender("enemy-12",12,14)&&!RailBridgeEncounter.IsDefender("enemy-11",12,14)&&RailBridgeEncounter.IsDefender("bridge-wave-0",12,14),"Only finale defenders get bridge objective");
        lines.Add("PASS "+count+" checks; nodes="+graph.NodeCount+" bindings="+lighting.bindings.Length+"; runtime/headset acceptance pending.");
        File.WriteAllLines(Evidence+"/checks.txt",lines);Debug.Log(string.Join("\n",lines));
    }
}
#endif
