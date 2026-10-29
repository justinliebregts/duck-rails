using System.Collections.Generic;
using Game.Animation;
using TMPro;
using UnityEngine;

// Finger drag along a track while that hand holds Grip or Trigger. Only one slider moves at a time.
// The knob follows the hand, the value snaps to an integer, and releasing the button or the hand leaves the knob there.
public class PauseMenuSlider : MonoBehaviour
{
    public enum Setting
    {
        SmoothStrength,
        RotateDeadzone,
        GameVolume,
        ChatVolume
    }

    public int minValue;
    public int maxValue = 1;
    public int defaultValue;
    public string prefsKey;
    public Setting setting;
    [Tooltip("Knob center at the minimum value. Matches the left snap-angle button.")]
    public float minX = -0.0165f;
    [Tooltip("Knob center at the maximum value. Matches the right snap-angle button.")]
    public float maxX = 0.2165f;
    public Transform knob;
    public TMP_Text valueText;

    // Finger colliders sit ahead of the controller. Farther contacts are the body or head.
    const float HandMatchDistance = 0.4f;

    static PauseMenuSlider activeSlider;

    BoxCollider box;
    SettingsMenu settings;
    PauseMenu menu;
    int playerLayer = -1;
    readonly List<Collider> contacts = new List<Collider>();
    readonly HashSet<Collider> cancelledContacts = new HashSet<Collider>();
    int value;
    bool dirty;
    bool adjusting;

    void Awake() {
        // Keep slider coordinates on this transform; only the collider cancels the visual reflection.
        box = GetComponentInChildren<BoxCollider>(true);
        playerLayer = LayerMask.NameToLayer("Player");
        if (box != null && playerLayer >= 0)
            box.includeLayers = 1 << playerLayer;
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
            body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        settings = GetComponentInParent<SettingsMenu>(true);
        menu = GetComponentInParent<PauseMenu>(true);
    }

    void OnEnable() {
        ForgetSeparatedContacts();
        if (UserPlayerPrefs.IsReady)
            ShowSaved();
        else {
            Show(defaultValue, push: true);
            UserPlayerPrefs.UserReady += OnPrefsReady;
        }
    }

    void OnDisable() {
        UserPlayerPrefs.UserReady -= OnPrefsReady;
        CancelDrag();
    }

    void CancelDrag() {
        foreach (Collider contact in contacts)
            if (contact != null)
                cancelledContacts.Add(contact);
        contacts.Clear();
        adjusting = false;
        ReleaseClaim();
        Release();
    }

    void OnPrefsReady() {
        UserPlayerPrefs.UserReady -= OnPrefsReady;
        ShowSaved();
    }

    void Update() {
        if (!CanInteract()) {
            CancelDrag();
            return;
        }
        ForgetSeparatedContacts();
        for (int i = contacts.Count - 1; i >= 0; i--) {
            if (contacts[i] == null)
                contacts.RemoveAt(i);
        }
        Collider hand = HoldingContact();
        if (hand == null || !CanAdjust()) {
            if (!adjusting)
                return;

            adjusting = false;
            ReleaseClaim();
            Release();
            return;
        }

        adjusting = true;
        activeSlider = this;
        Follow(hand);
    }

    void OnTriggerEnter(Collider other) {
        if (!CanInteract() || !IsPlayer(other) || cancelledContacts.Contains(other) || contacts.Contains(other))
            return;

        contacts.Add(other);
    }

    void OnTriggerExit(Collider other) {
        if (CanInteract())
            cancelledContacts.Remove(other);
        if (!IsPlayer(other))
            return;

        contacts.Remove(other);
        if (adjusting && HoldingContact() == null) {
            adjusting = false;
            ReleaseClaim();
            Release();
        }
    }

    bool CanAdjust() {
        return activeSlider == null || activeSlider == this;
    }

    bool CanInteract() {
        return isActiveAndEnabled && menu != null && menu.IsOpen
            && GR.Player.instance != null && !GR.Player.instance.IsDead;
    }

    void ForgetSeparatedContacts() {
        // A disabled trigger may never receive Exit. Keep blocking fingers still
        // overlapping on reopen, but forget ones that moved away while hidden.
        if (box == null)
            return;
        cancelledContacts.RemoveWhere(contact => contact == null || !contact.enabled
            || !contact.gameObject.activeInHierarchy
            || !Physics.ComputePenetration(box, box.transform.position, box.transform.rotation,
                contact, contact.transform.position, contact.transform.rotation, out _, out _));
    }

