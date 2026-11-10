using System;
using System.Collections.Generic;
using ExitGames.Client.Photon;
using Photon.Pun;
using UnityEditor;
using UnityEngine;

// Add -holderCheck to each of the three disposable live Photon clients.
public static partial class PickupJoinNetworkChecks
{
    static bool monitor, requireBoth, stationary, skipHeartbeat, skipReplies;
    static int heartbeatsSkipped, replies, repliesSkipped, masterDrops;
    static Transform headInput;
    static PhotonPlayer targetAvatar;
    static Photon.Realtime.Room originalRoom;
    static double TrialTime => EditorApplication.timeSinceStartup;
    static bool HoldingBoth() => bat.HeldActor == 2 && light.HeldActor == 2 &&
        bat.GetComponent<PhotonView>().OwnerActorNr == 2 && light.GetComponent<PhotonView>().OwnerActorNr == 2 &&
        (role != 2 || (bat.IsHeld && light.IsHeld && !bat.PickupPending && !light.PickupPending));
    static bool ReleasedBoth() => !bat.IsHeldByAnyPlayer && !light.IsHeldByAnyPlayer;
    static bool RoomOwnsBoth() => ReleasedBoth() && bat.GetComponent<PhotonView>().OwnerActorNr == 0 &&
        light.GetComponent<PhotonView>().OwnerActorNr == 0;

    static void MonitorHolderTrial()
    {
        if (!monitor) return;
        Check(ReferenceEquals(originalRoom, PhotonNetwork.CurrentRoom) && originalRoom.PlayerCount == 3 &&
            originalRoom.MasterClientId == 1 && PhotonNetwork.LocalPlayer.ActorNumber == role, "unexpected room, master or member change");
        if (requireBoth) Check(HoldingBoth(), "a confirmed hold was lost");
        if (role != 2) return;
        float x = stationary ? 0 : 0.65f * Mathf.Sin((float)(TrialTime % 1000) * 2);
        headInput.position = new Vector3(x, 2.2f, 0);
        left.transform.position = new Vector3(x - 0.3f, 1.7f, 0.2f);
        right.transform.position = new Vector3(x + 0.3f, 1.7f, 0.2f);
    }

    static void FilterHolderEvents(EventData e)
    {
        if (e.Sender == 2 && skipHeartbeat && e.Code == 105) { heartbeatsSkipped++; return; }
        if (e.Sender == 2 && e.Code == 117)
        {
            if (skipReplies) { repliesSkipped++; return; }
            replies++;
        }
        handoff.OnEvent(e);
    }

    static void GrabBoth() { if (role == 2) { Grab(left, bat); Grab(right, light); } }

    static void Streams(bool enabled)
    {
        if (role != 2) return;
        foreach (var view in new[] { targetAvatar.photonView, bat.GetComponent<PhotonView>(), light.GetComponent<PhotonView>() })
            view.Synchronization = enabled ? ViewSynchronization.UnreliableOnChange : ViewSynchronization.Off;
    }

    static IEnumerable<Func<bool>> KeepBoth(string stage, int seconds)
    {
        yield return Until(stage + " confirmed", HoldingBoth);
        requireBoth = true;
        yield return Barrier(stage + "-start");
        double until = TrialTime + seconds;
        yield return Until(stage, () => TrialTime >= until);
        requireBoth = false;
        yield return Barrier(stage + "-end");
        Debug.Log($"HOLDER PASS {stage} actor={role} master=1 holders=2,2 seconds={seconds}");
    }

