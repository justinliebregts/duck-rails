using Unity.Mathematics;
using UnityEngine;

namespace Game.Animation
{
    public class TwoBoneLimb : MonoBehaviour
    {
        [Header("Bones")]
        [SerializeField] Transform upperBone;
        [SerializeField] Transform midBone;
        [SerializeField] Transform endBone;

        [Header("End bone")]
        [SerializeField] Quaternion endRotOffset = Quaternion.identity;

        [Header("Roll (end rotation -> limb twist)")]
        [Tooltip("Fraction of the hand/foot's roll absorbed by the mid bone. 1 = wrist/ankle perfectly in line.")]
        [Range(0f, 1f)] [SerializeField] float rollToMid = 1f;
        [Tooltip("Optional fraction shared into the upper bone (whole-arm roll look).")]
        [Range(0f, 1f)] [SerializeField] float rollToUpper = 0f;
        [Tooltip("Roll smoothing rate, 1/s. 0 = instant.")]
        [SerializeField] float rollSmooth = 30f;
        [Tooltip("Max |twist| in degrees. 0 = unlimited. Anatomy: ~85.")]
        [SerializeField] float maxTwistDegrees = 150f;

        // world-frame rest (roll references)
        quaternion _upperRestRot, _midRestRot, _endRestRot;
        float3 _upperRestDir, _midRestDir;
        float _l1, _l2;

        // parent-frame rest (round 26: twist anchored to parent, follows the body)
        quaternion _upperRestLocalRot, _midRestLocalRot;
        float3 _upperRestDirLocal, _midRestDirLocal;

        // roll state (round 12)
        float3 _foreRefLocal, _endRefLocal;
        float _twist;
        bool _hasEnd;

        float3 _dbgTarget, _dbgPole;

        bool _ragdoll;
        Quaternion _ragUpper0, _ragMid0;
        Vector3 _ragHipAxis, _ragKneeAxis;
        float _ragHipAngle, _ragHipVel, _ragHipTarget;
        float _ragKneeAngle, _ragKneeVel;

        public float3 RootPos => upperBone.position;
        public float3 EndPos  => endBone.position;
        public Transform EndBone => endBone;
        public float TotalReach => _l1 + _l2;

        /// <summary>Offset from the end-bone pivot, expressed in the incoming target rotation's frame.
        /// Call during rig setup; includes the authored end rotation and model scale.</summary>
        public float3 EndPointOffset(float3 localPoint)
            => math.mul((quaternion)endRotOffset, localPoint * (float3)endBone.lossyScale);

#if UNITY_EDITOR
        public bool CanPreviewIK => upperBone && midBone && endBone && midBone.parent == upperBone;
#endif

        void Awake() => CaptureRest();

        [ContextMenu("Capture Rest Pose")]
        public void CaptureRest()
        {
            _upperRestRot = upperBone.rotation;
            _midRestRot   = midBone.rotation;
            _hasEnd       = endBone != null;
            _endRestRot   = _hasEnd ? endBone.rotation : quaternion.identity;

            _upperRestDir = math.normalizesafe(midBone.position - upperBone.position, new float3(0, 0, 1));
            _midRestDir   = math.normalizesafe(endBone.position - midBone.position, new float3(0, 0, 1));

            // rest in parent frame
            _upperRestLocalRot = upperBone.localRotation;
            _midRestLocalRot   = midBone.localRotation;
            quaternion upWorld = upperBone.parent != null ? upperBone.parent.rotation : quaternion.identity;
            _upperRestDirLocal = math.rotate(math.inverse(upWorld), _upperRestDir);
            _midRestDirLocal   = math.rotate(math.inverse(_upperRestRot), _midRestDir);
            if (midBone.parent != upperBone)
                {
                    if (DuckzLog.AllowWarning(out var logNotice1))
                        logNotice1.Write("[TwoBoneLimb] midBone must be a child of upperBone (chain contract).", this);
                }

            // Bone offsets define reach. Cache once with the rest pose; solving only rotates bones.
            _l1 = math.distance(upperBone.position, midBone.position);
            _l2 = math.distance(midBone.position, endBone.position);

            // roll references
            float3 refWorld = math.normalizesafe(
                math.cross(_midRestDir, new float3(0.31f, 0.72f, 0.44f)),
                new float3(1f, 0f, 0f));
            _foreRefLocal = math.mul(math.inverse(_midRestRot), refWorld);
            _endRefLocal  = math.mul(math.inverse(_endRestRot), refWorld);
            _twist = 0f;
        }

