#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One-time, editable subway art kit. Reinstallation references the saved prefab, never regenerates it.</summary>
public static class RailSubwayCompoundBuilder
{
    public const string Folder = "Assets/DuckRails/Compound/SubwayV2";
    public const string PrefabPath = Folder + "/SubwayCompound.prefab";
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    static Material tile, concrete, metal, rust, wood, floor, red, dark, water, glow, meshScreen, edge;

    [MenuItem("Duck Rails/Compound/Create Subway Blockout")]
    public static void Create()
    {
        RequireEditMode();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null)
            throw new InvalidOperationException("Subway prefab already exists. Edit its modules in Prefab Mode; creation will not overwrite your work.");
        Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory(Folder + "/Meshes");
        Directory.CreateDirectory(Folder + "/Modules");
        AssetDatabase.Refresh(); meshes.Clear();
        tile = Mat("Old tile", new Color(.52f,.5f,.42f), 0);
        concrete = Mat("Concrete", new Color(.28f,.29f,.27f), 1);
        metal = Mat("Corrugated steel", new Color(.29f,.33f,.33f), 2);
        rust = Mat("Rusty sheets", new Color(.33f,.19f,.12f), 2);
        wood = Mat("Salvaged timber", new Color(.32f,.23f,.14f), 3);
        floor = Mat("Floor tiles", new Color(.31f,.33f,.31f), 0);
        red = Mat("Faded red stripe", new Color(.28f,.09f,.06f), 0);
        dark = Mat("Dark metal", new Color(.055f,.07f,.075f), 1);
        water = Mat("Shallow water", new Color(.10f,.20f,.20f), 4);
        glow = Mat("Searchlight lens", new Color(.7f,.8f,.85f), 1);
        glow.EnableKeyword("_EMISSION"); glow.SetColor("_EmissionColor", new Color(1.3f,1.5f,1.7f));
        edge = Mat("Worn platform edge", new Color(.58f,.46f,.18f), 1);
        meshScreen = ScreenMaterial();
        var root = new GameObject("Subway compound — editable blockout");
        try
        {
            Shell(root.transform); Stairs(root.transform); Fountain(root.transform);
            Station(root.transform); Supplies(root.transform); Departure(root.transform); Fixtures(root.transform);
            // Each major section is a separately editable nested prefab.
            foreach (Transform child in root.transform.Cast<Transform>().ToArray())
                PrefabUtility.SaveAsPrefabAssetAndConnect(child.gameObject, Folder + "/Modules/" + child.name + ".prefab", InteractionMode.AutomatedAction);
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        AssetDatabase.SaveAssets();
        Install();
    }

