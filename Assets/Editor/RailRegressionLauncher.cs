#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Unity.Multiplayer.PlayMode;

// Does not open Test Center or read diagnostic reports. State survives domain reload.
[InitializeOnLoad]
public static class RailRegressionLauncher
{
    const string Key = "Rails.DeepRegression";
    static ScriptableObject Manager => Resources.FindObjectsOfTypeAll<ScriptableObject>().FirstOrDefault(o => o.GetType().FullName == "Unity.PlayMode.Editor.PlayModeManager");
    public static string Status => Manager?.GetType().GetProperty("CurrentState")?.GetValue(Manager)?.ToString() ?? "Unavailable";
    static RailRegressionLauncher() { EditorApplication.update += Tick; }
    public static void Start(string suite, int clients = 4)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || Status != "NotRunning") throw new Exception("Stop scenario first");
        if (!CurrentPlayer.IsMainEditor) throw new Exception("Main editor only");
        if (!(suite=="private" && clients==4 || suite=="public" && clients>=3 && clients<=4 || suite=="coal" && clients==2)) throw new Exception("Use private/4, public/3 or 4, coal/2");
        foreach(var tag in (clients == 4 ? new[]{"Player2","Player3","Player4"} : clients==3 ? new[]{"Player2","Player3"} : new[]{"Player2"})) if(!MppmStandaloneCredentials.TryGet(tag,out _)) throw new Exception("Missing " + tag);
        var selected = (UnityEngine.Object)Manager.GetType().GetProperty("ActivePlayModeConfig").GetValue(Manager);
        SessionState.SetString(Key+".scenario", AssetDatabase.GetAssetPath(selected));
        SessionState.SetBool(Key+".quick",RailEditorQuickStart.Enabled);
        SessionState.SetString(Key+".suite",suite);
        SessionState.SetBool(Key,true);
        RailCoalLiveChecks.Disarm(); RailWeek2LiveChecks.Disarm(); RailWeek3LiveChecks.Disarm(); RailStabilityLiveChecks.Disarm();
        RailEditorQuickStart.Enabled=false;
        Directory.CreateDirectory("docs/validation/overnight-regression");
        RailDeepRegressionChecks.Disarm(); RailCoalLiveChecks.Disarm();
        if(suite=="private") RailDeepRegressionChecks.ArmTest();
        if(suite=="public") RailStabilityLiveChecks.ArmClients(clients);
        if(suite=="coal") { RailEditorQuickStart.Enabled=true; RailCoalLiveChecks.Arm(); }
        Manager.GetType().GetProperty("ActivePlayModeConfig").SetValue(Manager,AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(clients == 4 ? "Assets/Settings/PlayMode/6 Four Client Regression.asset" : clients==3 ? "Assets/Settings/PlayMode/3 Week 2 Test.asset" : "Assets/Settings/PlayMode/4 Diagnostic Two Client.asset"));
        Manager.GetType().GetMethod("Start",Type.EmptyTypes).Invoke(Manager,null);
    }
    public static void Stop()
    {
        RailDeepRegressionChecks.Disarm(); RailCoalLiveChecks.Disarm();
        RailStabilityLiveChecks.Disarm();
        if(Status=="Running") Manager.GetType().GetMethod("Stop",Type.EmptyTypes).Invoke(Manager,null);
    }
    static void Tick()
    {
        if(!CurrentPlayer.IsMainEditor || !SessionState.GetBool(Key,false)) return;
        if(Status=="NotRunning" && !EditorApplication.isPlayingOrWillChangePlaymode)
        {
            RailStabilityLiveChecks.Disarm(); RailDeepRegressionChecks.Disarm(); RailCoalLiveChecks.Disarm();
            RailEditorQuickStart.Enabled=SessionState.GetBool(Key+".quick",true);
            Manager.GetType().GetProperty("ActivePlayModeConfig").SetValue(Manager,AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(SessionState.GetString(Key+".scenario","")));
            SessionState.SetBool(Key,false);
        }
    }
}
#endif
