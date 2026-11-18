#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

/// <summary>Imports the triangle-preserving Motel Blender review into isolated review assets.</summary>
public static class RailMotelLowPolyAtlasReview
{
    const string Source = "Assets/DuckRails/Blocks/roadside-motel-v1.prefab";
    const string Folder = "Assets/DuckRails/MotelLowPolyAtlasReview";
    const string Prefab = Folder + "/Motel-LowPoly-Atlas-Review.prefab";
    const string ScenePath = Folder + "/Motel-LowPoly-Atlas-Review.unity";
    const string Bake = "Assets/DuckRails/Lighting/Blocks/roadside-motel-v1/roadside-motel-v1-bake.unity";
    const string Art = "ArtSource/MotelAtlasLowPoly/";
    [Serializable] public class Payload { public string sourceHash; public string[] sourceKeys; public Part[] parts; }
    [Serializable] public class Part
    {
        public string name, lightmap, settings, materialPath;
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
        // Emission and surface values are constant per tile; avoid cross-tile mip glow.
        importer.wrapMode=TextureWrapMode.Clamp; importer.mipmapEnabled=srgb;
        importer.textureCompression=TextureImporterCompression.Uncompressed; importer.maxTextureSize=2048;
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
    [MenuItem("Duck Rails/Authoring/Motel/Create isolated low-poly Blender review")]
    public static void Create()
    {
        RequireIdle();
        if(AssetDatabase.IsValidFolder(Folder))throw new InvalidOperationException("Full-map review already exists; do not overwrite review edits.");
        var data=JsonUtility.FromJson<Payload>(File.ReadAllText(Art+"unity-meshes.json"));
        if(data.sourceHash!=AssetDatabase.GetAssetDependencyHash(Source).ToString())
            throw new InvalidOperationException("Source changed since Blender export; re-export before import.");
        if(data.sourceKeys.Length!=data.sourceKeys.Distinct().Count() || data.parts.Length==0)
            throw new InvalidOperationException("Invalid source/chunk manifest.");
        if(data.parts.Sum(p=>p.triangles.Length/3)!=79948)
            throw new InvalidOperationException("Triangle count must exactly match the saved source (79,948).");
        foreach(var p in data.parts)
        {
            if(p.vertices.Length!=p.normals.Length || p.vertices.Length!=p.uv.Length || p.vertices.Length!=p.uv2.Length || p.triangles.Length%3!=0 || p.triangles.Any(i=>i<0 || i>=p.vertices.Length))
                throw new InvalidOperationException("Invalid mesh payload: "+p.name);
            if(!string.IsNullOrEmpty(p.materialPath) && !AssetDatabase.LoadAssetAtPath<Material>(p.materialPath))
                throw new InvalidOperationException("Missing repeating material: "+p.materialPath);
        }
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name="Motel low-poly atlas"};
        mat.SetTexture("_BaseMap",Texture("Motel_BaseColor",true)); mat.SetColor("_BaseColor",Color.white);
        // No selected Motel material is emissive (glowing signs with emission maps stay
        // original renderers), so the atlas material uses no emission keyword or map.
        mat.SetTexture("_MetallicGlossMap",Texture("Motel_MetallicSmoothness",false)); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetFloat("_Smoothness",1); mat.SetFloat("_SmoothnessTextureChannel",0);
        mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
        AssetDatabase.CreateAsset(mat,Folder+"/MotelAtlas.mat");
        if(!AssetDatabase.CopyAsset(Source,Prefab))throw new IOException("Could not copy Motel for review.");
        var root=PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            // Resolve stable sibling-index paths before appending the optimized visual root.
            var sources=data.sourceKeys.Select(k=>Resolve(root.transform,k)).ToArray();
            // The atlas replaces the older generated render chunks; remove them from the
            // review copy only, so they cannot double-render or restore the old visuals.
            var opt=root.GetComponent<RailSceneryChunks>();
            if(opt){if(opt.chunkRoot)UnityEngine.Object.DestroyImmediate(opt.chunkRoot);UnityEngine.Object.DestroyImmediate(opt);}
            var owner=root.GetComponent<RailBakedBlockLighting>();
            var bindings=owner.bindings.Where(b=>b.renderer).ToList();
            var visual=new GameObject("Blender low-poly Motel - atlas review");visual.transform.SetParent(root.transform,false);
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
                    if(count++==0)AssetDatabase.CreateAsset(mesh,Folder+"/MotelAtlasMeshes.asset");
                    else AssetDatabase.AddObjectToAsset(mesh,Folder+"/MotelAtlasMeshes.asset");
                    var settings=part.settings.Split(',');
                    var go=new GameObject(part.name);go.transform.SetParent(visual.transform,false);go.layer=int.Parse(settings[0]);
                    go.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=string.IsNullOrEmpty(part.materialPath)?mat:AssetDatabase.LoadAssetAtPath<Material>(part.materialPath);
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
            Debug.Log($"Full Motel low-poly review: {sources.Length} source renderers replaced by {data.parts.Length} atlas/repeat chunks. Original collision/gameplay retained.");
        }
        finally {PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
        if(!AssetDatabase.CopyAsset(Bake,ScenePath))throw new IOException("Could not copy review lighting scene.");
        Open();
    }
    [MenuItem("Duck Rails/Authoring/Motel/Open low-poly Blender review")]
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
        RestoreBakedLightOutputs(review);
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.objects=Array.Empty<UnityEngine.Object>();
        var view=SceneView.lastActiveSceneView;
        if(view){view.sceneLighting=true;view.sceneViewState.showFog=false;view.drawGizmos=false;view.LookAt(new Vector3(0,3,80),Quaternion.LookRotation(new Vector3(1,-1,1)),65,false,true);view.Repaint();}
        Debug.Log("Full Motel low-poly atlas review open with original baked lighting. No Play Mode/device run.");
    }
    static void RestoreBakedLightOutputs(GameObject review)
    {
        // LightingData owns per-light bake output, which cannot be persisted on a
        // copied instance. Keep baked fixtures disabled ONLY in this review scene
        // to avoid realtime editor preview on top of their existing lightmaps/probes.
        // Both source and review prefab light settings remain untouched.
        var bakedScene=EditorSceneManager.OpenPreviewScene(Bake);
        try
        {
            var bakedRoot=bakedScene.GetRootGameObjects().Single(g=>g.GetComponent<RailBlock>());
            foreach(var sourceLight in bakedRoot.GetComponentsInChildren<Light>(true))
            {
                var indices=new System.Collections.Generic.List<int>();
                for(var t=sourceLight.transform;t!=bakedRoot.transform;t=t.parent)indices.Insert(0,t.GetSiblingIndex());
                var target=review.transform;
                foreach(var index in indices)target=target.GetChild(index);
                var light=target.GetComponent<Light>();
                if(!light)throw new InvalidOperationException("Missing original light in review: "+sourceLight.name);
                light.bakingOutput=sourceLight.bakingOutput;
                if(sourceLight.bakingOutput.isBaked && sourceLight.lightmapBakeType==LightmapBakeType.Baked)
                {
                    light.enabled=false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(light);
                }
            }
        }
        finally {EditorSceneManager.ClosePreviewScene(bakedScene);}
    }
}
#endif
