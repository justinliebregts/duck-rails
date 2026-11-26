#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>V3 Passenger Station rooms with their detail props, hall clutter and the King's Quack facades.</summary>
public static partial class RailAbandonedStation
{
    const string StationName = "KING'S QUACK STATION";

    // Prop helper: solid box with an optional yaw (degrees) and roll.
    static GameObject P(Transform t, string n, float x, float y, float z, float w, float h, float d, Material m, bool solid = true, float yaw = 0, float roll = 0)
    {
        var g = Box(t, n, new(x, y, z), new(w, h, d), m, solid);
        if (yaw != 0 || roll != 0) g.transform.localRotation = Quaternion.Euler(0, yaw, roll);
        return g;
    }

    // Pallet with a stack of boxes; loot can sit on top.
    static void Pallet(Transform t, string area, float x, float z, bool alongZ, int layers, float gold)
    {
        float w = alongZ ? 1.0f : 1.2f, d = alongZ ? 1.2f : 1.0f;
        P(t, "Pallet", x, F + .07f, z, w, .14f, d, wood);
        float top = F + .14f;
        for (int i = 0; i < layers; i++)
        {
            P(t, "Pallet load", x, top + .25f, z, w - .1f, .5f, d - .1f, i % 2 == 0 ? wood : paint);
            top += .5f;
        }
        P(t, "Pallet wrap strap", x, top - .25f, z, w - .08f, .06f, .06f, yellow, false);
        TopLoot(area, new(x, top, z), gold);
    }

    static void CrateStack(Transform t, string area, float x, float z, float gold)
    {
        P(t, "Crate", x, F + .3f, z, .6f, .6f, .6f, wood);
        P(t, "Crate", x + .05f, F + .85f, z - .05f, .5f, .5f, .5f, wood, true, 14);
        P(t, "Crate", x, F + .2f, z + .65f, .4f, .4f, .4f, paint, true, 30);
        TopLoot(area, new(x + .05f, F + 1.1f, z - .05f), gold);
    }

    // Bank of lockers 0.6 m deep standing on baseCentre; face = +1/-1 along the axis across the bank.
    static void LockerBank(Transform t, Vector3 baseCentre, float length, bool alongZ, float face, float height, int tiers)
    {
        Vector3 S(float across, float h, float along) => alongZ ? new(across, h, along) : new(along, h, across);
        Vector3 Off(float across, float along) => alongZ ? new(across, 0, along) : new(along, 0, across);
        Box(t, "Locker bank", baseCentre + Vector3.up * (height / 2), S(.6f, height, length), steel);
        for (float a = -length / 2 + .45f; a < length / 2 - .1f; a += .45f)
            Box(t, "Locker door seam", baseCentre + Off(face * .308f, a) + Vector3.up * (height / 2), S(.016f, height - .06f, .025f), darkTile, false);
        for (int i = 1; i < tiers; i++)
            Box(t, "Locker tier seam", baseCentre + Off(face * .312f, 0) + Vector3.up * (height * i / tiers), S(.024f, .025f, length - .06f), darkTile, false);
    }

    // Low-poly pram (batch 6): oval bassinet with rim and inner shell, segmented folding hood over the rear, tube
    // frame joining the wheels to a push handle fixed to the body, rimmed wheels with hubs, a blanket over the side.
    // Local +Z is the front; the hood and handle are at the rear (-Z). Solid as one box for spawn clearance.
    static void Pram(Transform g, Material fabric)
    {
        var bowl = SaveProc("pram-bassinet", () => Lathe(new MB(), new[] { new Vector2(0, 0), new Vector2(.16f, 0), new Vector2(.21f, .08f), new Vector2(.23f, .22f), new Vector2(.235f, .3f),
            new Vector2(.212f, .3f), new Vector2(.205f, .2f), new Vector2(.18f, .1f), new Vector2(0, .07f) }, 10, 1, 1.75f), false);
        Proc(g, "Pram bassinet", bowl, new[] { fabric }, new(0, .38f, 0), Quaternion.identity, false, false);
        var pivot = new Vector3(0, .68f, -.2f);   // hood arc from the rear rim up over the back half
        for (int k = 0; k < 4; k++)
        {
            float th = (75 + 30 * k) * Mathf.Deg2Rad, r = .21f + k * .006f;
            var radial = new Vector3(0, Mathf.Sin(th), Mathf.Cos(th)); var tangent = new Vector3(0, Mathf.Cos(th), -Mathf.Sin(th));
            Box(g, "Pram hood segment", pivot + radial * r, new(.48f + k * .01f, .02f, .125f), darkTile, false).transform.localRotation = Quaternion.LookRotation(tangent, radial);
        }
        void Tube(Vector3 a, Vector3 b, float rad = .012f, Material mat = null)
        {
            float len = Mathf.Round((b - a).magnitude * 100) / 100;
            Proc(g, "Pram frame", PipeMesh(rad, len), new[] { mat ? mat : steel }, a, Quaternion.FromToRotation(Vector3.up, (b - a).normalized), false, false);
        }
        foreach (float sx in new[] { -.2f, .2f })
        {
            Tube(new(sx, .14f, .33f), new(sx * .65f, .42f, .2f));     // front leg up into the bassinet
            Tube(new(sx, .14f, -.33f), new(sx * .65f, .42f, -.2f));   // rear leg
            Tube(new(sx * .75f, .58f, -.3f), new(sx * .85f, 1.0f, -.74f));   // push handle, fixed into the bassinet's rear side
        }
        Tube(new(-.19f, 1.0f, -.74f), new(.19f, 1.0f, -.74f), .018f, darkTile);   // grip
        foreach (float sz in new[] { -.33f, .33f })
        {
            Tube(new(-.25f, .14f, sz), new(.25f, .14f, sz), .01f);   // axle
            foreach (float sx in new[] { -.25f, .25f })
            {
                Proc(g, "Pram wheel", WheelMesh(), new[] { darkTile }, new(sx, .14f, sz), Quaternion.Euler(0, 0, 90), false, false).transform.localScale = Vector3.one * .3f;
                Box(g, "Pram wheel hub", new(sx + Mathf.Sign(sx) * .025f, .14f, sz), new(.012f, .05f, .05f), steel, false);
            }
        }
        Box(g, "Pram blanket", new(.02f, .69f, .12f), new(.36f, .015f, .42f), zoneOrange, false).transform.localRotation = Quaternion.Euler(0, 0, 4);
        Box(g, "Pram blanket (hanging)", new(.245f, .55f, .14f), new(.015f, .3f, .38f), zoneOrange, false).transform.localRotation = Quaternion.Euler(0, 0, 8);
        var col = g.gameObject.AddComponent<BoxCollider>(); col.center = new(0, .5f, -.1f); col.size = new(.52f, 1f, 1.15f);
        g.gameObject.layer = 6;
    }

    // ---------------- EXIT A2 (batch 6) ----------------
    // A full line of closed ticket gates across the room in front of the shuttered street exit, and a glass-fronted
    // service kiosk in the north-west corner. Room: X -36..-16, Z 18..34.
    static void ExitA2Gates(Transform portal)
    {
        const float gx = -20.6f;   // gate line (cabinets 1.3 m long in X, 1.0 m passages between them)
        for (int i = 0; i < 13; i++)
        {
            float z = 18.27f + i * 1.28f;
            Box(portal, "Ticket gate cabinet", new(gx, F + .5f, z), new(1.3f, 1f, .28f), steel);
            Box(portal, "Gate card reader", new(gx - .35f, F + 1.01f, z), new(.25f, .02f, .2f), darkTile, false);
            GlowBox(portal, "Gate stop light", new(gx - .66f, F + .82f, z), new(.02f, .12f, .12f), glowRed);
            GlowBox(portal, "Gate stop light", new(gx + .66f, F + .82f, z), new(.02f, .12f, .12f), glowRed);
            if (i < 12)
                foreach (float s in new[] { -1f, 1f })   // closed flaps meeting in the middle of each passage
                    Box(portal, "Gate flap (closed)", new(gx, F + .75f, z + .64f + s * .255f), new(.04f, .5f, .49f), wreckRed);
        }
        SignBoard(portal, "EXIT  A2", new(gx - .1f, F + 1.86f, 26f), -90, .2f, 'u', F + HallClear);
        foreach (var (x, m) in new[] { (-27.5f, zoneBlue), (-25f, wreckRed), (-22.5f, zoneGreen), (-18.5f, yellow) })
            P(portal, "Faded poster", x, -1.35f, 33.84f, 1.1f, 1.5f, .02f, m, false);
        foreach (float x in new[] { -27.5f, -24f }) { P(portal, "Waiting bench", x, F + .22f, 20.6f, 3f, .45f, .6f, wood); TopLoot("service portal", new(x - .8f, F + .45f, 20.6f), .3f); TopLoot("service portal", new(x + .8f, F + .45f, 20.6f), .3f); }

        // Service kiosk: counter wall, glass above with a speaking slot, header with an INFORMATION sign, desk inside.
        var glass = AtlasTile("Booth glass", "dark asphalt", true, 128, 128, 64, new(.32f, .4f, .44f), 4);
        const float kx0 = -35.4f, kx1 = -29f, kz = 29.6f, cx = (kx0 + kx1) / 2, depth = 33.875f - kz;
        Box(portal, "Kiosk counter wall", new(cx, F + .5f, kz + .06f), new(kx1 - kx0, 1f, .12f), whitePanel);
        Box(portal, "Kiosk counter top", new(cx, F + 1.02f, kz - .05f), new(kx1 - kx0 + .1f, .04f, .42f), wood);
        Box(portal, "Kiosk side wall", new(kx1 - .06f, F + .5f, kz + .12f + (depth - .13f) / 2), new(.12f, 1f, depth - .13f), whitePanel);   // starts behind the counter wall
        foreach (var (x0, x1) in new[] { (kx0, -33.2f), (-31.2f, kx1) })
            Box(portal, "Kiosk glass", new((x0 + x1) / 2, F + 1.33f, kz + .06f), new(x1 - x0 - .06f, .58f, .006f), glass, false);
        Box(portal, "Kiosk glass (over the speaking slot)", new(-32.2f, F + 1.5f, kz + .06f), new(1.94f, .24f, .006f), glass, false);
        Box(portal, "Kiosk side glass", new(kx1 - .06f, F + 1.33f, kz + depth / 2), new(.006f, .58f, depth - .1f), glass, false);
        foreach (float x in new[] { kx0 + .05f, -33.2f, -31.2f, kx1 - .05f }) Box(portal, "Kiosk mullion", new(x, F + 1.33f, kz + .06f), new(.06f, .58f, .07f), steel);
        Box(portal, "Kiosk header", new(cx, F + 1.84f, kz + .06f), new(kx1 - kx0, .42f, .14f), whitePanel);
        Box(portal, "Kiosk roof", new(cx, F + 2.07f, kz + depth / 2), new(kx1 - kx0, .04f, depth - .02f), whitePanel, false);
        SignBoard(portal, "INFORMATION", new(cx, F + 1.84f, kz - .065f), 180, .14f);
        Box(portal, "Kiosk desk", new(cx, F + .38f, 32.9f), new(3f, .76f, .8f), wood);
        Box(portal, "Kiosk CRT", new(cx - .7f, F + .98f, 33.0f), new(.45f, .4f, .4f), tile);
        GlowBox(portal, "Kiosk CRT screen", new(cx - .7f, F + .99f, 32.79f), new(.34f, .26f, .02f), glowScreen);
        Box(portal, "Ticket printer", new(cx + .6f, F + .86f, 32.9f), new(.4f, .2f, .35f), darkTile);
        Box(portal, "Kiosk stool", new(cx, F + .3f, 31.9f), new(.4f, .6f, .4f), steel);
        for (int i = 0; i < 3; i++) Box(portal, "Kiosk paperwork", new(cx + .2f + i * .25f, F + .765f, 32.7f), new(.21f, .004f, .3f), tile, false).transform.localRotation = Quaternion.Euler(0, i * 23, 0);
        TopLoot("service portal", new(-34.2f, F + 1.04f, kz - .05f), .4f); TopLoot("service portal", new(-30.2f, F + 1.04f, kz - .05f), .4f);

        Tube(portal, new(-28, F + HallClear - .08f, 26), false, 0); Tube(portal, new(-20, F + HallClear - .08f, 26), false, 1);
        FloorLoot("service portal", -34.5f, 18.5f, -21.8f, 29.0f, F, .3f, 3f, .6f);   // the open side of the gates, outside the kiosk
        FloorLoot("service portal", -28.6f, 29.0f, -21.8f, 33.5f, F, .3f, 3f, .3f);
        Enemy("service portal", -26, F, 24); Enemy("service portal east", -23.5f, F, 31f);
    }

