using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public sealed partial class DuckCompanion
{
    [SerializeField, Range(0f, 1f)] float killerAttackChance = 0.2f;
    [SerializeField, Min(0f)] float killerAttackCooldown = 180f;
    enum KillerPhase : byte { None, Warning, Approaching, Swing, Aftermath }
    KillerPhase killerPhase;
    int killerActor, killerWeaponKind, killerLineSequence, killerAttempt;
    bool killerHeardWarning, killerConfirmed, localKillerReported;
    bool killerVictimHeardWarning, localKillerHeardWarning, localWarningReported;
    readonly HashSet<int> pendingKillerReunions = new HashSet<int>();
    int[] killerReunionSnapshot = System.Array.Empty<int>();
    bool killerWeaponDropped;
    int soundedKillerAttempt = -1;
    double soundedKillerSwing = double.NegativeInfinity;
    int afterKillAttempt = -1, afterKillSpeech = -1;
    public static bool ShouldRespawnAfterKill(double secondsDead, double secondsSinceSpeechFinished)
        => secondsDead >= 25 || (secondsDead >= 10 && secondsSinceSpeechFinished >= 2);
    public bool TryGetAfterKillSpeech(int attempt, out int line)
    {
        line = afterKillSpeech;
        return afterKillAttempt == attempt && line >= 0;
    }
    double nextKillerCheck, killerDeadline, killerSwingAt;
    GameObject killerWeapon;
    BoxCollider killerHitbox;
    KillTrigger killerTrigger;
    Vector3 previousKillerCenter;
    bool previousKillerSample;
    readonly Collider[] killerContacts = new Collider[32];
    public bool KillerSequenceActive => killerPhase != KillerPhase.None;
    public bool KillerEyesAtDeath { get; private set; }
    void CaptureKillerDeathAppearance() => KillerEyesAtDeath = KillerSequenceActive;
    static readonly int KillerEyeEmission = Shader.PropertyToID("_EmissionColor");
    Renderer[] killerEyes;
    Color[] normalEyeEmission;
    MaterialPropertyBlock killerEyeProperties;
    bool killerEyesRed;

    void UpdateKillerEyes(bool active)
    {
        if (killerEyes == null)
        {
            var eyes = new System.Collections.Generic.List<Renderer>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                if ((renderer.name == "Eye_L" || renderer.name == "Eye_R")
                    && renderer.sharedMaterial != null && renderer.sharedMaterial.HasProperty(KillerEyeEmission))
                    eyes.Add(renderer);
            killerEyes = eyes.ToArray();
            normalEyeEmission = new Color[killerEyes.Length];
            killerEyeProperties = new MaterialPropertyBlock();
            for (int i = 0; i < killerEyes.Length; i++)
            {
                killerEyes[i].GetPropertyBlock(killerEyeProperties);
                normalEyeEmission[i] = killerEyeProperties.HasProperty(KillerEyeEmission)
                    ? killerEyeProperties.GetColor(KillerEyeEmission)
                    : killerEyes[i].sharedMaterial.GetColor(KillerEyeEmission);
            }
        }
        if (killerEyesRed == active) return;
        killerEyesRed = active;
        for (int i = 0; i < killerEyes.Length; i++)
        {
            if (killerEyes[i] == null) continue;
            killerEyes[i].GetPropertyBlock(killerEyeProperties);
            // RGB 255,0,0 in the color picker is normalized red, not HDR intensity 255.
            killerEyeProperties.SetColor(KillerEyeEmission, active ? Color.red : normalEyeEmission[i]);
            killerEyes[i].SetPropertyBlock(killerEyeProperties);
        }
    }

    void OnDisable()
    {
        CancelNavigation();
        if (killerEyes != null) UpdateKillerEyes(false);
    }
    bool KillerWeaponHeld => killerPhase == KillerPhase.Approaching || killerPhase == KillerPhase.Swing;
    public string KillerStatus
    {
        get
        {
            if (KillerSequenceActive) return $"Killer mode active: {killerPhase}";
            if (!HasAuthority) return "Killer mode inactive: waiting for host";
            if (nextKillerCheck > PhotonNetwork.Time)
                return $"Killer mode inactive: next roll in {nextKillerCheck - PhotonNetwork.Time:0}s ({killerAttackChance:P0})";
            return $"Killer mode inactive: {KillerStartBlocker() ?? "ready to roll"}";
        }
    }

    string KillerStartBlocker()
    {
        if (IsDead) return "Duck is dead";
        if (Mode != CompanionMode.Following) return "Waiting for a follow request";
        // Only gates a new roll. Active attacks retain their traversal and chase
        // deadline; these navigation fields already survive master takeover.
        if (PhotonNetwork.Time < fenceReleaseUntil) return "finishing fence snag recovery";
        if (fenceDetourCommitted) return "finishing fence detour";
        if (fenceAttemptUntil > 0) return "finishing fence crossing attempt";
        if (followingPaused || Dialogue.ChatterPaused) return "Waiting for follow / speech to resume";
        if (State != Activity.Following) return "Waiting for following movement";
        if (!groundSupported) return "Waiting for ground support";
        if (Dialogue.Busy) return "Waiting for current speech to finish";
        if (pendingKillerReunions.Contains(TargetActor)) return "Waiting to greet the returning victim";
        var buddy = PhotonPlayer.ForActor(TargetActor);
        if (buddy == null || buddy.IsDead || !buddy.TryGetHeadPosition(out Vector3 head)) return "Waiting for a living companion";
        if (Vector3.Distance(Head.position, head) > 3.5f) return "waiting to be within 3.5m of the followed player";
        if (!KillerClearSight(head)) return "Waiting for clear line of sight";
        if (KillerPrefab(0) == null || KillerPrefab(1) == null) return "Weapon prefabs missing from Photon pool";
        return null;
    }

    bool TickKillerAttack()
    {
        if (killerPhase == KillerPhase.None)
        {
            if (nextKillerCheck == 0) nextKillerCheck = PhotonNetwork.Time + Random.Range(90f, 150f);
            if (PhotonNetwork.Time < nextKillerCheck) return false;
            // A due opportunity waits through speech, traversal and other temporary
            // blockers. Previously each blocked check discarded another full check interval.
            if (KillerStartBlocker() != null) return false;
            nextKillerCheck = PhotonNetwork.Time + Random.Range(90f, 150f);
            if (killerAttackChance < 1f && Random.value >= killerAttackChance) return false;
            EndWander();
            killerActor = TargetActor;
            killerAttempt++;
            killerWeaponKind = Random.Range(0, 2);
            killerPhase = KillerPhase.Warning;
            killerHeardWarning = killerConfirmed = localKillerReported = false;
            killerWeaponDropped = false;
            killerVictimHeardWarning = localKillerHeardWarning = localWarningReported = false;
            nextKillerCheck = PhotonNetwork.Time + killerAttackCooldown;
            killerDeadline = PhotonNetwork.Time + 60;
            killerLineSequence = Dialogue.SpeakKillerLine("killer_before");
            LastEvent = "a possessive warning";
            DuckzAnalytics.DuckKillAttempt();
        }

        var victim = PhotonPlayer.ForActor(killerActor);
        if (killerPhase == KillerPhase.Aftermath)
        {
            Move(transform.position, false);
            FinishMovement(null);
            if (!Dialogue.Busy || PhotonNetwork.Time > killerDeadline)
            {
                CancelKillerAttack();
                ReturnHome();
            }
            return true;
        }
        if (killerConfirmed)
        {
            DropKillerWeapon();
            ClearKillerWeapon();
            killerPhase = KillerPhase.Aftermath;
            killerDeadline = PhotonNetwork.Time + 60;
            killerLineSequence = Dialogue.SpeakKillerLine("killer_after");
            photonView.RPC(nameof(ReceiveAfterKillSpeech), RpcTarget.All, killerAttempt, killerLineSequence);
            LastEvent = "won't let a friend leave Quackville";
            return true;
        }
        if (victim == null || victim.IsDead || !victim.isActiveAndEnabled || IsDead || State == Activity.Retreating
            || TargetActor != killerActor || followingPaused || Dialogue.ChatterPaused || PhotonNetwork.Time > killerDeadline
            || !victim.TryGetHeadPosition(out Vector3 victimHead))
        {
            // Allow a short confirmation transit window after the victim's dead pose arrives.
            if (killerPhase == KillerPhase.Swing && victim != null && victim.IsDead
                && PhotonNetwork.Time <= killerDeadline && PhotonNetwork.Time < killerSwingAt + 2) return true;
            CancelKillerAttack();
            return false;
        }
        if (killerPhase == KillerPhase.Warning)
        {
            Move(transform.position, false);
            FinishMovement(victim);
            if (Dialogue.SpeechSequence != killerLineSequence) { CancelKillerAttack(); return true; }
            killerHeardWarning |= Dialogue.IsSpeaking;
            if (!Dialogue.Busy)
            {
                // Missing TTS, cancelled audio or subtitle-only warnings never authorize a kill.
                if (!killerHeardWarning) { CancelKillerAttack(); return true; }
                if (!killerVictimHeardWarning) return true;
                killerPhase = KillerPhase.Approaching;
                killerDeadline = PhotonNetwork.Time + 10;
            }
            return true;
        }
        if (Dialogue.SpeechSequence != killerLineSequence)
        {
            CancelKillerAttack();
            return false;
        }
        // Traversal belongs to the same chase, even while a roof or bus panel
        // temporarily hides the target. Keep the weapon and the original deadline.
        if (State == Activity.Climbing || State == Activity.Jumping)
        {
            killerPhase = KillerPhase.Approaching;
            Traverse();
            FinishMovement(victim);
            return true;
        }
        if (!groundSupported && killerPhase == KillerPhase.Swing)
            killerPhase = KillerPhase.Approaching;
        // Recover from a missed swing and pursue again, without restarting the
        // ten-second budget or putting the weapon away between swings.
        if (killerPhase == KillerPhase.Swing && PhotonNetwork.Time >= killerSwingAt + 1.2)
        {
            killerPhase = KillerPhase.Approaching;
            previousKillerSample = false;
        }
        if (killerPhase == KillerPhase.Approaching)
        {
            Vector3 feet = FindGround(victimHead);
            float distance = Vector3.ProjectOnPlane(feet - transform.position, Vector3.up).magnitude;
            bool differentLevel = Mathf.Abs(feet.y - transform.position.y) > motor.stepOffset + 0.05f;
            Move(feet, distance > 0.65f || differentLevel || !groundSupported);
            if (distance <= 0.9f && !differentLevel && State != Activity.Climbing && State != Activity.Jumping
                && TryGroundSupport(transform.position, out _) && KillerClearSight(victimHead))
            {
                killerPhase = KillerPhase.Swing;
                killerSwingAt = PhotonNetwork.Time + 0.35;
            }
        }
        else Move(transform.position, false);
        FinishMovement(victim);
        return true;
    }

    bool KillerClearSight(Vector3 point) => !Physics.Linecast(Head.position, point, environment, QueryTriggerInteraction.Ignore);

    void CancelKillerAttack()
    {
        if (KillerSequenceActive) nextKillerCheck = System.Math.Max(nextKillerCheck, PhotonNetwork.Time + killerAttackCooldown);
        if (killerPhase == KillerPhase.Approaching || killerPhase == KillerPhase.Swing) DropKillerWeapon();
        killerPhase = KillerPhase.None;
        killerConfirmed = killerHeardWarning = false;
        ClearKillerWeapon();
    }

    static GameObject KillerPrefab(int kind)
    {
        if (PhotonNetwork.PrefabPool is DefaultPool pool
            && pool.ResourceCache.TryGetValue(kind == 0 ? "WeaponNailBat" : "WeaponFryPan", out var prefab)) return prefab;
        return null;
    }

    void PoseKillerArm()
    {
        if (killerPhase != KillerPhase.Approaching && killerPhase != KillerPhase.Swing) return;
        if (State == Activity.Climbing || State == Activity.Jumping || !groundSupported)
        {
            // The free left hand keeps its normal climbing/gait pose. Carry the
            // weapon beside the right shoulder instead of planting that hand.
            RightHand.position = Head.position + transform.right * 0.3f + transform.forward * 0.1f;
            RightHand.rotation = transform.rotation * Quaternion.Euler(-15f, 0f, -20f);
            expressiveFingers.rightThumb = expressiveFingers.rightIndex = expressiveFingers.rightPinky = 0.95f;
            return;
        }
        float t = killerPhase == KillerPhase.Swing ? Mathf.Clamp01((float)(PhotonNetwork.Time - killerSwingAt) / 0.8f) : 0;
        float strike = Mathf.SmoothStep(0, 1, Mathf.Clamp01((t - 0.15f) / 0.5f));
        RightHand.position = Head.position + transform.right * Mathf.Lerp(0.25f, 0.05f, strike)
            + transform.forward * Mathf.Lerp(0.05f, 0.55f, strike) + Vector3.up * Mathf.Lerp(0.25f, -0.1f, strike);
        RightHand.rotation = transform.rotation * Quaternion.Euler(Mathf.Lerp(-40f, 85f, strike), 0, 0);
        expressiveFingers.rightThumb = expressiveFingers.rightIndex = expressiveFingers.rightPinky = 0.95f;
    }

    void LateUpdate()
    {
        // killerPhase already replicates to every peer, including late joiners.
        UpdateKillerEyes(KillerSequenceActive && !IsDead);
        // The intended victim must actually hear the warning too, not just the host.
        if (killerPhase == KillerPhase.Warning && PhotonPlayer.Local != null
            && PhotonPlayer.Local.photonView.OwnerActorNr == killerActor
            && Dialogue.SpeechSequence == killerLineSequence)
        {
            localKillerHeardWarning |= Dialogue.IsSpeaking;
            if (localKillerHeardWarning && !Dialogue.Busy && !localWarningReported)
            {
                localWarningReported = true;
                photonView.RPC(nameof(ConfirmKillerWarning), RpcTarget.MasterClient, killerAttempt);
            }
        }
        bool armed = !IsDead && (killerPhase == KillerPhase.Approaching || killerPhase == KillerPhase.Swing);
        if (!armed) { ClearKillerWeapon(); return; }
        if (killerWeapon == null) CreateKillerWeapon();
        if (killerWeapon == null) return;
        float elapsed = (float)(PhotonNetwork.Time - killerSwingAt);
        bool striking = killerPhase == KillerPhase.Swing && PhotonNetwork.Time <= killerDeadline
            && elapsed >= 0.15f && elapsed <= 0.75f;
        killerHitbox.enabled = striking;
        if (!striking) { previousKillerSample = false; return; }
        if (HasAuthority && (soundedKillerAttempt != killerAttempt || soundedKillerSwing != killerSwingAt))
        {
            soundedKillerAttempt = killerAttempt;
            soundedKillerSwing = killerSwingAt;
            PlayKillerWeaponSound(ItemSound.Swing, RightHand.position);
        }
        Vector3 center = killerWeapon.transform.TransformPoint(killerHitbox.center);
        Vector3 start = previousKillerSample ? previousKillerCenter : center;
        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(start, center) / 0.08f), 1, 24);
        for (int step = 1; step <= steps && !localKillerReported; step++)
        {
            int count = Physics.OverlapBoxNonAlloc(Vector3.Lerp(start, center, (float)step / steps), killerHitbox.size * 0.5f,
                killerContacts, killerWeapon.transform.rotation, ~0, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count && !localKillerReported; i++) killerTrigger.TryContact(killerContacts[i]);
        }
        previousKillerCenter = center;
        previousKillerSample = true;
    }

    void CreateKillerWeapon()
    {
        var prefab = KillerPrefab(killerWeaponKind);
        if (prefab == null) return;
        killerWeapon = new GameObject(prefab.name + "_DuckHeld");
        killerWeapon.transform.SetParent(RightHand, false);
        // Reuse original model hierarchy/materials without running pickup, ownership or item-hit scripts.
        CopyKillerMeshes(prefab.transform, killerWeapon.transform, true);
        var original = prefab.GetComponent<BoxCollider>();
        killerHitbox = killerWeapon.AddComponent<BoxCollider>();
        killerHitbox.center = original.center;
        killerHitbox.size = original.size;
        killerHitbox.isTrigger = true;
        killerHitbox.enabled = false;
        var body = killerWeapon.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        killerTrigger = killerWeapon.AddComponent<KillTrigger>();
        killerTrigger.CompanionKiller = this;
        killerTrigger.CompanionAttack = killerAttempt;
        killerTrigger.ContactAllowed = other =>
        {
            var local = PhotonPlayer.Local;
            return !localKillerReported && !IsDead && killerPhase == KillerPhase.Swing
                && PhotonNetwork.Time <= killerDeadline
                && PhotonNetwork.Time >= killerSwingAt + 0.15 && PhotonNetwork.Time <= killerSwingAt + 0.75
                && local != null && local.photonView.OwnerActorNr == killerActor && !local.IsDead
                && other.attachedRigidbody != null && other.attachedRigidbody.CompareTag("Player")
                && KillerClearSight(other.ClosestPoint(Head.position));
        };
        killerTrigger.PlayerKilled += () =>
        {
            localKillerReported = true;
            photonView.RPC(nameof(ConfirmKillerContact), RpcTarget.MasterClient, killerAttempt);
        };
        PlayKillerWeaponSound(ItemSound.Grab, RightHand.position);
    }

    void PlayKillerWeaponSound(ItemSound sound, Vector3 position)
    {
        // Only the controller broadcasts. Use the original weapon's authored
        // events and the same room audio path as player-held items.
        if (!HasAuthority) return;
        var prefab = KillerPrefab(killerWeaponKind);
        var sounds = prefab != null ? prefab.GetComponent<ItemSounds>() : null;
        if (sounds != null) RoomRpc.PlaySound(sounds.Get(sound), position);
    }

    static void CopyKillerMeshes(Transform source, Transform destination, bool root)
    {
        if (!root)
        {
            destination.localPosition = source.localPosition;
            destination.localRotation = source.localRotation;
            destination.localScale = source.localScale;
        }
        var mesh = source.GetComponent<MeshFilter>();
        var renderer = source.GetComponent<MeshRenderer>();
        if (mesh != null && renderer != null)
        {
            destination.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh.sharedMesh;
            destination.gameObject.AddComponent<MeshRenderer>().sharedMaterials = renderer.sharedMaterials;
        }
        foreach (Transform child in source)
        {
            var copy = new GameObject(child.name).transform;
            copy.SetParent(destination, false);
            CopyKillerMeshes(child, copy, false);
        }
    }

    void ClearKillerWeapon()
    {
        if (killerWeapon != null) { killerWeapon.SetActive(false); Destroy(killerWeapon); }
        killerWeapon = null;
        previousKillerSample = false;
    }

    void DropKillerWeapon()
    {
        if (!HasAuthority || killerWeaponDropped) return;
        var prefab = KillerPrefab(killerWeaponKind);
        if (prefab == null) return;
        // Room-owned so the drop survives the attacking host leaving. Its normal
        // PhotonGrabbable handles pickup ownership, physics and the existing lifetime.
        var item = PhotonNetwork.InstantiateRoomObject(prefab.name, RightHand.position, RightHand.rotation);
        killerWeaponDropped = true;
        item.GetComponent<PhotonGrabbable>()?.NotifyEquipmentDropped();
        var body = item.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.isKinematic = false;
            body.useGravity = true;
            body.linearVelocity = transform.right * 0.5f;
            body.angularVelocity = transform.forward * 1.5f;
        }
    }

    [PunRPC]
    void ReceiveAfterKillSpeech(int attempt, int line, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Controller) return;
        afterKillAttempt = attempt;
        afterKillSpeech = line;
    }

    [PunRPC]
    void ConfirmKillerWarning(int attempt, PhotonMessageInfo info)
    {
        if (HasAuthority && !IsDead && killerPhase == KillerPhase.Warning && attempt == killerAttempt
            && info.Sender != null && info.Sender.ActorNumber == killerActor)
            killerVictimHeardWarning = true;
    }

    public override void OnMasterClientSwitched(Photon.Realtime.Player newMasterClient)
    {
        // Force the existing navigation/motor recovery even if this headset slept
        // through both loss and reacquisition, with no intervening Update.
        wasAuthority = !newMasterClient.IsLocal;
        CancelKillerAttack();
    }

    [PunRPC]
    void ConfirmKillerContact(int attempt, PhotonMessageInfo info)
    {
        if (!HasAuthority || IsDead || killerConfirmed || (killerPhase != KillerPhase.Swing && killerPhase != KillerPhase.Approaching) || attempt != killerAttempt
            || info.Sender == null || info.Sender.ActorNumber != killerActor
            || PhotonNetwork.Time < killerSwingAt || PhotonNetwork.Time > killerSwingAt + 3) return;
        killerConfirmed = true;
        pendingKillerReunions.Add(killerActor);
        PlayKillerWeaponSound(ItemSound.FleshImpact,
            killerHitbox != null ? killerHitbox.transform.TransformPoint(killerHitbox.center) : RightHand.position);
    }

    int[] GetKillerReunionSnapshot()
    {
        // Photon may retain the previous array for delta comparisons. Reuse an
        // unchanged snapshot, but never mutate an array already handed to it.
        bool unchanged = killerReunionSnapshot.Length == pendingKillerReunions.Count;
        for (int i = 0; unchanged && i < killerReunionSnapshot.Length; i++)
            unchanged = pendingKillerReunions.Contains(killerReunionSnapshot[i]);
        if (unchanged) return killerReunionSnapshot;
        killerReunionSnapshot = pendingKillerReunions.Count == 0
            ? System.Array.Empty<int>() : new int[pendingKillerReunions.Count];
        pendingKillerReunions.CopyTo(killerReunionSnapshot);
        return killerReunionSnapshot;
    }

    void SerializeKiller(PhotonStream stream)
    {
        if (stream.IsWriting)
        {
            serializationValues.Send(stream, (byte)killerPhase); serializationValues.Send(stream, killerActor); serializationValues.Send(stream, killerWeaponKind);
            serializationValues.Send(stream, killerSwingAt); serializationValues.Send(stream, killerAttempt); serializationValues.Send(stream, nextKillerCheck);
            serializationValues.Send(stream, GetKillerReunionSnapshot());
            serializationValues.Send(stream, killerLineSequence);
            serializationValues.Send(stream, killerDeadline);
            serializationValues.Send(stream, killerWeaponDropped);
            serializationValues.Send(stream, KillerEyesAtDeath);
        }
        else
        {
            killerPhase = (KillerPhase)(byte)stream.ReceiveNext(); killerActor = (int)stream.ReceiveNext();
            int weapon = (int)stream.ReceiveNext();
            if (weapon != killerWeaponKind) ClearKillerWeapon();
            killerWeaponKind = weapon; killerSwingAt = (double)stream.ReceiveNext();
            int attempt = (int)stream.ReceiveNext();
            if (attempt != killerAttempt)
            {
                localKillerReported = localKillerHeardWarning = localWarningReported = false;
                previousKillerSample = false;
            }
            killerAttempt = attempt; nextKillerCheck = (double)stream.ReceiveNext();
            pendingKillerReunions.Clear();
            foreach (int actor in (int[])stream.ReceiveNext()) pendingKillerReunions.Add(actor);
            killerLineSequence = (int)stream.ReceiveNext();
            killerDeadline = (double)stream.ReceiveNext();
            killerWeaponDropped = (bool)stream.ReceiveNext();
            KillerEyesAtDeath = (bool)stream.ReceiveNext();
            UpdateKillerEyes(KillerSequenceActive && !IsDead);
        }
    }
}
