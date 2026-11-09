using UnityEngine;

/// <summary>Authored candidate only; the route selects and network-spawns enemies.</summary>
public sealed class RailEnemySpawner : MonoBehaviour
{
    void OnDrawGizmos()
    {
        if (Application.isPlaying || GetComponent<RailSpawnMarker>() != null) return;
        Gizmos.color = new Color(1, .3f, .3f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * .4f, .4f);
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * .9f, "ENEMY");
#endif
    }
}
