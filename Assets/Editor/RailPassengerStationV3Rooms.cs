#if UNITY_EDITOR
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

    // Broken-down maintenance buggy (golf-cart style) with a flat front tyre and a tipped load.
    static void Buggy(Transform t, string area, float x, float z)
    {
        var g = Group(t, "Broken maintenance buggy"); g.localPosition = new(x, F, z); g.localRotation = Quaternion.Euler(0, 0, -2.5f);
        Box(g, "Buggy chassis", new(0, .45f, 0), new(1.2f, .35f, 2.5f), steel);
        Box(g, "Buggy cowl", new(0, .8f, .95f), new(1.15f, .5f, .55f), yellow);
        Box(g, "Buggy seat", new(0, .85f, .25f), new(1.05f, .25f, .55f), darkTile);
        Box(g, "Buggy seat back", new(0, 1.15f, -.02f), new(1.05f, .5f, .12f), darkTile);
        Box(g, "Buggy steering column", new(-.3f, 1.05f, .78f), new(.06f, .5f, .06f), steel, false);
        Box(g, "Buggy wheel", new(-.32f, 1.28f, .72f), new(.4f, .05f, .4f), darkTile, false);
        foreach (float px in new[] { -.55f, .55f }) foreach (float pz in new[] { -1.15f, 1.15f })
                Box(g, "Canopy post", new(px, 1.55f, pz), new(.05f, 1.6f, .05f), steel, false);
        Box(g, "Buggy canopy", new(0, 2.35f, 0), new(1.3f, .06f, 2.5f), yellow);
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
        P(portal, "Ticket booth", -31, F + .55f, 31.5f, 4f, 1.1f, 1.2f, wood);
        foreach (float x in new[] { -32.9f, -29.1f }) P(portal, "Booth window post", x, F + 1.75f, 31.0f, .08f, 1.3f, .08f, steel, false);
        P(portal, "Booth window frame", -31, F + 2.42f, 31.0f, 3.9f, .08f, .08f, steel, false);
        P(portal, "Booth counter shelf", -31, F + 1.12f, 30.8f, 3.8f, .04f, .35f, wood);
        for (int i = 0; i < 4; i++)
        {
            float x = -27.5f + i * 2.2f;
            P(portal, "Turnstile post", x, F + .5f, 28.5f, .3f, 1f, .5f, steel);
            P(portal, "Turnstile arm", x + .45f, F + .85f, 28.5f, .6f, .05f, .05f, steel, false, i * 40);
        }
        foreach (var (x, m) in new[] { (-33f, zoneBlue), (-30f, wreckRed), (-22f, zoneGreen), (-19f, yellow) })
            P(portal, "Faded poster", x, -1.35f, 33.84f, 1.1f, 1.5f, .02f, m, false);
        foreach (float z in new[] { 21.5f, 25.5f }) { P(portal, "Waiting bench", -20.5f, F + .22f, z, 3f, .45f, .6f, wood); TopLoot("service portal", new(-21.3f, F + .45f, z), .3f); TopLoot("service portal", new(-19.7f, F + .45f, z), .3f); }
        TopLoot("service portal", new(-32, F + 1.1f, 31.8f), .4f); TopLoot("service portal", new(-30, F + 1.1f, 31.8f), .4f);
        Tube(portal, new(-28, F + HallClear - .08f, 26), false, 0); Tube(portal, new(-20, F + HallClear - .08f, 26), false, 1);
        FloorLoot("service portal", -34.5f, 18.5f, -16.5f, 33.5f, F, .3f, 3f, .6f);
        Enemy("service portal", -26, F, 24); Enemy("service portal east", -18.5f, F, 31);

        var lost = RoomV3(t, "Lost and found", -36, 36, -29, 52, F, HallClear, tile, darkTile, null, Solid, Solid, Solid);
        for (int i = 0; i < 3; i++) { P(lost, "Lost property shelf", -29.6f, F + .6f, 39 + i * 4.5f, .7f, 1.2f, 3f, steel); TopLoot("lost and found", new(-29.6f, F + 1.2f, 38.2f + i * 4.5f), .7f); TopLoot("lost and found", new(-29.6f, F + 1.2f, 39.8f + i * 4.5f), .7f); }
        P(lost, "Umbrella bin", -30.6f, F + .3f, 37, .4f, .6f, .4f, steel);
        for (int i = 0; i < 5; i++) P(lost, "Umbrella", -30.6f + (i % 3 - 1) * .08f, F + .75f, 37 + (i % 2) * .08f, .05f, .9f, .05f, i % 2 == 0 ? zoneBlue : wreckRed, false, i * 30, 8 - i * 4);
        P(lost, "Suitcase", -35.5f, F + .35f, 49, .3f, .7f, .5f, wreckRed);
        P(lost, "Suitcase", -35.5f, F + .3f, 49.7f, .28f, .6f, .45f, zoneBlue);
        P(lost, "Suitcase (lying)", -35.3f, F + .12f, 48.2f, .5f, .24f, .7f, darkTile);
        var bike = Group(lost, "Abandoned bicycle"); bike.localPosition = new(-35.6f, F, 38.6f);
        Box(bike, "Bike wheel", new(0, .35f, -.55f), new(.04f, .7f, .7f), darkTile, false); Box(bike, "Bike wheel", new(0, .35f, .55f), new(.04f, .7f, .7f), darkTile, false);
        Box(bike, "Bike frame", new(0, .55f, 0), new(.05f, .05f, 1.0f), wreckRed); Box(bike, "Bike seat post", new(0, .75f, -.2f), new(.05f, .45f, .05f), wreckRed, false);
        Box(bike, "Bike bars", new(0, .95f, .45f), new(.5f, .04f, .04f), steel, false);
        var duck = Group(lost, "Giant stuffed duck"); duck.localPosition = new(-32.6f, F, 50.6f); duck.localRotation = Quaternion.Euler(0, 200, 0);
        Box(duck, "Duck body", new(0, .45f, 0), new(1.1f, .9f, 1.4f), yellow); Box(duck, "Duck head", new(0, 1.15f, .45f), new(.65f, .6f, .6f), yellow);
        Box(duck, "Duck beak", new(0, 1.08f, .85f), new(.4f, .14f, .3f), zoneOrange); Box(duck, "Duck tail", new(0, .72f, -.75f), new(.5f, .3f, .3f), yellow);
        TopLoot("lost and found", new(-32.6f, F + .9f, 50.4f), .9f);
        Tube(lost, new(-32.5f, F + HallClear - .08f, 44), true, 0);
        FloorLoot("lost and found", -35.5f, 36.5f, -30.5f, 51.5f, F, .6f, 2.4f, .5f);
        Enemy("lost and found", -33, F, 45);

        // Shop band behind Platform 1 (east walls are the platform shopfront).
        var toilets = RoomV3(t, "Toilets", -36, 53, -25, 64, F, pc, tile, floorTile, null, null, Solid, Solid);
        for (int i = 0; i < 3; i++)
        {
            float z = 55.4f + i * 2.2f;
            Wall(toilets, "Stall partition", false, 56.5f + i * 2.2f, -36, -33.5f, F, 2f, paint);
            P(toilets, "Toilet", -35.5f, F + .23f, z, .5f, .45f, .65f, tile);
            P(toilets, "Toilet cistern", -35.8f, F + .7f, z, .2f, .5f, .5f, tile);
            if (i == 1) P(toilets, "Hanging stall door", -33.2f, F + .9f, z + .55f, .05f, 1.7f, .9f, paint, false, 35, 12);
            else P(toilets, "Stall door", -33.5f, F + 1f, z, .05f, 1.7f, .9f, paint, false);
        }
        P(toilets, "Sink counter", -28.5f, F + .45f, 53.45f, 5f, .9f, .6f, concrete);
        for (int i = 0; i < 3; i++) P(toilets, "Sink bowl", -30.2f + i * 1.7f, F + .92f, 53.45f, .5f, .05f, .38f, tile, false);
        P(toilets, "Mirror", -28.5f, -1.5f, 53.19f, 4.6f, .8f, .03f, steel, false);
        P(toilets, "Hand dryer", -25.9f, -1.7f, 53.2f, .3f, .3f, .2f, tile, false);
        for (int i = 0; i < 3; i++) P(toilets, i == 1 ? "Cracked urinal" : "Urinal", -30.5f + i * 1.1f, F + .75f, 63.75f, .45f, .7f, .3f, tile, true, 0, i == 1 ? 6 : 0);
        P(toilets, "Mop bucket", -26.4f, F + .2f, 62.6f, .45f, .4f, .45f, yellow);
        TopLoot("toilets", new(-29.4f, F + .9f, 53.45f), .3f); TopLoot("toilets", new(-27.6f, F + .9f, 53.45f), .3f);
        Tube(toilets, new(-30.5f, F + pc - .08f, 58.5f), false, 1);
        FloorLoot("toilets", -33f, 53.5f, -25.5f, 63.5f, F, .3f, 2.4f, .6f);
        Enemy("toilets", -30.5f, F, 59);

        var waiting = RoomV3(t, "Waiting room", -36, 64, -25, 74, F, pc, tile, floorTile, null, null, null, Solid);
        for (int i = 0; i < 3; i++)
        {
            float z = 66 + i * 2.8f;
            P(waiting, "Joined seat row", -31, F + .22f, z, 4f, .45f, .6f, wood);
            P(waiting, "Seat backs", -31, F + .65f, z - .27f, 4f, .45f, .08f, steel);
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

        var lockers = RoomV3(t, "Coin lockers", -36, 74, -25, 84, F, pc, tile, floorTile, null, null, null, Solid);
        foreach (var (x, z, d, face) in new[] { (-35.6f, 79f, 9f, 1f), (-30.5f, 79f, 5f, 1f) })
        {
            P(lockers, "Coin locker bank", x, F + .5f, z, .7f, 1f, d, steel);
            for (float zz = z - d / 2 + .5f; zz < z + d / 2 - .1f; zz += .5f) P(lockers, "Locker door seam", x + face * .36f, F + .5f, zz, .02f, .96f, .03f, darkTile, false);
            P(lockers, "Locker shelf seam", x + face * .36f, F + .5f, z, .02f, .03f, d - .05f, darkTile, false);
            for (float zz = z - d / 2 + .8f; zz < z + d / 2; zz += 1.6f) TopLoot("coin lockers", new(x, F + 1f, zz), .5f);
        }
        P(lockers, "Open locker door", -35.0f, F + .75f, 76.1f, .03f, .45f, .45f, steel, false, 70);
        P(lockers, "Open locker door", -29.9f, F + .25f, 78.6f, .03f, .45f, .45f, steel, false, 110);
        P(lockers, "Change machine", -26.0f, F + .75f, 83.4f, .7f, 1.5f, .5f, steel);
        GlowBox(lockers, "Change machine display", new(-26.0f, F + 1.15f, 83.13f), new(.4f, .18f, .03f), glowScreen);
        Tube(lockers, new(-28, F + pc - .08f, 79), false, 2);
        PL(lockers, "Baked locker light", new(-33, F + 2f, 79), Color.Lerp(Cool, zoneTint, .5f), 6, 6);
        FloorLoot("coin lockers", -34.8f, 74.5f, -25.5f, 83.5f, F, .45f, 2.4f, .5f);
        Enemy("coin lockers", -27.5f, F, 76);

        var konbini = RoomV3(t, "Konbini", -36, 84, -25, 102, F, pc, tile, floorTile, null, null, null, Solid);
        Material[] goods = { yellow, wreckRed, zoneGreen, zoneBlue, zoneOrange };
        int g = 0;
        foreach (float x in new[] { -33f, -30.5f, -28f })
        {
            P(konbini, "Gondola shelf", x, F + .65f, 93, .9f, 1.3f, 12f, steel);
            // Individual product blocks (probe-lit, not lightmapped, to keep the bake small).
            for (int s = 0; s < 3; s++) foreach (float side in new[] { -1f, 1f })
                    for (float z = 87.4f; z < 98.8f; z += .5f)
                    {
                        if ((int)(z * 10 + s * 7 + side * 3) % 9 == 0) continue;   // a few gaps where stock was taken
                        var item = P(konbini, "Shelf stock", x + side * .5f, F + .3f + s * .4f, z, .1f, .2f + (g % 3) * .03f, .36f, goods[g++ % goods.Length], false);
                        UnityEditor.GameObjectUtility.SetStaticEditorFlags(item, 0);
                        item.GetComponent<MeshRenderer>().receiveGI = ReceiveGI.LightProbes;
                    }
            for (float z = 88f; z <= 98.5f; z += 1.5f) TopLoot("konbini", new(x, F + 1.3f, z), .15f);
        }
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
        for (int i = 0; i < 5; i++) P(konbini, "Fallen stock", -31.75f + (i % 2) * 2.5f, F + .1f, 88.5f + i * 2.1f, .3f, .2f, .25f, goods[i], true, i * 47);
        TopLoot("konbini", new(-26.2f, F + 1f, 100.2f), .2f); TopLoot("konbini", new(-31.5f, F + 1.02f, 101.5f), .3f);
        Sign3(konbini, "KONBINI  24", new(-25.15f, -1.0f, 93), 90, .32f);
        Tube(konbini, new(-31.8f, F + pc - .08f, 89), false, 1); Tube(konbini, new(-31.8f, F + pc - .08f, 97), false, 0);
        FloorLoot("konbini", -35f, 84.4f, -25.6f, 101.6f, F, .15f, 2.5f, .5f);
        Enemy("konbini", -31.8f, F, 86.2f); Enemy("konbini back", -29.3f, F, 100.6f);

        var staff = RoomV3(t, "Staff room", -36, 102, -25, 110, F, pc, tile, floorTile, null, null, null, Solid);
        P(staff, "Staff table", -30.5f, F + .38f, 106, 3f, .76f, 1.4f, wood);
        foreach (float x in new[] { -31.6f, -29.4f }) foreach (float dz in new[] { -1.05f, 1.05f }) P(staff, "Staff chair", x, F + .23f, 106 + dz, .45f, .45f, .45f, steel);
        P(staff, "Staff lockers", -35.6f, F + .9f, 104, .6f, 1.8f, 3f, steel);
        P(staff, "Kitchen counter", -31.5f, F + .45f, 109.6f, 3f, .9f, .6f, wood);
        P(staff, "Kettle", -32.4f, F + 1.02f, 109.6f, .18f, .24f, .18f, steel);
        P(staff, "Microwave", -31.0f, F + 1.07f, 109.6f, .5f, .32f, .35f, tile);
        P(staff, "Noticeboard", -35.85f, -1.3f, 107.6f, .03f, .9f, 1.4f, wood, false);
        foreach (float dz in new[] { -.4f, .1f, .45f }) P(staff, "Pinned notice", -35.82f, -1.25f + dz * .3f, 107.6f + dz, .01f, .25f, .2f, tile, false);
        P(staff, "Rota whiteboard", -28f, -1.2f, 109.8f, 1.6f, .9f, .03f, tile, false);
        P(staff, "Sofa seat", -27.6f, F + .22f, 103.0f, 2f, .45f, .8f, zoneBlue);
        P(staff, "Sofa back", -27.6f, F + .6f, 102.65f, 2f, .5f, .2f, zoneBlue);
        TopLoot("staff room", new(-31.3f, F + .76f, 106), .6f); TopLoot("staff room", new(-29.7f, F + .76f, 106), .6f); TopLoot("staff room", new(-30.4f, F + .9f, 109.6f), .6f);
        Tube(staff, new(-30.5f, F + pc - .08f, 106), false, 0);
        FloorLoot("staff room", -35f, 102.4f, -25.6f, 109.6f, F, .5f, 2.4f, .5f);
        Enemy("staff room", -27f, F, 108.6f);

        // Cable rooms sit right after the staff room so the transfer hall ends here.
        Zone(zoneTeal);
        var vault = RoomV3(t, "Cable vault", -36, 118, -30, 134, F, HallClear, darkTile, concrete, null, new[] { 124f, 127 }, Solid, Solid);
        for (int i = 0; i < 2; i++) { P(vault, "Cable drum", -33.5f, F + .6f, 120.5f + i * 9.5f, 1.2f, 1.2f, 1.2f, wood); TopLoot("cable vault", new(-33.5f, F + 1.2f, 120.5f + i * 9.5f), 1f); }
        P(vault, "Cable rack", -35.6f, F + .9f, 121f, .5f, 1.8f, 3f, steel);
        for (int i = 0; i < 4; i++) P(vault, "Cable run", -35.3f, F + .4f + i * .4f, 121f, .08f, .08f, 2.9f, i % 2 == 0 ? darkTile : zoneOrange, false);
        Sign3(vault, "DANGER   LIVE CABLES", new(-30.2f, -1.0f, 131.5f), -90, .28f);
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
            P(gallery, "Cable tray", -17.4f, y + .35f, 123, .5f, .12f, 21, steel, false);
            for (int i = 0; i < 3; i++) P(gallery, "Cable bundle", -29.7f + i * .12f, y + .1f, 123, .08f, .08f, 20.8f, i == 1 ? zoneOrange : darkTile, false);
        }
        foreach (float z in new[] { 116f, 122f, 128f }) { P(gallery, "Cable cabinet", -23.5f, F + .7f, z, 2.2f, 1.4f, 1f, steel); TopLoot("cable gallery", new(-24.2f, F + 1.4f, z), 1f); TopLoot("cable gallery", new(-22.8f, F + 1.4f, z), 1f); }
        foreach (float z in new[] { 114.5f, 131.5f }) P(gallery, "Junction box", -29.8f, -1.8f, z, .25f, .6f, .5f, steel, false);
        Sign3(gallery, "CABLE GALLERY  B", new(-29.8f, -2.0f, 121), 90, .28f);
        FloorLoot("cable gallery", -29.5f, 112.5f, -17.5f, 133.5f, F, .9f, 3f, .6f);
        Enemy("cable gallery south", -20, F, 114.5f); Enemy("cable gallery north", -27, F, 131); Enemy("cable gallery middle", -20, F, 125);
    }

    // ---------------- east maintenance side ----------------
    static void EastRooms(Transform t)
    {
        Zone(zoneOrange);
        float hc = HallClear;
        RoomV3(t, "Maintenance hall", 13, 40, 17, 122, F, hc, paint, concrete, new[] { 40f, 44, 46, 50, 90, 92 }, new[] { 61f, 63, 75, 77, 89, 91, 103, 105, 115, 118 }, Solid, Solid);
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
        RoomV3(t, "Wreck side door passage", 9.7f, 90, 13, 92, F, hc, darkTile, darkTile, null, null, Solid, Solid);

        var gallery = RoomV3(t, "Fan gallery", 8, 12, 13, 44, F, hc, darkTile, concrete, Solid, new[] { 28f, 31, 40, 44 }, Solid, Solid);
        foreach (float z in new[] { 18f, 28f, 38f })
        {
            P(gallery, "Fan duct grille", 8.14f, -1.6f, z, .03f, 1.8f, 2.4f, steel, false);
            for (int r = 0; r < 5; r++) P(gallery, "Grille slat", 8.17f, -2.3f + r * .35f, z, .03f, .05f, 2.3f, darkTile, false);
            Sign3(gallery, "FAN " + (z == 18 ? 1 : z == 28 ? 2 : 3), new(8.2f, -.85f, z), 90, .25f);
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
        P(control, "Valve wheel", 24.25f, -1.6f, 31.5f, .05f, .6f, .6f, wreckRed, false, 0, 45);
        P(control, "Operator chair", 21, F + .25f, 33.6f, .5f, .5f, .5f, darkTile);
        TopLoot("fan control", new(19.5f, F + .9f, 35.1f), 1f); TopLoot("fan control", new(22.5f, F + .9f, 35.1f), 1f);
        PL(control, "Baked control light", new(19, F + 1.9f, 29), Warm, 8, 7);
        FloorLoot("fan control", 13.5f, 22.5f, 23.8f, 33.8f, F, .9f, 2.5f, .5f);
        Enemy("fan control", 17, F, 26);

        foreach (var (name, z0) in new[] { ("Crew lockers", 56f), ("Signal room", 70f), ("Canteen", 84f), ("Electrical room", 98f) })
        {
            bool signal = name == "Signal room";
            var room = RoomV3(t, name, 17, z0, 28, z0 + 12, F, hc, paint, concrete, null, signal ? new[] { 75f, 77 } : Solid, Solid, Solid);
            switch (name)
            {
                case "Crew lockers":
                    P(room, "Crew locker row", 27.6f, F + .9f, z0 + 6, .6f, 1.8f, 9f, steel);
                    for (float zz = z0 + 2f; zz < z0 + 10.5f; zz += .6f) P(room, "Locker door seam", 27.28f, F + .9f, zz, .02f, 1.75f, .03f, darkTile, false);
                    P(room, "Changing bench", 22, F + .22f, z0 + 6, .6f, .45f, 6f, wood);
                    for (int i = 0; i < 4; i++) P(room, "Hanging hi-vis vest", 17.18f, -1.5f, z0 + 2.5f + i * 1.1f, .04f, .7f, .55f, i % 2 == 0 ? yellow : zoneOrange, false);
                    P(room, "Hard hat", 22.1f, F + .55f, z0 + 4.2f, .3f, .2f, .35f, yellow);
                    P(room, "Hard hat", 21.9f, F + .55f, z0 + 7.9f, .3f, .2f, .35f, yellow);
                    foreach (float dz in new[] { 0f, .3f }) P(room, "Work boot", 23.4f, F + .1f, z0 + 9.5f + dz, .3f, .2f, .14f, darkTile);
                    for (float zz = z0 + 3.0f; zz <= z0 + 9.0f; zz += 3f) TopLoot("crew lockers", new(22, F + .45f, zz), .8f);
                    break;
                case "Signal room":
                    P(room, "Signal desk", 22.5f, F + .45f, z0 + 11.3f, 7f, .9f, 1f, steel);
                    GlowBox(room, "Signal diagram", new(22.5f, -1.2f, z0 + 11.84f), new(6f, .9f, .03f), glowScreen);
                    for (int i = 0; i < 6; i++) P(room, "Signal lever", 20.4f + i * .5f, F + 1.15f, z0 + 10.95f, .06f, .55f, .06f, i % 3 == 0 ? wreckRed : steel, false, 0, i % 2 == 0 ? 12 : -10);
                    P(room, "Desk phone", 25.2f, F + .96f, z0 + 11.1f, .25f, .1f, .2f, darkTile);
                    foreach (float x in new[] { 21f, 24f }) P(room, "Office chair", x, F + .25f, z0 + 9.9f, .5f, .5f, .5f, darkTile, true, x * 9);
                    for (float x = 20.2f; x <= 24.6f; x += 2.2f) TopLoot("signal room", new(x, F + .9f, z0 + 11.45f), .9f);
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
                    for (int i = 0; i < 3; i++) P(room, "Cable conduit", 19 + i * 3f, F + 1.96f, z0 + 6, .12f, .12f, 11.6f, darkTile, false);
                    Sign3(room, "DANGER   HIGH VOLTAGE", new(21.5f, -.8f, z0 + 11.84f), 180, .28f);
                    break;
            }
            Tube(room, new(22.5f, F + hc - .08f, z0 + 6), false, name == "Canteen" ? 1 : name == "Electrical room" ? 2 : 0, name != "Electrical room");
            if (name == "Electrical room") RedLamp(room, new(17.14f, -1f, z0 + 9), 90);
            FloorLoot(name.ToLower(), 17.5f, z0 + .5f, 26.6f, z0 + 11.5f, F, name == "Electrical room" ? 1f : .7f, 2.6f, .5f);
            Enemy(name.ToLower(), 20.5f, F, z0 + 6);
        }
        RoomV3(t, "Parts passage", 28, 75, 30, 77, F, hc, paint, concrete, null, null, Solid, Solid);
        var parts = RoomV3(t, "Parts store", 30, 62, 42, 92, F, hc, paint, concrete, new[] { 75f, 77 }, Solid, Solid, Solid);
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
            Beam(pump, "Pump pipe", new(x, F + 2.7f, 125.5f), new(x, F + 2.7f, 129.6f), .35f, .35f, steel, false);
            P(pump, "Valve wheel", x + 1.2f, F + 1.3f, 125.5f, .05f, .55f, .55f, wreckRed, false, 0, 45);
            P(pump, "Pressure gauge", x - .5f, F + 1.6f, 124.36f, .3f, .3f, .04f, tile, false);
            TopLoot("pump room", new(x, F + 2.5f, 125.5f), 1f);
        }
        P(pump, "Ladder rail", 34.75f, -1.5f, 120.2f, .05f, 3f, .05f, steel, false); P(pump, "Ladder rail", 34.75f, -1.5f, 120.8f, .05f, 3f, .05f, steel, false);
        for (int r = 0; r < 8; r++) P(pump, "Ladder rung", 34.75f, -2.8f + r * .35f, 120.5f, .04f, .04f, .55f, steel, false);
        Tube(pump, new(22, F + 2.92f, 119), false, 0); Tube(pump, new(30, F + 2.92f, 119), false, 1); Tube(pump, new(26, F + 2.92f, 128), false, 2);
        FloorLoot("pump room", 17.5f, 112.5f, 34.5f, 129.5f, F, 1f, 3f, .6f);
        Enemy("pump room west", 20, F, 120.5f); Enemy("pump room east", 32.5f, F, 120.5f); Enemy("pump room north", 26, F, 129);
    }

    // ---------------- King's Quack facades at both tunnel ends ----------------
    static void Facade(Transform t, bool south)
    {
        Zone(zoneYellow);
        float z = south ? 6 : 154, s = south ? -1 : 1;   // s points out of the block
        float face = z + s * .125f;
        Vector3 At(float x, float y, float d) => new(x, y, face + s * d);
        // Plaster cladding over the earth wall, olive base band, arch frame and keystone.
        foreach (var (x0, x1) in new[] { (-44f, -TunnelHalf - .2f), (TunnelHalf + .2f, 43.75f) })
        {
            P(t, "Facade cladding", (x0 + x1) / 2, 4.5f, At(0, 0, .05f).z, x1 - x0, 9f, .1f, concrete);
            P(t, "Facade base band", (x0 + x1) / 2, .45f, At(0, 0, .12f).z, x1 - x0, .9f, .06f, paint);
        }
        P(t, "Facade cladding over arch", 0, (TunnelRoof + .3f + 9) / 2, At(0, 0, .05f).z, TunnelHalf * 2 + .4f, 9 - TunnelRoof - .3f, .1f, concrete);
        foreach (float side in new[] { -1f, 1f })
        {
            P(t, "Arch pilaster", side * (TunnelHalf + .65f), 3.3f, At(0, 0, .3f).z, .9f, 6.6f, .5f, tile);
            var hx = P(t, "Arch haunch", side * (TunnelHalf - .05f), TunnelRoof - .05f, At(0, 0, .3f).z, .8f, .8f, .5f, tile);
            hx.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
        P(t, "Arch head", 0, TunnelRoof + .7f, At(0, 0, .3f).z, TunnelHalf * 2 + 2.2f, .9f, .5f, tile);
        P(t, "Keystone", 0, TunnelRoof + .7f, At(0, 0, .55f).z, .7f, 1.1f, .2f, yellow);
        P(t, "Arch hazard band", 0, TunnelRoof + .2f, At(0, 0, .58f).z, TunnelHalf * 2, .12f, .05f, yellow, false);
        // Station name sign over the arch, lit by two cowl lamps.
        float signY = TunnelRoof + 1.95f;
        P(t, "Name board", 0, signY, At(0, 0, .2f).z, 9.6f, 1.5f, .2f, darkTile);
        P(t, "Name board trim", 0, signY + .8f, At(0, 0, .25f).z, 9.8f, .1f, .25f, yellow, false);
        P(t, "Name board trim", 0, signY - .8f, At(0, 0, .25f).z, 9.8f, .1f, .25f, yellow, false);
        Sign3(t, StationName, At(0, signY, .32f), south ? 180 : 0, 1.6f);
        var lettering = t.GetChild(t.childCount - 1).GetComponentInChildren<TMPro.TextMeshPro>();
        lettering.fontSizeMax = 7; lettering.fontSize = 7; lettering.rectTransform.sizeDelta = new(5.6f, .9f); lettering.color = new(1f, .9f, .55f);
        foreach (float x in new[] { -3.2f, 3.2f })
        {
            P(t, "Sign lamp arm", x, signY + 1.05f, At(0, 0, .55f).z, .06f, .06f, .7f, steel, false);
            GlowBox(t, "Sign lamp", At(x, signY + .95f, .9f), new(.4f, .15f, .3f), glowAmber);
        }
        PL(t, "Baked sign light", At(0, signY, 2.2f), Warm, 14, 9);
        // Parapet railing along the top of the wall.
        for (float x = -43.5f; x <= 43.5f; x += 2.5f) P(t, "Parapet post", x, 9.5f, At(0, 0, .05f).z, .08f, 1f, .08f, steel, false);
        P(t, "Parapet rail", 0, 10f, At(0, 0, .05f).z, 87.5f, .08f, .08f, steel, false);
        // Ledge behind the wall with a row of city buildings (kept clear of the midpoint shop pad at the north end).
        float deckZ0 = south ? 6 : 140, deckLen = 14;
        float deckX0 = south ? -44 : -5.5f;
        Slab(t, "City ledge", deckX0, deckZ0, 43.75f - deckX0, deckLen, 9.006f, gravel);
        var specs = south
            ? new[] { (-36f, 10f, 9f), (-24.5f, 9f, 16f), (-13.5f, 9f, 11f), (14f, 10f, 13f), (26f, 9f, 20f), (37.5f, 8f, 10f) }
            : new[] { (4f, 9f, 12f), (15f, 9f, 18f), (26f, 10f, 9f), (37.5f, 8f, 14f) };
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
