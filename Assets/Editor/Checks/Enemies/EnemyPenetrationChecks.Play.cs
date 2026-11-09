using System;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class EnemyPenetrationChecks
{
    const string PlayKey = "EnemyPenetrationChecks.Play";
    static readonly int[] PlayRates = { 30, 90, 144, 2 };
    static int playIndex, playFrame = -1;
    static Enemy playEnemy;
    static EnemyManager playManager;
    static Collider destroyedHelper;
    static bool playStarted, awaitingDestroy;
    static float playBegan;

    public static void RunPlay()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch only.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = new GameObject("Play recovery ground") { layer = 6 };
        floor.AddComponent<MeshCollider>().sharedMesh = FloorMesh();
        var guard = new GameObject("Offline test guard"); guard.SetActive(false); guard.AddComponent<LoginFlow>();
        SessionState.SetBool(PlayKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod] static void RegisterPlayChecks() => EditorApplication.update += PollPlayChecks;

    static void PollPlayChecks()
    {
        if (!SessionState.GetBool(PlayKey, false) || !EditorApplication.isPlaying || EditorApplication.isCompiling) return;
        try
        {
            if (playFrame == Time.frameCount) return;
            playFrame = Time.frameCount;
            if (!playStarted)
            {
                playStarted = true;
                QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
                var manager = new GameObject("Play recovery manager"); manager.SetActive(false);
                playManager = manager.AddComponent<EnemyManager>(); Set(playManager, "surfaceMask", (LayerMask)Mask);
                new GameObject("Test listener").AddComponent<AudioListener>();
                BeginPlayCase();
                return;
            }
            if (awaitingDestroy)
            {
                Check(destroyedHelper == null, "Play Mode actor destruction must remove the cached helper");
                awaitingDestroy = false; playIndex++;
                BeginPlayCase();
                return;
            }
            var ground = playEnemy.GetComponent<GroundLocomotion>();
            var climb = playEnemy.GetComponent<ClimbLocomotion>();
            var jump = playEnemy.GetComponent<ZombieSmasherJump>();
            bool fallingCase = playIndex >= 48;
            if (jump != null) jump.TickJump(playEnemy, playManager, Time.deltaTime);
            else playEnemy.TickAuthority(playManager, Time.deltaTime, false);
            Physics.SyncTransforms();
            bool overlap = ground != null ? ground.Overlaps(playEnemy.Root.position, Mask) : climb.Overlaps(playEnemy.Root.position, Mask);
            Collider helper = ground != null ? Get<CapsuleCollider>(ground, "_queryCapsule") : Get<SphereCollider>(climb, "_querySphere");
            Check(helper == null || (!helper.enabled && helper.isTrigger), "No helper may remain active across a Play Mode frame");
            if (!fallingCase) Check(!overlap, "Shallow penetration must resolve on the first tick: " + playIndex);
            Check(Time.time - playBegan < 5, "Play recovery timed out: " + playIndex);
            if (overlap || Mathf.Abs(playEnemy.Root.position.y) > .005f || playEnemy.IsJumping) return;
            if (!fallingCase) Check(helper != null, "Embedded fixture must exercise the corrected recovery helper");
            Debug.Log($"RECOVERY PLAY PASS case={playIndex} actor={playEnemy.name} hz={1f / Time.captureDeltaTime:F0} fall={fallingCase} position={playEnemy.Root.position:F5}");
            destroyedHelper = helper;
            Object.Destroy(playEnemy.gameObject); playEnemy = null;
            awaitingDestroy = true;
        }
        catch (Exception e)
        {
            Debug.LogException(e); SessionState.SetBool(PlayKey, false); EditorApplication.Exit(1);
        }
    }

    static void BeginPlayCase()
    {
        if (playIndex == 52)
        {
            Debug.Log("EnemyPenetrationChecks PLAY: ALL PASSED (48 embedded production-prefab cases, 4 fast-fall cases; frame-boundary helper retirement and destruction)");
            SessionState.SetBool(PlayKey, false); EditorApplication.Exit(0); return;
        }
        bool fall = playIndex >= 48;
        int rate = fall ? playIndex - 48 : playIndex / 3 % PlayRates.Length;
        string type = fall ? "Smasher" : Types[playIndex / 12];
        Time.captureDeltaTime = 1f / PlayRates[rate];
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/Zombie" + type + ".prefab");
        playEnemy = Object.Instantiate(prefab).GetComponent<Enemy>();
        float height = fall ? 3 : -new[] { .03f, .08f, .12f }[playIndex % 3];
        playEnemy.Teleport(Vector3.up * height, Quaternion.identity);
        Set(playEnemy, "_hadAuthority", true);
        // Exercise the normal hit/retreat recovery entry point for non-jumpers.
        if (playEnemy.GetComponent<ZombieSmasherJump>() == null)
        { Set(playEnemy, "_retreatActive", true); Set(playEnemy, "_retreatRemaining", 2f); }
        playBegan = Time.time;
        Physics.SyncTransforms();
    }
}
