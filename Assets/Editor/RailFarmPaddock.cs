#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Game.AI;
using Game.AI.Editor;
using Object = UnityEngine.Object;

/// <summary>Explicit, scoped replacement of Farm's northeast field. Never rebuilds the farm.</summary>
public static class RailFarmPaddock
{
    public const string RootName = "Northeast pond and playground";
    public const string Folder = "Assets/DuckRails/FarmSilos/Paddock";
    public const string Evidence = "docs/validation/farm-paddock";
    static Mesh cube;
    static Material timber, hay, green, soil, teal, yellow, water, dark;
    static readonly Dictionary<Material,List<CombineInstance>> parts = new();
    static Transform section;
    static readonly Vector2 Pond = new(25,130);
    static readonly Vector3[] Coal = {
        new(17.7f,2.4f,145.4f), new(28.5f,2.4f,146), new(40,2.4f,146),
        new(23,2.4f,146), new(35,2.4f,146), new(40.1f,1.0f,135.2f),
        new(41.6f,3,138), new(14,1,120), new(17,1,122), new(40,1,121),
        new(14,0,149), new(13.5f,0,130), new(31,0,124), new(30,0,137)
    };
    static readonly Vector3[] Gold = {
        new(18.5f,2.4f,147), new(29.5f,2.4f,147), new(40.8f,2.4f,147),
        new(21,2.4f,146), new(33,2.4f,146), new(38,2.4f,146),
        new(40.4f,2,137.6f), new(42,3,138), new(15,1,120),
        new(17.8f,1,122), new(41,1,121), new(14.7f,2,151),
        new(19,0,136), new(33.5f,0,137.5f)
    };
    static readonly Vector3[] Enemies = {
        new(12,0,120),new(44,0,120),new(13,0,139),new(44,0,152),new(28,0,152),new(34,0,138)
    };
    public static void Install()
    {
        Guard();
        Directory.CreateDirectory(Folder); Directory.CreateDirectory(Evidence);
        AssetDatabase.Refresh(); Materials();
        var root = PrefabUtility.LoadPrefabContents(RailFarmSilos.PrefabPath);
        try
        {
            var b=root.GetComponent<RailBlock>();
            if(root.transform.Find("Env/"+RootName)) throw new InvalidOperationException("Paddock already installed; edit the authored content instead of resetting it.");
            var corn=root.GetComponentsInChildren<Transform>().Single(t=>t.name=="Corn field 2");
            long before=Triangles(root), removed=Triangles(corn.gameObject);
            // Preserve all unrelated prefab edits and IDs. Snapshot is for recovery, not a regeneration input.
            Directory.CreateDirectory("Temp");File.Copy(RailFarmSilos.PrefabPath,"Temp/rails-farm-paddock-before.prefab.txt",true);
            Object.DestroyImmediate(corn.gameObject);
            var env=root.transform.Find("Env");
            var area=Group(env,RootName);
            PondGround(root); PondAndPaths(area); Playground(area); Props(area); Horsetails(area);
            Physics.SyncTransforms();
            PlaceLoot(b,RailSpawnKind.Coal,Coal);
            PlaceLoot(b,RailSpawnKind.Gold,Gold);
            var old=RailSpawnPool.Candidates(b,RailSpawnKind.Enemy).Where(t=>t.localPosition.x>10&&t.localPosition.z>117).ToArray();
            for(int i=0;i<Enemies.Length;i++)
            {
                var t=i<old.Length?old[i]:NewMarker(b,RailSpawnKind.Enemy);
                t.name="Enemy - Paddock patrol "+(i+1); t.localPosition=Enemies[i]+Vector3.up*.05f;
                PrefabUtility.RecordPrefabInstancePropertyModifications(t); PrefabUtility.RecordPrefabInstancePropertyModifications(t.gameObject);
            }
            ValidateLoot(b);
            PrefabUtility.SaveAsPrefabAsset(root,RailFarmSilos.PrefabPath);
            long added=Triangles(area.gameObject),after=Triangles(root);
            File.WriteAllText(Evidence+"/geometry.txt",$"Before {before} triangles. Removed northeast corn {removed}. Replacement {added}. After {after}. Net reduction {before-after} ({100.0*(before-after)/before:F1}% of Farm).\nOther two fields, buildings, track and existing unrelated edits preserved.\n");
            Debug.Log(File.ReadAllText(Evidence+"/geometry.txt"));
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    static void Guard()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new InvalidOperationException("Duck Rails Edit Mode required; finish bake first.");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();
        if(stage!=null&&stage.assetPath==RailFarmSilos.PrefabPath&&stage.scene.isDirty)throw new InvalidOperationException("Save Farm prefab edits first.");
    }
    static Transform Group(Transform p,string name){var g=new GameObject(name);g.transform.SetParent(p,false);g.layer=6;return g.transform;}
    static Material Mat(string name,string source,Color? color=null)
    {
        var path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/"+source+".mat"));AssetDatabase.CreateAsset(m,path);}
        if(color.HasValue)m.SetColor("_BaseColor",color.Value);
        EditorUtility.SetDirty(m);return m;
    }
    static void Materials()
    {
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/farm-silos-v1/Cube Block UV.asset");
        timber=Mat("Timber","Old timber");hay=Mat("Hay","Golden hay");green=Mat("Horsetail","Apple foliage",new Color(.35f,.57f,.19f));
        soil=Mat("Harvested soil","Farm lanes",new Color(.68f,.56f,.36f));teal=Mat("Teal roof","Silo metal",new Color(.23f,.52f,.48f));
        yellow=Mat("Slide yellow","Silo metal",new Color(.95f,.68f,.17f));dark=Mat("Dark bands","Old timber",new Color(.36f,.27f,.15f));
        water=Mat("Shallow water opaque","Silo metal",new Color(.28f,.48f,.44f));water.SetFloat("_Smoothness",.15f);
        foreach(var m in new[]{water,green,teal,yellow}){m.SetTexture("_BaseMap",null);m.SetTexture("_MainTex",null);EditorUtility.SetDirty(m);}
    }
    static Mesh SaveMesh(string name,Mesh m)
    {
        m.name=name;var path=Folder+"/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static void Start(Transform parent,string name){section=Group(parent,name);parts.Clear();}
    static void Box(string name,Vector3 pos,Vector3 size,Material mat,bool collision=true,Quaternion? rotation=null)
    {
        var rot=rotation??Quaternion.identity;
        if(!parts.TryGetValue(mat,out var list)){list=new();parts.Add(mat,list);}
        list.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(pos,rot,size)});
        if(collision){var t=Group(section,name);t.localPosition=pos;t.localRotation=rot;var c=t.gameObject.AddComponent<BoxCollider>();c.size=size;}
    }
    static void Beam(string name,Vector3 a,Vector3 b,float w,Material mat,bool collision=true)=>Box(name,(a+b)/2,new Vector3(w,(b-a).magnitude,w),mat,collision,Quaternion.FromToRotation(Vector3.up,b-a));
    static void Finish()
    {
        foreach(var kv in parts){var m=new Mesh();m.CombineMeshes(kv.Value.ToArray(),true,true);WorldUV(m);Unwrapping.GenerateSecondaryUVSet(m);MeshObject(section,section.name+" "+kv.Key.name,SaveMesh(section.name+"-"+kv.Key.name,m),kv.Key,false);}
        parts.Clear();
    }
    static void WorldUV(Mesh mesh)
    {
        var v=mesh.vertices;var n=mesh.normals;var uv=new Vector2[v.Length];
        for(int i=0;i<v.Length;i++)uv[i]=(Mathf.Abs(n[i].y)>.5f?new Vector2(v[i].x,v[i].z):Mathf.Abs(n[i].x)>.5f?new Vector2(v[i].z,v[i].y):new Vector2(v[i].x,v[i].y))/3;
        mesh.uv=uv;
    }
    public static void RefreshSurfaceUVs()
    {
        Materials();foreach(var id in AssetDatabase.FindAssets("t:Mesh",new[]{Folder}))
        {var m=AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(id));if(m.name.StartsWith("Playground towers")||m.name.StartsWith("Harvested paths")||m.name.StartsWith("Hay and fence")||m.name.StartsWith("Old paddock cart")){WorldUV(m);EditorUtility.SetDirty(m);}}
        AssetDatabase.SaveAssets();
    }
    static GameObject MeshObject(Transform parent,string name,Mesh mesh,Material mat,bool collide,bool probes=false)
    {
        var t=Group(parent,name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;
        r.receiveGI=probes?ReceiveGI.LightProbes:ReceiveGI.Lightmaps;r.lightProbeUsage=LightProbeUsage.BlendProbes;r.scaleInLightmap=.35f;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject,probes?0:StaticEditorFlags.ContributeGI);
        if(probes)r.shadowCastingMode=ShadowCastingMode.Off;
        if(collide)t.gameObject.AddComponent<MeshCollider>().sharedMesh=mesh;return t.gameObject;
    }
    static float PondRadius(float angle)=>1+.10f*Mathf.Sin(3*angle)+.06f*Mathf.Cos(5*angle);
    static float PondDistance(float x,float z){float a=Mathf.Atan2((z-Pond.y)/6.5f,(x-Pond.x)/10);return new Vector2((x-Pond.x)/10,(z-Pond.y)/6.5f).magnitude/PondRadius(a);}
    static void PondGround(GameObject root)
    {
        var f=root.GetComponentsInChildren<MeshFilter>().Single(m=>m.name=="Sculpted orchard and ditch");
        var m=Object.Instantiate(f.sharedMesh);var v=m.vertices;
        for(int i=0;i<v.Length;i++){float d=PondDistance(v[i].x,v[i].z);if(d<1.12f)v[i].y=-.38f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.70f,1.12f,d)));}
        m.vertices=v;m.RecalculateNormals();m.RecalculateBounds();f.sharedMesh=SaveMesh("Ground with shallow pond",m);f.GetComponent<MeshCollider>().sharedMesh=f.sharedMesh;
    }
    static void PondAndPaths(Transform area)
    {
        Ring(area,"Pond water",0,.89f,-.075f,water,true);
        Ring(area,"Pond shore",.91f,1.14f,.012f,soil,false);
        Ring(area,"Pond walking loop",1.22f,1.45f,.025f,soil,false);
        Start(area,"Harvested paths");
        Box("Entrance path",new Vector3(26,.018f,119),new Vector3(3,.025f,6),soil,false);
        Box("Playground ground",new Vector3(29,.018f,146),new Vector3(29,.025f,11),soil,false);Finish();
    }
    static void Ring(Transform area,string name,float inner,float outer,float y,Material mat,bool flat)
    {
        const int count=48;var v=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
        for(int i=0;i<=count;i++)
        {
            float a=i*Mathf.PI*2/count;foreach(float r in new[]{inner,outer})
            {float rr=r*PondRadius(a);float x=Pond.x+Mathf.Cos(a)*10*rr,z=Pond.y+Mathf.Sin(a)*6.5f*rr;
                float h=flat?y:Mathf.Min(0,-.38f*(1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.7f,1.12f,PondDistance(x,z)))))+y;
                v.Add(new Vector3(x,h,z));uv.Add(new Vector2(x,z)/3);}
            if(i<count){int k=i*2;tr.AddRange(new[]{k,k+2,k+1,k+1,k+2,k+3});}
        }
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(tr,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);
        MeshObject(area,name,SaveMesh(name,m),mat,false,flat);
    }
    static void Playground(Transform area)
    {
        Start(area,"Playground towers and bridges");
        foreach(float x in new[]{18f,29f,40f})
        {
            Box("Tower platform",new Vector3(x,2.25f,146),new Vector3(3.4f,.3f,3.6f),timber);
            foreach(float dx in new[]{-1.55f,1.55f})foreach(float dz in new[]{-1.65f,1.65f})Box("Tower post",new Vector3(x+dx,2.1f,146+dz),new Vector3(.25f,4.2f,.25f),timber);
            foreach(float z in new[]{144.35f,147.65f})
            {
                // Southern access opens to slide or ladder. Rear rail stays continuous.
                if(z>146)Box("Tower back rail",new Vector3(x,3.25f,z),new Vector3(3.1f,.16f,.16f),timber);
                else foreach(float dx in new[]{-1.2f,1.2f})Box("Entrance rail",new Vector3(x+dx,3.25f,z),new Vector3(.65f,.16f,.16f),timber);
            }
            if(x!=29)
            {
                foreach(float sign in new[]{-1f,1f})Box("Teal roof",new Vector3(x,4.65f,146+sign*1.02f),new Vector3(4.1f,.16f,2.35f),teal,true,Quaternion.Euler(sign*27,0,0));
                foreach(float dz in new[]{-1.65f,1.65f})Beam("Roof gable",new Vector3(x-1.5f,4.1f,146+dz),new Vector3(x+1.5f,4.1f,146+dz),.14f,timber);
            }
        }
        foreach(float start in new[]{19.7f,30.7f})
        {
            const float length=7.6f;
            // Continuous walking collider underneath visible plank seams avoids hand-catching gaps.
            Box("Bridge continuous deck",new Vector3(start+length/2,2.26f,146),new Vector3(length,.28f,2.2f),timber);
            for(int i=0;i<19;i++)Box("Bridge plank seam",new Vector3(start+i*.4f+.2f,2.407f,146),new Vector3(.025f,.012f,2.2f),dark,false);
            foreach(float z in new[]{144.9f,147.1f})
            {
                Box("Bridge handrail",new Vector3(start+length/2,3.3f,z),new Vector3(length,.14f,.14f),timber);
                for(int i=0;i<=4;i++)Box("Bridge rail post",new Vector3(start+i*length/4,2.85f,z),new Vector3(.12f,1.15f,.12f),timber);
            }
        }
        // Stair-like broad ladder treads are static and usable by the existing locomotion.
        foreach(float x in new[]{29f,40f})
        {
            for(int i=0;i<8;i++)Box("Climbing tread",new Vector3(x,.15f+i*.3f,141.15f+i*.4f),new Vector3(1.5f,.18f,.48f),timber);
            foreach(float dx in new[]{-.88f,.88f})Beam("Ladder side",new Vector3(x+dx,0,140.9f),new Vector3(x+dx,2.55f,144.2f),.17f,timber);
        }
        var a=new Vector3(18,.16f,139.2f);var b=new Vector3(18,2.4f,144.2f);var angle=Quaternion.Euler(-Mathf.Atan2(2.24f,5)*Mathf.Rad2Deg,0,0);
        Box("Static slide",(a+b)/2,new Vector3(1.5f,.16f,(b-a).magnitude),yellow,true,angle);
        foreach(float dx in new[]{-.8f,.8f})Box("Slide edge",(a+b)/2+new Vector3(dx,.16f,0),new Vector3(.12f,.32f,(b-a).magnitude),yellow,true,angle);
        // Intentionally empty swing frame: no chains, seats or runtime physics.
        Beam("Empty frame crossbeam",new Vector3(30.8f,2.8f,142.1f),new Vector3(35.8f,2.8f,142.1f),.24f,timber);
        foreach(float x in new[]{30.8f,35.8f})foreach(float z in new[]{141.2f,143f})Beam("Empty frame leg",new Vector3(x,0,z),new Vector3(x,2.8f,142.1f),.23f,timber);
        Finish();
    }
    static void Bale(Vector3 p,Vector3 size)
    {
        Box("Hay bale",p+Vector3.up*size.y/2,size,hay);
        foreach(float dx in new[]{-.28f,.28f})Box("Hay band",p+new Vector3(size.x*dx,size.y+.009f,0),new Vector3(.10f,.018f,size.z),dark,false);
    }
    static void Props(Transform area)
    {
        Start(area,"Hay and fence landmarks");
        for(int row=0;row<3;row++)for(int i=0;i<3-row;i++)Bale(new Vector3(40+i*1.6f+row*.8f,row,136+row),new Vector3(1.5f,1,2.5f));
        Bale(new Vector3(14.5f,0,151),new Vector3(3,1,1.8f));Bale(new Vector3(14.5f,1,151),new Vector3(1.4f,1,1.7f));
        Bale(new Vector3(14.5f,0,120),new Vector3(2.4f,1,1.8f));Bale(new Vector3(17.5f,0,122),new Vector3(2.4f,1,1.8f));
        foreach(var span in new[]{new Vector3(25,0,153),new Vector3(39,0,153),new Vector3(12,0,136)})
        {foreach(float dx in new[]{-2f,0,2f})Box("Fence post",span+new Vector3(dx,.65f,0),new Vector3(.16f,1.3f,.16f),timber);
            foreach(float y in new[]{.5f,1f})Box("Broken fence rail",span+new Vector3(-.3f,y,0),new Vector3(3.5f,.15f,.12f),timber);}
        Finish();
        Start(area,"Old paddock cart");
        Box("Cart bed",new Vector3(40.5f,.88f,121),new Vector3(3.2f,.24f,2),timber);
        foreach(float x in new[]{38.9f,42.1f})Box("Cart side",new Vector3(x,1.2f,121),new Vector3(.15f,.55f,2.1f),timber);
        Box("Cart back",new Vector3(40.5f,1.2f,122),new Vector3(3.2f,.55f,.15f),timber);
        foreach(float x in new[]{39.7f,41.3f})Beam("Cart handle",new Vector3(x,.8f,120),new Vector3(x,.4f,118),.12f,timber);
        foreach(float x in new[]{38.8f,42.2f})Box("Cart wheel",new Vector3(x,.48f,121),new Vector3(.18f,.9f,.9f),dark);
        Finish();
    }
    static void Horsetails(Transform area)
    {
        Start(area,"Dense horsetail clumps");var random=new System.Random(7311);
        var whorl=new Mesh();var face=new[]{new Vector3(-.20f,0,0),new Vector3(0,.06f,.20f),new Vector3(.20f,0,0),new Vector3(0,.06f,-.20f)};
        whorl.vertices=face.Concat(face).ToArray();whorl.uv=new[]{Vector2.zero,Vector2.up,Vector2.one,Vector2.right,Vector2.zero,Vector2.up,Vector2.one,Vector2.right};whorl.triangles=new[]{0,1,2,0,2,3,6,5,4,7,6,4};whorl.RecalculateNormals();
        foreach(float a in new[]{.1f,.95f,2.1f,2.9f,3.9f,5.15f})
        for(int j=0;j<18;j++)
        {
            float t=a+((float)random.NextDouble()-.5f)*.32f,r=1.03f+(float)random.NextDouble()*.10f;
            float rr=r*PondRadius(t);var p=new Vector3(Pond.x+10*Mathf.Cos(t)*rr,0,Pond.y+6.5f*Mathf.Sin(t)*rr);
            float height=.6f+(float)random.NextDouble()*.85f;
            Box("Horsetail stalk",p+Vector3.up*height/2,new Vector3(.035f,height,.035f),green,false);
            for(int k=1;k<=3;k++)parts[green].Add(new CombineInstance{mesh=whorl,transform=Matrix4x4.TRS(p+Vector3.up*(height*k/4),Quaternion.Euler(0,j*31+k*22,0),Vector3.one)});
        }
        // Combine decorative stems by material; no vegetation colliders/shadows or lightmap charts.
        foreach(var kv in parts){var m=new Mesh();m.CombineMeshes(kv.Value.ToArray(),true,true);MeshObject(section,"Horsetails",SaveMesh("Horsetails",m),kv.Key,false,true);}parts.Clear();Object.DestroyImmediate(whorl);
    }
    public static void RefreshHorsetailMesh()
    {
        Materials();var temporary=new GameObject("Temporary horsetail mesh regeneration");
        try{Horsetails(temporary.transform);}finally{Object.DestroyImmediate(temporary);}AssetDatabase.SaveAssets();
    }
    static Transform NewMarker(RailBlock b,RailSpawnKind kind)
    {
        var g=RailSpawnerPrefabs.Instantiate(kind,b.gameObject.scene);g.transform.SetParent(b.transform.Find(kind+" Spawns"),false);
        RailSpawnPool.SetPoints(b.spawnCandidates,kind,RailSpawnPool.Points(b.spawnCandidates,kind).Append(g.transform).ToArray());return g.transform;
    }
    static void PlaceLoot(RailBlock b,RailSpawnKind kind,Vector3[] positions)
    {
        var old=RailSpawnPool.Candidates(b,kind).Where(t=>t.name.Contains("Cornfield 3 hidden")).ToArray();
        if(old.Length!=positions.Length)throw new InvalidOperationException("Unexpected northeast loot count; preserve user edits and inspect.");
        var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
        for(int i=0;i<old.Length;i++)
        {
            old[i].name=kind+" - Paddock "+(i+1).ToString("00");old[i].localPosition=positions[i]+Vector3.up*(.05f-bounds.min.y);
            PrefabUtility.RecordPrefabInstancePropertyModifications(old[i]);PrefabUtility.RecordPrefabInstancePropertyModifications(old[i].gameObject);
        }
    }
    public static void ValidateLoot(RailBlock b)
    {
        Physics.SyncTransforms();var colliders=b.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var box=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            foreach(var t in RailSpawnPool.Candidates(b,kind).Where(t=>t.name.Contains("Paddock")))
            {
                var bottom=t.position+new Vector3(box.center.x,box.min.y,box.center.z);
                foreach(float x in new[]{-box.extents.x,box.extents.x})foreach(float z in new[]{-box.extents.z,box.extents.z})
                {var ray=new Ray(bottom+new Vector3(x,.12f,z),Vector3.down);if(!colliders.Any(c=>c.Raycast(ray,out var hit,.24f)))throw new Exception("Unsupported loot footprint "+t.name+" at "+t.localPosition);}
            }
        }
    }
    public static void Capture()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!=RailFarmSilos.BakeScene)throw new InvalidOperationException("Open Farm bake scene for capture.");
        var oldAmbient=RenderSettings.ambientMode;var oldColor=RenderSettings.ambientLight;bool oldFog=RenderSettings.fog;
        var oldSun=RenderSettings.sun;GameObject go=null,lamp=null;Camera cam=null;
        var lights=Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Where(l=>l.enabled).ToArray();
        RenderTexture rt=null;Texture2D tex=null;var previous=RenderTexture.active;
        try
        {
            foreach(var light in lights)light.enabled=false;
            go=new GameObject("Temporary paddock capture");SceneManager.MoveGameObjectToScene(go,scene);cam=go.AddComponent<Camera>();cam.scene=scene;cam.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);
            cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.15f,.19f,.20f);
            cam.orthographic=true;cam.orthographicSize=20;cam.farClipPlane=300;cam.transform.position=new Vector3(22,29,101);cam.transform.LookAt(new Vector3(27,0,136));
            lamp=new GameObject("Temporary review daylight");SceneManager.MoveGameObjectToScene(lamp,scene);var l=lamp.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.8f;l.transform.rotation=Quaternion.Euler(50,-30,0);RenderSettings.sun=l;
            RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.65f,.65f,.65f);
            rt=new RenderTexture(1500,1200,24);tex=new Texture2D(1500,1200,TextureFormat.RGB24,false);cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1500,1200),0,0);tex.Apply();
            Directory.CreateDirectory(Evidence);File.WriteAllBytes(Evidence+"/unity-preview.png",tex.EncodeToPNG());
        }
        finally{if(cam)cam.targetTexture=null;RenderTexture.active=previous;if(rt){rt.Release();Object.DestroyImmediate(rt);}if(tex)Object.DestroyImmediate(tex);if(go)Object.DestroyImmediate(go);if(lamp)Object.DestroyImmediate(lamp);foreach(var light in lights)if(light)light.enabled=true;RenderSettings.sun=oldSun;RenderSettings.fog=oldFog;RenderSettings.ambientMode=oldAmbient;RenderSettings.ambientLight=oldColor;}
    }
    static long Triangles(GameObject g)=>g.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh).Sum(f=>(long)f.sharedMesh.triangles.Length/3);
}
#endif
