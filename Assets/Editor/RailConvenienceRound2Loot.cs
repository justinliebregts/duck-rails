#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static partial class RailConvenienceStore
{
    static RailBlock Round2Block()
    {var s=PrefabStageUtility.GetCurrentPrefabStage();if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||s==null||s.assetPath!=PrefabPath||EditorApplication.isPlaying)throw new Exception("Open Convenience Store in Duck Rails Edit Mode");return s.prefabContentsRoot.GetComponent<RailBlock>();}
    public static void CapRound2Trees()
    {
        var b=Round2Block();var filters=b.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Needles"&&f.sharedMesh&&!f.sharedMesh.name.StartsWith("Capped")).ToArray();var meshes=new Dictionary<Mesh,Mesh>();
        AssetDatabase.StartAssetEditing();try
        {
            foreach(var f in filters)
            {
                if(!meshes.TryGetValue(f.sharedMesh,out var result))
                {
                    var src=f.sharedMesh;var v=src.vertices.ToList();var n=src.normals.ToList();var uv=src.uv.ToList();float low=v.Min(p=>p.y);var ring=v.Where(p=>Mathf.Abs(p.y-low)<.001f).Distinct().ToArray();var centre=ring.Aggregate(Vector3.zero,(a,c)=>a+c)/ring.Length;ring=ring.OrderBy(p=>Mathf.Atan2(p.z-centre.z,p.x-centre.x)).ToArray();
                    if(ring.Length<3)throw new Exception("No canopy base ring "+src.name);var triangles=src.triangles.ToList();int start=v.Count;v.Add(centre);n.Add(Vector3.down);uv.Add(new Vector2(centre.x,centre.z)/3);
                    foreach(var p in ring){v.Add(p);n.Add(Vector3.down);uv.Add(new Vector2(p.x,p.z)/3);}for(int i=0;i<ring.Length;i++)triangles.AddRange(new[]{start,start+1+i,start+1+(i+1)%ring.Length});
                    var mesh=new Mesh();mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);Unwrapping.GenerateSecondaryUVSet(mesh);result=Save2("Capped "+src.name,mesh);meshes.Add(src,result);
                }
                f.sharedMesh=result;
            }
        }finally{AssetDatabase.StopAssetEditing();}
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(R2Evidence+"/tree-caps.txt",$"PASS {filters.Length} canopy renderers receive underside geometry through {meshes.Count} block-local mesh copies. Original shared meshes and all tree transforms unchanged.\n");Debug.Log("Capped "+filters.Length+" canopy tiers");
    }
    [Serializable] public class LootMove2{public int kind,index;public Vector3 before,after;public string label;}
    [Serializable] public class LootPlan2{public List<LootMove2> moves=new();}
    static Collider[] LootColliders2(RailBlock b)=>b.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();
    static bool LootValid2(Vector3 p,Bounds shape,Collider[] colliders,PhysicsScene physics)
    {
        foreach(float x in new[]{-shape.extents.x,shape.extents.x})foreach(float z in new[]{-shape.extents.z,shape.extents.z})
        {var bottom=p+new Vector3(shape.center.x+x,shape.min.y,shape.center.z+z);if(!colliders.Any(c=>c.Raycast(new Ray(bottom+Vector3.up*.12f,Vector3.down),out _,.3f)))return false;}
        return physics.OverlapBox(p+shape.center,shape.extents*.97f,new Collider[64],Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0;
    }
    public static void PlanRound2Loot()
    {
        var b=Round2Block();Physics.SyncTransforms();var cols=LootColliders2(b);var physics=b.gameObject.scene.GetPhysicsScene();var plan=new LootPlan2();
        var staged=new List<(Transform t,Vector3 p)>();var used=new HashSet<Transform>();
        void Plan(Transform t,RailSpawnKind kind,Vector3 wanted,string label,Bounds? region=null)
        {
            var shape=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);var pool=RailSpawnPool.Points(b.spawnCandidates,kind);bool found=false;Vector3 chosen=Vector3.zero;
            for(float radius=0;radius<=2&&!found;radius+=.25f)for(int angle=0;angle<(radius==0?1:16)&&!found;angle++)
            {
                float rad=angle*Mathf.PI/8;var ray=new Ray(wanted+new Vector3(Mathf.Cos(rad)*radius,.85f,Mathf.Sin(rad)*radius),Vector3.down);
                var supports=new List<RaycastHit>();foreach(var col in cols)if(col.Raycast(ray,out var hit,2.3f)&&hit.normal.y>.7f)supports.Add(hit);
                foreach(var hit in supports.OrderBy(h=>Mathf.Abs(h.point.y-wanted.y)))
                {
                    var p=hit.point+Vector3.up*(.05f-shape.min.y);if(region.HasValue&&!region.Value.Contains(p))continue;if(!LootValid2(p,shape,cols,physics))continue;
                    if(staged.Any(q=>Vector3.Distance(p,q.p)<.8f))continue;
                    if(pool.Any(other=>other!=t&&!used.Contains(other)&&!plan.moves.Any(m=>m.kind==(int)kind&&pool[m.index]==other)&&Vector3.Distance(p,other.position)<.65f))continue;
                    chosen=p;found=true;break;
                }
            }
            if(!found)throw new Exception("No supported clear location for "+label+" near "+wanted);
            used.Add(t);staged.Add((t,chosen));plan.moves.Add(new LootMove2{kind=(int)kind,index=Array.IndexOf(pool,t),before=t.position,after=chosen,label=label});
        }
        // These markers lived in world-space groups and do not follow the user's moved semi automatically.
        var semi=b.transform.Find("Env/"+PolishName+"/Broken articulated semi/Enterable open trailer");var trailer=b.spawnCandidates.gold.Where(t=>t.name.StartsWith("Gold polish trailer cargo")).ToArray();
        for(int i=0;i<trailer.Length;i++)Plan(trailer[i],RailSpawnKind.Gold,semi.TransformPoint(new Vector3(i%2==0?-.75f:.75f,1.14f,-2+i/2*1.8f)),"Moved trailer cargo "+i);
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var pool=RailSpawnPool.Points(b.spawnCandidates,kind);var shape=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            // Redistribute from invalid placements first, then dense old utility/wash clusters; retain original identities/counts.
            var donors=pool.Where(t=>!used.Contains(t)&&!t.name.Contains("freezer")&&!t.name.Contains("trailer")).OrderBy(t=>LootValid2(t.position,shape,cols,physics)?1:0).ThenBy(t=>t.position.x<0&&t.position.z>118?0:t.position.x>27&&t.position.z<53?1:2).ToList();int donor=0;
            foreach(string name in new[]{"LAST SLICE","DUCK DINER","NOODLE BOX"})
            {
                var room=b.transform.Find("Env/"+RootName+"/Restaurant plaza/"+name);float x=room.Find("Roof").localPosition.x;var local=kind==RailSpawnKind.Coal?
                    new[]{new Vector3(x-3,.85f,104.5f),new Vector3(x+3,.95f,108),new Vector3(x-1,0,112.3f),new Vector3(x+1,0,105.7f)}:
                    new[]{new Vector3(x-3,.85f,107.5f),new Vector3(x-3.4f,.9f,112.25f),new Vector3(x+3,1,112.3f),new Vector3(x+.2f,0,108.7f)};
                var region=new Bounds(room.TransformPoint(new Vector3(x,1.3f,108)),new Vector3(9,2.6f,11.2f));
                for(int i=0;i<local.Length;i++){while(used.Contains(donors[donor]))donor++;Plan(donors[donor++],kind,room.TransformPoint(local[i]),name+" "+(kind==RailSpawnKind.Coal?"coal":"gold")+" interior "+i,region);}
            }
            var arcade=b.transform.Find("Env/"+RootName+"/Arcade");var rear=arcade.Find("Round two party interior/Rear room arcade cabinets");
            var points=kind==RailSpawnKind.Coal?new[]{new Vector3(-35,0,73.3f),new Vector3(-35,0,84.5f)}:new[]{new Vector3(-33.85f,2.09f,77.5f),new Vector3(-33.85f,2.09f,81.2f)};
            for(int i=0;i<points.Length;i++){while(used.Contains(donors[donor]))donor++;Plan(donors[donor++],kind,arcade.TransformPoint(points[i]),"Rear arcade "+kind+" "+i,new Bounds(new Vector3(-35,1.5f,79),new Vector3(3.4f,3,13)));}
        }
        // After intentional redistribution, locally repair any other marker whose support was changed/deleted by either edit pass.
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})foreach(var t in RailSpawnPool.Points(b.spawnCandidates,kind))
        {if(used.Contains(t))continue;var shape=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);if(!LootValid2(t.position,shape,cols,physics))Plan(t,kind,t.position+Vector3.up*shape.min.y,"Local support repair "+t.name);}
        File.WriteAllText(R2Evidence+"/loot-plan.json",JsonUtility.ToJson(plan,true));File.WriteAllLines(R2Evidence+"/loot-plan.txt",plan.moves.Select(m=>$"{(RailSpawnKind)m.kind} [{m.index}] {m.label}: {m.before:F2} -> {m.after:F2}"));Debug.Log("Read-only loot plan validated: "+plan.moves.Count+" relocations; no markers changed yet.");
    }
    public static void ApplyRound2Loot()
    {
        var b=Round2Block();var plan=JsonUtility.FromJson<LootPlan2>(File.ReadAllText(R2Evidence+"/loot-plan.json"));var cols=LootColliders2(b);var ps=b.gameObject.scene.GetPhysicsScene();Physics.SyncTransforms();
        foreach(var m in plan.moves){var t=RailSpawnPool.Points(b.spawnCandidates,(RailSpawnKind)m.kind)[m.index];if((t.position-m.before).sqrMagnitude>.00001f)throw new Exception("Marker changed since planning");if(!LootValid2(m.after,RailLootPlacementWindow.ItemBounds(m.kind==(int)RailSpawnKind.Gold),cols,ps))throw new Exception("Placement no longer valid");}
        foreach(var m in plan.moves){var t=RailSpawnPool.Points(b.spawnCandidates,(RailSpawnKind)m.kind)[m.index];Undo.RecordObject(t,"Redistribute convenience loot");t.position=m.after;PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();Debug.Log("Applied "+plan.moves.Count+" validated loot relocations; counts unchanged.");
    }
}
#endif
