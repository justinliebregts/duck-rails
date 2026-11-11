using System;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Run in isolated batch Play Mode. Contacts and launches use the real frame/fixed-step loop.
public static class ZombieHookPullChecks
{
    const string Pending = "DuckZ.ZombieHookPullChecks";

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        SessionState.SetBool(Pending, true);
        Register();
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    static void Register()
    {
        if (!SessionState.GetBool(Pending, false)) return;
        EditorApplication.update -= StartWhenPlaying;
        EditorApplication.update += StartWhenPlaying;
    }

    static void StartWhenPlaying()
    {
        if (!EditorApplication.isPlaying || EditorApplication.isCompiling || !SessionState.GetBool(Pending, false)) return;
        EditorApplication.update -= StartWhenPlaying;
        SessionState.SetBool(Pending, false);
        Time.captureDeltaTime = Time.fixedDeltaTime;
        var runner = new GameObject("Hook pull regression").AddComponent<ZombieHookPullProbe>();
        runner.gameObject.AddComponent<AudioListener>();
        var hit = (Action<Vector3>)Delegate.CreateDelegate(typeof(Action<Vector3>),
            typeof(PhotonPlayer).GetMethod("ApplyLocalHookPull", BindingFlags.Static | BindingFlags.NonPublic));
        GameObject Prefab(string path) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + path + ".prefab")
            ?? throw new InvalidOperationException("Missing test prefab: " + path);
        runner.Begin(Prefab("Items/ItemGoldBar"), Prefab("Weapons/WeaponNailBat"), Prefab("Weapons/WeaponFryPan"),
            Prefab("Bags/Bag"), _ => hit(Vector3.forward * 18f), passed => EditorApplication.Exit(passed ? 0 : 1));
    }
}

public sealed class ZombieHookPullProbe : MonoBehaviour
{
    public void Begin(GameObject item, GameObject bat, GameObject pan, GameObject bag, Action<NewPlayer> fire, Action<bool> finished)
        => StartCoroutine(RunChecked(Measure(item, bat, pan, bag, fire), finished));

    static IEnumerator RunChecked(IEnumerator cases, Action<bool> finished)
    {
        while (true)
        {
            bool next;
            try { next = cases.MoveNext(); }
            catch (Exception e) { Debug.LogException(e); finished(false); yield break; }
            if (!next) break;
            yield return cases.Current;
        }
        finished(true);
    }

