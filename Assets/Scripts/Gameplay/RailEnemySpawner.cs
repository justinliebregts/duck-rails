using UnityEngine;

/// <summary>Authored candidate only; the route selects and network-spawns enemies.</summary>
public sealed class RailEnemySpawner : MonoBehaviour
{
    public enum EnemyType { Random = -1, Zombie = 0, Bomber = 1, Smasher = 2, Spider = 3, Hook = 4 }
    [Tooltip("Fixed finale/authoring encounter type. Random keeps the seeded mix; scavenging crew selection uses its own mix.")]
    public EnemyType enemyType = EnemyType.Random;

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
