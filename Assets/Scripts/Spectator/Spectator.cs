#if UNITY_EDITOR || DUCKZ_SPECTATOR
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;
using NetPlayer = Photon.Realtime.Player;

/// <summary>
/// PC spectator: an invisible free camera in a live Duck Rails room (compound, public train or private
/// code). Compiled only into the editor and the DUCKZ_SPECTATOR Windows build. No duck, no microphone,
/// no RPCs or PhotonViews. Peers leave it out of the crew through RailSpectators; it never creates a
/// room and hands master on. Ported from Duckz 491b6f2, fcdff53 and a6e6a40 (docs/duckz-ports.md).
/// </summary>
public static class Spectator
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (!SpectatorMode.Active)
            return;
#if !UNITY_EDITOR
        // The Standalone XR settings start OpenXR for VR play; the spectator renders to the monitor.
        StopXr();
#endif
        Application.runInBackground = true;
        var root = new GameObject(nameof(Spectator));
        Object.DontDestroyOnLoad(root);
        root.AddComponent<SpectatorOverlay>();

        var view = new GameObject("SpectatorCamera");
        Object.DontDestroyOnLoad(view);
        view.SetActive(false);
        view.tag = "MainCamera";
        view.AddComponent<Camera>();
        view.AddComponent<AudioListener>();
        SpectatorCamera.Instance = view.AddComponent<SpectatorCamera>();

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    /// <summary>Login has its own camera and listener: hide the spectator view when no gameplay scene is left.</summary>
    static void OnSceneUnloaded(Scene scene)
    {
        if (!IsGameplayScene(scene) || SpectatorCamera.Instance == null)
            return;
        if (!GameScenes.IsLoaded(GameScenes.Compound) && !GameScenes.IsLoaded(GameScenes.Expedition))
            SpectatorCamera.Instance.gameObject.SetActive(false);
    }

    static void StopXr()
    {
        XRManagerSettings manager = XRGeneralSettings.Instance != null ? XRGeneralSettings.Instance.Manager : null;
        if (manager == null || !manager.isInitializationComplete)
            return;
        manager.StopSubsystems();
        manager.DeinitializeLoader();
    }

    internal static bool IsGameplayScene(Scene scene) =>
        scene.name == GameScenes.Compound || scene.name == GameScenes.Expedition;

    /// <summary>
    /// The gameplay scenes author the local duck rig. It stays as the local player (RailWorld and other
    /// local systems expect one), but is deactivated before its Start: no input, body, colliders, hands,
    /// camera or listener. Its singletons stay set. Block visibility then follows Camera.main, which is
    /// the spectator camera.
    /// </summary>
    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsGameplayScene(scene))
            return;
        Camera rigCamera = null;
        foreach (NewPlayer rig in Object.FindObjectsByType<NewPlayer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (rig.gameObject.scene != scene)
                continue;
            if (rigCamera == null)
                foreach (Camera camera in rig.GetComponentsInChildren<Camera>(true))
                    if (rigCamera == null || camera.CompareTag("MainCamera")) rigCamera = camera;
            rig.gameObject.SetActive(false);
        }
        if (SpectatorCamera.Instance != null)
            SpectatorCamera.Instance.EnterScene(scene, rigCamera);
    }
}

/// <summary>L turns fog off for a clear view of the whole route, and back on.</summary>
static class SpectatorView
{
    internal static bool FogOff { get; private set; }
    static bool _savedFog;

    internal static void Toggle()
    {
        FogOff = !FogOff;
        if (FogOff)
        {
            _savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
        }
        else
            RenderSettings.fog = _savedFog;
    }

    /// <summary>Scene loads and the route atmosphere bring fog back; keep it off while toggled off.</summary>
    internal static void Enforce()
    {
        if (!FogOff || !RenderSettings.fog)
            return;
        _savedFog = true;
        RenderSettings.fog = false;
    }
}

/// <summary>
/// Free fly by default. F orbits the current player (mouse orbits, scroll zooms); Tab switches players.
/// R rides the train: free fly continues in the train's space so the camera keeps up with it.
/// Mouse looks while captured. Esc frees the cursor; left-click captures it again.
/// </summary>
[DefaultExecutionOrder(32000)]
sealed class SpectatorCamera : MonoBehaviour
{
    internal static SpectatorCamera Instance;

