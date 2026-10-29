using Photon.Voice.PUN;
using Photon.Voice.Unity;
using Photon.Voice.Unity.UtilityScripts;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Open-mic Photon Voice. Remote speech plays from that duck's head and is silent past 30m.
/// </summary>
public class PhotonVoiceChat : MonoBehaviour
{
    public const float FullVolumeDistance = 1.5f;
    public const float SilentDistance = 40f;
    // Extra loudness when you're next to someone. The rolloff cancels this past a few meters.
    // Applied by the Voice group in GameAudio.mixer (+12 dB); keep the two in sync.
    public const float CloseGainDb = 12f;
    public const string ChatVolumePrefsKey = "ChatVolume";
    public const int DefaultChatVolume = 100;
    const string VoiceVolumeParameter = "VoiceVolume";
    const float SilenceDb = -80f;
    const float CloseBoostDistance = 2f;

    PunVoiceClient _client;
    Recorder _recorder;
    static bool punishmentMuted;

    public static bool PunishmentMuted => punishmentMuted;

    /// <summary>True while this player should not be heard: pause-menu mute or a Voice Patrol ban.</summary>
    public static bool IsLocallyMuted { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (FindAnyObjectByType<PhotonVoiceChat>() != null)
            return;

        var gameObject = new GameObject(nameof(PhotonVoiceChat));
        DontDestroyOnLoad(gameObject);
        gameObject.AddComponent<PhotonVoiceChat>();
    }

    void Awake()
    {
        _client = gameObject.AddComponent<PunVoiceClient>();
        _client.UsePunAppSettings = true;
        _client.UsePunAuthValues = true;
        _client.AutoConnectAndJoin = true;

        _recorder = gameObject.AddComponent<Recorder>();
        _recorder.TransmitEnabled = true;
        _recorder.VoiceDetection = true;
        _recorder.RecordWhenJoined = true;
        _recorder.RecordingEnabled = false;
        _client.PrimaryRecorder = _recorder;

        if (UserPlayerPrefs.IsReady)
            ApplySavedUserAudio();
        else
            UserPlayerPrefs.UserReady += OnUserPrefsReady;

        // Asks immediately on Quest when the mic isn't already allowed, then starts the open mic.
        gameObject.AddComponent<MicrophonePermission>();
        gameObject.AddComponent<WalkieRadio>();
    }

    void OnDestroy()
    {
        UserPlayerPrefs.UserReady -= OnUserPrefsReady;
    }

    void OnUserPrefsReady()
    {
        UserPlayerPrefs.UserReady -= OnUserPrefsReady;
        ApplySavedUserAudio();
    }

    static void ApplySavedUserAudio()
    {
        PlayerListItem.ApplySavedSelfMute();
        SetIncomingVolume(UserPlayerPrefs.GetInt(ChatVolumePrefsKey, DefaultChatVolume));
    }

    // Chat slider percent of the current incoming level. 100 keeps CloseGainDb. Does not change the mic.
    public static void SetIncomingVolume(int percent)
    {
        var settings = Resources.Load<Game.Audio.AudioSettings>("GameAudioSettings");
        AudioMixer mixer = settings != null && settings.voice != null ? settings.voice.audioMixer : null;
        if (mixer == null)
            return;

        mixer.SetFloat(VoiceVolumeParameter, IncomingVolumeDb(percent));
    }

    static float IncomingVolumeDb(int percent)
    {
        if (percent <= 0)
            return SilenceDb;
        return CloseGainDb + 20f * Mathf.Log10(percent / 100f);
    }

    // Keeps the microphone and walkie tap. RecordingEnabled would tear both down.
    public static void SetSelfMuted(bool muted)
    {
        if (punishmentMuted)
            muted = true;
        ApplyTransmit(muted);
    }

    // Voice Patrol holds this until the mute timer ends. Self-mute cannot clear it.
    public static void SetPunishmentMuted(bool muted)
    {
        punishmentMuted = muted;
        ApplyTransmit(muted || PlayerListItem.ReadSelfMuted());
        PlayerListItem.ShowLocalMuteLocked();
    }

    static void ApplyTransmit(bool muted)
    {
        IsLocallyMuted = muted;

        var chat = FindAnyObjectByType<PhotonVoiceChat>();
        if (chat == null || chat._recorder == null)
            return;

        chat._recorder.TransmitEnabled = !muted;
    }

    public static AudioSource Attach(GameObject player, Transform head, bool companion = false)
    {
        if (player.GetComponent<PhotonVoiceView>() != null)
        {
            Speaker speaker = player.GetComponentInChildren<Speaker>(true);
            return speaker != null ? speaker.GetComponent<AudioSource>() : null;
        }

        var voice = new GameObject("Voice");
        voice.transform.SetParent(head != null ? head : player.transform, false);

        var source = voice.AddComponent<AudioSource>();
        source.spatialBlend = 1f;
        source.spatialize = false;
        source.dopplerLevel = 0f;
        source.spread = 0f;
        source.rolloffMode = AudioRolloffMode.Custom;
        source.minDistance = FullVolumeDistance;
        source.maxDistance = SilentDistance;
        source.SetCustomCurve(AudioSourceCurveType.CustomRolloff, CloseBoostedRolloff());
        source.playOnAwake = false;
        source.loop = false;
        source.bypassReverbZones = false;
        source.priority = 32;
        // Voice retains its own source; only mixer routing is shared with game audio.
        // The Voice group carries CloseGainDb, so voice is far too quiet without it.
        var audioManager = Game.Audio.AudioManager.instance;
        var settings = audioManager != null && audioManager.Settings != null
            ? audioManager.Settings
            : Resources.Load<Game.Audio.AudioSettings>("GameAudioSettings");
        if (settings != null)
            source.outputAudioMixerGroup = settings.voice;

        if (companion)
        {
            // Each peer creates its own source from the same companion prefab;
            // no authority gate or RPC is needed for authored audio settings.
            player.GetComponent<DuckCompanion>()?.voiceSettings?.Apply(source);
            voice.AddComponent<PhotonVoiceLevel>();
        }
        else
        {
            voice.AddComponent<Speaker>();
            player.AddComponent<PhotonVoiceView>();
        }
        return source;
    }

    static AnimationCurve CloseBoostedRolloff()
    {
        float gain = Mathf.Pow(10f, CloseGainDb / 20f);
        float[] distances = { 0f, CloseBoostDistance, 8f, 16f, 24f, SilentDistance };
        var values = new float[distances.Length];
        for (int i = 0; i < distances.Length; i++)
        {
            float distance = distances[i];
            values[i] = distance <= CloseBoostDistance
                ? 1f
                : Mathf.Clamp01(LinearVolume(distance) / gain);
        }

        var keys = new Keyframe[distances.Length];
        for (int i = 0; i < distances.Length; i++)
        {
            float time = distances[i] / SilentDistance;
            float inTangent = 0f;
            float outTangent = 0f;
            if (i > 0)
            {
                float dt = (distances[i] - distances[i - 1]) / SilentDistance;
                inTangent = (values[i] - values[i - 1]) / dt;
            }
            if (i < distances.Length - 1)
            {
                float dt = (distances[i + 1] - distances[i]) / SilentDistance;
                outTangent = (values[i + 1] - values[i]) / dt;
            }
            keys[i] = new Keyframe(time, values[i], inTangent, outTangent);
        }

        return new AnimationCurve(keys);
    }

    static float LinearVolume(float distance)
    {
        if (distance <= FullVolumeDistance)
            return 1f;
        if (distance >= SilentDistance)
            return 0f;
        return (SilentDistance - distance) / (SilentDistance - FullVolumeDistance);
    }
}
