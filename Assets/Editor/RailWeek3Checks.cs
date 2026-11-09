#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using UnityEditor;
using UnityEngine;

public static class RailWeek3Checks
{
    static readonly List<string> lines = new List<string>();
    static void Check(bool value,string name){if(!value)throw new Exception(name);lines.Add("PASS "+name);}
    [MenuItem("Duck Rails/Legacy/Run Week 3 Checks")]
    public static void Run()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new InvalidOperationException("Stop Play first");
        lines.Clear();
        var blocks=AssetDatabase.FindAssets("t:Prefab",new[]{RailWeek3Builder.Folder}).Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<RailBlock>()).Where(b=>b!=null&&b.id!="bridge-finale-v1").ToArray();
        Check(blocks.Length==16&&blocks.Select(b=>b.id).Distinct().Count()==16,"Sixteen distinct encounter assets");
        var reference=CrawlGraph.Load(blocks[0].graph);
        foreach(var b in blocks)
        {
            Check(b.transform.localScale==Vector3.one&&b.entry.localPosition==Vector3.zero&&b.exit.localPosition==Vector3.forward*b.length,b.id+" consistent entry/exit/pivot");
            Check(b.track.Length>2&&b.track[0]==Vector3.zero&&Vector3.Distance(b.track.Last(),Vector3.forward*b.length)<.001f&&b.TrackLength>=b.length,b.id+" complete track");
            Check(b.spawnCandidatesMigrated && b.variations.Length==0,b.id+" migrated authored spawn pool");
            foreach(RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
            {
                var points=RailSpawnPool.Candidates(b,kind);
                Check(points.Length>0&&points.Distinct().Count()==points.Length,b.id+" unique nonempty "+kind+" candidates");
                Check(points.All(p=>p!=null&&p.IsChildOf(b.transform)),b.id+" local candidate references "+kind);
                Check(points.All(p=>kind==RailSpawnKind.Coal?p.GetComponent<CoalSpawner>()!=null:kind==RailSpawnKind.Gold?p.GetComponent<RailGoldSpawner>()!=null:p.GetComponent<RailEnemySpawner>()!=null),b.id+" typed spawn markers "+kind);
            }
            var g=CrawlGraph.Load(b.graph);
            Check(g!=null&&g.NodeCount>100&&g.CellSize==1,b.id+" prebaked navigation");
            Vector3 offset=(g.Origin-reference.Origin)/(g.CellSize*g.ChunkCells);
            Check(g.ChunkCells==reference.ChunkCells&&(offset-(Vector3)Vector3Int.RoundToInt(offset)).sqrMagnitude<.000001f,b.id+" compatible graph chunks");
            foreach(var point in RailSpawnPool.Candidates(b,RailSpawnKind.Enemy))
                Check(g.FindNearestGroundNode(point.position,2)>=0&&b.enemyBounds.Contains(point.position),b.id+"/"+point.name+" enemy navigation and bounds");
            b.Sample(0,out var entry,out var entryRotation);b.Sample(b.TrackLength,out var exit,out var exitRotation);
            Check(Vector3.Distance(entry,b.entry.position)<.001f&&Vector3.Distance(exit,b.exit.position)<.001f&&Quaternion.Angle(entryRotation,b.entry.rotation)<.01f&&Quaternion.Angle(exitRotation,b.exit.rotation)<.01f,b.id+" sampled seams");
        }
        for(int seed=0;seed<50;seed++)
        {
            var route=RailRoute.ChooseNormal("run",blocks,seed);var copy=JsonUtility.FromJson<RailRouteState>(JsonUtility.ToJson(route));
            Check(route.blocks.Distinct().Count()==4&&route.blocks.All(id=>blocks.Single(b=>b.id==id).allowedInNormalRuns)&&route.blocks.SequenceEqual(copy.blocks)&&route.placements.SequenceEqual(copy.placements),"Normal route "+seed+" four enabled distinct blocks and exact serialization");
        }
        var chosen=RailRoute.ChooseNormal("assembly",blocks,12345).blocks.Select(id=>blocks.Single(b=>b.id==id)).ToArray();
        var graphs=chosen.Select(b=>CrawlGraph.Load(b.graph)).ToArray();
        var positions=Enumerable.Range(0,chosen.Length).Select(i=>Vector3.forward*(RailRoute.StartZ+chosen.Take(i).Sum(b=>b.length))).ToArray();
        var combined=RailGraphAssembly.Combine(graphs,positions);
        Check(combined.NodeCount==graphs.Sum(g=>g.NodeCount)&&combined.EdgeCount==graphs.Sum(g=>g.EdgeCount),"Graph assembly preserves prebaked nodes/edges");
        for(int n=0;n<combined.NodeCount;n++)
        {
            combined.CoordsOf(combined.NodeKey[n],out int x,out int y,out int z);
            int found=combined.FindNodeAtCell(x,y,z);
            // Adjacent full-area bakes may share seam cells while retaining separate components.
            if(found<0 || combined.NodeKey[found]!=combined.NodeKey[n])throw new Exception("Invalid combined cell "+n);
            for(int e=combined.EdgeStart[n];e<combined.EdgeStart[n+1];e++)if(combined.GetComponent(n)!=combined.GetComponent(combined.EdgeTarget[e]))throw new Exception("Unexpected inter-block edge");
        }
        Check(true,"Every combined cell indexed; block components remain disconnected");
        for(int i=0;i<chosen.Length;i++)Check(combined.FindNearestGroundNode(RailSpawnPool.Candidates(chosen[i],RailSpawnKind.Enemy)[0].position+positions[i],2)>=0,chosen[i].id+" translated enemy marker resolves");
        Check(EditorBuildSettings.scenes.Where(s=>s.enabled).Take(3).Select(s=>Path.GetFileNameWithoutExtension(s.path)).SequenceEqual(new[]{"Login","Compound","Expedition"}),"Gameplay scene build order");
        Check(EditorBuildSettings.scenes.Any(s=>s.enabled&&s.path==RailStationLightingAuthoring.PayloadScene),"Starting station lighting included");
        RailSessionRules.EvaluateTrain(new object[]{0d,0f,4f,4f},RailResourceBalance.CoalValue(1),40,out var distance,out var speed,out var fuel);
        Check(Mathf.Abs(distance-44)<.001f&&speed==0&&fuel==0,"Solo coal powers 40 metres plus coast");
        // Current dedicated suites retain actual prefab assembly, starting station,
        // spawn placement, seeded sampling and support/clearance coverage.
        RailTestBlockOrderChecks.Run();Check(true,"Current route/station prefab assembly suite");
        RailLootPlacementChecks.Run();Check(true,"Current spawn-pool/placement suite");
        Directory.CreateDirectory("Temp");File.WriteAllLines("Temp/rails-week3-checks.txt",lines);Debug.Log("WEEK 3 CHECKS: "+lines.Count+" passed");
    }
}
#endif
