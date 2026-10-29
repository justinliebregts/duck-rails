using UnityEngine;

namespace Game.Audio
{
    public sealed class AmbientEmitter : MonoBehaviour
    {
        [SerializeField] SoundEvent sound;
        [SerializeField] bool loop;
        [Tooltip("Optional fixed sound locations. Empty uses this object's position.")]
        [SerializeField] Transform[] points = new Transform[0];
        [SerializeField] Vector2 interval = new Vector2(12f, 30f);
        [SerializeField, Min(0f)] float activationDistance = 40f;
        [Tooltip("Loops only. Extra distance past Activation Distance before a playing loop fades out, so it doesn't flicker on and off when the listener hovers near the edge.")]
        [SerializeField, Min(0f)] float exitHysteresis = 3f;
        [SerializeField, Min(0f)] float fadeOut = 0.5f;
        [SerializeField, Min(0f)] float fadeIn = 0.5f;
        [Tooltip("Optional listener zone, in addition to the source's activation distance.")]
        [SerializeField] AmbienceZone zone;
        [Tooltip("Play outside the assigned zone, for example outdoor ambience excluded from the subway.")]
        [SerializeField] bool outsideZone;
        AudioHandle handle;
        Transform loopPoint;
        float nextSound, nextCheck;
        bool zoneAllowed, heldLoop;
        public bool IsPlaying => handle.IsPlaying;

        void OnEnable()
        {
            if (loop) nextSound = Time.time;
            else Schedule();
            nextCheck = 0f;
            zoneAllowed = heldLoop = false;
        }
        void Schedule() => nextSound = Time.time + Random.Range(
            Mathf.Max(0.1f, Mathf.Min(interval.x, interval.y)), Mathf.Max(0.1f, Mathf.Max(interval.x, interval.y)));

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.25f;
            var manager = AudioManager.instance;
            if (manager == null || sound == null) return;
            var listener = manager.ListenerTransform;
            zoneAllowed = listener != null && (zone == null || zone.Allows(listener.position, outsideZone, zoneAllowed));
            if (loop)
            {
                if (handle.IsPlaying)
                {
                    if (!zoneAllowed || loopPoint == null || !InRange(loopPoint, listener, activationDistance + exitHysteresis))
                        handle.Stop(fadeOut);
                    return;
                }
                if (heldLoop)
                {
                    // A reclaimed or faded loop must not immediately refill a gameplay slot.
                    heldLoop = false;
                    nextSound = Time.time + 1f;
                }
                if (!zoneAllowed) return;
                if (Time.time < nextSound) return;
                loopPoint = ChoosePoint(listener);
                if (loopPoint != null) handle = manager.PlayAmbience(sound, loopPoint, this, true, fadeIn);
                heldLoop = handle.IsPlaying;
                // Throttle retries when the pool or event concurrency limit is full.
                nextSound = Time.time + 1f;
                return;
            }
            if (Time.time < nextSound) return;
            var point = zoneAllowed ? ChoosePoint(listener) : null;
            if (point != null) manager.PlayAmbience(sound, point, this, false);
            Schedule();
        }

        bool InRange(Transform point, Transform listener, float distance)
            => point != null && point.gameObject.activeInHierarchy && (activationDistance <= 0f
                || (listener != null && (point.position - listener.position).sqrMagnitude <= distance * distance));

        Transform ChoosePoint(Transform listener)
        {
            if (points.Length == 0) return InRange(transform, listener, activationDistance) ? transform : null;
            // Reservoir sampling: uniform among eligible points, without a temporary list.
            Transform chosen = null;
            int count = 0;
            foreach (var point in points)
                if (InRange(point, listener, activationDistance) && Random.Range(0, ++count) == 0) chosen = point;
            return chosen;
        }
        void OnDisable() { handle.Stop(); loopPoint = null; heldLoop = false; }
        void OnDrawGizmosSelected()
        {
            if (activationDistance <= 0f) return;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
            if (points.Length == 0) Gizmos.DrawWireSphere(transform.position, activationDistance);
            else foreach (var point in points) if (point != null) Gizmos.DrawWireSphere(point.position, activationDistance);
        }
    }
}
