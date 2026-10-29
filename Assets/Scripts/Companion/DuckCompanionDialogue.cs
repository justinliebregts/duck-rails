using System;
using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using UnityEngine;

/// <summary>Local authored dialogue. The controller selects one line; peers load baked audio locally.
/// No backend, paid API, or generative language model is used.</summary>
public sealed class DuckCompanionDialogue : MonoBehaviourPunCallbacks
{
    static readonly Unity.Profiling.ProfilerMarker UtteranceMarker = new Unity.Profiling.ProfilerMarker("DuckDialogue.ReceiveUtterance");
    static readonly Unity.Profiling.ProfilerMarker AcceptanceMarker = new Unity.Profiling.ProfilerMarker("DuckDialogue.AcceptSpeech");
    static readonly Unity.Profiling.ProfilerMarker IntentMarker = new Unity.Profiling.ProfilerMarker("DuckDialogue.Intent");
    public const float ConversationRange = 10f;
    public float VoicePitch => duck != null && duck.voiceSettings != null
        ? duck.voiceSettings.PlaybackPitch : DuckCompanionVoiceSettings.DefaultPitch;
    public const float BuddyConversationRange = 10f;
    [Serializable] public class Bank { public Group[] groups; }
    [Serializable] public class Group { public string intent; public string[] lines; }
    public string LastHeard { get; private set; } = "";
    public string LastReply { get; private set; } = "";
    public string Status { get; private set; } = "Listening nearby";
    public bool Busy => preparing || (source != null && source.isPlaying);
    public bool IsSpeaking => source != null && source.isPlaying;
    public int SpeechSequence => sequence;
    public int SpeakKillerLine(string topic)
    {
        Speak(Choose(topic), true);
        return sequence;
    }
    public bool ChatterPaused { get; private set; }
    double quietUntil;
    public void BeginQuietPeriod()
    {
        if (!duck.HasAuthority) return;
        ChatterPaused = true;
        quietUntil = PhotonNetwork.Time + DuckCompanion.QuietDuration;
    }

    void TickQuietPeriod()
    {
        if (duck.HasAuthority && quietUntil > 0 && PhotonNetwork.Time >= quietUntil)
        {
            quietUntil = 0;
            ChatterPaused = false;
        }
    }
    [SerializeField, Range(0f, 1f)] float creepyDialogueChance = 0.4f;
    [SerializeField, Range(0f, 1f)] float commandRefusalChance = 0.4f;
    [SerializeField, Range(0f, 1f)] float unknownReplyChance = 0.25f;
    [SerializeField, Min(0f)] float unknownReplyCooldown = 8f;
    double nextUnknownReply;
    readonly Dictionary<string, string[]> lines = new Dictionary<string, string[]>();
    readonly Dictionary<string, int> previous = new Dictionary<string, int>();
    readonly Dictionary<string, Queue<int>> remaining = new Dictionary<string, Queue<int>>();
    // General tips and memories never assert that Duck can see an item or landmark.
    static readonly string[] SmallTalkTopics = { "gold", "weapons", "bags", "survival", "helicopter",
        "nail_bat", "fry_pan", "night_vision", "flashlight", "helmet", "walkie_talkie",
        "rambro_knife", "ninja_knife", "city", "jokes", "military_sounds", "factory_banging", "crows", "thunder" };
    static readonly string[] ChildhoodTopics = { "town", "school", "river", "woods", "factory", "store",
        "cinema", "apartments", "warehouse", "subway", "caves", "sheriff", "rambro_memories",
        "gas_station", "church", "barn", "helicopter_memories", "diner", "water_tower",
        "bus_memories", "truck_memories", "helipad_memories" };
    readonly Queue<int> childhoodRemaining = new Queue<int>();
    int previousChildhood = -1;
    bool childhoodNext = true;
    readonly Queue<int> smallTalkRemaining = new Queue<int>();
    int previousSmallTalk = -1;
    readonly Dictionary<int, double> conversationUntil = new Dictionary<int, double>();
    readonly Dictionary<int, double> rateLimit = new Dictionary<int, double>();
    readonly Dictionary<int, double> greetingReplyTimes = new Dictionary<int, double>();
    int pendingGreetingActor;
    double pendingGreetingDeadline;
    readonly HashSet<int> ready = new HashSet<int>();
    readonly HashSet<int> expected = new HashSet<int>();
    readonly List<int> removed = new List<int>();
    DuckCompanion duck;
    AudioSource source;
    AudioClip prepared;
    int sequence;
    bool preparing;
    double prepareDeadline;
    double scheduledStart = -1;
    float scheduledPitch = DuckCompanionVoiceSettings.DefaultPitch;
    double cancelAt;
    float nextReactive;
    float nextAmbientAfterSpeech;
    float nextReminder;
    public const float ThreatRange = 25f;
    double nextThreatCheck, nextThreatWarning;
    double nextUrgentWarning, threatClearSince = double.NegativeInfinity;
    bool warnedEncounter, warnedUrgentEncounter;
    Dictionary<Game.AI.Enemy, Vector3> threatPositions = new Dictionary<Game.AI.Enemy, Vector3>();
    Dictionary<Game.AI.Enemy, Vector3> sampledThreatPositions = new Dictionary<Game.AI.Enemy, Vector3>();
    float lastConversation = float.NegativeInfinity;
    float conversationPause = 35f;
    int announcedHelicopter = -1;
    string lastTopic = "gold";
    string deferredTopic;
    bool childhoodAfterIntroduction;
    [SerializeField, Range(0f, 1f)] float nearbyMemoryChance = 0.4f;
    readonly Dictionary<string, double> nearbyMemoryCooldowns = new Dictionary<string, double>();
    readonly List<KeyValuePair<Renderer, string>> nearbyLandmarks = new List<KeyValuePair<Renderer, string>>();
    double nextLandmarkScan, nextNearbyMemory;
    [SerializeField, Range(0f, 1f)] float ambientReactionChance = 0.1f;
    sealed class AmbientObservation
    {
        public string topic;
        public bool audible;
        public float playbackTime;
        public double nextAttempt;
    }
    readonly Dictionary<AudioSource, AmbientObservation> ambientSources = new Dictionary<AudioSource, AmbientObservation>();
    readonly Dictionary<string, double> ambientTopicCooldowns = new Dictionary<string, double>();
    double nextAmbientScan, nextAmbientPoll, nextAmbientReaction;


