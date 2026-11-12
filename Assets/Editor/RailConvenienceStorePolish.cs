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
using Object = UnityEngine.Object;

// Scoped, one-time screenshot-feedback pass. Never invoke the old whole-block builder.
public static partial class RailConvenienceStore
{
    const string PolishFolder=Folder+"/Polish";
    const string PolishName="Convenience polish November 11";
    static Material redGlow, greenGlow, wood, metal, dark, white, yellowDuck, orangeDuck, blue;
    static int detailMesh;
    static Transform extras;
    static RailBlock polishBlock;
    static readonly List<Transform> touchedLoot=new();
    static Mesh SaveDetail(string n,Mesh m)
    {
        m.name=n;m.RecalculateBounds();
        var path=PolishFolder+"/"+n+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(old){EditorUtility.CopySerialized(m,old);Object.DestroyImmediate(m);EditorUtility.SetDirty(old);return old;}
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static Material DetailMat(string n,Color c,Material source=null)
    {
        string path=PolishFolder+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=source?new Material(source):new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.08f);EditorUtility.SetDirty(m);return m;
    }
    static void InitPolish()
    {
        Directory.CreateDirectory(PolishFolder);Directory.CreateDirectory("docs/validation/convenience-polish");AssetDatabase.Refresh();
        RailDepotMaterials.BeginSharedPass(PolishFolder);detailMesh=0;touchedLoot.Clear();
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/convenience-store-v1/Cube Block UV.asset");
        lens=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Lamp lens.mat");
        redGlow=Special("Polish neon red",new Color(.8f,.03f,.02f),new Color(3,.05f,.02f));
        greenGlow=Special("Polish neon green",new Color(.1f,.8f,.04f),new Color(.2f,3,.03f));
        wood=AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/Old timber.mat");
        metal=AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/Silo metal.mat");
        dark=AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/Dark shingles.mat");
        white=DetailMat("Warm ivory",new Color(.76f,.74f,.62f));
        yellowDuck=DetailMat("Mascot faded yellow",new Color(.88f,.66f,.12f));orangeDuck=DetailMat("Mascot orange bill",new Color(.85f,.25f,.035f));
        blue=DetailMat("Wash blue bristles",new Color(.08f,.28f,.45f));
    }
    // Simple 12-triangle boxes with metre-scaled repeating UVs, rather than dense atlas tiles.
    static Transform Detail(Transform p,string n,Vector3 pos,Vector3 size,Material mat,bool collision=true,Quaternion? rotation=null)
    {
        var t=Group(p,n);t.localPosition=pos;t.localRotation=rotation??Quaternion.identity;t.gameObject.layer=6;
        var m=Object.Instantiate(cube);var v=m.vertices;for(int i=0;i<v.Length;i++)v[i]=Vector3.Scale(v[i],size);m.vertices=v;
        var ns=m.normals;var uv=new Vector2[v.Length];for(int i=0;i<v.Length;i++)uv[i]=(Mathf.Abs(ns[i].y)>.5f?new Vector2(v[i].x,v[i].z):Mathf.Abs(ns[i].x)>.5f?new Vector2(v[i].z,v[i].y):new Vector2(v[i].x,v[i].y))/3;m.uv=uv;
        var f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=SaveDetail("Detail "+detailMesh++,m);
        var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.25f;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);
        if(collision)t.gameObject.AddComponent<BoxCollider>().size=size;return t;
    }
    static void Beam(Transform p,string n,Vector3 a,Vector3 b,float w,Material mat,bool collision=true)
        =>Detail(p,n,(a+b)/2,new Vector3(w,(b-a).magnitude,w),mat,collision,Quaternion.FromToRotation(Vector3.up,b-a));
    static Transform Cylinder(Transform p,string n,Vector3 pos,float radius,float height,Material mat,int sides=12,bool collision=true)
    {
        var t=Group(p,n);t.localPosition=pos;t.gameObject.layer=6;var vs=new List<Vector3>();var uv=new List<Vector2>();var ts=new List<int>();
        void Tri(Vector3 a,Vector3 b,Vector3 c){int k=vs.Count;vs.AddRange(new[]{a,b,c});uv.AddRange(new[]{new Vector2(a.x,a.y),new Vector2(b.x,b.y),new Vector2(c.x,c.y)});ts.AddRange(new[]{k,k+1,k+2});}
        for(int i=0;i<sides;i++){float a=i*Mathf.PI*2/sides,b=(i+1)*Mathf.PI*2/sides;var x=new Vector3(Mathf.Cos(a)*radius,-height/2,Mathf.Sin(a)*radius);var y=new Vector3(Mathf.Cos(b)*radius,-height/2,Mathf.Sin(b)*radius);var u=x+Vector3.up*height;var v=y+Vector3.up*height;Tri(x,u,v);Tri(x,v,y);Tri(Vector3.up*height/2,v,u);Tri(-Vector3.up*height/2,x,y);}
        var m=new Mesh();m.SetVertices(vs);m.SetUVs(0,uv);m.SetTriangles(ts,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);
        t.gameObject.AddComponent<MeshFilter>().sharedMesh=SaveDetail("Cylinder "+detailMesh++,m);var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.2f;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);
        if(collision)t.gameObject.AddComponent<BoxCollider>().size=new Vector3(radius*2,height,radius*2);return t;
    }
    static void RemoveNamed(Transform p,params string[] names){foreach(var t in p.Cast<Transform>().Where(t=>names.Contains(t.name)).ToArray())Object.DestroyImmediate(t.gameObject);}
    static void SetSign(Transform sign,Material glow)
    {
        foreach(var r in sign.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Emissive trim"))r.sharedMaterial=glow;
        var txt=sign.GetComponentInChildren<TMP_Text>();txt.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
        string path=PolishFolder+"/Text "+glow.name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(txt.font.material);AssetDatabase.CreateAsset(m,path);}m.SetColor("_FaceColor",glow.GetColor("_EmissionColor"));txt.fontSharedMaterial=m;
    }
    static void MoveMarker(Transform t,Vector3 p){t.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);if(!touchedLoot.Contains(t))touchedLoot.Add(t);}
    static void AddLoot(RailSpawnKind kind,Vector3 surface,string label)
    {
        var t=RailSpawnerPrefabs.Instantiate(kind,polishBlock.gameObject.scene).transform;t.SetParent(polishBlock.transform.Find(kind+" Spawns"),false);t.name=kind+" polish "+label;
        var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);MoveMarker(t,surface+Vector3.up*(.05f-bounds.min.y));
        RailSpawnPool.SetPoints(polishBlock.spawnCandidates,kind,RailSpawnPool.Points(polishBlock.spawnCandidates,kind).Concat(new[]{t}).ToArray());
    }
    [MenuItem("Duck Rails/Authoring/Convenience Store/Install screenshot polish")]
    public static void InstallPolish()
    {
        GuardEditor();var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Save prefab edits first");if(stage!=null)StageUtility.GoToMainStage();InitPolish();
        var root=PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            polishBlock=root.GetComponent<RailBlock>();var env=root.transform.Find("Env");if(env.Find(PolishName))throw new Exception("Already installed; edit saved polish instead of rebuilding");
            var a=env.Find(RootName);var track=polishBlock.track.ToArray();extras=Group(env,PolishName);
            ShellsAndSigns(a);WashPolish(a);PicnicAndTrees(a);CourtAndTruck(a);CarsPolish(a);RestaurantPolish(a);RoofEquipment(a);Freezers(a);ArcadeMascot(a);ExteriorPolish(a);
            long before=0,after=0;int walls=0;
            foreach(var f in a.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh&&f.sharedMesh.name.StartsWith("wall-")))
            {before+=f.sharedMesh.triangles.Length/3;RailDepotMaterials.Map(f,f.GetComponent<MeshRenderer>(),"wall",0,PolishFolder,2f);after+=f.sharedMesh.triangles.Length/3;walls++;}
            if(!track.SequenceEqual(polishBlock.track))throw new Exception("Track changed");
            foreach(var txt in extras.GetComponentsInChildren<TMP_Text>())txt.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            EditorUtility.SetDirty(polishBlock);PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);AssetDatabase.SaveAssets();
            File.WriteAllText("docs/validation/convenience-polish/install.txt",$"Wall meshes: {walls}; triangles {before} -> {after}.\nPools {polishBlock.spawnCandidates.coal.Length}/{polishBlock.spawnCandidates.gold.Length}/{polishBlock.spawnCandidates.enemies.Length}.\nChanged/new loot markers {touchedLoot.Count}; original track and per-run settings retained.\n");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        PrefabStageUtility.OpenPrefab(PrefabPath);Debug.Log("Convenience screenshot polish installed. Bake/check next.");
    }
    static void ShellsAndSigns(Transform a)
    {
        foreach(var fixture in a.GetComponentsInChildren<Transform>().Where(t=>t.name=="Fixture").ToArray())
        {
            var lights=fixture.parent.GetComponentsInChildren<Light>().Where(l=>(l.transform.localPosition-(fixture.localPosition-Vector3.up*.15f)).sqrMagnitude<.02f);
            if(lights.Any(l=>l.name.ToLowerInvariant().Contains("spill")))Object.DestroyImmediate(fixture.gameObject);
        }
        var arcade=a.Find("Arcade");Detail(arcade,"Continuous upper frontage",new Vector3(-19,3.94f,76),new Vector3(.24f,.72f,20),metal);
        var store=a.Find("Store");Detail(store,"Continuous upper frontage",new Vector3(22,3.94f,74),new Vector3(.24f,.72f,24),metal);
        for(int i=0;i<4;i++)
        {
            var original=arcade.Find("Arcade cabinet 1-"+i);var copy=Object.Instantiate(original.gameObject,arcade).transform;copy.name="Arcade reverse middle row "+i;
            float x=-29+i*1.5f+.55f;var pivot=new Vector3(x,0,74.75f);foreach(Transform c in copy)c.localPosition-=pivot;
            copy.localPosition=new Vector3(x,0,76.3f);copy.localRotation=Quaternion.Euler(0,180,0);
        }
        var plaza=a.Find("Restaurant plaza");foreach(var r in plaza.Cast<Transform>().Where(t=>t.Find("Kitchen range")))
        {float x=r.Find("Roof").localPosition.x;Detail(r,"Closed roof fascia",new Vector3(x,3.65f,101.95f),new Vector3(10.1f,.4f,.32f),metal);foreach(float xx in new[]{x-5,x+5})Detail(r,"Roof side coping",new Vector3(xx,3.75f,108),new Vector3(.3f,.25f,12.3f),metal);}
        var noodle=plaza.Find("NOODLE BOX");SetSign(noodle.Find("Glowing sign NOODLE BOX"),redGlow);foreach(var l in noodle.GetComponentsInChildren<Light>())l.color=new Color(1,.15f,.07f);
        var gas=a.Find("Gas forecourt");SetSign(gas.Find("Glowing sign GAS"),redGlow);foreach(var r in gas.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Canopy neon edge"||r.name=="Pump display"))r.sharedMaterial=redGlow;
        var pole=gas.Find("Gas pylon");pole.localPosition=new Vector3(9,2.85f,99);pole.localScale=new Vector3(.3f,5.7f,.3f);
        foreach(var l in gas.GetComponentsInChildren<Light>())l.color=new Color(1,.3f,.2f);a.Find("Outer thirds").GetComponentsInChildren<Light>().First(l=>l.name=="Baked pylon spill").color=new Color(1,.06f,.03f);
    }
    static void WashPolish(Transform a)
    {
        var wash=a.Find("Car wash");RemoveNamed(wash,"Wash brush");var pivot=new Vector3(34,0,45);var q=Quaternion.Euler(0,-90,0);
        var markers=polishBlock.spawnCandidates.coal.Concat(polishBlock.spawnCandidates.gold).Where(t=>t.position.x>=27.5f&&t.position.x<=40.5f&&t.position.z>=37.5f&&t.position.z<=53).ToArray();
        foreach(var t in markers){var p=t.position;if(p.y>1&&p.y<3.5f)p.y=(t.GetComponent<RailSpawnMarker>().kind==RailSpawnKind.Coal?.2f:.05f);MoveMarker(t,pivot+q*(p-pivot));}
        foreach(Transform t in wash)t.localPosition-=pivot;wash.localPosition=pivot;wash.localRotation=q;
        SetSign(wash.Find("Glowing sign CAR WASH"),greenGlow);wash.GetComponentsInChildren<Light>().First(l=>l.name.Contains("sign")).color=new Color(.1f,1,.08f);
        var brushes=Group(wash,"Rotary wash equipment");
        foreach(float x in new[]{-3.5f,3.5f})foreach(float z in new[]{-3f,0f,3f})
        {
            Detail(brushes,"Fixed roller base",new Vector3(x,.16f,z),new Vector3(1.6f,.32f,1.6f),metal);
            Cylinder(brushes,"Spindle",new Vector3(x,1.6f,z),.12f,3,metal,8);
            var roller=Group(brushes,"Spinning bristle roller");roller.localPosition=new Vector3(x,1.65f,z);roller.gameObject.AddComponent<RailWashBrushSpin>().degreesPerSecond=x<0?38:-38;
            Cylinder(roller,"Bristle core",Vector3.zero,.55f,2.6f,blue,12,false);
            for(int k=0;k<12;k++){float ang=k*30;Detail(roller,"Flexible bristle strip",Quaternion.Euler(0,ang,0)*new Vector3(.66f,0,0),new Vector3(.35f,2.55f,.16f),k%3==0?white:blue,false,Quaternion.Euler(0,ang,0));}
            foreach(var r in roller.GetComponentsInChildren<MeshRenderer>()){GameObjectUtility.SetStaticEditorFlags(r.gameObject,0);r.receiveGI=ReceiveGI.LightProbes;r.scaleInLightmap=0;r.shadowCastingMode=ShadowCastingMode.Off;}
        }
        Detail(wash,"Overhead wash manifold",new Vector3(0,3.25f,0),new Vector3(9,.25f,.4f),metal);
        for(int i=0;i<7;i++)Detail(wash,"Spray nozzle",new Vector3(-3+i,3.04f,0),new Vector3(.15f,.2f,.2f),blue,false);
        var road=Group(extras,"Car wash access road");Detail(road,"Rail-facing access lane",new Vector3(20,.025f,43),new Vector3(6,.035f,30),dark,false);Detail(road,"South return road",new Vector3(32,.025f,30),new Vector3(24,.035f,6),dark,false);
        Detail(road,"East return lane",new Vector3(42,.025f,42),new Vector3(4,.035f,20),dark,false);
        foreach(float z in new[]{32f,38f,44f,50f})Detail(road,"Lane paint",new Vector3(20,.055f,z),new Vector3(.12f,.025f,2.5f),white,false);
    }
    static void PicnicAndTrees(Transform a)
    {
        var rest=a.Find("Rest stop");RemoveNamed(rest,"Picnic tabletop","Picnic bench");
        foreach(var p in new[]{new Vector3(-25.5f,0,33),new Vector3(-25.5f,0,40)})Picnic(rest,p);
        var grove=Group(extras,"Southwest picnic grove");
        foreach(var p in new[]{new Vector2(-36,30),new Vector2(-34,36),new Vector2(-37,40),new Vector2(-38,51),new Vector2(-29,53),new Vector2(-22,52),new Vector2(-20,29),new Vector2(-40,34)})Tree(grove,p.x,p.y);
        var trees=a.GetComponentsInChildren<Transform>().Concat(grove.GetComponentsInChildren<Transform>()).Where(t=>t.name=="Pine silhouette").Distinct().ToArray();
        for(int i=0;i<trees.Length;i++){var t=trees[i];var trunk=t.Find("Trunk");var p=new Vector3(trunk.localPosition.x,0,trunk.localPosition.z);foreach(Transform child in t)child.localPosition-=p;t.localPosition=p;t.localRotation=Quaternion.Euler(0,(i*137)%360,0);float s=.72f+(i%6)*.13f;if(t.IsChildOf(grove))s+=.25f;t.localScale=new Vector3(s,s*(.9f+(i%3)*.13f),s);}
        Detail(rest,"Picnic waste bin",new Vector3(-20,.55f,44),new Vector3(.8f,1.1f,.8f),metal);
        Lamp(rest,"Grove lantern",-35,4.3f,36,true,new Color(1,.7f,.35f));
    }
    static void Picnic(Transform p,Vector3 pos)
    {
        var g=Group(p,"Braced picnic table");g.localPosition=pos;
        for(int i=0;i<5;i++)Detail(g,"Tabletop plank",new Vector3(0,.82f,(i-2)*.27f),new Vector3(5,.13f,.25f),wood);
        foreach(float z in new[]{-1.1f,1.1f})Detail(g,"Bench plank",new Vector3(0,.46f,z),new Vector3(5,.14f,.5f),wood);
        foreach(float x in new[]{-1.7f,1.7f}){foreach(float s in new[]{-1f,1f})Beam(g,"A-frame leg",new Vector3(x,.04f,s*1.15f),new Vector3(x,.76f,s*.45f),.16f,wood);Detail(g,"Bench cross brace",new Vector3(x,.35f,0),new Vector3(.18f,.18f,2.6f),wood);}
        Beam(g,"Longitudinal brace",new Vector3(-1.7f,.35f,0),new Vector3(1.7f,.35f,0),.14f,wood);
    }
    static void ExteriorPolish(Transform a)
    {
        var street=a.Find("Road and parking");RemoveNamed(street,"Parking fence post","Broken parking fence");
        foreach(var t in a.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("stripe")||t.name=="Crosswalk"))
        {var p=t.localPosition;p.y=.055f;t.localPosition=p;foreach(var c in t.GetComponents<Collider>())Object.DestroyImmediate(c);}
        var lamps=Group(extras,"Bilateral trackside streetlights");foreach(float x in new[]{-8.5f,8.5f})foreach(float z in new[]{28f,50f,72f,94f,120f,142f})Lamp(lamps,"Trackside streetlight",x,6,z,true,new Color(1,.77f,.46f));
        var fence=Group(extras,"Wooden side fences 1m inset");foreach(float x in new[]{-47f,47f})
        {for(float z=1;z<=159;z+=4)Detail(fence,"Boundary post",new Vector3(x,.8f,z),new Vector3(.18f,1.6f,.18f),wood);for(float z=0;z<160;z+=4)foreach(float y in new[]{.48f,1.15f})Detail(fence,"Boundary rail",new Vector3(x,y,z+2),new Vector3(.14f,.16f,4),wood);}
    }
}
#endif
