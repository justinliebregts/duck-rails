using System.Collections.Generic;
using Game.Animation;
using Game.Audio;
using Photon.Pun;
using UnityEngine;

// The local player's hands on bags (BagObject). Players start with no bag; bags are bought from the store wall.
// Grip a dropped bag and it snaps into that hand. Other players' bags can only be taken once they drop them.
// Release a held bag behind your shoulder to wear it, if your back is free. Anywhere else it falls.
// Hold an item near an empty slot and a line points at that slot. Release to shrink the item into it.
// An empty hand near a filled slot grows that slot; grip pulls the item out full size.
// Only the bag on your back is saved (bag.back in UserPlayerPrefs) and comes back with you next session. Dying drops everything.
public class BagInventory : MonoBehaviour, IHandGripHandler
{
    public static BagInventory Local { get; private set; }
    // Survives rig replacement; only death invalidates requests from this life.
    public static int Life { get; private set; }
    public static int HandClaimEpoch { get; private set; }
    public static void InvalidateHandClaims() { unchecked { HandClaimEpoch++; } }
    public static void InvalidateLife() { unchecked { Life++; } }
    /// <summary>The networked bag prefab (a BagObject). Every client registers it so the master's room objects can spawn.</summary>
    public static GameObject BagPrefab { get; private set; }

    [Header("Rig")]
    [Tooltip("The networked bag prefab, with a PhotonView and BagObject.")]
    [SerializeField] GameObject bagPrefab;
    [Tooltip("Over-the-shoulder reach zone, authored directly under the body's chest. Its transform is not changed at runtime.")]
    [SerializeField] BagBackTrigger backZone;
    [Tooltip("Where a worn bag rests if there is no body to put it on.")]
    [SerializeField] Transform stowAnchor;

    [Header("Interaction")]
    [Tooltip("How close the hand holding an item must be to an empty slot to target it.")]
    [SerializeField] float hoverRadius = 0.15f;
    [Tooltip("How close an empty hand must be to a filled slot to target it.")]
    [SerializeField] float takeRadius = 0.05f;
    [Tooltip("How far from the bag mesh a grip still counts as a hand on the bag.")]
    [SerializeField] float pickupRadius = 0.02f;
    [Tooltip("How long to wait for another player to hand over their dropped bag.")]
    [SerializeField] float requestTimeout = 3f;

    [Header("Haptics")]
    [Tooltip("Buzz in the hand that starts targeting a slot.")]
    [SerializeField, Range(0f, 1f)] float hoverHapticAmplitude = 0.15f;
    [SerializeField, Range(0f, 1f)] float hoverHapticFrequency = 0.5f;
    [SerializeField] float hoverHapticDuration = 0.04f;

    const string SaveKey = "bag.back";
    const char SaveSeparator = '|';

    enum PendingKind { Hold, Take, Put }

    class Pending
    {
        public PendingKind kind;
        public BagObject bag;
        public PlayerHand hand;
        public Grabbable item;
        public float until;
    }

    readonly List<Pending> pending = new List<Pending>();
    BagObject backBag;
    BagObject leftBag;
    BagObject rightBag;
    Transform chest;
    bool restoreRequested;
    bool debugBagPending;
    byte debugBagPendingId;
    byte debugWornId;
    float debugBagPendingUntil;
    static bool loggedMissingDebugBag;

    LineRenderer leftLine;
    LineRenderer rightLine;
    BagSlot leftHover;
    BagSlot rightHover;

    public BagObject BackBag => backBag;

    void Awake()
    {
        if (bagPrefab != null)
        {
            BagPrefab = bagPrefab;
            PhotonPreparePool.Register(bagPrefab);
        }
        else
            {
                if (DuckzLog.AllowError(out var logNotice1))
                    logNotice1.Write("[Bag] BagInventory has no bag prefab; bags can't be spawned.", this);
            }
    }

    void OnEnable()
    {
        Local = this;
        PlayerHand.GripHandler = this;
        BagObject.OwnershipChanged += OnBagOwnershipChanged;
    }

    void OnDisable()
    {
        if (Local == this)
            Local = null;
        if (ReferenceEquals(PlayerHand.GripHandler, this))
            PlayerHand.GripHandler = null;
        BagObject.OwnershipChanged -= OnBagOwnershipChanged;
    }

