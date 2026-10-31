#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Management;

[InitializeOnLoad]
static class DesktopPlaySceneButtons
{
    static readonly Color ActiveColor = new Color(0.35f, 0.75f, 0.45f);

    static DesktopPlaySceneButtons()
    {
        SceneView.duringSceneGui += OnScene;
        EditorApplication.playModeStateChanged += state =>
        {
            // Keep Link initialized even when starting with simulated input, so
            // F2 can hand the same rig to the headset during this Play session.
            if (state == PlayModeStateChange.ExitingEditMode) SetStandaloneXrInitOnStart(true);
        };
    }

    static void OnScene(SceneView sceneView)
    {
        Handles.BeginGUI();

        GUILayout.BeginArea(new Rect(8f, 8f, 168f, 78f));
        GUILayout.BeginHorizontal();
        if (ModeButton("Keyboard", DesktopPlay.UseDesktop))
            SetMode(true);
        if (ModeButton("VR", !DesktopPlay.UseDesktop))
            SetMode(false);
        GUILayout.EndHorizontal();
        using (new EditorGUI.DisabledScope(Application.isPlaying || LoginIsLoaded()))
        {
            if (GUILayout.Button("Load Login", GUILayout.Height(22f)))
                EditorSceneManager.OpenScene("Assets/Scenes/Login.unity", OpenSceneMode.Single);
        }
        if (GUILayout.Button("Clear PlayerPrefs", GUILayout.Height(22f)))
            ClearPlayerPrefs();
        GUILayout.EndArea();

        Handles.EndGUI();
    }

    static bool LoginIsLoaded()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            if (SceneManager.GetSceneAt(i).path == "Assets/Scenes/Login.unity")
                return true;
        }

        return false;
    }

    static bool ModeButton(string label, bool active)
    {
        var previous = GUI.backgroundColor;
        GUI.backgroundColor = active ? ActiveColor : Color.white;
        var style = new GUIStyle(GUI.skin.button);
        if (active)
            style.fontStyle = FontStyle.Bold;
        var pressed = GUILayout.Button(label, style, GUILayout.Height(22f));
        GUI.backgroundColor = previous;
        return pressed && !active;
    }

    static void ClearPlayerPrefs()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("Cleared all PlayerPrefs.");
    }

    static void SetMode(bool desktop)
    {
        if (Application.isPlaying)
        {
            SimulatedRigInput.Instance?.SetSimulating(desktop);
            return;
        }
        EditorPrefs.SetBool(DesktopPlay.PrefKey, desktop);
        DesktopPlay.SetSessionSimulation(null);
        SetStandaloneXrInitOnStart(true);
        SceneView.RepaintAll();
    }

    static void SetStandaloneXrInitOnStart(bool enabled)
    {
        var settings = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        if (settings == null || settings.InitManagerOnStart == enabled)
            return;

        settings.InitManagerOnStart = enabled;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssetIfDirty(settings);
    }
}
#endif
