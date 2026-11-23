#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public static class RailNuclearFullBlock
{
    public const string RootName = "Full plant site";
    const string Folder = RailNuclearPlant.Folder + "/FullBlock";
    const string Model = Folder + "/Plant site.fbx";
    [Serializable] class Site { public Swatch[] materials; public Fixture[] lights; public Label[] labels; }
    [Serializable] class Swatch { public string name, texture, color; public float emission; }
    [Serializable] class Fixture { public string name; public float x,y,z,power; public float[] color; }
    [Serializable] class Label { public string text; public float x,y,z,size; }
    public static Vector3 AcrossTrack(Vector3 p) => new Vector3(-p.x, p.y, p.z);

    static void MirrorHierarchy(Transform t)
    {
        t.localPosition = AcrossTrack(t.localPosition);
        var r = t.localRotation;
        t.localRotation = new Quaternion(r.x,-r.y,-r.z,r.w);
        foreach (Transform child in t) MirrorHierarchy(child);
    }

    static void PrepareModel(Site site)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
        importer.importAnimation = false;
        importer.importLights = false;
        importer.importCameras = false;
        importer.generateSecondaryUV = true;
        importer.isReadable = true;
        foreach (var swatch in site.materials)
        {
            var path = Folder + "/" + swatch.name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!material) {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material,path);
            }
            if (!ColorUtility.TryParseHtmlString("#"+swatch.color, out Color tint)) throw new InvalidDataException(swatch.name);
            material.SetColor("_BaseColor", tint);
            material.SetFloat("_Smoothness", .16f);
            if (!string.IsNullOrEmpty(swatch.texture))
                material.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/" + swatch.texture + ".png"));
            if(swatch.emission > 0) {
                material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",tint*swatch.emission);
                material.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;
            }
            EditorUtility.SetDirty(material);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),swatch.name),material);
        }
        importer.SaveAndReimport();
    }

    static void ApplySiteMaterials(Transform root)
    {
        foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            var originals = renderer.sharedMaterials;
            for (int i = 0; i < originals.Length; i++)
            {
                if (!originals[i]) continue;
                string swatch = originals[i].name switch
                {
                    "Plant concrete" => "Plant ivory",
                    "Plant steel" => "Galvanized steel",
                    "Plant teal" => "Oxidized enamel",
                    "Plant grass" => "Plant ground",
                    "Dark gravel" => "Aggregate",
                    "Weathered timber" => "Cedar",
                    "Basin water" => "Settling water",
                    "Dark glass" => "Cold glazing",
                    "Insulator ivory" => "Ceramic",
                    "Green displays" => "Lit glazing",
                    "Beacon red" => "Red marker",
                    "Small waste spill" => "Groundcover",
                    "Waste drum ochre" => "Work equipment",
                    _ => null
                };
                if (swatch != null) originals[i] = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + swatch + ".mat");
            }
            renderer.sharedMaterials = originals;
        }
    }
    [MenuItem("Duck Rails/Authoring/Nuclear Plant/Install full site on existing prefab")]
    public static void Install()
    {
        if(EditorApplication.isPlaying || Lightmapping.isRunning) throw new InvalidOperationException("Install in Edit Mode with the lightmapper idle.");
        var site = JsonUtility.FromJson<Site>(File.ReadAllText("ArtSource/NuclearFullBlock/site.json"));
        var instance = PrefabUtility.LoadPrefabContents(RailNuclearPlant.PrefabPath);
        try {
            var root = RailBlock.FindEnvironment(instance.transform,RailNuclearPlant.RootName);
            if(!root) throw new InvalidOperationException("Missing authored plant.");
            if(root.Find(RootName)) throw new InvalidOperationException("The playable plant already contains the finished site.");
            PrepareModel(site);
            ApplySiteMaterials(root);
            foreach(Transform child in root)
            {
                switch(child.name)
                {
                    case "Tower A damp": case "Tower B drained": case "Turbine hall": case "Reactor containment exterior": case "Hall arrival path": case "Hall rear exit path":
                        MirrorHierarchy(child);break;
                    case "Baked plant light": case "Light fixture":
                        if(child.localPosition.z >= 55 && child.localPosition.z <= 113) MirrorHierarchy(child);
                        break;
                    case "Old parking": case "Abandoned car body": case "Abandoned car cabin": case "Tree crown": case "Tree trunk":
                        child.gameObject.SetActive(false);break;
                }
            }
            foreach(var marker in instance.GetComponentsInChildren<RailSpawnMarker>(true))
                marker.transform.position = AcrossTrack(marker.transform.position);
            var siteRoot = new GameObject(RootName).transform;
            siteRoot.SetParent(root,false);
            var meshRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model),siteRoot);
            meshRoot.transform.localRotation=Quaternion.Euler(0,180,0);
            // Keep model mesh references live and make the physical dressing hand-editable.
            PrefabUtility.UnpackPrefabInstance(meshRoot,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
            foreach(var r in meshRoot.GetComponentsInChildren<MeshRenderer>())
            {
                var g=r.gameObject;
                var mesh=g.GetComponent<MeshFilter>().sharedMesh;
                if(!mesh || !r.sharedMaterial || mesh.uv2.Length!=mesh.vertexCount) throw new InvalidDataException(g.name);
                GameObjectUtility.SetStaticEditorFlags(g,StaticEditorFlags.ContributeGI | StaticEditorFlags.BatchingStatic);
                g.layer=6;
                r.scaleInLightmap=.45f; r.receiveGI=ReceiveGI.Lightmaps;
                if(g.name.EndsWith(" solid",StringComparison.Ordinal)) g.AddComponent<MeshCollider>().sharedMesh=mesh;
            }
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            foreach(var data in site.labels) {
                var label=new GameObject(data.text).AddComponent<TextMeshPro>();
                label.transform.SetParent(siteRoot,false);label.transform.localPosition=new Vector3(data.x,data.y,data.z-.02f);
                label.font=font;label.fontSharedMaterial=font.material;label.text=data.text;
                label.fontSize=data.size*6;label.alignment=TextAlignmentOptions.Center;
                label.rectTransform.sizeDelta=new Vector2(data.size*16,data.size*2);
                label.color=new Color(1f,.9f,.73f);label.ForceMeshUpdate();
            }
            foreach(var data in site.lights) {
                var l=new GameObject(data.name).AddComponent<Light>();l.transform.SetParent(siteRoot,false);
                l.transform.localPosition=new Vector3(data.x,data.y,data.z);l.type=LightType.Point;l.lightmapBakeType=LightmapBakeType.Baked;
                l.range=data.power>600?17:11;l.intensity=data.power*.043f;l.shadows=LightShadows.Soft;
                l.color=new Color(data.color[0],data.color[1],data.color[2]);
            }
            PrefabUtility.SaveAsPrefabAsset(instance,RailNuclearPlant.PrefabPath,out bool saved);
            if(!saved)throw new IOException("Failed to save nuclear prefab.");
        }finally{PrefabUtility.UnloadPrefabContents(instance);}
        AssetDatabase.SaveAssets();
    }
}
#endif
