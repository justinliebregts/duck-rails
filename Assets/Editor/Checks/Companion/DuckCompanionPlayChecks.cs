using System;
using System.Collections;
using Photon.Pun;
using Photon.Voice.PUN;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>Batch-only integration fixture. Uses a local Photon offline room; never saves a scene.</summary>
[InitializeOnLoad]
public static class DuckCompanionPlayChecks
{
    const string RunningKey = "Duck.PlayChecks.Running";
    static IEnumerator checks;
    static double started;

    static DuckCompanionPlayChecks()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(RunningKey, false)) return;
            started = EditorApplication.timeSinceStartup;
            checks = Run();
            EditorApplication.update += Tick;
        };
    }

    // Invoke with -batchmode -nographics -executeMethod DuckCompanionPlayChecks.Validate (without -quit).
    public static void Validate()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use a separate batch editor for this fixture.");
        DuckCompanionSetup.Prepare();
        SessionState.SetBool(RunningKey, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        // Prevent the normal editor sign-in bootstrap from loading Quackville over this fixture.
        var signInGuard = new GameObject("Fixture sign-in guard").AddComponent<LoginFlow>();
        signInGuard.enabled = false;
        EditorApplication.EnterPlaymode();
    }

    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 360) throw new TimeoutException("Duck Play checks timed out.");
            if (checks.MoveNext()) return;
            Finish(0);
        }
        catch (Exception exception) { Debug.LogException(exception); Finish(1); }
    }

    static void Finish(int exitCode)
    {
        SessionState.SetBool(RunningKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(exitCode);
    }

    static IEnumerator Run()
    {
        var patrol = UnityEngine.Object.FindAnyObjectByType<VoicePatrolManager>();
        if (patrol != null) UnityEngine.Object.DestroyImmediate(patrol.gameObject);
        foreach (var session in UnityEngine.Object.FindObjectsByType<PhotonSession>(FindObjectsSortMode.None))
            UnityEngine.Object.DestroyImmediate(session.gameObject);
        var voice = UnityEngine.Object.FindAnyObjectByType<PunVoiceClient>();
        if (voice != null) voice.AutoConnectAndJoin = false;
        var chat = UnityEngine.Object.FindAnyObjectByType<PhotonVoiceChat>();
        if (chat != null) UnityEngine.Object.Destroy(chat.gameObject);
        var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Fixture floor";
        floor.layer = LayerMask.NameToLayer("Environment");
        floor.transform.position = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(80, 1, 80);
        var manager = new GameObject("Fixture GameManager").AddComponent<GameManager>();
        manager.respawnPoint = new GameObject("Separate player spawn").transform;
        manager.respawnPoint.position = new Vector3(20, 0.05f, 0);
        manager.companionSpawnPoint = new GameObject("Fixture spawn").transform;
        manager.companionSpawnPoint.position = new Vector3(-8, 0.05f, 0);
        manager.companionSpawnPoint.rotation = Quaternion.Euler(15, 70, 10);
        PhotonNetwork.OfflineMode = true;
        PhotonNetwork.CreateRoom("Duck local integration checks");
        while (!PhotonNetwork.InRoom) yield return null;
        GameObject playerPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Photon/PhotonPlayer.prefab");
        PhotonPreparePool.Register(playerPrefab);
        PhotonPlayer human = PhotonNetwork.Instantiate(playerPrefab.name, Vector3.zero, Quaternion.identity).GetComponent<PhotonPlayer>();
        Transform head = new GameObject("Fixture human head").transform;
        head.gameObject.AddComponent<AudioListener>();
        head.position = new Vector3(0, 1.5f, 0);
        human.BindSources(head, head, head);
        DuckCompanion duck;
        while ((duck = UnityEngine.Object.FindAnyObjectByType<DuckCompanion>()) == null) yield return null;
        Check(duck.HasAuthority && PhotonPlayer.Local == human, "room bot authority and human identity");
        Check(Vector3.Distance(duck.transform.position, manager.companionSpawnPoint.position) < 0.3f, "initial spawn uses the companion spawn point, not the nearby human");
        Check(manager.TryGetCompanionSpawnPose(out _, out Quaternion spawnRotation) && Vector3.Dot(spawnRotation * Vector3.up, Vector3.up) > 0.999f,
            "companion spawn heading removes pitch and roll");
        head.position = new Vector3(12, 1.5f, 0);
        Check(duck.TargetActor == 0 && duck.Mode == DuckCompanion.CompanionMode.HomeWandering,
            "spawning does not automatically acquire a player");
        duck.Follow(human.photonView.OwnerActorNr);
        double until = EditorApplication.timeSinceStartup + 5;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Check(Vector3.Distance(duck.transform.position, new Vector3(12, 0, 0)) < 5, "follows human on flat ground");
        CheckGroundSupport(duck, 0f);
        var gait = duck.GetComponent<Game.Animation.GorillaGait>();
        Check(gait != null && gait.ExternalTick && gait.StepsTotal > 0, "companion movement drives the minion's procedural hand gait");
        Check(duck.GetComponent<PhotonPlayer>().LeftHand != null, "shared player hand rig exists");
        CheckMeshHandRecovery(duck, gait);
        CheckCompanionRoomMute(duck, human);
        CheckBuildingExitRoutes(duck);
        CheckObstacleDetourAndTakeover(duck);
        CheckIdleRecoveryIntent(duck, human);
        IEnumerator slopeDropChecks = CheckSlopedLedgeDrops(duck);
        while (slopeDropChecks.MoveNext()) yield return slopeDropChecks.Current;
        IEnumerator trimFallChecks = CheckActualBuildingTrimFalls(duck);
        while (trimFallChecks.MoveNext()) yield return trimFallChecks.Current;
        IEnumerator busDoorChecks = CheckBusDoorway(duck, human, head);
        while (busDoorChecks.MoveNext()) yield return busDoorChecks.Current;
        IEnumerator gasRoofChecks = CheckGasStationRoof(duck, human, head);
        while (gasRoofChecks.MoveNext()) yield return gasRoofChecks.Current;
        IEnumerator roofLevelChecks = CheckRoofLevelAndGaze(duck, human, head);
        while (roofLevelChecks.MoveNext()) yield return roofLevelChecks.Current;
        IEnumerator windowChecks = CheckWindowTraversal(duck, human, head);
        while (windowChecks.MoveNext()) yield return windowChecks.Current;
        CheckScoutAndSpeechRules(duck, human, head);
        CheckCreepyPersonality(duck);
        CheckAcknowledgments(duck, human, head);
        CheckKillerWarningRules(duck, human);
        CheckCompanionExpressiveness(duck);
        CheckThreatWarnings(duck, human, head);
        IEnumerator looseItemChecks = CheckLooseItemSupport(duck, head);
        while (looseItemChecks.MoveNext()) yield return looseItemChecks.Current;
        IEnumerator recoveryChecks = CheckStuckRecovery(duck, human, head);
        while (recoveryChecks.MoveNext()) yield return recoveryChecks.Current;
        duck.Dialogue.SubmitLocal("Duck, how do I find gold?");
        until = EditorApplication.timeSinceStartup + 30;
        while (duck.Dialogue.Busy && EditorApplication.timeSinceStartup < until) yield return null;
        Check(!string.IsNullOrEmpty(duck.Dialogue.LastReply), "authored response delivered through Photon RPC");
        Check(duck.ReactToPlayerHit(human.photonView.OwnerActorNr, Vector3.forward * 4f)
            && duck.State == DuckCompanion.Activity.Retreating, "player hit starts retreat");
        Check(!duck.ReactToPlayerHit(human.photonView.OwnerActorNr, Vector3.forward * 4f), "continuous item contact does not spam reactions");
        double retreatStart = PhotonNetwork.Time;
        while (PhotonNetwork.Time < retreatStart + 1) yield return null;
        Check(duck.State == DuckCompanion.Activity.Retreating
            && Vector3.Dot(duck.Head.forward, (head.position - duck.Head.position).normalized) > 0.8f, "retreat keeps looking at the hitter");
        while (PhotonNetwork.Time < retreatStart + 5.2) yield return null;
        Check(duck.State == DuckCompanion.Activity.Following && duck.TargetActor == human.photonView.OwnerActorNr,
            "five-second retreat resumes following the same buddy");
        // Exercise production item samples and the actual Photon offline-room RPC,
        // rather than only calling ReactToPlayerHit directly.
        foreach (bool fallback in new[] { false, true })
        foreach (bool leftHand in new[] { false, true })
        {
            var avatar = duck.GetComponent<PhotonPlayer>();
            SetPrivate(avatar, "companion", null);
            Check(avatar.Companion == duck, "hit path reacquires companion identity after cache loss");
            head.position = duck.Head.position + Vector3.back * 2;
            CheckItemSweep(duck, fallback, leftHand: leftHand);
            Check(duck.State == DuckCompanion.Activity.Retreating && IsHurtReply(duck.Dialogue.LastReply),
                fallback ? "sphere item without locomotion samples starts ouch/retreat" : "light weapon sweep starts ouch/retreat through RPC");
            retreatStart = PhotonNetwork.Time;
            while (PhotonNetwork.Time < retreatStart + 5.2) yield return null;
            Check(duck.State == DuckCompanion.Activity.Following, "item strike resumes following after five seconds");
        }
        CheckItemSweep(duck, false, true);
        Check(duck.State == DuckCompanion.Activity.Retreating,
            "room-owned item uses its human controller for hit attribution");
        while (duck.State == DuckCompanion.Activity.Retreating) yield return null;
        IEnumerator contactChecks = CheckThrownContact(duck, head);
        while (contactChecks.MoveNext()) yield return contactChecks.Current;
        IEnumerator desktopChecks = CheckDesktopThrow(duck, head);
        while (desktopChecks.MoveNext()) yield return desktopChecks.Current;
        foreach (bool left in new[] { false, true })
        {
            IEnumerator vrThrow = CheckVrThrow(duck, head, left);
            while (vrThrow.MoveNext()) yield return vrThrow.Current;
        }
        IEnumerator networkChecks = CheckRemoteHitReaction(duck, human, head);
        while (networkChecks.MoveNext()) yield return networkChecks.Current;
        Check(duck.TryKill() && duck.IsDead, "zombie death transition");
        until = EditorApplication.timeSinceStartup + 7;
        while (duck.IsDead && EditorApplication.timeSinceStartup < until) yield return null;
        Check(!duck.IsDead && duck.ReturningAfterDeath, "normal five-second respawn and return");
        manager.disableDuckCompanion = true;
        until = EditorApplication.timeSinceStartup + 2;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        Check(GameManager.instance == manager, "fixture retains its GameManager");
        Check(UnityEngine.Object.FindAnyObjectByType<DuckCompanion>() == null, "live GameManager switch removes Duck");
        Check(PhotonPlayer.Local == human, "disabling Duck preserves human player");
        manager.disableDuckCompanion = false;
        until = EditorApplication.timeSinceStartup + 3;
        while ((duck = UnityEngine.Object.FindAnyObjectByType<DuckCompanion>()) == null && EditorApplication.timeSinceStartup < until) yield return null;
        Check(duck != null, "unchecking GameManager switch respawns Duck");
        var motor = duck.GetComponent<CharacterController>();
        motor.enabled = false;
        duck.transform.position = new Vector3(0, 0.05f, 0);
        motor.enabled = true;
        var ledge = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ledge.layer = LayerMask.NameToLayer("Environment");
        ledge.transform.position = new Vector3(0, 0.5f, 1.5f);
        ledge.transform.localScale = new Vector3(3, 1, 2);
        head.position = new Vector3(0, 2.5f, 2);
        duck.Follow(human.photonView.OwnerActorNr);
        until = EditorApplication.timeSinceStartup + 8;
        bool traversed = false;
        while (EditorApplication.timeSinceStartup < until)
        {
            traversed |= duck.State == DuckCompanion.Activity.Climbing || duck.State == DuckCompanion.Activity.Jumping;
            if (traversed && duck.transform.position.y > 0.9f && duck.State == DuckCompanion.Activity.Following) break;
            yield return null;
        }
        Check(traversed && duck.transform.position.y > 0.9f,
            $"follows player onto reachable ledge (position {duck.transform.position}, state {duck.State}, event {duck.LastEvent})");
        until = EditorApplication.timeSinceStartup + 1;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        CheckGroundSupport(duck, 1f);

        // Bus-height roof: a lower landing must be discovered without an authored link.
        var bus = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bus.layer = LayerMask.NameToLayer("Environment");
        bus.transform.position = new Vector3(15, 1.5f, 0);
        bus.transform.localScale = new Vector3(3, 3, 6);
        motor.enabled = false;
        duck.transform.position = new Vector3(15, 3.04f, 0);
        motor.enabled = true;
        head.position = new Vector3(22, 0.5f, 0);
        duck.Follow(human.photonView.OwnerActorNr);
        until = EditorApplication.timeSinceStartup + 8;
        bool jumpedDown = false;
        while (EditorApplication.timeSinceStartup < until)
        {
            jumpedDown |= duck.State == DuckCompanion.Activity.Jumping;
            if (jumpedDown && motor.isGrounded && duck.transform.position.y < 0.1f) break;
            yield return null;
        }
        Check(duck.transform.position.x > 16.5f && duck.transform.position.y < 0.1f,
            $"drops off bus-height roof (position {duck.transform.position}, state {duck.State})");
        CheckGroundSupport(duck, 0f);

        // Tall roofs must not introduce an implicit drop limit or slow horizontal takeoff.
        foreach (float roofHeight in new[] { 6f, 30f, 150f })
        {
            bus.transform.position = new Vector3(15, roofHeight * 0.5f, 0);
            bus.transform.localScale = new Vector3(3, roofHeight, 6);
            motor.enabled = false;
            duck.transform.position = new Vector3(15, roofHeight + 0.04f, 0);
            motor.enabled = true;
            Physics.SyncTransforms();
            duck.Follow(human.photonView.OwnerActorNr);
            until = EditorApplication.timeSinceStartup + 12;
            jumpedDown = false;
            while (EditorApplication.timeSinceStartup < until)
            {
                jumpedDown |= duck.State == DuckCompanion.Activity.Jumping;
                if (jumpedDown && motor.isGrounded && duck.transform.position.y < 0.1f) break;
                yield return null;
            }
            Check(!duck.IsDead && duck.transform.position.x > 16.5f && duck.transform.position.y < 0.1f,
                $"drops from a {roofHeight}m roof without fall damage (position {duck.transform.position})");
            CheckGroundSupport(duck, 0f);
        }

        // Wall climbs must continue past arm reach and ordinary building heights.
        foreach (float roofHeight in new[] { 3f, 10f, 30f })
        {
            bus.transform.position = new Vector3(15, roofHeight * 0.5f, 0);
            bus.transform.localScale = new Vector3(3, roofHeight, 6);
            motor.enabled = false;
            duck.transform.position = new Vector3(12, 0.04f, 0);
            motor.enabled = true;
            Physics.SyncTransforms();
            head.position = new Vector3(15, roofHeight + 0.5f, 0);
            duck.Follow(human.photonView.OwnerActorNr);
            until = EditorApplication.timeSinceStartup + roofHeight / 1.8f + 8;
            bool climbed = false;
            bool leftAbove = false, rightAbove = false, plantedPull = false;
            float previousBodyY = duck.transform.position.y, previousLeftY = duck.LeftHand.position.y;
            while (EditorApplication.timeSinceStartup < until)
            {
                if (duck.State == DuckCompanion.Activity.Climbing)
                {
                    climbed = true;
                    leftAbove |= duck.LeftHand.position.y > duck.RightHand.position.y + 0.08f;
                    rightAbove |= duck.RightHand.position.y > duck.LeftHand.position.y + 0.08f;
                    plantedPull |= duck.transform.position.y > previousBodyY + 0.005f
                        && Mathf.Abs(duck.LeftHand.position.y - previousLeftY) < 0.002f;
                    Check(Vector3.Distance(duck.Head.position, duck.LeftHand.position) < 0.96f
                        && Vector3.Distance(duck.Head.position, duck.RightHand.position) < 0.96f,
                        "climbing hand targets stay within reach");
                }
                previousBodyY = duck.transform.position.y;
                previousLeftY = duck.LeftHand.position.y;
                if (climbed && duck.State == DuckCompanion.Activity.Following && duck.transform.position.y > roofHeight - 0.1f) break;
                yield return null;
            }
            Check(climbed && duck.transform.position.y > roofHeight - 0.1f && duck.transform.position.x > 13.5f,
                $"climbs a {roofHeight}m wall onto the roof (position {duck.transform.position})");
            Check(leftAbove && rightAbove && plantedPull, "climbing alternates reaching arms and pulls past a planted hand");
        }

        // A retreat can leave the roof, then normal following climbs back to the buddy.
        SetPrivate(duck, "nextHitReaction", 0d);
        Check(duck.ReactToPlayerHit(human.photonView.OwnerActorNr, Vector3.left * 4), "roof hit starts retreat");
        until = EditorApplication.timeSinceStartup + 5.2;
        bool fellWhileRetreating = false, landedAfterRetreatFall = false;
        while (EditorApplication.timeSinceStartup < until)
        {
            fellWhileRetreating |= duck.State == DuckCompanion.Activity.Retreating && duck.transform.position.y < 29;
            landedAfterRetreatFall |= duck.transform.position.y < 0.1f && motor.isGrounded;
            yield return null;
        }
        Check(fellWhileRetreating && landedAfterRetreatFall && !duck.IsDead,
            "retreat backs off a 30m roof and lands without fall damage");
        Check(duck.TargetActor == human.photonView.OwnerActorNr && duck.State != DuckCompanion.Activity.Retreating,
            "retreat expires and retains the same buddy after falling");
        until = EditorApplication.timeSinceStartup + 35;
        bool climbedBack = false;
        while (EditorApplication.timeSinceStartup < until)
        {
            climbedBack |= duck.State == DuckCompanion.Activity.Climbing;
            if (climbedBack && duck.State == DuckCompanion.Activity.Following && duck.transform.position.y > 29.9f) break;
            yield return null;
        }
        Check(climbedBack && duck.transform.position.y > 29.9f, "climbs back to rooftop buddy after retreat fall");

        // Ground traversal shares the minions' slope limit, including ramp/plateau seams.
        foreach (float angle in new[] { 50f, 65f, Game.AI.CrawlGraph.MaxGroundSlopeDegrees })
        {
            Vector3 rampStart = new Vector3(-30, 0, 25);
            Quaternion tilt = Quaternion.Euler(-angle, 0, 0);
            Vector3 rise = tilt * Vector3.forward * 8;
            GameObject ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ramp.layer = LayerMask.NameToLayer("Environment");
            ramp.transform.SetPositionAndRotation(rampStart + rise * 0.5f - tilt * Vector3.up * 0.25f, tilt);
            ramp.transform.localScale = new Vector3(6, 0.5f, 8);
            GameObject plateau = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plateau.layer = ramp.layer;
            plateau.transform.position = rampStart + rise + Vector3.forward * 3.5f - Vector3.up * (rise.y * 0.5f);
            plateau.transform.localScale = new Vector3(6, rise.y, 7);
            motor.enabled = false;
            duck.transform.position = rampStart + Vector3.back + Vector3.up * 0.04f;
            motor.enabled = true;
            Physics.SyncTransforms();
            head.position = rampStart + rise + Vector3.forward * 4 + Vector3.up * 0.5f;
            duck.Follow(human.photonView.OwnerActorNr);
            until = EditorApplication.timeSinceStartup + 12;
            bool usedTraversal = false;
            while (EditorApplication.timeSinceStartup < until)
            {
                usedTraversal |= duck.State == DuckCompanion.Activity.Jumping || duck.State == DuckCompanion.Activity.Climbing;
                if (duck.transform.position.y > rise.y - 0.1f && duck.transform.position.z > rampStart.z + rise.z + 0.5f) break;
                yield return null;
            }
            Check(!usedTraversal && duck.transform.position.y > rise.y - 0.1f
                && duck.transform.position.z > rampStart.z + rise.z + 0.5f, $"walks up {angle} degree slope onto plateau");
            head.position = rampStart + Vector3.back * 5 + Vector3.up * 0.5f;
            until = EditorApplication.timeSinceStartup + 12;
            while (EditorApplication.timeSinceStartup < until)
            {
                if (duck.transform.position.y < 0.1f && duck.transform.position.z < rampStart.z - 0.3f) break;
                yield return null;
            }
            Check(duck.transform.position.y < 0.1f && duck.transform.position.z < rampStart.z - 0.3f, $"descends {angle} degree slope");
            UnityEngine.Object.Destroy(ramp);
            UnityEngine.Object.Destroy(plateau);
            yield return null;
        }

        // A target disconnect/death must not suspend gravity while Duck is airborne.
        human.gameObject.SetActive(false);
        motor.enabled = false;
        duck.transform.position = new Vector3(10, 3, 0);
        motor.enabled = true;
        until = EditorApplication.timeSinceStartup + 3;
        while (EditorApplication.timeSinceStartup < until) yield return null;
        CheckGroundSupport(duck, 0f);
        human.gameObject.SetActive(true);
        Debug.Log("[Duck Play checks] Player spawn point, ground support (including without a target), follow, climb/jump down, shared rig, dialogue RPC, death/respawn, disable/re-enable all passed in a local Photon room.");
    }

    static void CheckGroundSupport(DuckCompanion duck, float surfaceY)
    {
        var controller = duck.GetComponent<CharacterController>();
        Check(controller.isGrounded && Mathf.Abs(duck.transform.position.y - surfaceY) < 0.08f, "controller rests on the support surface");
        Check(Mathf.Abs(duck.Head.position.y - surfaceY - 0.5f) < 0.1f, "duck eye height matches the desktop player prefab");
        Check(Mathf.Abs(controller.height - 0.65f) < 0.01f, "collision capsule fits the duck rather than a human");
    }

    static bool IsHurtReply(string text)
    {
        var bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        return Array.IndexOf(Array.Find(bank.groups, g => g.intent == "hurt").lines, text) >= 0;
    }

    static IEnumerator CheckLooseItemSupport(DuckCompanion duck, Transform head)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/WeaponNailBat.prefab");
        var original = prefab.GetComponent<BoxCollider>();
        var item = new GameObject("Nail bat support regression");
        item.layer = prefab.layer;
        var box = item.AddComponent<BoxCollider>();
        box.size = original.size;
        box.center = original.center;
        var motor = duck.GetComponent<CharacterController>();
        duck.WaitHere();
        foreach (float tilt in new[] { 0f, 45f, 90f })
        {
            item.transform.SetPositionAndRotation(new Vector3(-12, 0, 8), Quaternion.Euler(tilt, 0, 0));
            Physics.SyncTransforms();
            motor.enabled = false;
            duck.transform.position = new Vector3(box.bounds.center.x, box.bounds.max.y + 0.5f, box.bounds.center.z);
            motor.enabled = true;
            head.position = duck.transform.position + Vector3.right * 2;
            double until = PhotonNetwork.Time + 1.2;
            while (PhotonNetwork.Time < until) yield return null;
            CheckGroundSupport(duck, 0);
            Check(!duck.GetComponent<Game.Animation.GorillaGait>().Airborne, "nail bat beneath Duck cannot trap falling IK at " + tilt + " degrees");
        }
        UnityEngine.Object.Destroy(item);
        duck.Follow(PhotonPlayer.Local.photonView.OwnerActorNr);
    }

    static void CheckIdleRecoveryIntent(DuckCompanion duck, PhotonPlayer human)
    {
        Vector3 saved = duck.transform.position;
        var motor = duck.GetComponent<CharacterController>();
        try
        {
            motor.enabled = false;
            duck.transform.position = new Vector3(0, 0.04f, -15);
            motor.enabled = true;
            CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
            Physics.SyncTransforms();
            Vector3 before = duck.transform.position;
            SetPrivate(duck, "lastProgressAt", PhotonNetwork.Time - 30d);
            SetPrivate(duck, "groundSupported", true);
            SetPrivate(duck, "movementRequested", false);
            CallPrivate(duck, "TickRecovery", human);
            Check(Vector3.Distance(before, duck.transform.position) < 0.001f,
                "intentional idle does not consume the stuck timer or relocate Duck");
            SetPrivate(duck, "groundSupported", false);
            CallPrivate(duck, "TickRecovery", human);
            Check(Vector3.Distance(before, duck.transform.position) < 0.001f,
                "brief idle support loss cannot trigger recovery movement");
            Check((bool)CallPrivate(duck, "TryRecoveryStep", human)
                && Vector3.Distance(before, duck.transform.position) < 0.001f,
                "ordinary recovery schedules walking instead of a one-frame teleport");
        }
        finally
        {
            motor.enabled = false;
            duck.transform.position = saved;
            motor.enabled = true;
            CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
        }
    }

    static IEnumerator CheckSlopedLedgeDrops(DuckCompanion duck)
    {
        Vector3 saved = duck.transform.position;
        bool wasEnabled = duck.enabled;
        var motor = duck.GetComponent<CharacterController>();
        var ramp = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ramp.layer = LayerMask.NameToLayer("Environment");
        Vector3 origin = new Vector3(20, 0, -20);
        ramp.transform.position = origin + Vector3.up * 0.8f;
        ramp.transform.localScale = new Vector3(4, 1, 6);
        duck.enabled = false;
        try
        {
            foreach (float angle in new[] { 0f, 18f, -18f })
            {
                ramp.transform.rotation = Quaternion.Euler(0, 0, angle);
                Physics.SyncTransforms();
                Check(Physics.Raycast(origin + new Vector3(1.8f, 5, 0), Vector3.down, out RaycastHit edge,
                    10, LayerMask.GetMask("Environment")), "slope test has support");
                motor.enabled = false;
                duck.transform.position = edge.point + Vector3.up * 0.04f;
                motor.enabled = true;
                CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
                float until = Time.time + 4f;
                while (Time.time < until)
                {
                    CallPrivate(duck, "Move", origin + new Vector3(5, 0.04f, 0), true);
                    yield return null;
                    if (duck.transform.position.x > origin.x + 2.6f && duck.transform.position.y < 0.15f) break;
                }
                Check(duck.transform.position.x > origin.x + 2.6f && duck.transform.position.y < 0.15f,
                    "clears slope lip and falls to ground at angle " + angle);
            }
            // Regression: the city curb enters the follow height tolerance
            // before the capsule has finished its already-approved drop.
            ramp.transform.rotation = Quaternion.identity;
            ramp.transform.position = origin + Vector3.up * 0.115f;
            ramp.transform.localScale = new Vector3(4, 0.23f, 6);
            motor.enabled = false;
            duck.transform.position = origin + new Vector3(1.95f, 0.27f, 0);
            motor.enabled = true;
            CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
            Physics.SyncTransforms();
            object[] dropArgs = { Vector3.right, Vector3.zero };
            Check((bool)CallPrivate(duck, "TryDropDirection", dropArgs), "shallow curb has a checked drop");
            float finishDropAt = Time.time + 2f;
            while (Time.time < finishDropAt)
            {
                CallPrivate(duck, "Move", origin + new Vector3(2.6f, 0.04f, 0), false);
                yield return null;
            }
            Check(duck.transform.position.x > origin.x + 2.6f && duck.transform.position.y < 0.15f,
                "committed drop finishes despite the follow stopping tolerance");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(ramp);
            motor.enabled = false;
            duck.transform.position = saved;
            motor.enabled = true;
            CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
            duck.enabled = wasEnabled;
        }
    }

    static IEnumerator CheckActualBuildingTrimFalls(DuckCompanion duck)
    {
        Vector3 saved = duck.transform.position;
        bool enabled = duck.enabled;
        var motor = duck.GetComponent<CharacterController>();
        // Keep the original model's trim, including its mesh seams and corners.
        var city = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Models/Quackville/Quackville_City.obj"));
        city.transform.SetPositionAndRotation(new Vector3(300, 0, 300), Quaternion.Euler(0, 90, 0));
        foreach (MeshFilter mesh in city.GetComponentsInChildren<MeshFilter>())
        {
            mesh.gameObject.layer = LayerMask.NameToLayer("Environment");
            mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }
        duck.enabled = false;
        try
        {
            Physics.SyncTransforms();
            IEnumerator budgetChecks = CheckCityLedgeRetryBudget(duck, city);
            while (budgetChecks.MoveNext()) yield return budgetChecks.Current;
            foreach (string building in new[] { "Apartment_Block", "Rear_Tenement", "Police_Station" })
            {
                Bounds bounds = Array.Find(city.GetComponentsInChildren<MeshCollider>(), c => c.name == building).bounds;
                foreach (float offset in new[] { 0.02f, 0.08f })
                {
                    Vector3 start = new Vector3(bounds.max.x - 1.05f + offset, bounds.max.y + 0.5f,
                        bounds.max.z - 1.05f + offset);
                    motor.enabled = false;
                    duck.transform.position = start;
                    motor.enabled = true;
                    CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
                    typeof(DuckCompanion).GetProperty("State").SetValue(duck, DuckCompanion.Activity.Following);
                    Physics.SyncTransforms();
                    bool released = false;
                    float until = Time.time + 4f;
                    while (Time.time < until)
                    {
                        CallPrivate(duck, "Move", duck.transform.position, false);
                        CallPrivate(duck, "FinishMovement", new object[] { null });
                        released |= (double)typeof(DuckCompanion).GetField("fallReleaseUntil",
                            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(duck) > PhotonNetwork.Time;
                        yield return null;
                    }
                    Check(offset < 0.05f ? !released : released && duck.transform.position.y < start.y - 4f,
                        building + (offset < 0.05f ? " keeps a valid roof landing" : " releases a real trim snag after falling from above"));
                }
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(city);
            motor.enabled = false;
            duck.transform.position = saved;
            motor.enabled = true;
            CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
            duck.enabled = enabled;
        }
    }

    static IEnumerator CheckCityLedgeRetryBudget(DuckCompanion duck, GameObject city)
    {
        var motor = duck.GetComponent<CharacterController>();
        Bounds block = Array.Find(city.GetComponentsInChildren<MeshCollider>(),
            c => c.name == "Central_Service_Block").bounds;
        int checkedCrossings = 0;
        for (int sample = 1; sample < 6; sample++)
        {
            Vector3 probe = new Vector3(block.max.x + 0.04f, city.transform.position.y + 5f,
                Mathf.Lerp(block.min.z, block.max.z, sample / 6f));
            int mask = LayerMask.GetMask("Environment");
            if (!Physics.Raycast(probe, Vector3.down, out RaycastHit top, 10f, mask)) continue;
            Vector3 start = top.point + Vector3.up * 0.04f;
            if (!Physics.Raycast(start + Vector3.left * 0.65f + Vector3.up * 0.08f,
                Vector3.down, out RaycastHit lower, 10f, mask) || lower.point.y > start.y - 0.2f) continue;
            Vector3 goal = lower.point + Vector3.up * 0.04f;
            motor.enabled = false; duck.transform.position = start; motor.enabled = true;
            CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
            Physics.SyncTransforms();
            // Inject a stalled attempt so this regression does not depend on
            // frame-rate-specific contact jitter; routes use the real city mesh.
            SetPrivate(duck, "dropUntil", PhotonNetwork.Time + 100d);
            SetPrivate(duck, "dropExit", start + Vector3.left);
            SetPrivate(duck, "dropLanding", goal);
            SetPrivate(duck, "dropDirection", Vector3.zero);
            float until = Time.time + 3.3f;
            while (Time.time < until) { CallPrivate(duck, "Move", goal, true); yield return null; }
            Check((float)GetPrivate(duck, "avoidedLedgeUntil") > Time.time,
                "failed city crossing switches to detour within 3.3 seconds");
            Check(((Vector3[])GetPrivate(duck, "obstacleRoute")).Length > 0, "city ledge has a checked alternate route");
            bool wentAround = false;
            until = Time.time + 15f;
            while (Time.time < until && duck.transform.position.y > goal.y + 0.12f)
            {
                CallPrivate(duck, "Move", goal, true);
                wentAround |= Mathf.Abs(duck.transform.position.z - start.z) > 2f;
                yield return null;
            }
            Check(wentAround && duck.transform.position.y <= goal.y + 0.12f,
                "takes longer city route and reaches lower pavement");
            checkedCrossings++;
        }
        Check(checkedCrossings > 0, "actual city ledge retry cases were exercised");
    }

    static void CheckObstacleDetourAndTakeover(DuckCompanion duck)
    {
        Vector3 saved = duck.transform.position;
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.layer = LayerMask.NameToLayer("Environment");
        wall.transform.position = new Vector3(0, 5, -20);
        wall.transform.localScale = new Vector3(12, 10, 1);
        var motor = duck.GetComponent<CharacterController>();
        try
        {
            motor.enabled = false;
            duck.transform.position = new Vector3(0, 0.04f, -22);
            motor.enabled = true;
            Physics.SyncTransforms();
            SetPrivate(duck, "nextObstacleSearch", 0f);
            Check((bool)CallPrivate(duck, "TryObstacleRoute", new Vector3(0, 0.04f, -8)),
                "exterior obstacle gets a collision-checked detour");
            var route = (Vector3[])typeof(DuckCompanion).GetField("obstacleRoute",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(duck);
            Check(Array.Exists(route, point => Mathf.Abs(point.x) > 6.2f), "detour reaches beyond the building corner");
            SetPrivate(duck, "nextTraversal", Time.time + 500f);
            SetPrivate(duck, "localDetourUntil", PhotonNetwork.Time + 500d);
            CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
            Check((bool)CallPrivate(duck, "TryObstacleRoute", new Vector3(0, 0.04f, -8)),
                "authority reset permits immediate replanning");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(wall);
            motor.enabled = false;
            duck.transform.position = saved;
            motor.enabled = true;
            CallPrivate(duck, "ResetNavigationAfterAuthorityChange");
        }
    }

    static IEnumerator CheckStuckRecovery(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        Vector3 origin = new Vector3(-25, 0, 10);
        var obstacles = new System.Collections.Generic.List<GameObject>();
        Vector3[] offsets = { new Vector3(-0.5f, 1.5f, 0), new Vector3(0.5f, 1.5f, 0),
            new Vector3(0, 1.5f, -0.5f), new Vector3(0, 1.5f, 0.5f), Vector3.up * 3 };
        for (int i = 0; i < offsets.Length; i++)
        {
            var obstacle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obstacle.layer = LayerMask.NameToLayer("Environment");
            obstacle.transform.position = origin + offsets[i];
            obstacle.transform.localScale = i < 2 ? new Vector3(0.25f, 3, 1.25f)
                : i < 4 ? new Vector3(1.25f, 3, 0.25f) : new Vector3(1.25f, 0.3f, 1.25f);
            obstacles.Add(obstacle);
        }
        var motor = duck.GetComponent<CharacterController>();
        motor.enabled = false;
        duck.transform.position = origin + Vector3.up * 0.04f;
        motor.enabled = true;
        head.position = origin + Vector3.right * 8 + Vector3.up * 0.5f;
        duck.Follow(human.photonView.OwnerActorNr);
        Physics.SyncTransforms();
        yield return null;
        Check(GameManager.instance.TryGetPlayerSpawnPose(out Vector3 spawn, out _), "shared spawn available for stuck recovery");
        double startedAt = PhotonNetwork.Time;
        int attempts = 0;
        var counter = typeof(DuckCompanion).GetField("recoveryAttempts", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        while (PhotonNetwork.Time < startedAt + 22 && Vector3.Distance(duck.transform.position, spawn) > 0.3f)
        {
            attempts = Math.Max(attempts, (int)counter.GetValue(duck));
            yield return null;
        }
        Check(Vector3.Distance(duck.transform.position, spawn) < 0.3f && PhotonNetwork.Time - startedAt >= 19.8 && attempts >= 5,
            "stuck recovery tries locally before the twenty-second shared-spawn fallback");
        Check(!duck.IsDead && !duck.FollowingPaused && duck.State == DuckCompanion.Activity.Following
            && duck.TargetActor == human.photonView.OwnerActorNr, "recovery resumes following the same player");
        foreach (GameObject obstacle in obstacles) UnityEngine.Object.Destroy(obstacle);
    }

    static void CheckCompanionRoomMute(DuckCompanion duck, PhotonPlayer human)
    {
        var mute = duck.SelfMute;
        AudioSource duckVoice = duck.GetComponent<PhotonPlayer>().VoiceSource;
        bool humanMuted = human.VoiceSource.mute;
        duck.Dialogue.CancelSpeech();
        SetPrivate(mute, "nextLocalRequest", 0d);
        SetPrivate(mute, "nextReactionAt", 0d);
        duckVoice.mute = true;
        mute.SetMuted(true);
        Check(!duckVoice.mute && human.VoiceSource.mute == humanMuted, "Duck unmute is immediate and leaves human voice alone");
        CallPrivate(mute, "Update");
        int sequence = duck.Dialogue.SpeechSequence;
        var bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        Check(Array.IndexOf(Array.Find(bank.groups, g => g.intent == "unmuted").lines, duck.Dialogue.LastReply) >= 0,
            "room unmute selects an authored short complaint");
        for (int i = 0; i < 20; i++) mute.SetMuted(true);
        CallPrivate(mute, "Update");
        Check(duck.Dialogue.SpeechSequence == sequence && !duckVoice.mute, "mute spam cannot restart speech or leave Duck muted");
        duck.Dialogue.CancelSpeech();
    }

    static void CheckBuildingExitRoutes(DuckCompanion duck)
    {
        var town = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Quackville/Quackville_Town.obj"));
        Vector3 original = duck.transform.position;
        var motor = duck.GetComponent<CharacterController>();
        try
        {
            town.transform.position = new Vector3(-8.2f, .47170997f, 15.53724f);
            foreach (var mesh in town.GetComponentsInChildren<MeshFilter>())
            {
                mesh.gameObject.layer = LayerMask.NameToLayer("Environment");
                mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
            }
            Physics.SyncTransforms();
            foreach (string name in new[] { "Farmhouse", "General_Store", "Sheriffs_Office", "Gas_Station", "Church", "Barn" })
            {
                var building = Array.Find(town.GetComponentsInChildren<MeshCollider>(), c => c.name == name);
                Check(building != null, "exit fixture exists: " + name);
                Bounds bounds = building.bounds;
                int routes = 0;
                for (float x = bounds.min.x + .8f; x < bounds.max.x - .8f; x += 1.5f)
                for (float z = bounds.min.z + .8f; z < bounds.max.z - .8f; z += 1.5f)
                {
                    if (!Physics.Raycast(new Vector3(x, 2.5f, z), Vector3.down, out var floor, 4f, LayerMask.GetMask("Environment"))) continue;
                    Vector3 point = floor.point + Vector3.up * .04f;
                    if (!Physics.Raycast(point + Vector3.up * .8f, Vector3.up, out var roof, 8f, LayerMask.GetMask("Environment")) || roof.collider != building) continue;
                    motor.enabled = false; duck.transform.position = point; motor.enabled = true;
                    Physics.SyncTransforms();
                    if (!(bool)CallPrivate(duck, "ClearBody", point)) continue;
                    if ((bool)CallPrivate(duck, "TryInteriorExit", new Vector3(bounds.max.x + 5, point.y, bounds.center.z), bounds)) routes++;
                }
                Check(routes > 0, "geometry-based exit or safe furniture drop in " + name + ": " + routes + " samples");
            }
        }
        finally
        {
            motor.enabled = false; duck.transform.position = original; motor.enabled = true;
            CallPrivate(duck, "ResetRecovery");
            SetPrivate(duck, "levelRoute", Array.Empty<Vector3>());
            SetPrivate(duck, "followingInteriorExit", false);
            UnityEngine.Object.DestroyImmediate(town);
            Physics.SyncTransforms();
        }
    }

    static IEnumerator CheckBusDoorway(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        // Match both the scene mesh and its baked navigation coordinates. Testing
        // the mesh without routing misses graph nodes below/beside the doorway.
        var graphObject = new GameObject("Fixture navigation");
        graphObject.SetActive(false);
        var navigation = graphObject.AddComponent<Game.AI.EnemyManager>();
        var previousNavigation = Game.AI.EnemyManager.Instance;
        var graph = Game.AI.CrawlGraph.Load(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scenes/Quackville_CrawlGraph.bytes"));
        typeof(Game.AI.EnemyManager).GetProperty("Graph").SetValue(navigation, graph);
        typeof(Game.AI.EnemyManager).GetProperty("Pathfinder").SetValue(navigation, new Game.AI.CrawlPathfinder(graph));
        typeof(Game.AI.EnemyManager).GetProperty("Instance").SetValue(null, navigation);
        var bus = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/SchoolBus/SchoolBus.obj"));
        bus.transform.position = new Vector3(0, 0.443f, 1);
        foreach (MeshFilter mesh in bus.GetComponentsInChildren<MeshFilter>())
        {
            mesh.gameObject.layer = LayerMask.NameToLayer("Environment");
            mesh.gameObject.AddComponent<MeshCollider>().sharedMesh = mesh.sharedMesh;
        }
        var motor = duck.GetComponent<CharacterController>();
        // Curved roof shoulders must find an exit even with the buddy underneath
        // or along the bus, where tiny slope movements used to suppress recovery.
        foreach (float x in new[] { 1.2f, -1.2f })
        foreach (float z in new[] { -4.7f, 2f })
        foreach (Vector3 offset in new[] { Vector3.zero, Vector3.back * 4, Vector3.forward * 4, Vector3.right * 8 })
        {
            Physics.SyncTransforms();
            Check(Physics.Raycast(bus.transform.position + new Vector3(x, 10, z), Vector3.down,
                out RaycastHit roof, 12, LayerMask.GetMask("Environment")), "bus roof fixture has support");
            motor.enabled = false;
            duck.transform.position = roof.point + Vector3.up * 0.04f;
            motor.enabled = true;
            head.position = bus.transform.position + new Vector3(x, 1.533f, z) + offset;
            duck.Follow(human.photonView.OwnerActorNr);
            SetPrivate(duck, "corners", Array.Empty<Vector3>());
            SetPrivate(duck, "nextPath", 0f);
            SetPrivate(duck, "nextTraversal", 0f);
            SetPrivate(duck, "nextDropProbe", 0f);
            CallPrivate(duck, "ResetRecovery");
            Physics.SyncTransforms();
            double deadline = EditorApplication.timeSinceStartup + 10;
            while (EditorApplication.timeSinceStartup < deadline && duck.transform.position.y > 0.15f) yield return null;
            Check(!duck.IsDead && duck.transform.position.y < 0.15f,
                $"leaves curved bus roof without respawn: x={x}, z={z}, buddy offset={offset}");
        }
        foreach (float offset in new[] { 0f, 1.1f, -1.1f })
        {
            motor.enabled = false;
            duck.transform.position = bus.transform.position + new Vector3(offset, 1.03f, -4.5f);
            motor.enabled = true;
            head.position = bus.transform.position + new Vector3(offset, 0.057f, -7f);
            duck.Follow(human.photonView.OwnerActorNr);
            SetPrivate(duck, "corners", Array.Empty<Vector3>());
            SetPrivate(duck, "nextPath", 0f);
            CallPrivate(duck, "ResetRecovery");
            Physics.SyncTransforms();
            double deadline = EditorApplication.timeSinceStartup + 8;
            while (EditorApplication.timeSinceStartup < deadline && duck.transform.position.y > 0.15f) yield return null;
            Check(duck.transform.position.z < bus.transform.position.z - 5.3f && duck.transform.position.y < 0.15f,
                $"exits actual bus rear doorway from offset {offset} without respawn ({duck.transform.position})");
        }
        foreach (float offset in new[] { 0f, 1.1f, 1.8f })
        {
            motor.enabled = false;
            duck.transform.position = new Vector3(offset, 0.04f, -4.5f);
            motor.enabled = true;
            head.position = new Vector3(0, 1.933f, 0);
            duck.Follow(human.photonView.OwnerActorNr);
            SetPrivate(duck, "corners", Array.Empty<Vector3>());
            SetPrivate(duck, "nextPath", 0f);
            SetPrivate(duck, "nextTraversal", 0f);
            CallPrivate(duck, "ResetRecovery");
            Physics.SyncTransforms();
            double deadline = EditorApplication.timeSinceStartup + 12;
            bool inside = false;
            while (EditorApplication.timeSinceStartup < deadline)
            {
                yield return null;
                inside = duck.transform.position.z > -3.3f && duck.transform.position.y > 1.3f && duck.transform.position.y < 1.6f;
                if (inside) break;
            }
            Check(inside, $"enters actual bus floor from offset {offset}, including the outside corner ({duck.transform.position})");
        }
        UnityEngine.Object.Destroy(bus);
        typeof(Game.AI.EnemyManager).GetProperty("Instance").SetValue(null, previousNavigation);
        UnityEngine.Object.Destroy(graphObject);
        yield return null;
    }

    static IEnumerator CheckGasStationRoof(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        var navigationObject = new GameObject("Gas station navigation fixture");
        navigationObject.SetActive(false);
        var navigation = navigationObject.AddComponent<Game.AI.EnemyManager>();
        var previousNavigation = Game.AI.EnemyManager.Instance;
        var graph = Game.AI.CrawlGraph.Load(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Scenes/Quackville_CrawlGraph.bytes"));
        typeof(Game.AI.EnemyManager).GetProperty("Graph").SetValue(navigation, graph);
        typeof(Game.AI.EnemyManager).GetProperty("Pathfinder").SetValue(navigation, new Game.AI.CrawlPathfinder(graph));
        typeof(Game.AI.EnemyManager).GetProperty("Instance").SetValue(null, navigation);
        var town = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/Quackville/Quackville_Town.obj"));
        // Quackville scene's town-model placement, required to match the baked graph.
        town.transform.position = new Vector3(-8.2f, 0.47170997f, 15.53724f);
        Bounds gas = default, checkout = default;
        foreach (var mesh in town.GetComponentsInChildren<MeshFilter>())
        {
            mesh.gameObject.layer = LayerMask.NameToLayer("Environment");
            var collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.sharedMesh;
            if (mesh.name == "Gas_Station") gas = collider.bounds;
            if (mesh.name == "Gas_Station_Checkout_0") checkout = collider.bounds;
        }
        Check(gas.size.sqrMagnitude > 0 && checkout.size.sqrMagnitude > 0, "actual gas station and counter loaded");
        var motor = duck.GetComponent<CharacterController>();
        foreach (Vector3 offset in new[] { Vector3.zero, Vector3.right * 0.9f, Vector3.left * 0.9f })
        {
            Vector3 start = checkout.center + offset;
            start.y = checkout.max.y + 0.04f;
            motor.enabled = false; duck.transform.position = start; motor.enabled = true;
            Physics.SyncTransforms();
            Check(Physics.Raycast(new Vector3(start.x, gas.max.y + 3, start.z), Vector3.down,
                out RaycastHit roof, 20, LayerMask.GetMask("Environment")), "gas station rooftop found");
            head.position = roof.point + Vector3.up * 0.5f;
            duck.Follow(human.photonView.OwnerActorNr);
            CallPrivate(duck, "ResetRecovery");
            double deadline = EditorApplication.timeSinceStartup + 19;
            while (EditorApplication.timeSinceStartup < deadline)
            {
                if (duck.transform.position.y > roof.point.y - 0.1f && duck.State == DuckCompanion.Activity.Following) break;
                yield return null;
            }
            Check(duck.transform.position.y > roof.point.y - 0.1f,
                $"reaches actual gas-station roof from counter offset {offset} without respawn");
        }
        UnityEngine.Object.Destroy(town);
        typeof(Game.AI.EnemyManager).GetProperty("Instance").SetValue(null, previousNavigation);
        UnityEngine.Object.Destroy(navigationObject);
        yield return null;
    }

    static IEnumerator CheckRoofLevelAndGaze(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        var objects = new System.Collections.Generic.List<GameObject>();
        Vector3 origin = new Vector3(0, 0, 25);
        GameObject Box(Vector3 center, Vector3 size)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.layer = LayerMask.NameToLayer("Environment");
            box.transform.position = origin + center;
            box.transform.localScale = size;
            objects.Add(box);
            return box;
        }
        Box(new Vector3(0, -0.5f, 0), new Vector3(30, 1, 30));
        Box(new Vector3(0, 4, 0), new Vector3(10, 0.3f, 8));
        Box(new Vector3(-4.85f, 2, 0), new Vector3(0.3f, 4, 8));
        Box(new Vector3(4.85f, 2, 0), new Vector3(0.3f, 4, 8));
        Box(new Vector3(0, 2, 3.85f), new Vector3(10, 4, 0.3f));
        var sources = new System.Collections.Generic.List<UnityEngine.AI.NavMeshBuildSource>();
        foreach (var box in objects) sources.Add(new UnityEngine.AI.NavMeshBuildSource {
            shape = UnityEngine.AI.NavMeshBuildSourceShape.Box, transform = box.transform.localToWorldMatrix, size = Vector3.one });
        var settings = UnityEngine.AI.NavMesh.GetSettingsByID(0);
        settings.agentRadius = 0.22f; settings.agentHeight = 0.7f; settings.agentClimb = 0.25f;
        var data = UnityEngine.AI.NavMeshBuilder.BuildNavMeshData(settings, sources, new Bounds(origin, Vector3.one * 40), Vector3.zero, Quaternion.identity);
        var nav = UnityEngine.AI.NavMesh.AddNavMeshData(data);
        var motor = duck.GetComponent<CharacterController>();
        motor.enabled = false; duck.transform.position = origin + Vector3.up * 0.04f; motor.enabled = true;
        head.position = origin + Vector3.up * 4.65f;
        duck.Follow(human.photonView.OwnerActorNr);
        Physics.SyncTransforms();
        bool outside = false, climbed = false;
        double deadline = EditorApplication.timeSinceStartup + 19;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            Vector3 local = duck.transform.position - origin;
            outside |= Mathf.Abs(local.x) > 5 || Mathf.Abs(local.z) > 4;
            climbed |= duck.State == DuckCompanion.Activity.Climbing;
            if (local.y > 4.1f && duck.State == DuckCompanion.Activity.Following) break;
            yield return null;
        }
        Check(outside && climbed && duck.transform.position.y > 4.1f, "leaves interior and climbs to rooftop buddy rather than waiting underneath");
        nav.Remove();
        foreach (var box in objects) UnityEngine.Object.Destroy(box);
        var eyeLook = duck.GetComponent<PhotonEyeLook>();
        Check(eyeLook != null, "companion retains shared player eye system");
        var testEye = new GameObject("Gaze limit probe").transform;
        testEye.SetParent(duck.GetComponentInChildren<Game.Animation.RemotePlayerBody>().Head, false);
        foreach (Vector3 offset in new[] { Vector3.right * 10 + Vector3.up * 20, Vector3.left * 10 - Vector3.up * 20, Vector3.back * 10 })
        {
            head.position = duck.Head.position + offset;
            for (int i = 0; i < 30; i++)
            {
                yield return null;
                Vector2 angles = (Vector2)GetPrivate(duck, "headLookAngles");
                Check(Mathf.Abs(angles.y) <= 55.01f && angles.x >= -25.01f && angles.x <= 30.01f, "head tracking stays within anatomical limits");
                CallPrivate(eyeLook, "Aim", testEye, Quaternion.identity, head.position);
                Vector3 eyeAngles = testEye.localEulerAngles;
                Check(Mathf.Abs(Mathf.DeltaAngle(0, eyeAngles.x)) <= 8.01f && Mathf.Abs(Mathf.DeltaAngle(0, eyeAngles.y)) <= 12.01f,
                    "eye tracking stays within small angular limits");
            }
        }
        UnityEngine.Object.Destroy(testEye.gameObject);
    }

    static IEnumerator CheckWindowTraversal(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        var parts = new System.Collections.Generic.List<GameObject>();
        GameObject Part(Vector3 position, Vector3 size)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.layer = LayerMask.NameToLayer("Environment");
            part.transform.position = position;
            part.transform.localScale = size;
            parts.Add(part);
            return part;
        }
        Part(new Vector3(-15, 0.45f, 15), new Vector3(4, 0.9f, 0.2f));
        Part(new Vector3(-15, 2.4f, 15), new Vector3(4, 1.2f, 0.2f));
        var left = Part(new Vector3(-16.225f, 1.35f, 15), new Vector3(1.55f, 0.9f, 0.2f));
        var right = Part(new Vector3(-13.775f, 1.35f, 15), new Vector3(1.55f, 0.9f, 0.2f));
        Part(new Vector3(-15, 0.25f, 17), new Vector3(4, 0.5f, 4));
        var motor = duck.GetComponent<CharacterController>();
        motor.enabled = false;
        duck.transform.position = new Vector3(-15, 0.04f, 14.5f);
        motor.enabled = true;
        head.position = new Vector3(-15, 1, 19);
        duck.Follow(human.photonView.OwnerActorNr);
        SetPrivate(duck, "corners", Array.Empty<Vector3>());
        SetPrivate(duck, "nextPath", float.PositiveInfinity);
        SetPrivate(duck, "nextTraversal", 0f);
        Physics.SyncTransforms();
        Check((bool)CallPrivate(duck, "ClearBody", new Vector3(-15, 0.94f, 15)), "full body fits the open window");
        double deadline = EditorApplication.timeSinceStartup + 10;
        float peak = duck.transform.position.y;
        bool crossed = false;
        while (EditorApplication.timeSinceStartup < deadline)
        {
            yield return null;
            peak = Mathf.Max(peak, duck.transform.position.y);
            crossed = duck.transform.position.z > 15.3f && duck.transform.position.y < 1.4f;
            if (crossed) break;
        }
        Check(crossed && peak < 1.5f, "climbs through the window below its header instead of climbing onto the roof");
        left.transform.position = new Vector3(-16.0625f, 1.35f, 15);
        right.transform.position = new Vector3(-13.9375f, 1.35f, 15);
        left.transform.localScale = right.transform.localScale = new Vector3(1.875f, 0.9f, 0.2f);
        Physics.SyncTransforms();
        Check(!(bool)CallPrivate(duck, "ClearBody", new Vector3(-15, 0.94f, 15)), "too-narrow window rejects the body capsule");
        foreach (GameObject part in parts) UnityEngine.Object.Destroy(part);
        yield return null;
    }

    static IEnumerator CheckDesktopThrow(DuckCompanion duck, Transform head)
    {
        var motor = duck.GetComponent<CharacterController>();
        motor.enabled = false;
        duck.transform.position = new Vector3(-12, 0.04f, -12);
        motor.enabled = true;
        head.position = duck.transform.position + new Vector3(0, 1.55f, -2.5f);
        head.rotation = Quaternion.LookRotation(duck.transform.position + Vector3.up * 0.35f - head.position);
        SetPrivate(duck, "nextHitReaction", 0d);
        duck.Dialogue.CancelSpeech();
        // Inactive input object lets us invoke the exact E-key action without
        // capturing the cursor or creating a second camera/hand input rig.
        var inputObject = new GameObject("Desktop throw regression");
        inputObject.SetActive(false);
        var input = inputObject.AddComponent<DesktopPlayer>();
        SetPrivate(input, "_camera", head);
        SetPrivate(input, "_yaw", head);
        SetPrivate(input, "testNailBat", AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/WeaponNailBat.prefab"));
        var before = new System.Collections.Generic.HashSet<PhotonGrabbable>(UnityEngine.Object.FindObjectsByType<PhotonGrabbable>(FindObjectsSortMode.None));
        CallPrivate(input, "ThrowTestBat");
        PhotonGrabbable thrown = null;
        foreach (var item in UnityEngine.Object.FindObjectsByType<PhotonGrabbable>(FindObjectsSortMode.None))
            if (!before.Contains(item)) thrown = item;
        Check(thrown != null && !thrown.IsHeld, "desktop E action spawns a thrown bat without Grab");
        double deadline = EditorApplication.timeSinceStartup + 2;
        while (duck.State != DuckCompanion.Activity.Retreating && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(duck.State == DuckCompanion.Activity.Retreating && IsHurtReply(duck.Dialogue.LastReply),
            "desktop E-key bat triggers hurt speech and retreat through production throw action");
        PhotonNetwork.Destroy(thrown.gameObject);
        UnityEngine.Object.Destroy(inputObject);
        while (duck.State == DuckCompanion.Activity.Retreating) yield return null;
    }

    static IEnumerator CheckVrThrow(DuckCompanion duck, Transform head, bool left)
    {
        var motor = duck.GetComponent<CharacterController>();
        motor.enabled = false;
        duck.transform.position = new Vector3(-12, 0.04f, -12);
        motor.enabled = true;
        head.position = duck.transform.position + new Vector3(0, 0.5f, -2);
        SetPrivate(duck, "nextHitReaction", 0d);
        var handObject = new GameObject("VR throw regression");
        var hand = handObject.AddComponent<PlayerHand>();
        hand.enabled = false;
        hand.controller = left ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        hand.transform.position = duck.transform.position + new Vector3(-1.6f, 0.1f, 0);
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/WeaponNailBat.prefab");
        if (left)
        {
            prefab = new GameObject("VR throwable item without locomotion collider");
            prefab.SetActive(false);
            prefab.AddComponent<SphereCollider>().radius = 0.09f;
            prefab.AddComponent<PhotonGrabbable>();
        }
        PhotonPreparePool.Register(prefab);
        var item = PhotonNetwork.Instantiate(prefab.name, hand.transform.position + (left ? Vector3.up * 0.3f : Vector3.zero), Quaternion.identity);
        Check(hand.ForceGrab(item.GetComponent<PhotonGrabbable>()), "VR throw grips the item first");
        for (int sample = 0; sample < 5; sample++)
        {
            hand.transform.position += Vector3.right * (4f * Mathf.Min(Time.deltaTime, 0.05f));
            CallPrivate(hand, "SamplePose");
            yield return null;
        }
        hand.Release();
        Check(!hand.IsHolding && item.GetComponent<Rigidbody>().linearVelocity.x > 0,
            "VR tracked motion produces throw velocity through PlayerHand.Release");
        double deadline = EditorApplication.timeSinceStartup + 2;
        while (duck.State != DuckCompanion.Activity.Retreating && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(duck.State == DuckCompanion.Activity.Retreating && IsHurtReply(duck.Dialogue.LastReply),
            $"{hand.controller} thrown {(left ? "generic item" : "bat")} causes hurt speech and retreat");
        PhotonNetwork.Destroy(item);
        if (left) UnityEngine.Object.Destroy(prefab);
        UnityEngine.Object.Destroy(handObject);
        while (duck.State == DuckCompanion.Activity.Retreating) yield return null;
    }

    static IEnumerator CheckThrownContact(DuckCompanion duck, Transform head)
    {
        head.position = duck.Head.position + Vector3.back * 2;
        GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/WeaponNailBat.prefab");
        PhotonPreparePool.Register(template);
        GameObject item = PhotonNetwork.Instantiate(template.name,
            duck.transform.position + Vector3.left * 0.8f + Vector3.up * 0.1f, Quaternion.identity);
        var grabbable = item.GetComponent<PhotonGrabbable>();
        grabbable.NotifyEquipmentDropped();
        // Unity still delivers trigger contacts to disabled behaviours. Disabling
        // the render sweep here proves the physics-contact fallback independently.
        grabbable.enabled = false;
        Rigidbody body = item.GetComponent<Rigidbody>();
        body.isKinematic = false;
        body.detectCollisions = true;
        body.linearVelocity = Vector3.right * 5f;
        Physics.SyncTransforms();
        double deadline = EditorApplication.timeSinceStartup + 1;
        while (duck.State != DuckCompanion.Activity.Retreating && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(duck.State == DuckCompanion.Activity.Retreating && IsHurtReply(duck.Dialogue.LastReply),
            "thrown nail bat physics contact reacts even without render sweeps");
        PhotonNetwork.Destroy(item);
        while (duck.State == DuckCompanion.Activity.Retreating) yield return null;
    }

    static void CheckItemSweep(DuckCompanion duck, bool fallback, bool roomOwned = false, bool leftHand = false)
    {
        GameObject template;
        if (fallback)
        {
            template = new GameObject("Fixture sphere item");
            template.SetActive(false);
            template.AddComponent<SphereCollider>().radius = 0.06f;
            template.AddComponent<PhotonGrabbable>();
        }
        else template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/WeaponNailBat.prefab");
        PhotonPreparePool.Register(template);
        var go = PhotonNetwork.Instantiate(template.name, duck.transform.position + Vector3.up * 0.32f, Quaternion.identity);
        var item = go.GetComponent<PhotonGrabbable>();
        var handObject = new GameObject("VR weapon hand regression");
        var hand = handObject.AddComponent<PlayerHand>();
        hand.enabled = false; // Drive tracked poses directly, without device input.
        hand.controller = leftHand ? OVRInput.Controller.LTouch : OVRInput.Controller.RTouch;
        Check(hand.ForceGrab(item) && item.IsHeld && !go.GetComponent<Rigidbody>().detectCollisions,
            $"{hand.controller} uses real grip path with physics contacts disabled");
        if (roomOwned)
        {
            hand.Release();
            var view = go.GetComponent<PhotonView>();
            view.OwnerActorNr = 0;
            view.ControllerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        }
        var samples = new Vector3[Grabbable.MaxLocomotionSamples];
        if (item.CopySampleWorldPositions(samples) > 0)
            go.transform.position += duck.transform.position + Vector3.up * 0.32f - samples[0];
        var sweep = typeof(PhotonGrabbable).GetMethod("TickSwing", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        sweep.Invoke(item, null);
        if (item.IsHeld) hand.transform.position += Vector3.right * Mathf.Max(0.0001f, Time.deltaTime);
        else go.transform.position += Vector3.right * Mathf.Max(0.0001f, Time.deltaTime);
        sweep.Invoke(item, null);
        if (item.IsHeld) hand.Release();
        PhotonNetwork.Destroy(go);
        UnityEngine.Object.Destroy(handObject);
        if (fallback) UnityEngine.Object.Destroy(template);
    }

    static IEnumerator CheckRemoteHitReaction(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        // A separate receiver exercises the production stream/RPC handlers. This
        // is a protocol regression, not a replacement for a two-device playtest.
        GameObject prefab = Resources.Load<GameObject>("DuckCompanion");
        PhotonPreparePool.Register(prefab);
        var peerObject = PhotonNetwork.Instantiate(prefab.name, duck.transform.position, Quaternion.identity);
        var peer = peerObject.GetComponent<DuckCompanion>();
        peer.photonView.OwnerActorNr = 2;
        peer.photonView.ControllerActorNr = 2;
        yield return null;
        Check(!peer.HasAuthority, "remote companion never simulates retreat independently");
        head.position = duck.transform.position + Vector3.back * 2 + Vector3.up * 0.5f;
        SetPrivate(duck, "nextHitReaction", 0d);
        Check(duck.ReactToPlayerHit(human.photonView.OwnerActorNr, Vector3.forward * 4), "network reaction starts on authority");
        string selectedLine = duck.Dialogue.LastReply;
        var writer = new PhotonStream(true, null);
        SetPrivate(writer, "writeData", new System.Collections.Generic.List<object>());
        duck.GetComponent<PhotonPlayer>().OnPhotonSerializeView(writer, default);
        var reader = new PhotonStream(false, writer.ToArray());
        peer.GetComponent<PhotonPlayer>().OnPhotonSerializeView(reader, new PhotonMessageInfo(PhotonNetwork.MasterClient, PhotonNetwork.ServerTimestamp, peer.photonView));
        Check(peer.State == DuckCompanion.Activity.Retreating && peer.TargetActor == duck.TargetActor
            && Vector3.Distance(peer.transform.position, duck.transform.position) < 0.001f,
            "remote avatar receives retreat position, state and followed player through PhotonPlayer stream");
        duck.Dialogue.enabled = peer.Dialogue.enabled = false;
        int id = (int)GetPrivate(duck.Dialogue, "sequence");
        var masterInfo = new PhotonMessageInfo(PhotonNetwork.MasterClient, PhotonNetwork.ServerTimestamp, peer.photonView);
        CallPrivate(peer.Dialogue, "PrepareLine", id, selectedLine, DuckLocalSpeech.DefaultVoice, masterInfo);
        double deadline = EditorApplication.timeSinceStartup + 30;
        while ((GetPrivate(duck.Dialogue, "prepared") == null || GetPrivate(peer.Dialogue, "prepared") == null)
            && EditorApplication.timeSinceStartup < deadline) yield return null;
        Check(GetPrivate(duck.Dialogue, "prepared") != null && GetPrivate(peer.Dialogue, "prepared") != null
            && peer.Dialogue.LastReply == selectedLine, "both peers prepare the host-selected hurt line locally");
        double start = PhotonNetwork.Time + 0.1;
        CallPrivate(duck.Dialogue, "StartLine", id, start, masterInfo);
        CallPrivate(peer.Dialogue, "StartLine", id, start, masterInfo);
        while (PhotonNetwork.Time < start) yield return null;
        CallPrivate(duck.Dialogue, "Update");
        CallPrivate(peer.Dialogue, "Update");
        Check(duck.Dialogue.Status == "Speaking" && peer.Dialogue.Status == "Speaking"
            && duck.GetComponent<PhotonPlayer>().VoiceSource.clip != null && peer.GetComponent<PhotonPlayer>().VoiceSource.clip != null,
            "shared Photon timestamp starts the hurt clip on both avatar voice sources");
        CheckOverallVoicePitch(duck.Dialogue, masterInfo);
        CheckOverallVoicePitch(peer.Dialogue, masterInfo);
        duck.Dialogue.enabled = true;
        peer.photonView.OwnerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        peer.photonView.ControllerActorNr = PhotonNetwork.LocalPlayer.ActorNumber;
        PhotonNetwork.Destroy(peerObject);
        while (duck.State == DuckCompanion.Activity.Retreating) yield return null;
    }

    static void CheckOverallVoicePitch(DuckCompanionDialogue dialogue, PhotonMessageInfo masterInfo)
    {
        var voice = dialogue.GetComponent<PhotonPlayer>().VoiceSource;
        var clip = AudioClip.Create("Overall voice pitch fixture", 48000, 1, 24000, false);
        dialogue.CancelSpeech();
        SetPrivate(dialogue, "sequence", 9100);
        SetPrivate(dialogue, "preparing", true);
        SetPrivate(dialogue, "prepared", clip);
        CallPrivate(dialogue, "StartLine", 9100, PhotonNetwork.Time - 0.5, masterInfo);
        CallPrivate(dialogue, "Update");
        Check(Mathf.Abs(voice.pitch - 1.1f) < 0.001f, "all dialogue plays at ten percent higher pitch");
        Check(Mathf.Abs(voice.time - 0.55f) < 0.06f, "network start offset scales with higher pitch");
        voice.Stop();
        CallPrivate(dialogue, "Update");
        Check(Mathf.Abs(voice.pitch - 1.1f) < 0.001f, "completion retains new default pitch");
        dialogue.CancelSpeech();
        Check(Mathf.Abs(voice.pitch - 1.1f) < 0.001f, "cancellation retains new default pitch");
        UnityEngine.Object.Destroy(clip);
    }

    static object GetPrivate(object target, string name) => target.GetType().GetField(name,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(target);

    static void SetPrivate(object target, string name, object value) => target.GetType().GetField(name,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).SetValue(target, value);

    static object CallPrivate(object target, string name, params object[] args) => target.GetType().GetMethod(name,
        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(target, args);

    static void CheckScoutAndSpeechRules(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        var motor = duck.GetComponent<CharacterController>();
        Vector3 savedPosition = duck.transform.position;
        Vector3 savedHead = head.position;
        motor.enabled = false;
        duck.transform.position = new Vector3(-25, 0.04f, -25);
        motor.enabled = true;
        Physics.SyncTransforms();
        head.position = duck.transform.position + Vector3.up * 0.5f;
        duck.Follow(human.photonView.OwnerActorNr);
        var dialogue = duck.Dialogue;
        dialogue.CancelSpeech();
        SetPrivate(dialogue, "nextReactive", 0f);
        SetPrivate(duck, "greeted", true);
        SetPrivate(duck, "groundSupported", true);
        SetPrivate(duck, "nextWanderAt", 0d);
        Check((bool)CallPrivate(duck, "TryStartWander", duck.transform.position)
            && duck.State == DuckCompanion.Activity.Wandering, "safe supported scout starts");
        Check(Array.Exists(JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text).groups,
            group => group.intent == "wander" && Array.IndexOf(group.lines, dialogue.LastReply) >= 0), "scout departure uses authored network dialogue");
        float scoutDistance = Vector3.Distance((Vector3)GetPrivate(duck, "wanderDestination"), duck.transform.position);
        Check(scoutDistance >= 20f && scoutDistance <= 40f && (bool)GetPrivate(duck, "exploring"), "scout destination uses the new 20-40 metre range");
        CallPrivate(duck, "EndWander", true);
        Check((bool)GetPrivate(duck, "returningFromExplore"), "ending scout queues a reunion instead of speaking remotely");
        duck.Follow(human.photonView.OwnerActorNr);
        Check(duck.State == DuckCompanion.Activity.Following, "follow command cancels scout");
        Check(!(bool)GetPrivate(duck, "exploring") && !(bool)GetPrivate(duck, "returningFromExplore"), "explicit follow clears scouting and pending return");
        var enemy = new GameObject("Fixture nearby enemy").AddComponent<Game.AI.Enemy>();
        enemy.transform.position = duck.transform.position + Vector3.right * 25;
        Check(dialogue.ThreatNearby(), "enemy at twenty-five metres is detected");
        enemy.IsDormant = true;
        Check(!dialogue.ThreatNearby(), "dormant enemy does not warn");
        enemy.IsDormant = false;
        enemy.transform.position += Vector3.right * 0.1f;
        Check(!dialogue.ThreatNearby(), "enemy outside twenty-five metres does not warn");
        UnityEngine.Object.DestroyImmediate(enemy.gameObject);
        dialogue.CancelSpeech();
        SetPrivate(dialogue, "nextReactive", 0f);
        dialogue.React("school");
        string sentence = dialogue.LastReply;
        dialogue.ReactAirborne();
        Check(dialogue.Busy && dialogue.LastReply == sentence, "airborne event preserves a preparing sentence");
        dialogue.CancelSpeech();
        AudioSource voice = duck.GetComponent<PhotonPlayer>().VoiceSource;
        var clip = AudioClip.Create("Fixture sentence", 16000, 1, 16000, false);
        voice.clip = clip;
        voice.Play();
        dialogue.ReactAirborne();
        Check(voice.isPlaying && dialogue.LastReply == sentence, "airborne event preserves a playing sentence");
        voice.Stop();
        voice.clip = null;
        UnityEngine.Object.Destroy(clip);
        // Long-running recovery cases below should not start unrelated scouting.
        SetPrivate(duck, "allowWandering", false);
        motor.enabled = false;
        duck.transform.position = savedPosition;
        motor.enabled = true;
        head.position = savedHead;
        duck.Follow(human.photonView.OwnerActorNr);
    }

    static void CheckMeshHandRecovery(DuckCompanion duck, Game.Animation.GorillaGait gait)
    {
        var seat = GameObject.CreatePrimitive(PrimitiveType.Cube);
        seat.layer = LayerMask.NameToLayer("Environment");
        seat.transform.position = duck.transform.position + new Vector3(0.28f, 0.7f, 0);
        seat.transform.localScale = new Vector3(0.08f, 1.4f, 2);
        Mesh mesh = seat.GetComponent<MeshFilter>().sharedMesh;
        UnityEngine.Object.DestroyImmediate(seat.GetComponent<BoxCollider>());
        seat.AddComponent<MeshCollider>().sharedMesh = mesh;
        Physics.SyncTransforms();
        duck.RightHand.position = seat.transform.position;
        gait.ConstrainPoseTargets();
        Check(!Physics.CheckSphere(duck.RightHand.position, 0.045f, LayerMask.GetMask("Environment"), QueryTriggerInteraction.Ignore),
            "hand embedded in a mesh seat recovers to free space");
        seat.SetActive(false);
        UnityEngine.Object.Destroy(seat);
        gait.ResetPose();
    }

    static void CheckKillerWarningRules(DuckCompanion duck, PhotonPlayer human)
    {
        Check(!DuckCompanion.ShouldRespawnAfterKill(9.99, 5), "Duck death waits at least ten seconds");
        Check(!DuckCompanion.ShouldRespawnAfterKill(15, 1.99)
            && DuckCompanion.ShouldRespawnAfterKill(15, 2), "Duck post-kill speech has a two-second respawn buffer");
        Check(DuckCompanion.ShouldRespawnAfterKill(25, double.NegativeInfinity), "failed speech cannot extend respawn past twenty-five seconds");
        foreach (string weapon in new[] { "WeaponNailBat", "WeaponFryPan" })
        {
            var sounds = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Weapons/" + weapon + ".prefab").GetComponent<ItemSounds>();
            Check(sounds != null && sounds.Get(ItemSound.Grab) != null && sounds.Get(ItemSound.Swing) != null
                && sounds.Get(ItemSound.FleshImpact) != null, "companion weapon has authored pickup, swing and flesh sounds: " + weapon);
        }
        var authored = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/DuckCompanion.prefab");
        var settings = new SerializedObject(authored.GetComponent<DuckCompanion>());
        var jumpEvent = (Game.Audio.SoundEvent)new SerializedObject(authored.GetComponent<DuckCompanion>()).FindProperty("jumpSound").objectReferenceValue;
        Check(jumpEvent != null && jumpEvent.TrySelect(null, out var jumpSelection)
            && jumpSelection.Clip.name == "DuckCompanion_Jump", "companion jump event uses the authored jump clip");
        Check(Resources.Load<Game.Audio.AudioSettings>("GameAudioSettings").FindSound(jumpEvent.name) == jumpEvent,
            "companion jump event is registered for network playback");
        Check(Mathf.Approximately(settings.FindProperty("killerAttackChance").floatValue, 0.2f)
            && Mathf.Approximately(settings.FindProperty("killerAttackCooldown").floatValue, 180f),
            "spawned prefab uses configured attack chance and cooldown, not stale serialized defaults");
        var bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        foreach (string topic in new[] { "killer_before", "killer_after" })
            Check(Array.Find(bank.groups, g => g.intent == topic).lines.Length >= 50, "fifty possessive lines: " + topic);
        var phase = typeof(DuckCompanion).GetField("killerPhase", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        duck.Follow(human.photonView.OwnerActorNr);
        duck.Dialogue.CancelSpeech();
        SetPrivate(duck, "killerActor", human.photonView.OwnerActorNr);
        SetPrivate(duck, "killerDeadline", PhotonNetwork.Time + 60);
        SetPrivate(duck, "killerLineSequence", duck.Dialogue.SpeechSequence);
        SetPrivate(duck, "killerHeardWarning", false);
        phase.SetValue(duck, Enum.Parse(phase.FieldType, "Warning"));
        CallPrivate(duck, "TickKillerAttack");
        Check(!duck.KillerSequenceActive, "missing audible warning never permits an attack");
        SetPrivate(duck, "killerDeadline", PhotonNetwork.Time + 60);
        SetPrivate(duck, "killerLineSequence", duck.Dialogue.SpeechSequence);
        SetPrivate(duck, "killerHeardWarning", true);
        phase.SetValue(duck, Enum.Parse(phase.FieldType, "Warning"));
        duck.Dialogue.CancelSpeech();
        CallPrivate(duck, "TickKillerAttack");
        Check(!duck.KillerSequenceActive, "interrupted warning cancels attack");
        // Keep unrelated movement tests deterministic throughout this play-check session.
        SetPrivate(duck, "killerAttackChance", 0f);
    }

    static void CheckCreepyPersonality(DuckCompanion duck)
    {
        var bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        Check(Array.Find(bank.groups, g => g.intent == "creepy").lines.Length >= 50, "at least fifty unsettling personality lines");
        var randomState = UnityEngine.Random.state;
        UnityEngine.Random.InitState(7163);
        SetPrivate(duck.Dialogue, "creepyDialogueChance", 0.4f);
        int creepy = 0;
        for (int i = 0; i < 10000; i++)
            if ((string)CallPrivate(duck.Dialogue, "SelectPersonalityTopic", "town") == "creepy") creepy++;
        Check(creepy > 3800 && creepy < 4200, "personality slips occur approximately forty percent of the time");
        SetPrivate(duck.Dialogue, "commandRefusalChance", 0.4f);
        foreach (string command in new[] { "follow", "wait", "quiet", "dismiss" })
        {
            Check(Array.Find(bank.groups, g => g.intent == "refuse_" + command).lines.Length >= 10, "command-specific refusal lines: " + command);
            int refusals = 0;
            for (int i = 0; i < 10000; i++)
                if ((bool)CallPrivate(duck.Dialogue, "ShouldRefuseCommand", command)) refusals++;
            Check(refusals > 3800 && refusals < 4200, "forty percent command refusal: " + command);
        }
        SetPrivate(duck.Dialogue, "commandRefusalChance", 1f);
        foreach (string topic in new[] { "acknowledge", "gold", "unknown", "creepy" })
            Check(!(bool)CallPrivate(duck.Dialogue, "ShouldRefuseCommand", topic), "non-commands cannot be refused: " + topic);
        SetPrivate(duck.Dialogue, "commandRefusalChance", 0f);
        UnityEngine.Random.state = randomState;
        Check(DuckCompanionDialogue.Intent("creepy duck follow me") == "follow"
            && DuckCompanionDialogue.Intent("be quiet you imposter") == "quiet"
            && DuckCompanionDialogue.Intent("can you hear me creepy duck") == "acknowledge",
            "commands and hearing acknowledgments take priority over creepy keywords");
        // The rest of this suite asserts ordinary topic identities deterministically.
        SetPrivate(duck.Dialogue, "creepyDialogueChance", 0f);
    }

    static void CheckCompanionExpressiveness(DuckCompanion duck)
    {
        foreach (string question in new[] { "what is your favorite drink", "what's your favourite beverage", "what drinks did you enjoy growing up", "do you like tea", "what do you drink", "did you enjoy lemonade at school" })
            Check(DuckCompanionDialogue.Intent(question) == "favorite_drinks"
                && DuckCompanionDialogue.AcceptsSpeech(question, false, false), "favorite drink question: " + question);
        Check(DuckCompanionDialogue.Intent("follow me for a drink") == "follow"
            && DuckCompanionDialogue.Intent("what is your favorite food") == "creepy_food", "drink preferences preserve commands and food dialogue");
        foreach (string question in new[] { "what is your favorite music", "what's your favourite song", "what songs did you enjoy growing up", "what music do you listen to on the radio", "what songs did you like at school" })
            Check(DuckCompanionDialogue.Intent(question) == "favorite_music"
                && DuckCompanionDialogue.AcceptsSpeech(question, false, false), "favorite music question: " + question);
        Check(DuckCompanionDialogue.Intent("be quiet I like this song") == "quiet"
            && DuckCompanionDialogue.Intent("how do I use the radio") == "walkie_talkie", "music questions preserve commands and radio equipment advice");
        foreach (string question in new[] { "what is your favorite movie", "what's your favourite film", "what movies do you like", "have you seen any old films", "do you enjoy black and white movies" })
            Check(DuckCompanionDialogue.Intent(question) == "favorite_movies"
                && DuckCompanionDialogue.AcceptsSpeech(question, false, false), "favorite movie question: " + question);
        Check(DuckCompanionDialogue.Intent("follow me to the movies") == "follow"
            && DuckCompanionDialogue.Intent("tell me about the cinema") == "cinema", "movie preferences preserve commands and cinema memories");
        foreach (string request in new[] { "take me to the helicopter", "show me where the helicopter is", "which way to the chopper", "where is the helicopter", "show us the supplies" })
            Check(DuckCompanionDialogue.Intent(request) == "helicopter_directions"
                && DuckCompanionDialogue.AcceptsSpeech(request, false, false), "verbal helicopter direction request: " + request);
        foreach (string question in new[] { "supplies", "where can I find supplies", "survival gear", "I need gear", "helicopter supplies" })
            Check(DuckCompanionDialogue.Intent(question) == "helicopter", "helicopter supplies routing: " + question);
        Check(DuckCompanionDialogue.Intent("flashlight gear") == "flashlight"
            && DuckCompanionDialogue.Intent("follow me to the supplies") == "follow"
            && DuckCompanionDialogue.Intent("helicopter supplies when you were a kid") == "helicopter_memories"
            && DuckCompanionDialogue.Intent("barn supplies") == "barn"
            && DuckCompanionDialogue.Intent("survival tips") == "survival"
            && DuckCompanionDialogue.Intent("gearbox") == "unknown", "supply keywords preserve specific topics and whole-word matching");
        var memories = new System.Collections.Generic.HashSet<string> { "town", "school", "river", "woods", "factory", "store", "cinema", "apartments", "warehouse", "subway", "caves", "sheriff", "rambro_memories", "gas_station", "church", "barn", "helicopter_memories", "diner", "water_tower", "bus_memories", "truck_memories", "helipad_memories" };
        int memoryCount = 0;
        var otherTopics = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 38; i++)
        {
            string topic = (string)CallPrivate(duck.Dialogue, "ChooseSmallTalkTopic");
            if (memories.Contains(topic)) memoryCount++;
            else Check(otherTopics.Add(topic), "other casual topics still cycle without repeats");
        }
        Check(memoryCount == 19 && otherTopics.Count == 19, "half of casual topics are childhood memories");
        foreach (string topic in new[] { "military_sounds", "factory_banging", "crows", "thunder" })
            Check(otherTopics.Contains(topic), "ambient subject appears in casual chatter: " + topic);
        var stateProperty = typeof(DuckCompanion).GetProperty("State");
        var previousState = duck.State;
        stateProperty.SetValue(duck, DuckCompanion.Activity.Following);
        SetPrivate(duck, "groundSupported", true);
        SetPrivate(duck, "gestureSequence", duck.Dialogue.SpeechSequence);
        for (int style = 0; style < 6; style++)
        {
            SetPrivate(duck, "speakingGesture", style);
            SetPrivate(duck, "gestureWeight", 1f);
            Vector3 left = duck.LeftHand.position, right = duck.RightHand.position;
            CallPrivate(duck, "PoseSpeakingArms", 0f, true);
            bool leftMoved = Vector3.Distance(left, duck.LeftHand.position) > 0.01f;
            bool rightMoved = Vector3.Distance(right, duck.RightHand.position) > 0.01f;
            Check(leftMoved == (style == 0 || style == 2 || style == 3 || style == 4)
                && rightMoved == (style == 1 || style == 2 || style == 3 || style == 5), "speaking arm style " + style);
            duck.LeftHand.position = left; duck.RightHand.position = right;
        }
        stateProperty.SetValue(duck, DuckCompanion.Activity.Climbing);
        SetPrivate(duck, "traversalStart", duck.transform.position - Vector3.up * 0.56f);
        float oldTime = (float)GetPrivate(duck, "traversalTime"), oldLift = (float)GetPrivate(duck, "climbLiftDuration");
        SetPrivate(duck, "traversalTime", 0f); SetPrivate(duck, "climbLiftDuration", 10f);
        for (int i = 0; i < 60; i++) CallPrivate(duck, "PoseHandExpression", 1f, true);
        var curls = (Game.Animation.FingerCurl)GetPrivate(duck, "expressiveFingers");
        Check(curls.leftIndex < curls.rightIndex && curls.leftIndex >= 0 && curls.rightPinky <= 1,
            "climbing releases reaching fingers and curls the holding hand using player finger values");
        foreach (string field in new[] { "leftWristAngles", "rightWristAngles" })
        {
            Vector3 angles = (Vector3)GetPrivate(duck, field);
            Check(Mathf.Abs(angles.x) <= 20 && Mathf.Abs(angles.y) <= 12 && Mathf.Abs(angles.z) <= 15, "bounded wrist rotation: " + field);
        }
        SetPrivate(duck, "traversalTime", oldTime); SetPrivate(duck, "climbLiftDuration", oldLift);
        stateProperty.SetValue(duck, previousState);
    }

    static void CheckNearbyPlaceMemories(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        var dialogue = duck.Dialogue;
        var motor = duck.GetComponent<CharacterController>();
        Vector3 oldDuck = duck.transform.position, oldHead = head.position;
        var landmark = GameObject.CreatePrimitive(PrimitiveType.Cube);
        landmark.name = "Barn";
        landmark.transform.position = new Vector3(1000, 0, 1000);
        landmark.transform.localScale = new Vector3(8, 4, 8);
        try
        {
            duck.Follow(human.photonView.OwnerActorNr);
            motor.enabled = false;
            duck.transform.position = landmark.transform.position + Vector3.right * 5;
            head.position = duck.transform.position + Vector3.up;
            SetPrivate(dialogue, "nearbyMemoryChance", 1f);
            SetPrivate(dialogue, "nextLandmarkScan", 0d);
            SetPrivate(dialogue, "nextNearbyMemory", 0d);
            var cooldowns = (System.Collections.Generic.Dictionary<string, double>)GetPrivate(dialogue, "nearbyMemoryCooldowns");
            cooldowns.Clear();
            Physics.SyncTransforms();
            Check((bool)CallPrivate(dialogue, "HasPriorityConversation"), "nearby landmark takes priority over ambient remarks");
            Check((string)CallPrivate(dialogue, "ChooseNearbyMemory") == "barn", "nearby barn selects barn memories");
            Check(CallPrivate(dialogue, "ChooseNearbyMemory") == null, "location remarks respect global cooldown");
            SetPrivate(dialogue, "nextNearbyMemory", 0d);
            Check(CallPrivate(dialogue, "ChooseNearbyMemory") == null, "same landmark respects five-minute topic cooldown");
            Check((bool)CallPrivate(dialogue, "HasPriorityConversation"), "landmark priority survives its repeat cooldown");
            landmark.name = "School_Bus";
            SetPrivate(dialogue, "nextLandmarkScan", 0d);
            bool school = false, bus = false;
            for (int i = 0; i < 60; i++)
            {
                cooldowns.Clear(); SetPrivate(dialogue, "nextNearbyMemory", 0d);
                string topic = (string)CallPrivate(dialogue, "ChooseNearbyMemory");
                school |= topic == "school"; bus |= topic == "bus_memories";
                Check(topic == "school" || topic == "bus_memories", "school bus selects only school or bus memories");
            }
            Check(school && bus, "school bus offers classroom and bus-ride memories");
            cooldowns.Clear(); SetPrivate(dialogue, "nextNearbyMemory", 0d);
            head.position += Vector3.right * 30;
            Check(CallPrivate(dialogue, "ChooseNearbyMemory") == null, "distant buddy prevents location remark");
            head.position = duck.transform.position + Vector3.up;
            landmark.SetActive(false);
            Check(CallPrivate(dialogue, "ChooseNearbyMemory") == null, "inactive landmark cannot trigger memories");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(landmark);
            duck.transform.position = oldDuck; motor.enabled = true; head.position = oldHead;
            SetPrivate(dialogue, "nearbyMemoryChance", 0f); // Other tests assert ordinary chatter decks.
            SetPrivate(dialogue, "nextLandmarkScan", 0d);
        }
    }

    static void CheckAmbientPlayback(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        var dialogue = duck.Dialogue;
        var flags = System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic;
        var map = typeof(DuckCompanionDialogue).GetMethod("AmbientTopic", flags);
        string[,] categories = { { "Audio Source - Quackville_City_Military_02", "military_sounds" },
            { "City", "city" }, { "Town", "town" }, { "Forest", "crows" },
            { "Industrial", "factory_banging" }, { "Thunder", "thunder" } };
        for (int i = 0; i < categories.GetLength(0); i++)
            Check((string)map.Invoke(null, new object[] { categories[i, 0] }) == categories[i, 1], "ambient source category: " + categories[i, 0]);
        var obj = new GameObject("Audio Source - Quackville_Industrial_01");
        var ambient = obj.AddComponent<AudioSource>();
        var clip = AudioClip.Create("Ambient playback check", 44100 * 4, 1, 44100, false);
        var samples = new float[44100 * 4];
        for (int i = 0; i < samples.Length; i++) samples[i] = Mathf.Sin(i * .03f) * .05f;
        clip.SetData(samples, 0);
        ambient.clip = clip; ambient.loop = true; ambient.spatialBlend = 1;
        ambient.rolloffMode = AudioRolloffMode.Linear; ambient.minDistance = 1; ambient.maxDistance = 30;
        obj.transform.position = duck.Head.position;
        head.position = duck.Head.position + Vector3.right;
        duck.Follow(human.photonView.OwnerActorNr); dialogue.CancelSpeech();
        SetPrivate(dialogue, "ambientReactionChance", 1f);
        SetPrivate(dialogue, "nextAmbientAfterSpeech", 0f);
        // Isolate playback checks from the current scene's nearby landmarks.
        ((System.Collections.IList)GetPrivate(dialogue, "nearbyLandmarks")).Clear();
        SetPrivate(dialogue, "nextLandmarkScan", double.MaxValue);
        SetPrivate(dialogue, "childhoodAfterIntroduction", false);
        SetPrivate(dialogue, "nextAmbientScan", 0d); SetPrivate(dialogue, "nextAmbientPoll", 0d);
        SetPrivate(dialogue, "nextReactive", 0f); SetPrivate(dialogue, "lastConversation", float.NegativeInfinity);
        try
        {
            ambient.mute = true; ambient.Play();
            int sequence = dialogue.SpeechSequence;
            CallPrivate(dialogue, "CheckAmbientSounds");
            Check(dialogue.SpeechSequence == sequence, "muted ambience cannot trigger speech");
            ambient.mute = false; SetPrivate(dialogue, "nextAmbientPoll", 0d);
            SetPrivate(dialogue, "deferredTopic", "gas_station");
            CallPrivate(dialogue, "CheckAmbientSounds");
            Check(dialogue.SpeechSequence == sequence, "pending contextual reply blocks an actual ambient playback reaction");
            SetPrivate(dialogue, "deferredTopic", null);
            SetPrivate(dialogue, "lastConversation", Time.time - 19f);
            Check((bool)CallPrivate(dialogue, "HasPriorityConversation"), "player conversation keeps priority for twenty seconds");
            SetPrivate(dialogue, "lastConversation", Time.time - 21f);
            Check(!(bool)CallPrivate(dialogue, "HasPriorityConversation"), "ambient conversation priority expires after twenty seconds");
            SetPrivate(dialogue, "lastConversation", float.NegativeInfinity);
            ((System.Collections.IDictionary)GetPrivate(dialogue, "ambientSources")).Clear();
            SetPrivate(dialogue, "nextAmbientScan", 0d); SetPrivate(dialogue, "nextAmbientPoll", 0d);
            CallPrivate(dialogue, "CheckAmbientSounds");
            Check(dialogue.SpeechSequence > sequence, "actual audible source start triggers shared speech");
            var bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
            Check(Array.IndexOf(Array.Find(bank.groups, g => g.intent == "factory_banging").lines, dialogue.LastReply) >= 0,
                "industrial playback chooses factory-banging dialogue");
            Check((double)GetPrivate(dialogue, "nextAmbientReaction") >= PhotonNetwork.Time + 119,
                "ambient reaction starts two-minute global cooldown");
            var cooldowns = (System.Collections.Generic.Dictionary<string, double>)GetPrivate(dialogue, "ambientTopicCooldowns");
            Check(cooldowns["factory_banging"] >= PhotonNetwork.Time + 299, "ambient topic starts five-minute cooldown");
            dialogue.CancelSpeech(); sequence = dialogue.SpeechSequence;
            SetPrivate(dialogue, "nextAmbientPoll", 0d); CallPrivate(dialogue, "CheckAmbientSounds");
            Check(dialogue.SpeechSequence == sequence, "continuing loop does not trigger a reply every poll");
            var audible = typeof(DuckCompanionDialogue).GetMethod("AmbientAudibleAt", flags);
            Check(!(bool)audible.Invoke(null, new object[] { ambient, obj.transform.position + Vector3.right * 50 }),
                "out-of-range spatial ambience cannot trigger a reply");
            ambient.Stop();
            Check(!(bool)audible.Invoke(null, new object[] { ambient, obj.transform.position }), "stopped source is not audible");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(obj); UnityEngine.Object.DestroyImmediate(clip);
            dialogue.CancelSpeech(); SetPrivate(dialogue, "ambientReactionChance", 0f);
            SetPrivate(dialogue, "nextLandmarkScan", 0d);
        }
    }

    static void CheckAcknowledgments(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        CheckAmbientPlayback(duck, human, head);
        string[,] ambientCases = {
            { "military", "military_sounds" },
            { "army near the city", "military_sounds" },
            { "those distant gunshots", "military_sounds" },
            { "guns", "military_sounds" },
            { "can the military save us", "military_sounds" },
            { "factory banging", "factory_banging" },
            { "loud machinery", "factory_banging" },
            { "clanging at the factory", "factory_banging" },
            { "the factory", "factory" },
            { "crows in the woods", "crows" },
            { "annoying cawing", "crows" },
            { "ravens", "crows" },
            { "thunder", "thunder" },
            { "lightning over the church", "thunder" },
            { "the power went out", "thunder" },
            { "lights went out", "thunder" },
            { "follow me through the storm", "follow" },
            { "be quiet about the army", "quiet" },
            { "crown", "unknown" },
            { "gunmetal", "unknown" }
        };
        for (int i = 0; i < ambientCases.GetLength(0); i++)
            Check(DuckCompanionDialogue.Intent(ambientCases[i, 0]) == ambientCases[i, 1], "ambient topic routing: " + ambientCases[i, 0]);
        var ambientBank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        foreach (string topic in new[] { "military_sounds", "factory_banging", "crows", "thunder" })
            Check(Array.Find(ambientBank.groups, g => g.intent == topic).lines.Length == 30, "thirty ambient replies: " + topic);

        CheckNearbyPlaceMemories(duck, human, head);
        string[,] placeCases = {
            { "the barn animals", "barn" },
            { "the farmhouse", "barn" },
            { "the classroom", "school" },
            { "school bus", "bus_memories" },
            { "schoolbus", "bus_memories" },
            { "truck", "truck_memories" },
            { "delivery truck", "truck_memories" },
            { "gas pumps", "gas_station" },
            { "petrol station", "gas_station" },
            { "police station", "sheriff" },
            { "sheriff's office", "sheriff" },
            { "church pews", "church" },
            { "movie theatre", "cinema" },
            { "apartment block", "apartments" },
            { "loading dock", "warehouse" },
            { "train platform", "subway" },
            { "the old mine", "caves" },
            { "the cave", "caves" },
            { "the general store sells food", "store" },
            { "my friend at the factory", "factory" },
            { "creepy memories about the barn", "barn" },
            { "the creek bridge", "river" },
            { "campfire marshmallows", "woods" },
            { "water tower", "water_tower" },
            { "landing pad", "helipad_memories" },
            { "the diner serves burgers", "diner" },
            { "quackville", "town" },
            { "city", "city" },
            { "follow me to the school bus", "follow" },
            { "how do I climb the school bus", "climbing" },
            { "can I jump onto the truck", "jumping" },
            { "a friend of mine", "friendship" },
            { "copy", "unknown" },
            { "scoped", "unknown" }
        };
        for (int i = 0; i < placeCases.GetLength(0); i++)
            Check(DuckCompanionDialogue.Intent(placeCases[i, 0]) == placeCases[i, 1], "place memory routing: " + placeCases[i, 0]);

        foreach (string question in new[] { "who are you?", "why are you here?", "tell me about yourself", "where do you live?", "why do you exist?" })
            Check(DuckCompanionDialogue.Intent(question) == "identity" && DuckCompanionDialogue.AcceptsSpeech(question, false, false), "introduction question accepted: " + question);
        foreach (string question in new[] { "can you be my friend", "hello friend", "hi friend", "let's be buddies" })
            Check(DuckCompanionDialogue.Intent(question) == "friendship" && DuckCompanionDialogue.AcceptsSpeech(question, false, false), "friendship question accepted: " + question);
        Check(DuckCompanionDialogue.Intent("friend follow me") == "follow" && DuckCompanionDialogue.Intent("be quiet friend") == "quiet"
            && DuckCompanionDialogue.Intent("my friend's uncle runs the gas station") == "gas_station", "commands and named memories retain priority over friendship");
        var introductions = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        Check(Array.Find(introductions.groups, g => g.intent == "identity").lines.Length == 40
            && Array.Find(introductions.groups, g => g.intent == "friendship").lines.Length == 30, "forty introductions and thirty friendship replies");

        var dialogue = duck.Dialogue;
        head.position = duck.Head.position + Vector3.right;
        var bank = JsonUtility.FromJson<DuckCompanionDialogue.Bank>(Resources.Load<TextAsset>("DuckDialogue").text);
        var acknowledgments = Array.Find(bank.groups, g => g.intent == "acknowledge").lines;
        Check(acknowledgments.Length >= 30, "at least thirty listening acknowledgments");
        foreach (string request in new[] { "can you hear me?", "are you listening?", "mic check", "follow me", "wait", "be quiet", "go away" })
            Check(DuckCompanionDialogue.AcceptsSpeech(request, false, false), "direct request accepted without name: " + request);
        void Say(string request)
        {
            dialogue.CancelSpeech();
            CallPrivate(duck, "TickFarewell"); // Complete the previous response before the next request.
            ((System.Collections.Generic.Dictionary<int, double>)GetPrivate(dialogue, "rateLimit")).Clear();
            dialogue.SubmitLocal(request);
            Check(dialogue.Busy, "request schedules a spoken acknowledgment: " + request);
        }
        duck.Follow(human.photonView.OwnerActorNr);
        SetPrivate(dialogue, "commandRefusalChance", 1f);
        foreach (string command in new[] { "wait", "dismiss", "quiet" })
        {
            Say(command == "dismiss" ? "go away Duck" : command == "quiet" ? "be quiet Duck" : "wait Duck");
            Check(!duck.FollowingPaused && !dialogue.ChatterPaused, "refusal preserves active following and chatter: " + command);
            Check(Array.IndexOf(Array.Find(bank.groups, g => g.intent == "refuse_" + command).lines, dialogue.LastReply) >= 0,
                "refusal speaks the matching networked reply: " + command);
        }
        duck.WaitHere();
        Say("follow me");
        Check(duck.FollowingPaused && Array.IndexOf(Array.Find(bank.groups, g => g.intent == "refuse_follow").lines, dialogue.LastReply) >= 0,
            "refused follow really stays waiting");
        SetPrivate(dialogue, "commandRefusalChance", 0f);
        var foodLines = Array.Find(bank.groups, g => g.intent == "creepy_food").lines;
        Check(foodLines.Length >= 30, "thirty creepy food replies");
        foreach (string food in new[] { "Duck, pizza", "Duck, burgers", "Duck, rice", "Duck, fries", "Duck, I am hungry" })
        {
            Say(food);
            Check(Array.IndexOf(foodLines, dialogue.LastReply) >= 0, "food selects creepy reply without a personality roll: " + food);
        }
        SetPrivate(dialogue, "creepyDialogueChance", 1f);
        var laughs = Array.Find(bank.groups, g => g.intent == "laughter").lines;
        Check(laughs.Length >= 15, "fifteen chuckling and laughing replies");
        foreach (string laugh in new[] { "ha", "ha ha", "HA, HA!", "hahaha", "hehe", "heh", "lol" })
        {
            Check(DuckCompanionDialogue.AcceptsSpeech(laugh, false, false), "laughter accepted without Duck's name: " + laugh);
            Say(laugh);
            Check(Array.IndexOf(laughs, dialogue.LastReply) >= 0, "Duck laughs back despite full creepy chance: " + laugh);
        }

        foreach (string hello in new[] { "Hello", "Hi", "Hello Duck", "Hi Duck", "Howdy", "Howdy Duck" })
        {
            Check(DuckCompanionDialogue.AcceptsSpeech(hello, false, false), "bare greeting is accepted: " + hello);
            Say(hello);
            Check(Array.IndexOf(Array.Find(bank.groups, g => g.intent == "greeting").lines, dialogue.LastReply) >= 0,
                "greetings always get excited replies: " + hello);
        }
        Check(Array.Find(bank.groups, g => g.intent == "greeting").lines.Length >= 20, "at least twenty excited greetings");
        foreach (string farewell in new[] { "bye", "goodbye", "see you later", "bye duck", "goodbye duck", "go away duck", "shut up duck", "stop follow", "stop following me", "stop follow duck", "stop following me duck" })
        {
            Check(DuckCompanionDialogue.AcceptsSpeech(farewell, false, false), "farewell trigger is accepted: " + farewell);
            Say(farewell);
            string topic = "creepy_" + DuckCompanionDialogue.Intent(farewell);
            Check(Array.IndexOf(Array.Find(bank.groups, g => g.intent == topic).lines, dialogue.LastReply) >= 0,
                "farewell or rejection always gets creepy reply: " + farewell);
        }
        Check(dialogue.ChatterPaused && duck.Mode == DuckCompanion.CompanionMode.ReturningHome,
            "creepy dismissal obeys by returning home");
        SetPrivate(dialogue, "creepyDialogueChance", 0f);
        Say("be quiet Duck");
        Say("can you hear me?");
        Check(dialogue.ChatterPaused && Array.IndexOf(acknowledgments, dialogue.LastReply) >= 0,
            "microphone check acknowledges hearing while preserving quiet preference");
        Say("follow me");
        Say("follow me");
        Check(!dialogue.ChatterPaused && duck.TargetActor == human.photonView.OwnerActorNr,
            "repeated follow command still acknowledges the same buddy");
        Say("wait");
        Say("stay with me");
        ((System.Collections.Generic.Dictionary<string, System.Collections.Generic.Queue<int>>)GetPrivate(dialogue, "remaining")).Clear();
        var heard = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < acknowledgments.Length; i++) heard.Add((string)CallPrivate(dialogue, "Choose", "acknowledge"));
        Check(heard.Count == acknowledgments.Length, "listening acknowledgments cycle without repeats");
        dialogue.CancelSpeech();
    }

    static void CheckThreatWarnings(DuckCompanion duck, PhotonPlayer human, Transform head)
    {
        var dialogue = duck.Dialogue;
        duck.Follow(human.photonView.OwnerActorNr);
        head.position = duck.Head.position + Vector3.back;
        dialogue.CancelSpeech();
        SetPrivate(dialogue, "lastConversation", float.NegativeInfinity);
        SetPrivate(dialogue, "nextThreatWarning", 0d);
        SetPrivate(dialogue, "nextUrgentWarning", 0d);
        SetPrivate(dialogue, "warnedEncounter", false);
        SetPrivate(dialogue, "warnedUrgentEncounter", false);
        var enemy = new GameObject("Warning encounter regression").AddComponent<Game.AI.Enemy>();
        void Scan(float distance)
        {
            enemy.transform.position = duck.transform.position + Vector3.right * distance;
            SetPrivate(dialogue, "nextThreatCheck", 0d);
            CallPrivate(dialogue, "CheckNearbyEnemies");
        }
        Scan(24);
        Check(!dialogue.Busy, "distant zombie does not announce proximity");
        SetPrivate(dialogue, "nextReactive", 0f);
        Check(dialogue.React("tip"), "distant zombie allows friendly small talk");
        dialogue.CancelSpeech();
        dialogue.OnMasterClientSwitched(PhotonNetwork.MasterClient);
        Scan(14); Scan(14);
        Check(!dialogue.Busy, "stationary zombie inside fifteen metres does not trigger ordinary warning");
        Scan(13.8f);
        Check(dialogue.Busy, "approaching zombie inside fifteen metres triggers warning");
        double ordinaryDeadline = (double)GetPrivate(dialogue, "nextThreatWarning");
        Check(ordinaryDeadline >= PhotonNetwork.Time + 119 && ordinaryDeadline <= PhotonNetwork.Time + 181,
            "ordinary warnings have a two-to-three-minute cooldown");
        dialogue.CancelSpeech();
        SetPrivate(dialogue, "nextThreatWarning", 0d);
        Scan(13.6f);
        Check(!dialogue.Busy, "same encounter stays silent even after ordinary cooldown expires");
        Scan(4);
        Check(!dialogue.Busy, "urgent warning does not immediately echo ordinary warning");
        SetPrivate(dialogue, "nextUrgentWarning", 0d);
        Scan(4);
        Check(dialogue.Busy, "five-metre danger allows one urgent escalation");
        dialogue.CancelSpeech();
        SetPrivate(dialogue, "nextUrgentWarning", 0d);
        Scan(4);
        Check(!dialogue.Busy, "persistent close danger does not repeat urgent warnings");
        var writer = new PhotonStream(true, null);
        SetPrivate(writer, "writeData", new System.Collections.Generic.List<object>());
        dialogue.Serialize(writer);
        SetPrivate(dialogue, "warnedEncounter", false);
        SetPrivate(dialogue, "warnedUrgentEncounter", false);
        dialogue.Serialize(new PhotonStream(false, writer.ToArray()));
        dialogue.OnMasterClientSwitched(PhotonNetwork.MasterClient);
        Scan(4);
        Check(!dialogue.Busy, "encounter suppression survives serialization and master handoff");
        Scan(26);
        Check((bool)GetPrivate(dialogue, "warnedEncounter"), "brief clear interval does not reset encounter");
        SetPrivate(dialogue, "threatClearSince", PhotonNetwork.Time - 21);
        Scan(26);
        Check(!(bool)GetPrivate(dialogue, "warnedEncounter") && !(bool)GetPrivate(dialogue, "warnedUrgentEncounter"),
            "twenty clear seconds reset encounter without bypassing cooldown");
        Scan(14); Scan(13.8f);
        Check(!dialogue.Busy, "fresh encounter still respects existing global cooldown");
        SetPrivate(dialogue, "nextThreatWarning", 0d);
        Scan(13.6f);
        Check(dialogue.Busy, "fresh approaching encounter warns when cooldown permits");
        dialogue.CancelSpeech();
        enemy.enabled = false;
        UnityEngine.Object.Destroy(enemy.gameObject);
    }

    static void Check(bool condition, string description)
    {
        if (!condition) throw new InvalidOperationException("Duck Play check failed: " + description);
    }
}
