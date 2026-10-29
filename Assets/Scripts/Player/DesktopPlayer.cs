using Game.Animation;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Keyboard-and-mouse stand-in for the VR rig. No headset, camera rig, or Meta scripts.
/// Head and hand transforms feed the same body driver Photon already streams.
/// The right hand is a real PlayerHand: hold the left mouse button to grip. The click reaches the hand to whatever
/// is under the crosshair, then it drifts back to rest, bringing anything it grabbed.
/// </summary>
[DefaultExecutionOrder(-16200)] // ControllerInput runs at -16100; consume menu clicks first.
public class DesktopPlayer : MonoBehaviour
{
    public static bool SuppressCursorCapture { get; set; }
    public static bool SuppressMouseGrip { get; private set; }
    /// <summary>Frame the cursor was last captured by a click, so that click doesn't also grip.</summary>
    public static int CursorLockedFrame { get; private set; } = -1;

    static readonly Vector3 LeftHandRest = new Vector3(-0.22f, -0.32f, 0.38f);
    static readonly Vector3 RightHandRest = new Vector3(0.22f, -0.32f, 0.38f);

    [SerializeField] float eyeHeight = 1.55f;
    [SerializeField] float moveSpeed = 4.5f;
    [SerializeField] float sprintSpeed = 8f;
    [SerializeField] float lookSensitivity = 2f;
    [SerializeField] float jumpSpeed = 8.660254f;
    [SerializeField] HeadFlashlight headFlashlight;
    [SerializeField] FloatingDustMotes floatingDustMotesPrefab;
    [Tooltip("How far a click can reach the right hand.")]
    [SerializeField] float reach = 3f;
    [Tooltip("Distance from the desktop hand's HoldPoint to an item surface allowed for grabbing, in metres. Applied when the hand is created.")]
    [SerializeField, Min(0f)] float grabTolerance = 0.12f;
    [Tooltip("How fast the right hand returns to rest after reaching, in m/s.")]
    [SerializeField] float handReturnSpeed = 3f;
    [Tooltip("Thrown forward with E. The bat is laid sideways so its length sweeps across the aim.")]
    [SerializeField] GameObject testNailBat;
    const float TestThrowSpeed = 10f;
    const float TestThrowSpin = 8f;

    Transform _yaw;
    Transform _pitch;
    Transform _camera;
    PlayerHand _rightHand;
    CharacterController _controller;
    float _pitchAngle;
    float _verticalVelocity;
    Vector3 _pushVelocity;
    PauseMenu _pauseMenu;
    PauseMenuButton _pressedButton;
#if UNITY_EDITOR
    DesktopLobbySimulator _lobbySimulator;
#endif

    void Start()
    {
        _pauseMenu = FindAnyObjectByType<PauseMenu>();
#if UNITY_EDITOR
        // This rig is local; the separately spawned Photon avatar must never own a simulator.
        if (DesktopPlay.UseDesktop && GR.Player.instance != null
            && GR.Player.instance.gameObject == gameObject && GetComponentInParent<PhotonView>() == null
            && DesktopLobbySimulator.IsTaggedInstance())
            _lobbySimulator = new DesktopLobbySimulator();
#endif
    }

    void OnDisable()
    {
        ReleaseMenuButton();
        SuppressMouseGrip = false;
    }

