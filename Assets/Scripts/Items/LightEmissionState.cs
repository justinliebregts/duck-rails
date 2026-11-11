using System.Collections.Generic;
using UnityEngine;

/// <summary>Prepares material variants once; only changes renderer materials on light transitions.</summary>
public sealed class LightEmissionState
{
    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    sealed class Entry
    {
        public Renderer renderer;
        public Material[] on;
        public Material[] off;
    }

    readonly List<Entry> entries = new List<Entry>();
    readonly Dictionary<Material, Material> variants = new Dictionary<Material, Material>();
    bool initialized;
    bool current;

    // Live states, so ExpeditionRenderWarmup can prepare both the lit and unlit materials.
    static readonly List<LightEmissionState> live = new List<LightEmissionState>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => live.Clear();

    /// <summary>Adds each renderer with both its light-on and light-off material sets.</summary>
    internal static void AppendStates(List<KeyValuePair<Renderer, Material[]>> into)
    {
        foreach (LightEmissionState state in live)
            foreach (Entry entry in state.entries)
                if (entry.renderer != null)
                {
                    into.Add(new KeyValuePair<Renderer, Material[]>(entry.renderer, entry.on));
                    into.Add(new KeyValuePair<Renderer, Material[]>(entry.renderer, entry.off));
                }
    }

    public LightEmissionState(Component owner)
    {
        live.Add(this);
        foreach (Renderer renderer in owner.GetComponentsInChildren<Renderer>(true))
        {
            Material[] original = renderer.sharedMaterials;
            Material[] off = null;
            for (int i = 0; i < original.Length; i++)
            {
                Material material = original[i];
                if (material == null || !material.IsKeywordEnabled("_EMISSION") ||
                    !material.HasProperty(EmissionColor))
                    continue;

                if (off == null)
                    off = (Material[])original.Clone();
                if (!variants.TryGetValue(material, out Material variant))
                {
                    variant = new Material(material) { name = material.name + " (light off)" };
                    variant.SetColor(EmissionColor, Color.black);
                    variant.DisableKeyword("_EMISSION");
                    variants.Add(material, variant);
                }
                off[i] = variant;
            }
            if (off != null)
                entries.Add(new Entry { renderer = renderer, on = original, off = off });
        }
    }

    public void Apply(bool enabled)
    {
        if (initialized && current == enabled)
            return;
        initialized = true;
        current = enabled;
        foreach (Entry entry in entries)
            if (entry.renderer != null)
                entry.renderer.sharedMaterials = enabled ? entry.on : entry.off;
    }

    public void Dispose()
    {
        live.Remove(this);
        // Restore references before destroying the runtime-only variants.
        foreach (Entry entry in entries)
            if (entry.renderer != null)
                entry.renderer.sharedMaterials = entry.on;
        foreach (Material variant in variants.Values)
            Object.Destroy(variant);
        entries.Clear();
        variants.Clear();
    }
}
