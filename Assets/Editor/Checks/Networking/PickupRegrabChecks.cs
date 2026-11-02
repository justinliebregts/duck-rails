using System;
using System.Collections.Generic;
using System.Reflection;
using ExitGames.Client.Photon;
using Photon.Pun;
using Photon.Realtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Hashtable = ExitGames.Client.Photon.Hashtable;

// Two independent peer caches for one logical item. Local fixture view IDs differ;
// retained property keys are translated on delivery. No live Photon connection.
public static class PickupRegrabChecks
{
    const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    static int checks;
    sealed class Actor : Player { public Actor(int id) : base("regrab", id, true) { } }
    sealed class RoomFixture : Room
    {
        public readonly List<(Hashtable values, Hashtable expected)> Sent = new();
        public RoomFixture() : base("regrab-test", new RoomOptions { DeleteNullProperties = true }) { }
        public override bool SetCustomProperties(Hashtable values, Hashtable expected = null, WebFlags flags = null)
        { Sent.Add((values, expected)); return true; }
    }
    sealed class Peer
    {
        public readonly int Actor, Id;
        public readonly RoomFixture Room;
        public readonly PhotonGrabbable Item;
        public readonly PhotonView View;
        public readonly PlayerHand Hand;
        public readonly RoomObjectRecovery Record;
        public string Key => "recovery:s:" + Id;
        public void Activate()
        {
            var client = PhotonNetwork.NetworkingClient;
            client.CurrentRoom = Room;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, Room.GetPlayer(Actor));
            client.State = ClientState.Joined; client.LoadBalancingPeer.DebugOut = DebugLevel.OFF;
        }
        public Peer(int actor, int initialOwner, object[] initial = null)
        {
            Actor = actor; Id = 900 + actor;
            Room = new RoomFixture { LoadBalancingClient = PhotonNetwork.NetworkingClient };
            Room.AddPlayer(new Actor(1)); Room.AddPlayer(new Actor(2)); Set(Room, "masterClientId", 1);
            Activate();
            if (initial != null) Cache(initial);
            Item = (PhotonGrabbable)PhotonGrabbableChecks.Create(typeof(PhotonGrabbable), true);
            View = Item.GetComponent<PhotonView>(); View.ViewID = Id; View.OwnershipTransfer = OwnershipOption.Takeover;
            View.OwnerActorNr = initialOwner; View.ControllerActorNr = initialOwner;
            Hand = new GameObject("peer " + actor + " hand").AddComponent<PlayerHand>(); Hand.enabled = false;
            Call(Item, "EnsureRecovery"); Record = Get<RoomObjectRecovery>(Item, "recovery");
        }
        public void Cache(object[] data)
        {
            Room.CustomProperties[Key] = data;
            Room.CustomProperties[Key + ":v"] = Stamp((int)data[1], (int)data[2]);
        }
        public void Receive(object[] data) { Activate(); Cache(data); Record.Reconcile(); }
        public bool Flush()
        {
            Activate();
            // Isolate each peer's end-of-frame flush. Calling the global Flush
            // here would consume the other synthetic peer's dirty registry too.
            var dirty = (HashSet<RoomObjectRecovery>)typeof(RoomObjectRecovery)
                .GetField("dirty", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            if (!dirty.Remove(Record)) return false;
            int before = Room.Sent.Count; Call(Record, "Publish"); return Room.Sent.Count > before;
        }
        public object[] Send()
        {
            if (!Flush()) throw new Exception("Expected a changed checkpoint from actor " + Actor);
            return (object[])Room.Sent[Room.Sent.Count - 1].values[Key];
        }
        public object[] Packet()
        {
            Activate(); var stream = new PhotonStream(true, null); Set(stream, "writeData", new List<object>(20));
            Item.OnPhotonSerializeView(stream, default); return stream.ToArray();
        }
        public void ReceivePacket(object[] packet, int sender)
        {
            Activate(); Item.OnPhotonSerializeView(new PhotonStream(false, packet),
                new PhotonMessageInfo(Room.GetPlayer(sender), 0, View));
        }
        public void Transfer(int owner)
        {
            Activate(); var previous = View.Owner;
            View.OwnerActorNr = owner; View.ControllerActorNr = owner;
            Item.OnOwnershipTransfered(View, previous);
        }
        public void Dispose()
        { Activate(); Call(Item, "OnDisable"); Object.DestroyImmediate(Item.gameObject); Object.DestroyImmediate(Hand.gameObject); }
    }
    static FieldInfo Field(object o, string name)
    {
        for (Type t = o.GetType(); t != null; t = t.BaseType)
        { var f = t.GetField(name, Flags); if (f != null) return f; }
        throw new Exception(name);
    }
    static void Set(object o, string name, object value) => Field(o, name).SetValue(o, value);
    static T Get<T>(object o, string name) => (T)Field(o, name).GetValue(o);
    static void Call(object o, string name) => o.GetType().GetMethod(name, Flags).Invoke(o, null);
    static long Stamp(int revision, int writer) => ((long)revision << 32) | (uint)writer;
    static void Check(bool ok, string label) { checks++; if (!ok) throw new Exception(label); Debug.Log("PASS regrab: " + label); }
    static bool Commit(Peer peer, int index, ref long serverStamp)
    {
        var op = peer.Room.Sent[index];
        if (op.expected != null)
            foreach (var entry in op.expected) if (!Equals(entry.Value, serverStamp)) return false;
        var data = (object[])op.values[peer.Key]; serverStamp = Stamp((int)data[1], (int)data[2]); return true;
    }
    static void HeldBy(Peer peer, int holder)
    {
        peer.Activate();
        Check(peer.View.OwnerActorNr == holder && peer.View.ControllerActorNr == holder, "peer " + peer.Actor + " agrees on network owner");
        Check(peer.Item.HeldActor == holder, "peer " + peer.Actor + " renders the correct holder");
        Check(peer.Item.IsHeld == (peer.Actor == holder), "only the winner has a local hold");
        Check(peer.Hand.Held == (peer.Actor == holder ? peer.Item : null), "hand reference agrees with local hold");
        Check(!peer.Item.PickupPending, "ownership is resolved without waiting for timeout");
    }
    static void Scenario(int dropper, bool formerOwnerCheckpointWins)
    {
        Peer a = null, b = null;
        int winner = 3 - dropper;
        try
        {
            Debug.Log($"REGRAB CASE: dropper={dropper}, master=1, formerOwnerCheckpointWins={formerOwnerCheckpointWins}");
            long serverStamp = 0;
            a = new Peer(dropper, dropper); Check(a.Hand.ForceGrab(a.Item), "A initially holds the item");
            var initial = a.Send(); Check(Commit(a, 0, ref serverStamp), "initial held checkpoint commits"); a.Receive(initial);
            b = new Peer(winner, dropper, initial);
            a.Activate(); a.Hand.DetachHeld(); a.Item.Release(Vector3.zero, Vector3.zero);
            var drop = a.Send(); Check(Commit(a, 1, ref serverStamp), "A drop commits"); a.Receive(drop); b.Receive(drop);
            b.Activate(); Check(b.Hand.ForceGrab(b.Item), "B immediately predicts pickup");
            a.Activate(); Check(a.Hand.ForceGrab(a.Item), "A regrabs before B transfer is delivered");
            var aRegrab = a.Send(); // Issued while A still thinks it owns the item.
            a.Transfer(winner); b.Transfer(winner);
            var bRegrab = b.Send();
            Check((int)aRegrab[1] == (int)bRegrab[1], "held checkpoints collide at the same numeric revision");
            a.ReceivePacket(b.Packet(), winner);
            Check(a.Item.HeldActor == winner && !a.Item.IsHeld, "A observes B's held snapshot after losing ownership");
            Peer accepted = formerOwnerCheckpointWins ? a : b;
            Peer rejected = formerOwnerCheckpointWins ? b : a;
            var retained = formerOwnerCheckpointWins ? aRegrab : bRegrab;
            Check(Commit(accepted, accepted.Room.Sent.Count - 1, ref serverStamp), "selected competing checkpoint commits");
            a.Receive(retained); b.Receive(retained);
            Check(!Commit(rejected, rejected.Room.Sent.Count - 1, ref serverStamp), "other checkpoint loses server CAS");
            rejected.Activate(); RoomObjectRecovery.OperationResponse(new OperationResponse {
                OperationCode = OperationCode.SetProperties, ReturnCode = ErrorCode.InvalidOperation,
                DebugMessage = "Competing held checkpoint lost CAS" });
            // These assertions fail on the original reciprocal-ghost bug,
            // before any watchdog or corrective publication can conceal it.
            HeldBy(a, winner); HeldBy(b, winner);
            Check(!a.Flush(), "former owner cannot republish after losing ownership");
            bool repaired = b.Flush();
            Check(repaired == formerOwnerCheckpointWins, "only a stale winning checkpoint requires a corrective save");
            if (repaired)
            {
                int index = b.Room.Sent.Count - 1;
                Check(Commit(b, index, ref serverStamp), "current controller's corrective checkpoint commits");
                var repair = (object[])b.Room.Sent[index].values[b.Key]; a.Receive(repair); b.Receive(repair);
                a.ReceivePacket(b.Packet(), winner);
            }
            HeldBy(a, winner); HeldBy(b, winner);
            b.Activate(); b.Hand.DetachHeld(); b.Item.Release(Vector3.zero, Vector3.zero);
            var release = b.Send(); Check(Commit(b, b.Room.Sent.Count - 1, ref serverStamp), "winner's normal release commits");
            a.Receive(release); b.Receive(release); a.ReceivePacket(b.Packet(), winner);
            foreach (var peer in new[] { a, b })
            {
                peer.Activate();
                Check(!peer.Item.IsHeldByAnyPlayer && peer.Hand.Held == null, "peer " + peer.Actor + " sees the item loose with no ghost hand");
                Check(peer.View.OwnerActorNr == winner && peer.Item.HeldActor == 0, "release preserves agreed ownership without a holder");
            }
            a.Activate(); Check(a.Hand.ForceGrab(a.Item), "item can be picked up again after the contested grab and drop");
        }
        finally { b?.Dispose(); a?.Dispose(); }
    }
    public static int RunCases()
    {
        if (!Application.isBatchMode) throw new Exception("Disposable batch only");
        var client = PhotonNetwork.NetworkingClient;
        var room = client.CurrentRoom; var local = client.LocalPlayer; var state = client.State; var debug = client.LoadBalancingPeer.DebugOut;
        checks = 0;
        try
        {
            foreach (int dropper in new[] { 1, 2 })
                foreach (bool formerWins in new[] { true, false }) Scenario(dropper, formerWins);
            Debug.Log("PickupRegrabChecks ALL PASSED (4 cases, " + checks + " assertions)");
            return checks;
        }
        finally
        {
            client.CurrentRoom = room;
            typeof(LoadBalancingClient).GetProperty("LocalPlayer").SetValue(client, local);
            client.State = state; client.LoadBalancingPeer.DebugOut = debug;
        }
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new Exception("Disposable batch only");
        try
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RunCases(); EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
