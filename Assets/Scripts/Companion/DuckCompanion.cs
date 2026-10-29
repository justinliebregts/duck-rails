using System.Collections.Generic;
using Game.Animation;
using Game.Audio;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;
using UnityEngine.AI;

/// <summary>Room-owned companion. Only the master simulates; all peers solve the shared player IK.</summary>
[DefaultExecutionOrder(-50)]
[RequireComponent(typeof(PhotonPlayer))]
[RequireComponent(typeof(FootstepEmitter))]
public sealed partial class DuckCompanion : MonoBehaviourPunCallbacks
{
    public enum Activity { Following = 0, Wandering = 1, Waiting = 2, Jumping = 3, Climbing = 4, Dead = 5, Retreating = 6 }
    public enum CompanionMode : byte { HomeWandering, Following, ReturningHome, GreetingVisitor }
    public CompanionMode Mode { get; private set; }
    public Vector3 HomePosition => homePosition;
    const float HomeRadius = 10f;
    Vector3 homePosition, homeDestination;
    Quaternion homeRotation;
    bool homeInitialized, homeWalking;
    double nextHomeWander;
    float homeFloorY;
    bool homeFloorResolved;
    Vector3 HomeFloor => new Vector3(homePosition.x, homeFloorY, homePosition.z);
    Vector3[] homeRoute = System.Array.Empty<Vector3>();
    int homeCorner;
    Vector3[][] subwayStairs;
    Vector3[] returnStairs = System.Array.Empty<Vector3>();
    int returnStairCorner, returnEntrance = -1;
    float returnStairProgressAt, nextReturnStairSearch;
    Vector3 returnStairProgressPosition;
    readonly float[] blockedEntranceUntil = new float[2];
    Vector3[] activeStairFlight;
    int activeStairIndex, activeStairDirection;
    float nextStairJoin, stairBlockedTime;
    public string HomeWanderStatus { get; private set; } = "waiting for first wander";
    int visitorActor;
    bool visitorArrived, visitorGreetingPending;
    double visitorDeadline, visitorLastProgress, visitorLastSpeech;
    Vector3 visitorProgressPosition;
    int[] greetedVisitors = System.Array.Empty<int>();
    double[] visitorRetryTimes = System.Array.Empty<double>();
    public bool CanHearVisitor => Mode == CompanionMode.HomeWandering || Mode == CompanionMode.GreetingVisitor;
    public bool WaitingForFarewell { get; private set; }
    double farewellDeadline;
    enum SpeechAction : byte { None, Resume, Follow, ReturnHome, Wait, Quiet }
    SpeechAction speechAction;
    int speechActor;
    int respawnRevision;
    int conversationLookActor, pendingConversationLookActor;
    double conversationLookUntil;
    PhotonPlayer NearbyConversationSpeaker(int actor)
    {
        PhotonPlayer speaker = actor > 0 ? PhotonPlayer.ForActor(actor) : null;
        return speaker != null && speaker.isActiveAndEnabled && !speaker.IsDead
            && speaker.TryGetHeadPosition(out Vector3 head) && Head != null
            && Vector3.Distance(head, Head.position) <= DuckCompanionDialogue.ConversationRange ? speaker : null;
    }
    public PhotonPlayer ConversationSpeaker => !IsDead && !KillerSequenceActive && !CommandResponsePending
        && State != Activity.Climbing && State != Activity.Jumping && State != Activity.Retreating
        && PhotonNetwork.Time < conversationLookUntil ? NearbyConversationSpeaker(conversationLookActor) : null;

    public void AttendConversation(int actor)
    {
        if (!HasAuthority || IsDead || NearbyConversationSpeaker(actor) == null) return;
        if (Dialogue.Busy && PhotonNetwork.Time < conversationLookUntil
            && NearbyConversationSpeaker(conversationLookActor) != null && actor != conversationLookActor)
            pendingConversationLookActor = actor;
        else
        {
            conversationLookActor = actor;
            pendingConversationLookActor = 0;
            conversationLookUntil = PhotonNetwork.Time + 3;
        }
    }

    void TickConversationAttention()
    {
        if (IsDead)
        {
            conversationLookActor = pendingConversationLookActor = 0;
            conversationLookUntil = 0;
            return;
        }
        if (NearbyConversationSpeaker(conversationLookActor) == null) conversationLookActor = 0;
        if (!Dialogue.Busy && pendingConversationLookActor != 0)
        {
            int pending = pendingConversationLookActor;
            pendingConversationLookActor = 0;
            AttendConversation(pending);
        }
        if (Dialogue.Busy && conversationLookActor != 0)
            conversationLookUntil = PhotonNetwork.Time + 3;
        else if (PhotonNetwork.Time >= conversationLookUntil)
            conversationLookActor = 0;
    }
    double waitUntil;
    public bool CommandResponsePending => speechAction != SpeechAction.None;
    public PhotonPlayer CommandResponseSpeaker
    {
        get
        {
            if (!CommandResponsePending || IsDead || speechActor <= 0) return null;
            PhotonPlayer speaker = PhotonPlayer.ForActor(speechActor);
            return speaker != null && speaker.isActiveAndEnabled && !speaker.IsDead ? speaker : null;
        }
    }
    public const double CommandDuration = 15;
    public const double QuietDuration = 10;
    public Activity State { get; private set; }
    public bool IsDead => State == Activity.Dead;
    public bool HasAuthority => PhotonNetwork.InRoom && photonView.IsMine;
    public int TargetActor { get; private set; }
    public bool FollowingPaused => followingPaused;
    public bool ReturningAfterDeath { get; private set; }
    public string LastEvent { get; private set; } = "arrived";
    public Transform Head { get; private set; }
    public Transform LeftHand { get; private set; }
    public Transform RightHand { get; private set; }
    public DuckCompanionDialogue Dialogue { get; private set; }
    public DuckCompanionSelfMute SelfMute { get; private set; }
    public static event System.Action<DuckCompanion> UnmutedItself;

    internal void RaiseUnmutedItself() => UnmutedItself?.Invoke(this);
    public Vector3 Velocity { get; private set; }
    public float HitReactionMinSpeed => hitReactionMinSpeed;
    const double RecoveryDelay = 2.5;
    const double StuckRespawnDelay = 20;

    [SerializeField] float speed = 4.2f;
    [SerializeField] float followDistance = 3f;
    [Tooltip("Eye height above the support surface. Matches the short duck rig, not a standing human.")]
    [SerializeField] float eyeHeight = 0.5f;
    [SerializeField] float jumpSpeed = 15.5f;
    [Header("Companion Voice")]
    [Tooltip("Speech only. Applied locally on every peer when Duck spawns. Does not change player voices or weapon/footstep sounds.")]
    public DuckCompanionVoiceSettings voiceSettings = new DuckCompanionVoiceSettings();

    [Header("Movement Audio")]
    [SerializeField] SoundEvent jumpSound;
    [SerializeField] float gravity = 20f;
    [SerializeField, Min(0f)] float retreatSpeed = 2.2f;
    [Tooltip("A light moving item can startle Duck without needing a full player-launch swing.")]
    [SerializeField, Min(0.25f)] float hitReactionMinSpeed = 0.6f;
    [SerializeField] LayerMask environment;
    [Tooltip("Occasional short scouting trips, returns when more than 40 metres of the followed player.")]
    [SerializeField] bool allowWandering = true;
    const float WanderRadius = 40f;
    bool exploring, returningFromExplore;
    double nextWanderAt, wanderUntil;
    Vector3 wanderDestination;

    PhotonPlayer avatar;
    CharacterController motor;
    CapsuleCollider hurtbox;
    NavMeshPath path;
    Vector3[] corners = System.Array.Empty<Vector3>();
    int corner;
    Vector3[] obstacleRoute = System.Array.Empty<Vector3>();
    int obstacleCorner;
    Vector3 obstacleTarget;
    float obstacleRouteUntil, nextObstacleSearch;
    bool fenceTakeoffPending;
    Vector3 fenceLanding;
    Transform fenceLandingSurface;
    Collider lastFenceContact;
    float lastFenceContactAt, fenceStallSince = -1f;
    double fenceReleaseUntil, failedFenceUntil;
    Vector3 fenceReleaseDirection, fenceReleaseStart, failedFenceCrossing;
    double fenceAttemptUntil;
    Vector3 fenceAttemptPoint, fenceAttemptNormal, fenceDetourTarget;
    bool fenceDetourCommitted;
    double failedClimbUntil;
    Vector3 failedClimbOrigin, failedClimbDirection;
    float nextFenceDetourSearch;
    Vector3[] fenceDetourRoute = System.Array.Empty<Vector3>();
    int fenceDetourCorner;
    readonly RaycastHit[] fenceClearanceHits = new RaycastHit[32];
    readonly Collider[] fenceClearanceOverlaps = new Collider[32];
    float ledgeAttemptSince = -1f, avoidedLedgeUntil;
    Vector3 ledgeAttemptOrigin, avoidedLedge;
    bool AvoidingLedge => Time.time < avoidedLedgeUntil;
    bool NearAvoidedLedge => AvoidingLedge
        && Vector3.ProjectOnPlane(transform.position - avoidedLedge, Vector3.up).sqrMagnitude < 4f
        && Mathf.Abs(transform.position.y - avoidedLedge.y) < 0.5f;
    readonly Queue<Vector3> progressHistory = new Queue<Vector3>();
    Vector3 progressGoal;
    float nextPath, nextTraversal, nextTip, vertical, stuck;
    Vector3[] levelRoute = System.Array.Empty<Vector3>();
    int levelCorner;
    Vector3 levelTarget;
    float nextLevelSearch, levelRouteUntil, nextInteriorSearch;
    bool followingInteriorExit;
    Vector2 headLookAngles;
    FingerCurl expressiveFingers;
    Vector3 leftWristAngles, rightWristAngles;
    int gestureSequence = -1, speakingGesture = -1;
    float gestureWeight;
    GorillaGait proceduralGait;
    FootstepEmitter footstepEmitter;
    double respawnAt;
    double retreatUntil, nextHitReaction;
    int retreatActor;
    Vector3 retreatLookPosition;
    Vector3 lastPosition, lastTargetPosition;
    bool greeted, wasAuthority;
    bool groundSupported, reactedAirborne;
    Vector3 dropDirection;
    Vector3 dropLanding;
    Vector3 dropExit;
    double dropUntil;
    Vector3 localDetour;
    double localDetourUntil;
    float airbornePeak;
    float traversalTime, traversalDuration, climbLiftDuration, traversalJumpSpeed;
    Vector3 traversalStart, traversalEnd, ledge;
    Transform ledgeSurface;
    Vector3 localLedge, localLanding;
    readonly Queue<Vector3> trail = new Queue<Vector3>();
    readonly int[] groundRoute = new int[4096];
    readonly Collider[] recoveryOverlaps = new Collider[32];
    readonly RaycastHit[] traversalHits = new RaycastHit[32];
    Vector3? traversalProbeOrigin;
    float? traversalProbeMinimumHeight;
    Vector3 recoveryAnchor;
    double lastProgressAt, nextRecoveryAt;
    int recoveryAttempts;
    bool movementRequested, recoveryWasMoving;
    float followNoProgressSeconds;
    int followProgressActor;
    Vector3 followProgressAnchor;
    readonly List<Vector3> followProgressPositions = new List<Vector3>(64);
    double idleInvalidSince = -1;
    bool descendedWithoutLanding;
    double stalledFallSince = -1, nextFallRelease, fallReleaseUntil;

    void Awake()
    {
        avatar = GetComponent<PhotonPlayer>();
        footstepEmitter = GetComponent<FootstepEmitter>();
        environment = environment.value == 0 ? LayerMask.GetMask("Environment") : environment;
        Head = Anchor("DuckHeadTarget");
        LeftHand = Anchor("DuckLeftHandTarget");
        RightHand = Anchor("DuckRightHandTarget");
        avatar.BindSources(Head, LeftHand, RightHand);
        motor = gameObject.AddComponent<CharacterController>();
        motor.height = eyeHeight + 0.15f;
        motor.center = Vector3.up * (motor.height * 0.5f);
        motor.radius = 0.18f;
        motor.skinWidth = 0.02f;
        motor.minMoveDistance = 0f;
        motor.stepOffset = 0.25f;
        motor.slopeLimit = Game.AI.CrawlGraph.MaxGroundSlopeDegrees;
        // Items still hit the separate hurtbox/swept-hit pipeline, but must not
        // wedge under this motor. Support and IK only use the environment.
        motor.excludeLayers = LayerMask.GetMask("Grabbable");
        motor.enabled = false;
        // An untagged rigidbody lets enemy triggers see Duck without matching human-only triggers.
        var collisionBody = gameObject.AddComponent<Rigidbody>();
        collisionBody.isKinematic = true;
        collisionBody.useGravity = false;
        hurtbox = gameObject.AddComponent<CapsuleCollider>();
        hurtbox.height = motor.height;
        hurtbox.radius = motor.radius + 0.01f;
        hurtbox.center = motor.center;
        hurtbox.isTrigger = true;
        path = new NavMeshPath();
        Dialogue = gameObject.AddComponent<DuckCompanionDialogue>();
        SelfMute = gameObject.AddComponent<DuckCompanionSelfMute>();
        nextTip = Time.time + 45f;
        lastPosition = transform.position;
        airbornePeak = transform.position.y;
        ResetRecovery();
        ScheduleWander();
    }

    Transform Anchor(string label)
    {
        var result = new GameObject(label).transform;
        result.SetParent(transform, false);
        return result;
    }

    void Start()
    {
        PrepareNavigation();
        var body = GetComponentInChildren<RemotePlayerBody>();
        if (body == null || body.LeftArm == null || body.RightArm == null) return;
        // Place the shoulders before the gait measures the first reachable hand targets.
        Pose(null);
        proceduralGait = gameObject.AddComponent<GorillaGait>();
        proceduralGait.ConfigurePoseTargets(transform, Head, LeftHand, RightHand,
            body.LeftArm, body.RightArm, environment, eyeHeight);
        proceduralGait.FootPlanted += OnFootPlanted;
    }

    void OnFootPlanted(Vector3 point, Vector3 normal)
    {
        if (!HasAuthority || IsDead || !groundSupported
            || (State != Activity.Following && State != Activity.Retreating && State != Activity.Wandering)
            || Vector3.ProjectOnPlane(Velocity, Vector3.up).sqrMagnitude < 0.01f) return;
        // Remote avatars receive poses, not gait ticks. Send each live contact once;
        // every peer uses the same player emitter, surface mapping and audio mixer.
        photonView.RPC(nameof(ReceiveFootstep), RpcTarget.All, point, normal);
    }

    [PunRPC]
    void ReceiveFootstep(Vector3 point, Vector3 normal, PhotonMessageInfo info)
    {
        if (info.Sender != photonView.Controller || IsDead || !isActiveAndEnabled) return;
        footstepEmitter.PlayAt(point, normal);
    }

    void OnDestroy()
    {
        CancelNavigation();
        ClearKillerWeapon();
        if (proceduralGait != null) proceduralGait.FootPlanted -= OnFootPlanted;
    }

    readonly Game.AI.TargetGroundSnapshot targetGroundSnapshot = new Game.AI.TargetGroundSnapshot();

