using System;
using UnityEngine;

namespace VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime
{
    /// <summary>
    /// Minimal proxy that must be on the same GameObject as an AudioListener.
    /// It raises an event each time Unity calls OnAudioFilterRead.
    /// </summary>
    [RequireComponent(typeof(AudioListener))]
    public class AudioCaptureProxy : MonoBehaviour
    {
        /// <summary>
        /// Fired whenever OnAudioFilterRead occurs.
        /// float[] data  = interleaved audio samples
        /// int channels  = number of channels
        /// </summary>
        public static event Action<float[], int> OnAudioFilterReadEvent;

        private void OnAudioFilterRead(float[] data, int channels)
        {
            OnAudioFilterReadEvent?.Invoke(data, channels);
        }
    }
}