using Game.Audio;
using UnityEngine;

/// <summary>A local visual for a timestamped death event. Each peer owns its own copy.</summary>
public sealed class DeathFlare : MonoBehaviour
{
    static readonly System.Collections.Generic.HashSet<DeathFlare> active = new(64);
    static int lightSelectionFrame = -1;
    public bool IsBurning => isActiveAndEnabled && initialized
        && Time.unscaledTimeAsDouble >= started && Time.unscaledTimeAsDouble < started + Lifetime;

    void OnEnable()
    {
        active.Add(this);
        // Selection owns light visibility, including during the launch delay.
        if (flareLight != null) flareLight.enabled = false;
    }

    public static bool TryFindNear(Vector3 point, float radius, out Vector3 position)
    {
        var flare = FindNear(point, radius);
        position = flare != null ? flare.transform.position : default;
        return flare != null;
    }

    public static DeathFlare FindNear(Vector3 point, float radius)
    {
        float best = radius * radius;
        DeathFlare nearest = null;
        foreach (var flare in active)
        {
            if (flare == null || !flare.IsBurning) continue;
            float distance = (point - flare.transform.position).sqrMagnitude;
            if (distance >= best) continue;
            best = distance;
            nearest = flare;
        }
        return nearest;
    }

    public static bool AllowsMovement(Vector3 from, Vector3 to, float radius)
    {
        float radiusSq = radius * radius;
        foreach (var flare in active)
        {
            if (flare == null || !flare.IsBurning) continue;
            Vector3 offset = from - flare.transform.position;
            Vector3 step = to - from;
            if (offset.sqrMagnitude < radiusSq)
            {
                // Allow escape from an existing overlap, but never move closer.
                if (Vector3.Dot(offset, step) < -0.00001f) return false;
                continue;
            }
            float t = step.sqrMagnitude > 0f ? Mathf.Clamp01(-Vector3.Dot(offset, step) / step.sqrMagnitude) : 0f;
            if ((offset + step * t).sqrMagnitude < radiusSq) return false;
        }
        return true;
    }
    public const float Lifetime = 30f;
    public const float DeathLaunchDelay = 2f;
    [SerializeField, Min(0f)] float launchHeight = 24f;
    [SerializeField, Min(0.01f)] float launchDuration = 1.25f;
    [SerializeField, Min(0.01f)] float descentSpeed = 1.1f;
    [SerializeField, Min(0.01f)] float surfaceRadius = 0.12f;
    [SerializeField] LayerMask surfaceMask = 1 << 6; // Environment only.
    [SerializeField] SoundEvent launchSound;
    [SerializeField] Light flareLight;
    [SerializeField, Min(0f), Tooltip("Only the nearest burning flare within this distance of the local camera can illuminate the scene.")]
    float lightActivationDistance = 12f;
    Vector3 origin, apex;
    double started;
    float lightIntensity, previousAge;
    bool initialized, launched, landed;
    ParticleSystem[] waitingParticles;
    DeathEffectPool pool;
    bool prepared;
    float authoredIntensity;
    AudioHandle launchAudio;

    public static void Prewarm(DeathFlare prefab)
    {
        if (prefab != null) DeathEffectPool.Get().Prewarm(prefab.gameObject, 8);
    }

    internal void PrepareForPool() => PrepareReuse();

