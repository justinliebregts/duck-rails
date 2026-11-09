#if UNITY_EDITOR
using System;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

// Explicit preview authoring only; never installs into the live route.
public static class RailHelipadPreview
{
    public const string Path = "Assets/DuckRails/Blocks/midpoint-helipad-v1.prefab";
    const string Folder = "Assets/DuckRails/HelipadPreview";
    static Material Mat(string name) => AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville " + name + ".mat");
    static Transform Group(Transform p, string name, Vector3 pos)
    { var t = new GameObject(name).transform; t.SetParent(p, false); t.localPosition = pos; return t; }
    static GameObject Shape(Transform p, string name, Vector3 pos, Vector3 size, Material mat, PrimitiveType type = PrimitiveType.Cube, bool solid = true)
    {
        var g = GameObject.CreatePrimitive(type); g.name = name; g.layer = 6; g.transform.SetParent(p, false); g.transform.localPosition = pos; g.transform.localScale = size;
        g.GetComponent<Renderer>().sharedMaterial = mat;
        if (!solid) UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());
        if (type == PrimitiveType.Cube)
        {
            var m = UnityEngine.Object.Instantiate(g.GetComponent<MeshFilter>().sharedMesh);
            var v = m.vertices; var n = m.normals; var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) { var q = Vector3.Scale(v[i], size); uv[i] = (Mathf.Abs(n[i].y) > .5f ? new Vector2(q.x,q.z) : Mathf.Abs(n[i].x) > .5f ? new Vector2(q.z,q.y) : new Vector2(q.x,q.y)) / 4; }
            m.uv = uv; Unwrapping.GenerateSecondaryUVSet(m);
            string meshPath = Folder + "/Box-" + Hash128.Compute(size.ToString("F4")) + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if (existing == null) { AssetDatabase.CreateAsset(m, meshPath); existing = m; } else UnityEngine.Object.DestroyImmediate(m);
            g.GetComponent<MeshFilter>().sharedMesh = existing;
        }
        return g;
    }
    public static void Build()
    {
        if (Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Wrong project or Play Mode.");
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Path)) throw new InvalidOperationException("Preview exists; edit it directly to preserve authoring.");
        System.IO.Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var amber = new Material(Shader.Find("Universal Render Pipeline/Lit")); amber.name = "Amber edge lamp"; amber.color = new Color(1,.48f,.05f); amber.EnableKeyword("_EMISSION"); amber.SetColor("_EmissionColor",new Color(3,1.1f,.06f)); AssetDatabase.CreateAsset(amber,Folder+"/Amber.mat");
        var paint = new Material(Shader.Find("Universal Render Pipeline/Lit")); paint.name="Faded pad paint";paint.color=new Color(.76f,.74f,.59f);AssetDatabase.CreateAsset(paint,Folder+"/Paint.mat");
        var scene = EditorSceneManager.NewPreviewScene(); var root = new GameObject("Midpoint Helipad - 60m"); UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);
        try {
            var b=root.AddComponent<RailBlock>();b.id=RailRoute.MidpointId;b.displayName="Midpoint Helipad (60m preview)";b.length=60;b.allowedInNormalRuns=false;b.coalCount=b.goldCount=b.enemyCount=0;b.spawnCandidatesMigrated=true;b.spawnCandidates=new RailSpawnSet{coal=Array.Empty<Transform>(),gold=Array.Empty<Transform>(),enemies=Array.Empty<Transform>()};b.variations=Array.Empty<RailSpawnSet>();b.tags=new[]{"shop","helipad","preview"};
            b.entry=Group(root.transform,"Track Entry",Vector3.zero);b.exit=Group(root.transform,"Track Exit",new Vector3(0,0,60));b.stop=Group(root.transform,"Suggested Stop",new Vector3(0,0,30));b.track=new[]{Vector3.zero,new Vector3(0,0,60)};b.enemyBounds=new Bounds(new Vector3(0,6,30),new Vector3(96,12,60));
            var env=Group(root.transform,"Env",Vector3.zero);
            Shape(env,"Ground 96 x 60",new Vector3(0,-.5f,30),new Vector3(96,1,60),Mat("grass"));
            Shape(env,"Track ballast",new Vector3(0,.01f,30),new Vector3(4,.02f,60),Mat("gravel"),solid:false);
            var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/Materials/Metal.mat");
            foreach(float x in new[]{-1.5f,1.5f})Shape(env,"Rail",new Vector3(x,.1f,30),new Vector3(.12f,.15f,60),metal,solid:false);
            for(float z=1;z<60;z+=2)Shape(env,"Sleeper",new Vector3(0,.04f,z),new Vector3(3.8f,.08f,.35f),Mat("timber"),solid:false);
            var pad=Group(env,"Helipad",new Vector3(-23,0,30));
            Shape(pad,"Low concrete pad 30 x 30",new Vector3(0,.075f,0),new Vector3(30,.15f,30),Mat("concrete"));
            Shape(env,"Ground-level walking apron",new Vector3(-6,.035f,30),new Vector3(4,.07f,18),Mat("concrete"));
            foreach(float x in new[]{-3f,3f})Shape(pad,"H marking",new Vector3(x,.157f,0),new Vector3(1,.01f,9),paint,solid:false);
            Shape(pad,"H crossbar",new Vector3(0,.157f,0),new Vector3(6,.01f,1),paint,solid:false);
            foreach(float z in new[]{-14.3f,14.3f})foreach(float x in new[]{-14.3f,0,14.3f})Lamp(pad,new Vector3(x,.15f,z),metal,amber);
            foreach(float x in new[]{-14.3f,14.3f})Lamp(pad,new Vector3(x,.15f,0),metal,amber);
            var hut=Group(env,"Operations hut",new Vector3(-40,0,8));
            Shape(hut,"Foundation",new Vector3(0,.1f,0),new Vector3(6,.2f,5),Mat("concrete"));
            Shape(hut,"Back wall",new Vector3(0,1.6f,-2.4f),new Vector3(6,3,.2f),Mat("weathered paint"));
            foreach(float x in new[]{-2.9f,2.9f})Shape(hut,"Side wall",new Vector3(x,1.6f,0),new Vector3(.2f,3,5),Mat("weathered paint"));
            foreach(float x in new[]{-2f,2f})Shape(hut,"Door side wall",new Vector3(x,1.6f,2.4f),new Vector3(2,3,.2f),Mat("weathered paint"));
            Shape(hut,"Door header",new Vector3(0,2.85f,2.4f),new Vector3(2,.5f,.2f),Mat("weathered paint"));
            Shape(hut,"Roof",new Vector3(0,3.25f,0),new Vector3(6.6f,.3f,5.6f),Mat("dark roofing"));
            Shape(hut,"Work bench",new Vector3(0,.9f,-1.8f),new Vector3(4,.18f,1),Mat("timber"));
            Lamp(hut,new Vector3(0,2.7f,2.6f),metal,amber);
            var tank=Group(env,"Fuel tank and bund",new Vector3(-26,0,53));
            Shape(tank,"Bund floor",new Vector3(0,.1f,0),new Vector3(8,.2f,4),Mat("concrete"));
            foreach(float z in new[]{-1.9f,1.9f})Shape(tank,"Bund rim",new Vector3(0,.35f,z),new Vector3(8,.5f,.2f),Mat("concrete"));
            foreach(float x in new[]{-3.9f,3.9f})Shape(tank,"Bund rim",new Vector3(x,.35f,0),new Vector3(.2f,.5f,4),Mat("concrete"));
            foreach(float x in new[]{-2f,2f})Shape(tank,"Tank saddle",new Vector3(x,.65f,0),new Vector3(.5f,1,2),metal);
            Shape(tank,"Horizontal fuel tank",new Vector3(0,1.8f,0),new Vector3(2.4f,3.2f,2.4f),Mat("camper paint"),PrimitiveType.Cylinder).transform.localRotation=Quaternion.Euler(0,0,90);
            Shape(tank,"Filler cap",new Vector3(0,3.05f,0),new Vector3(.4f,.16f,.4f),metal,PrimitiveType.Cylinder);
            var wind=Group(env,"Windsock",new Vector3(-43,0,51));Shape(wind,"Pole",new Vector3(0,2.5f,0),new Vector3(.1f,2.5f,.1f),metal,PrimitiveType.Cylinder);
            Shape(wind,"Sock",new Vector3(1,4.9f,0),new Vector3(.55f,1,.55f),paint,PrimitiveType.Cylinder,false).transform.localRotation=Quaternion.Euler(0,0,90);
            for(int z=5;z<=55;z+=5) {Shape(env,"Fence post",new Vector3(-46,1,z),new Vector3(.2f,2,.2f),Mat("timber")); if(z<55)foreach(float y in new[]{.6f,1.4f})Shape(env,"Fence rail",new Vector3(-46,y,z+2.5f),new Vector3(.12f,.15f,5),Mat("timber"));}
            for(int x=-43;x<=-13;x+=5) {Shape(env,"Rear fence post",new Vector3(x,1,58),new Vector3(.2f,2,.2f),Mat("timber"));if(x<-13)foreach(float y in new[]{.6f,1.4f})Shape(env,"Rear fence rail",new Vector3(x+2.5f,y,58),new Vector3(5,.15f,.12f),Mat("timber"));}
            var trees=Group(env,"Trees and rocks",Vector3.zero);var rng=new System.Random(421);
            for(int i=0;i<26;i++){float x=i<8?-44:13+(float)rng.NextDouble()*29;float z=i<8?4+i*7:4+(float)rng.NextDouble()*52;Tree(trees,new Vector3(x,0,z),.75f+(float)rng.NextDouble()*.45f);}
            for(int i=0;i<14;i++)Shape(trees,"Rock",new Vector3(12+(float)rng.NextDouble()*31,.35f,3+(float)rng.NextDouble()*54),new Vector3(1.5f,.9f,1.3f),Mat("rock"),PrimitiveType.Sphere);
            PrefabUtility.SaveAsPrefabAsset(root,Path);
        } finally {EditorSceneManager.ClosePreviewScene(scene);}
        AssetDatabase.SaveAssets(); Debug.Log("Helipad preview saved: 96x60 ground, 30x30 low pad, 8 pad lights, hut, tank, trees; no helicopter or gold receiver.");
    }
    static void Lamp(Transform parent,Vector3 pos,Material metal,Material amber)
    {var t=Group(parent,"Amber lamp",pos);Shape(t,"Base",new Vector3(0,.08f,0),new Vector3(.4f,.16f,.4f),metal);Shape(t,"Lens",new Vector3(0,.23f,0),new Vector3(.23f,.2f,.23f),amber);var l=Group(t,"Light",new Vector3(0,.5f,0)).gameObject.AddComponent<Light>();l.type=LightType.Point;l.color=new Color(1,.55f,.15f);l.range=4;l.intensity=1.2f;l.shadows=LightShadows.None;l.lightmapBakeType=LightmapBakeType.Baked;}
    static void Tree(Transform parent,Vector3 pos,float scale)
    {var t=Group(parent,"Pine",pos);t.localScale=Vector3.one*scale;Shape(t,"Trunk",new Vector3(0,2,0),new Vector3(.5f,2,.5f),Mat("timber"),PrimitiveType.Cylinder);for(int i=0;i<3;i++){var mesh=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/CampgroundGreybox/Meshes/Pine-tier-"+i+".asset");var g=Group(t,"Needles",new Vector3(0,2+i*1.6f,0)).gameObject;g.AddComponent<MeshFilter>().sharedMesh=mesh;g.AddComponent<MeshRenderer>().sharedMaterial=Mat("foliage");}}
}
#endif
