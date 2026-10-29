using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Game.Audio;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using Object = UnityEngine.Object;

/// <summary>Idempotent starter content. Existing sound settings are never overwritten.</summary>
public static class DuckzAudioSetup
{
    const string Events = "Assets/Audio/Events";
    const string SoundSets = "Assets/Audio/SurfaceSoundSets";
    const string Prefabs = "Assets/Prefabs/Audio";
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    [MenuItem("Tools/DuckZ/Authoring/Audio/Create Starter Assets")]
    public static void BuildStarterAssets()
    {
        Directory.CreateDirectory(Events);
        Directory.CreateDirectory(SoundSets);
        Directory.CreateDirectory(Prefabs);
        Directory.CreateDirectory("Assets/Resources");
        AssetDatabase.Refresh();
        var mixer = CreateMixer();
        var settings = AssetDatabase.LoadAssetAtPath<Game.Audio.AudioSettings>("Assets/Resources/GameAudioSettings.asset");
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<Game.Audio.AudioSettings>();
            settings.effects = mixer.FindMatchingGroups("Effects").First();
            settings.voice = mixer.FindMatchingGroups("Voice").First();
            AssetDatabase.CreateAsset(settings, "Assets/Resources/GameAudioSettings.asset");
        }
        MakeEvent("ZombieSpider_Aggro", "Assets/Audio/Enemies/ZombieSpider", "ZombieSpider_Aggro", 64, 2);
        var steps = MakeSoundSet("ZombieSpider_Footsteps",
            MakeEvent("ZombieSpider_Footsteps", "Assets/Audio/Enemies/ZombieSpider", "ZombieSpider_Footstep", 160, 6));
        foreach (string cue in new[] { "Attack", "Idle", "Kill", "Wimper" })
            MakeEvent("ZombieSpider_" + cue, "Assets/Audio/Enemies/ZombieSpider", "ZombieSpider_" + cue, 100, 3);
        MakeEvent("Helicopter_Loop", "Assets/Audio/Helicopter", "Helicopter", 100, 1);
        MakeReverbPrefab();
        WireSpider(steps);
        AssetDatabase.SaveAssets();
        Debug.Log("Duckz audio starter assets ready: Events, Audio prefabs, GameAudio mixer/settings, and ZombieSpider footsteps.");
    }

    static AudioMixer CreateMixer()
    {
        const string path = "Assets/Audio/GameAudio.mixer";
        var existing = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
        if (existing != null) { EnsureMixerView(existing); return existing; }
        // Unity has no public mixer authoring API. Keep reflection confined to this editor setup tool.
        var type = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.Audio.AudioMixerController", true);
        var mixer = (AudioMixer)type.GetMethod("CreateMixerControllerAtPath", Flags).Invoke(null, new object[] { path });
        var master = type.GetProperty("masterGroup", Flags).GetValue(mixer);
        var groupType = master.GetType();
        var children = Array.CreateInstance(groupType, 2);
        string[] names = { "Effects", "Voice" };
        for (int i = 0; i < names.Length; i++)
            children.SetValue(type.GetMethod("CreateNewGroup", Flags).Invoke(mixer, new object[] { names[i], false }), i);
        groupType.GetProperty("children", Flags).SetValue(master, children);
        EditorUtility.SetDirty((Object)master);
        EditorUtility.SetDirty(mixer);
        EnsureMixerView(mixer);
        return mixer;
    }

    static void EnsureMixerView(AudioMixer mixer)
    {
        var type = mixer.GetType();
        var views = type.GetProperty("views", Flags);
        if (((Array)views.GetValue(mixer)).Length != 0) return;
        var groups = mixer.FindMatchingGroups(string.Empty);
        var ids = new GUID[groups.Length];
        for (int i = 0; i < groups.Length; i++)
            ids[i] = (GUID)groups[i].GetType().GetProperty("groupID", Flags).GetValue(groups[i]);
        var viewType = views.PropertyType.GetElementType();
        var view = Activator.CreateInstance(viewType);
        viewType.GetField("guids", Flags).SetValue(view, ids);
        viewType.GetField("name", Flags).SetValue(view, "All");
        var array = Array.CreateInstance(viewType, 1);
        array.SetValue(view, 0);
        views.SetValue(mixer, array);
        EditorUtility.SetDirty(mixer);
    }

    static SoundEvent MakeEvent(string name, string folder, string prefix, int priority, int limit, bool ambient = false)
    {
        string path = $"{Events}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<SoundEvent>(path);
        if (existing != null) return existing;
        var sound = ScriptableObject.CreateInstance<SoundEvent>();
        var so = new SerializedObject(sound);
        var clips = AssetDatabase.FindAssets("t:AudioClip", new[] { folder })
            .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p)
            .Where(p => Path.GetFileNameWithoutExtension(p).StartsWith(prefix, StringComparison.Ordinal))
            .Select(AssetDatabase.LoadAssetAtPath<AudioClip>).ToArray();
        var array = so.FindProperty("clips");
        array.arraySize = clips.Length;
        for (int i = 0; i < clips.Length; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        so.FindProperty("priority").intValue = priority;
        so.FindProperty("maxInstances").intValue = limit;
        if (ambient)
        {
            so.FindProperty("volumeRange").vector2Value = new Vector2(0.45f, 0.6f);
            so.FindProperty("pitchRange").vector2Value = new Vector2(0.98f, 1.02f);
            so.FindProperty("minDistance").floatValue = 5f;
            so.FindProperty("maxDistance").floatValue = 60f;
            so.FindProperty("cullDistance").floatValue = 65f;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(sound, path);
        return sound;
    }

    static SurfaceSoundSet MakeSoundSet(string name, SoundEvent fallback)
    {
        string path = $"{SoundSets}/{name}.asset";
        var existing = AssetDatabase.LoadAssetAtPath<SurfaceSoundSet>(path);
        if (existing != null) return existing;
        var set = ScriptableObject.CreateInstance<SurfaceSoundSet>();
        SetReference(set, "fallback", fallback);
        AssetDatabase.CreateAsset(set, path);
        return set;
    }

    static void MakeReverbPrefab()
    {
        string path = $"{Prefabs}/Reverb_Subway.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null) return;
        var go = new GameObject("Subway Reverb Zone");
        try
        {
            var zone = go.AddComponent<AudioReverbZone>();
            zone.reverbPreset = AudioReverbPreset.StoneCorridor;
            zone.minDistance = 6f;
            zone.maxDistance = 10f;
            PrefabUtility.SaveAsPrefabAsset(go, path);
        }
        finally { Object.DestroyImmediate(go); }
    }
    static void WireSpider(SurfaceSoundSet steps)
    {
        const string path = "Assets/Prefabs/Enemies/ZombieSpider.prefab";
        var footstepsType = Type.GetType("ProceduralFootstepAudio, Assembly-CSharp");
        if (footstepsType == null || AssetDatabase.LoadAssetAtPath<GameObject>(path) == null) return;
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            if (root.GetComponent<FootstepEmitter>() == null)
                SetReference(root.AddComponent<FootstepEmitter>(), "sounds", steps);
            if (root.GetComponent(footstepsType) == null) root.AddComponent(footstepsType);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
    static void SetReference(Object target, string field, Object value)
    {
        var so = new SerializedObject(target);
        so.FindProperty(field).objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    [MenuItem("Tools/DuckZ/Authoring/Audio/Add Player Footsteps To Selection")]
    static void AddPlayerFootsteps()
    {
        var go = Selection.activeGameObject;
        if (go == null || (go.GetComponent("NewPlayer") == null && go.GetComponent("DesktopPlayer") == null))
        { Debug.LogWarning("Select the local player root containing NewPlayer or DesktopPlayer."); return; }
        var emitter = go.GetComponent<FootstepEmitter>();
        if (emitter == null)
        {
            emitter = Undo.AddComponent<FootstepEmitter>(go);
            SetReference(emitter, "sounds", AssetDatabase.LoadAssetAtPath<SurfaceSoundSet>($"{SoundSets}/Player_Footsteps.asset"));
        }
        var adapter = Type.GetType("PlayerFootstepAudio, Assembly-CSharp");
        if (adapter != null && go.GetComponent(adapter) == null) Undo.AddComponent(go, adapter);
    }
}
