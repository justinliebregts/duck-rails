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
    [MenuItem("Duck Rails/Run Test Block Order Checks")]
    public static void Run()
    {
        if (Application.isPlaying) throw new Exception("Stop Play before running editor checks");
        var lines = new List<string>();
        void Check(bool value, string label) { if (!value) throw new Exception(label); lines.Add("PASS " + label); }
        var catalog = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks" })
            .Select(g => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)).GetComponent<RailBlock>())
            .Where(b => b != null && b.id != "bridge-finale-v1").OrderByDescending(b => b.id).ToArray();
        foreach (int count in new[] { 1, 2, 3, 5, 12 })
        {
            var selected = catalog.Take(count).ToArray();
            var state = RailRoute.ChooseOrdered("ordered-check", selected, 12345);
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
                route.midpointShop = new GameObject("Shop position check").transform; route.midpointShop.parent = root.transform;
                route.Build(copy);
                Check(route.Blocks.Select(b => b.id).SequenceEqual(state.blocks), count + " actual prefab instances in order");
                Check(route.finaleRoot.position.z == RailRoute.StartZ + selected.Sum(b => b.length), count + " bridge follows last block");
                Check(route.midpointShop.position == state.placements[count / 2] + new Vector3(-16, 0, 148), count + " midpoint shop placement");
                Check(Mathf.Abs(route.FinaleStart - RailRoute.StationDepartureLength - selected.Sum(b => b.TrackLength)) < .01f, count + " train route distance");
                Check(route.expedition.coalPoints.Length == count * 6 && route.expedition.baseEnemyCount >= count * 4 && route.expedition.baseEnemyCount <= count * 6, count + " supplies and encounters scale with route");
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
        for (int seed = 0; seed < 20; seed++)
        { var normal = RailRoute.Choose("normal", catalog, seed); Check(normal.blocks.Length == 5 && normal.blocks.Distinct().Count() == 5, "Normal route still five unique blocks, seed " + seed); }
        Directory.CreateDirectory("docs/validation/test-block-order");
        File.WriteAllLines("docs/validation/test-block-order/editor-checks.txt", lines);
        Debug.Log("TEST BLOCK ORDER: " + lines.Count + " checks passed");
    }
}
#endif
