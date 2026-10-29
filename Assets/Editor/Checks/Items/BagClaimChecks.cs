using System;
using System.Reflection;
using Photon.Pun;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Offline lifecycle regression checks; network ordering still needs a two-client play test.
public static class BagClaimChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(BagObject bag, string field, object value) =>
        typeof(BagObject).GetField(field, Private).SetValue(bag, value);
    static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        GameObject go = null, inventoryObject = null, playerObject = null;
        try
        {
            go = new GameObject("Claim fixture");
            go.SetActive(false);
            var bag = go.AddComponent<BagObject>();
            var rb = go.GetComponent<Rigidbody>();
            Set(bag, "rb", rb);
            Set(bag, "requester", BagObject.LocalActor);
            Set(bag, "requestLife", BagInventory.Life);
            Set(bag, "awaitingClaim", true);
            Set(bag, "spawnTime", Time.unscaledTime);
            var tick = (Action)Delegate.CreateDelegate(typeof(Action), bag,
                typeof(BagObject).GetMethod("UpdateOwned", Private));
            typeof(BagObject).GetMethod("ApplyPhysics", Private).Invoke(bag, null);
            tick();
            Check(bag.AwaitingClaim && rb.isKinematic, "Missing inventory must wait safely");

            Set(bag, "spawnTime", Time.unscaledTime - 11f);
            tick();
            Check(!bag.AwaitingClaim && !rb.isKinematic && rb.useGravity,
                "Requester's own unclaimed bag must recover to physics");

            // A late old-controller packet must not restore the claim on an authority.
            bag.OnPhotonSerializeView(new PhotonStream(false, new object[] {
                (byte)BagState.Dropped, 0, 0, Vector3.up, Quaternion.identity, 0d, true
            }), default);
            Check(!bag.AwaitingClaim && !rb.isKinematic, "Stale snapshot resurrected claim");

            Set(bag, "awaitingClaim", true);
            bag.CancelIncomingClaim();
            Check(!bag.AwaitingClaim && !rb.isKinematic, "Death cancellation must drop controlled incoming bag");

            // Simulate inventory returning after the one-shot Start opportunity was missed.
            inventoryObject = new GameObject("Inventory fixture");
            inventoryObject.SetActive(false);
            var inventory = inventoryObject.AddComponent<BagInventory>();
            typeof(BagInventory).GetProperty("Local").SetValue(null, inventory);
            Set(bag, "awaitingClaim", true);
            Set(bag, "claimCancelled", false);
            Set(bag, "spawnTime", Time.unscaledTime);
            Set(bag, "requestedTarget", BagObject.Target.Dropped);
            tick();
            Check(!bag.AwaitingClaim && bag.State == BagState.Dropped, "Available inventory must complete delivery");
            inventory.Receive(bag, BagObject.Target.Back);
            Check(bag.State == BagState.Dropped, "Repeated delivery must be ignored");

            var reconcile = (Action)Delegate.CreateDelegate(typeof(Action), bag,
                typeof(BagObject).GetMethod("ReconcileOwnership", Private));
            Set(bag, "grantedTo", 42);
            Check(!bag.CanInteract && !bag.RequestControl(), "Granted bag was reclaimed before handoff");
            // Time cannot release a reservation: repeated ticks and an unrelated
            // self-transfer callback must keep it until the grant resolves.
            for (int i = 0; i < 10000; i++) tick();
            bag.OnOwnershipTransfered(bag.photonView, null);
            Check(!bag.CanInteract, "Self-transfer echo unlocked an outgoing handoff");
            bag.StowLocal(inventoryObject.transform, false);
            Check(bag.State == BagState.Dropped, "Granted bag was stowed during handoff");
            var recipient = (Photon.Realtime.Player)Activator.CreateInstance(typeof(Photon.Realtime.Player),
                BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "recipient", 42, false }, null);
            bag.OnOwnershipTransferFailed(null, recipient);
            Check(!bag.CanInteract, "Unrelated view failure unlocked handoff");
            bag.OnOwnershipTransferFailed(bag.photonView, recipient);
            Check(bag.CanInteract && bag.RequestControl(), "Uncontested pickup must remain immediate");
            Set(bag, "grantedTo", 42);
            bag.OnPlayerLeftRoom(recipient);
            Check(bag.CanInteract, "Departed recipient left bag reserved");
            Set(bag, "grantedTo", 42);
            bag.photonView.OwnerActorNr = 42;
            bag.OnOwnershipTransfered(bag.photonView, null);
            Check(bag.CanInteract, "Completed handoff left reservation set");
            bag.photonView.OwnerActorNr = 0;

            // Exercise PUN's real Request-mode rejection branch, not its
            // unrelated Takeover failure callback. Only our matching grant clears.
            var view = bag.photonView;
            view.OwnershipTransfer = OwnershipOption.Request;
            view.ViewID = 990042;
            // PUN deliberately skips registry insertion outside Play Mode.
            var registry = typeof(PhotonNetwork).GetField("photonViewList", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
            registry.GetType().GetMethod("Add", new[] { typeof(int), typeof(PhotonView) })
                .Invoke(registry, new object[] { view.ViewID, view });
            typeof(PhotonView).GetProperty("Owner").SetValue(view, recipient);
            typeof(PhotonView).GetProperty("Controller").SetValue(view, recipient);
            bag.OnEnable();
            var previousRoom = PhotonNetwork.NetworkingClient.CurrentRoom;
            var room = new Photon.Realtime.Room("bag-transfer-check", new Photon.Realtime.RoomOptions());
            room.AddPlayer(recipient);
            PhotonNetwork.NetworkingClient.CurrentRoom = room;
            var receiveEvent = typeof(PhotonNetwork).GetMethod("OnEvent", BindingFlags.Static | BindingFlags.NonPublic);
            void Reject(int recipientActor)
            {
                var message = new ExitGames.Client.Photon.EventData { Code = 210 };
                message.Parameters[Photon.Realtime.ParameterCode.ActorNr] = PhotonNetwork.LocalPlayer.ActorNumber;
                message.Parameters[Photon.Realtime.ParameterCode.Data] = new int[] { view.ViewID, recipientActor };
                receiveEvent.Invoke(null, new object[] { message });
            }
            Set(bag, "grantedTo", 42);
            Reject(43);
            Check(!bag.CanInteract, "Unrelated rejection unlocked the grant");
            Reject(42);
            Check((int)typeof(BagObject).GetField("grantedTo", Private).GetValue(bag) == 0,
                "Actual Request-mode rejection left the grant reserved");
            // With Takeover permission, an absent recipient must still be
            // rejected without overwriting the live owner/controller.
            view.OwnershipTransfer = OwnershipOption.Takeover;
            int previousOwner = view.OwnerActorNr, previousController = view.ControllerActorNr;
            Reject(77);
            Check(view.OwnerActorNr == previousOwner && view.ControllerActorNr == previousController,
                "Late transfer assigned authority to a departed recipient");
            Reject(42);
            Check(view.OwnerActorNr == 42 && view.ControllerActorNr == 42,
                "Valid recipient transfer no longer succeeds");
            view.OwnershipTransfer = OwnershipOption.Request;
            PhotonNetwork.NetworkingClient.CurrentRoom = previousRoom;
            Set(bag, "grantedTo", 42);
            view.ControllerActorNr = 0;
            Check(bag.CanInteract, "Controller loss without ownership callback left grant reserved");
            bag.OnDisable();
            PhotonNetwork.LocalCleanPhotonView(view);
            view.ViewID = 0;
            view.OwnerActorNr = 0;

            // A new controller can inherit a remote worn snapshot with no anchor.
            Set(bag, "state", BagState.Stowed);
            Set(bag, "holderActor", 42);
            Set(bag, "localBackAnchor", null);
            bag.OnOwnershipTransfered(bag.photonView, null);
            Check(bag.State == BagState.Dropped && !rb.isKinematic && rb.useGravity,
                "Transferred worn snapshot must recover to a physical dropped bag");
            Check(bag.HolderActor == 0, "Recovery retained the previous wearer");

            // Duplicate/self transfers must not drop a valid local worn bag.
            bag.StowLocal(inventoryObject.transform, false);
            bag.OnOwnershipTransfered(bag.photonView, null);
            Check(bag.State == BagState.Stowed && rb.isKinematic, "Valid local attachment was dropped");

            Set(bag, "state", BagState.Held);
            Set(bag, "holdingHand", null);
            bag.OnOwnershipTransfered(bag.photonView, null);
            Check(bag.State == BagState.Dropped && !rb.isKinematic, "Transferred held snapshot stayed frozen");
            bag.StowLocal(inventoryObject.transform, false);

            // Death must find an owned worn bag even when inventory has no reference.
            var hand = inventoryObject.AddComponent<PlayerHand>();
            var kind = typeof(BagInventory).GetNestedType("PendingKind", BindingFlags.NonPublic);
            typeof(BagInventory).GetMethod("AddPending", Private).Invoke(inventory,
                new object[] { Enum.Parse(kind, "Hold"), bag, hand, null });
            bag.PullToward(hand);
            // Death must invalidate requests even with no enabled inventory.
            playerObject = new GameObject("Death without inventory");
            playerObject.SetActive(false);
            var player = playerObject.AddComponent<GR.Player>();
            typeof(BagInventory).GetProperty("Local").SetValue(null, null);
            int lifeBeforeDeath = BagInventory.Life;
            BagObject.Active.Add(bag);
            player.IsDead = true;
            Check(BagInventory.Life == unchecked(lifeBeforeDeath + 1), "Death without inventory did not invalidate requests");
            Check(bag.State == BagState.Dropped && !rb.isKinematic,
                "Death without inventory left the existing worn bag attached");
            player.IsDead = true;
            Check(BagInventory.Life == unchecked(lifeBeforeDeath + 1), "Repeated death assignment invalidated twice");
            player.IsDead = false;
            typeof(BagInventory).GetProperty("Local").SetValue(null, inventory);
            inventory.DropAllOnDeath();
            Check(BagInventory.Life == unchecked(lifeBeforeDeath + 1), "Inventory cleanup invalidated the same death twice");
            BagObject.Active.Remove(bag);
            Check(bag.State == BagState.Dropped && !rb.isKinematic, "Death missed an untracked worn bag");
            // A pending pull also needs clearing when the bag was never ours to drop.
            typeof(BagInventory).GetMethod("AddPending", Private).Invoke(inventory,
                new object[] { Enum.Parse(kind, "Hold"), bag, hand, null });
            bag.PullToward(hand);
            inventory.DropAllOnDeath();
            Check(typeof(BagObject).GetField("pullHand", Private).GetValue(bag) == null,
                "Death left an incoming bag pulling toward the respawned hand");

            // A disabled inventory is still part of the rig: clear its references
            // and pending actions as well as dropping the physical held bag.
            inventoryObject.transform.SetParent(playerObject.transform);
            typeof(BagInventory).GetProperty("Local").SetValue(null, null);
            Set(bag, "state", BagState.Held);
            Set(bag, "holderActor", BagObject.LocalActor);
            typeof(BagInventory).GetField("leftBag", Private).SetValue(inventory, bag);
            typeof(BagInventory).GetMethod("AddPending", Private).Invoke(inventory,
                new object[] { Enum.Parse(kind, "Hold"), bag, hand, null });
            BagObject.Active.Add(bag);
            player.IsDead = true;
            BagObject.Active.Remove(bag);
            Check(bag.State == BagState.Dropped &&
                typeof(BagInventory).GetField("leftBag", Private).GetValue(inventory) == null,
                "Disabled inventory retained its held bag after death");
            var pendingAfterDeath = (System.Collections.ICollection)typeof(BagInventory).GetField("pending", Private).GetValue(inventory);
            Check(pendingAfterDeath.Count == 0, "Disabled inventory retained pending actions after death");
            player.IsDead = false;
            inventoryObject.transform.SetParent(null);
            typeof(BagInventory).GetProperty("Local").SetValue(null, inventory);

            // The request existed before death, but the bag did not exist during
            // cleanup. Delivery after respawn must drop rather than equip it.
            typeof(BagInventory).GetField("chest", Private).SetValue(inventory, inventoryObject.transform);
            Set(bag, "awaitingClaim", true);
            Set(bag, "claimCancelled", false);
            inventory.Receive(bag, BagObject.Target.Back);
            Check(bag.State == BagState.Dropped && inventory.BackBag == null,
                "A previous life's delayed delivery was equipped after respawn");
            Set(bag, "awaitingClaim", true);
            Set(bag, "spawnTime", Time.unscaledTime);
            tick();
            Check(!bag.AwaitingClaim && bag.State == BagState.Dropped,
                "Owned delivery retry accepted a previous life's request");
            var data = BagObject.SpawnData(1, null, BagObject.LocalActor, BagObject.Target.Back, BagInventory.Life);
            Check((int)data[4] == BagInventory.Life, "Instantiation data lost the request life");
            Set(bag, "requestLife", (int)data[4]);
            Set(bag, "awaitingClaim", true);
            inventory.Receive(bag, BagObject.Target.Back);
            Check(bag.State == BagState.Stowed && inventory.BackBag == bag,
                "Current-life delivery no longer equips immediately");
            inventory.DropAllOnDeath();
            bag.DropLocal(Vector3.zero);
            Set(bag, "requestLife", BagInventory.Life);

            Set(bag, "awaitingClaim", true);
            Set(bag, "claimCancelled", false);
            reconcile();
            Check(bag.AwaitingClaim, "Ownership reconciliation interrupted spawn delivery");
            Set(bag, "claimCancelled", true);
            reconcile();
            Check(!bag.AwaitingClaim && bag.State == BagState.Dropped, "Late ownership ignored death cancellation");

            for (int i = 0; i < 100; i++) reconcile();
            long ownershipBefore = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) reconcile();
            Check(GC.GetAllocatedBytesForCurrentThread() == ownershipBefore, "Ownership reconciliation allocated");

            // Exercise only the steady-state delegate: reflection and fixture work stay outside measurement.
            for (int i = 0; i < 100; i++) tick();
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) tick();
            Check(GC.GetAllocatedBytesForCurrentThread() == before, "Resolved bag update allocated");
            typeof(BagInventory).GetProperty("Local").SetValue(null, null);
            Set(bag, "awaitingClaim", true);
            Set(bag, "claimCancelled", false);
            Set(bag, "spawnTime", Time.unscaledTime);
            for (int i = 0; i < 100; i++) tick();
            before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 10000; i++) tick();
            Check(GC.GetAllocatedBytesForCurrentThread() == before, "Waiting claim update allocated");
            Check(bag.AwaitingClaim, "Waiting claim unexpectedly resolved");
            Debug.Log("BagClaimChecks: ALL PASSED (including allocation-free resolved and waiting updates)");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        finally
        {
            if (go != null) BagObject.Active.Remove(go.GetComponent<BagObject>());
            typeof(BagInventory).GetProperty("Local").SetValue(null, null);
            if (go != null) Object.DestroyImmediate(go);
            if (inventoryObject != null) Object.DestroyImmediate(inventoryObject);
            if (playerObject != null) Object.DestroyImmediate(playerObject);
        }
    }
}
