#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Final Bridge surge checks. <see cref="RunUnitChecks"/> covers the run-state latch and marker helpers in Edit Mode.
/// <see cref="Start"/> runs a two-client private expedition (main + MPPM Player2): the host lowers the bridge through
/// the physical lever fixture (5x test speed), then both clients must see exactly one surge enemy per player, those
/// enemies must close on the stopped train, and a peer that leaves and rejoins mid-run must see the same surge with no
/// extra spawns. Real Photon rooms and existing test shortcuts only; runtime code is not instrumented.
/// Output: Temp/rails-bridge-surge/{main,player2}.txt.
/// </summary>
[InitializeOnLoad]
public static class RailBridgeSurgeLiveChecks
{
    [Serializable] sealed class Control { public string token, expires, privateCode; public int step, revision; }
    [Serializable] sealed class Receipt { public string token, note, error; public int revision, surge, stateCount; public bool pass; }
    const string Key = "Rails.BridgeSurge";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-bridge-surge";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";

    static Control control;
    static string token, networkState;
    static int revision = -1;
    static bool action, action2, ack, done, sawFull;
    static double entered, nextTick, readySince, arrivedAt, actionAt, fullAt, lastSample;
    static readonly Dictionary<string, float> startDistance = new(), minDistance = new();
    static int failures;

    static RailBridgeSurgeLiveChecks() { EditorApplication.update += Tick; EditorApplication.update += Restore; }

