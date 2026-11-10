#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RailFrontFurnaceChecks
{
    const string Pending="RailFrontFurnaceChecks.Pending", Setup="RailFrontFurnaceChecks.Setup";
    [Serializable] sealed class SavedSetup { public SceneSetup[] scenes; }
    static RailFrontFurnaceChecks() { EditorApplication.playModeStateChanged+=OnState; }
    public static void Start()
    {
        if(EditorApplication.isPlaying)throw new Exception("Start from Edit Mode");
        for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Save scene changes before furnace physics checks");
        SessionState.SetString(Setup,JsonUtility.ToJson(new SavedSetup{scenes=EditorSceneManager.GetSceneManagerSetup()}));
        SessionState.SetBool(Pending,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.isPlaying=true;
    }
    static void OnState(PlayModeStateChange state)
    {
        if(!SessionState.GetBool(Pending,false))return;
        if(state==PlayModeStateChange.EnteredPlayMode)EditorApplication.delayCall+=()=>
        {
            try{Run();}catch(Exception e){Directory.CreateDirectory(RailFrontFurnaceArt.Evidence);File.WriteAllText(RailFrontFurnaceArt.Evidence+"/checks.txt","FAIL "+e);Debug.LogException(e);}
            finally{EditorApplication.isPlaying=false;}
        };
        if(state==PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool(Pending,false);var setup=JsonUtility.FromJson<SavedSetup>(SessionState.GetString(Setup,""));
            EditorSceneManager.RestoreSceneManagerSetup(setup.scenes);
        }
    }
    [MenuItem("Duck Rails/Train/Validate Front Furnace")]
    public static void Run()
    {
        if(!EditorApplication.isPlaying){Start();return;}
        var lines=new List<string>();
        Action<bool,string> check=(ok,message)=>{if(!ok)throw new Exception(message);lines.Add("PASS "+message);};
        var scene=SceneManager.CreateScene("Front furnace isolated physics",new CreateSceneParameters(LocalPhysicsMode.Physics3D));
        try
        {
            var root=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RailYardMuleArt.Train));SceneManager.MoveGameObjectToScene(root,scene);
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);
            var a=root.GetComponent<RailTrainAssembly>();var f=a.furnace;var physics=scene.GetPhysicsScene();
            var solids=root.GetComponentsInChildren<Collider>().Where(c=>c.enabled&&!c.isTrigger).ToArray();Physics.SyncTransforms();
            check(root.GetComponentsInChildren<RailFurnaceModule>().Length==1,"Exactly one front furnace");
            check(f.transform.localPosition==new Vector3(0,0,5.08f),"Firebox beyond front deck edge");
            check(!f.Contains(new Vector3(-1.05f,.7f,1.9f)),"Former onboard fueling position rejects coal");
            foreach(var offset in new[]{new Vector3(0,f.acceptanceHalfExtents.y+.05f,0),new Vector3(f.acceptanceHalfExtents.x+.05f,0,0),new Vector3(0,0,-f.acceptanceHalfExtents.z-.05f),new Vector3(0,0,f.acceptanceHalfExtents.z+.05f)})
                check(!f.AcceptsSample(offset),"Reject outside chamber "+offset);
            check(f.AcceptsSample(Vector3.zero)&&f.AcceptsSample(new Vector3(.6f,.15f,.18f)),"Interior network samples accepted");
            for(int pose=0;pose<8;pose++)
            {
                root.transform.SetPositionAndRotation(new Vector3(pose*3,pose*.2f,-pose*5),Quaternion.Euler(pose,pose*43,0));
                check(f.Contains(f.insertion.position)&&f.AcceptsSample(new Vector3(.5f,.1f,.1f))&&!f.AcceptsSample(new Vector3(0,0,-f.acceptanceHalfExtents.z-.05f)),"Acceptance follows translated/rotated train pose "+pose);
            }
            root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);Physics.SyncTransforms();
            foreach(float x in new[]{-.8f,0,.8f})
            {
                var ray=new Ray(new Vector3(x,.07f,6.4f),Vector3.back);float nearest=float.MaxValue;string hitName="";
                foreach(var c in solids)if(c.Raycast(ray,out var hit,3)&&hit.distance<nearest){nearest=hit.distance;hitName=c.name;}
                check(nearest>1.2f&&hitName=="Closed rear","Open front ray reaches back wall at X="+x);
                var sight=new Ray(new Vector3(x,.5f,3.65f),Vector3.forward);
                check(!solids.Any(c=>c.Raycast(sight,out _,3)),"Forward 0.5 m eye line clear at X="+x);
            }
            var inside=new Ray(new Vector3(0,.07f,4.5f),Vector3.forward);
            check(solids.Any(c=>c.Raycast(inside,out var hit,1)&&hit.distance<.6f),"Onboard route is physically closed before acceptance volume");
            var coalPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/RailCoal.prefab");
            var source=coalPrefab.GetComponent<BoxCollider>();
            var cube=new GameObject("Coal collision test");SceneManager.MoveGameObjectToScene(cube,scene);
            var box=cube.AddComponent<BoxCollider>();box.size=Vector3.Scale(source.size,coalPrefab.transform.localScale);
            var body=cube.AddComponent<Rigidbody>();body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;body.useGravity=false;
            foreach(var rotation in new[]{Quaternion.identity,Quaternion.Euler(35,45,25)})foreach(float x in new[]{-.55f,0,.55f})
            {
                body.position=new Vector3(x,.07f,6.6f);body.rotation=rotation;body.linearVelocity=Vector3.back*3;body.angularVelocity=Vector3.zero;Physics.SyncTransforms();bool entered=false;
                for(int step=0;step<100;step++){physics.Simulate(.01f);if(f.Contains(body.position)){entered=true;break;}}
                check(entered,"Full coal collider physically enters mouth, X="+x+", rotation="+rotation.eulerAngles);
            }
            body.useGravity=true;body.position=new Vector3(0,.1f,6.8f);body.rotation=Quaternion.identity;body.linearVelocity=new Vector3(0,1.35f,-4);body.angularVelocity=Vector3.zero;Physics.SyncTransforms();bool thrown=false;
            for(int step=0;step<120;step++){physics.Simulate(.01f);if(f.Contains(body.position)){thrown=true;break;}}
            check(thrown,"Gravity-driven coal throw enters the real acceptance volume");
            check(f.fire.main.maxParticles==32&&f.smoke.main.maxParticles==24,"Existing bounded particle budgets retained");
            check(f.fire.transform.position.z>5.08f&&f.fire.transform.position.z<5.65f,"Fire emitter is recessed inside nose");
            int edges=0;
            foreach(var filter in f.GetComponentsInChildren<MeshFilter>())
            {
                var mesh=filter.sharedMesh;var v=mesh.vertices;var uv=mesh.uv;var ids=mesh.triangles;
                for(int i=0;i<ids.Length;i+=3)for(int j=0;j<3;j++)
                {int x=ids[i+j],y=ids[i+(j+1)%3];float error=Mathf.Abs(Vector2.Distance(uv[x],uv[y])*64/Vector3.Distance(v[x],v[y])/32-1);if(error>.003f)throw new Exception("UV stretch: "+filter.name);edges++;}
            }
            lines.Add("PASS "+edges+" furnace mesh edges retain square 32 texels/metre UVs");
        }
        finally{foreach(var go in scene.GetRootGameObjects())Object.DestroyImmediate(go);SceneManager.UnloadSceneAsync(scene);}
        Directory.CreateDirectory(RailFrontFurnaceArt.Evidence);File.WriteAllLines(RailFrontFurnaceArt.Evidence+"/checks.txt",lines);Debug.Log(string.Join("\n",lines));
    }
}
#endif
