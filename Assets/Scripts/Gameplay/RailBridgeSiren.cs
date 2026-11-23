using Game.Audio;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Looping bridge siren and spinning beacons. Runs from the first lever hold until the run finishes (the train has crossed or
/// the run was lost). The siren plays to completion, waits <see cref="RailAudioBank.bridgeSirenGap"/> seconds, then repeats.
/// Every client derives this from synced run state and plays its own 3D copy, so no RPC is needed.
/// </summary>
public sealed class RailBridgeSiren : MonoBehaviour
{
    [Tooltip("Where the siren is heard from (the bridge control box).")]
    public Transform soundPoint;
    [Tooltip("Spinner roots under each warning light. Hidden while the siren is inactive.")]
    public Transform[] spinners = new Transform[0];
    public float degreesPerSecond = 240f;

    AudioHandle handle;
    bool playing;
    double nextAt;

    /// <summary>Siren runs once the lever has been held and stops when the run ends. A playing clip is never cut off.</summary>
    public static bool ShouldRun(bool inRun, bool bridgeStarted, bool finished) => inRun && bridgeStarted && !finished;

    /// <summary>Time of the next play after one ends: the configured gap later, never negative.</summary>
    public static double NextStart(double now, float gap) => now + Mathf.Max(0f, gap);

    void Update()
    {
        var run = RailExpedition.Instance;
        bool inRun = PhotonSession.OnTrain && PhotonSession.Instance != null && PhotonSession.Instance.WorldReady &&
                     PhotonSession.Phase == RailPhase.Running && run != null;
        bool active = ShouldRun(inRun, inRun && run.State.bridgeAt > 0, inRun && run.State.finished);
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

        foreach (var s in spinners)
        {
            if (s == null) continue;
            if (s.gameObject.activeSelf != active) s.gameObject.SetActive(active);
            if (active) s.Rotate(0, degreesPerSecond * Time.deltaTime, 0, Space.Self);
        }
    }

    void OnDisable() { if (playing) handle.Stop(); playing = false; nextAt = 0; }
}
