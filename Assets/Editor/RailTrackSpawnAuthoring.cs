#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Place railway encounter candidates against existing geometry and offline graphs.</summary>
public static class RailTrackSpawnAuthoring
{
    public static Vector3 TrackAt(RailBlock b, float z)
    {
        for (int i = 1; i < b.track.Length; i++)
            if (z <= b.track[i].z || i == b.track.Length - 1)
                return Vector3.Lerp(b.track[i - 1], b.track[i], Mathf.InverseLerp(b.track[i - 1].z, b.track[i].z, z));
        throw new Exception("Missing track: " + b.id);
    }

    public static bool OnTrack(RailBlock b, Transform marker)
    {
        var p = b.transform.InverseTransformPoint(marker.position);
        var rail = TrackAt(b, p.z);
        return Mathf.Abs(p.x - rail.x) < 1 && Mathf.Abs(p.y - rail.y) < .8f;
    }

    static bool Clear(Vector3 p) => Physics.Raycast(p + Vector3.up * .2f, Vector3.down, .6f, 1 << 6, QueryTriggerInteraction.Ignore)
        && !Physics.CheckCapsule(p + Vector3.up * .5f, p + Vector3.up * 1.6f, .4f, 1 << 6, QueryTriggerInteraction.Ignore);

    static HashSet<int> Reachable(CrawlGraph graph, RailBlock b, Transform[] markers, int side)
    {
        var seen = new HashSet<int>(); var queue = new Queue<int>();
        foreach (var marker in markers.Where(m => RailEnemySpawns.Side(m) == side))
        {
            int n = graph.FindNearestGroundNode(b.transform.InverseTransformPoint(marker.position), 2);
            if (n >= 0 && seen.Add(n)) queue.Enqueue(n);
        }
        while (queue.Count > 0)
        {
            int n = queue.Dequeue();
            for (int e = graph.EdgeStart[n]; e < graph.EdgeStart[n + 1]; e++)
            {
                int next = graph.EdgeTarget[e];
                if (graph.IsGroundNode(next) && seen.Add(next)) queue.Enqueue(next);
            }
        }
        return seen;
    }

    public static void Upgrade(RailBlock b)
    {
        var graph = CrawlGraph.Load(b.graph);
        Physics.SyncTransforms();
        foreach (var set in b.variations)
        foreach (var marker in set.enemies)
        {
            var old = b.transform.InverseTransformPoint(marker.position);
            if (b.enemyBounds.Contains(old) && graph.FindNearestGroundNode(old, 1) >= 0 && Clear(marker.position)) continue;
            int side = RailEnemySpawns.Side(marker);
            var replacements = Enumerable.Range(0, graph.NodeCount).Where(graph.IsGroundNode).Select(n => graph.GetPosition(n) + Vector3.up * .05f)
                .Where(p => b.enemyBounds.Contains(p) && (side == 0 ? Mathf.Abs(p.x - TrackAt(b, p.z).x) < 1 : side * (p.x - TrackAt(b, p.z).x) >= 1))
                .OrderBy(p => (p - old).sqrMagnitude);
            bool repaired = false;
            foreach (var p in replacements)
            {
                var world = b.transform.TransformPoint(p);
                if (!Clear(world) || set.enemies.Any(m => m != marker && Vector3.Distance(m.position, world) < 4)) continue;
                marker.position = world; repaired = true; break;
            }
            if (!repaired) throw new Exception("No safe replacement for " + b.id + "/" + marker.name);
        }
        var available = new List<Vector3>();
        for (float z = b.enemyBounds.min.z + 4; z <= b.enemyBounds.max.z - 4; z += 1)
        {
            var rail = TrackAt(b, z);
            var world = b.transform.TransformPoint(rail);
            if (!Physics.Raycast(world + Vector3.up * .6f, Vector3.down, out var hit, 1.3f, 1 << 6, QueryTriggerInteraction.Ignore)) continue;
            var p = b.transform.InverseTransformPoint(hit.point + Vector3.up * .05f);
            if (b.enemyBounds.Contains(p) && graph.FindNearestGroundNode(p, .8f) >= 0 && Clear(b.transform.TransformPoint(p))) available.Add(p);
        }
        for (int v = 0; v < b.variations.Length; v++)
        {
            var markers = b.variations[v].enemies;
            if (markers.Any(p => OnTrack(b, p) && Clear(p.position))) continue;
            if (available.Count == 0) throw new Exception("No supported railway spawn: " + b.id);
            float desiredZ = Mathf.Lerp(b.enemyBounds.min.z, b.enemyBounds.max.z, .35f + v * .15f);
            var west = Reachable(graph, b, markers, -1); var east = Reachable(graph, b, markers, 1);
            var options = available.Where(p => west.Contains(graph.FindNearestGroundNode(p, .8f)) && east.Contains(graph.FindNearestGroundNode(p, .8f))).OrderBy(p => Mathf.Abs(p.z - desiredZ)).ToArray();
            if (options.Length == 0) throw new Exception("No railway spawn connected to both sides: " + b.id);
            var chosen = options[0];
            // Preserve elevated encounters and at least one existing marker on each side.
            var replace = markers.Where(p => RailEnemySpawns.Side(p) == 0 || markers.Count(q => RailEnemySpawns.Side(q) == RailEnemySpawns.Side(p)) > 1)
                .OrderBy(p => Mathf.Abs(b.transform.InverseTransformPoint(p.position).y - TrackAt(b, b.transform.InverseTransformPoint(p.position).z).y))
                .ThenBy(p => (b.transform.InverseTransformPoint(p.position) - chosen).sqrMagnitude).First();
            replace.position = b.transform.TransformPoint(chosen);
        }
    }

