using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.XR;

/// <summary>Transfers simulation off an unfocused headset; detects silent game loops, not transport ACKs.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(11000)]
public sealed partial class PhotonMasterHandoff : MonoBehaviourPunCallbacks, IOnEventCallback
{
    static PhotonMasterHandoff instance;
    public static bool LocalAvailable => instance == null || instance.InputAvailable;
    // Link's headset focus is independent of Unity's Game/Scene window focus.
    // Keep the app-focus requirement unless an editor XR display is running.
    bool InputAvailable => HasInputFocus(DesktopPlay.UseDesktop, focused, xrFocused, paused,
        Application.isEditor && display != null && display.running);
    static bool HasInputFocus(bool keyboard, bool appFocus, bool xrFocus, bool appPaused, bool editorXR)
        => !appPaused && (keyboard || ((appFocus || editorXR) && xrFocus));
    const byte HeartbeatCode = 105;
    const byte StartupBarrierCode = 114;
    const double HeartbeatInterval = 1;
    const int TimeoutMilliseconds = 5000;
    static readonly RaiseEventOptions Others = new RaiseEventOptions { Receivers = ReceiverGroup.Others };

    struct Heartbeat
    {
        public int SentAt;
        public bool Ready;
        public bool Received;
    }

    readonly Dictionary<int, Heartbeat> heartbeats = new Dictionary<int, Heartbeat>(PhotonSession.MaxPlayers);
    readonly List<XRDisplaySubsystem> displays = new List<XRDisplaySubsystem>(1);
    XRDisplaySubsystem display;
    bool xrFocused = true;
    bool focused, paused, lastReady;
    int observationStartedAt;
    double nextHeartbeat, retryAt, observeUntil, nextHeldCheck;
    Room room;
    int startupProbe;
    string startupScope;
    double startupProbeAt;

    bool Ready => InputAvailable && PhotonNetwork.IsMessageQueueRunning
        && GameScenes.IsLoaded(PhotonSession.RequiredScene) && PhotonPlayer.Local != null
        // An avatar can exist while cached room spawns/lighting are still arriving.
        // Electing that client lets it seed missing loot using IDs the old master
        // has already allocated. Wait for RailWorld's retained cargo snapshot.
        && (!PhotonSession.IsRailsRoom || PhotonSession.Instance != null &&
            PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest);

    void Awake() { instance = this; focused = Application.isFocused; }
    void OnDestroy() { if (instance == this) instance = null; }

    public override void OnEnable()
    {
        base.OnEnable();
        PhotonNetwork.NetworkingClient.OpResponseReceived += RoomObjectRecovery.OperationResponse;
        ObserveDisplay();
    }

    public override void OnDisable()
    {
        if (display != null) display.displayFocusChanged -= OnDisplayFocus;
        display = null;
        PhotonNetwork.NetworkingClient.OpResponseReceived -= RoomObjectRecovery.OperationResponse;
        base.OnDisable();
    }

    void ObserveDisplay()
    {
        if (display != null) return;
        SubsystemManager.GetSubsystems(displays);
        if (displays.Count == 0) return;
        display = displays[0];
        display.displayFocusChanged += OnDisplayFocus;
    }

    void OnDisplayFocus(bool value)
    {
        if (xrFocused == value) return;
        xrFocused = value;
        // Link can lose headset focus while the shared rig is keyboard-controlled.
        // Track it for the next mode switch without dropping simulated hands.
        if (!DesktopPlay.UseDesktop) AvailabilityChanged(value);
    }

    public override void OnJoinedRoom() => ResetRoom();
    public override void OnLeftRoom() { room = null; heartbeats.Clear(); }
    public override void OnDisconnected(DisconnectCause cause) { PhotonHeldItems.DropLocalHands(); room = null; heartbeats.Clear(); }

    void ResetRoom()
    {
        room = PhotonNetwork.CurrentRoom;
        ResetObservation();
        RoomObjectRecovery.JoinedRoom();
        Publish(true);
    }

