using System.Collections.Generic;
using Game.Animation;
using UnityEngine;
using UnityEngine.Serialization;

namespace Game.AI
{
    public interface IEnemyHitVocal
    {
        void PlayWhimper();
    }

    /// <summary>
    /// Shared enemy brain. No Update: EnemyManager calls Perceive/Think/Move on the
    /// authority and ApplyRemote elsewhere, then ticks the gait and AimHead.
    /// Root motion comes from IEnemyLocomotion; gait poses the body.
    /// </summary>
    public class Enemy : MonoBehaviour
    {
        public enum State : byte { Wander, Chase, Search, Retreat }

        public static readonly List<Enemy> Active = new List<Enemy>(8);

        [SerializeField] Transform root;
        [SerializeField] Transform eye;
        [SerializeField] Transform head;

        [Header("Player death launch")]
        [Tooltip("Outward speed added to a killed player's ragdoll, in m/s.")]
        [SerializeField, Min(0f)] float playerDeathForce;
        [Tooltip("Upward speed added to a killed player's ragdoll, in m/s.")]
        [SerializeField, Min(0f)] float playerDeathLift;
        [Tooltip("Initial torso rotation speed in radians/s. Applied once on death.")]
        [SerializeField, Range(0f, 15f)] float playerDeathSpin;
        [SerializeField, Range(0f, 0.25f)] float playerDeathVariation = 0.12f;

        // Victim chooses once at death. Peers receive the result, never reroll it.
        public Vector3 PlayerDeathLaunch(Vector3 victimPosition, Vector3 origin)
        {
            Vector3 away = Vector3.ProjectOnPlane(victimPosition - origin, Vector3.up);
            if (away.sqrMagnitude < 0.0001f)
                away = Vector3.ProjectOnPlane((root != null ? root : transform).forward, Vector3.up);
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
            float scale = Random.Range(1f - playerDeathVariation, 1f + playerDeathVariation);
            return (away.normalized * playerDeathForce + Vector3.up * playerDeathLift) * scale;
        }

        public Vector3 PlayerDeathRotation(Vector3 impact)
        {
            if (playerDeathSpin <= 0f) return Vector3.zero;
            Vector3 away = Vector3.ProjectOnPlane(impact, Vector3.up).normalized;
            if (away.sqrMagnitude < 0.0001f) away = Vector3.forward;
            // Mostly tumble away from the hit, with a little twist. No per-limb randomness.
            Vector3 axis = Vector3.Cross(Vector3.up, away) + Vector3.up * Random.Range(-0.25f, 0.25f);
            float scale = Random.Range(1f - playerDeathVariation, 1f + playerDeathVariation);
            return axis.normalized * (playerDeathSpin * scale);
        }

        [Header("Vision")]
        [SerializeField] float visionRange = 14f;
        [SerializeField, Range(10f, 360f)] float fovDegrees = 140f;
        [SerializeField] float nearSenseRadius = 2.5f;
        [SerializeField] float loseSightTime = 1.5f;

        [Header("Chase / Search")]
        [SerializeField] float repathInterval = 0.4f;
        [SerializeField] float directChaseDistance = 4f;
        [SerializeField] float stopDistance = 0.6f;
        [SerializeField] float searchTimeout = 10f;

        [Header("Chase Timeout")]
        [Tooltip("Minimum chase duration in seconds. Set both timeout values to 0 for unlimited chasing.")]
        [SerializeField, Min(0f)] float chaseTimeoutMin = 0f;
        [Tooltip("Maximum chase duration in seconds. A duration is randomly chosen once per chase; equal values give a fixed duration.")]
        [SerializeField, Min(0f)] float chaseTimeoutMax = 0f;
        [Tooltip("Seconds spent wandering without detecting or stalking players after a chase times out. 0 allows immediate reacquisition.")]
        [SerializeField, Min(0f)] float chaseTimeoutCooldown = 5f;

        [Header("Head")]
        [SerializeField] float headTurnSmooth = 8f;
        [SerializeField] float headMaxYaw = 140f;
        [SerializeField] float headMaxPitch = 80f;

        [Header("Hit")]
        [SerializeField] float minHitSpeed = 2f;
        [SerializeField] float fullHitSpeed = 9f;
        [Tooltip("Launch speed (m/s) at full swing strength, before the weapon's knockback scale.")]
        [SerializeField] float maxLaunchSpeed = 14f;
        [SerializeField] float lift = 0.55f;
        [SerializeField] float hitCooldown = 0.25f;
        [SerializeField] float bodyHeight = 0.6f;
        [SerializeField] float bodyRadius = 0.3f;
        [SerializeField] float limbRadius = 0.1f;
        [SerializeField] float flightDamping = 0.15f;
        [SerializeField] float flightAngularDamping = 0.4f;
        [SerializeField] float tumbleSpeed = 2f;
        [SerializeField] float recoverSpeed = 1.25f;
        [SerializeField] float maxFlightTime = 4f;
        [Tooltip("Minimum retreat duration. Starts after the shove ends or the enemy lands.")]
        [FormerlySerializedAs("meleeWanderLock")]
        [SerializeField, Min(0f), InspectorName("Retreat Time Min")] float retreatTime = 4f;
        [SerializeField, Min(0f)] float retreatTimeMax = 10f;
        [SerializeField] bool retreatAfterKill;
        [SerializeField, Min(0f)] float flareFearRadius;
        bool _flareRetreat, _searchingFlareLocation;
        float _flareRepath;
        DeathFlare _flareThreat;
        Vector3 _flareDestination;
        bool _killRetreatPending, _searchKillLocation, _searchingKillLocation;
        Vector3 _killLocation;
        [SerializeField, Min(0f)] float retreatSpeed = 5f;
        [Tooltip("Turn toward the player while retreating. Off faces the retreat direction.")]
        [SerializeField] bool retreatLookAtPlayer = true;
        [SerializeField] float throwStunDuration = 1f;
        [SerializeField] float throwShoveDistance = 0.35f;
        [Tooltip("Yaw either side of the head pose captured when the stun starts.")]
        [SerializeField] float headShakeAngle = 25f;
        [Tooltip("Back-and-forth cycles across the stun.")]
        [SerializeField] float headShakeCycles = 3f;
        [Tooltip("One-shot spray played at the contact. Its cone points along local forward.")]
        [SerializeField] GameObject hitBloodPrefab;

        [Header("Fall recovery")]
        [SerializeField, Min(1f)] float fallRecoveryDelay = 20f;
        float _fallTime, _fallRetry;
        Vector3 _fallPreviousPosition;
        bool _fallSampleValid;
        bool _hasHitSafePose;
        Vector3 _hitSafePosition, _hitSafeUp;
        Quaternion _hitSafeRotation;
        float _blockedRecoveryTime;
        float _localRecoveryRetry;

        public State CurrentState { get; private set; }
        public Transform Root => root;
        public IGaitDriver Gait => _gait;
        public IEnemyLocomotion Locomotion => _loco;
        public bool HasAuthority => _net == null || _net.HasAuthority;
        // Dormant enemies still reserve their manager slot while waiting to respawn.
        public bool IsDormant { get; set; }
        public bool WantsPath => !IsDormant && _loco != null && _loco.WantsPath;
        public bool Seen => _seen;
        public Vector3 LastSeenPos => _lastSeenPos;
        public float DirectChaseDistance => directChaseDistance;
        public float StopDistance => stopDistance;
        public float RetreatSpeed => retreatSpeed;
        public float MinHitSpeed => minHitSpeed;
        public bool IsKnocked => _flying || _remoteKnocked;
        public bool RagdollActive => _ragdollActive;
        public bool SwingLocked => Time.time < _localSwingLock;
        // Local melee grace is never replicated: only the player who submitted the hit benefits.
        public bool ProtectsLocalPlayer => Time.time < _localMeleeGraceUntil
            || (_net != null && _net.ProtectsLocalHitter);
        public bool IsStunned => _stunned;
        public bool WantsHeadAim => CurrentState != State.Retreat || retreatLookAtPlayer;

        EnemyNetSync _net;
        IEnemyLocomotion _loco;
        IGaitDriver _gait;
        ClimbLocomotion _climb;
        GroundLocomotion _ground;
        ZombieSmasherJump _jump;
        ZombieSpiderStalk _stalk;
        public float IdleSpeedMultiplier => _stalk != null && _stalk.isActiveAndEnabled && _stalk.IsStalking
            ? _stalk.SpeedMultiplier : 1f;
        public bool IsJumping => _jump != null && _jump.IsJumping;

        public void SetAirbornePose(bool airborne)
        {
            _airborne = airborne;
            if (_gait is GorillaGait gorilla) gorilla.Airborne = airborne;
        }
        Rigidbody _body;
        CapsuleCollider _capsule;
        SpiderLeg[] _legs = System.Array.Empty<SpiderLeg>();
        IEnemyHitVocal _vocal;
        bool _hadAuthority;

        bool _flying;
        bool _remoteKnocked;
        bool _ragdollActive;
        float _ragdollTime;
        bool _grounded;
        Vector3 _landNormal = Vector3.up;
        float _localSwingLock;
        // Full shove (3m at 6m/s) plus a short contact handoff allowance.
        const float LocalMeleeGraceSeconds = 0.85f;
        float _localMeleeGraceUntil;
        float _nextHitTime;
        float _flightStartTime;
        LayerMask _flightExclude;
        bool _savedKinematic = true;
        bool _savedGravity;
        bool _savedDetectCollisions = true;
        bool _savedCapsule;
        float _savedDamping;
        float _savedAngularDamping;
        CollisionDetectionMode _savedDetection;
        RigidbodyInterpolation _savedInterpolation;
        LayerMask _savedExclude;

