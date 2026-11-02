using System;
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
    public Vector3 LoadedPosition { get; internal set; }
    public bool Ready { get => ready && (transform.position - LoadedPosition).sqrMagnitude < .000001f; internal set => ready = value; }
    public string Failure { get; internal set; }
    void OnEnable()
    {
        if (Application.isPlaying && !string.IsNullOrEmpty(lightingScene)) RailBlockLightingLoader.Add(this);
    }
    void OnDisable()
    {
        if (Application.isPlaying) RailBlockLightingLoader.Remove(this);
        Ready = false;
    }
}
