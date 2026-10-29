using Unity.Profiling;
using UnityEngine;

// Scene-owned: inactive effects stay under this root, and scene unload clears everything.
// Surfaces are followed rather than owning pooled objects, so destroying a surface cannot destroy the pool.
public sealed class BloodEffectPool : MonoBehaviour
{
    public const int BurstCapacity = 8;
    public const int DecalCapacity = 64;
    public const int QueueCapacity = 64;
    [Range(1, 16)] public int decalsPerFrame = 4;
    public int CollisionEvents { get; internal set; }
    public int DroppedImpacts { get; private set; }
    public int RecycledDecals { get; private set; }
    public int CreatedObjects { get; private set; }
    public int PendingImpacts => pendingCount;
    public int ActiveBursts => Count(bursts);
    public int ActiveDecals => Count(decals);
    static BloodEffectPool instance;
    static readonly ProfilerMarker SpawnMarker = new ProfilerMarker("Blood.Spawn");
    static readonly ProfilerMarker ActivateMarker = new ProfilerMarker("Blood.ActivateDecals");
    static readonly ProfilerMarker CleanupMarker = new ProfilerMarker("Blood.Cleanup");
    readonly Slot[] bursts = new Slot[BurstCapacity];
    readonly Slot[] decals = new Slot[DecalCapacity];
    readonly Impact[] pending = new Impact[QueueCapacity];
    int pendingHead, pendingCount;
    bool warmed;
    Transform inactiveRoot;

    sealed class Slot
    {
        public GameObject prefab, effect;
        public ParticleSystem[] systems;
        public BloodDecalSpawner spawner;
        public Transform surface;
        public Vector3 localPosition, localScale;
        public Quaternion localRotation;
        public float expiry, started;
        public bool active, surfaceHidden;
    }

    struct Impact
    {
        public GameObject prefab;
        public Transform surface;
        public Vector3 localPosition;
        public Quaternion localRotation;
        public float expiry;
    }

    public static BloodEffectPool Get()
    {
        if (instance == null)
            instance = new GameObject("Blood effect pool").AddComponent<BloodEffectPool>();
        return instance;
    }

    void Awake()
    {
        var root = new GameObject("Inactive blood effects");
        root.transform.SetParent(transform, false);
        root.SetActive(false); // Prevent play-on-awake while constructing pooled prefabs.
        inactiveRoot = root.transform;
    }

    public void Prewarm(GameObject prefab)
    {
        if (warmed || prefab == null) return;
        var spawner = prefab.GetComponent<BloodDecalSpawner>();
        if (spawner == null || spawner.decalPrefab == null) return;
        for (int i = 0; i < bursts.Length; i++) bursts[i] = Create(prefab);
        for (int i = 0; i < decals.Length; i++) decals[i] = Create(spawner.decalPrefab);
        warmed = true;
    }

