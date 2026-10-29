using UnityEngine;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;

namespace Liv.Lck
{
    internal class LckPreviewer : ILckPreviewer
    {
        private readonly ILckVideoTextureProvider _videoTextureProvider;
        private readonly ILckEventBus _eventBus;
        public bool IsPreviewActive { get; set; } = true;

        [Preserve]
        public LckPreviewer(ILckVideoTextureProvider videoTextureProvider, ILckEventBus eventBus)
        {
            _videoTextureProvider = videoTextureProvider;
            _eventBus = eventBus;
            
            _eventBus.AddListener<ActiveCameraTrackTextureChangedEvent>(OnCameraTrackTextureChanged);
            LckMediator.MonitorRegistered += OnMonitorRegistered;
            LckMediator.MonitorUnregistered += OnMonitorUnregistered;
        }

        private void SetMonitorRenderTexture(ILckMonitor monitor)
        {
            var cameraTrackTexture = _videoTextureProvider.CameraTrackTexture;
            if (cameraTrackTexture == null)
            {
                LckLog.LogWarning("LCK Camera track texture not found.");
                return;
            }

            if (monitor == null)
            {
                LckLog.LogWarning("LCK Monitor not found.");
                return;
            }
            
            monitor.SetRenderTexture(cameraTrackTexture);
        }
        
        private void OnMonitorRegistered(ILckMonitor monitor)
        {
            SetMonitorRenderTexture(monitor);
        }

        private static void OnMonitorUnregistered(ILckMonitor monitor)
        {
            monitor?.SetRenderTexture(null);
        }
        
        private void SetMonitorTextureForAllMonitors()
        {
            foreach (var monitor in LckMediator.GetMonitors())
            {
                SetMonitorRenderTexture(monitor);
            }
        }
        
        private void OnCameraTrackTextureChanged(ActiveCameraTrackTextureChangedEvent activeCameraTrackTextureChangedEvent)
        {
            SetMonitorTextureForAllMonitors();
        }

        public void Dispose()
        {
            _eventBus?.RemoveListener<ActiveCameraTrackTextureChangedEvent>(OnCameraTrackTextureChanged);

            LckMediator.MonitorRegistered -= OnMonitorRegistered;
            LckMediator.MonitorUnregistered -= OnMonitorUnregistered;
        }
    }
}

