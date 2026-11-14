using System.Collections.Generic;
using Game.Audio;
using Photon.Pun;
using UnityEngine;

public static class RailAudio
{
    static RailAudioBank bank;
    public static RailAudioBank Bank
    {
        get
        {
            if (bank == null) bank = Resources.Load<RailAudioBank>("RailAudioBank");
            return bank;
        }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => bank = null;

    public static void Play(SoundEvent sound, Vector3 position, float volume = 1f, float pitchScale = 1f)
    {
        if (sound == null) return;
        var manager = AudioManager.instance;
        if (manager != null) manager.Play(sound, position, volume, null, pitchScale);
    }
}

/// <summary>
/// Drives the sounds that follow shared train state: rumble, rail joints, stop, fuel warnings,
/// coal deposits and the ending music. Read-only on session state, so every client hears the same events.
/// </summary>
[DefaultExecutionOrder(200)]
public sealed class RailAudioDirector : MonoBehaviour
{
    struct Cue { public double at; public SoundEvent sound; public Vector3 position; public float volume, pitch; }

    static RailAudioDirector instance;
    readonly List<Cue> cues = new List<Cue>();
    AudioSource rumble, music;
    float rumbleVolume, rumblePitch = 1f, pitchVelocity, aboardBlend, musicVolume;
    string musicRun;
    float prevDistance, prevFuel = -1f, nextWarning;
    bool haveDistance, wasMoving;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => instance = null;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (instance != null) return;
        var go = new GameObject("Rail Audio Director");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RailAudioDirector>();
    }
    void OnDestroy() { if (instance == this) instance = null; }

    AudioSource MakeSource(string name, bool loop)
    {
        var child = new GameObject(name);
        child.transform.SetParent(transform, false);
        var source = child.AddComponent<AudioSource>();
        source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0f;
        return source;
    }
    static UnityEngine.Audio.AudioMixerGroup EffectsGroup()
    {
        var manager = AudioManager.instance;
        return manager != null && manager.Settings != null ? manager.Settings.effects : null;
    }

    void Update()
    {
        var bank = RailAudio.Bank;
        if (bank == null) return;
        bool onTrain = PhotonSession.IsRailsRoom && PhotonSession.OnTrain;
        var train = onTrain ? RailTrain.Instance : null;
        RunCues();
        UpdateRumble(bank, train);
        UpdateMusic(bank, onTrain);
        if (train == null)
        {
            haveDistance = false; wasMoving = false; prevFuel = -1f;
            return;
        }
        UpdateMotion(bank, train);
        UpdateFuel(bank, train);
    }

    void RunCues()
    {
        if (cues.Count == 0) return;
        double now = Time.unscaledTimeAsDouble;
        for (int i = cues.Count - 1; i >= 0; i--)
        {
            if (cues[i].at > now) continue;
            RailAudio.Play(cues[i].sound, cues[i].position, cues[i].volume, cues[i].pitch);
            cues.RemoveAt(i);
        }
    }
    void Schedule(SoundEvent sound, Vector3 position, float delay, float pitch = 1f)
    {
        if (sound == null) return;
        cues.Add(new Cue { at = Time.unscaledTimeAsDouble + delay, sound = sound, position = position, volume = 1f, pitch = pitch });
    }

    /// <summary>Called once per committed furnace deposit on every client (see RailFurnaceModule.ShowFuelGain).</summary>
    public static void CoalDeposited(Vector3 at)
    {
        var bank = RailAudio.Bank;
        if (instance == null || bank == null) return;
        // Thud, then the flare-up, then the reward chime, so they read as one event.
        instance.Schedule(bank.coalThud, at, 0f);
        instance.Schedule(bank.fireFlare, at, .06f);
        instance.Schedule(bank.fuelChime, at, .18f, Random.Range(.97f, 1.05f));
    }

    void UpdateMotion(RailAudioBank bank, RailTrain train)
    {
        float speed = Mathf.Abs(train.Speed), distance = train.Distance;
        if (haveDistance)
        {
            int before = Mathf.FloorToInt(prevDistance / bank.railJointSpacing);
            int after = Mathf.FloorToInt(distance / bank.railJointSpacing);
            // A large jump is a re-sync or teleport, not wheels crossing joints.
            if (after > before && after - before <= 2 && speed > .3f)
                RailAudio.Play(bank.railJoint, train.transform.position, Mathf.Lerp(.55f, 1f, Mathf.Clamp01(speed / bank.rumbleFullSpeed)));
        }
        prevDistance = distance; haveDistance = true;
        if (speed > .2f) wasMoving = true;
        else if (speed <= .02f && wasMoving)
        {
            wasMoving = false;
            RailAudio.Play(bank.trainStop, train.transform.position);
        }
    }