        const float ThrowShoveDuration = 0.2f;
        const float ThrowSlideSkin = 0.02f;
        const float MeleePushDistanceMin = 2f;
        const float MeleePushDistanceMax = 3f;
        const float MeleePushSpeed = 6f;
        const float MeleePushBlocked = 0.001f;
        const float AirNudgeMin = 1f;
        const float AirNudgeMax = 2f;
        bool _shoveActive;
        bool _airHitLock;
        bool _retreatActive;
        bool _dropActive;
        bool _dropFromShove;
        float _shoveRemaining;
        float _retreatRemaining;
        float _dropSpeed;
        Vector3 _meleeShoveDir;
        Vector3 _retreatDir;
        Vector3 _retreatProgressPosition;
        float _retreatStalledTime;
        const float RetreatYawJitter = 80f;
        bool _stunned;
        bool _airborne;
        float _stunTimer;
        float _stunAge;
        float _shoveTimeLeft;
        float _shoveDuration;
        float _shoveDistance;
        Vector3 _shoveDir;
        Quaternion _headShakeBase = Quaternion.identity;

        float _fovCos;
        int _target = -1;
        bool _seen;
        Vector3 _lastSeenPos;
        float _lastSeenTime;
        int _trailCursor;

        float _repathTimer, _stateTimer;
        float _nextFarPerception, _farPerceptionInterval;
        bool _hasPathRequestGoal;
        Vector3 _pathRequestGoal;
        int _pathRequestTarget;
        float _stablePathRefresh;
        float _chaseTimeoutRemaining, _chaseCooldownRemaining;
        bool _chaseTimeoutEnabled;

        Quaternion _headRest = Quaternion.identity;
        bool _remoteAim;
        Vector3 _remoteAimPoint;

        void Awake()
        {
            if (root == null) root = transform;
            if (eye == null) eye = root;
            if (head == null) head = eye != root ? eye : FindChild(transform, "head");
            if (head != null) _headRest = head.localRotation;
            _net = GetComponent<EnemyNetSync>();
            _body = root.GetComponent<Rigidbody>();
            if (_body != null) _body.isKinematic = true;
            _capsule = root.GetComponent<CapsuleCollider>();
            _legs = GetComponentsInChildren<SpiderLeg>(true);
            _vocal = GetComponent<IEnemyHitVocal>();
            _loco = GetComponent<IEnemyLocomotion>();
            _climb = _loco as ClimbLocomotion;
            _ground = _loco as GroundLocomotion;
            _jump = GetComponent<ZombieSmasherJump>();
            _stalk = GetComponent<ZombieSpiderStalk>();
            _gait = GetComponent<IGaitDriver>();
            if (_gait != null) _gait.ExternalTick = true;
            _fovCos = Mathf.Cos(fovDegrees * 0.5f * Mathf.Deg2Rad);
            int exclude = 0;
            int playerLayer = LayerMask.NameToLayer("Player");
            int grabbableLayer = LayerMask.NameToLayer("Grabbable");
            if (playerLayer >= 0) exclude |= 1 << playerLayer;
            if (grabbableLayer >= 0) exclude |= 1 << grabbableLayer;
            _flightExclude = exclude;
        }

        void OnEnable() => Active.Add(this);
        void OnDisable() { Active.Remove(this); ResetFallRecovery(); _localMeleeGraceUntil = 0f; }

        void OnValidate()
        {
            chaseTimeoutMin = Mathf.Max(0f, chaseTimeoutMin);
            chaseTimeoutMax = Mathf.Max(chaseTimeoutMin, chaseTimeoutMax);
            chaseTimeoutCooldown = Mathf.Max(0f, chaseTimeoutCooldown);
            throwStunDuration = Mathf.Max(0.05f, throwStunDuration);
            throwShoveDistance = Mathf.Max(0f, throwShoveDistance);
            headShakeAngle = Mathf.Max(0f, headShakeAngle);
            headShakeCycles = Mathf.Max(0f, headShakeCycles);
            retreatTime = Mathf.Max(0f, retreatTime);
            retreatTimeMax = Mathf.Max(retreatTime, retreatTimeMax);
            retreatSpeed = Mathf.Max(0f, retreatSpeed);
        }

        public void SetRemoteState(State state) => CurrentState = state;
        public void SetRemoteKnocked(bool knocked)
        {
            bool wasKnocked = _remoteKnocked;
            _remoteKnocked = knocked;
            if (wasKnocked && !knocked) EndLimbRagdoll();
        }

        public void ResetBrain()
        {
            _flareRetreat = _searchingFlareLocation = false;
            _flareThreat = null;
            _killRetreatPending = _searchKillLocation = _searchingKillLocation = false;
            _localMeleeGraceUntil = 0f;
            _nextFarPerception = _farPerceptionInterval = 0f;
            _hasPathRequestGoal = false;
            _hasHitSafePose = false;
            _blockedRecoveryTime = 0f;
            _localRecoveryRetry = 0f;
            ResetFallRecovery();
            ClearStun();
            ClearMeleeShove();
            if (_flying) AbortFlight();
            if (_jump != null) _jump.ResetJump();
            if (_stalk != null) _stalk.ResetStalk();
            SetAirbornePose(false);
            CurrentState = State.Wander;
            _target = -1;
            _seen = false;
            _chaseTimeoutEnabled = false;
            _chaseTimeoutRemaining = _chaseCooldownRemaining = 0f;
            if (_loco != null)
            {
                _loco.Reset();
                _loco.BeginWanderPause();
            }
            if (_climb != null) _climb.SyncUpFromRoot(root);
        }

        /// <summary>Writes the gait root through its rigidbody so physics does not snap the transform back. The transform is updated in the same call so later reads in this frame see the new pose.</summary>
        public void SetRootPose(Vector3 position, Quaternion rotation)
        {
            if (_body != null)
            {
                _body.position = position;
                _body.rotation = rotation;
            }
            if (root != null)
                root.SetPositionAndRotation(position, rotation);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_flying) AbortFlight();
            SetRootPose(position, rotation);
            // Teleports must be visible to AI/gait and network serialization immediately,
            // even before physics synchronizes the rigidbody pose back to the Transform.
            root.SetPositionAndRotation(position, rotation);
            ResetBrain();
        }

        internal bool ShouldPerceive(EnemyManager manager, int frame, int index, int nearbyInterval)
        {
            if (IsDormant) return false;
            // Only quiet, upright wanderers may use a slower vision cadence.
            // Movement, support, damage and network interpolation are never gated.
            bool responsive = !_hadAuthority || CurrentState != State.Wander || IsKnocked
                || IsJumping || IsStunned || RagdollActive || SwingLocked
                || _shoveActive || _retreatActive || _dropActive || _fallTime > 0f
                || (_stalk != null && _stalk.IsStalking)
                || (_climb != null && Vector3.Dot(_climb.SurfaceNormal, Vector3.up) < 0.95f)
                || root == null || Vector3.Dot(root.up, Vector3.up) < 0.95f;
            float distanceSq = responsive ? 0f : manager.NearestTargetDistanceSquared(root.position);
            float interval = distanceSq > 2500f ? 0.5f : distanceSq > 625f ? 0.2f : 0f;
            if (responsive || interval == 0f)
            {
                _nextFarPerception = _farPerceptionInterval = 0f;
                return !_hadAuthority || (frame + index) % nearbyInterval == 0;
            }
            if (_farPerceptionInterval != interval)
            {
                _farPerceptionInterval = interval;
                // A stable per-instance phase spreads distant checks across frames.
                _nextFarPerception = Time.time + interval * ((GetInstanceID() & 15) + 1) / 16f;
            }
            if (Time.time < _nextFarPerception) return false;
            _nextFarPerception = Time.time + interval;
            return true;
        }

        bool ShouldRefreshChasePath()
        {
            // Keep an existing route for a stationary target. Missing/finished
            // paths, target switches and moved goals still request at the normal
            // chase cadence; locomotion's forced recovery paths are untouched.
            if (_hasPathRequestGoal && _pathRequestTarget != _target) return true;
            if (_loco.WantsPath) return false;
            return !_hasPathRequestGoal || _loco.PathDone || _pathRequestTarget != _target
                || (_lastSeenPos - _pathRequestGoal).sqrMagnitude >= 0.25f
                || Time.time >= _stablePathRefresh;
        }

