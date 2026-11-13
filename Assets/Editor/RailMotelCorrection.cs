#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class RailMotelCorrection
{
    const string Folder="Assets/DuckRails/MotelLeisure/Corrections";
    static bool Inside(Vector3 p,Vector3 a,Vector3 b,Vector3 c)
    {
        float Cross(Vector3 u,Vector3 v)=>u.x*v.z-u.z*v.x;
        float x=Cross(b-a,p-a),y=Cross(c-b,p-b),z=Cross(a-c,p-c);
        return (x>=-.000001f&&y>=-.000001f&&z>=-.000001f)||(x<=.000001f&&y<=.000001f&&z<=.000001f);
    }
    public static int ReversedSides(Mesh mesh,bool repair)
    {
        var v=mesh.vertices;var tr=mesh.triangles;var top=new List<int>();
        for(int i=0;i<tr.Length;i+=3)if(Vector3.Cross(v[tr[i+1]]-v[tr[i]],v[tr[i+2]]-v[tr[i]]).normalized.y>.9f)top.Add(i);
        int reversed=0;
        for(int i=0;i<tr.Length;i+=3)
        {
            var a=v[tr[i]];var b=v[tr[i+1]];var c=v[tr[i+2]];var n=Vector3.Cross(b-a,c-a).normalized;if(Mathf.Abs(n.y)>.1f)continue;
            var outside=(a+b+c)/3+n*.01f;
            if(!top.Any(j=>Inside(outside,v[tr[j]],v[tr[j+1]],v[tr[j+2]])))continue;
            reversed++;if(repair)(tr[i+1],tr[i+2])=(tr[i+2],tr[i+1]);
        }
        if(repair&&reversed>0){mesh.triangles=tr;mesh.RecalculateNormals();mesh.RecalculateTangents();EditorUtility.SetDirty(mesh);}
        return reversed;
    }
    static Mesh Wheel()
    {
        string path=Folder+"/Ten segment car wheel.asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(m)return m;
        var v=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
        void Tri(Vector3 a,Vector3 b,Vector3 c){int n=v.Count;v.AddRange(new[]{a,b,c});uv.AddRange(new[]{a,b,c}.Select(p=>new Vector2(p.x,p.z)));tris.AddRange(new[]{n,n+1,n+2});}
        for(int i=0;i<10;i++)
        {
            float a=i*Mathf.PI/5,b=(i+1)*Mathf.PI/5;var lo=new Vector3(Mathf.Cos(a)*.5f,-1,Mathf.Sin(a)*.5f);var next=new Vector3(Mathf.Cos(b)*.5f,-1,Mathf.Sin(b)*.5f);var hi=lo+Vector3.up*2;var nh=next+Vector3.up*2;
            Tri(lo,hi,nh);Tri(lo,nh,next);Tri(Vector3.up,nh,hi);Tri(Vector3.down,lo,next);
        }
        m=new Mesh{name="Ten segment car wheel"};m.SetVertices(v);m.SetTriangles(tris,0);m.SetUVs(0,uv);m.RecalculateNormals();m.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(m);AssetDatabase.CreateAsset(m,path);return m;
    }
    static Transform Box(Transform parent,string name,Vector3 pos,Vector3 scale,Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.layer=6;
        var mesh=Object.Instantiate(AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/Cube Block UV.asset"));var v=mesh.vertices;var norm=mesh.normals;var uv=new Vector2[v.Length];
        for(int i=0;i<v.Length;i++){var p=g.transform.TransformPoint(v[i]);uv[i]=(Mathf.Abs(norm[i].y)>.5f?new Vector2(p.x,p.z):Mathf.Abs(norm[i].x)>.5f?new Vector2(p.z,p.y):new Vector2(p.x,p.y))/3;}
        mesh.uv=uv;AssetDatabase.CreateAsset(mesh,Folder+"/"+parent.name+" "+name+".asset");g.GetComponent<MeshFilter>().sharedMesh=mesh;g.GetComponent<MeshRenderer>().sharedMaterial=mat;g.GetComponent<MeshRenderer>().scaleInLightmap=.3f;GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI);Undo.RegisterCreatedObjectUndo(g,"Motel feedback");return g.transform;
    }
    public static void Art()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Duck Rails Edit Mode required");
        var s=PrefabStageUtility.GetCurrentPrefabStage();if(s==null||s.assetPath!=RailMotelGreybox.PrefabPath||s.scene.isDirty)throw new Exception("Open clean saved motel prefab first");
        var root=s.prefabContentsRoot;Undo.RegisterFullObjectHierarchyUndo(root,"Motel feedback");int flipped=0;
        foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Unique putting outline"||f.name=="Joined white rink wall"||f.name=="Joined blue coping"))
        {Undo.RecordObject(f.sharedMesh,"Correct outward faces");flipped+=ReversedSides(f.sharedMesh,true);var c=f.GetComponent<MeshCollider>();if(c){c.sharedMesh=null;c.sharedMesh=f.sharedMesh;}}
        var matPath=Folder+"/Ceiling emissive white.mat";var glow=AssetDatabase.LoadAssetAtPath<Material>(matPath);
        if(!glow){glow=new Material(AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/MotelLeisure/Finish/White painted concrete.mat"));glow.SetColor("_BaseColor",Color.white);glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",Color.white*3);glow.SetTexture("_EmissionMap",glow.mainTexture);glow.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;AssetDatabase.CreateAsset(glow,matPath);}
        foreach(var t in root.GetComponentsInChildren<Transform>().Where(t=>t.name=="Interior study light"))
        {
            var fixture=t.Find("Fixture");float ceiling=t.parent.name=="Floor 1"?3.4f:7.2f;var p=t.position;p.y+=ceiling-fixture.GetComponent<Renderer>().bounds.max.y;t.position=p;
            if(t.GetComponentInChildren<Light>())fixture.GetComponent<Renderer>().sharedMaterial=glow;
        }
        foreach(var l in root.GetComponentsInChildren<Light>().Where(l=>l.name=="Streetlight baked")){l.transform.rotation=Quaternion.LookRotation(Vector3.down,Vector3.forward);l.color=new Color(1,.97f,.93f);}
        var wheel=Wheel();foreach(var f in root.GetComponentsInChildren<MeshFilter>().Where(f=>f.name=="Rubber wheel"))f.sharedMesh=wheel;
        var pool=root.transform.Find("Env/"+RailMotelLeisure.Root+"/Municipal pool");
        if(!pool.Find("Changing bench 1"))
        {
            foreach(var t in pool.Cast<Transform>().Where(t=>(t.name.StartsWith("Bench ")||t.name.StartsWith("Chair"))&&t.localPosition.z>91&&t.localPosition.x< -23).ToArray())Undo.DestroyObjectImmediate(t.gameObject);
            var wood=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/MotelLeisure/Finish/Weathered timber.mat");var metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/MotelLeisure/Finish/Dull textured steel.mat");int index=0;
            foreach(float z in new[]{100f,103.5f,107f})foreach(float x in new[]{-37f,-28f})
            {var bench=new GameObject("Changing bench "+ ++index).transform;bench.SetParent(pool,false);bench.localPosition=new Vector3(x,.16f,z);Box(bench,"Seat",new Vector3(0,.55f,0),new Vector3(3,.16f,.65f),wood);Box(bench,"Back",new Vector3(0,1,.36f),new Vector3(3,.65f,.12f),wood);foreach(float dx in new[]{-1.15f,1.15f})Box(bench,dx<0?"Left support":"Right support",new Vector3(dx,.24f,0),new Vector3(.13f,.48f,.6f),metal);}
        }
        PrefabUtility.SaveAsPrefabAsset(root,s.assetPath);AssetDatabase.SaveAssets();Debug.Log("Repaired "+flipped+" reversed side triangles; mounted ceiling fixtures; down-facing streetlights; eight 40-triangle wheels; six benches");
    }
}
#endif