    void ResetObservation()
    {
        startupScope = null;
        ClearPickupReports();
        // A waking/joining client must not elect itself using its pre-suspend history.
        heartbeats.Clear();
        observationStartedAt = PhotonNetwork.ServerTimestamp;
        observeUntil = Time.realtimeSinceStartupAsDouble + TimeoutMilliseconds / 1000d;
        nextHeartbeat = retryAt = nextHeldCheck = 0;
    }

    void OnApplicationPause(bool value)
    {
        if (paused == value) return;
        paused = value;
        AvailabilityChanged(!value);
    }

    void OnApplicationFocus(bool value)
    {
        if (focused == value) return;
        bool wasAvailable = InputAvailable;
        focused = value;
        if (DesktopPlay.UseDesktop || wasAvailable != InputAvailable) AvailabilityChanged(value);
    }

    void AvailabilityChanged(bool resuming)
    {
        RoomObjectRecovery.ReconcileAll();
        // Retain this frame's inventory changes while we still own the objects.
        // Dropping a client's hands returns ownership to the (possibly asleep) master.
        RoomObjectRecovery.Flush();
        // Resume also clears a pre-suspend hold if the OS never delivered the loss callback.
        // Do this before announcing readiness or allowing new hand interactions.
        PhotonHeldItems.DropLocalHands();
        RoomObjectRecovery.Flush();
        if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return;
        // Pause/app/XR focus can recover in any order. Discard old candidates on
        // the first wake signal, before an only-partly-focused master can hand off.
        if (resuming) ResetObservation();
        Publish(true);
        if (!Ready && PhotonNetwork.IsMasterClient) TryHandoff();
        // Also flush unavailability when there is no eligible successor yet.
        PhotonNetwork.SendAllOutgoingCommands();
    }

    void Update()
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return;
        if (!ReferenceEquals(room, PhotonNetwork.CurrentRoom)) ResetRoom();
        double now = Time.realtimeSinceStartupAsDouble;
        if (Ready != lastReady || now >= nextHeartbeat) Publish(Ready != lastReady);
        if (Ready && PhotonNetwork.IsMasterClient && now >= nextHeldCheck)
        {
            nextHeldCheck = now + HeartbeatInterval;
            PhotonHeldItems.RecoverUnavailable(this);
        }
        if (now < retryAt) return;

