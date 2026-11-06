using Photon.Pun;
using UnityEngine;

/// <summary>Authored-block boundary; opted-in boarding transfers temporarily own movement.</summary>
[DefaultExecutionOrder(12000)]
public sealed class RailEnemyBounds : MonoBehaviour
{
    Game.AI.Enemy enemy;
    Bounds home; bool assigned;
    bool relocated;
    bool wasAuthority;
    public void AdoptPosition(Vector3 position)
    {
        if (RailRoute.Instance == null || !RailRoute.Instance.Ready) return;
        home = RailRoute.Instance.EnemyBounds(position); assigned = relocated = true;
    }
    void Awake() => enemy = GetComponent<Game.AI.Enemy>();
    public bool TryRespawn(out Vector3 position, out Quaternion rotation)
    {
        position = default; rotation = Quaternion.identity;
        var route = RailRoute.Instance; var run = RailExpedition.Instance;
        if (route == null || !route.Ready || run == null) return false;
        var data = GetComponent<PhotonView>().InstantiationData;
        string marker = data != null && data.Length >= 3 ? data[data.Length - 1] as string : null;
        if (marker == null || !marker.StartsWith("enemy-") || !int.TryParse(marker.Substring(6), out int index)
            || index < 0 || index >= run.enemyPoints.Length) return false;
        if (!relocated) home = route.EnemyBounds(run.enemyPoints[index].position);
        assigned = true;
        var points = System.Array.FindAll(run.enemyPoints, p => p != null && home.Contains(p.position));
        if (points.Length == 0) return false;
        var point = points[Random.Range(0, points.Length)];
        position = point.position; rotation = point.rotation; return true;
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
        if (RailRoute.Instance != null && RailRoute.Instance.Ready && !assigned) { home = RailRoute.Instance.EnemyBounds(p); assigned = true; }
        var clamped = assigned ? home.ClosestPoint(p) : new Vector3(Mathf.Clamp(p.x, 6.5f, 29), Mathf.Max(0, p.y), Mathf.Clamp(p.z, 140, 545));
        if ((p - clamped).sqrMagnitude > .001f) { root.position = clamped; enemy.ResetBrain(); }
    }
}
