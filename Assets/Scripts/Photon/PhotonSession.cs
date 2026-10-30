using System;
using System.Collections;
using System.Linq;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using Hashtable = ExitGames.Client.Photon.Hashtable;

/// <summary>Authenticated connection, crew transfers and server-retained session state.</summary>
public class PhotonSession : MonoBehaviourPunCallbacks, IOnEventCallback
{
    const byte TrainControlEvent = 107, DriveGripEvent = 110;
    public const string DriveGripProperty = "rails.driveGrip";
    readonly RailDriveLease driveLease = new RailDriveLease();
    string driveLeaseRun;
    bool driveLeaseDirty;
    public static object[] DriveGripState => Get(DriveGripProperty, new object[] { "", 0, "", 0f });
    public static int DriveGripActor => Phase == RailPhase.Running && DriveGripState[0] is string run && run == RunId ? (int)DriveGripState[1] : 0;
    static readonly TypedLobby AutomaticLobby = new TypedLobby("rails-auto-v2", LobbyType.Default);
    bool? requestedTrainMoving;
    string requestedTrainRun;
    RailCrewReturn crewReturn;
    public const string Region = "us", PlayScene = GameScenes.Compound;
    public const int MaxPlayers = 12, RoomCodeLength = 5, StreamRate = 20;
    public const string NoOpenRoomsMessage = "No open compounds", MissingRoomMessage = "No room with that code", RoomFullMessage = "Room is full";
    public static PhotonSession Instance { get; private set; }
    public static bool ConnectPending { get; private set; }
    public static void MarkConnectPending() => ConnectPending = true;
    public static void ClearConnectPending() => ConnectPending = false;
    public static bool IsRailsRoom => PhotonNetwork.InRoom && Get(RailSessionRules.Schema, 0) == RailSessionRules.Version;
    public static RailPhase Phase => (RailPhase)Get(RailSessionRules.Phase, (int)RailPhase.Compound);
    public static string Kind => Get(RailSessionRules.Kind, "");
    public static string RunId => Get(RailSessionRules.Run, "");
    public static bool IsPrivate => Kind == RailSessionRules.PrivateKind;
    public static bool OnTrain => RailSessionRules.IsOnTrain(Phase);
    public static T Get<T>(string key, T fallback) => PhotonNetwork.CurrentRoom?.CustomProperties[key] is T value ? value : fallback;

    enum Request { None, Random, FreshCompound, CreatePrivate, JoinPrivate, Transfer, Rejoin, ReturnToCompound, CrewSearch, CrewDestination, CrewFresh
#if UNITY_EDITOR
        , EditorQuickStart
#endif
    }
    Request request;
    string requestedRoom, previousCompound, resumeRoom, transferKind, transferRun, joinedRoom, followedTransfer;
    string[] transferUsers = Array.Empty<string>();
    int allocationAttempt;
    string allocation;
    AppSettings settings;
    Action<string> loginStatus;
    bool operationPending, loadingScene, quitting, initialized, leaving;
    double operationUntil, retryAt, resumeUntil, nextAuthorityTick, lastCommit, loadingSince;
    int pendingRevision = -1;
    public bool RecoveringConnection { get; private set; }
    public bool ReturningToLogin => false;
    public bool HasRoomRequest => request != Request.None || operationPending || loadingScene;
    public string RoomRequestMessage => Status;
    public string Status { get; private set; } = "Connecting...";
    public string LastError { get; private set; } = "";
    public string CrewReturnDiagnostic => crewReturn?.Diagnostic ?? "No crew return";
    public bool AwaitingCrewDestination => crewReturn != null && crewReturn.Active &&
        Kind == RailSessionRules.CompoundKind && !crewReturn.InDestination;
    public bool WorldReady { get; private set; }
    public event Action RoomMembershipChanged, RoomRequestStarted;
    public event Action<string> RoomRequestFailed;

