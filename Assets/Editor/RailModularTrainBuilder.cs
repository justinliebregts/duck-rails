#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>One-time blockout authoring; subsequent scene rebuilds instantiate the edited prefab.</summary>
public static class RailModularTrainBuilder
{
    public const string Folder = "Assets/DuckRails/Train";
    public const string TrainPath = Folder + "/Prefabs/DuckRailsTrain.prefab";
    static Material teal, ivory, wood, steel, amber, dark, glow, particle;

    [MenuItem("Duck Rails/Train/Create Modular Blockout")]
    public static void Build()
    {
        RequireSavedScenes();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(TrainPath) != null)
            throw new InvalidOperationException("The train already exists. Edit its prefabs directly; this command will not overwrite your layout.");
        Directory.CreateDirectory(Folder + "/Prefabs"); Directory.CreateDirectory(Folder + "/Materials");
        AssetDatabase.Refresh(); Materials();
        var scene = EditorSceneManager.OpenScene("Assets/Scenes/Expedition.unity");
        var world = Object.FindFirstObjectByType<RailWorld>();
        if (world == null || world.train == null) throw new InvalidOperationException("Expedition train is missing.");
        var run = world.GetComponent<RailExpedition>();
        var root = world.train.gameObject; var t = root.transform;
        // Preserve the proven motion/cabin components and scene reference to the root.
        var support = Point(t, "Run support", Vector3.zero);
        run.results.transform.SetParent(support, true);
        foreach (var point in run.supplyPoints) point.SetParent(support, true);
        foreach (Transform child in t.Cast<Transform>().ToArray())
            if (child != support && child.name != "Passenger cabin") Object.DestroyImmediate(child.gameObject);
        root.name = "Duck Rails Train";
        var assembly = root.AddComponent<RailTrainAssembly>();
        assembly.results = run.results; assembly.supplyPoints = run.supplyPoints;
        Shell(t);
        assembly.chair = Module(Chair(), t, "Cockpit Chair", new Vector3(0, 0, 3.65f));
        assembly.controls = Module(Controls(), t, "Cockpit Controls", new Vector3(0, 0, 4.45f));
        assembly.fuelGauge = assembly.controls.GetComponentsInChildren<TMP_Text>().First(x => x.name == "Fuel gauge");
        assembly.furnace = Module(Furnace(), t, "Furnace", new Vector3(-1.5f, 0, 1.9f)).GetComponent<RailFurnaceModule>();
        assembly.radio = Module(Radio(), t, "Radio Table", new Vector3(1.9f, 0, 2.1f));
        assembly.runningLabels = t.GetComponentsInChildren<TMP_Text>(true).Where(x => x != run.results).ToArray();
        assembly.Bind(run);
        // Prefab stores a local origin. The live route still owns the scene's pose.
        Vector3 position = t.position; Quaternion rotation = t.rotation;
        t.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        PrefabUtility.SaveAsPrefabAssetAndConnect(root, TrainPath, InteractionMode.AutomatedAction);
        t.SetPositionAndRotation(position, rotation);
        PrefabUtility.RecordPrefabInstancePropertyModifications(t);
        EditorUtility.SetDirty(run); EditorUtility.SetDirty(world);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); AssetDatabase.SaveAssets();
        Selection.activeGameObject = root;
        Debug.Log("Modular train installed in Expedition. Edit nested station prefabs or individual shell children; Week 3 rebuilds reuse this asset.");
    }

    // Called by the route authoring workflow, never at runtime. Retains edited prefab assets.
    public static void InstallPrefab(RailWorld world, RailExpedition run)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(TrainPath);
        if (prefab == null || world.train.GetComponent<RailTrainAssembly>() != null) return;
        var old = world.train;
        var root = (GameObject)PrefabUtility.InstantiatePrefab(prefab, old.transform.parent);
        root.transform.SetPositionAndRotation(old.transform.position, old.transform.rotation);
        root.SetActive(old.gameObject.activeSelf);
        var train = root.GetComponent<RailTrain>(); train.origin = old.origin;
        world.train = train; root.GetComponent<RailTrainAssembly>().Bind(run);
        Object.DestroyImmediate(old.gameObject);
    }

    static void RequireSavedScenes()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your scene edits first.");
    }
    static void Materials()
    {
        teal = Mat("Teal", new Color(.045f, .27f, .29f));
        ivory = Mat("Ivory", new Color(.77f, .72f, .56f));
        wood = Mat("Deck wood", new Color(.36f, .19f, .085f));
        steel = Mat("Steel", new Color(.17f, .18f, .18f));
        dark = Mat("Charcoal", new Color(.055f, .062f, .065f));
        amber = Mat("Amber", new Color(.94f, .53f, .08f));
        glow = Mat("Lamp glass", new Color(1, .67f, .2f));
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(1, .45f, .08f) * 2);
        particle = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
        particle.SetFloat("_Surface", 1); particle.SetFloat("_Blend", 0);
        particle.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        particle.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        particle.SetFloat("_ZWrite", 0); particle.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); particle.renderQueue = 3000;
        AssetDatabase.CreateAsset(particle, Folder + "/Materials/Particles.mat");
    }
    static Material Mat(string name, Color color)
    {
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color = color;
        m.SetFloat("_Smoothness", .05f); AssetDatabase.CreateAsset(m, Folder + "/Materials/" + name + ".mat"); return m;
    }
    static Transform Point(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = position; return t;
    }
    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collision = true)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 6;
        go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = material;
        if (!collision) Object.DestroyImmediate(go.GetComponent<Collider>());
        return go;
    }
    static TMP_Text Label(Transform parent, string name, string value, Vector3 position, Vector2 size, float font)
    {
        var t = new GameObject(name).AddComponent<TextMeshPro>(); t.transform.SetParent(parent, false);
        t.transform.localPosition = position; t.text = value; t.fontSize = font;
        t.alignment = TextAlignmentOptions.Center; t.rectTransform.sizeDelta = size; t.color = new Color(1, .85f, .55f);
        return t;
    }
    static Transform Module(GameObject source, Transform parent, string name, Vector3 position)
    {
        source.name = name;
        var prefab = PrefabUtility.SaveAsPrefabAsset(source, Folder + "/Prefabs/" + name.Replace(" ", "") + ".prefab");
        Object.DestroyImmediate(source);
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        instance.transform.localPosition = position; PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
        return instance.transform;
    }
    static void Shell(Transform root)
    {
        var hull = Point(root, "Hull and deck", Vector3.zero);
        Box(hull, "Deck collision slab", new Vector3(0, -.15f, 0), new Vector3(5, .3f, 10), wood);
        // Separate plank strips retain the blockout's modular editing, without collision seams.
        for (int i = 0; i < 20; i++)
            Box(hull, "Deck plank " + i.ToString("00"), new Vector3(0, -.012f, -4.75f + i * .5f), new Vector3(4.98f, .025f, .48f), wood, false);
        Box(hull, "Chassis", new Vector3(0, -.48f, 0), new Vector3(4.5f, .35f, 9.8f), teal);
        var wheels = Point(root, "Wheels", Vector3.zero);
        foreach (float x in new[] { -2.16f, 2.16f }) foreach (float z in new[] { -3.3f, 3.3f })
        {
            var wheel = GameObject.CreatePrimitive(PrimitiveType.Cylinder); wheel.name = "Wheel " + x + " " + z;
            wheel.transform.SetParent(wheels, false); wheel.transform.localPosition = new Vector3(x, -.68f, z);
            wheel.transform.localRotation = Quaternion.Euler(0, 0, 90); wheel.transform.localScale = new Vector3(.86f, .16f, .86f);
            wheel.GetComponent<Renderer>().sharedMaterial = dark; Object.DestroyImmediate(wheel.GetComponent<Collider>());
        }
        var rails = Point(root, "Side rails - rear stays open", Vector3.zero);
        foreach (float x in new[] { -2.42f, 2.42f })
        {
            foreach (var span in new[] { new Vector2(-3, 4), new Vector2(2, 2) })
            {
                Box(rails, "Side panel " + x + " " + span.x, new Vector3(x, .16f, span.x), new Vector3(.14f, .32f, span.y), teal);
                Box(rails, "Amber rail " + x + " " + span.x, new Vector3(x, .35f, span.x), new Vector3(.16f, .06f, span.y), amber);
            }
            foreach (float z in new[] { -4.9f, -1.05f, 1.05f, 2.95f })
                Box(rails, "Rail post " + x + " " + z, new Vector3(x, .2f, z), new Vector3(.19f, .4f, .16f), ivory);
        }
        var shelter = Point(root, "Cockpit shell", Vector3.zero);
        Box(shelter, "Low front sill", new Vector3(0, .1f, 4.9f), new Vector3(5, .2f, .16f), teal);
        foreach (float x in new[] { -2.42f, 2.42f })
        {
            Box(shelter, "Low side sill " + x, new Vector3(x, .1f, 4), new Vector3(.16f, .2f, 2), teal);
            foreach (float z in new[] { 3.05f, 4.9f })
                Box(shelter, "Window pillar " + x + " " + z, new Vector3(x, .8f, z), new Vector3(.18f, 1.6f, .18f), ivory);
        }
        Box(shelter, "Roof", new Vector3(0, 1.65f, 4), new Vector3(5.15f, .18f, 2.2f), teal);
        Box(shelter, "Front roof trim", new Vector3(0, 1.56f, 5.06f), new Vector3(5.15f, .13f, .12f), ivory);
        var lights = Point(root, "Lighting", Vector3.zero);
        Box(lights, "Headlight housing", new Vector3(0, .23f, 5.1f), new Vector3(.48f, .38f, .25f), steel, false);
        Box(lights, "Headlight lens", new Vector3(0, .23f, 5.235f), new Vector3(.35f, .26f, .02f), glow, false);
        var head = Point(lights, "Headlight beam", new Vector3(0, .4f, 5.3f)).gameObject.AddComponent<Light>();
        head.type = LightType.Spot; head.range = 32; head.spotAngle = 65; head.intensity = 7; head.color = new Color(1, .85f, .6f); head.shadows = LightShadows.None;
        head.transform.localRotation = Quaternion.Euler(4, 0, 0);
        foreach (float x in new[] { -2.22f, 2.22f })
        {
            Box(lights, "Side lamp housing " + x, new Vector3(x, .66f, 4.8f), new Vector3(.22f, .3f, .2f), steel, false);
            Box(lights, "Side lamp lens " + x, new Vector3(x, .66f, 4.67f), new Vector3(.16f, .21f, .06f), glow, false);
            var light = Point(lights, "Cockpit side light " + x, new Vector3(x, .75f, 4.55f)).gameObject.AddComponent<Light>();
            light.type = LightType.Point; light.range = 3; light.intensity = 1.2f; light.color = new Color(1, .62f, .22f); light.shadows = LightShadows.None;
        }
    }
    static GameObject Chair()
    {
        var root = new GameObject("Chair"); var t = root.transform;
        Box(t, "Seat pedestal", new Vector3(0, .055f, 0), new Vector3(.42f, .11f, .4f), steel);
        Box(t, "Seat cushion", new Vector3(0, .145f, 0), new Vector3(.64f, .07f, .58f), dark);
        Box(t, "Seat back", new Vector3(0, .29f, -.27f), new Vector3(.64f, .4f, .08f), dark);
        return root;
    }
    static GameObject Controls()
    {
        var root = new GameObject("Controls"); var t = root.transform;
        Box(t, "Dash", new Vector3(0, .235f, .05f), new Vector3(1.5f, .07f, .42f), steel);
        foreach (float x in new[] { -.6f, .6f }) Box(t, "Dash support " + x, new Vector3(x, .1f, .15f), new Vector3(.08f, .2f, .08f), ivory);
        var drive = Point(t, "Throttle", new Vector3(0, .285f, -.07f)).gameObject.AddComponent<RailControl>();
        drive.action = RailControl.Action.Drive;
        drive.handle = Box(drive.transform, "Grip lever", new Vector3(0, .08f, 0), new Vector3(.1f, .16f, .1f), amber).transform;
        Box(drive.handle, "Lever cap", new Vector3(0, .45f, 0), new Vector3(1.6f, .3f, 1.3f), amber, false);
        drive.ConfigureDriveColliders();
        Box(t, "Gauge backing", new Vector3(-.48f, .28f, -.01f), new Vector3(.38f, .025f, .24f), dark, false);
        var gauge = Label(t, "Fuel gauge", "FUEL 10.0", new Vector3(-.48f, .297f, -.01f), new Vector2(.36f, .2f), .54f);
        gauge.transform.localRotation = Quaternion.Euler(90, 0, 0);
        Hold(t, "Horn", new Vector3(.48f, .28f, -.01f), false);
        var label = Label(t, "Throttle label", "GRIP + PUSH", new Vector3(0, .28f, .15f), new Vector2(.6f, .12f), .38f);
        label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        return root;
    }
    static void Hold(Transform parent, string name, Vector3 position, bool radio)
    {
        var control = Point(parent, name, position).gameObject.AddComponent<RailControl>();
        control.action = radio ? RailControl.Action.Radio : RailControl.Action.Horn;
        var buttonRoot = Point(control.transform, "Momentary contact", Vector3.zero);
        var volume = buttonRoot.gameObject.AddComponent<BoxCollider>(); volume.isTrigger = true; volume.size = new Vector3(.2f, .08f, .18f);
        volume.center = new Vector3(0, .03f, 0);
        var button = buttonRoot.gameObject.AddComponent<PressableButton>();
        button.cap = Box(buttonRoot, "Amber cap", new Vector3(0, .025f, 0), new Vector3(.18f, .04f, .16f), amber, false).transform;
        button.pressOffset = new Vector3(0, -.018f, 0);
        var label = Label(button.cap, "Button label", radio ? "TALK" : "HORN", new Vector3(0, .52f, 0), new Vector2(.9f, .7f), 2);
        label.transform.localRotation = Quaternion.Euler(90, 0, 0);
        // Label under scaled cap is visual only; hand contact volume remains in metres.
        var hold = control.gameObject.AddComponent<RailHoldControl>(); hold.radio = radio; hold.button = button;
    }
    static GameObject Radio()
    {
        var root = new GameObject("Radio"); var t = root.transform;
        Box(t, "Tabletop", new Vector3(0, .325f, 0), new Vector3(.6f, .05f, 1.2f), wood);
        foreach (float x in new[] { -.23f, .23f }) foreach (float z in new[] { -.5f, .5f })
            Box(t, "Table leg " + x + " " + z, new Vector3(x, .15f, z), new Vector3(.07f, .3f, .07f), steel);
        Box(t, "Receiver", new Vector3(.08f, .51f, .21f), new Vector3(.34f, .32f, .52f), teal);
        Box(t, "Speaker face", new Vector3(-.096f, .51f, .21f), new Vector3(.012f, .25f, .44f), ivory, false);
        // Flat grille texture, not a row of modeled knobs or buttons.
        var tex = new Texture2D(16, 16, TextureFormat.RGBA32, false); tex.filterMode = FilterMode.Point;
        for (int y = 0; y < 16; y++) for (int x = 0; x < 16; x++) tex.SetPixel(x, y, x % 3 == 1 && y % 3 == 1 ? new Color(.08f,.09f,.08f) : new Color(.77f,.72f,.56f));
        tex.Apply(); File.WriteAllBytes(Folder + "/Materials/RadioGrille.png", tex.EncodeToPNG()); Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(Folder + "/Materials/RadioGrille.png");
        var importer = (TextureImporter)AssetImporter.GetAtPath(Folder + "/Materials/RadioGrille.png"); importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var grille = Mat("Radio grille", Color.white); grille.mainTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Materials/RadioGrille.png");
        t.Find("Speaker face").GetComponent<Renderer>().sharedMaterial = grille;
        Box(t, "Mic base", new Vector3(-.08f, .38f, -.36f), new Vector3(.34f, .06f, .32f), teal);
        Box(t, "Mic stem", new Vector3(.015f, .51f, -.36f), new Vector3(.055f, .22f, .055f), steel);
        Box(t, "Mic head", new Vector3(.015f, .66f, -.36f), new Vector3(.13f, .16f, .14f), ivory);
        Box(t, "Mic grille", new Vector3(-.055f, .66f, -.36f), new Vector3(.01f, .12f, .1f), grille, false);
        Hold(t, "Radio talk", new Vector3(-.16f, .42f, -.36f), true);
        return root;
    }
    static GameObject Furnace()
    {
        var root = new GameObject("Furnace"); var t = root.transform;
        var module = root.AddComponent<RailFurnaceModule>();
        // Local +X is the only opening, toward the central aisle. Six separate shell pieces.
        Box(t, "Firebox floor", new Vector3(0, .08f, 0), new Vector3(1.6f, .16f, 1.6f), steel);
        Box(t, "Firebox ceiling", new Vector3(0, 1.32f, 0), new Vector3(1.6f, .16f, 1.6f), steel);
        Box(t, "Firebox back", new Vector3(-.72f, .7f, 0), new Vector3(.16f, 1.08f, 1.6f), steel);
        foreach (float z in new[] { -.72f, .72f })
            Box(t, "Firebox side " + z, new Vector3(0, .7f, z), new Vector3(1.6f, 1.08f, .16f), steel);
        // Opening 1.28m along Z x 1.08m high; no invisible collider or emissive plate across it.
        module.insertion = Point(t, "Coal acceptance - inside chamber", new Vector3(0, .7f, 0));
        Box(t, "Stack collar", new Vector3(0, 1.47f, 0), new Vector3(.8f, .14f, .8f), dark);
        var stack = Point(t, "Tall hollow stack", Vector3.zero);
        foreach (float x in new[] { -.28f, .28f }) Box(stack, "Stack wall X " + x, new Vector3(x, 2.52f, 0), new Vector3(.12f, 2.1f, .68f), steel);
        foreach (float z in new[] { -.28f, .28f }) Box(stack, "Stack wall Z " + z, new Vector3(0, 2.52f, z), new Vector3(.44f, 2.1f, .12f), steel);
        module.fire = Effect(t, "Fire particles", new Vector3(.05f, .25f, 0), false);
        module.smoke = Effect(t, "Moving smoke particles", new Vector3(0, 3.6f, 0), true);
        return root;
    }
    static ParticleSystem Effect(Transform parent, string name, Vector3 position, bool smoke)
    {
        var ps = Point(parent, name, position).gameObject.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main; main.playOnAwake = false; main.loop = true; main.duration = 3;
        main.simulationSpace = smoke ? ParticleSystemSimulationSpace.World : ParticleSystemSimulationSpace.Local;
        main.startLifetime = smoke ? 2.8f : .65f; main.startSpeed = smoke ? .65f : .5f;
        main.startSize = smoke ? .38f : .12f; main.maxParticles = smoke ? 24 : 32;
        main.startColor = smoke ? new Color(.18f, .19f, .2f, .38f) : new Color(1, .45f, .035f, .9f);
        ps.transform.localRotation = Quaternion.Euler(-90, 0, 0);
        var emission = ps.emission; emission.rateOverTime = smoke ? 6 : 22; emission.enabled = false;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 12; shape.radius = smoke ? .18f : .35f;
        var size = ps.sizeOverLifetime; size.enabled = true; size.size = new ParticleSystem.MinMaxCurve(1, new AnimationCurve(new Keyframe(0, smoke ? .5f : 1), new Keyframe(1, smoke ? 2 : .1f)));
        var color = ps.colorOverLifetime; color.enabled = true; var gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(1, .15f), new GradientAlphaKey(0, 1) }); color.color = gradient;
        var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = particle;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return ps;
    }
}
#endif
