using System;
using System.Linq;
using Liv.Lck.DependencyInjection;
using UnityEditor;
using UnityEngine;

namespace Liv.Lck
{
    public class LckDebugWindow : EditorWindow
    {
        private Vector2 _scrollPosition;

        [MenuItem("LCK/Debug/Debug Window")]
        private static void Init()
        {
            LckDebugWindow window = (LckDebugWindow)GetWindow(typeof(LckDebugWindow));
            window.titleContent = new GUIContent("LCK Debug");
            window.Show();
        }

        private void OnGUI()
        {
            GUI.enabled = Application.isPlaying;

            if (!GUI.enabled)
            {
                GUILayout.Label("This tool is only available in play mode.", EditorStyles.centeredGreyMiniLabel);
                return;
            }
            
            _scrollPosition = GUILayout.BeginScrollView(_scrollPosition);

            DrawDependencyInjectionInfo();

            GUILayout.Space(10);
            
            DrawLckServiceInfo();
            
            GUILayout.EndScrollView();
        }

        private void DrawDependencyInjectionInfo()
        {
            GUILayout.Label("Dependency Injection Status", EditorStyles.boldLabel);
            
            var registrations = LckDiRegistry.Instance.GetRegistrations();

            if (registrations == null || registrations.Count == 0)
            {
                GUILayout.Label("No services registered.");
                return;
            }

            foreach (var reg in registrations.Values)
            {
                EditorGUILayout.BeginVertical("box");
                GUILayout.Label($"Service: {reg.ServiceType.Name}", EditorStyles.boldLabel);
                if(reg.ImplementationType != null) GUILayout.Label($"  Implementation: {reg.ImplementationType.Name}");
                if(reg.ForwardToServiceType != null) GUILayout.Label($"  Forwards To: {reg.ForwardToServiceType.Name}");
                GUILayout.Label($"  Lifetime: {reg.Lifetime}");
                GUILayout.Label($"  Status: {(reg.Instance != null ? "Instantiated" : "Not Instantiated")}");
                EditorGUILayout.EndVertical();
                GUILayout.Space(5);
            }
        }

        private void DrawLckServiceInfo()
        {
            GUILayout.Label("LCK Service", EditorStyles.boldLabel);
            
            var service = LckDiRegistry.Instance.GetService<ILckService>();

            if (service == null)
            {
                GUILayout.Label("Service is not available or could not be resolved.");
                return;
            }

            var lckService = service as LckService;
            if (lckService == null)
            {
                GUILayout.Label("Resolved service is not of type LckService, cannot display details.");
                return;
            }
            
            GUILayout.Label("Service is available", EditorStyles.miniLabel);
            GUILayout.Space(10);
            DrawTrackInfo(lckService);
            GUILayout.Space(10);
            DrawIsCapturing(lckService);
            DrawIsRecording(lckService);
        }

        private static void DrawTrackInfo(LckService service)
        {
            var getDescriptor = service.GetDescriptor();
            GUILayout.Label("Track Info", EditorStyles.boldLabel);
            if(getDescriptor.Success)
            {
                var descriptor = getDescriptor.Result;
                GUILayout.Label($"Resolution: {descriptor.cameraTrackDescriptor.CameraResolutionDescriptor.Width}x{descriptor.cameraTrackDescriptor.CameraResolutionDescriptor.Height}");
                GUILayout.Label($"Framerate: {descriptor.cameraTrackDescriptor.Framerate}");
                GUILayout.Label($"Bitrate: {descriptor.cameraTrackDescriptor.Bitrate} ({descriptor.cameraTrackDescriptor.Bitrate / 1048576}mbit)");
            }
            else
            {
                GUILayout.Label("Error: " + getDescriptor.Error);
                GUILayout.Label("Message: " + getDescriptor.Message);
            }
        }

        private static void DrawIsCapturing(LckService service)
        {
            GUILayout.Label("Is Capturing", EditorStyles.boldLabel);
            var getIsCapturing = service.IsCapturing();
            if(getIsCapturing.Success)
            {
                GUILayout.Label(getIsCapturing.Result.ToString());
            }
            else
            {
                GUILayout.Label("Error: " + getIsCapturing.Error);
                GUILayout.Label("Message: " + getIsCapturing.Message);
            }
        }

        private static void DrawIsRecording(LckService service)
        {
            GUILayout.Label("Is Recording", EditorStyles.boldLabel);
            var getIsRecording = service.IsRecording();
            if(getIsRecording.Success)
            {
                GUILayout.Label(getIsRecording.Result.ToString());
            }
            else
            {
                GUILayout.Label("Error: " + getIsRecording.Error);
                GUILayout.Label("Message: " + getIsRecording.Message);
            }
        }
    }
}
