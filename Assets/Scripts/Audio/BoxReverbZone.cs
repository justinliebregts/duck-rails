using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// Box-shaped reverb for hallways and rooms. Unity zones are spheres, so this moves a child
    /// AudioReverbZone to the point in the box nearest the listener: full reverb anywhere inside,
    /// fading out over Fade Distance outside.
    /// </summary>
    [AddComponentMenu("DuckZ/Audio/Box Reverb Zone")]
    public sealed class BoxReverbZone : MonoBehaviour
    {
        [Tooltip("Box size in this object's local space. Rotate the object to align it with the hallway.")]
        [SerializeField] Vector3 size = new Vector3(4f, 3f, 20f);
        [Tooltip("How far outside the box the reverb fades to nothing.")]
        [SerializeField, Min(0.1f)] float fadeDistance = 2f;
        [Tooltip("Child zone that holds the reverb preset. Its Min/Max Distance are driven by Fade Distance.")]
        [SerializeField] AudioReverbZone zone;

        void Awake() => ApplyDistances();
        void OnValidate() => ApplyDistances();

        void ApplyDistances()
        {
            if (zone == null) return;
            zone.minDistance = 0.01f;
            zone.maxDistance = fadeDistance;
        }

        void LateUpdate()
        {
            var manager = AudioManager.instance;
            var listener = manager != null ? manager.ListenerTransform : null;
            if (zone == null || listener == null) return;
            Vector3 local = transform.InverseTransformPoint(listener.position);
            Vector3 half = size * 0.5f;
            local.x = Mathf.Clamp(local.x, -half.x, half.x);
            local.y = Mathf.Clamp(local.y, -half.y, half.y);
            local.z = Mathf.Clamp(local.z, -half.z, half.z);
            zone.transform.position = transform.TransformPoint(local);
        }

        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.8f);
            Gizmos.DrawWireCube(Vector3.zero, size);
            Gizmos.color = new Color(0.3f, 0.6f, 1f, 0.2f);
            Gizmos.DrawWireCube(Vector3.zero, size + Vector3.one * (fadeDistance * 2f));
        }
    }
}
