using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;

public class BloodDecalSpawner : MonoBehaviour
{
    public GameObject decalPrefab;
    ParticleSystem particles;
    readonly List<ParticleCollisionEvent> collisions = new List<ParticleCollisionEvent>(128);
    BloodEffectPool pool;
    float decalExpiry;
    static readonly ProfilerMarker CollisionMarker = new ProfilerMarker("Blood.Collisions");

    void Awake() => particles = GetComponent<ParticleSystem>();

    public static void Prewarm(GameObject prefab)
    {
        if (prefab != null) BloodEffectPool.Get().Prewarm(prefab);
    }

    // The existing RPC supplies one seed per death; no per-impact network traffic.
    public static void Spawn(GameObject prefab, Vector3 position, int seed)
    {
        if (prefab != null) BloodEffectPool.Get().Spawn(prefab, position, seed, Time.time);
    }

    internal void Begin(BloodEffectPool owner, float expiry)
    {
        pool = owner;
        decalExpiry = expiry;
        if (particles == null) particles = GetComponent<ParticleSystem>();
    }

    void OnParticleCollision(GameObject other)
    {
        using var sample = CollisionMarker.Auto();
        if (pool == null || particles == null) return;
        int count = particles.GetCollisionEvents(other, collisions);
        pool.CollisionEvents += count;
        for (int i = 0; i < count; i++)
        {
            var hit = collisions[i];
            if (hit.colliderComponent != null)
                pool.Enqueue(decalPrefab, hit.colliderComponent.transform,
                    hit.intersection + hit.normal * 0.01f, Quaternion.LookRotation(-hit.normal), decalExpiry);
        }
    }
}
