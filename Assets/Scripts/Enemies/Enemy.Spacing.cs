using UnityEngine;

namespace Game.AI
{
    public partial class Enemy
    {
        internal void SlideRemoteLanding(Vector3 target, Quaternion rotation)
        {
            if (_jump == null || !root || target.y >= root.position.y) return;
            // Delayed samples can cut the corner of a descending Smasher's
            // route around a body. Advance its horizontal interpolation first,
            // then descend at the accepted position, without a snapshot jump.
            SetRootPose(new Vector3(target.x, root.position.y, target.z), rotation);
            SetRootPose(new Vector3(root.position.x, target.y, root.position.z), rotation);
        }

        // Minkowski sum of the two upright capsule axes. A climbing spider uses a zero-length axis.
        internal Vector3 LimitNeighbors(Vector3 from, Vector3 to, out Enemy blocker, bool snapshot = false)
        {
            blocker = null;
            if (from == to || IsDormant) return to;
            Vector3 delta = to - from;
            Vector3 min = Vector3.Min(from, to) - Vector3.one * 6f;
            Vector3 max = Vector3.Max(from, to) + Vector3.one * 6f;
            Vector3 lower = default;
            float axis = 0, radius = -1, fraction = 1;
            foreach (var other in Active)
            {
                if (other == this || other.IsDormant || !other.root) continue;
                Vector3 p = other.root.position;
                if (p.x < min.x || p.z < min.z || p.y < min.y || p.x > max.x || p.z > max.z || p.y > max.y) continue;
                if (radius < 0) SpacingBody(out lower, out axis, out radius);
                other.SpacingBody(out var offset, out float height, out float width);
                Vector3 start = from + lower - p - offset;
                float gap = radius + width + .12f;
                float hit = BodyContact(start, delta, -axis, height, gap);
                if (hit >= fraction) continue;
                // A discontinuous authoritative snapshot can legitimately be on the far side.
                if (snapshot && AxisOffset(start + delta, -axis, height).sqrMagnitude > gap * gap) continue;
                fraction = hit;
                blocker = other;
            }
            return fraction < 1 ? from + delta * Mathf.Max(0, fraction - .0001f) : to;
        }

        static Vector3 AxisOffset(Vector3 point, float bottom, float top)
        {
            point.y -= Mathf.Clamp(point.y, bottom, top);
            return point;
        }

        static float BodyContact(Vector3 start, Vector3 step, float bottom, float top, float radius)
        {
            Vector3 normal = AxisOffset(start, bottom, top);
            if (normal.sqrMagnitude <= radius * radius)
                return Vector3.Dot(normal, step) >= 0 ? 1 : 0;
            Vector3 flat = new Vector3(start.x, 0, start.z);
            float side = SphereEntry(flat, new Vector3(step.x, 0, step.z), radius);
            float y = start.y + side * step.y;
            if (side < 1 && y >= bottom && y <= top) return side;
            return Mathf.Min(SphereEntry(start - Vector3.up * bottom, step, radius),
                SphereEntry(start - Vector3.up * top, step, radius));
        }

        static float SphereEntry(Vector3 origin, Vector3 step, float radius)
        {
            float a = step.sqrMagnitude, b = Vector3.Dot(origin, step);
            if (a < 1e-12f || b >= 0) return 1;
            float c = origin.sqrMagnitude - radius * radius;
            float disc = b * b - a * c;
            return disc <= 0 ? 1 : Mathf.Clamp01(c / (-b + Mathf.Sqrt(disc)));
        }
    }
}
