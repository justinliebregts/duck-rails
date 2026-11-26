using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Photon.Pun;
using UnityEngine;

[Serializable] public sealed class RailDiagnosticReport
{
    public string id, created, category, summary, build, platform, status = "open", resolution = "";
    public bool uploaded;
    public string localOwner;
    public RailDiagnosticSample snapshot;
    public RailDiagnosticSample[] history;
    public string[] events;
    public RailTestPreset preset;
}
[Serializable] public sealed class RailDiagnosticSample
{
    public double time;
    public string room, run, phase, connection, route;
    public int actor, master, ping, revision;
    public float frameMs, fuel, distance;
    public bool ready;
    public string[] objects;
}

public sealed class RailDiagnosticDebounce
{
    readonly Dictionary<string, float> since = new Dictionary<string, float>();
    readonly HashSet<string> reported = new HashSet<string>();
    public bool Observe(string key, float now, float grace)
    {
        if (!since.TryGetValue(key, out float start)) since[key] = start = now;
        return now - start >= grace && reported.Add(key);
    }
    public void Keep(IEnumerable<string> active)
    {
        var keys = new HashSet<string>(active);
        foreach (var key in since.Keys.ToArray()) if (!keys.Contains(key)) since.Remove(key);
    }
    public void Reset() { since.Clear(); reported.Clear(); }
}

