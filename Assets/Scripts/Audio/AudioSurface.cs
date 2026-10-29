using UnityEngine;

namespace Game.Audio
{
    /// <summary>Tags a collider (or its children) with a SurfaceType for sounds. Unrelated to locomotion Surface.</summary>
    [AddComponentMenu("DuckZ/Audio/Audio Surface")]
    public sealed class AudioSurface : MonoBehaviour
    {
        public SurfaceType type;

        public static SurfaceType TypeOf(Collider collider)
        {
            if (collider == null) return null;
            var surface = collider.GetComponentInParent<AudioSurface>();
            return surface != null ? surface.type : null;
        }
    }
}
