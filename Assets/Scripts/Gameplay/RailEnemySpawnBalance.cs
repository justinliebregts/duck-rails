using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>Initial placement only; does not constrain enemy movement or AI.</summary>
public static class RailEnemySpawnBalance
{
    public const float MinimumSpacing = 15f;
    const int SelectionAttempts = 32;
    public const int MaxRounds = 3;
    public static int PerQuadrant(int crew) => crew <= 2 ? 1 : crew <= 5 ? 2 : 3;
    /// <summary>Smallest crew whose quota includes the given round-robin round.</summary>
    public static int CrewForRound(int round) => round <= 0 ? 1 : round == 1 ? 3 : 6;
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
    /// <summary>Full-crew selection with each pick's round. Smaller crews use only the earlier rounds,
    /// so a mid-run crew change adds or removes later-round enemies and never moves the others.</summary>
    public static List<(Transform point, int round)> SelectRounds(RailBlock block, int seed)
    {
        if (block.enemyCount <= 0) return new List<(Transform, int)>();
        var candidates = RailSpawnPool.Available(block, RailSpawnKind.Enemy);
        var rng = RailSpawnPool.Random(block, RailSpawnKind.Enemy, seed);
        var best = new List<(Transform point, int round)>();
        float bestSpacing = -1;
        for (int attempt = 0; attempt < SelectionAttempts; attempt++)
        {
            var pools = Enumerable.Range(0, 4).Select(q => candidates.Where(p => Quadrant(block, p) == q).ToList()).ToArray();
            foreach (var pool in pools)
                for (int i = pool.Count - 1; i > 0; i--) { int j = rng.Next(i + 1); (pool[i], pool[j]) = (pool[j], pool[i]); }
            var chosen = new List<(Transform point, int round)>();
            int first = rng.Next(4);
            // Round-robin gives every quadrant a first spawn before adding seconds/thirds.
            for (int round = 0; round < MaxRounds; round++)
                for (int step = 0; step < 4; step++)
                {
                    var pool = pools[(first + step) % 4];
                    Transform pick = null; float farthest = -1;
                    foreach (var candidate in pool)
                    {
                        float spacing = chosen.Count == 0 ? float.MaxValue : chosen.Min(p => DistanceSquared(p.point, candidate));
                        if (spacing < MinimumSpacing * MinimumSpacing || spacing <= farthest) continue;
                        pick = candidate; farthest = spacing;
                    }
                    if (pick != null) { chosen.Add((pick, round)); pool.Remove(pick); }
                }
            float minimum = chosen.Count < 2 ? 0 : chosen.Min(a => chosen.Where(b => b.point != a.point).Min(b => DistanceSquared(a.point, b.point)));
            if (chosen.Count > best.Count || (chosen.Count == best.Count && minimum > bestSpacing))
            { best = chosen; bestSpacing = minimum; }
        }
        // Stable quadrant ordering (round order within a quadrant) also supports distributing types below.
        return best.OrderBy(p => Quadrant(block, p.point)).ToList();
    }
    public static Transform[] Select(RailBlock block, int seed, int crew)
    {
        int quota = PerQuadrant(Mathf.Clamp(crew, 1, 8));
        return SelectRounds(block, seed).Where(p => p.round < quota).Select(p => p.point).ToArray();
    }
    public static void Append(RailBlock block, int seed, int crew, List<Transform> points, List<int> types, bool reportShortfall = true)
        => Append(block, seed, crew, points, types, null, reportShortfall);
    /// <summary>Appends the crew's enemies. needs receives, per point, the smallest crew that includes it.</summary>
    public static void Append(RailBlock block, int seed, int crew, List<Transform> points, List<int> types, List<int> needs, bool reportShortfall = true)
    {
        int quota = PerQuadrant(Mathf.Clamp(crew, 1, 8));
        var chosen = SelectRounds(block, seed).Where(p => p.round < quota).ToList();
        var order = Enumerable.Range(0, RailEnemySpawns.Names.Length).ToArray();
        var rng = RailSpawnPool.Random(block, RailSpawnKind.Enemy, seed ^ 0x317a);
        for (int i = order.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        foreach (var (point, round) in chosen)
        {
            // Round-robin never leaves a gap, so the round is the point's rank within its quadrant.
            points.Add(point); types.Add(order[(Quadrant(block, point) + round) % order.Length]);
            needs?.Add(CrewForRound(round));
        }
        if (reportShortfall && block.enemyCount > 0 && chosen.Count < 4 * quota)
            if (DuckzLog.AllowWarning(out var notice, block.id))
                notice.Write($"Enemy spawn shortfall: {block.id} selected {chosen.Count}/{4 * quota} with {MinimumSpacing}m spacing. Check quadrant candidates.", block);
    }
}
