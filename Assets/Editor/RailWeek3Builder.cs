#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using Game.AI;
using Game.AI.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Repeatable gray-box authoring. Graphs are baked here, never by the runtime route loader.</summary>
public static class RailWeek3Builder
{
    public const string Folder = "Assets/DuckRails/Blocks";
    const string Source = "Assets/DuckRails/Authoring/Week2Baseline.unity";
    static Material ground, steel, yellow, wall;
    public static readonly string[] Names = { "Maintenance Depot", "Convenience Store", "Roadside Motel", "Lumber Yard", "Container Yard", "Passenger Station", "Farm Silos", "Tunnel Works", "Scrapyard", "Lakeside Campground", "Electrical Substation", "Quarry" };
    static string Id(int i) => Names[i].ToLowerInvariant().Replace(' ', '-') + "-v1";

    [MenuItem("Duck Rails/Build Week 3 Expedition")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode before authoring.");
        for (int i = 0; i < SceneManager.sceneCount; i++) if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save scene edits before rebuilding.");
        Directory.CreateDirectory(Folder); Directory.CreateDirectory("Assets/DuckRails/Authoring"); AssetDatabase.Refresh();
        // Preserve the user-edited train and controls as the initial authoring source. Never rerun Week 1/2 builders here.
        if (!File.Exists(Source) && !AssetDatabase.CopyAsset("Assets/Scenes/Compound.unity", Source)) throw new IOException("Could not preserve baseline scene.");
        ground = Material("Ground"); steel = Material("Metal"); yellow = Material("Safety"); wall = Material("Station");
        var catalog = new RailBlock[12];
        for (int i = 0; i < catalog.Length; i++) catalog[i] = BuildBlock(i);
        BuildScenes(catalog);
        AddCarrier();
        RailEnemyAuthoring.Install();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene("Assets/Scenes/Login.unity", true), new EditorBuildSettingsScene("Assets/Scenes/Compound.unity", true), new EditorBuildSettingsScene("Assets/Scenes/Expedition.unity", true) };
        AssetDatabase.SaveAssets();
        EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity");
        Debug.Log("Week 3 authored: twelve block prefabs, three variations each, baked graphs, Expedition scene, preserved train/finale.");
    }
    static Material Material(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/" + name + ".mat");
    static RailBlock BuildBlock(int index)
    {
        // Preserve authored campground geometry and lighting.
        if (index == 9)
        {
            var authored = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/" + Id(index) + ".prefab");
            if (authored != null && authored.transform.Find(RailCampgroundGreybox.RootName) != null)
                return authored.GetComponent<RailBlock>();
        }
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var root = new GameObject(Names[index]); var block = root.AddComponent<RailBlock>();
        block.id = Id(index); block.displayName = Names[index]; block.tags = new[] { index % 2 == 0 ? "interior" : "yard", index % 3 == 0 ? "curved" : "straight" };
        block.entry = Point(root.transform, "Track Entry", Vector3.zero); block.exit = Point(root.transform, "Track Exit", new Vector3(0, 0, 160));
        block.stop = Point(root.transform, "Suggested Stop", new Vector3(0, 0, 80));
        block.enemyBounds = new Bounds(new Vector3(23, 4, 80), new Vector3(28, 12, 70));
        Box(root.transform, "Block ground", new Vector3(0, -.5f, 80), new Vector3(96, 1, 160), ground);
        float bend = index % 3 == 0 ? (index % 2 == 0 ? -4f : 4f) : 0;
        block.track = Enumerable.Range(0, 81).Select(i => new Vector3(bend * Mathf.Pow(Mathf.Sin(Mathf.PI * i / 80f), 2), 0, i * 2f)).ToArray();
        block.stop.localPosition = block.track[40]; Track(root.transform, block.track);
        // Distant silhouette markers keep lead-in/out readable without expanding encounter boundaries.
        for (int z = 12; z < 160; z += 24)
        { Box(root.transform, "Trackside post", new Vector3(-10, 2, z), new Vector3(.3f, 4, .3f), steel); }
        Layout(root.transform, index);
        AddLeftSide(root.transform, index);
        Text(root.transform, Names[index].ToUpperInvariant(), new Vector3(15, 4, 45), 2.5f);
        Text(root.transform, "STOP • FIND COAL • REGROUP\nHit coal-carrying zombies to knock coal loose", new Vector3(10, 2.4f, 52), 1.3f);
        // Authored candidate lanes; choose different subsets, never random positions in geometry.
        Physics.SyncTransforms();
        var candidates = RailCoalAuthoring.Candidates(root);
        block.variations = new RailSpawnSet[3];
        for (int v = 0; v < 3; v++)
        {
            var parent = Point(root.transform, "Spawn variation " + v, Vector3.zero);
            var order = RailCoalAuthoring.SpreadBothSides(candidates, v);
            block.variations[v] = new RailSpawnSet {
                coal = Enumerable.Range(0, 6).Select(c => RailCoalAuthoring.Create(parent, "CoalSpawner " + (c + 1), order[c])).ToArray(),
                enemies = new[] { Point(parent, "Enemy A", new Vector3(12, .2f, 50 + v * 4)), Point(parent, "Enemy B", new Vector3(12, .2f, 110 - v * 4)) }
            };
        }
        Bake(block, Id(index));
        RailEconomyBuilder.AddGold(block);
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/" + Id(index) + ".prefab").GetComponent<RailBlock>();
        EditorSceneManager.MarkSceneDirty(scene); // disposable authoring scene, no scene asset required
        return prefab;
    }
    // Additive authoring also lets existing blocks gain a left side without rebuilding scenes or graphs.
    public static void AddLeftSide(Transform root, int index)
    {
        if (root.Find("Left-side buildings") != null) return;
        var left = Point(root, "Left-side buildings", Vector3.zero);
        string[] near = { "Parts store", "Stockroom", "Motel office", "Tool shed", "Dispatch office", "Ticket hall", "Farm workshop", "Site office", "Salvage office", "Lakeside cabin", "Service cabin", "Quarry office" };
        string[] far = { "Maintenance workshop", "Delivery shelter", "Guest rooms", "Saw workshop", "Freight shelter", "Waiting shelter", "Feed barn", "Tool store", "Parts workshop", "Boathouse", "Switchgear shelter", "Equipment shed" };
        var metal = Material("Metal"); var station = Material("Station"); var safety = Material("Safety");
        void Shed(string name, float x, float z, float width, float depth)
        {
            var building = Point(left, name, new Vector3(x, 0, z));
            Box(building, "Back wall", new Vector3(-width / 2, 2, 0), new Vector3(.4f, 4, depth), station);
            Box(building, "End wall A", new Vector3(0, 2, -depth / 2), new Vector3(width, 4, .4f), station);
            Box(building, "End wall B", new Vector3(0, 2, depth / 2), new Vector3(width, 4, .4f), station);
            Box(building, "Roof", new Vector3(0, 4.1f, 0), new Vector3(width, .25f, depth), metal);
            // The open +X face looks toward the train, with clear walk-in access.
            Box(building, "Workbench", new Vector3(-width / 2 + 2, .6f, 0), new Vector3(2, 1.2f, 3), safety);
        }
        Shed(near[index], -24, 62, 16, index % 3 == 0 ? 22 : 18);
        Shed(far[index], index % 2 == 0 ? -26 : -22, 98, 16, index % 3 == 1 ? 22 : 18);
        if (index == 9)
        {
            // Keep the new cabins on dry shoreline and retain the lake/dock silhouette.
            var lake = root.Find("Lake surface");
            lake.localPosition = new Vector3(-40, -.08f, 80); lake.localScale = new Vector3(14, .1f, 65);
            var dock = root.Find("Dock");
            dock.localPosition = new Vector3(-27, .18f, 80); dock.localScale = new Vector3(22, .35f, 4);
        }
    }

    [MenuItem("Duck Rails/Authoring/Add Left-Side Buildings and Coal")]
    public static void AddLeftSides()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Exit Play Mode first.");
        for (int i = 0; i < Names.Length; i++)
        {
            string path = Folder + "/" + Id(i) + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                AddLeftSide(root.transform, i);
                RailCoalAuthoring.PlaceMarkers(root.GetComponent<RailBlock>());
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }
        AssetDatabase.SaveAssets();
        Debug.Log("Added left-side buildings and split coal three per side in all twelve blocks.");
    }
    static void Layout(Transform root, int i)
    {
        void BoxAt(string name, float x, float y, float z, float sx, float sy, float sz, Material material = null) => Box(root, name, new Vector3(x, y, z), new Vector3(sx, sy, sz), material ?? steel);
        void Shed(string name, float x, float z, float sx, float sz)
        {
            BoxAt(name + " back", x + sx / 2, 2, z, .4f, 4, sz, wall);
            BoxAt(name + " end A", x, 2, z - sz / 2, sx, 4, .4f, wall);
            BoxAt(name + " end B", x, 2, z + sz / 2, sx, 4, .4f, wall);
            BoxAt(name + " roof", x, 4.1f, z, sx, .25f, sz);
        }
        switch (i)
        {
            case 0:
                Shed("Repair depot", 26, 80, 20, 44);
                for (int n = 0; n < 4; n++) BoxAt("Workbench", 33, .6f, 64 + n * 10, 3, 1.2f, 3, yellow);
                BoxAt("Service trench", 23, .15f, 80, 2, .3f, 18); break;
            case 1:
                Shed("Store", 26, 80, 20, 36);
                foreach (float x in new[] { 22f, 29f }) foreach (float z in new[] { 70f, 84f }) BoxAt("Shop aisle", x, 1, z, 1.5f, 2, 8);
                BoxAt("Checkout", 18, .65f, 91, 2, 1.3f, 5, yellow); BoxAt("Canopy", 12, 4.2f, 80, 8, .3f, 36); break;
            case 2:
                for (int n = 0; n < 4; n++) Shed("Motel room " + n, 30, 55 + n * 15, 12, 12);
                BoxAt("Courtyard planter", 18, .5f, 80, 4, 1, 15, wall); Text(root, "MOTEL\nVACANCY", new Vector3(17, 5, 49), 2); break;
            case 3:
                for (int n = 0; n < 6; n++)
                { BoxAt("Timber stack", 20 + n % 2 * 12, 1.2f, 58 + n / 2 * 17, 6, 2.4f, 9); BoxAt("Timber top", 20 + n % 2 * 12, 2.6f, 58 + n / 2 * 17, 4, .4f, 8, yellow); }
                Shed("Saw shed", 30, 107, 12, 9); break;
            case 4:
                for (int n = 0; n < 6; n++)
                {
                    float x = 19 + n % 2 * 13, z = 58 + n / 2 * 19;
                    BoxAt("Container wall", x - 2.5f, 1.5f, z, .25f, 3, 12, wall); BoxAt("Container wall", x + 2.5f, 1.5f, z, .25f, 3, 12, wall);
                    BoxAt("Container roof", x, 3, z, 5.3f, .25f, 12);
                }
                Ramp(root, new Vector3(19, 1.4f, 105), new Vector3(4, .3f, 12), -14); break;
            case 5:
                Shed("Waiting room", 30, 73, 12, 28); BoxAt("Opposite platform", 23, .25f, 80, 5, .5f, 60, wall);
                foreach (float z in new[] { 62f, 88f }) BoxAt("Platform bench", 21, .65f, z, 1, .8f, 5, yellow);
                BoxAt("Underpass roof", 18, 3, 104, 15, .4f, 8); BoxAt("Underpass wall", 18, 1.5f, 100, 15, 3, .3f);
                Text(root, "PLATFORM 2", new Vector3(26, 4, 59), 2); break;
            case 6:
                Shed("Barn", 27, 67, 18, 22);
                foreach (float z in new[] { 90f, 105f }) Cylinder(root, "Grain silo", new Vector3(32, 4, z), new Vector3(7, 4, 7));
                for (int n = 0; n < 4; n++) BoxAt("Pen fence", 18 + n * 4, .6f, 99, .2f, 1.2f, 14, yellow); break;
            case 7:
                Shed("Equipment cabin", 31, 61, 10, 10);
                for (int n = 0; n < 5; n++) BoxAt("Concrete barrier", 20 + n % 2 * 9, .65f, 74 + n * 7, 6, 1.3f, 1, wall);
                BoxAt("Tunnel form roof", 27, 5, 95, 18, 1, 16);
                foreach (float x in new[] { 19f, 35f }) BoxAt("Scaffold", x, 2.5f, 95, .4f, 5, 16, yellow); break;
            case 8:
                Shed("Scrap workshop", 30, 108, 12, 10);
                for (int n = 0; n < 8; n++) { float x = 20 + n % 2 * 12, z = 55 + n / 2 * 14; BoxAt("Car body", x, .65f, z, 5, 1.3f, 3); BoxAt("Stacked car", x + .4f, 1.9f, z, 4.5f, 1.2f, 2.8f, wall); } break;
            case 9:
                for (int n = 0; n < 3; n++) Shed("Camp cabin", 30, 56 + n * 20, 10, 12);
                BoxAt("Lake surface", -26, -.08f, 80, 34, .1f, 65, wall); BoxAt("Dock", -13, .18f, 80, 18, .35f, 4, yellow);
                foreach (float z in new[] { 66f, 88f }) BoxAt("Picnic table", 19, .7f, z, 4, 1.4f, 2); break;
            case 10:
                Shed("Control room", 30, 106, 12, 10);
                for (int n = 0; n < 6; n++) { float x = 21 + n % 2 * 11, z = 58 + n / 2 * 16; BoxAt("Transformer", x, 1.2f, z, 4, 2.4f, 5); Cylinder(root, "Insulator", new Vector3(x, 3, z), new Vector3(.8f, .6f, .8f)); }
                BoxAt("Fence back", 38, 1.2f, 80, .2f, 2.4f, 68, yellow); break;
            case 11:
                // Raised terraces imply the quarry rim while preserving a navigable base and walkable ramps.
                BoxAt("Quarry rim", 34, 1.5f, 80, 8, 3, 52, wall); BoxAt("Lower terrace", 25, .75f, 88, 8, 1.5f, 30);
                Ramp(root, new Vector3(25, .7f, 66), new Vector3(6, .3f, 14), -6);
                Ramp(root, new Vector3(34, 1.5f, 49), new Vector3(7, .3f, 18), -10);
                Shed("Conveyor shed", 19, 106, 10, 9); BoxAt("Conveyor", 25, 2, 95, 20, .4f, 2, yellow); break;
        }
    }
    static void Bake(RailBlock block, string name)
    {
        var baker = new GameObject("Authored crawlgraph settings").AddComponent<CrawlGraphBaker>(); baker.transform.SetParent(block.transform, false);
        baker.transform.localPosition = new Vector3(24, 4, 80); baker.size = new Vector3(32, 12, 96); baker.cellSize = 1; baker.agentRadius = .25f;
        baker.surfaceMask = 1 << 6; baker.allowCeilings = false; baker.minIslandNodes = 10; baker.seeds = block.variations.SelectMany(v => v.enemies).ToArray();
        string path = Folder + "/" + name + ".bytes";
        if (!File.Exists(path)) File.WriteAllBytes(path, new byte[8]);
        AssetDatabase.ImportAsset(path); baker.graphAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        Physics.SyncTransforms(); if (!CrawlGraphBake.Bake(baker, false)) throw new InvalidOperationException("Graph bake failed: " + name);
        block.graph = baker.graphAsset; baker.drawNodes = false;
    }
    static void BuildScenes(RailBlock[] catalog)
    {
        var scene = EditorSceneManager.OpenScene(Source);
        var world = Object.FindFirstObjectByType<RailWorld>(); var run = world.GetComponent<RailExpedition>();
        RailHoldControlBuilder.Install(run.bridgeButton.GetComponent<RailControl>());
        RailModularTrainBuilder.InstallPrefab(world, run);
        world.expedition.SetActive(true);
        var env = world.expedition.transform.Find("Week 2 Environment");
        var keep = new[] { "Far bank", "Ravine", "Exit rail", "Bridge control island", "Lower bridge button", "LOWER BRIDGE", "Bridge hinge", "Tunnel roof", "Tunnel wall", "DAWN", "BRIDGE AHEAD", "BRAKE NOW", "Warning post" };
        foreach (Transform child in env.Cast<Transform>().ToArray())
        {
            if (!keep.Contains(child.name)) Object.DestroyImmediate(child.gameObject);
            else child.localPosition -= Vector3.forward * 470;
        }
        // Bake the finale at its prefab-local origin, using the same graph grid as other blocks.
        env.name = "Bridge Finale"; env.position = Vector3.zero;
        Box(env, "Finale approach", new Vector3(0, -.5f, 40), new Vector3(96, 1, 80), ground);
        Track(env, Enumerable.Range(0, 41).Select(i => new Vector3(0, 0, i * 2)).ToArray());
        var finalBlock = env.gameObject.AddComponent<RailBlock>(); finalBlock.id = "bridge-finale-v1"; finalBlock.displayName = "Bridge Finale"; finalBlock.length = 300;
        finalBlock.entry = Point(env, "Track Entry", Vector3.zero); finalBlock.exit = Point(env, "Track Exit", Vector3.forward * 300); finalBlock.stop = Point(env, "Suggested Stop", Vector3.forward * 55);
        finalBlock.track = new[] { Vector3.zero, Vector3.forward * 300 };
        finalBlock.enemyBounds = new Bounds(new Vector3(23, 4, 57), new Vector3(28, 12, 40));
        finalBlock.variations = new[] { new RailSpawnSet { coal = Array.Empty<Transform>(), enemies = new[] { Point(env, "Bridge enemy A", new Vector3(25, .2f, 50)), Point(env, "Bridge enemy B", new Vector3(25, .2f, 70)) } } };
        Bake(finalBlock, "bridge-finale-v1");
        PrefabUtility.SaveAsPrefabAsset(env.gameObject, Folder + "/bridge-finale-v1.prefab");
        env.position = Vector3.forward * 870; run.bridgeZ = 950;
        var routeObject = new GameObject("Authored route"); var route = routeObject.AddComponent<RailRoute>(); var manager = routeObject.AddComponent<EnemyManager>();
        RailStartingStationBuilder.Install(route);
        route.catalog = catalog; route.finale = finalBlock; route.finaleRoot = env; route.expedition = run; route.manager = manager; run.enemies = manager;
        RailEconomyBuilder.InstallScene(route);
        var serialized = new SerializedObject(manager); serialized.FindProperty("surfaceMask").intValue = 1 << 6; serialized.FindProperty("visionMask").intValue = 1 << 6; serialized.FindProperty("maxEnemies").intValue = 0; serialized.ApplyModifiedPropertiesWithoutUndo();
        run.coalPoints = Array.Empty<Transform>(); run.enemyPoints = Array.Empty<Transform>();
        Text(world.train.transform, "FIVE STOPS → BRIDGE → DAWN\nRelease coal into furnace. Hold a hand near a downed duck for 3 seconds.\nBrake at the bridge. Hold the outside button to lower. Release pauses progress.", new Vector3(0, 2.1f, -4.4f), 1.1f).transform.localRotation = Quaternion.Euler(0, 180, 0);
        Object.DestroyImmediate(world.compound); world.compound = null; world.boardingTrigger = null; world.departureBoard = null; world.privateTerminal = null;
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat; RenderSettings.ambientLight = new Color(.36f, .42f, .5f); RenderSettings.fogDensity = .012f;
        EditorSceneManager.SaveScene(scene, "Assets/Scenes/Expedition.unity");
        // Compound keeps the user's social layout and VR terminal, without expedition scenery.
        scene = EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity"); world = Object.FindFirstObjectByType<RailWorld>();
        if (world.GetComponent<RailExpedition>() != null) Object.DestroyImmediate(world.GetComponent<RailExpedition>());
        if (world.expedition != null) Object.DestroyImmediate(world.expedition);
        world.expedition = null; world.train = null;
        if (world.compound.transform.Find("Expedition instructions") == null)
            Text(world.compound.transform, "FIVE RANDOM STOPS + THE BRIDGE\nBring coal back to the furnace. Rescue with a nearby hand.\nPlatform: public departure • Terminal: private crew", new Vector3(8, 3.8f, 8), 1.4f).name = "Expedition instructions";
        EditorSceneManager.SaveScene(scene);
    }
    static void AddCarrier()
    {
        string path = "Assets/Resources/Rails/RailZombie.prefab"; var root = PrefabUtility.LoadPrefabContents(path);
        var carrier = root.GetComponent<RailCoalCarrier>() ?? root.AddComponent<RailCoalCarrier>();
        if (carrier.display == null)
        {
            var enemy = root.GetComponent<Enemy>();
            var coal = Box(enemy.Root != null ? enemy.Root : root.transform, "Carried coal", new Vector3(.35f, .65f, .35f), Vector3.one * .35f, Material("Coal"));
            Object.DestroyImmediate(coal.GetComponent<Collider>()); carrier.display = coal.transform;
            Text(coal.transform, "COAL", Vector3.up * 1.1f, 2);
        }
        PrefabUtility.SaveAsPrefabAsset(root, path); PrefabUtility.UnloadPrefabContents(root);
    }
    static void Track(Transform parent, Vector3[] points)
    {
        for (int i = 1; i < points.Length; i++)
        {
            Vector3 d = points[i] - points[i - 1], centre = (points[i] + points[i - 1]) * .5f;
            Quaternion rotation = Quaternion.LookRotation(d);
            foreach (float x in new[] { -1.5f, 1.5f })
            { var rail = Box(parent, "Track rail", centre + rotation * Vector3.right * x + Vector3.up * .06f, new Vector3(.12f, .12f, d.magnitude + .02f), steel); rail.transform.localRotation = rotation; Object.DestroyImmediate(rail.GetComponent<Collider>()); }
            if (i % 2 == 0) { var sleeper = Box(parent, "Sleeper", centre + Vector3.up * .02f, new Vector3(3.8f, .04f, .35f), steel); sleeper.transform.localRotation = rotation; Object.DestroyImmediate(sleeper.GetComponent<Collider>()); }
        }
    }
    static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cube); go.name = name; go.layer = 6; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = material; return go; }
    static void Cylinder(Transform parent, string name, Vector3 position, Vector3 size)
    { var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder); go.name = name; go.layer = 6; go.transform.SetParent(parent, false); go.transform.localPosition = position; go.transform.localScale = size; go.GetComponent<Renderer>().sharedMaterial = steel; }
    static void Ramp(Transform parent, Vector3 p, Vector3 size, float pitch) => Box(parent, "Access ramp", p, size, yellow).transform.localRotation = Quaternion.Euler(pitch, 0, 0);
    static Transform Point(Transform parent, string name, Vector3 p)
    { var t = new GameObject(name).transform; t.SetParent(parent, false); t.localPosition = p; return t; }
    static TMP_Text Text(Transform parent, string text, Vector3 position, float size)
    { var label = new GameObject(text.Split('\n')[0]).AddComponent<TextMeshPro>(); label.transform.SetParent(parent, false); label.transform.localPosition = position; label.text = text; label.fontSize = size; label.alignment = TextAlignmentOptions.Center; label.rectTransform.sizeDelta = new Vector2(15, 4); return label; }
}
#endif
