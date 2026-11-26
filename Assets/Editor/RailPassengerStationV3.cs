#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>
/// V3 Passenger Station (Nov 26 2026): Tokyo-style metro. Live Platform 1 on the west, stopped escalators
/// up to two concourses over the live track, back down to abandoned Platform 3 with a three-car wreck
/// buried by a cave-in. Layout sheet: output/passenger-station-v3/layout-v1.png. Block-local metres.
/// </summary>
public static partial class RailAbandonedStation
{
    const float F = -3f;                 // main floor level
    const float Deck = 1f;                // concourse floor level (crawl graph cells sit at n+0.5, so floors avoid n+0.5)
    const float PlatformClear = 2.4f, HallClear = 2.13f;
    static Material tile, band, floorTile, darkTile, wreckRed, rubbleMat, glowTube, glowFlicker, glowRed, glowAmber, glowScreen, glowWindow, deadTube;
    struct Spot { public Vector3 p; public float gold; public string area; }
    static List<Spot> lootSpots;
    static List<(Vector3 p, string area)> enemySpots;
    static int v3Lights;
    static readonly float[] Solid = new float[0];
    static readonly Color Cool = new(.72f, .84f, 1f), Warm = new(1f, .77f, .5f), Red = new(1f, .16f, .1f), Amber = new(1f, .58f, .25f), Moon = new(.6f, .7f, 1f);
    // Escalator shafts: x0 of a 4.5 m wide shaft; 18 m runs keep the 6 m rise walkable.
    const float WestShaft = -23.2f, EastShaft = -4.6f;

    // Area colours: wall stripes and a tint on that area's tube light, so players can tell where they are.
    static Material zoneGreen, zoneBlue, zonePurple, zoneRed, zoneYellow, zoneOrange, zoneTeal;
    static Color zoneTint = Color.white;
    static void Zone(Material stripe) { band = stripe; zoneTint = stripe.GetColor("_BaseColor"); }

    static void V3Materials()
    {
        // Same Quackville atlas tiles and 16 px/m density as the Compound subway terminal (plaster walls,
        // speckled concrete floors, timber, dark ceilings), cut into repeating tiles for box UVs.
        Color dim = new(.72f, .71f, .66f);
        tile = AtlasTile("Atlas plaster", "plaster", true, 0, 64, 64, dim, 4);
        concrete = AtlasTile("Atlas concrete", "concrete", true, 192, 128, 64, new(.66f, .66f, .63f), 4);
        floorTile = AtlasTile("Atlas speckled floor", "speckled concrete", false, 32, 0, 32, dim, 2);
        darkTile = AtlasTile("Atlas grimy concrete", "concrete", true, 192, 128, 64, new(.42f, .42f, .4f), 4);
        paint = AtlasTile("Atlas olive paint", "olive paint", true, 0, 192, 64, new(.66f, .68f, .6f), 4);
        roof = AtlasTile("Atlas dark ceiling", "dark asphalt", true, 128, 128, 64, new(.5f, .5f, .52f), 4);
        steel = AtlasTile("Atlas metal panel", "metal panel", false, 96, 0, 32, new(.62f, .64f, .66f), 2);
        wood = AtlasTile("Atlas timber", "timber", true, 192, 64, 64, dim, 4);
        yellow = AtlasTile("Atlas yellow paint", "yellow paint", true, 0, 128, 64, new(.85f, .8f, .62f), 4);
        gravel = AtlasTile("Atlas gravel", "gravel", true, 64, 0, 64, new(.6f, .6f, .58f), 4);
        grass = AtlasTile("Atlas grass", "grass", true, 0, 0, 64, new(.6f, .65f, .55f), 4);
        wreckRed = AtlasTile("Atlas wreck red", "red paint", true, 192, 192, 64, new(.78f, .62f, .58f), 4);
        rubbleMat = AtlasTile("Atlas rubble", "rock", true, 192, 0, 64, new(.62f, .6f, .56f), 4);
        deadTube = Mat("Dead tube", "weathered paint", new(.3f, .32f, .33f));
        zoneGreen = AtlasTile("Stripe green (Platform 1)", "white panel", false, 128, 0, 32, new(.25f, .78f, .38f), 2);
        zoneBlue = AtlasTile("Stripe blue (ticket concourse)", "white panel", false, 128, 0, 32, new(.28f, .52f, 1f), 2);
        zonePurple = AtlasTile("Stripe purple (shuttered concourse)", "white panel", false, 128, 0, 32, new(.66f, .38f, .95f), 2);
        zoneRed = AtlasTile("Stripe red (Platform 3)", "white panel", false, 128, 0, 32, new(.95f, .2f, .16f), 2);
        zoneYellow = AtlasTile("Stripe yellow (west halls)", "white panel", false, 128, 0, 32, new(1f, .85f, .25f), 2);
        zoneOrange = AtlasTile("Stripe orange (maintenance)", "white panel", false, 128, 0, 32, new(1f, .52f, .15f), 2);
        zoneTeal = AtlasTile("Stripe teal (cable rooms)", "white panel", false, 128, 0, 32, new(.2f, .8f, .8f), 2);
        band = zoneGreen;
        SignMaterials(); FinishMaterials();
        glowTube = Glow("Glow tube steady", new(.82f, .9f, 1f), 2.2f, 0);
        glowFlicker = Glow("Glow tube flickering", new(.82f, .9f, 1f), 2.2f, .55f);
        glowRed = Glow("Glow emergency red", new(1f, .12f, .08f), 2.4f, 0);
        glowAmber = Glow("Glow wreck window amber", new(1f, .6f, .28f), 1.2f, .12f);
        glowScreen = Glow("Glow machine screen", new(.45f, .85f, .75f), 1.4f, .2f);
        glowWindow = Glow("Glow cable gallery window", new(1f, .78f, .45f), 1.6f, 0);
    }

    // Crops one atlas tile (pixel rect, top-left origin) into its own repeating texture and a material
    // cloned from the Compound subway's atlas material. Box UVs are metres/3, so scale by 3/repeat.
    static Material AtlasTile(string matName, string texName, bool town, int px, int py, int size, Color tint, float metresPerRepeat)
    {
        string texPath = Folder + "/Textures/Tile " + texName + ".png";
        if (!System.IO.File.Exists(texPath))
        {
            var atlas = new Texture2D(2, 2);
            atlas.LoadImage(System.IO.File.ReadAllBytes("Assets/Models/Quackville/Quackville_" + (town ? "Town" : "Industrial") + "_Diffuse.jpg"));
            var crop = new Texture2D(size, size, TextureFormat.RGB24, false);
            crop.SetPixels(atlas.GetPixels(px, atlas.height - py - size, size, size)); crop.Apply();
            System.IO.Directory.CreateDirectory(Folder + "/Textures");
            System.IO.File.WriteAllBytes(texPath, crop.EncodeToPNG());
            Object.DestroyImmediate(atlas); Object.DestroyImmediate(crop);
            AssetDatabase.ImportAsset(texPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.filterMode = FilterMode.Point; importer.wrapMode = TextureWrapMode.Repeat; importer.mipmapEnabled = true; importer.maxTextureSize = 64;
            importer.SaveAndReimport();
        }
        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
        string path = Folder + "/" + matName + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m)
        {
            var template = AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Compound/SubwayTerminal/Materials/Subway Town atlas.mat");
            if (!template) throw new Exception("Missing Compound atlas material");
            m = new Material(template); AssetDatabase.CreateAsset(m, path);
        }
        float s = 3f / metresPerRepeat;
        m.SetTexture("_BaseMap", tex); m.SetTexture("_MainTex", tex);
        m.SetTextureScale("_BaseMap", new Vector2(s, s)); m.SetTextureScale("_MainTex", new Vector2(s, s));
        m.SetColor("_BaseColor", tint); m.SetColor("_Color", tint); m.enableInstancing = true;
        EditorUtility.SetDirty(m); return m;
    }

