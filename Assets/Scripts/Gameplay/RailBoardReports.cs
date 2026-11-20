using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Store cumulative personal checkpoints. A retry or reconnect can safely replay any checkpoint.
public sealed class RailBoardReports : MonoBehaviour
{
    [Serializable] public class Metric { public string key; public int value; }
    [Serializable] public class Report
    {
        public string run, name;
        public List<Metric> metrics = new List<Metric>();
        public bool dirty;
        public void Set(string key, int value)
        {
            if (value <= 0) return;
            var metric = metrics.Find(m => m.key == key);
            if (metric == null) { metrics.Add(new Metric { key = key, value = value }); dirty = true; }
            else if (value > metric.value) { metric.value = value; dirty = true; }
        }
        public void Add(string key) => Set(key, (metrics.Find(m => m.key == key)?.value ?? 0) + 1);
    }
    [Serializable] class Queue { public List<Report> runs = new List<Report>(); }
    const string PrefKey = "rails.board.reports.v1";
    static RailBoardReports instance;
    Queue queue;
    string owner;
    float nextSend, nextSave;
    bool sending;
    public static void Install()
    {
        if (instance != null) return;
        var go = new GameObject("Rail board uploads");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<RailBoardReports>();
    }
    void Load()
    {
        if (owner == UserPlayerPrefs.CurrentUserId) return;
        owner = UserPlayerPrefs.CurrentUserId;
        queue = string.IsNullOrEmpty(owner) ? null : JsonUtility.FromJson<Queue>(UserPlayerPrefs.GetString(PrefKey, "{}"));
        if (queue != null) queue.runs ??= new List<Report>();
    }
    Report Current()
    {
        Load();
        if (queue == null || !PhotonSession.OnTrain || string.IsNullOrEmpty(PhotonSession.RunId)) return null;
        var report = queue.runs.Find(r => r.run == PhotonSession.RunId);
        if (report != null || PhotonSession.Phase != RailPhase.Running) return report;
        report = new Report { run = PhotonSession.RunId, name = "Duck" };
        queue.runs.Add(report);
        report.Set("attempt", 1);
        Save();
        return report;
    }
    public static void Observe(RailRunState state, RailParticipant p)
    {
        if (instance == null || p == null) return;
        var r = instance.Current();
        if (r == null || r.run != state.run) return;
        if (r.name != p.name) r.name = new string((p.name ?? "Duck").Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(64).ToArray());
        r.Set("coal", p.gatheredCoal); r.Set("gold", p.gatheredGold); r.Set("meters", Mathf.FloorToInt(p.riddenMeters));
        if (p.stopCoal != null) foreach (var m in p.stopCoal) r.Set("stop/" + m.key, m.value);
        if (state.finished && p.escaped && !r.metrics.Exists(m => m.key == "escape"))
        { r.Set("escape", 1); instance.Save(); instance.nextSend = 0; }
    }
    public static void Hit(string target, string item)
    {
        string kind = Kind(target);
        if (kind == null || string.IsNullOrEmpty(item) || PhotonSession.Phase != RailPhase.Running) return;
        string readable = System.Text.RegularExpressions.Regex.Replace(item.Replace("(Clone)", ""), @"^(RailShopWeapon|RailShopItem|Weapon|Rail|Item)(?=[A-Z])", "");
        var key = DuckzAnalytics.Segment(System.Text.RegularExpressions.Regex.Replace(readable, @"(?<=[a-z])(?=[A-Z])", "_"));
        instance?.Current()?.Add("hit/" + kind + "/" + key.Substring(0, Math.Min(48, key.Length)));
    }
    public static void Down(string cause)
    {
        string kind = Kind(cause);
        if (kind != null) instance?.Current()?.Add("down/" + kind);
    }
    static string Kind(string raw)
    {
        string s = (raw ?? "").ToLowerInvariant();
        foreach (var kind in Kinds) if (s.Contains(kind)) return kind;
        return s.Contains("zombie") || s.Contains("minion") ? "minion" : null;
    }
    static readonly string[] Kinds = { "smasher", "bomber", "spider", "hook" };
    void Update()
    {
        Load();
        if (queue == null) return;
        if (Time.unscaledTime >= nextSave && queue.runs.Exists(r => r.dirty))
        { Save(); nextSave = Time.unscaledTime + 15; }
        if (sending || Time.unscaledTime < nextSend || !DuckzApiClient.IsReady) return;
        var pending = queue.runs.Find(r => r.dirty);
        if (pending != null) StartCoroutine(Send(pending));
    }
    IEnumerator Send(Report pending)
    {
        sending = true;
        string user = owner;
        transmitting = pending;
        pending.dirty = false;
        bool ok = false;
        yield return DuckzApiClient.PostSigned<object>("rail-board/report", pending, r => ok = r.Success);
        transmitting = null;
        if (owner == user && owner == UserPlayerPrefs.CurrentUserId)
        {
            pending.dirty |= !ok;
            if (ok) queue.runs.RemoveAll(r => !r.dirty && r.run != PhotonSession.RunId);
            Save();
        }
        nextSend = Time.unscaledTime + (ok ? 30 : 60);
        sending = false;
    }
    void Save()
    {
        if (queue == null || owner != UserPlayerPrefs.CurrentUserId) return;
        // In-flight checkpoints must also survive process termination.
        var inFlight = transmitting;
        bool dirty = inFlight != null && inFlight.dirty;
        if (inFlight != null) inFlight.dirty = true;
        UserPlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(queue));
        if (inFlight != null) inFlight.dirty = dirty;
        UserPlayerPrefs.Save();
    }
    Report transmitting;
    void OnApplicationPause(bool paused) { if (paused) Save(); }
    void OnApplicationQuit() => Save();
}