    void Update()
    {
        targetGroundSnapshot.Clear();
        using var updateSample = DuckUpdateMarker.Auto();
        bool authority = HasAuthority;
        if (authority != wasAuthority)
        {
            wasAuthority = authority;
            motor.enabled = authority && !IsDead;
            ResetNavigationAfterAuthorityChange();
            proceduralGait?.ResetPose();
            CancelKillerAttack();
            // Interrupted traversal is recovered with gravity by the new master.
            if (authority && (State == Activity.Jumping || State == Activity.Climbing))
                State = followingPaused ? Activity.Waiting : Activity.Following;
        }
        if (!authority) { CancelNavigation(); return; }
        TickNavigation();
        TickConversationAttention();
        EnsureHome();
        if (IsDead)
        {
            CancelKillerAttack();
            if (PhotonNetwork.Time >= respawnAt) Respawn();
            return;
        }
        if (State == Activity.Retreating)
        {
            if (PhotonNetwork.Time < retreatUntil)
            {
                TickRetreat();
                FinishMovement(PhotonPlayer.ForActor(retreatActor));
                return;
            }
            State = followingPaused ? Activity.Waiting : Activity.Following;
            nextPath = 0;
            stuck = 0;
            LastEvent = "back with my buddy";
        }
        if (TickFarewell()) return;
        if (TickTimedWait()) return;
        if (TickKillerAttack()) return;
        PhotonPlayer target = PhotonPlayer.ForActor(TargetActor);
        if (Mode == CompanionMode.Following && (target == null || !target.isActiveAndEnabled || target.IsDead))
            ReturnHome();
        if (Mode == CompanionMode.GreetingVisitor)
        {
            TickVisitor();
            return;
        }
        if (Mode != CompanionMode.Following)
        {
            TickHome();
            return;
        }
        if (!target.TryGetHeadPosition(out Vector3 targetHead))
        {
            Move(transform.position, false);
            FinishMovement(null);
            return;
        }
        Vector3 targetFeet = FindGround(targetHead, true);
        TryFirstGreeting(targetFeet);
        if ((targetFeet - lastTargetPosition).sqrMagnitude > 2.25f)
        {
            trail.Enqueue(targetFeet);
            while (trail.Count > 24) trail.Dequeue();
            lastTargetPosition = targetFeet;
        }

        // Exploration persists through jump/climb activity changes. The leash
        // cancels the trip, while committed traversal finishes safely before return.
        if (exploring && (!allowWandering || followingPaused || Dialogue.ThreatNearby()
            || PhotonNetwork.Time >= wanderUntil || Vector3.Distance(transform.position, targetFeet) > WanderRadius
            || Vector3.Distance(wanderDestination, targetFeet) > WanderRadius
            || Vector3.Distance(transform.position, wanderDestination) < 0.7f || stuck > 5f))
            EndWander(true);
        if (State == Activity.Following && !exploring && !returningFromExplore) TryStartWander(targetFeet);
        if (TickFarewell()) return;
        if (exploring)
        {
            if (State == Activity.Climbing || State == Activity.Jumping) Traverse();
            else
            {
                State = Activity.Wandering;
                Move(wanderDestination, true);
            }
            FinishMovement(target);
            return;
        }

        if (State == Activity.Climbing || State == Activity.Jumping)
            Traverse();
        else
        {
            float distance = Vector3.Distance(transform.position, targetFeet);
            State = followingPaused ? Activity.Waiting : Activity.Following;
            bool walk = State != Activity.Waiting
                && (fenceDetourCommitted || distance > followDistance || Mathf.Abs(targetFeet.y - transform.position.y) > motor.stepOffset + 0.05f);
            Move(targetFeet, walk);
            bool killerReunion = pendingKillerReunions.Contains(TargetActor);
            if (killerReunion && distance < 5f && groundSupported
                && Mathf.Abs(targetFeet.y - transform.position.y) <= motor.stepOffset + 0.1f)
            {
                // Retry after busy speech/cooldowns. Only consume this victim's
                // reunion once the authority actually broadcasts the line.
                if (Dialogue.React("killer_reunion"))
                {
                    pendingKillerReunions.Remove(TargetActor);
                    greeted = true;
                    ReturningAfterDeath = false;
                    returningFromExplore = false;
                }
            }
            else if (!killerReunion && distance < 5f && ReturningAfterDeath
                && Dialogue.React("reunion"))
            {
                greeted = true;
                ReturningAfterDeath = false;
            }
            if (!killerReunion && returningFromExplore && distance < 5f && groundSupported
                && Mathf.Abs(targetFeet.y - transform.position.y) <= motor.stepOffset + 0.1f
                && Dialogue.React("wander_return")) returningFromExplore = false;
            if (!killerReunion && Time.time > nextTip && distance < 6f && !Dialogue.Busy)
            {
                // A temporary conversation pause must not consume the next chatter slot.
                nextTip = Time.time + (Dialogue.React("tip") ? Random.Range(55f, 95f) : 1f);
            }
        }
        FinishMovement(target);
    }

    public bool ReadyToGreet(PhotonPlayer speaker)
    {
        if (speaker == null || !speaker.TryGetHeadPosition(out Vector3 head) || !groundSupported) return false;
        Vector3 feet = FindGround(head);
        return Vector3.Distance(transform.position, feet) <= 3f
            && Mathf.Abs(feet.y - transform.position.y) < 0.75f;
    }

    public void MarkGreetingReplied(int actor)
    {
        if (!HasAuthority) return;
        if (TargetActor == actor) greeted = true;
        if (Mode == CompanionMode.GreetingVisitor && visitorActor == actor)
            visitorGreetingPending = false;
    }

    bool TryFirstGreeting(Vector3 targetFeet)
    {
        if (greeted || ReturningAfterDeath || pendingKillerReunions.Contains(TargetActor)
            || Vector3.Distance(transform.position, targetFeet) >= 5f) return false;
        // Meeting our buddy takes priority over incidental chatter, including
        // an airborne line still playing/preparing. The shared speech RPC cancels
        // that line on every peer. Run before traversal so landing isn't required.
        if (!Dialogue.React("greeting", true)) return false;
        greeted = true;
        return true;
    }

    void ScheduleWander() => nextWanderAt = PhotonNetwork.Time + Random.Range(90f, 150f);

    void EnsureHome()
    {
        if (homeInitialized) return;
        homePosition = transform.position;
        homeRotation = transform.rotation;
        if (GameManager.instance != null
            && GameManager.instance.TryGetCompanionSpawnPose(out Vector3 position, out Quaternion rotation))
        {
            homePosition = position;
            homeRotation = rotation;
        }
        homeInitialized = true;
        ResolveHomeFloor();
        nextHomeWander = PhotonNetwork.Time + Random.Range(2f, 4f);
    }

    void ResolveHomeFloor()
    {
        if (homeFloorResolved) return;
        homeFloorY = homePosition.y;
        if (Physics.Raycast(homePosition + Vector3.up * 0.2f, Vector3.down, out RaycastHit floor,
            4f, environment, QueryTriggerInteraction.Ignore) && Game.AI.CrawlGraph.IsWalkableGround(floor.normal))
        {
            homeFloorY = floor.point.y + 0.04f;
            homeFloorResolved = true;
        }
    }

    public void ReturnHome()
    {
        if (!HasAuthority || IsDead) return;
        ClearFollowProgress();
        fenceAttemptUntil = 0;
        fenceDetourCommitted = false;
        WaitingForFarewell = false;
        speechAction = SpeechAction.None;
        waitUntil = 0;
        EnsureHome();
        CancelKillerAttack();
        EndWander();
        ResetNavigationAfterAuthorityChange();
        Mode = CompanionMode.ReturningHome;
        visitorActor = 0;
        visitorGreetingPending = false;
        TargetActor = 0;
        followingPaused = false;
        ReturningAfterDeath = false;
        homeWalking = false;
        retreatUntil = 0;
        State = Activity.Following;
        LastEvent = "returning to my subway home";
    }

    public void ReturnHomeAfterFarewell()
    {
        if (!HasAuthority || IsDead) return;
        QueueCommandResponse("dismiss", 0, false);
    }

    public void QueueCommandResponse(string intent, int actor, bool refused)
    {
        if (HasAuthority && !IsDead && !refused && intent == "follow"
            && (!CommandResponsePending || WaitingForFarewell))
        {
            Follow(actor);
            Dialogue.CancelPendingGreeting(actor);
            // The acknowledgement already greets this buddy. Do not replace it
            // with the automatic first-meeting line as we begin moving.
            MarkGreetingReplied(actor);
            return;
        }
        if (!HasAuthority || IsDead || CommandResponsePending) return;
        speechAction = refused ? SpeechAction.Resume : intent == "follow" ? SpeechAction.Follow
            : intent == "dismiss" ? SpeechAction.ReturnHome : intent == "wait" ? SpeechAction.Wait
            : intent == "quiet" ? SpeechAction.Quiet : SpeechAction.Resume;
        speechActor = actor;
        WaitingForFarewell = speechAction == SpeechAction.ReturnHome;
        // Allow synthesis/scheduled playback, but never strand Duck if audio fails.
        farewellDeadline = PhotonNetwork.Time + 45;
        if (!refused && intent == "wait") { WaitHere(); waitUntil = 0; }
        if (!refused && intent == "dismiss")
            DuckzAnalytics.DuckDismiss();
        LastEvent = refused ? "finishing my refusal before resuming" : "finishing my response before acting";
    }

    bool TickFarewell()
    {
        if (!CommandResponsePending) return false;
        returnStairProgressAt = Time.time; // Speaking is not a blocked entrance.
        // Finish committed traversal instead of suspending Duck in midair.
        if (State == Activity.Climbing || State == Activity.Jumping)
        {
            Traverse();
            FinishMovement(PhotonPlayer.ForActor(TargetActor));
            return true;
        }
        if (!Dialogue.Busy || PhotonNetwork.Time >= farewellDeadline)
        {
            SpeechAction action = speechAction;
            int actor = speechActor;
            speechAction = SpeechAction.None;
            WaitingForFarewell = false;
            if (action == SpeechAction.ReturnHome) ReturnHome();
            else if (action == SpeechAction.Follow)
            {
                PhotonPlayer requested = PhotonPlayer.ForActor(actor);
                if (requested == null || !requested.isActiveAndEnabled || requested.IsDead) ReturnHome();
                else Follow(actor);
            }
            else if (action == SpeechAction.Wait) waitUntil = PhotonNetwork.Time + CommandDuration;
            else if (action == SpeechAction.Quiet) Dialogue.BeginQuietPeriod();
            if (State == Activity.Waiting && !followingPaused) State = Activity.Following;
            return false;
        }
        State = Activity.Waiting;
        Move(transform.position, false); // Keep support/gravity and invalid-position recovery alive.
        // Rotation and bounded head/eye tracking can acknowledge the speaker
        // while the response gate continues to hold horizontal travel.
        FinishMovement(CommandResponseSpeaker);
        return true;
    }

    bool TickTimedWait()
    {
        if (!followingPaused) return false;
        returnStairProgressAt = Time.time;
        // A death/interruption can cancel the acknowledgment before its timer starts.
        if (waitUntil <= 0) waitUntil = PhotonNetwork.Time + CommandDuration;
        if (waitUntil > 0 && PhotonNetwork.Time >= waitUntil)
        {
            followingPaused = false;
            waitUntil = 0;
            State = Activity.Following;
            LastEvent = "short wait finished";
            return false;
        }
        if (State == Activity.Climbing || State == Activity.Jumping) Traverse();
        else { State = Activity.Waiting; Move(transform.position, false); }
        FinishMovement(PhotonPlayer.ForActor(TargetActor));
        return true;
    }

    void TickHome()
    {
        ResolveHomeFloor();
        // Activity describes the physical motion; Mode survives climbs, falls,
        // retreats and master handoff and describes where that motion should go.
        if (State == Activity.Climbing || State == Activity.Jumping)
            Traverse();
        else if (Mode == CompanionMode.ReturningHome
            || (transform.position - homePosition).sqrMagnitude > HomeRadius * HomeRadius
            || Mathf.Abs(transform.position.y - homeFloorY) > 1f)
        {
            Mode = CompanionMode.ReturningHome;
            bool arrived = Vector3.Distance(transform.position, HomeFloor) < 0.7f
                && groundSupported;
            State = Activity.Following;
            Vector3 homeAim = HomeFloor;
            if (!arrived) TryHomeStairDestination(out homeAim);
            Move(homeAim, !arrived);
            if (arrived)
            {
                Mode = CompanionMode.HomeWandering;
                homeWalking = false;
                ReturningAfterDeath = false;
                nextHomeWander = PhotonNetwork.Time + Random.Range(4f, 10f);
                LastEvent = "home in the subway";
            }
        }
        else
        {
            if (homeWalking && Vector3.Distance(transform.position, homeDestination) < 0.4f)
            {
                homeWalking = false;
                nextHomeWander = PhotonNetwork.Time + Random.Range(4f, 10f);
            }
            if (!homeWalking && !followingPaused && PhotonNetwork.Time >= nextHomeWander)
            {
                nextHomeWander = PhotonNetwork.Time + 5;
                Vector3 alternate = default;
                bool hasAlternate = false;
                for (int i = 0; i < 12; i++)
                {
                    Vector3 offset = Quaternion.Euler(0, i * 30f, 0)
                        * Vector3.forward * Random.Range(2f, 4f);
                    Vector3 candidate = transform.position + offset;
                    candidate.y = homeFloorY;
                    if (!hasAlternate && TryHomeFloor(candidate, out alternate)) hasAlternate = true;
                    if (!TryHomeDestination(candidate, out homeDestination)) continue;
                    homeWalking = true;
                    homeRoute = new[] { homeDestination };
                    homeCorner = 0;
                    HomeWanderStatus = "walking a short supported route";
                    break;
                }
                if (!homeWalking && hasAlternate && TryHomeDetour(alternate, out homeRoute))
                {
                    homeDestination = alternate;
                    homeWalking = true;
                    homeCorner = 0;
                    HomeWanderStatus = "walking a bounded alternate route";
                }
            }
            // Routes are local physics results; rebuild after an authority change.
            if (homeWalking && homeRoute.Length == 0)
            {
                if (TryHomeDestination(homeDestination, out _)) homeRoute = new[] { homeDestination };
                else if (!TryHomeDetour(homeDestination, out homeRoute)) homeWalking = false;
                homeCorner = 0;
            }
            while (homeCorner < homeRoute.Length - 1
                && Vector3.Distance(transform.position, homeRoute[homeCorner]) < 0.4f) homeCorner++;
            State = homeWalking && !followingPaused ? Activity.Wandering : Activity.Waiting;
            Move(homeWalking && homeCorner < homeRoute.Length ? homeRoute[homeCorner] : transform.position,
                homeWalking && !followingPaused);
        }
        FinishMovement(null);
    }

    // Quackville's two named entrance meshes have four switchback flights.
    // These landing offsets were measured against their actual colliders.
    // Sample each tread locally so the canopy/overlapping flights cannot steal
    // the floor, and reject the connector if the level geometry changes.
    Vector3[] BuildSubwayStairs(string entranceName)
    {
        GameObject entrance = GameObject.Find(entranceName);
        Collider collider = entrance != null ? entrance.GetComponent<Collider>() : null;
        if (collider == null) return System.Array.Empty<Vector3>();
        Bounds bounds = collider.bounds;
        float top = bounds.max.y - 3.6f, bottom = bounds.min.y + 0.25f;
        float flight = (top - bottom) / 4f;
        float x = bounds.center.x, z = bounds.center.z;
        var landings = new[] {
            new Vector3(x - 4.6f, top, z + 0.5f),
            new Vector3(x - 2.4f, top, z + 1.55f),
            new Vector3(x + 2.4f, top - flight, z + 1.55f),
            new Vector3(x + 2.4f, top - flight, z - 1.55f),
            new Vector3(x - 2.4f, top - flight * 2, z - 1.55f),
            new Vector3(x - 2.4f, top - flight * 2, z + 1.55f),
            new Vector3(x + 2.4f, top - flight * 3, z + 1.55f),
            new Vector3(x + 2.4f, top - flight * 3, z - 1.55f),
            new Vector3(x - 2.4f, bottom, z - 1.55f),
            new Vector3(x - 2.4f, bottom, z + (entranceName.Contains("West") ? 4.5f : -4.5f))
        };
        var route = new List<Vector3>();
        for (int segment = 0; segment < landings.Length - 1; segment++)
        {
            int steps = Mathf.CeilToInt(Vector3.Distance(landings[segment], landings[segment + 1]) / 0.2f);
            for (int step = segment == 0 ? 0 : 1; step <= steps; step++)
            {
                Vector3 sample = Vector3.Lerp(landings[segment], landings[segment + 1], (float)step / steps);
                if (!Physics.Raycast(sample + Vector3.up * 0.8f, Vector3.down, out RaycastHit floor,
                    1.6f, environment, QueryTriggerInteraction.Ignore) || floor.normal.y < 0.7f)
                    { LastEvent = "stair floor missing at " + sample; return System.Array.Empty<Vector3>(); }
                Vector3 point = floor.point + Vector3.up * 0.04f;
                // A capsule straddles adjacent treads as it descends. Validate
                // at the higher tread's height; retain the actual floor target.
                Vector3 clearance = point + Vector3.up * 0.18f;
                if (!ClearBody(clearance)) { LastEvent = "stair body blocked at " + point; return System.Array.Empty<Vector3>(); }
                if (route.Count > 0)
                {
                    Vector3 previous = route[route.Count - 1] + Vector3.up * 0.18f;
                    // Descend a tread horizontally first, then settle onto it.
                    Vector3 across = new Vector3(point.x, Mathf.Max(previous.y, clearance.y), point.z);
                    if (Mathf.Abs(previous.y - clearance.y) > motor.stepOffset
                        || !ClearBodySweep(previous, across) || !ClearBodySweep(across, clearance))
                        { LastEvent = "stair segment blocked from " + previous + " to " + point; return System.Array.Empty<Vector3>(); }
                }
                route.Add(point);
            }
        }
        return route.ToArray();
    }

    bool TryHomeStairDestination(out Vector3 destination)
    {
        destination = HomeFloor;
        if (returnStairCorner >= returnStairs.Length)
        {
            returnStairs = System.Array.Empty<Vector3>();
            if (transform.position.y <= homeFloorY + 1f || Time.time < nextReturnStairSearch) return false;
            nextReturnStairSearch = Time.time + 3f;
            if (subwayStairs == null)
                subwayStairs = new[] { BuildSubwayStairs("Subway_West_Entrance"), BuildSubwayStairs("Subway_East_Entrance") };
            float best = float.PositiveInfinity;
            for (int entrance = 0; entrance < subwayStairs.Length; entrance++)
            {
                Vector3[] route = subwayStairs[entrance];
                if (route.Length == 0 || Time.time < blockedEntranceUntil[entrance]
                    || Mathf.Abs(route[route.Length - 1].y - homeFloorY) > 1.5f) continue;
                // On handoff/recovery, rejoin the nearby tread at the same
                // height rather than walking back to the street entrance.
                int join = 0;
                for (int i = 1; i < route.Length; i++)
                    if (Mathf.Abs(route[i].y - transform.position.y) < 0.3f
                        && (route[i] - transform.position).sqrMagnitude < 0.64f
                        && ClearBodySweep(transform.position, route[i])) join = i;
                float cost = Vector3.Distance(transform.position, route[join]);
                if (cost >= best) continue;
                best = cost;
                returnStairs = route;
                returnStairCorner = join;
                returnEntrance = entrance;
            }
            if (returnStairs.Length == 0) return false;
            returnStairProgressAt = Time.time;
            returnStairProgressPosition = transform.position;
        }
        // A small tolerance prevents skipping across adjacent switchback flights.
        while (returnStairCorner < returnStairs.Length
            && Vector3.Distance(transform.position, returnStairs[returnStairCorner]) < 0.35f)
        {
            returnStairCorner++;
            returnStairProgressAt = Time.time;
        }
        if ((transform.position - returnStairProgressPosition).sqrMagnitude > 0.25f)
        {
            returnStairProgressPosition = transform.position;
            returnStairProgressAt = Time.time;
        }
        if (returnStairCorner == returnStairs.Length) return false;
        if (Time.time - returnStairProgressAt > 8f)
        {
            blockedEntranceUntil[returnEntrance] = Time.time + 30f;
            returnStairs = System.Array.Empty<Vector3>();
            returnStairCorner = 0;
            nextReturnStairSearch = 0;
            return false; // Existing obstacle routes and recovery remain available.
        }
        destination = returnStairs[returnStairCorner];
        LastEvent = returnEntrance == 0 ? "returning through west subway stairs" : "returning through east subway stairs";
        return true;
    }

