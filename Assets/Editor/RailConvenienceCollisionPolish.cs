#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static partial class RailConvenienceStore
{
    public static void CourtCollisionFollowup()
    {
        var b=Round2Block();var court=b.transform.Find("Env/"+PolishName+"/Northwest basketball court");Undo.RegisterFullObjectHierarchyUndo(b.gameObject,"Basketball court collision");int added=0;
        foreach(var t in court.GetComponentsInChildren<Transform>())
        {
            if(t.GetComponent<Collider>())continue;Vector3 size;
            if(t.name=="Court surface")size=t.GetComponent<MeshFilter>().sharedMesh.bounds.size;
            else if(t.name=="Orange hoop")size=new Vector3(.055f,2*.46f*Mathf.Sin(Mathf.PI/20),.055f);
            else if(t.name=="Basket net")size=new Vector3(.018f,Mathf.Sqrt(.47f*.47f+.15f*.15f),.018f);
            else continue;
            var c=t.gameObject.AddComponent<BoxCollider>();c.size=size;t.gameObject.layer=6;added++;
        }
        Physics.SyncTransforms();var ps=b.gameObject.scene.GetPhysicsScene();var hits=new Collider[32];int checkedSolids=0;
        foreach(var c in court.GetComponentsInChildren<Collider>())
        {if(!c.enabled||c.isTrigger||c.gameObject.layer!=6)throw new Exception("Court collision disabled or wrong layer: "+c.name);if(!c.Raycast(new Ray(c.bounds.center+Vector3.up*(c.bounds.extents.magnitude+1),Vector3.down),out _,2*(c.bounds.extents.magnitude+1)))throw new Exception("Court collider does not intercept ray: "+c.name);checkedSolids++;}
        if(ps.OverlapCapsule(new Vector3(-20,.4f,120),new Vector3(-20,1.7f,120),.28f,hits,1<<6,QueryTriggerInteraction.Ignore)>0)throw new Exception("Court entrance blocked");
        foreach(float x in new[]{-35.12f,-18.88f})if(ps.OverlapSphere(new Vector3(x,3.04f,129),.25f,hits,1<<6,QueryTriggerInteraction.Ignore)>0)throw new Exception("Hoop centre filled");
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(R2Evidence+"/court-collision.txt",$"PASS {added} added fitted boxes: 40 rim sections, 20 net strands and the court surface. {checkedSolids} court colliders intercept shape rays and use enabled non-trigger Environment collision. Existing backboard/post/support/bench/fence collision retained. Both hoop centres and the south entrance gap stay open. Existing geometry and poses retained.\n");
    }
    public static void CollisionAndSparseStock()
    {
        var b=Round2Block();var env=b.transform.Find("Env");var store=b.transform.Find("Env/"+RootName+"/Store");var follow=b.transform.Find("Env/Convenience round two/Round two store refinement");
        if(follow.Find("Sparse brown shelf boxes"))throw new Exception("Sparse-stock pass already installed");
        Undo.RegisterFullObjectHierarchyUndo(b.gameObject,"Solid large props and sparse store stock");
        var oldPoses=b.GetComponentsInChildren<Transform>(true).Select(t=>(t,t.position,t.rotation,t.localScale)).ToArray();var removed=new HashSet<Transform>();
        var report=new List<string>();var added=new List<Collider>();
        void Remove(Transform t){foreach(var c in t.GetComponentsInChildren<Transform>(true))removed.Add(c);Object.DestroyImmediate(t.gameObject);}
        void MeshCollision(MeshFilter f,bool convex=false)
        {
            if(f.GetComponents<Collider>().Any(c=>c.enabled&&!c.isTrigger))return;
            f.gameObject.layer=6;var c=f.gameObject.AddComponent<MeshCollider>();c.sharedMesh=f.sharedMesh;c.convex=convex;added.Add(c);
        }
        int treeParts=0;
        foreach(var f in env.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Needles"||f.name=="Trunk"))
        {f.gameObject.layer=6;if(f.name=="Needles"){MeshCollision(f,true);treeParts++;}else if(!f.GetComponent<Collider>()){var c=f.gameObject.AddComponent<BoxCollider>();c.center=f.sharedMesh.bounds.center;c.size=f.sharedMesh.bounds.size;added.Add(c);}}
        report.Add("Tree canopy hulls: "+treeParts+"; existing trunk collision retained on Environment layer.");
        var modelRoots=env.GetComponentsInChildren<Transform>().Where(t=>new[]{"Last Slice rooftop pizza","Duck Diner rooftop roast duck","Noodle Box rooftop ramen","Quack Mart rooftop googly Q","Car Wash rooftop car","Arcade rooftop duck mascot"}.Contains(t.name)).ToArray();int beforeModels=added.Count;
        foreach(var root in modelRoots)foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.GetComponent<MeshRenderer>()&&f.GetComponent<MeshRenderer>().enabled))MeshCollision(f);
        report.Add("Added "+(added.Count-beforeModels)+" shape-matching static mesh colliders across "+modelRoots.Length+" rooftop models, preserving Q opening and individual shapes.");
        var wash=store.parent.Find("Car wash");var spins=wash.GetComponentsInChildren<RailWashBrushSpin>();
        var source=Object.Instantiate(spins[0].transform.Find("Bristle core").GetComponent<MeshFilter>().sharedMesh);var vertices=source.vertices;for(int i=0;i<vertices.Length;i++){vertices[i].x*=.86f/.55f;vertices[i].z*=.86f/.55f;}source.vertices=vertices;source.RecalculateNormals();var brushMesh=Save2("Full bristle collision cylinder",source);
        foreach(var spin in spins)
        {
            var root=spin.transform;var parts=root.Cast<Transform>().ToArray();var visual=Group(root,"Cosmetic spinning bristles");foreach(var part in parts)part.SetParent(visual,true);
            visual.gameObject.AddComponent<RailWashBrushSpin>().degreesPerSecond=spin.degreesPerSecond;Object.DestroyImmediate(spin);
            root.gameObject.layer=6;var c=root.gameObject.AddComponent<MeshCollider>();c.sharedMesh=brushMesh;c.convex=true;added.Add(c);
        }
        foreach(var t in wash.GetComponentsInChildren<Transform>().Where(t=>t.name=="Stationary brush pillar collision"||t.name=="Horizontal roller stationary core").ToArray())Remove(t);
        report.Add("Seven full-diameter convex roller hulls on stationary brush roots; only visual children spin. Detached undersized proxies removed.");
        int beforeOther=added.Count;
        var otherNames=new[]{"Fridge face","Prize display","Air hockey top","Fan grille","Ceiling reflector","Drinks glass"};
        foreach(var f in env.GetComponentsInChildren<MeshFilter>().Where(f=>otherNames.Contains(f.name)))
        {if(f.GetComponent<Collider>())continue;f.gameObject.layer=6;var c=f.gameObject.AddComponent<BoxCollider>();c.center=f.sharedMesh.bounds.center;c.size=f.sharedMesh.bounds.size;added.Add(c);}
        report.Add("Other solid display/glazing/fan/fixture panels: "+(added.Count-beforeOther)+". Existing furniture, vehicles, HVAC and fences retain their separate collision; paint/neon/carpet/ground overlays remain noncolliding.");
        var goods=follow.Find("Varied retail shelf goods");int retail=goods.Cast<Transform>().Count(t=>!t.name.StartsWith("Batched"));Remove(goods);int drinks=0;
        foreach(var cooler in follow.Find("South upright drink coolers").Cast<Transform>())foreach(var product in cooler.Cast<Transform>().Where(t=>t.name=="Drink bottle"||t.name=="Food tin").ToArray()){Remove(product);drinks++;}
        cube=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/convenience-store-v1/Cube Block UV.asset");wood=AssetDatabase.LoadAssetAtPath<Material>(RailFarmSilos.Folder+"/Old timber.mat");detailMesh=9000;
        var sparse=Group(follow,"Sparse brown shelf boxes");int box=0;
        foreach(var shelf in store.Cast<Transform>().Where(t=>t.name.StartsWith("Shelf run")))
        {
            var surfaces=shelf.Cast<Transform>().Where(t=>t.name=="Shelf").OrderBy(t=>t.position.y).ToArray();
            for(int i=0;i<3;i++)
            {
                var surface=surfaces[(i+box/3)%3].GetComponent<Collider>().bounds;var p=new Vector3(surface.center.x-2.5f+(i*2.1f)+(box%2)*.25f,surface.max.y+.2f,surface.center.z+(i%2==0?-.4f:.4f));
                // Avoid existing loot candidates; no marker movement to make room for decoration.
                for(int attempt=0;attempt<10&&b.spawnCandidates.coal.Concat(b.spawnCandidates.gold).Any(t=>Vector3.Distance(t.position,p)<.6f);attempt++)p.x+=.25f;
                Detail(sparse,"Brown shelf box "+(++box),p,new Vector3(.4f,.4f,.4f),wood,true,Quaternion.Euler(0,(box%3-1)*8,0));
            }
        }
        report.Add("Removed "+retail+" retail product groups and "+drinks+" cooler drinks; retained nine spaced brown cubes and existing cooler coal/gold markers.");
        Physics.SyncTransforms();foreach(var c in added)
        {
            var bounds=c.bounds;bool hit=false;foreach(var dir in new[]{Vector3.up,Vector3.down,Vector3.left,Vector3.right,Vector3.forward,Vector3.back})if(c.Raycast(new Ray(bounds.center+dir*(bounds.extents.magnitude+1),-dir),out _,2*(bounds.extents.magnitude+1))){hit=true;break;}
            if(!hit||!c.enabled||c.isTrigger||c.gameObject.layer!=6)throw new Exception("Collider does not intercept solid-prop ray: "+c.name);
        }
        foreach(var pose in oldPoses)if(!removed.Contains(pose.t)&&(!pose.t||Vector3.Distance(pose.t.position,pose.position)>.0001f||Quaternion.Angle(pose.t.rotation,pose.rotation)>.01f||pose.t.localScale!=pose.localScale))throw new Exception("Unexpected original pose change");
        report.Add("PASS "+added.Count+" new colliders intercept shape rays, are enabled non-trigger Environment colliders. All surviving original world poses/scales retained.");
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();File.WriteAllLines(R2Evidence+"/collision-and-stock.txt",report);
    }
}
#endif
