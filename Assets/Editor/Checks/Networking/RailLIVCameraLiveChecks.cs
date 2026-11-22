#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in two-client playtest for the network-visible LIV camera tablet proxy (PhotonLIVCamera / RemoteLIVCamera).
/// Main editor + MPPM Player2 ("4 Diagnostic Two Client"); each client writes its live state (own tablet pose, sent mode
/// and sequence, the peer's proxy pose, received mode/sequence, event-120 receive count) to Temp/rails-liv-camera/<role>-live.json
/// at 10 Hz, and the main editor evaluates every check from both files. Scenarios:
///  - Compound, owner main then owner Player2: open (one active proxy at the owner's pose, no own proxy), move + settle
///    (follows, settles exactly, zero event-120 traffic and no publishes for 2 s while stationary), held in the right hand
///    (mode 3, follows the owner's remote hand when the owner's rig warps), close (proxy inactive), reopen at a new pose.
///  - Owner leaves the room with the tablet open: no orphan proxy on the stayer.
///  - Late join: Player2 opens the tablet alone in a private room; main joins by code and must show the proxy.
///  - Private expedition, both aboard, train moving: a tablet riding in the cabin is mode 4 and stays at the same
///    train-local pose on the observer (both directions); a tablet placed in the cabin and left (not carried) drops to
///    mode 1 once it leaves the padded box; a tablet placed off the stopped train is mode 1 and stays put as the train leaves.
/// Logs: Temp/rails-liv-camera/{main,player2}.txt ("CHECK ... PASS/FAIL"). Runtime code is not instrumented. No diagnostic reports.
/// Run: RailLIVCameraLiveChecks.Start() from the main editor in Edit Mode; Stop() ends the scenario.
/// </summary>
[InitializeOnLoad]
public static class RailLIVCameraLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, compound, privateCode;
        public int step, revision, sub;
    }
    [Serializable] sealed class Live
    {
        public string token, room, note, proxies;
        public int revision, stage, actor, peer, sentMode = -1, seq, recvMode = -1, recvSeq, ev120, proxyCount, activeProxyCount, avatars;
        public bool master, ready, open, held, aboard, hasTrain, proxyExists, proxyActive, ownProxy, parented;
        public float speed;
        public Vector3 tabletPos, tabletLocal, proxyPos, proxyLocal;
        public Quaternion tabletRot, tabletLocalRot, proxyRot, proxyLocalRot;
    }

    const string Key = "Rails.LIVCameraLive";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-liv-camera";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string LivePath(string role) => Folder + "/" + role + "-live.json";

    const float PosTolerance = .05f, AngleTolerance = 3f;
    // Steps. Each compound pass is six steps: open, move, held, close, reopen, close.
    const int Init = 0, GatherStep = 1, PassMain = 2, PassP2 = 8, LeaveOpen = 14, Leave = 15, LoneOpen = 16, LateJoin = 17,
        Depart = 18, TrainP2 = 19, TrainMain = 20, Placed = 21, OffTrain = 22, Done = 23;
    static readonly string[] PassNames = { "open", "move+settle", "held", "close", "reopen", "close (cleanup)" };

    static Control control;
    static string token, networkState, last = "", lastError = "";
    static int revision = -1, stage, seenSub;
    static bool action, action2, done;
    static double entered, nextTick, readySince, okSince = -1, stageAt, acted;
    static Live live = new Live();

    // Evaluation scratch (main), reset per step.
    static int samples, phase, baseEv, baseSeq, baseRecvSeq, modeSwitches;
    static float maxErr, sumErr, maxAng, pathLen, maxWorld;
    static double phaseAt;
    static Vector3 lastProxy, refPos, openPos, moveStart;
    static bool hasLastProxy;
    static string modesSeen = "";

    static RailLIVCameraLiveChecks()
    {
        EditorApplication.update += Tick;
        EditorApplication.update += Restore;
    }

    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";

    /// <summary>Main editor, Edit Mode: arm and launch the two-client scenario. firstStep skips earlier steps (needs the matching room state).</summary>
    public static void Start(int firstStep = 0)
    {
        if (!CurrentPlayer.IsMainEditor || EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Main editor in Edit Mode with no scenario running");
        if (!MppmStandaloneCredentials.TryGet("Player2", out _)) throw new Exception("Configure Player2 account");
        Directory.CreateDirectory(Folder);
        foreach (var role in Roles) { File.WriteAllText(LivePath(role), ""); File.WriteAllText(Folder + "/" + role + ".txt", ""); }
        var property = Manager.GetType().GetProperty("ActivePlayModeConfig");
        SessionState.SetString(Key + ".scenario", AssetDatabase.GetAssetPath((UnityEngine.Object)property.GetValue(Manager)));
        SessionState.SetBool(Key + ".quick", RailEditorQuickStart.Enabled);
        SessionState.SetBool(Key, true);
        RailEditorQuickStart.Enabled = false;
        var c = new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(60).ToString("O"),
            privateCode = UnityEngine.Random.Range(10000, 99999).ToString(), step = firstStep };
        File.WriteAllText(ControlPath, JsonUtility.ToJson(c));
        property.SetValue(Manager, AssetDatabase.LoadAssetAtPath<UnityEngine.Object>("Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        Manager.GetType().GetMethod("Start", Type.EmptyTypes).Invoke(Manager, null);
    }

    public static void Stop()
    {
        if (File.Exists(ControlPath)) File.WriteAllText(ControlPath, "");
        if (Status == "Running") Manager.GetType().GetMethod("Stop", Type.EmptyTypes).Invoke(Manager, null);
    }

    /// <summary>Summary of the main log's CHECK lines.</summary>
    public static string Results()
    {
        string path = Folder + "/main.txt";
        return File.Exists(path) ? string.Join("\n", File.ReadAllLines(path).Where(l => l.Contains(" CHECK ") || l.Contains("COMPLETE") || l.Contains("ABORT"))) : "";
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

    // ---------- Logging and coordination ----------

    static void Log(string message)
    {
        try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " " + message + "\n"); } catch (IOException) { }
    }
    static string Where() => "room=" + PhotonNetwork.CurrentRoom?.Name + " kind=" + PhotonSession.Kind + " phase=" + PhotonSession.Phase
        + " master=" + PhotonNetwork.IsMasterClient + " actor=" + PhotonNetwork.LocalPlayer?.ActorNumber + " step=" + control?.step;
    static void Next(int step)
    {
        control.step = step; control.sub = 0; control.revision++;
        File.WriteAllText(ControlPath, JsonUtility.ToJson(control));
    }
    static void SetSub(int sub) { control.sub = sub; File.WriteAllText(ControlPath, JsonUtility.ToJson(control)); }
    static Live Read(string role)
    {
        try
        {
            if (!File.Exists(LivePath(role))) return null;
            string json = File.ReadAllText(LivePath(role));
            if (string.IsNullOrEmpty(json)) return null;
            var l = JsonUtility.FromJson<Live>(json);
            return l != null && l.token == token ? l : null;
        }
        catch (IOException) { return null; }
        catch (ArgumentException) { return null; }
    }

    // ---------- Session helpers ----------

    static bool Ready => PhotonSession.Instance != null && PhotonSession.IsRailsRoom && PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest
        && GR.Player.instance != null && NewPlayer.instance != null;
    static int ActivePlayers => PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Count(p => !p.IsInactive) : 0;
    static bool AllPoses => PhotonNetwork.InRoom && PhotonNetwork.PlayerList.All(p => !p.IsInactive && PhotonPlayer.ForActor(p.ActorNumber) != null);
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
    static bool Running => PhotonSession.Phase == RailPhase.Running;
    static double Now => EditorApplication.timeSinceStartup;
    static void StandAway() => NewPlayer.instance.Warp(new Vector3(8 + Array.IndexOf(Roles, Role) * 2, .4f, 4), Quaternion.identity);
    static Photon.Realtime.Player Peer() => PhotonNetwork.InRoom ? PhotonNetwork.PlayerList.Where(p => !p.IsLocal).OrderBy(p => p.IsInactive).FirstOrDefault() : null;
    static void GrantFocus()
    {
        foreach (var handoff in UnityEngine.Object.FindObjectsByType<PhotonMasterHandoff>(FindObjectsSortMode.None))
            typeof(PhotonMasterHandoff).GetField("focused", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(handoff, true);
    }
    static bool Stable(Func<string> check, double seconds)
    {
        string failure = check();
        if (failure != null) { okSince = -1; last = failure; return false; }
        if (okSince < 0) okSince = Now;
        return Now - okSince >= seconds;
    }
    static void Nudge(Action request, double every = 4)
    {
        if (Now - acted < every) return;
        acted = Now;
        request();
    }

    // ---------- Photon event-120 receive counter (no runtime instrumentation) ----------

    static Photon.Realtime.LoadBalancingClient hooked;
    static readonly Dictionary<int, int> motionEvents = new Dictionary<int, int>();
    static void Hook()
    {
        var client = PhotonNetwork.NetworkingClient;
        if (client == null || client == hooked) return;
        if (hooked != null) hooked.EventReceived -= OnEvent;
        client.EventReceived += OnEvent;
        hooked = client;
    }
    static void OnEvent(EventData e)
    {
        if (e.Code != PhotonLIVCamera.MotionEvent) return;
        motionEvents.TryGetValue(e.Sender, out int n);
        motionEvents[e.Sender] = n + 1;
    }

    // ---------- Tablet ----------

    static readonly FieldInfo SentField = typeof(PhotonLIVCamera).GetField("sent", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo SequenceField = typeof(PhotonLIVCamera).GetField("sequence", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HasSentField = typeof(PhotonLIVCamera).GetField("hasSent", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo ReceivedField = typeof(PhotonLIVCamera).GetField("received", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo ReceivedSeqField = typeof(PhotonLIVCamera).GetField("receivedSequence", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo HasReceivedField = typeof(PhotonLIVCamera).GetField("hasReceived", BindingFlags.Instance | BindingFlags.NonPublic);

    static PauseMenu Menu => UnityEngine.Object.FindObjectsByType<PauseMenu>(FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(m => m != null && m.cameraTablet != null);
    static Transform TabletRoot => Menu?.cameraTablet?.transform;
    static bool TabletOpen => TabletGrab.Local != null && TabletGrab.Local.isActiveAndEnabled;
    static RigidbodyInterpolation? savedInterpolation;

    static void FrontPose(float side, float yaw, out Vector3 position, out Quaternion rotation)
    {
        var head = Camera.main.transform;
        var forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < .0001f) forward = Vector3.forward;
        forward.Normalize();
        var right = Vector3.Cross(Vector3.up, forward);
        position = head.position + forward * .8f + right * side - Vector3.up * .15f;
        rotation = Quaternion.LookRotation(-forward, Vector3.up) * Quaternion.Euler(0, yaw, 0);
    }
    static void Place(Vector3 position, Quaternion rotation)
    {
        var t = TabletRoot;
        t.SetPositionAndRotation(position, rotation);
        if (t.TryGetComponent(out Rigidbody body)) { body.position = position; body.rotation = rotation; }
    }
    static bool OpenAt(float side, float yaw)
    {
        var menu = Menu;
        if (menu == null || Camera.main == null) { last = "no pause menu / main camera"; return false; }
        if (TabletOpen) CloseTablet();
        menu.OpenCameraTablet();
        if (!TabletOpen) { last = "OpenCameraTablet did not open the tablet"; return false; }
        FrontPose(side, yaw, out var p, out var r);
        Place(p, r);
        Log("OPEN tablet at " + p.ToString("F2") + " yaw " + r.eulerAngles.y.ToString("F0") + " " + Where());
        return true;
    }
    static void ParentToTrain(Vector3 local, Quaternion localRotation)
    {
        var t = TabletRoot;
        var body = t.GetComponent<Rigidbody>();
        if (savedInterpolation == null) savedInterpolation = body.interpolation;
        body.interpolation = RigidbodyInterpolation.None;
        t.SetParent(RailTrain.Instance.transform, false);
        t.localPosition = local; t.localRotation = localRotation;
        body.position = t.position; body.rotation = t.rotation;
    }
    static void Unparent()
    {
        var t = TabletRoot;
        if (t == null) return;
        if (RailTrain.Instance != null && t.parent == RailTrain.Instance.transform) t.SetParent(null, true);
        if (savedInterpolation != null && t.TryGetComponent(out Rigidbody body)) body.interpolation = savedInterpolation.Value;
        savedInterpolation = null;
    }
    static void CloseTablet()
    {
        Unparent();
        if (Menu != null && TabletOpen) { Menu.CloseCameraTablet(); Log("CLOSE tablet " + Where()); }
    }

    // ---------- Live state ----------

    static void Capture()
    {
        var l = new Live { token = token, revision = revision, stage = stage, note = last };
        l.room = PhotonNetwork.CurrentRoom?.Name;
        l.actor = PhotonNetwork.LocalPlayer?.ActorNumber ?? 0;
        l.master = PhotonNetwork.IsMasterClient;
        l.ready = Ready && Settled;
        var train = RailTrain.Instance != null ? RailTrain.Instance.transform : null;
        l.hasTrain = train != null;
        l.speed = RailTrain.Instance != null ? RailTrain.Instance.Speed : 0;
        l.aboard = RailTrain.TryPassengerFrame(out _);
        l.open = TabletOpen;
        if (l.open)
        {
            var t = TabletGrab.Local.transform;
            l.tabletPos = t.position; l.tabletRot = t.rotation;
            l.held = TabletGrab.Local.GrabSource != null && TabletGrab.Local.GrabSource.Holder != null;
            l.parented = train != null && t.parent == train;
            if (train != null) { l.tabletLocal = train.InverseTransformPoint(t.position); l.tabletLocalRot = Quaternion.Inverse(train.rotation) * t.rotation; }
        }
        var cams = UnityEngine.Object.FindObjectsByType<PhotonLIVCamera>(FindObjectsSortMode.None);
        var mine = cams.FirstOrDefault(c => c != null && c.GetComponent<PhotonPlayer>() is PhotonPlayer pp && pp.IsLocalHuman);
        if (mine != null && (bool)HasSentField.GetValue(mine))
        {
            l.sentMode = ((PhotonLIVCamera.CameraState)SentField.GetValue(mine)).mode;
            l.seq = (int)SequenceField.GetValue(mine);
        }
        var peer = Peer();
        l.peer = peer?.ActorNumber ?? 0;
        if (peer != null)
        {
            var theirs = cams.Where(c => c != null && c.photonView.OwnerActorNr == peer.ActorNumber && !(c.GetComponent<PhotonPlayer>()?.IsLocalHuman ?? false)).ToList();
            l.avatars = theirs.Count;
            var cam = theirs.FirstOrDefault(c => (bool)HasReceivedField.GetValue(c)) ?? theirs.FirstOrDefault();
            if (cam != null && (bool)HasReceivedField.GetValue(cam))
            {
                l.recvMode = ((PhotonLIVCamera.CameraState)ReceivedField.GetValue(cam)).mode;
                l.recvSeq = (int)ReceivedSeqField.GetValue(cam);
            }
            motionEvents.TryGetValue(peer.ActorNumber, out l.ev120);
        }
        var proxies = UnityEngine.Object.FindObjectsByType<RemoteLIVCamera>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        l.proxyCount = proxies.Length;
        l.activeProxyCount = proxies.Count(p => p.gameObject.activeInHierarchy);
        l.proxies = string.Join(",", proxies.Select(p => p.name + (p.gameObject.activeInHierarchy ? "" : "[off]")));
        l.ownProxy = proxies.Any(p => p.name == "Remote LIV Camera (" + l.actor + ")");
        var proxy = peer != null ? proxies.FirstOrDefault(p => p.name == "Remote LIV Camera (" + peer.ActorNumber + ")") : null;
        if (proxy != null)
        {
            l.proxyExists = true; l.proxyActive = proxy.gameObject.activeInHierarchy;
            l.proxyPos = proxy.transform.position; l.proxyRot = proxy.transform.rotation;
            if (train != null) { l.proxyLocal = train.InverseTransformPoint(l.proxyPos); l.proxyLocalRot = Quaternion.Inverse(train.rotation) * l.proxyRot; }
        }
        live = l;
        try { File.WriteAllText(LivePath(Role), JsonUtility.ToJson(l)); } catch (IOException) { }
    }

    // ---------- Step machine ----------

    static void Tick()
    {
        if (!EditorApplication.isPlaying || Now < nextTick) return;
        nextTick = Now + .1;
        try
        {
            if (!File.Exists(ControlPath)) return;
            string json = File.ReadAllText(ControlPath);
            if (string.IsNullOrEmpty(json)) { control = null; return; }
            try { control = JsonUtility.FromJson<Control>(json); } catch (ArgumentException) { return; }
            if (control == null || !DateTime.TryParse(control.expires, out var expires) || DateTime.UtcNow > expires.ToUniversalTime()) { control = null; return; }
            if (token != control.token)
            {
                token = control.token; revision = -1; done = false;
                Log("START LIV camera two-client playtest; Unity " + Application.unityVersion + " role=" + Role + " privateCode=" + control.privateCode);
            }
            if (done) return;
            Hook();
            string state = PhotonNetwork.NetworkClientState + " " + Where() + " ready=" + PhotonSession.Instance?.WorldReady + " players=" + ActivePlayers;
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            if (revision != control.revision)
            {
                revision = control.revision; entered = Now; seenSub = 0;
                action = action2 = false; readySince = Ready ? readySince : 0; okSince = -1; last = ""; stage = 0; stageAt = Now; acted = 0;
                samples = phase = baseEv = baseSeq = baseRecvSeq = modeSwitches = 0; maxErr = sumErr = maxAng = pathLen = maxWorld = 0; phaseAt = 0; hasLastProxy = false; modesSeen = "";
                Log("step=" + control.step + " " + StepName(control.step) + " owner=" + OwnerOf(control.step));
            }
            GrantFocus();
            try { Act(Now - entered); }
            catch (Exception e) { if (e.Message != lastError) { lastError = e.Message; Log("ACT ERROR " + e); } }
            Capture();
            if (Host) Evaluate(Now - entered);
        }
        catch (IOException) { }
        catch (Exception e)
        {
            if (e.Message != lastError) { lastError = e.Message; Log("ERROR " + e); }
        }
    }

    static string OwnerOf(int step) =>
        step >= PassMain && step < PassP2 ? "main" : step >= PassP2 && step < Depart ? "player2"
        : step == TrainP2 || step == Placed ? "player2" : step == TrainMain || step == OffTrain ? "main" : "";
    static string StepName(int step) =>
        step >= PassMain && step < LeaveOpen ? "compound " + PassNames[(step - PassMain) % 6] :
        step == LeaveOpen ? "leave: owner opens" : step == Leave ? "owner leaves the room" : step == LoneOpen ? "late join: owner opens alone" :
        step == LateJoin ? "late join: newcomer" : step == Depart ? "private departure and boarding" : step == TrainP2 ? "train-local (owner player2)" :
        step == TrainMain ? "train-local (owner main)" : step == Placed ? "placed in the cabin and left on the moving train" :
        step == OffTrain ? "off-train world pose" : step == Done ? "done" : step == Init ? "init" : "gather";
    static bool IsOwner => OwnerOf(control.step) == Role;
    static void SetStage(int s) { stage = s; stageAt = Now; }

    static void Act(double age)
    {
        var session = PhotonSession.Instance;
        int step = control.step;
        // Only the step's owner keeps a tablet open.
        if (!IsOwner && TabletOpen && step != LeaveOpen) CloseTablet();
        if (step == Init)
        {
            if (Host && !action && Settled && age > 8) { session.RequestCreatePublic(); action = true; readySince = 0; Log("CREATE public Compound"); }
            if (TabletOpen) CloseTablet();
            return;
        }
        if (step == GatherStep)
        {
            if (!Host && Ready && PhotonNetwork.CurrentRoom?.Name != control.compound && InCompound && age > 2 && (!action || age % 15 < .1))
            { session.RequestJoinCode(control.compound); action = true; readySince = 0; Log("JOIN " + control.compound); }
            return;
        }
        if (step >= PassMain && step < LeaveOpen)
        {
            int s = (step - PassMain) % 6;
            if (s == 0 && stage == 0 && Ready) { StandAway(); SetStage(1); return; }
            if (!IsOwner || !Ready) return;
            var hand = GR.Player.instance.rightPlayerHand;
            switch (s)
            {
                case 0: if (stage == 1 && Now - stageAt > 1 && OpenAt(0, 0)) SetStage(2); break;
                case 1:
                    if (stage == 0) { moveStart = TabletRoot.position; SetStage(1); Log("MOVE start from " + moveStart.ToString("F2")); break; }
                    if (stage == 1)
                    {
                        float t = Mathf.Clamp01((float)(Now - stageAt) / 3f);
                        Vector3 end = moveStart + new Vector3(.6f, .1f, .2f);
                        Vector3 wiggle = new Vector3(0, Mathf.Sin(t * Mathf.PI * 2) * .15f, Mathf.Sin(t * Mathf.PI) * .25f);
                        FrontPose(0, 0, out _, out var r0);
                        Place(Vector3.Lerp(moveStart, end, t) + wiggle, r0 * Quaternion.Euler(Mathf.Sin(t * Mathf.PI) * 15f, 40f * t, 0));
                        if (t >= 1) { SetStage(2); Log("MOVE stopped at " + TabletRoot.position.ToString("F3")); }
                    }
                    break;
                case 2:
                    if (stage == 0)
                    {
                        var grab = TabletGrab.Local?.GrabSource;
                        if (grab == null) { last = "no tablet grabbable"; break; }
                        bool ok = hand.ForceGrab(grab);
                        Log("GRAB ForceGrab(right) returned " + ok + " holder=" + grab.Holder + " canGrab=" + hand.CanGrab + " handHeld=" + hand.Held);
                        if (ok && grab.Holder == hand) SetStage(1);
                        else if (Now - stageAt > 20) SetStage(9);
                        break;
                    }
                    if (stage == 1 && control.sub == 1)
                    {
                        var p = NewPlayer.instance.transform.position;
                        NewPlayer.instance.Warp(p + Vector3.forward * 1.5f, Quaternion.identity);
                        SetStage(2); Log("WARP rig 1.5 m forward while holding the tablet");
                    }
                    break;
                case 3: if (stage == 0) { if (hand.Held != null) hand.Release(); CloseTablet(); SetStage(1); } break;
                case 4: if (stage == 0 && OpenAt(.5f, 30)) SetStage(1); break;
                case 5: if (stage == 0) { CloseTablet(); SetStage(1); } break;
            }
            return;
        }
        if (step == LeaveOpen) { if (!Host && Ready && stage == 0 && OpenAt(0, 0)) SetStage(1); return; }
        if (step == Leave)
        {
            if (!Host && !action && Ready) { session.RequestCreatePrivate(control.privateCode); action = true; readySince = 0; Log("LEAVE: create private " + control.privateCode + " with the tablet open=" + TabletOpen); }
            return;
        }
        if (step == LoneOpen)
        {
            if (!Host && InStaging && Settled && stage == 0) { NewPlayer.instance.Warp(NewPlayer.instance.transform.position, Quaternion.identity); SetStage(1); }
            if (!Host && stage == 1 && Now - stageAt > 1 && OpenAt(0, 0)) SetStage(2);
            return;
        }
        if (step == LateJoin)
        {
            if (Host && Ready && !PhotonSession.IsPrivate && (!action || age % 15 < .1)) { session.RequestJoinCode(control.privateCode); action = true; readySince = 0; Log("LATE JOIN by code " + control.privateCode); }
            return;
        }
        if (step == Depart)
        {
            if (TabletOpen) CloseTablet();
            if (PhotonNetwork.IsMasterClient && InStaging && Settled && ActivePlayers == 2) Nudge(session.StartPrivateRun, 10);
            if (!Running || !Settled || RailTrain.Instance == null) { stage = 0; return; }
            if (stage == 0) { SetStage(1); return; }
            if (stage == 1 && Now - stageAt > 6)
            {
                if (PhotonNetwork.IsMasterClient) { RailTestTools.SetFuel(100); Log("FUEL 100 (train stopped) status=" + RailTestTools.Status); }
                SetStage(2); return;
            }
            if (stage == 2 && RailTrain.Instance.Speed < .05f && (!RailTrain.TryPassengerFrame(out _) && Now - acted > 3))
            {
                acted = Now;
                var train = RailTrain.Instance.transform;
                NewPlayer.instance.Warp(train.TransformPoint(new Vector3(Host ? .6f : -.6f, .35f, Host ? -2.5f : 1f)), train.rotation);
                Log("BOARD warp aboard speed=" + RailTrain.Instance.Speed.ToString("F2"));
            }
            return;
        }
        if (step == TrainP2 || step == TrainMain)
        {
            if (IsOwner && stage == 0 && Ready && RailTrain.Instance != null && OpenAt(0, 0))
            {
                ParentToTrain(new Vector3(Host ? .5f : -.5f, 1.4f, Host ? -1.2f : 2.2f), Quaternion.Euler(0, 180, 0));
                SetStage(1); Log("PARENT tablet to the train at local " + TabletRoot.localPosition.ToString("F2"));
            }
            TrainGo();
            return;
        }
        if (step == Placed)
        {
            if (IsOwner && stage == 0 && Ready && RailTrain.Instance != null && OpenAt(0, 0))
            {
                Place(RailTrain.Instance.transform.TransformPoint(new Vector3(-.5f, 1.4f, 2.2f)), RailTrain.Instance.transform.rotation * Quaternion.Euler(0, 180, 0));
                SetStage(1); Log("PLACE tablet in the cabin (not carried) speed=" + RailTrain.Instance.Speed.ToString("F2"));
            }
            TrainGo();
            return;
        }
        if (step == OffTrain)
        {
            if (control.sub == 0)
            {
                if (PhotonNetwork.IsMasterClient && RailTrain.Instance != null && RailTrain.Instance.Speed > .05f) Nudge(() => { session.SetTrainMoving(false); Log("STOP train"); }, 3);
                if (IsOwner && stage == 0 && Ready && RailTrain.Instance != null && RailTrain.Instance.Speed < .02f && Now - entered > 2 && OpenAt(0, 0))
                {
                    var train = RailTrain.Instance.transform;
                    Place(train.TransformPoint(new Vector3(7f, 1.2f, 0)), train.rotation);
                    SetStage(1); Log("PLACE tablet 7 m off the stopped train, padded=" + RailTrain.Instance.ContainsPadded(TabletRoot.position));
                }
            }
            else TrainGo();
            return;
        }
        if (step == Done)
        {
            if (!action) { CloseTablet(); action = true; done = true; Log("COMPLETE (role " + Role + ")"); }
        }
    }

    static void TrainGo()
    {
        if (!PhotonNetwork.IsMasterClient || RailTrain.Instance == null) return;
        if (RailTrain.Instance.Speed < .05f) Nudge(() => { if (PhotonSession.CurrentFuel < 60) RailTestTools.SetFuel(100); PhotonSession.Instance.SetTrainMoving(true); Log("GO train fuel=" + PhotonSession.CurrentFuel.ToString("F0")); }, 3);
    }

    // ---------- Evaluation (main editor) ----------

    static float Err(Live o, Live x) => Vector3.Distance(o.tabletPos, x.proxyPos);
    static float Ang(Live o, Live x) => Quaternion.Angle(o.tabletRot, x.proxyRot);
    static float LocalErr(Live o, Live x) => Vector3.Distance(o.tabletLocal, x.proxyLocal);
    static float LocalAng(Live o, Live x) => Quaternion.Angle(o.tabletLocalRot, x.proxyLocalRot);
    static string F(float v, string unit = "cm") => unit == "cm" ? (v * 100).ToString("F2") + " cm" : v.ToString("F2") + unit;

    static void Check(bool pass, string name, string detail)
    {
        Log("CHECK " + name + (pass ? " PASS " : " FAIL ") + detail);
        Next(control.step + 1);
    }

    static string ShowsProxy(Live o, Live x, bool world = true)
    {
        if (o == null || x == null) return "no live state";
        if (o.revision != revision) return "owner not on this step yet";
        if (!o.open) return "owner tablet not open";
        if (o.ownProxy) return "owner has a proxy of its own tablet";
        // The observer's own closed tablet may leave an inactive proxy on the owner; only active ones count.
        if (o.activeProxyCount != 0) return "owner has active proxies (" + o.proxies + ")";
        if (!x.proxyActive) return "observer proxy for #" + o.actor + " not active (" + x.proxies + ") recvMode=" + x.recvMode;
        if (x.activeProxyCount != 1) return "observer proxies: " + x.proxies;
        if (x.proxies.Split(',').Count(n => n == "Remote LIV Camera (" + o.actor + ")") != 1) return "observer proxy names " + x.proxies;
        float e = world ? Err(o, x) : LocalErr(o, x), a = world ? Ang(o, x) : LocalAng(o, x);
        if (e > PosTolerance || a > AngleTolerance) return "pose error " + F(e) + " / " + a.ToString("F2") + " deg";
        return null;
    }
    static string Pose(Live o, Live x) => "err " + F(Err(o, x)) + " / " + Ang(o, x).ToString("F3") + " deg, owner sentMode " + o.sentMode + " seq " + o.seq + ", observer recvMode " + x.recvMode + " seq " + x.recvSeq;

    static void Evaluate(double age)
    {
        var m = Read("main"); var p = Read("player2");
        int step = control.step;
        string ownerRole = OwnerOf(step);
        Live o = ownerRole == "main" ? m : p, x = ownerRole == "main" ? p : m;
        string name = StepName(step) + (ownerRole.Length > 0 ? " [owner " + ownerRole + "]" : "");
        double timeout = step <= GatherStep || step == Leave || step == LateJoin || step == Depart ? 240 : 90;
        if (age > timeout)
        {
            if (step <= GatherStep || step == Depart) { Log("ABORT " + name + " timed out: " + last); done = true; Next(Done); return; }
            Check(false, name, "timeout after " + age.ToString("F0") + " s; last: " + last + (o != null && x != null ? " | " + Pose(o, x) : ""));
            return;
        }
        if (m == null || p == null) { last = "waiting for live state"; return; }
        if (step == Init)
        {
            if (!action || !InCompound || !Settled || !p.ready) return;
            control.compound = PhotonNetwork.CurrentRoom.Name;
            Log("COMPOUND " + control.compound);
            Next(GatherStep);
            return;
        }
        if (step == GatherStep)
        {
            if (PhotonNetwork.CurrentRoom?.Name != control.compound || p.room != control.compound || ActivePlayers != 2 || !AllPoses || !p.ready || !Settled) return;
            Log("GATHERED in " + control.compound + " main #" + m.actor + " player2 #" + p.actor);
            Next(PassMain);
            return;
        }
        if (step >= PassMain && step < LeaveOpen)
        {
            int s = (step - PassMain) % 6;
            if (o.revision != revision) return;
            switch (s)
            {
                case 0:
                case 4:
                    if (o.stage < (s == 0 ? 2 : 1)) return;
                    if (!Stable(() => ShowsProxy(o, x), 2)) return;
                    float moved = Vector3.Distance(x.proxyPos, openPos);
                    if (s == 4 && moved < .3f) { last = "reopened proxy only " + F(moved) + " from the first pose"; return; }
                    Check(true, name, "one active '" + x.proxies + "' on observer, none on owner; " + Pose(o, x) + (s == 4 ? "; new pose " + F(moved) + " from the first open" : ""));
                    // Keep the first-open pose to compare the reopen against.
                    openPos = x.proxyPos;
                    return;
                case 1:
                    if (o.stage == 1)
                    {
                        if (!x.proxyActive) return;
                        float e = Err(o, x);
                        samples++; sumErr += e; maxErr = Mathf.Max(maxErr, e);
                        if (hasLastProxy) pathLen += Vector3.Distance(lastProxy, x.proxyPos);
                        lastProxy = x.proxyPos; hasLastProxy = true;
                        return;
                    }
                    if (o.stage != 2) return;
                    if (phase == 0) { phase = 1; phaseAt = Now; }
                    if (phase == 1)
                    {
                        if (Now - phaseAt < 1) return;
                        string f = ShowsProxy(o, x);
                        if (f != null) { last = f; if (Now - phaseAt > 10) Check(false, name, "not settled: " + f); return; }
                        phase = 2; phaseAt = Now; baseEv = x.ev120; baseSeq = o.seq; baseRecvSeq = x.recvSeq;
                        return;
                    }
                    if (Now - phaseAt < 2.2)
                    {
                        string f = ShowsProxy(o, x);
                        if (f != null) { Check(false, name, "settled pose drifted: " + f); }
                        return;
                    }
                    {
                        bool quiet = x.ev120 == baseEv && o.seq == baseSeq && x.recvSeq == baseRecvSeq;
                        bool follows = samples >= 5 && pathLen > .5f && maxErr < .6f;
                        bool exact = Err(o, x) <= PosTolerance && Ang(o, x) <= AngleTolerance && x.recvMode == 1;
                        Check(quiet && follows && exact, name, "while moving: " + samples + " samples, proxy path " + F(pathLen, " m") + ", lag error mean " + F(samples > 0 ? sumErr / samples : 0) + " max " + F(maxErr)
                            + "; settled " + Pose(o, x) + "; stationary 2.2 s: event-120 received " + (x.ev120 - baseEv) + " (total " + x.ev120 + "), owner publishes " + (o.seq - baseSeq) + ", observer accepted " + (x.recvSeq - baseRecvSeq));
                    }
                    return;
                case 2:
                    if (o.stage == 9) { Check(false, name, "ForceGrab failed: " + o.note); return; }
                    if (phase == 0)
                    {
                        if (o.stage < 1) return;
                        if (!Stable(() => !o.held ? "owner not holding" : o.sentMode != 3 ? "owner sentMode " + o.sentMode : x.recvMode != 3 ? "observer recvMode " + x.recvMode : ShowsProxy(o, x), 2)) return;
                        Log("  held static: " + Pose(o, x));
                        refPos = x.proxyPos; baseEv = x.ev120; phase = 1; okSince = -1; SetSub(1);
                        return;
                    }
                    if (o.stage < 2) return;
                    if (!Stable(() =>
                    {
                        float d = Vector3.Distance(x.proxyPos, refPos);
                        if (d < 1.2f) return "proxy moved only " + F(d) + " with the hand";
                        return !o.held ? "owner not holding" : x.recvMode != 3 ? "observer recvMode " + x.recvMode : ShowsProxy(o, x);
                    }, 2)) return;
                    Check(true, name, "mode 3 (right hand); after the owner's rig warped 1.5 m the proxy moved " + F(Vector3.Distance(x.proxyPos, refPos), " m") + " with the remote hand; " + Pose(o, x)
                        + "; event-120 during hand motion " + (x.ev120 - baseEv));
                    return;
                case 3:
                case 5:
                    if (o.stage < 1) return;
                    if (!Stable(() => o.open ? "owner still open" : o.sentMode != 0 ? "owner sentMode " + o.sentMode : x.activeProxyCount != 0 ? "observer active proxies: " + x.proxies
                        : x.recvMode != 0 ? "observer recvMode " + x.recvMode : null, 2)) return;
                    Check(true, name, "observer proxy " + (x.proxyExists ? "present but inactive" : "absent") + " (" + x.proxies + "), recvMode 0");
                    return;
            }
            return;
        }
        if (step == LeaveOpen)
        {
            if (o.stage < 1 || !Stable(() => ShowsProxy(o, x), 1)) return;
            Log("  owner #" + o.actor + " tablet open before leaving: " + Pose(o, x));
            Next(Leave);
            return;
        }
        if (step == Leave)
        {
            if (!Stable(() =>
            {
                if (ActivePlayers != 1) return "owner still in the room";
                if (m.proxyCount != 0) return "stayer still has proxies: " + m.proxies;
                if (p.room != control.privateCode || !p.ready) return "owner not settled in the private room yet";
                return null;
            }, 3)) return;
            Check(true, name, "stayer in " + m.room + " has 0 Remote LIV Camera objects after the owner left with the tablet open; owner now alone in " + p.room);
            return;
        }
        if (step == LoneOpen)
        {
            if (p.stage < 2 || !p.open || p.room != control.privateCode) return;
            if (phase == 0) { phase = 1; baseSeq = p.seq; phaseAt = Now; return; }
            if (p.seq != baseSeq) { baseSeq = p.seq; phaseAt = Now; return; }
            if (Now - phaseAt < 2) return;
            Log("  owner alone in " + p.room + ", tablet open sentMode " + p.sentMode + " seq " + p.seq);
            Next(LateJoin);
            return;
        }
        if (step == LateJoin)
        {
            if (PhotonNetwork.CurrentRoom?.Name != control.privateCode || ActivePlayers != 2 || !Settled) { okSince = -1; return; }
            if (phase == 0) { phase = 1; phaseAt = Now; Log("  joined " + control.privateCode); }
            if (!Stable(() => x.recvMode != 1 ? "newcomer recvMode " + x.recvMode : ShowsProxy(o, x), 2)) return;
            Check(true, name, "newcomer shows '" + x.proxies + "' " + (Now - phaseAt - 2).ToString("F1") + " s after settling; " + Pose(o, x) + "; event-120 received " + x.ev120);
            return;
        }
        if (step == Depart)
        {
            if (!Stable(() => !m.aboard || !p.aboard ? "aboard main=" + m.aboard + " player2=" + p.aboard : m.activeProxyCount + p.activeProxyCount != 0 ? "active proxies main=" + m.proxies + " player2=" + p.proxies : null, 2)) return;
            Log("  both aboard the stopped train; no active proxies (main [" + m.proxies + "], player2 [" + p.proxies + "]) in " + PhotonNetwork.CurrentRoom?.Name + " phase " + PhotonSession.Phase);
            Next(TrainP2);
            return;
        }
        if (step == TrainP2 || step == TrainMain)
        {
            if (o.stage < 1 || o.revision != revision) return;
            if (m.speed < 1 || p.speed < 1) { last = "train speed main " + m.speed.ToString("F2") + " player2 " + p.speed.ToString("F2"); return; }
            if (phase == 0) { phase = 1; phaseAt = Now; baseEv = x.ev120; refPos = x.proxyPos; return; }
            if (Now - phaseAt < 1) { baseEv = x.ev120; baseSeq = o.seq; refPos = x.proxyPos; return; }
            string f = !o.aboard ? "owner not a passenger" : o.sentMode != 4 ? "owner sentMode " + o.sentMode : x.recvMode != 4 ? "observer recvMode " + x.recvMode : ShowsProxy(o, x, world: false);
            if (f != null) { Check(false, name, "while moving at " + m.speed.ToString("F1") + " m/s: " + f + " | local err " + F(LocalErr(o, x)) + " | " + Pose(o, x)); return; }
            float e = LocalErr(o, x), a = LocalAng(o, x);
            samples++; sumErr += e; maxErr = Mathf.Max(maxErr, e); maxAng = Mathf.Max(maxAng, a); maxWorld = Mathf.Max(maxWorld, Err(o, x));
            if (Now - phaseAt < 9) return;
            Check(maxErr <= PosTolerance && maxAng <= AngleTolerance, name, "mode 4 on both; " + samples + " samples over 8 s at ~" + m.speed.ToString("F1") + " m/s (proxy travelled " + F(Vector3.Distance(refPos, x.proxyPos), " m")
                + "): train-local error mean " + F(sumErr / samples) + " max " + F(maxErr) + ", max angle " + maxAng.ToString("F3") + " deg; raw world gap max " + F(maxWorld)
                + " (train sync skew); event-120 received " + (x.ev120 - baseEv) + ", owner publishes " + (o.seq - baseSeq));
            return;
        }
        if (step == Placed)
        {
            if (o.stage < 1 || o.revision != revision) return;
            string modes = o.sentMode + "/" + x.recvMode;
            if (modes != modesSeen) { modesSeen = modes; modeSwitches++; Log("  modes owner/observer " + modes + " train-local x " + o.tabletLocal.ToString("F2") + " speed " + o.speed.ToString("F1") + " err " + F(o.sentMode == 4 ? LocalErr(o, x) : Err(o, x))); }
            if (o.sentMode == 4 && x.recvMode == 4 && x.proxyActive) { samples++; maxErr = Mathf.Max(maxErr, LocalErr(o, x)); }
            if (!Stable(() => o.sentMode != 1 ? "owner sentMode " + o.sentMode : x.recvMode != 1 ? "observer recvMode " + x.recvMode : ShowsProxy(o, x), 2)) return;
            Check(true, name, "tablet left behind by the moving train; switched to mode 1 after " + (Now - entered).ToString("F1") + " s; mode-4 samples " + samples + " (train-local err max " + F(maxErr)
                + " while trailing); world " + Pose(o, x));
            return;
        }
        if (step == OffTrain)
        {
            if (o.stage < 1 || o.revision != revision) return;
            if (phase == 0)
            {
                if (!Stable(() => o.aboard && o.sentMode == 1 && x.recvMode == 1 ? ShowsProxy(o, x) : "owner aboard " + o.aboard + " sentMode " + o.sentMode + " observer recvMode " + x.recvMode, 2)) return;
                Log("  stopped train, tablet off the train: " + Pose(o, x));
                phase = 1; refPos = x.proxyPos; okSince = -1; SetSub(1);
                return;
            }
            if (m.speed < 1 || p.speed < 1) return;
            if (phase == 1) { phase = 2; phaseAt = Now; baseEv = x.ev120; baseSeq = o.seq; }
            string f = o.sentMode != 1 ? "owner sentMode " + o.sentMode : x.recvMode != 1 ? "observer recvMode " + x.recvMode : ShowsProxy(o, x);
            if (f != null) { Check(false, name, "with the train moving: " + f); return; }
            samples++; maxErr = Mathf.Max(maxErr, Err(o, x));
            if (Now - phaseAt < 5) return;
            Check(true, name, "mode 1 while the owner rides away at " + m.speed.ToString("F1") + " m/s; " + samples + " samples, world error max " + F(maxErr) + ", proxy moved " + F(Vector3.Distance(refPos, x.proxyPos))
                + "; event-120 " + (x.ev120 - baseEv) + ", owner publishes " + (o.seq - baseSeq) + "; owner now " + F(Vector3.Distance(RailTrain.Instance.transform.position, o.tabletPos), " m") + " from the tablet");
            return;
        }
    }
}
#endif
