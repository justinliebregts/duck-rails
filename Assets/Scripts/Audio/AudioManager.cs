using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    [DefaultExecutionOrder(-500)]
    public sealed class AudioManager : MonoBehaviour
    {
        sealed class Voice
        {
            public AudioSource Source;
            public AudioLowPassFilter LowPass;
            public SoundEvent Event;
            public Transform Follow;
            public Transform IgnoreOcclusionRoot;
            public Behaviour Owner;
            public bool HasFollow, HasOwner, Loop, Started, Ambience;
            public ulong Generation;
            public double EndDsp;
            public float LoadDeadline;
            public float FadeRemaining, FadeDuration;
            public float FadeInRemaining, FadeInDuration;
            public float BaseVolume = 1f;
            // Occlusion: 1 is open air, 0 is fully low-passed. Loudness eases separately so a source
            // just behind a wall stays louder than one deep behind geometry.
            public float Occlusion = 1f, OcclusionTarget = 1f;
            public float BelowListenerMix, BelowListenerTarget;
            public float Loudness = 1f, LoudnessTarget = 1f;
            public float NextOcclusionTime;
        }

        static AudioManager _instance;
        static bool quitting, creatingFallback;
        bool isFallback;
        const float OcclusionNearDistance = 0.6f;
        const float ClearCutoffHz = 22000f;
        public const string GameVolumePrefsKey = "GameVolume";
        public const int DefaultGameVolume = 100;
        const string EffectsVolumeParameter = "EffectsVolume";
        const float SilenceDb = -80f;
        static int gameVolumePercent = DefaultGameVolume;
        static bool hasGameVolume;

        [SerializeField] AudioSettings settings;
        [SerializeField] AudioListener listener;
        [Header("Occlusion")]
        [Tooltip("Muffle spatial sounds when Environment geometry blocks the path to the listener.")]
        [SerializeField] bool occlusion = true;
        [SerializeField] LayerMask occlusionMask = 1 << 6;
        [Tooltip("Loudness when the source is Occlusion Far Depth or more past the blocking surface.")]
        [SerializeField, Range(0f, 1f)] float occludedVolume = 0.65f;
        [Tooltip("Loudness when the source is just behind the blocking surface. Still low-passed.")]
        [SerializeField, Range(0f, 1f)] float occludedNearVolume = 0.85f;
        [SerializeField, Min(0f)] float occlusionNearDepth = 5f;
        [SerializeField, Min(0f)] float occlusionFarDepth = 30f;
        [SerializeField, Range(10f, 22000f)] float occludedCutoffHz = 2500f;
        [SerializeField, Min(0.02f)] float occlusionInterval = 0.1f;
        [SerializeField, Min(0.01f)] float occlusionSmoothTime = 0.65f;
        [Tooltip("Ray stops this far short of the source so a body standing on the floor does not muffle itself.")]
        [SerializeField, Min(0f)] float occlusionEndPadding = 0.4f;
        [Tooltip("Offset in metres at the sound end for four extra occlusion rays. Only sampled when the centre ray is blocked. Zero uses a single ray.")]
        [SerializeField, Min(0f)] float occlusionRaySpread = 2f;
        [Tooltip("Volume multiplier for blocked sounds at least Below Listener Minimum Depth below the listener. Replaces near/far occluded volume; open paths are unaffected.")]
        [SerializeField, Range(0f, 1f)] float belowListenerOccludedVolume = 0.20f;
        [Tooltip("Low-pass cutoff for blocked sounds below the listener's minimum depth.")]
        [SerializeField, Range(10f, 22000f)] float belowListenerOccludedCutoffHz = 900f;
        [Tooltip("Minimum vertical distance below the listener, in metres, for the special blocked-sound volume. Applies to any lower sound, including lower building floors.")]
        [SerializeField, Min(0f)] float belowListenerMinimumDepth = 2f;
        Voice[] voices;
        RaycastHit[] occlusionHits = new RaycastHit[16];
        float nextListenerSearch;
        // Last clip per event, for SoundEvent's "avoid immediate repeat".
        readonly Dictionary<SoundEvent, AudioClip> lastClips = new Dictionary<SoundEvent, AudioClip>();
        /// <summary>
        /// The scene's AudioManager (normally a child of GameManager). Scenes without one get a
        /// fallback created on demand. Neither survives a scene load.
        /// </summary>
        public static AudioManager instance
        {
            get
            {
                if (_instance == null && !quitting && Application.isPlaying)
                {
                    _instance = FindAnyObjectByType<AudioManager>();
                    if (_instance == null)
                    {
                        creatingFallback = true;
                        try { new GameObject("Audio Manager (fallback)").AddComponent<AudioManager>(); }
                        finally { creatingFallback = false; }
                    }
                }
                return _instance;
            }
        }
        public AudioSettings Settings => settings;
        public int Capacity => voices == null ? 0 : voices.Length;
        public int ActiveCount { get; private set; }
        public int ActiveAmbienceCount { get; private set; }
        // Always keep at least one slot outside the ambience budget, even in small test pools.
        public int AmbienceCapacity => Mathf.Clamp(settings != null ? settings.ambienceVoiceLimit : 6, 0, Mathf.Max(0, Capacity - 1));
        public int PeakUsage { get; private set; }
        public int SkippedRequests { get; private set; }
        public int StolenSounds { get; private set; }
        public Transform ListenerTransform => listener != null && listener.isActiveAndEnabled ? listener.transform : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            _instance = null;
            quitting = false;
            hasGameVolume = false;
            gameVolumePercent = DefaultGameVolume;
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() { _ = instance; }

        void Awake()
        {
            isFallback = creatingFallback;
            if (_instance != null && _instance != this)
            {
                // A scene-placed manager replaces a fallback that was created before it woke up.
                if (!_instance.isFallback || isFallback) { enabled = false; Destroy(this); return; }
                Destroy(_instance.gameObject);
            }
            _instance = this;
            if (settings == null) settings = Resources.Load<AudioSettings>("GameAudioSettings");
            int size = settings != null ? Mathf.Max(1, settings.poolSize) : 24;
            voices = new Voice[size];
            for (int i = 0; i < size; i++)
            {
                var child = new GameObject($"Pooled Audio {i:00}");
                child.transform.SetParent(transform, false);
                AudioSource source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                AudioLowPassFilter lowPass = child.AddComponent<AudioLowPassFilter>();
                lowPass.enabled = false;
                lowPass.cutoffFrequency = ClearCutoffHz;
                voices[i] = new Voice { Source = source, LowPass = lowPass };
            }
            RefreshListener();
            ApplyGameVolume();
        }

        /// <summary>
        /// Percent of unity gain on the Effects group. 100 is 0 dB, 0 is silence, 200 is +6 dB.
        /// Remembered so a manager created after the prefs load still picks it up.
        /// </summary>
        public static void SetGameVolume(int percent)
        {
            gameVolumePercent = percent;
            hasGameVolume = true;
            if (_instance != null)
                _instance.ApplyGameVolume();
        }

        void ApplyGameVolume()
        {
            if (!hasGameVolume || settings == null || settings.effects == null)
                return;
            AudioMixer mixer = settings.effects.audioMixer;
            if (mixer == null)
                return;
            mixer.SetFloat(EffectsVolumeParameter, GameVolumeDb(gameVolumePercent));
        }

        static float GameVolumeDb(int percent)
        {
            if (percent <= 0)
                return SilenceDb;
            return 20f * Mathf.Log10(percent / 100f);
        }

        public void SetListener(AudioListener value) => listener = value;
        void RefreshListener()
        {
            if (ListenerTransform != null || Time.unscaledTime < nextListenerSearch) return;
            nextListenerSearch = Time.unscaledTime + 1f;
            // Discovery only during listener replacement/spawning, never per sound or per frame.
            foreach (var candidate in FindObjectsByType<AudioListener>(FindObjectsSortMode.None))
                if (candidate.isActiveAndEnabled) { listener = candidate; break; }
        }

        public AudioHandle Play(SoundEvent sound, Vector3 position, float volumeScale = 1f, Transform ignoreOcclusionRoot = null)
            => Request(sound, position, null, null, false, volumeScale, ignoreOcclusionRoot);
        public AudioHandle PlayAttached(SoundEvent sound, Transform follow, Behaviour owner = null, Transform ignoreOcclusionRoot = null)
            => follow != null ? Request(sound, follow.position, follow, owner, false, ignoreOcclusionRoot: ignoreOcclusionRoot) : default;
        public AudioHandle PlayLoop(SoundEvent sound, Transform follow, Behaviour owner = null, Transform ignoreOcclusionRoot = null)
            => follow != null ? Request(sound, follow.position, follow, owner, true, ignoreOcclusionRoot: ignoreOcclusionRoot) : default;

        public AudioHandle PlayAmbience(SoundEvent sound, Transform origin, Behaviour owner, bool loop, float fadeIn = 0.5f)
            => origin != null ? Request(sound, origin.position, loop ? origin : null, owner, loop,
                ambience: true, fadeIn: loop ? fadeIn : 0f) : default;

        AudioHandle Request(SoundEvent sound, Vector3 position, Transform follow, Behaviour owner, bool loop, float volumeScale = 1f, Transform ignoreOcclusionRoot = null, bool ambience = false, float fadeIn = 0f)
        {
            if (!isActiveAndEnabled || voices == null || sound == null || (owner != null && !owner.isActiveAndEnabled))
                return default;
            RefreshListener();
            if (ambience && (AmbienceCapacity == 0 || ListenerTransform == null)) return Skip();
            if (!loop && sound.SpatialBlend > 0f && sound.CullDistance > 0f && ListenerTransform != null
                && (position - listener.transform.position).sqrMagnitude > sound.CullDistance * sound.CullDistance)
                return Skip();

            int count = 0, free = -1, steal = -1, reclaimAmbience = -1;
            float furthest = -1f;
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = voices[i];
                if (voice.Event == null) { if (free < 0) free = i; continue; }
                if (voice.Event == sound) count++;
                if (voice.Ambience)
                {
                    if (reclaimAmbience < 0 || WorseAmbience(voice, voices[reclaimAmbience])) reclaimAmbience = i;
                    continue;
                }
                if (voice.Loop || voice.Event.Priority <= sound.Priority) continue;
                float distance = ListenerTransform != null
                    ? (voice.Source.transform.position - listener.transform.position).sqrMagnitude : 0f;
                if (steal < 0 || voice.Event.Priority > voices[steal].Event.Priority
                    || (voice.Event.Priority == voices[steal].Event.Priority && distance > furthest))
                { steal = i; furthest = distance; }
            }
            bool eventFull = sound.MaxInstances > 0 && count >= sound.MaxInstances;
            if (ambience)
            {
                if (eventFull || ActiveAmbienceCount >= AmbienceCapacity || free < 0)
                {
                    // Fade one less relevant ambience voice, then retry after it releases its slot.
                    // No temporary over-budget crossfade, and ambience never steals gameplay audio.
                    int victim = -1;
                    for (int i = 0; i < voices.Length; i++)
                    {
                        Voice voice = voices[i];
                        if (voice.Event == null || !voice.Ambience || (eventFull && voice.Event != sound)) continue;
                        if (victim < 0 || WorseAmbience(voice, voices[victim])) victim = i;
                    }
                    if (victim >= 0 && MoreRelevantAmbience(sound, position, voices[victim]))
                        Stop(victim, voices[victim].Generation, 0.25f);
                    return Skip();
                }
            }
            else if (eventFull) return Skip();
            int index = free >= 0 ? free : (reclaimAmbience >= 0 ? reclaimAmbience : steal);
            lastClips.TryGetValue(sound, out var lastClip);
            if (index < 0 || !sound.TrySelect(lastClip, out var selection)) return Skip();
            lastClips[sound] = selection.Clip;
            var slot = voices[index];
            if (slot.Event != null) { Release(slot); CountSteal(); }
            slot.Generation++;
            slot.Event = sound;
            slot.Follow = follow;
            slot.IgnoreOcclusionRoot = ignoreOcclusionRoot;
            slot.HasFollow = follow != null;
            slot.Owner = owner;
            slot.HasOwner = owner != null;
            slot.Loop = loop;
            slot.Ambience = ambience;
            slot.Started = false;
            slot.LoadDeadline = Time.realtimeSinceStartup + 10f;
            slot.FadeDuration = slot.FadeRemaining = 0f;
            slot.FadeInDuration = slot.FadeInRemaining = Mathf.Max(0f, fadeIn);
            slot.Source.transform.position = position;
            AudioMixerGroup group = settings == null ? null : settings.effects;
            sound.Configure(slot.Source, selection, group, loop);
            slot.Source.volume *= Mathf.Clamp01(volumeScale);
            slot.BaseVolume = slot.Source.volume;
            // Start at the sampled mix. Smoothing is for changes during playback, not the first buffer.
            float clear = slot.OcclusionTarget;
            float loudness = slot.LoudnessTarget;
            SampleOcclusion(sound, position, slot.IgnoreOcclusionRoot, ref clear, ref loudness, ref slot.BelowListenerTarget);
            slot.BelowListenerMix = slot.BelowListenerTarget;
            slot.Occlusion = slot.OcclusionTarget = clear;
            slot.Loudness = slot.LoudnessTarget = loudness;
            ScheduleOcclusion(slot, index);
            ApplyMix(slot);
            ActiveCount++;
            if (ambience) ActiveAmbienceCount++;
            // Wait for asynchronously loaded clips before Play; the lease remains stoppable while loading.
            if (selection.Clip.loadState == AudioDataLoadState.Unloaded && !selection.Clip.LoadAudioData())
            { Release(slot); return Skip(); }
            if (selection.Clip.loadState == AudioDataLoadState.Failed) { Release(slot); return Skip(); }
            PeakUsage = Mathf.Max(PeakUsage, ActiveCount);
            StartIfLoaded(slot);
            return new AudioHandle(this, index, slot.Generation);
        }

        float RelativeDistance(SoundEvent sound, Vector3 position)
            => ListenerTransform == null ? 0f : Vector3.Distance(position, listener.transform.position) / Mathf.Max(1f, sound.MaxDistance);

        bool WorseAmbience(Voice candidate, Voice current)
            => candidate.Event.Priority > current.Event.Priority
                || (candidate.Event.Priority == current.Event.Priority
                    && RelativeDistance(candidate.Event, candidate.Source.transform.position) > RelativeDistance(current.Event, current.Source.transform.position));

        bool MoreRelevantAmbience(SoundEvent sound, Vector3 position, Voice current)
            => sound.Priority < current.Event.Priority
                || (sound.Priority == current.Event.Priority
                    // A meaningful distance advantage prevents swapping equally relevant loops at boundaries.
                    && RelativeDistance(sound, position) + 0.15f < RelativeDistance(current.Event, current.Source.transform.position));

        void StartIfLoaded(Voice voice)
        {
            if (voice.Started || voice.Source.clip.loadState != AudioDataLoadState.Loaded) return;
            voice.Source.Play();
            voice.EndDsp = UnityEngine.AudioSettings.dspTime + voice.Source.clip.length / voice.Source.pitch;
            voice.Started = true;
        }

        AudioHandle Skip() { CountSkip(); return default; }
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void CountSkip() => SkippedRequests++;
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        void CountSteal() => StolenSounds++;

        void LateUpdate()
        {
            RefreshListener();
            if (voices == null) return;
            float now = Time.unscaledTime;
            float delta = Time.unscaledDeltaTime;
            for (int i = 0; i < voices.Length; i++)
            {
                Voice voice = voices[i];
                if (voice.Event == null) continue;
                if ((voice.HasFollow && (voice.Follow == null || !voice.Follow.gameObject.activeInHierarchy))
                    || (voice.HasOwner && (voice.Owner == null || !voice.Owner.isActiveAndEnabled)))
                { Release(voice); continue; }
                if (voice.HasFollow) voice.Source.transform.position = voice.Follow.position;
                if (voice.Started && !AudioListener.pause)
                    voice.FadeInRemaining = Mathf.Max(0f, voice.FadeInRemaining - delta);
                if (voice.FadeDuration > 0f)
                {
                    voice.FadeRemaining -= delta;
                    if (voice.FadeRemaining <= 0f) { Release(voice); continue; }
                }
                UpdateOcclusion(voice, i, now, delta);
                ApplyMix(voice);
                if (voice.Source.clip == null || voice.Source.clip.loadState == AudioDataLoadState.Failed)
                { Release(voice); continue; }
                if (!voice.Started && Time.realtimeSinceStartup > voice.LoadDeadline)
                { Release(voice); CountSkip(); continue; }
                StartIfLoaded(voice);
                // dspTime freezes under AudioListener.pause, unlike Time.time or a clip-length coroutine.
                if (!voice.Loop && voice.Started && !AudioListener.pause
                    && UnityEngine.AudioSettings.dspTime >= voice.EndDsp)
                    Release(voice);
            }
        }

        void UpdateOcclusion(Voice voice, int index, float now, float delta)
        {
            if (!occlusion || !voice.Event.UseOcclusion)
            {
                voice.Occlusion = voice.OcclusionTarget = 1f;
                voice.BelowListenerMix = voice.BelowListenerTarget = 0f;
                voice.Loudness = voice.LoudnessTarget = 1f;
                return;
            }
            if (now >= voice.NextOcclusionTime)
            {
                ScheduleOcclusion(voice, index);
                SampleOcclusion(voice.Event, voice.Source.transform.position, voice.IgnoreOcclusionRoot, ref voice.OcclusionTarget, ref voice.LoudnessTarget, ref voice.BelowListenerTarget);
            }
            float step = delta / Mathf.Max(0.01f, occlusionSmoothTime);
            voice.Occlusion = Mathf.MoveTowards(voice.Occlusion, voice.OcclusionTarget, step);
            voice.BelowListenerMix = Mathf.MoveTowards(voice.BelowListenerMix, voice.BelowListenerTarget, step);
            voice.Loudness = Mathf.MoveTowards(voice.Loudness, voice.LoudnessTarget, step);
        }

        // Clear is 1 for an open path. Beyond max distance the rolloff is already silent, so targets are kept.
        void SampleOcclusion(SoundEvent sound, Vector3 sourcePosition, Transform ignoreOcclusionRoot, ref float clearTarget, ref float loudnessTarget, ref float belowTarget)
        {
            if (!occlusion || sound == null || !sound.UseOcclusion || sound.SpatialBlend <= 0f)
            {
                clearTarget = loudnessTarget = 1f;
                belowTarget = 0f;
                return;
            }
            Transform ear = ListenerTransform;
            if (ear == null)
            {
                clearTarget = loudnessTarget = 1f;
                belowTarget = 0f;
                return;
            }
            Vector3 delta = sourcePosition - ear.position;
            float sqrDistance = delta.sqrMagnitude;
            if (sqrDistance <= OcclusionNearDistance * OcclusionNearDistance)
            {
                clearTarget = loudnessTarget = 1f;
                belowTarget = 0f;
                return;
            }
            if (sound.MaxDistance > 0f && sqrDistance > sound.MaxDistance * sound.MaxDistance) return;
            float distance = Mathf.Sqrt(sqrDistance);
            float cast = distance - Mathf.Max(0f, occlusionEndPadding);
            if (cast <= 0f)
            {
                clearTarget = loudnessTarget = 1f;
                belowTarget = 0f;
                return;
            }
            Vector3 direction = delta / distance;
            if (!TryGetOccluder(ear.position, direction, cast, ignoreOcclusionRoot, out RaycastHit hit))
            {
                clearTarget = loudnessTarget = 1f;
                belowTarget = 0f;
                return;
            }
            float blockedFraction = 1f;
            float spread = Mathf.Max(0f, occlusionRaySpread);
            if (spread > 0f)
            {
                // A perpendicular cross also works for sounds directly above/below the listener.
                Vector3 reference = Mathf.Abs(direction.y) < 0.99f ? Vector3.up : Vector3.forward;
                Vector3 right = Vector3.Cross(direction, reference).normalized * spread;
                Vector3 up = Vector3.Cross(right, direction);
                int blocked = 1;
                if (IsOffsetOccluded(ear.position, delta + right, ignoreOcclusionRoot)) blocked++;
                if (IsOffsetOccluded(ear.position, delta - right, ignoreOcclusionRoot)) blocked++;
                if (IsOffsetOccluded(ear.position, delta + up, ignoreOcclusionRoot)) blocked++;
                if (IsOffsetOccluded(ear.position, delta - up, ignoreOcclusionRoot)) blocked++;
                blockedFraction = blocked / 5f;
            }
            clearTarget = 1f - blockedFraction;
            belowTarget = 0f;
            // The actual source height selects the below-listener settings, not the offset endpoints.
            if (delta.y < 0f && -delta.y >= Mathf.Max(0f, belowListenerMinimumDepth))
            {
                loudnessTarget = Mathf.Lerp(1f, Mathf.Clamp01(belowListenerOccludedVolume), blockedFraction);
                belowTarget = 1f;
                return;
            }
            float depth = distance - hit.distance;
            float span = occlusionFarDepth - occlusionNearDepth;
            float behind = span <= 0.01f ? 1f : Mathf.Clamp01((depth - occlusionNearDepth) / span);
            float blockedVolume = Mathf.Lerp(Mathf.Clamp01(occludedNearVolume), Mathf.Clamp01(occludedVolume), behind);
            loudnessTarget = Mathf.Lerp(1f, blockedVolume, blockedFraction);
        }

        bool IsOffsetOccluded(Vector3 origin, Vector3 delta, Transform ignoreRoot)
        {
            float distance = delta.magnitude;
            float cast = distance - Mathf.Max(0f, occlusionEndPadding);
            return cast > 0f && TryGetOccluder(origin, delta / distance, cast, ignoreRoot, out _);
        }

        bool TryGetOccluder(Vector3 origin, Vector3 direction, float distance, Transform ignoreRoot, out RaycastHit hit)
        {
            if (ignoreRoot == null)
                return Physics.Raycast(origin, direction, out hit, distance, occlusionMask, QueryTriggerInteraction.Ignore);

            // Results are unordered and may be truncated. Retry with more space when full
            // so self-colliders cannot hide external obstacles from the query.
            int count;
            while (true)
            {
                count = Physics.RaycastNonAlloc(origin, direction, occlusionHits, distance, occlusionMask, QueryTriggerInteraction.Ignore);
                if (count < occlusionHits.Length) break;
                System.Array.Resize(ref occlusionHits, occlusionHits.Length * 2);
            }
            hit = default;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit candidate = occlusionHits[i];
                if (candidate.collider.transform.IsChildOf(ignoreRoot)) continue;
                if (!found || candidate.distance < hit.distance)
                {
                    hit = candidate;
                    found = true;
                }
            }
            return found;
        }

        void ScheduleOcclusion(Voice voice, int index)
        {
            float interval = Mathf.Max(0.02f, occlusionInterval);
            float stagger = voices.Length > 1 ? interval * index / voices.Length : 0f;
            float now = Time.unscaledTime;
            float due = Mathf.Floor((now + stagger) / interval) * interval - stagger;
            if (due <= now) due += interval;
            voice.NextOcclusionTime = due;
        }

        void ApplyMix(Voice voice)
        {
            float fade = voice.FadeDuration > 0f ? Mathf.Clamp01(voice.FadeRemaining / voice.FadeDuration) : 1f;
            if (voice.FadeInDuration > 0f) fade *= 1f - Mathf.Clamp01(voice.FadeInRemaining / voice.FadeInDuration);
            if (!occlusion || !voice.Event.UseOcclusion)
            {
                voice.Source.volume = voice.BaseVolume * fade;
                if (voice.LowPass.enabled) voice.LowPass.enabled = false;
                return;
            }
            // Leave the filter on for the whole voice. Toggling it when a wall appears clicks.
            float clear = Mathf.Clamp01(voice.Occlusion);
            float blockedHz = Mathf.Clamp(occludedCutoffHz, 10f, ClearCutoffHz);
            float belowHz = Mathf.Clamp(belowListenerOccludedCutoffHz, 10f, ClearCutoffHz);
            float blockedLogHz = Mathf.Lerp(Mathf.Log(blockedHz), Mathf.Log(belowHz), voice.BelowListenerMix);
            voice.LowPass.cutoffFrequency = Mathf.Exp(Mathf.Lerp(blockedLogHz, Mathf.Log(ClearCutoffHz), clear));
            if (!voice.LowPass.enabled) voice.LowPass.enabled = true;
            voice.Source.volume = voice.BaseVolume * Mathf.Clamp01(voice.Loudness) * fade;
        }

        internal bool IsAlive(int index, ulong generation)
            => voices != null && index >= 0 && index < voices.Length
                && voices[index].Event != null && voices[index].Generation == generation;
        internal void Stop(int index, ulong generation, float fadeOut)
        {
            if (!IsAlive(index, generation)) return;
            var voice = voices[index];
            if (fadeOut <= 0f || !voice.Started) { Release(voice); return; }
            // Repeated Stop calls cannot restart or lengthen a fade.
            if (voice.FadeDuration > 0f && voice.FadeRemaining <= fadeOut) return;
            voice.FadeRemaining = voice.FadeDuration = fadeOut;
        }
        void Release(Voice voice)
        {
            if (voice.Event == null) return;
            voice.Source.Stop();
            voice.Source.clip = null;
            voice.Source.outputAudioMixerGroup = null;
            if (voice.LowPass != null && voice.LowPass.enabled) voice.LowPass.enabled = false;
            voice.Event = null;
            voice.Follow = null;
            voice.IgnoreOcclusionRoot = null;
            voice.BelowListenerMix = voice.BelowListenerTarget = 0f;
            voice.Owner = null;
            voice.Occlusion = voice.OcclusionTarget = 1f;
            voice.Loudness = voice.LoudnessTarget = 1f;
            voice.BaseVolume = 1f;
            if (voice.Ambience) ActiveAmbienceCount = Mathf.Max(0, ActiveAmbienceCount - 1);
            voice.Ambience = false;
            ActiveCount = Mathf.Max(0, ActiveCount - 1);
        }
        void OnDisable()
        {
            if (voices != null) foreach (var voice in voices) Release(voice);
            lastClips.Clear();
        }
        void OnDestroy() { if (_instance == this) _instance = null; }
        void OnApplicationQuit() => quitting = true;
    }
}
