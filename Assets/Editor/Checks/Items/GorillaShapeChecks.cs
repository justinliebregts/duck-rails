using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using System.Reflection;

public static class GorillaShapeChecks
{
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run in isolated batch mode.");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/WeaponGorilla.prefab");
        var go = UnityEngine.Object.Instantiate(prefab);
        try
        {
            var item = go.GetComponent<Grabbable>();
            var boxes = go.GetComponentsInChildren<BoxCollider>();
            var body = go.GetComponent<Rigidbody>();
            Check(boxes.Length == 3 && go.GetComponentsInChildren<Collider>().Length == 3, "Exactly three physical boxes");
            Check(boxes.All(b => b.attachedRigidbody == body && !b.isTrigger), "All boxes share one Rigidbody");
            for (int i = 1; i < boxes.Length; i++)
                Check(Physics.ComputePenetration(boxes[i - 1], boxes[i - 1].transform.position, boxes[i - 1].transform.rotation,
                    boxes[i], boxes[i].transform.position, boxes[i].transform.rotation, out _, out _), "Adjacent boxes overlap");
            foreach (var mesh in go.GetComponentsInChildren<MeshFilter>())
            {
                var vertices = mesh.sharedMesh.vertices;
                var triangles = mesh.sharedMesh.triangles;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = mesh.transform.TransformPoint(vertices[triangles[i]]);
                    Vector3 b = mesh.transform.TransformPoint(vertices[triangles[i + 1]]);
                    Vector3 c = mesh.transform.TransformPoint(vertices[triangles[i + 2]]);
                    foreach (var point in new[] { a, (a + b) * .5f, (a + b + c) / 3f })
                        Check(boxes.Any(box => Vector3.Distance(box.ClosestPoint(point), point) < .01f), "Boxes cover mesh vertices and triangle interiors");
                }
            }
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var configured = (BoxCollider[])typeof(Grabbable).GetField("locomotionBoxes", flags).GetValue(item);
            var primary = (Collider)typeof(Grabbable).GetField("locomotionCollider", flags).GetValue(item);
            Check(configured.Length == 3 && boxes.All(configured.Contains), "All physical boxes feed shared samples");
            var rebuild = typeof(Grabbable).GetMethod("RebuildLocomotionSamples", flags);
            var copySwing = typeof(Grabbable).GetMethod("CopySwingSampleWorldPositions", flags);
            var samples = new Vector3[Grabbable.MaxLocomotionSamples];
            var combat = new Vector3[samples.Length];
            var held = new Vector3[samples.Length];
            rebuild.Invoke(item, null);
            int count = item.CopySampleWorldPositions(samples);
            Check(count == 8, "Curved shape stays within eight samples");
            Check(samples.Min(p => p.z) < -.35f, "Samples reach the curved tip outside the old box");
            // Every segment has nearby samples; adjacent segment samples overlap at joins.
            foreach (var box in boxes)
            {
                Vector3 start = box.transform.TransformPoint(box.center - Vector3.up * box.size.y * .5f);
                Vector3 end = box.transform.TransformPoint(box.center + Vector3.up * box.size.y * .5f);
                for (int j = 0; j <= 20; j++)
                {
                    Vector3 point = Vector3.Lerp(start, end, j / 20f);
                    Check(samples.Min(p => Vector3.Distance(p, point)) <= item.LocomotionRadius + .001f, "Segment centerline covered");
                }
            }
            var handObject = new GameObject("Shape check hand");
            try
            {
                var hand = handObject.AddComponent<PlayerHand>();
                typeof(Grabbable).GetField("holder", flags).SetValue(item, hand);
                for (int pose = 0; pose < 4; pose++)
                {
                    go.transform.SetPositionAndRotation(new Vector3(pose, 2, -3), Quaternion.Euler(pose * 31, pose * 57, pose * 17));
                    go.transform.localScale = Vector3.one * (1f + pose * .2f);
                    rebuild.Invoke(item, null);
                    count = item.CopySampleWorldPositions(samples);
                    copySwing.Invoke(item, new object[] { combat, primary.transform.localToWorldMatrix });
                    Check(item.TryGetLocomotionSamples(held, out int heldCount, out float radius) && heldCount == count && radius == item.LocomotionRadius, "Held samples available");
                    for (int i = 0; i < count; i++)
                        Check(Vector3.Distance(samples[i], combat[i]) < .00001f && Vector3.Distance(samples[i], held[i]) < .00001f, "Combat/held sample parity after transform");
                }
                typeof(Grabbable).GetField("holder", flags).SetValue(item, null);
            }
            finally { UnityEngine.Object.DestroyImmediate(handObject); }
            // Existing single-box items retain their original sample builder.
            typeof(Grabbable).GetField("locomotionBoxes", flags).SetValue(item, Array.Empty<BoxCollider>());
            rebuild.Invoke(item, null);
            var legacy = LocomotionSamples.Build(primary, out float legacyRadius);
            Check(item.LocomotionSampleCount == legacy.Length && item.LocomotionRadius == legacyRadius, "Single collider fallback preserved");
            Debug.Log("GorillaShapeChecks: ALL PASSED");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }
}
