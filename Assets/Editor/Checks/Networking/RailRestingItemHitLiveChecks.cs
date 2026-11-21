#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.AI;
using Photon.Pun;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Opt-in two-client playtest for "Resting items no longer hit players riding the train" (playtest-queue.md, 2026-11-19).
/// Main (master) owns a gold bar; Player2 is the victim. Counts, without runtime instrumentation:
///  - LOCK: PhotonPlayer._swingLockUntil changes on the item owner (TickPlayerSwings registered a player hit),
///  - FLESH: ItemSounds.nextFleshImpactTime changes on the owner (PlayFleshImpact: sweep or OnCollisionEnter),
///  - EVT: RoomRpc PlaySound events (code 101) received from the other client (remote sound + blood burst),
///  - ACCEPT: the local PhotonPlayer._nextItemHitTime changes on the victim (ReceiveItemHit accepted),
///  - BLOOD: active pooled blood hit effects increasing.
/// Phases: resting bar under Player2 at cruise (standing, crouched), gate control (NotifyPlayerThrown while resting),
/// held walk-through, quick throw, and lobs with 1.2/1.7/2.2 s flight. Real Photon; no diagnostic report access.
/// </summary>
[InitializeOnLoad]
public static class RailRestingItemHitLiveChecks
{
    [Serializable] sealed class Control
    {
        public string token, expires, code, phase = "";
        public int step, revision, warpId;
        public Vector3 p2Pos; public float p2Yaw; public bool p2Crouch;
    }
    [Serializable] sealed class Receipt
    {
        public string token, room, note, error;
        public int revision, warpDone, locks, flesh, events, accepted, blood;
    }
    const string Key = "Rails.RestingItemHit";
    static readonly string[] Roles = { "main", "player2" };
    static string Root { get { var p = Application.dataPath.Replace('\\', '/'); var i = p.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase); return i < 0 ? Path.GetDirectoryName(p) : p.Substring(0, i); } }
    static string Folder => Root + "/Temp/rails-resting-items";
    static string ControlPath => Folder + "/control.json";
    static bool Host => CurrentPlayer.IsMainEditor;
    static string Role => Host ? "main" : "player2";
    static string ReceiptPath(string role) => Folder + "/" + role + ".json";
    const BindingFlags F = BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

    static Control control;
    static string token, networkState;
    static int revision = -1, warpDone;
    static bool action, done, eventsHooked;
    static double entered, nextTick, readySince, nextReceipt;
    static float stepTime, subTime; static int sub;

    // Counters (cumulative per client).
    static int locks, flesh, events, accepted, blood, lastBloodActive;
    static readonly Dictionary<UnityEngine.Object, float> lastValues = new Dictionary<UnityEngine.Object, float>();
    static ItemSounds[] soundsCache = new ItemSounds[0];
    static double nextSoundsScan;

    // Host state.
    static PhotonGrabbable bar;
    static int stepLocks, stepFlesh, p2Events0, p2Accepted0, p2Blood0, opportunities, contacts, frames;
    static float firstHitAt = -1, closest = float.MaxValue, closestAt, releaseAt;
    static Vector3 lastCenter; static bool hasCenter;

    static RailRestingItemHitLiveChecks()
    {
        EditorApplication.update += Poll;
        EditorApplication.update += Tick;
        EditorApplication.update += Restore;
        EditorApplication.playModeStateChanged += s => { if (s == PlayModeStateChange.ExitingPlayMode) Unhook(); };
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
        var c = new Control { token = Guid.NewGuid().ToString("N"), expires = DateTime.UtcNow.AddMinutes(40).ToString("O"), code = UnityEngine.Random.Range(10000, 99999).ToString() };
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
        try { File.AppendAllText(Folder + "/" + Role + ".txt", DateTime.UtcNow.ToString("O") + " t=" + Time.time.ToString("F2") + " " + message + "\n"); } catch (IOException) { }
    }
    static string V(Vector3 v) => "(" + v.x.ToString("F2") + "," + v.y.ToString("F2") + "," + v.z.ToString("F2") + ")";

    // ---------- Counters ----------
    static void Hook()
    {
        if (eventsHooked) return;
        PhotonNetwork.NetworkingClient.EventReceived += OnEvent;
        eventsHooked = true;
    }
    static void Unhook()
    {
        if (!eventsHooked) return;
        PhotonNetwork.NetworkingClient.EventReceived -= OnEvent;
        eventsHooked = false;
    }
    static void OnEvent(ExitGames.Client.Photon.EventData e)
    {
        if (e.Code != 101 || control == null || done) return;
        if (!(e.CustomData is object[] data) || data.Length < 2) return;
        string name = data[0] as string;
        bool impact = data.Length >= 3; // Sounds with impact particles (flesh/blood) carry a direction.
        if (impact) events++;
        if (impact || (name != null && name.IndexOf("flesh", StringComparison.OrdinalIgnoreCase) >= 0))
            Log("EVT sound=" + name + " from actor " + e.Sender + " at " + (data[1] is Vector3 p ? V(p) : "?") + " impactParticles=" + impact + " phase=" + control.phase);
    }
    static float GetFloat(object target, string field) => (float)target.GetType().GetField(field, F).GetValue(target);
    static void Poll()
    {
        if (!EditorApplication.isPlaying || control == null || done) return;
        Hook();
        foreach (var player in PhotonPlayer.Active)
        {
            if (player == null) continue;
            float lockUntil = GetFloat(player, "_swingLockUntil");
            if (lastValues.TryGetValue(player, out float old) && lockUntil != old && lockUntil > 0)
            {
                locks++;
                Log("LOCK victim actor=" + player.photonView.OwnerActorNr + " (this client's item hit that player) phase=" + control.phase);
                if (Host && firstHitAt < 0) firstHitAt = Time.time;
            }
            lastValues[player] = lockUntil;
            if (player.IsLocalHuman)
            {
                var key = player.photonView;
                float next = GetFloat(player, "_nextItemHitTime");
                if (lastValues.TryGetValue(key, out float oldNext) && next != oldNext && next > 0)
                { accepted++; Log("ACCEPT local player accepted an item hit phase=" + control.phase); }
                lastValues[key] = next;
            }
        }
        if (EditorApplication.timeSinceStartup > nextSoundsScan)
        {
            nextSoundsScan = EditorApplication.timeSinceStartup + 1;
            soundsCache = UnityEngine.Object.FindObjectsByType<ItemSounds>(FindObjectsSortMode.None);
        }
        foreach (var sounds in soundsCache)
        {
            if (sounds == null) continue;
            float next = GetFloat(sounds, "nextFleshImpactTime");
            if (lastValues.TryGetValue(sounds, out float old) && next != old && next > 0)
            {
                flesh++;
                var held = sounds.GetComponent<Grabbable>();
                Log("FLESH PlayFleshImpact item=" + sounds.name + " pos=" + V(sounds.transform.position) + " held=" + (held != null && held.IsHeld) + " phase=" + control.phase);
            }
            lastValues[sounds] = next;
        }
        int active = BloodHitsActive();
        if (active > lastBloodActive) { blood += active - lastBloodActive; Log("BLOOD active hit effects " + lastBloodActive + " -> " + active + " phase=" + control.phase); }
        lastBloodActive = active;
        if (Host) HostSample();
    }
    static int BloodHitsActive()
    {
        var instance = typeof(BloodEffectPool).GetField("instance", F)?.GetValue(null);
        if (instance == null) return 0;
        var slots = typeof(BloodEffectPool).GetField("hits", F).GetValue(instance) as Array;
        int n = 0;
        foreach (var slot in slots)
            if (slot != null && (bool)slot.GetType().GetField("active", F).GetValue(slot)) n++;
        return n;
    }

    // ---------- Host geometry sampling ----------
    static PhotonPlayer Victim => PhotonNetwork.PlayerList.Where(p => !p.IsLocal).Select(p => PhotonPlayer.ForActor(p.ActorNumber)).FirstOrDefault(p => p != null);
    static Collider BarCollider => bar == null ? null : bar.GetComponentsInChildren<Collider>().FirstOrDefault(c => c.enabled && !c.isTrigger);
    static float BarRadius => bar == null ? 0 : bar.LocomotionRadius > 0 ? bar.LocomotionRadius : .05f;
    static void HostSample()
    {
        if (bar == null) return;
        var victim = Victim; var col = BarCollider;
        if (victim == null || col == null) { hasCenter = false; return; }
        Vector3 center = col.bounds.center;
        frames++;
        if (victim.TryGetHeadPosition(out Vector3 head))
        {
            float d = Vector3.Distance(center, head);
            if (d < closest) { closest = d; closestAt = Time.time; }
        }
        if (hasCenter && Time.deltaTime > 0)
        {
            // Same overlap test as TickPlayerSwings with three points along the bar's long axis.
            Vector3 ext = col.bounds.extents;
            Vector3 axis = ext.x >= ext.y && ext.x >= ext.z ? Vector3.right * ext.x : ext.z >= ext.y ? Vector3.forward * ext.z : Vector3.up * ext.y;
            Vector3 delta = center - lastCenter;
            float speed = delta.magnitude / Time.deltaTime;
            bool overlap = false;
            for (int i = -1; i <= 1 && !overlap; i++)
                overlap = victim.OverlapsSwing(lastCenter + axis * (.8f * i), center + axis * (.8f * i), BarRadius);
            if (overlap && speed >= victim.MinHitSpeed && delta.magnitude < 3) opportunities++;
        }
        lastCenter = center; hasCenter = true;
        // Physical contact between the bar and any collider of the remote avatar (ItemSounds.OnCollisionEnter source).
        foreach (var other in victim.GetComponentsInChildren<Collider>())
        {
            if (!other.enabled || other.isTrigger) continue;
            if (Physics.ComputePenetration(col, col.transform.position, col.transform.rotation, other, other.transform.position, other.transform.rotation, out _, out _))
            { contacts++; break; }
        }
    }
    static void ResetStep()
    {
        stepLocks = locks; stepFlesh = flesh; opportunities = contacts = frames = 0; firstHitAt = -1; closest = float.MaxValue; hasCenter = false;
        var p = ReadReceipt("player2");
        p2Events0 = p?.events ?? 0; p2Accepted0 = p?.accepted ?? 0; p2Blood0 = p?.blood ?? 0;
    }
    static string StepCounts()
    {
        var p = ReadReceipt("player2");
        return "mainLocks(hit registered)=" + (locks - stepLocks) + " mainFlesh(PlayFleshImpact)=" + (flesh - stepFlesh)
            + " p2FleshEvents=" + ((p?.events ?? 0) - p2Events0) + " p2Accepted=" + ((p?.accepted ?? 0) - p2Accepted0) + " p2Blood=" + ((p?.blood ?? 0) - p2Blood0)
            + " overlapOpportunities(frames where TickPlayerSwings would overlap at >=minHitSpeed)=" + opportunities + "/" + frames
            + " avatarContactFrames=" + contacts;
    }

    // ---------- Coordination ----------
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Next(int step, string phase = null)
    {
        control.step = step; control.revision++; if (phase != null) control.phase = phase;
        File.WriteAllText(ControlPath, JsonUtility.ToJson(control));
    }
    static void P2Warp(Vector3 pos, float yaw, bool crouch)
    {
        control.p2Pos = pos; control.p2Yaw = yaw; control.p2Crouch = crouch; control.warpId++;
        control.revision++; File.WriteAllText(ControlPath, JsonUtility.ToJson(control));
    }
    static void WriteReceipt(string note = null, string error = null)
    {
        File.WriteAllText(ReceiptPath(Role), JsonUtility.ToJson(new Receipt { token = token, revision = revision, room = PhotonNetwork.CurrentRoom?.Name, note = note, error = error,
            warpDone = warpDone, locks = locks, flesh = flesh, events = events, accepted = accepted, blood = blood }));
    }
    static Receipt ReadReceipt(string role)
    {
        if (!File.Exists(ReceiptPath(role))) return null;
        string json; try { json = File.ReadAllText(ReceiptPath(role)); } catch (IOException) { return null; }
        if (string.IsNullOrEmpty(json)) return null;
        var receipt = JsonUtility.FromJson<Receipt>(json);
        return receipt != null && receipt.token == token ? receipt : null;
    }
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
    static Transform Train => RailTrain.Instance.transform;
    static Vector3 TrainPoint(float x, float y, float z) => Train.TransformPoint(new Vector3(x, y, z));
    static void Inject(SimulatedRigInput.Frame frame)
    {
        var rig = SimulatedRigInput.Instance;
        if (rig == null) return;
        rig.UseInjectedInput = true; rig.InjectedInput = frame;
    }
    static PlayerHand Hand => GR.Player.instance != null ? GR.Player.instance.rightPlayerHand : null;
    static float StepAge => Time.time - stepTime;
    static void NextSub() { sub++; subTime = Time.time; }
    static float SubAge => Time.time - subTime;

    static void Tick()
    {
        if (!EditorApplication.isPlaying || EditorApplication.timeSinceStartup < nextTick) return;
        nextTick = EditorApplication.timeSinceStartup + .05;
        try
        {
            if (!File.Exists(ControlPath)) return;
            string json = File.ReadAllText(ControlPath);
            if (string.IsNullOrEmpty(json)) { control = null; return; }
            try { control = JsonUtility.FromJson<Control>(json); } catch (ArgumentException) { return; }
            if (control == null || !DateTime.TryParse(control.expires, out var expires) || DateTime.UtcNow > expires.ToUniversalTime()) { control = null; return; }
            if (token != control.token)
            {
                token = control.token; revision = -1; done = false; warpDone = 0;
                Log("START resting-item hit playtest; Unity " + Application.unityVersion + " role=" + Role + " code=" + control.code);
            }
            if (done) return;
            string state = PhotonNetwork.NetworkClientState + " room=" + PhotonNetwork.CurrentRoom?.Name + " kind=" + PhotonSession.Kind + " phase=" + PhotonSession.Phase + " ready=" + PhotonSession.Instance?.WorldReady + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0) + " master=" + PhotonNetwork.IsMasterClient;
            if (state != networkState) { networkState = state; Log("STATE " + state); }
            foreach (string role in Roles)
                if (ReadReceipt(role)?.error is string error && error.Length > 0) throw new Exception("Peer " + role + " failure: " + error);
            if (revision != control.revision)
            {
                bool stepChanged = revision < 0 || ReadStep != control.step;
                revision = control.revision;
                if (stepChanged) { ReadStep = control.step; entered = EditorApplication.timeSinceStartup; stepTime = Time.time; subTime = Time.time; sub = 0; action = false; readySince = 0; Log("step=" + control.step + " phase=" + control.phase); if (Host) ResetStep(); }
            }
            if (!Host) { Peer(); return; }
            double age = EditorApplication.timeSinceStartup - entered;
            Require(age < 300, "Timeout step=" + control.step + " phase=" + PhotonSession.Phase + " status=" + PhotonSession.Instance?.Status);
            HostStep(age);
        }
        catch (IOException) { }
        catch (Exception e)
        {
            done = true;
            Log("FAIL " + e.Message + " " + e.StackTrace?.Split('\n').FirstOrDefault());
            try { WriteReceipt(error: e.Message); } catch (IOException) { }
        }
    }
    static int ReadStep = -1;

    static void Peer()
    {
        var session = PhotonSession.Instance;
        double age = EditorApplication.timeSinceStartup - entered;
        if (control.step >= 1 && control.step < 3 && !PhotonSession.IsPrivate && Ready && (!action || age % 15 < .06))
        { session.RequestJoinCode(control.code); action = true; readySince = 0; Log("JOIN by code " + control.code); }
        if (control.warpId != warpDone && Ready && PhotonSession.Phase == RailPhase.Running && NewPlayer.instance != null)
        {
            NewPlayer.instance.Warp(control.p2Pos, Quaternion.Euler(0, control.p2Yaw, 0));
            Inject(new SimulatedRigInput.Frame { crouch = control.p2Crouch });
            warpDone = control.warpId;
            Log("WARP to " + V(control.p2Pos) + " yaw=" + control.p2Yaw.ToString("F0") + " crouch=" + control.p2Crouch + " simulating=" + SimulatedRigInput.Instance?.IsSimulating);
            nextReceipt = 0;
        }
        if (EditorApplication.timeSinceStartup > nextReceipt)
        {
            nextReceipt = EditorApplication.timeSinceStartup + .25;
            string note = "private=" + PhotonSession.IsPrivate + " phase=" + PhotonSession.Phase + " settled=" + Settled + " players=" + (PhotonNetwork.InRoom ? ActivePlayers : 0)
                + " pos=" + (NewPlayer.instance != null ? V(NewPlayer.instance.transform.position) : "?");
            WriteReceipt(note);
        }
    }

    static void HostStep(double age)
    {
        var session = PhotonSession.Instance;
        var p2 = ReadReceipt("player2");
        switch (control.step)
        {
            case 0: // Host creates a private code room.
                if (!Settled) break;
                if (!action) { session.RequestCreatePrivate(control.code); action = true; readySince = 0; Log("CREATE private " + control.code); break; }
                if (!PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Staging || !Settled) break;
                Next(1, "gather"); break;
            case 1: // Peer joins by code.
                if (!PhotonSession.IsPrivate || PhotonSession.Phase != RailPhase.Staging || !Settled || ActivePlayers != 2 || !AllPoses) break;
                Log("GATHERED in " + PhotonNetwork.CurrentRoom.Name);
                Next(2, "launch"); break;
            case 2: // Launch; when running, board both.
                if (PhotonSession.Phase == RailPhase.Staging && Settled && (!action || age % 10 < .06)) { session.StartPrivateRun(); action = true; Log("LAUNCH StartPrivateRun"); }
                if (PhotonSession.Phase != RailPhase.Running || !Settled || ActivePlayers != 2 || !AllPoses || RailTrain.Instance == null) break;
                if (sub == 0) { NextSub(); break; }
                if (sub == 1 && SubAge > 6)
                {
                    NewPlayer.instance.Warp(TrainPoint(.6f, .35f, -3.5f), Train.rotation);
                    P2Warp(TrainPoint(-.6f, .35f, 1.2f), Train.eulerAngles.y, false);
                    Inject(default);
                    Log("BOARD fuel=" + PhotonSession.CurrentFuel + " trainSpeed=" + RailTrain.Instance.Speed + " runId=" + PhotonSession.RunId + " enemies=" + Enemy.Active.Count);
                    NextSub(); break;
                }
                if (sub == 2 && SubAge > 3 && p2 != null && p2.warpDone == control.warpId) Next(3, "place-bar");
                break;
            case 3: // Main grabs a gold bar, releases it and sets it down on the train floor.
                if (sub == 0)
                {
                    var bars = UnityEngine.Object.FindObjectsByType<ItemGoldBar>(FindObjectsSortMode.None).Select(b => b.GetComponent<PhotonGrabbable>()).Where(g => g != null && !g.IsHeldByAnyPlayer && !g.IsStashed).ToArray();
                    Require(bars.Length > 0, "No gold bar found");
                    bar = bars.OrderBy(b => Vector3.Distance(b.transform.position, Train.position)).First();
                    var sounds = bar.GetComponent<ItemSounds>();
                    var fleshEvent = sounds != null ? sounds.Get(ItemSound.FleshImpact) : null;
                    var body = bar.GetComponent<Rigidbody>();
                    Log("BAR " + bar.name + " view=" + bar.GetComponent<PhotonView>().ViewID + " owner=" + bar.GetComponent<PhotonView>().OwnerActorNr + " fleshSound=" + (fleshEvent != null ? fleshEvent.name : "none")
                        + " radius=" + BarRadius + " mass=" + body.mass + " damping=" + body.linearDamping + " launch=" + bar.PlayerLaunchSpeed);
                    Require(Hand.ForceGrab(bar), "ForceGrab bar failed");
                    NextSub(); break;
                }
                if (sub == 1 && SubAge > 1)
                {
                    Require(bar.IsHeld, "Bar not held");
                    Hand.Release();
                    var body = bar.GetComponent<Rigidbody>();
                    body.position = TrainPoint(-.6f, .3f, -1f); body.rotation = Train.rotation;
                    bar.transform.SetPositionAndRotation(body.position, body.rotation);
                    body.linearVelocity = Vector3.zero; body.angularVelocity = Vector3.zero;
                    Log("SET DOWN bar on train floor at " + V(body.position) + " owner=" + bar.GetComponent<PhotonView>().OwnerActorNr + " mine=" + bar.GetComponent<PhotonView>().IsMine);
                    NextSub(); break;
                }
                if (sub == 2 && SubAge > 2.5f)
                {
                    var c = BarCollider.bounds.center;
                    P2Warp(new Vector3(c.x, c.y + .25f, c.z), Train.eulerAngles.y, false);
                    Next(4, "resting-settle");
                }
                break;
            case 4: // Player2 stands on the bar; wait for the active window to expire.
                if (p2 == null || p2.warpDone != control.warpId || StepAge < 4) break;
                {
                    float lastActive = (float)typeof(PhotonGrabbable).GetField("_lastActiveTime", F).GetValue(bar);
                    var victim = Victim; victim.TryGetHeadPosition(out var head);
                    var avatarColliders = victim.GetComponentsInChildren<Collider>(true);
                    Log("RESTING bar=" + V(BarCollider.bounds.center) + " sinceActive=" + (Time.time - lastActive).ToString("F1") + "s attachedToTrain=" + RailTrain.Instance.Contains(bar.transform.position)
                        + " p2head=" + V(head) + " p2chest=" + (victim.Chest != null ? V(victim.Chest.position) : "?") + " p2hands=" + (victim.LeftHand != null ? V(victim.LeftHand.position) : "?") + "/" + (victim.RightHand != null ? V(victim.RightHand.position) : "?")
                        + " p2note=" + p2.note);
                    Log("AVATAR colliders on main for actor " + victim.photonView.OwnerActorNr + ": " + avatarColliders.Length + " [" + string.Join("; ", avatarColliders.Select(a => a.name + "/" + a.GetType().Name + " trigger=" + a.isTrigger + " enabled=" + a.enabled + " layer=" + LayerMask.LayerToName(a.gameObject.layer) + " rb=" + (a.attachedRigidbody != null ? (a.attachedRigidbody.isKinematic ? "kinematic" : "dynamic") : "none"))) + "]"
                        + " barLayer=" + LayerMask.LayerToName(BarCollider.gameObject.layer));
                    Log("STEP resting-settle (stopped) " + StepCounts());
                    Next(5, "cruise-standing");
                }
                break;
            case 5: // Start the train; observe 20 s at cruise with Player2 standing on the bar.
                if (sub == 0)
                {
                    if (PhotonSession.CurrentFuel < 60) RailTestTools.SetFuel(100);
                    session.SetTrainMoving(true); Log("TRAIN SetTrainMoving(true) fuel=" + PhotonSession.CurrentFuel); NextSub(); break;
                }
                if (sub == 1)
                {
                    if (RailTrain.Instance.Speed < .05f && SubAge > 1.5f) { session.SetTrainMoving(true); subTime = Time.time; }
                    if (RailTrain.Instance.Speed >= RailTestTools.TrainCruiseSpeed * .97f) { Log("CRUISE reached speed=" + RailTrain.Instance.Speed.ToString("F2") + " during accel " + StepCounts()); ResetStep(); NextSub(); }
                    break;
                }
                if (sub == 2 && SubAge >= 20)
                {
                    Log("STEP cruise-standing 20s speed=" + RailTrain.Instance.Speed.ToString("F2") + " " + StepCounts() + " barToP2Head=" + Dist() + " enemiesNear=" + EnemiesNear() + " p2note=" + p2?.note);
                    P2Warp(PlayerFeetAboveBar(), Train.eulerAngles.y, true);
                    Next(6, "cruise-crouched");
                }
                break;
            case 6: // Player2 crouched over the bar for 10 s at cruise.
                if (sub == 0 && p2 != null && p2.warpDone == control.warpId) { ResetStep(); NextSub(); break; }
                if (sub == 1 && SubAge >= 12)
                {
                    Log("STEP cruise-crouched 12s speed=" + RailTrain.Instance.Speed.ToString("F2") + " " + StepCounts() + " barToP2Head=" + Dist() + " enemiesNear=" + EnemiesNear() + " p2note=" + p2?.note);
                    Next(7, "gate-control");
                }
                break;
            case 7: // Gate control: stamp the resting bar active (NotifyPlayerThrown) while still cruising.
                if (sub == 0) { bar.NotifyPlayerThrown(); Log("GATE NotifyPlayerThrown on resting bar speed=" + RailTrain.Instance.Speed.ToString("F2")); NextSub(); break; }
                if (sub == 1 && SubAge >= 1.6f) { Log("STEP gate-control window(0-1.6s) " + StepCounts() + " firstHitAfterStamp=" + (firstHitAt < 0 ? "none" : (firstHitAt - subTime).ToString("F2") + "s")); ResetStep(); NextSub(); break; }
                if (sub == 2 && SubAge >= 4) { Log("STEP gate-control after-window(1.6-5.6s) " + StepCounts()); Next(8, "stop-train"); }
                break;
            case 8: // Stop the train.
                if (RailTrain.Instance.Speed >= .02f && (!action || SubAge > 1)) { session.SetTrainMoving(false); action = true; subTime = Time.time; }
                if (RailTrain.Instance.Speed < .02f && StepAge > 3)
                {
                    Log("STOPPED distance=" + RailTrain.Instance.Distance.ToString("F1") + " " + StepCounts());
                    P2Warp(TrainPoint(0, .35f, 2f), Train.eulerAngles.y + 180, false);
                    Next(9, "held-walk");
                }
                break;
            case 9: // Held: main holds the bar reaching forward and walks through Player2.
                if (sub == 0 && p2 != null && p2.warpDone == control.warpId)
                {
                    if (!bar.IsHeld) Require(Hand.ForceGrab(bar), "ForceGrab for held swing failed");
                    NewPlayer.instance.Warp(TrainPoint(-.12f, .35f, -1.5f), Train.rotation);
                    Inject(new SimulatedRigInput.Frame { rightReach = true });
                    NextSub(); break;
                }
                if (sub == 1 && SubAge > 2) { ResetStep(); Log("WALK start main=" + V(NewPlayer.instance.transform.position) + " bar=" + V(bar.transform.position) + " held=" + bar.IsHeld); Inject(new SimulatedRigInput.Frame { rightReach = true, move = new Vector2(0, 1) }); NextSub(); break; }
                if (sub == 2 && SubAge > .7f) { Inject(new SimulatedRigInput.Frame { rightReach = true }); Log("WALK end main=" + V(NewPlayer.instance.transform.position) + " bar=" + V(bar.transform.position)); NextSub(); break; }
                if (sub == 3 && SubAge > 1.5f)
                {
                    Log("STEP held-walk " + StepCounts() + " closestBarToP2Head=" + closest.ToString("F2") + " held=" + bar.IsHeld);
                    Next(10, "thrown-quick");
                }
                break;
            case 10: // Quick throw from ~2 m.
                if (Throw(1.6f, 0f)) Next(11, "lob-1.2s");
                break;
            case 11: if (Throw(1.8f, 1.2f)) Next(12, "lob-1.7s"); break;
            case 12: if (Throw(1.8f, 1.7f)) Next(13, "lob-2.2s"); break;
            case 13: if (Throw(1.8f, 2.2f)) Next(14, "done"); break;
            case 14:
                Inject(default);
                done = true;
                Log("COMPLETE totals main locks=" + locks + " flesh=" + flesh + " blood=" + blood + "; player2 events=" + p2?.events + " accepted=" + p2?.accepted + " blood=" + p2?.blood);
                break;
        }
    }

    /// <summary>Throw sub-steps. flight == 0: direct 7 m/s throw at the head. Otherwise a ballistic lob arriving after flight seconds.</summary>
    static bool Throw(float distance, float flight)
    {
        var p2 = ReadReceipt("player2");
        bool lob = flight > 0;
        if (sub == 0)
        {
            // Lobs need headroom: use the ground beside the stopped train.
            Vector3 victimPos = lob ? Ground(TrainPoint(5f, 0, 1f)) + Vector3.up * .35f : TrainPoint(0, .35f, 2f);
            P2Warp(victimPos, Train.eulerAngles.y + 180, false);
            NextSub(); return false;
        }
        if (sub == 1 && p2 != null && p2.warpDone == control.warpId && SubAge > 1)
        {
            if (!bar.IsHeld) Require(Hand.ForceGrab(bar), "ForceGrab for throw failed");
            Vector3 back = control.p2Pos - Train.forward * (distance + .65f) - Train.right * .12f;
            NewPlayer.instance.Warp(back, Train.rotation);
            Inject(new SimulatedRigInput.Frame { rightReach = true });
            NextSub(); return false;
        }
        if (sub == 2 && SubAge > 2.5f)
        {
            var victim = Victim;
            Require(victim.TryGetHeadPosition(out Vector3 head), "No victim head");
            Hand.Release();
            Inject(default);
            var body = bar.GetComponent<Rigidbody>();
            Vector3 d = head - body.worldCenterOfMass;
            float t = lob ? flight : Mathf.Max(.1f, d.magnitude / 7f);
            Vector3 v = d / t - .5f * Physics.gravity * t;
            body.linearVelocity = v; body.angularVelocity = Vector3.zero;
            float apex = Mathf.Max(0, v.y * v.y / (2 * -Physics.gravity.y));
            bool clear = !Physics.Raycast(body.worldCenterOfMass + d * .5f, Vector3.up, apex + .5f, ~0, QueryTriggerInteraction.Ignore);
            ResetStep(); releaseAt = Time.time;
            Log("RELEASE " + control.phase + " from=" + V(body.worldCenterOfMass) + " head=" + V(head) + " dist=" + d.magnitude.ToString("F2") + " plannedFlight=" + t.ToString("F2") + "s v=" + V(v) + " speed=" + v.magnitude.ToString("F2") + " apexAboveRelease=" + apex.ToString("F2") + " headroomClear=" + clear + " p2note=" + p2?.note);
            NextSub(); return false;
        }
        if (sub == 3 && SubAge > (lob ? flight : .5f) + 1.5f)
        {
            Log("STEP " + control.phase + " " + StepCounts() + " firstHitAfterRelease=" + (firstHitAt < 0 ? "none" : (firstHitAt - releaseAt).ToString("F2") + "s")
                + " closestBarToP2Head=" + closest.ToString("F2") + " at=" + (closestAt - releaseAt).ToString("F2") + "s");
            return true;
        }
        return false;
    }

    static Vector3 Ground(Vector3 p)
    {
        return Physics.Raycast(p + Vector3.up * 6, Vector3.down, out var hit, 20, ~0, QueryTriggerInteraction.Ignore) ? hit.point : p;
    }
    static Vector3 PlayerFeetAboveBar() { var c = BarCollider.bounds.center; return new Vector3(c.x, c.y + .25f, c.z); }
    static string Dist() { var v = Victim; return v != null && v.TryGetHeadPosition(out var h) && bar != null ? Vector3.Distance(h, BarCollider.bounds.center).ToString("F2") : "?"; }
    static int EnemiesNear() => bar == null ? 0 : Enemy.Active.Count(e => e != null && Vector3.Distance(e.transform.position, bar.transform.position) < 6);
}
#endif
