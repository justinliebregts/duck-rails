#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Two-client playtest for "Final bridge driving handoff" and "Victory music return fade" (playtest-queue.md, 2026-11-23),
/// plus an incomplete-bridge crash. Main editor (the master for test tools) drives the scripted steps. Player2 detects
/// shared train, bridge, run and music state itself and sends its own GO request.
/// Cases: 1) 1x approach, bridge lowered to 100%, stop midspan, Player2 GO restart, takeover and escape credit;
/// 2) replayed run, bridge lowered at 5x, approach at 5x without stopping, takeover ramp and escape credit;
/// 3a/3b) victory music audible until the fade, about 3 s fade, one music start per client, in endings A and B;
/// 4) replayed run, bridge held to about 50% and released, approach at 1x crashes at bridge entry.
/// Takeover point: the finish ("Bridge crossed") fires when the train front reaches finale-local z110 (bridgeZ + BridgeSpan,
/// bridgeZ = finale z + 80, RailRoute.cs:197). Bridge collision fires at finale-local z80 (RailExpedition.cs:326-328).
/// Output: Temp/rails-finale-drive/{main,player2}.txt (timestamped samples) and verdicts.txt (CASE lines, then final line).
/// Runtime code is not instrumented; all reads go through existing public or reflected state.
/// </summary>
[InitializeOnLoad]
public static class RailFinaleDriveLiveChecks
{
    [Serializable] sealed class Control { public string token, expires, privateCode; public int step, revision; }
    [Serializable] sealed class Receipt { public string token, note, error, data; public int revision; public bool pass; }
    struct TrainRow { public double t, cmd0; public string run; public float d, v, local; public RailPhase ph; public int grip; }
    struct MusicRow { public double t; public string run; public float vol; public bool playing; public RailPhase ph; public int sources; }
    sealed class Fin { public double t, progress; public float local, v; public string reason; public bool success; public int escaped, players; }

    const string Key = "Rails.FinaleDrive";
    const double StepLimit = 420, TrainEvery = .25, MusicEvery = .1;
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-finale-drive";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    static Control control;
    static string token, networkState, runA = "", runB = "", runC = "", windowRun = "";
    static int revision = -1, failures;
    static bool ack, done, action, action2, musicWasPlaying;
    static readonly bool[] f = new bool[12];
    static double entered, nextTick, readySince, arrivedAt, actionAt, markA, markB, markR, markS, nextTrainSample, nextMusicSample, nextHandBack;
    static readonly Dictionary<string, double> M = new();
    static readonly List<TrainRow> trainRows = new();
    static readonly List<MusicRow> musicRows = new();
    static readonly Dictionary<string, Fin> fins = new();
    static readonly Dictionary<string, int> musicStarts = new();

    static RailFinaleDriveLiveChecks() { EditorApplication.update += Poll; EditorApplication.update += Tick; EditorApplication.update += Restore; }

