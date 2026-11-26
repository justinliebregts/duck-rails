#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

/// <summary>V3 Passenger Station pipe runs, painted in each area's colour (visual only, no collision).</summary>
public static partial class RailAbandonedStation
{
    static void BuildPipes(Transform root)
    {
        var t = Group(root, "Pipes");
        float pcY = F + PlatformClear, hcY = F + HallClear;
        Material green = PipeMat(zoneGreen), blue = PipeMat(zoneBlue), purple = PipeMat(zonePurple), red = PipeMat(zoneRed),
                 yellowP = PipeMat(zoneYellow), orange = PipeMat(zoneOrange), teal = PipeMat(zoneTeal), bare = PipeMat(steel);
        void Pair(string n, Vector3 a, Vector3 b, Vector3 offset, float r, Material m, float ceiling)
        {
            PipeRun(t, n, new[] { a, b }, r, m, ceiling);
            PipeRun(t, n, new[] { a + offset, b + offset }, r * .75f, m, Mathf.Abs(offset.y) > .1f ? float.NaN : ceiling);   // stacked pairs share the upper hangers
        }
        // Platform 1: along the shopfront side, with a drop down the wall to a valve near the toilets.
        Pair("Platform 1 pipes", new(-24.4f, pcY - .2f, 53.4f), new(-24.4f, pcY - .2f, 106.6f), new(.38f, .03f, 0), .08f, green, pcY);
        PipeRun(t, "Platform 1 drop", new[] { new Vector3(-24.4f, pcY - .2f, 64.6f), new Vector3(-24.6f, pcY - .2f, 64.6f), new Vector3(-24.6f, F + 1.1f, 64.6f) }, .06f, green);
        Box(t, "Valve wheel", new(-24.45f, F + 1.15f, 64.6f), new(.04f, .4f, .4f), wreckRed, false).transform.localRotation = Quaternion.Euler(45, 0, 0);
        // Service ledge and Platform 3 (stopping short of the cave-in hole).
        PipeRun(t, "Ledge pipe", new[] { new Vector3(-5.35f, pcY - .2f, 53.4f), new Vector3(-5.35f, pcY - .2f, 106.6f) }, .07f, green, pcY);
        Pair("Platform 3 pipes", new(3.7f, pcY - .2f, 53.4f), new(3.7f, pcY - .2f, 97.2f), new(-.36f, .03f, 0), .08f, red, pcY);
        // Over the live track in the station: above the train top (rail + 3.56), inside the channel walls.
        foreach (float x in new[] { -14.25f, -9.75f }) PipeRun(t, "Track channel conduit", new[] { new Vector3(x, -4 + 3.75f, 53.2f), new Vector3(x, -4 + 3.75f, 106.8f) }, .07f, bare);
        // Tunnels: a conduit on each wall, following the curve and grade, above the train.
        foreach (var (z0, z1) in new[] { (6f, 53f), (107f, 154f) })
            foreach (float side in new[] { -1f, 1f })
            {
                var pts = new List<Vector3>();
                for (float z = z0 + .3f; z <= z1 - .3f + .01f; z += 2f) pts.Add(new(RailX(z) + side * 2.75f, RailHeight(z) + 3.72f, z));
                PipeRun(t, "Tunnel conduit", pts.ToArray(), .06f, bare);
            }
        // Long halls (on the side away from the buggies and pallets), rooms and concourses.
        Pair("Transfer hall pipes", new(-36.6f, hcY - .25f, 18.4f), new(-36.6f, hcY - .25f, 133.6f), new(-.35f, .02f, 0), .08f, yellowP, hcY);
        Pair("Maintenance hall pipes", new(13.4f, hcY - .25f, 40.4f), new(13.4f, hcY - .25f, 121.6f), new(.35f, .02f, 0), .08f, orange, hcY);
        Pair("Fan gallery pipes", new(12.6f, hcY - .25f, 12.4f), new(12.6f, hcY - .25f, 43.6f), new(-.35f, .02f, 0), .1f, orange, hcY);
        Pair("Ticket concourse pipes", new(-27.6f, Deck + PlatformClear - .22f, 40.6f), new(3.6f, Deck + PlatformClear - .22f, 40.6f), new(0, .02f, .35f), .08f, blue, Deck + PlatformClear);
        Pair("Shuttered concourse pipes", new(-27.6f, Deck + PlatformClear - .22f, 119.4f), new(3.6f, Deck + PlatformClear - .22f, 119.4f), new(0, .02f, -.35f), .08f, purple, Deck + PlatformClear);
        Pair("Cable gallery pipes", new(-29.6f, hcY - .25f, 112.4f), new(-29.6f, hcY - .25f, 133.6f), new(.35f, .02f, 0), .08f, teal, hcY);
        Pair("Waiting room pipes", new(-35.6f, pcY - .2f, 64.4f), new(-25.4f, pcY - .2f, 64.4f), new(0, .02f, .35f), .06f, yellowP, pcY);
        // Pump room: a heavy header along the north wall with drops to each pump.
        float pumpTop = F + 3f;
        Pair("Pump header", new(17.4f, pumpTop - .3f, 129.4f), new(34.6f, pumpTop - .3f, 129.4f), new(0, -.45f, 0), .16f, orange, pumpTop);
        for (int i = 0; i < 3; i++)
        {
            float x = 21 + i * 5f;
            PipeRun(t, "Pump riser", new[] { new Vector3(x, F + 2f, 125.5f), new Vector3(x, pumpTop - .3f, 125.5f), new Vector3(x, pumpTop - .3f, 129.4f) }, .12f, orange);
        }
        // Electrical room conduits and the fan control room feed.
        for (int i = 0; i < 3; i++) PipeRun(t, "Electrical conduit", new[] { new Vector3(19 + i * 3f, hcY - .2f, 98.4f), new Vector3(19 + i * 3f, hcY - .2f, 109.6f) }, .05f, orange, hcY);
        PipeRun(t, "Fan control feed", new[] { new Vector3(13.4f, hcY - .25f, 30f), new Vector3(24.6f, hcY - .25f, 30f), new Vector3(24.6f, F + 1.9f, 30f) }, .07f, orange, hcY);
    }
}
#endif
