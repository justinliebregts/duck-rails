using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Display the runtime arm fit in scene and prefab editing without changing saved meshes or bones.</summary>
[InitializeOnLoad]
internal static class PlayerArmPrefabPreview
{
    sealed class Preview
    {
        public SkinnedMeshRenderer source, display;
        public bool wasRenderingOff;
        public readonly MaterialPropertyBlock properties = new MaterialPropertyBlock();

        public void Dispose()
        {
            if (source != null) source.forceRenderingOff = wasRenderingOff;
            if (display != null) Object.DestroyImmediate(display.gameObject);
        }
    }

    static readonly Dictionary<SkinnedMeshRenderer, Preview> previews = new Dictionary<SkinnedMeshRenderer, Preview>();
    static bool queued, refreshing, suspended;

    static PlayerArmPrefabPreview()
    {
        PrefabStage.prefabStageOpened += OnOpened;
        PrefabStage.prefabStageClosing += OnClosing;
        PrefabStage.prefabSaving += OnSaving;
        PrefabStage.prefabSaved += OnSaved;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneSaved += OnSceneSaved;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneClosing += OnSceneClosing;
        EditorSceneManager.sceneClosed += OnSceneClosed;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
        EditorApplication.hierarchyChanged += QueueRefresh;
        EditorApplication.projectChanged += QueueRefresh;
        ObjectChangeEvents.changesPublished += OnObjectChanges;
        Undo.undoRedoPerformed += QueueRefresh;
        AssemblyReloadEvents.beforeAssemblyReload += Suspend;
        EditorApplication.quitting += Suspend;
        QueueRefresh();
    }

    static void OnOpened(PrefabStage stage) { suspended = false; Refresh(); }
    static void OnClosing(PrefabStage stage) => SuspendUntilNextEditorTick();
    static void OnSceneSaving(Scene scene, string path) => SuspendUntilNextEditorTick();
    static void OnSceneSaved(Scene scene) => ResumeAfterSave();
    static void OnSceneOpened(Scene scene, OpenSceneMode mode) => ResumeAfterSave();
    static void OnSceneClosing(Scene scene, bool removingScene) => SuspendUntilNextEditorTick();
    static void OnSceneClosed(Scene scene) => ResumeAfterSave();
    static void OnSaving(GameObject root)
    {
        SuspendUntilNextEditorTick();
    }
    static void OnSaved(GameObject root) => ResumeAfterSave();
    static void ResumeAfterSave()
    {
        EditorApplication.update -= ResumeAfterSave;
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        suspended = false;
        QueueRefresh();
    }

    static void SuspendUntilNextEditorTick()
    {
        Suspend();
        // Saving/closing is synchronous; also recover if checkout/validation cancels the save.
        EditorApplication.update += ResumeAfterSave;
    }

