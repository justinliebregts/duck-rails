using Unity.Mathematics;

namespace Game.Animation
{
    public struct TwoBoneResult
    {
        public float3 mid;     // elbow position
        public float3 end;     // solved end position (== target when reachable)
        public bool   reached; // false if target was outside [ |l1-l2|, l1+l2 ]
    }

    public static class TwoBoneIK
    {
        /// <summary>
        /// Exact closed-form 2-bone solve: chain rooted at 'root', reaching for 'target',
        /// middle joint bending toward 'poleDir'. l1/l2 = bone lengths.
        /// </summary>
        public static TwoBoneResult Solve(float3 root, float3 target, float3 poleDir, float l1, float l2)
        {
            var r = new TwoBoneResult();

            float3 toT = target - root;
            float  d   = math.length(toT);

            float dMax = l1 + l2 - 1e-3f;
            float dMin = math.abs(l1 - l2) + 1e-3f;
            float dc   = math.clamp(d, dMin, dMax);
            r.reached  = d <= dMax;

            float3 dir = d > 1e-6f ? toT / d : new float3(0f, 0f, 1f);

            // Angle at root between root->target and root->elbow (law of cosines)
            float cosA = math.clamp((l1 * l1 + dc * dc - l2 * l2) / (2f * l1 * dc), -1f, 1f);
            float a     = math.acos(cosA);

            // Bend axis: perpendicular to root->target, on the pole's side.
            // Degenerates if pole is parallel to the reach direction -> fallback axis.
            float3 axis     = math.cross(dir, poleDir);
            float  axisLenSq = math.lengthsq(axis);
            float3 bendAxis = axisLenSq > 1e-10f
                ? axis * math.rsqrt(axisLenSq)
                : math.normalize(math.cross(dir, new float3(0f, 1f, 0f)));

            float3 elbowDir = math.mul(quaternion.AxisAngle(bendAxis, a), dir);
            r.mid = root + elbowDir * l1;
            r.end = r.reached ? target : root + dir * dc;
            return r;
        }
    }
}