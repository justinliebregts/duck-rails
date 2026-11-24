using Game.Audio;
using UnityEngine;

/// <summary>A local visual for a timestamped death event. Each peer owns its own copy.</summary>
public sealed class DeathFlare : MonoBehaviour
{
    static readonly System.Collections.Generic.HashSet<DeathFlare> active = new(64);
    static int lightSelectionFrame = -1;
    public bool IsBurning => isActiveAndEnabled && initialized
        && Time.unscaledTimeAsDouble >= started && Time.unscaledTimeAsDouble < started + lifetime;
    /// <summary>Seconds this flare burns. Death flares use <see cref="Lifetime"/>; shot flares are shorter.</summary>
    public float BurnSeconds => lifetime;

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

    internal static void CollectThreats(System.Collections.Generic.List<Vector3> positions, Vector3 origin, float range)
    {
        positions.Clear();
        foreach (var flare in active)
        {
            if (!flare || !flare.IsBurning) continue;
            Vector3 position = flare.transform.position;
            if ((position - origin).sqrMagnitude <= range * range) positions.Add(position);
        }
    }

    public static bool AllowsMovement(Vector3 from, Vector3 to, float radius, bool escaping = false)
    {
        float radiusSq = radius * radius;
        foreach (var flare in active)
        {
            if (flare == null || !flare.IsBurning) continue;
            if (!AllowsStep(from, to, flare.transform.position, radiusSq, escaping)) return false;
        }
        return true;
    }
    internal static bool AllowsStep(Vector3 from, Vector3 to, Vector3 flare, float radiusSq, bool escaping)
    {
        Vector3 offset = from - flare, step = to - from;
        if (offset.sqrMagnitude < radiusSq)
            return escaping || Vector3.Dot(offset, step) >= -.00001f;
        float t = step.sqrMagnitude > 0 ? Mathf.Clamp01(-Vector3.Dot(offset, step) / step.sqrMagnitude) : 0;
        return (offset + step * t).sqrMagnitude >= radiusSq;
    }
    public const float Lifetime = 40f;
    public const float DeathLaunchDelay = 0f;
    [SerializeField, Min(0.1f)] float lifetime = Lifetime;
    [Header("Shot (flare gun)")]
    [SerializeField, Min(0.1f)] float shotSpeed = 32f;
    [SerializeField, Min(0.1f), Tooltip("Straight-line travel before the flare stops and floats down.")]
    float shotRange = 25f;
    [SerializeField, Range(0f, 1f), Tooltip("Speed kept after bouncing off a surface or enemy.")]
    float bounceKeep = 0.3f;
    [SerializeField, Min(0f)] float fallGravity = 9.81f;
    [SerializeField, Min(0f), Tooltip("Air drag after a bounce, so the flare looks like it lost its momentum.")]
    float fallDrag = 1.5f;
    [SerializeField, Tooltip("Burst played where a shot flare hits something. Simulate in world space; it is emitted manually.")]
    ParticleSystem impactSparks;
    [SerializeField, Min(0)] int impactSparkCount = 14;
    [SerializeField] LayerMask shotMask = (1 << 0) | (1 << 6); // Default (train) and Environment.
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
    internal string RoomEventKey { get; set; }

    // Shot flares advance in fixed steps from their launch so every peer traces the same
    // environment bounces. Only the shooter tests enemies and reports those deflections.
    const float ShotStep = 1f / 120f;
    enum ShotPhase { None, Flight, Fall, Float, Rest }
    ShotPhase shotPhase;
    Vector3 shotPosition, shotVelocity;
    float shotAge, shotTravel;
    int shotBounces;
    /// <summary>Set by the shooter only: an enemy hit at (position, deflected velocity, age).</summary>
    internal System.Action<DeathFlare, Game.AI.Enemy, Vector3, Vector3, float> ShooterEnemyHit;
    internal int ShotKey { get; set; }
    public bool IsShot => shotPhase != ShotPhase.None;
    /// <summary>Diagnostics: launch height after ceiling clamping, or -1 before launch.</summary>
    public float LaunchRise => launched ? apex.y - origin.y : -1f;
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
        RoomEventKey = null;
        shotPhase = ShotPhase.None; shotAge = shotTravel = 0f; shotBounces = 0;
        ShooterEnemyHit = null; ShotKey = 0;
        previousAge = 0f;
        launchAudio.Stop();
        if (flareLight != null) { flareLight.intensity = authoredIntensity; flareLight.enabled = false; }
        foreach (var ps in waitingParticles) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    // Launch immediately; remote clients catch up from the original downing timestamp.
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

    /// <summary>Fires a flare from a gun. A positive age catches up a remote shot along the same path.</summary>
    public static DeathFlare SpawnShot(DeathFlare prefab, Vector3 origin, Vector3 direction, float age = 0f,
        System.Action<DeathFlare, Game.AI.Enemy, Vector3, Vector3, float> shooterEnemyHit = null)
    {
        if (prefab == null || age >= prefab.lifetime || direction.sqrMagnitude < 1e-6f) return null;
        var pool = DeathEffectPool.Get();
        var effect = pool.Rent(prefab.gameObject, origin, Quaternion.identity);
        DeathFlare flare = effect.GetComponent<DeathFlare>();
        flare.pool = pool;
        flare.PrepareReuse();
        flare.shotPhase = ShotPhase.Flight;
        flare.shotPosition = origin;
        flare.shotVelocity = direction.normalized * flare.shotSpeed;
        flare.ShooterEnemyHit = shooterEnemyHit;
        pool.Activate(effect, playParticles: false);
        flare.Initialize(Mathf.Max(0f, age));
        return flare;
    }