        if (PhotonNetwork.IsMasterClient)
        {
            if (!Ready) TryHandoff();
            return;
        }
        RecoverStartup();
        if (!Ready || PhotonNetwork.MasterClient == null) return;
        int master = PhotonNetwork.MasterClient.ActorNumber;
        bool known = heartbeats.TryGetValue(master, out Heartbeat heartbeat) && heartbeat.Received;
        // Explicit unavailability can be acted on immediately. Missing heartbeats get five seconds.
        if (known ? heartbeat.Ready && Fresh(heartbeat.SentAt, PhotonNetwork.ServerTimestamp) : now < observeUntil) return;
        Player candidate = SelectCandidate();
        if (candidate != null && candidate.IsLocal) RequestMaster(candidate);
    }

    void LateUpdate()
    {
        RoomObjectRecovery.Flush();
        RoomObjectRecovery.Tick();
    }

    public override void OnRoomPropertiesUpdate(Hashtable changes) => RoomObjectRecovery.PropertiesChanged(changes);

    void Publish(bool reliable)
    {
        if (!PhotonNetwork.InRoom || PhotonNetwork.OfflineMode) return;
        ObserveDisplay();
        lastReady = Ready;
        nextHeartbeat = Time.realtimeSinceStartupAsDouble + HeartbeatInterval;
        PhotonNetwork.RaiseEvent(HeartbeatCode, new object[] { PhotonNetwork.ServerTimestamp, lastReady },
            Others, reliable ? SendOptions.SendReliable : SendOptions.SendUnreliable);
    }

    internal static bool Fresh(int sentAt, int now)
    {
        int age = unchecked(now - sentAt); // Photon timestamps wrap; subtraction is wrap-safe.
        return age >= -1000 && age < TimeoutMilliseconds;
    }

    public void OnEvent(EventData data)
    {
        if (data.Code == StartupBarrierCode) { ReceiveStartupBarrier(data); return; }
        if (data.Code == PickupReportCode || data.Code == PickupRefreshCode || data.Code == PickupAuthorityCode)
        { ReceivePickupEvent(data); return; }
        if (data.Code == PhotonHeldItems.DropEvent) { PhotonHeldItems.OnEvent(data); return; }
        if (data.Code != HeartbeatCode || !PhotonNetwork.InRoom || PhotonNetwork.OfflineMode
            || !(data.CustomData is object[] values) || values.Length != 2
            || !(values[0] is int sentAt) || !(values[1] is bool ready)) return;
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(data.Sender);
        if (player == null || player.IsInactive || player.IsLocal || !Fresh(sentAt, PhotonNetwork.ServerTimestamp)) return;
        // Even a short standby can leave otherwise-fresh packets queued. They
        // must not restore a sleeping candidate or override the wake observation window.
        if (unchecked(sentAt - observationStartedAt) < 0) return;
        if (heartbeats.TryGetValue(data.Sender, out Heartbeat previous) && unchecked(sentAt - previous.SentAt) < 0) return;
        heartbeats[data.Sender] = new Heartbeat { SentAt = sentAt, Ready = ready, Received = true };
        if (!ready && Ready && PhotonNetwork.IsMasterClient) PhotonHeldItems.RecoverUnavailable(this);
    }

    // WorldReady includes state produced by the master. A silent host must not
    // make that state a prerequisite for replacing it. This fallback only runs
    // after heartbeat silence, never merely because a live host is still loading.
    bool CanRecoverStartup => PhotonSession.IsRailsRoom && !PhotonNetwork.IsMasterClient &&
        InputAvailable && PhotonNetwork.IsMessageQueueRunning && PhotonSession.Instance != null &&
        !PhotonSession.Instance.WorldReady && !PhotonSession.Instance.HasRoomRequest &&
        !PhotonSession.Instance.AwaitingCrewDestination && PhotonPlayer.Local != null &&
        GameScenes.IsLoaded(PhotonSession.RequiredScene) && RailWorld.Instance != null &&
        (PhotonSession.Phase == RailPhase.Departing || PhotonSession.Phase == RailPhase.Running);

    string StartupScope => PhotonNetwork.CurrentRoom.Name + ":" + PhotonSession.RunId + ":" +
        PhotonSession.Phase + ":" + PhotonNetwork.MasterClient?.ActorNumber;

    bool StartupHostSilent()
    {
        if (PhotonNetwork.MasterClient == null || Time.realtimeSinceStartupAsDouble < observeUntil) return false;
        return !heartbeats.TryGetValue(PhotonNetwork.MasterClient.ActorNumber, out var beat) ||
            !Fresh(beat.SentAt, PhotonNetwork.ServerTimestamp);
    }

    void RecoverStartup()
    {
        if (!CanRecoverStartup || !StartupHostSilent()) { startupScope = null; return; }
        double now = Time.realtimeSinceStartupAsDouble;
        if (startupScope == StartupScope && now - startupProbeAt < TimeoutMilliseconds / 1000d) return;
        startupScope = StartupScope; startupProbeAt = now;
        // Use the same reliable channel as PUN instantiation. The server echo
        // follows its previously queued cached/live spawns; an empty local
        // receive queue alone would not account for packets still in flight.
        PhotonNetwork.RaiseEvent(StartupBarrierCode, new object[] { ++startupProbe, startupScope },
            new RaiseEventOptions { TargetActors = new[] { PhotonNetwork.LocalPlayer.ActorNumber } }, SendOptions.SendReliable);
        PhotonNetwork.SendAllOutgoingCommands();
    }

    void ReceiveStartupBarrier(EventData data)
    {
        if (!CanRecoverStartup || data.Sender != PhotonNetwork.LocalPlayer.ActorNumber ||
            !(data.CustomData is object[] fields) || fields.Length != 2 ||
            !(fields[0] is int probe) || probe != startupProbe || fields[1] as string != startupScope ||
            startupScope != StartupScope || !StartupHostSilent()) return;
        // SetMasterClient uses the server's current-master comparison. Competing
        // recovering peers cannot both win; spawning waits for its confirmation.
        RequestMaster(PhotonNetwork.LocalPlayer);
    }

    public bool IsActorUnavailable(int actor)
    {
        if (actor <= 0 || !PhotonNetwork.InRoom) return false;
        if (actor == PhotonNetwork.LocalPlayer.ActorNumber) return !LocalAvailable;
        Player player = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        if (player != null && player.IsInactive) return true;
        if (heartbeats.TryGetValue(actor, out Heartbeat heartbeat))
            return (heartbeat.Received && !heartbeat.Ready) || !Fresh(heartbeat.SentAt, PhotonNetwork.ServerTimestamp);
        // A confirmed master change must not restart the observation already done
        // since waking. observeUntil separately protects master-election stability.
        return !Fresh(observationStartedAt, PhotonNetwork.ServerTimestamp);
    }

    Player SelectCandidate()
    {
        Player candidate = null;
        int now = PhotonNetwork.ServerTimestamp;
        foreach (Player player in PhotonNetwork.CurrentRoom.Players.Values)
        {
            if (player.IsInactive || player.IsMasterClient) continue;
            // Heartbeats stay fresh for five seconds. A ready heartbeat from
            // staging (or a previous run) must not nominate a client whose new
            // scene/snapshot is still loading. This retained token is cleared
            // on loading and republished by RailWorld only after reconstruction.
            if (PhotonSession.IsRailsRoom && player.CustomProperties[RailSessionRules.Ready] as string !=
                PhotonSession.RunId + ":" + PhotonSession.Phase) continue;
            bool eligible = player.IsLocal ? Ready
                : heartbeats.TryGetValue(player.ActorNumber, out Heartbeat heartbeat) && heartbeat.Ready && Fresh(heartbeat.SentAt, now);
            if (eligible && (candidate == null || player.ActorNumber < candidate.ActorNumber)) candidate = player;
        }
        return candidate;
    }

    void TryHandoff()
    {
        if (Time.realtimeSinceStartupAsDouble < retryAt) return;
        Player candidate = SelectCandidate();
        if (candidate != null) RequestMaster(candidate);
    }

    void RequestMaster(Player candidate)
    {
        retryAt = Time.realtimeSinceStartupAsDouble + HeartbeatInterval;
        // PUN compares against the current master on the server, so competing requests cannot both win.
        // The return value means queued, not confirmed. Authority changes only via PUN's server event.
        if (PhotonNetwork.SetMasterClient(candidate)) PhotonNetwork.SendAllOutgoingCommands();
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        heartbeats[newPlayer.ActorNumber] = new Heartbeat { SentAt = PhotonNetwork.ServerTimestamp };
        Publish(true);
    }
    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        heartbeats.Remove(otherPlayer.ActorNumber);
        if (Ready && PhotonNetwork.IsMasterClient) PhotonHeldItems.RecoverUnavailable(this, otherPlayer.ActorNumber);
    }
    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        ClearPickupReports();
        pickupHandoffAfter = Time.realtimeSinceStartupAsDouble + PickupHandoffCooldown;
        RoomObjectRecovery.ReconcileAll();
        retryAt = 0;
        nextHeldCheck = 0;
        observeUntil = Time.realtimeSinceStartupAsDouble + TimeoutMilliseconds / 1000d;
        // Give a newly confirmed, previously ready host a full interval to start
        // publishing. A near-expired candidate heartbeat must not cause a second election.
        if (heartbeats.TryGetValue(newMasterClient.ActorNumber, out Heartbeat heartbeat) && heartbeat.Ready)
            heartbeats.Remove(newMasterClient.ActorNumber);
        Publish(true);
        if (newMasterClient.IsLocal && !Ready) TryHandoff();
    }
}
