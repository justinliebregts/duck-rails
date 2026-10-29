using Liv.Lck.Settings;
using UnityEngine;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;

namespace Liv.Lck
{
    internal class LckOutputConfigurer : ILckOutputConfigurer
    {
        private readonly ILckEventBus _eventBus;
        
        private CameraTrackDescriptor _recordingCameraTrackDescriptor;
        private CameraTrackDescriptor _streamingCameraTrackDescriptor;

        private LckCaptureType _activeCaptureType = LckCaptureType.Recording;
        
        [Preserve]
        public LckOutputConfigurer(ILckQualityConfig qualityConfig, ILckEventBus eventBus)
        {
            _eventBus = eventBus;
            ConfigureDefaultSettings(qualityConfig);
        }

        public LckResult ConfigureFromQualityConfig(QualityOption qualityOption)
        {
            var isRecordingDescriptorValid = IsValidDescriptor(qualityOption.RecordingCameraTrackDescriptor);
            if (isRecordingDescriptorValid)
            {
                SetCameraTrackDescriptor(LckCaptureType.Recording, qualityOption.RecordingCameraTrackDescriptor);
            }
            
            var isStreamingDescriptorValid = IsValidDescriptor(qualityOption.StreamingCameraTrackDescriptor);
            if (isStreamingDescriptorValid)
            {
                SetCameraTrackDescriptor(LckCaptureType.Streaming, qualityOption.StreamingCameraTrackDescriptor);
            }
            
            return CreateQualityConfigurationResult(qualityOption, isRecordingDescriptorValid, isStreamingDescriptorValid);
        }
        
        public LckResult<LckCaptureType> GetActiveCaptureType()
        {
            return LckResult<LckCaptureType>.NewSuccess(_activeCaptureType);
        }

        public LckResult SetActiveCaptureType(LckCaptureType captureType)
        {
            if (_activeCaptureType != captureType)
            {
                _activeCaptureType = captureType;
                OnActiveCameraTrackDescriptorChanged();
            }
            return LckResult.NewSuccess();
        }

        public LckResult SetActiveVideoFramerate(uint framerate)
        {
            switch (_activeCaptureType)
            {
                case LckCaptureType.Recording:
                    _recordingCameraTrackDescriptor.Framerate = framerate;
                    break;
                case LckCaptureType.Streaming:
                    _streamingCameraTrackDescriptor.Framerate = framerate;
                    break;
                default:
                    return NewUnknownCaptureTypeError();
            }

            TriggerCameraFramerateChangedEvent(framerate);
            return LckResult.NewSuccess();
        }

        public LckResult SetActiveVideoBitrate(uint bitrate)
        {
            switch (_activeCaptureType)
            {
                case LckCaptureType.Recording:
                    _recordingCameraTrackDescriptor.Bitrate = bitrate;
                    break;
                case LckCaptureType.Streaming:
                    _streamingCameraTrackDescriptor.Bitrate = bitrate;
                    break;
                default:
                    return NewUnknownCaptureTypeError();
            }
            return LckResult.NewSuccess();
        }

        public LckResult SetActiveAudioBitrate(uint bitrate)
        {
            switch (_activeCaptureType)
            {
                case LckCaptureType.Recording:
                    _recordingCameraTrackDescriptor.AudioBitrate = bitrate;
                    break;
                case LckCaptureType.Streaming:
                    _streamingCameraTrackDescriptor.AudioBitrate = bitrate;
                    break;
                default:
                    return NewUnknownCaptureTypeError();
            }
            
            return LckResult.NewSuccess();
        }

        public LckResult SetActiveResolution(CameraResolutionDescriptor resolution)
        {
            return SetResolution(_activeCaptureType, resolution);
        }

        public LckResult<CameraTrackDescriptor> GetCameraTrackDescriptor(LckCaptureType captureType)
        {
            return captureType switch
            {
                LckCaptureType.Recording => 
                    LckResult<CameraTrackDescriptor>.NewSuccess(_recordingCameraTrackDescriptor),
                LckCaptureType.Streaming => 
                    LckResult<CameraTrackDescriptor>.NewSuccess(_streamingCameraTrackDescriptor),
                _ => NewUnknownCaptureTypeError<CameraTrackDescriptor>()
            };
        }
        
