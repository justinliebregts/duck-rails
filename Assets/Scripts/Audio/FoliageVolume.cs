using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>Rustling coverage for non-solid plants, baked from their stalks.</summary>
    public sealed class FoliageVolume : MonoBehaviour
    {
        public SurfaceType surface;
        public SoundEvent rustle;
        public Bounds bounds;
        [Tooltip("A continuous canopy that rustles once when the listener enters its bounds.")]
        public bool rustleOnEntry;
        [Tooltip("Local stalk base XYZ and height W. No physical colliders are added.")]
        public Vector4[] stalks = System.Array.Empty<Vector4>();
        [Min(0.01f)] public float radius = 0.6f;
        static readonly List<FoliageVolume> active = new List<FoliageVolume>();

        void OnEnable()
        {
            if (!active.Contains(this)) active.Add(this);
            if (Application.isPlaying) FoliageRustle.EnsureExists();
        }
        void OnDisable() => active.Remove(this);

        public bool Contains(Vector3 point)
        {
            point = transform.InverseTransformPoint(point);
            if (!bounds.Contains(point)) return false;
            if (rustleOnEntry) return true;
            foreach (var stalk in stalks)
            {
                float dx = point.x - stalk.x, dz = point.z - stalk.z;
                if (dx * dx + dz * dz <= radius * radius &&
                    point.y >= stalk.y - 0.1f && point.y <= stalk.y + stalk.w) return true;
            }
            return false;
        }

        public static FoliageVolume Find(Vector3 point)
        {
            foreach (var volume in active)
                if (volume != null && volume.isActiveAndEnabled && volume.Contains(point)) return volume;
            return null;
        }
    }
}
