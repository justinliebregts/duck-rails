using UnityEngine;

namespace Game.Animation
{
    /// <summary>
    /// Two-legged, point-footed walker. Below runSpeed: strict alternation with weight sway.
    /// At runSpeed+: both legs leap together, body bounces (bound gait).
    /// Root = authority (driver now, locomotion/network later). Body = visual, written here only.
    /// legs[] must contain EXACTLY two slots: [0] = Left, [1] = Right.
    /// </summary>
    public class BipedGait : MonoBehaviour, IGaitStats, IGaitDriver
    {
        public event System.Action<Vector3, Vector3> FootPlanted;
        [System.Serializable]
        public class LegSlot
        {
            public SpiderLeg leg;
            public Vector3 homeOffset;                      // body-local XZ; near centerline for a biped

            [HideInInspector] public Vector3 home;
            [HideInInspector] public Vector3 homeNormal = Vector3.up;
            [HideInInspector] public Vector3 planted;
            [HideInInspector] public Vector3 plantedNormal = Vector3.up;
            [HideInInspector] public Vector3 swingFrom, swingFromNormal;
            [HideInInspector] public Vector3 swingTo, swingToNormal;
            [HideInInspector] public float swingT, swingDuration = 0.2f, swingHeight;
            [HideInInspector] public bool swinging;
            [HideInInspector] public float error;
        }

        [Header("Rig")]
        [SerializeField] Transform root;
        [SerializeField] Transform body;
        [SerializeField] LegSlot[] legs = new LegSlot[2];

        [Header("Surface")]
        [SerializeField] LayerMask surfaceMask;
        [SerializeField] float probeLift = 0.4f;
        [SerializeField] float probeDistance = 1.2f;
        [SerializeField] float probeRadius = 0.12f;

        [Header("Body — walk")]
        [SerializeField] float standHeight = 0.55f;   // ~60–75% of total leg reach
        [SerializeField] float posSmooth = 10f;
        [SerializeField] float upSmooth = 10f;
        [SerializeField] float swayDistance = 0.06f;   // lateral shift toward support leg
        [SerializeField] float swaySmooth = 8f;
        [SerializeField] float stepDip = 0.05f;        // height dip mid-step (weight transfer)
        [SerializeField] float speedLeanDeg = 8f;      // forward lean at full runSpeed
        [SerializeField] float leanGain = 2f;           // deg per m/s²
        [SerializeField] float maxLeanDeg = 12f;
        [SerializeField] float rollWeightDeg = 6f;     // roll toward support leg
        [SerializeField] float teleportDistance = 2f;

        [Header("Stepping — walk (alternating)")]
        [SerializeField] float stepTrigger = 0.14f;
        [SerializeField] float triggerSpeedScale = 0.4f;
        [SerializeField] float maxTrigger = 0.5f;
        [SerializeField] float forcedTrigger = 0.85f;
        [SerializeField] float swingDuration = 0.2f;
        [SerializeField] float swingDurationFast = 0.12f;
        [SerializeField] float stepHeight = 0.06f;
        [SerializeField] float stepHeightSpeedScale = 0.03f;
        [SerializeField] float maxStepHeight = 0.15f;
        [SerializeField, Range(0f, 1f)] float plantLead = 0.7f;
        [SerializeField, Range(0.7f, 1f)] float reachUse = 0.92f;

        [Header("Hop mode (speed >= runSpeed)")]
        [SerializeField] float runSpeed = 2.2f;
        [SerializeField] float hopHeightMult = 2.2f;   // leg lift during the leap
        [SerializeField] float hopLeadMult = 1.4f;      // land further ahead
        [SerializeField] float hopRise = 0.15f;         // body arc mid-leap
        [SerializeField] float hopSurge = 0.5f;         // forward bound impulse on landing
        [SerializeField] float hopLandImpulse = 0.9f;  // downward kick on landing (m/s)
        [SerializeField] float bounceSpring = 120f;
        [SerializeField] float bounceDamp = 12f;
        [SerializeField] float surgeDecay = 6f;

        [Header("Knees (pole in body space)")]
        [SerializeField] float kneeForward = 1f;        // raptor knees; negative = human
        [SerializeField] float kneeUp = 0.35f;
        [SerializeField] float kneeOut = 0.5f;          // per-leg outward splay