    static IEnumerator Measure(GameObject item, GameObject bat, GameObject pan, GameObject bag, Action<NewPlayer> fire)
    {
        var services = new GameObject("Hook pull player services");
        services.SetActive(false);
        var previous = GR.Player.instance;
        GR.Player.instance = services.AddComponent<GR.Player>();
        var previousItems = PhotonItemManager.instance;
        PhotonItemManager.instance = services.AddComponent<PhotonItemManager>();
        PhotonItemManager.instance.itemParent = services.transform;
        var floor = new GameObject("Hook pull floor");
        floor.layer = 6;
        floor.transform.position = Vector3.down * .1f;
        floor.AddComponent<BoxCollider>().size = new Vector3(100, .2f, 100);
        var wall = new GameObject("Hook pull wall");
        wall.layer = 6;
        wall.transform.position = new Vector3(0, 2, 2.1f);
        wall.AddComponent<BoxCollider>().size = new Vector3(12, 5, .2f);
        try
        {
            // Rails: "carried" moves the floor and wall sideways at train speed and applies the
            // same passenger displacement the train cabin applies, across the pull axis.
            foreach (var (obstructed, carried) in new[] { (false, false), (true, false), (false, true), (true, true) })
            {
                foreach (var grip in new[] { "clear", "bare", "item", "bat", "bag", "pan+bag" })
                {
                    using (var rig = new MovementTestRig())
                    {
                        var p = rig.p;
                        floor.transform.position = Vector3.down * .1f;
                        wall.transform.position = new Vector3(0, 2, 2.1f);
                        Physics.SyncTransforms();
                        Vector3 carriedTotal = Vector3.zero;
                        Action carry = () =>
                        {
                            if (!carried) return;
                            Vector3 delta = Vector3.right * (20f * Time.deltaTime);
                            floor.transform.position += delta;
                            wall.transform.position += delta;
                            Physics.SyncTransforms();
                            p.ApplyPassengerDisplacement(delta);
                            carriedTotal += delta;
                        };
                        wall.SetActive(false);
                        p.locomotionEnabledLayers = 1 << 6;
                        p.bodyCollider.enabled = p.headCollider.enabled = true;
                        p.bodyCollider.gameObject.layer = p.headCollider.gameObject.layer = LayerMask.NameToLayer("Player");
                        p.rb.useGravity = false;
                        p.rb.linearDamping = 0;
                        p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                        var left = AddHand(p.leftHandTransform, OVRInput.Controller.LTouch);
                        var right = AddHand(p.rightHandTransform, OVRInput.Controller.RTouch);
                        GR.Player.instance.leftPlayerHand = left;
                        GR.Player.instance.rightPlayerHand = right;
                        var leftPrefab = grip == "item" ? item : grip == "bat" ? bat : grip == "pan+bag" ? pan : null;
                        var rightPrefab = grip == "bag" || grip == "pan+bag" ? bag : null;
                        var leftItem = Plant(left, leftPrefab, grip != "clear" && grip != "bag");
                        var rightItem = Plant(right, rightPrefab, rightPrefab != null);
                        p.enabled = true;
                        // Begin with an established support so an uncorrected motor actually rewinds.
                        yield return null;
                        double deadline = Time.realtimeSinceStartupAsDouble + 2;
                        bool plantedLeft = grip != "clear" && grip != "bag";
                        bool plantedRight = rightPrefab != null;
                        while ((p.wasLeftHandTouching != plantedLeft || p.wasRightHandTouching != plantedRight)
                               && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                        var start = p.rb.position;
                        if (p.wasLeftHandTouching != plantedLeft || p.wasRightHandTouching != plantedRight)
                            throw new InvalidOperationException($"Could not plant {grip}: left={p.wasLeftHandTouching} right={p.wasRightHandTouching}");
                        fire(p);
                        deadline = Time.realtimeSinceStartupAsDouble + 3;
                        while (!p.disableMovement && Time.realtimeSinceStartupAsDouble < deadline) { carry(); yield return null; }
                        bool hit = p.disableMovement;
                        wall.SetActive(obstructed);
                        float peak = 0f;
                        while (p.disableMovement && Time.realtimeSinceStartupAsDouble < deadline)
                        {
                            peak = Mathf.Max(peak, p.rb.position.z - start.z);
                            carry();
                            yield return null;
                        }
                        float first = p.rb.position.z - start.z;
                        float resumeSpeed = p.rb.linearVelocity.z;
                        for (int i = 0; i < 12; i++) { carry(); yield return null; }
                        float retained = p.rb.position.z - start.z;
                        // Train travel must neither be lost nor rewound by the corrected hands.
                        float lateral = p.rb.position.x - start.x - carriedTotal.x;
                        bool attached = (leftPrefab == null || StillHeld(left, leftItem)) && (rightPrefab == null || StillHeld(right, rightItem));
                        bool forward = peak > (obstructed ? 1.5f : 4.5f) && first >= peak - .12f && retained >= peak - .12f;
                        bool stopped = grip == "clear" || Mathf.Abs(resumeSpeed) < .5f;
                        string sample = $"ZombieHook pull {grip} wall={obstructed} carried={carried} hit={hit} peak={peak:F2} first={first:F2} retained={retained:F2} lateral={lateral:F2} resumeSpeed={resumeSpeed:F2} held={attached}";
                        if (!hit || p.disableMovement || !attached || !forward || !stopped || (obstructed && retained >= 2f) || Mathf.Abs(lateral) > .15f)
                            throw new InvalidOperationException(sample);
                        Debug.Log(sample);
                    }
                }
            }
            wall.SetActive(false);
            // Pull across an uphill, single-sided surface with gravity on and the tracked
            // controllers deliberately below it. Measure the actual released rigidbody.
            floor.SetActive(false);
            var ramp = new Mesh();
            try
            {
                ramp.vertices = new[] {new Vector3(-10,-1,-10), new Vector3(-10,4,40),
                    new Vector3(10,4,40), new Vector3(10,-1,-10)};
                ramp.triangles = new[] {0,1,2,0,2,3};
                floor.transform.position = Vector3.zero;
                floor.GetComponent<BoxCollider>().enabled = false;
                floor.AddComponent<MeshCollider>().sharedMesh = ramp;
                floor.SetActive(true);
                Physics.SyncTransforms();
                CheckInterpolatedTip(bat, floor.GetComponent<MeshCollider>());
                foreach (var prefab in new[] {null, item, bat, bag})
                {
                    var steps = CheckHalfwayDrop(prefab, fire);
                    while (steps.MoveNext()) yield return steps.Current;
                }
                foreach (var prefab in new[] {bat, bag})
                {
                    var steps = CheckHalfwayDrop(prefab, fire, true);
                    while (steps.MoveNext()) yield return steps.Current;
                }
            }
            finally { Object.DestroyImmediate(ramp); }
        }
        finally
        {
            GR.Player.instance = previous;
            PhotonItemManager.instance = previousItems;
            Object.DestroyImmediate(services);
            Object.DestroyImmediate(floor);
            Object.DestroyImmediate(wall);
        }
    }

    static void CheckInterpolatedTip(GameObject prefab, Collider floor)
    {
        var bat = Object.Instantiate(prefab);
        try
        {
            var box = bat.GetComponent<BoxCollider>();
            var bounds = new Bounds(box.center, box.size);
            var normal = new Vector3(0, 1, -.1f).normalized;
            var type = typeof(PlayerHand).Assembly.GetType("RemoteHookDropSurface", true);
            object guard = Activator.CreateInstance(type);
            const BindingFlags fields = BindingFlags.Instance | BindingFlags.NonPublic;
            type.GetField("floor", fields).SetValue(guard, floor);
            type.GetField("shape", fields).SetValue(guard, bounds);
            type.GetField("localNormal", fields).SetValue(guard, normal);
            type.GetField("localPoint", fields).SetValue(guard, Vector3.zero);
            type.GetField("expires", fields).SetValue(guard, Time.unscaledTimeAsDouble + 1);
            var correct = type.GetMethod("Correct", fields);
            bat.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(85, 0, 0));
            Vector3 below = bat.transform.position;
            correct.Invoke(guard, new object[] {bat.transform, true});
            if (Vector3.Dot(bat.transform.position - below, normal) <= .003f)
                throw new InvalidOperationException("Remote bat tip regression did not exercise a correction");
            CheckCorners(box.transform, bounds, .001f);
            below.x = 20; // Past the edge of the ten-metre-wide half ramp.
            bat.transform.position = below;
            correct.Invoke(guard, new object[] {bat.transform, true});
            if (bat.transform.position != below) throw new InvalidOperationException("Invisible floor beyond the mesh edge");
        }
        finally { Object.DestroyImmediate(bat); }
    }

