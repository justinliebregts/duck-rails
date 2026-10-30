using System;
using System.Collections;
using Game.Animation;
using Game.Audio;
using UnityEngine;
using VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime;

// Boots the duck Voice Patrol prefab, keeps private rooms unmonitored, and
// mirrors warn/mute events onto the watch and the warning sting.
public class VoicePatrolManager : MonoBehaviour
{
    const string PrefabResource = "VoicePatrol_Duck";
    const float ToxicSpeechDelay = 3f;
    const float LanguageWarningSeconds = 30f;
    const string LanguageWarningText = "Watch your language!";

    public static VoicePatrolManager Instance { get; private set; }

    [SerializeField] SoundEvent warningSound;
    [SerializeField] SoundEvent toxicSpeechSound;
    [SerializeField] SoundEvent muteOverSound;

    DuckVoicePatrol patrol;
    PhotonSession hookedSession;
    bool duckReady;
    bool started;
    bool watchShowing;
    float nextWatchRefresh;
    float languageWarningUntil;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void Boot()
    {
        if (!IsEnabled)
            return;

        if (Instance != null || FindAnyObjectByType<VoicePatrolManager>() != null)
            return;

        GameObject prefab = Resources.Load<GameObject>(PrefabResource);
        if (prefab == null)
        {
            {
                if (DuckzLog.AllowError(out var logNotice1))
                    logNotice1.Write("Voice Patrol prefab missing from Resources (" + PrefabResource + ").");
            }
            return;
        }

        Instantiate(prefab);
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        patrol = GetComponent<DuckVoicePatrol>();
        DontDestroyOnLoad(gameObject);
    }

    void OnEnable()
    {
        VoicePatrolBase.OnWarningIssued += HandleWarning;
        VoicePatrolBase.OnMuteApplied += HandleMuteApplied;
        VoicePatrolBase.OnMuteExpired += HandleMuteExpired;
        VoicePatrolWrapper.OnAgeVerified += HandleAgeVerified;
    }

