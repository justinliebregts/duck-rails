#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RailDuckzPortChecks
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    static void Set(object target, string field, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, Fields);
            if (info != null) { info.SetValue(target, value); return; }
        }
        throw new MissingFieldException(field);
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
    static void Call(object target, string method) => target.GetType().GetMethod(method, Fields).Invoke(target, null);

    public static void Cargo()
    {
        var trainRoot = new GameObject("Port train"); trainRoot.SetActive(false);
        var train = trainRoot.AddComponent<RailTrain>();
        var oldTrain = RailTrain.Instance;
        typeof(RailTrain).GetProperty("Instance").SetValue(null, train);
        var root = new GameObject("Port predicted cargo"); root.SetActive(false);
        var handRoot = new GameObject("Port hand"); handRoot.SetActive(false);
        try
        {
            var view = root.AddComponent<PhotonView>();
            var body = root.AddComponent<Rigidbody>();
            var item = root.AddComponent<PhotonGrabbable>();
            var cargo = root.AddComponent<RailCargo>();
            var hand = handRoot.AddComponent<PlayerHand>();
            Set(item, "rb", body); Set(item, "photonView", view);
            item.enabled = false; cargo.enabled = false;
            root.SetActive(true);
            typeof(PhotonView).GetProperty("IsMine").SetValue(view, false);
            Set(item, "holder", hand); Set(item, "pickupPending", true);
            Set(hand, "held", item);
            Call(cargo, "Awake");
            root.transform.position = new Vector3(0, 1, 0);
            Physics.SyncTransforms();
            Set(hand, "velocitySamples", 2); Set(hand, "sampleFilled", 2); Set(hand, "sampleWrite", 0);
            Set(hand, "posSamples", new[] { Vector3.zero, Vector3.forward * .4f });
            Set(hand, "carrySamples", new[] { Vector3.zero, Vector3.forward * .4f });
            Set(hand, "rotSamples", new[] { Quaternion.identity, Quaternion.identity });
            Set(hand, "timeSamples", new[] { 0f, .1f });
            object[] velocity = { Vector3.zero, Vector3.zero };
            typeof(PlayerHand).GetMethod("ComputeThrowVelocity", Fields).Invoke(hand, velocity);
            Check(((Vector3)velocity[0]).sqrMagnitude < .000001f, "Standing on moving train must not become a throw");
            item.Release((Vector3)velocity[0], Vector3.zero);
            Call(cargo, "LateUpdate");
            Check(body.linearVelocity.sqrMagnitude < .000001f, "Relative release must not subtract train velocity twice");
            var before = body.position;
            cargo.Carry(Vector3.forward);
            Check(Vector3.Distance(body.position, before + Vector3.forward) < .0001f, "Pending ownership release must follow train");
            Check(!view.IsMine, "Predicted carry must not grant publication authority");
            cargo.RestoreFrame(new object[] { true, "old-run", Vector3.one * 100, Quaternion.identity });
            Check(Vector3.Distance(body.position, before + Vector3.forward) < .0001f, "Old checkpoint must not move predicted release");
            cargo.CancelPrediction(); Set(item, "pickupPending", false);
            before = body.position; cargo.Carry(Vector3.forward);
            Check(body.position == before, "Cancelled prediction must stop carrying");
            root.transform.position = Vector3.right * 20;
            Check(!cargo.PrepareRelease(), "Off-train releases must retain world velocity");
            Debug.Log("Rail cargo port: PASS sampled release, pending carry, authority, stale checkpoint, cancellation, off-train release");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(handRoot); UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(trainRoot);
            typeof(RailTrain).GetProperty("Instance").SetValue(null, oldTrain);
        }
    }

    // Use a disposable copied project. Imported fixtures replace scenes and Photon caches.
    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Disposable batch project only");
        var results = new List<string>();
        foreach (Action check in new Action[] {
            ControllerPalmCalibrationChecks.Run, ControllerPalmInteractionChecks.Run, TrackedHandInputChecks.Run,
            HeadPivotMovementChecks.Run, HeadPivotSynchronizationChecks.Run, HeadPivotLifeNetworkChecks.Run,
            HelicopterCabinCollisionChecks.Run, HelicopterSupportChecks.Run, HelicopterNetworkChecks.Run,
            DevelopmentMovementChecks.Run, DevelopmentMovementNetworkChecks.Run,
            PickupRecoveryChecks.Run, PickupRegrabChecks.Run, RoomObjectRecoveryChecks.Run, PlayerKnockbackChecks.Run,
            Cargo, RailTestRunner.Quick })
        {
            try { EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single); check(); results.Add("PASS " + check.Method.DeclaringType.Name); }
            catch (Exception e) { results.Add("FAIL " + check.Method.DeclaringType.Name + ": " + e); }
        }
        Directory.CreateDirectory("Logs");
        File.WriteAllLines("Logs/duckz-port-checks.txt", results);
        Debug.Log(string.Join("\n", results));
        EditorApplication.Exit(results.Exists(x => x.StartsWith("FAIL")) || RailTestRunner.Status.Contains("FAIL") ? 1 : 0);
    }
}
#endif
