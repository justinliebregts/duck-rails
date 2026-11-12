#if UNITY_EDITOR
using System; using System.IO; using System.Linq; using System.Collections.Generic; using UnityEngine; using UnityEngine.Rendering; using UnityEditor; using UnityEditor.SceneManagement; using Game.AI; using Game.AI.Editor; using TMPro; using Object=UnityEngine.Object;
public static class RailSkyscraperYardPolish {
public const string Folder="Assets/DuckRails/SkyscraperWorks/YardPolish"; public const string Evidence="docs/validation/skyscraper-polish";    static Mesh cube;
    static Material wood, white, red, steel, dark, hay, stone, grass, glow, cloth;
    static int meshId;
    static Transform group;
    static readonly Dictionary<Material,List<CombineInstance>> pieces = new();
    static Transform Group(Transform p,string n) { var g=new GameObject(n);g.layer=6;g.transform.SetParent(p,false);return g.transform; }
    static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(RailSkyscraper.Folder+"/"+n+".mat");
    static void Init()
    {
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();meshId=0;
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/quarry-v1/Cube Block UV.asset");
        wood=Mat("Construction timber");white=Mat("Raw concrete");steel=Mat("Scaffold steel");hay=Mat("Crane ochre");stone=white;grass=Mat("Site gravel");dark=NewMat("Tyre rubber",steel,new Color(.08f,.09f,.09f));red=NewMat("Faded red paint",steel,new Color(.42f,.18f,.13f));
        glow=NewMat("Warm lantern glass",steel,new Color(1,.68f,.28f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1,.48f,.13f)*2);glow.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        cloth=NewMat("Faded green upholstery",white,new Color(.33f,.42f,.25f));
    }
    static Material NewMat(string n,Material source,Color c)
    {var path=Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(source);AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",c);EditorUtility.SetDirty(m);return m;}
    static Mesh SaveMesh(string n,Mesh m)
    {
        m.name=n;var path=Folder+"/"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old)
        {
            // Explicit setters also refresh live GPU buffers after a mesh correction.
            old.Clear();old.indexFormat=m.indexFormat;old.vertices=m.vertices;old.normals=m.normals;old.uv=m.uv;old.uv2=m.uv2;old.triangles=m.triangles;old.bounds=m.bounds;old.UploadMeshData(false);
            Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;
        }
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static void UV(Mesh m)
    {var v=m.vertices;var norm=m.normals;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++)uv[i]=(Mathf.Abs(norm[i].y)>.5f?new Vector2(v[i].x,v[i].z):Mathf.Abs(norm[i].x)>.5f?new Vector2(v[i].z,v[i].y):new Vector2(v[i].x,v[i].y))/3;m.uv=uv;Unwrapping.GenerateSecondaryUVSet(m);}
    static void Start(Transform p,string n){group=Group(p,n);pieces.Clear();}
    static void Box(string n,Vector3 pos,Vector3 size,Material mat,bool collision=true,Quaternion? rot=null)
    {
        var q=rot??Quaternion.identity;if(!pieces.TryGetValue(mat,out var list)){list=new();pieces.Add(mat,list);}list.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(pos,q,size)});
        if(collision){var g=Group(group,n);g.localPosition=pos;g.localRotation=q;g.gameObject.AddComponent<BoxCollider>().size=size;}
    }
    static void Beam(string n,Vector3 a,Vector3 b,float width,Material m,bool collision=true)=>Box(n,(a+b)/2,new Vector3(width,(b-a).magnitude,width),m,collision,Quaternion.FromToRotation(Vector3.up,b-a));
    static void Finish()
    {
        foreach(var kv in pieces){var m=new Mesh{indexFormat=IndexFormat.UInt32};m.CombineMeshes(kv.Value.ToArray(),true,true);UV(m);MeshObject(group,group.name+" "+kv.Key.name,SaveMesh("Detail-"+(meshId++),m),kv.Key,false);}pieces.Clear();
    }
    static GameObject MeshObject(Transform p,string name,Mesh m,Material mat,bool collision)
    {
        var t=Group(p,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=m;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.35f;r.lightProbeUsage=LightProbeUsage.BlendProbes;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);
        if(collision)t.gameObject.AddComponent<MeshCollider>().sharedMesh=m;return t.gameObject;
    }

    static void Lamp(Vector3 p,bool warm) { Box("Floodlight fixture",p,new(.7f,.18f,.4f),hay,false);var l=Group(group,"Baked yard worklight").gameObject.AddComponent<Light>();l.transform.localPosition=p;l.transform.localRotation=Quaternion.Euler(90,0,0);l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=55;l.range=17;l.spotAngle=125;l.color=warm?new Color(1,.76f,.44f):new Color(.74f,.83f,1); }
    static void Sign(Transform p,Vector3 at,string text,float width=7) {var t=Group(p,text);t.localPosition=at;t.localRotation=Quaternion.Euler(0,180,0);var tmp=t.gameObject.AddComponent<TextMeshPro>();tmp.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");tmp.text=text;tmp.fontSize=4;tmp.alignment=TextAlignmentOptions.Center;tmp.rectTransform.sizeDelta=new(width,2);tmp.color=new Color(1,.87f,.54f);}
    static void Trailer(Transform p,string name,Vector3 at,bool turn=false) {
        Start(p,name);group.localPosition=at;if(turn)group.localRotation=Quaternion.Euler(0,90,0);
        Box("Raised trailer floor",new(0,.3f,0),new(12,.6f,6),steel);Box("Roof",new(0,3.8f,0),new(12.5f,.25f,6.5f),steel);
        Box("Back wall",new(0,2,3),new(12,3.4f,.2f),cloth);foreach(float x in new[]{-6f,6f})Box("Side wall",new(x,2,0),new(.2f,3.4f,6),cloth);
        Box("Front left",new(-3.6f,2,-3),new(4.8f,3.4f,.2f),cloth);Box("Front right",new(3.6f,2,-3),new(4.8f,3.4f,.2f),cloth);Box("Door lintel",new(0,3.4f,-3),new(2.4f,.6f,.2f),cloth);
        for(int i=0;i<3;i++)Box("Entrance step",new(0,.1f+i*.2f,-4+i*.35f),new(2.5f,.2f,.4f),white);
        foreach(float x in new[]{-3.5f,3.5f}){Box("Warm office window",new(x,2.1f,-3.12f),new(2,1.1f,.03f),glow,false);Box("Desk",new(x,1.3f,1.8f),new(2,.15f,1),wood);foreach(float dx in new[]{-.8f,.8f})Box("Desk leg",new(x+dx,.9f,1.8f),new(.12f,.8f,.8f),steel);Box("Chair",new(x,.95f,.5f),new(.8f,.6f,.8f),steel);}
        Lamp(new(0,3.5f,0),true);Finish();
    }
    static void Vehicle(Transform p,string name,Vector3 at,bool mixer=false,bool car=false) {
        Start(p,name);group.localPosition=at;float w=car?2:3;float len=car?4.5f:8;
        Box("Chassis",new(0,.9f,0),new(w,.7f,len),steel);Box("Cab",new(0,2,-len/2+1.3f),new(w,1.7f,2.4f),car?red:hay);Box("Windshield",new(0,2.35f,-len/2+.07f),new(w-.3f,.8f,.06f),dark,false);
        foreach(float x in new[]{-w/2,w/2})foreach(float z in new[]{-len/2+1,len/2-1})Box("Wheel",new(x,.65f,z),new(.5f,1.2f,1.2f),dark);
        if(!car){if(mixer){for(int i=0;i<5;i++)Box("Faceted mixer drum",new(0,2.2f,-.3f+i*.75f),new(2.5f,2.5f,.8f),white,true,Quaternion.Euler(0,0,45));}else{Box("Flatbed",new(0,1.4f,1.5f),new(3,.2f,4.5f),wood);for(int i=0;i<4;i++)Box("Delivery timber",new(0,1.65f+i*.25f,1.5f),new(2.3f,.2f,4),wood);}}
        Finish();
    }
    static void Yard(Transform p) {
        Trailer(p,"South office trailer",new(-31,0,18));Trailer(p,"West office trailer",new(-36,0,32),true);
        Vehicle(p,"Delivery flatbed",new(22,0,19));Vehicle(p,"Worker car red",new(-24,0,42),false,true);Vehicle(p,"Worker car steel",new(-18,0,43),false,true);Vehicle(p,"Concrete mixer",new(23,0,121),true);
        Start(p,"Covered break area");Box("Roof",new(-24,3.4f,30),new(11,.2f,9),steel);foreach(float x in new[]{-29f,-19f})foreach(float z in new[]{26f,34f})Box("Post",new(x,1.7f,z),new(.2f,3.4f,.2f),steel);Box("Picnic table",new(-24,1,30),new(5,.2f,1.5f),wood);foreach(float z in new[]{28.5f,31.5f})Box("Bench",new(-24,.55f,z),new(5,.3f,.6f),wood);Lamp(new(-24,3.1f,30),true);Finish();
        Start(p,"South utilities and development sign");for(int i=0;i<3;i++){Box("Portable toilet",new(-39+i*2,1.25f,10),new(1.5f,2.5f,1.5f),cloth);Box("Toilet door",new(-39+i*2,1.25f,9.23f),new(1.2f,2.1f,.08f),steel);}Box("Development board",new(-22,3,10),new(10,3,.3f),steel);foreach(float x in new[]{-26f,-18f})Box("Sign post",new(x,1.5f,10),new(.2f,3,.2f),wood);Sign(group,new(-22,3,9.8f),"FUTURE HOME OF\nQUACK HEIGHTS!\nLuxury nests. Coming soon.",9);Finish();
        foreach(bool north in new[]{false,true}){float z=north?141:37;Start(p,north?"North maintenance shed":"South loading shelter");Box("Roof",new(29,4.3f,z),new(23,.3f,15),steel);Box("Back wall",new(29,2.1f,z+7.3f),new(23,4.2f,.25f),white);foreach(float x in new[]{18f,40f})foreach(float zz in new[]{z-7,z+7})Box("Column",new(x,2.1f,zz),new(.35f,4.2f,.35f),steel);for(int i=0;i<4;i++){Box("Timber stack",new(20+i*5, .65f,z+4),new(3,1.3f,2),wood);Box("Work bench",new(20+i*5,1.2f,z),new(3,.25f,1.2f),steel);}Lamp(new(29,4,z),!north);Finish();}
        Start(p,"Excavator");group.localPosition=new(-33,0,150);foreach(float x in new[]{-2f,2f})Box("Crawler track",new(x,.65f,0),new(1,1.3f,6),dark);Box("Turntable",new(0,1.5f,0),new(4,.6f,4),steel);Box("Engine housing",new(0,2.4f,1),new(4,1.4f,2),hay);Box("Operator cab",new(-1,3,-1),new(2,2.5f,2),hay);Box("Cab glazing",new(-1,3.4f,-2.03f),new(1.7f,1.4f,.05f),dark,false);Beam("Raised boom",new(1,2.5f,-1),new(1,6,-5),.7f,hay);Beam("Dipper",new(1,6,-5),new(1,1,-8),.55f,hay);Box("Bucket",new(1,.6f,-8),new(2,1.2f,1.5f),steel);Finish();
        Start(p,"Foundation excavation");Box("Pit bottom",new(-27.5f,-1.75f,133),new(25,.5f,24),grass);foreach(float x in new[]{-40f,-15f})Box("Retaining wall",new(x,-.75f,133),new(.3f,1.5f,24),white);foreach(float z in new[]{121f,145f})Box("Retaining wall",new(-27.5f,-.75f,z),new(25,1.5f,.3f),white);
        foreach(var q in new[]{new Vector3(-35,-.65f,124),new Vector3(-20,-.65f,142)})Box("Wide escape ramp",q,new(5,.25f,8),white,true,Quaternion.Euler(q.z<130?11:-11,0,0));
        foreach(float x in new[]{-35f,-27f,-19f})foreach(float z in new[]{129f,136f}){Box("Concrete footing",new(x,-1.15f,z),new(3,.7f,3),white);for(int i=0;i<3;i++)Box("Exposed rebar",new(x-.7f+i*.7f,-.1f,z),new(.07f,1.8f,.07f),steel);}Lamp(new(-39,4,136),false);Box("Light mast",new(-39,2,136),new(.15f,4,.15f),steel);Finish();
        Start(p,"Yard surface details");foreach(float z in new[]{24f,46f,119f,150f})foreach(float x in new[]{-12f,12f})Box("Muddy service track",new(x,.008f,z),new(4,.016f,8),grass,false);
        foreach(float z in new[]{14f,38f,120f,144f})foreach(float x in new[]{-42f,42f}){for(int i=0;i<4;i++)Box("Fence post",new(x,1,z+i*2.5f),new(.15f,2,.15f),steel);foreach(float y in new[]{.65f,1.5f})Beam("Fence rail",new(x,y,z),new(x,y,z+7.5f),.1f,steel);}
        for(int i=0;i<5;i++){Box("Aggregate pile",new(35+i%2*3,.45f+i*.12f,118+i*2),new(4,1+i*.25f,3),grass);Box("Stacked formwork",new(34, .15f+i*.3f,18),new(7,.2f,4),wood);}Finish();
    }
    static Collider[] surfaces;
    static bool Hit(Vector3 start,float distance,out RaycastHit hit) {hit=default;float nearest=distance;bool found=false;foreach(var c in surfaces)if(c.enabled&&!c.isTrigger&&c.Raycast(new Ray(start,Vector3.down),out var h,distance)&&h.distance<nearest){nearest=h.distance;hit=h;found=true;}return found;}
    static bool Place(Vector3 q,Bounds envelope,out Vector3 point) {point=default;if(!Hit(q+Vector3.up*2,3,out var hit)||hit.normal.y<.95f)return false;point=hit.point+Vector3.up*(.06f-envelope.min.y);var center=point+envelope.center;foreach(float x in new[]{-envelope.extents.x,envelope.extents.x})foreach(float z in new[]{-envelope.extents.z,envelope.extents.z})if(!Hit(new Vector3(center.x+x,hit.point.y+.15f,center.z+z),.3f,out var h)||Mathf.Abs(h.point.y-hit.point.y)>.08f)return false;
        var overlaps=new Collider[32];var ps=surfaces[0].gameObject.scene.GetPhysicsScene();return ps.OverlapBox(center,envelope.extents,overlaps,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0;
    }
    static void Loot(RailBlock b) {
        surfaces=b.GetComponentsInChildren<Collider>().Where(c=>c.gameObject.layer==6&&!c.isTrigger).ToArray();Physics.SyncTransforms();
        var both=RailSpawnPool.Candidates(b,RailSpawnKind.Coal).Concat(RailSpawnPool.Candidates(b,RailSpawnKind.Gold)).Select(t=>t.position).ToList();
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold}){var points=RailSpawnPool.Candidates(b,kind).ToList();var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);var own=points.Select(t=>t.position).ToList();
            foreach(var tower in RailSkyscraper.Towers)foreach(int floor in new[]{3,4}){int need=floor==3?10:20,added=0;var options=new List<Vector3>();for(float x=1.4f;x<25;x+=1.35f)for(float z=1.4f;z<23;z+=1.35f){var local=new Vector2(x,z);if(RailSkyscraper.Holes.Any(h=>new Rect(h.x-.5f,h.y-.5f,h.width+1,h.height+1).Contains(local)))continue;options.Add(tower+new Vector3(x,floor*4,z));}var rng=new System.Random(712+(int)tower.x+(int)tower.z+floor+(int)kind*131);options=options.OrderBy(v=>rng.Next()).ToList();
                foreach(var q in options){if(!Place(q,bounds,out var at)||Mathf.Abs(at.y+bounds.min.y-floor*4)>.9f||own.Any(v=>Vector3.Distance(v,at)<1.8f)||both.Any(v=>Vector3.Distance(v,at)<.8f))continue;var go=RailSpawnerPrefabs.Instantiate(kind,b.gameObject.scene);go.transform.SetParent(points[0].parent,false);go.transform.position=at;go.name=kind+" Upper polish "+points.Count+" floor "+floor;PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);PrefabUtility.RecordPrefabInstancePropertyModifications(go);points.Add(go.transform);own.Add(at);both.Add(at);if(++added==need)break;}
                if(added!=need)throw new Exception("Insufficient valid upper floor loot "+kind+" "+tower+" floor "+floor+": "+added);
            }RailSpawnPool.SetPoints(b.spawnCandidates,kind,points.ToArray());
        }
        // Former outer-yard supplies were replaced; retain their markers and relocate only unsupported yard points.
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold,RailSpawnKind.Enemy})foreach(var t in RailSpawnPool.Candidates(b,kind)){if(t.position.z>=50&&t.position.z<=112)continue;var bounds=kind==RailSpawnKind.Enemy?new Bounds(Vector3.up*.4f,new(.8f,.8f,.8f)):RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);bool placed=false;var original=t.position;for(int r=0;r<16&&!placed;r++)for(int j=0;j<12&&!placed;j++){var q=original+new Vector3(Mathf.Cos(j*Mathf.PI/6)*r,0,Mathf.Sin(j*Mathf.PI/6)*r);q.y=0;if(Mathf.Abs(q.x)<8||Mathf.Abs(q.x)>41||q.z<7||q.z>153)continue;if(Place(q,bounds,out var at)){t.position=at;PrefabUtility.RecordPrefabInstancePropertyModifications(t);placed=true;}}if(!placed)throw new Exception("Yard marker could not be supported "+t.name);}
    }
    public static void Install() {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Wrong editor state");var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Unsaved prefab edits");if(stage!=null)StageUtility.GoToMainStage();Init();var root=PrefabUtility.LoadPrefabContents(RailSkyscraper.PrefabPath);
        try{var env=root.transform.Find("Env");if(env.Find("Approved yard polish"))throw new Exception("Already installed");var site=env.Find(RailSkyscraper.RootName);var towers=site.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Tower ")&&t.parent==site).ToArray();var before=towers.SelectMany(t=>t.GetComponentsInChildren<Transform>()).ToDictionary(t=>t,t=>t.localToWorldMatrix);var b=root.GetComponent<RailBlock>();var track=b.track.ToArray();
            Object.DestroyImmediate(site.Find("Approach and departure yards").gameObject);var p=Group(env,"Approved yard polish");
            // Cut only the north yard ground tiles touched by the foundation. Preserve flat perimeter strips.
            Start(p,"Excavation ground surround");foreach(var t in site.GetComponentsInChildren<Transform>().Where(t=>t.name=="Flat construction site").ToArray()){var c=t.GetComponent<Collider>();if(!c)continue;var bb=c.bounds;float x0=Mathf.Max(bb.min.x,-40),x1=Mathf.Min(bb.max.x,-15),z0=Mathf.Max(bb.min.z,121),z1=Mathf.Min(bb.max.z,145);if(x1<=x0||z1<=z0)continue;
                Action<float,float,float,float> patch=(a,d,e,f)=>{if(d>a&&f>e)Box("Retained flat site",new((a+d)/2,-.25f,(e+f)/2),new(d-a,.5f,f-e),grass);};patch(bb.min.x,x0,bb.min.z,bb.max.z);patch(x1,bb.max.x,bb.min.z,bb.max.z);patch(x0,x1,bb.min.z,z0);patch(x0,x1,z1,bb.max.z);Object.DestroyImmediate(t.gameObject);}Finish();Yard(p);Loot(b);
            if(before.Any(kv=>kv.Key.localToWorldMatrix!=kv.Value)||!track.SequenceEqual(b.track))throw new Exception("Tower or railway changed");if(RailSpawnPool.Candidates(b,RailSpawnKind.Coal).Length!=270||RailSpawnPool.Candidates(b,RailSpawnKind.Gold).Length!=270)throw new Exception("Pool count mismatch");
            PrefabUtility.SaveAsPrefabAsset(root,RailSkyscraper.PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(Evidence+"/install.txt","PASS: retained "+before.Count+" tower transforms; track unchanged; 270 coal / 270 gold; support/footprint/overlap checks for 240 new candidates; upper floor budget 10/20 per tower and type. Yard markers adapted to new geometry. Bakes and playtest pending.");
        }finally{PrefabUtility.UnloadPrefabContents(root);}PrefabStageUtility.OpenPrefab(RailSkyscraper.PrefabPath);
    }
    public static void FinishDetails() {
        if(Lightmapping.isRunning)throw new Exception("Bake running");Init();meshId=100;var b=Object.FindFirstObjectByType<RailBlock>();var p=b.transform.Find("Env/Approved yard polish");if(p.Find("Pipe storage and puddles"))throw new Exception("Already added");
        Start(p,"Pipe storage and puddles");foreach(var at in new[]{new Vector3(35,1.1f,24),new Vector3(36,1.1f,128),new Vector3(-12,1.1f,116)})for(int k=0;k<2;k++)for(int i=0;i<12;i++){float a=i*Mathf.PI/6;Box("Hollow concrete pipe segment",at+new Vector3(Mathf.Cos(a),Mathf.Sin(a),k*3),new(.27f,.55f,2.6f),white,true,Quaternion.Euler(0,0,a*Mathf.Rad2Deg));}
        var water=NewMat("Shallow muddy puddles",steel,new Color(.16f,.19f,.17f));water.SetFloat("_Smoothness",.65f);foreach(var at in new[]{new Vector3(-11,.025f,34),new Vector3(12,.025f,23),new Vector3(-12,.025f,140),new Vector3(13,.025f,131)})Box("Shallow puddle",at,new(2.8f,.02f,5),water,false,Quaternion.Euler(0,19,0));
        foreach(float z in new[]{28.5f,31.5f})foreach(float x in new[]{-26f,-22f})Box("Break bench leg",new(x,.2f,z),new(.2f,.4f,.45f),steel);foreach(float x in new[]{-26f,-22f})Box("Table support",new(x,.45f,30),new(.25f,.9f,1),steel);
        Finish();PrefabUtility.ApplyPrefabInstance(b.gameObject,InteractionMode.AutomatedAction);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(b.gameObject.scene);
    }
    public static void CheckSaved() {
        var b=Object.FindFirstObjectByType<RailBlock>();surfaces=b.GetComponentsInChildren<Collider>().Where(c=>c.gameObject.layer==6&&!c.isTrigger).ToArray();Physics.SyncTransforms();int checks=0;void Check(bool v,string s){if(!v)throw new Exception(s);checks++;}
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold}){var all=RailSpawnPool.Candidates(b,kind);Check(all.Length==270,"Pool count");foreach(var t in all.Where(t=>t.name.Contains("Upper polish"))){var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);Check(Place(t.position-Vector3.up*.1f,bounds,out var at)&&Vector3.Distance(t.position,at)<.15f,"Upper candidate envelope "+t.name);Check(PrefabUtility.GetCorrespondingObjectFromSource(t)!=null,"Native marker reference");}}
        var probe=new Collider[16];var solid=surfaces.First(c=>c.name=="Concrete footing");Check(solid.gameObject.scene.GetPhysicsScene().OverlapBox(solid.bounds.center,Vector3.one*.1f,probe,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)>0,"Overlap positive control");
        for(int z=0;z<=160;z+=2)Check(!Physics.CheckBox(new(0,3.2f,z),new(2.9f,3.1f,.7f),Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore),"Rail clearance "+z);
        foreach(float x in new[]{-46f,46f})foreach(float z in new[]{2f,40f,120f,158f})Check(Hit(new(x,.3f,z),.5f,out var h)&&Mathf.Abs(h.point.y)<.01f,"Flat seam");
        var graph=CrawlGraph.Load(b.graph);foreach(var t in RailSpawnPool.Candidates(b,RailSpawnKind.Enemy))Check(graph.FindNearestGroundNode(b.transform.InverseTransformPoint(t.position),.8f)>=0,"Enemy navigation "+t.name);
        var lighting=AssetDatabase.LoadAssetAtPath<GameObject>(RailSkyscraper.PrefabPath).GetComponent<RailBakedBlockLighting>();Check(lighting.bindings.All(v=>v.renderer&&v.color),"Saved lighting references");Check(lighting.bindings.Any(v=>v.renderer.transform.IsChildOf(lighting.transform.Find("Env/Approved yard polish"))),"Yard lightmap receivers");File.WriteAllText(Evidence+"/saved-checks.txt",checks+" saved geometry/loot/reference/navigation checks passed. Gameplay and headset validation queued.");Debug.Log("YARD CHECKS PASS "+checks);
    }    // Follow-up operates on the current prefab stage to retain the user's unsaved edits.
    public static void FixBenchesAndLight() {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Wrong editor state");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage==null||stage.assetPath!=RailSkyscraper.PrefabPath)throw new Exception("Open skyscraper prefab first");
        var root=stage.prefabContentsRoot;var p=root.transform.Find("Env/Approved yard polish");if(p.Find("Bench supports and brighter worklights"))throw new Exception("Already installed");
        Init();meshId=200;Start(p,"Bench supports and brighter worklights");Undo.RegisterCreatedObjectUndo(group.gameObject,"Fix benches and construction lights");
        int benches=0;foreach(string name in new[]{"South loading shelter","North maintenance shed"})foreach(var t in p.Find(name).GetComponentsInChildren<Transform>().Where(t=>t.name=="Work bench")){
            var c=t.GetComponent<BoxCollider>();var center=group.InverseTransformPoint(t.TransformPoint(c.center));var size=Vector3.Scale(c.size,t.lossyScale);float bottom=center.y-size.y/2;
            foreach(float dx in new[]{-size.x/2+.18f,size.x/2-.18f})foreach(float dz in new[]{-size.z/2+.15f,size.z/2-.15f})Box("Workbench leg",new(center.x+dx,bottom/2,center.z+dz),new(.14f,bottom,.14f),steel);
            foreach(float dz in new[]{-.43f,.43f})Box("Workbench cross brace",new(center.x,.32f,center.z+dz),new(size.x-.3f,.12f,.12f),steel);benches++;
        }
        foreach(var light in root.GetComponentsInChildren<Light>()){if(light.type!=LightType.Spot)continue;Undo.RecordObject(light,"Brighter worklights");if(light.name=="Tower worklight"){light.intensity=95;light.range=17;light.color=new Color(1,.81f,.58f);}else if(light.name=="Baked yard worklight"){light.intensity=90;light.range=19;}EditorUtility.SetDirty(light);}
        // High floor accents hang below existing skyline beams; only lighting fixtures are added to towers.
        foreach(var tower in RailSkyscraper.Towers){var pos=tower+new Vector3(13,19.5f,11);Flood(pos,pos+Vector3.down*10,true,130,22,115,false);}
        foreach(var at in new[]{new Vector3(-40,5,46),new Vector3(40,5,15),new Vector3(-40,5,115),new Vector3(40,5,152)})Flood(at,new Vector3(at.x*.55f,0,at.z+(at.z<80?8:-8)),true,210,29,100,true);
        foreach(var at in new[]{new Vector3(-12,5,48),new Vector3(12,5,112)})Flood(at,new Vector3(at.x*2,1,at.z+(at.z<80?12:-12)),false,150,25,100,true);
        Finish();PrefabUtility.SaveAsPrefabAsset(root,RailSkyscraper.PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(Evidence+"/bench-lighting-followup.txt","Installed supports and cross braces for "+benches+" workbenches (32 grounded legs); tower spots 95, existing yard spots 90; four upper-floor accents and six yard floodlights, all baked. Preserved current stage edits. Bakes/validation pending.");
    }
    static void Flood(Vector3 at,Vector3 target,bool warm,float intensity,float range,float angle,bool mast){
        if(mast){Box("Floodlight mast",new(at.x,at.y/2,at.z),new(.15f,at.y,.15f),steel);Box("Floodlight foot",new(at.x,.12f,at.z),new(1,.24f,1),steel);}
        var q=Quaternion.LookRotation(target-at);Box("Floodlight housing",at,new(.9f,.5f,.3f),steel,false,q);Box("Glowing floodlight lens",at+q*Vector3.forward*.17f,new(.75f,.35f,.04f),glow,false,q);
        var l=Group(group,warm?"Warm construction floodlight":"Cool construction fill").gameObject.AddComponent<Light>();l.transform.localPosition=at+q*Vector3.forward*.2f;l.transform.localRotation=q;l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.shadows=LightShadows.Soft;l.intensity=intensity;l.range=range;l.spotAngle=angle;l.innerSpotAngle=angle*.65f;l.color=warm?new Color(1,.77f,.43f):new Color(.55f,.82f,1);
    }    public static void AddRooftopTripods() {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Wrong editor state");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Unsaved prefab edits");if(stage!=null)StageUtility.GoToMainStage();Init();meshId=300;
        var root=PrefabUtility.LoadPrefabContents(RailSkyscraper.PrefabPath);
        try{var parent=root.transform.Find("Env/Approved yard polish");if(parent.Find("Rooftop tripod light stands"))throw new Exception("Already installed");var b=root.GetComponent<RailBlock>();var lights=parent.GetComponentsInChildren<Light>().Where(l=>l.name=="Warm construction floodlight"&&l.transform.position.y>18).ToArray();if(lights.Length!=4)throw new Exception("Expected four roof lights");
            surfaces=root.GetComponentsInChildren<Collider>().Where(c=>c.gameObject.layer==6&&!c.isTrigger).ToArray();var occupied=new List<Bounds>();foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})foreach(var t in RailSpawnPool.Candidates(b,kind)){var envelope=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);envelope.center+=t.position;occupied.Add(envelope);}
            Start(parent,"Rooftop tripod light stands");int count=0;
            foreach(var l in lights){var housing=l.transform.position-l.transform.forward*.2f;Vector3 mast=default;float floor=0;bool found=false;
                for(int i=0;i<24&&!found;i++){float a=i*Mathf.PI/12;var q=housing+new Vector3(Mathf.Cos(a)*.85f,0,Mathf.Sin(a)*.85f);if(!Hit(new(q.x,17,q.z),2,out var hit)||Mathf.Abs(hit.point.y-16)>.05f)continue;
                    var area=new Bounds(new(q.x,16.55f,q.z),new(1.6f,1.1f,1.6f));if(occupied.Any(o=>o.Intersects(area)))continue;bool feet=true;for(int leg=0;leg<3;leg++){float angle=leg*Mathf.PI*2/3;var foot=hit.point+new Vector3(Mathf.Cos(angle)*.65f,0,Mathf.Sin(angle)*.65f);if(!Hit(foot+Vector3.up*.2f,.4f,out var fh)||Mathf.Abs(fh.point.y-16)>.05f)feet=false;}if(!feet)continue;mast=q;floor=hit.point.y;found=true;}
                if(!found)throw new Exception("No supported tripod footprint beside "+housing);
                // Offset mast and a short top bracket leave the downward light beam unobstructed.
                Beam("Rooftop telescopic mast",new(mast.x,floor+.55f,mast.z),mast,.12f,steel);Beam("Rooftop light mounting arm",mast,housing,.12f,steel);
                Box("Tripod collar",new(mast.x,floor+.8f,mast.z),new(.24f,.2f,.24f),hay);
                for(int leg=0;leg<3;leg++){float a=leg*Mathf.PI*2/3;var foot=new Vector3(mast.x+Mathf.Cos(a)*.65f,floor+.06f,mast.z+Mathf.Sin(a)*.65f);Beam("Tripod splayed leg",new(mast.x,floor+.85f,mast.z),foot,.1f,steel);Box("Tripod rubber foot",foot,new(.22f,.12f,.22f),dark);}
                count++;
            }Finish();PrefabUtility.SaveAsPrefabAsset(root,RailSkyscraper.PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(Evidence+"/rooftop-tripods.txt","Installed "+count+" offset telescopic poles, mounting arms and three-legged bases. All 12 feet raycast-supported on +16 m floors; footprints avoid existing loot envelopes. Lamp pose/intensity and tower structure unchanged. Bakes pending.");
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }    public static void AddCrossingPointLights() {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Wrong editor state");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Unsaved prefab edits");if(stage!=null)StageUtility.GoToMainStage();Init();meshId=400;
        var root=PrefabUtility.LoadPrefabContents(RailSkyscraper.PrefabPath);
        try{var parent=root.transform.Find("Env/Approved yard polish");if(parent.Find("Bridge and crane guide lights"))throw new Exception("Already installed");var site=root.transform.Find("Env/"+RailSkyscraper.RootName);var paths=new List<Transform>();foreach(Transform t in site.Find("Open air skybridges"))paths.Add(t);paths.Add(site.Find("Fallen crane crossing +16m"));if(paths.Count!=5||paths.Any(t=>t==null))throw new Exception("Missing crossings");
            var oldTransforms=root.GetComponentsInChildren<Transform>().ToDictionary(t=>t,t=>t.localToWorldMatrix);Start(parent,"Bridge and crane guide lights");int count=0;var lines=new List<string>();
            foreach(var path in paths){var deck=path.Find("Continuous walking deck");var box=deck.GetComponent<BoxCollider>();if(!box)throw new Exception("Deck collider missing");float length=box.size.z*deck.localScale.z,width=box.size.x*deck.localScale.x;int n=Mathf.CeilToInt(length/6);for(int i=0;i<n;i++){float z=length*(i+.5f)/n;float side=i%2==0?-1:1;var at=path.TransformPoint(new Vector3(side*width/2,1.1f,z));var q=path.rotation;
                    Box("Clamped rail light housing",at,new(.26f,.14f,.26f),steel,false,q);Box("Warm rail marker lens",at+Vector3.up*.11f,new(.18f,.14f,.18f),glow,false,q);
                    var light=Group(group,path.name+" guide light "+(i+1)).gameObject.AddComponent<Light>();light.transform.position=at+Vector3.up*.25f+path.right*(-side*.18f);light.type=LightType.Point;light.lightmapBakeType=LightmapBakeType.Baked;light.intensity=8;light.range=6.5f;light.color=new Color(1,.8f,.52f);light.shadows=LightShadows.Soft;count++;
                }lines.Add(path.name+": "+n+" lights, length "+length.ToString("F2")+" m");}
            Finish();if(oldTransforms.Any(kv=>kv.Key.localToWorldMatrix!=kv.Value))throw new Exception("Existing transform changed");PrefabUtility.SaveAsPrefabAsset(root,RailSkyscraper.PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(Evidence+"/crossing-point-lights.txt",string.Join("\n",lines)+"\nInstalled "+count+" baked point lights: intensity 8, range 6.5 m, alternate rail sides, approximately 6 m spacing. Noncolliding fixtures; existing transforms/navigation/loot unchanged. Lighting export pending.");
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }    public static void FixFoundationFloodlight() {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Wrong editor state");var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Unsaved prefab edits");if(stage!=null)StageUtility.GoToMainStage();Init();meshId=500;
        var root=PrefabUtility.LoadPrefabContents(RailSkyscraper.PrefabPath);
        try{var parent=root.transform.Find("Env/Approved yard polish");if(parent.Find("Foundation cool tripod floodlight"))throw new Exception("Already installed");var old=parent.Find("Foundation excavation");var previous=old.Find("Baked yard worklight").GetComponent<Light>();var color=previous.color;
            // The original mast shares a combined mesh with the rebar: remove only its triangles.
            var mf=old.Find("Foundation excavation Scaffold steel").GetComponent<MeshFilter>();var copy=Object.Instantiate(mf.sharedMesh);var vertices=copy.vertices;var triangles=copy.triangles;var keep=new List<int>();var cut=new Bounds(new(-39,2,136),new(.17f,4.02f,.17f));int removed=0;for(int i=0;i<triangles.Length;i+=3){var center=(vertices[triangles[i]]+vertices[triangles[i+1]]+vertices[triangles[i+2]])/3;if(cut.Contains(center)){removed++;continue;}keep.Add(triangles[i]);keep.Add(triangles[i+1]);keep.Add(triangles[i+2]);}if(removed!=12)throw new Exception("Unexpected old mast triangle count "+removed);copy.triangles=keep.ToArray();copy.RecalculateBounds();mf.sharedMesh=SaveMesh("Foundation rebar without floating mast",copy);
            Object.DestroyImmediate(old.Find("Light mast").gameObject);Object.DestroyImmediate(previous.gameObject);Object.DestroyImmediate(old.Find("Foundation excavation Crane ochre").gameObject);
            Start(parent,"Foundation cool tripod floodlight");Vector3 basePoint=new(-41,0,119),head=new(-41,4.5f,119),target=new(-26,-.1f,138.5f);surfaces=root.GetComponentsInChildren<Collider>().Where(c=>c.gameObject.layer==6&&!c.isTrigger).ToArray();
            Beam("Telescopic floodlight pole",basePoint+Vector3.up*.65f,head,.15f,steel);Box("Tripod collar",basePoint+Vector3.up*.85f,new(.26f,.22f,.26f),hay);
            for(int i=0;i<3;i++){float a=i*Mathf.PI*2/3;var foot=basePoint+new Vector3(Mathf.Cos(a)*.7f,.07f,Mathf.Sin(a)*.7f);if(!Hit(foot+Vector3.up*.25f,.5f,out var hit)||Mathf.Abs(hit.point.y)>.01f)throw new Exception("Unsupported tripod foot");Beam("Splayed tripod leg",basePoint+Vector3.up*1,foot,.12f,steel);Box("Grounded rubber foot",foot,new(.25f,.14f,.25f),dark);}
            var q=Quaternion.LookRotation(target-head);Box("Forward floodlight housing",head,new(1.2f,.75f,.35f),steel,false,q);var glass=NewMat("Cool white floodlight glass",steel,color);glass.EnableKeyword("_EMISSION");glass.SetColor("_EmissionColor",color*2);glass.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;Box("Cool white floodlight lens",head+q*Vector3.forward*.2f,new(1,.55f,.045f),glass,false,q);
            var light=Group(group,"Foundation wide cool floodlight").gameObject.AddComponent<Light>();light.transform.position=head+q*Vector3.forward*.25f;light.transform.rotation=q;light.type=LightType.Spot;light.lightmapBakeType=LightmapBakeType.Baked;light.intensity=850;light.range=40;light.spotAngle=115;light.innerSpotAngle=85;light.color=color;light.shadows=LightShadows.Soft;
            foreach(var point in new[]{new Vector3(-39,-1.4f,122),new Vector3(-16,-1.4f,122),new Vector3(-39,-1.4f,144),new Vector3(-16,-1.4f,144),new Vector3(-33,2.4f,150)})if(Vector3.Angle(light.transform.forward,point-light.transform.position)>light.spotAngle/2||Vector3.Distance(point,light.transform.position)>light.range)throw new Exception("Foundation/excavator outside light cone");
            Finish();PrefabUtility.SaveAsPrefabAsset(root,RailSkyscraper.PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(Evidence+"/foundation-tripod.txt","Replaced unsupported downward fixture with rim-mounted tripod at (-41,0,119), head 4.5 m, aimed across pit toward (-26,-0.1,138.5). Original cool light color retained; cool lens added. Intensity 850/range 40/cone 115. Three feet supported; four pit corners and excavator body inside cone/range. Removed only 12 old mast triangles and its fixture/collider; rebar preserved. Bakes pending.");
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }    public static void AddConcreteSideWalls() {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Wrong editor state");var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Unsaved prefab edits");if(stage!=null)StageUtility.GoToMainStage();Init();meshId=600;
        var root=PrefabUtility.LoadPrefabContents(RailSkyscraper.PrefabPath);
        try{var env=root.transform.Find("Env");if(env.Find("Concrete side boundary walls"))throw new Exception("Already installed");var b=root.GetComponent<RailBlock>();var previous=root.GetComponentsInChildren<Transform>().ToDictionary(t=>t,t=>t.localToWorldMatrix);var walls=Group(env,"Concrete side boundary walls");float x=b.playerHalfWidth-1;
            foreach(float side in new[]{-1f,1f}){Start(walls,side<0?"West concrete boundary":"East concrete boundary");Box("Continuous concrete wall",new(side*x,1.5f,b.length/2),new(.5f,3,b.length),white);Finish();}
            if(previous.Any(kv=>kv.Key.localToWorldMatrix!=kv.Value))throw new Exception("Existing geometry changed");
            // World-distance UVs are projected after applying dimensions, not stretched unit-cube UVs.
            int edges=0;foreach(var mf in walls.GetComponentsInChildren<MeshFilter>()){var m=mf.sharedMesh;var v=m.vertices;var uv=m.uv;var tr=m.triangles;for(int i=0;i<tr.Length;i+=3)for(int j=0;j<3;j++){int a=tr[i+j],c=tr[i+(j+1)%3];float distance=Vector3.Distance(v[a],v[c]);if(distance>.0001f&&Mathf.Abs(Vector2.Distance(uv[a],uv[c])*3-distance)>.001f)throw new Exception("Stretched wall UV");edges++;}}
            PrefabUtility.SaveAsPrefabAsset(root,RailSkyscraper.PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(Evidence+"/side-walls.txt","Farm measured: halfwidth 48, side post centers +/-47, 1 m centerline inset; 0.16 m posts (outer face 0.92 m from edge), Z2..158. Skyscraper: two solid Environment walls at +/-"+x+", height 3 m, thickness 0.5 m, full Z0.."+b.length+"; no cross-track end caps. Outer face 0.75 m inside ground edge. "+edges+" UV triangle edges verified at 1 texture repeat / 3 m, all axes; repeat-wrapped concrete texture. Existing transforms retained. Bakes pending.");
        }finally{PrefabUtility.UnloadPrefabContents(root);}
    }}
#endif
