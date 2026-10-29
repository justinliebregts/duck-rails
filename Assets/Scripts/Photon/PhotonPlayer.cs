using System.Collections.Generic;
using Game.Animation;
using Photon.Pun;
using TMPro;
using Unity.Mathematics;
using UnityEngine;

public class PhotonPlayer : MonoBehaviourPun, IPunObservable
{
    [Header("Owner-only sources (assigned at spawn)")]
    [SerializeField] Transform headSource;    // XR camera
    [SerializeField] Transform lHandSource;  // PHYSICS hands, not raw tracked (round 10)
    [SerializeField] Transform rHandSource;

    [SerializeField] RemotePlayerBody body;  // remote render only
    [SerializeField] FingerRig fingers;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text timeAliveLabel;
    [Tooltip("Minimum remote playback delay. Late packets increase it for this avatar only.")]
    [SerializeField] float interpolationDelay = 0.18f;
    [SerializeField] GameObject deathBloodPrefab;
    [SerializeField] DeathFlare deathFlarePrefab;
    [SerializeField] Ragdoll deathRagdollPrefab;
    Ragdoll _remoteRagdoll;
    readonly List<Renderer> _deathHiddenRenderers = new List<Renderer>(32);
    readonly List<Renderer> deathRendererBuffer = new List<Renderer>(32);

    struct Sample { public double time; public BodyPose pose, cabinPose; public int arrival, pad; }
    int _renderCabinArrival, _renderCabinPad;
    Vector3 _renderCabinPosition;
    Quaternion _renderCabinRotation;
    const int PoseCapacity = 32;
    const float MaxAdaptiveDelay = 0.4f;
    const float GapResetSeconds = 1f;
    const float PlaybackHitchSeconds = 0.25f;
    const float RecoverySeconds = 0.15f;
    const double TimestampPeriod = 4294967.296d;
    const double TimestampHalfPeriod = TimestampPeriod * 0.5d;
    const float PoseFreshnessSeconds = 1f;
    const float RebaseSampleGap = 0.25f;
    const float RebaseObservationSeconds = 0.1f;
    const int RebaseSampleCount = 3;
    readonly List<Sample> _buf = new List<Sample>(PoseCapacity);
    float _adaptiveDelay, _sampleInterval = 0.05f;
    double _playbackTime, _playbackNow, _lastPoseArrival, _delayHoldUntil;
    double _lastPoseRealtime, _rebaseFirstRealtime, _rebaseLastRealtime, _rebaseLastSent;
    int _rebaseSamples;
    bool _playbackStarted, _hitchRecoveryPending;
    BodyPose _recoveryFrom;
    float _recoveryRemaining;
    bool _visible;
    bool _remoteDead;
    static readonly object NoDeathImpact = Vector3.zero;
    LocalBodyDriver _fingerSource;
    BodyPose _lastPose;
    bool _hasPose;
    double _aliveSince;
    int _timeAliveSeconds;
    int _shownTimeAlive = int.MinValue;
    bool _wasDead;
    bool _ownerClockStarted;
    [HideInInspector] public AudioSource _voiceSource;

    public GameObject helmetAnchor;

    [Header("Weapon knockback")]
    [SerializeField] float minHitSpeed = 2f;
    [SerializeField] float fullHitSpeed = 9f;
    [SerializeField] float lift = 0.55f;
    [SerializeField] float hitCooldown = 0.25f;
    [SerializeField] GameObject hitBloodPrefab;
    float _nextItemHitTime;
    [SerializeField] float headRadius = 0.15f;
    [SerializeField] float bodyRadius = 0.15f;
    [Tooltip("Length of the body capsule core, straight down from the chest.")]
    [SerializeField] float bodyLength = 0.35f;
    [SerializeField] float handRadius = 0.1f;

    float _swingLockUntil;
    const int LaunchStillFrames = 3;

    public AudioSource VoiceSource => _voiceSource;
    DuckCompanion companion;
    // Reacquire after a Play-mode script reload or when the companion is added
    // after this avatar's Awake. All hit/death/pose paths must agree on identity.
    public DuckCompanion Companion => companion != null ? companion : (companion = GetComponent<DuckCompanion>());
    public bool IsCompanion => Companion != null;
    public bool IsLocalHuman => !IsCompanion && photonView != null && photonView.Owner != null && photonView.Owner.IsLocal;
    public float MinHitSpeed => minHitSpeed;
    public bool SwingLocked => Time.time < _swingLockUntil;

    public int TimeAliveSeconds => _timeAliveSeconds;

    /// <summary>Local owner follows <see cref="GR.Player.IsDead"/>; remotes receive it over the pose stream.</summary>
    public bool IsDead => Companion != null ? Companion.IsDead : photonView != null && photonView.IsMine
        ? GR.Player.instance != null && GR.Player.instance.IsDead
        : _remoteDead;

    float DesiredPlaybackDelay => Mathf.Max(interpolationDelay, _adaptiveDelay);

    /// <summary>All enabled networked players (local and remote). Enemies read this instead of searching the scene.</summary>
    public static readonly List<PhotonPlayer> Active = new List<PhotonPlayer>(8);

    /// <summary>This client's own networked player, or null outside a room.</summary>
    public static PhotonPlayer Local
    {
        get
        {
            foreach (PhotonPlayer player in Active)
                if (player.IsLocalHuman)
                    return player;
            return null;
        }
    }

    public static void BroadcastDeathBlood(Vector3 position, int seed)
    {
        if (!PhotonNetwork.InRoom) return;

        foreach (PhotonPlayer player in Active)
        {
            PhotonView view = player.photonView;
            if (view == null || !player.IsLocalHuman || view.ViewID == 0) continue;

            // The victim already played its local burst. Do not replay old deaths for late joiners.
            view.RPC(nameof(ReceiveDeathBlood), RpcTarget.Others, position, seed);
            return;
        }
    }

