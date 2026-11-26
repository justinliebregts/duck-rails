#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.AI;
/// <summary>V3 Passenger Station checks and review captures. Run with the station bake scene open.</summary>
public static class RailMetroChecks
{
    const string Shots = "output/passenger-station-v3";
    [MenuItem("Duck Rails/Authoring/Abandoned Station/Capture review")]
    public static void Capture() => Capture(false);
    public static void Capture(bool bright)
    {
        var go = new GameObject("Metro review camera"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = .05f; cam.farClipPlane = 200; cam.fieldOfView = 78; cam.useOcclusionCulling = false;
        GameObject fill = null;
        if (bright) { fill = new GameObject("Temporary review fill"); var l = fill.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.2f; l.shadows = LightShadows.None; fill.transform.rotation = Quaternion.Euler(50, 30, 0); }
        Directory.CreateDirectory(Shots);
        string suffix = bright ? "-bright" : "";
        try
        {
            Shot("platform-1" + suffix, new(-17.5f, -1.4f, 58), new(-18, -1.6f, 98));
            Shot("tunnel-approach" + suffix, new(-10.4f, -1.2f, 38), new(-12, -2.2f, 75));
            Shot("platform-1-edge" + suffix, new(-14.9f, -1.5f, 70), new(-12.3f, -2.6f, 92));
            Shot("entrance-portal" + suffix, new(0, 1.8f, -4), new(0, 0.5f, 20));
            Shot("platform-1-escalators" + suffix, new(-20.5f, -1.4f, 82), new(-21, -1f, 66));
            Shot("ticket-concourse" + suffix, new(-25, 4.6f, 51), new(-2, 4.1f, 42));
            Shot("escalator-down-to-platform-3" + suffix, new(-2.4f, 4.6f, 51.5f), new(-2.4f, -1.5f, 69));
            Shot("platform-3" + suffix, new(1, -1.4f, 56), new(2.5f, -1.8f, 100));
            Shot("wreck-interior" + suffix, new(6, -1.5f, 59.5f), new(6, -1.7f, 96));
            Shot("cave-in" + suffix, new(.6f, -1.5f, 88), new(5, -.5f, 103));
            Shot("transfer-hall" + suffix, new(-39, -1.4f, 30), new(-39, -1.6f, 80));
            Shot("konbini" + suffix, new(-26, -1.4f, 86), new(-33, -1.8f, 99));
            Shot("maintenance-hall" + suffix, new(15, -1.4f, 44), new(15, -1.6f, 90));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); if (fill) UnityEngine.Object.DestroyImmediate(fill); }
        void Shot(string name, Vector3 eye, Vector3 target)
        {
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            var rt = new RenderTexture(1440, 900, 24); var tex = new Texture2D(1440, 900, TextureFormat.RGB24, false); var prior = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); tex.Apply(); File.WriteAllBytes(Shots + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { cam.targetTexture = null; RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); }
        }
    }

    [MenuItem("Duck Rails/Authoring/Abandoned Station/Validate geometry")]
    public static void Run() => Run(true);
    public static void Run(bool requireGraph)
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying || Lightmapping.isRunning) throw new Exception("Wrong project/mode");
        var b = UnityEngine.Object.FindFirstObjectByType<RailBlock>(); Physics.SyncTransforms(); var lines = new List<string>();
        const int env = 1 << 6;
        void Check(bool ok, string label) => lines.Add((ok ? "PASS " : "FAIL ") + label);
        void Walk(Vector3 p, string label)
        {
            Check(Physics.Raycast(p + Vector3.up * .1f, Vector3.down, out var h, .25f, env) && Mathf.Abs(h.point.y - p.y) < .05f, "floor " + label);
            var hits = Physics.OverlapCapsule(p + Vector3.up * .35f, p + Vector3.up * 1.75f, .3f, env);
            Check(hits.Length == 0, "body " + label + " " + string.Join(",", hits.Select(c => c.name).Distinct()));
        }
        void Line(Vector3 a, Vector3 c, float step, string label)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((c - a).magnitude / step));
            for (int i = 0; i <= n; i++) Walk(Vector3.Lerp(a, c, i / (float)n), label + " " + i);
        }
        Check(b.id == RailAbandonedStation.Id && b.length == 160, "block identity and footprint");
        Check(b.transform.Find("Env/" + RailAbandonedStation.RootName) != null, "V3 root present");
        for (float d = 0; d < b.TrackLength; d += .5f)
        {
            b.Sample(d, out var p, out var r);
            // Actual DuckRailsTrain (Nov 26): origin at rail + 1, deck top -0.03, hull below deck +/-2.09,
            // body/stack above deck +/-2.28, top 2.56, length -5.0..6.0. Boxes add a few cm.
            var o = p + Vector3.up;
            var hull = Physics.OverlapBox(o + r * new Vector3(0, -.35f, .5f), new Vector3(2.13f, .3f, 5.55f), r, env);
            var body = Physics.OverlapBox(o + r * new Vector3(0, 1.31f, .5f), new Vector3(2.33f, 1.29f, 5.55f), r, env);
            var head = Physics.OverlapBox(o + r * new Vector3(0, 3.55f, .5f), new Vector3(2.05f, .95f, 5.55f), r, env);
            var hits = hull.Concat(body).Concat(head).ToArray();
            Check(hits.Length == 0, "train sweep " + d + " " + string.Join(",", hits.Select(c => c.name).Distinct()));
        }
        const float F = -3, D = 3f;
        Line(new(-16.4f, F, 53.5f), new(-16.4f, F, 106.5f), 1, "platform 1 track side");
        Line(new(-24.1f, F, 54), new(-24.1f, F, 106), 1, "platform 1 shop side");
        Line(new(-7f, F, 53.5f), new(-7f, F, 106.5f), 1, "service ledge");
        Line(new(1f, F, 46.6f), new(1f, F, 113.4f), 1, "platform 3");
        Line(new(-26.5f, D, 46.45f), new(-3.6f, D, 46.45f), .5f, "ticket concourse across gates");
        Line(new(-26.5f, D, 116.45f), new(-14, D, 116.45f), .5f, "shuttered concourse west");
        Line(new(-10.5f, D, 116.5f), new(3f, D, 116.5f), .5f, "shuttered concourse east");
        Line(new(-39f, F, 19f), new(-39f, F, 145f), 1, "transfer hall");
        Line(new(15, F, 41), new(15, F, 127), 1, "maintenance hall");
        Line(new(10.5f, F, 13), new(10.5f, F, 43), 1, "fan gallery");
        Line(new(6, F, 59), new(6, F, 95f), .5f, "wreck aisle");
        Line(new(.5f, F, 48), new(12.5f, F, 48), .5f, "south cross passage");
        Line(new(.5f, F, 112), new(12.5f, F, 112), .5f, "north cross passage");
        Line(new(2, F, 82.75f), new(14, F, 82.75f), .5f, "wreck side door to hall");
        Line(new(-6, F, 80.25f), new(-3, F, 80.25f), .5f, "ledge door to platform 3");
        foreach (float z in new[] { 58f, 69f, 79f, 93f, 105f }) Line(new(-26.5f, F, z), new(-23.9f, F, z), .5f, "shop door " + z);
        foreach (var (x, z) in new[] { (-36f, 25.5f), (-36f, 44.5f), (-36f, 100f), (-36f, 107f), (-36f, 137.5f), (-30f, 137.5f) }) Line(new(x - 1.2f, F, z), new(x + 1.2f, F, z), .4f, "west door " + x + "," + z);
        foreach (var (x, z) in new[] { (17f, 62f), (17f, 76f), (17f, 90f), (17f, 104f), (29f, 76f), (13f, 29.5f) }) Line(new(x - 1.2f, F, z), new(x + 1.2f, F, z), .4f, "east door " + x + "," + z);
        Line(new(15, F, 126.5f), new(15, F, 130), .4f, "pump room door");
        foreach (var t in b.GetComponentsInChildren<Transform>().Where(t => t.name == "Stopped escalator step" || t.name == "Stair tread"))
        { var c = t.GetComponent<BoxCollider>(); Walk(new(c.bounds.center.x, c.bounds.max.y, c.bounds.center.z), "tread " + t.position.ToString("F1")); }
        foreach (var t in b.spawnCandidates.coal) { Check(Physics.Raycast(t.position, Vector3.down, out var h, .4f, env) && Mathf.Abs(t.position.y - h.point.y - .2f) < .07f, "coal support " + t.name); }
        foreach (var t in b.spawnCandidates.gold) { Check(Physics.Raycast(t.position + Vector3.up * .1f, Vector3.down, out var h, .4f, env) && Mathf.Abs(t.position.y - h.point.y - .05f) < .07f, "gold support " + t.name); }
        Check(Physics.Linecast(new(0, 1.5f, 7), new(0, 1.5f, 153), env), "Entrance-exit sightline blocked by solid geometry");
        int coalNeed = RailResourceBalance.CoalCount(8), goldNeed = RailResourceBalance.GoldCount(8);
        Check(RailSpawnPool.Available(b, RailSpawnKind.Coal).Length >= coalNeed, "coal capacity " + RailSpawnPool.Available(b, RailSpawnKind.Coal).Length + " >= " + coalNeed);
        Check(RailSpawnPool.Available(b, RailSpawnKind.Gold).Length >= goldNeed, "gold capacity " + RailSpawnPool.Available(b, RailSpawnKind.Gold).Length + " >= " + goldNeed);
        for (int crew = 1; crew <= 8; crew++)
        {
            bool ok = true;
            for (int seed = 0; seed < 100 && ok; seed++)
                foreach (var kind in new[] { RailSpawnKind.Coal, RailSpawnKind.Gold })
                {
                    var pick = RailRoute.SelectResources(b, kind, crew, seed);
                    int want = kind == RailSpawnKind.Coal ? RailResourceBalance.CoalCount(crew) : RailResourceBalance.GoldCount(crew);
                    if (pick.Length != want || pick.Distinct().Count() != pick.Length) ok = false;
                }
            Check(ok, "SelectResources crew " + crew + " x100 seeds exact and unique");
        }
        if (requireGraph && b.graph)
        {
            var graph = CrawlGraph.Load(b.graph);
            var probes = new[] { new Vector3(-16.6f, F, 80), new(-7, F, 80), new(1, F, 80), new(-20, D, 46.5f), new(-20, D, 114), new(-39, F, 80), new(15, F, 80), new(6, F, 66), new(20, F, 138), new(10.5f, F, 20) };
            var nodes = probes.Select(q => graph.FindNearestGroundNode(q, 1)).ToArray();
            for (int i = 0; i < probes.Length; i++) Check(nodes[i] >= 0, "graph node near " + probes[i]);
            if (nodes.All(n => n >= 0))
            {
                var seen = new HashSet<int> { nodes[0] }; var todo = new Queue<int>(); todo.Enqueue(nodes[0]);
                while (todo.Count > 0) { int n = todo.Dequeue(); for (int e = graph.EdgeStart[n]; e < graph.EdgeStart[n + 1]; e++) { int k = graph.EdgeTarget[e]; if (graph.GetNormal(k).y > .5f && seen.Add(k)) todo.Enqueue(k); } }
                for (int i = 1; i < probes.Length; i++) Check(seen.Contains(nodes[i]), "ground graph connects platform 1 to " + probes[i]);
            }
            for (int crew = 1; crew <= 8; crew++)
            {
                bool ok = true;
                for (int seed = 0; seed < 100 && ok; seed++) ok = RailEnemySpawnBalance.Select(b, seed, crew).Length == 4 * RailEnemySpawnBalance.PerQuadrant(crew);
                Check(ok, "enemy quadrant quota crew " + crew + " x100 seeds");
            }
        }
        Directory.CreateDirectory(RailAbandonedStation.EvidenceFolder); File.WriteAllLines(RailAbandonedStation.EvidenceFolder + "/checks.txt", lines);
        Debug.Log("METRO V3: " + lines.Count + " checks, " + lines.Count(s => s.StartsWith("FAIL")) + " failures");
        if (lines.Any(s => s.StartsWith("FAIL"))) throw new Exception(string.Join("\n", lines.Where(s => s.StartsWith("FAIL")).Take(25)));
    }
}
#endif
