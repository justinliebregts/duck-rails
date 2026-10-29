using Unity.Profiling;
using UnityEngine;

namespace Game.AI
{
    // Counters describe instrumented queries, not every Physics call in the game.
    public static class AIQueryProfile
    {
        public static readonly ProfilerMarker TargetGround = new ProfilerMarker("AIPhysics.TargetGround");
        public static readonly ProfilerMarker Support = new ProfilerMarker("AIPhysics.DuckSupport");
        public static readonly ProfilerMarker Vision = new ProfilerMarker("AIPhysics.EnemyVision");
        public static readonly ProfilerMarker Sweep = new ProfilerMarker("AIPhysics.DuckSweep");
        public static readonly ProfilerMarker Recovery = new ProfilerMarker("AIPhysics.DuckRecovery");
        public static long TargetRequests, TargetRaycasts, TargetHits, SupportRequests, SupportSpherecasts, SupportRaycasts, VisionRaycasts, SweepRequests;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset() => TargetRequests = TargetRaycasts = TargetHits = SupportRequests = SupportSpherecasts = SupportRaycasts = VisionRaycasts = SweepRequests = 0;
        public static bool VisionCast(Vector3 origin, Vector3 direction, out RaycastHit hit, float distance, int mask)
        {
            using var sample = Vision.Auto();
            VisionRaycasts++;
            return Physics.Raycast(origin, direction, out hit, distance, mask, QueryTriggerInteraction.Ignore);
        }
    }

    // A bounded observation snapshot for planning only. Never use this for contact,
    // attack visibility or movement clearance. New colliders can enter a cached ray
    // during a frame; movement must always validate against fresh physics queries.
    public sealed class TargetGroundSnapshot
    {
        struct Entry
        {
            public Vector3 origin;
            public float distance;
            public int mask;
            public RaycastHit hit;
            public Matrix4x4 matrix;
            public bool found;
        }
        readonly Entry[] entries = new Entry[16];
        int frame = -1, count;
        public void Clear() { frame = -1; count = 0; }
        public bool Query(Vector3 origin, float distance, int mask, out RaycastHit hit, bool reuse)
        {
            using var sample = AIQueryProfile.TargetGround.Auto();
            AIQueryProfile.TargetRequests++;
            if (frame != Time.frameCount) { frame = Time.frameCount; count = 0; }
            if (reuse)
                for (int i = 0; i < count; i++)
                {
                    ref Entry e = ref entries[i];
                    if (!e.origin.Equals(origin) || e.distance != distance || e.mask != mask) continue;
                    if (e.found && (e.hit.collider == null || !e.hit.collider.enabled || !e.hit.collider.gameObject.activeInHierarchy
                        || e.hit.collider.transform.localToWorldMatrix != e.matrix)) continue;
                    hit = e.hit;
                    AIQueryProfile.TargetHits++;
                    return e.found;
                }
            AIQueryProfile.TargetRaycasts++;
            bool found = Physics.Raycast(origin, Vector3.down, out hit, distance, mask, QueryTriggerInteraction.Ignore);
            if (reuse && count < entries.Length)
                entries[count++] = new Entry { origin = origin, distance = distance, mask = mask, hit = hit, found = found,
                    matrix = found ? hit.collider.transform.localToWorldMatrix : default };
            return found;
        }
    }
}
