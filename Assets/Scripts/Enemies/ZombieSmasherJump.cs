using UnityEngine;

namespace Game.AI
{
    /// <summary>Authority-driven traversal layered over GroundLocomotion; ticked by Enemy.</summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Enemy), typeof(GroundLocomotion))]
    public class ZombieSmasherJump : MonoBehaviour
    {
        [Header("Jump")]
        [Tooltip("Maximum height of the root above takeoff, including the arc, in world units.")]
        [SerializeField, Min(0.1f)] float maxJumpHeight = 5f;
        [Tooltip("Seconds for the whole jump. Lower values make the jump faster.")]
        [SerializeField, Min(0.1f)] float jumpDuration = 1.0f;
        [Tooltip("Minimum seconds remaining in the jump arc after a hit.")]
        [SerializeField, Min(0f)] float minAirtimeAfterHit = 0.3f;
        [SerializeField, Min(0.1f)] float maxJumpDistance = 9f;
        [Tooltip("Maximum distance below takeoff for a planned jump. Accidental falls still land on the first surface below.")]
        [SerializeField, Min(0.1f)] float maxDropHeight = 10f;
        [Tooltip("Height tolerance when checking whether a route can be walked without jumping.")]
        [SerializeField, Min(0.1f)] float minimumJumpHeight = 0.5f;
        [SerializeField, Min(0.05f)] float arcClearance = 0.65f;
        [SerializeField, Min(0f)] float cooldown = 2f;
        [Tooltip("Distance below the player's last seen head position to look for their supporting surface.")]
        [SerializeField, Min(0.1f)] float targetSurfaceProbe = 3f;

        const float Skin = GroundLocomotion.CapsuleSkin;
        const int ArcSteps = 64;
        const float RetreatRetryDelay = 1.5f;
        const float JumpCheckInterval = 0.2f;
        const int JumpCheckPhases = 18;
        static int nextCheckPhase;
        int _checkPhase;
        bool _checkArmed;
        static readonly Unity.Profiling.ProfilerMarker WalkCheckMarker = new Unity.Profiling.ProfilerMarker("EnemyGround.WalkFeasibility");
        float _nextRetreatCheck;
        GroundLocomotion _ground;
        Vector3 _start, _end, _position;
        Vector3 _landingPoint, _landingNormal;
        Quaternion _rotation;
        float _apex, _peakTime, _elapsed, _duration, _nextCheck, _fallSpeed;
        bool _falling;
        bool _plannedJump;
        Vector3 _airNudge;
        float _airNudgeFrom;
        Vector3 _fallDrift;
        const float AirDriftSpeed = 6f;
        const float AirNudgeCap = 3f;
        float _airTime, _fallStartY;
        public bool IsFalling => IsJumping && _falling;
        public bool IsJumping { get; private set; }
        public event System.Action Landed;

