using System;
using System.Reflection;
using System.Diagnostics;
using Photon.Pun;
using Unity.Profiling;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;
using Debug = UnityEngine.Debug;

public static class PhotonGrabbableChecks
{
    const BindingFlags P = BindingFlags.Instance | BindingFlags.NonPublic;
    static FieldInfo Field(Type type, string name) { while (type != null) { var f = type.GetField(name, P); if (f != null) return f; type = type.BaseType; } throw new Exception(name); }
    static void Set(object o, string n, object v) => Field(o.GetType(), n).SetValue(o, v);
    static object Get(object o, string n) => Field(o.GetType(), n).GetValue(o);
    static void Call(object o, string n) => o.GetType().GetMethod(n, P).Invoke(o, null);
    static Action Bind(object o, string n) => (Action)Delegate.CreateDelegate(typeof(Action), o, o.GetType().GetMethod(n, P));
    static void Mine(PhotonView v, bool value) => typeof(PhotonView).GetProperty("IsMine").SetValue(v, value);
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); }
    public static Grabbable Create(Type type, bool samples, bool fallback = false)
    {
        var go = new GameObject(type.Name + " fixture"); go.SetActive(false);
        go.AddComponent<PhotonView>(); go.AddComponent<PhotonTransformView>();
        var item = (Grabbable)go.AddComponent(type);
        var child = new GameObject("sample shape"); child.transform.SetParent(go.transform, false);
        if (samples) { var box = child.AddComponent<BoxCollider>(); box.size = new Vector3(.12f, 1.8f, .12f); Set(item, "locomotionCollider", box); }
        else if (fallback) child.AddComponent<SphereCollider>().radius = .1f;
        Set(item, "locomotionWhileHeld", true);
        Call(item, "Awake");
        Mine(go.GetComponent<PhotonView>(), true);
        go.SetActive(true); Physics.SyncTransforms();
        return item;
    }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch only");
        try {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Behavior();
            OwnershipCleanup();
            ContentionEdges();
            QueuedDropUpdates();
            QueuedDropUpdates(true);
            PrefabObservationOrder();
            Benchmark();
            EquipmentPersistenceChecks.Run();
            Debug.Log("PhotonGrabbableChecks: ALL PASSED");
            EditorApplication.Exit(0);
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Behavior()
    {
        var item = (PhotonGrabbable)Create(typeof(PhotonGrabbable), true);
        var tick = Bind(item, "LateUpdate"); var swing = Bind(item, "TickSwing");
        var shape = item.GetComponentInChildren<BoxCollider>(); var rb = item.GetComponent<Rigidbody>(); var view = item.GetComponent<PhotonView>();
        tick(); PhotonGrabbable.ResetProfiling();
        var previous = (Vector3[])((Vector3[])Get(item, "_samplePrev")).Clone();
        for (int i = 0; i < 100; i++) tick();
        Check(PhotonGrabbable.IdleSkips == 100 && PhotonGrabbable.SamplePoints == 0, "Idle sample bypass");
        Check(PhotonGrabbable.PhysicsWrites == 0 && PhotonGrabbable.SyncWrites == 0, "Idle state writes");
        var saved = (Vector3[])Get(item, "_samplePrev");
        for (int i = 0; i < previous.Length; i++) Check(saved[i].Equals(previous[i]), "Idle retains previous samples");
        item.transform.position += Vector3.right * .01f; swing();
        Check(PhotonGrabbable.SamplePoints == item.LocomotionSampleCount, "First movement is sampled");
        long points = PhotonGrabbable.SamplePoints;
        shape.transform.localPosition += Vector3.up * .01f; swing();
        Check(PhotonGrabbable.SamplePoints > points, "Child collider motion is sampled"); points = PhotonGrabbable.SamplePoints;
        item.transform.localScale *= 1.1f; swing(); Check(PhotonGrabbable.SamplePoints > points, "Scale motion is sampled");
        var reference = new Vector3[Grabbable.MaxLocomotionSamples];
        for (int pose = 0; pose < 20; pose++) {
            item.transform.rotation = Quaternion.Euler(pose * 7, pose * 13, pose * 3);
            item.transform.localScale = new Vector3(1 + pose * .03f, .8f, 1.2f);
            swing(); int n = item.CopySampleWorldPositions(reference);
            var actual = (Vector3[])Get(item, "_sampleNow");
            for (int i = 0; i < n; i++) Check((actual[i] - reference[i]).sqrMagnitude < 1e-10f, "Matrix/TransformPoint parity");
        }
        points = PhotonGrabbable.SamplePoints;
        typeof(Grabbable).GetMethod("RebuildLocomotionSamples", P).Invoke(item, null); swing();
        Check(PhotonGrabbable.SamplePoints > points, "Sample revision invalidates reuse");
        rb.useGravity = false; tick(); Check(rb.useGravity, "External physics change corrected");
        Mine(view, false); tick(); Check(rb.isKinematic && !rb.useGravity, "Remote physics preserved");
        Mine(view, true); tick(); Check(!rb.isKinematic && rb.useGravity, "Ownership transfer wakes physics");
        var handGo = new GameObject("hand"); var hand = handGo.AddComponent<PlayerHand>(); hand.enabled = false;
        view.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        item.Grab(hand); tick(); Check(item.IsHeld && rb.isKinematic && !rb.detectCollisions, "Grab pose physics");
        item.Release(Vector3.right * 3, Vector3.up * 2); tick();
        Check(!item.IsHeld && !rb.isKinematic && rb.useGravity && rb.linearVelocity.x == 3, "Throw velocity and physics");
        item.BeginPickupGrowth(Vector3.one * .1f, Vector3.one, Vector3.zero, Time.timeAsDouble);
        swing(); Check(!(bool)Get(item, "_hasSamplePrev"), "Growth disables weapon history");
        typeof(Grabbable).GetMethod("FinishPickupGrowth", P).Invoke(item, null); swing();
        Check((bool)Get(item, "_hasSamplePrev"), "Growth completion samples again");
        item.Stash(handGo.transform); tick(); Check(item.IsStashed && rb.isKinematic, "Stash preserved");
        item.Unstash(Vector3.zero, Quaternion.identity); tick(); Check(!item.IsStashed && !rb.isKinematic, "Unstash preserved");
        // Incoming held packet must still disable interpolation and freeze replica physics.
        Mine(view, false);
        item.OnPhotonSerializeView(new PhotonStream(false, new object[] { 0, true, 7, false, 1, true, 0d, false, false, Vector3.zero, Quaternion.identity }), default);
        tick(); Check(item.IsHeldByAnyPlayer && rb.isKinematic && !item.GetComponent<PhotonTransformView>().enabled, "Remote held stream");
        item.OnPhotonSerializeView(new PhotonStream(false, new object[] { 0, false, 0, false, 1, true, 0d, true, false }), default);
        tick(); Check(item.IsStashed, "Remote bag stream"); Mine(view, true); tick();
        Check(!item.IsStashed && !rb.isKinematic, "Owner fallback drops remote bag item");
        // Waiting for destruction ownership must not be swallowed by idle optimization.
        Set(item, "destroyAfterOwnershipFallback", true); Mine(view, false); PhotonGrabbable.ResetProfiling(); tick();
        Check((bool)Get(item, "destroyAfterOwnershipFallback") && PhotonGrabbable.SwingCalls == 0, "Lifecycle fallback keeps priority");
        Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(handGo);
        var fallback = (PhotonGrabbable)Create(typeof(PhotonGrabbable), false, true); var fallbackTick = Bind(fallback, "LateUpdate");
        fallbackTick(); PhotonGrabbable.ResetProfiling(); fallbackTick();
        Check(PhotonGrabbable.IdleSkips == 1, "Fallback sphere supports idle reuse");
        var sphere = fallback.GetComponentInChildren<SphereCollider>(); sphere.enabled = false; fallbackTick();
        sphere.enabled = true; fallbackTick(); Check((bool)Get(fallback, "_hasSamplePrev"), "Re-enabled collider discovered");
        Object.DestroyImmediate(fallback.gameObject);
        var missing = (PhotonGrabbable)Create(typeof(PhotonGrabbable), false); var missingTick = Bind(missing, "LateUpdate"); missingTick();
        missing.gameObject.AddComponent<SphereCollider>(); Physics.SyncTransforms(); missingTick();
        Check((bool)Get(missing, "_hasSamplePrev"), "New component without hierarchy event discovered"); Object.DestroyImmediate(missing.gameObject);
        Debug.Log("Grabbable behavior PASS: idle retention, first move, child/scale/rebuild, physics correction, grab/throw/growth, stash/remote/ownership/fallback transitions");
    }
    static void OwnershipCleanup()
    {
        var item = (PhotonGrabbable)Create(typeof(PhotonGrabbable), true);
        var view = item.GetComponent<PhotonView>();
        var body = item.GetComponent<Rigidbody>();
        var handGo = new GameObject("ownership hand");
        var hand = handGo.AddComponent<PlayerHand>(); hand.enabled = false;
        view.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        int releases = 0;
        item.OnReleased += _ => releases++;
        Check(hand.ForceGrab(item), "Immediate grab");
        Mine(view, false); Set(item, "pickupPending", true);
        Call(item, "LateUpdate");
        Check(hand.Held == item && item.IsHeld, "Pending pickup retains hand");
        item.OnOwnershipTransfered(view, null);
        Check(hand.Held == item && item.PickupPending, "Competing transfer before our grant retains pickup");
        Mine(view, true); item.OnOwnershipTransfered(view, null);
        Check(hand.Held == item && !item.PickupPending, "Our subsequent grant keeps the winning hand");
        Mine(view, false); item.OnOwnershipTransfered(view, null);
        Check(hand.Held == null && !item.IsHeld && releases == 0, "Loss clears both references without release");
        item.OnOwnershipTransfered(view, null);
        Check(releases == 0, "Repeated cleanup is harmless");
        Mine(view, true); Check(hand.ForceGrab(item), "Can grab again");
        Mine(view, false); Set(item, "pickupPending", true);
        hand.DetachHeld(); item.Release(Vector3.right * 3, Vector3.up);
        Call(item, "LateUpdate");
        Check(!body.isKinematic && body.linearVelocity.x == 3, "Pending throw stays immediate");
        Check(!item.GetComponent<PhotonTransformView>().enabled, "Pending throw rejects old transform stream");
        item.OnOwnershipTransfered(view, null); Call(item, "LateUpdate");
        Check(item.PickupPending && !body.isKinematic && body.linearVelocity.x == 3,
            "Intermediate competing transfer cannot interrupt pending throw");
        Mine(view, true); item.OnOwnershipTransfered(view, null); Call(item, "LateUpdate");
        Check(!item.IsHeld && hand.Held == null && body.linearVelocity.x == 3 && releases == 1,
            "Delayed grant preserves throw without reattachment or replay");
        var equipment = item.gameObject.AddComponent<HeadEquipment>();
        Set(item, "headEquipment", equipment);
        Set(equipment, "equipped", true);
        Set(equipment, "remoteEquipped", false);
        int unequips = 0;
        equipment.LocallyUnequipped += () => unequips++;
        Mine(view, false); item.OnOwnershipTransfered(view, null);
        item.OnOwnershipTransfered(view, null);
        Check(!equipment.IsLocallyEquipped && unequips == 1, "Equipment loss clears effects once");
        Mine(view, true);
        Check(hand.ForceGrab(item), "Grab before removal");
        item.gameObject.SetActive(false);
        Call(item, "OnDisable"); // Edit-mode fixtures invoke lifecycle methods explicitly, like Awake above.
        Check(hand.Held == null && !item.IsHeld && releases == 1, "Removal clears hand without release");
        Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(handGo);
        // Bounded removal history supports duplicate IDs and does not grow forever.
        var remember = typeof(PhotonNetwork).GetMethod("RememberRemovedView", BindingFlags.Static | BindingFlags.NonPublic);
        var slots = (System.Collections.IDictionary)typeof(PhotonNetwork).GetField("removedViewSlots", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        for (int i = 1; i <= 600; i++) remember.Invoke(null, new object[] { 900000 + i });
        Check(slots.Count <= 512 && slots.Contains(900600) && !slots.Contains(900001), "Removed-view history bounded");
        var receive = typeof(PhotonNetwork).GetMethod("OnEvent", BindingFlags.Static | BindingFlags.NonPublic);
        int errors = 0;
        Application.LogCallback countErrors = (_, __, kind) => { if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Warning) errors++; };
        Application.logMessageReceived += countErrors;
        try
        {
            foreach (byte code in new byte[] { 204, 209, 210, 212 })
            {
                var message = new ExitGames.Client.Photon.EventData { Code = code };
                message.Parameters[Photon.Realtime.ParameterCode.Data] = code == 204
                    ? (object)new ExitGames.Client.Photon.Hashtable { { (byte)0, 900600 } }
                    : new int[] { 900600, 1 };
                for (int i = 0; i < 100; i++) receive.Invoke(null, new object[] { message });
            }
        }
        finally { Application.logMessageReceived -= countErrors; }
        Check(errors == 0, "Repeated late ownership/destroy messages do not spam errors");
        Debug.Log("Ownership cleanup PASS: pending grab/throw, loss, repeated cleanup, removal, bounded history");
    }
    static void ContentionEdges()
    {
        var item = (PhotonGrabbable)Create(typeof(PhotonGrabbable), true);
        var view = item.GetComponent<PhotonView>();
        var pose = item.GetComponent<PhotonTransformView>();
        var handGo = new GameObject("edge hand");
        var hand = handGo.AddComponent<PlayerHand>(); hand.enabled = false;
        var current = PhotonNetwork.LocalPlayer;
        var stale = (Photon.Realtime.Player)Activator.CreateInstance(typeof(Photon.Realtime.Player),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "stale", 77, false }, null);
        typeof(PhotonView).GetProperty("Controller").SetValue(view, current);
        Mine(view, false);
        pose.m_SynchronizeScale = true;
        Vector3 start = new Vector3(2, 3, 4);
        item.transform.position = start;
        pose.enabled = false;
        pose.ResetNetworkState();
        // A sentinel represents the next observed component's data in the same stream.
        void Receive(Photon.Realtime.Player sender)
        {
            var stream = new PhotonStream(false, new object[] { Vector3.one * 90, Vector3.zero,
                Quaternion.Euler(20, 30, 40), Vector3.one * 2, 1234 });
            pose.OnPhotonSerializeView(stream, new PhotonMessageInfo(sender, 0, view));
            Check((int)stream.ReceiveNext() == 1234, "Rejected pose consumes all wire fields");
        }
        Receive(current);
        Check(item.transform.position == start && item.transform.localScale == Vector3.one,
            "Disabled first packet cannot move or resize predicted item");
        object[] packet = { Vector3.one, Vector3.zero, Quaternion.identity, Vector3.one };
        var repeated = new PhotonStream(false, packet);
        var packetInfo = new PhotonMessageInfo(current, 0, view);
        for (int i = 0; i < 100; i++) { repeated.SetReadStream(packet, 0); pose.OnPhotonSerializeView(repeated, packetInfo); }
        using (var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 10000,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
        {
            Check(allocations.Valid, "Rejected-pose allocation recorder available");
            for (int i = 0; i < 10000; i++) { repeated.SetReadStream(packet, 0); pose.OnPhotonSerializeView(repeated, packetInfo); }
            allocations.Stop();
            Check(allocations.Count == 0, "Rejected pose packets allocate nothing after warmup");
        }
        pose.enabled = true; pose.ResetNetworkState();
        Receive(stale); pose.Update();
        Check(item.transform.position == start && (bool)Get(pose, "m_firstTake"),
            "Old controller cannot seed interpolation after enable");
        Mine(view, true); Receive(current);
        Check(item.transform.position == start, "Incoming pose cannot move newly owned item");
        Mine(view, false); Receive(current);
        Check(item.transform.position == Vector3.one * 90 && !(bool)Get(pose, "m_firstTake"),
            "Current controller's first valid pose applies immediately");

        var inventoryGo = new GameObject("edge inventory");
        var inventory = inventoryGo.AddComponent<BagInventory>();
        var bagGo = new GameObject("edge bag");
        var bag = bagGo.AddComponent<BagObject>();
        var slot = bagGo.AddComponent<BagSlot>();
        Set(slot, "itemKey", "occupied");
        view.OwnerActorNr = current.ActorNumber; Mine(view, true);
        Check(hand.ForceGrab(item), "Grab before failed insertion");
        var put = typeof(BagInventory).GetMethod("Put", P);
        bool inserted = (bool)put.Invoke(inventory, new object[] { bag, slot, hand });
        Check(!inserted && hand.Held == item, "Rejected insertion reports failure without destroying held item");
        // Exercise the real grip handler with an item invalidated before commit.
        Set(slot, "itemKey", null);
        ((System.Collections.Generic.List<BagSlot>)Get(bag, "slots")).Add(slot);
        var prefabs = (System.Collections.Generic.Dictionary<string, GameObject>)typeof(BagObject)
            .GetField("prefabCache", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        prefabs[item.name] = item.gameObject;
        BagObject.Active.Add(bag);
        item.gameObject.SetActive(false); // Edit-mode fixture keeps its hand reference until explicit cleanup.
        try { inserted = inventory.OnGripReleased(hand); }
        finally { BagObject.Active.Remove(bag); prefabs.Remove(item.name); }
        Check(!inserted && slot.IsEmpty, "Grip handler propagates insertion failure without consuming release");
        if (!inserted) hand.Release(); // The same fallback used by PlayerHand on grip release.
        Check(hand.Held == null && !item.IsHeld && !item.GetComponent<Rigidbody>().isKinematic,
            "Failed insertion falls back to immediate release");

        var oldGo = new GameObject("old goggles");
        var newGo = new GameObject("new goggles");
        var oldGoggles = oldGo.AddComponent<NightVisionGoggles>();
        var newGoggles = newGo.AddComponent<NightVisionGoggles>();
        var hud = newGo.AddComponent<PlayerHUD>();
        hud.SetNightVisionEffect(true);
        Set(oldGoggles, "activeHUD", hud); Set(newGoggles, "activeHUD", hud);
        var active = typeof(NightVisionGoggles).GetField("localEquipped", BindingFlags.Static | BindingFlags.NonPublic);
        object previous = active.GetValue(null);
        float fog = RenderSettings.fogDensity;
        try
        {
            Set(oldGoggles, "effectActive", true); Set(newGoggles, "effectActive", true);
            active.SetValue(null, newGoggles); RenderSettings.fogDensity = .025f;
            Call(oldGoggles, "Deactivate"); Call(oldGoggles, "Deactivate");
            Check(RenderSettings.fogDensity == .025f && ReferenceEquals(active.GetValue(null), newGoggles) && (bool)Get(hud, "nightVisionActive"),
                "Old goggles cannot reset replacement's fog or active reference");
            Call(newGoggles, "Deactivate");
            Check(RenderSettings.fogDensity == .12f && active.GetValue(null) == null && !(bool)Get(hud, "nightVisionActive"),
                "Current goggles still restore normal fog");
        }
        finally
        {
            Object.DestroyImmediate(oldGo); Object.DestroyImmediate(newGo);
            active.SetValue(null, previous); RenderSettings.fogDensity = fog;
            Object.DestroyImmediate(item.gameObject); Object.DestroyImmediate(handGo);
            Object.DestroyImmediate(inventoryGo); Object.DestroyImmediate(bagGo);
        }
        Debug.Log("Contention edges PASS: transfer ordering, real pose packets, failed insertion release, replacement goggles");
    }

    static void QueuedDropUpdates() => QueuedDropUpdates(false);

    static void QueuedDropUpdates(bool equipment)
    {
        var item = (PhotonGrabbable)Create(typeof(PhotonGrabbable), true);
        var view = item.GetComponent<PhotonView>();
        var pose = item.GetComponent<PhotonTransformView>();
        var current = PhotonNetwork.LocalPlayer;
        var stale = (Photon.Realtime.Player)Activator.CreateInstance(typeof(Photon.Realtime.Player),
            BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { "stale", 77, false }, null);
        typeof(PhotonView).GetProperty("Controller").SetValue(view, current);
        Mine(view, false);
        pose.m_SynchronizeScale = false;
        view.ObservedComponents = new System.Collections.Generic.List<Component> { item, pose };
        HeadEquipment head = null;
        if (equipment)
        {
            head = item.gameObject.AddComponent<HeadEquipment>();
            Call(head, "Awake");
            Set(item, "headEquipment", head);
            Set(head, "equipped", true); Set(head, "remoteEquipped", true);
            view.ObservedComponents.Insert(1, head);
        }
        int errors = 0;
        void Log(string message, string trace, LogType type)
        { if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception) errors++; }
        Application.logMessageReceived += Log;
        try
        {
            // Match prefab observation order, without any LateUpdate between packets.
            void Receive(Photon.Realtime.Player sender, Vector3 position, bool held = false, bool worn = false)
            {
                var fields = new System.Collections.Generic.List<object> {
                    0, held, 0, false, 1, true, 1000d, false, false };
                if (held) { fields.Add(Vector3.zero); fields.Add(Quaternion.identity); }
                if (equipment) { fields.Add(worn); fields.Add(0); }
                fields.Add(position); fields.Add(Vector3.zero); fields.Add(Quaternion.identity); fields.Add(1234);
                var stream = new PhotonStream(false, fields.ToArray());
                var info = new PhotonMessageInfo(sender, 0, view);
                view.DeserializeView(stream, info);
                Check((int)stream.ReceiveNext() == 1234, "Queued drop consumes exact packet fields");
            }
            // Worn equipment drops without an intermediate held packet (death
            // or a grab/throw completed between serialization ticks).
            Set(item, "remoteHeld", !equipment); pose.enabled = false; pose.ResetNetworkState();
            Vector3 start = item.transform.position;
            Receive(stale, Vector3.one * 90);
            Check(!pose.enabled && item.transform.position == start &&
                (equipment ? head.IsEquipped : item.IsHeldByAnyPlayer),
                "Stale drop cannot enable transform sync or clear held state");
            Set(item, "pickupPending", true);
            Receive(current, Vector3.one * 90);
            Check(!pose.enabled && item.transform.position == start &&
                (equipment ? head.IsEquipped : item.IsHeldByAnyPlayer),
                "Pending local pickup still rejects drop packets");
            Set(item, "pickupPending", false);
            PhotonGrabbable.ResetProfiling();
            Receive(current, Vector3.right);
            Check(pose.enabled && !item.IsHeldByAnyPlayer && item.transform.position == Vector3.right,
                "First queued drop accepts its pose without waiting for LateUpdate");
            Check(!equipment || !head.IsEquipped, "Equipment drop clears worn state immediately");
            Receive(current, Vector3.right * 2);
            Receive(current, Vector3.right * 3);
            Check((Vector3)Get(pose, "m_NetworkPosition") == Vector3.right * 3,
                "Final stationary packet survives queued delivery");
            Check(PhotonGrabbable.SyncWrites == 1, "Queued drops toggle sync only once");
            // No more network packets: interpolation must still reach the final target.
            Call(item, "UpdateItemPose");
            Check(!(bool)Get(pose, "m_firstTake") && (float)Get(pose, "m_Distance") > 0f &&
                (Vector3)Get(pose, "m_NetworkPosition") == Vector3.right * 3,
                "LateUpdate preserves the queued final interpolation target");
            // Edit-mode batches have no frame delta. Also run in play mode to
            // exercise actual interpolation without introducing another packet.
            if (Application.isPlaying)
            {
                Check(Time.deltaTime > 0f, "Play-mode interpolation has advancing frame time");
                for (int i = 0; i < 10000; i++) pose.Update();
                Check(item.transform.position == Vector3.right * 3,
                    "Remote drop reaches final position without another network packet");
            }
            Check(PhotonGrabbable.SyncWrites == 1 && errors == 0,
                "Queued drop has no redundant sync writes or warning/error spam");
            if (equipment)
            {
                object[] stationary = { 0, false, 0, false, 1, true, 1000d, false, false, false, 0,
                    Vector3.right * 3, Vector3.zero, Quaternion.identity };
                var repeated = new PhotonStream(false, stationary);
                var info = new PhotonMessageInfo(current, 0, view);
                for (int i = 0; i < 100; i++)
                { repeated.SetReadStream(stationary, 0); view.DeserializeView(repeated, info); }
                using (var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 10000,
                    ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread))
                {
                    Check(allocations.Valid, "Equipment packet allocation recorder available");
                    for (int i = 0; i < 10000; i++)
                    { repeated.SetReadStream(stationary, 0); view.DeserializeView(repeated, info); }
                    allocations.Stop();
                    Check(allocations.Count == 0 && PhotonGrabbable.SyncWrites == 1,
                        "Repeated equipment packets allocate nothing and do not toggle sync");
                }
                Set(item, "remoteHeld", true); pose.enabled = false;
                PhotonGrabbable.ResetProfiling();
                Vector3 beforeEquip = item.transform.position;
                Receive(current, Vector3.one * 90, worn: true);
                Check(head.IsEquipped && !pose.enabled && item.transform.position == beforeEquip &&
                    PhotonGrabbable.SyncWrites == 0, "Held-to-worn packet never briefly enables transform sync");
                Receive(current, Vector3.one * 90, held: true);
                Check(!head.IsEquipped && item.IsHeldByAnyPlayer && !pose.enabled &&
                    PhotonGrabbable.SyncWrites == 0, "Worn-to-held packet keeps hand pose authoritative");
                Receive(current, Vector3.right * 4);
                Check(pose.enabled && PhotonGrabbable.SyncWrites == 1 && errors == 0,
                    "Equipment hand release enables sync once without warning/error spam");
            }
        }
        finally
        {
            Application.logMessageReceived -= Log;
            Object.DestroyImmediate(item.gameObject);
        }
        Debug.Log((equipment ? "Equipment drop" : "Queued drop") +
            " PASS: stale/pending guards, final pose, one sync write, no warning/error spam");
    }

    static void PrefabObservationOrder()
    {
        int checkedItems = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var item = prefab != null ? prefab.GetComponent<PhotonGrabbable>() : null;
            if (item == null) continue;
            var view = item.GetComponent<PhotonView>();
            var pose = item.GetComponent<PhotonTransformView>();
            if (pose == null) continue;
            int itemIndex, poseIndex;
            if (view.observableSearch == PhotonView.ObservableSearch.Manual)
            {
                itemIndex = view.ObservedComponents.IndexOf(item);
                poseIndex = view.ObservedComponents.IndexOf(pose);
            }
            else
            {
                // Auto-discovery follows component order on these item roots.
                var components = prefab.GetComponents<Component>();
                itemIndex = Array.IndexOf(components, item);
                poseIndex = Array.IndexOf(components, pose);
            }
            Check(itemIndex >= 0 && poseIndex > itemIndex,
                path + ": held state must be read before the accompanying transform");
            var head = item.GetComponent<HeadEquipment>();
            if (head != null)
            {
                int headIndex = view.observableSearch == PhotonView.ObservableSearch.Manual
                    ? view.ObservedComponents.IndexOf(head)
                    : Array.IndexOf(prefab.GetComponents<Component>(), head);
                Check(headIndex > itemIndex && headIndex < poseIndex,
                    path + ": equipped state must be read between held state and transform");
            }
            checkedItems++;
        }
        Check(checkedItems > 0, "Network item prefabs available for observation-order validation");
        Debug.Log("Item observation order PASS: " + checkedItems + " prefabs");
    }

    static void Benchmark()
    {
        var baseline = typeof(PhotonGrabbable).Assembly.GetType("PhotonGrabbableBaseline");
        if (baseline == null) { Debug.Log("Baseline snapshot unavailable; behavior checks still run. Supply isolated pre-change class for comparative benchmarks."); return; }
        foreach (bool missing in new[] { false, true }) {
            MeasureBatch(baseline, missing, false); MeasureBatch(typeof(PhotonGrabbable), missing, false);
        }
        MeasureBatch(baseline, false, true); MeasureBatch(typeof(PhotonGrabbable), false, true);
    }
    static void MeasureBatch(Type type, bool missing, bool moving)
    {
        const int count = 100, frames = 1000;
        var items = new Grabbable[count]; var ticks = new Action[count];
        for (int i = 0; i < count; i++) { items[i] = Create(type, !missing); ticks[i] = Bind(items[i], "LateUpdate"); ticks[i](); }
        for (int warm = 0; warm < 10; warm++) for (int i = 0; i < count; i++) ticks[i]();
        foreach (string name in new[] { "SamplePoints", "PhysicsWrites", "SyncWrites", "FallbackSearches" }) type.GetField(name).SetValue(null, 0L);
        using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC.Alloc", 1000000,
            ProfilerRecorderOptions.StartImmediately | ProfilerRecorderOptions.CollectOnlyOnCurrentThread);
        Check(gc.Valid, "GC recorder missing");
        long start = Stopwatch.GetTimestamp();
        for (int f = 0; f < frames; f++) for (int i = 0; i < count; i++) {
            if (moving) items[i].transform.position += Vector3.right * .001f;
            ticks[i]();
        }
        double ms = (Stopwatch.GetTimestamp() - start) * 1000d / Stopwatch.Frequency / frames;
        gc.Stop(); Check(gc.Count < gc.Capacity, "Allocation capture overflow");
        long points = (long)type.GetField("SamplePoints").GetValue(null);
        long writes = (long)type.GetField("PhysicsWrites").GetValue(null) + (long)type.GetField("SyncWrites").GetValue(null);
        Debug.Log($"ITEM BENCH {type.Name} {(missing ? "no collider" : moving ? "moving" : "idle")}: {ms:F3} ms / batch of 100; {points / frames} sample points, {writes / frames} writes, {(double)gc.Count / frames:F1} GC events per batch");
        if (type == typeof(PhotonGrabbable)) {
            Check(writes == 0, "No repeated settled state writes");
            Check(gc.Count == 0, "Warmed sampling should not allocate");
            if (!moving) Check(points == 0, "No idle point transforms"); else Check(points > 0, "Moving points not skipped");
        }
        foreach (var item in items) Object.DestroyImmediate(item.gameObject);
    }
}

