using System;
using System.Reflection;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Game.AI;
using Game.Animation;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class SpiderRaiseChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Get(object o, string name) => o.GetType().GetField(name, Private).GetValue(o);
    static void Call(object o, string name) => o.GetType().GetMethod(name, Private).Invoke(o, null);
    static void Set(object o, string name, object value) => o.GetType().GetField(name, Private).SetValue(o, value);
    static int checks;
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }

    sealed class Rig : IDisposable
    {
        public readonly GameObject actor;
        public readonly SpiderGait gait;
        public readonly Enemy enemy;
        public readonly EnemyNetSync sync;
        public readonly PhotonView view;
        public readonly Transform head, body;
        public readonly BoxCollider kill;
        public readonly SpiderGait.LegSlot[] legs;
        public Rig()
        {
            actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSpider.prefab"));
            foreach (var limb in actor.GetComponentsInChildren<TwoBoneLimb>()) limb.CaptureRest();
            foreach (var leg in actor.GetComponentsInChildren<SpiderLeg>()) Call(leg, "Awake");
            enemy = actor.GetComponent<Enemy>(); Call(enemy, "Awake");
            gait = actor.GetComponent<SpiderGait>(); Call(gait, "Awake");
            sync = actor.GetComponent<EnemyNetSync>(); Call(sync, "Awake");
            view = actor.GetComponent<PhotonView>();
            head = (Transform)Get(enemy, "head"); body = (Transform)Get(gait, "body");
            kill = actor.GetComponentInChildren<KillTrigger>().GetComponent<BoxCollider>();
            legs = (SpiderGait.LegSlot[])Get(gait, "legs");
            Set(enemy, "_target", 0); Set(enemy, "_seen", true); Set(enemy, "_hadAuthority", true);
            enemy.SetRemoteState(Enemy.State.Chase);
            gait.Tick(1f / 90);
        }
        public void Tick(float dt) { gait.Tick(dt); enemy.AimHead(dt); }
        public void Settle(int hz = 90) { for (int i = 0; i < hz; i++) Tick(1f / hz); }
        public EnemyPosePacket Capture()
        {
            var stream = new PhotonStream(true, null);
            Set(stream, "writeData", new List<object>(1));
            sync.OnPhotonSerializeView(stream, default);
            return (EnemyPosePacket)stream.ToArray()[0];
        }
        public void Receive(EnemyPosePacket packet, Player sender, int time)
            => sync.OnPhotonSerializeView(new PhotonStream(false, new object[] { packet }), new PhotonMessageInfo(sender, time, view));
        public void Dispose() { Object.DestroyImmediate(actor); }
    }

    static EnemyManager manager;
    static GameObject targetObject;
    static void Target(Vector3 point) => ((Vector3[])Get(manager, "_slotHead"))[0] = point;
    static void Setup()
    {
        if (!Application.isBatchMode) throw new Exception("Isolated batch checks only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = new GameObject("Raise test floor") { layer = 6 };
        floor.transform.position = Vector3.down * .1f;
        floor.AddComponent<BoxCollider>().size = new Vector3(500, .2f, 500);
        var go = new GameObject("Raise test manager"); go.SetActive(false);
        manager = go.AddComponent<EnemyManager>();
        typeof(EnemyManager).GetProperty("Instance").SetValue(null, manager);
        targetObject = new GameObject("Elevated target");
        ((Object[])Get(manager, "_slotOwner"))[0] = targetObject;
        Physics.SyncTransforms();
    }

    public static void Run()
    {
        Setup();
        Behavior(); Geometry(); Codec();
        Debug.Log($"SpiderRaiseChecks PASS: {checks} assertions; proximity/height hysteresis, moving gait, planted reach, head trigger/defensive hits, lifecycle, 22-byte codec, zero allocations.");
    }

    static void Behavior()
    {
        using var r = new Rig();
        Check(r.kill.transform.parent == r.head, "Kill trigger must be a direct head child");
        Vector3 mouth = r.kill.transform.TransformPoint(r.kill.center);
        Check(Vector3.Dot(mouth - r.head.position, r.head.forward) > .15f
            && r.enemy.OverlapsSwing(mouth, mouth, 0f), "Visible mouth accepts a defensive hit");
        foreach (Vector3 p in new[] { new Vector3(0, .7f, .8f), new Vector3(0, 1f, .8f), new Vector3(0, 2f, 1.001f), new Vector3(0, -2f, .5f), new Vector3(float.NaN, 2, 0), new Vector3(0, float.PositiveInfinity, 0) })
        {
            Target(p); r.Settle(); Check(r.gait.RaiseHeight == 0 && r.gait.RaiseLevel == 0, "Invalid/low/far target raised: " + p);
        }
        Target(new Vector3(0, 1.5f, 1f)); r.Tick(.05f);
        Check(r.gait.RaiseLevel == 7 && r.gait.RaiseHeight > 0 && r.gait.RaiseHeight < .3f, "Close target begins smooth rise");
        r.Settle(); float raised = r.gait.RaiseHeight;
        Debug.Log($"SPIDER RAISE standing lift={raised:F3}, head={r.head.position:F3}");
        Check(raised > .3f && raised <= .5f, "Useful but bounded extension");
        Target(new Vector3(0, 1.5f, 1.49f)); r.Settle(); Check(r.gait.RaiseLevel == 7, "Keep stance inside exit radius");
        Target(new Vector3(0, 1.5f, 1.501f)); r.Tick(.02f);
        Check(r.gait.RaiseLevel == 0 && r.gait.RaiseHeight > 0, "Outside exit radius lowers smoothly");
        r.Settle(); Check(r.gait.RaiseHeight == 0, "Lower completes");
        Target(new Vector3(0, 1.5f, 1.1f)); r.Settle(); Check(r.gait.RaiseLevel == 0, "Outside entry radius cannot restart");
        Target(new Vector3(0, 1.1f, .8f)); r.Settle(); Check(r.gait.RaiseLevel == 2 && r.gait.RaiseHeight < .2f, "Partial elevation gets partial lift");
        Target(new Vector3(0, 50f, .8f)); r.Settle(); Check(r.gait.RaiseHeight <= .5f, "Very high target capped");
        Target(new Vector3(0, float.MaxValue, .8f)); r.gait.Tick(.1f); Check(r.gait.RaiseLevel == 7, "Finite extreme target cannot overflow stance quantization");
        Target(new Vector3(0, .7f, .8f)); r.Settle(); Check(r.gait.RaiseHeight == 0, "Player drops down");
        Target(new Vector3(0, 1.5f, .8f));
        LayerMask mask = (LayerMask)Get(r.gait, "surfaceMask"); Set(r.gait, "surfaceMask", (LayerMask)0);
        r.Settle(); Check(r.gait.RaiseLevel == 0, "No floor support must not request standing"); Set(r.gait, "surfaceMask", mask);
        r.actor.transform.rotation = Quaternion.Euler(0, 0, 15); r.Settle(); Check(r.gait.RaiseHeight > .1f, "Gentle floor slope remains usable");
        r.actor.transform.rotation = Quaternion.identity;
        foreach (Enemy.State state in new[] { Enemy.State.Wander, Enemy.State.Search, Enemy.State.Retreat })
        {
            Target(new Vector3(0, 1.5f, .8f)); r.enemy.SetRemoteState(state); r.Settle();
            Check(r.gait.RaiseLevel == 0 && r.gait.RaiseHeight == 0, "No rise outside chase: " + state);
        }
        r.enemy.SetRemoteState(Enemy.State.Chase);
        foreach (string flag in new[] { "_stunned", "_remoteKnocked", "_ragdollActive" })
        {
            r.Settle(); Set(r.enemy, flag, true); Check(r.gait.RaiseLevel == 0, "Disabled state cannot publish rise: " + flag);
            r.gait.Tick(.5f); Check(r.gait.RaiseHeight == 0, "Disabled state clears target"); Set(r.enemy, flag, false);
        }
        r.Settle(); r.enemy.IsDormant = true; Check(r.gait.RaiseLevel == 0, "Dormant pose"); r.gait.Tick(.5f); r.enemy.IsDormant = false;
        foreach (Quaternion rotation in new[] { Quaternion.Euler(0, 0, 90), Quaternion.Euler(180, 0, 0) })
        {
            r.actor.transform.rotation = Quaternion.identity; r.Settle(); r.actor.transform.rotation = rotation; r.Tick(.01f);
            Check(r.gait.RaiseHeight == 0, "Wall/ceiling immediately restore crawling height");
        }
        r.actor.transform.rotation = Quaternion.identity; r.Settle();
        r.enemy.Teleport(Vector3.right * 30, Quaternion.identity); Check(r.gait.RaiseHeight == 0 && r.gait.RaiseLevel == 0, "Teleport clears raise");
        r.Tick(.02f);
        foreach (var slot in r.legs) Check(Mathf.Abs(slot.planted.x - 30) < 1f && Mathf.Abs(slot.planted.y) < .01f, "Teleport replants at destination");
        r.enemy.Teleport(Vector3.zero, Quaternion.identity);
        r.enemy.SetRemoteState(Enemy.State.Chase); Set(r.enemy, "_target", 0); r.Settle();
        r.sync.ResetRemotePose(Vector3.zero, Quaternion.identity); Check(r.gait.RaiseHeight == 0, "Reset network pose clears raise");
        r.Settle(); r.sync.OnControllerChange(null, null); Check(r.gait.RaiseHeight == 0 && r.gait.RaiseLevel == 0, "Handoff clears stale raise");
        r.Settle(); r.actor.SetActive(false); Call(r.gait, "OnDisable"); Check(r.gait.RaiseHeight == 0, "Disable clears raise"); r.actor.SetActive(true);
        Set(r.enemy, "_target", -1); r.Settle(); Check(r.gait.RaiseHeight == 0, "Missing/dead target clears raise");
    }

    static void Geometry()
    {
        foreach (int hz in new[] { 30, 72, 90, 144 })
        {
            Target(new Vector3(0, 1.5f, .8f));
            using var r = new Rig();
            var transforms = r.actor.GetComponentsInChildren<Transform>();
            var scales = new Vector3[transforms.Length];
            for (int i = 0; i < scales.Length; i++) scales[i] = transforms[i].localScale;
            r.Settle(hz);
            Check(r.enemy.OverlapsSwing(r.head.position, r.head.position, .05f), "Raised head accepts defensive hit");
            Vector3 oldTrigger = r.kill.transform.TransformPoint(r.kill.center);
            r.head.localRotation *= Quaternion.Euler(-35, 40, 0);
            Check((oldTrigger - r.kill.transform.TransformPoint(r.kill.center)).sqrMagnitude > .001f, "Trigger follows head aiming");
            float minimumLift = 10, maximumToeError = 0;
            for (int frame = 0; frame < hz * 3; frame++)
            {
                Vector3 next = new Vector3(0, 0, frame * (4.8f / hz));
                r.enemy.SetRootPose(next, Quaternion.identity); Target(next + new Vector3(0, 1.5f, .8f));
                r.Tick(1f / hz);
                Check(r.actor.transform.position == next, "Raising changed locomotion root/speed");
                Check(r.gait.RaiseHeight >= 0 && r.gait.RaiseHeight <= .50001f, "Lift range at " + hz);
                minimumLift = Mathf.Min(minimumLift, r.gait.RaiseHeight);
                foreach (var slot in r.legs)
                {
                    if (slot.swinging) continue;
                    Check(Mathf.Abs(slot.planted.y) < .005f, "Planted foot target left the floor");
                    maximumToeError = Mathf.Max(maximumToeError, Vector3.Distance(slot.leg.FootWorld, slot.planted));
                }
            }
            for (int i = 0; i < scales.Length; i++) Check(scales[i] == transforms[i].localScale, "Leg/bone scale changed");
            Debug.Log($"SPIDER RUN {hz}Hz minLift={minimumLift:F3} maxPlantedToeError={maximumToeError:F3}");
            Check(minimumLift > .05f, "Running stance repeatedly lost all extra height");
            Check(maximumToeError < .06f, "Raised stance detached planted feet");
            r.enemy.SetRootPose(Vector3.zero, Quaternion.identity); Target(new Vector3(0, 1.5f, .8f)); r.Settle(hz);
            for (int frame = 0; frame < hz * 2; frame++)
            {
                float angle = frame * 2.4f / hz;
                Vector3 next = new Vector3(2 * (1 - Mathf.Cos(angle)), 0, 2 * Mathf.Sin(angle));
                Quaternion rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 0);
                r.enemy.SetRootPose(next, rotation); Target(next + rotation * Vector3.forward * .8f + Vector3.up * 1.5f);
                r.Tick(1f / hz);
                Check(r.actor.transform.position == next && r.gait.RaiseHeight >= 0 && r.gait.RaiseHeight <= .50001f,
                    "Turning pursuit changed movement or produced invalid lift");
            }
            r.enemy.SetRootPose(Vector3.zero, Quaternion.identity); Target(new Vector3(0, 1.5f, .8f)); r.Settle(hz);
            for (int i = 0; i < 50; i++) r.Tick(1f / hz);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) r.Tick(1f / hz);
            Check(GC.GetAllocatedBytesForCurrentThread() == before, "Warmed raised gait allocated managed memory");
        }
        Target(new Vector3(0, 1.5f, .8f));
        using var contact = new Rig();
        var capsule = new GameObject("Elevated player body").AddComponent<CapsuleCollider>();
        capsule.radius = .15f; capsule.height = .5f;
        capsule.transform.position = new Vector3(0, 1.275f, .74f); // Authored body center is .225m below a 1.5m head.
        bool Touch() => Physics.ComputePenetration(contact.kill, contact.kill.transform.position, contact.kill.transform.rotation,
            capsule, capsule.transform.position, capsule.transform.rotation, out _, out _);
        Set(contact.gait, "raiseNearTarget", false); contact.Settle(); Check(!Touch(), "Low spider should not reach elevated body");
        Set(contact.gait, "raiseNearTarget", true); contact.Settle(); Check(Touch(), "Raised head trigger should reach elevated body");
        Object.DestroyImmediate(capsule.gameObject);
    }

    static void Codec()
    {
        var packet = new EnemyPosePacket();
        for (int level = 0; level < 8; level++)
        for (int state = 0; state < 4; state++)
        {
            packet.Set(new Vector3(1, 2, 3), Quaternion.Euler(10, 20, 30), (byte)state, true, 42, -71);
            packet.SpiderRaise = (byte)level;
            packet.Flags |= EnemyPosePacket.JumpFlag | EnemyPosePacket.KnockFlag;
            byte[] wire = EnemyPosePacket.Serialize(packet);
            Check(wire.Length == EnemyPosePacket.PacketBytes, "Packet length changed");
            var copy = (EnemyPosePacket)EnemyPosePacket.Deserialize(wire);
            Check(copy.SpiderRaise == level && copy.State == state && copy.Knocked && copy.HasAim
                && (copy.Flags & EnemyPosePacket.JumpFlag) != 0, "Raise flags corrupt other pose fields");
            Check(Mathf.Abs(copy.AimYaw - 42) < .01f && Mathf.Abs(copy.AimPitch + 71) < .01f, "Head aim wire precision");
            EnemyPosePacket.Return(copy);
        }
        packet.Set(Vector3.zero, Quaternion.identity, 0, false, 0, 0);
        Check(packet.SpiderRaise == 0, "Reused packet retains stale stance");
        for (int i = 0; i < 100; i++) EnemyPosePacket.Return((EnemyPosePacket)EnemyPosePacket.Deserialize(EnemyPosePacket.Serialize(packet)));
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) EnemyPosePacket.Return((EnemyPosePacket)EnemyPosePacket.Deserialize(EnemyPosePacket.Serialize(packet)));
        Check(GC.GetAllocatedBytesForCurrentThread() == start, "Pose codec allocated after warming");
    }

    public static void Regressions()
    {
        Setup();
        SpiderRoofLipChecks.Run();
        Debug.Log(SpiderWebChecks.Evaluate());
        Debug.Log(SpiderWebChecks.StressSafety());
        SpiderFlareChecks.Run();
        EnemySchedulingChecks.Run();
        ZombieSmasherJumpChecks.Run();
        MeleeGraceChecks.Run();
        HitBloodChecks.Run();
        Debug.Log("SpiderRaiseChecks REGRESSIONS PASS");
    }

    public static void FinalVerification()
    {
        Run();
        Benchmark();
        RunCloud();
    }

    public static void Benchmark()
    {
        Setup(); Target(new Vector3(0, 1.5f, .8f));
        var rigs = new Rig[20];
        try
        {
            for (int i = 0; i < rigs.Length; i++) rigs[i] = new Rig();
            foreach (bool raised in new[] { false, true })
            {
                foreach (var rig in rigs) { Set(rig.gait, "raiseNearTarget", raised); rig.Settle(); }
                var watch = new System.Diagnostics.Stopwatch();
                long before = GC.GetAllocatedBytesForCurrentThread();
                watch.Start();
                for (int frame = 0; frame < 1000; frame++)
                    for (int i = 0; i < rigs.Length; i++) rigs[i].Tick(1f / 90);
                watch.Stop();
                long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
                Check(bytes == 0, "Twenty-spider gait batch allocated " + bytes);
                Debug.Log($"SPIDER BENCH 20 {(raised ? "raised" : "crawl")} gaits: {watch.Elapsed.TotalMilliseconds / 1000:F4} ms/batch, {bytes} B, 1000 warmed editor batches (excludes rendering/network/native allocations).");
            }
        }
        finally { foreach (var rig in rigs) rig?.Dispose(); }
    }

    public static void Inspect()
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieSpider.prefab"));
        try
        {
            var gait = actor.GetComponent<SpiderGait>();
            var enemy = actor.GetComponent<Enemy>();
            var body = (Transform)Get(gait, "body");
            var head = (Transform)Get(enemy, "head");
            foreach (var limb in actor.GetComponentsInChildren<TwoBoneLimb>()) limb.CaptureRest();
            foreach (var leg in actor.GetComponentsInChildren<SpiderLeg>()) Call(leg, "Awake");
            Call(gait, "Awake");
            gait.Tick(.1f);
            Debug.Log($"SPIDER RIG body={body.position:F3} head={head.position:F3} headRotation={head.rotation.eulerAngles:F3}");
            foreach (var leg in actor.GetComponentsInChildren<SpiderLeg>())
                Debug.Log($"SPIDER LEG hip={leg.HipWorld:F3} toe={leg.FootWorld:F3} reach={leg.Reach:F3}");
            var box = actor.GetComponentInChildren<KillTrigger>().GetComponent<BoxCollider>();
            Debug.Log($"SPIDER KILL center={box.transform.TransformPoint(box.center):F3} size={box.size:F3} headLocalCenter={head.InverseTransformPoint(box.transform.TransformPoint(box.center)):F3}");
        }
        finally { Object.DestroyImmediate(actor); }
    }
}
