using Unity.Mathematics;
using UnityEngine;

namespace Game.Animation
{
    public struct BodyPose
    {
        public float3 headPos;  public quaternion headRot;
        public float3 lHandPos; public quaternion lHandRot;
        public float3 rHandPos; public quaternion rHandRot;
        public FingerCurl fingers;
    }

    /// <summary>
    /// Procedural remote-player body: head + chest + two IK arms, driven by one pose struct.
    /// Call ApplyPose once per frame (LateUpdate slot), after your pose source has updated.
    /// </summary>
    public class RemotePlayerBody : MonoBehaviour
    {
        [Header("Rig")]
        [SerializeField] Transform head;       // head mesh/bone root
        [SerializeField] Transform chest;       // empty; arm bones are its children
        [SerializeField] TwoBoneLimb leftArm;
        [SerializeField] TwoBoneLimb rightArm;

        [Header("Player hand calibration")]
        [Tooltip("Human players only: tracked hand poses locate the palm instead of the wrist. Leave off for NPCs.")]
        [SerializeField] bool trackPalms;

        // Half the shared rig's ~0.10654 wrist-to-knuckle distance in hand-bone space.
        // At the authored 0.7 hand scale this is 3.731 cm; independent of arm length and skin.
        public static readonly float3 PlayerPalmPoint = new float3(0f, 0.0533f, 0f);
        public bool TracksPalms => trackPalms;
        float3 _leftPalmOffset, _rightPalmOffset;

        void Awake() => CachePalmOffsets();

        void CachePalmOffsets()
        {
            if (!trackPalms) return;
            _leftPalmOffset = leftArm.EndPointOffset(PlayerPalmPoint);
            _rightPalmOffset = rightArm.EndPointOffset(PlayerPalmPoint);
        }

        public Transform Head => head;
        public Transform Chest => chest;
        public Transform LeftHand => leftArm != null ? leftArm.EndBone : null;
        public Transform RightHand => rightArm != null ? rightArm.EndBone : null;
        public TwoBoneLimb LeftArm => leftArm;
        public TwoBoneLimb RightArm => rightArm;
        /// <summary>Chest position relative to the eyes, in head-yaw space.</summary>
        public Vector3 ChestFromHead => new Vector3(0f, -shoulderDrop, shoulderForward);

        public Vector3 EyePosition => head != null ? head.TransformPoint(eyeOffset) : transform.position;

        public Vector3 GazeForward => head != null ? head.forward : transform.forward;

        [Header("Head calibration")]
        // Position of the model's EYES in the head bone's local space.
        // BodyPose.headPos is treated as the eye pose; the bone is placed
        // so the eyes land exactly there.
        [SerializeField] float3 eyeOffset = new float3(0f, 0.11f, 0.03f);

        [Header("Proportions (meters)")]
        [SerializeField] float shoulderDrop    = 0.20f; // head -> shoulder plane
        [SerializeField] float shoulderForward  = 0.04f; // slight forward bias reads better

        [Header("Elbow hint (chest-frame weights)")]
        [SerializeField] float elbowBack  = 1.0f;
        [SerializeField] float elbowOut   = 0.6f;
        [SerializeField] float elbowDown  = 0.25f;
        [SerializeField] float poleSmooth = 12f;  // 1/s; raise = snappier elbows
        [Header("Elbow stabilization")]
        [Tooltip("Maximum swivel speed around the shoulder-to-hand axis. Lower is smoother but slower to clear the torso.")]
        [Min(1f)] [SerializeField] float elbowMaxSpeed = 240f;
        [Tooltip("Response rate for torso and face influences. Lower values soften transitions.")]
        [Min(0.1f)] [SerializeField] float elbowInfluenceSmooth = 10f;

        [Header("Close-hand elbow bias")]
        [SerializeField] bool proximityElbowBias = true;
        [Tooltip("Chest-local offset of the torso influence ellipsoid, in meters.")]
        [SerializeField] Vector3 torsoBiasCenter = new Vector3(0f, -0.22f, 0f);
        [Tooltip("Outer half-width, half-height and half-depth in meters. Bias fades to zero at this surface.")]
        [SerializeField] Vector3 torsoBiasRadii = new Vector3(0.35f, 0.45f, 0.35f);
        [Range(0f, 0.95f)] [SerializeField] float torsoFullBiasFraction = 0.55f;
        [Min(0f)] [SerializeField] float faceFullBiasDistance = 0.16f;
        [Min(0.01f)] [SerializeField] float faceBiasDistance = 0.42f;
        [Tooltip("X = outward, Y = backward, Z = downward. Blended from the normal elbow weights near the torso.")]
        [SerializeField] Vector3 torsoElbowWeights = new Vector3(1.2f, 0.25f, 0.1f);
        [Tooltip("X = outward, Y = backward, Z = downward. Face influence takes precedence over torso influence.")]
        [SerializeField] Vector3 faceElbowWeights = new Vector3(1.4f, 0.15f, 0f);
        [SerializeField] bool showElbowBiasRegions = true;

