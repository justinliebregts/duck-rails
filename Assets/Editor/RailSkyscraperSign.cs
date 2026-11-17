#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

// A separately baked fascia preserves every existing derived atlas UV and binding.
public static class RailSkyscraperSign
{
    public const string Folder = "Assets/DuckRails/SkyscraperSign";
    const string Scene = Folder + "/Sign-bake.unity";
    public const string LightingCompanion = Folder + "/SignLighting.unity";
    const string Group = "Raised CutePixel development sign";
    [Serializable] class Data { public float[] vertices; public int[] triangles; }
    static void Idle() { if(Application.dataPath.Replace('\\','/')!="X:/Work/duck-rails/Assets" || Application.isPlaying || Lightmapping.isRunning) throw new Exception("Requires idle Duck Rails editor"); }
    public static void Prepare()
    {
        Idle(); RailSkyscraperAtlasProduction.ValidateSaved();
        if(File.Exists(Scene)) throw new Exception("Sign already prepared");
        for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++) if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty) throw new Exception("Preserve open scene edits first");
        Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var root=new GameObject(Group); root.transform.position=new Vector3(-22,3,9.8f);
        var d=JsonUtility.FromJson<Data>(File.ReadAllText("ArtSource/SkyscraperSign/letters.json"));
        var mesh=new Mesh{name="Solid CutePixel development lettering"};
        mesh.vertices=Enumerable.Range(0,d.vertices.Length/3).Select(i=>new Vector3(d.vertices[i*3],d.vertices[i*3+1],d.vertices[i*3+2])).ToArray();
        mesh.triangles=d.triangles; mesh.RecalculateNormals(); mesh.RecalculateBounds(); Unwrapping.GenerateSecondaryUVSet(mesh);
        AssetDatabase.CreateAsset(mesh,Folder+"/Letters.asset");
        var ivory=Material("Ivory lettering",new Color(.88f,.82f,.63f));
        var board=Material("Charcoal fascia",new Color(.065f,.085f,.09f));
        var metal=Material("Uplight housing",new Color(.12f,.14f,.15f));
        var letters=new GameObject("Solid CutePixel letters"); letters.transform.SetParent(root.transform,false);
        letters.AddComponent<MeshFilter>().sharedMesh=mesh; letters.AddComponent<MeshRenderer>().sharedMaterial=ivory;
        var fascia=GameObject.CreatePrimitive(PrimitiveType.Cube); fascia.name="Sign fascia"; fascia.transform.SetParent(root.transform,false); fascia.transform.localPosition=new Vector3(0,0,.015f); fascia.transform.localScale=new Vector3(9.9f,2.9f,.04f); Object.DestroyImmediate(fascia.GetComponent<Collider>()); fascia.GetComponent<Renderer>().sharedMaterial=board;
        var housing=GameObject.CreatePrimitive(PrimitiveType.Cube); housing.name="Bottom uplight housing"; housing.transform.SetParent(root.transform,false); housing.transform.localPosition=new Vector3(0,-2.65f,-2.2f); housing.transform.localScale=new Vector3(.65f,.25f,.4f); Object.DestroyImmediate(housing.GetComponent<Collider>()); housing.GetComponent<Renderer>().sharedMaterial=metal;
        var light=new GameObject("Baked upward sign spotlight").AddComponent<Light>(); light.transform.SetParent(root.transform,false); light.transform.localPosition=new Vector3(0,-2.45f,-2.2f); light.transform.rotation=Quaternion.LookRotation(root.transform.position-light.transform.position);
        light.type=LightType.Spot; light.lightmapBakeType=LightmapBakeType.Baked; light.intensity=95; light.range=8; light.spotAngle=120; light.innerSpotAngle=85; light.color=new Color(1,.86f,.65f); light.shadows=LightShadows.Soft;
        foreach(var r in root.GetComponentsInChildren<MeshRenderer>()) { GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ContributeGI); r.receiveGI=ReceiveGI.Lightmaps; r.scaleInLightmap=1; }
        RenderSettings.ambientMode=AmbientMode.Flat; RenderSettings.ambientLight=new Color(.015f,.018f,.025f); RenderSettings.fog=false; RenderSettings.skybox=null;
        var settings=new LightingSettings{name="Sign isolated bake",bakedGI=true,realtimeGI=false,lightmapper=LightingSettings.Lightmapper.ProgressiveCPU,lightmapResolution=32,lightmapMaxSize=1024,directionalityMode=LightmapsMode.NonDirectional,directSampleCount=64,indirectSampleCount=128,environmentSampleCount=32,maxBounces=2};
        AssetDatabase.CreateAsset(settings,Folder+"/Sign.lighting"); Lightmapping.lightingSettings=settings;
        EditorSceneManager.SaveScene(scene,Scene); AssetDatabase.SaveAssets(); Lightmapping.BakeAsync();
    }
    static Material Material(string name,Color color) { var m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name}; m.SetColor("_BaseColor",color); m.SetFloat("_Smoothness",.2f); AssetDatabase.CreateAsset(m,Folder+"/"+name+".mat"); return m; }
    public static void Install()
    {
        Idle(); RailSkyscraperAtlasProduction.ValidateSaved();
        if(UnityEngine.SceneManagement.SceneManager.GetActiveScene().path!=Scene) throw new Exception("Open sign bake first");
        var source=GameObject.Find(Group); var maps=LightmapSettings.lightmaps;
        if(!source || source.GetComponentsInChildren<MeshRenderer>().Any(r=>r.lightmapIndex<0||r.lightmapIndex>=maps.Length)) throw new Exception("Sign bake incomplete");
        EditorSceneManager.SaveScene(source.scene);
        var root=PrefabUtility.LoadPrefabContents(RailSkyscraper.PrefabPath);
        try {
            if(root.transform.Find(Group)) throw new Exception("Already installed");
            var old=root.GetComponentsInChildren<TMPro.TextMeshPro>(true).Single();
            old.gameObject.SetActive(false); // Retain original label for recovery, without rendering it.
            var copy=Object.Instantiate(source,root.transform); copy.name=Group; copy.transform.localPosition=source.transform.position;
            var owner=root.GetComponent<RailBakedBlockLighting>(); var bindings=owner.bindings.ToList();
            foreach(var r in source.GetComponentsInChildren<MeshRenderer>()) { var target=copy.transform.Find(r.name).GetComponent<Renderer>(); bindings.Add(new RailBakedBlockLighting.Binding{renderer=target,color=maps[r.lightmapIndex].lightmapColor,scaleOffset=r.lightmapScaleOffset}); }
            owner.bindings=bindings.ToArray(); owner.supplementalLightingScene=LightingCompanion;
            owner.CaptureBakedFixtures(); PrefabUtility.SaveAsPrefabAsset(root,RailSkyscraper.PrefabPath);
        } finally { PrefabUtility.UnloadPrefabContents(root); }
        ExportLightingCompanion();
        AssetDatabase.SaveAssets();
        var saved=AssetDatabase.LoadAssetAtPath<GameObject>(RailSkyscraper.PrefabPath);
        File.WriteAllText("ArtSource/SkyscraperAtlasLowPoly/production-state.json",JsonUtility.ToJson(new RailSkyscraperAtlasProduction.State{guid=AssetDatabase.AssetPathToGUID(RailSkyscraper.PrefabPath),fingerprint=RailSkyscraperAtlasProduction.Fingerprint(saved)},true));
        RailSkyscraperAtlasProduction.ValidateSaved();
    }

    public static void ExportLightingCompanion()
    {
        Idle();
        var source = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (source.path != Scene || source.isDirty || Lightmapping.lightingDataAsset == null)
            throw new InvalidOperationException("Open the saved sign bake with its lighting data first.");
        if (!EditorSceneManager.SaveScene(source, LightingCompanion, true))
            throw new IOException("Could not copy the sign lighting scene.");
        var companion = EditorSceneManager.OpenScene(LightingCompanion, OpenSceneMode.Additive);
        try
        {
            foreach (var root in companion.GetRootGameObjects()) Object.DestroyImmediate(root);
            if (!EditorSceneManager.SaveScene(companion))
                throw new IOException("Could not save the sign lighting companion.");
        }
        finally { EditorSceneManager.CloseScene(companion, true); }
        var scenes = EditorBuildSettings.scenes.Where(scene => scene.path != LightingCompanion).ToList();
        scenes.Add(new EditorBuildSettingsScene(LightingCompanion, true));
        EditorBuildSettings.scenes = scenes.ToArray();
    }
    public static void Capture()
    {
        Idle();
        EditorSceneManager.OpenScene(RailSkyscraper.BakeScene);
        var root=Object.FindFirstObjectByType<RailBlock>().gameObject;
        var maps=LightmapSettings.lightmaps.ToList();
        foreach(var b in root.GetComponent<RailBakedBlockLighting>().bindings) { int i=maps.FindIndex(m=>m.lightmapColor==b.color); if(i<0){i=maps.Count;maps.Add(new LightmapData{lightmapColor=b.color});} b.renderer.lightmapIndex=i; b.renderer.lightmapScaleOffset=b.scaleOffset; }
        LightmapSettings.lightmaps=maps.ToArray(); foreach(var l in root.GetComponentsInChildren<Light>()) l.enabled=false;
        RenderSettings.fog=false;
        var camera=new GameObject("Temporary sign review camera").AddComponent<Camera>(); camera.transform.position=new Vector3(-25,3,0); camera.transform.LookAt(new Vector3(-22,3,9.8f)); camera.fieldOfView=65; camera.farClipPlane=180; camera.clearFlags=CameraClearFlags.SolidColor; camera.backgroundColor=new Color(.015f,.02f,.03f);
        var rt=new RenderTexture(1600,1000,24); var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false); var previous=RenderTexture.active;
        try { camera.targetTexture=rt; camera.Render(); RenderTexture.active=rt; tex.ReadPixels(new Rect(0,0,1600,1000),0,0); tex.Apply(); Directory.CreateDirectory("output/skyscraper-sign"); File.WriteAllBytes("output/skyscraper-sign/review.png",tex.EncodeToPNG()); }
        finally { camera.targetTexture=null; RenderTexture.active=previous; Object.DestroyImmediate(camera.gameObject); Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt); }
        // Discard preview-only light suppression, fog and renderer binding overrides.
        EditorSceneManager.OpenScene(RailSkyscraper.BakeScene);
    }
}
#endif
