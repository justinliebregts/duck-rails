using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    [CreateAssetMenu(menuName = "DuckZ/Audio/Settings")]
    public sealed class AudioSettings : ScriptableObject
    {
        [Tooltip("Prewarmed once; never grows during playback. Leave real-voice headroom for Photon.")]
        [Min(1)] public int poolSize = 24;
        [Tooltip("Maximum ambience leases, including loading and fading sounds. Shares the pool; gameplay can reclaim these slots. Zero disables ambience.")]
        [Min(0)] public int ambienceVoiceLimit = 6;
        public AudioMixerGroup effects;
        public AudioMixerGroup voice;
        [Tooltip("Every SoundEvent in the project, kept up to date by the editor and before each build. Sounds sent over the network are looked up here by name.")]
        public SoundEvent[] sounds = new SoundEvent[0];

        Dictionary<string, SoundEvent> soundsByName;

        public SoundEvent FindSound(string soundName)
        {
            if (soundsByName == null)
            {
                soundsByName = new Dictionary<string, SoundEvent>(sounds.Length);
                foreach (SoundEvent sound in sounds)
                    if (sound != null)
                        soundsByName[sound.name] = sound;
            }
            return soundsByName.TryGetValue(soundName, out SoundEvent found) ? found : null;
        }

        void OnValidate() => soundsByName = null;
    }
}
