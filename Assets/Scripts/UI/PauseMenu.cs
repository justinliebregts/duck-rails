using Game.Audio;
using UnityEngine;
using UnityEngine.Events;

public class PauseMenu : MonoBehaviour
{
    public GameObject mesh;
    public Transform screen;
    public Transform playersButton;
    public Transform roomsButton;
    public Transform settingsButton;
    public Transform cameraButton;

    public PlayersMenu playersMenu;
    public RoomMenu roomMenu;
    public SettingsMenu settingsMenu;
    public GameObject cameraPage;
    public GameObject cameraTablet;

    [SerializeField] SoundEvent buttonPressSound;
    [SerializeField] SoundEvent openSound;
    [SerializeField] SoundEvent closeSound;
    [SerializeField] BoxCollider grabCollider;

    public float distance = 1.2f;
    [Tooltip("Metres above the head. Negative lowers the menu.")]
    public float height = -0.15f;
    public float dismissDistance = 2f;
    [Tooltip("World-space gap in metres between the pause menu top and camera tablet bottom.")]
    [Min(0f)] public float tabletGap = 0.08f;

    const float ScreenTextGap = 0.002f;
    const float ScreenTextInset = 0.01f;

    bool paused = false;
    public bool IsOpen => paused && mesh != null && mesh.activeInHierarchy;
    PauseMenuButton activeButton;
    Grabbable grabbable;
    Rigidbody body;

    void Awake() {
        BindCameraButton("Open", OpenCameraTablet);
        BindCameraButton("Close", CloseCameraTablet);
        body = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
        if (grabbable != null) {
            grabbable.OnGrabbed += OnMenuGrabbed;
            grabbable.OnReleased += OnMenuReleased;
        }
    }

    void OnDestroy() {
        if (grabbable == null) {
            return;
        }

        grabbable.OnGrabbed -= OnMenuGrabbed;
        grabbable.OnReleased -= OnMenuReleased;
    }

    void OnEnable() {
        if (playersMenu != null) {
            playersMenu.RegisterCallbacks();
        }
    }

    void OnDisable() {
        if (paused) {
            paused = false;
            ReleaseMenuIfHeld();
            StopMenuBody();
            if (mesh != null) {
                mesh.SetActive(false);
            }
            PlayMenuSound(closeSound, transform.position);
        }
        if (playersMenu != null) {
            playersMenu.UnregisterCallbacks();
        }
    }

    private void Start() {
        mesh.SetActive(false);
        roomMenu.gameObject.SetActive(false);
        settingsMenu.gameObject.SetActive(false);
        if (cameraPage != null) {
            cameraPage.SetActive(false);
        }
        if (cameraTablet != null) {
            cameraTablet.SetActive(false);
        }
    }

    private void Update() {
        if (RailDiagnosticPanel.OwnsMenuInput && !IsOpen) return;
        if (GR.Player.instance == null)
            return;
        if (GR.Player.instance.IsDead) {
            CloseForDeath();
            return;
        }
        bool wasPressed = false;
        if ((GR.Player.instance.ReturnButtonPress_Left(ButtonName.Start).wasPressed || GR.Player.instance.ReturnButtonPress_Right(ButtonName.Start).wasPressed)
#if UNITY_EDITOR || UNITY_STANDALONE
            || Input.GetKeyDown(KeyCode.P)
#endif
            ) {
            wasPressed = true;
        }
        if (wasPressed) {
            if (RailDiagnosticPanel.OwnsMenuInput) CloseForDeath(); else TogglePause();
        }

        if (!paused || (grabbable != null && grabbable.IsHeld)) {
            return;
        }

        Camera head = Camera.main;
        if (head == null || mesh == null) {
            return;
        }

        float dismissDistanceSq = dismissDistance * dismissDistance;
        if ((head.transform.position - mesh.transform.position).sqrMagnitude >= dismissDistanceSq) {
            TogglePause();
        }
    }

