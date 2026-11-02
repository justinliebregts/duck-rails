using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Stable per-route encounter choices using authored, navigation-checked points.</summary>
public static class RailEnemySpawns
{
    public static readonly string[] Names = { "RailZombie", "RailBomber", "RailSmasher", "RailSpider" };
    public static GameObject Prefab(int type) => Resources.Load<GameObject>("Rails/" + Names[type]);
    public static void Register() { for (int i = 0; i < Names.Length; i++) PhotonPreparePool.Register(Prefab(i)); }
    public static void Select(Transform[] candidates, System.Random rng, List<Transform> points, List<int> types)
    {
        if (candidates == null || candidates.Length < 6) throw new InvalidOperationException("An encounter needs six authored enemy candidates.");
        var order = (Transform[])candidates.Clone();
        for (int i = order.Length - 1; i > 0; i--) { int j = rng.Next(i + 1); (order[i], order[j]) = (order[j], order[i]); }
        int count = rng.Next(4, 7);
        var mix = new int[count];
        for (int i = 0; i < count; i++) mix[i] = i < 4 ? i : rng.Next(4);
        for (int i = count - 1; i > 0; i--) { int j = rng.Next(i + 1); (mix[i], mix[j]) = (mix[j], mix[i]); }
        for (int i = 0; i < count; i++) { points.Add(order[i]); types.Add(mix[i]); }
    }
}
