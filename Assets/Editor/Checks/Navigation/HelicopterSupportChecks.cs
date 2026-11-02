using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class HelicopterSupportChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks;
    static void Set(object o, string n, object value) => o.GetType().GetField(n, P).SetValue(o, value);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, P).GetValue(o);
    static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    static void Near(Vector3 a, Vector3 b, string message) => Check(Vector3.Distance(a, b) < 0.003f, message + ": " + a + " vs " + b);

    sealed class Fixture : IDisposable
    {
        public readonly Helicopter helicopter;
        public readonly HelicopterManager manager;
        public readonly BoxCollider roof, rotor;
        public readonly GR.Player player;
        public readonly NewPlayer vr;
        public readonly DesktopPlayer desktop;
        public readonly CharacterController controller;
        public readonly Transform left, right;
        public readonly Action<float> movement;
        public Fixture(bool useDesktop)
        {
            helicopter = new GameObject("Support helicopter").AddComponent<Helicopter>();
            var cabin = helicopter.gameObject.AddComponent<BoxCollider>(); cabin.isTrigger = true;
            cabin.center = Vector3.up; cabin.size = new Vector3(3, 2, 4);
            Set(helicopter, "passengerCabin", cabin);
            roof = Box(helicopter.transform, "Roof", new Vector3(0, 2.9f, 0), new Vector3(4, .2f, 6));
            rotor = Box(helicopter.transform, "Rotating rotor", new Vector3(8, 3, 0), new Vector3(2, .2f, 2));
            helicopter.propeller = rotor.transform;
            manager = new GameObject("Support manager").AddComponent<HelicopterManager>();
            Set(manager, "helicopter", helicopter); Set(manager, "hasCabinPose", true);
            HelicopterManager.instance = manager;
            var root = new GameObject("Local support rider");
            player = root.AddComponent<GR.Player>(); GR.Player.instance = player;
            if (useDesktop)
            {
                desktop = root.AddComponent<DesktopPlayer>();
                controller = root.AddComponent<CharacterController>(); controller.height = 1.7f; controller.radius = .25f; controller.center = Vector3.up * .85f;
                Set(desktop, "_controller", controller);
            }
            else
            {
                vr = root.AddComponent<NewPlayer>();
                vr.rb = root.AddComponent<Rigidbody>(); vr.rb.useGravity = false; vr.rb.constraints = RigidbodyConstraints.FreezeRotation;
                vr.bodyCollider = root.AddComponent<CapsuleCollider>(); vr.bodyCollider.height = 1f; vr.bodyCollider.radius = .25f;
                vr.bodyMidpoint = root.transform;
                vr.trackingSpace = Child(root.transform, "Tracking space", Vector3.zero);
                var head = Child(vr.trackingSpace, "Head", Vector3.up * .3f);
                vr.headCollider = head.gameObject.AddComponent<SphereCollider>(); vr.headCollider.radius = .15f;
                vr.headEyeOffset = head;
                left = Child(vr.trackingSpace, "Left hand", new Vector3(-.4f, -.5f, 0));
                right = Child(vr.trackingSpace, "Right hand", new Vector3(.3f, .3f, 0));
                vr.leftHandTransform = left; vr.rightHandTransform = right;
                vr.velocityHistorySize = 8; vr.locomotionEnabledLayers = 1 << 6;
                Set(vr, "headBodyOffset", Vector3.down * .3f); Set(vr, "envMask", (LayerMask)(1 << 6)); Set(vr, "envLayer", 6);
                movement = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), vr, typeof(NewPlayer).GetMethod("Movement", P));
            }
            Place(new Vector3(0, useDesktop ? 3.01f : 3.51f, 0));
        }
        public static Transform Child(Transform parent, string name, Vector3 position)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t; }
        public static BoxCollider Box(Transform parent, string name, Vector3 position, Vector3 size)
        { var t = Child(parent, name, position); t.gameObject.layer = 6; var c = t.gameObject.AddComponent<BoxCollider>(); c.size = size; return c; }
        public void Move(Vector3 delta)
        {
            if (vr != null) vr.ApplyPassengerDisplacement(delta); else desktop.ApplyPassengerDisplacement(delta);
            Physics.SyncTransforms();
        }
        public void Place(Vector3 position)
        {
            helicopter.ReleasePassenger(); Set(helicopter, "releasedFrame", -1);
            player.transform.position = position;
            if (vr != null)
            {
                vr.rb.position = position; vr.rb.linearVelocity = Vector3.zero;
                vr.wasLeftHandTouching = vr.wasRightHandTouching = false;
                vr.InitializeValues(); Set(vr, "lastBodyMidpoint", position); Set(vr, "lastPos", position);
            }
            Physics.SyncTransforms();
        }
        public void Dispose()
        { Object.DestroyImmediate(player.gameObject); Object.DestroyImmediate(helicopter.gameObject); Object.DestroyImmediate(manager.gameObject); }
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
        var oldPlayer = GR.Player.instance; var oldVR = NewPlayer.instance; var oldManager = HelicopterManager.instance;
        try
        {
            BodyAndHop(false); BodyAndHop(true); Hands(); HandHop(); FixedHandSurfaces(); RestingWallMovement(); ShapedHandContact(); RestingHandHandoff(); RealPrefab();
            Debug.Log("HelicopterSupportChecks: ALL PASSED (" + checks + " assertions; VR/desktop roof, single hand, alternating hands, hops, edges, ghost/warp, real mesh, allocation)");
        }
        finally { GR.Player.instance = oldPlayer; NewPlayer.instance = oldVR; HelicopterManager.instance = oldManager; }
    }

    static void BodyAndHop(bool desktop)
    {
        using var f = new Fixture(desktop);
        f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Exterior roof boards " + desktop);
        if (desktop)
        {
            var flags = f.controller.Move(Vector3.down * .2f);
            Check((flags & CollisionFlags.Below) != 0, "Actual CharacterController lands on roof");
            // Edit-mode Move resolves contacts but does not dispatch MonoBehaviour callbacks.
            f.manager.ReportBodySupport(f.player.transform, f.roof, new Vector3(0, 3, 0), Vector3.up);
            Check(Get<Collider>(f.helicopter, "bodySupport") == f.roof, "Controller support report reaches helicopter");
        }
        Vector3 start = f.player.transform.position;
        for (int i = 1; i <= 200; i++) f.helicopter.MoveCabin(Vector3.up * (i * .02f), Quaternion.identity, true);
        Near(f.player.transform.position, start + Vector3.up * 4f, "Body follows full lift on roof");
        f.Move(Vector3.up * .4f);
        f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Small exterior hop stays carried");
        Vector3 jumped = f.player.transform.position;
        f.helicopter.MoveCabin(Vector3.up * 4.1f, Quaternion.identity, true);
        Near(f.player.transform.position, jumped + Vector3.up * .1f, "Airborne hop follows lift");
        f.Move(Vector3.up * .45f);
        Set(f.helicopter, "lastSupportTime", Time.timeAsDouble - .8d);
        f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Expanded allowance carries 0.85 m hop at 0.8 seconds");
        f.Move(Vector3.up * .25f);
        Set(f.helicopter, "lastSupportTime", Time.timeAsDouble - 1.1d);
        f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Expanded allowance carries 1.1 m hop at 1.1 seconds");
        Set(f.helicopter, "lastSupportTime", Time.timeAsDouble - 1.25d);
        f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Hop timeout cannot attach indefinitely");

        f.Place(new Vector3(0, desktop ? 7.11f : 7.61f, 0));
        f.helicopter.RefreshPassenger();
        f.Move(Vector3.up * 1.35f); f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Large exterior jump releases");
        f.Place(new Vector3(0, desktop ? 7.11f : 7.61f, 0)); f.helicopter.RefreshPassenger();
        f.Move(new Vector3(3f, 1.1f, 0)); f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Leaving roof edge releases immediately even during hop");
        Vector3 outside = f.player.transform.position;
        f.helicopter.MoveCabin(Vector3.up * 4.2f, Quaternion.identity, true);
        Near(f.player.transform.position, outside, "Departed rider is not dragged");

        f.Place(new Vector3(0, desktop ? 7.21f : 7.71f, 0)); f.helicopter.RefreshPassenger();
        Set(f.player, "isDead", true);
        Check(f.helicopter.HasLocalPassenger, "Ghost keeps exterior body support");
        if (desktop) f.desktop.Teleport(new Vector3(20, 2, 0), Quaternion.identity);
        else f.vr.Warp(new Vector3(20, 2, 0));
        Check(!f.helicopter.HasLocalPassenger, "Exterior ghost respawn clears support");
        Set(f.player, "isDead", false);

        f.Place(new Vector3(8, desktop ? 7.31f : 7.81f, 0)); f.helicopter.RefreshPassenger();
        Check(!f.helicopter.OwnsSupport(f.rotor) && !f.helicopter.HasLocalPassenger, "Rotating rotor excluded");
        f.Place(new Vector3(0, desktop ? 7.21f : 7.71f, 0)); f.helicopter.RefreshPassenger();
        f.roof.enabled = false; f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Disabled exterior support releases");
        f.roof.enabled = true; Set(f.helicopter, "probeFrame", -1); Physics.SyncTransforms(); f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Re-enabled roof can board");

        // Once warm, both support queries and moving carry must remain allocation-free.
        for (int i = 0; i < 100; i++) f.helicopter.MoveCabin(Vector3.up * (4.2f + i * .001f), Quaternion.identity, true);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++) f.helicopter.MoveCabin(Vector3.up * (4.3f + i * .001f), Quaternion.identity, true);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "Exterior body/hop carry has zero recurring allocations");
        Debug.Log("Exterior body/hop PASS: " + (desktop ? "desktop" : "VR"));
    }

    static void Hands()
    {
        using var f = new Fixture(false);
        f.Place(new Vector3(2.2f, 3.55f, 0)); // Body outside roof/cabin; left hand is planted on its edge.
        f.movement(1f / 72f); f.helicopter.RefreshPassenger();
        Check(f.vr.wasLeftHandTouching && !f.vr.wasRightHandTouching, "Actual VR collision solver detects only supporting left hand");
        Check(f.helicopter.HasLocalPassenger, "Single hand boards with body outside cabin and roof footprint");
        Set(f.helicopter, "handSupportFrame", Time.frameCount - 2);
        f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Missed locomotion frames cannot retain stale hand support");
        f.movement(1f / 72f); f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Current hand contact reacquires after stale report");
        Vector3 initial = f.player.transform.position;
        for (int i = 1; i <= 200; i++)
        {
            f.helicopter.MoveCabin(Vector3.up * (i * .02f), Quaternion.identity, true);
            f.movement(1f / 72f); f.helicopter.RefreshPassenger();
            Check(f.helicopter.HasLocalPassenger, "One planted hand remains supported during ascent");
        }
        Near(f.player.transform.position, initial + Vector3.up * 4f, "Hand-supported player does not sink");
        Check(f.vr.rb.linearVelocity.magnitude < .05f, "Helicopter travel does not become a hand jump");
        for (int i = 0; i < 100; i++) f.helicopter.ReportHandSupport(f.player.transform, f.roof, null);
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            f.helicopter.ReportHandSupport(f.player.transform, f.roof, null);
            f.helicopter.RefreshPassenger();
        }
        Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Hand support has zero recurring allocations");

        // Switch support to the other hand using real casts, not a trigger or fabricated grounded flag.
        f.left.localPosition = new Vector3(.3f, .3f, 0); f.right.localPosition = new Vector3(-.4f, -.5f, 0);
        f.vr.InitializeValues(); f.movement(1f / 72f); f.helicopter.RefreshPassenger();
        Check(!f.vr.wasLeftHandTouching && f.vr.wasRightHandTouching && f.helicopter.HasLocalPassenger, "Alternating hands keeps the ride");
        Set(f.player, "isDead", true);
        f.movement(1f / 72f); f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Ghost can remain supported by one hand");
        f.right.localPosition = new Vector3(.3f, .3f, 0); f.vr.InitializeValues();
        f.movement(1f / 72f); f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Releasing last hand off the edge detaches without artificial hover");
        Set(f.player, "isDead", false);

        // A hand planted inside the cabin still carries a body leaning through a doorway.
        var cabinFloor = Fixture.Box(f.helicopter.transform, "Cabin threshold", new Vector3(0, 1f, 0), new Vector3(3f, .2f, 3f));
        // This fixture changes authored geometry; production geometry is cached once at Awake.
        Set(f.helicopter, "supportCached", false);
        f.Place(new Vector3(1.8f, 5.65f, 0));
        f.left.localPosition = new Vector3(-.4f, -.5f, 0); f.vr.InitializeValues();
        f.movement(1f / 72f); f.helicopter.RefreshPassenger();
        Check(f.vr.wasLeftHandTouching && f.helicopter.HasLocalPassenger, "Interior hand supports body outside doorway volume");
        f.helicopter.MoveCabin(Vector3.up * 4.1f, Quaternion.identity, true);
        f.movement(1f / 72f); f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Doorway hand support survives lift");
        Object.DestroyImmediate(cabinFloor.gameObject);
        Debug.Log("VR hand support PASS: real locomotion casts, sustained single hand, alternating hands, ghost, release, interior doorway");
    }

    static void HandHop()
    {
        using var f = new Fixture(false);
        f.Place(new Vector3(0, 3.55f, 0));
        f.movement(1f / 72); f.helicopter.RefreshPassenger();
        Check(f.vr.wasLeftHandTouching && f.helicopter.HasLocalPassenger, "Hand hop starts supported");
        float before = f.player.transform.position.y;
        f.left.localPosition += Vector3.down * .25f;
        f.movement(1f / 72); f.helicopter.RefreshPassenger();
        Check(f.player.transform.position.y > before + .2f, "Actual VR hand push raises the body");
        f.left.localPosition = new Vector3(-.4f, .3f, 0);
        f.movement(1f / 72); f.helicopter.RefreshPassenger();
        Check(!f.vr.wasLeftHandTouching && !f.vr.wasRightHandTouching && f.helicopter.HasLocalPassenger,
            "Hand-generated hop retains carry after both hands release above roof");
        Vector3 airborne = f.player.transform.position;
        f.helicopter.MoveCabin(Vector3.up * .1f, Quaternion.identity, true);
        Near(f.player.transform.position, airborne + Vector3.up * .1f, "Hand-generated airborne hop follows lift");
        Debug.Log("VR hand-generated hop PASS: real hand push, both hands released, airborne carry");
    }

    static void FixedHandSurfaces()
    {
        foreach (bool rightHand in new[] { false, true })
        foreach (Vector3 normal in new[] { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back })
        {
            using var f = new Fixture(false);
            Collider surface;
            if (rightHand)
            {
                var meshSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
                meshSurface.transform.SetParent(f.helicopter.transform, false);
                meshSurface.transform.localPosition = Vector3.up * 4;
                meshSurface.transform.localScale = Vector3.one * 2;
                meshSurface.layer = 6;
                Object.DestroyImmediate(meshSurface.GetComponent<BoxCollider>());
                var meshCollider = meshSurface.AddComponent<MeshCollider>();
                meshCollider.sharedMesh = meshSurface.GetComponent<MeshFilter>().sharedMesh;
                meshCollider.convex = false;
                surface = meshCollider;
            }
            else surface = Fixture.Box(f.helicopter.transform, "Fixed surface", Vector3.up * 4, Vector3.one * 2);
            Set(f.helicopter, "supportCached", false);
            Vector3 point = Vector3.up * 4 + normal;
            f.Place(point + normal * .65f);
            f.helicopter.RefreshPassenger();
            Transform hand = rightHand ? f.right : f.left;
            hand.position = point + normal * .05f;
            f.vr.InitializeValues();
            var capture = (Action<bool, RaycastHit, int>)Delegate.CreateDelegate(typeof(Action<bool, RaycastHit, int>), f.vr,
                typeof(NewPlayer).GetMethod("CapturePassengerHandSupport", P));
            var resolve = (Func<bool, bool, bool, Collider>)Delegate.CreateDelegate(typeof(Func<bool, bool, bool, Collider>), f.vr,
                typeof(NewPlayer).GetMethod("ResolvePassengerHandSupport", P));
            void Seed()
            {
                Physics.SyncTransforms();
                Vector3 n = surface.transform.TransformDirection(normal);
                Check(surface.Raycast(new Ray(hand.position + n * .2f, -n), out RaycastHit hit, .3f), "Seed actual fixed-surface hit");
                capture(rightHand, hit, -1);
                Set(f.vr, "leftPassengerSupport", null); Set(f.vr, "rightPassengerSupport", null);
            }
            void Report()
            {
                Collider support = resolve(rightHand, true, false);
                Set(f.helicopter, "lastSupportTime", Time.timeAsDouble - 10d);
                f.helicopter.ReportHandSupport(f.player.transform, rightHand ? null : support, rightHand ? support : null);
                f.helicopter.RefreshPassenger();
            }
            Seed(); Report();
            Check(f.helicopter.HasLocalPassenger, "Resting single hand carries on face " + normal + ", right=" + rightHand);
            Check(!f.vr.wasLeftHandTouching && !f.vr.wasRightHandTouching, "Fallback does not modify locomotion contact flags");
            for (int i = 1; i <= 100; i++)
            {
                f.helicopter.MoveCabin(Vector3.up * (i * .04f), Quaternion.Euler(0, i * .02f, i * .01f), true);
                Report();
                Check(f.helicopter.HasLocalPassenger, "Resting hand follows ascent and rotation");
            }
            for (int i = 0; i < 100; i++) resolve(rightHand, true, false);
            long allocated = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++) resolve(rightHand, true, false);
            Check(GC.GetAllocatedBytesForCurrentThread() == allocated, "Resting contact fallback allocates zero bytes");

            Set(f.player, "isDead", true); Report();
            Check(f.helicopter.HasLocalPassenger, "Ghost retains resting contact");
            Set(f.player, "isDead", false);
            Vector3 held = hand.position;
            hand.position += surface.transform.TransformDirection(normal) * .005f;
            Check(resolve(rightHand, true, false) == surface, "5 mm tracking separation retains resting contact");
            hand.position = held + surface.transform.TransformDirection(normal) * .02f;
            Check(resolve(rightHand, true, false) == null, "2 cm separation exceeds resting contact tolerance");
            hand.position = held; Seed();
            string contactField = rightHand ? "rightPassengerContact" : "leftPassengerContact";
            object contact = Get<object>(f.vr, contactField);
            contact.GetType().GetField("frame").SetValue(contact, Time.frameCount - 1);
            Set(f.vr, contactField, contact);
            Check(resolve(rightHand, true, false) == surface, "Previous frame contact survives a slow frame");
            contact = Get<object>(f.vr, contactField);
            contact.GetType().GetField("frame").SetValue(contact, Time.frameCount - 2);
            Set(f.vr, contactField, contact);
            Check(resolve(rightHand, true, false) == null, "Missing locomotion frames invalidate stale contact");
            Seed();
            hand.position += surface.transform.TransformDirection(normal) * .1f;
            Check(resolve(rightHand, true, false) == null, "Pulling hand 10 cm away releases");
            hand.position = held;
            Check(resolve(rightHand, true, false) == null, "Expired contact cannot reacquire without a new hit");
            Seed();
            hand.position += surface.transform.TransformDirection(Vector3.Cross(normal, Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.up)) * 3f;
            Check(resolve(rightHand, true, false) == null, "Sliding past finite surface edge releases");
            hand.position = held; Seed();
            surface.enabled = false;
            Check(resolve(rightHand, true, false) == null, "Disabled collider clears resting contact");
            surface.enabled = true; Seed();
            // Unity only permits convex MeshColliders to become triggers/dynamic bodies.
            if (surface is MeshCollider mesh) mesh.convex = true;
            surface.isTrigger = true;
            Check(resolve(rightHand, true, false) == null, "Trigger cannot retain resting contact");
            surface.isTrigger = false; Seed();
            var looseBody = surface.gameObject.AddComponent<Rigidbody>();
            Check(resolve(rightHand, true, false) == null, "Loose Rigidbody cannot retain resting contact");
            Object.DestroyImmediate(looseBody);
            if (surface is MeshCollider restoredMesh) restoredMesh.convex = false;
            Seed();
            Check(resolve(rightHand, false, false) == null, "Disabled hand locomotion clears resting contact");
            Seed(); surface.gameObject.SetActive(false);
            Check(resolve(rightHand, true, false) == null, "Inactive surface clears resting contact");
            surface.gameObject.SetActive(true); Seed();
            surface.transform.SetParent(null, true);
            Check(resolve(rightHand, true, false) == null, "Surface detached from helicopter cannot retain contact");
            surface.transform.SetParent(f.helicopter.transform, true); Seed();
            HelicopterManager.instance = null;
            Check(resolve(rightHand, true, false) == null, "Missing manager clears cached contact");
            HelicopterManager.instance = f.manager;
            Seed(); f.helicopter.MoveCabin(f.helicopter.transform.position, f.helicopter.transform.rotation, false);
            Check(resolve(rightHand, true, false) == null, "Discontinuous helicopter movement clears cached contact");
            Seed(); f.helicopter.ReleasePassenger();
            Check(resolve(rightHand, true, false) == null, "Explicit release clears cached hand surface");
            Seed(); f.vr.Warp(new Vector3(30, 2, 0));
            Check(resolve(rightHand, true, false) == null, "Warp clears resting hand contact");
        }
        Debug.Log("Fixed hand surfaces PASS: both hands, all six face directions, rotation/ascent, release/edges, ghost/warp, zero allocation");
    }

    static void RestingWallMovement()
    {
        foreach (int fps in new[] { 30, 72, 120 }) RestingWallMovementAtRate(fps);
        Debug.Log("Resting wall movement PASS: real movement acquisition, sustained ascent, release at 30/72/120 FPS");
    }

    static void RestingWallMovementAtRate(int fps)
    {
        using var f = new Fixture(false);
        var wall = Fixture.Box(f.helicopter.transform, "Wall", new Vector3(0, 4, 0), new Vector3(2, 2, 2));
        f.roof.enabled = false; // No floor below: releasing the wall must not become a roof hop.
        Set(f.helicopter, "supportCached", false);
        f.Place(new Vector3(1.7f, 4, 0)); f.helicopter.RefreshPassenger();
        f.left.position = new Vector3(1.12f, 4, 0); f.vr.InitializeValues();
        f.left.position = new Vector3(1.04f, 4, 0);
        f.movement(1f / fps); f.helicopter.RefreshPassenger();
        Check(f.helicopter.HasLocalPassenger, "Actual movement acquires side wall hand contact");
        for (int i = 1; i <= 200; i++)
        {
            f.helicopter.MoveCabin(Vector3.up * (i * .02f), Quaternion.identity, true);
            f.movement(1f / fps); f.helicopter.RefreshPassenger();
            Check(f.helicopter.HasLocalPassenger, "Actual stationary wall hand stays carried through lift");
        }
        f.left.localPosition += Vector3.right * .3f;
        f.movement(1f / fps); f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Actual wall hand release detaches hanging player");
    }

    sealed class HandShape : IHandLocomotionShape
    {
        public int count = 2;
        public bool TryGetLocomotionSamples(PlayerHand hand, Vector3[] buffer, out int samples, out float radius)
        {
            samples = count; radius = .08f;
            buffer[0] = hand.transform.position + Vector3.up * .2f;
            buffer[1] = hand.transform.position;
            return count > 0;
        }
    }

    static void ShapedHandContact()
    {
        using var f = new Fixture(false);
        var wall = Fixture.Box(f.helicopter.transform, "Shaped hand wall", new Vector3(0, 4, 0), new Vector3(2, 2, 2));
        Set(f.helicopter, "supportCached", false);
        f.Place(new Vector3(1.7f, 4, 0)); f.helicopter.RefreshPassenger();
        f.left.position = new Vector3(1.08f, 4, 0);
        var hand = f.left.gameObject.AddComponent<PlayerHand>(); f.player.leftPlayerHand = hand;
        var shape = new HandShape(); Set(hand, "heldShape", shape);
        f.vr.InitializeValues(); Physics.SyncTransforms();
        Check(wall.Raycast(new Ray(f.left.position + Vector3.right * .2f, Vector3.left), out RaycastHit hit, .4f), "Shaped hand seeds real surface hit");
        var capture = (Action<bool, RaycastHit, int>)Delegate.CreateDelegate(typeof(Action<bool, RaycastHit, int>), f.vr,
            typeof(NewPlayer).GetMethod("CapturePassengerHandSupport", P));
        var resolve = (Func<bool, bool, bool, Collider>)Delegate.CreateDelegate(typeof(Func<bool, bool, bool, Collider>), f.vr,
            typeof(NewPlayer).GetMethod("ResolvePassengerHandSupport", P));
        capture(false, hit, 1); Set(f.vr, "leftPassengerSupport", null);
        Check(resolve(false, true, false) == wall, "Fallback uses actual shaped sample radius and position");
        shape.count = 1;
        Check(resolve(false, true, false) == null, "Changing shape invalidates missing contact sample");
        shape.count = 2; capture(false, hit, 1); Set(f.vr, "leftPassengerSupport", null);
        shape.count = 0;
        Check(resolve(false, true, false) == null, "Dropping held locomotion shape clears cached support");
        Debug.Log("Shaped hand fallback PASS: sample/radius, sample removal, drop");
    }

    static void RestingHandHandoff()
    {
        using var f = new Fixture(false);
        var wall = Fixture.Box(f.helicopter.transform, "Handoff wall", new Vector3(0, 4, 0), new Vector3(2, 2, 2));
        f.roof.enabled = false;
        Set(f.helicopter, "supportCached", false);
        f.Place(new Vector3(1.6f, 4, 1.6f)); f.helicopter.RefreshPassenger();
        f.left.position = new Vector3(1.05f, 4, .9f);
        f.right.position = new Vector3(.9f, 4, 1.05f);
        f.vr.InitializeValues(); Physics.SyncTransforms();
        var capture = (Action<bool, RaycastHit, int>)Delegate.CreateDelegate(typeof(Action<bool, RaycastHit, int>), f.vr,
            typeof(NewPlayer).GetMethod("CapturePassengerHandSupport", P));
        var resolve = (Func<bool, bool, bool, Collider>)Delegate.CreateDelegate(typeof(Func<bool, bool, bool, Collider>), f.vr,
            typeof(NewPlayer).GetMethod("ResolvePassengerHandSupport", P));
        Check(wall.Raycast(new Ray(f.left.position + Vector3.right * .2f, Vector3.left), out RaycastHit leftHit, .3f), "Left corner contact seeded");
        Check(wall.Raycast(new Ray(f.right.position + Vector3.forward * .2f, Vector3.back), out RaycastHit rightHit, .3f), "Right corner contact seeded");
        capture(false, leftHit, -1); capture(true, rightHit, -1);
        Set(f.vr, "leftPassengerSupport", null); Set(f.vr, "rightPassengerSupport", null);
        void Report()
        {
            f.helicopter.ReportHandSupport(f.player.transform, resolve(false, true, false), resolve(true, true, false));
            f.helicopter.RefreshPassenger();
        }
        Report(); Check(f.helicopter.HasLocalPassenger, "Both resting hands carry across a corner");
        f.left.position += Vector3.right * .2f;
        Report(); Check(f.helicopter.HasLocalPassenger, "Losing left contact preserves independent right contact");
        Vector3 before = f.player.transform.position;
        f.helicopter.MoveCabin(Vector3.up * .2f, Quaternion.identity, true);
        Near(f.player.transform.position, before + Vector3.up * .2f, "Remaining resting hand carries full lift");
        f.right.position += Vector3.forward * .2f;
        Report(); Check(!f.helicopter.HasLocalPassenger, "Releasing final resting hand off the side releases immediately");
        Vector3 released = f.player.transform.position;
        f.helicopter.MoveCabin(Vector3.up * .4f, Quaternion.identity, true);
        Near(f.player.transform.position, released, "Released resting-hand rider is not dragged by next lift");
        Debug.Log("Resting hand handoff PASS: independent faces, either hand, last-hand release, next lift");
    }

    static void RealPrefab()
    {
        using var f = new Fixture(false);
        var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonHelicopter.prefab"));
        go.transform.position = Vector3.right * 50f;
        var helicopter = go.GetComponent<Helicopter>();
        int surfaces = 0;
        try
        {
            Check(Mathf.Approximately(Get<float>(helicopter, "supportHopHeight"), 1.2f)
                && Mathf.Approximately(Get<float>(helicopter, "supportHopSeconds"), 1.2f), "Authored prefab uses 1.2 m / 1.2 second exterior allowance");
            Set(f.manager, "helicopter", helicopter);
            Physics.SyncTransforms();
            foreach (Collider collider in go.GetComponentsInChildren<Collider>())
            {
                if (!helicopter.OwnsSupport(collider)) continue;
                Bounds b = collider.bounds;
                if (!collider.Raycast(new Ray(new Vector3(b.center.x, b.max.y + .2f, b.center.z), Vector3.down), out RaycastHit hit, b.size.y + 1f)
                    || hit.normal.y < .7f) continue;
                f.Place(hit.point + Vector3.up * .51f);
                helicopter.RefreshPassenger();
                Check(helicopter.HasLocalPassenger, "Actual prefab surface boards: " + collider.name);
                surfaces++;
            }
            Check(surfaces >= 2, "Real prefab checks multiple standable mesh surfaces");
            Debug.Log("Actual helicopter prefab PASS: " + surfaces + " upward-facing collider surfaces");
        }
        finally { Set(f.manager, "helicopter", f.helicopter); Object.DestroyImmediate(go); }
    }
}
