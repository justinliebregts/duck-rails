using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.LowLevel;
using Object = UnityEngine.Object;

// Lifecycle/allocation edges complement the real player-loop interaction regression.
public static class HeadPivotSynchronizationChecks
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    static readonly Action Publish = (Action)Delegate.CreateDelegate(typeof(Action),
        typeof(NewPlayer).GetMethod("RestoreMovementPoses", Private));
    static readonly Action Render = (Action)Delegate.CreateDelegate(typeof(Action),
        typeof(NewPlayer).GetMethod("RestoreZeroStepRenderedPoses", Private));
    static void Flush() { Publish(); Render(); }
    static int checks;
    static void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
    static void Near(Vector3 actual, Vector3 expected, string message) =>
        Check(Vector3.Distance(actual, expected) < .0001f, message + ": " + actual + " vs " + expected);
    static void Configure(NewPlayer p)
    {
        p.rb.interpolation = RigidbodyInterpolation.Interpolate;
        p.rb.position = new Vector3(2, 3, 4);
        p.transform.position = new Vector3(1, 3, 4); // deliberately older displayed pose
        p.rb.linearVelocity = Vector3.right * 4;
    }
    static Vector3 PhysicsHead(NewPlayer p) => p.rb.position + p.rb.rotation *
        (Quaternion.Inverse(p.transform.rotation) * (p.headCollider.transform.position - p.transform.position));
    static bool Queued(NewPlayer p) => (bool)typeof(NewPlayer).GetField("movementPoseQueued", Private).GetValue(p);
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        checks = 0; Flush();
        var mode = Physics.simulationMode; var autoSync = Physics.autoSyncTransforms;
        var oldPlayer = GR.Player.instance; var oldVR = NewPlayer.instance; var oldHeli = HelicopterManager.instance;
        var services = new GameObject("Synchronization test services"); services.SetActive(false);
        GR.Player.instance = services.AddComponent<GR.Player>(); HelicopterManager.instance = null;
        Physics.simulationMode = SimulationMode.Script; Physics.autoSyncTransforms = false;
        try
        {
            ContactAnchorSeeds();
            using (var rig = new MovementTestRig())
            {
                var p = rig.p; Configure(p);
                p.ApplyPassengerDisplacement(Vector3.zero);
                Check(!Queued(p), "Zero carry scheduled a pose write");
                Near(p.rb.position, new Vector3(2, 3, 4), "Zero carry changed physics");
                p.ApplyPassengerDisplacement(Vector3.right * .000001f);
                Check(p.rb.position.x > 2f, "Small nonzero carry was discarded");
                Flush(); Configure(p);

                var delta = new Vector3(.1f, .2f, .3f);
                p.ApplyPassengerDisplacement(delta);
                Near(p.rb.position, new Vector3(2, 3, 4) + delta, "Carry copied displayed pose to physics");
                Near(p.transform.position, new Vector3(1, 3, 4) + delta, "Carry changed displayed displacement");
                Vector3 head = p.headCollider.transform.position, physicsHead = PhysicsHead(p);
                p.Turn(45); p.Turn(-45); p.Turn(0);
                Near(p.headCollider.transform.position, head, "Multiple turns orbited displayed head");
                Near(PhysicsHead(p), physicsHead, "Multiple turns orbited physics head");
                p.ApplyPassengerDisplacement(-delta);
                Vector3 displayed = p.transform.position, physical = p.rb.position;
                Quaternion displayedRotation = p.transform.rotation, physicalRotation = p.rb.rotation;
                NewPlayer.SyncTransformsPreservingMovementPose();
                Near(p.transform.position, displayed, "Global sync changed displayed pose");
                Near(p.rb.position, physical, "Global sync discarded physical travel");
                Check(Quaternion.Angle(p.transform.rotation, displayedRotation) < .001f &&
                    Quaternion.Angle(p.rb.rotation, physicalRotation) < .001f, "Global sync changed rotation");
                Near(p.rb.linearVelocity, Vector3.right * 4, "Global sync changed velocity");
                Flush();
                Near(p.transform.position, physical, "Next-frame restore did not publish physics pose");
                Check(!Queued(p), "Repeated same-frame operations left a duplicate queue entry");

                Configure(p); p.Turn(45);
                Vector3 target = new Vector3(7, 1, -9);
                p.Warp(target, Quaternion.Euler(0, 110, 0));
                Vector3 warpedHead = p.headCollider.transform.position; Flush();
                Near(p.headCollider.transform.position, warpedHead, "Pending turn undid warp");
                Near(new Vector3(warpedHead.x, 1, warpedHead.z), target, "Warp destination changed");
                Near(p.rb.linearVelocity, Vector3.zero, "Pending pose restore reintroduced warp momentum");

                Configure(p); p.ApplyPassengerDisplacement(delta);
                // A later accepted collision pose must win over the queued turn/carry.
                Vector3 accepted = new Vector3(3, 2, 1);
                p.transform.position = accepted; p.rb.position = accepted; Flush();
                Near(p.transform.position, accepted, "Queued restore overwrote a later collision pose");

                Configure(p); p.Turn(45);
                var disable = (Action)Delegate.CreateDelegate(typeof(Action), p,
                    typeof(NewPlayer).GetMethod("OnDisable", Private));
                disable(); Vector3 disabledHead = p.headCollider.transform.position; Flush();
                Near(p.headCollider.transform.position, disabledHead, "Pending synchronization undid disable");
                Check(!Queued(p) && p.rb.interpolation == RigidbodyInterpolation.Interpolate, "Disable changed movement mode or retained a pose");

                Configure(p);
                for (int i = 0; i < 20; i++) Cycle(p, delta);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 1000; i++) Cycle(p, delta);
                Check(GC.GetAllocatedBytesForCurrentThread() - before == 0, "Synchronization allocated during warmed movement");

                // Set up the otherwise real zero-step render branch outside the measured
                // interval; reflection in the fixture must not count as runtime allocation.
                var poseTime = typeof(NewPlayer).GetField("movementPoseTime", Private);
                var poseFrame = typeof(NewPlayer).GetField("movementPoseFrame", Private);
                long renderAllocations = 0;
                for (int i = 0; i < 100; i++)
                {
                    p.ApplyPassengerDisplacement(delta);
                    poseTime.SetValue(p, Math.Min(Time.timeAsDouble - .001, Time.fixedTimeAsDouble + Time.fixedDeltaTime - .001));
                    poseFrame.SetValue(p, Time.frameCount - 1);
                    Publish();
                    before = GC.GetAllocatedBytesForCurrentThread(); Render();
                    renderAllocations += GC.GetAllocatedBytesForCurrentThread() - before;
                    Check(Queued(p), "Zero-step display fixture did not exercise interpolation");
                    Flush();
                }
                Check(renderAllocations == 0, "Zero-step display interpolation allocated");

                Configure(p); p.Turn(45);
                poseFrame.SetValue(p, Time.frameCount - 1);
                Publish(); p.Warp(target); warpedHead = p.headCollider.transform.position; Render();
                Near(p.headCollider.transform.position, warpedHead, "Zero-step rendering undid an intervening warp");

                Configure(p); p.Turn(45);
                displayed = p.transform.position; poseFrame.SetValue(p, Time.frameCount - 1);
                Publish(); Render(); // A later frame with no time advance represents pause.
                Near(p.transform.position, displayed, "Paused interpolation jumped to the physics pose");
                Flush();
                var reset = (Action)Delegate.CreateDelegate(typeof(Action), typeof(NewPlayer).GetMethod("ResetMovementPoseState", Private));
                reset(); p.ApplyPassengerDisplacement(delta); Flush();
            }
            using (var live = new MovementTestRig())
            {
                Configure(live.p); live.p.ApplyPassengerDisplacement(Vector3.up);
                var destroyed = new MovementTestRig(); Configure(destroyed.p);
                destroyed.p.Turn(45); destroyed.Dispose();
                Vector3 expected = live.p.rb.position;
                NewPlayer.SyncTransformsPreservingMovementPose(); Flush();
                Near(live.p.transform.position, expected, "Destroyed queued player prevented live player restore");
            }
            int markers = 0;
            InspectLoop(PlayerLoop.GetCurrentPlayerLoop(), ref markers);
            Check(markers == 2, "Player-loop synchronization stages missing or duplicated");
            Debug.Log("HeadPivotSynchronizationChecks PASS: " + checks + " assertions; zero/tiny/multiple carry, sync protection, warp, disable, destruction, loop ordering and zero recurring allocations.");
        }
        finally
        {
            Flush(); Object.DestroyImmediate(services);
            GR.Player.instance = oldPlayer; NewPlayer.instance = oldVR; HelicopterManager.instance = oldHeli;
            Physics.simulationMode = mode; Physics.autoSyncTransforms = autoSync;
        }
    }
    static void Cycle(NewPlayer p, Vector3 delta)
    {
        p.Turn(.5f); p.ApplyPassengerDisplacement(delta);
        NewPlayer.SyncTransformsPreservingMovementPose(); Flush();
        p.Turn(-.5f); p.ApplyPassengerDisplacement(-delta); Flush();
    }
    static Vector3 Cached(NewPlayer p,string field)=>(Vector3)typeof(NewPlayer).GetField(field,Private|BindingFlags.Public).GetValue(p);
    static void ContactAnchorSeeds()
    {
        using(var rig=new MovementTestRig()) {
            var p=rig.p;Configure(p);
            Vector3 display=p.transform.position,physics=p.rb.position;
            Vector3 offset=physics-display;
            p.InitializeValues();
            Near(Cached(p,"lastPosition"),physics,"Velocity history seeded from the displayed pose");
            Near(Cached(p,"lastHeadPosition"),PhysicsHead(p),"Head anchor seeded from the displayed pose");
            Near(Cached(p,"lastLeftHandPosition"),p.leftHandTransform.position+offset,"Left anchor seeded from display");
            Near(Cached(p,"lastRightHandPosition"),p.rightHandTransform.position+offset,"Right anchor seeded from display");
            p.leftHandTransform.localPosition+=Vector3.up*.1f;
            p.SnapHandLocomotionAnchor(false);
            Near(Cached(p,"lastLeftHandPosition"),p.leftHandTransform.position+offset,"Fresh grip seeded from display");
            Near(p.transform.position,display,"Seeding moved the rendered player");
            Near(p.rb.position,physics,"Seeding moved the physical player");
            rig.move(1f/72);
            Near(Cached(p,"currentVelocity"),Vector3.zero,"Display/physics gap became a launch impulse");
            Near(p.rb.position,physics,"Solver lost physical travel");
            Near(p.transform.position,display,"Free-motion render pose was lost");
            p.rb.interpolation=RigidbodyInterpolation.None;p.InitializeValues();
            Near(Cached(p,"lastPosition"),display,"Noninterpolated control history changed");
            Near(Cached(p,"lastLeftHandPosition"),p.leftHandTransform.position,"Noninterpolated control hand seed changed");
        }
        Flush();
    }
    static void InspectLoop(PlayerLoopSystem loop, ref int markers)
    {
        if (loop.subSystemList == null) return;
        var children = loop.subSystemList;
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].type?.Name == "MovementPoseRestoreLoop")
            {
                markers++;
                Check(i + 1 < children.Length && children[i + 1].type ==
                    typeof(UnityEngine.PlayerLoop.EarlyUpdate.PhysicsResetInterpolatedTransformPosition),
                    "Restore is not immediately before Unity interpolation reset");
            }
            if (children[i].type?.Name == "MovementZeroStepRenderLoop")
            {
                markers++;
                Check(i > 0 && children[i - 1].type == typeof(UnityEngine.PlayerLoop.PreUpdate.PhysicsUpdate),
                    "Zero-step display update is not immediately after native interpolation");
            }
            InspectLoop(children[i], ref markers);
        }
    }
}