    // ---------------- reverb (batch 5: "lots of reverb") ----------------
    static List<(string name, Bounds b)> reverbRooms;
    enum Echo { Small, Medium, Big, Tunnel }

    static void StationReverb(Transform t)
    {
        void Zone3(string n, Vector3 c, Vector3 size, Quaternion rot, Echo e)
        {
            var box = Group(t, n + " reverb"); box.localPosition = c; box.localRotation = rot;
            var z = Group(box, "Reverb settings").gameObject.AddComponent<AudioReverbZone>();
            z.reverbPreset = AudioReverbPreset.User;
            var (decay, room, roomHF, refl, rev, rd, vd, hf) = e switch
            {
                Echo.Tunnel => (5.5f, -150, -500, -500, 350, .04f, .06f, .7f),
                Echo.Big => (4.2f, -250, -650, -650, 250, .03f, .05f, .8f),
                Echo.Medium => (2.4f, -400, -800, -800, 100, .02f, .035f, .8f),
                _ => (1.4f, -600, -1000, -900, -100, .01f, .02f, .85f),
            };
            z.decayTime = decay; z.room = room; z.roomHF = roomHF; z.reflections = refl; z.reverb = rev; z.reflectionsDelay = rd; z.reverbDelay = vd; z.decayHFRatio = hf;
            var so = new UnityEditor.SerializedObject(box.gameObject.AddComponent<Game.Audio.BoxReverbZone>());
            so.FindProperty("size").vector3Value = size; so.FindProperty("fadeDistance").floatValue = e == Echo.Small ? .6f : 1.2f;
            so.FindProperty("zone").objectReferenceValue = z; so.ApplyModifiedPropertiesWithoutUndo();
        }
        var flat = Quaternion.identity;
        foreach (var (name, b) in reverbRooms)
        {
            float area = b.size.x * b.size.z;
            Zone3(name, b.center, b.size, flat, area > 300 ? Echo.Big : area > 60 ? Echo.Medium : Echo.Small);
        }
        Zone3("Platform 1 and track channel", new(-15, F + 1.6f, 80), new(20, 3.6f, 54), flat, Echo.Big);
        Zone3("Platform 3 and dead track", new(2.25f, -2.2f, 80), new(14.9f, 3.6f, 68), flat, Echo.Big);
        Zone3("Ticket concourse", new(-12, Deck + 1.35f, 46.5f), new(32, 2.7f, 13), flat, Echo.Big);
        Zone3("Shuttered concourse", new(-12, Deck + 1.35f, 113.5f), new(32, 2.7f, 13), flat, Echo.Big);
        foreach (var (x0, zb, zt) in new[] { (WestShaft, 65f, 53f), (WestShaft, 95f, 107f), (EastShaft, 65f, 53f), (EastShaft, 95f, 107f) })
        {
            var a = new Vector3(x0 + 2.25f, F + 1.3f, zb); var c = new Vector3(x0 + 2.25f, Deck + 1.3f, zt);
            Zone3("Stair shaft", (a + c) / 2, new(4.5f, 3f, (c - a).magnitude), Quaternion.LookRotation(c - a), Echo.Big);
        }
        Zone3("Cave-in shaft", new(5.85f, 3f, 102.4f), new(7.5f, 12f, 8.6f), flat, Echo.Big);
        foreach (var (z0, z1) in new[] { (6f, 53f), (107f, 154f) })
            for (float z = z0; z < z1 - .1f; z += 8f)
            {
                float zc = Mathf.Min(z + 4, (z + z1) / 2), len = Mathf.Min(8.6f, z1 - z + .6f);
                var tg = new Vector3(RailX(zc + .5f) - RailX(zc - .5f), RailHeight(zc + .5f) - RailHeight(zc - .5f), 1);
                Zone3("Tunnel", new(RailX(zc), RailHeight(zc) + 2, zc), new(6.5f, 4.6f, len), Quaternion.LookRotation(tg), Echo.Tunnel);
            }
    }

    // ---------------- evacuation clutter (batch 5) ----------------
    // Things dropped when the station was evacuated in a hurry. Bulky items are solid (so spawns avoid them);
    // small ones are visual only. Every spot is off the walk lines and the shaft openings.
    static void EvacuationClutter(Transform root)
    {
        // Batch 5: more enemy candidates so they spread through every platform, hall and concourse (never on track).
        foreach (var (area, x, y, z) in new[] { ("platform 1 south", -16.6f, F, 58f), ("platform 1 north", -16.6f, F, 102f), ("platform 1 shops", -24.1f, F, 70f), ("platform 1 shops", -24.1f, F, 90f),
                 ("service ledge", -7f, F, 60f), ("ticket concourse", -16f, Deck, 50f), ("shuttered concourse", -12f, Deck, 110f),
                 ("transfer hall", -38.8f, F, 45f), ("transfer hall", -38.8f, F, 85f), ("transfer hall", -38.8f, F, 115f),
                 ("maintenance hall", 15f, F, 62f), ("maintenance hall", 15f, F, 90f), ("maintenance hall", 15f, F, 110f),
                 ("platform 3", 1.8f, F, 66f), ("platform 3", 1.8f, F, 94f) })
            Enemy(area, x, y, z);
        var t = Group(root, "Evacuation clutter");
        const float D = Deck;
        var spots = new (string kind, float x, float y, float z, float yaw)[]
        {
            ("suitcase lying", -17.6f, F, 58.5f, 30), ("rolling bag", -21.0f, F, 68.0f, 70), ("papers", -19.0f, F, 71.0f, 0), ("umbrella", -22.4f, F, 76.8f, 35),
            ("cup", -17.3f, F, 83.2f, 0), ("pram", -19.6f, F, 90.5f, 15), ("shoe", -15.6f, F, 75f, 60), ("hat", -23.0f, F, 92f, 0), ("tickets", -20.2f, F, 84.2f, 0),
            ("rolling bag", -5.6f, F, 66f, 10), ("papers", -8.4f, F, 88f, 40),
            ("suitcase", -22f, D, 49.5f, 80), ("tickets", -12.8f, D, 44f, 0), ("rolling bag", -6.5f, D, 43.0f, 200), ("umbrella", -17.5f, D, 51.6f, 100), ("cup", -24.5f, D, 44.2f, 0),
            ("suitcase lying", -19f, D, 110.8f, 15), ("papers", -8f, D, 118f, 0), ("hat", -2f, D, 112f, 0),
            ("rolling bag", -40.9f, F, 40f, 90), ("suitcase", -37.0f, F, 88f, 0), ("pram", -40.9f, F, 104f, 80), ("papers", -38.8f, F, 66f, 20), ("shoe", -39.6f, F, 98f, 20),
            ("cleaning trolley", 16.4f, F, 72.5f, 0), ("hat", 13.6f, F, 90f, 0), ("papers", 15.6f, F, 112f, 70),
            ("suitcase", -2.6f, F, 62f, 40), ("papers", 2.4f, F, 70f, 0), ("umbrella", -1.6f, F, 100f, 120),
            ("rolling bag", -27.2f, F, 66.2f, 0), ("basket", -27.6f, F, 93.8f, 20), ("bento", -30.5f, F + .76f, 106.3f, 15),
            ("papers", -1.6f, D + .01f, 43.8f, 0), ("rolling bag", 5.35f, F, 75f, 0), ("shoe", -21.0f, F + 11 * (Deck - F) / 24, 59.75f, 80),
            ("cup", 21.9f, F + .76f, 87.6f, 0), ("tickets", -26.5f, F, 27.6f, 0), ("umbrella", -33.8f, F, 38.4f, 60),
        };
        Material[] bagMats = { wreckRed, zoneBlue, darkTile, zoneGreen };
        int n = 0;
        foreach (var (kind, x, y, z, yaw) in spots)
        {
            var g = Group(t, "Dropped " + kind); g.localPosition = new(x, y, z); g.localRotation = Quaternion.Euler(0, yaw, 0);
            var m = bagMats[n++ % bagMats.Length];
            switch (kind)
            {
                case "suitcase":
                    Box(g, "Suitcase", new(0, .33f, 0), new(.45f, .65f, .22f), m);
                    Box(g, "Suitcase handle", new(0, .68f, 0), new(.16f, .05f, .03f), darkTile, false); break;
                case "suitcase lying":
                    Box(g, "Suitcase", new(0, .11f, 0), new(.45f, .22f, .65f), m);
                    Box(g, "Suitcase handle", new(0, .11f, .34f), new(.16f, .03f, .05f), darkTile, false); break;
                case "rolling bag":
                    Box(g, "Rolling bag", new(0, .13f, 0), new(.38f, .26f, .55f), m);
                    Box(g, "Rolling bag handle", new(.1f, .1f, .52f), new(.02f, .02f, .5f), steel, false);
                    Box(g, "Rolling bag handle", new(-.1f, .1f, .52f), new(.02f, .02f, .5f), steel, false); break;
                case "pram":
                    Pram(g, m); break;   // batch 6: proper low-poly pram
                case "cleaning trolley":
                    Box(g, "Trolley base", new(0, .12f, 0), new(.5f, .08f, .9f), yellow);
                    Box(g, "Trolley frame", new(0, .5f, -.42f), new(.48f, .8f, .05f), yellow);
                    Proc(g, "Mop bucket", BinMesh(), new[] { zoneBlue }, new(0, .16f, .15f), Quaternion.identity, false, false).transform.localScale = new(1.1f, .5f, 1.1f);
                    Box(g, "Mop handle", new(.12f, .75f, .15f), new(.03f, 1.2f, .03f), wood, false).transform.localRotation = Quaternion.Euler(12, 0, -8); break;
                case "umbrella":
                    Box(g, "Umbrella shaft", new(0, .02f, 0), new(.02f, .02f, .9f), darkTile, false);
                    Box(g, "Umbrella canopy", new(0, .06f, .15f), new(.32f, .08f, .55f), m, false); break;
                case "cup":
                    Proc(g, "Coffee cup", SaveProc("coffee-cup", () => Lathe(new MB(), new[] { new Vector2(0, 0), new Vector2(.035f, 0), new Vector2(.045f, .11f), new Vector2(0, .11f) }, 8), false), new[] { whitePanel }, new(0, .045f, 0), Quaternion.Euler(0, 0, 90), false, false); break;
                case "shoe":
                    Box(g, "Lost shoe", new(0, .04f, 0), new(.1f, .08f, .26f), darkTile, false); break;
                case "hat":
                    Proc(g, "Dropped hat", SaveProc("hat", () => Lathe(new MB(), new[] { new Vector2(0, 0), new Vector2(.17f, 0), new Vector2(.17f, .015f), new Vector2(.1f, .02f), new Vector2(.1f, .11f), new Vector2(0, .12f) }, 8), false), new[] { m }, Vector3.zero, Quaternion.identity, false, false); break;
                case "papers":
                    for (int i = 0; i < 5; i++) Box(g, "Scattered paper", new(Mathf.Sin(i * 2.1f) * .45f, .002f + i * .001f, Mathf.Cos(i * 1.7f) * .4f), new(.21f, .001f, .3f), tile, false).transform.localRotation = Quaternion.Euler(0, i * 37, 0); break;
                case "tickets":
                    for (int i = 0; i < 8; i++) Box(g, "Dropped ticket", new(Mathf.Sin(i * 1.3f) * .5f, .002f + i * .001f, Mathf.Cos(i * 2.3f) * .5f), new(.06f, .001f, .03f), yellow, false).transform.localRotation = Quaternion.Euler(0, i * 53, 0); break;
                case "bento":
                    Box(g, "Dropped bento", new(0, .025f, 0), new(.2f, .05f, .15f), wreckRed, false);
                    Box(g, "Bento lid", new(.18f, .006f, .05f), new(.2f, .012f, .15f), darkTile, false).transform.localRotation = Quaternion.Euler(0, 30, 0); break;
                case "basket":
                    Box(g, "Dropped shop basket", new(0, .06f, 0), new(.3f, .12f, .4f), zoneBlue, false).transform.localRotation = Quaternion.Euler(0, 0, 80); break;
            }
        }
        // Warning tape (batch 6: anchored, off the stairs): south of the cave-in from the east stair shaft wall to
        // the wreck's side, north of it from the stair shaft wall to the cave-in shaft wall; a knot at every end.
        foreach (var (z, x1, dz) in new[] { (97.8f, 4.6f, 0f), (106.6f, 1.86f, 0f) })
            foreach (var (ya, yb, off) in new[] { (F + .95f, F + 1.25f, 0f), (F + 1.3f, F + .9f, .01f) })
            {
                Vector3 a = new(.04f, ya, z + off), b = new(x1, yb, z + off);
                Beam(t, "Warning tape", a, b, .005f, .07f, yellow, false);
                foreach (var e in new[] { a, b }) Box(t, "Tape knot", e + new Vector3(0, 0, .015f), new(.05f, .08f, .03f), yellow, false);
            }
    }

