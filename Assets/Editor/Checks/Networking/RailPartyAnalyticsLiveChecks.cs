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
/// Party-size analytics checks. <see cref="RunUnitChecks"/> covers launch/late classification and the once-per-run
/// claims in Edit Mode. <see cref="Start"/> runs two clients (main + MPPM Player2) through four runs and compares the
/// run:* events both clients would have sent with the expected set:
/// A public Compound departure together, crew down; B private solo launch, peer joins mid-run, leaves and rejoins,
/// then both cross the bridge; C launch together, master handed to the peer, crew down; D launch together, the peer
/// takes master and leaves, host goes down alone. Runtime code is not instrumented beyond DuckzAnalytics.Previewed.
/// Output: Temp/rails-party-analytics/{main,player2}.txt, events-*.txt, verdicts.txt.
/// </summary>
[InitializeOnLoad]
public static class RailPartyAnalyticsLiveChecks
{
    [Serializable] sealed class Control { public string token, expires, compound, privateCode; public int step, revision; }
    [Serializable] sealed class Receipt { public string token, note, error; public int revision; }
    const string Key = "Rails.PartyAnalytics";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-party-analytics";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    static string EventsPath(string role) => Folder + "/events-" + role + ".txt";

    static Control control;
    static string token, networkState, runSeen;
    static int revision = -1, failures;
    static bool action, action2, ack, done;
    static double entered, nextTick, readySince, arrivedAt, actionAt, doneAt;

    static RailPartyAnalyticsLiveChecks()
    {
        EditorApplication.update += Tick; EditorApplication.update += Restore;
        DuckzAnalytics.Previewed -= Record; DuckzAnalytics.Previewed += Record;
    }

    static void Record(string message)
    {
        if (control == null || done || !message.StartsWith("run:", StringComparison.Ordinal)) return;
        try { File.AppendAllText(EventsPath(Role), PhotonSession.RunId + "|" + message + "\n"); } catch (IOException e) { Log("EVENT LOST " + message + " " + e.Message); }
    }

    // ---------- Edit Mode checks ----------
    [MenuItem("Duck Rails/Tests/Run Party Analytics Checks")]
    public static void RunFromMenu() => RunUnitChecks();
    public static string RunUnitChecks()
    {
        var lines = new List<string>(); int count = 0, failed = 0;
        void Check(bool ok, string name) { count++; if (!ok) failed++; lines.Add((ok ? "ok   " : "FAIL ") + name); }
        RailParticipant Add(RailRunState s, string id, double now, params string[] roster)
        { var p = new RailParticipant { id = id }; s.Join(p, now, roster); s.players.Add(p); return p; }
        RailRunState Copy(RailRunState s) => JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(s));
        string[] duo = { "a", "b" };

        var run = new RailRunState();
        Check(Add(run, "a", 100, duo).launched, "Departing player at the first tick is a launch");
        run.startAt = 100; run.TrackCrew(100, 1, 1, duo);
        Check(run.startReporter == 1 && run.launchReporter == 0, "Start is claimed at once; launch waits for the slow loader");
        Check(Add(run, "b", 112, duo).launched, "Slow loader from the departure is still a launch");
        Check(!Add(run, "x", 113, duo).launched, "A stranger inside the window is a late join");
        run.TrackCrew(113, 3, 1, duo);
        Check(run.launchReporter == 1 && run.launchCrew == 2 && run.peakCrew == 3, "Launch party settles at 2, peak 3");
        Check(!Add(run, "y", 400, duo).launched, "Joining later is a late join");
        run = Copy(run); run.TrackCrew(400, 4, 2, duo);
        Check(run.startReporter == 1 && run.launchReporter == 1 && run.launchCrew == 2 && run.peakCrew == 4, "A new master keeps the earlier claims");
        Check(run.endReporter == 0, "No end claim while running");
        run.Finish(500, "Crew down", Array.Empty<string>()); run.TrackCrew(500, 3, 2, duo);
        Check(run.endReporter == 2 && run.endCrew == 3, "End claimed by the finishing master with the connected crew");
        run = Copy(run); run.TrackCrew(501, 1, 1, duo);
        Check(run.endReporter == 2 && run.endCrew == 3, "A later master cannot reclaim the end");
        Check(run.Player("b").launched && run.Player("b").joinedAt == 112 && !run.Player("x").launched, "Classification survives the snapshot");