    Slot Create(GameObject prefab)
    {
        var effect = Instantiate(prefab, inactiveRoot);
        effect.SetActive(false);
        var slot = new Slot { prefab = prefab, effect = effect,
            systems = effect.GetComponentsInChildren<ParticleSystem>(true),
            spawner = effect.GetComponent<BloodDecalSpawner>() };
        foreach (var ps in slot.systems)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.stopAction = ParticleSystemStopAction.None;
        }
        CreatedObjects++;
        return slot;
    }

    Slot Acquire(Slot[] slots, GameObject prefab, float now)
    {
        int selected = -1;
        for (int i = 0; i < slots.Length; i++)
            if (slots[i] != null && !slots[i].active && slots[i].prefab == prefab) { selected = i; break; }
        if (selected < 0)
            for (int i = 0; i < slots.Length; i++)
                if (slots[i] == null || !slots[i].active) { selected = i; break; }
        if (selected < 0)
        {
            selected = 0;
            for (int i = 1; i < slots.Length; i++)
                if (slots[i].started < slots[selected].started) selected = i;
            if (slots == decals) RecycledDecals++;
            Release(slots[selected]);
        }
        var slot = slots[selected];
        // Current player and Duck prefabs share one blood template. Alternate templates
        // are supported, but their first use can require replacing a pooled instance.
        if (slot == null || slot.prefab != prefab || slot.effect == null)
        {
            if (slot != null && slot.effect != null) Destroy(slot.effect);
            slots[selected] = slot = Create(prefab);
        }
        slot.active = true;
        slot.surfaceHidden = false;
        slot.started = now;
        slot.effect.transform.SetParent(transform, false);
        slot.effect.transform.localScale = prefab.transform.localScale;
        return slot;
    }

    static void Play(Slot slot)
    {
        if (slot.surfaceHidden) return;
        slot.effect.SetActive(true);
        foreach (var ps in slot.systems)
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            ps.Play(false);
        }
    }

    public void Spawn(GameObject prefab, Vector3 position, int seed, float now)
    {
        using var sample = SpawnMarker.Auto();
        Prewarm(prefab);
        var slot = Acquire(bursts, prefab, now);
        slot.expiry = now + 10f;
        slot.effect.transform.SetPositionAndRotation(position, Quaternion.identity);
        if (slot.spawner != null) slot.spawner.Begin(this, now + 20f);
        if (slot.systems.Length > 0)
        {
            slot.systems[0].useAutoRandomSeed = false;
            slot.systems[0].randomSeed = (uint)seed;
        }
        Play(slot);
    }

    public void Enqueue(GameObject prefab, Transform surface, Vector3 position, Quaternion rotation, float expiry)
    {
        if (prefab == null || surface == null) return;
        if (pendingCount == QueueCapacity) { DroppedImpacts++; return; }
        pending[(pendingHead + pendingCount) % QueueCapacity] = new Impact {
            prefab = prefab, surface = surface, localPosition = surface.InverseTransformPoint(position),
            localRotation = Quaternion.Inverse(surface.rotation) * rotation, expiry = expiry };
        pendingCount++;
    }

    void LateUpdate() => Tick(Time.time);

    public void Tick(float now)
    {
        using (CleanupMarker.Auto())
        {
            foreach (var slot in bursts)
                if (slot != null && slot.active && now >= slot.expiry) Release(slot);
            foreach (var slot in decals)
            {
                if (slot == null || !slot.active) continue;
                if (now >= slot.expiry || slot.surface == null) Release(slot);
                else Follow(slot);
            }
        }
        using (ActivateMarker.Auto())
        {
            int budget = Mathf.Clamp(decalsPerFrame, 1, 16);
            for (int i = 0; i < budget && pendingCount > 0; i++)
            {
                var hit = pending[pendingHead];
                pending[pendingHead] = default;
                pendingHead = (pendingHead + 1) % QueueCapacity;
                pendingCount--;
                if (hit.surface == null || hit.prefab == null || now >= hit.expiry) continue;
                var slot = Acquire(decals, hit.prefab, now);
                slot.surface = hit.surface;
                slot.localPosition = hit.localPosition;
                slot.localRotation = hit.localRotation;
                slot.localScale = Divide(hit.prefab.transform.localScale, hit.surface.lossyScale);
                slot.expiry = hit.expiry;
                Follow(slot);
                Play(slot);
            }
        }
    }

    static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(
        b.x != 0f ? a.x / b.x : 0f, b.y != 0f ? a.y / b.y : 0f, b.z != 0f ? a.z / b.z : 0f);

    static void Follow(Slot slot)
    {
        bool hidden = !slot.surface.gameObject.activeInHierarchy;
        bool resume = slot.surfaceHidden && !hidden;
        slot.surfaceHidden = hidden;
        if (hidden)
        {
            slot.effect.SetActive(false);
            return;
        }
        var target = slot.effect.transform;
        Vector3 position = slot.surface.TransformPoint(slot.localPosition);
        Quaternion rotation = slot.surface.rotation * slot.localRotation;
        Vector3 scale = Vector3.Scale(slot.localScale, slot.surface.lossyScale);
        if (target.position != position || target.rotation != rotation)
            target.SetPositionAndRotation(position, rotation);
        if (target.localScale != scale) target.localScale = scale;
        if (resume) Play(slot);
    }

    void Release(Slot slot)
    {
        slot.active = false;
        slot.surface = null;
        if (slot.effect == null) return;
        foreach (var ps in slot.systems) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        slot.effect.SetActive(false);
        slot.effect.transform.SetParent(inactiveRoot, false);
    }

    static int Count(Slot[] slots)
    {
        int count = 0;
        foreach (var slot in slots) if (slot != null && slot.active) count++;
        return count;
    }

    void OnDestroy() { if (instance == this) instance = null; }
}

