#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Offline catalogue bake, preserving the chunk lattice used by route assembly.</summary>
public static class RailFullAreaNavigation
{
    public const string Evidence = "docs/validation/full-area-navigation.txt";
    [MenuItem("Duck Rails/Authoring/Rebake Full Playable Navigation")]
    public static void Start() => RunBatch(0, RailBlockAuthoringCleanup.Paths.Length);

    // Explicit batches also work through MCP when background editor delay calls are suspended.
    public static void RunBatch(int start, int count)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning)
            throw new InvalidOperationException("Use Edit Mode outside a lighting bake.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null && stage.scene.isDirty) throw new InvalidOperationException("Save the open prefab first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        foreach (var s in setup)
            if (s.isLoaded && SceneManager.GetSceneByPath(s.path).isDirty)
                throw new InvalidOperationException("Save open scene changes first.");
        var stagePath = stage == null ? null : stage.assetPath;
        StageUtility.GoToMainStage();
        Directory.CreateDirectory(Path.GetDirectoryName(Evidence));
        if (start == 0) File.WriteAllText(Evidence, "Offline full-area bake " + DateTime.UtcNow.ToString("O") + "\n");
        try
        {
            foreach (var path in RailBlockAuthoringCleanup.Paths.Skip(start).Take(count)) BakeOne(path);
        }
        finally
        {
            EditorSceneManager.RestoreSceneManagerSetup(setup);
            if (!string.IsNullOrEmpty(stagePath)) PrefabStageUtility.OpenPrefab(stagePath);
            SceneView.RepaintAll();
        }
    }

    public static void BakeOne(string path)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (asset.GetComponentInChildren<CrawlGraphBaker>(true) == null)
        {
            if (asset.GetComponent<RailBlock>().id != "starting-station-v1") throw new Exception("Missing baker: " + path);
            File.AppendAllText(Evidence, "starting-station-v1: safe station, no enemy graph required.\n");
            return;
        }
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = UnityEngine.Object.Instantiate(asset);
        root.name = asset.name;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        var block = root.GetComponent<RailBlock>();
        var baker = root.GetComponentInChildren<CrawlGraphBaker>(true);
        var graphPath = AssetDatabase.GetAssetPath(baker.graphAsset);
        if (string.IsNullOrEmpty(graphPath)) throw new Exception("Missing graph asset: " + path);
        var oldGraph = File.ReadAllBytes(graphPath);
        var oldPrefab = File.ReadAllBytes(path);
        var markerPoses = root.GetComponentsInChildren<RailSpawnMarker>(true)
            .Select(m => (m.transform.position, m.transform.rotation, m.kind)).ToArray();
        Physics.SyncTransforms();
        var colliders = root.GetComponentsInChildren<Collider>().Where(c => c.enabled && !c.isTrigger
            && c.gameObject.layer == LayerMask.NameToLayer("Environment")).ToArray();
        if (colliders.Length == 0) throw new Exception("No Environment colliders: " + path);
        var playable = colliders[0].bounds;
        foreach (var c in colliders) playable.Encapsulate(c.bounds);
        var lo = playable.min; var hi = playable.max;
        lo.z = 0; hi.z = block.length;
        lo.y -= 1; hi.y += 1;
        playable.SetMinMax(lo, hi);
        block.enemyBounds = playable;
        // Include surface sampling margin, keeping the previous chunk-grid phase on every axis.
        var origin = baker.BakeBounds.min;
        float chunk = baker.cellSize * 16;
        lo -= Vector3.one * baker.cellSize;
        hi += Vector3.one * baker.cellSize;
        for (int axis = 0; axis < 3; axis++)
            lo[axis] = origin[axis] + Mathf.Floor((lo[axis] - origin[axis]) / chunk) * chunk;
        baker.transform.position = (lo + hi) * .5f;
        baker.size = hi - lo;
        // Spawners are authoring candidates, not a whitelist of reachable islands.
        baker.seeds = Array.Empty<Transform>();
        try
        {
            if (!CrawlGraphBake.Bake(baker, false)) throw new Exception("Bake cancelled/failed: " + path);
            block.graph = baker.graphAsset;
            var graph = CrawlGraph.Load(block.graph);
            if (graph.NodeCount == 0) throw new Exception("Empty graph: " + path);
            var covered = new Bounds(graph.GetPosition(0), Vector3.zero);
            for (int i = 1; i < graph.NodeCount; i++) covered.Encapsulate(graph.GetPosition(i));
            if (covered.min.z > 2 || covered.max.z < block.length - 2)
                throw new Exception("Graph misses block ends: " + path + " " + covered);
            // Save only navigation settings through prefab contents. Saving an instantiated
            // copy can regenerate IDs for repeated geometry names and break scene bindings.
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var target = contents.GetComponent<RailBlock>();
                var settings = contents.GetComponentInChildren<CrawlGraphBaker>(true);
                target.enemyBounds = block.enemyBounds;
                target.graph = block.graph;
                settings.transform.localPosition = baker.transform.localPosition;
                settings.size = baker.size;
                settings.seeds = Array.Empty<Transform>();
                settings.lastBakeReport = baker.lastBakeReport;
                PrefabUtility.SaveAsPrefabAsset(contents, path, out bool success);
                if (!success) throw new Exception("Could not save " + path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            var saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var after = saved.GetComponentsInChildren<RailSpawnMarker>(true)
                .Select(m => (m.transform.position, m.transform.rotation, m.kind)).ToArray();
            if (!markerPoses.SequenceEqual(after)) throw new Exception("Marker poses changed: " + path);
            int unsupported = block.spawnCandidates.enemies.Count(t => t != null && graph.FindNearestGroundNode(t.position, .8f) < 0);
            File.AppendAllText(Evidence, block.id + ": bounds=" + playable + "; graph=" + covered
                + "; nodes=" + graph.NodeCount + "; preservedMarkers=" + after.Length
                + "; existingEnemyMarkersWithoutGroundWithin0.8m=" + unsupported + "\n");
        }
        catch
        {
            File.WriteAllBytes(graphPath, oldGraph); File.WriteAllBytes(path, oldPrefab);
            AssetDatabase.ImportAsset(graphPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            throw;
        }
    }
}
#endif