        public void TickAuthority(EnemyManager m, float dt, bool perceive)
        {
            if (IsDormant) { ResetFallRecovery(); return; }
            if (!_hadAuthority) { _hadAuthority = true; ResetBrain(); }
            if (!_flying && !IsJumping && (_shoveActive || _retreatActive || _dropActive))
            {
                if (!RepairHitOverlap(m, dt)) return;
                // Jump traversal normally validates the previous movement segment.
                // Keep that history current while hit reactions bypass TickJump,
                // otherwise a route around a wall becomes one false wall-crossing.
                if (_jump != null && _jump.isActiveAndEnabled && _ground != null
                    && _ground.ValidateSmasherPose(this, m.SurfaceMask))
                {
                    _jump.ResetJump();
                    SetAirbornePose(false);
                    return;
                }
                RememberHitSafePose(m.SurfaceMask);
            }
            if (TickFallRecovery(m, dt)) return;
            _remoteKnocked = false;
            if (_flying)
            {
                TickKnock();
                return;
            }
            if (_dropActive)
            {
                TickDrop(m, dt);
                return;
            }
            if (_shoveActive)
            {
                TickMeleeShove(m, dt);
                return;
            }
            if (_killRetreatPending)
            {
                // Finish airborne traversal safely, without acquiring another target.
                if (IsJumping && _jump != null)
                {
                    _jump.TickJump(this, m, dt);
                    SetAirbornePose(IsJumping);
                    if (IsJumping) return;
                }
                _killRetreatPending = false;
                BeginRetreat(m, root.position - _killLocation);
            }
            // An active retreat jump owns the pose, including its landing tick.
            // Flare and ground movement must not overwrite airborne traversal.
            if (_retreatActive && IsJumping)
            {
                TickRetreat(m, dt);
                return;
            }
            if (TickFlareFear(m, dt)) return;
            if (_retreatActive)
            {
                TickRetreat(m, dt);
                return;
            }
            if (_stunned)
            {
                TickStunSlide(m, dt);
                return;
            }
            _chaseCooldownRemaining = Mathf.Max(0f, _chaseCooldownRemaining - dt);
            if (perceive) Perceive(m);
            Think(m, dt);
            if (_jump != null && _jump.isActiveAndEnabled && _jump.TickJump(this, m, dt))
            {
                SetAirbornePose(IsJumping);
                if (_airHitLock && !IsJumping)
                    BeginRetreat(m, _meleeShoveDir);
                return;
            }
            if (_airHitLock && !IsJumping)
                BeginRetreat(m, _meleeShoveDir);
            SetAirbornePose(false);
            if (_loco != null) _loco.TickMove(this, m, dt);
        }

        void ResetFallRecovery()
        {
            _fallTime = _fallRetry = 0f;
            _fallSampleValid = false;
        }

        bool TickFallRecovery(EnemyManager manager, float dt)
        {
            if (!HasAuthority || IsDormant || root == null || dt <= 0f) return false;
            Vector3 position = _body != null ? _body.position : root.position;
            bool descending = _flying && _body != null
                ? _body.linearVelocity.y < -0.05f
                : (_jump != null && _jump.IsFalling) ||
                  (_fallSampleValid && position.y < _fallPreviousPosition.y - 0.05f * dt);
            _fallPreviousPosition = position;
            _fallSampleValid = true;

            // Only inspect support while falling or tracking an unresolved fall.
            if (_fallTime == 0f && !descending) return false;
            bool supported;
            if (_flying)
            {
                Bounds bounds = _capsule != null ? _capsule.bounds : new Bounds(position, Vector3.zero);
                supported = _grounded || (Physics.Raycast(bounds.center, Vector3.down,
                    out RaycastHit hit, bounds.extents.y + 0.15f, manager.SurfaceMask,
                    QueryTriggerInteraction.Ignore) && CrawlGraph.IsWalkableGround(hit.normal));
            }
            else if (_climb != null)
                supported = _climb.HasSurfaceSupport(position, manager.SurfaceMask);
            else if (_ground != null)
                supported = _ground.HasGroundSupport(position, manager.SurfaceMask);
            else
                supported = Physics.Raycast(position + Vector3.up * 0.1f, Vector3.down,
                    out RaycastHit floorHit, 0.25f, manager.SurfaceMask, QueryTriggerInteraction.Ignore)
                    && CrawlGraph.IsWalkableGround(floorHit.normal);

            if (supported) { _fallTime = _fallRetry = 0f; return false; }
            // Keep the deadline through knockback's mid-air Recover() and movement changes.
            _fallTime += dt;
            _fallRetry -= dt;
            if (_fallTime < Mathf.Max(1f, fallRecoveryDelay) || _fallRetry > 0f) return false;
            _fallRetry = 1f;
            string detail = "unsupported fall";
            if (_ground != null)
            {
                bool escapeFound = _ground.TryLocalHitEscape(position, manager.SurfaceMask, out Vector3 escape);
                string floor = Physics.Raycast(position + Vector3.up, Vector3.down, out RaycastHit floorProbe,
                    3f, manager.SurfaceMask, QueryTriggerInteraction.Ignore)
                    ? $"{floorProbe.collider.name}, point={floorProbe.point}, normal={floorProbe.normal}, walkable={CrawlGraph.IsWalkableGround(floorProbe.normal)}"
                    : "none within probe";
                detail += $"; drop={_dropActive}, dropSpeed={_dropSpeed:F3}, dropStall={_dropStallTime:F3}, "
                    + $"shove={_shoveActive}, retreat={_retreatActive}, retreatRemaining={_retreatRemaining:F3}, "
                    + $"jump={IsJumping}, overlap={_ground.Overlaps(position, manager.SurfaceMask)}, "
                    + $"localEscape={escapeFound}, escapePosition={escape}, floor=[{floor}]";
            }
            return manager.TryRespawnRandom(this, out _, out _, detail);
        }

        public void RunPathRequest(EnemyManager m)
        {
            if (_loco != null) _loco.RunPathRequest(m);
        }

        void Perceive(EnemyManager m)
        {
            if (_chaseCooldownRemaining > 0f) return;
            Vector3 eyePos = eye.position;
            Vector3 fwd = root.forward;
            float bestSq = visionRange * visionRange;
            float nearSq = nearSenseRadius * nearSenseRadius;
            int best = -1;
            Vector3 bestHead = default;

            for (int s = 0; s < EnemyManager.MaxTargets; s++)
            {
                if (!m.TryGetTarget(s, out Vector3 headPos)) continue;
                if (IsFlareProtected(headPos)) continue;
                Vector3 d = headPos - eyePos;
                float sq = d.sqrMagnitude;
                if (sq > bestSq) continue;
                float dist = Mathf.Sqrt(sq);
                if (sq > nearSq && Vector3.Dot(fwd, d) < _fovCos * dist) continue;
                if (dist > 0.01f && AIQueryProfile.VisionCast(eyePos, d / dist, out RaycastHit hit, dist, m.VisionMask)
                    && hit.distance < dist - 0.35f)
                    continue;
                best = s;
                bestSq = sq;
                bestHead = headPos;
            }

            _seen = best >= 0;
            if (!_seen) return;

            _target = best;
            _searchingFlareLocation = false;
            _searchingKillLocation = false;
            _lastSeenPos = bestHead;
            _lastSeenTime = Time.time;
            _trailCursor = m.TrailSeq(best);
            if (CurrentState != State.Chase)
            {
                if (_stalk != null) _stalk.StopStalking(this);
                CurrentState = State.Chase;
                _hasPathRequestGoal = false;
                _repathTimer = 0f;
                float min = Mathf.Max(0f, chaseTimeoutMin);
                float max = Mathf.Max(min, chaseTimeoutMax);
                _chaseTimeoutEnabled = max > 0f;
                _chaseTimeoutRemaining = _chaseTimeoutEnabled ? Random.Range(min, max) : 0f;
            }
        }

        void Think(EnemyManager m, float dt)
        {
            if (_loco == null) return;
            if (_target >= 0 && !m.TryGetTarget(_target, out Vector3 _)) { _target = -1; _seen = false; }

            switch (CurrentState)
            {
                case State.Wander:
                    if (_chaseCooldownRemaining <= 0f && _stalk != null && _stalk.isActiveAndEnabled && _stalk.TickStalk(this, m, dt))
                        break;
                    if (_loco.PathDone && !_loco.WantsPath && _loco.TickWanderPause(dt))
                        _loco.RequestWander(m);
                    break;

                case State.Chase:
                    if (_chaseTimeoutEnabled)
                    {
                        _chaseTimeoutRemaining -= dt;
                        if (_chaseTimeoutRemaining <= 0f)
                        {
                            _chaseTimeoutEnabled = false;
                            _chaseCooldownRemaining = Mathf.Max(0f, chaseTimeoutCooldown);
                            _seen = false;
                            if (_stalk != null) _stalk.StopStalking(this);
                            EnterWander();
                            break;
                        }
                    }
                    if (!_seen && Time.time - _lastSeenTime > loseSightTime)
                    {
                        EnterSearch(m);
                        break;
                    }
                    _repathTimer -= dt;
                    if (_repathTimer <= 0f)
                    {
                        _repathTimer = repathInterval;
                        if (ShouldRefreshChasePath())
                        {
                            _loco.RequestPathToWorld(m, _lastSeenPos);
                            _pathRequestGoal = _lastSeenPos;
                            _pathRequestTarget = _target;
                            _hasPathRequestGoal = true;
                            _stablePathRefresh = Time.time + Mathf.Max(repathInterval, 1.2f);
                        }
                    }
                    break;

                case State.Search:
                    _stateTimer -= dt;
                    if (_stateTimer <= 0f || (_target < 0 && !_searchingKillLocation && !_searchingFlareLocation)) { EnterWander(); break; }
                    if (_searchingKillLocation || _searchingFlareLocation) break;
                    if (_loco.PathDone && !_loco.WantsPath && m.TryGetTrailPoint(_target, ref _trailCursor, out Vector3 p))
                    {
                        _trailCursor++;
                        _loco.ForcePathToWorld(m, p);
                    }
                    break;
            }
        }

        void EnterWander()
        {
            _searchingFlareLocation = false;
            _searchingKillLocation = false;
            CurrentState = State.Wander;
            _target = -1;
            if (_loco != null)
            {
                _loco.ClearPath();
                _loco.BeginWanderPause();
            }
        }

