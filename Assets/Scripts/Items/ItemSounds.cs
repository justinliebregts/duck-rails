using System.Collections.Generic;
using Game.Audio;
using Game.AI;
using Photon.Pun;
using UnityEngine;

public enum ItemSound : byte { Grab, Drop, Equip, TurnOn, TurnOff, Swing, FleshImpact }

// The owner broadcasts impacts. Held flesh hits are reported by swept hit detection.
[DisallowMultipleComponent]
[DefaultExecutionOrder(10010)] // Sample after hands and held bags have been posed.
public class ItemSounds : MonoBehaviour
{
    [Tooltip("A hand picks the item up: off the ground, out of a bag, or bought from the store.")]
    [SerializeField] SoundEvent grab;
    [Tooltip("Played on each qualifying collision while this item is loose.")]
    [SerializeField] SoundEvent drop;
    SoundEvent dropOverride;

    /// <summary>Null restores the serialized default; does not change impact or network behavior.</summary>
    public void SetDropOverride(SoundEvent sound) => dropOverride = sound;
    [Header("Impact sounds")]
    [Tooltip("Minimum relative speed into the surface in metres per second. Rejects gentle contacts and sliding.")]
    [Min(0f)] [SerializeField] float minimumImpactSpeed = 0.5f;
    [Tooltip("Minimum time in seconds between impact sounds from this item.")]
    [Min(0f)] [SerializeField] float impactSoundCooldown = 0.1f;
    [Tooltip("Sound for striking zombies or other players, held or thrown. Leave empty to disable.")]
    [SerializeField] SoundEvent fleshImpact;
    [Tooltip("Minimum interval between flesh impacts, shared by swept hits and physics contacts.")]
    [Min(0.01f)] [SerializeField] float fleshImpactCooldown = 0.1f;
    [Header("Items that can be worn")]
    [SerializeField] SoundEvent equip;
    [Header("Items that switch on and off")]
    [SerializeField] SoundEvent turnOn;
    [SerializeField] SoundEvent turnOff;

    [Header("Swing and throw sounds")]
    [Tooltip("Whoosh for held swings and throws. Leave empty to disable both.")]
    [SerializeField] SoundEvent swing;
    [Tooltip("Optional point near the tip. Defaults to the item's center of mass.")]
    [SerializeField] Transform swingSamplePoint;
    [Tooltip("Held sample speed relative to the player rig, in metres per second.")]
    [Min(0.01f)] [SerializeField] float minimumSwingSpeed = 2f;
    [Tooltip("Slow below this speed to arm another swing. Clamped below the trigger speed.")]
    [Min(0f)] [SerializeField] float swingResetSpeed = 0.75f;
    [Tooltip("Minimum release speed in metres per second to play a throw whoosh.")]
    [Min(0.01f)] [SerializeField] float minimumThrowSpeed = 2f;
    [Tooltip("Shared minimum interval between swing and throw whooshes, in seconds.")]
    [Min(0f)] [SerializeField] float swingSoundCooldown = 0.25f;

    Rigidbody body;
    Grabbable grabbable;
    BagObject bag;
    HeadEquipment equipment;
    PhotonView view;
    Collider[] itemColliders;
    readonly List<Collider> releaseContacts = new List<Collider>();
    float nextImpactTime = float.NegativeInfinity;
    float nextFleshImpactTime = float.NegativeInfinity;
    public bool HasFleshImpact => fleshImpact != null;

    /// <summary>Called for a qualifying strike, independently of target knockback cooldowns.</summary>
    public void PlayFleshImpact(Vector3 position)
    {
        if (!isActiveAndEnabled || fleshImpact == null || Time.time < nextFleshImpactTime ||
            (PhotonNetwork.InRoom && view != null && !view.IsMine) ||
            (grabbable != null && grabbable.IsStashed) ||
            (equipment != null && equipment.IsEquipped))
            return;
        nextFleshImpactTime = Time.time + Mathf.Max(0.01f, fleshImpactCooldown);
        RoomRpc.PlaySound(fleshImpact, position);
    }
    float nextSwingTime = float.NegativeInfinity;
    bool hasSwingSample;
    bool swingArmed = true;
    Vector3 previousSwingPosition;
    Vector3 previousRigPosition;
    Quaternion previousRigRotation;
    Transform previousRig;

    public void ResetSwingState()
    {
        hasSwingSample = false;
        swingArmed = true;
        // Keep the cooldown across hand changes and release.
    }

    bool CanPlayMotionSound => swing != null &&
        (!PhotonNetwork.InRoom || view == null || view.IsMine) &&
        (equipment == null || !equipment.IsEquipped);

    void LateUpdate()
    {
        bool held = grabbable != null && grabbable.IsHeld && !grabbable.IsStashed;
        held |= bag != null && bag.State == BagState.Held && bag.CanControl;
        if (!CanPlayMotionSound || !held)
        {
            ResetSwingState();
            return;
        }

        Transform rig = GR.Player.instance != null ? GR.Player.instance.transform : null;
        Vector3 position = swingSamplePoint != null ? swingSamplePoint.position :
            body != null ? transform.TransformPoint(body.centerOfMass) : transform.position;
        Vector3 rigPosition = rig != null ? rig.position : Vector3.zero;
        Quaternion rigRotation = rig != null ? rig.rotation : Quaternion.identity;
        Vector3 relative = Quaternion.Inverse(rigRotation) * (position - rigPosition);
        float dt = Time.deltaTime;
        if (hasSwingSample && previousRig == rig && dt > 0.00001f &&
            (rigPosition - previousRigPosition).sqrMagnitude < 9f &&
            Quaternion.Angle(previousRigRotation, rigRotation) < 45f &&
            (relative - previousSwingPosition).sqrMagnitude < 9f)
        {
            float speed = (relative - previousSwingPosition).magnitude / dt;
            float threshold = Mathf.Max(0.01f, minimumSwingSpeed);
            if (speed <= Mathf.Clamp(swingResetSpeed, 0f, threshold * 0.9f))
                swingArmed = true;
            if (speed >= threshold && swingArmed)
            {
                swingArmed = false;
                TryPlayWhoosh();
            }
        }
        else
            swingArmed = true;

        previousSwingPosition = relative;
        previousRigPosition = rigPosition;
        previousRigRotation = rigRotation;
        previousRig = rig;
        hasSwingSample = true;
    }

