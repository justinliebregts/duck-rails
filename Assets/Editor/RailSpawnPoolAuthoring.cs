#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailSpawnPoolAuthoring
{
    [MenuItem("Duck Rails/Authoring/Consolidate Legacy Spawn Variations")]
    public static void MigrateAssets()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        int count = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/DuckRails/Blocks", "Assets/DuckRails" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (asset == null || asset.GetComponent<RailBlock>() == null) continue;
            if (stage != null && path == stage.assetPath)
            {
                var block = stage.prefabContentsRoot.GetComponent<RailBlock>();
                if (block == null || block.spawnCandidatesMigrated) continue;
                Undo.RecordObject(block, "Consolidate spawn candidates");
                RailSpawnPool.Migrate(block);
                EditorUtility.SetDirty(block);
                EditorSceneManager.MarkSceneDirty(stage.scene);
                count++;
                continue; // Preserve unsaved user work; save the open prefab normally.
            }
            if (asset.GetComponent<RailBlock>().spawnCandidatesMigrated) continue;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var block = root.GetComponent<RailBlock>();
                // The starting station is intentionally safe and has no encounter loot.
                if (path == "Assets/DuckRails/StartingStation.prefab")
                    block.coalCount = block.goldCount = block.enemyCount = 0;
                RailSpawnPool.Migrate(block);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                count++;
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        Debug.Log("SPAWN POOLS: consolidated " + count + " blocks; existing marker transforms preserved. Open prefab changes use normal save/undo.");
    }
}
#endif
