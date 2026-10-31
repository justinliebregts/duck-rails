using System;
using Photon.Pun;
using UnityEngine;

/// <summary>Grip-and-hold drive, with a hold-T shortcut in desktop Keyboard or VR mode.</summary>
public sealed class RailControl : MonoBehaviour
{
    public enum Action { Drive, Horn, Bridge, Radio }
    public Action action;
    public Transform handle;
    public PressableButton holdButtonPrefab;
    AudioSource audioSource;
    AudioClip sound;
    bool touching;
    float nextUse;
    static readonly System.Collections.Generic.HashSet<RailControl> drives = new System.Collections.Generic.HashSet<RailControl>();
    PlayerHand driveHand;
    string gripToken, gripRun, releasedToken;
    float gripStartZ, gripStarted, nextHeartbeat, driveProgress;
    bool gripGranted, forwardLatched;
    bool simulatedDrive, throttleWasHeld;
    Vector3 handleRest;
    void OnEnable() { drives.Add(this); }
    void Awake() { if (handle != null) handleRest = handle.localPosition; }
    void Start()
    {
        ConfigureDriveColliders();
        if ((action == Action.Horn || action == Action.Radio) && GetComponent<RailHoldControl>() == null && holdButtonPrefab != null)
        {
            var button = Instantiate(holdButtonPrefab, transform);
            button.name = "Hold button";
            button.transform.localPosition = new Vector3(0, .22f, 0);
            button.transform.localRotation = Quaternion.Euler(90, 0, 0);
            button.transform.localScale = Vector3.one * 1.5f;
            var hold = gameObject.AddComponent<RailHoldControl>();
            hold.button = button; hold.radio = action == Action.Radio;
            var label = button.GetComponentInChildren<TMPro.TMP_Text>();
            label.text = hold.radio ? "HOLD\nTALK" : "HOLD\nHORN"; label.fontSize = .65f;
        }
        if (handle != null) handleRest = handle.localPosition;
    }
    void Update()
    {
        if (action == Action.Horn || action == Action.Radio) return;
        if (action == Action.Drive) UpdateDrive();
        if (!PhotonSession.OnTrain || GR.Player.instance == null) return;
        var player = GR.Player.instance;
        bool near = Vector3.Distance(player.transform.position, transform.position) < 3;
        bool hand = (player.leftPlayerHand != null && Vector3.Distance(player.leftPlayerHand.transform.position, transform.position) < .3f) ||
            (player.rightPlayerHand != null && Vector3.Distance(player.rightPlayerHand.transform.position, transform.position) < .3f);
        bool desktopUse = false;
        if (near && !RailWorld.InputBlocked && Input.GetKeyDown(KeyCode.E) && Camera.main != null &&
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
            control.gripStartZ = control.transform.InverseTransformPoint(hand.transform.position).z;
            control.BeginDrive();
            return control;
        }
        return null;
    }
    void BeginDrive()
    {
        gripRun = PhotonSession.RunId; gripToken = Guid.NewGuid().ToString("N");
        gripStarted = Time.unscaledTime; nextHeartbeat = 0;
        driveProgress = 0; gripGranted = forwardLatched = false;
        PhotonSession.Instance.RequestDriveGrip(gripRun, gripToken, 0, 0);
    }
    public void ReleaseDrive()
    {
        if (driveHand == null && !simulatedDrive) return;
        PhotonSession.Instance?.RequestDriveGrip(gripRun, gripToken, 2, 0);
        releasedToken = gripToken;
        driveHand = null; simulatedDrive = false; gripGranted = forwardLatched = false; driveProgress = 0;
        PoseDrive(0);
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
                Vector3.Distance(driveHand.transform.position, transform.position) > 1.5f;
            if (released || RailWorld.InputBlocked || RailExpedition.LocalDown ||
                PhotonSession.Instance == null || !PhotonSession.Instance.WorldReady || PhotonSession.Phase != RailPhase.Running ||
                gripRun != PhotonSession.RunId ||
                (gripGranted && !granted) || (!granted && Time.unscaledTime - gripStarted > 1.5f)) ReleaseDrive();
            else
            {
                gripGranted |= granted;
                float travel = simulatedDrive ? 1 : RailDriveLease.Travel(transform.InverseTransformPoint(driveHand.transform.position).z - gripStartZ);
                forwardLatched = RailDriveLease.ForwardDetent(forwardLatched, travel);
                driveProgress = forwardLatched ? 1 : (travel <= .2f ? 0 : travel);
                if (Time.unscaledTime >= nextHeartbeat)
                {
                    nextHeartbeat = Time.unscaledTime + .15f;
                    PhotonSession.Instance.RequestDriveGrip(gripRun, gripToken, 1, driveProgress);
                }
            }
        }
        var motion = PhotonSession.Get(RailSessionRules.Motion, new object[] { 0d, 0f, 0f, 0f });
        float progress = PhotonSession.DriveGripActor != 0 ? (float)PhotonSession.DriveGripState[3] : ((float)motion[3] > 0 ? 1 : 0);
        if (!string.IsNullOrEmpty(releasedToken) && (string)PhotonSession.DriveGripState[2] == releasedToken) progress = 0;
        if (driveHand != null || simulatedDrive) progress = gripGranted ? driveProgress : 0;
        PoseDrive(progress);
    }
    void PoseDrive(float progress)
    {
        if (handle == null) return;
        handle.localRotation = Quaternion.Euler(Mathf.Lerp(-35, 35, progress), 0, 0);
        handle.localPosition = handle.localRotation * handleRest;
    }
    public void Activate()
    {
        if (RailExpedition.LocalDown) return;
        if (action == Action.Drive)
        { var motion = PhotonSession.Get(RailSessionRules.Motion, new object[] { 0d, 0f, 0f, 0f }); PhotonSession.Instance.SetTrainMoving((float)motion[3] <= 0); }
        else if (action == Action.Bridge) RailExpedition.Instance.Request("bridge");

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


