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

/// <summary>Install, navigation prune and probe steps for the V3 Passenger Station. Explicit editor authoring only.</summary>
public static partial class RailAbandonedStation
{
    public const int CoalTarget = 120, GoldTarget = 64;
    public const string EvidenceFolder = "docs/validation/passenger-station-v3";

    static void GuardV3()
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new Exception("Use the Duck Rails editor in Edit Mode, outside a bake");
    }

    /// <summary>Step 1: rebuild station geometry and spawn candidates in the prefab (railway kept).</summary>
    [MenuItem("Duck Rails/Authoring/Abandoned Station/1 Install V3 Tokyo station")]
    public static void InstallV3()
    {
        GuardV3();
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty) throw new Exception("Save the open prefab first");
        StageUtility.GoToMainStage();
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new Exception("Save current scene first");
        Directory.CreateDirectory(Folder + "/Meshes"); AssetDatabase.Refresh(); meshes.Clear();
        cube = AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/" + Id + "/Cube Block UV.asset");
        if (!cube) throw new Exception("Missing cube UV");
        V3Materials();
        var report = new List<string>();
        var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var b = contents.GetComponent<RailBlock>();
            var env = b.transform.Find("Env") ?? throw new Exception("No Env");
            var old = env.Cast<Transform>().Where(t => t.name == RootName || t.name == "Abandoned underground station v2").ToArray();
            if (old.Length != 1) throw new Exception("Expected one existing station root, found " + old.Length);
            var trackBefore = b.track.ToArray();
            var root = Group(env, RootName);
            var railway = Group(root, "Railway (retained atlas rails)");
            foreach (var r in old[0].GetComponentsInChildren<Transform>(true).Where(t => t.name == "Rail bed" || t.name == "Visual rail" || t.name == "Visual sleeper").ToArray())
                r.SetParent(railway, true);
            Object.DestroyImmediate(old[0].gameObject);
            // Retained rail-bed segments overlap a little on the curves; alternate 3 mm heights stop them z-fighting.
            int bedIndex = 0;
            foreach (var bed in railway.Cast<Transform>().Where(t => t.name == "Rail bed").OrderBy(t => t.localPosition.z).ToArray())
            {
                var lp = bed.localPosition;
                float baseY = (RailHeight(lp.z - .5f) + RailHeight(lp.z + .5f)) / 2 - .15f;   // as the V2 builder placed it
                if (Mathf.Abs(lp.y - baseY) < .02f) { lp.y = baseY + (bedIndex % 2) * .003f; bed.localPosition = lp; }
                bedIndex++;
            }
            int rails = railway.GetComponentsInChildren<Transform>().Count(t => t.name == "Visual rail");
            if (rails < 300) throw new Exception("Retained railway missing: " + rails);
            lootSpots = new(); enemySpots = new(); v3Lights = 0;
            BuildV3(root, b);
            if (!b.track.SequenceEqual(trackBefore)) throw new Exception("Track changed");
            b.displayName = "Abandoned Underground Station"; b.tags = new[] { "interior", "underground", "graded" };
            var lighting = b.GetComponent<RailBakedBlockLighting>(); lighting.bindings = Array.Empty<RailBakedBlockLighting.Binding>();
            Physics.SyncTransforms();
            PlaceMarkers(contents, b, report);
            report.Insert(0, "Lights built: " + v3Lights + " (LightCount=" + LightCount + ")");
            if (v3Lights != LightCount) report.Insert(0, "WARNING update LightCount to " + v3Lights);
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out bool ok);
            if (!ok) throw new Exception("Prefab save failed");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
        AssetDatabase.SaveAssets();
        Directory.CreateDirectory(EvidenceFolder); File.WriteAllLines(EvidenceFolder + "/install.txt", report);
        Debug.Log("V3 station installed.\n" + string.Join("\n", report.Take(12)));
    }

    static void PlaceMarkers(GameObject contents, RailBlock b, List<string> report)
    {
        var ps = contents.scene.GetPhysicsScene(); var buf = new Collider[16];
        const int env = 1 << 6; var q = QueryTriggerInteraction.Ignore;
        // Positive control: the overlap query must see a known floor.
        if (ps.OverlapBox(new Vector3(-20, F - .1f, 80), Vector3.one * .05f, buf, Quaternion.identity, env, q) == 0) throw new Exception("Overlap positive control failed");
        bool Supported(Vector3 s, float r)
        {
            if (!ps.Raycast(s + Vector3.up * .6f, Vector3.down, out var h, 1f, env, q) || Mathf.Abs(h.point.y - s.y) > .06f || h.normal.y < .95f) return false;
            foreach (float dx in new[] { -r, r }) foreach (float dz in new[] { -r, r })
                if (!ps.Raycast(s + new Vector3(dx, .1f, dz), Vector3.down, out var c, .25f, env, q) || Mathf.Abs(c.point.y - s.y) > .06f) return false;
            return true;
        }
        bool LootOk(Vector3 s) => Supported(s, .18f) && ps.OverlapBox(s + Vector3.up * .3f, new Vector3(.22f, .22f, .22f), buf, Quaternion.identity, env, q) == 0;
        bool EnemyOk(Vector3 s) => Supported(s, .3f) && ps.OverlapCapsule(s + Vector3.up * .5f, s + Vector3.up * 1.6f, .38f, buf, env, q) == 0;

        var valid = lootSpots.Where(s => LootOk(s.p)).ToList();
        report.Add("Loot spots: generated " + lootSpots.Count + ", supported and clear " + valid.Count);
        var rng = new System.Random(11);
        for (int i = valid.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (valid[i], valid[j]) = (valid[j], valid[i]); }
        var gold = new List<Spot>(); var coal = new List<Spot>();
        bool Far(List<Spot> l, Vector3 p, float d) => l.All(o => (o.p - p).sqrMagnitude >= d * d);
        void RoundRobin(List<Spot> into, int target, Func<Spot, bool> ok)
        {
            var areas = valid.GroupBy(s => s.area).Select(g => new Queue<Spot>(g)).ToList();
            bool progress = true;
            while (into.Count < target && progress)
            {
                progress = false;
                foreach (var a in areas)
                {
                    while (a.Count > 0)
                    {
                        var s = a.Dequeue();
                        if (!ok(s) || gold.Contains(s) || coal.Contains(s)) continue;
                        into.Add(s); progress = true; break;
                    }
                    if (into.Count >= target) break;
                }
            }
        }
        foreach (float threshold in new[] { .8f, .5f, 0f })
            RoundRobin(gold, GoldTarget, s => s.gold >= threshold && Far(gold, s.p, 1.8f) && Far(coal, s.p, .8f));
        RoundRobin(coal, CoalTarget, s => Far(coal, s.p, 1.8f) && Far(gold, s.p, .8f));
        int coalNeed = RailResourceBalance.CoalCount(RailResourceBalance.MaxCrew), goldNeed = RailResourceBalance.GoldCount(RailResourceBalance.MaxCrew);
        report.Add("Selected " + coal.Count + " coal (need " + coalNeed + "), " + gold.Count + " gold (need " + goldNeed + ")");
        if (coal.Count < coalNeed || gold.Count < goldNeed) throw new Exception(report.Last());

        var enemies = enemySpots.Select(e => (p: e.p, e.area)).Where(e => EnemyOk(e.p)).ToList();
        foreach (var e in enemySpots.Where(e => !EnemyOk(e.p))) report.Add("Rejected enemy spot " + e.area + " " + e.p);

        Transform FindGroup(string n) => b.transform.Find(n) ?? throw new Exception("Missing " + n);
        Transform[] Make(RailSpawnKind kind, string groupName, IEnumerable<(Vector3 p, string area)> spots, float lift)
        {
            var parent = FindGroup(groupName);
            foreach (Transform c in parent.Cast<Transform>().ToArray()) Object.DestroyImmediate(c.gameObject);
            var list = new List<Transform>(); int n = 0;
            foreach (var (p, area) in spots)
            {
                var go = RailSpawnerPrefabs.Instantiate(kind, contents.scene);
                go.transform.SetParent(parent, false); go.transform.localPosition = p + Vector3.up * lift; go.transform.localRotation = Quaternion.identity;
                go.name = kind + " " + (++n).ToString("D3") + " " + area;
                list.Add(go.transform);
            }
            return list.ToArray();
        }
        b.spawnCandidates = new RailSpawnSet
        {
            coal = Make(RailSpawnKind.Coal, "Coal Spawns", coal.Select(s => (s.p, s.area)), .2f),
            gold = Make(RailSpawnKind.Gold, "Gold Spawns", gold.Select(s => (s.p, s.area)), .05f),
            enemies = Make(RailSpawnKind.Enemy, "Enemy Spawns", enemies, .05f),
        };
        b.spawnCandidatesMigrated = true;
        foreach (var g in coal.GroupBy(s => s.area).OrderBy(g => g.Key)) report.Add("  coal " + g.Key + ": " + g.Count());
        foreach (var g in gold.GroupBy(s => s.area).OrderBy(g => g.Key)) report.Add("  gold " + g.Key + ": " + g.Count());
        foreach (var g in enemies.GroupBy(e => Quadrant(e.p)).OrderBy(g => g.Key)) report.Add("  enemy quadrant " + g.Key + ": " + g.Count());
    }

    static string Quadrant(Vector3 p) => (p.z < 80 ? "S" : "N") + (p.x < 0 ? "W" : "E");

    /// <summary>Step 2: offline navigation for the whole block (existing catalogue tool).</summary>
    [MenuItem("Duck Rails/Authoring/Abandoned Station/2 Bake V3 navigation")]
    public static void BakeNavigationV3()
    {
        GuardV3();
        RailFullAreaNavigation.BakeOne(PrefabPath);
        EditorSceneManager.OpenScene(BakeScene, OpenSceneMode.Single);
    }

    /// <summary>Step 3: drop enemy candidates without ground navigation, verify 4/8/12 quadrant selection.</summary>
    [MenuItem("Duck Rails/Authoring/Abandoned Station/3 Prune enemies and set probes")]
    public static void PruneAndProbesV3()
    {
        GuardV3();
        var report = new List<string>();
        var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var b = contents.GetComponent<RailBlock>(); var graph = CrawlGraph.Load(b.graph);
            var keep = new List<Transform>();
            foreach (var e in b.spawnCandidates.enemies)
            {
                var local = b.transform.InverseTransformPoint(e.position);
                if (graph.FindNearestGroundNode(local, .8f) >= 0 && b.enemyBounds.Contains(local)) keep.Add(e);
                else { report.Add("Removed enemy without ground navigation: " + e.name); Object.DestroyImmediate(e.gameObject); }
            }
            b.spawnCandidates.enemies = keep.ToArray();
            for (int crew = 1; crew <= 8; crew++) for (int seed = 0; seed < 100; seed++)
                {
                    var chosen = RailEnemySpawnBalance.Select(b, seed, crew);
                    if (chosen.Length != 4 * RailEnemySpawnBalance.PerQuadrant(crew)) throw new Exception("Enemy quota short crew=" + crew + " seed=" + seed + " got " + chosen.Length);
                }
            report.Add("Enemies kept " + keep.Count + "; 800 crew/seed selections meet 4/8/12 with 15 m spacing");
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath, out bool ok); if (!ok) throw new Exception("Save failed");
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }

        var scene = EditorSceneManager.OpenScene(BakeScene, OpenSceneMode.Single);
        Physics.SyncTransforms();
        var probes = Object.FindFirstObjectByType<LightProbeGroup>();
        var points = new List<Vector3>(); const int env = 1 << 6;
        for (int z = 8; z <= 152; z += 4) for (int x = -42; x <= 42; x += 4)
            {
                var levels = new List<float> { F + 1.2f, Deck + 1.2f };
                float rail = RailHeight(z); levels.Add(rail + 1.6f); levels.Add(rail + 4f);
                foreach (float y in levels)
                {
                    var q = new Vector3(x, y, z);
                    if (Physics.CheckSphere(q, .25f, env, QueryTriggerInteraction.Ignore)) continue;
                    if (!Physics.Raycast(q, Vector3.down, 3.2f, env, QueryTriggerInteraction.Ignore) || !Physics.Raycast(q, Vector3.up, 5f, env, QueryTriggerInteraction.Ignore)) continue;
                    if (points.Any(p => (p - q).sqrMagnitude < 1)) continue;
                    points.Add(q);
                }
            }
        foreach (float z in new[] { 0f, 159f }) foreach (float x in new[] { -4f, 0f, 4f }) points.Add(new(x, 2, z));
        probes.probePositions = points.ToArray();
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        report.Add("Light probes: " + points.Count);
        Directory.CreateDirectory(EvidenceFolder); File.WriteAllLines(EvidenceFolder + "/prune-and-probes.txt", report);
        Debug.Log(string.Join("\n", report));
    }
}
#endif
