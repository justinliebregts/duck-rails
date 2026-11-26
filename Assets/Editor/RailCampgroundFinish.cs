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
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Adds the approved Pine Lake exterior to the existing walkable campground and bake scene.</summary>
public static class RailCampgroundFinish
{
    public const string RootName = "Pine Lake finish";
    public const int LightCount = 19;
    public const float FloorLift = .14f;
    public const float FireScale = 2, FireLightLift = .65f, FireLightIntensity = 16, FireLightRange = 12;
    const string FirePrefab = "Assets/Plugins/PolygonParticleFX/Prefabs/FX_Fire_Small_01.prefab";
    public const string Folder = RailCampgroundGreybox.Folder + "/Finish";
    const string ModelPath = Folder + "/Lakeside finish.fbx";
    const string AtlasPath = Folder + "/Lakeside atlas.mat";
    const string FernMaterialPath = Folder + "/Lakeside fern leaves.mat";

    [Serializable] class Layout { public Label[] labels; public Fixture[] fixtures; public Trail[] trails; public Vector4[] ferns; }
    [Serializable] class Trail { public Vector3[] points; }
    static Layout ReadLayout() => JsonUtility.FromJson<Layout>(File.ReadAllText("ArtSource/CampgroundFinish/fixtures.json"));
    public static Vector3[][] FinishedTrails() => ReadLayout().trails.Select(t=>t.points).ToArray();
    [Serializable] class Label { public string text; public float x, y, z, size, yaw; }
    [Serializable] class Fixture { public string name; public float x, y, z, power, yaw; }