        void EnterSearch(EnemyManager m)
        {
            if (IsFlareProtected(_lastSeenPos))
            {
                // Search from the retreat endpoint instead of returning to the safe zone.
                _lastSeenPos = root.position;
                _target = -1;
                _searchingFlareLocation = true;
            }
            CurrentState = State.Search;
            _stateTimer = searchTimeout;
            if (_loco != null) _loco.ForcePathToWorld(m, _lastSeenPos);
        }

        public bool TryGetAimPoint(out Vector3 point)
        {
            point = default;
            if (_target < 0 || EnemyManager.Instance == null) return false;
            return EnemyManager.Instance.TryGetTarget(_target, out point);
        }

        public void SetRemoteAim(bool hasAim, Vector3 point)
        {
            _remoteAim = hasAim;
            if (hasAim) _remoteAimPoint = point;
        }

        public void AimHead(float dt)
        {
            if (IsDormant) return;
            if (head == null || dt <= 0f) return;

            Quaternion desired = _headRest;
            Vector3 point = _remoteAimPoint;
            bool aim = HasAuthority ? TryGetAimPoint(out point) : _remoteAim;

            if (aim)
            {
                Vector3 dir = point - head.position;
                if (dir.sqrMagnitude > 1e-4f)
                {
                    Quaternion parentRot = head.parent != null ? head.parent.rotation : Quaternion.identity;
                    Vector3 localDir = Quaternion.Inverse(parentRot * _headRest) * dir.normalized;
                    float horiz = Mathf.Sqrt(localDir.x * localDir.x + localDir.z * localDir.z);
                    float yaw = horiz > 0.02f ? Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg : 0f;
                    float pitch = Mathf.Atan2(-localDir.y, horiz) * Mathf.Rad2Deg;
                    yaw = Mathf.Clamp(yaw, -headMaxYaw, headMaxYaw);
                    pitch = Mathf.Clamp(pitch, -headMaxPitch, headMaxPitch);
                    desired = _headRest * Quaternion.AngleAxis(yaw, Vector3.up) * Quaternion.AngleAxis(pitch, Vector3.right);
                }
            }

            head.localRotation = Quaternion.Slerp(head.localRotation, desired, 1f - Mathf.Exp(-headTurnSmooth * dt));
            if (_gait is SpiderGait spider) spider.ResolveHeadClearance(head);
        }

        public void TickStunHead(float dt)
        {
            if (!_stunned || dt <= 0f) return;
            _stunAge += dt;
            if (head != null && throwStunDuration > 0f)
            {
                float turns = headShakeCycles * Mathf.PI * 2f;
                float yaw = Mathf.Sin(_stunAge / throwStunDuration * turns) * headShakeAngle;
                head.localRotation = _headShakeBase * Quaternion.AngleAxis(yaw, Vector3.up);
            }
            _stunTimer -= dt;
            if (_stunTimer <= 0f) ClearStun();
        }

        /// <summary>Horizontal push out of the body. Penetration is how far the point sits inside the body plus the item radius.</summary>
        public void ThrownBodyContact(Vector3 point, float itemRadius, out Vector3 normal, out float penetration)
        {
            Vector3 up = root != null && root.up.sqrMagnitude > 1e-6f ? root.up.normalized : Vector3.up;
            Vector3 center = root != null ? root.position + up * (bodyHeight * 0.5f) : point;
            Vector3 away = Vector3.ProjectOnPlane(point - center, up);
            if (away.sqrMagnitude < 1e-6f)
                away = root != null ? Vector3.ProjectOnPlane(root.forward, up) : Vector3.forward;
            if (away.sqrMagnitude < 1e-6f) away = Vector3.forward;
            normal = away.normalized;
            float inside = bodyRadius + itemRadius - away.magnitude;
            penetration = inside > 0f ? inside : 0f;
        }

        public bool OverlapsSwing(Vector3 from, Vector3 to, float sweepRadius)
        {
            if (root == null) return false;
            Vector3 up = root.up.sqrMagnitude > 1e-6f ? root.up.normalized : Vector3.up;
            Vector3 bodyEnd = root.position + up * bodyHeight;
            if (SegmentsWithin(from, to, root.position, bodyEnd, sweepRadius + bodyRadius))
                return true;
            float limbReach = sweepRadius + limbRadius;
            for (int i = 0; i < _legs.Length; i++)
            {
                SpiderLeg leg = _legs[i];
                if (leg == null) continue;
                if (SegmentsWithin(from, to, leg.HipWorld, leg.FootWorld, limbReach))
                    return true;
            }
            return false;
        }

