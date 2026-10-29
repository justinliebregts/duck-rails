using UnityEngine;

namespace Game.Audio
{
    public sealed class FootstepEmitter : MonoBehaviour
    {
        [SerializeField] SurfaceSoundSet sounds;
        [Tooltip("Multiplies every step's volume, on top of the SoundEvent's own volume range.")]
        [SerializeField, Range(0f, 1f)] float volume = 1f;
        [SerializeField] Transform leftFoot;
        [SerializeField] Transform rightFoot;
        [SerializeField] LayerMask groundMask = ~0;
        [SerializeField, Min(0.01f)] float probeDistance = 0.3f;
        [Tooltip("Limits clusters of procedural feet landing together. Zero allows every contact.")]
        [SerializeField, Min(0f)] float minimumInterval = 0.06f;
        float nextStep;

        public void LeftFoot() => PlayAt(leftFoot != null ? leftFoot.position : transform.position, transform.up);
        public void RightFoot() => PlayAt(rightFoot != null ? rightFoot.position : transform.position, transform.up);
        public void Step() => PlayAt(transform.position, transform.up);
        public void PlayAt(Vector3 point, Vector3 normal)
        {
            if (!isActiveAndEnabled || Time.time < nextStep || sounds == null) return;
            SurfaceType surface = null;
            Vector3 up = normal.sqrMagnitude > 0.001f ? normal.normalized : Vector3.up;
            if (Physics.Raycast(point + up * 0.1f, -up, out var hit, probeDistance + 0.1f,
                groundMask, QueryTriggerInteraction.Ignore))
            {
                point = hit.point;
                surface = AudioSurface.TypeOf(hit.collider);
            }
            var sound = sounds.Resolve(surface);
            if (sound == null || AudioManager.instance == null) return;
            nextStep = Time.time + minimumInterval;
            AudioManager.instance.Play(sound, point, volume);
        }
        void OnEnable() => nextStep = 0f;
    }
}
