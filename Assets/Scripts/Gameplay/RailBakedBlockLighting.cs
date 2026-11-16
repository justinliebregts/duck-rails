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
    // Authored bake mode is editor-only; prefab bakingOutput does not retain it.
    // Export captures membership so player builds suppress exactly these fixtures.
    [SerializeField] Light[] bakedFixtures = Array.Empty<Light>();
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
        foreach (var light in bakedFixtures)
        {
            if (light == null || !light.enabled ||
                light.GetComponentInParent<RailBakedBlockLighting>(true) != this) continue;
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

#if UNITY_EDITOR
    public void CaptureBakedFixtures()
    {
        var fixtures = new List<Light>();
        foreach (var light in GetComponentsInChildren<Light>(true))
            if (light.lightmapBakeType == LightmapBakeType.Baked &&
                light.GetComponentInParent<RailBakedBlockLighting>(true) == this)
                fixtures.Add(light);
        bakedFixtures = fixtures.ToArray();
    }
#endif
}