        // Visual placement only, after the existing sweep accepts a hit. Prefix overlap
        // is monotonic even across multiple limbs; bounded refinement needs no allocations.
        public Vector3 HitBloodPosition(Vector3 from, Vector3 to, float sweepRadius)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (root == null || length < 1e-6f) return to;
            Vector3 direction = delta / length;
            if (OverlapsSwing(from, from, sweepRadius))
            {
                // A later swing can begin inside. Back up beyond all collision shapes.
                float reach = bodyHeight + bodyRadius;
                for (int i = 0; i < _legs.Length; i++)
                {
                    SpiderLeg leg = _legs[i];
                    if (leg == null) continue;
                    reach = Mathf.Max(reach, Mathf.Max(Vector3.Distance(root.position, leg.HipWorld),
                        Vector3.Distance(root.position, leg.FootWorld)) + limbRadius);
                }
                from -= direction * (Vector3.Distance(from, root.position) + reach + sweepRadius + 0.02f);
            }
            float low = 0f, high = 1f;
            for (int i = 0; i < 12; i++)
            {
                float middle = (low + high) * 0.5f;
                if (OverlapsSwing(from, Vector3.LerpUnclamped(from, to, middle), sweepRadius)) high = middle;
                else low = middle;
            }
            return Vector3.LerpUnclamped(from, to, low) - direction * 0.02f;
        }

        public void LockSwing() => _localSwingLock = Time.time + hitCooldown;

        public void SubmitSwing(Vector3 swingVelocity, float weaponScale, Vector3 hitPosition = default)
        {
            // Match the local hit acceptance gates. Sound-only cooldown contacts and
            // RPCs received from another player must not extend this protection.
            if (IsDormant || swingVelocity.magnitude <= minHitSpeed || Time.time < _nextHitTime) return;
            _localMeleeGraceUntil = Time.time + LocalMeleeGraceSeconds;
            if (_vocal != null) _vocal.PlayWhimper();
            if (_net != null) _net.SubmitSwing(swingVelocity, weaponScale, hitPosition);
            else ReceiveSwing(swingVelocity, weaponScale, hitPosition);
        }

        public void ReceiveSwing(Vector3 swingVelocity, float weaponScale, Vector3 hitPosition = default)
        {
            BeginHitPush(swingVelocity, hitPosition);
        }

        void BeginMeleeShove(Vector3 dir, float distance)
        {
            _airHitLock = false;
            _retreatActive = false;
            _retreatRemaining = 0f;
            _meleeShoveDir = dir;
            _shoveRemaining = distance;
            _chaseTimeoutEnabled = false;
            if (_stalk != null) _stalk.StopStalking(this);
            if (_loco != null) _loco.Interrupt();
            _shoveActive = true;
        }

        void RememberHitSafePose(LayerMask mask)
        {
            if (root == null || BodyOverlaps(root.position, mask) || !HasMoveSupport(root.position, mask)) return;
            _hitSafePosition = root.position;
            _hitSafeUp = _climb != null ? _climb.SurfaceNormal : SupportUp();
            _hitSafeRotation = RecoveryRotation(root.rotation, _hitSafeUp);
            _hasHitSafePose = true;
        }

        static Quaternion RecoveryRotation(Quaternion rotation, Vector3 up)
        {
            return Quaternion.FromToRotation(rotation * Vector3.up, up) * rotation;
        }

        bool CanRestoreHitPose(LayerMask mask, out string reason)
        {
            if (!_hasHitSafePose) { reason = "no saved pose"; return false; }
            if (Vector3.Distance(root.position, _hitSafePosition) > 3f)
            { reason = "saved pose farther than 3 units"; return false; }
            if (_climb != null) return _climb.IsRecoveryPoseValid(_hitSafePosition, _hitSafeUp, mask, out reason);
            if (BodyOverlaps(_hitSafePosition, mask)) { reason = "saved body overlaps geometry"; return false; }
            if (!HasMoveSupport(_hitSafePosition, mask)) { reason = "saved pose unsupported"; return false; }
            reason = "valid";
            return true;
        }

        // Never turn unresolved penetration into a fall, or commit a failed correction.
        bool RepairHitOverlap(EnemyManager m, float dt)
        {
            if (root == null || m == null) return false;
            Vector3 position = root.position;
            if (!BodyOverlaps(position, m.SurfaceMask))
            { _blockedRecoveryTime = _localRecoveryRetry = 0f; return true; }
            Quaternion rotation = root.rotation;
            bool separated = TrySeparate(ref position, m.SurfaceMask);
            bool clear = separated;
            string savedPoseReason = "separation succeeded";
            if (!clear && CanRestoreHitPose(m.SurfaceMask, out savedPoseReason))
            {
                position = _hitSafePosition;
                rotation = _hitSafeRotation;
                clear = true;
            }
            _localRecoveryRetry -= dt;
            if (!clear && _ground != null && _localRecoveryRetry <= 0f)
            {
                _localRecoveryRetry = .5f;
                clear = _ground.TryLocalHitEscape(root.position, m.SurfaceMask, out position);
            }
            if (!clear && _climb != null && _localRecoveryRetry <= 0f)
            {
                _localRecoveryRetry = .5f;
                clear = _climb.TryLocalRecovery(root.position, m.SurfaceMask, out position, out Vector3 up);
                if (clear) rotation = RecoveryRotation(root.rotation, up);
                else if (_blockedRecoveryTime == 0f)
                    {
                        if (DuckzLog.AllowWarning(out var logNotice1))
                            logNotice1.Write($"Enemy overlap recovery blocked: {name}, position={root.position}, surface={_climb.SurfaceNormal}, savedPose={savedPoseReason}, localEscape=none", this);
                    }
            }
            if (clear)
            {
                // Separation used the collision normal, which can lead the smoothed visual up.
                if (_climb != null && separated)
                    rotation = RecoveryRotation(rotation, _climb.SurfaceNormal);
                SetRootPose(position, rotation);
                if (_climb != null) _climb.SyncUpFromRoot(root);
                ResetFallRecovery();
                _blockedRecoveryTime = _localRecoveryRetry = 0f;
                ClearMeleeShove();
                BeginRetreat(m, _meleeShoveDir);
                return true;
            }
            _blockedRecoveryTime += dt;
            if (_blockedRecoveryTime >= Mathf.Max(1f, fallRecoveryDelay))
            {
                _blockedRecoveryTime = Mathf.Max(1f, fallRecoveryDelay) - 1f;
                string detail = _climb != null
                    ? $"unresolved hit overlap; savedPose={savedPoseReason}; localEscape=none; colliders=[{_climb.DescribeOverlap(root.position, m.SurfaceMask)}]"
                    : "unresolved hit overlap";
                m.TryRespawnRandom(this, out _, out _, detail);
            }
            return false;
        }

        void TickMeleeShove(EnemyManager m, float dt)
        {
            if (root == null || dt <= 0f || _shoveRemaining <= 0f)
            {
                FinishMeleeShove();
                return;
            }

            LayerMask mask = m != null ? m.SurfaceMask : Physics.DefaultRaycastLayers;
            Vector3 pos = root.position;
            if (BodyOverlaps(pos, mask))
            {
                if (!TrySeparate(ref pos, mask)) return;
                SetRootPose(pos, root.rotation);
            }
            if (!HasMoveSupport(pos, mask))
            {
                BeginDrop(true);
                TickDrop(m, dt);
                return;
            }
            if (BodyOverlaps(pos, mask))
            {
                FinishMeleeShove();
                return;
            }

            float step = MeleePushSpeed * dt;
            if (step > _shoveRemaining) step = _shoveRemaining;
            Vector3 delta = _meleeShoveDir * step;
            Vector3 next;
            if (_ground != null) next = _ground.Displace(pos, delta, mask);
            else if (_climb != null) next = _climb.Displace(pos, delta, mask);
            else next = SlideOnSurface(pos, delta, mask);

            float along = Vector3.Dot(next - pos, _meleeShoveDir);
            SetRootPose(next, root.rotation);
            if (!HasMoveSupport(next, mask))
            {
                BeginDrop(true);
                return;
            }
            if (along <= MeleePushBlocked)
            {
                FinishMeleeShove();
                return;
            }
            _shoveRemaining -= along;
            if (_shoveRemaining <= MeleePushBlocked) FinishMeleeShove();
        }

        void FinishMeleeShove()
        {
            Vector3 shoveDir = _meleeShoveDir;
            ClearMeleeShove();
            BeginRetreat(EnemyManager.Instance, shoveDir);
        }

        void ClearMeleeShove()
        {
            _shoveActive = false;
            _shoveRemaining = 0f;
            _airHitLock = false;
            _retreatActive = false;
            _retreatRemaining = 0f;
            _dropActive = false;
            _dropFromShove = false;
            _dropSpeed = 0f;
        }

        public bool IsFlareProtected(Vector3 position) => flareFearRadius > 0f
            && DeathFlare.TryFindNear(position, flareFearRadius, out _);

        public bool AllowsFlareMovement(Vector3 from, Vector3 to) => flareFearRadius <= 0f
            || DeathFlare.AllowsMovement(from, to, flareFearRadius);

        bool TickFlareFear(EnemyManager manager, float dt)
        {
            if (!HasAuthority || flareFearRadius <= 0f || root == null || _loco == null) return false;
            DeathFlare nearby = DeathFlare.FindNear(root.position, flareFearRadius);
            if (nearby != null && (!_flareRetreat || _flareThreat != nearby))
            {
                _flareThreat = nearby;
                Vector3 up = SupportUp();
                Vector3 offset = root.position - nearby.transform.position;
                Vector3 away = Vector3.ProjectOnPlane(offset, up);
                if (away.sqrMagnitude < 0.001f) away = Vector3.ProjectOnPlane(root.forward, up);
                if (away.sqrMagnitude < 0.001f) away = Vector3.Cross(up, Vector3.right);
                if (away.sqrMagnitude < 0.001f) away = Vector3.Cross(up, Vector3.forward);
                away = Quaternion.AngleAxis(Random.Range(-40f, 40f), up) * away.normalized;
                float distance = Random.Range(10f, 14f);
                float height = Vector3.Dot(offset, up);
                // Pick on the spider's support plane, at the chosen 3D distance.
                _flareDestination = nearby.transform.position + up * height
                    + away * Mathf.Sqrt(Mathf.Max(0f, distance * distance - height * height));
                int destinationNode = manager.ResolveNode(_flareDestination);
                if (destinationNode >= 0)
                {
                    Vector3 snapped = manager.Graph.GetPosition(destinationNode);
                    float fromFlare = Vector3.Distance(snapped, nearby.transform.position);
                    if (fromFlare >= 10f && fromFlare <= 14f && !IsFlareProtected(snapped))
                        _flareDestination = snapped;
                }
                _flareRepath = 0f;
                if (_stalk != null) _stalk.StopStalking(this);
                _loco.Interrupt();
                _flareRetreat = true;
            }
            bool reached = (root.position - _flareDestination).sqrMagnitude <= 0.25f;
            bool burning = _flareThreat != null && _flareThreat.IsBurning;
            // A crawl graph may end just short of the requested world position.
            reached |= burning && _loco.PathDone && !_loco.WantsPath
                && Vector3.Distance(root.position, _flareThreat.transform.position) >= 10f;
            if (!_flareRetreat || !burning || (nearby == null && reached))
            {
                if (_flareRetreat)
                {
                    _flareRetreat = false;
                    _flareThreat = null;
                    _loco.Interrupt();
                    if (_retreatActive) return false; // Preserve an interrupted hit/kill retreat.
                    _searchingFlareLocation = true;
                    EnterSearch(manager);
                }
                return false;
            }
            CurrentState = State.Retreat;
            _seen = false;
            _chaseTimeoutEnabled = false;
            _flareRepath -= dt;
            if (_flareRepath <= 0f)
            {
                _loco.ForcePathToWorld(manager, _flareDestination);
                _flareRepath = 0.3f;
            }
            _loco.TickMove(this, manager, dt);
            return true;
        }

        public void SubmitPlayerKill(Vector3 position)
        {
            if (!retreatAfterKill || IsDormant) return;
            if (_net != null) _net.SubmitPlayerKill(position);
            else ReceivePlayerKill(position);
        }

        public void ReceivePlayerKill(Vector3 position)
        {
            if (!HasAuthority || !retreatAfterKill || IsDormant) return;
            _killLocation = position;
            _searchingKillLocation = false;
            _searchKillLocation = _killRetreatPending = true;
            _target = -1;
            _seen = false;
            _chaseTimeoutEnabled = false;
            if (_stalk != null) _stalk.StopStalking(this);
            CurrentState = State.Retreat;
        }

        void BeginRetreat(EnemyManager m, Vector3 fallbackDir)
        {
            _airHitLock = false;
            _retreatActive = false;
            if (!_searchKillLocation) RememberPlayer(m);
            float duration = Random.Range(Mathf.Max(0f, retreatTime), Mathf.Max(retreatTime, retreatTimeMax));
            if (duration <= 0f)
            {
                FinishRetreat(m);
                return;
            }

            Vector3 support = SupportUp();
            Vector3 away = Vector3.ProjectOnPlane(fallbackDir, support);
            if (_searchKillLocation && root != null)
                away = Vector3.ProjectOnPlane(root.position - _killLocation, support);
            else if (root != null && TryPlayerPoint(m, out Vector3 player))
            {
                Vector3 fromPlayer = Vector3.ProjectOnPlane(root.position - player, support);
                if (fromPlayer.sqrMagnitude > 1e-4f) away = fromPlayer;
            }
            if (away.sqrMagnitude < 1e-6f && root != null)
                away = Vector3.ProjectOnPlane(root.forward, support);
            if (away.sqrMagnitude < 1e-6f) away = Vector3.forward;
            away.Normalize();
            _retreatDir = Quaternion.AngleAxis(Random.Range(-RetreatYawJitter, RetreatYawJitter), support) * away;
            _retreatRemaining = duration;
            _retreatProgressPosition = root != null ? root.position : Vector3.zero;
            _retreatStalledTime = 0f;
            _retreatActive = true;
            _seen = false;
            _chaseTimeoutEnabled = false;
            CurrentState = State.Retreat;
            if (_stalk != null) _stalk.StopStalking(this);
            if (_loco != null)
            {
                _loco.Interrupt();
                float travel = retreatSpeed * duration;
                if (travel < 2f) travel = 2f;
                _loco.RequestRetreat(m, _retreatDir, travel);
            }
        }

        void TickRetreat(EnemyManager m, float dt)
        {
            if (root == null || dt <= 0f || m == null)
            {
                FinishRetreat(m);
                return;
            }

            if (_jump != null && _jump.isActiveAndEnabled && IsJumping)
            {
                _jump.TickJump(this, m, dt);
                SetAirbornePose(IsJumping);
                _retreatProgressPosition = root.position;
                _retreatStalledTime = 0f;
                return; // Keep retreat time for the escape after landing.
            }
            LayerMask mask = m.SurfaceMask;
            Vector3 pos = root.position;
            if (BodyOverlaps(pos, mask))
            {
                if (!TrySeparate(ref pos, mask)) return;
                SetRootPose(pos, root.rotation);
            }
            if (!HasMoveSupport(pos, mask))
            {
                BeginDrop(false);
                TickDrop(m, dt);
                return;
            }
            if (BodyOverlaps(pos, mask))
            {
                _retreatRemaining -= dt;
                if (_retreatRemaining <= 0f) FinishRetreat(m);
                return;
            }

            _retreatRemaining -= dt;
            if (_loco != null)
            {
                if (_loco.PathDone && !_loco.WantsPath)
                {
                    float travel = retreatSpeed * _retreatRemaining;
                    if (travel < 2f) travel = 2f;
                    _loco.RequestRetreat(m, _retreatDir, travel);
                }
                _loco.TickMove(this, m, dt);
            }
            if (!_retreatActive) return;

            pos = root.position;
            if (!HasMoveSupport(pos, mask))
            {
                BeginDrop(false);
                return;
            }

            // Observe actual walking progress without requesting paths or probing physics.
            // A quarter metre of horizontal progress gives walking another full second.
            if (_jump != null && _jump.isActiveAndEnabled)
            {
                Vector3 progress = pos - _retreatProgressPosition;
                progress.y = 0f;
                if (progress.sqrMagnitude >= .25f * .25f)
                {
                    _retreatProgressPosition = pos;
                    _retreatStalledTime = 0f;
                }
                else _retreatStalledTime += dt;
                if (_retreatRemaining > 0f && _retreatStalledTime >= 1f
                    && _jump.TryStartRetreatJump(this, mask, _retreatDir))
                {
                    SetAirbornePose(true);
                    return;
                }
            }

            if (retreatLookAtPlayer && TryPlayerPoint(m, out Vector3 player))
            {
                Vector3 support = root.up.sqrMagnitude > 1e-6f ? root.up.normalized : Vector3.up;
                Vector3 toPlayer = Vector3.ProjectOnPlane(player - pos, support);
                if (toPlayer.sqrMagnitude > 1e-4f)
                {
                    Quaternion look = Quaternion.LookRotation(toPlayer.normalized, support);
                    if (_body != null) _body.rotation = look;
                    root.rotation = look;
                }
                _lastSeenPos = player;
            }
            if (_retreatRemaining <= 0f) FinishRetreat(m);
        }

        void BeginDrop(bool fromShove)
        {
            if (_dropActive) return;
            _dropActive = true;
            _dropSpeed = 0f;
            _dropStallTime = 0f;
            _dropStallOrigin = root.position;
            _dropFromShove = fromShove;
            if (fromShove)
            {
                _shoveActive = false;
                _shoveRemaining = 0f;
            }
        }

        float _dropStallTime;
        Vector3 _dropStallOrigin;

        void TickDrop(EnemyManager m, float dt)
        {
            if (root == null || dt <= 0f || m == null)
            {
                CompleteDrop(m);
                return;
            }

            LayerMask mask = m.SurfaceMask;
            Vector3 pos = root.position;
            bool landed = false;
            Vector3 normal = Vector3.up;
            if (_ground != null)
                landed = _ground.TickFall(ref pos, ref _dropSpeed, dt, mask, out normal);
            else if (_climb != null)
                landed = _climb.TickFall(ref pos, ref _dropSpeed, dt, mask, out normal);
            else
            {
                _dropSpeed += GroundLocomotion.FallGravity * dt;
                pos += Vector3.down * (_dropSpeed * dt);
            }

            if (!landed)
            {
                SetRootPose(pos, root.rotation);
                // A capsule can be blocked by a slope without overlapping it or
                // finding walkable support. Overlap recovery cannot handle that case.
                if (_ground != null)
                {
                    if (Vector3.Distance(pos, _dropStallOrigin) > 0.1f)
                    {
                        _dropStallOrigin = pos;
                        _dropStallTime = 0f;
                    }
                    else _dropStallTime += dt;
                    if (_dropStallTime >= 0.5f)
                    {
                        _dropStallTime = 0f;
                        if (_ground.TryLocalHitEscape(pos, mask, out Vector3 escape))
                        {
                            SetRootPose(escape, root.rotation);
                            ResetFallRecovery();
                            CompleteDrop(m);
                        }
                    }
                }
                return;
            }

            if (_ground != null)
            {
                Vector3 bottom = _ground.SupportedCapsuleBottom(pos, mask);
                pos = _ground.AdoptLanding(bottom, normal);
                SetRootPose(pos, root.rotation);
            }
            else if (_climb != null)
            {
                Vector3 support = normal.sqrMagnitude > 1e-6f ? normal.normalized : Vector3.up;
                Vector3 forward = Vector3.ProjectOnPlane(root.forward, support);
                if (forward.sqrMagnitude < 1e-6f)
                    forward = Vector3.ProjectOnPlane(Vector3.forward, support);
                if (forward.sqrMagnitude > 1e-6f)
                    SetRootPose(pos, Quaternion.LookRotation(forward.normalized, support));
                else
                    SetRootPose(pos, root.rotation);
                _climb.SyncUpFromRoot(root);
            }
            else
                SetRootPose(pos, root.rotation);
            CompleteDrop(m);
        }

        void CompleteDrop(EnemyManager m)
        {
            bool fromShove = _dropFromShove;
            _dropActive = false;
            _dropSpeed = 0f;
            _dropFromShove = false;
            if (fromShove)
            {
                BeginRetreat(m, _meleeShoveDir);
                return;
            }
            if (!_retreatActive || _loco == null || m == null) return;
            float travel = retreatSpeed * _retreatRemaining;
            if (travel < 2f) travel = 2f;
            _loco.ClearPath();
            _loco.RequestRetreat(m, _retreatDir, travel);
        }

        bool BodyOverlaps(Vector3 position, LayerMask mask)
        {
            if (_ground != null) return _ground.Overlaps(position, mask);
            if (_climb != null) return _climb.Overlaps(position, mask);
            return false;
        }

        bool TrySeparate(ref Vector3 position, LayerMask mask)
        {
            if (_ground != null) return _ground.Separate(ref position, mask);
            if (_climb != null) return _climb.Separate(ref position, mask);
            return true;
        }

        bool HasMoveSupport(Vector3 position, LayerMask mask)
        {
            if (_ground != null) return _ground.HasGroundSupport(position, mask);
            if (_climb != null) return _climb.HasSurfaceSupport(position, mask);
            return true;
        }

        void FinishRetreat(EnemyManager m)
        {
            _retreatActive = false;
            _retreatRemaining = 0f;
            if (_searchKillLocation)
            {
                _lastSeenPos = _killLocation;
                _target = -1;
                _searchKillLocation = false;
                _searchingKillLocation = true;
            }
            else if (m != null && TryPlayerPoint(m, out Vector3 player))
                _lastSeenPos = player;
            if (m != null) EnterSearch(m);
            else EnterWander();
        }

        void RememberPlayer(EnemyManager m)
        {
            if (m == null) return;
            if (_target >= 0 && m.TryGetTarget(_target, out Vector3 current))
            {
                _lastSeenPos = current;
                return;
            }

            float bestSq = float.PositiveInfinity;
            int best = -1;
            Vector3 bestPos = default;
            Vector3 origin = root != null ? root.position : Vector3.zero;
            for (int slot = 0; slot < EnemyManager.MaxTargets; slot++)
            {
                if (!m.TryGetTarget(slot, out Vector3 point)) continue;
                float sq = (point - origin).sqrMagnitude;
                if (sq >= bestSq) continue;
                bestSq = sq;
                best = slot;
                bestPos = point;
            }
            if (best < 0) return;
            _target = best;
            _lastSeenPos = bestPos;
            _lastSeenTime = Time.time;
        }

        bool TryPlayerPoint(EnemyManager m, out Vector3 point)
        {
            point = _lastSeenPos;
            if (m == null || _target < 0) return _target >= 0;
            return m.TryGetTarget(_target, out point);
        }

        Vector3 SupportUp()
        {
            if (_climb != null && root != null && root.up.sqrMagnitude > 1e-6f)
                return root.up.normalized;
            return Vector3.up;
        }

        public void SubmitThrownHit(Vector3 throwVelocity, Vector3 hitPosition = default)
        {
            float speed = throwVelocity.magnitude;
            if (IsDormant || speed <= minHitSpeed) return;
            if (Time.time < _nextHitTime) return;
            _localMeleeGraceUntil = Time.time + LocalMeleeGraceSeconds;
            if (_vocal != null) _vocal.PlayWhimper();
            if (_net != null) _net.SubmitThrownHit(throwVelocity, hitPosition);
            else ReceiveThrownHit(throwVelocity, hitPosition);
        }

        public void ReceiveThrownHit(Vector3 throwVelocity, Vector3 hitPosition = default)
        {
            BeginHitPush(throwVelocity, hitPosition);
        }

        // The submitting client already applied its acceptance gates. A different
        // client's hit must not be lost to the authority's arrival-order cooldown.
        public void ReceiveTrustedHit(Vector3 velocity, Vector3 hitPosition)
        {
            float localHitGate = _nextHitTime;
            BeginHitPush(velocity, hitPosition, true);
            _nextHitTime = localHitGate;
        }

        public void ReceiveRemoteHitEffect(Vector3 velocity, Vector3 hitPosition)
        {
            // Another hitter's feedback must not consume this client's hit gate.
            SpawnHitBlood(velocity, hitPosition);
        }

        public float LocalPushDistance(Vector3 velocity, out Vector3 direction)
        {
            Vector3 up = _climb != null && root != null ? root.up : Vector3.up;
            direction = Vector3.ProjectOnPlane(velocity, up).normalized;
            float strength = Mathf.Clamp01((velocity.magnitude - minHitSpeed)
                / Mathf.Max(0.01f, fullHitSpeed - minHitSpeed));
            return direction.sqrMagnitude > 0f
                ? (_airborne ? Mathf.Lerp(AirNudgeMin, AirNudgeMax, strength)
                    : Mathf.Lerp(MeleePushDistanceMin, MeleePushDistanceMax, strength)) : 0f;
        }

        // Only displacement runs on the hitter. No brain, path requests or retreat AI.
        public float MoveLocalPush(Vector3 direction, float remaining, float dt)
        {
            if (root == null || remaining <= 0f || dt <= 0f) return 0f;
            LayerMask mask = EnemyManager.Instance != null ? EnemyManager.Instance.SurfaceMask : Physics.DefaultRaycastLayers;
            Vector3 pos = root.position;
            if (BodyOverlaps(pos, mask)) return 0f;
            if (!_airborne && !HasMoveSupport(pos, mask)) return 0f;
            Vector3 delta = direction * Mathf.Min(remaining, MeleePushSpeed * dt);
            Vector3 next = _airborne ? SlideOnSurface(pos, delta, mask)
                : _ground != null ? _ground.Displace(pos, delta, mask)
                : _climb != null ? _climb.Displace(pos, delta, mask) : SlideOnSurface(pos, delta, mask);
            SetRootPose(next, root.rotation);
            float moved = Vector3.Dot(next - pos, direction);
            return moved <= MeleePushBlocked ? 0f : Mathf.Max(0f, remaining - moved);
        }

        void BeginHitPush(Vector3 velocity, Vector3 hitPosition, bool trusted = false)
        {
            float speed = velocity.magnitude;
            float speedSpan = Mathf.Max(0.01f, fullHitSpeed - minHitSpeed);
            if (speed < minHitSpeed) return;
            if (!trusted && Time.time < _nextHitTime) return;
            float strength = Mathf.Clamp01((speed - minHitSpeed) / speedSpan);
            if (strength <= 0f) return;

            _nextHitTime = Time.time + hitCooldown;
            ClearStun();
            SpawnHitBlood(velocity, hitPosition);
            if (!HasAuthority || root == null) return;
            GetComponent<RailCoalCarrier>()?.Hit();

            Vector3 support = _climb != null && root.up.sqrMagnitude > 1e-6f ? root.up.normalized : Vector3.up;
            Vector3 dir = Vector3.ProjectOnPlane(velocity / speed, support);
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();
            EnemyManager hitManager = EnemyManager.Instance;
            LayerMask hitMask = hitManager != null ? hitManager.SurfaceMask : Physics.DefaultRaycastLayers;
            RememberHitSafePose(hitMask);
            // Repeated pushes into a wall must not keep cancelling the escape path.
            if (!IsJumping && !BodyOverlaps(root.position, hitMask)
                && ((_ground != null && !_ground.HasHitClearance(root.position, dir, hitMask))
                    || (_climb != null && !_climb.HasHitClearance(root.position, dir, hitMask))))
            {
                if (!_retreatActive && !_dropActive)
                {
                    ClearMeleeShove();
                    BeginRetreat(hitManager, dir);
                }
                return;
            }
            _meleeShoveDir = dir;
            if (_dropActive)
            {
                // Landing will start retreat; do not queue a second shove behind the drop.
                _dropFromShove = true;
                return;
            }
            if (_jump != null && _jump.IsJumping)
            {
                EnemyManager manager = EnemyManager.Instance;
                LayerMask mask = manager != null ? manager.SurfaceMask : Physics.DefaultRaycastLayers;
                _jump.NudgeTrajectory(dir, Mathf.Lerp(AirNudgeMin, AirNudgeMax, strength), mask);
                _retreatActive = false;
                _retreatRemaining = 0f;
                _airHitLock = true;
                return;
            }
            float distance = Mathf.Lerp(MeleePushDistanceMin, MeleePushDistanceMax, strength);
            BeginMeleeShove(dir, distance);
        }

        // Cone forward is +Z. Spray out of the entry surface instead of into the mesh.
        void SpawnHitBlood(Vector3 velocity, Vector3 hitPosition)
        {
            if (hitBloodPrefab == null) return;
            float speedSq = velocity.sqrMagnitude;
            if (speedSq < 1e-8f) return;
            Vector3 dir = velocity / Mathf.Sqrt(speedSq);
            Vector3 origin = hitPosition;
            if (origin.sqrMagnitude < 1e-8f && root != null)
            {
                Vector3 up = root.up.sqrMagnitude > 1e-6f ? root.up.normalized : Vector3.up;
                origin = root.position + up * (bodyHeight * 0.5f) - dir * bodyRadius;
            }
            if (origin.sqrMagnitude < 1e-8f && root == null) return;
            Vector3 spin = Mathf.Abs(dir.y) > 0.99f ? Vector3.forward : Vector3.up;
            Instantiate(hitBloodPrefab, origin, Quaternion.LookRotation(-dir, spin));
        }

        void BeginStun(Vector3 dir, bool slide)
        {
            if (!_stunned && head != null)
                _headShakeBase = head.localRotation;
            _stunned = true;
            _stunTimer = throwStunDuration;
            _stunAge = 0f;
            _shoveDir = dir;
            if (!slide)
            {
                _shoveTimeLeft = 0f;
                return;
            }

            _shoveDistance = throwShoveDistance;
            _shoveDuration = throwStunDuration < ThrowShoveDuration ? throwStunDuration : ThrowShoveDuration;
            if (_shoveDuration < 1e-4f) _shoveDuration = 1e-4f;
            _shoveTimeLeft = _shoveDuration;
        }

        void TickStunSlide(EnemyManager m, float dt)
        {
            if (_shoveTimeLeft <= 0f || root == null || dt <= 0f) return;
            float before = _shoveTimeLeft;
            _shoveTimeLeft -= dt;
            if (_shoveTimeLeft < 0f) _shoveTimeLeft = 0f;
            float span = _shoveDuration > 1e-5f ? _shoveDuration : 1e-5f;
            float eased = (EaseOut(1f - _shoveTimeLeft / span) - EaseOut(1f - before / span)) * _shoveDistance;
            if (eased <= 1e-5f) return;

            int mask = m != null ? m.SurfaceMask.value : Physics.DefaultRaycastLayers;
            mask &= ~_flightExclude.value;
            Vector3 next = SlideOnSurface(root.position, _shoveDir * eased, mask);
            float traveled = (next - root.position).magnitude;
            if (traveled < eased - ThrowSlideSkin) _shoveTimeLeft = 0f;
            SetRootPose(next, root.rotation);
        }

        Vector3 SlideOnSurface(Vector3 from, Vector3 delta, int mask)
        {
            float distance = delta.magnitude;
            if (distance < 1e-5f) return from;
            Vector3 direction = delta / distance;
            Vector3 up = root.up.sqrMagnitude > 1e-6f ? root.up.normalized : Vector3.up;
            float extent = BodyExtent();
            float radius = bodyRadius * extent;
            float height = bodyHeight * extent;
            if (height < radius * 2f) height = radius * 2f;
            Vector3 bottom = from + up * (radius + ThrowSlideSkin);
            float segment = height - radius * 2f;
            if (segment < 0.01f) segment = 0.01f;
            Vector3 top = bottom + up * segment;
            if (!Physics.CapsuleCast(bottom, top, radius, direction, out RaycastHit hit,
                distance + ThrowSlideSkin, mask, QueryTriggerInteraction.Ignore))
                return from + delta;

            float travel = hit.distance - ThrowSlideSkin;
            if (travel < 0f) travel = 0f;
            if (travel < distance) return from + direction * travel;
            return from + delta;
        }

        float BodyExtent()
        {
            if (root == null) return 1f;
            Vector3 scale = root.lossyScale;
            float extent = Mathf.Abs(scale.x);
            float scaleY = Mathf.Abs(scale.y);
            float scaleZ = Mathf.Abs(scale.z);
            if (scaleY > extent) extent = scaleY;
            if (scaleZ > extent) extent = scaleZ;
            return extent > 1e-4f ? extent : 1f;
        }

        static float EaseOut(float u)
        {
            float inv = 1f - Mathf.Clamp01(u);
            return 1f - inv * inv;
        }

        void ClearStun()
        {
            _stunned = false;
            _stunTimer = 0f;
            _stunAge = 0f;
            _shoveTimeLeft = 0f;
        }

        void BeginFlight(Vector3 launch)
        {
            _savedKinematic = _body.isKinematic;
            _savedGravity = _body.useGravity;
            _savedDetectCollisions = _body.detectCollisions;
            _savedDamping = _body.linearDamping;
            _savedAngularDamping = _body.angularDamping;
            _savedDetection = _body.collisionDetectionMode;
            _savedInterpolation = _body.interpolation;
            _savedCapsule = _capsule.enabled;
            _savedExclude = _capsule.excludeLayers;

            Vector3 up = root.up.sqrMagnitude > 1e-6f ? root.up.normalized : Vector3.up;
            float extent = Mathf.Abs(root.lossyScale.x);
            float scaleY = Mathf.Abs(root.lossyScale.y);
            float scaleZ = Mathf.Abs(root.lossyScale.z);
            if (scaleY > extent) extent = scaleY;
            if (scaleZ > extent) extent = scaleZ;
            _body.position = root.position + up * (_capsule.radius * extent + 0.03f);
            _capsule.excludeLayers = _flightExclude;
            _capsule.enabled = true;
            _body.detectCollisions = true;
            _body.isKinematic = false;
            _body.useGravity = true;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            _body.linearDamping = flightDamping;
            _body.angularDamping = flightAngularDamping;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.linearVelocity = launch;
            _body.angularVelocity = Tumble(launch);
            _flying = true;
            _grounded = false;
            _landNormal = Vector3.up;
            _flightStartTime = Time.time;
        }

        void TickKnock()
        {
            bool grounded = _grounded;
            Vector3 normal = _landNormal;
            _grounded = false;
            bool slow = _body.linearVelocity.sqrMagnitude <= recoverSpeed * recoverSpeed;
            if (grounded && slow)
            {
                Recover(normal);
                return;
            }

            float elapsed = Time.time - _flightStartTime;
            if (elapsed < maxFlightTime) return;
            if (FindGround(1.5f, out RaycastHit groundHit))
            {
                Recover(groundHit.normal);
                return;
            }
            if (elapsed >= maxFlightTime * 2f)
                Recover(Vector3.up);
        }

        bool FindGround(float distance, out RaycastHit hit)
        {
            hit = default;
            bool wasEnabled = _capsule != null && _capsule.enabled;
            if (wasEnabled) _capsule.enabled = false;
            LayerMask mask = EnemyManager.Instance != null ? EnemyManager.Instance.SurfaceMask : Physics.DefaultRaycastLayers;
            bool found = Physics.Raycast(_body.position + Vector3.up * 0.2f, Vector3.down, out hit, distance, mask, QueryTriggerInteraction.Ignore)
                && CrawlGraph.IsWalkableGround(hit.normal);
            if (wasEnabled) _capsule.enabled = true;
            return found;
        }

        void Recover(Vector3 upHint)
        {
            Vector3 pos = _body.position;
            Vector3 previousForward = root.forward;
            AbortFlight();

            Vector3 up = CrawlGraph.IsWalkableGround(upHint) ? upHint.normalized : Vector3.up;
            LayerMask mask = EnemyManager.Instance != null ? EnemyManager.Instance.SurfaceMask : Physics.DefaultRaycastLayers;
            if (Physics.Raycast(pos + up * 0.25f, -up, out RaycastHit hit, 3f, mask, QueryTriggerInteraction.Ignore)
                && CrawlGraph.IsWalkableGround(hit.normal))
            {
                pos = hit.point;
                up = hit.normal;
            }

            Vector3 forward = Vector3.ProjectOnPlane(previousForward, up);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(Vector3.forward, up);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            Quaternion rotation = Quaternion.LookRotation(forward.normalized, up);

            SetRootPose(pos, rotation);
            if (_loco != null) _loco.Interrupt();
        }

        void AbortFlight()
        {
            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.isKinematic = true;
                _body.useGravity = _savedGravity;
                _body.linearDamping = _savedDamping;
                _body.angularDamping = _savedAngularDamping;
                _body.collisionDetectionMode = _savedDetection;
                _body.interpolation = _savedInterpolation;
                _body.detectCollisions = _savedDetectCollisions;
            }
            if (_capsule != null)
            {
                _capsule.excludeLayers = _savedExclude;
                _capsule.enabled = _savedCapsule;
            }
            _flying = false;
            _grounded = false;
            EndLimbRagdoll();
        }

        void BeginLimbRagdoll(float strength)
        {
            if (strength < 0f) strength = 0f;
            if (strength > 2f) strength = 2f;
            _ragdollActive = true;
            _ragdollTime = 0f;
            for (int i = 0; i < _legs.Length; i++)
            {
                SpiderLeg limb = _legs[i];
                if (limb == null) continue;
                float sign = (i & 1) == 0 ? 1f : -1f;
                limb.BeginRagdoll(sign * strength);
            }
        }

        public void TickRagdoll(float dt)
        {
            if (!_ragdollActive || dt <= 0f) return;
            if (!_flying && !_remoteKnocked)
            {
                _ragdollTime += dt;
                if (_ragdollTime >= maxFlightTime)
                {
                    EndLimbRagdoll();
                    return;
                }
            }
            else _ragdollTime = 0f;

            for (int i = 0; i < _legs.Length; i++)
            {
                SpiderLeg limb = _legs[i];
                if (limb != null) limb.TickRagdoll(dt);
            }
        }

        void EndLimbRagdoll()
        {
            if (!_ragdollActive) return;
            _ragdollActive = false;
            for (int i = 0; i < _legs.Length; i++)
            {
                SpiderLeg limb = _legs[i];
                if (limb != null) limb.EndRagdoll();
            }
        }

        Vector3 Tumble(Vector3 launch)
        {
            Vector3 axis = Vector3.Cross(launch, root.up);
            if (axis.sqrMagnitude < 1e-4f) axis = Vector3.Cross(launch, Vector3.forward);
            if (axis.sqrMagnitude < 1e-4f) return Vector3.zero;
            return axis.normalized * tumbleSpeed;
        }

        void OnCollisionEnter(Collision collision) => NoteGround(collision);
        void OnCollisionStay(Collision collision) => NoteGround(collision);

        void NoteGround(Collision collision)
        {
            if (!_flying) return;
            int count = collision.contactCount;
            for (int i = 0; i < count; i++)
            {
                Vector3 normal = collision.GetContact(i).normal;
                if (!CrawlGraph.IsWalkableGround(normal)) continue;
                if (!_grounded || normal.y > _landNormal.y)
                    _landNormal = normal;
                _grounded = true;
            }
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

        internal void OnAuthorityChanged() => _hadAuthority = false;

        public void ApplyRemote(float dt)
        {
            if (IsDormant) return;
            _hadAuthority = false;
            if (_net != null) _net.Apply(root, dt);
        }

        static Transform FindChild(Transform t, string name)
        {
            for (int i = 0; i < t.childCount; i++)
            {
                Transform c = t.GetChild(i);
                if (c.name == name) return c;
                Transform nested = FindChild(c, name);
                if (nested != null) return nested;
            }
            return null;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (root == null) return;
            Color stateColor = CurrentState == State.Chase ? Color.red
                : CurrentState == State.Search ? Color.yellow
                : CurrentState == State.Retreat ? Color.cyan
                : Color.green;
            Gizmos.color = stateColor;
            Gizmos.DrawWireSphere(root.position, 0.25f);
            Gizmos.DrawRay(root.position, root.forward * 0.5f);
            DrawVisionGizmo(stateColor);
            ClimbLocomotion climb = _climb != null ? _climb : GetComponent<ClimbLocomotion>();
            if (climb != null) { climb.DrawPathGizmos(); return; }
            GroundLocomotion ground = _ground != null ? _ground : GetComponent<GroundLocomotion>();
            if (ground != null) ground.DrawPathGizmos();
        }

        void DrawVisionGizmo(Color stateColor)
        {
            Transform eyeT = eye != null ? eye : root;
            Vector3 origin = eyeT.position;
            Vector3 fwd = root.forward;
            Vector3 up = root.up;
            Vector3 right = root.right;
            float half = fovDegrees * 0.5f;

            Gizmos.color = new Color(0.35f, 0.85f, 1f, 0.9f);
            Gizmos.DrawWireSphere(origin, nearSenseRadius);

            Color cone = stateColor;
            cone.a = 0.85f;
            Gizmos.color = cone;
            UnityEditor.Handles.color = cone;

            Vector3 left = Quaternion.AngleAxis(-half, up) * fwd;
            Vector3 rightDir = Quaternion.AngleAxis(half, up) * fwd;
            Vector3 top = Quaternion.AngleAxis(-half, right) * fwd;
            Vector3 bottom = Quaternion.AngleAxis(half, right) * fwd;

            Gizmos.DrawRay(origin, fwd * visionRange);
            Gizmos.DrawRay(origin, left * visionRange);
            Gizmos.DrawRay(origin, rightDir * visionRange);
            Gizmos.DrawRay(origin, top * visionRange);
            Gizmos.DrawRay(origin, bottom * visionRange);

            UnityEditor.Handles.DrawWireArc(origin, up, left, fovDegrees, visionRange);
            UnityEditor.Handles.DrawWireArc(origin, right, top, fovDegrees, visionRange);

            float discDist = visionRange * Mathf.Cos(half * Mathf.Deg2Rad);
            float discRadius = visionRange * Mathf.Sin(half * Mathf.Deg2Rad);
            UnityEditor.Handles.DrawWireDisc(origin + fwd * discDist, fwd, discRadius);
        }
#endif
    }
}
