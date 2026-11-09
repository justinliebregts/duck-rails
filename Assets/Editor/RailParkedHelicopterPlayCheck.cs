#if UNITY_EDITOR
using System;
using System.Reflection;
using Game.Audio;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class RailParkedHelicopterPlayCheck
{
    const string Key="RailParkedHelicopterPlayCheck";
    const string ScenePath="Assets/DuckRails/Authoring/ParkedHelicopterCheck.unity";
    static Helicopter heli;
    static RailParkedHelicopter fx;
    static Quaternion main,tail;
    static Transform tailPivot;
    static Vector3 position;
    static Quaternion rotation;
    static float start;
    static int phase;
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static RailParkedHelicopterPlayCheck()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode){start=Time.realtimeSinceStartup;phase=0;}
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);var path=SessionState.GetString(Key+".previous","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(path)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(path);}
        };
        EditorApplication.update+=Tick;
    }
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode first");
        RailParkedHelicopterSetup.Validate();
        var current=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        try{
            var block=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(RailHelipadPreview.Path),scene);
            // Keep presentation live; no wallet/authentication, purchases or deposit callbacks.
            foreach(var s in block.GetComponentsInChildren<StoreUI>(true))s.gameObject.SetActive(false);
            foreach(var r in block.GetComponentsInChildren<RailGoldReceiver>(true))r.enabled=false;
            var camera=new GameObject("Presentation test camera");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(camera,scene);camera.tag="MainCamera";camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();
            var screen=block.GetComponentInChildren<HelicopterMirror>();camera.transform.position=screen.transform.position+Vector3.right*2;camera.transform.LookAt(screen.transform);
            EditorSceneManager.SaveScene(scene,ScenePath);
        }finally{EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(current);}
        SessionState.SetString(Key+".previous",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
        SessionState.SetBool(Key,true);SessionState.SetString(Key+".result","running");
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||EditorApplication.isPaused)return;
        try{
            if(phase==0){heli=Object.FindFirstObjectByType<Helicopter>();if(heli==null)return;fx=heli.GetComponent<RailParkedHelicopter>();position=heli.transform.position;rotation=heli.transform.rotation;main=heli.propeller.localRotation;tailPivot=(Transform)typeof(Helicopter).GetField("tailRotor",Private).GetValue(heli);tail=tailPivot.localRotation;start=Time.realtimeSinceStartup;phase=1;return;}
            float elapsed=Time.realtimeSinceStartup-start;
            if(phase==1 && elapsed>2.137f){
                Check(heli.transform.position==position&&heli.transform.rotation==rotation,"Cabin moved");
                Check(Quaternion.Angle(main,heli.propeller.localRotation)>1,"Main rotor did not rotate");
                Check(Quaternion.Angle(tail,tailPivot.localRotation)>1,"Tail rotor did not rotate");
                Check(fx.engineSound.IsLooping,"Engine loop not playing");
                Check(fx.rotorWash.GetComponentInChildren<ParticleSystem>().particleCount>0,"No rotor wash particles");
                var mirror=heli.GetComponentInChildren<HelicopterMirror>();var cam=(Camera)typeof(HelicopterMirror).GetField("_camera",Private).GetValue(mirror);
                Check(cam!=null&&cam.targetTexture!=null&&cam.targetTexture.IsCreated(),"Security camera texture missing");
                Check((bool)typeof(HelicopterMirror).GetField("_haveFrame",Private).GetValue(mirror),"Security camera did not draw near viewer");
                fx.enabled=false;Check(!fx.engineSound.IsLooping,"Disabled presentation kept loop");Check(fx.rotorWash.GetComponentInChildren<ParticleSystem>().particleCount==0,"Disabled presentation kept particles");
                fx.enabled=true;start=Time.realtimeSinceStartup;phase=2;
            }else if(phase==2&&elapsed>1.2f){Check(fx.engineSound.IsLooping,"Audio did not restart");Check(fx.rotorWash.GetComponentInChildren<ParticleSystem>().particleCount>0,"Wash did not restart");Finish("PASS: fixed cabin pose, both rotors rotate, engine loop plays/restarts, wash emits/clears/restarts, security camera draws to texture. Commerce disabled in this isolated fixture.");}
        }catch(Exception e){Finish("FAIL: "+e);}
    }
    static void Finish(string result)
    {
        SessionState.SetString(Key+".result",result);System.IO.Directory.CreateDirectory("output/helipad");System.IO.File.WriteAllText("output/helipad/parked-helicopter-check.txt",result);
        phase=3;if(result.StartsWith("FAIL"))Debug.LogError(result);else Debug.Log(result);EditorApplication.ExitPlaymode();
    }
}
#endif
