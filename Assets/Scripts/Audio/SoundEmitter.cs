using UnityEngine;

namespace Game.Audio
{
    /// <summary>Inspector/UnityEvent-friendly playback on a prop, enemy, or item.</summary>
    public sealed class SoundEmitter : MonoBehaviour
    {
        [SerializeField] SoundEvent sound;
        [SerializeField] Transform origin;
        [Tooltip("Applies to Play only. Loops always follow the origin.")]
        [SerializeField] bool follow = true;
        [SerializeField] bool loopOnEnable;
        [Tooltip("Ignore colliders on this transform and its children when checking sound occlusion.")]
        [SerializeField] Transform ignoreOcclusionRoot;
        AudioHandle loop;
        Transform Origin => origin != null ? origin : transform;
        public bool IsLooping => loop.IsPlaying;
        void OnEnable()
        {
            if (loopOnEnable) StartLoop();
        }
        public void Play()
        {
            if (!isActiveAndEnabled || AudioManager.instance == null) return;
            if (follow) AudioManager.instance.PlayAttached(sound, Origin, this, ignoreOcclusionRoot);
            else AudioManager.instance.Play(sound, Origin.position, ignoreOcclusionRoot: ignoreOcclusionRoot);
        }
        public void StartLoop()
        {
            if (!isActiveAndEnabled || loop.IsPlaying || AudioManager.instance == null) return;
            loop = AudioManager.instance.PlayLoop(sound, Origin, this, ignoreOcclusionRoot);
        }
        public void StopLoop() => loop.Stop();
        public void FadeOutLoop(float seconds) => loop.Stop(seconds);
        void OnDisable() => loop.Stop();
    }
}
