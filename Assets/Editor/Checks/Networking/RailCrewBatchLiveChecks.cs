#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Crew batch checks (playtest-queue: "Spawns follow live crew size", "Cross-block enemy pursuit").
/// Two-client private expedition (main + MPPM Player2) with a shared control.json handshake. Route: Motel then Farm.
/// Cases: 1 solo crew-1 counts; 2 late join scales unreached blocks to crew 2 within 8 s, reached blocks and enemies
/// unchanged, no loot within 35 m; 3 a moved crew-2 extra survives the peer leave, extras are gone after 25 s, the rejoin
/// restores crew 2 with the same loot IDs; 4 master handoff keeps counts identical and hands back; 5 run ID format;
/// 6 cross-block pursuit across the Motel/Farm seam (6a) and back toward the Motel after the player leaves (6b).
/// Output: Temp/rails-crew-batch/{main,player2}.txt, verdicts.txt, pursuit-{main,player2}.csv.
/// Runtime code is not instrumented; reads go through public state (reflection only for the admin flag, as elsewhere).
/// </summary>
[InitializeOnLoad]
public static class RailCrewBatchLiveChecks
{
    [Serializable] sealed class Control { public string token, expires, privateCode; public int step, revision; }
    [Serializable] sealed class Receipt { public string token, note, error, data; public int revision; public bool pass; }
    sealed class Snap
    {
        public int[] coal, gold, enemy;
        public int dupMarkers, dupIds;
        public readonly SortedSet<int> loot = new SortedSet<int>();
        public readonly Dictionary<string, PhotonView> byMarker = new Dictionary<string, PhotonView>(StringComparer.Ordinal);
    }
    sealed class Row { public double t, x, z, along; public string state; }

    const string Key = "Rails.CrewBatch", MotelId = "roadside-motel-v1", FarmId = "farm-silos-v1";
    static readonly string[] Roles = { "main", "player2" };
    // Per-step limits, seconds. Index = step number.
    static readonly double[] StepLimit = { 120, 200, 120, 60, 90, 120, 60, 60, 30, 240, 60, 30 };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-crew-batch";
    static string ControlPath => Folder + "/control.json";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    static string PursuitPath(string role) => Folder + "/pursuit-" + role + ".csv";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";

    static Control control;
    static string token, networkState;
    static int revision = -1, failures;
    static bool ack, action, done;
    static readonly bool[] f = new bool[12];
    static double entered, nextTick, readySince, nextPursuit;
    static double hostJoinAt, peerJoinAt, leaveAt, handoffAt, handbackAt, markMove, lastChaseAge = -1;
    static readonly Dictionary<string, double> M = new Dictionary<string, double>();
    // Run-scoped state (reset when a new control token arrives).
    static Snap solo;
    static HashSet<int> soloIds = new HashSet<int>(), joinBaseline = new HashSet<int>();
    static int movedId = -1, movedIndex = -1, movedBlock = -1;
    static string movedMarker = "";
    static bool threeLeaveOk, fourCountsOk;
    static string threeLeaveNote = "";
    static double crossedAt, peakAlong = double.NegativeInfinity;

    static RailCrewBatchLiveChecks() { EditorApplication.update += Tick; EditorApplication.update += Restore; }

