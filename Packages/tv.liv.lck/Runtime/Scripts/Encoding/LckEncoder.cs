using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Liv.Lck.Collections;
using Liv.Lck.Core;
using Liv.Lck.ErrorHandling;
using Liv.Lck.Telemetry;
using Liv.NGFX;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;

namespace Liv.Lck.Encoding
{
    internal class LckEncoder : ILckEncoder
    {
        private readonly ILckOutputConfigurer _outputConfigurer;
        private readonly ILckVideoTextureProvider _videoTextureProvider;
        private readonly ILckEventBus _eventBus;
        private readonly ILckTelemetryClient _telemetryClient;
        private readonly ILckCaptureErrorDispatcher _captureErrorDispatcher;
        private readonly IList<LckEncodedPacketHandler> _registeredPacketHandlers = new List<LckEncodedPacketHandler>();
        private readonly LckNativeEncodingApi.AudioTrack[] _audioTracks = new LckNativeEncodingApi.AudioTrack[1];
        private readonly bool[] _readyVideoTracks = { false };
        
        private static readonly ProfilerMarker _allocateFrameSubmissionMarker = new ProfilerMarker("LckEncoder.AllocateFrameSubmission");
        private static readonly ProfilerMarker _commandBufferMarker = new ProfilerMarker("LckEncoder.CommandBuffer");
        private static readonly ProfilerMarker _releaseNativeRenderBufferMarker = new ProfilerMarker("LckEncoder.ReleaseNativeRenderBuffer");
        
        private NGFX.LogLevel _logLevel = NGFX.LogLevel.Error;
        private IntPtr _encoderContext;
        private Handle<LckNativeEncodingApi.FrameTexture[]> _textureIds;
        private List<LckNativeEncodingApi.FrameTexture> _texturesList;
        private Handle<LckNativeEncodingApi.ResourceData> _resourceInitData;
        private List<CaptureData> _cameraRenderData = new List<CaptureData>();
        private bool _isActive;
        private IntPtr _resourceContext = IntPtr.Zero;
        private bool _disposed;
        private EncoderSessionData _currentEncoderSessionData;

        private struct CaptureData
        {
            public NativeRenderBuffer nativeRenderBuffer;
            public uint trackIndex;
        }
        
        // Static reference to capture error dispatcher so that it can be used in static native error callback
        private static ILckCaptureErrorDispatcher CaptureErrorDispatcher { get; set; }
      
        [Preserve]
        public LckEncoder(
            ILckOutputConfigurer outputConfigurer, 
            ILckVideoTextureProvider videoTextureProvider, 
            ILckEventBus eventBus, 
            ILckTelemetryClient telemetryClient, 
            ILckCaptureErrorDispatcher captureErrorDispatcher)
        {
            _outputConfigurer = outputConfigurer;
            _videoTextureProvider = videoTextureProvider;
            _eventBus = eventBus;
            _telemetryClient = telemetryClient;
            CaptureErrorDispatcher = captureErrorDispatcher;
        }

        public bool IsActive()
        {
            return _isActive;
        }

        public bool IsPaused()
        {
            return _registeredPacketHandlers.All(encodedPacketHandler => 
                encodedPacketHandler.CaptureStateProvider.IsPaused().Result);
        }
        
        public LckResult StartEncoding(CameraTrackDescriptor cameraTrackDescriptor, 
            IEnumerable<LckEncodedPacketHandler> encodedPacketHandlers)
        {
            if (_isActive)
            {
                return LckResult.NewError(LckError.CaptureAlreadyStarted,
                    "Encoding has already started");
            }
            
            if (!CreateEncoderInstance())
            {
                return LckResult.NewError(LckError.EncodingError, "Failed to create encoder instance");
            }
            
            AddEncodedPacketHandlers(encodedPacketHandlers);
            
            _resourceContext = LckNativeEncodingApi.GetResourceContext(_encoderContext);
            if (_resourceContext == IntPtr.Zero)
            {
                return LckResult.NewError(LckError.EncodingError, 
                    "Resource context pointer is not set");
            }
            
            _resourceInitData = new Handle<LckNativeEncodingApi.ResourceData>(new LckNativeEncodingApi.ResourceData()
            {
                encoderContext = _encoderContext 
            });
            
            var trackInfos = CreateTrackInfoInteropData(cameraTrackDescriptor, 
                _outputConfigurer.GetAudioSampleRate().Result, 
                _outputConfigurer.GetNumberOfAudioChannels().Result);
            
            var initCameraRenderDataResult = InitCameraRenderData(trackInfos);
            if (!initCameraRenderDataResult.Success)
                return initCameraRenderDataResult;
            
            var success = LckNativeEncodingApi.StartEncoder(_encoderContext, trackInfos, (uint)trackInfos.Length);
            if (!success)
            {
                return LckResult.NewError(LckError.EncodingError, "Failed to start native encoder");
            }
            
            ExecuteNativeInitResourcesFunction();
            InitTextureHandles();

            _isActive = true;
            _currentEncoderSessionData = new EncoderSessionData();

            LckLog.Log("Encoder started successfully");
            
            var result = LckResult.NewSuccess();
            _eventBus.Trigger(new EncoderStartedEvent(result));
            return result;
        }
        