    /// <summary>Applies the shooter's enemy deflection on a peer, then catches up to the present.</summary>
    internal void ApplyRemoteDeflect(Vector3 position, Vector3 velocity, float age)
    {
        if (!IsShot || !initialized) return;
        shotPosition = position; shotVelocity = velocity; shotAge = age;
        shotPhase = ShotPhase.Fall; shotBounces = Mathf.Max(1, shotBounces);
        EmitSparks(position, -velocity);
        float now = (float)(Time.unscaledTimeAsDouble - started);
        AdvanceShot(now);
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
        lightIntensity = flareLight != null ? flareLight.intensity : 0f;
        if (IsShot)
        {
            AdvanceShot(age);
            previousAge = age;
            if (age < 0.5f && launchSound != null && AudioManager.instance != null)
                launchAudio = AudioManager.instance.PlayAttached(launchSound, transform, this);
            return;
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
        if (age >= lifetime)
        {
            if (pool != null) pool.Release(gameObject);
            else { gameObject.SetActive(false); Destroy(gameObject); }
            return;
        }
        if (!launched) Launch(age);
        if (IsShot) AdvanceShot(age);
        else
        {
            if (age > launchDuration && previousAge < launchDuration) Advance(launchDuration);
            Advance(age);
        }
        previousAge = age;
        if (flareLight != null)
            flareLight.intensity = lightIntensity * (0.94f + 0.06f * Mathf.Sin(age * 23f))
                * Mathf.Clamp01(lifetime - age);
    }

    void AdvanceShot(float age)
    {
        int guard = 0;
        while (shotAge + ShotStep <= age && shotPhase != ShotPhase.Rest && guard++ < 4000)
            StepShot(ShotStep);
        transform.position = shotPosition;
    }

    void StepShot(float dt)
    {
        shotAge += dt;
        if (shotPhase == ShotPhase.Float)
        {
            Vector3 down = Vector3.down * (descentSpeed * dt);
            if (Physics.SphereCast(shotPosition, surfaceRadius, Vector3.down, out RaycastHit floor,
                down.magnitude + 0.01f, shotMask, QueryTriggerInteraction.Ignore))
            {
                shotPosition += Vector3.down * Mathf.Max(0f, floor.distance - 0.01f);
                shotPhase = ShotPhase.Rest;
            }
            else shotPosition += down;
            return;
        }
        if (shotPhase == ShotPhase.Fall)
        {
            shotVelocity += Vector3.down * (fallGravity * dt);
            shotVelocity *= Mathf.Max(0f, 1f - fallDrag * dt);
        }
        Vector3 delta = shotVelocity * dt;
        float distance = delta.magnitude;
        if (distance <= 0f) { shotPhase = ShotPhase.Rest; return; }
        Vector3 dir = delta / distance;

        if (shotPhase == ShotPhase.Flight && ShooterEnemyHit != null && TryEnemyHit(shotPosition, shotPosition + delta)) return;

        if (Physics.SphereCast(shotPosition, surfaceRadius, dir, out RaycastHit hit, distance + 0.01f,
            shotMask, QueryTriggerInteraction.Ignore))
        {
            shotPosition += dir * Mathf.Max(0f, hit.distance - 0.01f);
            Bounce(hit.normal);
            return;
        }
        shotPosition += delta;
        if (shotPhase == ShotPhase.Flight)
        {
            shotTravel += distance;
            if (shotTravel >= shotRange) shotPhase = ShotPhase.Float;
        }
    }

    void Bounce(Vector3 normal)
    {
        float speed = shotVelocity.magnitude;
        if (speed > 2f) EmitSparks(shotPosition, normal);
        shotBounces++;
        // Settle on anything floor-like once the flare is slow, or after a few ricochets.
        if ((normal.y > 0.6f && speed < 4f) || shotBounces > 4) { shotPhase = ShotPhase.Rest; return; }
        shotVelocity = Vector3.Reflect(shotVelocity, normal) * bounceKeep;
        shotPhase = ShotPhase.Fall;
    }

    bool TryEnemyHit(Vector3 from, Vector3 to)
    {
        var enemies = Game.AI.Enemy.Active;
        if (enemies.Count == 0) return false;
        var sweep = new Bounds(from, Vector3.zero); sweep.Encapsulate(to); sweep.Expand(surfaceRadius * 2f);
        Game.AI.Enemy best = null; Game.AI.Enemy.SweepContact bestContact = default;
        float limit = 1f;
        foreach (var enemy in enemies)
        {
            if (enemy == null || enemy.IsDormant || !enemy.WithinSwingReach(sweep)) continue;
            enemy.PrepareSwingPose();
            if (!enemy.TrySwingContact(from, to, surfaceRadius, limit, out var contact)) continue;
            best = enemy; bestContact = contact; limit = contact.before;
        }
        if (best == null) return false;
        Vector3 incoming = shotVelocity;
        shotPosition = Vector3.Lerp(from, to, Mathf.Clamp01(bestContact.before));
        Vector3 normal = bestContact.normal.sqrMagnitude > 1e-6f ? bestContact.normal.normalized : -incoming.normalized;
        EmitSparks(bestContact.position, normal);
        shotVelocity = Vector3.Reflect(incoming, normal) * bounceKeep;
        shotPhase = ShotPhase.Fall; shotBounces++;
        ShooterEnemyHit?.Invoke(this, best, shotPosition, shotVelocity, shotAge);
        return true;
    }

    void EmitSparks(Vector3 point, Vector3 normal)
    {
        if (impactSparks == null || impactSparkCount <= 0) return;
        impactSparks.transform.SetPositionAndRotation(point,
            normal.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(normal) : Quaternion.identity);
        impactSparks.Emit(impactSparkCount);
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
