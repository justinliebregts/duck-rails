using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Editor-only: overlays the same world-space bounds and point used by bag pickup.
public sealed class BagPickupVisualizer : EditorWindow
{
    GameObject targetModel;
    bool show = true;
    bool probeEnabled;
    Vector3 probe;
    const string InventoryPath = "Assets/Prefabs/Player/DuckPlayer.prefab";

    [MenuItem("Tools/DuckZ/Diagnostics/Bag Pickup Reach")]
    static void Open()
    {
        GetWindow<BagPickupVisualizer>("Bag pickup reach").Show();
        SceneView.RepaintAll();
    }

    void OnEnable() => SceneView.duringSceneGui += DrawScene;
    void OnDisable()
    {
        SceneView.duringSceneGui -= DrawScene;
        SceneView.RepaintAll();
    }

    void OnInspectorUpdate()
    {
        if (show && EditorApplication.isPlaying)
            SceneView.RepaintAll();
    }

    float PickupPadding()
    {
        BagInventory inventory = BagInventory.Local;
        if (inventory == null)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPath);
            if (prefab != null) inventory = prefab.GetComponentInChildren<BagInventory>(true);
        }
        if (inventory == null) return 0.02f;
        return new SerializedObject(inventory).FindProperty("pickupRadius").floatValue;
    }

    void OnGUI()
    {
        EditorGUI.BeginChangeCheck();
        show = EditorGUILayout.Toggle("Show scene overlay", show);
        targetModel = (GameObject)EditorGUILayout.ObjectField("Bag / model in scene", targetModel, typeof(GameObject), true);
        EditorGUILayout.LabelField("Pickup padding", $"{PickupPadding() * 100f:0.#} cm per box face");
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Preview Basic model")) OpenModel("Assets/Prefabs/Bags/BagModel_Basic.prefab");
        if (GUILayout.Button("Preview Rambro model")) OpenModel("Assets/Prefabs/Bags/BagModel_Rambro.prefab");
        if (GUILayout.Button("Preview Ninja model")) OpenModel("Assets/Prefabs/Bags/BagModel_Ninja.prefab");
        if (GUILayout.Button("Preview Gorilla model")) OpenModel("Assets/Prefabs/Bags/BagModel_Gorilla.prefab");
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("In Play mode, dropped bags and the local player's hand points are drawn automatically. Pause to inspect them. Cyan = renderer bounds; yellow = pickup region; magenta dot = tested hand origin; white cross = HoldPoint (not tested for bag pickup).", MessageType.Info);
        EditorGUILayout.HelpBox("The yellow boxes stay aligned to the world as the bag rotates. Their corners can extend farther than the configured padding from the mesh. Labels report geometry only: slot items have priority, and an awaiting-claim bag cannot be picked up.", MessageType.None);
        if (GUILayout.Button("Use selected bag / model and frame it"))
        {
            targetModel = Selection.activeGameObject;
            if (targetModel != null && !EditorUtility.IsPersistent(targetModel))
            {
                probe = targetModel.transform.position + Vector3.right * 0.3f;
                SceneView.lastActiveSceneView?.FrameSelected();
            }
        }
        probeEnabled = EditorGUILayout.Toggle("Movable test hand point", probeEnabled);
        if (probeEnabled)
            EditorGUILayout.HelpBox("Drag the test point in Scene view to explore the boundary without moving your real hands. This does not change any gameplay setting.", MessageType.None);
        if (targetModel != null && EditorUtility.IsPersistent(targetModel))
            EditorGUILayout.HelpBox("Select a bag model instance in a scene or open its prefab first.", MessageType.Warning);
        if (EditorGUI.EndChangeCheck()) SceneView.RepaintAll();
    }

    void OpenModel(string path)
    {
        if (EditorApplication.isPlaying)
        {
            ShowNotification(new GUIContent("Exit Play mode to preview a model, or inspect a live dropped bag."));
            return;
        }
        PrefabStage stage = PrefabStageUtility.OpenPrefab(path);
        if (stage == null) return;
        targetModel = stage.prefabContentsRoot;
        bool any = false;
        Bounds bounds = new Bounds(targetModel.transform.position, Vector3.zero);
        foreach (Renderer renderer in targetModel.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer.GetComponentInParent<BagSlot>() != null) continue;
            if (!any) bounds = renderer.bounds;
            else bounds.Encapsulate(renderer.bounds);
            any = true;
        }
        probe = bounds.center + Vector3.right * (bounds.extents.x + PickupPadding());
        probeEnabled = true;
        show = true;
        bounds.Expand(PickupPadding() * 2f + 0.15f);
        SceneView view = SceneView.lastActiveSceneView;
        if (view != null) view.Frame(bounds, false);
        SceneView.RepaintAll();
    }

    void DrawScene(SceneView view)
    {
        if (!show) return;
        Color previousColor = Handles.color;
        CompareFunction previousDepth = Handles.zTest;
        Matrix4x4 previousMatrix = Handles.matrix;
        Handles.matrix = Matrix4x4.identity;
        Handles.zTest = CompareFunction.Always;
        try
        {
            float padding = PickupPadding();
            if (targetModel != null && !EditorUtility.IsPersistent(targetModel))
                DrawModel(targetModel, padding);
            else
                foreach (BagObject bag in BagObject.Active)
                    if (bag != null && bag.State == BagState.Dropped)
                        DrawModel(bag.gameObject, padding);

            GR.Player player = GR.Player.instance;
            if (player != null)
            {
                DrawHand(player.leftPlayerHand, "Left");
                DrawHand(player.rightPlayerHand, "Right");
            }
            if (probeEnabled)
            {
                probe = Handles.PositionHandle(probe, Quaternion.identity);
                DrawPoint(probe, "Test hand origin", Color.magenta);
            }
        }
        finally
        {
            Handles.color = previousColor;
            Handles.zTest = previousDepth;
            Handles.matrix = previousMatrix;
        }
    }

    void DrawModel(GameObject model, float padding)
    {
        bool any = false;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            // Keep this filter identical to BagObject.Touches.
            if (!renderer.enabled || renderer.GetComponentInParent<BagSlot>() != null) continue;
            any = true;
            Bounds bounds = renderer.bounds;
            Handles.color = Color.cyan;
            Handles.DrawWireCube(bounds.center, bounds.size);
            Bounds expanded = bounds;
            expanded.Expand(padding * 2f);
            Handles.color = Color.yellow;
            Handles.DrawWireCube(expanded.center, expanded.size);
            Vector3 face = bounds.center + Vector3.right * bounds.extents.x;
            Handles.DrawLine(face, face + Vector3.right * padding);
            Handles.Label(face + Vector3.right * (padding * 0.5f), $"{padding * 100f:0.#} cm");
        }
        if (!any)
        {
            Handles.color = Color.yellow;
            Vector3 p = model.transform.position;
            Handles.DrawWireDisc(p, Vector3.up, padding);
            Handles.DrawWireDisc(p, Vector3.right, padding);
            Handles.DrawWireDisc(p, Vector3.forward, padding);
            Handles.Label(p, "No mesh: spherical fallback");
        }
        BagObject bag = model.GetComponent<BagObject>();
        if (bag != null)
            Handles.Label(model.transform.position + Vector3.up * 0.2f,
                $"{bag.State}{(bag.AwaitingClaim ? " / awaiting claim" : "")}");
        GR.Player player = GR.Player.instance;
        if (player != null)
        {
            if (player.leftPlayerHand != null) DrawDistance(model, player.leftPlayerHand.transform.position, padding, "L");
            if (player.rightPlayerHand != null) DrawDistance(model, player.rightPlayerHand.transform.position, padding, "R");
        }
        if (probeEnabled) DrawDistance(model, probe, padding, "Test");
    }

    static void DrawHand(PlayerHand hand, string name)
    {
        if (hand == null) return;
        Vector3 origin = hand.transform.position;
        DrawPoint(origin, name + " hand origin (tested)", Color.magenta);
        Vector3 hold = hand.HoldPoint.position;
        Handles.color = Color.white;
        Handles.DrawDottedLine(origin, hold, 4f);
        float size = 0.008f;
        Handles.DrawLine(hold - Vector3.right * size, hold + Vector3.right * size);
        Handles.DrawLine(hold - Vector3.up * size, hold + Vector3.up * size);
        Handles.Label(hold + Vector3.down * 0.025f, name + " HoldPoint (not tested)");
    }

    static void DrawPoint(Vector3 point, string label, Color color)
    {
        Handles.color = color;
        Handles.SphereHandleCap(0, point, Quaternion.identity, 0.012f, EventType.Repaint);
        Handles.Label(point + Vector3.up * 0.025f, label);
    }

    static void DrawDistance(GameObject model, Vector3 point, float padding, string name)
    {
        bool any = false;
        float best = float.PositiveInfinity;
        Vector3 nearest = model.transform.position;
        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            if (!renderer.enabled || renderer.GetComponentInParent<BagSlot>() != null) continue;
            any = true;
            Bounds bounds = renderer.bounds;
            bounds.Expand(padding * 2f);
            Vector3 candidate = bounds.ClosestPoint(point);
            float distance = Vector3.Distance(point, candidate);
            if (distance < best) { best = distance; nearest = candidate; }
        }
        if (!any)
        {
            Vector3 delta = point - model.transform.position;
            best = Mathf.Max(0f, delta.magnitude - padding);
            nearest = best > 0f ? model.transform.position + delta.normalized * padding : point;
        }
        Handles.color = best <= 0f ? Color.green : Color.red;
        Handles.DrawDottedLine(point, nearest, 4f);
        Handles.Label(Vector3.Lerp(point, nearest, 0.5f) + Vector3.down * 0.055f,
            best <= 0f ? $"{name}: inside pickup region" : $"{name}: {best * 100f:0.0} cm outside pickup region");
    }
}
