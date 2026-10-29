using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

public static class GrabSurfaceChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static int assertions;
    static readonly Vector3 Origin = new Vector3(1024, 128, -1024);
    static void Check(bool ok, string label)
    {
        assertions++;
        if (!ok) throw new Exception("GrabSurfaceChecks: " + label);
    }
    static void Near(Vector3 a, Vector3 b, string label) => Check((a - b).sqrMagnitude < 0.000001f, label + $" {a} != {b}");
    static void Call(Grabbable item, string name) => typeof(Grabbable).GetMethod(name, Private).Invoke(item, null);
    static void SetSurface(Grabbable item, GrabSurfaceData data) => typeof(Grabbable).GetField("grabSurface", Private).SetValue(item, data);

    [MenuItem("Tools/DuckZ/Validation/Items/Validate Free Surface Grabbing")]
    public static void Run()
    {
        assertions = 0;
        GrabSurfaceBaker.BakeAll();
        var prefabs = new List<GameObject>();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Items", "Assets/Prefabs/Weapons" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab.GetComponent<PhotonGrabbable>() != null) prefabs.Add(prefab);
        }
        Check(prefabs.Count >= 10, "All current items, including Gorilla, discovered without catalog membership");
        Scene scene = EditorSceneManager.NewPreviewScene();
        var root = new GameObject("Surface grab validation");
        SceneManager.MoveGameObjectToScene(root, scene);
        try
        {
            foreach (GameObject prefab in prefabs) VerifyPrefab(prefab, root.transform);
            VerifySelectionAndAttachment(root.transform);
            VerifyGeometry();
            VerifyAllocations(prefabs, root.transform);
        }
        finally
        {
            // Edit-mode fixtures invoke lifecycle explicitly, as they do Awake/OnEnable.
            foreach (Grabbable item in root.GetComponentsInChildren<Grabbable>(true)) Call(item, "OnDisable");
            Object.DestroyImmediate(root);
            EditorSceneManager.ClosePreviewScene(scene);
        }
        Debug.Log($"GrabSurfaceChecks PASS: {assertions} assertions; {prefabs.Count} prefabs, free contacts, 6 cm reach, growth, lifecycle and allocation checks.");
    }

    static void VerifyPrefab(GameObject prefab, Transform parent)
    {
        GrabSurfaceData data = prefab.GetComponent<Grabbable>().GrabSurface;
        Check(data != null && data.IsValid, prefab.name + " bake exists");
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab));
        string path = AssetDatabase.GetAssetPath(data);
        string before = System.IO.File.ReadAllText(path);
        GrabSurfaceBaker.Bake(prefab, guid);
        Check(before == System.IO.File.ReadAllText(path), prefab.name + " identical bake makes no asset edits");
        var itemObject = new GameObject(prefab.name + " contact probe");
        itemObject.transform.SetParent(parent, false);
        Grabbable item = itemObject.AddComponent<Grabbable>();
        SetSurface(item, data);
        foreach (float scale in new[] { 0.5f, 1f, 1.2f })
        {
            item.transform.SetPositionAndRotation(Origin, Quaternion.Euler(17, 83, -29));
            item.transform.localScale = Vector3.Scale(prefab.transform.localScale, new Vector3(scale, scale * 0.8f, scale * 1.1f));
            foreach (Vector3 offset in new[] { Vector3.left, Vector3.right, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Check(item.TryGetSurfacePoint(Origin + offset * 2, out Vector3 point), "Surface found");
                // Independent check against the original mesh triangles using plane projection/edge distances.
                Check(DistanceToMesh(prefab, item.transform.localToWorldMatrix, point) < 0.0005f, prefab.name + " contact lies on rendered mesh");
            }
        }
        Object.DestroyImmediate(itemObject);

        parent.SetPositionAndRotation(Origin, Quaternion.Euler(25, 137, -18));
        foreach (float hover in new[] { 1f, 1.2f })
        {
            parent.localScale = Vector3.one * hover;
            GameObject mini = BagMiniModel.Build(prefab, prefab.name, parent, 0.065f);
            var probeObject = new GameObject("Growth surface probe");
            probeObject.transform.SetParent(parent, false);
            var probe = probeObject.AddComponent<PickupGrowthProbe>();
            try
            {
                foreach (Vector3 offset in new[] { new Vector3(-.024f, -.024f, -.036f), new Vector3(0, .024f, .024f), new Vector3(-.024f, .024f, -.036f) })
                {
                    Vector3 grip = parent.position + parent.rotation * offset;
                    ItemPickupPose pose = ItemPickupPose.Capture(prefab, mini.transform.GetChild(0), grip);
                    Check(pose.ClampGripToSurface(prefab, grip), "Bag surface correction succeeds");
                    Check(DistanceToMesh(prefab, Matrix4x4.identity, pose.localGrip) < .00001f, prefab.name + " corrected local grip on mesh");
                    probe.transform.SetParent(null, true);
                    probe.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    probe.transform.localScale = pose.fullScale;
                    probe.clock = 0;
                    probe.BeginPickupGrowth(pose.startScale, pose.fullScale, pose.localGrip, 0);
                    foreach (double time in new[] { 0.0, 0.05, 0.10, 0.16 })
                    {
                        probe.clock = time;
                        probe.Tick();
                        Near(probe.transform.TransformPoint(pose.localGrip), grip, "Surface contact anchored through growth");
                    }
                    Near(probe.transform.position, pose.position, "Final full-size pose");
                }
            }
            finally { Object.DestroyImmediate(mini); Object.DestroyImmediate(probeObject); }
        }
        parent.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        parent.localScale = Vector3.one;
    }

    static Grabbable Fixture(Transform parent, GrabSurfaceData data, Vector3 position)
    {
        var go = new GameObject("Free grip fixture");
        go.layer = 8;
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var item = go.AddComponent<Grabbable>();
        SetSurface(item, data);
        Call(item, "Awake");
        Call(item, "OnEnable");
        return item;
    }

    static void VerifySelectionAndAttachment(Transform parent)
    {
        var data = ScriptableObject.CreateInstance<GrabSurfaceData>();
        // Surface two metres from its root. No collider: visible geometry must still be reachable.
        data.SetBakedGeometry(new[] { new Vector3(2, -1, -1), new Vector3(2, 1, -1), new Vector3(2, 0, 1) }, null, Matrix4x4.identity);
        var handGo = new GameObject("Grip hand");
        handGo.transform.SetParent(parent, false);
        var hand = handGo.AddComponent<PlayerHand>();
        typeof(PlayerHand).GetMethod("Awake", Private).Invoke(hand, null);
        Grabbable item = Fixture(parent, data, Origin);
        Grabbable farther = Fixture(parent, data, Origin + Vector3.right * .04f);
        try
        {
            hand.transform.position = Origin + new Vector3(2.059f, 0, -.06f);
            Check(hand.FindGrabCandidate() == farther, "Nearest surface wins independently of root distance");
            Call(farther, "OnDisable");
            Check(hand.FindGrabCandidate() == item, "5.9 cm accepted outside all physics colliders");
            hand.transform.position = Origin + new Vector3(2.061f, 0, -.06f);
            Check(hand.FindGrabCandidate() == null, "6.1 cm rejected");
            hand.transform.position = Origin + new Vector3(2.04f, .25f, -.06f);
            Quaternion rotation = item.transform.rotation;
            Check(hand.TryGrab(item), "Free grab accepted");
            Check(Quaternion.Angle(rotation, item.transform.rotation) < .001f, "Rotation preserved");
            Check(item.TryGetSurfacePoint(hand.HoldPoint.position, out Vector3 contact), "Contact query");
            Near(contact, hand.HoldPoint.position, "Palm meets surface");
            Vector3 localContact = item.transform.InverseTransformPoint(contact);
            hand.transform.SetPositionAndRotation(hand.transform.position + Vector3.one, Quaternion.Euler(0, 70, 20));
            Near(item.transform.TransformPoint(localContact), hand.HoldPoint.position, "Attachment remains locked while hand moves");
            hand.Release();
            Check(!item.IsHeld && item.GetComponent<Rigidbody>().detectCollisions, "Release restores collision behavior");
            int count = Grabbable.SurfaceItems.Count;
            Call(item, "OnEnable");
            Check(Grabbable.SurfaceItems.Count == count, "Enable is idempotent");
            Call(item, "OnDisable");
            Check(Grabbable.SurfaceItems.Count == count - 1, "Disable removes registry entry");
            Call(item, "OnEnable");
            Check(Grabbable.SurfaceItems.Count == count, "Pool re-enable restores entry");
        }
        finally
        {
            Call(item, "OnDisable"); Call(farther, "OnDisable");
            Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(farther.gameObject);
            Object.DestroyImmediate(handGo); Object.DestroyImmediate(data);
        }
    }

    static void VerifyGeometry()
    {
        var data = ScriptableObject.CreateInstance<GrabSurfaceData>();
        try
        {
            data.SetBakedGeometry(new[] { Vector3.zero, Vector3.right, Vector3.up }, null, Matrix4x4.identity);
            Check(data.TryClosestPoint(Matrix4x4.identity, new Vector3(.2f, .3f, 1), out var p), "Triangle interior query");
            Near(p, new Vector3(.2f, .3f, 0), "Interior projection");
            data.TryClosestPoint(Matrix4x4.identity, new Vector3(1, 1, 1), out p);
            Near(p, new Vector3(.5f, .5f, 0), "Edge projection");
            data.TryClosestPoint(Matrix4x4.Scale(Vector3.zero), Vector3.one, out p);
            Near(p, Vector3.zero, "Collapsed scale stays finite");
        }
        finally { Object.DestroyImmediate(data); }
    }

    static void VerifyAllocations(List<GameObject> prefabs, Transform parent)
    {
        GameObject prefab = prefabs.Find(p => p.name == "WeaponNailBat");
        Grabbable item = Fixture(parent, prefab.GetComponent<Grabbable>().GrabSurface, Origin);
        var handGo = new GameObject("Allocation grip hand");
        handGo.transform.SetParent(parent, false);
        var hand = handGo.AddComponent<PlayerHand>();
        hand.transform.position = Origin;
        GameObject mini = BagMiniModel.Build(prefab, prefab.name, parent, .065f);
        Vector3 query = Origin + Vector3.one * .02f;
        try
        {
            Action calculate = () => { item.TryGetSurfacePoint(query, out _); hand.FindGrabCandidate(); };
            Action bag = () => { var pose = ItemPickupPose.Capture(prefab, mini.transform.GetChild(0), query); pose.ClampGripToSurface(prefab, query); };
            Measure("surface + candidate selection", calculate);
            Measure("bag pose + surface correction", bag);
        }
        finally { Call(item, "OnDisable"); Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(handGo); Object.DestroyImmediate(mini); }
    }

    static void Measure(string name, Action action)
    {
        for (int i = 0; i < 100; i++) action();
        var watch = new Stopwatch();
        long before = GC.GetAllocatedBytesForCurrentThread();
        watch.Start();
        for (int i = 0; i < 1000; i++) action();
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Check(allocated == 0, name + " warmed allocated bytes=" + allocated);
        Debug.Log($"GRAB BENCH {name}: {allocated} managed bytes / 1000 calls; {watch.Elapsed.TotalMilliseconds / 1000:F4} ms/call in Editor (not Quest timing).");
    }

    // Deliberately different algorithm from the runtime triangle-region implementation.
    static float DistanceToMesh(GameObject prefab, Matrix4x4 rootToWorld, Vector3 point)
    {
        float best = float.PositiveInfinity;
        foreach (MeshFilter filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            Matrix4x4 matrix = rootToWorld * prefab.transform.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            var vertices = filter.sharedMesh.vertices;
            var indices = filter.sharedMesh.triangles;
            for (int i = 0; i < indices.Length; i += 3)
            {
                Vector3 a = matrix.MultiplyPoint3x4(vertices[indices[i]]), b = matrix.MultiplyPoint3x4(vertices[indices[i + 1]]), c = matrix.MultiplyPoint3x4(vertices[indices[i + 2]]);
                Vector3 normal = Vector3.Cross(b - a, c - a);
                float n2 = normal.sqrMagnitude;
                if (n2 > 1e-20f)
                {
                    Vector3 projected = point - normal * (Vector3.Dot(point - a, normal) / n2);
                    if (Vector3.Dot(Vector3.Cross(b - a, projected - a), normal) >= -1e-10f &&
                        Vector3.Dot(Vector3.Cross(c - b, projected - b), normal) >= -1e-10f &&
                        Vector3.Dot(Vector3.Cross(a - c, projected - c), normal) >= -1e-10f)
                        best = Mathf.Min(best, Vector3.Distance(point, projected));
                }
                best = Mathf.Min(best, SegmentDistance(point, a, b), SegmentDistance(point, b, c), SegmentDistance(point, c, a));
            }
        }
        return best;
    }
    static float SegmentDistance(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        return Vector3.Distance(p, a + ab * (ab.sqrMagnitude > 0 ? Mathf.Clamp01(Vector3.Dot(p - a, ab) / ab.sqrMagnitude) : 0));
    }
}
