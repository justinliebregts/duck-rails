using Unity.Mathematics;

namespace Game.Animation
{
    public static class ElbowPoleMath
    {
        public static float3 TransportBend(float3 previousAxis, float3 axis, float3 bend)
        {
            float dot = math.clamp(math.dot(previousAxis, axis), -1f, 1f);
            // At a 180-degree reach reversal, rotating about the bend preserves it.
            if (dot < -0.9999f) return ProjectStable(axis, bend, bend, bend);
            quaternion rotation = math.normalize(new quaternion(new float4(math.cross(previousAxis, axis), 1f + dot)));
            return math.normalizesafe(math.rotate(rotation, bend), bend);
        }

        public static float3 HysteresisTarget(float3 axis, float3 preferred, float3 previous,
            float3 fallback, ref bool holding)
        {
            float3 projected = preferred - axis * math.dot(preferred, axis);
            float magnitude = math.length(projected);
            // Separate entry/exit thresholds prevent jitter from repeatedly switching branches.
            holding = magnitude < (holding ? 0.2f : 0.08f);
            return holding ? ProjectStable(axis, previous, previous, fallback)
                : math.normalizesafe(projected, fallback);
        }

        public static float3 SmoothBend(float3 axis, float3 from, float3 target, float rate,
            float maxDegreesPerSecond, float dt)
        {
            float angle = math.atan2(math.dot(axis, math.cross(from, target)), math.clamp(math.dot(from, target), -1f, 1f));
            float step = angle * (1f - math.exp(-math.max(0f, rate) * math.max(0f, dt)));
            float limit = math.radians(math.max(0f, maxDegreesPerSecond)) * math.max(0f, dt);
            step = math.clamp(step, -limit, limit);
            return math.normalizesafe(math.rotate(quaternion.AxisAngle(axis, step), from), from);
        }

        public static float Influence(float distance, float inner, float outer)
        {
            outer = math.max(outer, 0.001f);
            inner = math.clamp(inner, 0f, outer - 0.001f);
            float t = math.saturate((distance - inner) / (outer - inner));
            return 1f - t * t * (3f - 2f * t);
        }

        public static float TorsoInfluence(float3 hand, float3 center, float3 radii, float fullFraction)
        {
            float distance = math.length((hand - center) / math.max(radii, new float3(0.001f)));
            return Influence(distance, math.clamp(fullFraction, 0f, 0.95f), 1f);
        }

        public static float3 ProjectStable(float3 reach, float3 preferred, float3 previous, float3 outward)
        {
            float3 axis = math.normalizesafe(reach, new float3(0, 0, 1));
            float3 projected = preferred - axis * math.dot(preferred, axis);
            float3 retained = previous - axis * math.dot(previous, axis);
            float3 fallback = outward - axis * math.dot(outward, axis);
            // Choose a guaranteed nonparallel axis, including vertical and zero-length reaches.
            float3 reference = math.abs(axis.y) < 0.9f ? new float3(0, 1, 0) : new float3(0, 0, 1);
            fallback = math.normalizesafe(fallback, math.normalizesafe(math.cross(axis, reference)));
            retained = math.normalizesafe(retained, fallback);
            float magnitude = math.length(projected);
            // In the singular cone, keep the previous bend. Fade back to the preferred
            // direction outside it rather than amplifying tiny projection errors.
            float blend = 1f - Influence(magnitude, 0.05f, 0.2f);
            return math.normalizesafe(math.lerp(retained, math.normalizesafe(projected, retained), blend), retained);
        }
    }
}
