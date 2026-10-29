using System;
using System.Collections;
using Game.AI;
using Photon.Pun;
using Hashtable = ExitGames.Client.Photon.Hashtable;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Connects to Photon in the US region and joins an open room, creating one when none have space.
/// Survives the login scene so the room is already joined when Main loads.
/// Menu switches keep one pending request. New rooms use a 4-digit name.
/// </summary>
public class PhotonSession : MonoBehaviourPunCallbacks
{
    public const string Region = "us";
    public const string PlayScene = "Main";
    public const int MaxPlayers = 12;
    public const int RoomCodeLength = 4;
    public const int StreamRate = 20;

    public const string NoOpenRoomsMessage = "No open rooms";
    public const string MissingRoomMessage = "No room with that code";
    public const string RoomFullMessage = "Room is full";

    // PUN stores the synced scene under this room property. See PhotonNetwork.CurrentSceneProperty.
    const string SceneProperty = "curScn";
    const int MaxAttempts = 5;

    public static PhotonSession Instance { get; private set; }

    /// <summary>True from editor play sign-in until the room join finishes or is abandoned. Enemy spawn waits this out.</summary>
    public static bool ConnectPending { get; private set; }

    public static void MarkConnectPending() => ConnectPending = true;

    public static void ClearConnectPending() => ConnectPending = false;

    enum RoomRequest
    {
        None,
        Random,
        Public,
        Private,
        Code,
    }

    Action<string> _status;
    AppSettings _settings;
    int _attempts;
    bool _joined;
    bool _failed;
    bool _holdRoomJoin;
    bool _enteringPlay;
    RoomRequest _request = RoomRequest.None;
    string _requestCode;
    int _codeTries;
    int _lastRoomCode = -1;
    bool _switching;
    bool _requestSent;
    bool _leavingRoom;
    bool _returningToLogin;
    bool _quitting;
    AsyncOperation _sceneOperation;
    Coroutine _retryCoroutine;

    public bool RecoveringConnection { get; private set; }
    public bool ReturningToLogin => _returningToLogin;
    public bool HasRoomRequest => _request != RoomRequest.None;
    public string RoomRequestMessage => _request == RoomRequest.Private
        ? "Creating private room..."
        : _request == RoomRequest.Public ? "Creating room..."
        : _request == RoomRequest.Code ? "Joining room " + _requestCode + "..."
        : "Joining a room...";

    public event Action RoomMembershipChanged;
    public event Action RoomRequestStarted;
    public event Action<string> RoomRequestFailed;

    public static PhotonSession Ensure()
    {
        if (Instance != null)
            return Instance;

        var gameObject = new GameObject(nameof(PhotonSession));
        DontDestroyOnLoad(gameObject);
        return gameObject.AddComponent<PhotonSession>();
    }

