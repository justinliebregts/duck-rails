#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailTestBlockOrderChecks
{
    [MenuItem("Duck Rails/Legacy/Run Test Block Order Checks")]
    public static void Run()
    {
        if (Application.isPlaying) throw new Exception("Stop Play before running editor checks");
        var lines = new List<string>();
        void Check(bool value, string label) { if (!value) throw new Exception(label); lines.Add("PASS " + label); }
        var catalog = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<RailBlock>())
            .Where(b => b != null && b.id != "bridge-finale-v1" && b.id != RailRoute.MidpointId).OrderByDescending(b => b.id).ToArray();
        foreach (int count in new[] { 1, 2, 3, 4, 5, 12 })
        {
            var selected = count == RailRoute.NormalBlockCount
                ? RailRoute.ChooseNormal("assembly", catalog, 12345).blocks.Select(id => catalog.Single(b => b.id == id)).ToArray()
                : catalog.Take(count).ToArray();
            var state = RailRoute.ChooseOrdered("ordered-check", selected, 12345);
            state.resourceCrew = count == 4 ? 8 : 1;
            Check(state.blocks.SequenceEqual(selected.Select(b => b.id)), count + " blocks preserve input order");
            var copy = JsonUtility.FromJson<RailRouteState>(JsonUtility.ToJson(state));
            RailRoute.Validate(copy, catalog);
            Check(copy.blocks.SequenceEqual(state.blocks) && copy.placements.SequenceEqual(state.placements) && copy.variations.SequenceEqual(state.variations), count + " blocks survive retained-state serialization");
            var scene = EditorSceneManager.NewPreviewScene();
            try
            {
                var root = new GameObject("Ordered route checks");
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root, scene);
                var route = root.AddComponent<RailRoute>(); route.catalog = catalog;
                var data = new GameObject("Inactive data"); data.transform.parent = root.transform; data.SetActive(false);
                route.expedition = data.AddComponent<RailExpedition>(); route.manager = data.AddComponent<Game.AI.EnemyManager>();
                route.startingStation = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/StartingStation.prefab"), scene)).GetComponent<RailBlock>();
                route.finale = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/bridge-finale-v1.prefab"), scene)).GetComponent<RailBlock>();
                route.finaleRoot = route.finale.transform;
                route.midpointBlock = ((GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RailHelipadPreview.Path), scene)).GetComponent<RailBlock>();
                route.Build(copy);
                Check(route.Blocks.Select(b => b.id).SequenceEqual(state.blocks), count + " actual prefab instances in order");
                Check(route.finaleRoot.position.z == RailRoute.StartZ + selected.Sum(b => b.length) + RailRoute.MidpointLength, count + " bridge follows last block");
                Check(route.midpointBlock.transform.position == state.placements[(count - 1) / 2] + Vector3.forward * selected[(count-1)/2].length, count + " midpoint block placement");
                Check(Mathf.Abs(route.FinaleStart - RailRoute.StationDepartureLength - selected.Sum(b => b.TrackLength) - RailRoute.MidpointLength) < .01f, count + " train route distance");
                int FixedResources(RailSpawnKind kind) => RailSpawnPool.Select(route.midpointBlock, kind, copy.enemySeed).Length +
                    RailSpawnPool.Select(route.finale, kind, copy.enemySeed).Length;
                Check(route.expedition.ActivePoints(RailSpawnKind.Coal).Length == selected.Sum(b => RailRoute.SelectResources(b, RailSpawnKind.Coal, copy).Length) + FixedResources(RailSpawnKind.Coal) &&
                    route.expedition.ActivePoints(RailSpawnKind.Gold).Length == selected.Sum(b => RailRoute.SelectResources(b, RailSpawnKind.Gold, copy).Length) + FixedResources(RailSpawnKind.Gold) &&
                    route.expedition.ActiveBaseEnemyCount == selected.Sum(b => RailEnemySpawnBalance.Select(b, copy.enemySeed, copy.resourceCrew).Length), count + " exact configured supplies and encounters scale with route");
                foreach (var block in route.Blocks)
                {
                    foreach (var kind in new[] { RailSpawnKind.Coal, RailSpawnKind.Gold, RailSpawnKind.Enemy })
                    {
                        var actual = route.expedition.ActivePoints(kind);
                        var chosen = kind == RailSpawnKind.Enemy ? RailEnemySpawnBalance.Select(block, copy.enemySeed, copy.resourceCrew) : RailRoute.SelectResources(block, kind, copy);
                        Check(actual.Where(p => p.IsChildOf(block.transform)).SequenceEqual(chosen), count + " route uses seeded pool " + block.id + "/" + kind);
                    }
                }
            }
            finally { EditorSceneManager.ClosePreviewScene(scene); }
        }
        void Reject(Action action, string label)
        { bool rejected = false; try { action(); } catch (Exception) { rejected = true; } Check(rejected, label); }
        Reject(() => RailRoute.ChooseOrdered("empty", Array.Empty<RailBlock>(), 1), "Empty list rejected");
        Reject(() => RailRoute.ChooseOrdered("null", new RailBlock[] { null }, 1), "Missing prefab rejected");
        Reject(() => RailRoute.ChooseOrdered("duplicate", new[] { catalog[0], catalog[0] }, 1), "Duplicate blocks rejected");
        var invalid = RailRoute.ChooseOrdered("invalid", catalog.Take(1).ToArray(), 1);
        invalid.placements[0].x = float.NaN;
        Reject(() => RailRoute.Validate(invalid, catalog), "Nonfinite placement rejected");
        var expected = new[] {
            "container-yard-v1", "convenience-store-v1", "farm-silos-v1", "lakeside-campground-v1",
            "nuclear-power-plant-v1", "passenger-station-v1", "roadside-motel-v1", "skyscraper-works-v1"
        };
        Check(catalog.Where(b => b.allowedInNormalRuns).Select(b => b.id).OrderBy(id => id).SequenceEqual(expected), "Expected normal route pool enabled");
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 100; seed++)
        {
            var normal = RailRoute.ChooseNormal("normal", catalog, seed);
            Check(normal.blocks.Length == RailRoute.NormalBlockCount && normal.blocks.Distinct().Count() == RailRoute.NormalBlockCount &&
                normal.blocks.All(expected.Contains), "Four distinct eligible blocks, seed " + seed);
            Check(JsonUtility.ToJson(normal) == JsonUtility.ToJson(RailRoute.ChooseNormal("normal", catalog.Reverse().ToArray(), seed)), "Deterministic despite catalogue ordering, seed " + seed);
            var copy = JsonUtility.FromJson<RailRouteState>(JsonUtility.ToJson(normal));
            RailRoute.Validate(copy, catalog);
            Check(copy.blocks.SequenceEqual(normal.blocks) && copy.placements.SequenceEqual(normal.placements) && copy.variations.SequenceEqual(normal.variations), "Retained normal route round trip, seed " + seed);
            seen.UnionWith(normal.blocks);
        }
        Check(seen.SetEquals(expected), "Every eligible block selected across 100 seeds");
        Reject(() => RailRoute.ChooseNormal("short-pool", catalog.Where(b => b.allowedInNormalRuns).Take(3).ToArray(), 1), "Insufficient enabled pool rejected without disabled fallback");
        Directory.CreateDirectory("docs/validation/test-block-order");
        File.WriteAllLines("docs/validation/test-block-order/editor-checks.txt", lines);
        Debug.Log("TEST BLOCK ORDER: " + lines.Count + " checks passed");
    }
}
#endif