    void Start()
    {
        RemotePlayerBody body = FindLocalBody();
        chest = body != null ? body.Chest : null;
        EnsureRig();
    }

    // ---------- Rig ----------

    void EnsureRig()
    {
        if (backZone != null && stowAnchor != null)
            return;

        Transform head = FindHead();
        if (stowAnchor == null)
        {
            var anchor = new GameObject("BagStowAnchor");
            stowAnchor = anchor.transform;
            stowAnchor.SetParent(head, false);
            stowAnchor.localPosition = new Vector3(0f, -0.32f, -0.18f);
        }
        if (backZone == null)
        {
            var zone = new GameObject("BagBackTrigger");
            zone.transform.SetParent(chest != null ? chest : head, false);
            if (chest != null)
                zone.transform.position = chest.position + chest.rotation * new Vector3(0f, 0.15f, -0.2f);
            else
                zone.transform.localPosition = new Vector3(0f, -0.15f, -0.28f);
            backZone = zone.AddComponent<BagBackTrigger>();
        }
    }

    RemotePlayerBody FindLocalBody()
    {
        GR.Player player = GetComponentInParent<GR.Player>();
        LocalBodyDriver driver = player != null ? player.GetComponentInChildren<LocalBodyDriver>(true) : null;
        if (driver == null && LocalBodyDriver.Active.Count > 0)
            driver = LocalBodyDriver.Active[0];
        return driver != null ? driver.Body : null;
    }

    Transform FindHead()
    {
        GR.Player player = GetComponentInParent<GR.Player>();
        Camera[] cameras = (player != null ? player.transform : transform).GetComponentsInChildren<Camera>(true);
        Camera fallback = null;
        foreach (Camera cam in cameras)
        {
            if (cam.transform.name == "CenterEyeAnchor")
                return cam.transform;
            if (cam.CompareTag("MainCamera") || fallback == null)
                fallback = cam;
        }
        return fallback != null ? fallback.transform : transform;
    }

    // ---------- Save ----------

    void SaveBack()
    {
        if (!UserPlayerPrefs.IsReady)
            return;
        if (backBag == null)
        {
            UserPlayerPrefs.DeleteKey(SaveKey);
            DuckzLog.Log("[Bag] Back is empty; nothing saved");
        }
        else
        {
            string[] keys = backBag.Keys();
            UserPlayerPrefs.SetString(SaveKey, backBag.BagId + SaveSeparator.ToString() + string.Join(SaveSeparator.ToString(), keys));
            UserPlayerPrefs.SetString(SaveKey + ".room", PhotonNetwork.CurrentRoom?.Name ?? "");
            UserPlayerPrefs.SetString(SaveKey + ".identity", backBag.RecoveryIdentity ?? "");
            DuckzLog.Log($"[Bag] Saved bag {backBag.BagId} on back: [{string.Join(", ", keys)}]");
        }
        UserPlayerPrefs.Save();
    }

    // Once per room: if a bag was on our back when we last left, ask for it back onto our back.
    void TryRestore()
    {
        if (!PhotonNetwork.InRoom)
        {
            restoreRequested = false;
            return;
        }
        if (restoreRequested || PhotonPlayer.Local == null || IsPlayerDead())
            return;
        if (!ResolveDebugBag(out byte debugId))
            return;
        // DebugBagEquip on GameManager is wearing a bag. That path also takes it off when set to None.
        if (debugId != 0)
        {
            restoreRequested = true;
            return;
        }
        if (!UserPlayerPrefs.IsReady)
            return;
        restoreRequested = true;
        foreach (var bag in BagObject.Active)
            if (bag != null && bag.State == BagState.Stowed && bag.HolderActor == BagObject.LocalActor)
                ReattachRecoveredBack(bag);
        if (backBag != null)
            return;

        string saved = UserPlayerPrefs.GetString(SaveKey, "");
        if (string.IsNullOrEmpty(saved))
            return;
        // The room copy may have been picked up by somebody else while we were away.
        // Never mint a second copy from an older disk save in that room.
        if (RoomObjectRecovery.Exists(UserPlayerPrefs.GetString(SaveKey + ".room", ""),
            UserPlayerPrefs.GetString(SaveKey + ".identity", ""))) return;
        string[] parts = saved.Split(SaveSeparator);
        if (!byte.TryParse(parts[0], out byte bagId) || bagId == 0)
        {
            {
                if (DuckzLog.AllowWarning(out var logNotice2))
                    logNotice2.Write($"[Bag] Can't read saved back bag '{saved}'; forgetting it");
            }
            UserPlayerPrefs.DeleteKey(SaveKey);
            UserPlayerPrefs.Save();
            return;
        }
        var keys = new string[parts.Length - 1];
        System.Array.Copy(parts, 1, keys, 0, keys.Length);
        DuckzLog.Log($"[Bag] Restoring saved bag {bagId} onto back: [{string.Join(", ", keys)}]");
        Transform anchor = chest != null ? chest : stowAnchor;
        RoomRpc.SpawnBag(bagId, keys, anchor.position, anchor.rotation, BagObject.Target.Back);
    }

