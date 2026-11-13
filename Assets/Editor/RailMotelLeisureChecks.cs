#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class RailMotelLeisureChecks
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
                    {for(int i=0;i<16&&!fixedIt;i++){var p=origin+new Vector3(Mathf.Cos(i*Mathf.PI/8)*radius,0,Mathf.Sin(i*Mathf.PI/8)*radius);var ray=new Ray(p+Vector3.up*.7f,Vector3.down);var surfaces=cols.Select(c=>c.Raycast(ray,out var h,1.6f)?h.point.y:float.NegativeInfinity).Where(y=>!float.IsNegativeInfinity(y)).OrderByDescending(y=>y);foreach(float y in surfaces){p.y=y+.05f-bounds.min.y;if(Valid(p)){t.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);fixedIt=true;break;}}}if(fixedIt)break;}
                }
                Check(Valid(t.position),"Loot support/clearance "+t.name+" "+t.position);
                Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,"Native marker "+t.name);
            }
        }
        var extra=b.transform.Find("Env/"+RailMotelLeisure.Root);
        Check(extra.Find("Arrival and public parking").Cast<Transform>().Count(t=>t.name.StartsWith("Bus ") && t.Find("Bus lower body")!=null)==5,"Five buses");
        Check(b.transform.Find("Env/Motel greybox v1/Exterior paths and props").Cast<Transform>().Count(t=>t.name=="Abandoned car")==2,"Two retained cars");
        foreach(var c in extra.GetComponentsInChildren<Collider>())if(!c.name.StartsWith("Ground"))Check(c.bounds.max.x< -4||c.bounds.min.x>4,"Rail clearance "+c.name);
        foreach(var f in extra.GetComponentsInChildren<MeshFilter>())if(!f.GetComponent<TMPro.TMP_Text>())Check(f.sharedMesh&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Mesh UV2 "+f.name);
        foreach(var t in b.GetComponentsInChildren<TMPro.TMP_Text>())Check(t.font&&t.font.name=="CutePixel SDF","Font "+t.name);
        foreach(var p in new[]{new Vector3(-26,0,46),new Vector3(-8,.1f,75),new Vector3(-26,.16f,91),new Vector3(26,.22f,119),new Vector3(34,.16f,147),new Vector3(-39,.16f,148.5f),new Vector3(-26,-1.6f,69)})
            Check(phy.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.7f,.28f,hits,1<<6,QueryTriggerInteraction.Ignore)==0,"Entrance headroom "+p);
        if(repair){PrefabUtility.SaveAsPrefabAsset(s.prefabContentsRoot,s.assetPath);AssetDatabase.SaveAssets();}
        File.WriteAllLines("docs/validation/motel-leisure/checks.txt",log);
        UnityEngine.Debug.Log(log.Count+" checks; "+log.Count(l=>l.StartsWith("FAIL"))+" failures\n"+string.Join("\n",log.Where(l=>l.StartsWith("FAIL"))));
    }
}
#endif