    // ---------- Start / Stop / restore (same pattern as RailFinaleBatchLiveChecks) ----------
    public static void Start()
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); }
        File.WriteAllText(Folder + "/verdicts.txt", "");
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetString(Key + ".order", EditorJsonUtility.ToJson(RailTestBlockOrder.instance));
        SessionState.SetBool(Key, true);
        RailEditorQuickStart.Enabled = false;
        RailTestBlockOrder.instance.useOrder = true;
        RailTestBlockOrder.instance.blocks = new() { AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/convenience-store-v1.prefab").GetComponent<RailBlock>() };
        RailTestBlockOrder.instance.invincibleFromEnemies = true;
        RailTestBlockOrder.instance.Persist(); RailTestBlockOrderPanel.PublishPlayerOptions();
        File.WriteAllText(ControlPath, JsonUtility.ToJson(new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(45).ToString("O"),
            privateCode = UnityEngine.Random.Range(10000, 99999).ToString() }));
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        Manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(Manager, null);
    }

    public static void Stop()
    {
        RailBridgeHoldFixture.SetHeld(false);
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
    static void Verdict(string line) { try { File.AppendAllText(Folder + "/verdicts.txt", DateTime.UtcNow.ToString("O") + " " + line + "\n"); } catch (IOException) { } Log("VERDICT " + line); }
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " phase=" + PhotonSession.Phase + " run=" + Short(PhotonSession.RunId) + " step=" + control?.step + " master=" + PhotonNetwork.IsMasterClient;
    static string Short(string s) => string.IsNullOrEmpty(s) ? "-" : s.Substring(0, Math.Min(6, s.Length));
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest;
    static int ActivePlayers => PhotonNetwork.PlayerList.Count(p => !p.IsInactive);
    static bool AllPoses => PhotonNetwork.PlayerList.All(p => !p.IsInactive && PhotonPlayer.ForActor(p.ActorNumber) != null);
    static bool Settled { get { if (!Ready) { readySince = 0; return false; } if (readySince == 0) readySince = EditorApplication.timeSinceStartup; return EditorApplication.timeSinceStartup - readySince > 3; } }
    static bool InStaging => PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging;
    static bool ArrivedAndQuiet()
    {
        if (PhotonSession.Phase != RailPhase.Running || !Settled) { arrivedAt = 0; return false; }
        if (arrivedAt == 0) { arrivedAt = EditorApplication.timeSinceStartup; Log("ARRIVED " + Where()); }
        return EditorApplication.timeSinceStartup - arrivedAt > 4;
    }
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(Array.IndexOf(Roles, Role) == 0 ? -.8f : .8f, .35f, -1)));
    static bool LocalAboard()
    {
        var train = RailTrain.Instance; var local = PhotonNetwork.LocalPlayer;
        return train != null && local != null && RailExpedition.Position(local.ActorNumber, out var head) && train.ContainsPadded(head);
    }
    static bool AllAboard()
    {
        var train = RailTrain.Instance;
        return train != null && ActivePlayers == 2 && PhotonNetwork.PlayerList.Where(p => !p.IsInactive)
            .All(p => RailExpedition.Position(p.ActorNumber, out var point) && train.ContainsPadded(point));
    }
    /// <summary>Warp this client aboard (retry every 2 s) and report whether both players are aboard.</summary>
    static bool EnsureAboard()
    {
        if (AllAboard()) return true;
        if (Ready && RailTrain.Instance != null && EditorApplication.timeSinceStartup - actionAt > 2 && !LocalAboard())
        { Board(); actionAt = EditorApplication.timeSinceStartup; Log("BOARD aboard=" + LocalAboard()); }
        return false;
    }
    static bool AtApproach() => RailTrain.Instance != null && RailRoute.Instance != null &&
        RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40 && RailTrain.Instance.Speed < .05f;
    /// <summary>Finale-local front position: train front (TrainFrontOffset ahead of the train origin) minus finale root z.</summary>
    static float Local()
    {
        if (RailTrain.Instance == null || RailRoute.Instance == null || RailRoute.Instance.finaleRoot == null) return float.NaN;
        return RailTrain.Instance.transform.position.z + RailExpedition.TrainFrontOffset - RailRoute.Instance.finaleRoot.position.z;
    }
    static void ForceAdmin()
    {
        var session = PlayerSession.Instance;
        if (session == null) return;
        if (!session.IsAdmin) typeof(PlayerSession).GetProperty("IsAdmin").GetSetMethod(true).Invoke(session, new object[] { true });
        RailAdminTools.Refresh();
    }
    /// <summary>Test tools need the master. If Player2 is master, it hands master back to main (every 3 s while it holds it).</summary>
    static void HandBackMaster()
    {
        if (Host || !PhotonNetwork.IsMasterClient || EditorApplication.timeSinceStartup < nextHandBack) return;
        nextHandBack = EditorApplication.timeSinceStartup + 3;
        var main = PhotonNetwork.PlayerList.FirstOrDefault(pl => !pl.IsLocal && !pl.IsInactive);
        if (main != null) { PhotonNetwork.SetMasterClient(main); Log("HAND master back to main"); }
    }
    static AudioSource DirectorSource(string field)
    {
        const System.Reflection.BindingFlags F = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        var director = typeof(RailAudioDirector).GetField("instance", F)?.GetValue(null);
        return director == null ? null : typeof(RailAudioDirector).GetField(field, F)?.GetValue(director) as AudioSource;
    }
    static int CountBridgeMusic() => UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a != null && a.name == "Bridge music" && a.isPlaying);
    static void Put(string k, double v) => M[k] = v;
    static string Fmt() => string.Join(";", M.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture)));
    static Fin FinOf(string run) => run.Length > 0 && fins.TryGetValue(run, out var fin) ? fin : null;
    static int StartsOf(string run) => musicStarts.TryGetValue(run, out var n) ? n : 0;
    static List<TrainRow> RowsOf(string run) => trainRows.Where(r => r.run == run).ToList();

    static void Ack(string note, bool pass)
    {
        if (ack) return;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, note = note, data = Fmt(), pass = pass }));
        Log("ACK step=" + control.step + " pass=" + pass + " " + note + " " + Fmt());
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
    /// <summary>Host only: both receipts must pass. Logs one CASE line with both clients' numbers.</summary>
    static bool Case(string name, bool extra, string detail)
    {
        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
        bool pass = extra && m != null && p != null && m.pass && p.pass;
        if (!pass) failures++;
        Verdict("CASE " + name + " " + (pass ? "PASS" : "FAIL") + " | " + detail + " | main[" + m?.data + "] player2[" + p?.data + "]");
        return pass;
    }

    // ---------- train and music sampling (every client, every editor update) ----------
    static void Track(double now)
    {
        if (now < nextTrainSample || !PhotonSession.IsPrivate || RailTrain.Instance == null || RailRoute.Instance == null || RailExpedition.Instance == null) return;
        var phase = PhotonSession.Phase; string run = PhotonSession.RunId;
        if (run.Length == 0 || (phase != RailPhase.Running && phase != RailPhase.Ending)) return;
        nextTrainSample = now + TrainEvery;
        var motion = PhotonSession.Get<object[]>(RailSessionRules.Motion, null);
        var row = new TrainRow
        {
            t = PhotonNetwork.Time, cmd0 = motion is { Length: 4 } && motion[0] is double start ? start : double.NaN, run = run,
            d = RailTrain.Instance.Distance, v = RailTrain.Instance.Speed, local = Local(), ph = phase, grip = PhotonSession.DriveGripActor
        };
        trainRows.Add(row);
        Log("SAMPLE t=" + row.t.ToString("F3") + " d=" + row.d.ToString("F2") + " v=" + row.v.ToString("F2") + " local=" + row.local.ToString("F2")
            + " phase=" + phase + " cmd0=" + row.cmd0.ToString("F3") + " grip=" + row.grip + " progress=" + RailExpedition.Instance.State.BridgeProgress(row.t).ToString("F3"));
    }

    static void Poll()
    {
        if (!EditorApplication.isPlaying || control == null || done) { musicWasPlaying = false; return; }
        double now = EditorApplication.timeSinceStartup;
        var phase = PhotonSession.Phase; string run = PhotonSession.RunId;
        if (phase == RailPhase.Ending && run.Length > 0) windowRun = run;
        var e = RailExpedition.Instance;
        if (e != null && run.Length > 0 && e.State.finished && e.State.run == run && !fins.ContainsKey(run) && RailTrain.Instance != null)
        {
            fins[run] = new Fin
            {
                t = PhotonNetwork.Time, local = Local(), v = RailTrain.Instance.Speed, reason = e.State.reason, success = e.State.success,
                escaped = e.State.players.Count(p => p.escaped), players = e.State.players.Count, progress = e.State.BridgeProgress(PhotonNetwork.Time)
            };
            var fin = fins[run];
            Log("FINISH run=" + Short(run) + " reason=" + fin.reason + " success=" + fin.success + " local=" + fin.local.ToString("F2") + " v=" + fin.v.ToString("F2")
                + " escaped=" + fin.escaped + "/" + fin.players + " progress=" + fin.progress.ToString("F3") + " " + Where());
        }
        Track(now);
        var music = DirectorSource("music");
        bool playing = music != null && music.isPlaying;
        if (playing && !musicWasPlaying && windowRun.Length > 0)
        {
            musicStarts[windowRun] = StartsOf(windowRun) + 1;
            Log("MUSIC start #" + musicStarts[windowRun] + " run=" + Short(windowRun) + " " + Where());
        }
        else if (!playing && musicWasPlaying) Log("MUSIC end " + Where());
        musicWasPlaying = playing;
        if (now >= nextMusicSample && windowRun.Length > 0 && (phase == RailPhase.Ending || phase == RailPhase.Returning || phase == RailPhase.Staging))
        {
            nextMusicSample = now + MusicEvery;
            musicRows.Add(new MusicRow { t = PhotonNetwork.Time, run = windowRun, vol = music != null ? music.volume : 0f, playing = playing, ph = phase,
                sources = playing ? CountBridgeMusic() : 0 });
        }
    }

    // ---------- case evaluation (each client computes its own numbers and writes them into its receipt) ----------
    static double MaxExcess(List<TrainRow> rows, int from)
    {
        double worst = 0;
        for (int i = Math.Max(1, from); i < rows.Count; i++)
        {
            double dt = rows[i].t - rows[i - 1].t;
            if (dt <= 0) continue;
            double expected = (rows[i - 1].v + rows[i].v) * .5 * dt;
            worst = Math.Max(worst, Math.Abs((rows[i].d - rows[i - 1].d) - expected));
        }
        return worst;
    }

    static bool EvalCase1()
    {
        var rows = RowsOf(runA); var fin = FinOf(runA);
        if (fin == null) return false;
        int i0 = rows.FindIndex(r => r.v > 3f);
        int iStop = i0 < 0 ? -1 : rows.FindIndex(i0, r => r.v < .05f);
        int iRestart = iStop < 0 ? -1 : rows.FindIndex(iStop, r => r.v > .05f);
        int iRamp = iRestart < 0 ? -1 : rows.FindIndex(iRestart, r => r.v >= 3.95f);
        if (iRamp < 0) { Put("c1_missing", 1); return false; }
        double ramp = rows[iRamp].t - rows[iRestart].cmd0;
        float minSpeed = float.MaxValue;
        for (int i = iRamp; i < rows.Count && rows[i].local < 109f; i++) minSpeed = Math.Min(minSpeed, rows[i].v);
        double excess = MaxExcess(rows, i0);
        var grips = rows.Select(r => r.grip).Distinct().ToList();
        Put("c1_stopLocal", rows[iStop].local); Put("c1_restartLocal", rows[iRestart].local); Put("c1_rampS", ramp); Put("c1_minV", minSpeed);
        Put("c1_maxExcess", excess); Put("c1_takeLocal", fin.local); Put("c1_escaped", fin.escaped); Put("c1_players", fin.players);
        Put("c1_success", fin.success ? 1 : 0); Put("c1_gripCount", grips.Count);
        Log("CASE1 control grip actors seen=[" + string.Join(",", grips) + "] reason=" + fin.reason + " local at takeover=" + fin.local.ToString("F2"));
        return ramp >= 1.6 && ramp <= 2.4 && minSpeed >= 3.9f && excess <= .5 && fin.local >= 109f && fin.local <= 112f
            && fin.reason == "Bridge crossed" && fin.success && fin.escaped == 2 && fin.players == 2;
    }

    static bool EvalCase2()
    {
        var rows = RowsOf(runB); var fin = FinOf(runB);
        if (fin == null) return false;
        int iFast = rows.FindIndex(r => r.v > 19f);
        int iEnd = rows.FindIndex(r => r.ph == RailPhase.Ending);
        if (iFast < 0 || iEnd <= iFast) { Put("c2_missing", 1); return false; }
        float approach = 0f, minApproach = float.MaxValue;
        for (int i = iFast; i < iEnd; i++) { approach = Math.Max(approach, rows[i].v); minApproach = Math.Min(minApproach, rows[i].v); }
        double cmdE = rows[iEnd].cmd0;
        int iRamp = rows.FindIndex(iEnd, r => r.v <= 4.05f);
        double ramp = iRamp < 0 ? double.NaN : rows[iRamp].t - cmdE;
        float maxAfter = 0f, prev = float.MaxValue; bool monotone = true;
        for (int i = iEnd; i < rows.Count; i++)
        {
            maxAfter = Math.Max(maxAfter, rows[i].v);
            if (rows[i].t - cmdE <= 2.25) { if (rows[i].v > prev + .05f) monotone = false; prev = rows[i].v; }
        }
        double excess = MaxExcess(rows, iFast);
        Put("c2_approach", approach); Put("c2_minApproach", minApproach); Put("c2_rampS", ramp); Put("c2_maxAfter", maxAfter);
        Put("c2_monotone", monotone ? 1 : 0); Put("c2_maxExcess", excess); Put("c2_takeLocal", fin.local); Put("c2_escaped", fin.escaped); Put("c2_players", fin.players);
        return approach >= 19.5f && approach <= 20.05f && minApproach >= 19.5f && ramp >= 1.6 && ramp <= 2.4
            && maxAfter <= approach + .05f && monotone && excess <= .5 && fin.local >= 109f && fin.local <= 112f
            && fin.reason == "Bridge crossed" && fin.success && fin.escaped == 2 && fin.players == 2;
    }

    static bool EvalCase4()
    {
        var fin = FinOf(runC);
        if (fin == null) return false;
        Put("c4_crashLocal", fin.local); Put("c4_progress", fin.progress); Put("c4_escaped", fin.escaped); Put("c4_success", fin.success ? 1 : 0);
        Put("c4_music", StartsOf(runC));
        return fin.reason == "Bridge collision" && !fin.success && fin.escaped == 0 && fin.local >= 79.5f && fin.local <= 82f
            && fin.progress >= .4 && fin.progress <= .6;
    }

    /// <summary>Victory music for one successful ending: audible until the fade, fade 2.5-3.5 s, one start, one playing source.</summary>
    static bool EvalMusic(string run, out bool complete)
    {
        complete = false;
        var rows = musicRows.Where(r => r.run == run).ToList();
        int i0 = rows.FindIndex(r => r.ph == RailPhase.Ending);
        if (i0 < 0) return false;
        float peak = 0f;
        for (int i = i0; i < rows.Count; i++) peak = Math.Max(peak, rows[i].vol);
        int iPeak = rows.FindIndex(i0, r => r.vol >= peak - .01f);
        int iFade = rows.FindIndex(iPeak, r => r.vol < peak - .01f);
        int iZero = iFade < 0 ? -1 : rows.FindIndex(iFade, r => r.vol <= .001f);
        complete = iZero >= 0;
        int lastAudible = iFade < 0 ? rows.Count - 1 : iFade - 1;
        bool audible = true;
        for (int i = i0; i <= lastAudible; i++)
            if (rows[i].t - rows[i0].t >= 1 && (!rows[i].playing || rows[i].vol <= 0f)) audible = false;
        int maxSources = 0;
        for (int i = i0; i < rows.Count; i++) maxSources = Math.Max(maxSources, rows[i].sources);
        int iReturn = rows.FindIndex(i0, r => r.ph == RailPhase.Returning);
        double returnAt = iReturn < 0 ? double.NaN : rows[iReturn].t - rows[i0].t;
        double fadeStartAt = iFade < 0 ? double.NaN : rows[iFade].t - rows[i0].t;
        double fadeSec = complete ? rows[iZero].t - rows[iFade].t : double.NaN;
        int starts = StartsOf(run);
        Put("m_peak", peak); Put("m_audible", audible ? 1 : 0); Put("m_fadeStartS", fadeStartAt); Put("m_fadeSec", fadeSec);
        Put("m_fadePhase", iFade < 0 ? -1 : (int)rows[iFade].ph); Put("m_returnS", returnAt); Put("m_starts", starts); Put("m_maxSources", maxSources);
        return complete && peak >= .5f && audible && fadeSec >= 2.5 && fadeSec <= 3.5 && starts == 1 && maxSources <= 1;
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
                token = control.token; revision = -1; done = false; failures = 0;
                trainRows.Clear(); musicRows.Clear(); fins.Clear(); musicStarts.Clear();
                runA = runB = runC = windowRun = "";
                Log("START finale drive two-client test; Unity " + Application.unityVersion + " role=" + Role);
            }
            if (done) return;
            var session = PhotonSession.Instance;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + session?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0)
                + " master=" + PhotonNetwork.IsMasterClient;
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles) if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                ack = action = action2 = false; Array.Clear(f, 0, f.Length); M.Clear();
                readySince = arrivedAt = actionAt = markA = markB = markR = markS = 0;
                Log("step=" + control.step);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < StepLimit, "Timeout step=" + control.step + " " + Where() + " status=" + session?.Status);
            HandBackMaster();
            var run = RailExpedition.Instance;
            double now = PhotonNetwork.Time;
            int next = control.step + 1;

            switch (control.step)
            {
                case 0: // Host creates the private code room.
                    if (!Host) { if (Ready) Ack("waiting", true); break; }
                    if (!Ready) break;
                    if (!action && Settled) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    if (!InStaging || !Settled) break;
                    Ack("created private " + PhotonNetwork.CurrentRoom.Name, true);
                    if (AllAck) Next(1);
                    break;

                case 1: // Peer joins by code.
                    if (!Host && !PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!InStaging || !Settled || ActivePlayers != 2 || !AllPoses) break;
                    Ack("in private staging", true);
                    if (Host && AllAck) Next(2);
                    break;

                case 2: case 9: case 14: // Launch run A, B or C (master only).
                {
                    if (PhotonNetwork.IsMasterClient && InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH " + Where()); }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    string id = PhotonSession.RunId;
                    if (control.step == 2) runA = id; else if (control.step == 9) runB = id; else runC = id;
                    Ack("private run " + Short(id), PhotonSession.Phase == RailPhase.Running);
                    if (Host && AllAck) Next(next);
                    break;
                }

                case 3: // Run A: Host lowers the bridge to 100% at 1x; both clients confirm full lowering from shared progress.
                {
                    if (Host)
                    {
                        if (!f[0])
                        {
                            if (age > 1 && age % 3 < .1) { RailTestTools.Bridge(); Log("BRIDGE approach (test tool): " + RailTestTools.Status); }
                            if (AtApproach()) { f[0] = true; Log("AT approach " + Where() + " local=" + Local().ToString("F2")); }
                            break;
                        }
                        if (!f[1]) { Teleport(run.bridgeButton.position + new Vector3(0, 0, -1.5f)); actionAt = EditorApplication.timeSinceStartup; f[1] = true; break; }
                        if (!f[2] && EditorApplication.timeSinceStartup - actionAt > 2) { RailBridgeHoldFixture.SetHeld(true); f[2] = true; markA = now; Log("HOLD on (1x) at " + now.ToString("F3")); }
                        if (f[2] && !f[3] && run.State.BridgeProgress(now) >= 1) { RailBridgeHoldFixture.SetHeld(false); f[3] = true; markB = now; Log("FULL (host) t100=" + (markB - markA).ToString("F2")); }
                    }
                    bool full = run != null && run.State.BridgeProgress(now) >= 1;
                    if (full && (!Host || (f[3] && now - markB >= 1)) && !ack)
                        Ack("bridge full at 1x", RailTrain.Instance.Speed < .05f);
                    if (ack && Host && AllAck) Next(4);
                    break;
                }

                case 4: case 11: // Both board the stopped train.
                {
                    if (!EnsureAboard()) break;
                    if (!ack) Ack("aboard", true);
                    if (Host && AllAck) Next(next);
                    break;
                }

                case 5: // Run A: GO at 1x; Host stops midspan on the lowered bridge.
                {
                    if (Host)
                    {
                        if (!f[0] && age > 2 && RailTrain.Instance.Speed < .05f) { session.SetTrainMoving(true); f[0] = true; Log("GO 1x (master path) " + Where()); }
                        if (f[0] && !f[1] && Local() >= 95f) { session.SetTrainMoving(false); f[1] = true; Log("STOP midspan local=" + Local().ToString("F2")); }
                    }
                    if (!f[2] && RailTrain.Instance.Speed > 3f) { f[2] = true; Log("MOVING seen speed=" + RailTrain.Instance.Speed.ToString("F2")); }
                    bool stopped = f[2] && RailTrain.Instance.Speed < .05f && Local() >= 80f && Local() <= 110f;
                    if (!stopped) break;
                    if (!ack) Ack("stopped midspan local=" + Local().ToString("F2"), true);
                    if (Host && AllAck) Next(6);
                    break;
                }

                case 6: // Run A: Player2 sends GO through the non-master request path; both clients see the restart.
                {
                    if (!Host && age >= 2 && RailTrain.Instance.Speed < .05f && (!f[0] || age % 3 < .1))
                    { session.SetTrainMoving(true); f[0] = true; Log("GO request (Player2, non-master path) " + Where()); }
                    if (!f[1] && RailTrain.Instance.Speed > .5f) { f[1] = true; Log("RESTART seen speed=" + RailTrain.Instance.Speed.ToString("F2")); }
                    if (!f[1]) break;
                    if (!ack) Ack("restarted", true);
                    if (Host && AllAck) Next(7);
                    break;
                }

                case 7: // Run A: ride to the far bank; both clients observe the takeover and escape credit.
                {
                    if (FinOf(runA) == null) break;
                    if (!ack) Ack("takeover observed", EvalCase1());
                    if (Host && AllAck) { Case("1 slow approach: stop and GO restart on lowered bridge", true, "ramp 1.6-2.4 s, control to z110, no jump, both escaped"); Next(8); }
                    break;
                }

                case 8: // Back in staging after run A; victory music window (ending A) must complete first.
                {
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 5) break;
                    bool musicPass = EvalMusic(runA, out bool complete);
                    if (!complete) break;
                    if (!ack) Ack("staging after A", musicPass);
                    if (Host && AllAck) { Case("3a victory music return fade, ending A", true, "audible to fade, 2.5-3.5 s fade, one start"); Next(9); }
                    break;
                }

                case 10: // Run B: Host lowers the bridge at 5x (admin Train Speed) with one continuous hold.
                {
                    if (Host)
                    {
                        if (!f[0])
                        {
                            if (age > 1 && age % 3 < .1) { RailTestTools.Bridge(); Log("BRIDGE approach (test tool): " + RailTestTools.Status); }
                            if (AtApproach()) { f[0] = true; Log("AT approach " + Where()); }
                            break;
                        }
                        if (!f[1]) { ForceAdmin(); if (!RailTestTools.FastTrainEnabled) RailAdminTools.ToggleTrainSpeed(); f[1] = true; Log("FAST 5x requested"); break; }
                        if (!f[2] && RailTestTools.FastTrainEnabled) { Teleport(run.bridgeButton.position + new Vector3(0, 0, -1.5f)); actionAt = EditorApplication.timeSinceStartup; f[2] = true; Log("WARP to lever (5x)"); break; }
                        if (f[2] && !f[3] && EditorApplication.timeSinceStartup - actionAt > 2) { RailBridgeHoldFixture.SetHeld(true); f[3] = true; markA = now; Log("HOLD on (5x) at " + now.ToString("F3")); break; }
                        if (f[3] && !f[4] && run.State.BridgeProgress(now) >= 1) { RailBridgeHoldFixture.SetHeld(false); f[4] = true; markB = now; Log("FULL (host) t100=" + (markB - markA).ToString("F2")); }
                    }
                    bool full5 = run != null && run.State.BridgeProgress(now) >= 1 && RailTestTools.FastTrainEnabled;
                    if (!full5 || (Host && !(f[4] && now - markB >= 1))) break;
                    if (!ack) Ack("bridge full at 5x", true);
                    if (Host && AllAck) Next(11);
                    break;
                }

                case 12: // Run B: GO at 5x and approach without stopping; both clients observe takeover and escape credit.
                {
                    if (Host && !f[0] && age > 2 && RailTrain.Instance.Speed < .05f && RailTestTools.FastTrainEnabled)
                    { session.SetTrainMoving(true); f[0] = true; Log("GO 5x (master path) " + Where()); }
                    if (FinOf(runB) == null) break;
                    if (!ack) Ack("takeover observed", EvalCase2());
                    if (Host && AllAck) { Case("2 fast approach at 5x, no stop", true, "approach 20 m/s, ramp to 4 m/s in 1.6-2.4 s, both escaped"); Next(13); }
                    break;
                }

                case 13: // Back in staging after run B; victory music window (ending B).
                {
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 5) break;
                    bool musicPass = EvalMusic(runB, out bool complete);
                    if (!complete) break;
                    if (!ack) Ack("staging after B", musicPass);
                    if (Host && AllAck) { Case("3b victory music return fade, ending B", true, "audible to fade, 2.5-3.5 s fade, one start"); Next(14); }
                    break;
                }

                case 15: // Run C: Host holds the lever to about 50%, then releases; the bridge stays partly lowered.
                {
                    if (Host)
                    {
                        if (!f[0])
                        {
                            if (age > 1 && age % 3 < .1) { RailTestTools.Bridge(); Log("BRIDGE approach (test tool): " + RailTestTools.Status); }
                            if (AtApproach()) f[0] = true;
                            break;
                        }
                        if (!f[1]) { Teleport(run.bridgeButton.position + new Vector3(0, 0, -1.5f)); actionAt = EditorApplication.timeSinceStartup; f[1] = true; break; }
                        if (!f[2] && EditorApplication.timeSinceStartup - actionAt > 2) { RailBridgeHoldFixture.SetHeld(true); f[2] = true; markA = now; Log("HOLD on (1x) toward 50%"); break; }
                        if (f[2] && !f[3] && run.State.BridgeProgress(now) >= .5f) { RailBridgeHoldFixture.SetHeld(false); f[3] = true; markR = now; Log("RELEASE at progress " + run.State.BridgeProgress(now).ToString("F3")); }
                    }
                    bool stable = run != null && (!Host || f[3]) && !run.State.BridgeMoving(now)
                        && run.State.BridgeProgress(now) >= .45f && run.State.BridgeProgress(now) < .99f;
                    if (!stable) markS = 0;
                    else if (markS == 0) markS = EditorApplication.timeSinceStartup;
                    if (markS == 0 || EditorApplication.timeSinceStartup - markS < 2) break;
                    if (!ack) Ack("half-lowered and frozen progress=" + run.State.BridgeProgress(now).ToString("F3"), true);
                    if (Host && AllAck) Next(16);
                    break;
                }

                case 16: // Run C: board both, GO at 1x into the half-lowered bridge; both clients observe the crash.
                {
                    if (!EnsureAboard()) break;
                    if (Host && !f[0] && age > 3) { session.SetTrainMoving(true); f[0] = true; Log("GO 1x into half-lowered bridge " + Where()); }
                    if (FinOf(runC) == null) break;
                    if (!ack) Ack("crash observed", EvalCase4());
                    if (Host && AllAck) Next(17);
                    break;
                }

                case 17: // Back in staging after the crash.
                {
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 5) break;
                    if (!ack) Ack("staging after C", true);
                    if (Host && AllAck) { Case("4 incomplete bridge crash at entry", true, "bridge collision at z80, progress about 0.5, no escape credit"); Next(18); }
                    break;
                }

                case 18:
                    done = true;
                    if (Host) Verdict(failures == 0 ? "PASS COMPLETE" : "FAIL " + failures + " case(s) COMPLETE");
                    Log((Host ? (failures == 0 ? "PASS" : "FAIL " + failures + " case(s)") : "DONE") + " COMPLETE");
                    RailBridgeHoldFixture.SetHeld(false);
                    break;
            }
        }
        catch (IOException) { }
        catch (Exception e)
        {
            done = true; RailBridgeHoldFixture.SetHeld(false);
            Log("FAIL " + e.Message);
            if (Host) Verdict("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }
}
#endif
