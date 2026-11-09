using System;
using System.Collections.Generic;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Batch physics and synthetic Photon transport checks; never saves scenes or connects to a server.</summary>
public static class SmasherTallBuildingChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    const int Mask = 1 << 6;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, P).SetValue(o, v);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, P).GetValue(o);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, P).Invoke(o, args);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
    sealed class Actor : Player { public Actor(int id, bool local = false) : base("jump test", id, local) { } }

    public static void Run()
    {
        Check(Application.isBatchMode, "Batch mode only");
        var failures = new List<string>();
        foreach (Action test in new Action[] { PrefabAndNetwork, ActualPoliceRoof,
            ZombieSmasherTraversalChecks.Run, ZombieSmasherJumpChecks.Run, SmasherRetreatJumpChecks.Run,
            EnemyStairChecks.Run, EnemyStairChecks.SmasherCheckRegressions,
            GroundRecoveryChecks.SharedFallRecovery, GroundRecoveryChecks.HitRecovery })
        {
            try { test(); }
            catch (Exception e) { failures.Add(test.Method.DeclaringType.Name + "." + test.Method.Name); Debug.LogException(e); }
        }
        Check(failures.Count == 0, "Failed suites: " + string.Join(", ", failures));
        Debug.Log("SmasherTallBuildingChecks: ALL PASSED");
    }

    internal static Enemy MakeActor(out ZombieSmasherJump jump)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSmasher.prefab");
        var go = new GameObject("Smasher production motor");
        var ground = go.AddComponent<GroundLocomotion>();
        EditorUtility.CopySerialized(prefab.GetComponent<GroundLocomotion>(), ground);
        var root = Get<Transform>(prefab.GetComponent<Enemy>(), "root");
        go.transform.localScale = root != null ? root.lossyScale : prefab.transform.lossyScale;
        var enemy = go.AddComponent<Enemy>();
        jump = go.AddComponent<ZombieSmasherJump>();
        EditorUtility.CopySerialized(prefab.GetComponent<ZombieSmasherJump>(), jump);
        Call(enemy, "Awake"); Call(jump, "Awake");
        return enemy;
    }

    internal static EnemyManager Manager()
    {
        var go = new GameObject("Inactive test manager"); go.SetActive(false);
        var manager = go.AddComponent<EnemyManager>(); Set(manager, "surfaceMask", (LayerMask)Mask);
        return manager;
    }

    static void Box(Vector3 center, Vector3 size)
    {
        var go = new GameObject("Surface"); go.layer = 6; go.transform.position = center;
        go.AddComponent<BoxCollider>().size = size;
    }

    static void Prepare(Enemy enemy, ZombieSmasherJump jump, Vector3 start, Vector3 target)
    {
        enemy.Teleport(start, Quaternion.identity);
        enemy.SetRemoteState(Enemy.State.Chase);
        Set(enemy, "_lastSeenPos", target + Vector3.up * 1.7f);
        // Synchronous fixtures do not advance Time.time; test cadence separately.
        Set(jump, "_checkArmed", true); Set(jump, "_nextCheck", Time.time);
        Physics.SyncTransforms();
    }

    static void PrefabAndNetwork()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Box(new Vector3(0, -.5f, 0), new Vector3(100, 1, 100));
        Box(new Vector3(14, 4.8f, 0), new Vector3(5, 9.6f, 8));
        var enemy = MakeActor(out var jump);
        var manager = Manager();
        Check(Get<float>(jump, "maxJumpHeight") == 12f && Get<float>(jump, "maxJumpDistance") == 16f
            && Get<float>(jump, "jumpDuration") == 1.3f && Get<float>(jump, "arcClearance") == 1.25f,
            "Production prefab tuning");
        Prepare(enemy, jump, Vector3.zero, new Vector3(14, 9.6f, 0));
        var plan = (Func<Enemy, LayerMask, bool>)Delegate.CreateDelegate(typeof(Func<Enemy, LayerMask, bool>), jump, "TryPlan");
        Check(plan(enemy, Mask), "Long jump onto a Police-height roof is reachable");
        for (int i = 0; i < 20; i++) plan(enemy, Mask);
        long before = GC.GetAllocatedBytesForCurrentThread();
        long time = System.Diagnostics.Stopwatch.GetTimestamp();
        bool allPlanned = true;
        for (int i = 0; i < 200; i++) allPlanned &= plan(enemy, Mask);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        double ms = (System.Diagnostics.Stopwatch.GetTimestamp() - time) * 1000d / System.Diagnostics.Stopwatch.Frequency;
        Check(allPlanned && allocated == 0, "Tall-roof planning must allocate zero managed bytes: " + allocated);
        Debug.Log($"TALL ROOF PROFILE checks=200 allocated={allocated} mean={ms / 200:F3}ms");

        var remote = MakeActor(out var remoteJump);
        var ownerNet = enemy.gameObject.AddComponent<EnemyNetSync>();
        var remoteNet = remote.gameObject.AddComponent<EnemyNetSync>();
        Call(ownerNet, "Awake"); Call(remoteNet, "Awake"); Call(enemy, "Awake"); Call(remote, "Awake");
        var client = PhotonNetwork.NetworkingClient;
        var oldRoom = client.CurrentRoom; var oldLocal = client.LocalPlayer; var oldState = client.State;
        try
        {
            var owner = new Actor(1); var observer = new Actor(2, true);
            var room = new Room("synthetic smasher jump", new RoomOptions());
            room.LoadBalancingClient = client; room.AddPlayer(owner); room.AddPlayer(observer);
            client.CurrentRoom = room;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, observer);
            client.State = ClientState.Joined;
            ownerNet.photonView.ControllerActorNr = 1; remoteNet.photonView.ControllerActorNr = 1;
            typeof(PhotonView).GetProperty("IsMine").SetValue(ownerNet.photonView, true);
            Check(enemy.HasAuthority && !remote.HasAuthority, "Owner/observer authority fixture");
            Prepare(enemy, jump, Vector3.zero, new Vector3(14, 9.6f, 0));
            int landings = 0; jump.Landed += () => landings++;
            float peak = 0; byte[] stale = null;
            for (int tick = 0; tick < 180; tick++)
            {
                if (tick == 0 || jump.IsJumping) jump.TickJump(enemy, manager, 1f / 60);
                peak = Mathf.Max(peak, enemy.Root.position.y);
                if (tick % 6 == 0 || !jump.IsJumping)
                {
                    var write = new PhotonStream(true, null);
                    Call(write, "SetWriteStream", new List<object>(), 0);
                    ownerNet.OnPhotonSerializeView(write, default);
                    var packet = (EnemyPosePacket)write.ToArray()[0];
                    byte[] bytes = EnemyPosePacket.Serialize(packet);
                    Check(bytes.Length == 21, "Pose wire size remains 21 bytes");
                    Check(((packet.Flags & EnemyPosePacket.JumpFlag) != 0) == jump.IsJumping, "Airborne flag matches flight");
                    var copy = (EnemyPosePacket)EnemyPosePacket.Deserialize(bytes);
                    Check(Vector3.Distance(copy.Position, enemy.Root.position) < .0001f, "Tall pose survives wire round-trip");
                    remoteNet.OnPhotonSerializeView(new PhotonStream(false, new object[] { copy }),
                        new PhotonMessageInfo(owner, 1000 + tick * 17, remoteNet.photonView));
                    Check(Get<bool>(remote, "_airborne") == jump.IsJumping, "Remote airborne animation follows packet");
                    if (jump.IsJumping) stale = (byte[])bytes.Clone();
                }
                Check(!remoteJump.TickJump(remote, manager, 1f / 60), "Observer never runs local jump physics");
                remote.ApplyRemote(1f / 60);
                Check(remote.Root.position.y >= -.01f && remote.Root.position.y <= 12.01f, "Remote height stays bounded");
            }
            Check(peak > 9.6f && peak <= 12.01f && landings == 1, "Tall jump lands exactly once below height cap");
            Check(Mathf.Abs(enemy.Root.position.y - 9.6f) < .05f, "Owner lands on tall roof");
            Check(Vector3.Distance(remote.Root.position, enemy.Root.position) < .01f, "Remote settles on same roof");
            remoteNet.OnPhotonSerializeView(new PhotonStream(false, new object[] { EnemyPosePacket.Deserialize(stale) }),
                new PhotonMessageInfo(owner, 1000, remoteNet.photonView));
            Check(!Get<bool>(remote, "_airborne"), "Delayed airborne packet cannot undo landing");
            remoteNet.OnPhotonSerializeView(new PhotonStream(false, new object[] { EnemyPosePacket.Deserialize(stale) }),
                new PhotonMessageInfo(observer, 9000, remoteNet.photonView));
            Check(!Get<bool>(remote, "_airborne"), "Non-controller packet rejected");

            // Taking authority midair must discard any old arc and fall onto support.
            remote.Teleport(new Vector3(14, 11, 0), Quaternion.identity);
            remoteNet.photonView.ControllerActorNr = 2;
            remoteNet.OnControllerChange(observer, owner);
            Check(remote.HasAuthority, "Observer can become authority");
            for (int i = 0; i < 120; i++) remote.TickAuthority(manager, 1f / 60, false);
            Check(!remoteJump.IsJumping && Mathf.Abs(remote.Root.position.y - 9.6f) < .1f,
                "New authority safely lands from tall midair pose");
            Debug.Log("TALL ROOF NETWORK PASS: 10Hz wire round-trip, remote motion, landing, stale/foreign packets, authority handoff");
        }
        finally
        {
            Object.DestroyImmediate(enemy.gameObject); Object.DestroyImmediate(remote.gameObject);
            client.CurrentRoom = oldRoom;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, oldLocal);
            client.State = oldState;
        }
    }

    static void ActualPoliceRoof()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Quackville.unity", OpenSceneMode.Single);
        Physics.SyncTransforms();
        var police = GameObject.Find("Police_Station");
        Check(police != null, "Police building exists in Quackville");
        Bounds bounds = police.GetComponent<Collider>().bounds;
        var enemy = MakeActor(out var jump); var manager = Manager();
        var ground = enemy.GetComponent<GroundLocomotion>();
        var plan = (Func<Enemy, LayerMask, bool>)Delegate.CreateDelegate(typeof(Func<Enemy, LayerMask, bool>), jump, "TryPlan");
        int successes = 0;
        // Sample both building axes, from street approaches to inset roof spots.
        foreach (Vector3 direction in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
        foreach (float offset in new[] { -3f, 0f, 3f })
        {
            Vector3 side = Vector3.Cross(direction, Vector3.up) * offset;
            float extent = Mathf.Abs(direction.x) > .5f ? bounds.extents.x : bounds.extents.z;
            Vector3 edge = bounds.center + direction * extent + side;
            Vector3 outside = edge + direction * 4f; outside.y = bounds.max.y + 5;
            Vector3 inside = edge - direction * 3f; inside.y = outside.y;
            if (!Physics.Raycast(outside, Vector3.down, out RaycastHit street, 50, Mask)
                || !Physics.Raycast(inside, Vector3.down, out RaycastHit roof, 50, Mask)) continue;
            float rise = roof.point.y - street.point.y;
            if (rise < 5 || rise > 11.5f) continue;
            Vector3 start = ground.RootFromCapsuleBottom(ground.CapsuleBottomOnSurface(street.point, street.normal));
            Prepare(enemy, jump, start, roof.point);
            if (!plan(enemy, Mask)) { Debug.Log($"POLICE APPROACH BLOCKED start={start} roof={roof.point} rise={rise:F2}"); continue; }
            int landings = 0;
            Action landed = () => landings++; jump.Landed += landed;
            bool launched = jump.TickJump(enemy, manager, 1f / 60);
            for (int i = 0; i < 240 && jump.IsJumping; i++)
            {
                jump.TickJump(enemy, manager, 1f / 60);
                Check(enemy.Root.position.y <= start.y + 12.01f, "Police jump respects height cap");
            }
            jump.Landed -= landed;
            Check(launched && !jump.IsJumping && landings == 1
                && Mathf.Abs(enemy.Root.position.y - roof.point.y) < .15f
                && ground.HasGroundSupport(enemy.Root.position, Mask), "Actual Police roof landing failed");
            successes++;
            Debug.Log($"POLICE ROOF PASS start={start} roof={roof.point} rise={rise:F2} landed={enemy.Root.position}");
        }
        Check(successes >= 4, "At least four real Police roof approaches must land; got " + successes);
        Object.DestroyImmediate(enemy.gameObject);
        Debug.Log($"ACTUAL POLICE ROOF PASS approaches={successes} bounds={bounds}");
    }
}
