using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Upward-facing surface crawl-graph mover. Keeps the root upright and snaps downward; gait poses the body.
    /// </summary>
    public class GroundLocomotion : MonoBehaviour, IEnemyLocomotion
    {
        [Header("Movement")]
        [SerializeField] float walkSpeed = 0.7f;
        [SerializeField] float chaseSpeed = 1.8f;
        [SerializeField] float turnSpeed = 540f;          // deg/s
        [SerializeField] float arriveRadius = 0.3f;
        [SerializeField] float snapProbe = 0.5f;
        [SerializeField] float groundSnapDistance = 1.2f;
        [Tooltip("Maximum stair riser/ledge height in world units. Zero disables stepping.")]
        [SerializeField, Min(0f)] float maxStepHeight = 0.3f;
        static readonly Unity.Profiling.ProfilerMarker StepMarker = new Unity.Profiling.ProfilerMarker("EnemyGround.StepUp");
        static readonly Unity.Profiling.ProfilerMarker StairClearanceMarker = new Unity.Profiling.ProfilerMarker("EnemyGround.StairClearance");
        public int StepAttempts { get; private set; }
        public int StepsTaken { get; private set; }

        [Header("Wall clearance (root-local units)")]
        [SerializeField, Min(0.01f)] float collisionRadius = 0.35f;
        [SerializeField, Min(0.02f)] float collisionHeight = 0.8f;
        public const float CapsuleSkin = 0.02f;
        public const float FallGravity = 25f;
        const float CollisionSkin = CapsuleSkin;
        readonly Collider[] _overlaps = new Collider[32];
        CapsuleCollider _queryCapsule;
        bool _hasSafePose, _hasPreviousBottom;
        bool _hasMoveCapsule;
        float _moveRadius, _moveHeight;
        Vector3 _safePosition, _previousBottom;
        Quaternion _safeRotation;

        [Header("Wander")]
        [SerializeField] float wanderRadius = 18f;
        [SerializeField] Vector2 wanderPause = new Vector2(1f, 4f);

        [Header("Stuck recovery")]
        [SerializeField, Min(0.5f)] float stuckDelay = 2f;
        [SerializeField, Min(0.1f)] float escapeDistance = 0.8f;
        [SerializeField, Min(1f)] float blockedRouteDuration = 6f;
        [SerializeField, Min(5f)] float stalledRespawnDelay = 20f;
        readonly System.Collections.Generic.HashSet<int> _blockedNodes = new System.Collections.Generic.HashSet<int>();
        readonly System.Collections.Generic.Dictionary<int, float> _blockedExpiry = new System.Collections.Generic.Dictionary<int, float>();
        readonly System.Collections.Generic.List<int> _expiredNodes = new System.Collections.Generic.List<int>();
        float _blockedUntil;
        bool _stalled;
        float _stallTime, _spawnRetry;
        Vector3 _stallOrigin;
        readonly int[] _wanderNodes = new int[256], _wanderParents = new int[256];
        readonly System.Collections.Generic.HashSet<int> _wanderVisited = new System.Collections.Generic.HashSet<int>();
        bool _wanderRequest;
        Vector3 _escapeTarget;
        float _escapeTimer;
        float _escapeSpeed;
        int _escapeAttempt;

        readonly int[] _path = new int[128];
        readonly int[] _pendingPath = new int[128];
        int _pathVersion, _pathQueuedFrame;
        int _pathCount, _pathIndex;
        bool _pathComplete, _wantsPath;
        int _goalNode = -1;
        int _currentNode = -1;

        float _pauseTimer;
        Vector3 _stuckPos;
        float _stuckTimer;
        Vector3 _lastProgressPos;
        float _progressTravel;
        Vector3 _supportNormal = Vector3.up, _supportPosition;
        bool _hasSupport;

        public bool PathDone => _pathIndex >= _pathCount;
        public bool WantsPath => _wantsPath;
        public bool IsRecovering => _escapeTimer > 0f;
        bool AvoidingRoute { get { ExpireBlockedNodes(); return _blockedNodes.Count > 0; } }

        void ExpireBlockedNodes()
        {
            _expiredNodes.Clear();
            foreach (var entry in _blockedExpiry)
                if (Time.time >= entry.Value) _expiredNodes.Add(entry.Key);
            foreach (int node in _expiredNodes) { _blockedExpiry.Remove(node); _blockedNodes.Remove(node); }
        }

        void BlockNode(int node)
        {
            ExpireBlockedNodes();
            // Repeated failures must not renew a restriction that is still active.
            if (node < 0 || !_blockedNodes.Add(node)) return;
            _pathVersion++; // Invalidate an in-flight snapshot when a new obstruction is discovered.
            _blockedExpiry[node] = Time.time + blockedRouteDuration;
            _blockedUntil = _blockedExpiry[node];
        }

        public void Reset()
        {
            StepAttempts = StepsTaken = 0;
            _pathVersion++;
            _pathCount = _pathIndex = 0;
            _wantsPath = false;
            _goalNode = _currentNode = -1;
            _pauseTimer = Random.Range(wanderPause.x, wanderPause.y);
            _stuckTimer = 0f;
            _hasSupport = false;
            _hasSafePose = _hasPreviousBottom = false;
            _stuckPos = GetRoot().position;
            _lastProgressPos = _stuckPos;
            _progressTravel = 0f;
            _blockedNodes.Clear();
            _blockedExpiry.Clear();
            _stalled = false;
            _stallTime = _spawnRetry = 0f;
            _blockedUntil = 0f;
            _escapeTimer = 0f;
            _escapeAttempt = 0;
            _wanderRequest = false;
        }

        public void ClearPath()
        {
            _pathVersion++;
            _goalNode = -1;
            _pathCount = _pathIndex = 0;
            _wantsPath = false;
            _wanderRequest = false;
        }

        public void Interrupt()
        {
            ClearPath();
            _pauseTimer = 0f;
            _stuckTimer = 0f;
            _escapeTimer = 0f;
            _escapeAttempt = 0;
            _hasSupport = false;
            Transform root = GetRoot();
            if (root != null) _stuckPos = root.position;
        }

        /// <summary>Slide along the supporting ground without turning or following a path.</summary>
        public Vector3 Displace(Vector3 position, Vector3 delta, LayerMask mask)
        {
            return MoveWithWalls(position, delta, mask);
        }

        public bool HasHitClearance(Vector3 position, Vector3 direction, LayerMask mask)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(direction, GroundNormal(position, mask)).normalized;
            return !CastWall(position, tangent * 0.1f, mask, out RaycastHit hit)
                || CrawlGraph.IsWalkableGround(hit.normal);
        }

        // Used only by Smasher's existing, throttled walking-feasibility check.
        // Adjacent sampled treads can intersect the capsule at their raw floor
        // points. Check the tread-straddling body envelope above both floors.
        internal bool ClearStairSegment(Vector3 from, Vector3 to, LayerMask mask,
            float radius, float height, bool fromAlreadyClear)
        {
            if (maxStepHeight <= 0f || Mathf.Abs(to.y - from.y) > maxStepHeight + 0.001f
                || Vector3.ProjectOnPlane(to - from, Vector3.up).sqrMagnitude > 0.26f * 0.26f) return false;
            using var marker = StairClearanceMarker.Auto();
            float straddle = maxStepHeight + radius;
            Vector3 offset = Vector3.up * (radius + CollisionSkin + straddle);
            Vector3 a = from + offset;
            Vector3 b = to + offset;
            Vector3 axis = Vector3.up * (height - radius * 2f);
            Vector3 highA = new Vector3(a.x, Mathf.Max(a.y, b.y), a.z);
            Vector3 highB = new Vector3(b.x, highA.y, b.z);
            // Only reuse the immediately preceding endpoint in this synchronous
            // walking query. Movement, grounding and later queries stay fresh.
            return (fromAlreadyClear || !Physics.CheckCapsule(a, a + axis, radius, mask, QueryTriggerInteraction.Ignore))
                && !Physics.CheckCapsule(b, b + axis, radius, mask, QueryTriggerInteraction.Ignore)
                && ClearStairSweep(a, axis, radius, highA - a, mask)
                && ClearStairSweep(highA, axis, radius, highB - highA, mask)
                && ClearStairSweep(highB, axis, radius, b - highB, mask);
        }

        static bool ClearStairSweep(Vector3 bottom, Vector3 axis, float radius, Vector3 delta, LayerMask mask)
        {
            float distance = delta.magnitude;
            return distance < 1e-5f || !Physics.CapsuleCast(bottom, bottom + axis, radius,
                delta / distance, distance + CollisionSkin, mask, QueryTriggerInteraction.Ignore);
        }

        public void BeginWanderPause()
        {
            _pauseTimer = Random.Range(wanderPause.x, wanderPause.y);
        }

        public bool TickWanderPause(float dt)
        {
            _pauseTimer -= dt;
            return _pauseTimer <= 0f;
        }

        public void RequestWander(EnemyManager m)
        {
            if (_escapeTimer > 0f) return;
            _pathVersion++;
            _wanderRequest = true;
            _wantsPath = true;
        }
        public void RequestPathToWorld(EnemyManager m, Vector3 worldPoint)
        {
            if (_escapeTimer > 0f) return;
            int goal = m.ResolveGroundNode(worldPoint, true);
            if (goal >= 0 && (goal != _goalNode || PathDone)) RequestPath(goal);
        }

        public void ForcePathToWorld(EnemyManager m, Vector3 worldPoint)
        {
            if (_escapeTimer > 0f) return;
            int goal = m.ResolveGroundNode(worldPoint, true);
            if (goal >= 0) RequestPath(goal);
        }

        public void RequestRetreat(EnemyManager m, Vector3 awayDirection, float travelDistance)
        {
            if (_escapeTimer > 0f || m == null || m.Graph == null) return;
            Transform root = GetRoot();
            if (root == null) return;
            int start = CurrentNode(m, root.position);
            if (start < 0) return;
            int goal = m.Graph.FindRetreatNode(start, root.position, awayDirection, travelDistance, true, _wanderVisited);
            for (int i = 0; goal < 0 && i < 3; i++)
            {
                float angle = i == 0 ? 90f : i == 1 ? -90f : 180f;
                Vector3 alternate = Quaternion.AngleAxis(angle, Vector3.up) * awayDirection;
                goal = m.Graph.FindRetreatNode(start, root.position, alternate, travelDistance, true, _wanderVisited);
            }
            if (goal >= 0) RequestPath(goal);
        }

        public void RunPathRequest(EnemyManager m)
        {
            Transform root = GetRoot();
            if (root == null) { ClearPath(); return; }

            if (_wanderRequest)
            {
                _wanderRequest = false;
                _wantsPath = false;
                BuildWanderPath(m, root.position, CurrentNode(m, root.position));
                return;
            }

            // A replacement never writes into the route currently used by TickMove.
            bool continuing = m.HasPathRequest(this, _pathVersion);
            int start = continuing ? -1 : CurrentNode(m, root.position);
            if ((!continuing && start < 0) || _goalNode < 0) { ClearPath(); return; }
            var status = m.StepPathRequest(this, _pathVersion, start, _goalNode, _pendingPath,
                out int count, out bool complete, groundOnly: true, blocked: AvoidingRoute ? _blockedNodes : null, queuedFrame: _pathQueuedFrame);
            if (status == EnemyManager.PathStatus.Pending) return;
            if (status == EnemyManager.PathStatus.Found)
            {
                int current = continuing ? CurrentNode(m, root.position) : start;
                int join = EnemyManager.PathJoinIndex(m.Graph, current, _pendingPath, count);
                if (join < 0)
                {
                    // Movement/knockback carried us away. Queue again without snapping back.
                    _pathVersion++;
                    return;
                }
                System.Array.Copy(_pendingPath, _path, count);
                _pathCount = count;
                _pathIndex = join;
                _pathComplete = complete;
            }
            else
            {
                _pathCount = _pathIndex = 0;
                _pathComplete = true;
                Enemy enemy = GetComponent<Enemy>();
                if (enemy != null && enemy.CurrentState == Enemy.State.Wander)
                    _pauseTimer = 0.5f;
            }
            _wantsPath = false;
        }

        public void TickMove(Enemy enemy, EnemyManager m, float dt)
        {
            if (enemy == null || enemy.Root == null || m.Graph == null || dt <= 0f) return;
            if (TickStallRecovery(enemy, m, dt)) return;

            if (_escapeTimer > 0f)
            {
                TickEscape(enemy, m, dt);
                return;
            }

            if (!_wantsPath && _goalNode >= 0 && _pathCount > 0 && !_pathComplete && _pathIndex >= _pathCount - 2)
                RequestPath(_goalNode);

            CrawlGraph g = m.Graph;
            Transform root = enemy.Root;
            Vector3 pos = root.position;
            Vector3 up = Vector3.up;
            float speed = enemy.CurrentState == Enemy.State.Retreat ? enemy.RetreatSpeed
                : enemy.CurrentState == Enemy.State.Wander ? walkSpeed : chaseSpeed;
            LayerMask mask = m.SurfaceMask;

            Vector3 toSeen = enemy.LastSeenPos - pos;
            Vector3 flatSeen = Vector3.ProjectOnPlane(toSeen, up);
            bool direct = enemy.CurrentState == Enemy.State.Chase && enemy.Seen
                && GoalOnWalkingLevel(g, pos)
                && (!AvoidingRoute || flatSeen.sqrMagnitude < enemy.StopDistance * enemy.StopDistance)
                && flatSeen.sqrMagnitude < enemy.DirectChaseDistance * enemy.DirectChaseDistance
                && !CastWall(pos, flatSeen, mask, out _);

            Vector3 target;
            if (direct)
            {
                if (flatSeen.sqrMagnitude < enemy.StopDistance * enemy.StopDistance)
                {
                    pos = MoveWithWalls(pos, Vector3.zero, mask);
                    enemy.SetRootPose(pos, TurnTowards(root, flatSeen, up, dt));
                    _stuckPos = pos;
                    _stuckTimer = 0f; // Intentionally stopped within attack range.
                    _lastProgressPos = pos;
                    _progressTravel = 0f;
                    return;
                }
                target = pos + flatSeen.normalized * (speed * dt * 2f);
            }
            else if (_pathIndex < _pathCount)
            {
                int node = _path[_pathIndex];
                target = g.GetPosition(node);
                if ((target - pos).sqrMagnitude < arriveRadius * arriveRadius)
                {
                    _currentNode = node;
                    _pathIndex++;
                    if (_pathIndex >= _pathCount) { UpdateStuck(enemy, m, dt, pos); return; }
                    TrySkipAhead(g, pos, mask);
                    node = _path[_pathIndex];
                    target = g.GetPosition(node);
                }
            }
            else
            {
                UpdateStuck(enemy, m, dt, pos);
                return;
            }

            Vector3 delta = target - pos;
            float dist = delta.magnitude;
            if (dist < 1e-5f) { UpdateStuck(enemy, m, dt, pos); return; }
            Vector3 moveDir = delta / dist;
            pos = MoveWithWalls(pos, moveDir * Mathf.Min(speed * dt, dist), mask);

            ApplyMovementPose(enemy, pos, dt, turnSpeed);

            UpdateStuck(enemy, m, dt, pos);
        }

        // Root poses are written directly to a kinematic rigidbody, so collision
        // response must happen here, before committing the position.
        Vector3 MoveWithWalls(Vector3 pos, Vector3 remaining, LayerMask mask)
        {
            // Shape/scale cannot change during these synchronous physics queries.
            // Snapshot dimensions for this call only, including nested contact
            // repair. All sweeps/support checks still query the current world.
            GetCapsuleSize(out float radius, out float height);
            bool previousSnapshot = _hasMoveCapsule;
            float previousRadius = _moveRadius, previousHeight = _moveHeight;
            _hasMoveCapsule = true; _moveRadius = radius; _moveHeight = height;
            try { return MoveWithWallsCore(pos, remaining, mask); }
            finally
            {
                _hasMoveCapsule = previousSnapshot;
                _moveRadius = previousRadius; _moveHeight = previousHeight;
            }
        }

        Vector3 MoveWithWallsCore(Vector3 pos, Vector3 remaining, LayerMask mask)
        {
            GetCapsuleSize(out float radius, out _);
            Vector3 support = GroundNormal(pos, mask);
            // Keep the capsule outside the supporting plane even on steep slopes.
            // The root follows the surface, easing its offset at normal transitions.
            Vector3 bottom = SupportedCapsuleBottom(pos, mask);
            Vector3 initialBottom = bottom;
            TryRepairGroundContact(ref bottom, mask);
            remaining = Vector3.ProjectOnPlane(remaining, support).normalized * remaining.magnitude;
            bool attemptedStep = false;
            bool steppedUp = false;
            for (int i = 0; i < 8 && remaining.sqrMagnitude > 1e-8f; i++)
            {
                if (!CastFromBottom(bottom, remaining, mask, out RaycastHit hit))
                {
                    bottom += remaining;
                    break;
                }

                float distance = remaining.magnitude;
                Vector3 direction = remaining / distance;
                float travel = Mathf.Clamp(hit.distance - CollisionSkin, 0f, distance);
                bottom += direction * travel;
                remaining -= direction * travel;
                // Reuse the movement hit. Flat-ground travel adds no queries, and
                // corners cannot trigger more than one step attempt per tick.
                if (!attemptedStep && support.y >= 0.7f && hit.normal.y < 0.7f
                    && maxStepHeight > 0f)
                {
                    attemptedStep = true;
                    if (TryStepUp(bottom, remaining, hit, mask, out Vector3 stepped, out Vector3 stepNormal))
                    {
                        bottom = stepped;
                        support = stepNormal;
                        steppedUp = true;
                        break;
                    }
                }
                Vector3 slide = Vector3.ProjectOnPlane(remaining, hit.normal);
                if (CrawlGraph.IsWalkableGround(hit.normal))
                {
                    support = hit.normal;
                    // Do not amplify a tiny projection at a seam into a full-speed
                    // correction, or keep solving the same zero-distance contact.
                    if (travel <= 0.0001f && (slide - remaining).sqrMagnitude < 1e-8f) break;
                    remaining = slide;
                }
                else
                {
                    // Treat slopes above the limit as walls. Projecting onto their
                    // tilted normal would otherwise still introduce uphill motion.
                    Vector3 wallNormal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    remaining = wallNormal.sqrMagnitude > 1e-6f
                        ? Vector3.ProjectOnPlane(remaining, wallNormal)
                        : slide;
                    remaining.y = Mathf.Min(remaining.y, 0f);
                }
            }

            // Prefer the current supporting plane. At a steep ramp's foot a down
            // cast can hit the floor first and incorrectly detach us from the ramp.
            _hasSupport = steppedUp;
            if (!steppedUp && CastFromBottom(bottom, -support * groundSnapDistance, mask, out RaycastHit ground)
                && CrawlGraph.IsWalkableGround(ground.normal))
            {
                float normalSkin = CollisionSkin / Mathf.Max(0.00001f, Vector3.Dot(support, ground.normal));
                bottom -= support * Mathf.Max(0f, ground.distance - normalSkin);
                support = ground.normal;
                _hasSupport = true;
            }
            else if (!steppedUp && CastFromBottom(bottom, Vector3.down * groundSnapDistance, mask, out ground)
                && CrawlGraph.IsWalkableGround(ground.normal))
            {
                float verticalSkin = CollisionSkin / ground.normal.y;
                bottom += Vector3.down * Mathf.Max(0f, ground.distance - verticalSkin);
                support = ground.normal;
                _hasSupport = true;
            }
            // A cast can miss an already overlapping capsule. Never commit a
            // newly embedded pose, or cross a thin, one-sided terrain triangle.
            TryRepairGroundContact(ref bottom, mask);
            Vector3 travelled = bottom - initialBottom;
            if ((CapsuleOverlaps(bottom, mask) && !CapsuleOverlaps(initialBottom, mask))
                || (travelled.sqrMagnitude > 1e-8f && Physics.Raycast(initialBottom,
                    travelled.normalized, travelled.magnitude, mask, QueryTriggerInteraction.Ignore)))
                return pos;
            _supportNormal = support;
            // Root-to-capsule offset never changes with the contact normal.
            _supportPosition = RootFromCapsuleBottom(bottom);
            return _supportPosition;
        }

        // A local collision maneuver, not a path search. Keep horizontal travel
        // within this tick's movement allowance instead of teleporting over a lip.
        bool TryStepUp(Vector3 bottom, Vector3 remaining, RaycastHit obstacle, LayerMask mask,
            out Vector3 stepped, out Vector3 normal)
        {
            stepped = bottom;
            normal = Vector3.up;
            Vector3 forward = Vector3.ProjectOnPlane(remaining, Vector3.up);
            if (forward.sqrMagnitude < 1e-8f) return false;
            GetCapsuleSize(out float radius, out _);
            float foot = bottom.y - radius - CollisionSkin;
            if (obstacle.point.y > foot + maxStepHeight + CollisionSkin) return false;
            using var marker = StepMarker.Auto();
            StepAttempts++;
            // Do not step while airborne, or turn a steep slope into a staircase.
            if (!CastFromBottom(bottom, Vector3.down * 0.08f, mask, out RaycastHit support)
                || !CrawlGraph.IsWalkableGround(support.normal)) return false;
            Vector3 lift = Vector3.up * (maxStepHeight + CollisionSkin);
            if (CastFromBottom(bottom, lift, mask, out _)) return false;
            Vector3 raised = bottom + lift;
            if (CastFromBottom(raised, forward, mask, out _)) return false;
            Vector3 across = raised + forward;
            if (!CastFromBottom(across, -lift, mask, out RaycastHit landing)
                || !CrawlGraph.IsWalkableGround(landing.normal)) return false;
            Vector3 candidate = across + Vector3.down * Mathf.Max(0f,
                landing.distance - CollisionSkin / landing.normal.y);
            float rise = candidate.y - bottom.y;
            if (rise <= 0.0001f || rise > maxStepHeight || CapsuleOverlaps(candidate, mask)) return false;
            stepped = candidate;
            normal = landing.normal;
            StepsTaken++;
            return true;
        }

        /// <summary>Bottom sphere center used by supported ground movement.</summary>
        public Vector3 SupportedCapsuleBottom(Vector3 position, LayerMask mask)
        {
            GetCapsuleSize(out float radius, out _);
            return position + Vector3.up * (radius + CapsuleSkin);
        }

        public Vector3 RootFromCapsuleBottom(Vector3 bottom)
        {
            GetCapsuleSize(out float radius, out _);
            return bottom - Vector3.up * (radius + CapsuleSkin);
        }

        public Vector3 CapsuleBottomOnSurface(Vector3 position, Vector3 normal)
        {
            GetCapsuleSize(out float radius, out _);
            return position + normal * (radius + CollisionSkin);
        }

        bool CapsuleOverlaps(Vector3 bottom, LayerMask mask)
        {
            GetCapsuleSize(out float radius, out float height);
            return Physics.CheckCapsule(bottom, bottom + Vector3.up * (height - 2f * radius),
                radius, mask, QueryTriggerInteraction.Ignore);
        }

        // Recover only shallow penetration, not arbitrary surfaces above us.
        // The ray begins above the foot so thin mesh terrain can still be found
        // when a capsule cast begins intersecting its triangles.
        public bool TryRepairGroundContact(ref Vector3 bottom, LayerMask mask)
        {
            GetCapsuleSize(out float radius, out _);
            const float maxCorrection = 0.15f;
            if (!Physics.Raycast(bottom + Vector3.up * maxCorrection, Vector3.down,
                out RaycastHit hit, radius + maxCorrection * 2f, mask, QueryTriggerInteraction.Ignore)
                || !CrawlGraph.IsWalkableGround(hit.normal)) return false;
            float lift = (radius + CollisionSkin - Vector3.Dot(bottom - hit.point, hit.normal)) / hit.normal.y;
            if (lift <= 0.0001f || lift > maxCorrection) return false;
            Vector3 repaired = bottom + Vector3.up * lift;
            if (CapsuleOverlaps(repaired, mask)) return false;
            bottom = repaired;
            return true;
        }

        /// <summary>Adopt an airborne capsule without moving its collision volume into the slope.</summary>
        public Vector3 AdoptLanding(Vector3 bottom, Vector3 normal)
        {
            // Brief contact losses must not erase the stuck window or an escape.
            ClearPath();
            _currentNode = -1;
            _pauseTimer = 0f;
            GetCapsuleSize(out float radius, out _);
            _supportNormal = normal.normalized;
            _supportPosition = RootFromCapsuleBottom(bottom);
            _hasSupport = true;
            return _supportPosition;
        }

        public bool Overlaps(Vector3 position, LayerMask mask)
        {
            return CapsuleOverlaps(SupportedCapsuleBottom(position, mask), mask);
        }

        /// <summary>Push an overlapping capsule out of the environment. Returns true when it is clear.</summary>
        public bool Separate(ref Vector3 position, LayerMask mask)
        {
            Vector3 bottom = SupportedCapsuleBottom(position, mask);
            if (!CapsuleOverlaps(bottom, mask)) return true;
            bool clear = ResolvePenetration(ref bottom, mask);
            if (!clear || CapsuleOverlaps(bottom, mask)) return false;
            position = RootFromCapsuleBottom(bottom);
            return true;
        }

        /// <summary>Find a nearby supported escape from an overlap or stalled drop.</summary>
        public bool TryLocalHitEscape(Vector3 origin, LayerMask mask, out Vector3 position)
        {
            position = origin;
            Vector3 sourceBottom = SupportedCapsuleBottom(origin, mask);
            GetCapsuleSize(out float radius, out float height);
            float allowedContact = CapsuleOverlaps(sourceBottom, mask) ? 0.15f : 0f;
            float bestDistance = float.PositiveInfinity;
            for (int ring = 1; ring <= 3; ring++)
            for (int direction = 0; direction < 8; direction++)
            {
                Vector3 offset = Quaternion.AngleAxis(direction * 45f, Vector3.up)
                    * Vector3.forward * (ring * 0.3f);
                if (!Physics.Raycast(origin + offset + Vector3.up, Vector3.down,
                    out RaycastHit ground, 2f, mask, QueryTriggerInteraction.Ignore)
                    || !CrawlGraph.IsWalkableGround(ground.normal)) continue;
                Vector3 bottom = CapsuleBottomOnSurface(ground.point, ground.normal);
                Vector3 candidate = RootFromCapsuleBottom(bottom);
                float distance = Vector3.Distance(bottom, sourceBottom);
                if (distance > 1.2f || distance >= bestDistance || CapsuleOverlaps(bottom, mask)
                    || !HasGroundSupport(candidate, mask)) continue;

                // Sweep backwards from clear space: a sweep starting embedded can miss walls.
                // Only tolerate shallow contact at the original, already stuck position.
                Vector3 back = sourceBottom - bottom;
                if (CastFromBottom(bottom, back, mask, out RaycastHit obstruction)
                    && obstruction.distance < distance - allowedContact) continue;
                Vector3 centerOffset = Vector3.up * (height * 0.5f - radius);
                if (Physics.Linecast(bottom + centerOffset, sourceBottom + centerOffset,
                    mask, QueryTriggerInteraction.Ignore)) continue;
                bestDistance = distance;
                position = candidate;
            }
            return bestDistance < float.PositiveInfinity;
        }

        /// <summary>
        /// One kinematic drop. On landing, position is the contact pose before AdoptLanding.
        /// </summary>
        public bool TickFall(ref Vector3 position, ref float fallSpeed, float dt, LayerMask mask, out Vector3 landNormal)
        {
            landNormal = Vector3.up;
            if (dt <= 0f) return false;
            fallSpeed += FallGravity * dt;
            float distance = fallSpeed * dt;
            Vector3 delta = Vector3.down * distance;
            bool swept = FallSweep(position, delta, mask, out RaycastHit hit);
            bool foot = FallGround(position, distance, mask, out float groundTravel, out Vector3 groundNormal);
            if (foot && (!swept || groundTravel <= Mathf.Max(0f, hit.distance - CapsuleSkin)))
            {
                position += Vector3.down * groundTravel;
                landNormal = groundNormal;
                fallSpeed = 0f;
                return true;
            }
            if (swept)
            {
                float travel = hit.distance - CapsuleSkin;
                if (travel < 0f) travel = 0f;
                if (travel > distance) travel = distance;
                position += Vector3.down * travel;
                if (CrawlGraph.IsWalkableGround(hit.normal))
                {
                    landNormal = hit.normal;
                    fallSpeed = 0f;
                    return true;
                }
                Vector3 slide = Vector3.ProjectOnPlane(hit.normal, Vector3.up);
                if (slide.sqrMagnitude > 1e-6f)
                    position += slide.normalized * CapsuleSkin;
                return false;
            }
            position += delta;
            return false;
        }

        bool FallSweep(Vector3 from, Vector3 delta, LayerMask mask, out RaycastHit hit)
        {
            hit = default;
            float distance = delta.magnitude;
            if (distance < 1e-5f) return false;
            GetCapsuleSize(out float radius, out float height);
            Vector3 bottom = SupportedCapsuleBottom(from, mask);
            Vector3 top = bottom + Vector3.up * (height - radius * 2f);
            return Physics.CapsuleCast(bottom, top, radius, delta / distance, out hit,
                distance, mask, QueryTriggerInteraction.Ignore);
        }

        bool FallGround(Vector3 from, float distance, LayerMask mask, out float travel, out Vector3 normal)
        {
            travel = 0f;
            normal = Vector3.up;
            GetCapsuleSize(out float radius, out float height);
            Vector3 bottom = SupportedCapsuleBottom(from, mask);
            if (!Physics.Raycast(bottom + Vector3.up * CapsuleSkin, Vector3.down, out RaycastHit hit,
                distance + radius + CapsuleSkin * 2f, mask, QueryTriggerInteraction.Ignore)
                || !CrawlGraph.IsWalkableGround(hit.normal)) return false;
            travel = (Vector3.Dot(bottom - hit.point, hit.normal) - radius - CapsuleSkin) / hit.normal.y;
            if (travel < -0.15f || travel > distance) return false;
            Vector3 point = from + Vector3.down * travel;
            Vector3 lower = SupportedCapsuleBottom(point, mask);
            Vector3 upper = lower + Vector3.up * (height - radius * 2f);
            if (Physics.CheckCapsule(lower, upper, radius, mask, QueryTriggerInteraction.Ignore)) return false;
            normal = hit.normal;
            return true;
        }

        /// <summary>Checks actual nearby support, including slopes and partial edge contact.</summary>
        public bool HasGroundSupport(Vector3 position, LayerMask mask)
        {
            Vector3 normal = GroundNormal(position, mask);
            Vector3 bottom = SupportedCapsuleBottom(position, mask);
            if (CastFromBottom(bottom, -normal * 0.15f, mask, out RaycastHit hit)
                && CrawlGraph.IsWalkableGround(hit.normal)) return true;
            if (CastFromBottom(bottom, Vector3.down * 0.15f, mask, out hit)
                && CrawlGraph.IsWalkableGround(hit.normal)) return true;
            // Use the same validated foot contact as TickFall. Capsule casts can
            // miss resting contact at mesh seams, otherwise a successful landing
            // immediately starts another drop and never advances retreat.
            return FallGround(position, 0.15f, mask, out _, out _);
        }

        bool CastWall(Vector3 pos, Vector3 delta, LayerMask mask, out RaycastHit hit)
        {
            return CastFromBottom(SupportedCapsuleBottom(pos, mask), delta, mask, out hit);
        }

        // Called before Smasher traversal owns this tick. The same upright
        // capsule is now used for walking, takeoff, flight and landing.
        public bool ValidateSmasherPose(Enemy enemy, LayerMask mask)
        {
            Vector3 pos = enemy.Root.position;
            Vector3 bottom = SupportedCapsuleBottom(pos, mask);
            Vector3 movement = bottom - _previousBottom;
            bool crossed = _hasPreviousBottom && movement.sqrMagnitude > 1e-8f
                && Physics.Raycast(_previousBottom, movement.normalized, movement.magnitude,
                    mask, QueryTriggerInteraction.Ignore);
            bool overlapping = CapsuleOverlaps(bottom, mask);
            if (crossed || overlapping)
            {
                Vector3 repaired = bottom;
                if (!crossed && ResolvePenetration(ref repaired, mask))
                {
                    pos = RootFromCapsuleBottom(repaired);
                    enemy.SetRootPose(pos, enemy.Root.rotation);
                    enemy.Root.SetPositionAndRotation(pos, enemy.Root.rotation);
                    ClearPath();
                    _hasSupport = false;
                    bottom = repaired;
                }
                else if (_hasSafePose && !CapsuleOverlaps(SupportedCapsuleBottom(_safePosition, mask), mask)
                    && HasGroundSupport(_safePosition, mask))
                {
                    {
                        if (DuckzLog.AllowWarning(out var logNotice1))
                            logNotice1.Write($"Enemy recovery: {enemy.name}, reason=smasher pose rollback, crossed={crossed}, overlapping={overlapping}, state={enemy.CurrentState}, from={pos}, to={_safePosition}", enemy);
                    }
                    enemy.Teleport(_safePosition, _safeRotation);
                    return true;
                }
                else return false; // Existing timed respawn remains the final fallback.
            }
            _previousBottom = bottom;
            _hasPreviousBottom = true;
            if (!enemy.IsJumping && !CapsuleOverlaps(bottom, mask) && HasGroundSupport(pos, mask))
            {
                _safePosition = pos; _safeRotation = enemy.Root.rotation; _hasSafePose = true;
            }
            return crossed || overlapping;
        }

        bool ResolvePenetration(ref Vector3 bottom, LayerMask mask)
        {
            GetCapsuleSize(out float radius, out float height);
            if (_queryCapsule == null)
            {
                var helper = new GameObject("Enemy capsule query") { hideFlags = HideFlags.HideAndDontSave };
                _queryCapsule = helper.AddComponent<CapsuleCollider>();
                _queryCapsule.enabled = false; // Used only by ComputePenetration, never by simulation.
            }
            _queryCapsule.radius = radius; _queryCapsule.height = height;
            _queryCapsule.center = Vector3.up * (height * .5f - radius);
            Vector3 start = bottom;
            for (int iteration = 0; iteration < 8; iteration++)
            {
                int count = Physics.OverlapCapsuleNonAlloc(bottom, bottom + Vector3.up * (height - 2f * radius),
                    radius, _overlaps, mask, QueryTriggerInteraction.Ignore);
                if (count == 0) return true;
                if (count == _overlaps.Length) return false;
                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = _overlaps[i];
                    if (Physics.ComputePenetration(_queryCapsule, bottom, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance))
                    {
                        bottom += direction * (distance + CapsuleSkin);
                        moved = true;
                    }
                }
                if (!moved || Vector3.Distance(start, bottom) > Mathf.Max(1f, radius * 2f)) return false;
            }
            return !CapsuleOverlaps(bottom, mask);
        }

        void OnDestroy()
        {
            if (_queryCapsule == null) return;
            if (Application.isPlaying) Destroy(_queryCapsule.gameObject);
            else DestroyImmediate(_queryCapsule.gameObject);
        }

        Vector3 GroundNormal(Vector3 pos, LayerMask mask)
        {
            // Preserve capsule continuity at crests, where a vertical ray can
            // switch to the plateau before the capsule has cleared its edge.
            if (_hasSupport && (pos - _supportPosition).sqrMagnitude < 1e-6f) return _supportNormal;
            if (Physics.Raycast(pos + Vector3.up * snapProbe, Vector3.down, out RaycastHit hit,
                snapProbe + groundSnapDistance, mask, QueryTriggerInteraction.Ignore)
                && CrawlGraph.IsWalkableGround(hit.normal)) return hit.normal;
            return Vector3.up;
        }

        public void GetCapsuleSize(out float radius, out float height)
        {
            if (_hasMoveCapsule) { radius = _moveRadius; height = _moveHeight; return; }
            Vector3 scale = GetRoot().lossyScale;
            radius = Mathf.Max(0.01f, collisionRadius * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.z)));
            height = Mathf.Max(radius * 2f, collisionHeight * Mathf.Abs(scale.y));
        }

        bool CastFromBottom(Vector3 bottom, Vector3 delta, LayerMask mask, out RaycastHit hit)
        {
            hit = default;
            float distance = delta.magnitude;
            if (distance < 1e-5f) return false;
            GetCapsuleSize(out float radius, out float height);
            Vector3 top = bottom + Vector3.up * (height - radius * 2f);
            return Physics.CapsuleCast(bottom, top, radius, delta / distance, out hit,
                distance + CollisionSkin, mask, QueryTriggerInteraction.Ignore);
        }

        void RequestPath(int goal)
        {
            _wanderRequest = false;
            _pathVersion++;
            if (!_wantsPath) _pathQueuedFrame = Time.frameCount;
            _goalNode = goal;
            _wantsPath = goal >= 0;
        }

        int CurrentNode(EnemyManager m, Vector3 position)
        {
            // The locomotion root already represents foot contact. Projecting it
            // downward again can miss the supporting face (or a tread edge) and
            // choose the storey below. Snap locally to the known foot position.
            int n = m.Graph.FindNearestGroundNode(position, 1.5f);
            _currentNode = n;
            return _currentNode;
        }

        bool GoalOnWalkingLevel(CrawlGraph graph, Vector3 position)
        {
            // Use the existing planning goal, not another physics query per tick.
            // A visible target upstairs still needs its route to the stair entrance.
            return _goalNode >= 0 &&
                Mathf.Abs(graph.GetPosition(_goalNode).y - position.y) <= Mathf.Max(0.1f, maxStepHeight);
        }

        Transform GetRoot()
        {
            Enemy enemy = GetComponent<Enemy>();
            return enemy != null ? enemy.Root : transform;
        }

        Quaternion TurnTowards(Transform root, Vector3 flatDir, Vector3 up, float dt)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return root.rotation;
            return Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(flatDir.normalized, up), turnSpeed * dt);
        }

        void ApplyMovementPose(Enemy enemy, Vector3 next, float dt, float yawSpeed)
        {
            Transform root = enemy.Root;
            Vector3 moved = Vector3.ProjectOnPlane(next - root.position, Vector3.up);
            // A blocked desired direction can change with every new path. Only
            // actual travel should turn the rig; ignore contact-scale motion.
            Quaternion rotation = root.rotation;
            if (moved.sqrMagnitude > Mathf.Pow(0.08f * dt, 2f))
                rotation = Quaternion.RotateTowards(rotation,
                    Quaternion.LookRotation(moved, Vector3.up), yawSpeed * dt);
            enemy.SetRootPose(next, rotation);
        }

        void TrySkipAhead(CrawlGraph g, Vector3 pos, LayerMask mask)
        {
            if (AvoidingRoute) return; // Shortcuts must not bypass the detour.
            int furthest = _pathIndex;
            int limit = Mathf.Min(_pathCount - 1, _pathIndex + 3);
            for (int i = _pathIndex + 1; i <= limit; i++)
            {
                if (!g.IsGroundNode(_path[i])) break;
                // Retain slope transitions; a straight shortcut can cut through a crest.
                if (Mathf.Abs(g.GetPosition(_path[i]).y - pos.y) > arriveRadius) break;
                furthest = i;
            }
            if (furthest == _pathIndex) return;

            Vector3 delta = Vector3.ProjectOnPlane(g.GetPosition(_path[furthest]) - pos, Vector3.up);
            if (!CastWall(pos, delta, mask, out _))
                _pathIndex = furthest;
        }

        // Build a bounded, ground-connected route instead of sampling a cluster
        // that may only be connected through walls used by climbing enemies.
        void BuildWanderPath(EnemyManager m, Vector3 pos, int start, bool retryUnrestricted = true)
        {
            ClearPath();
            _pathComplete = true;
            _pauseTimer = 0.25f;
            if (start < 0) return;
            CrawlGraph g = m.Graph;
            _wanderVisited.Clear();
            _wanderVisited.Add(start);
            _wanderNodes[0] = start;
            _wanderParents[0] = -1;
            int count = 1, choice = -1, candidates = 0;
            float minDistance = Mathf.Max(arriveRadius * 2f, 0.5f);
            for (int head = 0; head < count; head++)
            {
                int node = _wanderNodes[head];
                Vector3 point = g.GetPosition(node);
                if ((point - pos).sqrMagnitude >= minDistance * minDistance
                    && Random.Range(0, ++candidates) == 0) choice = head;
                for (int e = g.EdgeStart[node]; e < g.EdgeStart[node + 1] && count < _wanderNodes.Length; e++)
                {
                    int next = g.EdgeTarget[e];
                    if (!g.IsGroundNode(next) || _wanderVisited.Contains(next)) continue;
                    if (AvoidingRoute && _blockedNodes.Contains(next)) continue;
                    Vector3 nextPos = g.GetPosition(next);
                    if ((nextPos - pos).sqrMagnitude > wanderRadius * wanderRadius) continue;
                    // Reject blocked first steps on level ground. Slope transitions
                    // are handled by the surface-following movement sweep.
                    if (head == 0 && Mathf.Abs(nextPos.y - pos.y) < 0.05f
                        && CastWall(pos, nextPos - pos, m.SurfaceMask, out _)) continue;
                    _wanderVisited.Add(next);
                    _wanderNodes[count] = next;
                    _wanderParents[count++] = head;
                }
            }
            if (choice < 0)
            {
                // Avoidance is advisory. If it eliminates every wander exit,
                // retry once with fresh routes while retaining collision checks.
                if (retryUnrestricted && AvoidingRoute)
                {
                    _blockedNodes.Clear();
                    _blockedExpiry.Clear();
                    _blockedUntil = 0f;
                    BuildWanderPath(m, pos, start, false);
                }
                return;
            }
            // Use scratch storage for the reverse chain, then retain its start if
            // the chosen route exceeds the movement path capacity.
            int length = 0;
            for (int i = choice; i >= 0; i = _wanderParents[i]) length++;
            _pathCount = Mathf.Min(length, _path.Length);
            int cursor = choice;
            for (int i = length - 1; i >= 0; i--)
            {
                if (i < _pathCount) _path[i] = _wanderNodes[cursor];
                cursor = _wanderParents[cursor];
            }
            _pathIndex = _pathCount > 1 ? 1 : 0;
            _goalNode = _path[_pathCount - 1];
            _pauseTimer = Random.Range(wanderPause.x, wanderPause.y);
        }

        void UpdateStuck(Enemy enemy, EnemyManager m, float dt, Vector3 pos)
        {
            _progressTravel += Vector3.ProjectOnPlane(pos - _lastProgressPos, Vector3.up).magnitude;
            _lastProgressPos = pos;
            _stuckTimer += dt;
            // Preserve ordinary wander pauses, but repeated empty/failed routes
            // cannot postpone recovery indefinitely.
            float delay = enemy.CurrentState == Enemy.State.Wander && PathDone && !WantsPath
                ? Mathf.Max(stuckDelay, Mathf.Max(wanderPause.x, wanderPause.y) + 0.5f)
                : stuckDelay;
            if (_stuckTimer < delay) return;
            float progress = Vector3.ProjectOnPlane(pos - _stuckPos, Vector3.up).magnitude;
            _stuckTimer = 0f;
            _stuckPos = pos;
            bool advancing = progress >= 0.2f && progress >= _progressTravel * 0.5f;
            _progressTravel = 0f;
            if (advancing) { _escapeAttempt = 0; return; }
            Vector3 blockedDirection = _pathIndex < _pathCount
                ? m.Graph.GetPosition(_path[_pathIndex]) - pos
                : enemy.CurrentState == Enemy.State.Chase ? enemy.LastSeenPos - pos : enemy.Root.forward;
            int blocked = _pathIndex < _pathCount ? _path[_pathIndex]
                : m.ResolveGroundNode(pos + Vector3.ProjectOnPlane(blockedDirection, Vector3.up).normalized * escapeDistance);
            BlockNode(blocked);
            if (!_stalled) { _stalled = true; _stallOrigin = pos; _stallTime = _spawnRetry = 0f; }
            ClearPath();
            _currentNode = -1;
            _pauseTimer = 0f;
            TryBeginEscape(pos, blockedDirection, m.SurfaceMask);
            if (_escapeTimer <= 0f) RequestWander(m);
        }

        void TryBeginEscape(Vector3 pos, Vector3 blockedDirection, LayerMask mask)
        {
            Vector3 backward = -Vector3.ProjectOnPlane(blockedDirection, Vector3.up).normalized;
            if (backward.sqrMagnitude < 1e-6f) backward = Vector3.back;
            // Rotate the search on repeated attempts instead of retrying the same
            // blocked destination. No teleporting or collision bypass is involved.
            int offset = _escapeAttempt++ % 8;
            for (int i = 0; i < 8; i++)
            {
                Vector3 direction = Quaternion.AngleAxis((i + offset) * 45f, Vector3.up) * backward;
                Vector3 target = pos + direction * escapeDistance;
                if (!Physics.Raycast(target + Vector3.up * snapProbe, Vector3.down, out RaycastHit ground,
                    snapProbe + groundSnapDistance, mask, QueryTriggerInteraction.Ignore)
                    || !CrawlGraph.IsWalkableGround(ground.normal)) continue;
                target = ground.point;
                if ((target - pos).sqrMagnitude < 0.04f || CastWall(pos, target - pos, mask, out _)) continue;
                bool supported = true;
                int samples = Mathf.Max(2, Mathf.CeilToInt(Vector3.Distance(pos, target) / 0.2f));
                for (int sample = 1; sample < samples; sample++)
                {
                    Vector3 probe = Vector3.Lerp(pos, target, sample / (float)samples);
                    if (!Physics.Raycast(probe + Vector3.up * snapProbe, Vector3.down, out RaycastHit support,
                        snapProbe + 0.2f, mask, QueryTriggerInteraction.Ignore)
                        || !CrawlGraph.IsWalkableGround(support.normal)) { supported = false; break; }
                }
                if (!supported) continue;
                _escapeTarget = target;
                _escapeSpeed = 0f;
                _escapeTimer = 2f;
                return;
            }
        }

        bool TickStallRecovery(Enemy enemy, EnemyManager manager, float dt)
        {
            if (!_stalled || !enemy.HasAuthority || enemy.IsDormant) return false;
            Vector3 pos = enemy.Root.position;
            Vector3 toPlayer = Vector3.ProjectOnPlane(enemy.LastSeenPos - pos, Vector3.up);
            bool attacking = enemy.CurrentState == Enemy.State.Chase && enemy.Seen
                && GoalOnWalkingLevel(manager.Graph, pos)
                && toPlayer.sqrMagnitude < enemy.StopDistance * enemy.StopDistance
                && !CastWall(pos, toPlayer, manager.SurfaceMask, out _);
            bool resting = enemy.CurrentState == Enemy.State.Wander && _pathCount > 0
                && PathDone && !_wantsPath && _escapeTimer <= 0f && _pauseTimer > 0f;
            if (attacking || resting || Vector3.ProjectOnPlane(pos - _stallOrigin, Vector3.up).sqrMagnitude >= 1f)
            {
                _stalled = false;
                _stallTime = _spawnRetry = 0f;
                return false;
            }
            _stallTime += dt;
            _spawnRetry -= dt;
            if (_stallTime < stalledRespawnDelay || _spawnRetry > 0f) return false;
            _spawnRetry = 1f;
            // Teleport resets the brain and locomotion. The caller must not write
            // its old movement pose after a successful recovery.
            return manager.TryRespawnRandom(enemy, out _, out _, "movement stalled");
        }

        void TickEscape(Enemy enemy, EnemyManager m, float dt)
        {
            Vector3 pos = enemy.Root.position;
            Vector3 delta = _escapeTarget - pos;
            _escapeTimer -= dt;
            if (delta.sqrMagnitude > 0.01f && _escapeTimer > 0f)
            {
                bool chasingVisiblePlayer = enemy.CurrentState == Enemy.State.Chase && enemy.Seen;
                float desiredSpeed = chasingVisiblePlayer
                    ? chaseSpeed : Mathf.Min(Mathf.Min(walkSpeed, 0.8f), Mathf.Sqrt(4f * delta.magnitude));
                // Recovery must not silently override the prefab's chase speed.
                _escapeSpeed = chasingVisiblePlayer ? desiredSpeed
                    : Mathf.MoveTowards(Mathf.Min(_escapeSpeed, desiredSpeed), desiredSpeed, 2f * dt);
                Vector3 next = MoveWithWalls(pos, delta.normalized * Mathf.Min(_escapeSpeed * dt, delta.magnitude), m.SurfaceMask);
                ApplyMovementPose(enemy, next, dt, Mathf.Min(turnSpeed, 90f));
                return;
            }
            _escapeTimer = 0f;
            _stuckPos = pos;
            _lastProgressPos = pos;
            _progressTravel = 0f;
            _stuckTimer = 0f;
            _pauseTimer = 0f;
            if (enemy.CurrentState == Enemy.State.Chase && enemy.Seen)
                ForcePathToWorld(m, enemy.LastSeenPos);
            else RequestWander(m);
        }

        // Jump owns the movement tick during a fall. Keep elapsed recovery time
        // so repeated one-frame falls cannot starve the ground watchdog.
        public void RecordUnplannedFall(float dt)
        {
            _stuckTimer += dt;
            _escapeTimer = Mathf.Max(0f, _escapeTimer - dt);
        }

        public void CheckAfterUnplannedLanding(Enemy enemy, EnemyManager manager, Vector3 position)
        {
            if (!IsRecovering) UpdateStuck(enemy, manager, 0f, position);
        }
#if UNITY_EDITOR
        public void DrawPathGizmos()
        {
            if (GetRoot() != null)
            {
                GetCapsuleSize(out float radius, out float height);
                Vector3 bottom = SupportedCapsuleBottom(GetRoot().position, 0);
                Vector3 top = bottom + Vector3.up * (height - radius * 2f);
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(bottom, radius); Gizmos.DrawWireSphere(top, radius);
                Gizmos.DrawLine(bottom + Vector3.right * radius, top + Vector3.right * radius);
                Gizmos.DrawLine(bottom - Vector3.right * radius, top - Vector3.right * radius);
                Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(GetRoot().position, .08f);
                if (_hasSafePose) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(_safePosition, .12f); }
            }
            if (EnemyManager.Instance == null || EnemyManager.Instance.Graph == null) return;
            CrawlGraph g = EnemyManager.Instance.Graph;
            Gizmos.color = Color.green;
            for (int i = Mathf.Max(1, _pathIndex); i < _pathCount; i++)
                Gizmos.DrawLine(g.GetPosition(_path[i - 1]), g.GetPosition(_path[i]));
        }
#endif
    }
}
