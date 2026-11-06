#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Atlas surface authoring for the existing subway boxes; no gameplay or collision changes.</summary>
public static class RailCompoundMaterials
{
    public const string Folder = "Assets/DuckRails/Compound/QuackvilleMaterials";
    const string Prefab = "Assets/DuckRails/Compound/SubwayV2/SubwayCompound.prefab";
    static readonly string[] Names = {"Stained plaster","Faded olive paint","Exposed brick","Worn concrete","Oxidized steel","Dark roofing","Old timber","Worn yellow","Ground asphalt"};
    static readonly Rect[] Tiles = {new Rect(0,64,64,64),new Rect(0,192,64,64),new Rect(64,0,32,32),new Rect(128,128,64,64),new Rect(32,64,64,32),new Rect(128,64,64,64),new Rect(192,64,64,64),new Rect(0,32,32,32),new Rect(128,0,64,64)};
    [Serializable] class SourceRecord { public string mesh; public string material; }
    sealed class Mapped { public Mesh mesh; public Material[] materials; }

    [MenuItem("Duck Rails/Compound/Materials/Apply Quackville depot treatment")]
    public static void Apply()
    {
        var scene = SceneManager.GetActiveScene();
        if (Application.dataPath != "X:/Work/duck-rails/Assets" || scene.path != "Assets/Scenes/Compound.unity" || EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new InvalidOperationException("Open Duck Rails Compound in Edit Mode with no bake running.");
        var root = GameObject.Find("Compound Geometry/SubwayCompound");
        if (!root) throw new InvalidOperationException("Subway compound missing.");
        Directory.CreateDirectory("tmp/compound-materials");
        Directory.CreateDirectory(Folder + "/Meshes");
        Directory.CreateDirectory("docs/validation/compound-materials");
        EditorSceneManager.SaveScene(scene,"tmp/compound-materials/Compound-before-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".unity",true);
        AssetDatabase.Refresh();
        var materials = Names.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(RailDepotMaterials.Folder+"/"+n+".mat")).ToArray();
        if(materials.Any(m=>!m)) throw new InvalidOperationException("Install depot materials first.");
        var colliders = root.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c)).ToArray();
        var transforms = root.GetComponentsInChildren<Transform>().Select(t=>t.GetInstanceID()+":"+t.localToWorldMatrix.ToString("F6")).ToArray();
        var lights = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(l=>l.GetInstanceID()+":"+EditorJsonUtility.ToJson(l)).ToArray();
        var cache = new Dictionary<string,Mapped>();
        int count=0;
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>())
        {
            var f=r.GetComponent<MeshFilter>();
            if(!f || !f.sharedMesh || r.GetComponent<TMPro.TMP_Text>()) continue;
            if(r.name=="Bright lens" || r.name=="Continuous see-through track guard" || r.name.StartsWith("Shallow water") || r.name=="Pinned map sheet") continue;
            var source=f.sharedMesh;
            var originalMaterial=r.sharedMaterial.name;
            var sourcePath=AssetDatabase.GetAssetPath(source);
            if(sourcePath.StartsWith(Folder+"/"))
            {
                var record=JsonUtility.FromJson<SourceRecord>(AssetImporter.GetAtPath(sourcePath).userData);
                source=AssetDatabase.LoadAssetAtPath<Mesh>(record.mesh);
                originalMaterial=record.material;
                sourcePath=record.mesh;
            }
            if(!source || source.vertexCount!=24 || source.uv2.Length!=24) throw new InvalidOperationException("Expected lightmapped box: "+r.name);
            string kind=Kind(r.name,originalMaterial);
            var size=Vector3.Scale(source.bounds.size,r.transform.lossyScale);
            float originY=r.transform.position.y;
            string key=sourcePath+"|"+kind+"|"+size.ToString("F5")+"|"+(kind=="wall"?originY.ToString("F5"):"0")+"|"+originalMaterial;
            if(!cache.TryGetValue(key,out var mapped))
            {
                mapped=Make(source,size,originY,kind,materials);
                string path=Folder+"/Meshes/"+kind+"-"+Hash128.Compute(key)+".asset";
                var saved=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(saved)
                {
                    saved.Clear();saved.indexFormat=mapped.mesh.indexFormat;saved.vertices=mapped.mesh.vertices;saved.normals=mapped.mesh.normals;
                    saved.uv=mapped.mesh.uv;saved.uv2=mapped.mesh.uv2;saved.tangents=mapped.mesh.tangents;saved.subMeshCount=mapped.mesh.subMeshCount;
                    for(int i=0;i<saved.subMeshCount;i++)saved.SetTriangles(mapped.mesh.GetTriangles(i),i);
                    saved.bounds=mapped.mesh.bounds;saved.UploadMeshData(false);EditorUtility.SetDirty(saved);
                    UnityEngine.Object.DestroyImmediate(mapped.mesh);mapped.mesh=saved;
                }
                else AssetDatabase.CreateAsset(mapped.mesh,path);
                var importer=AssetImporter.GetAtPath(path);
                importer.userData=JsonUtility.ToJson(new SourceRecord{mesh=sourcePath,material=originalMaterial});
                AssetDatabase.WriteImportSettingsIfDirty(path);
                cache.Add(key,mapped);
            }
            if((mapped.mesh.bounds.size-source.bounds.size).sqrMagnitude>.000001f)throw new InvalidOperationException("Bounds changed: "+r.name);
            Undo.RecordObject(f,"Compound atlas UVs");Undo.RecordObject(r,"Compound depot materials");
            f.sharedMesh=mapped.mesh;r.sharedMaterials=mapped.materials;
            PrefabUtility.RecordPrefabInstancePropertyModifications(f);PrefabUtility.RecordPrefabInstancePropertyModifications(r);
            // Apply only appearance to the outer prefab. Existing scene lighting and geometry overrides stay local.
            PrefabUtility.ApplyPropertyOverride(new SerializedObject(f).FindProperty("m_Mesh"),Prefab,InteractionMode.AutomatedAction);
            PrefabUtility.ApplyPropertyOverride(new SerializedObject(r).FindProperty("m_Materials"),Prefab,InteractionMode.AutomatedAction);
            count++;
        }
        if(!colliders.SequenceEqual(root.GetComponentsInChildren<Collider>().Select(c=>c.GetInstanceID()+":"+EditorJsonUtility.ToJson(c))) ||
           !transforms.SequenceEqual(root.GetComponentsInChildren<Transform>().Select(t=>t.GetInstanceID()+":"+t.localToWorldMatrix.ToString("F6"))) ||
           !lights.SequenceEqual(UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).Select(l=>l.GetInstanceID()+":"+EditorJsonUtility.ToJson(l))))
            throw new InvalidOperationException("Collision, transforms or lights changed.");
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        File.WriteAllText("docs/validation/compound-materials/apply.txt",$"Mapped {count} surfaces using {cache.Count} shared meshes.\nPASS unchanged collider serialization, transforms, lights and mesh bounds.\nShared depot materials; original atlases unchanged.\n");
        Debug.Log("Compound materials applied: "+count+" surfaces. Rebake via Compound > Lighting > Bake Sample.");
    }

    static string Kind(string name,string material)
    {
        if(name.Contains("structural pillar") || name=="Rear departure wall" || name=="Surface boundary wall" || name=="Stairwell front partition" || name=="Stair side wall" || name=="Plain controls mounting wall")return "wall";
        if(name=="Column red band")return "plaster";
        if(name.Contains("ceiling") || name.Contains("cross beam") || name.Contains("canopy"))return "roof";
        if(name=="Continuous walkable floor" || name.Contains("rim") || name.Contains("pedestal") || name=="Basin floor" || name.StartsWith("Step ") || name=="Original platform lip")return "concrete";
        if(name.Contains("edge paint") || name.Contains("edge stripe"))return "yellow";
        if(name=="Recessed ballast bed")return "ground";
        if(name=="Outer tunnel wall")return "concrete";
        if(material=="Salvaged timber" || material=="Rusty sheets" && (name.Contains("partition") || name.Contains("barricade")))return "timber";
        return "steel";
    }

    static Mapped Make(Mesh source,Vector3 size,float originY,string kind,Material[] materials)
    {
        var verts=new List<Vector3>();var normals=new List<Vector3>();var uv=new List<Vector2>();var uv2=new List<Vector2>();
        var triangles=Enumerable.Range(0,Names.Length).Select(_=>new List<int>()).ToArray();
        var sv=source.vertices;var sn=source.normals;var su=source.uv2;var bounds=source.bounds;
        foreach(var normal in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
        {
            var u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,normal);var v=Vector3.Cross(normal,u);
            float w=Vector3.Scale(u,size).magnitude,h=Vector3.Scale(v,size).magnitude;
            var xs=Edges(w,kind=="wall"?.5f:4);var ys=Edges(h,kind=="wall"?.5f:4);
            if(kind=="wall" && Mathf.Abs(v.y)>.5f){float band=1.3f-originY+h/2;if(band>0&&band<h)ys.Add(band);ys=ys.Distinct().OrderBy(a=>a).ToList();}
            Vector2 Corner(float x,float y)
            {
                var p=bounds.center+Vector3.Scale(normal*.5f+u*x+v*y,bounds.size);int best=-1;float d=float.MaxValue;
                for(int i=0;i<sv.Length;i++){if(Vector3.Dot(sn[i],normal)<.99f)continue;float dist=(sv[i]-p).sqrMagnitude;if(dist<d){best=i;d=dist;}}
                if(best<0)throw new InvalidOperationException("Missing box UV chart");return su[best];
            }
            var c00=Corner(-.5f,-.5f);var c10=Corner(.5f,-.5f);var c01=Corner(-.5f,.5f);var c11=Corner(.5f,.5f);
            for(int x=0;x<xs.Count-1;x++)for(int y=0;y<ys.Count-1;y++)
            {
                float mx=(xs[x]+xs[x+1])/2,my=(ys[y]+ys[y+1])/2;
                int mat=kind=="wall"||kind=="plaster"?0:kind=="roof"?5:kind=="steel"?4:kind=="timber"?6:kind=="yellow"?7:kind=="ground"?8:3;
                if(kind=="wall")
                {
                    float height=originY+(my-h/2)*v.y; if(height<1.3f)mat=1;
                    float wear=Mathf.PerlinNoise(mx*.5f+size.x+normal.x*3,my*.45f+size.z+normal.z*2);
                    if(Mathf.Abs(normal.y)<.5f && wear>.73f)mat=2;
                    else if(height<1.3f && wear>.66f)mat=0;
                }
                int start=verts.Count;var tile=Tiles[mat];var texture=materials[mat].mainTexture;
                float tu=Mathf.Floor(mx/4)*4,tv=Mathf.Floor(my/4)*4;
                foreach(var p in new[]{new Vector2(xs[x],ys[y]),new Vector2(xs[x+1],ys[y]),new Vector2(xs[x+1],ys[y+1]),new Vector2(xs[x],ys[y+1])})
                {
                    float a=p.x/w,b=p.y/h;
                    verts.Add(bounds.center+Vector3.Scale(normal*.5f+u*(a-.5f)+v*(b-.5f),bounds.size));normals.Add(normal);
                    uv2.Add(Vector2.Lerp(Vector2.Lerp(c00,c10,a),Vector2.Lerp(c01,c11,a),b));
                    uv.Add(new Vector2((tile.x+.5f+(p.x-tu)/4*(tile.width-1))/texture.width,1-(tile.y+.5f+(p.y-tv)/4*(tile.height-1))/texture.height));
                }
                triangles[mat].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
        }
        var used=Enumerable.Range(0,Names.Length).Where(i=>triangles[i].Count>0).ToArray();
        var mesh=new Mesh{name="Compound "+kind+" atlas"};if(verts.Count>65535)mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetUVs(1,uv2);mesh.subMeshCount=used.Length;
        for(int i=0;i<used.Length;i++)mesh.SetTriangles(triangles[used[i]],i);
        mesh.RecalculateBounds();mesh.RecalculateTangents();
        return new Mapped{mesh=mesh,materials=used.Select(i=>materials[i]).ToArray()};
    }
    static List<float> Edges(float length,float step)
    {var edges=new List<float>{0};for(float f=step;f<length-.0001f;f+=step)edges.Add(f);edges.Add(length);return edges;}
}
#endif
