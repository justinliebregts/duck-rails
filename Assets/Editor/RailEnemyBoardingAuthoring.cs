#if UNITY_EDITOR
using System;
using System.IO;
using Game.AI;
using Game.AI.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class RailEnemyBoardingAuthoring
{
    public const string TrainPath = "Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab";
    public const string GraphPath = "Assets/DuckRails/Train/TrainCrawlGraph.bytes";
    [MenuItem("Duck Rails/Authoring/Bake Train Enemy Navigation")]
    public static void Install()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before baking.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                throw new InvalidOperationException("Save scene changes before baking train navigation.");
        var setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var train = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(TrainPath));
            train.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var nav = train.GetComponent<RailTrainNavigation>() ?? train.AddComponent<RailTrainNavigation>();
            var bakeRoot = new GameObject("Train bake settings");
            var baker = bakeRoot.AddComponent<CrawlGraphBaker>();
            baker.transform.position = new Vector3(0, 1, 0);
            baker.size = new Vector3(5, 4, 11); baker.cellSize = .25f; baker.agentRadius = .35f;
            baker.surfaceMask = 1 << 6; baker.allowCeilings = false; baker.minIslandNodes = 4;
            var seed = new GameObject("Deck seed").transform; seed.position = new Vector3(0, .1f, -3);
            baker.seeds = new[] { seed };
            if (!File.Exists(GraphPath)) File.WriteAllBytes(GraphPath, Array.Empty<byte>());
            AssetDatabase.ImportAsset(GraphPath);
            baker.graphAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(GraphPath);
            Physics.SyncTransforms();
            if (!CrawlGraphBake.Bake(baker, false)) throw new Exception("Train crawlgraph bake failed");
            nav.bakedGraph = AssetDatabase.LoadAssetAtPath<TextAsset>(GraphPath);
            var graph = CrawlGraph.Load(nav.bakedGraph);
            if (graph == null) throw new Exception("Train graph missing");
            for (int opening = 0; opening < RailTrainNavigation.OpeningCount; opening++)
                if (graph.FindNearestGroundNode(nav.Landing(opening), .8f) < 0)
                    throw new Exception("Train graph has no " + RailTrainNavigation.OpeningName(opening) + " boarding landing");
            PrefabUtility.SaveAsPrefabAsset(train, TrainPath);
            Debug.Log("TRAIN BOARDING BAKE: " + graph.NodeCount + " nodes, " + graph.EdgeCount + " edges");
        }
        finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
        string enemyPath = "Assets/Resources/Rails/RailZombie.prefab";
        var enemy = PrefabUtility.LoadPrefabContents(enemyPath);
        try
        {
            if (enemy.GetComponent<RailEnemyBoarding>() == null) enemy.AddComponent<RailEnemyBoarding>();
            PrefabUtility.SaveAsPrefabAsset(enemy, enemyPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(enemy); }
        AssetDatabase.SaveAssets();
    }
}
#endif