        float3 _lPoleDir, _rPoleDir;
        struct ElbowState
        {
            public float3 axis, bend;
            public float2 influences;
            public bool initialized, holdingBase;
        }
        ElbowState _leftElbow, _rightElbow;
        void OnEnable()
        {
            _leftElbow = default;
            _rightElbow = default;
        }

#if UNITY_EDITOR
        // Editor preview can use the same temporal stabilization as gameplay.
        public bool CanPreviewIK => head && chest && leftArm && rightArm
            && leftArm.CanPreviewIK && rightArm.CanPreviewIK;
        public Vector3 LeftPreviewPole => (Vector3)(leftArm.RootPos + _lPoleDir);
        public Vector3 RightPreviewPole => (Vector3)(rightArm.RootPos + _rPoleDir);
        public void InitializeIKPreview()
        {
            leftArm.CaptureRest();
            rightArm.CaptureRest();
            CachePalmOffsets();
            _leftElbow = default;
            _rightElbow = default;
        }
        public void ApplyIKPreview(in BodyPose pose, bool simulateSmoothing = false, float deltaTime = 0f)
        {
            ApplyPoseInternal(pose, 0f, deltaTime, simulateSmoothing);
        }
#endif

        /// <summary>
        /// Places the rig from <paramref name="p"/>. <paramref name="chestBack"/> slides the chest
        /// backward along body yaw (the same flat axis as <see cref="shoulderForward"/>). 0 leaves remote copies 1:1.
        /// </summary>
        public void ApplyPose(in BodyPose p, float chestBack = 0f, Quaternion? bodyRotation = null)
            => ApplyPoseInternal(p, chestBack, Time.deltaTime, Application.isPlaying, bodyRotation);

        void ApplyPoseInternal(in BodyPose p, float chestBack, float deltaTime, bool smooth, Quaternion? bodyRotation = null)
        {
            // --- Chest FIRST (parent of the head in nested rigs) ----------------
            float3 hf   = bodyRotation.HasValue ? (float3)(bodyRotation.Value * Vector3.forward) : math.mul(p.headRot, new float3(0, 0, 1));
            float3 flat = new float3(hf.x, 0f, hf.z);
            flat = math.lengthsq(flat) > 1e-6f ? math.normalize(flat) : new float3(0, 0, 1);
            float3 up    = new float3(0, 1, 0);
            float3 right = math.normalize(math.cross(up, flat));
            float3 back  = -flat;

            chest.SetPositionAndRotation(
                p.headPos + new float3(0f, -shoulderDrop, 0f) + flat * (shoulderForward - chestBack),
                quaternion.LookRotationSafe(flat, up));

            // --- Head: bone placed so the model's eyes land on the tracked eye pose ---
            head.SetPositionAndRotation(p.headPos - math.mul(p.headRot, eyeOffset), p.headRot);

            // Keep the shared/network pose in tracking space. Convert palms to wrist targets
            // once here for both local and remote humans; ragdolls copy the resulting bones.
            float3 lWrist = p.lHandPos, rWrist = p.rHandPos;
            if (trackPalms)
            {
                lWrist -= math.mul(p.lHandRot, _leftPalmOffset);
                rWrist -= math.mul(p.rHandRot, _rightPalmOffset);
            }

            // --- Elbow poles (smoothed) --------------------------------------
            // Clamp long editor stalls / dropped frames so they cannot permit a giant swivel.
            float dt = math.clamp(deltaTime, 0f, 0.05f);
            _lPoleDir = ElbowTarget(lWrist, p.headPos, leftArm.RootPos, -right, back, up, ref _leftElbow, dt, smooth);
            _rPoleDir = ElbowTarget(rWrist, p.headPos, rightArm.RootPos, right, back, up, ref _rightElbow, dt, smooth);

            // --- Arms ----------------------------------------------------------
            float3 lShoulder = leftArm.RootPos;
            float3 rShoulder = rightArm.RootPos;
            leftArm .Solve(lWrist, p.lHandRot, lShoulder + _lPoleDir);
            rightArm.Solve(rWrist, p.rHandRot, rShoulder + _rPoleDir);
        }

