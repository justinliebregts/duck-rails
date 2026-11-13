using Photon.Pun;
using UnityEngine;

/// <summary>
/// A handheld light. Any use button on the hand holding it toggles it (see PlayerHand.WasUsePressed).
/// The owner's on/off state is synced so everyone sees the light.
/// Releasing keeps the light on; it turns off only after being abandoned (not held) for abandonTimeout seconds.
/// Stashing turns it off.
/// </summary>
[RequireComponent(typeof(PhotonGrabbable))]
public sealed class HandFlashlight : MonoBehaviour, IPunObservable
{
    [SerializeField] Light spotLight;
    [Tooltip("Seconds a dropped flashlight stays on before its light turns off.")]
    [SerializeField] float abandonTimeout = 60f;

    // Photon accepts objects. Reuse immutable Boolean boxes instead of allocating each sync.
    static readonly object OnState = true;
    static readonly object OffState = false;

    PhotonGrabbable grabbable;
    PhotonView view;
    float abandonedAt = -1f;
    ItemSounds sounds;
    bool isOn;
    LightEmissionState emission;
    LightRenderMode normalRenderMode;

    void Awake()
    {
        grabbable = GetComponent<PhotonGrabbable>();
        view = GetComponent<PhotonView>();
        sounds = GetComponent<ItemSounds>();
        if (spotLight != null)
            normalRenderMode = spotLight.renderMode;
        emission = new LightEmissionState(this);
        ApplyLight();
    }

    void Update()
    {
        if (isOn && grabbable.IsHeld && !grabbable.IsStashed) RailStats.Flashlight(Time.deltaTime); // local holder only
        UpdateAbandonTimer();
        PlayerHand hand = grabbable.Holder;
        if (hand == null || grabbable.IsStashed || !hand.WasUsePressed)
            return;

        isOn = !isOn;
        if (sounds != null)
            sounds.Play(isOn ? ItemSound.TurnOn : ItemSound.TurnOff);
    }

    void LateUpdate()
    {
        ApplyLight();
    }

    void OnEnable()
    {
        grabbable.OnGrabbed += HandleGrabbed;
        grabbable.OnReleased += HandleReleased;
        UpdateLightPriority();
    }

    void OnDisable()
    {
        grabbable.OnGrabbed -= HandleGrabbed;
        grabbable.OnReleased -= HandleReleased;
        ResetLight(null);
    }

    void HandleGrabbed(PlayerHand hand)
    {
        abandonedAt = -1f; // a light left on stays on when picked back up
    }

    void HandleReleased(PlayerHand hand)
    {
        // Light stays on; the abandon timer starts counting.
        abandonedAt = isOn ? Time.time : -1f;
    }

    // Owner-only: remotes receive the state through OnPhotonSerializeView.
    void UpdateAbandonTimer()
    {
        if (!isOn || grabbable.IsHeldByAnyPlayer || grabbable.IsStashed)
        {
            abandonedAt = -1f;
            return;
        }
        if (abandonedAt < 0f)
            abandonedAt = Time.time;
        if (view != null && !view.IsMine)
            return;
        if (Time.time - abandonedAt < abandonTimeout)
            return;

        isOn = false;
        abandonedAt = -1f;
        if (sounds != null)
            sounds.Play(ItemSound.TurnOff);
    }

    void ResetLight(PlayerHand hand)
    {
        isOn = false;
        ApplyLight();
    }

    // Remote holders count too. Stashing hides renderers but does not disable lights.
    void ApplyLight()
    {
        if (grabbable.IsStashed)
            isOn = false;

        if (spotLight != null && spotLight.enabled != isOn)
            spotLight.enabled = isOn;
        UpdateLightPriority();
        emission.Apply(spotLight != null && spotLight.isActiveAndEnabled);
    }

    void UpdateLightPriority()
    {
        // IsHeld is local; remote holders must keep the prefab's normal priority.
        LightRenderMode desired = isActiveAndEnabled && grabbable.IsHeld && !grabbable.IsStashed
            ? LightRenderMode.ForcePixel : normalRenderMode;
        if (spotLight != null && spotLight.renderMode != desired)
            spotLight.renderMode = desired;
    }

    void OnDestroy()
    {
        emission?.Dispose();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            ApplyLight();
            stream.SendNext(isOn ? OnState : OffState);
        }
        else
            isOn = (bool)stream.ReceiveNext();
    }

    void OnValidate()
    {
        if (spotLight == null)
            spotLight = GetComponentInChildren<Light>(true);
    }
}