        public LckResult SetCameraTrackDescriptor(LckCaptureType captureType, CameraTrackDescriptor trackDescriptor)
        {
            switch (captureType)
            {
                case LckCaptureType.Recording:
                    _recordingCameraTrackDescriptor = trackDescriptor;
                    break;
                case LckCaptureType.Streaming:
                    _streamingCameraTrackDescriptor = trackDescriptor;
                    break;
                default:
                    return LckResult.NewError(LckError.UnknownError, "Unknown capture type");
            }

            if (captureType == _activeCaptureType)
                OnActiveCameraTrackDescriptorChanged();
            
            return LckResult.NewSuccess();
        }

        public LckResult SetCameraOrientation(LckCameraOrientation orientation)
        {
            var setRecordingOrientationResult = SetCameraOrientation(LckCaptureType.Recording, orientation);
            var setStreamingOrientationResult = SetCameraOrientation(LckCaptureType.Streaming, orientation);

            if (setRecordingOrientationResult.Success && setStreamingOrientationResult.Success)
                return LckResult.NewSuccess();
            
            var errorMessage = $"{nameof(SetCameraOrientation)} failed with the following errors: ";
            if (!setRecordingOrientationResult.Success)
            {
                errorMessage += $"\n  - {setRecordingOrientationResult.Message}";
            }

            if (!setStreamingOrientationResult.Success)
            {
                errorMessage += $"\n  - {setStreamingOrientationResult.Message}";
            }

            return LckResult.NewError(LckError.UnknownError, errorMessage);
        }
        
        public LckResult<CameraTrackDescriptor> GetActiveCameraTrackDescriptor()
        {
            return _activeCaptureType switch
            {
                LckCaptureType.Recording => 
                    LckResult<CameraTrackDescriptor>.NewSuccess(_recordingCameraTrackDescriptor),
                LckCaptureType.Streaming => 
                    LckResult<CameraTrackDescriptor>.NewSuccess(_streamingCameraTrackDescriptor),
                _ => NewUnknownCaptureTypeError<CameraTrackDescriptor>()
            };
        }

        public LckResult SetActiveCameraTrackDescriptor(CameraTrackDescriptor trackDescriptor)
        {
            return SetCameraTrackDescriptor(_activeCaptureType, trackDescriptor);
        }

        public LckResult<uint> GetNumberOfAudioChannels()
        {
            // TODO: Use audio system (e.g. Unity AudioSettings) to determine actual channel count
            return LckResult<uint>.NewSuccess(2); 
        }

        public LckResult<uint> GetAudioSampleRate()
        {
            var audioSystemSampleRate = DetermineAudioSystemSampleRate();
            if (audioSystemSampleRate <= 0)
            {
                return LckResult<uint>.NewError(LckError.UnknownError,
                    $"Invalid audio sample rate retrieved from audio system: {audioSystemSampleRate}Hz");
            }
            
            return LckResult<uint>.NewSuccess((uint) audioSystemSampleRate);
        }

        private void ConfigureDefaultSettings(ILckQualityConfig qualityConfig)
        {
            var qualityOptions = qualityConfig.GetQualityOptionsForSystem();
            var currentQualityOption = qualityOptions.Find(option => option.IsDefault);
            var configureResult = ConfigureFromQualityConfig(currentQualityOption);
            if (!configureResult.Success)
            {
                LckLog.LogError($"LCK: Failed to configure default output settings - {configureResult.Message}");
            }
        }

        private void TriggerCameraResolutionChangedEvent(CameraResolutionDescriptor resolution)
        {
            var result = LckResult<CameraResolutionDescriptor>.NewSuccess(resolution);
            _eventBus.Trigger(new CameraResolutionChangedEvent(result));
        }
        
        private void TriggerCameraFramerateChangedEvent(uint framerate)
        {
            var result = LckResult<uint>.NewSuccess(framerate);
            _eventBus.Trigger(new CameraFramerateChangedEvent(result));
        }

        private void OnActiveCameraTrackDescriptorChanged()
        {
            var activeCameraTrackDescriptor = GetActiveCameraTrackDescriptor().Result;
            TriggerCameraFramerateChangedEvent(activeCameraTrackDescriptor.Framerate);
            TriggerCameraResolutionChangedEvent(activeCameraTrackDescriptor.CameraResolutionDescriptor);
        }
        
