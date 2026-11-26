#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in two-client playtest for the expedition departure horn (playtest-queue.md).
/// Counts starts of RailAudioDirector's "Expedition departure horn" AudioSource on every client by
/// polling isPlaying false->true (and restarts where time jumps back) each editor update.
/// Cases: A1 public, one boards and one stays; A2 public, both board; B private launch and replay;
/// C join a running private run by code. Rooms, transfers and launches use real Photon.
/// Runtime game code is not instrumented. No diagnostic report access.
/// </summary>
[InitializeOnLoad]
public static class RailDepartureHornLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, compound, privateCode;
        public int step, revision;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, note, error, hornKinds;
        public int revision, horns;
    }
    const string Key = "Rails.DepartureHorn";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-departure-horn";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";

    static Control control;
    static string token, networkState;
    static int revision = -1;
    static bool action, ack, done;
    static double entered, nextTick, readySince, arrivedAt;

    // Horn tracking.
    static int horns, hornsAtStep;
    static string hornKindsAtStep = "";
    static bool wasPlaying, sourceSeen, countdownSeen;
    static float lastTime;
    static double lastPoll, hornStartedAt, maxGapNearHorn;
    static int countdownMax;

    static RailDepartureHornLiveChecks()
    {
        EditorApplication.update += Poll;
        EditorApplication.update += Tick;
        EditorApplication.update += Restore;
    }

    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    /// <summary>Main editor, Edit Mode: arm and launch the two-client scenario.</summary>
    public static void Start()
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles) { File.WriteAllText(ReceiptPath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); }
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetBool(Key, true);
        RailEditorQuickStart.Enabled = false;
        var c = new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(45).ToString("O"),
            privateCode = UnityEngine.Random.Range(10000, 99999).ToString() };
        File.WriteAllText(ControlPath, JsonUtility.ToJson(c));
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        Manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(Manager, null);
    }

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
        SessionState.SetBool(Key, false);
    }

    static void Log(string message)
    {
        try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n"); } catch (IOException) { }
    }
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " kind=" + PhotonSession.Kind + " phase=" + PhotonSession.Phase
        + " compoundScene=" + GameScenes.IsLoaded(GameScenes.Compound) + " expeditionScene=" + GameScenes.IsLoaded(GameScenes.Expedition)
        + " request=" + PhotonSession.Instance?.HasRoomRequest + " step=" + control?.step;

    static AudioSource HornSource()
    {
        const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        var director = typeof(RailAudioDirector).GetField("instance", F)?.GetValue(null);
        return director == null ? null : typeof(RailAudioDirector).GetField("departureHorn", F)?.GetValue(director) as AudioSource;
    }

    /// <summary>Every editor update: count horn starts (false->true, or a restart where time jumps back).</summary>
    static void Poll()
    {
        if (!EditorApplication.isPlaying || control == null || done) { wasPlaying = false; lastPoll = 0; return; }
        double now = EditorApplication.timeSinceStartup;
        double gap = lastPoll == 0 ? 0 : now - lastPoll;
        lastPoll = now;
        PollCountdown();
        var source = HornSource();
        if (source == null) { wasPlaying = false; return; }
        if (!sourceSeen)
        {
            sourceSeen = true;
            Log("HORN source created; mixer=" + (source.outputAudioMixerGroup ? source.outputAudioMixerGroup.name : "none") + " clip=" + (source.clip ? source.clip.name : "none")
                + " audioMasterMute=" + EditorUtility.audioMasterMute + " listenerVolume=" + AudioListener.volume);
        }
        bool playing = source.isPlaying;
        float time = source.time;
        if (wasPlaying && gap > maxGapNearHorn) maxGapNearHorn = gap;
        if (playing && (!wasPlaying || time < lastTime - .1f))
        {
            horns++; hornStartedAt = now; maxGapNearHorn = gap;
            string kind = PhotonSession.Kind + "/" + PhotonSession.Phase;
            hornKindsAtStep += (hornKindsAtStep.Length > 0 ? "," : "") + kind;
            Log("HORN start #" + horns + (wasPlaying ? " (restart while playing)" : "") + " t=" + time.ToString("F2") + " pollGap=" + gap.ToString("F2") + "s " + Where());
        }
        else if (!playing && wasPlaying)
            Log("HORN end after " + (now - hornStartedAt).ToString("F2") + "s maxPollGapWhilePlaying=" + maxGapNearHorn.ToString("F2") + "s " + Where());
        wasPlaying = playing; lastTime = time;
    }

    static void PollCountdown()
    {
        // Stayer evidence: the public boarding countdown as the director computes it.
        if (PhotonNetwork.InRoom && PhotonSession.Kind == RailSessionRules.CompoundKind && PhotonSession.Phase == RailPhase.Compound)
        {
            double deadline = PhotonSession.Get(RailSessionRules.Deadline, 0d);
            int seconds = deadline > 0 ? (int)Math.Ceiling(Math.Max(0, deadline - PhotonNetwork.Time)) : 0;
            if (seconds > 0 && !countdownSeen) { countdownSeen = true; countdownMax = seconds; Log("COUNTDOWN visible from " + seconds + "s " + Where()); }
            else if (seconds == 0 && countdownSeen) { countdownSeen = false; Log("COUNTDOWN cleared (started at " + countdownMax + "s) " + Where()); }
        }
    }

    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static void Ack(string message)
    {
        if (ack) return;
        int delta = horns - hornsAtStep;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, room = PhotonNetwork.CurrentRoom?.Name, note = message, horns = delta, hornKinds = hornKindsAtStep }));
        Log("ACK step=" + control.step + " horns=" + delta + " [" + hornKindsAtStep + "] " + message);
        ack = true;
    }
    static Receipt ReadReceipt(string role)
    {
        if (!File.Exists(ReceiptPath(role))) return null;
        string json = File.ReadAllText(ReceiptPath(role));
        if (string.IsNullOrEmpty(json)) return null;
        var receipt = JsonUtility.FromJson<Receipt>(json);
        return receipt != null && receipt.token == token ? receipt : null;
    }
    static bool AllAck => Roles.All(r => ReadReceipt(r)?.revision == revision);
    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest;
    static int ActivePlayers => PhotonNetwork.PlayerList.Count(p => !p.IsInactive);
    static bool AllPoses => PhotonNetwork.PlayerList.All(p => !p.IsInactive && PhotonPlayer.ForActor(p.ActorNumber) != null);
    static bool Settled
    {
        get
        {
            if (!Ready) { readySince = 0; return false; }
            if (readySince == 0) readySince = EditorApplication.timeSinceStartup;
            return EditorApplication.timeSinceStartup - readySince > 3;
        }
    }
    static bool InCompound => PhotonSession.Kind == RailSessionRules.CompoundKind;
    /// <summary>True once the expedition has been Running and settled for 6 s (the clip is 2 s).</summary>
    static bool ArrivedAndQuiet()
    {
        if (PhotonSession.Phase != RailPhase.Running || !Settled) { arrivedAt = 0; return false; }
        if (arrivedAt == 0) { arrivedAt = EditorApplication.timeSinceStartup; Log("ARRIVED " + Where()); }
        return EditorApplication.timeSinceStartup - arrivedAt > 6;
    }
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void StandAway() => Teleport(new Vector3(8 + Array.IndexOf(Roles, Role), .4f, 4));
    static void StandInBoarding()
    {
        if (Ready && RailWorld.Instance != null && RailWorld.Instance.boardingTrigger != null)
            Teleport(RailWorld.Instance.boardingTrigger.bounds.center - Vector3.up * .8f);
    }
    /// <summary>Step off the stopped train and down; the run ends and the crew returns to a compound.</summary>
    static void DownAndReturn()
    {
        if (!Ready || PhotonSession.Phase != RailPhase.Running || RailExpedition.Instance == null) return;
        if (!action) { Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(6 + Array.IndexOf(Roles, Role), .5f, 0))); action = true; return; }
        RailExpedition.RequestDown();
    }
    /// <summary>Host only: compare per-role horn counts for a case and log the verdict.</summary>
    static void Verdict(string name, int mainExpected, int player2Expected, string expectedKind = null)
    {
        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
        bool kindOk = expectedKind == null || new[] { m, p }.All(r => string.IsNullOrEmpty(r.hornKinds) || r.hornKinds.Split(',').All(k => k.StartsWith(expectedKind, StringComparison.Ordinal)));
        bool pass = m.horns == mainExpected && p.horns == player2Expected && kindOk;
        Log("CASE " + name + " " + (pass ? "PASS" : "FAIL") + " main=" + m.horns + " [" + m.hornKinds + "] (expected " + mainExpected + ") player2=" + p.horns + " [" + p.hornKinds + "] (expected " + player2Expected + ")"
            + (expectedKind != null ? " expectedStartState=" + expectedKind : "") + " rooms main=" + m.room + " player2=" + p.room);
    }
    /// <summary>Gather both clients in the host's current public Compound, standing off the platform.</summary>
    static bool Gather()
    {
        if (!Ready) return false;
        if (PhotonNetwork.CurrentRoom.Name != control.compound)
        {
            double age = EditorApplication.timeSinceStartup - entered;
            if (!Host && InCompound && age > 2 && (!action || age % 15 < .1)) { PhotonSession.Instance.RequestJoinCode(control.compound); action = true; readySince = 0; }
            return false;
        }
        if (!Settled || ActivePlayers != 2 || !AllPoses) return false;
        StandAway();
        return true;
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
            if (token != control.token)
            {
                token = control.token; revision = -1; done = false; horns = 0; sourceSeen = false;
                Log("START departure horn playtest; Unity " + Application.unityVersion + " role=" + Role + " privateCode=" + control.privateCode);
            }
            if (done) return;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + PhotonSession.Instance?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0);
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = ack = false; readySince = 0; arrivedAt = 0;
                hornsAtStep = horns; hornKindsAtStep = "";
                Log("step=" + control.step);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 240, "Timeout step=" + control.step + " " + Where() + " status=" + PhotonSession.Instance?.Status);
            var session = PhotonSession.Instance;
            switch (control.step)
            {
                case 0: // Host opens a fresh public Compound.
                    if (!Host || !Settled) break;
                    if (!action) { session.RequestCreatePublic(); action = true; readySince = 0; break; }
                    if (!InCompound) break;
                    control.compound = PhotonNetwork.CurrentRoom.Name; Next(1); break;
                case 1: // Gather.
                    if (!Gather()) break;
                    Ack("gathered in " + control.compound);
                    if (Host && AllAck) Next(2);
                    break;
                case 2: // A1: player2 boards and departs; main stays in the Compound.
                    if (Host)
                    {
                        if (InCompound && Ready) StandAway();
                        if (!InCompound || ActivePlayers != 1 || age < 30) break; // Peer has left; give it time to arrive.
                        if (ReadReceipt("player2")?.revision != revision) break;
                        Ack("stayed in " + PhotonNetwork.CurrentRoom.Name + " while player2 departed");
                        Verdict("A1 public one boards (player2) one stays (main)", 0, 1, RailSessionRules.CompoundKind + "/");
                        Next(3);
                    }
                    else
                    {
                        if (InCompound) { StandInBoarding(); break; }
                        if (!ArrivedAndQuiet()) break;
                        Ack("departed alone into " + PhotonNetwork.CurrentRoom.Name);
                    }
                    break;
                case 3: // Player2 downs and returns to a compound; no horn on the way back.
                    if (Host) { if (Ready) StandAway(); if (ReadReceipt("player2")?.revision == revision) { Ack("waited"); Verdict("A1 return (no horn on crew return)", 0, 0); control.compound = PhotonNetwork.CurrentRoom.Name; Next(4); } break; }
                    if (!InCompound) { DownAndReturn(); break; }
                    if (!Settled) break;
                    StandAway();
                    Ack("returned to " + PhotonNetwork.CurrentRoom.Name);
                    break;
                case 4: // Gather again.
                    if (!Gather()) break;
                    Ack("gathered in " + control.compound);
                    if (Host && AllAck) Next(5);
                    break;
                case 5: // A2: both board together.
                    if (InCompound) { StandInBoarding(); break; }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    Ack("departed together into " + PhotonNetwork.CurrentRoom.Name);
                    if (Host && AllAck)
                    {
                        Require(ReadReceipt("player2").room == ReadReceipt("main").room, "Crew split across expeditions");
                        Verdict("A2 public both board", 1, 1, RailSessionRules.CompoundKind + "/");
                        Next(6);
                    }
                    break;
                case 6: // Both down and return.
                    if (!InCompound) { DownAndReturn(); break; }
                    if (!Settled) break;
                    StandAway();
                    Ack("returned to " + PhotonNetwork.CurrentRoom.Name);
                    if (Host && AllAck) { Verdict("A2 return (no horn on crew return)", 0, 0); Next(7); }
                    break;
                case 7: // B: host creates the private code room.
                    if (!Host) { Ack("waiting"); break; }
                    if (!Ready) break;
                    if (!action) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    if (!PhotonSession.IsPrivate || !Settled || PhotonSession.Phase != RailPhase.Staging) break;
                    Ack("created private " + PhotonNetwork.CurrentRoom.Name);
                    if (AllAck) { Verdict("B create private (no horn)", 0, 0); Next(8); }
                    break;
                case 8: // Peer joins by code.
                    if (!Host && !PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Staging || !Settled || ActivePlayers != 2 || !AllPoses) break;
                    Ack("in private staging " + PhotonNetwork.CurrentRoom.Name);
                    if (Host && AllAck) { Verdict("B join by code in staging (no horn)", 0, 0); Next(9); }
                    break;
                case 9: case 11: // Host launches; each hears one horn leaving staging and none on arrival.
                    if (Host && PhotonSession.Phase == RailPhase.Staging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH StartPrivateRun " + Where() + " liveCargo=" + RailWorld.HasLiveCargo); }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    Ack("private run " + PhotonSession.RunId);
                    if (Host && AllAck) { Verdict(control.step == 9 ? "B private launch" : "B private replay launch", 1, 1, RailSessionRules.PrivateKind + "/Departing"); Next(control.step + 1); }
                    break;
                case 10: case 12: // Host ends the run; both return to staging with no horn.
                    // Retry: a busy running room commits several revisions per second, so one Commit can lose its CAS.
                    if (Host && PhotonSession.Phase == RailPhase.Running && Ready) { if (!action) Log("FINISH requested"); session.FinishRun(false); action = true; }
                    if (PhotonSession.Phase != RailPhase.Staging || !Settled || ActivePlayers != 2) break;
                    if (age < 8) break;
                    Ack("back in staging");
                    if (Host && AllAck) { Verdict(control.step == 10 ? "B return to staging" : "B replay return to staging", 0, 0); Next(control.step + 1); }
                    break;
                case 13: // C setup: peer leaves to a public Compound; host stays alone in staging.
                    if (!Host)
                    {
                        if (PhotonSession.IsPrivate && Ready && !action) { session.RequestCreatePublic(); action = true; readySince = 0; }
                        if (!InCompound || !Settled) break;
                        StandAway();
                        Ack("left to public " + PhotonNetwork.CurrentRoom.Name);
                    }
                    else if (PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging && ActivePlayers == 1 && Settled) Ack("alone in staging");
                    if (Host && AllAck) { Verdict("C setup (no horn)", 0, 0); Next(14); }
                    break;
                case 14: // C: host launches alone.
                    if (!Host) { if (InCompound && Ready) StandAway(); Ack("waiting in public compound"); break; }
                    if (PhotonSession.Phase == RailPhase.Staging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH StartPrivateRun alone " + Where() + " liveCargo=" + RailWorld.HasLiveCargo); }
                    if (!ArrivedAndQuiet()) break;
                    Ack("solo private run " + PhotonSession.RunId);
                    if (AllAck) { Verdict("C host solo launch", 1, 0, RailSessionRules.PrivateKind + "/Departing"); Next(15); }
                    break;
                case 15: // C: peer joins the running private run by code; no horn for anyone.
                    if (!Host && !PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!PhotonSession.IsPrivate || !ArrivedAndQuiet() || ActivePlayers != 2 || !AllPoses) break;
                    if (age < 15) break;
                    Ack("in running private run " + PhotonSession.RunId);
                    if (Host && AllAck) { Verdict("C join by code mid-run", 0, 0); Next(16); }
                    break;
                case 16:
                    done = true;
                    Log("COMPLETE total horn starts this client=" + horns);
                    break;
            }
        }
        catch (IOException) { }
        catch (Exception e)
        {
            done = true;
            Log("FAIL " + e.Message);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }
}
#endif
