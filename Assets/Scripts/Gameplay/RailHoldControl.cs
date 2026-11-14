using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Momentary station controls. Expiring, run-scoped holds cannot leak into another expedition.</summary>
[DefaultExecutionOrder(100)]
public sealed class RailHoldControl : MonoBehaviour
{
    public PressableButton button;
    public bool radio;
    public bool bridge;
    public const string BridgeKey = "rails.bridgeHold";
    AudioSource source;
    AudioClip clip;
    bool held, focused = true, paused;
    bool hornActive, hornFading;
    AudioClip lastHorn;
    const float HornFadeSeconds = .15f;
    double nextHeartbeat;
    string heldRun;
    readonly HashSet<int> listeners = new HashSet<int>();
    readonly HashSet<int> current = new HashSet<int>();
    string Key => bridge ? BridgeKey : radio ? "rails.radioHold" : "rails.hornHold";
    void Start()
    {
        if (bridge) return;
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false; source.spatialBlend = 1;
        if (radio) gameObject.AddComponent<WalkieSpeaker>().Configure(0, true);
        else
        {
            source.loop = false; source.maxDistance = 100; source.rolloffMode = AudioRolloffMode.Linear;
            // Fallback only, used when RailAudioBank has no horn clips. The real horn is a one-shot.
            // Both harmonics finish a whole number of cycles, making the one-second loop seamless.
            var samples = new float[24000];
            for (int i = 0; i < samples.Length; i++)
                samples[i] = .4f * Mathf.Sin(2 * Mathf.PI * 130 * i / 24000f) + .15f * Mathf.Sin(2 * Mathf.PI * 195 * i / 24000f);
            clip = AudioClip.Create("Held train horn", samples.Length, 1, 24000, false);
            clip.SetData(samples, 0); source.clip = clip;
        }
    }
    AudioClip PickHorn()
    {
        var horns = RailAudio.Bank != null ? RailAudio.Bank.hornClips : null;
        if (horns == null || horns.Length == 0) return clip;
        int pick = Random.Range(0, horns.Length);
        if (horns.Length > 1 && horns[pick] == lastHorn) pick = (pick + 1) % horns.Length;
        return lastHorn = horns[pick];
    }
    // One-shot while any nearby holder presses: it never loops if held past its end, and fades out quickly on release.
    void UpdateHorn(bool pressed)
    {
        if (pressed)
        {
            if (hornActive) return;
            hornActive = true; hornFading = false;
            source.clip = PickHorn();
            if (source.clip == null) return;
            source.loop = source.clip == clip;
            source.outputAudioMixerGroup = Game.Audio.AudioManager.instance != null && Game.Audio.AudioManager.instance.Settings != null
                ? Game.Audio.AudioManager.instance.Settings.effects : null;
            source.volume = 1f; source.Play();
            return;
        }
        if (hornActive) { hornActive = false; hornFading = source.isPlaying; }
        if (!hornFading) return;
        source.volume = Mathf.MoveTowards(source.volume, 0f, Time.unscaledDeltaTime / HornFadeSeconds);
        if (source.volume <= 0f || !source.isPlaying) { source.Stop(); source.volume = 1f; hornFading = false; }
    }
    void LateUpdate()
    {
        bool allowed = focused && !paused && PhotonSession.OnTrain && PhotonSession.Instance != null &&
            PhotonSession.Instance.WorldReady && !RailWorld.InputBlocked && !RailExpedition.LocalDown;
        if (bridge) allowed &= PhotonSession.Phase == RailPhase.Running && RailExpedition.Instance != null &&
            RailExpedition.Instance.State.BridgeProgress(PhotonNetwork.Time) < 1;
        bool want = allowed && button != null && button.IsPressed && (!radio || !PhotonVoiceChat.IsLocallyMuted);
        if (held && heldRun != PhotonSession.RunId) SetHeld(false);
        if (want != held) SetHeld(want);
        if (held && PhotonNetwork.Time >= nextHeartbeat) Publish(true);
        if (bridge) return;
        current.Clear();
        if (PhotonSession.OnTrain)
            foreach (var player in PhotonNetwork.PlayerList)
            {
                bool active = player.IsLocal ? held : IsLive(player.CustomProperties[Key], PhotonSession.RunId, PhotonNetwork.Time);
                if (active && !player.IsInactive && RailExpedition.Position(player.ActorNumber, out var position) &&
                    Vector3.Distance(position, transform.position) < 4) current.Add(player.ActorNumber);
            }
        if (radio)
        {
            foreach (int actor in current) if (!listeners.Contains(actor)) WalkieRadio.SetTransmitting(actor, -100000 - actor, true);
            foreach (int actor in listeners) if (!current.Contains(actor)) WalkieRadio.ClearView(-100000 - actor);
            listeners.Clear(); listeners.UnionWith(current);
        }
        else if (source != null)
        {
            UpdateHorn(current.Count > 0);
        }
    }
    public static bool IsLive(object value, string run, double now) => value is object[] data && data.Length == 2 &&
        data[0] is string id && id == run && data[1] is double until && until > now && until <= now + 2;
    void SetHeld(bool value)
    {
        held = value; heldRun = PhotonSession.RunId;
        Publish(value);
    }
    void Publish(bool value)
    {
        nextHeartbeat = PhotonNetwork.Time + .25;
        if (PhotonNetwork.InRoom) PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable {
            [Key] = new object[] { heldRun ?? "", value ? PhotonNetwork.Time + 1 : 0d } });
    }
    void StopHold()
    {
        if (button != null) button.Release();
        if (held) SetHeld(false);
        foreach (int actor in listeners) WalkieRadio.ClearView(-100000 - actor);
        listeners.Clear();
        hornActive = hornFading = false;
        if (source != null) { source.Stop(); source.volume = 1f; }
    }
    void OnApplicationFocus(bool value) { focused = value; if (!value) StopHold(); }
    void OnApplicationPause(bool value) { paused = value; if (value) StopHold(); }
    void OnDisable() { StopHold(); }
    void OnDestroy() { if (clip != null) Destroy(clip); }
}