    public static void BroadcastDeathFlare(Vector3 position)
    {
        if (!PhotonNetwork.InRoom) return;
        PhotonPlayer player = Local;
        if (player != null && player.photonView.ViewID != 0)
            player.photonView.RPC(nameof(ReceiveDeathFlare), RpcTarget.Others, position, PhotonNetwork.Time);
    }

    [PunRPC]
    void ReceiveDeathFlare(Vector3 position, double diedAt, PhotonMessageInfo info)
    {
        if (info.Sender != (IsCompanion ? photonView.Controller : photonView.Owner)) return;
        if (double.IsNaN(diedAt) || double.IsInfinity(diedAt)) return;
        float age = (float)System.Math.Max(0d, PhotonNetwork.Time - diedAt);
        DeathFlare.SpawnAfterDeath(deathFlarePrefab, position, age);
    }

    [PunRPC]
    void ReceiveDeathBlood(Vector3 position, int seed, PhotonMessageInfo info)
    {
        // Room-owned Duck has no Owner; its current controller is the master client.
        if (info.Sender != (IsCompanion ? photonView.Controller : photonView.Owner)) return;
        BloodDecalSpawner.Spawn(deathBloodPrefab, position, seed);
    }

    public void EmitCompanionDeathBlood()
    {
        if (Companion == null || !Companion.HasAuthority) return;
        Vector3 flarePosition = transform.position + Vector3.up * 0.35f;
        DeathFlare.SpawnAfterDeath(deathFlarePrefab, flarePosition);
        if (photonView.ViewID != 0)
            photonView.RPC(nameof(ReceiveDeathFlare), RpcTarget.Others, flarePosition, PhotonNetwork.Time);
        // Match the human player's placement, prefab and deterministic particle burst.
        Vector3 position = transform.position;
        if (Physics.Raycast(position + Vector3.up * 0.2f, Vector3.down, out RaycastHit hit,
            5f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore))
            position = hit.point + hit.normal * 0.01f;
        int seed = UnityEngine.Random.Range(1, int.MaxValue);
        BloodDecalSpawner.Spawn(deathBloodPrefab, position, seed);
        // Only live peers receive the event; late joiners should not replay old bursts.
        if (photonView.ViewID != 0)
            photonView.RPC(nameof(ReceiveDeathBlood), RpcTarget.Others, position, seed);
    }

    void Awake()
    {
        BloodDecalSpawner.Prewarm(deathBloodPrefab);
        DeathFlare.Prewarm(deathFlarePrefab);
        companion = GetComponent<DuckCompanion>();
        _voiceSource = PhotonVoiceChat.Attach(gameObject, body != null ? body.Head : null, IsCompanion);
        if (body != null)
            DuckSkinVisuals.Attach(body.gameObject, IsCompanion ? DuckSkinSource.Manual : DuckSkinSource.Networked);
    }

    void OnEnable() => Active.Add(this);
    void OnDisable()
    {
        Active.Remove(this);
        ResetPoseBuffer(false);
        _hasPose = false;
        _ownerClockStarted = false;
    }

    void OnDestroy()
    {
        if (_remoteRagdoll != null)
            Destroy(_remoteRagdoll.gameObject);
        if (GameManager.instance != null) GameManager.instance.RemovePhotonPlayer(this);
    }

    public Transform LeftHand => body != null ? body.LeftHand : null;
    public Transform RightHand => body != null ? body.RightHand : null;
    public Transform Chest => body != null ? body.Chest : null;

    /// <summary>The owner's hand transform that the pose stream sends. Null on remote copies.</summary>
    public Transform HandSource(bool left) => left ? lHandSource : rHandSource;

    /// <summary>The hand pose as sent over the network: the owner's source transform, or the latest interpolated pose on remote copies.</summary>
    public bool TryGetHandPose(bool left, out Vector3 position, out Quaternion rotation)
    {
        Transform source = photonView != null && photonView.IsMine ? HandSource(left) : null;
        if (source != null)
        {
            position = source.position;
            rotation = source.rotation;
            return true;
        }
        if (_hasPose)
        {
            position = left ? _lastPose.lHandPos : _lastPose.rHandPos;
            rotation = left ? _lastPose.lHandRot : _lastPose.rHandRot;
            return true;
        }
        position = default;
        rotation = Quaternion.identity;
        return false;
    }

    public static PhotonPlayer ForActor(int actorNumber)
    {
        foreach (PhotonPlayer player in Active)
            if (player != null && !player.IsCompanion && player.photonView != null && player.photonView.OwnerActorNr == actorNumber)
                return player;
        return null;
    }

    public bool TryGetHeadPosition(out Vector3 position)
    {
        if (photonView != null && photonView.IsMine && headSource != null)
        {
            position = headSource.position;
            return true;
        }

        if (body != null && body.Head != null)
        {
            position = body.Head.position;
            return true;
        }

        position = transform.position;
        return false;
    }

    public void BindSources(Transform head, Transform leftHand, Transform rightHand, LocalBodyDriver fingerSource = null)
    {
        headSource = head;
        lHandSource = leftHand;
        rHandSource = rightHand;
        _fingerSource = fingerSource;
    }

