using UnityEngine;

namespace Game.Animation
{
    public interface IGaitStats          // so GaitTestDriver's HUD works with any gait
    {
        int StepsTotal { get; }
        float StepsPerSec { get; }
        float RootSpeed { get; }
    }

    /// <summary>Gaits the EnemyManager can drive after root motion each frame.</summary>
    public interface IGaitDriver
    {
        bool ExternalTick { get; set; }
        void Tick(float dt);
    }

    /// <summary>
    /// Two-armed knuckle-walker (gorilla): hands plant and pull; the body drags and lurches.
    /// Root = authority. Body = visual, written here only.
    /// hands[] must contain EXACTLY two slots: [0] = Left, [1] = Right.
    /// </summary>
    public class GorillaGait : MonoBehaviour, IGaitStats, IGaitDriver
    {
        public event System.Action<Vector3, Vector3> FootPlanted;
        [System.Serializable]
        public class HandSlot
        {
            public SpiderLeg leg;
            // Pose output lets a network avatar reuse this gait and solve its own IK once.
            [System.NonSerialized] public TwoBoneLimb poseLimb;
            [System.NonSerialized] public Transform poseTarget;
            bool posePlantLocked;
            Quaternion posePlantRotation;
            public Vector3 RootPos => poseLimb != null ? (Vector3)poseLimb.RootPos : (Vector3)leg.RootPos;
            public float Reach => poseLimb != null ? poseLimb.TotalReach : leg.Reach;
            public void ReleasePlantOrientation()
            {
                posePlantLocked = false;
                if (leg != null) leg.ReleasePlantOrientation();
            }
            public void SolveAt(Vector3 point, Vector3 normal, Vector3 direction, bool lockPlantOrientation = false)
            {
                if (poseTarget == null)
                {
                    if (leg != null) leg.SolveAt(point, normal, direction, lockPlantOrientation: lockPlantOrientation);
                    return;
                }
                Vector3 tangent = Vector3.ProjectOnPlane(direction, normal).normalized;
                if (tangent.sqrMagnitude < 0.001f) tangent = Vector3.ProjectOnPlane(Vector3.forward, normal).normalized;
                // Match the minion's mostly downward knuckle axis; the avatar retains
                // its own end-bone calibration and elbow solver.
                Quaternion rotation = Quaternion.LookRotation(-normal * 0.8f + tangent * 0.2f, normal);
                if (lockPlantOrientation)
                {
                    if (!posePlantLocked) posePlantRotation = rotation;
                    posePlantLocked = true;
                    rotation = posePlantRotation;
                }
                else posePlantLocked = false;
                poseTarget.SetPositionAndRotation(point + normal * 0.035f, rotation);
            }
            public Vector3 homeOffset;                     // body-local XZ; hands sit in FRONT (+Z)

            [HideInInspector] public Vector3 home;
            [HideInInspector] public Vector3 homeNormal = Vector3.up;
            [HideInInspector] public Vector3 planted;
            [HideInInspector] public Vector3 plantedNormal = Vector3.up;
            [HideInInspector] public Vector3 swingFrom, swingFromNormal;
            [HideInInspector] public Vector3 swingTo, swingToNormal;
            [HideInInspector] public float swingT, swingDuration = 0.22f, swingHeight;
            [HideInInspector] public bool swinging;
            [HideInInspector] public float error;
        }

        [Header("Rig")]
        [SerializeField] Transform root;
        [SerializeField] Transform body;
        [SerializeField] HandSlot[] hands = new HandSlot[2];

        [Header("Surface probing")]
        [SerializeField] LayerMask surfaceMask;
        [SerializeField] float probeLift = 0.4f;
        [SerializeField] float probeDistance = 1.0f;
        [SerializeField] float probeRadius = 0.15f;

        [Header("Body — drag & undulation")]
        [SerializeField] float hangHeight = 0.35f;   // body height when pulled in close to the hands
        [SerializeField] float dragHeight = 0.10f;   // body height when stretched out behind
        [SerializeField, Min(0f)] float bodyGroundClearance = 0.2f;
        [SerializeField] float posSmooth = 8f;        // 1/s — how quickly the body chases the root
        [SerializeField] float surgeImpulse = 0.4f;  // m/s forward kick on each plant — THE lurch knob
        [SerializeField] float surgeDecay = 6f;

