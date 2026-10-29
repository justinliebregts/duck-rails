using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Run with Unity -batchmode -executeMethod HelicopterCarryChecks.Run -quit.
public static class HelicopterCarryChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string field, object value) => target.GetType().GetField(field, Private).SetValue(target, value);
    static T Get<T>(object target, string field) => (T)target.GetType().GetField(field, Private).GetValue(target);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Near(Vector3 actual, Vector3 expected, string message) => Check((actual - expected).sqrMagnitude < 0.000001f, message + ": " + actual + " vs " + expected);

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch mode only");
        GR.Player previousPlayer = GR.Player.instance;
        NewPlayer previousVR = NewPlayer.instance;
        HelicopterManager previousManager = HelicopterManager.instance;
        SimulationMode previousMode = Physics.simulationMode;
        GameObject rig = null, cabinObject = null, managerObject = null;
        try
        {
            Physics.simulationMode = SimulationMode.Script;
            cabinObject = new GameObject("Carry regression cabin");
            var helicopter = cabinObject.AddComponent<Helicopter>();
            var cabin = cabinObject.AddComponent<BoxCollider>();
            cabin.isTrigger = true;
            cabin.center = Vector3.up;
            cabin.size = new Vector3(3f, 2f, 4f);
            Set(helicopter, "passengerCabin", cabin);
            var floorObject = new GameObject("Floor");
            floorObject.transform.SetParent(cabinObject.transform, false);
            var floor = floorObject.AddComponent<BoxCollider>();
            floor.center = new Vector3(0f, -0.1f, 0f);
            floor.size = new Vector3(3f, 0.2f, 4f);

            managerObject = new GameObject("Carry regression manager");
            var manager = managerObject.AddComponent<HelicopterManager>();
            Set(manager, "helicopter", helicopter);
            HelicopterManager.instance = manager;

            rig = new GameObject("Carry regression local VR rig");
            GR.Player.instance = rig.AddComponent<GR.Player>();
            var vr = rig.AddComponent<NewPlayer>();
            NewPlayer.instance = vr;
            vr.rb = rig.AddComponent<Rigidbody>();
            vr.rb.constraints = RigidbodyConstraints.FreezeRotation;
            vr.rb.useGravity = false;
            vr.bodyCollider = rig.AddComponent<CapsuleCollider>();
            vr.bodyCollider.height = 1f;
            vr.bodyCollider.radius = 0.25f;
            vr.bodyMidpoint = rig.transform;
            var head = new GameObject("Head");
            head.transform.SetParent(rig.transform, false);
            head.transform.localPosition = Vector3.up * 0.3f;
            vr.headCollider = head.AddComponent<SphereCollider>();
            vr.headCollider.radius = 0.15f;
            vr.leftHandTransform = head.transform;
            vr.rightHandTransform = head.transform;
            vr.velocityHistorySize = 8;
            rig.transform.position = new Vector3(0f, 0.51f, 0f);
            vr.InitializeValues();
            Set(vr, "lastBodyMidpoint", vr.bodyMidpoint.position);
            Set(vr, "lastPos", rig.transform.position);
            var storeVelocity = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), vr,
                typeof(NewPlayer).GetMethod("StoreVelocities", Private));

            // Populate held-tool samples as well as bare-hand anchors.
            Set(vr, "lastLeftLocoCount", 2);
            Set(vr, "lastRightLocoCount", 2);
            Vector3[] left = Get<Vector3[]>(vr, "lastLeftLocoSamples");
            Vector3[] right = Get<Vector3[]>(vr, "lastRightLocoSamples");
            left[0] = right[0] = head.transform.position;
            left[1] = right[1] = head.transform.position + Vector3.forward;
            Vector3 initialHead = head.transform.position;
            Vector3 initialRoot = rig.transform.position;
            Vector3 velocity = new Vector3(0.2f, 1.5f, 0.1f);
            vr.rb.linearVelocity = velocity;
            for (int i = 1; i <= 120; i++)
                helicopter.MoveCabin(Vector3.up * (i * 0.025f), Quaternion.identity, true);
            Near(rig.transform.position, initialRoot + Vector3.up * 3f, "VR lift-off carry");
            Near(vr.rb.position, rig.transform.position, "Rigidbody follows rig");
            Near(vr.rb.linearVelocity, velocity, "Real jump velocity preserved");
            Near(Get<Vector3>(vr, "lastHeadPosition"), initialHead + Vector3.up * 3f, "Head sweep translated");
            Near(Get<Vector3>(vr, "lastBodyMidpoint"), rig.transform.position, "Body sweep translated");
            Near(vr.lastLeftHandPosition, head.transform.position, "Left hand translated");
            Near(vr.lastRightHandPosition, head.transform.position, "Right hand translated");
            Near(left[1], initialHead + Vector3.forward + Vector3.up * 3f, "Held left tool samples translated");
            Near(right[1], left[1], "Held right tool samples translated");
            Near(Get<Vector3>(vr, "lastPos"), rig.transform.position, "Travel history excludes carry");
            storeVelocity(1f / 72f);
            Near(Get<Vector3>(vr, "denormalizedVelocityAverage"), Vector3.zero, "Carry creates no launch velocity");
            Check(Physics.Raycast(rig.transform.position, Vector3.down, out RaycastHit hit, 1f, ~0, QueryTriggerInteraction.Ignore)
                && hit.collider == floor && Mathf.Abs(hit.point.y - 3f) < 0.001f, "Queries see raised floor immediately with auto-sync disabled");

            // Jump above the entry volume, then die. Neither should end an existing ride.
            vr.ApplyPassengerDisplacement(Vector3.up * 1.7f);
            Vector3 jumped = rig.transform.position;
            helicopter.MoveCabin(Vector3.up * 3.1f, Quaternion.identity, true);
            Near(rig.transform.position, jumped + Vector3.up * 0.1f, "Small jump stays aboard");
            Set(GR.Player.instance, "isDead", true);
            Vector3 ghost = rig.transform.position;
            for (int i = 1; i <= 360; i++)
                helicopter.MoveCabin(Vector3.up * (3.1f + i / 72f), Quaternion.identity, true);
            Near(rig.transform.position, ghost + Vector3.up * 5f, "Ghost remains carried for 5 seconds of steps");
            Set(GR.Player.instance, "isDead", false);

            // Door exit releases immediately; an outside player is never pulled along.
            vr.ApplyPassengerDisplacement(Vector3.right * 2f);
            Vector3 outside = rig.transform.position;
            helicopter.MoveCabin(Vector3.up * 9f, Quaternion.identity, true);
            Near(rig.transform.position, outside, "Door exit releases");
            helicopter.MoveCabin(Vector3.up * 10f, Quaternion.identity, true);
            Near(rig.transform.position, outside, "Outside player stays released");

            // Re-enter low in cabin. Full pose changes carry position without tilting the view.
            vr.ApplyPassengerDisplacement(new Vector3(0.3f, 10.6f, 0.4f) - rig.transform.position);
            Vector3 relative = helicopter.transform.InverseTransformPoint(rig.transform.position);
            Quaternion view = rig.transform.rotation;
            helicopter.MoveCabin(Vector3.up * 10.1f, Quaternion.Euler(2f, 12f, 3f), true);
            Near(rig.transform.position, helicopter.transform.TransformPoint(relative), "Rotation and bob carry anchor");
            Check(Quaternion.Angle(rig.transform.rotation, view) < 0.001f, "VR view stays upright");

            // Warm the entire moving-cabin path, then measure only runtime work.
            for (int i = 0; i < 50; i++)
                helicopter.MoveCabin(Vector3.up * (10f + i * 0.001f), Quaternion.identity, true);
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                helicopter.MoveCabin(Vector3.up * (10f + i * 0.001f), Quaternion.identity, true);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            Check(allocated == 0, "Carry allocated " + allocated + " bytes in 1000 updates");

            CheckPhysicsLift(vr, helicopter);
            CheckRideEdges(vr, helicopter, cabin);
            CheckManagerLifecycle(vr, helicopter, manager);

            // Respawn uses Warp, including when it happens before cabin movement that frame.
            vr.Warp(new Vector3(20f, 1f, 20f));
            Vector3 respawn = rig.transform.position;
            helicopter.MoveCabin(Vector3.up * 12f, Quaternion.identity, true);
            Near(rig.transform.position, respawn, "Respawn clears VR carry");
            Check(!Get<bool>(helicopter, "carryingPassenger"), "No stale ghost passenger");
            Set(helicopter, "releasedFrame", -1); // Edit-mode test calls all run in the same frame.
            vr.ApplyPassengerDisplacement(new Vector3(0f, 12.6f, 0f) - rig.transform.position);
            helicopter.MoveCabin(Vector3.up * 40f, Quaternion.identity, false);
            Near(rig.transform.position, new Vector3(0f, 12.6f, 0f), "New arrival/pad does not teleport passenger");

            vr.enabled = false;
            Object.DestroyImmediate(vr.headCollider);
            Object.DestroyImmediate(vr.bodyCollider);
            Object.DestroyImmediate(vr.rb);
            var desktop = rig.AddComponent<DesktopPlayer>();
            var controller = rig.AddComponent<CharacterController>();
            controller.center = Vector3.up * 0.85f;
            controller.height = 1.7f;
            Set(desktop, "_controller", controller);
            Set(helicopter, "localPlayer", null); // Rebind to the replacement controller.
            rig.transform.position = new Vector3(0f, 40.05f, 0f);
            helicopter.MoveCabin(Vector3.up * 40.1f, Quaternion.identity, true);
            Near(rig.transform.position, new Vector3(0f, 40.15f, 0f), "Desktop carry");
            controller.Move(Vector3.down * 0.02f);
            Check(rig.transform.position.y >= 40.07f, "Desktop movement retains raised floor support");
            desktop.Teleport(new Vector3(30f, 1f, 30f), Quaternion.identity);
            helicopter.MoveCabin(Vector3.up * 41f, Quaternion.identity, true);
            Near(rig.transform.position, new Vector3(30f, 1f, 30f), "Desktop respawn clears carry");

            ValidatePrefab();
            int executionOrder = MonoImporter.GetExecutionOrder(MonoScript.FromMonoBehaviour(manager));
            if (executionOrder == 0) executionOrder = ((DefaultExecutionOrder)Attribute.GetCustomAttribute(typeof(HelicopterManager), typeof(DefaultExecutionOrder))).order;
            Check(executionOrder < -16200,
                "Helicopter manager must update before both player controllers");
            Debug.Log("HelicopterCarryChecks: ALL PASSED (VR/desktop lift, history, jump, ghost, exit, respawn, cabin boundaries, parked drift, lifecycle, floor sync, zero recurring allocation)");
        }
        finally
        {
            if (rig != null) Object.DestroyImmediate(rig);
            if (cabinObject != null) Object.DestroyImmediate(cabinObject);
            if (managerObject != null) Object.DestroyImmediate(managerObject);
            GR.Player.instance = previousPlayer;
            NewPlayer.instance = previousVR;
            HelicopterManager.instance = previousManager;
            Physics.simulationMode = previousMode;
        }
    }

    static void ResetRide(NewPlayer vr, Helicopter helicopter, Vector3 anchor)
    {
        helicopter.MoveCabin(Vector3.zero, Quaternion.identity, false);
        Set(helicopter, "releasedFrame", -1); // Explicitly advance past a release in these edit-mode calls.
        vr.ApplyPassengerDisplacement(anchor - vr.bodyMidpoint.position);
    }

    static void CheckRideEdges(NewPlayer vr, Helicopter helicopter, BoxCollider cabin)
    {
        // An unboarded player above the roof or below the floor must not acquire
        // the extra height reserved for passengers jumping from inside.
        foreach (Vector3 outside in new[] { new Vector3(0f, 2.5f, 0f), new Vector3(0f, -0.1f, 0f), new Vector3(1.51f, 1f, 0f), new Vector3(0f, 1f, 2.01f) })
        {
            ResetRide(vr, helicopter, outside);
            helicopter.MoveCabin(Vector3.up * 0.1f, Quaternion.identity, true);
            Near(vr.transform.position, outside, "Outside boundary must not board");
        }
        ResetRide(vr, helicopter, Vector3.up * 0.6f);
        helicopter.MoveCabin(Vector3.up * 0.1f, Quaternion.identity, true);
        vr.ApplyPassengerDisplacement(Vector3.up * 2.5f); // Above even the retained jump headroom.
        Vector3 tooHigh = vr.transform.position;
        helicopter.MoveCabin(Vector3.up * 0.2f, Quaternion.identity, true);
        Near(vr.transform.position, tooHigh, "Large jump releases carry");

        ResetRide(vr, helicopter, Vector3.up * 0.6f);
        helicopter.MoveCabin(Vector3.up * 0.1f, Quaternion.identity, true);
        cabin.enabled = false;
        Vector3 disabled = vr.transform.position;
        helicopter.MoveCabin(Vector3.up * 0.2f, Quaternion.identity, true);
        Near(vr.transform.position, disabled, "Disabled cabin releases carry");
        cabin.enabled = true;
        helicopter.MoveCabin(Vector3.up * 0.3f, Quaternion.identity, true);
        Near(vr.transform.position, disabled + Vector3.up * 0.1f, "Re-enabled cabin can board again");

        // Unlike a warp outside the helicopter, a warp *inside* it proves the
        // explicit same-frame release works independently of the bounds check.
        ResetRide(vr, helicopter, Vector3.up * 0.6f);
        helicopter.MoveCabin(Vector3.up * 0.1f, Quaternion.identity, true);
        vr.Warp(Vector3.up);
        helicopter.MoveCabin(Vector3.up * 0.2f, Quaternion.identity, true);
        Near(vr.transform.position, Vector3.up, "Warp inside cabin cannot consume stale carry in same frame");
        Set(helicopter, "releasedFrame", -1);
        helicopter.MoveCabin(Vector3.up * 0.3f, Quaternion.identity, true);
        Near(vr.transform.position, Vector3.up * 1.1f, "Warped passenger can board next frame");

        ResetRide(vr, helicopter, new Vector3(0.31f, 0.6f, 0.41f));
        helicopter.MoveCabin(Vector3.up, Quaternion.Euler(2f, 33f, 3f), true);
        Vector3 parked = vr.transform.position;
        for (int i = 0; i < 1000; i++)
            helicopter.MoveCabin(Vector3.up, Quaternion.Euler(2f, 33f, 3f), true);
        Check(vr.transform.position.Equals(parked), "Parked passenger accumulates no floating-point drift");

        GR.Player player = GR.Player.instance;
        GR.Player.instance = null;
        helicopter.MoveCabin(Vector3.up * 2f, Quaternion.identity, true);
        Near(vr.transform.position, parked, "Missing local player moves no rig");
        GR.Player.instance = player;
        ResetRide(vr, helicopter, Vector3.up * 0.6f);
        vr.enabled = false;
        helicopter.MoveCabin(Vector3.up * 0.1f, Quaternion.identity, true);
        Near(vr.transform.position, Vector3.up * 0.6f, "Disabled locomotion is not carried");
        vr.enabled = true;
        Debug.Log("Carry edges PASS: entry boundaries, jump limit, collider disable/re-enable, same-frame warp, exact parked stability, missing/disabled local player");
    }

    static void SetPhase(HelicopterManager manager, int phase)
    {
        FieldInfo field = typeof(HelicopterManager).GetField("phase", Private);
        field.SetValue(manager, Enum.ToObject(field.FieldType, phase));
    }

    static void CheckManagerLifecycle(NewPlayer vr, Helicopter helicopter, HelicopterManager manager)
    {
        var pad = new GameObject("Carry lifecycle pad");
        var secondPad = new GameObject("Carry lifecycle second pad");
        var apply = (Action)Delegate.CreateDelegate(typeof(Action), manager,
            typeof(HelicopterManager).GetMethod("ApplyLocal", Private));
        try
        {
            secondPad.transform.position = Vector3.right * 100f;
            Set(manager, "landingPoints", new[] { pad.transform, secondPad.transform });
            Set(manager, "groundDust", new HelicopterGroundDust[2]);
            Set(manager, "landingIndex", 0);
            Set(manager, "hasCabinPose", false);
            Set(manager, "<ArrivalSerial>k__BackingField", 1);
            ResetRide(vr, helicopter, Vector3.up * 0.6f);
            SetPhase(manager, 2); // Staying -> Departing should preserve the ride.
            apply();
            SetPhase(manager, 3);
            Set(manager, "landTimer", 5f);
            apply();
            Near(vr.transform.position, helicopter.transform.position + Vector3.up * 0.6f, "Manager departure preserves passenger offset");
            Check(helicopter.transform.position.y > 10f, "Departure actually moved cabin");
            Vector3 beforeDisappear = vr.transform.position;
            SetPhase(manager, 0);
            apply();
            Check(!helicopter.gameObject.activeSelf && !Get<bool>(manager, "hasCabinPose"), "Waiting deactivates and invalidates cabin pose");
            Near(vr.transform.position, beforeDisappear, "Disappearance does not teleport passenger");
            Set(manager, "landingIndex", 1);
            Set(manager, "<ArrivalSerial>k__BackingField", 2);
            Set(manager, "landTimer", 0f);
            SetPhase(manager, 1);
            apply();
            Near(vr.transform.position, beforeDisappear, "New arrival at different pad cannot drag old passenger");

            SetPhase(manager, 2);
            apply();
            Set(helicopter, "releasedFrame", -1);
            vr.ApplyPassengerDisplacement(helicopter.transform.position + Vector3.up * 0.6f - vr.transform.position);
            apply();
            Check(Get<bool>(helicopter, "carryingPassenger"), "Passenger boards new arrival");
            Set(manager, "landingIndex", -1);
            apply();
            Check(!Get<bool>(helicopter, "carryingPassenger") && !Get<bool>(manager, "hasCabinPose"), "Missing pad clears carry");
            Debug.Log("Manager lifecycle PASS: departure, despawn, new pad/arrival, reboarding, invalid pad cleanup");
        }
        finally
        {
            Object.DestroyImmediate(pad);
            Object.DestroyImmediate(secondPad);
            Set(manager, "groundDust", Array.Empty<HelicopterGroundDust>());
        }
    }

    static void CheckPhysicsLift(NewPlayer vr, Helicopter helicopter)
    {
        // Simulate 72 Hz physics with both slower and faster rendering. The floor
        // must keep supporting a gravity-driven passenger throughout a 30 m lift.
        foreach (int renderRate in new[] { 30, 72, 120 })
        {
            helicopter.MoveCabin(Vector3.zero, Quaternion.identity, false);
            vr.ApplyPassengerDisplacement(new Vector3(0f, 0.51f, 0f) - vr.transform.position);
            vr.rb.linearVelocity = Vector3.zero;
            vr.rb.useGravity = true;
            Physics.SyncTransforms();
            float accumulated = 0f;
            float dt = 1f / renderRate;
            for (int frame = 0; frame < renderRate * 10; frame++)
            {
                accumulated += dt;
                while (accumulated >= 1f / 72f)
                {
                    Physics.Simulate(1f / 72f);
                    accumulated -= 1f / 72f;
                }
                float height = 30f * Mathf.SmoothStep(0f, 1f, (frame + 1) * dt / 10f);
                helicopter.MoveCabin(Vector3.up * height, Quaternion.identity, true);
                float clearance = vr.transform.position.y - height;
                Check(clearance > 0.45f && clearance < 0.6f, "Floor support lost at " + renderRate + " fps, frame " + frame + ": " + clearance);
            }
        }
        vr.rb.useGravity = false;
        vr.rb.linearVelocity = Vector3.zero;
    }

    static void ValidatePrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonHelicopter.prefab");
        var instance = Object.Instantiate(prefab);
        try
        {
            var helicopter = instance.GetComponent<Helicopter>();
            var cabin = Get<BoxCollider>(helicopter, "passengerCabin");
            Check(cabin != null && cabin.isTrigger && cabin.transform.IsChildOf(helicopter.transform), "Prefab cabin reference");
            Check(Get<float>(helicopter, "passengerJumpHeadroom") >= 1f, "Prefab jump headroom");
            Physics.SyncTransforms();
            Vector3 center = cabin.transform.TransformPoint(cabin.center);
            Check(Physics.Raycast(center, Vector3.down, out RaycastHit hit, 3f, 1 << 6, QueryTriggerInteraction.Ignore)
                && hit.collider.transform.IsChildOf(instance.transform), "Real cabin volume sits above helicopter floor");
        }
        finally { Object.DestroyImmediate(instance); }
    }
}
