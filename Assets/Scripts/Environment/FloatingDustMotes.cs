using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Local-only Upside-Down style dust. Parent under a MainCamera (or instantiate via
/// <see cref="Attach"/>). ParticleSystem is built at runtime from the fields below.
/// </summary>
public sealed class FloatingDustMotes : MonoBehaviour
{
    const int MaxParticleCap = 200;

    [Header("Appearance")]
    public Mesh particleMesh;
    public Material particleMaterial;
    public Color darkColor = new Color(0.02f, 0.02f, 0.02f, 1f);
    public Color lightColor = new Color(0.08f, 0.08f, 0.08f, 1f);
    public Vector2 particleSize = new Vector2(0.01f, 0.04f);

    [Header("Volume")]
    [Tooltip("Full box size in meters (local space). Centered on the camera, then shifted by Box Offset.")]
    public Vector3 boxSize = new Vector3(12f, 8f, 12f);
    [Tooltip("Local-space shift of the box center from the camera. +Y up, +Z forward. Example: size (20,8,20) with offset (0,2,5) → 6m above / 2m below, 15m ahead / 5m behind.")]
    public Vector3 boxOffset = new Vector3(0f, 2f, 5f);
    [Tooltip("Extra meters to push the box along horizontal move direction per m/s of speed. Stops you from running into empty air ahead of the spawn volume.")]
    [Min(0f)] public float runLeadSeconds = 0.75f;
    [Tooltip("Caps how far velocity can shove the box (meters).")]
    [Min(0f)] public float maxRunLead = 12f;

    [Header("Particle budget and motion")]
    [Range(1, MaxParticleCap)] public int maxParticles = 120;
    [Tooltip("Particles emitted per second. Live count is still limited by Max Particles.")]
    [Range(0f, 100f)] public float emissionRate = 10f;
    public Vector2 lifetime = new Vector2(8f, 16f);
    [Tooltip("Random world drift speed in m/s.")]
    public Vector2 driftSpeed = new Vector2(0.02f, 0.08f);

    ParticleSystem particles;
    Vector3 lastWorldPosition;
    bool hasLastPosition;

#if UNITY_EDITOR
    public int LiveParticleCount => particles != null ? particles.particleCount : 0;
#endif

    /// <summary>Parents a prefab instance under <paramref name="camera"/> at local origin.</summary>
    public static FloatingDustMotes Attach(Transform camera, FloatingDustMotes prefab)
    {
        if (camera == null || prefab == null)
            return null;

        FloatingDustMotes instance = Instantiate(prefab, camera);
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        instance.name = "FloatingDustMotes";
        return instance;
    }

    public void ApplySettings()
    {
        if (!Application.isPlaying || particles == null)
            return;
        ConfigureParticles();
        if (isActiveAndEnabled && particleMesh != null && particleMaterial != null)
            particles.Play();
    }

    void Awake()
    {
        var particleObject = new GameObject("Dust mote particles");
        particleObject.layer = gameObject.layer;
        particleObject.transform.SetParent(transform, false);
        particles = particleObject.AddComponent<ParticleSystem>();
        ConfigureParticles();
    }

    void OnEnable()
    {
        hasLastPosition = false;
        if (particles != null && particleMesh != null && particleMaterial != null)
            particles.Play();
    }

    void OnDisable()
    {
        hasLastPosition = false;
        if (particles != null)
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    void LateUpdate()
    {
        if (particles == null)
            return;

        // Stay level with the ground: follow camera yaw only (no pitch/roll).
        Transform cam = transform.parent != null ? transform.parent : transform;
        transform.rotation = Quaternion.Euler(0f, cam.eulerAngles.y, 0f);

        Vector3 lead = Vector3.zero;
        if (runLeadSeconds > 0f && hasLastPosition)
        {
            float dt = Time.deltaTime;
            if (dt > 1e-5f)
            {
                // Horizontal world velocity → local offset so the volume leads where you run.
                Vector3 worldVel = (transform.position - lastWorldPosition) / dt;
                Vector3 worldLead = new Vector3(worldVel.x, 0f, worldVel.z) * runLeadSeconds;
                lead = transform.InverseTransformVector(worldLead);
                float maxLead = Mathf.Max(0f, maxRunLead);
                if (maxLead > 0f && lead.sqrMagnitude > maxLead * maxLead)
                    lead = lead.normalized * maxLead;
            }
        }

        lastWorldPosition = transform.position;
        hasLastPosition = true;

        var shape = particles.shape;
        shape.position = boxOffset + lead;
    }

    void ConfigureParticles()
    {
        particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = particles.main;
        main.playOnAwake = false;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles = Mathf.Clamp(maxParticles, 1, MaxParticleCap);
        main.startLifetime = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.05f, lifetime.x),
            Mathf.Max(lifetime.x, lifetime.y));
        main.startSize = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0.001f, particleSize.x),
            Mathf.Max(particleSize.x, particleSize.y));
        main.startSpeed = new ParticleSystem.MinMaxCurve(
            Mathf.Max(0f, driftSpeed.x),
            Mathf.Max(driftSpeed.x, driftSpeed.y));
        main.startColor = new ParticleSystem.MinMaxGradient(darkColor, lightColor);
        // Script curves are radians; randomize each axis so cubes aren't axis-aligned.
        main.startRotation3D = true;
        main.startRotationX = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationY = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startRotationZ = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.gravityModifier = 0f;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.cullingMode = ParticleSystemCullingMode.Pause;

        var emission = particles.emission;
        emission.enabled = true;
        emission.rateOverTime = Mathf.Clamp(emissionRate, 0f, 100f);

        var shape = particles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            Mathf.Max(0.1f, boxSize.x),
            Mathf.Max(0.1f, boxSize.y),
            Mathf.Max(0.1f, boxSize.z));
        shape.position = boxOffset;
        shape.rotation = Vector3.zero;

        var velocity = particles.velocityOverLifetime;
        velocity.enabled = false;

        var noise = particles.noise;
        noise.enabled = false;

        var size = particles.sizeOverLifetime;
        size.enabled = true;
        // Scale in from 0, hold, then scale out so motes don't pop.
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0f),
            new Keyframe(0.12f, 1f),
            new Keyframe(0.85f, 1f),
            new Keyframe(1f, 0f)));

        var color = particles.colorOverLifetime;
        color.enabled = false;

        var collision = particles.collision;
        collision.enabled = false;

        var triggers = particles.trigger;
        triggers.enabled = false;

        var lights = particles.lights;
        lights.enabled = false;

        var trails = particles.trails;
        trails.enabled = false;

        var renderer = particles.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Mesh;
        // Keep cube orientation fixed in the world — do not billboard toward the camera.
        renderer.alignment = ParticleSystemRenderSpace.World;
        renderer.enableGPUInstancing = false;
        renderer.mesh = particleMesh;
        renderer.sharedMaterial = particleMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
    }
}
