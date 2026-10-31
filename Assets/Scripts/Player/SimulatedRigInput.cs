using Game.Animation;
using UnityEngine;
using UnityEngine.XR;

/// <summary>Optional keyboard/mouse pose and button source on the real DuckPlayer.</summary>
[DefaultExecutionOrder(-16200)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NewPlayer), typeof(ControllerInput))]
public sealed class SimulatedRigInput : MonoBehaviour
{
    public struct Frame
    {
        public Vector2 move, look;
        public bool leftReach, rightReach, grip, trigger, jump, crouch, throttle;
        public bool x, y, a, b, menu;
        public float reachDelta, roll;
    }

    public static SimulatedRigInput Instance { get; private set; }
    public bool IsSimulating { get; private set; }
    // Test drivers feed exactly the same pose/button path as the keyboard.
    public bool UseInjectedInput { get; set; }
    public Frame InjectedInput { get; set; }
    public Frame Buttons { get; private set; }
    public bool ThrottleHeld { get; private set; }
    public bool HasTrackedPose { get; private set; }
    public string Status { get; private set; } = "";

    // NewPlayer's contact flag lasts two render frames; the editor can render
    // several frames between physics steps. Check the same body shape as well
    // so keyboard jumps do not randomly miss a settled floor contact.
    public bool HasBodySupport
    {
        get
        {
            var bounds = player.bodyCollider.bounds;
            float radius = Mathf.Min(bounds.extents.x, bounds.extents.z) * .9f;
            return Physics.SphereCast(bounds.center, radius, Vector3.down, out var hit,
                Mathf.Max(0, bounds.extents.y - radius) + .08f, player.locomotionEnabledLayers, QueryTriggerInteraction.Ignore) && hit.normal.y >= .5f;
        }
    }

    NewPlayer player;
    ControllerInput input;
    OVRCameraRig cameraRig;
    Transform head, left, right;
    Vector3 headPosition, leftPosition, rightPosition;
    Quaternion headRotation, leftRotation, rightRotation;
    Vector3 trackedHead, trackedLeft, trackedRight;
    Quaternion trackedHeadRotation, trackedLeftRotation, trackedRightRotation;
    float yaw, pitch, reach = .65f, handRoll;
    float standingHeight = 1.55f;
    bool started, awaitNeutral = true, focused = true, showHelp;
    int cursorFrame = -1;
#if UNITY_EDITOR
    DesktopLobbySimulator lobby;
#endif

    void Awake()
    {
        Instance = this;
        player = GetComponent<NewPlayer>();
        input = GetComponent<ControllerInput>();
        cameraRig = GetComponentInChildren<OVRCameraRig>(true);
        var driver = GetComponent<LocalBodyDriver>();
        head = driver.HeadAnchor; left = driver.LeftHandAnchor; right = driver.RightHandAnchor;
        input.simulation = this;
        // Legacy mouseGrip intentionally stays false: PlayerHand, buttons and the
        // drive lever must use their normal VR proximity/contact paths.
        input.mouseGrip = false;
        IsSimulating = DesktopPlay.UseDesktop;
        if (cameraRig != null)
        {
            cameraRig.UpdatedAnchors += OnTrackedAnchors;
            // This SDK helper also reads the keyboard; only one simulator may own poses.
            var emulator = cameraRig.GetComponent<OVRHeadsetEmulator>();
            if (emulator != null) emulator.enabled = false;
        }
    }

    void Start()
    {
        started = true;
        if (IsSimulating)
        {
            headPosition = new Vector3(0, standingHeight, 0);
            headRotation = Quaternion.identity;
            leftPosition = headPosition + new Vector3(-.22f, -.32f, .3f);
            rightPosition = headPosition + new Vector3(.22f, -.32f, .3f);
            leftRotation = rightRotation = Quaternion.identity;
            WriteSimulatedPose();
            player.ReseedTrackingPose();
            CaptureCursor();
        }
#if UNITY_EDITOR
        if (DesktopLobbySimulator.IsTaggedInstance()) lobby = new DesktopLobbySimulator();
#endif
    }

    void OnDestroy()
    {
        if (cameraRig != null) cameraRig.UpdatedAnchors -= OnTrackedAnchors;
        if (input != null && input.simulation == this) input.simulation = null;
        if (Instance == this) Instance = null;
    }

    void OnDisable() => ReleaseInput();
    void OnApplicationFocus(bool value) { focused = value; if (!value) ReleaseInput(); }
    void OnApplicationPause(bool paused) { if (paused) ReleaseInput(); }

    void ReleaseInput()
    {
        Buttons = default;
        ThrottleHeld = false;
        awaitNeutral = true;
        if (input != null) input.ApplySimulationButtons(default);
        var owner = GetComponent<GR.Player>();
        if (Application.isPlaying && owner != null && owner == GR.Player.instance)
            PhotonHeldItems.DropLocalHands();
        owner?.leftPlayerHand?.Release();
        owner?.rightPlayerHand?.Release();
    }