    // GameManager's DebugBagEquip. None takes off a bag this put on. A chosen bag is worn immediately, including mid-play.
    void ApplyDebugBag()
    {
        if (debugBagPending && Time.time > debugBagPendingUntil)
            debugBagPending = false;
        if (!ResolveDebugBag(out byte want))
            return;
        if (want == 0)
        {
            debugBagPendingId = 0;
            if (debugWornId != 0 && backBag != null && backBag.BagId == debugWornId && !debugBagPending)
            {
                BagObject old = backBag;
                debugWornId = 0;
                SetBackBag(null, true);
                DestroyBag(old);
            }
            return;
        }
        if (debugBagPending)
        {
            debugBagPendingId = want;
            return;
        }
        if (backBag != null && backBag.BagId == want)
        {
            debugWornId = want;
            return;
        }
        if (!PhotonNetwork.InRoom || PhotonPlayer.Local == null || IsPlayerDead())
            return;

        if (backBag != null)
        {
            BagObject old = backBag;
            SetBackBag(null, true);
            DestroyBag(old);
        }

        Transform anchor = chest != null ? chest : stowAnchor;
        if (anchor == null)
            return;
        debugWornId = 0;
        debugBagPending = true;
        debugBagPendingId = want;
        debugBagPendingUntil = Time.time + 5f;
        DuckzLog.Log($"[Bag] Debug: equipping bag {want} on the back");
        RoomRpc.SpawnBag(want, null, anchor.position, anchor.rotation, BagObject.Target.Back);
    }

    static void DestroyBag(BagObject bag)
    {
        if (bag == null)
            return;
        PhotonView view = bag.photonView;
        if (PhotonNetwork.InRoom && view != null && view.ViewID != 0)
            PhotonNetwork.Destroy(bag.gameObject);
        else
            Destroy(bag.gameObject);
    }

    // ---------- Frame ----------

    void LateUpdate()
    {
        ForgetLostBags();

        GR.Player player = Player();
        if (player != null)
        {
            if (leftBag != null && !HandActive(player.leftPlayerHand))
                DropFromHand(player.leftPlayerHand, leftBag);
            if (rightBag != null && !HandActive(player.rightPlayerHand))
                DropFromHand(player.rightPlayerHand, rightBag);
        }

        ProcessPending();
        TryRestore();
        ApplyDebugBag();
        UpdateHover();
    }

    // False while the store catalog is not loaded yet. bagId 0 means None.
    static bool ResolveDebugBag(out byte bagId)
    {
        bagId = 0;
#if UNITY_EDITOR
        DebugBagEquip equip = DebugBagEquip.Current;
        string key = equip != null ? equip.CatalogKey : null;
        if (string.IsNullOrEmpty(key))
            return true;
        StoreCatalog catalog = GameConfig.Current != null ? GameConfig.Current.storeCatalog : null;
        if (catalog == null)
            return false;
        StoreItem item = catalog.Find(key);
        if (item == null || item.type != StoreItemType.Bag || item.bagId == 0)
        {
            if (!loggedMissingDebugBag)
            {
                loggedMissingDebugBag = true;
                {
                    if (DuckzLog.AllowWarning(out var logNotice3))
                        logNotice3.Write($"[Bag] Debug bag '{key}' is missing from the store catalog.");
                }
            }
            return true;
        }
        bagId = item.bagId;
        return true;
#else
        return true;
#endif
    }