    // ---------- Edit Mode checks ----------
    public static string RunUnitChecks()
    {
        var lines = new List<string>(); int count = 0, failed = 0;
        void Check(bool ok, string name) { count++; if (!ok) failed++; lines.Add((ok ? "ok   " : "FAIL ") + name); }
        var s = new RailRunState { bridgeElapsed = RailRunState.BridgeSeconds - .5 };
        s.UpdateBridgeSurge(100, 3); Check(!s.bridgeSurgeTriggered && s.bridgeSurgeCount == 0, "No surge before 100%");
        s.bridgeElapsed = RailRunState.BridgeSeconds; s.UpdateBridgeSurge(100, 3);
        Check(s.bridgeSurgeTriggered && s.bridgeSurgeCount == 3, "Surge latches one per player at 100%");
        s = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(s)); s.UpdateBridgeSurge(200, 7);
        Check(s.bridgeSurgeCount == 3, "Handoff/late join/rejoin keeps the original count (once per run)");
        var held = new RailRunState(); held.UpdateBridgeHold(10, 11); held.UpdateBridgeSurge(10.5, 2);
        Check(!held.bridgeSurgeTriggered, "Lowering in progress does not trigger");
        var ended = new RailRunState { bridgeElapsed = RailRunState.BridgeSeconds, finished = true }; ended.UpdateBridgeSurge(100, 2);
        Check(!ended.bridgeSurgeTriggered, "Finished run cannot trigger");
        Check(!new RailRunState().bridgeSurgeTriggered, "Replay resets the surge");
        var solo = new RailRunState { bridgeElapsed = RailRunState.BridgeSeconds }; solo.UpdateBridgeSurge(1, 1);
        Check(solo.bridgeSurgeCount == 1, "Solo run gets one");
        Check(RailBridgeSurge.CountFor(0) == 0 && RailBridgeSurge.CountFor(8) == 8 && RailBridgeSurge.CountFor(20) == RailBridgeSurge.MaxCount, "Count clamps to the room size");
        Check(RailBridgeSurge.TryIndex("bridge-surge-4", out int i) && i == 4 && !RailBridgeSurge.IsSurge("bridge-wave-0") && !RailBridgeSurge.IsSurge("enemy-1") && !RailBridgeSurge.TryIndex("bridge-surge-x", out _), "Surge markers parse and stay distinct");
        Check(!RailBridgeEncounter.IsDefender("bridge-surge-0", 2, 4), "Surge enemies are not lever defenders");
        var finale = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/bridge-finale-v1.prefab")?.GetComponent<RailBlock>();
        var points = RailBridgeSurge.Points(finale);
        Check(points.Length >= 2 && finale.bridgeReinforcements.Length > 0 && points[0] == finale.bridgeReinforcements[0], "Finale surge points: reinforcement first, then distinct enemy candidates (" + points.Length + ")");
        Check(points.Select(p => p.position).Distinct().Count() == points.Length, "Surge points do not stack");
        lines.Insert(0, (failed == 0 ? "PASS " : "FAIL ") + (count - failed) + "/" + count + " bridge surge checks");
        string text = string.Join("\n", lines);
        Debug.Log(text);
        return text;
    }

    // ---------- Two-client live run ----------
    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    public static void Start()
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); }
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetString(Key + ".order", EditorJsonUtility.ToJson(RailTestBlockOrder.instance));
        SessionState.SetBool(Key, true);
        RailEditorQuickStart.Enabled = false;
        // Short deterministic route; enemies cannot down the crew so the run outlives the observation window.
        RailTestBlockOrder.instance.useOrder = true;
        RailTestBlockOrder.instance.blocks = new() { AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/convenience-store-v1.prefab").GetComponent<RailBlock>() };
        RailTestBlockOrder.instance.invincibleFromEnemies = true;
        RailTestBlockOrder.instance.Persist(); RailTestBlockOrderPanel.PublishPlayerOptions();
        File.WriteAllText(ControlPath, JsonUtility.ToJson(new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(40).ToString("O"),
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

    static void Log(string message) { try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n"); } catch (IOException) { } }
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " phase=" + PhotonSession.Phase + " run=" + Short(PhotonSession.RunId) + " step=" + control?.step + " master=" + PhotonNetwork.IsMasterClient;
    static string Short(string s) => string.IsNullOrEmpty(s) ? "-" : s.Substring(0, Math.Min(6, s.Length));
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static void Ack(string message, bool pass = true)
    {
        if (ack) return;
        var run = RailExpedition.Instance;
        int surge = Surge().Count, stateCount = run != null ? run.State.bridgeSurgeCount : -1;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, note = message, surge = surge, stateCount = stateCount, pass = pass }));
        Log("ACK step=" + control.step + " surge=" + surge + " stateCount=" + stateCount + " pass=" + pass + " " + message);
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
    static List<PhotonView> Surge()
    {
        var list = new List<PhotonView>();
        string run = PhotonSession.RunId;
        foreach (var v in PhotonNetwork.PhotonViewCollection)
            if (v != null && v.InstantiationData is object[] d && d.Length >= 3 && d[d.Length - 2] as string == run && RailBridgeSurge.IsSurge(d[d.Length - 1] as string)) list.Add(v);
        return list;
    }
    static string Marker(PhotonView v) => ((object[])v.InstantiationData).Last() as string;
    static string Describe(List<PhotonView> surge)
    {
        RailBridgeSurge.TryGetTrainGoal(out var goal);
        return string.Join(";", surge.Select(v => { var e = v.GetComponent<Game.AI.Enemy>(); var p = e != null && e.Root != null ? e.Root.position : v.transform.position;
            return Marker(v) + " " + v.name.Replace("(Clone)", "") + " owner=" + v.OwnerActorNr + " pos=" + p.ToString("F1") + " toTrain=" + Vector3.Distance(p, goal).ToString("F1") + " " + e?.CurrentState
                + (e?.Boarding != null ? " aboard=" + e.Boarding.IsAboard : ""); }));
    }

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
            if (token != control.token) { token = control.token; revision = -1; done = false; failures = 0; Log("START bridge surge two-client test; Unity " + Application.unityVersion + " role=" + Role); }
            if (done) return;
            var session = PhotonSession.Instance;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + session?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0);
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles) if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = action2 = ack = sawFull = false; readySince = arrivedAt = fullAt = 0; startDistance.Clear(); minDistance.Clear();
                Log("step=" + control.step);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 300, "Timeout step=" + control.step + " " + Where() + " status=" + session?.Status);
            var run = RailExpedition.Instance;
            switch (control.step)
            {
                case 0: // Host creates the private code room.
                    if (!Host) { if (Ready) Ack("waiting"); break; }
                    if (!Ready) break;
                    if (!action && Settled) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    if (!InStaging || !Settled) break;
                    Ack("created private " + PhotonNetwork.CurrentRoom.Name);
                    if (AllAck) Next(1);
                    break;
                case 1: // Peer joins by code.
                    if (!Host && !PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!InStaging || !Settled || ActivePlayers != 2 || !AllPoses) break;
                    Ack("in private staging");
                    if (Host && AllAck) Next(2);
                    break;
                case 2: // Launch.
                    if (PhotonNetwork.IsMasterClient && InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH " + Where()); }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    Require(Surge().Count == 0, "Surge enemies exist before the bridge was lowered");
                    Ack("private run " + PhotonSession.RunId);
                    if (Host && AllAck) Next(3);
                    break;
                case 3: // Train to the bridge approach; host lowers the bridge through the physical lever; peer rides aboard.
                {
                    if (PhotonSession.Phase != RailPhase.Running || !Ready || run == null || RailRoute.Instance == null || RailTrain.Instance == null) break;
                    bool atApproach = RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40;
                    float progress = run.State.BridgeProgress(PhotonNetwork.Time);
                    var surge = Surge();
                    if (progress < 1)
                    {
                        Require(surge.Count == 0 && !run.State.bridgeSurgeTriggered, "Surge before 100% (progress=" + progress.ToString("F3") + " count=" + surge.Count + ")");
                        if (Host)
                        {
                            if (!atApproach) { if (age % 2 < .1) { RailTestTools.Bridge(); Log("TEST Bridge(): " + RailTestTools.Status); } break; }
                            if (!RailTestTools.FastTrainEnabled) { if (age % 2 < .1) { RailTestTools.ToggleTrainSpeed(); Log("TEST fast: " + RailTestTools.Status); } break; }
                            if (!action) { Teleport(run.bridgeButton.position + new Vector3(0, 0, -1.5f)); action = true; actionAt = EditorApplication.timeSinceStartup; Log("WARP to lever " + Where()); break; }
                            if (!action2 && EditorApplication.timeSinceStartup - actionAt > 2) { action2 = true; RailBridgeHoldFixture.SetHeld(true); Log("HOLD lever fixture on"); }
                        }
                        else if (atApproach && !action) { Board(); action = true; Log("BOARD at approach distance=" + RailTrain.Instance.Distance.ToString("F1")); }
                        if (EditorApplication.timeSinceStartup - lastSample > 2) { lastSample = EditorApplication.timeSinceStartup; Log("SAMPLE progress=" + progress.ToString("F3") + " moving=" + run.State.BridgeMoving(PhotonNetwork.Time)); }
                        break;
                    }
                    if (fullAt == 0) { fullAt = EditorApplication.timeSinceStartup; RailBridgeHoldFixture.SetHeld(false); Log("BRIDGE 100% observed; surge now=" + surge.Count + " triggered=" + run.State.bridgeSurgeTriggered + " stateCount=" + run.State.bridgeSurgeCount); }
                    double wait = EditorApplication.timeSinceStartup - fullAt;
                    if (run.State.bridgeSurgeCount > 0 && surge.Count == run.State.bridgeSurgeCount && !sawFull) { sawFull = true; Log("SURGE complete after " + wait.ToString("F2") + "s " + Describe(surge)); }
                    if (!sawFull && wait < 8) break;
                    if (Host && !ack) Board();
                    bool pass = run.State.bridgeSurgeTriggered && run.State.bridgeSurgeCount == 2 && surge.Count == 2 && surge.Select(Marker).Distinct().Count() == 2;
                    Ack("surge at 100% " + Describe(surge), pass);
                    if (Host && AllAck) { Verdict("1 one surge enemy per player at 100%, seen by both clients"); Next(4); }
                    break;
                }
                case 4: // Aggro: surge enemies head for the stopped train with both players aboard. Count must not grow.
                {
                    if (!Ready || run == null) break;
                    if (!action) { Board(); action = true; }
                    var surge = Surge();
                    RailBridgeSurge.TryGetTrainGoal(out var goal);
                    foreach (var v in surge)
                    {
                        var e = v.GetComponent<Game.AI.Enemy>(); var p = e != null && e.Root != null ? e.Root.position : v.transform.position;
                        float d = Vector3.Distance(p, goal); string m = Marker(v);
                        if (!startDistance.ContainsKey(m)) { startDistance[m] = d; minDistance[m] = d; }
                        minDistance[m] = Mathf.Min(minDistance[m], d);
                    }
                    if (EditorApplication.timeSinceStartup - lastSample > 2) { lastSample = EditorApplication.timeSinceStartup; Log("SAMPLE " + Describe(surge)); }
                    Require(surge.Count <= 2, "Extra surge enemies spawned: " + surge.Count);
                    if (age < 25) break;
                    bool closed = startDistance.Count == 2 && startDistance.All(k => minDistance[k.Key] <= Mathf.Min(8f, k.Value - 3f) || minDistance[k.Key] <= 4f);
                    Ack("aggro " + string.Join(";", startDistance.Select(k => k.Key + " start=" + k.Value.ToString("F1") + " min=" + minDistance[k.Key].ToString("F1"))), surge.Count == 2 && (!Host || closed));
                    if (Host && AllAck) { Verdict("2 surge enemies close on the train; no duplicates"); Next(5); }
                    break;
                }
                case 5: // Peer leaves for a public compound, then rejoins the running private run by code (late join).
                    if (!Host)
                    {
                        if (!action && Ready && PhotonSession.IsPrivate) { session.RequestCreatePublic(); action = true; readySince = 0; Log("LEAVE to public " + Where()); break; }
                        if (action && !action2 && Ready && !PhotonSession.IsPrivate && Settled) { session.RequestJoinCode(control.privateCode); action2 = true; actionAt = EditorApplication.timeSinceStartup; readySince = 0; Log("REJOIN by code " + Where()); break; }
                        if (!action2 || !PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Running || !Settled || EditorApplication.timeSinceStartup - actionAt < 8) break;
                        var surge = Surge();
                        Ack("rejoined " + Describe(surge), run != null && run.State.bridgeSurgeCount == 2 && surge.Count == 2);
                    }
                    else
                    {
                        if (ActivePlayers == 1) action = true; // saw the peer leave
                        if (!action || ActivePlayers != 2 || !Settled || ReadReceipt("player2")?.revision != revision) break;
                        var surge = Surge();
                        Ack("peer back " + Describe(surge), run != null && run.State.bridgeSurgeCount == 2 && surge.Count == 2);
                    }
                    if (Host && AllAck) { Verdict("3 late rejoin sees the same surge; no respawn or recount"); Next(6); }
                    break;
                case 6:
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

    static void Verdict(string name)
    {
        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
        bool pass = m.pass && p.pass;
        if (!pass) failures++;
        Log("CASE " + name + " " + (pass ? "PASS" : "FAIL") + " | main surge=" + m.surge + " state=" + m.stateCount + " " + m.note + " | player2 surge=" + p.surge + " state=" + p.stateCount + " " + p.note);
    }
}
#endif