    void OnTrackedAnchors(OVRCameraRig rig)
    {
        if (!started) return;
        var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (device.isValid && device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) && tracked)
        {
            trackedHead = head.localPosition; trackedHeadRotation = head.localRotation;
            trackedLeft = left.localPosition; trackedLeftRotation = left.localRotation;
            trackedRight = right.localPosition; trackedRightRotation = right.localRotation;
            HasTrackedPose = true;
        }
        // OVR also updates immediately before rendering. Override through its own
        // event so those late updates cannot undo a simulated pose.
        if (IsSimulating) WriteSimulatedPose();
    }

    public bool SetSimulating(bool simulated)
    {
        if (simulated == IsSimulating) return true;
        var device = InputDevices.GetDeviceAtXRNode(XRNode.Head);
        if (!simulated && (!HasTrackedPose || !device.isValid ||
            !device.TryGetFeatureValue(CommonUsages.isTracked, out bool tracked) || !tracked))
        {
            Status = "Quest Link is not tracking. Connect Link before Play, then press F2.";
            return false;
        }
        SwitchInputSource(simulated);
        return true;
    }

    void SwitchInputSource(bool simulated)
    {
        ReleaseInput();
        input.RequireNeutralInput();
        Vector3 worldHead = head.position;
        float worldYaw = head.eulerAngles.y;
        if (simulated)
        {
            headPosition = head.localPosition; headRotation = head.localRotation;
            leftPosition = left.localPosition; leftRotation = left.localRotation;
            rightPosition = right.localPosition; rightRotation = right.localRotation;
            standingHeight = headPosition.y;
            yaw = headRotation.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0, headRotation.eulerAngles.x);
        }
        else
        {
            head.SetLocalPositionAndRotation(trackedHead, trackedHeadRotation);
            left.SetLocalPositionAndRotation(trackedLeft, trackedLeftRotation);
            right.SetLocalPositionAndRotation(trackedRight, trackedRightRotation);
            // Preserve the user's location and facing; changing input sources is
            // not a teleport or a physical hand swing.
            transform.rotation = Quaternion.Euler(0, Mathf.DeltaAngle(head.eulerAngles.y, worldYaw), 0) * transform.rotation;
            player.trackingSpace.position += worldHead - head.position;
        }
        IsSimulating = simulated;
        DesktopPlay.SetSessionSimulation(simulated);
        player.ReseedTrackingPose();
        if (simulated) CaptureCursor();
        else { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        Status = simulated ? "Keyboard/mouse input" : "Quest Link input";
    }

    void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        cursorFrame = Time.frameCount;
        awaitNeutral = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2) && !DuckCompanionDebug.IsTyping) SetSimulating(!IsSimulating);
        if (Input.GetKeyDown(KeyCode.F1)) showHelp = !showHelp;
        if (!IsSimulating)
        {
            // The keyboard throttle is also available while Quest Link supplies
            // tracked poses. VR does not capture the desktop cursor.
            bool held = UseInjectedInput ? InjectedInput.throttle : Input.GetKey(KeyCode.T);
            if (!focused || DuckCompanionDebug.IsTyping || RailWorld.InputBlocked) awaitNeutral = true;
            else if (!held) awaitNeutral = false;
            ThrottleHeld = !awaitNeutral && held;
            return;
        }
        Frame frame = UseInjectedInput ? InjectedInput : ReadKeyboard();
#if UNITY_EDITOR
        if (lobby != null && !UseInjectedInput && !RailWorld.InputBlocked)
        {
            lobby.Sample(this, out frame.move, out frame.look, out frame.jump);
        }
