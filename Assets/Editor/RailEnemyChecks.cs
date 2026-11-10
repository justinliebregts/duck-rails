#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.AI;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

public static class RailEnemyChecks
{
    [MenuItem("Duck Rails/Legacy/Run Mixed Enemy Checks")]
    public static void Run()
    {
        var log = new List<string>();
        void Check(bool ok, string message) { if (!ok) throw new Exception(message); log.Add("PASS " + message); }
        for (int i = 0; i < RailEnemySpawns.Names.Length; i++)
        {
            var prefab = RailEnemySpawns.Prefab(i);
            Check(prefab != null && prefab.GetComponent<Enemy>() != null && prefab.GetComponent<EnemyNetSync>() != null && prefab.GetComponent<PhotonView>() != null && prefab.GetComponent<RailEnemyBounds>() != null && prefab.GetComponent<RailCoalCarrier>()?.display != null, RailEnemySpawns.Names[i] + " network, bounds and coal carrier");
        }
        Check(RailEnemySpawns.Prefab(1).GetComponent<ZombieBomberExplode>() != null, "Bomber explosion retained");
        Check(RailEnemySpawns.Prefab(2).GetComponent<ZombieSmasherJump>() != null, "Smasher jump retained");
        Check(RailEnemySpawns.Prefab(3).GetComponent<ZombieSpiderStalk>() != null, "Spider stalk retained");
        Check(RailEnemySpawns.Prefab(4).GetComponent<ZombieHookAttack>() != null && RailEnemySpawns.Prefab(4).GetComponent<RailEnemyBoarding>() == null, "Hook attack retained; hook does not board the train");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RailWeek3Builder.Folder }))
        {
            var b = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponent<RailBlock>();
            if (b == null) continue;
            var graph = CrawlGraph.Load(b.graph);
            foreach (var set in b.variations)
            {
                Check(set.enemies.Any(p => RailEnemySpawns.Side(p) < 0) && set.enemies.Any(p => RailEnemySpawns.Side(p) > 0), b.id + " candidates on both rail sides");
                Check(set.enemies.Length == 6 && set.enemies.All(p => b.enemyBounds.Contains(p.position) && graph.FindNearestGroundNode(p.position, 1) >= 0), b.id + " six bounded baked-navigation candidates");
                var signatures = new HashSet<string>(); var counts = new HashSet<int>();
                for (int seed = 0; seed < 100; seed++)
                {
                    var points = new List<Transform>(); var types = new List<int>();
                    var copyPoints = new List<Transform>(); var copyTypes = new List<int>();
                    var state = JsonUtility.FromJson<RailRouteState>(JsonUtility.ToJson(new RailRouteState { enemySeed = seed }));
                    RailEnemySpawns.Select(set.enemies, new System.Random(seed), points, types);
                    RailEnemySpawns.Select(set.enemies, new System.Random(state.enemySeed), copyPoints, copyTypes);
                    if (!points.Any(p => RailEnemySpawns.Side(p) < 0) || !points.Any(p => RailEnemySpawns.Side(p) > 0)) throw new Exception("One-sided encounter: " + b.id + " seed " + seed);
                    if (points.Count < 4 || points.Count > 6 || points.Distinct().Count() != points.Count || types.Distinct().Count() != 4 || !points.SequenceEqual(copyPoints) || !types.SequenceEqual(copyTypes)) throw new Exception("Invalid/re-rolled encounter: " + b.id);
                    counts.Add(points.Count); signatures.Add(string.Join(",", points.Select(p => p.name)) + string.Join(",", types));
                }
                Check(counts.Count == 3 && signatures.Count > 90, b.id + " 100 seeds: varied 4–6 unique spawns, all types, exact serialized replay");
            }
        }
        Directory.CreateDirectory("output/mixed-enemies"); File.WriteAllLines("output/mixed-enemies/editor-checks.txt", log);
        Debug.Log("MIXED ENEMY CHECKS: " + log.Count + " passed");
    }
}
#endif
