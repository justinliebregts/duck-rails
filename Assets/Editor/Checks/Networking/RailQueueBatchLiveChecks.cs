#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in two-client batch playtest (playtest-queue.md, 2026-11-21 batch): admin train speed toggle, revive-complete SFX,
/// end-of-run results table (failure, bridge crash, success) and walkable collapsed bridge spans.
/// Admin is forced on both clients through reflection (editor hook); revive voices are counted by polling AudioManager's
/// private voice pool; the speed trace and results label are read from RailTrain/RailExpedition. Runtime code is not instrumented.
/// Logs: Temp/rails-queue-batch/{main,player2}.txt.
/// </summary>
[InitializeOnLoad]
public static class RailQueueBatchLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, privateCode;
        public int step, revision;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, note, error, data, table, stats, problems;
        public int revision, actor;
        public bool ok;
    }
    const string Key = "Rails.QueueBatch";
    const BindingFlags F = BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-queue-batch";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";

    static Control control;
    static string token, networkState, lastRun, capturedRun, tableText, statsText, problemsText, finalReason;
    static int revision = -1;
    static bool action, action2, ack, done, adminLogged, sawDown, localDownAtCapture, localAboardAtCapture, toggledViaButton;
    static double entered, nextTick, readySince, arrivedAt, actionAt, holdSince, capturedAt;
    static PhotonSession subscribed;

    // Speed trace.
    static bool haveSample;
    static double lastT;
    static readonly List<(double t, float s)> accelSamples = new List<(double t, float s)>();
    static float lastSpeed, lastDist, maxJump, maxAccel, maxSpeed, spdSinceHold;
    // Revive voices.
    static readonly Dictionary<int, ulong> lastGen = new Dictionary<int, ulong>();
    static int reviveVoices, otherEventsLogged;
    static string reviveVoiceInfo = "";
    // Test page keep-alive.
    static double testPageUntil;
    static readonly List<GameObject> testPageActivated = new List<GameObject>();
    // Bridge.
    static int span; static double spanAt; static float spanMinY, spanTop; static string spanReport = "";
    static bool spansDone; static float baseGap = -999;
    // Host memory.
    static int coalMain, coalPeer;
    static int masterActor, peerActor;

    static RailQueueBatchLiveChecks()
    {
        EditorApplication.update += Poll;
        EditorApplication.update += Tick;
        EditorApplication.update += Restore;
    }

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
    static string Short(string s) => string.IsNullOrEmpty(s) ? "-" : s.Substring(0, Math.Min(6, s.Length));
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " kind=" + PhotonSession.Kind + " phase=" + PhotonSession.Phase
        + " run=" + Short(PhotonSession.RunId) + " step=" + control?.step;

    // ---------- Reflection helpers ----------
    static T Field<T>(object o, string name) where T : class => o?.GetType().GetField(name, F)?.GetValue(o) as T;
    static GameObject PauseMenuTestPage(out Component menu)
    {
        menu = null;
        foreach (var c in Resources.FindObjectsOfTypeAll<MonoBehaviour>())
            if (c != null && c.GetType().Name == "PauseMenu" && c.gameObject.scene.IsValid()) { menu = c; break; }
        return menu == null ? null : menu.GetType().GetField("testPage", F)?.GetValue(menu) as GameObject;
    }
    /// <summary>Activates the pause menu and its Test page (kept active for a while so Update refreshes the labels).</summary>
    static void ShowTestPage(double seconds)
    {
        var page = PauseMenuTestPage(out var menu);
        if (page == null) return;
        testPageUntil = EditorApplication.timeSinceStartup + seconds;
        if (page.activeInHierarchy) return;
        var chain = new List<GameObject>();
        for (var t = page.transform; t != null; t = t.parent) { if (!t.gameObject.activeSelf) chain.Add(t.gameObject); }
        chain.Reverse();
        foreach (var go in chain) { go.SetActive(true); testPageActivated.Add(go); }
    }
    static void HideTestPage()
    {
        testPageUntil = 0;
        for (int i = testPageActivated.Count - 1; i >= 0; i--) if (testPageActivated[i] != null) testPageActivated[i].SetActive(false);
        testPageActivated.Clear();
    }
    static TMPro.TMP_Text TrainLabel()
    {
        var page = PauseMenuTestPage(out _);
        return page?.transform.Find("TrainSpeed")?.GetComponentInChildren<TMPro.TMP_Text>(true);
    }
    static string LabelText() => TrainLabel() is TMPro.TMP_Text t && t.gameObject.activeInHierarchy ? t.text : "(label unavailable)";
    static bool Press()
    {
        var page = PauseMenuTestPage(out _);
        var button = page?.transform.Find("TrainSpeed")?.GetComponent<PauseMenuButton>();
        if (button == null || !button.isActiveAndEnabled) return false;
        button.Press(); button.Release(); // the finger leaves: momentary buttons only re-arm on exit
        return true;
    }
    static void ForceAdmin()
    {
        var session = PlayerSession.Instance;
        if (session == null) return;
        if (!adminLogged) { adminLogged = true; Log("ADMIN account IsAdmin before forcing = " + session.IsAdmin); }
        if (!session.IsAdmin) typeof(PlayerSession).GetProperty("IsAdmin").GetSetMethod(true).Invoke(session, new object[] { true });
        RailAdminTools.Refresh();
    }

    static bool LocalAboard()
    {
        var train = RailTrain.Instance; var local = PhotonNetwork.LocalPlayer;
        return train != null && local != null && RailExpedition.Position(local.ActorNumber, out var head) && train.ContainsPadded(head);
    }
    static float Spd => RailTrain.Instance != null ? RailTrain.Instance.Speed : -1;
    static bool Fast => RailTestTools.FastTrainEnabled;
    static string LabelNow() => LabelText();
    static string TraceData() => "speed=" + Spd.ToString("F2") + ",fast=" + Fast + ",label=" + LabelNow() + ",maxJump=" + maxJump.ToString("F3") + ",maxAccel=" + maxAccel.ToString("F2")
        + ",maxSpeed=" + maxSpeed.ToString("F2") + ",fuel=" + PhotonSession.CurrentFuel.ToString("F1");

    // ---------- Per-update polling ----------
    static void Poll()
    {
        if (!EditorApplication.isPlaying || control == null || done) { haveSample = false; return; }
        double now = EditorApplication.timeSinceStartup;
        if (testPageUntil > 0 && now > testPageUntil) HideTestPage();
        if (PhotonSession.IsRailsRoom && PhotonNetwork.InRoom) ForceAdmin();
        foreach (var handoff in UnityEngine.Object.FindObjectsByType<PhotonMasterHandoff>(FindObjectsSortMode.None))
            typeof(PhotonMasterHandoff).GetField("focused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handoff, true);
        var e = RailExpedition.Instance;
        if (PhotonSession.RunId != lastRun) { lastRun = PhotonSession.RunId; capturedRun = null; tableText = statsText = problemsText = null; capturedAt = 0; }

        // Speed trace.
        var train = RailTrain.Instance;
        if (train != null && PhotonSession.Phase == RailPhase.Running)
        {
            double t = PhotonNetwork.Time; float s = train.Speed, d = train.Distance;
            if (haveSample && t > lastT && t - lastT < .25)
            {
                float dt = (float)(t - lastT);
                maxJump = Mathf.Max(maxJump, Mathf.Abs((d - lastDist) - (s + lastSpeed) * .5f * dt));
                accelSamples.Add((t, s)); while (accelSamples.Count > 1 && accelSamples[1].t < t - .4) accelSamples.RemoveAt(0);
                if (t - accelSamples[0].t >= .35) maxAccel = Mathf.Max(maxAccel, Mathf.Abs(s - accelSamples[0].s) / (float)(t - accelSamples[0].t));
            }
            maxSpeed = Mathf.Max(maxSpeed, s);
            lastT = t; lastSpeed = s; lastDist = d; haveSample = true;
        }
        else haveSample = false;

        // Revive-complete voices in AudioManager's pool.
        try
        {
            var manager = typeof(Game.Audio.AudioManager).GetField("_instance", F)?.GetValue(null);
            if (manager != null && manager.GetType().GetField("voices", F)?.GetValue(manager) is Array voices)
            {
                for (int i = 0; i < voices.Length; i++)
                {
                    var v = voices.GetValue(i); if (v == null) continue;
                    var vt = v.GetType();
                    var ev = vt.GetField("Event", F).GetValue(v) as UnityEngine.Object;
                    ulong gen = (ulong)vt.GetField("Generation", F).GetValue(v);
                    lastGen.TryGetValue(i, out var prev);
                    if (gen == prev) continue;
                    lastGen[i] = gen;
                    if (ev == null || ev.name != "ReviveSFX") continue;
                    var src = vt.GetField("Source", F).GetValue(v) as AudioSource;
                    reviveVoices++;
                    reviveVoiceInfo = "voice#" + i + " gen=" + gen + " clip=" + (src && src.clip ? src.clip.name : "none") + " mixer=" + (src && src.outputAudioMixerGroup ? src.outputAudioMixerGroup.name : "none")
                        + " spatial=" + (src ? src.spatialBlend : -1) + " vol=" + (src ? src.volume.ToString("F2") : "?") + " playing=" + (src && src.isPlaying);
                    Log("REVIVE SFX voice start #" + reviveVoices + " " + reviveVoiceInfo + " " + Where());
                }
            }
        }
        catch (Exception ex) { if (otherEventsLogged++ < 3) Log("voice poll error " + ex.Message); }

        // Results capture.
        if (e != null && e.State.finished && e.State.run == PhotonSession.RunId && e.results != null && e.results.gameObject.activeInHierarchy && e.results.text.Length > 0)
        {
            string text = e.results.text;
            int nl = text.IndexOf('\n');
            string table = nl >= 0 ? text.Substring(nl + 1) : text;
            if (capturedRun != PhotonSession.RunId || table != tableText)
            {
                capturedRun = PhotonSession.RunId; tableText = table; capturedAt = now;
                finalReason = e.State.reason;
                var ordered = e.State.players.OrderBy(p => p.actor).ToList();
                statsText = string.Join(";", ordered.Select(p => p.actor + ":" + p.name + ":" + p.coal + "," + p.gold + "," + p.revives + "," + p.downs + "," + (p.escaped ? 1 : 0)));
                problemsText = Analyze(table, e.State);
                localDownAtCapture = RailExpedition.LocalDown; localAboardAtCapture = LocalAboard();
                Log("RESULTS captured reason=" + e.State.reason + " success=" + e.State.success + " localDown=" + localDownAtCapture + " aboard=" + localAboardAtCapture + " overflow=" + e.results.isTextOverflowing
                    + " stats=" + statsText + " problems=[" + problemsText + "]\n" + table);
            }
        }
    }

    static string Analyze(string table, RailRunState st)
    {
        var probs = new List<string>();
        var rows = table.Split('\n').Where(l => l.StartsWith("<size=75%><pos=20%>")).ToList();
        if (!table.Contains(st.success ? "SUCCESS" : "FAIL") && !table.Contains("<align=left>")) probs.Add("no table");
        if (rows.Count != st.players.Count) probs.Add("rows " + rows.Count + " vs players " + st.players.Count);
        int[] cols = { 40, 46, 52, 59 };
        string[] names = { "coal", "gold", "revives", "downs" };
        Func<RailParticipant, int>[] get = { p => p.coal, p => p.gold, p => p.revives, p => p.downs };
        var max = get.Select(g => st.players.Count == 0 ? 0 : st.players.Max(g)).ToArray();
        for (int i = 0; i < Math.Min(rows.Count, st.players.Count); i++)
        {
            var p = st.players[i];
            var m = Regex.Match(rows[i], @"^<size=75%><pos=20%><color=(#[0-9A-Fa-f]{6})>(.*?)</color>");
            if (!m.Success) { probs.Add("row " + i + " unparsed"); continue; }
            string colour = p.escaped ? "#8AFFBA" : "#FF8866";
            if (!m.Groups[1].Value.Equals(colour, StringComparison.OrdinalIgnoreCase)) probs.Add(p.name + " colour " + m.Groups[1].Value + " expected " + colour);
            if (m.Groups[2].Value != RailResultsTable.Name(p.name)) probs.Add("name '" + m.Groups[2].Value + "' expected '" + RailResultsTable.Name(p.name) + "'");
            for (int c = 0; c < cols.Length; c++)
            {
                var vm = Regex.Match(rows[i], "<pos=" + cols[c] + @"%>(<color=#FFD84A>)?(\d+)");
                if (!vm.Success) { probs.Add(p.name + " " + names[c] + " missing"); continue; }
                int v = int.Parse(vm.Groups[2].Value), want = get[c](p);
                bool hi = vm.Groups[1].Success, wantHi = want > 0 && want == max[c];
                if (v != want) probs.Add(p.name + " " + names[c] + "=" + v + " state " + want);
                if (hi != wantHi) probs.Add(p.name + " " + names[c] + " highlight=" + hi + " expected " + wantHi);
            }
            if (!Regex.IsMatch(rows[i], "<pos=66%>[^<]+")) probs.Add(p.name + " no title");
        }
        return string.Join("; ", probs);
    }

    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step) { control.step = step; control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static void Ack(string message, bool ok = true, string data = "")
    {
        if (ack) return;
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, room = PhotonNetwork.CurrentRoom?.Name, note = message, ok = ok, data = data,
            actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0, table = tableText, stats = statsText, problems = problemsText }));
        Log("ACK step=" + control.step + " ok=" + ok + " " + message + " data=[" + data + "]");
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
    static string Val(string data, string key)
    {
        var m = Regex.Match(data ?? "", "(?:^|,)" + Regex.Escape(key) + "=([^,]*)");
        return m.Success ? m.Groups[1].Value : "";
    }
    static void Case(string name, bool pass, string details)
    {
        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
        Log("CASE " + name + " " + (pass ? "PASS" : "FAIL") + " :: " + details + " || main=[" + m?.data + "] ok=" + m?.ok + " || player2=[" + p?.data + "] ok=" + p?.ok);
    }
    static bool Both(Func<Receipt, bool> f) => f(ReadReceipt("main")) && f(ReadReceipt("player2"));

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
    static bool Hold(bool cond, double seconds)
    {
        double now = EditorApplication.timeSinceStartup;
        if (!cond) { holdSince = 0; return false; }
        if (holdSince == 0) holdSince = now;
        return now - holdSince >= seconds;
    }
    static void Teleport(Vector3 position) => NewPlayer.instance.Warp(position, Quaternion.identity);
    static void Board() => Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(Host ? -.8f : .8f, .35f, -1)));
    static Vector3 OffPoint => RailTrain.Instance.transform.TransformPoint(new Vector3(6f, .5f, 0));
    static int PeerActor => PhotonNetwork.PlayerList.First(p => p.ActorNumber != PhotonNetwork.LocalPlayer.ActorNumber).ActorNumber;

    /// <summary>Step off the train (distinct spot per role) and request a normal down.</summary>
    static void DownOffTrain(float x)
    {
        if (!Ready || PhotonSession.Phase != RailPhase.Running || RailExpedition.Instance == null || RailTrain.Instance == null) return;
        if (!action) { Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(x, .5f, 0))); action = true; actionAt = EditorApplication.timeSinceStartup; Log("WARP off train x=" + x); return; }
        if (EditorApplication.timeSinceStartup - actionAt < 1) return;
        if (!RailExpedition.LocalDown && (EditorApplication.timeSinceStartup - actionAt) % 3 < .15) RailExpedition.RequestDown();
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
                token = control.token; revision = -1; done = false; reviveVoices = 0; lastGen.Clear();
                Log("START queue batch; Unity " + Application.unityVersion + " role=" + Role + " privateCode=" + control.privateCode);
            }
            if (done) return;
            var session = PhotonSession.Instance;
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + session?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0) + " master=" + PhotonNetwork.IsMasterClient;
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                revision = control.revision; entered = EditorApplication.timeSinceStartup;
                action = action2 = ack = false; readySince = 0; arrivedAt = 0; holdSince = 0;
                maxJump = maxAccel = maxSpeed = 0; accelSamples.Clear(); spdSinceHold = 0; haveSample = false; sawDown = false; span = 0; spanAt = 0; spansDone = false; spanReport = "";
                reviveVoices = 0; reviveVoiceInfo = "";
                Log("step=" + control.step);
            }
            double now = EditorApplication.timeSinceStartup, age = now - entered;
            Require(age < 240, "Timeout step=" + control.step + " " + Where() + " status=" + session?.Status + " " + (RailTrain.Instance != null ? TraceData() : ""));
            bool master = PhotonNetwork.IsMasterClient;
            var expedition = RailExpedition.Instance;
            switch (control.step)
            {
                case 0: // Host creates the private room from its compound.
                    if (!Host) { Ack("waiting"); if (AllAck) { } break; }
                    if (!Ready || !InCompound && !InStaging) break;
                    if (!action && InCompound) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; break; }
                    if (!InStaging || !Settled) break;
                    Ack("created private " + PhotonNetwork.CurrentRoom.Name);
                    if (AllAck) Next(1);
                    break;
                case 1: // Peer joins by code.
                    if (!Host && !PhotonSession.IsPrivate && Ready && InCompound && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; }
                    if (!InStaging || !Settled || ActivePlayers != 2 || !AllPoses) break;
                    masterActor = PhotonNetwork.MasterClient.ActorNumber; peerActor = PhotonNetwork.PlayerList.First(pl => pl.ActorNumber != masterActor).ActorNumber;
                    Ack("in private staging " + PhotonNetwork.CurrentRoom.Name, Host == master, "master=" + master);
                    if (Host && AllAck) { Require(Both(r => r.ok), "master is not the main editor"); Next(2); }
                    break;
                case 2: case 15: case 18: // Host launches a private run; both board.
                    if (master && InStaging && Settled && (!action || age % 10 < .1)) { session.StartPrivateRun(); action = true; Log("LAUNCH StartPrivateRun " + Where()); }
                    if (!ArrivedAndQuiet() || ActivePlayers != 2) break;
                    if (!action2) { Board(); action2 = true; ShowTestPage(900); Log("BOARDED; test page shown"); holdSince = 0; actionAt = now; break; }
                    if (now - actionAt < 1.5) break;
                    {
                        string data = TraceData(); baseGap = GapNow(out var baseWhat); Log("TRAIN baseline gap=" + baseGap.ToString("F2") + " on " + baseWhat);
                        bool ok = !Fast && LabelNow().Contains("1x");
                        Ack("run " + PhotonSession.RunId + " starts at 1x: " + (ok ? "yes" : "NO"), ok, data);
                        if (Host && AllAck)
                        {
                            Case("next run starts at 1x (step " + control.step + ")", Both(r => r.ok), "fast must be false and label 1x on both clients");
                            Next(control.step == 2 ? 3 : control.step == 15 ? 16 : 19);
                        }
                    }
                    break;
                case 3: // Coal deposits so the results table has coal values (master 1, peer 2). Optional.
                {
                    if (expedition?.furnace == null || !Ready) break;
                    if (master && !action) { SpawnTestCoal(3); action = true; actionAt = now; }
                    if (!action2)
                    {
                        if (now - (master ? actionAt : entered) < 3) break;
                        Teleport(expedition.furnace.position + expedition.furnace.up * .2f + new Vector3(Host ? -.5f : .5f, 0, 0));
                        action2 = true; actionAt = now; break;
                    }
                    int mine = Host ? 1 : 2;
                    var me = expedition.State.players.FirstOrDefault(p => p.actor == PhotonNetwork.LocalPlayer.ActorNumber);
                    int have = me?.coal ?? 0;
                    if (have < mine && now - actionAt > 2 && (now - actionAt) % 2.5 < .15)
                    {
                        string wanted = "coal-test-" + (Host ? 0 : have + 1);
                        foreach (var coal in UnityEngine.Object.FindObjectsByType<RailCoal>(FindObjectsSortMode.None))
                        {
                            var v = coal.GetComponent<PhotonView>();
                            if (v != null && v.InstantiationData is object[] d && d.Length > 0 && d[d.Length - 1] as string == wanted) { expedition.Request("coal", v.ViewID); Log("REQUEST coal " + wanted + " view=" + v.ViewID + " dist=" + Vector3.Distance(coal.transform.position, expedition.furnace.position).ToString("F2")); break; }
                        }
                    }
                    bool got = have >= mine, giveUp = age > 45;
                    if (got || giveUp) Ack(got ? "coal credited" : "coal NOT credited within 45 s", true, "coal=" + have + ",exercised=" + got);
                    if (Host && AllAck)
                    {
                        coalMain = int.Parse(Val(ReadReceipt("main").data, "coal")); coalPeer = int.Parse(Val(ReadReceipt("player2").data, "coal"));
                        Case("coal stats exercised for the results table", Both(r => Val(r.data, "exercised") == "True"), "master coal=" + coalMain + " peer coal=" + coalPeer);
                        Next(4);
                    }
                    break;
                }
                case 4: // Fuel up, then GO at 1x.
                    if (master && !action && age > .5) { RailTestTools.SetFuel(100); action = true; actionAt = now; Log("TEST SetFuel(100): " + RailTestTools.Status); }
                    if (master && action && !action2 && now - actionAt > 2) { session.SetTrainMoving(true); action2 = true; Log("GO"); }
                    if (!Hold(Spd >= 3.9f && Spd <= 4.1f && !Fast && LabelNow().Contains("1x"), 1)) break;
                    Ack("moving at 1x", true, TraceData());
                    if (Host && AllAck) { Case("moving at 1x baseline", true, "label 1x, 4 m/s"); Next(5); }
                    break;
                case 5: // Master toggles while moving: 1x -> 5x.
                    if (Host && !action && age > 1) { RailAdminTools.ToggleTrainSpeed(); action = true; actionAt = now; Log("MASTER ToggleTrainSpeed (moving)"); }
                    if (!(Fast && Spd >= 19.5f && LabelNow().Contains("5x"))) break;
                    Ack("master toggle while moving -> 5x", true, TraceData());
                    if (Host && AllAck) { Case("master toggles while moving (1x->5x), label flips on both", Both(r => Val(r.data, "label").Contains("5x") && float.Parse(Val(r.data, "maxJump")) < .5f && float.Parse(Val(r.data, "maxAccel")) < 11f), "maxJump<0.5 m, accel<11 m/s2, speed reaches 20"); Next(6); }
                    break;
                case 6: // Non-master toggles while moving via the pause-menu button: 5x -> 1x.
                    if (!Host && !action && age > 1) { toggledViaButton = Press(); action = true; actionAt = now; Log("NON-MASTER pause-menu TrainSpeed Press() -> " + toggledViaButton); }
                    if (!Host && action && !action2 && now - actionAt > 3 && Fast) { RailAdminTools.ToggleTrainSpeed(); action2 = true; Log("fallback ToggleTrainSpeed (button press did not toggle)"); }
                    if (!(!Fast && Spd <= 4.1f && Spd > 3.5f && LabelNow().Contains("1x"))) break;
                    Ack("non-master toggle while moving -> 1x", true, TraceData() + ",viaButton=" + toggledViaButton + ",fallback=" + action2);
                    if (Host && AllAck) { Case("non-master admin toggles while moving (5x->1x) via pause-menu button", Both(r => Val(r.data, "label").Contains("1x") && float.Parse(Val(r.data, "maxJump")) < .5f && float.Parse(Val(r.data, "maxAccel")) < 11f) && ReadReceipt("player2").data.Contains("fallback=False"), "button Press() path used, ramp smooth"); Next(7); }
                    break;
                case 7: // Stop, then non-master toggles while stopped: 1x -> 5x, stays stopped.
                    if (master && !action && age > .5) { session.SetTrainMoving(false); action = true; Log("STOP"); }
                    if (!Host && !action && age > 5 && Spd < .05f) { toggledViaButton = Press(); if (!toggledViaButton) RailAdminTools.ToggleTrainSpeed(); action = true; Log("NON-MASTER toggle while stopped viaButton=" + toggledViaButton); }
                    if (Fast) spdSinceHold = Mathf.Max(spdSinceHold, Spd);
                    if (!Hold(Fast && Spd < .05f && LabelNow().Contains("5x"), 3)) break;
                    Ack("stopped train toggled to 5x and stayed stopped", spdSinceHold < .05f, TraceData() + ",spdWhileFast=" + spdSinceHold.ToString("F3"));
                    if (Host && AllAck) { Case("non-master toggles while stopped (1x->5x): label 5x, train stays stopped", Both(r => r.ok && Val(r.data, "label").Contains("5x")), "speed 0 for 3 s after the flip"); Next(8); }
                    break;
                case 8: // GO resumes at the selected 5x.
                    if (master && !action && age > .5) { session.SetTrainMoving(true); action = true; Log("GO"); }
                    if (!(Fast && Spd >= 19.5f)) break;
                    Ack("resumed at 5x after STOP/GO", true, TraceData());
                    if (Host && AllAck) { Case("resume after STOP/GO keeps 5x", Both(r => float.Parse(Val(r.data, "maxJump")) < .5f && float.Parse(Val(r.data, "maxAccel")) < 15f), "0 to 20 m/s ramp is 10 m/s2 nominal; limit 15 for sampling noise"); Next(9); }
                    break;
                case 9: // Stop, master toggles while stopped: 5x -> 1x.
                    if (master && !action && age > .5) { session.SetTrainMoving(false); action = true; Log("STOP"); }
                    if (Host && action && !action2 && age > 5 && Spd < .05f) { RailAdminTools.ToggleTrainSpeed(); action2 = true; Log("MASTER ToggleTrainSpeed (stopped)"); }
                    if (!Fast) spdSinceHold = Mathf.Max(spdSinceHold, Spd);
                    if (!Hold(!Fast && Spd < .05f && LabelNow().Contains("1x") && age > 6, 3)) break;
                    Ack("master toggled stopped train to 1x; stays stopped", spdSinceHold < .05f, TraceData());
                    if (Host && AllAck) { Case("master toggles while stopped (5x->1x): label 1x, stays stopped", Both(r => r.ok && Val(r.data, "label").Contains("1x")), "speed 0 for 3 s"); Next(10); }
                    break;
                case 10: // GO resumes at 1x.
                    if (master && !action && age > .5) { session.SetTrainMoving(true); action = true; Log("GO"); }
                    if (!(!Fast && Spd >= 3.9f && Spd <= 4.1f)) break;
                    Ack("resumed at 1x", true, TraceData());
                    if (Host && AllAck) { Case("resume after STOP/GO keeps 1x", Both(r => float.Parse(Val(r.data, "maxJump")) < .5f), "speed 4 m/s"); Next(11); }
                    break;
                case 11: // Leave the run at 5x so the next run must reset to 1x.
                    if (Host && !action && age > 1) { RailAdminTools.ToggleTrainSpeed(); action = true; Log("MASTER ToggleTrainSpeed (-> 5x, left on for next-run check)"); }
                    if (!(Fast && Spd >= 19.5f && LabelNow().Contains("5x"))) break;
                    Ack("left at 5x", true, TraceData());
                    if (Host && AllAck) { Case("5x set again (master, moving) before ending the run", true, "fuel logged"); Next(12); }
                    break;
                case 12: // Revive: stop, non-master downed off the train, master revives; count the SFX on each client.
                    HideTestPage();
                    if (master && !action && age > .5) { session.SetTrainMoving(false); action = true; Log("STOP for revive"); }
                    if (!Host)
                    {
                        if (!action2 && age > 5 && Spd < .05f && Ready) { Teleport(OffPoint); action2 = true; actionAt = now; Log("PEER warped off the train"); }
                        if (action2 && now - actionAt > 1 && !RailExpedition.LocalDown && (now - actionAt) % 3 < .15 && !sawDown) RailExpedition.RequestDown();
                    }
                    else if (RailExpedition.ActorDown(PeerActor) && (!action2 || now - actionAt > 12))
                    {
                        Teleport(RailTrain.Instance.transform.TransformPoint(new Vector3(6.5f, .5f, 0)));
                        action2 = true; actionAt = now; Log("MASTER beside the downed peer to revive");
                    }
                    if (RailExpedition.ActorDown(Host ? PeerActor : PhotonNetwork.LocalPlayer.ActorNumber)) { sawDown = true; holdSince = 0; break; }
                    if (!sawDown) break;
                    if (!Hold(true, 5)) break;
                    {
                        var rescuer = expedition.State.players.FirstOrDefault(p => p.actor == masterActor);
                        var revived = expedition.State.players.FirstOrDefault(p => p.actor == peerActor);
                        bool ok = reviveVoices == 1 && rescuer?.revives == 1 && revived?.revived == 1;
                        Ack("revive complete; SFX voices started=" + reviveVoices, ok, "sfx=" + reviveVoices + ",revives=" + rescuer?.revives + ",revived=" + revived?.revived + ",info=" + reviveVoiceInfo.Replace(',', ';'));
                    }
                    if (Host && AllAck) { Case("revive-complete SFX plays exactly once on each client", Both(r => r.ok), "AudioManager pooled voice with ReviveSFX counted per client"); sawDown = false; Next(13); }
                    break;
                case 13: // Loss: both go down off the train; capture the results table on each client.
                    if (PhotonSession.Phase == RailPhase.Running && age > 4) DownOffTrain(Host ? 7 : 8);
                    if (capturedRun != PhotonSession.RunId || !Hold(capturedAt > 0 && PhotonSession.Phase == RailPhase.Ending, 4)) break;
                    Ack("results captured (loss) reason=" + finalReason, string.IsNullOrEmpty(problemsText), "localDown=" + localDownAtCapture + ",aboard=" + localAboardAtCapture);
                    if (Host && AllAck) { VerdictResults("results table after a failed run (all down)", "Crew down", false, true); Next(14); }
                    break;
                case 14: case 17: case 20: // Back in staging.
                    if (!InStaging || !Settled || ActivePlayers != 2 || age < 6) break;
                    Ack("back in staging");
                    if (Host && AllAck) Next(control.step == 14 ? 15 : control.step == 17 ? 18 : 21);
                    break;
                case 16: // Bridge crash: approach, crash, walk the collapsed spans, capture results.
                    BridgeStep(session, master, now, age);
                    break;
                case 19: // Success preview; capture results.
                    if (PhotonSession.Phase == RailPhase.Running && Ready && !action && age > 1) { action = true; actionAt = now; }
                    if (master && action && now - actionAt > 3 && (now - actionAt) % 2 < .15 && !expedition.State.finished) { RailTestTools.SimulateSuccess(); Log("TEST SimulateSuccess(): " + RailTestTools.Status); }
                    if (capturedRun != PhotonSession.RunId || !Hold(capturedAt > 0 && PhotonSession.Phase == RailPhase.Ending, 4)) break;
                    Ack("results captured (success)", string.IsNullOrEmpty(problemsText), "localDown=" + localDownAtCapture + ",aboard=" + localAboardAtCapture);
                    if (Host && AllAck) { VerdictResults("results table after a successful run", "Test success preview", true, false); Next(20); }
                    break;
                case 21:
                    done = true; HideTestPage();
                    Log("COMPLETE");
                    break;
            }
        }
        catch (IOException) { }
        catch (Exception e)
        {
            done = true;
            Log("FAIL " + e.Message + "\n" + e.StackTrace);
            File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, error = e.Message }));
        }
    }

    static void SpawnTestCoal(int count)
    {
        var e = RailExpedition.Instance;
        for (int i = 0; i < count; i++)
            PhotonNetwork.InstantiateRoomObject(e.coalPrefab.name, e.furnace.position + e.furnace.up * .05f, Quaternion.identity, 0, new object[] { "rails", PhotonSession.RunId, "coal-test-" + i });
        Log("SPAWNED " + count + " test coal at the furnace " + e.furnace.position);
    }

    static void VerdictResults(string name, string reason, bool success, bool expectsStats)
    {
        var m = ReadReceipt("main"); var p = ReadReceipt("player2");
        // Expected stats per actor.
        string Expected(int actor)
        {
            if (!expectsStats) return "0,0,0," + (success ? 1 : 0);
            return "0," + (actor == masterActor ? 1 : 0) + "," + (actor == masterActor ? 1 : 2) + ",0";
        }
        string Compact(string stats) => string.Join(";", stats.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => { int first = s.IndexOf(':'), last = s.LastIndexOf(':'); var vals = s.Substring(last + 1).Split(','); return s.Substring(0, first) + "=" + string.Join(",", vals.Skip(1)); }));
        string expectedAll = string.Join(";", new[] { masterActor, peerActor }.OrderBy(a => a).Select(a => a + "=" + Expected(a)));
        bool same = m.table == p.table, stats = Compact(m.stats) == expectedAll && Compact(p.stats) == expectedAll, noProblems = string.IsNullOrEmpty(m.problems) && string.IsNullOrEmpty(p.problems);
        int CoalOf(string st, int actor) { foreach (var e in st.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)) { var q = e.Split(':'); if (q[0] == actor.ToString()) return int.Parse(q[q.Length - 1].Split(',')[0]); } return -1; }
        bool coalOk = !expectsStats || (CoalOf(m.stats, masterActor) >= coalMain && CoalOf(p.stats, peerActor) >= coalPeer && CoalOf(m.stats, masterActor) == CoalOf(p.stats, masterActor));
        Log("RESULTS-COMPARE " + name + " tablesIdentical=" + same + " statsExpected=" + expectedAll + " main=" + Compact(m.stats) + " player2=" + Compact(p.stats) + " problems main=[" + m.problems + "] player2=[" + p.problems + "]");
        Case(name, same && stats && coalOk && noProblems && m.ok && p.ok, "tables identical across clients, values == expected tallies, colours/highlights/names consistent");
    }

    static void BridgeStep(PhotonSession session, bool master, double now, double age)
    {
        var e = RailExpedition.Instance;
        if (PhotonSession.Phase == RailPhase.Running && Ready && RailRoute.Instance != null && RailTrain.Instance != null)
        {
            bool atApproach = RailTrain.Instance.Distance >= RailRoute.Instance.FinaleStart + 40;
            if (!action) { Board(); action = true; actionAt = now; }
            if (atApproach && !action2) { Board(); action2 = true; Log("AT bridge approach distance=" + RailTrain.Instance.Distance.ToString("F1")); }
            if (master && now - actionAt > 2)
            {
                if (!atApproach && age % 2 < .1) { RailTestTools.Bridge(); Log("TEST Bridge(): " + RailTestTools.Status); }
                else if (atApproach && action2 && RailTrain.Instance.Speed < .05f && age % 1.5 < .1) { session.SetTrainMoving(true); Log("TRAIN SetTrainMoving(true)"); }
            }
        }
        // After the collapse lands, walk-check each span on this client.
        var collapse = Field<RailBridgeCollapse>(e, "collapse");
        if (e != null && e.State.finished && e.State.reason == "Bridge collision" && collapse != null && PhotonNetwork.Time - e.State.finishedAt >= RailBridgeCollapse.Duration + .5 && !spansDone)
        {
            if (span >= collapse.sections.Length) { spansDone = true; Log("SPANS done " + spanReport); }
            else if (spanAt == 0 || now - spanAt > 2.6)
            {
                if (spanAt != 0) SampleSpan(collapse, false);
                if (span < collapse.sections.Length) EnterSpan(collapse);
                else { spansDone = true; Log("SPANS done " + spanReport); }
            }
            else if (spanAt != 0) spanMinY = Mathf.Min(spanMinY, NewPlayer.instance.transform.position.y);
        }
        if (!spansDone || capturedRun != PhotonSession.RunId || !Hold(capturedAt > 0 && PhotonSession.Phase == RailPhase.Ending, 4)) return;
        bool allOk = !spanReport.Contains("FAIL");
        Ack("bridge walk + results captured (crash)", allOk && string.IsNullOrEmpty(problemsText), "spans=" + spanReport.Replace(',', ';') + ",localDown=" + localDownAtCapture);
        if (Host && AllAck)
        {
            Case("collapsed bridge spans are solid/walkable on both clients", Both(r => r.ok && !Val(r.data, "spans").Contains("FAIL")), "per-span raycast top, ground contact after 2.6 s");
            VerdictResults("results table after a bridge crash", "Bridge collision", false, false);
            Next(17);
        }
    }

    static Collider Deck(RailBridgeCollapse c, int i)
    {
        Collider best = null; float area = 0;
        foreach (var col in c.sections[i].GetComponentsInChildren<Collider>(true))
        {
            if (col.gameObject.name != "Walk colliders") continue;
            float a = col.bounds.size.x * col.bounds.size.z;
            if (a > area) { area = a; best = col; }
        }
        return best;
    }
    static bool Below(Vector3 from, out RaycastHit best)
    {
        best = default; float nearest = float.MaxValue; bool found = false;
        foreach (var h in Physics.RaycastAll(from, Vector3.down, 6, ~0, QueryTriggerInteraction.Ignore))
        {
            if (NewPlayer.instance != null && h.collider.transform.IsChildOf(NewPlayer.instance.transform)) continue;
            if (h.distance < nearest) { nearest = h.distance; best = h; found = true; }
        }
        return found;
    }
    static float GapNow(out string what)
    {
        what = "none";
        var rig = NewPlayer.instance;
        if (!Below(rig.bodyCollider.bounds.center, out var h)) return float.NaN;
        what = h.collider.name + "(" + h.collider.transform.root.name + ")";
        return rig.transform.position.y - h.point.y;
    }
    static void EnterSpan(RailBridgeCollapse c)
    {
        var deck = Deck(c, span);
        int enabled = c.sections[span].GetComponentsInChildren<Collider>(true).Count(x => x.gameObject.name == "Walk colliders" && x.enabled);
        if (deck == null) { spanReport += "span" + span + ":FAIL(no deck);"; span++; spanAt = 0; return; }
        var b = deck.bounds;
        // Surface at the span centre: first enabled Walk collider hit from above (AABB tops are inflated by the tilt).
        Vector3 from = new Vector3(b.center.x, b.max.y + 3, b.center.z);
        RaycastHit top = default; bool hit = false;
        foreach (var h in Physics.RaycastAll(from, Vector3.down, 12, ~0, QueryTriggerInteraction.Ignore).OrderBy(x => x.distance))
        { if (NewPlayer.instance != null && h.collider.transform.IsChildOf(NewPlayer.instance.transform)) continue; top = h; hit = true; break; }
        bool walk = hit && top.collider.gameObject.name == "Walk colliders" && top.collider.enabled;
        spanTop = hit ? top.point.y : b.max.y;
        Log("SPAN " + span + " walkEnabled=" + enabled + " firstHitFromAbove=" + (hit ? top.collider.name + "(" + top.collider.transform.root.name + ") y=" + top.point.y.ToString("F2") : "none") + " aabbTop=" + b.max.y.ToString("F2"));
        spanReport += "span" + span + ":walkEnabled=" + enabled + " surfaceY=" + spanTop.ToString("F2") + ";";
        if (!walk || enabled != 3) spanReport += "FAIL(surface);";
        Teleport(new Vector3(b.center.x + (Host ? -.3f : .3f), spanTop + .3f, b.center.z));
        spanAt = EditorApplication.timeSinceStartup; spanMinY = float.MaxValue;
    }
    static void SampleSpan(RailBridgeCollapse c, bool _)
    {
        float gap = GapNow(out string what);
        float y = NewPlayer.instance.transform.position.y;
        bool onWalk = what.StartsWith("Walk colliders");
        bool nearBase = baseGap > -900 && Mathf.Abs(gap - baseGap) < .4f;
        bool ok = onWalk && nearBase && spanMinY > spanTop + baseGap - .6f; // rig origin sits |baseGap| below the contact on any floor (train baseline)
        Log("SPAN " + span + " settled rigY-surface=" + (y - spanTop).ToString("F2") + " minY-surface=" + (spanMinY - spanTop).ToString("F2") + " standingOn=" + what + " gap=" + gap.ToString("F2") + " trainBaselineGap=" + baseGap.ToString("F2") + " " + (ok ? "PASS" : "FAIL"));
        spanReport += "span" + span + ":on=" + what.Replace(',', ';') + " gap=" + gap.ToString("F2") + " base=" + baseGap.ToString("F2") + (ok ? ";" : " FAIL;");
        span++;
    }
}
#endif
