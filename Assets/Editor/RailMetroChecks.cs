#if UNITY_EDITOR
using System;
using System.Linq;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.AI;
/// <summary>V3 Passenger Station checks and review captures. Run with the station bake scene open.</summary>
public static class RailMetroChecks
{
    const string Shots = "output/passenger-station-v3";
    [MenuItem("Duck Rails/Authoring/Abandoned Station/Capture review")]
    public static void Capture() => Capture(false);
    public static void Capture(bool bright)
    {
        var go = new GameObject("Metro review camera"); var cam = go.AddComponent<Camera>(); cam.nearClipPlane = .05f; cam.farClipPlane = 200; cam.fieldOfView = 78; cam.useOcclusionCulling = false;
        GameObject fill = null;
        if (bright) { fill = new GameObject("Temporary review fill"); var l = fill.AddComponent<Light>(); l.type = LightType.Directional; l.intensity = 1.2f; l.shadows = LightShadows.None; fill.transform.rotation = Quaternion.Euler(50, 30, 0); }
        Directory.CreateDirectory(Shots);
        string suffix = bright ? "-bright" : "";
        try
        {
            Shot("platform-1" + suffix, new(-17.5f, -1.4f, 58), new(-18, -1.6f, 98));
            Shot("tunnel-approach" + suffix, new(-10.4f, -1.2f, 38), new(-12, -2.2f, 75));
            Shot("platform-1-edge" + suffix, new(-14.9f, -1.5f, 70), new(-12.3f, -2.6f, 92));
            Shot("entrance-portal" + suffix, new(0, 2.5f, -14), new(0, 6f, 20));
            Shot("exit-portal" + suffix, new(0, 2.5f, 174), new(0, 6f, 140));
            Shot("collapsed-tunnel" + suffix, new(1, -1.4f, 103), new(6.2f, -2f, 116));
            Shot("maintenance-hall-clutter" + suffix, new(15, -1.4f, 78), new(15.2f, -1.9f, 104));
            Shot("transfer-hall-clutter" + suffix, new(-39, -1.4f, 44), new(-39.5f, -1.9f, 70));
            Shot("lost-and-found" + suffix, new(-30.2f, -1.2f, 37), new(-33.5f, -2.2f, 50));
            Shot("pump-room" + suffix, new(18.5f, -.8f, 113.5f), new(28, -2f, 126));
            Shot("platform-1-escalators" + suffix, new(-20.5f, -1.4f, 82), new(-21, -1f, 66));
            Shot("ticket-concourse" + suffix, new(-25, 2.6f, 51), new(-2, 2.1f, 42));
            Shot("mens-toilet" + suffix, new(-26f, -1.3f, 57.6f), new(-34.5f, -2.3f, 54));
            Shot("womens-toilet" + suffix, new(-26f, -1.3f, 59.2f), new(-34.5f, -2.3f, 62.5f));
            Shot("train-exterior" + suffix, new(1.2f, -1.3f, 64), new(6, -2.2f, 85));
            Shot("station-office" + suffix, new(-2.2f, 2.6f, 44), new(2.8f, 1.8f, 45.8f));
            Shot("escalator-down-to-platform-3" + suffix, new(-2.4f, 2.6f, 51.5f), new(-2.4f, -1.5f, 66));
            Shot("platform-3" + suffix, new(1, -1.4f, 56), new(2.5f, -1.8f, 100));
            Shot("wreck-interior" + suffix, new(6, -1.4f, 52f), new(6, -1.6f, 88));
            Shot("cave-in" + suffix, new(.6f, -1.5f, 92), new(6, -1f, 110));
            Shot("transfer-hall" + suffix, new(-39, -1.4f, 30), new(-39, -1.6f, 80));
            Shot("konbini" + suffix, new(-26, -1.4f, 86), new(-33, -1.8f, 99));
            Shot("maintenance-hall" + suffix, new(15, -1.4f, 44), new(15, -1.6f, 90));
            // Batch 4 review views.
            Shot("tunnel-inner-mouth" + suffix, new(-12.3f, -1.6f, 44), new(-12, -2.2f, 60));
            Shot("tunnel-inner-mouth-north" + suffix, new(-12.3f, -1.6f, 116), new(-12, -2.2f, 100));
            Shot("portal-close" + suffix, new(0, 1.6f, 1.5f), new(0, 2.0f, 12));
            Shot("portal-close-north" + suffix, new(0, 1.6f, 158.5f), new(0, 2.0f, 148));
            Shot("mens-urinals" + suffix, new(-32.4f, -1.5f, 56.4f), new(-33.1f, -2.4f, 53.2f));
            Shot("mens-sign" + suffix, new(-23.4f, -1.05f, 57.4f), new(-24.9f, -1.1f, 53.95f));
            Shot("womens-sign" + suffix, new(-23.4f, -1.05f, 59.6f), new(-24.9f, -1.1f, 63.45f));
            Shot("coin-lockers" + suffix, new(-26.0f, -1.3f, 79), new(-34, -2.2f, 79));
            Shot("crew-lockers" + suffix, new(15.6f, -1.3f, 62), new(26, -2.2f, 62));
            Shot("cable-gallery" + suffix, new(-18.4f, -1.3f, 113), new(-25, -2.1f, 132));
            Shot("cable-vault" + suffix, new(-30.8f, -1.3f, 125.5f), new(-35, -2.2f, 119));
            Shot("cave-in-up" + suffix, new(3.0f, -1.6f, 102.5f), new(6, 3.6f, 102.5f));
            Shot("pump-room-door" + suffix, new(15, -1.4f, 116.5f), new(24, -.9f, 116.5f));
            Shot("escalator-light" + suffix, new(-21.2f, -.3f, 62.2f), new(-21.2f, 1.6f, 56.5f));
            Shot("train-interior-open-windows" + suffix, new(6, -1.4f, 74f), new(4.6f, -1.8f, 66));
        }
        finally { UnityEngine.Object.DestroyImmediate(go); if (fill) UnityEngine.Object.DestroyImmediate(fill); }
        void Shot(string name, Vector3 eye, Vector3 target)
        {
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            var rt = new RenderTexture(1440, 900, 24); var tex = new Texture2D(1440, 900, TextureFormat.RGB24, false); var prior = RenderTexture.active;
            try { cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt; tex.ReadPixels(new Rect(0, 0, 1440, 900), 0, 0); tex.Apply(); File.WriteAllBytes(Shots + "/" + name + ".png", tex.EncodeToPNG()); }
            finally { cam.targetTexture = null; RenderTexture.active = prior; UnityEngine.Object.DestroyImmediate(rt); UnityEngine.Object.DestroyImmediate(tex); }
        }
    }

