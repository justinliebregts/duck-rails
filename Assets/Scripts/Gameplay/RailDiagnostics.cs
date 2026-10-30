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
public sealed class RailDiagnostics : MonoBehaviour
{
    public static RailDiagnostics Instance { get; private set; }
    public static bool Development => Application.isEditor || Debug.isDebugBuild;
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
    float notReadyAt;
    static string UploadOwner => PlayerSession.Instance != null && !string.IsNullOrEmpty(PlayerSession.Instance.SigningId)
        ? Hash128.Compute(PlayerSession.Instance.SigningId).ToString() : "";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (Instance != null) return;
        var go = new GameObject("Rail diagnostics"); DontDestroyOnLoad(go);
        go.AddComponent<RailDiagnostics>();
        if (Development) go.AddComponent<RailDiagnosticPanel>();
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
    void OnLog(string message, string stack, LogType type)
    {
        if (saving || (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)) return;
        // Do not retain arbitrary log bodies: authentication libraries sometimes log credentials.
        string safe = "Unity " + type + " at " + Sanitize((stack ?? "").Split('\n').FirstOrDefault());
        Event(safe);
        if (Development && PhotonSession.IsRailsRoom) Violation("unity-error", safe, 0);
    }
    public void Event(string value)
    {
        events.Enqueue(DateTime.UtcNow.ToString("O") + " " + Sanitize(value));
        while (events.Count > 100) events.Dequeue();
    }
    public static RailDiagnosticSample Snapshot()
    {
        var objects = new List<string>();
        foreach (var view in PhotonNetwork.PhotonViewCollection)
        {
            if (view == null) continue;
            var item = view.GetComponent<PhotonGrabbable>(); var bag = view.GetComponent<BagObject>();
            if (item == null && bag == null && view.GetComponent<PhotonPlayer>() == null) continue;
            objects.Add("view=" + view.ViewID + " owner=" + view.OwnerActorNr + " controller=" + view.ControllerActorNr +
                " active=" + view.gameObject.activeInHierarchy + " held=" + (item != null ? item.HeldActor : 0) +
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
            if (current != context) { context = current; violations.Reset(); notReadyAt = 0; Event("State " + current); }
            if (!sample.ready && PhotonSession.IsRailsRoom)
            {
                if (notReadyAt == 0) notReadyAt = Time.unscaledTime;
                if (Development && Time.unscaledTime - notReadyAt > 90)
                    Violation("loading-stall", "World has not become ready for 90 seconds", 0);
                if (Development && !PhotonSession.OnTrain && Time.unscaledTime - notReadyAt > 20 && RailWorld.HasLiveCargo)
                    Violation("stale-cargo", "Cargo still blocks staging readiness after 20 seconds", 0);
            }
            else notReadyAt = 0;
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
            if (v != null && v.GetComponent<PhotonPlayer>() != null && !v.GetComponent<PhotonPlayer>().IsCompanion) avatars.Add(v);
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
            if (!PhotonSession.OnTrain && view.GetComponent<RailCargo>() != null) active.Add("stale-cargo:" + view.ViewID);
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
    public string ReportBug()
    {
        if (Time.unscaledTime - lastManual < 5) return Notice;
        lastManual = Time.unscaledTime; return Capture("manual", "Player pressed Report bug");
    }
    public string Capture(string category, string summary)
    {
        if (saving) return Notice;
        saving = true;
        try
        {
            var report = new RailDiagnosticReport { id = Guid.NewGuid().ToString("N"), created = DateTime.UtcNow.ToString("O"),
                category = Sanitize(category), summary = Sanitize(summary), build = Application.version + "/" + Application.buildGUID,
                platform = Application.platform.ToString(), snapshot = Snapshot(), history = samples.ToArray(), events = events.ToArray(),
                preset = RailTestTools.CurrentPreset(), localOwner = UploadOwner };
            if (Application.isEditor) report.build += "/editor-" + typeof(RailDiagnostics).Module.ModuleVersionId;
            while (System.Text.Encoding.UTF8.GetByteCount(JsonUtility.ToJson(report)) > 900000 && report.history.Length > 0)
                report.history = report.history.Skip(1).ToArray();
            Directory.CreateDirectory(Folder);
            // Never silently delete pending evidence to make room.
            if (Directory.GetFiles(Folder, "*.json").Length >= 200) return Notice = "Report storage full; export or resolve reports in Test Center";
            Write(report); SessionReports++; nextUpload = 0;
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
                report.uploaded = true;
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
