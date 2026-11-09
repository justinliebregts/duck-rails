using System;
using UnityEngine;

/// <summary>Authored at rail entry (0,0,0), +Z forward, unit scale. Graph coordinates are local.</summary>
public sealed class RailBlock : MonoBehaviour
{
    // Supports both legacy roots and the pick-lockable Env authoring hierarchy.
    public static Transform FindEnvironment(Transform root, string path) => root.Find(path) ?? root.Find("Env/" + path);
    public string id, displayName;
    [Tooltip("Allow this prefab in the four-block normal expedition pool. Explicit test routes can still use disabled blocks.")]
    public bool allowedInNormalRuns;
    public string[] tags;
    public float length = 160;
    public Transform entry, exit, stop;
    public TextAsset graph;
    public Bounds enemyBounds = new Bounds(new Vector3(23, 4, 80), new Vector3(28, 12, 70));
    // Optional above-ground rail exclusion for blocks whose navigation crosses below the track.
    public Bounds enemySurfaceRailExclusion;

    public Vector3 ConstrainEnemyPosition(Vector3 world)
    {
        if (enemySurfaceRailExclusion.size == Vector3.zero) return world;
        var local = transform.InverseTransformPoint(world);
        if (!enemySurfaceRailExclusion.Contains(local)) return world;
        local.x = local.x < enemySurfaceRailExclusion.center.x
            ? enemySurfaceRailExclusion.min.x - .05f : enemySurfaceRailExclusion.max.x + .05f;
        return transform.TransformPoint(local);
    }
    [Header("Random spawns per run")]
    [Min(0)] public int coalCount = 6;
    [Min(0)] public int goldCount = 2;
    [Min(0)] public int enemyCount = 5;
    public RailSpawnSet spawnCandidates = new RailSpawnSet();
    [HideInInspector] public bool spawnCandidatesMigrated;
    // Retained only for existing assets and legacy builders. Runtime merges these until migrated.
    [HideInInspector] public RailSpawnSet[] variations;
    public Vector3[] track;
    public float TrackLength
    {
        get { float total = 0; for (int i = 1; i < track.Length; i++) total += Vector3.Distance(track[i - 1], track[i]); return total; }
    }
    public void Sample(float distance, out Vector3 position, out Quaternion rotation)
    {
        for (int i = 1; i < track.Length; i++)
        {
            Vector3 delta = track[i] - track[i - 1]; float segment = delta.magnitude;
            if (distance <= segment || i == track.Length - 1)
            {
                position = transform.TransformPoint(track[i - 1] + delta * (distance / segment));
                // Interpolate adjacent tangent directions to avoid a yaw snap at each sampled bend.
                Vector3 a = i > 1 ? (track[i] - track[i - 2]).normalized : Vector3.forward;
                Vector3 b = i + 1 < track.Length ? (track[i + 1] - track[i - 1]).normalized : Vector3.forward;
                rotation = transform.rotation * Quaternion.LookRotation(Vector3.Slerp(a, b, Mathf.Clamp01(distance / segment)));
                return;
            }
            distance -= segment;
        }
        position = transform.position; rotation = transform.rotation;
    }
}

[Serializable]
public sealed class RailSpawnSet
{
    public Transform[] coal, enemies;
    public Transform[] gold = Array.Empty<Transform>();
}

[Serializable]
public sealed class RailRouteState
{
    public string run;
    public int enemySeed;
    public string[] blocks;
    public int[] variations;
    public Vector3[] placements;
}
