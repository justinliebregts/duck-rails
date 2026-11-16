#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

// Scoped migration of the saved farm; never reruns the original block installer.
public static class RailFarmScreenshotFixes
{
    const string Output = "Assets/DuckRails/FarmSilos/ScreenshotFixes";
    static readonly List<string> Log = new();
    static Mesh Save(Mesh mesh,string name)
    {
        mesh.name=name;mesh.RecalculateBounds();
        if(!Unwrapping.GenerateSecondaryUVSet(mesh))throw new Exception("UV2 failed: "+name);
        var path=Output+"/"+name+".asset";var existing=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(existing){EditorUtility.CopySerialized(mesh,existing);Object.DestroyImmediate(mesh);return existing;}
        AssetDatabase.CreateAsset(mesh,path);return mesh;
    }
    static void EditFixture(Transform group, Vector3 center, Vector3 delta, bool remove)
    {
        foreach(var f in group.GetComponentsInChildren<MeshFilter>())
        {
            var old=f.sharedMesh;var v=old.vertices;var touched=new HashSet<int>();var tri=old.triangles;var keep=new List<int>();
            for(int i=0;i<tri.Length;i+=3)
            {
                bool inside=true;for(int j=0;j<3;j++)if(Vector3.Distance(f.transform.TransformPoint(v[tri[i+j]]),center)>.56f)inside=false;
                if(inside){for(int j=0;j<3;j++)touched.Add(tri[i+j]);if(remove)continue;}
                keep.AddRange(new[]{tri[i],tri[i+1],tri[i+2]});
            }
            if(touched.Count==0)continue;
            var mesh=Object.Instantiate(old);foreach(int i in touched)v[i]+=f.transform.InverseTransformVector(delta);
            mesh.vertices=v;mesh.triangles=keep.ToArray();f.sharedMesh=Save(mesh,"Fixture-"+Guid.NewGuid().ToString("N"));
        }
    }
    static void Mount(Transform root,Vector3 cap,Vector3 contact,Material material,int index)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name="Lantern mounting bracket "+index;g.layer=6;g.transform.SetParent(root,false);
        var end=contact+(contact-cap).normalized*.03f;g.transform.position=(cap+end)*.5f;g.transform.rotation=Quaternion.FromToRotation(Vector3.up,end-cap);
        g.transform.localScale=new Vector3(.10f,(end-cap).magnitude,.10f);Object.DestroyImmediate(g.GetComponent<Collider>());
        var f=g.GetComponent<MeshFilter>();f.sharedMesh=Save(Object.Instantiate(f.sharedMesh),"Mount-"+index);
        var r=g.GetComponent<MeshRenderer>();r.sharedMaterial=material;r.scaleInLightmap=.35f;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);
    }
    static float Side(Vector3 p,Vector3 a,Vector3 b)=>(b.x-a.x)*(p.z-a.z)-(b.z-a.z)*(p.x-a.x);
    static List<Vector3> Half(List<Vector3> poly,Vector3 a,Vector3 b,float sign,bool inside)
    {
        var output=new List<Vector3>();if(poly.Count==0)return output;
        var prev=poly[poly.Count-1];float pd=Side(prev,a,b)*sign*(inside?1:-1);
        foreach(var p in poly){float d=Side(p,a,b)*sign*(inside?1:-1);if((d>=0)!=(pd>=0))output.Add(Vector3.Lerp(prev,p,pd/(pd-d)));if(d>=0)output.Add(p);prev=p;pd=d;}return output;
    }
    static void FixPaths(Transform fields)
    {
        var occupied=new List<Vector3[]>();
        foreach(string name in new[]{"Farmyard link north","Winding orchard drive"})
        {
            var f=fields.Find(name).GetComponent<MeshFilter>();var source=f.sharedMesh;var v=source.vertices.Select(f.transform.TransformPoint).ToArray();var ix=source.triangles;
            var verts=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<ix.Length;i+=3)
            {
                var original=new[]{v[ix[i]],v[ix[i+1]],v[ix[i+2]]};var pieces=new List<List<Vector3>>{original.ToList()};
                foreach(var cut in occupied)
                {
                    if(original.Max(p=>p.x)<=cut.Min(p=>p.x)||original.Min(p=>p.x)>=cut.Max(p=>p.x)||original.Max(p=>p.z)<=cut.Min(p=>p.z)||original.Min(p=>p.z)>=cut.Max(p=>p.z))continue;
                    float sign=Mathf.Sign(Side(cut[2],cut[0],cut[1]));var next=new List<List<Vector3>>();
                    foreach(var piece in pieces){var remainder=piece;for(int e=0;e<3&&remainder.Count>=3;e++){var outside=Half(remainder,cut[e],cut[(e+1)%3],sign,false);if(outside.Count>=3)next.Add(outside);remainder=Half(remainder,cut[e],cut[(e+1)%3],sign,true);}}
                    pieces=next;
                }
                foreach(var poly in pieces)for(int j=1;j<poly.Count-1;j++)
                {if(Vector3.Cross(poly[j]-poly[0],poly[j+1]-poly[0]).sqrMagnitude<1e-10f)continue;int k=verts.Count;verts.AddRange(new[]{poly[0],poly[j],poly[j+1]}.Select(f.transform.InverseTransformPoint));triangles.AddRange(new[]{k,k+1,k+2});}
                occupied.Add(original);
            }
            var m=new Mesh();m.SetVertices(verts);m.SetTriangles(triangles,0);m.uv=verts.Select(p=>new Vector2(p.x,p.z)/3).ToArray();m.RecalculateNormals();f.sharedMesh=Save(m,name);
            Log.Add(name+": removed overlapping triangle footprints; retained world placement and terrain height.");
        }
    }
    public static void Apply()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Idle Rails required");
        Directory.CreateDirectory(Output);AssetDatabase.Refresh();Log.Clear();
        var root=PrefabUtility.LoadPrefabContents(RailFarmSilos.PrefabPath);
        try
        {
            var visual=root.transform.Find(RailFarmSilosAtlasProduction.Visual);if(!visual)throw new Exception("Expected old atlas");
            var lighting=root.GetComponent<RailBakedBlockLighting>();
            var originals=lighting.bindings.Where(b=>b.renderer&&!b.renderer.transform.IsChildOf(visual)).ToArray();
            foreach(var b in originals)b.renderer.enabled=true;lighting.bindings=originals;Object.DestroyImmediate(visual.gameObject);
            var env=root.transform.Find("Env");var farm=env.Find("Farm authored v3");var barn=farm.Find("Barn and silos");
            var play=env.Find("Northeast pond and playground/Playground towers and bridges");
            var slide=play.Find("Playground towers and bridges Slide yellow");
            foreach(var t in play.Cast<Transform>().Where(t=>t.name=="Static slide"||t.name=="Slide edge"))t.localPosition+=slide.localPosition;
            Log.Add("Aligned slide deck and both edge colliders with the existing +0.20m visual offset.");
            Object.DestroyImmediate(farm.Find("Fields and orchard/Abandoned tractor").gameObject);Log.Add("Removed outdoor abandoned tractor and its collision.");
            FixPaths(farm.Find("Fields and orchard"));
            var silo=barn.Find("Enterable grain silo");
            var route=new[]{new Vector3(15,0,90.5f),new Vector3(15,4,106),new Vector3(22,5.5f,106),new Vector3(22,6,110),new Vector3(15,6.5f,110),new Vector3(15,7.5f,98)};
            for(int i=0;i<5;i++)
            {
                var a=route[i];var b=route[i+1];var dir=Vector3.ProjectOnPlane(b-a,Vector3.up).normalized;
                if(i>0)a+=dir*1.05f;if(i<4)b-=dir*1.05f;else b-=dir*1.75f;
                var side=Vector3.Cross(Vector3.up,dir)*1.05f;
                var m=new Mesh{vertices=new[]{a-side,a+side,b+side,b-side,a-side-Vector3.up*.18f,a+side-Vector3.up*.18f,b+side-Vector3.up*.18f,b-side-Vector3.up*.18f},triangles=new[]{0,2,1,0,3,2,4,5,6,4,6,7,0,1,5,0,5,4,3,7,6,3,6,2,0,4,7,0,7,3,1,2,6,1,6,5}};
                m.RecalculateNormals();m.uv=m.vertices.Select(v=>new Vector2(v.x,v.z)/3).ToArray();m=Save(m,"Silo ramp "+(i+1));
                var t=silo.Find("Silo ramp "+(i+1));t.GetComponent<MeshFilter>().sharedMesh=m;t.GetComponent<MeshCollider>().sharedMesh=m;
            }
            Log.Add("Trimmed five ramp decks to meet turning/door landings without intersecting top faces.");
            var fixtures=root.GetComponentsInChildren<Light>().Where(l=>l.name=="Baked warm light").ToArray();
            int mounts=0;
            foreach(var l in fixtures)
            {
                var center=l.transform.position+Vector3.up*.25f;var group=l.transform.parent;
                bool remove=(group.name=="Silo shell and doorway"&&center.x>20)||(group.name=="Cart shed entrance lanterns"&&center.z<110);
                if(remove){EditFixture(group,center,Vector3.zero,true);Log.Add("Removed fixture and baked light at "+center);Object.DestroyImmediate(l.gameObject);continue;}
                if(group.name=="Cart shed entrance lanterns"){center=group.GetComponentInChildren<MeshFilter>().transform.TransformPoint(new Vector3(23,2.45f,111.5f));l.transform.position=center-Vector3.up*.25f;}
                if(group.name=="Barn doorway lanterns"&&center.z<70){var d=Vector3.down*.35f;EditFixture(group,center,d,false);l.transform.position+=d;center+=d;Log.Add("Lowered south barn lantern 0.35m.");}
                if(group.name=="Pond lanterns")continue;
                Vector3 cap=center+Vector3.up*.24f;Vector3 nearest=Vector3.zero;float distance=float.MaxValue;
                foreach(var c in root.GetComponentsInChildren<Collider>())
                {
                    if(c.isTrigger||!c.enabled||c.gameObject.layer!=6)continue;
                    // Raycast each collider directly: works in isolated prefab contents.
                    foreach(var d in new[]{Vector3.up,Vector3.left,Vector3.right,Vector3.forward,Vector3.back})
                        if(c.Raycast(new Ray(cap,d),out var hit,2.5f)&&hit.distance<distance){nearest=hit.point;distance=hit.distance;}
                }
                if(distance==float.MaxValue)throw new Exception("No mount structure near "+center);
                if(distance>.015f){Mount(group,cap,nearest,AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/FarmSilos/Silo metal.mat"),mounts++);Log.Add("Mounted "+center+" to "+nearest);}
            }
            var pond=env.Find("Northeast pond and playground/Pond water");var under=new GameObject("Pond underside collider - assign Water AudioSurface");under.layer=6;under.transform.SetParent(pond,false);under.transform.localPosition=Vector3.down*.025f;under.AddComponent<MeshCollider>().sharedMesh=pond.GetComponent<MeshFilter>().sharedMesh;
            Log.Add("Added pond-shaped solid collider 0.025m below water; AudioSurface assignment left to user.");
            lighting.bindings=lighting.bindings.Where(b=>b.renderer).ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,RailFarmSilos.PrefabPath);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();File.WriteAllLines("docs/validation/farm-silos-polish-2026-11-15/source-fixes.txt",Log);
    }
}
#endif
