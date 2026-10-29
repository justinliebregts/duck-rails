using Unity.Mathematics;
using UnityEngine;

namespace Game.Animation
{
    /// <summary>3-bone spider-style leg on top of TwoBoneLimb.
    /// TwoBoneLimb bones: upper=coxa, mid=femur, end=foot pivot (ankle).
    /// footTip = empty transform at the toe, used to measure foot length.</summary>
    public class SpiderLeg : MonoBehaviour
    {
        [SerializeField] TwoBoneLimb leg;
        [SerializeField] Transform footTip;
        [SerializeField] Transform bodyCenter;   // outward splay reference

        [Header("Foot shape")]
        [Range(0f, 1f)] [SerializeField] float footDownness = 0.8f; // 1=tip-toe spider, 0=flat palm
        [Range(0f, 1f)] [SerializeField] float footLean    = 0.2f;

        [Header("Knee shape")]
        [Range(0f, 2f)] [SerializeField] float kneeOut = 0.6f;

        [Header("Test drive (delete when gait manager exists)")]
        [SerializeField] Transform plantTarget;
        [SerializeField] Vector3 surfaceNormal = Vector3.up;
        [SerializeField] Vector3 gaitDirection = Vector3.forward;

        public float3 RootPos => leg != null ? leg.RootPos : (float3)transform.position;
        public Vector3 HipWorld => leg != null ? (Vector3)leg.RootPos : transform.position;
        public Vector3 FootWorld => footTip != null ? footTip.position : HipWorld;
        public float Reach => _l3 + (leg != null ? leg.TotalReach : 0f);  // full leg incl. foot

        public void BeginRagdoll(float signedStrength)
        {
            if (leg != null) leg.BeginRagdoll(signedStrength);
        }

        public void TickRagdoll(float dt)
        {
            if (leg != null) leg.TickRagdoll(dt);
        }

        public void EndRagdoll()
        {
            if (leg != null) leg.EndRagdoll();
        }

        float _l3;
        bool _hasPlantOrientation;
        float3 _plantAxis;
        quaternion _plantRotation;

        // Called when a gait replants after spawning, teleporting or landing.
        public void ReleasePlantOrientation() => _hasPlantOrientation = false;

        void Awake() => _l3 = Vector3.Distance(leg.EndPos, footTip.position);

        /// <summary>Plant the toe tip at plantPoint on a surface with the given
        /// outward normal, leaning the foot toward gaitDir (tangent to surface).</summary>
        public void SolveAt(float3 plantPoint, float3 normal, float3 gaitDir, float3 poleOverride = default,
            bool lockPlantOrientation = false)
        {
            float3 n = math.normalizesafe(normal, new float3(0, 1, 0));

            float3 tangent = gaitDir - n * math.dot(gaitDir, n);
            tangent = math.normalizesafe(tangent, new float3(1, 0, 0));
            float3 footAxis = math.normalizesafe(-n * footDownness + tangent * footLean, -n);

            float3 root = leg.RootPos;
            float3 poleDir;
            if (math.lengthsq(poleOverride) > 1e-8f)                    // gait knows better
            {
                poleDir = math.normalizesafe(poleOverride, n);
            }
            else                                                          // original spider behavior
            {
                float3 outward = root - (float3)bodyCenter.position;
                outward -= n * math.dot(outward, n);
                outward = math.normalizesafe(outward, tangent);
                poleDir = math.normalizesafe(n + outward * kneeOut, n);
            }

            quaternion footRot = quaternion.LookRotationSafe(footAxis, poleDir);
            if (lockPlantOrientation)
            {
                if (!_hasPlantOrientation)
                {
                    _plantAxis = footAxis;
                    _plantRotation = footRot;
                    _hasPlantOrientation = true;
                }
                // A planted palm must not swivel or shift its wrist when the
                // movement heading changes. The elbow still follows the shoulder.
                footAxis = _plantAxis;
                footRot = _plantRotation;
            }
            else ReleasePlantOrientation();

            float3 ankle = plantPoint - footAxis * _l3;
            leg.Solve(ankle, footRot, root + poleDir * 0.5f);

            _dbgPlant = plantPoint; _dbgN = n; _dbgAxis = footAxis; _dbgPole = poleDir;
        }

        float3 _dbgPlant, _dbgN, _dbgAxis, _dbgPole;

        void LateUpdate()
        {
            if (plantTarget != null) SolveAt(plantTarget.position, surfaceNormal, gaitDirection);
        }

        void OnDrawGizmosSelected()
        {
            if (plantTarget == null) return;
            Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(plantTarget.position, 0.02f);
            Gizmos.color = Color.cyan;   Gizmos.DrawRay(plantTarget.position, (Vector3)_dbgN * 0.2f);
            Gizmos.color = Color.green;  Gizmos.DrawRay(plantTarget.position, (Vector3)_dbgAxis * 0.15f);
            Gizmos.color = Color.magenta;Gizmos.DrawRay(leg.RootPos, (Vector3)_dbgPole * 0.3f);
        }
    }
}
