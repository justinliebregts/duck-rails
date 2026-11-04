#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Low perimeter banks only. Flat encounter, track corridor and block seams are retained.</summary>
public static class RailMotelEdgeBanks
{
    const string Folder="Assets/DuckRails/MotelMaterials/Terrain";
    public static void Build(Transform root)
    {
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var old=root.Find("Low edge banks");if(old)UnityEngine.Object.DestroyImmediate(old.gameObject);
        var group=new GameObject("Low edge banks");group.transform.SetParent(root,false);
        string path=Folder+"/Quackville grass.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!material){material=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(material,path);}
        material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Models/Quackville/Quackville_Town_Diffuse.jpg");
        material.SetColor("_BaseColor",new Color(.6f,.67f,.48f));material.SetFloat("_Smoothness",0);EditorUtility.SetDirty(material);
        Patch("Southwest bank",-46,-18,6,42,.9f);Patch("Southeast bank",18,46,6,42,.8f);
        Patch("Northwest bank",-46,-18,118,154,.8f);Patch("Northeast bank",18,46,118,154,.9f);
        Patch("West edge rise",-48,-44,42,118,.3f);Patch("East edge rise",46,48,42,118,.15f);
        void Patch(string name,float x0,float x1,float z0,float z1,float height)
        {
            var vertices=new List<Vector3>();var uv=new List<Vector2>();var triangles=new List<int>();
            float Height(float x,float z)=>height*Mathf.Pow(Mathf.Sin(Mathf.PI*(x-x0)/(x1-x0)),2)*Mathf.Pow(Mathf.Sin(Mathf.PI*(z-z0)/(z1-z0)),2);
            for(float z=z0;z<z1;z++)for(float x=x0;x<x1;x++)
            {
                int k=vertices.Count;
                foreach(var p in new[]{new Vector2(x,z),new Vector2(x+1,z),new Vector2(x+1,z+1),new Vector2(x,z+1)})
                {
                    vertices.Add(new Vector3(p.x,.012f+Height(p.x,p.y),p.y));
                    float u=(p.x-x)+(x-x0)%4,v=(p.y-z)+(z-z0)%4;
                    uv.Add(new Vector2((.5f+u/4*63)/256,1-(.5f+v/4*63)/256));
                }
                triangles.AddRange(new[]{k,k+2,k+1,k,k+3,k+2});
            }
            string meshPath=Folder+"/"+name.Replace(' ','-')+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);bool fresh=!mesh;
            if(fresh)mesh=new Mesh();else mesh.Clear();mesh.name=Path.GetFileNameWithoutExtension(meshPath);
            mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.SetUVs(0,uv);mesh.RecalculateNormals();mesh.RecalculateBounds();Unwrapping.GenerateSecondaryUVSet(mesh);
            if(fresh)AssetDatabase.CreateAsset(mesh,meshPath);else EditorUtility.SetDirty(mesh);
            var go=new GameObject(name);go.layer=6;go.transform.SetParent(group.transform,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.receiveGI=ReceiveGI.Lightmaps;r.scaleInLightmap=.2f;
            go.AddComponent<MeshCollider>().sharedMesh=mesh;GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI);
        }
    }
}
#endif
