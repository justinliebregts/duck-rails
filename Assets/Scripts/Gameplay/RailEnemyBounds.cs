using Photon.Pun;
using UnityEngine;

/// <summary>Authored-block boundary; opted-in boarding transfers temporarily own movement.</summary>
[DefaultExecutionOrder(12000)]
public sealed class RailEnemyBounds : MonoBehaviour
{
    Game.AI.Enemy enemy;
    Bounds home; bool assigned;
    bool relocated;
    bool pursuingOutside;
    Vector3 returnPoint;

    public bool TryGetReturnGoal(out Vector3 goal)
    {
        goal = returnPoint;
        return pursuingOutside && (enemy.Boarding == null || !enemy.Boarding.OwnsBoundary);
    }

    bool Pursuing => enemy != null && (enemy.CurrentState == Game.AI.Enemy.State.Chase ||
        enemy.CurrentState == Game.AI.Enemy.State.Search);

    bool wasAuthority;
    public void AdoptPosition(Vector3 position)
    {
        if (RailRoute.Instance == null || !RailRoute.Instance.Ready) return;
        pursuingOutside = false; returnPoint = position; home = RailRoute.Instance.EnemyBounds(position); assigned = relocated = true;
    }
    void Awake() => enemy = GetComponent<Game.AI.Enemy>();
    public bool TryRespawn(out Vector3 position, out Quaternion rotation)
    {
        position = default; rotation = Quaternion.identity;
        var route = RailRoute.Instance; var run = RailExpedition.Instance;
        if (route == null || !route.Ready || run == null) return false;
        var data = GetComponent<PhotonView>().InstantiationData;
        string marker = data != null && data.Length >= 3 ? data[data.Length - 1] as string : null;
        if (marker != null && marker.StartsWith("bridge-wave-") && int.TryParse(marker.Substring(12), out int wave))
        {
            var reinforcements = route.finale.bridgeReinforcements;
            if (wave < 0 || reinforcements == null || reinforcements.Length == 0) return false;
            var wavePoint = reinforcements[wave % reinforcements.Length];
            position = wavePoint.position; rotation = wavePoint.rotation;
            home = route.EnemyBounds(position); assigned = true; return true;
        }
        if (marker == null || !marker.StartsWith("enemy-") || !int.TryParse(marker.Substring(6), out int index)
            || index < 0 || index >= run.enemyPoints.Length) return false;
        if (!relocated) home = route.EnemyBounds(run.enemyPoints[index].position);
        assigned = true;
        var points = System.Array.FindAll(run.enemyPoints, p => p != null && home.Contains(p.position));
        if (points.Length == 0) return false;
        var point = points[Random.Range(0, points.Length)];
        position = point.position; rotation = point.rotation; return true;
    }
    // Reject planned exits before the runtime boundary has to clamp the enemy.
    public bool AllowsTraversal(Vector3 point)
    {
        if (!assigned)
        {
            var route = RailRoute.Instance;
            if (route == null || !route.Ready) return true;
            var root = enemy != null && enemy.Root != null ? enemy.Root : transform;
            home = route.EnemyBounds(root.position); assigned = true;
        }
        return home.Contains(point) || ((Pursuing || pursuingOutside) && RailRoute.Instance != null && RailRoute.Instance.Ready && RailRoute.Instance.IsEncounter(point));
    }

    void LateUpdate()
    {
        if (enemy == null || !PhotonSession.IsRailsRoom) return;
        if (!enemy.HasAuthority) { wasAuthority = false; return; }
        if (!wasAuthority && enemy.Boarding != null)
            AdoptPosition(enemy.Root != null ? enemy.Root.position : transform.position);
        wasAuthority = true;
        if (enemy.Boarding != null && enemy.Boarding.OwnsBoundary) return;
        var root = enemy.Root != null ? enemy.Root : transform;
        Vector3 p = root.position;
        var clamped = ConstrainPosition(p);
        if ((p - clamped).sqrMagnitude > .001f) { root.position = clamped; enemy.ResetBrain(); }
    }

    Vector3 ConstrainPosition(Vector3 p)
    {
        if (RailRoute.Instance != null && RailRoute.Instance.Ready && !assigned) { home = RailRoute.Instance.EnemyBounds(p); assigned = true; }
        if (assigned && home.Contains(p))
        {
            pursuingOutside = false;
            if (!Pursuing || !home.Contains(returnPoint)) returnPoint = InteriorReturnPoint(p);
            return p;
        }
        if (assigned && (Pursuing || pursuingOutside) && RailRoute.Instance != null && RailRoute.Instance.Ready && RailRoute.Instance.IsEncounter(p))
        {
            pursuingOutside = true;
            if (!home.Contains(returnPoint)) returnPoint = InteriorReturnPoint(p);
            return p;
        }
        return assigned ? home.ClosestPoint(p) : new Vector3(Mathf.Clamp(p.x, 6.5f, 29), Mathf.Max(0, p.y), Mathf.Clamp(p.z, 140, 545));
    }

    Vector3 InteriorReturnPoint(Vector3 position)
    {
        var point = home.ClosestPoint(position);
        point.z = Mathf.Clamp(point.z, home.min.z + 2f, home.max.z - 2f);
        return point;
    }
}
