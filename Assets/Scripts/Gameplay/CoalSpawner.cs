using UnityEngine;

/// <summary>Authoring marker only. RailBlock's selected variation owns runtime coal spawning.</summary>
public sealed class CoalSpawner : MonoBehaviour
{
#if UNITY_EDITOR
    GUIStyle labelStyle;

    public string EditorLabel => "COAL";

    void OnDrawGizmos()
    {
        if (Application.isPlaying || GetComponent<RailSpawnMarker>() != null) return;
        Gizmos.color = new Color(1f, .72f, .12f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * .45f);
        var anchor = transform.position + Vector3.up * .9f;
        if (Camera.current != null && Camera.current.WorldToViewportPoint(anchor).z <= 0) return;
        if (labelStyle == null)
            labelStyle = new GUIStyle(UnityEditor.EditorStyles.label) { alignment = TextAnchor.MiddleCenter };
        var content = new GUIContent(EditorLabel);
        var size = labelStyle.CalcSize(content);
        var screen = UnityEditor.HandleUtility.WorldToGUIPoint(anchor);
        UnityEditor.Handles.BeginGUI();
        GUI.Label(new Rect(screen.x - size.x * .5f, screen.y - size.y * .5f, size.x, size.y), content, labelStyle);
        UnityEditor.Handles.EndGUI();
    }
#endif
}
