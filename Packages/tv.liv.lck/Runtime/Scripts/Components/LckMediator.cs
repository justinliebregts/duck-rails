using System;
using System.Collections.Generic;

namespace Liv.Lck
{
    public static class LckMediator
    {
        private static readonly Dictionary<string, ILckCamera> _cameras = new Dictionary<string, ILckCamera>();
        private static readonly Dictionary<string, ILckMonitor> _monitors = new Dictionary<string, ILckMonitor>();

        public static event Action<ILckCamera> CameraRegistered;
        public static event Action<ILckCamera> CameraUnregistered;
        public static event Action<ILckMonitor> MonitorRegistered;
        public static event Action<ILckMonitor> MonitorUnregistered;
        public static event Action<string, string> MonitorToCameraAssignment;
        public static void RegisterCamera(ILckCamera camera)
        {
            if (!_cameras.ContainsKey(camera.CameraId))
            {
                _cameras.Add(camera.CameraId, camera);
                CameraRegistered?.Invoke(camera);
                LckLog.Log($"{nameof(ILckCamera)} registered (id=\"{camera.CameraId}\")");
            }
            else
            {
                LckLog.LogWarning($"{nameof(RegisterCamera)} called with already registered camera id: \"{camera.CameraId}\"");
            }
        }

        public static void UnregisterCamera(ILckCamera camera)
        {
            if (_cameras.ContainsKey(camera.CameraId))
            {
                _cameras.Remove(camera.CameraId);
                CameraUnregistered?.Invoke(camera);
                LckLog.Log($"{nameof(ILckCamera)} unregistered (id=\"{camera.CameraId}\")");
            }
            else
            {
                LckLog.LogWarning($"{nameof(UnregisterCamera)} called with unknown camera id: \"{camera.CameraId}\"");
            }
        }

        public static void RegisterMonitor(ILckMonitor monitor)
        {
            if (!_monitors.ContainsKey(monitor.MonitorId))
            {
                _monitors.Add(monitor.MonitorId, monitor);
                MonitorRegistered?.Invoke(monitor);
                LckLog.Log($"{nameof(ILckMonitor)} registered (id=\"{monitor.MonitorId}\")");
            }
            else
            {
                LckLog.LogWarning($"{nameof(RegisterMonitor)} called with already registered monitor id: \"{monitor.MonitorId}\"");
            }
        }

        public static void UnregisterMonitor(ILckMonitor monitor)
        {
            if (_monitors.ContainsKey(monitor.MonitorId))
            {
                _monitors.Remove(monitor.MonitorId);
                MonitorUnregistered?.Invoke(monitor);
                LckLog.Log($"{nameof(ILckMonitor)} unregistered (id=\"{monitor.MonitorId}\")");
            }
            else
            {
                LckLog.LogWarning($"{nameof(UnregisterMonitor)} called with unknown monitor id: \"{monitor.MonitorId}\"");
            }
        }

        public static ILckCamera GetCameraById(string id)
        {
            _cameras.TryGetValue(id, out ILckCamera camera);
            return camera;
        }

        public static ILckMonitor GetMonitorById(string id)
        {
            _monitors.TryGetValue(id, out ILckMonitor monitor);
            return monitor;
        }

        public static IEnumerable<ILckCamera> GetCameras()
        {
            return _cameras.Values;
        }

        public static IEnumerable<ILckMonitor> GetMonitors()
        {
            return _monitors.Values;
        }
        
        public static void NotifyMixerAboutMonitorForCamera(string monitorId, string cameraId)
        {
            MonitorToCameraAssignment?.Invoke(monitorId, cameraId);
        }
    }
}