    // Open shop shelf unit on floor point c: uprights, a kick base, three boards (0.6 m apart so a spawn fits on
    // each) and a back panel (centre divider when two-sided). Length runs along local Z; a one-sided unit faces +X.
    // Mostly empty: a little leftover stock, and coal/gold spots on every board.
    static void ShelfUnit(Transform t, Vector3 c, float len, float yaw, bool twoSided, float sideDepth, Material[] goods, ref int g)
    {
        var u = Group(t, "Shelf unit"); u.localPosition = c; var rot = Quaternion.Euler(0, yaw, 0); u.localRotation = rot;
        float depth = twoSided ? sideDepth * 2 : sideDepth;
        float[] boards = { .12f, .72f, 1.32f };
        var upZ = new List<float> { -len / 2 + .02f, len / 2 - .02f }; if (len > 2.6f) upZ.Add(0);
        foreach (float z in upZ) Box(u, "Shelf upright", new(0, .725f, z), new(depth, 1.45f, .04f), steel);
        // Boards and base stop at the end uprights' inner faces; a middle upright splits them.
        var spans = upZ.Count == 3 ? new[] { (-len / 2 + .04f, -.02f), (.02f, len / 2 - .04f) } : new[] { (-len / 2 + .04f, len / 2 - .04f) };
        foreach (var (a, b) in spans)
        {
            Box(u, "Shelf base", new(0, .05f, (a + b) / 2), new(depth - .02f, .1f, b - a), steel);
            foreach (float y in boards) Box(u, "Shelf board", new(0, y, (a + b) / 2), new(depth, .03f, b - a), steel);
            Box(u, "Shelf back", new(twoSided ? 0 : -depth / 2 + .012f, .735f, (a + b) / 2), new(.02f, 1.17f, b - a), whitePanel, false);
        }
        foreach (float side in twoSided ? new[] { -1f, 1f } : new[] { 1f })
        {
            float x = twoSided ? side * sideDepth / 2 : .01f;
            foreach (float y in boards)
                for (float z = -len / 2 + .45f; z < len / 2 - .4f; z += .9f)
                {
                    if (upZ.Any(uz => Mathf.Abs(uz - z) < .3f)) continue;
                    TopLoot("konbini", c + rot * new Vector3(x, y + .015f, z), .3f);
                    // Leftover stock between the spawn spots on about one slot in six (probe-lit, not lightmapped).
                    if ((g++ * 7 + (int)(y * 10)) % 6 == 0 && z + .6f < len / 2 && !upZ.Any(uz => Mathf.Abs(uz - z - .45f) < .25f))
                    {
                        var item = Box(u, "Shelf stock", new(x, y + .015f + .11f, z + .45f), new(.3f, .22f, .1f + (g % 3) * .05f), goods[g % goods.Length], false);
                        UnityEditor.GameObjectUtility.SetStaticEditorFlags(item, 0); item.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
                    }
                }
        }
    }

    // Electrical cabinet standing on basePos; local +Z (after yaw) is the front with a door seam, vent and lamp.
    static void Cabinet(Transform t, string n, Vector3 basePos, float yaw, float w, float h, float d, Material lamp)
    {
        var g = Group(t, n); g.localPosition = basePos; g.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(g, n + " body", new(0, h / 2, 0), new(w, h, d), steel);
        Box(g, "Cabinet door seam", new(0, h / 2, d / 2 + .006f), new(.016f, h - .12f, .012f), darkTile, false);
        Box(g, "Cabinet vent", new(-w * .25f, .3f, d / 2 + .008f), new(w * .35f, .18f, .016f), darkTile, false);
        GlowBox(g, "Cabinet lamp", new(w * .3f, h - .25f, d / 2 + .01f), new(.08f, .08f, .02f), lamp);
        Box(g, "Cabinet handle", new(w * .12f, h * .55f, d / 2 + .02f), new(.03f, .18f, .04f), darkTile, false);
    }

    // Oil-cooled transformer: tank, cooling fins on both sides, three bushings and a terminal bar on top.
    static void Transformer(Transform t, Vector3 basePos, float yaw)
    {
        var g = Group(t, "Transformer"); g.localPosition = basePos; g.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(g, "Transformer tank", new(0, .75f, 0), new(1.4f, 1.5f, 1.1f), paint);
        foreach (float sx in new[] { -1f, 1f })
            for (float z = -.4f; z <= .41f; z += .2f) Box(g, "Cooling fin", new(sx * .76f, .7f, z), new(.12f, 1.1f, .04f), paint, false);
        var bushing = SaveProc("bushing", () => Lathe(new MB(), new[] { new Vector2(0, 0), new Vector2(.1f, 0), new Vector2(.1f, .08f), new Vector2(.06f, .1f), new Vector2(.08f, .16f), new Vector2(.05f, .19f), new Vector2(.07f, .25f), new Vector2(.03f, .3f), new Vector2(.03f, .38f), new Vector2(0, .38f) }, 8), false);
        for (int i = -1; i <= 1; i++) Proc(g, "Bushing", bushing, new[] { tile }, new(i * .4f, 1.5f, -.15f), Quaternion.identity, false, false);
        Box(g, "Terminal bar", new(0, 1.84f, -.15f), new(1.0f, .04f, .06f), steel, false);
        Box(g, "Rating plate", new(.3f, 1.0f, .556f), new(.3f, .2f, .012f), tile, false);
    }

    // Wall-mounted breaker panel; pos is its centre, local +Z (after yaw) faces into the room.
    static void BreakerPanel(Transform t, Vector3 pos, float yaw)
    {
        var g = Group(t, "Breaker panel"); g.localPosition = pos; g.localRotation = Quaternion.Euler(0, yaw, 0);
        Box(g, "Breaker panel box", Vector3.zero, new(.8f, 1.0f, .15f), steel, false);
        Box(g, "Breaker panel door seam", new(0, 0, .081f), new(.012f, .9f, .012f), darkTile, false);
        GlowBox(g, "Breaker lamp", new(-.25f, .38f, .085f), new(.06f, .06f, .02f), glowRed);
        GlowBox(g, "Breaker lamp", new(-.12f, .38f, .085f), new(.06f, .06f, .02f), glowScreen);
        Box(g, "Breaker handle", new(.2f, 0, .095f), new(.04f, .16f, .04f), darkTile, false);
        Box(g, "Breaker warning label", new(.2f, .32f, .082f), new(.22f, .14f, .014f), yellow, false);
    }

