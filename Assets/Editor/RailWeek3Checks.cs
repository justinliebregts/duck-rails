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
    static void Check(bool value, string name) { if (!value) throw new Exception(name); lines.Add("PASS " + name); }
    [MenuItem("Duck Rails/Run Week 3 Checks")]
    public static void Run()
    {
        lines.Clear();
        var blocks = AssetDatabase.FindAssets("t:Prefab", new[] { RailWeek3Builder.Folder }).Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<RailBlock>()).Where(b => b != null && b.id != "bridge-finale-v1").ToArray();
        Check(blocks.Length == 12 && blocks.Select(b => b.id).Distinct().Count() == 12, "Twelve unique block assets");
        foreach (var b in blocks)
        {
            Check(b.transform.localScale == Vector3.one && b.entry.localPosition == Vector3.zero && b.exit.localPosition == new Vector3(0, 0, 160), b.id + " consistent entry/exit/pivot");
            Check(b.track.Length > 2 && b.track[0] == Vector3.zero && Vector3.Distance(b.track.Last(), Vector3.forward * 160) < .001f && b.TrackLength >= 160, b.id + " complete track");
            Check(b.variations.Length == 3 && b.variations.All(v => v.coal.Length == 6 && v.enemies.Length == 2), b.id + " three authored supply/enemy variations");
            var g = CrawlGraph.Load(b.graph);
            Check(g != null && g.NodeCount > 100 && g.CellSize == 1, b.id + " prebaked graph");
            foreach (var point in b.variations.SelectMany(v => v.enemies.Concat(v.coal)))
                Check(g.FindNearestGroundNode(point.position, 2) >= 0 && b.enemyBounds.Contains(point.position), b.id + "/" + point.parent.name + "/" + point.name + " usable navigation");
            Check(b.track.Skip(20).Take(40).All(p => p.x <= 4 && p.x >= -4), b.id + " rail stays outside enemy corridor");
            b.Sample(0, out var entryPosition, out var entryRotation); b.Sample(b.TrackLength, out var exitPosition, out var exitRotation);
            Check(Vector3.Distance(entryPosition, b.entry.position) < .001f && Vector3.Distance(exitPosition, b.exit.position) < .001f && Quaternion.Angle(entryRotation, b.entry.rotation) < .01f && Quaternion.Angle(exitRotation, b.exit.rotation) < .01f, b.id + " sampled track connects with exact forward tangents");
            Check((b.length - b.enemyBounds.size.z) / 4 >= 20, b.id + " at least twenty seconds between encounters");
        }
        for (int seed = 0; seed < 50; seed++)
        {
            var route = RailRoute.Choose("run", blocks, seed); var copy = JsonUtility.FromJson<RailRouteState>(JsonUtility.ToJson(route));
            Check(route.blocks.Distinct().Count() == 5 && route.blocks.SequenceEqual(copy.blocks) && route.placements.SequenceEqual(copy.placements) && route.variations.SequenceEqual(copy.variations), "Route " + seed + " five distinct blocks and exact late-join serialization");
        }
        var selected = blocks.Take(5).ToArray(); var graphs = selected.Select(b => CrawlGraph.Load(b.graph)).ToArray(); var offsets = Enumerable.Range(0, 5).Select(i => Vector3.forward * (70 + i * 160)).ToArray();
        var combined = RailGraphAssembly.Combine(graphs, offsets);
        Check(combined.NodeCount == graphs.Sum(g => g.NodeCount) && combined.EdgeCount == graphs.Sum(g => g.EdgeCount), "Graph assembly preserves baked nodes/edges without rebaking");
        for (int n = 0; n < combined.NodeCount; n++)
        {
            if (combined.FindNodeAtCell(Coords(combined, n, 0), Coords(combined, n, 1), Coords(combined, n, 2)) != n) throw new Exception("Invalid assembled cell lookup " + n);
            for (int e = combined.EdgeStart[n]; e < combined.EdgeStart[n + 1]; e++)
                if (combined.GetComponent(n) != combined.GetComponent(combined.EdgeTarget[e])) throw new Exception("Cross-block edge created");
        }
        Check(true, "All assembled cells and edges preserve indexing and disconnected components");
        foreach (var tuple in selected.Select((b, i) => (b, i)))
            Check(combined.FindNearestGroundNode(tuple.b.variations[0].enemies[0].position + offsets[tuple.i], 2) >= 0, tuple.b.id + " translated spawn resolves in assembled graph");
        Check(EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).SequenceEqual(new[] { "Login", "Compound", "Expedition" }), "Separate Expedition included in build");
        RailSessionRules.EvaluateTrain(new object[] { 0d, 0f, 4f, 4f }, 2.5f, 40, out var distance, out var speed, out var fuel);
        Check(Mathf.Abs(distance - 50) < .001f && speed == 0 && fuel == 0, "One coal chunk buys 50 metres and exhaustion stops deterministically");
        Directory.CreateDirectory("Temp"); File.WriteAllLines("Temp/rails-week3-checks.txt", lines);
        Debug.Log("WEEK 3 CHECKS: " + lines.Count + " passed (includes per-node indexing), evidence Temp/rails-week3-checks.txt");
    }
    static int Coords(CrawlGraph g, int n, int axis) { g.CoordsOf(g.NodeKey[n], out int x, out int y, out int z); return axis == 0 ? x : axis == 1 ? y : z; }
}
#endif
