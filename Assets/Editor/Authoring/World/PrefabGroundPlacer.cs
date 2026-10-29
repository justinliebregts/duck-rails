using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class PrefabGroundPlacer : EditorWindow
{
    [SerializeField] GameObject prefab;
    [SerializeField] Transform container;
    [SerializeField] bool placing;

    [MenuItem("Tools/DuckZ/Authoring/World/Prefab Ground Placer")]
    static void Open()
    {
        GetWindow<PrefabGroundPlacer>("Prefab Placer");
    }

    void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGui;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGui;
    }

    void OnGUI()
    {
        EditorGUILayout.LabelField("Place Prefab On Ground", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Assign a prefab and a scene container, then turn on placement. Left-click the Environment layer in the Scene view. Hold Alt to orbit without placing.",
            MessageType.Info);

        prefab = (GameObject)EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
        container = (Transform)EditorGUILayout.ObjectField("Container", container, typeof(Transform), true);

        using (new EditorGUI.DisabledScope(!CanPlace()))
            placing = EditorGUILayout.Toggle("Place On Click", placing);

        if (EditorApplication.isPlaying)
            EditorGUILayout.HelpBox("Exit Play Mode. Placement only runs in edit mode.", MessageType.Warning);
        else if (prefab == null)
            EditorGUILayout.HelpBox("Assign a prefab asset.", MessageType.Warning);
        else if (!PrefabUtility.IsPartOfPrefabAsset(prefab))
            EditorGUILayout.HelpBox("The assigned object must be a prefab asset.", MessageType.Warning);
        else if (container == null)
            EditorGUILayout.HelpBox("Assign a scene object. New instances are parented under it.", MessageType.Warning);
        else if (EditorUtility.IsPersistent(container) || !container.gameObject.scene.IsValid())
            EditorGUILayout.HelpBox("Container must be an object in the open scene.", MessageType.Warning);
        else if (LayerMask.NameToLayer("Environment") < 0)
            EditorGUILayout.HelpBox("This project has no layer named Environment.", MessageType.Error);
    }

    void DuringSceneGui(SceneView view)
    {
        if (!placing || !CanPlace())
            return;

        int controlId = GUIUtility.GetControlID(FocusType.Passive);
        HandleUtility.AddDefaultControl(controlId);

        Event e = Event.current;
        Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
        bool hitGround = Physics.Raycast(
            ray,
            out RaycastHit hit,
            Mathf.Infinity,
            LayerMask.GetMask("Environment"),
            QueryTriggerInteraction.Ignore);

        if (e.type == EventType.Repaint)
        {
            Handles.BeginGUI();
            GUI.Label(new Rect(8f, 8f, 520f, 20f), "Placing " + prefab.name + " under " + container.name);
            Handles.EndGUI();

            if (hitGround)
            {
                float size = HandleUtility.GetHandleSize(hit.point) * 0.15f;
                Handles.color = new Color(0.3f, 0.95f, 0.45f, 0.9f);
                Handles.DrawWireDisc(hit.point, hit.normal, size);
                Handles.DrawLine(hit.point, hit.point + hit.normal * size);
            }
        }

        if (e.type == EventType.MouseMove)
            view.Repaint();

        if (!hitGround || e.alt || e.button != 0)
            return;

        if (e.type == EventType.MouseDown)
        {
            Place(hit.point);
            GUIUtility.hotControl = controlId;
            e.Use();
        }
        else if (e.type == EventType.MouseUp && GUIUtility.hotControl == controlId)
        {
            GUIUtility.hotControl = 0;
            e.Use();
        }
    }

    bool CanPlace()
    {
        return !EditorApplication.isPlaying
            && prefab != null
            && PrefabUtility.IsPartOfPrefabAsset(prefab)
            && container != null
            && !EditorUtility.IsPersistent(container)
            && container.gameObject.scene.IsValid()
            && LayerMask.NameToLayer("Environment") >= 0;
    }

    void Place(Vector3 point)
    {
        var scene = container.gameObject.scene;
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
        Undo.RegisterCreatedObjectUndo(instance, "Place " + prefab.name);
        Undo.SetTransformParent(instance.transform, container, "Place " + prefab.name);
        instance.transform.SetPositionAndRotation(point, prefab.transform.rotation);
        EditorSceneManager.MarkSceneDirty(scene);
    }
}
