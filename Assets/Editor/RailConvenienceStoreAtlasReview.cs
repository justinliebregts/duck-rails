#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Imports the full Convenience Store Blender review into isolated review assets.</summary>
public static class RailConvenienceStoreAtlasReview
{
    const string Source = "Assets/DuckRails/Blocks/convenience-store-v1.prefab";
    const string Folder = "Assets/DuckRails/ConvenienceStoreAtlasReview";
    const string Prefab = Folder + "/ConvenienceStore-FullAtlas-Review.prefab";
    const string ScenePath = Folder + "/ConvenienceStore-FullAtlas-Review.unity";
    const string Bake = "Assets/DuckRails/Lighting/Blocks/convenience-store-v1/convenience-store-v1-bake.unity";
    const string Art = "ArtSource/ConvenienceStoreAtlas/";
    [Serializable] public class Payload { public string sourceHash; public string[] sourceKeys; public Part[] parts; }
    [Serializable] public class Part
    {
        public string name, lightmap, settings;
        public Vector3[] vertices, normals;
        public Vector2[] uv, uv2;
        public int[] triangles;
    }
    static void RequireIdle()
    {
        if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets" || EditorApplication.isPlaying || Lightmapping.isRunning)
            throw new InvalidOperationException("Use idle Duck Rails Edit Mode.");
    }
    static Texture2D Texture(string name, bool srgb)
    {
        string path=Folder+"/"+name+".png";
        File.Copy(Art+name+".png",path,false);
        AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);
        importer.sRGBTexture=srgb; importer.alphaSource=TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency=false; importer.filterMode=FilterMode.Point;
        // Surface values are constant per tile. The original textured HDR neon is excluded.
        importer.wrapMode=TextureWrapMode.Clamp; importer.mipmapEnabled=srgb;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=1024;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }
    static MeshRenderer Resolve(Transform root, string key)
    {
        var t=root;
        foreach(string index in key.Split('/'))t=t.GetChild(int.Parse(index));
        var renderer=t.GetComponent<MeshRenderer>();
        if(!renderer || !renderer.enabled)throw new InvalidOperationException("Missing/enabled source mismatch: "+key);
        return renderer;
    }
    [MenuItem("Duck Rails/Authoring/Convenience Store/Create isolated full-map Blender review")]
    public static void Create()
    {
        RequireIdle();
        if(AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Full-map review already exists; do not overwrite review edits.");
        var data=JsonUtility.FromJson<Payload>(File.ReadAllText(Art+"unity-meshes.json"));
        if(data.sourceHash!=AssetDatabase.GetAssetDependencyHash(Source).ToString())
            throw new InvalidOperationException("Source changed since Blender export; re-export before import.");
        if(data.sourceKeys.Length!=data.sourceKeys.Distinct().Count() || data.parts.Length==0)
            throw new InvalidOperationException("Invalid source/chunk manifest.");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="ConvenienceStore full map single atlas"};
        mat.SetTexture("_BaseMap",Texture("ConvenienceStore_BaseColor",true)); mat.SetColor("_BaseColor",Color.white);
        // All selected source materials are nonemissive. Existing HDR neon remains intact.
        mat.SetColor("_EmissionColor",Color.black); mat.DisableKeyword("_EMISSION");
        mat.SetTexture("_MetallicGlossMap",Texture("ConvenienceStore_MetallicSmoothness",false)); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetFloat("_Smoothness",1); mat.SetFloat("_SmoothnessTextureChannel",0);
        mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        AssetDatabase.CreateAsset(mat,Folder+"/ConvenienceStoreAtlas.mat");
        if(!AssetDatabase.CopyAsset(Source,Prefab))throw new IOException("Could not copy ConvenienceStore for review.");
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            // Resolve stable sibling-index paths before appending the optimized visual root.
            var sources=data.sourceKeys.Select(k=>Resolve(root.transform,k)).ToArray();
            if(root.GetComponent<RailSceneryChunks>())throw new InvalidOperationException("Unexpected generated scenery switch; inspect before importing.");
            var owner=root.GetComponent<RailBakedBlockLighting>();
            var bindings=owner.bindings.Where(b=>b.renderer).ToList();
            var visual=new GameObject("Blender full ConvenienceStore - atlas review");visual.transform.SetParent(root.transform,false);
            foreach(var source in sources)source.enabled=false;
            // One asset with mesh subassets avoids hundreds of expensive standalone imports.
            AssetDatabase.StartAssetEditing();
            try
            {
                int count=0;
                foreach(var part in data.parts)
                {
                    var mesh=new Mesh{name=part.name,indexFormat=part.vertices.Length>65535?IndexFormat.UInt32:IndexFormat.UInt16,
                        vertices=part.vertices,normals=part.normals,uv=part.uv,uv2=part.uv2,triangles=part.triangles};
                    mesh.RecalculateBounds();mesh.RecalculateTangents();
                    if(count++==0)AssetDatabase.CreateAsset(mesh,Folder+"/ConvenienceStoreAtlasMeshes.asset");
                    else AssetDatabase.AddObjectToAsset(mesh,Folder+"/ConvenienceStoreAtlasMeshes.asset");
                    var settings=part.settings.Split(',');
                    var go=new GameObject(part.name);go.transform.SetParent(visual.transform,false);go.layer=int.Parse(settings[0]);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;
                    renderer.shadowCastingMode=(ShadowCastingMode)int.Parse(settings[1]);renderer.receiveShadows=settings[2]=="1";
                    renderer.lightProbeUsage=(LightProbeUsage)int.Parse(settings[3]);renderer.reflectionProbeUsage=(ReflectionProbeUsage)int.Parse(settings[4]);
                    renderer.renderingLayerMask=uint.Parse(settings[5]);renderer.sortingLayerID=int.Parse(settings[6]);renderer.sortingOrder=int.Parse(settings[7]);
                    renderer.motionVectorGenerationMode=(MotionVectorGenerationMode)int.Parse(settings[8]);renderer.receiveGI=ReceiveGI.Lightmaps;
                    var lightmap=AssetDatabase.LoadAssetAtPath<Texture2D>(part.lightmap);
                    if(!lightmap)throw new InvalidOperationException("Missing lightmap: "+part.lightmap);
                    bindings.Add(new RailBakedBlockLighting.Binding{renderer=renderer,color=lightmap,scaleOffset=new Vector4(1,1,0,0)});
                }
            }
            finally {AssetDatabase.StopAssetEditing();}
            owner.bindings=bindings.ToArray();
            PrefabUtility.SaveAsPrefabAsset(root,Prefab);
            Debug.Log($"Full ConvenienceStore review: {sources.Length} source renderers replaced by {data.parts.Length} single-material chunks. Original collision/gameplay retained.");
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        if(!AssetDatabase.CopyAsset(Bake,ScenePath))throw new IOException("Could not copy review lighting scene.");
        Open();
    }
    [MenuItem("Duck Rails/Authoring/Convenience Store/Open full-map Blender review")]
    public static void Open()
    {
        RequireIdle();
        var stage=PrefabStageUtility.GetCurrentPrefabStage();
        if(stage!=null&&stage.scene.isDirty)throw new InvalidOperationException("Save current prefab edits before opening review.");
        for(int i=0;i<SceneManager.sceneCount;i++)
            if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save current scene edits before opening review.");
        StageUtility.GoToMainStage();
        var scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
        var review=scene.GetRootGameObjects().FirstOrDefault(g=>PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(g)==Prefab);
        if(!review)
        {
            foreach(var g in scene.GetRootGameObjects().Where(g=>g.GetComponent<RailBlock>()!=null).ToArray())UnityEngine.Object.DestroyImmediate(g);
            review=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab),scene);
        }
        var maps=LightmapSettings.lightmaps.ToList();
        foreach(var binding in review.GetComponent<RailBakedBlockLighting>().bindings)
        {
            if(!binding.renderer||!binding.color)continue;
            int index=maps.FindIndex(m=>m.lightmapColor==binding.color);
            if(index<0){index=maps.Count;maps.Add(new LightmapData{lightmapColor=binding.color});}
            binding.renderer.lightmapIndex=index;binding.renderer.lightmapScaleOffset=binding.scaleOffset;
        }
        LightmapSettings.lightmaps=maps.ToArray();
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.objects=Array.Empty<UnityEngine.Object>();
        var view=SceneView.lastActiveSceneView;
        if(view){view.sceneLighting=true;view.sceneViewState.showFog=false;view.drawGizmos=false;view.LookAt(new Vector3(16,3,79),Quaternion.LookRotation(new Vector3(1,-.32f,.32f)),28,false,true);view.Repaint();}
        Debug.Log("Full ConvenienceStore atlas review open with original baked lighting. No Play Mode/device run.");
    }
}
#endif
