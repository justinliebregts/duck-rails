using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Initial placement only; does not constrain enemy movement or AI.</summary>
public static class RailEnemySpawnBalance
{
    public const float MinimumSpacing = 15f;
    const int SelectionAttempts = 32;
    public static int PerQuadrant(int crew) => crew <= 2 ? 1 : crew <= 5 ? 2 : 3;
    public static int Quadrant(RailBlock block, Transform point)
    {
        var p = block.transform.InverseTransformPoint(point.position);
        return (p.x < 0 ? 0 : 1) + (p.z < block.length * .5f ? 0 : 2);
    }
    static float DistanceSquared(Transform a, Transform b)
    {
        var delta = a.position - b.position; delta.y = 0;
        return delta.sqrMagnitude;
    }
    public static Transform[] Select(RailBlock block, int seed, int crew)
    {
        if (block.enemyCount <= 0) return Array.Empty<Transform>();
        int quota = PerQuadrant(Mathf.Clamp(crew, 1, 8));
        var candidates = RailSpawnPool.Available(block, RailSpawnKind.Enemy);
        var rng = RailSpawnPool.Random(block, RailSpawnKind.Enemy, seed);
        var best = new List<Transform>();
        float bestSpacing = -1;
        for (int attempt = 0; attempt < SelectionAttempts; attempt++)
        {
            var pools = Enumerable.Range(0, 4).Select(q => candidates.Where(p => Quadrant(block, p) == q).ToList()).ToArray();
            foreach (var pool in pools)
                for (int i = pool.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            var chosen = new List<Transform>();
            int first = rng.Next(4);
            // Round-robin gives every quadrant a first spawn before adding seconds/thirds.
            for (int round = 0; round < quota; round++)
                for (int step = 0; step < 4; step++)
                {
                    var pool = pools[(first + step) % 4];
                    Transform pick = null; float farthest = -1;
                    foreach (var candidate in pool)
                    {
                        float spacing = chosen.Count == 0 ? float.MaxValue : chosen.Min(p => DistanceSquared(p, candidate));
                        if (spacing < MinimumSpacing * MinimumSpacing || spacing <= farthest) continue;
                        pick = candidate; farthest = spacing;
                    }
                    if (pick != null) { chosen.Add(pick); pool.Remove(pick); }
                }
            float minimum = chosen.Count < 2 ? 0 : chosen.Min(a => chosen.Where(b => b != a).Min(b => DistanceSquared(a, b)));
            if (chosen.Count > best.Count || (chosen.Count == best.Count && minimum > bestSpacing))
            { best = chosen; bestSpacing = minimum; }
        }
        // Stable quadrant ordering also supports distributing types below.
        return best.OrderBy(p => Quadrant(block, p)).ToArray();
    }
    public static void Append(RailBlock block, int seed, int crew, List<Transform> points, List<int> types, bool reportShortfall = true)
    {
        var chosen = Select(block, seed, crew);
        var order = Enumerable.Range(0, RailEnemySpawns.Names.Length).ToArray();
        var rng = RailSpawnPool.Random(block, RailSpawnKind.Enemy, seed ^ 0x317a);
        for (int i = order.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        var used = new int[4];
        foreach (var point in chosen)
        {
            int q = Quadrant(block, point);
            points.Add(point); types.Add(order[(q + used[q]++) % order.Length]);
        }
        if (reportShortfall && block.enemyCount > 0 && chosen.Length < 4 * PerQuadrant(crew))
            Debug.LogWarning($"Enemy spawn shortfall: {block.id} selected {chosen.Length}/{4 * PerQuadrant(crew)} with {MinimumSpacing}m spacing. Check quadrant candidates.");
    }
}
