#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using Game.AI;
using Object=UnityEngine.Object;

public static class RailContainerNorthYardChecks
{
    static List<string> log;
    static void Check(bool value,string label)=>log.Add((value?"PASS ":"FAIL ")+label);
    static void Path(Vector3 a,Vector3 b,string label)
    {
        var d=b-a;bool start=!Physics.CheckCapsule(a+Vector3.up*.5f,a+Vector3.up*1.6f,.3f,1<<6);
        Check(start&&!Physics.CapsuleCast(a+Vector3.up*.5f,a+Vector3.up*1.6f,.3f,d.normalized,d.magnitude,1<<6),label);
    }
    public static void Run()
    {
        RailContainerYard.Guard();EditorSceneManager.OpenScene(RailContainerYard.BakeScene);Physics.SyncTransforms();log=new();
        var b=Object.FindFirstObjectByType<RailBlock>();var root=RailBlock.FindEnvironment(b.transform,RailContainerYard.RootName).Find(RailContainerNorthYard.Root);
        Check(root!=null,"Saved north yard present");
        foreach(var c in root.GetComponentsInChildren<Collider>())
        {
            var q=c.bounds;Check(c.enabled&&!c.isTrigger&&c.gameObject.layer==6,"Solid environment collision "+c.name);
            Check(q.min.x>=-46.8f&&q.max.x<=46.8f&&q.min.z>=118&&q.max.z<=154,"North-area/fence/seam footprint "+c.name);
            Check(q.max.x<=-6||q.min.x>=6,"Clear central railway "+c.name);
            Check(q.min.y>=-.13f,"No buried geometry below service slab "+c.name);
        }
        foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>!f.GetComponent<TMP_Text>()))
        {Check(f.sharedMesh&&f.sharedMesh.uv.Length==f.sharedMesh.vertexCount&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Material/lightmap UVs "+f.name);Check(f.GetComponent<Renderer>().sharedMaterial!=null,"Material present "+f.name);}
        var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
        foreach(var t in root.GetComponentsInChildren<TMP_Text>())Check(t.font==font,"CutePixel label "+t.text.Replace('\n',' '));
        var lights=root.GetComponentsInChildren<Light>();Check(lights.Length==2&&lights.All(l=>l.lightmapBakeType==LightmapBakeType.Baked&&l.type==LightType.Spot),"Two baked north floodlights");
        Check(!root.GetComponentsInChildren<Rigidbody>().Any(),"Static scenery only");
        var tanks=root.GetComponentsInChildren<MeshCollider>().Where(c=>c.name=="Rounded propane vessel").ToArray();Check(tanks.Length==2,"Two rounded horizontal propane vessels");
        foreach(var tank in tanks)
        {
            Check(Vector3.Distance(tank.bounds.size,new Vector3(3,3,11))<.01f,"Tank dimensions 3 x 3 x 11 m");
            var m=tank.sharedMesh;var v=m.vertices;var tris=m.triangles;
            for(int i=0;i<tris.Length;i+=3){var a=v[tris[i]];var c=v[tris[i+1]];var d=v[tris[i+2]];Check(Vector3.Dot(Vector3.Cross(c-a,d-a),(a+c+d)/3)>0,"Outward tank triangle "+i/3);}
            Check(tank.Raycast(new Ray(tank.bounds.center+Vector3.left*3,Vector3.right),out _,4),"Tank side has fitted collision");
        }
        Path(new(-8,0,120),new(-10,0,126.7f),"West approach beside forklift");
        Path(new(-10,0,126.7f),new(-23,0,126.7f),"West route behind forklift");
        Path(new(-23,0,126.7f),new(-33,.04f,130),"Open shed south entrance");
        Path(new(-39,.04f,130),new(-27,.04f,130),"Shed front work aisle");
        Path(new(-32,.2f,132),new(-32,.2f,135),"Dismantled-container side opening");
        Path(new(10,0,120),new(15,0,144),"East approach from tracks");
        Path(new(29,0,124),new(29,0,141),"Truck east return aisle");
        Path(new(38,0,125),new(38,0,141),"Tank service aisle");
        Path(new(45,0,123),new(45,0,146),"Tank/fence outer return");
        Path(new(25,1.48f,144.5f),new(25,1.48f,132.5f),"Dock threshold and trailer interior");
        Path(new(-19.5f,3.04f,134),new(-10,3.04f,135.5f),"Pallet landing to fallen container roof");
        foreach(var step in root.GetComponentsInChildren<BoxCollider>().Where(c=>c.name.StartsWith("Dock step ")||c.name.StartsWith("Pallet stair ")))
        {var q=step.bounds;var p=new Vector3(q.center.x,q.max.y+.04f,q.center.z);Check(!Physics.CheckCapsule(p+Vector3.up*.4f,p+Vector3.up*1.6f,.25f,1<<6),"Step headroom "+step.name);}
        var graph=CrawlGraph.Load(b.graph);
        foreach(var p in new[]{new Vector3(-33,.04f,130),new(-32,.2f,135),new(25,1.48f,135),new(26,1.48f,144.5f),new(38,0,133)})
            Check(graph.FindNearestGroundNode(p,.8f)>=0,"Offline ground navigation at "+p);
        foreach(var kind in new[]{RailSpawnKind.Coal,RailSpawnKind.Gold,RailSpawnKind.Enemy})
        {
            var bounds=kind==RailSpawnKind.Enemy?new Bounds(Vector3.up*.4f,Vector3.one*.8f):RailLootPlacementWindow.ItemBounds(kind==RailSpawnKind.Gold);
            foreach(var t in RailSpawnPool.Points(b.spawnCandidates,kind))
            {
                var hits=Physics.OverlapBox(t.position+bounds.center,bounds.extents*.97f,Quaternion.identity,1<<6,QueryTriggerInteraction.Ignore);
                Check(!hits.Any(c=>c.transform.IsChildOf(root)),"No new geometry overlaps existing marker "+t.name);
                if(t.position.z<118)continue;
                foreach(float x in new[]{-bounds.extents.x,bounds.extents.x})foreach(float z in new[]{-bounds.extents.z,bounds.extents.z})
                {var foot=t.position+bounds.center+new Vector3(x,-bounds.extents.y,z);Check(Physics.Raycast(foot+Vector3.up*.15f,Vector3.down,.4f,1<<6),"North marker footprint support "+t.name);}
            }
        }
        var saved=AssetDatabase.LoadAssetAtPath<GameObject>(RailContainerYard.PrefabPath).GetComponent<RailBlock>();
        Check(saved.spawnCandidates.coal.Length==168&&saved.spawnCandidates.gold.Length==142&&saved.spawnCandidates.enemies.Length==32,"Redistributed candidate pools 168/142/32");
        Check(saved.coalCount==6&&saved.goldCount==2&&saved.enemyCount==5,"Per-run authoring settings retained");
        Directory.CreateDirectory(RailContainerNorthYard.Evidence);File.WriteAllLines(RailContainerNorthYard.Evidence+"/geometry.txt",log);
        var failures=log.Where(s=>s.StartsWith("FAIL")).ToArray();if(failures.Length>0)throw new Exception(string.Join("\n",failures.Take(30)));
        Debug.Log("NORTH YARD: "+log.Count+" editor checks passed");
    }
    public static void Capture(string directory="output/container-north-yard")
    {
        RailContainerYard.Guard();EditorSceneManager.OpenScene(RailContainerYard.BakeScene);Directory.CreateDirectory(directory);
        var go=new GameObject("Temporary north yard review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.12f,.16f,.18f);cam.farClipPlane=350;
        var sun=new GameObject("Temporary north yard review light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=3;sun.transform.rotation=Quaternion.Euler(50,30,0);
        try
        {
            RenderSettings.fog=false;RenderSettings.sun=sun;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.white;
            var sh=new UnityEngine.Rendering.SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.8f,.8f,.8f));RenderSettings.ambientProbe=sh;
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){r.lightmapIndex=-1;r.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;}
            Shot("overview",new(67,55,93),new(0,1.5f,134));Shot("repair-shed",new(-13,7,119),new(-33,2,135));Shot("propane-and-truck",new(13,7,120),new(36,2,135));Shot("dock",new(15,5,151),new(25,2,138));
            cam.orthographic=true;cam.orthographicSize=32;Shot("top-down",new(0,100,133),new(0,0,133));
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(sun.gameObject);EditorSceneManager.OpenScene(RailContainerYard.BakeScene);}
        void Shot(string name,Vector3 eye,Vector3 target)
        {
            cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye,cam.orthographic?Vector3.forward:Vector3.up);cam.fieldOfView=65;
            var rt=new RenderTexture(1600,1000,24);var prior=RenderTexture.active;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes(directory+"/"+name+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=prior;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
#endif
