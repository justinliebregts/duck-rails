#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Object=UnityEngine.Object;

// Scoped one-time approved layout installation; never rebuild the preserved motel.
public static class RailMotelLeisure
{
    public const string Folder="Assets/DuckRails/MotelLeisure";
    public const string Root="Community leisure November 12";
    static Transform parent;
    static Material cream,teal,coral,yellow,glass,green;
    static Mesh cube;
    static Transform Group(Transform p,string n){var t=new GameObject(n).transform;t.SetParent(p,false);return t;}
    static Material Mat(string n,Color c){var p=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.08f);return m;}
    static Transform Box(string n,Vector3 p,Vector3 s,string kind="concrete",Material mat=null,bool solid=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.layer=6;g.transform.SetParent(parent,false);g.transform.localPosition=p;g.transform.localScale=s;
        var f=g.GetComponent<MeshFilter>();f.sharedMesh=cube;var r=g.GetComponent<MeshRenderer>();
        if(mat)r.sharedMaterial=mat;else RailDepotMaterials.Map(f,r,kind,0,Folder+"/Meshes",2);
        r.scaleInLightmap=.25f;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);
        if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;
    }
    static void Beam(string n,Vector3 a,Vector3 b,float w,string kind="steel",Material mat=null){var t=Box(n,(a+b)/2,new Vector3(w,w,Vector3.Distance(a,b)),kind,mat);t.localRotation=Quaternion.LookRotation(b-a);}
    static void Sign(string text,Vector3 pos,float width, float yaw=0)
    {
        Box(text+" board",pos,new Vector3(width,1.5f,.16f),"timber");var t=Group(parent,text);t.localPosition=pos+Vector3.back*.1f;t.localRotation=Quaternion.Euler(0,yaw,0);
        var label=t.gameObject.AddComponent<TextMeshPro>();label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");label.text=text;label.fontSize=5;label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=new Vector2(width,1.4f);label.color=new Color(.9f,.8f,.55f);
    }
    static void Bench(float x,float z){Box("Bench seat",new Vector3(x,.65f,z),new Vector3(3,.18f,.65f),"timber");Box("Bench back",new Vector3(x,1.1f,z+.35f),new Vector3(3,.6f,.12f),"timber");foreach(float dx in new[]{-1.1f,1.1f})Box("Bench leg",new Vector3(x+dx,.3f,z),new Vector3(.16f,.6f,.6f),"steel");}
    static void Lamp(float x,float z,float y=5)
    {
        Box("Lamp post",new Vector3(x,y/2,z),new Vector3(.15f,y,.15f),"steel");Box("Lamp housing",new Vector3(x,y,z),new Vector3(.8f,.25f,.5f),"steel");
        var t=Group(parent,"Baked leisure light");t.localPosition=new Vector3(x,y-.2f,z);var l=t.gameObject.AddComponent<Light>();l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.color=new Color(1,.77f,.5f);l.intensity=12;l.range=16;l.shadows=LightShadows.Soft;
    }
    static void Fence(Vector3 a,Vector3 b,float h=1.7f)
    {
        int n=Mathf.CeilToInt(Vector3.Distance(a,b)/.65f);for(int i=0;i<=n;i++)Box("Fence upright",Vector3.Lerp(a,b,(float)i/n)+Vector3.up*h/2,new Vector3(.07f,h,.07f),"steel");
        foreach(float y in new[]{.25f,h})Beam("Fence rail",a+Vector3.up*y,b+Vector3.up*y,.08f);
    }
    static void Room(float x,float z,float w,float d,string name)
    {
        Box(name+" floor",new Vector3(x,.08f,z),new Vector3(w,.16f,d));
        Box(name+" rear wall",new Vector3(x,1.8f,z+d/2),new Vector3(w,3.6f,.2f),"wall");
        foreach(float dx in new[]{-w/2,w/2})Box(name+" side",new Vector3(x+dx,1.8f,z),new Vector3(.2f,3.6f,d),"wall");
        float segment=(w-3)/2;foreach(float dx in new[]{-(w+3)/4,(w+3)/4})Box(name+" front",new Vector3(x+dx,1.8f,z-d/2),new Vector3(segment,3.6f,.2f),"wall");
        Box(name+" lintel",new Vector3(x,3.25f,z-d/2),new Vector3(3,.7f,.2f),"wall");Box(name+" roof",new Vector3(x,3.7f,z),new Vector3(w+.6f,.2f,d+.6f),"roof");
        Sign(name,new Vector3(x,3.05f,z-d/2-.15f),Mathf.Min(w-1,10));
    }
    static void Pool(Transform root)
    {
        parent=Group(root,"Municipal pool");
        // Excavate only the basin. Surrounding ground and track retain y=0.
        Box("Pool floor",new Vector3(-26,-1.7f,68),new Vector3(20,.2f,28),mat:teal);
        foreach(float x in new[]{-36f,-16f})Box("Basin side",new Vector3(x,-.8f,68),new Vector3(.25f,1.6f,28),mat:cream);
        Box("Basin north",new Vector3(-26,-.8f,82),new Vector3(20,1.6f,.25f),mat:cream);
        Box("Basin south",new Vector3(-26,-.8f,54),new Vector3(20,1.6f,.25f),mat:cream);
        for(int i=0;i<8;i++){float h=(8-i)*.2f;Box("Broad pool entry step",new Vector3(-26,-1.6f+h/2,54.3f+i*.45f),new Vector3(8,h,.45f));}
        Box("West deck",new Vector3(-40,.05f,68),new Vector3(8,.1f,36));Box("East deck",new Vector3(-12,.05f,68),new Vector3(8,.1f,36));
        foreach(float z in new[]{50f,86f})Box("End deck",new Vector3(-26,.05f,z),new Vector3(20,.1f,8));
        for(int i=0;i<14;i++)foreach(float x in new[]{-37f,-15f})Box("Faded deck tile",new Vector3(x,.108f,55+i*2),new Vector3(1.5f,.015f,1.5f),mat:i%2==0?coral:teal,solid:false);
        for(int i=0;i<4;i++)Box("Pool lane paint",new Vector3(-33+i*4.5f,-1.591f,69),new Vector3(.14f,.012f,20),mat:glass,solid:false);
        Room(-26,100,32,18,"CHANGE ROOMS");
        // Open cubbies: back, shelves and partitions, all physically reachable from the room.
        Box("Cubbies back",new Vector3(-26,1.3f,108.6f),new Vector3(22,2.4f,.1f),mat:teal);
        for(int i=0;i<=22;i++)Box("Cubby divider",new Vector3(-37+i,1.3f,108.2f),new Vector3(.07f,2.4f,.8f),mat:cream);
        foreach(float y in new[]{.2f,1,1.8f,2.6f})Box("Cubby shelf",new Vector3(-26,y,108.2f),new Vector3(22,.08f,.8f),mat:cream);
        foreach(float x in new[]{-35f,-26f,-17f})Bench(x,103);
        foreach(float x in new[]{-39f,-35f,-31f}){Box("Shower partition",new Vector3(x,1.3f,96),new Vector3(.1f,2.6f,3),mat:teal);Beam("Shower pipe",new Vector3(x+.7f,.4f,97),new Vector3(x+.7f,2.7f,97),.06f);Box("Shower head",new Vector3(x+.7f,2.7f,96.8f),new Vector3(.3f,.1f,.4f),"steel");}
        Box("Changing privacy divider",new Vector3(-23,1.35f,97),new Vector3(.15f,2.7f,8),mat:cream);
        Bench(-18,96);Sign("LOCAL POOL",new Vector3(-26,3.5f,46),9);
        Fence(new Vector3(-44,0,46),new Vector3(-44,0,111));Fence(new Vector3(-44,0,111),new Vector3(-8,0,111));
        Fence(new Vector3(-8,0,46),new Vector3(-8,0,73));Fence(new Vector3(-8,0,78),new Vector3(-8,0,111));
        Fence(new Vector3(-44,0,46),new Vector3(-30,0,46));Fence(new Vector3(-22,0,46),new Vector3(-8,0,46));
        Lamp(-10,52);Lamp(-42,88);Lamp(-10,105);var l=Group(parent,"Changing room fluorescent");l.localPosition=new Vector3(-26,3.3f,100);var light=l.gameObject.AddComponent<Light>();light.type=LightType.Point;light.lightmapBakeType=LightmapBakeType.Baked;light.color=new Color(.7f,.9f,.8f);light.range=18;light.intensity=10;
        Slide(root);
    }
    static void Slide(Transform root)
    {
        parent=Group(root,"Old waterslide");Box("Slide tower landing",new Vector3(-40,4,78),new Vector3(3,.2f,3),"steel");
        foreach(float x in new[]{-41f,-39f})foreach(float z in new[]{77f,79f})Box("Tower leg",new Vector3(x,2,z),new Vector3(.18f,4,.18f),"steel");
        for(int i=0;i<20;i++)Box("Slide stair",new Vector3(-40,.1f+i*.1f,86-i*.35f),new Vector3(2,.2f+i*.2f,.35f),"tread");
        foreach(float x in new[]{-41.1f,-38.9f})Beam("Stair handrail",new Vector3(x,1,86),new Vector3(x,5,79.3f),.1f);
        // Broad curved trough built from fitted collidable segments; static scenic slide.
        var points=new List<Vector3>();for(int i=0;i<=24;i++){float t=i/24f;float a=t*Mathf.PI;points.Add(new Vector3(-35-5*Mathf.Cos(a),4-5.1f*t,78-10*Mathf.Sin(a)));}
        for(int i=0;i<points.Count-1;i++){var a=points[i];var b=points[i+1];var delta=b-a;var side=Vector3.Cross(Vector3.up,delta).normalized;var t=Box("Yellow flume",(a+b)/2,new Vector3(2,.15f,delta.magnitude+.08f),mat:yellow);t.localRotation=Quaternion.LookRotation(delta);foreach(float sign in new[]{-1f,1f}){var lip=Box("Flume side",(a+b)/2+side*sign+Vector3.up*.35f,new Vector3(.12f,.8f,delta.magnitude+.1f),mat:yellow);lip.localRotation=t.localRotation;}}
    }
    static void North(Transform root)
    {
        parent=Group(root,"Mini golf");
        for(int i=0;i<5;i++){float x=i%2==0?-34:-18,z=119+i*6;Box("Putting green "+(i+1),new Vector3(x,.12f,z),new Vector3(11,.2f,4.5f),mat:green);foreach(float dz in new[]{-2.3f,2.3f})Box("Green curb",new Vector3(x,.22f,z+dz),new Vector3(11,.25f,.15f));Beam("Hole flag pole",new Vector3(x+4,.2f,z),new Vector3(x+4,1.7f,z),.035f);Box("Hole flag",new Vector3(x+4.3f,1.5f,z),new Vector3(.6f,.35f,.04f),mat:coral);Box("Golf obstacle",new Vector3(x,.5f,z),new Vector3(1.2f,.7f,1),"timber");}
        Room(-39,151,7,5,"GOLF");Bench(-12,148);Lamp(-10,121);Lamp(-43,148);
        // Chunky duck-shaped course obstacle with collidable body, bill and eyes.
        Box("Duck obstacle body",new Vector3(-34,1,131),new Vector3(2.5f,1.4f,1.5f),mat:yellow);Box("Duck head",new Vector3(-33,2,131),new Vector3(1,1,1),mat:yellow);Box("Duck bill",new Vector3(-32.3f,1.9f,131),new Vector3(.7f,.3f,.8f),mat:coral);
        parent=Group(root,"Roller rink");Box("Rink concrete",new Vector3(26,.1f,132),new Vector3(32,.2f,26));
        for(int i=0;i<3;i++){float inset=i*.8f;var m=i==0?coral:i==1?yellow:teal;foreach(float z in new[]{120f+inset,144f-inset})Box("Faded skating stripe",new Vector3(26,.211f,z),new Vector3(30-inset*2,.015f,.65f),mat:m,solid:false);foreach(float x in new[]{11f+inset,41f-inset})Box("Faded skating stripe",new Vector3(x,.211f,132),new Vector3(.65f,.015f,24-inset*2),mat:m,solid:false);}
        Fence(new Vector3(10,0,119),new Vector3(10,0,145),1);Fence(new Vector3(10,0,145),new Vector3(42,0,145),1);Fence(new Vector3(42,0,119),new Vector3(42,0,145),1);Fence(new Vector3(10,0,119),new Vector3(23,0,119),1);Fence(new Vector3(29,0,119),new Vector3(42,0,119),1);
        Room(34,150,12,6,"SKATE RENTAL");Box("Rental counter",new Vector3(34,.65f,150),new Vector3(8,1.3f,.8f),"timber");
        foreach(float z in new[]{125f,132f,139f}){Bench(44,z);foreach(float dz in new[]{-2f,2f})Box("Bench canopy post",new Vector3(45.5f,1.5f,z+dz),new Vector3(.1f,3,.1f),"steel");Box("Bench canopy",new Vector3(44,3,z),new Vector3(3.5f,.15f,5),mat:teal);}
        Lamp(12,147);Lamp(40,117);
    }
    static void Bus(Transform root,float x,float z,float yaw,Material paint,int index)
    {
        parent=Group(root,"Bus "+index);var bus=parent;
        Box("Bus lower body",new Vector3(0,.95f,0),new Vector3(3.1f,1.2f,11.5f),mat:paint);Box("Bus window band",new Vector3(0,2.1f,0),new Vector3(3,1.2f,11.2f),mat:glass);Box("Bus roof",new Vector3(0,2.8f,0),new Vector3(3.15f,.25f,11.5f),mat:cream);
        foreach(float side in new[]{-1.56f,1.56f}){Box("Coach stripe",new Vector3(side,1.15f,0),new Vector3(.025f,.22f,11),mat:cream);for(int i=0;i<8;i++)Box("Window pillar",new Vector3(side,2.1f,-4.7f+i*1.35f),new Vector3(.08f,1.3f,.12f),mat:paint);foreach(float zz in new[]{-3.7f,3.7f})Box("Bus wheel",new Vector3(side,.5f,zz),new Vector3(.35f,1,1),mat:glass);}
        foreach(float dx in new[]{-1.1f,1.1f})Box("Headlamp",new Vector3(dx,1,-5.78f),new Vector3(.4f,.3f,.08f),mat:yellow);
        Box("Front bumper",new Vector3(0,.6f,-5.85f),new Vector3(3.2f,.22f,.16f),"steel");bus.localPosition=new Vector3(x,0,z);bus.localRotation=Quaternion.Euler(0,yaw,0);
    }
    static void South(Transform root)
    {
        parent=Group(root,"Arrival and public parking");var arrival=parent;
        foreach(float x in new[]{-26f,26f}){Box("Parking asphalt",new Vector3(x,.025f,26),new Vector3(34,.05f,34),"ground");for(int i=0;i<5;i++)Box("Bus bay stripe",new Vector3(x-13+i*6,.058f,22),new Vector3(.12f,.015f,15),mat:cream,solid:false);}
        Bus(arrival,-37,23,0,teal,1);Bus(arrival,-25,22,0,coral,2);Bus(arrival,-13,32,-12,cream,3);Bus(arrival,18,22,0,teal,4);Bus(arrival,34,33,28,yellow,5);
        parent=arrival;Bench(-34,42);Bench(14,43);Sign("PUBLIC POOL",new Vector3(-26,3,43),9);Lamp(-43,12);Lamp(-10,40);Lamp(10,12);Lamp(42,43);
        for(int i=0;i<5;i++){float x=-39+i*1.2f;Beam("Bike rack",new Vector3(x,.1f,40),new Vector3(x,1,40),.08f);Beam("Bike rack top",new Vector3(x,1,40),new Vector3(x,1,41),.08f);Beam("Bike rack",new Vector3(x,1,41),new Vector3(x,.1f,41),.08f);}
    }
    public static void FinishLayout()
    {
        StageUtility.GoToMainStage();RailDepotMaterials.BeginSharedPass(Folder+"/Meshes");
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/Cube Block UV.asset");
        green=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Worn putting felt.mat");
        var go=PrefabUtility.LoadPrefabContents(RailMotelGreybox.PrefabPath);
        try{
            var banks=go.transform.Find("Env/Motel greybox v1/Low edge banks");
            foreach(var t in banks.Cast<Transform>().Where(t=>t.name.EndsWith("bank")).ToArray())Object.DestroyImmediate(t.gameObject);
            var root=go.transform.Find("Env/"+Root);if(root.Find("Perimeter trees"))throw new Exception("Already finished");
            parent=Group(root,"Perimeter trees");
            var verts=new List<Vector3>();var tri=new List<int>();var uv=new List<Vector2>();
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;int k=verts.Count;verts.AddRange(new[]{new Vector3(Mathf.Cos(a),0,Mathf.Sin(a)),Vector3.up*1.4f,new Vector3(Mathf.Cos(b),0,Mathf.Sin(b)),Vector3.zero});uv.AddRange(new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right});tri.AddRange(new[]{k,k+1,k+2,k+2,k+3,k});}
            var mesh=new Mesh();mesh.SetVertices(verts);mesh.SetTriangles(tri,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,Folder+"/Closed tree crown.asset");
            var pts=new[]{new Vector3(-43,0,10),new Vector3(-43,0,33),new Vector3(-40,0,44),new Vector3(-9,0,12),new Vector3(43,0,12),new Vector3(43,0,42),new Vector3(10,0,43),new Vector3(-44,0,115),new Vector3(-44,0,133),new Vector3(-28,0,152),new Vector3(-11,0,151),new Vector3(-10,0,134),new Vector3(11,0,153),new Vector3(23,0,151),new Vector3(44,0,153)};
            foreach(var p in pts){Box("Tree trunk",p+Vector3.up*1.4f,new Vector3(.5f,2.8f,.5f),"timber");for(int j=0;j<2;j++){var t=Group(parent,"Closed low-poly canopy");t.localPosition=p+Vector3.up*(2+j*1.6f);t.localScale=new Vector3(2.1f-j*.4f,1.6f,2.1f-j*.4f);t.gameObject.layer=6;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var rr=t.gameObject.AddComponent<MeshRenderer>();rr.sharedMaterial=green;rr.scaleInLightmap=.2f;t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);}}
            // Open the motel rear lane toward the rink, retaining both existing balcony routes.
            foreach(var text in go.GetComponentsInChildren<TMP_Text>())if(text.text.Contains("•"))text.text=text.text.Replace("•","-");
            PrefabUtility.SaveAsPrefabAsset(go,RailMotelGreybox.PrefabPath);AssetDatabase.SaveAssets();
        }finally{PrefabUtility.UnloadPrefabContents(go);}
        PrefabStageUtility.OpenPrefab(RailMotelGreybox.PrefabPath);
    }
    [MenuItem("Duck Rails/Authoring/Motel/Install approved leisure layout")]
    public static void Install()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Duck Rails Edit Mode required");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Unsaved prefab");if(stage!=null)StageUtility.GoToMainStage();
        Directory.CreateDirectory(Folder);RailDepotMaterials.BeginSharedPass(Folder+"/Meshes");cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/Cube Block UV.asset");
        cream=Mat("Worn cream",new Color(.69f,.66f,.51f));teal=Mat("Faded teal",new Color(.18f,.45f,.43f));coral=Mat("Faded coral",new Color(.62f,.29f,.23f));yellow=Mat("Old yellow fiberglass",new Color(.73f,.53f,.17f));glass=Mat("Dark glazing rubber",new Color(.07f,.11f,.13f));green=Mat("Worn putting felt",new Color(.25f,.4f,.22f));
        var go=PrefabUtility.LoadPrefabContents(RailMotelGreybox.PrefabPath);
        try{
            var env=go.transform.Find("Env");if(env.Find(Root))throw new Exception("Already installed: edit existing objects instead");var old=env.Find(RailMotelGreybox.RootName);var motel=old.Find("Motel");var preserved=motel.GetComponentsInChildren<Transform>().Select(t=>t.localToWorldMatrix).ToArray();
            foreach(string n in new[]{"Office and garage","Transformer yard"})Object.DestroyImmediate(old.Find(n).gameObject);
            var exterior=old.Find("Exterior paths and props");foreach(Transform t in exterior.Cast<Transform>().ToArray())if(t.name=="Garage apron"||t.name=="Office approach"||t.name=="Maintenance van"||t.name=="Apron crates"||t.name=="North boundary")Object.DestroyImmediate(t.gameObject);
            // Replace only the ground slab under the pool excavation with four matching surrounding pieces.
            Object.DestroyImmediate(env.Find("Block ground").gameObject);parent=Group(env,Root);var root=parent;
            Box("Ground west",new Vector3(-42,-.5f,80),new Vector3(12,1,160),"ground");Box("Ground east",new Vector3(16,-.5f,80),new Vector3(64,1,160),"ground");Box("Ground south",new Vector3(-26,-.5f,27),new Vector3(20,1,54),"ground");Box("Ground north",new Vector3(-26,-.5f,121),new Vector3(20,1,78),"ground");
            Pool(root);North(root);South(root);
            // Relocate the existing west-side loot only, keeping identities and counts.
            var block=go.GetComponent<RailBlock>();var coal=new[]{new Vector3(-33,.1f,104),new Vector3(-18,.1f,99),new Vector3(-26,-1.6f,69),new Vector3(-40,.1f,58),new Vector3(-12,.1f,62),new Vector3(-18,.22f,125),new Vector3(-34,.22f,143),new Vector3(-34,.05f,39),new Vector3(-38,.1f,95)};
            int k=0;foreach(var t in block.spawnCandidates.coal.Where(t=>t.localPosition.x<0)){t.localPosition=coal[k++]+Vector3.up*(.05f-RailLootPlacementWindow.ItemBounds(false).min.y);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            var gold=new[]{new Vector3(-34,1.84f,108.1f),new Vector3(-25,-1.6f,77),new Vector3(34,.16f,152)};k=0;foreach(var t in block.spawnCandidates.gold.Where(t=>t.localPosition.x<0)){t.localPosition=gold[k++]+Vector3.up*(.05f-RailLootPlacementWindow.ItemBounds(true).min.y);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            // Preserve existing east and track candidates; move displaced west candidates into distinct destinations.
            var enemy=new[]{new Vector3(-40,.15f,56),new Vector3(-12,.15f,101),new Vector3(-12,.15f,76),new Vector3(-38,.25f,137),new Vector3(-38,.15f,36),new Vector3(-25,.15f,91),new Vector3(-11,.15f,142)};k=0;foreach(var t in block.spawnCandidates.enemies.Where(t=>t.localPosition.x<0)){t.localPosition=enemy[k++];PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            if(!preserved.SequenceEqual(motel.GetComponentsInChildren<Transform>().Select(t=>t.localToWorldMatrix)))throw new Exception("Motel changed");
            PrefabUtility.SaveAsPrefabAsset(go,RailMotelGreybox.PrefabPath);AssetDatabase.SaveAssets();
            Directory.CreateDirectory("docs/validation/motel-leisure");File.WriteAllText("docs/validation/motel-leisure/install.txt","Preserved motel transforms and track. Installed pool/rink/golf/5 buses; retained 2 cars. Pools "+block.spawnCandidates.coal.Length+"/"+block.spawnCandidates.gold.Length+"/"+block.spawnCandidates.enemies.Length+". West markers relocated; per-run settings unchanged.\n");
        }finally{PrefabUtility.UnloadPrefabContents(go);}PrefabStageUtility.OpenPrefab(RailMotelGreybox.PrefabPath);
    }
}
#endif