        [Header("Ticking")]
        [SerializeField] bool externalTick;          // true when EnemyManager calls Tick()

        [Header("Debug")]
        public bool debugDraw = true;
        public int StepsTotal { get; private set; }
        public float StepsPerSec { get; private set; }
        public float RootSpeed => _vel.magnitude;

        Vector3 _vel, _accel, _prevVel, _surgeVel, _lastRootPos, _bodyPos;
        Vector3 _up = Vector3.up, _fwd = Vector3.forward, _right = Vector3.right;
        float _sway, _bounceOffset, _bounceVel;
        bool _hop, _init;
        int _stepWindow; float _stepRateTimer;

        public bool ExternalTick { get => externalTick; set => externalTick = value; }

        void LateUpdate()
        {
            if (!externalTick) Tick(Time.deltaTime);
        }

        public void Tick(float dt)
        {
            if (root == null || body == null || legs == null || legs.Length != 2 || dt <= 0f) return;

            if (!_init || (root.position - _lastRootPos).sqrMagnitude > teleportDistance * teleportDistance)
            {
                _lastRootPos = root.position;
                _vel = _prevVel = _accel = _surgeVel = Vector3.zero;
                _sway = _bounceOffset = _bounceVel = 0f; _hop = false;
                _up = Vector3.up;
                _bodyPos = root.position + _up * standHeight;
                _fwd = root.forward - _up * Vector3.Dot(root.forward, _up);
                if (_fwd.sqrMagnitude < 1e-4f) _fwd = Vector3.forward;
                _fwd.Normalize();
                _right = Vector3.Cross(_up, _fwd);
                foreach (var l in legs)
                {
                    l.swinging = false;
                    ProbeHome(l);
                    l.planted = l.home; l.plantedNormal = l.homeNormal;
                }
                _init = true;
                UpdateBody(dt);
                SolveLegs(dt);
                CountSteps(dt);
                if (debugDraw) DebugDraw();
                return;
            }

            Vector3 raw = (root.position - _lastRootPos) / dt;
            _lastRootPos = root.position;
            _vel = Vector3.Lerp(_vel, raw, 1f - Mathf.Exp(-10f * dt));
            float speed = _vel.magnitude;
            _hop = speed >= runSpeed;

            for (int i = 0; i < 2; i++) ProbeHome(legs[i]);

            float trigger = Mathf.Min(stepTrigger + speed * triggerSpeedScale, maxTrigger);
            for (int i = 0; i < 2; i++)
            {
                var l = legs[i];
                if (l.swinging) continue;
                l.error = Vector3.Distance(l.planted, l.home);
                if (l.error > forcedTrigger) { BeginSwing(l, speed); continue; }   // panic recovery
                bool otherSwinging = legs[1 - i].swinging;
                if (l.error > trigger && (!otherSwinging || _hop))                 // walk: alternate / hop: together
                {
                    BeginSwing(l, speed);
                    if (_hop && !otherSwinging) BeginSwing(legs[1 - i], speed);     // leap as a pair
                }
            }

            UpdateBody(dt);
            SolveLegs(dt);
            CountSteps(dt);
            if (debugDraw) DebugDraw();
        }

        // ------------------------------------------------------------------ internals

        void CountSteps(float dt)
        {
            _stepRateTimer += dt;
            if (_stepRateTimer >= 1f) { StepsPerSec = _stepWindow / _stepRateTimer; _stepWindow = 0; _stepRateTimer = 0f; }
        }

        void ProbeHome(LegSlot l)
        {
            // Ground walkers probe in a stable, upright frame. The animated body's
            // tilt must not steer subsequent probes into walls or preserve a bad normal.
            Vector3 forward = Vector3.ProjectOnPlane(root.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            Quaternion heading = Quaternion.LookRotation(forward, Vector3.up);
            Vector3 offset = heading * Vector3.Scale(l.homeOffset, body.lossyScale);
            Vector3 anchor = root.position + Vector3.up * standHeight + offset;
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
                l.home = hit.point;
                l.homeNormal = hit.normal.normalized;
            }
            else
            {
                l.home = anchor - Vector3.up * standHeight;
                l.homeNormal = Vector3.up;
            }
        }

