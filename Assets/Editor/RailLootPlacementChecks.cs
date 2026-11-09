#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailLootPlacementChecks
{
    [MenuItem("Duck Rails/Tests/Loot Placement")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var lines = new List<string>();
        void Check(bool value, string label) { if (!value) throw new Exception(label); lines.Add("PASS " + label); }
        var scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Spawn pool fixture");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            var b = root.AddComponent<RailBlock>(); b.id = "fixture";
            b.track = new[] { Vector3.zero, Vector3.forward * 160 };
            var old = new GameObject("Legacy marker").transform; old.SetParent(root.transform); old.localPosition = Vector3.right * -10;
            b.variations = new[] { new RailSpawnSet { coal = new[] { old }, gold = new[] { old } }, new RailSpawnSet { coal = new[] { old } } };
            Check(RailSpawnPool.Candidates(b, RailSpawnKind.Coal).SequenceEqual(new[] { old }), "Legacy pools merge shared references once");
            root.transform.SetPositionAndRotation(new Vector3(20, 3, -7), Quaternion.Euler(0, 30, 0));
            foreach (RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
            {
                int before = RailSpawnPool.Candidates(b, kind).Length;
                var position = new Vector3(23, 4, -5);
                var marker = RailLootPlacementWindow.Place(b, kind, position);
                Check(RailSpawnPool.Candidates(b, kind).Length == before + 1 && Vector3.Distance(marker.position, position) < .001f, kind + " placement and transformed parent");
                Check(kind == RailSpawnKind.Coal ? marker.GetComponent<CoalSpawner>() != null : kind == RailSpawnKind.Gold ? marker.GetComponent<RailGoldSpawner>() != null : marker.GetComponent<RailEnemySpawner>() != null, kind + " marker component");
                Undo.PerformUndo();
                Check(RailSpawnPool.Candidates(b, kind).Length == before && marker == null, kind + " undo reference and object");
                Undo.PerformRedo();
                Check(RailSpawnPool.Candidates(b, kind).Length == before + 1 && RailSpawnPool.Candidates(b, kind).All(p => p != null), kind + " redo");
            }
            Check(b.spawnCandidatesMigrated && b.spawnCandidates.coal.Contains(old) && b.spawnCandidates.gold.Contains(old), "Migration preserves legacy markers across types");
            var points = new List<Transform>();
            for (int i = 0; i < 24; i++)
            {
                var p = new GameObject("Candidate " + i).transform; p.SetParent(root.transform);
                p.localPosition = new Vector3(i % 2 == 0 ? -10 : 10, 0, i * 3); points.Add(p);
            }
            b.spawnCandidates = new RailSpawnSet { coal = points.ToArray(), gold = points.ToArray(), enemies = points.ToArray() };
            b.coalCount = 6; b.goldCount = 2; b.enemyCount = 5;
            var combinations = new HashSet<string>();
            for (int seed = 0; seed < 100; seed++)
            {
                foreach (RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
                {
                    var chosen = RailSpawnPool.Select(b, kind, seed);
                    Check(chosen.Length == RailSpawnPool.Count(b, kind) && chosen.Distinct().Count() == chosen.Length, kind + " exact count and no repeats seed " + seed);
                    Check(chosen.SequenceEqual(RailSpawnPool.Select(b, kind, seed)), kind + " deterministic replay seed " + seed);
                }
                var enemies = RailSpawnPool.Select(b, RailSpawnKind.Enemy, seed);
                Check(enemies.Any(p => RailEnemySpawns.Side(p) < 0) && enemies.Any(p => RailEnemySpawns.Side(p) > 0), "Bilateral enemies seed " + seed);
                combinations.Add(string.Join(",", RailSpawnPool.Select(b, RailSpawnKind.Coal, seed).Select(p => p.name)));
            }
            Check(combinations.Count > 90, "Run seeds vary candidate selections");
            var expectedEnemies = RailSpawnPool.Select(b, RailSpawnKind.Enemy, 42);
            b.coalCount = 0;
            Check(RailSpawnPool.Select(b, RailSpawnKind.Coal, 42).Length == 0 && expectedEnemies.SequenceEqual(RailSpawnPool.Select(b, RailSpawnKind.Enemy, 42)), "Zero and independent type streams");
            b.coalCount = 100;
            points[0].gameObject.SetActive(false);
            var overlap = new GameObject("Overlapping alternative").transform; overlap.SetParent(root.transform); overlap.position = points[1].position;
            b.spawnCandidates.coal = points.Concat(new[] { overlap, points[1], null }).ToArray();
            Check(RailSpawnPool.Select(b, RailSpawnKind.Coal, 42).Length == 23, "Undersupply clamps; ignores null/inactive/repeated references and overlapping positions");
            b.enemyCount = 1;
            Check(RailSpawnPool.Select(b, RailSpawnKind.Enemy, 42).Length == 1, "Single enemy allowed");
            b.enemyCount = 0;
            var selected = new List<Transform>(); var types = new List<int>();
            RailSpawnPool.SelectEnemies(b, 42, selected, types);
            Check(selected.Count == 0 && types.Count == 0, "Enemy count zero");
            // A track candidate plus one side must not lose that side when the other is inserted.
            points[0].gameObject.SetActive(true);
            points[2].localPosition = new Vector3(0, 0, 20);
            b.spawnCandidates.enemies = points.Take(3).ToArray(); b.enemyCount = 2;
            for (int seed = 0; seed < 100; seed++)
            {
                var pair = RailSpawnPool.Select(b, RailSpawnKind.Enemy, seed);
                Check(pair.Length == 2 && pair.Any(p => RailEnemySpawns.Side(p) < 0) && pair.Any(p => RailEnemySpawns.Side(p) > 0), "Two-enemy bilateral selection with track candidate " + seed);
            }
            foreach (bool gold in new[] { false, true })
            {
                var bounds = RailLootPlacementWindow.ItemBounds(gold);
                Check(bounds.size.y > 0 && bounds.size.x < 2, "Runtime bounds " + gold);
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(floor, scene);
            floor.transform.SetParent(root.transform); floor.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            floor.layer = LayerMask.NameToLayer("Environment");
            var colliders = new[] { floor.GetComponent<Collider>() };
            Physics.SyncTransforms();
            var ray = new Ray(Vector3.up * 5, Vector3.down);
            Check(RailLootPlacementWindow.FloorHit(colliders, ray, out _), "Preview-scene Environment floor");
            Check(!RailLootPlacementWindow.FloorHit(colliders, new Ray(Vector3.right * 5, Vector3.left), out _), "Wall rejection");
            floor.layer = 0;
            Check(!RailLootPlacementWindow.FloorHit(colliders, ray, out _), "Wrong layer rejection");
            floor.layer = LayerMask.NameToLayer("Environment"); colliders[0].isTrigger = true;
            Check(!RailLootPlacementWindow.FloorHit(colliders, ray, out _), "Trigger rejection");
            Check(!RailLootPlacementWindow.EnemyLocation(b, Vector3.zero, null), "Enemy rejects missing baked graph");
        }
        finally
        {
            foreach (var c in root.GetComponentsInChildren<Component>(true)) Undo.ClearUndo(c);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) Undo.ClearUndo(t.gameObject);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks" }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            var block = asset.GetComponent<RailBlock>(); if (block == null) continue;
            var graph = Game.AI.CrawlGraph.Load(block.graph);
            var enemies = RailSpawnPool.Available(block, RailSpawnKind.Enemy);
            Check(enemies.Length >= block.enemyCount, block.id + " sufficient enemy candidates");
            Check(enemies.All(p => RailLootPlacementWindow.EnemyLocation(block, p.position, graph)), block.id + " enemies inside bounds and baked ground");
            Check(!RailLootPlacementWindow.EnemyLocation(block, block.transform.TransformPoint(block.enemyBounds.max + Vector3.one * 10), graph), block.id + " out-of-bounds rejection");
            for (int seed = 0; seed < 10; seed++)
            {
                var a = RailSpawnPool.Select(block, RailSpawnKind.Enemy, seed);
                Check(a.Length == block.enemyCount && a.Any(p => RailEnemySpawns.Side(p) < 0) && a.Any(p => RailEnemySpawns.Side(p) > 0), block.id + " exact bilateral enemy selection " + seed);
            }
        }
        Directory.CreateDirectory("docs/validation/spawn-pools");
        File.WriteAllLines("docs/validation/spawn-pools/editor-checks.txt", lines);
        Debug.Log("SPAWN POOLS: " + lines.Count + " checks passed.");
    }
}
#endif
