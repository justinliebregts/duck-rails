using System;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>One per kind of sound source (player hands, spider feet...): what each surface sounds like for it.</summary>
    [CreateAssetMenu(menuName = "DuckZ/Audio/Surface Sound Set")]
    public sealed class SurfaceSoundSet : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public SurfaceType surface;
            public SoundEvent sound;
        }

        [SerializeField] Entry[] entries = new Entry[0];
        [Tooltip("Used for untagged surfaces and surface types with no entry.")]
        [SerializeField] SoundEvent fallback;

        public SoundEvent Resolve(SurfaceType surface)
        {
            if (surface != null && entries != null)
                foreach (var entry in entries)
                    if (entry.surface == surface && entry.sound != null) return entry.sound;
            return fallback;
        }

        public SoundEvent Resolve(Collider collider) => Resolve(AudioSurface.TypeOf(collider));
    }
}
