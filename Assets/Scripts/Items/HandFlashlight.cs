using Photon.Pun;
using UnityEngine;

/// <summary>
/// A handheld light. Any use button on the hand holding it toggles it (see PlayerHand.WasUsePressed).
/// The owner's on/off state is synced so everyone sees the light.
/// Releasing or stashing resets the light; picking it up leaves it off.
/// </summary>
[RequireComponent(typeof(PhotonGrabbable))]
public sealed class HandFlashlight : MonoBehaviour, IPunObservable
{
    [SerializeField] Light spotLight;

    // Photon accepts objects. Reuse immutable Boolean boxes instead of allocating each sync.
    static readonly object OnState = true;
    static readonly object OffState = false;

    PhotonGrabbable grabbable;
    ItemSounds sounds;
    bool isOn;
    LightEmissionState emission;
    LightRenderMode normalRenderMode;

    void Awake()
    {
        grabbable = GetComponent<PhotonGrabbable>();
        sounds = GetComponent<ItemSounds>();
        if (spotLight != null)
            normalRenderMode = spotLight.renderMode;
        emission = new LightEmissionState(this);
        ApplyLight();
    }

    void Update()
    {
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
        grabbable.OnGrabbed += ResetLight;
        grabbable.OnReleased += HandleReleased;
        UpdateLightPriority();
    }

    void OnDisable()
    {
        grabbable.OnGrabbed -= ResetLight;
        grabbable.OnReleased -= HandleReleased;
        ResetLight(null);
    }

    void HandleReleased(PlayerHand hand)
    {
        bool wasOn = isOn;
        ResetLight(hand);
        // Same networked TurnOff path as a manual toggle; remotes hear via RoomRpc.
        if (wasOn && sounds != null)
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
        if (!grabbable.IsHeldByAnyPlayer || grabbable.IsStashed)
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
