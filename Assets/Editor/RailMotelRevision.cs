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
public static class RailMotelRevision
{
    const string F="Assets/DuckRails/MotelLeisure/Revision";
    static Transform p;
    static Mesh cube;
    static Material white,blue,metal,dark,cream,teal,red,glow,poolGlow,yellow;
    static int meshId;
    public static readonly int[] Closed={103,105,201,203,205,206,208};
    static Transform G(Transform parent,string n){var t=new GameObject(n).transform;t.SetParent(parent,false);return t;}
    static Material M(string n,Color c,float emission=0){string path=F+"/"+n+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}m.SetColor("_BaseColor",c);m.SetFloat("_Smoothness",.12f);if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",c*emission);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}return m;}
    static void Init(){Directory.CreateDirectory(F);RailDepotMaterials.BeginSharedPass(F+"/Meshes");cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/Cube Block UV.asset");white=M("White concrete",new Color(.8f,.81f,.77f));blue=M("Blue trim",new Color(.08f,.25f,.58f));metal=M("Dull metal",new Color(.25f,.28f,.3f));dark=M("Window and tyre",new Color(.035f,.06f,.075f));cream=M("Cream enamel",new Color(.7f,.65f,.48f));teal=M("Pool tile",new Color(.23f,.49f,.48f));red=M("Rust red car",new Color(.38f,.1f,.07f));glow=M("White neon",Color.white,5);poolGlow=M("Pool cyan neon",new Color(.1f,.85f,1),5);yellow=M("Slide yellow",new Color(.72f,.54f,.17f));meshId=0;}
    static Transform B(string n,Vector3 pos,Vector3 size,Material mat=null,string kind="concrete",bool solid=true)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.layer=6;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;var f=g.GetComponent<MeshFilter>();f.sharedMesh=cube;var r=g.GetComponent<MeshRenderer>();
        if(mat)r.sharedMaterial=mat;else RailDepotMaterials.Map(f,r,kind,0,F+"/Meshes",2);
        r.scaleInLightmap=.3f;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);if(!solid)Object.DestroyImmediate(g.GetComponent<Collider>());return g.transform;
    }
    static void Beam(string n,Vector3 a,Vector3 b,float w,Material m=null){var t=B(n,(a+b)/2,new Vector3(w,w,(b-a).magnitude),m??metal);t.localRotation=Quaternion.LookRotation(b-a);}
    static void Delete(Transform root,Func<Transform,bool> test){foreach(var t in root.Cast<Transform>().Where(test).ToArray())Object.DestroyImmediate(t.gameObject);}
    // Rectangle openings cut actual visual/collision walls, not decals.
    static void Wall(bool alongZ,float axis,float u0,float u1,float y,float h,params Rect[] holes)
    {
        var us=new[]{u0,u1}.Concat(holes.SelectMany(q=>new[]{q.xMin,q.xMax})).Where(v=>v>=u0&&v<=u1).Distinct().OrderBy(v=>v).ToArray();
        var ys=new[]{y,y+h}.Concat(holes.SelectMany(q=>new[]{q.yMin,q.yMax})).Where(v=>v>=y&&v<=y+h).Distinct().OrderBy(v=>v).ToArray();
        for(int i=0;i<us.Length-1;i++)for(int j=0;j<ys.Length-1;j++){float u=(us[i]+us[i+1])/2,v=(ys[j]+ys[j+1])/2;if(holes.Any(q=>q.Contains(new Vector2(u,v))))continue;B("Revised wall",alongZ?new Vector3(axis,v,u):new Vector3(u,v,axis),alongZ?new Vector3(.2f,ys[j+1]-ys[j],us[i+1]-us[i]):new Vector3(us[i+1]-us[i],ys[j+1]-ys[j],.2f),kind:"wall");}
    }
    static void Window(bool alongZ,float axis,float u,float y,float w=2.3f)
    {
        foreach(float dy in new[]{0f,1.5f})B("Open window sill",alongZ?new Vector3(axis,y+dy,u):new Vector3(u,y+dy,axis),alongZ?new Vector3(.35f,.1f,w+.2f):new Vector3(w+.2f,.1f,.35f),cream);
        foreach(float du in new[]{-w/2,w/2})B("Window jamb",alongZ?new Vector3(axis,y+.75f,u+du):new Vector3(u+du,y+.75f,axis),alongZ?new Vector3(.3f,1.5f,.1f):new Vector3(.1f,1.5f,.3f),cream);
    }
    static void LightAt(string n,Vector3 pos,Vector3 target,float intensity,float range,Color color,float angle=110)
    {var t=G(p,n);t.localPosition=pos;t.localRotation=Quaternion.LookRotation(target-pos);var l=t.gameObject.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=intensity;l.range=range;l.color=color;l.spotAngle=angle;l.innerSpotAngle=65;l.shadows=LightShadows.Soft;}
    static void Motel(Transform root)
    {
        for(int floor=0;floor<2;floor++)
        {
            p=root.Find("Floor "+(floor+1));float y=floor*3.6f,h=floor==0?3.4f:3.6f;
            Delete(p,t=>t.name=="Wall"||t.name.StartsWith("Ajar door")||int.TryParse(t.name,out _));
            foreach(var label in p.GetComponentsInChildren<TMP_Text>().Where(t=>int.TryParse(t.text,out _)).ToArray())Object.DestroyImmediate(label.gameObject);
            for(int i=0;i<5;i++)
            {
                float z=54+i*8;int id=(floor+1)*100+i+1;bool shut=Closed.Contains(id);
                if(shut){Delete(p,t=>t.name.EndsWith(" "+id));B("Closed room solid "+id,new Vector3(34,y+h/2,z+4),new Vector3(11.3f,h,7.7f),kind:"wall");}
                var door=new Rect(z+1,y,1.5f,2.6f);var win=new Rect(z+4.8f,y+1.1f,2.3f,1.5f);
                Wall(true,28.1f,z,z+8,y,h,door,win);
                if(shut){B("Closed door "+id,new Vector3(28.06f,y+1.3f,z+1.75f),new Vector3(.12f,2.6f,1.5f),blue);B("Closed window recess "+id,new Vector3(28.25f,y+1.85f,z+5.95f),new Vector3(.12f,1.5f,2.3f),dark);}
                else{B("Open door leaf "+id,new Vector3(28.85f,y+1.3f,z+1.04f),new Vector3(1.5f,2.6f,.1f),blue);Wall(false,z+4,36,40,y,h);Wall(true,36,z+4,z+8,y,h,new Rect(z+4.6f,y,1.2f,2.6f));}
                Window(true,28.08f,z+5.95f,y+1.1f);
                Wall(true,39.9f,z,z+8,y,h,shut?Array.Empty<Rect>():new[]{new Rect(z+1,y+1.1f,2.3f,1.5f)});
                if(!shut)Window(true,39.9f,z+2.15f,y+1.1f);
                // Every room gets a fixture; exactly half the sixteen facade fixtures emit.
                B((i+floor)%2==0?"Working room fixture":"Broken room fixture",new Vector3(27.9f,y+2.9f,z+3.3f),new Vector3(.3f,.25f,.55f),(i+floor)%2==0?glow:dark);
                if((i+floor)%2==0)LightAt("Room frontage light",new Vector3(27.7f,y+2.8f,z+3.3f),new Vector3(24,y,z+3.3f),20,9,new Color(1,.82f,.6f));
            }
            for(int i=0;i<=5;i++)Wall(false,i==0?54.1f:i==5?93.9f:54+i*8,28,40,y,h,
                floor==0&&i==1?new[]{new Rect(30,y,1.5f,2.6f)}:Array.Empty<Rect>());
            for(int i=0;i<3;i++)
            {
                float x=10+i*10;int id=206+i;bool shut=floor==1&&Closed.Contains(id);
                if(shut){Delete(p,t=>t.name.EndsWith(" "+id));B("Closed room solid "+id,new Vector3(x+5,y+h/2,103),new Vector3(9.7f,h,11.4f),kind:"wall");}
                Wall(false,97.1f,x,x+10,y,h,new Rect(x+1,y,1.2f,2.6f),new Rect(x+5,y+1.1f,2.3f,1.5f));
                if(shut){B("Closed door "+id,new Vector3(x+1.6f,y+1.3f,97.04f),new Vector3(1.2f,2.6f,.12f),blue);B("Closed window recess "+id,new Vector3(x+6.15f,y+1.85f,97.25f),new Vector3(2.3f,1.5f,.12f),dark);}
                Window(false,97.08f,x+6.15f,y+1.1f);
                Wall(false,108.9f,x,x+10,y,h,shut?Array.Empty<Rect>():new[]{new Rect(x+3,y+1.1f,2.3f,1.5f)});
                if(!shut)Window(false,108.9f,x+4.15f,y+1.1f);
                if(floor==1&&!shut){Wall(true,x+6,105,109,y,h);Wall(false,105,x+6,x+10,y,h,new Rect(x+6.5f,y,1.2f,2.6f));}
                B((i+floor)%2==0?"Working room fixture":"Broken room fixture",new Vector3(x+3.5f,y+2.9f,96.9f),new Vector3(.55f,.25f,.3f),(i+floor)%2==0?glow:dark);
                if((i+floor)%2==0)LightAt("Room frontage light",new Vector3(x+3.5f,y+2.8f,96.7f),new Vector3(x+3.5f,y,93),20,9,new Color(1,.82f,.6f));
            }
            Wall(true,10.1f,97,109,y,h,floor==0?new[]{new Rect(101,y,1.2f,2.6f),new Rect(105,y+1.1f,2.3f,1.5f)}:Array.Empty<Rect>());
            Wall(true,39.9f,97,109,y,h,floor==0?new[]{new Rect(101,y+1.1f,2.3f,1.5f)}:Array.Empty<Rect>());
            Wall(true,20,97,109,y,h);Wall(true,30,97,109,y,h);
            // Remove light sources embedded in now-solid closed rooms.
            foreach(var l in p.GetComponentsInChildren<Light>().ToArray()){var q=l.transform.localPosition;bool blocked=q.x>28&&q.z<94&&Closed.Contains((floor+1)*100+Mathf.FloorToInt((q.z-54)/8)+1)||floor==1&&q.z>=97&&Closed.Contains(206+Mathf.FloorToInt((q.x-10)/10));if(blocked)Object.DestroyImmediate(l.gameObject);}
        }
        p=root.Find("Floor 1");B("Connecting internal door 101-102",new Vector3(30.05f,1.3f,62.75f),new Vector3(.1f,2.6f,1.5f),blue);
        foreach(string name in new[]{"A","B"})
        {
            p=root.Find("Stair "+name);Delete(p,t=>t.name.StartsWith("Guard"));float z=name=="A"?54:85.2f,x=name=="A"?22.6f:10;
            foreach(float xx in new[]{x,x+2.4f}){Beam("Open stair handrail",new Vector3(xx,1.05f,z),new Vector3(xx,4.65f,z+6.4f),.1f);for(int i=0;i<=10;i++){float t=i/10f;Beam("Stair baluster",new Vector3(xx,t*3.6f,z+t*6.4f),new Vector3(xx,t*3.6f+1.05f,z+t*6.4f),.07f);}}
        }
    }
    // Sample the approved CutePixel glyph atlas and extrude opaque pixel runs into real solid geometry.
    public static void AddMeshSign(Transform parent,string word,Vector3 pos,float width,float height,float yaw,Material mat){p=parent;cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/Cube Block UV.asset");MeshLetters(word,pos,width,height,yaw,mat);}
    static void MeshLetters(string word,Vector3 pos,float width,float height,float yaw,Material mat)
    {
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");font.ReadFontAssetDefinition();
        var atlas=font.atlasTexture;var rt=RenderTexture.GetTemporary(atlas.width,atlas.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);Graphics.Blit(atlas,rt);var previous=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(atlas.width,atlas.height,TextureFormat.RGBA32,false,true);tex.ReadPixels(new Rect(0,0,atlas.width,atlas.height),0,0);tex.Apply();RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);
        var parent=p;var group=G(p,word+" solid CutePixel letters");p=group;float advance=word.Sum(ch=>font.characterLookupTable[ch].glyph.metrics.horizontalAdvance);float cursor=-advance/2;var pieces=new List<CombineInstance>();
        foreach(char ch in word){var glyph=font.characterLookupTable[ch].glyph;var rect=glyph.glyphRect;var metrics=glyph.metrics;
            for(int yy=0;yy<rect.height;yy++){int start=-1;for(int xx=0;xx<=rect.width;xx++){bool on=xx<rect.width&&tex.GetPixel(rect.x+xx,rect.y+yy).a>.52f;if(on&&start<0)start=xx;if(!on&&start>=0){var center=new Vector3((cursor+metrics.horizontalBearingX+(start+xx)*.5f)*width/advance,(yy-rect.height*.5f)*height/rect.height,0);var size=new Vector3((xx-start)*width/advance,height/rect.height,.18f);pieces.Add(new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(center,Quaternion.identity,size)});start=-1;}}}cursor+=metrics.horizontalAdvance;}
        var mesh=new Mesh();mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.CombineMeshes(pieces.ToArray());Unwrapping.GenerateSecondaryUVSet(mesh);AssetDatabase.CreateAsset(mesh,F+"/"+word+"-letters.asset");group.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;group.gameObject.AddComponent<MeshRenderer>().sharedMaterial=mat;group.gameObject.AddComponent<BoxCollider>().size=new Vector3(width,height,.18f);group.gameObject.layer=6;GameObjectUtility.SetStaticEditorFlags(group.gameObject,StaticEditorFlags.ContributeGI);group.localPosition=pos;group.localRotation=Quaternion.Euler(0,yaw,0);p=parent;Object.DestroyImmediate(tex);
    }
    static void CarsAndSigns(Transform root,Transform env)
    {
        var exterior=root.Find("Exterior paths and props");foreach(var car in exterior.Cast<Transform>().Where(t=>t.name=="Abandoned car").ToArray())
        {
            var bounds=car.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).ToArray();var b=bounds[0];foreach(var bb in bounds)b.Encapsulate(bb);var center=b.center;Delete(car,t=>true);p=car;car.localPosition=new Vector3(center.x,0,center.z);
            B("Car chassis",new Vector3(0,.65f,0),new Vector3(2.5f,.7f,4.8f),red);B("Hood",new Vector3(0,1.05f,-1.5f),new Vector3(2.45f,.25f,1.65f),red);B("Trunk",new Vector3(0,1,1.8f),new Vector3(2.4f,.2f,1),red);
            B("Window cabin",new Vector3(0,1.45f,.2f),new Vector3(2.15f,.85f,2.1f),dark);B("Car roof",new Vector3(0,1.92f,.3f),new Vector3(2.2f,.14f,1.8f),red);
            foreach(float x in new[]{-1.1f,1.1f})foreach(float z in new[]{-.7f,1.1f})B("Window pillar",new Vector3(x,1.48f,z),new Vector3(.12f,.85f,.12f),red);
            foreach(float x in new[]{-1.25f,1.25f})foreach(float z in new[]{-1.5f,1.5f}){var wheel=GameObject.CreatePrimitive(PrimitiveType.Cylinder);wheel.name="Rubber wheel";wheel.transform.SetParent(p,false);wheel.transform.localPosition=new Vector3(x,.48f,z);wheel.transform.localRotation=Quaternion.Euler(0,0,90);wheel.transform.localScale=new Vector3(.85f,.16f,.85f);wheel.GetComponent<Renderer>().sharedMaterial=dark;wheel.layer=6;Object.DestroyImmediate(wheel.GetComponent<Collider>());wheel.AddComponent<BoxCollider>().size=new Vector3(1,2,1);}
            foreach(float z in new[]{-2.42f,2.42f})B("Chrome bumper",new Vector3(0,.55f,z),new Vector3(2.55f,.18f,.13f),metal);
            foreach(float x in new[]{-.85f,.85f}){B("Headlight",new Vector3(x,.92f,-2.42f),new Vector3(.55f,.27f,.05f),cream);B("Tail lamp",new Vector3(x,.92f,2.42f),new Vector3(.4f,.2f,.05f),red);}
        }
        p=exterior;Delete(exterior,t=>t.name=="MOTEL");MeshLetters("MOTEL",new Vector3(10.7f,5,49),4.3f,1.3f,90,glow);
        // Remove redundant floating catalogue labels, preserving the physical motel sign.
        Delete(env,t=>t.name=="MOTEL"||t.name=="ROADSIDE MOTEL"||t.name.StartsWith("STOP"));
        p=G(exterior,"North dumpsters");foreach(float x in new[]{32f,37f}){B("Dumpster",new Vector3(x,.8f,112),new Vector3(3,1.6f,1.8f),teal);var lid=B("Dumpster lid",new Vector3(x,1.66f,112),new Vector3(3.15f,.15f,1.95f),dark);lid.localRotation=Quaternion.Euler(-8,0,0);foreach(float dx in new[]{-1.2f,1.2f})B("Dumpster foot",new Vector3(x+dx,.15f,112),new Vector3(.25f,.3f,1.3f),metal);}
    }
    static void Pool(Transform leisure)
    {
        p=leisure.Find("Municipal pool");
        Delete(p,t=>t.name=="Pool floor"||t.name.StartsWith("Pool lane")||t.name=="Basin side"||t.name=="Basin north"||t.name.StartsWith("LOCAL POOL"));
        B("Shallow basin floor",new Vector3(-26,-1.7f,58),new Vector3(20,.2f,8),teal);
        var slope=B("Sloping basin floor",new Vector3(-26,-2.5f,67),new Vector3(20,.2f,Mathf.Sqrt(102.56f)),teal);slope.localRotation=Quaternion.Euler(Mathf.Atan2(1.6f,10)*Mathf.Rad2Deg,0,0);
        B("Deep basin floor",new Vector3(-26,-3.3f,77),new Vector3(20,.2f,10),teal);
        foreach(float x in new[]{-36f,-16f})B("Deep basin side",new Vector3(x,-1.6f,68),new Vector3(.25f,3.2f,28),cream);
        B("Deep basin north",new Vector3(-26,-1.6f,82),new Vector3(20,3.2f,.25f),cream);
        // Furnish only the left changing half; reserve right half for vendor.
        Delete(p,t=>t.name.StartsWith("Cub")||t.name=="Bench seat"&&t.localPosition.x>-23||t.name=="Bench back"&&t.localPosition.x>-23||t.name=="Bench leg"&&t.localPosition.x>-23);
        Cubbies(-33,108.2f,14);Cubbies(-34,90.3f,12);
        Delete(p,t=>t.name=="CHANGE ROOMS front"&&t.localPosition.x>-26);
        // Serving hatch plus staff door on pool-facing frontage.
        Wall(false,91,-24.5f,-10,0,3.6f,new Rect(-22,1,5,1.6f),new Rect(-13,0,1.5f,2.6f));
        B("Snack serving counter",new Vector3(-19.5f,1,90.8f),new Vector3(5.3f,.16f,1),cream);
        B("Vendor partition",new Vector3(-23,1.8f,100),new Vector3(.2f,3.6f,18),kind:"wall");
        Wall(false,103,-23,-10,0,3.6f,new Rect(-16,0,1.5f,2.6f));
        B("Chest freezer",new Vector3(-19, .75f,100.7f),new Vector3(3,1.5f,1.4f),white);B("Freezer lid",new Vector3(-19,1.55f,100.7f),new Vector3(3.1f,.12f,1.5f),cream);
        B("Fryer cabinet",new Vector3(-12, .65f,99),new Vector3(1.5f,1.3f,2),metal);B("Fryer dark well",new Vector3(-12,1.32f,99),new Vector3(1.1f,.08f,1.5f),dark);B("Fryer basket",new Vector3(-12,1.4f,99),new Vector3(.8f,.1f,1.1f),metal);
        foreach(float x in new[]{-21f,-12f}){foreach(float y in new[]{.35f,1.15f,1.95f}){B("Supply shelf",new Vector3(x,y,106.5f),new Vector3(1.5f,.1f,3),metal);B("Supply box",new Vector3(x,y+.35f,106.2f),new Vector3(1,.6f,.8f),kind:"timber");}foreach(float z in new[]{105f,108f})B("Shelf upright",new Vector3(x,1.1f,z),new Vector3(.1f,2.2f,.1f),metal);}
        foreach(float z in new[]{57f,64f,71f})foreach(float x in new[]{-41f,-11f}){B("Lounge seat",new Vector3(x,.5f,z),new Vector3(1.2f,.16f,2.1f),white);var back=B("Lounge reclined back",new Vector3(x,.9f,z+1.2f),new Vector3(1.2f,.12f,1.2f),teal);back.localRotation=Quaternion.Euler(-45,0,0);foreach(float dz in new[]{-.7f,.7f})B("Lounge legs",new Vector3(x,.25f,z+dz),new Vector3(1,.5f,.12f),metal);}
        foreach(var item in new[]{new Vector2(-31,1),new Vector2(-20,3)})
        {float x=item.x,h=item.y;B("Diving board "+h,new Vector3(x,h,81.5f),new Vector3(1.5f,.2f,6),white);B("Diving pedestal",new Vector3(x,h/2,84),new Vector3(1,h,1),blue);int count=(int)(h/.2f);for(int i=0;i<count;i++){float hh=(i+1)*.2f;B("Diving stair",new Vector3(x,hh/2,84.65f+(count-1-i)*.3f),new Vector3(1.5f,hh,.3f),kind:"tread");}foreach(float dx in new[]{-.85f,.85f})Beam("Diving stair handrail",new Vector3(x+dx,1,84.65f+(count-1)*.3f),new Vector3(x+dx,h+1,84.65f),.07f);}
        var slide=leisure.Find("Old waterslide");slide.localPosition+=Vector3.back*5;Delete(slide,t=>t.name=="Yellow flume"||t.name=="Flume side");p=slide;
        var a=new Vector3(-40,4.1f,78);var b=new Vector3(-27,-1.6f,78);var dir=b-a;var flume=B("Straight slide floor",(a+b)/2,new Vector3(2,.16f,dir.magnitude),yellow);flume.localRotation=Quaternion.LookRotation(dir);foreach(float z in new[]{77f,79f}){var lip=B("Straight slide side",(a+b)/2+new Vector3(0,.35f,z-78),new Vector3(.12f,.8f,dir.magnitude),yellow);lip.localRotation=flume.localRotation;}
        foreach(var t in leisure.Find("Municipal pool").Cast<Transform>().Where(t=>t.name.StartsWith("Lounge")&&t.localPosition.x< -35&&t.localPosition.z>69).ToArray())t.localPosition+=new Vector3(1,0,-18);
        var arrival=leisure.Find("Arrival and public parking");Delete(arrival,t=>t.name.StartsWith("PUBLIC POOL"));p=arrival;
        foreach(float x in new[]{-33f,-19f})B("Pool neon pylon",new Vector3(x,3.4f,44),new Vector3(.2f,6.8f,.2f),metal);
        B("Pool sign backing",new Vector3(-26,5.5f,44),new Vector3(14,2.5f,.3f),blue);MeshLetters("POOL",new Vector3(-26,5.5f,43.76f),11,1.8f,0,poolGlow);
    }
    static void Cubbies(float x,float z,int n)
    {
        B("Cubby back",new Vector3(x,1.3f,z+.4f),new Vector3(n,2.4f,.1f),teal);
        for(int i=0;i<=n;i++)B("Cubby divider",new Vector3(x-n/2f+i,1.3f,z),new Vector3(.07f,2.4f,.8f),cream);
        foreach(float y in new[]{.2f,1,1.8f,2.6f})B("Cubby shelf",new Vector3(x,y,z),new Vector3(n,.08f,.8f),cream);
    }
    static void Golf(Transform leisure)
    {
        var golf=leisure.Find("Mini golf");p=golf;
        var shack=G(golf,"Moved north-facing golf shack");shack.localPosition=new Vector3(-39,0,151);
        foreach(var t in golf.Cast<Transform>().Where(t=>t.name.StartsWith("GOLF")).ToArray())t.SetParent(shack,true);
        shack.localPosition=new Vector3(-16,0,114.5f);shack.localRotation=Quaternion.Euler(0,180,0);
        Delete(golf,t=>t.name.StartsWith("Putting green")||t.name=="Green curb"||t.name.StartsWith("Hole flag")||t.name=="Golf obstacle"||t.name.StartsWith("Duck "));
        var felt=M("Varied golf green",new Color(.22f,.42f,.24f));
        for(int i=0;i<9;i++)
        {
            float x=-39+(i%3)*11,z=123+(i/3)*11;var hole=G(golf,"Golf hole "+(i+1)+(i%3==0?" L":i%3==1?" Long":" S"));p=hole;hole.localPosition=new Vector3(x,0,z);
            var segments=i%3==0?new[]{new Rect(-3,-3,2.5f,5.5f),new Rect(-3,2.5f,6,2.5f)}:i%3==1?new[]{new Rect(-2,-4,4,9)}:new[]{new Rect(-3,-4,6,2),new Rect(.5f,-2,2.5f,3),new Rect(-3,1,6,2),new Rect(-3,3,2.5f,2)};
            foreach(var q in segments)B("Putting surface",new Vector3(q.center.x,.1f,q.center.y),new Vector3(q.width,.2f,q.height),felt);
            var flag=G(p,"Random flag");flag.localPosition=new Vector3(i%3==0?1.8f:i%3==1?0:-1.8f,.2f,4.2f);flag.localRotation=Quaternion.Euler(0,(i*137+29)%360,0);p=flag;Beam("Flag mast",Vector3.zero,Vector3.up*1.5f,.03f);B("Flag cloth",new Vector3(.3f,1.3f,0),new Vector3(.6f,.35f,.04f),i%2==0?red:blue);p=hole;
            if(i%2==0)B("Golf obstacle",new Vector3(-1,.4f,0),new Vector3(1,.6f,.6f),kind:"timber");
        }
    }
    static void RinkAndEdges(Transform leisure,Transform motel)
    {
        p=leisure.Find("Roller rink");Delete(p,t=>t.name.StartsWith("Fence"));
        void RinkWall(Vector3 a,Vector3 b){var d=b-a;var t=B("White rink wall",(a+b)/2+Vector3.up*.45f,new Vector3(.22f,.9f,d.magnitude),white);t.localRotation=Quaternion.LookRotation(d);var cap=B("Blue rink top stripe",(a+b)/2+Vector3.up*.88f,new Vector3(.24f,.16f,d.magnitude),blue);cap.localRotation=t.localRotation;}
        RinkWall(new Vector3(10,0,119),new Vector3(10,0,145));RinkWall(new Vector3(42,0,119),new Vector3(42,0,145));
        foreach(float z in new[]{119f,145f}){RinkWall(new Vector3(10,0,z),new Vector3(23,0,z));RinkWall(new Vector3(29,0,z),new Vector3(42,0,z));}
        // Also give the rental building a north door, so either reading of the requested north access is served.
        Delete(p,t=>t.name=="SKATE RENTAL rear wall");Wall(false,153,28,40,0,3.6f,new Rect(33,0,2,2.6f));
        p=G(leisure,"Simple metal side fences");
        void Fence(Vector3 a,Vector3 b){int count=Mathf.CeilToInt(Vector3.Distance(a,b)/3);for(int i=0;i<=count;i++)B("Side fence post",Vector3.Lerp(a,b,(float)i/count)+Vector3.up, new Vector3(.12f,2,.12f),metal);foreach(float y in new[]{.35f,1.1f,1.9f})Beam("Side fence rail",a+Vector3.up*y,b+Vector3.up*y,.08f);var d=b-a;var t=G(p,"Fence collision");t.localPosition=(a+b)/2+Vector3.up;t.localRotation=Quaternion.LookRotation(d);t.gameObject.layer=6;t.gameObject.AddComponent<BoxCollider>().size=new Vector3(.1f,2,d.magnitude);}
        foreach(float x in new[]{-47f,47f}){Fence(new Vector3(x,0,0),new Vector3(x,0,52));Fence(new Vector3(x,0,113),new Vector3(x,0,160));float inner=x<0?-44:45;Fence(new Vector3(x,0,52),new Vector3(inner,0,52));Fence(new Vector3(inner,0,113),new Vector3(x,0,113));}
        // West central pool fence already extends 46..111; bridge the small joins.
        Fence(new Vector3(-44,0,111),new Vector3(-44,0,113));
        var trees=leisure.Find("Perimeter trees");var random=new System.Random(1212);var items=trees.Cast<Transform>().ToArray();
        for(int i=0;i<items.Length;i+=3){float scale=.72f+(float)random.NextDouble()*.65f;var anchor=items[i].localPosition;anchor.y=0;for(int j=i;j<Mathf.Min(items.Length,i+3);j++){items[j].localPosition=anchor+(items[j].localPosition-anchor)*scale;items[j].localScale*=scale;foreach(var c in items[j].GetComponentsInChildren<MeshCollider>())c.convex=true;}}
    }
    static void Lighting(Transform leisure)
    {
        p=G(leisure,"Taller lighting");
        foreach(float x in new[]{-8f,8f})foreach(float z in new[]{18f,43f,72f,105f,133f,153f})
        {B("Tall streetlight pole",new Vector3(x,3.75f,z),new Vector3(.16f,7.5f,.16f),metal);B("Streetlight head",new Vector3(x,7.5f,z),new Vector3(1.2f,.2f,.6f),glow);LightAt("Streetlight baked",new Vector3(x,7.3f,z),new Vector3(x+Mathf.Sign(x)*8,0,z),65,25,new Color(1,.86f,.67f));}
        foreach(var pos in new[]{new Vector3(-42,10,50),new Vector3(-10,10,86)})
        {B("Pool flood mast",new Vector3(pos.x,5,pos.z),new Vector3(.22f,10,.22f),metal);var head=B("Large pool flood",pos,new Vector3(1.7f,.8f,.3f),glow);head.localRotation=Quaternion.LookRotation(new Vector3(-26,0,68)-pos);LightAt("Pool flood baked",pos,new Vector3(-26,-1,68),180,45,new Color(.8f,.9f,1),100);}
    }
    public static void Install()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Duck Rails Edit Mode required");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Save prefab first");StageUtility.GoToMainStage();Init();
        var go=PrefabUtility.LoadPrefabContents(RailMotelGreybox.PrefabPath);
        try{var env=go.transform.Find("Env");var leisure=env.Find(RailMotelLeisure.Root);if(leisure.Find("Taller lighting"))throw new Exception("Already installed");var motel=env.Find("Motel greybox v1");Motel(motel.Find("Motel"));CarsAndSigns(motel,env);Pool(leisure);Golf(leisure);RinkAndEdges(leisure,motel);Lighting(leisure);
            var block=go.GetComponent<RailBlock>();
            // Relocate candidates removed by closed rooms, rebuilt golf and deeper pool; preserve marker IDs/counts.
            int k=0;var east=new[]{new Vector3(33,0,58),new Vector3(33,0,66),new Vector3(33,0,82),new Vector3(33,3.6f,66),new Vector3(33,3.6f,82),new Vector3(24,3.6f,103)};
            foreach(var t in block.spawnCandidates.coal.Where(t=>t.localPosition.x>0)){var v=east[k%east.Length];v.x+=(k/east.Length)*.8f;t.localPosition=v+Vector3.up*.3f;PrefabUtility.RecordPrefabInstancePropertyModifications(t);k++;}
            foreach(var t in block.spawnCandidates.coal.Where(t=>t.localPosition.x<0&&t.localPosition.z>115)){t.localPosition=new Vector3(-17, .4f,122+(k++%3)*11);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            foreach(var t in block.spawnCandidates.gold.Where(t=>t.localPosition.x>0&&t.localPosition.z<110)){t.localPosition=new Vector3(24+(k++%3),3.8f,106);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            foreach(var t in block.spawnCandidates.enemies.Where(t=>t.localPosition.x>28&&t.localPosition.z<94)){t.localPosition=new Vector3(33, .2f,82);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
            PrefabUtility.SaveAsPrefabAsset(go,RailMotelGreybox.PrefabPath);AssetDatabase.SaveAssets();
        }finally{PrefabUtility.UnloadPrefabContents(go);}PrefabStageUtility.OpenPrefab(RailMotelGreybox.PrefabPath);
    }
}
#endif
