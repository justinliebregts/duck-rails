#if UNITY_EDITOR
using System.Linq;
using UnityEngine;
public static partial class RailAbandonedStation
{
    public static float RailX(float z)
    {
        if(z<6||z>154)return 0; if(z>=53&&z<=107)return -12;
        float t=z<53?(z-6)/47:(154-z)/47;
        return -12*t*t*(3-2*t);
    }
    static void LowSpace(Transform p,string name,float x,float z,float w,float d,float y,bool sides=true)
    {
        var t=Group(p,name);Slab(t,"Floor",x,z,w,d,y,concrete);
        Slab(t,"Low ceiling",x,z,w,d,y+2.43f,roof);
        if(sides)foreach(float xx in new[]{x,x+w})Wall(t,"Tiled wall",true,xx,z,z+d,y,2.13f,paint);
        for(float zz=z+3;zz<z+d;zz+=10)Lamp(t,new(x+w/2,y+1.93f,zz),false,9);
    }
    static void BuildMetro(Transform p,RailBlock b)
    {
        var track=Group(p,"Curved descending railway");
        b.track=Enumerable.Range(0,321).Select(i=>new Vector3(RailX(i*.5f),RailHeight(i*.5f),i*.5f)).ToArray();b.stop.localPosition=new(-12,-4,80);
        for(int z=0;z<160;z++)
        {
            var a=new Vector3(RailX(z),RailHeight(z),z);var c=new Vector3(RailX(z+1),RailHeight(z+1),z+1);
            Beam(track,"Rail bed",a-Vector3.up*.15f,c-Vector3.up*.15f,9,.3f,gravel);
            foreach(float x in new[]{-.8f,.8f})Beam(track,"Visual rail",a+new Vector3(x,.08f,0),c+new Vector3(x,.08f,0),.1f,.12f,steel,false);
            if(z%2==0)Box(track,"Visual sleeper",(a+c)/2+Vector3.up*.02f,new(2.4f,.08f,.25f),wood,false);
            if(z<6||z>=154)continue;
            Beam(track,"Rail channel roof",a+Vector3.up*6.6f,c+Vector3.up*6.6f,9.2f,.4f,roof);
            if(z>=53&&z<107)continue;
            foreach(float side in new[]{-1f,1f})
            {
                Beam(track,"Narrow tunnel wall",a+new Vector3(side*7,3,0),c+new Vector3(side*7,3,0),.4f,7,paint);
                Beam(track,"Tunnel walkway",a+new Vector3(side*5.7f,.85f,0),c+new Vector3(side*5.7f,.85f,0),2.5f,.3f,concrete);
                Beam(track,"Low walkway ceiling",a+new Vector3(side*5.7f,3.28f,0),c+new Vector3(side*5.7f,3.28f,0),2.5f,.3f,roof);
            }
            if(z%12==0)Lamp(track,(a+c)/2+Vector3.up*5.8f,false,14);
        }
        foreach(float z in new[]{0f,154f})Slab(p,"Flat seam",-48,z,96,6,0,grass);
        foreach(float x in new[]{-48f,44f})Slab(p,"Flat side boundary",x,6,4,148,0,grass);
        Slab(p,"Reserved midpoint shop pad",-44,140,38,14,0,concrete);
        // Opaque earth envelope behind the occupied chambers, including the roof steps.
        foreach(float x in new[]{-44f,40f})Box(p,"Buried outer retaining wall",new(x,-1,80),new(.5f,20,148),gravel);
        Slab(p,"Overburden",-44,6,84,148,9,gravel);
        Slab(p,"Buried foundation",-44,6,84,148,-11,gravel);
        foreach(float z in new[]{6f,154f}) { Wall(p,"Buried end wall",false,z,-44,-7.3f,-11,20,gravel); Wall(p,"Buried end wall",false,z,7.3f,40,-11,20,gravel); }
        p=Group(p,"Offset station chambers");p.localPosition=Vector3.left*12;
        foreach(float x in new[]{-4.5f,4.5f})Wall(p,"Rail channel upper wall",true,x,53,107,-.87f,3.5f,paint);
        foreach(float x in new[]{-9f,4.5f})LowSpace(p,"Low main platform",x,53,4.5f,54,-3,false);
        Wall(p,"West shopfront",true,-9,53,107,-3,2.13f,paint,60,67,74,77,86,89,98,101);
        Wall(p,"East platform wall",true,9,53,107,-3,2.13f,paint,60,64,104,107);
        foreach(float x in new[]{-4.7f,4.7f})Box(p,"Platform stripe",new(x,-2.98f,80),new(.18f,.02f,54),yellow,false);
        for(int i=0;i<4;i++)
        {
            float z=62+i*11;LowSpace(p,i<2?"Abandoned convenience shop":"Staff stockroom",-16,z,7,9,-3,false);
            Wall(p,"Shop back",true,-16,z,z+9,-3,2.13f,paint);
            foreach(float zz in new[]{z,z+9})Wall(p,"Shop partition",false,zz,-16,-9,-3,2.13f,paint);
            Box(p,"Dusty shelving",new(-15,-2.2f,z+4),new(.6f,1.6f,5),steel);
            Box(p,"Abandoned counter",new(-13,-2.5f,z+7),new(3,1,.6f),wood);
        }
        // Lower chamber is offset to the east, under a solid ceiling, never an open atrium.
        LowSpace(p,"Lower platform",14,38,5,68,-8,false);
        Wall(p,"Lower dividing wall",true,14,38,106,-8,2.13f,paint,38,42,82,86);
        Slab(p,"Wreck track floor",19,60,7,44,-8.4f,gravel);
        Slab(p,"Wreck chamber roof",19,38,7,68,-4.35f,roof);
        Wall(p,"Wreck outer wall",true,26,38,106,-8.4f,4,paint);
        foreach(float z in new[]{38f,106f})Wall(p,"Lower end wall",false,z,14,26,-8.4f,4,paint,z==106?new[]{14f,19f}:new float[0]);
        var wreckParent=Group(p,"Lower crashed train");wreckParent.localPosition=Vector3.down*5;Wreck(wreckParent);
        for(int i=0;i<7;i++)
        {
            var rock=Box(p,"Crash rubble",new(20+i%3*1.7f,-7.7f+i%2*.35f,101+i/3),new(1.3f,1.1f,1.1f),gravel);
            rock.transform.localRotation=Quaternion.Euler(i*13,i*27,i*9);
        }
        Box(p,"Buckled nose panel",new(21.5f,-6.6f,100),new(3.7f,2.3f,.25f),paint).transform.localRotation=Quaternion.Euler(22,18,9);
        // Three enclosed stairs: both east connections plus west access through the lower cross-passage.
        foreach(var spec in new[]{new Vector2(10,42),new Vector2(10,86),new Vector2(-13,42)})
        {
            float x=spec.x,z=spec.y;Stairs(p,"Enclosed lower stair",x,z,3,18,-8,-3,30,spec.x==10&&spec.y==86);
            foreach(float xx in new[]{x-.2f,x+3.2f})Wall(p,"Stairwell wall",true,xx,z,z+18,-8,7.3f,paint);
            Beam(p,"Sloped stair ceiling",new(x+1.5f,-5.57f,z),new(x+1.5f,-.57f,z+18),3.4f,.3f,roof);
            LowSpace(p,"Upper stair landing",x,z+18,3,4,-3,false);
            Wall(p,"Upper landing end",false,z+22,x,x+3,-3,2.13f,paint);
            Wall(p,"Upper landing outer side",true,x<0?x:x+3,z+18,z+22,-3,2.13f,paint);
            LowSpace(p,"Lower stair landing",x,z-4,9,4,-8,false);
            Wall(p,"Landing back wall",false,z-4,x,x+3,-8,2.13f,paint);
            if(x<0)Wall(p,"Landing outer wall",true,x,z-4,z,-8,2.13f,paint);
            Lamp(p,new(x+1.5f,-3.5f,z+9),true,10);
        }
        LowSpace(p,"Lower cross passage",-13,38,32,4,-8,false);
        foreach(float z in new[]{38f,42f})Wall(p,"Cross passage wall",false,z,-13,10,-8,2.13f,paint,z==42?new[]{-13f,-10f}:new[]{3f,10f});
        foreach(float z in new[]{60f,104f})LowSpace(p,"East transfer landing",7,z,6,3,-3,false);
        LowSpace(p,"West transfer landing",-13,60,8,3,-3,false);
        LowSpace(p,"Lower maintenance loop",10,60,4,26,-8,false);
        foreach(float z in new[]{68f,88f})Sign(p,"LOWER LINE",new(14.15f,-6.5f,z),90);
        Sign(p,"PLATFORM 1",new(-8.85f,-1.5f,70),90);
        foreach(var t in p.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Sign ")))t.localScale=Vector3.one*.4f;
        // Supporting rooms occupy the approach/departure thirds and connect to the lower loop.
        LowSpace(p,"Pump service corridor",3,28,7,10,-8,false);
        Wall(p,"Service corridor east wall",true,10,28,38,-8,2.13f,paint);
        LowSpace(p,"Pump room",-6,28,9,10,-8,false);
        foreach(float z in new[]{28f,38f})Wall(p,"Pump room end",false,z,-6,3,-8,2.13f,paint);
        Wall(p,"Pump room back",true,-6,28,38,-8,2.13f,paint);
        Box(p,"Pump machinery",new(-3,-7.2f,32),new(3,1.6f,3),steel);
        LowSpace(p,"Departure service corridor",14,106,5,22,-8,false);
        Wall(p,"Departure corridor wall",true,19,106,128,-8,2.13f,paint);
        Wall(p,"Departure corridor wall",true,14,106,128,-8,2.13f,paint,120,123);
        Wall(p,"Departure corridor end",false,128,14,19,-8,2.13f,paint);
        LowSpace(p,"Electrical room",6,118,8,10,-8,false);
        foreach(float z in new[]{118f,128f})Wall(p,"Electrical end wall",false,z,6,14,-8,2.13f,paint);
        Wall(p,"Electrical back wall",true,6,118,128,-8,2.13f,paint);
        Box(p,"Switch cabinets",new(7,-7.2f,123),new(1,1.6f,5),steel);
    }
}
#endif