        public async Task<LckResult> StopEncodingAsync()
        {
            if (!IsActive())
            {
                // Encoder is already stopped - treat as success (idempotent operation)
                LckLog.Log($"{nameof(StopEncodingAsync)} called while encoder is already inactive - treating as success");
                return LckResult.NewSuccess();
            }

            _isActive = false;
            var stopEncoderResult = await Task.Run(() =>
            {
                try
                {
                    LckNativeEncodingApi.StopEncoder(_encoderContext);
                    return LckResult.NewSuccess();
                }
                catch (Exception ex)
                {
                    LckLog.LogError($"An exception occurred while stopping the encoder: {ex}");
                    return LckResult.NewError(LckError.EncodingError, ex.Message);
                }
            });

            if (!stopEncoderResult.Success)
            {
                return stopEncoderResult;
            }

            UnregisterEncodedPacketHandlers();
            
            ReleaseResources();
            
            // DestroyEncoder will be called by ReleaseResource in native from the render thread
            //LckNativeEncodingApi.DestroyEncoder(_encoderContext);
            
            _encoderContext = IntPtr.Zero;

            LckLog.Log("Encoding stopped successfully");
            
            var result = LckResult.NewSuccess();
            _eventBus.Trigger(new EncoderStoppedEvent(result));
            return result;
        }
        
        public bool EncodeFrame(float videoTimeSeconds, AudioBuffer audioData, bool encodeVideo)
        {
            if (!IsActive())
            {
                LckLog.LogError("Cannot encode frame - encoder is not open");
                return false;
            }
            
            try
            {
                ProvideDataToEncoder(videoTimeSeconds, audioData, encodeVideo);
            }
            catch (Exception e)
            {
                HandleEncodeFrameError("LCK EncodeFrame failed: " + e.Message);
                return false;
            }
            
            return true;
        }
        
        public void SetLogLevel(NGFX.LogLevel logLevel)
        {
            _logLevel = logLevel;
            
            if (_encoderContext != IntPtr.Zero)
                LckNativeEncodingApi.SetEncoderLogLevel(_encoderContext, (uint)_logLevel);
        }

        public EncoderSessionData GetCurrentSessionData()
        {
            return _currentEncoderSessionData;
        }

        private void ProvideDataToEncoder(float videoTime, AudioBuffer audioData, bool encodeVideo)
        {
            using var nativeGameAudio = new Handle<float[]>(audioData.Buffer);
            
            // TODO: Currently assumes 1 audio track, but would need updating to support more tracks
            _audioTracks[0].data = nativeGameAudio.ptr();
            _audioTracks[0].dataSize = (uint)audioData.Count;
            _audioTracks[0].timestampSamples = _currentEncoderSessionData.EncodedAudioSamplesPerChannel; 
            _audioTracks[0].trackIndex = 0;
            
            // TODO: Currently assumes 1 video track, but would need updating to support more tracks
            _readyVideoTracks[0] = encodeVideo;
            
            // Encode frame
            var framePtr = AllocateFrameSubmission(videoTime, _readyVideoTracks, _audioTracks);
            EncodeFrameData(framePtr);
            
            // Update current session data
            if (_readyVideoTracks[0]) _currentEncoderSessionData.EncodedVideoFrames++;
            _currentEncoderSessionData.EncodedAudioSamplesPerChannel +=
                _audioTracks[0].dataSize / _outputConfigurer.GetNumberOfAudioChannels().Result;
            _currentEncoderSessionData.CaptureTimeSeconds = videoTime;
        }

        private void AddEncodedPacketHandler(LckEncodedPacketHandler handler)
        {
            if (_encoderContext == IntPtr.Zero)
            {
                LckLog.LogError("Cannot add encoded packet handler - invalid encoder context");
                return;
            }
            
            if (!handler.EncodedPacketCallback.IsValid)
            {
                LckLog.LogError("Cannot add encoded packet handler - missing callback object or function pointer");
                return;
            }

            if (_registeredPacketHandlers.Contains(handler))
            {
                LckLog.LogError("Cannot add encoded packet handler - it is already registered");
                return;
            }
            
            _registeredPacketHandlers.Add(handler);

            var encodedPacketCallback = handler.EncodedPacketCallback;
            LckNativeEncodingApi.AddEncoderPacketCallback(_encoderContext, 
                encodedPacketCallback.CallbackObjectPtr, encodedPacketCallback.CallbackFunctionPtr);
            
            LckLog.Log("Encoder packet handler added");
        }

