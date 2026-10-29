using System;
using System.Collections.Generic;
using UnityEngine;

public sealed partial class DuckCompanion
{
    Vector3[] truckExitRoute = Array.Empty<Vector3>();
    int truckExitCorner;
    Bounds truckCargoBounds;
    float nextTruckExitSearch, truckExitStalled;
    Vector3 truckProgressAnchor;

    bool InsideTruckCargo(Vector3 point) =>
        point.x > truckCargoBounds.min.x && point.x < truckCargoBounds.max.x
        && point.z > truckCargoBounds.min.z && point.z < truckCargoBounds.max.z
        && point.y > truckCargoBounds.min.y - 3.5f && point.y < truckCargoBounds.max.y;

    bool BuildTruckExit(Vector3 destination) => RequestNavigation(NavigationPlan.Truck, destination);

    IEnumerable<int> PlanTruck(Vector3 destination)
    {

        if (!Physics.Raycast(transform.position + Vector3.up * eyeHeight, Vector3.up,
            out var roof, 4f, environment, QueryTriggerInteraction.Ignore)
            || roof.collider.name.IndexOf("Box_Truck", StringComparison.OrdinalIgnoreCase) < 0)
            { planSucceeded = false; yield break; }
        truckCargoBounds = roof.collider.bounds;
        if (InsideTruckCargo(destination)) { planSucceeded = false; yield break; }

        var positions = floorPositions;
        var parents = floorParents;
        // A fine, bounded floor graph fits between cargo. Its edges retain the
        // existing full-body checks; only the final exit may be unsupported.
        foreach (var step in PlanFloor(destination, truckCargoBounds, 2048, .25f)) yield return step;
        float best = float.PositiveInfinity;
        Vector2Int chosen = default;
        Vector3 exit = default, landing = default;
        foreach (var entry in positions)
        for (int direction = 0; direction < 8; direction++)
        {
            Vector3 outward = Quaternion.Euler(0, direction * 45, 0) * Vector3.forward;
            for (float distance = .5f; distance <= 1.5f; distance += .25f)
            {
                yield return 0;
                Vector3 edge = entry.Value + outward * distance;
                if (InsideTruckCargo(edge) || !ClearBody(edge)
                    || !ClearBodySweep(entry.Value, edge)) continue;
                if (!Physics.Raycast(edge + Vector3.up * .1f, Vector3.down, out var ground,
                    1000f, environment, QueryTriggerInteraction.Ignore)
                    || ground.normal.y < .7f || ground.point.y > entry.Value.y - .4f) continue;
                Vector3 floor = ground.point + Vector3.up * .04f;
                if (!ClearBody(floor) || !ClearBodySweep(edge, floor)) continue;
                float score = Vector3.Distance(transform.position, entry.Value)
                    + distance + Vector3.Distance(floor, destination) * .25f;
                if (score >= best) continue;
                best = score; chosen = entry.Key; exit = edge; landing = floor;
            }
        }
        if (float.IsPositiveInfinity(best)) { planSucceeded = false; yield break; }
        var points = planningRoute; points.Clear();
        while (chosen != Vector2Int.zero)
        {
            yield return 0;
            points.Add(positions[chosen]);
            chosen = parents[chosen];
        }
        points.Reverse();
        points.Add(exit); points.Add(landing);
        truckExitRoute = points.ToArray();
        truckExitCorner = 0;
        truckExitStalled = 0;
        truckProgressAnchor = transform.position;
        LastEvent = "leaving truck through a clear cargo opening";
        { planSucceeded = true; yield break; }

    }

    bool WalkTruckExit(Vector3 destination, float dt)
    {
        if (truckExitRoute.Length > 0 && InsideTruckCargo(destination))
            truckExitRoute = Array.Empty<Vector3>(); // Buddy returned inside.
        if (truckExitRoute.Length == 0)
        {
            if (Time.time < nextTruckExitSearch) return false;
            nextTruckExitSearch = Time.time + 2f;
            if (!BuildTruckExit(destination)) return false;
        }
        while (truckExitCorner < truckExitRoute.Length - 1
            && Vector3.ProjectOnPlane(truckExitRoute[truckExitCorner] - transform.position,
                Vector3.up).sqrMagnitude < .0225f) truckExitCorner++;
        Vector3 point = truckExitRoute[truckExitCorner];
        if (truckExitCorner == truckExitRoute.Length - 1
            && Vector3.Distance(transform.position, point) < .25f)
        {
            truckExitRoute = Array.Empty<Vector3>();
            return false;
        }
        // Preserve the exit commitment even when the buddy moves outside. A
        // dynamic blockage yields to ordinary traversal/recovery after 3 s.
        MoveStairMotor(point, dt, Mathf.Min(speed, 2f));
        truckExitStalled += dt;
        if (Vector3.Distance(transform.position, truckProgressAnchor) > .25f)
        {
            truckProgressAnchor = transform.position;
            truckExitStalled = 0;
        }
        if (truckExitStalled >= 3f)
        {
            truckExitRoute = Array.Empty<Vector3>();
            nextTruckExitSearch = Time.time + 3f;
            LastEvent = "truck exit blocked; trying alternate movement";
            return false;
        }
        return true;
    }
}