    public IEnumerator ConnectAndJoin(Action<string> status)
    {
        if (LoginFlow.Previewing)
        {
            DuckzLog.Log("Login spot preview: not joining a room.");
            status?.Invoke("Login preview.");
            yield break;
        }

        _status = status;
        _joined = false;
        _failed = false;
        _attempts = 0;
        _holdRoomJoin = false;

        Configure();

        if (PhotonNetwork.InRoom)
        {
            HandleJoined();
            yield break;
        }

#if UNITY_EDITOR
        if (GameManager.observePhotonRooms && !HasRoomRequest)
        {
            yield return JoinObservedLobby();
            yield break;
        }
#endif

        Report(HasRoomRequest ? RoomRequestMessage : "Connecting to Photon...");
        if (PhotonNetwork.IsConnectedAndReady)
            RunPendingOrRandom();
        else if (!BeginConnect())
            Retry("Could not start the Photon connection.");

        while (!_joined && !_failed)
            yield return null;
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (!TryGetComponent<DuckPlayerColors>(out _))
            gameObject.AddComponent<DuckPlayerColors>();
        if (!TryGetComponent<PhotonMasterHandoff>(out _))
            gameObject.AddComponent<PhotonMasterHandoff>();
        DontDestroyOnLoad(gameObject);
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnApplicationQuit() => _quitting = true;

    void Configure()
    {
        var source = PhotonNetwork.PhotonServerSettings.AppSettings;
        _settings = source.CopyTo(new AppSettings());
        // Spawn generations and optional cabin snapshots change the wire layout;
        // keep older builds in separate rooms (all peers must understand passenger poses).
        _settings.AppVersion = source.AppVersion + "-room-recovery-v3-cabin-v1";
        _settings.FixedRegion = Region;

        // LoadLevel uses single-scene mode and would unload Quackville_Env. Clients follow the room property instead.
        PhotonNetwork.AutomaticallySyncScene = false;
        PhotonNetwork.SendRate = StreamRate;
        PhotonNetwork.SerializationRate = StreamRate;

        var session = PlayerSession.Instance;
        PhotonNetwork.NickName = session != null && !string.IsNullOrEmpty(session.DisplayName)
            ? session.DisplayName
            : "Duck";

        // A new AuthenticationValues has no Token. After LeaveRoom, Photon must
        // authenticate on the master server, and that only succeeds with the token
        // from the first name-server connect. Keep that object and update the user id.
        if (session != null && !string.IsNullOrEmpty(session.PlayerId))
        {
            string photonUserId = string.IsNullOrEmpty(session.GrId) ? session.PlayerId : session.GrId;
            if (PhotonNetwork.AuthValues == null)
                PhotonNetwork.AuthValues = new AuthenticationValues(photonUserId);
            else if (PhotonNetwork.AuthValues.UserId != photonUserId)
                PhotonNetwork.AuthValues.UserId = photonUserId;

            // Only configure backend credentials before a new connection. Preserve
            // Photon's issued token while moving between rooms/master/game servers.
            if (!PhotonNetwork.IsConnected && session.TryGetAccessToken(out string accessToken))
            {
                PhotonNetwork.AuthValues.AuthType = CustomAuthenticationType.Custom;
                PhotonNetwork.AuthValues.AuthGetParameters = null;
                PhotonNetwork.AuthValues.AddAuthParameter("usr", session.PlayerId);
                PhotonNetwork.AuthValues.AddAuthParameter("pla", session.Platform);
                PhotonNetwork.AuthValues.AddAuthParameter("tkn", accessToken);
            }
        }
    }

    bool BeginConnect()
    {
        if (PhotonNetwork.IsConnected)
            return true;

        return PhotonNetwork.ConnectUsingSettings(_settings, false);
    }

    public void JoinOpenRoom()
    {
        if (PhotonNetwork.InRoom || _switching)
            return;

        _holdRoomJoin = false;
        _joined = false;
        _failed = false;
        _attempts = 0;
        MarkConnectPending();
        Configure();

        if (PhotonNetwork.IsConnectedAndReady)
            RequestRandomRoom();
        else if (!BeginConnect())
        {
            Report("Could not start the Photon connection.", failure: true);
            ClearConnectPending();
        }
    }

    public void LeaveCurrentRoom()
    {
        if (!PhotonNetwork.InRoom || _switching)
            return;

        _holdRoomJoin = true;
        _joined = false;
        _leavingRoom = true;
        ClearConnectPending();
        NotifyRoomMembership();

        if (!PhotonNetwork.LeaveRoom())
        {
            _holdRoomJoin = false;
            _joined = true;
            _leavingRoom = false;
            NotifyRoomMembership();
        }
    }

    public bool HasCurrentRoom(out string code, out bool isPublic)
    {
        code = null;
        isPublic = false;
        if (_leavingRoom || !PhotonNetwork.IsConnected || !PhotonNetwork.InRoom || PhotonNetwork.CurrentRoom == null)
            return false;

        code = PhotonNetwork.CurrentRoom.Name;
        isPublic = PhotonNetwork.CurrentRoom.IsVisible;
        return true;
    }

    public void RequestJoinRandom() => BeginRequest(RoomRequest.Random, null);

    public void RequestCreatePublic() => BeginRequest(RoomRequest.Public, null);

    public void RequestCreatePrivate() => BeginRequest(RoomRequest.Private, null);

    public void RequestJoinCode(string code)
    {
        if (string.IsNullOrEmpty(code) || code.Length != RoomCodeLength)
            return;

        BeginRequest(RoomRequest.Code, code);
    }

    void BeginRequest(RoomRequest request, string code)
    {
        if (_switching || _returningToLogin || _quitting)
            return;

        _switching = true;
        _request = request;
        _requestCode = code;
        _requestSent = false;
        _joined = false;
        _failed = false;
        _holdRoomJoin = true;
        _returningToLogin = true;
        RecoveringConnection = false;
        _status = null;
        if (_retryCoroutine != null)
            StopCoroutine(_retryCoroutine);
        _retryCoroutine = null;
        MarkConnectPending();
        RoomRequestStarted?.Invoke();
        _leavingRoom = PhotonNetwork.InRoom;
        PhotonNetwork.IsMessageQueueRunning = true;
        // Disconnect deliberately so the old room cannot survive the scene transition.
        PhotonNetwork.Disconnect();
        DisablePlayScene();
        NotifyRoomMembership();
        StartCoroutine(ReturnToLogin());
    }

    void RunPendingOrRandom()
    {
        if (HasRoomRequest)
            RunRequest();
        else
            RequestRandomRoom();
    }

    void RunRequest()
    {
        if (_request == RoomRequest.None || _requestSent || _holdRoomJoin || _returningToLogin)
            return;

        _requestSent = true;
        switch (_request)
        {
            case RoomRequest.Random:
                Report("Joining a room...");
                if (!PhotonNetwork.JoinRandomRoom(null, MaxPlayers))
                    FailRoomRequest("Could not join a room.");
                break;
            case RoomRequest.Public:
                _codeTries = 0;
                CreateRoomWithCode(true);
                break;
            case RoomRequest.Private:
                _codeTries = 0;
                CreateRoomWithCode(false);
                break;
            case RoomRequest.Code:
                Report(RoomRequestMessage);
                if (!PhotonNetwork.JoinRoom(_requestCode))
                    FailRoomRequest("Could not join room " + _requestCode + ".");
                break;
        }
    }

    void FailRoomRequest(string message)
    {
        _requestSent = false;
        _leavingRoom = false;
        DuckzAnalytics.SessionJoinFailed(message);
        RoomRequestFailed?.Invoke(message);
        NotifyRoomMembership();
        Retry(message);
    }

    void NotifyRoomMembership() => RoomMembershipChanged?.Invoke();

    void RequestRandomRoom()
    {
        Report("Joining a room...");
        if (!PhotonNetwork.JoinRandomRoom(null, MaxPlayers))
            Retry("Could not join a room.");
    }

    void CreateOpenRoom()
    {
        _codeTries = 0;
        CreateRoomWithCode(true);
    }

    void CreateRoomWithCode(bool visible)
    {
        Report(visible ? "Creating room..." : "Creating private room...");
        var options = new RoomOptions
        {
            MaxPlayers = MaxPlayers,
            IsOpen = true,
            IsVisible = visible,
            PublishUserId = true,
            // Per-spawn recovery keys must disappear on destruction, not accumulate null entries.
            DeleteNullProperties = true,
        };
        if (PhotonNetwork.CreateRoom(NewRoomCode(), options))
            return;

        if (_request == RoomRequest.Random || _request == RoomRequest.Public || _request == RoomRequest.Private)
            FailRoomRequest("Could not create a room.");
        else
            Retry("Could not create a room.");
    }

    string NewRoomCode()
    {
        int code = UnityEngine.Random.Range(0, 10000);
        if (code == _lastRoomCode)
            code = (code + 1) % 10000;

        _lastRoomCode = code;
        return code.ToString("D4");
    }

    public override void OnConnectedToMaster()
    {
        if (_returningToLogin || _quitting || _holdRoomJoin || _retryCoroutine != null)
            return;
        if (_request != RoomRequest.None)
        {
            RunRequest();
            return;
        }

#if UNITY_EDITOR
        // Editor room list stays on the master, including after Leave room.
        if (GameManager.observePhotonRooms)
        {
            if (!PhotonNetwork.InLobby)
                PhotonNetwork.JoinLobby();
            return;
        }
#endif

        if (_joined || _holdRoomJoin)
            return;

        RequestRandomRoom();
    }

    public override void OnJoinedRoom()
    {
        if (_returningToLogin || _holdRoomJoin)
            return;
        HandleJoined();
    }

    public override void OnLeftRoom()
    {
        _leavingRoom = false;
        NotifyRoomMembership();
    }

    void HandleJoined()
    {
        string joinKind = _request == RoomRequest.Private ? "private"
            : _request == RoomRequest.Public ? "public"
            : _request == RoomRequest.Code ? "code"
            : _request == RoomRequest.Random ? "random"
            : PhotonNetwork.CurrentRoom != null && PhotonNetwork.CurrentRoom.IsVisible ? "public" : "private";
        int players = PhotonNetwork.CurrentRoom != null ? PhotonNetwork.CurrentRoom.PlayerCount : 0;
        _attempts = 0;
        if (_retryCoroutine != null)
            StopCoroutine(_retryCoroutine);
        _retryCoroutine = null;
        _request = RoomRequest.None;
        _requestCode = null;
        _requestSent = false;
        _switching = false;
        _leavingRoom = false;
        _holdRoomJoin = false;
        _joined = true;
        PlayerProps.PushCosmetics();
        ClearConnectPending();
        EnemyManager.ReleaseLocalSpawns();
        DuckzAnalytics.SessionJoined(joinKind, players);
        Report("Joined a room.");
        NotifyRoomMembership();

        if (InPlayScene())
        {
            PublishPlayScene();
            return;
        }

        if (PhotonNetwork.IsMasterClient)
            PublishPlayScene();

        if (PhotonNetwork.IsMasterClient || RoomIsPlayScene())
            EnterPlayScene();
    }

    public override void OnRoomPropertiesUpdate(Hashtable propertiesThatChanged)
    {
        if (!_joined || _holdRoomJoin || propertiesThatChanged == null || !propertiesThatChanged.ContainsKey(SceneProperty))
            return;

        if (propertiesThatChanged[SceneProperty] is string scene && scene == PlayScene)
            EnterPlayScene();
    }

    bool RoomIsPlayScene()
    {
        return PhotonNetwork.CurrentRoom != null
            && PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SceneProperty, out var existing)
            && existing is string scene
            && scene == PlayScene;
    }

