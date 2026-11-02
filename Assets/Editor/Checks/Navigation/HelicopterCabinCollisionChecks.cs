using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Real cabin colliders and the production VR movement solver, without an XR device.
public static class HelicopterCabinCollisionChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static int checks, failures;
    delegate bool HeadCast(Vector3 start, float radius, Vector3 move, float precision, out Vector3 end, bool singleHand);
    static void Set(object o, string n, object v) => o.GetType().GetField(n, P).SetValue(o, v);
    static T Get<T>(object o, string n) => (T)o.GetType().GetField(n, P).GetValue(o);
    static void Check(bool ok, string message)
    {
        checks++;
        if (!ok) { failures++; Debug.LogError("Cabin collision FAIL: " + message); }
    }

    sealed class Fixture : IDisposable
    {
        public readonly Helicopter helicopter;
        public readonly BoxCollider cabin;
        public readonly HelicopterManager manager;
        public readonly GR.Player player;
        public readonly NewPlayer vr;
        public readonly Action<float> movement;
        public readonly HeadCast cast;
        public Fixture()
        {
            var go = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonHelicopter.prefab"));
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            helicopter = go.GetComponent<Helicopter>();
            cabin = Get<BoxCollider>(helicopter, "passengerCabin");
            manager = new GameObject("Cabin collision manager").AddComponent<HelicopterManager>();
            Set(manager, "helicopter", helicopter); Set(manager, "hasCabinPose", true);
            HelicopterManager.instance = manager;
            var rig = new GameObject("Cabin collision rider"); rig.layer = 7;
            player = rig.AddComponent<GR.Player>(); GR.Player.instance = player;
            vr = rig.AddComponent<NewPlayer>();
            vr.rb = rig.AddComponent<Rigidbody>(); vr.rb.useGravity = false;
            vr.rb.constraints = RigidbodyConstraints.FreezeRotation;
            vr.trackingSpace = Child(rig.transform, "Tracking");
            var head = Child(vr.trackingSpace, "Head");
            vr.headCollider = head.gameObject.AddComponent<SphereCollider>(); vr.headCollider.radius = .15f;
            vr.headEyeOffset = head;
            var body = Child(vr.trackingSpace, "Body");
            vr.bodyCollider = body.gameObject.AddComponent<CapsuleCollider>();
            vr.bodyCollider.height = .5f; vr.bodyCollider.radius = .15f; vr.bodyCollider.center = Vector3.down * .225f;
            vr.bodyMidpoint = Child(body, "Body midpoint"); vr.bodyMidpoint.localPosition = Vector3.down * .225f;
            vr.leftHandTransform = Child(vr.trackingSpace, "Left"); vr.rightHandTransform = Child(vr.trackingSpace, "Right");
            vr.velocityHistorySize = 8; vr.locomotionEnabledLayers = 1 << 6;
            Set(vr, "headBodyOffset", new Vector3(0, 0, -.06f));
            Set(vr, "bodyHalfHeight", .4f); Set(vr, "envMask", (LayerMask)(1 << 6)); Set(vr, "envLayer", 6);
            movement = (Action<float>)Delegate.CreateDelegate(typeof(Action<float>), vr, typeof(NewPlayer).GetMethod("Movement", P));
            var method = typeof(NewPlayer).GetMethod("IterativeCollisionSphereCast", P, null,
                new[] { typeof(Vector3), typeof(float), typeof(Vector3), typeof(float), typeof(Vector3).MakeByRefType(), typeof(bool) }, null);
            cast = (HeadCast)Delegate.CreateDelegate(typeof(HeadCast), vr, method);
            Place(new Vector3(0, 2.3f, 3.473f));
        }
        static Transform Child(Transform parent, string name)
        {
            var t = new GameObject(name).transform; t.gameObject.layer = 7; t.SetParent(parent, false); return t;
        }
        public void Place(Vector3 head)
        {
            helicopter.ReleasePassenger(); Set(helicopter, "releasedFrame", -1);
            // Production recenters the rig beneath the head in X/Z. Start there so
            // edit-mode Rigidbody recentering does not become part of this fixture.
            vr.transform.position = new Vector3(head.x, 0, head.z); vr.rb.position = vr.transform.position; vr.rb.linearVelocity = Vector3.zero;
            vr.trackingSpace.localPosition = Vector3.zero;
            Vector3 localHead = Vector3.up * head.y;
            vr.headCollider.transform.localPosition = localHead;
            vr.bodyCollider.transform.localPosition = localHead + new Vector3(0, 0, -.06f);
            vr.leftHandTransform.localPosition = localHead + new Vector3(-.3f, -.4f, .1f);
            vr.rightHandTransform.localPosition = localHead + new Vector3(.3f, -.4f, .1f);
            vr.wasLeftHandTouching = vr.wasRightHandTouching = false;
            vr.InitializeValues(); Set(vr, "lastBodyMidpoint", vr.bodyMidpoint.position); Set(vr, "lastPos", vr.transform.position);
            Physics.SyncTransforms(); helicopter.RefreshPassenger();
        }
        // Physics/tracked movement must not translate history as platform carry does.
        public void Advance(Vector3 delta)
        {
            vr.transform.position += delta; vr.rb.position = vr.transform.position; Physics.SyncTransforms();
        }
        public void Dispose()
        {
            Object.DestroyImmediate(vr.gameObject); Object.DestroyImmediate(manager.gameObject); Object.DestroyImmediate(helicopter.gameObject);
        }
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Batch only");
        var oldPlayer = GR.Player.instance; var oldVR = NewPlayer.instance; var oldManager = HelicopterManager.instance;
        checks = failures = 0;
        try
        {
            Boundaries();
            InteriorEndTransitions();
            InteriorCoverage();
            foreach (float dt in new[] { 1f / 30, 1f / 72, 1f / 120 })
                foreach (bool moving in new[] { false, true }) Ceiling(dt, moving);
            WallAndFreeMotion();
            DiagonalHops();
            Allocation();
            if (failures != 0) throw new Exception("HelicopterCabinCollisionChecks: " + failures + " failures / " + checks + " assertions");
            Debug.Log("HelicopterCabinCollisionChecks: ALL PASSED (" + checks + " assertions; real ceiling, render rates, moving cabin, full interior coverage, independent shop trigger, exits)");
        }
        finally { GR.Player.instance = oldPlayer; NewPlayer.instance = oldVR; HelicopterManager.instance = oldManager; }
    }

    static void Ceiling(float dt, bool moving)
    {
        using var f = new Fixture();
        if (moving) f.helicopter.MoveCabin(Vector3.up * .2f, Quaternion.identity, true);
        Vector3 start = f.vr.headCollider.transform.position;
        f.Advance(Vector3.up * .25f);
        Vector3 current = f.vr.headCollider.transform.position;
        Check(f.cast(start, .15f, current - start, f.vr.defaultPrecision, out Vector3 expected, false), "Hop reaches actual ceiling");
        f.movement(dt); f.helicopter.RefreshPassenger();
        Vector3 actual = f.vr.headCollider.transform.position;
        float error = Vector3.Distance(actual, expected);
        Debug.Log($"Cabin ceiling trace: moving={moving} dt={dt:F4} startY={start.y:F4} attemptedY={current.y:F4} resolvedY={expected.y:F4} actualY={actual.y:F4} error={error:F4} carry={f.helicopter.HasLocalPassenger}");
        Check(error < .003f, "Head uses collision-resolved position, not a full-frame rewind");
        Check(f.vr.wasHeadTouching && f.helicopter.HasLocalPassenger, "Ceiling contact retains cabin carry");
        Check(actual.y + .15f <= (moving ? .2f : 0) + 2.652f, "Head remains below actual ceiling");
        // Repeated upward pressure must settle at the ceiling rather than oscillate.
        for (int i = 0; i < 20; i++)
        {
            f.Advance(Vector3.up * .02f);
            f.movement(dt); f.helicopter.RefreshPassenger();
            Check(Vector3.Distance(f.vr.headCollider.transform.position, expected) < .004f, "Repeated ceiling contact remains stable");
            Check(f.helicopter.HasLocalPassenger, "Repeated ceiling contact never drops carry");
        }
    }

    static void Boundaries()
    {
        using var f = new Fixture();
        Bounds bounds = Get<Bounds>(f.helicopter, "interiorCarryBounds");
        Vector3 center = bounds.center;
        float halfWidth = bounds.extents.x;
        // Sample vertical airborne positions without hand/body contact masking volume membership.
        foreach (float x in new[] { 0, halfWidth - .05f, -halfWidth + .05f })
        {
            f.Place(new Vector3(x, 2.2f, center.z));
            for (int i = 0; i < 20; i++)
            {
                float rise = .25f * Mathf.Sin(i / 19f * Mathf.PI);
                Vector3 target = new Vector3(x, 2.2f + rise, center.z);
                f.Advance(target - f.vr.headCollider.transform.position);
                f.helicopter.RefreshPassenger();
                Check(f.helicopter.HasLocalPassenger, "Vertical cabin hop preserves horizontal membership");
            }
        }
        f.Place(new Vector3(0, 2.2f, center.z));
        f.Advance(Vector3.right * (halfWidth + .03f)); f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Actual horizontal doorway exit releases carry");
        foreach (float z in new[] { bounds.min.z - .1f, bounds.max.z + .1f })
        {
            f.Place(new Vector3(0, 2.2f, center.z));
            f.Advance(Vector3.forward * (z + .06f - center.z)); f.helicopter.RefreshPassenger();
            Check(!f.helicopter.HasLocalPassenger, "Leaving an actual cabin end releases carry");
        }
        // World-up hops on a tilted cabin also travel sideways in cabin coordinates.
        f.helicopter.transform.rotation = Quaternion.Euler(0, 0, 3);
        Vector3 anchor = f.helicopter.transform.TransformPoint(new Vector3(halfWidth - .01f, 2.1f, center.z));
        f.Place(anchor + new Vector3(0, .225f, .06f));
        Check(f.helicopter.HasLocalPassenger, "Rider begins just inside tilted doorway boundary");
        f.Advance(Vector3.up * .4f); f.helicopter.RefreshPassenger();
        Check(!f.helicopter.HasLocalPassenger, "Tilted edge hop crosses strict horizontal boundary");
        Debug.Log("Cabin boundary trace: central/level hops retain carry; a 0.4 m world-up hop at 3 degrees roll shifts about 2.1 cm sideways in cabin space and can exit a doorway boundary");
    }

    static void WallAndFreeMotion()
    {
        using var f = new Fixture();
        Vector3 start = f.vr.headCollider.transform.position;
        f.Advance(new Vector3(.1f, -.1f, .1f));
        Vector3 intended = f.vr.headCollider.transform.position;
        f.movement(1f / 72);
        Check(Vector3.Distance(f.vr.headCollider.transform.position, intended) < .001f, "Free head movement remains unchanged");

        var wall = new GameObject("Cabin collision test wall"); wall.layer = 6;
        try
        {
            wall.transform.position = new Vector3(.8f, 2, 3.473f);
            var box = wall.AddComponent<BoxCollider>(); box.size = new Vector3(.1f, 2, 2);
            f.Place(start);
            // Keep hands behind the head so this isolates head contact from hand bracing.
            f.vr.leftHandTransform.localPosition = f.vr.headCollider.transform.localPosition + new Vector3(-.4f, -.4f, 0);
            f.vr.rightHandTransform.localPosition = f.vr.headCollider.transform.localPosition + new Vector3(-.3f, -.4f, 0);
            f.vr.InitializeValues();
            f.Advance(Vector3.right * .7f);
            Check(f.cast(start, .15f, Vector3.right * .7f, f.vr.defaultPrecision, out Vector3 expected, false), "Sideways head motion reaches wall");
            f.movement(1f / 72); f.helicopter.RefreshPassenger();
            Check(Vector3.Distance(f.vr.headCollider.transform.position, expected) < .003f, "Wall correction uses current head position too");
            Check(f.helicopter.HasLocalPassenger, "Wall contact retains cabin carry");
        }
        finally { Object.DestroyImmediate(wall); }
    }

    static void Allocation()
    {
        using var f = new Fixture();
        for (int i = 0; i < 50; i++)
        {
            f.Advance(Vector3.up * .02f); f.movement(1f / 72); f.helicopter.RefreshPassenger();
        }
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 1000; i++)
        {
            f.Advance(Vector3.up * .02f); f.movement(1f / 72); f.helicopter.RefreshPassenger();
        }
        long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(bytes == 0, "Repeated ceiling correction allocates zero managed bytes: " + bytes);
    }

    static void DiagonalHops()
    {
        using var f = new Fixture();
        foreach (float z in new[] { .4f, 1.8f, 3.473f, 5f, 6f })
            foreach (float direction in new[] { -1f, 1f })
            {
                f.Place(new Vector3(0, 2.3f, z));
                f.vr.leftHandTransform.localPosition = f.vr.headCollider.transform.localPosition + new Vector3(-.3f, -.4f, -.2f * direction);
                f.vr.rightHandTransform.localPosition = f.vr.headCollider.transform.localPosition + new Vector3(.3f, -.4f, -.2f * direction);
                f.vr.InitializeValues();
                Vector3 start = f.vr.headCollider.transform.position;
                f.Advance(new Vector3(.15f, .25f, .45f * direction));
                Vector3 current = f.vr.headCollider.transform.position;
                Check(f.cast(start, .15f, current - start, f.vr.defaultPrecision, out Vector3 expected, false), "Diagonal hop reaches ceiling");
                f.movement(1f / 72); f.helicopter.RefreshPassenger();
                Check(Vector3.Distance(f.vr.headCollider.transform.position, expected) < .003f, "Diagonal ceiling correction preserves the solver's allowed horizontal travel");
                Check(f.helicopter.HasLocalPassenger, "Back/front diagonal cabin hop retains carry");
                Debug.Log($"Cabin diagonal trace: z={z:F3} direction={direction} resolved={expected:F4} actual={f.vr.headCollider.transform.position:F4} carry={f.helicopter.HasLocalPassenger}");
            }
    }

    static void InteriorCoverage()
    {
        using var f = new Fixture();
        Check(Get<bool>(f.helicopter, "useInteriorCarryBounds"), "Prefab enables independent cabin carry bounds");
        Check(Mathf.Approximately(f.cabin.transform.localPosition.z, 3.473f)
            && Mathf.Approximately(f.cabin.transform.localScale.z, 4.86f)
            && Mathf.Approximately(f.cabin.size.z, 1.0000004f), "Shop trigger retains its authored position and size");
        Check(Mathf.Approximately(Get<float>(f.helicopter, "passengerJumpHeadroom"), 1f), "Existing 1 m cabin headroom remains unchanged");
        var colliders = f.helicopter.GetComponentsInChildren<Collider>();
        int covered = 0;
        // Audit a grid of actual floor/ceiling pairs, including raised interior surfaces.
        for (int zi = -20; zi <= 70; zi++) for (int xi = -8; xi <= 8; xi++)
        {
            Vector3 origin = new Vector3(xi * .2f, 1.879f, zi * .1f);
            float floor = float.NaN, ceiling = float.NaN, down = 10, up = 10;
            float floorNormal = 0, ceilingNormal = 0;
            foreach (var c in colliders)
            {
                if (c.isTrigger || !c.enabled) continue;
                if (c.Raycast(new Ray(origin, Vector3.down), out var hit, down)) { down = hit.distance; floor = hit.point.y; floorNormal = hit.normal.y; }
                if (c.Raycast(new Ray(origin, Vector3.up), out hit, up)) { up = hit.distance; ceiling = hit.point.y; ceilingNormal = hit.normal.y; }
            }
            if (float.IsNaN(floor) || float.IsNaN(ceiling) || floor < .7f || ceiling < 2.4f || ceiling > 2.8f
                || floorNormal < .5f || ceilingNormal > -.5f || ceiling - floor < .65f) continue;
            float headY = floor + .475f;
            f.Place(new Vector3(origin.x, headY, origin.z));
            Check(f.helicopter.HasLocalPassenger, "Actual interior floor sample boards at " + origin);
            f.Advance(Vector3.up * Mathf.Min(.3f, ceiling - .16f - headY)); f.helicopter.RefreshPassenger();
            Check(f.helicopter.HasLocalPassenger, "Airborne interior sample retains carry at " + origin);
            covered++;
        }
        Check(covered > 800, "Whole interior coverage checks a dense grid of actual floor/ceiling pairs");
        f.cabin.enabled = false;
        f.Place(new Vector3(0, 2f, .6f));
        Check(f.helicopter.HasLocalPassenger, "Carry is independent of shop trigger enable state");
        Set(f.player, "isDead", true);
        Vector3 ghost = f.vr.transform.position;
        f.helicopter.MoveCabin(Vector3.up * .2f, Quaternion.identity, true);
        Check(Vector3.Distance(f.vr.transform.position, ghost + Vector3.up * .2f) < .001f, "Ghost rides in newly covered interior");
        f.vr.Warp(Vector3.right * 20);
        Check(!f.helicopter.HasLocalPassenger, "Ghost respawn releases expanded interior carry");
        Debug.Log("Cabin interior coverage: " + covered + " floor/ceiling samples, hops, shop independence, ghost and respawn");
    }

    static void InteriorEndTransitions()
    {
        using var f = new Fixture();
        foreach (bool front in new[] { false, true })
        {
            f.Place(new Vector3(0, 1.8f, front ? 5.5f : 1.4f));
            f.vr.leftHandTransform.localPosition = f.vr.headCollider.transform.localPosition + new Vector3(-.3f, -.3f, front ? -.6f : .6f);
            f.vr.rightHandTransform.localPosition = f.vr.headCollider.transform.localPosition + new Vector3(.3f, -.3f, front ? -.6f : .6f);
            f.vr.InitializeValues();
            f.Advance(new Vector3(0, .2f, front ? .8f : -.8f));
            f.movement(1f / 72); f.helicopter.RefreshPassenger();
            Check(f.helicopter.HasLocalPassenger, "Diagonal hop across former shop boundary remains carried");
            Vector3 before = f.vr.transform.position;
            f.helicopter.MoveCabin(f.helicopter.transform.position + Vector3.up * .1f, Quaternion.identity, true);
            Check(Vector3.Distance(f.vr.transform.position, before + Vector3.up * .1f) < .001f, "Newly covered end follows cabin lift");
            Debug.Log($"Cabin end trace: front={front} head={f.vr.headCollider.transform.position:F4} body={f.vr.bodyMidpoint.position:F4} headContact={f.vr.wasHeadTouching} carry={f.helicopter.HasLocalPassenger}");
            f.helicopter.MoveCabin(Vector3.zero, Quaternion.identity, false);
        }
    }
}
