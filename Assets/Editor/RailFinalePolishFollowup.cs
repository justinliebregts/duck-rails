#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using Object = UnityEngine.Object;

// Applies only the five authorized follow-ups to the current, user-edited prefab.
public static class RailFinalePolishFollowup
{
    const string Path = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";
    const string Folder = "Assets/DuckRails/FinaleFort/Polish/";
    static Mesh Save(string name, Mesh m, bool unwrap = true)
    {
        m.name=name; m.RecalculateNormals(); m.RecalculateBounds();
        if(unwrap && !Unwrapping.GenerateSecondaryUVSet(m)) throw new Exception("UV unwrap failed: "+name);
        var existing=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+name+".asset");
        if(existing){EditorUtility.CopySerialized(m,existing);Object.DestroyImmediate(m);EditorUtility.SetDirty(existing);return existing;}
        AssetDatabase.CreateAsset(m,Folder+name+".asset");return m;
    }
    static Mesh SubdivideGround(MeshFilter f)
    {
        var src=f.sharedMesh;var vertices=src.vertices;var uv=src.uv;var uv2=src.uv2;
        if(uv2.Length!=vertices.Length)throw new Exception("Missing ground lightmap UVs");
        var v=new List<Vector3>();var u=new List<Vector2>();var light=new List<Vector2>();var triangles=new List<int>();
        var sourceTriangles=src.triangles;
        void Triangle(Vector3 a,Vector3 b,Vector3 c,Vector2 ua,Vector2 ub,Vector2 uc,Vector2 la,Vector2 lb,Vector2 lc,int depth)
        {
            float ab=f.transform.TransformVector(b-a).sqrMagnitude,bc=f.transform.TransformVector(c-b).sqrMagnitude,ca=f.transform.TransformVector(a-c).sqrMagnitude;
            if(Mathf.Max(ab,bc,ca)>32 && depth<16)
            {
                if(bc>ab && bc>=ca){Triangle(b,c,a,ub,uc,ua,lb,lc,la,depth);return;}
                if(ca>ab && ca>bc){Triangle(c,a,b,uc,ua,ub,lc,la,lb,depth);return;}
                var mid=(a+b)*.5f;var um=(ua+ub)*.5f;var lm=(la+lb)*.5f;
                Triangle(a,mid,c,ua,um,uc,la,lm,lc,depth+1);Triangle(mid,b,c,um,ub,uc,lm,lb,lc,depth+1);return;
            }
            int start=v.Count;v.AddRange(new[]{a,b,c});u.AddRange(new[]{ua,ub,uc});light.AddRange(new[]{la,lb,lc});triangles.AddRange(new[]{start,start+1,start+2});
        }
        for(int i=0;i<sourceTriangles.Length;i+=3){int a=sourceTriangles[i],b=sourceTriangles[i+1],c=sourceTriangles[i+2];Triangle(vertices[a],vertices[b],vertices[c],uv[a],uv[b],uv[c],uv2[a],uv2[b],uv2[c],0);}
        var m=new Mesh();m.SetVertices(v);m.SetUVs(0,u);m.SetUVs(1,light);m.SetTriangles(triangles,0);
        // Interpolation retains the existing non-overlapping UV charts and clipped boundaries.
        return Save("Subdivided compound ground",m,false);
    }
    static Mesh BankMesh(Transform bank)
    {
        var v=new List<Vector3>();var uv=new List<Vector2>();var grass=new List<int>();var dark=new List<int>();
        void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool stone)
        {
            int n=v.Count;var normal=Vector3.Cross(b-a,c-a).normalized;
            foreach(var p in new[]{a,b,c,d}){v.Add(bank.InverseTransformPoint(p));uv.Add(Mathf.Abs(normal.y)>.5f?new(p.x/3,p.z/3):Mathf.Abs(normal.x)>.5f?new(p.z/3,p.y/3):new(p.x/3,p.y/3));}
            (stone?dark:grass).AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
        }
        Quad(new(-35,0,111.5f),new(-35,0,160),new(35,0,160),new(35,0,111.5f),false);
        Quad(new(-35,0,110),new(-35,0,111.5f),new(35,0,111.5f),new(35,0,110),true);
        // Foundation covers the front below -0.03; do not leave a coplanar grass face there.
        Quad(new(-35,-.03f,110),new(-35,0,110),new(35,0,110),new(35,-.03f,110),true);
        foreach(int s in new[]{-1,1})
        {
            float x=s*35;var a=new Vector3(x,-1,120);var b=new Vector3(x,0,120);var c=new Vector3(x,0,160);var d=new Vector3(x,-1,160);
            if(s<0)Quad(a,d,c,b,true);else Quad(a,b,c,d,true);
            // Above the foundation's top, retain a closed edge to the dark lip.
            a=new(x,-.03f,110);b=new(x,0,110);c=new(x,0,120);d=new(x,-.03f,120);
            if(s<0)Quad(a,d,c,b,true);else Quad(a,b,c,d,true);
        }
        Quad(new(35,-1,160),new(35,0,160),new(-35,0,160),new(-35,-1,160),true);
        var m=new Mesh{ subMeshCount=2 };m.SetVertices(v);m.SetUVs(0,uv);m.SetTriangles(grass,0);m.SetTriangles(dark,1);return Save("Dark edged tunnel bank",m);
    }
    public static void Apply()
    {
        RailCanyon.Guard();var root=PrefabUtility.LoadPrefabContents(Path);
        try
        {
            var env=root.transform.Find("Env");var polish=env.Find("November 11 polish");
            if(polish.Find("West south floodlight"))throw new Exception("Follow-up already applied; inspect rather than rebuild.");
            var ground=env.Find("Finale approach");ground.GetComponent<MeshFilter>().sharedMesh=SubdivideGround(ground.GetComponent<MeshFilter>());
            ground.GetComponent<MeshRenderer>().scaleInLightmap=2;
            foreach(int s in new[]{-1,1})
            {
                var ramp=polish.Find("Middle ramp "+s);var m=Object.Instantiate(ramp.GetComponent<MeshFilter>().sharedMesh);var v=m.vertices;
                for(int i=0;i<v.Length;i++)if(v[i].y<1.81f)v[i].y=1.8f+(v[i].z-48)*.25f;
                m.vertices=v;m=Save("Middle ramp "+s,m);ramp.GetComponent<MeshFilter>().sharedMesh=m;ramp.GetComponent<MeshCollider>().sharedMesh=m;
            }
            var all=root.GetComponentsInChildren<Transform>(true);
            foreach(var head in all.Where(t=>t.name=="Sports field floodlight array"))
            {
                var pole=all.Where(t=>t.name=="Security lamp post").OrderBy(t=>Vector3.Distance(t.position,head.position)).First();
                var lamp=all.Where(t=>t.name=="Baked compound floodlight").OrderBy(t=>Vector3.Distance(t.position,head.position)).First();
                var group=new GameObject((head.position.x<0?"West":"East")+(head.position.z<50?" south":" north")+" floodlight").transform;
                group.SetParent(polish,false);group.position=pole.position-Vector3.up*3;
                head.SetParent(group,true);lamp.SetParent(group,true);pole.SetParent(group,true);
            }
            var bank=env.Find("Far bank");var grass=bank.GetComponent<MeshRenderer>().sharedMaterial;
            bank.GetComponent<MeshFilter>().sharedMesh=BankMesh(bank);
            bank.GetComponent<MeshRenderer>().sharedMaterials=new[]{grass,AssetDatabase.LoadAssetAtPath<Material>("Assets/DuckRails/FinaleFort/Ridge rock.mat")};
            foreach(var wall in polish.Cast<Transform>().Where(t=>t.name=="Low approach concrete wall"))
            {
                wall.localPosition=new(wall.localPosition.x,.6f,40);wall.localScale=new(.6f,1.2f,80);
                var m=Object.Instantiate(wall.GetComponent<MeshFilter>().sharedMesh);var v=m.vertices;var ns=m.normals;var u=new Vector2[v.Length];
                for(int i=0;i<v.Length;i++){var p=wall.TransformPoint(v[i]);u[i]=Mathf.Abs(ns[i].y)>.5f?new(p.x/3,p.z/3):Mathf.Abs(ns[i].x)>.5f?new(p.z/3,p.y/3):new(p.x/3,p.y/3);}
                m.uv=u;wall.GetComponent<MeshFilter>().sharedMesh=Save("Extended perimeter wall "+Mathf.Sign(wall.position.x),m);
            }
            PrefabUtility.SaveAsPrefabAsset(root,Path);AssetDatabase.SaveAssets();
            Debug.Log("Follow-ups 17-21 installed. Rebuild navigation and bake/export lighting before acceptance.");
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
}
#endif
