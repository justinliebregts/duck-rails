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
        float travelled;
        bool initialized;
        void Awake() => emitter = GetComponent<FootstepEmitter>();
        public void Tick(Vector3 bodyPosition, bool grounded, Vector3 up)
        {
            if (!isActiveAndEnabled) return;
            if (!initialized) { previous = bodyPosition; initialized = true; return; }
            Vector3 delta = bodyPosition - previous;
            previous = bodyPosition;
            if (!grounded || delta.sqrMagnitude > teleportDistance * teleportDistance) { travelled = 0f; return; }
            travelled += Vector3.ProjectOnPlane(delta, up).magnitude;
            if (travelled < metresPerStep) return;
            travelled %= Mathf.Max(0.05f, metresPerStep);
            emitter.PlayAt(bodyPosition, up);
        }
        public void ResetDistance() { initialized = false; travelled = 0f; }
        void OnDisable() => ResetDistance();
    }
}