    // ---------- Start / Stop / restore (same pattern as RailFinaleBatchLiveChecks) ----------
    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    public static void Start()
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); File.WriteAllText(PursuitPath(role), ""); }
        File.WriteAllText(Folder + "/verdicts.txt", "");
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetString(Key + ".order", EditorJsonUtility.ToJson(RailTestBlockOrder.instance));
        SessionState.SetBool(Key, true);
        RailEditorQuickStart.Enabled = false;
        RailTestBlockOrder.instance.useOrder = true;
        RailTestBlockOrder.instance.blocks = new() { Block(MotelId), Block(FarmId) };
        RailTestBlockOrder.instance.invincibleFromEnemies = true;
        RailTestBlockOrder.instance.Persist(); RailTestBlockOrderPanel.PublishPlayerOptions();
        File.WriteAllText(ControlPath, JsonUtility.ToJson(new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(40).ToString("O"),
            privateCode = UnityEngine.Random.Range(10000, 99999).ToString() }));
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        Manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(Manager, null);
    }

    static RailBlock Block(string id) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/" + id + ".prefab").GetComponent<RailBlock>();

    public static void Stop()
    {
        if (File.Exists(ControlPath)) File.WriteAllText(ControlPath, "");
        if (Status == "Running") Manager.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(Manager, null);
    }

    static void Restore()
    {
        if (!CurrentPlayer.IsMainEditor || !SessionState.GetBool(Key, false)) return;
        if (Status != "NotRunning" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        RailEditorQuickStart.Enabled = SessionState.GetBool(Key + ".quick", true);
        string scenario = SessionState.GetString(Key + ".scenario", "");
        if (scenario.Length > 0) Manager.GetType().GetProperty("ActivePlayModeConfig").SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(scenario));
        EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(Key + ".order", "{}"), RailTestBlockOrder.instance);
        RailTestBlockOrder.instance.Persist(); RailTestBlockOrderPanel.PublishPlayerOptions();
        SessionState.SetBool(Key, false);
    }

    // ---------- helpers ----------
    static void Log(string message) { try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n"); } catch (IOException) { } }
    static void AppendVerdict(string line) { try { File.AppendAllText(Folder + "/verdicts.txt", DateTime.UtcNow.ToString("O") + " " + line + "\n"); } catch (IOException) { } Log("VERDICT " + line); }
    static void Verdict(string name, bool pass, Receipt m, Receipt p) => VerdictDetail(name, pass, "main[" + m?.note + "] player2[" + p?.note + "]");
    static void VerdictDetail(string name, bool pass, string detail)
    {
        if (!pass) failures++;
        AppendVerdict("CASE " + name + " " + (pass ? "PASS" : "FAIL") + " | " + detail);
    }
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " phase=" + PhotonSession.Phase + " run=" + Short(PhotonSession.RunId) + " step=" + control?.step + " master=" + PhotonNetwork.IsMasterClient;
    static string Short(string s) => string.IsNullOrEmpty(s) ? "-" : s.Substring(0, Math.Min(6, s.Length));
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest;
    static int ActivePlayers => PhotonNetwork.PlayerList.Count(p => !p.IsInactive);
    static bool Settled { get { if (!Ready) { readySince = 0; return false; } if (readySince == 0) readySince = EditorApplication.timeSinceStartup; return EditorApplication.timeSinceStartup - readySince > 3; } }
    static bool InStaging => PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging;
    static bool ArrivedAndQuiet()
    {
        if (PhotonSession.Phase != RailPhase.Running || !Settled) { arrivedAt = 0; return false; }
        if (arrivedAt == 0) { arrivedAt = EditorApplication.timeSinceStartup; Log("ARRIVED " + Where()); }
        return EditorApplication.timeSinceStartup - arrivedAt > 4;
    }
    static double arrivedAt;
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void ForceAdmin()
    {
        var session = PlayerSession.Instance;
        if (session == null) return;
        if (!session.IsAdmin) typeof(PlayerSession).GetProperty("IsAdmin").GetSetMethod(true).Invoke(session, new object[] { true });
        RailAdminTools.Refresh();
    }
    static void ResetRun()
    {
        solo = null; soloIds = new HashSet<int>(); joinBaseline = new HashSet<int>();
        movedId = movedIndex = movedBlock = -1; movedMarker = "";
        threeLeaveOk = fourCountsOk = false; threeLeaveNote = "";
        crossedAt = 0; peakAlong = double.NegativeInfinity;
    }

    static double Get(string k) => M.TryGetValue(k, out var v) ? v : double.NaN;
    static void Put(string k, double v) => M[k] = v;
    static void Mx(string k, double v) { if (!M.TryGetValue(k, out var old) || v > old) M[k] = v; }
    static void Mn(string k, double v) { if (!M.TryGetValue(k, out var old) || v < old) M[k] = v; }
    static string Fmt() => string.Join(";", M.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture)));
    static string Field(string data, string key)
    {
        if (string.IsNullOrEmpty(data)) return null;
        foreach (var part in data.Split(';')) { int eq = part.IndexOf('='); if (eq > 0 && part.Substring(0, eq) == key) return part.Substring(eq + 1); }
        return null;
    }
    static double Val(string data, string key)
    {
        string s = Field(data, key);
        return s != null && double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : double.NaN;
    }
    static string Show(double v) => double.IsNaN(v) ? "-" : v.ToString("F3", CultureInfo.InvariantCulture);
    static string N(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

    static void Ack(string note, bool pass, string data = "")
    {
        if (ack) return;
        string full = string.Join(";", new[] { Fmt(), data }.Where(x => x.Length > 0));
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, note = note, data = full, pass = pass }));
        Log("ACK step=" + control.step + " pass=" + pass + " " + note + " " + full);
        ack = true;
    }
    static Receipt ReadReceipt(string role)
    {
        if (!File.Exists(ReceiptPath(role))) return null;
        string json = File.ReadAllText(ReceiptPath(role));
        if (string.IsNullOrEmpty(json)) return null;
        var r = JsonUtility.FromJson<Receipt>(json);
        return r != null && r.token == token ? r : null;
    }
    static bool AllAck => Roles.All(r => ReadReceipt(r)?.revision == revision);

    // ---------- loot and enemy snapshots ----------
    static float[] BlockStarts() => RailExpedition.Instance == null ? Array.Empty<float>()
        : RailExpedition.Instance.coalStart.Where(s => s >= 0f).Distinct().OrderBy(s => s).ToArray();
    static int BlockOf(float[] starts, int i, float[] blockStarts) => starts != null && i >= 0 && i < starts.Length && starts[i] >= 0f ? Array.IndexOf(blockStarts, starts[i]) : -1;
    static int Need(int[] needs, int i) => needs != null && i < needs.Length ? needs[i] : 1;
    static Transform[] PointsOf(RailSpawnKind k) => k == RailSpawnKind.Coal ? RailExpedition.Instance.coalPoints : k == RailSpawnKind.Gold ? RailExpedition.Instance.goldPoints : RailExpedition.Instance.enemyPoints;
    static int[] NeedsOf(RailSpawnKind k) => k == RailSpawnKind.Coal ? RailExpedition.Instance.coalCrew : k == RailSpawnKind.Gold ? RailExpedition.Instance.goldCrew : RailExpedition.Instance.enemyCrew;
    static float[] StartsOf(RailSpawnKind k) => k == RailSpawnKind.Coal ? RailExpedition.Instance.coalStart : k == RailSpawnKind.Gold ? RailExpedition.Instance.goldStart : RailExpedition.Instance.enemyStart;
    // Expected live count for a block at a crew: points whose smallest crew is <= crew.
    static int Expect(RailSpawnKind kind, int block, int crew)
    {
        var starts = StartsOf(kind); var needs = NeedsOf(kind); var points = PointsOf(kind); var bs = BlockStarts(); int n = 0;
        for (int i = 0; i < points.Length; i++) if (BlockOf(starts, i, bs) == block && Need(needs, i) <= crew) n++;
        return n;
    }
    static bool ParseMarker(string m, out string prefix, out int index)
    {
        prefix = ""; index = -1; int dash = m.LastIndexOf('-');
        if (dash < 0) return false;
        prefix = m.Substring(0, dash + 1);
        return int.TryParse(m.Substring(dash + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out index);
    }
    static Snap Take()
    {
        var run = RailExpedition.Instance; var bs = BlockStarts(); int n = bs.Length;
        var s = new Snap { coal = new int[n], gold = new int[n], enemy = new int[n] };
        string runId = PhotonSession.RunId; var seen = new HashSet<int>();
        foreach (var v in PhotonNetwork.PhotonViewCollection)
        {
            var d = v == null ? null : v.InstantiationData as object[];
            if (d == null || d.Length < 3 || (d[d.Length - 2] as string) != runId) continue;
            string m = d[d.Length - 1] as string;
            if (string.IsNullOrEmpty(m)) continue;
            if (!seen.Add(v.ViewID)) s.dupIds++;
            if (s.byMarker.ContainsKey(m)) s.dupMarkers++; else s.byMarker[m] = v;
            if (run == null || !ParseMarker(m, out string prefix, out int index)) continue;
            if (prefix == "coal-") { s.loot.Add(v.ViewID); int b = BlockOf(run.coalStart, index, bs); if (b >= 0) s.coal[b]++; }
            else if (prefix == "gold-") { s.loot.Add(v.ViewID); int b = BlockOf(run.goldStart, index, bs); if (b >= 0) s.gold[b]++; }
            else if (prefix == "enemy-") { int b = BlockOf(run.enemyStart, index, bs); if (b >= 0) s.enemy[b]++; }
        }
        return s;
    }
    static string Counts(Snap s) => "c=" + string.Join("/", s.coal) + ";g=" + string.Join("/", s.gold) + ";e=" + string.Join("/", s.enemy) + ";dup=" + (s.dupMarkers + s.dupIds);
    static string Ids(Snap s) => string.Join(",", s.loot);
    static string Dump(Snap s) => Counts(s) + ";crew=" + RailExpedition.Crew + ";ids=" + Ids(s);
    static double MinPlayerDistance(Vector3 p)
    {
        double best = double.MaxValue;
        foreach (var pl in PhotonNetwork.PlayerList)
        {
            if (pl.IsInactive) continue;
            if (!RailExpedition.Position(pl.ActorNumber, out var head)) continue;
            best = Math.Min(best, Vector3.Distance(head, p));
        }
        return best == double.MaxValue ? 1e9 : best;
    }

    // Late-join sampling for either client: unreached blocks should reach crew 2, reached blocks and enemies should not change.
    static void JoinSample(Snap s, double since)
    {
        var bs = BlockStarts(); double dist = RailTrain.Instance.Distance;
        bool unreached = false, reachedAll = true, oldOk = true, enemyOk = true;
        for (int b = 0; b < bs.Length; b++)
        {
            if (dist < bs[b])
            {
                unreached = true;
                if (s.coal[b] != Expect(RailSpawnKind.Coal, b, 2) || s.gold[b] != Expect(RailSpawnKind.Gold, b, 2)) reachedAll = false;
            }
            else if (s.coal[b] != Expect(RailSpawnKind.Coal, b, 1) || s.gold[b] != Expect(RailSpawnKind.Gold, b, 1)) oldOk = false;
            if (s.enemy[b] != Expect(RailSpawnKind.Enemy, b, 1) || Expect(RailSpawnKind.Enemy, b, 1) != Expect(RailSpawnKind.Enemy, b, 2)) enemyOk = false;
        }
        Mx("oldBad", oldOk ? 0 : 1); Mx("enemyBad", enemyOk ? 0 : 1); Mx("joinDup", s.dupMarkers + s.dupIds);
        if (unreached && reachedAll && double.IsNaN(Get("tOk"))) { Put("tOk", since); Put("value", RailResourceBalance.CoalValue(RailExpedition.Crew)); }
        int fresh = 0;
        foreach (var kv in s.byMarker)
        {
            if (!kv.Key.StartsWith("coal-", StringComparison.Ordinal) && !kv.Key.StartsWith("gold-", StringComparison.Ordinal)) continue;
            if (joinBaseline.Contains(kv.Value.ViewID)) continue;
            fresh++;
            foreach (var p in PhotonNetwork.PlayerList)
            {
                if (p.IsInactive) continue;
                if (!RailExpedition.Position(p.ActorNumber, out var head)) continue;
                Mn(p.ActorNumber == PhotonNetwork.LocalPlayer.ActorNumber ? "dLocal" : "dRemote", Vector3.Distance(head, kv.Value.transform.position));
            }
        }
        Mx("newLoot", fresh);
    }
    static bool JoinPass() => Get("tOk") <= 8 && Get("oldBad") == 0 && Get("enemyBad") == 0 && Get("joinDup") == 0
        && (double.IsNaN(Get("dLocal")) || Get("dLocal") >= 35) && (double.IsNaN(Get("dRemote")) || Get("dRemote") >= 35)
        && Math.Abs(Get("value") - RailResourceBalance.CoalValue(2)) < .001 && RailExpedition.Crew == 2;
    static string JoinNote() => "tOk=" + Show(Get("tOk")) + " oldBad=" + Show(Get("oldBad")) + " enemyBad=" + Show(Get("enemyBad")) + " joinDup=" + Show(Get("joinDup"))
        + " newLoot=" + Show(Get("newLoot")) + " dLocal=" + Show(Get("dLocal")) + " dRemote=" + Show(Get("dRemote")) + " value=" + Show(Get("value")) + " crew=" + RailExpedition.Crew;

    static bool SoloPass(out string note)
    {
        var bs = BlockStarts(); bool ok = solo != null && bs.Length == 2 && RailExpedition.Crew == 1 && Math.Abs(RailResourceBalance.CoalValue(1) - 50.0 / 12) < .001;
        var parts = new List<string>();
        for (int b = 0; b < bs.Length && solo != null; b++)
        {
            int we = Expect(RailSpawnKind.Enemy, b, 1);
            if (solo.coal[b] != RailResourceBalance.CoalCount(1) || solo.gold[b] != RailResourceBalance.GoldCount(1) || solo.enemy[b] != we) ok = false;
            parts.Add("b" + b + " coal=" + (solo != null ? solo.coal[b] : -1) + "/" + RailResourceBalance.CoalCount(1) + " gold=" + (solo != null ? solo.gold[b] : -1) + "/" + RailResourceBalance.GoldCount(1) + " enemy=" + (solo != null ? solo.enemy[b] : -1) + "/" + we);
        }
        note = string.Join("; ", parts) + " crew=" + RailExpedition.Crew + " value=" + N(RailResourceBalance.CoalValue(RailExpedition.Crew));
        return ok;
    }

    // Moved-item bookkeeping for case 3.
    static bool ExtrasGone(Snap s, out string note)
    {
        var bs = BlockStarts(); double dist = RailTrain.Instance.Distance; bool ok = true; var parts = new List<string>();
        PhotonView mv = null;
        bool movedThere = movedIndex >= 0 && s.byMarker.TryGetValue(movedMarker, out mv) && mv.ViewID == movedId;
        double movedDist = movedThere ? Vector3.Distance(mv.transform.position, RailExpedition.Instance.coalPoints[movedIndex].position) : -1;
        for (int b = 0; b < bs.Length; b++)
        {
            bool unreached = dist < bs[b];
            int extra = unreached && movedThere && b == movedBlock ? 1 : 0;
            int wantCoal = Expect(RailSpawnKind.Coal, b, 1) + extra, wantGold = Expect(RailSpawnKind.Gold, b, 1);
            if (s.coal[b] != wantCoal || s.gold[b] != wantGold) ok = false;
            parts.Add("b" + b + (unreached ? "u" : "r") + " coal=" + s.coal[b] + "/" + wantCoal + " gold=" + s.gold[b] + "/" + wantGold);
        }
        if (!movedThere || movedDist <= .75) ok = false;
        note = string.Join(" ", parts) + " moved=" + movedThere + " movedDist=" + N(movedDist);
        return ok;
    }
    static bool CrewTwoRestored(Snap s, out string note)
    {
        var bs = BlockStarts(); double dist = RailTrain.Instance.Distance;
        bool ok = RailExpedition.Crew == 2 && s.dupMarkers + s.dupIds == 0;
        PhotonView mv = null;
        bool movedThere = movedIndex >= 0 && s.byMarker.TryGetValue(movedMarker, out mv) && mv.ViewID == movedId;
        ok &= movedThere;
        var parts = new List<string>();
        for (int b = 0; b < bs.Length; b++)
        {
            bool unreached = dist < bs[b];
            int wantCoal = Expect(RailSpawnKind.Coal, b, unreached ? 2 : 1), wantGold = Expect(RailSpawnKind.Gold, b, unreached ? 2 : 1);
            if (s.coal[b] != wantCoal || s.gold[b] != wantGold) ok = false;
            parts.Add("b" + b + (unreached ? "u" : "r") + " coal=" + s.coal[b] + "/" + wantCoal + " gold=" + s.gold[b] + "/" + wantGold);
        }
        note = string.Join(" ", parts) + " moved=" + movedThere + " crew=" + RailExpedition.Crew + " dup=" + (s.dupMarkers + s.dupIds);
        return ok;
    }

    // ---------- pursuit helpers ----------
    static PhotonView Pursuit()
    {
        string runId = PhotonSession.RunId;
        foreach (var v in PhotonNetwork.PhotonViewCollection)
        {
            var d = v == null ? null : v.InstantiationData as object[];
            if (d != null && d.Length >= 3 && (d[d.Length - 2] as string) == runId && (d[d.Length - 1] as string) == "pursuit-minion") return v;
        }
        return null;
    }
    static Vector3 MinionPos(PhotonView v)
    {
        var e = v.GetComponent<Game.AI.Enemy>();
        return e != null && e.Root != null ? e.Root.position : v.transform.position;
    }
    // Signed distance along the seam direction (XZ only); negative is the Motel side.
    static double Along(Vector3 p, Vector3 seam, Vector3 fwd)
    {
        var d = p - seam; d.y = 0;
        var f2 = fwd; f2.y = 0;
        return Vector3.Dot(d, f2.normalized);
    }
    static void PursuitRow(PhotonView m, Vector3 seam, Vector3 fwd)
    {
        var e = m.GetComponent<Game.AI.Enemy>();
        Vector3 p = MinionPos(m);
        string state = e != null ? e.CurrentState.ToString() : "none";
        try { File.AppendAllText(PursuitPath(Role), N(PhotonNetwork.Time) + "," + N(p.x) + "," + N(p.z) + "," + N(Along(p, seam, fwd)) + "," + state + "\n"); } catch (IOException) { }
    }
    static List<Row> ReadCsv(string path)
    {
        var rows = new List<Row>();
        if (!File.Exists(path)) return rows;
        foreach (var line in File.ReadAllLines(path))
        {
            var c = line.Split(',');
            if (c.Length < 5) continue;
            if (!double.TryParse(c[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var t)) continue;
            if (!double.TryParse(c[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) continue;
            if (!double.TryParse(c[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) continue;
            if (!double.TryParse(c[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var along)) continue;
            rows.Add(new Row { t = t, x = x, z = z, along = along, state = c[4] });
        }
        return rows;
    }

    // ---------- main tick ----------
    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .1;
        try
        {
            if (!File.Exists(ControlPath)) return;
            string json = File.ReadAllText(ControlPath);
            if (string.IsNullOrEmpty(json)) { control = null; return; }
            try { control = JsonUtility.FromJson<Control>(json); } catch (ArgumentException) { return; }
            if (control == null || !DateTime.TryParse(control.expires, out var expires) || DateTime.UtcNow > expires.ToUniversalTime()) { control = null; return; }
            if (token != control.token)
            {
                token = control.token; revision = -1; done = false; failures = 0; ResetRun();
                Log("START crew batch two-client test; Unity " + Application.unityVersion + " role=" + Role);
            }
            if (done) return;
            var session = PhotonSession.Instance;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + session?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0);
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles) if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                ack = action = false; Array.Clear(f, 0, f.Length); M.Clear();
                readySince = arrivedAt = nextPursuit = 0;
                hostJoinAt = peerJoinAt = leaveAt = handoffAt = handbackAt = markMove = 0; lastChaseAge = -1;
                Log("step=" + control.step);
            }
            double t = EditorApplication.timeSinceStartup, age = t - entered;
            Require(age < StepLimit[Math.Min(control.step, StepLimit.Length - 1)], "Timeout step=" + control.step + " " + Where() + " status=" + session?.Status);
            var route = RailRoute.Instance; var run = RailExpedition.Instance; var train = RailTrain.Instance;
            bool live = run != null && route != null && train != null;

            switch (control.step)
            {
                case 0: // Host creates the private code room; the peer stays in the public compound.
                {
                    if (!Host) { if (Ready && !PhotonSession.IsPrivate) Ack("waiting in public compound", true); break; }
                    if (!Ready) break;
                    if (!action && Settled) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    if (!InStaging || !Settled) break;
                    Ack("created private " + PhotonNetwork.CurrentRoom.Name, true);
                    if (AllAck) Next(1);
                    break;
                }

                case 1: // Case 1: host launches solo; the peer stays in the public compound. Host runs to block 0 and stops.
                {
                    if (!Host)
                    {
                        if (Ready && !PhotonSession.IsPrivate) Ack("held in public compound during solo run", PhotonSession.Phase == RailPhase.Compound);
                        break;
                    }
                    if (!f[0] && !live && PhotonNetwork.IsMasterClient && InStaging && Settled && ActivePlayers == 1 && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH solo " + Where()); }
                    if (!live) break;
                    if (!f[0])
                    {
                        if (PhotonNetwork.IsMasterClient && InStaging && Settled && ActivePlayers == 1 && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH solo " + Where()); }
                        if (!ArrivedAndQuiet() || ActivePlayers != 1) break;
                        Require(route.Selection != null && route.Selection.blocks.Length >= 2 && route.Selection.blocks[0] == MotelId && route.Selection.blocks[1] == FarmId, "Route is not Motel then Farm");
                        solo = Take(); soloIds = new HashSet<int>(solo.loot); f[0] = true;
                        Log("SOLO " + Counts(solo));
                        session.SetTrainMoving(true);
                        break;
                    }
                    if (!f[1])
                    {
                        if (train.Distance < BlockStarts()[0] + 5) break;
                        session.SetTrainMoving(false); f[1] = true; Log("STOP after block 0 reached " + Where());
                        break;
                    }
                    if (train.Speed > .05f) break;
                    bool pass1 = SoloPass(out string note1);
                    Ack("solo " + note1, pass1, Dump(solo));
                    if (AllAck) { VerdictDetail("1 solo crew 1 counts: coal 18/gold 10 per block, value 4.17", ReadReceipt("main").pass, "main[" + ReadReceipt("main").note + "] player2 in compound[" + ReadReceipt("player2")?.note + "]"); Next(2); }
                    break;
                }

                case 2: // Case 2: late join by code while the train is between blocks 0 and 1.
                {
                    if (!Host)
                    {
                        if (!PhotonSession.IsPrivate && Ready && (!action || age % 3 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; Log("JOIN by code " + Where()); }
                        if (!live || !PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Running || ActivePlayers != 2 || !Settled) break;
                        if (peerJoinAt == 0) { peerJoinAt = t; joinBaseline = new HashSet<int>(Take().loot); }
                        var sp = Take(); JoinSample(sp, t - peerJoinAt);
                        if (t - peerJoinAt < 10) break;
                        Ack(JoinNote(), JoinPass(), Dump(sp));
                        break;
                    }
                    if (!live) break;
                    if (hostJoinAt == 0) { if (ActivePlayers != 2) break; hostJoinAt = t; joinBaseline = soloIds; Log("PEER joined " + Where()); }
                    var sh = Take(); JoinSample(sh, t - hostJoinAt);
                    if (t - hostJoinAt < 10) break;
                    Ack(JoinNote(), JoinPass(), Dump(sh));
                    if (AllAck)
                    {
                        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                        Verdict("2 late join: unreached blocks reach crew 2 within 8 s; reached blocks and enemies unchanged; no loot within 35 m", m.pass && p.pass, m, p);
                        Next(3);
                    }
                    break;
                }

                case 3: // Case 3 setup: host moves one crew-2 extra in an unreached block; the peer waits.
                {
                    if (!Host) { if (Ready) Ack("waiting for the host to move one extra", true); break; }
                    if (!live) break;
                    if (!f[0])
                    {
                        var s = Take(); var bs = BlockStarts(); var pts = run.coalPoints; double dist = train.Distance;
                        int pick = -1; double best = -1;
                        for (int i = 0; i < pts.Length; i++)
                        {
                            int b = BlockOf(run.coalStart, i, bs);
                            if (b < 0 || Need(run.coalCrew, i) != 2 || run.coalStart[i] <= dist) continue;
                            if (!s.byMarker.TryGetValue("coal-" + i, out var v) || soloIds.Contains(v.ViewID)) continue;
                            double near = MinPlayerDistance(v.transform.position);
                            if (near > best) { best = near; pick = i; }
                        }
                        if (pick >= 0)
                        {
                            var view = s.byMarker["coal-" + pick];
                            movedId = view.ViewID; movedIndex = pick; movedMarker = "coal-" + pick; movedBlock = BlockOf(run.coalStart, pick, bs);
                            view.transform.position = pts[pick].position + Vector3.right * 3f;
                        }
                        f[0] = true; markMove = t;
                        Log("MOVE crew-2 extra " + movedMarker + " id=" + movedId + " block=" + movedBlock + " nearestPlayer=" + N(best));
                        break;
                    }
                    if (t - markMove < 1.5) break;
                    var s2 = Take();
                    PhotonView mv = null;
                    bool movedThere = movedIndex >= 0 && s2.byMarker.TryGetValue(movedMarker, out mv) && mv.ViewID == movedId;
                    double dd = movedThere ? Vector3.Distance(mv.transform.position, run.coalPoints[movedIndex].position) : -1;
                    Ack("moved " + movedMarker + " id=" + movedId + " block=" + movedBlock + " dist=" + N(dd) + " m", movedThere && dd > .75, Dump(s2));
                    if (AllAck) Next(4);
                    break;
                }

                case 4: // Case 3: peer leaves to a public compound; after 25 s untouched crew-2 extras are gone; the moved item stays.
                {
                    if (!Host)
                    {
                        if (!action && PhotonSession.IsPrivate && Ready) { session.RequestCreatePublic(); action = true; readySince = 0; Log("LEAVE to public " + Where()); }
                        if (action && !PhotonSession.IsPrivate && Ready) Ack("left to public compound", true);
                        break;
                    }
                    if (!live) break;
                    if (leaveAt == 0) { if (ActivePlayers == 1) { leaveAt = t; Log("PEER gone " + Where()); } break; }
                    double since = t - leaveAt;
                    if (since < 25) break;
                    var gone = Take();
                    bool okGone = ExtrasGone(gone, out string noteGone);
                    if (!okGone && since < 35) break;
                    threeLeaveOk = okGone; threeLeaveNote = noteGone + " since=" + N(since);
                    Ack("leave " + threeLeaveNote, okGone, Dump(gone));
                    if (AllAck) Next(5);
                    break;
                }

                case 5: // Case 3: peer rejoins by code; crew 2 returns with no duplicates and the same loot IDs on both clients.
                {
                    if (!Host)
                    {
                        if (!PhotonSession.IsPrivate && Ready && (!action || age % 3 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; Log("REJOIN by code " + Where()); }
                        if (!live || !PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Running || ActivePlayers != 2 || !Settled) break;
                        if (peerJoinAt == 0) peerJoinAt = t;
                        var sp = Take(); bool okP = CrewTwoRestored(sp, out string noteP);
                        if (!okP && t - peerJoinAt < 20) break;
                        Ack("rejoined " + noteP, okP, Dump(sp));
                        break;
                    }
                    if (!live || ActivePlayers != 2 || !Settled) break;
                    if (hostJoinAt == 0) hostJoinAt = t;
                    var sh = Take(); bool okH = CrewTwoRestored(sh, out string noteH);
                    if (!okH && t - hostJoinAt < 20) break;
                    Ack("rejoin " + noteH, okH, Dump(sh));
                    if (AllAck)
                    {
                        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                        string idsM = Field(m.data, "ids"), idsP = Field(p.data, "ids");
                        bool idsSame = !string.IsNullOrEmpty(idsM) && idsM == idsP;
                        VerdictDetail("3 held/moved crew-2 extra survives the leave; extras gone by 25 s; rejoin restores crew 2 with the same loot IDs",
                            threeLeaveOk && m.pass && p.pass && idsSame,
                            "leave[" + threeLeaveNote + "] rejoin main[" + m.note + "] player2[" + p.note + "] sameIds=" + idsSame + " idCount=" + (idsM ?? "").Split(',').Length);
                        Next(6);
                    }
                    break;
                }

                case 6: // Case 4: host hands master to the peer; both sample counts 5 s after the change.
                {
                    if (Host)
                    {
                        if (!live) break;
                        if (!f[0])
                        {
                            if (ActivePlayers == 2 && Settled && PhotonNetwork.IsMasterClient) { PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerListOthers[0]); f[0] = true; Log("HANDOFF to peer " + Where()); }
                            break;
                        }
                        if (PhotonNetwork.IsMasterClient) break;
                        if (handoffAt == 0) handoffAt = t;
                        if (t - handoffAt < 5) break;
                        var s = Take();
                        bool okH = ActivePlayers == 2 && RailExpedition.Crew == 2 && s.dupMarkers + s.dupIds == 0;
                        Ack("host after handoff " + Counts(s) + " crew=" + RailExpedition.Crew, okH, Dump(s));
                        if (AllAck)
                        {
                            var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                            fourCountsOk = Field(m.data, "c") == Field(p.data, "c") && Field(m.data, "g") == Field(p.data, "g") && Field(m.data, "e") == Field(p.data, "e")
                                && Field(m.data, "dup") == "0" && Field(p.data, "dup") == "0";
                            Log("CASE4 counts identical=" + fourCountsOk);
                            Next(7);
                        }
                        break;
                    }
                    if (!live) break;
                    if (!PhotonNetwork.IsMasterClient) break;
                    if (handoffAt == 0) handoffAt = t;
                    if (t - handoffAt < 5) break;
                    var sp = Take();
                    bool okP2 = ActivePlayers == 2 && RailExpedition.Crew == 2 && sp.dupMarkers + sp.dupIds == 0;
                    Ack("peer as master " + Counts(sp) + " crew=" + RailExpedition.Crew, okP2, Dump(sp));
                    break;
                }

                case 7: // Case 4: master returns to the host.
                {
                    if (!Host)
                    {
                        if (!action && PhotonNetwork.IsMasterClient && ActivePlayers == 2) { PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerListOthers[0]); action = true; Log("HANDBACK to host"); }
                        if (action && !PhotonNetwork.IsMasterClient) { if (handbackAt == 0) handbackAt = t; if (t - handbackAt >= 3) Ack("handed back to host", true); }
                        break;
                    }
                    if (!live || !PhotonNetwork.IsMasterClient) break;
                    if (handbackAt == 0) handbackAt = t;
                    if (t - handbackAt < 3) break;
                    Ack("master again " + Where(), ActivePlayers == 2);
                    if (AllAck)
                    {
                        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                        VerdictDetail("4 master handoff keeps per-block counts identical on both clients; master returns",
                            fourCountsOk && m.pass && p.pass, "counts[" + fourCountsOk + "] main[" + m.note + "] player2[" + p.note + "]");
                        Next(8);
                    }
                    break;
                }

                case 8: // Case 5: run ID is 32 lowercase hex and identical on both clients.
                {
                    string runId = PhotonSession.RunId ?? "";
                    bool valid = Regex.IsMatch(runId, "^[0-9a-f]{32}$") && PhotonSession.Phase == RailPhase.Running;
                    Ack("run " + runId + " valid=" + valid, valid, "runId=" + runId);
                    if (Host && AllAck)
                    {
                        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                        string a = Field(m.data, "runId"), b = Field(p.data, "runId");
                        VerdictDetail("5 run ID is 32 lowercase hex and identical on both clients", m.pass && p.pass && a == b, "main=" + a + " player2=" + b);
                        Next(9);
                    }
                    break;
                }

                case 9: // Case 6a: host stops the train near the seam, spawns a minion 10 m on the Motel side, warps main 6 m into the Farm side.
                {
                    if (!live || route.Blocks.Count < 2) break;
                    Require(route.Selection != null && route.Selection.blocks.Length >= 2 && route.Selection.blocks[1] == FarmId, "Route block 1 is not " + FarmId);
                    var seamT = route.Blocks[1].transform; Vector3 seam = seamT.position, fwd = seamT.forward;
                    var minion = Pursuit();
                    if (minion != null)
                    {
                        Vector3 mp = MinionPos(minion); double along = Along(mp, seam, fwd);
                        peakAlong = Math.Max(peakAlong, along);
                        if (t >= nextPursuit) { nextPursuit = t + .5; PursuitRow(minion, seam, fwd); }
                        if (along > 0 && crossedAt == 0) { crossedAt = t; Log("CROSSED seam along=" + N(along) + " " + Where()); }
                    }
                    if (Host)
                    {
                        if (!f[0]) { ForceAdmin(); if (!RailTestTools.FastTrainEnabled) RailAdminTools.ToggleTrainSpeed(); f[0] = true; Log("FAST 5x for the seam approach"); break; }
                        if (!f[1]) { if (!RailTestTools.FastTrainEnabled) break; session.SetTrainMoving(true); f[1] = true; Log("GO seam approach"); break; }
                        if (!f[2])
                        {
                            Vector3 tp = train.transform.position;
                            if (Along(tp, seam, fwd) < 0 && Vector3.Distance(tp, seam) < 15) { session.SetTrainMoving(false); f[2] = true; Log("STOP near seam dist=" + N(Vector3.Distance(tp, seam))); }
                            break;
                        }
                        if (!f[3])
                        {
                            if (train.Speed > .05f) break;
                            PhotonNetwork.InstantiateRoomObject(RailEnemySpawns.Prefab(0).name, seamT.TransformPoint(new Vector3(24f, .02f, -10f)), Quaternion.LookRotation(fwd), 0,
                                new object[] { "rails", PhotonSession.RunId, "pursuit-minion" });
                            f[3] = true; Log("SPAWN minion 10 m on the Motel side");
                            break;
                        }
                        if (!f[4])
                        {
                            if (minion == null) break;
                            Teleport(seamT.TransformPoint(new Vector3(24f, 1f, 6f))); f[4] = true; markMove = t;
                            Log("WARP main 6 m across the seam " + Where());
                        }
                    }
                    bool crossed = crossedAt > 0;
                    bool stuck = crossedAt == 0 && (age > 150 || (Host && f[4] && t - markMove > 30));
                    if (!crossed && !stuck) break;
                    if (crossed && t - crossedAt < 3) break;
                    Ack((crossed ? "crossed" : "no crossing") + " peakAlong=" + N(peakAlong) + " crossedAt=" + N(crossedAt - entered), crossed);
                    if (AllAck && Host)
                    {
                        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                        var mine = ReadCsv(PursuitPath("main")); var theirs = ReadCsv(PursuitPath("player2"));
                        int matched = 0; double maxDiff = 0;
                        foreach (var a in mine)
                        {
                            Row near = null; double best = double.MaxValue;
                            foreach (var b in theirs) { double dt = Math.Abs(a.t - b.t); if (dt < best) { best = dt; near = b; } }
                            if (near == null || best > .3) continue;
                            matched++;
                            maxDiff = Math.Max(maxDiff, Math.Sqrt((a.x - near.x) * (a.x - near.x) + (a.z - near.z) * (a.z - near.z)));
                        }
                        VerdictDetail("6a minion crosses the Motel/Farm seam; both clients agree within 2 m at matching times",
                            m.pass && p.pass && matched >= 5 && maxDiff <= 2,
                            "main[" + m.note + "] player2[" + p.note + "] matchedRows=" + matched + " maxDiff=" + N(maxDiff) + " m rowsMain=" + mine.Count + " rowsPlayer2=" + theirs.Count);
                        Next(10);
                    }
                    break;
                }

                case 10: // Case 6b: main is warped 60 m away; the minion should stop chasing and head back toward the Motel within 30 s.
                {
                    if (!live || route.Blocks.Count < 2) break;
                    var seamT = route.Blocks[1].transform; Vector3 seam = seamT.position, fwd = seamT.forward;
                    if (Host && !f[0]) { Teleport(seamT.TransformPoint(new Vector3(24f, 1f, 66f))); f[0] = true; Log("WARP main 60 m away on the Farm side " + Where()); }
                    var minion = Pursuit();
                    if (minion == null) { if (age > 30) Ack("minion missing", false); break; }
                    double along = Along(MinionPos(minion), seam, fwd);
                    if (t >= nextPursuit) { nextPursuit = t + .5; PursuitRow(minion, seam, fwd); }
                    var e = minion.GetComponent<Game.AI.Enemy>();
                    if (e != null && e.CurrentState == Game.AI.Enemy.State.Chase) lastChaseAge = age;
                    if (age < 30) break;
                    bool quiet = lastChaseAge < 25;
                    bool back = along < 0 || peakAlong - along >= 3;
                    Ack("end along=" + N(along) + " peakAlong=" + N(peakAlong) + " lastChaseAge=" + N(lastChaseAge) + " quiet=" + quiet + " back=" + back, quiet && back);
                    if (AllAck && Host)
                    {
                        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                        VerdictDetail("6b after the player leaves, minion stops chasing and heads back toward the Motel within 30 s", m.pass && p.pass,
                            "main[" + m.note + "] player2[" + p.note + "]");
                        Next(11);
                    }
                    break;
                }

                case 11:
                {
                    done = true;
                    string final = Host ? (failures == 0 ? "PASS COMPLETE" : "FAIL " + failures + " case(s) COMPLETE") : "DONE COMPLETE";
                    if (Host) AppendVerdict(final);
                    Log(final);
                    break;
                }
            }
        }
        catch (IOException) { }
        catch (Exception e)
        {
            done = true;
            Log("FAIL " + e.Message);
            if (Host) AppendVerdict("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }
}
#endif
