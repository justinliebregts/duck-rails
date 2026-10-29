using Unity.Mathematics;
using UnityEngine;

namespace Game.Animation
{
    /// <summary>Six finger curls, each 0 (open) to 1 (closed).</summary>
    public struct FingerCurl
    {
        public float rightThumb, rightIndex, rightPinky;
        public float leftThumb, leftIndex, leftPinky;

        public static FingerCurl Lerp(FingerCurl a, FingerCurl b, float u) => new FingerCurl
        {
            rightThumb = math.lerp(a.rightThumb, b.rightThumb, u),
            rightIndex = math.lerp(a.rightIndex, b.rightIndex, u),
            rightPinky = math.lerp(a.rightPinky, b.rightPinky, u),
            leftThumb = math.lerp(a.leftThumb, b.leftThumb, u),
            leftIndex = math.lerp(a.leftIndex, b.leftIndex, u),
            leftPinky = math.lerp(a.leftPinky, b.leftPinky, u),
        };
    }

    [System.Serializable]
    public class FingerJoint
    {
        public Transform bone;
        [Tooltip("Local axis this joint curls around (usually X).")]
        public Vector3 axis = new Vector3(1f, 0f, 0f);
        [Tooltip("Max curl in degrees at curl = 1.")]
        public float maxAngle = 70f;
        [Tooltip("Per-joint ratio — this is where the pose's look lives.")]
        public float weight = 1f;
    }

    [System.Serializable]
    public class Finger
    {
        public FingerJoint[] joints;
        [Tooltip("Input -> curl response. Linear default; use a pow curve for a stiffer start.")]
        public AnimationCurve response = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        quaternion[] _rest;
        public void Capture()
        {
            if (joints == null)
            {
                _rest = null;
                return;
            }
            _rest = new quaternion[joints.Length];
            for (int i = 0; i < joints.Length; i++)
            {
                var j = joints[i];
                if (j == null || j.bone == null)
                    continue;
                if (j.axis == Vector3.zero) j.axis = new Vector3(1f, 0f, 0f);  // runtime guard
                _rest[i] = j.bone.localRotation;
            }
        }
        
        public void Apply(float input)
        {
            // Inspector selection re-deserializes [Serializable] Finger and drops _rest.
            if (joints == null || joints.Length == 0)
                return;
            if (_rest == null || _rest.Length != joints.Length)
                Capture();
            if (_rest == null)
                return;

            float curl = response != null
                ? response.Evaluate(math.clamp(input, 0f, 1f))
                : math.clamp(input, 0f, 1f);
            for (int i = 0; i < joints.Length; i++)
            {
                var j = joints[i];
                if (j == null || j.bone == null)
                    continue;
                j.bone.localRotation = math.mul(_rest[i],
                    quaternion.AxisAngle(math.normalize(j.axis), math.radians(j.maxAngle * j.weight * curl)));
            }
        }
    }

    /// <summary>
    /// Procedural finger poses from two analog inputs.
    /// Local player: call SetInput from PlayerInput. Remote: call it from networked floats.
    /// Writes localRotation only, so it's order-independent vs. the arm IK solve.
    /// </summary>
    public class HandPoseDriver : MonoBehaviour
    {
        [SerializeField] Finger thumb, index, middle, ring, pinky;

        [Header("Input mapping")]
        [SerializeField, Range(0f, 1f)] float gripToThumb    = 0.95f;
        [SerializeField, Range(0f, 1f)] float gripToIndex    = 0.15f; // resting half-curl on a controller
        [SerializeField, Range(0f, 1f)] float triggerToThumb = 0.40f;

        [Header("Smoothing (mainly for remote hands)")]
        [SerializeField] float smooth = 0f; // 1/s; 0 = instant

        float _grip, _trigger;

        void Awake()
        {
            thumb?.Capture(); index?.Capture(); middle?.Capture(); ring?.Capture(); pinky?.Capture();
        }

        /// <summary>Owner pushes raw analog 0..1 values here (once per frame, before LateUpdate).</summary>
        public void SetInput(float grip, float trigger)
        {
            if (smooth <= 0f) { _grip = grip; _trigger = trigger; return; }
            float k = 1f - math.exp(-smooth * Time.deltaTime);
            _grip    += (grip - _grip) * k;
            _trigger += (trigger - _trigger) * k;
        }

        void LateUpdate() => Apply();

        public void Apply()
        {
            thumb?.Apply(math.saturate(math.max(_grip * gripToThumb, _trigger * triggerToThumb)));
            index?.Apply(math.saturate(_trigger + _grip * gripToIndex));
            middle?.Apply(_grip);
            ring?.Apply(_grip);
            pinky?.Apply(_grip);
        }
    }
}