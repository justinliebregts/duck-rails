using System;
using System.Collections.Generic;
using System.Linq;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using TMPro;
using UnityEngine;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Authoritative Week 2 encounter, rescue, furnace and bridge lifecycle.</summary>
public sealed class RailExpedition : MonoBehaviour, IOnEventCallback
{
    public const string StateKey = "rails.expedition";
    const byte ActionEvent = 108;
    public static RailExpedition Instance { get; private set; }
    public GameObject[] supplies;
    public Transform[] supplyPoints, coalPoints, enemyPoints;
    public GameObject coalPrefab, enemyPrefab;
    public int[] enemyTypes = Array.Empty<int>();
    public GameObject goldPrefab;
    public Transform[] goldPoints = Array.Empty<Transform>();
    public Transform furnace, bridgeButton, bridgeDeck;
    public TMP_Text gauge, bridgeLabel, results;
    public TMP_Text[] runningLabels;
    public Material explosionMaterial;
    public Game.AI.EnemyManager enemies;
    public float bridgeZ = 550;
    public int baseEnemyCount = 2;
    public DeathFlare flarePrefab;
    public RailRunState State { get; private set; } = new RailRunState();
    readonly Queue<(int actor, string action, int target, object[] coal)> requests = new Queue<(int, string, int, object[])>();
    readonly Dictionary<string, double> shownFlares = new Dictionary<string, double>();
    readonly Dictionary<string, bool> tracedDown = new Dictionary<string, bool>();
    string lastJson, localRun, caughtRun;
    string pendingRequestJson;
    int pendingRequestRevision;
    double pendingRequestAt;
    bool wasDown;
    readonly RailAtmosphere atmosphere = new RailAtmosphere();
    double nextTick, nextDownRequest;
    RailReviveEffects reviveEffects;
    readonly Dictionary<string, TMP_Text> downLabels = new Dictionary<string, TMP_Text>();
    RailBridgeCollapse collapse;
    GameObject collapsePrefab;
    readonly HashSet<string> carrierHits = new HashSet<string>();
    readonly HashSet<string> issuedDrops = new HashSet<string>();
    public void CarrierHit(string marker) { if (PhotonSession.Phase == RailPhase.Running && State.carriers.Any(c => c.marker == marker && c.carrying)) carrierHits.Add(marker); }
    public static bool LocalDown => Instance != null && Instance.LocalParticipant?.down == true && PhotonSession.OnTrain;
    public RailParticipant LocalParticipant => State.Player(PhotonNetwork.LocalPlayer?.UserId);
    public static bool ActorDown(int actor) => Instance != null && Instance.State.players.Any(p => p.actor == actor && p.down);

