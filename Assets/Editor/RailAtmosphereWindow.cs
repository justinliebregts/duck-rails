using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Render-only preview: never serialize preview environment values into a scene.</summary>
public sealed class RailAtmosphereWindow : EditorWindow
{
    [SerializeField] float progress = 2;
    bool preview;
    RailAtmosphereSettings settings;
    SerializedObject serializedSettings;
    RailAtmosphere controller;
    Material sceneSky;
    SceneView view;
    bool oldFog, oldSky, oldLighting;
    Vector2 scroll;

    [MenuItem("Duck Rails/Lighting/Atmosphere Tuner")]
    public static void Open() => GetWindow<RailAtmosphereWindow>("Atmosphere Tuner");

    [MenuItem("Duck Rails/Lighting/Check Atmosphere Tuner")]
    public static void CheckPreview()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new System.InvalidOperationException("Edit Mode only.");
        var window = GetWindow<RailAtmosphereWindow>();
        window.StopPreview();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        bool dirty = scene.isDirty;
        var sky = RenderSettings.skybox;
        var color = RenderSettings.fogColor;
        float density = RenderSettings.fogDensity;
        var ambient = RenderSettings.ambientLight;
        var mode = RenderSettings.ambientMode;
        float progress = window.progress;
        int checks = 0;
        void Check(bool ok, string name) { if (!ok) throw new System.Exception(name); checks++; }
        try
        {
            window.StartPreview();
            var settings = window.settings;
            Check(settings != null && Resources.Load<RailAtmosphereSettings>(RailAtmosphereSettings.ResourcePath) == settings, "Gameplay resource");
            for (int i = 0; i < 3; i++)
            {
                window.progress = i;
                window.BeginCamera(window.view.camera);
                Check(Mathf.Approximately(RenderSettings.fogDensity, i == 0 ? settings.nightDensity : i == 1 ? settings.preDawnDensity : settings.dawnDensity), "Stage density");
                Check(RenderSettings.fogColor == (i == 0 ? settings.nightColor : i == 1 ? settings.preDawnColor : settings.dawnColor), "Stage color");
                window.EndCamera(window.view.camera);
                Check(RenderSettings.skybox == sky && RenderSettings.fogColor == color && RenderSettings.fogDensity == density && RenderSettings.ambientLight == ambient && RenderSettings.ambientMode == mode, "Render restoration");
            }
            window.StopPreview();
            Check(scene.isDirty == dirty, "Scene dirty state preserved");
            var custom = Instantiate(settings);
            custom.dawnDensity = .023f;
            custom.dawnColor = Color.magenta;
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/ExpeditionSky.mat");
            var controller = new RailAtmosphere(custom);
            try
            {
                controller.Apply(1.5f);
                Check(Mathf.Approximately(RenderSettings.fogDensity, (custom.preDawnDensity + .023f) / 2), "Custom midpoint");
                controller.Apply(2);
                Check(RenderSettings.fogColor == Color.magenta && Mathf.Approximately(RenderSettings.fogDensity, .023f), "Custom dawn");
            }
            finally { controller.Restore(); RenderSettings.skybox = sky; DestroyImmediate(custom); }
            Check(RailAtmosphere.Progress(100, false, 100) == 1 && RailAtmosphere.Progress(100, true, 20) == 2, "Timing and failure unchanged");
            Debug.Log($"Atmosphere Tuner: {checks} Edit Mode checks passed. Gameplay/headset appearance remains untested.");
        }
        finally { window.StopPreview(); window.progress = progress; }
    }

    void OnEnable()
    {
        settings = AssetDatabase.LoadAssetAtPath<RailAtmosphereSettings>(RailAtmosphereSettings.AssetPath);
        if (settings != null) serializedSettings = new SerializedObject(settings);
        RenderPipelineManager.beginCameraRendering += BeginRender;
        RenderPipelineManager.endCameraRendering += EndRender;
        Camera.onPreCull += BeginCamera;
        Camera.onPostRender += EndCamera;
        EditorApplication.playModeStateChanged += PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload += StopPreview;
        Undo.undoRedoPerformed += RepaintViews;
    }

    void OnDisable()
    {
        StopPreview();
        RenderPipelineManager.beginCameraRendering -= BeginRender;
        RenderPipelineManager.endCameraRendering -= EndRender;
        Camera.onPreCull -= BeginCamera;
        Camera.onPostRender -= EndCamera;
        EditorApplication.playModeStateChanged -= PlayModeChanged;
        AssemblyReloadEvents.beforeAssemblyReload -= StopPreview;
        Undo.undoRedoPerformed -= RepaintViews;
    }

    void PlayModeChanged(PlayModeStateChange state) => StopPreview();
    void RepaintViews() { Repaint(); SceneView.RepaintAll(); }
    void BeginRender(ScriptableRenderContext context, Camera camera) => BeginCamera(camera);
    void EndRender(ScriptableRenderContext context, Camera camera) => EndCamera(camera);

    void BeginCamera(Camera camera)
    {
        if (!preview || EditorApplication.isPlayingOrWillChangePlaymode || view == null || camera != view.camera || controller != null) return;
        sceneSky = RenderSettings.skybox;
        // Bake/review scenes may have no storm sky. Use the production sky for preview only.
        if (!RailAtmosphere.IsStormSky(sceneSky))
            RenderSettings.skybox = AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/ExpeditionSky.mat");
        controller = new RailAtmosphere(settings);
        try { controller.Apply(progress); }
        catch { RestoreRender(); throw; }
    }

    void EndCamera(Camera camera)
    {
        if (view != null && camera == view.camera) RestoreRender();
    }

    void RestoreRender()
    {
        if (controller == null) return;
        controller.Restore();
        controller = null;
        RenderSettings.skybox = sceneSky;
    }

    void StartPreview()
    {
        view = SceneView.lastActiveSceneView;
        if (view == null) view = GetWindow<SceneView>();
        oldFog = view.sceneViewState.showFog;
        oldSky = view.sceneViewState.showSkybox;
        oldLighting = view.sceneLighting;
        view.sceneViewState.showFog = true;
        view.sceneViewState.showSkybox = true;
        view.sceneLighting = true;
        preview = true;
        RepaintViews();
    }

    void StopPreview()
    {
        RestoreRender();
        if (preview && view != null)
        {
            view.sceneViewState.showFog = oldFog;
            view.sceneViewState.showSkybox = oldSky;
            view.sceneLighting = oldLighting;
        }
        preview = false;
        view = null;
        RepaintViews();
    }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Tune saved gameplay fog, storm sky and distant mountain colours below. Preview affects the Scene View only, outside Play Mode. Lower density means clearer air. No lighting rebake is needed for fog or sky changes.", MessageType.Info);
        if (settings == null)
        {
            EditorGUILayout.HelpBox("The shared Atmosphere asset is missing.", MessageType.Error);
            return;
        }
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("Open Expedition and frame dawn exit"))
            {
                StopPreview();
                if (UnityEditor.SceneManagement.EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                {
                    UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
                    var route = Object.FindFirstObjectByType<RailRoute>();
                    if (route != null && route.finaleRoot != null)
                    {
                        var sceneView = SceneView.lastActiveSceneView;
                        if (sceneView == null) sceneView = GetWindow<SceneView>();
                        sceneView.LookAt(route.finaleRoot.position + new Vector3(0, 3, 190), Quaternion.identity, 15, false, true);
                    }
                    StartPreview();
                }
            }
            if (GUILayout.Button(preview ? "Stop preview" : "Preview in Scene View"))
            { if (preview) StopPreview(); else StartPreview(); }
            EditorGUI.BeginChangeCheck();
            progress = EditorGUILayout.Slider("Deep night > Night > Pre-dawn > Dawn", progress, -1, 2);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Deep night")) { progress = -1; GUI.changed = true; }
                if (GUILayout.Button("Night")) { progress = 0; GUI.changed = true; }
                if (GUILayout.Button("Pre-dawn")) { progress = 1; GUI.changed = true; }
                if (GUILayout.Button("Dawn")) { progress = 2; GUI.changed = true; }
            }
            if (EditorGUI.EndChangeCheck()) RepaintViews();
            scroll = EditorGUILayout.BeginScrollView(scroll);
            serializedSettings.Update();
            EditorGUI.BeginChangeCheck();
            foreach (string name in new[] { "nightColor", "nightDensity", "preDawnColor", "preDawnDensity", "dawnColor", "dawnDensity", "deepNightSky", "nightSky", "preDawnSky", "dawnSky" })
                EditorGUILayout.PropertyField(serializedSettings.FindProperty(name), true);
            if (EditorGUI.EndChangeCheck())
            {
                serializedSettings.ApplyModifiedProperties();
                RepaintViews();
            }
            EditorGUILayout.EndScrollView();
            if (GUILayout.Button("Save fog and sky settings")) AssetDatabase.SaveAssetIfDirty(settings);
        }
        EditorGUILayout.HelpBox("Settings edits affect future game runs even when preview is off; use Undo to revert edits. Preview includes the sky, mountain ring, ambient and sun transition. Baked lighting and runtime block loading are not simulated. For the ending, open Expedition and navigate to the far side of the finale tunnel.", MessageType.None);
    }
}
