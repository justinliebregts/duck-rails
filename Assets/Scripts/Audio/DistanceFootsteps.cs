using UnityEngine;

namespace Game.Audio
{
    /// <summary>Call Tick from grounded locomotion, using the body root, never the headset.</summary>
    [RequireComponent(typeof(FootstepEmitter))]
    public sealed class DistanceFootsteps : MonoBehaviour
    {
        [SerializeField, Min(0.05f)] float metresPerStep = 0.8f;
        [SerializeField, Min(0.1f)] float teleportDistance = 2f;
        FootstepEmitter emitter;
        Vector3 previous;
        Transform previousFrame;
        float travelled;
        bool initialized;
        void Awake() => emitter = GetComponent<FootstepEmitter>();
        public void Tick(Vector3 bodyPosition, bool grounded, Vector3 up, Transform referenceFrame = null)
        {
            if (!isActiveAndEnabled) return;
            Vector3 position = referenceFrame != null ? referenceFrame.InverseTransformPoint(bodyPosition) : bodyPosition;
            if (!initialized || previousFrame != referenceFrame)
            {
                previous = position; previousFrame = referenceFrame; initialized = true; travelled = 0f; return;
            }
            Vector3 delta = position - previous;
            previous = position;
            // Count walking within a moving carriage; emit the sound at the world-space foot position.
            if (referenceFrame != null) delta = referenceFrame.TransformVector(delta);
            if (!grounded || delta.sqrMagnitude > teleportDistance * teleportDistance) { travelled = 0f; return; }
            travelled += Vector3.ProjectOnPlane(delta, up).magnitude;
            if (travelled < metresPerStep) return;
            travelled %= Mathf.Max(0.05f, metresPerStep);
            emitter.PlayAt(bodyPosition, up);
        }
        public void ResetDistance() { initialized = false; previousFrame = null; travelled = 0f; }
        void OnDisable() => ResetDistance();
    }
}
