using System;
using System.Collections;
using System.Collections.Generic;
using Liv.Lck.Collections;
using Liv.Lck.Core;
using Liv.Lck.Encoding;
using Liv.Lck.Telemetry;
using UnityEngine;
using UnityEngine.Scripting;

namespace Liv.Lck
{
    internal class LckEncodeLooper : ILckEncodeLooper, ILckEarlyUpdate
    {
        private readonly ILckEncoder _encoder;
        private readonly ILckOutputConfigurer _outputConfigurer;
        private readonly ILckAudioMixer _audioMixer;
        private readonly ILckVideoCapturer _videoCapturer;
        private readonly ILckTelemetryClient _telemetryClient;

        private float _pausedForTime;
        private float _videoTime;
        private float _prevVideoTime;
        private bool _disposed;
        
        private const float MinVideoTimeIncrement = 0.001f;
        private const float TrackTimestampDifferenceTolerance = 0.3f;
        private const int EncodingWarmupFrames = 3;
        private const string EncodingWarmupCoroutineName = "LckEncodeLooper:StartEncodingAfterWarmupFrames";
        
        
        [Preserve]
        public LckEncodeLooper(
            ILckEncoder encoder, 
            ILckOutputConfigurer outputConfigurer, 
            ILckAudioMixer audioMixer, 
            ILckVideoCapturer videoCapturer,
            ILckEventBus eventBus,
            ILckTelemetryClient telemetryClient)
        {
            _encoder = encoder;
            _outputConfigurer = outputConfigurer;
            _audioMixer = audioMixer;
            _videoCapturer = videoCapturer;
            _telemetryClient = telemetryClient;

            eventBus.AddListener<LckEvents.EncoderStartedEvent>(OnEncoderStarted);
        }
        
        public void EarlyUpdate()
        {
            if (_encoder?.IsActive() != true)
            {
                UnregisterEncodeFrameEarlyUpdate();
                return;
            }

            var passedTime = Time.unscaledDeltaTime;

            // Consume audio even if encoder is paused to avoid audio build-up while paused
            var audioData = _audioMixer.GetMixedAudio(_videoTime + _pausedForTime);
            
            // If there is a lag spike and the time between frames is >1s, audio buffers may have filled resulting in
            // lost data, so clamp the time progression based on the number of available audio samples to avoid de-sync
            if (passedTime > 1f)
            {
                LckLog.LogWarning("LCK detected lag spike during capture - adjusting capture time accordingly");
                var allChannelSampleRate = _outputConfigurer.GetAudioSampleRate().Result *
                                           _outputConfigurer.GetNumberOfAudioChannels().Result;
                passedTime = (float)audioData.Count / allChannelSampleRate;
            }
            
            if (_encoder.IsPaused())
            {
                _pausedForTime += passedTime;
                return;
            }
            
            if (!IsAudioDataValid(audioData))
                return;

            // Handle case where audioData is empty on the first frame
            var currentEncoderSessionData = _encoder.GetCurrentSessionData();
            if (currentEncoderSessionData.EncodedAudioSamplesPerChannel == 0 && audioData.Count == 0)
            {
                for (int i = 0; i < 1024; i++)
                {
                    audioData.TryAdd(0);
                }
            }
            
            // Keep track times aligned
            EnsureTrackTimeAlignment(ref _videoTime, CalculateAudioTime(), _prevVideoTime);
            
            if (!_encoder.EncodeFrame(_videoTime, audioData, _videoCapturer.HasCurrentFrameBeenCaptured()))
            {
                HandleEncodeFrameError(
                    $"LCK EncodeFrame returned false. This indicates a critical error. (recordingTime: {currentEncoderSessionData.CaptureTimeSeconds}, audioTimestampSamples: {currentEncoderSessionData.EncodedAudioSamplesPerChannel})");
            }
            
            _videoTime += passedTime;
        }

        private float CalculateAudioTime()
        {
            var encoderSessionData = _encoder.GetCurrentSessionData();
            var sampleRate = _outputConfigurer.GetAudioSampleRate().Result;
            return (float)encoderSessionData.EncodedAudioSamplesPerChannel / sampleRate;
        }
        
        private bool IsAudioDataValid(AudioBuffer audioData)
        {
            if (audioData != null) return true;

            var encoderSessionData = _encoder.GetCurrentSessionData();
            HandleEncodeFrameError(
                $"LCK Audio data is null (captureTime: {encoderSessionData.CaptureTimeSeconds}, audioTimestampSamples: {encoderSessionData.EncodedAudioSamplesPerChannel})");

            return false;
        }
        
        private void StartEncodingFrames()
        {
            _videoTime = _prevVideoTime = _pausedForTime = 0f;
            LckUpdateManager.RegisterSingleEarlyUpdate(this);
        }
        
        private void UnregisterEncodeFrameEarlyUpdate()
        {
            LckUpdateManager.UnregisterSingleEarlyUpdate(this);
        }
        
        private void HandleEncodeFrameError(string errorMessage)
        {
            LckLog.LogError(errorMessage);
            _encoder.StopEncodingAsync();
        }

        private IEnumerator StartEncodingAfterWarmupFrames(int warmupFrameCount)
        {
            // Capture all warmup frames
            _videoCapturer.ForceCaptureAllFrames = true;
            
            // Wait for encoding warmup frames
            while (warmupFrameCount > 0)
            {
                yield return null;
                warmupFrameCount--;
            }
            
            // No longer need to force all frames to be captured (allow frames between target frame times to be culled)
            _videoCapturer.ForceCaptureAllFrames = false;
            
            // Start encoding
            StartEncodingFrames();
        }
        
        private void OnEncoderStarted(LckEvents.EncoderStartedEvent encoderStartedEvent)
        {
            if (!encoderStartedEvent.Result.Success)
                return;
            
            LckMonoBehaviourMediator.StartCoroutine(EncodingWarmupCoroutineName, 
                StartEncodingAfterWarmupFrames(EncodingWarmupFrames));
        }

        private static void EnsureTrackTimeAlignment(ref float videoTime, float audioTime, float prevVideoTime)
        {
            var trackTimeDifference = videoTime - audioTime;
            var absTrackTimeDifference = Math.Abs(trackTimeDifference);
            if (absTrackTimeDifference <= TrackTimestampDifferenceTolerance)
                return; // Track times are approximately aligned
            
            // Should address any de-sync issues at an earlier point in the pipeline. However, if tracks are out of sync
            // at this point, adjust the video track timestamp to force track time alignment and avoid encoding issues
            LckLog.LogError($"Video track is {Mathf.FloorToInt(1000f * absTrackTimeDifference)}ms " + 
                            $"{(trackTimeDifference > 0 ? "ahead of" : "behind")} audio track - adjusting video time to re-sync");
            
            // Adjust video time whilst ensuring video time always progresses forward
            videoTime = Math.Max(audioTime, prevVideoTime + MinVideoTimeIncrement);
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            LckMonoBehaviourMediator.StopCoroutineByName(EncodingWarmupCoroutineName);
            UnregisterEncodeFrameEarlyUpdate();

            _disposed = true;
        }
    }
}