        [Header("Body — rotation")]
        [SerializeField] float upSmooth = 10f;
        [SerializeField] float leanGain = 2.5f;       // deg per m/s²
        [SerializeField] float maxLeanDeg = 12f;
        [SerializeField] float dragPitchDeg = 10f;    // nose-UP degrees when stretched out (flip sign if wrong for your model)
        [SerializeField] float rollWeightDeg = 7f;   // roll toward the planted hand
        [SerializeField] float rollSmooth = 6f;
        [SerializeField] float teleportDistance = 2f;

        [Header("Stepping")]
        [SerializeField] float stepTrigger = 0.16f;
        [SerializeField] float triggerSpeedScale = 0.4f;
        [SerializeField] float maxTrigger = 0.55f;
        [SerializeField] float forcedTrigger = 0.85f;
        [SerializeField] float swingDuration = 0.22f;
        [SerializeField] float swingDurationFast = 0.13f;
        [SerializeField] float runSpeed = 2.0f;
        [SerializeField] float stepHeight = 0.09f;
        [SerializeField] float stepHeightSpeedScale = 0.03f;
        [SerializeField] float maxStepHeight = 0.18f;
        [SerializeField, Range(0f, 1f)] float plantLead = 0.7f;
        [SerializeField, Range(0.7f, 1f)] float reachUse = 0.92f;

        [Header("Ticking")]
        [SerializeField] bool externalTick;          // true when EnemyManager calls Tick()

        [Header("Debug")]
        public bool debugDraw = true;
        public int StepsTotal { get; private set; }
        public float StepsPerSec { get; private set; }
        public float RootSpeed => _vel.magnitude;

        Vector3 _vel, _accel, _prevVel, _lastRootPos, _bodyPos, _surgeVel;
        Vector3 _up = Vector3.up;
        float _roll;
        bool _init;
        int _stepWindow; float _stepRateTimer;

        public bool ExternalTick { get => externalTick; set => externalTick = value; }
        public bool Airborne { get; set; }

        /// <summary>Use the minion gait to generate shared player head/hand targets, without writing its bones.</summary>
        public void ConfigurePoseTargets(Transform motionRoot, Transform headTarget, Transform leftTarget,
            Transform rightTarget, TwoBoneLimb leftArm, TwoBoneLimb rightArm, LayerMask mask, float eyeHeight)
        {
            root = motionRoot;
            body = headTarget;
            surfaceMask = mask;
            externalTick = true;
            debugDraw = false;
            hands = new[] {
                new HandSlot { poseLimb = leftArm, poseTarget = leftTarget, homeOffset = new Vector3(-0.32f, 0, 0.22f) },
                new HandSlot { poseLimb = rightArm, poseTarget = rightTarget, homeOffset = new Vector3(0.32f, 0, 0.22f) }
            };
            hangHeight = eyeHeight;
            dragHeight = Mathf.Max(0.1f, eyeHeight - 0.04f);
            bodyGroundClearance = dragHeight;
            surgeImpulse = 0.12f;
            posSmooth = 16f;
            stepTrigger = 0.12f;
            triggerSpeedScale = 0.06f;
            maxTrigger = 0.3f;
            forcedTrigger = 0.5f;
            probeRadius = 0.06f;
            ResetPose();
        }

        public void ResetPose() => _init = false;

        /// <summary>Keep avatar hand targets on this side of walls, seats and ledges.
        /// These are visual contacts; they never pin the locomotion root.</summary>
        public void ConstrainPoseTargets()
        {
            if (root == null || hands == null) return;
            foreach (var hand in hands)
            {
                if (hand.poseTarget == null) continue;
                Vector3 desired = hand.poseTarget.position;
                Vector3 safe = ClearHandTarget(desired);
                if ((safe - desired).sqrMagnitude > 0.0001f)
                {
                    // An old planted point can be behind a wall after turning or walking
                    // past a seat. Release it immediately instead of pulling back toward it.
                    hand.ReleasePlantOrientation();
                    hand.swinging = false;
                    hand.planted = safe - Vector3.up * 0.035f;
                    hand.plantedNormal = Vector3.up;
                }
                hand.poseTarget.position = safe;
            }
        }

        Vector3 ClearHandTarget(Vector3 desired)
        {
            const float radius = 0.05f;
            const float skin = 0.008f;
            // Start inside the controller's free space, not at last frame's hand;
            // sweeps from an already embedded hand can miss a mesh's back face.
            Vector3 origin = root.position + Vector3.up * Mathf.Clamp(desired.y - root.position.y, 0.12f, hangHeight);
            Vector3 delta = desired - origin;
            float distance = delta.magnitude;
            if (distance > 0.001f && Physics.SphereCast(origin, radius, delta / distance,
                out RaycastHit hit, distance + skin, surfaceMask, QueryTriggerInteraction.Ignore))
                return origin + delta / distance * Mathf.Max(0f, hit.distance - skin);
            return desired;
        }

