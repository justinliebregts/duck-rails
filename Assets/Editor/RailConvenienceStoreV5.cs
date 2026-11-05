#if UNITY_EDITOR
using System;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static partial class RailConvenienceStore
{
    static Material teal, amber, screen;
    static void GrassGround(Transform ground)
    {
        string path=Folder+"/Meshes/Grass ground.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!mesh){mesh=Object.Instantiate(cube);mesh.name="Grass ground";mesh.uv=mesh.vertices.Select(v=>new Vector2(v.x*24,v.z*40)).ToArray();AssetDatabase.CreateAsset(mesh,path);}
        ground.GetComponent<MeshFilter>().sharedMesh=mesh;ground.GetComponent<MeshRenderer>().sharedMaterials=new[]{AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville grass.mat")};
        PrefabUtility.RecordPrefabInstancePropertyModifications(ground.GetComponent<MeshFilter>());PrefabUtility.RecordPrefabInstancePropertyModifications(ground.GetComponent<MeshRenderer>());
    }
    static void BuildV5(Transform root)
    {
        teal=Special("Neon teal",new Color(.1f,.8f,.75f),new Color(.1f,3,2.5f));
        amber=Special("Neon amber",new Color(1,.6f,.15f),new Color(3,1.5f,.1f));
        screen=Special("Arcade screen",new Color(.15f,.4f,.65f),new Color(.15f,.8f,1.8f));
        ShopV5(root);ArcadeV5(root);GasV5(root);WashV5(root);PlazaV5(root);StreetV5(root);OuterV5(root);
        if(root.GetComponentsInChildren<Light>().Length!=LightCount)throw new Exception("Unexpected V5 fixture count: "+root.GetComponentsInChildren<Light>().Length);
    }
    // Local frontage faces +Z. Rotate +90 for east and -90 for west.
    static void GlowSign(Transform p,string text,Vector3 pos,float width,float yaw,Material glow)
    {
        var t=Group(p,"Glowing sign "+text);t.localPosition=pos;t.localRotation=Quaternion.Euler(0,yaw,0);
        Box(t,"Sign backing",Vector3.zero,new Vector3(width,1.5f,.18f),"roof");
        foreach(float y in new[]{-.68f,.68f})Box(t,"Emissive trim",new Vector3(0,y,.13f),new Vector3(width,.06f,.06f),"steel",false,glow);
        Sign(t,text,new Vector3(0,0,.13f),width*.95f,true);
        var txt=t.GetComponentInChildren<TextMeshPro>();txt.enableAutoSizing=true;txt.fontSizeMin=4;txt.fontSizeMax=110;txt.ForceMeshUpdate();
        string path=Folder+"/Text-"+glow.name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(txt.fontSharedMaterial);mat.SetColor("_FaceColor",glow.GetColor("_EmissionColor"));AssetDatabase.CreateAsset(mat,path);}txt.fontSharedMaterial=mat;
    }
    static void Glazing(Transform p,float x,float a,float b)
    {
        Prop(p,"Window sill",x-.1f,a,.2f,b-a,.65f,"wall");
        // Clear window opening with thin mullions; no black opaque sheet hiding the interior.
        Box(p,"Window header",new Vector3(x,3.1f,(a+b)/2),new Vector3(.2f,1,b-a),"wall");
        for(float z=a;z<=b;z+=3)Prop(p,"Window mullion",x-.06f,z,.12f,.12f,2.7f,"steel");
        var g=Group(p,"Clear shop window collision").gameObject;g.layer=6;var c=g.AddComponent<BoxCollider>();c.center=new Vector3(x,1.65f,(a+b)/2);c.size=new Vector3(.08f,2,b-a);
    }
    static void ShopV5(Transform root)
    {
        var p=Group(root,"Store");Pad(p,"Floor",22,62,18,24);Wall(p,false,62,22,40,4.2f);Wall(p,false,86,22,40,4.2f);Wall(p,true,40,62,86,4.2f,79,82);
        Glazing(p,22,62,77);Glazing(p,22,80,86);Box(p,"Entry lintel",new Vector3(22,3.5f,78.5f),new Vector3(.2f,1.4f,3),"wall");
        Wall(p,true,36,62,86,4.2f,79,82);
        var roof=Group(p,"Roofs");Box(roof,"Roof",new Vector3(31,4.35f,74),new Vector3(18.5f,.25f,24.5f),"roof");Box(roof,"Awning",new Vector3(21,3.2f,74),new Vector3(2,.15f,24),"roof");
        GlowSign(p,"QUACK MART",new Vector3(21.7f,4,74),20,-90,neon);
        for(int i=0;i<3;i++)
        {
            float z=68+i*4.5f;var shelf=Group(p,"Shelf run "+i);Prop(shelf,"Back",26,z+.65f,7,.2f,1.65f,"steel");
            foreach(float y in new[]{.18f,.75f,1.3f})Box(shelf,"Shelf",new Vector3(29.5f,y,z+.75f),new Vector3(7,.12f,1.5f),"steel");
            for(int j=0;j<6;j++){Prop(shelf,"Carton",26.3f+j,z+.2f,.45f,.45f,.32f,"timber",1.36f);}
        }
        Prop(p,"Checkout counter",23,81,2,3,.95f);Prop(p,"Register",23.4f,82,.6f,.6f,.35f,"steel",.95f);
        for(int i=0;i<5;i++){float x=26+i*1.5f;Prop(p,"Fridge cabinet",x,84.4f,1.4f,1.2f,2.4f,"steel");Box(p,"Fridge face",new Vector3(x+.7f,1.35f,84.32f),new Vector3(1.2f,1.8f,.08f),"steel",false,glass);Box(p,"Fridge lit header",new Vector3(x+.7f,2.25f,84.27f),new Vector3(1.2f,.1f,.06f),"steel",false,lens);}
        Prop(p,"Chest freezer",31,64,3,2,1,"steel");Prop(p,"Stock shelving",38,63,1.2f,7,2,"timber");Prop(p,"Delivery cartons",37,83,2,2,1,"timber");
        foreach(float x in new[]{26f,33f})foreach(float z in new[]{72f,81f})Lamp(p,"Baked shop light",x,3.9f,z,false,new Color(1,.82f,.6f));
        Lamp(p,"Baked stock light",38,3.9f,77);Lamp(p,"Baked pink frontage spill",20,4.1f,74,false,new Color(1,.22f,.5f));
        var s=Group(root,"Service loop");Pad(s,"Front walk",20,58,2,32);Pad(s,"Rear lane",40,58,4,44);Pad(s,"South walk",20,58,24,4);Pad(s,"North walk",20,86,24,4);
        foreach(float z in new[]{65f,71f}){Prop(s,"Dumpster",42,z,1.6f,3,1.4f);Box(s,"Dumpster lid",new Vector3(42.8f,1.48f,z+1.5f),new Vector3(1.7f,.14f,3.1f),"steel");}
        Prop(s,"ICE freezer",20.5f,83,1,1.5f,1.5f,"steel");Sign(s,"ICE",new Vector3(20.45f,1,83.75f),1,false);Vending(s,20.5f,85);
        Vehicle(s,24,93,true);
    }
    static void ArcadeV5(Transform root)
    {
        var p=Group(root,"Arcade");Pad(p,"Floor",-37,66,18,20);Wall(p,false,66,-37,-19,4.2f);Wall(p,false,86,-37,-19,4.2f);Wall(p,true,-37,66,86,4.2f,69,72);
        Glazing(p,-19,66,77);Glazing(p,-19,80,86);Box(p,"Entry lintel",new Vector3(-19,3.5f,78.5f),new Vector3(.2f,1.4f,3),"wall");Wall(p,true,-33,66,86,4.2f,70,73);
        Box(p,"Roof",new Vector3(-28,4.35f,76),new Vector3(18.5f,.25f,20.5f),"roof");GlowSign(p,"ARCADE",new Vector3(-18.8f,4,76),15,90,teal);
        for(int row=0;row<2;row++)for(int i=0;i<4;i++)
        {
            float x=(row==0?-32:-29)+i*1.5f,z=row==0?83.2f:74;var c=Group(p,"Arcade cabinet "+row+"-"+i);
            Prop(c,"Base",x,z,1.1f,1.5f,.9f,"prop");Prop(c,"Upper cabinet",x,z+.35f,1.1f,1.15f,1.3f,"roof",.9f);
            Box(c,"Screen",new Vector3(x+.55f,1.55f,z+.30f),new Vector3(.85f,.65f,.05f),"steel",false,i%2==0?screen:glass);
            Box(c,"Marquee",new Vector3(x+.55f,2.15f,z+.25f),new Vector3(1,.2f,.05f),"steel",false,i%2==0?teal:neon);
            Box(c,"Controls",new Vector3(x+.55f,1.03f,z+.13f),new Vector3(1,.1f,.5f),"steel");
        }
        Prop(p,"Prize machine",-22.5f,82,2.5f,2.5f,2.3f,"prop");Box(p,"Prize display",new Vector3(-21.25f,1.4f,81.95f),new Vector3(2.2f,1.4f,.06f),"steel",false,screen);
        Prop(p,"Counter",-23.5f,67,3.5f,1.7f,.95f);Prop(p,"Air hockey",-30.8f,68,3.5f,2,.85f,"steel");Box(p,"Air hockey top",new Vector3(-29.05f,.9f,69),new Vector3(3.4f,.1f,1.9f),"steel",false,lens);
        Prop(p,"Service shelves",-36.5f,78,1,5,2,"timber");
        Lamp(p,"Baked arcade screen fill",-27,3.8f,82,false,new Color(.3f,.65f,1));Lamp(p,"Baked arcade rear fill",-29,3.8f,70,false,new Color(1,.3f,.65f));Lamp(p,"Baked arcade sign spill",-17.5f,4.1f,76,false,new Color(.15f,1,.9f));
        Pad(p,"Arcade plaza",-40,62,30,28);Vending(p,-16,67);
    }
    static void Vending(Transform p,float x,float z)
    {Prop(p,"Vending machine",x,z,1,1.2f,2,"prop");Box(p,"Vending lit display",new Vector3(x+.5f,1.25f,z-.04f),new Vector3(.8f,1.1f,.05f),"steel",false,screen);}
    static void GasV5(Transform root)
    {
        var p=Group(root,"Gas forecourt");Pad(p,"Forecourt",10,58,30,44,"ground");
        Box(p,"Canopy roof",new Vector3(15,4.8f,94),new Vector3(10,.35f,12),"roof");
        foreach(float x in new[]{10f,20f})Box(p,"Canopy neon edge",new Vector3(x,4.8f,94),new Vector3(.08f,.2f,12),"steel",false,teal);
        foreach(float z in new[]{88f,100f})Box(p,"Canopy neon edge",new Vector3(15,4.8f,z),new Vector3(10,.2f,.08f),"steel",false,teal);
        foreach(float x in new[]{12f,17f})
        {
            Prop(p,"Pump island",x,90,1,7,.12f,"concrete");Prop(p,"Canopy column",x+.35f,93.3f,.3f,.3f,4.65f,"wall");
            foreach(float z in new[]{91f,95.5f}){Prop(p,"Gas pump",x+.05f,z,.9f,1.1f,1.8f,"prop",.12f);Box(p,"Pump display",new Vector3(x-.01f,1.4f,z+.55f),new Vector3(.06f,.45f,.65f),"steel",false,screen);Box(p,"Hose",new Vector3(x+1.1f,.9f,z+.55f),new Vector3(.08f,1.5f,.08f),"roof",false);}
        }
        Lamp(p,"Baked canopy light",15,4.5f,91);Lamp(p,"Baked canopy light",15,4.5f,97);
        Prop(p,"Gas pylon",8.8f,99,.3f,.3f,7,"steel");GlowSign(p,"GAS",new Vector3(9,6.5f,99),3,-90,teal);
        foreach(float z in new[]{62f,65f,68f,71f,74f})Pad(p,"Parking stripe",11,z,5,.1f);
        var a=Group(p,"Parking car A");Vehicle(a,0,0);a.localRotation=Quaternion.Euler(0,90,0);a.localPosition=new Vector3(11,0,65.2f);
        var b=Group(p,"Parking car B");Vehicle(b,0,0);b.localRotation=Quaternion.Euler(0,90,0);b.localPosition=new Vector3(11,0,71.2f);
    }
    static void WashV5(Transform root)
    {
        var p=Group(root,"Car wash");Pad(p,"Wash apron",25,35,18,20);Wall(p,true,28,38,52,4);Wall(p,true,40,38,52,4);Wall(p,false,38,28,40,4,31,37);Wall(p,false,52,28,40,4,31,37);
        Box(p,"Roof",new Vector3(34,4.2f,45),new Vector3(12.5f,.25f,14.5f),"roof");
        foreach(float x in new[]{29f,38f})for(int i=0;i<3;i++)Prop(p,"Wash brush",x,41+i*3,1,1.5f,2.7f,"prop");
        GlowSign(p,"CAR WASH",new Vector3(34,3.5f,52.2f),11,0,neon);Lamp(p,"Baked wash lamp",34,3.8f,46);Lamp(p,"Baked wash sign spill",34,4,53,false,new Color(1,.3f,.6f));
    }
    static void PlazaV5(Transform root)
    {
        // Reuse three full restaurant interiors, translated to the approved north footprint.
        Plaza(root);var p=root.Find("Restaurant plaza");p.localPosition=new Vector3(-1,0,18);
        foreach(var t in p.Cast<Transform>().Where(t=>t.Find("Kitchen range")).ToArray())
        {
            foreach(var old in t.Cast<Transform>().Where(t=>t.name.StartsWith("Sign ")).ToArray())Object.DestroyImmediate(old.gameObject);
            var counter=t.Find("Service counter");float x=counter.localPosition.x-8;
            GlowSign(t,t.name,new Vector3(x+5,3.2f,101.7f),9,180,t.name=="DUCK DINER"?teal:neon);
        }
    }
    static void StreetV5(Transform root)
    {
        var p=Group(root,"Road and parking");Pad(p,"Road",-48,104,96,8,"ground");Pad(p,"South sidewalk",-42,102,84,2);Pad(p,"North sidewalk",-42,112,84,2);
        foreach(float x in new[]{-36f,-18f,18f,36f})foreach(float z in new[]{103f,113f})Lamp(p,"Road streetlight",x,6,z,true,new Color(1,.8f,.5f));
        for(float x=-45;x<48;x+=7)Pad(p,"Centre stripe",x,107.9f,3,.15f,"yellow");foreach(float x in new[]{-15f,24f})for(float z=104;z<112;z+=1.3f)Pad(p,"Crosswalk",x,z,3,.6f);
        foreach(float x in new[]{-9f,9f}){float z=x<0?104:112;Prop(p,"Crossing base",x-.35f,z-.35f,.7f,.7f,1.2f,"steel");Prop(p,"Raised crossing gate",x-.1f,z-.1f,.2f,.2f,4,"yellow",1.2f);Box(p,"Signal head",new Vector3(x,2.8f,z),new Vector3(1.3f,.4f,.2f),"steel");foreach(float dx in new[]{-.4f,.4f})Box(p,"Red signal lens",new Vector3(x+dx,2.8f,z+.12f),new Vector3(.25f,.25f,.05f),"steel",false,neon);}
        Pad(p,"Old parking",-39,90,27,11,"ground");foreach(float x in new[]{-36f,-29f,-22f,-15f})Vehicle(p,x,91);
        foreach(float x in new[]{-40f,-12f})for(float z=94;z<=100;z+=3)Prop(p,"Parking fence post",x,z,.15f,.15f,1.6f,"timber");
        foreach(float z in new[]{89f,101f})foreach(float x in new[]{-37f,-20f})Box(p,"Broken parking fence",new Vector3(x,1,z),new Vector3(9,.12f,.12f),"timber");
    }
    static void OuterV5(Transform root)
    {
        var p=Group(root,"Outer thirds");foreach(float x in new[]{-7f,4f})Pad(p,"Trackside walk",x,10,3,140);
        var rest=Group(root,"Rest stop");Pad(rest,"Gravel rest area",-37,28,25,28);Pad(rest,"Rest path",-12,25,3,37);
        foreach(float z in new[]{32f,39f}){Prop(rest,"Picnic tabletop",-28,z,5,2,.12f,"timber",.75f);foreach(float zz in new[]{z-.8f,z+2.3f})Prop(rest,"Picnic bench",-28,zz,5,.5f,.45f,"timber");}
        Box(rest,"Vending shelter roof",new Vector3(-32.5f,3,45.5f),new Vector3(7,.2f,5),"roof");foreach(float x in new[]{-36f,-29f})Prop(rest,"Shelter post",x,43,.15f,.15f,3,"timber");Vending(rest,-34,45);
        Box(rest,"Bus shelter roof",new Vector3(-15,2.8f,47.5f),new Vector3(4,.2f,7),"roof");Prop(rest,"Bus shelter back",-17,44,.15f,7,2.7f,"steel");Prop(rest,"Bus bench",-16.5f,45,.7f,4,.55f,"timber");Lamp(rest,"Baked rest light",-30,3.4f,45);
        Pad(p,"Utility pad",-38,120,24,16);Prop(p,"Utility cabinet",-34,125,6,5,2.2f,"steel");Prop(p,"Utility crates",-22,124,4,4,1.4f,"timber");
        // Shallow dry-lined swales on the existing level ground; bridge decks flush to the banks.
        foreach(float x in new[]{-42f,10f})
        {
            Pad(p,"Drain bed",x,19,32,3,"roof");foreach(float z in new[]{18.7f,22f})Prop(p,"Drain curb",x,z,32,.25f,.18f,"concrete");
            float bx=x<0?-22:23;Box(p,"Footbridge deck",new Vector3(bx+2,.12f,20.5f),new Vector3(4,.24f,7),"timber");
            // 12 cm rise spread over a metre: no mandatory jump.
            foreach(float z in new[]{16.5f,24.5f}){var ramp=Box(p,"Bridge approach",new Vector3(bx+2,.06f,z),new Vector3(4,.12f,1.2f),"timber");ramp.transform.localRotation=Quaternion.Euler(z<20?-5.7f:5.7f,0,0);}
        }
        foreach(float z in new[]{10f,29f,58f,116f,143f,151f})foreach(float x in new[]{-43f,43f})Tree(p,x,z);
        foreach(float z in new[]{12f,145f,151f}){Tree(p,-28,z);Tree(p,25,z);Tree(p,14,z-1);}
        foreach(float z in new[]{29f,44f,140f})foreach(float x in new[]{-41f,41f}){foreach(float zz in new[]{z,z+4,z+8})Prop(p,"Fence post",x,zz,.15f,.15f,1.5f,"timber");Box(p,"Fence fragment",new Vector3(x,1,z+4),new Vector3(.12f,.12f,8),"timber");}
        // Additional sign-side light gives the canopy pylon a local pool without realtime lighting.
        Lamp(p,"Baked pylon spill",9,6,100,false,new Color(.15f,1,.85f));
        // One utility fixture closes the north-west scenery lighting coverage.
        Lamp(p,"Baked utility lamp",-25,5,123,true);
    }
}
#endif

