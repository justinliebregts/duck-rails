using Photon.Pun;
using UnityEngine;

/// <summary>Bridge defenders approach the controls even without sight of an operator.
/// Visible players still take priority through the ordinary chase/retreat brain.</summary>
public static class RailBridgeEncounter
{
    public static bool IsDefender(string marker, int firstDefender, int enemyCount) =>
        marker != null && (marker.StartsWith("bridge-wave-") ||
        (marker.StartsWith("enemy-") && int.TryParse(marker.Substring(6), out int index) &&
         index >= firstDefender && index < enemyCount));

    public static bool TryGetObjective(Game.AI.Enemy enemy, out Vector3 goal)
    {
        goal = default;
        var run = RailExpedition.Instance;
        if (!PhotonSession.IsRailsRoom || run == null || run.State.finished || run.State.bridgeAt <= 0 ||
            run.bridgeButton == null || RailRoute.Instance == null || !RailRoute.Instance.Ready) return false;
        var data = enemy.GetComponent<PhotonView>()?.InstantiationData;
        if (data == null || data.Length < 3 || data[data.Length - 2] as string != run.State.run ||
            !IsDefender(data[data.Length - 1] as string, run.baseEnemyCount, run.enemyPoints.Length)) return false;
        // Stop just south of the cabinet, on the shared catwalk rather than inside its collider.
        goal = run.bridgeButton.position + Vector3.back * 1.4f;
        return true;
    }
}
