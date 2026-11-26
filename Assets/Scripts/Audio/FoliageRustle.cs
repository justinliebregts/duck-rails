using UnityEngine;

namespace Game.Audio
{
    /// <summary>One local listener emitter, independent of footsteps or hand contacts.</summary>
    public sealed class FoliageRustle : MonoBehaviour
    {
        static FoliageRustle instance;
        Transform listener;
        Vector3 previous;
        bool sampled;
        FoliageRustleMotion motion;
        AudioHandle handle;
        FoliageVolume entryVolume;

        public static void EnsureExists()
        {
            if (instance != null) return;
            instance = new GameObject("Foliage Rustle").AddComponent<FoliageRustle>();
        }

        void LateUpdate()
        {
            var manager = AudioManager.instance;
            var head = manager != null ? manager.ListenerTransform : null;
            if (head != listener || head == null || Time.deltaTime <= 0f)
            {
                ResetMotion();
                listener = head;
                return;
            }
            Vector3 position = head.position;
            if (!sampled) { previous = position; sampled = true; return; }
            Vector3 delta = position - previous;
            previous = position;
            var volume = FoliageVolume.Find(position);
            bool inside = volume != null && volume.rustle != null && !WaterListenerEffects.HeadSubmerged;
            if (inside && volume.rustleOnEntry)
            {
                if (entryVolume != volume)
                {
                    handle.Stop(0.04f);
                    handle = default;
                    manager.PlayAttached(volume.rustle, head, this);
                }
                entryVolume = volume;
                motion = default;
                return;
            }
            entryVolume = null;
            bool play = motion.Step(delta, Time.deltaTime, inside, Time.time);
            if (!motion.Moving)
            {
                handle.Stop(0.06f);
                handle = default;
            }
            if (play)
            {
                // Avoid stacked voices; selection randomizes pitch and volume on every repeat.
                handle.Stop(0.04f);
                handle = manager.PlayAttached(volume.rustle, head, this);
            }
        }

        void ResetMotion()
        {
            handle.Stop();
            handle = default;
            motion = default;
            entryVolume = null;
            sampled = false;
        }

        void OnDisable()
        {
            ResetMotion();
            if (instance == this) instance = null;
        }
    }

    /// <summary>Movement gate shared by the emitter and deterministic EditMode checks.</summary>
    public struct FoliageRustleMotion
    {
        float distance;
        float next;
        public bool Moving { get; private set; }

        public bool Step(Vector3 delta, float dt, bool inside, float now)
        {
            float horizontal = new Vector2(delta.x, delta.z).magnitude;
            // Ignore tiny tracking motion, frame stalls, and warps. Vertical bobbing alone is silent.
            Moving = inside && dt > 0f && dt <= 0.2f &&
                horizontal / dt >= 0.12f && delta.magnitude / dt <= 15f;
            if (!Moving) { distance = 0f; next = now; return false; }
            distance += horizontal;
            if (distance < 0.04f || now < next) return false;
            distance = 0f;
            next = now + 0.32f;
            return true;
        }
    }
}
