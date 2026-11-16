using UnityEngine;

/// <summary>Shared saved fog endpoints for gameplay and the editor preview.</summary>
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
}