    public bool TryGetGaze(out Vector3 eyePosition, out Vector3 forward)
    {
        if (photonView != null && photonView.IsMine && headSource != null)
        {
            eyePosition = headSource.position;
            forward = headSource.forward;
            return forward.sqrMagnitude > 1e-6f;
        }

        if (body != null && body.Head != null)
        {
            eyePosition = body.EyePosition;
            forward = body.GazeForward;
            return forward.sqrMagnitude > 1e-6f;
        }

        eyePosition = default;
        forward = Vector3.forward;
        return false;
    }

    void Start()
    {
        if (!IsLocalHuman) PrepareDeathRagdoll();
        if (IsLocalHuman)
        {
            if (VoicePatrolManager.Instance != null)
                VoicePatrolManager.Instance.NotifyLocalDuck();
        }

        if (nameLabel != null)
        {
            if (IsLocalHuman)
                nameLabel.gameObject.SetActive(false);
            else
            {
                var owner = photonView.Owner;
                nameLabel.text = IsCompanion ? "Duck" : owner != null && !string.IsNullOrEmpty(owner.NickName) ? owner.NickName : "Duck";
                if (!IsCompanion)
                    CreatorNametag.AttachRemote(nameLabel);
            }
        }

        if (timeAliveLabel != null && IsLocalHuman)
            timeAliveLabel.gameObject.SetActive(false);

        if (IsLocalHuman)
        {
            PlayerProps.PushCosmetics();
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                // The body stays hidden in first person. The wrist watch is how this
                // player reads coins and a Voice Patrol mute.
                if (renderer.GetComponentInParent<Watch>() != null)
                    continue;
                renderer.enabled = false;
            }
        }