        float3 ElbowTarget(float3 hand, float3 eyes, float3 shoulder, float3 outward,
            float3 back, float3 up, ref ElbowState state, float dt, bool smooth)
        {
            float3 weights = new float3(elbowOut, elbowBack, elbowDown);
            float3 axis = math.normalizesafe(hand - shoulder, state.initialized ? state.axis : -back);
            float3 previous = state.initialized ? ElbowPoleMath.TransportBend(state.axis, axis, state.bend) : outward;
            if (proximityElbowBias)
            {
                // Yaw-only chest frame; radii are world meters, independent of model scale.
                float3 localHand = math.mul(math.inverse((quaternion)chest.rotation), hand - (float3)chest.position);
                float torso = ElbowPoleMath.TorsoInfluence(localHand, torsoBiasCenter, torsoBiasRadii, torsoFullBiasFraction);
                float face = ElbowPoleMath.Influence(math.distance(hand, eyes), faceFullBiasDistance, faceBiasDistance);
                float2 influences = new float2(torso, face);
                float k = !smooth || !state.initialized ? 1f : 1f - math.exp(-math.max(0.1f, elbowInfluenceSmooth) * dt);
                state.influences = math.lerp(state.influences, influences, k);
                weights = math.lerp(weights, (float3)torsoElbowWeights, state.influences.x);
                weights = math.lerp(weights, (float3)faceElbowWeights, state.influences.y);
            }
            else state.influences = float2.zero;
            float3 preferred = math.normalizesafe(outward * weights.x + back * weights.y - up * weights.z, outward);
            float3 bend = ElbowPoleMath.HysteresisTarget(axis, preferred, previous, outward, ref state.holdingBase);
            if (smooth && state.initialized)
                bend = ElbowPoleMath.SmoothBend(axis, previous, bend, poleSmooth, elbowMaxSpeed, dt);
            state.axis = axis;
            state.bend = math.normalizesafe(bend, previous);
            state.initialized = true;
            return state.bend;
        }


        Vector3 _headScale = Vector3.one;
        bool _headScaleCaptured;

        /// <summary>
        /// Flattens the head bone so the view inside it disappears, or restores the bone.
        /// Eyes and lips hang off this bone, so the flattened scale hides them too.
        /// </summary>
        public void SetHeadHidden(bool hidden)
        {
            if (head == null)
                return;

            if (!_headScaleCaptured)
            {
                _headScale = head.localScale;
                _headScaleCaptured = true;
            }

            head.localScale = hidden
                ? new Vector3(_headScale.x, 0f, 0f)
                : _headScale;
        }

        SkinnedMeshRenderer[] _skinned;

        /// <summary>
        /// Makes this rig's skinned meshes read the current bones for the camera about to draw.
        /// The head and face are changed after the normal skinning pass.
        /// </summary>
        public void SetSkinRecalculation(bool recalculate)
        {
            if (_skinned == null)
            {
                if (!recalculate)
                    return;
                _skinned = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            }

            for (int i = 0; i < _skinned.Length; i++)
            {
                SkinnedMeshRenderer skin = _skinned[i];
                if (skin != null)
                    skin.forceMatrixRecalculationPerRender = recalculate;
            }
        }

        void OnDrawGizmosSelected()
{
            if (head == null) return;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(head.TransformPoint(eyeOffset), 0.015f);
            // draw a small forward ray so you can see the view direction too
            Gizmos.DrawRay(head.TransformPoint(eyeOffset), head.forward * 0.2f);
            if (!proximityElbowBias || !showElbowBiasRegions || chest == null) return;
            Matrix4x4 oldMatrix = Gizmos.matrix;
            Gizmos.color = new Color(1f, 0.75f, 0.15f, 0.65f);
            Gizmos.DrawWireSphere(EyePosition, Mathf.Max(0.001f, faceBiasDistance));
            Gizmos.DrawWireSphere(EyePosition, Mathf.Clamp(faceFullBiasDistance, 0f, faceBiasDistance - 0.001f));
            Vector3 radii = new Vector3(Mathf.Max(0.001f, torsoBiasRadii.x), Mathf.Max(0.001f, torsoBiasRadii.y), Mathf.Max(0.001f, torsoBiasRadii.z));
            Gizmos.matrix = Matrix4x4.TRS(chest.position + chest.rotation * torsoBiasCenter, chest.rotation, radii);
            Gizmos.color = new Color(0.2f, 1f, 0.45f, 0.65f);
            Gizmos.DrawWireSphere(Vector3.zero, 1f);
            Gizmos.DrawWireSphere(Vector3.zero, Mathf.Clamp(torsoFullBiasFraction, 0f, 0.95f));
            Gizmos.matrix = oldMatrix;
        }
        
    }
}