    void PrepareReuse()
    {
        if (!prepared)
        {
            waitingParticles = GetComponentsInChildren<ParticleSystem>(true);
            authoredIntensity = flareLight != null ? flareLight.intensity : 0f;
            prepared = true;
        }
        initialized = launched = landed = false;
        previousAge = 0f;
        launchAudio.Stop();
        if (flareLight != null) { flareLight.intensity = authoredIntensity; flareLight.enabled = false; }
        foreach (var ps in waitingParticles) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // Age is measured from death, so remote clients wait only the remaining delay.
    public static DeathFlare SpawnAfterDeath(DeathFlare prefab, Vector3 position, float deathAge = 0f)
        => Spawn(prefab, position, Mathf.Max(0f, deathAge) - DeathLaunchDelay);

    // A negative launch age schedules a future launch at this fixed world position.
    public static DeathFlare Spawn(DeathFlare prefab, Vector3 position, float age = 0f)
    {
        if (prefab == null || age >= Lifetime) return null;
        using var sample = DeathProfile.Flare.Auto();
        var pool = DeathEffectPool.Get();
        var effect = pool.Rent(prefab.gameObject, position, Quaternion.identity);
        DeathFlare flare = effect.GetComponent<DeathFlare>();
        flare.pool = pool;
        flare.PrepareReuse();
        pool.Activate(effect, playParticles: false);
        flare.Initialize(age);
        return flare;
    }

    void Start()
    {
        // Also works when the prefab is dropped directly into a test scene.
        if (!initialized) Initialize(0f);
    }

    void Initialize(float age)
    {
        if (!prepared) PrepareReuse();
        initialized = true;
        started = Time.unscaledTimeAsDouble - age;
        if (age < 0f)
        {
            foreach (var particles in waitingParticles)
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (flareLight != null) flareLight.enabled = false;
            return;
        }
        Launch(age);
    }

    void Launch(float age)
    {
        launched = true;
        if (waitingParticles != null)
        {
            foreach (var particles in waitingParticles) particles.Play(false);
            // Retain references for subsequent leases.
        }
        origin = transform.position;
        apex = origin + Vector3.up * launchHeight;
        if (Physics.SphereCast(origin, surfaceRadius, Vector3.up, out RaycastHit ceiling,
            launchHeight, surfaceMask, QueryTriggerInteraction.Ignore))
            apex = origin + Vector3.up * Mathf.Max(0f, ceiling.distance - 0.01f);
        lightIntensity = flareLight != null ? flareLight.intensity : 0f;
        // Keep ascent and descent separate when catching up a delayed network event.
        Advance(Mathf.Min(age, launchDuration));
        if (age > launchDuration) Advance(age);
        previousAge = age;
        if (age < launchDuration && launchSound != null && AudioManager.instance != null)
            launchAudio = AudioManager.instance.PlayAttached(launchSound, transform, this);
    }

    void Update()
    {
        if (!initialized) return;
        float age = (float)(Time.unscaledTimeAsDouble - started);
        if (age < 0f) return;
        if (age >= Lifetime)
        {
            if (pool != null) pool.Release(gameObject);
            else { gameObject.SetActive(false); Destroy(gameObject); }
            return;
        }
        if (!launched) Launch(age);
        if (age > launchDuration && previousAge < launchDuration) Advance(launchDuration);
        Advance(age);
        previousAge = age;
        if (flareLight != null)
            flareLight.intensity = lightIntensity * (0.94f + 0.06f * Mathf.Sin(age * 23f))
                * Mathf.Clamp01(Lifetime - age);
    }

    void Advance(float age)
    {
        if (landed) return;
        bool descending = age > launchDuration;
        float progress = Mathf.Clamp01(age / launchDuration);
        Vector3 target = descending
            ? apex + Vector3.down * ((age - launchDuration) * descentSpeed)
            : Vector3.Lerp(origin, apex, 1f - (1f - progress) * (1f - progress));
        Vector3 delta = target - transform.position;
        float distance = delta.magnitude;
        // Sweep even sub-millimetre steps at high frame rates. The skin keeps tiny casts reliable
        // and manual normalization avoids Vector3.normalized rounding small motion to zero.
        if (distance > 0f && Physics.SphereCast(transform.position, surfaceRadius,
            delta / distance, out RaycastHit hit, distance + 0.01f, surfaceMask, QueryTriggerInteraction.Ignore))
        {
            transform.position += delta / distance * Mathf.Clamp(hit.distance - 0.01f, 0f, distance);
            if (descending) landed = true;
            else apex = transform.position;
        }
        else transform.position = target;
    }

    void LateUpdate()
    {
        // All flares have moved in Update. Select once per frame across the local copies.
        if (lightSelectionFrame == Time.frameCount) return;
        lightSelectionFrame = Time.frameCount;
        var camera = Camera.main;
        SelectNearbyLight(camera != null ? (Vector3?)camera.transform.position : null);
    }

    static void SelectNearbyLight(Vector3? viewerPosition)
    {
        DeathFlare nearest = null;
        float nearestDistanceSq = float.PositiveInfinity;
        foreach (var flare in active)
        {
            if (flare == null || flare.flareLight == null) continue;
            if (!viewerPosition.HasValue || !flare.launched || !flare.IsBurning
                || !flare.flareLight.gameObject.activeInHierarchy) continue;
            float distanceSq = (flare.flareLight.transform.position - viewerPosition.Value).sqrMagnitude;
            if (distanceSq >= flare.lightActivationDistance * flare.lightActivationDistance
                || distanceSq >= nearestDistanceSq) continue;
            nearest = flare;
            nearestDistanceSq = distanceSq;
        }
        // Disable the previous light before enabling the winner; avoid toggling an unchanged winner.
        foreach (var flare in active)
            if (flare != null && flare != nearest && flare.flareLight != null && flare.flareLight.enabled)
                flare.flareLight.enabled = false;
        if (nearest != null && !nearest.flareLight.enabled) nearest.flareLight.enabled = true;
    }

    void OnDisable()
    {
        active.Remove(this);
        if (flareLight != null) flareLight.enabled = false;
        launchAudio.Stop();
    }
}
