#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Installs the saved SubwayTerminal prefab into the Compound scene and applies the compound's gameplay
/// placements and environment. Used by the legacy scene rebuild; the artist-edited prefab is only referenced.
/// </summary>
public static class RailCompoundInstaller
{
    public const string PrefabPath = "Assets/DuckRails/Compound/SubwayTerminal/SubwayTerminal.prefab";

    public static void InstallExisting(RailWorld world)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        if (world == null || world.compound == null || world.boardingTrigger == null || world.departureBoard == null)
            throw new InvalidOperationException("Compound gameplay bindings are missing.");
        var parent = world.compound.transform;
        if (parent.Find(prefab.name) == null) PrefabUtility.InstantiatePrefab(prefab, parent);
        foreach (string name in new[] { "Compound floor", "Station back wall", "Station canopy", "Canopy pillar", "Public boarding platform", "Title", "Private instructions", "Controls", "Expedition instructions" })
            foreach (Transform child in parent) if (child.name == name) child.gameObject.SetActive(false);
        // Keep the same gameplay components and persistent UnityEvents. Only move their presentation.
        world.boardingTrigger.transform.position = new Vector3(0, 1.4f, 7.1f);
        world.boardingTrigger.center = Vector3.zero; world.boardingTrigger.size = new Vector3(6, 3, 3.6f);
        world.departureBoard.transform.position = new Vector3(0, 1.5f, 8.88f);
        world.departureBoard.transform.rotation = Quaternion.identity;
        world.departureBoard.fontSize = 6;
        world.departureBoard.rectTransform.sizeDelta = new Vector2(5.5f, 2.1f);
        world.departureBoard.enableAutoSizing = true;
        world.departureBoard.fontSizeMin = 1; world.departureBoard.fontSizeMax = 6;
        // Private create/join lives in the pause menu; the gate gets the owner's launch button.
        RailLaunchButtonBuilder.Install(world);
        world.compoundSpawn.position = new Vector3(0, .2f, -6.7f);
        var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gm != null) { gm.respawnPoint = world.compoundSpawn; EditorUtility.SetDirty(gm); }
        var moon = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(l => l.gameObject.name == "Moonlight");
        if (moon != null) moon.gameObject.SetActive(false);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.21f, .23f, .27f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.025f, .035f, .045f);
        RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .012f;
        EditorUtility.SetDirty(world);
    }
}
#endif