        void Awake()
        {
            _ground = GetComponent<GroundLocomotion>();
            _checkPhase = nextCheckPhase;
            nextCheckPhase = (nextCheckPhase + 1) % JumpCheckPhases;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetCheckPhases() => nextCheckPhase = 0;

        public void ResetJump()
        {
            IsJumping = false;
            _falling = false;
            _elapsed = _fallSpeed = 0f;
            _nextCheck = 0f;
            _checkArmed = false;
            _nextRetreatCheck = 0f;
            _airNudge = Vector3.zero;
            _airNudgeFrom = 0f;
            _fallDrift = Vector3.zero;
        }

        /// <summary>Shift the remaining jump. A forward hit shortens it; a side hit slides the landing.</summary>
        public void NudgeTrajectory(Vector3 worldDirection, float distance, LayerMask mask)
        {
            if (!IsJumping || distance <= 0f) return;
            Vector3 flat = Vector3.ProjectOnPlane(worldDirection, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f) return;
            Vector3 delta = flat.normalized * distance;

            if (_falling)
            {
                _fallDrift += delta;
                if (_fallDrift.magnitude > AirNudgeCap)
                    _fallDrift = _fallDrift.normalized * AirNudgeCap;
                return;
            }

            float t = _duration > 1e-4f ? Mathf.Clamp01(_elapsed / _duration) : 0f;
            if (t < 1f && _duration - _elapsed < minAirtimeAfterHit)
            {
                // Keep the current arc position while slowing the remaining motion.
                _duration = minAirtimeAfterHit / (1f - t);
                _elapsed = t * _duration;
            }
            Vector3 applied = AirOffset(t);
            _start += applied;
            _end += applied;
            _airNudge = _airNudge - applied + delta;
            _airNudgeFrom = t;
            if (_airNudge.magnitude > AirNudgeCap)
                _airNudge = _airNudge.normalized * AirNudgeCap;
            ClampNudgeAhead();

            Vector3 landing = _end + _airNudge;
            Vector3 forward = Vector3.ProjectOnPlane(landing - _position, Vector3.up);
            if (forward.sqrMagnitude > 0.001f)
                _rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
            if (Physics.Raycast(landing + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f, mask,
                QueryTriggerInteraction.Ignore) && CrawlGraph.IsWalkableGround(hit.normal))
            {
                _landingPoint = hit.point;
                _landingNormal = hit.normal;
            }
        }

        void OnDisable() => ResetJump();

        /// <summary>Bounded lower-surface escape search; no graph/path requests.</summary>
        public bool TryStartRetreatJump(Enemy enemy, LayerMask mask, Vector3 away)
        {
            if (!enemy.HasAuthority || enemy.Root == null || IsJumping || enemy.CurrentState != Enemy.State.Retreat)
                return false;
            if (Time.time < _nextRetreatCheck || Time.time < _nextCheck) return false;
            _nextRetreatCheck = Time.time + RetreatRetryDelay;
            if (!_ground.HasGroundSupport(enemy.Root.position, mask)) return false;
            away = Vector3.ProjectOnPlane(away, Vector3.up).normalized;
            if (away.sqrMagnitude < .001f) return false;
            _start = enemy.Root.position;
            _airNudge = _fallDrift = Vector3.zero;
            _airNudgeFrom = 0f;
            // Six probes at most. Far candidates clear a bus even when takeoff
            // starts near its centre; diagonals remain in the retreat hemisphere.
            for (int candidate = 0; candidate < 6; candidate++)
            {
                float yaw = candidate / 2 == 0 ? 0f : candidate / 2 == 1 ? 45f : -45f;
                float distance = candidate % 2 == 0 ? Mathf.Min(3f, maxJumpDistance) : maxJumpDistance;
                Vector3 probe = _start + Quaternion.AngleAxis(yaw, Vector3.up) * away * distance;
                if (!Physics.Raycast(probe + Vector3.up * .1f, Vector3.down, out RaycastHit hit,
                    maxDropHeight + .1f, mask, QueryTriggerInteraction.Ignore)
                    || !CrawlGraph.IsWalkableGround(hit.normal)
                    || _start.y - hit.point.y < minimumJumpHeight || !Supported(hit.point, mask)) continue;
                _landingPoint = hit.point; _landingNormal = hit.normal;
                _end = _ground.RootFromCapsuleBottom(_ground.CapsuleBottomOnSurface(hit.point, hit.normal));
                float rise = _end.y - _start.y;
                if (rise >= maxJumpHeight || Vector3.ProjectOnPlane(_end - _start, Vector3.up).magnitude > maxJumpDistance)
                    continue;
                // Only two arcs per candidate, using the normal jump collision
                // sampling and clearance. Stop as soon as a safe arc is found.
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    _apex = attempt == 0 ? Mathf.Min(maxJumpHeight, Mathf.Max(0f, rise) + arcClearance) : maxJumpHeight;
                    _peakTime = Mathf.Sqrt(_apex) / (Mathf.Sqrt(_apex) + Mathf.Sqrt(_apex - rise));
                    Vector3 previous = _start;
                    bool clear = true;
                    for (int step = 1; step <= ArcSteps; step++)
                    {
                        Vector3 next = Arc(step / (float)ArcSteps);
                        if (!ClearSegment(previous, next, mask)) { clear = false; break; }
                        previous = next;
                    }
                    if (!clear) continue;
                    _position = _start;
                    _rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(_end - _start, Vector3.up).normalized);
                    _duration = Mathf.Max(.1f, jumpDuration);
                    _elapsed = _fallSpeed = _airTime = 0f;
                    _fallStartY = _start.y;
                    _falling = false; _plannedJump = true; IsJumping = true;
                    _ground.ClearPath();
                    GetComponent<ZombieSmasherSFX>()?.PlayJump();
                    return true;
                }
            }
            return false;
        }