    const float LookSensitivity = 2f;
    const float FastMultiplier = 4f;
    const float MinSpeed = 1f;
    const float MaxSpeed = 120f;
    const float MinDistance = 1.5f;
    const float MaxDistance = 25f;
    const float FocusSharpness = 8f;   // higher follows tighter; lower smooths network jitter more
    const float WallPadding = 0.2f;

    internal static float Speed = 8f;
    internal static float Distance = 4f;
    internal static string FollowingName { get; private set; }
    internal static string Notice { get; private set; }
    internal static bool RidingTrain { get; private set; }

    Camera _camera;
    float _yaw, _pitch;
    int _index = -1;
    int _targetActor;
    Vector3 _focus;
    float _shownDistance;
    // The train while the target (or the free camera) rides it. Smoothing in its local space keeps the
    // camera with the moving train instead of trailing speed / sharpness metres behind it.
    Transform _platform;
    Vector3 _focusLocal;
    bool _hasTrainPose;
    Vector3 _trainPosition;
    Quaternion _trainRotation;
    bool _placed;
    string _placedFor;
    readonly List<int> _targets = new List<int>(RailSessionRules.CrewCapacity);
    readonly RaycastHit[] _hits = new RaycastHit[16];

    bool Following => _targetActor != 0;

    /// <summary>Copies the rig camera's rendering setup and shows the spectator view in this scene.</summary>
    internal void EnterScene(Scene scene, Camera rigCamera)
    {
        // Created inactive in Boot, so Awake has not run the first time this is called.
        if (_camera == null) _camera = GetComponent<Camera>();
        if (rigCamera != null)
        {
            _camera.CopyFrom(rigCamera);
            var source = rigCamera.GetUniversalAdditionalCameraData();
            var target = _camera.GetUniversalAdditionalCameraData();
            if (source != null && target != null)
            {
                target.renderPostProcessing = source.renderPostProcessing;
                target.antialiasing = source.antialiasing;
                target.renderShadows = source.renderShadows;
                target.requiresDepthTexture = source.requiresDepthTexture;
                target.requiresColorTexture = source.requiresColorTexture;
            }
        }
        // URP ignores stereoTargetEye (and warns when it is set); XR is stopped, so this renders to the monitor.
        _camera.targetTexture = null;
        _camera.fieldOfView = 70f;
        _camera.rect = new Rect(0f, 0f, 1f, 1f);
        gameObject.tag = "MainCamera";
        _placed = false;
        StopFollowing(null);
        gameObject.SetActive(true);
        SetCaptured(true);
    }

    void OnEnable() => SetCaptured(true);
    void OnDisable() => SetCaptured(false);

    static bool Captured => Cursor.lockState == CursorLockMode.Locked;
    static bool Shift => RailDesktopInput.GetKey(KeyCode.LeftShift) || RailDesktopInput.GetKey(KeyCode.RightShift);

    static void SetCaptured(bool captured)
    {
        Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !captured;
    }

    static RailTrain Train => PhotonSession.OnTrain && RailTrain.Instance != null && RailTrain.Instance.isActiveAndEnabled
        ? RailTrain.Instance : null;

    /// <summary>The moving part of the train: the cabin RailTrain drives each frame.</summary>
    static Transform TrainFrame(RailTrain train) => train.cabin != null ? train.cabin.transform : train.transform;

