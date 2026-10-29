using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;
public static class RecurringAllocationChecks
{
    const BindingFlags P = BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance;
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    static object Get(object o, string name) => o.GetType().GetField(name, P)?.GetValue(o) ?? o.GetType().GetProperty(name, P)?.GetValue(o);
    static PhotonStream Writer(List<object> data) { var s = new PhotonStream(true, null); typeof(PhotonStream).GetField("writeData", P).SetValue(s, data); return s; }
    static void Measure(string name, Action action)
    {
        for (int i=0;i<100;i++) action();
        var watch = Stopwatch.StartNew();
        for (int i=0;i<10000;i++) action();
        watch.Stop();
        using var recorder = Unity.Profiling.ProfilerRecorder.StartNew(Unity.Profiling.ProfilerCategory.Memory, "GC.Alloc", 100000,
            Unity.Profiling.ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        Check(recorder.Valid,"Allocation recorder available");
        for(int i=0;i<100;i++)action();
        recorder.Stop();Check(recorder.Count<recorder.Capacity,"No recorder overflow");
        Debug.Log($"ALLOC BENCH {name}: {recorder.Count/100.0:F2} allocation events/call; {watch.Elapsed.TotalMilliseconds/10000:F6} ms/call");
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Isolated batch only");
        try {
            Codec(); Cache(); ActualDuck(); DuckHomeChecks.Run();
            Debug.Log("RecurringAllocationChecks: ALL PASSED"); EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Codec()
    {
        var wire = new StreamBuffer(256);
        for(int n=0;n<200;n++) {
            var pose = new PosePacket { HeadPos = new Vector3(n,-n,.00001f*n), RHandPos = new Vector3(n+1,-n+1,0), LHandPos = new Vector3(n-1,-n-1,0), HeadRot = Quaternion.Euler(n, n*2, n*3), RHandRot=Quaternion.identity, LHandRot=Quaternion.Euler(n,0,0) };
            object boxed = pose; byte[] original = PosePacket.Serialize(boxed);
            wire.Position=0; wire.WriteByte(91); PosePacket.SerializeStream(wire, boxed); wire.WriteByte(92);
            wire.Position=1; var actual = new byte[39]; wire.Read(actual,0,39);
            Check(Convert.ToBase64String(actual)==Convert.ToBase64String(original), "Pose wire parity");
            wire.Position=1; object decoded=PosePacket.DeserializeStream(wire,39);
            Check(wire.ReadByte()==92, "Codec does not consume next packet");
            Check(Convert.ToBase64String(PosePacket.Serialize(decoded))==Convert.ToBase64String(PosePacket.Serialize(PosePacket.Deserialize(original))), "Decoded parity");
        }
        object value = new PosePacket { HeadRot=Quaternion.identity, RHandRot=Quaternion.identity, LHandRot=Quaternion.identity };
        Measure("pose old encode", ()=>PosePacket.Serialize(value));
        Measure("pose stream encode", ()=>{wire.Position=0;PosePacket.SerializeStream(wire,value);});
        bool rejected=false; try { PosePacket.DeserializeStream(wire,38); } catch(ArgumentException) {rejected=true;}
        Check(rejected,"Reject malformed length");
    }
    static void Cache()
    {
        var data=new List<object>(256); var stream=Writer(data); var cache=new PhotonValueCache();
        Action baseline=()=>{data.Clear();for(int i=0;i<64;i++)stream.SendNext(new Vector3(i,0,0));};
        Action optimized=()=>{data.Clear();cache.Begin();for(int i=0;i<64;i++)cache.Send(stream,new Vector3(i,0,0));};
        baseline(); var old=data.ToArray(); optimized(); for(int i=0;i<64;i++)Check(old[i].Equals(data[i]),"Typed parity");
        var held=data.ToArray(); data.Clear();cache.Begin();cache.Send(stream,new Vector3(1e-8f,0,0));
        Check(!held[0].Equals(data[0]) && ((Vector3)held[0]).x==0,"Tiny changes sent; prior snapshot immutable");
        data.Clear();cache.Begin();for(int i=0;i<260;i++)cache.Send(stream,i);Check(data.Count==260,"Bounded cache overflow still sends");
        Measure("64 history positions baseline",baseline);Measure("64 history positions reused",optimized);
        int revision=0;
        Measure("64 changing positions baseline",()=>{data.Clear();revision++;for(int i=0;i<64;i++)stream.SendNext(new Vector3(i,revision,0));});
        Measure("64 changing positions cached",()=>{data.Clear();cache.Begin();revision++;for(int i=0;i<64;i++)cache.Send(stream,new Vector3(i,revision,0));});
    }
    static void ActualDuck()
    {
        var create=typeof(DuckHomeChecks).GetMethod("Create",BindingFlags.Static|BindingFlags.NonPublic);
        var d=(DuckCompanion)create.Invoke(null,new object[]{"allocation source"});
        var peer=(DuckCompanion)create.Invoke(null,new object[]{"allocation peer"});
        var positions=(List<Vector3>)Get(d,"followProgressPositions");
        var data=new List<object>(256);var stream=Writer(data);
        string[] order="respawnRevision|transform.position|transform.rotation|(byte)State|TargetActor|respawnAt|ReturningAfterDeath|greeted|retreatUntil|retreatActor|retreatLookPosition|followingPaused|recoveryAnchor|lastProgressAt|nextWanderAt|wanderUntil|wanderDestination|exploring|returningFromExplore|failedClimbUntil|failedClimbOrigin|failedClimbDirection|followNoProgressSeconds|followProgressActor|followProgressAnchor|followProgressPositions.Count|position|(byte)Mode|homeInitialized|homeFloorResolved|homeFloorY|homePosition|homeRotation|homeWalking|homeDestination|nextHomeWander|WaitingForFarewell|farewellDeadline|(byte)speechAction|speechActor|waitUntil|visitorActor|visitorArrived|visitorGreetingPending|visitorDeadline|visitorLastProgress|visitorLastSpeech|visitorProgressPosition|greetedVisitors|visitorRetryTimes|conversationLookActor|pendingConversationLookActor|conversationLookUntil|fenceReleaseUntil|fenceReleaseDirection|fenceReleaseStart|failedFenceUntil|failedFenceCrossing|fenceAttemptUntil|fenceAttemptPoint|fenceAttemptNormal|fenceDetourCommitted|fenceDetourTarget".Split('|');
        foreach(int count in new[]{0,64,1,32,0}) {
            positions.Clear();for(int i=0;i<count;i++)positions.Add(new Vector3(i,.1f*i,-i));
            d.Serialize(stream);int slot=0;
            foreach(string expression in order) {
                if(expression=="position") {foreach(var position in positions)Check(position.Equals(data[slot++]),"History parity");continue;}
                object expected;
                string name=expression.Replace("(byte)","");
                if(name=="transform.position")expected=d.transform.position;
                else if(name=="transform.rotation")expected=d.transform.rotation;
                else if(name=="followProgressPositions.Count")expected=count;
                else expected=Get(d,name);
                if(expression.StartsWith("(byte)"))expected=Convert.ToByte(expected);
                Check(Equals(expected,data[slot++]),"Legacy field order/value: "+expression);
            }
            peer.Serialize(new PhotonStream(false,data.ToArray()));
            Check(((List<Vector3>)Get(peer,"followProgressPositions")).Count==count,"Remote history count");
            data.Clear();peer.Serialize(stream); d.Serialize(new PhotonStream(false,data.ToArray())); // new authority retransmits
            data.Clear();
        }
        for(int i=0;i<64;i++)positions.Add(new Vector3(i,0,0));
        Measure("whole Duck serialization warm",()=>{data.Clear();d.Serialize(stream);});
        var ambient = new GameObject("Thunder").AddComponent<AudioSource>();
        var refresh=typeof(DuckCompanionDialogue).GetMethod("RefreshAmbientSources",BindingFlags.NonPublic|BindingFlags.Instance);
        var sources=(System.Collections.IDictionary)Get(d.Dialogue,"ambientSources");
        refresh.Invoke(d.Dialogue,null);Check(sources.Contains(ambient),"Ambient source discovered");
        ambient.gameObject.SetActive(false);refresh.Invoke(d.Dialogue,null);Check(!sources.Contains(ambient),"Inactive ambient source removed");
        ambient.gameObject.SetActive(true);refresh.Invoke(d.Dialogue,null);Check(sources.Contains(ambient),"Reactivated ambient source found");
        UnityEngine.Object.DestroyImmediate(ambient.gameObject);refresh.Invoke(d.Dialogue,null);Check(!sources.Contains(ambient),"Destroyed source removed");
        UnityEngine.Object.DestroyImmediate(d.gameObject);UnityEngine.Object.DestroyImmediate(peer.gameObject);
    }
}
