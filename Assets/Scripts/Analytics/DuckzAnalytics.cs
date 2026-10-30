using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GameAnalyticsSDK;
using Photon.Pun;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Duck Rails analytics; the class name preserves inherited interaction call sites.
/// Call Ensure() early; configure platform keys in
/// Window → GameAnalytics → Settings before expecting events in a build.
/// </summary>
public sealed class DuckzAnalytics : MonoBehaviour
{
    const int GoldTarget = 40;
    const int GoldLow = 30;
    const int GoldHigh = 50;
    const float PresenceFlushSeconds = 45f;
    const float FirstMinutes = 300f;
    const int MaxSegmentLength = 16;

    static readonly Regex NonSegment = new("[^a-zA-Z0-9_]", RegexOptions.Compiled);

    public static DuckzAnalytics Instance { get; private set; }

    readonly Dictionary<string, float> dwellSeconds = new Dictionary<string, float>(8);
    readonly HashSet<string> visitedAreas = new HashSet<string>();
    readonly HashSet<string> sessionFirsts = new HashSet<string>();

    float aliveSince = -1f;
    float sessionStarted = -1f;
    float presenceFlushAt;
    string currentAreaKey = "other";
    bool initialized;
    bool initializationAttempted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Bootstrap() => Ensure();

    public static DuckzAnalytics Ensure()
    {
        if (Instance != null)
            return Instance;
        var go = new GameObject(nameof(DuckzAnalytics));
        DontDestroyOnLoad(go);
        return go.AddComponent<DuckzAnalytics>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        sessionStarted = Time.realtimeSinceStartup;
        presenceFlushAt = Time.realtimeSinceStartup + PresenceFlushSeconds;
        SceneManager.sceneLoaded += OnSceneLoaded;
        EnsureSdk();
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    void OnApplicationPause(bool paused)
    {
        if (paused)
            FlushPresence(force: true);
    }

    void OnApplicationQuit() => FlushPresence(force: true);

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == PhotonSession.PlayScene || scene.name == GameScenes.Expedition)
            MarkAlive();
    }

    void Update()
    {
        if (!initialized)
            EnsureSdk();
        TickPresence();
    }

    void EnsureSdk()
    {
        if (initializationAttempted)
            return;
        initializationAttempted = true;
        // Editor calls are local previews, never uploads. Avoid the SDK's noisy stubs.
        if (Application.isEditor) { initialized = true; return; }
        var settings = GameAnalytics.SettingsGA;
        int platform = settings.Platforms.IndexOf(Application.platform);
        if (platform < 0 || string.IsNullOrEmpty(settings.GetGameKey(platform)) || string.IsNullOrEmpty(settings.GetSecretKey(platform)))
        {
            Debug.LogWarning("Duck Rails analytics disabled: configure GameAnalytics keys for " + Application.platform);
            return;
        }
        if (FindAnyObjectByType<GameAnalytics>() == null)
        {
            var ga = new GameObject("GameAnalytics");
            DontDestroyOnLoad(ga);
            ga.AddComponent<GameAnalytics>();
        }
        if (!GameAnalytics.Initialized)
            GameAnalytics.Initialize();
        initialized = GameAnalytics.Initialized;
    }

    static bool Ready => Instance != null && Instance.initialized && (Application.isEditor || GameAnalytics.Initialized);

    static bool Preview(string message)
    {
        if (!Application.isEditor) return false;
        if (GameAnalytics.SettingsGA.InfoLogEditor) Debug.Log("[Duck Rails Analytics preview] " + message);
        return true;
    }

    static void Design(string eventId)
    {
        if (!Ready || string.IsNullOrEmpty(eventId))
            return;
        if (!Preview(eventId)) GameAnalytics.NewDesignEvent(eventId);
    }

    static void Design(string eventId, float value)
    {
        if (!Ready || string.IsNullOrEmpty(eventId))
            return;
        if (!Preview(eventId + " value=" + value)) GameAnalytics.NewDesignEvent(eventId, value);
    }

    static void Design(string eventId, float value, IDictionary<string, object> fields)
    {
        if (!Ready || string.IsNullOrEmpty(eventId))
            return;
        if (!Preview(eventId + " value=" + value)) GameAnalytics.NewDesignEvent(eventId, value, fields);
    }

    static void Error(GAErrorSeverity severity, string message)
    {
        if (!Ready || string.IsNullOrEmpty(message))
            return;
        if (message.Length > 8192)
            message = message.Substring(0, 8192);
        if (!Preview("error:" + severity + " " + message)) GameAnalytics.NewErrorEvent(severity, message);
    }

    /// <summary>GameAnalytics event segments are max 16 chars.</summary>
    public static string Segment(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "unknown";
        string s = raw.Trim();
        if (s.StartsWith("weapon_", StringComparison.OrdinalIgnoreCase))
            s = "w_" + s.Substring(7);
        else if (s.StartsWith("item_", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(5);
        else if (s.StartsWith("skin_duck_", StringComparison.OrdinalIgnoreCase))
            s = "sk_" + s.Substring(10);
        else if (s.StartsWith("duckz_", StringComparison.OrdinalIgnoreCase))
            s = s.Substring(6);
        else if (s.EndsWith("(Clone)", StringComparison.Ordinal))
            s = s.Substring(0, s.Length - 7).Trim();
        s = NonSegment.Replace(s, "_");
        if (s.Length == 0)
            return "unknown";
        if (s.Length > MaxSegmentLength)
            s = s.Substring(0, MaxSegmentLength);
        return s.ToLowerInvariant();
    }

    static string Id(params string[] parts)
    {
        for (int i = 0; i < parts.Length; i++)
            parts[i] = Segment(parts[i]);
        return string.Join(":", parts);
    }

    public static void MarkAlive()
    {
        Ensure();
        Instance.aliveSince = Time.realtimeSinceStartup;
    }

    public static float AliveSeconds()
    {
        if (Instance == null || Instance.aliveSince < 0f)
            return 0f;
        return Mathf.Max(0f, Time.realtimeSinceStartup - Instance.aliveSince);
    }

    static bool InFirstMinutes()
    {
        Ensure();
        return Time.realtimeSinceStartup - Instance.sessionStarted <= FirstMinutes;
    }

    static void SessionFirst(string key)
    {
        Ensure();
        if (!InFirstMinutes() || !Instance.sessionFirsts.Add(key))
            return;
        Design(Id("session", "first", key));
    }

    public static void SessionJoined(string kind, int playerCount)
    {
        Ensure();
        Instance.sessionStarted = Time.realtimeSinceStartup;
        Instance.sessionFirsts.Clear();
        Instance.visitedAreas.Clear();
        Instance.FlushPresence(force: true);
        Instance.currentAreaKey = "other";
        var fields = new Dictionary<string, object> { { "players", playerCount } };
        Design(Id("session", "join", kind), playerCount, fields);
    }

    public static void SessionJoinFailed(string message)
    {
        Error(GAErrorSeverity.Error, "photon_join: " + message);
        Design(Id("session", "join_fail"));
    }

    // Participant observations, not unique expedition counts. IDs never contain codes or player IDs.
    public static void SessionEvent(string action)
    {
        Ensure();
        Design(Id("rails", "session", action), 1f, new Dictionary<string, object>
        {
            ["kind"] = PhotonSession.Kind,
            ["phase"] = PhotonSession.Phase.ToString().ToLowerInvariant(),
            ["players"] = PhotonNetwork.CurrentRoom?.PlayerCount ?? 0,
            ["client"] = Application.isEditor ? "editor" : Debug.isDebugBuild ? "development" : "release"
        });
    }

    public static void SessionPhaseChanged()
    {
        Ensure();
        Design(Id("rails", "phase", PhotonSession.Phase.ToString()));
        if (PhotonSession.Phase == RailPhase.Ending)
            SessionEvent(PhotonSession.Get("rails.success", false) ? "success" : "failure");
    }

    public static void SessionDisconnected(string cause)
    {
        Ensure();
        Design(Id("rails", "disconnect", cause));
    }

    public static void GoldCount(int count, int players)
    {
        var fields = new Dictionary<string, object>
        {
            { "players", players },
            { "target", GoldTarget },
        };
        Design(Id("gold", "count"), count, fields);
        if (count < GoldLow)
        {
            Design(Id("gold", "alert", "low"), count, fields);
            Error(GAErrorSeverity.Warning, $"gold_count_low:{count}");
        }
        else if (count > GoldHigh)
        {
            Design(Id("gold", "alert", "high"), count, fields);
            Error(GAErrorSeverity.Warning, $"gold_count_high:{count}");
        }
    }

    public static void GoldDeposited(int bars)
    {
        Design(Id("gold", "deposit"), bars);
        SessionFirst("deposit");
    }

    public static void GoldDepositFailed(int bars, long code, string message)
    {
        Error(GAErrorSeverity.Error, $"gold_deposit_fail code={code} bars={bars} {message}");
    }

    public static void PlayerDied(string cause, float aliveSeconds, string heldItem)
    {
        string detail = Segment(cause);
        string root = detail == "duck" ? "duck" : detail == "hazard" || detail == "unknown" ? detail : "enemy";
        string eventId = root == "enemy" ? Id("death", "enemy", detail) : Id("death", root);
        if (!string.IsNullOrEmpty(heldItem))
        {
            var fields = new Dictionary<string, object> { { "held", Segment(heldItem) } };
            Design(eventId, aliveSeconds, fields);
        }
        else
            Design(eventId, aliveSeconds);
        SessionFirst("death");
    }

    public static void StorePurchase(string itemKey, StorePurchaseResult result)
    {
        string item = Segment(itemKey);
        switch (result)
        {
            case StorePurchaseResult.Bought:
                Design(Id("store", "buy", item));
                SessionFirst("store_buy");
                break;
            case StorePurchaseResult.Unlocked:
                Design(Id("store", "unlock", item));
                break;
            case StorePurchaseResult.NotEnoughCoins:
                Design(Id("store", "no_coins", item));
                break;
            case StorePurchaseResult.Failed:
                Design(Id("store", "fail", item));
                Error(GAErrorSeverity.Error, "store_buy_fail:" + itemKey);
                break;
        }
    }

    public static void IapPurchased(string sku, int amountCents, string currency = "USD")
    {
        if (!Ready)
            return;
        string itemId = Segment(sku);
        if (!Application.isEditor) GameAnalytics.NewBusinessEvent(currency, Math.Max(0, amountCents), "bundle", itemId, "store");
        Design(Id("iap", "buy", itemId), amountCents);
        SessionFirst("iap");
    }

    public static void IapFailed(string sku, string reason)
    {
        Design(Id("iap", "fail", Segment(sku)));
        Error(GAErrorSeverity.Warning, $"iap_fail:{sku} {reason}");
    }

    public static void IapGrantFailed(string sku, long code, string message)
    {
        Error(GAErrorSeverity.Error, $"iap_grant_fail:{sku} code={code} {message}");
    }

    public static void DuckActivated()
    {
        Design(Id("duck", "activated"));
        SessionFirst("duck_act");
    }

    public static void DuckFollow() => Design(Id("duck", "follow"));

    public static void DuckDismiss() => Design(Id("duck", "dismiss"));

    public static void DuckUtterance(string bucket)
    {
        Design(Id("duck", "utterance", bucket));
    }

    public static void DuckKillAttempt() => Design(Id("duck", "kill_try"));

    public static void DuckKillSuccess() => Design(Id("duck", "kill_ok"));

    public static string UtteranceBucket(string intent)
    {
        if (string.IsNullOrEmpty(intent))
            return "chat";
        switch (intent)
        {
            case "follow":
            case "dismiss":
            case "wait":
            case "quiet":
                return "command";
            case "greeting":
                return "wake";
            default:
                return "chat";
        }
    }

    public static int ParsePriceCents(string displayPrice)
    {
        if (string.IsNullOrEmpty(displayPrice))
            return 0;
        string digits = Regex.Replace(displayPrice, @"[^\d.]", "");
        if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out double dollars))
            return 0;
        return Mathf.RoundToInt((float)(dollars * 100.0));
    }

    void TickPresence()
    {
        if (!TryLocalHead(out Vector3 head))
            return;

        AnalyticsArea area = AnalyticsArea.FindAt(head);
        string key = PhotonSession.IsRailsRoom ? Segment(PhotonSession.Kind + "_" + PhotonSession.Phase)
            : area != null ? area.AreaKey : "other";
        if (key != currentAreaKey)
        {
            if (PhotonNetwork.InRoom)
                FlushPresenceArea(currentAreaKey);
            currentAreaKey = key;
            if (PhotonNetwork.InRoom && visitedAreas.Add(key))
                Design(Id("presence", "visit", key));
        }

        // Only accumulate / flush dwell while in a live session.
        if (!PhotonNetwork.InRoom)
            return;

        if (!dwellSeconds.ContainsKey(currentAreaKey))
            dwellSeconds[currentAreaKey] = 0f;
        dwellSeconds[currentAreaKey] += Time.unscaledDeltaTime;

        if (Time.realtimeSinceStartup >= presenceFlushAt)
            FlushPresence(force: false);
    }

    void FlushPresence(bool force)
    {
        presenceFlushAt = Time.realtimeSinceStartup + PresenceFlushSeconds;
        if (dwellSeconds.Count == 0)
            return;
        var keys = new List<string>(dwellSeconds.Keys);
        for (int i = 0; i < keys.Count; i++)
            FlushPresenceArea(keys[i], keepZero: force);
    }

    void FlushPresenceArea(string key, bool keepZero = false)
    {
        if (string.IsNullOrEmpty(key) || !dwellSeconds.TryGetValue(key, out float seconds))
            return;
        if (seconds < 1f && !keepZero)
            return;
        if (seconds >= 1f)
            Design(Id("presence", "dwell", key), seconds);
        dwellSeconds[key] = 0f;
    }

    static bool TryLocalHead(out Vector3 head)
    {
        PhotonPlayer local = PhotonPlayer.Local;
        if (local != null && local.TryGetHeadPosition(out head))
            return true;
        if (GR.Player.instance != null)
        {
            head = GR.Player.instance.transform.position;
            return true;
        }
        if (Camera.main != null)
        {
            head = Camera.main.transform.position;
            return true;
        }
        head = default;
        return false;
    }
}