    // Bags vanish when we leave the room; that is not the same as taking one off, so the save stays.
    void ForgetLostBags()
    {
        if (leftBag != null && (!leftBag.CanControl || leftBag.State != BagState.Held)) leftBag = null;
        if (rightBag != null && (!rightBag.CanControl || rightBag.State != BagState.Held)) rightBag = null;
        if (backBag != null && (backBag.State != BagState.Stowed || backBag.HolderActor != BagObject.LocalActor))
            SetBackBag(null, false);
    }

    static bool HandActive(PlayerHand hand) => hand != null && hand.isActiveAndEnabled;

    // ---------- Grip handling ----------

    // Keep equipped-helmet removal out of the bag's reach zones.
    public bool BlocksHelmetGrab(PlayerHand hand)
    {
        if (hand == null)
            return false;
        Vector3 position = hand.transform.position;
        return HandBag(hand) != null ||
            (backBag != null && backZone != null && backZone.Contains(position)) ||
            TouchedDroppedBag(position) != null;
    }

    public bool OnGripPressed(PlayerHand hand)
    {
        if (IsPlayerDead() || !PhotonMasterHandoff.LocalAvailable)
            return false;
        if (HandBag(hand) != null)
            return true;

        BagSlot slot = FindSlot(hand, false, out BagObject slotBag);
        if (slot != null)
        {
            if (slotBag.RequestControl())
                TakeOut(slotBag, slot, hand);
            else
                AddPending(PendingKind.Take, slotBag, hand);
            return true;
        }

        BagObject dropped = TouchedDroppedBag(hand.transform.position);
        if (dropped != null)
        {
            if (dropped.RequestControl())
                Hold(dropped, hand);
            else
            {
                AddPending(PendingKind.Hold, dropped, hand);
                dropped.PullToward(hand);
                HoverHaptic(hand);
            }
            return true;
        }

        if (backBag != null && backZone != null && backZone.Contains(hand.transform.position))
        {
            Hold(backBag, hand);
            return true;
        }
        return false;
    }

    public bool OnGripReleased(PlayerHand hand)
    {
        BagObject held = HandBag(hand);
        if (held != null)
        {
            SetHandBag(hand, null);
            if (backBag == null && backZone != null &&
                (backZone.Contains(hand.transform.position) || backZone.Contains(held.transform.position)))
                Stow(held);
            else
                held.DropLocal(held.Velocity);
            return true;
        }

        CancelPending(hand);

        Grabbable item = hand.Held;
        if (item == null || !CanStash(item) || IsPlayerDead())
            return false;

        BagSlot target = FindSlot(hand, true, out BagObject targetBag);
        if (target != null)
        {
            if (targetBag.RequestControl())
            {
                return Put(targetBag, target, hand);
            }
            // Someone else's dropped bag: the item falls now and goes in once they hand the bag over.
            AddPending(PendingKind.Put, targetBag, hand, item);
            return false;
        }

        if (backBag != null && backZone != null && backZone.Contains(hand.transform.position))
        {
            BagSlot free = backBag.FirstEmptySlot();
            if (free != null)
            {
                return Put(backBag, free, hand);
            }
        }
        return false;
    }

    // ---------- Actions ----------

    public void DropHandsForUnavailablePlayer()
    {
        InvalidateHandClaims();
        // Do not route through grip release: it may stow the bag or insert an item.
        foreach (Pending request in pending)
            if (request.bag != null) request.bag.StopPull(request.hand);
        pending.Clear();
        foreach (BagObject bag in BagObject.Active)
            if (bag != null) bag.CancelIncomingHandClaim();
        BagObject left = leftBag, right = rightBag;
        leftBag = rightBag = null;
        if (left != null && left.State == BagState.Held && left.HolderActor == BagObject.LocalActor) PhotonHeldItems.Drop(left);
        if (right != null && right.State == BagState.Held && right.HolderActor == BagObject.LocalActor) PhotonHeldItems.Drop(right);
    }

