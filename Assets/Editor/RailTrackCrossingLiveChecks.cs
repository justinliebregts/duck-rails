#if UNITY_EDITOR
using System;
using System.IO;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Actual motel collision geometry, minion brain, and locomotion.</summary>
[InitializeOnLoad]
public static class RailTrackCrossingLiveChecks
{
    const string Armed = "Rails.Crossing.Offline";
    const string LogPath = "docs/validation/track-crossing/play.txt";
    [Serializable] class Setup { public SceneSetup[] scenes; }
    static Enemy enemy;
    static RailTrain train;
    static Transform target;
    static EnemyManager manager;
    static double entered;
    static int stage;
    static bool started, introducedTrain;
    static RailTrackCrossingLiveChecks()
    { EditorApplication.playModeStateChanged += Changed; EditorApplication.update += Tick; }
    [MenuItem("Duck Rails/Tests/Track Crossings/Motel Play Loop")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new Exception("Exit Play Mode first");
        foreach (var scene in EditorSceneManager.GetSceneManagerSetup())
            if (scene.isLoaded && UnityEngine.SceneManagement.SceneManager.GetSceneByPath(scene.path).isDirty)
                throw new Exception("Save scene edits first");
        Directory.CreateDirectory(Path.GetDirectoryName(LogPath)); File.WriteAllText(LogPath, "");
        SessionState.SetString(Armed + ".setup", JsonUtility.ToJson(new Setup { scenes = EditorSceneManager.GetSceneManagerSetup() }));
        SessionState.SetBool(Armed, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var sentinel = new GameObject("Offline login sentinel"); sentinel.SetActive(false); sentinel.AddComponent<LoginFlow>();
        EditorApplication.isPlaying = true;
    }
    static void Log(string text)
    { File.AppendAllText(LogPath, DateTime.UtcNow.ToString("O") + " " + text + "\n"); Debug.Log("CROSSING PLAY: " + text); }
    static void Changed(PlayModeStateChange change)
    {
        if (!SessionState.GetBool(Armed, false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        { try { SetupWorld(); } catch (Exception e) { Finish("FAIL setup " + e); } }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Armed, false); started = false;
            var setup = JsonUtility.FromJson<Setup>(SessionState.GetString(Armed + ".setup", "{}"));
            if (setup?.scenes != null) EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
        }
    }
    static void SetupWorld()
    {
        Application.runInBackground = true;
        var block = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/roadside-motel-v1.prefab")).GetComponent<RailBlock>();
        train = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RailEnemyBoardingAuthoring.TrainPath)).GetComponent<RailTrain>();
        train.transform.position = new Vector3(0, 1, 120);
        target = new GameObject("Cross-track pursuit target").transform; target.position = new Vector3(6, 1, 70);
        var managerRoot = new GameObject("Motel crossing manager"); managerRoot.SetActive(false);
        manager = managerRoot.AddComponent<EnemyManager>();
        using (var so = new SerializedObject(manager))
        {
            so.FindProperty("graphAsset").objectReferenceValue = block.graph;
            so.FindProperty("surfaceMask").intValue = 1 << 6;
            so.FindProperty("visionMask").intValue = 1 << 6;
            so.FindProperty("maxEnemies").intValue = 0;
            so.FindProperty("fallbackTarget").objectReferenceValue = target;
            so.FindProperty("perceptionInterval").intValue = 1;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        managerRoot.SetActive(true);
        enemy = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/RailZombie.prefab")).GetComponent<Enemy>();
        Restart(-6);
        stage = 0; started = true;
        Log("Actual motel/minion; natural perception across empty track");
    }
    static void Restart(float x)
    {
        int n = manager.Graph.FindNearestGroundNode(new Vector3(x, 0, 70), 2);
        if (n < 0) throw new Exception("No crossing start node");
        enemy.Teleport(manager.Graph.GetPosition(n) + Vector3.up * .02f, Quaternion.LookRotation(target.position - manager.Graph.GetPosition(n)));
        entered = EditorApplication.timeSinceStartup; Physics.SyncTransforms();
    }
    static void Finish(string message) { Log(message); started = false; EditorApplication.isPlaying = false; }
    static void Tick()
    {
        if (!started || !EditorApplication.isPlaying) return;
        try
        {
            double age = EditorApplication.timeSinceStartup - entered;
            if (age > 40) throw new Exception("Timeout stage=" + stage + " pos=" + enemy.Root.position + " state=" + enemy.CurrentState);
            if (stage > 0 && RailTrainObstacle.Contains(enemy.Root.position))
                throw new Exception("Minion entered train occupancy at " + enemy.Root.position);
            if (stage == 2)
            {
                train.transform.position = new Vector3(0, 1, 70 + (float)age * 3);
                Physics.SyncTransforms();
            }
            if (stage == 3 && age > .25 && !introducedTrain)
            {
                train.transform.position = new Vector3(0, 1, 70);
                Physics.SyncTransforms(); introducedTrain = true;
                Log("Train now occupies the previously clear planned crossing");
            }
            if (Vector3.Distance(enemy.Root.position, target.position - Vector3.up) > 1.25f) return;
            if (stage == 0)
            {
                Log("PASS natural minion pursuit crosses empty motel tracks");
                train.transform.position = new Vector3(0, 1, 70);
                // Keep the target known behind the opaque train, isolating navigation
                // from the intentional 1.5-second line-of-sight memory.
                using (var so = new SerializedObject(manager)) { so.FindProperty("visionMask").intValue = 0; so.ApplyModifiedPropertiesWithoutUndo(); }
                Restart(-6); stage = 1;
            }
            else if (stage == 1)
            {
                Log("PASS pursuit detours around parked train without entering its footprint");
                target.position = new Vector3(-6, 1, 70); Restart(6); stage = 2;
            }
            else if (stage == 2)
            {
                Log("PASS opposite-direction pursuit uses crossing released by departing train");
                train.transform.position = new Vector3(0, 1, 120);
                target.position = new Vector3(6, 1, 70); Restart(-6); stage = 3; introducedTrain = false;
            }
            else
            {
                Log("PASS stale path replans around newly occupied crossing");
                Finish("PASS motel crossing / parked-train detour / moving-train release / occupied old path");
            }
        }
        catch (Exception e) { Finish("FAIL " + e.Message); }
    }
}
#endif
