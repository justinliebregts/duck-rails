#if UNITY_EDITOR
using System;
using System.Linq;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailEnemyAuthoring
{
    [MenuItem("Duck Rails/Authoring/Install Mixed Enemies")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits first.");
        string[] sources = { "ZombieBomber", "ZombieSmasher", "ZombieSpider" };
        for (int i = 0; i < sources.Length; i++)
        {
            string path = "Assets/Resources/Rails/" + RailEnemySpawns.Names[i + 1] + ".prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) continue;
            var root = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/" + sources[i] + ".prefab"));
            try
            {
                root.name = RailEnemySpawns.Names[i + 1];
                root.AddComponent<RailEnemyBounds>();
                var carrier = root.AddComponent<RailCoalCarrier>();
                var display = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/RailZombie.prefab").GetComponent<RailCoalCarrier>().display.gameObject);
                display.name = "Carried coal"; display.transform.SetParent(root.GetComponent<Enemy>().Root, false);
                display.transform.localPosition = new Vector3(.35f, .65f, .35f); carrier.display = display.transform;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { RailWeek3Builder.Folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid); var root = PrefabUtility.LoadPrefabContents(path);
            try { var block = root.GetComponent<RailBlock>(); if (block == null) continue; AddCandidates(block); PrefabUtility.SaveAsPrefabAsset(root, path); }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        try
        {
            var scene = EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
            var route = UnityEngine.Object.FindFirstObjectByType<RailRoute>();
            AddCandidates(route.finale);
            EditorSceneManager.SaveScene(scene);
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        AssetDatabase.SaveAssets();
        Debug.Log("Mixed enemies installed; six navigation-checked candidates per encounter variation.");
    }

    static void AddCandidates(RailBlock block)
    {
        if (RailBothSidesAuthoring.HasBothSides(block)) return;
        // Canyon's reviewed encounters occupy several cliff heights. The generic y=0
        // allocator must not replace those authored plateau markers on a catalogue rebuild.
        if (block.id == RailCanyon.Id && block.transform.Find(RailCanyon.RootName) != null) return;
        var graph = CrawlGraph.Load(block.graph);
        var colliders = block.GetComponentsInChildren<Collider>().Where(c => !c.isTrigger && c.gameObject.layer == 6).ToArray();
        var available = Enumerable.Range(0, graph.NodeCount).Select(graph.GetPosition).Where(p =>
            p.y >= -.1f && p.y <= .3f && block.enemyBounds.Contains(p) &&
            colliders.All(c => new[] { .5f, 1.2f, 2f }.All(h =>
                Vector3.Distance(c.ClosestPoint(block.transform.TransformPoint(p + Vector3.up * h)), block.transform.TransformPoint(p + Vector3.up * h)) >= .45f))).ToArray();
        for (int v = 0; v < block.variations.Length; v++)
        {
            var set = block.variations[v];
            var parent = set.enemies[0].parent;
            var selected = new System.Collections.Generic.List<Vector3>();
            for (int i = 0; i < 6; i++)
            {
                var desired = Vector3.Lerp(block.enemyBounds.min, block.enemyBounds.max, (i + 1f) / 7);
                desired.x = block.id == "bridge-finale-v1" ? 25 : 12 + v;
                desired.y = 0;
                var options = available.Where(p => selected.All(q => Vector3.Distance(p, q) >= 4)).OrderBy(p => (p - desired).sqrMagnitude).ToArray();
                if (options.Length == 0) throw new InvalidOperationException("Insufficient clear enemy points: " + block.id);
                selected.Add(options[0]);
            }
            foreach (var old in set.enemies) UnityEngine.Object.DestroyImmediate(old.gameObject);
            set.enemies = selected.Select((p, i) => {
                var t = new GameObject("Enemy candidate " + (i + 1)).transform;
                t.SetParent(parent, false); t.position = block.transform.TransformPoint(p + Vector3.up * .05f); return t;
            }).ToArray();
        }
    }
}
#endif