    static Material Glow(string n, Color c, float intensity, float flicker)
    {
        string path = Folder + "/" + n + ".mat";
        var shader = Shader.Find("DuckRails/StationGlow");
        if (!shader) throw new Exception("Missing DuckRails/StationGlow shader");
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader; m.SetColor("_BaseColor", c); m.SetFloat("_Intensity", intensity); m.SetFloat("_Flicker", flicker);
        m.enableInstancing = true; EditorUtility.SetDirty(m); return m;
    }

    static GameObject GlowBox(Transform p, string n, Vector3 pos, Vector3 size, Material m)
    {
        var g = Box(p, n, pos, size, m, false);
        GameObjectUtility.SetStaticEditorFlags(g, 0);
        var r = g.GetComponent<MeshRenderer>(); r.receiveGI = ReceiveGI.LightProbes; r.shadowCastingMode = ShadowCastingMode.Off;
        return g;
    }

    static void PL(Transform p, string n, Vector3 pos, Color c, float intensity, float range)
    {
        var l = Group(p, n).gameObject.AddComponent<Light>(); l.transform.localPosition = pos;
        l.type = LightType.Point; l.lightmapBakeType = LightmapBakeType.Baked; l.intensity = intensity; l.range = range; l.color = c; l.shadows = LightShadows.Soft;
        v3Lights++;
    }

    // state: 0 steady, 1 flickering (glow only, dimmer light), 2 dead (no glow, no light)
    static void Tube(Transform p, Vector3 pos, bool alongZ, int state, bool light = true, float range = 9)
    {
        Vector3 S(float w, float h, float l) => alongZ ? new(w, h, l) : new(l, h, w);
        Box(p, "Tube housing", pos + Vector3.up * .06f, S(.26f, .06f, 1.3f), steel, false);
        if (state == 2) Box(p, "Dead tube", pos, S(.12f, .06f, 1.2f), deadTube, false);
        else GlowBox(p, state == 1 ? "Flickering tube" : "Tube", pos, S(.12f, .06f, 1.2f), state == 1 ? glowFlicker : glowTube);
        if (light && state != 2) PL(p, "Baked tube light", pos - Vector3.up * .2f, Color.Lerp(Cool, zoneTint, .5f), state == 1 ? 7 : 16, range);
    }

    static void RedLamp(Transform p, Vector3 pos, float yaw)
    {
        var t = Group(p, "Emergency lamp"); t.localPosition = pos; t.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(t, "Emergency lamp housing", Vector3.zero, new(.35f, .25f, .12f), steel, false);
        GlowBox(t, "Emergency lamp glow", new(0, 0, .08f), new(.28f, .16f, .05f), glowRed);
        PL(p, "Baked emergency light", pos + Quaternion.Euler(0, yaw, 0) * new Vector3(0, 0, .5f), Red, 9, 7);
    }

    static Transform RoomV3(Transform p, string name, float x0, float z0, float x1, float z1, float y, float clear, Material wallM, Material floorM, float[] w, float[] e, float[] s, float[] n)
    {
        var t = Group(p, name);
        Slab(t, "Floor", x0, z0, x1 - x0, z1 - z0, y, floorM);
        Slab(t, "Ceiling", x0, z0, x1 - x0, z1 - z0, y + clear + .3f, roof);
        float h = clear + .3f;
        if (w != null) Wall(t, "Wall", true, x0, z0, z1, y, h, wallM, w);
        if (e != null) Wall(t, "Wall", true, x1, z0, z1, y, h, wallM, e);
        if (s != null) Wall(t, "Wall", false, z0, x0, x1, y, h, wallM, s);
        if (n != null) Wall(t, "Wall", false, z1, x0, x1, y, h, wallM, n);
        // Area-colour stripe on the inside of every wall this room builds.
        if (w != null) Stripe(t, true, x0 + .15f, z0, z1, y + 1.6f, w);
        if (e != null) Stripe(t, true, x1 - .15f, z0, z1, y + 1.6f, e);
        if (s != null) Stripe(t, false, z0 + .15f, x0, x1, y + 1.6f, s);
        if (n != null) Stripe(t, false, z1 - .15f, x0, x1, y + 1.6f, n);
        return t;
    }

    static void Stripe(Transform p, bool alongZ, float axis, float a, float b, float y, float[] gaps)
    {
        float cursor = a;
        for (int i = 0; i <= gaps.Length; i += 2)
        {
            float end = i < gaps.Length ? gaps[i] : b;
            if (end - cursor > .2f)
                Box(p, "Area stripe", alongZ ? new(axis, y, (cursor + end) / 2) : new((cursor + end) / 2, y, axis), alongZ ? new(.03f, .3f, end - cursor) : new(end - cursor, .3f, .03f), band, false);
            if (i < gaps.Length) cursor = gaps[i + 1];
        }
    }

    static void FloorLoot(string area, float x0, float z0, float x1, float z1, float y, float gold, float step = 2.2f, float margin = .7f)
    {
        for (float x = x0 + margin; x <= x1 - margin + .01f; x += step)
            for (float z = z0 + margin; z <= z1 - margin + .01f; z += step)
                lootSpots.Add(new Spot { p = new(x, y, z), gold = gold, area = area });
    }
    static void TopLoot(string area, Vector3 top, float gold) => lootSpots.Add(new Spot { p = top, gold = gold, area = area });
    static void Enemy(string area, float x, float y, float z) => enemySpots.Add((new(x, y, z), area));

    static void Sign3(Transform p, string text, Vector3 pos, float yaw, float scale)
    {
        // Compound-style board (RailPassengerStationV3Props.SignBoard); old scales map to letter heights.
        SignBoard(p, text, pos, yaw, scale >= .45f ? .45f : scale >= .3f ? .26f : .22f);
    }

