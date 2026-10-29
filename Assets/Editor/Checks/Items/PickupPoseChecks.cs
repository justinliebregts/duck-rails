using System;
using System.Collections.Generic;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

// Run from the menu or with -executeMethod PickupPoseChecks.Run in batch mode.
public static class PickupPoseChecks
{
    [MenuItem("Tools/DuckZ/Validation/Items/Validate Continuous Pickup")]
    public static void Run()
    {
        string[] paths = {
            "Assets/Prefabs/Items/ItemWalkieTalkie.prefab",
            "Assets/Prefabs/Items/ItemFlashlight.prefab",
            "Assets/Prefabs/Items/ItemNightVisionGoggles.prefab",
            "Assets/Prefabs/Weapons/WeaponFryPan.prefab"
        };
        GameObject helicopter = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonHelicopter.prefab");
        int checkedDisplays = 0;
        foreach (string path in paths)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Require(prefab != null, path);
            var parent = new GameObject("Rotated bag check");
            try
            {
                parent.transform.SetPositionAndRotation(new Vector3(2, 3, -4), Quaternion.Euler(25, 137, -18));
                parent.transform.localScale = Vector3.one * 1.2f;
                GameObject mini = BagMiniModel.Build(prefab, prefab.name, parent.transform, 0.07f);
                Verify(prefab, mini.transform);
                foreach (StoreWallItem wall in helicopter.GetComponentsInChildren<StoreWallItem>(true))
                {
                    if (!SharesMesh(prefab.transform, wall.transform)) continue;
                    Verify(prefab, wall.transform);
                    checkedDisplays++;
                    break;
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(parent); }
        }
        Require(checkedDisplays == paths.Length, "All four wall displays must be checked");
        VerifyStream();
        VerifySurfaceStreams();
        Debug.Log("PickupPoseChecks PASS: four bag miniatures and four wall displays; mesh alignment, anchored growth, remote pose reconstruction, delayed arrival and completion.");
    }

