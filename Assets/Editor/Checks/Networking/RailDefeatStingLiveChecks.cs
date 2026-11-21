#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>
/// Opt-in two-client playtest for the defeat sting (playtest-queue.md, 2026-11-19).
/// Counts starts of RailAudioDirector's "Defeat sting" AudioSource (and "Bridge music") on each
/// client by polling isPlaying false->true every editor update, and samples the sting's volume.
/// Cases: public crew down (off-train), private crew down, private bridge crash, private success
/// preview, private forced early Returning (fade), and a fresh join by code during Ending.
/// Rooms, runs and finishes use real Photon and the existing test shortcuts.
/// Runtime game code is not instrumented. No diagnostic report access.
/// </summary>
[InitializeOnLoad]
public static class RailDefeatStingLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, compound, privateCode;
        public int step, revision;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, note, error, stingKinds, reasons, fades;
        public int revision, stings, music;
    }
    const string Key = "Rails.DefeatSting";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-defeat-sting";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";

    static Control control;
    static string token, networkState, lastRun, finishedRun;
    static int revision = -1;
    static bool action, action2, ack, done;
    static double entered, nextTick, readySince, arrivedAt, actionAt;

    // Sting / music tracking.
    static int stings, stingsAtStep, musics, musicsAtStep;
    static string stingKinds = "", reasons = "", fades = "", joinResult = "";
    static bool wasPlaying, musicWasPlaying, sourceSeen;
    static float lastTime;
    static double stingStartedAt, nextSample, fadeStartedAt;
    static float fadeStartVolume;
    static PhotonSession subscribed;

    static RailDefeatStingLiveChecks()
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
        var c = new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(60).ToString("O"),
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
        + " run=" + Short(PhotonSession.RunId) + " request=" + PhotonSession.Instance?.HasRoomRequest + " step=" + control?.step;
    static string Short(string s) => string.IsNullOrEmpty(s) ? "-" : s.Substring(0, Math.Min(6, s.Length));

    static AudioSource Source(string field)
    {
        const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic;
        var director = typeof(RailAudioDirector).GetField("instance", F)?.GetValue(null);
        return director == null ? null : typeof(RailAudioDirector).GetField(field, F)?.GetValue(director) as AudioSource;
    }
    static bool LocalAboard()
    {
        var train = RailTrain.Instance; var local = PhotonNetwork.LocalPlayer;
        return train != null && local != null && RailExpedition.Position(local.ActorNumber, out var head) && train.ContainsPadded(head);
    }
    static string RunState()
    {
        var e = RailExpedition.Instance;
        return e == null ? "noExpedition" : "finished=" + e.State.finished + " success=" + e.State.success + " reason=" + e.State.reason
            + " aboard=" + LocalAboard() + " localDown=" + RailExpedition.LocalDown + " invincible=" + RailAdminTools.Invincible;
    }

    /// <summary>Every editor update: count sting and music starts; sample the sting volume.</summary>
    static void Poll()
    {
        if (!EditorApplication.isPlaying || control == null || done) { wasPlaying = musicWasPlaying = false; return; }
        double now = EditorApplication.timeSinceStartup;
        var e = RailExpedition.Instance;
        if (PhotonSession.RunId != lastRun) { lastRun = PhotonSession.RunId; finishedRun = null; }
        if (e != null && e.State.finished && e.State.run == PhotonSession.RunId && finishedRun != PhotonSession.RunId)
        {
            finishedRun = PhotonSession.RunId;
            reasons += (reasons.Length > 0 ? "," : "") + e.State.reason + (e.State.success ? "(success)" : "(loss)");
            Log("FINISH observed reason=" + e.State.reason + " success=" + e.State.success + " " + Where() + " aboard=" + LocalAboard() + " localDown=" + RailExpedition.LocalDown);
        }
        var music = Source("music");
        bool musicPlaying = music != null && music.isPlaying;
        if (musicPlaying && !musicWasPlaying) { musics++; Log("MUSIC start #" + musics + " " + Where()); }
        else if (!musicPlaying && musicWasPlaying) Log("MUSIC end " + Where());
        musicWasPlaying = musicPlaying;

        var source = Source("defeat");
        if (source == null) { wasPlaying = false; return; }
        if (!sourceSeen)
        {
            sourceSeen = true;
            Log("STING source created; mixer=" + (source.outputAudioMixerGroup ? source.outputAudioMixerGroup.name : "none") + " clip=" + (source.clip ? source.clip.name + " len=" + source.clip.length.ToString("F2") : "none")
                + " spatialBlend=" + source.spatialBlend + " loop=" + source.loop);
        }
        bool playing = source.isPlaying;
        float time = source.time;
        if (playing && (!wasPlaying || time < lastTime - .1f))
        {
            stings++; stingStartedAt = now; nextSample = now + 2; fadeStartedAt = 0;
            string kind = PhotonSession.Kind + "/" + PhotonSession.Phase + "/aboard=" + LocalAboard() + "/down=" + RailExpedition.LocalDown;
            stingKinds += (stingKinds.Length > 0 ? "," : "") + kind;
            Log("STING start #" + stings + (wasPlaying ? " (restart while playing)" : "") + " t=" + time.ToString("F2") + " vol=" + source.volume.ToString("F2") + " " + Where() + " " + RunState());
        }
        else if (playing)
        {
            if (PhotonSession.Phase == RailPhase.Returning && fadeStartedAt == 0)
            {
                fadeStartedAt = now; fadeStartVolume = source.volume; nextSample = now;
                Log("STING Returning reached while playing t=" + time.ToString("F2") + " vol=" + source.volume.ToString("F2") + " " + Where());
            }
            if (now >= nextSample)
            {
                nextSample = now + (fadeStartedAt > 0 ? .25 : 2);
                Log("STING sample t=" + time.ToString("F2") + " vol=" + source.volume.ToString("F3") + " phase=" + PhotonSession.Phase);
            }
        }
        else if (wasPlaying)
        {
            string fade = fadeStartedAt > 0 ? (now - fadeStartedAt).ToString("F2") : "none";
            if (fadeStartedAt > 0) fades += (fades.Length > 0 ? "," : "") + fade + "s from vol " + fadeStartVolume.ToString("F2");
            Log("STING end after " + (now - stingStartedAt).ToString("F2") + "s lastT=" + lastTime.ToString("F2") + " vol=" + source.volume.ToString("F3") + " fadeSeconds=" + fade + " " + Where());
        }
        wasPlaying = playing; lastTime = time;
    }

    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static void Ack(string message)
    {
        if (ack) return;
        int delta = stings - stingsAtStep, music = musics - musicsAtStep;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, room = PhotonNetwork.CurrentRoom?.Name, note = message,
            stings = delta, stingKinds = stingKinds, music = music, reasons = reasons, fades = fades }));
        Log("ACK step=" + control.step + " stings=" + delta + " [" + stingKinds + "] music=" + music + " reasons=[" + reasons + "] fades=[" + fades + "] " + message);
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
    static bool InStaging => PhotonSession.IsPrivate && PhotonSession.Phase == RailPhase.Staging;
    static bool ArrivedAndQuiet()
    {
        if (PhotonSession.Phase != RailPhase.Running || !Settled) { arrivedAt = 0; return false; }
        if (arrivedAt == 0) { arrivedAt = EditorApplication.timeSinceStartup; Log("ARRIVED " + Where()); }
        return EditorApplication.timeSinceStartup - arrivedAt > 4;
    }
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void StandAway() => Teleport(new Vector3(8 + Array.IndexOf(Roles, Role), .4f, 4));
    static void StandInBoarding()
    {
        if (Ready && RailWorld.Instance != null && RailWorld.Instance.boardingTrigger != null)
            Teleport(RailWorld.Instance.boardingTrigger.bounds.center - Vector3.up * .8f);
    }
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(Array.IndexOf(Roles, Role) == 0 ? -.8f : .8f, .35f, -1)));
    /// <summary>Step off the train, then request a normal down (downs aboard are ignored without a boarded attacker).</summary>
    static void DownOffTrain()
    {
        if (!Ready || PhotonSession.Phase != RailPhase.Running || RailExpedition.Instance == null || RailTrain.Instance == null) return;
        if (!action) { Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(6 + Array.IndexOf(Roles, Role), .5f, 0))); action = true; actionAt = EditorApplication.timeSinceStartup; Log("WARP off train " + RunState()); return; }
        if (EditorApplication.timeSinceStartup - actionAt < 1) return;
        if (!RailExpedition.LocalDown) RailExpedition.RequestDown();
        else if (!action2) { action2 = true; Log("DOWN confirmed " + RunState()); }
    }
    /// <summary>Host only: compare per-role sting counts for a case and log the verdict.</summary>
    static bool Verdict(string name, int mainExpected, int player2Expected, string reason = null, int musicExpected = -1, bool expectFade = false)
    {
        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
        bool pass = m.stings == mainExpected && p.stings == player2Expected;
        if (reason != null) pass &= m.reasons.EndsWith(reason, StringComparison.Ordinal) && (player2Expected == 0 || p.reasons.EndsWith(reason, StringComparison.Ordinal));
        if (musicExpected >= 0) pass &= m.music == musicExpected && p.music == musicExpected;
        if (expectFade) pass &= m.fades.Length > 0 && p.fades.Length > 0;
        Log("CASE " + name + " " + (pass ? "PASS" : "FAIL") + " main=" + m.stings + " [" + m.stingKinds + "] music=" + m.music + " fades=[" + m.fades + "] reasons=[" + m.reasons + "] (expected " + mainExpected + ")"
            + " | player2=" + p.stings + " [" + p.stingKinds + "] music=" + p.music + " fades=[" + p.fades + "] reasons=[" + p.reasons + "] (expected " + player2Expected + ")"
            + (reason != null ? " expectedReason=" + reason : "") + (musicExpected >= 0 ? " expectedMusic=" + musicExpected : "") + " rooms main=" + m.room + " player2=" + p.room + " note2=" + p.note);
        return pass;
    }
    static void OnJoinFailed(string text) { joinResult = "refused: " + text; Log("JOIN refused: " + text + " " + Where()); }

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
                token = control.token; revision = -1; done = false; stings = musics = 0; sourceSeen = false;
                Log("START defeat sting playtest; Unity " + Application.unityVersion + " role=" + Role + " privateCode=" + control.privateCode);
            }
            if (done) return;
            var session = PhotonSession.Instance;
            if (session != null && subscribed != session) { if (subscribed != null) subscribed.RoomRequestFailed -= OnJoinFailed; session.RoomRequestFailed += OnJoinFailed; subscribed = session; }
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + session?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0)
                + " open=" + PhotonNetwork.CurrentRoom?.IsOpen + " master=" + PhotonNetwork.IsMasterClient;
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = action2 = ack = false; readySince = 0; arrivedAt = 0;
                // Step 17 continues the host's step-16 loss: keep its sting and reason in the same tally.
                if (control.step != 17) { stingsAtStep = stings; musicsAtStep = musics; stingKinds = reasons = fades = ""; }
                joinResult = "";
                Log("step=" + control.step);
            }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 240, "Timeout step=" + control.step + " " + Where() + " status=" + session?.Status);
            switch (control.step)
            {
                case 0: // Host opens a fresh public Compound.
                    if (!Host || !Settled) break;
                    if (!action) { session.RequestCreatePublic(); action = true; readySince = 0; break; }
                    if (!InCompound) break;
                    control.compound = PhotonNetwork.CurrentRoom.Name; Next(1); break;
                case 1: // Gather in it.
                    if (!Ready) break;
                    if (PhotonNetwork.CurrentRoom.Name != control.compound)
                    {
                        if (!Host && InCompound && age > 2 && (!action || age % 15 < .1)) { session.RequestJoinCode(control.compound); action = true; readySince = 0; }
                        break;
                    }
                    if (!Settled || ActivePlayers != 2 || !AllPoses) break;
                    StandAway();
                    Ack("gathered in " + control.compound);
                    if (Host && AllAck) Next(2);
                    break;
                case 2: // Both board a public departure.
                    if (InCompound) { StandInBoarding(); break; }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    Ack("departed together into " + PhotonNetwork.CurrentRoom.Name);
                    if (Host && AllAck) { Verdict("setup public departure (no sting)", 0, 0); Next(3); }
                    break;
                case 3: // CHECK 1 (public): both step off the train and go down; crew returns to a Compound.
                    if (!InCompound) { DownOffTrain(); break; }
                    if (!Settled) break;
                    StandAway();
                    Ack("returned to " + PhotonNetwork.CurrentRoom.Name);
                    if (Host && AllAck) { Verdict("1a public everyone down off-train", 1, 1, "Crew down(loss)"); Next(4); }
                    break;
                case 4: // Host creates the private code room.
                    if (!Host) { Ack("waiting"); break; }
                    if (!Ready) break;
                    if (!action) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    if (!InStaging || !Settled) break;
                    Ack("created private " + PhotonNetwork.CurrentRoom.Name);
                    if (AllAck) Next(5);
                    break;
                case 5: // Peer joins by code.
                    if (!Host && !PhotonSession.IsPrivate && Ready && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!InStaging || !Settled || ActivePlayers != 2 || !AllPoses) break;
                    Ack("in private staging " + PhotonNetwork.CurrentRoom.Name);
                    if (Host && AllAck) Next(6);
                    break;
                case 6: case 8: case 10: case 12: // Host launches a private run.
                    if (PhotonNetwork.IsMasterClient && InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH StartPrivateRun " + Where()); }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    Ack("private run " + PhotonSession.RunId);
                    if (Host && AllAck) { Verdict("launch before step " + (control.step + 1) + " (no sting)", 0, 0); Next(control.step + 1); }
                    break;
                case 7: // CHECK 1 (private): everyone down off-train; Ending; back to staging.
                    if (PhotonSession.Phase == RailPhase.Running) DownOffTrain();
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 10) break;
                    Ack("back in staging");
                    if (Host && AllAck) { Verdict("1b private everyone down off-train", 1, 1, "Crew down(loss)"); Next(8); }
                    break;
                case 9: // CHECK 2: bridge crash. Board, jump to the bridge approach with the bridge raised, drive into it.
                    if (PhotonSession.Phase == RailPhase.Running && Ready && RailRoute.Instance != null && RailTrain.Instance != null)
                    {
                        bool atApproach = RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40;
                        if (!action) { Board(); action = true; actionAt = EditorApplication.timeSinceStartup; }
                        if (atApproach && !action2) { Board(); action2 = true; Log("AT bridge approach distance=" + RailTrain.Instance.Distance.ToString("F1") + " finaleStart=" + RailRoute.Instance.FinaleStart.ToString("F1") + " bridgeZ=" + RailExpedition.Instance.bridgeZ + " " + RunState()); }
                        if (PhotonNetwork.IsMasterClient && EditorApplication.timeSinceStartup - actionAt > 2)
                        {
                            if (!atApproach && age % 2 < .1) { RailTestTools.Bridge(); Log("TEST Bridge(): " + RailTestTools.Status); }
                            else if (atApproach && action2 && RailTrain.Instance.Speed < .05f && age % 1.5 < .1) { session.SetTrainMoving(true); Log("TRAIN SetTrainMoving(true) fuel=" + PhotonSession.CurrentFuel.ToString("F1")); }
                        }
                    }
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 10) break;
                    Ack("back in staging");
                    if (Host && AllAck) { Verdict("2 bridge crash", 1, 1, "Bridge collision(loss)"); Next(10); }
                    break;
                case 11: // CHECK 3: successful escape preview (bridge music, no sting).
                    if (PhotonSession.Phase == RailPhase.Running && Ready && RailTrain.Instance != null)
                    {
                        if (!action) { Board(); action = true; actionAt = EditorApplication.timeSinceStartup; }
                        if (PhotonNetwork.IsMasterClient && EditorApplication.timeSinceStartup - actionAt > 3 && age % 2 < .1 && !RailExpedition.Instance.State.finished)
                        { RailTestTools.SimulateSuccess(); Log("TEST SimulateSuccess(): " + RailTestTools.Status); }
                    }
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 10) break;
                    Ack("back in staging");
                    if (Host && AllAck) { Verdict("3 successful escape (music, no sting)", 0, 0, "Test success preview(success)", 1); Next(12); }
                    break;
                case 13: // CHECK 4a: everyone down; host forces Returning ~4 s into the sting to observe the fade.
                    if (PhotonSession.Phase == RailPhase.Running) DownOffTrain();
                    if (PhotonNetwork.IsMasterClient && PhotonSession.Phase == RailPhase.Ending && finishedRun == PhotonSession.RunId && wasPlaying
                        && EditorApplication.timeSinceStartup - stingStartedAt > 4 && age % .5 < .1)
                    {
                        bool ok = session.Commit(new Hashtable { [RailSessionRules.Phase] = (int)RailPhase.Returning,
                            [RailSessionRules.Deadline] = PhotonNetwork.Time + RailSessionRules.ReturnDelaySeconds });
                        Log("FORCE Returning commit=" + ok + " " + Where());
                    }
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 10) break;
                    Ack("back in staging");
                    if (Host && AllAck) { Verdict("4a forced early Returning fades the sting", 1, 1, "Crew down(loss)", -1, true); Next(14); }
                    break;
                case 14: // CHECK 4b setup: peer leaves to a public Compound; host stays alone in staging.
                    if (!Host)
                    {
                        if (PhotonSession.IsPrivate && Ready && !action) { session.RequestCreatePublic(); action = true; readySince = 0; }
                        if (!InCompound || !Settled) break;
                        StandAway();
                        Ack("left to public " + PhotonNetwork.CurrentRoom.Name);
                    }
                    else if (InStaging && ActivePlayers == 1 && Settled) Ack("alone in staging");
                    if (Host && AllAck) Next(15);
                    break;
                case 15: // Host launches alone.
                    if (!Host) { if (InCompound && Ready) StandAway(); Ack("waiting in public compound"); break; }
                    if (InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH StartPrivateRun alone " + Where()); }
                    if (!ArrivedAndQuiet()) break;
                    Ack("solo private run " + PhotonSession.RunId);
                    if (AllAck) Next(16);
                    break;
                case 16: // Host goes down off-train; once Ending starts, release the peer's join attempt.
                    if (!Host) { Ack("waiting"); break; }
                    DownOffTrain();
                    if (PhotonSession.Phase == RailPhase.Ending && finishedRun == PhotonSession.RunId) { Ack("ending " + RunState()); if (AllAck) Next(17); }
                    break;
                case 17: // CHECK 4b: peer tries to join the private run by code during Ending.
                    if (!Host)
                    {
                        if (!action && Ready && !PhotonSession.IsPrivate) { Log("JOIN attempt by code " + control.privateCode + " " + Where()); session.RequestJoinCode(control.privateCode); action = true; actionAt = EditorApplication.timeSinceStartup; }
                        if (!action) break;
                        if (joinResult.Length > 0 && Ready) { Ack("join during Ending " + joinResult + "; stayed in " + PhotonNetwork.CurrentRoom?.Name + " lastError=" + session.LastError); break; }
                        if (PhotonSession.IsPrivate && Settled && EditorApplication.timeSinceStartup - actionAt > 20) { Ack("joined private room " + PhotonNetwork.CurrentRoom.Name + " in phase " + PhotonSession.Phase); break; }
                    }
                    else
                    {
                        if (!InStaging || !Settled || age < 5) break;
                        Ack("back in staging");
                    }
                    if (Host && AllAck)
                    {
                        Verdict("4b fresh join during Ending (host sting; peer refused => N/A)", 1, 0, "Crew down(loss)");
                        Next(18);
                    }
                    break;
                case 18:
                    done = true;
                    Log("COMPLETE total sting starts this client=" + stings + " music starts=" + musics);
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
