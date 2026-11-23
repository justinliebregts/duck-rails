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
    public Color dawnColor = new Color(.439f, .463f, .392f);
    [Min(0)] public float dawnDensity = .0125f;

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
        [Range(0, 2)] public float exposure;
        [Tooltip("Warm glow along the horizon toward the sunrise.")]
        public Color sunGlow;
        [Range(0, 2)] public float sunGlowStrength;
        public Color mountainNear;
        public Color mountainFar;

        // Night is a darker version of the Duckz storm palette (Duckz exposure 0.8).
        public static SkyPhase Night => new SkyPhase
        {
            zenith = new Color(.08f, .095f, .065f), horizon = new Color(.12f, .135f, .09f),
            cloudEdge = new Color(.14f, .155f, .11f), cloudShadow = new Color(.018f, .024f, .017f),
            exposure = .6f, sunGlow = new Color(1, .55f, .25f), sunGlowStrength = 0,
            mountainNear = new Color(.008f, .01f, .007f), mountainFar = new Color(.035f, .041f, .028f),
        };
        public static SkyPhase PreDawn => new SkyPhase
        {
            zenith = new Color(.08f, .095f, .075f), horizon = new Color(.13f, .14f, .1f),
            cloudEdge = new Color(.15f, .16f, .12f), cloudShadow = new Color(.02f, .026f, .02f),
            exposure = .7f, sunGlow = new Color(1, .45f, .2f), sunGlowStrength = .12f,
            mountainNear = new Color(.01f, .012f, .009f), mountainFar = new Color(.045f, .05f, .036f),
        };
        // Muted dawn, never daytime: brighter and warmer, still overcast.
        public static SkyPhase Dawn => new SkyPhase
        {
            zenith = new Color(.26f, .28f, .27f), horizon = new Color(.44f, .45f, .37f),
            cloudEdge = new Color(.5f, .46f, .38f), cloudShadow = new Color(.2f, .2f, .18f),
            exposure = 1, sunGlow = new Color(1, .6f, .3f), sunGlowStrength = .55f,
            mountainNear = new Color(.12f, .125f, .11f), mountainFar = new Color(.26f, .27f, .23f),
        };

        public static SkyPhase Lerp(SkyPhase a, SkyPhase b, float t) => new SkyPhase
        {
            zenith = Color.Lerp(a.zenith, b.zenith, t), horizon = Color.Lerp(a.horizon, b.horizon, t),
            cloudEdge = Color.Lerp(a.cloudEdge, b.cloudEdge, t), cloudShadow = Color.Lerp(a.cloudShadow, b.cloudShadow, t),
            exposure = Mathf.Lerp(a.exposure, b.exposure, t),
            sunGlow = Color.Lerp(a.sunGlow, b.sunGlow, t), sunGlowStrength = Mathf.Lerp(a.sunGlowStrength, b.sunGlowStrength, t),
            mountainNear = Color.Lerp(a.mountainNear, b.mountainNear, t), mountainFar = Color.Lerp(a.mountainFar, b.mountainFar, t),
        };
    }
}
