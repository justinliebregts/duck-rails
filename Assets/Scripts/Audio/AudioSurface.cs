using UnityEngine;

namespace Game.Audio
{
    /// <summary>Tags a collider (or its children) with a SurfaceType for sounds. Unrelated to locomotion Surface.</summary>
    [AddComponentMenu("DuckZ/Audio/Audio Surface")]
    public sealed class AudioSurface : MonoBehaviour
    {
        public SurfaceType type;

        /// <summary>Like TypeOf, but a contact point under a WaterVolume surface is water whatever the collider is.</summary>
        public static SurfaceType TypeAt(Collider collider, Vector3 point)
        {
            var water = WaterVolume.Find(point, 0.05f);
            if (water != null && water.waterSurface != null) return water.waterSurface;
            return TypeOf(collider);
        }

        public static SurfaceType TypeOf(Collider collider)
        {
            if (collider == null) return null;
            var surface = collider.GetComponentInParent<AudioSurface>();
            return surface != null ? surface.type : null;
        }
    }
}
