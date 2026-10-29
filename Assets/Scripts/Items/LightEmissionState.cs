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

    public LightEmissionState(Component owner)
    {
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
