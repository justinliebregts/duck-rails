using UnityEngine;

namespace Game.Audio
{
    /// <summary>A bounded source offset and real-time duration, accounting for playback pitch.</summary>
    public readonly struct AmbienceExcerpt
    {
        public readonly float Offset, Duration, Fade;

        AmbienceExcerpt(float offset, float duration, float fade)
        { Offset = offset; Duration = duration; Fade = fade; }

        public static AmbienceExcerpt Select(float clipSeconds, float pitch, Vector2 durationRange, float fadeSeconds)
        {
            pitch = Mathf.Max(0.01f, Mathf.Abs(pitch));
            float fullDuration = clipSeconds / pitch;
            float maximum = Mathf.Max(durationRange.x, durationRange.y);
            // A zero range retains full playback. Short accents keep their original attack/tail.
            if (maximum <= 0f || fullDuration <= maximum) return new AmbienceExcerpt(0f, fullDuration, 0f);
            float minimum = Mathf.Clamp(Mathf.Min(durationRange.x, durationRange.y), Mathf.Min(0.1f, maximum), maximum);
            float duration = Random.Range(minimum, maximum);
            float offset = Random.Range(0f, Mathf.Max(0f, clipSeconds - duration * pitch));
            return new AmbienceExcerpt(offset, duration, Mathf.Clamp(fadeSeconds, 0f, duration * 0.5f));
        }

        public static float TailGain(double secondsRemaining, float fadeSeconds)
            => fadeSeconds > 0f ? Mathf.Clamp01((float)(secondsRemaining / fadeSeconds)) : 1f;
    }
}