    public static PhotonSession Ensure() => Instance != null ? Instance : new GameObject(nameof(PhotonSession)).AddComponent<PhotonSession>();
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Application.runInBackground = true;
        gameObject.AddComponent<DuckPlayerColors>();
        gameObject.AddComponent<PhotonMasterHandoff>();
    }
    void OnDestroy() { crewReturn?.Dispose(); if (Instance == this) Instance = null; }
    void OnApplicationQuit() { quitting = true; crewReturn?.Dispose(); }

    public IEnumerator ConnectAndJoin(Action<string> status)
    {
        if (LoginFlow.Previewing) yield break;
        loginStatus = status;
        if (!initialized)
        {
            initialized = true; request = Request.Random;
#if UNITY_EDITOR
            if (RailEditorQuickStart.TryGetRoom(out string editorRoom))
            {
                request = Request.EditorQuickStart;
                allocation = "editor-quick-start-" + editorRoom;
                requestedRoom = editorRoom;
            }
#endif
            MarkConnectPending(); Connect();
        }
        while (!WorldReady) yield return null;
        // Direct Compound entry skips LoadCompound, so release its startup
        // callback here as well as on the scene-loading path.
        loginStatus = null;
    }
    void Configure()
    {
        var source = PhotonNetwork.PhotonServerSettings.AppSettings;
        settings = source.CopyTo(new AppSettings());
        settings.AppVersion = source.AppVersion + "-duck-rails-session-v5";
        settings.FixedRegion = Region;
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.SendRate = PhotonNetwork.SerializationRate = StreamRate;
        var account = PlayerSession.Instance;
        if (account == null) return;
        PhotonNetwork.NickName = account.DisplayName;
        string id = string.IsNullOrEmpty(account.GrId) ? account.PlayerId : account.GrId;
        if (PhotonNetwork.AuthValues == null) PhotonNetwork.AuthValues = new AuthenticationValues(id);
        else PhotonNetwork.AuthValues.UserId = id;
        if (!PhotonNetwork.IsConnected && account.TryGetAccessToken(out string token))
        {
            PhotonNetwork.AuthValues.AuthType = CustomAuthenticationType.Custom;
            PhotonNetwork.AuthValues.AuthGetParameters = null;
            PhotonNetwork.AuthValues.AddAuthParameter("usr", account.PlayerId);
            PhotonNetwork.AuthValues.AddAuthParameter("pla", account.Platform);
            PhotonNetwork.AuthValues.AddAuthParameter("tkn", token);
        }
    }
    void SetStatus(string text)
    {
        if (Status == text) return;
        Status = text;
        loginStatus?.Invoke(text);
    }
#if UNITY_EDITOR
    // Direct scene Play uses the same status banner before Photon connects.
    public void SetEditorSignInStatus(string text) => SetStatus(text);
