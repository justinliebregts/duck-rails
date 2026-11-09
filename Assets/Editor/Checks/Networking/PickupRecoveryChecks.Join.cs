using System;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class PickupRecoveryChecks
{
    const BindingFlags StaticPrivate = BindingFlags.Static | BindingFlags.NonPublic;
    static void OwnershipEvent(byte code, int sender, object payload)
    {
        var message = new EventData { Code = code };
        message.Parameters[ParameterCode.ActorNr] = sender;
        message.Parameters[ParameterCode.Data] = payload;
        typeof(PhotonNetwork).GetMethod("OnEvent", StaticPrivate).Invoke(null, new object[] { message });
    }

    static void Bootstrap(PhotonView view, int sender, int owner, string lifetime) =>
        OwnershipEvent(212, sender, new object[] { new[] { view.ViewID, owner }, new[] { lifetime } });

    static void TestJoinOwnership()
    {
        foreach (int local in new[] { 1, 2 })
        {
            var room = Setup(local);
            int other = local == 1 ? 2 : 1;
            var item = Seed(room, other, 990 + local);
            var view = item.GetComponent<PhotonView>();
            var hand = new GameObject("late join pickup hand").AddComponent<PlayerHand>();
            hand.enabled = false;
            try
            {
                // Edit-mode PUN skips registration. Deliver through the same event handler as a real client.
                var views = typeof(PhotonNetwork).GetField("photonViewList", StaticPrivate).GetValue(null);
                views.GetType().GetMethod("Add", new[] { typeof(int), typeof(PhotonView) }).Invoke(views, new object[] { view.ViewID, view });
                Call(item, "OnEnable");
                Bootstrap(view, 3, 0, "");
                Bootstrap(view, 1, 0, "retired-spawn");
                Bootstrap(view, 1, 14, "");
                OwnershipEvent(212, 1, new object[] { new[] { view.ViewID }, Array.Empty<string>() });
                OwnershipEvent(212, 1, new[] { view.ViewID, 0 });
                Check(view.OwnerActorNr == other && !view.HasLiveOwnership, "bootstrap requires current master, valid shape, matching spawn and room member");
                Bootstrap(view, 1, 0, "");
                Check(view.OwnerActorNr == 0 && view.ControllerActorNr == 1, "first valid join list initializes room ownership");
                item.Grab(hand);
                OwnershipEvent(210, local, new[] { view.ViewID, local });
                item.transform.position = Vector3.up * 3;
                RoomObjectRecovery.Flush();
                Check(room.Deliver(0) && item.IsHeld && view.IsMine, "live transfer and acknowledgement establish a confirmed pickup");
                Bootstrap(view, 1, other, "");
                Bootstrap(view, 3, other, "");
                Check(view.IsMine && view.OwnerActorNr == local && item.Holder == hand && item.transform.position == Vector3.up * 3,
                    "late lists keep the confirmed hand and pose for actor " + local);
                OwnershipEvent(210, other, new[] { view.ViewID, other });
                Check(!view.IsMine && !item.IsHeld && view.OwnerActorNr == other, "legitimate competing transfer still releases previous holder");
                PhotonHeldItems.ReturnToRoom(view);
                Bootstrap(view, 1, other, "");
                Check(view.OwnerActorNr == 0 && view.ControllerActorNr == 1, "late list preserves explicit return to room");
            }
            finally
            {
                PhotonNetwork.LocalCleanPhotonView(view);
                Destroy(item);
                Object.DestroyImmediate(hand.gameObject);
            }
        }
    }

    static void TestObserverRepair()
    {
        foreach (bool checkpointFirst in new[] { true, false })
        {
            var room = Setup(3);
            var item = Seed(room, 2, 996);
            var view = item.GetComponent<PhotonView>();
            var record = Get<RoomObjectRecovery>(item, "recovery");
            var host = new GameObject("repair check").AddComponent<PhotonMasterHandoff>();
            try
            {
                Call(host, "Awake");
                Set(host, "room", room);
                string key = "recovery:s:" + view.ViewID;
                int request = PhotonNetwork.ServerTimestamp;
                var losses = Get<Array>(host, "localLosses");
                object loss = losses.GetValue(0);
                Set(loss, "Key", key); Set(loss, "Attempts", 1); Set(loss, "Request", request);
                Set(loss, "Revision", record.Revision); Set(loss, "Generation", view.OwnershipGeneration);
                losses.SetValue(loss, 0);
                // Locally actor 2 is still owner. The master and a newer checkpoint agree that the room owns it.
                var data = (object[])((object[])room.CustomProperties[key]).Clone();
                data[1] = 2; data[2] = 1; data[3] = 0;
                void ReceiveCheckpoint() { room.CustomProperties[key] = data; record.Reconcile(); }
                void Confirm(int nonce) => Call(host, "ConfirmPickupAuthority", record, nonce, 0, 1);
                // A higher revision already in flight before the request is insufficient, even from the right writer.
                Confirm(request - 1);
                ReceiveCheckpoint();
                Check(view.OwnerActorNr == 2, "delayed checkpoint or unmatched confirmation cannot repair an observer");
                if (!checkpointFirst) Confirm(request);
                Check(view.OwnerActorNr == 2, "untagged controller checkpoint cannot impersonate a refresh");
                Array.Resize(ref data, 7);
                data[1] = 3; data[6] = Stamp(1, request);
                if (checkpointFirst) ReceiveCheckpoint();
                Confirm(request);
                if (!checkpointFirst)
                {
                    Check(view.OwnerActorNr == 2, "confirmation waits for a newer controller checkpoint");
                    ReceiveCheckpoint();
                }
                Check(view.OwnerActorNr == 0 && view.ControllerActorNr == 1, "targeted repair works in both delivery orders");
                view.OwnerActorNr = 2; view.ControllerActorNr = 2; view.MarkLiveOwnership();
                data[1] = 4;
                Confirm(request); ReceiveCheckpoint();
                Check(view.OwnerActorNr == 2, "intervening live transfer invalidates the old repair");
            }
            finally { Object.DestroyImmediate(host.gameObject); Destroy(item); }
        }
    }

    static void TestPickupHandoffLimits()
    {
        var room = Setup(1);
        for (int id = 2; id <= 3; id++)
        {
            room.Players.Remove(id);
            room.AddPlayer(new Actor(id, false));
        }
        var first = Seed(room, 1, 987);
        var second = Seed(room, 1, 988);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        string previousName = scene.name;
        scene.name = PhotonSession.RequiredScene;
        var avatar = new GameObject("available master");
        avatar.SetActive(false);
        avatar.AddComponent<PhotonView>();
        var human = avatar.AddComponent<PhotonPlayer>();
        human.photonView.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        human.photonView.ControllerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        typeof(PhotonView).GetProperty("IsMine").SetValue(human.photonView, true);
        PhotonPlayer.Active.Add(human);
        var host = new GameObject("pickup handoff limits").AddComponent<PhotonMasterHandoff>();
        try
        {
            Call(host, "Awake");
            Set(host, "focused", true);
            Set(host, "room", room);
            Call(host, "ResetObservation");
            Check((bool)typeof(PhotonMasterHandoff).GetProperty("Ready", Flags).GetValue(host),
                "healthy master is ready throughout pickup-only handoff checks");
            var heartbeat = new EventData { Code = 105 };
            heartbeat.Parameters[ParameterCode.ActorNr] = 3;
            heartbeat.Parameters[ParameterCode.Data] = new object[] { PhotonNetwork.ServerTimestamp, true };
            host.OnEvent(heartbeat);
            Check(Equals(typeof(PhotonMasterHandoff).GetMethod("SelectCandidate", Flags).Invoke(host, null), room.GetPlayer(3)),
                "handoff boundary checks have an eligible backup");

            int stamp = PhotonNetwork.ServerTimestamp;
            const string a = "recovery:s:987", b = "recovery:s:988";
            void Report(int actor, string key, int seq, double at) =>
                Call(host, "HandlePickupReport", actor, key, unchecked(stamp + seq), at);
            void Reset() => Call(host, "ClearPickupReports");
            bool Attempted() => Get<double>(host, "retryAt") != 0;
            int Recurrences()
            {
                int count = 0;
                foreach (object repair in Get<Array>(host, "repairs"))
                    if (Get<bool>(repair, "Recurred")) count++;
                return count;
            }

            Report(2, a, 1, 0); Report(2, b, 1, 0);
            Report(2, a, 2, 5); Report(2, b, 2, 5);
            Check(Recurrences() == 2 && !Attempted(), "one reporter on multiple objects cannot switch master");
            Reset();
            Report(2, a, 1, 0); Report(3, a, 1, 0);
            Report(2, a, 2, 5); Report(3, a, 2, 5);
            Check(Recurrences() == 2 && !Attempted(), "two reporters on one object cannot switch master");
            Reset();
            Report(2, a, 3, 0); Report(3, b, 3, 0);
            Report(2, a, 4, 3.99); Report(3, b, 4, 3.99);
            Report(2, a, 3, 5); Report(3, b, 2, 5);
            Check(Recurrences() == 0 && !Attempted(), "early, duplicate and out-of-order reports cannot corroborate");
            Report(2, a, 4, 20); Report(3, b, 4, 20);
            Check(Recurrences() == 0 && !Attempted(), "reports at 20 seconds start a new observation window");

            double switched = Time.realtimeSinceStartupAsDouble;
            host.OnMasterClientSwitched(room.GetPlayer(1));
            double cutoff = Get<double>(host, "pickupHandoffAfter");
            Check(cutoff >= switched + 60 && cutoff <= Time.realtimeSinceStartupAsDouble + 60,
                "confirmed switch installs the 60-second pickup cooldown");
            void Corroborate(double end)
            {
                Report(2, a, 1, end - 5); Report(3, b, 1, end - 5);
                Report(2, a, 2, end); Report(3, b, 2, end);
            }
            Corroborate(cutoff - 0.01);
            Check(Recurrences() == 2 && !Attempted(), "fully corroborated failures during cooldown cannot switch master again");
            Reset();
            Corroborate(cutoff);
            Check(Recurrences() == 2 && Attempted(), "with identical evidence the master can hand off once cooldown expires");
        }
        finally
        {
            Object.DestroyImmediate(host.gameObject);
            PhotonPlayer.Active.Remove(human); Object.DestroyImmediate(avatar);
            scene.name = previousName;
            Destroy(first); Destroy(second);
        }
    }
}
