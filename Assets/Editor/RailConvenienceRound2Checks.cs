#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Game.AI;
using Object=UnityEngine.Object;

public static partial class RailConvenienceStore
{
    public static void CompactRound2()
    {
        var b=Round2Block();var roots=b.GetComponentsInChildren<Transform>().Where(t=>t.name=="Convenience round two"||t.name.StartsWith("Round two ")||t.name=="Open south sideline entrance"||t.name=="Basic abandoned delivery van").ToArray();
        int combined=0;AssetDatabase.StartAssetEditing();try
        {
            foreach(var p in roots.SelectMany(t=>t.GetComponentsInChildren<Transform>()).Distinct().ToArray())
            {
                if(p.GetComponentInParent<RailWashBrushSpin>())continue;var candidates=p.name=="Varied retail shelf goods"||p.name.StartsWith("Stockroom rack ")?p.GetComponentsInChildren<Transform>().Where(t=>t!=p):p.Cast<Transform>();var renderers=candidates.Where(t=>t.childCount==0&&t.GetComponent<MeshFilter>()&&t.GetComponent<MeshRenderer>()&&!t.GetComponent<TMPro.TMP_Text>()).Select(t=>t.GetComponent<MeshRenderer>()).Where(r=>r.enabled&&r.sharedMaterials.Length==1&&GameObjectUtility.AreStaticEditorFlagsSet(r.gameObject,StaticEditorFlags.ContributeGI)).ToArray();
                foreach(var set in renderers.GroupBy(r=>r.sharedMaterial).Where(g=>g.Count()>=4))
                {
                    var mesh=new Mesh{indexFormat=UnityEngine.Rendering.IndexFormat.UInt32};mesh.CombineMeshes(set.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=p.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray(),true,true);Unwrapping.GenerateSecondaryUVSet(mesh);
                    var t=Group(p,"Batched round-two details "+set.Key.name);t.gameObject.AddComponent<MeshFilter>().sharedMesh=Save2("Batched details "+combined++,mesh);var rr=t.gameObject.AddComponent<MeshRenderer>();rr.sharedMaterial=set.Key;rr.receiveGI=ReceiveGI.Lightmaps;rr.scaleInLightmap=.3f;GameObjectUtility.SetStaticEditorFlags(t.gameObject,StaticEditorFlags.ContributeGI);
                    foreach(var r in set){Object.DestroyImmediate(r.GetComponent<MeshFilter>());Object.DestroyImmediate(r);}
                }
            }
        }finally{AssetDatabase.StopAssetEditing();}
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,PrefabPath);AssetDatabase.SaveAssets();File.WriteAllText(R2Evidence+"/batching.txt","Consolidated "+combined+" new static material groups; retained collider objects and cosmetic brush motion.\n");
    }
    public static void CheckRound2(bool graphReady=true)
    {
        var b=Round2Block();Physics.SyncTransforms();var ps=b.gameObject.scene.GetPhysicsScene();var cols=LootColliders2(b);var log=new List<string>();var hits=new Collider[64];
        void Check(bool ok,string label)=>log.Add((ok?"PASS ":"FAIL ")+label);
        bool Clear(Vector3 p)=>ps.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.7f,.28f,hits,1<<6,QueryTriggerInteraction.Ignore)==0;
        Check(b.id=="convenience-store-v1"&&b.length==160&&b.track.Length==81&&b.track.Select((p,i)=>p==new Vector3(0,0,i*2)).All(x=>x),"Stable identity, length and original straight railway");
        Check(b.stop.localPosition==new Vector3(0,0,80),"Stop preserved");Check(b.spawnCandidates.coal.Length==126&&b.spawnCandidates.gold.Length==109&&b.spawnCandidates.enemies.Length==27,"Candidate counts preserved 126 / 109 / 27");
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var shape=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);var points=RailSpawnPool.Points(b.spawnCandidates,kind);
            foreach(var t in points){Check(LootValid2(t.position,shape,cols,ps),kind+" footprint/clearance "+t.position.ToString("F2"));Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,"Native marker "+t.name);}
            for(int seed=0;seed<100;seed++){int target=kind==RailSpawnKind.Coal?31:52;var selected=RailSpawnPool.Select(b,kind,seed,target);Check(selected.Length==target&&selected.Distinct().Count()==target,"Eight-player loot capacity "+kind+" seed "+seed);}
        }
        var a=b.transform.Find("Env/"+RootName);var extra=b.transform.Find("Env/Convenience round two");var previous=b.transform.Find("Env/"+PolishName);
        var semi=previous.Find("Broken articulated semi");Check(Vector3.Distance(semi.position,new Vector3(-19.95f,0,108.37f))<.015f&&Mathf.Abs(Mathf.DeltaAngle(semi.eulerAngles.y,250.7873f))<.001f,"User-moved semi preserved");
        foreach(string name in new[]{"LAST SLICE","DUCK DINER","NOODLE BOX"})
        {
            var room=a.Find("Restaurant plaza/"+name);float x=room.Find("Roof").localPosition.x;
            Check(!Clear(room.TransformPoint(new Vector3(x-.5f,0,114))),name+" ornamental rear door blocks exit");Check(Clear(room.TransformPoint(new Vector3(x-.5f,0,102))),name+" front doorway remains open");
            foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})Check(RailSpawnPool.Points(b.spawnCandidates,kind).Count(t=>{var p=room.InverseTransformPoint(t.position);return p.x>x-5&&p.x<x+5&&p.z>102&&p.z<114&&p.y<3;})>=4,name+" contains interior "+kind+" candidates");
            Check(room.Find("Round two storefront and closed rear door").Cast<Transform>().Count(t=>t.name=="Former glazing mullion")==6,name+" two empty framed windows");
        }
        Check(Clear(new Vector3(-20,0,120)),"South-side court gap open");Check(!Clear(new Vector3(-15,0,129)),"Old court opening closed");Check(previous.Find("Northwest basketball court/Open court gate")==null,"No swinging court gate");
        Check(a.Find("Outer thirds/Baked utility lamp").position.z<120,"Court light moved off playing surface");Check(Vector3.Distance(a.Find("Rest stop/Grove lantern").position,new Vector3(-21,4.15f,36))<.01f,"Grove lamp near benches");
        Check(extra.Find("Court perimeter grove").childCount==9,"Nine large varied court trees");
        foreach(var f in b.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Needles")){var m=f.sharedMesh;Check(m.name.StartsWith("Capped")&&m.normals.Count(n=>n.y<-.99f)>=4,"Tree canopy underside "+m.name);}
        Check(a.GetComponentsInChildren<RailWashBrushSpin>().Length==7,"Six vertical and one horizontal scrub roller");var wash=a.Find("Car wash");Check(Mathf.Abs(Vector3.Dot(wash.Find("Horizontal rear scrub roller").up,Vector3.up))<.01f,"Rear roller axis horizontal");
        // A sampled walk corridor must connect the two wash doors around staggered solid pillars.
        const int nx=23,nz=33;var open=new bool[nx,nz];for(int x=0;x<nx;x++)for(int z=0;z<nz;z++)open[x,z]=Clear(wash.TransformPoint(new Vector3((x-11)*.5f,0,(z-16)*.5f)));
        var seen=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();var start=new Vector2Int(11,32);if(open[start.x,start.y]){seen.Add(start);queue.Enqueue(start);}while(queue.Count>0){var at=queue.Dequeue();foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right}){var n=at+d;if(n.x<0||n.x>=nx||n.y<0||n.y>=nz||!open[n.x,n.y]||!seen.Add(n))continue;queue.Enqueue(n);}}
        Check(seen.Contains(new Vector2Int(11,0)),"Wash has traversable corridor around staggered pillars");
        var rear=a.Find("Arcade/Round two party interior/Rear room arcade cabinets");Check(rear.childCount==8,"Eight extra rear-room arcade machines");for(float z=71.5f;z<85;z+=.5f)Check(Clear(new Vector3(-35,0,z)),"Rear arcade aisle "+z);
        var gates=extra.Find("Lowered railway crossing barriers");Check(gates.childCount==2,"Two lowered road barriers");foreach(var c in gates.GetComponentsInChildren<Collider>())Check(c.bounds.max.x< -4||c.bounds.min.x>4,"Crossing stays outside railway "+c.name);
        Check(gates.GetComponentsInChildren<Transform>().Count(t=>t.name=="Red white barrier stripe")==20,"Alternating red/white arm segments");
        foreach(string name in new[]{"Last Slice rooftop pizza","Duck Diner rooftop roast duck","Noodle Box rooftop ramen","Quack Mart rooftop googly Q","Car Wash rooftop car"})Check(extra.Find(name)!=null,"Rooftop identifier "+name);
        var emissive=b.GetComponentsInChildren<MeshRenderer>().Where(r=>!r.GetComponent<TMPro.TMP_Text>()).SelectMany(r=>r.sharedMaterials).Distinct().Where(m=>m&&m.HasProperty("_EmissionColor")&&m.GetColor("_EmissionColor").maxColorComponent>0).ToArray();Check(emissive.Length==1&&emissive[0].name=="Shared neon atlas","One mesh-neon material for block");
        foreach(var t in b.GetComponentsInChildren<TMPro.TMP_Text>())Check(t.font&&t.font.name=="CutePixel SDF","CutePixel text "+t.name);
        Check(a.Find("Arcade/Glowing sign ARCADE").GetComponentInChildren<TMPro.TMP_Text>().fontSharedMaterial.name.Contains("yellow"),"User yellow arcade sign retained");
        foreach(var l in b.GetComponentsInChildren<Light>())Check(l.lightmapBakeType==LightmapBakeType.Baked,"Baked-only light "+l.name);
        var refined=extra.Find("Round two store refinement");if(refined)
        {
            Check(refined.Find("Vendor model spotlights").GetComponentsInChildren<Light>().Length==12,"Two spotlights per vendor rooftop model");
            var ceiling=refined.Find("Long ceiling fluorescent fixtures");Check(ceiling.childCount==5&&ceiling.Cast<Transform>().Count(t=>t.name=="Fluorescent burnt out")==1,"Five fluorescent fixtures including one burnt out");
            Check(ceiling.GetComponentsInChildren<Light>().Length==8,"Four working twin-pool fluorescents");
            Check(refined.Find("South upright drink coolers").childCount==4,"Four upright drink coolers");
            Check(refined.Find("Back room inventory shelves").childCount==6,"Six inventory racks");
            for(float z=63;z<80;z+=.5f)Check(Clear(new Vector3(38,0,z)),"Stockroom aisle "+z);
            foreach(var mat in refined.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Distinct())Check(mat&&mat.shader&&mat.shader.isSupported&&mat.shader.name!="Hidden/InternalErrorShader","Supported store material "+mat?.name);
            Check(a.Find("Gas forecourt/Glowing sign GAS").GetComponentInChildren<TMPro.TMP_Text>().fontSharedMaterial.name.Contains("teal"),"Gas sign restored to teal");
        }
        Check(!Clear(new Vector3(40,0,70)),"Positive collision-query control");
        var graph=graphReady?CrawlGraph.Load(b.graph):null;foreach(var t in b.spawnCandidates.enemies){Check(Clear(t.position),"Enemy standing clearance "+t.position);if(graphReady)Check(graph.FindNearestGroundNode(t.position,.8f)>=0,"Enemy baked support "+t.position);}
        for(int crew=1;crew<=8;crew++)for(int seed=0;seed<100;seed++){var selected=RailEnemySpawnBalance.Select(b,seed,crew);int quota=RailEnemySpawnBalance.PerQuadrant(crew);bool ok=selected.Length==4*quota&&Enumerable.Range(0,4).All(q=>selected.Count(t=>RailEnemySpawnBalance.Quadrant(b,t)==q)==quota);for(int i=0;i<selected.Length;i++)for(int j=0;j<i;j++){var d=selected[i].position-selected[j].position;d.y=0;ok&=d.magnitude>=14.999f;}Check(ok,"Enemy quota/spacing crew "+crew+" seed "+seed);}
        File.WriteAllLines(R2Evidence+"/checks.txt",log);Debug.Log("Round two checks "+log.Count(x=>x.StartsWith("PASS"))+" pass / "+log.Count(x=>x.StartsWith("FAIL"))+" fail");
    }
}
#endif