    [MenuItem("Duck Rails/Authoring/Campground/Install approved Pine Lake exterior")]
    public static void Install()
    {
        if (EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish the current play session or bake before installing the exterior.");

        PrepareAtlas();
        var prefab = PrefabUtility.LoadPrefabContents(RailCampgroundGreybox.PrefabPath);
        try
        {
            var source = RailBlock.FindEnvironment(prefab.transform,RailCampgroundGreybox.RootName);
            if (!source) throw new InvalidOperationException("The campground terrain shell is missing.");
            var previous = source.Find(RootName);
            bool refined = previous && previous.Find("Fire pit");
            if (previous) Object.DestroyImmediate(previous.gameObject);
            if (!refined) RefineShell(prefab.transform, source);
            RefineCampsites(source);
            FinishShellSurfaces(prefab.transform);
            var additions = new GameObject(RootName).transform;
            additions.SetParent(source, false);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, additions);
            instance.transform.localRotation = Quaternion.Euler(0,180,0);
            var fernMaterial=AssetDatabase.LoadAssetAtPath<Material>(FernMaterialPath);
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                if (!mesh || !renderer.sharedMaterial || mesh.uv2.Length != mesh.vertexCount)
                    throw new InvalidOperationException("Missing finish mesh or lighting UVs on " + renderer.name);
                var go = renderer.gameObject;
                go.layer = 6;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.ContributeGI);
                bool plants = go.name.StartsWith("Fern thicket") || go.name.StartsWith("Horsetail");
                if (plants)
                {
                    if (go.name.StartsWith("Fern thicket")) renderer.sharedMaterial=fernMaterial;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                renderer.receiveGI = plants ? ReceiveGI.LightProbes : ReceiveGI.Lightmaps;
                renderer.scaleInLightmap = .5f;
                if (go.name.EndsWith("_solid", StringComparison.Ordinal)) go.AddComponent<MeshCollider>().sharedMesh = mesh;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go);
            }
            foreach(float x in new[]{-47f,47f})
            {
                var rail = new GameObject(x < 0 ? "West fence collision" : "East fence collision");
                rail.layer = 6;
                rail.transform.SetParent(additions,false);
                rail.transform.localPosition = new Vector3(x,.78f,80);
                rail.AddComponent<BoxCollider>().size = new Vector3(.18f,1.56f,156.18f);
                rail.AddComponent<Game.Audio.AudioSurface>().type = AssetDatabase.LoadAssetAtPath<Game.Audio.SurfaceType>("Assets/Audio/Surfaces/Wood.asset");
            }
            var layout = ReadLayout();
            var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            foreach (var text in source.GetComponentsInChildren<TMP_Text>(true))
            {
                text.font = font;
                text.fontSharedMaterial = font.material;
                text.ForceMeshUpdate();
            }
            foreach (var sign in layout.labels)
            {
                var go = new GameObject(sign.text);
                go.transform.SetParent(additions, false);
                go.transform.localPosition = new Vector3(sign.x, sign.y, sign.z);
                var label = go.AddComponent<TextMeshPro>();
                label.font = font;
                label.fontSharedMaterial = font.material;
                label.alignment = TextAlignmentOptions.Center;
                label.textWrappingMode = TextWrappingModes.NoWrap;
                bool crossing = sign.text.Contains("TRAINS");
                label.rectTransform.sizeDelta = crossing ? new Vector2(1.55f,.80f) : new Vector2(4.1f,1.15f);
                label.transform.localRotation = Quaternion.Euler(0,sign.yaw,0);
                label.enableAutoSizing = true;
                label.fontSizeMin = 1;
                label.fontSizeMax = 6;
                label.text = sign.text;
                label.color = crossing ? new Color(.12f,.10f,.075f) : new Color(1,.9f,.72f);
                label.ForceMeshUpdate();
            }
            foreach (var fixture in layout.fixtures)
            {
                var go = new GameObject(fixture.name);
                go.transform.SetParent(additions, false);
                go.transform.localPosition = new Vector3(fixture.x,fixture.y,fixture.z);
                var light = go.AddComponent<Light>();
                bool fire = fixture.name == "Fire pit";
                bool solar = fixture.name == "Solar stake";
                bool tipped = fixture.name == "Dropped lantern";
                light.type = tipped ? LightType.Spot : LightType.Point;
                if (tipped)
                {
                    light.transform.localRotation = Quaternion.Euler(-5,fixture.yaw,0);
                    light.spotAngle = 125;
                    light.innerSpotAngle = 74;
                }
                light.lightmapBakeType = LightmapBakeType.Baked;
                light.color = fire ? new Color(1,.53f,.24f) : solar ? new Color(.75f,.87f,.82f) : new Color(1,.78f,.51f);
                light.intensity = fixture.power * .041f;
                light.range = fire ? 11 : solar ? 4 : 7;
                light.shadows = LightShadows.Soft;
                if (tipped) AddLanternFill(light);
                if (fire) AddCampfire(light);
            }
            RailCampgroundAudio.Configure(prefab,additions,layout.ferns);
            PrefabUtility.SaveAsPrefabAsset(prefab,RailCampgroundGreybox.PrefabPath,out bool success);
            if (!success) throw new IOException("Could not save the Lakeside Campground prefab.");
        }
        finally { PrefabUtility.UnloadPrefabContents(prefab); }
        AssetDatabase.SaveAssets();
    }

    static void AddCampfire(Light light)
    {
        // The fixture marker sits inside the crossed logs, which would block the whole bake.
        light.transform.localPosition += Vector3.up*FireLightLift;
        light.color = new Color(1,.55f,.22f);
        light.intensity = FireLightIntensity;
        light.range = FireLightRange;
        light.shadowRadius = .3f;
        var flame = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(FirePrefab),light.transform);
        flame.name = "Burning firewood";
        flame.transform.localPosition = Vector3.down*(.36f+FireLightLift);
        flame.transform.localScale = Vector3.one*FireScale;
        foreach (var particles in flame.GetComponentsInChildren<ParticleSystem>(true))
        {
            // The imported effect ignores parent scale until it follows the hierarchy.
            var main = particles.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            PrefabUtility.RecordPrefabInstancePropertyModifications(particles);
        }
        PrefabUtility.RecordPrefabInstancePropertyModifications(flame.transform);
    }

    static void AddLanternFill(Light spot)
    {
        var fill = new GameObject("Lantern fill").AddComponent<Light>();
        fill.transform.SetParent(spot.transform, false);
        // The spotlight sits beyond the lens. Position the fill over the lantern body.
        fill.transform.localPosition = new Vector3(0, .48f, -.46f);
        fill.type = LightType.Point;
        fill.lightmapBakeType = LightmapBakeType.Baked;
        fill.color = spot.color;
        fill.intensity = spot.intensity * .22f;
        fill.range = 4.5f;
        fill.shadows = LightShadows.Soft;
    }

    static void RefineShell(Transform block, Transform shell)
    {
        var identity = shell.Find("Expanded campground V2/Campground signs and furnishings");
        foreach (var t in identity.Cast<Transform>().ToArray())
        {
            var label = t.GetComponent<TMP_Text>();
            if (t.name == "Sign post" || t.name == "Entrance sign board" || (label && label.text.Contains("LAKESIDE")))
                Object.DestroyImmediate(t.gameObject);
        }
        var oldScenery = new[] {"Trackside post","Pine silhouette","Baked campground lamp","Timber wall",
            "Bed","Workbench","Shelf","Canoe seat","Canoe hull","Rack upright","Rack arm","Rack spine"};
        foreach (var t in block.GetComponentsInChildren<Transform>(true).Reverse())
        {
            if (oldScenery.Contains(t.name) || t.name.StartsWith("Gravel path ",StringComparison.Ordinal))
                Object.DestroyImmediate(t.gameObject);
        }
        foreach (var slab in block.GetComponentsInChildren<Transform>().Where(t=>t.name=="Foundation"))
            slab.parent.localPosition += Vector3.up*FloorLift;
    }

    static void RefineCampsites(Transform shell)
    {
        var expansion=shell.Find("Expanded campground V2");
        var northeast=expansion.Find("Northeast woodland pitches");
        var southeast=expansion.Find("Southeast camper pitch");
        foreach (var area in new[]{northeast,southeast})
        {
            foreach (var child in area.Cast<Transform>().ToArray())
            {
                if (child.name.StartsWith("Folding chair") || child.name=="Chair frame" ||
                    (area==northeast && child.name=="Cooler") || (area==southeast && child.name=="Picnic table"))
                    Object.DestroyImmediate(child.gameObject);
            }
        }
        var oldTent=expansion.Find("Southwest tent clearing/Damaged tent");
        if(oldTent) Object.DestroyImmediate(oldTent.gameObject);
        foreach (var oldRock in expansion.Find("Rocky overlook").Cast<Transform>().Where(t=>t.name=="Overlook boulder").ToArray())
            Object.DestroyImmediate(oldRock.gameObject);
        foreach(var t in expansion.GetComponentsInChildren<Transform>().Where(t=>t.name=="Tent").ToArray())
            Object.DestroyImmediate(t.gameObject);
        foreach(var t in shell.Cast<Transform>().Where(t=>t.name=="Fire ring stone").ToArray())
            Object.DestroyImmediate(t.gameObject);
        var identity=expansion.Find("Campground signs and furnishings");
        foreach(var t in identity.Cast<Transform>().ToArray())
        {
            var label=t.GetComponent<TMP_Text>();
            if(t.name=="Trail map board" || t.name=="Wayfinding post" || t.name=="Wayfinding board" ||
                (label && label.text.IndexOf("TRAIL",StringComparison.OrdinalIgnoreCase)>=0) ||
                (t.name=="Rubbish bin" && Vector2.Distance(new Vector2(t.localPosition.x,t.localPosition.z),new Vector2(25,54))<.1f))
                Object.DestroyImmediate(t.gameObject);
        }
        // These small signs have separate meshes and labels in block coordinates.
        ShiftSign(expansion.Find("Canoe beach"),new Vector2(-16,114),new Vector2(-24,117));
        ShiftSign(expansion.Find("Southeast camper pitch"),new Vector2(38,31),new Vector2(20.6f,37.2f));
        foreach(var board in expansion.GetComponentsInChildren<Transform>().Where(t=>t.name=="Wayfinding board"))
        {
            var post=board.parent.Find("Wayfinding post");
            if(!post) continue;
            var mount=board.position+board.forward*.12f;
            post.position=new Vector3(mount.x,post.position.y,mount.z);
        }
        var picnic=expansion.Find("Northeast woodland pitches/Picnic table");
        if(picnic) picnic.localPosition=new Vector3(6.5f,0,-2);
        var terrain=shell.Find("Sculpted ground").GetComponent<MeshCollider>();
        float Ground(Vector3 p)
        {
            if (!terrain.Raycast(new Ray(p + Vector3.up * 10, Vector3.down), out var hit, 24))
                throw new InvalidOperationException("Campground support outside terrain at " + p);
            return hit.point.y;
        }
        var camper=expansion.Find("Southeast camper pitch/Abandoned camper");
        var wheel=camper.Find("Wheel").GetComponent<BoxCollider>().bounds;
        camper.position+=Vector3.up*(Ground(wheel.center)-wheel.min.y);
        foreach (var leg in picnic.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Table trestle"))
        {
            var b=leg.bounds;
            float bottom=Mathf.Min(Ground(b.center + Vector3.forward * b.extents.z),
                Ground(b.center - Vector3.forward * b.extents.z))-.02f;
            var t=leg.transform;
            var size=t.localScale;
            size.y=b.max.y-bottom;
            t.localScale=size;
            t.position+=Vector3.up*((bottom-b.min.y)*.5f);
        }
        // The fallen trunk rests across the crushed tent, with its downhill end on the soil.
        var wreck=expansion.Find("Fallen tree scene");
        var trunk=wreck.Find("Fallen trunk").GetComponent<BoxCollider>().bounds;
        wreck.position+=Vector3.up*(Ground(trunk.center)-trunk.min.y);
    }

    static void ShiftSign(Transform area, Vector2 original, Vector2 destination)
    {
        var shift=destination-original;
        float lift=RailCampgroundGreybox.Height(destination.x,destination.y)-RailCampgroundGreybox.Height(original.x,original.y);
        foreach(var child in area.Cast<Transform>())
        {
            if(child.name!="Wayfinding post" && child.name!="Wayfinding board" && child.name!="Sign lettering")continue;
            var p=child.localPosition;
            if(Vector2.Distance(new Vector2(p.x,p.z),original)<.3f)
                child.localPosition+=new Vector3(shift.x,lift,shift.y);
        }
    }

    static void FinishShellSurfaces(Transform shell)
    {
        var variants = new System.Collections.Generic.Dictionary<Material, Material>();
        foreach (var renderer in shell.GetComponentsInChildren<MeshRenderer>(true))
        {
            var surfaces = renderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var original = surfaces[i];
                if (!original || !AssetDatabase.GetAssetPath(original).StartsWith(RailCampgroundGreybox.Folder + "/Quackville ")) continue;
                if (!variants.TryGetValue(original, out var finished))
                {
                    string name = original.name.Substring("Quackville ".Length);
                    string path = Folder + "/Surfaces/Pine Lake " + name;
                    var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path + ".png");
                    if (!texture) throw new InvalidOperationException("Missing Pine Lake finish texture " + name);
                    finished = AssetDatabase.LoadAssetAtPath<Material>(path + ".mat");
                    if (!finished) { finished = new Material(original); AssetDatabase.CreateAsset(finished,path + ".mat"); }
                    finished.SetTexture("_BaseMap", texture);
                    finished.SetColor("_BaseColor", Color.white);
                    finished.SetFloat("_Smoothness",name == "lake water" ? .28f : .16f);
                    EditorUtility.SetDirty(finished);
                    variants.Add(original, finished);
                }
                surfaces[i] = finished;
                changed = true;
            }
            if (changed) renderer.sharedMaterials = surfaces;
        }
    }
    static void PrepareAtlas()
    {
        var atlas = AssetDatabase.LoadAssetAtPath<Material>(AtlasPath);
        if (!atlas)
        {
            atlas = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(atlas, AtlasPath);
        }
        atlas.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Campground color.png"));
        atlas.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Campground emission.png"));
        atlas.SetColor("_EmissionColor", Color.white * 3.5f);
        atlas.SetFloat("_Smoothness", .12f);
        atlas.EnableKeyword("_EMISSION");
        atlas.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        EditorUtility.SetDirty(atlas);
        var fern=AssetDatabase.LoadAssetAtPath<Material>(FernMaterialPath);
        if (!fern)
        {
            fern=new Material(atlas);
            AssetDatabase.CreateAsset(fern,FernMaterialPath);
        }
        fern.CopyPropertiesFromMaterial(atlas);
        fern.SetFloat("_Cull",(float)CullMode.Off);
        fern.doubleSidedGI=true;
        EditorUtility.SetDirty(fern);
        var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
        var materialKey = new AssetImporter.SourceAssetIdentifier(typeof(Material),"Lakeside atlas");
        if (!importer.importAnimation && !importer.importLights && !importer.importCameras && importer.isReadable &&
            importer.generateSecondaryUV && importer.GetExternalObjectMap().TryGetValue(materialKey,out var mapped) && mapped == atlas) return;
        importer.importAnimation = false;
        importer.importLights = false;
        importer.importCameras = false;
        importer.isReadable = true;
        importer.generateSecondaryUV = true;
        importer.AddRemap(materialKey,atlas);
        importer.SaveAndReimport();
    }
}
#endif
