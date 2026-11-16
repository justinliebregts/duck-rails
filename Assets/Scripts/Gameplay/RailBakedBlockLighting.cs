using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Prefab lightmap bindings; its additive companion scene owns baked probe data.
/// Placement is translation-only at unit scale; readiness waits for probe relocation.</summary>
public sealed class RailBakedBlockLighting : MonoBehaviour
{
    [Serializable]
    public struct Binding
    {
        public Renderer renderer;
        public Texture2D color;
        public Vector4 scaleOffset;
    }
    public string lightingScene;
    public Binding[] bindings = Array.Empty<Binding>();
    bool ready;
    readonly List<Light> suppressedBakedLights = new List<Light>();
    public Vector3 LoadedPosition { get; internal set; }
    public bool Ready { get => ready && (transform.position - LoadedPosition).sqrMagnitude < .000001f; internal set => ready = value; }
    public string Failure { get; internal set; }
    void OnEnable()
    {
        if (Application.isPlaying && !string.IsNullOrEmpty(lightingScene))
        {
            SuppressBakedLights();
            RailBlockLightingLoader.Add(this);
        }
    }
    void OnDisable()
    {
        if (Application.isPlaying) RailBlockLightingLoader.Remove(this);
        RestoreBakedLights();
        Ready = false;
    }

    // Native baked-light output belongs to the authoring scene, not the prefab.
    // The companion supplies illumination through maps/probes; its fixtures must
    // not also render direct light when instantiated in an unbaked gameplay scene.
    void SuppressBakedLights()
    {
        foreach (var light in GetComponentsInChildren<Light>(true))
        {
            if (!light.enabled || light.lightmapBakeType != LightmapBakeType.Baked ||
                light.GetComponentInParent<RailBakedBlockLighting>() != this) continue;
            suppressedBakedLights.Add(light);
            light.enabled = false;
        }
    }

    void RestoreBakedLights()
    {
        foreach (var light in suppressedBakedLights)
            if (light != null) light.enabled = true;
        suppressedBakedLights.Clear();
    }
}
