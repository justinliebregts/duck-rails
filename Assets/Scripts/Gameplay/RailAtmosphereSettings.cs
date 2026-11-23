using System;
using UnityEngine;

/// <summary>Shared saved fog and sky endpoints for gameplay and the editor preview.</summary>
public sealed class RailAtmosphereSettings : ScriptableObject
{
    public const string ResourcePath = "Rails/Atmosphere";
    public const string AssetPath = "Assets/Resources/Rails/Atmosphere.asset";
    [Header("Night")]
    public Color nightColor = new Color(.008f, .008f, .004f);
    [Min(0)] public float nightDensity = .06f;
    [Header("Pre-dawn")]
    public Color preDawnColor = new Color32(6, 6, 3, 255);
    [Min(0)] public float preDawnDensity = .05f;
    [Header("Dawn")]
    public Color dawnColor = new Color(.9f, .8f, .62f);
    [Min(0)] public float dawnDensity = .005f;

    // Storm sky and distant mountain endpoints. The sky's lower hemisphere follows the fog colour.
    [Header("Storm sky")]
    public SkyPhase nightSky = SkyPhase.Night;
    public SkyPhase preDawnSky = SkyPhase.PreDawn;
    public SkyPhase dawnSky = SkyPhase.Dawn;

    [Serializable]
    public struct SkyPhase
    {
        public Color zenith;
        public Color horizon;
        public Color cloudEdge;
        public Color cloudShadow;
        [Range(0, 1)] public float cloudCoverage;
        [Range(0, 1)] public float cloudOpacity;
        [Range(0, 2)] public float exposure;
        [Tooltip("Warm glow along the horizon toward the sunrise.")]
        public Color sunGlow;
        [Range(0, 2)] public float sunGlowStrength;
        [Tooltip("Sun height in degrees (negative = below the horizon). Aims the glow and both sun lights.")]
        [Range(-20, 45)] public float sunElevation;
        [Tooltip("Realtime sunlight added on top of the baked night lighting. Zero disables the light.")]
        public Color sunlight;
        [Range(0, 3)] public float sunlightIntensity;
        public Color ambient;
        public Color mountainNear;
        public Color mountainFar;

        // Night is a much darker version of the Duckz storm palette (Duckz exposure 0.8; Justin, Nov 23: night too bright).
        public static SkyPhase Night => new SkyPhase
        {
            zenith = new Color(.08f, .095f, .065f), horizon = new Color(.12f, .135f, .09f),
            cloudEdge = new Color(.14f, .155f, .11f), cloudShadow = new Color(.018f, .024f, .017f),
            cloudCoverage = .54f, cloudOpacity = .95f,
            exposure = .12f, sunGlow = new Color(1, .55f, .25f), sunGlowStrength = 0, sunElevation = -12,
            sunlight = new Color(1, .85f, .65f), sunlightIntensity = 0, ambient = new Color(.025f, .03f, .045f),
            mountainNear = new Color(.002f, .002f, .002f), mountainFar = new Color(.008f, .009f, .007f),
        };
        public static SkyPhase PreDawn => new SkyPhase
        {
            zenith = new Color(.08f, .095f, .075f), horizon = new Color(.13f, .14f, .1f),
            cloudEdge = new Color(.15f, .16f, .12f), cloudShadow = new Color(.02f, .026f, .02f),
            cloudCoverage = .54f, cloudOpacity = .95f,
            exposure = .2f, sunGlow = new Color(1, .45f, .2f), sunGlowStrength = .12f, sunElevation = -5,
            sunlight = new Color(1, .7f, .45f), sunlightIntensity = 0, ambient = new Color(.055f, .065f, .09f),
            mountainNear = new Color(.003f, .003f, .003f), mountainFar = new Color(.013f, .015f, .011f),
        };
        // November 23 (Justin): dawn should feel really sunny. Blue sky, broken clouds lit gold,
        // a low warm sun with realtime sunlight, and a light golden haze instead of grey fog.
        public static SkyPhase Dawn => new SkyPhase
        {
            zenith = new Color(.3f, .47f, .72f), horizon = new Color(.98f, .82f, .6f),
            cloudEdge = new Color(1, .9f, .76f), cloudShadow = new Color(.56f, .52f, .54f),
            cloudCoverage = .42f, cloudOpacity = .8f,
            exposure = 1, sunGlow = new Color(1, .72f, .42f), sunGlowStrength = 1.3f, sunElevation = 14,
            sunlight = new Color(1, .86f, .66f), sunlightIntensity = 1.3f, ambient = new Color(.5f, .5f, .52f),
            mountainNear = new Color(.36f, .4f, .36f), mountainFar = new Color(.68f, .66f, .62f),
        };

        public static SkyPhase Lerp(SkyPhase a, SkyPhase b, float t) => new SkyPhase
        {
            zenith = Color.Lerp(a.zenith, b.zenith, t), horizon = Color.Lerp(a.horizon, b.horizon, t),
            cloudEdge = Color.Lerp(a.cloudEdge, b.cloudEdge, t), cloudShadow = Color.Lerp(a.cloudShadow, b.cloudShadow, t),
            cloudCoverage = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t), cloudOpacity = Mathf.Lerp(a.cloudOpacity, b.cloudOpacity, t),
            exposure = Mathf.Lerp(a.exposure, b.exposure, t),
            sunGlow = Color.Lerp(a.sunGlow, b.sunGlow, t), sunGlowStrength = Mathf.Lerp(a.sunGlowStrength, b.sunGlowStrength, t),
            sunElevation = Mathf.Lerp(a.sunElevation, b.sunElevation, t),
            sunlight = Color.Lerp(a.sunlight, b.sunlight, t), sunlightIntensity = Mathf.Lerp(a.sunlightIntensity, b.sunlightIntensity, t),
            ambient = Color.Lerp(a.ambient, b.ambient, t),
            mountainNear = Color.Lerp(a.mountainNear, b.mountainNear, t), mountainFar = Color.Lerp(a.mountainFar, b.mountainFar, t),
        };
    }
}
