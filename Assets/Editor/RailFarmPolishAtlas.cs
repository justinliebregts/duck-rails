#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class RailFarmPolishAtlas
{
    public const string Art="ArtSource/FarmSilosPolish/";
    const string Folder="Assets/DuckRails/FarmSilosPolishAtlas";
    public const string ReviewScene=Folder+"/FarmSilos-Polished-Review.unity";
    static double nextCheck;
    static RailFarmPolishAtlas(){EditorApplication.update+=RebindReview;}
    static void RebindReview()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode||Lightmapping.isRunning||EditorApplication.timeSinceStartup<nextCheck)return;
        nextCheck=EditorApplication.timeSinceStartup+1;var scene=SceneManager.GetSceneByPath(ReviewScene);if(!scene.IsValid()||!scene.isLoaded)return;
        var root=scene.GetRootGameObjects().FirstOrDefault(g=>g.GetComponent<RailBlock>());if(!root)return;
        var maps=LightmapSettings.lightmaps.ToList();
        foreach(var b in root.GetComponent<RailBakedBlockLighting>().bindings){if(!b.renderer||!b.color)continue;int i=maps.FindIndex(m=>m.lightmapColor==b.color);if(i<0){i=maps.Count;maps.Add(new LightmapData{lightmapColor=b.color});}b.renderer.lightmapIndex=i;b.renderer.lightmapScaleOffset=b.scaleOffset;}
        LightmapSettings.lightmaps=maps.ToArray();
    }
    [MenuItem("Duck Rails/Authoring/Farm Silos/Open polished Blender review")]
    public static void OpenReview()
    {
        if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Finish Play/bake first");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save pending scene edits before opening the Farm review");
        var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage!=null&&stage.scene.isDirty)throw new Exception("Save pending prefab edits before opening the Farm review");
        var scene=EditorSceneManager.OpenScene(ReviewScene);RebindReview();
        var view=SceneView.lastActiveSceneView;if(view){view.sceneLighting=true;view.sceneViewState.showFog=false;view.drawGizmos=false;view.LookAt(new Vector3(18,4,105),Quaternion.LookRotation(new Vector3(1,-.6f,-1)),35,false,true);}
    }
    [Serializable] public class Payload {public string sourceHash;public string[] sourceKeys;public Part[] parts;}
    [Serializable] public class Part {public string name,lightmap,settings,materialPath;public Vector3[] vertices,normals;public Vector2[] uv,uv2;public int[] triangles;}
    static Texture2D Texture(string name,bool srgb)
    {
        var path=Folder+"/"+name+".png";File.Copy(Art+name+".png",path,false);AssetDatabase.ImportAsset(path);
        var i=(TextureImporter)AssetImporter.GetAtPath(path);i.sRGBTexture=srgb;i.alphaSource=TextureImporterAlphaSource.FromInput;i.filterMode=FilterMode.Point;i.wrapMode=TextureWrapMode.Clamp;i.mipmapEnabled=srgb;i.textureCompression=TextureImporterCompression.Uncompressed;i.maxTextureSize=1024;i.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    public static void Import()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Idle Rails required");
        var path=RailFarmSilos.PrefabPath;var data=JsonUtility.FromJson<Payload>(File.ReadAllText(Art+"unity-meshes.json"));
        if(data.sourceHash!=AssetDatabase.GetAssetDependencyHash(path).ToString()||Directory.Exists(Folder))throw new Exception("Changed source or existing import");
        if(!File.ReadAllText(Art+"geometry-checks.json").Contains("PASS"))throw new Exception("Independent geometry checks required");
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Farm polished atlas"};
        mat.SetTexture("_BaseMap",Texture("FarmSilos_BaseColor",true));mat.SetColor("_BaseColor",Color.white);
        mat.SetTexture("_EmissionMap",Texture("FarmSilos_Emission",false));mat.SetColor("_EmissionColor",Color.white*2);mat.EnableKeyword("_EMISSION");
        mat.SetTexture("_MetallicGlossMap",Texture("FarmSilos_MetallicSmoothness",false));mat.EnableKeyword("_METALLICSPECGLOSSMAP");mat.SetFloat("_Smoothness",1);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
        AssetDatabase.CreateAsset(mat,Folder+"/FarmSilosAtlas.mat");
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            var sources=data.sourceKeys.Select(k=>{var t=root.transform;foreach(var n in k.Split('/'))t=t.GetChild(int.Parse(n));return t.GetComponent<MeshRenderer>();}).ToArray();
            if(sources.Any(r=>!r||!r.enabled))throw new Exception("Source renderer mismatch");
            if(sources.Sum(r=>r.GetComponent<MeshFilter>().sharedMesh.triangles.Length)!=data.parts.Sum(p=>p.triangles.Length))throw new Exception("Triangle count mismatch");
            var visual=new GameObject(RailFarmSilosAtlasProduction.Visual);visual.transform.SetParent(root.transform,false);
            var owner=root.GetComponent<RailBakedBlockLighting>();var bindings=owner.bindings.ToList();
            AssetDatabase.StartAssetEditing();
            try
            {
                int index=0;
                foreach(var p in data.parts)
                {
                    var mesh=new Mesh{name=p.name,indexFormat=p.vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16};mesh.vertices=p.vertices;mesh.normals=p.normals;mesh.uv=p.uv;mesh.uv2=p.uv2;mesh.triangles=p.triangles;mesh.RecalculateBounds();
                    if(index++==0)AssetDatabase.CreateAsset(mesh,Folder+"/Meshes.asset");else AssetDatabase.AddObjectToAsset(mesh,Folder+"/Meshes.asset");
                    var g=new GameObject(p.name);g.transform.SetParent(visual.transform,false);var f=g.AddComponent<MeshFilter>();f.sharedMesh=mesh;var r=g.AddComponent<MeshRenderer>();r.sharedMaterial=string.IsNullOrEmpty(p.materialPath)?mat:AssetDatabase.LoadAssetAtPath<Material>(p.materialPath);
                    var s=p.settings.Split(',');g.layer=int.Parse(s[0]);r.shadowCastingMode=(ShadowCastingMode)int.Parse(s[1]);r.receiveShadows=s[2]=="1";r.lightProbeUsage=(LightProbeUsage)int.Parse(s[3]);r.reflectionProbeUsage=(ReflectionProbeUsage)int.Parse(s[4]);r.renderingLayerMask=uint.Parse(s[5]);r.sortingLayerID=int.Parse(s[6]);r.sortingOrder=int.Parse(s[7]);r.motionVectorGenerationMode=(MotionVectorGenerationMode)int.Parse(s[8]);
                    bindings.Add(new RailBakedBlockLighting.Binding{renderer=r,color=AssetDatabase.LoadAssetAtPath<Texture2D>(p.lightmap),scaleOffset=new Vector4(1,1,0,0)});
                }
            }
            finally{AssetDatabase.StopAssetEditing();}
            foreach(var r in sources)r.enabled=false;owner.bindings=bindings.ToArray();PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();var saved=AssetDatabase.LoadAssetAtPath<GameObject>(path);
        File.WriteAllText(Art+"production-state.json",JsonUtility.ToJson(new RailFarmSilosAtlasProduction.State{guid=AssetDatabase.AssetPathToGUID(path),fingerprint=RailFarmSilosAtlasProduction.Fingerprint(saved)},true));
        RailFarmSilosAtlasProduction.ValidateSaved();
    }
}
#endif
