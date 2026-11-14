using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Edit-mode preview for RailFurnaceModule's debug override. Tick "Debug Override" and drag "Debug Level" on the
/// Furnace (prefab mode or a scene) to watch the idle fire at that size without Play Mode.
/// The preview is a hidden, unsaved clone of the Idle fire child, so authored particle values are never modified.
/// </summary>
[InitializeOnLoad]
public static class RailFurnaceFirePreview
{
    sealed class Preview { public GameObject clone; public ParticleSystem[] systems; public float[] baseRates; public float level = 1; }
    static readonly Dictionary<RailFurnaceModule, Preview> previews = new Dictionary<RailFurnaceModule, Preview>();
    static double last;

    static RailFurnaceFirePreview()
    {
        EditorApplication.update += Tick;
        AssemblyReloadEvents.beforeAssemblyReload += ClearAll;
        EditorApplication.playModeStateChanged += _ => ClearAll();
    }

    static void ClearAll()
    {
        foreach (var p in previews.Values) if (p.clone != null) Object.DestroyImmediate(p.clone);
        previews.Clear();
    }

    public static void Tick()
    {
        double now = EditorApplication.timeSinceStartup;
        float dt = Mathf.Min((float)(now - last), .1f);
        last = now;
        if (Application.isPlaying) { if (previews.Count > 0) ClearAll(); return; }

        var active = new HashSet<RailFurnaceModule>();
        var modules = new List<RailFurnaceModule>(Object.FindObjectsByType<RailFurnaceModule>(FindObjectsSortMode.None));
        var stage = UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();
        if (stage != null)
            foreach (var m in stage.prefabContentsRoot.GetComponentsInChildren<RailFurnaceModule>())
                if (!modules.Contains(m)) modules.Add(m);
        foreach (var module in modules)
        {
            if (!module.debugOverride || module.idleFire == null) continue;
            active.Add(module);
            if (!previews.TryGetValue(module, out var p) || p.clone == null) previews[module] = p = Build(module);
            Update(module, p, dt);
        }
        var stale = new List<RailFurnaceModule>();
        foreach (var kv in previews) if (kv.Key == null || !active.Contains(kv.Key)) stale.Add(kv.Key);
        foreach (var key in stale)
        {
            if (previews[key].clone != null) Object.DestroyImmediate(previews[key].clone);
            previews.Remove(key);
        }
        if (previews.Count > 0) SceneView.RepaintAll();
    }

    static Preview Build(RailFurnaceModule module)
    {
        var clone = Object.Instantiate(module.idleFire);
        clone.hideFlags = HideFlags.HideAndDontSave;
        foreach (var t in clone.GetComponentsInChildren<Transform>(true)) t.gameObject.hideFlags = HideFlags.HideAndDontSave;
        if (module.gameObject.scene.IsValid()) SceneManager.MoveGameObjectToScene(clone, module.gameObject.scene);
        clone.SetActive(true);
        var p = new Preview { clone = clone, systems = clone.GetComponentsInChildren<ParticleSystem>() };
        p.baseRates = new float[p.systems.Length];
        for (int i = 0; i < p.systems.Length; i++)
        {
            p.baseRates[i] = p.systems[i].emission.rateOverTimeMultiplier;
            var main = p.systems[i].main; main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            p.systems[i].Clear(true);
        }
        return p;
    }

    static void Update(RailFurnaceModule module, Preview p, float dt)
    {
        // Follow the real child so moving/scaling it in the Scene view updates the preview live.
        var src = module.idleFire.transform;
        p.clone.transform.SetPositionAndRotation(src.position, src.rotation);
        p.clone.transform.localScale = src.lossyScale;
        p.level = Mathf.MoveTowards(p.level, module.debugLevel, dt * Mathf.Max(module.fadeSpeed, .01f));
        for (int i = 0; i < p.systems.Length; i++)
        {
            var emission = p.systems[i].emission;
            emission.rateOverTimeMultiplier = p.baseRates[i] * p.level;
            p.systems[i].Simulate(dt, false, false, true);
        }
    }
}
