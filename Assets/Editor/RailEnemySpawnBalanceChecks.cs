using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class RailEnemySpawnBalanceChecks
{
    public static void Run()
    {
        int checks = 0;
        void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
        var blocks = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks" })
            .Select(id => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(id)).GetComponent<RailBlock>())
            .Where(b => b != null && b.allowedInNormalRuns).ToArray();
        foreach (var block in blocks)
            for (int crew = 1; crew <= 8; crew++)
                for (int seed = 0; seed < 100; seed++)
                {
                    var points = new List<Transform>(); var types = new List<int>();
                    RailEnemySpawnBalance.Append(block, seed, crew, points, types, false);
                    int quota = crew <= 2 ? 1 : crew <= 5 ? 2 : 3;
                    int expected = quota * 4;
                    Check(points.Count == expected, block.id + " feasible crew budget " + crew + "/" + seed);
                    Check(points.Distinct().Count() == points.Count, "Unique locations");
                    Check(points.SequenceEqual(RailEnemySpawnBalance.Select(block, seed, crew)), "Deterministic reconstruction");
                    for (int q = 0; q < 4; q++)
                    {
                        var indices = Enumerable.Range(0, points.Count).Where(i => RailEnemySpawnBalance.Quadrant(block, points[i]) == q).ToArray();
                        Check(indices.Length >= 1 && indices.Length <= quota, "Quadrant coverage and cap");
                        Check(indices.Select(i => types[i]).Distinct().Count() == indices.Length, "No repeated type in quadrant");
                    }
                    foreach (var a in points) foreach (var b in points.Where(p => p != a))
                    { var delta = a.position - b.position; delta.y = 0; Check(delta.magnitude >= 14.999f, "Cross-quadrant spacing"); }
                    foreach (int type in Enumerable.Range(0, RailEnemySpawns.Names.Length))
                    {
                        // Five types spread over 4*quota spawns: totals differ by at most one.
                        int total = types.Count(t => t == type), even = points.Count / RailEnemySpawns.Names.Length;
                        Check(total <= quota && total >= even && total <= even + 1, "Distributed type totals");
                    }
                }
        var root = new GameObject("Temporary enemy pool test");
        try
        {
            var block = root.AddComponent<RailBlock>(); block.id = "shortfall-check"; block.length = 160;
            var a = new GameObject("A").transform; a.SetParent(root.transform); a.localPosition = new Vector3(-1, 0, 79);
            var b = new GameObject("B").transform; b.SetParent(root.transform); b.localPosition = new Vector3(1, 8, 81);
            block.spawnCandidatesMigrated = true; block.spawnCandidates.enemies = new[] { a, b };
            Check(RailEnemySpawnBalance.Select(block, 1, 8).Length == 1, "Shortfall does not force cross-boundary/floor clustering");
            block.enemyCount = 0;
            Check(RailEnemySpawnBalance.Select(block, 1, 8).Length == 0, "Explicit disabled encounter stays empty");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Directory.CreateDirectory("docs/validation/enemy-spawn-balance");
        File.WriteAllText("docs/validation/enemy-spawn-balance/checks.txt", checks + " assertions passed across four enabled blocks, eight crew sizes and 100 seeds; spacing/type/shortfall checks. Editor-only, not headset/multiplayer acceptance.\n");
        Debug.Log("ENEMY SPAWN BALANCE: " + checks + " checks passed");
    }
}
