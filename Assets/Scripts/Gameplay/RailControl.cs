using System;
using Photon.Pun;
using UnityEngine;

/// <summary>Grip-and-hold drive, with a toggle-T shortcut in desktop Keyboard or VR mode.</summary>
public sealed class RailControl : MonoBehaviour
{
    public enum Action { Drive, Horn, Bridge, Radio }
    public Action action;
    public Transform handle;
    public PressableButton holdButtonPrefab;
    [Header("Throttle feel (live tunable)")]
    [Tooltip("Rendered palm offset in handle-local metres. +Y lifts; -Z moves toward the driver. Does not affect tracking or breakaway.")]
    public Vector3 palmOffset = new Vector3(0, .025f, -.015f);
    [Min(0)] public float returnDuration = .25f;
    int clickStep = -1;
    // Pitch rises with each 25% step; the 100% step also plays the latch on top.
    static readonly float[] ClickPitch = { .8f, .95f, 1.1f, 1.3f };
    bool touching;
    float nextUse;
    static readonly System.Collections.Generic.HashSet<RailControl> drives = new System.Collections.Generic.HashSet<RailControl>();
    PlayerHand driveHand;
    string gripToken, gripRun, releasedToken;
    float gripStartAngle, gripStarted, nextHeartbeat, driveProgress;
    Transform drivePalm;
    Vector3 gripPalmLocal;
    Quaternion gripPalmRotation;
    const float BreakDistance = .18f;
    bool gripGranted, forwardLatched;
    bool simulatedDrive, throttleWasHeld;
    Vector3 handleRest;
    float renderedProgress, gripStartProgress, returnStart, returnElapsed;
    bool returning;
    void OnEnable() { drives.Add(this); }
    void Awake() { if (handle != null) handleRest = handle.localPosition; }
    void Start()
    {
        ConfigureDriveColliders();
        if ((action == Action.Horn || action == Action.Radio || action == Action.Bridge) && GetComponent<RailHoldControl>() == null && holdButtonPrefab != null)
        {
            var button = Instantiate(holdButtonPrefab, transform);
            button.name = "Hold button";
            button.transform.localPosition = new Vector3(0, .22f, 0);
            button.transform.localRotation = Quaternion.Euler(90, 0, 0);
            button.transform.localScale = Vector3.one * 1.5f;
            var hold = gameObject.AddComponent<RailHoldControl>();
            hold.button = button; hold.radio = action == Action.Radio; hold.bridge = action == Action.Bridge;
            var label = button.GetComponentInChildren<TMPro.TMP_Text>();
            label.text = hold.bridge ? "HOLD\nLOWER" : hold.radio ? "HOLD\nTALK" : "HOLD\nHORN"; label.fontSize = .65f;
        }
        if (handle != null) handleRest = handle.localPosition;
    }
    void Update()
    {
        if (action == Action.Horn || action == Action.Radio || action == Action.Bridge) return;
        if (action == Action.Drive) UpdateDrive();
        if (!PhotonSession.OnTrain || GR.Player.instance == null) return;
        var player = GR.Player.instance;
        bool near = Vector3.Distance(player.HeadCenteredGameplayPosition(player.transform.position), transform.position) < 3;
        bool hand = (player.leftPlayerHand != null && Vector3.Distance(player.leftPlayerHand.transform.position, transform.position) < .3f) ||
            (player.rightPlayerHand != null && Vector3.Distance(player.rightPlayerHand.transform.position, transform.position) < .3f);
        bool desktopUse = false;
        if (near && !RailWorld.InputBlocked && RailDesktopInput.GetKeyDown(KeyCode.E) && Camera.main != null &&
            Physics.Raycast(Camera.main.transform.position, Camera.main.transform.forward, out var hit, 3, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
            desktopUse = hit.collider.GetComponentInParent<RailControl>() == this;
        bool desktop = player.GetComponentInChildren<DesktopPlayer>() != null ||
            (player.rightPlayerHand != null && player.rightPlayerHand.IsDesktopInput);
        if (!RailExpedition.LocalDown && Time.time >= nextUse &&
            ((desktopUse && desktop) || (hand && !touching && (action != Action.Drive || desktop))))
        { nextUse = Time.time + .5f; Activate(); }
        touching = hand;
    }
    void OnDisable()
    {
        ReleaseDrive();
        drives.Remove(this);
    }
    public bool HeldBy(PlayerHand hand) => driveHand == hand;
    // A control must not become a hand-locomotion wall, even before it is gripped.
    // Keep enabled trigger shapes for grip proximity and desktop targeting.
    public void ConfigureDriveColliders()
    {
        if (action != Action.Drive) return;
        foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.isTrigger = true;
    }
    public static RailControl TryGripDrive(PlayerHand hand)
    {
        if (hand.IsDesktopInput || !hand.CanGrab || hand.IsHolding || RailWorld.InputBlocked ||
            PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady || PhotonSession.Phase != RailPhase.Running) return null;
        foreach (var control in drives)
        {
            if (control.action != Action.Drive || control.handle == null || control.driveHand != null || control.simulatedDrive || PhotonSession.DriveGripActor != 0) continue;
            var collider = control.handle.GetComponent<Collider>();
            if (collider == null || Vector3.Distance(collider.ClosestPoint(hand.transform.position), hand.transform.position) > .12f) continue;
            control.driveHand = hand;
            var driver = hand.GetComponentInParent<Game.Animation.LocalBodyDriver>();
            control.drivePalm = driver == null ? hand.transform :
                hand.IsControllerLTouch ? driver.LeftPalmSource : driver.RightPalmSource;
            // Bring the palm's hold point onto the handle surface at acquisition.
            Vector3 contactOffset = collider.ClosestPoint(hand.HoldPoint.position) - hand.HoldPoint.position;
            control.gripPalmLocal = control.handle.InverseTransformPoint(control.drivePalm.position + contactOffset);
            control.gripPalmRotation = Quaternion.Inverse(control.handle.rotation) * control.drivePalm.rotation;
            control.gripStartAngle = LeverAngle(control.transform.InverseTransformPoint(control.drivePalm.position));
            control.gripStartProgress = control.renderedProgress;
            control.BeginDrive();
            VibrationManager.instance?.TriggerVibrationPulse(.5f, .25f, .04f, hand);
            return control;
        }
        return null;
    }
    void BeginDrive()
    {
        gripRun = PhotonSession.RunId; gripToken = Guid.NewGuid().ToString("N");
        gripStarted = Time.unscaledTime; nextHeartbeat = 0;
        driveProgress = simulatedDrive ? 0 : gripStartProgress; gripGranted = forwardLatched = false;
        PhotonSession.Instance.RequestDriveGrip(gripRun, gripToken, 0, 0);
        PlayThrottle(RailAudio.Bank?.throttleGrab);
    }
    void PlayThrottle(Game.Audio.SoundEvent sound, float pitch = 1f) =>
        RailAudio.Play(sound, handle != null ? handle.position : transform.position, 1f, pitch);
    // One click per 25% of lever travel in either direction, so the spring-back ratchets down through them.
    void ThrottleClicks(float progress)
    {
        int step = Mathf.Clamp(Mathf.FloorToInt(progress * 4f + .001f), 0, 4);
        var bank = RailAudio.Bank;
        if (clickStep < 0 || bank == null || !PhotonSession.OnTrain) { clickStep = step; return; }
        if (step == clickStep) return;
        bool up = step > clickStep;
        int from = up ? clickStep + 1 : step + 1, to = up ? step : clickStep;
        for (int i = 0; i <= to - from; i++)
        {
            int k = up ? from + i : to - i; // descending pitch on the way back down
            PlayThrottle(bank.throttleClick, ClickPitch[k - 1]);
            if (up && k == 4) PlayThrottle(bank.throttleLatch);
        }
        clickStep = step;
    }
    public void ReleaseDrive()
    {
        if (driveHand == null && !simulatedDrive) return;
        PlayThrottle(RailAudio.Bank?.throttleRelease);
        PhotonSession.Instance?.RequestDriveGrip(gripRun, gripToken, 2, 0);
        releasedToken = gripToken;
        driveHand = null; drivePalm = null; simulatedDrive = false; gripGranted = forwardLatched = false; driveProgress = 0;
        // Stop authority immediately; the unheld artwork returns separately in UpdateDrive.
    }
    void OnApplicationFocus(bool focused) { if (!focused) ReleaseDrive(); }
    void OnApplicationPause(bool paused) { if (paused) ReleaseDrive(); }
    void UpdateDrive()
    {
        var simulator = SimulatedRigInput.Instance;
        bool throttleHeld = simulator != null && simulator.isActiveAndEnabled && simulator.ThrottleHeld;
        // One press acquires the normal shared lease; a denied/lost lease needs a
        // fresh press, so a held key cannot steal another player's control later.
        if (throttleHeld && !throttleWasHeld && driveHand == null && !simulatedDrive && handle != null &&
            !RailWorld.InputBlocked && !RailExpedition.LocalDown && PhotonSession.Instance != null &&
            PhotonSession.Instance.WorldReady && PhotonSession.Phase == RailPhase.Running && PhotonSession.DriveGripActor == 0)
        {
            simulatedDrive = true;
            BeginDrive();
        }
        throttleWasHeld = throttleHeld;
        if (driveHand != null || simulatedDrive)
        {
            var state = PhotonSession.DriveGripState;
            bool granted = PhotonNetwork.LocalPlayer != null && PhotonSession.DriveGripActor == PhotonNetwork.LocalPlayer.ActorNumber && (string)state[2] == gripToken;
            bool released = simulatedDrive ? !throttleHeld :
                !driveHand.isActiveAndEnabled || !driveHand.IsGripHeld || !driveHand.CanGrab ||
                drivePalm == null || Vector3.Distance(drivePalm.position, handle.TransformPoint(gripPalmLocal)) > BreakDistance;
            if (released || RailWorld.InputBlocked || RailExpedition.LocalDown ||
                PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady || PhotonSession.Phase != RailPhase.Running ||
                gripRun != PhotonSession.RunId ||
                (gripGranted && !granted) || (!granted && Time.unscaledTime - gripStarted > 1.5f)) ReleaseDrive();
            else
            {
                gripGranted |= granted;
                float travel = simulatedDrive ? 1 : GripTravel(gripStartProgress, gripStartAngle, transform.InverseTransformPoint(drivePalm.position));
                bool wasForward = forwardLatched;
                forwardLatched = RailDriveLease.ForwardDetent(forwardLatched, travel);
                driveProgress = forwardLatched ? 1 : travel;
                if (wasForward != forwardLatched)
                {
                    nextHeartbeat = 0; // Send STOP/GO edges immediately, not on the heartbeat timer.
                    if (driveHand != null && gripGranted) DetentFeedback();
                }
                if (Time.unscaledTime >= nextHeartbeat)
                {
                    nextHeartbeat = Time.unscaledTime + .15f;
                    PhotonSession.Instance.RequestDriveGrip(gripRun, gripToken, 1, driveProgress);
                }
            }
        }
        var motion = PhotonSession.Get(RailSessionRules.Motion, new object[] { 0d, 0f, 0f, 0f });
        float progress = PhotonSession.DriveGripActor != 0 ? (float)PhotonSession.DriveGripState[3] : ((float)motion[3] > 0 ? 1 : 0);
        bool awaitingRelease = !string.IsNullOrEmpty(releasedToken) && (string)PhotonSession.DriveGripState[2] == releasedToken;
        if (awaitingRelease) progress = 0;
        if (driveHand != null || simulatedDrive) progress = driveProgress;
        if (progress == 0 && driveHand == null && !simulatedDrive && (PhotonSession.DriveGripActor == 0 || awaitingRelease))
        {
            if (!returning) { returning = true; returnStart = renderedProgress; returnElapsed = 0; }
            returnElapsed += Time.unscaledDeltaTime;
            PoseDrive(ReturnProgress(returnStart, returnElapsed, returnDuration));
        }
        else { returning = false; PoseDrive(progress); }
    }
    static float LeverAngle(Vector3 point) => Mathf.Atan2(point.z, point.y) * Mathf.Rad2Deg;
    public static float ArcTravel(float startAngle, Vector3 point) =>
        Mathf.Clamp01(Mathf.DeltaAngle(startAngle, LeverAngle(point)) / 70f);
    public static float GripTravel(float startProgress, float startAngle, Vector3 point) =>
        Mathf.Clamp01(startProgress + Mathf.DeltaAngle(startAngle, LeverAngle(point)) / 70f);
    public static float ReturnProgress(float start, float elapsed, float duration) =>
        duration <= 0 ? 0 : Mathf.Lerp(start, 0, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / duration)));

