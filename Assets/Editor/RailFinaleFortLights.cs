#if UNITY_EDITOR
using System.Text;
using UnityEngine;

// Block lights around the fort: wall-mounted lenses and floor lanterns, each with an emissive lens and one
// baked point light. Baked only, like every other light in the block, so they cost nothing at runtime but
// do not illuminate anything until the finale lighting is rebaked.
public static partial class RailFinaleFortArtPass
{
    static Transform lightGroup; static int lightCount;
    static readonly Color WarmLight = new(1, .76f, .48f), CoolLight = new(.65f, .8f, 1);

    static void BakedLight(string name, Vector3 p, bool warm, float intensity, float range)
    {
        var go = new GameObject(name); go.transform.SetParent(lightGroup, false); go.transform.localPosition = p;
        var l = go.AddComponent<Light>(); l.type = LightType.Point; l.lightmapBakeType = LightmapBakeType.Baked; l.shadows = LightShadows.Soft;
        l.color = warm ? WarmLight : CoolLight; l.intensity = intensity; l.range = range; l.bounceIntensity = 1; lightCount++;
    }
    // Box given along the wall tangent (w), height (h) and distance out of the wall (d0..d1). n is axis-aligned.
    static void WallBox(string kind, Vector3 c, Vector3 n, float w, float h, float d0, float d1)
    {
        var t = new Vector3(Mathf.Abs(n.z), 0, Mathf.Abs(n.x)); var mid = c + n * ((d0 + d1) / 2);
        var half = t * (w / 2) + Vector3.up * (h / 2) + new Vector3(Mathf.Abs(n.x), 0, Mathf.Abs(n.z)) * ((d1 - d0) / 2);
        VBox(kind, mid - half, mid + half, true, 0);
    }
    // Lens on a wall face. (x, z) is on the face, y is the lens centre, (nx, nz) the outward normal.
    static void WallLight(float x, float y, float z, int nx, int nz, bool warm, bool lensOnly = false)
    {
        var c = new Vector3(x, y, z); var n = new Vector3(nx, 0, nz); string lens = warm ? "warm lamp" : "lamp";
        if (!lensOnly)
        {
            WallBox("steel", c, n, .84f, .5f, 0, .05f); WallBox(lens, c, n, .6f, .26f, .05f, .13f);
            WallBox("steel", c + Vector3.up * .22f, n, .84f, .06f, .05f, .26f);
        }
        BakedLight((warm ? "Warm" : "Cool") + " wall block light", c + n * .7f - Vector3.up * .15f, warm, warm ? 9 : 12, warm ? 8 : 9);
    }
    // Lantern standing on a floor: steel foot, yellow body, glowing block, steel cap.
    static void FloorLight(float x, float z, float y, bool warm)
    {
        string lens = warm ? "warm lamp" : "lamp"; Vector3 P(float a, float b, float c) => new(x + a, y + b, z + c);
        VBox("steel", P(-.28f, 0, -.28f), P(.28f, .08f, .28f), false, 0); VBox("yellow", P(-.2f, .08f, -.2f), P(.2f, .3f, .2f), false, 0);
        VBox(lens, P(-.17f, .3f, -.17f), P(.17f, .58f, .17f), false, 0); VBox("steel", P(-.21f, .58f, -.21f), P(.21f, .64f, .21f), true, 0);
        BoxCol(P(-.28f, 0, -.28f), P(.28f, .64f, .28f));
        BakedLight((warm ? "Warm" : "Cool") + " floor block light", new Vector3(x, y + 1, z), warm, warm ? 10 : 13, 9);
    }

    static void Lights(StringBuilder log)
    {
        lightGroup = new GameObject("Fixture lights").transform; lightGroup.SetParent(group, false); lightCount = 0;
        region = "West keep";
        WallLight(-33, 3.3f, 40, 1, 0, false); WallLight(-33, 3.3f, 47.5f, 1, 0, false);          // wall-walk face above the terrace
        WallLight(-22.5f, 1.45f, 34, 0, -1, true); WallLight(-12, 1.45f, 38, 1, 0, false);       // terrace faces
        WallLight(-38, 2.8f, 55, 1, 0, true); WallLight(-38, 2.8f, 65, 1, 0, true);              // arcade
        WallLight(-22.5f, 3.22f, 57.9f, 0, -1, true, true); WallLight(-16, 3.22f, 57.9f, 0, -1, true, true);   // existing bunker door lenses
        WallLight(-20, 2.8f, 75, 0, -1, true); WallLight(-28, 2.8f, 75, 0, -1, true);            // bunker room
        WallLight(-17, 6.4f, 64, 0, -1, false); WallLight(-30, 6.2f, 74, 1, 0, true);            // blockhouse and keep tower over the roof
        FloorLight(-15, 52, 0, false); FloorLight(-8, 72, 0, true); FloorLight(-24, 40, 2, true); FloorLight(-22, 61, 4, false);
        region = "East depot";
        WallLight(33, 3.3f, 37, -1, 0, false); WallLight(15, 1.4f, 40, 0, 1, true);              // bastion face, loading dock
        WallLight(38, 2.8f, 57, -1, 0, true); WallLight(34, 2.8f, 62, 0, -1, true);              // under the north-east deck
        WallLight(30, 3, 65.2f, -1, 0, false); WallLight(38, 2.8f, 73, -1, 0, true);             // tower, pocket deck
        WallLight(16, 2.8f, 75, 0, -1, true); WallLight(24, 2.8f, 75, 0, -1, true);              // colonnade
        WallLight(17, 6.4f, 66, 0, -1, false);                                                   // east blockhouse
        FloorLight(14, 49, 0, false); FloorLight(25.5f, 53.4f, 0, true); FloorLight(9.5f, 72.5f, 0, false);
        FloorLight(22, 43, 2, true); FloorLight(39, 44, 4, false); FloorLight(28, 58, 4, true);
        region = "Perimeter";
        WallLight(-6.5f, 4.5f, 33, 0, 1, false); WallLight(6.5f, 4.5f, 33, 0, 1, false);         // gate pylons, facing the yard
        WallLight(-20, 3.2f, 30, 0, -1, true); WallLight(15, 3.2f, 30, 0, -1, true);             // outer face, toward the approach
        region = "Centre";
        FloorLight(-5.6f, 38, 0, true); FloorLight(6, 41.5f, 0, false); FloorLight(-9.2f, 34.6f, 0, false);
        log.AppendLine($"Fixture lights: {lightCount} baked point lights");
    }
}
#endif
