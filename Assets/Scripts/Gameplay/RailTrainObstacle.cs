using System.Collections.Generic;
using Game.AI;
using UnityEngine;

/// <summary>Moving train occupancy for world navigation; never modifies the baked graph.</summary>
public static class RailTrainObstacle
{
    // Include body clearance and a short forward look-ahead while the train moves.
    static Bounds LocalBounds(RailTrain train) => new Bounds(
        new Vector3(0, .5f, Mathf.Clamp(train.Speed, 0, 12) * .5f),
        new Vector3(6f, 5f, 11f + Mathf.Clamp(train.Speed, 0, 12)));

    public static bool Contains(Vector3 point, float padding = 0)
    {
        var train = RailTrain.Instance;
        if (train == null) return false;
        var bounds = LocalBounds(train); bounds.Expand(padding * 2);
        return bounds.Contains(train.transform.InverseTransformPoint(point));
    }

    public static bool BlocksSegment(Vector3 from, Vector3 to)
    {
        var train = RailTrain.Instance;
        if (train == null) return false;
        var bounds = LocalBounds(train);
        Vector3 a = train.transform.InverseTransformPoint(from);
        Vector3 b = train.transform.InverseTransformPoint(to);
        // If the moving train overtakes an enemy, allow it to escape the volume.
        // Physical capsule sweeps still prevent movement through the train body.
        if (bounds.Contains(a)) return false;
        if (bounds.Contains(b)) return true;
        Vector3 delta = b - a;
        return delta.sqrMagnitude > .000001f &&
            bounds.IntersectRay(new Ray(a, delta.normalized), out float distance) && distance <= delta.magnitude;
    }

    public static void AddBlockedNodes(CrawlGraph graph, HashSet<int> blocked)
    {
        var train = RailTrain.Instance;
        if (train == null || graph == null || graph.Frame != null) return;
        var local = LocalBounds(train);
        // One graph cell of clearance prevents diagonal graph edges cutting a
        // corner of the occupied volume even when both endpoints lie outside it.
        local.Expand(graph.CellSize * 2);
        Bounds world = new Bounds(train.transform.TransformPoint(local.center), Vector3.zero);
        for (int i = 0; i < 8; i++)
            world.Encapsulate(train.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
        graph.CellOf(world.min, out int x0, out int y0, out int z0);
        graph.CellOf(world.max, out int x1, out int y1, out int z1);
        for (int z = Mathf.Max(0, z0 - 1); z <= Mathf.Min(graph.DimZ - 1, z1 + 1); z++)
        for (int y = Mathf.Max(0, y0 - 1); y <= Mathf.Min(graph.DimY - 1, y1 + 1); y++)
        for (int x = Mathf.Max(0, x0 - 1); x <= Mathf.Min(graph.DimX - 1, x1 + 1); x++)
        {
            int node = graph.FindNodeAtCell(x, y, z);
            if (node >= 0 && local.Contains(train.transform.InverseTransformPoint(graph.GetPosition(node))))
                blocked.Add(node);
        }
    }
}