/// <summary>Local, bounded diagnostics. No account IDs, tokens, voice or arbitrary room properties.</summary>
public sealed class RailDiagnostics : MonoBehaviour, Photon.Realtime.IOnEventCallback
{
    public static RailDiagnostics Instance { get; private set; }
    /// <summary>Editor, or any build (including release) where the signed-in account is a backend admin. Client-side check, not a security boundary; evaluated live because admin status arrives after sign-in.</summary>
    public static bool Development => Application.isEditor || RailAdminTools.Available;
    public static string Folder
    {
        get
        {
#if UNITY_EDITOR
            string root = Application.dataPath.Replace('\\', '/');
            int virtualIndex = root.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            root = virtualIndex < 0 ? Path.GetDirectoryName(root) : root.Substring(0, virtualIndex);
            return Path.Combine(root, "Logs", "Diagnostics");
#else
            return Path.Combine(Application.persistentDataPath, "Diagnostics");
#endif
        }
    }
    public string Notice { get; private set; } = "No reports this session";
    public int SessionReports { get; private set; }
    public float FrameMs { get; private set; }
    readonly Queue<RailDiagnosticSample> samples = new Queue<RailDiagnosticSample>();
    readonly Queue<string> events = new Queue<string>();
    readonly RailDiagnosticDebounce violations = new RailDiagnosticDebounce();
    float nextSample, nextUpload, settledAt, lastManual = -100;
    string context;
    bool uploading, saving;
    float notReadyAt, phaseSince, lastWitness = -100;
    const byte IncidentEvent = 121;
    readonly HashSet<string> witnessed = new HashSet<string>();
    void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);
    static string RunKey => Hash128.Compute(PhotonNetwork.CurrentRoom.Name + "/" + PhotonSession.RunId).ToString();
    static string IncidentKey(string category, int actor) => Hash128.Compute(RunKey + "/" + PhotonSession.Phase + "/" +
        PhotonNetwork.MasterClient.ActorNumber + "/" + category + "/" + (category == "departure-stall" ? 0 : actor)).ToString();
    static string Level(string category) => category == "departure-stall" ? "Critical" :
        category == "loading-stall" ? "High" : category == "slow-load-recovered" ? "Warning" : "Untriaged";
    void Incident(string category, string message)
    {
        if (!violations.Observe(category, Time.unscaledTime, 0)) return;
        int actor = PhotonNetwork.LocalPlayer.ActorNumber;
        string id = IncidentKey(category, actor);
        SavePerspective(category, message, id);
        if (category == "slow-load-recovered") return;
        var peer = PhotonNetwork.IsMasterClient ? PhotonNetwork.PlayerListOthers.FirstOrDefault(p => !p.IsInactive) : PhotonNetwork.MasterClient;
        if (peer != null)
            PhotonNetwork.RaiseEvent(IncidentEvent, new object[] { category, id },
                new Photon.Realtime.RaiseEventOptions { TargetActors = new[] { peer.ActorNumber } }, ExitGames.Client.Photon.SendOptions.SendReliable);
    }
    void SavePerspective(string category, string message, string id)
    {
        if (!witnessed.Add(id)) return;
        string role = PhotonNetwork.IsMasterClient ? "master" : "client";
        Capture(category, message, "[" + Level(category) + "|" + id + "|" + RunKey + "|" + role + " " +
            PhotonNetwork.LocalPlayer.ActorNumber + "] ");
    }
    public void OnEvent(ExitGames.Client.Photon.EventData data)
    {
        if (data.Code != IncidentEvent || !PhotonSession.IsRailsRoom || PhotonNetwork.MasterClient == null ||
            !(data.CustomData is object[] fields) || fields.Length != 2 || !(fields[0] is string category) ||
            (category != "loading-stall" && category != "departure-stall") || !(fields[1] is string id) ||
            (!PhotonNetwork.IsMasterClient && data.Sender != PhotonNetwork.MasterClient.ActorNumber) ||
            PhotonNetwork.CurrentRoom.GetPlayer(data.Sender) == null || id != IncidentKey(category, data.Sender) ||
            witnessed.Contains(id) || Time.unscaledTime - lastWitness < 5) return;
        lastWitness = Time.unscaledTime;
        SavePerspective(category, "Peer snapshot requested by actor " + data.Sender, id);
    }
    static string UploadOwner => PlayerSession.Instance != null && !string.IsNullOrEmpty(PlayerSession.Instance.SigningId)
        ? Hash128.Compute(PlayerSession.Instance.SigningId).ToString() : "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("Rail diagnostics"); DontDestroyOnLoad(go);
        go.AddComponent<RailDiagnostics>();
        go.AddComponent<RailDiagnosticPanel>();
    }
    void Awake() { Instance = this; Application.logMessageReceived += OnLog; }
    void OnDestroy() { Application.logMessageReceived -= OnLog; if (Instance == this) Instance = null; }
    public static string Sanitize(string text)
    {
        text = text ?? "";
        text = Regex.Replace(text, @"(?i)(bearer\s+|(?:access_token|refresh_token|authorization|password|secret|nonce|signature)\s*[=:]\s*)[^\s,;]+", "$1[redacted]");
        text = Regex.Replace(text, @"\b\d{12,}\b", "[account]");
        text = Regex.Replace(text, @"(?:[A-Za-z]:[\\/]|/Users/|/home/)[^\r\n]*", "[local path]");
        return text.Length > 600 ? text.Substring(0, 600) : text;
    }
    // Console lines need stronger scrubbing than our own events: authentication libraries
    // sometimes log credentials. JWTs and long opaque strings are treated as tokens.
    public static string SanitizeConsole(string text)
    {
        text = Regex.Replace(text ?? "", @"eyJ[\w-]+\.[\w-]+\.[\w-]*", "[token]");
        text = Regex.Replace(text, @"(?=[A-Za-z0-9+/_=-]{32,})(?=[A-Za-z0-9+/_=-]*\d)(?=[A-Za-z0-9+/_=-]*[A-Za-z])[A-Za-z0-9+/_=-]{32,}", "[token]");
        return Sanitize(text);
    }
    void OnLog(string message, string stack, LogType type)
    {
        if (saving || type == LogType.Log) return;
        // First frame outside the logging plumbing, so the location names the actual caller.
        string where = (stack ?? "").Split('\n').Select(f => f.Trim()).FirstOrDefault(f => f.Length > 0 &&
            !f.StartsWith("UnityEngine.Debug", StringComparison.Ordinal) && !f.StartsWith("UnityEngine.Logger", StringComparison.Ordinal) &&
            !f.StartsWith("DuckzLog", StringComparison.Ordinal)) ?? "";
        // Token scrubbing applies to the message only, so stack paths stay readable.
        RecordConsole(type + ": " + SanitizeConsole((message ?? "").Split('\n').FirstOrDefault())
            + (where.Length > 0 ? " @ " + Sanitize(where) : ""));
        if (type == LogType.Warning) return;
        // Violation summaries stay message-free; the scrubbed message is in the console lines.
        string safe = "Unity " + type + " at " + Sanitize(where);
        if (Development && PhotonSession.IsRailsRoom) Violation("unity-error", safe, 0);
    }
    // Warnings/errors/exceptions only (no Debug.Log), deduplicated when repeated back to back.
    // Reports reserve up to ConsoleSlots of the backend's 100-entry events list for them.
    const int ConsoleSlots = 40;
    readonly LinkedList<(string stamp, string text, int repeats)> consoleLines = new LinkedList<(string, string, int)>();
    void RecordConsole(string scrubbed)
    {
        string text = "console " + scrubbed;
        string stamp = DateTime.UtcNow.ToString("O");
        if (consoleLines.Last != null && consoleLines.Last.Value.text == text)
        { var last = consoleLines.Last.Value; consoleLines.Last.Value = (stamp, text, last.repeats + 1); return; }
        consoleLines.AddLast((stamp, text, 1));
        while (consoleLines.Count > ConsoleSlots) consoleLines.RemoveFirst();
    }
    /// <summary>The events list the next report would carry: console lines merged with diagnostic events.</summary>
    public string[] ReportEvents()
    {
        var lines = consoleLines.Select(c => c.stamp + " " + c.text + (c.repeats > 1 ? " [x" + c.repeats + "]" : "")).ToList();
        lines.AddRange(events.Skip(Math.Max(0, events.Count - (100 - lines.Count))));
        // ISO-8601 UTC stamps sort chronologically as text.
        return lines.OrderBy(l => l, StringComparer.Ordinal).ToArray();
    }
    public void Event(string value)
    {
        events.Enqueue(DateTime.UtcNow.ToString("O") + " " + Sanitize(value));
        while (events.Count > 100) events.Dequeue();
    }
    public static RailDiagnosticSample Snapshot()
    {
        var objects = new List<string>();
        // Reserve a bounded part of each sample for transfer state. This uses
        // the existing active list and report history, not per-frame Console logs.
        foreach (var enemy in Game.AI.Enemy.Active)
        {
            if (enemy == null || enemy.Boarding == null || enemy.Root == null) continue;
            var view = enemy.GetComponent<PhotonView>();
            objects.Add("enemy=" + (view != null ? view.ViewID : 0) + " state=" + enemy.CurrentState
                + " authority=" + enemy.HasAuthority + " position=" + enemy.Root.position.ToString("F2")
                + " target=" + enemy.LastSeenPos.ToString("F2") + " " + enemy.Boarding.DiagnosticState);
            if (objects.Count >= 32) break;
        }
        foreach (var view in PhotonNetwork.PhotonViewCollection)
        {
            if (view == null) continue;
            var item = view.GetComponent<PhotonGrabbable>(); var bag = view.GetComponent<BagObject>();
            if (item == null && bag == null && view.GetComponent<PhotonPlayer>() == null) continue;
            int holder = item != null ? item.HeldActor : 0;
            objects.Add("view=" + view.ViewID + " owner=" + view.OwnerActorNr + " controller=" + view.ControllerActorNr +
                " active=" + view.gameObject.activeInHierarchy + " held=" + holder +
                (holder != 0 ? " item=" + item.name.Replace("(Clone)", "").Trim() : "") +
                " stashed=" + (item != null && item.IsStashed) + " bag=" + (bag != null ? bag.State.ToString() : "-") +
                " contents=" + (bag != null ? string.Join(",", bag.Keys()) : "-") +
                " position=" + view.transform.position.ToString("F2"));
            if (objects.Count >= 160) break;
        }
        return new RailDiagnosticSample { time = PhotonNetwork.Time, room = PhotonNetwork.CurrentRoom?.Name ?? "", run = PhotonSession.RunId,
            phase = PhotonSession.Phase.ToString(), connection = PhotonNetwork.NetworkClientState.ToString(),
            actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0, master = PhotonNetwork.MasterClient?.ActorNumber ?? 0,
            ping = PhotonNetwork.GetPing(), frameMs = Instance != null ? Instance.FrameMs : 0, fuel = PhotonSession.CurrentFuel,
            revision = PhotonSession.Get(RailSessionRules.Revision, 0),
            distance = RailTrain.Instance != null ? RailTrain.Instance.Distance : 0, ready = PhotonSession.Instance?.WorldReady == true,
            route = PhotonSession.Get(RailRoute.StateKey, ""), objects = objects.ToArray() };
    }
    void Update()
    {
        FrameMs = Mathf.Lerp(FrameMs, Time.unscaledDeltaTime * 1000, .05f);
        if (Time.unscaledTime >= nextSample)
        {
            nextSample = Time.unscaledTime + 1;
            var sample = Snapshot(); samples.Enqueue(sample);
            while (samples.Count > 60) samples.Dequeue();
            string current = sample.room + "/" + sample.run + "/" + sample.phase + "/" + sample.master;
            if (current != context || !sample.ready) settledAt = Time.unscaledTime;
            if (current != context) { context = current; phaseSince = Time.unscaledTime; witnessed.Clear(); violations.Reset(); notReadyAt = 0; Event("State " + current); }
            if (!sample.ready && PhotonSession.IsRailsRoom)
            {
                if (notReadyAt == 0) notReadyAt = Time.unscaledTime;
                if (PhotonNetwork.MasterClient != null && Time.unscaledTime - notReadyAt > 90)
                    Incident("loading-stall", "World has not become ready for 90 seconds");
                if (Development && !PhotonSession.OnTrain && Time.unscaledTime - notReadyAt > 20 && RailWorld.HasLiveCargo)
                    Violation("stale-cargo", "Cargo still blocks staging readiness after 20 seconds", 0);
            }
            else
            {
                if (PhotonSession.IsRailsRoom && PhotonNetwork.MasterClient != null && notReadyAt > 0 &&
                    Time.unscaledTime - notReadyAt >= 30 && Time.unscaledTime - notReadyAt <= 90)
                    Incident("slow-load-recovered", "World became ready after " + (int)(Time.unscaledTime - notReadyAt) + " seconds");
                notReadyAt = 0;
            }
            if (PhotonSession.IsRailsRoom && PhotonNetwork.MasterClient != null && PhotonSession.Phase == RailPhase.Departing &&
                Time.unscaledTime - phaseSince >= 90)
                Incident("departure-stall", "Expedition has remained in Departing for 90 seconds");
            if (Development && sample.ready && Time.unscaledTime - settledAt > 8) Detect(sample);
        }
        if (!uploading && Time.unscaledTime >= nextUpload && DuckzApiClient.IsReady)
        { nextUpload = Time.unscaledTime + 30; StartCoroutine(UploadPending()); }
    }
    void Detect(RailDiagnosticSample sample)
    {
        var active = new HashSet<string>();
        var avatars = new List<PhotonView>();
        foreach (var v in PhotonNetwork.PhotonViewCollection)
            if (v != null && v.GetComponent<PhotonPlayer>() != null) avatars.Add(v);
        string readyToken = sample.run + ":" + sample.phase;
        foreach (var actor in PhotonNetwork.PlayerList.Where(p => !p.IsInactive))
        {
            int copies = avatars.Count(v => v.OwnerActorNr == actor.ActorNumber);
            if (copies > 1 || copies == 0 && actor.CustomProperties[RailSessionRules.Ready] as string == readyToken)
                active.Add("avatar-count:" + actor.ActorNumber);
        }
        foreach (var view in PhotonNetwork.PhotonViewCollection)
        {
            if (view == null) continue;
            var item = view.GetComponent<PhotonGrabbable>();
            if (item != null && !item.PickupPending && item.HeldActor != 0 && item.HeldActor != view.OwnerActorNr)
                active.Add("held-owner:" + view.ViewID);
            if (!PhotonSession.OnTrain && view.GetComponent<RailCargo>() != null && !RailCargo.KeepCompoundPurchase(view))
                active.Add("stale-cargo:" + view.ViewID);
        }
        // Compare retained route contents only for the same run and settled phase. Moving poses need time alignment.
        string digest = Hash128.Compute(sample.route).ToString();
        string marker = sample.run + ":" + sample.phase + ":" + digest;
        if (PhotonNetwork.InRoom && PhotonNetwork.LocalPlayer.CustomProperties["rails.diag.route"] as string != marker)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new ExitGames.Client.Photon.Hashtable { ["rails.diag.route"] = marker });
        string prefix = sample.run + ":" + sample.phase + ":";
        foreach (var peer in PhotonNetwork.PlayerList)
            if (!peer.IsInactive && peer.CustomProperties[RailSessionRules.Ready] as string == readyToken &&
                peer.CustomProperties["rails.diag.route"] is string other && other.StartsWith(prefix, StringComparison.Ordinal) && other != marker)
                active.Add("route-disagreement:" + peer.ActorNumber);
        violations.Keep(active);
        foreach (string key in active) Violation(key, "Persistent invariant violation: " + key, 5);
    }
    void Violation(string key, string summary, float grace)
    {
        if (violations.Observe(key, Time.unscaledTime, grace)) Capture(key, summary);
    }
    public const float ManualCooldown = 30f;
    /// <summary>Raised with the report ID when a report upload is acknowledged by the backend.</summary>
    public static event Action<string> ReportUploaded;
    public string LastReportId { get; private set; }
    public float ManualCooldownRemaining => Mathf.Max(0f, lastManual + ManualCooldown - Time.unscaledTime);
    public string ReportBug()
    {
        if (ManualCooldownRemaining > 0f) return Notice;
        lastManual = Time.unscaledTime; return Capture("manual", "Player pressed Report bug");
    }
    public string Capture(string category, string summary) => Capture(category, summary, "");
    string Capture(string category, string summary, string prefix)
    {
        if (saving) return Notice;
        saving = true;
        try
        {
            var report = new RailDiagnosticReport { id = Guid.NewGuid().ToString("N"), created = DateTime.UtcNow.ToString("O"),
                category = Sanitize(category), summary = prefix + Sanitize(summary), build = Application.version + "/" + Application.buildGUID,
                platform = Application.platform.ToString(), snapshot = Snapshot(), history = samples.ToArray(), events = ReportEvents(),
                preset = RailTestTools.CurrentPreset(), localOwner = UploadOwner };
            if (Application.isEditor) report.build += "/editor-" + typeof(RailDiagnostics).Module.ModuleVersionId;
            while (System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(report)) > 900000 && report.history.Length > 0)
                report.history = report.history.Skip(1).ToArray();
            Directory.CreateDirectory(Folder);
            // Never silently delete pending evidence to make room.
            if (Directory.GetFiles(Folder, "*.json").Length >= 200) return Notice = "Report storage full; export or resolve reports in Test Center";
            Write(report); LastReportId = report.id; SessionReports++; nextUpload = 0;
            RailDiagnosticPanel.Instance?.Notify("Issue saved " + report.id.Substring(0, 8) + "\nOpen the left menu for details");
            return Notice = "Saved " + report.id.Substring(0, 8) + " · upload pending";
        }
        catch (Exception e) { return Notice = "Could not save report: " + e.GetType().Name; }
        finally { saving = false; }
    }
    public static void Write(RailDiagnosticReport report)
    {
        if (!Guid.TryParseExact(report.id, "N", out _)) throw new ArgumentException("Invalid report ID");
        Directory.CreateDirectory(Folder);
        string path = Path.Combine(Folder, report.id + ".json");
        File.WriteAllText(path + ".tmp", JsonUtility.ToJson(report, true));
        if (File.Exists(path)) File.Replace(path + ".tmp", path, null);
        else File.Move(path + ".tmp", path);
    }
    public static RailDiagnosticReport[] ReadReports()
    {
        if (!Directory.Exists(Folder)) return Array.Empty<RailDiagnosticReport>();
        var found = new List<RailDiagnosticReport>();
        foreach (string path in Directory.GetFiles(Folder, "*.json"))
            try { var r = JsonUtility.FromJson<RailDiagnosticReport>(File.ReadAllText(path)); if (r != null && Guid.TryParseExact(r.id, "N", out _)) found.Add(r); } catch (Exception) { }
        return found.OrderByDescending(r => r.created).ToArray();
    }
    IEnumerator UploadPending()
    {
        uploading = true;
        try
        {
            string owner = UploadOwner;
            foreach (var report in ReadReports().Where(r => CanUpload(r, owner)).Take(3))
            {
                bool ok = false;
                var payload = UploadPayload(report);
                yield return DuckzApiClient.PostSigned<object>("diagnostics/report", payload, r => ok = r.Success);
                if (!ok) { Notice = "Reports saved locally · upload will retry"; yield break; }
                report.uploaded = true; ReportUploaded?.Invoke(report.id);
                try { Write(report); Notice = "Uploaded " + report.id.Substring(0, 8); }
                catch (Exception) { Notice = "Uploaded; local receipt could not be saved"; }
            }
        }
        finally { uploading = false; }
    }
    public static bool CanUpload(RailDiagnosticReport report, string owner) => report != null && !report.uploaded && !string.IsNullOrEmpty(owner) && report.localOwner == owner;
    public static RailDiagnosticReport UploadPayload(RailDiagnosticReport report)
    {
        var payload = JsonUtility.FromJson<RailDiagnosticReport>(JsonUtility.ToJson(report));
        payload.localOwner = ""; // Local queue routing only; never transmit the account fingerprint.
        return payload;
    }
}
