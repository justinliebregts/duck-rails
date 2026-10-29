using System;
using System.Reflection;
using Game.AI;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Batch Play Mode chase: production brain, perception, trails and budgeted manager.
// Copies Minion settings into a test actor; excludes rendering, combat and networking.
public static class EnemyChasePlayChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const int Mask = 1 << 6;
    const string SessionKey = "EnemyChasePlayChecks";
    static EnemyManager manager;
    static Enemy enemy;
    static GroundLocomotion mover;
    static Transform target;
    static int scenario, frame, failures, targetIndex, lastFrame = -1;
    static float began;
    static Vector3 goal;
    static Vector3[] targetRoute;
    static bool acquired, started;

    static readonly Vector3[] Starts = {
        new Vector3(78, 4.5f, 39.5f), new Vector3(74.2f, 4.5f, 41.3f),
        new Vector3(80, 1, 27), new Vector3(98, 13.5f, 15),
        new Vector3(78, 1, 39.5f), new Vector3(74.2f, 1, 41.3f),
        new Vector3(78, 4.5f, 39.5f)
    };
    static readonly Vector3[] Goals = {
        new Vector3(81.431f, 7.95f, 41.81f), new Vector3(81.431f, 7.95f, 41.81f),
        new Vector3(80, 1.1f, 21), new Vector3(102.431f, 16.95f, 16.81f),
        new Vector3(81.431f, 4.45f, 41.81f), new Vector3(81.431f, 4.45f, 41.81f),
        new Vector3(70.8f, .86f, 33.5f)
    };

    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)o.GetType().GetField(name, Private).GetValue(o);

    // Execute without -quit: the Play Mode checks exit Unity after the final case.
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Batch mode only");
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        foreach (var existing in UnityEngine.Object.FindObjectsByType<EnemyManager>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(existing.gameObject);
        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Register() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (!SessionState.GetBool(SessionKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (!started)
            {
                started = true;
                Time.captureDeltaTime = 1f / 60f;
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                UnityEngine.Random.InitState(4517);
                Begin();
            }
            if (lastFrame == Time.frameCount) return;
            lastFrame = Time.frameCount;
            Observe();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(SessionKey, false);
            EditorApplication.Exit(1);
        }
    }

    static void Begin()
    {
        if (scenario >= Starts.Length)
        {
            SessionState.SetBool(SessionKey, false);
            Debug.Log("CHASE DONE failures=" + failures);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
            return;
        }
        var managerObject = new GameObject("Chase manager");
        managerObject.SetActive(false);
        manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "graphAsset", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scenes/Quackville_CrawlGraph.bytes"));
        Set(manager, "surfaceMask", (LayerMask)Mask);
        Set(manager, "visionMask", (LayerMask)Mask);
        Set(manager, "maxEnemies", 0);
        target = new GameObject("Target head").transform;
        goal = Goals[scenario];
        Set(manager, "fallbackTarget", target);
        managerObject.SetActive(true);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieMinion.prefab");
        var sourceEnemy = prefab.GetComponent<Enemy>();
        var sourceRoot = Get<Transform>(sourceEnemy, "root");
        var sourceEye = Get<Transform>(sourceEnemy, "eye");
        var actor = new GameObject("Chasing Minion");
        actor.SetActive(false);
        mover = actor.AddComponent<GroundLocomotion>();
        EditorUtility.CopySerialized(prefab.GetComponent<GroundLocomotion>(), mover);
        enemy = actor.AddComponent<Enemy>();
        EditorUtility.CopySerialized(sourceEnemy, enemy);
        actor.transform.localScale = sourceRoot.lossyScale;
        actor.transform.position = Starts[scenario];
        actor.transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(goal - Starts[scenario], Vector3.up));
        var eye = new GameObject("Eye").transform;
        eye.SetParent(actor.transform, false);
        eye.localPosition = sourceRoot.InverseTransformPoint(sourceEye.position);
        Set(enemy, "root", actor.transform);
        Set(enemy, "eye", eye);
        Set(enemy, "head", eye);
        actor.SetActive(true);
        Physics.SyncTransforms();
        if (mover.Overlaps(enemy.Root.position, Mask)) throw new Exception("Overlapping start " + scenario);
        if (!mover.HasGroundSupport(enemy.Root.position, Mask)) throw new Exception("Unsupported start " + scenario);

        // Only the simulated player is scripted along a route. The enemy gets no
        // supplied waypoints: it must perceive, request, join and follow its own paths.
        var route = new int[128];
        int start = manager.Graph.FindNearestGroundNode(actor.transform.position, 1.5f);
        int end = manager.ResolveGroundNode(goal + Vector3.up * 1.5f);
        var targetFinder = manager.CreateCompanionPathfinder();
        if (!targetFinder.FindPath(start, end, route, out int count, out bool complete, true) || !complete)
            throw new Exception("Incomplete target route " + scenario);
        targetRoute = new Vector3[count];
        for (int i = 0; i < count; i++) targetRoute[i] = manager.Graph.GetPosition(route[i]) + Vector3.up * 1.5f;
        targetIndex = frame = 0;
        acquired = false;
        target.position = actor.transform.position + Vector3.up * 1.5f;
        began = Time.time;
        Debug.Log($"CHASE BEGIN {scenario} start={actor.transform.position} goal={goal} targetRoute={count}");
    }

    static void Observe()
    {
        if (enemy == null) return;
        frame++;
        acquired |= enemy.Seen;
        if (Time.time - began > 1 && targetIndex < targetRoute.Length)
        {
            // The final descent/exit case runs ahead to exercise lost-sight search.
            float speed = scenario == 6 ? 6f : 2.4f;
            target.position = Vector3.MoveTowards(target.position, targetRoute[targetIndex], speed * Time.deltaTime);
            if (Vector3.Distance(target.position, targetRoute[targetIndex]) < .02f) targetIndex++;
        }
        if (frame % 120 == 0)
        {
            int index = Get<int>(mover, "_pathIndex"), count = Get<int>(mover, "_pathCount");
            int[] path = Get<int[]>(mover, "_path");
            string next = index < count ? manager.Graph.GetPosition(path[index]).ToString("F3") : "none";
            Debug.Log($"CHASE {scenario} t={Time.time - began:F2} pos={enemy.Root.position:F3} " +
                $"state={enemy.CurrentState} seen={enemy.Seen} path={index}/{count} pending={mover.WantsPath} " +
                $"recovering={mover.IsRecovering} next={next} steps={mover.StepsTaken}");
        }
        bool success = acquired && targetIndex == targetRoute.Length && Vector3.Distance(enemy.Root.position, goal) < .85f;
        if (!success && Time.time - began <= 28) return;
        Debug.Log($"CHASE RESULT {scenario} success={success} pos={enemy.Root.position} state={enemy.CurrentState}");
        if (!success) failures++;
        UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        UnityEngine.Object.DestroyImmediate(manager.gameObject);
        UnityEngine.Object.DestroyImmediate(target.gameObject);
        scenario++;
        Begin();
    }
}