        // Returning true reserves root movement for this tick, including the landing tick.
        public bool TickJump(Enemy enemy, EnemyManager manager, float dt)
        {
            if (!enemy.HasAuthority || enemy.Root == null || dt <= 0f) return false;
            if (_ground.ValidateSmasherPose(enemy, manager.SurfaceMask))
            {
                ResetJump();
                return true;
            }
            if (!IsJumping)
            {
                // Each jump or fall starts without knockback from the previous flight.
                _airNudge = Vector3.zero;
                _airNudgeFrom = 0f;
                _fallDrift = Vector3.zero;
                // Ground movement only snaps short steps. A roof edge (or removed
                // support) must enter free fall even while idle or on jump cooldown.
                if (!_ground.HasGroundSupport(enemy.Root.position, manager.SurfaceMask))
                {
                    Vector3 bottom = _ground.SupportedCapsuleBottom(enemy.Root.position, manager.SurfaceMask);
                    _ground.TryRepairGroundContact(ref bottom, manager.SurfaceMask);
                    _position = _ground.RootFromCapsuleBottom(bottom);
                    _rotation = enemy.Root.rotation;
                    _fallSpeed = 0f;
                    _falling = true;
                    _plannedJump = false;
                }
                else
                {
                    if (_ground.IsRecovering || enemy.CurrentState != Enemy.State.Chase)
                    {
                        _checkArmed = false;
                        return false;
                    }
                    if (!ReadyForJumpCheck(Time.time)) return false;
                    if (!TryPlan(enemy, manager.SurfaceMask)) return false;
                    _plannedJump = true;
                }
                _airTime = 0f;
                _fallStartY = enemy.Root.position.y;
                IsJumping = true;
                _ground.ClearPath();
                if (_plannedJump) GetComponent<ZombieSmasherSFX>()?.PlayJump();
            }

            _airTime += dt;
            if (!_plannedJump) _ground.RecordUnplannedFall(dt);
            if (_falling)
            {
                TickFallDrift(manager.SurfaceMask, dt);
                if (_ground.TickFall(ref _position, ref _fallSpeed, dt, manager.SurfaceMask, out Vector3 landNormal))
                    Finish(landNormal);
            }
            else
            {
                float next = Mathf.Min(1f, (_elapsed + dt) / _duration);
                float previous = _elapsed / _duration;
                float committed = previous;
                int steps = Mathf.Max(1, Mathf.CeilToInt((next - previous) * ArcSteps));
                for (int i = 1; i <= steps; i++)
                {
                    float sample = Mathf.Lerp(previous, next, (float)i / steps);
                    Vector3 point = Arc(sample);
                    if (!ClearSegment(_position, point, manager.SurfaceMask))
                    {
                        AbsorbNudgeIntoFall(committed);
                        _falling = true;
                        break;
                    }
                    _position = point;
                    committed = sample;
                }
                _elapsed = next * _duration;
                if (!_falling && next >= 1f)
                {
                    // A vehicle may have moved or been removed since takeoff.
                    if (Supported(_landingPoint, manager.SurfaceMask)) Finish(_landingNormal);
                    else _falling = true;
                }
            }
            enemy.SetRootPose(_position, _rotation);
            if (!IsJumping && !_plannedJump)
                _ground.CheckAfterUnplannedLanding(enemy, manager, _position);
            // Publish after the landing pose is committed so attached audio starts at impact.
            if (!IsJumping && (_plannedJump || (_airTime >= 0.1f && _fallStartY - _position.y >= 0.25f)))
            {
                // Resolve at impact so pooling/script reload cannot leave a stale
                // audio event subscription. Landed remains available to other listeners.
                GetComponent<ZombieSmasherSFX>()?.PlayLanding();
                Landed?.Invoke();
            }
            return true;
        }

        void Finish(Vector3 normal)
        {
            IsJumping = false;
            _nextCheck = Time.time + Mathf.Max(0f, cooldown);
            _checkArmed = false;
            // Preserve the airborne capsule's bottom while switching back to the
            // ground mover's surface-normal offset. Invalidate the old path too.
            Capsule(_position, out Vector3 bottom, out _, out _);
            _position = _ground.AdoptLanding(bottom, normal);
        }

