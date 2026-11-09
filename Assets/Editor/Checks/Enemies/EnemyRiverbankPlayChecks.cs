using System;
using System.IO;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Isolated Play Mode regression on the shipped riverbank colliders and crawl graph.
/// Run with -batchmode -nographics -executeMethod EnemyRiverbankPlayChecks.Run (without -quit).
/// Exercises every mover without jumps, perception or respawns masking a traversal failure.
/// </summary>
public static class EnemyRiverbankPlayChecks
{
    const string SessionKey = nameof(EnemyRiverbankPlayChecks);
    const int Mask = 1 << 6;
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly string[] Enemies = { "Bomber", "Smasher", "Minion", "Spider" };
    static readonly string[] Speeds = { "slow", "walk", "chase", "retreat" };
    static readonly int[] Rates = { 30, 60, 72, 80, 90, 120, 144 };
    // Mesh-local transects: the spawn-bus bank and three unobstructed stretches.
    static readonly Vector2[] Banks = {
        new Vector2(42.2f, -19.5f), new Vector2(0, -19.5f),
        new Vector2(0, -32.5f), new Vector2(-25, -32.5f)
    };
    static int index, failures, lastFrame = -1;
    static bool started;
    static string reportPath;
    static EnemyManager manager;
    static Enemy enemy;
    static IEnemyLocomotion mover;
    static GroundLocomotion ground;
    static ClimbLocomotion climb;
    static MeshCollider terrain;
    static Vector3 goal, previous, progressAnchor;
    static float began, stationary, timeout, contactAllowance;
    static Result current;

    [Serializable] public class Result
    {
        public string enemy, speedMode, direction, outcome;
        public int bank, hz, recoveryFrames, unsupportedFrames;
        public float speed, seconds, maxStationary, travel, maxStep;
        public Vector3 start, goal, end;
    }

    static T Get<T>(object o, string field) => (T)o.GetType().GetField(field, Private).GetValue(o);
    static void Set(object o, string field, object value) => o.GetType().GetField(field, Private).SetValue(o, value);
    static int Total => Enemies.Length * Speeds.Length * Rates.Length * Banks.Length * 2;

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch mode only.");
        var main = EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var world = EditorSceneManager.OpenScene("Assets/Scenes/Quackville.unity", OpenSceneMode.Additive);
        var colliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        var fixture = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(fixture);
        foreach (Collider source in colliders)
        {
            if (!source.enabled || source.isTrigger || source.gameObject.layer != 6) continue;
            var copy = new GameObject(source.name) { layer = 6 };
            copy.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            copy.transform.localScale = source.transform.lossyScale;
            EditorUtility.CopySerialized(source, copy.AddComponent(source.GetType()));
        }
        EditorSceneManager.CloseScene(main, true);
        EditorSceneManager.CloseScene(world, true);
        // Suppress the editor's automatic sign-in without starting a login flow.
        var offline = new GameObject("Offline test guard"); offline.SetActive(false);
        offline.AddComponent<LoginFlow>();
        new GameObject("Test listener").AddComponent<AudioListener>();
        EditorSceneManager.playModeStartScene = null;
        SessionState.SetBool(SessionKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod] static void Register() => EditorApplication.update += Poll;

