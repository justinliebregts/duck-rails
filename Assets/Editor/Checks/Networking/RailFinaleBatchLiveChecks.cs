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
/// Finale batch checks (playtest-queue: "5x train speed burns no fuel" and "Admin train speed and bridge lowering").
/// Two-client private expedition (main + MPPM Player2) with a shared control.json handshake. Cases:
/// 1) fuel at 1x, 60 s at 5x (flat, both clients equal), peer leave/rejoin during 5x, back to 1x (no jump), STOP and 5x/1x while stopped.
/// 2) bridge hold at 5x (time to 100%, midpoint reinforcement count); run replayed; bridge hold at 1x with release/resume,
///    speed switch to 5x mid-hold, peer leave/rejoin during the hold (progress agreement).
/// Output: Temp/rails-finale-batch/{main,player2}.txt (timestamped samples), verdicts.txt (PASS/FAIL per case).
/// Runtime code is not instrumented; all reads go through existing public state.
/// </summary>
[InitializeOnLoad]
public static class RailFinaleBatchLiveChecks
{
    [Serializable] sealed class Control { public string token, expires, privateCode; public int step, revision; }
    [Serializable] sealed class Receipt { public string token, note, error, data; public int revision; public bool pass; }
    const string Key = "Rails.FinaleBatch";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-finale-batch";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";

    static Control control;
    static string token, networkState, prevRun, run1 = "";
    static int revision = -1, failures;
    static bool ack, done, guardStop;
    static readonly bool[] f = new bool[10];
    static bool action, action2;
    static double entered, nextTick, readySince, arrivedAt, actionAt, markA, markB, markJ, markR, markS, leaveAt, nextSample;
    static double lastP = -1;
    static float lastFuel = float.NaN, entryFuel = float.NaN;
    static readonly Dictionary<string, double> M = new();
    static readonly List<double[]> series = new();

    static RailFinaleBatchLiveChecks() { EditorApplication.update += Tick; EditorApplication.update += Restore; }

