using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Serializes additive lighting loads, rebinding indices when Unity's atlas list changes.
/// No runtime baking. Unity owns merging/removal of each companion scene's baked probes.</summary>
public sealed class RailBlockLightingLoader : MonoBehaviour
{
    sealed class Entry
    {
        public RailBakedBlockLighting owner;
        public bool wanted = true;
        public readonly List<Scene> scenes = new List<Scene>(2);
        public LightProbes probes;
        public Vector3[] localPositions;
    }
    static RailBlockLightingLoader instance;
    readonly List<Entry> entries = new List<Entry>();
    readonly Queue<Entry> pending = new Queue<Entry>();
    bool working;
    bool quitting;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { instance = null; }

    public static void Add(RailBakedBlockLighting owner)
    {
        if (instance == null)
        {
            var go = new GameObject("Block lighting loader");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<RailBlockLightingLoader>();
        }
        owner.Ready = false;
        owner.Failure = null;
        var entry = new Entry { owner = owner };
        instance.entries.Add(entry);
        instance.pending.Enqueue(entry);
    }
    public static void Remove(RailBakedBlockLighting owner)
    {
        if (instance == null) return;
        foreach (var entry in instance.entries)
            if (entry.owner == owner) entry.wanted = false;
    }
    void OnApplicationQuit() { quitting = true; }
    void Update()
    {
        if (working || quitting) return;
        bool relocated = false;
        foreach (var entry in entries)
        {
            if (!entry.wanted || entry.owner == null || entry.probes == null || entry.localPositions == null) continue;
            if ((entry.owner.transform.position - entry.owner.LoadedPosition).sqrMagnitude < .000001f) continue;
            try { PlaceProbes(entry); relocated = true; }
            catch (Exception ex) { Fail(entry.owner, ex.Message); }
        }
        if (relocated) LightProbes.Tetrahedralize();
        bool needed = pending.Count > 0;
        foreach (var entry in entries) if (!entry.wanted || entry.owner == null) needed = true;
        if (needed) StartCoroutine(Process());
    }
    IEnumerator Process()
    {
        working = true;
        // Releases run before new loads, including an owner destroyed during its async load.
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.wanted && entry.owner != null) continue;
            foreach (var scene in entry.scenes)
            {
                if (!scene.isLoaded) continue;
                var unload = SceneManager.UnloadSceneAsync(scene);
                if (unload != null) yield return unload;
            }
            if (entry.probes != null) Destroy(entry.probes);
            entries.RemoveAt(i);
            if (entry.scenes.Count > 0) RebindAndTetrahedralize();
        }
        while (pending.Count > 0)
        {
            var entry = pending.Dequeue();
            if (!entry.wanted || entry.owner == null) continue;
            var owner = entry.owner;
            if (Quaternion.Angle(owner.transform.rotation, Quaternion.identity) > .01f ||
                Vector3.Distance(owner.transform.lossyScale, Vector3.one) > .001f)
            {
                Fail(owner, "Baked block lighting supports translation only, with unit scale.");
                continue;
            }
            var paths = string.IsNullOrEmpty(owner.supplementalLightingScene)
                ? new[] { owner.lightingScene } : new[] { owner.lightingScene, owner.supplementalLightingScene };
            foreach (string path in paths)
            {
                if (!entry.wanted || owner == null) break;
                if (!Application.CanStreamedLevelBeLoaded(path))
                {
                    Fail(owner, "Lighting companion scene missing from build: " + path);
                    break;
                }
                var before = new HashSet<int>();
                for (int i = 0; i < SceneManager.sceneCount; i++) before.Add(SceneManager.GetSceneAt(i).handle);
                var load = SceneManager.LoadSceneAsync(path, LoadSceneMode.Additive);
                if (load == null) { Fail(owner, "Could not load lighting companion scene: " + path); break; }
                yield return load;
                Scene loaded = default;
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    var scene = SceneManager.GetSceneAt(i);
                    if (!before.Contains(scene.handle) && scene.path == path) { loaded = scene; break; }
                }
                if (!loaded.IsValid())
                {
                    if (owner != null && entry.wanted) Fail(owner, "Loaded lighting scene could not be identified: " + path);
                    break;
                }
                entry.scenes.Add(loaded);
            }
            // The next pass releases scenes if the owner disappeared during an async load.
            if (!entry.wanted || owner == null || entry.scenes.Count != paths.Length) continue;
            try
            {
                // The primary companion owns the block probes. The supplemental scene only supplies lightmaps.
                var probes = LightProbes.GetInstantiatedLightProbesForScene(entry.scenes[0]);
                entry.probes = probes;
                if (probes == null || probes.countSelf == 0) throw new InvalidOperationException("No baked probes in companion scene.");
                entry.localPositions = probes.GetPositionsSelf();
                PlaceProbes(entry);
                RebindAndTetrahedralize();
                owner.Ready = string.IsNullOrEmpty(owner.Failure);
            }
            catch (Exception ex) { Fail(owner, ex.Message); }
        }
        working = false;
    }
    static void PlaceProbes(Entry entry)
    {
        var owner = entry.owner;
        if (Quaternion.Angle(owner.transform.rotation, Quaternion.identity) > .01f ||
            Vector3.Distance(owner.transform.lossyScale, Vector3.one) > .001f)
            throw new InvalidOperationException("Baked block lighting supports translation only, with unit scale.");
        var positions = new Vector3[entry.localPositions.Length];
        for (int i = 0; i < positions.Length; i++) positions[i] = entry.localPositions[i] + owner.transform.position;
        if (!entry.probes.SetPositionsSelf(positions, true)) throw new InvalidOperationException("Probe translation failed.");
        owner.LoadedPosition = owner.transform.position;
    }
    void RebindAndTetrahedralize()
    {
        var maps = LightmapSettings.lightmaps;
        foreach (var entry in entries)
        {
            if (!entry.wanted || entry.owner == null || entry.probes == null || !entry.scenes[0].isLoaded) continue;
            foreach (var binding in entry.owner.bindings)
            {
                if (binding.renderer == null) { Fail(entry.owner, "Baked renderer reference is missing; re-export lighting."); break; }
                int index = Array.FindIndex(maps, map => map.lightmapColor == binding.color);
                if (index < 0) { Fail(entry.owner, "Baked lightmap missing from companion scene."); break; }
                binding.renderer.lightmapIndex = index;
                binding.renderer.lightmapScaleOffset = binding.scaleOffset;
            }
        }
        // LightmapSettings.lightProbes can be null when the active gameplay scene has no
        // bake of its own, even though additive scenes contain valid native probe data.
        LightProbes.Tetrahedralize();
    }
    static void Fail(RailBakedBlockLighting owner, string message)
    {
        owner.Ready = false;
        owner.Failure = message;
        if (DuckzLog.AllowError(out var notice, owner.lightingScene))
            notice.Write("Block lighting: " + message, owner);
    }
}
