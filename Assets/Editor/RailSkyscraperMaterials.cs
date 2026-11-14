#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Scoped dressing correction on the reviewed source geometry, not a whole-block rebuild.</summary>
public static class RailSkyscraperMaterials
{
    public const string Evidence = "docs/validation/skyscraper-materials";
    const string Folder = "Assets/DuckRails/SkyscraperWorks/MaterialsRevision";
    static readonly List<string> log = new();
    static Mesh container;
    static int meshIndex;
    static void Require(bool value,string why) { if(!value)throw new InvalidOperationException(why); }
    static Transform Site(GameObject root)=>root.transform.Find("Env/"+RailSkyscraper.RootName);
    static bool IsLayer(Transform t)=>t.name=="Cement bags"||t.name=="Stacked timber";
    static Vector2 XZ(Vector3 p)=>new Vector2(p.x,p.z);
    static Mesh Save(Mesh mesh,string name)
    {mesh.name=(++meshIndex).ToString("D2")+" "+name;AssetDatabase.AddObjectToAsset(mesh,container);return mesh;}
    sealed class Pile
    {
        public Transform pallet, floor;
        public Transform[] layers;
        public List<Transform> loot=new();
        public Transform group;
        public float top;
    }
    sealed class BoxEdit
    {
        public BoxCollider box; public Matrix4x4 before; public Vector3 center,size; public bool remove;
        public BoxEdit(BoxCollider c,bool deleted=false){box=c;before=c.transform.localToWorldMatrix;center=c.center;size=c.size;remove=deleted;}
        public bool Contains(Vector3 world)
        {var p=before.inverse.MultiplyPoint3x4(world)-center;var h=size*.5f+Vector3.one*.0005f;return Mathf.Abs(p.x)<=h.x&&Mathf.Abs(p.y)<=h.y&&Mathf.Abs(p.z)<=h.z;}
        public Matrix4x4 Delta=>box.transform.localToWorldMatrix*Matrix4x4.Translate(box.center)*Matrix4x4.Scale(new Vector3(box.size.x/size.x,box.size.y/size.y,box.size.z/size.z))*Matrix4x4.Translate(-center)*before.inverse;
    }
    static void Guard()
    {
        Require(Application.dataPath.Replace('\\','/')=="X:/Work/duck-rails/Assets"&&!EditorApplication.isPlaying&&!Lightmapping.isRunning,"Use idle Duck Rails editor.");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();Require(stage==null||!stage.scene.isDirty,"Save prefab edits first.");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)Require(!UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty,"Save scene edits first.");
        if(stage!=null)StageUtility.GoToMainStage();
    }
    static bool Ray(Collider[] surfaces,Vector3 p,float distance,out RaycastHit best,Collider exclude=null)
    {
        best=default;bool found=false;float nearest=distance;
        foreach(var c in surfaces)if(c&&c!=exclude&&c.enabled&&!c.isTrigger&&c.gameObject.activeInHierarchy&&c.Raycast(new Ray(p,Vector3.down),out var h,distance)&&h.distance<nearest)
        {best=h;nearest=h.distance;found=true;}
        return found;
    }
    static Vector3[] Bottom(BoxCollider c,float inset=0)
    {
        var h=c.size*.5f;var points=new List<Vector3>();
        foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
            points.Add(c.transform.TransformPoint(c.center+Vector3.Scale(h,new Vector3(x,y,z))));
        float bottom=points.Min(p=>p.y);var center=c.transform.TransformPoint(c.center);
        return points.Where(p=>p.y<bottom+.0001f).Select(p=>new Vector3(Mathf.Lerp(p.x,center.x,inset),p.y,Mathf.Lerp(p.z,center.z,inset))).ToArray();
    }
    static bool Contact(BoxCollider top,BoxCollider below)
    {return Bottom(top,.002f).All(p=>below.Raycast(new Ray(p+Vector3.up*.015f,Vector3.down),out var hit,.03f)&&Mathf.Abs(hit.point.y-p.y)<.004f);}

    [MenuItem("Duck Rails/Authoring/Skyscraper Works/Ground and vary material stacks")]
    public static void Install()
    {
        Guard();Require(!AssetDatabase.IsValidFolder(Folder),"Already installed; preserve subsequent edits.");
        Directory.CreateDirectory(Evidence);Directory.CreateDirectory("output/skyscraper-materials/recovery");
        File.Copy(RailSkyscraper.PrefabPath,"output/skyscraper-materials/recovery/before.prefab",true);
        var root=PrefabUtility.LoadPrefabContents(RailSkyscraper.PrefabPath);
        var transient=new List<Mesh>();GameObject probe=null;
        try
        {
            log.Clear();var site=Site(root);var b=root.GetComponent<RailBlock>();
            Require(!root.transform.Find(RailSkyscraperAtlasProduction.Visual),"Restore source geometry first.");
            var all=root.GetComponentsInChildren<Collider>().Where(c=>c.gameObject.layer==6&&!c.isTrigger).ToArray();
            var markers=RailSpawnPool.Candidates(b,RailSpawnKind.Coal).Concat(RailSpawnPool.Candidates(b,RailSpawnKind.Gold)).ToArray();
            var initialMarkers=markers.ToDictionary(t=>t,t=>t.position);
            var originalStructure=site.GetComponentsInChildren<Transform>().Where(t=>!IsLayer(t)&&t.name!="Supply pallet"&&t.name!="Loose pipe bundle").ToDictionary(t=>t,t=>t.localToWorldMatrix);
            var originalKinds=Enum.GetValues(typeof(RailSpawnKind)).Cast<RailSpawnKind>().ToDictionary(k=>k,k=>RailSpawnPool.Candidates(b,k).Length);
            var floors=site.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Level ")).ToArray();
            var piles=new List<Pile>();
            foreach(var floor in floors)
                foreach(var pallet in floor.Cast<Transform>().Where(t=>t.name=="Supply pallet"))
                {
                    var p=new Pile{floor=floor,pallet=pallet,layers=floor.Cast<Transform>().Where(t=>IsLayer(t)&&Vector2.Distance(XZ(t.position),XZ(pallet.position))<.1f).OrderBy(t=>t.position.y).ToArray()};
                    Require(p.layers.Length==3,"Expected original three-layer pallet: "+pallet.position);piles.Add(p);
                }
            Require(piles.Count==50,"Expected 50 retained tower pallets.");Physics.SyncTransforms();
            var support=new Dictionary<Transform,Collider>();
            foreach(var t in markers)
            {
                var bounds=RailLootPlacementWindow.ItemBounds(t.GetComponent<RailGoldSpawner>()!=null);bounds.center+=t.position;
                if(Ray(all,new Vector3(bounds.center.x,bounds.min.y+.15f,bounds.center.z),.6f,out var hit))support[t]=hit.collider;
            }
            foreach(var p in piles)
            {
                var cs=p.layers.Select(t=>t.GetComponent<Collider>()).Append(p.pallet.GetComponent<Collider>()).ToArray();
                p.loot=markers.Where(t=>support.TryGetValue(t,out var c)&&cs.Contains(c)).ToList();
                foreach(var c in cs)c.enabled=false;
            }
            var attached=new HashSet<Transform>(piles.SelectMany(p=>p.loot));
            var obstacles=markers.Where(t=>!attached.Contains(t)).Select(t=>{var bb=RailLootPlacementWindow.ItemBounds(t.GetComponent<RailGoldSpawner>()!=null);bb.center+=t.position;bb.Expand(.12f);return bb;}).ToList();
            obstacles.AddRange(RailSpawnPool.Candidates(b,RailSpawnKind.Enemy).Select(t=>new Bounds(t.position+Vector3.up*.5f,new Vector3(1.2f,1,1.2f))));
            probe=new GameObject("Temporary placement volume");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(probe,root.scene);var test=probe.AddComponent<BoxCollider>();test.isTrigger=true;
            int ordinal=0,totalLayers=0;
            foreach(var floor in floors)
            {
                int level=int.Parse(floor.name.Split(' ')[1]);int tower=floor.parent.name.Last()-'A';
                float y=floor.GetComponentsInChildren<BoxCollider>().First(c=>c.name=="Ground slab"||c.name=="Open floor slab").bounds.max.y;
                var rng=new System.Random(11397+tower*937+level*193);
                var options=new List<Vector2>();
                // Different shelves/corners along each floor; all real support and collision are checked below.
                foreach(var spot in new[]{new Vector2(3,12.5f),new Vector2(3.5f,17),new Vector2(3.5f,21.5f),new Vector2(10,3.5f),new Vector2(14,3),new Vector2(21.5f,3.5f),new Vector2(22,7.5f),new Vector2(10,7),new Vector2(15.5f,6),new Vector2(17,8.5f)})
                    for(int k=0;k<8;k++)options.Add(spot+new Vector2((float)rng.NextDouble()-.5f,(float)rng.NextDouble()-.5f)*1.6f);
                options=options.OrderBy(v=>rng.Next()).ToList();
                var used=new List<Vector2>();
                foreach(var p in piles.Where(p=>p.floor==floor))
                {
                    int count=1+(ordinal*7+tower+level)%4;float width=1.8f+(ordinal%3)*.15f,depth=1.2f+(ordinal%2)*.12f;
                    float height=.24f+count*.16f;float yaw=(ordinal%3==0?90:0)+rng.Next(-24,25);
                    Quaternion rotation=Quaternion.Euler(0,yaw,0);Vector3 location=default;bool found=false;
                    foreach(var q in options)
                    {
                        if(used.Any(v=>Vector2.Distance(v,q)<3.6f))continue;
                        var point=floor.parent.TransformPoint(new Vector3(q.x,y,q.y));
                        var center=point+Vector3.up*(height*.5f+.02f);test.size=new Vector3(width+.3f,height+.02f,depth+.3f);
                        var right=rotation*Vector3.right;var forward=rotation*Vector3.forward;
                        var aabb=new Bounds(center,new Vector3(Mathf.Abs(right.x)*test.size.x+Mathf.Abs(forward.x)*test.size.z,test.size.y,Mathf.Abs(right.z)*test.size.x+Mathf.Abs(forward.z)*test.size.z));
                        if(obstacles.Any(o=>o.Intersects(aabb)))continue;
                        bool covered=true;
                        foreach(float x in new[]{-width*.5f,width*.5f})foreach(float z in new[]{-depth*.5f,depth*.5f})
                        {
                            var foot=point+rotation*new Vector3(x,0,z);
                            if(!Ray(all,foot+Vector3.up*.08f,.18f,out var hit)||Mathf.Abs(hit.point.y-point.y)>.015f)covered=false;
                        }
                        if(!covered)continue;
                        if(all.Any(c=>c&&c.enabled&&c.bounds.Intersects(aabb)&&Physics.ComputePenetration(test,center,rotation,c,c.transform.position,c.transform.rotation,out _,out var distance)&&distance>.005f))continue;
                        found=true;location=point;used.Add(q);break;
                    }
                    Require(found,"No safe stack location on "+floor.parent.name+"/"+floor.name+" pile "+ordinal);
                    var group=new GameObject("Material stack "+(++ordinal).ToString("D2")).transform;group.SetParent(floor,false);group.position=location;group.rotation=rotation;p.group=group;
                    p.pallet.SetParent(group,true);p.pallet.localPosition=Vector3.up*.12f;p.pallet.localRotation=Quaternion.identity;p.pallet.localScale=new Vector3(width,.24f,depth);p.pallet.GetComponent<Collider>().enabled=true;
                    var retained=p.layers.ToList();while(retained.Count<count)retained.Add(Object.Instantiate(p.layers[0].gameObject,floor).transform);
                    foreach(var t in retained.Skip(count).ToArray())Object.DestroyImmediate(t.gameObject);
                    retained=retained.Take(count).ToList();float layerYaw=rng.Next(-5,6);
                    for(int j=0;j<count;j++)
                    {
                        var t=retained[j];t.name=t.name.Replace("(Clone)","").Trim();t.SetParent(group,true);t.localPosition=new Vector3(0,.24f+(j+.5f)*.16f,0);t.localRotation=Quaternion.Euler(0,layerYaw+j*.7f,0);
                        float taper=Mathf.Pow(.95f,j);t.localScale=new Vector3((width-.38f)*taper,.16f,(depth-.38f)*taper);t.GetComponent<Collider>().enabled=true;
                        Physics.SyncTransforms();
                        Require(Contact(t.GetComponent<BoxCollider>(),j==0?p.pallet.GetComponent<BoxCollider>():retained[j-1].GetComponent<BoxCollider>()),"Unsupported rotated layer "+group.name+"/"+j);
                    }
                    p.layers=retained.ToArray();p.top=y+height;totalLayers+=count;
                    foreach(var t in p.loot){var bb=RailLootPlacementWindow.ItemBounds(t.GetComponent<RailGoldSpawner>()!=null);t.position=location+Vector3.up*(height+.05f-bb.min.y);PrefabUtility.RecordPrefabInstancePropertyModifications(t);}
                    obstacles.Add(new Bounds(location+Vector3.up*height*.5f,new Vector3(3,height,3)));Physics.SyncTransforms();
                    log.Add($"{floor.parent.name} / {floor.name}: stack {ordinal:00}, {count} layers, local XZ={floor.parent.InverseTransformPoint(location)}, yaw={yaw:F1}");
                }
            }
            // These square-section pipes also had a 6 cm gap below them.
            int groundedPipes=0;
            foreach(var t in site.GetComponentsInChildren<Transform>().Where(t=>t.name=="Loose pipe bundle"))
            {var c=t.GetComponent<BoxCollider>();float floor=t.parent.GetComponentsInChildren<BoxCollider>().First(s=>s.name=="Ground slab"||s.name=="Open floor slab").bounds.max.y;t.position+=Vector3.up*(floor-c.bounds.min.y);groundedPipes++;}

            var yard=root.transform.Find("Env/Approved yard polish");var edits=new List<BoxEdit>();
            void EditLayers(string groupName,string name,float bottom,float yaw,int keep)
            {
                var pieces=yard.Find(groupName).GetComponentsInChildren<BoxCollider>().Where(c=>c.name==name).OrderBy(c=>c.bounds.center.y).ToArray();
                for(int i=0;i<pieces.Length;i++)
                {
                    var c=pieces[i];edits.Add(new BoxEdit(c,i>=keep));if(i>=keep)continue;
                    var at=c.transform.position;at.y=bottom+c.size.y*(i+.5f);c.transform.position=at;c.transform.rotation=Quaternion.Euler(0,yaw,0);
                }
            }
            EditLayers("Delivery flatbed","Delivery timber",1.5f,1.5f,3);
            EditLayers("Yard surface details","Stacked formwork",0,-7,4);
            int shelterIndex=0;
            foreach(string name in new[]{"South loading shelter","North maintenance shed"})
                foreach(var c in yard.Find(name).GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Timber stack"))
                {
                    edits.Add(new BoxEdit(c));float height=.65f+(shelterIndex%4)*.25f;var size=c.size;size.y=height;c.size=size;
                    var p=c.transform.position;p.y=height*.5f;p.z+=(shelterIndex%3-1)*.3f;c.transform.position=p;c.transform.rotation=Quaternion.Euler(0,(shelterIndex++%2==0?-1:1)*8,0);
                }
            var removed=yard.Find("Pipe storage and puddles").GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Hollow concrete pipe segment"&&c.transform.position.x>30&&c.transform.position.z>120).ToArray();
            Require(removed.Length==24,"Expected the northeast pair of concrete pipe sections.");foreach(var c in removed)edits.Add(new BoxEdit(c,true));
            // Combined yard visuals and separate collision must receive exactly the same edit.
            var modifiedMeshes=new List<(MeshFilter filter,Mesh mesh)>();
            foreach(var f in yard.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=EditCombined(f,edits);if(mesh){transient.Add(mesh);modifiedMeshes.Add((f,mesh));}
            }
            foreach(var edit in edits.Where(e=>e.remove))edit.box.enabled=false;
            Physics.SyncTransforms();
            all=root.GetComponentsInChildren<Collider>().Where(c=>c.gameObject.layer==6&&!c.isTrigger&&c.enabled).ToArray();
            int yardLoot=0;
            foreach(var t in markers.Where(t=>!attached.Contains(t)&&support.ContainsKey(t)))
            {
                var change=edits.FirstOrDefault(e=>e.box==support[t]);if(change==null)continue;
                var bounds=RailLootPlacementWindow.ItemBounds(t.GetComponent<RailGoldSpawner>()!=null);
                var at=change.remove?t.position:change.Delta.MultiplyPoint3x4(t.position);
                Require(Ray(all,new Vector3(at.x,at.y+2,at.z),5,out var hit),"Lost yard loot support "+t.name);
                at.y=hit.point.y+.05f-bounds.min.y;t.position=at;PrefabUtility.RecordPrefabInstancePropertyModifications(t);yardLoot++;
            }
            foreach(var edit in edits.Where(e=>e.remove))Object.DestroyImmediate(edit.box.gameObject);
            Require(originalStructure.All(k=>k.Key&&k.Key.localToWorldMatrix==k.Value),"Tower structure/crossings changed.");
            Require(originalKinds.All(k=>RailSpawnPool.Candidates(b,k.Key).Length==k.Value),"Candidate counts changed.");
            AssetDatabase.CreateFolder("Assets/DuckRails/SkyscraperWorks","MaterialsRevision");container=new Mesh{name="Material dressing revision meshes"};AssetDatabase.CreateAsset(container,Folder+"/Meshes.asset");meshIndex=0;
            foreach(var m in modifiedMeshes)m.filter.sharedMesh=Save(m.mesh,m.filter.name);
            transient.Clear();Object.DestroyImmediate(probe);probe=null;
            var lighting=root.GetComponent<RailBakedBlockLighting>();lighting.bindings=lighting.bindings.Where(x=>x.renderer).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,RailSkyscraper.PrefabPath);AssetDatabase.SaveAssets();
            log.Insert(0,$"50 grounded pallets; 150 -> {totalLayers} material layers; {groundedPipes} loose pipe pieces grounded; 17 distinct floor plans.");
            log.Add($"Southeast truck/formwork layers 4/5 -> 3/4, grounded. Eight shelter stocks varied. Northeast pipe: {removed.Length} collider cubes and corresponding triangles removed; other pipe groups preserved.");
            log.Add($"Loot moved with supporting stacks: {markers.Count(t=>t.position!=initialMarkers[t])} total ({yardLoot} yard). Pools unchanged: 202 coal / 192 gold / 23 enemies.");
            File.WriteAllLines(Evidence+"/install.txt",log);Debug.Log(string.Join("\n",log.Take(1).Concat(log.TakeLast(2))));
        }
        finally {if(probe)Object.DestroyImmediate(probe);foreach(var m in transient)Object.DestroyImmediate(m);PrefabUtility.UnloadPrefabContents(root);}
    }

    static Mesh EditCombined(MeshFilter filter,List<BoxEdit> edits)
    {
        var source=filter.sharedMesh;var vertices=source.vertices;var normals=source.normals;var uv=source.uv;var uv2=source.uv2;var tris=source.triangles;
        var vv=new List<Vector3>();var nn=new List<Vector3>();var uu=new List<Vector2>();var ll=new List<Vector2>();var tt=new List<int>();var lookup=new Dictionary<(int,int),int>();bool changed=false;
        var world=vertices.Select(v=>filter.transform.TransformPoint(v)).ToArray();
        for(int i=0;i<tris.Length;i+=3)
        {
            int match=edits.FindIndex(e=>e.Contains(world[tris[i]])&&e.Contains(world[tris[i+1]])&&e.Contains(world[tris[i+2]]));
            if(match>=0){changed=true;if(edits[match].remove)continue;}
            var delta=match>=0?filter.transform.worldToLocalMatrix*edits[match].Delta*filter.transform.localToWorldMatrix:Matrix4x4.identity;
            for(int k=0;k<3;k++)
            {
                int old=tris[i+k];var key=(old,match);
                if(!lookup.TryGetValue(key,out int index))
                {index=vv.Count;lookup[key]=index;vv.Add(delta.MultiplyPoint3x4(vertices[old]));nn.Add(delta.inverse.transpose.MultiplyVector(normals[old]).normalized);uu.Add(uv[old]);ll.Add(uv2[old]);}
                tt.Add(index);
            }
        }
        if(!changed)return null;
        var mesh=new Mesh{indexFormat=source.indexFormat};mesh.SetVertices(vv);mesh.SetNormals(nn);mesh.SetUVs(0,uu);mesh.SetUVs(1,ll);mesh.SetTriangles(tt,0);mesh.RecalculateBounds();return mesh;
    }

    public static void CheckDressing()
    {
        Guard();EditorSceneManager.OpenScene(RailSkyscraper.BakeScene);Physics.SyncTransforms();
        var root=Object.FindFirstObjectByType<RailBlock>().gameObject;var site=Site(root);
        var checks=new List<string>();void Check(bool ok,string name)=>checks.Add((ok?"PASS ":"FAIL ")+name);
        var surfaces=root.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();
        var stacks=site.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Material stack ")).ToArray();
        Check(stacks.Length==50,"50 retained tower pallets");var signatures=new List<string>();var counts=new HashSet<int>();
        foreach(var floor in site.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Level ")))
        {
            var groups=floor.Cast<Transform>().Where(t=>t.name.StartsWith("Material stack ")).ToArray();
            signatures.Add(string.Join(";",groups.Select(g=>g.localPosition.ToString("F1")+g.localRotation.eulerAngles.y.ToString("F1"))));
        }
        Check(signatures.Count==17&&signatures.Distinct().Count()==17,"All 17 floor dressing layouts differ");
        foreach(var stack in stacks)
        {
            var pallet=stack.Find("Supply pallet").GetComponent<BoxCollider>();
            foreach(var point in Bottom(pallet,.002f))Check(Ray(surfaces,point+Vector3.up*.015f,.035f,out var hit,pallet)&&Mathf.Abs(hit.point.y-point.y)<.004f,"Pallet floor contact "+stack.name);
            var layers=stack.Cast<Transform>().Where(IsLayer).OrderBy(t=>t.position.y).Select(t=>t.GetComponent<BoxCollider>()).ToArray();counts.Add(layers.Length);
            for(int i=0;i<layers.Length;i++)Check(Contact(layers[i],i==0?pallet:layers[i-1]),"All four layer corners supported "+stack.name+"/"+i);
        }
        Check(counts.SetEquals(new[]{1,2,3,4}),"One/two/three/four layer variants present");
        foreach(var pipe in site.GetComponentsInChildren<Transform>().Where(t=>t.name=="Loose pipe bundle"))
        {
            var c=pipe.GetComponent<BoxCollider>();foreach(var p in Bottom(c,.002f))Check(Ray(surfaces,p+Vector3.up*.015f,.035f,out var hit,c)&&Mathf.Abs(hit.point.y-p.y)<.004f,"Loose pipe floor contact "+pipe.position);
        }
        var yard=root.transform.Find("Env/Approved yard polish");
        foreach(var groupName in new[]{"Delivery flatbed","Yard surface details","South loading shelter","North maintenance shed"})
        {
            var group=yard.Find(groupName);var vertices=group.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).ToArray();
            var boxes=group.GetComponentsInChildren<BoxCollider>().Where(c=>c.name=="Delivery timber"||c.name=="Stacked formwork"||c.name=="Timber stack").ToArray();
            foreach(var c in boxes)
            {
                foreach(var p in Bottom(c,.002f))Check(Ray(surfaces,p+Vector3.up*.015f,.035f,out var hit,c)&&Mathf.Abs(hit.point.y-p.y)<.004f,"Yard material layer contact "+groupName+" "+c.transform.position);
                foreach(int x in new[]{-1,1})foreach(int y in new[]{-1,1})foreach(int z in new[]{-1,1})
                {var corner=c.transform.TransformPoint(c.center+Vector3.Scale(c.size*.5f,new Vector3(x,y,z)));Check(vertices.Any(v=>(v-corner).sqrMagnitude<.000004f),"Yard visual/collider corner match "+groupName);}
            }
        }
        var pipeGroup=yard.Find("Pipe storage and puddles");
        Check(pipeGroup.GetComponentsInChildren<BoxCollider>().Count(c=>c.name=="Hollow concrete pipe segment")==48,"Other two pipe locations preserved; northeast pair deleted");
        Check(!pipeGroup.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).Any(v=>v.x>30&&v.z>120),"No residual northeast pipe triangles");
        var failures=checks.Where(s=>s.StartsWith("FAIL ")).ToArray();File.WriteAllLines(Evidence+"/dressing-checks.txt",checks);
        File.WriteAllText(Evidence+"/dressing-summary.txt",$"{checks.Count-failures.Length}/{checks.Count} dressing checks pass.\n"+string.Join("\n",failures));
        Require(failures.Length==0,string.Join("\n",failures.Take(15)));Debug.Log($"{checks.Count} dressing checks passed.");
    }
}
#endif
