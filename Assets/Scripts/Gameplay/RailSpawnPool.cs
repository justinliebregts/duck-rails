using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum RailSpawnKind { Coal, Gold, Enemy }

/// <summary>Deterministic, independent per-block pools shared by authoring and route reconstruction.</summary>
public static class RailSpawnPool
{
    public static Transform[] Points(RailSpawnSet set, RailSpawnKind kind) =>
        set == null ? Array.Empty<Transform>() :
        (kind == RailSpawnKind.Coal ? set.coal : kind == RailSpawnKind.Gold ? set.gold : set.enemies) ?? Array.Empty<Transform>();

    public static void SetPoints(RailSpawnSet set, RailSpawnKind kind, Transform[] points)
    {
        if (kind == RailSpawnKind.Coal) set.coal = points;
        else if (kind == RailSpawnKind.Gold) set.gold = points;
        else set.enemies = points;
    }

    public static Transform[] Candidates(RailBlock block, RailSpawnKind kind)
    {
        IEnumerable<Transform> points = Points(block.spawnCandidates, kind);
        if (!block.spawnCandidatesMigrated)
            points = points.Concat((block.variations ?? Array.Empty<RailSpawnSet>()).SelectMany(s => Points(s, kind)));
        return points.Where(p => p != null && p.IsChildOf(block.transform)).Distinct().ToArray();
    }

    // Preserve every authored transform, including overlapping alternatives, so nothing is lost on migration.
    public static void Migrate(RailBlock block)
    {
        if (block.spawnCandidatesMigrated) return;
        var coal = Candidates(block, RailSpawnKind.Coal);
        var gold = Candidates(block, RailSpawnKind.Gold);
        var enemies = Candidates(block, RailSpawnKind.Enemy);
        block.spawnCandidates = new RailSpawnSet { coal = coal, gold = gold, enemies = enemies };
        block.spawnCandidatesMigrated = true;
    }

    public static int Count(RailBlock block, RailSpawnKind kind) => Mathf.Max(0,
        kind == RailSpawnKind.Coal ? block.coalCount : kind == RailSpawnKind.Gold ? block.goldCount : block.enemyCount);

    public static Transform[] Available(RailBlock block, RailSpawnKind kind)
    {
        var result = new List<Transform>();
        foreach (var p in Candidates(block, kind))
        {
            if (!EnabledInBlock(block, p)) continue;
            // Legacy variations sometimes have separate markers at exactly the same location.
            if (!result.Any(other => (other.position - p.position).sqrMagnitude < .0001f))
                result.Add(p);
        }
        return result.ToArray();
    }

    static bool EnabledInBlock(RailBlock block, Transform point)
    {
        // Prefab assets have activeInHierarchy=false even when all authored objects are enabled.
        for (var t = point; t != null; t = t.parent)
        {
            if (!t.gameObject.activeSelf) return false;
            if (t == block.transform) return true;
        }
        return false;
    }

    public static System.Random Random(RailBlock block, RailSpawnKind kind, int seed)
    {
        // String.GetHashCode is not stable across runtimes. Explicit FNV keeps client choices identical.
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in block.id ?? "") hash = (hash ^ c) * 16777619;
            hash = (hash ^ (uint)kind) * 16777619;
            return new System.Random((int)(hash ^ (uint)seed));
        }
    }

    public static Transform[] Select(RailBlock block, RailSpawnKind kind, int seed, int? requestedCount = null)
    {
        var points = Available(block, kind);
        var rng = Random(block, kind, seed);
        for (int i = points.Length - 1; i > 0; i--)
        { int j = rng.Next(i + 1); (points[i], points[j]) = (points[j], points[i]); }
        int count = Mathf.Clamp(requestedCount ?? Count(block, kind), 0, points.Length);
        if (kind == RailSpawnKind.Enemy && count >= 2)
        {
            // Keep the existing bilateral encounter rule where the candidate pool supports it.
            foreach (int side in new[] { -1, 1 })
            {
                if (points.Take(count).Any(p => RailEnemySpawns.Side(p) == side)) continue;
                int source = Array.FindIndex(points, count, p => RailEnemySpawns.Side(p) == side);
                if (source < 0) continue;
                // Prefer replacing a track point; do not discard the sole opposite-side enemy.
                int slot = Array.FindIndex(points, 0, count, p => RailEnemySpawns.Side(p) != -side);
                if (slot < 0) slot = count - 1; // All selected points are opposite-side; at least two exist.
                (points[slot], points[source]) = (points[source], points[slot]);
            }
        }
        return points.Take(count).ToArray();
    }

    public static void SelectEnemies(RailBlock block, int seed, List<Transform> points, List<int> types)
    {
        var chosen = Select(block, RailSpawnKind.Enemy, seed);
        var mix = Enumerable.Range(0, chosen.Length).Select(i => i % RailEnemySpawns.Names.Length).ToArray();
        var rng = Random(block, RailSpawnKind.Enemy, seed ^ 0x317a);
        // Small encounters should not always use the same enemy types.
        if (mix.Length < RailEnemySpawns.Names.Length)
            for (int i = 0; i < mix.Length; i++) mix[i] = rng.Next(RailEnemySpawns.Names.Length);
        for (int i = mix.Length - 1; i > 0; i--)
        { int j = rng.Next(i + 1); (mix[i], mix[j]) = (mix[j], mix[i]); }
        points.AddRange(chosen); types.AddRange(mix);
    }
}
