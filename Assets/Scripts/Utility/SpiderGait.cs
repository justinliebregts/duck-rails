using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// Event-driven adaptive spider (quadruped) gait.
    /// Reads: root (authoritative driver — locomotion or network interpolation).
    /// Writes: body visual pose + foot plant/swing targets into SpiderLegs.
    /// Legs stay dumb solvers; ALL policy lives here.
    /// </summary>
    public class SpiderGait : MonoBehaviour, IGaitStats, IGaitDriver
    {
        public event System.Action<Vector3, Vector3> FootPlanted;
        [System.Serializable]
        public class LegSlot
        {
            public SpiderLeg leg;
            public Vector3 homeOffset;                 // body-local XZ rest position (Y ignored)

            [HideInInspector] public Vector3 home;
            [HideInInspector] public Vector3 homeNormal = Vector3.up;
            [HideInInspector] public Vector3 planted;
            [HideInInspector] public Vector3 plantedNormal = Vector3.up;
            [HideInInspector] public Vector3 swingFrom, swingFromNormal;
            [HideInInspector] public Vector3 swingTo, swingToNormal;
            [HideInInspector] public float swingT, swingDuration = 0.18f, swingHeight;
            [HideInInspector] public bool swinging;
            [HideInInspector] public float error;
        }

        [Header("Rig")]
        [SerializeField] public Transform root;               // authoritative: locomotion/network moves this
        [SerializeField] Transform body;               // visual hips; MUST be child of root
        [SerializeField] LegSlot[] legs = new LegSlot[4]; // 0 FL, 1 FR, 2 RL, 3 RR

        [Header("Surface")]
        [SerializeField] LayerMask surfaceMask;
        [SerializeField] float probeLift = 0.4f;       // along current up
        [SerializeField] float probeDistance = 1.0f;
        [SerializeField] float probeRadius = 0.15f;    // sphere-cast: stable normals on bumps

        [Header("Body")]
        [SerializeField] float rideHeight = 0.35f;
        [SerializeField] float posSmooth = 12f;        // 1/s
#pragma warning disable CS0414 // kept so existing prefabs retain the value; root owns up
        [SerializeField] float upSmooth = 10f;
#pragma warning restore CS0414
        [SerializeField] float leanGain = 2.5f;       // deg per m/s² — flip signs to taste
        [SerializeField] float maxLeanDeg = 10f;
        [SerializeField] float teleportDistance = 2f;  // root jump ⇒ replant all

        [Header("Visual head clearance")]
        [SerializeField, Min(0f)] float headClearanceRadius = 0.42f;
        [SerializeField, Min(0f)] float maxHeadClearanceOffset = 0.65f;
        readonly Collider[] _headObstacles = new Collider[32];
        SphereCollider _headProbe;
        Collider _scarCandidate;
        Vector3 _scarPlanePoint, _scarPlaneNormal;
        bool _scarPlaced;
        Vector3 _scarPoint;
        SpiderScarPool _scarPool;
        Game.AI.EnemyNetSync _scarNetwork;
        float _nextScarProbe;

        [Header("Stepping")]
        [SerializeField] float stepTrigger = 0.20f;   // error that starts a step
        [SerializeField] float triggerSpeedScale = 0.35f;
        [SerializeField] float maxTrigger = 0.55f;
        [SerializeField] float forcedTrigger = 0.85f;  // pattern-breaking recovery step
        [SerializeField] float swingDuration = 0.18f;
        [SerializeField] float swingDurationFast = 0.11f;
        [SerializeField] float runSpeed = 2.2f;        // walk (1 leg) → trot (diagonal pair)
        [SerializeField] float stepHeight = 0.10f;
        [SerializeField] float stepHeightSpeedScale = 0.04f;
        [SerializeField] float maxStepHeight = 0.22f;
        [SerializeField, Range(0f, 1f)] float plantLead = 0.65f;
        [SerializeField, Range(0.7f, 1f)] float reachUse = 0.92f;

        [Header("Ticking")]
        [SerializeField] bool externalTick;          // true when EnemyManager calls Tick()

        Vector3 _vel, _accel, _prevVel, _bodyPos, _lastRootPos;
        Vector3 _up = Vector3.up;
        bool _init;
        float _pitch, _roll;

        public bool ExternalTick { get => externalTick; set => externalTick = value; }

        void Awake()
        {
            _scarNetwork = GetComponent<Game.AI.EnemyNetSync>();
            if (Application.isPlaying) _scarPool = SpiderScarPool.Get();
            // Imported animation bounds do not cover procedural wall/ceiling
            // poses. Recalculate the skinned bounds even after leaving view.
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                renderer.updateWhenOffscreen = true;
        }

        // Called after head aiming: animation can extend beyond the locomotion
        // volume even though the authoritative root never crosses the wall.
        public void ResolveHeadClearance(Transform head)
        {
            // The shipping spider deliberately disables clearance. Cosmetic detection
            // must still run without enabling any of the movement corrections below.
            if (headClearanceRadius <= 0f)
            {
                ObserveUncorrectedScar(head);
                return;
            }
            if (head == null || body == null || root == null || !head.IsChildOf(body)
                || headClearanceRadius <= 0f) return;
            if (_headProbe == null)
            {
                var probe = new GameObject("Spider head clearance probe") { hideFlags = HideFlags.HideAndDontSave };
                probe.layer = 2; // Ignore Raycast
                _headProbe = probe.AddComponent<SphereCollider>();
                _headProbe.isTrigger = true;
                _headProbe.excludeLayers = ~0; // query shape only; no collision/trigger contacts
            }
            Vector3 scale = head.lossyScale;
            _headProbe.radius = headClearanceRadius * Mathf.Max(Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y)), Mathf.Abs(scale.z));
            _headProbe.transform.position = head.position;
            var climb = root.GetComponent<Game.AI.ClimbLocomotion>();
            Vector3 supportUp = climb != null ? climb.SurfaceNormal : root.up;
            Vector3 anchor = root.position + supportUp * rideHeight;
            Vector3 rootScale = root.lossyScale;
            float maxOffset = maxHeadClearanceOffset * Mathf.Max(Mathf.Abs(rootScale.x),
                Mathf.Abs(rootScale.y), Mathf.Abs(rootScale.z));
            Vector3 initialPosition = body.position;
            _scarCandidate = null;
            body.position = LimitHeadClearance(body.position, anchor, supportUp, maxOffset);
            KeepRigOutsideSupport(head, supportUp, _headProbe.radius, anchor, maxOffset);
            // A penetration query alone cannot see a head that has already
            // passed entirely through a thin wall. Sweep from the supported
            // side to the animated body/head, including animation lag/turns.
            SweepVisualPoint(body, Mathf.Min(_headProbe.radius, 0.3f), supportUp);
            SweepVisualPoint(head, _headProbe.radius, supportUp);
            KeepRigOutsideSupport(head, supportUp, _headProbe.radius, anchor, maxOffset);
            bool corrected = body.position != initialPosition;
            for (int pass = 0; pass < 4; pass++)
            {
                int count = Physics.OverlapSphereNonAlloc(head.position, _headProbe.radius,
                    _headObstacles, surfaceMask, QueryTriggerInteraction.Ignore);
                bool overlap = false;
                for (int i = 0; i < count; i++)
                {
                    Collider obstacle = _headObstacles[i];
                    if (obstacle.transform.IsChildOf(root)) continue;
                    if (!Physics.ComputePenetration(_headProbe, head.position, Quaternion.identity,
                        obstacle, obstacle.transform.position, obstacle.transform.rotation,
                        out Vector3 direction, out float depth)) continue;
                    _scarCandidate = obstacle;
                    _scarPlaneNormal = direction;
                    _scarPlanePoint = head.position - direction * (_headProbe.radius - depth);
                    // Conflicting contacts must never eject the visual rig or
                    // push it behind its supporting surface. The root remains
                    // the authority even when there is no room for the head.
                    Vector3 candidate = body.position + direction * (depth + 0.01f);
                    body.position = LimitHeadClearance(candidate, anchor, supportUp, maxOffset);
                    KeepRigOutsideSupport(head, supportUp, _headProbe.radius, anchor, maxOffset);
                    overlap = corrected = true;
                }
                if (!overlap) break;
            }
            if (corrected)
            {
                // Do not feed collision offsets back into gait smoothing:
                // repeated contacts would otherwise accumulate across frames.
                // Keep the planted feet anchored after correcting the visual body.
                SolveLegs(0f);
            }
            _headProbe.transform.position = head.position;
            // Observe only the final visual pose. Never feed scar results into movement.
            bool clearedSurface = Vector3.Dot(head.position - _scarPlanePoint, _scarPlaneNormal) >= _headProbe.radius - 0.01f
                && Vector3.Dot(root.position - _scarPlanePoint, _scarPlaneNormal) >= 0f;
            if (_scarCandidate == null || clearedSurface) _scarPlaced = false;
            else if (_scarPool != null && (!_scarPlaced || (head.position - _scarPoint).sqrMagnitude > 0.25f))
                _scarPool.Observe(this, _scarCandidate, root.position, head.position, _headProbe.radius);
        }

        public void ScarPlaced(Vector3 point) { _scarPlaced = true; _scarPoint = point; }

        public void ObserveKillScar(Vector3 victimPosition)
        {
            if (_scarPool != null && root != null)
            {
                _scarPool.ObserveKill(this, root.position, victimPosition, surfaceMask.value);
                if (_scarNetwork != null) _scarNetwork.SendScarKill(root.position, victimPosition);
            }
        }

        public void ReceiveKillScar(Vector3 spiderPosition, Vector3 victimPosition)
        {
            // Cosmetic snapshots only; receiving never changes gameplay or sends again.
            if (_scarPool != null)
                _scarPool.ObserveKill(this, spiderPosition, victimPosition, surfaceMask.value);
        }

        void ObserveUncorrectedScar(Transform head)
        {
            if (head == null || root == null || _scarPool == null || Time.time < _nextScarProbe) return;
            if (_scarPlaced && (head.position - _scarPoint).sqrMagnitude <= 0.25f) return;
            _nextScarProbe = Time.time + 0.1f;
            Vector3 scale = head.lossyScale;
            float radius = 0.42f * Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z));
            _scarPool.Observe(this, null, root.position, head.position, radius, surfaceMask.value);
        }

        void KeepRigOutsideSupport(Transform head, Vector3 normal, float headRadius, Vector3 anchor, float limit)
        {
            // The root is on the physical contact plane. Mesh overlap queries
            // ignore backfaces, and casts cannot detect an already-overlapping
            // start. Keep the animated rig on the root's side analytically,
            // including while its smoothed rotation still faces the old wall.
            float headDistance = Vector3.Dot(head.position - root.position, normal);
            float bodyDistance = Vector3.Dot(body.position - root.position, normal);
            float lift = Mathf.Max(headRadius + 0.02f - headDistance,
                Mathf.Min(headRadius, 0.3f) + 0.02f - bodyDistance);
            if (lift > 0f) body.position += normal * lift;
            Vector3 offset = body.position - anchor;
            float outward = Mathf.Clamp(Vector3.Dot(offset, normal), 0f, limit);
            Vector3 tangent = Vector3.ProjectOnPlane(offset, normal);
            float tangentLimit = Mathf.Sqrt(Mathf.Max(0f, limit * limit - outward * outward));
            body.position = anchor + normal * outward + Vector3.ClampMagnitude(tangent, tangentLimit);
        }

        void SweepVisualPoint(Transform point, float radius, Vector3 normal)
        {
            Vector3 start = root.position + normal * (radius + 0.02f);
            Vector3 delta = point.position - start;
            if (delta.sqrMagnitude < 1e-8f) return;
            if (Physics.SphereCast(start, radius, delta.normalized, out RaycastHit hit,
                delta.magnitude, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                _scarCandidate = hit.collider;
                _scarPlanePoint = hit.point;
                _scarPlaneNormal = hit.normal;
                body.position += start + delta.normalized * Mathf.Max(0f, hit.distance - 0.01f) - point.position;
            }
        }

        static Vector3 LimitHeadClearance(Vector3 position, Vector3 anchor, Vector3 up, float limit)
        {
            Vector3 offset = position - anchor;
            float inward = Vector3.Dot(offset, up);
            if (inward < 0f) offset -= up * inward;
            return anchor + Vector3.ClampMagnitude(offset, limit);
        }

        void OnDestroy()
        {
            if (_headProbe != null) Destroy(_headProbe.gameObject);
        }

        void LateUpdate()
        {
            if (!externalTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (root == null || body == null || legs == null || legs.Length < 4 || dt <= 0f) return;

            // --- init / teleport guard ---------------------------------------
            if (!_init || (root.position - _lastRootPos).sqrMagnitude > teleportDistance * teleportDistance)
            {
                _lastRootPos = root.position;
                _vel = _prevVel = _accel = Vector3.zero;
                _pitch = _roll = 0f;
                _up = root.up;
                _bodyPos = root.position + _up * rideHeight;
                foreach (var l in legs)
                {
                    l.swinging = false;
                    ProbeHome(l);
                    l.planted = l.home;
                    l.plantedNormal = l.homeNormal;
                }
                _init = true;

                /////DEBUG////
                // in Tick, before UpdateBody:
                _stepRateTimer += dt;
                if (_stepRateTimer >= 1f) { StepsPerSec = _stepWindow / _stepRateTimer; _stepWindow = 0; _stepRateTimer = 0f; }
                // at the very end of Tick:
                if (debugDraw) DebugDraw();
                /////////////////////////

                

                UpdateBody(dt);
                SolveLegs(dt);
                return;
            }

            // --- driver velocity (smoothed) ----------------------------------
            Vector3 raw = (root.position - _lastRootPos) / dt;
            _lastRootPos = root.position;
            _vel = Vector3.Lerp(_vel, raw, 1f - Mathf.Exp(-10f * dt));
            float speed = _vel.magnitude;

            // --- probe homes ---------------------------------------------------
            for (int i = 0; i < legs.Length; i++) ProbeHome(legs[i]);

            // --- step triggering ------------------------------------------------
            float trigger = Mathf.Min(stepTrigger + speed * triggerSpeedScale, maxTrigger);
            int maxSwinging = speed < runSpeed ? 1 : 2;

            for (int i = 0; i < legs.Length; i++)
            {
                var l = legs[i];
                if (l.swinging) continue;
                l.error = Vector3.Distance(l.planted, l.home);
                if (l.error > forcedTrigger)                    { BeginSwing(l, speed); continue; } // recovery
                if (l.error > trigger && CanStart(i, maxSwinging)) BeginSwing(l, speed);
            }

            UpdateBody(dt);
            SolveLegs(dt);
        }

        // ------------------------------------------------------------ internals

        void ProbeHome(LegSlot l)
        {
            Vector3 anchor = body.TransformPoint(l.homeOffset);
            if (Physics.SphereCast(anchor + _up * probeLift, probeRadius, -_up,
                out RaycastHit hit, probeLift + probeDistance, surfaceMask, QueryTriggerInteraction.Ignore))
            {
                l.home = hit.point;
                l.homeNormal = hit.normal.normalized;
            }
            else
            {
                l.home = anchor - _up * rideHeight;   // leg over a gap: dangle at assumed ground
                l.homeNormal = _up;
            }
        }

        bool CanStart(int i, int maxSwinging)
        {
            int swinging = 0;
            for (int j = 0; j < legs.Length; j++)
            {
                if (!legs[j].swinging) continue;
                if (j != i && Adjacent(i, j)) return false;
                swinging++;
            }
            return swinging < maxSwinging;
        }

        static bool Adjacent(int a, int b) => (a / 2 == b / 2) || (a % 2 == b % 2); // same row OR same side

        void BeginSwing(LegSlot l, float speed)
        {
            StepsTotal++; _stepWindow++; /////DEBUG

            l.swinging = true;
            l.swingT = 0f;
            l.swingFrom = l.planted;
            l.swingFromNormal = l.plantedNormal;
            l.swingDuration = Mathf.Lerp(swingDuration, swingDurationFast, Mathf.Clamp01(speed / runSpeed));
            l.swingHeight = Mathf.Min(stepHeight + speed * stepHeightSpeedScale, maxStepHeight);
            Retarget(l);
        }

        void Retarget(LegSlot l)
        {
            // land where home will be when the swing ends
            float remaining = l.swingDuration * (1f - Mathf.Clamp01(l.swingT));
            Vector3 to = l.home + _vel * (remaining * plantLead);
            Vector3 hip = l.leg.RootPos;
            Vector3 d = to - hip;
            float maxR = l.leg.Reach * reachUse;
            if (d.magnitude > maxR) to = hip + d.normalized * maxR;   // soft reach clamp
            l.swingTo = to;
            l.swingToNormal = l.homeNormal;
        }

        void UpdateBody(float dt)
        {
            // Root owns orientation. Foot normals stay on the legs for planting.
            _up = root.up;

            // position: ROOT IS THE AUTHORITY. Body tracks root + ride height.
            // Feet stay world-anchored and adapt via stepping — body NEVER anchors to its own feet.
            Vector3 anchor = root.position + _up * rideHeight;
            _bodyPos = Vector3.Lerp(_bodyPos, anchor, 1f - Mathf.Exp(-posSmooth * dt));

            // lean in the root's local axes; zero lean leaves body axes identical to the root
            Vector3 fwd = root.forward;
            Vector3 rawAccel = (_vel - _prevVel) / dt;
            _prevVel = _vel;
            _accel = Vector3.Lerp(_accel, rawAccel, 1f - Mathf.Exp(-6f * dt));
            Vector3 right = root.right;
            float pitch = Mathf.Clamp(-Vector3.Dot(_accel, fwd)   * leanGain, -maxLeanDeg, maxLeanDeg);
            float roll  = Mathf.Clamp(-Vector3.Dot(_accel, right) * leanGain, -maxLeanDeg, maxLeanDeg);
            // Alternating seam contacts can reverse acceleration each frame.
            // Limit only the decorative lean; surface alignment stays with root.
            _pitch = Mathf.MoveTowards(_pitch, pitch, 45f * dt);
            _roll = Mathf.MoveTowards(_roll, roll, 45f * dt);

            Quaternion rot = root.rotation
                        * Quaternion.AngleAxis(_pitch, Vector3.right)
                        * Quaternion.AngleAxis(_roll,  Vector3.forward);

            body.SetPositionAndRotation(_bodyPos, rot);
        }

        void SolveLegs(float dt)
        {
            // gait direction: velocity tangent, else body forward
            Vector3 gaitDir = _vel.sqrMagnitude > 0.01f ? _vel : root.forward;
            gaitDir -= _up * Vector3.Dot(gaitDir, _up);
            gaitDir = gaitDir.sqrMagnitude > 1e-6f ? gaitDir.normalized : root.forward;

            for (int i = 0; i < legs.Length; i++)
            {
                var l = legs[i];
                if (!l.swinging) { l.leg.SolveAt(l.planted, l.plantedNormal, gaitDir); continue; }

                l.swingT += dt / l.swingDuration;
                Retarget(l);                                    // adaptive: keep landing point current
                float t = Mathf.Clamp01(l.swingT);
                float e = t * t * (3f - 2f * t);                // smoothstep horizontal
                Vector3 n = Vector3.Slerp(l.swingFromNormal, l.swingToNormal, e).normalized;
                Vector3 pos = Vector3.LerpUnclamped(l.swingFrom, l.swingTo, e)
                            + n * (Mathf.Sin(Mathf.PI * t) * l.swingHeight);

                if (t >= 1f)
                {
                    l.swinging = false;
                    l.planted = l.swingTo;
                    l.plantedNormal = l.swingToNormal;
                    FootPlanted?.Invoke(l.planted, l.plantedNormal);
                    l.leg.SolveAt(l.planted, l.plantedNormal, gaitDir);
                }
                else l.leg.SolveAt(pos, n, gaitDir);
            }
        }

        void OnDrawGizmosSelected()
        {
            if (legs == null) return;
            foreach (var l in legs)
            {
                if (l.leg == null) continue;
                Gizmos.color = Color.green;  Gizmos.DrawWireSphere(l.home, 0.02f);
                Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(l.planted, 0.025f);
                if (l.swinging)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawLine(l.swingFrom, l.swingTo);
                    Gizmos.DrawWireSphere(l.swingTo, 0.02f);
                }
            }
        }

        [ContextMenu("Capture Home Offsets From Rig")]
        void CaptureHomeOffsets()
        {
            foreach (var l in legs)
            {
                if (l.leg == null) continue;
                Vector3 local = body.InverseTransformPoint((Vector3)l.leg.RootPos);
                local.y = 0f;
                l.homeOffset = local;
            }
        #if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
        #endif
        }

        
        void OnValidate()
        {
            if (root == null || body == null) return;
            if (body == root)
                {
                    if (DuckzLog.AllowWarning(out var logNotice1))
                        logNotice1.Write("[SpiderGait] body == root — the gait will fight itself (this is the 'spinning' bug).", this);
                }
            else if (!body.IsChildOf(root))   // note: IsChildOf returns true for self, hence the check above
                {
                    if (DuckzLog.AllowWarning(out var logNotice2))
                        logNotice2.Write("[SpiderGait] body must be a strict child of root.", this);
                }
        }



        [Header("Debug")]
            public bool debugDraw = true;
            public int StepsTotal { get; private set; }
            public float StepsPerSec { get; private set; }
            public float RootSpeed => _vel.magnitude;
            int _stepWindow; float _stepRateTimer;

            void DebugDraw()
            {
                Debug.DrawLine(_bodyPos - Vector3.one * 0.05f, _bodyPos + Vector3.one * 0.05f, Color.white); // body anchor
                Debug.DrawRay(_bodyPos, _up * 0.4f, Color.cyan);                                             // surface up
                if (_vel.sqrMagnitude   > 0.001f) Debug.DrawRay(_bodyPos, _vel   * 0.25f, Color.blue);       // velocity
                if (_accel.sqrMagnitude > 0.001f) Debug.DrawRay(_bodyPos, _accel * 0.05f, Color.magenta);    // lean source

                float trigger = Mathf.Min(stepTrigger + _vel.magnitude * triggerSpeedScale, maxTrigger);
                for (int i = 0; i < legs.Length; i++)
                {
                    var l = legs[i];
                    if (l.leg == null) continue;
                    float t = trigger > 0f ? Mathf.Clamp01(l.error / trigger) : 0f;              // error -> trigger
                    Debug.DrawLine(l.planted, l.home, Color.Lerp(Color.gray, Color.red, t));     // gray→red = about to step
                    Debug.DrawRay(l.planted, l.plantedNormal * 0.1f, Color.green);
                    if (l.swinging)
                    {
                        Debug.DrawLine(l.swingFrom, l.swingTo, Color.yellow);
                        Debug.DrawRay(l.swingTo, l.swingToNormal * 0.1f, new Color(1f, 0.5f, 0f));
                    }
                }
            }

    }


}
