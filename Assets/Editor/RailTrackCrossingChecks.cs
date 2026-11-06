#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.AI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RailTrackCrossingChecks
{
    static readonly List<string> evidence = new List<string>();
    static void Check(bool ok, string message)
    {
        evidence.Add((ok ? "PASS " : "FAIL ") + message);
        if (!ok) throw new Exception(message);
    }
    [MenuItem("Duck Rails/Tests/Track Crossings/Graph And Train Avoidance")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first");
        evidence.Clear();
        GameObject trainRoot = null;
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks" }))
            {
                var block = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)).GetComponent<RailBlock>();
                if (block == null) continue;
                var graph = CrawlGraph.Load(block.graph);
                Check(block.enemySurfaceRailExclusion.size == Vector3.zero, block.id + " no permanent railway boundary");
                Check(!block.GetComponentsInChildren<CrawlGraphBlocker>(true).Any(x => x.name.ToLowerInvariant().Contains("railway navigation exclusion")),
                    block.id + " no railway bake blocker");
                // Ground-edge flood, not surface-component IDs (those also connect walls).
                var markers = block.variations.SelectMany(v => v.enemies).Distinct().ToArray();
                var west = markers.Where(p => RailEnemySpawns.Side(p) < 0).ToArray();
                var east = markers.Where(p => RailEnemySpawns.Side(p) > 0).ToArray();
                var goals = new HashSet<int>(east.Select(p => graph.FindNearestGroundNode(p.position, 2)));
                var visited = new HashSet<int>();
                var queue = new Queue<int>();
                foreach (var p in west)
                {
                    int n = graph.FindNearestGroundNode(p.position, 2);
                    if (n >= 0 && visited.Add(n)) queue.Enqueue(n);
                }
                while (queue.Count > 0)
                {
                    int n = queue.Dequeue();
                    for (int e = graph.EdgeStart[n]; e < graph.EdgeStart[n + 1]; e++)
                    {
                        int next = graph.EdgeTarget[e];
                        if (graph.IsGroundNode(next) && visited.Add(next)) queue.Enqueue(next);
                    }
                }
                Check(goals.Any(n => n >= 0 && visited.Contains(n)), block.id + " walkable connection between rail sides");
            }
            var motel = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/roadside-motel-v1.prefab").GetComponent<RailBlock>();
            var g = CrawlGraph.Load(motel.graph);
            var path = new int[4096]; var blocked = new HashSet<int>();
            var finder = new CrawlPathfinder(g, 100000);
            for (int z = 50; z <= 110; z += 20)
            {
                int a = g.FindNearestGroundNode(new Vector3(-6, 0, z), 2), b = g.FindNearestGroundNode(new Vector3(6, 0, z), 2);
                Check(a >= 0 && b >= 0 && finder.FindPath(a, b, path, out _, out bool reached, true) && reached,
                    "Motel ground crossing at z=" + z);
            }
            trainRoot = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RailEnemyBoardingAuthoring.TrainPath));
            var train = trainRoot.GetComponent<RailTrain>(); trainRoot.transform.position = new Vector3(0, 1, 70);
            // OnEnable is not called for non-ExecuteAlways components in Edit Mode.
            typeof(RailTrain).GetMethod("OnEnable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(train, null);
            int start = g.FindNearestGroundNode(new Vector3(-6, 0, 70), 2), goal = g.FindNearestGroundNode(new Vector3(6, 0, 70), 2);
            foreach (float yaw in new[] { 0f, 25f, -25f })
            {
                trainRoot.transform.rotation = Quaternion.Euler(0, yaw, 0);
                blocked.Clear(); RailTrainObstacle.AddBlockedNodes(g, blocked);
                Check(blocked.Count > 0, "Occupied nodes at yaw " + yaw);
                Check(finder.FindPath(start, goal, path, out int count, out bool reached, true, blocked) && reached,
                    "Motel detour reaches opposite side at yaw " + yaw);
                Check(Enumerable.Range(0, count).All(i => !blocked.Contains(path[i])), "Detour avoids train nodes " + yaw);
                for (int i = 1; i < count; i++)
                    Check(!RailTrainObstacle.BlocksSegment(g.GetPosition(path[i - 1]), g.GetPosition(path[i])), "Detour edge clear " + yaw + "/" + i);
            }
            trainRoot.transform.SetPositionAndRotation(new Vector3(0, 1, 70), Quaternion.identity);
            Check(RailTrainObstacle.BlocksSegment(new Vector3(-6, 0, 70), new Vector3(6, 0, 70)), "Direct chase cannot shortcut under train");
            Check(!RailTrainObstacle.BlocksSegment(new Vector3(-6, -4, 70), new Vector3(6, -4, 70)), "Underpass below train remains available");
            trainRoot.transform.position = new Vector3(0, 1, 100);
            blocked.Clear(); RailTrainObstacle.AddBlockedNodes(g, blocked);
            Check(!blocked.Contains(g.FindNearestGroundNode(new Vector3(0, 0, 70), 2)), "Moved train releases old crossing");
            Check(!RailTrainObstacle.BlocksSegment(new Vector3(-6, 0, 70), new Vector3(6, 0, 70)), "Old crossing usable immediately");
            Debug.Log("TRACK CROSSINGS: " + evidence.Count + " checks passed");
        }
        finally
        {
            if (trainRoot != null) {
                typeof(RailTrain).GetMethod("OnDisable", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(trainRoot.GetComponent<RailTrain>(), null);
                Object.DestroyImmediate(trainRoot);
            }
            Directory.CreateDirectory(RailTrackCrossingAuthoring.Evidence);
            File.WriteAllLines(RailTrackCrossingAuthoring.Evidence + "/graph-checks.txt", evidence);
        }
    }
}
#endif
