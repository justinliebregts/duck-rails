using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

/// <summary>
/// Items held in the local player's hands when they change room or world scene (Compound to Expedition,
/// crew return, Back to Compound, private departure/return) come with them. The held originals are removed
/// from the old world, then fresh copies drop at the player's feet once the destination world is ready.
/// Hands are never re-gripped: the player may have let go during the transition.
/// Worn bags and worn head equipment keep their own restore paths; only hand-held items and bags are carried.
/// </summary>
public static class RailHeldCarry
{
    const string SpawnTag = "rails-carry-v1";
    const float SettleSeconds = .75f;

    struct Carried
    {
        public string key;      // Grabbable prefab key; null for a bag.
        public byte bagId;
        public string[] keys;   // Bag slot contents.
    }

    static readonly List<Carried> pending = new List<Carried>();
    static double readySince = -1d;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { pending.Clear(); readySince = -1d; }

    static void Trace(string message) => RailDiagnostics.Instance?.Event("carry: " + message);

    /// <summary>
    /// Records and removes the local player's held items. Call before the hands are dropped for a room
    /// change, and before a same-room world scene swap. Additive: a crew return passes through several rooms.
    /// </summary>
    public static void CaptureLocalHands()
    {
        if (!PhotonNetwork.InRoom || !PhotonSession.IsRailsRoom) return;
        // Downing/death already dropped everything; never carry from a dead or downed player.
        if (RailExpedition.LocalDown || (GR.Player.instance != null && GR.Player.instance.IsDead)) return;

        var originals = new List<GameObject>(4);
        foreach (PhotonGrabbable item in PhotonGrabbable.HeldItems)
        {
            if (item == null || !item.IsHeld || item.PickupPending || item.IsStashed) continue;
            PhotonView view = item.GetComponent<PhotonView>();
            if (!Removable(view) || originals.Contains(item.gameObject)) continue;
            string key = PrefabKey(item.name);
            // Same resolution rule as bag slots: only items that can be spawned again are carried.
            if (BagObject.ResolvePrefab(key) == null) continue;
            pending.Add(new Carried { key = key });
            originals.Add(item.gameObject);
            Trace("captured " + key + " view " + view.ViewID);
        }
        foreach (BagObject bag in BagObject.Active)
        {
            if (bag == null || bag.State != BagState.Held || !bag.HasLocalHand || bag.AwaitingClaim) continue;
            if (!Removable(bag.photonView) || originals.Contains(bag.gameObject)) continue;
            string[] keys = bag.Keys();
            pending.Add(new Carried { bagId = bag.BagId, keys = keys });
            originals.Add(bag.gameObject);
            Trace("captured bag " + bag.BagId + " view " + bag.photonView.ViewID + " [" + string.Join(",", keys) + "]");
        }
        // Destroy, not drop: a drop returns the object to the room, after which only the master could
        // remove it, leaving a duplicate behind in the old world. Bag stashing despawns held items the same way.
        foreach (GameObject original in originals)
            if (original != null) PhotonNetwork.Destroy(original);
        if (originals.Count > 0) readySince = -1d;
    }

    static bool Removable(PhotonView view) =>
        view != null && view.ViewID != 0 && view.IsMine && PhotonView.Find(view.ViewID) == view;

    /// <summary>Drops pending carried items once the destination world is ready. Called every frame.</summary>
    public static void Tick(PhotonSession session)
    {
        if (pending.Count == 0) return;
        GR.Player player = GR.Player.instance;
        if (session == null || !PhotonNetwork.InRoom || !PhotonSession.IsRailsRoom || !session.WorldReady ||
            session.InWorldTransition || session.AwaitingCrewDestination || player == null || player.IsDead ||
            RailExpedition.LocalDown)
        {
            readySince = -1d;
            return;
        }
        double now = Time.realtimeSinceStartupAsDouble;
        // Let the arrival warp and rig settle before choosing a drop point.
        if (readySince < 0d) { readySince = now; return; }
        if (now - readySince < SettleSeconds) return;

        FeetPose(player, out Vector3 origin, out Vector3 forward);
        Vector3 right = Vector3.Cross(Vector3.up, forward);
        Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);
        for (int i = 0; i < pending.Count; i++)
        {
            float side = pending.Count == 1 ? 0f : (i - (pending.Count - 1) * .5f) * .3f;
            Vector3 position = ClearPoint(origin, forward * .35f + right * side);
            Carried carried = pending[i];
            if (carried.key == null)
            {
                // Bags are master-spawned room objects; the compound purchase tag keeps them out of
                // Compound leftover cleanup (no-op aboard, where expedition cargo rules apply).
                RoomRpc.SpawnBag(carried.bagId, carried.keys, position, rotation, BagObject.Target.Dropped, storePurchase: true);
                Trace("dropped bag " + carried.bagId + " [" + string.Join(",", carried.keys ?? new string[0]) + "]");
                continue;
            }
            GameObject prefab = BagObject.ResolvePrefab(carried.key);
            if (prefab == null) { Trace("lost " + carried.key + ": prefab unresolved"); continue; }
            PhotonPreparePool.Register(prefab);
            object[] data = RailCargo.WithCompoundPurchase(new object[] { SpawnTag }, PhotonNetwork.LocalPlayer.UserId);
            GameObject go = PhotonNetwork.InstantiateWithRoomLifetime(prefab.name, position, rotation * prefab.transform.rotation, 0, data);
            Trace((go != null ? "dropped " : "failed to drop ") + carried.key + (go != null ? " view " + go.GetComponent<PhotonView>().ViewID : ""));
        }
        DuckzLog.Log($"[Carry] Dropped {pending.Count} carried item(s) at the player's feet");
        pending.Clear();
        readySince = -1d;
    }

    // Waist height in front of the duck's body; physics settles it onto the floor or train deck.
    static void FeetPose(GR.Player player, out Vector3 origin, out Vector3 forward)
    {
        NewPlayer rig = NewPlayer.instance;
        origin = rig != null && rig.bodyCollider != null ? rig.bodyCollider.bounds.center : player.transform.position + Vector3.up * .3f;
        Transform head = rig != null && rig.headCollider != null ? rig.headCollider.transform
            : Camera.main != null ? Camera.main.transform : player.transform;
        forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);
        if (forward.sqrMagnitude < 1e-4f) forward = Vector3.ProjectOnPlane(player.transform.forward, Vector3.up);
        forward = forward.sqrMagnitude < 1e-4f ? Vector3.forward : forward.normalized;
    }

    // Keep the drop on the player's side of any wall, counter or train panel.
    static Vector3 ClearPoint(Vector3 origin, Vector3 offset)
    {
        float distance = offset.magnitude;
        if (distance < 1e-3f) return origin;
        Vector3 direction = offset / distance;
        // Walkable/solid world layers only, so the duck's own hands and body never shorten the drop.
        NewPlayer rig = NewPlayer.instance;
        int mask = rig != null && rig.locomotionEnabledLayers.value != 0 ? rig.locomotionEnabledLayers.value : Physics.DefaultRaycastLayers;
        if (Physics.SphereCast(origin, .08f, direction, out RaycastHit hit, distance, mask, QueryTriggerInteraction.Ignore))
            return origin + direction * Mathf.Max(0f, hit.distance - .05f);
        return origin + offset;
    }

    // "ItemGoldBar(Clone)" and scene copies like "ItemHelmet (2)" both map to the prefab name (as bag slots do).
    static string PrefabKey(string name)
    {
        int cut = name.IndexOf('(');
        return (cut > 0 ? name.Substring(0, cut) : name).Trim();
    }
}
