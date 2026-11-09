#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Stage-local candidate painting for the deterministic block spawn pools.</summary>
public sealed class RailLootPlacementWindow : EditorWindow
{
    [SerializeField] bool placing;
    [SerializeField] RailSpawnKind kind;
    [SerializeField] float clearance = .05f;
    [SerializeField] RailBlock sceneBlock;
    Collider[] surfaces = Array.Empty<Collider>();
    RailBlock cachedBlock;
    Game.AI.CrawlGraph graph;
    TextAsset cachedGraph;
    bool zHeld;
    static readonly string[] Names = { "Coal", "Gold", "Enemy" };

    [MenuItem("Duck Rails/Authoring/Place Coal, Gold and Enemies")]
    public static void Open() => GetWindow<RailLootPlacementWindow>("Block Spawners");

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
    void Invalidate() { cachedBlock = null; cachedGraph = null; graph = null; Repaint(); SceneView.RepaintAll(); }
    RailBlock Target()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null) return stage.prefabContentsRoot.GetComponentInChildren<RailBlock>();
        if (sceneBlock != null && !EditorUtility.IsPersistent(sceneBlock)) return sceneBlock;
        return Selection.activeGameObject != null ? Selection.activeGameObject.GetComponentInParent<RailBlock>() : null;
    }
    void OnGUI()
    {
        EditorGUILayout.HelpBox("Open a block prefab, choose a spawner, then enable placement. Click Environment floors in Scene view. Z: undo / Shift+Z: redo / Esc: stop. Alt/right mouse still navigate.", MessageType.Info);
        if (PrefabStageUtility.GetCurrentPrefabStage() == null)
            sceneBlock = (RailBlock)EditorGUILayout.ObjectField("Scene block", sceneBlock, typeof(RailBlock), true);
        var block = Target();
        EditorGUILayout.LabelField("Target", block != null ? block.name : "Open or select a RailBlock");
        kind = (RailSpawnKind)GUILayout.Toolbar((int)kind, Names);
        if (kind != RailSpawnKind.Enemy)
            clearance = Mathf.Max(0, EditorGUILayout.FloatField("Clearance above floor (m)", clearance));
        if (block == null) { placing = false; return; }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Fixed authoring/finale counts", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            int coal = Mathf.Max(0, EditorGUILayout.IntField("Coal", block.coalCount));
            int gold = Mathf.Max(0, EditorGUILayout.IntField("Gold", block.goldCount));
            int enemies = Mathf.Max(0, EditorGUILayout.IntField("Enemies", block.enemyCount));
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(block, "Change block spawn counts");
                RailSpawnPool.Migrate(block);
                block.coalCount = coal; block.goldCount = gold; block.enemyCount = enemies;
                Dirty(block);
            }
            foreach (RailSpawnKind type in Enum.GetValues(typeof(RailSpawnKind)))
            {
                int available = RailSpawnPool.Available(block, type).Length;
                int requested = RailSpawnPool.Count(block, type);
                EditorGUILayout.LabelField(Names[(int)type] + " candidates", available + " unique active locations");
                if (available < requested)
                    EditorGUILayout.HelpBox($"{Names[(int)type]}: fixed preview requests {requested}, but only {available} locations exist. Scavenging routes use separate crew-scaled quotas; validate those before enabling a block.", MessageType.Warning);
            }
            EditorGUILayout.HelpBox("The counts above are fixed authoring/finale counts; zero disables a type. Scavenging routes use crew-scaled resource counts and 1/2/3 enemies per quadrant. Enemy pools must support three per quadrant with 15m horizontal separation across all selected points, including quadrant boundaries. Keep markers within enemy bounds and near baked ground navigation. Save the prefab normally.", MessageType.None);
            placing = GUILayout.Toggle(placing, placing ? "Placing — click in Scene view" : "Start placing", "Button", GUILayout.Height(32));
        }
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
        return distance < float.PositiveInfinity && Vector3.Dot(nearest.normal, Vector3.up) >= .65f;
    }

    internal static Bounds ItemBounds(bool isGold)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(isGold ? "Assets/DuckRails/Economy/RailGoldBar.prefab" : "Assets/Resources/Rails/RailCoal.prefab");
        if (prefab == null) throw new InvalidOperationException("Missing runtime loot prefab.");
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
    static Bounds PreviewBounds(RailSpawnKind type) => type == RailSpawnKind.Enemy
        ? new Bounds(Vector3.up * .4f, new Vector3(.8f, .8f, .8f)) : ItemBounds(type == RailSpawnKind.Gold);
    static Color Tint(RailSpawnKind type) => type == RailSpawnKind.Enemy ? Color.red : type == RailSpawnKind.Gold ? Color.yellow : Color.green;
    static void Dirty(RailBlock block)
    {
        PrefabUtility.RecordPrefabInstancePropertyModifications(block);
        EditorUtility.SetDirty(block);
        EditorSceneManager.MarkSceneDirty(block.gameObject.scene);
    }

    internal static Transform Place(RailBlock block, RailSpawnKind type, Vector3 position)
    {
        if (block == null) throw new ArgumentException("Choose a block.");
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();
        string action = "Place " + Names[(int)type] + " Spawner";
        Undo.SetCurrentGroupName(action);
        Undo.RecordObject(block, action);
        RailSpawnPool.Migrate(block);
        var marker = RailSpawnerPrefabs.Instantiate(type, block.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(marker, action);
        var parent = block.transform.Find(Names[(int)type] + " Spawns") ?? block.transform;
        Undo.SetTransformParent(marker.transform, parent, action);
        Undo.RecordObject(marker.transform, action);
        marker.transform.position = position;
        PrefabUtility.RecordPrefabInstancePropertyModifications(marker.transform);
        Undo.RecordObject(block, action);
        RailSpawnPool.SetPoints(block.spawnCandidates, type, RailSpawnPool.Points(block.spawnCandidates, type).Concat(new[] { marker.transform }).ToArray());
        Dirty(block);
        Undo.FlushUndoRecordObjects();
        Undo.CollapseUndoOperations(group);
        return marker.transform;
    }

    internal static bool EnemyLocation(RailBlock block, Vector3 position, Game.AI.CrawlGraph navigation) =>
        navigation != null && block.enemyBounds.Contains(block.transform.InverseTransformPoint(position)) &&
        navigation.FindNearestGroundNode(block.transform.InverseTransformPoint(position), .8f) >= 0;

    void DuringSceneGUI(SceneView view)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) { placing = false; return; }
        var block = Target();
        if (block == null) return;
        var e = Event.current;
        if (e.type == EventType.Repaint)
            foreach (RailSpawnKind type in Enum.GetValues(typeof(RailSpawnKind))) DrawMarkers(RailSpawnPool.Candidates(block, type), type);
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
        { surfaces = block.GetComponentsInChildren<Collider>(); cachedBlock = block; cachedGraph = null; graph = null; }
        if (kind == RailSpawnKind.Enemy && cachedGraph != block.graph)
        { cachedGraph = block.graph; graph = block.graph != null ? Game.AI.CrawlGraph.Load(block.graph) : null; }
        if (e.type == EventType.MouseMove) view.Repaint();
        Physics.SyncTransforms();
        if (!FloorHit(surfaces, HandleUtility.GUIPointToWorldRay(e.mousePosition), out var hit)) return;
        var bounds = PreviewBounds(kind);
        // Upright item bounds must clear the entire supporting plane, including sloped floors.
        float slopeLift = (Mathf.Abs(hit.normal.x) * bounds.extents.x + Mathf.Abs(hit.normal.z) * bounds.extents.z
            - hit.normal.x * bounds.center.x - hit.normal.z * bounds.center.z) / hit.normal.y;
        var position = hit.point + Vector3.up * (kind == RailSpawnKind.Enemy ? .05f : clearance - bounds.min.y + slopeLift);
        bool valid = kind != RailSpawnKind.Enemy || EnemyLocation(block, position, graph);
        if (e.type == EventType.Repaint)
        {
            Handles.color = valid ? Tint(kind) : Color.gray;
            Handles.DrawWireDisc(hit.point, hit.normal, .25f);
            Handles.DrawLine(hit.point, position);
            Handles.DrawWireCube(position + bounds.center, bounds.size);
            Handles.SphereHandleCap(0, position, Quaternion.identity, .06f, EventType.Repaint);
            if (!valid) Handles.Label(position + Vector3.up * .9f, "Enemy needs baked ground within enemy bounds");
        }
        if (e.type == EventType.MouseDown && e.button == 0 && !e.control && !e.command && HandleUtility.nearestControl == id)
        { if (valid) Place(block, kind, position); e.Use(); Repaint(); }
    }
    static void DrawMarkers(Transform[] markers, RailSpawnKind type)
    {
        if (markers == null || markers.Length == 0) return;
        var bounds = PreviewBounds(type);
        Handles.color = Tint(type);
        foreach (var marker in markers)
        {
            if (marker == null || !marker.gameObject.activeInHierarchy || marker.GetComponent<RailSpawnMarker>() != null) continue;
            Handles.DrawWireCube(marker.position + bounds.center, bounds.size);
            Handles.Label(marker.position + Vector3.up * .35f, Names[(int)type].ToUpperInvariant());
        }
    }
}
#endif
