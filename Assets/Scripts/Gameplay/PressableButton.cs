using UnityEngine;
using UnityEngine.Events;

/// <summary>Local momentary button. Only the cap moves; the touch volume stays fixed.</summary>
[RequireComponent(typeof(BoxCollider))]
[DefaultExecutionOrder(75)] // After PlayerHand (50), before RailHoldControl (100).
public sealed class PressableButton : MonoBehaviour
{
    public Transform cap;
    public Vector3 pressOffset = new Vector3(0, 0, .025f);
    public float speed = .3f;
    public bool interactable = true;
    [Tooltip("Local diagnostic UI remains usable while downed/loading. Never enable on gameplay controls.")]
    public bool diagnostic;
    public UnityEvent onPressed = new UnityEvent();
    public UnityEvent onReleased = new UnityEvent();
    public AudioSource audioSource;
    Vector3 rest;
    BoxCollider volume;
    bool touching, pressed;
    float showUntil;
    public bool IsPressed => pressed;

    void Awake() { volume = GetComponent<BoxCollider>(); if (cap != null) rest = cap.localPosition; }
    public bool Press()
    {
        if (!isActiveAndEnabled || !interactable || pressed) return false;
        pressed = true;
        showUntil = Time.unscaledTime + .1f;
        if (audioSource != null) audioSource.Play();
        onPressed.Invoke();
        return true;
    }
    public void Release()
    {
        if (!pressed) return;
        pressed = false;
        onReleased.Invoke();
    }
    void LateUpdate()
    {
        var player = GR.Player.instance;
        bool available = diagnostic || (!RailWorld.InputBlocked && !RailExpedition.LocalDown);
        bool contact = player != null && available &&
            (Touches(player.leftPlayerHand) || Touches(player.rightPlayerHand));
        // Require withdrawal before another press, including after a disabled contact.
        if (contact && !touching) Press();
        if (!contact && touching) Release();
        touching = contact;
        if (!interactable || !available) Release();
        if (cap != null) cap.localPosition = Vector3.MoveTowards(cap.localPosition,
            rest + ((pressed || Time.unscaledTime < showUntil) ? pressOffset : Vector3.zero), speed * Time.unscaledDeltaTime);
    }
    bool Touches(PlayerHand hand)
    {
        if (hand == null || !hand.isActiveAndEnabled || hand.IsDesktopInput || (!diagnostic && !hand.CanGrab)) return false;
        return hand.TouchesButton(volume);
    }
    void OnDisable()
    {
        Release(); touching = false; showUntil = 0;
        if (cap != null) cap.localPosition = rest;
    }
    public static PressableButton Aim(Ray ray, float reach, int mask)
    {
        PressableButton button = null;
        float nearest = reach;
        foreach (var hit in Physics.RaycastAll(ray, reach, mask, QueryTriggerInteraction.Collide))
        {
            var candidate = hit.collider.GetComponentInParent<PressableButton>();
            if (hit.collider.isTrigger && candidate == null) continue;
            if (hit.distance > nearest) continue;
            nearest = hit.distance;
            button = candidate;
        }
        return button;
    }
}
