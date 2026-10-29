using UnityEngine;

namespace Game.AI
{
    /// <summary>
    /// Oriented box the crawl-graph bake treats as out of bounds. The box is the
    /// BoxCollider on this object, so it can be edited with the scene-view collider handles.
    /// Nodes inside it are dropped and edges that cross it are cut, so a skirt along the world
    /// lip disconnects the sides and underside for seed pruning. The collider is kept as a
    /// trigger so it does not affect gameplay or the bake's surface raycasts.
    /// </summary>
    [RequireComponent(typeof(BoxCollider))]
    public class CrawlGraphBlocker : MonoBehaviour
    {
        BoxCollider _box;

        BoxCollider Box
        {
            get
            {
                if (_box == null) _box = GetComponent<BoxCollider>();
                return _box;
            }
        }

        void Reset()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(4f, 10f, 40f);
        }

        void OnValidate()
        {
            BoxCollider box = GetComponent<BoxCollider>();
            if (box != null && !box.isTrigger) box.isTrigger = true;
        }

        public bool Contains(Vector3 world)
        {
            if (!TryLocalBox(out Vector3 center, out Vector3 half)) return false;
            return Inside(transform.InverseTransformPoint(world) - center, half);
        }

        /// <summary>True when the world AABB is fully inside this box. Used to skip covered chunks.</summary>
        public bool ContainsBounds(Vector3 min, Vector3 max)
        {
            if (!TryLocalBox(out Vector3 center, out Vector3 half)) return false;
            return Inside(transform.InverseTransformPoint(new Vector3(min.x, min.y, min.z)) - center, half)
                && Inside(transform.InverseTransformPoint(new Vector3(max.x, min.y, min.z)) - center, half)
                && Inside(transform.InverseTransformPoint(new Vector3(min.x, max.y, min.z)) - center, half)
                && Inside(transform.InverseTransformPoint(new Vector3(max.x, max.y, min.z)) - center, half)
                && Inside(transform.InverseTransformPoint(new Vector3(min.x, min.y, max.z)) - center, half)
                && Inside(transform.InverseTransformPoint(new Vector3(max.x, min.y, max.z)) - center, half)
                && Inside(transform.InverseTransformPoint(new Vector3(min.x, max.y, max.z)) - center, half)
                && Inside(transform.InverseTransformPoint(new Vector3(max.x, max.y, max.z)) - center, half);
        }

        /// <summary>True when the world segment intersects this box, including a segment that starts inside.</summary>
        public bool IntersectsSegment(Vector3 a, Vector3 b)
        {
            if (!TryLocalBox(out Vector3 center, out Vector3 half)) return false;
            Vector3 localA = transform.InverseTransformPoint(a) - center;
            Vector3 localB = transform.InverseTransformPoint(b) - center;
            Vector3 delta = localB - localA;
            float t0 = 0f;
            float t1 = 1f;
            if (!Slab(localA.x, delta.x, -half.x, half.x, ref t0, ref t1)) return false;
            if (!Slab(localA.y, delta.y, -half.y, half.y, ref t0, ref t1)) return false;
            if (!Slab(localA.z, delta.z, -half.z, half.z, ref t0, ref t1)) return false;
            return t0 <= t1;
        }

        bool TryLocalBox(out Vector3 center, out Vector3 half)
        {
            BoxCollider box = Box;
            if (box == null)
            {
                center = default;
                half = default;
                return false;
            }

            center = box.center;
            Vector3 size = box.size;
            half = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z)) * 0.5f;
            return true;
        }

        static bool Inside(Vector3 local, Vector3 half)
        {
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        static bool Slab(float origin, float delta, float min, float max, ref float t0, ref float t1)
        {
            if (Mathf.Abs(delta) < 1e-8f)
                return origin >= min && origin <= max;

            float inv = 1f / delta;
            float ta = (min - origin) * inv;
            float tb = (max - origin) * inv;
            if (ta > tb)
            {
                float swap = ta;
                ta = tb;
                tb = swap;
            }
            if (ta > t0) t0 = ta;
            if (tb < t1) t1 = tb;
            return t0 <= t1;
        }

        void OnDrawGizmosSelected()
        {
            BoxCollider box = Box;
            if (box == null) return;

            Matrix4x4 previous = Gizmos.matrix;
            Gizmos.color = new Color(1f, 0.25f, 0.3f, 0.9f);
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.center, new Vector3(Mathf.Abs(box.size.x), Mathf.Abs(box.size.y), Mathf.Abs(box.size.z)));
            Gizmos.matrix = previous;
        }
    }
}
