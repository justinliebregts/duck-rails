using System;
using System.Reflection;
using Photon.Pun;
using UnityEngine;

// Isolated batch fixture, real offline Photon RPC dispatch; no microphone needed.
public static class DuckMuteRangeChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object target, string name, object value) => target.GetType().GetField(name, Private).SetValue(target, value);
    static double Read(object target, string name) => (double)target.GetType().GetField(name, Private).GetValue(target);
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Debug.Log("MUTE RANGE PASS: " + label); }
    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Isolated batch fixture only");
        if (PhotonNetwork.PhotonServerSettings == null)
        {
            if (!UnityEditor.AssetDatabase.IsValidFolder("Assets/Resources"))
                UnityEditor.AssetDatabase.CreateFolder("Assets", "Resources");
            UnityEditor.AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<ServerSettings>(),
                "Assets/Resources/PhotonServerSettings.asset");
        }
        // Batch edit-mode does not run PUN's play-mode shortcut initialization.
        typeof(PhotonNetwork).GetField("rpcShortcuts", BindingFlags.Static | BindingFlags.NonPublic)
            .SetValue(null, new System.Collections.Generic.Dictionary<string, int>());
        DuckSpawnChecks.Run();
        var human = PhotonPlayer.Local;
        DuckCompanion duck = null;
        foreach (var player in PhotonPlayer.Active) if (player != null && player.IsCompanion) duck = player.Companion;
        var head = new GameObject("Test Duck head").transform;
        typeof(DuckCompanion).GetProperty("Head").SetValue(duck, head);
        var humanHead = new GameObject("Test human head").transform;
        human.BindSources(humanHead, humanHead, humanHead);
        var mute = duck.gameObject.AddComponent<DuckCompanionSelfMute>();
        Set(mute, "duck", duck);
        var voice = duck.gameObject.AddComponent<AudioSource>(); Set(mute, "voice", voice);
        var humanVoice = human.gameObject.AddComponent<AudioSource>(); humanVoice.mute = true;
        // RegisterPhotonView deliberately refuses edit-mode registration.
        var views = typeof(PhotonNetwork).GetField("photonViewList", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null);
        views.GetType().GetMethod("Add", new[] { typeof(int), typeof(PhotonView) })
            .Invoke(views, new object[] { duck.photonView.ViewID, duck.photonView });
        duck.photonView.RefreshRpcMonoBehaviourCache();
        var range = (Func<PhotonPlayer, bool>)Delegate.CreateDelegate(typeof(Func<PhotonPlayer, bool>), mute,
            typeof(DuckCompanionSelfMute).GetMethod("WithinReactionRange", Private));
        Check(!range(null), "missing avatar is rejected");
        Check(!range(duck.GetComponent<PhotonPlayer>()), "companion cannot be the requester");
        humanHead.position = Vector3.right * 10;
        Check(range(human), "exactly 10 metres is accepted");
        humanHead.position = Vector3.up * 10.01f;
        Check(!range(human), "vertical separation counts; beyond 10 metres rejected");
        voice.mute = true; mute.SetMuted(true);
        Check(!voice.mute && humanVoice.mute, "far click still unmutes Duck only");
        Check(Read(mute, "nextLocalRequest") == 0 && Read(mute, "pendingReactionUntil") < 0,
            "far click sends no request and queues no complaint");
        var request = typeof(DuckCompanionSelfMute).GetMethod("RequestRoomUnmute", Private);
        var info = new PhotonMessageInfo(PhotonNetwork.LocalPlayer, 0, duck.photonView);
        request.Invoke(mute, new object[] { info });
        Check(Read(mute, "nextReactionAt") == 0 && Read(mute, "pendingReactionUntil") < 0,
            "authority independently rejects a distant request");
        humanHead.position = Vector3.right * 2;
        mute.SetMuted(true);
        double pending = Read(mute, "pendingReactionUntil"), cooldown = Read(mute, "nextReactionAt");
        Check(pending >= PhotonNetwork.Time && cooldown >= PhotonNetwork.Time + 29,
            "near click dispatches RPC and queues one reaction with existing cooldown");
        for (int i = 0; i < 100; i++) mute.SetMuted(true);
        Check(Read(mute, "pendingReactionUntil") == pending && Read(mute, "nextReactionAt") == cooldown,
            "spam does not enqueue or restart the reaction");
        range(human); long before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; i++) range(human);
        Check(GC.GetAllocatedBytesForCurrentThread() == before, "10000 range checks allocate zero managed bytes");
        CheckFixedCooldown(mute);
        Debug.Log("ALL DUCK MUTE RANGE CHECKS PASSED (offline Photon)");
    }

    static void CheckFixedCooldown(DuckCompanionSelfMute mute)
    {
        var queue = (Func<double, bool>)Delegate.CreateDelegate(typeof(Func<double, bool>), mute,
            typeof(DuckCompanionSelfMute).GetMethod("TryQueueReaction", Private));
        Set(mute, "nextReactionAt", 0d); Set(mute, "pendingReactionUntil", -1d);
        Check(queue(100), "first accepted click starts fixed cooldown");
        Check(Read(mute, "nextReactionAt") == 130 && Read(mute, "pendingReactionUntil") == 115,
            "30 second deadline and 15 second pending expiry start at accepted click");
        for (int second = 101; second < 130; second++)
            Check(!queue(second) && Read(mute, "nextReactionAt") == 130,
                "authority-level repeated request cannot extend deadline at " + second);
        Check(queue(130) && Read(mute, "nextReactionAt") == 160,
            "same player can trigger another reaction exactly at 30 seconds, even after spam");

        // Simulate delivery after waiting for another sentence: clearing pending
        // work is all delivery does; it must not establish another cooldown.
        Set(mute, "pendingReactionUntil", -1d);
        Check(!queue(145) && Read(mute, "nextReactionAt") == 160,
            "delayed delivery leaves the accepted-click deadline intact");

        var replicaObject = new GameObject("Mute takeover replica");
        replicaObject.SetActive(false);
        var replica = replicaObject.AddComponent<DuckCompanionSelfMute>();
        try
        {
            var outgoing = WriteStream(); mute.Serialize(outgoing);
            replica.Serialize(new PhotonStream(false, outgoing.ToArray()));
            Check(Read(replica, "nextReactionAt") == 160,
                "serialization transfers the original absolute deadline without restarting it");
            var takeoverQueue = (Func<double, bool>)Delegate.CreateDelegate(typeof(Func<double, bool>), replica,
                typeof(DuckCompanionSelfMute).GetMethod("TryQueueReaction", Private));
            Check(!takeoverQueue(159) && takeoverQueue(160),
                "new authority waits only the remaining time and accepts next eligible click");

            Set(mute, "nextReactionAt", 0d); Set(mute, "pendingReactionUntil", -1d);
            queue(200);
            outgoing = WriteStream(); mute.Serialize(outgoing);
            replica.Serialize(new PhotonStream(false, outgoing.ToArray()));
            Check(Read(replica, "pendingReactionUntil") == 215 && !takeoverQueue(220)
                && Read(replica, "nextReactionAt") == 230 && takeoverQueue(230),
                "expired pending complaint after takeover cannot block future reactions");
        }
        finally { UnityEngine.Object.DestroyImmediate(replicaObject); }
    }

    static PhotonStream WriteStream()
    {
        var stream = new PhotonStream(true, null);
        typeof(PhotonStream).GetMethod("SetWriteStream", Private).Invoke(stream,
            new object[] { new System.Collections.Generic.List<object>(), 0 });
        return stream;
    }
}
