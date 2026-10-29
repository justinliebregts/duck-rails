using System;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace VoicePatrol.Scripts.VoicePatrol.Scripts.Runtime
{
    internal static class VoicePatrolPermissionManager
    {
        private const string LOG_PREFIX = "<color=#00D5FF>[VoicePatrol]</color>";

        public static bool HasMicrophonePermission()
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            return Permission.HasUserAuthorizedPermission(Permission.Microphone);
            #else
            return true;
            #endif
        }

        public static void RequestMicrophonePermission(MonoBehaviour context, Action onPermissionGranted, Action onPermissionDenied = null)
        {
            #if UNITY_ANDROID && !UNITY_EDITOR
            if (HasMicrophonePermission())
            {
                onPermissionGranted?.Invoke();
                return;
            }

            #if UNITY_2020_2_OR_NEWER
            var callbacks = new PermissionCallbacks();

            callbacks.PermissionGranted += _ =>
            {
                Debug.Log($"{LOG_PREFIX} Microphone permission granted");
                onPermissionGranted?.Invoke();
            };

            callbacks.PermissionDenied += _ =>
            {
                Debug.LogWarning($"{LOG_PREFIX} Microphone permission was denied. Voice moderation will be disabled. Please enable it in device settings if you wish to use this feature.");
                onPermissionDenied?.Invoke();
            };

            callbacks.PermissionDeniedAndDontAskAgain += _ =>
            {
                Debug.LogWarning($"{LOG_PREFIX} Microphone permission was permanently denied. Please enable it in device settings for voice moderation functionality.");
                onPermissionDenied?.Invoke();
            };

            Permission.RequestUserPermission(Permission.Microphone, callbacks);
            #else
            Permission.RequestUserPermission(Permission.Microphone);
            context.StartCoroutine(WaitForPermissionResponse(onPermissionGranted, onPermissionDenied));
            #endif
            #else
            onPermissionGranted?.Invoke();
            #endif
        }

        #if UNITY_ANDROID && !UNITY_EDITOR && !UNITY_2020_2_OR_NEWER
        private static IEnumerator WaitForPermissionResponse(Action onPermissionGranted, Action onPermissionDenied)
        {
            // Wait for a frame to allow the permission dialog to appear
            yield return new WaitForEndOfFrame();

            // Check for permission after the dialog should have been answered
            if (HasMicrophonePermission())
            {
                Debug.Log($"{LOG_PREFIX} Microphone permission granted");
                onPermissionGranted?.Invoke();
            }
            else
            {
                Debug.LogWarning($"{LOG_PREFIX} Microphone permission denied. Voice moderation will be disabled.");
                onPermissionDenied?.Invoke();
            }
        }
        #endif
    }
}
