using UnityEngine;

#if DUCKZ_SPECTATOR && UNITY_ANDROID
#error DUCKZ_SPECTATOR is for the PC spectator build only. Quest builds must never define it.
#endif

/// <summary>
/// True only in the PC spectator build (DUCKZ_SPECTATOR) or in the editor when the Spectator
/// play mode is selected. Always false in Quest builds; the spectator itself is not compiled there.
/// Ported from Duckz 491b6f2/fcdff53/a6e6a40; see docs/duckz-ports.md.
/// </summary>
public static class SpectatorMode
{
    public const string PrefKey = "Duck.Spectator";

#if UNITY_EDITOR && !DUCKZ_SPECTATOR
    static int _cachedFrame = -1;
    static bool _cached;
#endif

    public static bool Active
    {
        get
        {
#if DUCKZ_SPECTATOR
            return true;
#elif UNITY_EDITOR
            if (!Application.isPlaying)
                return UnityEditor.EditorPrefs.GetBool(PrefKey, false);
            if (_cachedFrame != Time.frameCount)
            {
                _cachedFrame = Time.frameCount;
                _cached = UnityEditor.EditorPrefs.GetBool(PrefKey, false);
            }
            return _cached;
#else
            return false;
#endif
        }
    }
}