    // Broken-down maintenance buggy (golf-cart style) with a flat front tyre and a tipped load.
    static void Buggy(Transform t, string area, float x, float z)
    {
        var g = Group(t, "Broken maintenance buggy"); g.localPosition = new(x, F, z); g.localRotation = Quaternion.Euler(0, 0, -2.5f);
        Box(g, "Buggy chassis", new(0, .45f, 0), new(1.2f, .35f, 2.5f), steel);
        Box(g, "Buggy cowl", new(0, .8f, .95f), new(1.15f, .5f, .55f), yellow);
        Box(g, "Buggy seat", new(0, .85f, .25f), new(1.05f, .25f, .55f), darkTile);
        Box(g, "Buggy seat back", new(0, 1.15f, -.02f), new(1.03f, .5f, .12f), darkTile);
        Box(g, "Buggy steering column", new(-.3f, 1.05f, .78f), new(.06f, .5f, .06f), steel, false);
        Box(g, "Buggy wheel", new(-.32f, 1.28f, .72f), new(.4f, .05f, .4f), darkTile, false);
        foreach (float px in new[] { -.54f, .54f }) foreach (float pz in new[] { -1.15f, 1.15f })
                Box(g, "Canopy post", new(px, 1.42f, pz), new(.05f, 1.34f, .05f), steel, false);
        Box(g, "Buggy canopy", new(0, 2.05f, 0), new(1.3f, .06f, 2.5f), yellow);
        Box(g, "Cargo bed", new(0, .72f, -.75f), new(1.15f, .18f, 1.0f), steel);
        foreach (float px in new[] { -.6f, .6f }) foreach (float pz in new[] { -.9f, .95f })
                Box(g, "Buggy tyre", new(px, pz > 0 && px < 0 ? .16f : .25f, pz), new(.18f, pz > 0 && px < 0 ? .32f : .5f, .5f), darkTile);
        Box(t, "Tipped buggy load", new(x + .15f, F + .2f, z - 1.6f), new(.5f, .4f, .45f), wood).transform.localRotation = Quaternion.Euler(0, 25, 70);
        Box(t, "Spilled toolbox", new(x - .3f, F + .1f, z - 1.75f), new(.45f, .2f, .25f), wreckRed).transform.localRotation = Quaternion.Euler(0, -40, 0);
        TopLoot(area, new(x, F + .81f, z - .75f), .5f);
    }

