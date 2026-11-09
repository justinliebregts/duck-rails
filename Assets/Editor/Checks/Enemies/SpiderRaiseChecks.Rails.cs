using System;
using Game.AI;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static partial class SpiderRaiseChecks
{
    sealed class RaiseActor : Player { public RaiseActor(int id, bool local = false) : base("raise-" + id, id, local) { } }
    public static void RunRails()
    {
        Run();
        for (int flags = 0; flags < 256; flags++)
        for (byte level = 0; level < 8; level++)
        {
            var packet = new EnemyPosePacket { Flags = (byte)flags, SpiderRaise = level };
            var copy = (EnemyPosePacket)EnemyPosePacket.Deserialize(EnemyPosePacket.Serialize(packet));
            Check(copy.Flags == flags && copy.SpiderRaise == level, "Boarding/state flags and lift round-trip independently");
            copy.SpiderRaise = (byte)((level + 1) % 8);
            Check(!packet.Equals(copy), "Lift-only changes bypass UnreliableOnChange suppression");
            EnemyPosePacket.Return(copy);
        }
        var client = PhotonNetwork.NetworkingClient;
        var oldRoom = client.CurrentRoom; var oldLocal = client.LocalPlayer; var oldState = client.State;
        var train = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(RailEnemyBoardingAuthoring.TrainPath));
        train.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        Call(train.GetComponent<RailTrain>(), "OnEnable");
        Call(train.GetComponent<RailTrainNavigation>(), "Awake");
        try
        {
            var owner = new RaiseActor(1); var observer = new RaiseActor(2, true);
            var room = new Room("synthetic raised boarding", new RoomOptions());
            room.LoadBalancingClient = client; room.AddPlayer(owner); room.AddPlayer(observer);
            client.CurrentRoom = room; client.State = ClientState.Joined;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, observer);
            using var a = new Rig(); using var b = new Rig();
            a.actor.AddComponent<RailEnemyBoarding>(); b.actor.AddComponent<RailEnemyBoarding>();
            Call(a.actor.GetComponent<RailEnemyBoarding>(), "Awake"); Call(b.actor.GetComponent<RailEnemyBoarding>(), "Awake");
            Call(a.enemy, "Awake"); Call(b.enemy, "Awake");
            a.view.ControllerActorNr = b.view.ControllerActorNr = 1;
            typeof(PhotonView).GetProperty("IsMine").SetValue(a.view, true);
            a.enemy.Boarding.SetRemoteFrame(true, false);
            Check(a.enemy.Boarding.IsAboard, "Owner attached to actual train graph");
            a.enemy.SetRootPose(train.transform.TransformPoint(Vector3.zero), train.transform.rotation);
            a.enemy.SetRemoteState(Enemy.State.Chase); Set(a.enemy, "_target", 0);
            Target(a.enemy.Root.position + Vector3.up * 1.5f + a.enemy.Root.forward * .8f); a.Settle();
            var wire = (byte[])EnemyPosePacket.Serialize(a.Capture()).Clone();
            var sent = (EnemyPosePacket)EnemyPosePacket.Deserialize(wire);
            Check(sent.SpiderRaise > 0 && (sent.Flags & EnemyPosePacket.TrainFlag) != 0 && (sent.Flags & EnemyPosePacket.DisplacedFlag) != 0, "Lift and both boarding flags published together");
            b.Receive(sent, owner, 1000);
            Target(Vector3.down * 20);
            for (int i = 0; i < 90; i++) { b.enemy.ApplyRemote(1f / 90); b.Tick(1f / 90); }
            Check(b.gait.RaiseLevel == a.gait.RaiseLevel && b.enemy.Boarding.IsAboard, "Remote obeys owner lift while aboard");
            for (int i = 0; i < 16; i++)
            {
                train.transform.SetPositionAndRotation(new Vector3(i, 0, i * 2), Quaternion.Euler(0, i * 13, 0));
                b.enemy.ApplyRemote(1f);
                b.enemy.AimHead(1f);
                Check(Vector3.Distance(b.enemy.Root.position, train.transform.position) < .001f, "Train-relative pose follows motion");
                Check((bool)Get(b.enemy, "_remoteHeadAim"), "Train interpolation preserves local head aim");
            }
            var stale = new EnemyPosePacket(); stale.Set(Vector3.zero, Quaternion.identity, 0, false, 0, 0);
            b.Receive(stale, owner, 999); Check(b.gait.RaiseLevel > 0 && b.enemy.Boarding.IsAboard, "Stale packet cannot erase lift/boarding");
            b.Receive(new EnemyPosePacket(), observer, 2000); Check(b.gait.RaiseLevel > 0, "Wrong owner cannot erase lift");
            var lower = new EnemyPosePacket(); lower.Set(Vector3.zero, Quaternion.identity, (byte)Enemy.State.Chase, false, 0, 0); lower.Flags |= EnemyPosePacket.DisplacedFlag;
            b.Receive(lower, owner, 2001); Check(b.gait.RaiseLevel == 0 && !b.enemy.Boarding.IsAboard && b.enemy.Boarding.OwnsBoundary, "Lower and detached boundary state received together");
            b.sync.OnControllerChange(observer, owner); Check(b.gait.RaiseHeight == 0, "Handoff resets raised pose");
        }
        finally
        {
            Object.DestroyImmediate(train);
            client.CurrentRoom = oldRoom; client.State = oldState;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, oldLocal);
        }
        RailEnemyBoardingChecks.Run();
        RailTrackCrossingChecks.Run();
        Debug.Log($"SpiderRaiseChecks RAILS PASS: {checks} assertions; exhaustive lift/boarding codec, synthetic owner/observer, moving train aim, detach, stale/foreign rejection, handoff.");
    }
}
