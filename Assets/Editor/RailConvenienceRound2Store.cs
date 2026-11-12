#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using TMPro;
using Object=UnityEngine.Object;

public static partial class RailConvenienceStore
{
    static int accentMesh;
    static Material atlas2;
    static void AtlasCell2(Transform t,int cell)
    {
        var f=t.GetComponent<MeshFilter>();var mesh=Object.Instantiate(f.sharedMesh);
        mesh.uv=Enumerable.Repeat(new Vector2((cell*8+4f)/64,.5f),mesh.vertexCount).ToArray();
        f.sharedMesh=Save2("Store accent UV "+accentMesh++,mesh);t.GetComponent<MeshRenderer>().sharedMaterial=atlas2;
    }
    static Light Spot2(Transform p,string name,Vector3 pos,Vector3 aim,float intensity,float angle,float range,Color color)
    {
        var t=Group(p,name);t.localPosition=pos;t.rotation=Quaternion.LookRotation(p.TransformPoint(aim)-t.position);
        var l=t.gameObject.AddComponent<Light>();l.type=LightType.Spot;l.lightmapBakeType=LightmapBakeType.Baked;l.intensity=intensity;l.spotAngle=angle;l.innerSpotAngle=angle*.65f;l.range=range;l.color=color;l.shadows=LightShadows.Soft;return l;
    }
    public static void RefineRound2Store()
    {
        var b=Round2Block();if(b.transform.Find("Env/Convenience round two/Round two store refinement"))throw new Exception("Store refinement already installed");
        Init2();detailMesh=6000;accentMesh=0;polishBlock=b;authored=b.transform.Find("Env/"+RootName);prior=b.transform.Find("Env/"+PolishName);r2=b.transform.Find("Env/Convenience round two");
        atlas2=AssetDatabase.LoadAssetAtPath<Material>(R2Folder+"/Shared neon atlas.mat");
        var before=b.GetComponentsInChildren<Transform>(true).Select(t=>(t,t.parent,t.localPosition,t.localRotation,t.localScale)).ToArray();Undo.RegisterFullObjectHierarchyUndo(b.gameObject,"Store fixtures and vendor spotlights");
        var root=Group(r2,"Round two store refinement");var store=authored.Find("Store");
        AssetDatabase.StartAssetEditing();try
        {
            // Keep all current poses; replace only fixtures/products explicitly named in feedback.
            var gas=authored.Find("Gas forecourt");foreach(var mr in gas.GetComponentsInChildren<MeshRenderer>().Where(m=>m.sharedMaterial==atlas2))AtlasCell2(mr.transform,3);
            gas.Find("Glowing sign GAS").GetComponentInChildren<TMP_Text>().fontSharedMaterial=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Text-Neon teal.mat");
            foreach(var l in gas.GetComponentsInChildren<Light>())l.color=new Color(.48f,1,.9f);
            var spots=Group(root,"Vendor model spotlights");
            foreach(var model in r2.Cast<Transform>().Where(t=>t.name.Contains("rooftop")).Concat(new[]{prior.Find("Arcade rooftop duck mascot")}).Where(t=>t))
            {
                var renderers=model.GetComponentsInChildren<MeshRenderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
                bool west=model.name.Contains("Quack")||model.name.Contains("Car Wash");bool east=model.name.Contains("Arcade");var front=west?Vector3.left:east?Vector3.right:Vector3.back;var side=Vector3.Cross(Vector3.up,front);
                for(int i=0;i<2;i++)
                {
                    var pos=bounds.center+front*3.5f+side*(i==0?-1.5f:1.5f);pos.y=bounds.min.y+.4f;
                    var aim=bounds.center+Vector3.up*.25f;var light=Spot2(spots,model.name+" spotlight "+i,pos,aim,38,65,12,new Color(1,.9f,.72f));
                    Detail(spots,"Mounted roof floodlight",pos,new Vector3(.5f,.3f,.3f),black2,false,light.transform.rotation);
                }
            }
            foreach(var l in prior.GetComponentsInChildren<Light>().Where(l=>l.name=="Baked mascot uplight"))l.enabled=false;
            foreach(var t in store.Cast<Transform>().Where(t=>t.name=="Fixture"||t.name=="Baked shop light"||t.name=="Baked stock light").ToArray())Delete2(t);
            var ceiling=Group(root,"Long ceiling fluorescent fixtures");int fixture=0;
            foreach(var pos in new[]{new Vector3(26,4.02f,71),new Vector3(32,4.02f,71),new Vector3(26,4.02f,80),new Vector3(32,4.02f,80),new Vector3(38,4.02f,76)})
            {
                bool on=fixture++!=1;var g=Group(ceiling,on?"Fluorescent on":"Fluorescent burnt out");g.localPosition=pos;
                Detail(g,"Ceiling reflector",Vector3.zero,new Vector3(.7f,.14f,3.1f),metal,false);
                foreach(float x in new[]{-.2f,.2f}){var tube=Cylinder(g,on?"Glowing fluorescent tube":"Dead dark tube",new Vector3(x,-.12f,0),.055f,2.8f,on?atlas2:ivory2,8,false);tube.localRotation=Quaternion.Euler(90,0,0);if(on)AtlasCell2(tube,1);}
                if(on)foreach(float z in new[]{-.85f,.85f})Spot2(g,"Baked fluorescent pool",new Vector3(0,-.2f,z),new Vector3(0,-4,z),16,115,7,new Color(.82f,.94f,1));
            }
            Delete2(store.Find("South wall freezer bank"));var coolers=Group(root,"South upright drink coolers");
            var productMats=new[]{Mat2("Stock orange",new Color(.85f,.31f,.07f)),Mat2("Stock mint",new Color(.12f,.52f,.37f)),Mat2("Stock blue",new Color(.08f,.25f,.65f)),Mat2("Stock pink",new Color(.75f,.16f,.36f)),ivory2};
            for(int i=0;i<4;i++)
            {
                var g=Group(coolers,"Upright drinks cooler "+i);g.localPosition=new Vector3(24+i*2.8f,0,63.2f);
                Detail(g,"Insulated rear",new Vector3(0,1.3f,-.57f),new Vector3(2.4f,2.6f,.14f),white);
                foreach(float x in new[]{-1.15f,1.15f})Detail(g,"Cooler side",new Vector3(x,1.3f,0),new Vector3(.12f,2.6f,1.2f),white);
                Detail(g,"Cooler crown",new Vector3(0,2.55f,0),new Vector3(2.4f,.18f,1.2f),white);
                foreach(float y in new[]{.25f,.95f,1.65f,2.32f})Detail(g,"Drink shelf",new Vector3(0,y,0),new Vector3(2.22f,.08f,1.1f),metal);
                AtlasCell2(Detail(g,"Cooler illuminated header",new Vector3(0,2.53f,.62f),new Vector3(2.1f,.12f,.04f),atlas2,false),1);
                for(int door=0;door<2;door++)
                {
                    var h=Group(g,i%2==0?"Open cooler glass door":"Closed cooler glass door");h.localPosition=new Vector3(door==0?-1.1f:1.1f,0,.62f);float s=door==0?1:-1;h.localRotation=Quaternion.Euler(0,i%2==0?(door==0?-100:100):0,0);
                    foreach(float x in new[]{0,s*1.08f})Detail(h,"Door upright",new Vector3(x,1.3f,0),new Vector3(.06f,2.3f,.07f),black2);
                    foreach(float y in new[]{.15f,2.45f})Detail(h,"Door frame rail",new Vector3(s*.54f,y,0),new Vector3(1.14f,.06f,.07f),black2);
                    var glassMat=CoolerGlass2();
                    // Open units expose reachable shelves; closed units use simple dark glazing like existing fridges.
                    Detail(h,"Drinks glass",new Vector3(s*.54f,1.3f,0),new Vector3(1.02f,2.22f,.025f),glassMat,i%2!=0);
                    Detail(h,"Door handle",new Vector3(s*.92f,1.3f,.1f),new Vector3(.06f,.5f,.06f),metal,false);
                }
                for(int row=0;row<3;row++)for(int col=0;col<6;col++)
                {
                    if(i%2==0&&row==2)continue;var p=new Vector3(-.9f+col*.35f,.99f+row*.67f,0);if(row==2)p.y=.29f;
                    Product2(g,p,col%2,productMats[(i+col)%5],.65f,col*13);
                }
            }
            var rng=new System.Random(971);var stock=Group(root,"Varied retail shelf goods");int count=0;
            foreach(var shelf in store.Cast<Transform>().Where(t=>t.name.StartsWith("Shelf run")))
            {
                foreach(var carton in shelf.Cast<Transform>().Where(t=>t.name=="Carton").ToArray())Delete2(carton);
                float z=shelf.Find("Shelf").position.z;
                foreach(float surface in new[]{.24f,.81f,1.36f})foreach(float side in new[]{-.4f,.4f})for(int slot=0;slot<10;slot++)
                {
                    if(rng.NextDouble()<.35)continue;var p=new Vector3(26.4f+slot*.64f+(float)rng.NextDouble()*.1f,surface,z+side+(float)rng.NextDouble()*.09f);
                    var bounds=new Bounds(p+Vector3.up*.22f,new Vector3(.55f,.46f,.48f));if(b.spawnCandidates.coal.Concat(b.spawnCandidates.gold).Any(t=>bounds.Contains(t.position)))continue;
                    Product2(stock,p,count%4,productMats[count%5],.7f+(float)rng.NextDouble()*.28f,(float)rng.NextDouble()*24-12);count++;
                }
            }
            Delete2(store.Find("Stock shelving"));var racks=Group(root,"Back room inventory shelves");
            for(int rack=0;rack<6;rack++)
            {
                float x=rack<3?39.35f:36.65f;float z=65.3f+(rack%3)*4.3f;var g=Group(racks,"Stockroom rack "+rack);g.localPosition=new Vector3(x,0,z);
                foreach(float xx in new[]{-.4f,.4f})foreach(float zz in new[]{-1.65f,1.65f})Detail(g,"Steel rack upright",new Vector3(xx,1.35f,zz),new Vector3(.07f,2.7f,.07f),metal);
                foreach(float y in new[]{.22f,1.05f,1.88f,2.62f})Detail(g,"Inventory shelf",new Vector3(0,y,0),new Vector3(.9f,.08f,3.4f),metal);
                for(int level=0;level<3;level++)for(int j=0;j<4;j++)
                {
                    if((rack+level+j)%5==0)continue;float h=.3f+(j%3)*.13f;var p=new Vector3(0,.26f+level*.83f+h/2,-1.2f+j*.78f);
                    Detail(g,"Sealed stock carton",p,new Vector3(.7f,h,.6f),wood);Detail(g,"Carton tape",p+Vector3.up*(h/2+.008f),new Vector3(.12f,.012f,.61f),ivory2,false);
                }
            }
        }finally{AssetDatabase.StopAssetEditing();}
        var invalid=before.Where(s=>!permitted.Contains(s.t)&&(!s.t||s.t.parent!=s.parent||s.t.localPosition!=s.localPosition||s.t.localRotation!=s.localRotation||s.t.localScale!=s.localScale)).ToArray();
        if(invalid.Length>0)throw new Exception("Unexpected pose change in store pass");
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(R2Evidence+"/store-refinement.txt","PASS all untargeted object poses preserved. Added 12 vendor-model spotlights, five long twin-tube ceiling fixtures (one burnt out), four upright drink coolers (two open), varied retail goods, six inventory racks. Gas neon uses existing teal atlas cell; TMP restored to teal. Loot requires follow-up checks after these model changes.\n");
    }
    static void Product2(Transform parent,Vector3 at,int type,Material color,float scale,float yaw)
    {
        var g=Group(parent,new[]{"Drink bottle","Food tin","Cereal carton","Snack bag"}[type]);g.localPosition=at;g.localScale=Vector3.one*scale;g.localRotation=Quaternion.Euler(0,yaw,0);
        if(type<2){Cylinder(g,type==0?"Bottle body":"Tin body",new Vector3(0,.18f,0),.11f,.36f,color,8,false);Cylinder(g,"Product label",new Vector3(0,.19f,0),.113f,.12f,ivory2,8,false);if(type==0){Cylinder(g,"Bottle neck",new Vector3(0,.4f,0),.05f,.12f,color,8,false);Cylinder(g,"Cap",new Vector3(0,.47f,0),.06f,.04f,black2,8,false);}}
        else{Detail(g,"Package",new Vector3(0,.22f,0),new Vector3(type==2?.3f:.38f,.44f,type==2?.14f:.24f),color,false);Detail(g,"Printed label",new Vector3(0,.24f,type==2?.075f:.125f),new Vector3(.2f,.17f,.015f),ivory2,false);}
    }
    static Material CoolerGlass2()
    {
        var m=Mat2("Cooler clear glazing",new Color(.2f,.42f,.45f,.2f));m.SetFloat("_Surface",1);m.SetFloat("_Blend",0);m.SetFloat("_SrcBlend",5);m.SetFloat("_DstBlend",10);m.SetFloat("_ZWrite",0);m.SetFloat("_Cull",0);m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");m.SetOverrideTag("RenderType","Transparent");m.renderQueue=3000;EditorUtility.SetDirty(m);return m;
    }
    public static void FixCoolerGlazing2()
    {
        var b=Round2Block();var m=CoolerGlass2();foreach(var r in b.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Drinks glass"))r.sharedMaterial=m;PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();
    }
    public static void RepairRound2AfterStore(bool resetCoolerShelves=true,string evidenceName="post-model-loot-repairs.txt")
    {
        var b=Round2Block();Physics.SyncTransforms();var cols=LootColliders2(b);var ps=b.gameObject.scene.GetPhysicsScene();var changes=new List<string>();
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})foreach(var t in RailSpawnPool.Points(b.spawnCandidates,kind))
        {
            var shape=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);var before=t.position;
            if(resetCoolerShelves&&t.name.Contains("open freezer"))
            {
                var p=t.position;p.y=.29f+.05f-shape.min.y;if(!LootValid2(p,shape,cols,ps))throw new Exception("Cooler loot clearance "+t.name);t.position=p;
            }
            else if(!LootValid2(t.position,shape,cols,ps))
            {
                bool found=false;for(float radius=0;radius<3&&!found;radius+=.2f)for(int a=0;a<16&&!found;a++)
                {var ray=new Ray(before+new Vector3(Mathf.Cos(a*Mathf.PI/8)*radius,1.5f,Mathf.Sin(a*Mathf.PI/8)*radius),Vector3.down);var support=new List<RaycastHit>();foreach(var c in cols)if(c.Raycast(ray,out var hit,4)&&hit.normal.y>.7f)support.Add(hit);
                    foreach(var hit in support.OrderBy(h=>Mathf.Abs(h.point.y-before.y-shape.min.y))){var p=hit.point+Vector3.up*(.05f-shape.min.y);if(!LootValid2(p,shape,cols,ps)||RailSpawnPool.Points(b.spawnCandidates,kind).Any(o=>o!=t&&Vector3.Distance(o.position,p)<.65f))continue;t.position=p;found=true;break;}}
                if(!found)throw new Exception("No local placement for "+t.name);
            }
            if(t.position!=before){PrefabUtility.RecordPrefabInstancePropertyModifications(t);changes.Add(t.name+" "+before+" -> "+t.position);}
        }
        // One existing enemy marker is now inside the user's moved trailer: keep the trailer, adjust its candidate nearby.
        foreach(var t in b.spawnCandidates.enemies)
        {
            var hits=new Collider[64];if(ps.OverlapCapsule(t.position+Vector3.up*.4f,t.position+Vector3.up*1.7f,.28f,hits,1<<6,QueryTriggerInteraction.Ignore)==0)continue;
            var before=t.position;bool found=false;for(float radius=.5f;radius<5&&!found;radius+=.5f)for(int a=0;a<16&&!found;a++){var p=before+new Vector3(Mathf.Cos(a*Mathf.PI/8)*radius,0,Mathf.Sin(a*Mathf.PI/8)*radius);if(ps.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.7f,.28f,hits,1<<6,QueryTriggerInteraction.Ignore)>0)continue;t.position=p;found=true;}
            if(!found)throw new Exception("No local enemy clearance");PrefabUtility.RecordPrefabInstancePropertyModifications(t);changes.Add("Enemy "+before+" -> "+t.position);
        }
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();File.WriteAllLines(R2Evidence+"/"+evidenceName,changes);
    }
}
#endif
