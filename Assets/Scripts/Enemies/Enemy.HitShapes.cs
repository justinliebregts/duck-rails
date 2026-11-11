using UnityEngine;

namespace Game.AI
{
    public partial class Enemy
    {
        [System.Serializable]
        struct HitCapsule
        {
            public Transform start, end;
            public Vector3 startOffset, endOffset;
            [Min(0.001f)] public float radius;
        }

        [Header("Weapon hit shapes")]
        [Tooltip("Maximum distance from the root to any posed hit surface, in root-local metres.")]
        [SerializeField, Min(0f)] float swingReach;
        [Tooltip("Offsets are bone-local. Radius is in root-local metres. Leave End empty for a sphere.")]
        [SerializeField] HitCapsule[] hitShapes = System.Array.Empty<HitCapsule>();

        struct WorldCapsule
        {
            public Vector3 start, end;
            public float radius;

            public Vector3 ClosestAxisPoint(Vector3 point)
            {
                Vector3 axis = end - start;
                float lengthSq = axis.sqrMagnitude;
                return lengthSq > 0f
                    ? start + axis * Mathf.Clamp01(Vector3.Dot(point - start, axis) / lengthSq)
                    : start;
            }
        }

        WorldCapsule[] swingPose;
        int swingShapeCount;

        void AllocateSwingPose()
        {
            int count = hitShapes.Length > 0 ? hitShapes.Length : 2 + _legs.Length;
            if (swingPose == null || swingPose.Length != count) swingPose = new WorldCapsule[count];
        }

        float HitScale()
        {
            Vector3 s = root.lossyScale;
            return Mathf.Max(Mathf.Abs(s.x), Mathf.Max(Mathf.Abs(s.y), Mathf.Abs(s.z)));
        }

        // The item bounds include every sample at both ends of its sweep plus its radius.
        internal bool WithinSwingReach(Bounds sweep)
        {
            if (IsDormant || root == null) return false;
            if (swingReach <= 0f) return true; // Older or procedurally created enemies.
            float reach = swingReach * HitScale();
            return sweep.SqrDistance(root.position) <= reach * reach;
        }

        void AddSwingShape(Vector3 start, Vector3 end, float radius)
        {
            swingPose[swingShapeCount++] = new WorldCapsule { start = start, end = end, radius = radius };
        }

        // Snapshot once per candidate. All weapon samples and contact refinement use the same pose.
        internal void PrepareSwingPose()
        {
            AllocateSwingPose();
            swingShapeCount = 0;
            if (IsDormant || root == null) return;
            if (hitShapes.Length == 0)
            {
                AddSwingShape(root.position, root.position + root.up * bodyHeight, bodyRadius);
                if (_gait is Game.Animation.SpiderGait && head != null)
                    AddSwingShape(head.position, head.position + head.forward * .235f, bodyRadius);
                foreach (var leg in _legs)
                    if (leg != null) AddSwingShape(leg.HipWorld, leg.FootWorld, limbRadius);
                return;
            }

            float scale = HitScale();
            foreach (var shape in hitShapes)
            {
                if (shape.start == null) continue;
                Vector3 start = shape.start.TransformPoint(shape.startOffset);
                Vector3 end = shape.end != null ? shape.end.TransformPoint(shape.endOffset) : start;
                AddSwingShape(start, end, shape.radius * scale);
            }
        }

        int FirstOverlap(Vector3 from, Vector3 to, float radius)
        {
            for (int i = 0; i < swingShapeCount; i++)
            {
                ref var shape = ref swingPose[i];
                if (SegmentsWithin(from, to, shape.start, shape.end, radius + shape.radius)) return i;
            }
            return -1;
        }

        internal bool OverlapsPreparedSwing(Vector3 from, Vector3 to, float radius)
            => FirstOverlap(from, to, radius) >= 0;

        public bool OverlapsSwing(Vector3 from, Vector3 to, float sweepRadius)
        {
            PrepareSwingPose();
            return OverlapsPreparedSwing(from, to, sweepRadius);
        }