    bool InPlayScene() => GameScenes.IsLoaded(PlayScene) && !GameScenes.IsLoaded(GameScenes.Login);

    void EnterPlayScene()
    {
        if (_returningToLogin || _quitting || LoginFlow.Previewing || _enteringPlay || InPlayScene())
            return;

        _enteringPlay = true;
        StartCoroutine(LoadPlayScene());
    }

    IEnumerator LoadPlayScene()
    {
        PhotonNetwork.IsMessageQueueRunning = false;
        _status = null;

        yield return GameScenes.EnsureEnvironment();
        if (_returningToLogin)
        {
            _enteringPlay = false;
            yield break;
        }

        // Login and DuckPlayer each own an OVRManager. Unload Login first:
        // otherwise Main's manager destroys itself as a duplicate, and unloading
        // Login afterwards leaves the VR rig without a tracking/input manager.
        Scene login = SceneManager.GetSceneByName(GameScenes.Login);
        if (login.isLoaded)
        {
            AsyncOperation unload = _sceneOperation = SceneManager.UnloadSceneAsync(login);
            while (unload != null && !unload.isDone)
                yield return null;
        }

        if (_returningToLogin)
        {
            _enteringPlay = false;
            yield break;
        }
        Scene play = SceneManager.GetSceneByName(PlayScene);
        if (!play.isLoaded)
        {
            AsyncOperation load = _sceneOperation = SceneManager.LoadSceneAsync(PlayScene, LoadSceneMode.Additive);
            while (load != null && !load.isDone)
                yield return null;
            play = SceneManager.GetSceneByName(PlayScene);
        }

        if (!play.IsValid() || !play.isLoaded)
        {
            {
                if (DuckzLog.AllowError(out var logNotice1))
                    logNotice1.Write("Could not load " + PlayScene + ".");
            }
            PhotonNetwork.IsMessageQueueRunning = true;
            _enteringPlay = false;
            yield break;
        }

        GameScenes.ActivateEnvironment();

        _sceneOperation = null;
        if (!_returningToLogin)
            RecoveringConnection = false;
        PhotonNetwork.IsMessageQueueRunning = true;
        _enteringPlay = false;
    }