    static void Poll()
    {
        if (!SessionState.GetBool(SessionKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (!started)
            {
                started = true;
                reportPath = Path.GetFullPath("Logs/EnemyRiverbankChecks.jsonl");
                Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
                File.WriteAllText(reportPath, "");
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = -1;
                foreach (var mesh in UnityEngine.Object.FindObjectsByType<MeshCollider>(FindObjectsSortMode.None))
                    if (mesh.name == "Terrain_and_Mining_Caves") { terrain = mesh; break; }
                if (terrain == null) throw new Exception("Town terrain is missing.");
                var go = new GameObject("Traversal manager"); go.SetActive(false);
                manager = go.AddComponent<EnemyManager>();
                Set(manager, "graphAsset", AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scenes/Quackville_CrawlGraph.bytes"));
                Set(manager, "surfaceMask", (LayerMask)Mask); Set(manager, "visionMask", (LayerMask)Mask);
                Set(manager, "maxEnemies", 0);
                go.SetActive(true); manager.enabled = false;
                if (manager.Graph == null) throw new Exception("Crawl graph failed to load.");
                Begin();
            }
            if (lastFrame == Time.frameCount || enemy == null) return;
            lastFrame = Time.frameCount;
            Tick();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            SessionState.SetBool(SessionKey, false);
            EditorApplication.Exit(2);
        }
    }

    static Vector3 Floor(float x, float z)
    {
        var origin = terrain.transform.TransformPoint(new Vector3(x, 25, z));
        if (!terrain.Raycast(new Ray(origin, Vector3.down), out RaycastHit hit, 100))
            throw new Exception("Missing terrain at " + origin);
        return hit.point;
    }

    static void Begin()
    {
        if (index >= Total)
        {
            Debug.Log($"EnemyRiverbankPlayChecks: {Total - failures}/{Total} PASSED; failures={failures}; report={reportPath}");
            SessionState.SetBool(SessionKey, false);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
            return;
        }
        int value = index;
        bool down = value % 2 == 0; value /= 2;
        int bank = value % Banks.Length; value /= Banks.Length;
        int hz = Rates[value % Rates.Length]; value /= Rates.Length;
        int speedMode = value % Speeds.Length, type = value / Speeds.Length;
        Time.captureDeltaTime = 1f / hz;
        UnityEngine.Random.InitState(4517 + index);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Zombie" + Enemies[type] + ".prefab");
        var actor = UnityEngine.Object.Instantiate(prefab);
        enemy = actor.GetComponent<Enemy>(); mover = enemy.Locomotion;
        ground = actor.GetComponent<GroundLocomotion>(); climb = actor.GetComponent<ClimbLocomotion>();
        // Crawlers commit short ledge landings in one move, bounded by three
        // collision radii in TryStepDown/TryMountOntoTop. That is not a respawn.
        contactAllowance = climb == null ? .4f : Mathf.Max(.4f, 3f *
            (float)typeof(ClimbLocomotion).GetMethod("WorldRadius", Private).Invoke(climb, null));
        float walk = Get<float>(mover, "walkSpeed");
        float speed = speedMode == 0 ? walk * .3f : speedMode == 1 ? walk
            : speedMode == 2 ? Get<float>(mover, "chaseSpeed") : enemy.RetreatSpeed;
        // Search uses chaseSpeed; only this test instance is adjusted. The full
        // production collision shape, scale and path follower are preserved.
        Set(mover, "chaseSpeed", speed);
        Vector2 section = Banks[bank];
        Vector3 start = Floor(section.x, down ? section.y : -26) + Vector3.up * .03f;
        goal = Floor(section.x, down ? -26 : section.y) + Vector3.up * .03f;
        enemy.Teleport(start, Quaternion.LookRotation(Vector3.ProjectOnPlane(goal - start, Vector3.up)));
        Set(enemy, "_hadAuthority", true);
        enemy.SetRemoteState(Enemy.State.Search);
        Set(enemy, "_lastSeenPos", goal + Vector3.up * 1.5f);
        Physics.SyncTransforms();
        current = new Result { enemy = Enemies[type], speedMode = Speeds[speedMode], speed = speed, hz = hz,
            bank = bank, direction = down ? "down" : "up", start = start, goal = goal };
        began = Time.time; stationary = 0; previous = progressAnchor = start;
        timeout = Mathf.Max(25, Vector3.Distance(start, goal) / speed * 4);
        if (Overlap() || !Supported()) { Finish("invalid-start"); return; }
        mover.ForcePathToWorld(manager, goal + Vector3.up * 1.5f);
    }

    static bool Overlap() => ground != null ? ground.Overlaps(enemy.Root.position, Mask) : climb.Overlaps(enemy.Root.position, Mask);
    static bool Supported() => ground != null ? ground.HasGroundSupport(enemy.Root.position, Mask) : climb.HasSurfaceSupport(enemy.Root.position, Mask);

    static void Tick()
    {
        float dt = Time.deltaTime;
        if (mover.WantsPath) mover.RunPathRequest(manager);
        mover.TickMove(enemy, manager, dt);
        Physics.SyncTransforms();
        Vector3 position = enemy.Root.position;
        float step = Vector3.Distance(position, previous);
        current.maxStep = Mathf.Max(current.maxStep, step);
        current.travel += step; previous = position;
        if (step > current.speed * dt + contactAllowance) { Finish("position-jump"); return; }
        if (Overlap()) { Finish("overlap"); return; }
        if (!Supported()) current.unsupportedFrames++;
        bool recovering = ground != null ? ground.IsRecovering : Get<float>(climb, "_escapeTimer") > 0;
        if (recovering) current.recoveryFrames++;
        if (Vector3.Distance(position, progressAnchor) >= .2f) { progressAnchor = position; stationary = 0; }
        else stationary += dt;
        current.maxStationary = Mathf.Max(current.maxStationary, stationary);
        bool reached = Vector3.ProjectOnPlane(position - goal, Vector3.up).magnitude < .65f
            && Mathf.Abs(position.y - goal.y) < .1f;
        if (reached) Finish(current.recoveryFrames == 0 ? "passed" : "required-recovery");
        else if (Time.time - began > timeout) Finish("timeout");
    }

    static void Finish(string outcome)
    {
        current.outcome = outcome; current.seconds = Time.time - began; current.end = enemy.Root.position;
        if (outcome != "passed") failures++;
        File.AppendAllText(reportPath, JsonUtility.ToJson(current) + "\n");
        if (outcome != "passed" || index % 56 == 55)
            Debug.Log($"RIVER {index + 1}/{Total} " + JsonUtility.ToJson(current));
        UnityEngine.Object.DestroyImmediate(enemy.gameObject); enemy = null;
        index++; Begin();
    }
}