    void UpdateFuel(RailAudioBank bank, RailTrain train)
    {
        bool running = PhotonSession.Phase == RailPhase.Running && PhotonSession.Instance != null && PhotonSession.Instance.WorldReady;
        if (!running) { prevFuel = -1f; return; }
        float fuel = PhotonSession.CurrentFuel;
        if (prevFuel > 0f && fuel <= 0f) RailAudio.Play(bank.outOfFuel, train.transform.position);
        if (fuel > 0f && fuel < bank.lowFuelThreshold)
        {
            if (Time.unscaledTime >= nextWarning)
            {
                RailAudio.Play(bank.lowFuelWarning, train.transform.position);
                nextWarning = Time.unscaledTime + Mathf.Lerp(bank.lowFuelFastInterval, bank.lowFuelSlowInterval, fuel / bank.lowFuelThreshold);
            }
        }
        else nextWarning = 0f;
        prevFuel = fuel;
    }

    static bool LocalAboard(RailTrain train)
    {
        var local = PhotonNetwork.LocalPlayer;
        return local != null && RailExpedition.Position(local.ActorNumber, out var head) && train.ContainsPadded(head);
    }

    // The rumble fades in with speed and its pitch follows speed, both smoothed so stops are gentle.
    void UpdateRumble(RailAudioBank bank, RailTrain train)
    {
        float targetVolume = 0f, targetPitch = rumblePitch;
        if (train != null && bank.rumbleLoop != null)
        {
            float n = Mathf.Clamp01(Mathf.Abs(train.Speed) / bank.rumbleFullSpeed);
            aboardBlend = Mathf.MoveTowards(aboardBlend, LocalAboard(train) ? 1f : 0f, Time.unscaledDeltaTime);
            targetVolume = bank.rumbleVolume * Mathf.SmoothStep(0f, 1f, n * 2f) * aboardBlend;
            targetPitch = Mathf.Lerp(bank.rumbleMinPitch, bank.rumbleMaxPitch, n);
        }
        rumbleVolume = Mathf.MoveTowards(rumbleVolume, targetVolume, Time.unscaledDeltaTime / .8f);
        rumblePitch = Mathf.SmoothDamp(rumblePitch, targetPitch, ref pitchVelocity, .4f, Mathf.Infinity, Time.unscaledDeltaTime);
        if (rumbleVolume <= .001f && targetVolume <= .001f)
        {
            if (rumble != null && rumble.isPlaying) rumble.Stop();
            return;
        }
        if (bank.rumbleLoop == null) return;
        if (rumble == null) rumble = MakeSource("Train rumble", true);
        if (rumble.clip != bank.rumbleLoop) rumble.clip = bank.rumbleLoop;
        rumble.outputAudioMixerGroup = EffectsGroup();
        rumble.volume = rumbleVolume; rumble.pitch = rumblePitch;
        if (!rumble.isPlaying) rumble.Play();
    }

    // Starts with a successful ending and fades out as the crew is sent back to the compound.
    void UpdateMusic(RailAudioBank bank, bool onTrain)
    {
        var expedition = RailExpedition.Instance;
        string run = PhotonSession.RunId;
        bool active = onTrain && bank.bridgeMusic != null && expedition != null && expedition.State.finished &&
            expedition.State.success && expedition.State.run == run &&
            (PhotonSession.Phase == RailPhase.Ending || PhotonSession.Phase == RailPhase.Returning);
        float target = 0f;
        if (active)
        {
            if (musicRun != run)
            {
                musicRun = run;
                if (music == null) music = MakeSource("Bridge music", false);
                music.clip = bank.bridgeMusic; music.loop = bank.loopMusic;
                music.outputAudioMixerGroup = EffectsGroup();
                musicVolume = 0f; music.volume = 0f; music.Play();
            }
            double deadline = PhotonSession.Get(RailSessionRules.Deadline, 0d) + RailSessionRules.ReturnDelaySeconds;
            float remaining = (float)(deadline - PhotonNetwork.Time);
            float fade = PhotonSession.Phase == RailPhase.Returning ? 0f : Mathf.Clamp01(remaining / bank.musicFadeOutSeconds);
            target = bank.musicVolume * fade;
        }
        if (music == null) return;
        float rate = target > musicVolume ? .5f : (active ? .15f : 1f);
        musicVolume = Mathf.MoveTowards(musicVolume, target, Time.unscaledDeltaTime * bank.musicVolume / rate);
        music.volume = musicVolume;
        if (!active && musicVolume <= 0f && music.isPlaying) music.Stop();
    }
}
