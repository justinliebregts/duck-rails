#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class RailDriveChecks
{
    [MenuItem("Duck Rails/Run VR Drive Checks")]
    public static void Run()
    {
        var lease = new RailDriveLease();
        Check(lease.Apply(1, "left", 0, 1, 10) && lease.Actor == 1 && lease.Progress == 0, "Grip starts in STOP even with a GO payload");
        Check(!lease.Apply(2, "other", 0, 0, 10) && !lease.Apply(1, "right", 0, 0, 10), "Another player or hand cannot steal an active hold");
        Check(!lease.Apply(2, "other", 1, 1, 10) && !lease.Apply(2, "other", 2, 0, 10), "Non-holder cannot drive or release");
        Check(lease.Apply(1, "left", 1, .5f, 10.1) && lease.Progress == .5f, "Partial hand travel replicates without GO");
        Check(lease.Apply(1, "left", 1, 1, 10.2) && lease.Progress == 1 && lease.Expires == 11.7, "Held GO renews the timeout");
        Check(lease.Apply(1, "left", 2, 0, 10.3) && lease.Actor == 0 && lease.Progress == 0, "Release clears ownership and GO");
        Check(!lease.Apply(1, "left", 1, 1, 10.4), "Delayed heartbeat cannot reacquire after release");
        Check(lease.Apply(2, "next", 0, 0, 10.5) && !lease.Apply(1, "left", 2, 0, 10.6), "Old release cannot cancel a new holder");
        lease.Reset();
        Check(lease.Actor == 0 && lease.Progress == 0 && !lease.Apply(2, "next", 1, 1, 12), "Timeout/master reset needs a fresh grip");
        Check(RailDriveLease.Travel(-1) == 0 && Mathf.Approximately(RailDriveLease.Travel(.14f), .5f) && RailDriveLease.Travel(2) == 1, "Forward travel is bounded; backward movement stays OFF");
        var root = new GameObject("Drive fixture");
        Check(RailDriveLease.ForwardDetent(false, .8f) && RailDriveLease.ForwardDetent(true, .7f), "Push snaps GO and small backward movements do not chatter");
        Check(!RailDriveLease.ForwardDetent(true, .2f) && !RailDriveLease.ForwardDetent(false, .3f), "Pull back snaps OFF and stays OFF until pushed forward again");
        try
        {
            root.transform.position = new Vector3(10000, 10000, 10000);
            var control = root.AddComponent<RailControl>();
            var pedestal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pedestal.transform.SetParent(root.transform, false);
            pedestal.transform.localScale = new Vector3(.5f, .35f, .5f);
            var handle = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
            handle.SetParent(root.transform, false); handle.localPosition = Vector3.up * .4f;
            handle.localScale = new Vector3(.13f, .8f, .13f);
            control.handle = handle;
            control.ConfigureDriveColliders();
            Physics.SyncTransforms();
            Check(handle.GetComponent<Collider>().isTrigger && pedestal.GetComponent<Collider>().isTrigger, "Handle and base are non-solid interaction triggers");
            Check(!Physics.SphereCast(handle.position - Vector3.forward * 2, .05f, Vector3.forward, out _, 4, ~0, QueryTriggerInteraction.Ignore) &&
                !Physics.SphereCast(pedestal.transform.position - Vector3.forward * 2, .05f, Vector3.forward, out _, 4, ~0, QueryTriggerInteraction.Ignore), "Locomotion casts pass through the lever and base");
            Check(Physics.Raycast(handle.position - Vector3.forward * 2, Vector3.forward, out var target, 4, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide) &&
                target.collider.GetComponentInParent<RailControl>() == control, "Desktop targeting still hits the trigger control");
            Check(handle.GetComponent<Collider>().ClosestPoint(handle.position) == handle.position, "Grip proximity still detects a hand inside the handle trigger");
            var type = typeof(RailControl);
            type.GetField("handleRest", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetValue(control, handle.localPosition);
            var pose = type.GetMethod("PoseDrive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            pose.Invoke(control, new object[] { 0f }); var off = handle.localPosition;
            pose.Invoke(control, new object[] { 1f });
            Check(off.z < 0 && handle.localPosition.z > 0 && Mathf.Approximately(handle.localPosition.magnitude, .4f), "Handle swings forward about the base rather than its centre");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
        Debug.Log("RAILS VR DRIVE: 17 checks passed");
    }
    public static void RunBatch()
    {
        Run(); RailSessionChecks.Run(); RailWeek2Checks.Run();
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Debug.Log("RAILS DRIVE PASS: " + message); }
}
#endif