    static IEnumerable<Func<bool>> HolderTrial()
    {
        if (role == 1)
        {
            Mark("join2");
            yield return Until("second actor", () => PhotonNetwork.CurrentRoom.PlayerCount >= 2);
            Mark("join3");
        }
        yield return Until("three actors", () => PhotonNetwork.CurrentRoom.PlayerCount == 3);
        originalRoom = PhotonNetwork.CurrentRoom;
        var manager = new GameObject("fixture manager").AddComponent<GameManager>(); manager.enabled = false;
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab");
        PhotonPreparePool.Register(prefab);
        yield return Barrier("registered");
        if (role == 2)
        {
            PhotonPlayer.Active.Remove(PhotonPlayer.Local);
            headInput = new GameObject("tracked head input").transform;
            targetAvatar = PhotonNetwork.Instantiate(prefab.name, Vector3.zero, Quaternion.identity).GetComponent<PhotonPlayer>();
            targetAvatar.BindSources(headInput, left.transform, right.transform);
            PhotonNetwork.NetworkingClient.EventReceived += e =>
            {
                if (e.Code == PhotonHeldItems.DropEvent && e.Sender == 1 && e.CustomData is object[] data && (int)data[1] == 2)
                    masterDrops++;
            };
        }
        yield return Until("target avatar", () => (targetAvatar = PhotonPlayer.ForActor(2)) != null);
        monitor = true;
        if (role == 1)
        {
            PhotonNetwork.NetworkingClient.EventReceived -= handoff.OnEvent;
            PhotonNetwork.NetworkingClient.EventReceived += FilterHolderEvents;
        }
        GrabBoth();
        foreach (var step in KeepBoth("baseline", 5)) yield return step;
        if (role == 2) { left.Release(); right.Release(); }
        yield return Until("baseline release agreed", ReleasedBoth);
        yield return Barrier("baseline-released");
        if (role == 2)
        {
            bat.GetComponent<PhotonView>().TransferOwnership(0);
            light.GetComponent<PhotonView>().TransferOwnership(0);
        }
        yield return Until("both room owned", RoomOwnsBoth);
        if (role == 1)
        {
            skipHeartbeat = skipReplies = true;
            yield return Until("expired heartbeat", () => heartbeatsSkipped >= 6 && handoff.IsActorUnavailable(2));
        }
        yield return Barrier("stale-before-grab");
        GrabBoth();
        foreach (var step in KeepBoth("stale-moving", 14)) yield return step;
        if (role == 2) Check(masterDrops == 0, "healthy moving player was force-dropped");
        if (role == 1) skipReplies = false;
        stationary = true;
        Streams(false);
        // Readiness for the master job also depends on a local avatar. Suppress this
        // lookup while stopping pose/hold streams so only the active-holder reply works.
        if (role == 2) PhotonPlayer.Active.Remove(targetAvatar);
        foreach (var step in KeepBoth("stationary-replies-only", 18)) yield return step;
        if (role == 1)
        {
            Check(replies >= 2, "stationary holder did not answer repeated checks");
            Debug.Log($"HOLDER EVIDENCE ignoredHeartbeats={heartbeatsSkipped} activeReplies={replies}");
            skipReplies = true;
            int before = repliesSkipped;
            yield return Until("unanswered check outstanding", () => repliesSkipped > before);
        }
        int previousBat = bat.HoldVersion, previousLight = light.HoldVersion;
        yield return Barrier("wake-during-check");
        if (role == 2)
        {
            PhotonPlayer.Active.Add(targetAvatar);
            Streams(true);
            left.Release(); right.Release();
            Grab(left, bat); Grab(right, light);
        }
        stationary = false;
        yield return Until("new grips visible", () => bat.HoldVersion > previousBat && light.HoldVersion > previousLight && HoldingBoth());
        foreach (var step in KeepBoth("fresh-regrab-during-check", 12)) yield return step;
        if (role == 2) Check(masterDrops == 0, "old check revoked a newer grip");

        // Model a frozen game loop while Photon transport stays connected. The test
        // still services the network so it can observe both real master-issued drops.
        int prior = repliesSkipped;
        Streams(false);
        yield return Until("silent holder drops", RoomOwnsBoth);
        if (role == 1) Check(repliesSkipped >= prior + 2, "silent release without initial query and retry");
        if (role == 2) Check(masterDrops == 2, "expected one master release for each silent hold");
        yield return Barrier("silent-recovered");
        Debug.Log($"HOLDER PASS silent actor={role} master=1 holders=0,0");
        if (role == 1) skipHeartbeat = skipReplies = false;
        Streams(true);
        GrabBoth();
        foreach (var step in KeepBoth("active-again", 3)) yield return step;
        if (role == 2) typeof(PhotonMasterHandoff).GetMethod("OnApplicationPause", Fields).Invoke(handoff, new object[] { true });
        yield return Until("explicit pause released both", RoomOwnsBoth);
        yield return Barrier("pause-released");
        Debug.Log($"HOLDER PASS explicit-standby actor={role}");
        if (role == 2)
        {
            typeof(PhotonMasterHandoff).GetMethod("OnApplicationPause", Fields).Invoke(handoff, new object[] { false });
            GrabBoth();
        }
        foreach (var step in KeepBoth("resumed", 5)) yield return step;
        monitor = false;
        yield return Barrier("disconnect-permission");
        if (role == 2) PhotonNetwork.Disconnect();
        else yield return Until("confirmed disconnect released both", () => originalRoom.PlayerCount == 2 &&
            originalRoom.MasterClientId == 1 && !originalRoom.Players.ContainsKey(2) && RoomOwnsBoth());
        Debug.Log($"HOLDER PASS final actor={role} heartbeat, probes, silence, wake, standby, disconnect");
        yield return Barrier("passed-holder-trial");
    }
}