    void PublishPlayScene()
    {
        if (!PhotonNetwork.IsMasterClient || PhotonNetwork.CurrentRoom == null)
            return;

        if (PhotonNetwork.CurrentRoom.CustomProperties.TryGetValue(SceneProperty, out var existing)
            && existing is string scene
            && scene == PlayScene)
            return;

        PhotonNetwork.CurrentRoom.SetCustomProperties(new Hashtable { { SceneProperty, PlayScene } });
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        if (DuckzLog.AllowWarning(out var logNotice2))
            logNotice2.Write($"Photon random-room failure ({returnCode}): {message}");
        if (_joined || _returningToLogin || _holdRoomJoin)
            return;

        if (_request == RoomRequest.Random)
        {
            CreateOpenRoom();
            return;
        }

        if (_request != RoomRequest.None)
            return;

        CreateOpenRoom();
    }

    public override void OnJoinRoomFailed(short returnCode, string message)
    {
        if (DuckzLog.AllowWarning(out var logNotice3))
            logNotice3.Write($"Photon room-join failure ({returnCode}): {message}");
        if (_joined || _returningToLogin || _holdRoomJoin)
            return;

        if (_request == RoomRequest.Code)
        {
            if (returnCode == ErrorCode.GameFull || returnCode == ErrorCode.GameDoesNotExist
                || returnCode == ErrorCode.GameClosed)
            {
                string reason = "Room " + _requestCode
                    + (returnCode == ErrorCode.GameFull ? " is full."
                    : returnCode == ErrorCode.GameClosed ? " is closed." : " not found.");
                _request = RoomRequest.Random;
                _requestCode = null;
                FailRoomRequest(reason + " Joining another room...");
            }
            else
                FailRoomRequest("Could not join room " + _requestCode + ".");
            return;
        }

        if (_request != RoomRequest.None)
        {
            FailRoomRequest(MissingRoomMessage);
            return;
        }

        if (returnCode == ErrorCode.GameFull)
        {
            CreateOpenRoom();
            return;
        }

        Retry("Could not join a room.");
    }