    // Only the rendered palm is constrained. Tracking remains untouched for release,
    // locomotion and travel, so the attached hand can never sustain its own GO input.
    public static bool TryGetDrivePalm(Transform trackedPalm, out Vector3 position, out Quaternion rotation)
    {
        position = default; rotation = Quaternion.identity;
        foreach (var control in drives)
        {
            if (control.driveHand == null || !control.gripGranted || control.drivePalm != trackedPalm ||
                !control.driveHand.IsGripHeld || !control.driveHand.CanGrab || RailWorld.InputBlocked) continue;
            var contact = control.handle.TransformPoint(control.gripPalmLocal);
            if (Vector3.Distance(trackedPalm.position, contact) > BreakDistance) return false;
            position = contact + control.handle.TransformDirection(control.palmOffset);
            rotation = control.handle.rotation * control.gripPalmRotation;
            return true;
        }
        return false;
    }
    void DetentFeedback()
    {
        // The throttle sounds (clicks and latch) come from ThrottleClicks as the lever moves.
        VibrationManager.instance?.TriggerVibrationPulse(.65f, .55f, .055f, driveHand);
    }
    void PoseDrive(float progress)
    {
        if (handle == null) return;
        renderedProgress = progress;
        ThrottleClicks(progress);
        handle.localRotation = Quaternion.Euler(Mathf.Lerp(-35, 35, progress), 0, 0);
        handle.localPosition = handle.localRotation * handleRest;
    }
    public void Activate()
    {
        if (RailExpedition.LocalDown) return;
        if (action == Action.Drive)
        { var motion = PhotonSession.Get(RailSessionRules.Motion, new object[] { 0d, 0f, 0f, 0f }); PhotonSession.Instance.SetTrainMoving((float)motion[3] <= 0); }

    }
    public static AudioClip Tone(string name, float hz, float seconds)
    {
        const int rate = 24000;
        var data = new float[(int)(rate * seconds)];
        for (int i = 0; i < data.Length; i++)
        { float t = (float)i / rate; float envelope = Mathf.Min(1, t * 20) * Mathf.Min(1, (seconds - t) * 10); data[i] = envelope * (.4f * Mathf.Sin(2 * Mathf.PI * hz * t) + .15f * Mathf.Sin(2 * Mathf.PI * hz * 1.5f * t)); }
        var clip = AudioClip.Create(name, data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
    }
}


