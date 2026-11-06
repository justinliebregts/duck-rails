#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>Offline front-only furnace authoring. Preserves the original module/effect/anchor identities.</summary>
public static class RailFrontFurnaceArt
{
    public const string Furnace = "Assets/DuckRails/Train/Prefabs/Furnace.prefab";
    public const string Folder = "Assets/DuckRails/Train/YardMule/FrontFurnace";
    public const string Evidence = "docs/validation/front-furnace";
    static Material yellow, dark, brick, ember;
    sealed class Shape
    {
        readonly List<Vector3> vertices=new List<Vector3>();
        readonly List<Vector2> uv=new List<Vector2>();
        readonly List<int> triangles=new List<int>();
        public void Face(Vector3[] points,Vector3 normal)
        {
            if(Vector3.Dot(Vector3.Cross(points[1]-points[0],points[2]-points[0]),normal)<0)Array.Reverse(points);
            var n=Vector3.Cross(points[1]-points[0],points[2]-points[0]).normalized;
            var u=Mathf.Abs(n.y)>.99f?Vector3.right:Vector3.Cross(Vector3.up,n).normalized;
            var v=Vector3.Cross(n,u);int start=vertices.Count;
            foreach(var p in points){vertices.Add(p);uv.Add(new Vector2(Vector3.Dot(p,u),Vector3.Dot(p,v))*.5f);}
            for(int i=1;i<points.Length-1;i++){triangles.Add(start);triangles.Add(start+i);triangles.Add(start+i+1);}
        }
        public void Box(Vector3 center,Vector3 size,Quaternion? rotation=null)
        {
            var q=rotation??Quaternion.identity;var p=new Vector3[8];
            for(int i=0;i<8;i++)p[i]=center+q*Vector3.Scale(size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
            foreach(var f in new[]{new[]{0,2,3,1},new[]{4,5,7,6},new[]{0,4,6,2},new[]{1,3,7,5},new[]{0,1,5,4},new[]{2,6,7,3}})
            {var face=f.Select(i=>p[i]).ToArray();Face(face,face.Aggregate(Vector3.zero,(a,b)=>a+b)/4-center);}
        }
        public Mesh Save(string name)
        {
            string path=Folder+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh{name=name};AssetDatabase.CreateAsset(mesh,path);}
            mesh.Clear();mesh.SetVertices(vertices);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);return mesh;
        }
    }
    static Material Material(string name,Color color,bool bricks=false,bool glowing=false)
    {
        var texture=new Texture2D(64,64,TextureFormat.RGB24,false);
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            float patch=Mathf.Round(Mathf.PerlinNoise((x/2)*.18f+17,(y/2)*.18f+17)*7)/7;
            Color c=color*(.9f+.2f*patch);
            if(bricks&&(y%6==0||(x+((y/6)%2)*8)%16==0))c*=.5f;
            texture.SetPixel(x,y,c);
        }
        texture.Apply();string path=Folder+"/"+name+".png";File.WriteAllBytes(path,texture.EncodeToPNG());Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var imp=(TextureImporter)AssetImporter.GetAtPath(path);imp.filterMode=FilterMode.Point;imp.wrapMode=TextureWrapMode.Repeat;imp.mipmapEnabled=true;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.maxTextureSize=64;imp.SaveAndReimport();
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+name+".mat");
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+"/"+name+".mat");}
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Smoothness",0);mat.SetFloat("_SpecularHighlights",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        if(glowing){mat.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));mat.SetColor("_EmissionColor",new Color(1,.55f,.2f)*2);mat.EnableKeyword("_EMISSION");mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
        EditorUtility.SetDirty(mat);return mat;
    }
    static GameObject Part(Transform parent,string name,Shape shape,Material material)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.layer=6;
        go.AddComponent<MeshFilter>().sharedMesh=shape.Save(name);go.AddComponent<MeshRenderer>().sharedMaterial=material;GameObjectUtility.SetStaticEditorFlags(go,0);return go;
    }
    static void Box(Transform parent,string name,Vector3 center,Vector3 size,Material material,Quaternion? rotation=null,bool solid=true)
    {
        var shape=new Shape();shape.Box(Vector3.zero,size);var go=Part(parent,name,shape,material);go.transform.localPosition=center;go.transform.localRotation=rotation??Quaternion.identity;
        if(solid)go.AddComponent<BoxCollider>().size=size;
    }
    [MenuItem("Duck Rails/Art/Build Front Only Furnace")]
    public static void Build()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying)throw new Exception("Duck Rails Edit Mode required");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory(Evidence);AssetDatabase.Refresh();
        yellow=Material("Yellow",new Color32(218,153,32,255));dark=Material("Iron",new Color32(40,44,48,255));
        brick=Material("Firebrick",new Color32(78,47,29,255),true);ember=Material("Embers",new Color32(235,75,8,255),false,true);
        var root=PrefabUtility.LoadPrefabContents(Furnace);
        try
        {
            var module=root.GetComponent<RailFurnaceModule>();
            // Retain these three objects so scene and assembly references remain stable.
            foreach(Transform child in root.transform.Cast<Transform>().ToArray())
                if(child!=module.insertion&&child!=module.fire.transform&&child!=module.smoke.transform)Object.DestroyImmediate(child.gameObject);
            var art=new GameObject("Front firebox art").transform;art.SetParent(root.transform,false);
            // Chamber lies beyond the deck's +5 m end. +Z is the single exterior mouth.
            Box(art,"Chamber floor",new Vector3(0,-.25f,.38f),new Vector3(2.16f,.08f,.76f),brick);
            Box(art,"Chamber ceiling",new Vector3(0,.39f,.38f),new Vector3(2.16f,.08f,.76f),yellow);
            Box(art,"Closed rear",new Vector3(0,.07f,.04f),new Vector3(2.16f,.56f,.08f),brick);
            foreach(float s in new[]{-1f,1f})Box(art,"Chamber side "+s,new Vector3(s*1.02f,.07f,.38f),new Vector3(.12f,.56f,.76f),brick);
            // Dark rim follows the nose slope; four separate solids never cap the opening.
            var tilt=Quaternion.Euler(-24.6f,0,0);
            Box(art,"Mouth sill",new Vector3(0,-.24f,.766f),new Vector3(2.16f,.1f,.12f),dark,tilt);
            Box(art,"Mouth lintel",new Vector3(0,.38f,.482f),new Vector3(2.16f,.1f,.12f),dark,tilt);
            foreach(float s in new[]{-1f,1f})
            {
                Box(art,"Mouth jamb "+s,new Vector3(s*1.015f,.07f,.624f),new Vector3(.13f,.68f,.12f),dark,tilt);
                foreach(float y in new[]{-.19f,.33f})Box(art,"Rim bolt "+s+" "+y,new Vector3(s*1.015f,y,.656f-y*.458f),new Vector3(.055f,.055f,.035f),yellow,tilt,false);
            }
            // Enclosed duct stays below eye level and reaches a stack outside the right canopy edge.
            Box(art,"Low exhaust duct",new Vector3(1.43f,.26f,.23f),new Vector3(.9f,.28f,.32f),dark);
            foreach(float s in new[]{-1f,1f})
            {
                Box(art,"Stack X "+s,new Vector3(2.16f+s*.2f,1.41f,.18f),new Vector3(.08f,2.3f,.48f),dark);
                Box(art,"Stack Z "+s,new Vector3(2.16f,1.41f,.18f+s*.2f),new Vector3(.32f,2.3f,.08f),dark);
            }
            Box(art,"Stack collar",new Vector3(2.16f,.45f,.18f),new Vector3(.56f,.15f,.56f),dark);
            // Flat recessed ember strips, never decorative pickup objects or a front flame panel.
            var bed=new Shape();for(int i=0;i<9;i++)bed.Box(new Vector3(-.72f+i*.18f,-.202f,.2f+(i%2)*.08f),new Vector3(.12f,.012f,.12f));Part(art,"Ember bed",bed,ember);
            module.insertion.localPosition=new Vector3(0,.07f,.38f);module.insertion.localRotation=Quaternion.identity;
            module.acceptanceHalfExtents=new Vector3(.65f,.20f,.22f);
            module.fire.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            module.fire.transform.localPosition=new Vector3(0,-.16f,.28f);module.fire.transform.localRotation=Quaternion.Euler(-90,0,0);
            var main=module.fire.main;main.startLifetime=.6f;main.startSpeed=.48f;main.startSize=new ParticleSystem.MinMaxCurve(.07f,.13f);main.maxParticles=32;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var shape=module.fire.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(1.5f,.16f,.025f);
            var emission=module.fire.emission;emission.rateOverTime=40;emission.enabled=false;
            module.smoke.transform.localPosition=new Vector3(2.16f,2.6f,.18f);
            PrefabUtility.SaveAsPrefabAsset(root,Furnace);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        RailYardMuleArt.RebuildShell();
        var train=PrefabUtility.LoadPrefabContents(RailYardMuleArt.Train);
        try
        {
            var f=train.GetComponent<RailTrainAssembly>().furnace;
            f.transform.localPosition=new Vector3(0,0,5.08f);f.transform.localRotation=Quaternion.identity;f.transform.localScale=Vector3.one;
            PrefabUtility.SaveAsPrefabAsset(train,RailYardMuleArt.Train);
        }
        finally{PrefabUtility.UnloadPrefabContents(train);}
        AssetDatabase.SaveAssets();Debug.Log("Installed front-only furnace; original control and network anchor identities retained.");
    }

    public static void Preview()
    {
        Directory.CreateDirectory(Evidence);var p=new PreviewRenderUtility();
        try
        {
            var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RailYardMuleArt.Train));p.AddSingleGO(model);
            model.GetComponentInChildren<RailCockpitFuelBar>().SetFuel(10);
            foreach(var t in model.GetComponentsInChildren<TMPro.TMP_Text>())t.ForceMeshUpdate(true,true);
            var fire=model.GetComponent<RailTrainAssembly>().furnace.fire;var emission=fire.emission;emission.enabled=true;fire.Simulate(1,true,true);
            foreach(var r in model.GetComponentsInChildren<Renderer>()){r.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;r.receiveShadows=false;}
            p.ambientColor=new Color(.5f,.5f,.5f);p.lights[0].intensity=1.4f;p.lights[0].transform.rotation=Quaternion.Euler(45,150,0);p.lights[1].intensity=.7f;p.lights[1].transform.rotation=Quaternion.Euler(25,-40,0);
            var camera=p.camera;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.19f,.21f,.24f);camera.nearClipPlane=.02f;camera.farClipPlane=100;
            var positions=new[]{new Vector3(10,5,15),new Vector3(0,.65f,8),new Vector3(0,.5f,3.65f)};
            var targets=new[]{new Vector3(0,.4f,0),new Vector3(0,.05f,5.5f),new Vector3(0,.5f,8)};
            for(int i=0;i<3;i++)
            {
                camera.orthographic=i==0;camera.orthographicSize=5;camera.fieldOfView=i==1?65:85;camera.transform.position=positions[i];camera.transform.LookAt(targets[i]);
                for(int frame=0;frame<2;frame++){p.BeginStaticPreview(new Rect(0,0,1500,900));p.Render(true);var im=p.EndStaticPreview();if(frame==1)File.WriteAllBytes(Evidence+"/"+new[]{"exterior","mouth","driver"}[i]+".png",im.EncodeToPNG());Object.DestroyImmediate(im);}
            }
        }
        finally{p.Cleanup();}
    }
}
#endif