    public void PlayButtonPress(Vector3 position) {
        PlayMenuSound(buttonPressSound, position);
    }

    static void PlayMenuSound(SoundEvent sound, Vector3 position) {
        if (sound == null || AudioManager.instance == null) {
            return;
        }

        AudioManager.instance.Play(sound, position);
    }

    public void SetActiveButton(PauseMenuButton button) {
        activeButton = button;
        if (playersMenu != null) {
            playersMenu.gameObject.SetActive(activeButton != null && activeButton.transform == playersButton);
        }
        roomMenu.gameObject.SetActive(activeButton != null && activeButton.transform == roomsButton);
        settingsMenu.gameObject.SetActive(activeButton != null && activeButton.transform == settingsButton);
        if (cameraPage != null) {
            cameraPage.SetActive(activeButton != null && activeButton.transform == cameraButton);
        }
    }

    public void OpenCameraTablet() {
        Vector3 position;
        Quaternion rotation;
        if (!TryGetTabletPose(out position, out rotation)) {
            return;
        }

        if (cameraTablet == null) {
            return;
        }

        ReleaseTabletIfHeld();
        cameraTablet.SetActive(true);
        cameraTablet.transform.SetParent(null, true);
        cameraTablet.transform.SetPositionAndRotation(position, rotation);
        SnapTabletBody(position, rotation);
    }

    public void CloseCameraTablet() {
        if (cameraTablet == null) {
            return;
        }

        ReleaseTabletIfHeld();
        cameraTablet.SetActive(false);
    }

    void BindCameraButton(string childName, UnityAction action) {
        if (cameraPage == null) {
            return;
        }

        Transform child = cameraPage.transform.Find(childName);
        if (child == null) {
            return;
        }

        PauseMenuButton button = child.GetComponent<PauseMenuButton>();
        if (button == null) {
            return;
        }

        button.momentary = true;
        button.pressedIn.AddListener(action);
    }

    void OnMenuGrabbed(PlayerHand hand) {
        if (body != null) {
            body.detectCollisions = true;
        }

        if (grabCollider != null) {
            grabCollider.enabled = false;
        }
    }

    void OnMenuReleased(PlayerHand hand) {
        // Grabbable supplies gravity and release velocity; ItemSounds handles impacts.
        if (grabCollider != null) {
            grabCollider.enabled = true;
        }
    }

