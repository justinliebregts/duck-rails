using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stable per-route encounter choices using authored, navigation-checked points.</summary>
public static class RailEnemySpawns
{
    // Set to an enemy index (3 = RailSpider) to force a single type for playtests; -1 uses the normal enemy mix.
    public const int ForcedEnemyType = -1;
    // RailHook is appended so existing type indices (3 = RailSpider) stay stable. It is spawned only by
    // RailEnemySpawnBalance; the legacy Select/finale stream keeps the original four types.
    public static readonly string[] Names = { "RailZombie", "RailBomber", "RailSmasher", "RailSpider", "RailHook" };
    public static GameObject Prefab(int type) => Resources.Load<GameObject>("Rails/" + Names[type]);
    public static void Register() { for (int i = 0; i < Names.Length; i++) PhotonPreparePool.Register(Prefab(i)); }
    public static void Select(Transform[] candidates, System.Random rng, List<Transform> points, List<int> types)
    {
        if (candidates == null || candidates.Length < 6) throw new InvalidOperationException("An encounter needs six authored enemy candidates.");
        var order = (Transform[])candidates.Clone();
        for (int i = order.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        int count = rng.Next(4, 7);
        // Preserve the seeded shuffle/count/type stream, but never omit a rail side.
        foreach (int side in new[] { -1, 1 })
        {
            bool present = false;
            for (int i = 0; i < count; i++) present |= Side(order[i]) == side;
            if (present) continue;
            int replacement = Array.FindIndex(order, count, p => Side(p) == side);
            if (replacement < 0) throw new InvalidOperationException("Encounter has no enemy candidate on each rail side.");
            int slot = count - (side < 0 ? 2 : 1);
            (order[slot], order[replacement]) = (order[replacement], order[slot]);
        }
        var mix = new int[count];
        for (int i = 0; i < count; i++) mix[i] = i < 4 ? i : rng.Next(4);
        for (int i = count - 1; i > 0; i--) { int j = rng.Next(i + 1); (mix[i], mix[j]) = (mix[j], mix[i]); }
        for (int i = 0; i < count; i++) { points.Add(order[i]); types.Add(mix[i]); }
    }

    public static int Side(Transform point)
    {
        var block = point.GetComponentInParent<RailBlock>(true);
        Vector3 p = block.transform.InverseTransformPoint(point.position);
        var track = block.track;
        for (int i = 1; i < track.Length; i++)
            if (p.z <= track[i].z || i == track.Length - 1)
            {
                float x = Mathf.Lerp(track[i - 1].x, track[i].x,
                    Mathf.InverseLerp(track[i - 1].z, track[i].z, p.z));
                return Mathf.Abs(p.x - x) < 1f ? 0 : p.x < x ? -1 : 1;
            }
        return 0;
    }
}
