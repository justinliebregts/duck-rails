#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RailMotelFinishChecks
{
    public static void Run(bool repair=false)
    {
        var s=PrefabStageUtility.GetCurrentPrefabStage();
        if(s==null||s.assetPath!=RailMotelGreybox.PrefabPath)throw new Exception("Open motel prefab");
        var b=s.prefabContentsRoot.GetComponent<RailBlock>();var phy=s.scene.GetPhysicsScene();Physics.SyncTransforms();
        var cols=b.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();
        var hits=new Collider[128];var log=new List<string>();
        void Check(bool ok,string label)=>log.Add((ok?"PASS ":"FAIL ")+label);
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            bool Valid(Vector3 p)
            {
                foreach(float x in new[]{-bounds.extents.x,bounds.extents.x})foreach(float z in new[]{-bounds.extents.z,bounds.extents.z})
                {var q=p+new Vector3(bounds.center.x+x,bounds.min.y,bounds.center.z+z);if(!cols.Any(c=>c.Raycast(new Ray(q+Vector3.up*.12f,Vector3.down),out _,.3f)))return false;}
                return phy.OverlapBox(p+bounds.center,bounds.extents*.97f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0;
            }
            foreach(var t in RailSpawnPool.Candidates(b,kind))
            {
                if(repair&&!Valid(t.position))
                {
                    var origin=t.position;bool fixedIt=false;
                    foreach(float radius in new[]{0f,.25f,.5f,1f,1.5f,2f})
                    {for(int i=0;i<16&&!fixedIt;i++){var p=origin+new Vector3(Mathf.Cos(i*Mathf.PI/8)*radius,0,Mathf.Sin(i*Mathf.PI/8)*radius);var ray=new Ray(p+Vector3.up*.7f,Vector3.down);var surfaces=cols.Select(c=>c.Raycast(ray,out var h,4.8f)?h.point.y:float.NegativeInfinity).Where(y=>!float.IsNegativeInfinity(y)).OrderByDescending(y=>y);foreach(float y in surfaces){p.y=y+.05f-bounds.min.y;if(Valid(p)){t.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);fixedIt=true;break;}}}if(fixedIt)break;}
                }
                Check(Valid(t.position),"Loot support/clearance "+t.name+" "+t.position);
                Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,"Native marker "+t.name);
            }
        }
        var extra=b.transform.Find("Env/"+RailMotelLeisure.Root);
        Check(extra.Find("Arrival and public parking").Cast<Transform>().Count(t=>t.name.StartsWith("Bus ") && t.Find("Bus lower body")!=null)==5,"Five buses");
        Check(b.transform.Find("Env/Motel greybox v1/Exterior paths and props").Cast<Transform>().Count(t=>t.name=="Abandoned car")==2,"Two retained cars");
        foreach(var c in extra.GetComponentsInChildren<Collider>())if(!c.name.StartsWith("Ground"))Check(c.bounds.max.x< -4||c.bounds.min.x>4,"Rail clearance "+c.name);
        foreach(var f in extra.GetComponentsInChildren<MeshFilter>())if(!f.GetComponent<TMPro.TMP_Text>() && !f.name.Contains("wheel"))Check(f.sharedMesh&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Mesh UV2 "+f.name);
        foreach(var t in b.GetComponentsInChildren<TMPro.TMP_Text>())Check(t.font&&t.font.name=="CutePixel SDF","Font "+t.name);
        var motel=b.transform.Find("Env/Motel greybox v1/Motel");
        Check(motel.GetComponentsInChildren<Transform>().Count(t=>t.name=="Joined wall shell")==2,"Joined exterior wall shells on both floors");
        Check(motel.GetComponentsInChildren<Transform>().Where(t=>t.name=="Revised wall"||t.name.StartsWith("Closed room solid")).All(t=>!t.GetComponent<MeshRenderer>()),"Old overlapping wall renderers removed; collision retained");
        Check(motel.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Room furnishings "))==6,"Six accessible rooms furnished");
        foreach(var dresser in motel.GetComponentsInChildren<Collider>().Where(c=>c.name=="Dresser cabinet"))
            Check(!motel.GetComponentsInChildren<Collider>().Where(c=>c.name.StartsWith("Bed ")).Any(c=>c.bounds.Intersects(dresser.bounds)),"Dresser clear of beds "+dresser.transform.parent.name);
        Check(motel.GetComponentsInChildren<Transform>().Count(t=>t.name=="HVAC cabinet")==3,"Three rooftop HVAC cabinets");
        foreach(var mr in b.GetComponentsInChildren<MeshRenderer>().Where(mr=>!mr.GetComponent<TMPro.TMP_Text>()))
            Check(mr.sharedMaterials.All(m=>m&&m.mainTexture),"Pixel material "+mr.name);
        Check(!extra.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="GOLF"||t.text=="SKATE RENTAL"),"Golf and rental signs converted from TMP to solid meshes");
        var golf=extra.Find("Mini golf");
        Check(golf.GetComponentsInChildren<Transform>().Any(t=>t.name=="Restored duck obstacle"),"Duck restored");
        Check(golf.GetComponentsInChildren<Transform>().Any(t=>t.name=="Shoot-through house"),"Shoot-through house");
        var house=golf.GetComponentsInChildren<Transform>().First(t=>t.name=="Shoot-through house");
        Check(phy.OverlapSphere(house.TransformPoint(new Vector3(0,.65f,0)),.25f,hits,1<<6,QueryTriggerInteraction.Ignore)==0,"House center is an actual shoot-through opening");
        Check(golf.GetComponentsInChildren<MeshFilter>().Where(t=>t.name=="Unique putting outline").Select(f=>string.Join(";",f.sharedMesh.vertices.Select(v=>v.ToString("F3")))).Distinct().Count()==9,"Nine distinct outline geometries");
        foreach(var board in extra.Find("Municipal pool").Cast<Transform>().Where(t=>t.name.StartsWith("Diving board")))
        {
            var bb=board.GetComponent<Collider>().bounds;
            var access=extra.Find("Municipal pool").Cast<Transform>().Where(t=>t.name.StartsWith("Supported diving access")).OrderBy(t=>Mathf.Abs(t.GetComponentsInChildren<Collider>().First().bounds.center.x-bb.center.x)).First();
            var steps=access.Cast<Transform>().Where(t=>t.name.StartsWith("Tread ")).Select(t=>t.GetComponent<Collider>().bounds).ToArray();
            var last=steps.OrderBy(q=>q.min.z).First();
            Check(Mathf.Abs(last.min.z-bb.max.z)<.001f&&Mathf.Abs(last.max.y-bb.max.y)<.001f,"Diving board stair joint "+board.name);
            foreach(var post in access.Cast<Transform>().Where(t=>t.name=="Diving anchored post"||t.name=="Diving top post"))
            {
                var q=post.GetComponent<Collider>().bounds;var bottom=q.center;bottom.y=q.min.y;
                Check(steps.Append(bb).Any(v=>bottom.x>v.min.x&&bottom.x<v.max.x&&bottom.z>=v.min.z-.01f&&bottom.z<=v.max.z+.01f&&Mathf.Abs(bottom.y-v.max.y)<.01f),"Railing post supported "+post.position);
            }
        }
        Check(motel.GetComponentsInChildren<Transform>().Count(t=>t.name.StartsWith("Closed room solid"))==7,"Seven closed guest rooms");
        Check(motel.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!int.TryParse(t.text,out _)),"No room number text");
        Check(extra.Find("Mini golf").Cast<Transform>().Count(t=>t.name.StartsWith("Golf hole "))==9,"Nine varied golf holes");
        // The saved starting prefab has no waterslide; preserve that scene edit.
        Check(extra.Find("Municipal pool").Find("Deep basin floor").position.y< -3,"Deeper pool end");
        Check(extra.Find("Municipal pool").Cast<Transform>().Count(t=>t.name.StartsWith("Diving board"))==2,"Two diving boards");
        Check(extra.Find("Roller rink").GetComponentsInChildren<Transform>().All(t=>!t.name.StartsWith("Fence")),"Rink fence replaced");
        Check(extra.Find("Perimeter trees").GetComponentsInChildren<MeshCollider>().All(c=>c.convex),"Convex tree colliders");
        Check(extra.Find("Perimeter trees").Cast<Transform>().Where(t=>t.name=="Tree trunk").Select(t=>t.localScale.y).Distinct().Count()>5,"Varied tree sizes");
        foreach(var t in motel.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Stair ")))Check(t.GetComponentsInChildren<Transform>().All(c=>!c.name.StartsWith("Guard")),"No solid stair sides "+t.name);
        var fixtures=motel.GetComponentsInChildren<Transform>();Check(fixtures.Count(t=>t.name=="Working room fixture")==8&&fixtures.Count(t=>t.name=="Broken room fixture")==0&&fixtures.Count(t=>t.name=="White fixture baked light")==8,"Only white facade fixtures with eight baked lights");
        var points=new List<Vector3>{new Vector3(30.75f,0,62),new Vector3(-26,.16f,91),new Vector3(-12.25f,.16f,91),new Vector3(26,.22f,119),new Vector3(26,.22f,145),new Vector3(34,.16f,153),new Vector3(-16,.16f,117)};
        for(int floor=0;floor<2;floor++)for(int i=0;i<5;i++)if(!RailMotelRevision.Closed.Contains((floor+1)*100+i+1))points.Add(new Vector3(28.1f,floor*3.6f,55.75f+i*8));
        foreach(var q in points)Check(phy.OverlapCapsule(q+Vector3.up*.35f,q+Vector3.up*1.6f,.25f,hits,1<<6,QueryTriggerInteraction.Ignore)==0,"Accessible doorway "+q);
        foreach(var l in extra.GetComponentsInChildren<Light>())Check(l.lightmapBakeType==LightmapBakeType.Baked,"Baked light "+l.name);
        if(repair){PrefabUtility.SaveAsPrefabAsset(s.prefabContentsRoot,s.assetPath);AssetDatabase.SaveAssets();}
        File.WriteAllLines("docs/validation/motel-leisure/finish-checks.txt",log);
        UnityEngine.Debug.Log(log.Count+" checks; "+log.Count(l=>l.StartsWith("FAIL"))+" failures\n"+string.Join("\n",log.Where(l=>l.StartsWith("FAIL"))));
    }
}
#endif
