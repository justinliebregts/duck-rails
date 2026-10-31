#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Additive, repeatable Week 2 authoring. Preserves the existing login/compound flow.</summary>
public static class RailWeek2Builder
{
    const string Folder = "Assets/Resources/Rails/";
    static Material steel, safety, ground, coal, red;
    [MenuItem("Duck Rails/Build Week 2 Encounter")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene changes first.");
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity");
        RenderSettings.skybox = null;
        var world = UnityEngine.Object.FindFirstObjectByType<RailWorld>();
        if (world == null) throw new InvalidOperationException("Week 1 RailWorld is required.");
        var old = world.GetComponent<RailExpedition>(); if (old != null) UnityEngine.Object.DestroyImmediate(old);
        foreach (var go in new[] { GameObject.Find("Week 2 Environment"), world.train.transform.Find("Week 2 Train")?.gameObject })
            if (go != null) UnityEngine.Object.DestroyImmediate(go);
        world.expedition.SetActive(true);
        var previous = world.expedition.transform.Find("Week 2 Environment"); if (previous != null) UnityEngine.Object.DestroyImmediate(previous.gameObject);
        steel = Mat("Metal", new Color(.22f, .29f, .34f)); safety = Mat("Safety", new Color(.95f, .65f, .12f));
        ground = Mat("Ground", new Color(.12f, .16f, .19f)); coal = Mat("Coal", new Color(.045f, .05f, .055f)); red = Mat("Danger", new Color(.8f, .12f, .06f));
        var run = world.gameObject.AddComponent<RailExpedition>();
        var env = new GameObject("Week 2 Environment").transform; env.SetParent(world.expedition.transform, false);
        var train = new GameObject("Week 2 Train").transform; train.SetParent(world.train.transform, false);
        // Remove old test signage, infinite track and markers; retain the proven carriage.
        foreach (Transform child in world.expedition.transform.Cast<Transform>().ToArray())
            if (child.name == "Track bed" || child.name == "Track rail" || child.name == "Yard marker") UnityEngine.Object.DestroyImmediate(child.gameObject);
        var sign = world.train.transform.Find("Train controls"); if (sign != null) UnityEngine.Object.DestroyImmediate(sign.gameObject);
        foreach (var c in world.train.GetComponentsInChildren<Collider>(true)) if (!c.isTrigger) c.gameObject.layer = 6;
        Box("Approach ground", env, new Vector3(0, -.5f, 275), new Vector3(70, 1, 550), ground);
        Box("Far bank", env, new Vector3(0, -.5f, 730), new Vector3(70, 1, 300), ground);
        Box("Ravine", env, new Vector3(0, -12, 565), new Vector3(100, 1, 30), steel);
        foreach (float x in new[] { -1.5f, 1.5f })
        {
            Box("Approach rail", env, new Vector3(x, .04f, 275), new Vector3(.15f, .08f, 550), steel);
            Box("Exit rail", env, new Vector3(x, .04f, 680), new Vector3(.15f, .08f, 200), steel);
        }
        for (int z = 70; z < 550; z += 8) Box("Sleeper", env, new Vector3(0, .02f, z), new Vector3(3.8f, .04f, .3f), steel);
        // One authored depot, with a continuous side service path to the bridge controls.
        Box("Depot floor", env, new Vector3(17, .1f, 180), new Vector3(24, .2f, 35), steel);
        Box("Depot back", env, new Vector3(28, 2, 180), new Vector3(.4f, 4, 35), ground);
        Box("Depot end", env, new Vector3(17, 2, 197), new Vector3(22, 4, .4f), ground);
        Box("Depot canopy", env, new Vector3(17, 4, 180), new Vector3(24, .3f, 35), steel);
        for (int i = 0; i < 4; i++) Box("Coal crate", env, new Vector3(12 + i % 2 * 8, .5f, 171 + i / 2 * 14), new Vector3(2, 1, 2), safety);
        Text(env, "MAINTENANCE DEPOT\nSTOP • SCAVENGE COAL • RESCUE YOUR CREW", new Vector3(12, 4.8f, 160), 2.6f);
        Text(env, "BRIDGE AHEAD\nSTOP AT RED SIGNS", new Vector3(0, 3.5f, 440), 3);
        Text(env, "BRAKE NOW\nRAISED BRIDGE — CRASH = TEAM FAIL", new Vector3(0, 3.5f, 500), 3);
        for (int z = 500; z <= 540; z += 10)
        { Box("Warning post", env, new Vector3(-4, 1.5f, z), new Vector3(.3f, 3, .3f), red); Box("Warning post", env, new Vector3(4, 1.5f, z), new Vector3(.3f, 3, .3f), red); }
        Box("Bridge control island", env, new Vector3(12, .1f, 530), new Vector3(14, .2f, 25), steel);
        var button = Control("Lower bridge button", env, new Vector3(11, 1.25f, 530), RailControl.Action.Bridge, red);
        run.bridgeButton = button.transform;
        run.bridgeLabel = Text(env, "LOWER BRIDGE", new Vector3(11, 3, 532), 2.5f);
        var pivot = new GameObject("Bridge hinge").transform; pivot.SetParent(env, false); pivot.localPosition = new Vector3(0, 0, 550); run.bridgeDeck = pivot;
        Box("Bridge deck", pivot, new Vector3(0, -.15f, 15), new Vector3(6, .3f, 30), steel);
        foreach (float x in new[] { -2.9f, 2.9f }) Box("Bridge safety rail", pivot, new Vector3(x, .8f, 15), new Vector3(.2f, 1.6f, 30), safety);
        pivot.localRotation = Quaternion.Euler(-80, 0, 0);
        Box("Tunnel roof", env, new Vector3(0, 5, 610), new Vector3(12, 1, 40), steel);
        foreach (float x in new[] { -6f, 6f }) Box("Tunnel wall", env, new Vector3(x, 2.5f, 610), new Vector3(1, 5, 40), ground);
        Text(env, "DAWN", new Vector3(0, 4, 650), 4);
        var lever = Control("STOP GO lever", train, new Vector3(-1.1f, 1, 3.5f), RailControl.Action.Drive, safety);
        lever.handle = Box("Lever", lever.transform, new Vector3(0, .4f, 0), new Vector3(.13f, .8f, .13f), red).transform;
        lever.ConfigureDriveColliders();
        Control("Horn", train, new Vector3(1.1f, 1, 3.5f), RailControl.Action.Horn, safety);
        Control("Train radio", train, new Vector3(-1.8f, 1, -.8f), RailControl.Action.Radio, steel);
        Text(train, "RADIO\nHold the TALK button", new Vector3(-1.8f, 1.8f, -.6f), 1.4f);
        Text(train, "STOP / GO                  HORN\nGrip + push / release to stop", new Vector3(0, 2.7f, 4.5f), 1.8f);
        // Open furnace mouth: loose coal settles on the bottom within the acceptance radius.
        var furnace = new GameObject("Furnace mouth").transform; furnace.SetParent(train, false); furnace.localPosition = new Vector3(0, .55f, 2); run.furnace = furnace;
        Box("Furnace bottom", train, new Vector3(0, .15f, 2), new Vector3(1.4f, .2f, 1.4f), red);
        foreach (float x in new[] { -.7f, .7f }) Box("Furnace side", train, new Vector3(x, .5f, 2), new Vector3(.15f, .7f, 1.4f), steel);
        Box("Furnace back", train, new Vector3(0, .5f, 2.7f), new Vector3(1.4f, .7f, .15f), steel);
        run.gauge = Text(train, "FUEL 10", new Vector3(0, 1.25f, 2.65f), 1.6f);
        // Full-size bags rest on four physical hooks; open shelves hold loose equipment.
        for (int i = 0; i < 4; i++)
        {
            float z = -3.7f + i * .75f;
            Box("Backpack hook stem", train, new Vector3(2.1f, .65f, z), new Vector3(.12f, 1.3f, .12f), safety);
            Box("Backpack hook support", train, new Vector3(1.85f, .3f, z), new Vector3(.6f, .12f, .6f), steel);
        }
        Box("Equipment shelf", train, new Vector3(-1.8f, .3f, -2.8f), new Vector3(1, .15f, 3), steel);
        run.results = Text(train, "RESULTS", new Vector3(0, 2.3f, 4.3f), 1.5f); run.results.rectTransform.sizeDelta = new Vector2(5, 4.5f); run.results.gameObject.SetActive(false);
        var backing = Box("Results backing", run.results.transform, new Vector3(0, 0, .06f), new Vector3(5.2f, 4.5f, .03f), ground);
        UnityEngine.Object.DestroyImmediate(backing.GetComponent<Collider>());
        run.runningLabels = train.GetComponentsInChildren<TMP_Text>(true).Where(t => t != run.results).ToArray();
        run.explosionMaterial = red;
        var bat = Variant("Assets/Prefabs/Weapons/WeaponNailBat.prefab", "RailBat");
        var bag = Variant("Assets/Prefabs/Bags/Bag.prefab", "RailBag");
        var walkie = Variant("Assets/Prefabs/Items/ItemWalkieTalkie.prefab", "RailWalkie");
        var helmet = Variant("Assets/Prefabs/Items/ItemHelmet.prefab", "RailHelmet");
        var goggles = Variant("Assets/Prefabs/Items/ItemNightVisionGoggles.prefab", "RailGoggles");
        var flashlight = Variant("Assets/Prefabs/Items/ItemFlashlight.prefab", "RailFlashlight");
        world.batPrefab = bat; world.bagPrefab = bag;
        run.supplies = new[] { bat, bat, bag, bag, walkie, walkie, helmet, helmet, goggles, goggles, flashlight, flashlight };
        run.supplyPoints = new Transform[run.supplies.Length];
        for (int i = 0; i < run.supplies.Length; i++) run.supplyPoints[i] = Point(train, "Supply " + i, new Vector3(-1.6f + i % 4 * 1.05f, .65f + i / 4 * .05f, -3.5f + i / 4 * 1.2f));
        run.coalPrefab = Coal();
        run.coalPoints = Enumerable.Range(0, 8).Select(i => Point(env, "Coal " + i, new Vector3(12 + i % 2 * 8, 1.4f, 171 + (i / 2 % 2) * 14 + (i / 4) * .5f))).ToArray();
        run.enemyPoints = new[] { Point(env, "Depot enemy A", new Vector3(24, .3f, 165)), Point(env, "Depot enemy B", new Vector3(24, .3f, 190)),
            Point(env, "Bridge enemy A", new Vector3(25, .2f, 515)), Point(env, "Bridge enemy B", new Vector3(25, .2f, 540)) };
        var enemy = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/ZombieMinion.prefab"));
        enemy.name = "RailZombie"; enemy.AddComponent<RailEnemyBounds>();
        run.enemyPrefab = PrefabUtility.SaveAsPrefabAsset(enemy, Folder + "RailZombie.prefab"); UnityEngine.Object.DestroyImmediate(enemy);
        run.flarePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Effects/DeathFlare.prefab").GetComponent<DeathFlare>();
        var baker = new GameObject("Depot crawlgraph bake").AddComponent<CrawlGraphBaker>(); baker.transform.SetParent(env); baker.transform.position = new Vector3(18, 2, 342.5f);
        baker.size = new Vector3(24, 6, 405); baker.cellSize = .8f; baker.agentRadius = .25f; baker.surfaceMask = 1 << 6; baker.allowCeilings = false; baker.minIslandNodes = 10;
        baker.seeds = run.enemyPoints; baker.graphAsset = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scenes/Compound_CrawlGraph.bytes");
        Physics.SyncTransforms();
        if (!CrawlGraphBake.Bake(baker, false)) throw new InvalidOperationException("Depot crawlgraph bake failed.");
        var manager = new GameObject("Depot enemies").AddComponent<EnemyManager>(); manager.transform.SetParent(env);
        var serialized = new SerializedObject(manager); serialized.FindProperty("graphAsset").objectReferenceValue = baker.graphAsset;
        serialized.FindProperty("surfaceMask").intValue = 1 << 6; serialized.FindProperty("visionMask").intValue = 1 << 6;
        serialized.FindProperty("maxEnemies").intValue = 0; serialized.ApplyModifiedPropertiesWithoutUndo(); run.enemies = manager;
        world.expedition.SetActive(false);
        EditorUtility.SetDirty(world); EditorUtility.SetDirty(run); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene("Assets/Scenes/Login.unity");
        Debug.Log("Week 2 authored: 12 shared supplies, 8 coal, depot graph, train controls, bridge and results.");
    }
    static Material Mat(string name, Color color)
    {
        string path = "Assets/DuckRails/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
        m.color = color; return m;
    }
    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 6;
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = scale; go.GetComponent<Renderer>().sharedMaterial = mat; return go;
    }
    static Transform Point(Transform parent, string name, Vector3 pos)
    { var go = new GameObject(name).transform; go.SetParent(parent, false); go.localPosition = pos; return go; }
    static TMP_Text Text(Transform parent, string text, Vector3 pos, float size)
    {
        var label = new GameObject(text.Split('\n')[0]).AddComponent<TextMeshPro>(); label.transform.SetParent(parent, false); label.transform.localPosition = pos;
        label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(16, 4); return label;
    }
    static RailControl Control(string name, Transform parent, Vector3 pos, RailControl.Action action, Material mat)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        Box("Control surface", go.transform, Vector3.zero, new Vector3(.5f, .35f, .5f), mat);
        var control = go.AddComponent<RailControl>(); control.action = action;
        RailHoldControlBuilder.Install(control); return control;
    }
    static GameObject Variant(string source, string name)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source)); go.name = name;
        var cargo = go.GetComponent<RailCargo>() ?? go.AddComponent<RailCargo>(); var view = go.GetComponent<PhotonView>();
        // Match the Week 1 cargo variants: stationary cargo must still provide
        // carriage-relative state to a client entering after the last change.
        view.Synchronization = ViewSynchronization.Unreliable;
        if (!view.ObservedComponents.Contains(cargo)) view.ObservedComponents.Add(cargo);
        var result = PrefabUtility.SaveAsPrefabAsset(go, Folder + name + ".prefab"); UnityEngine.Object.DestroyImmediate(go); return result;
    }
    static GameObject Coal()
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = "RailCoal"; go.layer = 8;
        go.transform.localScale = Vector3.one * .3f; go.GetComponent<Renderer>().sharedMaterial = coal;
        go.AddComponent<Rigidbody>().mass = .6f; var view = go.AddComponent<PhotonView>(); view.OwnershipTransfer = OwnershipOption.Takeover;
        var transformSync = go.AddComponent<PhotonTransformView>(); var item = go.AddComponent<PhotonGrabbable>(); var cargo = go.AddComponent<RailCargo>(); go.AddComponent<RailCoal>();
        view.ObservedComponents = new List<Component> { transformSync, item, cargo }; view.Synchronization = ViewSynchronization.Unreliable;
        var result = PrefabUtility.SaveAsPrefabAsset(go, Folder + "RailCoal.prefab"); UnityEngine.Object.DestroyImmediate(go); return result;
    }
}
#endif
