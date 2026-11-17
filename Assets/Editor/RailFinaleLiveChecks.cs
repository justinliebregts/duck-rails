#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Game.AI;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class RailFinaleLiveChecks
{
    const string Key="Rails.FinaleLive";
    const string Folder="output/bridge-finale-live";
    static double tick, started, ready, lastLog, completed;
    static bool placed, held, wave, done;
    static readonly Dictionary<string,Vector3> previous=new();
    static readonly Dictionary<string,float> minimum=new();
    static readonly HashSet<string> jumped=new();
    static RailFinaleLiveChecks(){EditorApplication.update+=Tick;}
    public static void Start()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Stop Play first");
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Folder+"/trace.txt","Bridge finale live test "+DateTime.UtcNow.ToString("O")+" Unity "+Application.unityVersion+"\n");
        SessionState.SetString(Key+".order",EditorJsonUtility.ToJson(RailTestBlockOrder.instance));
        SessionState.SetBool(Key,true);
        RailTestBlockOrder.instance.useOrder=true;
        RailTestBlockOrder.instance.blocks=new(){AssetDatabase.LoadAssetAtPath<GameObject>("Assets/DuckRails/Blocks/convenience-store-v1.prefab").GetComponent<RailBlock>()};
        RailTestBlockOrder.instance.invincibleFromEnemies=true;
        RailTestBlockOrder.instance.Persist();
        StageUtility.GoToMainStage();
        RailTestRunner.Start("sandbox");
    }
    public static void Stop()
    {
        RailBridgeHoldFixture.SetHeld(false);SessionState.SetBool(Key,false);
        EditorJsonUtility.FromJsonOverwrite(SessionState.GetString(Key+".order","{}"),RailTestBlockOrder.instance);
        RailTestBlockOrder.instance.Persist();RailTestBlockOrderPanel.PublishPlayerOptions();
        RailTestRunner.Stop("Bridge finale focused playtest ended; see output/bridge-finale-live/trace.txt");
    }
    static void Log(string text)=>File.AppendAllText(Folder+"/trace.txt",DateTime.UtcNow.ToString("O")+" "+text+"\n");
    static void Tick()
    {
        if(!SessionState.GetBool(Key,false)||!EditorApplication.isPlaying||done||EditorApplication.timeSinceStartup<tick)return;
        tick=EditorApplication.timeSinceStartup+.1;
        if(started==0)started=tick;
        try
        {
            if(tick-started>360)throw new Exception("Overall timeout");
            var run=RailExpedition.Instance;var route=RailRoute.Instance;
            if(run==null||route==null||!route.Ready||PhotonSession.Phase!=RailPhase.Running||!PhotonSession.Instance.WorldReady)return;
            if(!placed)
            {
                RailTestTools.Bridge();NewPlayer.instance.Warp(run.bridgeButton.position+new Vector3(0,0,-1.5f),Quaternion.identity);
                placed=true;ready=tick;
                Log("Ready authenticated solo; train at bridge; operator invincible through existing test option. Production hold and enemy locomotion unchanged.");
            }
            if(!held&&tick-ready>3){held=true;RailBridgeHoldFixture.SetHeld(true);Log("Physical lever fixture enabled; no bridge time/state injection");}
            var defenders=Enemy.Active.Where(e=>RailBridgeEncounter.IsDefender(e.GetComponent<PhotonView>()?.InstantiationData?.LastOrDefault() as string,run.baseEnemyCount,run.enemyPoints.Length)).ToArray();
            var goal=run.bridgeButton.position+Vector3.back*1.4f;
            foreach(var e in defenders)
            {
                string marker=e.GetComponent<PhotonView>().InstantiationData.Last() as string;
                var p=e.Root.position;float distance=Vector3.Distance(p,goal);
                if(!minimum.ContainsKey(marker)){minimum[marker]=distance;Log("SPAWN "+marker+" held="+run.State.BridgeElapsed(PhotonNetwork.Time).ToString("F3")+" position="+p.ToString("F3"));}
                if(previous.TryGetValue(marker,out var prev)&&Vector3.Distance(prev,p)>3){jumped.Add(marker);Log("DISCONTINUITY "+marker+" "+prev.ToString("F3")+" -> "+p.ToString("F3"));}
                previous[marker]=p;
                if(distance<minimum[marker]){if(distance<=2&&minimum[marker]>2)Log("REACHED controls "+marker+" held="+run.State.BridgeElapsed(PhotonNetwork.Time).ToString("F3"));minimum[marker]=distance;}
            }
            double work=run.State.BridgeElapsed(PhotonNetwork.Time);
            if(!wave&&run.State.bridgeReinforcementsTriggered){wave=true;Log("WAVE retained trigger held="+work.ToString("F3")+" count="+run.State.bridgeReinforcementCount);}
            if(tick-lastLog>=1){lastLog=tick;Log("SAMPLE held="+work.ToString("F3")+" moving="+run.State.BridgeMoving(PhotonNetwork.Time)+" count="+defenders.Length+" enemies="+string.Join(";",defenders.Select(e=>e.Root.position.ToString("F2")+" "+e.CurrentState)));}
            if(work>=60&&completed==0){completed=tick;Log("COMPLETE held="+work.ToString("F3")+" progress="+run.State.BridgeProgress(PhotonNetwork.Time)+" deck="+run.bridgeDeck.localEulerAngles.ToString("F3"));RailBridgeHoldFixture.SetHeld(false);}
            if(completed>0&&tick-completed>5)
            {
                var initial=minimum.Where(k=>k.Key.StartsWith("enemy-")).ToArray();
                bool pass=initial.Length==2&&initial.All(k=>k.Value<=2&&!jumped.Contains(k.Key))&&wave&&defenders.Length==3;
                Log((pass?"PASS":"FAIL")+" SUMMARY "+string.Join(";",minimum.Select(k=>k.Key+" minDistance="+k.Value.ToString("F3")+" discontinuity="+jumped.Contains(k.Key))));done=true;
            }
            if(held&&tick-ready>150&&completed==0)throw new Exception("Bridge did not complete within 150 real seconds; inspect physical hold and movement trace");
        }
        catch(Exception ex){Log("FAIL "+ex);done=true;RailBridgeHoldFixture.SetHeld(false);}
    }
}
#endif
