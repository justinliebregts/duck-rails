using Liv.Lck.Core;
using Liv.Lck.DependencyInjection;
using Liv.Lck.Encoding;
using Liv.Lck.Recorder;
using Liv.Lck.Streaming;
using Liv.NativeAudioBridge;
using System;
using System.Collections.Generic;
using System.IO;
using Liv.Lck.Core.Cosmetics;
using Liv.Lck.Core.Serialization;
using Liv.Lck.ErrorHandling;
using Liv.Lck.Telemetry;

#if UNITY_ANDROID && !UNITY_EDITOR
using Liv.NativeAudioBridge.Android;
#endif

using UnityEngine;

namespace Liv.Lck
{
    [DefaultExecutionOrder(-900)]
    public class LckServiceInitializer : MonoBehaviour
    {
        [SerializeReference]
        private LckQualityConfig _qualityConfig;

        private void Awake()
        {
            bool hasLckService = LckDiContainer.Instance.HasService<ILckService>();
            if(hasLckService)
                return;

            var lckDiContainer = LckDiContainer.Instance;

            ConfigureServices(lckDiContainer, _qualityConfig);
        }

        /// <summary>
        /// Registers the dependencies required for the Lck Service with the LCK DI container.
        /// This should be the called before any access of the Lck Service.
        /// </summary>
        /// <param name="container">The DI container, usually provided by 'LckDiContainer.Instance'.</param>
        /// <param name="qualityConfig">The QualityConfig, by default supplied through an SO. </param>
        /// <param name="overrides">Enables custom implementations to be overwritten, while ensuring all
        /// required dependencies are provided.</param>
        /// <example>For most standard uses of LCK, you will not need to override its implementations.
        /// However, to do so, provide the override(s) as an action:</example>
        /// <code>
        /// [DefaultExecutionOrder(-900)] // Ensure this executes before the LckService is required
        /// public class LckCustomPhotoCaptureInitializer : MonoBehaviour
        /// {
        ///       [SerializeField]
        ///       private LckQualityConfig _qualityConfig;
        ///
        ///       private LckDiContainer _lckDiContainer;
        ///
        ///       private void OnEnable()
        ///       {
        ///          _lckDiContainer = LckDiContainer.Instance;
        ///          LckServiceInitializer.ConfigureServices(_lckDiContainer, _qualityConfig, container =>
        ///          {
        ///              container.AddSingleton<ILckCustomPhotoCapture, LckCustomPhotoCapture>();
        ///           });
        ///        }
        /// }
        /// </code>
        public static void ConfigureServices(
            LckDiContainer container,
            ILckQualityConfig qualityConfig,
            Action<LckDiContainer> overrides = null)
        {
            container.AddSingleton<ILckStreamer, NullLckStreamer>();

            LckModuleLoader.Configure(container);
            container.AddSingleton(qualityConfig);

            container.AddSingletonFactory<ILckCore>(provider => new LckCoreWrapper());
            container.AddSingleton<ILckCaptureErrorDispatcher, MainThreadCaptureErrorDispatcher>();
            container.AddSingleton<ILckSerializer, LckMsgPackSerializer>();
            container.AddSingleton<ILckTelemetryContextProvider, LckTelemetryContextProvider>();
            container.AddSingleton<ILckTelemetryClient, LckTelemetryClient>();
            container.AddSingleton<ILckEventBus, LckEventBus>();
            container.AddSingleton<ILckPhotoCapture, LckPhotoCapture>();
            container.AddSingleton<ILckNativeRecordingService, LckNativeRecordingService>();
            container.AddSingleton<ILckRecorder, LckRecorder>();
            container.AddSingleton<ILckEncoder, LckEncoder>();
            container.AddSingleton<ILckPreviewer, LckPreviewer>();
            container.AddSingleton<ILckStorageWatcher, LckStorageWatcher>();

            container.AddSingleton<ILckCosmeticsCoordinator, NullLckCosmeticsCoordinator>();

            container.AddSingleton<ILckVideoMixer, LckVideoMixer>();
            container.AddSingletonForward<ILckVideoTextureProvider, ILckVideoMixer>();
            container.AddSingletonForward<ILckActiveCameraConfigurer, ILckVideoMixer>();

            container.AddSingleton<ILckAudioMixer, LckAudioMixer>();
            container.AddSingleton<ILckOutputConfigurer, LckOutputConfigurer>();
            container.AddSingleton<ILckVideoCapturer, LckVideoCapturer>();
            container.AddSingleton<ILckEncodeLooper, LckEncodeLooper>();
            
            container.AddSingletonFactory<INativeAudioPlayer>(provider =>
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR
                return new NativeAudioPlayerWindows();
#elif UNITY_ANDROID
                return new NativeAudioPlayerAndroid();
#else
                throw new PlatformNotSupportedException("NativeAudioManager is not supported on this platform.");
#endif
            });

            container.AddSingleton<ILckService, LckService>();
            overrides?.Invoke(container);
            container.Build();
        }
    }
}
