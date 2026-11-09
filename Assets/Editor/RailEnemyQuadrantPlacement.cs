using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Requested additive pass for Convenience Store and Skyscraper only.</summary>
public static class RailEnemyQuadrantPlacement
{
    static readonly string[] Ids = { "convenience-store-v1", "skyscraper-works-v1" };
    const string Prefix = "Enemy quadrant addition ";
    public static void Run(bool apply)
    {
        if (Application.dataPath != "X:/Work/duck-rails/Assets" || Application.isPlaying) throw new Exception("Wrong editor state");
        var report = new List<string>();
        foreach (string id in Ids)
        {
            string path = "Assets/DuckRails/Blocks/" + id + ".prefab";
            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            bool useStage = stage != null && stage.assetPath == path;
            var root = useStage ? stage.prefabContentsRoot : PrefabUtility.LoadPrefabContents(path);
            var block = root.GetComponent<RailBlock>();
            var created = new List<GameObject>();
            var original = block.spawnCandidates.enemies;
            var originalCoal = block.spawnCandidates.coal.ToArray(); var originalGold = block.spawnCandidates.gold.ToArray();
            var oldPoses = original.Select(p => (p, p.localPosition, p.localRotation, p.localScale)).ToArray();
            var environment = root.transform.Find("Env").GetComponentsInChildren<Transform>(true)
                .Select(t => (t, t.localPosition, t.localRotation, t.localScale)).ToArray();
            var graphAsset = block.graph;
            try
            {
                if (original.Any(p => p.name.StartsWith(Prefix))) throw new Exception("Pass already applied: " + id);
                var graph = CrawlGraph.Load(block.graph);
                var physics = root.scene.GetPhysicsScene(); Physics.SyncTransforms();
                var hits = new Collider[32];
                var additions = new List<Vector3>();
                var parent = original[0].parent;
                // Meaningful outer-yard/approach/departure coverage; retain tower/interior candidates.
                foreach (float z in new[] { 22f, 44f, 120f, 142f }) foreach (float x in new[] { -26f, 26f })
                {
                    var target = new Vector3(x, 0, z); Vector3? chosen = null; string support = "";
                    foreach (var node in Enumerable.Range(0, graph.NodeCount).Where(graph.IsGroundNode)
                        .Select(n => graph.GetPosition(n)).Where(p => Math.Abs(p.y) < 2 &&
                            (p.x < 0) == (x < 0) && (p.z < 80) == (z < 80) &&
                            Vector2.Distance(new Vector2(p.x,p.z), new Vector2(x,z)) <= 12)
                        .OrderBy(p => (p - target).sqrMagnitude))
                    {
                        var world = root.transform.TransformPoint(node);
                        if (!physics.Raycast(world + Vector3.up * .6f, Vector3.down, out var hit, 1.3f, 1 << 6, QueryTriggerInteraction.Ignore) || hit.normal.y < .95f) continue;
                        var p = hit.point + Vector3.up * .05f;
                        var local = root.transform.InverseTransformPoint(p);
                        if (!block.enemyBounds.Contains(local) || graph.FindNearestGroundNode(local, .8f) < 0) continue;
                        if (original.Any(t => Vector3.Distance(t.position,p) < 4) || additions.Any(a => { var d=a-p; d.y=0; return d.magnitude<18; })) continue;
                        if (physics.OverlapBox(p + Vector3.up * .9f, new Vector3(.45f,.75f,.45f), hits, Quaternion.identity, 1<<6, QueryTriggerInteraction.Ignore) != 0) continue;
                        bool corners = true;
                        foreach(float dx in new[]{-.35f,.35f}) foreach(float dz in new[]{-.35f,.35f})
                            if (!physics.Raycast(p+new Vector3(dx,.15f,dz),Vector3.down,out var corner,.35f,1<<6,QueryTriggerInteraction.Ignore) || Mathf.Abs(corner.point.y-hit.point.y)>.1f) corners=false;
                        if(!corners)continue;
                        if(physics.OverlapBox(hit.point-Vector3.up*.05f,Vector3.one*.1f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0)throw new Exception("Overlap positive control failed");
                        chosen=p; support=hit.collider.name; break;
                    }
                    if (!chosen.HasValue) throw new Exception("No supported clear candidate near " + id + " " + target);
                    additions.Add(chosen.Value);
                    var go = RailSpawnerPrefabs.Instantiate(RailSpawnKind.Enemy, root.scene);
                    if (apply) Undo.RegisterCreatedObjectUndo(go, "Add quadrant enemy marker");
                    go.transform.SetParent(parent, true); go.transform.position = chosen.Value;
                    go.name = Prefix + additions.Count.ToString("D2") + " " + (x<0?"west":"east") + " " + (z<80?"approach":"departure") + " " + support;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(go);
                    created.Add(go);
                    report.Add(id + " " + go.name + " at " + root.transform.InverseTransformPoint(chosen.Value).ToString("F2"));
                }
                if(apply)Undo.RecordObject(block,"Register quadrant enemy markers");
                block.spawnCandidates.enemies = original.Concat(created.Select(go=>go.transform)).ToArray();
                for(int crew=1;crew<=8;crew++) for(int seed=0;seed<100;seed++)
                    if(RailEnemySpawnBalance.Select(block,seed,crew).Length!=4*RailEnemySpawnBalance.PerQuadrant(crew))
                        throw new Exception("Insufficient spaced coverage: "+id+" crew="+crew+" seed="+seed);
                if(block.graph!=graphAsset || !block.spawnCandidates.coal.SequenceEqual(originalCoal) || !block.spawnCandidates.gold.SequenceEqual(originalGold))throw new Exception("Unrelated references changed");
                foreach(var (t,p,r,s) in environment.Concat(oldPoses))
                    if(t.localPosition!=p||t.localRotation!=r||t.localScale!=s)throw new Exception("Existing transform changed");
                report.Add(id+": "+original.Length+" -> "+block.spawnCandidates.enemies.Length+" enemies; all 800 crew/seed selections reach quota; support, clearance, bounds, graph and preservation checks passed.");
                if(apply)
                {
                    EditorUtility.SetDirty(block);
                    PrefabUtility.SaveAsPrefabAsset(root,path,out bool saved);
                    if(!saved)throw new Exception("Save failed");
                }
            }
            finally
            {
                if(!apply) { block.spawnCandidates.enemies=original; foreach(var go in created)Object.DestroyImmediate(go); }
                if(!useStage)PrefabUtility.UnloadPrefabContents(root);
            }
        }
        Directory.CreateDirectory("docs/validation/enemy-spawn-balance");
        File.WriteAllLines("docs/validation/enemy-spawn-balance/"+(apply?"placement":"placement-preview")+".txt",report);
        Debug.Log(string.Join("\n",report));
    }
}
