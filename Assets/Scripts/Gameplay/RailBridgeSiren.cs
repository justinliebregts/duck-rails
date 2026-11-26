using Game.Audio;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Looping bridge siren and runway-style warning lights. Runs from the moment the bridge is fully lowered until the run finishes.
/// The siren plays to completion, waits <see cref="RailAudioBank.bridgeSirenGap"/> seconds, then repeats. The warning post caps
/// pulse in sequence toward the bridge; each step is a group that flashes together. Every client derives this from synced run
/// state and plays its own copy, so no RPC is needed.
/// </summary>
public sealed class RailBridgeSiren : MonoBehaviour
{
    [System.Serializable] public struct Step { public Renderer[] renderers, posts; }

    [Tooltip("Test hook: run the siren and light chase without a live run (used for recordings and preview).")]
    public bool previewActive;
    [Tooltip("Where the siren is heard from (the bridge control box).")]
    public Transform soundPoint;
    [Tooltip("Warning caps in lighting order, nearest the train first. Renderers in one step flash together.")]
    public Step[] steps = new Step[0];
    [Tooltip("Seconds between one step lighting and the next.")]
    public float stepInterval = .22f;
    [Tooltip("Seconds for a lit step to fade back to dim.")]
    public float fade = .5f;
    [Tooltip("Pause after the last step before the sequence restarts.")]
    public float restPause = .35f;
    [Tooltip("Emission multipliers: dim between pulses and peak at the flash.")]
    public float dimLevel = .12f, peakLevel = 2.2f;
    [Tooltip("Post glow shell brightness at the flash, as a multiple of the cap emission colour. Shells are hidden between pulses.")]
    public float postPeakLevel = 1.4f;
    [Tooltip("Pulse strength below which a post glow shell is hidden.")]
    [Range(0f, 1f)] public float postShowThreshold = .06f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    AudioHandle handle;
    bool playing, lit;
    double nextAt;
    MaterialPropertyBlock block;

    /// <summary>Siren runs once the bridge is 100% lowered and stops when the run ends. A playing clip is never cut off.</summary>
    public static bool ShouldRun(bool inRun, bool bridgeStarted, bool finished) => inRun && bridgeStarted && !finished;

    /// <summary>Time of the next play after one ends: the configured gap later, never negative.</summary>
    public static double NextStart(double now, float gap) => now + Mathf.Max(0f, gap);

    /// <summary>0..1 flash strength for a step: jumps to 1 when the step fires, then fades; 0 before it fires in the cycle.</summary>
    public static float Pulse(float time, int step, float interval, float cycle, float fade)
    {
        float t = cycle > 0 ? Mathf.Repeat(time, cycle) : 0;
        float age = t - step * interval;
        if (age < 0 || fade <= 0) return 0;
        float k = Mathf.Clamp01(1 - age / fade);
        return k * k;
    }

    void Update()
    {
        var run = RailExpedition.Instance;
        bool inRun = PhotonSession.OnTrain && PhotonSession.Instance != null && PhotonSession.Instance.WorldReady &&
                     PhotonSession.Phase == RailPhase.Running && run != null;
        bool active = ShouldRun(inRun, inRun && run.State.BridgeProgress(PhotonNetwork.Time) >= 1, inRun && run.State.finished);
        active |= previewActive;
        double now = Time.realtimeSinceStartupAsDouble;

        if (playing && !handle.IsPlaying) { playing = false; nextAt = NextStart(now, RailAudio.Bank != null ? RailAudio.Bank.bridgeSirenGap : 5f); }
        if (!active && !playing) nextAt = 0;
        if (active && !playing && now >= nextAt)
        {
            var bank = RailAudio.Bank;
            var manager = AudioManager.instance;
            if (bank != null && bank.bridgeSiren != null && manager != null)
            {
                handle = manager.Play(bank.bridgeSiren, (soundPoint != null ? soundPoint : transform).position);
                playing = handle.IsPlaying;
            }
            if (!playing) nextAt = NextStart(now, RailAudio.Bank != null ? RailAudio.Bank.bridgeSirenGap : 5f);
        }

        UpdateLights(active);
    }

    void UpdateLights(bool active)
    {
        if (!active)
        {
            if (lit) { Clear(); lit = false; }
            return;
        }
        lit = true;
        if (block == null) block = new MaterialPropertyBlock();
        float cycle = steps.Length * stepInterval + restPause;
        for (int i = 0; i < steps.Length; i++)
        {
            float pulse = Pulse(Time.time, i, stepInterval, cycle, fade);
            float level = Mathf.Lerp(dimLevel, peakLevel, pulse);
            Color glow = Color.red;
            foreach (var r in steps[i].renderers)
            {
                if (r == null) continue;
                glow = r.sharedMaterial != null ? r.sharedMaterial.GetColor(EmissionColor) : Color.red;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColor, glow * level);
                r.SetPropertyBlock(block);
            }
            foreach (var r in steps[i].posts)
            {
                if (r == null) continue;
                bool show = pulse > postShowThreshold;
                if (r.enabled != show) r.enabled = show;
                if (!show) continue;
                r.GetPropertyBlock(block);
                block.SetColor(EmissionColor, glow * (postPeakLevel * pulse));
                r.SetPropertyBlock(block);
            }
        }
    }

    void Clear()
    {
        foreach (var s in steps) { foreach (var r in s.renderers) if (r != null) r.SetPropertyBlock(null); foreach (var r in s.posts) if (r != null) { r.SetPropertyBlock(null); r.enabled = false; } }
    }

    void OnDisable() { if (playing) handle.Stop(); playing = false; nextAt = 0; if (lit) Clear(); lit = false; }
}
