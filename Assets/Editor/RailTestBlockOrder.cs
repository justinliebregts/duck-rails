#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
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
    static RailTestBlockOrderPanel()
    {
        RailTestTools.OrderedRoute = Select;
        RailTestTools.EnemyInvincibilityRequested = () => RailTestBlockOrder.instance.invincibleFromEnemies && !RailTestRunner.AutomatedRunning;
    }
    public static void DrawPlayerOptions()
    {
        var settings = RailTestBlockOrder.instance;
        EditorGUI.BeginChangeCheck();
        settings.invincibleFromEnemies = EditorGUILayout.Toggle(new GUIContent("Invincible from enemies",
            "Protect this local player from enemy contact attacks and bomber explosions in private development runs. Applies immediately; saved between editor sessions. Automated suites ignore it."), settings.invincibleFromEnemies);
        if (EditorGUI.EndChangeCheck()) settings.Persist();
        if (settings.invincibleFromEnemies)
            GUILayout.Label(RailTestTools.EnemyInvincible ? "Enemy invincibility active — local player" :
                "Enemy invincibility selected — active in private manual runs", EditorStyles.wordWrappedLabel);
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
            return RailRoute.Choose(run, RailRoute.Instance.catalog, UnityEngine.Random.Range(0, int.MaxValue));
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