        private void AddEncodedPacketHandlers(IEnumerable<LckEncodedPacketHandler> encodedPacketHandlers)
        {
            foreach (var encodedPacketHandler in encodedPacketHandlers)
            {
                AddEncodedPacketHandler(encodedPacketHandler);
            }
        }

        private void RemoveEncodedPacketHandler(LckEncodedPacketHandler handler)
        {
            if (!_registeredPacketHandlers.Remove(handler))
            {
                LckLog.LogError("Cannot remove encoded packet handler - it is not registered");
                return;
            }

            var encodedPacketCallback = handler.EncodedPacketCallback;
            
            LckNativeEncodingApi.RemoveEncoderPacketCallback(_encoderContext, 
                encodedPacketCallback.CallbackObjectPtr, encodedPacketCallback.CallbackFunctionPtr);
            
            LckLog.Log("Removed encoded packet handler");
        }
        
        private bool CreateEncoderInstance()
        {
            if (_encoderContext != IntPtr.Zero)
            {
                LckLog.LogWarning("Encoder context is already set");
                return false;
            }
            
            _encoderContext = LckNativeEncodingApi.CreateEncoder();
            if (_encoderContext == IntPtr.Zero)
            {
                LckLog.LogError("Failed to create native encoder");
                return false;
            }
            
            LckNativeEncodingApi.SetEncoderLogLevel(_encoderContext, (uint)_logLevel);

            if (!LckNativeEncodingApi.SetCaptureErrorCallback(_encoderContext, OnNativeCaptureError))
            {
                LckLog.LogError("Failed to set encoder error callback");
                return false;
            }
            
            LckLog.Log("Encoder created successfully");
            return true;
        }
        
        private IntPtr AllocateFrameSubmission(float frameTime, bool[] readyTracks, LckNativeEncodingApi.AudioTrack[] audioTracks)
        {
            _allocateFrameSubmissionMarker.Begin();
            
            // This ptr will be freed in native after use, it can be null
            var framePtr = LckNativeEncodingApi.AllocateFrameSubmission(new LckNativeEncodingApi.FrameSubmission
            {
                encoderContext = _encoderContext,
                textureIDs = _textureIds.ptr(),
                textureIDsSize = (uint)_textureIds.data().Length,
                videoTimestampMilli = (ulong)(frameTime * 1000),
                audioTracksSize = 1u,
                readyFramesSize = 1u
            }, audioTracks, readyTracks);

            _allocateFrameSubmissionMarker.End();

            return framePtr;
        }

        private static void EncodeFrameData(IntPtr framePtr)
        {
            _commandBufferMarker.Begin();
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(LckNativeEncodingApi.GetPluginUpdateFunction(), 1, framePtr);
            cb.name = "qck Encoder";
            Graphics.ExecuteCommandBuffer(cb);
            _commandBufferMarker.End();
        }

        public void ReleaseNativeRenderBuffers()
        {
            using (_releaseNativeRenderBufferMarker.Auto())
            {
                if (IsActive())
                {
                    LckLog.LogWarning("LCK can't release native render buffers while encoder is active");
                    return;
                }

                foreach (var data in _cameraRenderData)
                {
                    data.nativeRenderBuffer.Dispose();
                }
            }
        }
        
        public int GetAudioFrameSize()
        {
            // NOTE: this assumes audio track is 0
            return (int)LckNativeEncodingApi.GetAudioTrackFrameSize(_encoderContext, 0);
        }

        private void ReleaseResources()
        {
            LckLog.Log("Releasing encoder resources");
            
            ReleaseNativeRenderBuffers();
            
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(LckNativeEncodingApi.GetReleaseResourcesFunction(), 1, _resourceInitData.ptr());
            cb.name = "qck ReleaseResource";
            Graphics.ExecuteCommandBuffer(cb);
        }

        private LckResult InitCameraRenderData(LckNativeEncodingApi.TrackInfo[] trackInfo)
        {
            _cameraRenderData = new List<CaptureData>();

            var trackIndices = trackInfo.Select((track, trackIndex) => (track, trackIndex)).ToArray();
            foreach (var (track, trackIndex) in trackIndices)
            {
                if (track.type == LckNativeEncodingApi.TrackType.Video)
                {
                    _cameraRenderData.Add(InitCameraRenderData(trackIndex));
                }
            }
            
            return _cameraRenderData.Any()
                ? LckResult.NewSuccess()
                : LckResult.NewError(LckError.EncodingError, "No video tracks found");
        }
        