    int deferredActor;

    void Awake()
    {
        duck = GetComponent<DuckCompanion>();
        nextReminder = Time.time + UnityEngine.Random.Range(180f, 300f);
        TextAsset asset = Resources.Load<TextAsset>("DuckDialogue");
        Bank bank = asset != null ? JsonUtility.FromJson<Bank>(asset.text) : null;
        if (bank?.groups != null)
            foreach (Group group in bank.groups)
                if (group.lines != null && group.lines.Length > 0) lines[group.intent] = group.lines;
    }

    void Start()
    {
        source = GetComponent<PhotonPlayer>().VoiceSource;
    }

    public static bool AcceptsSpeech(string text, bool nearbyBuddy, bool conversationOpen)
    {
        string intent = null;
        return AcceptsSpeech(text, nearbyBuddy, conversationOpen, ref intent);
    }

    static bool AcceptsSpeech(string text, bool nearbyBuddy, bool conversationOpen, ref string intent)
    {
        using var sample = AcceptanceMarker.Auto();
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (nearbyBuddy || conversationOpen || DuckIntentMatcher.HasWakeWord(text)) return true;
        intent ??= Intent(text);
        return IsDirectRequest(intent) || IsSocialTrigger(intent);
    }

    static bool IsDirectRequest(string intent) => intent == "follow" || intent == "wait"
        || intent == "quiet" || intent == "dismiss" || intent == "acknowledge" || intent == "helicopter_directions";
    static bool IsSocialTrigger(string intent) => intent == "greeting" || intent == "goodbye" || intent == "laughter" || intent == "identity" || intent == "friendship" || intent == "favorite_movies" || intent == "favorite_music" || intent == "favorite_drinks" || intent == "appearance" || intent == "appearance_jealous";

    public bool IsNearbyBuddy(PhotonPlayer speaker)
    {
        return speaker != null && !speaker.IsDead && speaker.photonView.OwnerActorNr == duck.TargetActor
            && speaker.TryGetHeadPosition(out Vector3 head)
            && (head - duck.Head.position).sqrMagnitude <= BuddyConversationRange * BuddyConversationRange;
    }

