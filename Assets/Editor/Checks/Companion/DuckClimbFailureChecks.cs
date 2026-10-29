using System;
using System.Reflection;
using Photon.Pun;
using UnityEngine;
using UnityEditor.SceneManagement;

public static class DuckClimbFailureChecks
{
    const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(DuckCompanion d, string name, params object[] args) => typeof(DuckCompanion).GetMethod(name, Flags).Invoke(d, args);
    static void Set(DuckCompanion d, string name, object value) => typeof(DuckCompanion).GetField(name, Flags).SetValue(d, value);
    static object Get(DuckCompanion d, string name) => typeof(DuckCompanion).GetField(name, Flags).GetValue(d);
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Debug.Log("PASS: " + message); }
    static DuckCompanion Create()
    {
        var go = new GameObject("Climb check");
        go.AddComponent<PhotonView>();
        var duck = go.AddComponent<DuckCompanion>();
        Call(duck, "Awake");
        Set(duck, "environment", (LayerMask)(1 << 6));
        go.GetComponent<CharacterController>().enabled = true;
        return duck;
    }
    public static void Run()
    {
        Check(Application.isBatchMode, "isolated batch fixture");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        typeof(PhotonNetwork).GetField("offlineMode", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, true);
        typeof(PhotonNetwork).GetField("frametime", BindingFlags.Static | BindingFlags.NonPublic).SetValue(null, 100d);
        var duck = Create();
        var blocker = new GameObject("Slope lip obstruction");
        blocker.layer = 6;
        blocker.transform.position = new Vector3(0, 1.5f, 0);
        blocker.AddComponent<BoxCollider>().size = new Vector3(3, .2f, 3);
        Physics.SyncTransforms();
        typeof(DuckCompanion).GetProperty("Mode").SetValue(duck, DuckCompanion.CompanionMode.ReturningHome);
        typeof(DuckCompanion).GetProperty("State").SetValue(duck, DuckCompanion.Activity.Climbing);
        Set(duck, "traversalStart", Vector3.zero);
        Set(duck, "traversalEnd", new Vector3(0, 3, 1));
        Set(duck, "traversalDuration", 2f);
        Set(duck, "climbLiftDuration", 1.5f);
        Set(duck, "lastProgressAt", 95d);
        double beforeClimb = PhotonNetwork.Time;
        for (int i = 0; i < 60 && duck.State == DuckCompanion.Activity.Climbing; i++)
            Call(duck, "AdvanceTraversal", .05f);
        Debug.Log($"Climb result: {duck.State}, avoidance {Get(duck, "failedClimbUntil")}, position {duck.transform.position}");
        double deadline = (double)Get(duck, "failedClimbUntil");
        Check(duck.State == DuckCompanion.Activity.Following && deadline >= beforeClimb + 30 && deadline <= PhotonNetwork.Time + 30,
            "blocked production climb exits and records thirty-second avoidance");
        Check(duck.Mode == DuckCompanion.CompanionMode.ReturningHome && (double)Get(duck, "lastProgressAt") == 95d,
            "failure preserves home intent and recovery clock");
        Check((bool)Call(duck, "AvoidFailedClimb", Vector3.zero, new Vector3(0, 2, 1)),
            "same upward approach cannot immediately repeat");
        Check(!(bool)Call(duck, "AvoidFailedClimb", Vector3.zero, new Vector3(0, -2, 1))
            && !(bool)Call(duck, "AvoidFailedClimb", Vector3.zero, new Vector3(0, 2, -1))
            && !(bool)Call(duck, "AvoidFailedClimb", Vector3.right * 4, new Vector3(4, 2, 1)),
            "descent, opposite approach and distant approach remain available");
        var peer = Create();
        var stream = new PhotonStream(true, null);
        typeof(PhotonStream).GetMethod("SetWriteStream", Flags).Invoke(stream,
            new object[] { new System.Collections.Generic.List<object>(), 0 });
        Call(duck, "SerializeHome", stream);
        Call(peer, "SerializeHome", new PhotonStream(false, stream.ToArray()));
        Call(peer, "ResetNavigationAfterAuthorityChange");
        Check((bool)Call(peer, "AvoidFailedClimb", Vector3.zero, new Vector3(0, 2, 1))
            && (double)Get(peer, "failedClimbUntil") == deadline, "master takeover retains failed climb avoidance");
        Set(peer, "failedClimbUntil", PhotonNetwork.Time - 1);
        Check(!(bool)Call(peer, "AvoidFailedClimb", Vector3.zero, new Vector3(0, 2, 1)), "avoidance expires without permanently blocking climbing");
        Debug.Log("ALL CLIMB FAILURE CHECKS PASSED");
    }
}