    // ---------------- west rooms ----------------
    static void WestRooms(Transform t)
    {
        Zone(zoneYellow);
        float pc = PlatformClear;
        RoomV3(t, "Long transfer hall", -42, 18, -36, 134, F, HallClear, tile, darkTile, Solid, new[] { 24f, 27, 43, 46, 99, 101, 106, 108, 124, 127 }, Solid, Solid);
        Box(t, "Stopped moving walkway", new(-40.2f, F + .01f, 76), new(1.2f, .02f, 92), darkTile, false);
        foreach (float x in new[] { -40.85f, -39.55f }) Box(t, "Walkway edge", new(x, F + .015f, 76), new(.08f, .03f, 92), yellow, false);
        BandAlongZ(t, -41.86f, 18, 134, -1.75f);
        int k = 0;
        for (float z = 24; z <= 128; z += 8, k++) Tube(t, new(-38.6f, F + HallClear - .08f, z), true, k % 3 == 2 ? 2 : k % 4 == 1 ? 1 : 0, k % 2 == 0 && k % 3 != 2);
        FloorLoot("transfer hall", -38.8f, 20, -38.8f, 132, F, .3f, 9f, 0);
        foreach (float z in new[] { 30f, 60f, 100f, 130f }) Enemy("transfer hall", -38.8f, F, z);
        Sign3(t, "TRANSFER   ->   EXIT A2", new(-41.86f, -1.2f, 50), 90, .3f);
        // Clutter along alternating walls; a lane X -40.4..-37.6 stays clear.
        Buggy(t, "transfer hall", -41.2f, 54);
        Pallet(t, "transfer hall", -41.3f, 33, true, 2, .3f);
        CrateStack(t, "transfer hall", -41.4f, 72, .3f);
        var cage = P(t, "Toppled roll cage", -41.2f, F + .35f, 90, 1.1f, .7f, 1.6f, steel); cage.transform.localRotation = Quaternion.Euler(0, 8, 0);
        P(t, "Roll cage bars", -41.2f, F + .71f, 90, 1.0f, .04f, 1.5f, darkTile, false, 8);
        Pallet(t, "transfer hall", -41.3f, 117, true, 1, .4f);
        P(t, "Mop bucket", -36.6f, F + .2f, 35, .45f, .4f, .45f, yellow);
        P(t, "Toppled bin", -36.7f, F + .25f, 37, .5f, .5f, .8f, steel, true, 0, 90);
        Pallet(t, "transfer hall", -36.8f, 62, true, 3, .3f);
        P(t, "Cable drum", -36.9f, F + .55f, 80, .7f, 1.1f, 1.1f, wood);
        CrateStack(t, "transfer hall", -36.8f, 114, .35f);

        var portal = RoomV3(t, "Tiled service portal", -36, 18, -16, 34, F, HallClear, tile, floorTile, null, Solid, Solid, Solid);
        var mural = Group(portal, "Duck mural"); mural.localPosition = new(-26, -1.6f, 18.14f);
        Box(mural, "Mural panel", Vector3.zero, new(6f, 1.9f, .03f), band, false);
        int layer = 0;
        foreach (var (x, y2, w, h) in new[] { (0f, -.25f, 2.4f, .9f), (.9f, .35f, .8f, .8f), (1.45f, .3f, .5f, .2f), (-1.2f, -.05f, .6f, .4f) })
            Box(mural, "Mural duck", new(x, y2, .025f + layer++ * .005f), new(w, h, .02f), x == 1.45f ? zoneOrange : yellow, false);
        P(portal, "Rolled-down street shutter", -16.15f, F + 1.15f, 26, .12f, 2.3f, 4.5f, steel);
        for (int r = 0; r < 7; r++) P(portal, "Shutter rib", -16.24f, F + .2f + r * .32f, 26, .03f, .05f, 4.4f, darkTile, false);
        Sign3(portal, "EXIT A2   CLOSED", new(-16.25f, -1.0f, 26), -90, .32f);
        ExitA2Gates(portal);   // batch 6: a real gate line and service kiosk (RailPassengerStationV3Rooms.ExitA2Gates)

        var lost = RoomV3(t, "Lost and found", -36, 36, -29, 52, F, HallClear, tile, vinyl, null, Solid, Solid, Solid);
        for (int i = 0; i < 3; i++) { P(lost, "Lost property shelf", -29.6f, F + .6f, 39 + i * 4.5f, .7f, 1.2f, 3f, steel); TopLoot("lost and found", new(-29.6f, F + 1.2f, 38.2f + i * 4.5f), .7f); TopLoot("lost and found", new(-29.6f, F + 1.2f, 39.8f + i * 4.5f), .7f); }
        P(lost, "Umbrella bin", -30.6f, F + .3f, 37, .4f, .6f, .4f, steel);
        for (int i = 0; i < 5; i++) P(lost, "Umbrella", -30.6f + (i % 3 - 1) * .08f, F + .75f, 37 + (i % 2) * .08f, .05f, .9f, .05f, i % 2 == 0 ? zoneBlue : wreckRed, false, i * 30, 8 - i * 4);
        P(lost, "Suitcase", -35.5f, F + .35f, 49, .3f, .7f, .5f, wreckRed);
        P(lost, "Suitcase", -35.5f, F + .3f, 49.7f, .28f, .6f, .45f, zoneBlue);
        P(lost, "Suitcase (lying)", -35.3f, F + .12f, 48.2f, .5f, .24f, .7f, darkTile);
        var bike = Group(lost, "Abandoned bicycle"); bike.localPosition = new(-35.6f, F, 38.6f);
        Box(bike, "Bike wheel", new(0, .35f, -.55f), new(.04f, .7f, .7f), darkTile, false); Box(bike, "Bike wheel", new(0, .35f, .55f), new(.04f, .7f, .7f), darkTile, false);
        Box(bike, "Bike frame", new(0, .55f, 0), new(.05f, .05f, 1.0f), wreckRed); Box(bike, "Bike seat post", new(0, .75f, -.2f), new(.035f, .45f, .035f), wreckRed, false);
        Box(bike, "Bike bars", new(0, .95f, .45f), new(.5f, .04f, .04f), steel, false);
        var duck = Group(lost, "Giant stuffed duck"); duck.localPosition = new(-32.6f, F, 50.6f); duck.localRotation = Quaternion.Euler(0, 200, 0);
        Box(duck, "Duck body", new(0, .45f, 0), new(1.1f, .9f, 1.4f), yellow); Box(duck, "Duck head", new(0, 1.15f, .45f), new(.65f, .6f, .6f), yellow);
        Box(duck, "Duck beak", new(0, 1.08f, .85f), new(.4f, .14f, .3f), zoneOrange); Box(duck, "Duck tail", new(0, .72f, -.75f), new(.5f, .3f, .3f), yellow);
        TopLoot("lost and found", new(-32.6f, F + .9f, 50.4f), .9f);
        Tube(lost, new(-32.5f, F + HallClear - .08f, 44), true, 0);
        FloorLoot("lost and found", -35.5f, 36.5f, -30.5f, 51.5f, F, .6f, 2.4f, .5f);
        P(lost, "Sorting table", -33.0f, F + .4f, 43f, 2.0f, .8f, .8f, wood);
        foreach (var (dx, m) in new[] { (-.5f, wreckRed), (.05f, zoneBlue) }) P(lost, "Unclaimed bag", -33.0f + dx, F + .92f, 43f, .45f, .24f, .32f, m, true, dx * 40 + 8);
        TopLoot("lost and found", new(-32.3f, F + .8f, 43.1f), .7f);
        var trolley = Group(lost, "Luggage trolley"); trolley.localPosition = new(-33.4f, F, 46.6f);
        Box(trolley, "Trolley base", new(0, .12f, 0), new(.7f, .06f, 1.0f), steel); Box(trolley, "Trolley handle", new(0, .7f, -.48f), new(.7f, 1.1f, .04f), steel, false);
        Box(trolley, "Trolley suitcase", new(0, .4f, .05f), new(.5f, .5f, .7f), zoneGreen);
        Enemy("lost and found", -31.6f, F, 40);

        // Shop band behind Platform 1 (east walls are the platform shopfront).
        Toilets(t);   // men's and women's (RailPassengerStationV3Props.cs)

        var waiting = RoomV3(t, "Waiting room", -36, 64, -25, 74, F, pc, tile, terrazzo, null, null, null, Solid);
        Wainscot(waiting, -36, 64, -25, 74, F, 1.15f, wood, Solid, null, Solid, Solid);
        for (int i = 0; i < 3; i++)
        {
            float z = 66 + i * 2.8f;
            P(waiting, "Joined seat row", -31, F + .22f, z, 4f, .45f, .6f, wood);
            P(waiting, "Seat backs", -31, F + .65f, z - .27f, 3.96f, .45f, .08f, steel);
            for (int s = 0; s < 4; s++) P(waiting, "Seat armrest", -32.9f + s * 1.25f, F + .55f, z, .05f, .2f, .5f, steel, false);
            TopLoot("waiting room", new(-32.2f, F + .45f, z + .05f), .2f); TopLoot("waiting room", new(-29.8f, F + .45f, z + .05f), .2f);
        }
        for (int i = 0; i < 3; i++) { P(waiting, "Vending machine", -35.5f, F + .9f, 65.6f + i * 1.15f, .8f, 1.8f, 1.05f, steel); GlowBox(waiting, "Vending machine front", new(-35.08f, F + 1.15f, 65.6f + i * 1.15f), new(.03f, 1.1f, .8f), glowScreen); }
        P(waiting, "Stopped wall clock", -30, -1.0f, 73.8f, .6f, .6f, .05f, tile, false);
        P(waiting, "Clock hand", -30, -.92f, 73.765f, .03f, .22f, .02f, darkTile, false);
        P(waiting, "Clock hand", -30.08f, -1.04f, 73.76f, .16f, .03f, .02f, darkTile, false, 0, 30);
        GlowBox(waiting, "Timetable board", new(-27.2f, -1.15f, 73.8f), new(1.8f, .8f, .03f), glowScreen);
        P(waiting, "CRT TV on bracket", -25.6f, -1.0f, 64.45f, .6f, .5f, .5f, darkTile, false, 25);
        for (int i = 0; i < 4; i++) P(waiting, "Spilled newspaper", -33.5f + i * 1.9f, F + .006f, 72.6f - (i % 2) * .9f, .45f, .01f, .6f, tile, false, i * 37);
        Tube(waiting, new(-30.5f, F + pc - .08f, 69), false, 0);
        FloorLoot("waiting room", -34.5f, 64.5f, -25.5f, 73.5f, F, .2f, 2.4f, .5f);
        Enemy("waiting room", -27.5f, F, 70);

        // Justin, Nov 26 batch 4: lockers all around the walls, plus two low back-to-back islands.
        var lockers = RoomV3(t, "Coin lockers", -36, 74, -25, 84, F, pc, whitePanel, terrazzo, null, null, null, Solid);
        LockerBank(lockers, new(-35.57f, F, 79f), 9.3f, true, 1, 1.8f, 3);                 // west wall
        LockerBank(lockers, new(-30.35f, F, 74.43f), 9.8f, false, 1, 1.8f, 3);           // south wall
        LockerBank(lockers, new(-31.1f, F, 83.57f), 8.3f, false, -1, 1.8f, 3);           // north wall (change machine at the east end)
        foreach (var (z0, z1) in new[] { (74.8f, 76.8f), (81.2f, 83.2f) }) LockerBank(lockers, new(-25.43f, F, (z0 + z1) / 2), z1 - z0, true, -1, 1.8f, 3);   // beside the platform doorway
        foreach (float x in new[] { -31.9f, -28.9f })
            foreach (float face in new[] { -1f, 1f })
            {
                LockerBank(lockers, new(x + face * .3f, F, 79f), 4f, true, face, 1.1f, 2);
                if (face > 0) for (float zz = 77.6f; zz <= 80.5f; zz += 1.4f) TopLoot("coin lockers", new(x, F + 1.1f, zz), .5f);
            }
        P(lockers, "Open locker door", -35.0f, F + 1.05f, 76.1f, .03f, .5f, .45f, steel, false, 70);
        P(lockers, "Open locker door", -28.3f, F + .45f, 78.6f, .03f, .45f, .45f, steel, false, 110);
        P(lockers, "Change machine", -26.0f, F + .75f, 83.5f, .7f, 1.5f, .5f, steel);
        GlowBox(lockers, "Change machine display", new(-26.0f, F + 1.15f, 83.23f), new(.4f, .18f, .03f), glowScreen);
        Tube(lockers, new(-28, F + pc - .08f, 79), false, 2);
        PL(lockers, "Baked locker light", new(-33, F + 2f, 79), Color.Lerp(Cool, zoneTint, .5f), 6, 6);
        FloorLoot("coin lockers", -34.8f, 74.5f, -25.5f, 83.5f, F, .45f, 2.4f, .5f);
        Enemy("coin lockers", -27.5f, F, 76);

        var konbini = RoomV3(t, "Konbini", -36, 84, -25, 102, F, pc, whitePanel, vinyl, null, null, null, Solid);
        Wainscot(konbini, -36, 84, -25, 102, F, .3f, zoneGreen, new[] { 99f, 101 }, null, null, Solid);
        // Batch 5: real open shelf units in shorter, varied runs (Justin: three identical long blocks felt samey),
        // mostly empty, with coal/gold spots on the shelf boards.
        Material[] goods = { yellow, wreckRed, zoneGreen, zoneBlue, zoneOrange };
        int g = 0;
        ShelfUnit(konbini, new(-32.6f, F, 88.1f), 4.2f, 0, true, .45f, goods, ref g);
        ShelfUnit(konbini, new(-29.6f, F, 86.5f), 3.0f, 0, true, .45f, goods, ref g);
        ShelfUnit(konbini, new(-32.5f, F, 93.0f), 3.8f, 90, true, .45f, goods, ref g);
        ShelfUnit(konbini, new(-29.4f, F, 96.5f), 3.0f, 0, true, .45f, goods, ref g);
        ShelfUnit(konbini, new(-35.645f, F, 94.5f), 6.0f, 0, false, .45f, goods, ref g);   // along the west wall, facing the aisle
        ShelfUnit(konbini, new(-32.4f, F, 97.6f), 2.4f, 25, true, .5f, goods, ref g);      // knocked askew in the rush
        for (int i = 0; i < 4; i++) { P(konbini, "Drinks fridge", -35.6f, F + 1f, 86 + i * 1.3f, .7f, 2f, 1.2f, steel); GlowBox(konbini, "Fridge glow", new(-35.23f, F + 1.1f, 86 + i * 1.3f), new(.03f, 1.6f, 1f), glowScreen); }
        P(konbini, "Konbini counter", -26.2f, F + .5f, 100.2f, 1f, 1f, 3f, wood);
        P(konbini, "Register", -26.3f, F + 1.12f, 99.2f, .45f, .25f, .4f, darkTile);
        P(konbini, "Hot food warmer", -26.3f, F + 1.2f, 101.0f, .5f, .4f, .6f, steel);
        GlowBox(konbini, "Warmer glass", new(-26.04f, F + 1.2f, 101.0f), new(.02f, .3f, .55f), glowAmber);
        P(konbini, "Onigiri chiller", -31.5f, F + .5f, 101.5f, 3f, 1f, .7f, steel);
        GlowBox(konbini, "Chiller glow", new(-31.5f, F + 1.01f, 101.5f), new(2.8f, .02f, .6f), glowScreen);
        P(konbini, "Magazine rack", -34.6f, F + .65f, 101.6f, 1.4f, 1.3f, .45f, steel);
        P(konbini, "ATM", -35.6f, F + .8f, 84.6f, .7f, 1.6f, .7f, steel);
        GlowBox(konbini, "ATM screen", new(-35.24f, F + 1.15f, 84.6f), new(.02f, .3f, .4f), glowScreen);
        var fallen = new[] { (-31.2f, 89.6f), (-28.2f, 90.2f), (-31.0f, 94.6f), (-28.3f, 94.3f), (-30.8f, 99.6f) };
        for (int i = 0; i < fallen.Length; i++) P(konbini, "Fallen stock", fallen[i].Item1, F + .1f, fallen[i].Item2, .3f, .2f, .25f, goods[i], true, i * 47);
        TopLoot("konbini", new(-26.2f, F + 1f, 100.2f), .2f); TopLoot("konbini", new(-31.5f, F + 1.02f, 101.5f), .3f);
        SignBoard(konbini, "KONBINI  24", new(-25.15f, -1.0f, 93), 90, .26f, ' ', F + pc);   // hangs in the wide shop doorway
        Tube(konbini, new(-31.8f, F + pc - .08f, 89), false, 1); Tube(konbini, new(-31.8f, F + pc - .08f, 97), false, 0);
        FloorLoot("konbini", -35f, 84.4f, -25.6f, 101.6f, F, .15f, 2.5f, .5f);
        Enemy("konbini", -31.1f, F, 86.2f); Enemy("konbini back", -29.3f, F, 100.6f);

        var staff = RoomV3(t, "Staff room", -36, 102, -25, 110, F, pc, tile, vinyl, null, null, null, Solid);
        Wainscot(staff, -36, 102, -25, 110, F, 1.1f, paint, new[] { 106f, 108 }, null, null, Solid);
        P(staff, "Staff table", -30.5f, F + .38f, 106, 3f, .76f, 1.4f, wood);
        foreach (float x in new[] { -31.6f, -29.4f }) foreach (float dz in new[] { -1.05f, 1.05f }) P(staff, "Staff chair", x, F + .23f, 106 + dz, .45f, .45f, .45f, steel);
        P(staff, "Staff lockers", -35.6f, F + .9f, 104, .6f, 1.8f, 3f, steel);
        P(staff, "Kitchen counter", -31.5f, F + .45f, 109.6f, 3f, .9f, .6f, wood);
        P(staff, "Kettle", -32.4f, F + 1.02f, 109.6f, .18f, .24f, .18f, steel);
        P(staff, "Microwave", -31.0f, F + 1.07f, 109.6f, .5f, .32f, .35f, tile);
        P(staff, "Noticeboard", -34.8f, -1.3f, 109.81f, 1.4f, .9f, .03f, wood, false);   // north wall (the west wall has a doorway here)
        foreach (float dx in new[] { -.4f, .1f, .45f }) P(staff, "Pinned notice", -34.8f + dx, -1.25f + dx * .3f, 109.789f, .2f, .25f, .01f, tile, false);
        P(staff, "Rota whiteboard", -28f, -1.2f, 109.8f, 1.6f, .9f, .03f, tile, false);
        P(staff, "Sofa seat", -27.6f, F + .22f, 103.0f, 2f, .45f, .8f, zoneBlue);
        P(staff, "Sofa back", -27.6f, F + .6f, 102.65f, 1.96f, .5f, .2f, zoneBlue);
        TopLoot("staff room", new(-31.3f, F + .76f, 106), .6f); TopLoot("staff room", new(-29.7f, F + .76f, 106), .6f); TopLoot("staff room", new(-30.4f, F + .9f, 109.6f), .6f);
        Tube(staff, new(-30.5f, F + pc - .08f, 106), false, 0);
        FloorLoot("staff room", -35f, 102.4f, -25.6f, 109.6f, F, .5f, 2.4f, .5f);
        Enemy("staff room", -27f, F, 108.6f);

        // Cable rooms sit right after the staff room so the transfer hall ends here.
        Zone(zoneTeal);
        // Justin, Nov 26 batch 4: both cable rooms packed with electrical plant (switchgear, transformers,
        // breaker panels, relay racks, trays and conduit drops), keeping a lane from each door.
        var vault = RoomV3(t, "Cable vault", -36, 118, -30, 134, F, HallClear, darkTile, concrete, null, new[] { 124f, 127 }, Solid, Solid);
        Material[] lamps = { glowRed, glowScreen, glowAmber };
        for (int i = 0; i < 4; i++) Cabinet(vault, "Switchgear cabinet", new(-35.4f, F, 118.8f + i * 1.1f), 90, 1.0f, 1.85f, .8f, lamps[i % 3]);   // south, west wall
        P(vault, "Cable drum", -32.9f, F + .6f, 119.4f, 1.2f, 1.2f, 1.2f, wood); TopLoot("cable vault", new(-32.9f, F + 1.2f, 119.4f), 1f);
        Transformer(vault, new(-32.8f, F, 122.0f), 0);
        foreach (float z in new[] { 119.0f, 120.6f, 122.2f, 128.3f, 129.9f }) BreakerPanel(vault, new(-30.205f, F + 1.45f, z), -90);
        P(vault, "Battery rack", -35.55f, F + .5f, 130.75f, .6f, 1f, 5.6f, steel);
        for (float z = 128.3f; z < 133.4f; z += .7f) P(vault, "Battery block", -35.55f, F + 1.11f, z, .5f, .22f, .5f, z % 1.4f < .7f ? darkTile : zoneOrange);
        for (int i = 0; i < 3; i++) Cabinet(vault, "Relay rack", new(-33.0f, F, 128.4f + i * 1.2f), 90, 1.1f, 1.9f, .7f, lamps[(i + 1) % 3]);
        P(vault, "Cable drum", -31.2f, F + .6f, 132.6f, 1.2f, 1.2f, 1.2f, wood); TopLoot("cable vault", new(-31.2f, F + 1.2f, 132.6f), 1f);
        foreach (float x in new[] { -35.55f, -30.45f }) P(vault, "Overhead cable tray", x, F + 2.06f, 126f, .45f, .08f, 15.5f, steel, false);
        for (int i = 0; i < 4; i++) PipeRun(vault, "Conduit drop", new[] { new Vector3(-35.4f, F + 1.85f, 118.8f + i * 1.1f), new Vector3(-35.4f, F + 2.02f, 118.8f + i * 1.1f) }, .04f, steel);
        Tube(vault, new(-33, F + HallClear - .08f, 129), true, 1);
        FloorLoot("cable vault", -35.5f, 118.5f, -30.5f, 133.5f, F, 1f, 2.4f, .5f);
        Enemy("cable vault", -32.5f, F, 132);

        var gallery = RoomV3(t, "Cable gallery", -30, 112, -17, 134, F, HallClear, darkTile, concrete, null, Solid, Solid, Solid);
        Wall(gallery, "Wall", true, -30, 112, 118, F, HallClear + .3f, darkTile);
        Stripe(gallery, true, -29.85f, 112, 118, F + 1.6f, Solid);
        for (float z = 113.5f; z < 134; z += 5)
        {
            GlowBox(gallery, "Gallery window (lit)", new(-17.14f, -1.6f, z), new(.03f, .9f, 1.5f), glowWindow);
            PL(gallery, "Baked window glow", new(-17.8f, -1.6f, z), Warm, 5, 5);
        }
        foreach (float y in new[] { -1.3f, -.95f })
        {
            P(gallery, "Cable tray", -29.6f, y, 123, .5f, .12f, 21, steel, false);
            P(gallery, "Cable tray", -17.4f, y - .3f, 123, .5f, .12f, 21, steel, false);
            for (int i = 0; i < 3; i++) P(gallery, "Cable bundle", -29.7f + i * .12f, y + .1f, 123, .08f, .08f, 20.8f, i == 1 ? zoneOrange : darkTile, false);
        }
        foreach (float z in new[] { 116f, 122f, 128f }) { P(gallery, "Cable cabinet", -23.5f, F + .7f, z, 2.2f, 1.4f, 1f, steel); TopLoot("cable gallery", new(-24.2f, F + 1.4f, z), 1f); TopLoot("cable gallery", new(-22.8f, F + 1.4f, z), 1f); }
        foreach (float z in new[] { 114.5f, 131.5f }) P(gallery, "Junction box", -29.8f, -1.8f, z, .25f, .6f, .5f, steel, false);
        // Two rows of rectifier cabinets facing the outer lanes, cross aisles at Z 119, 125 and 131.
        int lamp = 0;
        foreach (var (x, yaw) in new[] { (-26.8f, 90f), (-20.2f, -90f) })
            foreach (var (a, b) in new[] { (112.9f, 118.2f), (119.8f, 124.2f), (125.8f, 130.2f), (131.8f, 133.6f) })
                for (float z = a + .55f; z + .55f <= b + .01f; z += 1.15f)
                {
                    Cabinet(gallery, "Rectifier cabinet", new(x, F, z), yaw, 1.1f, 1.8f, 1.0f, lamps[lamp++ % 3]);
                    PipeRun(gallery, "Conduit to ceiling", new[] { new Vector3(x, F + 1.8f, z), new Vector3(x, F + 2.16f, z) }, .04f, steel);
                }
        foreach (float z in new[] { 113.1f, 132.8f }) Transformer(gallery, new(-23.5f, F, z), 0);
        foreach (float z in new[] { 116f, 121f, 126f, 131f }) BreakerPanel(gallery, new(-17.205f, F + .83f, z), -90);
        foreach (float z in new[] { 119f, 125f, 131f }) P(gallery, "Overhead cable tray", -23.5f, F + 2.04f, z, 11.7f, .08f, .45f, steel, false);
        Sign3(gallery, "CABLE GALLERY  B", new(-29.8f, -2.0f, 121), 90, .28f);
        FloorLoot("cable gallery", -29.5f, 112.5f, -17.5f, 133.5f, F, .9f, 3f, .6f);
        Enemy("cable gallery south", -18.4f, F, 114.5f); Enemy("cable gallery north", -27, F, 131); Enemy("cable gallery middle", -20, F, 125);
    }