    // Shared, bidirectional stair walking for following/chasing as well as home.
    // Join only a nearby tread on this floor, never a flight directly overhead.
    bool WalkSubwayStairs(Vector3 destination, float deltaTime)
    {
        if (activeStairFlight == null)
        {
            if (Time.time < nextStairJoin || Mathf.Abs(destination.y - transform.position.y) < 0.4f) return false;
            nextStairJoin = Time.time + 0.5f;
            if (subwayStairs == null)
                subwayStairs = new[] { BuildSubwayStairs("Subway_West_Entrance"), BuildSubwayStairs("Subway_East_Entrance") };
            float nearest = 1.44f;
            foreach (Vector3[] route in subwayStairs)
            for (int i = 0; i < route.Length; i++)
            {
                Vector3 point = route[i];
                float distance = (point - transform.position).sqrMagnitude;
                if (distance >= nearest || Mathf.Abs(point.y - transform.position.y) > 0.3f
                    || !ClearBodySweep(transform.position + Vector3.up * 0.18f, point + Vector3.up * 0.18f)) continue;
                int direction = destination.y > transform.position.y ? -1 : 1;
                if (i + direction < 0 || i + direction >= route.Length) continue;
                nearest = distance;
                activeStairFlight = route;
                activeStairIndex = i;
                activeStairDirection = direction;
            }
            if (activeStairFlight == null) return false;
            stairBlockedTime = 0;
            // Discard shortcuts left over from the general traversal planner.
            corners = levelRoute = obstacleRoute = System.Array.Empty<Vector3>();
            localDetourUntil = dropUntil = 0;
        }
        // If the player changes floors, reverse on the same staircase.
        float difference = destination.y - transform.position.y;
        if (Mathf.Abs(difference) > 0.5f)
            activeStairDirection = difference > 0 ? -1 : 1;
        while (activeStairIndex >= 0 && activeStairIndex < activeStairFlight.Length
            && Vector3.Distance(transform.position, activeStairFlight[activeStairIndex]) < 0.35f)
            activeStairIndex += activeStairDirection;
        if (activeStairIndex < 0 || activeStairIndex >= activeStairFlight.Length)
        {
            activeStairFlight = null;
            nextPath = 0;
            nextStairJoin = Time.time + 1f;
            return false;
        }
        Vector3 waypoint = activeStairFlight[activeStairIndex];
        if (Mathf.Abs(difference) < 0.3f && ClearBodySweep(transform.position, destination))
        {
            activeStairFlight = null;
            return false; // Same-floor destination is directly reachable.
        }
        // Falling into the stairwell must rejoin locally, not steer across the void.
        if (Mathf.Abs(waypoint.y - transform.position.y) > 0.8f)
        {
            activeStairFlight = null;
            nextStairJoin = 0;
            return false;
        }
        // CharacterController steps up the 17–18 cm risers. No jump, mantle,
        // drop shortcut or IK hand hold is needed for these supported flights.
        bool progressed = MoveStairMotor(waypoint, deltaTime);
        stairBlockedTime = progressed ? 0 : stairBlockedTime + deltaTime;
        if (stairBlockedTime > 3f)
        {
            activeStairFlight = null;
            nextStairJoin = Time.time + 5f;
            return false; // Let ordinary alternate routes and recovery run.
        }
        LastEvent = activeStairDirection < 0 ? "walking up the subway stairs" : "walking down the subway stairs";
        return true;
    }

    public bool HearVisitor(int actor, bool needsGreeting)
    {
        if (!HasAuthority || IsDead || !CanHearVisitor || followingPaused || Dialogue.ChatterPaused) return false;
        PhotonPlayer visitor = PhotonPlayer.ForActor(actor);
        if (visitor == null || visitor.IsDead || !visitor.isActiveAndEnabled
            || !visitor.TryGetHeadPosition(out Vector3 head)
            || Vector3.Distance(head, Head.position) > DuckCompanionDialogue.ConversationRange) return false;
        if (Mode == CompanionMode.GreetingVisitor)
        {
            if (visitorActor != actor) return false;
            visitorLastSpeech = PhotonNetwork.Time;
            if (!needsGreeting) visitorGreetingPending = false;
            return true; // Speech never restarts the approach deadline.
        }
        for (int i = 0; i < greetedVisitors.Length; i++)
            if (greetedVisitors[i] == actor && PhotonNetwork.Time < visitorRetryTimes[i]) return false;
        EnsureHome();
        EndWander();
        ResetNavigationAfterAuthorityChange();
        Mode = CompanionMode.GreetingVisitor;
        homeWalking = false;
        visitorActor = actor;
        visitorArrived = false;
        visitorGreetingPending = needsGreeting;
        visitorDeadline = PhotonNetwork.Time + 10;
        visitorLastProgress = visitorLastSpeech = PhotonNetwork.Time;
        visitorProgressPosition = transform.position;
        State = Activity.Following;
        DuckzAnalytics.DuckActivated();
        // Immutable snapshots may be retained by Photon's delta comparisons.
        int index = System.Array.IndexOf(greetedVisitors, actor);
        var actors = new List<int>();
        var times = new List<double>();
        for (int i = 0; i < greetedVisitors.Length; i++)
            if (i != index && visitorRetryTimes[i] > PhotonNetwork.Time)
            { actors.Add(greetedVisitors[i]); times.Add(visitorRetryTimes[i]); }
        actors.Add(actor); times.Add(PhotonNetwork.Time + 60);
        greetedVisitors = actors.ToArray();
        visitorRetryTimes = times.ToArray();
        LastEvent = "approaching a nearby voice";
        return true;
    }

    bool VisitorExpired(float distance)
    {
        if (distance > DuckCompanionDialogue.ConversationRange
            || (transform.position - homePosition).sqrMagnitude > HomeRadius * HomeRadius) return true;
        if (visitorArrived) return distance > 4.5f || PhotonNetwork.Time - visitorLastSpeech >= 15;
        return PhotonNetwork.Time >= visitorDeadline || PhotonNetwork.Time - visitorLastProgress >= 3;
    }

    void TickVisitor()
    {
        PhotonPlayer visitor = PhotonPlayer.ForActor(visitorActor);
        if (visitor == null || visitor.IsDead || !visitor.isActiveAndEnabled
            || !visitor.TryGetHeadPosition(out Vector3 head))
        { EndVisitor(); return; }
        Vector3 feet = FindGround(head);
        float distance = Vector3.Distance(transform.position, feet);
        if (!visitorArrived && (transform.position - visitorProgressPosition).sqrMagnitude >= 0.25f)
        {
            visitorLastProgress = PhotonNetwork.Time;
            visitorProgressPosition = transform.position;
        }
        if (VisitorExpired(Vector3.Distance(Head.position, head)))
        { EndVisitor(); return; }
        if (distance <= 3 && Mathf.Abs(feet.y - transform.position.y) < 0.75f && groundSupported)
            visitorArrived = true;
        if (State == Activity.Climbing || State == Activity.Jumping) Traverse();
        else
        {
            Vector3 destination = homePosition + Vector3.ClampMagnitude(feet - homePosition, HomeRadius - 0.5f);
            State = visitorArrived ? Activity.Waiting : Activity.Following;
            Move(destination, !visitorArrived);
        }
        // Never interrupt an answer or ongoing sentence with the greeting.
        if (visitorArrived && visitorGreetingPending && Dialogue.React("greeting"))
        {
            visitorGreetingPending = false;
            greeted = true;
        }
        FinishMovement(visitor);
    }

    void EndVisitor()
    {
        Dialogue.CancelPendingGreeting(visitorActor);
        if (Dialogue.Busy)
        {
            ReturnHomeAfterFarewell();
            TickFarewell();
        }
        else { ReturnHome(); TickHome(); }
    }

