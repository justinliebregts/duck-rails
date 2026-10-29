using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class FryPanShapeChecks
{
    const string PrefabPath = "Assets/Prefabs/Weapons/WeaponFryPan.prefab";
    const string HullPath = "Assets/Models/WeaponFryPan/WeaponFryPanCollision.asset";
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;

    // Explicit authoring command; never runs automatically on import.
    public static void Refit()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(HullPath);
        if (mesh == null)
        {
            mesh = new Mesh { name = "Fry pan head collision hull" };
            AssetDatabase.CreateAsset(mesh, HullPath);
        }
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int side = 0; side < 2; side++)
            for (int i = 0; i < 24; i++)
            {
                float angle = (i + .5f) * Mathf.PI / 12;
                vertices.Add(new Vector3(.193f * Mathf.Cos(angle), .4164f + .193f * Mathf.Sin(angle), side == 0 ? -.002f : .0692f));
            }
        for (int i = 0; i < 24; i++)
        {
            int next = (i + 1) % 24;
            triangles.AddRange(new[] { i, next, i + 24, next, next + 24, i + 24 });
            if (i > 0 && i < 23)
                triangles.AddRange(new[] { 0, i + 1, i, 24, i + 24, i + 25 });
        }
        mesh.Clear();
        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        EditorUtility.SetDirty(mesh);

        var go = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            // Retain the existing primary box reference for pickup and sample coordinates.
            var handle = go.GetComponent<BoxCollider>();
            handle.center = new Vector3(0, .125f, .056f);
            handle.size = new Vector3(.050f, .254f, .088f);
            var head = go.GetComponent<MeshCollider>();
            if (head == null) head = go.AddComponent<MeshCollider>();
            head.sharedMesh = mesh;
            head.convex = true;
            head.isTrigger = false;
            var points = new List<Vector3>();
            for (int ring = 1; ring <= 2; ring++)
            {
                int count = ring == 1 ? 4 : 12;
                float radius = ring == 1 ? .055f : .155f;
                for (int i = 0; i < count; i++)
                {
                    float angle = i * 2 * Mathf.PI / count + (ring == 1 ? Mathf.PI / 4 : 0);
                    points.Add(new Vector3(radius * Mathf.Cos(angle), .4164f + radius * Mathf.Sin(angle), ring == 1 ? .025f : .0336f));
                }
            }
            for (int i = 0; i < 2; i++)
            {
                float y = .045f + i * .110f;
                points.Add(new Vector3(0, y, .083f - y * .17f));
            }
            var serialized = new SerializedObject(go.GetComponent<Grabbable>());
            var offsets = serialized.FindProperty("locomotionSampleOffsets");
            offsets.arraySize = points.Count;
            for (int i = 0; i < points.Count; i++) offsets.GetArrayElementAtIndex(i).vector3Value = points[i];
            serialized.FindProperty("locomotionSampleRadius").floatValue = .07f;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(go); }
        AssetDatabase.SaveAssets();
        Run();
    }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }

    public static void Run()
    {
        var go = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath));
        try
        {
            var item = go.GetComponent<Grabbable>();
            var colliders = go.GetComponentsInChildren<Collider>();
            var primary = go.GetComponent<BoxCollider>();
            Check(colliders.Length == 2 && go.GetComponent<MeshCollider>().convex, "Head hull plus handle box required");
            Check(colliders.All(c => !c.isTrigger && c.attachedRigidbody == go.GetComponent<Rigidbody>()), "Solid colliders must share one body");
            Check(primary.size.x < .06f, "Handle must not retain the full-width box");
            var heldObject = new GameObject("Fry pan test hand");
            try
            {
                typeof(Grabbable).GetField("holder", Flags).SetValue(item, heldObject.AddComponent<PlayerHand>());
                for (int pose = 0; pose < 3; pose++)
                {
                    go.transform.SetPositionAndRotation(new Vector3(pose, 2, -3), Quaternion.Euler(pose * 37, pose * 53, pose * 19));
                    go.transform.localScale = Vector3.one * (1 + pose * .25f);
                    typeof(Grabbable).GetMethod("RebuildLocomotionSamples", Flags).Invoke(item, null);
                    Physics.SyncTransforms();
                    var samples = new Vector3[Grabbable.MaxLocomotionSamples];
                    var held = new Vector3[samples.Length];
                    var combat = new Vector3[samples.Length];
                    int count = item.CopySampleWorldPositions(samples);
                    Check(count == 18, "Expected 12 rim, four interior and two handle samples");
                    Check(item.TryGetLocomotionSamples(held, out int heldCount, out float radius) && heldCount == count, "Held samples missing");
                    typeof(Grabbable).GetMethod("CopySwingSampleWorldPositions", Flags).Invoke(item, new object[] { combat, primary.transform.localToWorldMatrix });
                    for (int i = 0; i < count; i++)
                        Check(Vector3.Distance(samples[i], held[i]) < .00001f && Vector3.Distance(samples[i], combat[i]) < .00001f, "Hit/locomotion samples differ");
                    Check(Mathf.Abs(radius - .07f * go.transform.localScale.x) < .00001f, "Sample radius changed");
                    for (int i = 0; i < 12; i++)
                    {
                        float angle = i * Mathf.PI / 6;
                        Vector3 rim = go.transform.TransformPoint(new Vector3(.155f * Mathf.Cos(angle), .4164f + .155f * Mathf.Sin(angle), .0336f));
                        Check(samples.Take(count).Any(s => Vector3.Distance(s, rim) < .00001f), "Original rim sample moved");
                    }
                    float worstGap = 0;
                    foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
                    {
                        var mesh = filter.sharedMesh;
                        var vertices = mesh.vertices;
                        var indices = mesh.triangles;
                        for (int t = 0; t < indices.Length; t += 3)
                            for (int a = 0; a <= 48; a++)
                                for (int b = 0; b <= 48 - a; b++)
                                {
                                    Vector3 local = (vertices[indices[t]] * a + vertices[indices[t + 1]] * b + vertices[indices[t + 2]] * (48 - a - b)) / 48;
                                    Vector3 p = filter.transform.TransformPoint(local);
                                    float gap = (samples.Take(count).Min(s => Vector3.Distance(s, p)) - radius) / go.transform.localScale.x;
                                    worstGap = Mathf.Max(worstGap, gap);
                                    // Accepted approximation: at most 0.25 mm at original scale.
                                    Check(gap <= .00025f, "Uncovered visual face/edge at " + local);
                                    Check(colliders.Any(c => Vector3.Distance(c.ClosestPoint(p), p) < .003f), "Physical collider misses mesh at " + local);
                                }
                    }
                    Debug.Log($"FryPanShapeChecks pose {pose}: worst surface gap {worstGap * 1000:F3} mm at original scale");
                    // The original box collided with these empty spaces beside the handle.
                    Vector3 empty = go.transform.TransformPoint(new Vector3(.14f, .1f, .05f));
                    Check(colliders.All(c => Vector3.Distance(c.ClosestPoint(empty), empty) > .08f), "Phantom handle collision");
                }
                typeof(Grabbable).GetField("holder", Flags).SetValue(item, null);
            }
            finally { UnityEngine.Object.DestroyImmediate(heldObject); }
            Debug.Log("FryPanShapeChecks: ALL PASSED");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
        GorillaShapeChecks.Run();
    }
}
