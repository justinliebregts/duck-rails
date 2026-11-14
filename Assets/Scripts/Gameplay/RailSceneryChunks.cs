using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Offline-built scenery only. Original renderers remain the editable/bake source.
/// No runtime mesh combining; original colliders and gameplay objects are never disabled.</summary>
[DefaultExecutionOrder(-1000)]
public sealed class RailSceneryChunks : MonoBehaviour
{
    public GameObject chunkRoot;
    public Renderer[] sources = Array.Empty<Renderer>();
    public RailBakedBlockLighting.Binding[] sourceLighting = Array.Empty<RailBakedBlockLighting.Binding>();
    public RailBakedBlockLighting.Binding[] chunkLighting = Array.Empty<RailBakedBlockLighting.Binding>();
    [HideInInspector] public string sourceFingerprint;
    RailBakedBlockLighting owner;
    RailBakedBlockLighting.Binding[] originalBindings;

    void OnEnable() { if (Application.isPlaying) Activate(); }
    void OnDisable() => Restore();

    // Public for disposable EditMode validation; do not invoke on saved authoring objects.
    public bool Activate()
    {
        if (originalBindings != null) return true;
        owner = GetComponent<RailBakedBlockLighting>();
        if (!owner || !chunkRoot || sources.Length == 0 || sourceLighting.Length != sources.Length || chunkLighting.Length == 0) return false;
        var current = new Dictionary<Renderer, RailBakedBlockLighting.Binding>();
        foreach (var b in owner.bindings) if (b.renderer) current[b.renderer] = b;
        // A re-exported atlas must never silently reuse obsolete combined UVs.
        foreach (var b in sourceLighting)
            if (!b.renderer || !current.TryGetValue(b.renderer, out var found) ||
                found.color != b.color || found.scaleOffset != b.scaleOffset) return false;
        foreach (var r in sources) if (!r || !r.enabled || !r.gameObject.activeInHierarchy) return false;
        foreach (var b in chunkLighting) if (!b.renderer || !b.color) return false;
        originalBindings = owner.bindings;
        var combined = new RailBakedBlockLighting.Binding[originalBindings.Length + chunkLighting.Length];
        Array.Copy(originalBindings, combined, originalBindings.Length);
        Array.Copy(chunkLighting, 0, combined, originalBindings.Length, chunkLighting.Length);
        owner.bindings = combined;
        foreach (var r in sources) r.enabled = false;
        chunkRoot.SetActive(true);
        return true;
    }

    public void Restore()
    {
        if (originalBindings == null) return;
        if (chunkRoot) chunkRoot.SetActive(false);
        foreach (var r in sources) if (r) r.enabled = true;
        if (owner) owner.bindings = originalBindings;
        originalBindings = null;
    }
}