    void Update()
    {
        PlaceWhenWorldReady();

        // Esc frees the cursor for the panel; a left click outside the panel captures it again.
        if (RailDesktopInput.GetKeyDown(KeyCode.Escape))
            SetCaptured(false);
        else if (!Captured && RailDesktopInput.GetMouseButtonDown(0) && !SpectatorOverlay.ContainsMouse())
            SetCaptured(true);

        if (Captured)
        {
            _yaw += RailDesktopInput.GetAxisRaw("Mouse X") * LookSensitivity;
            _pitch = Mathf.Clamp(_pitch - RailDesktopInput.GetAxisRaw("Mouse Y") * LookSensitivity, -89f, 89f);
            float scroll = RailDesktopInput.mouseScrollDelta.y;
            if (scroll != 0f)
            {
                if (Following)
                    Distance = Mathf.Clamp(Distance * Mathf.Pow(1.2f, -scroll), MinDistance, MaxDistance);
                else
                    Speed = Mathf.Clamp(Speed * Mathf.Pow(1.2f, scroll), MinSpeed, MaxSpeed);
            }
        }

        if (Following && Focus(_targetActor) == null)
            StopFollowing("Lost " + FollowingName + " — back to free fly.");

        if (!SpectatorOverlay.Typing)
        {
            if (RailDesktopInput.GetKeyDown(KeyCode.F))
            {
                if (Following) StopFollowing(null);
                else StartFollowing();
            }
            if (RailDesktopInput.GetKeyDown(KeyCode.Tab))
                NextPlayer(Shift ? -1 : 1);
            if (RailDesktopInput.GetKeyDown(KeyCode.R))
                SetRiding(!RidingTrain);
        }

        CarryWithTrain();
        transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        if (SpectatorOverlay.Typing)
            return;

        Vector3 move = Vector3.zero;
        if (RailDesktopInput.GetKey(KeyCode.W)) move += transform.forward;
        if (RailDesktopInput.GetKey(KeyCode.S)) move -= transform.forward;
        if (RailDesktopInput.GetKey(KeyCode.D)) move += transform.right;
        if (RailDesktopInput.GetKey(KeyCode.A)) move -= transform.right;
        if (RailDesktopInput.GetKey(KeyCode.E)) move += Vector3.up;
        if (RailDesktopInput.GetKey(KeyCode.Q)) move -= Vector3.up;
        if (move == Vector3.zero)
            return;
        // Flying breaks the follow and continues from where the camera is.
        if (Following)
            StopFollowing(null);
        float speed = Speed * (Shift ? FastMultiplier : 1f);
        transform.position += Vector3.ClampMagnitude(move, 1f) * speed * Time.unscaledDeltaTime;
    }

    /// <summary>First view once the world is ready: beside the train aboard, else where the duck would stand.</summary>
    void PlaceWhenWorldReady()
    {
        var session = PhotonSession.Instance;
        string key = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.Name + ":" + PhotonSession.RunId + ":" + PhotonSession.OnTrain : null;
        if (key != _placedFor) { _placed = false; _placedFor = key; }
        if (_placed || key == null || session == null || !session.WorldReady)
            return;
        _placed = true;
        RailTrain train = Train;
        if (train != null)
        {
            Transform cabin = TrainFrame(train);
            Vector3 eye = cabin.TransformPoint(new Vector3(0f, 5f, -14f));
            Vector3 look = cabin.TransformPoint(new Vector3(0f, 1.5f, 0f)) - eye;
            SetView(eye, Quaternion.LookRotation(look, Vector3.up));
            SetRiding(true);
            return;
        }
        SetRiding(false);
        var rig = GR.Player.instance;
        if (rig != null)
            SetView(rig.transform.position + Vector3.up * 1.7f, Quaternion.Euler(10f, rig.transform.eulerAngles.y, 0f));
    }

    void SetView(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        Vector3 euler = rotation.eulerAngles;
        _yaw = euler.y;
        _pitch = euler.x > 180f ? euler.x - 360f : euler.x;
    }

    void SetRiding(bool riding)
    {
        RailTrain train = Train;
        RidingTrain = riding && train != null;
        _hasTrainPose = false;
        if (riding && train == null)
            Notice = "No train to ride here.";
    }

    /// <summary>While riding, the free camera keeps its place and heading in the train's space.</summary>
    void CarryWithTrain()
    {
        RailTrain train = Train;
        if (!RidingTrain || train == null || Following)
        {
            _hasTrainPose = false;
            if (RidingTrain && train == null) RidingTrain = false;
            return;
        }
        Transform frame = TrainFrame(train);
        if (_hasTrainPose)
        {
            Vector3 local = Quaternion.Inverse(_trainRotation) * (transform.position - _trainPosition);
            transform.position = frame.position + frame.rotation * local;
            _yaw += Mathf.DeltaAngle(_trainRotation.eulerAngles.y, frame.rotation.eulerAngles.y);
        }
        _trainPosition = frame.position;
        _trainRotation = frame.rotation;
        _hasTrainPose = true;
    }

