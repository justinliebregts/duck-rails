using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DeathFlareLightChecks
{
    const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

    // Run in a separate batch editor to keep the user's open scenes and live flares untouched.
    public static void Run()
    {
        Check(Application.isBatchMode, "requires a batch editor");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var prefab = AssetDatabase.LoadAssetAtPath<DeathFlare>(DeathFlareSetup.PrefabPath);
        var prefabLight = prefab.GetComponentInChildren<Light>();
        Check(prefabLight.range == 4f && !prefabLight.enabled && prefabLight.shadows == LightShadows.None,
            "prefab has a four-metre, initially disabled, shadowless light");
        Check(new SerializedObject(prefab).FindProperty("lightActivationDistance").floatValue == 12f,
            "prefab has a twelve-metre activation distance");
        var near = Create("Near", new Vector3(2, 0, 0));
        var farther = Create("Farther", new Vector3(5, 0, 0));
        var waiting = Create("Waiting", Vector3.zero, -2f);
        try
        {
            Select(Vector3.zero);
            Check(Lit(near) && !Lit(farther) && !Lit(waiting), "only nearest launched flare lights");
            Check(DeathFlare.FindNear(Vector3.zero, 10) == near, "gameplay flare lookup stays independent");
            Select(new Vector3(6, 0, 0));
            Check(!Lit(near) && Lit(farther), "moving viewer transfers the light");
            Select(new Vector3(17, 0, 0));
            Check(!Lit(near) && !Lit(farther), "lights disable at the distance boundary");
            Select(Vector3.zero);
            near.gameObject.SetActive(false);
            Call(near, "OnDisable");
            Check(!Lit(near), "disabled flare immediately releases its light");
            Select(Vector3.zero);
            Check(Lit(farther), "next nearby flare takes over");
            typeof(DeathFlare).GetField("started", Private).SetValue(farther,
                Time.unscaledTimeAsDouble - DeathFlare.Lifetime - 1);
            Select(Vector3.zero);
            Check(!Lit(farther) && !Lit(waiting), "expired and delayed flares cannot light");
            Call(farther, "Initialize", 0f);
            Select(Vector3.zero);
            Check(Lit(farther), "eligible flare lights again");
            Select(null);
            Check(!Lit(farther), "no local camera disables all flare lights");
            Debug.Log("[DeathFlare] Light checks PASS: range, nearest-only selection, distance cutoff, handoff, delay, expiry, missing viewer, independent gameplay lookup.");
        }
        finally
        {
            foreach (var flare in new[] { near, farther, waiting })
            {
                Call(flare, "OnDisable");
                UnityEngine.Object.DestroyImmediate(flare.gameObject);
            }
        }
    }

    static DeathFlare Create(string name, Vector3 position, float age = 0f)
    {
        var root = new GameObject(name);
        var light = root.AddComponent<Light>();
        var flare = root.AddComponent<DeathFlare>();
        typeof(DeathFlare).GetField("flareLight", Private).SetValue(flare, light);
        Call(flare, "OnEnable");
        Call(flare, "Initialize", age);
        root.transform.position = position;
        return flare;
    }

    static bool Lit(DeathFlare flare) => flare.GetComponent<Light>().enabled;
    static void Call(DeathFlare flare, string method, params object[] args)
        => typeof(DeathFlare).GetMethod(method, Private).Invoke(flare, args);
    static void Select(Vector3? viewer)
        => typeof(DeathFlare).GetMethod("SelectNearbyLight", BindingFlags.NonPublic | BindingFlags.Static)
            .Invoke(null, new object[] { viewer });
    static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Flare light check failed: " + message);
    }
}
