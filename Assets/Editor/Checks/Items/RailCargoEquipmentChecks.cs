using System;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

public static class RailCargoEquipmentChecks
{
    [MenuItem("Duck Rails/Tests/Check worn cargo exclusion")]
    public static void Run()
    {
        if (Application.isPlaying || PhotonNetwork.IsConnected) throw new InvalidOperationException("Run disconnected in Edit Mode.");
        var previousTrain = RailTrain.Instance;
        var previousPool = PhotonNetwork.PrefabPool;
        if (previousPool == null) PhotonNetwork.PrefabPool = new DefaultPool();
        var trainObject = new GameObject("Equipment cargo test train");
        var anchor = new GameObject("Equipment cargo test head");
        GameObject item = null;
        int checks = 0;
        try
        {
            var train = trainObject.AddComponent<RailTrain>();
            RailRemoteBridge.Call(train, "OnEnable");
            train.transform.SetPositionAndRotation(new Vector3(13, 2, 27), Quaternion.Euler(0, 35, 0));
            anchor.transform.SetPositionAndRotation(train.transform.TransformPoint(new Vector3(0, 1, 0)), Quaternion.Euler(15, 70, 5));
            foreach (string prefab in new[] { "RailHelmet", "RailGoggles" })
            {
                item = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Rails/" + prefab + ".prefab"));
                var gear = item.GetComponent<HeadEquipment>();
                var grab = item.GetComponent<PhotonGrabbable>();
                var cargo = item.GetComponent<RailCargo>();
                var sync = item.GetComponent<PhotonTransformView>();
                var view = item.GetComponent<PhotonView>();
                RailRemoteBridge.Call(grab, "Awake");
                RailRemoteBridge.Call(gear, "Awake");
                RailRemoteBridge.Call(cargo, "Awake");
                RailRemoteBridge.Set(gear, "equipped", true);
                RailRemoteBridge.Set(gear, "remoteEquipped", true);
                RailRemoteBridge.Set(gear, "equippedAnchor", anchor.transform);
                RailRemoteBridge.Call(gear, "ApplyEquippedState");
                object[] stale = { true, PhotonSession.RunId, new Vector3(1, 1, -2), Quaternion.identity };
                foreach (bool mine in new[] { false, true })
                {
                    RailRemoteBridge.Set(view, "IsMine", mine);
                    sync.enabled = false;
                    cargo.RestoreFrame(stale);
                    CheckPose();
                    RailRemoteBridge.Call(cargo, "LateUpdate");
                    CheckPose();
                    Require(!(bool)cargo.CaptureFrame()[0], "Worn gear captured as loose cargo"); checks++;
                    Require(!sync.enabled, "Cargo reenabled worn world interpolation"); checks++;
                    cargo.Carry(new Vector3(2, 0, 3));
                    CheckPose();
                    cargo.Carry(train.transform.position, train.transform.rotation, train.transform.position + Vector3.forward, Quaternion.Euler(0, 80, 0));
                    CheckPose();
                }
                RailRemoteBridge.Set(gear, "equipped", false);
                Require((bool)cargo.CaptureFrame()[0], "Dropped gear must resume train-relative cargo"); checks++;
                RailRemoteBridge.Set(view, "IsMine", false);
                cargo.RestoreFrame(stale);
                Require(Vector3.Distance(item.transform.position, train.transform.TransformPoint((Vector3)stale[2])) < .0001f, "Dropped remote gear ignored cargo frame"); checks++;
                UnityEngine.Object.DestroyImmediate(item); item = null;

                void CheckPose()
                {
                    Require(Vector3.Distance(item.transform.position, anchor.transform.position) < .0001f && Quaternion.Angle(item.transform.rotation, anchor.transform.rotation) < .01f,
                        prefab + " cargo overwrote worn head pose");
                    checks++;
                }
            }
            Debug.Log("RailCargoEquipmentChecks PASS: " + checks + " assertions; both Rails prefabs, owner/remote, stale frame restore, both carry paths, dropped cargo resumption.");
        }
        finally
        {
            if (item != null) UnityEngine.Object.DestroyImmediate(item);
            UnityEngine.Object.DestroyImmediate(anchor);
            UnityEngine.Object.DestroyImmediate(trainObject);
            RailRemoteBridge.Set(typeof(RailTrain), "<Instance>k__BackingField", previousTrain);
            PhotonNetwork.PrefabPool = previousPool;
        }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
}
