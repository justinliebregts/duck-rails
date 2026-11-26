using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
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
        // Keep analytics inert in Edit Mode.
        var scene = EditorSceneManager.NewPreviewScene();
        var inert = new GameObject("Disabled regression analytics");
        inert.SetActive(false);
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(inert, scene);
        var analytics = typeof(DuckzAnalytics).GetProperty("Instance");
        var previous = DuckzAnalytics.Instance;
        analytics.SetValue(null, inert.AddComponent<DuckzAnalytics>());
        try {
            Codec(); Cache();
            Debug.Log("RecurringAllocationChecks: ALL PASSED"); EditorApplication.Exit(0);
        } catch(Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        finally {
            analytics.SetValue(null, previous);
            EditorSceneManager.ClosePreviewScene(scene);
        }
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
}
