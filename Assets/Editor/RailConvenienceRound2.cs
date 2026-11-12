#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using TMPro;
using Object=UnityEngine.Object;

public static partial class RailConvenienceStore
{
    const string R2Folder=Folder+"/Round2";
    const string R2Evidence="docs/validation/convenience-round2";
    static Transform r2, authored, prior;
    static readonly HashSet<Transform> permitted=new();
    static Material pink2,yellow2,ivory2,black2,red2,carBlue2;
    static void Permit(Transform t){permitted.Add(t);}
    static void Delete2(Transform t){if(!t)return;foreach(var c in t.GetComponentsInChildren<Transform>(true))Permit(c);Object.DestroyImmediate(t.gameObject);}
    static void Move2(Transform t,Vector3 p){Permit(t);t.localPosition=p;}
    static Mesh Save2(string n,Mesh m)
    {m.name=n;m.RecalculateBounds();string path=R2Folder+"/"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;}AssetDatabase.CreateAsset(m,path);return m;}
    static Material Mat2(string n,Color c,Material source=null)
    {string path=R2Folder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=source?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(m);return m;}
    static void Init2()
    {
        Directory.CreateDirectory(R2Folder);Directory.CreateDirectory(R2Evidence);AssetDatabase.Refresh();detailMesh=3000;permitted.Clear();
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/convenience-store-v1/Cube Block UV.asset");
        metal=AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/Silo metal.mat");wood=AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/Old timber.mat");dark=AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/Dark shingles.mat");
        white=AssetDatabase.LoadAssetAtPath<Material>(PolishFolder+"/Warm ivory.mat");blue=AssetDatabase.LoadAssetAtPath<Material>(PolishFolder+"/Wash blue bristles.mat");
        pink2=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Neon pink.mat");yellow2=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Neon yellow.mat");
        if(!yellow2)throw new Exception("User yellow neon material missing");
        ivory2=Mat2("Mascot cream",new Color(.88f,.81f,.59f));black2=Mat2("Rubber and pupil",new Color(.035f,.045f,.05f));red2=Mat2("Crossing red",new Color(.7f,.04f,.025f));carBlue2=Mat2("Van faded blue",new Color(.22f,.34f,.42f));
    }
    public static void InstallRound2()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Duck Rails Edit Mode required");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage==null||stage.assetPath!=PrefabPath)throw new Exception("Open current Convenience Store prefab");
        var b=stage.prefabContentsRoot.GetComponent<RailBlock>();if(b.transform.Find("Env/Convenience round two"))throw new Exception("Round two already installed; do not rebuild");
        Init2();polishBlock=b;authored=b.transform.Find("Env/"+RootName);prior=b.transform.Find("Env/"+PolishName);
        // Snapshot the actual current objects, including every user move/deletion saved before this pass.
        var snapshots=b.GetComponentsInChildren<Transform>(true).Select(t=>(t,t.parent,t.localPosition,t.localRotation,t.localScale,t.gameObject.activeSelf)).ToArray();
        Undo.RegisterFullObjectHierarchyUndo(b.gameObject,"Convenience round two");r2=Group(b.transform.Find("Env"),"Convenience round two");
        AssetDatabase.StartAssetEditing();
        try{RestaurantRound2();WashRound2();MoveLamps2();CourtRound2();VanRound2();ArcadeRound2();CrossingsRound2();RooftopModels2();AtlasRound2(b);}
        finally{AssetDatabase.StopAssetEditing();}
        var violations=new List<string>();int unchanged=0;foreach(var s in snapshots){if(permitted.Contains(s.t))continue;if(!s.t||s.t.parent!=s.parent||s.t.localPosition!=s.localPosition||s.t.localRotation!=s.localRotation||s.t.localScale!=s.localScale||s.t.gameObject.activeSelf!=s.activeSelf)violations.Add(s.t?s.t.name:"Deleted existing object");else unchanged++;}
        if(violations.Count>0)throw new Exception("Unexpected transform changes: "+string.Join(",",violations));
        File.WriteAllText(R2Evidence+"/preservation.txt",$"PASS {unchanged} original transforms/parents/active states preserved exactly; {permitted.Count} explicitly targeted objects altered or replaced.\nNo baseline regeneration; deleted objects are never restored.\nSemi: {prior.Find("Broken articulated semi").position} yaw={prior.Find("Broken articulated semi").eulerAngles.y}\n");
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();Debug.Log("Round two models installed; loot redistribution is the next separate step.");
    }
    static void RestaurantRound2()
    {
        var plaza=authored.Find("Restaurant plaza");foreach(string name in new[]{"LAST SLICE","DUCK DINER","NOODLE BOX"})
        {
            var room=plaza.Find(name);float cx=room.Find("Roof").localPosition.x;
            var g=Group(room,"Round two storefront and closed rear door");
            Detail(g,"Closed ornamental rear double door",new Vector3(cx-.5f,1.35f,114),new Vector3(3,2.7f,.22f),metal);
            foreach(float x in new[]{cx-2,cx+1})Detail(g,"Rear door jamb",new Vector3(x,1.4f,114.15f),new Vector3(.12f,2.8f,.1f),black2);
            Detail(g,"Rear door centre seam",new Vector3(cx-.5f,1.35f,114.14f),new Vector3(.045f,2.7f,.04f),black2,false);
            foreach(float x in new[]{cx-.75f,cx-.25f})Detail(g,"Rear door handle",new Vector3(x,1.2f,114.2f),new Vector3(.07f,.35f,.07f),ivory2,false);
            foreach(var wall in room.Cast<Transform>().Where(t=>t.name=="Wall"&&Mathf.Abs(t.localPosition.z-102)<.01f&&t.localScale.y>3).ToArray())
            {
                float center=wall.localPosition.x,width=wall.localScale.x;var mats=wall.GetComponent<MeshRenderer>().sharedMaterials;var src=wall.GetComponent<MeshFilter>().sharedMesh;
                // Replace only these two solid frontage sections with a low wall and an empty glazing frame.
                var sill=Object.Instantiate(wall.gameObject,g).transform;sill.name="Former window low wall";sill.localPosition=new Vector3(center,.4f,102);sill.localScale=new Vector3(width,.8f,.2f);
                Delete2(wall);
                Detail(g,"Empty window upper frame",new Vector3(center,2.65f,101.9f),new Vector3(width,.14f,.18f),metal);
                Detail(g,"Empty window sill",new Vector3(center,.85f,101.9f),new Vector3(width,.12f,.3f),metal);
                foreach(float x in new[]{center-width/2+.08f,center,center+width/2-.08f})Detail(g,"Former glazing mullion",new Vector3(x,1.72f,101.9f),new Vector3(.09f,1.8f,.14f),metal);
                Detail(g,"Window header infill",new Vector3(center,3.15f,102),new Vector3(width,.9f,.2f),metal);
            }
        }
    }
    static void WashRound2()
    {
        var wash=authored.Find("Car wash");var eq=wash.Find("Rotary wash equipment");var rollers=eq.Cast<Transform>().Where(t=>t.GetComponent<RailWashBrushSpin>()).ToArray();
        var changes=new[]{(index:1,p:new Vector3(-.9f,1.65f,.5f)),(index:3,p:new Vector3(1.4f,1.65f,-2.7f)),(index:5,p:new Vector3(.8f,1.65f,3.2f))};
        var combined=eq.GetComponentInChildren<MeshFilter>();var mesh=Object.Instantiate(eq.Cast<Transform>().First(t=>t.name.StartsWith("Combined details")).GetComponent<MeshFilter>().sharedMesh);var verts=mesh.vertices;
        foreach(var change in changes)
        {
            var roller=rollers[change.index];var before=roller.localPosition;var delta=change.p-before;
            for(int i=0;i<verts.Length;i++)if(Mathf.Abs(verts[i].x-before.x)<.85f&&Mathf.Abs(verts[i].z-before.z)<.85f)verts[i]+=delta;
            foreach(var t in eq.Cast<Transform>().Where(t=>(t.name=="Fixed roller base"||t.name=="Spindle")&&Mathf.Abs(t.localPosition.x-before.x)<.01f&&Mathf.Abs(t.localPosition.z-before.z)<.01f).ToArray())Move2(t,t.localPosition+delta);
            Move2(roller,change.p);
        }
        mesh.vertices=verts;mesh.RecalculateBounds();eq.Cast<Transform>().First(t=>t.name.StartsWith("Combined details")).GetComponent<MeshFilter>().sharedMesh=Save2("Repositioned brush bases",mesh);
        foreach(var roller in rollers){var c=Group(roller.parent,"Stationary brush pillar collision");c.localPosition=roller.localPosition;c.gameObject.layer=6;var capsule=c.gameObject.AddComponent<CapsuleCollider>();capsule.radius=.76f;capsule.height=2.6f;}
        var horizontal=Object.Instantiate(rollers[0].gameObject,wash).transform;horizontal.name="Horizontal rear scrub roller";horizontal.localPosition=new Vector3(0,3.15f,-5.1f);horizontal.localRotation=Quaternion.Euler(0,0,90);horizontal.localScale=new Vector3(.8f,1.7f,.8f);
        foreach(float x in new[]{-2.4f,2.4f})Detail(wash,"Rear roller gantry",new Vector3(x,1.85f,-5.1f),new Vector3(.18f,3.7f,.25f),metal);
        Detail(wash,"Rear roller crossbar",new Vector3(0,3.75f,-5.1f),new Vector3(5,.2f,.3f),metal);
        // A stationary envelope keeps moving bristles cosmetic; 2.5m minimum clearance beneath.
        Detail(wash,"Horizontal roller stationary core",new Vector3(0,3.15f,-5.1f),new Vector3(4.5f,.95f,.95f),metal,true).GetComponent<MeshRenderer>().enabled=false;
    }
    static void MoveLamp2(Transform parent,string lightName,Vector3 to)
    {
        var light=parent.Cast<Transform>().First(t=>t.name==lightName);var from=light.localPosition;var delta=new Vector3(to.x-from.x,0,to.z-from.z);
        foreach(var t in parent.Cast<Transform>().Where(t=>t==light||((t.name=="Fixture"||t.name=="Lamp post")&&Vector2.Distance(new Vector2(t.localPosition.x,t.localPosition.z),new Vector2(from.x,from.z))<.2f)).ToArray())Move2(t,t.localPosition+delta);
    }
    static void MoveLamps2(){MoveLamp2(authored.Find("Rest stop"),"Grove lantern",new Vector3(-21,0,36));MoveLamp2(authored.Find("Outer thirds"),"Baked utility lamp",new Vector3(-27,0,119));}
    static void CourtRound2()
    {
        var court=prior.Find("Northwest basketball court");Delete2(court.Find("Open court gate"));
        var opening=new Bounds(new Vector3(-20,1.5f,120),new Vector3(4,3.4f,.4f));
        foreach(var f in court.GetComponentsInChildren<MeshFilter>().ToArray())if(f.GetComponent<Renderer>().bounds.Intersects(opening))f.sharedMesh=CutBox2(f.sharedMesh,f.transform,court,opening,"Court opening "+f.GetInstanceID());
        foreach(var c in court.GetComponentsInChildren<Collider>().ToArray())
        {
            if(!c.bounds.Intersects(opening))continue;
            // The old south continuous collision is replaced by two segments around the new side gap.
            if(c.name=="Court fence collision"&&c.bounds.size.x>10){Delete2(c.transform);continue;}
            if(c.bounds.center.x> -22&&c.bounds.center.x< -18&&Mathf.Abs(c.bounds.center.z-120)<.3f)Delete2(c.transform);
            else if(c.bounds.size.x>10&&Mathf.Abs(c.bounds.center.z-120)<.3f)Object.DestroyImmediate(c);
        }
        var g=Group(court,"Open south sideline entrance");
        foreach(var pair in new[]{new Vector2(-39,-22),new Vector2(-18,-15)}){var t=Group(g,"South fence collision");t.gameObject.layer=6;t.localPosition=new Vector3((pair.x+pair.y)/2,1.5f,120);t.gameObject.AddComponent<BoxCollider>().size=new Vector3(pair.y-pair.x,3,.08f);}
        foreach(float x in new[]{-22f,-18f})Detail(g,"Entrance post",new Vector3(x,1.5f,120),new Vector3(.11f,3,.11f),metal);
        CourtFence(g,new Vector3(-15,0,127.5f),new Vector3(-15,0,130.5f));
        var trees=Group(r2,"Court perimeter grove");int i=0;
        foreach(var p in new[]{new Vector2(-41.7f,124),new Vector2(-42,133),new Vector2(-35,143),new Vector2(-28,147),new Vector2(-18,145),new Vector2(-36,153),new Vector2(-23,153),new Vector2(-33,116),new Vector2(-11.7f,119)})
        {
            var template=authored.GetComponentsInChildren<Transform>().First(t=>t.name=="Pine silhouette");var t=Object.Instantiate(template.gameObject,trees).transform;t.name="Large court pine "+i;t.localPosition=new Vector3(p.x,0,p.y);t.localRotation=Quaternion.Euler(0,i*73,0);float s=1.15f+(i%3)*.13f;t.localScale=new Vector3(s,s*(1.08f+(i%2)*.12f),s);i++;
        }
    }
    static void VanRound2()
    {
        var old=authored.Find("Service loop/Abandoned delivery van");var body=old.Find("Body");var at=body.localPosition;at.y=0;var p=Group(old.parent,"Basic abandoned delivery van");p.localPosition=old.TransformPoint(at);p.rotation=old.rotation;
        Detail(p,"Van chassis",new Vector3(0,.65f,0),new Vector3(2.6f,.4f,5.5f),black2);Detail(p,"Cargo body",new Vector3(0,1.5f,-.6f),new Vector3(2.6f,1.8f,3.8f),carBlue2);Detail(p,"Cab",new Vector3(0,1.45f,1.55f),new Vector3(2.5f,1.7f,1.6f),carBlue2);
        var glassMat=Mat2("Van dark windows",new Color(.1f,.19f,.23f));Detail(p,"Windshield",new Vector3(0,1.8f,2.37f),new Vector3(2.15f,.75f,.05f),glassMat,false);foreach(float x in new[]{-1.27f,1.27f})Detail(p,"Cab side window",new Vector3(x,1.8f,1.5f),new Vector3(.04f,.7f,1.15f),glassMat,false);
        foreach(float x in new[]{-1.28f,1.28f})foreach(float z in new[]{-1.8f,1.75f})Wheel(p,new Vector3(x,.48f,z),.48f,.32f);
        foreach(float z in new[]{-2.8f,2.5f})Detail(p,"Bumper",new Vector3(0,.6f,z),new Vector3(2.7f,.22f,.18f),metal);
        foreach(float x in new[]{-.85f,.85f})Detail(p,"Headlamp",new Vector3(x,1,2.4f),new Vector3(.45f,.3f,.07f),ivory2,false);
        Detail(p,"Rear door seam",new Vector3(0,1.5f,-2.53f),new Vector3(.035f,1.65f,.035f),black2,false);Delete2(old);
    }
    static void CrossingsRound2()
    {
        var street=authored.Find("Road and parking");foreach(var t in street.Cast<Transform>().Where(t=>new[]{"Crossing base","Raised crossing gate","Signal head","Red signal lens"}.Contains(t.name)).ToArray())Delete2(t);
        var g=Group(r2,"Lowered railway crossing barriers");var glow=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Polish neon red.mat");
        foreach(var spec in new[]{new Vector3(-9,0,104),new Vector3(9,0,112)})
        {
            var p=Group(g,"Lowered road gate");p.localPosition=spec;float direction=spec.x<0?1:-1;
            Detail(p,"Motor pedestal",new Vector3(0,.65f,0),new Vector3(.7f,1.3f,.65f),metal);
            // Arms run along Z across the road lanes; never point across the X=0 railway.
            for(int i=0;i<10;i++)Detail(p,"Red white barrier stripe",new Vector3(0,1.25f,direction*(.3f+i*.4f)),new Vector3(.18f,.24f,.4f),i%2==0?ivory2:red2,false);
            p.gameObject.layer=6;var arm=p.gameObject.AddComponent<BoxCollider>();arm.center=new Vector3(0,1.25f,direction*2.1f);arm.size=new Vector3(.18f,.24f,4);
            Detail(p,"Signal pole",new Vector3(-direction*.4f,1.65f,0),new Vector3(.12f,3.3f,.12f),metal);
            Detail(p,"Signal housing",new Vector3(-direction*.4f,2.7f,0),new Vector3(.28f,.55f,1.35f),black2);
            foreach(float z in new[]{-.4f,.4f}){var lamp=Cylinder(p,"Red crossing lamp",new Vector3(-direction*.57f,2.7f,z),.2f,.09f,glow,12,false);lamp.localRotation=Quaternion.Euler(0,0,90);}
            var l=Group(p,"Baked crossing red spill").gameObject.AddComponent<Light>();l.transform.localPosition=new Vector3(-direction*.7f,2.5f,0);l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;l.range=5;l.intensity=5;l.color=Color.red;
        }
    }
}
#endif
