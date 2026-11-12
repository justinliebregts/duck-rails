#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TMPro;
using Object=UnityEngine.Object;

public static partial class RailConvenienceStore
{
    public static void PreparePolishBake()
    {
        var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();if(stage==null||stage.assetPath!=PrefabPath)throw new Exception("Open Convenience Store");
        var b=stage.prefabContentsRoot.GetComponent<RailBlock>();var a=b.transform.Find("Env/"+RootName);var extra=b.transform.Find("Env/"+PolishName);
        var gravel=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville gravel.mat");var concrete=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/CampgroundGreybox/Quackville concrete.mat");
        var asphalt=DetailMat("Access road asphalt",new Color(.22f,.23f,.22f),gravel);var courtMat=DetailMat("Faded court surface",new Color(.17f,.26f,.24f),concrete);
        foreach(var t in extra.Find("Car wash access road").Cast<Transform>().Where(t=>!t.name.Contains("paint")))t.GetComponent<Renderer>().sharedMaterial=asphalt;
        extra.Find("Northwest basketball court/Court surface").GetComponent<Renderer>().sharedMaterial=courtMat;
        foreach(var t in a.Find("Outer thirds").Cast<Transform>().Where(t=>(t.name=="Fence fragment"||t.name=="Fence post")&&t.position.x>40&&t.position.z<55).ToArray())Object.DestroyImmediate(t.gameObject);
        foreach(var l in extra.Find("Arcade rooftop duck mascot").GetComponentsInChildren<Light>())l.intensity=16;
        var parents=extra.GetComponentsInChildren<Transform>().Concat(a.GetComponentsInChildren<Transform>().Where(t=>t.name=="Braced picnic table"||t.name=="Distinct dining room"||t.name=="Rotary wash equipment"||t.name.StartsWith("Chest freezer "))).Distinct().ToArray();int combined=0;
        foreach(var parent in parents)
        {
            if(parent.GetComponentInParent<RailWashBrushSpin>())continue;
            var renderers=parent.Cast<Transform>().Where(t=>t.GetComponent<MeshFilter>()&&t.GetComponent<MeshRenderer>()&&t.childCount==0&&!t.GetComponent<TMP_Text>()).Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r.sharedMaterials.Length==1&&GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.ContributeGI)).ToArray();
            foreach(var set in renderers.GroupBy(r=>r.sharedMaterial).Where(g=>g.Count()>=4))
            {
                var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(set.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=parent.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray(),true,true);Unwrapping.GenerateSecondaryUVSet(mesh);
                var t=Group(parent,"Combined details "+set.Key.name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=SaveDetail("Combined "+combined++,mesh);var rr=t.gameObject.AddComponent<MeshRenderer>();rr.sharedMaterial=set.Key;rr.receiveGI=ReceiveGI.Lightmaps;rr.scaleInLightmap=.3f;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);
                foreach(var r in set){Object.DestroyImmediate(r.GetComponent<MeshFilter>());Object.DestroyImmediate(r);}
            }
        }
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();
        // This new folder belongs only to this pass. Retain all assets referenced by the saved prefab.
        var used=new System.Collections.Generic.HashSet<string>(AssetDatabase.GetDependencies(PrefabPath,true));
        var unused=AssetDatabase.FindAssets("t:Mesh",new[]{PolishFolder}).Select(AssetDatabase.GUIDToAssetPath).Where(path=>path.StartsWith(PolishFolder+"/",StringComparison.Ordinal)&&!used.Contains(path)).ToArray();
        AssetDatabase.StartAssetEditing();try{foreach(var path in unused)AssetDatabase.DeleteAsset(path);}finally{AssetDatabase.StopAssetEditing();}
        AssetDatabase.SaveAssets();Debug.Log("Consolidated static detail groups: "+combined);
    }
    public static void FinishPolishGeometry()
    {
        var stage=UnityEditor.SceneManagement.PrefabStageUtility.GetCurrentPrefabStage();if(stage==null||stage.assetPath!=PrefabPath)throw new Exception("Open Convenience Store");
        InitPolish();detailMesh=2000;var b=stage.prefabContentsRoot.GetComponent<RailBlock>();var a=b.transform.Find("Env/"+RootName);var extra=b.transform.Find("Env/"+PolishName);
        if(extra.Find("Final shell seals"))throw new Exception("Final geometry already applied");var seals=Group(extra,"Final shell seals");
        foreach(var r in a.Find("Restaurant plaza").Cast<Transform>().Where(t=>t.Find("Kitchen range")))
        {float x=r.Find("Roof").localPosition.x;foreach(var side in r.Cast<Transform>().Where(t=>t.name=="Roof side coping"))side.localPosition+=Vector3.down*.12f;Detail(seals,"Restaurant rear roof seal",r.TransformPoint(new Vector3(x,3.65f,114)),new Vector3(10.2f,.3f,.3f),metal);}
        foreach(var spec in new[]{("Store",31f,74f,18f,24f),("Arcade",-28f,76f,18f,20f)})
        {foreach(float x in new[]{spec.Item2-spec.Item4/2,spec.Item2+spec.Item4/2})Detail(seals,"Continuous roof side edge",new Vector3(x,4.22f,spec.Item3),new Vector3(.32f,.22f,spec.Item5+.2f),metal);foreach(float z in new[]{spec.Item3-spec.Item5/2,spec.Item3+spec.Item5/2})Detail(seals,"Continuous roof end edge",new Vector3(spec.Item2,4.22f,z),new Vector3(spec.Item4+.2f,.22f,.32f),metal);}
        var wash=a.Find("Car wash");foreach(float x in new[]{-6f,6f})Detail(wash,"Wash roof side closure",new Vector3(x,4.05f,0),new Vector3(.3f,.3f,14.2f),metal);foreach(float z in new[]{-7f,7f})Detail(wash,"Wash roof end closure",new Vector3(0,4.05f,z),new Vector3(12.2f,.3f,.3f),metal);
        var semi=extra.Find("Broken articulated semi");var semiBounds=new Bounds(semi.position,Vector3.zero);foreach(var r in semi.GetComponentsInChildren<Renderer>())semiBounds.Encapsulate(r.bounds);
        int moved=0;foreach(var tree in a.GetComponentsInChildren<Transform>().Where(t=>t.name=="Pine silhouette"))
        {var trunk=tree.Find("Trunk").GetComponent<Collider>().bounds;if(trunk.Intersects(semiBounds)){tree.position=new Vector3(-41,0,143+moved*7);moved++;}}
        var cab=semi.Find("Turning semi tractor");cab.Find("Cab body").GetComponent<Renderer>().sharedMaterial=DetailMat("Faded semi red",new Color(.5f,.13f,.08f));
        // These new bodies have explicit physical shells, while decorative mascot and brush fins use no collision.
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(stage.scene);PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();Debug.Log("Final shell seals installed; moved "+moved+" trees clear of semi");
    }
    static void CourtAndTruck(Transform a)
    {
        var outer=a.Find("Outer thirds");RemoveNamed(outer,"Utility pad","Utility cabinet","Utility crates");
        var court=Group(extras,"Northwest basketball court");
        Detail(court,"Court surface",new Vector3(-27,.035f,129),new Vector3(22,.05f,16),dark,false);
        foreach(float x in new[]{-37f,-17f})Detail(court,"Sideline",new Vector3(x,.075f,129),new Vector3(.1f,.02f,14),white,false);
        foreach(float z in new[]{122f,136f})Detail(court,"Baseline",new Vector3(-27,.075f,z),new Vector3(20,.02f,.1f),white,false);
        Detail(court,"Half court line",new Vector3(-27,.075f,129),new Vector3(.1f,.02f,14),white,false);
        Ring(court,"Centre circle",new Vector3(-27,.085f,129),1.65f,.055f,white);
        foreach(float x in new[]{-35.6f,-18.4f})
        {
            float sign=x<-27?1:-1;Detail(court,"Hoop post",new Vector3(x-sign*.8f,1.8f,129),new Vector3(.18f,3.6f,.18f),metal);
            Beam(court,"Hoop support",new Vector3(x-sign*.8f,3.4f,129),new Vector3(x,3.4f,129),.15f,metal);
            Detail(court,"Backboard",new Vector3(x,3.45f,129),new Vector3(.13f,1.15f,1.8f),white);
            var hoop=new Vector3(x+sign*.48f,3.04f,129);Ring(court,"Orange hoop",hoop,.46f,.055f,orangeDuck);
            for(int k=0;k<10;k++){float angle=k*Mathf.PI/5;Beam(court,"Basket net",hoop+new Vector3(Mathf.Cos(angle)*.43f,-.03f,Mathf.Sin(angle)*.43f),hoop+new Vector3(Mathf.Cos(angle)*.28f,-.5f,Mathf.Sin(angle)*.28f),.018f,white,false);}
            foreach(float z in new[]{126.5f,131.5f})Detail(court,"Key paint",new Vector3(x+sign*2.2f,.08f,z),new Vector3(4.4f,.02f,.08f),white,false);
            Detail(court,"Free throw line",new Vector3(x+sign*4.4f,.08f,129),new Vector3(.08f,.02f,5),white,false);
        }
        // East entrance is a 3m clear opening; its visible gate is secured fully open.
        CourtFence(court,new Vector3(-39,0,120),new Vector3(-39,0,138));CourtFence(court,new Vector3(-39,0,120),new Vector3(-15,0,120));CourtFence(court,new Vector3(-39,0,138),new Vector3(-15,0,138));
        CourtFence(court,new Vector3(-15,0,120),new Vector3(-15,0,127.5f));CourtFence(court,new Vector3(-15,0,130.5f),new Vector3(-15,0,138));
        var gate=Group(court,"Open court gate");gate.localPosition=new Vector3(-15,0,130.5f);CourtFence(gate,Vector3.zero,new Vector3(3,0,0));
        Detail(court,"Courtside bench seat",new Vector3(-36,.5f,137),new Vector3(4,.15f,.6f),wood);foreach(float x in new[]{-37.5f,-34.5f})Detail(court,"Bench support",new Vector3(x,.22f,137),new Vector3(.15f,.44f,.5f),metal);
        // Relocate only candidates whose utility props were removed. Keep all candidate identities.
        int ci=0,gi=0;foreach(var t in polishBlock.spawnCandidates.coal.Concat(polishBlock.spawnCandidates.gold).Where(t=>t.position.x>=-38&&t.position.x<=-14&&t.position.z>=120&&t.position.z<=136))
        {bool gold=t.GetComponent<RailSpawnMarker>().kind==RailSpawnKind.Gold;int i=gold?gi++:ci++;MoveMarker(t,new Vector3(-36+i*2.5f,gold?.13f:.28f,gold?134.5f:123.5f));}
        SemiTruck();
    }
    static void Ring(Transform p,string n,Vector3 centre,float radius,float thick,Material m)
    {for(int i=0;i<20;i++){float a=i*Mathf.PI/10,b=(i+1)*Mathf.PI/10;Beam(p,n,centre+new Vector3(Mathf.Cos(a)*radius,0,Mathf.Sin(a)*radius),centre+new Vector3(Mathf.Cos(b)*radius,0,Mathf.Sin(b)*radius),thick,m,false);}}
    static void CourtFence(Transform p,Vector3 start,Vector3 end)
    {
        float length=(end-start).magnitude;var d=(end-start).normalized;
        for(float f=0;f<length+.01f;f+=Mathf.Min(3,length))Detail(p,"Court fence post",start+d*f+Vector3.up*1.5f,new Vector3(.09f,3,.09f),metal);
        foreach(float y in new[]{.12f,2.95f})Beam(p,"Court fence rail",start+Vector3.up*y,end+Vector3.up*y,.07f,metal);
        for(float f=.4f;f<length;f+=.5f)Beam(p,"Court fence wire",start+d*f+Vector3.up*.15f,start+d*f+Vector3.up*2.92f,.018f,metal,false);
        foreach(float y in new[]{.6f,1.1f,1.6f,2.1f,2.6f})Beam(p,"Court horizontal wire",start+Vector3.up*y,end+Vector3.up*y,.018f,metal,false);
        var barrier=Group(p,"Court fence collision");barrier.localPosition=(start+end)/2+Vector3.up*1.5f;barrier.localRotation=Quaternion.LookRotation(d);barrier.gameObject.layer=6;barrier.gameObject.AddComponent<BoxCollider>().size=new Vector3(.08f,3,length);
    }
    static void SemiTruck()
    {
        var rig=Group(extras,"Broken articulated semi");rig.localPosition=new Vector3(-27,0,149);rig.localRotation=Quaternion.Euler(0,68,0);
        var trailer=Group(rig,"Enterable open trailer");
        Detail(trailer,"Trailer deck",new Vector3(0,1.02f,0),new Vector3(3.2f,.24f,10.5f),wood);
        foreach(float x in new[]{-1.6f,1.6f}){Detail(trailer,"Trailer side",new Vector3(x,2.52f,0),new Vector3(.15f,2.8f,10.5f),white);for(float z=-4.5f;z<=4.5f;z+=1)Detail(trailer,"Side rib",new Vector3(x*1.06f,2.52f,z),new Vector3(.08f,2.8f,.08f),metal,false);}
        Detail(trailer,"Trailer roof",new Vector3(0,3.98f,0),new Vector3(3.35f,.16f,10.6f),metal);Detail(trailer,"Front bulkhead",new Vector3(0,2.52f,5.25f),new Vector3(3.2f,2.8f,.15f),white);
        foreach(float side in new[]{-1f,1f})
        {var hinge=Group(trailer,"Rear door held open");hinge.localPosition=new Vector3(side*1.6f,0,-5.28f);hinge.localRotation=Quaternion.Euler(0,side*112,0);Detail(hinge,"Door leaf",new Vector3(-side*.77f,2.52f,0),new Vector3(1.54f,2.8f,.14f),white);Detail(hinge,"Door lock bar",new Vector3(-side*.77f,2.52f,-.12f),new Vector3(.07f,2.5f,.07f),metal,false);}
        // Broad, gentle plank ramp makes the trailer walk-in without a mandatory jump.
        var ramp=Detail(trailer,"Rear loading ramp",new Vector3(0,.57f,-7.1f),new Vector3(2.6f,.16f,3.9f),wood,true,Quaternion.Euler(-17,0,0));
        foreach(float z in new[]{-3.7f,-2.3f})foreach(float x in new[]{-1.55f,1.55f})Wheel(trailer,new Vector3(x,.55f,z),.55f,.45f);
        foreach(float x in new[]{-.85f,.85f})Detail(trailer,"Landing leg",new Vector3(x,.5f,3.5f),new Vector3(.18f,1,.18f),metal);
        var cab=Group(rig,"Turning semi tractor");cab.localPosition=new Vector3(0,0,6.3f);cab.localRotation=Quaternion.Euler(0,-32,0);
        Detail(cab,"Tractor chassis",new Vector3(0,.65f,0),new Vector3(2.7f,.35f,5.6f),dark);
        Detail(cab,"Cab body",new Vector3(0,1.65f,1.1f),new Vector3(2.8f,2.2f,2.5f),redGlow);
        Detail(cab,"Diesel hood",new Vector3(0,1.2f,3),new Vector3(2.5f,1.2f,1.5f),metal);
        Detail(cab,"Windshield",new Vector3(0,2.15f,2.37f),new Vector3(2.4f,.9f,.05f),blue,false);
        Detail(cab,"Radiator grille",new Vector3(0,1.17f,3.8f),new Vector3(1.8f,.9f,.08f),dark,false);
        for(int i=0;i<7;i++)Detail(cab,"Grille slat",new Vector3(-.75f+i*.25f,1.17f,3.86f),new Vector3(.06f,.85f,.04f),metal,false);
        foreach(float x in new[]{-1.4f,1.4f}){foreach(float z in new[]{-1.7f,-.4f,2.8f})Wheel(cab,new Vector3(x,.6f,z),.6f,.45f);Cylinder(cab,"Exhaust stack",new Vector3(x,2.2f,-.2f),.12f,3.5f,metal,8);}
        Detail(cab,"Dropped bumper",new Vector3(.3f,.36f,3.95f),new Vector3(2.9f,.25f,.22f),metal,true,Quaternion.Euler(0,0,-9));
        for(int i=0;i<6;i++)AddLoot(RailSpawnKind.Gold,trailer.TransformPoint(new Vector3((i%2==0?-.75f:.75f),1.14f,-2+i/2*1.8f)),"trailer cargo "+i);
    }
    static void Wheel(Transform p,Vector3 pos,float radius,float width)
    {var t=Cylinder(p,"Rubber wheel",pos,radius,width,dark,12);t.localRotation=Quaternion.Euler(0,0,90);var hub=Cylinder(p,"Wheel hub",pos+Vector3.right*Mathf.Sign(pos.x)*width*.52f,radius*.48f,.06f,metal,8,false);hub.localRotation=t.localRotation;}
    static void CarsPolish(Transform a)
    {
        var originals=a.GetComponentsInChildren<Transform>().Where(t=>t.name=="Abandoned car").ToArray();int count=originals.Length;
        // Use the already-authored finale cars, including their actual wheels/windows/bumpers.
        var finale=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/bridge-finale-v1.prefab");
        var sources=finale.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith("Abandoned car ")).ToArray();if(sources.Length==0)throw new Exception("Finale cars missing");
        var poses=new[]{new Vector3(12,0,65),new Vector3(14,0,73),new Vector3(-35,0,93),new Vector3(-27,0,96),new Vector3(-18,0,91),new Vector3(-14,0,98),new Vector3(-35,0,58),new Vector3(-16,0,58),new Vector3(31,0,25),new Vector3(36,0,113),new Vector3(-39,0,113),new Vector3(17,0,141)};
        var group=Group(extras,"Scattered abandoned cars");
        for(int i=0;i<count*2;i++)
        {var copy=Object.Instantiate(sources[i%sources.Length].gameObject,group).transform;copy.name="Scattered car "+i+(i==7||i==10?" overturned":"");copy.localScale=Vector3.one;copy.localRotation=Quaternion.Euler(0,new[]{82f,106f,-15f,31f,-42f,64f,16f,117f,-28f,73f,133f,25f}[i%12],i==7||i==10?180:0);copy.localPosition=poses[i%poses.Length];var bounds=copy.GetComponentsInChildren<Renderer>().Select(r=>r.bounds).ToArray();float low=bounds.Min(b=>b.min.y);copy.position+=Vector3.up*(.03f-low);}
        // Existing loot on the old cars is lowered to the surrounding pavement for review.
        foreach(var car in originals){var r=car.GetComponentsInChildren<Renderer>();var b=r[0].bounds;foreach(var rr in r.Skip(1))b.Encapsulate(rr.bounds);foreach(var t in polishBlock.spawnCandidates.coal.Concat(polishBlock.spawnCandidates.gold).Where(t=>t.position.x>b.min.x-.2f&&t.position.x<b.max.x+.2f&&t.position.z>b.min.z-.2f&&t.position.z<b.max.z+.2f&&t.position.y>.3f))MoveMarker(t,new Vector3(b.max.x+1,t.GetComponent<RailSpawnMarker>().kind==RailSpawnKind.Coal?.2f:.05f,t.position.z));Object.DestroyImmediate(car.gameObject);}
    }
    static void RestaurantPolish(Transform a)
    {
        var plaza=a.Find("Restaurant plaza");int index=0;
        foreach(string name in new[]{"LAST SLICE","DUCK DINER","NOODLE BOX"})
        {
            var r=plaza.Find(name);float cx=r.Find("Roof").localPosition.x;RemoveNamed(r,"Dining table","Booth bench");var furniture=Group(r,"Distinct dining room");
            if(index==0)
            {
                foreach(float z in new[]{104.5f,107.5f}){DiningTable(furniture,new Vector3(cx-3,0,z),1.8f,1.4f);Chair(furniture,new Vector3(cx-4.2f,0,z),90);Chair(furniture,new Vector3(cx-1.8f,0,z),-90);}
                Detail(furniture,"Pizza display ledge",new Vector3(cx+3.3f,1.12f,107),new Vector3(2,.12f,.7f),white);
                foreach(float z in new[]{106.5f,108f}){Cylinder(furniture,"Pizza pan",new Vector3(cx+3.2f,1.02f,z),.42f,.04f,metal,12,false);Cylinder(furniture,"Pizza",new Vector3(cx+3.2f,1.06f,z),.36f,.06f,yellowDuck,12,false);}
                Detail(furniture,"Pizza oven",new Vector3(cx+3.5f,1.7f,112.3f),new Vector3(2,1.3f,1.3f),metal);Detail(furniture,"Oven mouth",new Vector3(cx+3.5f,1.6f,111.6f),new Vector3(1.5f,.35f,.06f),dark,false);
            }
            else if(index==1)
            {
                foreach(float z in new[]{104.5f,107.5f}){DiningTable(furniture,new Vector3(cx-2.7f,0,z),1.5f,1.2f);foreach(float dx in new[]{-1.05f,1.05f}){Detail(furniture,"Upholstered booth seat",new Vector3(cx-2.7f+dx,.45f,z),new Vector3(.7f,.55f,1.6f),blue);Detail(furniture,"Booth seat back",new Vector3(cx-2.7f+dx*1.3f,.95f,z),new Vector3(.18f,.85f,1.6f),blue);}}
                Detail(furniture,"Diner counter extension",new Vector3(cx+3.25f,.95f,104.7f),new Vector3(1.4f,.2f,3.3f),white);foreach(float z in new[]{103.8f,105.2f}){Cylinder(furniture,"Chrome stool stem",new Vector3(cx+1.8f,.35f,z),.09f,.7f,metal);Cylinder(furniture,"Round stool seat",new Vector3(cx+1.8f,.74f,z),.36f,.14f,blue);}
                Cylinder(furniture,"Coffee urn",new Vector3(cx+3.4f,1.3f,108.5f),.25f,.6f,metal);
            }
            else
            {
                Detail(furniture,"Noodle bar top",new Vector3(cx-3.6f,1.05f,106),new Vector3(1.35f,.18f,6),wood);foreach(float z in new[]{104f,106f,108f}){Cylinder(furniture,"Noodle bar stool",new Vector3(cx-2.3f,.4f,z),.3f,.8f,metal);Cylinder(furniture,"Serving bowl",new Vector3(cx-3.6f,1.2f,z),.22f,.15f,white,10,false);}
                DiningTable(furniture,new Vector3(cx+2,0,104),2.2f,1.3f);Chair(furniture,new Vector3(cx+2,0,102.9f),180);Chair(furniture,new Vector3(cx+2,0,105.1f),0);
                foreach(float z in new[]{104f,107f}){Cylinder(furniture,"Red pendant lantern",new Vector3(cx,2.6f,z),.35f,.6f,redGlow,10,false);Beam(furniture,"Lantern cable",new Vector3(cx,2.9f,z),new Vector3(cx,3.65f,z),.03f,dark,false);}
            }
            Detail(furniture,"Kitchen ventilation hood",new Vector3(cx+3,2.4f,112.25f),new Vector3(2.3f,.65f,1.8f),metal);Detail(furniture,"Extraction duct",new Vector3(cx+3,3.1f,112.25f),new Vector3(.6f,1,.6f),metal);
            index++;
        }
    }
    static void DiningTable(Transform p,Vector3 pos,float w,float d)
    {Detail(p,"Dining tabletop",pos+Vector3.up*.78f,new Vector3(w,.14f,d),wood);foreach(float x in new[]{-w/2+.15f,w/2-.15f})foreach(float z in new[]{-d/2+.15f,d/2-.15f})Detail(p,"Table leg",pos+new Vector3(x,.36f,z),new Vector3(.12f,.72f,.12f),metal);}
    static void Chair(Transform p,Vector3 pos,float yaw)
    {var g=Group(p,"Dining chair");g.localPosition=pos;g.localRotation=Quaternion.Euler(0,yaw,0);Detail(g,"Chair seat",new Vector3(0,.46f,0),new Vector3(.6f,.12f,.6f),wood);Detail(g,"Chair back",new Vector3(0,.85f,.24f),new Vector3(.6f,.7f,.1f),wood);foreach(float x in new[]{-.23f,.23f})foreach(float z in new[]{-.23f,.23f})Detail(g,"Chair leg",new Vector3(x,.2f,z),new Vector3(.08f,.4f,.08f),metal);}
    static void RoofEquipment(Transform a)
    {
        var roofs=a.GetComponentsInChildren<Transform>().Where(t=>t.name=="Roof"||t.name=="Canopy roof"||t.name.EndsWith("shelter roof")).ToArray();int i=0;
        foreach(var roof in roofs)
        {
            var b=roof.GetComponent<Renderer>().bounds;var p=Group(extras,"Rooftop equipment "+roof.parent.name);var centre=new Vector3(b.center.x,b.max.y,b.center.z);
            if(i%3==0)
            {Detail(p,"HVAC plinth",centre+Vector3.up*.12f,new Vector3(3.2f,.24f,2.2f),metal);Detail(p,"HVAC casing",centre+Vector3.up*.75f,new Vector3(3,1.1f,2),metal);foreach(float x in new[]{-.8f,.8f}){Cylinder(p,"Fan grille",centre+new Vector3(x,1.35f,0),.65f,.08f,dark,12,false);for(int k=0;k<5;k++)Detail(p,"Cooling fin",centre+new Vector3(x,1.41f,(k-2)*.2f),new Vector3(1.05f,.03f,.025f),metal,false);}}
            else if(i%3==1)
            {Cylinder(p,"Water tank",centre+Vector3.up*1.1f,1.05f,1.8f,metal);Cylinder(p,"Tank cap",centre+Vector3.up*2.04f,1.1f,.1f,dark);foreach(float x in new[]{-.7f,.7f})Detail(p,"Tank foot",centre+new Vector3(x,.16f,0),new Vector3(.15f,.32f,1.4f),metal);Beam(p,"Tank outlet",centre+new Vector3(1,1,0),centre+new Vector3(1.5f,1,0),.12f,metal);}
            else {Detail(p,"Ventilation duct",centre+Vector3.up*.45f,new Vector3(3,.65f,.75f),metal);foreach(float x in new[]{-1f,1f}){Cylinder(p,"Vent riser",centre+new Vector3(x,1.1f,0),.3f,1.2f,metal);Cylinder(p,"Mushroom vent cap",centre+new Vector3(x,1.75f,0),.5f,.12f,dark);}}
            i++;
        }
    }
    static void Freezers(Transform a)
    {
        var store=a.Find("Store");var p=Group(store,"South wall freezer bank");
        for(int i=0;i<4;i++)
        {
            float x=24+i*2.8f;var f=Group(p,"Chest freezer "+i+(i%2==0?" open":" closed"));f.localPosition=new Vector3(x,0,63.2f);
            Detail(f,"Freezer interior base",new Vector3(0,.35f,0),new Vector3(2.3f,.15f,1.4f),white);foreach(float xx in new[]{-1.15f,1.15f})Detail(f,"Freezer end",new Vector3(xx,.6f,0),new Vector3(.12f,.85f,1.5f),white);foreach(float z in new[]{-.7f,.7f})Detail(f,"Freezer side",new Vector3(0,.6f,z),new Vector3(2.3f,.85f,.12f),metal);
            var hinge=Group(f,"Lid hinge");hinge.localPosition=new Vector3(0,1.05f,-.76f);hinge.localRotation=Quaternion.Euler(i%2==0?-75:0,0,0);Detail(hinge,"Freezer lid",new Vector3(0,0,.76f),new Vector3(2.45f,.12f,1.6f),white);Detail(hinge,"Lid handle",new Vector3(0,.09f,1.35f),new Vector3(.6f,.07f,.1f),metal,false);
            if(i%2==0){AddLoot(RailSpawnKind.Gold,f.TransformPoint(new Vector3(-.55f,.425f,0)),"open freezer "+i);AddLoot(RailSpawnKind.Coal,f.TransformPoint(new Vector3(.55f,.425f,0)),"open freezer "+i);}
        }
    }
    // Flat-shaded latitude rings keep the mascot intentionally low-poly.
    static Transform Blob(Transform p,string n,Vector3 pos,Vector3 size,Material mat,int rings=5,int sides=10)
    {
        var g=Group(p,n);g.localPosition=pos;var vs=new System.Collections.Generic.List<Vector3>();var ts=new System.Collections.Generic.List<int>();
        Vector3 Point(int y,int x){float phi=Mathf.PI*y/rings,theta=Mathf.PI*2*x/sides;return Vector3.Scale(new Vector3(Mathf.Sin(phi)*Mathf.Cos(theta),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(theta)),size*.5f);}
        void Tri(Vector3 a,Vector3 b,Vector3 c){int i=vs.Count;vs.AddRange(new[]{a,b,c});ts.AddRange(new[]{i,i+1,i+2});}
        for(int y=0;y<rings;y++)for(int x=0;x<sides;x++){var aa=Point(y,x);var b=Point(y,x+1);var c=Point(y+1,x);var d=Point(y+1,x+1);if(y>0)Tri(aa,b,c);if(y<rings-1)Tri(b,d,c);}
        var m=new Mesh();m.SetVertices(vs);m.SetTriangles(ts,0);m.RecalculateNormals();Unwrapping.GenerateSecondaryUVSet(m);g.gameObject.AddComponent<MeshFilter>().sharedMesh=SaveDetail("Mascot "+detailMesh++,m);var r=g.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.receiveGI=UnityEngine.ReceiveGI.Lightmaps;r.scaleInLightmap=.4f;GameObjectUtility.SetStaticEditorFlags(g.gameObject,StaticEditorFlags.ContributeGI);return g;
    }
    static void ArcadeMascot(Transform a)
    {
        var p=Group(extras,"Arcade rooftop duck mascot");p.localPosition=new Vector3(-22,4.5f,76);p.localRotation=Quaternion.Euler(0,90,0);
        Detail(p,"Mascot mounting plinth",new Vector3(0,.15f,0),new Vector3(3.6f,.3f,3.2f),metal);
        Blob(p,"Oversized duck body",new Vector3(0,1.8f,0),new Vector3(2.8f,3.1f,2.5f),yellowDuck);
        Blob(p,"Big goofy head",new Vector3(0,4,.15f),new Vector3(3.2f,2.7f,2.8f),yellowDuck);
        Blob(p,"Broad upper bill",new Vector3(0,3.65f,1.6f),new Vector3(2.8f,.65f,1.6f),orangeDuck,3,8);
        Blob(p,"Smiling mouth gap",new Vector3(0,3.36f,1.65f),new Vector3(2.35f,.16f,1.35f),dark,3,8);
        Blob(p,"Lower bill",new Vector3(0,3.19f,1.55f),new Vector3(2.5f,.3f,1.4f),orangeDuck,3,8);
        foreach(float x in new[]{-.68f,.68f}){Blob(p,"Big white eye",new Vector3(x,4.55f,1.23f),new Vector3(.93f,1.16f,.42f),white,4,8);Blob(p,"Offbeat pupil",new Vector3(x+(x<0?.12f:-.06f),4.5f+(x<0?.12f:0),1.44f),new Vector3(.33f,.5f,.18f),dark,4,8);Blob(p,"Eye glint",new Vector3(x+.07f,4.68f,1.53f),new Vector3(.1f,.13f,.06f),white,3,6);}
        var wing=Blob(p,"Raised waving wing",new Vector3(-1.75f,3.2f,0),new Vector3(1.1f,2.9f,.8f),yellowDuck);wing.localRotation=Quaternion.Euler(0,0,-30);Blob(p,"Resting wing",new Vector3(1.4f,1.8f,.1f),new Vector3(.7f,2,.9f),yellowDuck);
        foreach(float x in new[]{-.8f,.8f})Blob(p,"Webbed foot",new Vector3(x,.55f,.7f),new Vector3(1.3f,.5f,1.7f),orangeDuck,3,8);
        Blob(p,"Bow tie left",new Vector3(-.4f,2.9f,1.17f),new Vector3(.8f,.6f,.35f),redGlow,3,4);Blob(p,"Bow tie right",new Vector3(.4f,2.9f,1.17f),new Vector3(.8f,.6f,.35f),redGlow,3,4);Blob(p,"Bow tie knot",new Vector3(0,2.9f,1.32f),new Vector3(.35f,.4f,.3f),orangeDuck,3,6);
        Detail(p,"Tiny tilted hat brim",new Vector3(.3f,5.35f,0),new Vector3(1.55f,.13f,1.1f),blue,false,Quaternion.Euler(0,0,-12));Cylinder(p,"Tiny mascot hat",new Vector3(.35f,5.7f,0),.5f,.7f,blue,8,false);
        foreach(float x in new[]{-1.4f,1.4f}){var l=Group(p,"Baked mascot uplight").gameObject.AddComponent<Light>();l.transform.localPosition=new Vector3(x,.4f,2);l.transform.localRotation=Quaternion.LookRotation(new Vector3(-x,3,-1.5f));l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.color=new Color(1,.78f,.45f);l.intensity=45;l.range=9;l.spotAngle=75;}
    }
}
#endif
