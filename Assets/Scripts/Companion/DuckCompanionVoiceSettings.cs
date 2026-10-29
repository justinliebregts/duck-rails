using System;
using UnityEngine;

[Serializable]
public sealed class DuckCompanionVoiceSettings
{
    public const float DefaultPitch = 1.10f;
    [Range(.1f, 3f), Tooltip("Duck's speech pitch and playback speed. Applied on every client; higher values shorten speech.")]
    public float pitch = DefaultPitch;
    public float PlaybackPitch => float.IsNaN(pitch) || float.IsInfinity(pitch)
        ? DefaultPitch : Mathf.Clamp(pitch, .1f, 3f);
    [Range(0f, 1f), Tooltip("Duck's speech source volume, before the shared Voice mixer and chat-volume slider.")]
    public float volume = 1f;
    [Range(0f, 1f), Tooltip("0 = 2D, 1 = fully positional 3D voice.")]
    public float spatialBlend = 1f;
    [Min(.01f)] public float minDistance = 1.5f;
    [Min(.02f)] public float maxDistance = 40f;
    public AudioRolloffMode rolloffMode = AudioRolloffMode.Custom;
    [Tooltip("Custom mode only. X = distance / Max Distance (0–1), Y = source gain (0–1). The curve controls attenuation; Min Distance alone does not reshape it.")]
    public AnimationCurve customRolloff = DefaultCurve();

    // Snapshot of the existing voice curve. Independent of player settings so
    // tuning Duck never changes human voices or the shared mixer.
    public static AnimationCurve DefaultCurve()
    {
        float gain = Mathf.Pow(10f, 12f / 20f);
        float[] distances = { 0f, 2f, 8f, 16f, 24f, 40f };
        var keys = new Keyframe[distances.Length];
        for (int i = 0; i < keys.Length; i++)
            keys[i] = new Keyframe(distances[i] / 40f,
                distances[i] <= 2f ? 1f : Mathf.Clamp01((40f - distances[i]) / 38.5f / gain));
        for (int i = 0; i < keys.Length; i++)
        {
            if (i > 0) keys[i].inTangent = (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time);
            if (i + 1 < keys.Length) keys[i].outTangent = (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time);
        }
        return new AnimationCurve(keys);
    }

    public void Apply(AudioSource source)
    {
        if (source == null) return;
        source.pitch = PlaybackPitch;
        source.volume = Mathf.Clamp01(volume);
        source.spatialBlend = Mathf.Clamp01(spatialBlend);
        source.minDistance = Mathf.Max(.01f, minDistance);
        source.maxDistance = Mathf.Max(source.minDistance + .01f, maxDistance);
        source.rolloffMode = rolloffMode;
        if (rolloffMode == AudioRolloffMode.Custom)
            source.SetCustomCurve(AudioSourceCurveType.CustomRolloff,
                customRolloff != null && customRolloff.length > 0 ? customRolloff : DefaultCurve());
    }
}
