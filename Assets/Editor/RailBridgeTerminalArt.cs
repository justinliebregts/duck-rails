#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Imports the approved Blender terminal without rebuilding the finale.</summary>
public static class RailBridgeTerminalArt
{
    public const string Folder = "Assets/DuckRails/BridgeTerminal";
    public const string Finale = "Assets/DuckRails/Blocks/bridge-finale-v1.prefab";
    public const string MechanismClip = "Assets/Audio/Bridge/BridgeLower_Loop.wav";
    const string ArtName = "Bridge terminal cabinet";
    [Serializable] public class Part { public string name; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
    [Serializable] public class Model { public Part[] parts; }
    static Mesh MeshFor(Part p)
    {
        string path = Folder + "/" + p.name + ".asset";
        var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (!mesh) { mesh = new Mesh { name = p.name }; AssetDatabase.CreateAsset(mesh, path); }
        mesh.Clear(); mesh.vertices = p.vertices; mesh.uv = p.uv; mesh.triangles = p.triangles;
        mesh.RecalculateNormals(); mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh); return mesh;
    }
    static Material MaterialFor(string name, Color color, bool unlit = false)
    {
        string path = Folder + "/" + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!mat) { mat = new Material(Shader.Find(unlit ? "Universal Render Pipeline/Unlit" : "Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(mat,path); }
        mat.SetColor("_BaseColor",color);
        if (!unlit) { mat.SetFloat("_Smoothness",0); mat.SetFloat("_SpecularHighlights",0); }
        EditorUtility.SetDirty(mat); return mat;
    }
    static Transform Child(Transform parent, string name, Vector3 position)
    {
        var t = new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t;
    }
    static MeshRenderer Visual(Transform parent, string name, Mesh mesh, Material material)
    {
        var t=Child(parent,name,Vector3.zero); t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
        var r=t.gameObject.AddComponent<MeshRenderer>(); r.sharedMaterial=material;
        r.lightProbeUsage=LightProbeUsage.BlendProbes; return r;
    }
    static Transform Bar(Transform parent, string name, Vector3 position, Vector3 scale, Material mat)
    {
        var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=name;Object.DestroyImmediate(g.GetComponent<Collider>());
        g.transform.SetParent(parent,false);g.transform.localPosition=position;g.transform.localScale=scale;
        g.GetComponent<Renderer>().sharedMaterial=mat;return g.transform;
    }
    [MenuItem("Duck Rails/Art/Install Blender Bridge Terminal")]
    public static void Install()
    {
        if (Application.dataPath != "X:/Work/duck-rails/Assets" || EditorApplication.isPlaying) throw new InvalidOperationException("Duck Rails Edit Mode required");
        AssetDatabase.Refresh();
        var model=JsonUtility.FromJson<Model>(File.ReadAllText("ArtSource/BridgeTerminal/terminal-meshes.json"));
        foreach(var p in model.parts) MeshFor(p);
        var imp=(TextureImporter)AssetImporter.GetAtPath(Folder+"/TerminalPixels.png");
        imp.filterMode=FilterMode.Point;imp.mipmapEnabled=true;imp.textureCompression=TextureImporterCompression.Uncompressed;imp.SaveAndReimport();
        var mat=MaterialFor("Terminal",Color.white);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/TerminalPixels.png"));EditorUtility.SetDirty(mat);
        MaterialFor("Display glow",new Color(.55f,1,.42f),true);MaterialFor("Display track",new Color(.065f,.12f,.06f),true);
        var audio=(AudioImporter)AssetImporter.GetAtPath(MechanismClip);
        audio.forceToMono=true;var settings=audio.defaultSampleSettings;settings.loadType=AudioClipLoadType.DecompressOnLoad;settings.compressionFormat=AudioCompressionFormat.PCM;audio.defaultSampleSettings=settings;audio.SaveAndReimport();
        var root=PrefabUtility.LoadPrefabContents(Finale);
        try { InstallOn(root.transform.Find("Env/Lower bridge button"));PrefabUtility.SaveAsPrefabAsset(root,Finale); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
        Debug.Log("Bridge terminal installed on existing finale control. Gold crate preserved.");
    }
    public static void InstallOn(Transform anchor)
    {
        if (!anchor) throw new InvalidOperationException("Existing bridge control anchor missing");
        // Keep this transform/fileID and its position: route and scene references depend on it.
        var old=anchor.Find(ArtName);if(old)Object.DestroyImmediate(old.gameObject);
        var oldPivot=anchor.Find("Bridge lever pivot");if(oldPivot)Object.DestroyImmediate(oldPivot.gameObject);
        foreach(var r in anchor.GetComponents<Renderer>())r.enabled=false;
        foreach(var c in anchor.GetComponents<Collider>())c.enabled=false;
        foreach(Transform t in anchor) t.gameObject.SetActive(false);
        var legacy=anchor.GetComponent<RailControl>();if(legacy){legacy.enabled=false;legacy.holdButtonPrefab=null;}
        float floorOffset=8f-anchor.localPosition.y;
        var cabinet=Child(anchor,ArtName,new Vector3(0,floorOffset,0));
        Visual(cabinet,"Blender cabinet",AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Cabinet.asset"),AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Terminal.mat"));
        // No new locomotion obstacle: same non-solid control convention as train throttle.
        var pivot=Child(anchor,"Bridge lever pivot",new Vector3(0,floorOffset+.17f,-.22f));
        var lever=pivot.gameObject.AddComponent<RailControl>();lever.action=RailControl.Action.Bridge;
        lever.palmOffset=new Vector3(0,.02f,.0315f);lever.returnDuration=.25f;
        var grip=Child(pivot,"Grip lever",Quaternion.Euler(-35,0,0)*Vector3.up*.26f);
        grip.localRotation=Quaternion.Euler(-35,0,0);grip.gameObject.layer=6;
        Visual(grip,"Blender lever",AssetDatabase.LoadAssetAtPath<Mesh>(Folder+"/Lever.asset"),AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Terminal.mat"));
        var volume=grip.gameObject.AddComponent<BoxCollider>();volume.isTrigger=true;volume.center=new Vector3(0,.08f,0);volume.size=new Vector3(.36f,.065f,.075f);
        lever.handle=grip;
        var hold=anchor.GetComponent<RailHoldControl>();if(!hold)hold=anchor.gameObject.AddComponent<RailHoldControl>();
        hold.button=null;hold.lever=lever;hold.bridge=true;hold.radio=false;
        var screen=Child(cabinet,"Progress screen",new Vector3(.075f,.23f+.245f/3+.053f,.245f));
        screen.localRotation=Quaternion.Euler(71.56505f,0,0);
        var label=Child(screen,"Bridge percent",new Vector3(0,.025f,-.003f)).gameObject.AddComponent<TextMeshPro>();
        label.font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
        label.text="0%";label.fontSize=1.8f;label.alignment=TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta=new Vector2(.48f,.15f);label.color=new Color(.65f,1,.48f);
        var track=Bar(screen,"Progress track",new Vector3(0,-.084f,0),new Vector3(.41f,.033f,.004f),AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Display track.mat"));
        var fill=Bar(screen,"Progress fill",new Vector3(0,-.084f,-.004f),new Vector3(.39f,.023f,.004f),AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Display glow.mat"));
        var terminal=cabinet.gameObject.AddComponent<RailBridgeTerminal>();terminal.percentage=label;terminal.progressFill=fill;
        var source=cabinet.gameObject.AddComponent<AudioSource>();source.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(MechanismClip);
        source.playOnAwake=false;source.loop=true;source.spatialBlend=1;source.volume=.5f;source.minDistance=2;source.maxDistance=35;source.rolloffMode=AudioRolloffMode.Linear;source.dopplerLevel=0;
        terminal.mechanism=source;terminal.Present(0,false);
    }
}
#endif
