using UnityEngine;

/// <summary>Editable authored marker; RailExpedition performs the network spawn.</summary>
public sealed class RailGoldSpawner : MonoBehaviour
{
    void OnDrawGizmos()
    {
        if (Application.isPlaying || GetComponent<RailSpawnMarker>() != null) return;
        Gizmos.color = new Color(1, .75f, .1f);
        Gizmos.DrawWireCube(transform.position, new Vector3(.45f, .2f, .25f));
#if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * .3f, "GOLD");
#endif
    }
}
