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
    const float Deck = 3f;               // concourse floor level (crawl graph cells sit at n+0.5, so floors avoid n+0.5)
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

    static void V3Materials()
    {
        concrete = Mat("Worn station concrete", "concrete", new(.7f, .7f, .65f));
        paint = Mat("Faded station green", "weathered paint", new(.52f, .63f, .55f));
        steel = Mat("Oxidized station metal", "weathered paint", new(.36f, .41f, .42f));
        roof = Mat("Dark station roof", "dark roofing", new(.4f, .43f, .44f));
        wood = Mat("Abandoned timber", "timber", new(.57f, .43f, .3f));
        yellow = Mat("Faded safety yellow", "weathered paint", new(.82f, .65f, .25f));
        gravel = Mat("Tunnel rock and ballast", "gravel", new(.5f, .5f, .45f));
        grass = Mat("Outer ground", "grass", new(.48f, .55f, .4f));
        tile = Mat("Station cream tile", "concrete", new(.86f, .83f, .74f));
        band = Mat("Line colour band", "weathered paint", new(.24f, .6f, .4f));
        floorTile = Mat("Platform floor tile", "concrete", new(.56f, .57f, .58f));
        darkTile = Mat("Abandoned grime tile", "concrete", new(.42f, .43f, .38f));
        wreckRed = Mat("Wreck carriage red", "weathered paint", new(.62f, .2f, .17f));
        rubbleMat = Mat("Cave-in rubble", "gravel", new(.46f, .41f, .35f));
        deadTube = Mat("Dead tube", "weathered paint", new(.3f, .32f, .33f));
        glowTube = Glow("Glow tube steady", new(.82f, .9f, 1f), 2.2f, 0);
        glowFlicker = Glow("Glow tube flickering", new(.82f, .9f, 1f), 2.2f, .55f);
        glowRed = Glow("Glow emergency red", new(1f, .12f, .08f), 2.4f, 0);
        glowAmber = Glow("Glow wreck window amber", new(1f, .6f, .28f), 1.2f, .12f);
        glowScreen = Glow("Glow machine screen", new(.45f, .85f, .75f), 1.4f, .2f);
        glowWindow = Glow("Glow cable gallery window", new(1f, .78f, .45f), 1.6f, 0);
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
        if (light && state != 2) PL(p, "Baked tube light", pos - Vector3.up * .2f, Cool, state == 1 ? 7 : 16, range);
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
        return t;
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
        Sign(p, text, pos, yaw);
        p.GetChild(p.childCount - 1).localScale = Vector3.one * scale;
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
        Escalator(esc, "South escalators to Platform 1", WestShaft, 71f, 53f);
        Escalator(esc, "North escalators to Platform 1", WestShaft, 89f, 107f);
        Escalator(esc, "South escalators to Platform 3", EastShaft, 71f, 53f);
        Escalator(esc, "North escalators to Platform 3", EastShaft, 89f, 107f);
        Concourse(Group(root, "Ticket concourse (south)"), 40, 53, true);
        Concourse(Group(root, "Shuttered concourse (north)"), 107, 120, false);
        WestRooms(Group(root, "West rooms and transfer hall"));
        EastRooms(Group(root, "East maintenance side"));
    }

    // ---------------- tunnels ----------------
    static void Tunnels(Transform t)
    {
        float[] fans = { 18, 28, 38 };
        for (int z = 6; z < 154; z++)
        {
            if (z >= 53 && z < 107) continue;
            var a = new Vector3(RailX(z), RailHeight(z), z); var c = new Vector3(RailX(z + 1), RailHeight(z + 1), z + 1);
            bool crossing = z >= 40 && z < 120;    // under a concourse deck
            foreach (float side in new[] { -1f, 1f })
            {
                bool fanGap = side > 0 && fans.Any(f => Mathf.Abs(z + .5f - f) < 2.5f);
                if (!fanGap)
                {
                    if (crossing)
                    {
                        float bottom = a.y - .5f, top = Deck - .3f;
                        Beam(t, "Narrow tunnel wall", new Vector3(a.x + side * 7, (bottom + top) / 2, a.z), new Vector3(c.x + side * 7, (bottom + top) / 2, c.z), .4f, top - bottom, tile);
                    }
                    else Beam(t, "Narrow tunnel wall", a + new Vector3(side * 7, 3, 0), c + new Vector3(side * 7, 3, 0), .4f, 7, tile);
                }
                else
                {
                    // Recessed ventilation fan bay behind the walkway.
                    Beam(t, "Fan bay back wall", a + new Vector3(8.6f, 3, 0), c + new Vector3(8.6f, 3, 0), .4f, 7, darkTile);
                    Beam(t, "Fan bay floor", a + new Vector3(7.8f, .85f, 0), c + new Vector3(7.8f, .85f, 0), 1.6f, .3f, concrete);
                    Beam(t, "Fan bay ceiling", a + new Vector3(7.8f, 5.6f, 0), c + new Vector3(7.8f, 5.6f, 0), 1.6f, .3f, roof);
                }
                Beam(t, "Tunnel walkway", a + new Vector3(side * 5.7f, .85f, 0), c + new Vector3(side * 5.7f, .85f, 0), 2.5f, .3f, concrete);
                Beam(t, "Low walkway ceiling", a + new Vector3(side * 5.7f, 3.28f, 0), c + new Vector3(side * 5.7f, 3.28f, 0), 2.5f, .3f, roof);
            }
            if (!crossing) Beam(t, "Rail channel roof", a + Vector3.up * 6.6f, c + Vector3.up * 6.6f, 9.2f, .4f, roof);
            if (z % 12 == 0) Tube(t, (a + c) / 2 + Vector3.up * (crossing ? 5.4f : 5.8f), true, z % 36 == 0 ? 1 : 0, true, 14);
            // Lit cable-gallery windows staggered along the outside of the departure curve.
            if (z >= 124 && z < 147 && z % 5 == 0)
            {
                var g = GlowBox(t, "Cable gallery window (lit)", (a + c) / 2 + new Vector3(-6.78f, 2.4f, 0), new(.05f, .9f, 1.5f), glowWindow);
                g.transform.localRotation = Quaternion.LookRotation(c - a);
                Box(t, "Window frame", (a + c) / 2 + new Vector3(-6.76f, 2.4f, 0), new(.04f, 1.1f, 1.7f), steel, false).transform.localRotation = Quaternion.LookRotation(c - a);
            }
        }
        // Fan bay end caps and the three stopped fans facing the track.
        foreach (float f in fans)
        {
            foreach (float zz in new[] { f - 2.5f, f + 2.5f })
            {
                var e = new Vector3(RailX(zz), RailHeight(zz), zz);
                Box(t, "Fan bay end wall", e + new Vector3(7.8f, 3, 0), new(1.8f, 7, .4f), darkTile);
            }
            var fc = new Vector3(RailX(f) + 8.25f, RailHeight(f) + 3.2f, f);
            var tangent = new Vector3(RailX(f + .5f) - RailX(f - .5f), 0, 1).normalized;
            var fan = Group(t, "Stopped ventilation fan"); fan.localPosition = fc; fan.localRotation = Quaternion.LookRotation(Vector3.Cross(Vector3.up, tangent) * -1, Vector3.up);
            Box(fan, "Fan shroud", Vector3.zero, new(4.2f, 4.2f, .3f), steel, false);
            Box(fan, "Fan hub", new(0, 0, -.25f), new(.7f, .7f, .3f), darkTile, false);
            for (int i = 0; i < 4; i++)
            {
                var blade = Box(fan, "Fan blade", new(0, 0, -.2f), new(.5f, 3.6f, .06f), concrete, false);
                blade.transform.localRotation = Quaternion.Euler(0, 0, i * 45 + f);
            }
            Box(fan, "Fan guard bar", new(0, 0, -.42f), new(4f, .08f, .06f), steel, false);
            Box(fan, "Fan guard bar", new(0, 0, -.42f), new(.08f, 4f, .06f), steel, false);
        }
    }

    static void Envelope(Transform t)
    {
        foreach (float z in new[] { 0f, 154f }) Slab(t, "Flat seam", -48, z, 96, 6, 0, grass);
        foreach (float x in new[] { -48f, 44f }) Slab(t, "Flat side boundary", x, 6, 4, 148, 0, grass);
        // RailRoute places the existing midpoint shop at (-16,0,148) on any selected middle block.
        Slab(t, "Reserved midpoint shop pad", -44, 140, 38, 14, 0, concrete);
        foreach (float x in new[] { -44f, 43.75f }) Box(t, "Buried outer retaining wall", new(x, -1, 80), new(.5f, 20, 148), gravel);
        // Overburden with the cave-in hole open to the sky.
        Slab(t, "Overburden", -44, 6, 87.75f, 92, 9, gravel);
        Slab(t, "Overburden", -44, 107, 87.75f, 47, 9, gravel);
        Slab(t, "Overburden", -44, 98, 46, 9, 9, gravel);
        Slab(t, "Overburden", 9.7f, 98, 34.05f, 9, 9, gravel);
        Slab(t, "Buried foundation", -44, 6, 87.75f, 148, -11, gravel);
        foreach (float z in new[] { 6f, 154f }) { Wall(t, "Buried end wall", false, z, -44, -7.3f, -11, 20, gravel); Wall(t, "Buried end wall", false, z, 7.3f, 43.75f, -11, 20, gravel); }
    }

    // ---------------- Platform 1 ----------------
    static void Platform1(Transform t)
    {
        Slab(t, "Platform floor", -25, 53, 10.1f, 54, F, floorTile);
        Box(t, "Platform edge face", new(-15f, -3.5f, 80), new(.2f, 1, 54), concrete);
        // Ceiling with holes over the two escalator shafts.
        Slab(t, "Platform ceiling", -25, 53, WestShaft + 25, 54, F + PlatformClear + .3f, roof);
        // Near both platform ends a 14 m carriage still swings out of the curve, so the
        // channel wall and ceiling edge step back to X -16.2 there (train sweep check).
        Slab(t, "Platform ceiling", WestShaft + 4.5f, 61, -15 - (WestShaft + 4.5f), 38, F + PlatformClear + .3f, roof);
        foreach (float z in new[] { 53f, 99f }) Slab(t, "Platform ceiling", WestShaft + 4.5f, z, -16.2f - (WestShaft + 4.5f), 8, F + PlatformClear + .3f, roof);
        Slab(t, "Platform ceiling", WestShaft, 71f, 4.5f, 18, F + PlatformClear + .3f, roof);
        // Live track channel: upper walls and roof stay outside the train sweep (rail X -12 +/- 2.7).
        Wall(t, "Rail channel upper wall", true, -15f, 61, 99, F + PlatformClear, 3.4f, tile);
        foreach (float z in new[] { 53f, 99f }) Wall(t, "Rail channel upper wall", true, -16.2f, z, z + 8, F + PlatformClear, 3.4f, tile);
        foreach (float z in new[] { 61f, 99f }) Wall(t, "Rail channel upper wall", false, z, -16.2f, -15f, F + PlatformClear, 3.4f, tile);
        Wall(t, "Rail channel upper wall", true, -9f, 53, 107, F + PlatformClear, 3.4f, tile);
        Slab(t, "Rail channel roof", -16.3f, 53, 7.4f, 54, 2.8f, roof);
        // Shopfront wall and ends. Tunnel walkways enter at both ends.
        Wall(t, "Shopfront wall", true, -25, 53, 110, F, PlatformClear + .3f, tile, 57, 59, 66, 72, 77, 81, 89, 97, 104, 106);
        BandAlongZ(t, -24.86f, 53, 107, -1.75f, 57, 59, 66, 72, 77, 81, 89, 97, 104, 106);
        foreach (float z in new[] { 53f, 107f }) Wall(t, "Platform end wall", false, z, -25, -15, F, PlatformClear + .3f, tile, -19.2f, -15f);
        Box(t, "Tactile strip", new(-15.85f, F + .012f, 80), new(.3f, .024f, 54), yellow, false);
        Box(t, "Platform edge line", new(-15.12f, F + .012f, 80), new(.12f, .024f, 54), yellow, false);
        // Platform screen door units, stuck open with wide gaps for boarding.
        for (float z = 63.5f; z < 99; z += 7)
        {
            Box(t, "Platform door unit", new(-15.4f, F + .65f, z), new(.2f, 1.3f, 1.6f), steel);
            Box(t, "Platform door top light (dead)", new(-15.4f, F + 1.33f, z), new(.22f, .05f, 1.2f), deadTube, false);
        }
        foreach (float z in new[] { 73.5f, 86.5f })
        {
            Box(t, "Platform column", new(-20.95f, F + 1.2f, z), new(.6f, 2.4f, .6f), tile);
            Box(t, "Column band", new(-20.95f, -1.75f, z), new(.64f, .28f, .64f), band, false);
            Sign3(t, "1  DAWN LINE", new(-20.95f, -1.1f, z + .33f), 0, .3f);
            Sign3(t, "1  DAWN LINE", new(-20.95f, -1.1f, z - .33f), 180, .3f);
        }
        foreach (var (x, z) in new[] { (-22.6f, 80f), (-17.9f, 80f) })
        {
            Box(t, "Platform bench", new(x, F + .22f, z), new(.6f, .45f, 3f), wood);
            TopLoot("platform 1", new(x, F + .45f, z - .9f), .15f); TopLoot("platform 1", new(x, F + .45f, z + .9f), .15f);
        }
        Sign3(t, "KAMOBASHI", new(-5.1f, -1.35f, 66), -90, .45f);
        Sign3(t, "KAMOBASHI", new(-5.1f, -1.35f, 94), -90, .45f);
        Sign3(t, "EXIT  /  TICKET GATES", new(-20.95f, -.9f, 70), 0, .3f);
        Sign3(t, "EXIT  /  TICKET GATES", new(-20.95f, -.9f, 90), 180, .3f);
        int k = 0;
        for (float z = 56; z <= 104; z += 6, k++)
        {
            int state = k % 4 == 3 ? 2 : k % 5 == 2 ? 1 : 0;
            Tube(t, new(-16.9f, F + PlatformClear - .08f, z), true, state, k % 2 == 0 || state == 1);
        }
        foreach (float z in new[] { 74f, 80f, 86f }) Tube(t, new(-23.9f, F + PlatformClear - .08f, z), true, z == 80 ? 1 : 0, z != 74);
        Enemy("platform 1 south", -16.6f, F, 72); Enemy("platform 1 north", -16.6f, F, 88); Enemy("platform 1 middle", -23.6f, F, 80);

        // East service ledge beside the train (no side of the train is a dead end).
        Slab(t, "Service ledge floor", -9f, 53, 4.1f, 54, F, concrete);
        Box(t, "Service ledge edge face", new(-9f, -3.5f, 80), new(.2f, 1, 54), concrete);
        Slab(t, "Service ledge ceiling", -9f, 53, 4.1f, 54, F + PlatformClear + .3f, roof);
        Wall(t, "Ledge back wall", true, -4.9f, 53, 107, F, 5.8f, tile, 79, 81.5f);
        Box(t, "Ledge edge line", new(-8.82f, F + .012f, 80), new(.12f, .024f, 54), yellow, false);
        Tube(t, new(-6.9f, F + PlatformClear - .08f, 62), true, 0); Tube(t, new(-6.9f, F + PlatformClear - .08f, 80), true, 1); Tube(t, new(-6.9f, F + PlatformClear - .08f, 98), true, 2);
        Sign3(t, "STAFF ONLY", new(-5.06f, -1.5f, 77.6f), -90, .3f);
        FloorLoot("service ledge", -8.6f, 54, -5.3f, 106, F, .35f, 4.4f, .5f);
        Enemy("service ledge", -7f, F, 96);
    }

    // ---------------- Platform 3 and dead track ----------------
    static void Platform3(Transform t)
    {
        Slab(t, "Platform 3 floor", -4.75f, 53, 8.75f, 54, F, darkTile);
        // Ends run under the curving tunnel wall so no gap opens beside it.
        Slab(t, "Platform 3 south end", -5.2f, 46, 9.2f, 7, F, darkTile);
        Slab(t, "Platform 3 north end", -5.2f, 107, 9.2f, 7, F, darkTile);
        Box(t, "Platform 3 edge face", new(4f, -3.5f, 80), new(.2f, 1, 60), concrete);
        float ceil = F + PlatformClear + .3f;
        Slab(t, "Platform 3 ceiling", EastShaft + 4.5f, 53, 4 - (EastShaft + 4.5f), 45, ceil, roof);   // to Z 98
        Slab(t, "Platform 3 ceiling", EastShaft + 4.5f, 98, 2 - (EastShaft + 4.5f), 9, ceil, roof);     // beside the cave-in
        Slab(t, "Platform 3 ceiling", -4.75f, 71f, 4.65f, 18, ceil, roof);
        Slab(t, "Platform 3 ceiling", -4f, 46, 8, 7, ceil, roof);
        Slab(t, "Platform 3 ceiling", -4f, 107, 8, 7, ceil, roof);
        Wall(t, "Platform 3 south wall", false, 46, -4, 13, F, PlatformClear + .3f, darkTile);
        Wall(t, "Platform 3 north wall", false, 114, -4, 13, F, PlatformClear + .3f, darkTile);
        Box(t, "Platform 3 edge line", new(3.75f, F + .012f, 80), new(.12f, .024f, 66), yellow, false);
        // Dead track: bed, non-catalogue rails, east service walkway, high channel ceiling.
        Slab(t, "Dead track bed", 3.75f, 50, 5.95f, 60, -4f, gravel);
        foreach (float x in new[] { 5.2f, 6.8f }) Box(t, "Dead track rail", new(x, -3.92f, 80), new(.1f, .12f, 60), steel, false);
        for (float z = 51; z < 110; z += 2) Box(t, "Dead track tie", new(6f, -3.98f, z), new(2.4f, .08f, .25f), wood, false);
        Box(t, "Buffer stop", new(6f, -3.4f, 50.5f), new(2.6f, 1.2f, .6f), steel);
        Slab(t, "Dead track east walkway", 8f, 50, 1.7f, 60, F, concrete);
        Box(t, "Walkway edge face", new(8f, -3.5f, 80), new(.2f, 1, 60), concrete);
        Box(t, "Dead track wall base", new(9.7f, -3.65f, 80), new(.25f, 1.3f, 60), darkTile);
        Wall(t, "Dead track wall", true, 9.7f, 50, 110, F, 3.9f, darkTile, 81.75f, 83.75f);
        Wall(t, "Dead track channel upper wall", true, 4f, 50, 98, F + PlatformClear, 1.5f, darkTile);
        Wall(t, "Dead track channel upper wall", true, 4f, 107, 110, F + PlatformClear, 1.5f, darkTile);
        Slab(t, "Dead track channel ceiling", 3.75f, 50, 6.1f, 48, .9f, roof);
        Slab(t, "Dead track channel ceiling", 3.75f, 107, 6.1f, 3, .9f, roof);
        foreach (float z in new[] { 50f, 110f }) Wall(t, "Dead track end wall", false, z, 3.75f, 9.7f, F + PlatformClear, 1.5f, darkTile);
        // Abandoned furniture, toppled screen doors, dead tubes, red emergency lamps.
        for (float z = 56.5f; z < 106; z += 7)
        {
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
        const float x0 = 4.4f, x1 = 7.6f, cx = 6f;
        float[] starts = { 58f, 73f, 88f };
        for (int i = 0; i < 3; i++)
        {
            float z = starts[i]; bool front = i == 2; var car = Group(t, "Crashed carriage " + (3 - i));
            Slab(car, "Carriage floor", x0, z, x1 - x0, 14, F, wood);
            Box(car, "Carriage underframe", new(cx, -3.65f, z + 7), new(2.6f, .7f, 13.6f), steel);
            float[] doors = { z + 3.2f, z + 4.8f, z + 9.2f, z + 10.8f };
            Wall(car, "Carriage body", true, x0, z, z + 14, F, 2.75f, wreckRed, doors);
            Wall(car, "Carriage body", true, x1, z, z + 14, F, 2.75f, wreckRed, doors);
            Wall(car, "Carriage end", false, z, x0, x1, F, 2.75f, wreckRed, cx - .6f, cx + .6f);
            Wall(car, "Carriage end", false, z + 14, x0, x1, F, 2.75f, wreckRed, front ? Solid : new[] { cx - .6f, cx + .6f });
            Slab(car, "Carriage roof", x0 - .1f, z, 3.4f, front ? 9 : 14, .05f, steel);
            foreach (var (a, l) in new[] { (z + .4f, 2.6f), (z + 5.1f, 3.8f), (z + 11.1f, 2.5f) })
                foreach (float x in new[] { x0 + .45f, x1 - .45f })
                {
                    if (front && a > z + 10) continue;
                    Box(car, "Bench seat", new(x, F + .22f, a + l / 2), new(.6f, .45f, l), wood);
                    TopLoot("wreck", new(x, F + .45f, a + l / 2), 1f);
                }
            foreach (var (a, l) in new[] { (z + 1.6f, 2f), (z + 7f, 2f), (z + 12.2f, 1.6f) })
                foreach (float x in new[] { x0 - .14f, x1 + .14f })
                    if (front) Box(car, "Broken window", new(x, -1.6f, a), new(.03f, .7f, l), darkTile, false);
                    else GlowBox(car, "Lit window", new(x, -1.6f, a), new(.03f, .7f, l), glowAmber);
            if (!front) PL(car, "Baked carriage light", new(cx, -.7f, z + 7), Amber, 6, 6);
            FloorLoot("wreck", cx, z + 1, cx, front ? z + 8.5f : z + 13, F, 1f, 3.6f, 0);
            Enemy("wreck car " + (3 - i), cx, F, z + 7);
        }
        foreach (float z in new[] { 72f, 87f }) Slab(t, "Gangway plate", cx - .7f, z, 1.4f, 1, F, steel);
        // Crushed front roof and the cave-in mound.
        Box(t, "Crushed roof", new(cx, -1.1f, 99.5f), new(3.4f, .3f, 5.4f), steel).transform.localRotation = Quaternion.Euler(-14, 0, 6);
        var rng = new System.Random(7); float[] sizes = { .8f, 1.1f, 1.4f, 1.8f };
        for (int i = 0; i < 90; i++)
        {
            float s = sizes[rng.Next(4)];
            float x = 2.3f + s * .55f + (float)rng.NextDouble() * (9.5f - 2.3f - s * 1.1f);
            float z = 96f + s * .5f + (float)rng.NextDouble() * (109.4f - 96f - s);
            if (z < 99 && x < 4.2f) continue;              // keep the platform strip beside the escalators clear
            float r = Mathf.Clamp01(Mathf.Pow((x - 6f) / 4.2f, 2) + Mathf.Pow((z - 103f) / 7f, 2));
            float h = 4.6f * (1 - r);
            float y = -4f + h * (.35f + .65f * (float)rng.NextDouble());
            var rock = Box(t, "Cave-in rubble", new(x, y, z), new(s, s * .8f, s * 1.1f), rubbleMat);
            rock.transform.localRotation = Quaternion.Euler(rng.Next(360), rng.Next(360), rng.Next(360));
        }
        for (int i = 0; i < 10; i++)
        {
            var rock = Box(t, "Cave-in rubble", new(4.5f + i % 3 * 1.6f, 1.2f + i * .7f, 100.5f + i % 4 * 1.4f), new(1.4f, 1.1f, 1.5f), rubbleMat);
            rock.transform.localRotation = Quaternion.Euler(i * 37, i * 53, i * 21);
        }
        // Chimney up to the surface, lit by a cold moon shaft.
        Wall(t, "Cave-in shaft wall", true, 2f, 98, 107, F + PlatformClear, 12, rubbleMat);
        Wall(t, "Cave-in shaft wall", true, 9.7f, 98, 107, .9f, 8.2f, rubbleMat);
        foreach (float z in new[] { 98f, 107f }) Wall(t, "Cave-in shaft wall", false, z, 2, 9.7f, F + PlatformClear, 12, rubbleMat);
        var moon = Group(t, "Baked moonlight shaft").gameObject.AddComponent<Light>();
        moon.transform.localPosition = new(5.85f, 8.2f, 102.5f); moon.transform.localRotation = Quaternion.Euler(90, 0, 0);
        moon.type = LightType.Spot; moon.spotAngle = 70; moon.lightmapBakeType = LightmapBakeType.Baked; moon.intensity = 30; moon.range = 15; moon.color = Moon; moon.shadows = LightShadows.Soft; v3Lights++;
        Sign3(t, "DANGER   KEEP OUT", new(2.1f, -1.2f, 97.9f), 180, .3f);
        foreach (float z in new[] { 97.2f }) TopLoot("cave-in", new(1.2f, F, z), 1f);
    }

    // ---------------- escalators ----------------
    static void Escalator(Transform p, string name, float x0, float zBottom, float zTop)
    {
        var t = Group(p, name); const int count = 34; float dir = Mathf.Sign(zTop - zBottom), run = Mathf.Abs(zTop - zBottom);
        float step = run / count, rise = (Deck - F) / count;
        void Flight(float fx, float width, bool esc)
        {
            for (int i = 0; i < count; i++)
            {
                float h = (i + 1) * rise, zc = zBottom + dir * (i + .5f) * step;
                Box(t, esc ? "Stopped escalator step" : "Stair tread", new(fx + width / 2, F + h / 2, zc), new(width, h, step + .008f), esc ? steel : concrete);
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
        float y = Deck, clear = PlatformClear;
        Slab(t, "Concourse deck", -28, z0, 32, z1 - z0, y, floorTile);
        Slab(t, "Concourse ceiling", -28, z0, 32, z1 - z0, y + clear + .3f, roof);
        float[] shafts = { WestShaft + .125f, WestShaft + 4.375f, EastShaft + .125f, EastShaft + 4.375f };
        Wall(t, "Concourse wall", false, z0, -28, 4, y, clear + .3f, tile, south ? Solid : shafts);
        Wall(t, "Concourse wall", false, z1, -28, 4, y, clear + .3f, tile, south ? shafts : Solid);
        Wall(t, "Concourse wall", true, -28, z0, z1, y, clear + .3f, tile);
        Wall(t, "Concourse wall", true, 4, z0, z1, y, clear + .3f, tile);
        float shaftSide = south ? z1 - .16f : z0 + .16f, shaftYaw = south ? 180 : 0;
        Sign3(t, "PLATFORM 1   DAWN LINE", new(WestShaft + 2.25f, y + 2.15f, shaftSide), shaftYaw, .32f);
        Sign3(t, "PLATFORM 3   CLOSED", new(EastShaft + 2.25f, y + 2.15f, shaftSide), shaftYaw, .32f);
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
        Sign3(t, south ? "TICKET GATES" : "GATES CLOSED", new(-12, y + 2.1f, z0 + .16f), 0, .32f);
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
            Box(t, "Office desk", new(1.5f, y + .38f, z0 + 2.2f), new(2.4f, .76f, 1f), wood);
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

    // ---------------- west rooms ----------------
    static void WestRooms(Transform t)
    {
        float pc = PlatformClear;
        RoomV3(t, "Long transfer hall", -42, 18, -36, 146, F, HallClear, tile, darkTile, Solid, new[] { 24f, 27, 43, 46, 99, 101, 106, 108, 136, 139 }, Solid, Solid);
        Box(t, "Stopped moving walkway", new(-40.2f, F + .01f, 82), new(1.2f, .02f, 104), darkTile, false);
        foreach (float x in new[] { -40.85f, -39.55f }) Box(t, "Walkway edge", new(x, F + .015f, 82), new(.08f, .03f, 104), yellow, false);
        BandAlongZ(t, -41.86f, 18, 146, -1.75f);
        int k = 0;
        for (float z = 24; z <= 140; z += 8, k++) Tube(t, new(-38.6f, F + HallClear - .08f, z), true, k % 3 == 2 ? 2 : k % 4 == 1 ? 1 : 0, k % 2 == 0 && k % 3 != 2);
        FloorLoot("transfer hall", -38.8f, 20, -38.8f, 144, F, .3f, 9f, 0);
        foreach (float z in new[] { 30f, 60f, 100f, 132f }) Enemy("transfer hall", -38.8f, F, z);
        Sign3(t, "TRANSFER   ->   EXIT A2", new(-41.86f, -1.2f, 50), 90, .3f);

        var portal = RoomV3(t, "Tiled service portal", -36, 18, -16, 34, F, HallClear, tile, floorTile, null, Solid, Solid, Solid);
        // Duck mural on the south wall.
        var mural = Group(portal, "Duck mural"); mural.localPosition = new(-26, -1.6f, 18.14f);
        Box(mural, "Mural panel", Vector3.zero, new(6f, 1.9f, .03f), band, false);
        foreach (var (x, y2, w, h) in new[] { (0f, -.25f, 2.4f, .9f), (.9f, .35f, .8f, .8f), (1.45f, .3f, .5f, .2f), (-1.2f, -.05f, .6f, .4f) })
            Box(mural, "Mural duck", new(x, y2, .025f), new(w, h, .02f), yellow, false);
        Box(portal, "Rolled-down street shutter", new(-16.15f, F + 1.15f, 26), new(.12f, 2.3f, 4.5f), steel);
        Sign3(portal, "EXIT A2   CLOSED", new(-16.25f, -1.0f, 26), -90, .32f);
        Box(portal, "Ticket booth", new(-31, F + .55f, 31.5f), new(4f, 1.1f, 1.2f), wood);
        foreach (float z in new[] { 22f, 27f }) { Box(portal, "Waiting bench", new(-21, F + .22f, z), new(3f, .45f, .6f), wood); TopLoot("service portal", new(-21.8f, F + .45f, z), .3f); TopLoot("service portal", new(-20.2f, F + .45f, z), .3f); }
        TopLoot("service portal", new(-32, F + 1.1f, 31.5f), .4f); TopLoot("service portal", new(-30, F + 1.1f, 31.5f), .4f);
        Tube(portal, new(-28, F + HallClear - .08f, 26), false, 0); Tube(portal, new(-20, F + HallClear - .08f, 26), false, 1);
        FloorLoot("service portal", -35.5f, 18.5f, -16.5f, 33.5f, F, .3f, 3f, .6f);
        Enemy("service portal", -26, F, 25); Enemy("service portal east", -18.5f, F, 31);

        var lost = RoomV3(t, "Lost and found", -36, 36, -29, 52, F, HallClear, tile, darkTile, null, Solid, Solid, Solid);
        for (int i = 0; i < 3; i++) { Box(lost, "Lost property shelf", new(-29.6f, F + .6f, 39 + i * 4.5f), new(.7f, 1.2f, 3f), steel); TopLoot("lost and found", new(-29.6f, F + 1.2f, 38.2f + i * 4.5f), .7f); TopLoot("lost and found", new(-29.6f, F + 1.2f, 39.8f + i * 4.5f), .7f); }
        Tube(lost, new(-32.5f, F + HallClear - .08f, 44), true, 0);
        FloorLoot("lost and found", -35.5f, 36.5f, -30.5f, 51.5f, F, .6f, 2.4f, .5f);
        Enemy("lost and found", -33, F, 47);

        // Shop band behind Platform 1 (east walls are the platform shopfront).
        var toilets = RoomV3(t, "Toilets", -36, 53, -25, 64, F, pc, tile, floorTile, null, null, Solid, Solid);
        for (int i = 0; i < 3; i++) { Wall(toilets, "Stall partition", false, 56.5f + i * 2.2f, -36, -33.5f, F, 2f, paint); }
        Box(toilets, "Sink counter", new(-28.5f, F + .45f, 53.45f), new(5f, .9f, .6f), concrete);
        TopLoot("toilets", new(-29.5f, F + .9f, 53.45f), .3f); TopLoot("toilets", new(-27.5f, F + .9f, 53.45f), .3f);
        Tube(toilets, new(-30.5f, F + pc - .08f, 58.5f), false, 1);
        FloorLoot("toilets", -33f, 53.5f, -25.5f, 63.5f, F, .3f, 2.4f, .6f);
        Enemy("toilets", -30.5f, F, 59);

        var waiting = RoomV3(t, "Waiting room", -36, 64, -25, 74, F, pc, tile, floorTile, null, null, null, Solid);
        for (int i = 0; i < 3; i++) { Box(waiting, "Waiting bench", new(-31, F + .22f, 66 + i * 2.8f), new(4f, .45f, .6f), wood); TopLoot("waiting room", new(-32.2f, F + .45f, 66 + i * 2.8f), .2f); TopLoot("waiting room", new(-29.8f, F + .45f, 66 + i * 2.8f), .2f); }
        for (int i = 0; i < 3; i++) { Box(waiting, "Vending machine", new(-35.5f, F + .9f, 65.6f + i * 1.15f), new(.8f, 1.8f, 1.05f), steel); GlowBox(waiting, "Vending machine front", new(-35.08f, F + 1.15f, 65.6f + i * 1.15f), new(.03f, 1.1f, .8f), glowScreen); }
        Tube(waiting, new(-30.5f, F + pc - .08f, 69), false, 0);
        FloorLoot("waiting room", -34.5f, 64.5f, -25.5f, 73.5f, F, .2f, 2.4f, .5f);
        Enemy("waiting room", -27.5f, F, 70);

        var lockers = RoomV3(t, "Coin lockers", -36, 74, -25, 84, F, pc, tile, floorTile, null, null, null, Solid);
        foreach (var (x, z, w, d) in new[] { (-35.6f, 79f, .7f, 9f), (-30.5f, 79f, .7f, 5f) })
        {
            Box(lockers, "Coin locker bank", new(x, F + .5f, z), new(w, 1f, d), steel);
            for (float zz = z - d / 2 + .8f; zz < z + d / 2; zz += 1.6f) TopLoot("coin lockers", new(x, F + 1f, zz), .5f);
        }
        Tube(lockers, new(-28, F + pc - .08f, 79), false, 2);
        PL(lockers, "Baked locker light", new(-33, F + 2f, 79), Cool, 6, 6);
        FloorLoot("coin lockers", -34.8f, 74.5f, -25.5f, 83.5f, F, .45f, 2.4f, .5f);
        Enemy("coin lockers", -27.5f, F, 76);

        var konbini = RoomV3(t, "Konbini", -36, 84, -25, 102, F, pc, tile, floorTile, null, null, null, Solid);
        foreach (float x in new[] { -33f, -30.5f, -28f })
        {
            Box(konbini, "Gondola shelf", new(x, F + .65f, 93), new(.9f, 1.3f, 12f), steel);
            for (float z = 88f; z <= 98.5f; z += 1.5f) TopLoot("konbini", new(x, F + 1.3f, z), .15f);
        }
        for (int i = 0; i < 4; i++) { Box(konbini, "Drinks fridge", new(-35.6f, F + 1f, 86 + i * 1.3f), new(.7f, 2f, 1.2f), steel); GlowBox(konbini, "Fridge glow", new(-35.23f, F + 1.1f, 86 + i * 1.3f), new(.03f, 1.6f, 1f), glowScreen); }
        Box(konbini, "Konbini counter", new(-26.2f, F + .5f, 100.2f), new(1f, 1f, 3f), wood);
        TopLoot("konbini", new(-26.2f, F + 1f, 99.3f), .2f); TopLoot("konbini", new(-26.2f, F + 1f, 101.1f), .2f);
        Sign3(konbini, "KONBINI  24", new(-25.15f, -1.0f, 93), 90, .32f);
        Tube(konbini, new(-31.8f, F + pc - .08f, 89), false, 1); Tube(konbini, new(-31.8f, F + pc - .08f, 97), false, 0);
        FloorLoot("konbini", -35f, 84.4f, -25.6f, 101.6f, F, .15f, 2.5f, .5f);
        Enemy("konbini", -31.8f, F, 86); Enemy("konbini back", -31.8f, F, 100.5f);

        var staff = RoomV3(t, "Staff room", -36, 102, -25, 110, F, pc, tile, floorTile, null, null, null, Solid);
        Box(staff, "Staff table", new(-30.5f, F + .38f, 106), new(3f, .76f, 1.4f), wood);
        Box(staff, "Staff lockers", new(-35.6f, F + .9f, 104), new(.6f, 1.8f, 3f), steel);
        TopLoot("staff room", new(-31.5f, F + .76f, 106), .6f); TopLoot("staff room", new(-29.5f, F + .76f, 106), .6f);
        Tube(staff, new(-30.5f, F + pc - .08f, 106), false, 0);
        FloorLoot("staff room", -35f, 102.4f, -25.6f, 109.6f, F, .5f, 2.4f, .5f);
        Enemy("staff room", -27f, F, 108.6f);

        var vault = RoomV3(t, "Cable vault", -36, 130, -30, 146, F, HallClear, darkTile, concrete, null, new[] { 136f, 139 }, Solid, Solid);
        for (int i = 0; i < 3; i++) { Box(vault, "Cable drum", new(-33.5f, F + .6f, 132.5f + i * 4.8f), new(1.2f, 1.2f, 1.2f), wood); TopLoot("cable vault", new(-33.5f, F + 1.2f, 132.5f + i * 4.8f), 1f); }
        Tube(vault, new(-33, F + HallClear - .08f, 141), true, 1);
        FloorLoot("cable vault", -35.5f, 130.5f, -30.5f, 145.5f, F, 1f, 2.4f, .5f);
        Enemy("cable vault", -32.5f, F, 143.5f);

        var gallery = RoomV3(t, "Cable gallery", -30, 124, -17, 146, F, HallClear, darkTile, concrete, null, Solid, Solid, Solid);
        Wall(gallery, "Wall", true, -30, 124, 130, F, HallClear + .3f, darkTile);
        for (float z = 125.5f; z < 146; z += 5)
        {
            GlowBox(gallery, "Gallery window (lit)", new(-17.14f, -1.6f, z), new(.03f, .9f, 1.5f), glowWindow);
            PL(gallery, "Baked window glow", new(-17.8f, -1.6f, z), Warm, 5, 5);
        }
        Box(gallery, "Cable tray", new(-29.6f, -1.3f, 135), new(.5f, .12f, 21), steel, false);
        Box(gallery, "Cable tray", new(-17.4f, -.95f, 135), new(.5f, .12f, 21), steel, false);
        foreach (float z in new[] { 128f, 134f, 140f }) { Box(gallery, "Cable cabinet", new(-23.5f, F + .7f, z), new(2.2f, 1.4f, 1f), steel); TopLoot("cable gallery", new(-24.2f, F + 1.4f, z), 1f); TopLoot("cable gallery", new(-22.8f, F + 1.4f, z), 1f); }
        FloorLoot("cable gallery", -29.5f, 124.5f, -17.5f, 145.5f, F, .9f, 3f, .6f);
        Enemy("cable gallery south", -20, F, 126.5f); Enemy("cable gallery north", -27, F, 143); Enemy("cable gallery middle", -20, F, 137);
    }

    // ---------------- east maintenance side ----------------
    static void EastRooms(Transform t)
    {
        float hc = HallClear;
        RoomV3(t, "Maintenance hall", 13, 40, 17, 128, F, hc, paint, concrete, new[] { 40f, 44, 46, 50, 81.75f, 83.75f, 110, 114 }, new[] { 61f, 63, 75, 77, 89, 91, 103, 105 }, Solid, null);
        int k = 0;
        for (float z = 44; z <= 124; z += 10, k++) Tube(t, new(15, F + hc - .08f, z), true, k % 3 == 1 ? 2 : k % 4 == 2 ? 1 : 0, k % 2 == 0);
        FloorLoot("maintenance hall", 15, 42, 15, 126, F, .6f, 8f, 0);
        foreach (float z in new[] { 52f, 72f, 100f, 122f }) Enemy("maintenance hall", 15, F, z);

        RoomV3(t, "South cross passage", 4, 46, 13, 50, F, hc, darkTile, darkTile, null, null, null, new[] { 8.05f, 9.55f });
        RoomV3(t, "Wreck side door passage", 9.7f, 81.75f, 13, 83.75f, F, hc, darkTile, darkTile, null, null, Solid, Solid);
        RoomV3(t, "North cross passage", 4, 110, 13, 114, F, hc, darkTile, darkTile, null, null, Solid, null);

        var gallery = RoomV3(t, "Fan gallery", 8, 12, 13, 44, F, hc, darkTile, concrete, Solid, new[] { 28f, 31, 40, 44 }, Solid, Solid);
        foreach (float z in new[] { 18f, 28f, 38f })
        {
            Box(gallery, "Fan duct grille", new(8.14f, -1.6f, z), new(.03f, 1.8f, 2.4f), steel, false);
            Sign3(gallery, "FAN " + (z == 18 ? 1 : z == 28 ? 2 : 3), new(8.2f, -.95f, z), 90, .25f);
        }
        Tube(gallery, new(10.5f, F + hc - .08f, 20), true, 0); Tube(gallery, new(10.5f, F + hc - .08f, 34), true, 1);
        FloorLoot("fan gallery", 10.5f, 13, 10.5f, 43, F, .7f, 4f, 0);
        Enemy("fan gallery south", 10.5f, F, 15); Enemy("fan gallery north", 10.5f, F, 33);

        var control = RoomV3(t, "Fan control room", 13, 22, 25, 36, F, hc, paint, concrete, null, Solid, Solid, Solid);
        Box(control, "Control desk", new(21, F + .45f, 34.9f), new(6f, .9f, 1f), steel);
        GlowBox(control, "Control panel lamps", new(21, F + .92f, 34.6f), new(5f, .03f, .3f), glowRed);
        for (int i = 0; i < 3; i++) { Box(control, "Switch cabinet", new(24.6f, F + .9f, 24.5f + i * 2.2f), new(.6f, 1.8f, 1.6f), steel); }
        TopLoot("fan control", new(19.5f, F + .9f, 35.1f), 1f); TopLoot("fan control", new(22.5f, F + .9f, 35.1f), 1f);
        PL(control, "Baked control light", new(19, F + 1.9f, 29), Warm, 8, 7);
        FloorLoot("fan control", 13.5f, 22.5f, 23.8f, 33.8f, F, .9f, 2.5f, .5f);
        Enemy("fan control", 17, F, 26);

        foreach (var (name, z0, door) in new[] { ("Crew lockers", 56f, 62f), ("Signal room", 70f, 76f), ("Canteen", 84f, 90f), ("Electrical room", 98f, 104f) })
        {
            bool signal = name == "Signal room";
            var room = RoomV3(t, name, 17, z0, 28, z0 + 12, F, hc, paint, concrete, null, signal ? new[] { 75f, 77 } : Solid, Solid, Solid);
            switch (name)
            {
                case "Crew lockers":
                    Box(room, "Crew locker row", new(27.6f, F + .9f, z0 + 6), new(.6f, 1.8f, 9f), steel);
                    Box(room, "Changing bench", new(22, F + .22f, z0 + 6), new(.6f, .45f, 6f), wood);
                    for (float zz = z0 + 3.6f; zz <= z0 + 8.4f; zz += 2.4f) TopLoot("crew lockers", new(22, F + .45f, zz), .8f);
                    break;
                case "Signal room":
                    Box(room, "Signal desk", new(22.5f, F + .45f, z0 + 11.3f), new(7f, .9f, 1f), steel);
                    GlowBox(room, "Signal diagram", new(22.5f, -1.2f, z0 + 11.84f), new(6f, .9f, .03f), glowScreen);
                    for (float x = 20; x <= 25; x += 1.6f) TopLoot("signal room", new(x, F + .9f, z0 + 11.3f), .9f);
                    break;
                case "Canteen":
                    foreach (float zz in new[] { z0 + 3.5f, z0 + 8.5f }) { Box(room, "Canteen table", new(22.5f, F + .38f, zz), new(4f, .76f, 1.2f), wood); TopLoot("canteen", new(21.5f, F + .76f, zz), .7f); TopLoot("canteen", new(23.5f, F + .76f, zz), .7f); }
                    Box(room, "Kitchen counter", new(27.6f, F + .45f, z0 + 6), new(.6f, .9f, 8f), concrete);
                    break;
                default:
                    for (int i = 0; i < 4; i++) Box(room, "Switchgear", new(27.5f, F + 1f, z0 + 2 + i * 2.6f), new(.8f, 2f, 2f), steel);
                    GlowBox(room, "Breaker lamps", new(27.08f, -1.4f, z0 + 6), new(.03f, .1f, 9f), glowRed);
                    break;
            }
            Tube(room, new(22.5f, F + hc - .08f, z0 + 6), false, name == "Canteen" ? 1 : name == "Electrical room" ? 2 : 0, name != "Electrical room");
            if (name == "Electrical room") RedLamp(room, new(17.14f, -1f, z0 + 9), 90);
            FloorLoot(name.ToLower(), 17.5f, z0 + .5f, 26.6f, z0 + 11.5f, F, name == "Electrical room" ? 1f : .7f, 2.6f, .5f);
            Enemy(name.ToLower(), 21, F, z0 + 6);
        }
        RoomV3(t, "Parts passage", 28, 75, 30, 77, F, hc, paint, concrete, null, null, Solid, Solid);
        var parts = RoomV3(t, "Parts store", 30, 62, 42, 92, F, hc, paint, concrete, new[] { 75f, 77 }, Solid, Solid, Solid);
        foreach (float z in new[] { 65f, 70f, 82f, 87f })
        {
            Box(parts, "Parts rack", new(36, F + .75f, z), new(8f, 1.5f, .8f), steel);
            for (float x = 33f; x <= 39f; x += 2f) TopLoot("parts store", new(x, F + 1.5f, z), 1f);
        }
        Tube(parts, new(36, F + hc - .08f, 76), false, 1); Tube(parts, new(36, F + hc - .08f, 66), false, 2, false);
        PL(parts, "Baked parts light", new(36, F + 1.9f, 86), Cool, 6, 7);
        FloorLoot("parts store", 30.5f, 62.5f, 41.5f, 91.5f, F, 1f, 3f, .5f);
        Enemy("parts store south", 36, F, 67.5f); Enemy("parts store north", 36, F, 89.5f);

        var pump = RoomV3(t, "Pump room", 8, 128, 32, 148, F, 3f, paint, concrete, Solid, Solid, new[] { 13.5f, 16.5f }, Solid);
        for (int i = 0; i < 4; i++)
        {
            Box(pump, "Sump grille", new(11.5f + i * 5.5f, F + .01f, 133), new(3.6f, .02f, 3f), darkTile, false);
            for (int r = 0; r < 6; r++) Box(pump, "Grille bar", new(10f + i * 5.5f + r * .6f, F + .025f, 133), new(.06f, .03f, 3f), steel, false);
            Box(pump, "Pump housing", new(11.5f + i * 5.5f, F + 1f, 141), new(2.2f, 2f, 2.2f), steel);
            Beam(pump, "Pump pipe", new(11.5f + i * 5.5f, F + 2.6f, 141), new(11.5f + i * 5.5f, F + 2.6f, 147.6f), .35f, .35f, steel, false);
            TopLoot("pump room", new(11.5f + i * 5.5f, F + 2f, 141), 1f);
        }
        Tube(pump, new(14, F + 2.92f, 136), false, 0); Tube(pump, new(25, F + 2.92f, 136), false, 1); Tube(pump, new(20, F + 2.92f, 145), false, 2);
        FloorLoot("pump room", 8.5f, 128.5f, 31.5f, 147.5f, F, 1f, 3f, .6f);
        Enemy("pump room west", 11, F, 137); Enemy("pump room east", 29, F, 137); Enemy("pump room north", 20, F, 146);
    }
}
#endif