        // Dephase newly activated chasers without consuming the A* work budget.
        // Only optional jump planning waits (at most one normal check interval);
        // support, falling, collision and regular walking still run every tick.
        bool ReadyForJumpCheck(float now)
        {
            if (now < _nextCheck && (!_checkArmed || _nextCheck - now > 0.00001f)) return false;
            if (!_checkArmed)
            {
                _nextCheck = NextPhasedCheck(now);
                _checkArmed = true;
                return false;
            }
            // Anchor to the phase, not the (possibly late) execution frame;
            // otherwise rounding/frame delays gradually bunch the cohort again.
            _nextCheck = NextPhasedCheck(now);
            return true;
        }

        float NextPhasedCheck(float now)
        {
            double interval = JumpCheckInterval;
            double phase = _checkPhase * interval / JumpCheckPhases;
            return (float)((System.Math.Floor((now + 0.00001d - phase) / interval) + 1d) * interval + phase);
        }

        bool TryPlan(Enemy enemy, LayerMask mask)
        {
            _start = enemy.Root.position;
            if (!_ground.HasGroundSupport(_start, mask)) return false;
            if (!Physics.Raycast(enemy.LastSeenPos + Vector3.up * 0.1f, Vector3.down,
                out RaycastHit target, Mathf.Max(0.1f, targetSurfaceProbe), mask, QueryTriggerInteraction.Ignore)
                || !CrawlGraph.IsWalkableGround(target.normal)) return false;

            float rise = target.point.y - _start.y;
            if (rise < -maxDropHeight || rise + 0.05f > maxJumpHeight) return false;
            if (CanWalkTo(target.point, mask)) return false;
            _ground.GetCapsuleSize(out float radius, out _);
            float takeoffY = _start.y;
            Vector3 towardStart = Vector3.ProjectOnPlane(_start - target.point, Vector3.up).normalized;
            if (towardStart.sqrMagnitude < 0.01f) towardStart = Vector3.forward;
            // Walking and airborne poses now share the same capsule offset.
            float maxApex = maxJumpHeight + takeoffY - _start.y;
            // Prefer a spot beside the player, then try the center for narrow roofs.
            for (int candidate = 0; candidate < 9; candidate++)
            {
                Vector3 offset = candidate == 8 ? Vector3.zero
                    : Quaternion.AngleAxis(candidate * 45f, Vector3.up) * towardStart * (radius * 2f + 0.35f);
                Vector3 probe = OnSupportPlane(target.point, target.normal, offset) + Vector3.up * 0.25f;
                if (!Physics.Raycast(probe, Vector3.down, out RaycastHit landing, 0.5f, mask,
                    QueryTriggerInteraction.Ignore) || !CrawlGraph.IsWalkableGround(landing.normal)) continue;
                _landingPoint = landing.point;
                _landingNormal = landing.normal;
                _end = _ground.RootFromCapsuleBottom(_ground.CapsuleBottomOnSurface(landing.point, landing.normal));
                rise = _end.y - _start.y;
                if (landing.point.y - takeoffY < -maxDropHeight || landing.point.y - takeoffY + 0.05f > maxJumpHeight
                    || rise + 0.05f > maxApex
                    || Vector3.ProjectOnPlane(_end - _start, Vector3.up).magnitude > maxJumpDistance
                    || !Supported(_landingPoint, mask)) continue;

                bool clear = true;
                float lowApex = Mathf.Min(maxApex, Mathf.Max(0f, rise) + Mathf.Max(0.05f, arcClearance));
                // Descending jumps may need more lift to leave a broad roof before
                // descending. Test progressively higher arcs up to the configured cap.
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    _apex = Mathf.Lerp(lowApex, maxApex, attempt / 3f);
                    _peakTime = Mathf.Sqrt(_apex) / (Mathf.Sqrt(_apex) + Mathf.Sqrt(_apex - rise));
                    clear = true;
                    Vector3 previous = _start;
                    for (int step = 1; step <= ArcSteps; step++)
                    {
                        Vector3 point = Arc((float)step / ArcSteps);
                        if (!ClearSegment(previous, point, mask)) { clear = false; break; }
                        previous = point;
                    }
                    if (clear) break;
                }
                if (!clear) continue;
                _position = _start;
                Vector3 forward = Vector3.ProjectOnPlane(_end - _start, Vector3.up);
                _rotation = forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : enemy.Root.rotation;
                _duration = Mathf.Max(0.1f, jumpDuration);
                _elapsed = _fallSpeed = 0f;
                _falling = false;
                return true;
            }
            return false;
        }

        bool CanWalkTo(Vector3 target, LayerMask mask)
        {
            using var marker = WalkCheckMarker.Auto();
            float distance = Vector3.Distance(_start, target);
            if (distance > maxJumpDistance + 2f) return false;
            _ground.GetCapsuleSize(out float radius, out float height);
            int steps = Mathf.Max(1, Mathf.CeilToInt(distance / 0.25f));
            float tolerance = Mathf.Max(0.1f, minimumJumpHeight);
            Vector3 previous = _start;
            bool usedStairClearance = false;
            bool previousRaisedClear = false;
            int treadChanges = 0;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 point = Vector3.Lerp(_start, target, (float)i / steps);
                // Follow the sampled floor height through each tread. A line to
                // the top landing can be above the first riser by several steps.
                point.y = previous.y;
                if (!Physics.Raycast(point + Vector3.up * tolerance, Vector3.down, out RaycastHit hit,
                    tolerance * 2f, mask, QueryTriggerInteraction.Ignore)
                    || !CrawlGraph.IsWalkableGround(hit.normal)
                    || Mathf.Abs(hit.point.y - previous.y) > tolerance) return false;
                if (!ClearWalkingSegment(previous, hit.point, radius, height, mask))
                {
                    if (!_ground.ClearStairSegment(previous, hit.point, mask, radius, height, previousRaisedClear)) return false;
                    usedStairClearance = true;
                    previousRaisedClear = true;
                }
                else previousRaisedClear = false;
                if (Mathf.Abs(hit.point.y - previous.y) > 0.02f) treadChanges++;
                previous = hit.point;
            }
            // Raised clearance alone must not hide an isolated barrier between
            // level floor samples. Require actual sampled treads/ledge support.
            return Mathf.Abs(previous.y - target.y) < 0.1f && (!usedStairClearance || treadChanges > 0);
        }

        // Dimensions are constant for one synchronous feasibility query. Avoid
        // repeated GetComponent/transform reads for every 0.25m sample.
        static bool ClearWalkingSegment(Vector3 from, Vector3 to, float radius, float height, LayerMask mask)
        {
            Vector3 bottom = to + Vector3.up * (radius + Skin);
            Vector3 top = bottom + Vector3.up * (height - radius * 2f);
            if (Physics.CheckCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore)) return false;
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            Vector3 start = from + Vector3.up * (radius + Skin);
            return distance < 1e-5f || !Physics.CapsuleCast(start, start + Vector3.up * (height - radius * 2f), radius,
                delta / distance, distance, mask, QueryTriggerInteraction.Ignore);
        }

        Vector3 Arc(float t)
        {
            // Ease horizontal motion so the body gains clearance before crossing the ledge.
            Vector3 point = Vector3.Lerp(_start, _end, Mathf.SmoothStep(0f, 1f, t));
            float relative = (t - _peakTime) / _peakTime;
            point.y = _start.y + _apex * (1f - relative * relative);
            point += AirOffset(t);
            return point;
        }

        Vector3 AirOffset(float t)
        {
            if (_airNudge.sqrMagnitude < 1e-8f) return Vector3.zero;
            float span = 1f - _airNudgeFrom;
            if (span < 1e-6f) span = 1e-6f;
            float u = (t - _airNudgeFrom) / span;
            if (u < 0f) u = 0f;
            else if (u > 1f) u = 1f;
            return _airNudge * Mathf.SmoothStep(0f, 1f, u);
        }

        void ClampNudgeAhead()
        {
            Vector3 travel = Vector3.ProjectOnPlane(_end - _start, Vector3.up);
            if (travel.sqrMagnitude < 0.01f) return;
            Vector3 remain = Vector3.ProjectOnPlane(_end + _airNudge - _position, Vector3.up);
            float ahead = Vector3.Dot(remain, travel.normalized);
            if (ahead < 0.15f)
                _airNudge += travel.normalized * (0.15f - ahead);
        }

        void AbsorbNudgeIntoFall(float t)
        {
            _fallDrift += _airNudge - AirOffset(t);
            _airNudge = Vector3.zero;
            if (_fallDrift.magnitude > AirNudgeCap)
                _fallDrift = _fallDrift.normalized * AirNudgeCap;
        }

        void TickFallDrift(LayerMask mask, float dt)
        {
            float remain = _fallDrift.magnitude;
            if (remain < 1e-4f || dt <= 0f)
            {
                _fallDrift = Vector3.zero;
                return;
            }
            float step = AirDriftSpeed * dt;
            if (step > remain) step = remain;
            Vector3 dir = _fallDrift / remain;
            Vector3 delta = dir * step;
            if (Sweep(_position, delta, mask, out RaycastHit hit))
            {
                float travel = hit.distance - Skin;
                if (travel < 0f) travel = 0f;
                if (travel > step) travel = step;
                _position += dir * travel;
                _fallDrift = Vector3.zero;
                return;
            }
            _position += delta;
            _fallDrift -= delta;
        }

        bool Supported(Vector3 point, LayerMask mask)
        {
            _ground.GetCapsuleSize(out float radius, out _);
            if (!Physics.Raycast(point + Vector3.up * 0.2f, Vector3.down, out RaycastHit center,
                0.4f, mask, QueryTriggerInteraction.Ignore) || !CrawlGraph.IsWalkableGround(center.normal)) return false;
            for (int i = 0; i < 5; i++)
            {
                Vector3 offset = i == 0 ? Vector3.zero
                    : Quaternion.AngleAxis((i - 1) * 90f, Vector3.up) * Vector3.forward * radius;
                Vector3 sample = OnSupportPlane(center.point, center.normal, offset);
                if (!Physics.Raycast(sample + Vector3.up * 0.2f, Vector3.down,
                    out RaycastHit hit, 0.4f, mask, QueryTriggerInteraction.Ignore)
                    || !CrawlGraph.IsWalkableGround(hit.normal)) return false;
            }
            return true;
        }

        static Vector3 OnSupportPlane(Vector3 point, Vector3 normal, Vector3 horizontalOffset)
        {
            horizontalOffset.y = -(normal.x * horizontalOffset.x + normal.z * horizontalOffset.z)
                / Mathf.Max(0.001f, normal.y);
            return point + horizontalOffset;
        }

        bool ClearSegment(Vector3 from, Vector3 to, LayerMask mask)
        {
            Capsule(to, out Vector3 bottom, out Vector3 top, out float radius);
            return !Physics.CheckCapsule(bottom, top, radius, mask, QueryTriggerInteraction.Ignore)
                && !Sweep(from, to - from, mask, out _);
        }

        bool Sweep(Vector3 from, Vector3 delta, LayerMask mask, out RaycastHit hit)
        {
            hit = default;
            if (delta.sqrMagnitude < 1e-10f) return false;
            Capsule(from, out Vector3 bottom, out Vector3 top, out float radius);
            return Physics.CapsuleCast(bottom, top, radius, delta.normalized, out hit,
                delta.magnitude, mask, QueryTriggerInteraction.Ignore);
        }

        bool TryFallGround(Vector3 from, float distance, LayerMask mask, out float travel, out Vector3 normal)
        {
            travel = 0f; normal = Vector3.up;
            Capsule(from, out Vector3 bottom, out _, out float radius);
            if (!Physics.Raycast(bottom + Vector3.up * Skin, Vector3.down, out RaycastHit hit,
                distance + radius + Skin * 2f, mask, QueryTriggerInteraction.Ignore)
                || !CrawlGraph.IsWalkableGround(hit.normal)) return false;
            travel = (Vector3.Dot(bottom - hit.point, hit.normal) - radius - Skin) / hit.normal.y;
            if (travel < -0.15f || travel > distance) return false;
            Vector3 point = from + Vector3.down * travel;
            Capsule(point, out Vector3 lower, out Vector3 upper, out _);
            if (Physics.CheckCapsule(lower, upper, radius, mask, QueryTriggerInteraction.Ignore)) return false;
            normal = hit.normal;
            return true;
        }

        void Capsule(Vector3 point, out Vector3 bottom, out Vector3 top, out float radius)
        {
            _ground.GetCapsuleSize(out radius, out float height);
            bottom = _ground.SupportedCapsuleBottom(point, 0);
            top = bottom + Vector3.up * (height - radius * 2f);
        }
    }
}