#endif
    void Started(string text)
    {
        operationPending = true; operationUntil = Time.realtimeSinceStartupAsDouble + 30;
        SetStatus(text);
    }
    void Connect()
    {
        if (quitting) return;
        Configure();
        if (PhotonNetwork.IsConnectedAndReady && !PhotonNetwork.InRoom) { ExecuteRequest(); return; }
        Started(RecoveringConnection ? "Reconnecting..." : "Connecting to Photon...");
        if (!PhotonNetwork.IsConnected && !PhotonNetwork.ConnectUsingSettings(settings, false)) RetryConnection("Could not connect to Photon.");
    }

    public void RequestJoinRandom() => Begin(Request.Random);
    public void RequestCreatePublic() => Begin(Request.FreshCompound);
    public void RequestCreatePrivate() => FailInput("Enter a five-digit code, then choose Create.");
    public void RequestCreatePrivate(string code)
    {
        if (!RailSessionRules.IsCode(code)) { FailInput("Enter exactly five digits."); return; }
        Begin(Request.CreatePrivate, code);
    }
    public void RequestJoinCode(string code)
    {
        if (!RailSessionRules.IsCode(code)) { FailInput("Enter exactly five digits."); return; }
        Begin(Request.JoinPrivate, code);
    }
    public void JoinOpenRoom() { if (!HasRoomRequest) Begin(Request.Random); }
    public void LeaveCurrentRoom() => Begin(Request.Random);
    public bool HasCurrentRoom(out string code, out bool isPublic)
    {
        code = PhotonNetwork.CurrentRoom?.Name; isPublic = Kind == RailSessionRules.CompoundKind;
        return PhotonNetwork.InRoom && !leaving;
    }
    void FailInput(string text) { LastError = text; DuckzAnalytics.SessionJoinFailed(text); RoomRequestFailed?.Invoke(text); }
    void Begin(Request next, string room = null)
    {
        if (HasRoomRequest || quitting) return;
        if (!IsCrewRequest(next)) { crewReturn?.Dispose(); crewReturn = null; }
        requestedTrainMoving = null;
        if (Kind == RailSessionRules.CompoundKind) previousCompound = PhotonNetwork.CurrentRoom.Name;
        LastError = ""; request = next; requestedRoom = room;
        allocationAttempt = 0;
        allocation = next == Request.Transfer || next == Request.CrewFresh ? room : null;
        if (allocation != null) requestedRoom = RailSessionRules.RoomCode(allocation);
        if (next == Request.Transfer) DuckzAnalytics.SessionEvent("transfer");
        WorldReady = false; MarkConnectPending(); RoomRequestStarted?.Invoke();
        PhotonHeldItems.DropLocalHands(); RoomObjectRecovery.Flush();
        if (PhotonNetwork.InRoom)
        {
            leaving = true; Started("Leaving room...");
            if (!PhotonNetwork.LeaveRoom(false)) RequestFailed("Could not leave the current room.");
        }
        else Connect();
    }
    static bool IsCrewRequest(Request value) => value == Request.CrewSearch || value == Request.CrewDestination || value == Request.CrewFresh;
    public void BeginCrewSearch(string[] users)
    {
        transferUsers = users;
        Begin(Request.CrewSearch);
    }
    public void BeginCrewDestination(bool fresh, string destination, string[] users)
    {
        transferUsers = users; transferKind = RailSessionRules.CompoundKind; transferRun = "";
        Begin(fresh ? Request.CrewFresh : Request.CrewDestination, destination);
    }
    public void CrewReturnStopped(bool arrived)
    {
        if (!arrived)
        {
            FailInput("Crew return could not finish. Check your connection, then use the room code to regroup or choose Leave.");
            SetStatus(LastError);
        }
    }
    public override void OnConnectedToMaster()
    {
        operationPending = false;
        if (!quitting && request != Request.None) ExecuteRequest();
    }
    public override void OnLeftRoom()
    {
        // Realtime also calls OnLeftRoom before OnDisconnected after transport
        // loss. Keep the last room until recovery captures it; only an explicit
        // room transfer/leave should discard the reconnect destination here.
        if (leaving) joinedRoom = null;
        leaving = false; WorldReady = false;
        RoomMembershipChanged?.Invoke();
        // Wait for OnConnectedToMaster before submitting the next operation.
    }
    void ExecuteRequest()
    {
        if (operationPending || PhotonNetwork.InRoom || request == Request.None) return;
        bool queued = false;
        switch (request)
        {
            case Request.CrewSearch:
                Started("Reserving a compound for your crew...");
                queued = PhotonNetwork.JoinRandomRoom(new Hashtable { [RailSessionRules.Kind] = RailSessionRules.CompoundKind,
                    [RailSessionRules.Schema] = RailSessionRules.Version }, MaxPlayers,
                    MatchmakingMode.FillRoom, AutomaticLobby, null, transferUsers);
                break;
            case Request.CrewDestination:
                Started("Joining your crew's compound...");
                queued = PhotonNetwork.JoinRoom(requestedRoom);
                break;
            case Request.CrewFresh:
                Started("Creating a compound for your crew...");
                queued = JoinAllocatedRoom(RailSessionRules.CompoundKind, "", transferUsers);
                break;
#if UNITY_EDITOR
            case Request.EditorQuickStart:
                Started("Joining the Two Ducks test expedition...");
                // Either editor can authenticate first. Photon arbitrates creation;
                // slower clients use the existing late-join/world-ready path.
                queued = JoinAllocatedRoom(RailSessionRules.ExpeditionKind, Guid.NewGuid().ToString("N"), null);
                break;
#endif
            case Request.Random:
                Started("Finding a public compound...");
                queued = PhotonNetwork.JoinRandomRoom(new Hashtable { [RailSessionRules.Kind] = RailSessionRules.CompoundKind, [RailSessionRules.Schema] = RailSessionRules.Version }, MaxPlayers,
                    MatchmakingMode.FillRoom, AutomaticLobby, null);
                break;
            case Request.FreshCompound:
                Started("Creating a compound...");
                if (allocation == null) allocation = Guid.NewGuid().ToString("N");
                requestedRoom = RailSessionRules.RoomCode(allocation, allocationAttempt);
                queued = PhotonNetwork.CreateRoom(requestedRoom, RailSessionRules.Options(RailSessionRules.CompoundKind), AutomaticLobby);
                break;
            case Request.CreatePrivate:
                Started("Creating private room " + requestedRoom + "...");
                queued = PhotonNetwork.CreateRoom(requestedRoom, RailSessionRules.Options(RailSessionRules.PrivateKind));
                break;
            case Request.JoinPrivate:
            case Request.ReturnToCompound:
                Started("Joining " + requestedRoom + "..."); queued = PhotonNetwork.JoinRoom(requestedRoom);
                break;
            case Request.Rejoin:
                Started("Rejoining your crew...");
                queued = Time.realtimeSinceStartupAsDouble < resumeUntil ? PhotonNetwork.RejoinRoom(resumeRoom) : PhotonNetwork.JoinRoom(resumeRoom);
                break;
            case Request.Transfer:
                Started("Travelling with your crew...");
                // Any selected crew member can create it if the old coordinator disappears.
                // Expected users reserve capacity on the Photon server.
                queued = JoinAllocatedRoom(transferKind, transferRun, transferUsers);
                break;
        }
        if (!queued) RequestFailed("The connection is not ready. Please retry.");
    }
    bool JoinAllocatedRoom(string kind, string run, string[] users)
    {
        var options = RailSessionRules.Options(kind, run);
        options.CustomRoomProperties[RailSessionRules.Allocation] = allocation;
        options.CustomRoomProperties[RailSessionRules.TransferUntil] = PhotonNetwork.Time + 30d;
        // Server-side matching by allocation prevents joining an unrelated occupied
        // code, even briefly. Public matchmaking still filters exclusively to compounds.
        // Private rooms remain hidden and are never added to this lobby.
        options.IsVisible = true;
        options.CustomRoomPropertiesForLobby = new[] { RailSessionRules.Kind, RailSessionRules.Schema, RailSessionRules.Allocation };
        return PhotonNetwork.JoinRandomOrCreateRoom(
            new Hashtable { [RailSessionRules.Allocation] = allocation, [RailSessionRules.Kind] = kind, [RailSessionRules.Schema] = RailSessionRules.Version },
            (byte)options.MaxPlayers, MatchmakingMode.FillRoom, AutomaticLobby, roomName: requestedRoom,
            roomOptions: options, expectedUsers: users);
    }
    bool IsAllocatedRequest => request == Request.Transfer || request == Request.CrewFresh
#if UNITY_EDITOR
        || request == Request.EditorQuickStart
