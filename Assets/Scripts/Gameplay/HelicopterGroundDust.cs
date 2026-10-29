using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Local-only rotor wash. Place the prefab under a landing point to override its surface.</summary>
public sealed class HelicopterGroundDust : MonoBehaviour
{
    [Header("Height response")]
    [Min(0.01f)] public float activationHeight = 10f;
    [Min(0.1f)] public float rampExponent = 1.5f;

    [Header("Surface appearance")]
    public Mesh particleMesh;
    public Material particleMaterial;
    public Color darkColor = new Color(0.35f, 0.27f, 0.16f);
    public Color lightColor = new Color(0.65f, 0.53f, 0.35f);
    [Tooltip("Offset from the landing point to the actual ground surface.")]
    public Vector3 groundOffset = new Vector3(0f, 0.08f, 0f);
    public Vector2 particleSize = new Vector2(0.04f, 0.16f);

    [Header("Particle budget and motion")]
    [Range(1, 128)] public int maxParticles = 80;
    [Tooltip("Particles emitted per second at ground level. Live particle count is still limited by Max Particles.")]
    [Range(0f, 500f)] public float fullEmissionRate = 55f;
    public Vector2 lifetime = new Vector2(0.55f, 1.2f);
    public Vector2 spawnRadius = new Vector2(0.7f, 2.2f);
    public Vector2 outwardSpeed = new Vector2(2f, 5f);
    [Min(0f)] public float liftSpeed = 0.45f;

    ParticleSystem particles;
    float intensity;
    float emissionRemainder;

#if UNITY_EDITOR
    public bool PreviewActive { get; private set; }
    public float PreviewHeight { get; private set; }
    public int PreviewParticleCount => particles != null ? particles.particleCount : 0;

    public void PreviewAtHeight(float height)
    {
        if (!Application.isPlaying || !isActiveAndEnabled || particles == null) return;
        PreviewHeight = Mathf.Max(0f, height);
        PreviewActive = true;
    }

    public void EndPreview()
    {
        PreviewActive = false;
        emissionRemainder = 0f;
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    public void RefreshPreviewSettings()
    {
        if (Application.isPlaying && particles != null)
            ConfigureParticles();
    }
#endif

    void Awake()
    {
        // Build once, from prefab settings. No particle GameObjects, colliders, or per-frame arrays.
        var particleObject = new GameObject("Ground dust particles");
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(transform, false);
        particles = particleObject.AddComponent<ParticleSystem>();
        ConfigureParticles();
    }

    void ConfigureParticles()
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Clamp(maxParticles, 1, 128);
        main.startSpeed = 0f;
        main.gravityModifier = 0f;
        main.startRotation3D = true;
        main.cullingMode = ParticleSystemCullingMode.Pause;
        var emission = particles.emission;
        emission.enabled = false;
        var shape = particles.shape;
        shape.enabled = false;
        var size = particles.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f,
            new AnimationCurve(new Keyframe(0f, 0.3f), new Keyframe(0.15f, 1f), new Keyframe(1f, 0f)));
        var velocity = particles.limitVelocityOverLifetime;
        velocity.enabled = true;
        velocity.limit = 2f;
        velocity.dampen = 0.15f;
        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        // Keep cube orientation fixed in the world — do not billboard toward the camera.
        renderer.alignment = ParticleSystemRenderSpace.World;
        // This tiny mesh budget uses ordinary particle batching, not a procedural instancing shader.
        renderer.enableGPUInstancing = false;
        renderer.mesh = particleMesh;
        renderer.sharedMaterial = particleMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }

    public void SetHeight(float height)
    {
        intensity = IntensityAtHeight(height);
    }

    float IntensityAtHeight(float height)
    {
        float proximity = Mathf.Clamp01(1f - Mathf.Max(0f, height) / Mathf.Max(0.01f, activationHeight));
        return Mathf.Pow(proximity, Mathf.Max(0.1f, rampExponent));
    }

    public void StopDust(bool clear = false)
    {
        intensity = 0f;
#if UNITY_EDITOR
        // Keep manager updates from interrupting the manual preview.
        if (PreviewActive && !clear) return;
#endif
        emissionRemainder = 0f;
        if (particles != null)
            particles.Stop(true, clear ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
    }

    void Update()
    {
        float emissionIntensity = intensity;
#if UNITY_EDITOR
        if (PreviewActive)
            emissionIntensity = IntensityAtHeight(PreviewHeight);
#endif
        if (emissionIntensity <= 0f || particleMesh == null || particleMaterial == null)
        {
            emissionRemainder = 0f;
            if (particles.isPlaying)
                particles.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            return;
        }

        if (!particles.isPlaying)
            particles.Play();

        // Clamp catch-up after a hitch, and never emit more than the configured live budget.
        emissionRemainder += Mathf.Clamp(fullEmissionRate, 0f, 500f) * emissionIntensity * Mathf.Min(Time.deltaTime, 0.1f);
        int requested = Mathf.FloorToInt(emissionRemainder);
        emissionRemainder -= requested;
        int count = Mathf.Min(requested, particles.main.maxParticles - particles.particleCount);
        Vector3 origin = transform.TransformPoint(groundOffset);
        for (int i = 0; i < count; i++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            Vector3 direction = transform.rotation * new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            var emit = new ParticleSystem.EmitParams
            {
                position = origin + direction * Sample(spawnRadius),
                velocity = direction * Sample(outwardSpeed) * Mathf.Lerp(0.35f, 1f, emissionIntensity)
                    + transform.up * Random.Range(0f, Mathf.Max(0f, liftSpeed)),
                startLifetime = Mathf.Max(0.05f, Sample(lifetime)),
                startSize = Mathf.Max(0.001f, Sample(particleSize)),
                startColor = Color.Lerp(darkColor, lightColor, Random.value),
                rotation3D = new Vector3(Random.value, Random.value, Random.value) * 360f
            };
            particles.Emit(emit, 1);
        }
    }

    static float Sample(Vector2 range) => Random.Range(Mathf.Max(0f, range.x), Mathf.Max(range.x, range.y));

    void OnDisable()
    {
#if UNITY_EDITOR
        PreviewActive = false;
#endif
        StopDust(true);
    }
}
