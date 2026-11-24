using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Final Bridge surge: when the bridge first reaches 100%, the master adds one enemy per player in the room.
/// The trigger and count live in the synced <see cref="RailRunState"/> (once per run, stable across master
/// handoff and late joins). Spawns are room objects keyed by "bridge-surge-N" markers, so a new master or a
/// repeated tick never duplicates them. Surge enemies head for the train when they have nobody in sight;
/// the ordinary chase brain (and minion/spider boarding) takes over once they see a duck.
/// </summary>
public static class RailBridgeSurge
{
    public const string Prefix = "bridge-surge-";
    public const int MaxCount = 8;

    public static bool IsSurge(string marker) => marker != null && marker.StartsWith(Prefix, System.StringComparison.Ordinal);

    public static bool TryIndex(string marker, out int index)
    {
        index = -1;
        return IsSurge(marker) && int.TryParse(marker.Substring(Prefix.Length), out index) && index >= 0;
    }

    /// <summary>One extra enemy per player, bounded by the room size.</summary>
    public static int CountFor(int players) => Mathf.Clamp(players, 0, MaxCount);

    /// <summary>Live (not inactive) players in the room.</summary>
    public static int LivePlayers()
    {
        int count = 0;
        foreach (var player in PhotonNetwork.PlayerList) if (player != null && !player.IsInactive) count++;
        return count;
    }

    /// <summary>Authored finale reinforcement points first, then the finale's enemy candidates, in a stable order
    /// so every master (and respawn) maps a marker to the same place. Spreads a full crew's surge out.</summary>
    public static Transform[] Points(RailBlock finale)
    {
        var points = new List<Transform>();
        if (finale == null) return points.ToArray();
        if (finale.bridgeReinforcements != null)
            foreach (var p in finale.bridgeReinforcements) if (p != null) points.Add(p);
        foreach (var p in RailSpawnPool.Available(finale, RailSpawnKind.Enemy))
            if (p != null && !points.Exists(o => (o.position - p.position).sqrMagnitude < .01f)) points.Add(p);
        return points.ToArray();
    }

    /// <summary>Spawn pose for a surge index. Later laps around the few authored points step out on a ring
    /// (onto supported, walkable ground) so a large crew's surge does not stack enemies in one spot.</summary>
    public static bool TryPoint(int index, out Vector3 position, out Quaternion rotation)
    {
        position = default; rotation = Quaternion.identity;
        var route = RailRoute.Instance;
        if (index < 0 || route == null || !route.Ready) return false;
        var points = Points(route.finale);
        if (points.Length == 0) return false;
        var point = points[index % points.Length];
        position = point.position; rotation = point.rotation;
        int lap = index / points.Length;
        if (lap > 0)
        {
            Vector3 offset = Quaternion.Euler(0, lap * 137.5f, 0) * Vector3.forward * (1.3f * Mathf.Sqrt(lap));
            if (Physics.Raycast(position + offset + Vector3.up * 2f, Vector3.down, out var hit, 4f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore)
                && hit.normal.y > .7f && !Physics.Linecast(position + Vector3.up * .5f, hit.point + Vector3.up * .5f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                position = hit.point;
        }
        return true;
    }

    /// <summary>Master-only: instantiate any surge enemy the committed state calls for that does not exist yet.</summary>
    public static void Spawn(string run, int count)
    {
        if (count <= 0 || string.IsNullOrEmpty(run)) return;
        var existing = new HashSet<string>();
        foreach (var v in PhotonNetwork.PhotonViewCollection)
            if (v != null && v.InstantiationData is object[] d && d.Length >= 3 && d[d.Length - 2] as string == run && d[d.Length - 1] is string m && IsSurge(m))
                existing.Add(m);
        for (int i = 0; i < count; i++)
        {
            string marker = Prefix + i;
            if (existing.Contains(marker) || !TryPoint(i, out var position, out var rotation)) continue;
            // Minions can board the train; honour the playtest type override like the base encounter.
            var prefab = RailEnemySpawns.Prefab(RailEnemySpawns.ForcedEnemyType >= 0 ? RailEnemySpawns.ForcedEnemyType : 0);
            PhotonNetwork.InstantiateRoomObject(prefab.name, position, rotation, 0, new object[] { "rails", run, marker });
        }
    }

    /// <summary>Where a surge enemy heads while it has no target: the train deck (boarding enemies resolve this to
    /// an opening; others to the nearest reachable ground beside the train).</summary>
    public static bool TryGetTrainGoal(out Vector3 goal)
    {
        var train = RailTrain.Instance;
        goal = train != null ? train.transform.TransformPoint(new Vector3(0, .35f, -2)) : default;
        return train != null;
    }
}