    public override void OnCreateRoomFailed(short returnCode, string message)
    {
        if (DuckzLog.AllowWarning(out var logNotice4))
            logNotice4.Write($"Photon room-creation failure ({returnCode}): {message}");
        if (_joined || _returningToLogin || _holdRoomJoin)
            return;

        if (returnCode == ErrorCode.GameIdAlreadyExists && _codeTries + 1 < MaxAttempts)
        {
            _codeTries++;
            CreateRoomWithCode(_request != RoomRequest.Private);
            return;
        }

        if (_request == RoomRequest.Public || _request == RoomRequest.Private)
        {
            FailRoomRequest("Could not create a room.");
            return;
        }

        if (_request != RoomRequest.None)
        {
            FailRoomRequest("Could not create a room.");
            return;
        }

        Retry("Could not create a room.");
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        if (DuckzLog.AllowWarning(out var logNotice5))
            logNotice5.Write("Photon disconnected: " + cause);
        if (_quitting || cause == DisconnectCause.ApplicationQuit || _returningToLogin)
            return;

        if (_joined || _enteringPlay || GameScenes.IsLoaded(PlayScene))
        {
            BeginLoginRecovery();
            return;
        }

        _leavingRoom = false;
        if (_holdRoomJoin)
            return;
        if (_request != RoomRequest.None)
        {
            FailRoomRequest("Disconnected from Photon.");
            return;
        }

        NotifyRoomMembership();

        if (_joined || _failed || _holdRoomJoin)
            return;

        Retry("Disconnected from Photon.");
    }

    void BeginLoginRecovery()
    {
        _returningToLogin = true;
        RecoveringConnection = true;
        if (_retryCoroutine != null)
            StopCoroutine(_retryCoroutine);
        _retryCoroutine = null;
        _status = null;
        _joined = false;
        _failed = false;
        _holdRoomJoin = true;
        _request = RoomRequest.None;
        _requestCode = null;
        _requestSent = false;
        _switching = false;
        _leavingRoom = false;
        _attempts = 0;
        ClearConnectPending();
        PhotonNetwork.IsMessageQueueRunning = true;
        DisablePlayScene();
        NotifyRoomMembership();
        StartCoroutine(ReturnToLogin());
    }

    static void DisablePlayScene()
    {
        Scene play = SceneManager.GetSceneByName(PlayScene);
        if (play.isLoaded)
            foreach (GameObject root in play.GetRootGameObjects())
                root.SetActive(false);
    }

    IEnumerator ReturnToLogin()
    {
        // Unity scene operations cannot be cancelled. Drain any interrupted load
        // before unloading Main, so it cannot finish behind the new login rig.
        while (_enteringPlay || (_sceneOperation != null && !_sceneOperation.isDone))
            yield return null;
        _sceneOperation = null;
        if (_switching)
        {
            while (PhotonNetwork.IsConnected)
                yield return null;
            _leavingRoom = false;
        }
        DisablePlayScene();
        yield return GameScenes.EnsureEnvironment();
        if (GameScenes.IsLoaded(PlayScene))
            yield return SceneManager.UnloadSceneAsync(PlayScene);

        // Unload the gameplay OVRManager before creating Login's OVRManager.
        // A disconnect before Main starts can leave an old LoginFlow running.
        if (GameScenes.IsLoaded(GameScenes.Login))
            yield return SceneManager.UnloadSceneAsync(GameScenes.Login);
        yield return SceneManager.LoadSceneAsync(GameScenes.Login, LoadSceneMode.Additive);
        GameScenes.ActivateEnvironment();
        _returningToLogin = false;
    }

