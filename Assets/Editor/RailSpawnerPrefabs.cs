#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class RailSpawnerPrefabs
{
    public static string Path(RailSpawnKind kind) => "Assets/DuckRails/Authoring/" + kind + "SpawnMarker.prefab";
    public static Texture2D Icon(RailSpawnKind kind) => EditorGUIUtility.IconContent("sv_label_" +
        (kind == RailSpawnKind.Coal ? 3 : kind == RailSpawnKind.Gold ? 4 : 6)).image as Texture2D;

    public static void CreateAssets()
    {
        foreach (RailSpawnKind kind in Enum.GetValues(typeof(RailSpawnKind)))
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(Path(kind)) != null) continue;
            var root = new GameObject(kind + " Spawner");
            try
            {
                root.AddComponent<RailSpawnMarker>().kind = kind;
                if (kind == RailSpawnKind.Coal) root.AddComponent<CoalSpawner>();
                else if (kind == RailSpawnKind.Gold) root.AddComponent<RailGoldSpawner>();
                else root.AddComponent<RailEnemySpawner>();
                EditorGUIUtility.SetIconForObject(root, Icon(kind));
                PrefabUtility.SaveAsPrefabAsset(root, Path(kind));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }
    }

    public static GameObject Instantiate(RailSpawnKind kind, UnityEngine.SceneManagement.Scene scene)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Path(kind));
        if (prefab == null) throw new InvalidOperationException("Missing spawner prefab: " + Path(kind));
        return (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
    }
}
#endif