    void Hold(BagObject bag, PlayerHand hand)
    {
        if (!bag.CanInteract) return;
        if (bag == backBag)
            SetBackBag(null, true);
        if (bag == leftBag) leftBag = null;
        if (bag == rightBag) rightBag = null;
        SetHandBag(hand, bag);
        bag.HoldLocal(hand);
    }

    void Stow(BagObject bag)
    {
        if (!bag.CanInteract) return;
        Transform anchor = chest != null ? chest : stowAnchor;
        bag.StowLocal(anchor, chest != null);
        SetBackBag(bag, true);
    }

    void DropFromHand(PlayerHand hand, BagObject bag)
    {
        SetHandBag(hand, null);
        bag.DropLocal(Vector3.zero);
    }

    void SetBackBag(BagObject bag, bool save)
    {
        if (backBag != null)
            backBag.ContentsChanged -= OnBackContentsChanged;
        backBag = bag;
        if (backBag != null)
            backBag.ContentsChanged += OnBackContentsChanged;
        if (save)
            SaveBack();
    }

    internal void ReattachRecoveredBack(BagObject bag)
    {
        if (bag == null || !bag.CanControl || bag.State != BagState.Stowed || bag.HolderActor != BagObject.LocalActor ||
            (backBag != null && backBag != bag)) return;
        Transform anchor = chest != null ? chest : stowAnchor;
        if (anchor == null) return;
        bag.ReattachBack(anchor, chest != null);
        if (backBag != bag) SetBackBag(bag, false);
    }

    void OnBackContentsChanged(BagObject bag)
    {
        if (bag == backBag)
            SaveBack();
    }

    /// <summary>Death: every bag we have falls where it is, contents and all. Nothing is saved.</summary>
    public void DropAllOnDeath()
    {
        DropWorldBagsOnDeath();
        foreach (Pending request in pending)
            if (request.bag != null)
                request.bag.StopPull(request.hand);
        pending.Clear();
        leftBag = rightBag = null;
        SetBackBag(null, false);
        DuckzLog.Log("[Bag] Died: dropped every bag");
    }

    // Also works when the local inventory is missing. One event-only pass;
    // remote bags' local pull previews must stop even when we cannot drop them.
    public static void DropWorldBagsOnDeath()
    {
        // Event-only scan: cancel incoming bags even if ownership has not arrived yet.
        foreach (BagObject bag in BagObject.Active)
        {
            bag.CancelLocalPull();
            bag.CancelIncomingClaim();
            // Inventory references may already have been cleared by a handoff.
            if (bag.CanControl && bag.HolderActor == BagObject.LocalActor && bag.State != BagState.Dropped)
                bag.DropLocal(Vector3.zero);
        }
        if (UserPlayerPrefs.IsReady)
        {
            UserPlayerPrefs.DeleteKey(SaveKey);
            UserPlayerPrefs.Save();
        }
    }

    /// <summary>The controlling bag retries delivery until this inventory is available.</summary>
    public void Receive(BagObject bag, BagObject.Target target)
    {
        if (bag == null || !bag.CanControl || !bag.AwaitingClaim) return;
        if (!bag.IsCurrentLocalClaim)
        {
            bag.DropLocal(Vector3.zero);
            return;
        }
        Place(bag, target);
        DuckzLog.Log($"[Bag] Delivered view {bag.photonView.ViewID}, target {target}, state {bag.State}", bag);
    }

    bool Put(BagObject bag, BagSlot slot, PlayerHand hand)
    {
        if (bag == null || !bag.CanControl || slot == null || !slot.IsEmpty || hand == null) return false;
        Grabbable item = hand.Held;
        if (!CanStash(item) || !item.gameObject.activeInHierarchy) return false;
        string key = PrefabKey(item);
        Vector3 from = item.transform.position;
        float size = BagSlot.WorldSize(item.gameObject);

        if (!Despawn(item)) return false;
        bag.Put(slot, key, from, size);
        return true;
    }

