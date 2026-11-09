using Game.Audio;
using UnityEngine;

/// <summary>Local presentation for an authored, stationary shop. Never moves the cabin.</summary>
public sealed class RailParkedHelicopter : MonoBehaviour
{
    public SoundEmitter engineSound;
    public HelicopterGroundDust rotorWash;
    float nextAudioRetry;

    void OnEnable() => nextAudioRetry = 0;

    void Update()
    {
        if (rotorWash != null && rotorWash.isActiveAndEnabled) rotorWash.SetHeight(0);
        // The audio manager may initialize after an authored scene object enables.
        if (Time.unscaledTime < nextAudioRetry) return;
        nextAudioRetry = Time.unscaledTime + 1;
        if (engineSound != null && !engineSound.IsLooping) engineSound.StartLoop();
    }

    void OnDisable()
    {
        if (rotorWash != null) rotorWash.StopDust(true);
        if (engineSound != null) engineSound.StopLoop();
    }
}