    public void SubmitLocal(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || PhotonVoiceChat.IsLocallyMuted || duck.IsDead) return;
        PhotonPlayer human = PhotonPlayer.Local;
        if (human == null || human.IsDead || !human.TryGetHeadPosition(out Vector3 position)
            || Vector3.Distance(position, duck.Head.position) > ConversationRange) return;
        LastHeard = text;
        photonView.RPC(nameof(ReceiveUtterance), RpcTarget.MasterClient, text.Length > 240 ? text.Substring(0, 240) : text);
    }

    [PunRPC]
    void ReceiveUtterance(string text, PhotonMessageInfo info)
    {
        using var sample = UtteranceMarker.Auto();
        if (!duck.HasAuthority || duck.IsDead || info.Sender == null || string.IsNullOrWhiteSpace(text) || text.Length > 240) return;
        int actor = info.Sender.ActorNumber;
        PhotonPlayer speaker = PhotonPlayer.ForActor(actor);
        if (speaker == null || speaker.IsDead || !speaker.TryGetHeadPosition(out Vector3 head)
            || Vector3.Distance(head, duck.Head.position) > ConversationRange) return;
        if (rateLimit.TryGetValue(actor, out double allowed) && PhotonNetwork.Time < allowed) return;
        // Direct commands and microphone checks work without requiring his name.
        // Other overheard buddy chatter must not wake Duck.
        string intent = null;
        if (ChatterPaused && !AcceptsSpeech(text, false, false, ref intent)) return;
        if (!duck.CanHearVisitor && !AcceptsSpeech(text, IsNearbyBuddy(speaker),
            conversationUntil.TryGetValue(actor, out double until) && until >= PhotonNetwork.Time, ref intent)) return;
        rateLimit[actor] = PhotonNetwork.Time + 0.75;
        conversationUntil[actor] = PhotonNetwork.Time + 60;
        LastHeard = text;
        intent ??= Intent(text);
        if (intent == "greeting")
        {
            // Keep the visitor approach, but answer explicit hellos independently
            // of its one-time greeting and sixty-second visit retry timer.
            if (!duck.CommandResponsePending && duck.CanHearVisitor)
                duck.HearVisitor(actor, false);
            QueueGreeting(actor);
            return;
        }
        if (duck.CommandResponsePending && !(intent == "follow" && duck.WaitingForFarewell)) return;
        duck.AttendConversation(actor);
        bool requestedRepeat = intent == "repeat";
        if (requestedRepeat) intent = lastTopic;
        if (duck.CanHearVisitor && !IsDirectRequest(intent))
        {
            bool greetingOnly = intent == "unknown" || intent == "greeting";
            duck.HearVisitor(actor, greetingOnly);
            // Casual speech starts a visit, not repeated unknown/hello replies.
            // Questions keep their normal contextual answer and suppress hello.
            if (greetingOnly) return;
        }
        // Only unrecognized chatter is sampled. Commands and hearing checks bypass this gate.
        if (intent == "unknown" && !requestedRepeat && !ShouldReplyToUnknown()) return;
        childhoodAfterIntroduction = intent == "identity" && UnityEngine.Random.value < 0.5f;
        lastConversation = Time.time;
        conversationPause = intent == "unknown" ? 5f : 35f;
        // Event-only reactions cannot be replayed later as a claim about the world.
        if (intent == "helicopter_arrival") intent = "helicopter";
        if (intent == "airborne" || intent == "hurt" || intent == "wander") intent = "survival";
        if (intent == "enemy_warning") intent = "danger";
        if (intent == "danger" && !ThreatNearby()) intent = "survival";
        // Roll once on the authority, before any command changes the follow or quiet state.
        // A refusal preserves his current activity; peers receive that state and the chosen line.
        if (ShouldRefuseCommand(intent))
        {
            deferredTopic = null;
            lastTopic = "creepy";
            nextReactive = Time.time + 35f;
            Speak(Choose("refuse_" + intent), true);
            duck.QueueCommandResponse(intent, actor, true);
            return;
        }
        bool returningToFriend = intent == "follow" && (ChatterPaused || duck.FollowingPaused);
        ChatterPaused = intent == "quiet" || intent == "dismiss" || (ChatterPaused && intent == "acknowledge");
        if (intent == "quiet" || !ChatterPaused) quietUntil = 0;
        deferredTopic = null;
        if (ChatterPaused) conversationUntil.Clear();
        if (!ChatterPaused && ImmediateThreatNearby() && !warnedUrgentEncounter && PhotonNetwork.Time >= nextUrgentWarning
            && intent != "unknown" && intent != "danger" && !IsDirectRequest(intent) && !IsSocialTrigger(intent))
        {
            deferredTopic = intent;
            deferredActor = actor;
            intent = "danger";
            MarkThreatWarning(true);
        }
        lastTopic = intent;
        nextReactive = Time.time + conversationPause;
        DuckzAnalytics.DuckUtterance(DuckzAnalytics.UtteranceBucket(intent));
        string replyTopic = returningToFriend ? "follow_return" : intent;
        if (intent == "goodbye" || intent == "quiet" || intent == "dismiss")
            replyTopic = "creepy_" + intent;
        else if (intent == "mood" || intent == "jokes"
            || intent == "thanks" || intent == "kindness")
            replyTopic = SelectPersonalityTopic(replyTopic);
        Speak(Choose(replyTopic == "unknown" ? "unknown_short" : replyTopic), true);
        if (intent == "follow" || intent == "dismiss" || intent == "wait" || intent == "quiet")
            duck.QueueCommandResponse(intent, actor, false);
    }

    bool ShouldReplyToUnknown()
    {
        if (Busy || duck.KillerSequenceActive || PhotonNetwork.Time < nextUnknownReply
            || UnityEngine.Random.value >= unknownReplyChance) return false;
        nextUnknownReply = PhotonNetwork.Time + unknownReplyCooldown;
        return true;
    }

    bool ShouldRefuseCommand(string intent)
    {
        return (intent == "follow" || intent == "wait" || intent == "quiet" || intent == "dismiss")
            && UnityEngine.Random.value < commandRefusalChance;
    }

    public static string Intent(string text)
    {
        using var sample = IntentMarker.Auto();
        return DuckIntentMatcher.Intent(text);
    }

    public bool ThreatNearby() => ThreatWithin(ThreatRange);

    bool ImmediateThreatNearby() => ThreatWithin(5f);

    bool ThreatWithin(float range)
    {
        // The registered live enemies include all zombie types. Their moving root
        // can differ from the manager/prefab transform. Dormant respawns do not count.
        foreach (var enemy in Game.AI.Enemy.Active)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDormant) continue;
            Vector3 position = enemy.Root != null ? enemy.Root.position : enemy.transform.position;
            if ((position - transform.position).sqrMagnitude <= range * range) return true;
        }
        return false;
    }

    public bool TryReactToUnmute()
    {
        if (!duck.HasAuthority || duck.IsDead || duck.KillerSequenceActive || Busy) return false;
        // A menu-mute reaction is allowed while conversationally quiet, but must
        // not resume idle chatter or change the player's follow/wait command.
        nextReactive = Mathf.Max(nextReactive, Time.time + 10f);
        Speak(Choose("unmuted"), false);
        return true;
    }

    void QueueGreeting(int actor)
    {
        if (greetingReplyTimes.TryGetValue(actor, out double allowed) && PhotonNetwork.Time < allowed) return;
        greetingReplyTimes[actor] = PhotonNetwork.Time + 5;
        // Coalesce repeated greetings, including those from other listeners.
        if (pendingGreetingActor == 0)
        {
            pendingGreetingActor = actor;
            pendingGreetingDeadline = PhotonNetwork.Time + 45;
        }
        TickGreetingReply();
    }

    public void CancelPendingGreeting(int actor)
    {
        if (duck.HasAuthority && pendingGreetingActor == actor) pendingGreetingActor = 0;
    }

    void TickGreetingReply()
    {
        if (!duck.HasAuthority || pendingGreetingActor == 0) return;
        PhotonPlayer speaker = PhotonPlayer.ForActor(pendingGreetingActor);
        if (duck.IsDead || PhotonNetwork.Time >= pendingGreetingDeadline
            || speaker == null || speaker.IsDead || !speaker.isActiveAndEnabled
            || !speaker.TryGetHeadPosition(out Vector3 head)
            || Vector3.Distance(head, duck.Head.position) > ConversationRange)
        {
            pendingGreetingActor = 0;
            return;
        }
        if (Busy || duck.CommandResponsePending || duck.KillerSequenceActive || !duck.ReadyToGreet(speaker)) return;
        int actor = pendingGreetingActor;
        pendingGreetingActor = 0;
        greetingReplyTimes[actor] = PhotonNetwork.Time + 5;
        duck.AttendConversation(actor);
        duck.MarkGreetingReplied(actor);
        lastConversation = Time.time;
        conversationPause = 35f;
        nextReactive = Time.time + conversationPause;
        lastTopic = "greeting";
        Speak(Choose("greeting"), false);
    }

    bool CanReact(string context, bool interrupt)
    {
        if (context == "airborne") return false;
        if (context == "greeting" && pendingGreetingActor != 0) return false;
        // Quiet/dismissal suppress voluntary chatter, not physical pain responses.
        // ReactToPlayerHit already rate-limits these to one line per retreat.
        return duck.HasAuthority && !duck.IsDead && !duck.KillerSequenceActive
            && (!ChatterPaused || context == "hurt")
            && (interrupt || (!Busy && Time.time >= nextReactive));
    }

    public bool React(string context, bool interrupt = false)
    {
        if (!CanReact(context, interrupt)) return false;
        if (context == "tip")
        {
            if (Time.time - lastConversation < conversationPause || duck.State == DuckCompanion.Activity.Waiting) return false;
            if (ImmediateThreatNearby()) return false;
            else if (Time.time >= nextReminder && Time.time - lastConversation >= 90f)
            {
                context = "reminder";
                nextReminder = Time.time + UnityEngine.Random.Range(240f, 420f);
            }
            else if (childhoodAfterIntroduction)
            {
                childhoodAfterIntroduction = false;
                previousChildhood = DrawIndex(childhoodRemaining, ChildhoodTopics.Length, previousChildhood);
                context = ChildhoodTopics[previousChildhood];
            }
            else
            {
                context = ChooseNearbyMemory() ?? SelectPersonalityTopic(ChooseSmallTalkTopic());
            }
        }
        lastTopic = context;
        nextReactive = Time.time + 20f;
        Speak(Choose(context), interrupt);
        return true;
    }

    // Match authored landmark names, not dialogue keywords: a weapon, shelf,
    // or the combined terrain mesh must not masquerade as a nearby building.
    static string LandmarkTopic(string name)
    {
        string key = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), @"[ _\-]+", " ");
        key = System.Text.RegularExpressions.Regex.Replace(key, @"\s*(?:\(clone\)|\(\d+\)|\d+)\s*$", "").Trim();
        switch (key)
        {
            case "school bus": case "schoolbus": return "bus_memories";
            case "school": case "schoolhouse": case "classroom": return "school";
            case "barn": case "farmhouse": return "barn";
            case "church": case "chapel": return "church";
            case "factory": return "factory";
            case "warehouse": return "warehouse";
            case "cinema": case "movie theater": case "movie theatre": return "cinema";
            case "apartment": case "apartments": case "apartment block": return "apartments";
            case "subway": case "subway station": return "subway";
            case "general store": case "grocery store": case "store": return "store";
            case "gas station": return "gas_station";
            case "sheriffs office": case "sheriff's office": case "police station": return "sheriff";
            case "diner": case "restaurant": return "diner";
            case "water tower": return "water_tower";
            case "creek bridge": case "creek water": case "bridge": return "river";
            case "cabin": case "cabins": return "woods";
            case "cave": case "caves": case "mine entrance": return "caves";
            case "helipad": return "helipad_memories";
            case "truck": case "pickup truck": return "truck_memories";
            default: return null;
        }
    }

    string ChooseNearbyMemory()
    {
        if (PhotonNetwork.Time < nextNearbyMemory || UnityEngine.Random.value >= nearbyMemoryChance) return null;
        string nearest = FindNearbyLandmarkTopic(true);
        if (nearest == null) return null;
        nextNearbyMemory = PhotonNetwork.Time + 120;
        nearbyMemoryCooldowns[nearest] = PhotonNetwork.Time + 300;
        // School and bus memories share a single landmark cooldown.
        return nearest == "bus_memories" && UnityEngine.Random.value < 0.5f ? "school" : nearest;
    }

    string FindNearbyLandmarkTopic(bool respectCooldowns)
    {
        var buddy = PhotonPlayer.ForActor(duck.TargetActor);
        if (buddy == null || buddy.IsDead || !buddy.TryGetHeadPosition(out Vector3 buddyPosition)) return null;
        if (PhotonNetwork.Time >= nextLandmarkScan)
        {
            nextLandmarkScan = PhotonNetwork.Time + 60;
            nearbyLandmarks.Clear();
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
            {
                string topic = null;
                for (Transform node = renderer.transform; node != null && topic == null; node = node.parent)
                    topic = LandmarkTopic(node.name);
                if (topic != null) nearbyLandmarks.Add(new KeyValuePair<Renderer, string>(renderer, topic));
            }
        }
        string nearest = null;
        float bestDistance = 15f * 15f;
        foreach (var landmark in nearbyLandmarks)
        {
            var renderer = landmark.Key;
            if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
            if (respectCooldowns && nearbyMemoryCooldowns.TryGetValue(landmark.Value, out double allowed) && PhotonNetwork.Time < allowed) continue;
            Bounds bounds = renderer.bounds;
            if (bounds.size.sqrMagnitude > 120f * 120f) continue;
            if ((bounds.ClosestPoint(buddyPosition) - buddyPosition).sqrMagnitude > 15f * 15f) continue;
            float distance = (bounds.ClosestPoint(duck.transform.position) - duck.transform.position).sqrMagnitude;
            if (distance >= bestDistance) continue;
            bestDistance = distance;
            nearest = landmark.Value;
        }
        return nearest;
    }

    bool HasPriorityConversation()
    {
        // Ambient remarks are lowest priority. A landmark keeps its priority even
        // when its own random roll fails or its repeat cooldown is still active.
        return deferredTopic != null || childhoodAfterIntroduction
            || Time.time - lastConversation < 20f || FindNearbyLandmarkTopic(false) != null;
    }

    string ChooseSmallTalkTopic()
    {
        bool childhood = childhoodNext;
        childhoodNext = !childhoodNext;
        if (childhood)
        {
            previousChildhood = DrawIndex(childhoodRemaining, ChildhoodTopics.Length, previousChildhood);
            return ChildhoodTopics[previousChildhood];
        }
        previousSmallTalk = DrawIndex(smallTalkRemaining, SmallTalkTopics.Length, previousSmallTalk);
        return SmallTalkTopics[previousSmallTalk];
    }

    string SelectPersonalityTopic(string ordinaryTopic)
    {
        // Only the authority chooses the line; the existing speech RPC gives
        // every listener the same personality slip and local synthesized voice.
        return UnityEngine.Random.value < creepyDialogueChance ? "creepy" : ordinaryTopic;
    }

    static string AmbientTopic(string sourceName)
    {
        string name = System.Text.RegularExpressions.Regex.Replace(sourceName.ToLowerInvariant(), @"[_\-]+", " ");
        // Military must precede City for the combined City_Military names.
        if (DuckIntentMatcher.ContainsWholePhrase(name, "military")) return "military_sounds";
        if (DuckIntentMatcher.ContainsWholePhrase(name, "industrial")) return "factory_banging";
        if (DuckIntentMatcher.ContainsWholePhrase(name, "forest")) return "crows";
        if (DuckIntentMatcher.ContainsWholePhrase(name, "thunder")) return "thunder";
        if (DuckIntentMatcher.ContainsWholePhrase(name, "city")) return "city";
        if (DuckIntentMatcher.ContainsWholePhrase(name, "town")) return "town";
        return null;
    }

    static bool AmbientAudibleAt(AudioSource ambient, Vector3 position)
    {
        if (ambient == null || !ambient.isActiveAndEnabled || !ambient.isPlaying || ambient.mute
            || ambient.clip == null || (AudioListener.pause && !ambient.ignoreListenerPause)) return false;
        float gain = 1f;
        if (ambient.spatialBlend > 0f)
        {
            float distance = Vector3.Distance(ambient.transform.position, position);
            float minimum = Mathf.Max(0.01f, ambient.minDistance);
            float maximum = Mathf.Max(minimum + 0.01f, ambient.maxDistance);
            if (distance >= maximum) gain = 0f;
            else if (distance > minimum)
            {
                if (ambient.rolloffMode == AudioRolloffMode.Linear)
                    gain = 1f - (distance - minimum) / (maximum - minimum);
                else if (ambient.rolloffMode == AudioRolloffMode.Custom)
                    gain = Mathf.Clamp01(ambient.GetCustomCurve(AudioSourceCurveType.CustomRolloff).Evaluate(distance / maximum));
                else gain = minimum / distance;
            }
            gain = Mathf.Lerp(1f, gain, ambient.spatialBlend);
        }
        return ambient.volume * gain * AudioListener.volume >= 0.02f;
    }

    readonly HashSet<AudioSource> ambientPresent = new HashSet<AudioSource>();
    readonly List<AudioSource> ambientRemoved = new List<AudioSource>();

    void RefreshAmbientSources()
    {
        var present = ambientPresent; present.Clear();
        foreach (var ambient in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            string topic = AmbientTopic(ambient.gameObject.name);
            if (topic == null || ambient == source) continue;
            present.Add(ambient);
            if (!ambientSources.TryGetValue(ambient, out var observation))
                ambientSources[ambient] = new AmbientObservation { topic = topic };
            else observation.topic = topic;
    }
    ambientRemoved.Clear();
    foreach (var old in ambientSources.Keys)
        if (old == null || !present.Contains(old)) ambientRemoved.Add(old);
    foreach (var old in ambientRemoved) ambientSources.Remove(old);
    ambientRemoved.Clear();
    present.Clear();
    }

    void CheckAmbientSounds()
    {
        if (!duck.HasAuthority || PhotonNetwork.Time < nextAmbientPoll) return;
        nextAmbientPoll = PhotonNetwork.Time + 0.5;
        if (PhotonNetwork.Time >= nextAmbientScan)
        {
            nextAmbientScan = PhotonNetwork.Time + 30;
            RefreshAmbientSources();
        }
        var buddy = PhotonPlayer.ForActor(duck.TargetActor);
        bool hasBuddy = buddy != null && !buddy.IsDead && buddy.TryGetHeadPosition(out _);
        Vector3 buddyPosition = Vector3.zero;
        if (hasBuddy) buddy.TryGetHeadPosition(out buddyPosition);
        foreach (var entry in ambientSources)
        {
            AudioSource ambient = entry.Key;
            AmbientObservation observation = entry.Value;
            bool audible = hasBuddy && AmbientAudibleAt(ambient, duck.Head.position) && AmbientAudibleAt(ambient, buddyPosition);
            float playbackTime = ambient != null && ambient.clip != null ? ambient.time : 0f;
            bool opportunity = audible && (!observation.audible || playbackTime + 0.1f < observation.playbackTime);
            observation.audible = audible;
            observation.playbackTime = playbackTime;
            if (!opportunity || PhotonNetwork.Time < observation.nextAttempt) continue;
            observation.nextAttempt = PhotonNetwork.Time + 30;
            // Skip busy moments rather than queuing a stale reaction to an old sound.
            if (duck.IsDead || duck.KillerSequenceActive || ChatterPaused || Busy || duck.FollowingPaused
                || duck.State == DuckCompanion.Activity.Retreating || duck.State == DuckCompanion.Activity.Climbing
                || duck.State == DuckCompanion.Activity.Jumping || !hasBuddy
                || (buddyPosition - duck.Head.position).sqrMagnitude > ConversationRange * ConversationRange
                || Time.time < nextAmbientAfterSpeech
                || ImmediateThreatNearby() || PhotonNetwork.Time < nextAmbientReaction) continue;
            if (ambientTopicCooldowns.TryGetValue(observation.topic, out double allowed) && PhotonNetwork.Time < allowed) continue;
            if (HasPriorityConversation() || UnityEngine.Random.value >= ambientReactionChance) continue;
            nextAmbientReaction = PhotonNetwork.Time + 120;
            ambientTopicCooldowns[observation.topic] = PhotonNetwork.Time + 300;
            lastTopic = observation.topic;
            nextReactive = Time.time + 20;
            Speak(Choose(observation.topic), false);
        }
    }

    void CheckNearbyEnemies()
    {
        if (PhotonNetwork.Time < nextThreatCheck) return;
        nextThreatCheck = PhotonNetwork.Time + 0.5;
        bool nearby = false, approaching = false, urgent = false;
        sampledThreatPositions.Clear();
        foreach (var enemy in Game.AI.Enemy.Active)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.IsDormant) continue;
            Vector3 position = enemy.Root != null ? enemy.Root.position : enemy.transform.position;
            Vector3 toDuck = transform.position - position;
            float distanceSq = toDuck.sqrMagnitude;
            if (distanceSq > ThreatRange * ThreatRange) continue;
            nearby = true;
            urgent |= distanceSq <= 25f;
            // Measure the enemy's own movement, not Duck walking toward it.
            approaching |= distanceSq <= 225f && threatPositions.TryGetValue(enemy, out Vector3 previous)
                && Vector3.Dot(position - previous, toDuck.normalized) > 0.05f;
            sampledThreatPositions[enemy] = position;
        }
        var swap = threatPositions;
        threatPositions = sampledThreatPositions;
        sampledThreatPositions = swap;
        if (nearby) threatClearSince = double.NegativeInfinity;
        else
        {
            if (double.IsNegativeInfinity(threatClearSince)) threatClearSince = PhotonNetwork.Time;
            if (PhotonNetwork.Time - threatClearSince >= 20)
                warnedEncounter = warnedUrgentEncounter = false;
        }
        bool urgentWarning = urgent && !warnedUrgentEncounter && PhotonNetwork.Time >= nextUrgentWarning;
        bool ordinaryWarning = approaching && !warnedEncounter && PhotonNetwork.Time >= nextThreatWarning;
        if (ChatterPaused || Busy || Time.time - lastConversation < 4f
            || duck.State == DuckCompanion.Activity.Retreating || (!urgentWarning && !ordinaryWarning)) return;
        PhotonPlayer buddy = PhotonPlayer.ForActor(duck.TargetActor);
        if (buddy == null || buddy.IsDead || !buddy.TryGetHeadPosition(out Vector3 head)
            || Vector3.Distance(head, duck.Head.position) > ConversationRange) return;
        // A first warning need not wait for the casual-chatter cooldown, but it
        // never cuts off a player reply or a hit reaction already being spoken.
        MarkThreatWarning(urgentWarning);
        nextReactive = Time.time + 20f;
        lastTopic = "enemy_warning";
        Speak(Choose(lastTopic), false);
    }

    void MarkThreatWarning(bool urgent)
    {
        warnedEncounter = true;
        nextThreatWarning = PhotonNetwork.Time + UnityEngine.Random.Range(120f, 180f);
        nextUrgentWarning = PhotonNetwork.Time + (urgent ? 60 : 20);
        if (urgent) warnedUrgentEncounter = true;
    }

    public void ReactAirborne()
    {
        // Incidental jump/fall speech is disabled. Keep the compatibility hook;
        // jump SFX and physical traversal are independent of dialogue.
    }

    string Choose(string intent)
    {
        // Resolve at selection time so repeats, deferred answers and idle chatter
        // all use the current host state, then broadcast the same text to peers.
        if (intent == "helicopter" || intent == "helicopter_arrival" || intent == "helicopter_directions")
        {
            var helicopter = HelicopterManager.instance;
            if (helicopter != null && helicopter.IsAvailableForVisit)
                intent = helicopter.IsLanding ? "helicopter_landing"
                    : intent == "helicopter_arrival" ? "helicopter_arrival"
                    : intent == "helicopter_directions" ? "helicopter_directions_here" : "helicopter_here";
            else if (helicopter != null && helicopter.TryGetNextArrivalSeconds(out float seconds))
            {
                string timing = seconds <= 60f ? "helicopter_eta_soon"
                    : seconds <= 120f ? "helicopter_eta_middle" : "helicopter_eta_later";
                return Choose("helicopter_waiting") + " " + Choose(timing);
            }
            else intent = "helicopter_unscheduled";
        }
        if (!lines.TryGetValue(intent, out string[] options))
        {
            intent = "unknown";
            if (!lines.TryGetValue(intent, out options)) return DuckLocalSpeech.Fallback;
        }
        if (!remaining.TryGetValue(intent, out Queue<int> deck)) remaining[intent] = deck = new Queue<int>();
        int last = previous.TryGetValue(intent, out int value) ? value : -1;
        int index = DrawIndex(deck, options.Length, last);
        previous[intent] = index;
        return options[index];
    }

    static int DrawIndex(Queue<int> deck, int count, int last)
    {
        if (deck.Count == 0)
        {
            var order = new int[count];
            for (int i = 0; i < count; i++) order[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                int value = order[i]; order[i] = order[j]; order[j] = value;
            }
            if (count > 1 && order[0] == last)
            {
                int j = UnityEngine.Random.Range(1, count);
                int value = order[0]; order[0] = order[j]; order[j] = value;
            }
            foreach (int index in order) deck.Enqueue(index);
        }
        return deck.Dequeue();
    }

    void Speak(string text, bool interrupt)
    {
        if (!duck.HasAuthority || (!interrupt && Busy)) return;
        // Ambient comments use a shorter pause than ordinary proactive chatter.
        nextAmbientAfterSpeech = Time.time + 20f;
        sequence++;
        expected.Clear();
        foreach (Player player in PhotonNetwork.PlayerList) expected.Add(player.ActorNumber);
        ready.Clear();
        prepareDeadline = PhotonNetwork.Time + 25;
        photonView.RPC(nameof(PrepareLine), RpcTarget.All, sequence, text, DuckLocalSpeech.DefaultVoice);
    }

    [PunRPC]
    void PrepareLine(int id, string text, string voice, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient || duck.IsDead || voice != DuckLocalSpeech.DefaultVoice || string.IsNullOrEmpty(text) || text.Length > 600) return;
        CancelSpeech();
        sequence = id;
        LastReply = text;
        Status = "Loading voice clip";
        preparing = true;
        cancelAt = PhotonNetwork.Time + 35;
        StartCoroutine(Prepare(id, text, voice));
    }

    IEnumerator Prepare(int id, string text, string voice)
    {
        AudioClip clip = null;
        yield return DuckLocalSpeech.Load(text, result => clip = result,
            () => this != null && id == sequence && !duck.IsDead && preparing);
        if (id != sequence || duck.IsDead || !preparing)
        {
            if (clip != null) Destroy(clip);
            yield break;
        }
        prepared = clip;
        if (clip == null) Status = "Voice unavailable (subtitle only)";
        photonView.RPC(nameof(LineReady), RpcTarget.MasterClient, id);
    }

    [PunRPC]
    void LineReady(int id, PhotonMessageInfo info)
    {
        if (duck.HasAuthority && id == sequence && info.Sender != null) ready.Add(info.Sender.ActorNumber);
    }

    [PunRPC]
    void StartLine(int id, double time, PhotonMessageInfo info)
    {
        if (info.Sender != PhotonNetwork.MasterClient || id != sequence || duck.IsDead) return;
        scheduledPitch = VoicePitch;
        scheduledStart = time;
    }

    void Update()
    {
        TickQuietPeriod();
        TickGreetingReply();
        CheckAmbientSounds();
        if (duck.IsDead) return;
        if (duck.HasAuthority && !duck.KillerSequenceActive)
        {
            CheckNearbyEnemies();
            CheckHelicopterArrival();
        }
        if (duck.HasAuthority && preparing && scheduledStart < 0 && prepareDeadline > 0)
        {
            removed.Clear();
            foreach (int actor in expected) if (PhotonNetwork.CurrentRoom.GetPlayer(actor) == null) removed.Add(actor);
            foreach (int actor in removed) expected.Remove(actor);
            if (ready.IsSupersetOf(expected) || PhotonNetwork.Time > prepareDeadline)
            {
                prepareDeadline = 0;
                photonView.RPC(nameof(StartLine), RpcTarget.All, sequence, PhotonNetwork.Time + 0.6);
            }
        }
        if (scheduledStart >= 0 && PhotonNetwork.Time >= scheduledStart)
        {
            // Local prefab snapshot captured at StartLine. Matching builds use
            // the same pitch; edits cannot change an already scheduled line.
            double elapsed = (PhotonNetwork.Time - scheduledStart) * scheduledPitch;
            if (prepared != null && elapsed < prepared.length && source != null)
            {
                source.clip = prepared;
                source.pitch = scheduledPitch;
                source.time = (float)elapsed;
                source.Play();
                Status = "Speaking";
            }
            else { Status = "Listening nearby"; ReleasePrepared(); }
            scheduledStart = -1;
            preparing = false;
        }
        if (preparing && PhotonNetwork.Time > cancelAt) CancelSpeech();
        if (!Busy && Status == "Speaking")
        {
            if (source != null) source.pitch = VoicePitch;
            ReleasePrepared();
            Status = "Listening nearby";
        }
        if (duck.HasAuthority && !duck.KillerSequenceActive && !ChatterPaused && !Busy && deferredTopic != null && Time.time > nextReactive)
        {
            nextReactive = Time.time + 5;
            PhotonPlayer listener = PhotonPlayer.ForActor(deferredActor);
            if (listener == null) deferredTopic = null;
            else if (!ImmediateThreatNearby() && listener.TryGetHeadPosition(out Vector3 head)
                && Vector3.Distance(head, duck.Head.position) <= ConversationRange)
            {
                string topic = deferredTopic;
                deferredTopic = null;
                Speak(DuckLocalSpeech.Prefix + " " + Choose(topic), false);
            }
        }
    }

    public void ResetForRespawn()
    {
        CancelSpeech();
        ChatterPaused = false;
        quietUntil = 0;
        pendingGreetingActor = 0;
        pendingGreetingDeadline = 0;
        deferredTopic = null;
        deferredActor = 0;
        childhoodAfterIntroduction = false;
        conversationUntil.Clear();
        greetingReplyTimes.Clear();
        rateLimit.Clear();
        expected.Clear();
        ready.Clear();
        prepareDeadline = 0;
        lastTopic = "gold";
    }

    public void CancelSpeech()
    {
        // Invalidates in-flight loads; no background synthesis survives cancellation.
        sequence++;
        preparing = false;
        scheduledStart = -1;
        if (source != null) { source.Stop(); source.pitch = VoicePitch; }
        ReleasePrepared();
        Status = "Listening nearby";
    }

    void ReleasePrepared()
    {
        if (source != null && source.clip == prepared) source.clip = null;
        if (prepared != null) Destroy(prepared);
        prepared = null;
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        CancelSpeech();
        threatPositions.Clear();
        sampledThreatPositions.Clear();
        nextThreatCheck = 0;
    }

    readonly PhotonValueCache serializationValues = new PhotonValueCache(16);
    public void Serialize(PhotonStream stream)
    {
        serializationValues.Begin();
        if (stream.IsWriting)
        {
            serializationValues.Send(stream, ChatterPaused);
            serializationValues.Send(stream, quietUntil);
            serializationValues.Send(stream, announcedHelicopter);
            serializationValues.Send(stream, nextThreatWarning);
            serializationValues.Send(stream, nextUrgentWarning);
            serializationValues.Send(stream, threatClearSince);
            serializationValues.Send(stream, warnedEncounter);
            serializationValues.Send(stream, warnedUrgentEncounter);
            serializationValues.Send(stream, pendingGreetingActor);
            serializationValues.Send(stream, pendingGreetingDeadline);
        }
        else
        {
            ChatterPaused = (bool)stream.ReceiveNext();
            quietUntil = (double)stream.ReceiveNext();
            announcedHelicopter = (int)stream.ReceiveNext();
            nextThreatWarning = (double)stream.ReceiveNext();
            nextUrgentWarning = (double)stream.ReceiveNext();
            threatClearSince = (double)stream.ReceiveNext();
            warnedEncounter = (bool)stream.ReceiveNext();
            warnedUrgentEncounter = (bool)stream.ReceiveNext();
            pendingGreetingActor = (int)stream.ReceiveNext();
            pendingGreetingDeadline = (double)stream.ReceiveNext();
        }
    }

    void CheckHelicopterArrival()
    {
        HelicopterManager helicopter = HelicopterManager.instance;
        bool available = helicopter != null && helicopter.IsAvailableForVisit;
        // A queued announcement may become stale while local TTS prepares it.
        if (!available && preparing && lastTopic == "helicopter_arrival") CancelSpeech();
        if (!available) return;
        if (ChatterPaused)
        {
            announcedHelicopter = helicopter.ArrivalSerial;
            return;
        }
        if (announcedHelicopter == helicopter.ArrivalSerial || Busy || Time.time < nextReactive
            || duck.State != DuckCompanion.Activity.Following || ImmediateThreatNearby()) return;
        PhotonPlayer buddy = PhotonPlayer.ForActor(duck.TargetActor);
        if (buddy == null || buddy.IsDead || !buddy.TryGetHeadPosition(out Vector3 head)
            || (head - duck.Head.position).sqrMagnitude > 36f) return;
        announcedHelicopter = helicopter.ArrivalSerial;
        React("helicopter_arrival");
    }
    void OnDestroy() => CancelSpeech();
}
