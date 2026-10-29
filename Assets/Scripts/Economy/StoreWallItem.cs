using Photon.Pun;
using TMPro;
using UnityEngine;

// A store item displayed on the wall. Grab it and pull it away from the wall: a line back to its spot changes
// color with the pull distance. At pullDistance the purchase starts (owned items just equip). On success the
// model comes off the wall into the hand; otherwise it snaps back. Released models shrink away and reappear on the wall.
// Needs a collider on the Grabbable layer and a kinematic Rigidbody.
public class StoreWallItem : Grabbable
{
    enum State { Idle, Pulling, Pending, Taken, Returning, Vanishing, Respawning }

    [Header("Store")]
    [SerializeField] StoreUI store;
    [SerializeField] string itemKey;
    [Tooltip("Optional: shows the price, Equip or Equipped.")]
    [SerializeField] TMP_Text label;

    [Header("Pull")]
    [SerializeField] float pullDistance = 0.3f;
    [Tooltip("Resistance beyond the purchase distance while waiting for confirmation. 0 follows the hand fully; 1 stops at the purchase distance.")]
    [SerializeField, Range(0f, 1f)] float pendingPullResistance = 0.02f;
    [SerializeField] LineRenderer line;
    [Tooltip("Optional close-fitting surface collider for the tether. Use a primitive or convex MeshCollider that stays inside the visible mesh.")]
    [SerializeField] Collider pullSurface;
    [SerializeField] Gradient pullColor = DefaultPullColor();
    [SerializeField] Color failColor = new Color(1f, 0.25f, 0.2f);
    [SerializeField] float snapBackDuration = 0.18f;
    [SerializeField] AudioClip pullOffHaptic;

    [Header("Restock")]
    [SerializeField] float vanishDuration = 0.2f;
    [SerializeField] float respawnDelay = 1.5f;
    [SerializeField] float appearDuration = 0.25f;

    State state;
    Transform homeParent;
    Vector3 homeLocalPosition;
    Quaternion homeLocalRotation;
    Vector3 homeScale;
    Vector3 vanishFromScale;
    Vector3 grabOffset;
    Vector3 pullAnchorLocal;
    Vector3 wallAnchorLocal;
    Bounds wallMeshBounds;
    Collider[] pullColliders;
    Vector3 animFrom;
    float animTime;

    public override bool CanBeStashed => false;

    Vector3 HomePosition => homeParent != null ? homeParent.TransformPoint(homeLocalPosition) : homeLocalPosition;
    Quaternion HomeRotation => homeParent != null ? homeParent.rotation * homeLocalRotation : homeLocalRotation;