        var missing = new RailRunState(); Add(missing, "a", 10, duo); missing.startAt = 10;
        missing.TrackCrew(40, 1, 1, duo); Check(missing.launchReporter == 0, "Waits through the window for a missing player");
        missing.TrackCrew(40.1, 1, 1, duo); Check(missing.launchCrew == 1, "A player who never arrives is not counted");
        Check(!Add(missing, "b", 41, duo).launched, "Arriving after the window is a late join");
        var quick = new RailRunState(); Add(quick, "a", 10, duo); quick.startAt = 10;
        quick.Finish(12, "Crew down", Array.Empty<string>()); quick.TrackCrew(12, 1, 1, duo);
        Check(quick.launchCrew == 1 && quick.launchReporter == 1 && quick.endReporter == 1, "A run that ends inside the window still reports both");
        var open = new RailRunState(); Add(open, "a", 10); open.startAt = 10;
        Check(Add(open, "b", 20).launched && !Add(open, "c", 41).launched, "No roster: the window alone decides");
        lines.Insert(0, (failed == 0 ? "PASS " : "FAIL ") + (count - failed) + "/" + count + " party analytics checks");
        string text = string.Join("\n", lines); Debug.Log(text); return text;
    }

    // ---------- Two-client live run ----------
    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    public static void Start()
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); File.WriteAllText(EventsPath(role), ""); }
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

    static void Log(string message) { try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n"); } catch (IOException) { } }
    static string Short(string s) => string.IsNullOrEmpty(s) ? "-" : s.Substring(0, Math.Min(6, s.Length));
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " kind=" + PhotonSession.Kind + " phase=" + PhotonSession.Phase + " run=" + Short(PhotonSession.RunId)
        + " step=" + control?.step + " master=" + PhotonNetwork.IsMasterClient;
    static string Crew(RailRunState s) => "launch=" + s.launchCrew + " peak=" + s.peakCrew + " end=" + s.endCrew + " reporters=" + s.startReporter + "/" + s.launchReporter + "/" + s.endReporter
        + " players=" + string.Join(",", s.players.Select(p => p.actor + (p.launched ? ":launch" : ":late")));
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static void Ack(string message)
    {
        if (ack) return;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, note = message }));
        Log("ACK step=" + control.step + " run=" + Short(runSeen) + " " + message);
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
    static bool AllPoses => PhotonNetwork.PlayerList.All(p => p.IsInactive || PhotonPlayer.ForActor(p.ActorNumber) != null);
    static bool Settled { get { if (!Ready) { readySince = 0; return false; } if (readySince == 0) readySince = EditorApplication.timeSinceStartup; return EditorApplication.timeSinceStartup - readySince > 3; } }
    static bool InCompound => PhotonSession.Kind == RailSessionRules.CompoundKind;
    static bool InStaging => PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging;
    static bool Running => PhotonSession.Phase == RailPhase.Running;
    static bool ArrivedAndQuiet()
    {
        if (!Running || !Settled) { arrivedAt = 0; return false; }
        if (arrivedAt == 0) { arrivedAt = EditorApplication.timeSinceStartup; Log("ARRIVED " + Where()); }
        return EditorApplication.timeSinceStartup - arrivedAt > 4;
    }
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static int Index => Array.IndexOf(Roles, Role);
    static void StandAway() => Teleport(new Vector3(8 + Index, .4f, 4));
    static void StandInBoarding()
    {
        if (Ready && RailWorld.Instance != null && RailWorld.Instance.boardingTrigger != null)
            Teleport(RailWorld.Instance.boardingTrigger.bounds.center - Vector3.up * .8f);
    }
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(Index == 0 ? -.8f : .8f, .35f, -1)));
    static void DownOffTrain()
    {
        if (!Ready || !Running || RailExpedition.Instance == null || RailTrain.Instance == null) return;
        if (!action) { Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(6 + Index, .5f, 0))); action = true; actionAt = EditorApplication.timeSinceStartup; Log("WARP off train"); return; }
        if (EditorApplication.timeSinceStartup - actionAt < 1) return;
        if (!RailExpedition.LocalDown) RailExpedition.RequestDown();
        else if (!action2) { action2 = true; Log("DOWN confirmed"); }
    }

    static void Verdict(string name, string run, params string[] expected)
    {
        var got = new List<string>(); var raw = new List<string>();
        foreach (var role in Roles)
            foreach (var line in File.ReadAllLines(EventsPath(role)))
            {
                var parts = line.Split(new[] { '|' }, 2);
                if (parts.Length != 2 || parts[0] != run) continue;
                raw.Add(role + " " + parts[1]);
                string id = parts[1].Split(' ')[0];
                if (!id.StartsWith("run:last_block", StringComparison.Ordinal)) got.Add(id);
            }
        bool pass = !string.IsNullOrEmpty(run) && got.OrderBy(x => x, StringComparer.Ordinal).SequenceEqual(expected.OrderBy(x => x, StringComparer.Ordinal));
        if (!pass) failures++;
        string text = "CASE " + name + " " + (pass ? "PASS" : "FAIL") + " run=" + Short(run) + "\n  " + string.Join("\n  ", raw) + (pass ? "" : "\n  expected: " + string.Join(", ", expected));
        Log(text); File.AppendAllText(Folder + "/verdicts.txt", text + "\n");
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
            if (token != control.token) { token = control.token; revision = -1; done = false; failures = 0; Log("START party analytics; Unity " + Application.unityVersion + " role=" + Role); }
            if (done) return;
            var session = PhotonSession.Instance;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + session?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0);
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles) if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = action2 = ack = false; readySince = arrivedAt = doneAt = 0;
                Log("step=" + control.step);
            }
            if (Running) runSeen = PhotonSession.RunId;
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 300, "Timeout step=" + control.step + " " + Where() + " status=" + session?.Status);
            var expedition = RailExpedition.Instance;
            switch (control.step)
            {
                case 0: // Host opens a fresh public Compound.
                    if (!Host || !Settled) break;
                    if (!action) { session.RequestCreatePublic(); action = true; readySince = 0; break; }
                    if (!InCompound) break;
                    control.compound = PhotonNetwork.CurrentRoom.Name; Next(1); break;
                case 1:
                    if (!Ready) break;
                    if (PhotonNetwork.CurrentRoom.Name != control.compound)
                    {
                        if (!Host && InCompound && age > 2 && (!action || age % 15 < .1)) { session.RequestJoinCode(control.compound); action = true; readySince = 0; }
                        break;
                    }
                    if (!Settled || ActivePlayers != 2 || !AllPoses) break;
                    StandAway(); Ack("gathered");
                    if (Host && AllAck) Next(2);
                    break;
                case 2: // A: depart together from the Compound.
                    if (InCompound) { StandInBoarding(); break; }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2 || expedition.State.launchReporter == 0) break;
                    Ack("departed " + Crew(expedition.State));
                    if (Host && AllAck) Next(3);
                    break;
                case 3:
                    if (!InCompound) { DownOffTrain(); break; }
                    if (!Settled) break;
                    StandAway(); Ack("back in compound");
                    if (Host && AllAck)
                    {
                        Verdict("A public departure of 2, crew down", runSeen, "run:start:public", "run:launch:test:p2", "run:join:test:launch", "run:join:test:launch",
                            "run:end:fail:crew_down", "run:result:test:p2:fail", "run:result_end:test:p2:fail");
                        Next(4);
                    }
                    break;
                case 4: // B: host opens a private room and launches alone.
                    if (!Host) { Ack("waiting"); break; }
                    if (!Ready) break;
                    if (!action) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    if (!InStaging || !Settled) break;
                    Ack("created private");
                    if (AllAck) Next(5);
                    break;
                case 5:
                    if (!Host) { Ack("waiting"); break; }
                    if (InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; }
                    if (!ArrivedAndQuiet() || expedition.State.launchReporter == 0) break;
                    Ack("solo launch " + Crew(expedition.State));
                    if (AllAck) Next(6);
                    break;
                case 6: // Peer joins mid-run, leaves, rejoins.
                    if (Host)
                    {
                        if (ReadReceipt("player2")?.revision != revision || ActivePlayers != 2 || !Settled) break;
                        Ack("peer here " + Crew(expedition.State));
                        if (AllAck) Next(7);
                        break;
                    }
                    if (doneAt == 0)
                    {
                        if (!PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                        if (PhotonSession.IsPrivate && ArrivedAndQuiet()) { doneAt = EditorApplication.timeSinceStartup; arrivedAt = 0; Log("JOINED mid-run " + Crew(expedition.State)); session.RequestCreatePublic(); }
                        break;
                    }
                    if (InCompound && Settled && !action2) { session.RequestJoinCode(control.privateCode); action2 = true; readySince = 0; break; }
                    if (!action2 || !PhotonSession.IsPrivate || !ArrivedAndQuiet()) break;
                    Ack("rejoined " + Crew(expedition.State));
                    break;
                case 7: // Lower the bridge and cross together.
                    if (Running && Ready && RailRoute.Instance != null && RailTrain.Instance != null && !expedition.State.finished)
                    {
                        bool atApproach = RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40;
                        float progress = expedition.State.BridgeProgress(PhotonNetwork.Time);
                        if (!Host) { if (atApproach && !action) { Board(); action = true; } break; }
                        if (!atApproach) { if (age % 2 < .1) RailTestTools.Bridge(); break; }
                        if (progress < 1)
                        {
                            if (!RailTestTools.FastTrainEnabled) { if (age % 2 < .1) RailTestTools.ToggleTrainSpeed(); break; }
                            if (!action) { Teleport(expedition.bridgeButton.position + new Vector3(0, 0, -1.5f)); action = true; actionAt = EditorApplication.timeSinceStartup; break; }
                            if (!action2 && EditorApplication.timeSinceStartup - actionAt > 2) { action2 = true; RailBridgeHoldFixture.SetHeld(true); Log("HOLD lever"); }
                            break;
                        }
                        RailBridgeHoldFixture.SetHeld(false);
                        if (doneAt == 0) { Board(); doneAt = EditorApplication.timeSinceStartup; Log("BRIDGE lowered, boarding"); }
                        else if (EditorApplication.timeSinceStartup - doneAt > 3 && RailTrain.Instance.Speed < .05f && age % 1.5 < .1) { session.SetTrainMoving(true); Log("GO"); }
                        break;
                    }
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 10) break;
                    Ack("back in staging");
                    if (Host && AllAck)
                    {
                        Verdict("B solo launch, mid-run join and rejoin, both escape", runSeen, "run:start:private", "run:launch:test:p1", "run:join:test:launch", "run:join:test:late",
                            "run:end:success:bridge_crossed", "run:result:test:p1:success", "run:result_end:test:p2:success");
                        Next(8);
                    }
                    break;
                case 8: case 10: // Launch the private room together.
                    if (PhotonNetwork.IsMasterClient && InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2 || expedition.State.launchReporter == 0) break;
                    Ack("private launch " + Crew(expedition.State));
                    if (Host && AllAck) Next(control.step + 1);
                    break;
                case 9: // C: master moves to the peer mid-run, then everyone goes down.
                    if (Running && doneAt == 0)
                    {
                        if (Host && PhotonNetwork.IsMasterClient && age > 1 && age % 2 < .1) { PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerListOthers[0]); Log("HANDOFF"); }
                        if (Host == PhotonNetwork.IsMasterClient) break;
                        doneAt = EditorApplication.timeSinceStartup; Log("handoff seen " + Where());
                    }
                    if (Running && EditorApplication.timeSinceStartup - doneAt > 4) DownOffTrain();
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 10) break;
                    Ack("back in staging master=" + PhotonNetwork.IsMasterClient);
                    if (Host && AllAck)
                    {
                        Verdict("C launch 2, master handed over, crew down", runSeen, "run:start:private", "run:launch:test:p2", "run:join:test:launch", "run:join:test:launch",
                            "run:end:fail:crew_down", "run:result:test:p2:fail", "run:result_end:test:p2:fail");
                        Next(10);
                    }
                    break;
                case 11: // D: the peer takes master, then leaves; the host inherits the run and goes down alone.
                    if (!Host)
                    {
                        if (PhotonSession.IsPrivate && Ready && !action && PhotonNetwork.IsMasterClient) { Log("LEAVE as master"); session.RequestCreatePublic(); action = true; readySince = 0; }
                        if (InCompound && Settled) Ack("left");
                        break;
                    }
                    if (Running && ActivePlayers == 2 && PhotonNetwork.IsMasterClient && age % 2 < .1) { PhotonNetwork.SetMasterClient(PhotonNetwork.PlayerListOthers[0]); Log("HANDOFF"); }
                    if (Running && ActivePlayers == 1 && PhotonNetwork.IsMasterClient) DownOffTrain();
                    if (!InStaging || !Settled || age < 10) break;
                    Ack("back in staging alone");
                    if (AllAck)
                    {
                        Verdict("D launch 2, master leaves, host ends alone", runSeen, "run:start:private", "run:launch:test:p2", "run:join:test:launch", "run:join:test:launch",
                            "run:end:fail:crew_down", "run:result:test:p2:fail", "run:result_end:test:p1:fail");
                        Next(12);
                    }
                    break;
                case 12:
                    done = true;
                    string result = (failures == 0 ? "PASS" : "FAIL " + failures + " case(s)") + " COMPLETE";
                    Log(Host ? result : "DONE");
                    if (Host) File.AppendAllText(Folder + "/verdicts.txt", result + "\n");
                    break;
            }
        }
        catch (IOException) { }
        catch (Exception e)
        {
            done = true; RailBridgeHoldFixture.SetHeld(false);
            Log("FAIL " + e);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
            if (Host) File.AppendAllText(Folder + "/verdicts.txt", "FAIL " + e.Message + " COMPLETE\n");
        }
    }
}
#endif
