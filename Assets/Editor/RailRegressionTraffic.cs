#if UNITY_EDITOR
using System;
using System.IO;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using Unity.Multiplayer.PlayMode;

// Opt-in telemetry for these fixtures only. No account/token/message bodies.
[InitializeOnLoad]
public static class RailRegressionTraffic
{
    static double next;
    static bool hooked, previousStats;
    static int largest, events;
    static RailRegressionTraffic(){EditorApplication.update+=Tick;}
    static string Root {get {string p=Application.dataPath.Replace('\\','/');int n=p.IndexOf("/Library/VP/",StringComparison.OrdinalIgnoreCase);return n<0?Path.GetDirectoryName(p):p.Substring(0,n);}}
    static void Received(ExitGames.Client.Photon.EventData e)
    {
        if(e.Code!=201 && e.Code!=206)return;
        events++;largest=Math.Max(largest,ExitGames.Client.Photon.Protocol.Serialize(e.CustomData).Length);
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying){Unhook();return;}
        if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+5;
        bool armed=new[]{Root+"/Temp/rails-stability/control.json",Root+"/Temp/rails-deep-control.json"}.AnyFileActive();
        if(!armed){Unhook();return;}
        var p=PhotonNetwork.NetworkingClient.LoadBalancingPeer;
        if(!hooked){previousStats=p.TrafficStatsEnabled;p.TrafficStatsEnabled=true;PhotonNetwork.NetworkingClient.EventReceived+=Received;hooked=true;}
        string role=CurrentPlayer.IsMainEditor?"main":string.Join("-",CurrentPlayer.Tags);
        string folder=Root+"/docs/validation/overnight-regression";Directory.CreateDirectory(folder);
        File.AppendAllText(folder+"/traffic-"+role+".txt",DateTime.UtcNow.ToString("O")+" state="+PhotonNetwork.NetworkClientState+" actor="+PhotonNetwork.LocalPlayer.ActorNumber+" master="+PhotonNetwork.IsMasterClient+" phase="+PhotonSession.Phase+" sim="+p.IsSimulationEnabled+" ping="+p.RoundTripTime+" queue="+p.QueuedOutgoingCommands+" elapsedMs="+p.TrafficStatsElapsedMs+" inBytes="+p.TrafficStatsIncoming.TotalPacketBytes+" outBytes="+p.TrafficStatsOutgoing.TotalPacketBytes+" fragmentsOut="+p.TrafficStatsOutgoing.FragmentCommandCount+" events="+events+" maxPoseBytes="+largest+"\n");largest=events=0;
    }
    static void Unhook(){if(!hooked)return;PhotonNetwork.NetworkingClient.EventReceived-=Received;PhotonNetwork.NetworkingClient.LoadBalancingPeer.TrafficStatsEnabled=previousStats;hooked=false;largest=events=0;}
    static bool AnyFileActive(this string[] paths){foreach(var path in paths)try{if(File.Exists(path)&&File.ReadAllText(path).Length>0)return true;}catch(IOException){}return false;}
}
#endif