    static void OnPlayModeChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.ExitingEditMode) Suspend();
        else if (state == PlayModeStateChange.EnteredEditMode) { suspended = false; QueueRefresh(); }
    }

    static void OnObjectChanges(ref ObjectChangeEventStream stream)
    {
        if (refreshing || suspended || EditorApplication.isPlayingOrWillChangePlaymode) return;
        for (int i = 0; i < stream.length; i++)
        {
            if (stream.GetEventType(i) != ObjectChangeKind.ChangeGameObjectOrComponentProperties) continue;
            stream.GetChangeGameObjectOrComponentPropertiesEvent(i, out var change);
            var changed = EditorUtility.EntityIdToObject(change.instanceId);
            // Our disposable renderers never need to trigger another refresh.
            if (changed != null && (changed.hideFlags & HideFlags.DontSaveInEditor) == 0)
            { QueueRefresh(); return; }
        }
    }

    static void QueueRefresh()
    {
        if (queued || suspended || EditorApplication.isPlayingOrWillChangePlaymode) return;
        queued = true;
        // One editor tick per change, including when no Inspector is repainting.
        EditorApplication.update += DelayedRefresh;
    }

    static void DelayedRefresh()
    {
        EditorApplication.update -= DelayedRefresh;
        queued = false;
        if (EditorApplication.isUpdating || EditorApplication.isCompiling) { QueueRefresh(); return; }
        Refresh();
    }

    static IEnumerable<GameObject> PreviewRoots()
    {
        var stage = PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
        {
            if (stage.prefabContentsRoot != null) yield return stage.prefabContentsRoot;
            yield break;
        }
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (!scene.isLoaded || EditorSceneManager.IsPreviewScene(scene)) continue;
            foreach (var root in scene.GetRootGameObjects())
                if ((root.hideFlags & HideFlags.DontSaveInEditor) == 0) yield return root;
        }
    }

    internal static void Refresh()
    {
        if (refreshing || suspended || EditorApplication.isPlayingOrWillChangePlaymode) return;
        refreshing = true;
        try
        {
            var seen = new HashSet<SkinnedMeshRenderer>();
            foreach (var root in PreviewRoots())
            foreach (var fit in root.GetComponentsInChildren<PlayerArmGeometry>(true))
            {
                if (!fit.enabled) continue; // Companion inherits a disabled opt-in.
                var source = new SerializedObject(fit).FindProperty("body").objectReferenceValue as SkinnedMeshRenderer;
                if (source == null || source.sharedMesh == null || source.gameObject.scene != fit.gameObject.scene) continue;
                Mesh fitted = fit.Fit(source.sharedMesh, source.bones);
                if (fitted == source.sharedMesh) continue; // Already fitted or not a supported player rig.
                seen.Add(source);
                if (!previews.TryGetValue(source, out var preview) || preview.display == null)
                {
                    preview?.Dispose();
                    // Duplication and Undo can copy our non-saving child and the source's hidden state.
                    // Adopt that child, whose visibility retains the original state, instead of doubling it.
                    SkinnedMeshRenderer display = null;
                    foreach (var child in source.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (child.transform.parent != source.transform || child.name != "Player arm mesh preview"
                            || (child.hideFlags & HideFlags.DontSaveInEditor) == 0) continue;
                        if (display == null) display = child;
                        else Object.DestroyImmediate(child.gameObject);
                    }
                    bool wasRenderingOff = display != null ? display.forceRenderingOff : source.forceRenderingOff;
                    if (display == null)
                    {
                        var go = new GameObject("Player arm mesh preview") { hideFlags = HideFlags.HideAndDontSave };
                        SceneManager.MoveGameObjectToScene(go, source.gameObject.scene);
                        go.transform.SetParent(source.transform, false);
                        display = go.AddComponent<SkinnedMeshRenderer>();
                    }
                    preview = new Preview { source = source, display = display, wasRenderingOff = wasRenderingOff };
                    previews[source] = preview;
                }

                // Copy rendering settings, materials, skin weights and bones; no authored object is serialized differently.
                EditorUtility.CopySerialized(source, preview.display);
                preview.display.hideFlags = HideFlags.HideAndDontSave;
                preview.display.sharedMesh = fitted;
                preview.display.forceRenderingOff = preview.wasRenderingOff;
                var bounds = source.localBounds;
                bounds.Encapsulate(bounds.min * PlayerArmGeometry.LengthScale);
                bounds.Encapsulate(source.localBounds.max * PlayerArmGeometry.LengthScale);
                preview.display.localBounds = bounds;
                source.GetPropertyBlock(preview.properties);
                preview.display.SetPropertyBlock(preview.properties);
                for (int i = 0; i < source.sharedMaterials.Length; i++)
                {
                    source.GetPropertyBlock(preview.properties, i);
                    preview.display.SetPropertyBlock(preview.properties, i);
                }
                source.forceRenderingOff = true;
            }
            var removed = new List<SkinnedMeshRenderer>();
            foreach (var pair in previews)
                if (!seen.Contains(pair.Key)) { pair.Value.Dispose(); removed.Add(pair.Key); }
            foreach (var source in removed) previews.Remove(source);
        }
        finally { refreshing = false; }
        SceneView.RepaintAll();
    }

    internal static void Clear()
    {
        foreach (var preview in previews.Values) preview.Dispose();
        previews.Clear();
        // Fitted meshes belong to PlayerArmGeometry's shared cache, also reused by gameplay.
        // Its existing reload/quit cleanup owns their lifetime; do not destroy shared meshes here.
    }

    static void Suspend()
    {
        suspended = true;
        EditorApplication.update -= DelayedRefresh;
        EditorApplication.update -= ResumeAfterSave;
        queued = false;
        Clear();
    }
}