    void Retry(string reason)
    {
        // One pending retry per failure, even if Photon emits multiple callbacks.
        if (_retryCoroutine != null || _returningToLogin || _quitting)
            return;

        // Saturate the counter so an extended outage cannot overflow it.
        _attempts = Mathf.Min(_attempts + 1, 7);
        int delay = _attempts <= 5 ? 2 : _attempts == 6 ? 4 : 8;
        _retryCoroutine = StartCoroutine(RetryConnect(reason, delay));
    }

    IEnumerator RetryConnect(string reason, int delay)
    {
        for (int remaining = delay; remaining > 0; remaining--)
        {
            // Keep the failure visible throughout the existing retry wait.
            Report(reason + " Retrying in " + remaining + " seconds...",
                ShortFailure(reason) + (reason.Contains(" Joining another room...")
                    ? " Joining another room..." : " Retrying..."));
            yield return new WaitForSecondsRealtime(1f);
        }
        _retryCoroutine = null;
        if (_joined || _failed || _holdRoomJoin)
            yield break;

        Report(HasRoomRequest ? RoomRequestMessage : "Connecting to Photon...");
#if UNITY_EDITOR
        if (GameManager.observePhotonRooms && !HasRoomRequest)
        {
            if (PhotonNetwork.IsConnectedAndReady)
            {
                if (!PhotonNetwork.InLobby)
                    PhotonNetwork.JoinLobby();
            }
            else if (!PhotonNetwork.IsConnected && !BeginConnect())
                Retry("Could not start the Photon connection.");

            yield break;
        }
#endif

        if (PhotonNetwork.IsConnectedAndReady)
            RunPendingOrRandom();
        else if (!PhotonNetwork.IsConnected && !BeginConnect())
            Retry("Could not start the Photon connection.");
    }

#if UNITY_EDITOR
    IEnumerator JoinObservedLobby()
    {
        if (PhotonNetwork.InLobby)
        {
            ClearConnectPending();
            Report("In the lobby.");
            yield break;
        }

        Report("Connecting to Photon...");
        if (PhotonNetwork.IsConnectedAndReady)
        {
            if (!PhotonNetwork.JoinLobby())
            {
                Report("Could not join the lobby.", failure: true);
                ClearConnectPending();
                yield break;
            }
        }
        else if (!BeginConnect())
        {
            Report("Could not start the Photon connection.", failure: true);
            ClearConnectPending();
            yield break;
        }

        while (!PhotonNetwork.InLobby && !_failed)
            yield return null;

        if (PhotonNetwork.InLobby)
        {
            ClearConnectPending();
            Report("In the lobby.");
        }
    }
#endif

    static string ShortFailure(string reason)
    {
        if (reason.Contains(" not found.")) return "Room not found.";
        if (reason.Contains(" is full.")) return "Room is full.";
        if (reason.Contains(" is closed.")) return "Room is closed.";
        if (reason == MissingRoomMessage) return "Room not found.";
        if (reason == "Could not create a room.") return "Couldn't create room.";
        if (reason.StartsWith("Could not join")) return "Couldn't join room.";
        return "Connection failed.";
    }

    void Report(string message, string displayMessage = null, bool failure = false)
    {
        if (failure)
        {
            if (DuckzLog.AllowWarning(out var logNotice6, message))
                logNotice6.Write(message);
        }
        else DuckzLog.Log(message);
        if (displayMessage == null)
        {
            switch (message)
            {
                // Network steps remain detailed in logs, but share one visible phase.
                case "Connecting to Photon...":
                case "Joining a room...":
                case "Creating room...":
                case "Creating private room...":
                    displayMessage = RecoveringConnection ? "Reconnecting..." : RoomRequestMessage;
                    break;
                case "Joined a room.": return;
                case "Could not start the Photon connection.": displayMessage = "Connection failed."; break;
                case "Could not join the lobby.": displayMessage = "Couldn't join lobby."; break;
                default: displayMessage = message; break;
            }
        }
        _status?.Invoke(displayMessage);
    }
}
