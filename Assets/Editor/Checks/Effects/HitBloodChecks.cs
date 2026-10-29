using System;
using Game.AI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Editor-only regression: visual placement must not change hit acceptance or allocate.
public static class HitBloodChecks
{
    public static void Run()
    {
        foreach (string name in new[] { "ZombieSmasher", "ZombieMinion" })
        {
            var instance = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{name}.prefab"));
            try
            {
                foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>()) behaviour.enabled = false;
                var enemy = instance.GetComponent<Enemy>();
                Vector3 center = enemy.Root.position + enemy.Root.up * 0.3f;
                foreach (Vector3 direction in new[] { Vector3.forward, Vector3.back, Vector3.right, Vector3.up, new Vector3(1, 1, 1).normalized })
                {
                    foreach (bool startsInside in new[] { false, true })
                    {
                        Vector3 from = startsInside ? center : center - direction * 3;
                        Vector3 to = center + direction * 3;
                        Check(enemy.OverlapsSwing(from, to, 0.05f), "Fixture sweep must intersect");
                        Vector3 origin = enemy.HitBloodPosition(from, to, 0.05f);
                        Check(!enemy.OverlapsSwing(origin, origin, 0.05f), "Origin must be outside all collision shapes");
                        Check(Vector3.Dot(origin - center, direction) < 0, "Origin must be on approach side");
                        Check(enemy.OverlapsSwing(origin, to, 0.05f), "Contact must remain on intersecting sweep");
                    }
                }
                Vector3 start = center - Vector3.forward * 3, end = center + Vector3.forward * 3;
                for (int i = 0; i < 100; i++) enemy.HitBloodPosition(start, end, 0.05f);
                long bytes = GC.GetAllocatedBytesForCurrentThread();
                long began = System.Diagnostics.Stopwatch.GetTimestamp();
                for (int i = 0; i < 10000; i++) enemy.HitBloodPosition(start, end, 0.05f);
                long elapsed = System.Diagnostics.Stopwatch.GetTimestamp() - began;
                bytes = GC.GetAllocatedBytesForCurrentThread() - bytes;
                Check(bytes == 0, "Contact calculation must allocate zero bytes");
                Debug.Log($"HitBloodChecks PASS {name}: directions/start-inside, zero allocation; {elapsed * 1000000.0 / System.Diagnostics.Stopwatch.Frequency / 10000:F3} us/hit");
            }
            finally { Object.DestroyImmediate(instance); }
        }
    }
    static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }
}
