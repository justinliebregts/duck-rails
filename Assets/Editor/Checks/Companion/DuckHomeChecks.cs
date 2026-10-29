using System;
using System.Reflection;
using Photon.Pun;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Run in an isolated batch editor; exercises production movement/state and stream handlers.</summary>
public static class DuckHomeChecks
{
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Set(object o, string n, object v) => o.GetType().GetField(n, Private).SetValue(o, v);
    static object Get(object o, string n) => o.GetType().GetField(n, Private).GetValue(o);
    static object Call(object o, string n, params object[] args) => o.GetType().GetMethod(n, Private).Invoke(o, args);
    static void Check(bool value, string label) { if (!value) throw new Exception(label); Debug.Log("PASS: " + label); }
    static void Mode(DuckCompanion d, DuckCompanion.CompanionMode mode)
        => typeof(DuckCompanion).GetProperty("Mode").SetValue(d, mode);

    static DuckCompanion Create(string name)
    {
        var go = new GameObject(name);
        go.AddComponent<PhotonView>();
        var duck = go.AddComponent<DuckCompanion>();
        Call(duck, "Awake");
        Call(duck.Dialogue, "Awake");
        Call(duck.SelfMute, "Awake");
        Set(duck, "environment", (LayerMask)(1 << 6));
        Set(duck, "homeInitialized", true);
        Set(duck, "homePosition", new Vector3(0, 0.04f, 0));
        Set(duck, "homeRotation", Quaternion.identity);
        Set(duck, "greeted", true); // No TTS required for these movement checks.
        go.transform.position = new Vector3(0, 0.04f, 0);
        go.GetComponent<CharacterController>().enabled = true;
        return duck;
    }

