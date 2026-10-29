using UnityEngine;

namespace Game.Audio
{
    /// <summary>Listener-based box membership; no trigger colliders or physics callbacks required.</summary>
    [AddComponentMenu("DuckZ/Audio/Ambience Zone")]
    public sealed class AmbienceZone : MonoBehaviour
    {
        [SerializeField] Vector3 size = new Vector3(10f, 5f, 10f);
        [Tooltip("World-space boundary slack while an emitter is active.")]
        [SerializeField, Min(0f)] float hysteresis = 0.75f;

        public bool Allows(Vector3 listenerPosition, bool outside, bool wasAllowed)
        {
            if (!isActiveAndEnabled) return outside;
            float margin = wasAllowed ? Mathf.Max(0f, hysteresis) * (outside ? -1f : 1f) : 0f;
            bool inside = Contains(listenerPosition, margin);
            return outside ? !inside : inside;
        }

        bool Contains(Vector3 world, float margin)
        {
            Vector3 local = transform.InverseTransformPoint(world);
            Vector3 scale = transform.lossyScale;
            Vector3 half = new Vector3(
                Mathf.Max(0f, Mathf.Abs(size.x) * 0.5f + margin / Mathf.Max(0.001f, Mathf.Abs(scale.x))),
                Mathf.Max(0f, Mathf.Abs(size.y) * 0.5f + margin / Mathf.Max(0.001f, Mathf.Abs(scale.y))),
                Mathf.Max(0f, Mathf.Abs(size.z) * 0.5f + margin / Mathf.Max(0.001f, Mathf.Abs(scale.z))));
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.7f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
    }
}
