#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Stage-local loot painting; writes the same variation arrays consumed by RailRoute.</summary>
public sealed class RailLootPlacementWindow : EditorWindow
{
    [SerializeField] bool gold, placing, allVariations;
    [SerializeField] int variation;
    [SerializeField] float clearance = .05f;
    [SerializeField] RailBlock sceneBlock;
    Collider[] surfaces = Array.Empty<Collider>();
    RailBlock cachedBlock;
    bool zHeld;

    [MenuItem("Duck Rails/Authoring/Place Coal and Gold")]
    public static void Open() => GetWindow<RailLootPlacementWindow>("Coal & Gold");

    void OnEnable()
    {
        SceneView.duringSceneGui += DuringSceneGUI;
        EditorApplication.hierarchyChanged += Invalidate;
        Undo.undoRedoPerformed += Invalidate;
    }
    void OnDisable()
    {
        SceneView.duringSceneGui -= DuringSceneGUI;
        EditorApplication.hierarchyChanged -= Invalidate;
        Undo.undoRedoPerformed -= Invalidate;
        SceneView.RepaintAll();
    }
    void Invalidate() { cachedBlock = null; Repaint(); SceneView.RepaintAll(); }
    RailBlock Target()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) return stage.prefabContentsRoot.GetComponentInChildren<RailBlock>();
        if (sceneBlock != null && !EditorUtility.IsPersistent(sceneBlock)) return sceneBlock;
        return Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<RailBlock>() : null;
    }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Open a block prefab, choose loot, then enable placement. Click Environment floors in Scene view. Z: undo • Shift+Z: redo • Esc: stop. Alt/right mouse still navigate.", MessageType.Info);
        if (PrefabStageUtility.GetCurrentPrefabStage() == null)
            sceneBlock = (RailBlock)EditorGUILayout.ObjectField("Scene block", sceneBlock, typeof(RailBlock), true);
        var block = Target();
        EditorGUILayout.LabelField("Target", block != null ? block.name : "Open or select a RailBlock");
        gold = GUILayout.Toolbar(gold ? 1 : 0, new[] { "Coal", "Gold bar" }) == 1;
        clearance = Mathf.Max(0, EditorGUILayout.FloatField("Clearance above floor (m)", clearance));
        if (block == null || block.variations == null || block.variations.Length == 0)
        {
            EditorGUILayout.HelpBox("A block with at least one spawn variation is required.", MessageType.Warning);
            placing = false; return;
        }
        variation = Mathf.Clamp(variation, 0, block.variations.Length - 1);
        variation = EditorGUILayout.Popup("Variation", variation, Enumerable.Range(1, block.variations.Length).Select(v => "Variation " + v).ToArray());
        allVariations = EditorGUILayout.Toggle("Include in every variation", allVariations);
        var set = block.variations[variation];
        EditorGUILayout.LabelField("Selected variation", $"Coal: {set?.coal?.Count(t => t != null) ?? 0}    Gold: {set?.gold?.Count(t => t != null) ?? 0}");
        EditorGUILayout.HelpBox("One variation is chosen per run. Every-variation placement shares one marker across the lists. Outlines show the initial item bounds; physics can move it afterward. Save the prefab normally.", MessageType.None);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
            placing = GUILayout.Toggle(placing, placing ? "Placing — click in Scene view" : "Start placing", "Button", GUILayout.Height(32));
        if (GUI.changed) SceneView.RepaintAll();
    }

    internal static bool FloorHit(Collider[] colliders, Ray ray, out RaycastHit nearest)
    {
        nearest = default;
        float distance = float.PositiveInfinity;
        int environment = LayerMask.NameToLayer("Environment");
        foreach (var c in colliders)
        {
            if (c == null || !c.enabled || !c.gameObject.activeInHierarchy || c.isTrigger || c.gameObject.layer != environment || SceneVisibilityManager.instance.IsHidden(c.gameObject)) continue;
            if (c.Raycast(ray, out var hit, 10000) && hit.distance < distance)
            { nearest = hit; distance = hit.distance; }
        }
        // Reject walls, rather than painting through them onto a hidden floor.
        return distance < float.PositiveInfinity && Vector3.Dot(nearest.normal, Vector3.up) >= .65f;
    }

    internal static Bounds ItemBounds(bool isGold)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(isGold ? "Assets/DuckRails/Economy/RailGoldBar.prefab" : "Assets/Resources/Rails/RailCoal.prefab");
        if (prefab == null) throw new InvalidOperationException("Missing runtime loot prefab.");
        // Prefab renderer bounds include the runtime root scale and child transforms.
        var renderers = prefab.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) throw new InvalidOperationException("Loot prefab has no renderer.");
        var bounds = renderers[0].bounds;
        foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
        bounds.center -= prefab.transform.position;
        foreach (var box in prefab.GetComponentsInChildren<BoxCollider>())
        {
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                bounds.Encapsulate(box.transform.TransformPoint(box.center + Vector3.Scale(box.size * .5f, new Vector3(x, y, z))) - prefab.transform.position);
        }
        return bounds;
    }

    internal static Transform Place(RailBlock block, int index, bool every, bool isGold, Vector3 position)
    {
        if (block == null || block.variations == null || index < 0 || index >= block.variations.Length)
            throw new ArgumentException("Choose a valid block variation.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        string action = "Place " + (isGold ? "Gold" : "Coal") + " Spawner";
        Undo.SetCurrentGroupName(action);
        var marker = new GameObject((isGold ? "Gold" : "Coal") + " Spawn " + (every ? "All Variations" : "V" + (index + 1)));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(marker, block.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(marker, action);
        Undo.SetTransformParent(marker.transform, block.transform, action);
        Undo.RecordObject(marker.transform, action);
        marker.transform.position = position;
        if (isGold) Undo.AddComponent<RailGoldSpawner>(marker); else Undo.AddComponent<CoalSpawner>(marker);
        Undo.RecordObject(block, action);
        for (int i = 0; i < block.variations.Length; i++)
        {
            if (!every && i != index) continue;
            var set = block.variations[i] ?? (block.variations[i] = new RailSpawnSet());
            var points = isGold ? set.gold : set.coal;
            points = (points ?? Array.Empty<Transform>()).Concat(new[] { marker.transform }).ToArray();
            if (isGold) set.gold = points; else set.coal = points;
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);
        EditorUtility.SetDirty(block);
        EditorSceneManager.MarkSceneDirty(block.gameObject.scene);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(group);
        return marker.transform;
    }

    void DuringSceneGUI(SceneView view)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { placing = false; return; }
        var block = Target();
        if (block == null || block.variations == null || block.variations.Length == 0) return;
        variation = Mathf.Clamp(variation, 0, block.variations.Length - 1);
        var e = Event.current;
        if (e.type == EventType.Repaint)
        {
            var set = block.variations[variation];
            DrawMarkers(set?.coal, false);
            DrawMarkers(set?.gold, true);
        }
        if (!placing) return;
        if (e.type == EventType.KeyUp && e.keyCode == KeyCode.Z) zHeld = false;
        if (e.type == EventType.KeyDown && !EditorGUIUtility.editingTextField)
        {
            if (e.keyCode == KeyCode.Escape) { placing = false; Repaint(); e.Use(); return; }
            if (e.keyCode == KeyCode.Z && !e.control && !e.command && !e.alt)
            {
                if (!zHeld) { if (e.shift) Undo.PerformRedo(); else Undo.PerformUndo(); }
                zHeld = true; e.Use(); return;
            }
        }
        if (e.alt || e.button == 1 || e.button == 2 || Tools.viewToolActive) return;
        int id = GUIUtility.GetControlID(FocusType.Passive);
        if (e.type == EventType.Layout) HandleUtility.AddDefaultControl(id);
        if (cachedBlock != block)
        { surfaces = block.GetComponentsInChildren<Collider>(); cachedBlock = block; }
        if (e.type == EventType.MouseMove) view.Repaint();
        Physics.SyncTransforms();
        if (!FloorHit(surfaces, HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit)) return;
        var bounds = ItemBounds(gold);
        var position = hit.point + Vector3.up * (clearance - bounds.min.y);
        if (e.type == EventType.Repaint)
        {
            Handles.color = gold ? new Color(1, .75f, .1f) : Color.cyan;
            Handles.DrawWireDisc(hit.point, hit.normal, .25f);
            Handles.DrawLine(hit.point, position);
            Handles.DrawWireCube(position + bounds.center, bounds.size);
            Handles.SphereHandleCap(0, position, Quaternion.identity, .06f, EventType.Repaint);
            Handles.Label(position + Vector3.up * .4f, (gold ? "GOLD" : "COAL") + (allVariations ? " • ALL" : " • V" + (variation + 1)));
        }
        if (e.type == EventType.MouseDown && e.button == 0 && !e.control && !e.command && HandleUtility.nearestControl == id)
        { Place(block, variation, allVariations, gold, position); e.Use(); Repaint(); }
    }
    static void DrawMarkers(Transform[] markers, bool isGold)
    {
        if (markers == null || markers.Length == 0) return;
        var bounds = ItemBounds(isGold);
        Handles.color = isGold ? new Color(1, .75f, .1f) : Color.cyan;
        foreach (var marker in markers)
        {
            if (marker == null) continue;
            Handles.DrawWireCube(marker.position + bounds.center, bounds.size);
            Handles.Label(marker.position + Vector3.up * .35f, isGold ? "GOLD" : "COAL");
        }
    }
}
#endif
