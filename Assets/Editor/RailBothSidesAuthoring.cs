#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Offline migration of one-sided encounters; leaves reviewed two-sided layouts intact.</summary>
public static class RailBothSidesAuthoring
{
    public static bool HasBothSides(RailBlock b) => b.variations.All(v =>
        v.enemies.Any(p => RailEnemySpawns.Side(p) < 0) && v.enemies.Any(p => RailEnemySpawns.Side(p) > 0));

    [MenuItem("Duck Rails/Authoring/Enable Enemies On Both Sides")]
    public static void Run()
    {
        RailCanyon.Guard();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { RailWeek3Builder.Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<RailBlock>();
                if (asset == null || HasBothSides(asset)) continue;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                var b = root.GetComponent<RailBlock>();
                Upgrade(b);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                Debug.Log("BOTH SIDES: baked and saved " + b.id);
            }
            // The finale is scene-owned, rather than instantiated from its catalogue prefab.
            EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
            var route = Object.FindFirstObjectByType<RailRoute>();
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(RailWeek3Builder.Folder + "/bridge-finale-v1.prefab").GetComponent<RailBlock>();
            var target = route.finale;
            target.enemyBounds = source.enemyBounds;
            target.enemySurfaceRailExclusion = source.enemySurfaceRailExclusion;
            target.graph = source.graph;
            for (int v = 0; v < source.variations.Length; v++)
            for (int i = 0; i < source.variations[v].enemies.Length; i++)
                target.variations[v].enemies[i].position = target.transform.TransformPoint(source.transform.InverseTransformPoint(source.variations[v].enemies[i].position));
            EditorUtility.SetDirty(target);
            EditorSceneManager.SaveScene(route.gameObject.scene);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }

    static void Upgrade(RailBlock b)
    {
        var baker = b.GetComponentInChildren<CrawlGraphBaker>();
        if (b.enemyBounds.min.x > 0)
        {
            var min = b.enemyBounds.min; min.x = -b.enemyBounds.max.x;
            var bounds = b.enemyBounds; bounds.SetMinMax(min, bounds.max); b.enemyBounds = bounds;
            // Keep the shared chunk origin modulo 16: original X min is 8, new min -40/-56.
            var bakeMin = baker.transform.localPosition - baker.size / 2;
            var bakeMax = baker.transform.localPosition + baker.size / 2;
            while (bakeMin.x > min.x - 1) bakeMin.x -= 16;
            baker.size = bakeMax - bakeMin; baker.transform.localPosition = (bakeMax + bakeMin) / 2;
        }
        // Discover all usable islands before selecting new seeds; baking is editor-only.
        baker.seeds = Array.Empty<Transform>();
        Physics.SyncTransforms();
        if (!CrawlGraphBake.Bake(baker, false)) throw new Exception("Both-side graph failed: " + b.id);
        b.graph = baker.graphAsset;
        var graph = CrawlGraph.Load(b.graph);
        var available = Enumerable.Range(0, graph.NodeCount).Where(graph.IsGroundNode).Select(graph.GetPosition)
            .Where(p => p.x < -9 && b.enemyBounds.Contains(p) && p.y >= -.1f && p.y <= 2.5f &&
                !Physics.CheckCapsule(p + Vector3.up * .5f, p + Vector3.up * 1.6f, .45f, 1 << 6, QueryTriggerInteraction.Ignore)).ToArray();
        foreach (var set in b.variations)
        {
            var selected = new List<Vector3>();
            foreach (int index in new[] { 0, 2, 4 })
            {
                Vector3 desired = b.transform.InverseTransformPoint(set.enemies[index].position); desired.x = -Mathf.Abs(desired.x); desired.y = 0;
                var options = available.Where(p => selected.All(q => Vector3.Distance(p, q) >= 8)).OrderBy(p => (p - desired).sqrMagnitude).ToArray();
                if (options.Length == 0) throw new Exception("No clear left-side candidates: " + b.id);
                selected.Add(options[0]); set.enemies[index].position = b.transform.TransformPoint(options[0] + Vector3.up * .05f);
            }
        }
        baker.seeds = b.variations.SelectMany(v => v.enemies).ToArray();
        if (!CrawlGraphBake.Bake(baker, false)) throw new Exception("Seeded both-side graph failed: " + b.id);
        b.graph = baker.graphAsset;
    }
}
#endif