        void LateUpdate()
        {
            if (!externalTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (root == null || body == null || hands == null || hands.Length != 2 || dt <= 0f) return;

            if (Airborne)
            {
                // Keep hands with the rig instead of planting them on surfaces below the arc.
                _init = false; // Replant cleanly on landing, without a velocity/lean spike.
                _vel = _prevVel = _accel = _surgeVel = Vector3.zero;
                _bodyPos = root.position + Vector3.up * hangHeight;
                body.SetPositionAndRotation(_bodyPos, root.rotation);
                foreach (var hand in hands)
                {
                    if (hand.leg == null && hand.poseTarget == null) continue;
                    Vector3 point = root.position + root.rotation * Vector3.Scale(hand.homeOffset, body.lossyScale)
                        + Vector3.up * (hangHeight + 0.15f);
                    hand.SolveAt(point, Vector3.up, root.forward);
                }
                return;
            }

            if (!_init || (root.position - _lastRootPos).sqrMagnitude > teleportDistance * teleportDistance)
            {
                _lastRootPos = root.position;
                _vel = _prevVel = _accel = _surgeVel = Vector3.zero;
                _up = Vector3.up; _roll = 0f;
                _bodyPos = root.position + _up * hangHeight;
                foreach (var h in hands)
                {
                    h.swinging = false;
                    h.ReleasePlantOrientation();
                    ProbeHome(h);
                    h.planted = h.home; h.plantedNormal = h.homeNormal;
                }
                _init = true;
                UpdateBody(dt);
                SolveHands(dt);
                if (debugDraw) DebugDraw();
                return;
            }

            Vector3 raw = (root.position - _lastRootPos) / dt;
            _lastRootPos = root.position;
            _vel = Vector3.Lerp(_vel, raw, 1f - Mathf.Exp(-10f * dt));
            float speed = _vel.magnitude;

            for (int i = 0; i < 2; i++) ProbeHome(hands[i]);

            float trigger = Mathf.Min(stepTrigger + speed * triggerSpeedScale, maxTrigger);
            for (int i = 0; i < 2; i++)
            {
                var h = hands[i];
                if (h.swinging) continue;
                h.error = Vector3.Distance(h.planted, h.home);
                if (h.error > forcedTrigger) { BeginSwing(h, speed); continue; }   // panic recovery — ignores alternation
                if (h.error > trigger && !hands[1 - i].swinging) BeginSwing(h, speed); // strict alternation
            }

            UpdateBody(dt);
            SolveHands(dt);

            _stepRateTimer += dt;
            if (_stepRateTimer >= 1f) { StepsPerSec = _stepWindow / _stepRateTimer; _stepWindow = 0; _stepRateTimer = 0f; }
            if (debugDraw) DebugDraw();
        }

        // ------------------------------------------------------------------ internals

        void ProbeHome(HandSlot h)
        {
            // Ground walkers probe in a stable, upright frame. The animated body's
            // tilt must not steer subsequent probes into walls or preserve a bad normal.
            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            Quaternion heading = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 offset = heading * Vector3.Scale(h.homeOffset, body.lossyScale);
            Vector3 anchor = root.position + Vector3.up * hangHeight + offset;
            Vector3 origin = anchor + Vector3.up * probeLift;
            float distance = probeLift + probeDistance;

            bool grounded = Physics.SphereCast(origin, probeRadius, Vector3.down,
                out RaycastHit hit, distance, surfaceMask, QueryTriggerInteraction.Ignore)
                && Game.AI.CrawlGraph.IsWalkableGround(hit.normal);
            // A wall can block the wider probe even when there is floor below its center.
            if (!grounded)
                grounded = Physics.Raycast(origin, Vector3.down, out hit, distance,
                    surfaceMask, QueryTriggerInteraction.Ignore)
                    && Game.AI.CrawlGraph.IsWalkableGround(hit.normal);

            if (grounded)
            {
                h.home = hit.point;
                h.homeNormal = hit.normal.normalized;
            }
            else
            {
                // No supported plant: keep avatar hands near the body until a safe
                // surface is found instead of inventing a contact inside a mesh.
                h.home = h.poseTarget != null ? anchor : anchor - Vector3.up * hangHeight;
                h.homeNormal = Vector3.up;
            }
            if (h.poseTarget != null)
                h.home = ClearHandTarget(h.home + h.homeNormal * 0.05f) - h.homeNormal * 0.035f;
        }

        static Vector3 HandAnchorPos(HandSlot h) => h.swinging ? h.swingTo : h.planted;

        void BeginSwing(HandSlot h, float speed)
        {
            h.ReleasePlantOrientation();
            h.swinging = true;
            h.swingT = 0f;
            h.swingFrom = h.planted;
            h.swingFromNormal = h.plantedNormal;
            h.swingDuration = Mathf.Lerp(swingDuration, swingDurationFast, Mathf.Clamp01(speed / runSpeed));
            h.swingHeight = Mathf.Min(stepHeight + speed * stepHeightSpeedScale, maxStepHeight);
            Retarget(h);
        }

        void Retarget(HandSlot h)
        {
            float remaining = h.swingDuration * (1f - Mathf.Clamp01(h.swingT));
            // Avatar probes already found a safe plant. Extrapolating it by velocity
            // can put the hand through a bus seat or beyond the edge of the floor.
            Vector3 to = h.home + (h.poseTarget != null ? Vector3.zero : _vel * (remaining * plantLead));
            Vector3 shoulder = h.RootPos;
            Vector3 d = to - shoulder;
            float maxR = h.Reach * reachUse;
            if (d.magnitude > maxR) to = shoulder + d.normalized * maxR;
            h.swingTo = to;
            h.swingToNormal = h.homeNormal;
        }

        void UpdateBody(float dt)
        {
            // surface up from hand probes
            Vector3 avgUp = ((hands[0].homeNormal + hands[1].homeNormal) * 0.5f).normalized;
            _up = Vector3.Slerp(_up, avgUp, 1f - Mathf.Exp(-upSmooth * dt)).normalized;

            // closeness: gathered-up between the hands (0 = stretched out, 1 = pulled in tight)
            Vector3 centroid = (HandAnchorPos(hands[0]) + HandAnchorPos(hands[1])) * 0.5f;
            float reach = Mathf.Max((hands[0].Reach + hands[1].Reach) * 0.5f, 0.05f);
            float distTangent = Vector3.ProjectOnPlane(_bodyPos - centroid, _up).magnitude;
            float closeness = 1f - Mathf.Clamp01(distTangent / (reach * 0.9f));

            // undulation: hangs high when pulled in, drags low when stretched
            float height = Mathf.Lerp(dragHeight, hangHeight, closeness);

            // lurch: decaying forward surge from the last plant
            _surgeVel *= Mathf.Exp(-surgeDecay * dt);

            // POSITION: root is the authority (round-22 rule — never anchor the body to its own hands)
            _bodyPos += _surgeVel * dt;
            _bodyPos = Vector3.Lerp(_bodyPos, root.position + _up * height, 1f - Mathf.Exp(-posSmooth * dt));
            // Horizontal lag and surge may move the visual body over higher
            // terrain than the root. Never smooth its height through that floor.
            Vector3 bodyProbe = new Vector3(_bodyPos.x,
                Mathf.Max(root.position.y, _bodyPos.y) + probeLift, _bodyPos.z);
            if (Physics.Raycast(bodyProbe, Vector3.down, out RaycastHit bodyGround,
                probeLift + probeDistance, surfaceMask, QueryTriggerInteraction.Ignore)
                && Game.AI.CrawlGraph.IsWalkableGround(bodyGround.normal))
                _bodyPos.y = Mathf.Max(_bodyPos.y, bodyGround.point.y + Mathf.Max(height, bodyGroundClearance));

            // facing from root
            Vector3 fwd = root.forward - _up * Vector3.Dot(root.forward, _up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = root.up - _up * Vector3.Dot(root.up, _up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();

            // accel lean + drag pitch (nose-up while stretched out)
            Vector3 rawAccel = (_vel - _prevVel) / dt;
            _prevVel = _vel;
            _accel = Vector3.Lerp(_accel, rawAccel, 1f - Mathf.Exp(-6f * dt));
            float pitch = Mathf.Clamp(-Vector3.Dot(_accel, fwd) * leanGain, -maxLeanDeg, maxLeanDeg)
                        - dragPitchDeg * (1f - closeness);
            pitch = Mathf.Clamp(pitch, -maxLeanDeg * 1.5f, maxLeanDeg * 1.5f);

            // roll: weight toward the planted hand
            float rollTarget = 0f;
            if (!hands[0].swinging && hands[1].swinging) rollTarget = rollWeightDeg;    // left planted
            else if (hands[0].swinging && !hands[1].swinging) rollTarget = -rollWeightDeg;
            _roll = Mathf.Lerp(_roll, rollTarget, 1f - Mathf.Exp(-rollSmooth * dt));

            Quaternion rot = Quaternion.LookRotation(fwd, _up)
                           * Quaternion.AngleAxis(pitch, Vector3.right)
                           * Quaternion.AngleAxis(_roll, Vector3.forward);

            // Smooth abrupt lean/roll changes when locomotion stops or reverses during recovery.
            body.SetPositionAndRotation(_bodyPos, Quaternion.Slerp(body.rotation, rot, 1f - Mathf.Exp(-10f * dt)));
        }

        void SolveHands(float dt)
        {
            Vector3 gaitDir = _vel.sqrMagnitude > 0.01f ? _vel : root.forward;
            gaitDir -= _up * Vector3.Dot(gaitDir, _up);
            gaitDir = gaitDir.sqrMagnitude > 1e-6f ? gaitDir.normalized : root.forward;

            for (int i = 0; i < 2; i++)
            {
                var h = hands[i];
                if (!h.swinging)
                {
                    h.SolveAt(h.planted, h.plantedNormal, gaitDir, lockPlantOrientation: true);
                    continue;
                }

                h.swingT += dt / h.swingDuration;
                Retarget(h);
                float t = Mathf.Clamp01(h.swingT);
                float e = t * t * (3f - 2f * t);
                Vector3 n = Vector3.Slerp(h.swingFromNormal, h.swingToNormal, e).normalized;
                Vector3 pos = Vector3.LerpUnclamped(h.swingFrom, h.swingTo, e)
                            + n * (Mathf.Sin(Mathf.PI * t) * h.swingHeight);

                if (t >= 1f)
                {
                    h.swinging = false;
                    h.planted = h.swingTo;
                    h.plantedNormal = h.swingToNormal;
                    OnPlant(h, gaitDir);
                    h.SolveAt(h.planted, h.plantedNormal, gaitDir, lockPlantOrientation: true);
                }
                else h.SolveAt(pos, n, gaitDir);
            }
        }

        void OnPlant(HandSlot h, Vector3 gaitDir)
        {
            FootPlanted?.Invoke(h.planted, h.plantedNormal);
            StepsTotal++; _stepWindow++;
            if (surgeImpulse > 0f) _surgeVel += gaitDir * surgeImpulse;   // the lurch
        }

        void DebugDraw()
        {
            Debug.DrawRay(_bodyPos, _up * 0.4f, Color.cyan);
            if (_vel.sqrMagnitude > 0.001f) Debug.DrawRay(_bodyPos, _vel * 0.25f, Color.blue);
            if (_surgeVel.sqrMagnitude > 0.001f) Debug.DrawRay(_bodyPos, _surgeVel * 0.25f, Color.white);
            float trigger = Mathf.Min(stepTrigger + _vel.magnitude * triggerSpeedScale, maxTrigger);
            for (int i = 0; i < 2; i++)
            {
                var h = hands[i];
                if (h.leg == null) continue;
                float t = trigger > 0f ? Mathf.Clamp01(h.error / trigger) : 0f;
                Debug.DrawLine(h.planted, h.home, Color.Lerp(Color.gray, Color.red, t));
                if (h.swinging) Debug.DrawLine(h.swingFrom, h.swingTo, Color.yellow);
            }
        }

        // ------------------------------------------------------------------ editor support

        [ContextMenu("Capture Home Offsets From Rig")]
        void CaptureHomeOffsets()
        {
            foreach (var h in hands)
            {
                if (h.leg == null) continue;
                Vector3 local = body.InverseTransformPoint((Vector3)h.leg.RootPos);
                local.y = 0f;
                h.homeOffset = local;
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
                        logNotice1.Write("[GorillaGait] body == root — the gait will fight itself.", this);
                }
            else if (!body.IsChildOf(root))
                {
                    if (DuckzLog.AllowWarning(out var logNotice2))
                        logNotice2.Write("[GorillaGait] body must be a strict child of root.", this);
                }
        }

        void OnDrawGizmosSelected()
        {
            if (hands == null) return;
            foreach (var h in hands)
            {
                if (h.leg == null) continue;
                Gizmos.color = Color.green;  Gizmos.DrawWireSphere(h.home, 0.02f);
                Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(h.planted, 0.025f);
            }
        }
    }
}