#endif
        ;
    bool RetryAllocation()
    {
#if UNITY_EDITOR
        // Quick start uses the requested fixed code, never a generated fallback.
        if (request == Request.EditorQuickStart) return false;
#endif
        if (++allocationAttempt >= 100) return false;
        requestedRoom = RailSessionRules.RoomCode(allocation, allocationAttempt);
        if (PhotonNetwork.InRoom)
        {
            leaving = true; Started("Finding an available room code...");
            PhotonNetwork.LeaveRoom(false);
        }
        else retryAt = Time.realtimeSinceStartupAsDouble + 1;
        return true;
    }
    public override void OnJoinRandomFailed(short code, string message)
    {
        if (IsAllocatedRequest) { OnCreateRoomFailed(code, message); return; }
        if (IsCrewRequest(request)) { CrewRequestFailed(); return; }
        operationPending = false; allocation = null; allocationAttempt = 0; request = Request.FreshCompound; ExecuteRequest();
    }
    public override void OnCreateRoomFailed(short code, string message)
    {
        operationPending = false;
        if ((request == Request.FreshCompound || IsAllocatedRequest) && code == ErrorCode.GameIdAlreadyExists && RetryAllocation()) return;
        if (IsCrewRequest(request)) { CrewRequestFailed(); return; }
        RequestFailed(request == Request.CreatePrivate && code == ErrorCode.GameIdAlreadyExists
            ? "That code is occupied. Choose another code or use Join." : "Could not create the room. Please retry.");
    }
    public override void OnJoinRoomFailed(short code, string message)
    {
        operationPending = false;
        if (IsAllocatedRequest && code == ErrorCode.GameIdAlreadyExists && RetryAllocation()) return;
        if (IsCrewRequest(request)) { CrewRequestFailed(); return; }
        if (request == Request.ReturnToCompound) { request = Request.Random; ExecuteRequest(); return; }
        RequestFailed(code == ErrorCode.GameFull ? "Room is full (including reserved reconnect slots)."
            : code == ErrorCode.GameClosed ? "This expedition is ending. Join after the crew returns."
            : code == ErrorCode.GameDoesNotExist ? "Room not found. Check the code and game version."
            : "Could not join the room. Check your connection and game version.");
    }
    void RequestFailed(string error)
    {
        if (IsCrewRequest(request)) { CrewRequestFailed(); return; }
        operationPending = false; FailInput(error); RecoveringConnection = false; resumeRoom = null;
        if (PhotonNetwork.InRoom)
        {
            request = Request.None; leaving = false; WorldReady = true; ClearConnectPending(); return;
        }
        request = string.IsNullOrEmpty(previousCompound) ? Request.Random : Request.ReturnToCompound;
        requestedRoom = previousCompound; retryAt = Time.realtimeSinceStartupAsDouble + 1;
        SetStatus(error + " Returning to a compound...");
    }
    void CrewRequestFailed()
    {
        operationPending = false; request = Request.None; leaving = false;
        crewReturn?.JoinFailed();
        SetStatus("Securing a shared fallback compound...");
    }
    public override void OnJoinedRoom()
    {
        operationPending = false;
        bool crewRequest = IsCrewRequest(request);
        bool allocatedRequest = request == Request.Transfer || request == Request.CrewFresh;
#if UNITY_EDITOR
        allocatedRequest |= request == Request.EditorQuickStart;
#endif
        // Validate the crew token before enabling world loading or publishing readiness.
        if (allocatedRequest && (!IsRailsRoom || Get(RailSessionRules.Allocation, "") != allocation))
        {
            if (crewRequest) { CrewRequestFailed(); return; }
            if (RetryAllocation()) return;
            request = Request.Random; leaving = true;
            FailInput("Could not allocate a room code. Please retry.");
            Started(LastError); PhotonNetwork.LeaveRoom(false); return;
        }
        if (!IsRailsRoom || !RailSessionRules.IsCode(PhotonNetwork.CurrentRoom.Name) ||
            (crewRequest && Kind != RailSessionRules.CompoundKind) ||
            (request == Request.CreatePrivate && !IsPrivate))
        {
            if (crewRequest) { CrewRequestFailed(); return; }
            FailInput("Incompatible room. Use the same Duck Rails build.");
            request = Request.Random; leaving = true; Started(LastError); PhotonNetwork.LeaveRoom(false); return;
        }
        DuckzAnalytics.SessionJoined(Kind, PhotonNetwork.CurrentRoom.PlayerCount);
        DuckzAnalytics.SessionEvent(request == Request.Rejoin ? "rejoined" : "joined");
        request = Request.None; RecoveringConnection = false; leaving = false;
        joinedRoom = PhotonNetwork.CurrentRoom.Name; followedTransfer = null; pendingRevision = -1;
        loadingSince = Time.realtimeSinceStartupAsDouble; WorldReady = false;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [RailSessionRules.Boarding] = false, [RailSessionRules.Ready] = "" });
        PlayerProps.PushCosmetics(); RoomMembershipChanged?.Invoke();
        if (!GameScenes.IsLoaded(RequiredScene) && !loadingScene) StartCoroutine(LoadWorldScene());
        else SetStatus("Loading current world state...");
        if (crewRequest) crewReturn?.JoinedCompound();
    }
    public static string RequiredScene => OnTrain ? GameScenes.Expedition : GameScenes.Compound;
    IEnumerator LoadWorldScene()
    {
        MarkWorldLoading();
        string destinationScene = RequiredScene;
        loadingScene = true; PhotonNetwork.IsMessageQueueRunning = false; SetStatus("Loading " + destinationScene + "...");
        // Cached room objects may arrive before scene loading. Keep their Photon IDs alive.
        foreach (var view in PhotonNetwork.PhotonViewCollection)
            if (view != null && view.ViewID != 0 && !view.IsSceneView) DontDestroyOnLoad(view.transform.root.gameObject);
        var load = SceneManager.LoadSceneAsync(destinationScene, LoadSceneMode.Single);
        if (load != null) yield return load;
        loadingScene = false; PhotonNetwork.IsMessageQueueRunning = true; loginStatus = null;
        loadingSince = Time.realtimeSinceStartupAsDouble;
        foreach (var player in PhotonPlayer.Active) GameManager.instance?.AddPhotonPlayer(player);
        if (!GameScenes.IsLoaded(destinationScene)) RequestFailed("Could not load " + destinationScene + ".");
    }
    public void MarkWorldReady()
    {
        if (!IsRailsRoom || HasRoomRequest || AwaitingCrewDestination) return;
        if (!WorldReady) DuckzAnalytics.SessionEvent("world_ready");
        WorldReady = true; ClearConnectPending(); SetStatus("Ready");
        string token = RunId + ":" + Phase;
        if (!(PhotonNetwork.LocalPlayer.CustomProperties[RailSessionRules.Ready] is string ready) || ready != token)
            PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [RailSessionRules.Ready] = token });
    }
    public void MarkWorldLoading()
    {
        if (!WorldReady) return;
        WorldReady = false; loadingSince = Time.realtimeSinceStartupAsDouble;
        SetStatus("Preparing current world state...");
        if (IsRailsRoom) PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [RailSessionRules.Ready] = "" });
    }
    public void SetBoarding(bool value)
    {
        if (!IsRailsRoom || !WorldReady || Kind != RailSessionRules.CompoundKind) return;
        if (PhotonNetwork.LocalPlayer.CustomProperties[RailSessionRules.Boarding] is bool old && old == value) return;
        PhotonNetwork.LocalPlayer.SetCustomProperties(new Hashtable { [RailSessionRules.Boarding] = value });
    }
    public bool Commit(Hashtable changes)
    {
        if (!IsRailsRoom || !PhotonNetwork.IsMasterClient) return false;
        if (changes[RailSessionRules.Phase] is int next && !RailSessionRules.CanTransition(Phase, (RailPhase)next)) return false;
        int revision = Get(RailSessionRules.Revision, 0);
        if (pendingRevision == revision && Time.realtimeSinceStartupAsDouble - lastCommit < 1) return false;
        changes[RailSessionRules.Revision] = revision + 1;
        pendingRevision = revision; lastCommit = Time.realtimeSinceStartupAsDouble;
        return PhotonNetwork.CurrentRoom.SetCustomProperties(changes, new Hashtable { [RailSessionRules.Revision] = revision });
    }
    public void StartPrivateRun()
    {
        if (!IsPrivate || Phase != RailPhase.Staging || !WorldReady || !PhotonNetwork.IsMasterClient || RailWorld.HasLiveCargo) return;
        Commit(new Hashtable { [RailSessionRules.Phase] = (int)RailPhase.Departing, [RailSessionRules.Run] = Guid.NewGuid().ToString("N"),
            [RailSessionRules.Deadline] = PhotonNetwork.Time + 30d, [RailSessionRules.Fuel] = 10f, [RailExpedition.StateKey] = "",
            [RailSessionRules.Cargo] = Array.Empty<int>(), [RailSessionRules.Motion] = new object[] { PhotonNetwork.Time, 0f, 0f, 0f } });
    }
    public void FinishRun(bool success)
    {
        if (Phase != RailPhase.Running || !PhotonNetwork.IsMasterClient) return;
        Commit(new Hashtable { [RailSessionRules.Phase] = (int)RailPhase.Ending,
            [RailSessionRules.Deadline] = PhotonNetwork.Time + 5d, ["rails.success"] = success });
    }
    public static float CurrentFuel
    {
        get
        {
            RailSessionRules.EvaluateTrain(Get(RailSessionRules.Motion, new object[] { 0d, 0f, 0f, 0f }),
                Get(RailSessionRules.Fuel, 100f), PhotonNetwork.Time, out _, out _, out float fuel);
            return fuel;
        }
    }
    public void SetTrainMoving(bool moving)
    {
        if (RailExpedition.LocalDown) return;
        if (!IsRailsRoom || Phase != RailPhase.Running || !WorldReady || HasRoomRequest) return;
        if (PhotonNetwork.IsMasterClient) { TickDriveLease(); if (driveLease.Actor == 0) QueueTrainControl(RunId, moving); }
        else PhotonNetwork.RaiseEvent(TrainControlEvent, new object[] { RunId, moving },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, ExitGames.Client.Photon.SendOptions.SendReliable);
    }
    public void OnEvent(ExitGames.Client.Photon.EventData photonEvent)
    {
        if (photonEvent.Code == DriveGripEvent)
        {
            if (photonEvent.CustomData is object[] grip && grip.Length == 4 && grip[0] is string gripRun &&
                grip[1] is string token && grip[2] is int operation && grip[3] is float progress)
                ApplyDriveGrip(photonEvent.Sender, gripRun, token, operation, progress);
            return;
        }
        if (photonEvent.Code != TrainControlEvent || !PhotonNetwork.IsMasterClient || !IsRailsRoom ||
            Phase != RailPhase.Running || HasRoomRequest) return;
        var sender = PhotonNetwork.CurrentRoom.GetPlayer(photonEvent.Sender);
        if (sender == null || sender.IsInactive || RailExpedition.ActorDown(sender.ActorNumber) ||
            !(sender.CustomProperties[RailSessionRules.Ready] is string ready) || ready != RunId + ":" + RailPhase.Running) return;
        if (!(photonEvent.CustomData is object[] data) || data.Length != 2 ||
            !(data[0] is string run) || run != RunId || !(data[1] is bool moving)) return;
        TickDriveLease();
        if (driveLease.Actor == 0) QueueTrainControl(run, moving);
    }
    public void RequestDriveGrip(string run, string token, int operation, float progress)
    {
        if (!IsRailsRoom) return;
        if (PhotonNetwork.IsMasterClient) ApplyDriveGrip(PhotonNetwork.LocalPlayer.ActorNumber, run, token, operation, progress);
        else PhotonNetwork.RaiseEvent(DriveGripEvent, new object[] { run, token, operation, progress },
            new RaiseEventOptions { Receivers = ReceiverGroup.MasterClient }, ExitGames.Client.Photon.SendOptions.SendReliable);
    }
    void ApplyDriveGrip(int actor, string run, string token, int operation, float progress)
    {
        if (!PhotonNetwork.IsMasterClient || !IsRailsRoom || Phase != RailPhase.Running || run != RunId || HasRoomRequest) return;
        TickDriveLease();
        var sender = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        if (sender == null || sender.IsInactive) return;
        if (operation != 2 && (RailExpedition.ActorDown(actor) ||
            !(sender.CustomProperties[RailSessionRules.Ready] is string ready) || ready != RunId + ":" + RailPhase.Running)) return;
        if (!driveLease.Apply(actor, token, operation, progress, PhotonNetwork.Time)) return;
        driveLeaseDirty = true;
        QueueTrainControl(run, driveLease.Progress >= 1f);
    }
    void TickDriveLease()
    {
        if (!PhotonNetwork.IsMasterClient || !IsRailsRoom) return;
        if (driveLeaseRun != RunId)
        {
            bool orphaned = DriveGripActor != 0;
            driveLease.Reset(); driveLeaseRun = RunId; driveLeaseDirty = orphaned;
            if (orphaned && Phase == RailPhase.Running) QueueTrainControl(RunId, false);
        }
        if (driveLease.Actor == 0)
        {
            // A previous master's final property write can arrive after the handoff callback.
            if (DriveGripActor != 0 && !driveLeaseDirty)
            { driveLeaseDirty = true; QueueTrainControl(RunId, false); }
            return;
        }
        var holder = PhotonNetwork.CurrentRoom.GetPlayer(driveLease.Actor);
        if (Phase != RailPhase.Running || holder == null || holder.IsInactive || RailExpedition.ActorDown(driveLease.Actor) ||
            PhotonNetwork.Time >= driveLease.Expires)
        {
            driveLease.Reset(); driveLeaseDirty = true;
            if (Phase == RailPhase.Running) QueueTrainControl(RunId, false);
        }
    }
    void QueueTrainControl(string run, bool moving)
    {
        requestedTrainRun = run;
        requestedTrainMoving = moving;
        FlushTrainControl();
    }
    void FlushTrainControl()
    {
        if (!requestedTrainMoving.HasValue) return;
        if (!IsRailsRoom || !PhotonNetwork.IsMasterClient || HasRoomRequest ||
            Phase != RailPhase.Running || requestedTrainRun != RunId)
        { requestedTrainMoving = null; return; }
        // Keep the latest request until observed in room state. Another commit may
        // be in flight, including an earlier GO followed immediately by STOP.
        if (!WorldReady || (pendingRevision >= 0 && Time.realtimeSinceStartupAsDouble - lastCommit < 1)) return;
        double now = PhotonNetwork.Time;
        float target = requestedTrainMoving.Value && CurrentFuel > 0f ? 4f : 0f;
        var command = Get(RailSessionRules.Motion, new object[] { PhotonNetwork.Time, 0f, 0f, 0f });
        if (command.Length == 4 && command[3] is float currentTarget && currentTarget == target && (!driveLeaseDirty || DriveGripMatches()))
        {
            if (pendingRevision < 0) { requestedTrainMoving = null; driveLeaseDirty = false; }
            return;
        }
        RailSessionRules.EvaluateTrain(command, Get(RailSessionRules.Fuel, 100f), now,
            out float z, out float speed, out float fuel);
        Commit(new Hashtable { [RailSessionRules.Motion] = new object[] { now, z, speed, target },
            [RailSessionRules.Fuel] = fuel,
            [DriveGripProperty] = new object[] { RunId, driveLease.Actor, driveLease.Token, driveLease.Progress } });
    }
    bool DriveGripMatches()
    {
        var state = DriveGripState;
        return (string)state[0] == RunId && (int)state[1] == driveLease.Actor &&
            (string)state[2] == driveLease.Token && (float)state[3] == driveLease.Progress;
    }
    public override void OnRoomPropertiesUpdate(Hashtable changed)
    {
        if (changed.ContainsKey(RailSessionRules.Phase) || changed.ContainsKey(RailSessionRules.Run))
        { WorldReady = false; loadingSince = Time.realtimeSinceStartupAsDouble; }
        if (changed.ContainsKey(RailSessionRules.Phase)) DuckzAnalytics.SessionPhaseChanged();
        if (changed.ContainsKey(RailSessionRules.Revision)) pendingRevision = -1;
        if (!GameScenes.IsLoaded(RequiredScene)) { if (!loadingScene) StartCoroutine(LoadWorldScene()); return; }
        FollowTransfer();
    }
    public override void OnMasterClientSwitched(Player master)
    {
        requestedTrainMoving = null;
        driveLease.Reset(); driveLeaseRun = null;
        pendingRevision = -1; nextAuthorityTick = 0;
        if (master.IsLocal) DuckzAnalytics.SessionEvent("master_changed");
    }
    void FollowTransfer()
    {
        if (HasRoomRequest || !IsRailsRoom) return;
        string destination = Get(RailSessionRules.Destination, "");
        if (destination.Length == 0 || destination == followedTransfer || Get(RailSessionRules.TransferUntil, 0d) < PhotonNetwork.Time) return;
        string[] users = Get(RailSessionRules.TransferUsers, Array.Empty<string>());
        if (!users.Contains(PhotonNetwork.LocalPlayer.UserId)) return;
        followedTransfer = destination;
        if (destination.StartsWith("return-", StringComparison.Ordinal) && Kind == RailSessionRules.ExpeditionKind)
        {
            crewReturn?.Dispose();
            crewReturn = new RailCrewReturn(this, settings, destination, users);
            SetStatus("Keeping your crew together while finding a compound...");
            return;
        }
        transferKind = Kind == RailSessionRules.CompoundKind ? RailSessionRules.ExpeditionKind : RailSessionRules.CompoundKind;
        transferRun = transferKind == RailSessionRules.ExpeditionKind ? destination : ""; transferUsers = users;
        Begin(Request.Transfer, destination);
    }
    void TickAuthority()
    {
        if (!PhotonNetwork.IsMasterClient || !IsRailsRoom || HasRoomRequest) return;
        double now = PhotonNetwork.Time;
        var room = PhotonNetwork.CurrentRoom;
        bool shouldOpen = Phase != RailPhase.Ending && Phase != RailPhase.Returning;
        if (room.IsOpen != shouldOpen) room.IsOpen = shouldOpen;
        TickReservations(room, now);
        if (Kind == RailSessionRules.CompoundKind)
        {
            if (Get(RailSessionRules.Destination, "").Length > 0)
            {
                var users = Get(RailSessionRules.TransferUsers, Array.Empty<string>());
                if (now < Get(RailSessionRules.TransferUntil, 0d) && room.Players.Values.Any(p => !p.IsInactive && users.Contains(p.UserId))) return;
                Commit(new Hashtable { [RailSessionRules.Destination] = "", [RailSessionRules.Roster] = Array.Empty<int>(), [RailSessionRules.Deadline] = 0d }); return;
            }
            var occupants = room.Players.Values.Where(p => !p.IsInactive && p.CustomProperties[RailSessionRules.Boarding] is bool b && b
                && RailWorld.IsActorOnPlatform(p.ActorNumber)).Select(p => p.ActorNumber);
            int[] old = Get(RailSessionRules.Roster, Array.Empty<int>()), roster = RailSessionRules.Admit(old, occupants);
            double deadline = RailSessionRules.Countdown(Get(RailSessionRules.Deadline, 0d), roster.Length, now);
            if (roster.Length > 0 && now >= deadline)
            {
                string[] users = roster.Select(id => room.GetPlayer(id)?.UserId).Where(id => !string.IsNullOrEmpty(id)).Distinct().ToArray();
                if (users.Length != roster.Length) return;
                Commit(new Hashtable { [RailSessionRules.Roster] = roster, [RailSessionRules.Destination] = "run-" + Guid.NewGuid().ToString("N"),
                    [RailSessionRules.TransferUsers] = users, [RailSessionRules.TransferUntil] = now + 30d, [RailSessionRules.Deadline] = 0d });
            }
            else if (!old.SequenceEqual(roster) || deadline != Get(RailSessionRules.Deadline, 0d))
                Commit(new Hashtable { [RailSessionRules.Roster] = roster, [RailSessionRules.Deadline] = deadline });
            return;
        }
        if (Phase == RailPhase.Departing)
        {
            double deadline = Get(RailSessionRules.Deadline, 0d);
            if (deadline == 0d) { Commit(new Hashtable { [RailSessionRules.Deadline] = now + 30d }); return; }
            bool ready = WorldReady && room.Players.Values.Where(p => !p.IsInactive).All(p =>
                p.CustomProperties[RailSessionRules.Ready] is string r && r == RunId + ":" + RailPhase.Departing);
            if (ready || now >= deadline)
                Commit(new Hashtable { [RailSessionRules.Phase] = (int)(WorldReady ? RailPhase.Running : RailPhase.Returning), [RailSessionRules.Deadline] = now + 2d });
        }
        else if (Phase == RailPhase.Ending && now >= Get(RailSessionRules.Deadline, 0d))
            Commit(new Hashtable { [RailSessionRules.Phase] = (int)RailPhase.Returning, [RailSessionRules.Deadline] = now + 2d });
        else if (Phase == RailPhase.Returning && now >= Get(RailSessionRules.Deadline, 0d))
        {
            if (IsPrivate)
            {
                Commit(new Hashtable { [RailSessionRules.Phase] = (int)RailPhase.Staging, [RailSessionRules.Run] = "", [RailSessionRules.Cargo] = Array.Empty<int>() });
            }
            else if (Get(RailSessionRules.Destination, "").Length == 0)
                Commit(new Hashtable { [RailSessionRules.Destination] = "return-" + Guid.NewGuid().ToString("N"),
                    [RailSessionRules.TransferUsers] = room.Players.Values.Where(p => !p.IsInactive).Select(p => p.UserId).ToArray(), [RailSessionRules.TransferUntil] = now + 90d });
        }
    }
    void TickReservations(Room room, double now)
    {
        string[] expected = room.ExpectedUsers ?? Array.Empty<string>();
        var old = Get(RailSessionRules.Reservations, new Hashtable());
        var leases = RailSessionRules.ReservationLeases(expected, old, now);
        string[] keep = expected.Where(user => (double)leases[user] > now).ToArray();
        foreach (string user in expected.Except(keep)) leases.Remove(user);
        if (old.Count == leases.Count && keep.Length == expected.Length && expected.All(user => old.ContainsKey(user))) return;
        // Include the expected-user CAS in the same server operation so a newly
        // arriving crew cannot have its reservations erased by stale cleanup.
        // Room.SetCustomProperties strips byte keys; use the same SetProperties
        // wire operation as the installed SDK's protected room-property helper.
        PhotonNetwork.NetworkingClient.LoadBalancingPeer.SendOperation(OperationCode.SetProperties,
            new System.Collections.Generic.Dictionary<byte, object>
            {
                [ParameterCode.Properties] = new Hashtable { [GamePropertyKey.ExpectedUsers] = keep, [RailSessionRules.Reservations] = leases },
                [ParameterCode.ExpectedValues] = new Hashtable { [GamePropertyKey.ExpectedUsers] = expected },
                [ParameterCode.Broadcast] = true
            }, ExitGames.Client.Photon.SendOptions.SendReliable);
    }
    public override void OnDisconnected(DisconnectCause cause)
    {
        requestedTrainMoving = null;
        driveLease.Reset(); driveLeaseRun = null;
        if (quitting) return;
        operationPending = false; WorldReady = false;
        DuckzAnalytics.SessionDisconnected(cause.ToString());
        if (crewReturn != null && !crewReturn.Finished)
        {
            joinedRoom = null; request = Request.None; leaving = false;
            RecoveringConnection = true;
            if (crewReturn.Active) crewReturn.JoinFailed();
            RoomMembershipChanged?.Invoke(); return;
        }
        if (!string.IsNullOrEmpty(joinedRoom) && !leaving)
        {
            resumeRoom = joinedRoom; resumeUntil = Time.realtimeSinceStartupAsDouble + 120;
            request = Request.Rejoin; joinedRoom = null;
        }
        if (request == Request.None) request = Request.Random;
        leaving = false; RecoveringConnection = true; RetryConnection("Connection lost.");
        RoomMembershipChanged?.Invoke();
    }
    void RetryConnection(string reason)
    {
        operationPending = false; retryAt = Time.realtimeSinceStartupAsDouble + 2;
        SetStatus(reason + " Reconnecting..."); MarkConnectPending();
    }
    void Update()
    {
        if (!initialized || quitting) return;
        crewReturn?.Tick();
        TickDriveLease();
        FlushTrainControl();
        double now = Time.realtimeSinceStartupAsDouble;
        if (operationPending && now > operationUntil && !loadingScene)
        { operationPending = false; PhotonNetwork.Disconnect(); RetryConnection("Connection timed out."); }
        if (!operationPending && request != Request.None && !PhotonNetwork.InRoom && now >= retryAt && !loadingScene) Connect();
        if (!IsRailsRoom || HasRoomRequest) return;
        FollowTransfer();
        if (!WorldReady && now - loadingSince > 30 && Phase == RailPhase.Running)
        { Begin(Request.Random); FailInput("World loading timed out. Returning to a compound."); return; }
        if (now < nextAuthorityTick) return;
        nextAuthorityTick = now + .1; TickAuthority();
    }
}
