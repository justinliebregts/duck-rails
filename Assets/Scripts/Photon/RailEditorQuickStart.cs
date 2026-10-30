#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using Unity.Multiplayer.PlayMode;
using UnityEditor;
using UnityEngine;

/// <summary>Local MPPM rendezvous only; authentication and world loading stay unchanged.</summary>
[InitializeOnLoad]
public static class RailEditorQuickStart
{
    const string MenuPath = "Duck Rails/Two Ducks Quick Start";
    const string HostTag = "RailsQuickStart";
    public const string RoomCode = "11111";
    static string Key => MppmStandaloneCredentials.PrefsKeyForAssets(Application.dataPath) + ".QuickStart";

    public static bool Enabled
    {
        get => EditorPrefs.GetBool(Key + ".Enabled", true);
        set => EditorPrefs.SetBool(Key + ".Enabled", value);
    }

    static RailEditorQuickStart()
    {
        if (!CurrentPlayer.IsMainEditor) return;
        EditorApplication.update += SyncSelectedMainTag;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) PublishSelectedMainTag();
        };
    }

    static void SyncSelectedMainTag()
    {
        if (!EditorApplication.isPlayingOrWillChangePlaymode) PublishSelectedMainTag();
    }

    static void PublishSelectedMainTag()
    {
        string tag = SelectedMainEditorTag() ?? "";
        if (EditorPrefs.GetString(Key + ".MainTag", "") != tag)
            EditorPrefs.SetString(Key + ".MainTag", tag);
    }

    public static bool TryGetRoom(out string room)
    {
        room = null;
        string mainTag = CurrentPlayer.IsMainEditor ? SelectedMainEditorTag() : EditorPrefs.GetString(Key + ".MainTag", "");
        if (!ShouldQuickStart(Enabled, CurrentPlayer.IsMainEditor, CurrentPlayer.Tags, mainTag)) return false;
        room = RoomCode;
        return true;
    }

    public static bool ShouldQuickStart(bool enabled, bool mainEditor, IReadOnlyList<string> runtimeTags, string selectedMainTag)
    {
        // Main-editor runtime tags can be empty/stale even with a tagged scenario
        // selected. Its launch configuration is the source of truth. Do not let a
        // stale runtime tag enable quick start after switching back to solo Play.
        return enabled && selectedMainTag == HostTag &&
            (mainEditor || runtimeTags != null && runtimeTags.Contains("Player2"));
    }

    static string SelectedMainEditorTag()
    {
        if (!CurrentPlayer.IsMainEditor) return null;
        // Unity 6.3 keeps its active scenario API internal. Read the serialized
        // selection instead of reflecting into its lifecycle methods or relying
        // on an asset name (which can be renamed or exist without being selected).
        foreach (var manager in Resources.FindObjectsOfTypeAll<ScriptableObject>())
        {
            if (manager.GetType().FullName != "Unity.PlayMode.Editor.PlayModeManager") continue;
            using (var state = new SerializedObject(manager))
            {
                var scenario = state.FindProperty("m_Config")?.objectReferenceValue;
                if (scenario == null) return null;
                using (var config = new SerializedObject(scenario))
                {
                    if (config.FindProperty("m_EnableEditors")?.boolValue != true) return null;
                    return config.FindProperty("m_MainEditorInstance.m_PlayerTag")?.stringValue;
                }
            }
        }
        return null;
    }

    [MenuItem(MenuPath)]
    static void Toggle() => Enabled = !Enabled;

    [MenuItem(MenuPath, true)]
    static bool Validate()
    {
        Menu.SetChecked(MenuPath, Enabled);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
#endif
