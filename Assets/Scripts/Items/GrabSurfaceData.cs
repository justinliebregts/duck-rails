using System;
using UnityEngine;

// Editor-baked, item-local geometry shared by every instance. Queried only on grip events.
public sealed class GrabSurfaceData : ScriptableObject
{
    [SerializeField, HideInInspector] Vector3[] triangles = Array.Empty<Vector3>();
    [SerializeField, HideInInspector] Bounds localBounds;
    [SerializeField, HideInInspector] Mesh referenceMesh;
    [SerializeField, HideInInspector] Matrix4x4 referenceToRoot;

    public bool IsValid => triangles != null && triangles.Length >= 3 && triangles.Length % 3 == 0;
    public int TriangleCount => triangles.Length / 3;
    public Mesh ReferenceMesh => referenceMesh;
    public Matrix4x4 ReferenceToRoot => referenceToRoot;

    // Transform the bounds conservatively, including nonuniform scale and rotated parents.
    public bool WithinReach(Matrix4x4 toWorld, Vector3 point, float radiusSquared)
    {
        Vector3 e = localBounds.extents;
        Vector3 x = toWorld.MultiplyVector(new Vector3(e.x, 0, 0));
        Vector3 y = toWorld.MultiplyVector(new Vector3(0, e.y, 0));
        Vector3 z = toWorld.MultiplyVector(new Vector3(0, 0, e.z));
        Vector3 extent = Abs(x) + Abs(y) + Abs(z);
        var bounds = new Bounds(toWorld.MultiplyPoint3x4(localBounds.center), extent * 2f);
        return bounds.SqrDistance(point) <= radiusSquared;
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));

    public bool TryClosestPoint(Matrix4x4 toWorld, Vector3 query, out Vector3 closest)
    {
        closest = default;
        if (!IsValid) return false;
        float best = float.PositiveInfinity;
        // Evaluate in world space so distance remains correct under nonuniform scaling.
        for (int i = 0; i < triangles.Length; i += 3)
        {
            Vector3 p = ClosestOnTriangle(query, toWorld.MultiplyPoint3x4(triangles[i]),
                toWorld.MultiplyPoint3x4(triangles[i + 1]), toWorld.MultiplyPoint3x4(triangles[i + 2]));
            float d = (p - query).sqrMagnitude;
            if (d >= best) continue;
            best = d;
            closest = p;
        }
        return !float.IsPositiveInfinity(best);
    }

    static Vector3 ClosestOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a, ac = c - a, ap = p - a;
        // A zero scale during a display transition can collapse otherwise valid triangles.
        if (Vector3.Cross(ab, ac).sqrMagnitude < 1e-20f)
        {
            Vector3 x = ClosestOnSegment(p, a, b), y = ClosestOnSegment(p, b, c), z = ClosestOnSegment(p, c, a);
            Vector3 best = (x - p).sqrMagnitude <= (y - p).sqrMagnitude ? x : y;
            return (best - p).sqrMagnitude <= (z - p).sqrMagnitude ? best : z;
        }
        float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f) return a;
        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3) return b;
        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f) return a + ab * (d1 / (d1 - d3));
        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6) return c;
        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f) return a + ac * (d2 / (d2 - d6));
        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && d4 - d3 >= 0f && d5 - d6 >= 0f)
            return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        float inverse = 1f / (va + vb + vc);
        return a + ab * (vb * inverse) + ac * (vc * inverse);
    }

    static Vector3 ClosestOnSegment(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector3 ab = b - a;
        float length = ab.sqrMagnitude;
        return length > 0f ? a + ab * Mathf.Clamp01(Vector3.Dot(p - a, ab) / length) : a;
    }

#if UNITY_EDITOR
    // Return false for an identical bake, preventing import/save feedback loops.
    public bool SetBakedGeometry(Vector3[] points, Mesh mesh, Matrix4x4 model)
    {
        bool same = referenceMesh == mesh && referenceToRoot == model && triangles.Length == points.Length;
        for (int i = 0; same && i < points.Length; i++) same = triangles[i].Equals(points[i]);
        if (same) return false;
        triangles = points;
        referenceMesh = mesh;
        referenceToRoot = model;
        localBounds = new Bounds(points[0], Vector3.zero);
        foreach (Vector3 point in points) localBounds.Encapsulate(point);
        return true;
    }
#endif
}
