#if UNITY_EDITOR
using System;
using System.Linq;
using Photon.Pun;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Repeatable authoring of Week 1 gray-box scenes. Never touches Quackville or the source Duckz project.</summary>
public static class RailSceneBuilder
{
    const string Root = "Assets/DuckRails";
    static Material ground, teal, yellow, metal;
    [MenuItem("Duck Rails/Build Week 1 Scenes")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your scene edits before building.");
        System.IO.Directory.CreateDirectory(Root + "/Materials");
        System.IO.Directory.CreateDirectory("Assets/Resources/Rails");
        AssetDatabase.Refresh();
        ground = Material("Ground", new Color(.12f, .16f, .19f));
        teal = Material("Station", new Color(.08f, .42f, .43f));
        yellow = Material("Safety", new Color(.95f, .65f, .15f));
        metal = Material("Metal", new Color(.27f, .3f, .34f));
        var bat = CargoPrefab("Assets/Prefabs/Weapons/WeaponNailBat.prefab", "RailBat");
        var bag = CargoPrefab("Assets/Prefabs/Bags/Bag.prefab", "RailBag");
        BuildCompound(bat, bag);
        BuildLogin();
        EditorBuildSettings.scenes = new[] {
            new EditorBuildSettingsScene("Assets/Scenes/Login.unity", true),
            new EditorBuildSettingsScene("Assets/Scenes/Compound.unity", true)
        };
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Login.unity");
        Debug.Log("Duck Rails Week 1 scenes authored: Login -> Compound. Quackville excluded from build.");
    }
    static Material Material(string name, Color color)
    {
        string path = Root + "/Materials/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, path); }
        mat.color = color; return mat;
    }
    static GameObject CargoPrefab(string source, string name)
    {
        var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(source));
        go.name = name;
        var cargo = go.AddComponent<RailCargo>();
        var view = go.GetComponent<PhotonView>();
        view.ObservedComponents.Add(cargo);
        view.Synchronization = ViewSynchronization.Unreliable;
        var prefab = PrefabUtility.SaveAsPrefabAsset(go, "Assets/Resources/Rails/" + name + ".prefab");
        UnityEngine.Object.DestroyImmediate(go);
        return prefab;
    }
    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name;
        go.transform.SetParent(parent, false); go.transform.localPosition = pos; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = mat; return go;
    }
    static TMP_Text Text(string name, Transform parent, string text, Vector3 pos, float size = 1)
    {
        var go = new GameObject(name); go.transform.SetParent(parent, false); go.transform.localPosition = pos;
        var label = go.AddComponent<TextMeshPro>(); label.text = text; label.fontSize = 3 * size;
        label.alignment = TextAlignmentOptions.Center; label.color = Color.white;
        label.rectTransform.sizeDelta = new Vector2(10, 3); return label;
    }
    static void Lighting(Transform parent = null)
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.36f, .42f, .5f);
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.07f, .11f, .17f);
        RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .008f;
        var light = new GameObject("Moonlight").AddComponent<Light>(); light.type = LightType.Directional;
        light.transform.SetParent(parent, false); light.intensity = 1.2f; light.transform.rotation = Quaternion.Euler(50, -35, 0);
    }
    static void BuildCompound(GameObject bat, GameObject bag)
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        Lighting();
        var world = new GameObject("Duck Rails Session World").AddComponent<RailWorld>();
        world.batPrefab = bat; world.bagPrefab = bag;
        world.compound = new GameObject("Compound Geometry");
        var root = world.compound.transform;
        Box("Compound floor", root, new Vector3(0, -.3f, 0), new Vector3(30, .6f, 30), ground);
        Box("Station back wall", root, new Vector3(0, 3, 12), new Vector3(28, 6, .5f), teal);
        Box("Station canopy", root, new Vector3(0, 6, 7), new Vector3(28, .4f, 10), metal);
        foreach (float x in new[] { -13f, 13f }) Box("Canopy pillar", root, new Vector3(x, 3, 3), new Vector3(.4f, 6, .4f), yellow);
        Box("Public boarding platform", root, new Vector3(0, .1f, 4), new Vector3(8, .2f, 6), yellow);
        var trigger = new GameObject("Departure occupancy trigger").AddComponent<BoxCollider>();
        trigger.transform.SetParent(root); trigger.transform.position = new Vector3(0, 1.65f, 4);
        trigger.size = new Vector3(8, 3, 6); trigger.isTrigger = true; world.boardingTrigger = trigger;
        world.departureBoard = Text("Public countdown", root, "PUBLIC DEPARTURE\nStep onto the platform", new Vector3(0, 3, 8));
        Text("Title", root, "DUCK RAILS", new Vector3(0, 5, 11.6f), 2);
        var spawn = new GameObject("Compound spawn").transform; spawn.position = new Vector3(0, .2f, -8); world.compoundSpawn = spawn;
        var terminal = Box("Private terminal", root, new Vector3(-9, 1, 0), new Vector3(2.5f, 2, 1), teal);
        world.privateTerminal = terminal.transform;
        Text("Private instructions", root, "PRIVATE EXPEDITIONS\nTab: Create / Join\nChoose a five-digit code", new Vector3(-9, 3, 1), .65f);
        Text("Controls", root, "WASD move • Mouse grip\nSpace jump • Tab controls", new Vector3(8, 2, 0), .65f);
        RailPrivateTerminalBuilder.Install(world);
        var gm = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Manager/GameManager.prefab"));
        gm.transform.position = Vector3.zero;
        var manager = gm.GetComponent<GameManager>(); manager.respawnPoint = spawn;
        manager.disableEnemySpawning = true; manager.disableDuckCompanion = true;
        PrefabUtility.RecordPrefabInstancePropertyModifications(manager);
        var rig = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DuckPlayer.prefab"));
        rig.transform.position = spawn.position;
        var desktop = new GameObject("Desktop rig selection").AddComponent<DesktopPlay>();
        var serialized = new SerializedObject(desktop);
        serialized.FindProperty("desktopPlayerPrefab").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/DesktopPlayer.prefab");
        serialized.ApplyModifiedPropertiesWithoutUndo();
        world.expedition = new GameObject("Expedition test block — rail-yard-test-v1");
        root = world.expedition.transform;
        Box("Track bed", root, new Vector3(0, -.4f, 2000), new Vector3(70, .8f, 4200), ground);
        foreach (float x in new[] { -1.5f, 1.5f }) Box("Track rail", root, new Vector3(x, .05f, 2000), new Vector3(.15f, .1f, 4200), metal);
        for (int z = 40; z < 1200; z += 40)
            Box("Yard marker", root, new Vector3(12, 2, z), new Vector3(1, 4, 1), teal);
        var carriage = new GameObject("Moving carriage"); carriage.transform.SetParent(root); carriage.transform.position = new Vector3(0, 1, 70);
        Box("Carriage floor", carriage.transform, new Vector3(0, -.15f, 0), new Vector3(5, .3f, 10), teal);
        foreach (float x in new[] { -2.4f, 2.4f })
        {
            Box("Side rail front", carriage.transform, new Vector3(x, .75f, 3.3f), new Vector3(.15f, .15f, 3), yellow);
            Box("Side rail back", carriage.transform, new Vector3(x, .75f, -3.3f), new Vector3(.15f, .15f, 3), yellow);
        }
        Box("Forward rail", carriage.transform, new Vector3(0, .75f, 4.9f), new Vector3(5, .15f, .15f), yellow);
        Text("Train controls", carriage.transform, "TEST CARRIAGE\nTab: GO / STOP / Return", new Vector3(0, 2, 4.5f), .6f);
        var cabinBounds = new GameObject("Passenger cabin").AddComponent<BoxCollider>(); cabinBounds.transform.SetParent(carriage.transform, false);
        cabinBounds.center = new Vector3(0, 1.3f, 0); cabinBounds.size = new Vector3(5, 2.8f, 10); cabinBounds.isTrigger = true;
        var cabin = carriage.AddComponent<Helicopter>();
        var cabinData = new SerializedObject(cabin); cabinData.FindProperty("passengerCabin").objectReferenceValue = cabinBounds; cabinData.ApplyModifiedPropertiesWithoutUndo();
        cabin.swayBobMeters = cabin.swayPitchDegrees = cabin.swayRollDegrees = 0;
        world.train = carriage.AddComponent<RailTrain>(); world.train.cabin = cabin;
        world.expedition.SetActive(false);
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Compound.unity");
    }
    static void BuildLogin()
    {
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Login.unity", OpenSceneMode.Single);
        var old = GameObject.Find("Duck Rails Login Art"); if (old != null) UnityEngine.Object.DestroyImmediate(old);
        var art = new GameObject("Duck Rails Login Art").transform;
        Box("Login floor", art, new Vector3(0, -.3f, 0), new Vector3(36, .6f, 36), ground);
        Box("Station facade", art, new Vector3(0, 3, 9), new Vector3(26, 6, .6f), teal);
        Box("Canopy", art, new Vector3(0, 6, 6), new Vector3(28, .4f, 9), metal);
        foreach (float x in new[] { -10f, 10f }) Box("Pillar", art, new Vector3(x, 3, 3), new Vector3(.5f, 6, .5f), yellow);
        Text("Login title", art, "DUCK RAILS", new Vector3(0, 4, 8.5f), 2);
        Text("Login subtitle", art, "THE NIGHT SHIFT", new Vector3(0, 2.7f, 8.5f), .6f);
        var spots = UnityEngine.Object.FindObjectsByType<LoginSpot>(FindObjectsInactive.Include, FindObjectsSortMode.InstanceID);
        for (int i = 0; i < spots.Length; i++)
        {
            spots[i].transform.position = new Vector3((i % 3 - 1) * 6, 0, -7 - i / 3 * 2);
            spots[i].transform.rotation = Quaternion.LookRotation(new Vector3(0, 0, 5) - spots[i].transform.position);
        }
        Lighting(art);
        EditorSceneManager.SaveScene(scene);
    }
}
#endif
