#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RailTrackCrossingAuthoring
{
    public const string Evidence = "docs/validation/track-crossing";
    static void Guard()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first");
        foreach (var s in EditorSceneManager.GetSceneManagerSetup())
            if (s.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(s.path).isDirty)
                throw new Exception("Save scene changes first");
    }
    public static void ClearRailExclusion(RailBlock block)
    {
        block.enemySurfaceRailExclusion = default;
        foreach (var blocker in block.GetComponentsInChildren<CrawlGraphBlocker>(true))
            if (blocker.name.IndexOf("railway navigation exclusion", StringComparison.OrdinalIgnoreCase) >= 0)
                Object.DestroyImmediate(blocker.gameObject);
    }
    [MenuItem("Duck Rails/Authoring/Track Crossings/Bake Motel")]
    public static void BakeMotel() => Bake(false);
    [MenuItem("Duck Rails/Authoring/Track Crossings/Bake All Blocks")]
    public static void BakeAll() => Bake(true);
    static void Bake(bool all)
    {
        Guard();
        var setup = EditorSceneManager.GetSceneManagerSetup();
        Directory.CreateDirectory(Evidence);
        try
        {
            var paths = all ? AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks" })
                .Select(AssetDatabase.GUIDToAssetPath).ToArray()
                : new[] { "Assets/DuckRails/Blocks/roadside-motel-v1.prefab" };
            foreach (var path in paths)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (asset.GetComponent<RailBlock>() == null) continue;
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var root = Object.Instantiate(asset); root.name = asset.name;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var block = root.GetComponent<RailBlock>();
                ClearRailExclusion(block);
                var baker = root.GetComponentInChildren<CrawlGraphBaker>(true);
                if (baker == null) throw new Exception("No baker: " + path);
                Physics.SyncTransforms();
                if (!CrawlGraphBake.Bake(baker, false)) throw new Exception("Bake failed: " + path);
                block.graph = baker.graphAsset;
                PrefabUtility.SaveAsPrefabAsset(root, path);
                string line = DateTime.UtcNow.ToString("O") + " BAKED " + block.id + " nodes=" + CrawlGraph.Load(block.graph).NodeCount;
                File.AppendAllText(Evidence + "/bakes.txt", line + "\n"); Debug.Log(line);
            }
            if (all)
            {
                EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
                var route = Object.FindFirstObjectByType<RailRoute>();
                if (route != null && route.finale != null)
                {
                    ClearRailExclusion(route.finale);
                    EditorUtility.SetDirty(route.finale);
                    EditorSceneManager.SaveScene(route.gameObject.scene);
                }
            }
            AssetDatabase.SaveAssets();
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
    }
}
#endif
