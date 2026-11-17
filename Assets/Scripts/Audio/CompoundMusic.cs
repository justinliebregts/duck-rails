using System.Collections;
using UnityEngine;

/// <summary>Scene-owned music with an explicit pre-unload fade.</summary>
[RequireComponent(typeof(AudioSource))]
public sealed class CompoundMusic : MonoBehaviour
{
    [SerializeField, Min(0f)] float fadeInSeconds = 1.5f;
    [SerializeField, Min(0f)] float fadeOutSeconds = 0.75f;
    AudioSource source;
    float fullVolume;
    bool stopping;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        fullVolume = source.volume;
        source.playOnAwake = false;
        source.volume = 0f;
    }

    void OnEnable()
    {
        stopping = false;
        source.volume = 0f;
        source.Play();
    }

    void Update()
    {
        // Wait for the streaming source before advancing its entry envelope.
        if (!stopping && !source.isPlaying) return;
        float seconds = stopping ? fadeOutSeconds : fadeInSeconds;
        source.volume = Mathf.MoveTowards(source.volume, stopping ? 0f : fullVolume,
            seconds > 0f ? fullVolume * Time.unscaledDeltaTime / seconds : fullVolume);
        if (stopping && source.volume <= 0f) source.Stop();
    }

    void OnDisable()
    {
        source.Stop();
        source.volume = 0f;
    }

    public static void BeginSceneExit()
    {
        foreach (var music in FindObjectsByType<CompoundMusic>(FindObjectsSortMode.None))
            music.stopping = true;
    }

    public static IEnumerator WaitForSceneExit()
    {
        var tracks = FindObjectsByType<CompoundMusic>(FindObjectsSortMode.None);
        foreach (var music in tracks)
            while (music != null && music.isActiveAndEnabled && music.source.volume > 0f)
                yield return null;
    }
}