    void OnGUI()
    {
        if (_pauseMenu == null || !_pauseMenu.IsOpen) return;
        GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2f, Screen.height * 0.5f - 2f, 4f, 4f), Texture2D.whiteTexture);
    }

    void ReleaseMenuButton()
    {
        if (_pressedButton != null) _pressedButton.Release();
        _pressedButton = null;
    }

    void UpdateMenuClick(bool typing)
    {
        if (!Input.GetMouseButton(0))
        {
            ReleaseMenuButton();
            SuppressMouseGrip = false;
        }
        if (typing || Cursor.lockState != CursorLockMode.Locked || _pauseMenu == null || !_pauseMenu.IsOpen)
        {
            ReleaseMenuButton();
            return;
        }
        if (Time.frameCount == CursorLockedFrame || !Input.GetMouseButtonDown(0)) return;

        // The menu's grab volume may surround the buttons; pick the nearest button inside it.
        PauseMenuButton button = null;
        float nearest = reach;
        foreach (var hit in Physics.RaycastAll(_camera.position, _camera.forward, reach,
                     Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
        {
            var candidate = hit.collider.GetComponentInParent<PauseMenuButton>();
            if (candidate == null || !candidate.isActiveAndEnabled || hit.distance > nearest
                || candidate.GetComponentInParent<PauseMenu>() != _pauseMenu) continue;
            nearest = hit.distance;
            button = candidate;
        }
        if (button == null) return;
        // Keep consuming until mouse-up, even if the button closes its page/menu.
        SuppressMouseGrip = true;
        _pressedButton = button;
        button.Press();
    }

    void Awake()
    {
        _controller = gameObject.AddComponent<CharacterController>();
        _controller.height = 1.7f;
        _controller.radius = 0.25f;
        _controller.center = new Vector3(0f, 0.85f, 0f);

        _yaw = new GameObject("Yaw").transform;
        _yaw.SetParent(transform, false);
        _yaw.localPosition = new Vector3(0f, eyeHeight, 0f);

        _pitch = new GameObject("Pitch").transform;
        _pitch.SetParent(_yaw, false);

        var cameraObject = new GameObject("Camera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(_pitch, false);
        var camera = cameraObject.AddComponent<Camera>();
        camera.nearClipPlane = 0.08f;
        cameraObject.AddComponent<AudioListener>();
        _camera = cameraObject.transform;

        if (headFlashlight != null)
        {
            headFlashlight.transform.SetParent(cameraObject.transform, false);
            headFlashlight.transform.localPosition = Vector3.zero;
            headFlashlight.transform.localRotation = Quaternion.identity;
        }

        if (floatingDustMotesPrefab != null)
            FloatingDustMotes.Attach(cameraObject.transform, floatingDustMotesPrefab);

        var input = gameObject.AddComponent<ControllerInput>();
        input.mouseGrip = true;

        var leftHand = MakeHand("LeftHand", LeftHandRest);
        var rightHand = MakeHand("RightHand", RightHandRest);
        _rightHand = rightHand.gameObject.AddComponent<PlayerHand>();
        _rightHand.GrabTolerance = grabTolerance;
        _rightHand.controller = OVRInput.Controller.RTouch;
        var player = GetComponent<GR.Player>();
        if (player != null)
            player.rightPlayerHand = _rightHand;

        var driver = GetComponent<LocalBodyDriver>();
        if (driver == null)
            driver = gameObject.AddComponent<LocalBodyDriver>();
        if (driver.controllerInput == null)
            driver.controllerInput = input;
        driver.BindAnchors(cameraObject.transform, leftHand, rightHand, GetComponentInChildren<RemotePlayerBody>(true));

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    Transform MakeHand(string handName, Vector3 localPosition)
    {
        var hand = new GameObject(handName).transform;
        hand.SetParent(_pitch, false);
        hand.localPosition = localPosition;
        return hand;
    }

    public void Teleport(Vector3 position, Quaternion rotation, bool resetLook = false)
    {
        if (HelicopterManager.instance != null) HelicopterManager.instance.ReleasePassenger();
        _controller.enabled = false;
        transform.SetPositionAndRotation(position, rotation);
        if (resetLook)
        {
            _yaw.localRotation = Quaternion.identity;
            _pitchAngle = 0f;
            _pitch.localRotation = Quaternion.identity;
        }
        _verticalVelocity = 0f;
        _pushVelocity = Vector3.zero;
        _controller.enabled = true;
    }

    public Vector3 PassengerAnchor => transform.TransformPoint(_controller.center);
    public Vector3 PassengerFoot => PassengerAnchor - Vector3.up * (_controller.height * 0.5f * Mathf.Abs(transform.lossyScale.y));
    public float PassengerRadius => _controller.radius * Mathf.Max(Mathf.Abs(transform.lossyScale.x), Mathf.Abs(transform.lossyScale.z));

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (HelicopterManager.instance != null && hit.normal.y >= 0.5f)
            HelicopterManager.instance.ReportBodySupport(transform, hit.collider, hit.point, hit.normal);
    }

    public void ApplyPassengerDisplacement(Vector3 delta)
    {
        // The manager syncs this translation together with the floor before Move().
        // Sweeping the carry itself would collide with the already-raised floor.
        transform.position += delta;
    }

    public void Launch(Vector3 worldVelocity)
    {
        _pushVelocity = worldVelocity;
    }

    void Update()
    {
        bool typing = DuckCompanionDebug.IsTyping;
        bool simulated = false;
        Vector2 moveInput = typing ? Vector2.zero : new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
        Vector2 lookInput = Vector2.zero;
        bool jumpInput = !typing && Input.GetKeyDown(KeyCode.Space);
        bool sprintInput = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
#if UNITY_EDITOR
        if (_lobbySimulator != null)
        {
            simulated = true;
            _lobbySimulator.Sample(this, out moveInput, out lookInput, out jumpInput);
            sprintInput = false;
        }
#endif
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else if (Input.GetMouseButtonDown(0)
            && Cursor.lockState != CursorLockMode.Locked
            && !SuppressCursorCapture && !DuckCompanionDebug.IsHovered)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            CursorLockedFrame = Time.frameCount;
        }

        if (simulated || (!typing && Cursor.lockState == CursorLockMode.Locked))
        {
            if (!simulated)
                lookInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * lookSensitivity;
            _yaw.Rotate(0f, lookInput.x, 0f, Space.Self);
            _pitchAngle = Mathf.Clamp(_pitchAngle - lookInput.y, -80f, 80f);
            _pitch.localRotation = Quaternion.Euler(_pitchAngle, 0f, 0f);
        }

        if (!typing && Input.GetKeyDown(KeyCode.E))
            ThrowTestBat();

        UpdateMenuClick(typing);
        if (!typing && !SuppressMouseGrip) UpdateReach();

        var planar = new Vector3(moveInput.x, 0f, moveInput.y);
        if (planar.sqrMagnitude > 1f)
            planar.Normalize();

        float speed = sprintInput ? sprintSpeed : moveSpeed;
        var motion = _yaw.TransformDirection(planar) * speed;

        bool pushRising = _pushVelocity.y > 0f;
        if (_controller.isGrounded && _verticalVelocity < 0f && !pushRising)
            _verticalVelocity = -1f;
        if (_controller.isGrounded && jumpInput)
            _verticalVelocity = jumpSpeed;

        float gravityStep = Physics.gravity.y * Time.deltaTime;
        _verticalVelocity += gravityStep;

        if (_pushVelocity.sqrMagnitude > 0f)
        {
            _pushVelocity.y += gravityStep;
            motion.x += _pushVelocity.x;
            motion.z += _pushVelocity.z;
            if (pushRising || !_controller.isGrounded)
                _verticalVelocity = _pushVelocity.y;
            else
                _pushVelocity = Vector3.zero;
        }

        motion.y = _verticalVelocity;
        _controller.Move(motion * Time.deltaTime);
        if (HelicopterManager.instance != null) HelicopterManager.instance.RefreshPassenger();
    }

    void ThrowTestBat()
    {
        if (testNailBat == null || _camera == null)
            return;

        Vector3 aim = _camera.forward;
        if (aim.sqrMagnitude < 0.0001f)
            aim = _yaw.forward;
        aim.Normalize();

        Vector3 side = Vector3.Cross(Vector3.up, aim);
        if (side.sqrMagnitude < 0.0001f)
            side = _camera.right;
        else
            side.Normalize();

        // Shaft is local +Y. Lay it along the camera's right and center the collider on the aim ray.
        Quaternion rotation = Quaternion.LookRotation(aim, side);
        Vector3 position = _camera.position + aim * 1.25f - side * 0.5f;

        GameObject spawned;
        if (PhotonNetwork.InRoom)
        {
            PhotonPreparePool.Register(testNailBat);
            spawned = PhotonNetwork.InstantiateWithRoomLifetime(testNailBat.name, position, rotation);
        }
        else
            spawned = Instantiate(testNailBat, position, rotation);

        if (spawned == null)
            return;

        // This shortcut never calls Grab/Release. Register the player throw so
        // companion hits and dropped-item cleanup treat it like an ordinary throw.
        spawned.GetComponent<PhotonGrabbable>()?.NotifyPlayerThrown();

        Rigidbody body = spawned.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = aim * TestThrowSpeed;
            body.angularVelocity = aim * TestThrowSpin;
        }

        if (!PhotonNetwork.InRoom)
        {
            PhotonGrabbable grabbable = spawned.GetComponent<PhotonGrabbable>();
            if (grabbable != null)
                grabbable.KeepLocalPhysics();
        }
    }

    // Runs in Update so the hand is already in place when PlayerHand handles the grip in LateUpdate.
    void UpdateReach()
    {
        Transform hand = _rightHand.transform;
        hand.localPosition = Vector3.MoveTowards(hand.localPosition, RightHandRest, handReturnSpeed * Time.deltaTime);

        if (Cursor.lockState != CursorLockMode.Locked || Time.frameCount == CursorLockedFrame || !Input.GetMouseButtonDown(0))
            return;
        int mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
        if (Physics.Raycast(_camera.position, _camera.forward, out RaycastHit hit, reach, mask, QueryTriggerInteraction.Ignore))
            hand.position = hit.point - _camera.forward * 0.02f;
    }
}