        private CameraResolutionDescriptor GetResolution(LckCaptureType captureType)
        {
            return GetCameraTrackDescriptor(captureType).Result.CameraResolutionDescriptor;
        }
        
        private LckResult SetResolution(LckCaptureType captureType, CameraResolutionDescriptor resolution)
        {
            switch (captureType)
            {
                case LckCaptureType.Recording:
                    _recordingCameraTrackDescriptor.CameraResolutionDescriptor = resolution;
                    break;
                case LckCaptureType.Streaming:
                    _streamingCameraTrackDescriptor.CameraResolutionDescriptor = resolution;
                    break;
                default:
                    return NewUnknownCaptureTypeError();
            }
            
            if (captureType == _activeCaptureType)
                TriggerCameraResolutionChangedEvent(resolution);
            
            return LckResult.NewSuccess();
        }
        
        private LckResult SetCameraOrientation(LckCaptureType captureType, LckCameraOrientation orientation)
        {
            var currentResolution = GetResolution(captureType);
            if (GetCameraOrientation(currentResolution) == orientation)
            {
                // Resolution for capture type is already in the given orientation
                return LckResult.NewSuccess(); 
            }

            var newResolution = currentResolution.GetResolutionInOrientation(orientation);
            return SetResolution(captureType, newResolution);
        }
        
        private static LckResult CreateQualityConfigurationResult(QualityOption qualityOption, 
            bool isRecordingValid, bool isStreamingValid)
        {
            if (isRecordingValid && isStreamingValid)
                return LckResult.NewSuccess();
            
            var errorMessage = $"{nameof(QualityOption)} ({qualityOption.Name}) has an invalid " + 
                               $"{nameof(CameraTrackDescriptor)} for the following capture type(s): ";

            if (!isRecordingValid)
                errorMessage += "\n  - Recording";

            if (!isStreamingValid)
                errorMessage += "\n  - Streaming";

            return LckResult.NewError(LckError.InvalidDescriptor, errorMessage);
        }
        
        private static int DetermineAudioSystemSampleRate()
        {
#if LCK_WWISE
            // ReSharper disable AccessToStaticMemberViaDerivedType (for compatability with Wwise versions < 2024.1.0)
#pragma warning disable CS0618 // Ignore "Type or member is obsolete" (for compatability with Wwise versions < 2024.1.0)
            if (AkSoundEngine.IsInitialized()) 
                return (int)AkSoundEngine.GetSampleRate();
#pragma warning restore CS0618
            // ReSharper restore AccessToStaticMemberViaDerivedType
            
            var fallbackSampleRate = LckSettings.Instance.FallbackSampleRate;
            LckLog.LogWarning(
                "LCK tried to get the Wwise sample rate before Wwise was initialized. " + 
                $"Using fallback sample rate from {nameof(LckSettings)} ({fallbackSampleRate})");
            return fallbackSampleRate;
#elif LCK_FMOD
            FMODUnity.RuntimeManager.CoreSystem.getSoftwareFormat(out int sampleRate, out _, out _);
            return sampleRate;
#endif

#if LCK_NOT_UNITY_AUDIO
            return LckSettings.Instance.FallbackSampleRate;
#else
            return AudioSettings.outputSampleRate;
#endif
        }
        
        private static bool IsValidDescriptor(CameraTrackDescriptor descriptor)
        {
            return descriptor.CameraResolutionDescriptor.IsValid() &&
                   descriptor.Bitrate > 0 &&
                   descriptor.Framerate > 0 &&
                   descriptor.AudioBitrate > 0;
        }

        private static LckCameraOrientation GetCameraOrientation(CameraResolutionDescriptor resolution)
        {
            return resolution.Width >= resolution.Height
                ? LckCameraOrientation.Landscape
                : LckCameraOrientation.Portrait;
        }

        private static LckResult<T> NewUnknownCaptureTypeError<T>() =>
            LckResult<T>.NewError(LckError.UnknownError, "Unknown capture type");

        private static LckResult NewUnknownCaptureTypeError() =>
            LckResult.NewError(LckError.UnknownError, "Unknown capture type");
    }
}

