#if UNITY_EDITOR
using System.Linq;
using UnityEngine;

/// <summary>V3 Passenger Station crashed commuter train: three Tokyo-style cars at real scale.</summary>
public static partial class RailAbandonedStation
{
    const float CarLen = 19.5f, CarHalfW = 1.475f, CarCentreX = 6f;
    static readonly float[] CarDoors = { -7.1f, -2.4f, 2.4f, 7.1f };   // door centres along the car
    const float DoorW = 1.3f, DoorH = 2.07f, WindowLow = .85f, WindowHigh = 1.8f, WallTop = 2.1f;   // headroom for players walking through
    public static readonly float[] CarCentres = { 60.35f, 80.35f, 100.35f };   // car 3, car 2, car 1 (front, crashed)
    public const float WreckSideDoorZ = 80.35f + 7.1f;                          // car 2's last door faces the maintenance hall passage

    static Mesh CarRoofMesh(float length) => SaveProc("car-roof-" + length.ToString("F2"), () =>
    {
        var mb = new MB();
        Vector2[] outer = { new(-CarHalfW, WallTop), new(-1.42f, WallTop + .28f), new(-1.22f, WallTop + .48f), new(-.85f, WallTop + .6f), new(.85f, WallTop + .6f), new(1.22f, WallTop + .48f), new(1.42f, WallTop + .28f), new(CarHalfW, WallTop) };
        Vector2[] inner = outer.Select(p => new Vector2(p.x * .96f, p.y - .06f)).ToArray();
        float z0 = -length / 2, z1 = length / 2;
        for (int i = 0; i < outer.Length - 1; i++)
        {
            Vector2 a = outer[i], b = outer[i + 1], ia = inner[i], ib = inner[i + 1];
            var mid = (a + b) / 2; var nrm = new Vector3(mid.x, mid.y - 1.6f, 0);
            mb.Quad(new(a.x, a.y, z0), new(b.x, b.y, z0), new(b.x, b.y, z1), new(a.x, a.y, z1), nrm);
            mb.Quad(new(ia.x, ia.y, z0), new(ib.x, ib.y, z0), new(ib.x, ib.y, z1), new(ia.x, ia.y, z1), -nrm);
            mb.Quad(new(a.x, a.y, z0), new(b.x, b.y, z0), new(ib.x, ib.y, z0), new(ia.x, ia.y, z0), Vector3.back);
            mb.Quad(new(a.x, a.y, z1), new(b.x, b.y, z1), new(ib.x, ib.y, z1), new(ia.x, ia.y, z1), Vector3.forward);
        }
        return mb;
    }, true);

    // The space between the wall top and the roof arch at each car end.
    static Mesh CarEndTopMesh() => SaveProc("car-end-top", () =>
        Extrude(new MB(), new[] { new Vector2(-1.41f, WallTop), new Vector2(-1.37f, WallTop + .24f), new Vector2(-1.18f, WallTop + .42f), new Vector2(-.82f, WallTop + .53f), new Vector2(.82f, WallTop + .53f), new Vector2(1.18f, WallTop + .42f), new Vector2(1.37f, WallTop + .24f), new Vector2(1.41f, WallTop) }, -.03f, .03f), true);

    static Mesh WheelMesh() => SaveProc("car-wheel", () =>
        Lathe(new MB(), new[] { new Vector2(0, -.07f), new Vector2(.43f, -.07f), new Vector2(.46f, -.04f), new Vector2(.46f, .04f), new Vector2(.43f, .07f), new Vector2(0, .07f) }, 10), false);