    static Gradient DefaultPullColor()
    {
        var g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.85f, 0.2f), 0.6f), new GradientColorKey(new Color(0.3f, 1f, 0.4f), 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
        return g;
    }

    protected override void Awake()
    {
        base.Awake();
        rb.isKinematic = true;
        rb.useGravity = false;

        homeParent = transform.parent;
        homeLocalPosition = transform.localPosition;
        homeLocalRotation = transform.localRotation;
        homeScale = transform.localScale;
        pullColliders = GetComponentsInChildren<Collider>();
        CacheWallMeshBounds();

        if (store == null)
            store = GetComponentInParent<StoreUI>();
        if (line == null)
            line = CreateLine();
        if (line != null)
        {
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.enabled = false;
        }

        if (label == null)
            StorePriceLabel.Attach(this, store, itemKey);
    }

    // Sprites/Default is always included in builds and honors the per-vertex colors the gradient sets.
    LineRenderer CreateLine()
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
            return null;

        var go = new GameObject(name + " PullLine");
        go.transform.SetParent(homeParent, false);
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.sharedMaterial = new Material(shader);
        lr.widthMultiplier = 0.01f;
        lr.numCapVertices = 4;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        return lr;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (store != null)
            store.OnStateChanged += RefreshLabel;
        RefreshLabel();
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        if (store != null)
            store.OnStateChanged -= RefreshLabel;
    }

    // The Meta payment modal takes focus. Let go immediately; the purchase keeps running, and the item
    // stays on the wall for the player to pull once they own it.
    void OnApplicationPause(bool paused)
    {
        if (paused)
            LetGoForCheckout();
    }

    void OnApplicationFocus(bool focused)
    {
        if (!focused)
            LetGoForCheckout();
    }

    void LetGoForCheckout()
    {
        if (state != State.Pending || store == null || !store.CheckingOut)
            return;
        DuckzLog.Log($"[Store] Meta checkout took focus, letting go of '{itemKey}'");
        ReturnToWall();
    }

    public override void DropForUnavailablePlayer()
    {
        if (!IsHeld) return;
        // This is the store's display, not a purchased world item.
        Holder.NotifyStolen();
        Release(Vector3.zero, Vector3.zero);
    }

    public override void Grab(PlayerHand hand)
    {
        if (hand == null || !hand.CanGrab) return;
        if (state != State.Idle && state != State.Taken) return;

        // Capture the wall anchor before attaching the display to the hand.
        if (state == State.Idle)
            CachePullAnchor(hand.HoldPoint.position);
        base.Grab(hand);
        if (Holder != hand || state == State.Taken) return;

        // Stay on the wall's transform; LateUpdate drags the model along the hand's offset.
        transform.SetParent(homeParent, true);
        grabOffset = transform.position - hand.HoldPoint.position;
        state = State.Pulling;
        if (line != null)
        {
            UpdateLine(0f, false);
            line.enabled = true;
        }
    }

    public override void Release(Vector3 linearVelocity, Vector3 angularVelocity)
    {
        base.Release(linearVelocity, angularVelocity);
        rb.isKinematic = true;
        rb.useGravity = false;

        switch (state)
        {
            case State.Pulling:
                StartReturn();
                break;
            case State.Pending:
                transform.SetParent(homeParent, true);
                break;
            case State.Taken:
                StartVanish();
                break;
        }
    }

    void LateUpdate()
    {
        switch (state)
        {
            case State.Pulling:
                UpdatePull();
                break;
            case State.Pending:
                if (Holder != null)
                    transform.position = PendingPullPosition();
                UpdateLine(1f, false);
                break;
            case State.Returning:
                Animate(snapBackDuration, t =>
                {
                    transform.position = Vector3.LerpUnclamped(animFrom, HomePosition, EaseOutBack(t));
                    UpdateLine(1f - t, true);
                }, FinishReturn);
                break;
            case State.Vanishing:
                Animate(vanishDuration, t => transform.localScale = vanishFromScale * (1f - t), BeginRespawn);
                break;
            case State.Respawning:
                animTime += Time.deltaTime;
                if (animTime >= respawnDelay)
                {
                    float t = Mathf.Clamp01((animTime - respawnDelay) / appearDuration);
                    transform.localScale = homeScale * EaseOutBack(t);
                    if (t >= 1f)
                    {
                        transform.localScale = homeScale;
                        state = State.Idle;
                    }
                }
                break;
        }
    }

    void UpdatePull()
    {
        if (Holder == null)
        {
            StartReturn();
            return;
        }

        Vector3 target = Holder.HoldPoint.position + grabOffset;
        float distance = Vector3.Distance(target, HomePosition);
        transform.position = target;
        UpdateLine(distance / pullDistance, false);

        if (distance >= pullDistance)
            BeginPurchase();
    }

    void BeginPurchase()
    {
        state = State.Pending;
        transform.position = PendingPullPosition();
        if (store == null)
        {
            Fail();
            return;
        }
        store.Purchase(itemKey, OnPurchaseResult);
    }

    void OnPurchaseResult(StorePurchaseResult result)
    {
        if (state != State.Pending) return;

        if (result == StorePurchaseResult.Bought || result == StorePurchaseResult.Equipped)
            PullOff();
        else if (result == StorePurchaseResult.Unlocked)
            ReturnToWall();
        else
            Fail();
    }

    void PullOff()
    {
        if (line != null)
            line.enabled = false;

        PlayerHand hand = Holder;
        if (hand != null && TrySpawnWeapon(hand))
            return;
        if (hand != null)
            RequestBag(hand);

        if (Holder == null)
        {
            StartVanish();
            return;
        }

        state = State.Taken;
        transform.SetParent(Holder.HoldPoint, true);
        StoreItem item = store != null ? store.Find(itemKey) : null;
        ItemSounds bought = item != null && item.prefab != null ? item.prefab.GetComponent<ItemSounds>() : null;
        if (bought != null)
            RoomRpc.PlaySound(bought.Get(ItemSound.Grab), transform.position);
        if (pullOffHaptic != null && VibrationManager.instance != null)
            VibrationManager.instance.TriggerVibration(pullOffHaptic, Holder);
    }

    // Swaps the wall display in the hand for a real networked copy of the item's PhotonGrabbable prefab.
    bool TrySpawnWeapon(PlayerHand hand)
    {
        StoreItem item = store != null ? store.Find(itemKey) : null;
        GameObject prefab = item != null ? item.prefab : null;
        if (prefab == null || prefab.GetComponent<PhotonGrabbable>() == null || !PhotonNetwork.InRoom)
            return false;

        ItemPickupPose pose = ItemPickupPose.Capture(prefab, transform, hand.HoldPoint.position);
        if (!pose.ClampGripToSurface(prefab, hand.HoldPoint.position)) return false;
        Grabbable weapon = pose.Spawn(prefab, hand);
        if (weapon == null) return false;
        StartVanish();
        hand.Release();
        // The real item now occupies this display's mesh pose. Avoid overlapping copies.
        transform.localScale = Vector3.zero;
        vanishFromScale = Vector3.zero;
        if (!hand.ForceGrab(weapon))
            PhotonNetwork.Destroy(weapon.gameObject);

        if (pullOffHaptic != null && VibrationManager.instance != null)
            VibrationManager.instance.TriggerVibration(pullOffHaptic, hand);
        return true;
    }

    // Bags are room objects spawned by the master. This model stays in the hand until BagInventory swaps the real bag in.
    void RequestBag(PlayerHand hand)
    {
        StoreItem item = store != null ? store.Find(itemKey) : null;
        if (item == null || item.type != StoreItemType.Bag)
            return;
        Transform hold = hand.HoldPoint;
        RoomRpc.SpawnBag(item.bagId, null, hold.position, hold.rotation,
            hand.IsControllerLTouch ? BagObject.Target.LeftHand : BagObject.Target.RightHand);
    }

    void Fail() => ReturnToWall();

    // Drops the held display and snaps it back. Safe to call again once it has already let go.
    void ReturnToWall()
    {
        if (state != State.Pending && state != State.Pulling)
            return;
        state = State.Returning;
        if (Holder != null)
            Holder.Release();
        StartReturn();
    }

    void StartReturn()
    {
        state = State.Returning;
        transform.SetParent(homeParent, true);
        animFrom = transform.position;
        animTime = 0f;
    }

    void FinishReturn()
    {
        ResetToHome();
        state = State.Idle;
    }

    void StartVanish()
    {
        state = State.Vanishing;
        animTime = 0f;
        vanishFromScale = transform.localScale;
        if (line != null)
            line.enabled = false;
    }

    void BeginRespawn()
    {
        ResetToHome();
        transform.localScale = Vector3.zero;
        state = State.Respawning;
        animTime = 0f;
    }

    void ResetToHome()
    {
        transform.SetParent(homeParent, false);
        transform.localPosition = homeLocalPosition;
        transform.localRotation = homeLocalRotation;
        transform.localScale = homeScale;
        if (line != null)
            line.enabled = false;
    }

    Vector3 PendingPullPosition()
    {
        Vector3 home = HomePosition;
        Vector3 target = Holder != null ? Holder.HoldPoint.position + grabOffset : transform.position;
        Vector3 offset = target - home;
        if (offset.sqrMagnitude <= pullDistance * pullDistance)
            return target;

        Vector3 thresholdPosition = home + offset.normalized * pullDistance;
        return Vector3.Lerp(target, thresholdPosition, pendingPullResistance);
    }

    void UpdateLine(float progress, bool failed)
    {
        if (line == null) return;
        Vector3 itemAnchor = transform.TransformPoint(pullAnchorLocal);
        line.SetPosition(0, homeParent != null ? homeParent.TransformPoint(wallAnchorLocal) : wallAnchorLocal);
        line.SetPosition(1, itemAnchor);
        Color c = failed ? failColor : pullColor.Evaluate(Mathf.Clamp01(progress));
        line.startColor = c;
        line.endColor = c;
    }

    void CacheWallMeshBounds()
    {
        wallMeshBounds = new Bounds(homeLocalPosition, Vector3.zero);
        bool hasBounds = false;
        foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
        {
            if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;
            Bounds bounds = renderer.localBounds;
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = bounds.center + Vector3.Scale(bounds.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                Vector3 world = renderer.transform.TransformPoint(corner);
                Vector3 local = homeParent != null ? homeParent.InverseTransformPoint(world) : world;
                if (!hasBounds)
                {
                    wallMeshBounds = new Bounds(local, Vector3.zero);
                    hasBounds = true;
                }
                else wallMeshBounds.Encapsulate(local);
            }
        }
    }

    void CachePullAnchor(Vector3 handPosition)
    {
        Vector3 closest = transform.position;
        float bestDistance = float.PositiveInfinity;
        if (pullSurface != null)
            TryPullSurface(pullSurface, handPosition, ref closest, ref bestDistance);
        else
            foreach (Collider candidate in pullColliders)
                TryPullSurface(candidate, handPosition, ref closest, ref bestDistance);
        pullAnchorLocal = transform.InverseTransformPoint(closest);
        // Lock the wall endpoint for this grab, while still following the helicopter.
        wallAnchorLocal = homeParent != null ? homeParent.InverseTransformPoint(closest) : closest;
        wallAnchorLocal.x = wallMeshBounds.center.x;
        wallAnchorLocal.y = Mathf.Clamp(wallAnchorLocal.y, wallMeshBounds.min.y, wallMeshBounds.max.y);
        wallAnchorLocal.z = homeLocalPosition.z;
    }

    static void TryPullSurface(Collider candidate, Vector3 handPosition, ref Vector3 closest, ref float bestDistance)
    {
        if (!candidate.enabled || !candidate.gameObject.activeInHierarchy || candidate.isTrigger) return;
        // Physics.ClosestPoint supports primitives and convex meshes, and does not
        // depend on a physics transform sync after moving the helicopter.
        if (!(candidate is BoxCollider) && !(candidate is SphereCollider) &&
            !(candidate is CapsuleCollider) && !(candidate is MeshCollider mesh && mesh.convex)) return;
        Vector3 point = Physics.ClosestPoint(handPosition, candidate, candidate.transform.position, candidate.transform.rotation);
        float distance = (point - handPosition).sqrMagnitude;
        if (distance >= bestDistance) return;
        bestDistance = distance;
        closest = point;
    }

    void Animate(float duration, System.Action<float> step, System.Action finished)
    {
        animTime += Time.deltaTime;
        float t = duration > 0f ? Mathf.Clamp01(animTime / duration) : 1f;
        step(t);
        if (t >= 1f)
            finished();
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    void RefreshLabel()
    {
        if (label == null || store == null) return;
        StoreItem item = store.Find(itemKey);
        if (item == null) return;

        label.text = store.StatusLabel(item);
    }
}
