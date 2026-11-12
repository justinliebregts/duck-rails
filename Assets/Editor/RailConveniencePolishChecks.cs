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

public static class RailConveniencePolishChecks
{
    public static void Run()
    {
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage==null||stage.assetPath!=RailConvenienceStore.PrefabPath)throw new Exception("Open Convenience Store prefab");
        var b=stage.prefabContentsRoot.GetComponent<RailBlock>();var physics=stage.scene.GetPhysicsScene();Physics.SyncTransforms();log.Clear();
        var cols=b.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger&&c.gameObject.layer==6).ToArray();var hits=new Collider[128];
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold})
        {
            var bounds=RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            foreach(var t in RailSpawnPool.Candidates(b,kind))
            {
                bool support=true;foreach(float x in new[]{-bounds.extents.x,bounds.extents.x})foreach(float z in new[]{-bounds.extents.z,bounds.extents.z})
                {var bottom=t.position+new Vector3(bounds.center.x+x,bounds.min.y,bounds.center.z+z);support&=cols.Any(c=>c.Raycast(new Ray(bottom+Vector3.up*.12f,Vector3.down),out _,.3f));}
                int count=physics.OverlapBox(t.position+bounds.center,bounds.extents*.97f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore);
                Check(support,"Loot support "+kind+" "+t.position.ToString("F2"));Check(count==0,"Loot clearance "+kind+" "+t.position.ToString("F2")+(count>0?" hits "+string.Join(",",hits.Take(count).Select(c=>c.name)):""));
                Check(PrefabUtility.GetCorrespondingObjectFromSource(t.gameObject)!=null,"Native marker "+t.name);
            }
        }
        var env=b.transform.Find("Env");var extra=env.Find("Convenience polish November 11");var authored=env.Find(RailConvenienceStore.RootName);
        Check(extra!=null,"Polish hierarchy present");Check(b.spawnCandidates.coal.Length==126&&b.spawnCandidates.gold.Length==109&&b.spawnCandidates.enemies.Length==27,"Pools 126 coal /109 gold /27 enemies");
        Check(extra.Find("Scattered abandoned cars").childCount==12,"Twelve cars");Check(authored.Find("Arcade").Cast<Transform>().Count(t=>t.name.StartsWith("Arcade reverse middle"))==4,"Four reverse middle arcade cabinets");
        Check(extra.Find("Bilateral trackside streetlights").GetComponentsInChildren<Light>().Length==12,"Twelve additional bilateral lamps");
        Check(b.GetComponentsInChildren<RailWashBrushSpin>().Length==6,"Six cosmetic rotating brush rollers");
        Check(Vector3.Dot(authored.Find("Car wash").forward,Vector3.left)>.99f,"Car wash faces railway");
        foreach(var t in extra.Find("Wooden side fences 1m inset").Cast<Transform>().Where(t=>t.name.StartsWith("Boundary")))Check(Mathf.Abs(Mathf.Abs(t.localPosition.x)-47)<.001f,"Fence 1m inset "+t.name);
        foreach(var t in authored.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("stripe")||t.name=="Crosswalk"))Check(t.localPosition.y>=.05f&&t.GetComponent<Collider>()==null,"Raised noncolliding paint "+t.name);
        foreach(var l in b.GetComponentsInChildren<Light>())Check(l.lightmapBakeType==LightmapBakeType.Baked,"Baked lighting "+l.name);
        foreach(var f in b.GetComponentsInChildren<MeshFilter>().Where(f=>!f.GetComponent<TMPro.TMP_Text>()))Check(f.sharedMesh!=null,"Mesh reference "+f.name);
        foreach(var r in b.GetComponentsInChildren<Renderer>())Check(r.sharedMaterials.All(m=>m!=null),"Material references "+r.name);
        foreach(var t in b.GetComponentsInChildren<TMPro.TMP_Text>())Check(t.font&&t.font.name=="CutePixel SDF","CutePixel font "+t.name);
        // Changes must leave the entire train clearance corridor empty above track level.
        foreach(var c in extra.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger))Check(c.bounds.max.x< -4||c.bounds.min.x>4,"Rail clearance "+c.name);
        var semi=extra.Find("Broken articulated semi");foreach(var r in semi.GetComponentsInChildren<Renderer>())Check(r.bounds.max.x< -4&&r.bounds.min.z>138&&r.bounds.max.z<158,"Northwest semi outside rails and seams "+r.name);
        bool Clear(Vector3 p)=>physics.OverlapCapsule(p+Vector3.up*.4f,p+Vector3.up*1.7f,.28f,hits,1<<6,QueryTriggerInteraction.Ignore)==0;
        foreach(var p in new[]{new Vector3(-15,0,129),new Vector3(-13,0,129),new Vector3(-27,0,129),new Vector3(26,0,45),new Vector3(34,0,45),new Vector3(41,0,45),new Vector3(-19,0,78.5f),new Vector3(22,0,78.5f)})Check(Clear(p),"Access headroom "+p);
        var trailer=semi.Find("Enterable open trailer");for(float z=-4.8f;z<=4;z+=1)Check(Clear(trailer.TransformPoint(new Vector3(0,1.16f,z))),"Trailer interior headroom "+z);
        Check(physics.OverlapBox(new Vector3(-47,.48f,80),Vector3.one*.2f,hits,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore)>0,"Positive collision-query control");
        bool selection=true;for(int crew=1;crew<=8;crew++)for(int seed=0;seed<100;seed++)
        {var selected=RailEnemySpawnBalance.Select(b,seed,crew);int quota=RailEnemySpawnBalance.PerQuadrant(crew);selection&=selected.Length==quota*4&&Enumerable.Range(0,4).All(q=>selected.Count(t=>RailEnemySpawnBalance.Quadrant(b,t)==q)==quota);for(int i=0;i<selected.Length;i++)for(int j=0;j<i;j++){var d=selected[i].position-selected[j].position;d.y=0;selection&=d.magnitude>=14.999f;}}
        Check(selection,"800 seeded enemy selections retain 4/8/12 quotas and 15m spacing");
        var graph=Game.AI.CrawlGraph.Load(b.graph);
        foreach(var t in b.spawnCandidates.enemies)
        {
            Check(graph.FindNearestGroundNode(t.position,.8f)>=0,"Enemy baked ground support "+t.position);
            Check(physics.OverlapCapsule(t.position+Vector3.up*.45f,t.position+Vector3.up*1.65f,.3f,hits,1<<6,QueryTriggerInteraction.Ignore)==0,"Enemy standing clearance "+t.position);
        }
        File.WriteAllLines("docs/validation/convenience-polish/checks.txt",log);Debug.Log("Convenience checks: "+log.Count(s=>s.StartsWith("PASS"))+" pass; "+log.Count(s=>s.StartsWith("FAIL"))+" fail");
    }
    static readonly List<string> log=new();
    static void Check(bool ok,string message)=>log.Add((ok?"PASS ":"FAIL ")+message);
    // Repair only invalid candidates, keeping their landmark and floor where possible.
    public static void RepairLoot()
    {
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage==null||stage.assetPath!=RailConvenienceStore.PrefabPath)throw new Exception("Open Convenience Store prefab");var b=stage.prefabContentsRoot.GetComponent<RailBlock>();var physics=stage.scene.GetPhysicsScene();Physics.SyncTransforms();
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
                        Undo.RecordObject(t,"Repair convenience loot support");t.position=p;PrefabUtility.RecordPrefabInstancePropertyModifications(t);moved.Add(t.name+" "+before.ToString("F3")+" -> "+p.ToString("F3"));found=true;break;
                    }
                }
                if(!found)throw new Exception("No supported local placement for "+t.name);
            }
        }
        PrefabUtility.SaveAsPrefabAsset(b.gameObject,RailConvenienceStore.PrefabPath);File.WriteAllLines("docs/validation/convenience-polish"+"/loot-repairs.txt",moved);Debug.Log("Repaired "+moved.Count+" convenience loot candidates");
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
            rt=new RenderTexture(1200,900,24);tex=new Texture2D(1200,900,TextureFormat.RGB24,false);c.targetTexture=rt;c.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1200,900),0,0);tex.Apply();File.WriteAllBytes("docs/validation/convenience-polish"+"/"+name+".png",tex.EncodeToPNG());
        }
        finally{RenderTexture.active=previous;if(camera)Object.DestroyImmediate(camera);if(light)Object.DestroyImmediate(light);if(rt){rt.Release();Object.DestroyImmediate(rt);}if(tex)Object.DestroyImmediate(tex);RenderSettings.ambientMode=mode;RenderSettings.ambientLight=ambient;RenderSettings.fog=fog;RenderSettings.sun=sun;}
    }
}
#endif