    static bool Destination(DuckCompanion d, Vector3 candidate)
    {
        object[] args = { candidate, Vector3.zero };
        return (bool)Call(d, "TryHomeDestination", args);
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var floor = new GameObject("Subway floor"); floor.layer = 6;
        floor.transform.position = Vector3.down * 0.5f;
        floor.AddComponent<BoxCollider>().size = new Vector3(40, 1, 40);
        var duck = Create("Authority");
        var remote = Create("Remote");
        remote.transform.position = Vector3.right * 30;
        Physics.SyncTransforms();
        Check(duck.Mode == DuckCompanion.CompanionMode.HomeWandering && duck.TargetActor == 0,
            "default mode wanders at home without a player");
        Check(Destination(duck, new Vector3(5, 0.04f, 0)), "supported subway corridor is eligible");
        Check(!Destination(duck, new Vector3(11, 0.04f, 0)), "home radius rejects distant destinations");
        var wall = new GameObject("Subway wall"); wall.layer = 6; wall.transform.position = new Vector3(2, 1, 0);
        wall.AddComponent<BoxCollider>().size = new Vector3(0.3f, 2, 4); Physics.SyncTransforms();
        Check(!Destination(duck, new Vector3(5, 0.04f, 0)), "home wandering rejects paths through walls");
        object[] detourArgs = { new Vector3(5, 0.04f, 0), null };
        Check((bool)Call(duck, "TryHomeDetour", detourArgs), "home wandering finds a bounded route around a wall");
        foreach (Vector3 waypoint in (Vector3[])detourArgs[1])
            Check(Vector3.Distance(waypoint, duck.HomePosition) <= 10f, "alternate waypoint stays inside home radius");
        UnityEngine.Object.DestroyImmediate(wall);
        var street = new GameObject("Street overhead"); street.layer = 6; street.transform.position = Vector3.up * 3;
        street.AddComponent<BoxCollider>().size = new Vector3(20, 0.5f, 20); Physics.SyncTransforms();
        Check(Destination(duck, new Vector3(5, 0.04f, 0)), "overhead street does not steal the subway floor probe");
        Check(!Destination(duck, new Vector3(5, 3.3f, 0)), "upper level is not a home wander destination");
        Set(duck, "homePosition", new Vector3(0, 1.04f, 0));
        Set(duck, "homeFloorResolved", false);
        Check(Destination(duck, new Vector3(0, 0.04f, 3)), "elevated spawn uses supporting floor for wandering");
        Check(Mathf.Abs((float)Get(duck, "homeFloorY") - 0.04f) < 0.01f, "home floor height is separate from respawn marker");
        Set(duck, "homePosition", new Vector3(0, 0.04f, 0));
        Check(!duck.Dialogue.React("airborne", true), "airborne dialogue is disabled even for forced reactions");
        foreach (var mode in new[] { DuckCompanion.CompanionMode.HomeWandering,
            DuckCompanion.CompanionMode.Following, DuckCompanion.CompanionMode.ReturningHome,
            DuckCompanion.CompanionMode.GreetingVisitor })
        {
            Mode(duck, mode);
            Set(duck, "homeWalking", true);
            Set(duck, "homeDestination", Vector3.right * 5);
            Set(duck, "nextHomeWander", 123d);
            var write = new PhotonStream(true, null);
            Call(write, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
            duck.Serialize(write);
            remote.Serialize(new PhotonStream(false, write.ToArray()));
            Check(remote.Mode == mode && remote.HomePosition == duck.HomePosition
                && (float)Get(remote, "homeFloorY") == (float)Get(duck, "homeFloorY")
                && (bool)Get(remote, "homeFloorResolved") == (bool)Get(duck, "homeFloorResolved")
                && (Vector3)Get(remote, "homeDestination") == Vector3.right * 5
                && (double)Get(remote, "nextHomeWander") == 123d,
                "full Photon stream round trip preserves " + mode);
            Call(remote, "ResetNavigationAfterAuthorityChange");
            Check(remote.Mode == mode && (bool)Get(remote, "homeWalking"),
                "takeover resets local routes without losing " + mode);
        }
        foreach (string text in new[] { "go away duck", "stop follow", "stop following", "go back", "go home" })
            Check(DuckCompanionDialogue.Intent(text) == "dismiss"
                && DuckCompanionDialogue.AcceptsSpeech(text, false, false), "return command accepted: " + text);
        Check(DuckCompanionDialogue.Intent("be quiet") == "quiet", "quiet remains a separate command");
        Check(DuckCompanionDialogue.Intent("help me") != "follow", "generic help does not acquire a follower");
        Check(DuckCompanionDialogue.ConversationRange == 10f && DuckCompanionDialogue.BuddyConversationRange <= 10f,
            "recognizer and authoritative conversation limit is ten metres");
        Mode(duck, DuckCompanion.CompanionMode.ReturningHome);
        Check((bool)Call(duck, "ResetAtCompanionSpawn", true)
            && duck.Mode == DuckCompanion.CompanionMode.HomeWandering && duck.TargetActor == 0,
            "return recovery respawns at home and resumes home wandering");
        Mode(duck, DuckCompanion.CompanionMode.Following);
        typeof(DuckCompanion).GetProperty("TargetActor").SetValue(duck, 7);
        Call(duck, "ResetAtCompanionSpawn", true);
        Check(duck.Mode == DuckCompanion.CompanionMode.HomeWandering && duck.TargetActor == 0,
            "follow recovery clears the requested player and wanders at home");
        Check(Vector3.Distance(duck.transform.position, duck.HomePosition) < 0.001f,
            "recovery uses persistent home pose");

        // No transport or external room: supply an offline authority and invoke
        // the same production update/command handlers used by the room owner.
        const BindingFlags Static = BindingFlags.Static | BindingFlags.NonPublic;
        typeof(PhotonNetwork).GetField("offlineMode", Static).SetValue(null, true);
        typeof(PhotonNetwork).GetField("offlineModeRoom", Static).SetValue(null,
            new Photon.Realtime.Room("Home fixture", new Photon.Realtime.RoomOptions(), true));
        // Edit-mode batch execution does not advance PUN's cached frame clock.
        typeof(PhotonNetwork).GetField("frametime", Static).SetValue(null, 100d);
        typeof(PhotonView).GetProperty("IsMine").SetValue(duck.photonView, true);
        Set(duck, "wasAuthority", true);
        Set(duck, "nextKillerCheck", double.MaxValue);
        var humanObject = new GameObject("Requested player");
        humanObject.AddComponent<PhotonView>();
        var human = humanObject.AddComponent<PhotonPlayer>();
        human.photonView.OwnerActorNr = 7;
        if (!PhotonPlayer.Active.Contains(human)) PhotonPlayer.Active.Add(human);
        duck.Follow(7);
        Check(duck.Mode == DuckCompanion.CompanionMode.Following && duck.TargetActor == 7,
            "explicit follow command acquires the requesting player");
        Set(human, "_remoteDead", true);
        duck.transform.position = new Vector3(6, 0.04f, 0);
        Call(duck, "Update");
        Check(duck.Mode == DuckCompanion.CompanionMode.ReturningHome && duck.TargetActor == 0,
            "target death returns home without acquiring another player");
        Set(human, "_remoteDead", false);
        duck.Follow(7);
        Check(duck.Mode == DuckCompanion.CompanionMode.Following, "follow interrupts a return home");
        PhotonPlayer.Active.RemoveAll(p => p == human);
        duck.transform.position = new Vector3(6, 0.04f, 0);
        Call(duck, "Update");
        Check(duck.Mode == DuckCompanion.CompanionMode.ReturningHome && duck.TargetActor == 0,
            "target departure returns home");
        Mode(duck, DuckCompanion.CompanionMode.Following);
        var phase = typeof(DuckCompanion).GetField("killerPhase", Private);
        phase.SetValue(duck, Enum.Parse(phase.FieldType, "Warning"));
        Call(duck, "CaptureKillerDeathAppearance");
        Call(duck, "CancelKillerAttack");
        Check(duck.KillerEyesAtDeath && !duck.KillerSequenceActive,
            "death preserves killer eyes after attack cancellation");
        var deathWrite = new PhotonStream(true, null);
        Call(deathWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        Call(duck, "SerializeKiller", deathWrite);
        Call(remote, "SerializeKiller", new PhotonStream(false, deathWrite.ToArray()));
        Check(remote.KillerEyesAtDeath && !remote.KillerSequenceActive,
            "late receiver gets red corpse eyes without an active attack");
        Call(remote, "ResetNavigationAfterAuthorityChange");
        Check(remote.KillerEyesAtDeath, "authority handoff retains death appearance");
        Call(duck, "ResetAtCompanionSpawn", false);
        Check(!duck.KillerEyesAtDeath, "respawn clears killer death appearance");
        deathWrite = new PhotonStream(true, null);
        Call(deathWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        Call(duck, "SerializeKiller", deathWrite);
        Call(remote, "SerializeKiller", new PhotonStream(false, deathWrite.ToArray()));
        Check(!remote.KillerEyesAtDeath, "respawn clears remote death appearance");
        Call(duck, "CaptureKillerDeathAppearance");
        Check(!duck.KillerEyesAtDeath, "ordinary death retains normal eyes");
        phase.SetValue(duck, Enum.Parse(phase.FieldType, "Aftermath"));
        Call(duck, "TickKillerAttack");
        Check(duck.Mode == DuckCompanion.CompanionMode.ReturningHome && duck.TargetActor == 0,
            "completed post-kill speech returns home instead of acquiring a victim");
        Set(duck, "movementRequested", true);
        Set(duck, "recoveryWasMoving", true);
        Set(duck, "groundSupported", true);
        Set(duck, "recoveryAnchor", duck.transform.position);
        Set(duck, "lastProgressAt", PhotonNetwork.Time - 21);
        Call(duck, "TickRecovery", new object[] { null });
        Check(duck.Mode == DuckCompanion.CompanionMode.HomeWandering
            && Vector3.Distance(duck.transform.position, duck.HomePosition) < 0.001f,
            "blocked return with no player still triggers the twenty-second failsafe");
        Mode(duck, DuckCompanion.CompanionMode.ReturningHome);
        typeof(PhotonView).GetProperty("IsMine").SetValue(duck.photonView, false);
        duck.Follow(7);
        Check(duck.Mode == DuckCompanion.CompanionMode.ReturningHome, "remote peers cannot change follow decisions");
        typeof(PhotonView).GetProperty("IsMine").SetValue(duck.photonView, true);
        Set(duck, "wasAuthority", false);
        Call(duck, "Update");
        Check(duck.GetComponent<CharacterController>().enabled, "authority takeover re-enables movement");
        Mode(duck, DuckCompanion.CompanionMode.HomeWandering);
        duck.transform.position = duck.HomePosition + Vector3.right * 2;
        Set(duck, "homeWalking", true);
        Set(duck, "movementRequested", true);
        Set(duck, "recoveryWasMoving", true);
        Set(duck, "groundSupported", true);
        Set(duck, "recoveryAnchor", duck.transform.position);
        Set(duck, "lastProgressAt", PhotonNetwork.Time - 21);
        Call(duck, "TickRecovery", new object[] { null });
        Check(Vector3.Distance(duck.transform.position, duck.HomePosition) < 0.001f
            && !(bool)Get(duck, "homeWalking"), "blocked home wandering also recovers without a player");
        Set(duck, "movementRequested", false);
        Set(duck, "groundSupported", true);
        Set(duck, "lastProgressAt", PhotonNetwork.Time - 21);
        Call(duck, "TickRecovery", new object[] { null });
        Check(PhotonNetwork.Time - (double)Get(duck, "lastProgressAt") < 1,
            "supported idle at home is not counted as stuck");
        PhotonPlayer.Active.Add(human);
        typeof(PhotonView).GetProperty("IsMine").SetValue(human.photonView, true);
        var head = new GameObject("Visitor head").transform;
        head.position = duck.HomePosition + new Vector3(6, 0.5f, 0);
        human.BindSources(head, head, head);
        Check(duck.HearVisitor(7, true) && duck.Mode == DuckCompanion.CompanionMode.GreetingVisitor
            && duck.TargetActor == 0, "hearing speech starts a temporary visit, not following");
        double deadline = (double)Get(duck, "visitorDeadline");
        Check(duck.HearVisitor(7, false) && (double)Get(duck, "visitorDeadline") == deadline
            && !(bool)Get(duck, "visitorGreetingPending"), "question suppresses hello without restarting approach");
        Set(duck, "visitorLastProgress", PhotonNetwork.Time - 3.1);
        Check((bool)Call(duck, "VisitorExpired", 6f), "three seconds without progress ends approach");
        Call(duck, "TickVisitor");
        Check(duck.Mode != DuckCompanion.CompanionMode.GreetingVisitor, "blocked visitor approach returns home");
        Check(!duck.HearVisitor(7, true), "same visitor cannot immediately restart a failed approach");
        Set(duck, "visitorLastProgress", PhotonNetwork.Time);
        Set(duck, "visitorDeadline", PhotonNetwork.Time - 0.1);
        Set(duck, "visitorArrived", false);
        Check((bool)Call(duck, "VisitorExpired", 6f), "ten-second deadline applies even while making progress");
        Set(duck, "visitorArrived", true);
        Set(duck, "visitorLastSpeech", PhotonNetwork.Time);
        Check((bool)Call(duck, "VisitorExpired", 5f), "visitor walking away ends engagement");
        Check(!(bool)Call(duck, "VisitorExpired", 2f), "nearby visitor can stay engaged");
        Set(duck, "visitorLastSpeech", PhotonNetwork.Time - 16);
        Check((bool)Call(duck, "VisitorExpired", 2f), "engagement ends after fifteen seconds without speech");
        Mode(duck, DuckCompanion.CompanionMode.GreetingVisitor);
        Set(duck, "visitorActor", 7);
        var visitorWrite = new PhotonStream(true, null);
        Call(visitorWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        duck.Serialize(visitorWrite);
        remote.Serialize(new PhotonStream(false, visitorWrite.ToArray()));
        Call(remote, "ResetNavigationAfterAuthorityChange");
        Check((int)Get(remote, "visitorActor") == 7
            && (double)Get(remote, "visitorDeadline") == (double)Get(duck, "visitorDeadline")
            && ((int[])Get(remote, "greetedVisitors"))[0] == 7
            && ((double[])Get(remote, "visitorRetryTimes"))[0] == ((double[])Get(duck, "visitorRetryTimes"))[0],
            "visitor, deadlines and retry cooldown survive full stream and takeover reset");
        Set(duck, "visitorArrived", false);
        Set(duck, "visitorDeadline", PhotonNetwork.Time + 10);
        Set(duck, "visitorLastProgress", PhotonNetwork.Time);
        Call(duck.Dialogue, "QueueGreeting", 7);
        head.position = duck.HomePosition + new Vector3(9.5f, 0.5f, 0);
        Check(!(bool)Call(duck, "VisitorExpired", 9.5f),
            "moving within hearing range does not end an unexpired greeting approach");
        Set(duck, "visitorDeadline", PhotonNetwork.Time - 0.1);
        Call(duck, "TickVisitor");
        Check(duck.Mode != DuckCompanion.CompanionMode.GreetingVisitor && duck.TargetActor == 0
            && (int)Get(duck.Dialogue, "pendingGreetingActor") == 0,
            "approach timeout cancels queued greeting and returns home without following");
        head.position = duck.HomePosition + new Vector3(6, 0.5f, 0);
        ((System.Collections.Generic.Dictionary<int, double>)Get(duck.Dialogue, "greetingReplyTimes")).Clear();
        duck.Follow(7);
        Check(duck.Mode == DuckCompanion.CompanionMode.Following && duck.TargetActor == 7,
            "explicit follow replaces temporary greeting");
        typeof(DuckCompanion).GetProperty("TargetActor").SetValue(duck, 99);
        Set(duck.Dialogue, "preparing", true);
        int greetingSequence = duck.Dialogue.SpeechSequence;
        Call(duck.Dialogue, "QueueGreeting", 7);
        Call(duck.Dialogue, "QueueGreeting", 7);
        Check((int)Get(duck.Dialogue, "pendingGreetingActor") == 7
            && duck.Dialogue.SpeechSequence == greetingSequence && duck.TargetActor == 99,
            "repeated hello queues one reply without interrupting speech or changing follow target");
        var greetingTimes = (System.Collections.Generic.Dictionary<int, double>)Get(duck.Dialogue, "greetingReplyTimes");
        Check(greetingTimes[7] == PhotonNetwork.Time + 5, "hello has a five-second per-player cooldown");
        Set(duck.Dialogue, "pendingGreetingActor", 0);
        Call(duck.Dialogue, "QueueGreeting", 7);
        Check((int)Get(duck.Dialogue, "pendingGreetingActor") == 0, "hello inside cooldown does not enqueue another reply");
        greetingTimes[7] = PhotonNetwork.Time - 1;
        Call(duck.Dialogue, "QueueGreeting", 7);
        Check((int)Get(duck.Dialogue, "pendingGreetingActor") == 7, "hello accepted after cooldown");
        Set(duck.Dialogue, "preparing", false);
        Call(duck.Dialogue, "TickGreetingReply");
        Check((int)Get(duck.Dialogue, "pendingGreetingActor") == 7
            && duck.Dialogue.SpeechSequence == greetingSequence,
            "hello waits for arrival instead of speaking from a distance");
        Vector3 arrivalHead = head.position;
        Set(duck, "groundSupported", true);
        head.position = duck.transform.position + new Vector3(1, 0.5f, 0);
        Check(duck.ReadyToGreet(human), "supported arrival near listener permits greeting");
        Set(duck, "groundSupported", false);
        Check(!duck.ReadyToGreet(human), "airborne approach does not start greeting");
        Set(duck, "groundSupported", true);
        head.position = arrivalHead;
        var greetingWrite = new PhotonStream(true, null);
        Call(greetingWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        duck.Serialize(greetingWrite);
        remote.Serialize(new PhotonStream(false, greetingWrite.ToArray()));
        Check((int)Get(remote.Dialogue, "pendingGreetingActor") == 7
            && (double)Get(remote.Dialogue, "pendingGreetingDeadline") == (double)Get(duck.Dialogue, "pendingGreetingDeadline"),
            "pending approach greeting is synchronized for authority takeover");
        Set(duck.Dialogue, "preparing", true);
        Vector3 greetingPosition = head.position;
        head.position += Vector3.right * 50;
        Call(duck.Dialogue, "TickGreetingReply");
        Check((int)Get(duck.Dialogue, "pendingGreetingActor") == 0, "queued hello discarded when listener leaves range");
        head.position = greetingPosition;
        Set(duck.Dialogue, "preparing", false);
        duck.AttendConversation(7);
        Check(duck.ConversationSpeaker == human && duck.TargetActor == 99,
            "conversation attention does not replace the followed player");
        Set(duck.Dialogue, "preparing", true);
        Call(duck, "TickConversationAttention");
        Check((double)Get(duck, "conversationLookUntil") == PhotonNetwork.Time + 3,
            "speech preparation/playback maintains attention plus three seconds");
        var conversationWrite = new PhotonStream(true, null);
        Call(conversationWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        duck.Serialize(conversationWrite);
        remote.Serialize(new PhotonStream(false, conversationWrite.ToArray()));
        Check(remote.ConversationSpeaker == human && remote.TargetActor == 99,
            "remote conversation attention is separate from following");
        Call(remote, "ResetNavigationAfterAuthorityChange");
        Check(remote.ConversationSpeaker == human, "conversation attention survives authority handoff");
        Set(duck.Dialogue, "preparing", false);
        Set(duck, "conversationLookUntil", PhotonNetwork.Time - 0.1);
        Call(duck, "TickConversationAttention");
        Check(duck.ConversationSpeaker == null, "conversation gaze expires after reply");
        duck.AttendConversation(7);
        var previousHead = head.position;
        head.position = duck.Head.position + Vector3.forward * 11;
        Check(duck.ConversationSpeaker == null, "out-of-range speaker loses attention");
        head.position = previousHead;
        var otherObject = new GameObject("Other conversation speaker");
        otherObject.AddComponent<PhotonView>();
        var other = otherObject.AddComponent<PhotonPlayer>();
        other.photonView.OwnerActorNr = 8;
        typeof(PhotonView).GetProperty("IsMine").SetValue(other.photonView, true);
        other.BindSources(head, head, head);
        PhotonPlayer.Active.Add(other);
        Set(duck.Dialogue, "preparing", true);
        duck.AttendConversation(8);
        Check(duck.ConversationSpeaker == human, "reply does not swivel to an interrupting speaker");
        Set(duck.Dialogue, "preparing", false);
        Call(duck, "TickConversationAttention");
        Check(duck.ConversationSpeaker == other && duck.TargetActor == 99,
            "latest accepted speaker receives attention after current reply");
        PhotonPlayer.Active.Remove(other);
        UnityEngine.Object.DestroyImmediate(otherObject);
        Call(duck, "TickConversationAttention");
        Check(duck.ConversationSpeaker == null, "departed conversation speaker is cleared");
        foreach (string command in new[] { "follow", "dismiss", "wait", "quiet" })
        foreach (bool refused in new[] { false, true })
        {
            duck.Follow(7);
            typeof(DuckCompanion).GetProperty("TargetActor").SetValue(duck, 99);
            Set(duck.Dialogue, "preparing", true);
            duck.QueueCommandResponse(command, 7, refused);
            if (command == "follow" && !refused)
            {
                Check(!duck.CommandResponsePending && duck.TargetActor == 7
                    && duck.Mode == DuckCompanion.CompanionMode.Following
                    && !(bool)Call(duck, "TickFarewell") && duck.Dialogue.Busy,
                    "accepted follow moves immediately while acknowledgement is preparing");
                var followWrite = new PhotonStream(true, null);
                Call(followWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
                duck.Serialize(followWrite);
                remote.Serialize(new PhotonStream(false, followWrite.ToArray()));
                Check(remote.TargetActor == 7 && !remote.CommandResponsePending
                    && remote.Mode == DuckCompanion.CompanionMode.Following,
                    "immediate accepted follow state reaches remote peers");
                Set(duck.Dialogue, "preparing", false);
                continue;
            }
            Vector3 heldPosition = duck.transform.position;
            Check(duck.CommandResponseSpeaker == human, command + " response tracks its speaker, refusal=" + refused);
            Check((bool)Call(duck, "TickFarewell")
                && Vector3.ProjectOnPlane(duck.transform.position - heldPosition, Vector3.up).sqrMagnitude < 0.001f,
                command + " response retains movement lock, refusal=" + refused);
            var responseWrite = new PhotonStream(true, null);
            Call(responseWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
            duck.Serialize(responseWrite);
            remote.Serialize(new PhotonStream(false, responseWrite.ToArray()));
            Check(remote.CommandResponseSpeaker == human && remote.State == DuckCompanion.Activity.Waiting,
                "remote peer receives the same command look target");
            Call(remote, "ResetNavigationAfterAuthorityChange");
            Check(remote.CommandResponseSpeaker == human, "command look target survives authority handoff");
            typeof(PhotonView).GetProperty("IsMine").SetValue(human.photonView, false);
            Set(human, "_remoteDead", true);
            Check(duck.CommandResponseSpeaker == null, "dead speaker is no longer tracked");
            Set(human, "_remoteDead", false);
            typeof(PhotonView).GetProperty("IsMine").SetValue(human.photonView, true);
            PhotonPlayer.Active.Remove(human);
            Check(duck.CommandResponseSpeaker == null, "departed speaker is no longer tracked");
            PhotonPlayer.Active.Add(human);
            Set(duck.Dialogue, "preparing", false);
            Call(duck, "TickFarewell");
            Check(duck.CommandResponseSpeaker == null, "response look target ends with speech gate");
        }
        duck.Follow(7);
        Set(duck.Dialogue, "preparing", true);
        duck.ReturnHomeAfterFarewell();
        Vector3 farewellPosition = duck.transform.position;
        Check(duck.WaitingForFarewell && (bool)Call(duck, "TickFarewell")
            && duck.State == DuckCompanion.Activity.Waiting
            && Vector3.ProjectOnPlane(duck.transform.position - farewellPosition, Vector3.up).sqrMagnitude < 0.001f,
            "dismissal holds horizontal movement while farewell prepares or plays");
        var farewellWrite = new PhotonStream(true, null);
        Call(farewellWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        duck.Serialize(farewellWrite);
        remote.Serialize(new PhotonStream(false, farewellWrite.ToArray()));
        Call(remote, "ResetNavigationAfterAuthorityChange");
        Check(remote.WaitingForFarewell
            && (double)Get(remote, "farewellDeadline") == (double)Get(duck, "farewellDeadline"),
            "farewell hold and deadline survive network transfer and takeover");
        Set(duck.Dialogue, "preparing", false);
        Check(!(bool)Call(duck, "TickFarewell") && !duck.WaitingForFarewell
            && duck.Mode == DuckCompanion.CompanionMode.ReturningHome, "finished farewell releases return-home movement");
        duck.ReturnHomeAfterFarewell();
        Set(duck.Dialogue, "preparing", true);
        Set(duck, "farewellDeadline", PhotonNetwork.Time - 1);
        Check(!(bool)Call(duck, "TickFarewell"), "failed or stalled audio cannot hold Duck forever");
        duck.ReturnHomeAfterFarewell();
        duck.Follow(7);
        Check(!duck.WaitingForFarewell && duck.Mode == DuckCompanion.CompanionMode.Following,
            "accepted follow request cancels pending departure");
        Set(duck.Dialogue, "preparing", false);
        duck.ReturnHome();
        Set(duck.Dialogue, "preparing", true);
        duck.QueueCommandResponse("follow", 7, true);
        Check((bool)Call(duck, "TickFarewell") && duck.Mode == DuckCompanion.CompanionMode.ReturningHome,
            "refused follow pauses the return journey without changing intent");
        double refusalDeadline = (double)Get(duck, "farewellDeadline");
        duck.QueueCommandResponse("follow", 7, true);
        Check((double)Get(duck, "farewellDeadline") == refusalDeadline, "repeated commands cannot extend response pause");
        Set(duck.Dialogue, "preparing", false);
        Call(duck, "TickFarewell");
        Check(!duck.CommandResponsePending && duck.Mode == DuckCompanion.CompanionMode.ReturningHome,
            "return journey resumes after refusal finishes");
        Set(duck.Dialogue, "preparing", true);
        duck.QueueCommandResponse("follow", 7, false);
        Check(duck.Mode == DuckCompanion.CompanionMode.Following && !duck.CommandResponsePending,
            "accepted follow immediately replaces return journey");
        Set(duck.Dialogue, "preparing", false);
        Call(duck, "TickFarewell");
        Check(duck.Mode == DuckCompanion.CompanionMode.Following && duck.TargetActor == 7, "follow remains active after acknowledgement");
        Set(duck.Dialogue, "preparing", true);
        duck.ReturnHomeAfterFarewell();
        duck.QueueCommandResponse("follow", 7, false);
        Check(!duck.WaitingForFarewell && !duck.CommandResponsePending && duck.TargetActor == 7
            && duck.Mode == DuckCompanion.CompanionMode.Following,
            "accepted follow cancels a pending farewell departure immediately");
        duck.QueueCommandResponse("follow", 7, true);
        duck.QueueCommandResponse("follow", 7, false);
        Check(duck.CommandResponsePending, "accepted follow cannot bypass an active refusal pause");
        Set(duck.Dialogue, "preparing", false);
        Call(duck, "TickFarewell");
        duck.ReturnHome();
        Set(duck.Dialogue, "preparing", true);
        duck.QueueCommandResponse("wait", 7, false);
        Check(duck.FollowingPaused && (double)Get(duck, "waitUntil") == 0,
            "wait stops immediately but its timer waits for speech");
        Call(duck, "TickFarewell");
        Set(duck.Dialogue, "preparing", false);
        Call(duck, "TickFarewell");
        Check((bool)Call(duck, "TickTimedWait") && duck.Mode == DuckCompanion.CompanionMode.ReturningHome
            && Math.Abs((double)Get(duck, "waitUntil") - PhotonNetwork.Time - 15) < 0.2,
            "wait stops return-home movement for fifteen seconds after acknowledgement");
        var timedWrite = new PhotonStream(true, null);
        Call(timedWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        duck.Serialize(timedWrite); remote.Serialize(new PhotonStream(false, timedWrite.ToArray()));
        Check(remote.FollowingPaused && (double)Get(remote, "waitUntil") == (double)Get(duck, "waitUntil"),
            "timed wait deadline is synchronized");
        Set(duck, "waitUntil", double.Epsilon);
        Check(!(bool)Call(duck, "TickTimedWait") && !duck.FollowingPaused
            && duck.Mode == DuckCompanion.CompanionMode.ReturningHome, "expired wait resumes original return-home journey");
        Set(duck.Dialogue, "preparing", true);
        duck.QueueCommandResponse("quiet", 7, false);
        Call(duck, "TickFarewell");
        Set(duck.Dialogue, "preparing", false);
        Call(duck, "TickFarewell");
        Check(duck.Dialogue.ChatterPaused
            && Math.Abs((double)Get(duck.Dialogue, "quietUntil") - PhotonNetwork.Time - 10) < 0.2,
            "quiet lasts ten seconds after acknowledgement");
        timedWrite = new PhotonStream(true, null);
        Call(timedWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        duck.Serialize(timedWrite); remote.Serialize(new PhotonStream(false, timedWrite.ToArray()));
        Check(remote.Dialogue.ChatterPaused
            && (double)Get(remote.Dialogue, "quietUntil") == (double)Get(duck.Dialogue, "quietUntil"),
            "quiet deadline is synchronized");
        Set(duck.Dialogue, "quietUntil", double.Epsilon);
        Call(duck.Dialogue, "TickQuietPeriod");
        Check(!duck.Dialogue.ChatterPaused, "quiet expiry restores eligibility for normal dialogue");
        duck.Dialogue.BeginQuietPeriod();
        double quietDeadline = (double)Get(duck.Dialogue, "quietUntil");
        Set(duck.Dialogue, "preparing", true);
        Set(duck.Dialogue, "nextReactive", Time.time + 30f);
        Check((bool)Call(duck.Dialogue, "CanReact", "hurt", true),
            "physical hit speech bypasses quiet, busy speech and chatter cooldown");
        Check(!(bool)Call(duck.Dialogue, "CanReact", "tip", true),
            "quiet still blocks ordinary chatter even when forced");
        Check(!(bool)Call(duck.Dialogue, "CanReact", "airborne", true),
            "hit exception does not restore disabled airborne speech");
        Check(duck.Dialogue.ChatterPaused && (double)Get(duck.Dialogue, "quietUntil") == quietDeadline,
            "hit eligibility preserves the quiet preference and its deadline");
        Set(duck.Dialogue, "quietUntil", 0d);
        Check((bool)Call(duck.Dialogue, "CanReact", "hurt", true),
            "dismissal's untimed chatter pause also permits hit speech");
        typeof(PhotonView).GetProperty("IsMine").SetValue(duck.photonView, false);
        Check(!(bool)Call(duck.Dialogue, "CanReact", "hurt", true),
            "remote peers cannot independently choose hit dialogue");
        typeof(PhotonView).GetProperty("IsMine").SetValue(duck.photonView, true);
        Set(duck.Dialogue, "preparing", false);
        foreach (bool recovery in new[] { false, true })
        {
            duck.Follow(7);
            duck.WaitHere();
            duck.Dialogue.BeginQuietPeriod();
            Set(duck.Dialogue, "preparing", true);
            duck.QueueCommandResponse("dismiss", 7, true);
            Set(duck.Dialogue, "pendingGreetingActor", 7);
            Set(duck.Dialogue, "pendingGreetingDeadline", PhotonNetwork.Time + 45);
            Set(duck.Dialogue, "deferredTopic", "school");
            Set(duck, "conversationLookActor", 7);
            Set(duck, "pendingConversationLookActor", 7);
            var killerPhaseField = typeof(DuckCompanion).GetField("killerPhase", Private);
            killerPhaseField.SetValue(duck, Enum.Parse(killerPhaseField.FieldType, "Warning"));
            Call(duck, "ResetAtCompanionSpawn", recovery);
            Check(duck.Mode == DuckCompanion.CompanionMode.HomeWandering && duck.TargetActor == 0
                && !duck.FollowingPaused && !duck.CommandResponsePending && !duck.WaitingForFarewell
                && !duck.KillerSequenceActive && !duck.ReturningAfterDeath,
                "every respawn clears follow, wait, command and attack state; recovery=" + recovery);
            Check(!duck.Dialogue.Busy && !duck.Dialogue.ChatterPaused
                && (int)Get(duck.Dialogue, "pendingGreetingActor") == 0
                && Get(duck.Dialogue, "deferredTopic") == null
                && (int)Get(duck, "conversationLookActor") == 0
                && (int)Get(duck, "pendingConversationLookActor") == 0,
                "respawn clears speech, queued greeting, quiet and conversation actions");
            Set(remote.Dialogue, "preparing", true);
            Set(remote.Dialogue, "deferredTopic", "school");
            var resetWrite = new PhotonStream(true, null);
            Call(resetWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
            duck.Serialize(resetWrite);
            remote.Serialize(new PhotonStream(false, resetWrite.ToArray()));
            Check(remote.Mode == DuckCompanion.CompanionMode.HomeWandering && remote.TargetActor == 0
                && !remote.CommandResponsePending && !remote.FollowingPaused
                && !remote.Dialogue.Busy && !remote.Dialogue.ChatterPaused
                && Get(remote.Dialogue, "deferredTopic") == null,
                "remote receives respawn reset and discards stale local speech");
            Call(remote, "ResetNavigationAfterAuthorityChange");
            Check(remote.Mode == DuckCompanion.CompanionMode.HomeWandering && remote.TargetActor == 0
                && (double)Get(remote, "nextHomeWander") == (double)Get(duck, "nextHomeWander"),
                "authority takeover preserves home wandering and its schedule after respawn");
            Set(remote.Dialogue, "preparing", true);
            remote.Serialize(new PhotonStream(false, resetWrite.ToArray()));
            Check(remote.Dialogue.Busy, "repeated respawn snapshot does not cancel new speech");
            Set(remote.Dialogue, "preparing", false);
        }
        duck.Follow(7);
        typeof(DuckCompanion).GetProperty("State").SetValue(duck, DuckCompanion.Activity.Dead);
        Set(duck, "respawnAt", PhotonNetwork.Time - 1);
        var dyingWrite = new PhotonStream(true, null);
        Call(dyingWrite, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        duck.Serialize(dyingWrite);
        remote.Serialize(new PhotonStream(false, dyingWrite.ToArray()));
        typeof(PhotonView).GetProperty("IsMine").SetValue(duck.photonView, false);
        typeof(PhotonView).GetProperty("IsMine").SetValue(remote.photonView, true);
        Set(remote, "wasAuthority", false);
        Call(remote, "Update");
        Check(!remote.IsDead && remote.Mode == DuckCompanion.CompanionMode.HomeWandering
            && remote.TargetActor == 0 && !remote.CommandResponsePending
            && !remote.FollowingPaused && !remote.Dialogue.ChatterPaused,
            "new authority completes an inherited pending death respawn as a fresh home wanderer");
        remote.Follow(7);
        Call(remote, "ClearFollowProgress");
        Set(remote, "followProgressActor", 7);
        for (int i = 0; i < 28; i++) Call(remote, "UpdateFollowProgress", true, false, .25f);
        Check((float)Get(remote, "followNoProgressSeconds") == 7f, "follow stalls accumulate independently of recovery");
        for (int i = 0; i < 80; i++) Call(remote, "UpdateFollowProgress", false, false, .25f);
        Check((float)Get(remote, "followNoProgressSeconds") == 7f, "intentional pauses spend no follow timeout");
        for (int i = 0; i < 80; i++)
        {
            remote.transform.position += Vector3.right * .2f;
            Check(!(bool)Call(remote, "UpdateFollowProgress", true, true, .25f), "valid longer route keeps follow alive");
        }
        Call(remote, "ClearFollowProgress"); Set(remote, "followProgressActor", 7);
        Vector3 stallOrigin = remote.transform.position;
        for (int i = 0; i < 36; i++)
        {
            remote.transform.position = stallOrigin + Vector3.forward * (i % 2 == 0 ? .2f : -.2f);
            Call(remote, "UpdateFollowProgress", true, false, .25f);
        }
        Check((float)Get(remote, "followNoProgressSeconds") == 9f, "sideways search does not reset follow timeout");
        var progressStream = new PhotonStream(true, null);
        Call(progressStream, "SetWriteStream", new System.Collections.Generic.List<object>(), 0);
        Call(remote, "SerializeHome", progressStream);
        Call(duck, "SerializeHome", new PhotonStream(false, progressStream.ToArray()));
        Call(duck, "ResetNavigationAfterAuthorityChange");
        Check((float)Get(duck, "followNoProgressSeconds") == 9f, "takeover preserves accumulated follow timeout");
        typeof(PhotonView).GetProperty("IsMine").SetValue(duck.photonView, true);
        typeof(DuckCompanion).GetProperty("TargetActor").SetValue(duck, 7);
        Mode(duck, DuckCompanion.CompanionMode.Following);
        typeof(DuckCompanion).GetProperty("State").SetValue(duck, DuckCompanion.Activity.Following);
        Set(duck, "followingPaused", false);
        for (int i = 0; i < 4; i++)
        {
            Set(duck, "movementRequested", true);
            Call(duck, "TickFollowProgress", null, .25f);
        }
        Check(duck.Mode == DuckCompanion.CompanionMode.ReturningHome && duck.TargetActor == 0,
            "ten seconds without progress clears following and starts walking home after takeover");
        Debug.Log("ALL DUCK HOME CHECKS PASSED");
    }
}