    // ---------------- east maintenance side ----------------
    static void EastRooms(Transform t)
    {
        Zone(zoneOrange);
        float hc = HallClear;
        RoomV3(t, "Maintenance hall", 13, 40, 17, 122, F, hc, paint, concrete, new[] { 40f, 44, 46, 50, WreckSideDoorZ - .85f, WreckSideDoorZ + .85f }, new[] { 61f, 63, 75, 77, 89, 91, 103, 105, 115, 118 }, Solid, Solid);
        int k = 0;
        for (float z = 44; z <= 120; z += 10, k++) Tube(t, new(15, F + hc - .08f, z), true, k % 3 == 1 ? 2 : k % 4 == 2 ? 1 : 0, k % 2 == 0);
        FloorLoot("maintenance hall", 15, 42, 15, 120, F, .6f, 8f, 0);
        foreach (float z in new[] { 52f, 72f, 100f, 120f }) Enemy("maintenance hall", 15, F, z);
        // Clutter on alternating sides; a lane X 14..16 stays clear (the buggy leaves X 13.1..15.6).
        Pallet(t, "maintenance hall", 13.6f, 56, true, 2, .6f);
        CrateStack(t, "maintenance hall", 13.5f, 70, .6f);
        P(t, "Oil drum", 13.5f, F + .45f, 99, .6f, .9f, .6f, wreckRed);
        P(t, "Oil drum (tipped)", 13.6f, F + .3f, 100.2f, .6f, .6f, .9f, wreckRed, true, 0, 90);
        Pallet(t, "maintenance hall", 13.6f, 110, true, 1, .6f);
        foreach (float dz in new[] { 0f, .45f, .9f }) P(t, "Gas cylinder", 16.6f, F + .7f, 52 + dz, .35f, 1.4f, .35f, dz == .45f ? yellow : steel);
        CrateStack(t, "maintenance hall", 16.5f, 67, .6f);
        Buggy(t, "maintenance hall", 16.2f, 96);
        Pallet(t, "maintenance hall", 16.4f, 82, true, 2, .6f);

        RoomV3(t, "South cross passage", 4, 46, 13, 50, F, hc, darkTile, darkTile, null, null, null, new[] { 8.05f, 9.55f });
        RoomV3(t, "Wreck side door passage", 9.7f, WreckSideDoorZ - .85f, 13, WreckSideDoorZ + .85f, F, hc, darkTile, darkTile, null, null, Solid, Solid);

        var gallery = RoomV3(t, "Fan gallery", 8, 12, 13, 44, F, hc, darkTile, concrete, Solid, new[] { 28f, 31, 40, 44 }, Solid, Solid);
        foreach (float z in new[] { 18f, 28f, 38f })
        {
            P(gallery, "Fan duct grille", 8.14f, -1.6f, z, .03f, 1.8f, 2.4f, steel, false);
            for (int r = 0; r < 5; r++) P(gallery, "Grille slat", 8.17f, -2.3f + r * .35f, z, .03f, .05f, 2.3f, darkTile, false);
            Sign3(gallery, "FAN " + (z == 18 ? 1 : z == 28 ? 2 : 3), new(8.32f, -.75f, z), 90, .25f);
        }
        P(gallery, "Spare fan blade", 12.6f, F + .6f, 23, .1f, 1.2f, 2.8f, concrete, true, 0, -8);
        P(gallery, "Ladder rail", 12.75f, -1.9f, 36.3f, .05f, 2.1f, .05f, steel, false); P(gallery, "Ladder rail", 12.75f, -1.9f, 36.8f, .05f, 2.1f, .05f, steel, false);
        for (int r = 0; r < 6; r++) P(gallery, "Ladder rung", 12.75f, -2.8f + r * .35f, 36.55f, .04f, .04f, .5f, steel, false);
        Tube(gallery, new(10.5f, F + hc - .08f, 20), true, 0); Tube(gallery, new(10.5f, F + hc - .08f, 34), true, 1);
        FloorLoot("fan gallery", 10.5f, 13, 10.5f, 43, F, .7f, 4f, 0);
        Enemy("fan gallery south", 10.5f, F, 15); Enemy("fan gallery north", 10.5f, F, 33);

        var control = RoomV3(t, "Fan control room", 13, 22, 25, 36, F, hc, paint, concrete, null, Solid, Solid, Solid);
        P(control, "Control desk", 21, F + .45f, 34.9f, 6f, .9f, 1f, steel);
        GlowBox(control, "Control panel lamps", new(21, F + .92f, 34.6f), new(5f, .03f, .3f), glowRed);
        for (int i = 0; i < 3; i++) P(control, "Switch cabinet", 24.6f, F + .9f, 24.5f + i * 2.2f, .6f, 1.8f, 1.6f, steel);
        for (int i = 0; i < 4; i++) P(control, "Dial gauge", 18.5f + i * 1.4f, -1.3f, 35.85f, .4f, .4f, .04f, tile, false);
        P(control, "Valve wheel", 24.84f, -1.6f, 31.5f, .05f, .6f, .6f, wreckRed, false, 0, 45);
        P(control, "Operator chair", 21, F + .25f, 33.6f, .5f, .5f, .5f, darkTile);
        // Central fan console island so the room is not empty in the middle (batch 4).
        P(control, "Fan console", 19f, F + .5f, 28.5f, 2.4f, 1.0f, 1.0f, steel);
        GlowBox(control, "Fan console screen", new(19f, F + 1.005f, 28.5f), new(2.0f, .01f, .6f), glowScreen);
        TopLoot("fan control", new(18.1f, F + 1.0f, 28.5f), 1f);
        TopLoot("fan control", new(19.5f, F + .9f, 35.1f), 1f); TopLoot("fan control", new(22.5f, F + .9f, 35.1f), 1f);
        PL(control, "Baked control light", new(19, F + 1.9f, 29), Warm, 8, 7);
        FloorLoot("fan control", 13.5f, 22.5f, 23.8f, 33.8f, F, .9f, 2.5f, .5f);
        Enemy("fan control", 17, F, 26);

        foreach (var (name, z0) in new[] { ("Crew lockers", 56f), ("Signal room", 70f), ("Canteen", 84f), ("Electrical room", 98f) })
        {
            bool signal = name == "Signal room";
            bool canteen = name == "Canteen";
            var room = RoomV3(t, name, 17, z0, 28, z0 + 12, F, hc, signal ? whitePanel : canteen ? tile : paint, signal ? vinyl : canteen ? checker : concrete, null, signal ? new[] { 75f, 77 } : Solid, Solid, Solid);
            if (canteen) Wainscot(room, 17, z0, 28, z0 + 12, F, 1.3f, ceramic, null, Solid, Solid, Solid);
            switch (name)
            {
                case "Crew lockers":
                    // Lockers all around (Justin, Nov 26 batch 4) with a back-to-back island and benches either side.
                    LockerBank(room, new(27.57f, F, z0 + 6), 11.3f, true, -1, 1.8f, 1);
                    LockerBank(room, new(22.25f, F, z0 + .43f), 9.8f, false, 1, 1.8f, 1);
                    LockerBank(room, new(22.25f, F, z0 + 11.57f), 9.8f, false, -1, 1.8f, 1);
                    foreach (var (a, b) in new[] { (z0 + 1.0f, z0 + 4.8f), (z0 + 7.2f, z0 + 11.0f) }) LockerBank(room, new(17.43f, F, (a + b) / 2), b - a, true, 1, 1.8f, 1);   // either side of the hall doorway
                    foreach (float face in new[] { -1f, 1f }) LockerBank(room, new(22.5f + face * .3f, F, z0 + 6), 5f, true, face, 1.8f, 1);
                    foreach (float bx in new[] { 20.6f, 24.4f })
                    {
                        P(room, "Changing bench", bx, F + .22f, z0 + 6, .4f, .45f, 4.6f, wood);
                        for (float zz = z0 + 4.4f; zz <= z0 + 7.7f; zz += 1.6f) TopLoot("crew lockers", new(bx, F + .45f, zz), .8f);
                    }
                    for (int i = 0; i < 2; i++) P(room, "Hi-vis vest on bench", 20.6f, F + .456f, z0 + 4.2f + i * 3.3f, .36f, .02f, .5f, i == 0 ? yellow : zoneOrange, false, 12);
                    P(room, "Hard hat", 24.4f, F + .544f, z0 + 3.9f, .3f, .2f, .35f, yellow);
                    foreach (float dz in new[] { 0f, .3f }) P(room, "Work boot", 25.6f, F + .1f, z0 + 9.3f + dz, .3f, .2f, .14f, darkTile);
                    break;
                case "Signal room":
                    P(room, "Signal desk", 22.5f, F + .45f, z0 + 11.3f, 7f, .9f, 1f, steel);
                    GlowBox(room, "Signal diagram", new(22.5f, -1.2f, z0 + 11.84f), new(6f, .9f, .03f), glowScreen);
                    for (int i = 0; i < 6; i++) P(room, "Signal lever", 20.4f + i * .5f, F + 1.15f, z0 + 10.95f, .06f, .55f, .06f, i % 3 == 0 ? wreckRed : steel, false, 0, i % 2 == 0 ? 12 : -10);
                    P(room, "Desk phone", 25.2f, F + .96f, z0 + 11.1f, .25f, .1f, .2f, darkTile);
                    foreach (float x in new[] { 21f, 24f }) P(room, "Office chair", x, F + .25f, z0 + 9.9f, .5f, .5f, .5f, darkTile, true, x * 9);
                    for (float x = 20.2f; x <= 24.6f; x += 2.2f) TopLoot("signal room", new(x, F + .9f, z0 + 11.45f), .9f);
                    foreach (var (dz, yaw) in new[] { (-.35f, 180f), (.35f, 0f) }) foreach (float x in new[] { 21.95f, 23.05f }) Cabinet(room, "Relay rack", new(x, F, z0 + 5f + dz), yaw, 1.1f, 1.9f, .7f, x > 22.5f ? glowRed : glowScreen);   // back-to-back island
                    break;
                case "Canteen":
                    foreach (float zz in new[] { z0 + 3.5f, z0 + 8.5f })
                    {
                        P(room, "Canteen table", 22.5f, F + .38f, zz, 4f, .76f, 1.2f, wood);
                        foreach (float x in new[] { 21f, 24f }) foreach (float dz in new[] { -.95f, .95f }) P(room, "Canteen chair", x, F + .23f, zz + dz, .42f, .45f, .42f, steel);
                        TopLoot("canteen", new(21.5f, F + .76f, zz), .7f); TopLoot("canteen", new(23.5f, F + .76f, zz), .7f);
                    }
                    P(room, "Serving counter", 27.6f, F + .45f, z0 + 6, .6f, .9f, 8f, concrete);
                    P(room, "Canteen fridge", 27.5f, F + .95f, z0 + 11.2f, .8f, 1.9f, .8f, tile);
                    P(room, "Canteen vending machine", 27.5f, F + .9f, z0 + .9f, .8f, 1.8f, 1f, steel);
                    GlowBox(room, "Canteen vending front", new(27.08f, F + 1.15f, z0 + .9f), new(.03f, 1.1f, .8f), glowScreen);
                    break;
                default:
                    for (int i = 0; i < 4; i++)
                    {
                        P(room, "Switchgear", 27.5f, F + 1f, z0 + 2 + i * 2.6f, .8f, 2f, 2f, steel);
                        P(room, "Switchgear handle", 27.08f, F + 1.1f, z0 + 2 + i * 2.6f, .04f, .25f, .08f, darkTile, false);
                    }
                    GlowBox(room, "Breaker lamps", new(27.08f, -1.4f, z0 + 6), new(.03f, .1f, 9f), glowRed);
                    P(room, "Fuse board", 21.5f, -1.4f, z0 + .16f, 1.6f, 1f, .06f, steel, false);
                    Sign3(room, "DANGER   HIGH VOLTAGE", new(21.5f, -.8f, z0 + 11.84f), 180, .28f);
                    Transformer(room, new(23.2f, F, z0 + 6.5f), 90);
                    foreach (float bz in new[] { z0 + 2.2f, z0 + 9.8f }) Cabinet(room, "Breaker cabinet", new(22.2f, F, bz), 0, 1.6f, 1.6f, .6f, glowRed);
                    break;
            }
            Tube(room, new(22.5f, F + hc - .08f, z0 + 6), false, name == "Canteen" ? 1 : name == "Electrical room" ? 2 : 0, name != "Electrical room");
            if (name == "Electrical room") RedLamp(room, new(17.14f, -1f, z0 + 9), 90);
            FloorLoot(name.ToLower(), 17.5f, z0 + .5f, 26.6f, z0 + 11.5f, F, name == "Electrical room" ? 1f : .7f, 2.6f, .5f);
            Enemy(name.ToLower(), name == "Crew lockers" ? 19.1f : 20.5f, F, z0 + 6);
        }
        RoomV3(t, "Parts passage", 28, 75, 30, 77, F, hc, paint, concrete, null, null, Solid, Solid);
        var parts = RoomV3(t, "Parts store", 30, 62, 42, 92, F, hc, paint, concrete, new[] { 75f, 77 }, Solid, Solid, Solid);
        // Split into a front store and a back store with a doorway (batch 4: rooms felt too large).
        Wall(parts, "Partition wall", false, 78.6f, 30, 42, F, hc + .3f, paint, 35, 37);
        Stripe(parts, false, 78.45f, 30.15f, 41.85f, F + 1.6f, new[] { 35f, 37 });
        Stripe(parts, false, 78.75f, 30.15f, 41.85f, F + 1.6f, new[] { 35f, 37 });
        foreach (float z in new[] { 65f, 70f, 82f, 87f })
        {
            P(parts, "Parts rack", 36, F + .75f, z, 8f, 1.5f, .8f, steel);
            for (int i = 0; i < 3; i++) P(parts, "Spare wheel", 33.5f + i * 2.5f, F + .45f, z + .45f, .7f, .7f, .12f, darkTile, false);
            P(parts, "Coil spring", 38.8f, F + 1.0f, z - .3f, .3f, .4f, .3f, zoneOrange, false);
            for (float x = 33f; x <= 39f; x += 2f) TopLoot("parts store", new(x, F + 1.5f, z), 1f);
        }
        foreach (float dz in new[] { 0f, .25f }) P(parts, "Spare rail", 36, F + .08f, 91.3f + dz, 9f, .16f, .12f, steel);
        var lift = Group(parts, "Forklift"); lift.localPosition = new(38.5f, F, 76); lift.localRotation = Quaternion.Euler(0, -90, 0);
        Box(lift, "Forklift body", new(0, .65f, -.2f), new(1.2f, 1.0f, 1.8f), yellow);
        Box(lift, "Forklift seat", new(0, 1.3f, -.35f), new(.6f, .35f, .5f), darkTile);
        Box(lift, "Forklift overhead guard", new(0, 2.1f, -.2f), new(1.15f, .06f, 1.2f), steel, false);
        foreach (float x in new[] { -.5f, .5f }) { Box(lift, "Guard post", new(x, 1.6f, .35f), new(.06f, 1.1f, .06f), steel, false); Box(lift, "Mast", new(x * .8f, 1.2f, .8f), new(.1f, 2.2f, .1f), steel); Box(lift, "Fork", new(x * .6f, .12f, 1.35f), new(.12f, .06f, 1.0f), steel); }
        foreach (float x in new[] { -.62f, .62f }) foreach (float z in new[] { -.75f, .45f }) Box(lift, "Forklift tyre", new(x, .28f, z), new(.2f, .56f, .56f), darkTile);
        TopLoot("parts store", new(38.5f, F + 1.15f, 76.2f), 1f);
        Tube(parts, new(36, F + hc - .08f, 76), false, 1); Tube(parts, new(36, F + hc - .08f, 66), false, 2, false);
        PL(parts, "Baked parts light", new(36, F + 1.9f, 86), Color.Lerp(Cool, zoneTint, .5f), 6, 7);
        FloorLoot("parts store", 30.5f, 62.5f, 41.5f, 91.5f, F, 1f, 3f, .5f);
        Enemy("parts store south", 36, F, 67.5f); Enemy("parts store north", 36, F, 89.5f);

        // Pump room moved south beside the end of the maintenance hall.
        var pump = RoomV3(t, "Pump room", 17, 112, 35, 130, F, 3f, paint, concrete, null, Solid, Solid, Solid);
        Wall(pump, "Wall", true, 17, 122, 130, F, 3.3f, paint);
        Stripe(pump, true, 17.15f, 122, 130, F + 1.6f, Solid);
        for (int i = 0; i < 3; i++)
        {
            float x = 21 + i * 5f;
            P(pump, "Sump grille", x, F + .01f, 116.5f, 3.6f, .02f, 3f, darkTile, false);
            for (int r = 0; r < 6; r++) P(pump, "Grille bar", x - 1.5f + r * .6f, F + .025f, 116.5f, .06f, .03f, 3f, steel, false);
            P(pump, "Pump housing", x, F + 1f, 125.5f, 2.2f, 2f, 2.2f, steel);
            P(pump, "Pump motor", x, F + 2.25f, 125.5f, 1.2f, .5f, 1.4f, zoneOrange);
            P(pump, "Valve wheel", x + 1.2f, F + 1.3f, 125.5f, .05f, .55f, .55f, wreckRed, false, 0, 45);
            P(pump, "Pressure gauge", x - .5f, F + 1.6f, 124.36f, .3f, .3f, .04f, tile, false);
            TopLoot("pump room", new(x, F + 2.5f, 125.5f), 1f);
        }
        // Split into a sump room (south, off the hall) and a pump hall (north) with two doorways (batch 4).
        Wall(pump, "Partition wall", false, 121, 17, 35, F, 3.3f, paint, 19.5f, 21, 31, 32.5f);
        // The pump room is taller than the hall: close the band between the hall wall top and this ceiling.
        Wall(pump, "Wall above hall", true, 17, 112, 122, F + hc + .3f - .01f, 3.3f - hc - .3f + .01f, paint);   // starts exactly on the hall wall top
        Tube(pump, new(22, F + 2.92f, 119), false, 0); Tube(pump, new(30, F + 2.92f, 119), false, 1); Tube(pump, new(26, F + 2.92f, 128), false, 2);
        FloorLoot("pump room", 17.5f, 112.5f, 34.5f, 129.5f, F, 1f, 3f, .6f);
        Enemy("pump room west", 20, F, 119.8f); Enemy("pump room east", 32.5f, F, 119.8f); Enemy("pump room north", 26, F, 129);
    }

