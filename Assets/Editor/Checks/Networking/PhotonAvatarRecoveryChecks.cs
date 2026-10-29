using System;
using System.Collections;
using System.Reflection;
using Game.Animation;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in a disposable Unity project: replaces the scene and supplies a synthetic room/clock.
// Exercises production receive, playback, body rig, ragdoll and timer code without a network.
public static class PhotonAvatarRecoveryChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    const double Period = 4294967.296d;
    static readonly FieldInfo NetworkFrame = typeof(PhotonNetwork).GetField("frame", BindingFlags.Static | BindingFlags.NonPublic);
    static readonly FieldInfo NetworkTime = typeof(PhotonNetwork).GetField("frametime", BindingFlags.Static | BindingFlags.NonPublic);
    static readonly string[] RealFields = { "_lastPoseRealtime", "_rebaseFirstRealtime", "_rebaseLastRealtime" };
    static int checks;
    static TestActor owner, stranger;
    sealed class TestActor : Photon.Realtime.Player
    {
        public TestActor(int id) : base("test " + id, id, false) { }
    }
    static FieldInfo Field(object o, string name) => o.GetType().GetField(name, Private);
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Call(object o, string name) => o.GetType().GetMethod(name, Private).Invoke(o, null);
    static Action Bind(object o, string name) => (Action)Delegate.CreateDelegate(typeof(Action), o, o.GetType().GetMethod(name, Private));
    static void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
    static bool Near(double a, double b, double tolerance = 0.002) => Math.Abs(a - b) < tolerance;
    static void Clock(double now) { NetworkFrame.SetValue(null, Time.frameCount); NetworkTime.SetValue(null, now); }
    static int Count(PhotonPlayer p) => Get<IList>(p, "_buf").Count;
    static float X(PhotonPlayer p) => Get<BodyPose>(p, "_lastPose").headPos.x;

    sealed class Fixture : IDisposable
    {
        public readonly PhotonPlayer player;
        public readonly RemotePlayerBody body;
        public readonly Action tick;
        public Fixture(bool ragdoll = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab");
            Check(prefab != null, "Avatar prefab exists");
            var go = Object.Instantiate(prefab);
            go.SetActive(false);
            player = go.GetComponent<PhotonPlayer>();
            Check(player != null, "Avatar script resolves");
            player.photonView.OwnerActorNr = owner.ActorNumber;
            player.photonView.ControllerActorNr = owner.ActorNumber;
            body = Get<RemotePlayerBody>(player, "body");
            Check(body != null && body.Head != null, "Remote body resolves");
            foreach (var limb in go.GetComponentsInChildren<TwoBoneLimb>(true)) limb.CaptureRest();
            if (!ragdoll) Set(player, "deathRagdollPrefab", null);
            tick = Bind(player, "LateUpdate");
        }
        public void Dispose()
        {
            var ragdoll = Get<Ragdoll>(player, "_remoteRagdoll");
            if (ragdoll != null) Object.DestroyImmediate(ragdoll.gameObject);
            Object.DestroyImmediate(player.gameObject);
        }
    }

    static PosePacket Pose(float x) => new PosePacket
    {
        HeadPos = new Vector3(x, 2, 0), HeadRot = Quaternion.identity,
        LHandPos = new Vector3(x - .35f, 1.7f, .2f), LHandRot = Quaternion.identity,
        RHandPos = new Vector3(x + .35f, 1.7f, .2f), RHandRot = Quaternion.identity,
    };

    static void Receive(PhotonPlayer p, double now, double real, double sent, float x, int alive = 0,
        bool dead = false, Photon.Realtime.Player sender = null, bool invalid = false)
    {
        var packet = Pose(x);
        if (invalid) packet.HeadPos.x = float.NaN;
        object[] values = dead
            ? new object[] { packet, true, Vector3.right, Vector3.up, alive }
            : new object[] { packet, false, alive };
        Deliver(p, now, real, sent, values, sender ?? owner);
    }

    static void Deliver(PhotonPlayer p, double now, double real, double sent, object[] values, Photon.Realtime.Player sender)
    {
        Clock(now);
        // Translate the private elapsed-time history into the real Unity clock domain.
        // No production clock injection or test-only gameplay fields are needed.
        double offset = Time.realtimeSinceStartupAsDouble - real;
        foreach (string field in RealFields) Set(p, field, Get<double>(p, field) + offset);
        int timestamp = unchecked((int)(uint)(long)Math.Round(sent * 1000d));
        p.OnPhotonSerializeView(new PhotonStream(false, values), new PhotonMessageInfo(sender, timestamp, p.photonView));
        foreach (string field in RealFields) Set(p, field, Get<double>(p, field) - offset);
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch project only");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            owner = new TestActor(2); stranger = new TestActor(3);
            var room = new Room("avatar regression", new RoomOptions());
            room.AddPlayer(owner); room.AddPlayer(stranger);
            PhotonNetwork.NetworkingClient.CurrentRoom = room;
            FirstPoseAndValidation();
            TimerAndWire();
            Recovery(100, 80);
            Recovery(2600000, 100);
            Recovery(100, 2600000);
            RejectionAndDeath();
            RagdollLifecycle();
            RecoveredDeath();
            CompanionLifeOrdering();
            WrapAndDisable();
            Playback(.08, false, false);
            Playback(.22, false, false);
            Playback(.08, true, false);
            Playback(.08, false, true);
            ReleaseReviewEdges();
            Allocation();
            Debug.Log("PhotonAvatarRecoveryChecks: ALL PASSED (" + checks + " assertions)");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static void FirstPoseAndValidation()
    {
        using var f = new Fixture(); var p = f.player;
        Receive(p, 100, 0, 100, 20, 99, sender: stranger);
        Receive(p, 100, 0, 100, 20, 99, invalid: true);
        Receive(p, 100, 0, 102, 20, 99);
        Check(Count(p) == 0 && !Get<bool>(p, "_hasPose") && p.TimeAliveSeconds == 0, "Bad sender/pose/future cannot initialize avatar");
        Receive(p, 100, 0, 99.92, 10);
        Check(Count(p) == 1 && Near(X(p), 10), "First zero-lifetime pose applied immediately");
        Check(Near(f.body.Head.position.x, 10), "First pose reaches real head rig");
        Clock(100.04); f.tick();
        Check(Near(X(p), 10), "Single sample holds valid pose");
        Receive(p, 100.05, .05, 99.97, 10.025f, 1);
        Clock(100.05); f.tick();
        Check(X(p) >= 10 && X(p) <= 10.0251f && Get<bool>(p, "_playbackStarted"), "Second sample starts bounded interpolation");
        Check(p.TimeAliveSeconds == 1, "Accepted lifetime updates");
        Check(p.photonView.Synchronization == ViewSynchronization.Unreliable
            && p.photonView.ObservedComponents.Count == 1, "Prefab stream configuration unchanged");
        Debug.Log("First pose / input validation PASS");
    }

    static void TimerAndWire()
    {
        using var f = new Fixture(); var p = f.player;
        var timer = (Action<bool, double>)Delegate.CreateDelegate(typeof(Action<bool, double>), p,
            typeof(PhotonPlayer).GetMethod("UpdateAliveClock", Private));
        Clock(0); timer(false, 100);
        Clock(2600000); timer(false, 110.9);
        Check(p.TimeAliveSeconds == 10, "2.6-million network clock jump cannot inflate lifetime");
        Clock(2); timer(false, 111.1);
        Check(p.TimeAliveSeconds == 11, "Network wrap cannot rewind lifetime");
        timer(true, 112); timer(true, 500);
        Check(p.TimeAliveSeconds == 11, "Dead lifetime remains frozen");
        timer(false, 501);
        Check(p.TimeAliveSeconds == 0, "Respawn resets lifetime");
        timer(false, 504.2);
        Check(p.TimeAliveSeconds == 3, "Respawn lifetime advances");
        Set(p, "_ownerClockStarted", false); timer(false, 900);
        timer(false, 902.1);
        Check(p.TimeAliveSeconds == 5, "Authority reacquisition retains received lifetime without counting inactive gap");
        typeof(PhotonView).GetProperty("IsMine").SetValue(p.photonView, true);
        Set(p, "_aliveSince", Time.realtimeSinceStartupAsDouble - 12.2);
        Clock(2600000); Call(p, "Update");
        Check(p.TimeAliveSeconds == 12, "Actual Update uses local elapsed time");
        var stream = new PhotonStream(true, null);
        Set(stream, "writeData", new System.Collections.Generic.List<object>(5));
        p.OnPhotonSerializeView(stream, default);
        var values = stream.ToArray();
        Check(values.Length == 3 && values[0] is PosePacket && values[1] is bool && (int)values[2] == 12,
            "Living wire schema unchanged");
        Check(PosePacket.Serialize(values[0]).Length == 39, "Pose payload remains 39 bytes");
        Check(PhotonSession.StreamRate == 20, "Send rate remains 20 Hz");
        typeof(PhotonView).GetProperty("IsMine").SetValue(p.photonView, false);
        Set(p, "_remoteDead", true);
        Set(stream, "writeData", new System.Collections.Generic.List<object>(5));
        p.OnPhotonSerializeView(stream, default);
        values = stream.ToArray();
        Check(values.Length == 5 && (bool)values[1] && values[2] is Vector3 && values[3] is Vector3
            && (int)values[4] == 12, "Dead wire schema and impulse fields unchanged");
        Call(p, "Update");
        Check(!Get<bool>(p, "_ownerClockStarted"), "Loss of authority invalidates local baseline");
        Debug.Log("Lifetime / wire schema PASS");
    }

    static void Recovery(double original, double corrected)
    {
        using var f = new Fixture(); var p = f.player;
        Receive(p, original, 0, original, 10, 0);
        Receive(p, corrected, .1, corrected - .08, 20, 7, dead: true);
        Check(!p.IsDead && p.TimeAliveSeconds == 0, "Clock discontinuity cannot immediately replay death");
        Receive(p, corrected + 1, 1.1, corrected + .92, 21, 8, dead: true);
        Receive(p, corrected + 1.075, 1.175, corrected + .995, 22, 8, dead: true);
        Check(!p.IsDead && p.TimeAliveSeconds == 0, "Recovery quarantines unconfirmed life state");
        Receive(p, corrected + 1.15, 1.25, corrected + 1.07, 23, 9);
        Check(Count(p) == 1 && p.TimeAliveSeconds == 9 && !p.IsDead, "Fresh advancing sequence replaces blocked history");
        Check(Near(X(p), 10), "Established first pose retained for smooth recovery");
        for (int i = 1; i <= 20; i++)
        {
            double t = i * .05;
            Receive(p, corrected + 1.15 + t, 1.25 + t, corrected + 1.07 + t, 23 + (float)t, 10);
            f.tick();
        }
        Check(X(p) > 23 && X(p) < 24.01f && Count(p) <= 32, "Playback resumes after initial zero-lifetime sample and discontinuity");
        Debug.Log("Zero-lifetime clock recovery PASS: " + original + " -> " + corrected);
    }

    static void RejectionAndDeath()
    {
        using var f = new Fixture(); var p = f.player;
        Receive(p, 100, 0, 99.92, 10, 10);
        Receive(p, 100.05, .05, 99.97, 11, 10, dead: true);
        Check(p.IsDead && Near(X(p), 11), "Death applies immediately");
        Receive(p, 100.1, .1, 99.92, 50, 0);
        Receive(p, 100.1, .1, 99.97, 50, 0);
        Check(p.IsDead && Near(X(p), 11) && p.TimeAliveSeconds == 10, "Older/duplicate alive packets cannot resurrect");
        Receive(p, 100.15, .15, 100.07, 12, 0);
        Check(!p.IsDead && Near(X(p), 12) && p.TimeAliveSeconds == 0, "Respawn applies immediately");
        Receive(p, 102, 2, 99.97, 99, 10, dead: true);
        Check(!p.IsDead && Near(X(p), 12), "Stale death after timeout cannot kill respawned player");
        // A burst of plausible earlier snapshots in one frame cannot authorize rebasing.
        for (int i = 0; i < 10; i++) Receive(p, 99.9, 3, 99.3 + i * .05, 99, 8, dead: true);
        Check(!p.IsDead && Near(X(p), 12) && p.TimeAliveSeconds == 0, "Queued burst cannot rebase death state");
        Receive(p, 100.4, 3.5, 100.32, 13, 1);
        Check(!p.IsDead && p.TimeAliveSeconds == 1, "Newer normal stream resumes after rejected burst");
        Debug.Log("Duplicate / stale / burst / death ordering PASS");
    }

    static void RagdollLifecycle()
    {
        using var f = new Fixture(true); var p = f.player;
        Receive(p, 100, 0, 99.92, 10, 20);
        Receive(p, 100.05, .05, 99.97, 11, 20, dead: true);
        var ragdoll = Get<Ragdoll>(p, "_remoteRagdoll");
        Check(ragdoll != null && ragdoll.gameObject.activeSelf, "Death activates real ragdoll");
        Receive(p, 100.1, .1, 100.02, 11, 20, dead: true);
        Check(Get<Ragdoll>(p, "_remoteRagdoll") == ragdoll, "Repeated death reuses ragdoll");
        Receive(p, 100.15, .15, 100.07, 12, 0);
        Check(!ragdoll.gameObject.activeSelf && !p.IsDead && Near(X(p), 12), "Respawn parks ragdoll and restores pose");
        Receive(p, 100.2, .2, 99.97, 99, 20, dead: true);
        Check(!ragdoll.gameObject.activeSelf && !p.IsDead, "Delayed death leaves ragdoll parked");
        Receive(p, 100.25, .25, 100.17, 13, 0, dead: true);
        Check(Get<Ragdoll>(p, "_remoteRagdoll") == ragdoll && ragdoll.gameObject.activeSelf, "Next death reuses prepared ragdoll");
        Debug.Log("Real ragdoll death / respawn PASS");
    }

    static void WrapAndDisable()
    {
        using var f = new Fixture(); var p = f.player;
        Receive(p, Period - .1, 0, Period - .18, 10, 4);
        Receive(p, .1, .2, .02, 11, 4);
        Check(p.TimeAliveSeconds == 4 && Count(p) == 1, "Uint wrap accepted without stall timeout");
        Receive(p, .15, .25, .07, 12, 4); f.tick();
        Check(Get<bool>(p, "_playbackStarted"), "Playback restarts after uint wrap");
        Receive(p, .2, .3, Period - .18, 99, 40, dead: true);
        Check(!p.IsDead && p.TimeAliveSeconds == 4, "Pre-wrap old death rejected");
        Call(p, "OnDisable");
        Check(Count(p) == 0 && !Get<bool>(p, "_hasPose") && !Get<bool>(p, "_ownerClockStarted"), "Disable clears playback and clock baseline");
        Receive(p, 50, 1, 49.92, 30, 0);
        Check(Near(X(p), 30), "Reenabled avatar applies first pose immediately");
        Debug.Log("Timestamp wrap / disable reset PASS");
    }

    static void RecoveredDeath()
    {
        using var f = new Fixture(true); var p = f.player;
        Receive(p, 100, 0, 100, 10);
        Receive(p, 81, 1.1, 80.92, 11, 4, dead: true);
        Receive(p, 81.075, 1.175, 80.995, 12, 4, dead: true);
        Check(!p.IsDead && Get<Ragdoll>(p, "_remoteRagdoll") == null, "Unconfirmed corrected death cannot spawn corpse");
        Receive(p, 81.15, 1.25, 81.07, 13, 4, dead: true);
        var corpse = Get<Ragdoll>(p, "_remoteRagdoll");
        Check(p.IsDead && corpse != null && corpse.gameObject.activeSelf && Near(X(p), 13), "Confirmed corrected death applies immediately");
        Receive(p, 81.2, 1.3, 81.12, 14, 0);
        Check(!p.IsDead && !corpse.gameObject.activeSelf && Near(X(p), 14), "Respawn after clock recovery works");
        Debug.Log("Death / respawn during timestamp recovery PASS");
    }

    static DuckCompanion AttachCompanion(Fixture f)
    {
        var duck = f.player.gameObject.AddComponent<DuckCompanion>();
        typeof(DuckCompanion).GetProperty("Head").SetValue(duck, f.body.Head);
        typeof(DuckCompanion).GetProperty("LeftHand").SetValue(duck, f.body.LeftHand);
        typeof(DuckCompanion).GetProperty("RightHand").SetValue(duck, f.body.RightHand);
        typeof(DuckCompanion).GetProperty("Dialogue").SetValue(duck, f.player.gameObject.AddComponent<DuckCompanionDialogue>());
        typeof(DuckCompanion).GetProperty("SelfMute").SetValue(duck, f.player.gameObject.AddComponent<DuckCompanionSelfMute>());
        return duck;
    }

    static object[] CompanionSnapshot(DuckCompanion duck, bool dead)
    {
        typeof(DuckCompanion).GetProperty("State").SetValue(duck, dead ? DuckCompanion.Activity.Dead : DuckCompanion.Activity.Following);
        var stream = new PhotonStream(true, null);
        Set(stream, "writeData", new System.Collections.Generic.List<object>(100));
        stream.SendNext(Pose(10)); stream.SendNext(dead);
        if (dead) { stream.SendNext(Vector3.zero); stream.SendNext(Vector3.zero); }
        stream.SendNext(dead ? 8 : 0);
        duck.Serialize(stream);
        return stream.ToArray();
    }

    static void CompanionLifeOrdering()
    {
        using var source = new Fixture(); using var target = new Fixture();
        var sender = AttachCompanion(source); var receiver = AttachCompanion(target);
        var alive = CompanionSnapshot(sender, false);
        var dead = CompanionSnapshot(sender, true);
        Deliver(target.player, 100, 0, 99.92, alive, owner);
        Deliver(target.player, 100.05, .05, 99.97, dead, owner);
        Check(receiver.IsDead && Get<bool>(target.player, "_remoteDead"), "Companion death payload accepted");
        Deliver(target.player, 100.1, .1, 99.92, alive, owner);
        Check(receiver.IsDead && Get<bool>(target.player, "_remoteDead"), "Rejected pose cannot mutate companion life payload");
        Deliver(target.player, 100.15, .15, 100.07, alive, owner);
        Check(!receiver.IsDead && !Get<bool>(target.player, "_remoteDead"), "Companion respawn payload accepted");
        Deliver(target.player, 100.2, .2, 99.97, dead, owner);
        Check(!receiver.IsDead && !Get<bool>(target.player, "_remoteDead"), "Rejected companion death cannot replay");
        Debug.Log("Companion life payload ordering PASS");
    }

    static void Playback(double delay, bool outage, bool hitch)
    {
        using var f = new Fixture(); var p = f.player;
        double nextSend = 0; float previous = 10; int movingFrames = 0;
        for (int frame = 0; frame < 720; frame++)
        {
            double t = frame / 72d;
            if (hitch && t > 3 && t < 3.9) continue;
            while (nextSend + delay <= t)
            {
                if (!outage || nextSend < 3 || nextSend > 5)
                    Receive(p, 100 + t, t, 100 + nextSend, 10 + (float)nextSend * .5f, (int)nextSend);
                nextSend += .05;
            }
            Clock(100 + t); f.tick();
            if (!Get<bool>(p, "_hasPose")) continue;
            float x = X(p);
            Check(!float.IsNaN(x) && x >= previous - .0001f && x <= 10 + t * .5 + .0001, "Playback finite, monotonic, never extrapolates");
            Check(Near(f.body.Head.position.x, x), "Head rig follows interpolated pose");
            Check(Count(p) <= 32, "Pose history bounded");
            if (x > previous + .00001f && t > 7) movingFrames++;
            previous = x;
        }
        Check(movingFrames > 200 && previous > 14.6f, "Smooth movement resumes and catches up");
        Debug.Log("Interpolation PASS: delay=" + delay + " outage=" + outage + " hitch=" + hitch);
    }

    static void Allocation()
    {
        using var f = new Fixture(); var p = f.player;
        Receive(p, 100, 0, 99.92, 10);
        Receive(p, 100.05, .05, 99.97, 10.1f);
        var gate = (Func<double, double, double, bool>)Delegate.CreateDelegate(typeof(Func<double, double, double, bool>), p,
            typeof(PhotonPlayer).GetMethod("AcceptPoseTimestamp", Private));
        var timer = (Action<bool, double>)Delegate.CreateDelegate(typeof(Action<bool, double>), p,
            typeof(PhotonPlayer).GetMethod("UpdateAliveClock", Private));
        Clock(100.05);
        timer(false, 100);
        for (int i = 0; i < 1000; i++) { f.tick(); gate(100, 100.05, .1); timer(false, 100.5); }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 30000; i++) { f.tick(); gate(100, 100.05, .1); timer(false, 100.5); }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(bytes == 0, "Warmed playback/timestamp/timer path allocates zero bytes, got " + bytes);
        Debug.Log("Allocation PASS: " + bytes + " bytes / 30,000 warmed playback + timestamp + timer calls (unchanged displayed second)");

        // PUN supplies decoded/boxed values. Reuse that input to measure our full
        // receiver and playback, excluding transport decoding and changed label text.
        object[] input = { Pose(10), false, 0 };
        var reader = new PhotonStream(false, input);
        IList buffer = Get<IList>(p, "_buf");
        for (int pass = 0; pass < 2; pass++)
        {
            int iterations = pass == 0 ? 1000 : 30000;
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < iterations; i++)
            {
                // Keep packet times in the fixed test clock's valid range. Clearing
                // the preallocated history at each boundary does not allocate.
                int sample = i % 1000;
                if (sample == 0) buffer.Clear();
                reader.SetReadStream(input);
                p.OnPhotonSerializeView(reader, new PhotonMessageInfo(owner, 100000 + sample, p.photonView));
                f.tick();
            }
            bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        }
        Check(bytes == 0, "Warmed full receiver/playback allocates zero bytes, got " + bytes);
        Debug.Log("Full receiver allocation PASS: " + bytes + " bytes / 30,000 accepted packets + playback calls (predecoded input; unchanged displayed second)");
    }

    static void ReleaseReviewEdges()
    {
        int failures = 0;
        foreach (Action scenario in new Action[] { DelayedStream, OwnerCache, CompanionControllerSwitch })
        {
            try { scenario(); }
            catch (Exception e) { failures++; Debug.LogError("Release review: " + scenario.Method.Name + ": " + e.Message); }
        }
        Check(failures == 0, "Release-review edge scenarios failed: " + failures);
    }

    static void DelayedStream()
    {
        using var f = new Fixture(); var p = f.player;
        Receive(p, 100, 0, 98.8, 10);
        Check(Get<bool>(p, "_hasPose") && Near(X(p), 10), "1.2-second delivery delay must not leave a new avatar in T-pose");
        for (int i = 1; i <= 60; i++)
        {
            double t = i * .05;
            Receive(p, 100 + t, t, 98.8 + t, 10 + (float)t * .5f, (int)t);
            f.tick();
        }
        Check(X(p) > 11.3f && p.TimeAliveSeconds == 3, "Delayed but advancing stream keeps moving and updates lifetime");
        Receive(p, 103.05, 3.05, 101.85, 12, 3, dead: true);
        Check(p.IsDead, "Chronologically newer delayed death applies");
        Receive(p, 103.1, 3.1, 101.9, 13, 0);
        Check(!p.IsDead && Near(X(p), 13), "Chronologically newer delayed respawn applies");
        Receive(p, 103.15, 3.15, 101.85, 99, 3, dead: true);
        Check(!p.IsDead && Near(X(p), 13), "Out-of-order delayed death cannot replay");
        Debug.Log("Delayed advancing stream / life ordering PASS");
    }

    static void OwnerCache()
    {
        using var f = new Fixture(); var p = f.player;
        typeof(PhotonView).GetProperty("Owner").SetValue(p.photonView, null);
        Check(p.photonView.OwnerActorNr == owner.ActorNumber, "Actor identity survives a missing cached Owner object");
        Receive(p, 100, 0, 99.92, 10, 1, sender: new TestActor(owner.ActorNumber));
        Check(Get<bool>(p, "_hasPose") && p.TimeAliveSeconds == 1, "Valid actor updates must not depend on cached Player object identity");
        Receive(p, 100.05, .05, 99.97, 99, 99, dead: true, sender: stranger);
        Check(!p.IsDead && p.TimeAliveSeconds == 1, "Foreign actor still rejected with missing Owner cache");
        Debug.Log("Owner actor identity / missing cache PASS");
    }

    static void CompanionControllerSwitch()
    {
        using var source = new Fixture(); using var target = new Fixture();
        var duck = AttachCompanion(source); var peer = AttachCompanion(target);
        var alive = CompanionSnapshot(duck, false); var dead = CompanionSnapshot(duck, true);
        Deliver(target.player, 100, 0, 99.92, alive, owner);
        target.player.photonView.ControllerActorNr = stranger.ActorNumber;
        Deliver(target.player, 100.05, .05, 99.97, dead, owner);
        Check(!peer.IsDead, "Old companion controller cannot replay death after master switch");
        Deliver(target.player, 100.1, .1, 100.02, dead, stranger);
        Check(peer.IsDead, "New companion controller can send death state");
        Deliver(target.player, 100.15, .15, 100.07, alive, stranger);
        Check(!peer.IsDead, "New companion controller can respawn");
        Debug.Log("Companion controller handoff PASS");
    }
}
