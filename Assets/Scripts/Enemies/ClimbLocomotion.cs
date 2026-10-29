using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Wall/ceiling crawl-graph mover. Moves the enemy root; gait poses the body.
    /// </summary>
    public class ClimbLocomotion : MonoBehaviour, IEnemyLocomotion
    {
        [Header("Movement")]
        [SerializeField] float walkSpeed = 0.7f;
        [SerializeField] float chaseSpeed = 1.8f;
        [SerializeField] float turnSpeed = 540f;          // deg/s
        [SerializeField] float upSmooth = 10f;
        [SerializeField] float arriveRadius = 0.3f;
        [SerializeField] float snapProbe = 0.35f;
        [SerializeField, Min(0.01f)] float collisionRadius = 0.21f;
        [Tooltip("Maximum low ledge height the body can step onto or down from a floor, in world units. Zero disables stepping.")]
        [SerializeField, Min(0f)] float maxStepHeight = 0f;
        const float CollisionSkin = 0.02f;

        [Header("Wander")]
        [SerializeField] float wanderRadius = 18f;
        [SerializeField] Vector2 wanderPause = new Vector2(1f, 4f);

        readonly int[] _path = new int[128];
        readonly int[] _pendingPath = new int[128];
        int _pathVersion, _pathQueuedFrame;
        int _pathCount, _pathIndex;
        bool _pathComplete, _wantsPath;
        int _goalNode = -1;
        int _currentNode = -1;

        Vector3 _up = Vector3.up;
        Vector3 _surfaceUp = Vector3.up;
        float _pauseTimer;
        Vector3 _stuckPos;
        float _stuckTimer;
        Vector3 _escapeTarget, _escapeNormal;
        float _escapeTimer;
        int _escapeAttempt;
        readonly System.Collections.Generic.HashSet<int> _retreatVisited = new System.Collections.Generic.HashSet<int>();
        readonly Collider[] _overlaps = new Collider[16];
        SphereCollider _querySphere;
        Vector3 _travelHeading, _pendingHeading;
        float _headingTimer;
        Vector3 _edgeDirection, _edgeNormal;
        int _progressNode = -1;
        float _bestNodeDistance, _nodeProgressTimer;

        public Vector3 SurfaceNormal => _surfaceUp;

        SpiderWeb _web;
        bool _webDeparture, _webContact;
        Vector3 _webOrigin;
        void Awake() => _web = GetComponent<SpiderWeb>();

        // Recovery queries use an explicit contact normal. The visible root up is
        // smoothed during transitions and is not necessarily the collision normal.
        public bool IsRecoveryPoseValid(Vector3 position, Vector3 up, LayerMask mask, out string reason)
        {
            if (up.sqrMagnitude < .5f) { reason = "invalid surface normal"; return false; }
            up.Normalize();
            float radius = WorldRadius();
            Vector3 center = position + up * (radius + CollisionSkin);
            if (Physics.CheckSphere(center, radius, mask, QueryTriggerInteraction.Ignore))
            { reason = "saved body overlaps geometry"; return false; }
            if (!Physics.SphereCast(center, radius, -up, out RaycastHit hit,
                snapProbe + CollisionSkin, mask, QueryTriggerInteraction.Ignore)
                || Vector3.Dot(hit.normal, up) < .8f)
            { reason = "saved surface no longer supports body"; return false; }
            reason = "valid";
            return true;
        }

        public string DescribeOverlap(Vector3 position, LayerMask mask)
        {
            float radius = WorldRadius();
            int count = Physics.OverlapSphereNonAlloc(BodyCenter(position, radius), radius, _overlaps,
                mask, QueryTriggerInteraction.Ignore);
            var description = new System.Text.StringBuilder();
            for (int i = 0; i < count; i++)
            {
                if (i > 0) description.Append(", ");
                Collider collider = _overlaps[i];
                description.Append(collider.name).Append('#').Append(collider.GetInstanceID())
                    .Append('(').Append(collider.GetType().Name).Append(", layer=").Append(collider.gameObject.layer).Append(')');
            }
            if (count == _overlaps.Length) description.Append(" [buffer full]");
            return description.ToString();
        }

        public bool TryLocalRecovery(Vector3 position, LayerMask mask, out Vector3 recovered, out Vector3 up)
        {
            recovered = position;
            up = _surfaceUp;
            float radius = WorldRadius();
            Vector3 origin = BodyCenter(position, radius);
            // Nearest shell first; never relocate more than one world unit.
            for (int shell = 1; shell <= 3; shell++)
            for (int x = -1; x <= 1; x++)
            for (int y = -1; y <= 1; y++)
            for (int z = -1; z <= 1; z++)
            {
                Vector3 offset = new Vector3(x, y, z);
                if (offset.sqrMagnitude == 0f) continue;
                Vector3 center = origin + offset.normalized * (shell * .25f);
                if (Physics.CheckSphere(center, radius, mask, QueryTriggerInteraction.Ignore)) continue;
                // Sweep from the clear end toward the embedded body. Only contact
                // in the original body's immediate vicinity may be crossed.
                Vector3 back = origin - center;
                if (Physics.SphereCast(center, radius, back.normalized, out RaycastHit obstruction,
                    back.magnitude, mask, QueryTriggerInteraction.Ignore)
                    && obstruction.distance < back.magnitude - radius - CollisionSkin) continue;
                for (int axis = 0; axis < 7; axis++)
                {
                    Vector3 normal = axis == 0 ? _surfaceUp : axis == 1 ? Vector3.up : axis == 2 ? Vector3.down
                        : axis == 3 ? Vector3.right : axis == 4 ? Vector3.left : axis == 5 ? Vector3.forward : Vector3.back;
                    if (!Physics.SphereCast(center, radius, -normal, out RaycastHit support,
                        .25f, mask, QueryTriggerInteraction.Ignore)) continue;
                    Vector3 landingCenter = center - normal * Mathf.Max(0f, support.distance - CollisionSkin);
                    Vector3 candidate = landingCenter - support.normal * (radius + CollisionSkin);
                    if (Vector3.Distance(position, candidate) > 1f
                        || !IsRecoveryPoseValid(candidate, support.normal, mask, out _)) continue;
                    recovered = candidate;
                    up = support.normal;
                    return true;
                }
            }
            return false;
        }

        public bool HasSurfaceSupport(Vector3 position, LayerMask mask)
        {
            float radius = WorldRadius();
            Vector3 center = position + _surfaceUp * (radius + CollisionSkin);
            return Physics.SphereCast(center, radius, -_surfaceUp, out _,
                snapProbe + CollisionSkin, mask, QueryTriggerInteraction.Ignore);
        }

        public bool Overlaps(Vector3 position, LayerMask mask)
        {
            float radius = WorldRadius();
            return Physics.CheckSphere(BodyCenter(position, radius), radius, mask, QueryTriggerInteraction.Ignore);
        }

        /// <summary>Push an overlapping body sphere out of the environment. Returns true when it is clear.</summary>
        public bool Separate(ref Vector3 position, LayerMask mask)
        {
            float radius = WorldRadius();
            float clearance = radius + CollisionSkin;
            Vector3 center = BodyCenter(position, radius);
            if (!Physics.CheckSphere(center, radius, mask, QueryTriggerInteraction.Ignore)) return true;
            EnsureQuerySphere(radius);
            Vector3 start = center;
            bool clear = false;
            for (int iteration = 0; iteration < 8; iteration++)
            {
                int count = Physics.OverlapSphereNonAlloc(center, radius, _overlaps, mask, QueryTriggerInteraction.Ignore);
                if (count == 0) { clear = true; break; }
                if (count == _overlaps.Length) break;
                bool moved = false;
                for (int i = 0; i < count; i++)
                {
                    Collider other = _overlaps[i];
                    if (other == null) continue;
                    if (!Physics.ComputePenetration(_querySphere, center, Quaternion.identity,
                        other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance))
                        continue;
                    center += direction * (distance + CollisionSkin);
                    moved = true;
                }
                if (!moved || Vector3.Distance(start, center) > Mathf.Max(1f, radius * 2f)) break;
            }
            if (!clear)
                clear = !Physics.CheckSphere(center, radius, mask, QueryTriggerInteraction.Ignore);
            if (!clear || Vector3.Distance(start, center) > Mathf.Max(1f, radius * 2f)) return false;
            position = center - _surfaceUp * clearance;
            return true;
        }

        /// <summary>One kinematic drop. A hit reattaches the body to that surface.</summary>
        public bool TickFall(ref Vector3 position, ref float fallSpeed, float dt, LayerMask mask, out Vector3 landNormal)
        {
            landNormal = _surfaceUp.sqrMagnitude > 1e-6f ? _surfaceUp : Vector3.up;
            if (dt <= 0f) return false;
            fallSpeed += GroundLocomotion.FallGravity * dt;
            float distance = fallSpeed * dt;
            float radius = WorldRadius();
            float clearance = radius + CollisionSkin;
            Vector3 center = position + landNormal * clearance;
            if (!Physics.SphereCast(center, radius, Vector3.down, out RaycastHit hit, distance + CollisionSkin,
                mask, QueryTriggerInteraction.Ignore))
            {
                position += Vector3.down * distance;
                return false;
            }

            float travel = hit.distance - CollisionSkin;
            if (travel < 0f) travel = 0f;
            if (travel > distance) travel = distance;
            center += Vector3.down * travel;
            _surfaceUp = hit.normal.sqrMagnitude > 1e-6f ? hit.normal.normalized : Vector3.up;
            position = center - _surfaceUp * clearance;
            landNormal = _surfaceUp;
            fallSpeed = 0f;
            return true;
        }

        Vector3 BodyCenter(Vector3 position, float radius)
        {
            Vector3 up = _surfaceUp.sqrMagnitude > 1e-6f ? _surfaceUp : Vector3.up;
            return position + up * (radius + CollisionSkin);
        }

        void EnsureQuerySphere(float radius)
        {
            if (_querySphere == null)
            {
                GameObject helper = new GameObject("Enemy sphere query");
                helper.hideFlags = HideFlags.HideAndDontSave;
                _querySphere = helper.AddComponent<SphereCollider>();
                _querySphere.enabled = false;
            }
            _querySphere.radius = radius;
            _querySphere.center = Vector3.zero;
        }

        void OnDestroy()
        {
            if (_querySphere == null) return;
            if (Application.isPlaying) Destroy(_querySphere.gameObject);
            else DestroyImmediate(_querySphere.gameObject);
        }

        public bool PathDone => _pathIndex >= _pathCount;
        public bool WantsPath => _wantsPath;

        public void Reset()
        {
            _pathVersion++;
            _edgeDirection = Vector3.zero;
            _progressNode = -1;
            _nodeProgressTimer = 0f;
            _pathCount = _pathIndex = 0;
            _wantsPath = false;
            _goalNode = _currentNode = -1;
            _pauseTimer = Random.Range(wanderPause.x, wanderPause.y);
            _stuckTimer = 0f;
            _escapeTimer = 0f;
            _travelHeading = transform.forward;
            _pendingHeading = Vector3.zero;
            _headingTimer = 0f;
            _stuckPos = transform.position;
        }

        public void ClearPath()
        {
            _pathVersion++;
            _goalNode = -1;
            _pathCount = _pathIndex = 0;
            _wantsPath = false;
        }

        public void Interrupt()
        {
            ClearPath();
            _pauseTimer = 0f;
            Enemy enemy = GetComponent<Enemy>();
            SyncUpFromRoot(enemy != null ? enemy.Root : transform);
        }

        /// <summary>Slide along the current face without turning or following a path.</summary>
        public Vector3 Displace(Vector3 position, Vector3 delta, LayerMask mask)
        {
            Vector3 tangent = Vector3.ProjectOnPlane(delta, _surfaceUp);
            float distance = tangent.magnitude;
            if (distance < 1e-5f) return position;
            return MoveOnSurface(position, position + tangent, _surfaceUp, distance, mask);
        }

        public bool HasHitClearance(Vector3 position, Vector3 direction, LayerMask mask)
        {
            float radius = WorldRadius();
            return !Physics.SphereCast(BodyCenter(position, radius), radius, direction, out _,
                0.1f + CollisionSkin, mask, QueryTriggerInteraction.Ignore);
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
            Transform root = GetRoot(m);
            if (root == null) return;

            CrawlGraph g = m.Graph;
            int current = CurrentNode(m, root.position);
            if (current < 0) { _pauseTimer = 1f; return; }
            int cluster = g.NodeCluster[current];
            int pick = g.RandomClusterNear(root.position, wanderRadius, g.ClusterComponent[cluster], cluster);
            if (pick < 0) pick = cluster;
            RequestPath(g.RandomNodeInCluster(pick));
            _pauseTimer = Random.Range(wanderPause.x, wanderPause.y);
        }

        public void RequestPathToWorld(EnemyManager m, Vector3 worldPoint)
        {
            int goal = m.ResolveNode(worldPoint, true);
            if (goal >= 0 && (goal != _goalNode || PathDone)) RequestPath(goal);
        }

        public void ForcePathToWorld(EnemyManager m, Vector3 worldPoint)
        {
            int goal = m.ResolveNode(worldPoint, true);
            if (goal >= 0) RequestPath(goal);
        }

        public void RequestRetreat(EnemyManager m, Vector3 awayDirection, float travelDistance)
        {
            if (_escapeTimer > 0f || m == null || m.Graph == null) return;
            Transform root = GetRoot(m);
            if (root == null) return;
            int start = CurrentNode(m, root.position);
            if (start < 0) return;
            int goal = m.Graph.FindRetreatNode(start, root.position, awayDirection, travelDistance, false, _retreatVisited);
            for (int i = 0; goal < 0 && i < 3; i++)
            {
                float angle = i == 0 ? 90f : i == 1 ? -90f : 180f;
                Vector3 alternate = Quaternion.AngleAxis(angle, _surfaceUp) * awayDirection;
                goal = m.Graph.FindRetreatNode(start, root.position, alternate, travelDistance, false, _retreatVisited);
            }
            if (goal >= 0) RequestPath(goal);
        }

        public void RunPathRequest(EnemyManager m)
        {
            Transform root = GetRoot(m);
            if (root == null) { ClearPath(); return; }

            // A replacement never writes into the route currently used by TickMove.
            bool continuing = m.HasPathRequest(this, _pathVersion);
            int start = continuing ? -1 : CurrentNode(m, root.position);
            if ((!continuing && start < 0) || _goalNode < 0) { ClearPath(); return; }
            var status = m.StepPathRequest(this, _pathVersion, start, _goalNode, _pendingPath,
                out int count, out bool complete, queuedFrame: _pathQueuedFrame);
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
            // Check before every path/arrival/boundary early return. Turning or
            // changing paths is not progress out of a blocked corner.
            if (UpdateStuck(enemy, m, dt)) return;

            // continue partial (corridor-limited or capped) paths before they run out
            if (!_wantsPath && _goalNode >= 0 && _pathCount > 0 && !_pathComplete && _pathIndex >= _pathCount - 2)
                RequestPath(_goalNode);

            CrawlGraph g = m.Graph;
            Transform root = enemy.Root;
            Vector3 pos = root.position;
            Vector3 target;
            Vector3 targetUp = _surfaceUp;
            float speed = enemy.CurrentState == Enemy.State.Retreat ? enemy.RetreatSpeed
                : enemy.CurrentState == Enemy.State.Wander ? walkSpeed * enemy.IdleSpeedMultiplier : chaseSpeed;
            LayerMask mask = m.SurfaceMask;

            Vector3 toSeen = enemy.LastSeenPos - pos;
            bool nearbyTarget = enemy.CurrentState == Enemy.State.Chase && enemy.Seen
                && toSeen.sqrMagnitude < enemy.DirectChaseDistance * enemy.DirectChaseDistance;
            int chaseNode = nearbyTarget ? m.ResolveNode(enemy.LastSeenPos) : -1;
            // Projection onto a wall can erase the entire distance to a player
            // standing out on the floor. Direct chase is only a same-face
            // shortcut; changing surfaces must follow the crawl graph.
            bool sameSurface = chaseNode >= 0
                && Vector3.Dot(g.GetNormal(chaseNode), _surfaceUp) > 0.95f
                && Mathf.Abs(Vector3.Dot(g.GetPosition(chaseNode) - pos, _surfaceUp)) < arriveRadius;
            bool direct = enemy.CurrentState == Enemy.State.Chase && enemy.Seen
                && nearbyTarget && sameSurface
                && !SweepSurface(pos, Vector3.ProjectOnPlane(toSeen, _surfaceUp), mask, out _);

            if (direct)
            {
                Vector3 flat = Vector3.ProjectOnPlane(toSeen, _surfaceUp);
                if (flat.sqrMagnitude < enemy.StopDistance * enemy.StopDistance)
                {
                    enemy.SetRootPose(pos, TurnTowards(root, flat, _up, dt));
                    return;
                }
                Vector3 dir = flat.normalized;
                target = pos + dir * (speed * dt * 2f);
            }
            else if (_pathIndex < _pathCount)
            {
                int node = _path[_pathIndex];
                target = g.GetPosition(node);
                if ((target - pos).sqrMagnitude < arriveRadius * arriveRadius)
                {
                    _currentNode = node;
                    _pathIndex++;
                    if (_pathIndex >= _pathCount) return;
                    node = _path[_pathIndex];
                    target = g.GetPosition(node);
                }

                targetUp = g.GetNormal(node);
                float nodeDistance = Vector3.Distance(pos, target);
                if (node != _progressNode || nodeDistance < _bestNodeDistance - 0.1f)
                {
                    _progressNode = node;
                    _bestNodeDistance = nodeDistance;
                    _nodeProgressTimer = 0f;
                }
                else _nodeProgressTimer += dt;
                // Circling is movement, but not navigation progress. Do not
                // let the displacement watchdog keep resetting indefinitely.
                if (_nodeProgressTimer >= 5f)
                {
                    _nodeProgressTimer = 0f;
                    _stuckPos = pos;
                    _stuckTimer = 3f;
                    _edgeDirection = Vector3.zero;
                    UpdateStuck(enemy, m, dt);
                    return;
                }
            }
            else
            {
                return;
            }

            Vector3 delta = target - pos;
            float dist = delta.magnitude;
            if (dist < 1e-5f) return;
            Vector3 previousSurfaceUp = _surfaceUp;
            _webDeparture = false;
            _webContact = true;
            _webOrigin = pos;
            Vector3 next;
            if (!TryStepUp(pos, target, speed * dt, mask, g, out next))
                next = MoveOnSurface(pos, target, targetUp, speed * dt, mask);
            if (!g.ContainsCrawlSegment(pos, next))
            {
                _surfaceUp = previousSurfaceUp;
                ClearPath();
                RequestWander(m);
                return;
            }
            pos = next;

            ApplyMovementPose(enemy, next, previousSurfaceUp, dt, turnSpeed);

        }

        public void SyncUpFromRoot(Transform root)
        {
            if (_web != null) _web.Cancel();
            _edgeDirection = Vector3.zero;
            _progressNode = -1;
            if (root != null) _up = _surfaceUp = root.up;
            _stuckPos = root != null ? root.position : _stuckPos;
            _stuckTimer = 0f;
            _escapeTimer = 0f;
            _travelHeading = root != null ? root.forward : Vector3.forward;
            _pendingHeading = Vector3.zero;
            _headingTimer = 0f;
        }

        void ApplyMovementPose(Enemy enemy, Vector3 next, Vector3 previousNormal, float dt, float turnLimit)
        {
            Transform root = enemy.Root;
            if (!enemy.AllowsFlareMovement(root.position, next))
            {
                _surfaceUp = previousNormal;
                _stuckTimer = 0f;
                _escapeTimer = 0f;
                return;
            }
            _up = Vector3.Slerp(_up, _surfaceUp, 1f - Mathf.Exp(-upSmooth * dt)).normalized;
            // Contact changes move the surface root around the collision sphere.
            // That offset is not walking velocity and must not flip the heading.
            float clearance = WorldRadius() + CollisionSkin;
            Vector3 travel = next + _surfaceUp * clearance - (root.position + previousNormal * clearance);
            Vector3 heading = Vector3.ProjectOnPlane(_travelHeading, _up).normalized;
            if (heading.sqrMagnitude < 0.01f) heading = Vector3.ProjectOnPlane(root.forward, _up).normalized;
            Vector3 direction = Vector3.ProjectOnPlane(travel, _up);
            if (direction.sqrMagnitude > dt * dt * 0.04f)
            {
                direction.Normalize();
                bool reversing = Vector3.Dot(direction, heading) < 0f;
                if (reversing)
                {
                    if (Vector3.Dot(direction, _pendingHeading) < 0.8f) _headingTimer = 0f;
                    _pendingHeading = direction;
                    _headingTimer += dt;
                }
                else { _headingTimer = 0f; _pendingHeading = Vector3.zero; }
                // Ignore brief backsteps from alternating mesh contacts. A real
                // sustained reversal still takes effect after 0.18 seconds.
                if (!reversing || _headingTimer >= 0.18f)
                {
                    heading = Vector3.RotateTowards(heading, direction, turnLimit * Mathf.Deg2Rad * dt, 0f);
                }
            }
            else { _headingTimer = 0f; _pendingHeading = Vector3.zero; }
            if (heading.sqrMagnitude > 0.01f) _travelHeading = heading;
            Quaternion rotation = heading.sqrMagnitude > 0.01f
                ? Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(heading, _up), turnLimit * dt)
                : root.rotation;
            enemy.SetRootPose(next, rotation);
            if (_web != null) _web.ObserveMove(_webDeparture, _webContact, _webOrigin, next);
        }

        void RequestPath(int goal)
        {
            if (_escapeTimer > 0f) return;
            _pathVersion++;
            if (!_wantsPath) _pathQueuedFrame = Time.frameCount;
            _goalNode = goal;
            _wantsPath = goal >= 0;
        }

        float WorldRadius()
        {
            Vector3 scale = transform.lossyScale;
            return collisionRadius * Mathf.Max(Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)), Mathf.Abs(scale.z));
        }

        bool TryStepUp(Vector3 pos, Vector3 target, float distance, LayerMask mask, CrawlGraph graph, out Vector3 next)
        {
            next = pos;
            if (maxStepHeight <= 0f || distance <= 0f || _surfaceUp.y < CrawlGraph.FloorMinY) return false;
            Vector3 flat = Vector3.ProjectOnPlane(target - pos, Vector3.up);
            if (flat.sqrMagnitude < 1e-6f) return false;
            Vector3 direction = flat.normalized;
            float radius = WorldRadius();
            float clearance = radius + CollisionSkin;
            Vector3 center = pos + _surfaceUp * clearance;
            // Only intervene at a blocking riser, not during ordinary surface travel.
            if (!Physics.SphereCast(center, radius, direction, out RaycastHit obstacle,
                distance + CollisionSkin, mask, QueryTriggerInteraction.Ignore)
                || obstacle.normal.y >= CrawlGraph.FloorMinY) return false;

            // Probe far enough over the lip to support the whole collision sphere.
            float forward = Mathf.Min(flat.magnitude, obstacle.distance + radius * 2f + CollisionSkin);
            Vector3 raised = center + Vector3.up * (maxStepHeight + CollisionSkin);
            Vector3 over = raised + direction * forward;
            if (Physics.SphereCast(center, radius, Vector3.up, out _, maxStepHeight + CollisionSkin,
                    mask, QueryTriggerInteraction.Ignore)
                || Physics.SphereCast(raised, radius, direction, out _, forward,
                    mask, QueryTriggerInteraction.Ignore)) return false;
            if (!Physics.SphereCast(over, radius, Vector3.down, out RaycastHit landing,
                maxStepHeight + clearance, mask, QueryTriggerInteraction.Ignore)
                || landing.normal.y < CrawlGraph.FloorMinY) return false;

            Vector3 landedCenter = over + Vector3.down * landing.distance + landing.normal * CollisionSkin;
            Vector3 landedRoot = landedCenter - landing.normal * clearance;
            float rise = landedRoot.y - pos.y;
            if (rise <= CollisionSkin || rise > maxStepHeight
                || Physics.CheckSphere(landedCenter, radius, mask, QueryTriggerInteraction.Ignore)
                || !graph.ContainsCrawlSegment(pos, raised - _surfaceUp * clearance)
                || !graph.ContainsCrawlSegment(raised - _surfaceUp * clearance, over - landing.normal * clearance)
                || !graph.ContainsCrawlSegment(over - landing.normal * clearance, landedRoot)) return false;

            _surfaceUp = landing.normal;
            _edgeDirection = Vector3.zero;
            next = landedRoot;
            return true;
        }

        bool SweepSurface(Vector3 pos, Vector3 delta, LayerMask mask, out RaycastHit hit)
        {
            float radius = WorldRadius();
            hit = default;
            return delta.sqrMagnitude > 1e-8f && Physics.SphereCast(
                pos + _surfaceUp * (radius + CollisionSkin), radius, delta.normalized,
                out hit, delta.magnitude + CollisionSkin, mask, QueryTriggerInteraction.Ignore);
        }

        bool WallToTop(Vector3 targetNormal)
        {
            return Mathf.Abs(_surfaceUp.y) < CrawlGraph.FloorMinY && targetNormal.y >= CrawlGraph.FloorMinY;
        }

        // Climb the wall toward an up-facing node. Lateral motion stays within the
        // climb so an inset roof goal cannot turn the body along the roofline.
        bool TryCrestOntoTop(Vector3 pos, Vector3 target, Vector3 targetNormal, Vector3 tangent, float distance, out Vector3 delta)
        {
            delta = default;
            if (!WallToTop(targetNormal)) return false;
            Vector3 crest = Vector3.ProjectOnPlane(Vector3.up, _surfaceUp);
            if (crest.sqrMagnitude < 0.01f) return false;
            crest.Normalize();
            if (Vector3.Dot(target - pos, crest) <= 0.02f) return false;

            float along = Vector3.Dot(tangent, crest);
            float climb = along > distance ? along : distance;
            Vector3 lateral = tangent - crest * along;
            Vector3 steer = crest * climb + Vector3.ClampMagnitude(lateral, climb);
            if (steer.sqrMagnitude < 0.0025f) return false;
            delta = steer.normalized * Mathf.Max(distance, steer.magnitude);
            return true;
        }

        // Snap onto the roof once the side face ends within a body radius.
        // A sweep through the corner keeps overlapping the wall and sticks there.
        bool TryMountOntoTop(Vector3 pos, Vector3 target, Vector3 targetNormal, LayerMask mask, out Vector3 next)
        {
            next = pos;
            if (!WallToTop(targetNormal)) return false;

            float radius = WorldRadius();
            float clearance = radius + CollisionSkin;
            Vector3 crest = Vector3.ProjectOnPlane(Vector3.up, _surfaceUp);
            if (crest.sqrMagnitude < 0.01f) return false;
            crest.Normalize();

            Vector3 center = pos + _surfaceUp * clearance;
            if (Physics.Raycast(center + crest * radius, -_surfaceUp, clearance + 0.08f, mask, QueryTriggerInteraction.Ignore))
                return false;

            float cast = radius + snapProbe;
            Vector3 probe = center - _surfaceUp * (clearance + 0.05f) + crest * radius;
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, cast, mask, QueryTriggerInteraction.Ignore))
                return false;
            if (!RoofHit(hit, target, targetNormal, radius)) return false;

            // The probe only clears the corner. Sit the body far enough onto the
            // top that its sphere no longer overlaps the wall it just left.
            Vector3 inward = Vector3.ProjectOnPlane(-_surfaceUp, hit.normal);
            if (inward.sqrMagnitude < 0.01f) return false;
            inward.Normalize();
            Vector3 confirmFrom = hit.point + inward * (radius + CollisionSkin) + hit.normal * cast;
            if (!Physics.Raycast(confirmFrom, -hit.normal, out RaycastHit seated, cast + radius, mask, QueryTriggerInteraction.Ignore))
                return false;
            if (!RoofHit(seated, target, targetNormal, radius)) return false;

            Vector3 landedCenter = seated.point + seated.normal * clearance;
            Vector3 landedRoot = landedCenter - seated.normal * clearance;
            if (Vector3.Distance(pos, landedRoot) > radius * 3f
                || Physics.CheckSphere(landedCenter, radius, mask, QueryTriggerInteraction.Ignore))
                return false;

            _surfaceUp = seated.normal;
            _edgeDirection = Vector3.zero;
            next = landedRoot;
            return true;
        }

        static bool RoofHit(RaycastHit hit, Vector3 target, Vector3 targetNormal, float radius)
        {
            return hit.normal.y >= CrawlGraph.FloorMinY
                && Vector3.Dot(hit.normal, targetNormal) >= 0.7f
                && Mathf.Abs(Vector3.Dot(hit.point - target, targetNormal)) <= radius;
        }

        // Drop onto a lower floor once this face ends within a body radius.
        // The down-cast otherwise hits the upper corner and the body stays on the lip.
        bool TryStepDown(Vector3 pos, Vector3 target, Vector3 targetNormal, LayerMask mask, out Vector3 next)
        {
            next = pos;
            if (maxStepHeight <= 0f) return false;
            if (_surfaceUp.y < CrawlGraph.FloorMinY || targetNormal.y < CrawlGraph.FloorMinY) return false;
            float dropToTarget = Vector3.Dot(pos - target, targetNormal);
            if (dropToTarget <= CollisionSkin || dropToTarget > maxStepHeight) return false;

            Vector3 flat = Vector3.ProjectOnPlane(target - pos, _surfaceUp);
            if (flat.sqrMagnitude < 1e-6f) return false;
            Vector3 forward = flat.normalized;
            float radius = WorldRadius();
            float clearance = radius + CollisionSkin;
            Vector3 center = pos + _surfaceUp * clearance;
            if (Physics.Raycast(center + forward * radius, -_surfaceUp, out RaycastHit still,
                    clearance + maxStepHeight, mask, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(still.normal, _surfaceUp) > 0.8f
                && Vector3.Dot(pos - still.point, _surfaceUp) <= CollisionSkin)
                return false;

            float cast = maxStepHeight + clearance;
            Vector3 probe = center + forward * (radius + CollisionSkin);
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit hit, cast, mask, QueryTriggerInteraction.Ignore))
                return false;
            if (!RoofHit(hit, target, targetNormal, radius) || !WithinStep(pos, hit.point)) return false;

            Vector3 ontoFloor = Vector3.ProjectOnPlane(forward, hit.normal);
            if (ontoFloor.sqrMagnitude < 0.01f) return false;
            ontoFloor.Normalize();
            Vector3 confirmFrom = hit.point + ontoFloor * (radius + CollisionSkin) + hit.normal * cast;
            if (!Physics.Raycast(confirmFrom, -hit.normal, out RaycastHit seated, cast + radius, mask, QueryTriggerInteraction.Ignore))
                return false;
            if (!RoofHit(seated, target, targetNormal, radius) || !WithinStep(pos, seated.point)) return false;

            Vector3 landedCenter = seated.point + seated.normal * clearance;
            Vector3 landedRoot = landedCenter - seated.normal * clearance;
            if (Vector3.Distance(pos, landedRoot) > radius * 3f
                || Physics.CheckSphere(landedCenter, radius, mask, QueryTriggerInteraction.Ignore))
                return false;

            _surfaceUp = seated.normal;
            _edgeDirection = Vector3.zero;
            next = landedRoot;
            return true;
        }

        bool WithinStep(Vector3 pos, Vector3 point)
        {
            float fall = Vector3.Dot(pos - point, Vector3.up);
            return fall > CollisionSkin && fall <= maxStepHeight;
        }

        Vector3 MoveOnSurface(Vector3 pos, Vector3 target, Vector3 targetNormal, float distance, LayerMask mask)
        {
            _webDeparture = _webContact = false;
            _webOrigin = pos;
            // Bound every sweep/adhesion transition, including low-frame-rate
            // movement and recovery. Never skip across a thin intermediate face.
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / 0.05f), 1, 128);
            float stepDistance = Mathf.Min(distance / steps, 0.05f);
            for (int step = 0; step < steps; step++)
            {
                // A short drop is one landing. Sweeping it lets the body sphere
                // catch the upper corner and stick there.
                if (TryStepDown(pos, target, targetNormal, mask, out Vector3 stepped))
                {
                    _webContact = true;
                    return stepped;
                }
                // The convex roof lip is one committed landing. Slicing it into
                // sweeps lets the body sphere re-stick to the wall.
                if (TryMountOntoTop(pos, target, targetNormal, mask, out Vector3 mounted))
                {
                    _webContact = true;
                    return mounted;
                }
                Vector3 next = MoveSurfaceStep(pos, target, targetNormal, stepDistance, mask);
                if ((next - pos).sqrMagnitude < 1e-10f) break;
                pos = next;
            }
            return pos;
        }

        Vector3 MoveSurfaceStep(Vector3 pos, Vector3 target, Vector3 targetNormal, float distance, LayerMask mask)
        {
            float radius = WorldRadius();
            float clearance = radius + CollisionSkin;
            // Sweep a body volume through free space. The root is reconstructed
            // from the actual contact normal, never an interpolated visual normal.
            Vector3 center = pos + _surfaceUp * clearance;
            Vector3 delta = target + targetNormal * clearance - center;
            bool leavingUnderside = Vector3.Dot(targetNormal, _surfaceUp) < -0.5f
                && Vector3.Dot(delta, _surfaceUp) > 0.05f;
            _webDeparture |= leavingUnderside && _surfaceUp.y < -0.5f;
            // A goal on another face can point into the shelf (or straight
            // away from it, where adhesion cancels every step). Travel along
            // the current face to its edge first; then the normal attachment
            // probes below can carry us around the lip onto the next face.
            if (!leavingUnderside
                && (Vector3.Dot(targetNormal, _surfaceUp) < 0.95f || Vector3.Dot(delta, _surfaceUp) < -0.05f)
                && Physics.Raycast(center, -_surfaceUp, out RaycastHit support,
                    clearance + 0.03f, mask, QueryTriggerInteraction.Ignore)
                && Vector3.Dot(support.normal, _surfaceUp) > 0.8f)
            {
                Vector3 tangent = Vector3.ProjectOnPlane(delta, _surfaceUp);
                // A roof node sits inward of the wall, which looks like the shelf
                // case below and freezes a sideways heading. Climb to the lip instead.
                if (TryCrestOntoTop(pos, target, targetNormal, tangent, distance, out Vector3 ontoTop))
                {
                    _edgeDirection = Vector3.zero;
                    delta = ontoTop;
                }
                else
                {
                    // For a goal directly underneath a shelf, steering back toward
                    // its projection reverses every step. Keep heading to the edge
                    // until the supporting face ends instead.
                    if (Vector3.Dot(delta, _surfaceUp) < -tangent.magnitude)
                    {
                        // Never feed the turning visual forward back into edge
                        // steering each frame: that creates a circular trajectory.
                        if (_edgeDirection.sqrMagnitude < 0.01f || Vector3.Dot(_edgeNormal, _surfaceUp) < 0.9f)
                        {
                            _edgeNormal = _surfaceUp;
                            _edgeDirection = tangent.normalized;
                            if (_edgeDirection.sqrMagnitude < 0.01f)
                                _edgeDirection = Vector3.ProjectOnPlane(Vector3.down, _surfaceUp).normalized;
                            if (_edgeDirection.sqrMagnitude < 0.01f)
                                _edgeDirection = Vector3.ProjectOnPlane(transform.forward, _surfaceUp).normalized;
                        }
                        tangent = _edgeDirection;
                    }
                    else _edgeDirection = Vector3.zero;
                    if (tangent.sqrMagnitude < 0.0025f)
                        tangent = Vector3.ProjectOnPlane(Vector3.down, _surfaceUp);
                    if (tangent.sqrMagnitude < 0.0025f)
                        tangent = Vector3.ProjectOnPlane(transform.forward, _surfaceUp);
                    if (tangent.sqrMagnitude > 0.0025f)
                        delta = tangent.normalized * Mathf.Max(distance, tangent.magnitude);
                }
            }
            Vector3 remaining = Vector3.ClampMagnitude(delta, distance);
            for (int i = 0; i < 5 && remaining.sqrMagnitude > 1e-8f; i++)
            {
                if (!Physics.SphereCast(center, radius, remaining.normalized, out RaycastHit hit,
                    remaining.magnitude + CollisionSkin, mask, QueryTriggerInteraction.Ignore))
                {
                    center += remaining;
                    break;
                }
                Vector3 direction = remaining.normalized;
                float travel = Mathf.Clamp(hit.distance - CollisionSkin, 0f, remaining.magnitude);
                center += direction * travel;
                remaining = Vector3.ProjectOnPlane(remaining - direction * travel, hit.normal);
                _surfaceUp = hit.normal;
            }

            // A narrow seam or thin fence edge may briefly have no support.
            // Keep the swept movement instead of undoing it and rejecting routes.
            bool attached = TryAttach(ref center, targetNormal, radius, mask);
            if (!attached && !leavingUnderside)
                attached = TryAttach(ref center, _surfaceUp, radius, mask);
            _webContact = attached;
            return center - _surfaceUp * clearance;
        }

        bool TryAttach(ref Vector3 center, Vector3 normal, float radius, LayerMask mask)
        {
            if (!Physics.SphereCast(center, radius, -normal, out RaycastHit hit,
                snapProbe + CollisionSkin, mask, QueryTriggerInteraction.Ignore)) return false;
            float skin = CollisionSkin / Mathf.Max(0.01f, Vector3.Dot(hit.normal, normal));
            center -= normal * Mathf.Max(0f, hit.distance - skin);
            _surfaceUp = hit.normal;
            return true;
        }

        int CurrentNode(EnemyManager m, Vector3 position)
        {
            int n = m.Graph.FindNearestNode(position, 1.5f);
            _currentNode = n;
            return _currentNode;
        }

        Transform GetRoot(EnemyManager m)
        {
            Enemy enemy = GetComponent<Enemy>();
            return enemy != null ? enemy.Root : transform;
        }

        Quaternion TurnTowards(Transform root, Vector3 flatDir, Vector3 up, float dt)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return root.rotation;
            return Quaternion.RotateTowards(root.rotation, Quaternion.LookRotation(flatDir.normalized, up), turnSpeed * dt);
        }

        bool UpdateStuck(Enemy enemy, EnemyManager m, float dt)
        {
            Vector3 pos = enemy.Root.position;
            float recoverySpeed = enemy.CurrentState == Enemy.State.Chase && enemy.Seen
                ? chaseSpeed : Mathf.Min(walkSpeed, 1.5f);
            if (_escapeTimer > 0f)
            {
                _escapeTimer -= dt;
                Vector3 oldNormal = _surfaceUp;
                Vector3 next = MoveOnSurface(pos, _escapeTarget, _escapeNormal,
                    recoverySpeed * dt, m.SurfaceMask);
                if (!m.Graph.ContainsCrawlSegment(pos, next))
                { next = pos; _surfaceUp = oldNormal; _escapeTimer = 0f; }
                ApplyMovementPose(enemy, next, oldNormal, dt, 90f);
                if (_escapeTimer <= 0f || (next - _escapeTarget).sqrMagnitude < 0.04f)
                {
                    _escapeTimer = 0f;
                    _stuckPos = next;
                    _stuckTimer = 0f;
                    if (enemy.CurrentState == Enemy.State.Chase)
                        RequestPathToWorld(m, enemy.LastSeenPos);
                    else RequestWander(m);
                }
                return true;
            }
            if (enemy.CurrentState == Enemy.State.Chase && enemy.Seen
                && (enemy.LastSeenPos - pos).sqrMagnitude < enemy.StopDistance * enemy.StopDistance)
            { _stuckPos = pos; _stuckTimer = 0f; return false; }
            if ((pos - _stuckPos).sqrMagnitude > 0.25f)
            { _stuckPos = pos; _stuckTimer = 0f; }
            _stuckTimer += dt;
            if (_stuckTimer < 3f) return false;
            ClearPath();
            // Furniture can be an isolated patch of the crawl graph. A short
            // sideways step just chooses another point on the same shelf.
            // Look outside its lip for floor and actually crawl there; the
            // usual movement sweep and level bounds still apply every frame.
            Vector3 probe = pos + _surfaceUp * (WorldRadius() + snapProbe + 0.05f);
            if (Physics.Raycast(probe, Vector3.down, out RaycastHit floor, 8f,
                    m.SurfaceMask, QueryTriggerInteraction.Ignore)
                && floor.normal.y > 0.65f && (floor.point - pos).sqrMagnitude > 0.25f
                && m.Graph.ContainsCrawlPosition(floor.point))
            {
                _escapeTarget = floor.point;
                _escapeNormal = floor.normal;
                _escapeTimer = 1f + Vector3.Distance(pos, floor.point) / Mathf.Max(0.1f, recoverySpeed);
                _edgeDirection = Vector3.zero;
                return true;
            }
            // One short, committed back/side step, using the normal mover.
            // No candidate simulations or permanent route exclusions.
            Vector3 back = Vector3.ProjectOnPlane(-enemy.Root.forward, _surfaceUp).normalized;
            if (back.sqrMagnitude < 0.01f) back = Vector3.Cross(_surfaceUp, Vector3.right).normalized;
            if (back.sqrMagnitude < 0.01f) back = Vector3.Cross(_surfaceUp, Vector3.forward).normalized;
            Vector3 direction = Quaternion.AngleAxis((_escapeAttempt++ % 3 - 1) * 90f, _surfaceUp) * back;
            _escapeTarget = pos + direction;
            _escapeNormal = _surfaceUp;
            _escapeTimer = 1.5f;
            return true;
        }

#if UNITY_EDITOR
        public void DrawPathGizmos()
        {
            if (EnemyManager.Instance == null || EnemyManager.Instance.Graph == null) return;
            CrawlGraph g = EnemyManager.Instance.Graph;
            Gizmos.color = Color.cyan;
            for (int i = Mathf.Max(1, _pathIndex); i < _pathCount; i++)
                Gizmos.DrawLine(g.GetPosition(_path[i - 1]), g.GetPosition(_path[i]));
        }
#endif
    }
}