    void Awake()
    {
        Instance = this;
        RailEnemySpawns.Register();
        foreach (var prefab in supplies) PhotonPreparePool.Register(prefab);
        PhotonPreparePool.Register(goldPrefab); PhotonPreparePool.Register(coalPrefab); PhotonPreparePool.Register(enemyPrefab);
        reviveEffects = new RailReviveEffects();
    }
    void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    void OnDisable() { RestoreNightAtmosphere(); PhotonNetwork.RemoveCallbackTarget(this); requests.Clear(); reviveEffects?.Reset(); ResetCollapse(); }
    void OnDestroy() { if (Instance == this) Instance = null; }
    public static bool CanTarget(PhotonPlayer player)
    {
        if (!PhotonSession.IsRailsRoom || Instance == null) return true;
        if (PhotonSession.Phase != RailPhase.Running || ActorDown(player.photonView.OwnerActorNr)) return false;
        return player.TryGetHeadPosition(out var head) && RailTrain.Instance != null &&
            (RailTrain.Instance.Contains(head) ||
            (RailRoute.Instance != null ? RailRoute.Instance.Ready && RailRoute.Instance.IsEncounter(head) : head.x > 4 && head.z >= 140 && head.z <= 550));
    }
    public static void RequestDown()
    {
        if (RailAdminTools.Invincible) return;
        var instance = Instance;
        if (instance == null || PhotonNetwork.Time < instance.nextDownRequest || LocalDown) return;
        instance.nextDownRequest = PhotonNetwork.Time + .3;
        TraceFlare("local down requested via=self onTrain=" + PhotonSession.OnTrain + " " + LocalWhere());
        instance.Request("down");
    }
    public static void RequestEnemyDown(Game.AI.Enemy enemy)
    {
        if (RailAdminTools.Invincible) return;
        var instance = Instance;
        if (instance == null || enemy == null || PhotonNetwork.Time < instance.nextDownRequest || LocalDown) return;
        instance.nextDownRequest = PhotonNetwork.Time + .3;
        var view = enemy.GetComponent<PhotonView>();
        TraceFlare("local down requested via=enemy view=" + (view != null ? view.ViewID : 0) + " onTrain=" + PhotonSession.OnTrain + " " + LocalWhere());
        instance.Request("enemy-down", view != null ? view.ViewID : 0);
    }
    // Flare/down diagnostics go to the bounded RailDiagnostics event list so a submitted
    // report carries them. Actor numbers only; never user IDs or nicknames.
    static void TraceFlare(string message) => RailDiagnostics.Instance?.Event("flare: " + message);
    static string Where(Vector3 point)
    {
        var train = RailTrain.Instance;
        return "pos=" + point.ToString("F1") + " aboard=" + (train != null && train.Contains(point))
            + " trainSpeed=" + (train != null ? train.Speed.ToString("F1") : "-");
    }
    static string LocalWhere() => PhotonNetwork.LocalPlayer != null && Position(PhotonNetwork.LocalPlayer.ActorNumber, out var point)
        ? Where(point) : "pos=unknown";
    public void Request(string action, int target = 0)
    {
        if (!PhotonSession.IsRailsRoom || !PhotonSession.OnTrain || PhotonNetwork.LocalPlayer == null) return;
        PhotonNetwork.RaiseEvent(ActionEvent, new object[] { PhotonSession.RunId, action, target },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public void OnEvent(EventData e)
    {
        if (e.Code != ActionEvent || !PhotonNetwork.IsMasterClient || PhotonSession.Phase != RailPhase.Running) return;
        if (!(e.CustomData is object[] d) || (d.Length != 3 && d.Length != 4) || d[0] as string != PhotonSession.RunId || !(d[1] is string action) || !(d[2] is int target)) return;
        if (requests.Count < 64) requests.Enqueue((e.Sender, action, target, d.Length == 4 ? d[3] as object[] : null));
        else if (action == "down" || action == "enemy-down") TraceFlare("master dropped down actor=" + e.Sender + ": request queue full");
    }
    // Legacy test carriages retain their radius; modular fireboxes also require a point inside the cavity.
    public bool AcceptsFurnaceSample(Vector3 offset)
    {
        if (furnace == null) return false;
        var module = furnace.GetComponentInParent<RailFurnaceModule>();
        return module == null || module.AcceptsSample(offset);
    }
    public void RequestStats(object[] values)
    {
        if (!PhotonSession.IsRailsRoom || !PhotonSession.OnTrain || PhotonNetwork.LocalPlayer == null) return;
        PhotonNetwork.RaiseEvent(ActionEvent, new object[] { PhotonSession.RunId, "stats", 0, values },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public void RequestCoal(PhotonView view, PhotonGrabbable item)
    {
        if (!PhotonSession.IsRailsRoom || !PhotonSession.OnTrain || view == null || !view.IsMine ||
            item == null || item.IsHeldByAnyPlayer || item.IsStashed || furnace == null) return;
        var sample = new object[] { view.SpawnToken, item.HoldVersion,
            Quaternion.Inverse(furnace.rotation) * (view.transform.position - furnace.position), PhotonNetwork.Time };
        PhotonNetwork.RaiseEvent(ActionEvent, new object[] { PhotonSession.RunId, "coal", view.ViewID, sample },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, SendOptions.SendReliable);
    }
    public static bool Position(int actor, out Vector3 point)
    {
        // Use the same tracked/simulated head for local and remote actors. The
        // gorilla rig root can sit below the deck while the player is aboard;
        // adding a fixed height made the host fail its own finish-volume test.
        var p = PhotonPlayer.ForActor(actor);
        if (p != null && p.TryGetHeadPosition(out point)) return true;
        if (PhotonNetwork.LocalPlayer != null && actor == PhotonNetwork.LocalPlayer.ActorNumber && GR.Player.instance != null &&
            GR.Player.instance.TryGetHeadPivotHeadPosition(out point)) return true;
        point = default; return false;
    }
    static bool HandNear(int actor, Vector3 point, float radius)
    {
        var p = PhotonPlayer.ForActor(actor);
        if (p == null) return false;
        for (int i = 0; i < 2; i++) if (p.TryGetHandPose(i == 0, out var hand, out _) && Vector3.Distance(hand, point) <= radius) return true;
        return false;
    }
    static bool RescuePosition(int actor, out Vector3 point)
    {
        var avatar = PhotonPlayer.ForActor(actor);
        if (avatar != null && avatar.TryGetHeadPosition(out point)) return true;
        return Position(actor, out point);
    }
    public static bool TryGetDesktopRescuePoint(Ray aim, float reach, int obstructionMask, out Vector3 point)
    {
        point = default;
        if (Instance == null || !PhotonSession.OnTrain || LocalDown) return false;
        float nearest = reach;
        bool found = false;
        foreach (var participant in Instance.State.players)
        {
            if (!participant.down || !IsConnected(participant.actor) ||
                participant.actor == PhotonNetwork.LocalPlayer.ActorNumber ||
                !RescuePosition(participant.actor, out var head)) continue;
            float along = Vector3.Dot(head - aim.origin, aim.direction);
            if (along <= 0 || Vector3.Distance(aim.origin, head) > reach || along > nearest ||
                Vector3.Distance(aim.GetPoint(along), head) > .65f ||
                Physics.Linecast(aim.origin, head, obstructionMask, QueryTriggerInteraction.Ignore)) continue;
            nearest = along; point = head; found = true;
        }
        return found;
    }
    void ReadState()
    {
        string json = PhotonSession.Get(StateKey, "");
        if (pendingRequestJson != null && json == pendingRequestJson &&
            PhotonSession.Get(RailSessionRules.Revision, 0) >= pendingRequestRevision)
        { if (requests.Count > 0) requests.Dequeue(); pendingRequestJson = null; }
        if (json == lastJson && State.run == PhotonSession.RunId) return;
        lastJson = json;
        State = string.IsNullOrEmpty(json) ? new RailRunState() : JsonUtility.FromJson<RailRunState>(json);
        if (State.run != PhotonSession.RunId) State = new RailRunState { run = PhotonSession.RunId };
    }
    void Update()
    {
        if (!PhotonSession.IsRailsRoom) { reviveEffects.Reset(); return; }
        if (!PhotonNetwork.IsMessageQueueRunning || PhotonSession.Instance == null || PhotonSession.Instance.HasRoomRequest) { reviveEffects.StopProgress(); return; }
#if UNITY_EDITOR
        if (PhotonSession.Phase == RailPhase.Running && PhotonSession.Instance.WorldReady &&
            !RailWorld.InputBlocked && RailDesktopInput.GetKeyDown(KeyCode.F))
            Request("editor-fuel-100");
#endif
        ReadState();
        if (!PhotonSession.OnTrain)
        {
            if (PhotonNetwork.IsMasterClient) foreach (var e in Game.AI.Enemy.Active.ToArray())
                if (e != null && e.TryGetComponent<PhotonView>(out var view) && view.IsMine) PhotonNetwork.Destroy(e.gameObject);
            requests.Clear(); reviveEffects.Reset(); wasDown = false;
            foreach (var label in downLabels.Values) if (label != null) label.gameObject.SetActive(false);
            return;
        }
        if (RailRoute.Instance != null && !RailRoute.Instance.Ready) return;
        if (localRun != PhotonSession.RunId)
        {
            localRun = PhotonSession.RunId; caughtRun = null; shownFlares.Clear(); tracedDown.Clear(); wasDown = false; requests.Clear(); pendingRequestJson = null;
            RestoreNightAtmosphere();
            ResetCollapse();
        }
        float intoFinale = RailRoute.Instance != null && RailTrain.Instance != null
            ? RailTrain.Instance.Distance - RailRoute.Instance.FinaleStart : -1;
        atmosphere.Apply(RailAtmosphere.Progress(intoFinale, State.finished && State.success, PhotonNetwork.Time - State.finishedAt));
        Present();
        RailStats.Tick();
        RailAnalytics.Observe(State);
        ConsumeCommittedCoal();
        ShowFuelPopups();
        RailGoldReceiver.PresentRewards(this);
        if (PhotonNetwork.IsMasterClient) SpawnCarrierDrops();
        if (PhotonNetwork.IsMasterClient && PhotonNetwork.Time >= nextTick)
        { nextTick = PhotonNetwork.Time + .1; TickAuthority(); }
    }
    void TickAuthority()
    {
        bool ending = PhotonSession.Phase == RailPhase.Ending && State.finished;
        if (PhotonSession.Phase != RailPhase.Running && !ending) return;
        if (!ending) SpawnEnemies();
        if (pendingRequestJson != null && PhotonNetwork.Time - pendingRequestAt < 1.2) return;
        pendingRequestJson = null;
        var next = JsonUtility.FromJson<RailRunState>(JsonUtility.ToJson(State));
        double now = PhotonNetwork.Time;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            var p = next.Player(player.UserId);
            if (p == null) { p = new RailParticipant { id = player.UserId, name = player.NickName }; next.players.Add(p); }
            p.actor = player.ActorNumber; p.name = player.NickName;
        }
        var changes = new Hashtable();
        if (!ending && RailRoute.Instance != null) UpdateCarriers(next, now);
        // Process one request per CAS. Keep it queued until the server's snapshot acknowledges it.
        if (!ending && requests.Count > 0)
        {
            var request = requests.Peek();
            var p = next.players.Find(x => x.actor == request.actor);
            if (p != null && Position(request.actor, out var position))
                ApplyRequest(next, p, position, request.action, request.target, now, changes, request.coal);
            else if (request.action == "down" || request.action == "enemy-down")
                TraceFlare("master skipped down actor=" + request.actor + ": " + (p == null ? "no participant" : "position unavailable"));
        }
        foreach (var p in next.players.Where(p => p.down))
        {
            // Prefer the current rescuer while still in reach, so a second helper never
            // steals the slot. If the rescuer changes while someone else is still in reach,
            // hand over without resetting progress; only losing everyone resets it.
            RailParticipant rescuer = null;
            if (RescuePosition(p.actor, out var pos))
            {
                bool Valid(RailParticipant r) => !r.down && IsConnected(r.actor) && r.id != p.id && HandNear(r.actor, pos, .95f);
                var current = string.IsNullOrEmpty(p.rescuer) ? null : next.Player(p.rescuer);
                rescuer = current != null && Valid(current) ? current : next.players.FirstOrDefault(Valid);
            }
            if (rescuer == null)
            {
                // Brief grace: a single dropped hand sample pauses progress instead of wiping it.
                if (p.reviveAt <= 0) { p.rescuer = ""; p.reviveLostAt = 0; }
                else if (p.reviveLostAt <= 0) p.reviveLostAt = now;
                else if (now - p.reviveLostAt >= RailRunState.ReviveGraceSeconds) { p.rescuer = ""; p.reviveAt = 0; p.reviveLostAt = 0; }
            }
            else if (p.reviveAt <= 0) { p.rescuer = rescuer.id; p.reviveAt = now; p.reviveLostAt = 0; }
            else
            {
                if (p.reviveLostAt > 0) { p.reviveAt += now - p.reviveLostAt; p.reviveLostAt = 0; } // pause during grace
                p.rescuer = rescuer.id;
                if (now - p.reviveAt >= RailRunState.ReviveSeconds)
                { p.down = false; p.protectedUntil = now + RailRunState.ProtectionSeconds; p.reviveAt = 0; p.reviveLostAt = 0; p.rescuer = ""; p.reviveSequence++; p.revivedAt = now; if (!ending) { rescuer.revives++; p.revived++; } }
            }
        }
        if (ending)
        {
            // Teammates may still help one another during the ride. Locked finish
            // credit and gameplay statistics remain unchanged.
            requests.Clear();
            string rescued = JsonUtility.ToJson(next);
            if (rescued != lastJson) PhotonSession.Instance.Commit(new Hashtable { [StateKey] = rescued });
            return;
        }
        next.UpdateBridgeHold(now, BridgeHoldUntil(next, now));
        next.UpdateBridgeReinforcements(now, Mathf.Max(0, enemyPoints.Length - baseEnemyCount));
        SampleStats(next, now);
        bool allDown = next.players.Any(p => IsConnected(p.actor)) && next.players.Where(p => IsConnected(p.actor)).All(p => p.down);
        if (!allDown) next.allDownAt = 0;
        else if (next.allDownAt <= 0) next.allDownAt = now;
        var train = RailTrain.Instance;
        if (train != null && train.transform.position.z + 5 >= bridgeZ)
        {
            if (next.BridgeProgress(now) < 1) next.Finish(now, "Bridge collision", Array.Empty<string>());
            else next.Finish(now, "Bridge crossed", next.players.Where(p => IsConnected(p.actor) && Position(p.actor, out var point) && train.ContainsPadded(point)).Select(p => p.id));
        }
        if (!next.finished && next.allDownAt > 0 && now - next.allDownAt >= RailRunState.AllDownGrace)
            next.Finish(now, "Crew down", Array.Empty<string>());
        if (next.finished)
        {
            var motion = PhotonSession.Get(RailSessionRules.Motion, new object[] { now, 0f, 0f, 0f });
            float distance = train != null ? train.Distance : 0;
            changes[RailSessionRules.Motion] = new object[] { now, distance, next.success ? 4f : 0f, next.success ? 4f : 0f };
            // Ending has a fuel budget far beyond its fixed 32-second ride.
            changes[RailSessionRules.Fuel] = next.success ? 100f : PhotonSession.CurrentFuel;
            changes[RailSessionRules.Phase] = (int)RailPhase.Ending;
            changes[RailSessionRules.Deadline] = now + 32;
            changes["rails.success"] = next.success;
        }
        string json = JsonUtility.ToJson(next);
        // Invalid/repeated requests make no state change, so no property callback
        // can acknowledge them. Discard them without blocking subsequent actions.
        if (json == lastJson && changes.Count == 0)
        { if (requests.Count > 0) requests.Dequeue(); return; }
        changes[StateKey] = json;
        int requestRevision = PhotonSession.Get(RailSessionRules.Revision, 0) + 1;
        if (PhotonSession.Instance.Commit(changes) && requests.Count > 0)
        { pendingRequestJson = json; pendingRequestRevision = requestRevision; pendingRequestAt = now; }
    }
    public static bool IsConnected(int actor) => PhotonNetwork.CurrentRoom?.GetPlayer(actor)?.IsInactive == false;
    // Actors counted as holding the bridge button on the latest master tick (superlatives only).
    readonly HashSet<int> bridgeHolders = new HashSet<int>();
    // Tuning, not acceptance: see docs/game-design.md "Superlatives".
    public const float LoneWolfMeters = 15, EmotionalSupportMeters = 2, StillMetersPerSecond = .2f;
    /// <summary>Master-only, 10 Hz. Accumulates per-player seconds/distances for end-of-run superlatives.</summary>
    void SampleStats(RailRunState s, double now)
    {
        if (s.startAt <= 0) s.startAt = now;
        float dt = s.sampleAt > 0 ? (float)Math.Min(now - s.sampleAt, .5) : 0;
        s.sampleAt = now;
        var train = RailTrain.Instance;
        if (dt <= 0 || train == null) return;
        var present = new List<(RailParticipant p, Vector3 pos)>();
        foreach (var p in s.players)
            if (IsConnected(p.actor) && Position(p.actor, out var pos)) present.Add((p, pos));
        bool bridgeOpen = s.BridgeProgress(now) < 1;
        // Whoever holds the drive lease with the handle pushed forward is "holding the throttle".
        int driver = PhotonSession.DriveGripActor;
        bool driving = driver != 0 && PhotonSession.DriveGripState != null && PhotonSession.DriveGripState.Length > 3 &&
            PhotonSession.DriveGripState[3] is float lever && RailDriveLease.SpeedFraction(lever) > 0;
        foreach (var (p, pos) in present)
        {
            if (driving && p.actor == driver) p.throttleSeconds += dt;
            bool hadPrevious = p.trackedSeconds > 0;
            p.trackedSeconds += dt;
            bool aboard = train.Contains(pos);
            if (aboard) p.aboardSeconds += dt; else p.offTrainSeconds += dt;
            if (p.down) p.downSeconds += dt;
            p.maxFromTrain = Mathf.Max(p.maxFromTrain, Vector3.Distance(pos, train.transform.position));
            if (present.Count > 1)
            {
                float nearest = float.MaxValue;
                foreach (var (o, other) in present) if (o != p) nearest = Mathf.Min(nearest, Vector3.Distance(pos, other));
                if (nearest > LoneWolfMeters) p.loneSeconds += dt;
                if (nearest <= EmotionalSupportMeters) p.nearSeconds += dt;
            }
            // Stillness is measured in the train's frame while aboard so a moving train is not "movement".
            Vector3 frame = aboard ? train.transform.InverseTransformPoint(pos) : pos;
            if (hadPrevious && aboard == p.sampledAboard && !p.down && (frame - p.lastSample).magnitude / dt < StillMetersPerSecond)
                p.stillSeconds += dt;
            p.lastSample = frame; p.sampledAboard = aboard;
            if (bridgeOpen && bridgeHolders.Contains(p.actor)) p.bridgeSeconds += dt;
        }
    }
    double BridgeHoldUntil(RailRunState state, double now)
    {
        bridgeHolders.Clear();
        if (bridgeButton == null) return 0;
        var control = bridgeButton.GetComponent<RailHoldControl>();
        if (control == null || !control.isActiveAndEnabled || !control.Interactable) return 0;
        double until = 0;
        foreach (var player in PhotonNetwork.PlayerList)
        {
            var participant = state.players.Find(p => p.actor == player.ActorNumber);
            var value = player.CustomProperties[RailHoldControl.BridgeKey];
            if (player.IsInactive || participant == null || participant.down ||
                !RailHoldControl.IsLive(value, state.run, now) ||
                !Position(player.ActorNumber, out var position) || Vector3.Distance(position, bridgeButton.position) >= 3 ||
                (RailTrain.Instance != null && RailTrain.Instance.Contains(position)) ||
                !HandNear(player.ActorNumber, control.ContactPosition, .65f)) continue;
            until = Math.Max(until, (double)((object[])value)[1]);
            bridgeHolders.Add(player.ActorNumber);
        }
        return until;
    }
    void ApplyRequest(RailRunState state, RailParticipant p, Vector3 position, string action, int target, double now, Hashtable changes, object[] coalSample = null)
    {
#if UNITY_EDITOR
        if (action == "editor-fuel" || action == "editor-fuel-100")
        {
            var currentMotion = PhotonSession.Get(RailSessionRules.Motion, new object[] { now, 0f, 0f, 0f });
            RailSessionRules.EvaluateTrain(currentMotion, PhotonSession.Get(RailSessionRules.Fuel, RailResourceBalance.StartingFuel), now,
                out var distance, out var velocity, out var remainingFuel);
            changes[RailSessionRules.Motion] = new object[] { now, distance, velocity, (float)currentMotion[3] };
            changes[RailSessionRules.Fuel] = Mathf.Min(RailResourceBalance.Capacity, remainingFuel + (action == "editor-fuel-100" ? 100f : 1f));
            return;
        }
        // Only the requesting participant is changed; production masters omit this action.
        if (action == "editor-revive")
        {
            if (!p.down) return;
            p.down = false; p.reviveAt = 0; p.rescuer = "";
            p.reviveLostAt = 0; p.reviveSequence++; p.revivedAt = now;
            p.protectedUntil = now + RailRunState.ProtectionSeconds;
            state.allDownAt = 0;
            return;
        }
#endif
        if (action == "admin-coal")
        {
            var sender = PhotonNetwork.CurrentRoom?.GetPlayer(p.actor);
            if (sender?.CustomProperties[RailAdminTools.AdminProperty] is bool admin && admin &&
                (target == 1 || target == 50)) ApplyCoalFuel(changes, now, target);
            return;
        }
        if (action == "admin-train-speed")
        {
            var sender = PhotonNetwork.CurrentRoom?.GetPlayer(p.actor);
            if (sender?.CustomProperties[RailAdminTools.AdminProperty] is bool admin && admin &&
                (target == 1 || target == 5)) RailTestTools.ApplyTrainSpeed(changes, target == 5, now);
            return;
        }
        if (action == "stats") { RailStats.Apply(p, coalSample); return; }
        if (action == "down" || action == "enemy-down")
        {
            string who = "actor=" + p.actor + " via=" + action;
            if (PhotonSession.Phase != RailPhase.Running) { TraceFlare("master ignored down " + who + ": phase " + PhotonSession.Phase); return; }
            if (p.down) return; // Repeat while already down: expected, not traced.
            if (now < p.protectedUntil) { TraceFlare("master ignored down " + who + ": revive protection"); return; }
            if (RailTrain.Instance.Contains(position))
            {
                var attacker = action == "enemy-down" ? PhotonView.Find(target)?.GetComponent<Game.AI.Enemy>() : null;
                if (attacker == null || attacker.Boarding == null || !attacker.Boarding.IsAboard || attacker.IsDormant
                    || attacker.IsKnocked || attacker.IsStunned || attacker.Root == null
                    || Vector3.Distance(attacker.Root.position, position) >= 2f)
                {
                    TraceFlare("master ignored down " + who + " " + Where(position) + ": aboard, attacker " +
                        (attacker == null ? "none" : "aboard=" + (attacker.Boarding != null && attacker.Boarding.IsAboard) +
                        " dormant=" + attacker.IsDormant + " knocked=" + attacker.IsKnocked + " stunned=" + attacker.IsStunned +
                        " dist=" + (attacker.Root != null ? Vector3.Distance(attacker.Root.position, position).ToString("F1") : "-")));
                    return;
                }
            }
            p.down = true; p.downs++;
            bool flare = p.flareAt <= 0 || now - p.flareAt >= RailRunState.FlareCooldown;
            TraceFlare("master accepted down " + who + " " + Where(position) + " flare=" +
                (flare ? "new" : "cooldown (last " + (now - p.flareAt).ToString("F0") + "s ago)"));
            if (flare) { p.flareAt = now; p.flarePosition = position; }
            return;
        }
        if (p.down) return;
        if (action == "gold") { RailGoldReceiver.ApplyDeposit(state, p, position, target, coalSample, now); return; }
        if (action != "coal") return;
        var view = PhotonView.Find(target);
        if (view == null || state.WasConsumed(target, view.SpawnToken)) return;
        var coal = view != null ? view.GetComponent<RailCoal>() : null;
        var item = view != null ? view.GetComponent<PhotonGrabbable>() : null;
        if (coal == null || item == null || Vector3.Distance(position, furnace.position) > 5) return;
        if (coalSample != null)
        {
            if (coalSample.Length != 4 || !(coalSample[0] is string spawn) || !(coalSample[1] is int version) ||
                !(coalSample[2] is Vector3 offset) || !(coalSample[3] is double sent) ||
                !RailCoal.ValidInsertion(p.actor, view.ControllerActorNr, spawn, view.SpawnToken,
                    version, item.HoldVersion, offset, now - sent) || !AcceptsFurnaceSample(offset)) return;
            // Local authority has newer direct evidence; remote held state can be
            // the pre-release snapshot and must not veto its controller's release.
            if (view.IsMine && (item.IsHeldByAnyPlayer || item.IsStashed)) return;
        }
        else if (item.IsHeldByAnyPlayer || item.IsStashed || Vector3.Distance(coal.transform.position, furnace.position) > .9f ||
            !AcceptsFurnaceSample(Quaternion.Inverse(furnace.rotation) * (coal.transform.position - furnace.position))) return;
        state.consumed.Add(target); state.consumedSpawns.Add(view.SpawnToken); p.coal++;
        if (view.InstantiationData is object[] data && data.Length >= 3 && data[data.Length - 1] is string marker) state.consumedMarkers.Add(marker);
        ApplyCoalFuel(changes, now);
    }
    // Shared by physical furnace deposits and the explicit development-panel simulation.
    public static void ApplyCoalFuel(Hashtable changes, double now, int count = 1)
    {
        var motion = PhotonSession.Get(RailSessionRules.Motion, new object[] { now, 0f, 0f, 0f });
        RailSessionRules.EvaluateTrain(motion, PhotonSession.Get(RailSessionRules.Fuel, RailResourceBalance.StartingFuel), now, out var z, out var speed, out var fuel);
        changes[RailSessionRules.Motion] = new object[] { now, z, speed, (float)motion[3] };
        changes[RailSessionRules.Fuel] = Mathf.Min(RailResourceBalance.Capacity,
            fuel + count * RailResourceBalance.CoalValue(RailRoute.Instance?.Selection?.resourceCrew ?? 1));
    }
    readonly HashSet<string> shownFuel = new HashSet<string>();
    string fuelPopupRun;
    // Every client sees each committed deposit once; coal consumed before we joined stays silent.
    void ShowFuelPopups()
    {
        bool seed = fuelPopupRun != PhotonSession.RunId;
        if (seed) { fuelPopupRun = PhotonSession.RunId; shownFuel.Clear(); }
        var module = furnace != null ? furnace.GetComponentInParent<RailFurnaceModule>() : null;
        for (int i = 0; i < State.consumed.Count && i < State.consumedSpawns.Count; i++)
            if (shownFuel.Add(State.consumed[i] + "|" + State.consumedSpawns[i]) && !seed && module != null)
                module.ShowFuelGain(RailResourceBalance.CoalValue(RailRoute.Instance?.Selection?.resourceCrew ?? 1));
    }
    void ConsumeCommittedCoal()
    {
        foreach (int id in State.consumed)
        {
            var view = PhotonView.Find(id);
            if (view != null && State.WasConsumed(id, view.SpawnToken) &&
                (view.GetComponent<RailCoal>() != null || view.GetComponent<ItemGoldBar>() != null) && view.IsMine) PhotonNetwork.Destroy(view.gameObject);
        }
    }
    void Present()
    {
        double now = PhotonNetwork.Time;
        if (gauge != null) gauge.text = "FUEL  " + PhotonSession.CurrentFuel.ToString("0.0") +
            (gauge.GetComponentInParent<RailTrainAssembly>() == null ? "\nRelease coal into furnace (+2.5)" : "");
        float progress = State.BridgeProgress(now);
        bridgeDeck.localRotation = Quaternion.Euler(-80 * (1 - progress), 0, 0);
        bridgeLabel.text = progress >= 1 ? "BRIDGE READY\nBOARD THE TRAIN" :
            (State.BridgeMoving(now) ? "LOWERING BRIDGE" : State.bridgeAt > 0 ? "PAUSED - HOLD LEVER FORWARD" : "HOLD LEVER FORWARD TO LOWER");
        foreach (var p in State.players)
        {
            if (p.flareAt > 0 && (!shownFlares.TryGetValue(p.id, out var shown) || shown != p.flareAt))
            {
                shownFlares[p.id] = p.flareAt;
                var flare = DeathFlare.SpawnAfterDeath(flarePrefab, p.flarePosition, (float)(now - p.flareAt));
                var view = Camera.main != null ? Camera.main.transform.position : (Vector3?)null;
                TraceFlare("shown actor=" + p.actor + " age=" + (now - p.flareAt).ToString("F1") + " " + Where(p.flarePosition) +
                    " spawned=" + (flare != null) + (flarePrefab == null ? " prefab=missing" : "") +
                    " rise=" + (flare != null ? flare.LaunchRise.ToString("F1") : "-") +
                    " viewerDist=" + (view.HasValue ? Vector3.Distance(view.Value, p.flarePosition).ToString("F0") : "-"));
            }
            bool sawDown = tracedDown.TryGetValue(p.id, out var priorDown) && priorDown;
            if (p.down && !sawDown)
                TraceFlare("seen down actor=" + p.actor + " lastFlare=" + (p.flareAt > 0 ? (now - p.flareAt).ToString("F1") + "s ago" : "never") +
                    " " + (Position(p.actor, out var downAt) ? Where(downAt) : "pos=unknown"));
            tracedDown[p.id] = p.down;
            if (!downLabels.TryGetValue(p.id, out var label))
            {
                var go = new GameObject("Crew condition"); go.transform.SetParent(transform); label = go.AddComponent<TextMeshPro>();
                label.fontSize = 2; label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(3, 1); downLabels[p.id] = label;
            }
            label.gameObject.SetActive(p.down && Position(p.actor, out _));
            if (p.down && Position(p.actor, out var pos))
            {
                label.transform.position = pos + Vector3.up;
                if (Camera.main != null) label.transform.rotation = Camera.main.transform.rotation;
                label.text = p.reviveAt > 0 ? "REVIVING " + RailReviveEffects.Progress(p, now).ToString("P0") : "DOWN\nHold a hand nearby";
                label.color = new Color(1, .75f, .2f);
            }
        }
        var local = LocalParticipant;
        if (local != null && local.down && !wasDown)
        {
            GR.Player.instance?.leftPlayerHand?.Release(); GR.Player.instance?.rightPlayerHand?.Release();
            // Match Duckz death bag cleanup while preserving Rails revival.
            BagInventory.DropAllOnDowned();
            HeadEquipment.DropAllOnDowned();
        }
        wasDown = local?.down == true;
        reviveEffects.Present(State, transform, now);
        bool bridgeCrash = State.finished && State.reason == "Bridge collision";
        float crashAge = (float)(now - State.finishedAt);
        if (bridgeCrash)
        {
            if (collapse == null)
            {
                if (collapsePrefab == null) collapsePrefab = Resources.Load<GameObject>("Rails/BridgeCollapse");
                if (collapsePrefab != null)
                {
                    collapse = Instantiate(collapsePrefab, transform).GetComponent<RailBridgeCollapse>();
                    var train = RailTrain.Instance.transform;
                    collapse.Begin(bridgeDeck, State.BridgeProgress(State.finishedAt),
                        train.position + train.forward * 5 + Vector3.up * 1.5f, crashAge);
                }
            }
            if (collapse != null) collapse.Sample(crashAge);
        }
        results.gameObject.SetActive(State.finished && (!bridgeCrash || crashAge >= RailBridgeCollapse.Duration));
        if (State.finished)
        {
            int returnSeconds = RailSessionRules.ReturnCountdown(PhotonSession.Phase,
                PhotonSession.Get(RailSessionRules.Deadline, 0d), now);
            results.text = "<size=65%>" + (returnSeconds > 0 ? "Returning to compound in " + returnSeconds + "s" : "Returning to compound…") + "</size>\n" +
                (State.success ? "<color=#8AFFBA>SUCCESS</color>" : "<color=#FF8866>FAIL</color>") + "\n" + State.reason + "\n\n" +
                RailResultsTable.Build(State.players);
            bool localAboard = Position(PhotonNetwork.LocalPlayer.ActorNumber, out var localPoint) && RailTrain.Instance.ContainsPadded(localPoint);
            if (caughtRun != State.run)
            { caughtRun = State.run; if (GR.Player.instance != null && !localAboard && !RailAdminTools.NoClip) TeleportAboard(); }
            // Teleport aboard only once per finished run; afterwards the player may walk off freely.
        }
        if (Camera.main != null) Camera.main.backgroundColor = RenderSettings.fogColor;
        if (runningLabels != null) foreach (var label in runningLabels) if (label != null) label.gameObject.SetActive(!State.finished);
    }
    void ResetCollapse()
    {
        if (collapse == null) return;
        collapse.Restore();
        Destroy(collapse.gameObject);
        collapse = null;
    }
    void RestoreNightAtmosphere() => atmosphere.Restore();
    public void SeedCargo()
    {
        string run = PhotonSession.RunId;
        bool Has(string marker) => HasMarker(run, marker);
        // Other equipment is bought at shops. RailWorld grants one train flashlight per player.
        for (int i = 0; i < goldPoints.Length; i++)
            if (!Has("gold-" + i) && !State.consumedMarkers.Contains("gold-" + i))
                PhotonNetwork.InstantiateRoomObject(goldPrefab.name, goldPoints[i].position, Quaternion.identity, 0, new object[] { "rails", run, "gold-" + i });
        for (int i = 0; i < coalPoints.Length; i++)
            if (!Has("coal-" + i)) PhotonNetwork.InstantiateRoomObject(coalPrefab.name, coalPoints[i].position, Quaternion.identity, 0, new object[] { "rails", run, "coal-" + i });
    }
    void SpawnEnemies()
    {
        int count = State.bridgeAt > 0 && State.BridgeProgress(PhotonNetwork.Time) < 1 ? enemyPoints.Length : baseEnemyCount;
        for (int i = 0; i < count; i++)
        {
            string marker = "enemy-" + i;
            bool exists = HasMarker(PhotonSession.RunId, marker);
            if (!exists) PhotonNetwork.InstantiateRoomObject(RailEnemySpawns.ForcedEnemyType >= 0 ? RailEnemySpawns.Prefab(RailEnemySpawns.ForcedEnemyType).name : i < enemyTypes.Length ? RailEnemySpawns.Prefab(enemyTypes[i]).name : enemyPrefab.name, enemyPoints[i].position, enemyPoints[i].rotation, 0, new object[] { "rails", PhotonSession.RunId, marker });
        }
        var points = RailRoute.Instance != null ? RailRoute.Instance.finale.bridgeReinforcements : null;
        if (points == null || points.Length == 0) return;
        for (int i = 0; i < State.bridgeReinforcementCount; i++)
        {
            string marker = "bridge-wave-" + i;
            if (HasMarker(PhotonSession.RunId, marker)) continue;
            var point = points[i % points.Length];
            PhotonNetwork.InstantiateRoomObject(RailEnemySpawns.Prefab(0).name, point.position, point.rotation,
                0, new object[] { "rails", PhotonSession.RunId, marker });
        }
    }
    static bool HasMarker(string run, string marker)
    {
        foreach (var v in PhotonNetwork.PhotonViewCollection)
            if (v != null && v.InstantiationData is object[] d && d.Length >= 3 && d[d.Length - 2] as string == run && d[d.Length - 1] as string == marker) return true;
        return false;
    }
    void UpdateCarriers(RailRunState next, double now)
    {
        // Roughly half the enemies carry coal, plus bridge defenders. Equipment is retained by marker.
        foreach (var enemy in Game.AI.Enemy.Active)
        {
            var carrier = enemy.GetComponent<RailCoalCarrier>(); string marker = carrier != null ? carrier.Marker : null;
            if (marker == null || !marker.StartsWith("enemy-")) continue;
            if (!int.TryParse(marker.Substring(6), out int index)) continue;
            var state = next.carriers.Find(c => c.marker == marker);
            if (state == null && (index % 2 == 0 || index >= baseEnemyCount)) { state = new RailCarrierState { marker = marker }; next.carriers.Add(state); }
            if (state != null && state.carrying && carrierHits.Contains(marker))
            { state.carrying = false; state.drops.Add(enemy.Root.position + Vector3.up * .7f); }
        }
        // Keep hit intents until the committed state acknowledges the drop, including CAS contention.
        carrierHits.RemoveWhere(marker => State.carriers.Any(c => c.marker == marker && !c.carrying));
        var train = RailTrain.Instance;
        bool NearCrew(Vector3 point) => Vector3.Distance(point, train.transform.position) < 100 ||
            next.players.Any(p => IsConnected(p.actor) && Position(p.actor, out var pos) && Vector3.Distance(point, pos) < 65);
        bool fuelAvailable = false;
        foreach (var v in PhotonNetwork.PhotonViewCollection)
                if (v != null && v.GetComponent<RailCoal>() != null && v.transform.position.y > -.5f && !next.WasConsumed(v.ViewID, v.SpawnToken) && NearCrew(v.transform.position)) fuelAvailable = true;
        fuelAvailable |= BagObject.Active.Any(b => b != null && NearCrew(b.transform.position) && b.Keys().Any(k => k == coalPrefab.name));
        if (PhotonSession.CurrentFuel > 2 || fuelAvailable) { next.recoveryAt = 0; return; }
        if (next.recoveryAt <= 0) { next.recoveryAt = now + 20; return; }
        if (now < next.recoveryAt) return;
          // Prefer an existing nearby supply opportunity. Otherwise re-equip a
          // spent carrier outside every crew member's view, on its own baked block.
          var localEnemies = Game.AI.Enemy.Active.Where(e => e != null && e.GetComponent<RailCoalCarrier>() != null && NearCrew(e.Root.position))
              .OrderBy(e => Vector3.Distance(e.Root.position, train.transform.position)).ToArray();
          if (localEnemies.Any(e => next.carriers.Any(c => c.marker == e.GetComponent<RailCoalCarrier>().Marker && c.carrying)))
          { next.recoveryAt = now + 30; return; }
          var candidate = localEnemies.FirstOrDefault(e => CanResupply(e.Root.position));
        if (candidate == null) return;
        var key = candidate.GetComponent<RailCoalCarrier>().Marker;
        var retained = next.carriers.Find(c => c.marker == key);
          if (retained == null) { retained = new RailCarrierState { marker = key, carrying = false }; next.carriers.Add(retained); }
        if (!retained.carrying)
        {
            retained.generation++; retained.carrying = true;
        }
        next.recoveryAt = now + 30;
    }
      bool CanResupply(Vector3 position)
      {
          foreach (var player in PhotonNetwork.PlayerList)
          {
              if (player.IsInactive) continue;
              var avatar = PhotonPlayer.ForActor(player.ActorNumber);
              if (avatar == null || !avatar.TryGetGaze(out var eye, out var forward)) return false;
              Vector3 delta = position + Vector3.up - eye;
              if (delta.magnitude < 15) return false;
              if (Vector3.Dot(forward.normalized, delta.normalized) > .3f &&
                  !Physics.Linecast(eye, position + Vector3.up, 1 << 6, QueryTriggerInteraction.Ignore)) return false;
          }
          return true;
      }
    void SpawnCarrierDrops()
    {
        if (State.finished || !PhotonSession.OnTrain) return;
        string receiptKey = "rails.carrierDrops";
        var receipts = PhotonSession.Get(receiptKey, Array.Empty<string>());
        bool changed = false;
        foreach (var carrier in State.carriers) for (int i = 0; i < carrier.drops.Count; i++)
        {
            string marker = "carrier-" + carrier.marker + "-" + i;
            string receipt = State.run + ":" + marker;
            // Issuance survives stowing, consumption and master handoff. A missing
            // loose object does not mean it should be spawned again.
            if (State.consumedMarkers.Contains(marker) || receipts.Contains(receipt) || issuedDrops.Contains(receipt)) continue;
            if (!HasMarker(State.run, marker))
                PhotonNetwork.InstantiateRoomObject(coalPrefab.name, carrier.drops[i], Quaternion.identity, 0, new object[] { "rails", State.run, marker });
            issuedDrops.Add(receipt); changed = true;
        }
        // Sent after reliable instantiation, by the same authority. Keep only this
        // run's receipts so repeated private departures do not grow room metadata.
        if (changed) PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable {
            [receiptKey] = receipts.Concat(issuedDrops).Where(r => r.StartsWith(State.run + ":", StringComparison.Ordinal)).Distinct().ToArray() });
    }
    void TeleportAboard()
    {
        if (GR.Player.instance == null || RailTrain.Instance == null) return;
        Vector3 position = RailTrain.Instance.AboardPoint(PhotonNetwork.LocalPlayer.ActorNumber);
        var desktop = GR.Player.instance.GetComponent<DesktopPlayer>();
        if (desktop != null) desktop.Teleport(position, Quaternion.identity, true); else NewPlayer.instance?.Warp(position, Quaternion.identity);
    }
}

