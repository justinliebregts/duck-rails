#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using TMPro;
using Object = UnityEngine.Object;

/// <summary>Installs reviewed modular Blender controls without regenerating the train shell.</summary>
public static class RailCockpitV2
{
    const string Folder="Assets/DuckRails/Train/CockpitV2";
    const string Train="Assets/DuckRails/Train/Prefabs/DuckRailsTrain.prefab";
    [Serializable] public class Part { public string name; public Vector3[] vertices; public Vector2[] uv; public int[] triangles; }
    [Serializable] public class Model { public Part[] parts; }
    static Quaternion Slope=>Quaternion.Euler(-25,0,0);
    static Vector3 Surface(float x,float z,float lift=0)=>new Vector3(x,.23f+(z-.02f)*Mathf.Tan(25*Mathf.Deg2Rad),z)+Slope*Vector3.up*lift;
    static Material Material(string name,string texture)
    {
        var path=Folder+"/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
        mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+texture));mat.SetColor("_BaseColor",Color.white);
        mat.SetFloat("_Smoothness",0);mat.SetFloat("_SpecularHighlights",0);mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");EditorUtility.SetDirty(mat);return mat;
    }
    static Transform PartObject(Transform parent,string name,Mesh mesh,Material mat,Vector3 p,Quaternion q)
    {
        var t=parent.Find(name);if(!t){t=new GameObject(name).transform;t.SetParent(parent,false);}
        t.localPosition=p;t.localRotation=q;t.localScale=Vector3.one;t.gameObject.layer=6;
        var f=t.GetComponent<MeshFilter>();if(!f)f=t.gameObject.AddComponent<MeshFilter>();f.sharedMesh=mesh;
        var r=t.GetComponent<MeshRenderer>();if(!r)r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=mat;r.enabled=true;
        return t;
    }
    static Mesh SaveMesh(string name,Vector3[] v,Vector2[] uv,int[] tris)
    {
        var path=Folder+"/"+name+".asset";var m=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!m){m=new Mesh{name=name};AssetDatabase.CreateAsset(m,path);}m.Clear();m.vertices=v;m.uv=uv;m.triangles=tris;m.RecalculateNormals();m.RecalculateBounds();EditorUtility.SetDirty(m);return m;
    }
    [MenuItem("Duck Rails/Art/Install Approved Cockpit V2")]
    public static void Install()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying)throw new InvalidOperationException("Duck Rails Edit Mode required");
        foreach(var file in new[]{"Palette.png","CoalInstruction.png"})
        {
            var im=(TextureImporter)AssetImporter.GetAtPath(Folder+"/"+file);im.wrapMode=TextureWrapMode.Clamp;im.mipmapEnabled=true;im.maxTextureSize=file=="Palette.png"?128:1024;im.filterMode=file=="Palette.png"?FilterMode.Point:FilterMode.Bilinear;im.SaveAndReimport();
        }
        var model=JsonUtility.FromJson<Model>(File.ReadAllText(Folder+"/geometry.json"));
        var meshes=model.parts.ToDictionary(p=>p.name,p=>SaveMesh(p.name,p.vertices,p.uv,p.triangles));
        var mat=Material("Controls","Palette.png");var paper=Material("Instruction","CoalInstruction.png");
        var root=PrefabUtility.LoadPrefabContents(Train);
        try
        {
            var a=root.GetComponent<RailTrainAssembly>();var c=a.controls;
            var d=c.GetComponentsInChildren<RailControl>().Single(x=>x.action==RailControl.Action.Drive);
            // Keep the handle identity and neutral position: RailControl rotates its offset about the original axle.
            foreach(var r in d.handle.GetComponentsInChildren<MeshRenderer>()) if(r.name!="Linked throttle art")r.enabled=false;
            d.transform.localPosition=new Vector3(.05f,.115f,-.035f);
            d.handle.localPosition=new Vector3(0,.26f,0);d.handle.localRotation=Quaternion.identity;
            d.handle.localScale=Vector3.one;
            PartObject(d.handle,"Linked throttle art",meshes["ThrottleMoving"],mat,Vector3.zero,Quaternion.identity);
            var grip=d.handle.GetComponent<BoxCollider>();grip.center=new Vector3(0,.08f,0);grip.size=new Vector3(.36f,.065f,.075f);grip.isTrigger=true;
            PartObject(d.transform,"Three slot housing",meshes["ThrottleBase"],mat,new Vector3(0,.18f,0),Quaternion.identity);
            var h=c.GetComponentInChildren<RailHoldControl>();var cap=h.button.cap;
            foreach(var r in cap.GetComponentsInChildren<MeshRenderer>())r.enabled=false;
            cap.localScale=Vector3.one;
            PartObject(cap,"Faceted horn art",meshes["HornCap"],mat,new Vector3(0,-.03f,0),Quaternion.identity);
            PartObject(h.transform,"Horn mounting",meshes["HornBase"],mat,Vector3.zero,Quaternion.identity);
            var touch=h.button.GetComponent<BoxCollider>();touch.center=new Vector3(0,.055f,0);touch.size=new Vector3(.19f,.07f,.19f);
            var font=AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/Fonts/CutePixel SDF.asset");
            // Retain HORN lettering, but move it onto the panel rather than the moving cap.
            var label=cap.GetComponentInChildren<TMP_Text>(true);label.GetComponent<MeshRenderer>().enabled=true;
            label.transform.localPosition=new Vector3(0,-.015f,-.145f);label.transform.localScale=Vector3.one;label.font=font;label.fontSize=.36f;label.rectTransform.sizeDelta=new Vector2(.22f,.06f);
            var distance=c.Find("Distance display").GetComponent<TMP_Text>();
            distance.transform.localPosition=Surface(-1.2f,.025f,.04f);distance.transform.localRotation=Slope*Quaternion.Euler(90,0,0);distance.font=font;distance.fontSize=.65f;distance.rectTransform.sizeDelta=new Vector2(.60f,.20f);
            root.GetComponentInChildren<RailDistanceDisplay>(true).stacked=true;distance.text="DISTANCE\n<size=160%>0 m</size>";
            var throttleLabel=c.Find("Throttle label");throttleLabel.localPosition=new Vector3(.05f,.294f,-.182f);throttleLabel.localRotation=Quaternion.Euler(90,0,0);
            PartObject(c,"Distance bezel",meshes["DistanceBase"],mat,Surface(-1.2f,.025f,.013f),Slope);
            MatchFuelBacking(c);
            // Top surface UVs deliberately map the complete instruction picture.
            var v=new[]{new Vector3(-.275f,0,-.13f),new Vector3(-.275f,0,.13f),new Vector3(.275f,0,.13f),new Vector3(.275f,0,-.13f)};
            var card=SaveMesh("Instruction face",v,new[]{new Vector2(0,0),new Vector2(0,1),new Vector2(1,1),new Vector2(1,0)},new[]{0,1,2,0,2,3});
            PartObject(c,"Coal instruction",card,paper,Surface(1.14f,.025f,.023f),Slope);
            PartObject(c,"Instruction tape",meshes["Tape"],mat,Surface(1.14f,.025f,.024f),Slope);
            var anchor=root.transform.Find("Train light probe anchor");
            foreach(var r in c.GetComponentsInChildren<MeshRenderer>())r.probeAnchor=anchor;
            PrefabUtility.SaveAsPrefabAsset(root,Train);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();Validate();
    }
    // Narrow art-only update; does not rerun the throttle/horn installer or reset tuning.
    public static void MatchFuelBacking(Transform controls)
    {
        var distance=controls.Find("Distance bezel");
        var source=AssetDatabase.LoadAssetAtPath<Mesh>("Assets/DuckRails/Train/Blender/Retained gauge trim.asset");
        var vertices=source.vertices;var triangles=source.triangles;
        var kept=new System.Collections.Generic.List<int>();
        for(int i=0;i<triangles.Length;i+=3)
        {
            // Preserve the original ten dark segment sockets; remove only the two large backing boxes.
            for(int slot=0;slot<10;slot++)
            {
                var center=Surface(-.732f+slot*.056f,-.065f,.047f);
                bool inside=true;
                for(int j=0;j<3;j++)
                {
                    var p=Quaternion.Inverse(Slope)*(vertices[triangles[i+j]]-center);
                    inside &= Mathf.Abs(p.x)<.0226f && Mathf.Abs(p.y)<.0031f && Mathf.Abs(p.z)<.0276f;
                }
                if(inside){kept.AddRange(new[]{triangles[i],triangles[i+1],triangles[i+2]});break;}
            }
        }
        if(kept.Count!=360)throw new InvalidOperationException("Expected ten original fuel socket boxes; found "+kept.Count+" indices");
        var trim=controls.Find("Integrated dash art/Dash trim");
        trim.GetComponent<MeshFilter>().sharedMesh=SaveMesh("Fuel sockets",vertices,source.uv,kept.ToArray());
        var backing=PartObject(controls,"Fuel bezel",distance.GetComponent<MeshFilter>().sharedMesh,
            distance.GetComponent<MeshRenderer>().sharedMaterial,
            new Vector3(-.48f,distance.localPosition.y,distance.localPosition.z),distance.localRotation);
        backing.localScale=distance.localScale;
        backing.GetComponent<MeshRenderer>().probeAnchor=distance.GetComponent<MeshRenderer>().probeAnchor;
    }
    [MenuItem("Duck Rails/Art/Match Fuel Panel Backing")]
    public static void MatchFuelPanel()
    {
        if(Application.dataPath!="X:/Work/duck-rails/Assets"||EditorApplication.isPlaying)throw new InvalidOperationException("Duck Rails Edit Mode required");
        var root=PrefabUtility.LoadPrefabContents(Train);
        try
        {
            MatchFuelBacking(root.GetComponent<RailTrainAssembly>().controls);
            PrefabUtility.SaveAsPrefabAsset(root,Train);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
        AssetDatabase.SaveAssets();
    }
    [MenuItem("Duck Rails/Train/Validate Cockpit V2")]
    public static void Validate()
    {
        var root=AssetDatabase.LoadAssetAtPath<GameObject>(Train);var a=root.GetComponent<RailTrainAssembly>();var c=a.controls;
        var d=c.GetComponentsInChildren<RailControl>().Single(x=>x.action==RailControl.Action.Drive);
        if(!d.handle.Find("Linked throttle art")||!d.transform.Find("Three slot housing"))throw new Exception("Throttle art missing");
        if(d.handle.localScale!=Vector3.one||!d.handle.GetComponent<Collider>().isTrigger)throw new Exception("Grip configuration");
        if(!c.GetComponentInChildren<RailHoldControl>().button.cap.Find("Faceted horn art"))throw new Exception("Horn binding");
        if(!c.GetComponent<RailCockpitFuelBar>().segments||!a.fuelGauge)throw new Exception("Fuel binding");
        if(c.GetComponentsInChildren<TMP_Text>().Any(t=>t.font.name!="CutePixel SDF"))throw new Exception("Font mismatch");
        if(!c.Find("Coal instruction")||!c.Find("Distance bezel"))throw new Exception("Readouts missing");
        if(Mathf.Abs(d.transform.localPosition.y-.115f)>.0001f||Mathf.Abs(d.handle.localPosition.y-.26f)>.0001f)throw new Exception("Lowered axle alignment");
        // Conservative rectangular arm envelope through the full housing thickness.
        for(int angle=-35;angle<=35;angle++)
        {
            float rad=angle*Mathf.Deg2Rad;
            float reach=.1875f*Mathf.Abs(Mathf.Tan(rad))+.013f/Mathf.Cos(rad);
            if(reach>=.151f)throw new Exception("Slot end clearance at "+angle);
        }
        Directory.CreateDirectory("docs/validation/cockpit-v2");
        File.WriteAllText("docs/validation/cockpit-v2/editmode.txt","PASS Saved prefab art and retained grip/horn/fuel references\nPASS CutePixel text\nPASS Axle lowered 0.18 m; neutral grip centre retained at controls Y 0.455 m\nPASS 71 sampled angles (-35..35): conservative arm envelope clears slot end bridges\nPlay Mode, multiplayer and headset feel NOT run; queued for playtest.\n");
        Debug.Log("Cockpit V2: saved throttle, grip trigger, horn cap, fuel, CutePixel and instruction/readout checks PASS; playtest pending.");
    }
}
#endif