        void BeginSwing(LegSlot l, float speed)
        {
            l.swinging = true;
            l.swingT = 0f;
            l.swingFrom = l.planted;
            l.swingFromNormal = l.plantedNormal;
            l.swingDuration = Mathf.Lerp(swingDuration, swingDurationFast, Mathf.Clamp01(speed / runSpeed));
            l.swingHeight = Mathf.Min(stepHeight + speed * stepHeightSpeedScale, maxStepHeight)
                          * (_hop ? hopHeightMult : 1f);
            Retarget(l);
        }

        void Retarget(LegSlot l)
        {
            float remaining = l.swingDuration * (1f - Mathf.Clamp01(l.swingT));
            float lead = plantLead * (_hop ? hopLeadMult : 1f);
            Vector3 to = l.home + _vel * (remaining * lead);
            Vector3 hip = l.leg.RootPos;
            Vector3 d = to - hip;
            float maxR = l.leg.Reach * reachUse;
            if (d.magnitude > maxR) to = hip + d.normalized * maxR;
            l.swingTo = to;
            l.swingToNormal = l.homeNormal;
        }

        void UpdateBody(float dt)
        {
            // surface up from leg probes
            Vector3 avgUp = ((legs[0].homeNormal + legs[1].homeNormal) * 0.5f).normalized;
            _up = Vector3.Slerp(_up, avgUp, 1f - Mathf.Exp(-upSmooth * dt)).normalized;

            // facing from root
            Vector3 fwd = root.forward - _up * Vector3.Dot(root.forward, _up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = root.up - _up * Vector3.Dot(root.up, _up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.forward;
            fwd.Normalize();
            _fwd = fwd;
            _right = Vector3.Cross(_up, fwd);

            // support sway: +1 = weight on RIGHT (left is swinging)
            float swayT = 0f;
            if (!_hop)
            {
                if (legs[0].swinging && !legs[1].swinging) swayT = 1f;
                else if (legs[1].swinging && !legs[0].swinging) swayT = -1f;
            }
            _sway = Mathf.Lerp(_sway, swayT, 1f - Mathf.Exp(-swaySmooth * dt));

            // step phase → walk dip / hop arc
            float phase = 0f;
            if (legs[0].swinging) phase = Mathf.Max(phase, Mathf.Clamp01(legs[0].swingT));
            if (legs[1].swinging) phase = Mathf.Max(phase, Mathf.Clamp01(legs[1].swingT));
            float dip  = _hop ? 0f : stepDip * Mathf.Sin(Mathf.PI * phase);
            float rise = _hop ? hopRise * Mathf.Sin(Mathf.PI * phase) : 0f;

            // hop landing bounce (damped spring, kicked on landing)
            _bounceVel += (-bounceSpring * _bounceOffset - bounceDamp * _bounceVel) * dt;
            _bounceOffset += _bounceVel * dt;

            // landing surge decay
            _surgeVel *= Mathf.Exp(-surgeDecay * dt);

            // POSITION: root is the authority (round-22 rule); sway/dip/bounce are cosmetic layers on top
            float height = standHeight - dip + rise + _bounceOffset;
            _bodyPos += _surgeVel * dt;
            Vector3 anchor = root.position + _up * height + _right * (_sway * swayDistance);
            _bodyPos = Vector3.Lerp(_bodyPos, anchor, 1f - Mathf.Exp(-posSmooth * dt));

            // lean: acceleration + speed (a walker leans into its run)
            Vector3 rawAccel = (_vel - _prevVel) / dt;
            _prevVel = _vel;
            _accel = Vector3.Lerp(_accel, rawAccel, 1f - Mathf.Exp(-6f * dt));
            float pitch = Mathf.Clamp(-Vector3.Dot(_accel, fwd) * leanGain, -maxLeanDeg, maxLeanDeg)
                        + speedLeanDeg * Mathf.Clamp01(_vel.magnitude / runSpeed);
            pitch = Mathf.Clamp(pitch, -maxLeanDeg * 1.5f, maxLeanDeg * 1.5f);
            float roll = -_sway * rollWeightDeg;   // tip toward the support leg

            Quaternion rot = Quaternion.LookRotation(fwd, _up)
                           * Quaternion.AngleAxis(pitch, Vector3.right)
                           * Quaternion.AngleAxis(roll, Vector3.forward);
            // Smooth abrupt lean/roll changes when locomotion stops or reverses during recovery.
            body.SetPositionAndRotation(_bodyPos, Quaternion.Slerp(body.rotation, rot, 1f - Mathf.Exp(-10f * dt)));
        }

        Vector3 PoleDir(int i)
        {
            float side = (i == 0) ? -1f : 1f;    // L, R
            return (_fwd * kneeForward + _up * kneeUp + _right * side * kneeOut).normalized;
        }

        void SolveLegs(float dt)
        {
            Vector3 gaitDir = _vel.sqrMagnitude > 0.01f ? _vel : root.forward;
            gaitDir -= _up * Vector3.Dot(gaitDir, _up);
            gaitDir = gaitDir.sqrMagnitude > 1e-6f ? gaitDir.normalized : root.forward;

            for (int i = 0; i < 2; i++)
            {
                var l = legs[i];
                Vector3 pole = PoleDir(i);
                if (!l.swinging) { l.leg.SolveAt(l.planted, l.plantedNormal, gaitDir, pole); continue; }

                l.swingT += dt / l.swingDuration;
                Retarget(l);
                float t = Mathf.Clamp01(l.swingT);
                float e = t * t * (3f - 2f * t);
                Vector3 n = Vector3.Slerp(l.swingFromNormal, l.swingToNormal, e).normalized;
                Vector3 pos = Vector3.LerpUnclamped(l.swingFrom, l.swingTo, e)
                            + n * (Mathf.Sin(Mathf.PI * t) * l.swingHeight);

                if (t >= 1f)
                {
                    l.swinging = false;
                    l.planted = l.swingTo;
                    l.plantedNormal = l.swingToNormal;
                    OnPlant(i);
                    l.leg.SolveAt(l.planted, l.plantedNormal, gaitDir, pole);
                }
                else l.leg.SolveAt(pos, n, gaitDir, pole);
            }
        }

        void OnPlant(int i)
        {
            FootPlanted?.Invoke(legs[i].planted, legs[i].plantedNormal);
            StepsTotal++; _stepWindow++;
            if (_hop && !legs[1 - i].swinging)          // both feet down = landing complete
            {
                _bounceVel = -hopLandImpulse;            // dip; the spring makes it rebound
                Vector3 dir = _vel.sqrMagnitude > 0.01f ? _vel.normalized : _fwd;
                _surgeVel += dir * hopSurge;             // forward bound
                var p = legs[1 - i];                      // re-sync a lagging partner (chained bounds)
                if (!p.swinging && p.error > stepTrigger * 0.5f) BeginSwing(p, _vel.magnitude);
            }
        }

        void DebugDraw()
        {
            Debug.DrawRay(_bodyPos, _up * 0.4f, Color.cyan);
            if (_vel.sqrMagnitude > 0.001f) Debug.DrawRay(_bodyPos, _vel * 0.25f, Color.blue);
            if (_surgeVel.sqrMagnitude > 0.001f) Debug.DrawRay(_bodyPos, _surgeVel * 0.25f, Color.white);
            Debug.DrawRay(_bodyPos, _right * (_sway * swayDistance * 4f), Color.yellow);   // sway, magnified
            float trigger = Mathf.Min(stepTrigger + _vel.magnitude * triggerSpeedScale, maxTrigger);
            for (int i = 0; i < 2; i++)
            {
                var l = legs[i];
                if (l.leg == null) continue;
                float t = trigger > 0f ? Mathf.Clamp01(l.error / trigger) : 0f;
                Debug.DrawLine(l.planted, l.home, Color.Lerp(Color.gray, Color.red, t));
                if (l.swinging) Debug.DrawLine(l.swingFrom, l.swingTo, Color.yellow);
            }
        }

        // ------------------------------------------------------------------ editor support

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
                        logNotice1.Write("[BipedGait] body == root — the gait will fight itself.", this);
                }
            else if (!body.IsChildOf(root))
                {
                    if (DuckzLog.AllowWarning(out var logNotice2))
                        logNotice2.Write("[BipedGait] body must be a strict child of root.", this);
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
            }
        }
    }
}
