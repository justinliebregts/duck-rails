using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

// Scene-owned local effects. Exhaustion grows the pool: never remove a live flare's protection.
public sealed class DeathEffectPool : MonoBehaviour
{
    sealed class Slot
    {
        public GameObject prefab, effect;
        public ParticleSystem[] particles;
        public bool active;
        public float expires;
    }
    static DeathEffectPool instance;
    readonly List<Slot> slots = new List<Slot>(64);
    Transform inactive;
    public int CreatedObjects { get; private set; }
    public int OverflowCreates { get; private set; }
    public int ActiveCount { get { int n = 0; foreach (var slot in slots) if (slot.active) n++; return n; } }
    public static DeathEffectPool Get()
    {
        if (instance == null) instance = new GameObject("Death effect pool").AddComponent<DeathEffectPool>();
        return instance;
    }
    void Awake()
    {
        var root = new GameObject("Inactive death effects");
        root.transform.SetParent(transform, false); root.SetActive(false); inactive = root.transform;
    }
    Slot Create(GameObject prefab)
    {
        var effect = Instantiate(prefab, inactive);
        effect.SetActive(false);
        var slot = new Slot { prefab = prefab, effect = effect, particles = effect.GetComponentsInChildren<ParticleSystem>(true) };
        foreach (var ps in slot.particles)
        {
            var main = ps.main; main.stopAction = ParticleSystemStopAction.None; main.playOnAwake = false;
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        effect.GetComponent<DeathFlare>()?.PrepareForPool();
        slots.Add(slot); CreatedObjects++; return slot;
    }
    public void Prewarm(GameObject prefab, int count)
    {
        if (prefab == null) return;
        int have = 0;
        foreach (var slot in slots) if (slot.prefab == prefab && slot.effect != null) have++;
        while (have++ < count) Create(prefab);
    }
    public GameObject Rent(GameObject prefab, Vector3 position, Quaternion rotation)
    {
        using var sample = DeathProfile.EffectRent.Auto();
        Slot selected = null;
        foreach (var slot in slots)
            if (!slot.active && slot.prefab == prefab && slot.effect != null) { selected = slot; break; }
        if (selected == null) { selected = Create(prefab); OverflowCreates++; }
        selected.active = true; selected.expires = float.PositiveInfinity;
        var effect = selected.effect;
        effect.transform.SetParent(transform, false);
        effect.transform.localScale = prefab.transform.localScale;
        effect.transform.SetPositionAndRotation(position, rotation);
        return effect;
    }
    public void Activate(GameObject effect, float lifetime = -1, bool playParticles = true)
    {
        foreach (var slot in slots)
        {
            if (slot.effect != effect) continue;
            slot.expires = lifetime < 0 ? float.PositiveInfinity : Time.time + lifetime;
            effect.SetActive(true);
            if (playParticles) foreach (var ps in slot.particles) ps.Play(false);
            return;
        }
    }
    public void Release(GameObject effect)
    {
        foreach (var slot in slots)
        {
            if (slot.effect != effect || !slot.active) continue;
            foreach (var ps in slot.particles) if (ps != null) ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            slot.active = false;
            if (effect != null) { effect.SetActive(false); effect.transform.SetParent(inactive, false); }
            return;
        }
    }
    void Update() => Tick(Time.time);
    public void Tick(float now)
    {
        foreach (var slot in slots)
            if (slot.active && (slot.effect == null || now >= slot.expires)) Release(slot.effect);
    }
    void OnDestroy() { if (instance == this) instance = null; }
}

public static class DeathProfile
{
    public static readonly ProfilerMarker Contact = new ProfilerMarker("Death.Contact");
    public static readonly ProfilerMarker Local = new ProfilerMarker("Death.LocalSetup");
    public static readonly ProfilerMarker Equipment = new ProfilerMarker("Death.EquipmentAndVisibility");
    public static readonly ProfilerMarker Ragdoll = new ProfilerMarker("Death.Ragdoll");
    public static readonly ProfilerMarker Flare = new ProfilerMarker("Death.Flare");
    public static readonly ProfilerMarker Bomber = new ProfilerMarker("Death.Bomber");
    public static readonly ProfilerMarker Listeners = new ProfilerMarker("Death.KillListeners");
    public static readonly ProfilerMarker EffectRent = new ProfilerMarker("Death.EffectRent");
    [System.Diagnostics.Conditional("DUCKZ_DIAGNOSTICS")]
    public static void Trace(string message, Object context = null) => DuckzLog.Log(message, context);
    public static long Contacts, Accepted, RagdollCreates;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void Reset() => Contacts = Accepted = RagdollCreates = 0;
}