    void LateUpdate()
    {
        SpectatorView.Enforce();
        if (!Following)
            return;
        Vector3? found = Focus(_targetActor);
        if (found == null)
            return;
        Vector3 head = found.Value;

        // Remote poses arrive a few times a second; smoothing the focus hides the steps. Aboard, the
        // smoothing runs in the train's space so the orbit stays with it.
        float blend = 1f - Mathf.Exp(-FocusSharpness * Time.unscaledDeltaTime);
        RailTrain train = Train;
        Transform platform = train != null && train.ContainsPadded(head) ? TrainFrame(train) : null;
        if (platform != _platform)
        {
            _platform = platform;
            if (platform != null)
                _focusLocal = platform.InverseTransformPoint(_focus);
        }
        if (_platform != null)
        {
            _focusLocal = Vector3.Lerp(_focusLocal, _platform.InverseTransformPoint(head), blend);
            _focus = _platform.TransformPoint(_focusLocal);
        }
        else
            _focus = Vector3.Lerp(_focus, head, blend);

        Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        Vector3 back = rotation * Vector3.back;
        float allowed = Mathf.Min(Distance, ClearDistance(_focus, back, Distance));
        // Pull in at once when a wall cuts in; ease back out when it clears.
        _shownDistance = allowed < _shownDistance ? allowed : Mathf.Lerp(_shownDistance, allowed, blend);
        transform.SetPositionAndRotation(_focus + back * _shownDistance, rotation);
    }

    void StartFollowing()
    {
        CollectTargets();
        if (_targets.Count == 0)
        {
            Notice = "No players to follow.";
            return;
        }
        // Keep the last Tab target if it is still here; otherwise take the nearest player.
        int pick = _index >= 0 && _index < _targets.Count ? _index : Nearest();
        Follow(pick, keepAngle: true);
    }

    void NextPlayer(int step)
    {
        CollectTargets();
        if (_targets.Count == 0)
        {
            Notice = "No players to follow.";
            return;
        }
        int current = Following ? _targets.IndexOf(_targetActor) : _index;
        Follow(((current + step) % _targets.Count + _targets.Count) % _targets.Count, keepAngle: Following);
    }

    void Follow(int index, bool keepAngle)
    {
        _index = index;
        _targetActor = _targets[index];
        FollowingName = NameOf(_targetActor);
        Notice = null;
        _focus = Focus(_targetActor) ?? transform.position;
        _platform = null;
        _shownDistance = Distance;
        if (!keepAngle)
            _pitch = 15f; // Coming from free fly with Tab: start level-ish behind the current view direction.
    }

    void StopFollowing(string notice)
    {
        _targetActor = 0;
        FollowingName = null;
        Notice = notice;
        _hasTrainPose = false;
        // Free fly continues from the orbit position and view.
    }

    /// <summary>Crew members (never spectators) whose duck is present, in actor order.</summary>
    void CollectTargets()
    {
        _targets.Clear();
        foreach (NetPlayer player in PhotonNetwork.PlayerListOthers)
            if (RailSpectators.IsLiveCrew(player) && PhotonPlayer.ForActor(player.ActorNumber) != null)
                _targets.Add(player.ActorNumber);
    }

    int Nearest()
    {
        int best = 0;
        float bestSqr = float.MaxValue;
        for (int i = 0; i < _targets.Count; i++)
        {
            Vector3? focus = Focus(_targets[i]);
            if (focus == null) continue;
            float sqr = (focus.Value - transform.position).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = i; }
        }
        return best;
    }

    /// <summary>The synced head of that actor's duck; steadier than avatar bounds, which shift as hands reach out.</summary>
    static Vector3? Focus(int actor)
    {
        if (actor == 0 || !PhotonNetwork.InRoom) return null;
        NetPlayer player = PhotonNetwork.CurrentRoom.GetPlayer(actor);
        if (!RailSpectators.IsLiveCrew(player)) return null;
        PhotonPlayer duck = PhotonPlayer.ForActor(actor);
        if (duck == null || !duck.isActiveAndEnabled) return null;
        duck.TryGetHeadPosition(out Vector3 head);
        return head;
    }

    static string NameOf(int actor)
    {
        NetPlayer player = PhotonNetwork.InRoom ? PhotonNetwork.CurrentRoom.GetPlayer(actor) : null;
        return player != null && !string.IsNullOrEmpty(player.NickName) ? player.NickName : "Duck #" + actor;
    }

    /// <summary>Distance from the focus toward the camera before level geometry blocks it.</summary>
    float ClearDistance(Vector3 focus, Vector3 direction, float distance)
    {
        int count = Physics.SphereCastNonAlloc(focus, WallPadding, direction, _hits, distance,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        float nearest = distance;
        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _hits[i];
            // Only static level geometry: skip ducks, enemies, items and anything that moves.
            if (hit.rigidbody != null || hit.collider is CharacterController)
                continue;
            if (hit.distance <= 0f || hit.collider.GetComponentInParent<PhotonView>() != null)
                continue;
            // The train the target rides would pin the camera inside the cabin.
            if (_platform != null && hit.collider.transform.IsChildOf(_platform))
                continue;
            nearest = Mathf.Min(nearest, hit.distance);
        }
        return Mathf.Max(MinDistance * 0.5f, nearest);
    }
}