    void PutLoose(BagObject bag, Grabbable item)
    {
        if (bag == null || !bag.CanControl) return;
        BagSlot slot = bag.FirstEmptySlot();
        if (slot == null || item == null || item.IsHeld || item.IsStashed || !CanStash(item))
            return;
        string key = PrefabKey(item);
        Vector3 from = item.transform.position;
        float size = BagSlot.WorldSize(item.gameObject);
        if (!Despawn(item)) return;
        bag.Put(slot, key, from, size);
    }

    void TakeOut(BagObject bag, BagSlot slot, PlayerHand hand)
    {
        if (!bag.CanControl) return;
        GameObject prefab = BagObject.ResolvePrefab(slot.ItemKey);
        if (prefab == null) return;
        ItemPickupPose pose = ItemPickupPose.Capture(prefab, slot.DisplayContent, hand.HoldPoint.position);
        if (!pose.ClampGripToSurface(prefab, hand.HoldPoint.position)) return;
        Grabbable item = pose.Spawn(prefab, hand);
        if (item == null) return;
        if (!hand.ForceGrab(item))
        {
            Despawn(item);
            return;
        }
        bag.Take(slot);
    }

    // ---------- Pending ownership requests ----------

    Pending AddPending(PendingKind kind, BagObject bag, PlayerHand hand, Grabbable item = null)
    {
        var p = new Pending { kind = kind, bag = bag, hand = hand, item = item, until = Time.time + requestTimeout };
        pending.Add(p);
        return p;
    }

