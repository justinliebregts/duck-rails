#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>Reviewed Blender radio artwork, retaining the original station/button identities.</summary>
public static class RailRadioConsoleArt
{
    const string Folder = "Assets/DuckRails/Train/RadioConsole";
    const string Radio = "Assets/DuckRails/Train/Prefabs/RadioTable.prefab";
    const string Train = "Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab";
    const string Art = "Integrated radio console";
    static readonly Vector3 Anchor = new Vector3(.055f, .475f + .165f / .37f * .075f, -.56f);
    static readonly Quaternion Slope = Quaternion.Euler(0, 0, Mathf.Atan(.075f / .37f) * Mathf.Rad2Deg);
    static void Guard()
    {
        if (Application.dataPath != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying)
            throw new InvalidOperationException("Duck Rails Edit Mode required");
    }
    static Mesh SaveMesh(RailCockpitV2.Part part)
    {
        string path = Folder + "/" + part.name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh) { mesh = new Mesh { name = part.name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear();
        mesh.vertices = part.name == "TalkButton"
            ? part.vertices.Select(v => Quaternion.Inverse(Slope) * (v - Anchor)).ToArray() : part.vertices;
        mesh.uv = part.uv; mesh.triangles = part.triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
        return mesh;
    }
    [MenuItem("Duck Rails/Art/Update Radio Button Geometry")]
    public static void UpdateButtonGeometry()
    {
        Guard();
        var model=JsonUtility.FromJson<RailCockpitV2.Model>(File.ReadAllText(Folder+"/geometry.json"));
        foreach(var p in model.parts.Where(p=>p.name=="TalkButton" || p.name=="ButtonMount"))SaveMesh(p);
        AssetDatabase.SaveAssets();Validate();
    }
    static Transform Child(Transform parent, string name)
    {
        var t = parent.Find(name);
        if (!t) { t = new GameObject(name).transform; t.SetParent(parent, false); }
        t.localPosition = Vector3.zero; t.localRotation = Quaternion.identity; t.localScale = Vector3.one;
        t.gameObject.layer = 6; return t;
    }
    static void Draw(Transform t, Mesh mesh, Material material)
    {
        var f = t.GetComponent<MeshFilter>(); if (!f) f = t.gameObject.AddComponent<MeshFilter>(); f.sharedMesh = mesh;
        var r = t.GetComponent<MeshRenderer>(); if (!r) r = t.gameObject.AddComponent<MeshRenderer>();
        r.sharedMaterial = material; r.enabled = true;
        r.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
        GameObjectUtility.SetStaticEditorFlags(t.gameObject, 0);
    }
    static void Box(Transform t, Vector3 center, Vector3 size)
    {
        var c = t.GetComponent<BoxCollider>(); if (!c) c = t.gameObject.AddComponent<BoxCollider>();
        c.center = center; c.size = size; c.isTrigger = false; c.enabled = true;
    }
    [MenuItem("Duck Rails/Art/Install Approved Radio Console")]
    public static void Install()
    {
        Guard();
        var tex = (TextureImporter)AssetImporter.GetAtPath(Folder + "/Palette.png");
        tex.filterMode = FilterMode.Point; tex.wrapMode = TextureWrapMode.Clamp;
        tex.textureCompression = TextureImporterCompression.Uncompressed; tex.mipmapEnabled = false; tex.SaveAndReimport();
        var model = JsonUtility.FromJson<RailCockpitV2.Model>(File.ReadAllText(Folder + "/geometry.json"));
        var meshes = model.parts.Where(p => p.name != "Label").ToDictionary(p => p.name, SaveMesh);
        string materialPath = Folder + "/Console.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (!mat) { mat = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat, materialPath); }
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Palette.png"));
        mat.SetColor("_BaseColor", Color.white); mat.SetFloat("_Smoothness", 0);
        mat.SetFloat("_SpecularHighlights", 0); mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF"); EditorUtility.SetDirty(mat);
        var root = PrefabUtility.LoadPrefabContents(Radio);
        try
        {
            var control = root.GetComponentInChildren<RailControl>(true);
            var hold = control.GetComponent<RailHoldControl>(); var button = hold.button; var cap = button.cap;
            // Exact serialized gameplay configuration must survive the artwork migration.
            var snapshots = new[] { (MonoBehaviour)control, hold, button }.ToDictionary(c => c, c => EditorJsonUtility.ToJson(c));
            var label = root.GetComponentInChildren<TMP_Text>(true);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true)) r.enabled = false;
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) if (c != button.GetComponent<BoxCollider>()) c.enabled = false;
            // Keep original objects/file IDs for external references, but remove all obsolete solids/visuals.
            foreach (Transform t in root.transform)
                if (t.name != "Radio talk" && t.name != Art && t != label.transform) t.gameObject.SetActive(false);
            var art = Child(root.transform, Art);
            foreach (var pair in meshes.Where(p => p.Key != "TalkButton"))
            {
                var t = Child(art, pair.Key); Draw(t, pair.Value, mat);
                if (pair.Key == "Shelf" || pair.Key == "Supports" || pair.Key == "MicBase")
                {
                    var c = t.GetComponent<MeshCollider>(); if (!c) c = t.gameObject.AddComponent<MeshCollider>();
                    c.sharedMesh = pair.Value; c.convex = false; c.isTrigger = false; c.enabled = true;
                }
                if (pair.Key == "Speaker") Box(t, new Vector3(.115f, .642f, .26f), new Vector3(.31f, .444f, .70f));
                if (pair.Key == "Microphone")
                {
                    Box(Child(t, "Head collision"), new Vector3(.19f, .84f, -.56f), new Vector3(.085f, .205f, .19f));
                    Box(Child(t, "Stem collision"), new Vector3(.202f, .655f, -.56f), new Vector3(.046f, .25f, .044f));
                }
            }
            control.transform.localPosition = Anchor; control.transform.localRotation = Slope;
            button.transform.localPosition = Vector3.zero; button.transform.localRotation = Quaternion.identity;
            button.transform.localScale = Vector3.one;
            cap.localPosition = Vector3.zero; cap.localRotation = Quaternion.identity; cap.localScale = Vector3.one;
            Draw(cap, meshes["TalkButton"], mat);
            var touch = button.GetComponent<BoxCollider>();
            touch.center = new Vector3(0, .051f, 0); touch.size = new Vector3(.23f, .075f, .23f); touch.isTrigger = true; touch.enabled = true;
            // Retain the existing TMP identity and use the project's mandated SDF font.
            label.transform.SetParent(art, false); label.gameObject.SetActive(true);
            label.transform.localPosition = new Vector3(-.119f, .449f, -.56f);
            label.transform.localRotation = Quaternion.Euler(0, 90, 0); label.transform.localScale = Vector3.one;
            label.text = "HOLD TO TALK"; label.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            label.fontSharedMaterial = label.font.material; label.color = new Color(.91f, .84f, .61f);
            label.enableAutoSizing = false; label.fontSize = .40f; label.alignment = TextAlignmentOptions.Center;
            label.rectTransform.sizeDelta = new Vector2(.34f, .05f); label.GetComponent<MeshRenderer>().enabled = true;
            foreach (var pair in snapshots)
                if (EditorJsonUtility.ToJson(pair.Key) != pair.Value) throw new InvalidOperationException("Gameplay configuration changed: " + pair.Key);
            PrefabUtility.SaveAsPrefabAsset(root, Radio);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        // Train-specific probe anchor remains on the shared train, not inside the reusable radio asset.
        root = PrefabUtility.LoadPrefabContents(Train);
        try
        {
            var a = root.GetComponent<RailTrainAssembly>(); var anchor = root.transform.Find("Train light probe anchor");
            foreach (var r in a.radio.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy))
                r.probeAnchor = anchor;
            PrefabUtility.SaveAsPrefabAsset(root, Train);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets(); Validate();
    }
    [MenuItem("Duck Rails/Train/Validate Radio Console")]
    public static void Validate()
    {
        Guard(); var lines = new List<string>();
        Action<bool,string> check = (ok, message) => { if (!ok) throw new InvalidOperationException(message); lines.Add("PASS " + message); };
        foreach (string path in new[] { Radio, Train })
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var radio = path == Train ? root.GetComponent<RailTrainAssembly>().radio : root.transform;
            var hold = radio.GetComponentInChildren<RailHoldControl>(); var button = hold.button;
            check(hold.radio && !hold.bridge && hold.GetComponent<RailControl>().action == RailControl.Action.Radio, path + " station action/voice routing");
            check(button && button.cap && button.interactable && !button.diagnostic && button.pressOffset == new Vector3(0,-.018f,0), "Original momentary button settings and cap reference");
            check(button.GetComponent<BoxCollider>().isTrigger && button.GetComponent<BoxCollider>().enabled, "Physical contact trigger enabled");
            check(radio.GetComponentsInChildren<PressableButton>().Length == 1 && radio.GetComponentsInChildren<RailHoldControl>().Length == 1, "Exactly one talk button and hold controller");
            check(radio.GetComponentsInChildren<MeshRenderer>().Count(r=>r.enabled)==9, "Eight Blender mesh renderers and one retained TMP label");
            check(radio.GetComponentsInChildren<TMP_Text>().Single().font.name == "CutePixel SDF", "CutePixel SDF label");
            check(!radio.Find("Tabletop").gameObject.activeSelf && radio.Cast<Transform>().Where(t=>t.name.StartsWith("Table leg")).All(t=>!t.gameObject.activeSelf), "Old tabletop and legs inactive");
            check(radio.GetComponentsInChildren<Collider>().Count(c=>c.enabled && !c.isTrigger)==6, "Six aligned static console collision shapes");
            var mesh=button.cap.GetComponent<MeshFilter>().sharedMesh;var touch=button.GetComponent<BoxCollider>();
            check(mesh.vertices.All(v=>Mathf.Abs(v.x)<=touch.size.x/2+.0001f && Mathf.Abs(v.z)<=touch.size.z/2+.0001f && Mathf.Abs(v.y-touch.center.y)<=touch.size.y/2+.0001f), "Entire raised cap inside fixed touch volume");
            check(Vector3.Dot(button.transform.TransformDirection(button.pressOffset).normalized, radio.TransformDirection(Slope*Vector3.down))>.999f, "Cap presses into sloped base");
            check(radio.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0), "No missing scripts");
            if(path==Train)
            {
                var shelf=radio.Find(Art+"/Shelf").GetComponent<MeshFilter>();
                var v=shelf.sharedMesh.vertices.Select(p=>root.transform.InverseTransformPoint(shelf.transform.TransformPoint(p))).ToArray();
                check(Mathf.Abs(v.Min(p=>p.z)-1.14f)<.0001f && Mathf.Abs(v.Max(p=>p.z)-2.86f)<.0001f,"Shelf spans wall with 10 mm end clearances");
                check(radio.GetComponentsInChildren<MeshRenderer>().Where(r=>r.enabled).All(r=>r.probeAnchor==root.transform.Find("Train light probe anchor")),"New art uses moving-train probe anchor");
            }
        }
        Directory.CreateDirectory("docs/validation/radio-console");
        File.WriteAllText("docs/validation/radio-console/editmode.txt",string.Join("\n",lines)+"\nPlay Mode, headset and multiplayer not run; queued for playtest.\n");
        Debug.Log("Radio console: "+lines.Count+" saved-prefab checks PASS; live acceptance queued.");
    }

    // Bake in a disposable additive scene so the user's unsaved Compound edits stay open.
    public static void BakeNavigation()
    {
        Guard();
        var previous = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var external = Resources.FindObjectsOfTypeAll<Collider>()
            .Where(c=>c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded && c.enabled).ToArray();
        var blockers = Resources.FindObjectsOfTypeAll<Game.AI.CrawlGraphBlocker>()
            .Where(c=>c.gameObject.scene.IsValid() && c.gameObject.scene.isLoaded && c.enabled).ToArray();
        var scene = UnityEditor.SceneManagement.EditorSceneManager.NewScene(UnityEditor.SceneManagement.NewSceneSetup.EmptyScene, UnityEditor.SceneManagement.NewSceneMode.Additive);
        try
        {
            foreach(var c in external)if(c)c.enabled=false;
            foreach(var c in blockers)if(c)c.enabled=false;
            var train=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Train),scene);
            train.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var baker=new GameObject("Temporary console navigation bake").AddComponent<Game.AI.CrawlGraphBaker>();
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(baker.gameObject,scene);
            baker.transform.position=new Vector3(0,1,0);baker.size=new Vector3(5,4,11);
            baker.cellSize=.25f;baker.agentRadius=.35f;baker.surfaceMask=1<<6;baker.allowCeilings=false;baker.minIslandNodes=4;
            var seed=new GameObject("Deck seed").transform;UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(seed.gameObject,scene);
            seed.position=new Vector3(0,.1f,-3);baker.seeds=new[]{seed};
            baker.graphAsset=AssetDatabase.LoadAssetAtPath<TextAsset>(RailEnemyBoardingAuthoring.GraphPath);
            Physics.SyncTransforms();
            if(!Game.AI.Editor.CrawlGraphBake.Bake(baker,false))throw new Exception("Train navigation bake failed");
            var graph=Game.AI.CrawlGraph.Load(AssetDatabase.LoadAssetAtPath<TextAsset>(RailEnemyBoardingAuthoring.GraphPath));
            var nav=train.GetComponent<RailTrainNavigation>();
            for(int i=0;i<RailTrainNavigation.OpeningCount;i++)
                if(graph.FindNearestGroundNode(nav.Landing(i),.8f)<0)throw new Exception("Missing boarding landing "+i);
            File.WriteAllText("docs/validation/radio-console/navigation.txt","PASS offline train navigation rebaked: "+graph.NodeCount+" nodes, "+graph.EdgeCount+" edges; rear and both side landing nodes present.\nUser scene retained; external collider states restored in finally. No Play Mode run.\n");
        }
        finally
        {
            UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene,true);
            foreach(var c in external)if(c)c.enabled=true;
            foreach(var c in blockers)if(c)c.enabled=true;
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(previous);Physics.SyncTransforms();
        }
    }

    public static void RenderPreview(bool closeup = false)
    {
        Guard();var root=PrefabUtility.LoadPrefabContents(Train);
        var oldAmbient=RenderSettings.ambientLight;var oldMode=RenderSettings.ambientMode;
        RenderTexture target=null;Texture2D image=null;Camera camera=null;
        try
        {
            foreach(var t in root.GetComponentsInChildren<Transform>(true))t.gameObject.layer=31;
            var cameraObject=new GameObject("Radio review camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraObject,root.scene);
            camera=cameraObject.AddComponent<Camera>();camera.transform.position=new Vector3(-1.4f,1.9f,.9f);
            camera.scene=root.scene;
            camera.transform.LookAt(new Vector3(1.64f,.52f,2));camera.orthographic=true;camera.orthographicSize=.83f;
            if(closeup)
            {
                camera.transform.position=new Vector3(.5f,1.45f,1.54f);
                camera.transform.LookAt(new Vector3(1.605f,.52f,1.54f));camera.orthographicSize=.25f;
            }
            camera.cullingMask=1<<31;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.23f,.27f,.30f);camera.allowHDR=false;
            foreach(var pos in new[]{new Vector3(-1,3,4),new Vector3(-1,2,0)})
            {
                var go=new GameObject("Radio review light");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,root.scene);
                var light=go.AddComponent<Light>();light.type=LightType.Directional;light.intensity=.9f;light.cullingMask=1<<31;go.transform.position=pos;go.transform.LookAt(new Vector3(1.64f,.5f,2));
            }
            RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Flat;RenderSettings.ambientLight=Color.gray;
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true))if(text.gameObject.activeInHierarchy)text.ForceMeshUpdate();
            target=new RenderTexture(1400,1000,24);camera.targetTexture=target;camera.Render();
            var previous=RenderTexture.active;RenderTexture.active=target;
            try{image=new Texture2D(1400,1000,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();}
            finally{RenderTexture.active=previous;}
            File.WriteAllBytes("docs/validation/radio-console/"+(closeup?"button-preview.png":"unity-preview.png"),image.EncodeToPNG());
        }
        finally
        {
            RenderSettings.ambientLight=oldAmbient;RenderSettings.ambientMode=oldMode;
            if(camera)camera.targetTexture=null;
            if(target){target.Release();Object.DestroyImmediate(target);}if(image)Object.DestroyImmediate(image);
            // Closing the prefab preview scene also removes its temporary camera/lights.
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}
#endif