    bool TryHomeDestination(Vector3 candidate, out Vector3 destination)
    {
        ResolveHomeFloor();
        destination = default;
        // Probe from subway floor height, never from above the street. Require
        // a supported, clear corridor entirely inside the home area.
        Vector3 previous = transform.position;
        int samples = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(previous, candidate) / 0.4f));
        for (int i = 1; i <= samples; i++)
        {
            Vector3 point = Vector3.Lerp(transform.position, candidate, (float)i / samples);
            if (!TryHomeFloor(point, out point)) return false;
            if (!ClearBodySweep(previous, point)) { HomeWanderStatus = "blocked direct corridor"; return false; }
            previous = point;
        }
        destination = previous;
        return Vector3.Distance(transform.position, destination) > 1f;
    }

    bool TryHomeFloor(Vector3 sample, out Vector3 point)
    {
        point = default;
        if (!Physics.Raycast(sample + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor,
            1f, environment, QueryTriggerInteraction.Ignore)
            || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal))
        { HomeWanderStatus = "no walkable floor near sample"; return false; }
        point = floor.point + Vector3.up * 0.04f;
        if (Mathf.Abs(point.y - homeFloorY) > 0.5f || (point - HomeFloor).sqrMagnitude > HomeRadius * HomeRadius)
        { HomeWanderStatus = "sample outside home floor or radius"; return false; }
        if (!ClearBody(point)) { HomeWanderStatus = "body clearance blocked"; return false; }
        return true;
    }

    bool TryHomeDetour(Vector3 goal, out Vector3[] route)
    {
        bool found = RequestNavigation(NavigationPlan.Home, goal);
        route = found ? completedNavigationRoute : System.Array.Empty<Vector3>();
        return found;
    }

    void EndWander(bool announceReturn = false)
    {
        fenceTakeoffPending = false;
        returningFromExplore = announceReturn && (exploring || returningFromExplore);
        exploring = false;
        levelRoute = System.Array.Empty<Vector3>();
        levelCorner = 0;
        nextLevelSearch = nextInteriorSearch = 0;
        followingInteriorExit = false;
        dropUntil = localDetourUntil = 0;
        if (State == Activity.Wandering) State = followingPaused ? Activity.Waiting : Activity.Following;
        wanderUntil = 0;
        corners = System.Array.Empty<Vector3>();
        nextPath = 0;
        stuck = 0;
        ScheduleWander();
    }

    bool TryStartWander(Vector3 buddyFeet)
    {
        if (!allowWandering || followingPaused || PhotonNetwork.Time < nextWanderAt || !greeted
            || Mathf.Abs(transform.position.y - buddyFeet.y) > motor.stepOffset + 0.05f
            || ReturningAfterDeath || !groundSupported || Dialogue.Busy || Dialogue.ChatterPaused
            || Dialogue.ThreatNearby() || Vector3.Distance(transform.position, buddyFeet) > followDistance + 1f) return false;
        nextWanderAt = PhotonNetwork.Time + 10;
        for (int attempt = 0; attempt < 12; attempt++)
        {
            Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(20f, 40f);
            Vector3 candidate = buddyFeet + new Vector3(offset.x, 0, offset.y);
            if (!TryWanderDestination(candidate, buddyFeet, out Vector3 landing)) continue;
            // Announce only a trip that can actually start; never interrupt a conversation.
            if (!Dialogue.React("wander")) return false;
            wanderDestination = landing;
            wanderUntil = PhotonNetwork.Time + 45;
            exploring = true;
            returningFromExplore = false;
            State = Activity.Wandering;
            QueueCommandResponse("resume", TargetActor, false);
            corners = System.Array.Empty<Vector3>();
            nextPath = 0;
            stuck = 0;
            LastEvent = "checking familiar places near my buddy";
            ResetRecovery();
            return true;
        }
        return false;
    }

    bool TryWanderDestination(Vector3 candidate, Vector3 buddyFeet, out Vector3 landing)
    {
        landing = default;
        // Choose a real standing surface, including roofs or lower ground. The
        // ordinary route/traversal solver checks each jump, climb and drop en route.
        if (!Physics.Raycast(candidate + Vector3.up * 20f, Vector3.down, out RaycastHit floor,
            60f, environment, QueryTriggerInteraction.Ignore)
            || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) return false;
        landing = floor.point + Vector3.up * 0.04f;
        float distance = Vector3.Distance(landing, buddyFeet);
        return distance >= 20f && distance <= WanderRadius && ClearBody(landing);
    }

    void FinishMovement(PhotonPlayer target)
    {
        Velocity = (transform.position - lastPosition) / Mathf.Max(Time.deltaTime, 0.001f);
        lastPosition = transform.position;
        groundSupported = motor.enabled && TryGroundSupport(transform.position, out _);
        TickFenceAttempt();
        TickFallSnag();
        if (TickFollowProgress(target, Time.deltaTime)) target = null;
        TickRecovery(target);
        if (State == Activity.Climbing || (groundSupported && State != Activity.Jumping))
        {
            reactedAirborne = false;
            airbornePeak = transform.position.y;
        }
        else
        {
            airbornePeak = Mathf.Max(airbornePeak, transform.position.y);
            // Ignore stair seams and brief losses of support. React once to an
            // unplanned drop, even when no human target is currently available.
            if (!reactedAirborne && Velocity.y < -1f && airbornePeak - transform.position.y > 0.6f)
            {
                reactedAirborne = true;
                Dialogue.ReactAirborne();
            }
        }
        Pose(target);
        if (transform.position.y < -100f) TryKill();
    }

    void ResetNavigationAfterAuthorityChange()
    {
        targetGroundSnapshot.Clear();
        CancelNavigation();
        truckExitRoute = System.Array.Empty<Vector3>();
        truckExitCorner = 0;
        nextTruckExitSearch = truckExitStalled = 0;
        interiorStairRoute = System.Array.Empty<Vector3>();
        stairSearchRunning = false;
        interiorStairCorner = 0;
        nextInteriorStairSearch = interiorStairBlocked = 0;
        sidewaysClimbing = sidewaysProbe = false;
        fenceDetourRoute = System.Array.Empty<Vector3>();
        fenceDetourCorner = 0;
        nextFenceDetourSearch = 0;
        lastFenceContact = null;
        fenceStallSince = -1f;
        fenceTakeoffPending = false;
        fenceLandingSurface = null;
        // Paths and traversal probes belong to the previous simulator. Rebuild
        // from the replicated pose; retain the replicated buddy/quiet state.
        corners = levelRoute = obstacleRoute = System.Array.Empty<Vector3>();
        homeRoute = System.Array.Empty<Vector3>();
        activeStairFlight = null;
        nextStairJoin = stairBlockedTime = 0;
        returnStairs = System.Array.Empty<Vector3>();
        returnStairCorner = 0;
        nextReturnStairSearch = 0;
        homeCorner = 0;
        corner = levelCorner = obstacleCorner = 0;
        followingInteriorExit = false;
        nextPath = nextTraversal = nextLevelSearch = nextInteriorSearch = nextObstacleSearch = nextDropProbe = nextEdgeRelease = 0;
        localDetourUntil = dropUntil = 0;
        traversalTime = traversalDuration = 0;
        traversalProbeOrigin = null;
        traversalProbeMinimumHeight = null;
        ledgeSurface = null;
        vertical = stuck = 0;
        trail.Clear();
        progressHistory.Clear();
        movementRequested = recoveryWasMoving = false;
        idleInvalidSince = -1;
        descendedWithoutLanding = false;
        stalledFallSince = -1;
        nextFallRelease = fallReleaseUntil = 0;
        ledgeAttemptSince = -1f;
        avoidedLedgeUntil = 0;
        lastPosition = transform.position;
        Velocity = Vector3.zero;
        ResetRecovery();
    }

    void ResetRecovery()
    {
        recoveryAnchor = transform.position;
        lastProgressAt = PhotonNetwork.Time;
        nextRecoveryAt = lastProgressAt + RecoveryDelay;
        recoveryAttempts = 0;
    }

    bool TickFollowProgress(PhotonPlayer target, float dt)
    {
        if (Mode != CompanionMode.Following || IsDead)
        {
            ClearFollowProgress();
            return false;
        }
        if (followProgressActor != TargetActor)
        {
            ClearFollowProgress();
            followProgressActor = TargetActor;
        }
        // Intentional holds and separate behaviours do not spend the follow
        // budget. Jump/climb movement does: a stalled traversal is still stuck.
        bool trying = !followingPaused && !CommandResponsePending && !KillerSequenceActive
            && !exploring && State != Activity.Retreating
            && (movementRequested || State == Activity.Jumping || State == Activity.Climbing);
        bool routed = (fenceDetourCommitted && fenceDetourCorner < fenceDetourRoute.Length)
            || truckExitCorner < truckExitRoute.Length
            || interiorStairCorner < interiorStairRoute.Length
            || corner < corners.Length || levelCorner < levelRoute.Length
            || (obstacleCorner < obstacleRoute.Length && !fenceTakeoffPending)
            || returnStairCorner < returnStairs.Length
            || State == Activity.Jumping || State == Activity.Climbing;
        if (!routed && target != null && target.TryGetHeadPosition(out Vector3 head))
            routed = ClearBodySweep(transform.position, FindGround(head, true));
        if (!UpdateFollowProgress(trying, routed, dt)) return false;
        ReturnHome();
        movementRequested = false;
        LastEvent = "no follow progress for 10 seconds; walking home";
        return true;
    }

    void ClearFollowProgress()
    {
        followNoProgressSeconds = 0;
        followProgressActor = 0;
        followProgressAnchor = transform.position;
        followProgressPositions.Clear();
        followProgressPositions.Add(transform.position);
    }

    bool UpdateFollowProgress(bool trying, bool routed, float dt)
    {
        if (!trying)
        {
            // Do not credit retreat/recovery motion as successful following.
            followProgressAnchor = transform.position;
            return false;
        }
        followNoProgressSeconds += Mathf.Clamp(dt, 0f, 0.25f);
        if (routed && (transform.position - followProgressAnchor).sqrMagnitude >= 0.25f)
        {
            bool newGround = true;
            foreach (Vector3 previous in followProgressPositions)
                if ((transform.position - previous).sqrMagnitude < 0.36f) { newGround = false; break; }
            if (newGround)
            {
                if (followProgressPositions.Count == 64) followProgressPositions.RemoveAt(0);
                followProgressPositions.Add(transform.position);
                followProgressAnchor = transform.position;
                followNoProgressSeconds = 0;
            }
        }
        return followNoProgressSeconds >= 10f;
    }

    bool BodyEmbedded()
    {
        // Inset the probe so ordinary floor contact and controller skin are not
        // mistaken for a mesh trapping an intentionally idle/waiting companion.
        Vector3 center = transform.position + motor.center;
        Vector3 half = Vector3.up * (motor.height * 0.5f - motor.radius);
        return Physics.CheckCapsule(center - half, center + half, motor.radius - 0.06f,
            environment, QueryTriggerInteraction.Ignore);
    }

    void TickRecovery(PhotonPlayer target)
    {
        using var sample = Game.AI.AIQueryProfile.Recovery.Auto();
        // Follow distance is not movement intent: a conversation, attack pause
        // or wait command can deliberately hold position with a distant buddy.
        bool wantsMovement = movementRequested;
        movementRequested = false;
        bool embedded = BodyEmbedded();
        bool stalledUnsupported = !groundSupported && Mathf.Abs(Velocity.y) < 0.1f;
        if (!wantsMovement)
        {
            recoveryWasMoving = false;
            if (embedded || stalledUnsupported)
            {
                if (idleInvalidSince < 0) idleInvalidSince = PhotonNetwork.Time;
            }
            else idleInvalidSince = -1;
            if (idleInvalidSince < 0 || PhotonNetwork.Time - idleInvalidSince < RecoveryDelay)
            {
                progressHistory.Clear();
                ResetRecovery();
                return;
            }
        }
        else
        {
            idleInvalidSince = -1;
            if (!recoveryWasMoving)
            {
                recoveryWasMoving = true;
                progressHistory.Clear();
                ResetRecovery();
            }
        }
        if ((transform.position - recoveryAnchor).sqrMagnitude >= 0.25f)
        {
            bool newGround = true;
            foreach (Vector3 previous in progressHistory)
                if ((transform.position - previous).sqrMagnitude < 0.36f) { newGround = false; break; }
            if (newGround)
            {
                progressHistory.Enqueue(transform.position);
                while (progressHistory.Count > 128) progressHistory.Dequeue();
                ResetRecovery();
                return;
            }
        }
        double immobileFor = PhotonNetwork.Time - lastProgressAt;
        if (immobileFor >= StuckRespawnDelay && ResetAtCompanionSpawn(true)) return;
        if (immobileFor < RecoveryDelay || PhotonNetwork.Time < nextRecoveryAt) return;
        nextRecoveryAt = PhotonNetwork.Time + RecoveryDelay;
        recoveryAttempts++;
        if (exploring) EndWander(true);

        // Release hand plants and stale navigation/traversal, then try bounded
        // physical recovery. These attempts do not restart the 20-second clock.
        truckExitRoute = System.Array.Empty<Vector3>();
        nextTruckExitSearch = Time.time + 3f;
        proceduralGait?.ResetPose();
        corners = System.Array.Empty<Vector3>();
        nextPath = 0;
        stuck = 0;
        vertical = groundSupported ? -2f : Mathf.Max(vertical, -4f);
        if (State == Activity.Climbing || State == Activity.Jumping
            || (State == Activity.Retreating && immobileFor >= 5))
        {
            sidewaysClimbing = false;
            State = followingPaused ? Activity.Waiting : Activity.Following;
            retreatUntil = 0;
            nextTraversal = PhotonNetwork.Time >= lastProgressAt + 5 ? 0 : Time.time + 1;
        }
        Vector3 before = transform.position;
        string recoveryAction = "no clear recovery route";
        if (embedded && TryDepenetrate()) recoveryAction = "separating from embedded geometry";
        else if (TrySettleOnFloor()) recoveryAction = "settling onto support";
        else if (embedded && TryMeshEscape(target)) recoveryAction = "escaping embedded geometry";
        else if (wantsMovement && TryRecoveryStep(target)) recoveryAction = "walking a recovery step";
        // A recovery nudge is not proof that ordinary following works again.
        recoveryAnchor += transform.position - before;
        followProgressAnchor += transform.position - before;
        lastPosition = transform.position;
        Velocity = Vector3.zero;
        groundSupported = TryGroundSupport(transform.position, out _);
        LastEvent = "local recovery attempt " + recoveryAttempts + ": " + recoveryAction;
    }

    bool TryDepenetrate()
    {
        Vector3 start = transform.position;
        Vector3 candidate = start;
        for (int iteration = 0; iteration < 4; iteration++)
        {
            Vector3 center = candidate + motor.center;
            Vector3 half = Vector3.up * (motor.height * 0.5f - motor.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center - half, center + half, motor.radius,
                recoveryOverlaps, environment, QueryTriggerInteraction.Ignore);
            bool moved = false;
            for (int i = 0; i < count; i++)
            {
                Collider obstacle = recoveryOverlaps[i];
                if (!Physics.ComputePenetration(hurtbox, candidate, transform.rotation,
                    obstacle, obstacle.transform.position, obstacle.transform.rotation, out Vector3 away, out float depth)
                    || depth < 0.005f) continue;
                candidate += away * Mathf.Min(depth + 0.02f, 0.35f);
                moved = true;
            }
            if ((candidate - start).sqrMagnitude > 1f) return false;
            if (moved && ClearBody(candidate))
            {
                motor.enabled = false;
                transform.position = candidate;
                motor.enabled = true;
                return true;
            }
            if (!moved) break;
        }
        return false;
    }

    bool TrySettleOnFloor()
    {
        if (groundSupported || !Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down,
            out RaycastHit floor, 1.6f, environment, QueryTriggerInteraction.Ignore)
            || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) return false;
        Vector3 landing = floor.point + Vector3.up * 0.04f;
        if (!ClearBody(landing) || !ClearBodySweep(transform.position, landing)) return false;
        Vector3 before = transform.position;
        motor.Move(landing - before);
        return (transform.position - before).sqrMagnitude > 0.0004f;
    }

    bool TryRecoveryStep(PhotonPlayer target)
    {
        Vector3 direction = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (target != null && target.TryGetHeadPosition(out Vector3 head))
            direction = Vector3.ProjectOnPlane(head - transform.position, Vector3.up).normalized;
        else if (Mode == CompanionMode.ReturningHome || homeWalking)
            direction = Vector3.ProjectOnPlane((Mode == CompanionMode.ReturningHome ? homePosition : homeDestination)
                - transform.position, Vector3.up).normalized;
        if (State == Activity.Retreating) direction = -direction;
        if (direction.sqrMagnitude < 0.01f) direction = Vector3.forward;
        // Alternate side steps, backtracking and diagonals. Sweep the whole body
        // and require support so recovery cannot step through a wall or off a roof.
        for (int i = 0; i < 8; i++)
        {
            float angle = ((i + recoveryAttempts) % 8) * 45f;
            Vector3 candidate = transform.position + Quaternion.Euler(0, angle, 0) * direction * 0.65f;
            if (!Physics.Raycast(candidate + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor,
                1f, environment, QueryTriggerInteraction.Ignore) || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) continue;
            candidate = floor.point + Vector3.up * 0.04f;
            if (!ClearBody(candidate) || !ClearBodySweep(transform.position, candidate)) continue;
            // Ordinary recovery follows a waypoint at normal walking speed.
            // Only confirmed mesh overlap may reposition the body instantly.
            localDetour = candidate;
            localDetourUntil = PhotonNetwork.Time + 1.5;
            return true;
        }
        return false;
    }

    bool TryMeshEscape(PhotonPlayer target)
    {
        // Concave meshes can overlap the body while ComputePenetration cannot
        // return a separation vector (the bus rear panel is one such case).
        // An initial-overlap sweep also rejects every route. Search the nearest
        // clear floor, allowing only the overlap at the start of the escape.
        if (ClearBody(transform.position)) return false;
        Vector3 forward = transform.forward;
        if (target != null && target.TryGetHeadPosition(out Vector3 head))
            forward = Vector3.ProjectOnPlane(head - transform.position, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        for (float distance = 0.2f; distance <= 1.01f; distance += 0.2f)
        for (int i = 0; i < 8; i++)
        {
            float angle = (i % 2 == 0 ? 1 : -1) * (90f + (i / 2) * 45f);
            Vector3 candidate = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * forward * distance;
            if (!Physics.Raycast(candidate + Vector3.up * 0.3f, Vector3.down, out RaycastHit floor,
                0.8f, environment, QueryTriggerInteraction.Ignore) || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) continue;
            candidate.y = floor.point.y + 0.04f;
            if (!ClearBody(candidate)) continue;
            Vector3 before = transform.position;
            Vector3 reverse = before - candidate;
            Vector3 center = candidate + motor.center;
            Vector3 half = Vector3.up * (motor.height * 0.5f - motor.radius);
            if (Physics.CapsuleCast(center - half, center + half, motor.radius - motor.skinWidth,
                reverse.normalized, out RaycastHit obstruction, reverse.magnitude, environment, QueryTriggerInteraction.Ignore)
                && obstruction.distance < reverse.magnitude - motor.radius * 2f) continue;
            // This is overlap recovery only, bounded to one metre. Ordinary
            // walking/climbing still requires a completely unobstructed sweep.
            motor.enabled = false;
            transform.position = candidate;
            motor.enabled = true;
            localDetour = candidate;
            localDetourUntil = PhotonNetwork.Time + 1.5;
            return true;
        }
        return false;
    }

    public bool ReactToPlayerHit(int actor, Vector3 hitVelocity)
    {
        PhotonPlayer attacker = PhotonPlayer.ForActor(actor);
        if (!HasAuthority || IsDead || attacker == null || attacker.IsDead || PhotonNetwork.Time < nextHitReaction
            || !PhotonPlayer.IsValidValue(hitVelocity.x) || !PhotonPlayer.IsValidValue(hitVelocity.y)
            || !PhotonPlayer.IsValidValue(hitVelocity.z) || hitVelocity.magnitude < hitReactionMinSpeed) return false;
        if (!attacker.TryGetHeadPosition(out retreatLookPosition)) return false;
        CancelKillerAttack();
        EndWander();
        // One reaction per retreat: a held item must not keep restarting the voice/timer.
        nextHitReaction = PhotonNetwork.Time + 5;
        retreatUntil = PhotonNetwork.Time + 5;
        retreatActor = actor;
        State = Activity.Retreating;
        vertical = Mathf.Min(vertical, 0f);
        stuck = 0;
        corners = System.Array.Empty<Vector3>();
        proceduralGait?.ResetPose();
        dropUntil = localDetourUntil = 0;
        LastEvent = "ouch";
        Dialogue.React("hurt", true);
        return true;
    }

    void TickRetreat()
    {
        movementRequested = true;
        PhotonPlayer attacker = PhotonPlayer.ForActor(retreatActor);
        if (attacker != null && attacker.TryGetHeadPosition(out Vector3 head)) retreatLookPosition = head;
        Vector3 away = Vector3.ProjectOnPlane(transform.position - retreatLookPosition, Vector3.up).normalized;
        if (away.sqrMagnitude < 0.001f) away = -transform.forward;
        Vector3 direction = Vector3.zero;
        bool supported = TryGroundSupport(transform.position, out Vector3 supportNormal);
        // A sphere probe at a ledge returns a diagonal corner normal. Only use
        // the surface directly under his feet to steer retreat along a slope.
        Vector3 retreatNormal = Vector3.up;
        if (supported && Physics.Raycast(transform.position + Vector3.up * 0.3f, Vector3.down,
            out RaycastHit retreatFloor, 0.6f, environment, QueryTriggerInteraction.Ignore)
            && Game.AI.CrawlGraph.IsWalkableGround(retreatFloor.normal))
            retreatNormal = retreatFloor.normal;
        // Avoid solid obstacles, but allow retreat to carry him off ledges.
        // Gravity handles the fall; normal following resumes after the retreat timer.
        for (int i = 0; i < 5; i++)
        {
            float angle = i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * ((i + 1) / 2) * 40f;
            Vector3 candidate = Quaternion.AngleAxis(angle, Vector3.up) * away;
            if (supported) candidate = Vector3.ProjectOnPlane(candidate, retreatNormal).normalized;
            if (!ClearBodySweep(transform.position, transform.position + candidate * 0.4f)) continue;
            direction = candidate;
            break;
        }
        if (!motor.enabled) motor.enabled = true;
        if (supported) vertical = -2f;
        else vertical -= gravity * Time.deltaTime;
        Vector3 grounding = supported ? -supportNormal * 0.5f : Vector3.up * vertical;
        CollisionFlags flags = motor.Move((direction * retreatSpeed + grounding) * Time.deltaTime);
        if ((flags & CollisionFlags.Above) != 0) vertical = Mathf.Min(0f, vertical);
    }

    Vector3 FindGround(Vector3 head, bool reusePlanningObservation = false)
    {
        return targetGroundSnapshot.Query(head, 3f, environment, out RaycastHit hit, reusePlanningObservation)
            ? hit.point : head - Vector3.up * eyeHeight;
    }

    bool CompletePath(Vector3 destination)
    {
        return NavMesh.SamplePosition(transform.position, out NavMeshHit start, 2f, NavMesh.AllAreas)
            && NavMesh.SamplePosition(destination, out NavMeshHit end, 1.5f, NavMesh.AllAreas)
            && NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path)
            && path.status == NavMeshPathStatus.PathComplete;
    }

    bool TryRoute(Vector3 destination, bool requireComplete, out Vector3[] route)
    {
        bool found = RequestNavigation(NavigationPlan.Route, destination);
        route = found ? completedNavigationRoute : System.Array.Empty<Vector3>();
        return found;
    }

    void Move(Vector3 destination, bool walk)
    {
        if (TickFenceRelease(Time.deltaTime)) return;
        TickFenceAttempt();
        if (walk && TickFenceDetour(destination, Time.deltaTime)) return;
        if (walk && WalkTruckExit(destination, Time.deltaTime)) return;
        if (walk && WalkSubwayStairs(destination, Time.deltaTime)) return;
        if (walk && WalkInteriorStairs(destination, Time.deltaTime)) return;
        bool committedDrop = PhotonNetwork.Time < dropUntil
            && (transform.position.y > dropLanding.y + 0.1f
                || Vector3.Dot(transform.position - dropExit, dropDirection) < 0f);
        // A shallow city curb can enter the follow/height stopping tolerance
        // midway through a valid drop. Finish clearing its lip before stopping;
        // otherwise grounded slope pressure nudges us back up and we retry forever.
        if (committedDrop && (!followingPaused || PhotonNetwork.Time < fallReleaseUntil)) walk = true;
        UpdateLedgeAttempt(destination, walk);
        committedDrop = committedDrop && PhotonNetwork.Time < dropUntil;
        movementRequested = walk;
        // A genuinely new destination may require retracing a successful route.
        if ((destination - progressGoal).sqrMagnitude > 64f)
        {
            progressGoal = destination;
            progressHistory.Clear();
            obstacleRoute = System.Array.Empty<Vector3>();
        }
        if (!motor.enabled) motor.enabled = true;
        bool onReturnStairs = Mode == CompanionMode.ReturningHome && returnStairCorner > 0
            && returnStairCorner < returnStairs.Length;
        if (onReturnStairs) corners = System.Array.Empty<Vector3>();
        if (Time.time >= nextPath)
        {
            nextPath = Time.time + 0.8f;
            // A partial route must never masquerade as a route to the player.
            if (Mode == CompanionMode.HomeWandering || onReturnStairs)
                corners = System.Array.Empty<Vector3>(); // Home waypoints already have checked corridors.
            else RequestNavigation(NavigationPlan.Route, destination);
            if (corners.Length == 0) corner = 0;
        }
        while (corner < corners.Length && Vector3.Distance(transform.position, corners[corner]) < 0.6f) corner++;
        Vector3 aim = corner < corners.Length ? corners[corner] : destination;
        if ((destination - obstacleTarget).sqrMagnitude > 64f || Time.time >= obstacleRouteUntil)
        {
            obstacleRoute = System.Array.Empty<Vector3>();
            fenceTakeoffPending = false;
        }
        while (obstacleCorner < obstacleRoute.Length
            && Vector3.Distance(transform.position, obstacleRoute[obstacleCorner]) < (fenceTakeoffPending ? 0.08f : 0.35f))
        {
            obstacleCorner++;
            obstacleRouteUntil = Time.time + 12f;
        }
        if (walk && fenceTakeoffPending && obstacleCorner >= obstacleRoute.Length)
        {
            fenceTakeoffPending = false;
            // Revalidate at the actual takeoff position; moving obstacles or a
            // slightly early waypoint arrival must never turn into clipping.
            if (obstacleRoute.Length > 0
                && Vector3.Distance(transform.position, obstacleRoute[obstacleRoute.Length - 1]) < 0.15f
                && BeginTraversal(fenceLanding, fenceLandingSurface, false)) return;
            nextObstacleSearch = Time.time + 3f;
        }
        bool obstacleDetour = walk && obstacleCorner < obstacleRoute.Length;
        if (obstacleDetour) aim = obstacleRoute[obstacleCorner];
        bool needsHigherLevel = !committedDrop && walk && (State == Activity.Following || State == Activity.Wandering)
            && destination.y > transform.position.y + motor.stepOffset + 0.05f;
        if ((!needsHigherLevel && !followingInteriorExit) || levelCorner >= levelRoute.Length || Time.time > levelRouteUntil || (destination - levelTarget).sqrMagnitude > 9f)
        {
            levelRoute = System.Array.Empty<Vector3>();
            followingInteriorExit = false;
        }
        if (needsHigherLevel && levelRoute.Length == 0 && Time.time >= nextLevelSearch
            && Physics.Raycast(transform.position + Vector3.up * eyeHeight, Vector3.up,
                out RaycastHit ceiling, destination.y - transform.position.y, environment, QueryTriggerInteraction.Ignore))
        {
            nextLevelSearch = Time.time + 2f;
            RequestNavigation(NavigationPlan.RoofExit, destination, ceiling.collider.bounds);
        }
        // Allow for floor contact at intermediate waypoints, but reach the
        // checked exterior approach closely before starting the roof climb.
        while (levelCorner < levelRoute.Length
            && Vector3.Distance(transform.position, levelRoute[levelCorner]) < (levelCorner == levelRoute.Length - 1 ? 0.25f : 0.35f)) levelCorner++;
        if (needsHigherLevel && !followingInteriorExit && levelRoute.Length > 0 && levelCorner == levelRoute.Length && Time.time >= nextTraversal)
        {
            nextTraversal = Time.time + 0.7f;
            traversalProbeMinimumHeight = destination.y - 0.25f;
            try
            {
                if (TryTraversal(Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).normalized, destination)) return;
            }
            finally { traversalProbeMinimumHeight = null; }
        }
        bool changingLevel = levelCorner < levelRoute.Length;
        if (changingLevel)
        {
            aim = levelRoute[levelCorner];
            // Follow the exit route's floor first, including dropping off shop
            // counters. The buddy's roof must not turn that drop into an ascent.
            destination = aim;
        }
        // Ground graph nodes can sit on the road below a vehicle or beside its
        // panel. Near a buddy on another level, prefer a clear approach at our
        // own height; ascent/drop planners still check the complete traversal.
        // Otherwise a valid graph route can steer us into or beneath the bus.
        Vector3 directExit = new Vector3(destination.x, transform.position.y, destination.z);
        if (!changingLevel && !obstacleDetour && walk && Mathf.Abs(destination.y - transform.position.y) > 0.3f
            && (directExit - transform.position).sqrMagnitude <= 36f
            && ClearBodySweep(transform.position, directExit)) aim = destination;
        bool detouring = walk && PhotonNetwork.Time < localDetourUntil
            && Vector3.ProjectOnPlane(localDetour - transform.position, Vector3.up).sqrMagnitude > 0.025f;
        if (detouring) aim = localDetour;
        else localDetourUntil = 0;
        Vector3 horizontal = Vector3.ProjectOnPlane(aim - transform.position, Vector3.up);
        Vector3 direction = horizontal.sqrMagnitude > 0.05f && walk ? horizontal.normalized : Vector3.zero;
        bool supported = TryGroundSupport(transform.position, out Vector3 supportNormal);
        if (walk && supported && !changingLevel && !detouring && !needsHigherLevel
            && Time.time >= nextInteriorSearch && direction.sqrMagnitude > 0f
            && !ClearBodySweep(transform.position, transform.position + direction * 0.75f)
            && Physics.Raycast(transform.position + Vector3.up * eyeHeight, Vector3.up,
                out RaycastHit interiorCeiling, 8f, environment, QueryTriggerInteraction.Ignore))
        {
            nextInteriorSearch = Time.time + 2f;
            if (TryInteriorExit(destination, interiorCeiling.collider.bounds)) return;
        }
        // Duck fits beneath some vehicle floors. Plan an ascent before ordinary
        // walking takes him underneath his friend, where jumping would hit the floor.
        bool wantsAscent = !committedDrop && walk && destination.y > transform.position.y + motor.stepOffset + 0.05f;
        Vector3 floorProbe = transform.position + direction * 0.8f;
        floorProbe.y = destination.y + 0.25f;
        bool raisedFloorAhead = wantsAscent && Physics.Raycast(floorProbe, Vector3.down, out RaycastHit raisedFloor,
            destination.y - transform.position.y + 0.5f, environment, QueryTriggerInteraction.Ignore)
            && raisedFloor.normal.y > 0.98f && raisedFloor.point.y > transform.position.y + motor.stepOffset;
        if (raisedFloorAhead && !detouring && !obstacleDetour && supported && supportNormal.y > 0.98f && Time.time > nextTraversal)
        {
            nextTraversal = Time.time + 0.7f;
            if (direction.sqrMagnitude > 0f) RequestNavigation(NavigationPlan.Ascent, destination, default, direction);
        }
        // Detect the face of an approaching ramp before the body reaches it.
        // The same 80-degree rule as minions distinguishes ramps from walls.
        if (supported && direction.sqrMagnitude > 0f
            && Physics.Raycast(transform.position + Vector3.up * motor.radius, direction,
                out RaycastHit ramp, 0.5f, environment, QueryTriggerInteraction.Ignore)
            && Game.AI.CrawlGraph.IsWalkableGround(ramp.normal)) supportNormal = ramp.normal;
        // At a crest the current support is still flat. Look for a connected
        // descending ramp, while rejecting a distant floor below a roof edge.
        if (supported && direction.sqrMagnitude > 0f
            && Physics.Raycast(transform.position + direction * 0.35f + Vector3.up * 0.6f,
                Vector3.down, out RaycastHit ahead, 3f, environment, QueryTriggerInteraction.Ignore)
            && ahead.normal.y < 0.98f && Game.AI.CrawlGraph.IsWalkableGround(ahead.normal)
            && Mathf.Abs(Vector3.Dot(transform.position - ahead.point, ahead.normal)) < motor.radius + 0.15f)
            supportNormal = ahead.normal;
        Vector3 travel = supported ? Vector3.ProjectOnPlane(direction, supportNormal).normalized : direction;
        bool hasFloor = TryGroundSupport(transform.position + travel * 0.35f, out _, 0.5f);
        // A bus roof can make the normal jump arc impossible. Walk out through a
        // clear opening and let gravity handle a drop instead. Probe from body
        // height so a roof above us cannot hide the road below the doorway.
        bool dropping = walk && PhotonNetwork.Time < dropUntil;
        bool clearedDropLip = Vector3.Dot(transform.position - dropExit, dropDirection) >= 0f;
        if (dropping && clearedDropLip && supported && transform.position.y <= dropLanding.y + 0.1f)
        {
            dropping = false;
            dropUntil = 0;
        }
        if (!dropping && !obstacleDetour && !NearAvoidedLedge && walk && stuck > 0.15f && Time.time >= nextEdgeRelease)
        {
            nextEdgeRelease = Time.time + 0.3f;
            dropping = TryEdgeRelease();
        }
        if (!dropping && !detouring && !obstacleDetour && !NearAvoidedLedge && walk && supported
            && Time.time >= nextDropProbe && destination.y < transform.position.y - 0.15f
            && (!hasFloor || stuck > 0.35f
                || Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).sqrMagnitude < 2.25f))
        {
            nextDropProbe = Time.time + 0.35f;
            dropping = TryDropDirection(direction, out dropDirection);
        }
        if (dropping)
        {
            clearedDropLip = Vector3.Dot(transform.position - dropExit, dropDirection) >= 0f;
            travel = clearedDropLip ? Vector3.zero : dropDirection;
            // A wide support cast can keep touching the sloped lip after our
            // feet leave it. During a checked step-off, require floor directly
            // beneath the feet so the old slope cannot suspend Duck in midair.
            supported = Physics.Raycast(transform.position + Vector3.up * 0.08f, Vector3.down,
                out RaycastHit dropSupport, 0.18f, environment, QueryTriggerInteraction.Ignore)
                && Game.AI.CrawlGraph.IsWalkableGround(dropSupport.normal);
            supportNormal = supported ? dropSupport.normal : Vector3.up;
            // Retain the checked outward heading until the entire body clears.
            hasFloor = true;
        }
        if (supported) vertical = -2f;
        else vertical -= gravity * Time.deltaTime;
        Vector3 before = transform.position;
        Vector3 grounding = supported ? -supportNormal * 0.5f : Vector3.up * vertical;
        CollisionFlags flags = motor.Move(((hasFloor ? travel : Vector3.zero) * speed + grounding) * Time.deltaTime);
        if ((flags & CollisionFlags.Above) != 0) vertical = Mathf.Min(0f, vertical);
        float displacement = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
        stuck = walk && displacement < speed * Time.deltaTime * 0.15f ? stuck + Time.deltaTime : 0f;
        if (!dropping && direction.sqrMagnitude > 0f && Time.time > nextTraversal && supported && stuck > 0.35f)
        {
            nextTraversal = Time.time + 2f;
            // A pillar or chair beside a doorway needs a lateral approach, not
            // repeated jumps at the same obstruction. Retain that waypoint so
            // following does not immediately steer back into the pillar.
            // Prefer a committed ground detour, but allow checked jumps/climbs
            // over an obstruction even when the buddy is on the same floor.
            RequestNavigation(NavigationPlan.Obstruction, destination, default, direction);
        }
        if (stuck > 5f)
        {
            LastEvent = "route blocked";
            nextPath = 0;
            // Try a recently observed approach point rather than repeatedly jumping at the same wall.
            RequestNavigation(NavigationPlan.Recovery, destination);
            stuck = 0;
        }
    }

    void UpdateLedgeAttempt(Vector3 destination, bool walk)
    {
        // Track the crossing, not each shuffle: small lateral movements must not
        // buy another unlimited set of drop attempts. Real descent resets it.
        if (!walk || destination.y >= transform.position.y - 0.15f)
        {
            ledgeAttemptSince = -1f;
            return;
        }
        if (NearAvoidedLedge)
        {
            if (obstacleCorner >= obstacleRoute.Length) TryObstacleRoute(destination);
            return;
        }
        if (obstacleCorner < obstacleRoute.Length) return;
        if (ledgeAttemptSince >= 0f && (transform.position.y < ledgeAttemptOrigin.y - 0.4f
            || Vector3.ProjectOnPlane(transform.position - ledgeAttemptOrigin, Vector3.up).sqrMagnitude > 4f))
            ledgeAttemptSince = -1f;
        Vector3 toward = Vector3.ProjectOnPlane(destination - transform.position, Vector3.up).normalized;
        bool attempting = PhotonNetwork.Time < dropUntil || stuck > 0.15f
            || !TryGroundSupport(transform.position + toward * 0.35f, out _, 0.5f);
        if (ledgeAttemptSince < 0f)
        {
            if (attempting) { ledgeAttemptSince = Time.time; ledgeAttemptOrigin = transform.position; }
            return;
        }
        if (Time.time - ledgeAttemptSince < 3f || !TryGroundSupport(transform.position, out _)) return;
        avoidedLedge = ledgeAttemptOrigin;
        avoidedLedgeUntil = Time.time + 15f;
        ledgeAttemptSince = -1f;
        dropUntil = localDetourUntil = 0;
        nextObstacleSearch = 0;
        TryObstacleRoute(destination);
    }

    void RegisterFenceAttempt(RaycastHit fence, Vector3 destination)
    {
        if (fenceDetourCommitted || fenceAttemptUntil > 0
            || (PhotonNetwork.Time < failedFenceUntil && (fence.point - failedFenceCrossing).sqrMagnitude < 16f)) return;
        fenceAttemptPoint = fence.point;
        fenceAttemptNormal = Vector3.ProjectOnPlane(fence.normal, Vector3.up).normalized;
        if (Vector3.Dot(transform.position - fenceAttemptPoint, fenceAttemptNormal) < 0)
            fenceAttemptNormal = -fenceAttemptNormal;
        fenceDetourTarget = destination;
        fenceAttemptUntil = PhotonNetwork.Time + 5;
    }

    void TickFenceAttempt()
    {
        if (fenceAttemptUntil <= 0 || IsDead) return;
        // Success is reaching the far side, not a lateral shuffle or takeoff.
        if (groundSupported && Vector3.Dot(transform.position - fenceAttemptPoint, fenceAttemptNormal) < -0.4f)
        { fenceAttemptUntil = 0; return; }
        if (PhotonNetwork.Time < fenceAttemptUntil
            || State == Activity.Jumping || State == Activity.Climbing) return;
        failedFenceCrossing = fenceAttemptPoint;
        failedFenceUntil = PhotonNetwork.Time + 30;
        fenceAttemptUntil = 0;
        fenceTakeoffPending = false;
        fenceDetourCommitted = true;
        fenceDetourRoute = System.Array.Empty<Vector3>();
        nextFenceDetourSearch = 0;
        corners = levelRoute = obstacleRoute = System.Array.Empty<Vector3>();
        localDetourUntil = 0;
        LastEvent = "fence attempt exhausted; finding a longer route";
    }

    bool TryFenceDetourRoute(Vector3 destination) => RequestNavigation(NavigationPlan.Fence, destination);

    bool TickFenceDetour(Vector3 destination, float dt)
    {
        if (!fenceDetourCommitted) return false;
        if ((destination - fenceDetourTarget).sqrMagnitude > 64f)
        {
            fenceDetourCommitted = false;
            fenceDetourRoute = System.Array.Empty<Vector3>();
            return false;
        }
        while (fenceDetourCorner < fenceDetourRoute.Length
            && Vector3.Distance(transform.position, fenceDetourRoute[fenceDetourCorner]) < 0.3f)
            fenceDetourCorner++;
        if (fenceDetourCorner >= fenceDetourRoute.Length)
        {
            if (Vector3.Distance(transform.position, destination) < 1f)
            { fenceDetourCommitted = false; return false; }
            if (Time.time >= nextFenceDetourSearch)
            {
                nextFenceDetourSearch = Time.time + 3f;
                TryFenceDetourRoute(destination);
            }
            // A failed complete-ground-route search must not own movement.
            // Let the ordinary alternate-route/traversal/recovery machinery
            // work while the failed crossing remains locally avoided.
            if (fenceDetourCorner >= fenceDetourRoute.Length) return false;
        }
        movementRequested = true;
        if (!motor.enabled) motor.enabled = true;
        bool supported = TryGroundSupport(transform.position, out _);
        vertical = supported ? -2f : vertical - gravity * dt;
        Vector3 travel = Vector3.zero;
        if (fenceDetourCorner < fenceDetourRoute.Length)
        {
            Vector3 point = fenceDetourRoute[fenceDetourCorner];
            if (!ClearBodySweep(transform.position, point))
            {
                fenceDetourRoute = System.Array.Empty<Vector3>();
                nextFenceDetourSearch = Time.time + 3f;
                return false;
            }
            else travel = Vector3.ClampMagnitude(Vector3.ProjectOnPlane(point - transform.position, Vector3.up), speed * dt);
        }
        State = Activity.Following;
        Vector3 before = transform.position;
        motor.Move(travel + Vector3.up * (vertical * dt));
        float displacement = Vector3.ProjectOnPlane(transform.position - before, Vector3.up).magnitude;
        stuck = travel.sqrMagnitude > 0.000001f && displacement < travel.magnitude * 0.15f ? stuck + dt : 0f;
        if (stuck >= 0.75f)
        {
            // A geometrically valid route can still snag the controller.
            // Drop that local plan without renewing avoidance or recovery time.
            fenceDetourRoute = System.Array.Empty<Vector3>();
            nextFenceDetourSearch = Time.time + 3f;
            LastEvent = "fence detour blocked; trying alternate movement";
        }
        // Holding the route prevents generic shortest-path/jump selection from
        // steering back at the fence. Existing recovery still runs afterwards.
        return true;
    }

    bool TryFenceTakeoffRoute(Vector3 destination) => RequestNavigation(NavigationPlan.FenceTakeoff, destination);

    bool TryObstacleRoute(Vector3 destination)
    {
        if (navigationPlan == null && Time.time < nextObstacleSearch) return false;
        return RequestNavigation(NavigationPlan.Obstacle, destination);
    }



    bool TryLocalDetour(Vector3 forward)
    {
        if (ClearBodySweep(transform.position, transform.position + forward * 0.5f)) return false;
        for (int i = 0; i < 4; i++)
        {
            float angle = (i % 2 == 0 ? 1 : -1) * (i < 2 ? 60f : 90f);
            Vector3 sample = transform.position + Quaternion.AngleAxis(angle, Vector3.up) * forward * 0.7f;
            if (!Physics.Raycast(sample + Vector3.up * 0.3f, Vector3.down, out RaycastHit floor,
                0.7f, environment, QueryTriggerInteraction.Ignore)
                || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) continue;
            Vector3 candidate = floor.point + Vector3.up * 0.04f;
            if (!ClearBody(candidate) || !ClearBodySweep(transform.position, candidate)) continue;
            localDetour = candidate;
            localDetourUntil = PhotonNetwork.Time + 1.5;
            LastEvent = "going around an obstruction";
            stuck = 0;
            return true;
        }
        return false;
    }

    bool TryRoofApproach(Vector3 destination, Bounds roof) => RequestNavigation(NavigationPlan.Roof, destination, roof);

    bool TryInteriorExit(Vector3 destination, Bounds building) => RequestNavigation(NavigationPlan.Interior, destination, building);



    bool TryClimbApproach(Vector3 destination) => RequestNavigation(NavigationPlan.Climb, destination);

    float nextDropProbe, nextEdgeRelease;

    void TickFallSnag()
    {
        if (!HasAuthority || IsDead || State == Activity.Climbing || State == Activity.Jumping)
        {
            descendedWithoutLanding = false;
            stalledFallSince = -1;
            return;
        }
        // Real standing support ends the fall normally. Do not eject an idle
        // companion from a usable roof, sill or platform merely for being still.
        if (groundSupported)
        {
            descendedWithoutLanding = false;
            stalledFallSince = -1;
            return;
        }
        if (Velocity.y < -0.5f) descendedWithoutLanding = true;
        if (!descendedWithoutLanding || Mathf.Abs(Velocity.y) > 0.12f || vertical > -1f)
        {
            stalledFallSince = -1;
            return;
        }
        if (stalledFallSince < 0) stalledFallSince = PhotonNetwork.Time;
        if (PhotonNetwork.Time - stalledFallSince < 0.65 || PhotonNetwork.Time < nextFallRelease) return;
        nextFallRelease = PhotonNetwork.Time + 0.75;
        TryFallSnagRelease();
    }

    bool TryFallSnagRelease()
    {
        Vector3 origin = transform.position;
        // Unlike a foot snag, trim can contact the middle of the capsule.
        // Probe several body heights and both sides of building corners.
        for (int height = 0; height < 4; height++)
        for (int side = 0; side < 12; side++)
        {
            Vector3 radial = Quaternion.Euler(0, side * 30f, 0) * Vector3.forward;
            // The rounded capsule bottom can catch below the root while every
            // body-height ray passes above a thin trim strip. Include its underside.
            float probeHeight = height == 0 ? -0.12f : 0.08f + (height - 1) * 0.25f;
            Vector3 probe = origin + Vector3.up * probeHeight + radial * 0.7f;
            if (!Physics.Raycast(probe, -radial, out RaycastHit wall, 0.9f, environment, QueryTriggerInteraction.Ignore)
                || Mathf.Abs(wall.normal.y) > 0.5f) continue;
            Vector3 outward = Vector3.ProjectOnPlane(wall.normal, Vector3.up).normalized;
            if (Vector3.Dot(outward, radial) < 0.5f) continue;
            for (int angle = 0; angle < 5; angle++)
            for (float reach = 0.24f; reach <= 0.85f; reach += 0.15f)
            {
                float turn = angle == 0 ? 0 : (angle % 2 == 1 ? 1 : -1) * ((angle + 1) / 2) * 30f;
                Vector3 away = Quaternion.Euler(0, turn, 0) * outward;
                Vector3 exit = origin + away * reach;
                if (!ClearBody(exit) || !ClearBodySweep(origin, exit)) continue;
                if (!Physics.Raycast(exit + Vector3.up * 0.08f, Vector3.down, out RaycastHit floor,
                    Mathf.Infinity, environment, QueryTriggerInteraction.Ignore)
                    || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal) || floor.point.y > origin.y - 0.25f) continue;
                Vector3 landing = floor.point + Vector3.up * 0.04f;
                if (!ClearBody(landing) || !ClearBodySweep(exit, landing)) continue;
                dropDirection = away;
                dropExit = exit;
                dropLanding = landing;
                dropUntil = fallReleaseUntil = PhotonNetwork.Time + reach / Mathf.Max(speed, 0.1f) + 2f;
                localDetourUntil = 0;
                corners = levelRoute = obstacleRoute = System.Array.Empty<Vector3>();
                followingInteriorExit = false;
                nextPath = nextTraversal = Time.time + 2f;
                vertical = -2f;
                proceduralGait?.ResetPose();
                LastEvent = "clearing trim after a stalled fall";
                return true;
            }
        }
        // Keep normal route and recovery timers intact when no safe exit exists.
        return false;
    }

    bool TryEdgeRelease()
    {
        // Detect marginal support, including a capsule straddling a lip whose
        // centre still has floor. A wide sphere cast alone cannot distinguish it.
        Vector3 origin = transform.position;
        int supports = 0;
        for (int i = 0; i < 4; i++)
        {
            Vector3 offset = Quaternion.Euler(0, i * 90f, 0) * Vector3.forward * (motor.radius + motor.skinWidth);
            if (Physics.Raycast(origin + offset + Vector3.up * 0.08f, Vector3.down,
                out RaycastHit floor, 0.18f, environment, QueryTriggerInteraction.Ignore)
                && Game.AI.CrawlGraph.IsWalkableGround(floor.normal)) supports++;
        }
        if (supports > 2) return false;
        // Probe the face just below the feet from outside, so its normal points
        // into the free passage regardless of where our buddy happens to be.
        for (int i = 0; i < 12; i++)
        {
            Vector3 radial = Quaternion.Euler(0, i * 30f, 0) * Vector3.forward;
            Vector3 probe = origin + radial * 0.6f - Vector3.up * 0.12f;
            if (!Physics.Raycast(probe, -radial, out RaycastHit face, 0.8f, environment, QueryTriggerInteraction.Ignore)
                || Mathf.Abs(face.normal.y) > 0.5f) continue;
            Vector3 outward = Vector3.ProjectOnPlane(face.normal, Vector3.up).normalized;
            if (Vector3.Dot(outward, radial) < 0.5f) continue;
            // Shorter, body-sized exits fit passages where the normal 0.85m
            // drop probe lands inside the opposite building wall.
            for (int angle = 0; angle < 5; angle++)
            for (float distance = motor.radius + motor.skinWidth + 0.08f; distance <= 0.8f; distance += 0.12f)
            {
                float turn = angle == 0 ? 0 : (angle % 2 == 1 ? 1 : -1) * ((angle + 1) / 2) * 30f;
                Vector3 direction = Quaternion.Euler(0, turn, 0) * outward;
                Vector3 exit = origin + direction * distance;
                if (!ClearBody(exit) || !ClearBodySweep(origin, exit)) continue;
                if (!Physics.Raycast(exit + Vector3.up * 0.08f, Vector3.down, out RaycastHit floor,
                    Mathf.Infinity, environment, QueryTriggerInteraction.Ignore)
                    || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal) || floor.point.y > origin.y - 0.15f) continue;
                Vector3 landing = floor.point + Vector3.up * 0.04f;
                if (!ClearBody(landing) || !ClearBodySweep(exit, landing)) continue;
                dropDirection = direction;
                dropExit = exit;
                dropLanding = landing;
                dropUntil = PhotonNetwork.Time + distance / Mathf.Max(speed, 0.1f) + 2f;
                localDetourUntil = 0;
                corners = levelRoute = obstacleRoute = System.Array.Empty<Vector3>();
                followingInteriorExit = false;
                nextTraversal = Time.time + 2f;
                nextPath = Time.time + 1f;
                LastEvent = "releasing a snagged ledge";
                return true;
            }
            // If the passage is too tight here, look along the lip for a wider
            // departure. Both the lateral approach and its drop must be clear.
            Vector3 tangent = Vector3.Cross(Vector3.up, outward);
            for (int side = -1; side <= 1; side += 2)
            for (float reach = 0.6f; reach <= 1.8f; reach += 0.6f)
            {
                Vector3 approach = origin + tangent * (side * reach);
                Vector3 exit = approach + outward * (motor.radius * 2f + 0.08f);
                if (!ClearBody(approach) || !ClearBodySweep(origin, approach)
                    || !TryGroundSupport(approach, out _) || !ClearBody(exit) || !ClearBodySweep(approach, exit)) continue;
                if (!Physics.Raycast(exit + Vector3.up * 0.08f, Vector3.down, out RaycastHit floor,
                    Mathf.Infinity, environment, QueryTriggerInteraction.Ignore)
                    || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal) || floor.point.y > origin.y - 0.15f) continue;
                Vector3 landing = floor.point + Vector3.up * 0.04f;
                if (!ClearBody(landing) || !ClearBodySweep(exit, landing)) continue;
                localDetour = approach;
                localDetourUntil = PhotonNetwork.Time + reach / Mathf.Max(speed, 0.1f) + 1f;
                nextTraversal = Time.time + reach / Mathf.Max(speed, 0.1f) + 1f;
                LastEvent = "moving along the ledge to a clear drop";
                return false; // A lateral approach, not a committed drop yet.
            }
        }
        return false;
    }

    bool TryDropDirection(Vector3 forward, out Vector3 result)
    {
        result = Vector3.zero;
        if (NearAvoidedLedge) return false;
        // A buddy below the same roof can leave us with no horizontal heading,
        // or a heading along the roof instead of toward its edge. Search all
        // sides and far enough to clear the bus's curved shoulder and overhang.
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
        if (forward.sqrMagnitude < 0.01f) forward = Vector3.forward;
        forward.Normalize();
        for (int reach = 0; reach < 3; reach++)
        for (int i = 0; i < 12; i++)
        {
            float angle = i == 0 ? 0 : (i % 2 == 1 ? 1 : -1) * ((i + 1) / 2) * 30f;
            Vector3 direction = Quaternion.AngleAxis(angle, Vector3.up) * forward;
            float distance = Mathf.Max(0.85f, motor.radius * 2f + motor.skinWidth + 0.15f) + reach * 0.6f;
            Vector3 exit = transform.position + direction * distance;
            if (!ClearBody(exit) || !ClearBodySweep(transform.position, exit)) continue;
            if (!Physics.Raycast(exit + Vector3.up * 0.25f, Vector3.down, out RaycastHit floor,
                Mathf.Infinity, environment, QueryTriggerInteraction.Ignore)
                || !Game.AI.CrawlGraph.IsWalkableGround(floor.normal)
                || floor.point.y >= transform.position.y - 0.15f) continue;
            Vector3 landing = floor.point + Vector3.up * 0.04f;
            if (!ClearBody(landing) || !ClearBodySweep(exit, landing)) continue;
            result = direction;
            dropDirection = direction;
            dropLanding = landing;
            dropExit = exit;
            // Use a plane beyond the lip for completion, not just a short timer.
            // This timeout only releases a commitment obstructed by moving geometry.
            dropUntil = PhotonNetwork.Time + distance / Mathf.Max(speed, 0.1f) + 2f;
            localDetourUntil = 0;
            LastEvent = "stepping off a ledge";
            return true;
        }
        return false;
    }

    bool TryGroundSupport(Vector3 feet, out Vector3 normal, float probeLift = 0.1f)
    {
        using var sample = Game.AI.AIQueryProfile.Support.Auto();
        Game.AI.AIQueryProfile.SupportRequests++;
        Game.AI.AIQueryProfile.SupportSpherecasts++;
        Vector3 bottom = feet + motor.center - Vector3.up * (motor.height * 0.5f - motor.radius);
        if (Physics.SphereCast(bottom + Vector3.up * probeLift, motor.radius - motor.skinWidth,
            Vector3.down, out RaycastHit hit, probeLift + 0.25f, environment, QueryTriggerInteraction.Ignore)
            && Game.AI.CrawlGraph.IsWalkableGround(hit.normal))
        {
            normal = hit.normal;
            return true;
        }
        // At the foot/crest of a ramp the wider cast can hit an edge or start
        // touching its side. A narrow probe still finds the floor beneath us.
        Game.AI.AIQueryProfile.SupportRaycasts++;
        if (Physics.Raycast(bottom + Vector3.up * probeLift, Vector3.down, out hit,
            probeLift + 0.25f + motor.radius, environment, QueryTriggerInteraction.Ignore)
            && Game.AI.CrawlGraph.IsWalkableGround(hit.normal))
        {
            normal = hit.normal;
            return true;
        }
        normal = Vector3.up;
        return false;
    }

    bool TryTraversal(Vector3 direction, Vector3 destination) => RequestNavigation(NavigationPlan.Traversal, destination, default, direction);



    public static bool TryJumpDuration(Vector3 delta, float launchSpeed, float acceleration, float horizontalSpeed, out float duration)
    {
        duration = 0;
        if (acceleration <= 0 || launchSpeed <= 0 || horizontalSpeed <= 0) return false;
        float discriminant = launchSpeed * launchSpeed - 2f * acceleration * delta.y;
        if (discriminant < 0) return false;
        duration = (launchSpeed + Mathf.Sqrt(discriminant)) / acceleration;
        return duration > 0 && Vector3.ProjectOnPlane(delta, Vector3.up).magnitude <= horizontalSpeed * duration;
    }

    bool ClearBody(Vector3 feet)
    {
        float radius = motor.radius - motor.skinWidth;
        Vector3 center = feet + motor.center;
        Vector3 halfSegment = Vector3.up * (motor.height * 0.5f - motor.radius);
        return !Physics.CheckCapsule(center - halfSegment, center + halfSegment,
            radius, environment, QueryTriggerInteraction.Ignore);
    }

    bool BeginTraversal(Vector3 landing, Transform surface, bool climb)
    {
        if (traversalProbeMinimumHeight.HasValue && landing.y < traversalProbeMinimumHeight.Value) return false;
        Vector3 origin = traversalProbeOrigin ?? transform.position;
        Vector3 delta = landing - origin;
        if (AvoidFailedClimb(origin, landing)) return false;
        if (PhotonNetwork.Time < failedFenceUntil)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(delta, Vector3.up);
            float along = horizontal.sqrMagnitude > 0.001f
                ? Mathf.Clamp01(Vector3.Dot(failedFenceCrossing - origin, horizontal) / horizontal.sqrMagnitude) : 0;
            if (Vector3.ProjectOnPlane(origin + horizontal * along - failedFenceCrossing, Vector3.up).sqrMagnitude < 16f)
                return false;
        }
        float duration = 0, liftDuration = 0, launchSpeed = 0;
        Vector3 grip = landing;
        if (!ClearBody(landing)) return false;
        if (climb)
        {
            if (delta.y < 0.4f || Vector3.ProjectOnPlane(delta, Vector3.up).magnitude > 1.6f) return false;
            Vector3 approach = Vector3.ProjectOnPlane(delta, Vector3.up);
            Vector3 probe = new Vector3(origin.x, landing.y - 0.12f, origin.z);
            if (!Physics.Raycast(probe, approach.normalized, out RaycastHit edge, approach.magnitude,
                environment, QueryTriggerInteraction.Ignore)) return false;
            grip = edge.point + Vector3.up * 0.12f;
            // Climb using nearby wall holds, rather than reaching for the roof from the ground.
            liftDuration = Mathf.Max(0.5f, delta.y / 1.8f);
            duration = liftDuration + 0.5f;
            int steps = Mathf.Clamp(Mathf.CeilToInt(delta.y / 0.2f), 8, 2048);
            Vector3 previous = origin;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 position = origin + Vector3.up * (delta.y * i / steps);
                Vector3 handOrigin = position + Vector3.up * eyeHeight;
                handOrigin.y = Mathf.Min(handOrigin.y, landing.y - 0.12f);
                if (!Physics.Raycast(handOrigin, approach.normalized, out RaycastHit hold, 0.85f,
                    environment, QueryTriggerInteraction.Ignore) || Mathf.Abs(hold.normal.y) > 0.7f) return false;
                if (!ClearBody(position) || !ClearBodySweep(previous, position)) return false;
                previous = position;
            }
            if (!ClearBodySweep(previous, landing)) return false;
        }
        else
        {
            // Prefer low arcs under ceilings, but also try the full six-metre jump when an
            // intervening fence blocks a low arc to a same-height landing.
            bool clearJump = false;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                float normalLaunchSpeed = Mathf.Min(8.66f, jumpSpeed);
                if (attempt == 1 && jumpSpeed <= normalLaunchSpeed) break;
                launchSpeed = attempt == 0 ? normalLaunchSpeed : jumpSpeed;
                if (!TryJumpDuration(delta, launchSpeed, gravity, speed * 1.4f, out duration)) continue;
                int steps = Mathf.Clamp(Mathf.CeilToInt(duration * 60f), 16, 180);
                Vector3 previous = origin;
                clearJump = true;
                for (int i = 1; i <= steps; i++)
                {
                    Vector3 next = JumpPoint(origin, landing, i / (float)steps, duration, launchSpeed);
                    if (!ClearBody(next) || !ClearBodySweep(previous, next) || !ClearFenceMargin(previous, next))
                    {
                        clearJump = false;
                        break;
                    }
                    previous = next;
                }
                if (clearJump) break;
            }
            if (!clearJump) return false;
        }
        if (traversalProbeOrigin.HasValue)
        {
            if (sidewaysProbe)
            {
                sidewaysLanding = landing;
                sidewaysExitSurface = surface;
                sidewaysExitClimb = climb;
            }
            return true;
        }
        sidewaysClimbing = false;
        lastFenceContact = null;
        fenceStallSince = -1f;
        traversalDuration = duration;
        traversalJumpSpeed = launchSpeed;
        climbLiftDuration = liftDuration;
        localDetourUntil = dropUntil = 0;
        traversalStart = origin;
        traversalEnd = landing;
        ledge = grip;
        ledgeSurface = surface;
        localLanding = surface != null ? surface.InverseTransformPoint(landing) : landing;
        localLedge = surface != null ? surface.InverseTransformPoint(grip) : grip;
        traversalTime = 0;
        State = climb ? Activity.Climbing : Activity.Jumping;
        LastEvent = climb ? "climbing" : "jumping";
        if (!climb)
        {
            // Only committed jumps broadcast; path probes and remote pose updates stay silent.
            if (HasAuthority) RoomRpc.PlaySound(jumpSound, transform.position);
            reactedAirborne = true;
            airbornePeak = transform.position.y;
            Dialogue.ReactAirborne();
        }
        return true;
    }

    Vector3 JumpPoint(Vector3 start, Vector3 end, float t, float duration, float launchSpeed)
    {
        float horizontalT = t;
        if (end.y < start.y - 0.3f)
        {
            // Clear the edge at normal jump speed, even when the fall lasts much longer.
            float horizontalDistance = Vector3.ProjectOnPlane(end - start, Vector3.up).magnitude;
            float horizontalDuration = Mathf.Min(duration, Mathf.Max(launchSpeed / gravity, horizontalDistance / (speed * 1.4f)));
            horizontalT = Mathf.Clamp01(t * duration / horizontalDuration);
        }
        Vector3 point = Vector3.Lerp(start, end, horizontalT);
        point.y = start.y + launchSpeed * (t * duration) - 0.5f * gravity * t * t * duration * duration;
        return point;
    }

    bool ClearBodySweep(Vector3 from, Vector3 to)
    {
        using var sample = Game.AI.AIQueryProfile.Sweep.Auto();
        Game.AI.AIQueryProfile.SweepRequests++;
        Vector3 motion = to - from;
        if (motion.sqrMagnitude < 0.000001f) return true;
        Vector3 center = from + motor.center;
        Vector3 halfSegment = Vector3.up * (motor.height * 0.5f - motor.radius);
        return !Physics.CapsuleCast(center - halfSegment, center + halfSegment,
            motor.radius - motor.skinWidth, motion.normalized, motion.magnitude,
            environment, QueryTriggerInteraction.Ignore);
    }

    static bool IsFence(Collider collider) => collider != null
        && collider.name.IndexOf("fence", System.StringComparison.OrdinalIgnoreCase) >= 0;

    bool ClearFenceMargin(Vector3 from, Vector3 to)
    {
        // Expand the full capsule by 35 cm only against fences. This checks the
        // actual rails/posts, not the bounds of an entire perimeter mesh.
        float radius = motor.radius - motor.skinWidth + 0.35f;
        Vector3 half = Vector3.up * (motor.height * 0.5f - motor.radius);
        Vector3 center = to + motor.center;
        int count = Physics.OverlapCapsuleNonAlloc(center - half, center + half, radius,
            fenceClearanceOverlaps, environment, QueryTriggerInteraction.Ignore);
        if (count == fenceClearanceOverlaps.Length) return false;
        for (int i = 0; i < count; i++) if (IsFence(fenceClearanceOverlaps[i])) return false;
        Vector3 motion = to - from;
        if (motion.sqrMagnitude < 0.000001f) return true;
        center = from + motor.center;
        count = Physics.CapsuleCastNonAlloc(center - half, center + half, radius, motion.normalized,
            fenceClearanceHits, motion.magnitude, environment, QueryTriggerInteraction.Ignore);
        if (count == fenceClearanceHits.Length) return false;
        for (int i = 0; i < count; i++) if (IsFence(fenceClearanceHits[i].collider)) return false;
        return true;
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        if (!HasAuthority || !IsFence(hit.collider)) return;
        if (State != Activity.Jumping && groundSupported) return;
        lastFenceContact = hit.collider;
        lastFenceContactAt = Time.time;
    }

    void StartFenceRelease()
    {
        Vector3 away = Vector3.ProjectOnPlane(traversalStart - transform.position, Vector3.up);
        if (away.sqrMagnitude < 0.001f) away = -transform.forward;
        fenceReleaseDirection = away.normalized;
        fenceReleaseStart = transform.position;
        failedFenceCrossing = transform.position;
        failedFenceUntil = PhotonNetwork.Time + 30;
        fenceDetourCommitted = true;
        fenceAttemptUntil = 0;
        fenceReleaseUntil = PhotonNetwork.Time + 2;
        fenceStallSince = -1;
        fenceTakeoffPending = false;
        corners = levelRoute = obstacleRoute = System.Array.Empty<Vector3>();
        State = followingPaused ? Activity.Waiting : Activity.Following;
        vertical = -2f;
        nextTraversal = Time.time + 2f;
        proceduralGait?.ResetPose();
        LastEvent = "backing off a fence contact to fall clear";
    }

    bool TickFenceRelease(float dt)
    {
        if (PhotonNetwork.Time >= fenceReleaseUntil || IsDead) return false;
        float backedOff = Vector3.Dot(transform.position - fenceReleaseStart, fenceReleaseDirection);
        if (backedOff >= 0.7f && TryGroundSupport(transform.position, out _))
        {
            fenceReleaseUntil = 0;
            return false;
        }
        // Keep gravity and collisions active even if a narrow rail reported
        // support. This does not cancel the attack or alter its original deadline.
        movementRequested = true;
        vertical -= gravity * dt;
        Vector3 retreat = backedOff < 0.7f ? fenceReleaseDirection * 2f : Vector3.zero;
        motor.Move((retreat + Vector3.up * vertical) * dt);
        return true;
    }

    void Traverse() => AdvanceTraversal(Time.deltaTime);

    bool AvoidFailedClimb(Vector3 origin, Vector3 landing)
    {
        if (PhotonNetwork.Time >= failedClimbUntil || landing.y <= origin.y + 0.3f) return false;
        Vector3 approach = Vector3.ProjectOnPlane(origin - failedClimbOrigin, Vector3.up);
        Vector3 direction = Vector3.ProjectOnPlane(landing - origin, Vector3.up).normalized;
        return approach.sqrMagnitude < 6.25f && Vector3.Dot(direction, failedClimbDirection) > 0.5f;
    }

    void AbandonFailedClimb()
    {
        failedClimbUntil = PhotonNetwork.Time + 30;
        failedClimbOrigin = traversalStart;
        failedClimbDirection = Vector3.ProjectOnPlane(traversalEnd - traversalStart, Vector3.up).normalized;
        State = followingPaused ? Activity.Waiting : Activity.Following;
        vertical = -2f;
        corners = levelRoute = obstacleRoute = System.Array.Empty<Vector3>();
        nextPath = nextObstacleSearch = 0;
        nextTraversal = Time.time + 0.5f;
        proceduralGait?.ResetPose();
        LastEvent = "climb blocked; avoiding this approach and seeking another route";
        // Do not reset the recovery clock: climbing then sliding back down is
        // not proof of progress. Keep gravity and all other approaches available.
    }

    void AdvanceTraversal(float dt)
    {
        if (sidewaysClimbing) { AdvanceSidewaysClimb(dt); return; }
        movementRequested = true;
        traversalTime += dt;
        float t = Mathf.Clamp01(traversalTime / traversalDuration);
        bool climbing = State == Activity.Climbing;
        if (climbing && ledgeSurface != null)
        {
            traversalEnd = ledgeSurface.TransformPoint(localLanding);
            ledge = ledgeSurface.TransformPoint(localLedge);
        }
        Vector3 raised = new Vector3(traversalStart.x, traversalEnd.y, traversalStart.z);
        Vector3 next = climbing
            ? (traversalTime < climbLiftDuration ? Vector3.Lerp(traversalStart, raised, traversalTime / climbLiftDuration)
                : Vector3.Lerp(raised, traversalEnd, (traversalTime - climbLiftDuration) / 0.5f))
            : JumpPoint(traversalStart, traversalEnd, t, traversalDuration, traversalJumpSpeed);
        CollisionFlags collision = motor.Move(next - transform.position);
        if (State == Activity.Jumping
            && (collision & (CollisionFlags.Above | CollisionFlags.Sides | CollisionFlags.Below)) != 0
            && (next - transform.position).sqrMagnitude > 0.0025f)
        {
            // Also resolve contact from physics. Controller callbacks can be
            // absent in editor simulations or lost when support catches a rail.
            Vector3 center = transform.position + motor.center;
            Vector3 half = Vector3.up * (motor.height * 0.5f - motor.radius);
            int count = Physics.OverlapCapsuleNonAlloc(center - half, center + half, motor.radius + 0.05f,
                fenceClearanceOverlaps, environment, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
                if (IsFence(fenceClearanceOverlaps[i]))
                {
                    lastFenceContact = fenceClearanceOverlaps[i];
                    lastFenceContactAt = Time.time;
                    break;
                }
        }
        bool fenceBlocked = State == Activity.Jumping && lastFenceContact != null
            && Time.time - lastFenceContactAt < 0.2f
            && (next - transform.position).sqrMagnitude > 0.0025f;
        if (fenceBlocked)
        {
            if (fenceStallSince < 0) fenceStallSince = Time.time;
            if (Time.time - fenceStallSince >= 0.5f || (next - transform.position).sqrMagnitude > 0.04f)
            {
                StartFenceRelease();
                return;
            }
        }
        else fenceStallSince = -1;
        if (climbing && (((collision & (CollisionFlags.Above | CollisionFlags.Sides | CollisionFlags.Below)) != 0
                && (next - transform.position).sqrMagnitude > 0.04f)
            || (t >= 1f && Vector3.Distance(transform.position, traversalEnd) > 0.3f)))
        {
            Vector3 wallDirection = Vector3.ProjectOnPlane(traversalEnd - traversalStart, Vector3.up).normalized;
            if (!followingPaused && TrySidewaysClimb(wallDirection, traversalEnd)) return;
            AbandonFailedClimb();
            return;
        }
        if ((collision & (CollisionFlags.Above | CollisionFlags.Sides)) != 0 && (next - transform.position).sqrMagnitude > 0.04f)
        {
            State = followingPaused ? Activity.Waiting : Activity.Following;
            vertical = 0;
            nextTraversal = Time.time + 4f;
            LastEvent = "missed a climb";
        }
        else if (t >= 1f)
        {
            State = followingPaused ? Activity.Waiting : Activity.Following;
            vertical = -2f;
            nextPath = 0;
            LastEvent = climbing ? "reached a ledge" : "landed a jump";
        }
    }

    void Pose(PhotonPlayer target)
    {
        PhotonPlayer conversation = ConversationSpeaker;
        if (conversation != null) target = conversation;
        Vector3 forward = Vector3.ProjectOnPlane(Velocity, Vector3.up);
        if (State == Activity.Climbing)
            forward = sidewaysClimbing ? sidewaysWallDirection : Vector3.ProjectOnPlane(ledge - transform.position, Vector3.up);
        else if (State == Activity.Retreating)
            forward = Vector3.ProjectOnPlane(retreatLookPosition - transform.position, Vector3.up);
        else if (target != null && target.TryGetHeadPosition(out Vector3 targetHead)
            && (forward.sqrMagnitude < 0.2f || (conversation == null && Dialogue.Busy)))
            forward = Vector3.ProjectOnPlane(targetHead - transform.position, Vector3.up);
        if (forward.sqrMagnitude > 0.1f)
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(forward), 1f - Mathf.Exp(-6f * Time.deltaTime));
        float moving = Mathf.Clamp01(Vector3.ProjectOnPlane(Velocity, Vector3.up).magnitude / speed);
        if (proceduralGait == null || State == Activity.Climbing)
        {
            Head.position = transform.position + Vector3.up * eyeHeight;
            Head.rotation = transform.rotation;
        }
        Vector3 right = transform.right;
        LeftHand.position = Head.position - Vector3.up * 0.32f - right * 0.32f + transform.forward * 0.12f;
        RightHand.position = Head.position - Vector3.up * 0.32f + right * 0.32f + transform.forward * 0.12f;
        LeftHand.rotation = RightHand.rotation = transform.rotation;
        if (proceduralGait != null)
        {
            if (State == Activity.Climbing) proceduralGait.ResetPose();
            else
            {
                proceduralGait.Airborne = State == Activity.Jumping || !groundSupported;
                proceduralGait.Tick(Time.deltaTime);
            }
        }
        if (State == Activity.Climbing)
        {
            PoseClimbingHand(LeftHand, -1f, 0f);
            if (!KillerWeaponHeld) PoseClimbingHand(RightHand, 1f, 0.5f);
        }
        PoseSpeakingArms(moving, Dialogue.IsSpeaking);
        PoseHandExpression(moving, Dialogue.IsSpeaking);
        PoseKillerArm();
        // Also constrain gestures, airborne poses and climbing holds before networking the pose.
        proceduralGait?.ConstrainPoseTargets();
        Vector2 desiredLook = Vector2.zero;
        if (target != null && target.TryGetHeadPosition(out Vector3 lookAt))
        {
            Vector3 local = transform.InverseTransformDirection(lookAt - Head.position);
            desiredLook = new Vector2(
                Mathf.Clamp(-Mathf.Atan2(local.y, new Vector2(local.x, local.z).magnitude) * Mathf.Rad2Deg, -25f, 30f),
                Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -55f, 55f));
        }
        headLookAngles = Vector2.Lerp(headLookAngles, desiredLook, 1f - Mathf.Exp(-7f * Time.deltaTime));
        Head.rotation = transform.rotation * Quaternion.Euler(headLookAngles.x, headLookAngles.y, 0f);
        avatar.ApplyCompanionPose(new BodyPose {
            headPos = Head.position, headRot = Head.rotation,
            lHandPos = LeftHand.position, lHandRot = LeftHand.rotation,
            rHandPos = RightHand.position, rHandRot = RightHand.rotation,
            fingers = expressiveFingers
        });
    }

    void PoseSpeakingArms(float moving, bool speaking)
    {
        if (speaking && gestureSequence != Dialogue.SpeechSequence)
        {
            gestureSequence = Dialogue.SpeechSequence;
            // Six styles, never the same style on successive sentences.
            speakingGesture = (Mathf.Max(0, speakingGesture) + Random.Range(1, 6)) % 6;
        }
        bool canGesture = speaking && moving < 0.05f && groundSupported
            && State != Activity.Climbing && State != Activity.Jumping && State != Activity.Retreating;
        gestureWeight = Mathf.MoveTowards(gestureWeight, canGesture ? 1f : 0f, Time.deltaTime * 4f);
        if (moving >= 0.05f || !groundSupported || State == Activity.Climbing || State == Activity.Retreating) return;
        float beat = 0.5f + 0.5f * Mathf.Sin(Time.time * 2.6f);
        bool left = speakingGesture == 0 || speakingGesture == 2 || speakingGesture == 3 || speakingGesture == 4;
        bool right = speakingGesture == 1 || speakingGesture == 2 || speakingGesture == 3 || speakingGesture == 5;
        float lift = speakingGesture == 3 ? 0.09f : 0.18f + 0.07f * beat;
        float reach = speakingGesture >= 4 ? 0.16f : 0.04f * beat;
        float spread = speakingGesture == 2 ? 0.08f : 0.02f;
        if (left) LeftHand.position += (Vector3.up * lift + transform.forward * reach - transform.right * spread) * gestureWeight;
        if (right) RightHand.position += (Vector3.up * (lift * 0.9f) + transform.forward * reach + transform.right * spread) * gestureWeight;
    }

    void PoseHandExpression(float moving, bool speaking)
    {
        float leftGrip = HandExpression(LeftHand, -1f, 0f, moving, speaking, ref leftWristAngles);
        float rightGrip = HandExpression(RightHand, 1f, 0.5f, moving, speaking, ref rightWristAngles);
        var target = new FingerCurl {
            leftThumb = leftGrip * 0.75f, leftIndex = leftGrip, leftPinky = Mathf.Clamp01(leftGrip + 0.08f),
            rightThumb = rightGrip * 0.75f, rightIndex = rightGrip, rightPinky = Mathf.Clamp01(rightGrip + 0.08f)
        };
        expressiveFingers = FingerCurl.Lerp(expressiveFingers, target, 1f - Mathf.Exp(-10f * Time.deltaTime));
    }

    float HandExpression(Transform hand, float side, float phaseOffset, float moving, bool speaking, ref Vector3 wrist)
    {
        float wave = Mathf.Sin(Time.time * (speaking ? 3.4f : 2f) + phaseOffset * Mathf.PI * 2f);
        float grip = speaking ? 0.35f + wave * 0.22f : moving > 0.05f ? 0.6f + wave * 0.08f : 0.22f + wave * 0.04f;
        float amplitude = speaking && moving < 0.05f ? 1f : 0.25f;
        if (State == Activity.Climbing)
        {
            float phase = Mathf.Repeat((sidewaysClimbing ? Vector3.Distance(transform.position, traversalStart) / 0.6f
                : Mathf.Max(0f, transform.position.y - traversalStart.y) / 0.8f) + phaseOffset, 1f);
            bool holding = phase < 0.6f || (!sidewaysClimbing && traversalTime >= climbLiftDuration);
            grip = holding ? 0.88f : 0.18f;
            amplitude = holding ? 0f : 0.5f;
        }
        Vector3 desired = new Vector3(wave * 16f, wave * side * 8f, Mathf.Cos(Time.time * 2f + phaseOffset * 6.28f) * side * 12f) * amplitude;
        wrist = Vector3.Lerp(wrist, desired, 1f - Mathf.Exp(-9f * Time.deltaTime));
        // Offsets stay small relative to the locomotion/climbing wrist orientation.
        wrist = new Vector3(Mathf.Clamp(wrist.x, -20f, 20f), Mathf.Clamp(wrist.y, -12f, 12f), Mathf.Clamp(wrist.z, -15f, 15f));
        hand.rotation *= Quaternion.Euler(wrist);
        return Mathf.Clamp01(grip);
    }

    void PoseClimbingHand(Transform hand, float side, float phaseOffset)
    {
        if (sidewaysClimbing) { PoseSidewaysHand(hand, side, phaseOffset); return; }
        // Drive the cycle from ascent distance, so a blocked body cannot keep
        // climbing with its arms. During a pull the hand stays at one world
        // height while the shoulder rises; the opposite arm reaches next.
        const float rungSpacing = 0.8f;
        float ascent = Mathf.Max(0f, transform.position.y - traversalStart.y);
        float cycle = ascent / rungSpacing + phaseOffset;
        float phase = cycle - Mathf.Floor(cycle);
        float reach = Mathf.Clamp01((phase - 0.6f) / 0.4f);
        float rise = Mathf.SmoothStep(0f, 1f, reach);
        float holdHeight = traversalStart.y + eyeHeight + 0.22f
            + (Mathf.Floor(cycle) - phaseOffset + rise) * rungSpacing;
        Vector3 facing = Vector3.ProjectOnPlane(ledge - traversalStart, Vector3.up).normalized;
        if (facing.sqrMagnitude < 0.001f) facing = transform.forward;
        Vector3 across = Vector3.Cross(Vector3.up, facing);
        Vector3 hold = ledge + across * (side * 0.2f);
        hold.y = Mathf.Min(ledge.y + 0.04f, holdHeight);
        Vector3 probe = new Vector3(transform.position.x, hold.y, transform.position.z) + across * (side * 0.2f);
        if (Physics.Raycast(probe, facing, out RaycastHit contact, 0.95f, environment, QueryTriggerInteraction.Ignore)
            && Mathf.Abs(contact.normal.y) < 0.7f)
            hold = contact.point + contact.normal * 0.04f;
        else hold -= facing * 0.04f;
        // Lift away from the wall during recovery instead of sliding through it.
        // Both hands settle on the rim for the final pull onto the roof.
        float mantle = Mathf.Clamp01((traversalTime - climbLiftDuration) / 0.5f);
        float arc = Mathf.Sin(reach * Mathf.PI) * (1f - mantle);
        hold += -facing * (arc * 0.12f) + across * (side * arc * 0.04f);
        Vector3 rim = ledge + across * (side * 0.2f) - facing * 0.04f + Vector3.up * 0.04f;
        hand.position = Vector3.MoveTowards(Head.position, Vector3.Lerp(hold, rim, mantle), 0.9f);
        hand.rotation = Quaternion.LookRotation(facing, Vector3.up);
    }

    public void Follow(int actor)
    {
        PhotonPlayer requested = PhotonPlayer.ForActor(actor);
        if (!HasAuthority || IsDead || requested == null || !requested.isActiveAndEnabled || requested.IsDead) return;
        EndWander();
        Mode = CompanionMode.Following;
        WaitingForFarewell = false;
        speechAction = SpeechAction.None;
        waitUntil = 0;
        visitorActor = 0;
        visitorGreetingPending = false;
        homeWalking = false;
        followingPaused = false;
        if (TargetActor != actor)
        {
            trail.Clear();
            greeted = false;
        }
        TargetActor = actor;
        if (State != Activity.Climbing && State != Activity.Jumping && State != Activity.Retreating) State = Activity.Following;
        nextPath = 0;
        DuckzAnalytics.DuckFollow();
    }

    public void ReceivePose(in BodyPose pose)
    {
        if (HasAuthority) return;
        Head.SetPositionAndRotation(pose.headPos, pose.headRot);
        LeftHand.SetPositionAndRotation(pose.lHandPos, pose.lHandRot);
        RightHand.SetPositionAndRotation(pose.rHandPos, pose.rHandRot);
    }

    bool followingPaused;

    public void WaitHere()
    {
        if (!HasAuthority || IsDead) return;
        CancelNavigation();
        stairSearchRunning = false;
        EndWander();
        if (Mode == CompanionMode.GreetingVisitor)
        {
            Mode = CompanionMode.HomeWandering;
            visitorActor = 0;
            visitorGreetingPending = false;
        }
        followingPaused = true;
        waitUntil = PhotonNetwork.Time + CommandDuration;
        // Finish an in-progress climb/jump or hit retreat before holding position.
        if (State != Activity.Retreating && State != Activity.Climbing && State != Activity.Jumping) State = Activity.Waiting;
    }

    public bool TryKill()
    {
        if (!HasAuthority || IsDead) return false;
        CancelNavigation();
        stairSearchRunning = false;
        CaptureKillerDeathAppearance();
        CancelKillerAttack();
        if (speechAction == SpeechAction.Quiet) Dialogue.BeginQuietPeriod();
        speechAction = SpeechAction.None;
        WaitingForFarewell = false;
        EndWander();
        State = Activity.Dead;
        respawnAt = PhotonNetwork.Time + 5;
        ReturningAfterDeath = true;
        LastEvent = "killed by a zombie";
        motor.enabled = false;
        Dialogue.CancelSpeech();
        proceduralGait?.ResetPose();
        avatar.EmitCompanionDeathBlood();
        avatar.ApplyCompanionDeath(true);
        return true;
    }

    void Respawn() => ResetAtCompanionSpawn(false);

    void ClearRespawnActions()
    {
        failedClimbUntil = 0;
        ClearFollowProgress();
        fenceAttemptUntil = 0;
        fenceDetourCommitted = false;
        fenceReleaseUntil = failedFenceUntil = 0;
        CancelKillerAttack();
        killerActor = 0;
        killerDeadline = killerSwingAt = 0;
        pendingKillerReunions.Clear();
        Dialogue.ResetForRespawn();
        EndWander();
        Mode = CompanionMode.HomeWandering;
        TargetActor = 0;
        ReturningAfterDeath = false;
        respawnAt = 0;
        followingPaused = false;
        waitUntil = 0;
        speechAction = SpeechAction.None;
        speechActor = 0;
        WaitingForFarewell = false;
        farewellDeadline = 0;
        visitorActor = 0;
        visitorArrived = visitorGreetingPending = false;
        visitorDeadline = visitorLastProgress = visitorLastSpeech = 0;
        greetedVisitors = System.Array.Empty<int>();
        visitorRetryTimes = System.Array.Empty<double>();
        greeted = false;
        conversationLookActor = pendingConversationLookActor = 0;
        conversationLookUntil = 0;
        homeWalking = false;
        ResetNavigationAfterAuthorityChange();
    }

    bool ResetAtCompanionSpawn(bool recovery)
    {
        EnsureHome();
        ClearRespawnActions();
        respawnRevision++;
        motor.enabled = false;
        transform.SetPositionAndRotation(homePosition, homeRotation);
        nextHomeWander = PhotonNetwork.Time + 5;
        lastPosition = transform.position;
        reactedAirborne = false;
        airbornePeak = transform.position.y;
        proceduralGait?.ResetPose();
        State = Activity.Waiting;
        KillerEyesAtDeath = false;
        UpdateKillerEyes(false);
        vertical = 0;
        motor.enabled = true;
        avatar.ApplyCompanionDeath(false);
        LastEvent = recovery ? "recovered at companion spawn after 20 seconds stuck" : "respawned";
        retreatUntil = nextHitReaction = 0;
        dropUntil = localDetourUntil = fallReleaseUntil = nextFallRelease = 0;
        ledgeAttemptSince = -1f;
        avoidedLedgeUntil = 0;
        descendedWithoutLanding = false;
        stalledFallSince = -1;
        retreatActor = 0;
        traversalTime = traversalDuration = 0;
        ledgeSurface = null;
        corners = System.Array.Empty<Vector3>();
        trail.Clear();
        obstacleRoute = System.Array.Empty<Vector3>();
        progressHistory.Clear();
        nextObstacleSearch = 0;
        stuck = 0;
        nextTraversal = 0;
        nextPath = 0;
        Velocity = Vector3.zero;
        groundSupported = TryGroundSupport(transform.position, out _);
        ResetRecovery();
        return true;
    }

    readonly PhotonValueCache serializationValues = new PhotonValueCache();
    static readonly Unity.Profiling.ProfilerMarker SerializationMarker = new Unity.Profiling.ProfilerMarker("Duck.Serialize");
    public void Serialize(PhotonStream stream)
    {
        using var sample = SerializationMarker.Auto();
        serializationValues.Begin();
        if (stream.IsWriting)
        {
            serializationValues.Send(stream, respawnRevision);
            serializationValues.Send(stream, transform.position);
            serializationValues.Send(stream, transform.rotation);
            serializationValues.Send(stream, (byte)State);
            serializationValues.Send(stream, TargetActor);
            serializationValues.Send(stream, respawnAt);
            serializationValues.Send(stream, ReturningAfterDeath);
            serializationValues.Send(stream, greeted);
            serializationValues.Send(stream, retreatUntil);
            serializationValues.Send(stream, retreatActor);
            serializationValues.Send(stream, retreatLookPosition);
            serializationValues.Send(stream, followingPaused);
            serializationValues.Send(stream, recoveryAnchor);
            serializationValues.Send(stream, lastProgressAt);
            serializationValues.Send(stream, nextWanderAt);
            serializationValues.Send(stream, wanderUntil);
            serializationValues.Send(stream, wanderDestination);
            serializationValues.Send(stream, exploring);
            serializationValues.Send(stream, returningFromExplore);
            SerializeHome(stream);
        }
        else
        {
            int revision = (int)stream.ReceiveNext();
            if (revision != respawnRevision)
            {
                // Clear local speech queues as well as replicated movement state.
                // A new authority inherits the reset, not its old local pending work.
                ClearRespawnActions();
                respawnRevision = revision;
            }
            transform.SetPositionAndRotation((Vector3)stream.ReceiveNext(), (Quaternion)stream.ReceiveNext());
            State = (Activity)(byte)stream.ReceiveNext();
            TargetActor = (int)stream.ReceiveNext();
            respawnAt = (double)stream.ReceiveNext();
            ReturningAfterDeath = (bool)stream.ReceiveNext();
            greeted = (bool)stream.ReceiveNext();
            retreatUntil = (double)stream.ReceiveNext();
            retreatActor = (int)stream.ReceiveNext();
            retreatLookPosition = (Vector3)stream.ReceiveNext();
            followingPaused = (bool)stream.ReceiveNext();
            recoveryAnchor = (Vector3)stream.ReceiveNext();
            lastProgressAt = (double)stream.ReceiveNext();
            nextWanderAt = (double)stream.ReceiveNext();
            wanderUntil = (double)stream.ReceiveNext();
            wanderDestination = (Vector3)stream.ReceiveNext();
            exploring = (bool)stream.ReceiveNext();
            returningFromExplore = (bool)stream.ReceiveNext();
            SerializeHome(stream);
            if (IsDead) Dialogue.CancelSpeech();
        }
        SelfMute.Serialize(stream);
        Dialogue.Serialize(stream);
        SerializeKiller(stream);
    }

    void SerializeHome(PhotonStream stream)
    {
        if (stream.IsWriting)
        {
            serializationValues.Send(stream, failedClimbUntil);
            serializationValues.Send(stream, failedClimbOrigin);
            serializationValues.Send(stream, failedClimbDirection);
            serializationValues.Send(stream, followNoProgressSeconds);
            serializationValues.Send(stream, followProgressActor);
            serializationValues.Send(stream, followProgressAnchor);
            serializationValues.Send(stream, followProgressPositions.Count);
            foreach (Vector3 position in followProgressPositions) serializationValues.Send(stream, position);
            serializationValues.Send(stream, (byte)Mode);
            serializationValues.Send(stream, homeInitialized);
            serializationValues.Send(stream, homeFloorResolved);
            serializationValues.Send(stream, homeFloorY);
            serializationValues.Send(stream, homePosition);
            serializationValues.Send(stream, homeRotation);
            serializationValues.Send(stream, homeWalking);
            serializationValues.Send(stream, homeDestination);
            serializationValues.Send(stream, nextHomeWander);
            serializationValues.Send(stream, WaitingForFarewell);
            serializationValues.Send(stream, farewellDeadline);
            serializationValues.Send(stream, (byte)speechAction);
            serializationValues.Send(stream, speechActor);
            serializationValues.Send(stream, waitUntil);
            serializationValues.Send(stream, visitorActor);
            serializationValues.Send(stream, visitorArrived);
            serializationValues.Send(stream, visitorGreetingPending);
            serializationValues.Send(stream, visitorDeadline);
            serializationValues.Send(stream, visitorLastProgress);
            serializationValues.Send(stream, visitorLastSpeech);
            serializationValues.Send(stream, visitorProgressPosition);
            serializationValues.Send(stream, greetedVisitors);
            serializationValues.Send(stream, visitorRetryTimes);
            serializationValues.Send(stream, conversationLookActor);
            serializationValues.Send(stream, pendingConversationLookActor);
            serializationValues.Send(stream, conversationLookUntil);
            serializationValues.Send(stream, fenceReleaseUntil);
            serializationValues.Send(stream, fenceReleaseDirection);
            serializationValues.Send(stream, fenceReleaseStart);
            serializationValues.Send(stream, failedFenceUntil);
            serializationValues.Send(stream, failedFenceCrossing);
            serializationValues.Send(stream, fenceAttemptUntil);
            serializationValues.Send(stream, fenceAttemptPoint);
            serializationValues.Send(stream, fenceAttemptNormal);
            serializationValues.Send(stream, fenceDetourCommitted);
            serializationValues.Send(stream, fenceDetourTarget);
        }
        else
        {
            failedClimbUntil = (double)stream.ReceiveNext();
            failedClimbOrigin = (Vector3)stream.ReceiveNext();
            failedClimbDirection = (Vector3)stream.ReceiveNext();
            followNoProgressSeconds = (float)stream.ReceiveNext();
            followProgressActor = (int)stream.ReceiveNext();
            followProgressAnchor = (Vector3)stream.ReceiveNext();
            followProgressPositions.Clear();
            int followPositionCount = (int)stream.ReceiveNext();
            for (int i = 0; i < followPositionCount; i++) followProgressPositions.Add((Vector3)stream.ReceiveNext());
            Mode = (CompanionMode)(byte)stream.ReceiveNext();
            homeInitialized = (bool)stream.ReceiveNext();
            homeFloorResolved = (bool)stream.ReceiveNext();
            homeFloorY = (float)stream.ReceiveNext();
            homePosition = (Vector3)stream.ReceiveNext();
            homeRotation = (Quaternion)stream.ReceiveNext();
            homeWalking = (bool)stream.ReceiveNext();
            homeDestination = (Vector3)stream.ReceiveNext();
            nextHomeWander = (double)stream.ReceiveNext();
            WaitingForFarewell = (bool)stream.ReceiveNext();
            farewellDeadline = (double)stream.ReceiveNext();
            speechAction = (SpeechAction)(byte)stream.ReceiveNext();
            speechActor = (int)stream.ReceiveNext();
            waitUntil = (double)stream.ReceiveNext();
            visitorActor = (int)stream.ReceiveNext();
            visitorArrived = (bool)stream.ReceiveNext();
            visitorGreetingPending = (bool)stream.ReceiveNext();
            visitorDeadline = (double)stream.ReceiveNext();
            visitorLastProgress = (double)stream.ReceiveNext();
            visitorLastSpeech = (double)stream.ReceiveNext();
            visitorProgressPosition = (Vector3)stream.ReceiveNext();
            greetedVisitors = (int[])stream.ReceiveNext();
            visitorRetryTimes = (double[])stream.ReceiveNext();
            conversationLookActor = (int)stream.ReceiveNext();
            pendingConversationLookActor = (int)stream.ReceiveNext();
            conversationLookUntil = (double)stream.ReceiveNext();
            fenceReleaseUntil = (double)stream.ReceiveNext();
            fenceReleaseDirection = (Vector3)stream.ReceiveNext();
            fenceReleaseStart = (Vector3)stream.ReceiveNext();
            failedFenceUntil = (double)stream.ReceiveNext();
            failedFenceCrossing = (Vector3)stream.ReceiveNext();
            fenceAttemptUntil = (double)stream.ReceiveNext();
            fenceAttemptPoint = (Vector3)stream.ReceiveNext();
            fenceAttemptNormal = (Vector3)stream.ReceiveNext();
            fenceDetourCommitted = (bool)stream.ReceiveNext();
            fenceDetourTarget = (Vector3)stream.ReceiveNext();
        }
    }
}