    // ---------------- King's Quack facades at both tunnel ends ----------------
    static void Facade(Transform t, bool south)
    {
        Zone(zoneYellow);
        float z = south ? 6 : 154, s = south ? -1 : 1;   // s points out of the block
        float face = z + s * .125f;
        Vector3 At(float x, float y, float d) => new(x, y, face + s * d);
        // Plaster cladding over the earth wall, olive base band, arch frame and keystone.
        foreach (var (x0, x1) in new[] { (-48f, -TunnelHalf - .2f), (TunnelHalf + .2f, 48f) })   // to the block edges (batch 5)
        {
            P(t, "Facade cladding", (x0 + x1) / 2, 4.5f, At(0, 0, .05f).z, x1 - x0, 9f, .1f, concrete);
            P(t, "Facade base band", (x0 + x1) / 2, .45f, At(0, 0, .12f).z, x1 - x0 - .02f, .9f, .06f, paint);
        }
        P(t, "Facade cladding over arch", 0, (TunnelRoof + .3f + 9) / 2, At(0, 0, .05f).z, TunnelHalf * 2 + .4f, 9 - TunnelRoof - .3f, .1f, concrete);
        foreach (float side in new[] { -1f, 1f })
        {
            P(t, "Arch pilaster", side * (TunnelHalf + .66f), 3.3f, At(0, 0, .3f).z, .9f, 6.6f, .5f, tile);
            var hx = P(t, "Arch haunch", side * (TunnelHalf - .25f), TunnelRoof + .05f, At(0, 0, .3f).z, .5f, .5f, .5f, tile);   // clear of the train body (2.33 m half width)
            hx.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
        P(t, "Arch head", 0, TunnelRoof + .7f, At(0, 0, .31f).z, TunnelHalf * 2 + 2.26f, .9f, .5f, tile);
        // Reveal lining from the arch front into the tunnel, hiding the tunnel's cut end and the gap to the pilasters.
        foreach (float side in new[] { -1f, 1f }) P(t, "Portal reveal", side * (TunnelHalf + .01f), (TunnelRoof - .03f - .5f) / 2, At(0, 0, .0525f).z, .48f, TunnelRoof - .03f + .5f, .955f, tile);   // top meets the head underside
        P(t, "Portal reveal head", 0, TunnelRoof + .145f, At(0, 0, .0525f).z, TunnelHalf * 2 + .52f, .35f, .955f, tile);
        P(t, "Keystone", 0, TunnelRoof + .7f, At(0, 0, .55f).z, .7f, 1.1f, .2f, yellow);
        P(t, "Arch hazard band", 0, TunnelRoof + .2f, At(0, 0, .58f).z, TunnelHalf * 2, .12f, .05f, yellow, false);
        // Station name sign over the arch, lit by two cowl lamps.
        float signY = TunnelRoof + 3.65f;   // batch 6: above the arch pilasters (top 6.6) now the mesh-lettered board is ~10 m wide
        SignBoard(t, StationName, At(0, signY, .25f), south ? 180 : 0, .55f);
        foreach (float x in new[] { -3.2f, 3.2f })
        {
            P(t, "Sign lamp arm", x, signY + 1.05f, At(0, 0, .55f).z, .06f, .06f, .7f, steel, false);
            GlowBox(t, "Sign lamp", At(x, signY + .95f, .9f), new(.4f, .15f, .3f), glowAmber);
        }
        PL(t, "Baked sign light", At(0, signY, 2.2f), Warm, 14, 9);
        // Parapet railing along the top of the wall.
        for (float x = -47.5f; x <= 47.5f; x += 2.5f) P(t, "Parapet post", x, 9.5f, At(0, 0, .05f).z, .07f, 1f, .07f, steel, false);
        P(t, "Parapet rail", 0, 10f, At(0, 0, .05f).z, 95.5f, .08f, .08f, steel, false);
        // A row of city buildings on the roof behind the wall (batch 5: the roof replaces the old ledges).
        var specs = south
            ? new[] { (-36f, 10f, 9f), (-24.5f, 9f, 16f), (-13.5f, 9f, 11f), (14f, 10f, 13f), (26f, 9f, 20f), (37.5f, 8f, 10f) }
            : new[] { (-40f, 9f, 13f), (-29f, 10f, 17f), (-18f, 9f, 10f), (-8.5f, 8f, 15f), (4f, 9f, 12f), (15f, 9f, 18f), (26f, 10f, 9f), (37.5f, 8f, 14f) };   // west half filled in batch 5
        int n = 0;
        foreach (var (bx, bw, bh) in specs)
        {
            float bz = south ? 13f : 147f, bd = 9f;
            var mat = n % 2 == 0 ? tile : darkTile;
            P(t, "City building", bx, 9 + bh / 2, bz, bw, bh, bd, mat);
            P(t, "Roof parapet", bx, 9 + bh + .2f, bz, bw + .2f, .4f, bd + .2f, roof);
            float fz = bz + s * (bd / 2 + .03f);
            for (float wx = bx - bw / 2 + 1.2f; wx < bx + bw / 2 - .6f; wx += 2.2f)
                for (float wy = 10.6f; wy < 9 + bh - .8f; wy += 2.6f)
                {
                    int hash = Mathf.Abs((int)(wx * 13 + wy * 7 + n * 31));
                    if (hash % 9 == 0) GlowBox(t, "Lit window", new(wx, wy, fz), new(.9f, 1.2f, .04f), glowWindow);
                    else P(t, "Dark window", wx, wy, fz, .9f, 1.2f, .04f, roof, false);
                }
            n++;
        }
        // Street furniture on the flat seam in front of the wall.
        float sz = south ? 3.2f : 156.8f;
        foreach (float x in new[] { -7.5f, 7.5f })
        {
            P(t, "Lamp post", x, 2.2f, sz, .15f, 4.4f, .15f, steel);
            P(t, "Lamp head", x + (x < 0 ? .35f : -.35f), 4.35f, sz, .8f, .12f, .3f, steel, false);
            GlowBox(t, "Lamp glow", new(x + (x < 0 ? .5f : -.5f), 4.27f, sz), new(.35f, .04f, .22f), glowAmber);
        }
        PL(t, "Baked street lamp", new(-7f, 4f, sz), Warm, 10, 10);
        PL(t, "Baked street lamp", new(7f, 4f, sz), Warm, 10, 10);
        if (south)
        {
            var bus = Group(t, "Abandoned city bus"); bus.localPosition = new(-22, 0, 3.2f); bus.localRotation = Quaternion.Euler(0, 92, 0);
            Box(bus, "Bus body", new(0, 1.55f, 0), new(2.5f, 2.5f, 10f), tile);
            Box(bus, "Bus stripe", new(0, 1.3f, 0), new(2.52f, .25f, 10.02f), zoneBlue, false);
            Box(bus, "Bus windows", new(0, 2.15f, -.3f), new(2.53f, .8f, 8.5f), roof, false);
            foreach (float bz in new[] { -3.3f, 3.3f }) foreach (float bx2 in new[] { -1.15f, 1.15f }) Box(bus, "Bus wheel", new(bx2, .45f, bz), new(.35f, .9f, .9f), darkTile);
            P(t, "Pallet stack", 13, .5f, 2.6f, 1.2f, 1f, 1f, wood);
            P(t, "Dumpster", 18, .6f, 2.8f, 2f, 1.2f, 1.2f, paint);
        }
        else
        {
            P(t, "Pallet stack", 12, .5f, 157.2f, 1.2f, 1f, 1f, wood);
            P(t, "Crate", 14, .35f, 157.4f, .7f, .7f, .7f, wood, true, 20);
            P(t, "Dumpster", 18.5f, .6f, 157.2f, 2f, 1.2f, 1.2f, paint);
        }
    }
}
#endif