        public Vector3 HitBloodPosition(Vector3 from, Vector3 to, float sweepRadius)
        {
            PrepareSwingPose();
            return SwingContact(from, to, sweepRadius, out _, out _);
        }

        internal Vector3 SwingContact(Vector3 from, Vector3 to, float radius, out Vector3 normal, out float separation)
        {
            TrySwingContact(from, to, radius, 1f, out var contact);
            normal = contact.normal;
            separation = contact.separation;
            return contact.position;
        }

        internal struct SweepContact
        {
            public Vector3 position, normal;
            public float separation;
            // Entry is bracketed by [before, fraction]. Testing only up to 'before'
            // skips contacts already tied to the precision of the refinement.
            public float before, fraction;
        }

        internal bool TrySwingContact(Vector3 from, Vector3 to, float radius, float limit, out SweepContact result)
        {
            result = default;
            result.position = to;
            Vector3 delta = to - from;
            int hit = FirstOverlap(from, Vector3.LerpUnclamped(from, to, limit), radius);
            if (hit < 0) return false;
            Vector3 direction = delta.normalized;
            result.normal = -direction;
            if (delta.sqrMagnitude < 1e-12f) return true;
            int startedInside = FirstOverlap(from, from, radius);
            if (startedInside >= 0)
            {
                // Back up past every shape along the approach direction.
                float back = 0f;
                for (int i = 0; i < swingShapeCount; i++)
                {
                    ref var shape = ref swingPose[i];
                    back = Mathf.Max(back, Mathf.Max(Vector3.Dot(from - shape.start, direction),
                        Vector3.Dot(from - shape.end, direction)) + shape.radius + radius);
                }
                from -= direction * (back + 0.02f);
                limit = 1f; // Refinement now walks the extended path for the visual contact.
            }
            float low = 0f, high = limit;
            for (int i = 0; i < 12; i++)
            {
                float mid = (low + high) * 0.5f;
                int candidate = FirstOverlap(from, Vector3.LerpUnclamped(from, to, mid), radius);
                if (candidate >= 0) { high = mid; hit = candidate; }
                else low = mid;
            }
            Vector3 entry = Vector3.LerpUnclamped(from, to, high);
            // A release inside a limb can already be travelling out of it. Use
            // the item's current clearance there so it cannot be pulled back
            // across the limb to the reconstructed blood position.
            ref var contact = ref swingPose[startedInside >= 0 ? startedInside : hit];
            Vector3 sample = startedInside >= 0 ? to : entry;
            Vector3 outward = sample - contact.ClosestAxisPoint(sample);
            if (outward.sqrMagnitude > 1e-10f) result.normal = outward.normalized;
            result.separation = startedInside >= 0
                ? Mathf.Max(0f, contact.radius + radius - outward.magnitude)
                : Mathf.Max(0f, Vector3.Dot(entry - to, result.normal));
            result.position = Vector3.LerpUnclamped(from, to, low) - direction * 0.02f;
            if (startedInside < 0)
            {
                result.before = low;
                result.fraction = high;
            }
            return true;
        }

#if UNITY_EDITOR
        void DrawSwingShapes()
        {
            PrepareSwingPose();
            Gizmos.color = new Color(1f, 0.6f, 0.1f, 0.8f);
            for (int i = 0; i < swingShapeCount; i++)
            {
                ref var s = ref swingPose[i];
                Gizmos.DrawWireSphere(s.start, s.radius);
                Vector3 axis = s.end - s.start;
                if (axis.sqrMagnitude < 1e-10f) continue;
                Gizmos.DrawWireSphere(s.end, s.radius);
                Vector3 side = Vector3.Cross(axis, Vector3.up);
                if (side.sqrMagnitude < 1e-10f) side = Vector3.Cross(axis, Vector3.right);
                side = side.normalized * s.radius;
                Vector3 across = Vector3.Cross(axis, side).normalized * s.radius;
                Gizmos.DrawLine(s.start + side, s.end + side);
                Gizmos.DrawLine(s.start - side, s.end - side);
                Gizmos.DrawLine(s.start + across, s.end + across);
                Gizmos.DrawLine(s.start - across, s.end - across);
            }
        }
#endif
    }
}
