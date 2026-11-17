#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

// Far side of the finale: tunnel portals and interior, Happy Land hills, trees and flowers.
// Part of the fort builder so one Apply() rebuilds the whole block art group. Layout is unchanged from the
// endorsed V5 full-route plan: tunnel Z120-160, valley Z160-320, far ridge Z320-380, stream on the east.
public static partial class RailFinaleFortArtPass
{
    static float Hash(int a, int b) { float s = Mathf.Sin(a * 127.1f + b * 311.7f) * 43758.5453f; return s - Mathf.Floor(s); }
    static float StreamX(float z) => 17 + 4 * Mathf.Sin(z * .09f);
    // Same footprint as the original hills (flat inside |x| 27, far ridge from Z320) with distinct lobes along the sides.
    public static float FarHill(float x, float z)
    {
        float lobes = .72f + .5f * Mathf.PerlinNoise(z * .03f + (x < 0 ? 7.3f : 41.9f), .5f);
        float side = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(27, 56, Mathf.Abs(x))) * (18 + 5 * Mathf.Sin(z * .065f) + 3 * Mathf.Cos(z * .14f)) * lobes;
        float end = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(320, 362, z)) * (27 + 4 * Mathf.Sin(x * .13f));
        return Mathf.Max(side, end);
    }

    // World-scale UVs for the tiling pixel materials (grass, rock, leaves).
    static void Raw(string mat, Vector3 outward, bool planar, params Vector3[] p)
    {
        var n = Vector3.Cross(p[1] - p[0], p[2] - p[0]);
        if (n.sqrMagnitude < 1e-9f) { if (p.Length < 4) return; n = Vector3.Cross(p[2] - p[0], p[3] - p[0]); if (n.sqrMagnitude < 1e-9f) return; }
        var q = p.ToList(); if (Vector3.Dot(n, outward) < 0) { q.Reverse(); n = -n; }
        n.Normalize(); var m = Vis(mat); int b = m.v.Count;
        foreach (var v in q) { m.v.Add(v); m.uv.Add(planar || Mathf.Abs(n.y) > .5f ? new Vector2(v.x, v.z) / 3 : Mathf.Abs(n.x) > Mathf.Abs(n.z) ? new Vector2(v.z, v.y) / 3 : new Vector2(v.x, v.y) / 3); }
        for (int f = 1; f < q.Count - 1; f++) { m.t.Add(b); m.t.Add(b + f); m.t.Add(b + f + 1); }
    }
    static void RawBox(string mat, Vector3 mn, Vector3 mx, bool bottom)
    {
        Vector3 P(float x, float y, float z) => new(x, y, z);
        Raw(mat, Vector3.up, false, P(mn.x, mx.y, mn.z), P(mn.x, mx.y, mx.z), P(mx.x, mx.y, mx.z), P(mx.x, mx.y, mn.z));
        if (bottom) Raw(mat, Vector3.down, false, P(mn.x, mn.y, mn.z), P(mx.x, mn.y, mn.z), P(mx.x, mn.y, mx.z), P(mn.x, mn.y, mx.z));
        Raw(mat, Vector3.back, false, P(mn.x, mn.y, mn.z), P(mn.x, mx.y, mn.z), P(mx.x, mx.y, mn.z), P(mx.x, mn.y, mn.z));
        Raw(mat, Vector3.forward, false, P(mn.x, mn.y, mx.z), P(mn.x, mx.y, mx.z), P(mx.x, mx.y, mx.z), P(mx.x, mn.y, mx.z));
        Raw(mat, Vector3.left, false, P(mn.x, mn.y, mn.z), P(mn.x, mx.y, mn.z), P(mn.x, mx.y, mx.z), P(mn.x, mn.y, mx.z));
        Raw(mat, Vector3.right, false, P(mx.x, mn.y, mn.z), P(mx.x, mx.y, mn.z), P(mx.x, mx.y, mx.z), P(mx.x, mn.y, mx.z));
    }
    // Convex polygon in X/Y extruded along Z, with atlas skin `kind`.
    static void SlabXY(string kind, float za, float zb, params float[] xy)
    {
        int n = xy.Length / 2; var c = Vector2.zero; for (int i = 0; i < n; i++) c += new Vector2(xy[i * 2], xy[i * 2 + 1]); c /= n;
        Vector3 At(int i, float z) => new(xy[(i % n) * 2], xy[(i % n) * 2 + 1], z);
        Face(kind, 0, Vector3.back, Enumerable.Range(0, n).Select(i => At(i, Mathf.Min(za, zb))).ToArray());
        Face(kind, 0, Vector3.forward, Enumerable.Range(0, n).Select(i => At(i, Mathf.Max(za, zb))).ToArray());
        for (int i = 0; i < n; i++)
        {
            var mid = (new Vector2(xy[i * 2], xy[i * 2 + 1]) + new Vector2(xy[((i + 1) % n) * 2], xy[((i + 1) % n) * 2 + 1])) / 2 - c;
            Face(kind, 0, new Vector3(mid.x, mid.y, 0), At(i, za), At(i + 1, za), At(i + 1, zb), At(i, zb));
        }
    }

    static void RemoveFarSide(Transform art, Transform polish, Dictionary<string, int> removed)
    {
        var happy = art.Find("Happy Land");
        var gone = new HashSet<string> { "Tree trunk", "Faceted crown", "Flower patch", "Continuous valley hills and far ridge" };
        if (happy) foreach (var t in happy.Cast<Transform>().Where(t => gone.Contains(t.name)).ToArray()) { removed.TryGetValue(t.name, out int c); removed[t.name] = c + 1; Object.DestroyImmediate(t.gameObject); }
        foreach (var t in polish.Cast<Transform>().Where(t => t.name == "Concrete tunnel portal pier" || t.name == "Concrete tunnel lintel").ToArray()) { removed.TryGetValue(t.name, out int c); removed[t.name] = c + 1; Object.DestroyImmediate(t.gameObject); }
    }

    static void Ground(Vector3 a, Vector3 b, Vector3 c, Vector3 d) { Raw("grass", Vector3.up, true, a, b, c, d); Col().Quad(a, b, c, d, Vector3.up); }
    static void Hills()
    {
        region = "Happy Land";
        // Jittered coarse grid: each quad is a visible facet. Edges shared with the flat floor are not jittered.
        Vector3 V(float x, float z)
        {
            bool edge = Mathf.Abs(Mathf.Abs(x) - 27) < .01f || Mathf.Abs(x) > 64.9f || Mathf.Abs(x) < .01f || z < 160.1f || z > 379.9f || Mathf.Abs(z - 320) < .01f;
            if (!edge) { int ix = Mathf.RoundToInt(x), iz = Mathf.RoundToInt(z); x += (Hash(ix, iz) - .5f) * 4.4f; z += (Hash(iz, ix + 91) - .5f) * 5.5f; }
            return new Vector3(x, FarHill(x, z), z);
        }
        float[] side = { 27, 36, 46, 56, 65 };
        for (int z = 160; z < 380; z += 10)
            foreach (float s in new[] { -1f, 1f })
                for (int i = 0; i < side.Length - 1; i++) Ground(V(s * side[i], z), V(s * side[i], z + 10), V(s * side[i + 1], z + 10), V(s * side[i + 1], z));
        for (int z = 320; z < 380; z += 10) for (int x = -27; x < 27; x += 9) Ground(V(x, z), V(x, z + 10), V(x + 9, z + 10), V(x + 9, z));
        // Flat valley floor in large pieces; only the stream reach is cut finer.
        Vector3 F(float x, float z) => new(x, 0, z);
        Ground(F(-27, 160), F(-27, 320), F(0, 320), F(0, 160)); Ground(F(0, 160), F(0, 190), F(27, 190), F(27, 160)); Ground(F(0, 270), F(0, 320), F(27, 320), F(27, 270));
        var zs = new List<float> { 190, 193 }; for (float z = 198; z <= 258; z += 5) zs.Add(z); zs.Add(263); zs.Add(270);
        Vector3[] Row(float z)
        {
            float sx = StreamX(z), bed = -.6f * Mathf.Clamp01((z - 193) / 5) * Mathf.Clamp01((263 - z) / 5);
            return new[] { F(0, z), F(sx - 3.4f, z), new Vector3(sx - 1.9f, bed, z), new Vector3(sx + 1.9f, bed, z), F(sx + 3.4f, z), F(27, z) };
        }
        for (int j = 0; j < zs.Count - 1; j++) { var a = Row(zs[j]); var b = Row(zs[j + 1]); for (int i = 0; i < 5; i++) Ground(a[i], b[i], b[i + 1], a[i + 1]); }
    }

    // Chunky faceted foliage lump.
    static void Lump(string mat, Vector3 c, float r, float squash, int seed)
    {
        const int N = 5; float[] ys = { -.78f, .02f, .62f }; float[] rs = { .56f, 1, .64f }; var ring = new Vector3[3][];
        for (int k = 0; k < 3; k++)
        {
            ring[k] = new Vector3[N];
            for (int i = 0; i < N; i++)
            {
                float a = (i + Hash(seed, 3) + k * .5f) * Mathf.PI * 2 / N, rr = r * rs[k] * (.92f + .16f * Hash(seed + k * 7, i));
                ring[k][i] = c + new Vector3(Mathf.Cos(a) * rr, ys[k] * r * squash, Mathf.Sin(a) * rr);
            }
        }
        var top = c + Vector3.up * (.98f * r * squash); var bottom = c - Vector3.up * (1.02f * r * squash);
        void T(Vector3 a, Vector3 b, Vector3 d) => Raw(mat, (a + b + d) / 3 - c, false, a, b, d);
        for (int i = 0; i < N; i++)
        {
            int j = (i + 1) % N; T(bottom, ring[0][i], ring[0][j]); T(top, ring[2][i], ring[2][j]);
            for (int k = 0; k < 2; k++) { T(ring[k][i], ring[k][j], ring[k + 1][i]); T(ring[k + 1][i], ring[k][j], ring[k + 1][j]); }
        }
    }
    static void Trunk(Vector3 p, float h, float lo, float hi, Vector3 lean)
    {
        var t = p + Vector3.up * h + lean; var c = (p + t) / 2;
        Vector3[] d = { new(-1, 0, -1), new(1, 0, -1), new(1, 0, 1), new(-1, 0, 1) };
        for (int i = 0; i < 4; i++) { var a = d[i]; var b = d[(i + 1) % 4]; Raw("timber", (p + (a + b) * lo / 2 + t + (a + b) * hi / 2) / 2 - c, false, p + a * lo, t + a * hi, t + b * hi, p + b * lo); }
        BoxCol(new Vector3(p.x - lo, p.y, p.z - lo), new Vector3(p.x + lo, p.y + h, p.z + lo));
    }
    static void Broadleaf(float x, float z, float s, bool blossom, int seed)
    {
        var p = new Vector3(x, FarHill(x, z) - .1f, z); float yaw = Hash(seed, 1) * Mathf.PI * 2; var lean = new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw)) * (.5f * s * Hash(seed, 2));
        float h = (3 + Hash(seed, 4)) * s; Trunk(p, h, .42f * s, .24f * s, lean); var top = p + Vector3.up * h + lean; string mat = blossom ? "blossom" : "leaf";
        Vector3 O(float a, float d, float y) => new Vector3(Mathf.Cos(yaw + a) * d, y, Mathf.Sin(yaw + a) * d) * s;
        Lump(mat, top + O(0, .2f, 1.3f), 2.3f * s, .82f, seed); Lump(mat, top + O(.6f, 1.7f, .35f), 1.65f * s, .8f, seed + 11); Lump(mat, top + O(3.5f, 1.5f, .6f), 1.75f * s, .78f, seed + 23);
    }
    static void Conifer(Vector3 p, float s, int seed)
    {
        p.y -= .15f; Trunk(p, 1.6f * s, .3f * s, .22f * s, Vector3.zero); const int N = 6; float rot = Hash(seed, 5) * 1.05f;
        for (int k = 0; k < 3; k++)
        {
            float y0 = (1.2f + k * 1.55f) * s, y1 = y0 + (2.5f - k * .2f) * s, r = (2.1f - k * .55f) * s; var apex = p + Vector3.up * y1; var centre = p + Vector3.up * y0;
            for (int i = 0; i < N; i++)
            {
                float a = rot + (i + k * .5f) * Mathf.PI * 2 / N, b = rot + (i + 1 + k * .5f) * Mathf.PI * 2 / N;
                var va = centre + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * r; var vb = centre + new Vector3(Mathf.Cos(b), 0, Mathf.Sin(b)) * r;
                Raw("leaf", (va + vb) / 2 - centre + Vector3.up * .5f, false, va, vb, apex); Raw("leaf", Vector3.down, false, centre, va, vb);
            }
        }
    }

    static int Vegetation(PhysicsScene phys)
    {
        region = "Happy Land"; int count = 0, seed = 100;
        // Valley rows: irregular spacing, pushed clear of the stream, orchard and cottages.
        for (int z = 166; z < 304; z += 12)
            foreach (float s in new[] { -1f, 1f })
            {
                seed++; float zz = z + (Hash(seed, 8) - .5f) * 5, x = s * (19 + Hash(seed, 9) * 7);
                if (s > 0 && zz > 188 && zz < 268) x = StreamX(zz) + 6.5f + Hash(seed, 10) * 2.5f;
                if (s < 0 && zz > 198 && zz < 246) x = -28 - Hash(seed, 10) * 2;
                if (Mathf.Abs(x) > 11 && Mathf.Abs(x) < 25 && ((zz > 257 && zz < 273) || (zz > 281 && zz < 297))) x = s * 26.5f;
                Broadleaf(x, zz, .85f + Hash(seed, 11) * .4f, Hash(seed, 12) < .42f, seed); count++;
            }
        for (int z = 204; z < 250; z += 12) foreach (float x in new[] { -12f, -23f }) { seed++; Broadleaf(x + (Hash(seed, 8) - .5f), z + (Hash(seed, 9) - .5f), .7f + Hash(seed, 11) * .12f, false, seed); count++; }
        foreach (var q in new[] { new Vector2(-11, 176), new(11.5f, 183), new(-12, 256), new(11, 276), new(-11.5f, 304), new(12, 312) }) { seed++; Broadleaf(q.x, q.y, .7f + Hash(seed, 11) * .2f, true, seed); count++; }
        // Conifers on the hill shoulders and on the tunnel ridge (seated on the existing cliff colliders).
        foreach (var q in new[] { new Vector2(-41, 188), new(-45, 236), new(-39, 282), new(43, 206), new(41, 252), new(46, 300), new(-37, 212), new(38, 174), new(-20, 343), new(15, 346), new(-3, 352), new(31, 338), new(-34, 334) })
        { seed++; Conifer(new Vector3(q.x, FarHill(q.x, q.y), q.y), .9f + Hash(seed, 6) * .5f, seed); count++; }
        foreach (var q in new[] { new Vector2(-14, 128), new(-24, 141), new(-38, 131), new(-50, 146), new(-30, 154), new(-57, 127), new(16, 134), new(27, 147), new(41, 127), new(52, 139), new(22, 156), new(46, 153), new(-4, 140), new(5, 151) })
        {
            seed++; if (!phys.Raycast(new Vector3(q.x, 60, q.y), Vector3.down, out var hit, 70, 1 << 6, QueryTriggerInteraction.Ignore) || hit.point.y < 6) continue;
            Conifer(hit.point, .85f + Hash(seed, 6) * .55f, seed); count++;
        }
        // Flower clusters along the railway, merged into two meshes.
        for (int z = 170; z < 300; z += 7) foreach (float x in new[] { -9f, 9f }) for (int i = 0; i < 3; i++)
                {
                    var mn = new Vector3(x + i * .5f - .15f, .01f, z + i * .4f - .15f); var mx = mn + Vector3.one * .3f;
                    RawBox(i == 0 ? "leaf" : "blossom", mn, mx, false);
                }
        return count;
    }

    static void Tunnel()
    {
        region = "Tunnel";
        // Stone arch portals in front of the cliff faces. Opening: 11 m wide, chamfered above 5.7 m; the train envelope
        // (|x| 4.5, height 6.3) clears the chamfer by 0.4 m.
        foreach (var (inner, outer) in new[] { (120.2f, 119.2f), (159.8f, 161.2f) })
        {
            float dir = Mathf.Sign(outer - inner);
            void Block(float proud, params float[] xy) { SlabXY("plaster", inner, outer + dir * proud, xy); }
            foreach (float s in new[] { -1f, 1f })
            {
                Block(.3f, s * 5.3f, 0, s * 5.3f, .9f, s * 7.9f, .9f, s * 7.9f, 0);                                  // plinth
                Block(0, s * 5.5f, .9f, s * 5.5f, 2.5f, s * 7.6f, 2.5f, s * 7.6f, .9f);
                Block(.18f, s * 5.5f, 2.5f, s * 5.5f, 4.1f, s * 7.6f, 4.1f, s * 7.6f, 2.5f);
                Block(0, s * 5.5f, 4.1f, s * 5.5f, 5.7f, s * 7.6f, 6.6f, s * 7.6f, 4.1f);
                Block(.18f, s * 5.5f, 5.7f, s * 4.2f, 7, s * 5.4f, 8.9f, s * 7.6f, 6.6f);                            // haunch
                Block(0, s * 4.2f, 7, s * 1.4f, 7, s * 1.6f, 8.9f, s * 5.4f, 8.9f);
                BoxCol(new Vector3(Mathf.Min(s * 5.5f, s * 7.6f), 0, Mathf.Min(inner, outer)), new Vector3(Mathf.Max(s * 5.5f, s * 7.6f), 6.6f, Mathf.Max(inner, outer)));
            }
            Block(.3f, -1.4f, 7, -1.6f, 9.4f, 1.6f, 9.4f, 1.4f, 7);                                                  // keystone
            BoxCol(new Vector3(-5.4f, 7, Mathf.Min(inner, outer)), new Vector3(5.4f, 8.9f, Mathf.Max(inner, outer)));
            B("lamp", -.35f, outer + dir * .3f, .35f, outer + dir * .38f, 7.5f, 7.9f, false);
        }
        // Interior: chamfered haunches along the bore and stone ribs every 8 m. Visual only; nothing enters the envelope.
        foreach (float s in new[] { -1f, 1f })
            Raw("rock", new Vector3(-s, -1, 0), false, new Vector3(s * 5.5f, 5.7f, 120.2f), new Vector3(s * 5.5f, 5.7f, 159.8f), new Vector3(s * 4.2f, 7, 159.8f), new Vector3(s * 4.2f, 7, 120.2f));
        for (int z = 124; z <= 156; z += 8)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                SlabXY("concrete", z - .3f, z + .3f, s * 5.25f, 0, s * 5.25f, 5.6f, s * 5.6f, 5.6f, s * 5.6f, 0);
                SlabXY("concrete", z - .3f, z + .3f, s * 5.25f, 5.6f, s * 4.1f, 6.75f, s * 4.3f, 7.05f, s * 5.6f, 5.75f);
                B("lamp", s * 5.17f, z - .2f, s * 5.25f, z + .2f, 4.6f, 4.85f, false);
            }
            SlabXY("concrete", z - .3f, z + .3f, -4.1f, 6.75f, -4.1f, 7.05f, 4.1f, 7.05f, 4.1f, 6.75f);
        }
    }

    static void FarSide(Transform root, StringBuilder log)
    {
        Physics.SyncTransforms(); var phys = root.gameObject.scene.GetPhysicsScene();
        Tunnel(); Hills(); int trees = Vegetation(phys);
        log.AppendLine($"Far side: {trees} trees; hills {Vis("grass").t.Count / 3} triangles");
    }
}
#endif