    // ---------- Start / Stop / restore (same pattern as RailBridgeSurgeLiveChecks) ----------
    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

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
        File.WriteAllText(ControlPath, JsonUtility.ToJson(new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(60).ToString("O"),
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
    static bool Settled { get { if (!Ready) { readySince = 0; return false; } if (readySince == 0) readySince = EditorApplication.timeSinceStartup; return EditorApplication.timeSinceStartup - readySince > 3; } }
    static bool InStaging => PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging;
    static bool AllPoses => PhotonNetwork.PlayerList.All(p => !p.IsInactive && PhotonPlayer.ForActor(p.ActorNumber) != null);
    static bool ArrivedAndQuiet()
    {
        if (PhotonSession.Phase != RailPhase.Running || !Settled) { arrivedAt = 0; return false; }
        if (arrivedAt == 0) { arrivedAt = EditorApplication.timeSinceStartup; Log("ARRIVED " + Where()); }
        return EditorApplication.timeSinceStartup - arrivedAt > 4;
    }
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void Put(string k, double v) => M[k] = v;
    static void Mx(string k, double v) { if (!M.TryGetValue(k, out var old) || v > old) M[k] = v; }
    static void Mn(string k, double v) { if (!M.TryGetValue(k, out var old) || v < old) M[k] = v; }
    static double Get(string k) => M.TryGetValue(k, out var v) ? v : double.NaN;
    static string Fmt() => string.Join(";", M.OrderBy(kv => kv.Key).Select(kv => kv.Key + "=" + kv.Value.ToString("R", CultureInfo.InvariantCulture)));
    static double Val(string data, string key)
    {
        if (string.IsNullOrEmpty(data)) return double.NaN;
        foreach (var part in data.Split(';'))
        {
            var kv = part.Split('=');
            if (kv.Length == 2 && kv[0] == key && double.TryParse(kv[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v;
        }
        return double.NaN;
    }
    static double ProgressAt(double t)
    {
        double best = double.NaN, bestDt = double.MaxValue;
        foreach (var s in series) { double dt = Math.Abs(s[0] - t); if (dt < bestDt) { bestDt = dt; best = s[1]; } }
        return best;
    }
    static int Waves()
    {
        string run = PhotonSession.RunId; int n = 0;
        foreach (var v in PhotonNetwork.PhotonViewCollection)
            if (v != null && v.InstantiationData is object[] d && d.Length >= 3 && d[d.Length - 2] as string == run && (d[d.Length - 1] as string ?? "").StartsWith("bridge-wave-")) n++;
        return n;
    }
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
    static void Case(string name, bool hostExtra, string detail)
    {
        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
        bool pass = hostExtra && m != null && p != null && m.pass && p.pass;
        if (!pass) failures++;
        Verdict("CASE " + name + " " + (pass ? "PASS" : "FAIL") + " | " + detail + " | main[" + m?.data + "] player2[" + p?.data + "]");
    }
    static void Track(double every, bool steady)
    {
        if (!PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Running || RailTrain.Instance == null || RailExpedition.Instance == null) return;
        double now = EditorApplication.timeSinceStartup;
        if (now < nextSample) return;
        nextSample = now + every;
        double t = PhotonNetwork.Time;
        float fuel = PhotonSession.CurrentFuel, speed = RailTrain.Instance.Speed;
        double p = RailExpedition.Instance.State.BridgeProgress(t);
        Put("n", (double.IsNaN(Get("n")) ? 0 : Get("n")) + 1);
        if (!M.ContainsKey("first")) Put("first", fuel);
        Mn("fuelMin", fuel); Mx("fuelMax", fuel); Put("last", fuel); Put("sp", speed); Put("dist", RailTrain.Instance.Distance);
        if (speed > 15f) { Mn("f5min", fuel); Mx("f5max", fuel); Put("n5", (double.IsNaN(Get("n5")) ? 0 : Get("n5")) + 1); }
        if (steady) { Mn("spMin", speed); Mx("spMax", speed); }
        if (lastP >= 0) { if (p < lastP - .0005) Mx("back", 1); Mx("stepMax", p - lastP); }
        lastP = p; series.Add(new[] { t, p }); Put("p", p);
        lastFuel = fuel;
        Log("SAMPLE t=" + t.ToString("F3") + " fuel=" + fuel.ToString("F4") + " speed=" + speed.ToString("F2") + " dist=" + RailTrain.Instance.Distance.ToString("F1")
            + " fast=" + RailTestTools.FastTrainEnabled + " progress=" + p.ToString("F4") + " waves=" + Waves() + " guard=" + guardStop);
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
                token = control.token; revision = -1; done = false; failures = 0; series.Clear(); guardStop = false;
                Log("START finale batch two-client test; Unity " + Application.unityVersion + " role=" + Role);
            }
            if (done) return;
            var session = PhotonSession.Instance;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + session?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0);
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles) if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                ack = action = action2 = false; Array.Clear(f, 0, f.Length); M.Clear();
                readySince = arrivedAt = actionAt = markA = markB = markJ = markR = markS = leaveAt = 0; nextSample = 0; lastP = -1;
                entryFuel = lastFuel;
                Log("step=" + control.step);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 420, "Timeout step=" + control.step + " " + Where() + " status=" + session?.Status);
            var run = RailExpedition.Instance;
            if (Host && PhotonSession.Phase == RailPhase.Running && !string.IsNullOrEmpty(PhotonSession.RunId)) run1 = PhotonSession.RunId;
            double now = PhotonNetwork.Time;

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

                case 2: // Launch the run.
                    if (PhotonNetwork.IsMasterClient && InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH " + Where()); }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    Ack("private run " + PhotonSession.RunId, PhotonSession.Phase == RailPhase.Running);
                    if (Host && AllAck) Next(3);
                    break;

                case 3: // C1a: 1x baseline, fuel drains.
                    if (Host && !f[0] && age > 2 && RailTrain.Instance.Speed < .05f) { session.SetTrainMoving(true); f[0] = true; Log("GO 1x"); }
                    Track(2, age >= 5);
                    if (age < 10) break;
                    Ack("baseline 1x", !double.IsNaN(Get("spMin")) && Get("spMin") > 3 && Get("n") >= 3);
                    if (Host && AllAck) { Case("baseline 1x", ReadReceipt("main").pass, "speed 4 m/s; fuel draining before the 5x toggle"); Next(4); }
                    break;

                case 4: // C1b: 5x for 60 s; fuel flat; peer leaves and rejoins by code during 5x.
                    if (Host)
                    {
                        if (!f[0]) { ForceAdmin(); RailAdminTools.ToggleTrainSpeed(); f[0] = true; Log("TOGGLE 5x (admin) fuel=" + PhotonSession.CurrentFuel.ToString("F4")); }
                        if (!f[1] && run != null && RailRoute.Instance != null && RailTrain.Instance.Distance > RailRoute.Instance.FinaleStart - 60)
                        { session.SetTrainMoving(false); f[1] = true; guardStop = true; Log("ROUTE GUARD: stopped before the bridge (distance " + RailTrain.Instance.Distance.ToString("F1") + ")"); }
                    }
                    else
                    {
                        if (!f[2] && age >= 20 && PhotonSession.IsPrivate && Ready) { session.RequestCreatePublic(); f[2] = true; markJ = EditorApplication.timeSinceStartup; Log("LEAVE to public " + Where()); }
                        else if (f[2] && !f[3] && Ready && !PhotonSession.IsPrivate && EditorApplication.timeSinceStartup - markJ >= 8) { session.RequestJoinCode(control.privateCode); f[3] = true; Log("REJOIN by code " + Where()); }
                        else if (f[3] && !f[4] && PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Running && Settled && run != null)
                        { f[4] = true; Put("rejoinFuel", PhotonSession.CurrentFuel); Put("rejoinT", now); Put("players", ActivePlayers); Log("REJOINED fuel=" + PhotonSession.CurrentFuel.ToString("F4")); }
                    }
                    Track(.5, age >= 5);
                    if (age < 60) break;
                    if (!Host && !f[4]) break;
                    if (Host) Ack("5x driving", Get("n5") >= 5 && Get("f5max") - Get("f5min") < .01);
                    else Ack("rejoined", f[4] && Get("n5") >= 5 && Get("f5max") - Get("f5min") < .01);
                    if (Host && AllAck)
                    {
                        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                        double dFuel = Math.Abs(Val(m.data, "last") - Val(p.data, "rejoinFuel"));
                        Case("5x fuel flat 60 s; rejoin matches host", m.pass && p.pass && dFuel < .01 && !double.IsNaN(dFuel),
                            "host last=" + Val(m.data, "last").ToString("F4") + " peer rejoinFuel=" + Val(p.data, "rejoinFuel").ToString("F4") + " dFuel=" + dFuel.ToString("F4"));
                        Next(5);
                    }
                    break;

                case 5: // C1c: back to 1x: no jump, draining resumes.
                    if (Host && !f[0] && age > 1) { ForceAdmin(); RailAdminTools.ToggleTrainSpeed(); f[0] = true; Log("TOGGLE 1x (admin)"); }
                    if (Host && f[0] && !f[1] && age > 3) { session.SetTrainMoving(true); f[1] = true; Log("GO 1x after toggle (train was stopped by route guard)"); }
                    Track(2, age >= 5);
                    if (age < 10) break;
                    {
                        double jump = Math.Abs(Get("first") - entryFuel);
                        bool pass = jump <= .05 && Get("last") < Get("first") && Get("sp") > 3 && Get("sp") < 5;
                        Put("jump", jump);
                        Ack("toggle 1x", pass);
                        if (Host && AllAck) { Case("5x->1x no fuel jump; draining resumes", ReadReceipt("main").pass, "entry=" + entryFuel.ToString("F4") + " jump=" + jump.ToString("F4")); Next(6); }
                    }
                    break;

                case 6: // C1d: STOP, then 5x and 1x toggles while stopped: speed stays 0.
                    if (Host && !f[0] && age > 1) { session.SetTrainMoving(false); f[0] = true; Log("STOP train"); }
                    if (Host && f[0] && !f[1] && age >= 4) { ForceAdmin(); RailAdminTools.ToggleTrainSpeed(); f[1] = true; Log("TOGGLE 5x while stopped"); }
                    if (Host && f[1] && !f[2] && age >= 7) { RailAdminTools.ToggleTrainSpeed(); f[2] = true; Log("TOGGLE 1x while stopped"); }
                    Track(2, age >= 4);
                    if (age < 10) break;
                    Ack("stopped toggles", Get("spMax") < .05 && (!Host || f[2]));
                    if (Host && AllAck) { Case("stopped: 5x and 1x toggles keep speed 0", ReadReceipt("main").pass, "max speed after STOP=" + Get("spMax").ToString("F3")); Next(7); }
                    break;

                case 7: // C2a: 5x hold timing and midpoint reinforcement.
                    if (Host)
                    {
                        if (!f[0]) { if (age > 1 && age % 3 < .1) { RailTestTools.Bridge(); Log("BRIDGE approach (test tool): " + RailTestTools.Status); } if (RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40) f[0] = true; break; }
                        if (!f[1] && RailTrain.Instance.Speed < .05f && RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40)
                        { ForceAdmin(); if (!RailTestTools.FastTrainEnabled) RailAdminTools.ToggleTrainSpeed(); f[1] = true; Log("FAST 5x requested"); }
                        if (f[1] && !f[2] && RailTestTools.FastTrainEnabled) { Teleport(run.bridgeButton.position + new Vector3(0, 0, -1.5f)); actionAt = EditorApplication.timeSinceStartup; f[2] = true; Log("WARP to lever"); }
                        if (f[2] && !f[3] && EditorApplication.timeSinceStartup - actionAt > 2) { RailBridgeHoldFixture.SetHeld(true); f[3] = true; markA = now; Log("HOLD on (5x) at " + now.ToString("F3")); }
                    }
                    Track(1, true);
                    if (run != null && !f[5] && (f[3] || !Host) && run.State.BridgeProgress(now) >= 1)
                    { f[5] = true; markB = now; if (Host) RailBridgeHoldFixture.SetHeld(false); Log("FULL observed at " + now.ToString("F3")); }
                    if (!f[5] || now - markB < 3) break;
                    Put("full", markB); Put("waves", Waves()); Put("rc", run.State.bridgeReinforcementCount); Put("fast", RailTestTools.FastTrainEnabled ? 1 : 0);
                    if (Host) { Put("hold", markA); Put("t100", markB - markA); }
                    Ack("5x hold", Host ? (markB - markA) >= 10.5 && (markB - markA) <= 13.5 && Waves() == 1 : Waves() == 1);
                    if (Host && AllAck)
                    {
                        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
                        Case("5x hold ~12 s; one midpoint reinforcement", m.pass && p.pass && Math.Abs(Val(m.data, "full") - Val(p.data, "full")) < 1,
                            "t100=" + Val(m.data, "t100").ToString("F2") + "s waves main=" + Val(m.data, "waves") + " player2=" + Val(p.data, "waves") + " rc=" + Val(m.data, "rc"));
                        Next(8);
                    }
                    break;

                case 8: // Cross the lowered bridge to end run 1; wait for staging.
                    if (Host && !f[0] && age > 1) { session.SetTrainMoving(true); f[0] = true; Log("GO across bridge"); }
                    if (!InStaging || !Settled) break;
                    Ack("staging after run 1", PhotonSession.Phase == RailPhase.Staging);
                    if (Host && AllAck) { Case("run 1 ends and returns to staging", ReadReceipt("main").pass, "run1=" + Short(run1)); Next(20); }
                    break;

                case 20: // Replay: new private run.
                    if (!f[0]) { prevRun = run1; f[0] = true; }
                    if (Host && InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH run 2"); }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    Ack("run2=" + PhotonSession.RunId + " prev=" + Short(prevRun), PhotonSession.RunId != prevRun);
                    if (Host && AllAck) { Case("replay starts a new run", ReadReceipt("main").pass, "run2=" + Short(PhotonSession.RunId) + " run1=" + Short(prevRun)); Next(21); }
                    break;

                case 21: // C2b: full 1x hold (60 s active) with one release/resume, peer leave/rejoin during the hold.
                    if (Host)
                    {
                        if (!f[0]) { if (age > 1 && age % 3 < .1) { RailTestTools.Bridge(); Log("BRIDGE approach (test tool): " + RailTestTools.Status); } if (RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40) f[0] = true; break; }
                        if (!f[1] && RailTrain.Instance.Speed < .05f && RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40)
                        { ForceAdmin(); if (RailTestTools.FastTrainEnabled) RailAdminTools.ToggleTrainSpeed(); f[1] = true; Log("SPEED 1x requested"); }
                        if (f[1] && !f[2] && !RailTestTools.FastTrainEnabled) { Teleport(run.bridgeButton.position + new Vector3(0, 0, -1.5f)); actionAt = EditorApplication.timeSinceStartup; f[2] = true; Log("WARP to lever (1x)"); }
                        if (f[2] && !f[3] && EditorApplication.timeSinceStartup - actionAt > 2) { RailBridgeHoldFixture.SetHeld(true); f[3] = true; markA = now; Log("HOLD on (1x) at " + now.ToString("F3")); }
                        if (f[3] && !f[4] && run.State.BridgeProgress(now) >= .15) { RailBridgeHoldFixture.SetHeld(false); f[4] = true; markR = now; Log("RELEASE at progress " + run.State.BridgeProgress(now).ToString("F3")); }
                        if (f[4] && !f[5] && now - markR > 1) { Mn("relMin", run.State.BridgeProgress(now)); Mx("relMax", run.State.BridgeProgress(now)); }
                        if (f[4] && !f[5] && now - markR >= 4) { RailBridgeHoldFixture.SetHeld(true); f[5] = true; markS = now; Log("RESUME hold"); }
                        if (f[3] && !f[7] && run.State.BridgeProgress(now) >= 1) { f[7] = true; markB = now; RailBridgeHoldFixture.SetHeld(false); Log("FULL observed (host)"); }
                    }
                    else
                    {
                        double p = run != null ? run.State.BridgeProgress(now) : double.NaN;
                        if (PhotonNetwork.IsMasterClient && age % 3 < .1) { var mainPlayer = PhotonNetwork.PlayerList.FirstOrDefault(pl => !pl.IsLocal && !pl.IsInactive); if (mainPlayer != null) { PhotonNetwork.SetMasterClient(mainPlayer); Log("HAND master back to main"); } }
                        if (!f[0] && !double.IsNaN(p) && p >= .3 && PhotonSession.IsPrivate) { session.RequestCreatePublic(); f[0] = true; markJ = EditorApplication.timeSinceStartup; Log("LEAVE public mid-hold p=" + p.ToString("F3")); }
                        else if (f[0] && !f[1] && Ready && !PhotonSession.IsPrivate && EditorApplication.timeSinceStartup - markJ >= 8) { session.RequestJoinCode(control.privateCode); f[1] = true; Log("REJOIN by code mid-hold"); }
                        else if (f[1] && !f[2] && PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Running && Settled && run != null)
                        { f[2] = true; Put("rejT", now); Put("rejP", p); markR = EditorApplication.timeSinceStartup; Log("REJOINED p=" + p.ToString("F4")); }
                        else if (f[2] && !f[3] && EditorApplication.timeSinceStartup - markR >= 3 && run != null)
                        { f[3] = true; Put("p2", p); Put("p2T", now); Log("AFTER REJOIN 3s p=" + p.ToString("F4")); }
                        if (run != null && f[3] && !f[4] && p >= 1) { f[4] = true; Put("full", now); Log("FULL observed (player2) at " + now.ToString("F3")); }
                        if (f[4]) { Ack("hold 1x done", true); }
                    }
                    Track(1, true);
                    if (!Host || !f[7] || now - markB < 3) break;
                    Put("full", markB); Put("waves", Waves()); Put("t100", markB - markA);
                    Put("relRange", Get("relMax") - Get("relMin")); Put("active", (markB - markA) - (markS - markR));
                    Ack("1x hold done", double.IsNaN(Get("back")) && Get("stepMax") <= .1);
                    if (AllAck)
                    {
                        var m = ReadReceipt("main"); var pr = ReadReceipt("player2");
                        double hostAtRejoin = ProgressAt(Val(pr.data, "rejT"));
                        double rejP = Val(pr.data, "rejP");
                        bool rejoinMatch = Math.Abs(hostAtRejoin - rejP) < .03;
                        bool advancing = Val(pr.data, "p2") > rejP;
                        bool noBack = double.IsNaN(Val(m.data, "back"));
                        bool frozen = Val(m.data, "relRange") < .01;
                        bool stepOk = Val(m.data, "stepMax") <= .1; double active = Val(m.data, "active"); bool timing = active >= 57 && active <= 63 && Val(m.data, "waves") == 1;
                        double t100 = Val(m.data, "t100");
                        Case("1x hold ~60 s active; release/resume; late join matches; one reinforcement",
                            m.pass && pr.pass && rejoinMatch && advancing && noBack && frozen && stepOk && timing && Math.Abs(Val(m.data, "full") - Val(pr.data, "full")) < 1,
                            "t100=" + t100.ToString("F2") + "s active=" + active.ToString("F2") + "s (expected 60 +-3) rejoin host=" + hostAtRejoin.ToString("F4")
                            + " peer=" + rejP.ToString("F4") + " p2=" + Val(pr.data, "p2").ToString("F4") + " release range=" + Val(m.data, "relRange").ToString("F5")
                            + " maxStep=" + Val(m.data, "stepMax").ToString("F4") + " waves=" + Val(m.data, "waves"));
                        Next(22);
                    }
                    break;

                case 22: // End run 2 and return to staging.
                    if (Host && !f[0] && age > 1) { session.SetTrainMoving(true); f[0] = true; Log("GO across bridge (run 2)"); }
                    if (!InStaging || !Settled) break;
                    Ack("staging after run 2", PhotonSession.Phase == RailPhase.Staging);
                    if (Host && AllAck) { Case("run 2 ends and returns to staging", ReadReceipt("main").pass, "run2 ended"); Next(23); }
                    break;

                case 23:
                    done = true;
                    Log((Host ? (failures == 0 ? "PASS" : "FAIL " + failures + " case(s)") : "DONE") + " COMPLETE");
                    break;
            }
        }
        catch (IOException) { }
        catch (Exception e)
        {
            done = true; RailBridgeHoldFixture.SetHeld(false);
            Log("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }

    static void ForceAdmin()
    {
        var session = PlayerSession.Instance;
        if (session == null) return;
        if (!session.IsAdmin) typeof(PlayerSession).GetProperty("IsAdmin").GetSetMethod(true).Invoke(session, new object[] { true });
        RailAdminTools.Refresh();
    }
}
#endif