/// <summary>Room picker and status, top-right. H hides it.</summary>
[DefaultExecutionOrder(32000)]
sealed class SpectatorOverlay : MonoBehaviour, ILobbyCallbacks, IMatchmakingCallbacks, IInRoomCallbacks
{
    const float Width = 300f;
    const string CodeControl = "SpectatorCode";
    // Photon makes the longest-present client master when the host leaves. PhotonMasterHandoff hands it
    // to a ready duck at once; if none takes it, leave rather than hold an unrun room.
    const float MasterGraceSeconds = 8f;
    // After the last duck leaves (a departure or a return), go back to the room list.
    const float EmptyRoomSeconds = 6f;
    const double FollowSeconds = 45d, FollowFirstTry = 4d, FollowRetry = 2d;

    internal static bool Typing { get; private set; }
    // Panel bounds in GUI coordinates (top-left origin); empty while hidden.
    static Rect _panel;

    internal static bool ContainsMouse()
    {
        Vector3 mouse = RailDesktopInput.mousePosition;
        return _panel.Contains(new Vector2(mouse.x, Screen.height - mouse.y));
    }

    readonly Dictionary<string, RoomInfo> _rooms = new Dictionary<string, RoomInfo>();
    readonly List<RoomInfo> _compounds = new List<RoomInfo>();
    readonly List<RoomInfo> _trains = new List<RoomInfo>();
    Vector2 _scroll;
    string _code = "";
    string _message;
    bool _hidden;
    bool _followDepartures = true;
    float _nextLobbyAttempt;
    float _masterSince = -1f, _emptySince = -1f;
    // Following a public departure into the crew's new expedition room (addressed by its allocation).
    string _followAllocation, _seenDestination, _lastTried;
    int _followAttempt;
    double _followUntil, _followNextTry;
    GUIStyle _box, _label, _header;

    void OnEnable() => PhotonNetwork.AddCallbackTarget(this);
    void OnDisable() => PhotonNetwork.RemoveCallbackTarget(this);

    static PhotonSession Session => PhotonSession.Instance;
    static bool Busy => Session != null && Session.HasRoomRequest;

    void Update()
    {
        if (!Typing && RailDesktopInput.GetKeyDown(KeyCode.H))
            _hidden = !_hidden;
        if (!Typing && RailDesktopInput.GetKeyDown(KeyCode.L))
            SpectatorView.Toggle();
        SpectatorView.Enforce();

        TickFollow();
        LeaveIfStuckAsMaster();
        LeaveIfCrewGone();
        ReturnToLoginWhenOutOfRoom();

        // Keep the room list live while connected and outside a room.
        if (PhotonNetwork.NetworkClientState == ClientState.ConnectedToMasterServer && !PhotonNetwork.InLobby
            && !Busy && Time.unscaledTime >= _nextLobbyAttempt)
        {
            _nextLobbyAttempt = Time.unscaledTime + 2f;
            PhotonNetwork.JoinLobby(PhotonSession.RoomListLobby);
        }
    }

    void LeaveIfStuckAsMaster()
    {
        if (!PhotonNetwork.InRoom || !PhotonNetwork.IsMasterClient || Busy)
        {
            _masterSince = -1f;
            return;
        }
        if (_masterSince < 0f) { _masterSince = Time.unscaledTime; return; }
        if (Time.unscaledTime - _masterSince < MasterGraceSeconds)
            return;
        _masterSince = -1f;
        Leave("Left room " + PhotonNetwork.CurrentRoom.Name + ": became host and no duck could take over ("
            + System.DateTime.Now.ToString("HH:mm") + ").");
    }

    void LeaveIfCrewGone()
    {
        if (!PhotonNetwork.InRoom || Busy || _followAllocation != null || RailSpectators.LiveCrewCount() > 0)
        {
            _emptySince = -1f;
            return;
        }
        if (_emptySince < 0f) { _emptySince = Time.unscaledTime; return; }
        if (Time.unscaledTime - _emptySince < EmptyRoomSeconds)
            return;
        _emptySince = -1f;
        Leave("The crew left room " + PhotonNetwork.CurrentRoom.Name + ".");
    }

