#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using Object = UnityEngine.Object;

// Fort architecture pass, layout v2 (approved November 16, 2026; see docs/final-escape-layout.md).
// Adapts the CURRENT prefab: removes the old fort masses, keeps the catwalk, button balcony, gold deposit,
// warning posts, traffic jam, river recovery and everything north of the bank, then builds the new fort
// under one group. Re-runnable: the group is rebuilt and the removals are idempotent.
// Surfaces use the Starting Station / Compound recipe: Quackville atlas tiles through the shared depot
// materials, a painted dado below plaster, and noise-driven patches of exposed brick.
// Navigation and lighting must be rebaked afterwards.
public static class RailFinaleFortArtPass
{
    public const string PrefabPath = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";
    public const string Folder = "Assets/DuckRails/FinaleFort/ArtPass";
    public const string GroupName = "Fort architecture v2";
    const float Lip = .12f, Rise = .25f, Band = 1.3f;

    class MB
    {
        public readonly List<Vector3> v = new(); public readonly List<Vector2> uv = new(); public readonly List<int> t = new();
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0) (b, d) = (d, b);
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int k = 0; k < 4; k++) uv.Add(Vector2.zero);
            t.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
        }
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward)
        {
            var n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, outward) < 0) (b, c) = (c, b);
            int i = v.Count; v.Add(a); v.Add(b); v.Add(c);
            for (int k = 0; k < 3; k++) uv.Add(Vector2.zero); t.AddRange(new[] { i, i + 1, i + 2 });
        }
        // Convex polygon in X/Z, extruded between y0 and y1.
        public void Prism(Vector2[] p, float y0, float y1)
        {
            var c = Vector2.zero; foreach (var q in p) c += q; c /= p.Length;
            Vector3 At(Vector2 q, float y) => new(q.x, y, q.y);
            for (int i = 1; i < p.Length - 1; i++) { Tri(At(p[0], y1), At(p[i], y1), At(p[i + 1], y1), Vector3.up); if (y0 > .01f) Tri(At(p[0], y0), At(p[i], y0), At(p[i + 1], y0), Vector3.down); }
            for (int i = 0; i < p.Length; i++)
            {
                var a = p[i]; var b = p[(i + 1) % p.Length]; var mid = (a + b) / 2 - c;
                Quad(At(a, y0), At(a, y1), At(b, y1), At(b, y0), new Vector3(mid.x, 0, mid.y));
            }
        }
        // Rectangular bar from a to b; r is the unit horizontal normal to the bar.
        public void Bar(Vector3 a, Vector3 b, Vector3 r, float hw, float hh)
        {
            var u = Vector3.up * hh; var w = r * hw; var d = (b - a).normalized;
            Quad(a - w + u, b - w + u, b + w + u, a + w + u, Vector3.up); Quad(a - w - u, b - w - u, b + w - u, a + w - u, Vector3.down);
            Quad(a - w - u, a - w + u, b - w + u, b - w - u, -r); Quad(a + w - u, a + w + u, b + w + u, b + w - u, r);
            Quad(a - w - u, a - w + u, a + w + u, a + w - u, -d); Quad(b - w - u, b - w + u, b + w + u, b + w - u, d);
        }
        public Mesh Build(string name, bool lightmapUv)
        {
            // Refill an existing asset in place: copying a temporary mesh over it leaves the old GPU buffers bound.
            string path = Folder + "/" + name + ".asset"; var m = AssetDatabase.LoadAssetAtPath<Mesh>(path); bool fresh = !m;
            if (fresh) m = new Mesh(); else m.Clear();
            m.indexFormat = IndexFormat.UInt32; m.name = name; m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds(); if (lightmapUv && v.Count > 0) Unwrapping.GenerateSecondaryUVSet(m);
            m.UploadMeshData(false);
            if (fresh) AssetDatabase.CreateAsset(m, path); else EditorUtility.SetDirty(m);
            return m;
        }
    }

    // Atlas skins. Rects are top-left pixel rectangles, identical to RailDepotMaterials.
    struct Skin { public string mat; public Rect tile; public float w, h; public Skin(string m, float x, float y, float tw, float th, bool town) { mat = m; tile = new Rect(x, y, tw, th); w = 256; h = town ? 256 : 128; } }
    static readonly Dictionary<string, Skin> skins = new()
    {
        ["plaster"] = new("plaster", 0, 64, 64, 64, true), ["paint"] = new("paint", 0, 192, 64, 64, true), ["brick"] = new("brick", 64, 0, 32, 32, false),
        ["concrete"] = new("concrete", 128, 128, 64, 64, true), ["steel"] = new("steel", 32, 64, 64, 32, false), ["roof"] = new("roof", 128, 64, 64, 64, true),
        ["yellow"] = new("yellow", 0, 32, 32, 32, false), ["hazard"] = new("yellow", 64, 96, 32, 32, false), ["asphalt"] = new("asphalt", 128, 0, 64, 64, true),
        ["rail yellow"] = new("rail yellow", 0, 32, 32, 32, false), ["rail steel"] = new("rail steel", 32, 64, 64, 32, false),
    };

    static Transform group; static string region; static Component surfaceTemplate; static float? bandFloor;
    static readonly Dictionary<string, MB> vis = new(), col = new();
    static readonly Dictionary<string, GameObject> holders = new();
    static readonly Dictionary<string, Material> mats = new();

    static MB Vis(string mat) { string k = region + "|" + mat; if (!vis.TryGetValue(k, out var m)) vis[k] = m = new MB(); return m; }
    static MB Col() { if (!col.TryGetValue(region, out var m)) col[region] = m = new MB(); return m; }
    static GameObject NewCollisionObject(string name)
    {
        var g = new GameObject(name) { layer = 6 }; g.transform.SetParent(group, false);
        if (surfaceTemplate) { var s = g.AddComponent(surfaceTemplate.GetType()); EditorUtility.CopySerialized(surfaceTemplate, s); }
        return g;
    }
    static GameObject Holder() { if (!holders.TryGetValue(region, out var g)) holders[region] = g = NewCollisionObject(region + " box collision"); return g; }
    static void BoxCol(Vector3 mn, Vector3 mx) { var c = Holder().AddComponent<BoxCollider>(); c.center = (mn + mx) / 2; c.size = mx - mn; }

    static List<Vector3> Clip(List<Vector3> p, Vector3 axis, float limit, bool keepAbove)
    {
        var o = new List<Vector3>(); if (p.Count == 0) return o;
        float D(Vector3 q) => keepAbove ? Vector3.Dot(q, axis) - limit : limit - Vector3.Dot(q, axis);
        for (int i = 0; i < p.Count; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Count]; float da = D(a), db = D(b);
            if (da >= -1e-5f) o.Add(a);
            if ((da > 1e-5f && db < -1e-5f) || (da < -1e-5f && db > 1e-5f)) o.Add(Vector3.Lerp(a, b, da / (da - db)));
        }
        return o;
    }
    // One planar face. It is cut on the atlas grid so each piece maps inside a single tile, exactly like the
    // Starting Station surfaces. kind "wall" picks paint / plaster / brick per cell; horizontal "wall" faces are concrete.
    static void Face(string kind, float floor, Vector3 outward, params Vector3[] poly)
    {
        var n = Vector3.Cross(poly[1] - poly[0], poly[2] - poly[0]); if (n.sqrMagnitude < 1e-9f) return;
        var p = poly.ToList(); if (Vector3.Dot(n, outward) < 0) { p.Reverse(); n = -n; }
        n.Normalize();
        if (kind == "lamp") { var lm = Vis("lamp"); for (int i = 1; i < p.Count - 1; i++) lm.Tri(p[0], p[i], p[i + 1], n); return; }
        bool flat = Mathf.Abs(n.y) > .5f; if (kind == "wall" && flat) kind = "concrete";
        bool wall = kind == "wall";
        var U = flat ? Vector3.right : Vector3.Cross(Vector3.up, n).normalized; var V = flat ? Vector3.forward : Vector3.up;
        float cell = wall ? 2 : 4, u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
        foreach (var q in p) { float u = Vector3.Dot(q, U), v = Vector3.Dot(q, V); u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u); v0 = Mathf.Min(v0, v); v1 = Mathf.Max(v1, v); }
        List<float> Cuts(float a, float b, float extra)
        {
            var c = new List<float> { a }; for (float x = Mathf.Floor(a / cell) * cell + cell; x < b - 1e-4f; x += cell) if (x > a + 1e-4f) c.Add(x);
            if (!float.IsNaN(extra) && extra > a + .02f && extra < b - .02f && !c.Any(e => Mathf.Abs(e - extra) < .02f)) c.Add(extra);
            c.Add(b); c.Sort(); return c;
        }
        var us = Cuts(u0, u1, float.NaN); var vs = Cuts(v0, v1, wall ? floor + Band : float.NaN); float plane = Vector3.Dot(p[0], n);
        for (int i = 0; i < us.Count - 1; i++)
            for (int j = 0; j < vs.Count - 1; j++)
            {
                var piece = Clip(Clip(Clip(Clip(p, U, us[i], true), U, us[i + 1], false), V, vs[j], true), V, vs[j + 1], false);
                if (piece.Count < 3) continue;
                float cu = (us[i] + us[i + 1]) / 2, cv = (vs[j] + vs[j + 1]) / 2; string k = kind;
                if (wall)
                {
                    var c = U * cu + V * cv + n * plane; float wear = Mathf.PerlinNoise(c.x * .2f + c.y * .11f + 131.7f, c.z * .27f + c.y * .07f + 112.3f);
                    k = wear > .74f ? "brick" : cv < floor + Band && wear <= .66f ? "paint" : "plaster";
                }
                var s = skins[k]; var m = Vis(s.mat); float tu = Mathf.Floor(cu / 4) * 4, tv = Mathf.Floor(cv / 4) * 4; int b = m.v.Count;
                foreach (var q in piece)
                {
                    float du = Mathf.Clamp01((Vector3.Dot(q, U) - tu) / 4), dv = Mathf.Clamp01((Vector3.Dot(q, V) - tv) / 4);
                    m.v.Add(q); m.uv.Add(new Vector2((s.tile.x + .5f + du * (s.tile.width - 1)) / s.w, 1 - (s.tile.y + .5f + dv * (s.tile.height - 1)) / s.h));
                }
                for (int f = 1; f < piece.Count - 1; f++) { m.t.Add(b); m.t.Add(b + f); m.t.Add(b + f + 1); }
            }
    }
    static void VBox(string kind, Vector3 mn, Vector3 mx, bool bottom, float floor)
    {
        string side = kind == "mass" ? "wall" : kind, top = kind == "mass" ? "concrete" : kind; Vector3 P(float x, float y, float z) => new(x, y, z);
        Face(top, floor, Vector3.up, P(mn.x, mx.y, mn.z), P(mn.x, mx.y, mx.z), P(mx.x, mx.y, mx.z), P(mx.x, mx.y, mn.z));
        if (bottom) Face(top, floor, Vector3.down, P(mn.x, mn.y, mn.z), P(mx.x, mn.y, mn.z), P(mx.x, mn.y, mx.z), P(mn.x, mn.y, mx.z));
        Face(side, floor, Vector3.back, P(mn.x, mn.y, mn.z), P(mn.x, mx.y, mn.z), P(mx.x, mx.y, mn.z), P(mx.x, mn.y, mn.z));
        Face(side, floor, Vector3.forward, P(mn.x, mn.y, mx.z), P(mn.x, mx.y, mx.z), P(mx.x, mx.y, mx.z), P(mx.x, mn.y, mx.z));
        Face(side, floor, Vector3.left, P(mn.x, mn.y, mn.z), P(mn.x, mx.y, mn.z), P(mn.x, mx.y, mx.z), P(mn.x, mn.y, mx.z));
        Face(side, floor, Vector3.right, P(mx.x, mn.y, mn.z), P(mx.x, mx.y, mn.z), P(mx.x, mx.y, mx.z), P(mx.x, mn.y, mx.z));
    }
    static void VBar(string kind, Vector3 a, Vector3 b, Vector3 r, float hw, float hh)
    {
        var u = Vector3.up * hh; var w = r * hw; var d = (b - a).normalized;
        Face(kind, 0, Vector3.up, a - w + u, b - w + u, b + w + u, a + w + u); Face(kind, 0, Vector3.down, a - w - u, b - w - u, b + w - u, a + w - u);
        Face(kind, 0, -r, a - w - u, a - w + u, b - w + u, b - w - u); Face(kind, 0, r, a + w - u, a + w + u, b + w + u, b + w - u);
    }

    // mat: "mass" (wall sides, concrete top), "concrete", "steel", "roof", "hazard", "brick", "lamp".
    static void B(string mat, float x1, float z1, float x2, float z2, float y1, float y2, bool collide = true)
    {
        var mn = new Vector3(Mathf.Min(x1, x2), y1, Mathf.Min(z1, z2)); var mx = new Vector3(Mathf.Max(x1, x2), y2, Mathf.Max(z1, z2));
        VBox(mat, mn, mx, y1 > .01f, bandFloor ?? y1); if (collide) BoxCol(mn, mx);
    }
    static void Prism(string mat, float y0, float y1, params float[] xz)
    {
        var p = new Vector2[xz.Length / 2]; for (int i = 0; i < p.Length; i++) p[i] = new(xz[i * 2], xz[i * 2 + 1]);
        string side = mat == "mass" ? "wall" : mat, top = mat == "mass" ? "concrete" : mat;
        var c = Vector2.zero; foreach (var q in p) c += q; c /= p.Length; Vector3 At(Vector2 q, float y) => new(q.x, y, q.y);
        Face(top, y0, Vector3.up, p.Select(q => At(q, y1)).ToArray()); if (y0 > .01f) Face(top, y0, Vector3.down, p.Select(q => At(q, y0)).ToArray());
        for (int i = 0; i < p.Length; i++)
        {
            var a = p[i]; var b = p[(i + 1) % p.Length]; var mid = (a + b) / 2 - c;
            Face(side, y0, new Vector3(mid.x, 0, mid.y), At(a, y0), At(a, y1), At(b, y1), At(b, y0));
        }
        Col().Prism(p, y0, y1);
    }
    // Cap course on the exposed sides. sides: "S,E,N:-22:-12" (optional range along the edge).
    static void Lips(float x1, float z1, float x2, float z2, float y0, float top, string sides)
    {
        if (string.IsNullOrEmpty(sides)) return;
        var tokens = sides.Split(','); bool Full(char c) => tokens.Any(s => s.Length == 1 && s[0] == c);
        foreach (var token in tokens)
        {
            var parts = token.Split(':'); char side = parts[0][0]; bool ranged = parts.Length == 3;
            float F(int i) => float.Parse(parts[i], CultureInfo.InvariantCulture); float ya = top - .25f, yb = top;
            if (side == 'S' || side == 'N')
            {
                float a = ranged ? Mathf.Min(F(1), F(2)) : x1 - (Full('W') ? Lip : 0), b = ranged ? Mathf.Max(F(1), F(2)) : x2 + (Full('E') ? Lip : 0);
                if (side == 'S') B("concrete", a, z1 - Lip, b, z1, ya, yb, false); else B("concrete", a, z2, b, z2 + Lip, ya, yb, false);
            }
            else
            {
                float a = ranged ? Mathf.Min(F(1), F(2)) : z1, b = ranged ? Mathf.Max(F(1), F(2)) : z2;
                if (side == 'W') B("concrete", x1 - Lip, a, x1, b, ya, yb, false); else B("concrete", x2, a, x2 + Lip, b, ya, yb, false);
            }
        }
    }
    static void Mass(float x1, float z1, float x2, float z2, float y0, float top, string sides) { B("mass", x1, z1, x2, z2, y0, top); Lips(x1, z1, x2, z2, y0, top, sides); }
    // Half-metre slab carried on pillars: the space underneath stays open.
    static void Deck(float x1, float z1, float x2, float z2, float top) { B("concrete", x1, z1, x2, z2, top - .5f, top); }
    static void Pillar(float x, float z, float y0, float top)
    {
        B("mass", x - .4f, z - .4f, x + .4f, z + .4f, y0, top - .3f); B("concrete", x - .6f, z - .6f, x + .6f, z + .6f, top - .3f, top, false);
        B("concrete", x - .5f, z - .5f, x + .5f, z + .5f, y0, y0 + .2f, false);
    }
    // Wall along X (z1..z2 thick) with openings (from, to, sill, head).
    static void WallX(float z1, float z2, float xa, float xb, float y0, float y1, params float[] o)
    {
        float cur = xa; bandFloor = y0;
        for (int i = 0; i < o.Length; i += 4)
        {
            if (o[i] > cur) B("mass", cur, z1, o[i], z2, y0, y1);
            if (o[i + 2] > y0) B("mass", o[i], z1, o[i + 1], z2, y0, o[i + 2]);
            if (o[i + 3] < y1) B("mass", o[i], z1, o[i + 1], z2, o[i + 3], y1);
            cur = o[i + 1];
        }
        if (cur < xb) B("mass", cur, z1, xb, z2, y0, y1);
        bandFloor = null;
    }
    static void WallZ(float x1, float x2, float za, float zb, float y0, float y1, params float[] o)
    {
        float cur = za; bandFloor = y0;
        for (int i = 0; i < o.Length; i += 4)
        {
            if (o[i] > cur) B("mass", x1, cur, x2, o[i], y0, y1);
            if (o[i + 2] > y0) B("mass", x1, o[i], x2, o[i + 1], y0, o[i + 2]);
            if (o[i + 3] < y1) B("mass", x1, o[i], x2, o[i + 1], o[i + 3], y1);
            cur = o[i + 1];
        }
        if (cur < zb) B("mass", x1, cur, x2, zb, y0, y1);
        bandFloor = null;
    }
    // Open rail in the Starting Station palette: worn-yellow handrail, oxidized-steel midrail and posts.
    static void Rail(float ax, float az, float bx, float bz, float y)
    {
        var a = new Vector3(ax, y, az); var b = new Vector3(bx, y, bz); float len = Vector3.Distance(a, b); if (len < .05f) return;
        var d = (b - a) / len; var r = new Vector3(d.z, 0, -d.x); var hi = Vector3.up * 1.1f; var mid = Vector3.up * .55f;
        VBar("rail yellow", a + hi, b + hi, r, .05f, .05f); VBar("rail steel", a + mid, b + mid, r, .04f, .04f);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / 3));
        for (int i = 0; i <= n; i++) { var p = Vector3.Lerp(a, b, (float)i / n); VBox("rail steel", p - new Vector3(.045f, 0, .045f), p + new Vector3(.045f, 1.1f, .045f), false, 0); }
        if (Mathf.Abs(d.x) < .001f || Mathf.Abs(d.z) < .001f)
        {
            var pad = new Vector3(.05f, .05f, .05f);
            BoxCol(Vector3.Min(a, b) + hi - pad, Vector3.Max(a, b) + hi + pad); BoxCol(Vector3.Min(a, b) + mid - pad, Vector3.Max(a, b) + mid + pad);
        }
        else { Col().Bar(a + hi, b + hi, r, .05f, .05f); Col().Bar(a + mid, b + mid, r, .05f, .05f); }
    }
    // Visible treads over one smooth collider slope, with cheek walls. Runs from the low point to the high point.
    static void Stair(float lx, float lz, float hx, float hz, float yLow, float yHigh, float width, float baseY)
    {
        var lo = new Vector3(lx, 0, lz); var hi = new Vector3(hx, 0, hz); float run = Vector3.Distance(lo, hi); var d = (hi - lo) / run;
        var rn = new Vector3(d.z, 0, -d.x); var r = rn * (width / 2); var up = Vector3.up;
        int n = Mathf.Max(1, Mathf.RoundToInt((yHigh - yLow) / Rise)); float going = run / n, h = (yHigh - yLow) / n;
        for (int i = 0; i < n; i++)
        {
            var s0 = lo + d * (going * i); var s1 = lo + d * (going * (i + 1)); float y0 = yLow + h * i, y1 = y0 + h;
            Face("concrete", 0, -d, s0 - r + up * y0, s0 - r + up * y1, s0 + r + up * y1, s0 + r + up * y0);
            Face("concrete", 0, up, s0 - r + up * y1, s1 - r + up * y1, s1 + r + up * y1, s0 + r + up * y1);
        }
        Face("wall", baseY, d, hi - r + up * baseY, hi - r + up * yHigh, hi + r + up * yHigh, hi + r + up * baseY);
        foreach (float sign in new[] { -1f, 1f })
        {
            var inner = r * sign; var outer = inner + rn * (sign * .25f); float tl = yLow + .5f, th = yHigh + .5f;
            void Q(string kind, Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector3 outward) { Face(kind, baseY, outward, a, b, c, e); Col().Quad(a, b, c, e, outward); }
            Q("wall", lo + inner + up * baseY, hi + inner + up * baseY, hi + inner + up * th, lo + inner + up * tl, -rn * sign);
            Q("wall", lo + outer + up * baseY, hi + outer + up * baseY, hi + outer + up * th, lo + outer + up * tl, rn * sign);
            Q("concrete", lo + inner + up * tl, hi + inner + up * th, hi + outer + up * th, lo + outer + up * tl, up);
            Q("wall", lo + inner + up * baseY, lo + inner + up * tl, lo + outer + up * tl, lo + outer + up * baseY, -d);
            Q("wall", hi + inner + up * baseY, hi + inner + up * th, hi + outer + up * th, hi + outer + up * baseY, d);
        }
        // Collider: a slope through the tread midpoints, then a short flat run onto the landing.
        var ca = lo - d * Rise; var cb = hi - d * Rise;
        Col().Quad(ca - r + up * yLow, cb - r + up * yHigh, cb + r + up * yHigh, ca + r + up * yLow, up);
        Col().Quad(cb - r + up * yHigh, hi - r + up * yHigh, hi + r + up * yHigh, cb + r + up * yHigh, up);
    }

    static void Perimeter()
    {
        region = "Perimeter";
        Mass(-40, 36.8f, -38, 78, 0, 6, "W,N");
        Prism("mass", 0, 6, -40, 36.8f, -40, 36, -34, 30, -33, 30, -33, 36.8f);                 // south-west corner pier
        Mass(-33, 30, -16, 32, 0, 6, "S,N");
        B("mass", -16, 30.2f, -15.2f, 31.8f, 0, 4.2f); B("mass", -10.8f, 30.2f, -10, 31.8f, 0, 3);           // ragged breach jambs
        B("hazard", -16, 30.1f, -10, 31.9f, 4.6f, 5.2f); B("mass", -16, 30, -10, 32, 5.2f, 6);
        B("brick", -14.5f, 28.6f, -13.5f, 29.4f, 0, .6f); B("brick", -12.4f, 32.6f, -11.2f, 33.6f, 0, .8f); B("brick", -13.6f, 33.4f, -12.9f, 34.1f, 0, .45f);
        Mass(-10, 30, -8, 32, 0, 6, "S,N");
        foreach (float s in new[] { -1f, 1f })
        {
            Mass(Mathf.Min(s * 5, s * 8), 29, Mathf.Max(s * 5, s * 8), 33, 0, 8.5f, "S,N,E,W");                                   // gate pylons
            B("roof", Mathf.Min(s * 4.7f, s * 8.3f), 28.7f, Mathf.Max(s * 4.7f, s * 8.3f), 33.3f, 8.5f, 8.9f, false);
        }
        B("steel", -5, 30.4f, 5, 31.6f, 7.5f, 8.1f); B("hazard", -5, 30.3f, 5, 30.4f, 7.6f, 8, false);                           // gate gantry
        Mass(8, 30, 23, 32, 0, 6, "S,N");
        B("mass", 23, 30.2f, 23.9f, 31.8f, 0, 3.6f); B("mass", 32.1f, 30.2f, 33, 31.8f, 0, 4);                                   // collapsed section edges
        // Toppled wall slab: a climbable slope from outside up to the loading terrace.
        {
            Vector3 P(float x, float y, float z) => new(x, y, z); float xa = 24.5f, xb = 30.5f, zl = 29.6f, zh = 34, th = .5f;
            void Q(string kind, Vector3 a, Vector3 b, Vector3 c, Vector3 e, Vector3 outward) { Face(kind, 0, outward, a, b, c, e); Col().Quad(a, b, c, e, outward); }
            Q("plaster", P(xa, 0, zl), P(xa, 2, zh), P(xb, 2, zh), P(xb, 0, zl), Vector3.up);
            Q("brick", P(xa, 0, zl + th), P(xa, 2 - th, zh), P(xb, 2 - th, zh), P(xb, 0, zl + th), Vector3.down);
            Q("brick", P(xa, 0, zl), P(xa, 2, zh), P(xa, 2 - th, zh), P(xa, 0, zl + th), Vector3.left);
            Q("brick", P(xb, 0, zl), P(xb, 2, zh), P(xb, 2 - th, zh), P(xb, 0, zl + th), Vector3.right);
        }
        B("brick", 25, 28.4f, 26.2f, 29.3f, 0, .7f); B("brick", 31, 30.5f, 32, 31.6f, 0, 1.1f); B("brick", 23.4f, 32.6f, 24.4f, 33.6f, 0, .9f); B("brick", 30.8f, 32.6f, 31.8f, 33.6f, 0, 1.5f);
        Mass(38, 53, 40, 78, 0, 6, "E,N");
        for (int z = 40; z <= 76; z += 6) B("mass", -41, z - .6f, -40 - Lip, z + .6f, 0, 5);
        for (int z = 58; z <= 76; z += 6) B("mass", 40 + Lip, z - .6f, 41, z + .6f, 0, 5);
    }

    static void WestKeep()
    {
        region = "West keep";
        Mass(-38, 36.8f, -33, 50, 0, 4, "");                                   // wall-walk: solid behind the terrace...
        Deck(-38, 50, -33, 70, 4);                                             // ...then an arcade open to the yard and bunker room
        foreach (float z in new[] { 59.5f, 64.5f, 69.4f }) Pillar(-33.6f, z, 0, 3.5f);
        Mass(-33, 34, -12, 46, 0, 2, "S,E,N:-22:-12");                         // A: parade terrace
        Mass(-33, 46, -22, 50, 0, 2, "E,N:-28:-22");
        // Loggia: the bunker roof runs four metres south on pillars, sheltering the doors.
        Deck(-33, 54, -12, 58, 4); Lips(-33, 54, -12, 58, 3.5f, 4, "S:-27.75:-12,E");
        foreach (float x in new[] { -32.5f, -27.3f, -19.3f, -12.5f }) Pillar(x, 54.5f, 0, 3.5f);
        // C: bunker with a room below
        B("concrete", -33, 58, -12, 76, 3.5f, 4); Lips(-33, 58, -12, 76, 3.5f, 4, "E,N:-30:-12");
        WallX(58, 59, -26, -12, 0, 3.5f, -24, -21, 0, 2.6f, -17.5f, -14.5f, 0, 2.6f);
        WallZ(-13, -12, 59, 75, 0, 3.5f, 60.2f, 61.8f, 1, 1.8f, 73.2f, 74.8f, 1, 1.8f);
        B("mass", -33, 75, -12, 76, 0, 3.5f);
        foreach (float z in new[] { 63.5f, 69.5f }) B("mass", -23, z, -22, z + 1, 0, 3.5f);
        foreach (float x in new[] { -24f, -17.5f }) { B("hazard", x, 57.9f, x + 3, 58, 2.6f, 3, false); B("lamp", x + 1.2f, 57.82f, x + 1.8f, 57.9f, 3.1f, 3.35f, false); }
        // D: blockhouse where the catwalk lands
        Mass(-20, 64, -14, 72, 4, 8, "S,E");
        foreach (float x in new[] { -20f, -15f }) foreach (float z in new[] { 64f, 71f }) B("mass", x, z, x + 1, z + 1, 8, 10.8f);
        WallX(64, 64.5f, -19, -15, 8, 10.8f, -18.5f, -15.5f, 8.3f, 8.9f); WallX(71.5f, 72, -19, -15, 8, 10.8f, -18.5f, -15.5f, 8.3f, 8.9f);
        B("roof", -21, 63, -13, 73, 10.8f, 11.3f);
        // +8 deck: slabs on pillars standing on the bunker roof, so the roof below stays walkable
        Deck(-30, 72, -20, 76, 8); Deck(-24.5f, 65, -20, 72, 8);
        Pillar(-20.6f, 75.4f, 4, 7.5f); Pillar(-25.3f, 75.4f, 4, 7.5f); Pillar(-23.7f, 65.6f, 4, 7.5f);
        // Keep tower
        Mass(-38, 70, -30, 78, 0, 8, "N");
        foreach (float x in new[] { -38f, -30.7f }) foreach (float z in new[] { 70f, 77.3f }) B("mass", x, z, x + .7f, z + .7f, 8, 11.5f);
        B("roof", -39, 69, -29, 79, 11.5f, 12.2f);
        for (int i = 0; i < 3; i++) { float x = -36.6f + i * 2.1f; B("mass", x, 77.6f, x + 1.1f, 78, 8, 9.1f); B("mass", x, 70, x + 1.1f, 70.4f, 8, 9.1f); }
        for (int i = 0; i < 3; i++) { float z = 71.4f + i * 2.1f; B("mass", -38, z, -37.6f, z + 1.1f, 8, 9.1f); }
        B("mass", -37.3f, 77.7f, -30.7f, 78, 8, 8.3f); B("mass", -37.3f, 70, -30.7f, 70.3f, 8, 8.3f); B("mass", -38, 70.7f, -37.7f, 77.3f, 8, 8.3f);
        Rail(-30, 70.7f, -30, 72.5f, 8); Rail(-30, 75.5f, -30, 77.3f, 8);
        Stair(-8, 43.5f, -12, 43.5f, 0, 2, 3, 0); Stair(-29.5f, 50, -29.5f, 54, 2, 4, 3, 0); Stair(-26, 64, -26, 72, 4, 8, 3, 4);
        // rails
        Rail(-33, 34, -12, 34, 2); Rail(-12, 34, -12, 41.75f, 2); Rail(-12, 45.25f, -12, 46, 2); Rail(-12, 46, -22, 46, 2); Rail(-22, 46, -22, 50, 2); Rail(-22, 50, -27.75f, 50, 2); Rail(-31.25f, 50, -33, 50, 2);
        Rail(-33, 36.8f, -33, 54, 4);
        Rail(-27.75f, 54, -12, 54, 4); Rail(-31.25f, 54, -33, 54, 4);
        Rail(-12, 54, -12, 76, 4); Rail(-12, 76, -30, 76, 4);
        Rail(-20, 76, -30, 76, 8); Rail(-20, 72, -20, 76, 8); Rail(-30, 72, -27.75f, 72, 8); Rail(-24.5f, 65, -24.5f, 72, 8); Rail(-24.5f, 65, -20, 65, 8);
        Rail(-14, 65, -14, 66.5f, 8); Rail(-14, 69.5f, -14, 71, 8);
    }

    static readonly float[] Bastion = { 33, 30, 41.5f, 30, 46, 34.5f, 46, 47, 40, 53, 33, 53 };
    static readonly float[] Tower = { 31.5f, 62, 36.5f, 62, 38, 63.5f, 38, 68.5f, 36.5f, 70, 31.5f, 70, 30, 68.5f, 30, 63.5f };

    static void EastDepot()
    {
        region = "East depot";
        Mass(12, 34, 33, 40, 0, 2, "S,W,N:12:18"); Mass(18, 40, 33, 46, 0, 2, "W,N");     // F: loading terrace around the dock
        Prism("mass", 0, 4, Bastion);                                                      // corner bastion
        Rail(33.2f, 30.2f, 41.4f, 30.2f, 4); Rail(41.4f, 30.2f, 45.8f, 34.6f, 4); Rail(45.8f, 34.6f, 45.8f, 46.9f, 4); Rail(45.8f, 46.9f, 40.1f, 52.6f, 4);
        Rail(33, 30.2f, 33, 41.75f, 4); Rail(33, 45.25f, 33, 50, 4);
        // North-east deck: +4 on pillars over the yard, reached by its own stair, bridged to the gun deck.
        Deck(18, 50, 33, 62, 4); Deck(33, 53, 38, 62, 4); B("concrete", 36.5f, 62, 38, 63.5f, 3.5f, 4);
        Lips(18, 50, 33, 62, 3.5f, 4, "S,W:50:54.75,W:58.25:62,N:18:21,N:25:31.5");
        foreach (float x in new[] { 18.6f, 25.5f, 32.4f }) foreach (float z in new[] { 50.6f, 56, 61.4f }) Pillar(x, z, 0, 3.5f);
        Pillar(37.3f, 53.7f, 0, 3.5f);
        Stair(11, 56.5f, 18, 56.5f, 0, 4, 3, 0);
        Rail(18, 50, 33, 50, 4); Rail(18, 50, 18, 55, 4); Rail(18, 58, 18, 62, 4); Rail(18, 62, 21, 62, 4); Rail(25, 62, 31.5f, 62, 4);
        Deck(21, 62, 25, 66, 4); Rail(21, 62, 21, 66, 4); Rail(25, 62, 25, 66, 4);                                               // bridge to the gun deck
        // Pocket deck north of the watchtower
        Deck(30, 70, 38, 76, 4); Prism("concrete", 3.5f, 4, 30, 68.5f, 31.5f, 70, 30, 70); Prism("concrete", 3.5f, 4, 38, 68.5f, 38, 70, 36.5f, 70);
        Pillar(34, 75.4f, 0, 3.5f); Pillar(37.4f, 75.4f, 0, 3.5f); Rail(30, 76, 38, 76, 4);
        Prism("mass", 0, 8, Tower);                                                        // watchtower
        var roof = new float[Tower.Length]; for (int i = 0; i < Tower.Length; i += 2) { roof[i] = 34 + (Tower[i] - 34) * 1.25f; roof[i + 1] = 66 + (Tower[i + 1] - 66) * 1.25f; }
        Prism("roof", 11.5f, 12.2f, roof);
        foreach (float x in new[] { 31.6f, 35.9f }) foreach (float z in new[] { 63.6f, 67.9f }) B("mass", x, z, x + .5f, z + .5f, 8, 11.5f);
        Rail(31.5f, 62, 33.25f, 62, 8); Rail(31.5f, 62, 30, 63.5f, 8); Rail(30, 63.5f, 30, 66, 8); Rail(30, 68.5f, 31.5f, 70, 8); Rail(31.5f, 70, 36.5f, 70, 8);
        Rail(36.5f, 70, 38, 68.5f, 8); Rail(38, 68.5f, 38, 63.5f, 8); Rail(38, 63.5f, 36.75f, 62.25f, 8);
        // Steel gangway from the tower to the east blockhouse
        B("steel", 20, 66, 30, 68.5f, 7.7f, 8); foreach (float z in new[] { 66.2f, 67.7f }) B("steel", 24.7f, z, 25.3f, z + .6f, 4, 7.7f);
        Rail(20, 66, 30, 66, 8); Rail(20, 68.5f, 30, 68.5f, 8);
        // H: gun deck on an open colonnade
        B("concrete", 12, 66, 30, 76, 3.5f, 4); Lips(12, 66, 30, 76, 3.5f, 4, "S:12:21,S:25:30,W,N");
        B("mass", 12, 75, 30, 76, 0, 3.5f); B("mass", 29, 67.4f, 30, 75, 0, 3.5f);
        WallZ(12, 13, 67.4f, 75, 0, 3.5f, 68.2f, 69.8f, 1, 1.8f, 73.4f, 74.8f, 1, 1.8f);
        foreach (float x in new[] { 12f, 17.5f, 23f, 28.6f }) { B("mass", x, 66, x + 1.4f, 67.4f, 0, 3.5f); B("lamp", x + .4f, 65.92f, x + 1, 66, 2.7f, 2.95f, false); }
        Rail(20, 66, 21, 66, 4); Rail(25, 66, 30, 66, 4); Rail(12, 66, 14, 66, 4); Rail(12, 66, 12, 76, 4); Rail(12, 76, 30, 76, 4); Rail(30, 66, 30, 68.5f, 4);
        // East blockhouse, open toward the gangway and stair
        Mass(14, 66, 20, 73, 4, 8, "N");
        foreach (float z in new[] { 66f, 72f }) B("mass", 14, z, 15, z + 1, 8, 10.8f);
        WallX(66, 66.5f, 15, 20, 8, 10.8f, 15.5f, 19.5f, 8.3f, 8.9f); WallX(72.5f, 73, 15, 20, 8, 10.8f, 15.5f, 19.5f, 8.3f, 8.9f);
        B("roof", 13, 65, 21, 74, 10.8f, 11.3f);
        Rail(14, 69.5f, 14, 72, 8); B("steel", 19.9f, 68.5f, 20.1f, 69, 8, 9.1f);
        Stair(8, 37, 12, 37, 0, 2, 4, 0); Stair(29, 43.5f, 33, 43.5f, 2, 4, 3, 2); Stair(35, 54, 35, 62, 4, 8, 3, 4); Stair(28, 70.5f, 20, 70.5f, 4, 8, 3, 4);
        Rail(12, 34, 24.5f, 34, 2); Rail(30.5f, 34, 33, 34, 2); Rail(12, 34, 12, 34.75f, 2); Rail(12, 39.25f, 12, 40, 2); Rail(18, 46, 33, 46, 2);
    }

    static void Centre()
    {
        region = "Centre";
        // The existing yard mesh has cut-outs under the removed platforms and uses the old fort concrete. A visual
        // asphalt skin 2 cm above it closes both; the yard's own box collider is intact. The track bed is left alone.
        foreach (float s in new[] { -1f, 1f })
        {
            float xa = Mathf.Min(s * 2.2f, s * 48), xb = Mathf.Max(s * 2.2f, s * 48);
            Face("asphalt", 0, Vector3.up, new Vector3(xa, .02f, 28), new Vector3(xa, .02f, 80), new Vector3(xb, .02f, 80), new Vector3(xb, .02f, 28));
        }
        foreach (float s in new[] { -1f, 1f })
        {
            foreach (float z in new[] { 66.5f, 68.9f }) B("steel", s * 7.5f - .3f, z, s * 7.5f + .3f, z + .6f, 0, 7.2f);
            B("steel", s * 7.5f - .4f, 66.5f, s * 7.5f + .4f, 69.5f, 7.2f, 7.5f);
        }
        Rail(-14, 66.5f, 14, 66.5f, 8); Rail(-14, 69.5f, -2, 69.5f, 8); Rail(2, 69.5f, 14, 69.5f, 8); Rail(-2, 69.5f, -2, 72, 8); Rail(2, 69.5f, 2, 72, 8); Rail(-2, 72, 2, 72, 8);
        foreach (var p in new[] { new Vector3(-9.5f, .6f, 49), new(-6.5f, 1.2f, 53.5f), new(-10.5f, 1.8f, 58), new(7, .6f, 45), new(9.5f, 1.2f, 51), new(6.8f, .6f, 63.2f), new(10, 1.8f, 61) })
            B("mass", p.x - 1.25f, p.z - 1.25f, p.x + 1.25f, p.z + 1.25f, 0, p.y);
    }

    static readonly (string name, Vector3 p)[] Coal =
    {
        ("West terrace", new(-17, 2.2f, 37)), ("West terrace", new(-27, 2.2f, 38)), ("West terrace", new(-30, 2.2f, 44)), ("West terrace arm", new(-25, 2.2f, 48)),
        ("West bunker room", new(-28, .2f, 66)), ("West bunker room", new(-16, .2f, 72)), ("West bunker room", new(-27, .2f, 73.5f)),
        ("West bunker roof", new(-16, 4.2f, 60.5f)), ("West wall-walk", new(-35.5f, 4.2f, 46)), ("Keep tower", new(-34, 8.2f, 73)),
        ("East loading terrace", new(15, 2.2f, 36)), ("East loading terrace", new(26, 2.2f, 37)), ("East loading terrace", new(31, 2.2f, 44.8f)), ("East dock", new(16.5f, .2f, 41)),
        ("East bastion", new(42, 4.2f, 41)), ("East bastion", new(36, 4.2f, 33.5f)), ("East colonnade", new(21, .2f, 72)), ("East colonnade", new(27, .2f, 69.5f)),
        ("East gun deck", new(27, 4.2f, 74.5f)), ("East watchtower", new(34, 8.2f, 64.6f)),
    };
    static readonly (string name, Vector3 p)[] Gold =
    {
        ("West terrace", new(-20, 2.1f, 44)), ("West terrace", new(-31, 2.1f, 41)), ("West bunker room", new(-20, .1f, 62)), ("West bunker room", new(-26, .1f, 73)),
        ("West bunker roof", new(-14, 4.1f, 74)), ("West wall-walk", new(-35.5f, 4.1f, 62)), ("West wall-walk", new(-35.5f, 4.1f, 39)), ("West upper deck", new(-22, 8.1f, 74.5f)), ("Keep tower", new(-34, 8.1f, 75)),
        ("East loading terrace", new(28, 2.1f, 38.5f)), ("East loading terrace", new(20, 2.1f, 38.5f)), ("East bastion", new(44, 4.1f, 45)), ("East bastion", new(38, 4.1f, 51)),
        ("East wall-walk nook", new(37.3f, 4.1f, 58)), ("East colonnade", new(15, .1f, 74)), ("East gun deck", new(22, 4.1f, 74.6f)), ("East watchtower", new(34, 8.1f, 67.4f)), ("East yard under gangway", new(25, .1f, 63)),
    };
    static readonly Vector3[] Crates = { new(13.8f, 0, 41.3f), new(16.6f, 0, 44.9f), new(13.7f, 0, 45.1f), new(30.5f, 2, 35.5f), new(31.9f, 2, 35.5f), new(-30.5f, 2, 37.6f), new(-31.5f, 0, 61), new(25.4f, 0, 73.6f), new(-35.5f, 4, 58), new(37, 4, 49.5f) };
    static readonly Vector3[] CrateStacks = { new(-30.5f, 2, 36), new(27, 0, 73.6f) };

    [MenuItem("Duck Rails/Authoring/Finale/Build fort architecture v2")]
    public static void Apply()
    {
        if (Application.dataPath.Replace('\\', '/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying) throw new Exception("Use Duck Rails in Edit Mode");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh(); vis.Clear(); col.Clear(); holders.Clear(); mats.Clear();
        void M(string key, string path) { mats[key] = AssetDatabase.LoadAssetAtPath<Material>(path); if (!mats[key]) throw new Exception("Missing material " + path); }
        const string depot = "Assets/DuckRails/DepotMaterials/";
        M("plaster", depot + "Stained plaster.mat"); M("paint", depot + "Faded olive paint.mat"); M("brick", depot + "Exposed brick.mat"); M("concrete", depot + "Worn concrete.mat");
        M("steel", depot + "Oxidized steel.mat"); M("roof", depot + "Dark roofing.mat"); M("yellow", depot + "Worn yellow.mat"); M("asphalt", depot + "Ground asphalt.mat");
        mats["rail yellow"] = mats["yellow"]; mats["rail steel"] = mats["steel"];
        M("lamp", "Assets/DuckRails/Compound/LightingSample/Emissive work-light lens.mat");
        var log = new StringBuilder(); var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var env = root.transform.Find("Env"); var art = env.Find(RailFinaleFort.RootName); var polish = env.Find("November 11 polish");
            if (!art || !polish) throw new Exception("Unexpected finale hierarchy");
            surfaceTemplate = art.Find("Cross-track catwalk +8")?.GetComponent("AudioSurface");
            if (!surfaceTemplate) log.AppendLine("WARNING: no AudioSurface template found");
            var old = env.Find(GroupName); if (old) Object.DestroyImmediate(old.gameObject);

            // Remove the previous fort masses. Keep the catwalk, balcony and grouped scenery.
            var keep = new HashSet<string> { "Cross-track catwalk +8", "Off-path button balcony", "Abandoned approach traffic jam", "River retaining walls and fortress recovery", "Happy Land" };
            var removed = new Dictionary<string, int>();
            foreach (var t in art.Cast<Transform>().ToArray())
            {
                if (keep.Contains(t.name)) continue; var rs = t.GetComponentsInChildren<Renderer>(true); if (rs.Length == 0) continue;
                var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
                if (b.center.z < 29.5f || b.center.z > 78.5f || Mathf.Abs(b.center.x) > 41) continue;
                removed.TryGetValue(t.name, out int c); removed[t.name] = c + 1; Object.DestroyImmediate(t.gameObject);
            }
            foreach (var t in polish.Cast<Transform>().ToArray())
                if (t.name.StartsWith("South inward ramp") || t.name.StartsWith("Middle ramp") || t.name.StartsWith("Upper short ramp") || t.name == "North bastion climb block")
                { removed.TryGetValue(t.name, out int c); removed[t.name] = c + 1; Object.DestroyImmediate(t.gameObject); }
            log.AppendLine("Removed: " + (removed.Count == 0 ? "nothing" : string.Join("; ", removed.Select(k => k.Key + " x" + k.Value))));
            // The runtime loader rejects a block whose baked bindings point at destroyed renderers, so drop those
            // entries. Surviving geometry keeps its lightmaps; the new fort is unlit by lightmaps until the rebake.
            var lighting = root.GetComponent<RailBakedBlockLighting>();
            if (lighting)
            {
                int before = lighting.bindings.Length; lighting.bindings = lighting.bindings.Where(b => b.renderer != null).ToArray();
                log.AppendLine($"Lighting bindings: {before} -> {lighting.bindings.Length}");
            }

            group = new GameObject(GroupName).transform; group.SetParent(env, false);
            Perimeter(); WestKeep(); EastDepot(); Centre();
            int tris = 0;
            foreach (var kv in vis)
            {
                var parts = kv.Key.Split('|'); bool thin = parts[1].StartsWith("rail") || parts[1] == "lamp";
                var g = new GameObject(parts[0] + " " + parts[1]) { layer = 6 }; g.transform.SetParent(group, false);
                var mesh = kv.Value.Build(parts[0] + " " + parts[1], !thin); tris += kv.Value.t.Count / 3;
                g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterial = mats[parts[1]];
                r.receiveGI = thin ? ReceiveGI.LightProbes : ReceiveGI.Lightmaps; r.scaleInLightmap = .3f; GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI);
            }
            foreach (var kv in col) NewCollisionObject(kv.Key + " mesh collision").AddComponent<MeshCollider>().sharedMesh = kv.Value.Build(kv.Key + " collision", false);
            log.AppendLine($"Built {vis.Count} render meshes, {tris} triangles, {holders.Values.Sum(h => h.GetComponents<BoxCollider>().Length)} box colliders, {col.Count} mesh colliders");

            // Existing dressing and fixtures move onto the new surfaces.
            var singles = polish.Cast<Transform>().Where(t => t.name == "Supply crate").ToArray(); var stacks = polish.Cast<Transform>().Where(t => t.name == "Stacked supply crates").ToArray();
            for (int i = 0; i < singles.Length && i < Crates.Length; i++) singles[i].localPosition = Crates[i];
            for (int i = 0; i < stacks.Length && i < CrateStacks.Length; i++) stacks[i].localPosition = CrateStacks[i];
            log.AppendLine($"Moved {Mathf.Min(singles.Length, Crates.Length)} crates and {Mathf.Min(stacks.Length, CrateStacks.Length)} crate stacks");
            void Flood(string name, Vector3 pos, Vector3 target)
            {
                var g = polish.Find(name); if (!g) { log.AppendLine("WARNING: missing " + name); return; }
                var spot = g.Find("Baked compound floodlight"); var to = target - (pos + Vector3.up * 6); float yaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                g.localPosition = pos; g.localRotation = Quaternion.Euler(0, yaw - (spot ? spot.localEulerAngles.y : 0), 0);
            }
            Flood("West south floodlight", new(-36.5f, 6, 33.8f), new(0, 1.5f, 48)); Flood("West north floodlight", new(-34, 12.2f, 74), new(0, 1.5f, 60));
            Flood("East south floodlight", new(42.5f, 4, 36), new(0, 1.5f, 48)); Flood("East north floodlight", new(34, 12.2f, 66), new(0, 1.5f, 60));
            foreach (var l in env.Find("Baked block sample lights").Cast<Transform>().Where(t => t.name.StartsWith("Bridge control work light") && t.position.x > 0)) l.position = new(34, 11, 66);

            // Spawn markers: counts, order and identity are retained; fort markers are re-seated.
            void Seat(string groupName, string kind, (string name, Vector3 p)[] list)
            {
                var g = root.transform.Find(groupName); if (g.childCount != 8 + list.Length) throw new Exception(groupName + " has " + g.childCount + " markers; expected " + (8 + list.Length));
                for (int i = 0; i < list.Length; i++) { var t = g.GetChild(8 + i); t.localPosition = list[i].p; t.name = $"{kind} {9 + i:00} {list[i].name}"; }
            }
            Seat("Coal Spawns", "Coal", Coal); Seat("Gold Spawns", "Gold", Gold);
            var ne = root.transform.Find("Enemy Spawns/Enemy Northeast tower"); if (ne) ne.localPosition = new(34, 8.05f, 66); else log.AppendLine("WARNING: missing Enemy Northeast tower");
            var reverb = root.transform.Find("Reverb Zones/East bunker"); if (reverb) reverb.localPosition = new(21, 1.8f, 71);

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath); AssetDatabase.SaveAssets();
            log.AppendLine("Saved. Navigation and lighting rebake pending.");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        Debug.Log("Fort architecture v2\n" + log);
    }

    // Static checks on the saved prefab, using its own physics scene. Returns a report; lines starting FAIL need attention.
    public static string Check()
    {
        var log = new StringBuilder(); int pass = 0, fail = 0; var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        void Assert(bool ok, string what) { if (ok) pass++; else { fail++; log.AppendLine("FAIL " + what); } }
        try
        {
            Physics.SyncTransforms(); var phys = root.scene.GetPhysicsScene(); var fort = root.transform.Find("Env/" + GroupName); Assert(fort, "fort group exists");
            var hits = new Collider[64];
            int n = phys.OverlapBox(new Vector3(0, 3.25f, 55), new Vector3(4.4f, 3.05f, 25), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++) Assert(!hits[i].transform.IsChildOf(fort), "train envelope is clear of " + hits[i].name);
            Assert(phys.OverlapBox(new Vector3(-22, 1, 40), Vector3.one * .3f, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore) > 0, "positive control: terrace is solid");
            void Marker(Transform t, float lift, float half)
            {
                var p = t.position; bool hit = phys.Raycast(p + Vector3.up * .6f, Vector3.down, out var h, 1.5f, ~0, QueryTriggerInteraction.Ignore);
                Assert(hit && Mathf.Abs(p.y - lift - h.point.y) < .08f, $"{t.name} sits on its surface (marker y {p.y:F2}, floor {(hit ? h.point.y.ToString("F2") : "none")})");
                Assert(phys.OverlapBox(p + Vector3.up * (half + .04f), Vector3.one * half, hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore) == 0, t.name + " has a clear envelope");
                foreach (var o in new[] { new Vector3(half, 0, half), new(-half, 0, half), new(half, 0, -half), new(-half, 0, -half) })
                    Assert(phys.Raycast(p + o + Vector3.up * .6f, Vector3.down, 1.5f, ~0, QueryTriggerInteraction.Ignore), t.name + " has corner support");
            }
            var coal = root.transform.Find("Coal Spawns"); var gold = root.transform.Find("Gold Spawns"); Assert(coal.childCount == 28 && gold.childCount == 26, "candidate counts are 28 coal / 26 gold");
            for (int i = 8; i < coal.childCount; i++) Marker(coal.GetChild(i), .2f, .16f);
            for (int i = 8; i < gold.childCount; i++) Marker(gold.GetChild(i), .1f, .16f);
            foreach (var name in new[] { "Enemy Northwest tower", "Enemy Northeast tower" })
            {
                var t = root.transform.Find("Enemy Spawns/" + name); var p = t.position;
                Assert(phys.Raycast(p + Vector3.up * .6f, Vector3.down, out var h, 1.5f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(h.point.y - 8) < .06f, name + " stands on a +8 deck");
                Assert(phys.OverlapBox(p + Vector3.up * .9f, new Vector3(.4f, .8f, .4f), hits, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore) == 0, name + " has body clearance");
            }
            // Each stair: the walking surface climbs smoothly from the low landing to the high landing.
            foreach (var s in new[] { (-8f, 43.5f, -12f, 43.5f, 0f, 2f), (-29.5f, 50f, -29.5f, 54f, 2f, 4f), (-26f, 64f, -26f, 72f, 4f, 8f), (8f, 37f, 12f, 37f, 0f, 2f), (29f, 43.5f, 33f, 43.5f, 2f, 4f), (35f, 54f, 35f, 62f, 4f, 8f), (28f, 70.5f, 20f, 70.5f, 4f, 8f), (11f, 56.5f, 18f, 56.5f, 0f, 4f) })
            {
                float last = s.Item5 - .01f; bool ok = true;
                for (int i = 0; i <= 10; i++)
                {
                    var p = Vector3.Lerp(new Vector3(s.Item1, 0, s.Item2), new Vector3(s.Item3, 0, s.Item4), i / 10f);
                    if (!phys.Raycast(p + Vector3.up * (s.Item6 + 1.5f), Vector3.down, out var h, 20, ~0, QueryTriggerInteraction.Ignore) || h.point.y < last - .01f || h.point.y - last > .6f) { ok = false; break; }
                    last = h.point.y;
                }
                Assert(ok && Mathf.Abs(last - s.Item6) < .05f, $"stair at ({s.Item1},{s.Item2}) climbs smoothly to +{s.Item6}");
            }
            // Catwalk, button and deposit are where they were.
            var button = root.transform.Find("Env/Lower bridge button"); Assert(button && Mathf.Abs(button.localPosition.x) < 2 && button.localPosition.z > 69.5f && button.localPosition.z < 72 && button.localPosition.y > 8, "bridge button is on the balcony at " + (button ? button.localPosition.ToString() : "?"));
            Assert(root.transform.Find("Env/Finale gold deposit"), "gold deposit present"); Assert(root.transform.Find("Env/Bridge hinge"), "bridge hinge present");
            Assert(phys.Raycast(new Vector3(0, 9.5f, 68), Vector3.down, out var cat, 3, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(cat.point.y - 8) < .05f, "catwalk deck at +8");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        return $"{pass} passed, {fail} failed\n{log}";
    }
}
#endif