    void OnDisable()
    {
        VoicePatrolBase.OnWarningIssued -= HandleWarning;
        VoicePatrolBase.OnMuteApplied -= HandleMuteApplied;
        VoicePatrolBase.OnMuteExpired -= HandleMuteExpired;
        VoicePatrolWrapper.OnAgeVerified -= HandleAgeVerified;
        if (hookedSession != null)
            hookedSession.RoomMembershipChanged -= RefreshPrivateRoom;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void NotifyLocalDuck()
    {
        duckReady = true;
        TryInitialize();
    }

    public void LoadBan(Action<VoiceBanBlob> completed)
    {
        StartCoroutine(LoadBanRoutine(completed));
    }

    public void SaveBan(VoiceBanBlob blob)
    {
        if (blob == null)
        {
            if (DuckzLog.AllowWarning(out var logNotice2))
                logNotice2.Write("[VoicePatrol:Photon-Duck] Ban not saved. No ban data.");
            return;
        }

        PlayerSession session = PlayerSession.Instance;
        if (!CanUseCloud(session))
        {
            if (session?.Platform != "EDITOR" && DuckzLog.AllowWarning(out var logNotice3))
                logNotice3.Write("[VoicePatrol:Photon-Duck] Ban not saved: missing session, platform or player ID.");
            return;
        }

        StartCoroutine(SaveBanRoutine(blob));
    }

    void Update()
    {
        TryInitialize();
        TrackSession();
        RefreshWatch();
    }

    void TryInitialize()
    {
        if (!IsEnabled)
            return;

        if (!duckReady && PhotonPlayer.Local != null)
            duckReady = true;

        if (started || !duckReady || patrol == null || PlayerSession.Instance == null)
            return;

        started = true;
        PlayerSession session = PlayerSession.Instance;
        string uniqueId = string.IsNullOrEmpty(session.GrId) ? session.PlayerId : session.GrId;
        patrol.Initialize(uniqueId);
        RefreshPrivateRoom();
    }

    // Fail closed in login/test scenes without a GameManager, too.
    static bool IsEnabled => GameManager.instance != null && GameManager.instance.enableVoicePatrol;

    void TrackSession()
    {
        PhotonSession session = PhotonSession.Instance;
        if (session == hookedSession)
            return;

        if (hookedSession != null)
            hookedSession.RoomMembershipChanged -= RefreshPrivateRoom;

        hookedSession = session;
        if (hookedSession != null)
            hookedSession.RoomMembershipChanged += RefreshPrivateRoom;

        RefreshPrivateRoom();
    }

    void RefreshPrivateRoom()
    {
        if (patrol == null)
            return;

        bool privateRoom = false;
        PhotonSession session = PhotonSession.Instance;
        if (session != null && session.HasCurrentRoom(out _, out bool isPublic))
            privateRoom = !isPublic;

        patrol.SetPrivateRoom(privateRoom);
    }

    void RefreshWatch()
    {
        bool muted = VoicePatrolBase.Instance != null && VoicePatrolBase.Instance.IsPunishmentMuted();
        if (muted)
        {
            if (watchShowing && Time.unscaledTime < nextWatchRefresh)
                return;

            watchShowing = true;
            nextWatchRefresh = Time.unscaledTime + 1f;
            TimeSpan remaining = VoicePatrolBase.Instance.GetRemainingPunishmentDuration();
            LocalWatch()?.SetPunishmentCountdown(remaining);
            return;
        }

        if (Time.unscaledTime < languageWarningUntil)
        {
            if (watchShowing && Time.unscaledTime < nextWatchRefresh)
                return;

            watchShowing = true;
            nextWatchRefresh = Time.unscaledTime + 1f;
            LocalWatch()?.SetStatus(LanguageWarningText);
            return;
        }

        if (!watchShowing)
            return;

        watchShowing = false;
        LocalWatch()?.SetPunishmentCountdown(null);
    }

    static Watch LocalWatch()
    {
        // The wrist watch lives on the local body (DesktopPlayer in the editor, DuckPlayer on a headset).
        // The networked PhotonPlayer is a separate object and does not have one.
        for (int i = 0; i < LocalBodyDriver.Active.Count; i++)
        {
            LocalBodyDriver driver = LocalBodyDriver.Active[i];
            if (driver == null)
                continue;

            Watch watch = driver.GetComponentInChildren<Watch>(true);
            if (watch != null)
                return watch;
        }

        PhotonPlayer player = PhotonPlayer.Local;
        if (player == null)
            return null;

        return player.GetComponentInChildren<Watch>(true);
    }

    public void RemindExistingMute()
    {
        PlayWarning(includeToxicSpeech: false);
        watchShowing = true;
        nextWatchRefresh = Time.unscaledTime + 1f;
        if (VoicePatrolBase.Instance != null)
            LocalWatch()?.SetPunishmentCountdown(VoicePatrolBase.Instance.GetRemainingPunishmentDuration());
    }

    void HandleWarning(string _)
    {
        PlayWarning(includeToxicSpeech: true);
        languageWarningUntil = Time.unscaledTime + LanguageWarningSeconds;
        watchShowing = true;
        nextWatchRefresh = Time.unscaledTime + 1f;
        LocalWatch()?.SetStatus(LanguageWarningText);
    }

    void HandleMuteApplied(DateTime _, string __)
    {
        PlayWarning(includeToxicSpeech: true);
        PhotonVoiceChat.SetPunishmentMuted(true);
        watchShowing = true;
        nextWatchRefresh = Time.unscaledTime + 1f;
        if (VoicePatrolBase.Instance != null)
            LocalWatch()?.SetPunishmentCountdown(VoicePatrolBase.Instance.GetRemainingPunishmentDuration());
    }

    void HandleMuteExpired()
    {
        PlayMuteOver();
        PhotonVoiceChat.SetPunishmentMuted(false);
        watchShowing = false;
        LocalWatch()?.SetPunishmentCountdown(null);
    }

    static void HandleAgeVerified(VoicePatrolWrapper.AgeVerificationData data)
    {
        PlayerPrefs.SetInt("VoicePatrolMaxAge", (int)data.MaxAge);
        PlayerPrefs.Save();
    }

    void PlayMuteOver()
    {
        if (muteOverSound == null || AudioManager.instance == null)
            return;

        Watch watch = LocalWatch();
        Vector3 position = watch != null ? watch.transform.position : Vector3.zero;
        AudioManager.instance.Play(muteOverSound, position);
    }

    void PlayWarning(bool includeToxicSpeech)
    {
        if (AudioManager.instance == null)
            return;

        Vector3 position = PhotonPlayer.Local != null
            ? PhotonPlayer.Local.transform.position
            : Vector3.zero;
        if (warningSound != null)
            AudioManager.instance.Play(warningSound, position);
        if (includeToxicSpeech && toxicSpeechSound != null)
            StartCoroutine(PlayToxicSpeechAfterSiren(position));
    }

    IEnumerator PlayToxicSpeechAfterSiren(Vector3 position)
    {
        yield return new WaitForSecondsRealtime(ToxicSpeechDelay);
        if (toxicSpeechSound != null && AudioManager.instance != null)
            AudioManager.instance.Play(toxicSpeechSound, position);
    }

    IEnumerator LoadBanRoutine(Action<VoiceBanBlob> completed)
    {
        PlayerSession session = PlayerSession.Instance;
        if (!CanUseCloud(session))
        {
            completed?.Invoke(null);
            yield break;
        }

        DuckzApiClient.Response<VoiceBanRecord> response = null;
        yield return DuckzApiClient.Get(BanPath(session), (DuckzApiClient.Response<VoiceBanRecord> value) => response = value);

        VoiceBanBlob blob = null;
        if (response != null && response.Success && response.Data != null)
            blob = response.Data.blob;
        else if (response == null || response.Code != 404)
            {
                if (DuckzLog.AllowWarning(out var logNotice4))
                    logNotice4.Write("Voice Patrol ban could not be loaded" + (response != null ? " (" + response.Code + ")." : "."));
            }

        completed?.Invoke(blob);
    }

    IEnumerator SaveBanRoutine(VoiceBanBlob blob)
    {
        PlayerSession session = PlayerSession.Instance;
        if (!CanUseCloud(session))
        {
            if (session?.Platform != "EDITOR" && DuckzLog.AllowWarning(out var logNotice5))
                logNotice5.Write("[VoicePatrol:Photon-Duck] Ban not saved: session unavailable before the request.");
            yield break;
        }

        string path = BanPath(session);
        DuckzLog.Log("[VoicePatrol:Photon-Duck] POST " + path + " violations=" + blob.violationCount);
        DuckzApiClient.Response<long> response = null;
        yield return DuckzApiClient.PostSigned(path, blob, (DuckzApiClient.Response<long> value) => response = value);

        if (response != null && response.Success)
        {
            DuckzLog.Log("[VoicePatrol:Photon-Duck] Ban saved (" + response.Code + ") " + path + "\n" + response.Body);
            yield break;
        }

        long code = response != null ? response.Code : 0;
        string message = response != null ? response.Message : "No API response.";
        string body = response != null ? response.Body : "";
        if (DuckzLog.AllowWarning(out var logNotice6))
            logNotice6.Write("[VoicePatrol:Photon-Duck] Ban not saved (" + code + ") " + path + "\n" + message);
        DuckzLog.Log("[VoicePatrol:Photon-Duck] Ban save response: " + body);
    }

    static bool CanUseCloud(PlayerSession session)
    {
        if (session == null)
            return false;
        if (string.IsNullOrEmpty(session.PlayerId) || string.IsNullOrEmpty(session.Platform))
            return false;
        // Editor sessions use platform EDITOR, which the prefab route rejects.
        return session.Platform != "EDITOR";
    }

    static string BanPath(PlayerSession session)
    {
        return "player/prefab/dz/voicebandata/"
            + Uri.EscapeDataString(session.Platform)
            + "/"
            + Uri.EscapeDataString(session.PlayerId);
    }
}

[Serializable]
public class VoiceBanBlob
{
    public int violationCount;
    public long muteEndTimeTicks;
    public string muteReason;
}

[Serializable]
public class VoiceBanRecord
{
    public long id;
    public VoiceBanBlob blob;
}