    /// <summary>Call only for a held item's release, after equip/stash handling.</summary>
    public void PlayThrow(Vector3 velocity)
    {
        ResetSwingState();
        if (!CanPlayMotionSound || (grabbable != null && (grabbable.IsHeld || grabbable.IsStashed)))
            return;
        float threshold = Mathf.Max(0.01f, minimumThrowSpeed);
        if (velocity.sqrMagnitude >= threshold * threshold)
            TryPlayWhoosh();
    }

    void TryPlayWhoosh()
    {
        if (Time.time < nextSwingTime) return;
        nextSwingTime = Time.time + Mathf.Max(0f, swingSoundCooldown);
        Play(ItemSound.Swing);
    }

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        grabbable = GetComponent<Grabbable>();
        bag = GetComponent<BagObject>();
        equipment = GetComponent<HeadEquipment>();
        view = GetComponent<PhotonView>();
        itemColliders = GetComponentsInChildren<Collider>(true);
    }

    public void ResetImpactState()
    {
        releaseContacts.Clear();
        nextImpactTime = float.NegativeInfinity;
    }

    // Ignore only release-time overlaps with the local player, until those colliders separate.
    // This gates audio, not physics, and never excludes another player or a zombie.
    public void PrepareRelease()
    {
        ResetImpactState();
        Transform player = GR.Player.instance != null ? GR.Player.instance.transform : null;
        if (player == null) return;
        foreach (Collider other in player.GetComponentsInChildren<Collider>(true))
            if (!other.transform.IsChildOf(transform) && OverlapsItem(other))
                releaseContacts.Add(other);
    }

    bool OverlapsItem(Collider other)
    {
        if (other == null || !other.enabled || !other.gameObject.activeInHierarchy || other.isTrigger)
            return false;
        foreach (Collider own in itemColliders)
            if (own != null && own.enabled && !own.isTrigger && own.gameObject.activeInHierarchy &&
                own.bounds.Intersects(other.bounds))
                return true;
        return false;
    }

    void FixedUpdate()
    {
        for (int i = releaseContacts.Count - 1; i >= 0; i--)
            if (!OverlapsItem(releaseContacts[i]))
                releaseContacts.RemoveAt(i);
    }

    void OnDisable()
    {
        ResetImpactState();
        ResetSwingState();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isActiveAndEnabled || body == null || body.isKinematic ||
            (PhotonNetwork.InRoom && view != null && !view.IsMine) ||
            (grabbable != null && (grabbable.IsHeld || grabbable.IsStashed)) ||
            (bag != null && (bag.State != BagState.Dropped || !bag.CanControl)) ||
            (equipment != null && equipment.IsEquipped) ||
            releaseContacts.Contains(collision.collider))
            return;

        // Normal speed rejects fast tangential sliding with only a gentle surface contact.
        float speed = 0f;
        for (int i = 0; i < collision.contactCount; i++)
            speed = Mathf.Max(speed, Mathf.Abs(Vector3.Dot(collision.relativeVelocity, collision.GetContact(i).normal)));

        Enemy enemy = collision.collider.GetComponentInParent<Enemy>();
        PhotonPlayer player = collision.collider.GetComponentInParent<PhotonPlayer>();
        if (enemy != null || player != null)
        {
            // Flesh never also plays the generic drop sound, even when unassigned or cooling down.
            if (player != null && (player.IsDead ||
                (view != null && player.photonView != null && view.OwnerActorNr == player.photonView.OwnerActorNr)))
                return;
            float minSpeed = enemy != null ? enemy.MinHitSpeed : player.MinHitSpeed;
            if (speed >= minSpeed && collision.contactCount > 0)
                PlayFleshImpact(collision.GetContact(0).point);
            return;
        }
        if (Get(ItemSound.Drop) == null) return;
        if (speed < minimumImpactSpeed || Time.time < nextImpactTime)
            return;
        nextImpactTime = Time.time + impactSoundCooldown;
        Play(ItemSound.Drop);
    }

    public SoundEvent Get(ItemSound sound)
    {
        switch (sound)
        {
            case ItemSound.Grab: return grab;
            case ItemSound.Drop: return dropOverride != null ? dropOverride : drop;
            case ItemSound.Equip: return equip;
            case ItemSound.TurnOn: return turnOn;
            case ItemSound.TurnOff: return turnOff;
            case ItemSound.Swing: return swing;
            case ItemSound.FleshImpact: return fleshImpact;
            default: return null;
        }
    }

    /// <summary>Plays here and for every other player in the room.</summary>
    public void Play(ItemSound sound) => RoomRpc.PlaySound(Get(sound), transform.position);
}