#endif
        ApplyFrame(frame, Time.deltaTime);
    }

    Frame ReadKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true; ReleaseInput();
        }
        // Session Controls owns the pointer while open. Editor overlays also
        // write the legacy suppression flag, so that flag alone is insufficient.
        if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked && !RailWorld.InputBlocked &&
            !DesktopPlayer.SuppressCursorCapture && !DuckCompanionDebug.IsHovered && !DuckCompanionDebug.IsTyping)
            CaptureCursor();
        if (!focused || Cursor.lockState != CursorLockMode.Locked || DuckCompanionDebug.IsTyping || Time.frameCount == cursorFrame)
            return default;
        if (RailWorld.InputBlocked) awaitNeutral = true;
        if (!Input.GetMouseButton(0) && !Input.GetMouseButton(1) && !Input.GetKey(KeyCode.T)) awaitNeutral = false;
        return new Frame {
            move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")),
            look = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 2f,
            leftReach = Input.GetKey(KeyCode.Q), rightReach = Input.GetKey(KeyCode.E),
            grip = !awaitNeutral && Input.GetMouseButton(1), trigger = !awaitNeutral && Input.GetMouseButton(0),
            throttle = !awaitNeutral && Input.GetKey(KeyCode.T),
            reachDelta = Input.mouseScrollDelta.y * .05f,
            roll = (Input.GetKey(KeyCode.X) ? 1 : 0) - (Input.GetKey(KeyCode.Z) ? 1 : 0),
            crouch = Input.GetKey(KeyCode.C), jump = Input.GetKeyDown(KeyCode.Space),
            x = Input.GetKey(KeyCode.Alpha1), y = Input.GetKey(KeyCode.Alpha2),
            a = Input.GetKey(KeyCode.Alpha3), b = Input.GetKey(KeyCode.Alpha4), menu = Input.GetKey(KeyCode.M)
        };
    }

    public void ApplyFrame(Frame frame, float dt)
    {
        if (!IsSimulating || !started) return;
        dt = Mathf.Clamp(dt, 0, .05f);
        bool blocked = RailWorld.InputBlocked;
        // Hands/head remain positionable for the diagnostic panel while loading.
        // Gameplay acquisition is still gated by the shared PlayerHand/control code.
        Buttons = blocked ? default : frame;
        ThrottleHeld = !blocked && frame.throttle;
        yaw += frame.look.x;
        pitch = Mathf.Clamp(pitch - frame.look.y, -80, 80);
        headRotation = Quaternion.Euler(pitch, yaw, 0);
        headPosition.y = Mathf.MoveTowards(headPosition.y, standingHeight - (frame.crouch || RailExpedition.LocalDown ? .7f : 0), dt * 2);
        reach = Mathf.Clamp(reach + frame.reachDelta, .25f, 1.1f);
        handRoll += frame.roll * dt * 90;
        var yawRotation = Quaternion.Euler(0, yaw, 0);
        Vector3 leftTarget = headPosition + (frame.leftReach ? headRotation * new Vector3(-.12f, -.06f, reach) : yawRotation * new Vector3(-.22f, -.32f, .3f));
        Vector3 rightTarget = headPosition + (frame.rightReach ? headRotation * new Vector3(.12f, -.06f, reach) : yawRotation * new Vector3(.22f, -.32f, .3f));
        leftPosition = Vector3.MoveTowards(leftPosition, leftTarget, dt * 2);
        rightPosition = Vector3.MoveTowards(rightPosition, rightTarget, dt * 2);
        leftRotation = Quaternion.RotateTowards(leftRotation, (frame.leftReach ? headRotation : yawRotation) * Quaternion.Euler(0, 0, handRoll), dt * 360);
        rightRotation = Quaternion.RotateTowards(rightRotation, (frame.rightReach ? headRotation : yawRotation) * Quaternion.Euler(0, 0, handRoll), dt * 360);
        WriteSimulatedPose();
        if (!blocked && !player.disableMovement)
        {
            // Simulated room-scale translation enters the existing head/body/hand
            // collision solver in NewPlayer.Update, including moving-platform carry.
            Vector2 move = Vector2.ClampMagnitude(frame.move, 1);
            Vector3 displacement = yawRotation * new Vector3(move.x, 0, move.y) * (RailExpedition.LocalDown ? .65f : 4.5f) * dt;
            player.trackingSpace.position += transform.TransformDirection(displacement);
            if (frame.jump && !RailExpedition.LocalDown && (player.IsGrounded() || HasBodySupport))
                player.rb.linearVelocity = new Vector3(player.rb.linearVelocity.x, 5, player.rb.linearVelocity.z);
        }
    }

    void WriteSimulatedPose()
    {
        head.SetLocalPositionAndRotation(headPosition, headRotation);
        left.SetLocalPositionAndRotation(leftPosition, leftRotation);
        right.SetLocalPositionAndRotation(rightPosition, rightRotation);
        if (cameraRig != null)
        {
            cameraRig.leftEyeAnchor.SetLocalPositionAndRotation(headPosition, headRotation);
            cameraRig.rightEyeAnchor.SetLocalPositionAndRotation(headPosition, headRotation);
        }
    }

    void OnGUI()
    {
        if (!IsSimulating) return;
        GUI.Label(new Rect(12, Screen.height - 30, 1000, 24), "Q / E reach · Right mouse grip · Left mouse trigger · Wheel reach · Hold T throttle · F1 help · F2 Quest Link");
        if (showHelp)
            GUI.Box(new Rect(12, Screen.height - 210, 570, 175), "SHARED DUCK RIG\nWASD move · Mouse look · Space jump · C crouch\nHold Q / E to extend left / right hand; release to bring it back\nRight mouse grips both hands · Left mouse triggers both hands\nWheel adjusts reach · Z / X rolls wrists · 1–4 controller face buttons\nHold T for throttle GO; release for STOP\nP diagnostic panel · Tab session tools · Esc release cursor\nF2 switches input without restarting Play\n" + Status);
    }
}
