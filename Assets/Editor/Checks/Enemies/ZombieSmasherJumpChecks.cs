using System;
using System.Reflection;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Run with Unity -batchmode -executeMethod ZombieSmasherJumpChecks.Run -quit.</summary>
public static class ZombieSmasherJumpChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static object Call(object target, string name, params object[] args) => target.GetType().GetMethod(name, Private).Invoke(target, args);
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception("Jump check failed: " + message);
    }

    public static void HitRetreatPoseHistory()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box("Floor", new Vector3(0, -.5f, 0), new Vector3(30, 1, 30));
        Box("Obstacle", new Vector3(0, 1, 0), new Vector3(1, 2, 1));
        var actor = new GameObject("Smasher hit history");
        actor.transform.position = new Vector3(-2, 0, 0);
        var ground = actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>();
        var jump = actor.AddComponent<ZombieSmasherJump>();
        Call(enemy, "Awake"); Call(jump, "Awake");
        Set(enemy, "_hadAuthority", true);
        var managerObject = new GameObject("Manager"); managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        Physics.SyncTransforms();
        Check(!ground.ValidateSmasherPose(enemy, manager.SurfaceMask), "initial pose is safe");
        Call(enemy, "BeginRetreat", manager, Vector3.forward);
        // Supported movement around an obstacle while hit recovery owns movement.
        foreach (Vector3 point in new[] { new Vector3(-2, 0, 2), new Vector3(2, 0, 2), new Vector3(2, 0, 0) })
        {
            enemy.SetRootPose(point, Quaternion.identity);
            enemy.TickAuthority(manager, .02f, false);
            Check(Vector3.Distance(actor.transform.position, point) < .001f, "retreat keeps valid pose");
        }
        Call(enemy, "ClearMeleeShove");
        enemy.SetRemoteState(Enemy.State.Wander);
        Vector3 before = actor.transform.position;
        jump.TickJump(enemy, manager, .02f);
        Check(Vector3.Distance(before, actor.transform.position) < .001f,
            "resuming jump controller after retreat must not teleport to a stale safe pose");
        Check(!jump.IsJumping, "retreat does not launch a jump");
        Debug.Log("HitRetreatPoseHistory: ALL PASSED");
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run these checks in batch mode in an empty scene.");
        HitRetreatPoseHistory();
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box("Ground", new Vector3(0, -0.5f, 0), new Vector3(40, 1, 40));
        GameObject roof = Box("Roof", new Vector3(4, 1.5f, 0), new Vector3(4, 3, 4));
        var actor = new GameObject("Smasher test");
        var ground = actor.AddComponent<GroundLocomotion>();
        var enemy = actor.AddComponent<Enemy>();
        var jump = actor.AddComponent<ZombieSmasherJump>();
        Call(enemy, "Awake");
        Call(jump, "Awake");
        int landings = 0;
        jump.Landed += () =>
        {
            landings++;
            Check(!jump.IsJumping, "landing notification occurs after jump ends");
            Check(Mathf.Abs(actor.transform.position.y - 3f) < 0.1f || Mathf.Abs(actor.transform.position.y) < 0.1f,
                "landing pose committed before notification");
        };
        var managerObject = new GameObject("Inactive test manager");
        managerObject.SetActive(false);
        var manager = managerObject.AddComponent<EnemyManager>();
        Set(manager, "surfaceMask", (LayerMask)(1 << 6));
        Set(enemy, "_lastSeenPos", new Vector3(4, 4.7f, 0));
        enemy.SetRemoteState(Enemy.State.Chase);
        Physics.SyncTransforms();
        Check((bool)Call(jump, "TryPlan", enemy, manager.SurfaceMask), "one-storey roof reachable");
        for (int i = 0; i <= 100; i++)
            Check(((Vector3)Call(jump, "Arc", i / 100f)).y <= 5.001f, "arc respects maximum height");

        Set(jump, "maxJumpHeight", 2f);
        Check(!(bool)Call(jump, "TryPlan", enemy, manager.SurfaceMask), "too-high roof rejected");
        Set(jump, "maxJumpHeight", 5f);
        Set(jump, "maxJumpDistance", 1f);
        Check(!(bool)Call(jump, "TryPlan", enemy, manager.SurfaceMask), "out-of-range roof rejected");
        Set(jump, "maxJumpDistance", 9f);

        GameObject ceiling = Box("Low ceiling", new Vector3(2, 4.1f, 0), new Vector3(12, 0.2f, 12));
        Physics.SyncTransforms();
        Check(!(bool)Call(jump, "TryPlan", enemy, manager.SurfaceMask), "obstructed arc rejected");
        UnityEngine.Object.DestroyImmediate(ceiling);
        Physics.SyncTransforms();

        Check((bool)Call(jump, "TryPlan", enemy, manager.SurfaceMask), "clean baseline jump planned");
        Vector3 baselineEnd = (Vector3)Call(jump, "Arc", 1f);
        // Simulate the state left behind by a hit on the previous flight.
        Set(jump, "_airNudge", Vector3.forward * 2f);
        Set(jump, "_airNudgeFrom", 0.5f);
        Set(jump, "_fallDrift", Vector3.right);
        Check(jump.TickJump(enemy, manager, 0.3f) && jump.IsJumping, "jump takes over ground movement");
        Check(Vector3.Distance((Vector3)Call(jump, "Arc", 1f), baselineEnd) < 0.001f,
            "previous hit does not change the next jump destination");
        Check((Vector3)typeof(ZombieSmasherJump).GetField("_fallDrift", Private).GetValue(jump) == Vector3.zero,
            "previous fall drift cleared on takeoff");
        Check(jump.TickJump(enemy, manager, 0.3f) && jump.IsJumping, "still airborne before duration");
        Check(jump.TickJump(enemy, manager, 0.41f) && !jump.IsJumping, "lands at configured duration");
        Check(Mathf.Abs(actor.transform.position.y - 3f) < 0.01f, "lands on roof");
        Check(ground.PathDone && !ground.WantsPath, "old ground path invalidated");
        Check(!jump.TickJump(enemy, manager, 0.1f), "cooldown releases normal movement");
        Check(landings == 1, "one notification per landing, none on cooldown");

        actor.transform.position = Vector3.zero;
        jump.ResetJump();
        Set(jump, "jumpDuration", 0.3f);
        Check(jump.TickJump(enemy, manager, 0.31f) && !jump.IsJumping, "short duration and large timestep land safely");
        Check(Mathf.Abs(actor.transform.position.y - 3f) < 0.01f, "fast jump lands on roof");

        actor.transform.position = Vector3.zero;
        jump.ResetJump();
        Set(jump, "jumpDuration", 0.9f);
        Check(jump.TickJump(enemy, manager, 0.3f) && jump.IsJumping, "second jump begins");
        UnityEngine.Object.DestroyImmediate(roof);
        Physics.SyncTransforms();
        for (int i = 0; i < 150 && jump.IsJumping; i++) jump.TickJump(enemy, manager, 0.02f);
        Check(!jump.IsJumping && Mathf.Abs(actor.transform.position.y) < 0.1f, "missing landing falls safely to ground");
        Check(landings == 3, "normal, fast and fallback landings each notify once");
        jump.ResetJump();
        Check(landings == 3, "reset does not play a landing sound");

        // Reproduce walking off a roof through the ground mover, with no active
        // pursuit/path and a long cooldown. None of these may prevent gravity.
        roof = Box("Exit roof", new Vector3(4, 1.5f, 0), new Vector3(4, 3, 4));
        Physics.SyncTransforms();
        actor.transform.position = new Vector3(5, 3, 0);
        ground.Reset();
        enemy.SetRemoteState(Enemy.State.Wander);
        Set(jump, "_nextCheck", Time.time + 100f);
        Check(!jump.TickJump(enemy, manager, 0.02f), "standing on roof does not fall");
        bool fell = false;
        for (int i = 0; i < 200; i++)
        {
            if (jump.TickJump(enemy, manager, 0.02f)) fell = true;
            else if (!fell)
                actor.transform.position = (Vector3)Call(ground, "MoveWithWalls", actor.transform.position,
                    Vector3.right * 0.1f, manager.SurfaceMask);
            if (fell && !jump.IsJumping) break;
        }
        Check(fell && !jump.IsJumping && Mathf.Abs(actor.transform.position.y) < 0.1f,
            "walking off roof falls to ground despite cooldown and no chase");
        Check(landings == 4, "roof exit plays landing once");
        Check(!jump.TickJump(enemy, manager, 0.02f), "grounded after roof exit without repeated falls");

        // An intermediate surface must catch the fall before the world floor.
        var lowerRoof = Box("Lower roof", new Vector3(9, 2.5f, 0), new Vector3(4, 1, 4));
        Physics.SyncTransforms();
        actor.transform.position = new Vector3(9, 6, 0);
        jump.ResetJump();
        ground.Reset();
        for (int i = 0; i < 200; i++)
        {
            jump.TickJump(enemy, manager, 0.02f);
            if (!jump.IsJumping) break;
        }
        Check(!jump.IsJumping && Mathf.Abs(actor.transform.position.y - 3f) < 0.1f,
            "fall lands on nearest supporting surface instead of passing through to floor");
        UnityEngine.Object.DestroyImmediate(lowerRoof);

        var packet = new EnemyPosePacket();
        packet.Set(Vector3.zero, Quaternion.identity, (byte)Enemy.State.Chase, true, 10, 20);
        packet.Flags |= EnemyPosePacket.JumpFlag;
        var copy = (EnemyPosePacket)EnemyPosePacket.Deserialize(EnemyPosePacket.Serialize(packet));
        Check(copy.State == (byte)Enemy.State.Chase && copy.HasAim && (copy.Flags & EnemyPosePacket.JumpFlag) != 0,
            "jump flag survives serialization without changing chase or aim");
        EnemyPosePacket.Return(copy);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSmasher.prefab");
        Check(prefab != null && prefab.GetComponent<ZombieSmasherJump>() != null, "prefab has jump component");
        Check(prefab.GetComponent<Game.Audio.SoundEmitter>() == null, "no separate landing SoundEmitter");
        var sfx = new SerializedObject(prefab.GetComponent<ZombieSmasherSFX>());
        var sound = AssetDatabase.LoadAssetAtPath<Game.Audio.SoundEvent>("Assets/Audio/Events/ZombieSmasher_Land.asset");
        Check(sound != null && sfx.FindProperty("landingSound").objectReferenceValue == sound, "SFX references landing event");
        var soundData = new SerializedObject(sound);
        Check(soundData.FindProperty("clips").GetArrayElementAtIndex(0).objectReferenceValue ==
            AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Enemies/ZombieSmasher/ZombieSmasher_Land.ogg"),
            "landing event references imported clip");
        Debug.Log("ZombieSmasherJumpChecks: ALL PASSED");
    }

    static GameObject Box(string name, Vector3 center, Vector3 size)
    {
        var box = new GameObject(name);
        box.layer = 6;
        box.transform.position = center;
        box.AddComponent<BoxCollider>().size = size;
        return box;
    }
}
