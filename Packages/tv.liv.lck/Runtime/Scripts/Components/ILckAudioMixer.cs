using System;
using Liv.Lck.Collections;

namespace Liv.Lck
{
    /// <summary>
    /// Interface defining the API of an audio mixer service
    /// </summary>
    internal interface ILckAudioMixer : IDisposable
    {
        /// <summary>
        /// Provides mixed audio from all <see cref="ILckAudioSource"/>s
        /// </summary>
        /// <param name="recordingTime">
        /// The current <see cref="recordingTime"/> used to apply sample count correction to keep audio in sync
        /// </param>
        /// <returns><see cref="AudioBuffer"/> of the mixed audio data</returns>
        AudioBuffer GetMixedAudio(float recordingTime);
        
        /// <summary>
        /// Activates / deactivates microphone capture
        /// </summary>
        /// <param name="isOpen">Whether to activate microphone capture or not</param>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        LckResult SetMicrophoneCaptureActive(bool isOpen);
        
        /// <summary>
        /// Checks whether microphone capture is currently active
        /// </summary>
        /// <returns>
        /// <see cref="LckResult"/> indicating success / failure, providing a <c>bool</c> indicating whether microphone
        /// capture is active or not
        /// </returns>
        LckResult<bool> GetMicrophoneCaptureActive();
        
        /// <summary>
        /// Mutes / unmutes game audio
        /// </summary>
        /// <param name="isMute">Whether to mute game audio or not</param>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        LckResult SetGameAudioMute(bool isMute);
        
        /// <summary>
        /// Checks whether game audio is currently muted
        /// </summary>
        /// <returns>
        /// <see cref="LckResult"/> indicating success / failure, providing a <c>bool</c> indicating whether game audio
        /// is currently muted or not
        /// </returns>
        LckResult<bool> IsGameAudioMute();
        
        /// <summary>
        /// Gets the most recent microphone output level
        /// </summary>
        /// <returns>The most recent microphone output level</returns>
        float GetMicrophoneOutputLevel();
        
        /// <summary>
        /// Gets the most recent game output level
        /// </summary>
        /// <returns>The most recent game output level</returns>
        float GetGameOutputLevel();
        
        /// <summary>
        /// Sets the microphone gain value
        /// </summary>
        /// <param name="gain">The new microphone gain value</param>
        void SetMicrophoneGain(float gain);
        
        /// <summary>
        /// Sets the game audio gain value
        /// </summary>
        /// <param name="gain">The new game audio gain value</param>
        void SetGameAudioGain(float gain);

        /// <summary>
        /// Reads any available audio data from <see cref="ILckAudioSource"/>s so that it is included in the next mix
        /// </summary>
        public void ReadAvailableAudioData();
    }
}
