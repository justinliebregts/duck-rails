using System.Collections.Generic;
using Game.AI;
using Game.Audio;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Huggy's finger gun. Melee comes from the weapon itself; any use button on the holding hand fires a flare
/// from the fingertip, once per cooldown. The charge belongs to the gun, so a handed-over gun keeps recharging.
/// Each peer simulates the shot locally from the shooter's origin, direction and server timestamp. Only the
/// shooter tests enemies; it pushes the enemy lightly and sends the deflection so peers follow the same path.
/// </summary>
[RequireComponent(typeof(PhotonGrabbable))]
public sealed class HuggyFlareGun : MonoBehaviour, IPunObservable
{
    [SerializeField] DeathFlare flarePrefab;
    [Tooltip("At the fingertip; forward is the firing direction.")]
    [SerializeField] Transform muzzle;
    [SerializeField, Min(0.1f)] float cooldownSeconds = 10f;
    [Tooltip("Scaled along local Z from 0 (just fired) to 1 (charged). Pivot at the cuff end of the slot.")]
    [SerializeField] Transform chargeFill;
    [Tooltip("Hit speed given to an enemy a flare strikes, above that enemy's minimum hit speed.")]
    [SerializeField, Min(0f)] float enemyHitExtraSpeed = 2.5f;

    [Tooltip("Played at the muzzle on every peer when a flare is fired, in addition to the flare's own launch sound.")]
    [SerializeField] SoundEvent fireSound;
    [Tooltip("Played at the gun on every peer when the recharge completes.")]
    [SerializeField] SoundEvent reloadReadySound;

    [Tooltip("Local-only feedback for the shooter when the use button is pressed before the recharge is done.")]
    [SerializeField] SoundEvent notReadySound;

    PhotonGrabbable grabbable;
    PhotonView view;
    Renderer fillRenderer;
    int lastShotAt;
    bool hasShot, wasCharged = true;
    int nextShotNumber;
    readonly Dictionary<int, DeathFlare> shots = new Dictionary<int, DeathFlare>();
    readonly List<int> stale = new List<int>();

    int CooldownMs => Mathf.RoundToInt(cooldownSeconds * 1000f);
    public float Charge01 => !hasShot ? 1f
        : Mathf.Clamp01(unchecked(PhotonNetwork.ServerTimestamp - lastShotAt) / (float)CooldownMs);
    public bool IsCharged => Charge01 >= 1f;

    void Awake()
    {
        grabbable = GetComponent<PhotonGrabbable>();
        view = GetComponent<PhotonView>();
        if (chargeFill != null) fillRenderer = chargeFill.GetComponentInChildren<Renderer>(true);
        DeathFlare.Prewarm(flarePrefab);
    }

    void Update()
    {
        PlayerHand hand = grabbable.Holder;
        if (hand != null && !grabbable.IsStashed && hand.WasUsePressed)
        {
            if (IsCharged) Fire(hand);
            else if (notReadySound != null && AudioManager.instance != null)
                AudioManager.instance.PlayAttached(notReadySound, transform, this);
        }
        if (shots.Count > 0) PruneShots();
        TrackReload();
    }

    // Every peer knows the shot time, so each one hears the ready cue when the charge completes.
    void TrackReload()
    {
        bool charged = IsCharged;
        if (charged && !wasCharged && reloadReadySound != null && AudioManager.instance != null)
            AudioManager.instance.PlayAttached(reloadReadySound, transform, this);
        wasCharged = charged;
    }

    void LateUpdate()
    {
        if (chargeFill == null) return;
        float charge = Charge01;
        Vector3 scale = chargeFill.localScale;
        scale.z = Mathf.Max(0.0001f, charge);
        chargeFill.localScale = scale;
        if (fillRenderer != null) fillRenderer.enabled = charge > 0.01f;
    }