    void CancelPending(PlayerHand hand)
    {
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Pending p = pending[i];
            if (p.hand != hand || (p.kind != PendingKind.Hold && p.kind != PendingKind.Take))
                continue;
            if (p.kind == PendingKind.Hold && p.bag != null)
                p.bag.StopPull(hand);
            pending.RemoveAt(i);
        }
    }

    void OnBagOwnershipChanged(BagObject bag)
    {
        ForgetLostBags();
        ReattachRecoveredBack(bag);
        ProcessPending();
    }

    void ProcessPending()
    {
        if (!PhotonMasterHandoff.LocalAvailable) return;
        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Pending p = pending[i];
            bool takenByOther = p.kind == PendingKind.Hold && p.bag != null && p.bag.State != BagState.Dropped;
            bool itemGone = p.kind == PendingKind.Put && (p.item == null || !p.item.isActiveAndEnabled);
            if (p.bag == null || itemGone || Time.time > p.until || takenByOther)
            {
                if (p.bag != null)
                {
                    DuckzLog.Log($"[Bag] Gave up waiting for bag {p.bag.BagId} ({p.kind}){(takenByOther ? ": someone else picked it up" : "")}");
                    p.bag.StopPull(p.hand);
                }
                pending.RemoveAt(i);
                continue;
            }
            if (!p.bag.CanInteract)
                continue;
            pending.RemoveAt(i);
            if (p.kind == PendingKind.Hold)
                p.bag.StopPull(p.hand);
            Complete(p);
        }
    }

    void Complete(Pending p)
    {
        BagObject bag = p.bag;
        PlayerHand hand = p.hand;
        switch (p.kind)
        {
            case PendingKind.Hold:
                if (bag.State == BagState.Dropped && HandFree(hand) && GripDown(hand) && !IsPlayerDead())
                    Hold(bag, hand);
                break;
            case PendingKind.Take:
                if (HandFree(hand) && GripDown(hand) && !IsPlayerDead())
                {
                    BagSlot slot = FindSlotOn(bag, hand, false);
                    if (slot != null)
                        TakeOut(bag, slot, hand);
                }
                break;
            case PendingKind.Put:
                PutLoose(bag, p.item);
                break;
        }
    }

    void Place(BagObject bag, BagObject.Target target)
    {
        if (debugBagPending && target == BagObject.Target.Back)
        {
            debugBagPending = false;
            if (bag.BagId != debugBagPendingId || debugBagPendingId == 0 || IsPlayerDead())
            {
                DestroyBag(bag);
                return;
            }
            debugWornId = bag.BagId;
        }
        if (IsPlayerDead())
        {
            bag.DropLocal(Vector3.zero);
            return;
        }
        if (target == BagObject.Target.Back)
        {
            if (backBag == null)
                Stow(bag);
            else
                bag.DropLocal(Vector3.zero);
            return;
        }
        if (target == BagObject.Target.Dropped)
        {
            bag.DropLocal(Vector3.zero);
            return;
        }

        GR.Player player = Player();
        PlayerHand hand = player == null ? null : target == BagObject.Target.LeftHand ? player.leftPlayerHand : player.rightPlayerHand;
        // The store wall's display model has been standing in for the bag; it shrinks away on release.
        if (hand != null && hand.Held is StoreWallItem)
            hand.Release();
        if (hand != null && HandFree(hand) && GripDown(hand))
            Hold(bag, hand);
        else
            bag.DropLocal(Vector3.zero);
    }

    // ---------- Hands ----------

    BagObject HandBag(PlayerHand hand) => hand == null ? null : hand.IsControllerLTouch ? leftBag : rightBag;

    void SetHandBag(PlayerHand hand, BagObject bag)
    {
        if (hand.IsControllerLTouch)
            leftBag = bag;
        else
            rightBag = bag;
    }

    bool HandFree(PlayerHand hand) => hand != null && hand.CanGrab && hand.Held == null && HandBag(hand) == null;

    static bool GripDown(PlayerHand hand)
    {
        GR.Player player = GR.Player.instance;
        if (player == null || hand == null)
            return false;
        return hand.IsControllerLTouch
            ? player.ReturnButtonPress_Left(ButtonName.Grip).down
            : player.ReturnButtonPress_Right(ButtonName.Grip).down;
    }

    static GR.Player Player() => GR.Player.instance;

    // ---------- Finding bags and slots ----------

    BagObject TouchedDroppedBag(Vector3 position)
    {
        BagObject best = null;
        float bestSq = float.MaxValue;
        foreach (BagObject bag in BagObject.Active)
        {
            if (bag.State != BagState.Dropped || bag.AwaitingClaim || !bag.Touches(position, pickupRadius))
                continue;
            float d = (bag.transform.position - position).sqrMagnitude;
            if (d < bestSq)
            {
                bestSq = d;
                best = bag;
            }
        }
        return best;
    }

    // Bags whose slots this hand may use: dropped bags, and a bag held in our other hand. Never a bag on a back or in someone's hand.
    bool SlotsReachable(BagObject bag, PlayerHand hand)
    {
        if (bag == HandBag(hand))
            return false;
        if (bag.State == BagState.Dropped)
            return !bag.AwaitingClaim;
        return bag.State == BagState.Held && bag.CanControl && (bag == leftBag || bag == rightBag);
    }

    // Hand holding something only targets empty slots; an empty hand only targets filled ones.
    BagSlot FindSlot(PlayerHand hand, bool wantEmpty, out BagObject bag)
    {
        bag = null;
        if (hand == null || HandBag(hand) != null)
            return null;
        if (wantEmpty && (hand.Held == null || !CanStash(hand.Held)))
            return null;
        if (!wantEmpty && hand.Held != null)
            return null;

        float radius = wantEmpty ? hoverRadius : takeRadius;
        float bestSq = radius * radius;
        BagSlot result = null;
        foreach (BagObject candidate in BagObject.Active)
        {
            if (!SlotsReachable(candidate, hand))
                continue;
            BagSlot slot = candidate.NearestSlot(hand.transform.position, hand.HoldPoint.position, wantEmpty, ref bestSq);
            if (slot != null)
            {
                result = slot;
                bag = candidate;
            }
        }
        return result;
    }

    BagSlot FindSlotOn(BagObject bag, PlayerHand hand, bool wantEmpty)
    {
        float radius = wantEmpty ? hoverRadius : takeRadius;
        float bestSq = radius * radius;
        return bag.NearestSlot(hand.transform.position, hand.HoldPoint.position, wantEmpty, ref bestSq);
    }

    // ---------- Hover ----------

    void UpdateHover()
    {
        GR.Player player = Player();
        BagSlot left = null, right = null;
        BagObject leftOn = null, rightOn = null;
        if (player != null && !IsPlayerDead())
        {
            left = HoverFor(player.leftPlayerHand, out leftOn);
            right = HoverFor(player.rightPlayerHand, out rightOn);
        }

        // Per hand, so the slot under a hand that just put or took an item doesn't re-trigger.
        if (left != null && left != leftHover)
        {
            PlaySound(leftOn.HoverSound, left.transform.position);
            HoverHaptic(player.leftPlayerHand);
        }
        if (right != null && right != rightHover)
        {
            if (right != left)
                PlaySound(rightOn.HoverSound, right.transform.position);
            HoverHaptic(player.rightPlayerHand);
        }

        if (leftHover != null && leftHover != left && leftHover != right)
            leftHover.SetHovered(false);
        if (rightHover != null && rightHover != left && rightHover != right)
            rightHover.SetHovered(false);
        if (left != null) left.SetHovered(true);
        if (right != null) right.SetHovered(true);
        leftHover = left;
        rightHover = right;

        ShowPutLine(ref leftLine, left, player != null ? player.leftPlayerHand : null);
        ShowPutLine(ref rightLine, right, player != null ? player.rightPlayerHand : null);
    }

    BagSlot HoverFor(PlayerHand hand, out BagObject bag)
    {
        bag = null;
        if (hand == null) return null;
        return FindSlot(hand, hand.IsHolding, out bag);
    }

    void ShowPutLine(ref LineRenderer line, BagSlot slot, PlayerHand hand)
    {
        bool show = slot != null && slot.IsEmpty && hand != null && hand.IsHolding;
        if (!show)
        {
            if (line != null)
                line.enabled = false;
            return;
        }

        if (line == null)
            line = CreatePutLine();
        if (line == null)
            return;

        line.enabled = true;
        line.SetPosition(0, hand.transform.position);
        line.SetPosition(1, slot.transform.position);
    }

    LineRenderer CreatePutLine()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            return null;

        var go = new GameObject("BagSlotLine");
        go.transform.SetParent(transform, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = new Material(shader);
        lr.useWorldSpace = true;
        lr.positionCount = 2;
        lr.widthMultiplier = 0.004f;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.startColor = new Color(1f, 0.92f, 0.55f, 1f);
        lr.endColor = lr.startColor;
        return lr;
    }

    void HoverHaptic(PlayerHand hand)
    {
        if (hand != null && hoverHapticAmplitude > 0f && VibrationManager.instance != null)
            VibrationManager.instance.TriggerVibrationPulse(hoverHapticFrequency, hoverHapticAmplitude, hoverHapticDuration, hand);
    }

    // ---------- Items ----------

    static bool Despawn(Grabbable item)
    {
        if (item == null || !item.gameObject.activeInHierarchy) return false;
        PhotonView view = item.GetComponent<PhotonView>();
        if (view != null && PhotonNetwork.InRoom)
        {
            if (!view.IsMine || view.ViewID == 0 || PhotonView.Find(view.ViewID) != view) return false;
            PhotonNetwork.Destroy(item.gameObject);
            return item == null || !item.gameObject.activeSelf;
        }
        else
        {
            item.gameObject.SetActive(false);
            Destroy(item.gameObject);
            return true;
        }
    }

    // "ItemGoldBar(Clone)" and scene copies like "ItemHelmet (2)" both save as the prefab name.
    static string PrefabKey(Grabbable item)
    {
        string name = item.name;
        int cut = name.IndexOf('(');
        if (cut > 0)
            name = name.Substring(0, cut);
        return name.Trim();
    }

    // Only items the bag can spawn again (a known prefab) and destroy (ours on the network) can go in.
    static bool CanStash(Grabbable item)
    {
        if (item == null || !item.CanBeStashed) return false;
        BagItem bagItem = item.GetComponent<BagItem>();
        if (bagItem != null && !bagItem.canStash) return false;
        if (BagObject.ResolvePrefab(PrefabKey(item)) == null) return false;
        PhotonView view = item.GetComponent<PhotonView>();
        return view == null || !PhotonNetwork.InRoom || view.IsMine;
    }

    static bool IsPlayerDead() => GR.Player.instance != null && GR.Player.instance.IsDead;

    static void PlaySound(SoundEvent sound, Vector3 position)
    {
        if (sound != null && AudioManager.instance != null)
            AudioManager.instance.Play(sound, position);
    }
}
