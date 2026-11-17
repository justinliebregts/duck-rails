using System;
using Game.Audio;
using UnityEngine;

[CreateAssetMenu(menuName = "Duck Rails/Ambience Settings")]
public sealed class RailAmbienceSettings : ScriptableObject
{
    [Min(0.1f)] public float crossfadeSeconds = 3f;
    public Vector2 detailInterval = new Vector2(25f, 55f);
    [Tooltip("Long details play a random section of this many seconds. Short accents play in full.")]
    public Vector2 detailDuration = new Vector2(8f, 14f);
    [Min(0f)] public float detailFadeSeconds = 1.25f;
    public Vector2 scareInterval = new Vector2(150f, 300f);
    public SoundEvent[] rareScares = Array.Empty<SoundEvent>();
    public RailAmbienceProfile[] profiles = Array.Empty<RailAmbienceProfile>();

    public RailAmbienceProfile Find(string blockId)
    {
        foreach (var profile in profiles)
            if (profile != null && profile.blockId == blockId) return profile;
        return null;
    }
}

[Serializable]
public sealed class RailAmbienceProfile
{
    public string blockId;
    public SoundEvent[] outdoorBeds = Array.Empty<SoundEvent>();
    public SoundEvent[] outdoorDetails = Array.Empty<SoundEvent>();
    public bool allowScares = true;
    public RailAmbienceArea[] areas = Array.Empty<RailAmbienceArea>();

    // First matching area wins. Put cellar/small rooms before broader buildings.
    public RailAmbienceArea AreaAt(Vector3 localPosition, RailAmbienceArea previous)
    {
        foreach (var area in areas)
        {
            var bounds = area.bounds;
            if (area == previous) bounds.Expand(0.8f);
            if (bounds.Contains(localPosition)) return area;
        }
        return null;
    }
}

[Serializable]
public sealed class RailAmbienceArea
{
    public string name;
    public Bounds bounds;
    public SoundEvent[] beds = Array.Empty<SoundEvent>();
    public SoundEvent[] details = Array.Empty<SoundEvent>();
}