    // Exercise the new surface correction together with actual PUN spawn/stream callbacks.
    // Separate probes represent owner/observer clocks; this does not simulate server delivery.
    static void VerifySurfaceStreams()
    {
        int cases = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Items", "Assets/Prefabs/Weapons" }))
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
            if (prefab.GetComponent<PhotonGrabbable>() == null) continue;
            foreach (bool left in new[] { false, true })
            foreach (double arrival in new[] { 0.0, 0.05, 0.30 })
            {
                var parent = new GameObject("Corrected network bag");
                var handObject = new GameObject("Network source hand");
                var ownerObject = new GameObject("Corrected network owner");
                var remoteObject = new GameObject("Corrected network observer");
                try
                {
                    parent.transform.SetPositionAndRotation(new Vector3(2, 3, -4), Quaternion.Euler(25, 137, -18));
                    parent.transform.localScale = Vector3.one * 1.2f;
                    GameObject mini = BagMiniModel.Build(prefab, prefab.name, parent.transform, .065f);
                    PlayerHand hand = handObject.AddComponent<PlayerHand>();
                    hand.controller = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
                    hand.transform.SetPositionAndRotation(parent.transform.position + new Vector3(.024f, .024f, -.036f), Quaternion.Euler(-15, 63, 21));
                    ItemPickupPose pose = ItemPickupPose.Capture(prefab, mini.transform.GetChild(0), hand.HoldPoint.position);
                    Require(pose.ClampGripToSurface(prefab, hand.HoldPoint.position), "corrected spawn grip");
                    var owner = ownerObject.AddComponent<NetworkPickupGrowthProbe>();
                    var remote = remoteObject.AddComponent<NetworkPickupGrowthProbe>();
                    MethodInfo awake = typeof(PhotonGrabbable).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                    awake.Invoke(owner, null); awake.Invoke(remote, null);
                    PhotonView ownerView = owner.GetComponent<PhotonView>(), remoteView = remote.GetComponent<PhotonView>();
                    typeof(PhotonView).GetProperty("IsMine").SetValue(ownerView, true);
                    typeof(PhotonView).GetProperty("IsMine").SetValue(remoteView, false);
                    owner.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    remote.transform.SetPositionAndRotation(pose.position, pose.rotation);
                    owner.transform.localScale = remote.transform.localScale = pose.fullScale;
                    Quaternion inverse = Quaternion.Inverse(hand.transform.rotation);
                    // Same nine fields emitted by ItemPickupPose.Spawn; no protocol additions.
                    object[] spawn = { "pickup-pose-v1", pose.startScale, pose.fullScale, pose.localGrip, 0d,
                        PhotonNetwork.LocalPlayer.ActorNumber, left,
                        inverse * (pose.position - hand.transform.position), inverse * pose.rotation };
                    typeof(PhotonView).GetProperty("InstantiationData").SetValue(ownerView, spawn);
                    typeof(PhotonView).GetProperty("InstantiationData").SetValue(remoteView, spawn);
                    owner.OnPhotonInstantiate(default);
                    owner.Grab(hand);
                    Require(owner.Holder == hand, "corrected network owner grab");
                    remote.clock = arrival;
                    remote.OnPhotonInstantiate(default);
                    VectorNear(remote.transform.TransformPoint(pose.localGrip), hand.HoldPoint.position, "spawn arrival keeps corrected contact");

                    foreach (double time in new[] { arrival, arrival + .07, arrival + .20 })
                    {
                        hand.transform.position += new Vector3(.03f, -.02f, .01f);
                        hand.transform.rotation *= Quaternion.Euler(4, 9, -3);
                        owner.clock = remote.clock = time;
                        owner.TickOwner();
                        var writing = WriteStream();
                        owner.OnPhotonSerializeView(writing, default);
                        Require(writing.ToArray().Length == 11, "corrected held schema unchanged");
                        writing.SendNext(1234);
                        var reading = new PhotonStream(false, writing.ToArray());
                        remote.OnPhotonSerializeView(reading, default);
                        Require((int)reading.ReceiveNext() == 1234, "corrected packet consumes exactly its fields");
                        Vector3 offset = (Vector3)typeof(PhotonGrabbable).GetField("remoteGripPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(remote);
                        Quaternion rotation = (Quaternion)typeof(PhotonGrabbable).GetField("remoteGripRotation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(remote);
                        bool wireLeft = (bool)typeof(PhotonGrabbable).GetField("remoteLeftHand", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(remote);
                        Require(wireLeft == left, "correct hand survives serialization");
                        remote.Reconstruct(hand.transform.position + hand.transform.rotation * offset, hand.transform.rotation * rotation);
                        VectorNear(remote.transform.position, owner.transform.position, "corrected network root");
                        VectorNear(remote.transform.lossyScale, owner.transform.lossyScale, "corrected network scale");
                        Require(Quaternion.Angle(remote.transform.rotation, owner.transform.rotation) < .01f, "corrected network orientation");
                        VectorNear(remote.transform.TransformPoint(pose.localGrip), hand.HoldPoint.position, "corrected remote grip follows moving hand");
                        prefab.GetComponent<Grabbable>().GrabSurface.TryClosestPoint(remote.transform.localToWorldMatrix, hand.HoldPoint.position, out Vector3 contact);
                        VectorNear(contact, hand.HoldPoint.position, "remote rendered surface touches hand");
                    }
                    owner.Release(Vector3.zero, Vector3.zero);
                    var released = WriteStream();
                    owner.OnPhotonSerializeView(released, default);
                    Require(released.ToArray().Length == 9, "corrected release schema unchanged");
                    remote.OnPhotonSerializeView(new PhotonStream(false, released.ToArray()), default);
                    Require(!remote.IsHeldByAnyPlayer, "observer accepts release after corrected growth");
                    cases++;
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(ownerObject);
                    UnityEngine.Object.DestroyImmediate(remoteObject);
                    UnityEngine.Object.DestroyImmediate(handObject);
                    UnityEngine.Object.DestroyImmediate(parent);
                }
            }
        }
        Require(cases >= 60, "all ten prefabs, both hands and three arrival delays");
        Debug.Log($"Pickup surface network PASS: {cases} cases through actual PUN spawn and stream callbacks; corrected mesh contact, moving hands, delayed arrival, full growth and release.");
    }

    static void VerifyStream()
    {
        var ownerObject = new GameObject("Serialized owner check");
        var remoteObject = new GameObject("Serialized remote check");
        var handObject = new GameObject("Serialized hand check");
        try
        {
            PhotonGrabbable owner = ownerObject.AddComponent<PhotonGrabbable>();
            PhotonGrabbable remote = remoteObject.AddComponent<PhotonGrabbable>();
            if (!Application.isPlaying)
            {
                // Edit Mode does not invoke runtime Awake on these synthetic objects.
                MethodInfo awake = typeof(PhotonGrabbable).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic);
                awake.Invoke(owner, null);
                awake.Invoke(remote, null);
            }
            PlayerHand hand = handObject.AddComponent<PlayerHand>();
            hand.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(23, 64, -17));
            owner.transform.SetPositionAndRotation(new Vector3(1.1f, 2.2f, 3.3f), Quaternion.Euler(56, 78, 90));
            FieldInfo holder = typeof(Grabbable).GetField("holder", BindingFlags.Instance | BindingFlags.NonPublic);
            holder.SetValue(owner, hand);
            var writing = WriteStream();
            owner.OnPhotonSerializeView(writing, default);
            object[] packet = writing.ToArray();
            Require(packet.Length == 11, "Held stream field count (including recovery revision)");
            remote.OnPhotonSerializeView(new PhotonStream(false, packet), default);
            Vector3 offset = (Vector3)typeof(PhotonGrabbable).GetField("remoteGripPosition", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(remote);
            Quaternion rotation = (Quaternion)typeof(PhotonGrabbable).GetField("remoteGripRotation", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(remote);
            VectorNear(hand.transform.position + hand.transform.rotation * offset, owner.transform.position, "serialized grip position");
            Require(Quaternion.Angle(hand.transform.rotation * rotation, owner.transform.rotation) < 0.01f, "serialized grip rotation");
            holder.SetValue(owner, null);
            writing = WriteStream();
            owner.OnPhotonSerializeView(writing, default);
            packet = writing.ToArray();
            Require(packet.Length == 9, "Released stream field count (including recovery revision)");
            remote.OnPhotonSerializeView(new PhotonStream(false, packet), default);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ownerObject);
            UnityEngine.Object.DestroyImmediate(remoteObject);
            UnityEngine.Object.DestroyImmediate(handObject);
        }
    }

    static PhotonStream WriteStream()
    {
        var stream = new PhotonStream(true, null);
        // PUN supplies this internal buffer when dispatching serialization callbacks.
        typeof(PhotonStream).GetField("writeData", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(stream, new List<object>());
        return stream;
    }

    static bool SharesMesh(Transform a, Transform b)
    {
        foreach (MeshFilter source in a.GetComponentsInChildren<MeshFilter>(true))
            foreach (MeshFilter target in b.GetComponentsInChildren<MeshFilter>(true))
                if (source.sharedMesh != null && source.sharedMesh == target.sharedMesh) return true;
        return false;
    }

    static void Verify(GameObject prefab, Transform display)
    {
        Vector3 grip = display.position + display.rotation * new Vector3(0.012f, 0.025f, -0.008f);
        ItemPickupPose pose = ItemPickupPose.Capture(prefab, display, grip);
        var ownerObject = new GameObject("Owner check");
        var remoteObject = new GameObject("Remote check");
        try
        {
            PickupGrowthProbe owner = ownerObject.AddComponent<PickupGrowthProbe>();
            PickupGrowthProbe remote = remoteObject.AddComponent<PickupGrowthProbe>();
            owner.transform.SetPositionAndRotation(pose.position, pose.rotation);
            owner.transform.localScale = pose.fullScale;
            owner.BeginPickupGrowth(pose.startScale, pose.fullScale, pose.localGrip, 0);
            VectorNear(owner.transform.TransformPoint(pose.localGrip), grip, "initial grip");
            foreach (MeshFilter source in prefab.GetComponentsInChildren<MeshFilter>(true))
            foreach (MeshFilter target in display.GetComponentsInChildren<MeshFilter>(true))
            {
                if (source.sharedMesh == null || source.sharedMesh != target.sharedMesh) continue;
                Matrix4x4 mapped = owner.transform.localToWorldMatrix * prefab.transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
                foreach (Vector3 point in new[] { Vector3.zero, Vector3.right, Vector3.up, Vector3.forward })
                    VectorNear(mapped.MultiplyPoint3x4(point), target.transform.TransformPoint(point), "display mesh alignment");
            }
            remote.transform.SetPositionAndRotation(pose.position, pose.rotation);
            remote.transform.localScale = pose.fullScale;
            remote.clock = 0.05; // Arrival after part of the animation has elapsed.
            remote.BeginPickupGrowth(pose.startScale, pose.fullScale, pose.localGrip, 0);
            foreach (double time in new[] { 0.05, 0.10, 0.16 })
            {
                owner.clock = remote.clock = time;
                owner.Tick();
                VectorNear(owner.transform.TransformPoint(pose.localGrip), grip, "growth grip");
                // Same hand-relative encoding and decoding used by PhotonGrabbable.
                Quaternion handRotation = Quaternion.Euler(-15, 63, 21);
                Vector3 handPosition = new Vector3(-1, 2, 3);
                Quaternion inverse = Quaternion.Inverse(handRotation);
                Vector3 wirePosition = inverse * (owner.transform.position - owner.Offset - handPosition);
                Quaternion wireRotation = inverse * owner.transform.rotation;
                remote.transform.SetPositionAndRotation(handPosition + handRotation * wirePosition, handRotation * wireRotation);
                remote.TickRemote();
                VectorNear(owner.transform.position, remote.transform.position, "remote position");
                VectorNear(owner.transform.lossyScale, remote.transform.lossyScale, "remote scale");
                Require(Quaternion.Angle(owner.transform.rotation, remote.transform.rotation) < 0.01f, "remote rotation");
            }
            VectorNear(owner.transform.lossyScale, pose.fullScale, "full size");
            VectorNear(owner.transform.position, pose.position, "final root");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ownerObject);
            UnityEngine.Object.DestroyImmediate(remoteObject);
        }
    }

    static void VectorNear(Vector3 actual, Vector3 expected, string message)
    {
        Require(Vector3.Distance(actual, expected) < 0.001f, message + $": {actual} != {expected}");
    }

    static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception("PickupPoseChecks: " + message);
    }
}

public class PickupGrowthProbe : Grabbable
{
    public double clock;
    protected override double PickupTime => clock;
    public Vector3 Offset => PickupOffset;
    public void Tick() => ApplyPickupGrowth();
    public void TickRemote() => ApplyPickupGrowth(true);
}

public class NetworkPickupGrowthProbe : PhotonGrabbable
{
    public double clock;
    protected override double PickupTime => clock;
    public void TickOwner() => ApplyPickupGrowth();
    public void Reconstruct(Vector3 position, Quaternion rotation)
    {
        transform.SetPositionAndRotation(position, rotation);
        ApplyPickupGrowth(true);
    }
}
