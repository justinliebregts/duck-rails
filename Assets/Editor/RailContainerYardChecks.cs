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

public static class RailContainerYardChecks
{
    public const string Evidence="docs/validation/container-yard-v6";
    static readonly List<string> lines=new();
    static void Check(bool ok,string label){lines.Add((ok?"PASS ":"FAIL ")+label);}
    static void Clear(Vector3 a,Vector3 b,string label)
    {
        var d=b-a;Check(!Physics.CheckCapsule(a+Vector3.up*.55f,a+Vector3.up*1.6f,.35f,1<<6)&&!Physics.CapsuleCast(a+Vector3.up*.55f,a+Vector3.up*1.6f,.35f,d.normalized,d.magnitude,1<<6),label);
    }
    [MenuItem("Duck Rails/Authoring/Container Yard/Validate geometry")]
    public static void Run()
    {
        RailContainerYard.Guard();EditorSceneManager.OpenScene(RailContainerYard.BakeScene);Physics.SyncTransforms();lines.Clear();Directory.CreateDirectory(Evidence);
        try
        {
            var b=Object.FindFirstObjectByType<RailBlock>();var root=b.transform.Find(RailContainerYard.RootName);
            Check(b.id==RailContainerYard.Id&&b.length==160&&b.track.Length==81,"Original identity and block length");
            Check(b.track.Select((p,i)=>p==new Vector3(0,0,i*2)).All(v=>v)&&b.stop.localPosition==new Vector3(0,0,80),"Original straight track and stop");
            Check(b.enemyBounds==new Bounds(new Vector3(23,4,80),new Vector3(28,12,70)),"Existing east-only enemy bounds retained");
            Check(root.GetComponentsInChildren<Light>().Length==RailContainerYard.LightCount&&root.GetComponentsInChildren<Light>().All(l=>l.lightmapBakeType==LightmapBakeType.Baked),"16 baked lights only");
            var containers=root.GetComponentsInChildren<Transform>().Where(t=>t.Find("Corrugated steel shell")!=null).ToArray();
            Check(containers.Count(t=>t.name=="Open passage")==4&&containers.Count(t=>t.name=="Open search container")==2,"Six hollow ground containers");
            Check(containers.Count(t=>t.name.Contains("fallen container"))==3,"Three fallen containers");
            Check(containers.Count(t=>t.name=="Paired ground container")==42,"Double-depth ground pairs across the twelve bands");
            foreach(string side in new[]{"West yard","East yard"})
            {
                var yard=root.Find(side);Check(yard.Cast<Transform>().Count(t=>t.name.StartsWith("Row "))==6,side+" six rows");
                for(int i=1;i<=6;i++)
                {
                    var row=yard.Find("Row "+i);var bounds=row.GetComponentsInChildren<Collider>().Select(c=>c.bounds).ToArray();
                    Check(bounds.Max(q=>q.max.x)-bounds.Min(q=>q.min.x)>=29,side+" irregular band "+i+" fills at least 29m width");
                    Check(bounds.All(q=>q.min.x>=-42.1f&&q.max.x<=42.1f),side+" row "+i+" inside side shoulders");
                }
            }
            foreach(var t in containers.Where(t=>t.name=="Open passage"||t.name=="Open search container"))
            {
                float len=t.Find("Interior floor").GetComponent<BoxCollider>().size.x;
                var a=t.TransformPoint(new Vector3(t.name=="Open passage"?-len/2-.7f:-len/2+1,.16f,0));var end=t.TransformPoint(new Vector3(len/2+.7f,.16f,0));Clear(a,end,"Interior and end opening "+t.position);
                for(float x=-len/2+.5f;x<len/2;x+=1)Check(Physics.Raycast(t.TransformPoint(new Vector3(x,.4f,0)),Vector3.down,out var hit,.5f,1<<6)&&Mathf.Abs(hit.point.y-.16f)<.03f,"Interior floor support "+t.position+" / "+x);
            }
            foreach(int sign in new[]{-1,1})
            {
                Clear(new(sign*8,0,45),new(sign*8,0,115),"Rail-side return lane "+sign);Clear(new(sign*43.5f,0,45),new(sign*43.5f,0,115),"Outer return lane "+sign);
                float z=sign<0?72.5f:73;
                Clear(new(sign*10.6f,3.04f,z),new(sign*25.4f,3.04f,z),"Continuous low roof route "+sign);
                Clear(new(sign*18.6f,6.04f,z+3),new(sign*23.4f,6.04f,z+3),"Upper roof landing "+sign);
                for(float x=10.6f;x<=25.4f;x+=.8f)Check(Physics.Raycast(new(sign*x,3.2f,z),Vector3.down,out var hit,.4f,1<<6)&&Mathf.Abs(hit.point.y-3)<.04f,"Roof support "+sign+" / "+x);
                for(float x=18.6f;x<=23.4f;x+=.8f)Check(Physics.Raycast(new(sign*x,6.2f,z+3),Vector3.down,out var hit,.4f,1<<6)&&Mathf.Abs(hit.point.y-6)<.04f,"Upper landing support "+sign+" / "+x);
                GroundConnectivity(root,sign);
                foreach(var t in root.Find(sign<0?"West yard":"East yard").GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("Pallet step ")))
                {
                    var bounds=t.GetComponent<BoxCollider>().bounds;var foot=new Vector3(bounds.center.x,bounds.max.y+.04f,bounds.center.z);
                    Check(!Physics.CheckCapsule(foot+Vector3.up*.4f,foot+Vector3.up*1.6f,.28f,1<<6),"Pallet climb headroom "+sign+" / "+t.position);
                    Check(Physics.Raycast(foot+Vector3.up*.1f,Vector3.down,.2f,1<<6),"Pallet climb support "+t.position);
                }
            }
            var gantry=root.Find("Cantilever gantry crane");var truck=root.Find("Parked reach stacker");Check(gantry&&truck,"Both freight machines present");
            Check(gantry.GetComponentsInChildren<Collider>().All(c=>c.bounds.min.z>6&&c.bounds.max.z<45),"Gantry wholly in south loading apron");
            Check(!root.GetComponentsInChildren<Rigidbody>().Any(),"Containers and loading machines are static scenery");
            Check(gantry.GetComponentsInChildren<Collider>().Where(c=>c.name=="High loading boom").All(c=>c.bounds.min.y>=16),"Gantry overhead boom at least 16m clear");
            Check(truck.GetComponentsInChildren<Collider>().All(c=>c.bounds.max.x<-6),"Reach stacker and suspended cargo off railway");
            foreach(var c in root.GetComponentsInChildren<Collider>())
            {
                if(c.name=="Block ground")continue;var q=c.bounds;
                Check(q.min.x>=-48&&q.max.x<=48&&q.min.z>=6&&q.max.z<=154,"Flat seam reservation "+c.name);
                Check(q.max.x<=-6||q.min.x>=6||q.min.y>=16,"Railway 12m horizontal / 16m vertical clearance "+c.name);
            }
            foreach(float z in new[]{1f,159f})for(float x=-47;x<=47;x+=4)Check(Physics.Raycast(new(x,.5f,z),Vector3.down,out var hit,1,1<<6)&&Mathf.Abs(hit.point.y)<.01f,"Level seam "+x+" / "+z);
            foreach(var v in b.variations)foreach(var t in v.coal.Concat(v.gold))
            {
                Check(Physics.Raycast(t.position,Vector3.down,1,1<<6),"Loot supported "+t.position);Check(!Physics.CheckSphere(t.position+Vector3.up*.2f,.45f,1<<6),"Loot pickup space "+t.position);
            }
            var graph=CrawlGraph.Load(b.graph);
            foreach(var v in b.variations)foreach(var t in v.enemies.Concat(v.coal.Where(t=>t.localPosition.x>0)))Check(graph.FindNearestGroundNode(t.localPosition,2)>=0,"East loot/enemy baked navigation "+t.localPosition);
            int entry=graph.FindNearestGroundNode(b.variations[0].enemies[0].localPosition,2);var reached=new HashSet<int>();var queue=new Queue<int>();if(entry>=0){reached.Add(entry);queue.Enqueue(entry);}
            while(queue.Count>0){int n=queue.Dequeue();for(int e=graph.EdgeStart[n];e<graph.EdgeStart[n+1];e++){int next=graph.EdgeTarget[e];if(reached.Add(next))queue.Enqueue(next);}}
            foreach(var t in b.variations[0].coal.Where(t=>t.localPosition.x>0).Concat(b.variations[0].enemies))Check(reached.Contains(graph.FindNearestGroundNode(t.localPosition,2)),"Connected east ground/interior/roof graph "+t.localPosition);
            foreach(var f in root.GetComponentsInChildren<MeshFilter>())Check(f.sharedMesh&&f.sharedMesh.uv.Length==f.sharedMesh.vertexCount&&f.sharedMesh.uv2.Length==f.sharedMesh.vertexCount,"Material and lightmap UVs "+f.name);
            var build=typeof(RailWeek3Builder).GetMethod("BuildBlock",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic);var kept=(RailBlock)build.Invoke(null,new object[]{4});Check(kept.transform.Find(RailContainerYard.RootName),"Catalogue rebuild preserves authored yard");
            var fail=lines.Where(x=>x.StartsWith("FAIL")).ToArray();if(fail.Length>0)throw new Exception(fail.Length+" failures\n"+string.Join("\n",fail.Take(30)));Debug.Log("CONTAINER YARD: "+lines.Count+" geometry checks passed");
        }
        finally{File.WriteAllLines(Evidence+"/geometry-checks.txt",lines);}
    }
    static void GroundConnectivity(Transform root,int sign)
    {
        // Independent collision raster: walk from the rail-side entrance to every ground interior and both outer exits.
        const int nx=75,nz=141;var free=new bool[nx*nz];var reached=new bool[free.Length];var queue=new Queue<int>();
        Vector3 Point(int i)=>new(sign*(7+i%nx*.5f),0,44+i/nx*.5f);
        for(int i=0;i<free.Length;i++){var p=Point(i);free[i]=!Physics.CheckCapsule(p+Vector3.up*.55f,p+Vector3.up*1.6f,.35f,1<<6);}
        int start=2+2*nx;if(free[start]){queue.Enqueue(start);reached[start]=true;}
        while(queue.Count>0)
        {
            int n=queue.Dequeue(),x=n%nx,z=n/nx;foreach(var d in new[]{new Vector2Int(1,0),new(-1,0),new(0,1),new(0,-1)})
            {
                int xx=x+d.x,zz=z+d.y;if(xx<0||xx>=nx||zz<0||zz>=nz)continue;int next=xx+zz*nx;if(reached[next]||!free[next])continue;
                var a=Point(n);var delta=Point(next)-a;if(Physics.CapsuleCast(a+Vector3.up*.55f,a+Vector3.up*1.6f,.35f,delta.normalized,.5f,1<<6))continue;
                reached[next]=true;queue.Enqueue(next);
            }
        }
        foreach(var t in root.Find(sign<0?"West yard":"East yard").GetComponentsInChildren<Transform>().Where(t=>t.name=="Open passage"||t.name=="Open search container"))
        {int x=Mathf.RoundToInt((Mathf.Abs(t.position.x)-7)*2),z=Mathf.RoundToInt((t.position.z-44)*2);Check(reached[x+z*nx],"Ground route reaches interior "+t.position);}
        foreach(float z in new[]{46f,112f}){int index=73+Mathf.RoundToInt((z-44)*2)*nx;Check(reached[index],"Connected outer return exit "+sign+" / "+z);}
        int occupied=0,total=0;for(float x=10;x<=42;x+=1)for(float z=48;z<=111;z+=1){total++;if(Physics.CheckSphere(new(sign*x,.75f,z),.1f,1<<6))occupied++;}
        float density=(float)occupied/total;Check(density>.4f&&density<.75f,"Ground cluster density "+sign+" = "+density.ToString("P1"));
        var centre=sign>0?new Vector3(18,1,58.25f):new Vector3(-18,1,69.25f);
        bool f=Physics.Raycast(centre,Vector3.forward,out var ahead,10,1<<6),back=Physics.Raycast(centre,Vector3.back,out var behind,10,1<<6);
        Check(f&&back&&ahead.distance+behind.distance>=3&&ahead.distance+behind.distance<=5,"Measured tight aisle between 3 and 5m "+sign);
        var paired=root.Find(sign<0?"West yard":"East yard").GetComponentsInChildren<Transform>().Where(t=>t.name=="Offset upper stack"||t.name=="Upper roof landing");
        foreach(var t in paired)
        {
            float length=t.Find("Closed container collision").GetComponent<BoxCollider>().size.x;
            foreach(float x in new[]{-length/2+.25f,length/2-.25f})foreach(float z in new[]{-1.2f,1.2f})
                Check(Physics.Raycast(t.TransformPoint(new Vector3(x,.15f,z)),Vector3.down,out var hit,.3f,1<<6)&&Mathf.Abs(hit.point.y-t.position.y)<.02f,"Upper box supported "+t.position+" / "+x+" / "+z);
        }
    }
    public static void Capture()
    {
        RailContainerYard.Guard();EditorSceneManager.OpenScene(RailContainerYard.BakeScene);Directory.CreateDirectory("output/container-yard-v6");
        var go=new GameObject("Temporary review camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new(.14f,.18f,.2f);cam.farClipPlane=500;
        var sun=new GameObject("Temporary review light").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=3;sun.lightmapBakeType=LightmapBakeType.Realtime;sun.renderMode=LightRenderMode.ForcePixel;sun.transform.rotation=Quaternion.Euler(48,30,0);
        try
        {
            RenderSettings.fog=false;RenderSettings.sun=sun;RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.white;RenderSettings.ambientIntensity=1.3f;
            var sh=new UnityEngine.Rendering.SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.8f,.8f,.8f));RenderSettings.ambientProbe=sh;
            foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)){r.lightmapIndex=-1;r.lightProbeUsage=UnityEngine.Rendering.LightProbeUsage.Off;}
            Shot("overview",new(-72,85,-8),new(0,2,80));Shot("maze",new(25,1.2f,57.5f),new(25,1.2f,70));Shot("interior",new(11,.75f,62),new(25,.8f,62));Shot("roof",new(29,7.5f,69),new(18,4.5f,76));Shot("gantry",new(-8,9,13),new(22,12,29));Shot("reach-stacker",new(-9,5,40),new(-22,4,27));
            cam.orthographic=true;cam.orthographicSize=84;Shot("top-down",new(0,130,80),new(0,0,80));
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(sun.gameObject);EditorSceneManager.OpenScene(RailContainerYard.BakeScene);}
        void Shot(string name,Vector3 eye,Vector3 target)
        {
            cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye,cam.orthographic?Vector3.forward:Vector3.up);cam.fieldOfView=65;var rt=new RenderTexture(1600,1000,24);var old=RenderTexture.active;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);
            try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("output/container-yard-v6/"+name+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
    public static void PrepareProof()
    {
        RailContainerYard.Guard();var ids=new[]{RailContainerYard.Id,"convenience-store-v1","roadside-motel-v1","lumber-yard-v1","electrical-substation-v1"};
        var catalog=ids.Select(id=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/"+id+".prefab").GetComponent<RailBlock>()).ToArray();var expected=new float[catalog.Length];
        for(int i=0;i<catalog.Length;i++){EditorSceneManager.OpenScene("Assets/DuckRails/Lighting/Blocks/"+catalog[i].id+"/Bake.unity");LightProbes.GetInterpolatedProbe(new Vector3(-26,2,61),null,out var sh);expected[i]=sh[0,0];}
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);var guard=new GameObject("Offline sign-in guard");guard.SetActive(false);guard.AddComponent<LoginFlow>();
        var proof=new GameObject("Container yard route integration proof").AddComponent<RailCatalogLightingProof>();proof.catalog=catalog;proof.expectedRedL0=expected;proof.seeds=new[]{11,43};proof.outputDirectory=Evidence+"/runtime";
        proof.stationPrefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailStartingStationBuilder.PrefabPath).GetComponent<RailBlock>();proof.finalePrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/bridge-finale-v1.prefab").GetComponent<RailBlock>();
        EditorSceneManager.SaveScene(scene,RailContainerYard.Folder+"/RouteProof.unity");Debug.Log("Container yard two-round offline route proof ready");
    }
}
#endif