    // Band of line colour along a wall face, broken at openings.
    static void BandAlongZ(Transform p, float x, float z0, float z1, float y, params float[] gaps)
    {
        float cursor = z0;
        for (int i = 0; i <= gaps.Length; i += 2)
        {
            float end = i < gaps.Length ? gaps[i] : z1;
            if (end - cursor > .2f) Box(p, "Line colour band", new(x, y, (cursor + end) / 2), new(.03f, .28f, end - cursor), band, false);
            if (i < gaps.Length) cursor = gaps[i + 1];
        }
    }

    static void BuildV3(Transform root, RailBlock b)
    {
        Tunnels(Group(root, "Approach and departure tunnels"));
        Envelope(Group(root, "Earth envelope"));
        var west = Group(root, "Platform 1 (live)");
        Platform1(west);
        Platform3(Group(root, "Platform 3 (abandoned)"));
        WreckV3(Group(root, "Buried wreck and cave-in"));
        var esc = Group(root, "Escalators");
        Escalator(esc, "South escalators to Platform 1", WestShaft, 65f, 53f);
        Escalator(esc, "North escalators to Platform 1", WestShaft, 95f, 107f);
        Escalator(esc, "South escalators to Platform 3", EastShaft, 65f, 53f);
        Escalator(esc, "North escalators to Platform 3", EastShaft, 95f, 107f);
        Concourse(Group(root, "Ticket concourse (south)"), 40, 53, true);
        Concourse(Group(root, "Shuttered concourse (north)"), 107, 120, false);
        WestRooms(Group(root, "West rooms and transfer hall"));
        Facade(Group(root, "South facade (King's Quack)"), true);
        Facade(Group(root, "North facade (King's Quack)"), false);
        EastRooms(Group(root, "East maintenance side"));
        BuildPipes(root);
    }

