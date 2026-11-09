#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RailHelipadVisibilityCheck
{
    const string Key="RailHelipadVisibilityCheck";
    const string Path="Assets/DuckRails/Authoring/HelipadRouteCheck.unity";
    static double started;
    static RailHelipadVisibilityCheck()
    {
        EditorApplication.playModeStateChanged+=state=>{
            if(!SessionState.GetBool(Key,false))return;
            if(state==PlayModeStateChange.EnteredPlayMode)started=EditorApplication.timeSinceStartup;
            if(state==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);var old=SessionState.GetString(Key+".previous","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(old)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(old);}
        };
        EditorApplication.update+=()=>{
            if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying)return;
            var proof=UnityEngine.Object.FindFirstObjectByType<RailBlockVisibilityProof>();
            string result=proof==null?"Running":proof.Result;
            if(result=="Running"&&EditorApplication.timeSinceStartup-started<240)return;
            if(result=="Running")result="FAIL timed out";
            SessionState.SetString(Key+".result",result);Directory.CreateDirectory("docs/validation/helipad");File.WriteAllText("docs/validation/helipad/play-mode.txt",result+"\nSee docs/validation/block-visibility/play-mode.txt for assertions.\n");
            SessionState.SetBool(Key+".finished",true);EditorApplication.ExitPlaymode();
        };
    }
    public static void Run()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode");
        var active=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
        var go=new GameObject("Helipad route visibility proof");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go,scene);go.AddComponent<RailBlockVisibilityProof>();
        EditorSceneManager.SaveScene(scene,Path);EditorSceneManager.CloseScene(scene,true);UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
        SessionState.SetString(Key+".previous",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));SessionState.SetBool(Key,true);SessionState.SetString(Key+".result","Running");
        EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(Path);EditorApplication.EnterPlaymode();
    }
}
#endif
