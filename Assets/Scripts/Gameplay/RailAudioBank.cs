using Game.Audio;
using UnityEngine;

/// <summary>Every train, furnace and throttle sound in one asset (Resources/RailAudioBank). Empty slots stay silent.</summary>
[CreateAssetMenu(menuName = "DuckZ/Audio/Rail Audio Bank")]
public sealed class RailAudioBank : ScriptableObject
{
    [Header("Furnace")]
    public SoundEvent coalThud;
    public SoundEvent fireFlare, fuelChime;
    [Tooltip("Looping fire bed at the firebox while fuel remains.")]
    public SoundEvent furnaceFireLoop;

    [Header("Train")]
    public SoundEvent railJoint;
    public SoundEvent trainStop, outOfFuel, lowFuelWarning, bridgeCrash;
    [Tooltip("Seamless loop. Volume fades in with speed and pitch follows speed.")]
    public AudioClip rumbleLoop;
    [Tooltip("One-shots played while the horn button is held. Never loops; fades quickly on release.")]
    public AudioClip[] hornClips = new AudioClip[0];
    [Tooltip("Plays 2D for the whole successful ending, fading out as the crew returns to the compound.")]
    public AudioClip bridgeMusic;

    [Header("Throttle")]
    public SoundEvent throttleGrab;
    public SoundEvent throttleRelease, throttleClick, throttleLatch;

    [Header("Tuning")]
    [Min(1)] public float railJointSpacing = 3.5f;
    [Min(.1f)] public float rumbleFullSpeed = 4f;
    [Range(0f, 1f)] public float rumbleVolume = .6f;
    [Range(.1f, 2f)] public float rumbleMinPitch = .75f, rumbleMaxPitch = 1.25f;
    [Tooltip("Fuel below this (capacity is 100) starts the repeating low-fuel beep.")]
    [Min(0)] public float lowFuelThreshold = 20f;
    [Min(.1f)] public float lowFuelSlowInterval = 2.2f, lowFuelFastInterval = .6f;
    [Range(0f, 1f)] public float musicVolume = .8f;
    [Min(.1f)] public float musicFadeOutSeconds = 3f;
    [Tooltip("The ending ride is longer than the track, so repeat it. Turn off if the loop seam is audible.")]
    public bool loopMusic = true;
}