    /// <summary>Outside a room the stale gameplay scene would keep showing; go back to Login and the room list.</summary>
    void ReturnToLoginWhenOutOfRoom()
    {
        if (PhotonNetwork.InRoom || Busy || _followAllocation != null)
            return;
        Scene active = SceneManager.GetActiveScene();
        if (!Spectator.IsGameplayScene(active) || GameScenes.IsLoaded(GameScenes.Login))
            return;
        SceneManager.LoadScene(GameScenes.Login, LoadSceneMode.Single);
    }

    void Leave(string message)
    {
        _message = message;
        _followAllocation = null;
        Session?.LeaveAsSpectator(message);
    }

    void Join(string roomName)
    {
        GUI.FocusControl(null);
        _followAllocation = null;
        if (Session == null || !RailSessionRules.IsCode(roomName)) { _message = "Enter a five-digit room code."; return; }
        _message = "Joining " + roomName + "...";
        Session.RequestSpectate(roomName);
    }

    /// <summary>
    /// A public departure moves the crew to a new expedition room whose code comes from the departure's
    /// allocation token. Try that code (and the next ones a collision would use) until the room exists.
    /// </summary>
    void TickFollow()
    {
        if (PhotonNetwork.InRoom && PhotonSession.IsRailsRoom && PhotonSession.Kind == RailSessionRules.CompoundKind && !Busy)
        {
            string destination = PhotonSession.Get(RailSessionRules.Destination, "");
            if (destination != _seenDestination)
            {
                _seenDestination = destination;
                if (_followDepartures && destination.StartsWith("run-", System.StringComparison.Ordinal) && _followAllocation == null)
                {
                    _followAllocation = destination; _followAttempt = 0; _lastTried = null;
                    double now = Time.realtimeSinceStartupAsDouble;
                    _followUntil = now + FollowSeconds; _followNextTry = now + FollowFirstTry;
                    _message = "A crew is departing — following them aboard...";
                }
            }
        }
        if (_followAllocation == null || Busy)
            return;
        double time = Time.realtimeSinceStartupAsDouble;
        if (PhotonNetwork.InRoom && PhotonSession.IsRailsRoom && PhotonSession.Get(RailSessionRules.Allocation, "") == _followAllocation)
        {
            _followAllocation = null;
            _message = "Riding with the departing crew.";
            return;
        }
        if (time > _followUntil)
        {
            _followAllocation = null;
            _message = "Could not find the departing train.";
            if (PhotonNetwork.InRoom && PhotonSession.Kind != RailSessionRules.CompoundKind) Session?.LeaveAsSpectator(_message);
            return;
        }
        // Joined a room with that code, but it belongs to someone else: the crew used the next code.
        if (PhotonNetwork.InRoom && PhotonNetwork.CurrentRoom.Name == _lastTried)
        {
            _followAttempt = (_followAttempt + 1) % 100;
            _lastTried = null;
            _followNextTry = time;
        }
        if (time < _followNextTry)
            return;
        _followNextTry = time + FollowRetry;
        _lastTried = RailSessionRules.RoomCode(_followAllocation, _followAttempt);
        Session?.RequestSpectate(_lastTried);
    }