    static IEnumerator CheckHalfwayDrop(GameObject prefab, Action<NewPlayer> fire, bool obstructDrop = false)
    {
        using var rig = new MovementTestRig();
        var p = rig.p;
        p.bodyCollider.enabled = p.headCollider.enabled = true;
        p.bodyCollider.gameObject.layer = p.headCollider.gameObject.layer = LayerMask.NameToLayer("Player");
        p.locomotionEnabledLayers = 1 << 6;
        p.rb.useGravity = true;
        p.rb.linearDamping = 0;
        p.rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        var hand = AddHand(p.rightHandTransform, OVRInput.Controller.RTouch);
        hand.enabled = true; // Sample normal throw velocity while the rig travels.
        GR.Player.instance.leftPlayerHand = AddHand(p.leftHandTransform, OVRInput.Controller.LTouch);
        GR.Player.instance.rightPlayerHand = hand;
        GameObject gear = Plant(hand, prefab, true);
        var meshes = gear != null ? gear.GetComponentsInChildren<MeshFilter>() : null;
        GameObject obstruction = null;
        try
        {
            p.enabled = true;
            yield return null;
            fire(p);
            if (!p.disableMovement || Mathf.Abs(p.rb.linearVelocity.y) > .01f)
                throw new InvalidOperationException("Missing hook impulse or unexpected lift");
            if (!obstructDrop)
            {
                var resolve = (Action<Transform, Transform>)Delegate.CreateDelegate(typeof(Action<Transform, Transform>),
                    p, typeof(NewPlayer).GetMethod("ResolveHookHands", BindingFlags.Instance | BindingFlags.NonPublic));
                resolve(null, null);
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 128; i++) resolve(null, null);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                if (allocated != 0) throw new InvalidOperationException($"Hook hand casts allocated {allocated} bytes");
            }
            float start = Time.time, startZ = p.rb.position.z, peak = startZ;
            bool released = false;
            int samples = 0, recoveryFrames = 0;
            while (p.disableMovement || recoveryFrames++ < 8)
            {
                Vector3 raw = hand.transform.position;
                raw.y = raw.z * .1f - .12f;
                hand.transform.position = raw;
                yield return null;
                // HoldPoint is the actual equipment parent in this controller-only fixture.
                Vector3 grip = hand.HoldPoint.position;
                if (grip.y - grip.z * .1f < -.003f)
                    throw new InvalidOperationException("Underground grip " + grip);
                if (gear != null) CheckGearClearance(gear.GetComponent<BoxCollider>(), meshes, released ? .015f : .003f);
                samples++;
                if (!released && Time.time - start >= .16f)
                {
                    if (!p.disableMovement) throw new InvalidOperationException("Missed halfway release");
                    if (obstructDrop)
                    {
                        obstruction = new GameObject("Moving geometry occupies current and previous grip");
                        obstruction.layer = 6;
                        obstruction.transform.position = hand.HoldPoint.position;
                        obstruction.AddComponent<BoxCollider>().size = Vector3.one * 20f;
                        Physics.SyncTransforms();
                        hand.enabled = true;
                    }
                    if (gear != null && gear.TryGetComponent<BagObject>(out var bag)) bag.DropLocal(bag.GetReleaseVelocity());
                    else if (!obstructDrop && prefab != null && prefab.name == "WeaponNailBat")
                    {
                        var spinning = hand.DetachHeld();
                        spinning.Release(Vector3.down, Vector3.right * 14f);
                        float roll = gear.GetComponent<Rigidbody>().angularVelocity.x;
                        if (Mathf.Abs(roll) > .1f) throw new InvalidOperationException("Bat tip spinning into the ramp on release");
                    }
                    else hand.Release();
                    if (obstructDrop)
                    {
                        if (!StillHeld(hand, gear)) throw new InvalidOperationException("Released inside moving geometry");
                        obstruction.SetActive(false);
                        // The original release must complete without a second grip input.
                        float until = Time.time + .2f;
                        while (StillHeld(hand, gear) && Time.time < until) yield return null;
                    }
                    if (gear != null)
                    {
                        CheckGearClearance(gear.GetComponent<BoxCollider>(), meshes, .003f);
                        if (!obstructDrop && gear.GetComponent<Rigidbody>().linearVelocity.z > 4f)
                            throw new InvalidOperationException("The pull became a forward throw");
                        if (gear.GetComponent<Rigidbody>().isKinematic || gear.transform.IsChildOf(hand.HoldPoint))
                            throw new InvalidOperationException($"Halfway release did not drop {gear.name}, obstructed={obstructDrop}, " +
                                $"held={StillHeld(hand, gear)}, parent={gear.transform.parent}, kinematic={gear.GetComponent<Rigidbody>().isKinematic}, " +
                                $"handEnabled={hand.isActiveAndEnabled}, grip={hand.HoldPoint.position}, gear={gear.transform.position}");
                    }
                    released = true;
                }
                peak = Mathf.Max(peak, p.rb.position.z);
                if (peak - p.rb.position.z > .15f) throw new InvalidOperationException("Hook snapback");
                if (Time.time - start > 2f) throw new InvalidOperationException("Hook lock timed out");
            }
            if (!released || peak - startZ < 1f) throw new InvalidOperationException("Pull or drop incomplete");
            Debug.Log($"ZombieHook surface {prefab?.name ?? "bare"} obstructDrop={obstructDrop} passed {samples} samples, travel={peak-startZ:F2}");
        }
        finally
        {
            if (gear != null) Object.DestroyImmediate(gear);
            if (obstruction != null) Object.DestroyImmediate(obstruction);
        }
    }

    static void CheckGearClearance(BoxCollider box, MeshFilter[] meshes, float tolerance)
    {
        CheckCorners(box.transform, new Bounds(box.center, box.size), tolerance);
        foreach (var mesh in meshes)
            if (mesh.sharedMesh != null) CheckCorners(mesh.transform, mesh.sharedMesh.bounds, tolerance);
    }

    static void CheckCorners(Transform shape, Bounds bounds, float tolerance)
    {
        // Independent of the production bounds calculation. Include the visible model.
        Vector3 e = bounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 point = shape.TransformPoint(bounds.center + new Vector3(
                (i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z));
            if (point.y - point.z * .1f < -tolerance)
                throw new InvalidOperationException($"Underground {shape.name} at {point:F4}");
        }
    }

    static PlayerHand AddHand(Transform anchor, OVRInput.Controller controller)
    {
        var hand = anchor.gameObject.AddComponent<PlayerHand>();
        hand.controller = controller;
        hand.enabled = false;
        return hand;
    }

    static GameObject Plant(PlayerHand hand, GameObject prefab, bool plant)
    {
        if (!plant) return null;
        if (prefab == null)
        {
            var pos = hand.transform.position;
            pos.y = .05f;
            hand.transform.position = pos;
            return null;
        }
        var obj = Object.Instantiate(prefab, hand.HoldPoint.position, Quaternion.identity);
        GrantInputFocus();
        if (obj.TryGetComponent<PhotonGrabbable>(out var offlineItem)) offlineItem.KeepLocalPhysics();
        if (obj.TryGetComponent<BagObject>(out var bag))
        {
            bag.Init(Array.Empty<object>());
            bag.HoldLocal(hand);
        }
        else if (!hand.ForceGrab(obj.GetComponent<Grabbable>())) throw new InvalidOperationException("Grab failed: " + prefab.name);
        var points = new Vector3[Grabbable.MaxLocomotionSamples];
        if (!hand.TryGetLocomotionShape(points, out int count, out float radius) || count == 0)
            throw new InvalidOperationException("Missing contact samples: " + prefab.name);
        float low = points[0].y;
        for (int i = 1; i < count; i++) low = Mathf.Min(low, points[i].y);
        hand.transform.position += Vector3.up * (radius - low);
        return obj;
    }

    // Rails: grabs require PhotonMasterHandoff input focus, which an unfocused batch
    // process lacks once PhotonSession exists. Grant it as the other Rails fixtures do.
    static void GrantInputFocus()
    {
        foreach (var handoff in Object.FindObjectsByType<PhotonMasterHandoff>(FindObjectsSortMode.None))
            typeof(PhotonMasterHandoff).GetField("focused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handoff, true);
        if (!PhotonMasterHandoff.LocalAvailable) throw new InvalidOperationException("Fixture could not grant input focus");
    }

    static bool StillHeld(PlayerHand hand, GameObject obj) =>
        obj != null && (obj.TryGetComponent<BagObject>(out var bag)
            ? bag.State == BagState.Held && bag.transform.IsChildOf(hand.HoldPoint)
            : hand.Held != null && hand.Held.gameObject == obj);
}