    /// <summary>
    /// Batch 4: every mesh (not just boxes), triangle by triangle. Pairs of renderers whose triangles share a
    /// plane, face the same way and overlap by more than 40 cm² in total, unless the shared area is pressed
    /// against an opposite-facing surface (an underside on a floor, a back against a wall), which hides it.
    /// </summary>
    public static List<string> ZFightingAll(RailBlock b, out int triangles)
    {
        const int env = 1 << 6;
        var tris = new List<(Vector3 n, float d, Vector3 a, Vector3 b, Vector3 c, int r)>();
        var rends = new List<Renderer>();
        foreach (var r in b.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || !r.enabled || r.GetComponent<TMPro.TMP_Text>()) continue;
            var m = mf.sharedMesh; var v = m.vertices; var t = r.transform; int ri = rends.Count; rends.Add(r);
            var w = v.Select(p => t.TransformPoint(p)).ToArray();
            for (int s = 0; s < m.subMeshCount; s++)
            {
                var idx = m.GetTriangles(s);
                for (int i = 0; i < idx.Length; i += 3)
                {
                    Vector3 a = w[idx[i]], c1 = w[idx[i + 1]], c2 = w[idx[i + 2]]; var cr = Vector3.Cross(c1 - a, c2 - a);
                    if (cr.sqrMagnitude < 1e-10f) continue; var n = cr.normalized;
                    tris.Add((n, Vector3.Dot(n, a), a, c1, c2, ri));
                }
            }
        }
        triangles = tris.Count;
        string Key(Vector3 n, float d, int off) => Mathf.RoundToInt(n.x * 200) + "," + Mathf.RoundToInt(n.y * 200) + "," + Mathf.RoundToInt(n.z * 200) + "|" + (Mathf.RoundToInt(d * 500) + off);
        var buckets = new Dictionary<string, List<int>>();
        for (int i = 0; i < tris.Count; i++) { string k = Key(tris[i].n, tris[i].d, 0); if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<int>(); l.Add(i); }
        float TriOverlap(int i, int j) => TriOverlapAt(i, j, out _);
        float TriOverlapAt(int i, int j, out Vector3 centroid)
        {
            var A = tris[i]; var B = tris[j]; centroid = A.a;
            var u = Vector3.Cross(A.n, Mathf.Abs(A.n.y) < .9f ? Vector3.up : Vector3.right).normalized; var vv = Vector3.Cross(A.n, u);
            Vector2 P(Vector3 q) => new(Vector3.Dot(q, u), Vector3.Dot(q, vv));
            Vector2 a0 = P(A.a), a1 = P(A.b), a2 = P(A.c);
            var poly = new List<Vector2> { P(B.a), P(B.b), P(B.c) };
            float orient = Mathf.Sign((a1.x - a0.x) * (a2.y - a0.y) - (a1.y - a0.y) * (a2.x - a0.x));
            foreach (var (e0, e1) in new[] { (a0, a1), (a1, a2), (a2, a0) })
            {
                var o = new List<Vector2>();
                for (int k = 0; k < poly.Count; k++)
                {
                    var p = poly[k]; var q = poly[(k + 1) % poly.Count];
                    float fp = orient * ((e1.x - e0.x) * (p.y - e0.y) - (e1.y - e0.y) * (p.x - e0.x)), fq = orient * ((e1.x - e0.x) * (q.y - e0.y) - (e1.y - e0.y) * (q.x - e0.x));
                    if (fp >= 0) o.Add(p); if (fp * fq < 0) o.Add(Vector2.Lerp(p, q, fp / (fp - fq)));
                }
                poly = o; if (poly.Count < 3) return 0;
            }
            float ar = 0; var cs = Vector2.zero; foreach (var p in poly) cs += p; cs /= poly.Count;
            for (int k = 0; k < poly.Count; k++) { var p = poly[k]; var q = poly[(k + 1) % poly.Count]; ar += p.x * q.y - q.x * p.y; }
            centroid = u * cs.x + vv * cs.y + A.n * A.d;
            return Mathf.Abs(ar) / 2;
        }
        // Buried: the space just in front of the shared area is inside some other solid, so nobody can see it.
        bool Buried(Vector3 at, Vector3 n, int r1, int r2)
        {
            foreach (var c in Physics.OverlapSphere(at + n * .006f, .003f, env, QueryTriggerInteraction.Ignore))
                if (c.gameObject != rends[r1].gameObject && c.gameObject != rends[r2].gameObject) return true;
            return false;
        }
        bool Bounded(int i, int j)
        {
            var A = tris[i]; var B = tris[j];
            var amin = Vector3.Min(Vector3.Min(A.a, A.b), A.c); var amax = Vector3.Max(Vector3.Max(A.a, A.b), A.c);
            var bmin = Vector3.Min(Vector3.Min(B.a, B.b), B.c); var bmax = Vector3.Max(Vector3.Max(B.a, B.b), B.c);
            const float e = .002f;   // conservative prefilter; TriOverlap gives the real area
            return amin.x <= bmax.x + e && bmin.x <= amax.x + e && amin.y <= bmax.y + e && bmin.y <= amax.y + e && amin.z <= bmax.z + e && bmin.z <= amax.z + e;
        }
        // Pressed: a triangle coincident with an opposite-facing triangle of another renderer is hidden there.
        bool Pressed(int i, int other)
        {
            var A = tris[i]; float pressed = 0;
            for (int off = -1; off <= 1; off++)
                if (buckets.TryGetValue(Key(-A.n, -A.d, off), out var l))
                    foreach (int j in l) { if (tris[j].r == A.r || tris[j].r == other || Mathf.Abs(tris[j].d + A.d) > .0015f || !Bounded(i, j)) continue; pressed += TriOverlap(i, j); }
            // Polish: only hidden when (nearly) all of it is pressed; a thin stripe pressed on part of a face used to hide a
            // real z-fight on the rest of it (the wreck car corners).
            return pressed >= .8f * Vector3.Cross(A.b - A.a, A.c - A.a).magnitude / 2;
        }
        var pairArea = new Dictionary<long, (float area, Vector3 at)>();
        for (int i = 0; i < tris.Count; i++)
        {
            var f = tris[i];
            for (int off = -1; off <= 1; off++)
            {
                if (!buckets.TryGetValue(Key(f.n, f.d, off), out var l)) continue;
                foreach (int j in l)
                {
                    if (j <= i) continue; var g = tris[j];
                    if (g.r == f.r || Vector3.Dot(f.n, g.n) < .9999f || Mathf.Abs(f.d - g.d) > .0015f || !Bounded(i, j)) continue;
                    float area = TriOverlapAt(i, j, out var at); if (area < 1e-5f) continue;
                    if (Buried(at, f.n, f.r, g.r) || Pressed(i, g.r) || Pressed(j, f.r)) continue;
                    // Undersides sitting at a walking level (main floor, concourse deck, track beds) are never seen from below.
                    if (f.n.y < -.99f && new[] { -3f, 1f, -4f, -4.3f }.Any(level => Mathf.Abs(at.y - level) < .012f)) continue;
                    long key = ((long)Mathf.Min(f.r, g.r) << 32) | (uint)Mathf.Max(f.r, g.r);
                    pairArea.TryGetValue(key, out var e); pairArea[key] = (e.area + area, at);
                }
            }
        }
        var result = new List<string>();
        foreach (var kv in pairArea.Where(kv => kv.Value.area > .004f).OrderByDescending(kv => kv.Value.area))
        {
            var r1 = rends[(int)(kv.Key >> 32)]; var r2 = rends[(int)(kv.Key & 0xffffffff)];
            result.Add(Path(r1.transform, b.transform) + " <> " + Path(r2.transform, b.transform) + " at " + kv.Value.at.ToString("F2") + " area " + kv.Value.area.ToString("F3"));
        }
        return result;
        static string Path(Transform t, Transform root) { var s = t.name; for (var p = t.parent; p && p != root && p.parent != root; p = p.parent) s = p.name + "/" + s; return s; }
    }

    static bool HangingSign(Transform t)
    {
        for (var p = t; p; p = p.parent) if (p.name.StartsWith("Sign ") && !p.GetComponent<Renderer>()) return p.Cast<Transform>().Any(c => c.name == "Sign hanger rod");
        return false;
    }
    static string Path2(Transform t, Transform root) { var s = t.name; for (var p = t.parent; p && p != root && p.parent != root; p = p.parent) s = p.name + "/" + s; return s; }

    /// <summary>Pairs of renderer faces that share a plane, face the same way and overlap by more than 40 cm².</summary>
    public static List<string> ZFighting(RailBlock b)
    {
        var faces = new List<(Vector3 n, float d, Vector3 c, Vector3 u, Vector3 v, float hu, float hv, Renderer r)>();
        foreach (var r in b.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || !r.enabled || mf.sharedMesh.vertexCount != 24) continue;   // boxes only
            var t = r.transform; var mb = mf.sharedMesh.bounds; var ls = t.lossyScale;
            var center = t.TransformPoint(mb.center);
            var axes = new[] { t.right, t.up, t.forward }; var half = Vector3.Scale(mb.extents, ls);
            for (int i = 0; i < 3; i++)
                foreach (float sign in new[] { -1f, 1f })
                {
                    var n = axes[i] * sign; var c = center + n * Mathf.Abs(half[i]);
                    int a = (i + 1) % 3, bb = (i + 2) % 3;
                    faces.Add((n, Vector3.Dot(n, c), c, axes[a], axes[bb], Mathf.Abs(half[a]), Mathf.Abs(half[bb]), r));
                }
        }
        string Key(Vector3 n, float d, int off) => Mathf.RoundToInt(n.x * 200) + "," + Mathf.RoundToInt(n.y * 200) + "," + Mathf.RoundToInt(n.z * 200) + "|" + (Mathf.RoundToInt(d * 500) + off);
        var buckets = new Dictionary<string, List<int>>();
        for (int i = 0; i < faces.Count; i++)
        {
            string k = Key(faces[i].n, faces[i].d, 0);
            if (!buckets.TryGetValue(k, out var l)) buckets[k] = l = new List<int>();
            l.Add(i);
        }
        bool RestsOnSurface((Vector3 n, float d, Vector3 c, Vector3 u, Vector3 v, float hu, float hv, Renderer r) f)
        {
            var up = Vector3.up; float y = f.c.y;
            for (int off = -1; off <= 1; off++)
                if (buckets.TryGetValue(Key(up, y, off), out var l))
                    foreach (int j in l) { var h = faces[j]; if (h.r != f.r && Mathf.Abs(h.c.y - y) < .003f && Overlap(h, f) > .002f) return true; }
            return false;
        }
        var result = new List<string>(); var seen = new HashSet<long>();
        for (int i = 0; i < faces.Count; i++)
        {
            var f = faces[i];
            for (int off = -1; off <= 1; off++)
            {
                if (!buckets.TryGetValue(Key(f.n, f.d, off), out var l)) continue;
                foreach (int j in l)
                {
                    if (j <= i) continue; var g = faces[j];
                    if (g.r == f.r || Vector3.Dot(f.n, g.n) < .9999f || Mathf.Abs(f.d - g.d) > .0015f) continue;
                    // Visible cases: floors, ceilings and tops (horizontal faces), and decals/thin visuals with
                    // no collider. Vertical faces of solid blocks meet inside walls at joints and are not seen.
                    bool horizontal = Mathf.Abs(f.n.y) > .99f;
                    float area = Overlap(f, g);
                    // Large vertical overlaps between solid blocks are visible too (platform edges); small ones are joints.
                    if (!horizontal && (f.r.GetComponent<Collider>() || g.r.GetComponent<Collider>()) && area < .25f) continue;
                    if (area < .004f) continue;   // ignore slivers under 40 cm²
                    // Undersides resting on a floor or slab are hidden by it.
                    if (f.n.y < -.99f && RestsOnSurface(f)) continue;
                    long key = ((long)Mathf.Min(f.r.GetInstanceID(), g.r.GetInstanceID()) << 32) ^ (uint)Mathf.Max(f.r.GetInstanceID(), g.r.GetInstanceID());
                    if (!seen.Add(key)) continue;
                    result.Add(Path(f.r.transform, b.transform) + " <> " + Path(g.r.transform, b.transform) + " at " + f.c.ToString("F2") + " area " + area.ToString("F3"));
                }
            }
        }
        return result;
        static string Path(Transform t, Transform root) { var s = t.name; for (var p = t.parent; p && p != root && p.parent != root; p = p.parent) s = p.name + "/" + s; return s; }
    }

    // Area of overlap of two coplanar rectangles (convex polygon clipping in face A's 2D basis).
    static float Overlap((Vector3 n, float d, Vector3 c, Vector3 u, Vector3 v, float hu, float hv, Renderer r) a, (Vector3 n, float d, Vector3 c, Vector3 u, Vector3 v, float hu, float hv, Renderer r) b)
    {
        Vector2 P(Vector3 w) { var q = w - a.c; return new Vector2(Vector3.Dot(q, a.u), Vector3.Dot(q, a.v)); }
        var poly = new List<Vector2> { P(b.c - b.u * b.hu - b.v * b.hv), P(b.c + b.u * b.hu - b.v * b.hv), P(b.c + b.u * b.hu + b.v * b.hv), P(b.c - b.u * b.hu + b.v * b.hv) };
        // Clip against the four edges of A: |x| <= hu, |y| <= hv.
        foreach (var (axis, limit, sign) in new[] { (0, a.hu, 1f), (0, a.hu, -1f), (1, a.hv, 1f), (1, a.hv, -1f) })
        {
            var output = new List<Vector2>();
            for (int i = 0; i < poly.Count; i++)
            {
                var p = poly[i]; var q = poly[(i + 1) % poly.Count];
                float fp = limit - sign * p[axis], fq = limit - sign * q[axis];
                if (fp >= 0) output.Add(p);
                if (fp * fq < 0) output.Add(Vector2.Lerp(p, q, fp / (fp - fq)));
            }
            poly = output; if (poly.Count < 3) return 0;
        }
        float area = 0; for (int i = 0; i < poly.Count; i++) { var p = poly[i]; var q = poly[(i + 1) % poly.Count]; area += p.x * q.y - q.x * p.y; }
        return Mathf.Abs(area) / 2;
    }

    [MenuItem("Duck Rails/Authoring/Abandoned Station/Validate geometry")]
    public static void Run() => Run(true);
    public static void Run(bool requireGraph)
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying || Lightmapping.isRunning) throw new Exception("Wrong project/mode");
        var b = UnityEngine.Object.FindFirstObjectByType<RailBlock>(); Physics.SyncTransforms(); var lines = new List<string>();
        const int env = 1 << 6;
        void Check(bool ok, string label) => lines.Add((ok ? "PASS " : "FAIL ") + label);
        void Walk(Vector3 p, string label)
        {
            Check(Physics.Raycast(p + Vector3.up * .1f, Vector3.down, out var h, .25f, env) && Mathf.Abs(h.point.y - p.y) < .05f, "floor " + label);
            var hits = Physics.OverlapCapsule(p + Vector3.up * .35f, p + Vector3.up * 1.75f, .3f, env);
            Check(hits.Length == 0, "body " + label + " " + string.Join(",", hits.Select(c => c.name).Distinct()));
            // Enclosure: from every walkable point, eight level rays must hit station geometry (no missing walls).
            if (!label.StartsWith("tread"))
                for (int k = 0; k < 8; k++)
                {
                    var dir = Quaternion.Euler(0, k * 45, 0) * Vector3.forward;
                    if (!Physics.Raycast(p + Vector3.up * 1.2f, dir, 200f, env, QueryTriggerInteraction.Ignore)) { Check(false, "enclosure " + label + " open toward " + (k * 45) + " deg"); break; }
                }
            // Batch 4: walls must reach the ceiling. Where a level ray meets a wall, the same ray just under the
            // local ceiling must stop at that wall too (catches open bands above walls, like the pump room door).
            if (!label.StartsWith("tread") && Physics.Raycast(p + Vector3.up * 1.0f, Vector3.up, out var ceil, 4f, env, QueryTriggerInteraction.Ignore))
                for (int k = 0; k < 8; k++)
                {
                    var dir = Quaternion.Euler(0, k * 45, 0) * Vector3.forward;
                    if (!Physics.Raycast(p + Vector3.up * 1.2f, dir, out var low, 60f, env, QueryTriggerInteraction.Ignore) || !low.collider.name.ToLower().Contains("wall") || low.collider.name.StartsWith("Car ")) continue;   // train car walls are low by design
                    var hiStart = new Vector3(p.x, ceil.point.y - .12f, p.z);
                    if (!Physics.Raycast(hiStart, dir, out var high, 60f, env, QueryTriggerInteraction.Ignore) || high.distance > low.distance + .6f)
                    { Check(false, "gap above " + low.collider.name + " from " + label + " toward " + (k * 45) + " deg"); break; }
                }
        }
        void Line(Vector3 a, Vector3 c, float step, string label)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt((c - a).magnitude / step));
            for (int i = 0; i <= n; i++) Walk(Vector3.Lerp(a, c, i / (float)n), label + " " + i);
        }
        Check(b.id == RailAbandonedStation.Id && b.length == 160, "block identity and footprint");
        Check(b.transform.Find("Env/" + RailAbandonedStation.RootName) != null, "V3 root present");
        for (float d = 0; d < b.TrackLength; d += .5f)
        {
            b.Sample(d, out var p, out var r);
            // Actual DuckRailsTrain (Nov 26): origin at rail + 1, deck top -0.03, hull below deck +/-2.09,
            // body/stack above deck +/-2.28, top 2.56, length -5.0..6.0. Boxes add a few cm.
            var o = p + Vector3.up;
            var hull = Physics.OverlapBox(o + r * new Vector3(0, -.35f, .5f), new Vector3(2.13f, .3f, 5.55f), r, env);
            var body = Physics.OverlapBox(o + r * new Vector3(0, 1.31f, .5f), new Vector3(2.33f, 1.29f, 5.55f), r, env);
            var hits = hull.Concat(body).ToArray();   // Nov 26: Justin lowered the roof 2 m, leaving ~0.4 m over the train top
            Check(hits.Length == 0, "train sweep " + d + " " + string.Join(",", hits.Select(c => c.name).Distinct()));
        }
        const float F = -3, D = 1f;
        Line(new(-16.4f, F, 53.5f), new(-16.4f, F, 106.5f), 1, "platform 1 track side");
        Line(new(-24.1f, F, 54), new(-24.1f, F, 106), 1, "platform 1 shop side");
        Line(new(-7f, F, 53.5f), new(-7f, F, 106.5f), 1, "service ledge");
        Line(new(1f, F, 46.6f), new(1f, F, 113.4f), 1, "platform 3");
        Line(new(-26.5f, D, 46.45f), new(-3.6f, D, 46.45f), .5f, "ticket concourse across gates");
        Line(new(-26.5f, D, 116.45f), new(-14, D, 116.45f), .5f, "shuttered concourse west");
        Line(new(-10.5f, D, 116.5f), new(3f, D, 116.5f), .5f, "shuttered concourse east");
        Line(new(-39f, F, 19f), new(-39f, F, 133f), 1, "transfer hall");
        Line(new(15, F, 41), new(15, F, 121), 1, "maintenance hall");
        Line(new(10.5f, F, 13), new(10.5f, F, 43), 1, "fan gallery");
        Line(new(6, F, 52f), new(6, F, 89.5f), .5f, "wreck aisle (cars 3 and 2)");
        Line(new(.5f, F, 48), new(12.5f, F, 48), .5f, "south cross passage");
        // North cross passage removed: the collapsed tunnel closes it (Justin, Nov 26).
        foreach (var (x0, x1) in new[] { (2f, 4.05f), (4.75f, 7.25f), (7.9f, 9.5f), (10f, 14f) }) Line(new(x0, F, RailAbandonedStation.WreckSideDoorZ), new(x1, F, RailAbandonedStation.WreckSideDoorZ), .5f, "wreck side door to hall " + x0);   // steps over the 0.2-0.3 m gaps beside the car
        Line(new(-6, F, 80.25f), new(-3, F, 80.25f), .5f, "ledge door to platform 3");
        foreach (float z in new[] { 55.5f, 61f, 69f, 79f, 93f, 105f }) Line(new(-26.2f, F, z), new(-23.9f, F, z), .5f, "shop door " + z);
        foreach (var (x, z) in new[] { (-36f, 25.5f), (-36f, 44.5f), (-36f, 100f), (-36f, 107f), (-36f, 125.5f), (-30f, 125.5f) }) Line(new(x - 1.2f, F, z), new(x + 1.2f, F, z), .4f, "west door " + x + "," + z);
        foreach (var (x, z) in new[] { (17f, 62f), (17f, 76f), (17f, 90f), (17f, 104f), (17f, 116.5f), (29f, 76f), (13f, 29.5f) }) Line(new(x - 1.2f, F, z), new(x + 1.2f, F, z), .4f, "east door " + x + "," + z);
        // Batch 4 partitions: pump room (two doorways) and parts store (one), plus a run through the sump room.
        foreach (float x in new[] { 20.25f, 31.75f }) Line(new(x, F, 119.6f), new(x, F, 122.4f), .4f, "pump partition door " + x);
        Line(new(36f, F, 77.4f), new(36f, F, 79.8f), .4f, "parts partition door");
        Line(new(18.6f, F, 119.6f), new(34.2f, F, 119.6f), 1f, "pump sump room");
        Line(new(-33.4f, F, 75.4f), new(-33.4f, F, 82.6f), 1f, "coin lockers west lane");
        Line(new(-28.3f, F, 127.5f), new(-28.3f, F, 133.4f), 1f, "cable gallery west lane");

        // Batch 5: each shaft is one stair mesh; walk every tread (0.5 m) at three points across the width.
        var stairs = b.GetComponentsInChildren<MeshCollider>().Where(c => c.name == "Staircase (single mesh)").ToArray();
        Check(stairs.Length == 4, "four single-mesh staircases: " + stairs.Length);
        foreach (var c in stairs)
        {
            var bb = c.bounds;
            for (float zz = bb.min.z + .25f; zz < bb.max.z; zz += .5f)
                foreach (float dx in new[] { -1.4f, 0f, 1.4f })
                {
                    var from = new Vector3(bb.center.x + dx, bb.max.y + .05f, zz);
                    if (c.Raycast(new Ray(from, Vector3.down), out var hit, 10f)) Walk(hit.point, "tread " + hit.point.ToString("F1"));
                    else Check(false, "stair tread missing at " + from.ToString("F1"));
                }
        }
        foreach (var t in b.spawnCandidates.coal) { Check(Physics.Raycast(t.position, Vector3.down, out var h, .4f, env) && Mathf.Abs(t.position.y - h.point.y - .2f) < .07f, "coal support " + t.name); }
        foreach (var t in b.spawnCandidates.gold) { Check(Physics.Raycast(t.position + Vector3.up * .1f, Vector3.down, out var h, .4f, env) && Mathf.Abs(t.position.y - h.point.y - .05f) < .07f, "gold support " + t.name); }
        Check(Physics.Linecast(new(0, 1.5f, 7), new(0, 1.5f, 153), env), "Entrance-exit sightline blocked by solid geometry");
        // Batch 5: no spawn of any kind on the track (live track corridor or the dead-track bed).
        var trackPts = new List<Vector3>(); for (float d = 0; d < b.TrackLength; d += .5f) { b.Sample(d, out var tp, out _); trackPts.Add(b.transform.InverseTransformPoint(tp)); }
        int onTrack = 0;
        foreach (var m in b.spawnCandidates.coal.Concat(b.spawnCandidates.gold).Concat(b.spawnCandidates.enemies))
        {
            var lp = b.transform.InverseTransformPoint(m.position);
            var near = trackPts.OrderBy(q => (new Vector2(q.x - lp.x, q.z - lp.z)).sqrMagnitude).First();
            bool live = new Vector2(near.x - lp.x, near.z - lp.z).magnitude < 2.6f && lp.y < near.y + 1.4f;
            bool deadBed = lp.y < F - .5f;
            if (live || deadBed) { onTrack++; if (onTrack <= 10) lines.Add("FAIL spawn on track " + m.name + " " + lp.ToString("F2")); }
        }
        Check(onTrack == 0, "no coal, gold or enemy spawn on the track: " + onTrack);
        int coalNeed = RailResourceBalance.CoalCount(8), goldNeed = RailResourceBalance.GoldCount(8);
        Check(RailSpawnPool.Available(b, RailSpawnKind.Coal).Length >= coalNeed, "coal capacity " + RailSpawnPool.Available(b, RailSpawnKind.Coal).Length + " >= " + coalNeed);
        Check(RailSpawnPool.Available(b, RailSpawnKind.Gold).Length >= goldNeed, "gold capacity " + RailSpawnPool.Available(b, RailSpawnKind.Gold).Length + " >= " + goldNeed);
        for (int crew = 1; crew <= 8; crew++)
        {
            bool ok = true;
            for (int seed = 0; seed < 100 && ok; seed++)
                foreach (var kind in new[] { RailSpawnKind.Coal, RailSpawnKind.Gold })
                {
                    var pick = RailRoute.SelectResources(b, kind, crew, seed);
                    int want = kind == RailSpawnKind.Coal ? RailResourceBalance.CoalCount(crew) : RailResourceBalance.GoldCount(crew);
                    if (pick.Length != want || pick.Distinct().Count() != pick.Length) ok = false;
                }
            Check(ok, "SelectResources crew " + crew + " x100 seeds exact and unique");
        }
        if (requireGraph && b.graph)
        {
            var graph = CrawlGraph.Load(b.graph);
            var probes = new[] { new Vector3(-16.6f, F, 80), new(-7, F, 80), new(1, F, 80), new(-20, D, 46.5f), new(-20, D, 114), new(-39, F, 80), new(15, F, 80), new(6, F, 74), new(26, F, 119), new(10.5f, F, 20) };
            var nodes = probes.Select(q => graph.FindNearestGroundNode(q, 1)).ToArray();
            for (int i = 0; i < probes.Length; i++) Check(nodes[i] >= 0, "graph node near " + probes[i]);
            if (nodes.All(n => n >= 0))
            {
                var seen = new HashSet<int> { nodes[0] }; var todo = new Queue<int>(); todo.Enqueue(nodes[0]);
                while (todo.Count > 0) { int n = todo.Dequeue(); for (int e = graph.EdgeStart[n]; e < graph.EdgeStart[n + 1]; e++) { int k = graph.EdgeTarget[e]; if (graph.GetNormal(k).y > .5f && seen.Add(k)) todo.Enqueue(k); } }
                for (int i = 1; i < probes.Length; i++) Check(seen.Contains(nodes[i]), "ground graph connects platform 1 to " + probes[i]);
            }
            for (int crew = 1; crew <= 8; crew++)
            {
                bool ok = true;
                for (int seed = 0; seed < 100 && ok; seed++) ok = RailEnemySpawnBalance.Select(b, seed, crew).Length == 4 * RailEnemySpawnBalance.PerQuadrant(crew);
                Check(ok, "enemy quadrant quota crew " + crew + " x100 seeds");
            }
        }
        // Rubble stays inside the wreck area: no corner of any rock reaches walkable rooms beside it.
        var keepOut = new[] { new Bounds(new(-.6f, -1.8f, 110.5f), new(9.1f, 2.4f, 7f)), new Bounds(new(.95f, -1.8f, 92f), new(2.1f, 2.4f, 12f)), new Bounds(new(13.4f, -1.8f, 90f), new(7f, 2.4f, 40f)) };
        foreach (var r in b.GetComponentsInChildren<MeshRenderer>().Where(r => r.name.Contains("rubble") || r.name == "Collapse fill" || r.name == "Fallen roof slab"))
        {
            var tr = r.transform; var e = Vector3.Scale(r.GetComponent<MeshFilter>().sharedMesh.bounds.extents, tr.lossyScale);
            bool inside = false;
            foreach (float sx in new[] { -1f, 1f }) foreach (float sy in new[] { -1f, 1f }) foreach (float sz in new[] { -1f, 1f })
                    {
                        var corner = b.transform.InverseTransformPoint(tr.TransformPoint(Vector3.Scale(new Vector3(sx, sy, sz), r.GetComponent<MeshFilter>().sharedMesh.bounds.extents) + r.GetComponent<MeshFilter>().sharedMesh.bounds.center));
                        if (keepOut.Any(k => k.Contains(corner))) inside = true;
                    }
            if (inside) Check(false, "rubble pokes into a room: " + r.name + " at " + tr.position.ToString("F2"));
        }
        Check(true, "rubble containment checked");
        var fights = ZFightingAll(b, out int triCount);
        Check(fights.Count == 0, "no coplanar overlapping faces on any mesh (z-fighting), " + triCount + " triangles: " + fights.Count);
        foreach (var f in fights.Take(60)) lines.Add("  z-fight " + f);
        // Wall-mounted thin visuals (posters, panels, signs, vests...) must have something solid right behind them.
        string[] freeStanding = { "Stall door", "Open locker door", "Fan blade", "Hanging strap", "Jammed door leaf", "Trolley handle", "Fan guard bar", "Turnstile arm", "Stall partition", "Urinal privacy divider", "Shutter", "Shutter rib", "Arrow", "Sign hanger rod", "Litter", "Crushed roof panel", "Hi-vis vest on bench", "Spilled newspaper", "Grille slat", "Fan duct grille", "Line colour band", "Area stripe", "Wainscot", "Bike", "Seat armrest", "Cooling fin", "Kiosk glass", "Kiosk side glass" };
        int floating = 0;
        foreach (var r in b.GetComponentsInChildren<MeshRenderer>())
        {
            var mf = r.GetComponent<MeshFilter>(); if (!mf || !mf.sharedMesh || mf.sharedMesh.vertexCount != 24 || r.GetComponent<Collider>()) continue;
            if (freeStanding.Any(n => r.name.StartsWith(n)) || HangingSign(r.transform) || r.GetComponentsInParent<Transform>().Any(p => p.name == "Pipes" || p.name == "Rooftop buildings")) continue;   // pipe fittings sit on pipes; rooftop buildings have no colliders
            var s = Vector3.Scale(mf.sharedMesh.bounds.size, r.transform.lossyScale); s = new(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            int thin = s.x < s.z ? (s.x < s.y ? 0 : 1) : (s.z < s.y ? 2 : 1);
            if (thin == 1 || s[thin] > .07f || Mathf.Min(s[(thin + 1) % 3], s[(thin + 2) % 3]) < .12f) continue;   // vertical, thin, panel-sized
            var axis = thin == 0 ? r.transform.right : r.transform.forward; var c0 = r.bounds.center;
            bool backed = false;
            foreach (float sgn in new[] { -1f, 1f }) if (Physics.Raycast(c0, axis * sgn, .26f, env, QueryTriggerInteraction.Ignore)) backed = true;
            if (Physics.CheckSphere(c0, .01f, env, QueryTriggerInteraction.Ignore)) backed = true;   // partly sunk into the wall
            if (!backed) { floating++; if (floating <= 40) lines.Add("FAIL floating wall visual " + Path2(r.transform, b.transform) + " at " + c0.ToString("F2")); }
        }
        Check(floating == 0, "wall visuals backed by something solid: " + floating + " floating");
        // Rubble rests on something: each rock touches the floor, the wreck, a wall or a lower rock.
        int loose = 0;
        foreach (var r in b.GetComponentsInChildren<MeshRenderer>().Where(r => r.name == "Cave-in rubble" || r.name == "Collapse fill" || r.name == "Jammed roof slab"))
        {
            var bc = r.GetComponent<BoxCollider>(); var tr = r.transform;
            var half = Vector3.Scale(bc.size, tr.lossyScale) * .5f * 1.02f;
            var hits = Physics.OverlapBox(tr.TransformPoint(bc.center), new(Mathf.Abs(half.x), Mathf.Abs(half.y), Mathf.Abs(half.z)), tr.rotation, env, QueryTriggerInteraction.Ignore);
            bool held = hits.Any(h => h != bc && (h.bounds.center.y < r.bounds.center.y - .05f || h.name.ToLower().Contains("wall")));
            if (!held) { loose++; lines.Add("FAIL unsupported rock " + r.name + " at " + tr.position.ToString("F2")); }
        }
        Check(loose == 0, "every rock and jammed slab is supported: " + loose + " loose");
        Directory.CreateDirectory(RailAbandonedStation.EvidenceFolder); File.WriteAllLines(RailAbandonedStation.EvidenceFolder + "/checks.txt", lines);
        Debug.Log("METRO V3: " + lines.Count + " checks, " + lines.Count(s => s.StartsWith("FAIL")) + " failures");
        if (lines.Any(s => s.StartsWith("FAIL"))) throw new Exception(string.Join("\n", lines.Where(s => s.StartsWith("FAIL")).Take(25)));
    }
}
#endif
