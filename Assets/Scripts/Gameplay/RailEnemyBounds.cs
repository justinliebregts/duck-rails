using Photon.Pun;
using UnityEngine;

/// <summary>Hard authored-block safety boundary, including knockback. No train access.</summary>
[DefaultExecutionOrder(12000)]
public sealed class RailEnemyBounds : MonoBehaviour
{
    Game.AI.Enemy enemy;
    Bounds home; bool assigned;
    void Awake() => enemy = GetComponent<Game.AI.Enemy>();
    void LateUpdate()
    {
        if (enemy == null || !enemy.HasAuthority || !PhotonSession.IsRailsRoom) return;
        var root = enemy.Root != null ? enemy.Root : transform;
        Vector3 p = root.position;
        if (RailRoute.Instance != null && RailRoute.Instance.Ready && !assigned) { home = RailRoute.Instance.EnemyBounds(p); assigned = true; }
        var clamped = assigned ? home.ClosestPoint(p) : new Vector3(Mathf.Clamp(p.x, 6.5f, 29), Mathf.Max(0, p.y), Mathf.Clamp(p.z, 140, 545));
        if ((p - clamped).sqrMagnitude > .001f) { root.position = clamped; enemy.ResetBrain(); }
    }
}
