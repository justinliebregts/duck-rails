#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using Unity.Multiplayer.PlayMode;
using UnityEngine;

[FilePath("UserSettings/RailTestBlockOrder.asset", FilePathAttribute.Location.ProjectFolder)]
public sealed class RailTestBlockOrder : ScriptableSingleton<RailTestBlockOrder>
{
    public bool useOrder;
    public bool invincibleFromEnemies;
    public List<RailBlock> blocks = new List<RailBlock>();
    public void Persist() => Save(true);
}

[InitializeOnLoad]
public static class RailTestBlockOrderPanel
{
    static ReorderableList list;
    static bool sharedInvincibility;
    static bool? publishedInvincibility;
    static double nextPlayerOptionsRead;
    static string PlayerOptionsPath
    {
        get
        {
            string assets = Application.dataPath.Replace('\\', '/');
            int virtualRoot = assets.IndexOf("/Library/VP/", StringComparison.OrdinalIgnoreCase);
            string root = virtualRoot >= 0 ? assets.Substring(0, virtualRoot) : Path.GetDirectoryName(assets);
            return Path.Combine(root, "Temp", "rails-player-invincibility.txt");
        }
    }
    public static bool InvincibilityRequested => CurrentPlayer.IsMainEditor
        ? RailTestBlockOrder.instance.invincibleFromEnemies : sharedInvincibility;
    static RailTestBlockOrderPanel()
    {
        RailTestTools.OrderedRoute = Select;
        RailTestTools.EnemyInvincibilityRequested = () => InvincibilityRequested;
        if (CurrentPlayer.IsMainEditor)
        {
            PublishPlayerOptions();
            EditorApplication.update += PublishPlayerOptions;
            EditorApplication.playModeStateChanged += _ => PublishPlayerOptions();
        }
        else
        {
            ReadPlayerOptions();
            EditorApplication.update += ReadPlayerOptions;
        }
    }
    public static void PublishPlayerOptions()
    {
        if (!CurrentPlayer.IsMainEditor) return;
        bool enabled = RailTestBlockOrder.instance.invincibleFromEnemies;
        if (publishedInvincibility == enabled) return;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PlayerOptionsPath));
            File.WriteAllText(PlayerOptionsPath, enabled.ToString());
            publishedInvincibility = enabled;
        }
        catch (IOException) { } // Retry next update if a virtual editor is reading.
    }
    static void ReadPlayerOptions()
    {
        if (EditorApplication.timeSinceStartup < nextPlayerOptionsRead) return;
        nextPlayerOptionsRead = EditorApplication.timeSinceStartup + .25;
        // EditorPrefs can cache values per process. Poll the shared project file
        // so already-running virtual editors see the main editor's live toggle.
        try
        {
            if (bool.TryParse(File.ReadAllText(PlayerOptionsPath), out bool enabled)) sharedInvincibility = enabled;
        }
        catch (IOException) { } // Retry if the main editor is currently writing.
    }
    public static void DrawPlayerOptions()
    {
        var settings = RailTestBlockOrder.instance;
        using (new EditorGUI.DisabledScope(!CurrentPlayer.IsMainEditor))
        {
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.Toggle(new GUIContent("Invincible from enemies",
                "Protect all local test clients from enemy contact attacks and bomber explosions in every test expedition, including automated suites. Change this in the main editor; saved between sessions."), InvincibilityRequested);
            if (EditorGUI.EndChangeCheck()) { settings.invincibleFromEnemies = enabled; settings.Persist(); PublishPlayerOptions(); }
        }
        if (InvincibilityRequested)
            GUILayout.Label(RailTestTools.EnemyInvincible ? "Enemy invincibility active — shared by test clients" :
                "Enemy invincibility selected — active in all test expeditions", EditorStyles.wordWrappedLabel);
    }
    public static RailRouteState Select(string run)
    {
        var settings = RailTestBlockOrder.instance;
        if (!settings.useOrder || RailTestRunner.AutomatedRunning) return null;
        try
        {
            var state = RailRoute.ChooseOrdered(run, settings.blocks.ToArray(), 12345);
            RailRoute.Validate(state, RailRoute.Instance.catalog);
            RailTestTools.Status = "Using Test Block Order (" + state.blocks.Length + " blocks)";
            return state;
        }
        catch (Exception e)
        {
            RailTestTools.Status = "Test Block Order invalid: " + e.Message + " Using normal random route.";
            // Do not silently select a stale reproduction preset instead.
            return RailRoute.ChooseNormal(run, RailRoute.Instance.catalog, UnityEngine.Random.Range(0, int.MaxValue));
        }
    }
    public static void Disable()
    {
        RailTestBlockOrder.instance.useOrder = false;
        RailTestBlockOrder.instance.Persist();
    }
    public static void Draw()
    {
        var settings = RailTestBlockOrder.instance;
        GUILayout.Space(8);
        GUILayout.Label("Test Block Order", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        settings.useOrder = EditorGUILayout.Toggle("Use Test Block Order", settings.useOrder);
        if (list == null || list.list != settings.blocks)
        {
            list = new ReorderableList(settings.blocks, typeof(RailBlock), true, true, true, true);
            list.drawHeaderCallback = rect => EditorGUI.LabelField(rect, "Scavenging prefabs — top to bottom");
            list.drawElementCallback = (rect, index, active, focused) =>
            {
                rect.height = EditorGUIUtility.singleLineHeight;
                settings.blocks[index] = (RailBlock)EditorGUI.ObjectField(rect, (index + 1).ToString(), settings.blocks[index], typeof(RailBlock), false);
            };
            list.onAddCallback = _ => { settings.blocks.Add(null); settings.Persist(); };
            list.onRemoveCallback = l => { settings.blocks.RemoveAt(l.index); settings.Persist(); };
            list.onReorderCallback = _ => settings.Persist();
        }
        list.DoLayoutList();
        if (EditorGUI.EndChangeCheck()) settings.Persist();
        var drop = GUILayoutUtility.GetRect(0, 32, GUILayout.ExpandWidth(true));
        GUI.Box(drop, "Drop block prefabs here to append");
        var evt = Event.current;
        if (drop.Contains(evt.mousePosition) && (evt.type == EventType.DragUpdated || evt.type == EventType.DragPerform))
        {
            var added = DragAndDrop.objectReferences.Select(o => o is GameObject go ? go.GetComponent<RailBlock>() : o as RailBlock)
                .Where(b => b != null && EditorUtility.IsPersistent(b)).ToArray();
            DragAndDrop.visualMode = added.Length > 0 ? DragAndDropVisualMode.Copy : DragAndDropVisualMode.Rejected;
            if (evt.type == EventType.DragPerform && added.Length > 0)
            { DragAndDrop.AcceptDrag(); settings.blocks.AddRange(added); settings.Persist(); }
            evt.Use();
        }
        EditorGUILayout.HelpBox("Saved automatically. Applies to the next private run hosted by this editor, including Manual sandbox. Station and bridge are automatic. Automated suites ignore this list. Disable it to use presets/random routes.", MessageType.Info);
        if (settings.useOrder)
        {
            try
            {
                if (settings.blocks.Any(b => b != null && !EditorUtility.IsPersistent(b))) throw new Exception("Use prefab assets, not scene objects.");
                RailRoute.ChooseOrdered("preview", settings.blocks.ToArray(), 12345);
                GUILayout.Label("Station → " + string.Join(" → ", settings.blocks.Select(b => b.displayName ?? b.name)) + " → Bridge", EditorStyles.wordWrappedLabel);
            }
            catch (Exception e) { EditorGUILayout.HelpBox(e.Message, MessageType.Warning); }
        }
    }
}
#endif