    static void TrainWreck(Transform t)
    {
        // Justin, Nov 26 batch 4: open windows (no panes), dark grey floor, white interior, light grey body
        // with the line colour as a stripe; the car shape stays.
        var cream = AtlasTile("Train roof grey", "white panel", false, 128, 0, 32, new(.62f, .64f, .66f), 2);
        var body = AtlasTile("Train body white", "white panel", false, 128, 0, 32, new(.9f, .91f, .9f), 2);
        var floorGrey = AtlasTile("Train floor grey", "metal panel", false, 96, 0, 32, new(.3f, .31f, .33f), 2);
        var glass = AtlasTile("Train window glass", "dark asphalt", true, 128, 128, 64, new(.22f, .25f, .27f), 4);   // cab windscreen only
        Material[] ads = { zoneBlue, zoneGreen, yellow, zoneOrange };
        for (int c = 0; c < 3; c++)
        {
            bool front = c == 2;
            var car = Group(t, "Crashed carriage " + (3 - c));
            float zc = CarCentres[c];
            car.localPosition = new(CarCentreX + (front ? .15f : 0), F, zc);
            car.localRotation = front ? Quaternion.Euler(0, 1.2f, 3f) : c == 1 ? Quaternion.Euler(0, .6f, 0) : Quaternion.identity;
            float hl = CarLen / 2;

            // Floor, underframe, skirt and equipment boxes.
            Box(car, "Car floor", new(0, -.1f, 0), new(CarHalfW * 2 - .08f, .2f, CarLen - .1f), floorGrey);
            Box(car, "Underframe", new(0, -.42f, 0), new(2.5f, .44f, CarLen - 2f), steel);
            foreach (float z in new[] { -4f, -1f, 3.5f }) Box(car, "Underfloor equipment", new(.3f, -.75f, z), new(1.6f, .4f, 1.8f), darkTile, false);
            foreach (float bz in new[] { -7f, 7f })
            {
                bool derailed = front && bz > 0;
                var bogie = Group(car, derailed ? "Bogie (derailed)" : "Bogie");
                bogie.localPosition = new(derailed ? .35f : 0, derailed ? -.15f : 0, bz); bogie.localRotation = Quaternion.Euler(0, derailed ? 11 : 0, derailed ? -4 : 0);
                Box(bogie, "Bogie frame", new(0, -.52f, 0), new(2.3f, .28f, 2.6f), darkTile, false);
                foreach (float wx in new[] { -.8f, .8f }) foreach (float wz in new[] { -1.05f, 1.05f })
                        Proc(bogie, "Wheel", WheelMesh(), new[] { steel }, new(wx, -.4f, wz), Quaternion.Euler(0, 0, 90), false, false);   // wheel bottom on the rail top, 0.86 m under the floor
            }

            // Side walls between the doors: lower panel, open window band with pillars, upper band.
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * (CarHalfW - .03f);
                float cursor = -hl;
                for (int d = 0; d <= CarDoors.Length; d++)
                {
                    float end = d < CarDoors.Length ? CarDoors[d] - DoorW / 2 : hl;
                    float len = end - cursor, mid = (cursor + end) / 2;
                    if (len > .05f)
                    {
                        Box(car, "Car side panel", new(x, WindowLow / 2, mid), new(.06f, WindowLow, len), body);
                        Box(car, "Car upper band", new(x, (WindowHigh + WallTop) / 2, mid), new(.06f, WallTop - WindowHigh, len), body);
                        Box(car, "Line colour stripe", new(x + side * .035f, WindowLow - .14f, mid), new(.012f, .14f, len), wreckRed, false);
                        int panes = Mathf.Max(1, Mathf.RoundToInt(len / 1.35f)); float pw = len / panes;
                        for (int w = 0; w < panes; w++)
                        {
                            float wz = cursor + (w + .5f) * pw;
                            Box(car, "Window pillar", new(x, (WindowLow + WindowHigh) / 2, cursor + w * pw + .08f), new(.07f, WindowHigh - WindowLow, .16f), body);
                            Box(car, "Ad frame", new(x - side * .05f, 1.95f, wz), new(.02f, .18f, pw - .3f), ads[(w + d) % ads.Length], false);
                        }
                        Box(car, "Window pillar", new(x, (WindowLow + WindowHigh) / 2, end - .08f), new(.07f, WindowHigh - WindowLow, .16f), body);   // closes the last opening
                        // Longitudinal bench seat and backrest inside this bay.
                        float sx = side * (CarHalfW - .32f);
                        Box(car, "Bench seat", new(sx, .21f, mid), new(.5f, .42f, len - .25f), zoneBlue);
                        Box(car, "Seat back", new(side * (CarHalfW - .1f), .65f, mid), new(.08f, .45f, len - .29f), zoneBlue, false);
                        if (!(front && mid > 1)) TopLoot("wreck", new(CarCentreX + (front ? .15f : 0) + sx, F + .42f, zc + mid), 1f);
                    }
                    if (d < CarDoors.Length)
                    {
                        float dz = CarDoors[d];
                        Box(car, "Door header", new(x, (DoorH + WallTop) / 2, dz), new(.06f, WallTop - DoorH, DoorW), body);
                        // Some sliding doors jammed half open.
                        if ((d + c + (side > 0 ? 1 : 0)) % 3 == 0)
                            Box(car, "Jammed door leaf", new(x - side * .05f, DoorH / 2, dz - DoorW * .3f), new(.04f, DoorH, DoorW * .4f), steel);
                        foreach (float e in new[] { -1f, 1f })
                            Proc(car, "Grab pole", PipeMesh(.025f, WallTop), new[] { steel }, new(side * (CarHalfW - .45f), 0, dz + e * (DoorW / 2 + .15f)), Quaternion.identity, false, false);
                        cursor = dz + DoorW / 2;
                    }
                }
                // Strap rail with hanging straps.
                Box(car, "Strap rail", new(side * .75f, 2.05f, 0), new(.03f, .03f, CarLen - .6f), steel, false);
                for (float sz = -hl + 1; sz < hl - .5f; sz += 1.1f) Box(car, "Hanging strap", new(side * .75f, 1.92f, sz), new(.02f, .24f, .08f), darkTile, false);
            }

            // Ends: gangway openings between cars, a cab front on car 1.
            foreach (float e in new[] { -1f, 1f })
            {
                float ez = e * (hl - .035f);   // outer faces 5 mm inside the side panel ends (no coplanar faces)
                bool cab = front && e > 0;
                if (cab)
                {
                    Box(car, "Cab front lower", new(0, .5f, ez), new(CarHalfW * 2 - .12f, 1f, .08f), body);
                    Box(car, "Cab windscreen (broken)", new(0, 1.45f, ez + .01f), new(CarHalfW * 2 - .3f, .85f, .04f), glass);
                    foreach (float sx2 in new[] { -1.3f, 0f, 1.3f }) Box(car, "Cab pillar", new(sx2, 1.45f, ez), new(.15f, .9f, .1f), body);
                    Box(car, "Cab header", new(0, 2f, ez), new(CarHalfW * 2 - .12f, .2f, .08f), body);
                    foreach (float sx2 in new[] { -.9f, .9f }) GlowBox(car, "Headlight (dying)", new(sx2, .7f, ez + .05f), new(.25f, .15f, .04f), glowAmber);
                    Box(car, "Destination board", new(0, 2f, ez + .06f), new(1.2f, .16f, .03f), darkTile, false);
                    Box(car, "Anticlimber", new(0, -.25f, ez + .1f), new(2.6f, .25f, .3f), steel);
                }
                else
                {
                    foreach (float s in new[] { -1f, 1f }) Box(car, "Car end wall", new(s * (CarHalfW + .45f) / 2, WallTop / 2, ez), new(CarHalfW - .45f, WallTop, .06f), body);
                    Box(car, "Gangway header", new(0, (DoorH + WallTop) / 2, ez), new(.9f, WallTop - DoorH, .06f), body);
                }
                Proc(car, "Car end top", CarEndTopMesh(), new[] { body }, new(0, 0, ez), Quaternion.identity, true, false);
            }
            // Roof: car 1's front half is crushed under the cave-in.
            float roofLen = front ? 10.6f : CarLen;
            Proc(car, "Car roof", CarRoofMesh(roofLen), new[] { cream }, new(0, 0, -hl + roofLen / 2), Quaternion.identity, true, false);
            if (front)
            {
                for (int i = 0; i < 3; i++)
                {
                    var crushed = Box(car, "Crushed roof panel", new(-.2f + i * .3f, 1.75f - i * .25f, 2.2f + i * 2.6f), new(2.8f, .08f, 2.7f), cream, false);
                    crushed.transform.localRotation = Quaternion.Euler(-12 - i * 6, i * 4, 8 - i * 7);
                }
            }
            // Interior lights: amber strip in cars 2 and 3, dead in car 1.
            if (front) Box(car, "Dead light strip", new(0, WallTop + .52f, -3), new(.25f, .04f, 8f), deadTube, false);
            else
            {
                GlowBox(car, "Car light strip", new(0, WallTop + .52f, 0), new(.25f, .04f, CarLen - 2), c == 1 ? glowFlicker : glowTube);   // cool white so the white interior reads white (batch 4)
                PL(car, "Baked carriage light", new(0, 1.9f, -4.5f), Color.Lerp(Cool, Amber, .25f), 6, 7);
                PL(car, "Baked carriage light", new(0, 1.9f, 4.5f), Color.Lerp(Cool, Amber, .25f), 6, 7);
            }
            for (int i = 0; i < 4; i++) Box(car, "Litter", new(-.4f + (i % 2) * .7f, .01f, -6 + i * 3.7f), new(.25f, .02f, .32f), i % 2 == 0 ? cream : tile, false).transform.localRotation = Quaternion.Euler(0, i * 41, 0);
            FloorLoot("wreck", CarCentreX, zc - hl + 1.5f, CarCentreX, front ? zc - 1 : zc + hl - 1.5f, F, 1f, 4.2f, 0);
            Enemy("wreck car " + (3 - c), CarCentreX, F, front ? zc - 6 : zc - 3);
        }
        // Gangway bellows and floor plates between cars.
        foreach (float gz in new[] { 70.35f, 90.35f })
        {
            Slab(t, "Gangway plate", CarCentreX - .5f, gz - .3f, 1f, .6f, F - .003f, steel);
            foreach (float s in new[] { -1f, 1f }) Box(t, "Gangway bellows", new(CarCentreX + s * .62f, F + 1.05f, gz), new(.12f, 2.1f, .55f), darkTile);
            Box(t, "Gangway bellows", new(CarCentreX, F + 2.16f, gz), new(1.36f, .12f, .55f), darkTile);
        }
    }
}
#endif