    [MenuItem("Duck Rails/Authoring/Enable Track Enemy Spawns")]
    public static void Run() => Process(true);

    [MenuItem("Duck Rails/Tests/Enemy Spawns/All Blocks And Scene Finale")]
    public static void Check() => Process(false);

    static void Process(bool write)
    {
        RailCanyon.Guard();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        var log = new List<string>();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponent<RailBlock>() == null) continue;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(asset);
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var b = root.GetComponent<RailBlock>();
                if (write) Upgrade(b);
                Validate(b, log);
                if (write) PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
            var finale = Object.FindFirstObjectByType<RailRoute>().finale;
            if (write) Upgrade(finale);
            Validate(finale, log);
            if (write) { EditorUtility.SetDirty(finale); EditorSceneManager.SaveScene(finale.gameObject.scene); }
            Directory.CreateDirectory("docs/validation/enemy-spawns");
            File.WriteAllLines("docs/validation/enemy-spawns/track-spawns.txt", log);
            Debug.Log("TRACK SPAWNS: " + log.Count + " variations passed (catalogue and scene finale), 100 seeds each.");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    static void Validate(RailBlock b, List<string> log)
    {
        Physics.SyncTransforms();
        var g = CrawlGraph.Load(b.graph);
        foreach (var set in b.variations)
        {
            foreach (var marker in set.enemies)
            {
                var local = b.transform.InverseTransformPoint(marker.position);
                if (!b.enemyBounds.Contains(local) || g.FindNearestGroundNode(local, 1) < 0 || !Clear(marker.position))
                    throw new Exception("Invalid enemy candidate: " + b.id + "/" + marker.name);
            }
            var tracks = set.enemies.Where(p => OnTrack(b, p)).ToArray();
            if (set.enemies.Length != 6 || tracks.Length == 0 || !set.enemies.Any(p => RailEnemySpawns.Side(p) < 0) || !set.enemies.Any(p => RailEnemySpawns.Side(p) > 0))
                throw new Exception("Missing spawn region: " + b.id);
            foreach (var t in tracks)
            {
                var p = b.transform.InverseTransformPoint(t.position);
                if (!b.enemyBounds.Contains(p) || !Clear(t.position) || g.FindNearestGroundNode(p, .8f) < 0)
                    throw new Exception("Unsafe track spawn: " + b.id + " " + p);
                foreach (int side in new[] { -1, 1 })
                    if (!Reachable(g, b, set.enemies, side).Contains(g.FindNearestGroundNode(p, .8f)))
                        throw new Exception("Track disconnected from side " + side + ": " + b.id);
            }
            int trackSelections = 0;
            for (int seed = 0; seed < 100; seed++)
            {
                var points = new List<Transform>(); var types = new List<int>();
                var replay = new List<Transform>(); var replayTypes = new List<int>();
                RailEnemySpawns.Select(set.enemies, new System.Random(seed), points, types);
                RailEnemySpawns.Select(set.enemies, new System.Random(seed), replay, replayTypes);
                if (points.Count < 4 || points.Count > 6 || points.Distinct().Count() != points.Count || !points.Any(p => RailEnemySpawns.Side(p) < 0) || !points.Any(p => RailEnemySpawns.Side(p) > 0)
                    || !points.SequenceEqual(replay) || !types.SequenceEqual(replayTypes)) throw new Exception("Selection regression: " + b.id);
                if (points.Any(p => OnTrack(b, p))) trackSelections++;
            }
            if (trackSelections == 0) throw new Exception("Track never selected: " + b.id);
            log.Add("PASS " + b.id + " variation=" + Array.IndexOf(b.variations, set) + " track=" + string.Join(",", tracks.Select(t => b.transform.InverseTransformPoint(t.position))) + " supported, clear, bounded, ground-connected to both sides; track selected in " + trackSelections + "/100 deterministic 4-6-enemy seeds");
        }
    }
}
#endif
