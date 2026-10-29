using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    [CreateAssetMenu(menuName = "DuckZ/Audio/Sound Event")]
    public sealed class SoundEvent : ScriptableObject
    {
        [SerializeField] AudioClip[] clips = new AudioClip[0];
        [SerializeField] bool avoidImmediateRepeat = true;
        [SerializeField, MinMaxRange(0.5f, 1.5f)] Vector2 pitchRange = new Vector2(0.95f, 1.05f);
        [SerializeField, MinMaxRange(0f, 1f)] Vector2 volumeRange = Vector2.one;
        [Header("Position")]
        [SerializeField, Range(0f, 1f)] float spatialBlend = 1f;
        [SerializeField, Min(0.01f)] float minDistance = 1f;
        [SerializeField, Min(0.01f)] float maxDistance = 25f;
        [SerializeField] AudioRolloffMode rolloff = AudioRolloffMode.Linear;
        [Tooltip("Zero disables request culling. Separate from Unity's rolloff distance. Loops are not culled here.")]
        [SerializeField, Min(0f)] float cullDistance = 40f;
        [Header("Mix / budget")]
        [SerializeField] AudioMixerGroup mixerOverride;
        [Tooltip("Lower is more important, matching Unity AudioSource priority.")]
        [SerializeField, Range(0, 255)] int priority = 128;
        [Tooltip("Global limit for this asset; zero means pool capacity only.")]
        [SerializeField, Min(0)] int maxInstances = 4;
        [SerializeField, Range(0f, 1f)] float reverbZoneMix = 1f;
        [Tooltip("Use the manager's wall occlusion checks. Disable for broad environment beds that do not need per-source raycasts.")]
        [SerializeField] bool useOcclusion = true;

        public int Priority => priority;
        public int MaxInstances => maxInstances;
        public float CullDistance => cullDistance;
        public float SpatialBlend => spatialBlend;
        public float MaxDistance => maxDistance;
        public bool UseOcclusion => useOcclusion;

        // History lives in the caller (AudioManager, preview), never in the asset, so Play mode can't dirty it.
        public bool TrySelect(AudioClip previous, out SoundSelection selection)
        {
            selection = default;
            if (clips == null) return false;
            int valid = 0, alternatives = 0;
            foreach (var clip in clips)
            {
                if (clip == null) continue;
                valid++;
                if (clip != previous) alternatives++;
            }
            if (valid == 0) return false;
            bool excludePrevious = avoidImmediateRepeat && alternatives > 0;
            int pick = Random.Range(0, excludePrevious ? alternatives : valid);
            foreach (var clip in clips)
            {
                if (clip == null || (excludePrevious && clip == previous)) continue;
                if (pick-- != 0) continue;
                selection = new SoundSelection(clip,
                    Random.Range(Mathf.Clamp(Mathf.Min(pitchRange.x, pitchRange.y), 0.1f, 3f),
                        Mathf.Clamp(Mathf.Max(pitchRange.x, pitchRange.y), 0.1f, 3f)),
                    Random.Range(Mathf.Clamp01(Mathf.Min(volumeRange.x, volumeRange.y)),
                        Mathf.Clamp01(Mathf.Max(volumeRange.x, volumeRange.y))));
                return true;
            }
            return false;
        }

        internal void Configure(AudioSource source, SoundSelection selection, AudioMixerGroup fallback, bool loop)
        {
            source.clip = selection.Clip;
            source.pitch = selection.Pitch;
            source.volume = selection.Volume;
            source.loop = loop;
            source.playOnAwake = false;
            source.spatialBlend = spatialBlend;
            source.spatialize = false;
            source.spatializePostEffects = false;
            source.minDistance = Mathf.Max(0.01f, minDistance);
            source.maxDistance = Mathf.Max(source.minDistance, maxDistance);
            source.rolloffMode = rolloff == AudioRolloffMode.Custom ? AudioRolloffMode.Linear : rolloff;
            source.outputAudioMixerGroup = mixerOverride != null ? mixerOverride : fallback;
            source.priority = priority;
            source.dopplerLevel = 0f;
            source.spread = 0f;
            source.panStereo = 0f;
            source.mute = false;
            source.bypassEffects = false;
            source.bypassListenerEffects = false;
            source.bypassReverbZones = reverbZoneMix <= 0f;
            source.reverbZoneMix = reverbZoneMix;
            source.ignoreListenerPause = false;
            source.ignoreListenerVolume = false;
        }

        void OnValidate()
        {
            minDistance = Mathf.Max(0.01f, minDistance);
            maxDistance = Mathf.Max(minDistance, maxDistance);
            maxInstances = Mathf.Max(0, maxInstances);
            cullDistance = Mathf.Max(0f, cullDistance);
            // Custom curves are intentionally outside the first version.
            if (rolloff == AudioRolloffMode.Custom) rolloff = AudioRolloffMode.Linear;
        }
    }

    public readonly struct SoundSelection
    {
        public AudioClip Clip { get; }
        public float Pitch { get; }
        public float Volume { get; }
        public SoundSelection(AudioClip clip, float pitch, float volume)
        { Clip = clip; Pitch = pitch; Volume = volume; }
    }
}