    [MenuItem("Duck Rails/Compound/Install Saved Subway Blockout")]
    public static void Install()
    {
        RequireEditMode();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) throw new InvalidOperationException("Save current scene edits before installing.");
        if (scene.path != "Assets/Scenes/Compound.unity") scene = EditorSceneManager.OpenScene("Assets/Scenes/Compound.unity");
        var world = UnityEngine.Object.FindAnyObjectByType<RailWorld>();
        InstallExisting(world);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject = world.compound.transform.Find(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath).name).gameObject;
    }

    // Called by the legacy scene authoring path too: artist-edited prefab is preserved.
    public static void InstallExisting(RailWorld world)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (prefab == null) return;
        if (world == null || world.compound == null || world.boardingTrigger == null || world.departureBoard == null)
            throw new InvalidOperationException("Compound gameplay bindings are missing.");
        var parent = world.compound.transform;
        var previous = parent.Find(prefab.name);
        if (previous != null && PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(previous.gameObject) == "Assets/DuckRails/Compound/Subway/SubwayCompound.prefab")
            UnityEngine.Object.DestroyImmediate(previous.gameObject); // V1 assets remain intact as the fallback.
        if (parent.Find(prefab.name) == null) PrefabUtility.InstantiatePrefab(prefab, parent);
        foreach (string name in new[] { "Compound floor", "Station back wall", "Station canopy", "Canopy pillar", "Public boarding platform", "Title", "Private instructions", "Controls", "Expedition instructions" })
            foreach (Transform child in parent) if (child.name == name) child.gameObject.SetActive(false);
        // Keep the same gameplay components and persistent UnityEvents. Only move their presentation.
        world.boardingTrigger.transform.position = new Vector3(0, 1.4f, 6.8f);
        world.boardingTrigger.center = Vector3.zero; world.boardingTrigger.size = new Vector3(6, 3, 3.6f);
        world.departureBoard.transform.position = new Vector3(0, 1.5f, 8.66f);
        world.departureBoard.transform.rotation = Quaternion.identity;
        world.departureBoard.fontSize = 6;
        world.departureBoard.rectTransform.sizeDelta = new Vector2(5.5f, 2.1f);
        world.departureBoard.enableAutoSizing = true;
        world.departureBoard.fontSizeMin = 1; world.departureBoard.fontSizeMax = 6;
        world.privateTerminal.position = new Vector3(-4.9f, .8f, 8.3f);
        world.privateTerminal.localScale = new Vector3(2.5f, 1.6f, .4f);
        var keypad = parent.Find("Private keypad");
        if (keypad != null) { keypad.position = new Vector3(-4.9f,-.26f,8.04f); keypad.rotation = Quaternion.identity; }
        world.compoundSpawn.position = new Vector3(0,.2f,-6.7f);
        var gm = UnityEngine.Object.FindAnyObjectByType<GameManager>();
        if (gm != null) { gm.respawnPoint = world.compoundSpawn; EditorUtility.SetDirty(gm); }
        var moon = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(l => l.gameObject.name == "Moonlight");
        if (moon != null) moon.gameObject.SetActive(false);
        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = new Color(.21f,.23f,.27f);
        RenderSettings.ambientIntensity = 1;
        RenderSettings.fog = true; RenderSettings.fogColor = new Color(.025f,.035f,.045f);
        RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogDensity = .012f;
        EditorUtility.SetDirty(world);
    }

    static void RequireEditMode()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Exit Play Mode first.");
        if (!Application.dataPath.Replace('\\','/').EndsWith("/duck-rails/Assets", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("This authoring tool is scoped to Duck Rails.");
    }
    static Transform Group(string name, Transform parent, Vector3 pos = default, float yaw = 0)
    {
        var t = new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=pos; t.localRotation=Quaternion.Euler(0,yaw,0); return t;
    }
    static GameObject Box(string name, Transform parent, Vector3 pos, Vector3 size, Material mat, bool solid = true)
    {
        var go = new GameObject(name); go.layer = LayerMask.NameToLayer("Environment");
        go.transform.SetParent(parent,false); go.transform.localPosition=pos;
        go.AddComponent<MeshFilter>().sharedMesh = BoxMesh(size);
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        if(solid) go.AddComponent<BoxCollider>().size=size;
        go.isStatic=true; return go;
    }
    static Mesh BoxMesh(Vector3 s)
    {
        string key=string.Format(System.Globalization.CultureInfo.InvariantCulture,"box-{0:0.###}-{1:0.###}-{2:0.###}",s.x,s.y,s.z);
        if(meshes.TryGetValue(key,out var found)) return found;
        string path=Folder+"/Meshes/"+key+".asset";
        found=AssetDatabase.LoadAssetAtPath<Mesh>(path); if(found!=null) { meshes[key]=found; return found; }
        var v=new List<Vector3>(); var uv=new List<Vector2>(); var tris=new List<int>();
        Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
        foreach(var n in normals)
        {
            Vector3 u=Mathf.Abs(n.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,n);
            Vector3 b=Vector3.Cross(n,u);
            Vector3 center=Vector3.Scale(n,s)*.5f, a=Vector3.Scale(u,s)*.5f, c=Vector3.Scale(b,s)*.5f;
            int o=v.Count; v.Add(center-a-c); v.Add(center+a-c); v.Add(center+a+c); v.Add(center-a+c);
            uv.Add(Vector2.zero); uv.Add(new Vector2(a.magnitude*2,0)); uv.Add(new Vector2(a.magnitude*2,c.magnitude*2)); uv.Add(new Vector2(0,c.magnitude*2));
            tris.AddRange(new[]{o,o+1,o+2,o,o+2,o+3});
        }
        var mesh=new Mesh{name=key}; mesh.SetVertices(v); mesh.SetUVs(0,uv); mesh.SetTriangles(tris,0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,path); meshes[key]=mesh; return mesh;
    }
    static Material Mat(string name,Color color,int style)
    {
        const int n=64; var tex=new Texture2D(n,n,TextureFormat.RGB24,false); var pixels=new Color[n*n];
        var rng=new System.Random(317+style); // Repeatable coarse texels, not noisy high-res detail.
        for(int y=0;y<n;y++) for(int x=0;x<n;x++)
        {
            float shade=.8f+(float)rng.NextDouble()*.25f;
            if(style==0 && (x%16==0 || y%16==0)) shade=.35f;
            if(style==0 && ((x/16*11+y/16*7)%9==0)) shade*=.72f;
            if(style==2) shade*=x%8<2?.5f:1.1f;
            if(style==2 && ((x/7+y/5*3)%7==0)) shade*=.65f;
            if(style==3) shade*=x%12<2?.45f:1f;
            if(style==4) shade=.8f+(((x/4+y/2)%11==0)?.35f:0);
            pixels[y*n+x]=color*shade;
        }
        tex.SetPixels(pixels); tex.Apply(); string path=Folder+"/Materials/"+name+".png";
        File.WriteAllBytes(path,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path); importer.filterMode=FilterMode.Point; importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=true; importer.textureCompression=TextureImporterCompression.Uncompressed; importer.SaveAndReimport();
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path)); mat.SetFloat("_Smoothness",style==4?.45f:.05f);
        AssetDatabase.CreateAsset(mat,Folder+"/Materials/"+name+".mat"); return mat;
    }
    static void Label(Transform parent,string text,Vector3 pos,Vector2 size,float font=2)
    {
        var go=new GameObject(text); go.transform.SetParent(parent,false); go.transform.localPosition=pos;
        var t=go.AddComponent<TextMeshPro>(); t.text=text; t.fontSize=font; t.alignment=TextAlignmentOptions.Center;
        t.color=new Color(.8f,.79f,.67f); t.rectTransform.sizeDelta=size;
        t.enableAutoSizing=true; t.fontSizeMin=1; t.fontSizeMax=font;
        t.textWrappingMode=TextWrappingModes.NoWrap;
    }
    static void Sign(Transform parent,string text,Vector3 pos,float width)
    {
        pos += Vector3.back * .3f; // Keep lettering in front of the projecting sheet-metal canopy.
        Box(text+" sign",parent,pos,new Vector3(width,.65f,.12f),dark,false);
        Label(parent,text,pos+Vector3.back*.075f,new Vector2(width-.1f,.6f),12);
    }
    static Material ScreenMaterial()
    {
        // Actual cutout wire mesh behind the low scrap panels closes the climbing route to the ceiling.
        var tex=new Texture2D(64,64,TextureFormat.RGBA32,false); var pixels=new Color[4096];
        for(int y=0;y<64;y++) for(int x=0;x<64;x++)
            pixels[y*64+x]=(x%16==0 || y%16==0)?new Color(.19f,.22f,.23f,1):Color.clear;
        tex.SetPixels(pixels); tex.Apply(); string path=Folder+"/Materials/Boarding safety mesh.png";
        File.WriteAllBytes(path,tex.EncodeToPNG()); UnityEngine.Object.DestroyImmediate(tex); AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.filterMode=FilterMode.Point; importer.wrapMode=TextureWrapMode.Repeat;
        importer.mipmapEnabled=false; importer.alphaIsTransparency=true; importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Boarding safety mesh"};
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        mat.SetFloat("_AlphaClip",1); mat.SetFloat("_Cutoff",.5f); mat.SetFloat("_Cull",0);
        mat.SetColor("_BaseColor",new Color(.35f,.35f,.35f,1)); mat.SetFloat("_Smoothness",0);
        mat.SetFloat("_SpecularHighlights",0); mat.SetFloat("_EnvironmentReflections",0);
        mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
        mat.EnableKeyword("_ALPHATEST_ON"); mat.renderQueue=2450; mat.SetOverrideTag("RenderType","TransparentCutout");
        AssetDatabase.CreateAsset(mat,Folder+"/Materials/Boarding safety mesh.mat"); return mat;
    }
    static void Shell(Transform parent)
    {
        var t=Group("01 Terminal shell",parent);
        Box("Continuous walkable floor",t,new Vector3(0,-.2f,-1),new Vector3(18,.4f,24),floor);
        Box("High original station ceiling",t,new Vector3(0,7.2f,-1),new Vector3(25,.4f,24),concrete);
        Box("Rear departure wall",t,new Vector3(0,3.5f,10.85f),new Vector3(25,7,.3f),tile);
        Box("Surface boundary wall",t,new Vector3(0,3.5f,-12.85f),new Vector3(25,7,.3f),concrete);
        int pillar=1;
        foreach(float z in new[]{5.5f,-.8f,-7.1f})
        {
            Box("Original high cross beam",t,new Vector3(0,6.6f,z),new Vector3(24.5f,.55f,.65f),concrete);
            foreach(float x in new[]{-3.4f,3.4f})
            {
                Box("P"+(pillar++)+" structural pillar",t,new Vector3(x,3.3f,z),new Vector3(.65f,6.6f,.65f),tile);
                Box("Column red band",t,new Vector3(x,1.75f,z),new Vector3(.67f,.3f,.67f),red,false);
            }
        }
        foreach(float side in new[]{-1f,1f})
        {
            var track=Group(side<0?"Left inaccessible track audio anchor":"Right inaccessible track audio anchor",t,new Vector3(side*10.45f,0,-1));
            Box("Recessed ballast bed",track,new Vector3(0,-1.15f,0),new Vector3(2.9f,.3f,24),dark);
            Box("Outer tunnel wall",track,new Vector3(side*1.7f,2.95f,0),new Vector3(.5f,8.1f,24),concrete);
            foreach(float railX in new[]{-.65f,.65f}) Box("Rail below boarding edge",track,new Vector3(railX,-.86f,0),new Vector3(.09f,.18f,24),metal,false);
            for(int z=-11;z<=11;z++) Box("Rail sleeper",track,new Vector3(0,-.99f,z),new Vector3(2,.1f,.16f),wood,false);
            Box("Original platform lip",t,new Vector3(side*8.85f,-.05f,-1),new Vector3(.3f,.15f,24),concrete);
            Box("Worn boarding edge stripe",t,new Vector3(side*8.55f,.012f,-1),new Vector3(.15f,.02f,24),edge,false);
            Box("Continuous see-through track guard",t,new Vector3(side*8.98f,3.5f,-1),new Vector3(.035f,7,24),meshScreen);
            for(int bay=0;bay<8;bay++)
            {
                float z=-11.5f+bay*3;
                Box("Original boarding bay post",t,new Vector3(side*9,3.5f,z),new Vector3(.17f,7,.17f),dark);
                if(bay==2 || bay==3 || bay==5)
                {
                    Box("Low boarding barricade base",t,new Vector3(side*8.65f,.1f,z+1.4f),new Vector3(.13f,.2f,2.85f),wood);
                    foreach(float offset in new[]{-.76f,.76f})
                        Box("Salvaged sheet beside narrow viewing slit",t,new Vector3(side*8.65f,1.43f,z+1.4f+offset),new Vector3(.13f,2.46f,1.35f),bay%2==0?metal:rust);
                }
                else Box("Mismatched low boarding barricade",t,new Vector3(side*8.65f,1.3f,z+1.4f),new Vector3(.13f,2.6f,2.85f),bay%2==0?metal:rust);
                var brace=Box("Boarding barricade timber brace",t,new Vector3(side*8.55f,1.5f,z+1.4f),new Vector3(.12f,.13f,2.8f),wood,false);
                brace.transform.localRotation=Quaternion.Euler(bay%2==0?24:-18,0,0);
            }
            Box("Original overhead pipe",t,new Vector3(side*6.9f,6.3f,-1),new Vector3(.2f,.2f,23),rust,false);
            var trackSign=Group("Original track identification",t,new Vector3(side*8.4f,2.95f,-3),side<0?-90:90);
            Sign(trackSign,side<0?"TRACK 1 - CLOSED":"TRACK 2 - CLOSED",Vector3.zero,3.2f);
        }
    }
    static void Stairs(Transform parent)
    {
        var t=Group("02 Sealed surface stairs",parent);
        // Stairwell sits behind spawn. Nothing inside the playable stairs is a hidden teleport.
        foreach(float x in new[]{-6.35f,6.35f}) Box("Stairwell front partition",t,new Vector3(x,1.4f,-9),new Vector3(5.3f,2.8f,.3f),tile);
        foreach(float x in new[]{-3.8f,3.8f}) Box("Stair side wall",t,new Vector3(x,2,-10.7f),new Vector3(.4f,4,3.8f),tile);
        for(int i=0;i<10;i++) Box("Step "+i,t,new Vector3(0,.09f*(i+1),-9.2f-i*.32f),new Vector3(7.2f,.18f*(i+1),.32f),concrete);
        Box("Impassable barricade backing",t,new Vector3(0,4.3f,-12.4f),new Vector3(7.2f,5.4f,.3f),dark);
        for(int i=0;i<6;i++)
        {
            var p=Box("Salvaged barricade panel",t,new Vector3(-3+i*1.2f,3.15f,-12.15f),new Vector3(1.24f,2.9f,.12f),i%2==0?metal:rust);
            p.transform.localRotation=Quaternion.Euler(0,0,(i%3-1)*4);
        }
        for(int i=0;i<3;i++) Crate(t,new Vector3(-2.4f+i*2.1f,1.82f,-11.6f),.8f);
        var brace=Box("Diagonal emergency brace",t,new Vector3(0,3.2f,-11.95f),new Vector3(7,.18f,.16f),wood);
        brace.transform.localRotation=Quaternion.Euler(0,0,18);
        foreach(float x in new[]{-3.2f,3.2f})
        {
            var rail=Box("Stair handrail",t,new Vector3(x,1.7f,-10.6f),new Vector3(.08f,.08f,3.6f),metal,false);
            rail.transform.localRotation=Quaternion.Euler(29,0,0);
        }
    }
    static void Fountain(Transform parent)
    {
        var t=Group("03 Broken shallow fountain",parent,Vector3.zero);
        Box("Basin floor",t,new Vector3(0,.035f,0),new Vector3(3,.07f,2.4f),concrete);
        foreach(float x in new[]{-1.5f,1.5f}) Box("Low chipped rim",t,new Vector3(x,.18f,0),new Vector3(.22f,.36f,2.6f),tile);
        foreach(float z in new[]{-1.2f,1.2f}) Box("Low chipped rim",t,new Vector3(0,.18f,z),new Vector3(2.8f,.36f,.22f),tile);
        Box("Shallow water surface — visual only",t,new Vector3(0,.115f,0),new Vector3(2.75f,.015f,2.12f),water,false);
        Box("Broken low pedestal",t,new Vector3(.25f,.28f,.1f),new Vector3(.45f,.45f,.45f),concrete);
    }
    static void Station(Transform parent)
    {
        var t=Group("04 Station information nook",parent,new Vector3(-5.65f,0,3.4f),-65);
        Bay(t,5.3f,3.4f);
        Sign(t,"STATION INFO",new Vector3(0,2.9f,-1.8f),5.2f);
        Box("Noticeboard backing",t,new Vector3(0,1.35f,1.5f),new Vector3(4,1.8f,.12f),wood,false);
        for(int i=0;i<6;i++) Box("Pinned map sheet",t,new Vector3(-1.5f+i*.6f,1.5f+(i%2)*.2f,1.41f),new Vector3(.5f,.65f,.025f),tile,false);
        Box("Low bench seat",t,new Vector3(0,.32f,.7f),new Vector3(3.8f,.12f,.55f),wood);
        foreach(float x in new[]{-1.5f,1.5f}) Box("Bench support",t,new Vector3(x,.14f,.7f),new Vector3(.15f,.28f,.5f),dark);
        Lamp(t,new Vector3(0,2.65f,-.4f),new Vector3(0,.5f,.7f),new Color(1,.67f,.32f),3,6);
    }
    static void Supplies(Transform parent)
    {
        var t=Group("05 Walk-through supplies store",parent,new Vector3(5.7f,0,2.4f),70);
        Bay(t,5.6f,3.8f,true);
        Sign(t,"SUPPLIES",new Vector3(0,2.9f,-1.95f),5.4f);
        // Full front opening plus left-side passage. Shelves are only at rear and right.
        Shelf(t,new Vector3(.7f,0,1.5f),3.4f);
        var side=Group("Right display",t,new Vector3(2.25f,0,.2f),90); Shelf(side,Vector3.zero,2.4f);
        Lamp(t,new Vector3(0,2.65f,0),new Vector3(0,.2f,.8f),new Color(1,.69f,.4f),3,7);
        Group("Store interaction anchor",t,new Vector3(.5f,0,0));
    }
    static void Bay(Transform t,float width,float depth,bool sidePassage=false)
    {
        Box("Salvaged back partition",t,new Vector3(0,1.4f,depth*.5f),new Vector3(width,2.8f,.12f),metal);
        Box("Right patchwork partition",t,new Vector3(width*.5f,1.4f,0),new Vector3(.12f,2.8f,depth),rust);
        if(!sidePassage) Box("Left patchwork partition",t,new Vector3(-width*.5f,1.4f,0),new Vector3(.12f,2.8f,depth),rust);
        else Box("Side passage header",t,new Vector3(-width*.5f,2.65f,0),new Vector3(.14f,.3f,depth),wood);
        Box("Rough corrugated canopy",t,new Vector3(0,3.08f,0),new Vector3(width+.3f,.14f,depth+.4f),rust);
        foreach(float x in new[]{-width*.5f,width*.5f}) Box("Salvaged front post",t,new Vector3(x,1.4f,-depth*.5f),new Vector3(.16f,2.8f,.16f),wood);
    }
    static void Shelf(Transform t,Vector3 pos,float width)
    {
        var s=Group("Salvaged shelving",t,pos);
        foreach(float x in new[]{-width*.5f,width*.5f}) Box("Shelf upright",s,new Vector3(x,.9f,0),new Vector3(.08f,1.8f,.6f),dark);
        for(int i=0;i<3;i++)
        {
            Box("Shelf board",s,new Vector3(0,.25f+i*.6f,0),new Vector3(width,.07f,.6f),wood);
            for(int j=0;j<3;j++) Box("Placeholder stock — not purchasable",s,new Vector3(-width*.32f+j*width*.32f,.44f+i*.6f,0),new Vector3(.35f,.3f,.3f),j%2==0?metal:wood,false);
        }
    }
    static void Crate(Transform t,Vector3 basePos,float size)
    {
        Box("Salvaged crate",t,basePos+Vector3.up*size*.5f,Vector3.one*size,wood);
        Box("Crate band",t,basePos+Vector3.up*size*.5f+Vector3.back*(size*.5f+.015f),new Vector3(size,.09f,.03f),dark,false);
    }
    static void Departure(Transform parent)
    {
        var t=Group("06 Platforms and controls wall",parent,new Vector3(0,0,-1.5f));
        Box("Closed reinforced platforms gate",t,new Vector3(0,1.7f,10.5f),new Vector3(6.4f,3.4f,.3f),dark);
        foreach(float x in new[]{-1.55f,1.55f})
        {
            var b=Box("Gate diagonal brace",t,new Vector3(x,1.7f,10.3f),new Vector3(.13f,4.1f,.1f),metal,false);
            b.transform.localRotation=Quaternion.Euler(0,0,x<0?-42:42);
        }
        Sign(t,"PLATFORMS",new Vector3(0,3.85f,10.4f),6.6f);
        Box("Boarding zone paint",t,new Vector3(0,.006f,8.2f),new Vector3(6,.01f,3.6f),metal,false);
        foreach(float x in new[]{-3f,3f}) Box("Boarding edge paint",t,new Vector3(x,.015f,8.2f),new Vector3(.08f,.01f,3.6f),tile,false);
        Box("Plain controls mounting wall",t,new Vector3(-4.9f,1.6f,10),new Vector3(3,3.2f,.2f),tile);
        Group("Existing keypad anchor",t,new Vector3(-4.9f,0,9.44f));
    }
    static void Fixtures(Transform parent)
    {
        var t=Group("07 Retrofitted lighting and repairs",parent);
        Lamp(t,new Vector3(-3.05f,3.8f,5.2f),new Vector3(-.7f,0,0),new Color(.72f,.83f,1),5,14);
        Lamp(t,new Vector3(3.05f,3.8f,5.2f),new Vector3(.7f,0,0),new Color(.72f,.83f,1),5,14);
        Lamp(t,new Vector3(0,4.2f,-7.4f),new Vector3(0,1,-11.5f),new Color(.65f,.78f,1),4,9);
        Lamp(t,new Vector3(-3.05f,3.9f,-.8f),new Vector3(-6.8f,6.3f,4),new Color(.60f,.70f,.82f),1.8f,10);
        Lamp(t,new Vector3(3.05f,3.9f,-.8f),new Vector3(6.8f,6.3f,-4),new Color(.60f,.70f,.82f),1.8f,10);
        foreach(float side in new[]{-1f,1f})
        {
            Crate(t,new Vector3(side*7.7f,0,-7.5f),.7f);
            Crate(t,new Vector3(side*7.7f,.7f,-7.5f),.5f);
            var l=new GameObject("Distant rail maintenance glow").AddComponent<Light>();
            l.transform.SetParent(t,false); l.transform.localPosition=new Vector3(side*10.4f,.25f,-3);
            l.type=LightType.Point; l.range=4; l.intensity=.75f; l.color=new Color(.3f,.4f,.5f); l.shadows=LightShadows.None;
        }
    }
    static void Lamp(Transform parent,Vector3 pos,Vector3 target,Color color,float intensity,float range)
    {
        var t=Group("Retrofitted swivel work light",parent,pos);
        t.localRotation=Quaternion.LookRotation(target-pos);
        Box("Bolted mounting arm",t,new Vector3(0,-.25f,-.18f),new Vector3(.12f,.6f,.14f),dark,false);
        Box("Square low-poly lamp housing",t,Vector3.zero,new Vector3(.45f,.32f,.3f),dark,false);
        Box("Bright lens",t,new Vector3(0,0,.16f),new Vector3(.35f,.24f,.025f),glow,false);
        var go=new GameObject("Provisional shadowless spotlight"); go.transform.SetParent(t,false); go.transform.localPosition=Vector3.forward*.2f;
        var l=go.AddComponent<Light>(); l.type=LightType.Spot; l.color=color; l.intensity=intensity*7; l.range=range; l.spotAngle=95; l.innerSpotAngle=55;
        l.shadows=LightShadows.None; l.lightmapBakeType=LightmapBakeType.Realtime;
    }
}
#endif