        GameManager.instance.AddPhotonPlayer(this);
    }

    void Update()
    {
        if (photonView == null || !photonView.IsMine)
        {
            _ownerClockStarted = false;
            return;
        }

        UpdateAliveClock(IsDead, Time.realtimeSinceStartupAsDouble);
    }

    void UpdateAliveClock(bool dead, double now)
    {
        if (!_ownerClockStarted)
        {
            _ownerClockStarted = true;
            _wasDead = dead;
            if (!dead)
                // Resume from the received lifetime if companion authority changes.
                _aliveSince = now - _timeAliveSeconds;
            ApplyTimeAliveSeconds(_timeAliveSeconds);
            return;
        }

        if (_wasDead && !dead)
        {
            _aliveSince = now;
            ApplyTimeAliveSeconds(0);
        }
        else if (!dead)
            // Lifetime is elapsed real time, independent of network clock corrections,
            // server changes, timestamp wrap and gameplay timeScale.
            ApplyTimeAliveSeconds((int)System.Math.Clamp(now - _aliveSince, 0d, int.MaxValue));

        _wasDead = dead;
    }

    void ApplyTimeAliveSeconds(int seconds)
    {
        _timeAliveSeconds = seconds;
        if (seconds == _shownTimeAlive)
            return;
        _shownTimeAlive = seconds;
        if (timeAliveLabel != null)
            timeAliveLabel.text = seconds.ToString();
    }

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            if (headSource == null || lHandSource == null || rHandSource == null)
            {
                stream.SendNext(new PosePacket
                {
                    HeadPos = transform.position,
                    HeadRot = Quaternion.identity,
                    LHandRot = Quaternion.identity,
                    RHandRot = Quaternion.identity,
                });
            }
            else
            {
                var packet = PosePacket.Capture(
                    headSource,
                    lHandSource,
                    rHandSource,
                    _fingerSource != null ? _fingerSource.Curl : default);
                var manager = HelicopterManager.instance;
                int arrival = 0, pad = 0; Transform frame = null;
                if (IsLocalHuman && ((manager != null && manager.TryGetLocalPassengerFrame(out arrival, out pad, out frame))
                    || RailTrain.TryLocalFrame(out arrival, out pad, out frame)))
                {
                    packet.CabinArrival = arrival;
                    packet.CabinLanding = pad;
                    packet.CabinPosition = frame.position;
                    packet.CabinRotation = frame.rotation;
                }
                stream.SendNext(packet);
            }
            bool sendingDead = IsDead;
            stream.SendNext(sendingDead);
            // Keep living pose packets unchanged. Repeating the cached vector with
            // dead state handles dropped packets and late joiners without an RPC race.
            if (sendingDead)
            {
                stream.SendNext(!IsCompanion && GR.Player.instance != null
                    ? GR.Player.instance.DeathImpactState : NoDeathImpact);
                stream.SendNext(!IsCompanion && GR.Player.instance != null
                    ? GR.Player.instance.DeathRotationState : NoDeathImpact);
            }
            stream.SendNext(_timeAliveSeconds);
            if (Companion != null) Companion.Serialize(stream);
            return;
        }

        // Human poses belong to their owner; room-owned companions follow their controller.
        // Do not let an unrelated actor reseed either the clock or death state.
        DuckCompanion sourceCompanion = Companion;
        int sourceActor = sourceCompanion != null ? photonView.ControllerActorNr : photonView.OwnerActorNr;
        // Actor identity survives a missing/rebuilt Player cache during room transitions.
        if (sourceActor <= 0 || info.Sender == null || info.Sender.ActorNumber != sourceActor)
            return;

        var pkt = (PosePacket)stream.ReceiveNext();
        bool dead = (bool)stream.ReceiveNext();
        Vector3 deathImpact = dead ? (Vector3)stream.ReceiveNext() : Vector3.zero;
        Vector3 deathRotation = dead ? (Vector3)stream.ReceiveNext() : Vector3.zero;
        int aliveSeconds = (int)stream.ReceiveNext();
        BodyPose pose = pkt.ToBodyPose();
        if (!ValidPose(pose) || !ValidVector(deathImpact) || !ValidVector(deathRotation))
            return;
        if (pkt.CabinArrival < 0 || (pkt.CabinArrival > 0 && (sourceCompanion != null || pkt.CabinLanding < 0
            || !ValidVector(pkt.CabinPosition) || !ValidRotation(pkt.CabinRotation)))) return;
        double now = PhotonNetwork.Time;
        // Express the sender's timestamp near our clock, including the uint wrap.
        double sent = info.SentServerTime;
        if (sent - now > TimestampHalfPeriod) sent -= TimestampPeriod;
        else if (now - sent > TimestampHalfPeriod) sent += TimestampPeriod;
        double received = Time.realtimeSinceStartupAsDouble;
        if (!AcceptPoseTimestamp(sent, now, received))
            return;
        // Each avatar view observes this component only. Discard the companion
        // payload with a rejected snapshot instead of replaying stale life state.
        if (sourceCompanion != null) sourceCompanion.Serialize(stream);
        if (_playbackStarted && now - _playbackNow > PlaybackHitchSeconds)
            _hitchRecoveryPending = true;
        bool wasDead = _remoteDead;
        _remoteDead = dead;
        ApplyTimeAliveSeconds(Mathf.Max(0, aliveSeconds));
        // Measure the motion before the buffer is cleared, so the corpse keeps it.
        RagdollLaunch launch = !wasDead && _remoteDead ? RecentLaunch() : default;
        launch.impact = deathImpact;
        launch.rotation = deathRotation;
        if (wasDead != _remoteDead)
            ResetPoseBuffer(false);
        else if (_buf.Count > 0 && (sent - _buf[_buf.Count - 1].time > GapResetSeconds || now - _lastPoseArrival > GapResetSeconds))
            ResetPoseBuffer(true);
        if (_buf.Count > 0)
        {
            float spacing = (float)(sent - _buf[_buf.Count - 1].time);
            // Missing packets must not turn the cadence estimate into the outage length.
            if (spacing < 0.15f)
                _sampleInterval = Mathf.Lerp(_sampleInterval, Mathf.Clamp(spacing, 0.025f, 0.1f), 0.1f);
        }
        // Packets queued during a local stall look artificially old. Once the
        // batch is drained, playback estimates delay from its newest sample.
        if (!_hitchRecoveryPending) UpdateAdaptiveDelay(now, sent);
        _lastPoseArrival = now;
        _lastPoseRealtime = received;
        if (_buf.Count == PoseCapacity)
            _buf.RemoveAt(0);
        var receivedSample = new Sample { time = sent, pose = pose, arrival = pkt.CabinArrival, pad = pkt.CabinLanding };
        if (pkt.CabinArrival > 0)
        {
            Quaternion inverse = Quaternion.Inverse(pkt.CabinRotation);
            receivedSample.cabinPose = TransformPose(pose, inverse * -pkt.CabinPosition, inverse);
        }
        _buf.Add(receivedSample);
        bool firstPose = !_hasPose;
        if (firstPose || wasDead != _remoteDead)
            ApplySample(receivedSample, 0f); // Also resolve a late joiner's first passenger pose immediately.
        if (wasDead != _remoteDead)
        {
            // Use the death/respawn packet immediately, including for late joiners.
            SetRemoteDeathVisuals(_remoteDead, launch);
        }
        if (!_visible)
        {
            _visible = true;
            // SetAvatarVisible(true);
        }
    }

    bool AcceptPoseTimestamp(double sent, double now, double received)
    {
        // Keep a bad future timestamp from poisoning the ordering baseline. Old but
        // advancing snapshots remain usable on high-latency connections; freshness
        // is required below only when replacing an existing ordering baseline.
        if (sent - now > PoseFreshnessSeconds)
        {
            _rebaseSamples = 0;
            return false;
        }
        if (_buf.Count == 0)
        {
            _rebaseSamples = 0;
            return true;
        }
        double advance = sent - _buf[_buf.Count - 1].time;
        if (advance < -TimestampHalfPeriod
            && advance + TimestampPeriod <= System.Math.Max(0d, received - _lastPoseRealtime) + PoseFreshnessSeconds * 2d)
            advance += TimestampPeriod;
        else if (advance > TimestampHalfPeriod) advance -= TimestampPeriod;
        if (advance > 0d)
        {
            _rebaseSamples = 0;
            // An ordinary uint wrap changes the time coordinate, not packet order.
            if (sent < _buf[_buf.Count - 1].time) ResetPoseBuffer(true);
            return true;
        }

        // Never turn one delayed/duplicate packet into a resurrection. A clock reset
        // needs a stalled stream AND a fresh, advancing sequence observed over real
        // time. Bursts drained in one frame cannot satisfy the observation period.
        if (received - _lastPoseRealtime < GapResetSeconds || advance == 0d
            || now - sent > PoseFreshnessSeconds)
        {
            _rebaseSamples = 0;
            return false;
        }
        double step = sent - _rebaseLastSent;
        double arrivalStep = received - _rebaseLastRealtime;
        if (_rebaseSamples == 0 || step <= 0d || step > RebaseSampleGap
            || arrivalStep < 0d || arrivalStep > RebaseSampleGap)
        {
            _rebaseSamples = 1;
            _rebaseFirstRealtime = received;
        }
        else
            _rebaseSamples = Mathf.Min(_rebaseSamples + 1, RebaseSampleCount);
        _rebaseLastSent = sent;
        _rebaseLastRealtime = received;
        if (_rebaseSamples < RebaseSampleCount || received - _rebaseFirstRealtime < RebaseObservationSeconds)
            return false;

        ResetPoseBuffer(true);
        return true;
    }

    static bool ValidVector(Vector3 value) => math.all(math.isfinite((float3)value));

    static bool ValidPose(in BodyPose pose) => math.all(math.isfinite(pose.headPos))
        && math.all(math.isfinite(pose.lHandPos)) && math.all(math.isfinite(pose.rHandPos))
        && ValidRotation(pose.headRot) && ValidRotation(pose.lHandRot) && ValidRotation(pose.rHandRot);

    static bool ValidRotation(quaternion value) => math.all(math.isfinite(value.value))
        && math.lengthsq(value.value) > 1e-6f;

    void LateUpdate()
    {
        if (!photonView.IsMine)
        {
            ApplyRemotePose(); // Dead avatars are hidden; their ghost pose still follows the cabin.
        }
    }

    public void ApplyCompanionPose(in BodyPose pose) => ApplyBody(pose);
    public void ApplyCompanionDeath(bool dead) => SetRemoteDeathVisuals(dead);

    // Velocity across the last ~0.15 s of received poses.
    RagdollLaunch RecentLaunch()
    {
        if (_buf.Count < 2)
            return default;
        Sample last = _buf[_buf.Count - 1];
        Sample first = _buf[_buf.Count - 2];
        for (int i = _buf.Count - 2; i >= 0; i--)
        {
            first = _buf[i];
            if (last.time - first.time >= 0.15)
                break;
        }
        float dt = (float)(last.time - first.time);
        if (dt < 1e-4f)
            return default;
        return new RagdollLaunch
        {
            body = (Vector3)(last.pose.headPos - first.pose.headPos) / dt,
            leftHand = (Vector3)(last.pose.lHandPos - first.pose.lHandPos) / dt,
            rightHand = (Vector3)(last.pose.rHandPos - first.pose.rHandPos) / dt,
        };
    }

    void PrepareDeathRagdoll()
    {
        if (_remoteRagdoll != null || deathRagdollPrefab == null) return;
        // Construct inactive so no physics or rendering runs during preparation.
        var staging = new GameObject("Ragdoll staging"); staging.SetActive(false);
        _remoteRagdoll = Instantiate(deathRagdollPrefab, staging.transform);
        _remoteRagdoll.gameObject.SetActive(false);
        _remoteRagdoll.transform.SetParent(null, false);
        _remoteRagdoll.Prepare(transform);
        if (!IsCompanion) _remoteRagdoll.ApplySkin(photonView.Owner != null ? photonView.Owner.GetSkin() : (byte)0, photonView.OwnerActorNr);
        Destroy(staging);
        DeathProfile.RagdollCreates++;
    }

    void SetRemoteDeathVisuals(bool dead, RagdollLaunch launch = default)
    {
        using var sample = DeathProfile.Ragdoll.Auto();
        if (!dead)
        {
            if (_remoteRagdoll != null) _remoteRagdoll.Park();
            foreach (Renderer renderer in _deathHiddenRenderers)
                if (renderer != null) renderer.enabled = true;
            _deathHiddenRenderers.Clear();
            return;
        }

        if (deathRagdollPrefab == null || body == null || body.Chest == null
            || body.LeftHand == null || body.RightHand == null)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice1))
                    logNotice1.Write($"[Ragdoll] Remote death of {photonView.Owner?.NickName}: no ragdoll (prefab set {deathRagdollPrefab != null}, body set {body != null})", this);
            }
            return;
        }

        // Room-owned companions can resolve their Photon owner to the master client.
        // Their appearance comes from their own rig, never the master's skin/color.
        byte ragdollSkin = IsCompanion ? (byte)0 : photonView.Owner != null ? photonView.Owner.GetSkin() : (byte)0;
        int colorActor = IsCompanion ? 0 : photonView.OwnerActorNr;
        DeathProfile.Trace($"[Ragdoll] Death of {(IsCompanion ? "Duck companion" : photonView.Owner?.NickName)}: skin {ragdollSkin}, color actor {colorActor}", this);
        PrepareDeathRagdoll();
        _remoteRagdoll.leftHandSource = body.LeftHand;
        _remoteRagdoll.rightHandSource = body.RightHand;
        _remoteRagdoll.sourceHip = body.Chest;
        if (IsCompanion) _remoteRagdoll.ApplyCompanionAppearance(body.transform, Companion.KillerEyesAtDeath);
        else _remoteRagdoll.ApplySkin(ragdollSkin, colorActor);
        _remoteRagdoll.PoseToPlayer(body.Chest, launch);

        GetComponentsInChildren(true, deathRendererBuffer);
        foreach (Renderer renderer in deathRendererBuffer)
        {
            if (!renderer.enabled)
                continue;
            _deathHiddenRenderers.Add(renderer);
            renderer.enabled = false;
        }
    }

    void ApplyRemotePose()
    {
        if (body == null || _buf.Count == 0)
            return;
        RebasePassengerPlayback();
        if (_buf.Count == 1)
        {
            if (_buf[0].arrival > 0) ApplySample(_buf[0], 0f);
            return;
        }

        double now = PhotonNetwork.Time;
        double elapsed = _playbackStarted ? now - _playbackNow : 0d;
        if (elapsed > PlaybackHitchSeconds) _hitchRecoveryPending = true;
        // If dispatch resumes after rendering, wait for new data before rebasing.
        bool recoverHitch = _hitchRecoveryPending && _lastPoseArrival > _playbackNow;
        float dt = recoverHitch ? 0f : (float)System.Math.Clamp(elapsed, 0d, 0.1d);
        double newest = _buf[_buf.Count - 1].time;
        if (recoverHitch)
            UpdateAdaptiveDelay(now, newest);
        _playbackNow = now;
        if (now > _delayHoldUntil)
            _adaptiveDelay = Mathf.MoveTowards(DesiredPlaybackDelay, interpolationDelay, dt * 0.025f);
        if (!_playbackStarted || recoverHitch)
        {
            // Rebase after a receiver hitch instead of spending seconds catching
            // up at 10%. Start the visual blend on the next frame, without a snap.
            if (recoverHitch) BeginPoseRecovery();
            double resumed = System.Math.Clamp(now - DesiredPlaybackDelay, _buf[0].time, newest);
            _playbackTime = _playbackStarted ? System.Math.Max(_playbackTime, resumed) : resumed;
            _playbackStarted = true;
            _hitchRecoveryPending = false;
        }
        else
        {
            // Move the playback clock, not the pose target: increasing delay must
            // never replay old gestures backwards. Catch-up is limited to 10%.
            float error = (float)(now - DesiredPlaybackDelay - (_playbackTime + dt));
            float speed = Mathf.Clamp(1f + error * 4f, 0.5f, 1.1f);
            double remaining = System.Math.Max(0d, newest - _playbackTime);
            // Ease to a stop at the edge during a gap. Do not invent hand motion.
            speed = Mathf.Min(speed, (float)(remaining / 0.04d));
            _playbackTime += System.Math.Min(remaining, dt * speed);
            if (_playbackTime < _buf[0].time)
            {
                // A receiver stall can evict the playback position from the buffer.
                BeginPoseRecovery();
                _playbackTime = System.Math.Clamp(now - DesiredPlaybackDelay, _buf[0].time, newest);
            }
        }
        double target = _playbackTime;

        for (int i = _buf.Count - 2; i >= 0; i--)
        {
            if (_buf[i].time <= target)
            {
                var a = _buf[i];
                var b = _buf[i + 1];
                float span = (float)(b.time - a.time);
                float u = span > 1e-6f ? (float)((target - a.time) / span) : 1f;
                if (a.arrival == b.arrival && a.pad == b.pad)
                {
                    a.pose = BlendPose(a.pose, b.pose, math.saturate(u));
                    if (a.arrival > 0) a.cabinPose = BlendPose(a.cabinPose, b.cabinPose, math.saturate(u));
                    ApplySample(a, dt);
                }
                else
                    // Never blend a cabin-relative pose with world space at a doorway
                    // or respawn; that blend can pass straight through the rising floor.
                    ApplySample(u >= 1f ? b : a, dt);
                return;
            }
        }

        ApplySample(_buf[0], dt);
    }

    static BodyPose TransformPose(BodyPose pose, Vector3 position, Quaternion rotation)
    {
        pose.headPos = position + rotation * (Vector3)pose.headPos;
        pose.lHandPos = position + rotation * (Vector3)pose.lHandPos;
        pose.rHandPos = position + rotation * (Vector3)pose.rHandPos;
        pose.headRot = rotation * (Quaternion)pose.headRot;
        pose.lHandRot = rotation * (Quaternion)pose.lHandRot;
        pose.rHandRot = rotation * (Quaternion)pose.rHandRot;
        return pose;
    }

    void RebasePassengerPlayback()
    {
        if (_renderCabinArrival <= 0) return;
        var manager = HelicopterManager.instance;
        Transform frame = null;
        if (!(manager != null && manager.TryGetPassengerFrame(_renderCabinArrival, _renderCabinPad, out frame))
            && !RailTrain.TryFrame(_renderCabinArrival, out frame))
            return;
        Quaternion deltaRotation = frame.rotation * Quaternion.Inverse(_renderCabinRotation);
        Vector3 deltaPosition = frame.position - deltaRotation * _renderCabinPosition;
        _lastPose = TransformPose(_lastPose, deltaPosition, deltaRotation);
        if (_recoveryRemaining > 0f) _recoveryFrom = TransformPose(_recoveryFrom, deltaPosition, deltaRotation);
        _renderCabinPosition = frame.position;
        _renderCabinRotation = frame.rotation;
    }

    void ApplySample(in Sample sample, float dt)
    {
        var manager = HelicopterManager.instance;
        Transform frame = null;
        bool passenger = sample.arrival > 0 && ((manager != null
            && manager.TryGetPassengerFrame(sample.arrival, sample.pad, out frame)) || RailTrain.TryFrame(sample.arrival, out frame));
        int arrival = passenger ? sample.arrival : 0;
        int pad = passenger ? sample.pad : 0;
        if (arrival != _renderCabinArrival || pad != _renderCabinPad) _recoveryRemaining = 0f;
        _renderCabinArrival = arrival;
        _renderCabinPad = pad;
        if (passenger)
        {
            _renderCabinPosition = frame.position;
            _renderCabinRotation = frame.rotation;
        }
        ApplyBufferedPose(passenger ? TransformPose(sample.cabinPose, frame.position, frame.rotation) : sample.pose, dt);
    }

    void ResetPoseBuffer(bool recover)
    {
        if (recover) BeginPoseRecovery();
        else
        {
            _renderCabinArrival = 0;
            _renderCabinPad = 0;
            _recoveryRemaining = 0f;
            _hitchRecoveryPending = false;
        }
        _buf.Clear();
        _rebaseSamples = 0;
        _playbackStarted = false;
        _adaptiveDelay = interpolationDelay;
        _sampleInterval = 0.05f;
        _delayHoldUntil = 0d;
    }

    void UpdateAdaptiveDelay(double now, double sent)
    {
        float needed = Mathf.Clamp((float)(now - sent) + _sampleInterval * 1.5f + 0.01f,
            interpolationDelay, Mathf.Max(interpolationDelay, MaxAdaptiveDelay));
        if (needed >= _adaptiveDelay)
        {
            _adaptiveDelay = needed;
            _delayHoldUntil = now + 1d;
        }
    }

    void BeginPoseRecovery()
    {
        if (!_hasPose) return;
        _recoveryFrom = _lastPose;
        _recoveryRemaining = RecoverySeconds;
    }

    void ApplyBufferedPose(in BodyPose pose, float dt)
    {
        if (_recoveryRemaining <= 0f) { ApplyBody(pose); return; }
        _recoveryRemaining = Mathf.Max(0f, _recoveryRemaining - dt);
        float u = 1f - _recoveryRemaining / RecoverySeconds;
        ApplyBody(BlendPose(_recoveryFrom, pose, u * u * (3f - 2f * u)));
    }

    void ApplyBody(in BodyPose pose)
    {
        _lastPose = pose;
        _hasPose = true;
        if (Companion != null) Companion.ReceivePose(pose);
        if (body != null)
            body.ApplyPose(pose, bodyRotation: Companion != null ? (Quaternion?)transform.rotation : null);
        fingers?.Apply(pose.fingers);
    }

    // Linear positions avoid curve overshoot and retroactive tangent changes
    // when a late sample arrives. Rotations and fingers retain their pose blends.
    static BodyPose BlendPose(in BodyPose a, in BodyPose b, float u)
    {
        return new BodyPose
        {
            headPos = math.lerp(a.headPos, b.headPos, u),
            headRot = math.slerp(a.headRot, b.headRot, u),
            lHandPos = math.lerp(a.lHandPos, b.lHandPos, u),
            lHandRot = math.slerp(a.lHandRot, b.lHandRot, u),
            rHandPos = math.lerp(a.rHandPos, b.rHandPos, u),
            rHandRot = math.slerp(a.rHandRot, b.rHandRot, u),
            fingers = FingerCurl.Lerp(a.fingers, b.fingers, u),
        };
    }

    public void LockSwing() => _swingLockUntil = Time.time + hitCooldown;

    public bool OverlapsSwing(Vector3 from, Vector3 to, float sweepRadius)
    {
        // Duck's short torso is not covered reliably by the human chest/head calibration.
        // Include his supported body capsule, even before the first visual pose arrives.
        if (Companion != null)
        {
            CharacterController capsule = Companion.GetComponent<CharacterController>();
            if (capsule != null)
            {
                Vector3 center = transform.TransformPoint(capsule.center);
                Vector3 half = Vector3.up * Mathf.Max(0f, capsule.height * 0.5f - capsule.radius);
                if (SegmentsWithin(from, to, center - half, center + half, sweepRadius + capsule.radius)) return true;
            }
        }
        if (!_hasPose || body == null) return false;

        Transform head = body.Head;
        if (head != null && SegmentHitsSphere(from, to, head.position, sweepRadius + headRadius))
            return true;

        Transform chest = body.Chest;
        if (chest != null)
        {
            Vector3 up = chest.up.sqrMagnitude > 1e-6f ? chest.up.normalized : Vector3.up;
            Vector3 bodyEnd = chest.position - up * bodyLength;
            if (SegmentsWithin(from, to, chest.position, bodyEnd, sweepRadius + bodyRadius))
                return true;
        }

        float handReach = sweepRadius + handRadius;
        Transform left = body.LeftHand;
        if (left != null && SegmentHitsSphere(from, to, left.position, handReach))
            return true;
        Transform right = body.RightHand;
        if (right != null && SegmentHitsSphere(from, to, right.position, handReach))
            return true;
        return false;
    }

    public bool TryLaunchVelocity(Vector3 swingVelocity, float launchSpeed, out Vector3 launch)
    {
        launch = default;
        if (launchSpeed <= 0f) return false;
        float speed = swingVelocity.magnitude;
        float speedSpan = Mathf.Max(0.01f, fullHitSpeed - minHitSpeed);
        if (speed < minHitSpeed) return false;
        float strength = Mathf.Clamp01((speed - minHitSpeed) / speedSpan);
        if (strength <= 0f) return false;

        Vector3 dir = swingVelocity / speed;
        dir = (dir + Vector3.up * (lift * strength)).normalized;
        launch = dir * (launchSpeed * strength);
        return true;
    }

    [PunRPC]
    public void ReceiveCompanionHit(int itemViewId, Vector3 velocity, Vector3 hitPosition, PhotonMessageInfo info)
    {
        if (Companion == null || !Companion.HasAuthority || info.Sender == null) return;
        PhotonView item = PhotonView.Find(itemViewId);
        if (item == null || item.Controller != info.Sender || item.GetComponent<PhotonGrabbable>() == null) return;
        // The item's owner performs the same swept hit test used for human players.
        // Allow network delay, but reject reports from items nowhere near Duck.
        if ((item.transform.position - transform.position).sqrMagnitude > 36f) return;
        AcceptItemHit(info.Sender.ActorNumber, velocity, hitPosition, 0f);
    }

    [PunRPC]
    public void ReceiveItemHit(int itemViewId, Vector3 velocity, Vector3 hitPosition, PhotonMessageInfo info)
    {
        if (IsCompanion || !photonView.IsMine || info.Sender == null) return;
        PhotonView item = PhotonView.Find(itemViewId);
        if (item == null || item.Controller != info.Sender) return;
        PhotonGrabbable grabbable = item.GetComponent<PhotonGrabbable>();
        if (grabbable == null) return;
        // The human avatar root stays at spawn; validate against the live tracked head.
        // Read existing references only when a hit arrives, retaining the position fallback.
        TryGetHeadPosition(out Vector3 victimPosition);
        if ((item.transform.position - victimPosition).sqrMagnitude > 36f) return;
        AcceptItemHit(info.Sender.ActorNumber, velocity, hitPosition, grabbable.PlayerLaunchSpeed);
    }

    // Called only by the victim owner, or the master for the room-owned companion.
    // Keep this short hit cooldown separate from Duck's longer retreat reaction.
    public bool AcceptItemHit(int actor, Vector3 velocity, Vector3 hitPosition, float launchSpeed)
    {
        if (IsCompanion ? !Companion.HasAuthority : !photonView.IsMine) return false;
        if (IsDead || Time.time < _nextItemHitTime
            || !IsValidValue(velocity.x) || !IsValidValue(velocity.y) || !IsValidValue(velocity.z)
            || !IsValidValue(hitPosition.x) || !IsValidValue(hitPosition.y) || !IsValidValue(hitPosition.z)) return false;
        float minimum = IsCompanion ? Companion.HitReactionMinSpeed : minHitSpeed;
        if (velocity.sqrMagnitude < minimum * minimum || velocity.sqrMagnitude < 1e-8f) return false;
        PhotonPlayer attacker = ForActor(actor);
        if (attacker == null || attacker.IsDead || attacker == this) return false;
        _nextItemHitTime = Time.time + hitCooldown;
        SpawnItemHitBlood(velocity, hitPosition);
        if (PhotonNetwork.InRoom && photonView.ViewID != 0)
            photonView.RPC(nameof(ReceiveItemHitBlood), RpcTarget.Others, velocity, hitPosition);
        if (IsCompanion) Companion.ReactToPlayerHit(actor, velocity);
        else if (TryLaunchVelocity(velocity, launchSpeed, out Vector3 launch)) ReceiveWeaponLaunch(launch);
        return true;
    }

    [PunRPC]
    void ReceiveItemHitBlood(Vector3 velocity, Vector3 hitPosition, PhotonMessageInfo info)
    {
        if (info.Sender != (IsCompanion ? photonView.Controller : photonView.Owner)) return;
        SpawnItemHitBlood(velocity, hitPosition);
    }

    void SpawnItemHitBlood(Vector3 velocity, Vector3 hitPosition)
    {
        if (hitBloodPrefab == null || velocity.sqrMagnitude < 1e-8f) return;
        Vector3 dir = velocity.normalized;
        Vector3 spin = Mathf.Abs(dir.y) > 0.99f ? Vector3.forward : Vector3.up;
        Instantiate(hitBloodPrefab, hitPosition, Quaternion.LookRotation(dir, spin));
    }

    [PunRPC]
    public void ReceiveWeaponLaunch(Vector3 velocity)
    {
        if (photonView == null || !photonView.IsMine) return;
        if (Companion != null) return;
        if (GR.Player.instance != null && GR.Player.instance.IsDead) return;
        if (!IsValidValue(velocity.x) || !IsValidValue(velocity.y) || !IsValidValue(velocity.z)) return;

        DesktopPlayer desktop = GR.Player.instance != null
            ? GR.Player.instance.GetComponent<DesktopPlayer>()
            : null;
        if (desktop != null && desktop.isActiveAndEnabled)
        {
            desktop.Launch(velocity);
            return;
        }

        if (NewPlayer.instance != null && NewPlayer.instance.isActiveAndEnabled)
            NewPlayer.instance.LaunchPlayerFromLaunchPad(velocity, LaunchType.OverwriteVelocity, LaunchStillFrames);
    }

    static bool SegmentHitsSphere(Vector3 from, Vector3 to, Vector3 center, float radius)
    {
        Vector3 segment = to - from;
        float lengthSq = segment.sqrMagnitude;
        float t = lengthSq > 1e-8f ? Mathf.Clamp01(Vector3.Dot(center - from, segment) / lengthSq) : 0f;
        return (from + segment * t - center).sqrMagnitude <= radius * radius;
    }

    static bool SegmentsWithin(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, float radius)
    {
        Vector3 u = p1 - p0;
        Vector3 v = q1 - q0;
        Vector3 w = p0 - q0;
        float a = Vector3.Dot(u, u);
        float b = Vector3.Dot(u, v);
        float c = Vector3.Dot(v, v);
        float d = Vector3.Dot(u, w);
        float e = Vector3.Dot(v, w);
        float denom = a * c - b * b;
        float sN;
        float sD = denom;
        float tN;
        float tD = denom;

        if (sD < 1e-8f)
        {
            sN = 0f;
            sD = 1f;
            tN = e;
            tD = c;
        }
        else
        {
            sN = b * e - c * d;
            tN = a * e - b * d;
            if (sN < 0f)
            {
                sN = 0f;
                tN = e;
                tD = c;
            }
            else if (sN > sD)
            {
                sN = sD;
                tN = e + b;
                tD = c;
            }
        }

        if (tN < 0f)
        {
            tN = 0f;
            if (-d < 0f) sN = 0f;
            else if (-d > a)
                sN = sD;
            else
            {
                sN = -d;
                sD = a;
            }
        }
        else if (tN > tD)
        {
            tN = tD;
            float along = -d + b;
            if (along < 0f) sN = 0f;
            else if (along > a)
                sN = sD;
            else
            {
                sN = along;
                sD = a;
            }
        }

        float sc = sD < 1e-8f ? 0f : sN / sD;
        float tc = tD < 1e-8f ? 0f : tN / tD;
        Vector3 closest = w + u * sc - v * tc;
        return closest.sqrMagnitude <= radius * radius;
    }

    public static bool IsValidValue(float value)
    {
        if (float.IsNaN(value) || float.IsInfinity(value))
            return false;
        else
            return true;
    }
}
