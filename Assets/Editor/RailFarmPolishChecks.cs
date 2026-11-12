#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

public static class RailFarmPolishChecks
{
    static readonly List<string> log=new();
    static void Check(bool ok,string message)=>log.Add((ok?"PASS ":"FAIL ")+message);
    // Repair only invalid candidates, keeping their landmark and floor where possible.
    public static void RepairLoot()
    {
        var stage=PrefabStageUtility.GetCurrentPrefabStage();var b=stage.prefabContentsRoot.GetComponent<RailBlock>();var physics=stage.scene.GetPhysicsScene();Physics.SyncTransforms();
        var cols=b.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();var overlaps=new Collider[64];var moved=new List<string>();
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            bool Valid(Vector3 p)
            {
                foreach(float x in new[]{-bounds.extents.x,bounds.extents.x})foreach(float z in new[]{-bounds.extents.z,bounds.extents.z})
                {var bottom=p+new Vector3(bounds.center.x+x,bounds.min.y,bounds.center.z+z);if(!cols.Any(c=>c.Raycast(new Ray(bottom+Vector3.up*.12f,Vector3.down),out _,.3f)))return false;}
                return physics.OverlapBox(p+bounds.center,bounds.extents*.97f,overlaps,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)==0;
            }
            foreach(var t in RailSpawnPool.Candidates(b,kind))
            {
                if(Valid(t.position))continue;var before=t.position;bool found=false;
                for(float radius=0;radius<=4&&!found;radius+=.4f)for(int j=0;j<(radius==0?1:16)&&!found;j++)
                {
                    float a=j*Mathf.PI/8;var start=before+new Vector3(Mathf.Cos(a)*radius,1.5f,Mathf.Sin(a)*radius);var ray=new Ray(start,Vector3.down);
                    var supports=new List<RaycastHit>();foreach(var c in cols)if(c.Raycast(ray,out var hit,4)&&hit.normal.y>.7f)supports.Add(hit);
                    foreach(var hit in supports.OrderBy(h=>Mathf.Abs(h.point.y-(before.y+bounds.min.y))))
                    {
                        var p=hit.point+Vector3.up*(.05f-bounds.min.y);if(!Valid(p))continue;
                        // Do not merge different candidates or cross the railway during a local repair.
                        if(RailSpawnPool.Candidates(b,kind).Any(other=>other!=t&&Vector3.Distance(other.position,p)<.65f))continue;
                        Undo.RecordObject(t,"Repair farm loot support");t.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);moved.Add(t.name+" "+before.ToString("F3")+" -> "+p.ToString("F3"));found=true;break;
                    }
                }
                if(!found)throw new Exception("No supported local placement for "+t.name);
            }
        }
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,RailFarmSilos.PrefabPath);File.WriteAllLines(RailFarmPolish.Evidence+"/loot-repairs.txt",moved);Debug.Log("Repaired "+moved.Count+" farm loot candidates");
    }
    public static void Run(RailBlock blockOverride=null)
    {
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(!blockOverride&&(stage==null||stage.assetPath!=RailFarmSilos.PrefabPath))throw new Exception("Open Farm prefab");
        var b=blockOverride?blockOverride:stage.prefabContentsRoot.GetComponent<RailBlock>();var physics=b.gameObject.scene.GetPhysicsScene();Physics.SyncTransforms();log.Clear();
        var colliders=b.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();
        var hits=new Collider[64];
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            foreach(var t in RailSpawnPool.Candidates(b,kind))
            {
                bool support=true;foreach(float x in new[]{-bounds.extents.x,bounds.extents.x})foreach(float z in new[]{-bounds.extents.z,bounds.extents.z})
                {var bottom=t.position+new Vector3(bounds.center.x+x,bounds.min.y,bounds.center.z+z);support&=colliders.Any(c=>c.Raycast(new Ray(bottom+Vector3.up*.12f,Vector3.down),out _, .3f));}
                Check(support,"Loot footprint "+t.name+" "+t.localPosition.ToString("F2"));
                int n=physics.OverlapBox(t.position+bounds.center,bounds.extents*.97f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore);
                Check(n==0,"Loot clearance "+t.name+(n>0?" hits "+string.Join(",",hits.Take(n).Select(c=>c.name)):""));
                Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,"Marker prefab "+t.name);
            }
        }
        var house=b.transform.Find("Env/"+RailFarmSilos.RootName+"/Farmhouse");
        foreach(var name in new[]{"Ground floor","Upper floor","Attic"})
        {
            var f=house.Find(name);var steps=f.Find("Broad nine-step stair").GetComponentsInChildren<BoxCollider>();Check(steps.Length==9,"Nine steps "+name);
            foreach(var c in steps)
            {
                var top=c.transform.position+Vector3.up*c.size.y/2;
                Check(Mathf.Abs(c.size.x-4.39f)<.001f,"Full stair width "+name+" "+c.name);
                int n=physics.OverlapCapsule(top+Vector3.up*.47f,top+Vector3.up*1.75f,.28f,hits,1<<6,QueryTriggerInteraction.Ignore);
                Check(n==0,"Stair headroom "+name+" "+c.name+(n>0?" hits "+string.Join(",",hits.Take(n).Select(v=>v.name)):""));
            }
        }
        var route=new[]{new Vector3(15,0,90.5f),new Vector3(15,4,106),new Vector3(22,5.5f,106),new Vector3(22,6,110),new Vector3(15,6.5f,110),new Vector3(15,7.5f,98)};
        foreach(var t in b.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Silo ramp ")))
        {
            int segment=int.Parse(t.name.Substring("Silo ramp ".Length));var a=route[segment-1];var end=route[segment];
            for(int i=1;i<10;i++){var p=t.TransformPoint(Vector3.Lerp(a,end,i/10f));int n=physics.OverlapCapsule(p+Vector3.up*.47f,p+Vector3.up*1.75f,.28f,hits,1<<6,QueryTriggerInteraction.Ignore);Check(n==0,t.name+" headroom "+i+(n>0?" hits "+string.Join(",",hits.Take(n).Select(c=>c.name)):""));}
        }
        foreach(var l in b.GetComponentsInChildren<Light>())Check(l.lightmapBakeType==LightmapBakeType.Baked,"Baked light "+l.name);
        var w=b.GetComponentInChildren<Game.Audio.WaterVolume>();Check(w&&w.waterSurface&&w.Contains(new Vector3(12,-.6f,30))&&!w.Contains(new Vector3(12,.15f,41)),"Ditch water covers bed and leaves bridge dry");
        Check(physics.OverlapBox(house.TransformPoint(new Vector3(14,1,134)),Vector3.one*.3f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)>0,"Positive overlap control");
        foreach(var k in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold,RailSpawnKind.Enemy})log.Add("INFO "+k+" candidates "+RailSpawnPool.Candidates(b,k).Length+" configured count "+RailSpawnPool.Count(b,k));
        Directory.CreateDirectory(RailFarmPolish.Evidence);File.WriteAllLines(RailFarmPolish.Evidence+"/geometry-checks.txt",log);Debug.Log("Farm checks: "+log.Count(x=>x.StartsWith("PASS"))+" pass, "+log.Count(x=>x.StartsWith("FAIL"))+" fail.");
    }
    public static void Capture(string name,Vector3 position,Vector3 target,float ortho=0,bool bright=true)
    {
        var stage=PrefabStageUtility.GetCurrentPrefabStage();var scene=stage!=null?stage.scene:SceneManager.GetActiveScene();
        var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;bool fog=RenderSettings.fog;var sun=RenderSettings.sun;
        GameObject camera=null,light=null;RenderTexture rt=null;Texture2D tex=null;var previous=RenderTexture.active;
        try
        {
            camera=new GameObject("Temporary review camera");SceneManager.MoveGameObjectToScene(camera,scene);var c=camera.AddComponent<Camera>();c.scene=scene;c.overrideSceneCullingMask=EditorSceneManager.GetSceneCullingMask(scene);c.transform.position=position;c.transform.LookAt(target);c.nearClipPlane=.05f;c.farClipPlane=300;c.fieldOfView=75;c.orthographic=ortho>0;c.orthographicSize=ortho;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.08f,.11f,.14f);
            if(bright){light=new GameObject("Temporary review fill");SceneManager.MoveGameObjectToScene(light,scene);var l=light.AddComponent<Light>();l.type=LightType.Directional;l.intensity=1.4f;l.transform.rotation=Quaternion.Euler(55,-35,0);RenderSettings.sun=l;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.6f,.6f,.6f);}RenderSettings.fog=false;
            rt=new RenderTexture(1200,900,24);tex=new Texture2D(1200,900,TextureFormat.RGB24,false);c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes(RailFarmPolish.Evidence+"/"+name+".png",tex.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;if(camera)Object.DestroyImmediate(camera);if(light)Object.DestroyImmediate(light);if(rt){rt.Release();Object.DestroyImmediate(rt);}if(tex)Object.DestroyImmediate(tex);RenderSettings.ambientMode=mode;RenderSettings.ambientLight=ambient;RenderSettings.fog=fog;RenderSettings.sun=sun;}
    }
}
#endif
