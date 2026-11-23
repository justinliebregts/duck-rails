#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;
using Object=UnityEngine.Object;

public static class RailCampgroundChecks
{
    [MenuItem("Duck Rails/Authoring/Campground/Check terrain and geometry")]
    public static void Run()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish Play/bake first");
        var root=PrefabUtility.LoadPrefabContents(RailCampgroundGreybox.PrefabPath);
        var log=new List<string>();int count=0;
        void Check(bool value,string name){if(!value)throw new Exception(name);count++;log.Add("PASS "+name);}
        try
        {
            var b=root.GetComponent<RailBlock>();var authored=RailBlock.FindEnvironment(root.transform,RailCampgroundGreybox.RootName);
            Check(authored!=null,"authored campground exists");
            Check(RailBlock.FindEnvironment(root.transform,"Block ground")==null,"old flat ground collider removed");
            var ground=authored.Find("Sculpted ground").GetComponent<MeshCollider>();
            var verts=ground.sharedMesh.vertices;
            Check(verts.Where(v=>Mathf.Abs(v.x)>=42||v.z<=6||v.z>=154).All(v=>Mathf.Abs(v.y)<.0001f),"all vertices across six-metre perimeter exactly level");
            Check(verts.Where(v=>Mathf.Abs(v.x)>=41||v.z<=7||v.z>=153).All(v=>Mathf.Abs(v.y)<.0001f),"perimeter slopes zero including inner transition row");
            Check(verts.Where(v=>v.x>=-4&&v.x<=10).All(v=>Mathf.Abs(v.y)<.0001f),"rail corridor remains level across expanded scenery");
            Check(verts.Min(v=>v.y)<=-1.99f&&verts.Max(v=>v.y)>=1.99f,"real lake depression and two-metre terrace");
            var lake=authored.Find("Lake water (no collision)");Check(lake.GetComponent<Collider>()==null,"water has no walking collider");
            Check(lake.GetComponent<MeshFilter>().sharedMesh.bounds.min.x> -42,"lake clear of flat seam");
            Check(ground.Raycast(new Ray(new Vector3(-28,3,88),Vector3.down),out var bed,10)&&bed.point.y<-.8f,"ray reaches lake bed below water");
            foreach(var p in b.track)Check(ground.Raycast(new Ray(p+Vector3.up,Vector3.down),out var hit,2)&&Mathf.Abs(hit.point.y)<.001f,"track ground at "+p);
            foreach(var a in new[]{("Cabin 1",1f),("Cabin 2",2f),("Cabin 3",1f),("Reception",0f),("Washrooms",0f)})
            {
                var cabin=authored.Find(a.Item1);Check(cabin!=null,a.Item1+" exists");
                var slab=cabin.Find("Foundation").GetComponent<BoxCollider>();Check(Mathf.Abs(slab.bounds.max.y-a.Item2)<.001f,a.Item1+" level pad");
                var roof=cabin.Find("Roof").GetComponent<BoxCollider>();Check(roof.bounds.min.y-slab.bounds.max.y>=3,a.Item1+" headroom above 3 m");
            }
            var collisions=root.GetComponentsInChildren<Collider>().Where(c=>!c.isTrigger&&c.gameObject.layer==6).ToArray();
            var expansion=authored.Find("Expanded campground V2");
            Check(expansion!=null,"expanded campground installed");
            Check(expansion.Find("Woodland clusters").childCount>=55,"at least 55 clustered trees");
            foreach(var name in new[]{"Southwest tent clearing","Southeast camper pitch","Northeast woodland pitches","Canoe beach","Rocky overlook","Gully footbridge","Fallen tree scene","Campground signs and furnishings"})Check(expansion.Find(name)!=null,name+" installed");
            var bridge=expansion.Find("Gully footbridge/Bridge deck").GetComponent<BoxCollider>();
            Check(bridge.bounds.max.y==1&&ground.Raycast(new Ray(new Vector3(20,3,113),Vector3.down),out var gully,6)&&gully.point.y<.3f,"bridge spans real lowered gully");
            var probe=new GameObject("Clearance probe");var sphere=probe.AddComponent<SphereCollider>();sphere.radius=.4f;
            try
            {
                int samples=0;float steepest=0;
                foreach(var path in RailCampgroundGreybox.Paths)for(int i=1;i<path.Length;i++)
                {
                    var a=path[i-1];var d=path[i]-a;int n=Mathf.CeilToInt(new Vector2(d.x,d.z).magnitude*2);float prev=0;
                    for(int j=0;j<=n;j++)
                    {
                        var q=Vector3.Lerp(a,path[i],(float)j/n);var ray=new Ray(new Vector3(q.x,8,q.z),Vector3.down);
                        Check(ground.Raycast(ray,out var h,12),"path ground "+samples);
                        if(bridge.Raycast(ray,out var deck,12)&&deck.point.y>h.point.y)h=deck;
                        if(j>0)steepest=Mathf.Max(steepest,Mathf.Abs(h.point.y-prev)/(new Vector2(d.x,d.z).magnitude/n));
                        prev=h.point.y;
                        var blocker=collisions.FirstOrDefault(c=>Physics.ComputePenetration(sphere,h.point+Vector3.up*.55f,Quaternion.identity,c,c.transform.position,c.transform.rotation,out _,out _));
                        Check(!blocker,"path body clearance "+samples+" at "+h.point+(blocker?" intersects "+blocker.name:""));
                        samples++;
                    }
                }
                log.Add("Measured maximum path centreline grade="+steepest.ToString("F3")+" (metres rise/metre run)");
                Check(steepest<=.18f,"all sampled primary routes below 10.3 degrees; gentler target remains tuning");
            }
            finally{Object.DestroyImmediate(probe);}
            Check(b.GetComponent<RailBakedBlockLighting>().bindings.Length>100,"lighting bindings exported");
            Check(root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.name=="Timber wall"||r.name=="Sculpted ground").All(r=>AssetDatabase.GetAssetPath(r.sharedMaterial).StartsWith(RailCampgroundGreybox.Folder)&&r.sharedMaterial.mainTexture!=null),"Quackville textured terrain and cabins");
            var finish=authored.Find(RailCampgroundFinish.RootName);
            if(finish)
            {
                var art=finish.GetComponentsInChildren<MeshRenderer>().Where(r=>!r.GetComponent<TMPro.TMP_Text>()).ToArray();
                Check(art.Length>0 && art.All(r=>r.sharedMaterial && r.sharedMaterial.name=="Lakeside atlas"),"finished mesh sections share the Pine Lake atlas");
                Check(art.All(r=>r.GetComponent<MeshFilter>().sharedMesh.uv2.Length>0),"all finish meshes have secondary UVs");
                var roofs=art.Where(r=>r.name.StartsWith("Gabled roofs")).ToArray();
                foreach(var pos in new[]{new Vector3(34,5,76),new Vector3(34,6,99),new Vector3(20,5,121),new Vector3(18,4,27),new Vector3(34,4,50),new Vector3(-11,4,57.5f),new Vector3(-11,4,97)})
                    Check(roofs.Any(r=>r.bounds.Contains(pos)),"gable positioned over original building "+pos);
                Check(finish.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>t.font && t.font.name=="CutePixel SDF"),"Pine Lake trail signs use CutePixel SDF");
                Check(root.GetComponentsInChildren<Light>(true).Count(l=>l.lightmapBakeType==LightmapBakeType.Baked)==RailCampgroundFinish.LightCount,"19 baked interior, porch and ember fixtures");
                var shellMats=authored.GetComponentsInChildren<MeshRenderer>().SelectMany(r=>r.sharedMaterials).Where(m=>m && AssetDatabase.GetAssetPath(m).Contains("/Finish/Surfaces/")).Distinct();
                Check(shellMats.Count()==11,"11 campground-local Quackville surface variants");
            }
            log.Add("TOTAL "+count+" checks. Editor geometry only; headset and multiplayer acceptance pending.");
            string folder=authored.Find(RailCampgroundFinish.RootName)?"docs/validation/campground-finish":"docs/validation/campground-greybox/v2";Directory.CreateDirectory(folder);File.WriteAllLines(folder+"/geometry-checks.txt",log);Debug.Log("CAMPGROUND: "+count+" checks passed");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    [MenuItem("Duck Rails/Authoring/Campground/Capture finished Pine Lake review")]
    public static void CaptureFinish()
    {
        if (EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new InvalidOperationException("Finish the current play session or bake before capturing.");
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(RailCampgroundGreybox.PrefabPath);
        if (!RailBlock.FindEnvironment(prefab.transform,RailCampgroundGreybox.RootName+"/"+RailCampgroundFinish.RootName))
            throw new InvalidOperationException("Install the Pine Lake finish before capturing.");
        var preview=new PreviewRenderUtility();
        var oldPipeline=QualitySettings.renderPipeline;
        var pipeline=Object.Instantiate(AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset"));
        const string output="output/campground-finish";
        try
        {
            var model=Object.Instantiate(prefab);
            preview.AddSingleGO(model);
            foreach(var fixture in model.GetComponentsInChildren<Light>(true)) fixture.enabled=false;
            foreach(var sign in model.GetComponentsInChildren<TMPro.TMP_Text>(true)) sign.ForceMeshUpdate(true,true);
            var settings=new SerializedObject(pipeline);
            settings.FindProperty("m_ShadowDistance").floatValue=440;
            settings.FindProperty("m_ShadowCascadeCount").intValue=2;
            settings.FindProperty("m_MainLightShadowmapResolution").intValue=4096;
            settings.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.renderPipeline=pipeline;
            var sun=preview.lights[0];
            sun.type=LightType.Directional;
            sun.intensity=1.65f;
            sun.shadows=LightShadows.Soft;
            preview.lights[1].intensity=.35f;
            preview.lights[1].transform.rotation=Quaternion.Euler(55,-140,0);
            sun.transform.rotation=Quaternion.Euler(40,44,0);
            preview.ambientColor=new Color(.43f,.47f,.52f);
            var camera=preview.camera;
            camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=new Color(.15f,.20f,.24f);

            camera.farClipPlane=550;
            camera.useOcclusionCulling=false;
            Directory.CreateDirectory(output);
            Shot("whole-block",new Vector3(-128,146,-90),new Vector3(0,1,80),true,70);
            Shot("site-plan",new Vector3(0,160,80),new Vector3(0,0,80),true,86);
            Shot("arrival",new Vector3(-48,29,0),new Vector3(4,1,32),false,55);
            Shot("cabin-terraces",new Vector3(-3.5f,8.7f,82),new Vector3(30,2.5f,94),false,55);
            Shot("canoe-beach",new Vector3(-11,9.2f,105),new Vector3(-31,1.6f,121),false,63);
            void Shot(string name,Vector3 eye,Vector3 target,bool ortho,float lens)
            {
                camera.transform.SetPositionAndRotation(eye,Quaternion.LookRotation(target-eye,name=="site-plan"?Vector3.forward:Vector3.up));
                camera.orthographic=ortho;
                if(ortho) camera.orthographicSize=lens;
                else camera.fieldOfView=lens;
                preview.BeginStaticPreview(new Rect(0,0,1920,1440));
                preview.Render(true);
                var texture=preview.EndStaticPreview();
                try { File.WriteAllBytes(output+"/"+name+".png",texture.EncodeToPNG()); }
                finally { Object.DestroyImmediate(texture); }
            }
        }
        finally
        {
            QualitySettings.renderPipeline=oldPipeline;
            Object.DestroyImmediate(pipeline);
            preview.Cleanup();
        }
    }
    [MenuItem("Duck Rails/Authoring/Campground/Capture rough V2 review views")]
    public static void Capture()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish Play/bake first");
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)throw new Exception("Save scene first");
        EditorSceneManager.OpenScene(RailCampgroundGreybox.BakeScene);
        var go=new GameObject("Temporary campground camera");var cam=go.AddComponent<Camera>();cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.14f,.18f,.22f);cam.farClipPlane=300;
        var fill=new GameObject("Temporary review sunlight").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=1.2f;fill.transform.rotation=Quaternion.Euler(48,-30,0);
        bool fog=RenderSettings.fog;var ambient=RenderSettings.ambientLight;var mode=RenderSettings.ambientMode;
        RenderSettings.fog=false;RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.5f,.5f,.5f);
        Directory.CreateDirectory("output/campground-greybox/v2");
        try
        {
            Shot("overview",new Vector3(-88,96,10),new Vector3(0,0,80),true,66);
            Shot("arrival-campsites",new Vector3(-12,8,16),new Vector3(-25,1,32),false,70);
            Shot("north-shore",new Vector3(-8,8,140),new Vector3(-26,0,111),false,70);
            Shot("lake-and-dock",new Vector3(-7,2,72),new Vector3(-29,-.3f,89),false,65);
            Shot("cabin-terraces",new Vector3(10,.6f,72),new Vector3(29,2,94),false,70);
        }
        finally{Object.DestroyImmediate(go);Object.DestroyImmediate(fill.gameObject);RenderSettings.fog=fog;RenderSettings.ambientLight=ambient;RenderSettings.ambientMode=mode;EditorSceneManager.OpenScene(RailCampgroundGreybox.BakeScene);}
        void Shot(string name,Vector3 eye,Vector3 target,bool ortho,float size)
        {
            cam.transform.position=eye;cam.transform.rotation=Quaternion.LookRotation(target-eye);cam.orthographic=ortho;cam.orthographicSize=size;cam.fieldOfView=size;
            var rt=new RenderTexture(1600,1100,24);var old=RenderTexture.active;cam.targetTexture=rt;var tex=new Texture2D(1600,1100,TextureFormat.RGB24,false);
            try{cam.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1600,1100),0,0);tex.Apply();File.WriteAllBytes("output/campground-greybox/v2/"+name+".png",tex.EncodeToPNG());}
            finally{cam.targetTexture=null;RenderTexture.active=old;Object.DestroyImmediate(tex);rt.Release();Object.DestroyImmediate(rt);}
        }
    }
}
#endif

