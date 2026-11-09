#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Game.AI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

public static class RailQuarryMineChecks
{
    public static void PrepareRuntimeProof()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish bake/play first");
        var prefabs=RailCatalogLightingAuthoring.Prefabs.Select(p=>AssetDatabase.LoadAssetAtPath<GameObject>(p).GetComponent<RailBlock>()).ToArray();
        var catalog=prefabs.Where(b=>b.id!="bridge-finale-v1").ToArray();var expected=new float[catalog.Length];
        for(int i=0;i<catalog.Length;i++){EditorSceneManager.OpenScene("Assets/DuckRails/Lighting/Blocks/"+catalog[i].id+"/"+catalog[i].id+"-bake.unity");LightProbes.GetInterpolatedProbe(new Vector3(-26,2,61),null,out var sh);expected[i]=sh[0,0];}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var guard=new GameObject("Offline sign-in guard");guard.SetActive(false);guard.AddComponent<LoginFlow>();
        var proof=new GameObject("Mine catalogue integration proof").AddComponent<RailCatalogLightingProof>();proof.catalog=catalog;proof.expectedRedL0=expected;proof.outputDirectory="output/quarry-mine/runtime-proof";
        proof.stationPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailStartingStationBuilder.PrefabPath).GetComponent<RailBlock>();proof.finalePrefab=prefabs.Single(b=>b.id=="bridge-finale-v1");
        var seen=new HashSet<string>();var seeds=new List<int>();while(seen.Count<catalog.Length){int best=Enumerable.Range(0,1000).OrderByDescending(s=>RailRoute.Choose("proof",catalog,s).blocks.Count(id=>!seen.Contains(id))).First();seeds.Add(best);foreach(var id in RailRoute.Choose("proof",catalog,best).blocks)seen.Add(id);}
        proof.seeds=seeds.ToArray();EditorSceneManager.SaveScene(scene,RailQuarryMine.Folder+"/RuntimeProof.unity");
    }
    const string Evidence="docs/validation/quarry-mine";
    static readonly List<string> lines=new();
    static void Check(bool yes,string text){lines.Add((yes?"PASS ":"FAIL ")+text);}
    static void Walk(Vector3 p,string label)
    {
        Check(Physics.Raycast(p+Vector3.up*.2f,Vector3.down,out var h,.5f,1<<6)&&Mathf.Abs(h.point.y-p.y)<.08f,"Floor "+label);
        Check(!Physics.CheckCapsule(p+Vector3.up*.5f,p+Vector3.up*1.65f,.32f,1<<6),"Body clearance "+label);
    }
    [MenuItem("Duck Rails/Authoring/Quarry Mine/Check geometry and navigation")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Use Edit Mode, finish bake");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=RailQuarryMine.BakeScene)EditorSceneManager.OpenScene(RailQuarryMine.BakeScene);
        Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Evidence);var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailQuarryMine.RootName);
        try
        {
            Check(b.id==RailQuarryMine.Id&&b.length==160&&root,"Mine identity and authored root");
            Check(b.track.Length==81&&b.track.Select((p,i)=>p==new Vector3(0,0,i*2)).All(v=>v),"Straight original track samples");
            Check(b.entry.localPosition==Vector3.zero&&b.exit.localPosition==new Vector3(0,0,160)&&b.stop.localPosition==new Vector3(0,0,80),"Entry exit and stop retained");
            foreach(float z in Enumerable.Range(0,81).Select(i=>i*2f))Walk(new Vector3(0,0,z==160?159.9f:z),"Rail "+z);
            foreach(float x in new[]{-47f,-43f,43f,47f})foreach(float z in new[]{1f,7f,40f,80f,120f,153f,159f})Walk(new Vector3(x,0,z),"Flat edge "+x+","+z);
            foreach(float z in new[]{1f,5f,155f,159f})foreach(float x in new[]{-40f,-20f,0f,20f,40f})Walk(new Vector3(x,0,z),"Flat seam "+x+","+z);
            for(int r=0;r<RailQuarryMine.Routes.Length;r++)
            {
                var path=RailQuarryMine.Routes[r];
                for(int i=1;i<path.Length;i++)
                {
                    var a=path[i-1];var c=path[i];int steps=Mathf.CeilToInt(Vector2.Distance(a,c));
                    for(int j=0;j<=steps;j++){var q=Vector2.Lerp(a,c,j/(float)steps);Walk(new Vector3(q.x,-6,q.y),"Tunnel "+r+" / "+i+" / "+j);}
                    var start=new Vector3(a.x,-5.3f,a.y);var end=new Vector3(c.x,-5.3f,c.y);
                    Check(!Physics.SphereCast(start,.3f,(end-start).normalized,out _,Vector3.Distance(start,end),1<<6),"Tunnel continuous sweep "+r+" / "+i);
                }
            }
            Walk(new Vector3(11,-6,76),"Office doorway");Walk(new Vector3(11,-6,79.5f),"Office interior");
            var haul=RailQuarryMine.Haul;
            for(int i=1;i<haul.Length;i++)
            {
                var d=haul[i]-haul[i-1];Check(Mathf.Abs(d.y)/new Vector2(d.x,d.z).magnitude<=1f/12+.001f,"Haul grade "+i);
                int steps=Mathf.CeilToInt(d.magnitude);for(int j=1;j<steps;j++){var q=Vector3.Lerp(haul[i-1],haul[i],j/(float)steps);Walk(q,"Haul "+i+" / "+j);}
            }
            Check(root.Find("West rim climbing ledges").childCount==22,"Climb-over rim with outer and inner shelves");
            foreach(Transform t in root.Find("West rim climbing ledges")){var c=t.GetComponent<BoxCollider>();Walk(new Vector3(c.bounds.center.x,c.bounds.max.y,c.bounds.center.z),"Climb shelf "+t.name);}
            Check(root.Find("Surface machinery and gravel/Crane")&&root.Find("Surface machinery and gravel/Loader")&&root.Find("Surface machinery and gravel/Excavator")&&root.Find("Surface machinery and gravel/Haul truck"),"Crane and heavy machinery");
            foreach(var c in root.Find("Surface machinery and gravel").GetComponentsInChildren<Collider>())Check(c.bounds.max.x<=-7||c.bounds.min.x>=7,"Machinery clear of railway "+c.name);
            foreach(var t in b.GetComponentsInChildren<Transform>().Where(t=>t.name=="Track rail"||t.name=="Sleeper"))Check(!t.GetComponent<Collider>(),"Visual-only rails "+t.position.z);
            Check(b.GetComponentsInChildren<Light>().Length==RailQuarryMine.LightCount&&b.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"Ten baked fixtures");
            foreach(var v in b.variations)
            {
                Check(v.coal.Length==6&&v.gold.Length==2&&v.enemies.Length==6,"Spawn counts retained");
                Check(v.coal.Count(t=>t.position.x<0)==3&&v.coal.Count(t=>t.position.x>0)==3,"Three coal per side");
                foreach(var t in v.coal.Concat(v.gold).Concat(v.enemies))Walk(t.position-new Vector3(0,t.position.y+6,0),"Spawn "+t.name+" "+t.position);
            }
            var graph=CrawlGraph.Load(b.graph);Check(graph!=null&&graph.NodeCount>100,"Prebaked graph present");
            var nodes=new List<int>();foreach(var route in RailQuarryMine.Routes)foreach(var p in route){int n=graph.FindNearestGroundNode(new Vector3(p.x,-5.8f,p.y),2);Check(n>=0,"Graph node at tunnel waypoint "+p);if(n>=0)nodes.Add(n);}
            if(nodes.Count>0)
            {
                var seen=new HashSet<int>{nodes[0]};var queue=new Queue<int>();queue.Enqueue(nodes[0]);while(queue.Count>0){int n=queue.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int next=graph.EdgeTarget[e];if(graph.GetNormal(next).y>.5f&&seen.Add(next))queue.Enqueue(next);}}
                Check(nodes.All(seen.Contains),"Ground-only graph connects all three branches and both pits");
            }
            Check(b.enemySurfaceRailExclusion.size == Vector3.zero,"No permanent railway navigation exclusion");
            foreach(float z in new[]{63f,82f,101f}){Walk(new Vector3(0,-6,z),"Under-track crossing "+z);Check(Physics.Raycast(new Vector3(0,-4,z),Vector3.up,out var hit,2,1<<6)&&Mathf.Abs(hit.point.y+3)<.05f,"Tunnel roof below intact railway "+z);}
            var old=b.transform.position;b.transform.position=new Vector3(0,0,390);foreach(float x in new[]{-2f,2f}){var p=new Vector3(x,.5f,470);Check(b.ConstrainEnemyPosition(p)==p,"Translated surface crossing remains allowed "+x);var q=new Vector3(x,-5.5f,470);Check(b.ConstrainEnemyPosition(q)==q,"Translated underground remains allowed "+x);}b.transform.position=old;Physics.SyncTransforms();
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())Check(f.sharedMesh.uv.Length==f.sharedMesh.vertexCount&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Texture/lightmap UVs "+f.name);
            if(lines.Any(l=>l.StartsWith("FAIL")))throw new Exception(string.Join("\n",lines.Where(l=>l.StartsWith("FAIL")).Take(30)));
            Debug.Log("MINE CHECKS: "+lines.Count+" passed");
        }
        finally{File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);}
    }
    [MenuItem("Duck Rails/Authoring/Quarry Mine/Capture mine")]
    public static void Capture()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish play/bake first");
        EditorSceneManager.OpenScene(RailQuarryMine.BakeScene);Directory.CreateDirectory("output/quarry-mine");
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.1f,.13f,.16f);cam.farClipPlane=500;
        var fill=new GameObject("Temporary review fill").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=1.3f;fill.transform.rotation=Quaternion.Euler(55,25,0);
        try
        {
            RenderSettings.fog=false;Shot("surface",new Vector3(-65,100,0),new Vector3(0,0,80),65);Shot("east-pit-crane",new Vector3(0,24,10),new Vector3(27,0,99),70);Shot("west-climb",new Vector3(-10,7,48),new Vector3(-26,-1,75),70);
            fill.enabled=false;Shot("office",new Vector3(11,-4.5f,73),new Vector3(11,-4.5f,81),80);Shot("under-track",new Vector3(-9,-4.5f,89.5f),new Vector3(8,-4.5f,75.3f),85);
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(fill.gameObject);EditorSceneManager.OpenScene(RailQuarryMine.BakeScene);}
        void Shot(string n,Vector3 eye,Vector3 target,float fov){cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.fieldOfView=fov;var rt=new RenderTexture(1600,1100,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes("output/quarry-mine/"+n+".png",tex.EncodeToPNG());}finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}}
    }
}
#endif
