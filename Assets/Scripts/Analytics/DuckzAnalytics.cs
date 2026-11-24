using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using GameAnalyticsSDK;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Duck Rails GameAnalytics wrapper; the class name preserves inherited call sites.
/// Gameplay run events are composed in <see cref="RailAnalytics"/>. Configure platform keys in
/// Window → GameAnalytics → Settings before expecting events in a build. See docs/analytics-setup.md.
/// </summary>
public sealed class DuckzAnalytics : MonoBehaviour
{
    // GameAnalytics accepts 64 characters per event part, but its own messages state 32; stay under both.
    const int MaxSegmentLength = 32;
    const float FirstMinutes = 300f;
    public const string Coins = "coins";
    // Resource item types must be registered before initialization.
    public const string GoldSource = "gold", ShopSink = "shop";

    static readonly Regex NonSegment = new("[^a-zA-Z0-9_-]", RegexOptions.Compiled);

    public static DuckzAnalytics Instance { get; private set; }

    readonly HashSet<string> sessionFirsts = new HashSet<string>();
    float sessionStarted = -1f;
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
        EnsureSdk();
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void Update()
    {
        if (!initialized)
            EnsureSdk();
    }

    void EnsureSdk()
    {
        if (initializationAttempted)
            return;
        initializationAttempted = true;
        // Spectator sessions are not player sessions: never initialised, so every event is dropped.
        if (SpectatorMode.Active) return;
        // Editor calls are local previews, never uploads. Avoid the SDK's noisy stubs.
        if (Application.isEditor) { initialized = true; return; }
        var settings = GameAnalytics.SettingsGA;
        int platform = settings.Platforms.IndexOf(Application.platform);
        if (platform < 0 || string.IsNullOrEmpty(settings.GetGameKey(platform)) || string.IsNullOrEmpty(settings.GetSecretKey(platform)))
        {
            if (DuckzLog.AllowWarning(out var notice))
                notice.Write("Duck Rails analytics disabled: configure GameAnalytics keys for " + Application.platform);
            return;
        }
        Register(settings.ResourceCurrencies, Coins);
        Register(settings.ResourceItemTypes, GoldSource);
        Register(settings.ResourceItemTypes, ShopSink);
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

    static void Register(List<string> list, string value)
    {
        if (list != null && !list.Contains(value)) list.Add(value);
    }

    static bool Ready => Instance != null && Instance.initialized && (Application.isEditor || GameAnalytics.Initialized);

    static bool Preview(string message)
    {
        if (!Application.isEditor) return false;
#if UNITY_EDITOR
        if (GameAnalytics.SettingsGA.InfoLogEditor) Debug.Log("[Duck Rails Analytics preview] " + message);
#endif
        return true;
    }

    static string Describe(IDictionary<string, object> fields)
    {
        if (fields == null || fields.Count == 0) return "";
        var parts = new List<string>(fields.Count);
        foreach (var pair in fields) parts.Add(pair.Key + "=" + pair.Value);
        return " {" + string.Join(", ", parts) + "}";
    }

    /// <summary>Design event built from segments, e.g. Event(null, null, "run", "start").</summary>
    public static void Event(float? value, IDictionary<string, object> fields, params string[] parts)
    {
        Ensure();
        if (!Ready || parts == null || parts.Length == 0)
            return;
        string eventId = Id(parts);
        if (Preview(eventId + (value.HasValue ? " value=" + value.Value.ToString("0.##", CultureInfo.InvariantCulture) : "") + Describe(fields)))
            return;
        if (value.HasValue)
        {
            if (fields != null) GameAnalytics.NewDesignEvent(eventId, value.Value, fields);
            else GameAnalytics.NewDesignEvent(eventId, value.Value);
        }
        else if (fields != null) GameAnalytics.NewDesignEvent(eventId, fields);
        else GameAnalytics.NewDesignEvent(eventId);
    }

    public static void Event(params string[] parts) => Event(null, null, parts);

    public static void Progression(GAProgressionStatus status, string progression01, string progression02, int score, IDictionary<string, object> fields = null)
    {
        Ensure();
        if (!Ready)
            return;
        string p1 = Segment(progression01), p2 = Segment(progression02);
        if (Preview("progression:" + status + " " + p1 + ":" + p2 + " score=" + score + Describe(fields)))
            return;
        if (status == GAProgressionStatus.Start)
        {
            if (fields != null) GameAnalytics.NewProgressionEvent(status, p1, p2, fields);
            else GameAnalytics.NewProgressionEvent(status, p1, p2);
        }
        else if (fields != null) GameAnalytics.NewProgressionEvent(status, p1, p2, score, fields);
        else GameAnalytics.NewProgressionEvent(status, p1, p2, score);
    }

    /// <summary>Gameplay coins only. Real-money cosmetics use business events, never this currency.</summary>
    public static void CoinFlow(GAResourceFlowType flow, int amount, string itemType, string itemId)
    {
        Ensure();
        if (!Ready || amount <= 0)
            return;
        string id = Segment(itemId);
        if (Preview("resource:" + flow + " " + Coins + " " + amount + " " + itemType + ":" + id))
            return;
        GameAnalytics.NewResourceEvent(flow, Coins, amount, itemType, id);
    }

    static void Error(GAErrorSeverity severity, string message)
    {
        if (!Ready || string.IsNullOrEmpty(message))
            return;
        if (message.Length > 8192)
            message = message.Substring(0, 8192);
        if (!Preview("error:" + severity + " " + message)) GameAnalytics.NewErrorEvent(severity, message);
    }

    /// <summary>Bounded event segment: letters, digits, '_' and '-', lower case, at most 32 characters.</summary>
    public static string Segment(string raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "unknown";
        string s = raw.Trim();
        if (s.EndsWith("(Clone)", StringComparison.Ordinal))
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
        var segments = new string[parts.Length];
        for (int i = 0; i < parts.Length; i++)
            segments[i] = Segment(parts[i]);
        return string.Join(":", segments);
    }

    static bool InFirstMinutes()
    {
        Ensure();
        return Time.realtimeSinceStartup - Instance.sessionStarted <= FirstMinutes;
    }

    /// <summary>Once per session, within the first five minutes after joining.</summary>
    public static void SessionFirst(string key)
    {
        Ensure();
        if (!InFirstMinutes() || !Instance.sessionFirsts.Add(key))
            return;
        Event("session", "first", key);
    }

    public static void SessionJoined(string kind, int playerCount)
    {
        Ensure();
        Instance.sessionStarted = Time.realtimeSinceStartup;
        Instance.sessionFirsts.Clear();
        Event(playerCount, new Dictionary<string, object> { { "players", playerCount } }, "session", "join", kind);
    }

    public static void SessionJoinFailed(string message)
    {
        Error(GAErrorSeverity.Error, "photon_join: " + message);
        Event("session", "join_fail");
    }

    // Participant observations, not unique expedition counts. IDs never contain codes or player IDs.
    public static void SessionEvent(string action)
    {
        Event(1f, new Dictionary<string, object>
        {
            ["kind"] = PhotonSession.Kind,
            ["phase"] = PhotonSession.Phase.ToString().ToLowerInvariant(),
            ["players"] = PhotonNetwork.CurrentRoom?.PlayerCount ?? 0,
            ["client"] = Application.isEditor ? "editor" : Debug.isDebugBuild ? "development" : "release"
        }, "rails", "session", action);
    }

    // Run outcomes are reported once per run by RailAnalytics, not per participant here.
    public static void SessionPhaseChanged() => Event("rails", "phase", PhotonSession.Phase.ToString());

    public static void SessionDisconnected(string cause) => Event("rails", "disconnect", cause);

    /// <summary>Gold bars accepted by the backend and the gameplay coins this client was credited.</summary>
    public static void GoldDeposited(int bars, int credited)
    {
        Event(bars, null, "gold", "deposit");
        CoinFlow(GAResourceFlowType.Source, credited, GoldSource, "deposit");
        SessionFirst("deposit");
    }

    public static void GoldDepositFailed(int bars, long code, string message)
    {
        Error(GAErrorSeverity.Error, $"gold_deposit_fail code={code} bars={bars} {message}");
    }

    /// <summary>
    /// Expedition shops (gameplay coins) report as shop:*, with a coin sink on purchase.
    /// Anything else is the cosmetics store and keeps store:*.
    /// </summary>
    public static void StorePurchase(string itemKey, StorePurchaseResult result, int price = 0, Component store = null)
    {
        string item = Segment(itemKey);
        if (store != null && PhotonSession.IsRailsRoom && PhotonSession.OnTrain)
        {
            string shop = RailAnalytics.ShopName(store.transform.position);
            switch (result)
            {
                case StorePurchaseResult.Bought:
                    Event(price, null, "shop", "buy", shop, item);
                    CoinFlow(GAResourceFlowType.Sink, price, ShopSink, item);
                    SessionFirst("shop_buy");
                    break;
                case StorePurchaseResult.NotEnoughCoins:
                    Event(price, null, "shop", "no_coins", shop, item);
                    break;
                case StorePurchaseResult.Failed:
                    Event("shop", "fail", shop, item);
                    Error(GAErrorSeverity.Error, "shop_buy_fail:" + itemKey);
                    break;
            }
            return;
        }
        switch (result)
        {
            case StorePurchaseResult.Bought:
                Event("store", "buy", item);
                SessionFirst("store_buy");
                break;
            case StorePurchaseResult.Unlocked:
                Event("store", "unlock", item);
                break;
            case StorePurchaseResult.NotEnoughCoins:
                Event("store", "no_coins", item);
                break;
            case StorePurchaseResult.Failed:
                Event("store", "fail", item);
                Error(GAErrorSeverity.Error, "store_buy_fail:" + itemKey);
                break;
        }
    }

    public static void IapPurchased(string sku, int amountCents, string currency = "USD")
    {
        Ensure();
        if (!Ready)
            return;
        string itemId = Segment(sku);
        if (!Application.isEditor) GameAnalytics.NewBusinessEvent(currency, Math.Max(0, amountCents), "bundle", itemId, "store");
        Event(amountCents, null, "iap", "buy", itemId);
        SessionFirst("iap");
    }

    public static void IapFailed(string sku, string reason)
    {
        Event("iap", "fail", sku);
        Error(GAErrorSeverity.Warning, $"iap_fail:{sku} {reason}");
    }

    public static void IapGrantFailed(string sku, long code, string message)
    {
        Error(GAErrorSeverity.Error, $"iap_grant_fail:{sku} code={code} {message}");
    }

    /// <summary>Local head for analytics positions: avatar, then rig, then main camera.</summary>
    public static bool LocalHead(out Vector3 head) => TryLocalHead(out head);

    // Covered by HeadPivotFeedbackPlayChecks and MovementProductionPlayChecks by name.
    static bool TryLocalHead(out Vector3 head)
    {
        PhotonPlayer local = PhotonPlayer.Local;
        if (local != null && local.TryGetHeadPosition(out head))
            return true;
        if (GR.Player.instance != null)
        {
            if (GR.Player.instance.TryGetHeadPivotHeadPosition(out head))
                return true;
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

    public static int ParsePriceCents(string displayPrice)
    {
        if (string.IsNullOrEmpty(displayPrice))
            return 0;
        string digits = Regex.Replace(displayPrice, @"[^\d.]", "");
        if (!double.TryParse(digits, NumberStyles.Float, CultureInfo.InvariantCulture, out double dollars))
            return 0;
        return Mathf.RoundToInt((float)(dollars * 100.0));
    }
}