        private CaptureData InitCameraRenderData(int trackIndex)
        {
            // TODO: Assumes there will only be one video track - will need changes to support more tracks
            var isGL = SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLCore || SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3;
            var cameraTrackTexture = _videoTextureProvider.CameraTrackTexture;
            return new CaptureData
            {
                nativeRenderBuffer = isGL ?
                    new NativeRenderBuffer(_resourceContext, cameraTrackTexture.colorBuffer, cameraTrackTexture.GetNativeTexturePtr(),
                        cameraTrackTexture.width, cameraTrackTexture.height, 1, GraphicsFormat.R8G8B8A8_UNorm) :
                    new NativeRenderBuffer(_resourceContext, cameraTrackTexture.colorBuffer, 
                        cameraTrackTexture.width, cameraTrackTexture.height, 1, GraphicsFormat.R8G8B8A8_UNorm),
                trackIndex = (uint)trackIndex
            };
        }

        private void InitTextureHandles()
        {
            _texturesList = new List<LckNativeEncodingApi.FrameTexture>();
            foreach (var data in _cameraRenderData)
            {
                _texturesList.Add(new LckNativeEncodingApi.FrameTexture()
                {
                    id = data.nativeRenderBuffer.id,
                    trackIndex = data.trackIndex
                });
            }
            _textureIds = new Handle<LckNativeEncodingApi.FrameTexture[]>(_texturesList.ToArray());
        }
        
        private void ExecuteNativeInitResourcesFunction()
        {
            var cb = new CommandBuffer();
            cb.IssuePluginEventAndData(LckNativeEncodingApi.GetInitResourcesFunction(), 1, _resourceInitData.ptr());
            cb.name = "qck InitResource";
            Graphics.ExecuteCommandBuffer(cb);
        }

        private void UnregisterEncodedPacketHandlers()
        {
            var packetHandlersToRemove = _registeredPacketHandlers.ToArray();
            foreach (var encodedPacketHandler in packetHandlersToRemove)
            {
                RemoveEncodedPacketHandler(encodedPacketHandler);
            }
        }
        
        private void HandleEncodeFrameError(string errorMessage)
        {
            LckLog.LogError(errorMessage);
            _ = StopEncodingAsync();
        }
        
        private static LckNativeEncodingApi.TrackInfo[] CreateTrackInfoInteropData(
            CameraTrackDescriptor cameraTrackDescriptor, 
            uint audioSampleRate, uint numberOfAudioChannels)
        {
            return new LckNativeEncodingApi.TrackInfo[]
            {
                new LckNativeEncodingApi.TrackInfo
                {
                    type = LckNativeEncodingApi.TrackType.Audio,
                    bitrate = cameraTrackDescriptor.AudioBitrate,
                    samplerate = audioSampleRate,
                    channels = numberOfAudioChannels
                },
                new LckNativeEncodingApi.TrackInfo
                {
                    type = LckNativeEncodingApi.TrackType.Video,
                    bitrate = cameraTrackDescriptor.Bitrate,
                    width = cameraTrackDescriptor.CameraResolutionDescriptor.Width,
                    height = cameraTrackDescriptor.CameraResolutionDescriptor.Height,
                    framerate = cameraTrackDescriptor.Framerate
                }
            };
        }

        [AOT.MonoPInvokeCallback(typeof(LckNativeEncodingApi.CaptureErrorCallback))]
        private static void OnNativeCaptureError(CaptureErrorType errorType, string errorMessage)
        {
            // Instead of handling here (likely not on main thread), push to dispatcher so that any
            // LckEvents.CaptureErrorEvent listeners can react to the error on the main thread
            if (CaptureErrorDispatcher != null)
                CaptureErrorDispatcher.PushError(new LckCaptureError(errorType, errorMessage));
            else
            {
                LckLog.LogError($"The {nameof(CaptureErrorDispatcher)} reference is null while error occurred - " + 
                                $"Error will not be handled: {errorMessage}");
            }
        }
        
        public void Dispose()
        {
            if (_disposed)
                return;
            
            if (IsActive())
            {
                var stopEncodingResult = StopEncodingAsync().Result;
                if (!stopEncodingResult.Success)
                {
                    LckLog.LogError($"{nameof(LckEncoder)} was disposed while active, but failed to stop encoding: " + 
                                    stopEncodingResult.Message);
                }
            }

            CaptureErrorDispatcher = null;
            
            _disposed = true;
        }
    }
}
