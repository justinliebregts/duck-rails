#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

/// <summary>Offline import and isolated presentation of the approved logo.</summary>
public static class RailLogoArt
{
    public const string Folder = "Assets/DuckRails/Branding/Logo";
    [MenuItem("Duck Rails/Art/Build Logo Prefab")]
    public static void Build()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Duck Rails Edit Mode required.");
        AssetDatabase.Refresh();
        var importer=(TextureImporter)AssetImporter.GetAtPath(Folder+"/LogoPixels.png");
        importer.filterMode=FilterMode.Point;importer.wrapMode=TextureWrapMode.Clamp;
        importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.Uncompressed;
        importer.maxTextureSize=256;importer.anisoLevel=0;importer.SaveAndReimport();
        var modelImporter=(ModelImporter)AssetImporter.GetAtPath(Folder+"/DuckRailsLogo.fbx");
        modelImporter.materialImportMode=ModelImporterMaterialImportMode.None;
        modelImporter.importCameras=false;modelImporter.importLights=false;
        modelImporter.importAnimation=false;modelImporter.generateSecondaryUV=true;
        modelImporter.SaveAndReimport();
        var mat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/LogoPixelMatte.mat");
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,Folder+"/LogoPixelMatte.mat");}
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/LogoPixels.png"));
        mat.SetColor("_BaseColor",Color.white);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",0);
        mat.SetFloat("_SpecularHighlights",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
        EditorUtility.SetDirty(mat);
        var root=new GameObject("Duck Rails Logo");
        try
        {
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/DuckRailsLogo.fbx"));
            model.transform.SetParent(root.transform,false);
            model.transform.localRotation=Quaternion.Euler(0,180,0);
            foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterial=mat;
            var combined=new Mesh{name="Duck Rails Logo"};
            combined.CombineMeshes(model.GetComponentsInChildren<MeshFilter>().Select(f=>new CombineInstance{mesh=f.sharedMesh,transform=root.transform.worldToLocalMatrix*f.transform.localToWorldMatrix}).ToArray(),true,true);
            var saved=AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/DuckRailsLogoMesh.asset");
            if(!saved){AssetDatabase.CreateAsset(combined,Folder+"/DuckRailsLogoMesh.asset");saved=combined;}
            else{EditorUtility.CopySerialized(combined,saved);UnityEngine.Object.DestroyImmediate(combined);EditorUtility.SetDirty(saved);}
            UnityEngine.Object.DestroyImmediate(model);
            root.AddComponent<MeshFilter>().sharedMesh=saved;
            root.AddComponent<MeshRenderer>().sharedMaterial=mat;
            PrefabUtility.SaveAsPrefabAsset(root,Folder+"/DuckRailsLogo.prefab");
        }
        finally{UnityEngine.Object.DestroyImmediate(root);}
        AssetDatabase.SaveAssets();
        Debug.Log("Logo prefab saved: "+Folder+"/DuckRailsLogo.prefab");
    }

    [MenuItem("Duck Rails/Art/Render Logo Spotlight Preview")]
    public static void Preview()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first.");
        var previous=SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var previousRT=RenderTexture.active;
        RenderTexture rt=null;Texture2D image=null;
        try
        {
            SceneManager.SetActiveScene(scene);
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight=new Color(.13f,.14f,.19f);RenderSettings.fog=false;
            var logo=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/DuckRailsLogo.prefab"),scene);
            foreach(var t in logo.GetComponentsInChildren<Transform>())t.gameObject.layer=31;
            var bounds=new Bounds();bool first=true;
            foreach(var r in logo.GetComponentsInChildren<Renderer>()){if(first){bounds=r.bounds;first=false;}else bounds.Encapsulate(r.bounds);}
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Preview wall";
            wall.layer=31;
            wall.transform.position=bounds.center+new Vector3(0,0,.27f);wall.transform.localScale=new Vector3(14,10,.2f);
            var wallMat=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/PreviewWall.mat");
            if(!wallMat){wallMat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(wallMat,Folder+"/PreviewWall.mat");}
            wallMat.SetColor("_BaseColor",new Color(.055f,.065f,.08f));wallMat.SetFloat("_Smoothness",0);
            wall.GetComponent<Renderer>().sharedMaterial=wallMat;EditorUtility.SetDirty(wallMat);
            var light=new GameObject("Warm spotlight").AddComponent<Light>();light.type=LightType.Spot;
            light.transform.position=bounds.center+new Vector3(-3,4,-4);light.transform.LookAt(bounds.center);
            light.color=new Color(1,.90f,.78f);light.intensity=65;light.range=20;light.spotAngle=85;
            light.cullingMask=1<<31;
            light.shadows=LightShadows.Soft;light.shadowBias=.015f;light.shadowNormalBias=.02f;
            var fill=new GameObject("Faint cool fill").AddComponent<Light>();fill.type=LightType.Point;
            fill.transform.position=bounds.center+new Vector3(4,0,-3);fill.color=new Color(.4f,.53f,1);fill.intensity=.65f;fill.range=12;
            fill.cullingMask=1<<31;
            var camera=new GameObject("Logo preview camera").AddComponent<Camera>();
            camera.transform.position=bounds.center+new Vector3(2.2f,.7f,-10.6f);camera.transform.LookAt(bounds.center);
            camera.fieldOfView=33;camera.nearClipPlane=.05f;camera.farClipPlane=40;
            camera.cullingMask=1<<31;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.015f,.018f,.025f);
            var data=camera.gameObject.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
            data.renderShadows=true;data.renderPostProcessing=false;
            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveScene(scene,Folder+"/LogoSpotlightPreview.unity");
            rt=new RenderTexture(1600,1100,24);camera.targetTexture=rt;camera.Render();
            RenderTexture.active=rt;image=new Texture2D(1600,1100,TextureFormat.RGB24,false);
            image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();
            Directory.CreateDirectory("docs/validation/logo");File.WriteAllBytes("docs/validation/logo/spotlight.png",image.EncodeToPNG());
            camera.targetTexture=null;
            int triangles=logo.GetComponentsInChildren<MeshFilter>().Sum(f=>f.sharedMesh.triangles.Length/3);
            File.WriteAllText("docs/validation/logo/editor-checks.txt","Unity imported logo: "+triangles+" triangles; "+logo.GetComponentsInChildren<MeshRenderer>().Length+" renderers; bounds="+bounds+"; 256px point-filtered atlas; URP spotlight preview rendered.\n");
            Debug.Log("Logo preview saved. Bounds "+bounds+" triangles="+triangles);
        }
        finally
        {
            RenderTexture.active=previousRT;if(image)UnityEngine.Object.DestroyImmediate(image);
            if(rt){rt.Release();UnityEngine.Object.DestroyImmediate(rt);}
            EditorSceneManager.CloseScene(scene,true);SceneManager.SetActiveScene(previous);
        }
    }
}
#endif
