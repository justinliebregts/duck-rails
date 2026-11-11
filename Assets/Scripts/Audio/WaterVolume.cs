using System.Collections.Generic;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// A body of water as an oriented box whose top face is the surface. No collider or trigger callbacks: other code
    /// asks whether a point is below the surface. Used for wet footsteps/hand hits and for the local head going under
    /// (see WaterListenerEffects). Purely local audio/visual feedback; nothing here is networked.
    /// </summary>
    [AddComponentMenu("DuckZ/Audio/Water Volume")]
    public sealed class WaterVolume : MonoBehaviour
    {
        [Tooltip("Local-space box. Its top face is the water surface; everything inside counts as under water.")]
        public Vector3 size = new Vector3(10f, 2f, 10f);
        [Tooltip("Surface type used for footsteps and hand hits taken below the surface.")]
        public SurfaceType waterSurface;
        [Header("Head under water")]
        public SoundEvent enterSound;
        public SoundEvent exitSound;
        [Tooltip("Played now and then while the head stays under.")]
        public SoundEvent bubblesSound;
        [Min(1f)] public float bubbleInterval = 8f;
        [Tooltip("Low-pass cutoff on the listener while under water.")]
        [Range(200f, 8000f)] public float underwaterCutoffHz = 900f;
        public Color fogColor = new Color(0.10f, 0.32f, 0.38f, 1f);
        [Tooltip("Exponential fog density while fully under water.")]
        [Min(0f)] public float fogDensity = 0.35f;

        static readonly List<WaterVolume> active = new List<WaterVolume>();
        public static IReadOnlyList<WaterVolume> Active => active;

        public float SurfaceY => transform.TransformPoint(new Vector3(0f, size.y * 0.5f, 0f)).y;

        void OnEnable()
        {
            active.Add(this);
            WaterListenerEffects.EnsureExists();
        }

        void OnDisable() => active.Remove(this);

        /// <summary>True when the world point is inside the box. surfaceMargin extends the top face upward (metres, local).</summary>
        public bool Contains(Vector3 worldPoint, float surfaceMargin = 0f)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint);
            Vector3 half = size * 0.5f;
            return Mathf.Abs(p.x) <= half.x && Mathf.Abs(p.z) <= half.z
                && p.y >= -half.y && p.y <= half.y + surfaceMargin;
        }

        public static WaterVolume Find(Vector3 worldPoint, float surfaceMargin = 0f)
        {
            for (int i = 0; i < active.Count; i++)
                if (active[i] != null && active[i].Contains(worldPoint, surfaceMargin)) return active[i];
            return null;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.25f);
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 1f);
            Gizmos.DrawWireCube(Vector3.zero, size);
        }

        void OnValidate()
        {
            size = Vector3.Max(size, new Vector3(0.1f, 0.1f, 0.1f));
        }
    }
}