    void OnGUI()
    {
        Typing = GUI.GetNameOfFocusedControl() == CodeControl;
        if (_hidden)
        {
            _panel = Rect.zero;
            return;
        }
        EnsureStyles();

        Rect area = new Rect(Screen.width - Width - 8f, 8f, Width, Screen.height - 16f);
        GUILayout.BeginArea(area);
        GUILayout.BeginVertical(_box);
        GUILayout.Label("<b>DUCK RAILS SPECTATOR</b>   " + PhotonNetwork.NetworkClientState, _header);

        if (PhotonNetwork.InRoom)
            DrawRoom();
        else
            DrawRoomPicker();

        string status = Session != null && Busy ? Session.Status : null;
        if (!string.IsNullOrEmpty(status))
            GUILayout.Label(status, _label);
        if (!string.IsNullOrEmpty(_message))
            GUILayout.Label(_message, _label);
        else if (Session != null && !string.IsNullOrEmpty(Session.LastError))
            GUILayout.Label(Session.LastError, _label);
        if (SpectatorCamera.FollowingName != null)
            GUILayout.Label("<b>Following " + SpectatorCamera.FollowingName + "</b>   F / WASD free fly", _label);
        else if (!string.IsNullOrEmpty(SpectatorCamera.Notice))
            GUILayout.Label(SpectatorCamera.Notice, _label);
        string scroll = SpectatorCamera.FollowingName != null
            ? "Scroll zoom (" + SpectatorCamera.Distance.ToString("0.0") + " m)"
            : "Scroll speed (" + SpectatorCamera.Speed.ToString("0") + ")";
        GUILayout.Label("Mouse look · Esc free cursor · click to look again\nWASD move · Q/E down/up · Shift fast\n" + scroll
            + " · Tab next player · F follow\nR ride train (" + (SpectatorCamera.RidingTrain ? "on" : "off")
            + ") · L fog (" + (SpectatorView.FogOff ? "off" : "on") + ") · H hide", _label);
        GUILayout.EndVertical();
        if (Event.current.type == EventType.Repaint)
        {
            Rect box = GUILayoutUtility.GetLastRect();
            _panel = new Rect(area.x + box.x, area.y + box.y, box.width, box.height);
        }
        GUILayout.EndArea();
    }

    static string KindLabel(string kind, RailPhase phase) =>
        kind == RailSessionRules.CompoundKind ? "Compound"
        : kind == RailSessionRules.ExpeditionKind ? "Public train · " + phase
        : kind == RailSessionRules.PrivateKind ? "Private · " + phase
        : "Room";

    void DrawRoom()
    {
        Room room = PhotonNetwork.CurrentRoom;
        int crew = RailSpectators.LiveCrewCount();
        GUILayout.Label("Room " + room.Name + "   " + KindLabel(PhotonSession.Kind, PhotonSession.Phase)
            + "   " + crew + " duck" + (crew == 1 ? "" : "s"), _label);
        foreach (NetPlayer player in PhotonNetwork.PlayerListOthers)
        {
            if (RailSpectators.Is(player)) continue;
            string name = string.IsNullOrEmpty(player.NickName) ? "#" + player.ActorNumber : player.NickName;
            GUILayout.Label("  " + name + (player.IsMasterClient ? "  (host)" : "") + (player.IsInactive ? "  (reconnecting)" : "")
                + (RailExpedition.ActorDown(player.ActorNumber) ? "  (down)" : ""), _label);
        }
        if (PhotonSession.Kind == RailSessionRules.CompoundKind)
            _followDepartures = GUILayout.Toggle(_followDepartures, " Follow departing crews aboard", GUI.skin.toggle);
        GUI.enabled = !Busy;
        if (GUILayout.Button("Leave room"))
            Leave(null);
        GUI.enabled = true;
    }

