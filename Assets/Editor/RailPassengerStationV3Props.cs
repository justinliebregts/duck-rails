#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// V3 Passenger Station procedural meshes: swept tunnels, shaped low-poly fixtures, pipes, signs and the
/// crashed commuter train. Meshes are generated here (not imported) so the station rebuilds from code.
/// </summary>
public static partial class RailAbandonedStation
{
    // ---------------- mesh building ----------------
    sealed class MB
    {
        public readonly List<Vector3> v = new(), n = new(); public readonly List<Vector2> uv = new();
        public readonly List<int>[] subs;
        public MB(int subCount = 1) { subs = Enumerable.Range(0, subCount).Select(_ => new List<int>()).ToArray(); }
        // Quad a,b,c,d (a ring); flipped if needed so it faces 'outward'.
        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, int sub = 0, Vector2[] uvs = null)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, outward) < 0) { (b, d) = (d, b); normal = -normal; if (uvs != null) (uvs[1], uvs[3]) = (uvs[3], uvs[1]); }
            int s = v.Count; v.Add(a); v.Add(b); v.Add(c); v.Add(d);
            for (int i = 0; i < 4; i++) n.Add(normal);
            if (uvs != null) uv.AddRange(uvs); else foreach (var p in new[] { a, b, c, d }) uv.Add(BoxUv(p, normal));
            subs[sub].AddRange(new[] { s, s + 1, s + 2, s, s + 2, s + 3 });
        }
        public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, int sub = 0)
        {
            var normal = Vector3.Cross(b - a, c - a).normalized;
            if (Vector3.Dot(normal, outward) < 0) { (b, c) = (c, b); normal = -normal; }
            int s = v.Count; v.Add(a); v.Add(b); v.Add(c);
            for (int i = 0; i < 3; i++) { n.Add(normal); }
            foreach (var p in new[] { a, b, c }) uv.Add(BoxUv(p, normal));
            subs[sub].AddRange(new[] { s, s + 1, s + 2 });
        }
        static Vector2 BoxUv(Vector3 p, Vector3 nn) => (Mathf.Abs(nn.y) > .5f ? new Vector2(p.x, p.z) : Mathf.Abs(nn.x) > .5f ? new Vector2(p.z, p.y) : new Vector2(p.x, p.y)) / 3;
        public Mesh Build(string name)
        {
            var m = new Mesh { name = name };
            if (v.Count > 65000) m.indexFormat = IndexFormat.UInt32;
            m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.subMeshCount = subs.Length;
            for (int i = 0; i < subs.Length; i++) m.SetTriangles(subs[i], i);
            m.RecalculateBounds(); m.RecalculateTangents(); return m;
        }
    }

    static readonly Dictionary<string, Mesh> procMeshes = new();
    static Mesh SaveProc(string key, Func<MB> make, bool lightmap)
    {
        if (procMeshes.TryGetValue(key, out var cached)) return cached;
        string path = Folder + "/Meshes/Proc-" + key.Replace(' ', '-') + ".asset";
        var m = make().Build(System.IO.Path.GetFileNameWithoutExtension(path));
        if (lightmap) Unwrapping.GenerateSecondaryUVSet(m);
        var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (old) { EditorUtility.CopySerialized(m, old); Object.DestroyImmediate(m); m = old; EditorUtility.SetDirty(m); }
        else AssetDatabase.CreateAsset(m, path);
        procMeshes[key] = m; return m;
    }

    // Places a procedural mesh. Static (lightmapped) or probe-lit, with an optional mesh collider.
    static GameObject Proc(Transform p, string n, Mesh mesh, Material[] mats, Vector3 pos, Quaternion rot, bool staticLit, bool collide)
    {
        var g = Group(p, n).gameObject; g.layer = 6; g.transform.localPosition = pos; g.transform.localRotation = rot;
        g.AddComponent<MeshFilter>().sharedMesh = mesh; var r = g.AddComponent<MeshRenderer>(); r.sharedMaterials = mats;
        if (staticLit) { r.receiveGI = ReceiveGI.Lightmaps; r.scaleInLightmap = .22f; GameObjectUtility.SetStaticEditorFlags(g, StaticEditorFlags.ContributeGI); }
        else { r.receiveGI = ReceiveGI.LightProbes; GameObjectUtility.SetStaticEditorFlags(g, 0); }
        if (collide) g.AddComponent<MeshCollider>().sharedMesh = mesh;
        return g;
    }

    // Revolve a profile of (radius, height) points, listed bottom to top along the outside, around Y.
    static MB Lathe(MB mb, Vector2[] prof, int seg, float sx = 1, float sz = 1, Vector3 off = default, int sub = 0)
    {
        for (int i = 0; i < prof.Length - 1; i++)
        {
            var p0 = prof[i]; var p1 = prof[i + 1]; var n2 = new Vector2(p1.y - p0.y, -(p1.x - p0.x));
            for (int s = 0; s < seg; s++)
            {
                float a0 = s * Mathf.PI * 2 / seg, a1 = (s + 1) * Mathf.PI * 2 / seg, am = (a0 + a1) / 2;
                Vector3 P(Vector2 q, float a) => off + new Vector3(Mathf.Cos(a) * q.x * sx, q.y, Mathf.Sin(a) * q.x * sz);
                var outward = new Vector3(Mathf.Cos(am) * n2.x, n2.y, Mathf.Sin(am) * n2.x);
                if (outward.sqrMagnitude < 1e-6f) outward = Vector3.up;
                if (Mathf.Abs(p0.x) < 1e-4f) mb.Tri(P(p0, a0), P(p1, a0), P(p1, a1), outward, sub);
                else if (Mathf.Abs(p1.x) < 1e-4f) mb.Tri(P(p0, a0), P(p1, a0), P(p0, a1), outward, sub);
                else mb.Quad(P(p0, a0), P(p1, a0), P(p1, a1), P(p0, a1), outward, sub);
            }
        }
        return mb;
    }

    // Box in a mesh builder (for compound procedural props).
    static MB Cube(MB mb, Vector3 c, Vector3 s, int sub = 0)
    {
        var h = s / 2;
        Vector3 C(float x, float y, float z) => c + new Vector3(x * h.x, y * h.y, z * h.z);
        mb.Quad(C(-1, -1, 1), C(1, -1, 1), C(1, 1, 1), C(-1, 1, 1), Vector3.forward, sub);
        mb.Quad(C(-1, -1, -1), C(-1, 1, -1), C(1, 1, -1), C(1, -1, -1), Vector3.back, sub);
        mb.Quad(C(1, -1, -1), C(1, 1, -1), C(1, 1, 1), C(1, -1, 1), Vector3.right, sub);
        mb.Quad(C(-1, -1, -1), C(-1, -1, 1), C(-1, 1, 1), C(-1, 1, -1), Vector3.left, sub);
        mb.Quad(C(-1, 1, -1), C(-1, 1, 1), C(1, 1, 1), C(1, 1, -1), Vector3.up, sub);
        mb.Quad(C(-1, -1, -1), C(1, -1, -1), C(1, -1, 1), C(-1, -1, 1), Vector3.down, sub);
        return mb;
    }

    // Extrude a closed 2D polygon (x, y) along local Z from z0 to z1, with fan-capped ends (convex outlines).
    static MB Extrude(MB mb, Vector2[] poly, float z0, float z1, bool caps = true, int sub = 0)
    {
        var centre = poly.Aggregate(Vector2.zero, (a, b) => a + b) / poly.Length;
        for (int i = 0; i < poly.Length; i++)
        {
            var a = poly[i]; var b = poly[(i + 1) % poly.Length]; var mid = (a + b) / 2;
            var outward = (Vector3)(mid - centre);
            mb.Quad(new(a.x, a.y, z0), new(b.x, b.y, z0), new(b.x, b.y, z1), new(a.x, a.y, z1), outward, sub);
        }
        if (caps)
            for (int i = 0; i < poly.Length; i++)
            {
                var a = poly[i]; var b = poly[(i + 1) % poly.Length];
                mb.Tri(new(centre.x, centre.y, z0), new(a.x, a.y, z0), new(b.x, b.y, z0), Vector3.back, sub);
                mb.Tri(new(centre.x, centre.y, z1), new(a.x, a.y, z1), new(b.x, b.y, z1), Vector3.forward, sub);
            }
        return mb;
    }

    // ---------------- swept tunnel ----------------
    // One continuous mesh per tunnel: U-shaped section (two walls and the roof) swept along the curved,
    // graded track every 0.5 m. East wall strips are left out across the fan bays.
    static void SweepTunnel(Transform t, string name, float z0, float z1, float[] fans)
    {
        float hi = TunnelHalf + .2f, lo = TunnelHalf - .2f, top = TunnelRoof + .3f, bottom = -.5f;
        Vector2[] loop = { new(-hi, bottom), new(-hi, top), new(hi, top), new(hi, bottom), new(lo, bottom), new(lo, TunnelRoof), new(-lo, TunnelRoof), new(-lo, bottom) };
        bool[] roofEdge = { false, true, false, false, false, true, false, false };
        bool[] eastEdge = { false, false, true, true, true, false, false, false };
        string key = name + "-" + TunnelRoof.ToString("F2") + "-" + TunnelHalf.ToString("F2");
        var mesh = SaveProc(key, () =>
        {
            var mb = new MB(2);
            Vector3 Centre(float z) => new(RailX(z), RailHeight(z), z);
            Vector3 Lat(float z) { var tg = new Vector3(RailX(z + .25f) - RailX(z - .25f), 0, .5f); return Vector3.Cross(Vector3.up, tg).normalized; }
            float along = 0;
            for (float z = z0; z < z1 - .001f; z += .5f)
            {
                float za = z, zb = Mathf.Min(z1, z + .5f); var ca = Centre(za); var cb = Centre(zb); var la = Lat(za); var lb = Lat(zb);
                bool gap = fans.Any(f => Mathf.Abs((za + zb) / 2 - f) < 2.5f);
                float step = (cb - ca).magnitude; float u0 = 0;
                for (int i = 0; i < loop.Length; i++)
                {
                    var p = loop[i]; var q = loop[(i + 1) % loop.Length]; float len = (q - p).magnitude;
                    var n2 = new Vector2(-(q.y - p.y), q.x - p.x).normalized;   // the loop runs clockwise
                    if (!(gap && eastEdge[i]))
                    {
                        var outward = la * n2.x + Vector3.up * n2.y;
                        Vector2[] uvs = { new(along / 3, u0 / 3), new(along / 3, (u0 + len) / 3), new((along + step) / 3, (u0 + len) / 3), new((along + step) / 3, u0 / 3) };
                        mb.Quad(ca + la * p.x + Vector3.up * p.y, ca + la * q.x + Vector3.up * q.y, cb + lb * q.x + Vector3.up * q.y, cb + lb * p.x + Vector3.up * p.y, outward, roofEdge[i] ? 1 : 0, uvs);
                    }
                    u0 += len;
                }
                // Close the east wall where a fan bay starts or ends.
                bool gapNext = fans.Any(f => Mathf.Abs(zb + .25f - f) < 2.5f) && zb < z1 - .001f;
                if (gap != gapNext && zb < z1 - .001f)
                {
                    var dirT = (cb - ca).normalized;
                    mb.Quad(cb + lb * lo + Vector3.up * bottom, cb + lb * hi + Vector3.up * bottom, cb + lb * hi + Vector3.up * TunnelRoof, cb + lb * lo + Vector3.up * TunnelRoof, gap ? -dirT : dirT);
                }
                along += step;
            }
            return mb;
        }, true);
        Proc(t, name, mesh, new[] { tile, roof }, Vector3.zero, Quaternion.identity, true, true);
    }

    // ---------------- signs (Compound style) ----------------
    static Material signOlive, signIvory;
    static void SignMaterials()
    {
        signOlive = AtlasTile("Sign olive board", "olive paint", true, 0, 192, 64, new(.52f, .55f, .4f), 4);
        signIvory = AtlasTile("Sign ivory", "white panel", false, 128, 0, 32, new(.95f, .9f, .74f), 2);
    }

    // Olive board with timber trim, big ivory CutePixel letters and an area-colour strip. arrow: 'u','d','l','r' or ' '.
    // yaw keeps the old Sign() convention: 0 faces +Z, 180 faces -Z, 90 faces +X.
    static Transform SignBoard(Transform p, string text, Vector3 pos, float yaw, float letterH, char arrow = ' ', float hangTo = float.NaN, bool twoSided = false)
    {
        var t = Group(p, "Sign " + text); t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw - 180, 0);
        float arrowW = arrow == ' ' ? 0 : letterH * 1.3f;
        float textW = text.Length * letterH * .78f;
        float w = textW + arrowW + letterH * 1.4f, h = letterH * 1.75f;
        Box(t, "Sign board", new(0, 0, .03f), new(w, h, .05f), signOlive, false);
        foreach (float y in new[] { h / 2 + .035f, -h / 2 - .035f }) Box(t, "Sign timber trim", new(0, y, .03f), new(w + .12f, .07f, .09f), wood, false);
        Box(t, "Sign area strip", new(0, -h / 2 - .095f, .03f), new(w + .04f, .05f, .06f), band, false);
        void Face(float side)
        {
            var f = Group(t, side > 0 ? "Front" : "Back"); if (side < 0) f.localRotation = Quaternion.Euler(0, 180, 0);
            float textX = arrowW / 2 * (arrow == 'r' ? -1 : 1);
            var label = Group(f, "Lettering").gameObject.AddComponent<TMPro.TextMeshPro>();
            label.transform.localPosition = new(textX, 0, side > 0 ? -.006f : -.066f);
            label.font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset"); label.text = text;
            label.enableAutoSizing = true; label.fontSizeMin = 1; label.fontSizeMax = letterH * 14; label.fontSize = letterH * 14;
            label.alignment = TMPro.TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new(textW + .1f, h * .8f); label.color = new(.95f, .9f, .74f);
            if (arrow != ' ')
            {
                float ax = (arrow == 'r' ? textW / 2 + arrowW * .35f : -textW / 2 - arrowW * .35f) + textX;
                var a = Group(f, "Arrow"); a.localPosition = new(ax, 0, side > 0 ? -.012f : -.072f);
                a.localRotation = Quaternion.Euler(0, 0, arrow == 'u' ? 0 : arrow == 'd' ? 180 : arrow == 'l' ? 90 : -90);
                Box(a, "Arrow shaft", new(0, -letterH * .1f, 0), new(letterH * .2f, letterH * .75f, .012f), signIvory, false);
                foreach (float s in new[] { -1f, 1f }) Box(a, "Arrow head", new(s * letterH * .16f, letterH * .2f, 0), new(letterH * .2f, letterH * .5f, .012f), signIvory, false).transform.localRotation = Quaternion.Euler(0, 0, s * 40);
            }
        }
        Face(1); if (twoSided) Face(-1);
        if (!float.IsNaN(hangTo))
        {
            float rodLen = hangTo - (pos.y + h / 2 + .07f);
            if (rodLen > .02f) foreach (float x in new[] { -w * .35f, w * .35f }) Box(t, "Sign hanger rod", new(x, h / 2 + .07f + rodLen / 2, .03f), new(.04f, rodLen, .04f), steel, false);
        }
        else foreach (float x in new[] { -w * .3f, w * .3f }) Box(t, "Sign bracket", new(x, 0, .09f), new(.06f, h * .6f, .08f), steel, false);
        return t;
    }

    // ---------------- pipes (Compound style) ----------------
    static Material PipeMat(Material zone)
    {
        var c = zone.GetColor("_BaseColor");
        return AtlasTile("Pipe paint " + zone.name, "rust", false, 96, 32, 32, Color.Lerp(c, new(.55f, .4f, .35f), .35f), 2);
    }
    static Mesh PipeMesh(float r, float len) => SaveProc("pipe-" + r.ToString("F2") + "-" + len.ToString("F2"), () =>
        Lathe(new MB(), new[] { new Vector2(r, 0), new Vector2(r, len) }, 8), false);

    // A pipe run through the given points: 8-sided pipe, flanges every 3 m and at bends, hangers up to ceilingY.
    static void PipeRun(Transform p, string name, Vector3[] pts, float r, Material mat, float ceilingY = float.NaN)
    {
        var t = Group(p, name);
        var flange = SaveProc("flange-" + r.ToString("F2"), () => Lathe(new MB(), new[] { new Vector2(r * 1.45f, 0), new Vector2(r * 1.45f, .07f) }, 8), false);
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var a = pts[i]; var b = pts[i + 1]; float len = (b - a).magnitude; if (len < .01f) continue;
            var rot = Quaternion.FromToRotation(Vector3.up, (b - a) / len);
            Proc(t, "Pipe", PipeMesh(r, Mathf.Round(len * 100) / 100), new[] { mat }, a, rot, false, false);
            for (float d = 0; d <= len + .01f; d += 3f)
            {
                var at = a + (b - a) * (Mathf.Min(d, len) / len);
                Proc(t, "Pipe flange", flange, new[] { steel }, at - (b - a) / len * .035f, rot, false, false);
                if (!float.IsNaN(ceilingY) && Mathf.Abs((b - a).y) < .01f && ceilingY - at.y > r + .03f && d > 0)
                {
                    float hang = ceilingY - at.y;
                    Box(t, "Pipe hanger", at + Vector3.up * (hang / 2), new(.03f, hang, .03f), steel, false);
                    Box(t, "Pipe hanger strap", at - Vector3.up * (r + .015f), new(.04f, .03f, .04f), steel, false).transform.localRotation = rot;
                }
            }
        }
    }

    // ---------------- low-poly fixtures ----------------
    static Mesh UrinalMesh() => SaveProc("urinal", () =>
    {
        var mb = new MB();
        Lathe(mb, new[] { new Vector2(0, .0f), new Vector2(.12f, .02f), new Vector2(.2f, .15f), new Vector2(.22f, .4f), new Vector2(.19f, .62f), new Vector2(.12f, .7f), new Vector2(0, .72f) }, 8, 1, .75f);
        Lathe(mb, new[] { new Vector2(.025f, .7f), new Vector2(.025f, 1.25f) }, 6, 1, 1, new Vector3(0, 0, -.12f));   // flush pipe
        Cube(mb, new Vector3(0, 1.3f, -.12f), new Vector3(.12f, .12f, .1f));                                        // flush valve
        return mb;
    }, false);
    static Mesh ToiletMesh() => SaveProc("toilet", () =>
    {
        var mb = new MB();
        Lathe(mb, new[] { new Vector2(0, 0), new Vector2(.14f, 0), new Vector2(.15f, .2f), new Vector2(.21f, .38f), new Vector2(.2f, .41f), new Vector2(0, .41f) }, 10, .85f, 1.15f, new Vector3(0, 0, .08f));
        Lathe(mb, new[] { new Vector2(.12f, .41f), new Vector2(.22f, .41f), new Vector2(.22f, .44f), new Vector2(.12f, .44f) }, 10, .85f, 1.15f, new Vector3(0, 0, .08f));   // seat ring
        Cube(mb, new Vector3(0, .62f, -.25f), new Vector3(.42f, .38f, .18f));   // cistern
        Cube(mb, new Vector3(0, .82f, -.25f), new Vector3(.45f, .03f, .21f));   // cistern lid
        Cube(mb, new Vector3(.15f, .78f, -.15f), new Vector3(.06f, .03f, .03f)); // flush lever
        return mb;
    }, false);
    static Mesh BasinMesh() => SaveProc("basin", () =>
    {
        var mb = new MB();
        Lathe(mb, new[] { new Vector2(0, 0), new Vector2(.08f, 0), new Vector2(.22f, .12f), new Vector2(.25f, .16f), new Vector2(.25f, .18f), new Vector2(0, .18f) }, 10, 1.1f, .85f);
        Lathe(mb, new[] { new Vector2(.03f, -.55f), new Vector2(.04f, 0) }, 6);                                    // pedestal
        Lathe(mb, new[] { new Vector2(.02f, .18f), new Vector2(.02f, .36f) }, 6, 1, 1, new Vector3(0, 0, -.2f));   // tap riser
        Cube(mb, new Vector3(0, .35f, -.12f), new Vector3(.035f, .035f, .17f));                                   // spout
        return mb;
    }, false);
    static Mesh BinMesh() => SaveProc("bin", () => Lathe(new MB(), new[] { new Vector2(0, 0), new Vector2(.16f, 0), new Vector2(.19f, .55f), new Vector2(.2f, .56f), new Vector2(0, .56f) }, 8), false);
    static Mesh DryerMesh() => SaveProc("hand dryer", () => Extrude(new MB(), new[] { new Vector2(-.14f, -.12f), new Vector2(.14f, -.12f), new Vector2(.16f, .04f), new Vector2(.12f, .14f), new Vector2(-.12f, .14f), new Vector2(-.16f, .04f) }, 0, .22f), false);
    static Mesh PersonSignMesh(bool woman) => SaveProc(woman ? "pictogram woman" : "pictogram man", () =>
    {
        var mb = new MB();
        Lathe(mb, new[] { new Vector2(0, -.01f), new Vector2(.07f, -.01f), new Vector2(.07f, .01f), new Vector2(0, .01f) }, 8, 1, 1, new Vector3(0, .32f, 0));
        if (woman) Extrude(mb, new[] { new Vector2(-.14f, -.12f), new Vector2(.14f, -.12f), new Vector2(.05f, .22f), new Vector2(-.05f, .22f) }, -.01f, .01f);
        else Cube(mb, new Vector3(0, .07f, 0), new Vector3(.17f, .3f, .02f));
        Cube(mb, new Vector3(-.05f, -.24f, 0), new Vector3(.05f, .24f, .02f)); Cube(mb, new Vector3(.05f, -.24f, 0), new Vector3(.05f, .24f, .02f));
        return mb;
    }, false);

    static GameObject Fixture(Transform p, string n, Mesh m, Material mat, Vector3 pos, float yaw, bool collide = true, float roll = 0)
        => Proc(p, n, m, new[] { mat }, pos, Quaternion.Euler(0, yaw, roll), false, collide);

    // Stall partition: raised off the floor, posts with feet, optional door with hinges and coat hook.
    static void StallPanel(Transform p, Vector3 a, Vector3 b, Material m)
    {
        var mid = (a + b) / 2; float len = (b - a).magnitude; var rot = Quaternion.LookRotation(b - a);
        var panel = Box(p, "Stall partition", mid + Vector3.up * 1.05f, new(.03f, 1.7f, len), m, true); panel.transform.localRotation = rot;
    }

    // ---------------- men's and women's toilets ----------------
    static Material ceramic, checker, vinyl, terrazzo, carpet, whitePanel;
    static void Toilets(Transform p)
    {
        float pc = PlatformClear;
        // Split along Z 58.5: men's (Z 53..58.5) and women's (58.5..64), each with its own platform doorway.
        var men = RoomV3(p, "Men's toilet", -36, 53, -25, 58.5f, F, pc, ceramic, checker, null, null, Solid, Solid);
        var women = RoomV3(p, "Women's toilet", -36, 58.5f, -25, 64, F, pc, ceramic, checker, null, null, null, Solid);
        foreach (var (room, z0, z1, isMen) in new[] { (men, 53f, 58.5f, true), (women, 58.5f, 64f, false) })
        {
            float door = isMen ? 55.5f : 61.0f;
            // Privacy screen just inside the doorway.
            Wall(room, "Privacy screen", true, -26.7f, door - 1.4f, door + 1.4f, F, 2.1f, ceramic);
            var sign = SignBoard(room, isMen ? "MEN" : "WOMEN", new(-24.84f, -1.05f, door), 90, .22f);
            Fixture(sign, "Pictogram", PersonSignMesh(!isMen), signIvory, new(isMen ? -.4f : -.55f, 0, -.012f), 0, false).transform.localScale = Vector3.one * .9f;
            // Basins with a framed (cracked) mirror on the divider wall side.
            float basinZ = isMen ? 58.185f : 63.685f; int basins = isMen ? 2 : 3;   // wall inner face - 0.19
            for (int i = 0; i < basins; i++)
            {
                float x = -31.2f + i * 1.05f;
                Fixture(room, "Basin", BasinMesh(), tile, new(x, F + .62f, basinZ - .22f), 180);
                TopLoot(isMen ? "men's toilet" : "women's toilet", new(x + .45f, F, basinZ - .55f), .3f);
            }
            float mw = basins * 1.05f; float mx = -31.2f + (basins - 1) * .525f;
            Box(room, "Mirror frame", new(mx, -1.3f, basinZ + .115f), new(mw + .1f, .9f, .03f), steel, false);
            Box(room, "Mirror glass", new(mx, -1.3f, basinZ + .095f), new(mw - .05f, .78f, .01f), whitePanel, false);
            Box(room, "Mirror crack", new(mx + .3f, -1.25f, basinZ + .088f), new(.02f, .6f, .004f), darkTile, false).transform.localRotation = Quaternion.Euler(0, 0, 28);
            Fixture(room, "Hand dryer", DryerMesh(), tile, new(-28.4f, -1.55f, basinZ + .18f), 180, false);
            Fixture(room, "Soap dispenser", SaveProc("soap", () => Cube(new MB(), Vector3.zero, new(.1f, .16f, .08f)), false), steel, new(-27.3f, -1.45f, basinZ + .12f), 0, false);
            var bin = Fixture(room, "Toppled bin", BinMesh(), steel, new(-27.6f, F + .2f, basinZ - .8f), 30, true, 90);
            Box(room, "Puddle", new(-30.2f, F + .004f, (z0 + z1) / 2), new(1.4f, .006f, .9f), roof, false);
            if (isMen)
            {
                for (int i = 0; i < 3; i++) Fixture(room, "Urinal", UrinalMesh(), tile, new(-34.0f + i * .9f, F + .45f, z0 + .32f), 0);
                // Two stalls along the west wall.
                for (int i = 0; i < 2; i++)
                {
                    float sz = 55.6f + i * 1.35f;
                    Fixture(room, "Toilet", ToiletMesh(), tile, new(-35.55f, F, sz), 90);
                }
                foreach (float sz in new[] { 54.9f, 56.25f, 57.6f }) StallPanel(room, new(-36, F, sz), new(-34.4f, F, sz), paint);
                Box(room, "Stall door", new(-34.4f, F + 1.05f, 55.6f), new(.03f, 1.7f, 1.2f), paint, false);
                var hanging = Box(room, "Stall door (hanging off)", new(-34.2f, F + .85f, 56.95f), new(.03f, 1.7f, 1.2f), paint, false);
                hanging.transform.localRotation = Quaternion.Euler(0, 38, 14);
                Enemy("men's toilet", -31.5f, F, 56.3f);
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    float sz = 59.15f + i * 1.2f;
                    Fixture(room, "Toilet", ToiletMesh(), tile, new(-35.55f, F, sz + .6f), 90);
                    StallPanel(room, new(-36, F, sz), new(-34.4f, F, sz), paint);
                    var d = Box(room, "Stall door", new(-34.4f, F + 1.05f, sz + .6f), new(.03f, 1.7f, 1.1f), paint, false);
                    if (i == 2) { d.transform.localRotation = Quaternion.Euler(0, 70, 0); d.transform.localPosition += new Vector3(.5f, 0, -.4f); }
                    Box(room, "Door hinge", new(-34.4f, F + 1.6f, sz + .1f), new(.05f, .08f, .04f), steel, false);
                    Box(room, "Coat hook", new(-35.97f, F + 1.65f, sz + .6f), new(.06f, .04f, .04f), steel, false);
                }
                Enemy("women's toilet", -30.5f, F, 61.2f);
            }
            Tube(room, new(-30.5f, F + pc - .08f, (z0 + z1) / 2), false, isMen ? 1 : 0);
            FloorLoot(isMen ? "men's toilet" : "women's toilet", -33.8f, z0 + .5f, -27.2f, z1 - .5f, F, .3f, 2.2f, .5f);
        }
    }

    // ---------------- procedural pixel textures for room finishes ----------------
    static Material GenMaterial(string name, Func<int, int, System.Random, Color> pixel, float metresPerRepeat, Color tint)
    {
        string texPath = Folder + "/Textures/Gen " + name + ".png";
        if (!System.IO.File.Exists(texPath))
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGB24, false); var rng = new System.Random(name.GetHashCode() & 0xffff);
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) tex.SetPixel(x, y, pixel(x, y, rng));
            tex.Apply(); System.IO.Directory.CreateDirectory(Folder + "/Textures"); System.IO.File.WriteAllBytes(texPath, tex.EncodeToPNG()); Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(texPath);
            var imp = (TextureImporter)AssetImporter.GetAtPath(texPath); imp.filterMode = FilterMode.Point; imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.maxTextureSize = 64; imp.SaveAndReimport();
        }
        var t = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        string path = Folder + "/Finish " + name + ".mat"; var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Compound/SubwayTerminal/Materials/Subway Town atlas.mat")); AssetDatabase.CreateAsset(m, path); }
        float s = 3f / metresPerRepeat;
        m.SetTexture("_BaseMap", t); m.SetTexture("_MainTex", t); m.SetTextureScale("_BaseMap", new(s, s)); m.SetTextureScale("_MainTex", new(s, s));
        m.SetColor("_BaseColor", tint); m.SetColor("_Color", tint); m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }
    static Color Noise(System.Random r, Color c, float amount) { float k = 1 + ((float)r.NextDouble() - .5f) * amount; return new(c.r * k, c.g * k, c.b * k); }
    static void FinishMaterials()
    {
        // 64 px tiles at the Compound's 16 px/m: 4 m repeats, 4 px ceramic tiles with 1 px grout.
        ceramic = GenMaterial("ceramic tile", (x, y, r) => x % 4 == 0 || y % 4 == 0 ? new Color(.62f, .62f, .6f) : Noise(r, new(.9f, .9f, .86f), .08f), 4, new(.85f, .85f, .82f));
        checker = GenMaterial("checker floor", (x, y, r) => ((x / 8) + (y / 8)) % 2 == 0 ? Noise(r, new(.82f, .8f, .74f), .1f) : Noise(r, new(.25f, .26f, .28f), .2f), 4, new(.8f, .8f, .78f));
        vinyl = GenMaterial("vinyl floor", (x, y, r) => x % 32 == 0 || y % 32 == 0 ? new Color(.55f, .56f, .55f) : Noise(r, new(.74f, .75f, .73f), r.NextDouble() < .05 ? .6f : .06f), 4, new(.85f, .85f, .83f));
        terrazzo = GenMaterial("terrazzo", (x, y, r) => { double d = r.NextDouble(); return d < .08 ? new Color(.35f, .33f, .3f) : d < .14 ? new Color(.85f, .8f, .7f) : d < .18 ? new Color(.6f, .45f, .35f) : Noise(r, new(.66f, .64f, .6f), .05f); }, 4, new(.8f, .8f, .78f));
        carpet = GenMaterial("carpet", (x, y, r) => ((x + y) % 8 < 2 ? new Color(.2f, .24f, .34f) : Noise(r, new(.26f, .3f, .42f), .25f)), 2, new(.85f, .85f, .85f));
        whitePanel = AtlasTile("Atlas white panel", "white panel", false, 128, 0, 32, new(.82f, .82f, .8f), 2);
    }

    // Lower wall finish (wainscot) along the inside of a room's walls.
    static void Wainscot(Transform p, float x0, float z0, float x1, float z1, float y, float height, Material m, float[] w, float[] e, float[] s, float[] n)
    {
        var keep = band; band = m;
        void Run(bool alongZ, float axis, float a, float b, float[] gaps)
        {
            if (gaps == null) return; float cursor = a;
            for (int i = 0; i <= gaps.Length; i += 2)
            {
                float end = i < gaps.Length ? gaps[i] : b;
                if (end - cursor > .2f) Box(p, "Wainscot", alongZ ? new(axis, y + height / 2, (cursor + end) / 2) : new((cursor + end) / 2, y + height / 2, axis), alongZ ? new(.02f, height, end - cursor) : new(end - cursor, height, .02f), m, false);
                if (i < gaps.Length) cursor = gaps[i + 1];
            }
        }
        Run(true, x0 + .137f, z0 + .14f, z1 - .14f, w); Run(true, x1 - .137f, z0 + .14f, z1 - .14f, e);
        Run(false, z0 + .137f, x0 + .14f, x1 - .14f, s); Run(false, z1 - .137f, x0 + .14f, x1 - .14f, n);
        band = keep;
    }
}
#endif
