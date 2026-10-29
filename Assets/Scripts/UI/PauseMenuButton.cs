using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PauseMenuButton : MonoBehaviour
{
    [Tooltip("Local Z pushed in on the latched button. A bit under half the button thickness.")]
    public float pressDepth = 0.012f;
    [Tooltip("Snaps in when the menu opens.")]
    public bool selectOnEnable;
    [Tooltip("Presses in on contact and pops back out when the finger leaves. Left out of the sibling radio group.")]
    public bool momentary;
    [Tooltip("Keeps its pressed pose when the menu opens, and does not switch the pause tab.")]
    public bool preserveLatch;
    public UnityEvent pressedIn = new UnityEvent();
    public UnityEvent pressedOut = new UnityEvent();

    const float PressSpeed = 0.1f;

    BoxCollider box;
    Vector3 restLocalPosition;
    bool restReady;
    PauseMenu menu;
    PauseMenuButton[] group = System.Array.Empty<PauseMenuButton>();
    bool latched;
    int playerLayer = -1;
    readonly List<Collider> contacts = new List<Collider>();

    void Awake() {
        // The collider child cancels the menu's visual reflection without changing its layout.
        box = GetComponentInChildren<BoxCollider>(true);
        restLocalPosition = transform.localPosition;
        restReady = true;
        menu = GetComponentInParent<PauseMenu>(true);
        playerLayer = LayerMask.NameToLayer("Player");
        if (box != null && playerLayer >= 0) {
            box.includeLayers = 1 << playerLayer;
        }
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null) {
            body = gameObject.AddComponent<Rigidbody>();
        }
        body.isKinematic = true;
        body.useGravity = false;
        CacheGroup();
    }

    void OnEnable() {
        if (momentary) {
            latched = false;
            SnapPose();
            return;
        }

        if (preserveLatch) {
            SnapPose();
            return;
        }

        if (IsSolo()) {
            // Keep the toggle. selectOnEnable stays off, so opening the menu does not press it.
            SnapPose();
            return;
        }

        bool changed = latched != selectOnEnable;
        latched = selectOnEnable;
        SnapPose();
        if (changed) {
            InvokeLatch(latched);
        }
        if (selectOnEnable && menu != null) {
            menu.SetActiveButton(this);
        }
    }

    void OnDisable() {
        contacts.Clear();
        if (momentary && latched) {
            latched = false;
            if (restReady) {
                SnapPose();
            }
        }
    }

    void LateUpdate() {
        Depress(PressSpeed * Time.deltaTime);
    }

    // A lone button toggles on the first Player collider and ignores further overlaps until all have left.
    // Sibling buttons latch on enter and stay latched after the finger leaves.
    // Momentary keys press in on the first contact and pop back out when the last one leaves.
    void OnTriggerEnter(Collider other) {
        if (!IsPlayer(other) || contacts.Contains(other)) {
            return;
        }

        contacts.Add(other);
        if (contacts.Count == 1) {
            Press();
        }
    }

    // Shared by physical pokes and desktop clicks.
    public void Press() {
        if (!isActiveAndEnabled) return;
        PlayPress();
        if (momentary) {
            SetLatched(true);
            return;
        }

        // Settings rows clear momentary in the parent Awake, which can run after this
        // button already cached an empty group. A poke rebuilds it before latching.
        if (group.Length == 0)
            CacheGroup();

        if (IsSolo()) {
            Toggle();
            return;
        }

        if (latched) {
            return;
        }

        for (int i = 0; i < group.Length; i++) {
            group[i].SetLatched(group[i] == this);
        }
        if (!preserveLatch && menu != null) {
            menu.SetActiveButton(this);
        }
    }

    void OnTriggerExit(Collider other) {
        if (!IsPlayer(other)) {
            return;
        }
        contacts.Remove(other);
        Release();
    }

    public void Release() {
        if (momentary && contacts.Count == 0) {
            SetLatched(false);
        }
    }

    bool IsPlayer(Collider other) {
        return other != null && other.gameObject.layer == playerLayer;
    }

    public void RebuildGroup() {
        CacheGroup();
    }

    void CacheGroup() {
        Transform parent = transform.parent;
        if (parent == null) {
            group = momentary ? System.Array.Empty<PauseMenuButton>() : new[] { this };
            return;
        }

        int count = 0;
        for (int i = 0; i < parent.childCount; i++) {
            PauseMenuButton button = parent.GetChild(i).GetComponent<PauseMenuButton>();
            if (button != null && !button.momentary) {
                count++;
            }
        }

        group = new PauseMenuButton[count];
        int index = 0;
        for (int i = 0; i < parent.childCount; i++) {
            PauseMenuButton button = parent.GetChild(i).GetComponent<PauseMenuButton>();
            if (button != null && !button.momentary) {
                group[index++] = button;
            }
        }
    }

    bool IsSolo() {
        return group.Length == 1;
    }

    void Toggle() {
        SetLatched(!latched);
    }

    void SetLatched(bool value) {
        if (latched == value) {
            return;
        }
        latched = value;
        InvokeLatch(latched);
    }

    public void ShowPressed(bool pressed) {
        latched = pressed;
        // Pause rows are built while the menu mesh is still inactive, so Awake has
        // not stored the rest pose. Snapping here would write local zero, and Awake
        // would then keep that. OnEnable snaps once the rest pose is known.
        if (restReady) {
            SnapPose();
        }
    }

    void PlayPress() {
        if (menu != null) {
            menu.PlayButtonPress(transform.position);
        }
    }

    void InvokeLatch(bool pressed) {
        UnityEvent latchEvent = pressed ? pressedIn : pressedOut;
        if (latchEvent != null) {
            latchEvent.Invoke();
        }
    }

    void SnapPose() {
        Vector3 position = restLocalPosition;
        if (latched) {
            position.z -= pressDepth;
        }
        transform.localPosition = position;
    }

    void Depress(float step) {
        Vector3 target = restLocalPosition;
        if (latched) {
            target.z -= pressDepth;
        }
        transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, step);
    }
}
