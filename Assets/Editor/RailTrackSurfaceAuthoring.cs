#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// Surface-only catalogue migration. Never rebuild block layouts or navigation.
public static class RailTrackSurfaceAuthoring
{
    public const string Folder = "Assets/DuckRails/TrackSurfaces";
    public static string[] Paths => RailCatalogLightingAuthoring.Prefabs.Concat(new[]{"Assets/DuckRails/StartingStation.prefab"}).ToArray();
    static bool Wood(string n) => n == "Sleeper" || n == "Visual sleeper" || n == "Disused sleeper" || n == "Bridge steel sleeper" || n == "Victory track sleeper";
    static bool Rail(string n) => n == "Track rail" || n == "Rail" || n == "Visual rail" || n == "Disused visual rail" || n == "Moving railway rail" || n == "Exit rail";
    static bool Target(Component c) => Wood(c.name) || Rail(c.name);
    static void Guard()
    {
        if(Application.dataPath.Replace('\\','/') != "X:/Work/duck-rails/Assets" || EditorApplication.isPlayingOrWillChangePlaymode || Lightmapping.isRunning) throw new Exception("Duck Rails Edit Mode, no bake required");
        for(int i=0;i<SceneManager.sceneCount;i++) if(SceneManager.GetSceneAt(i).isDirty) throw new Exception("Save scene edits first");
    }
    static Vector3 PointAtZ(RailBlock b, float z)
    {
        for(int i=1;i<b.track.Length;i++) if(z<=b.track[i].z) return Vector3.Lerp(b.track[i-1],b.track[i],Mathf.InverseLerp(b.track[i-1].z,b.track[i].z,z));
        return b.track.Last();
    }
    static void Spacing(GameObject root)
    {
        var b=root.GetComponent<RailBlock>();
        var old=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Sleeper").OrderBy(r=>root.transform.InverseTransformPoint(r.transform.position).z).ToList();
        if(old.Count>0)
        {
            float end=b.id=="bridge-finale-v1"?80:b.length;
            var template=old[0]; float y=root.transform.InverseTransformPoint(template.transform.position).y;
            for(int i=0;i<end/2;i++)
            {
                var r=i<old.Count?old[i]:Object.Instantiate(template,template.transform.parent);
                r.name="Sleeper"; float z=1+i*2;var p=PointAtZ(b,z);p.y+=y;
                r.transform.position=root.transform.TransformPoint(p);
                var tangent=PointAtZ(b,Mathf.Min(end,z+.1f))-PointAtZ(b,Mathf.Max(0,z-.1f));
                r.transform.rotation=root.transform.rotation*Quaternion.LookRotation(tangent);
            }
        }
        var victory=root.GetComponentsInChildren<MeshRenderer>(true).Where(r=>r.name=="Victory track sleeper").OrderBy(r=>r.transform.localPosition.z).ToList();
        if(victory.Count>0) for(int i=0;i<100;i++)
        {
            var r=i<victory.Count?victory[i]:Object.Instantiate(victory[0],victory[0].transform.parent);
            r.name="Victory track sleeper";var p=r.transform.localPosition;p.z=111+2*i;r.transform.localPosition=p;
        }
    }
    [MenuItem("Duck Rails/Authoring/Track/Apply atlas surfaces and two metre sleepers")]
    public static void Apply()
    {
        Guard(); var setup=EditorSceneManager.GetSceneManagerSetup();
        RailDepotMaterials.BeginSharedPass(Folder+"/Meshes");
        var steel=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/Dull grey rail.mat");
        if(!steel){steel=new Material(AssetDatabase.LoadAssetAtPath<Material>(RailDepotMaterials.Folder+"/Oxidized steel.mat"));AssetDatabase.CreateAsset(steel,Folder+"/Dull grey rail.mat");}
        steel.SetColor("_BaseColor",new Color(.55f,.55f,.55f));steel.SetFloat("_Metallic",.35f);steel.SetFloat("_Smoothness",.08f);steel.enableInstancing=true;EditorUtility.SetDirty(steel);
        var wood=AssetDatabase.LoadAssetAtPath<Material>(RailDepotMaterials.Folder+"/Old timber.mat");
        var compacted=new HashSet<Mesh>();var report=new List<string>();
        try
        {
            foreach(var path in Paths)
            {
                var root=PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var b=root.GetComponent<RailBlock>();var track=b.track.ToArray();var graph=b.graph;
                    var collision=root.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6")).ToArray();
                    Spacing(root);
                    foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).Where(Target))
                    {
                        if(r.GetComponent<Collider>())throw new Exception("Unexpected track collider "+path+" "+r.name);
                        var f=r.GetComponent<MeshFilter>();var bounds=f.sharedMesh.bounds;
                        RailDepotMaterials.Map(f,r,Wood(r.name)?"timber":"steel",0,Folder+"/Meshes");
                        if(compacted.Add(f.sharedMesh))
                        {
                            var tris=f.sharedMesh.triangles;f.sharedMesh.subMeshCount=1;f.sharedMesh.SetTriangles(tris,0);
                            // Industrial atlas: neutral riveted sheet instead of the depot's teal oxidized tile.
                            if(!Wood(r.name)) f.sharedMesh.uv=f.sharedMesh.uv.Select(v=>new Vector2((192.5f+(v.x*256-32.5f)*31/63)/256,v.y)).ToArray();
                            EditorUtility.SetDirty(f.sharedMesh);
                        }
                        r.sharedMaterials=new[]{Wood(r.name)?wood:steel};
                        if((bounds.size-f.sharedMesh.bounds.size).sqrMagnitude>.00001f)throw new Exception("Track mesh bounds changed");
                    }
                    if(!track.SequenceEqual(b.track)||graph!=b.graph||!collision.SequenceEqual(root.GetComponentsInChildren<Collider>(true).Select(c=>EditorJsonUtility.ToJson(c)+c.transform.localToWorldMatrix.ToString("F6"))))throw new Exception("Gameplay geometry changed");
                    PrefabUtility.SaveAsPrefabAsset(root,path);
                    report.Add(path+": "+root.GetComponentsInChildren<MeshRenderer>(true).Count(r=>Rail(r.name))+" rails, "+root.GetComponentsInChildren<MeshRenderer>(true).Count(r=>Wood(r.name))+" sleepers; collision/track/graph preserved");
                }
                finally{PrefabUtility.UnloadPrefabContents(root);}
            }
            AssetDatabase.SaveAssets();
            foreach(var path in Paths)
            {
                string scenePath=path.EndsWith("StartingStation.prefab")?RailStationLightingAuthoring.BakeScene:"Assets/DuckRails/Lighting/Blocks/"+Path.GetFileNameWithoutExtension(path)+"/"+Path.GetFileNameWithoutExtension(path)+"-bake.unity";
                SyncScene(scenePath,false);
            }
            SyncScene("Assets/Scenes/Expedition.unity",false);
            Directory.CreateDirectory("docs/validation/track-surfaces");File.WriteAllLines("docs/validation/track-surfaces/apply.txt",report);
        }
        finally{EditorSceneManager.RestoreSceneManagerSetup(setup);}
        Validate();
    }
    public static void SyncScene(string path,bool bindings)
    {
        var scene=EditorSceneManager.OpenScene(path);
        foreach(var root in scene.GetRootGameObjects()) foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).Where(Target))
        {
            var source=PrefabUtility.GetCorrespondingObjectFromSource(r);if(!source)continue;
            r.sharedMaterials=source.sharedMaterials;r.GetComponent<MeshFilter>().sharedMesh=source.GetComponent<MeshFilter>().sharedMesh;
            r.transform.localPosition=source.transform.localPosition;r.transform.localRotation=source.transform.localRotation;
            PrefabUtility.RecordPrefabInstancePropertyModifications(r);PrefabUtility.RecordPrefabInstancePropertyModifications(r.GetComponent<MeshFilter>());PrefabUtility.RecordPrefabInstancePropertyModifications(r.transform);
        }
        if(bindings) foreach(var root in scene.GetRootGameObjects()) foreach(var l in root.GetComponentsInChildren<RailBakedBlockLighting>(true))
        {
            if(!PrefabUtility.IsPartOfPrefabInstance(l))continue;
            var so=new SerializedObject(l);PrefabUtility.RevertPropertyOverride(so.FindProperty("bindings"),InteractionMode.AutomatedAction);
        }
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
    }
    public static void Validate()
    {
        var report=new List<string>();int count=0;
        foreach(var path in Paths)
        {
            var root=AssetDatabase.LoadAssetAtPath<GameObject>(path);var b=root.GetComponent<RailBlock>();
            foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true).Where(Target))
            {
                var m=r.GetComponent<MeshFilter>().sharedMesh;
                if(r.sharedMaterials.Length!=1||!r.sharedMaterial.mainTexture||m.subMeshCount!=1||m.uv.Length!=m.vertexCount||m.uv2.Length!=m.vertexCount||r.GetComponent<Collider>())throw new Exception("Invalid textured railway: "+path+"/"+r.name);
                if(m.uv.Any(v=>!float.IsFinite(v.x)||!float.IsFinite(v.y)||v.x<0||v.x>1||v.y<0||v.y>1))throw new Exception("Invalid atlas UV");count++;
            }
            var sleepers=root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Sleeper").OrderBy(t=>t.position.z).ToArray();
            if(sleepers.Length>0)
            {
                int expected=(int)(b.id=="bridge-finale-v1"?40:b.length/2);
                if(sleepers.Length!=expected)throw new Exception("Sleeper count "+path);
                for(int i=0;i<sleepers.Length;i++) if(Mathf.Abs(root.transform.InverseTransformPoint(sleepers[i].position).z-(1+2*i))>.001f)throw new Exception("Sleeper spacing "+path);
            }
            report.Add("PASS "+path);
        }
        report.Add("PASS "+count+" railway renderers: single textured material/submesh, complete atlas/lightmap UVs, no colliders.");
        File.WriteAllLines("docs/validation/track-surfaces/checks.txt",report);Debug.Log(string.Join("\n",report));
    }
}
#endif
