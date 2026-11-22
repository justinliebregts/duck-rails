using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public static class LIVCameraAssetTools
{
    const string Folder = "Assets/Models/LIVCamera/";
    const string PrefabPath = "Assets/Resources/RemoteLIVCamera.prefab";

    [MenuItem("Tools/DuckZ/LIV Camera/Render Imported Preview")]
    public static void RenderPreview()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();
        var oldTarget = RenderTexture.active;
        var target = RenderTexture.GetTemporary(1200, 800, 24);
        Texture2D image = null;
        try
        {
            var prop = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath), scene);
            // Show the actual imported game mesh/material from player height and obliquely.
            var cameraGO = new GameObject("Camera preview");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraGO, scene);
            var camera = cameraGO.AddComponent<Camera>(); camera.enabled = false; camera.scene = scene;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f, .075f, .09f);
            camera.orthographic = true; camera.orthographicSize = .28f;
            camera.nearClipPlane = .01f; camera.farClipPlane = 10;
            var lightGO = new GameObject("Preview key");
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(lightGO, scene);
            var light = lightGO.AddComponent<Light>(); light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(35, 210, 0);
            camera.targetTexture = target;
            foreach (var shot in new[] { ("front", new Vector3(0, .08f, 1)), ("angle", new Vector3(.65f, .4f, 1)) })
            {
                camera.transform.position = shot.Item2;
                camera.transform.LookAt(new Vector3(0, .08f, 0));
                prop.GetComponent<RemoteLIVCamera>().ApplyView(camera);
                camera.Render(); // Let the newly-created preview scene register its renderers.
                camera.Render(); RenderTexture.active = target;
                image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0); image.Apply();
                File.WriteAllBytes("Tools/LIVCamera/Unity-" + shot.Item1 + ".png", image.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(image); image = null;
            }
        }
        finally
        {
            if (image != null) UnityEngine.Object.DestroyImmediate(image);
            RenderTexture.active = oldTarget;
            RenderTexture.ReleaseTemporary(target);
            UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [MenuItem("Tools/DuckZ/LIV Camera/Create Missing Remote Assets")]
    public static void CreateMissingAssets()
    {
        // Only new output gets defaults. Existing assets/importer settings are never replaced.
        string meshPath = Folder + "RemoteLIVCamera.asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
        if (mesh == null)
        {
            var source = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "RemoteLIVCamera.fbx");
            var filter = source.GetComponentInChildren<MeshFilter>();
            mesh = new Mesh { name = "RemoteLIVCamera" };
            mesh.CombineMeshes(new[] { new CombineInstance { mesh = filter.sharedMesh,
                transform = filter.transform.localToWorldMatrix } }, true, true);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, meshPath);
        }
        string materialPath = Folder + "RemoteLIVCamera.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "RemoteLIVCamera", enableInstancing = true };
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "RemoteLIVCameraAtlas.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0);
            material.SetFloat("_SpecularHighlights", 0);
            material.SetFloat("_EnvironmentReflections", 0);
            material.SetFloat("_ReceiveShadows", 0);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            material.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            AssetDatabase.CreateAsset(material, materialPath);
        }
        if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
        {
            var go = new GameObject("RemoteLIVCamera");
            try
            {
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.lightProbeUsage = LightProbeUsage.Off;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
                go.AddComponent<RemoteLIVCamera>();
                PrefabUtility.SaveAsPrefabAsset(go, PrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(go); }
        }
        LIVCameraChecks.Run();
    }

    public static void Inspect()
    {
        if (!string.Equals(Path.GetFullPath(Application.dataPath), @"C:\duckz\Assets", StringComparison.OrdinalIgnoreCase))
            throw new Exception("Wrong project: " + Application.dataPath);
        Debug.Log("LIV_PROJECT_VERIFIED " + Application.dataPath);
        foreach (var path in new[] { "Assets/Prefabs/Interface/CameraTablet.prefab", "Assets/Models/LIVCamera/RemoteLIVCamera.fbx" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log("LIV_MODEL " + path);
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                Debug.Log("LIV_GEOMETRY " + filter.name + " local=" + filter.transform.localPosition + " world=" + filter.transform.position
                    + " rotation=" + filter.transform.rotation.eulerAngles + " scale=" + filter.transform.lossyScale
                    + " bounds=" + mesh.bounds + " worldBounds=" + filter.GetComponent<Renderer>().bounds);
            }
        }
    }
}