    void Fire(PlayerHand hand)
    {
        if (flarePrefab == null || muzzle == null) return;
        int now = PhotonNetwork.ServerTimestamp;
        lastShotAt = now; hasShot = true;
        int key = ShotKey(PhotonNetwork.LocalPlayer != null ? PhotonNetwork.LocalPlayer.ActorNumber : 0, ++nextShotNumber);
        Vector3 origin = muzzle.position, direction = muzzle.forward;
        Spawn(key, origin, direction, 0f, true);
        if (PhotonNetwork.InRoom && view != null)
            view.RPC(nameof(RpcFlareShot), RpcTarget.Others, key, origin, direction, now);
        VibrationManager.instance?.TriggerVibrationPulse(0.5f, 0.8f, 0.12f, hand);
        DuckzLog.Log($"[FlareGun] '{name}' fired flare {key:X8}", this);
    }

    static int ShotKey(int actor, int number) => unchecked((actor << 20) ^ (number & 0xFFFFF));

    void Spawn(int key, Vector3 origin, Vector3 direction, float age, bool shooter)
    {
        var flare = DeathFlare.SpawnShot(flarePrefab, origin, direction, age, shooter ? OnShooterEnemyHit : null);
        if (flare == null) return;
        flare.ShotKey = key;
        shots[key] = flare;
        // Fresh shots only; a late copy of an old shot stays silent.
        if (age < 0.5f && fireSound != null && AudioManager.instance != null)
            AudioManager.instance.Play(fireSound, origin);
    }

    void OnShooterEnemyHit(DeathFlare flare, Enemy enemy, Vector3 position, Vector3 deflected, float age)
    {
        if (enemy != null)
        {
            // A light push, the flare gun's "small damage". The enemy applies its own hit cooldown.
            Vector3 body = enemy.Root != null ? enemy.Root.position : enemy.transform.position;
            Vector3 push = Vector3.ProjectOnPlane(body - position, Vector3.up);
            if (push.sqrMagnitude < 1e-6f) push = Vector3.ProjectOnPlane(muzzle != null ? muzzle.forward : transform.forward, Vector3.up);
            if (push.sqrMagnitude < 1e-6f) push = transform.forward;
            enemy.SubmitThrownHit(push.normalized * (enemy.MinHitSpeed + enemyHitExtraSpeed), position, gameObject.name);
        }
        if (PhotonNetwork.InRoom && view != null)
            view.RPC(nameof(RpcFlareDeflect), RpcTarget.Others, flare.ShotKey, position, deflected, age);
    }

    [PunRPC]
    void RpcFlareShot(int key, Vector3 origin, Vector3 direction, int firedAt, PhotonMessageInfo info)
    {
        // Late copies of an older shot never rewind a newer charge.
        if (!hasShot || unchecked(firedAt - lastShotAt) > 0) { lastShotAt = firedAt; hasShot = true; }
        if (shots.ContainsKey(key)) return;
        float age = Mathf.Max(0f, unchecked(PhotonNetwork.ServerTimestamp - firedAt) / 1000f);
        Spawn(key, origin, direction, age, false);
    }

    [PunRPC]
    void RpcFlareDeflect(int key, Vector3 position, Vector3 velocity, float age, PhotonMessageInfo info)
    {
        if (shots.TryGetValue(key, out var flare) && flare != null && flare.ShotKey == key)
            flare.ApplyRemoteDeflect(position, velocity, age);
    }

    void PruneShots()
    {
        foreach (var entry in shots)
            if (entry.Value == null || entry.Value.ShotKey != entry.Key || !entry.Value.IsBurning) stale.Add(entry.Key);
        foreach (int key in stale) shots.Remove(key);
        stale.Clear();
    }

    // The owner (normally the holder) keeps late joiners and new holders in step with the charge.
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            stream.SendNext(hasShot);
            stream.SendNext(lastShotAt);
        }
        else
        {
            bool shot = (bool)stream.ReceiveNext();
            int at = (int)stream.ReceiveNext();
            if (shot && (!hasShot || unchecked(at - lastShotAt) > 0)) { lastShotAt = at; hasShot = true; }
        }
    }
}