    void StopMenuBody() {
        if (body != null) {
            if (!body.isKinematic) {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            body.useGravity = false;
            body.isKinematic = true;
        }
    }

    void ReleaseMenuIfHeld() {
        if (grabbable != null && grabbable.IsHeld && grabbable.Holder != null) {
            grabbable.Holder.Release();
        }
    }

    void ReleaseTabletIfHeld() {
        if (cameraTablet == null) {
            return;
        }

        Grabbable grabbable = cameraTablet.GetComponent<Grabbable>();
        if (grabbable != null && grabbable.IsHeld && grabbable.Holder != null) {
            grabbable.Holder.Release();
        }
    }

    void SnapTabletBody(Vector3 position, Quaternion rotation) {
        Rigidbody body = cameraTablet.GetComponent<Rigidbody>();
        if (body != null) {
            body.position = position;
            body.rotation = rotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.useGravity = false;
            body.isKinematic = true;
        }

        TabletGrab grab = cameraTablet.GetComponent<TabletGrab>();
        if (grab != null) {
            grab.StopFollowMotion();
        }
    }

    public void CloseForDeath() {
        if (!paused)
            return;

        paused = false;
        ReleaseMenuIfHeld();
        StopMenuBody();
        if (roomMenu != null)
            roomMenu.ClearEnteredCode();
        if (mesh != null)
            mesh.SetActive(false);
        PlayMenuSound(closeSound, transform.position);
    }

    void TogglePause() {
        if (GR.Player.instance == null || GR.Player.instance.IsDead)
            return;
        paused = !paused;
        if (paused) {
            PlaceInFrontOfPlayer();
            if (playersMenu != null) {
                playersMenu.SyncPlayerRows();
            }
        } else {
            ReleaseMenuIfHeld();
            StopMenuBody();
            if (roomMenu != null) {
                roomMenu.ClearEnteredCode();
            }

            PhotonSession.Ensure().JoinOpenRoom();
        }

        mesh.SetActive(paused);
        PlayMenuSound(paused ? openSound : closeSound, transform.position);
    }

    public void OpenFromDiagnostics() {
        if (!paused) TogglePause();
    }

    void PlaceInFrontOfPlayer() {
        if (mesh == null) {
            return;
        }

        Vector3 position;
        Quaternion rotation;
        if (!TryGetHeadPose(0f, out position, out rotation)) {
            return;
        }

        StopMenuBody();
        transform.SetPositionAndRotation(position, rotation);
        if (body != null) {
            body.position = position;
            body.rotation = rotation;
        }
    }

    bool TryGetHeadPose(float side, out Vector3 position, out Quaternion rotation) {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        Camera head = Camera.main;
        if (head == null) {
            return false;
        }

        Vector3 flatForward = Vector3.ProjectOnPlane(head.transform.forward, Vector3.up);
        if (flatForward.sqrMagnitude < 0.0001f) {
            flatForward = Vector3.ProjectOnPlane(head.transform.up, Vector3.up);
        }
        if (flatForward.sqrMagnitude < 0.0001f) {
            return false;
        }

        flatForward.Normalize();
        Vector3 right = Vector3.Cross(Vector3.up, flatForward);
        position = head.transform.position + flatForward * distance + Vector3.up * height + right * side;
        rotation = Quaternion.LookRotation(-flatForward, Vector3.up);
        return true;
    }

    // Local bounds also work for an inactive tablet, before its world bounds refresh.
    static Bounds GetVisualBounds(GameObject root, Quaternion rotation) {
        Bounds bounds = new Bounds(Vector3.zero, Vector3.zero);
        bool any = false;
        Quaternion delta = rotation * Quaternion.Inverse(root.transform.rotation);
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true)) {
            Bounds local = renderer.localBounds;
            for (int corner = 0; corner < 8; corner++) {
                Vector3 point = local.center + Vector3.Scale(local.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f,
                    (corner & 2) == 0 ? -1f : 1f,
                    (corner & 4) == 0 ? -1f : 1f));
                point = delta * (renderer.transform.TransformPoint(point) - root.transform.position);
                if (!any) {
                    bounds = new Bounds(point, Vector3.zero);
                    any = true;
                } else {
                    bounds.Encapsulate(point);
                }
            }
        }
        return bounds;
    }

    bool TryGetTabletPose(out Vector3 position, out Quaternion rotation) {
        position = Vector3.zero;
        rotation = Quaternion.identity;
        Camera head = Camera.main;
        if (head == null || mesh == null || cameraTablet == null) {
            return false;
        }

        Bounds menuBounds = GetVisualBounds(mesh, mesh.transform.rotation);
        Vector3 menuCenter = mesh.transform.position + menuBounds.center;
        Vector3 facePlayer = Vector3.ProjectOnPlane(head.transform.position - menuCenter, Vector3.up);
        if (facePlayer.sqrMagnitude < 0.0001f) {
            facePlayer = Vector3.ProjectOnPlane(-head.transform.forward, Vector3.up);
        }
        if (facePlayer.sqrMagnitude < 0.0001f) {
            facePlayer = Vector3.forward;
        }
        rotation = Quaternion.LookRotation(facePlayer, Vector3.up);

        Bounds tabletBounds = GetVisualBounds(cameraTablet, rotation);
        position = menuCenter - tabletBounds.center;
        position.y = mesh.transform.position.y + menuBounds.max.y
            + Mathf.Max(0f, tabletGap) - tabletBounds.min.y;
        return true;
    }
}