    // ---------------- tunnels ----------------
    static void Tunnels(Transform t)
    {
        // Train-sized tunnel: walls 3.05 m from the rail centre (train 2.3 m half width plus curve swing),
        // roof underside at least 2 m above the train top (rail + 1 + 2.56 + 2), plus slope allowance.
        zoneTint = Cool;
        float[] fans = { 18, 28, 38 };
        // Walls and roof are one continuous swept mesh per tunnel (Justin, Nov 26); the loop adds fan bays, lights and windows.
        SweepTunnel(t, "Approach tunnel (swept)", 6, 53, fans);
        SweepTunnel(t, "Departure tunnel (swept)", 107, 154, new float[0]);
        for (int z = 6; z < 154; z++)
        {
            if (z >= 53 && z < 107) continue;
            var a = new Vector3(RailX(z), RailHeight(z), z); var c = new Vector3(RailX(z + 1), RailHeight(z + 1), z + 1);
            float wallY = (TunnelRoof + .2f - .5f) / 2, wallH = TunnelRoof + .7f;   // tops 2 cm under the roof top (no coplanar faces)
            foreach (float side in new[] { -1f, 1f })
            {
                bool fanGap = side > 0 && fans.Any(f => Mathf.Abs(z + .5f - f) < 2.5f);
                if (fanGap)
                {
                    // Recessed ventilation fan bay in the east wall.
                    Beam(t, "Fan bay back wall", a + new Vector3(TunnelHalf + 1.6f, wallY, 0), c + new Vector3(TunnelHalf + 1.6f, wallY, 0), .4f, wallH, darkTile);
                    Beam(t, "Fan bay floor", a + new Vector3(TunnelHalf + .8f, -.157f + (z % 2) * .004f, 0), c + new Vector3(TunnelHalf + .8f, -.157f + (z % 2) * .004f, 0), 1.6f, .3f, gravel);
                    Beam(t, "Fan bay ceiling", a + new Vector3(TunnelHalf + .7f, TunnelRoof + .142f, 0), c + new Vector3(TunnelHalf + .7f, TunnelRoof + .142f, 0), 1.8f, .3f, roof);
                }
            }
            if (z % 12 == 0) Tube(t, (a + c) / 2 + Vector3.up * (TunnelRoof - .08f), true, z % 36 == 0 ? 1 : 0, true, 12);
            // Lit cable-gallery windows staggered along the outside of the departure curve.
            if (z >= 112 && z < 135 && z % 5 == 2)
            {
                var g = GlowBox(t, "Cable gallery window (lit)", (a + c) / 2 + new Vector3(-TunnelHalf + .23f, 2.4f, 0), new(.05f, .9f, 1.5f), glowWindow);
                g.transform.localRotation = Quaternion.LookRotation(c - a);
                Box(t, "Window frame", (a + c) / 2 + new Vector3(-TunnelHalf + .21f, 2.4f, 0), new(.04f, 1.1f, 1.7f), steel, false).transform.localRotation = Quaternion.LookRotation(c - a);
            }
        }
        // Fan bay end caps and the three stopped fans facing the track.
        foreach (float f in fans)
        {
            foreach (float zz in new[] { f - 2.5f, f + 2.5f })
            {
                var e = new Vector3(RailX(zz), RailHeight(zz), zz);
                Box(t, "Fan bay end wall", e + new Vector3(TunnelHalf + .8f, (TunnelRoof + .3f - .5f) / 2, 0), new(2f, TunnelRoof + .8f, .4f), darkTile);
            }
            var fc = new Vector3(RailX(f) + TunnelHalf + 1.21f, RailHeight(f) + 1.9f, f);
            var tangent = new Vector3(RailX(f + .5f) - RailX(f - .5f), 0, 1).normalized;
            var fan = Group(t, "Stopped ventilation fan"); fan.localPosition = fc; fan.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, tangent) * -1, Vector3.up);
            Box(fan, "Fan shroud", Vector3.zero, new(3f, 3f, .3f), steel, false);
            Box(fan, "Fan hub", new(0, 0, -.25f), new(.6f, .6f, .3f), darkTile, false);
            for (int i = 0; i < 4; i++)
            {
                var blade = Box(fan, "Fan blade", new(0, 0, -.2f), new(.4f, 2.6f, .06f), concrete, false);
                blade.transform.localRotation = Quaternion.Euler(0, 0, i * 45 + f); blade.transform.localPosition += Vector3.forward * (i * .006f);
            }
            Box(fan, "Fan guard bar", new(0, 0, -.42f), new(2.8f, .08f, .06f), steel, false);
            Box(fan, "Fan guard bar", new(0, 0, -.43f), new(.08f, 2.8f, .06f), steel, false);
        }
    }

    // Tunnel cross-section around the rail centre line (see Tunnels).
    const float TunnelHalf = 3.25f, TunnelRoof = 3.95f;   // Justin, Nov 26: 2 m lower; ~0.4 m over the train top (rail + 3.56)

    static void Envelope(Transform t)
    {
        foreach (float z in new[] { 0f, 154f }) { Slab(t, "Flat seam", -48, z, 43.5f, 6, 0, grass); Slab(t, "Flat seam", 4.5f, z, 43.5f, 6, 0, grass); }   // the retained rail bed fills the middle
        foreach (float x in new[] { -48f, 44f }) Slab(t, "Flat side boundary", x, 6, 4, 148, 0, grass);
        // RailRoute places the existing midpoint shop at (-16,0,148) on any selected middle block.
        Slab(t, "Reserved midpoint shop pad", -44, 140, 38, 14, 0, concrete);
        foreach (float x in new[] { -44f, 43.75f }) Box(t, "Buried outer retaining wall", new(x, -1, 80), new(.5f, 20, 148), gravel);
        // Nov 26: the overburden roof over the whole block is left out on request so the block is easy to look
        // around; rooms, platforms and tunnels keep their own ceilings. Put it back (Y 9, hole over the cave-in) later.
        Slab(t, "Buried foundation", -44, 6, 87.75f, 148, -11, gravel);
        foreach (float z in new[] { 6f, 154f })
        {
            Wall(t, "Buried end wall", false, z, -44, -TunnelHalf - .2f, -11, 20, gravel);
            Wall(t, "Buried end wall", false, z, TunnelHalf + .2f, 43.75f, -11, 20, gravel);
            Box(t, "Tunnel portal lintel", new(0, (TunnelRoof + .3f + 9) / 2, z), new(TunnelHalf * 2 + .4f, 9 - TunnelRoof - .3f, .25f), gravel);
        }
    }
    // ---------------- Platform 1 ----------------
    static void Platform1(Transform t)
    {
        Zone(zoneGreen);
        // The platform top is level with the train deck (rail + 1) and its edge sits 2.17 m from the rail
        // centre, 8 cm from the hull. Within 7 m of each end the train body swings ~0.5 m toward this
        // platform on the curve, so the edge steps back to 2.7 m there.
        const float edgeMid = -12 - 2.17f, edgeEnd = -12 - 2.7f;
        Slab(t, "Platform floor", -25, 60, edgeMid + 25, 40, F, floorTile);
        foreach (float z in new[] { 53f, 100f }) Slab(t, "Platform floor", -25, z, edgeEnd + 25, 7, F, floorTile);
        Box(t, "Platform edge face", new(edgeMid - .095f, -3.505f, 80), new(.2f, .99f, 40), concrete);   // 5 mm proud of the slab side
        foreach (float z in new[] { 59.9f, 100.1f }) Box(t, "Platform step face", new((edgeMid + edgeEnd) / 2, -3.505f, z), new(edgeMid - edgeEnd, .99f, .2f), concrete);
        foreach (float z in new[] { 56.5f, 103.5f }) Box(t, "Platform edge face", new(edgeEnd - .095f, -3.505f, z), new(.2f, .99f, 7), concrete);
        foreach (float z in new[] { 53f, 107f }) Box(t, "Platform end face", new((-25 + edgeEnd) / 2, -3.65f, z), new(edgeEnd + 25, .7f, .2f), concrete);
        // Low ceiling over the platform, holes over the escalator shafts; it stops at the channel wall.
        float ceil = F + PlatformClear + .3f, wallMid = -12 - 2.6f, wallEnd = -12 - 3.1f;
        Slab(t, "Platform ceiling", -25, 53, WestShaft + 25, 54, ceil, roof);
        Slab(t, "Platform ceiling", WestShaft + 4.5f, 60, wallMid - (WestShaft + 4.5f), 40, ceil, roof);
        foreach (float z in new[] { 53f, 100f }) Slab(t, "Platform ceiling", WestShaft + 4.5f, z, wallEnd - (WestShaft + 4.5f), 7, ceil, roof);
        Slab(t, "Platform ceiling", WestShaft, 65f, 4.5f, 30, ceil, roof);
        // Track channel: same low roof as the tunnels (2 m over the train), walls just outside the train.
        float roofTop = -4 + TunnelRoof + .3f, upperH = roofTop - (F + PlatformClear);
        Wall(t, "Rail channel upper wall", true, wallMid, 60, 100, F + PlatformClear + .01f, upperH - .01f, tile);
        foreach (float z in new[] { 53f, 100f }) Wall(t, "Rail channel upper wall", true, wallEnd, z, z + 7, F + PlatformClear + .01f, upperH - .01f, tile);
        foreach (float z in new[] { 60f, 100f }) Wall(t, "Rail channel upper wall", false, z, wallEnd, wallMid, F + PlatformClear + .02f, upperH - .02f, tile);
        Wall(t, "Rail channel upper wall", true, -12 + 2.6f, 53, 107, F + PlatformClear + .01f, upperH - .01f, tile);
        Slab(t, "Rail channel roof", wallEnd - .1f, 53, 5.9f, 54, roofTop + .006f, roof);
        // Shopfront wall and ends (the tunnels have no walkways, so the ends are closed).
        Wall(t, "Shopfront wall", true, -25, 53, 110, F, PlatformClear + .3f, tile, 54.75f, 56.25f, 60.25f, 61.75f, 66, 72, 77, 81, 89, 97, 104, 106);
        BandAlongZ(t, -24.86f, 53, 107, -1.4f, 54.75f, 56.25f, 60.25f, 61.75f, 66, 72, 77, 81, 89, 97, 104, 106);
        foreach (float z in new[] { 53f, 107f }) Wall(t, "Platform end wall", false, z, -25, wallEnd - .1f, F, PlatformClear + .3f, tile);
        Box(t, "Tactile strip", new(edgeEnd - .45f, F + .012f, 80), new(.3f, .024f, 54), yellow, false);
        Box(t, "Platform edge line", new(edgeMid - .07f, F + .012f, 80), new(.12f, .024f, 40), yellow, false);
        foreach (float z in new[] { 56.5f, 103.5f }) Box(t, "Platform edge line", new(edgeEnd - .07f, F + .012f, z), new(.12f, .024f, 7), yellow, false);
        // Platform screen door units, stuck open with wide gaps for boarding (outside the 2.33 m train body).
        for (float z = 63.5f; z < 99; z += 7)
        {
            Box(t, "Platform door unit", new(-14.6f, F + .65f, z), new(.2f, 1.3f, 1.6f), steel);
            Box(t, "Platform door top light (dead)", new(-14.6f, F + 1.33f, z), new(.22f, .05f, 1.2f), deadTube, false);
        }
        foreach (float z in new[] { 73.5f, 86.5f })
        {
            Box(t, "Platform column", new(-20.95f, F + 1.2f, z), new(.6f, 2.4f, .6f), tile);
            Box(t, "Column band", new(-20.95f, -1.75f, z), new(.64f, .28f, .64f), band, false);
        }
        foreach (var (x, z) in new[] { (-22.6f, 80f), (-17.9f, 80f) })
        {
            Box(t, "Platform bench", new(x, F + .22f, z), new(.6f, .45f, 3f), wood);
            TopLoot("platform 1", new(x, F + .45f, z - .9f), .15f); TopLoot("platform 1", new(x, F + .45f, z + .9f), .15f);
        }
        Sign3(t, "KING'S QUACK", new(-5.14f, -1.35f, 66), -90, .45f);
        Sign3(t, "KING'S QUACK", new(-5.14f, -1.35f, 94), -90, .45f);
        foreach (float z in new[] { 72f, 88f }) SignBoard(t, "1   DAWN LINE", new(-17.6f, F + 2.07f, z), 0, .26f, ' ', F + PlatformClear, true);
        SignBoard(t, "TICKET GATES  EXIT", new(-20.95f, F + 2.07f, 66.5f), 0, .26f, 'u', F + PlatformClear);
        SignBoard(t, "TICKET GATES  EXIT", new(-20.95f, F + 2.07f, 93.5f), 180, .26f, 'u', F + PlatformClear);
        int k = 0;
        for (float z = 56; z <= 104; z += 6, k++)
        {
            int state = k % 4 == 3 ? 2 : k % 5 == 2 ? 1 : 0;
            Tube(t, new(-16.9f, F + PlatformClear - .08f, z), true, state, k % 2 == 0 || state == 1);
        }
        foreach (float z in new[] { 74f, 80f, 86f }) Tube(t, new(-23.9f, F + PlatformClear - .08f, z), true, z == 80 ? 1 : 0, z != 74);
        Enemy("platform 1 south", -16.6f, F, 72); Enemy("platform 1 north", -16.6f, F, 88); Enemy("platform 1 middle", -23.6f, F, 80);

        // East service ledge beside the train (no side of the train is a dead end).
        Slab(t, "Service ledge floor", -12 + 2.17f, 53, -4.9f - (-12 + 2.17f), 54, F, floorTile);
        Box(t, "Service ledge edge face", new(-12 + 2.265f, -3.505f, 80), new(.2f, .99f, 54), concrete);
        foreach (float z in new[] { 53f, 107f })
        {
            Box(t, "Service ledge end face", new(-7.4f, -3.65f, z), new(4.9f, .7f, .2f), concrete);
            Wall(t, "Service ledge end wall", false, z, -12 + 2.6f, -4.9f, F, PlatformClear + .3f, tile);
        }
        Slab(t, "Service ledge ceiling", -12 + 2.6f, 53, -4.9f - (-12 + 2.6f), 54, F + PlatformClear + .3f, roof);
        Wall(t, "Ledge back wall", true, -4.9f, 53, 107, F, -4 + TunnelRoof + .3f - F, tile, 79, 81.5f);
        BandAlongZ(t, -5.06f, 53, 107, -1.4f, 79, 81.5f);
        Box(t, "Ledge edge line", new(-12 + 2.24f, F + .012f, 80), new(.12f, .024f, 54), yellow, false);
        Tube(t, new(-6.9f, F + PlatformClear - .08f, 62), true, 0); Tube(t, new(-6.9f, F + PlatformClear - .08f, 80), true, 1); Tube(t, new(-6.9f, F + PlatformClear - .08f, 98), true, 2);
        Sign3(t, "STAFF ONLY", new(-5.14f, -1.5f, 77.4f), -90, .3f);
        FloorLoot("service ledge", -8.6f, 54, -5.3f, 106, F, .35f, 4.4f, .5f);
        Enemy("service ledge", -7f, F, 96);
    }

    // ---------------- Platform 3 and dead track ----------------
    static void Platform3(Transform t)
    {
        Zone(zoneRed);
        Slab(t, "Platform 3 floor", -4.75f, 53, 9f, 54, F, darkTile);   // edge 0.15 m from the wreck
        // Ends run under the curving tunnel wall so no gap opens beside it.
        Slab(t, "Platform 3 south end", -5.2f, 46, 9.2f, 7, F, darkTile);
        Slab(t, "Platform 3 north end", -5.2f, 107, 9.2f, 7, F, darkTile);
        Box(t, "Platform 3 edge face", new(4.155f, -3.505f, 80), new(.2f, .99f, 60), concrete);
        float ceil = F + PlatformClear + .3f;
        Slab(t, "Platform 3 ceiling", EastShaft + 4.5f, 53, 4 - (EastShaft + 4.5f), 45, ceil, roof);   // to Z 98
        Slab(t, "Platform 3 ceiling", EastShaft + 4.5f, 98, 2 - (EastShaft + 4.5f), 9, ceil, roof);     // beside the cave-in
        Slab(t, "Platform 3 ceiling", -4.75f, 65f, 4.65f, 30, ceil, roof);
        Slab(t, "Platform 3 ceiling", -5.2f, 46, 9.2f, 7, ceil, roof);
        foreach (float z in new[] { 46f, 107f }) Wall(t, "Platform 3 end west wall", true, -5.2f, z, z + 7, F, PlatformClear + .3f, darkTile);
        Slab(t, "Platform 3 ceiling", -5.2f, 107, 9.2f, 7, ceil, roof);
        Wall(t, "Platform 3 south wall", false, 46, -5.2f, 13, F, PlatformClear + .3f, darkTile);
        Wall(t, "Platform 3 north wall", false, 114, -5.2f, 4.05f, F, PlatformClear + .3f, darkTile);
        Box(t, "Platform 3 edge line", new(4.13f, F + .012f, 80), new(.12f, .024f, 66), yellow, false);
        // Dead track: bed, non-catalogue rails, east service walkway, high channel ceiling.
        Slab(t, "Dead track bed", 3.75f, 50, 5.95f, 60, -4f, gravel);
        Slab(t, "Dead track bed (tunnel)", 4.05f, 110, 4.1f, 12, -4.004f, gravel);
        foreach (float x in new[] { 5.2f, 6.8f }) Box(t, "Dead track rail", new(x, -3.92f, 80.5f), new(.1f, .12f, 61), steel, false);
        for (float z = 51; z < 111; z += 2) Box(t, "Dead track tie", new(6f, -3.98f, z), new(2.4f, .08f, .25f), wood, false);
        // The line the wreck was leaving on: its own tunnel north of the platform, collapsed and impassable.
        var tunnel = Group(t, "Collapsed north tunnel");
        Wall(tunnel, "Dead tunnel wall", true, 4.05f, 110, 122, -4.3f, 5.2f, darkTile);
        Wall(tunnel, "Dead tunnel wall", true, 8.15f, 110, 122, -4.3f, 5.2f, darkTile);
        Wall(tunnel, "Dead tunnel end", false, 122, 4.05f, 8.15f, -4.29f, 5.19f, rubbleMat);
        Slab(tunnel, "Dead tunnel roof", 3.9f, 110, 4.4f, 12, .9f, roof);
        P(tunnel, "Portal pillar", 4.15f, -1.705f, 109.84f, .7f, 5.15f, .3f, tile);
        P(tunnel, "Portal pillar", 8.6f, -1.705f, 109.84f, 2.2f, 5.15f, .3f, tile);
        P(tunnel, "Portal lintel", 6.1f, .48f, 109.82f, 4.5f, .74f, .3f, tile);
        P(tunnel, "Portal hazard band", 6.1f, .08f, 109.68f, 3.2f, .12f, .03f, yellow, false);
        P(tunnel, "Dead signal post", 8.45f, -2.4f, 109.55f, .12f, 2.6f, .12f, steel);
        P(tunnel, "Dead signal head", 8.45f, -.9f, 109.5f, .45f, .8f, .3f, darkTile);
        foreach (float y in new[] { -.7f, -1.1f }) P(tunnel, "Dead signal lens", 8.45f, y, 109.34f, .22f, .22f, .03f, y > -1 ? wreckRed : deadTube, false);
        Sign3(tunnel, "LINE CLOSED", new(6.1f, -.15f, 109.6f), 180, .3f);
        var bent = P(tunnel, "Bent rail", 5.2f, -3.4f, 111.6f, .1f, .12f, 2.6f, steel); bent.transform.localRotation = Quaternion.Euler(-28, 6, 0);
        var fill = new System.Random(19);
        for (float z = 110.6f; z < 121.6f; z += .9f)
            for (float y = -3.6f; y < .5f; y += .9f)
                for (int k = 0; k < 3; k++)
                {
                    float s = .9f + fill.Next(3) * .25f, hd = .825f * s;   // few sizes keep the box mesh assets shared
                    float xMin = 4.18f + hd, xMax = 8.02f - hd, x = xMin + (float)fill.NextDouble() * Mathf.Max(0, xMax - xMin);
                    float yy = Mathf.Min(y, .58f - hd), zz = Mathf.Min(z, 121.85f - hd);
                    var rock = Box(tunnel, "Collapse fill", new(x, yy, zz), new(s, s * .85f, s), rubbleMat);
                    rock.transform.localRotation = Quaternion.Euler(fill.Next(360), fill.Next(360), fill.Next(360));
                }
        foreach (float z in new[] { 111.5f, 115f, 119f })
        {
            var slab = Box(tunnel, "Fallen roof slab", new(6.1f, -2.2f + (z - 111) * .12f, z), new(3.2f, .3f, 2.2f), roof);
            slab.transform.localRotation = Quaternion.Euler(z * 3 % 20 - 10, 0, z * 7 % 12 - 6);
        }
        Box(t, "Buffer stop", new(6f, -3.4f, 50.25f), new(2.6f, 1.2f, .5f), steel);
        Slab(t, "Dead track east walkway", 7.65f, 50, 2.05f, 60, F, concrete);   // 0.18 m from the wreck's doors
        Box(t, "Walkway edge face", new(7.645f, -3.505f, 80), new(.2f, .99f, 60), concrete);
        Box(t, "Dead track wall base", new(9.7f, -3.65f, 80), new(.25f, 1.28f, 60), darkTile);
        Wall(t, "Dead track wall", true, 9.7f, 50, 110, F, 3.9f, darkTile, WreckSideDoorZ - .85f, WreckSideDoorZ + .85f);
        Wall(t, "Dead track channel upper wall", true, 4f, 50, 98, F + PlatformClear + .01f, 1.49f, darkTile);
        Wall(t, "Dead track channel upper wall", true, 4f, 107, 110, F + PlatformClear + .01f, 1.49f, darkTile);
        Slab(t, "Dead track channel ceiling", 3.75f, 50, 6.1f, 48, .9f, roof);
        Slab(t, "Dead track channel ceiling", 3.75f, 107, 6.1f, 3, .9f, roof);
        Wall(t, "Dead track end wall", false, 50, 3.75f, 9.7f, F + PlatformClear + .02f, 1.48f, darkTile);
        // Abandoned furniture, toppled screen doors, dead tubes, red emergency lamps.
        for (float z = 56.5f; z < 106; z += 7)
        {
            if (Mathf.Abs(z - WreckSideDoorZ) < 2) continue;   // keep the way to the wreck's side door clear
            var d = Box(t, z % 2 < 1 ? "Toppled platform door" : "Platform door unit (abandoned)", z % 2 < 1 ? new(3.0f, F + .12f, z + .6f) : new(3.4f, F + .65f, z), z % 2 < 1 ? new(.2f, 1.3f, 1.6f) : new(.2f, 1.3f, 1.6f), steel);
            if (z % 2 < 1) d.transform.localRotation = Quaternion.Euler(0, 20, 88);
        }
        for (float z = 56; z <= 104; z += 6) Tube(t, new(1.6f, F + PlatformClear - .08f, z), true, 2, false);
        foreach (float z in new[] { 58f, 76f, 94f, 111f }) RedLamp(t, new(-4.72f + (z > 107 ? .72f : 0), -1.2f, z), 90);
        RedLamp(t, new(0, -1.2f, 46.15f), 0);
        foreach (float z in new[] { 74f, 86f }) { Box(t, "Dusty bench", new(-3.9f, F + .22f, z), new(.6f, .45f, 2.6f), wood); TopLoot("platform 3", new(-3.9f, F + .45f, z), .9f); }
        Sign3(t, "PLATFORM 3   OUT OF SERVICE", new(-4.76f, -1.0f, 85), 90, .35f);
        Sign3(t, "3", new(-4.76f, -1.4f, 72), 90, .5f);
        FloorLoot("platform 3", .4f, 54, 3.6f, 97, F, .9f, 3.2f, .4f);
        FloorLoot("platform 3", -3.6f, 47, 3.6f, 52.5f, F, .8f, 3f, .4f);
        FloorLoot("platform 3", -3.6f, 108, 3.6f, 113.5f, F, .9f, 3f, .4f);
        FloorLoot("dead track walkway", 8.3f, 51, 9.4f, 95, F, .7f, 4f, .4f);
        Enemy("platform 3 south", 1.8f, F, 58); Enemy("platform 3 middle", 1.8f, F, 80); Enemy("platform 3 north", 1.0f, F, 111);
        Enemy("platform 3 west strip", -2.5f, F, 76); Enemy("platform 3 south end", -2.5f, F, 49);
        Enemy("dead track walkway", 8.8f, F, 66); Enemy("dead track walkway", 8.8f, F, 90);
    }

    static void WreckV3(Transform t)
    {
        TrainWreck(t);   // three real-scale commuter cars (RailPassengerStationV3Train.cs)
        var rng = new System.Random(7); float[] sizes = { .8f, 1.1f, 1.4f, 1.8f };
        for (int i = 0; i < 90; i++)
        {
            // Every corner of a randomly rotated rock stays inside its space: clamp by the half-diagonal.
            float s = sizes[rng.Next(4)], hd = .844f * s;
            float z = 100.5f + (float)rng.NextDouble() * 9.2f;
            if (z + hd > 109.65f) continue;                                    // portal plane
            float xMin = (z + hd > 107f || z - hd < 98f) ? 4.2f + hd : 2.05f + hd, xMax = 9.57f - hd;   // platform ends / track wall
            if (xMin > xMax) continue;
            float x = xMin + (float)rng.NextDouble() * (xMax - xMin);
            float r = Mathf.Clamp01(Mathf.Pow((x - 6f) / 4.2f, 2) + Mathf.Pow((z - 106f) / 6f, 2));
            float h = 4.6f * (1 - r);
            float y = -4f + h * (.35f + .65f * (float)rng.NextDouble());
            if (z + hd > 107f) y = Mathf.Min(y, .58f - hd);                    // under the channel ceiling outside the hole
            var rock = Box(t, "Cave-in rubble", new(x, y, z), new(s, s * .8f, s * 1.1f), rubbleMat);
            rock.transform.localRotation = Quaternion.Euler(rng.Next(360), rng.Next(360), rng.Next(360));
        }
        for (int i = 0; i < 10; i++)
        {
            var rock = Box(t, "Cave-in rubble", new(4.5f + i % 3 * 1.6f, 1.2f + i * .7f, 100.5f + i % 4 * 1.4f), new(1.4f, 1.1f, 1.5f), rubbleMat);
            rock.transform.localRotation = Quaternion.Euler(i * 37, i * 53, i * 21);
        }
        // Chimney up to the surface, lit by a cold moon shaft.
        Wall(t, "Cave-in shaft wall", true, 2f, 98, 107, F + PlatformClear + .03f, 12, rubbleMat);
        Wall(t, "Cave-in shaft wall", true, 9.7f, 98, 107, .9f, 8.2f, rubbleMat);
        foreach (float z in new[] { 98f, 106.85f }) Wall(t, "Cave-in shaft wall", false, z, 2, 9.7f, F + PlatformClear + .04f, 12, rubbleMat);
        var moon = Group(t, "Baked moonlight shaft").gameObject.AddComponent<Light>();
        moon.transform.localPosition = new(5.85f, 8.2f, 102.5f); moon.transform.localRotation = Quaternion.Euler(90, 0, 0);
        moon.type = LightType.Spot; moon.spotAngle = 70; moon.lightmapBakeType = LightmapBakeType.Baked; moon.intensity = 30; moon.range = 15; moon.color = Moon; moon.shadows = LightShadows.Soft; v3Lights++;
        Sign3(t, "DANGER   KEEP OUT", new(2.1f, -1.2f, 97.9f), 180, .3f);
        foreach (float z in new[] { 100.2f }) TopLoot("cave-in", new(1.2f, F, z), 1f);
    }

    // ---------------- escalators ----------------
    static void Escalator(Transform p, string name, float x0, float zBottom, float zTop)
    {
        Zone(x0 < -10 ? zoneGreen : zoneRed);
        var t = Group(p, name); const int count = 24;   // 4 m rise over 12 m
        float dir = Mathf.Sign(zTop - zBottom), run = Mathf.Abs(zTop - zBottom);
        float step = run / count, rise = (Deck - F) / count;
        void Flight(float fx, float width, bool esc)
        {
            for (int i = 0; i < count; i++)
            {
                float h = (i + 1) * rise, zc = zBottom + dir * (i + .5f) * step;
                Box(t, esc ? "Stopped escalator step" : "Stair tread", new(fx + width / 2, F + h / 2, zc), new(width, h, step), esc ? steel : concrete);
                Box(t, "Tread edge", new(fx + width / 2, F + h + .012f, zBottom + dir * (i * step + .04f)), new(width, .024f, .08f), yellow, false);
            }
        }
        Flight(x0 + .15f, 1.6f, true);
        Flight(x0 + 2.05f, 2.3f, false);
        var bottom = new Vector3(0, F, zBottom); var top = new Vector3(0, Deck, zTop);
        foreach (var (x, w) in new[] { (x0 + .15f, .2f), (x0 + 1.9f, .3f), (x0 + 4.35f, .2f) })
        {
            Beam(t, "Escalator casing", new(x, bottom.y + .45f, bottom.z), new(x, top.y + .45f, top.z), w, .9f, x == x0 + 1.9f ? steel : tile);
            Beam(t, "Handrail", new(x, bottom.y + 1f, bottom.z), new(x, top.y + 1f, top.z), .12f, .12f, darkTile, false);
        }
        float lo = Mathf.Min(zBottom, zTop), hi = Mathf.Max(zBottom, zTop);
        Wall(t, "Shaft wall", true, x0, lo, hi, F, Deck - F + PlatformClear + .3f, tile);
        Wall(t, "Shaft wall", true, x0 + 4.5f, lo, hi, F, Deck - F + PlatformClear + .3f, tile);
        Beam(t, "Sloped shaft ceiling", new(x0 + 2.25f, F + PlatformClear + .15f, zBottom), new(x0 + 2.25f, Deck + PlatformClear + .15f, zTop), 4.5f, .3f, roof);
        var mid = new Vector3(x0 + 2.25f, (F + Deck) / 2 + PlatformClear - .1f, (zBottom + zTop) / 2);
        Tube(t, mid, true, x0 > -10 ? 2 : (zBottom < 80 ? 0 : 1), x0 < -10);
        if (x0 > -10) RedLamp(t, new(x0 + .14f, (F + Deck) / 2 + 1.2f, (zBottom + zTop) / 2), 90);
        TopLoot(x0 < -10 ? "escalator 1" : "escalator 3", new(x0 + 3.2f, F + rise * 17, zBottom + dir * 16.5f * step), x0 < -10 ? .2f : .8f);
    }

    // ---------------- concourses ----------------
    static void Concourse(Transform t, float z0, float z1, bool south)
    {
        Zone(south ? zoneBlue : zonePurple);
        float y = Deck, clear = PlatformClear;
        Slab(t, "Concourse deck", -28, z0, 32, z1 - z0, y, floorTile);
        Slab(t, "Concourse ceiling", -28, z0, 32, z1 - z0, y + clear + .3f, roof);
        float[] shafts = { WestShaft + .135f, WestShaft + 4.365f, EastShaft + .135f, EastShaft + 4.365f };   // jambs 1 cm inside the shaft walls
        var cw = south ? ceramic : tile;   // the ticket hall is tiled
        Wall(t, "Concourse wall", false, z0, -28, 4, y, clear + .3f, cw, south ? Solid : shafts);
        Wall(t, "Concourse wall", false, z1, -28, 4, y, clear + .3f, cw, south ? shafts : Solid);
        Wall(t, "Concourse wall", true, -28, z0, z1, y, clear + .3f, cw);
        Wall(t, "Concourse wall", true, 4, z0, z1, y, clear + .3f, cw);
        Stripe(t, false, z0 + .15f, -28, 4, y + 1.6f, south ? Solid : shafts);
        Stripe(t, false, z1 - .15f, -28, 4, y + 1.6f, south ? shafts : Solid);
        Stripe(t, true, -27.85f, z0, z1, y + 1.6f, Solid);
        Stripe(t, true, 3.85f, z0, z1, y + 1.6f, Solid);
        float shaftSide = south ? z1 - .16f : z0 + .16f, shaftYaw = south ? 180 : 0;
        SignBoard(t, "PLATFORM 1   DAWN LINE", new(WestShaft + 2.25f, y + 2.02f, shaftSide), shaftYaw, .22f, 'd', y + clear);
        SignBoard(t, "PLATFORM 3   CLOSED", new(EastShaft + 2.25f, y + 2.02f, shaftSide), shaftYaw, .22f, 'd', y + clear);
        float mz = (z0 + z1) / 2;
        // Ticket gate line across the concourse (over the live track).
        for (int i = 0; i < 8; i++)
        {
            float z = z0 + 1.2f + i * 1.5f;
            var gate = Box(t, "Ticket gate", new(-12, y + .5f, z), new(1.3f, 1f, .35f), steel);
            bool toppled = !south && (i == 2 || i == 5);
            if (toppled) { gate.transform.localPosition = new(-11.2f, y + .18f, z); gate.transform.localRotation = Quaternion.Euler(0, 30, 90); }
            else GlowBox(t, "Gate indicator", new(-12.66f, y + .95f, z), new(.03f, .1f, .2f), south ? glowScreen : glowRed);
        }
        SignBoard(t, south ? "TICKET GATES" : "GATES CLOSED", new(-12, y + 1.95f, z0 + .16f), 0, .26f);
        if (south)
        {
            for (int i = 0; i < 6; i++)
            {
                float x = -27 + i * 1.1f + .5f;
                Box(t, "Fare machine", new(x, y + .85f, z0 + .45f), new(1f, 1.7f, .6f), steel);
                GlowBox(t, "Fare machine screen", new(x, y + 1.2f, z0 + .77f), new(.7f, .45f, .03f), glowScreen);
                TopLoot("ticket concourse", new(x, y + 1.7f, z0 + .45f), .3f);
            }
            // Station office in the south-east corner.
            Wall(t, "Office wall", true, -3, z0, z0 + 6.5f, y, clear + .3f, tile, z0 + 3, z0 + 5);
            Wall(t, "Office wall", false, z0 + 6.5f, -3, 4, y, clear + .3f, tile);
            Box(t, "Office carpet", new(.5f, y + .006f, z0 + 3.25f), new(6.8f, .008f, 6.3f), carpet, false);
            Box(t, "Office wood panelling", new(-2.86f, y + .6f, z0 + 1.5f), new(.02f, 1.2f, 2.8f), wood, false);
            Box(t, "Office wood panelling", new(.5f, y + .6f, z0 + 6.36f), new(6.7f, 1.2f, .02f), wood, false);
            Box(t, "Office desk", new(1.5f, y + .38f, z0 + 2.2f), new(2.4f, .76f, 1f), wood);
            foreach (float mx in new[] { .9f, 2.1f }) { Box(t, "CRT monitor", new(mx, y + .98f, z0 + 2.45f), new(.45f, .4f, .4f), tile); GlowBox(t, "CRT screen", new(mx, y + .99f, z0 + 2.24f), new(.34f, .26f, .02f), glowScreen); }
            Box(t, "Filing cabinet", new(-2.5f, y + .65f, z0 + 5.6f), new(.5f, 1.3f, .6f), steel);
            for (int i = 0; i < 6; i++) GlowBox(t, i % 3 == 1 ? "CCTV screen (dead)" : "CCTV screen", new(1.0f + (i % 3) * .75f, y + 1.55f + (i / 3) * .5f, z0 + 6.34f), new(.6f, .4f, .03f), i % 3 == 1 ? glowRed : glowScreen);
            Box(t, "Office lockers", new(3.55f, y + .9f, z0 + 4.5f), new(.6f, 1.8f, 2.6f), steel);
            TopLoot("station office", new(1.0f, y + .76f, z0 + 2.2f), 1f); TopLoot("station office", new(2.1f, y + .76f, z0 + 2.2f), 1f);
            FloorLoot("station office", -2.6f, z0 + .3f, 3.3f, z0 + 6.2f, y, 1f, 2.2f, .5f);
            GlowBox(t, "Departure board", new(-12, y + 1.75f, z1 - .25f), new(4.5f, .55f, .06f), glowScreen);
            Sign3(t, "NEXT  DAWN LINE   --:--", new(-12, y + 1.75f, z1 - .32f), 180, .32f);
            Tube(t, new(-21, y + clear - .08f, mz), false, 0); Tube(t, new(-12, y + clear - .08f, mz), false, 1); Tube(t, new(-4, y + clear - .08f, mz), false, 0, false);
            PL(t, "Baked office light", new(.5f, y + 2f, z0 + 3.2f), Warm, 8, 6);
            FloorLoot("ticket concourse", -26.5f, z0 + 2.2f, -13.2f, z1 - .6f, y, .25f, 3.2f, .5f);
            FloorLoot("ticket concourse", -10.8f, z0 + 7.2f, -1f, z1 - .6f, y, .35f, 3.2f, .5f);
            Enemy("ticket concourse west", -24, y, mz); Enemy("ticket concourse east", -7, y, mz); Enemy("station office", .5f, y, z0 + 3.3f);
        }
        else
        {
            for (int i = 0; i < 4; i++)
            {
                float x = -26 + i * 5f;
                var sh = Group(t, "Rolled-down shop shutter");
                Box(sh, "Shutter", new(x + 2.2f, y + 1.15f, z1 - .2f), new(4.4f, 2.3f, .12f), steel, false);
                for (int r = 0; r < 7; r++) Box(sh, "Shutter rib", new(x + 2.2f, y + .2f + r * .32f, z1 - .27f), new(4.4f, .05f, .03f), darkTile, false);
            }
            Box(t, "Kiosk counter", new(-6.5f, y + .5f, z0 + 6), new(4.5f, 1f, 1f), wood);
            Box(t, "Kiosk shelf", new(-6.5f, y + .7f, z0 + 7.9f), new(4.5f, 1.4f, .5f), steel);
            foreach (float x in new[] { -8f, -6.5f, -5f }) { TopLoot("shuttered concourse", new(x, y + 1f, z0 + 6), .8f); TopLoot("shuttered concourse", new(x, y + 1.4f, z0 + 7.9f), .8f); }
            Box(t, "Concourse bench", new(-24, y + .22f, z0 + 7), new(3f, .45f, .6f), wood);
            TopLoot("shuttered concourse", new(-24.8f, y + .45f, z0 + 7), .6f); TopLoot("shuttered concourse", new(-23.2f, y + .45f, z0 + 7), .6f);
            Tube(t, new(-21, y + clear - .08f, mz), false, 2); Tube(t, new(-12, y + clear - .08f, mz), false, 1); Tube(t, new(-6, y + clear - .08f, mz), false, 2, false);
            RedLamp(t, new(-27.86f, y + 1.9f, mz), 90);
            FloorLoot("shuttered concourse", -26.5f, z0 + 1.5f, -13.2f, z1 - 1.5f, y, .6f, 3.2f, .5f);
            FloorLoot("shuttered concourse", -10.8f, z0 + 1.5f, 3f, z1 - 1.5f, y, .7f, 3.2f, .5f);
            Enemy("shuttered concourse west", -24, y, mz); Enemy("shuttered concourse east", 1, y, mz);
        }
    }

    // West rooms, east rooms, facades and clutter live in RailPassengerStationV3Rooms.cs.
}
#endif