    void DrawRoomPicker()
    {
        if (!PhotonNetwork.InLobby)
        {
            GUILayout.Label(Busy ? "Joining..." : "Connecting to the room list...", _label);
            DrawCodeEntry();
            return;
        }

        RefreshLists();
        _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(Mathf.Max(140f, Screen.height * 0.5f)));
        GUILayout.Label(_trains.Count == 0 ? "No public trains running" : "<b>Public trains</b>", _label);
        foreach (RoomInfo info in _trains)
            DrawRoomButton(info, RailSessionRules.CrewCapacity);
        GUILayout.Label(_compounds.Count == 0 ? "No public compounds" : "<b>Compounds</b>", _label);
        foreach (RoomInfo info in _compounds)
            DrawRoomButton(info, RailSessionRules.CompoundCapacity);
        GUILayout.EndScrollView();
        DrawCodeEntry();
    }

    void DrawRoomButton(RoomInfo info, int capacity)
    {
        int max = info.MaxPlayers > 0 ? info.MaxPlayers : capacity;
        bool full = info.PlayerCount >= max;
        string phase = info.CustomProperties[RailSessionRules.Phase] is int value ? ((RailPhase)value).ToString() : "";
        GUI.enabled = info.IsOpen && !full && !Busy;
        string label = info.Name + "   " + info.PlayerCount + "/" + max
            + (phase.Length > 0 && info.CustomProperties[RailSessionRules.Kind] as string == RailSessionRules.ExpeditionKind ? "   " + phase : "")
            + (full ? "  full" : !info.IsOpen ? "  closed" : "");
        if (GUILayout.Button(label))
            Join(info.Name);
        GUI.enabled = true;
    }

    void DrawCodeEntry()
    {
        GUILayout.BeginHorizontal();
        GUI.SetNextControlName(CodeControl);
        _code = GUILayout.TextField(_code, PhotonSession.RoomCodeLength, GUILayout.Width(80f));
        bool submit = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return && Typing;
        GUI.enabled = RailSessionRules.IsCode(_code) && !Busy;
        if (GUILayout.Button("Join room code") || (submit && GUI.enabled))
            Join(_code);
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    void RefreshLists()
    {
        _compounds.Clear();
        _trains.Clear();
        foreach (RoomInfo info in _rooms.Values)
        {
            string kind = info.CustomProperties[RailSessionRules.Kind] as string;
            if (kind == RailSessionRules.ExpeditionKind) _trains.Add(info);
            else if (kind == RailSessionRules.CompoundKind) _compounds.Add(info);
        }
        System.Comparison<RoomInfo> order = (a, b) => b.PlayerCount != a.PlayerCount
            ? b.PlayerCount.CompareTo(a.PlayerCount) : string.CompareOrdinal(a.Name, b.Name);
        _compounds.Sort(order);
        _trains.Sort(order);
    }

    void EnsureStyles()
    {
        if (_box != null)
            return;
        var background = new Texture2D(1, 1);
        background.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.72f));
        background.Apply();
        _box = new GUIStyle(GUI.skin.box) { padding = new RectOffset(8, 8, 8, 8) };
        _box.normal.background = background;
        // Game text uses CutePixel; IMGUI needs its source font, when the font asset keeps one at runtime.
        Font font = TMPro.TMP_Settings.defaultFontAsset != null ? TMPro.TMP_Settings.defaultFontAsset.sourceFontFile : null;
        _label = new GUIStyle(GUI.skin.label) { richText = true, wordWrap = true };
        if (font != null) _label.font = font;
        _label.normal.textColor = Color.white;
        _header = new GUIStyle(_label) { fontSize = _label.fontSize > 0 ? _label.fontSize + 2 : 15 };
    }

    public void OnJoinedLobby() => _rooms.Clear();
    public void OnLeftLobby() => _rooms.Clear();
    public void OnLobbyStatisticsUpdate(List<TypedLobbyInfo> lobbyStatistics) { }

    public void OnRoomListUpdate(List<RoomInfo> roomList)
    {
        foreach (RoomInfo info in roomList)
        {
            if (info == null || string.IsNullOrEmpty(info.Name)) continue;
            bool rails = info.CustomProperties[RailSessionRules.Schema] is int schema && schema == RailSessionRules.Version;
            if (info.RemovedFromList || !info.IsVisible || !rails) _rooms.Remove(info.Name);
            else _rooms[info.Name] = info;
        }
    }

    public void OnJoinedRoom()
    {
        if (_followAllocation == null) _message = null;
        _seenDestination = PhotonSession.Get(RailSessionRules.Destination, "");
        _masterSince = _emptySince = -1f;
    }
    public void OnJoinRoomFailed(short returnCode, string message)
    {
        if (_followAllocation != null) return; // Not created yet: TickFollow retries.
        _message = returnCode == ErrorCode.GameFull ? "Room is full."
            : returnCode == ErrorCode.GameDoesNotExist ? "No room with that code (or a different game version)."
            : returnCode == ErrorCode.GameClosed ? "Room is closed (the run is ending)."
            : "Join failed: " + message;
    }
    public void OnFriendListUpdate(List<FriendInfo> friendList) { }
    public void OnCreatedRoom() { }
    public void OnCreateRoomFailed(short returnCode, string message) { }
    public void OnJoinRandomFailed(short returnCode, string message) { }
    public void OnLeftRoom() { _masterSince = _emptySince = -1f; }

    public void OnPlayerEnteredRoom(NetPlayer newPlayer) { }
    public void OnPlayerLeftRoom(NetPlayer otherPlayer) { }
    public void OnRoomPropertiesUpdate(ExitGames.Client.Photon.Hashtable propertiesThatChanged) { }
    public void OnPlayerPropertiesUpdate(NetPlayer targetPlayer, ExitGames.Client.Photon.Hashtable changedProps) { }
    public void OnMasterClientSwitched(NetPlayer newMasterClient) { _masterSince = -1f; }
}
#endif