        public void Solve(float3 targetPos, quaternion targetRot, float3 polePos)
        {
            float3 root    = upperBone.position;
            float3 poleDir = math.normalizesafe(polePos - root, new float3(0f, -1f, 0f));
            _dbgTarget = targetPos; _dbgPole = polePos;

            TwoBoneResult r = TwoBoneIK.Solve(root, targetPos, poleDir, _l1, _l2);

            float3 upperDir = math.normalizesafe(r.mid - root, _upperRestDir);
            float3 midDir   = math.normalizesafe(r.end - r.mid, _midRestDir);

            // parent-anchored rotations (round 26): twist follows the body, not the Awake world frame
            quaternion upRot = upperBone.parent != null ? upperBone.parent.rotation : quaternion.identity;
            float3 upperDirLocal = math.rotate(math.inverse(upRot), upperDir);
            quaternion upperRot = math.mul(upRot, math.mul(GRMathUtils.FromTo(_upperRestDirLocal, upperDirLocal), _upperRestLocalRot));

            quaternion mpRot = upperRot;   // mid's parent IS the upper bone
            float3 midDirLocal = math.rotate(math.inverse(mpRot), midDir);
            quaternion midRot = math.mul(mpRot, math.mul(GRMathUtils.FromTo(_midRestDirLocal, midDirLocal), _midRestLocalRot));

            quaternion endRot = math.mul(targetRot, endRotOffset);

            // roll: absorb the end rotation around the mid-bone axis (round 12 + 13 clamp)
            if (_hasEnd && (rollToMid > 0f || rollToUpper > 0f))
            {
                float3 pf = math.mul(midRot, _foreRefLocal);
                float3 ph = math.mul(endRot, _endRefLocal);

                pf -= midDir * math.dot(pf, midDir);
                ph -= midDir * math.dot(ph, midDir);
                float plf = math.lengthsq(pf), plh = math.lengthsq(ph);

                if (plf > 1e-8f && plh > 1e-8f)
                {
                    pf *= math.rsqrt(plf);
                    ph *= math.rsqrt(plh);

                    float measured = math.atan2(math.dot(math.cross(pf, ph), midDir), math.dot(pf, ph));

                    float delta = measured - _twist;
                    delta = math.atan2(math.sin(delta), math.cos(delta));
                    float k = !Application.isPlaying || rollSmooth <= 0f ? 1f : 1f - math.exp(-rollSmooth * Time.deltaTime);
                    _twist += delta * k;

                    if (maxTwistDegrees > 0f)
                        _twist = math.clamp(_twist, -math.radians(maxTwistDegrees), math.radians(maxTwistDegrees));

                    midRot = math.mul(quaternion.AxisAngle(midDir, _twist * rollToMid), midRot);
                    if (rollToUpper > 0f)
                        upperRot = math.mul(quaternion.AxisAngle(upperDir, _twist * rollToUpper), upperRot);
                }
            }

            upperBone.rotation = upperRot;
            midBone.rotation   = midRot;
            if (_hasEnd) endBone.rotation = endRot;
        }

        /// <summary>Limp swing while knocked. No extra rigidbodies. Gait overwrites this on recovery.</summary>
        public void BeginRagdoll(float signedStrength)
        {
            if (upperBone == null || midBone == null) return;
            if (!_ragdoll)
            {
                _ragUpper0 = upperBone.localRotation;
                _ragMid0 = midBone.localRotation;
                _ragHipAxis = BendAxis(upperBone, midBone);
                _ragKneeAxis = endBone != null ? BendAxis(midBone, endBone) : _ragHipAxis;
                _ragHipAngle = 0f;
                _ragKneeAngle = 0f;
                _ragHipVel = 0f;
                _ragKneeVel = 0f;
                _ragdoll = true;
            }

            float hipSign = signedStrength >= 0f ? 1f : -1f;
            _ragHipTarget = hipSign * 60f;
            _ragHipVel += signedStrength * 280f;
            float kneeKick = signedStrength >= 0f ? signedStrength : -signedStrength;
            _ragKneeVel += kneeKick * 360f;
        }

        public void TickRagdoll(float dt)
        {
            if (!_ragdoll || upperBone == null || midBone == null || dt <= 0f) return;
            float hipAcc = (_ragHipTarget - _ragHipAngle) * 14f - _ragHipVel * 4.5f;
            _ragHipVel += hipAcc * dt;
            _ragHipAngle += _ragHipVel * dt;
            float kneeAcc = (75f - _ragKneeAngle) * 20f - _ragKneeVel * 5f;
            _ragKneeVel += kneeAcc * dt;
            _ragKneeAngle += _ragKneeVel * dt;
            upperBone.localRotation = _ragUpper0 * Quaternion.AngleAxis(_ragHipAngle, _ragHipAxis);
            midBone.localRotation = _ragMid0 * Quaternion.AngleAxis(_ragKneeAngle, _ragKneeAxis);
        }

        public void EndRagdoll() => _ragdoll = false;

        static Vector3 BendAxis(Transform bone, Transform child)
        {
            Vector3 along = child.localPosition;
            Vector3 down = bone.InverseTransformDirection(Vector3.down);
            Vector3 axis = Vector3.Cross(along, down);
            if (axis.sqrMagnitude < 1e-6f) axis = Vector3.right;
            return axis.normalized;
        }

        void OnDrawGizmosSelected()
        {
            if (!upperBone || !midBone || !endBone) return;
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(upperBone.position, midBone.position);
            Gizmos.DrawLine(midBone.position, endBone.position);
            if (Application.isPlaying)
            {
                Gizmos.color = Color.red;    Gizmos.DrawWireSphere(upperBone.position, 0.02f);
                Gizmos.color = Color.yellow;  Gizmos.DrawWireSphere((Vector3)_dbgTarget, 0.03f);
                Gizmos.color = Color.magenta; Gizmos.DrawWireSphere((Vector3)_dbgPole, 0.03f);
            }
        }
    }
}