    void ReleaseClaim() {
        if (activeSlider == this)
            activeSlider = null;
    }

    Collider HoldingContact() {
        for (int i = contacts.Count - 1; i >= 0; i--) {
            if (IsHolding(contacts[i]))
                return contacts[i];
        }
        return null;
    }

    bool IsHolding(Collider other) {
        PlayerHand hand = HandFor(other);
        GR.Player player = GR.Player.instance;
        if (hand == null || player == null || player.IsDead)
            return false;

        bool left = hand.IsControllerLTouch;
        ButtonStatus grip = left
            ? player.ReturnButtonPress_Left(ButtonName.Grip)
            : player.ReturnButtonPress_Right(ButtonName.Grip);
        ButtonStatus trigger = left
            ? player.ReturnButtonPress_Left(ButtonName.Trigger)
            : player.ReturnButtonPress_Right(ButtonName.Trigger);
        return grip.down || trigger.down;
    }

    PlayerHand HandFor(Collider other) {
        if (other == null)
            return null;

        PlayerHand onHand = other.GetComponentInParent<PlayerHand>();
        if (onHand != null)
            return onHand;

        GR.Player player = GR.Player.instance;
        if (player == null)
            return null;

        Transform hit = other.transform;
        LocalBodyDriver driver = hit.GetComponentInParent<LocalBodyDriver>();
        RemotePlayerBody body = driver != null ? driver.Body : null;
        if (body != null) {
            if (OnBone(hit, body.LeftHand))
                return player.leftPlayerHand;
            if (OnBone(hit, body.RightHand))
                return player.rightPlayerHand;
        }

        Vector3 point = other.bounds.center;
        float bestSq = HandMatchDistance * HandMatchDistance;
        PlayerHand nearest = null;
        nearest = CloserHand(player.leftPlayerHand, point, nearest, ref bestSq);
        nearest = CloserHand(player.rightPlayerHand, point, nearest, ref bestSq);
        return nearest;
    }

    static bool OnBone(Transform hit, Transform bone) {
        return bone != null && (hit == bone || hit.IsChildOf(bone));
    }

    static PlayerHand CloserHand(PlayerHand hand, Vector3 point, PlayerHand nearest, ref float bestSq) {
        if (hand == null)
            return nearest;

        float sq = (hand.transform.position - point).sqrMagnitude;
        if (sq > bestSq)
            return nearest;

        bestSq = sq;
        return hand;
    }

    bool IsPlayer(Collider other) {
        return other != null && other.gameObject.layer == playerLayer;
    }

    void Follow(Collider hand) {
        float x = transform.InverseTransformPoint(hand.bounds.center).x;
        x = Mathf.Clamp(x, minX, maxX);
        PlaceKnob(x);

        int next = XToValue(x);
        if (next == value)
            return;

        value = next;
        dirty = true;
        if (valueText != null)
            valueText.text = value.ToString();
        UserPlayerPrefs.SetInt(prefsKey, value);
        Push();
    }

    void Release() {
        PlaceKnob(ValueToX(value));
        if (!dirty)
            return;

        dirty = false;
        UserPlayerPrefs.Save();
    }

    void ShowSaved() {
        Show(UserPlayerPrefs.GetInt(prefsKey, defaultValue), push: true);
    }

    void Show(int next, bool push) {
        value = Mathf.Clamp(next, minValue, maxValue);
        PlaceKnob(ValueToX(value));
        if (valueText != null)
            valueText.text = value.ToString();
        if (push)
            Push();
    }

    void Push() {
        if (settings != null)
            settings.ApplySlider(setting, value);
    }

    int XToValue(float x) {
        if (maxX <= minX || maxValue <= minValue)
            return minValue;

        float t = Mathf.InverseLerp(minX, maxX, x);
        return Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(minValue, maxValue, t)), minValue, maxValue);
    }

    float ValueToX(int amount) {
        if (maxValue <= minValue)
            return minX;

        float t = (amount - minValue) / (float)(maxValue - minValue);
        return Mathf.Lerp(minX, maxX, t);
    }

    void PlaceKnob(float x) {
        if (knob == null)
            return;

        Vector3 position = knob.localPosition;
        position.x = x;
        knob.localPosition = position;
    }
}
