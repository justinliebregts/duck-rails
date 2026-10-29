using Liv.Lck.Settings;
using Liv.Lck.Tablet;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Android;

namespace Liv.Lck.UI
{
    /// <summary>
    /// Disables the Mic LckToggle visuals if no mic permission is given, and restores the visuals to default when user has given permission. 
    /// </summary>
    public class LckMicTogglePermissionHelper : MonoBehaviour
    {
        [SerializeField]
        private LCKCameraController _controller;
        [SerializeField]
        private LckButtonColors _noMicPermissionColors;
        [SerializeField]
        private Sprite _noMicPermissionIcon;
        [SerializeField]
        private LckToggle _micLckToggle;
        [SerializeField]
        private Toggle _micToggle;
        [SerializeField]
        private Image _micToggleIcon;

        // using static here to avoid tablet getting Destroyed and value getting reset
        private static int _permissionAskCount = 0;
        private bool _hasMicPermission = true;

        void Start()
        {
            if (Application.platform != RuntimePlatform.Android || Application.isEditor == true || LckSettings.Instance.MicPermissionType == LckSettings.MicPermissionAskType.NeverAskFromLck)
            {
                _controller.ToggleMicrophoneRecording(true);
                return;
            }

            if (Application.platform == RuntimePlatform.Android && Application.isEditor == false)
            {
                if (Permission.HasUserAuthorizedPermission(Permission.Microphone) == false)
                {
                    _hasMicPermission = false;
                    SetMicPermissionOffVisuals();
                    _controller.ToggleMicrophoneRecording(false);

                    switch (LckSettings.Instance.MicPermissionType)
                    {
                        case LckSettings.MicPermissionAskType.OnAppStartup:
                            _micLckToggle.SetDisabledState();
                            break;
                        case LckSettings.MicPermissionAskType.OnTabletSpawn:
                            _micLckToggle.SetDisabledState();
                            if (UserSelectedDontShowAgain() == false)
                            {
                                CheckForMicPermission();
                            }
                            break;
                        case LckSettings.MicPermissionAskType.OnMicUnmute:
                            _micToggle.onValueChanged.AddListener(CheckForMicPermission);
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    _controller.ToggleMicrophoneRecording(true);
                }
            }
        }

        private void CheckForMicPermission(bool toggleValue = true)
        {
            if (Application.platform == RuntimePlatform.Android
                && Application.isEditor == false 
                && Permission.HasUserAuthorizedPermission(Permission.Microphone) == false)
            {

                if (UserSelectedDontShowAgain() == true)
                {
                    if (_micToggle && LckSettings.Instance.MicPermissionType == LckSettings.MicPermissionAskType.OnMicUnmute)
                    {
                        _micLckToggle.SetDisabledState();
                        LCKCameraController.ColliderButtonsInUse = false;
                        _micToggle.onValueChanged.RemoveListener(CheckForMicPermission);
                    }
                    return;
                }

                _permissionAskCount++;

                var callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += PermissionCallbacks_PermissionGranted;
                callbacks.PermissionDenied += PermissionCallbacks_PermissionDenied;
                LckLog.Log("Requesting Microphone Permission");
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
            }
        }

        // Called if the user selects 'Allow'
        internal void PermissionCallbacks_PermissionGranted(string permissionName)
        {
            _hasMicPermission = true;
            LckLog.Log("Microphone Permission Granted");
            SetMicPermissionOnVisuals();
            _controller.ToggleMicrophoneRecording(true);
            _micLckToggle.RestoreToggleState();

            if (_micToggle && LckSettings.Instance.MicPermissionType == LckSettings.MicPermissionAskType.OnMicUnmute)
            {
                _micToggle.onValueChanged.RemoveListener(CheckForMicPermission);
            }
        }

        // Called if the user selects 'Deny'
        internal void PermissionCallbacks_PermissionDenied(string permissionName)
        {
            _hasMicPermission = false;
            LckLog.Log("Microphone Permission Denied");
            SetMicPermissionOffVisuals();
            _controller.ToggleMicrophoneRecording(false);

            if (LckSettings.Instance.MicPermissionType != LckSettings.MicPermissionAskType.OnMicUnmute)
            {
                _micLckToggle.SetDisabledState();
            }
        }

        private bool UserSelectedDontShowAgain()
        {
#if UNITY_2023_1_OR_NEWER
            return _permissionAskCount >= 1 && Permission.ShouldShowRequestPermissionRationale(Permission.Microphone) == false;
#else
            return _permissionAskCount >= 2;
#endif
        }

        public void SetMicPermissionOnVisuals()
        {
            _micLckToggle.RestoreDefaultColors();
            _micLckToggle.RestoreDefaultIcons();
            _micLckToggle.SetToggleVisualsOn();
            SetToggleIconAlpha(1);
        }

        public void SetMicPermissionOffVisuals()
        {
            _micLckToggle.SetCustomColors(_noMicPermissionColors, _noMicPermissionColors);
            _micLckToggle.SetCustomIcons(_noMicPermissionIcon, _noMicPermissionIcon);
            SetToggleIconAlpha(0.2f);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus == true && LckSettings.Instance.MicPermissionType != LckSettings.MicPermissionAskType.NeverAskFromLck)
            {
                // If mic permissions were previously denied but user has now gone in settings and allowed it, update visuals to ON / OFF
                if (_hasMicPermission == false && Permission.HasUserAuthorizedPermission(Permission.Microphone) == true)
                {
                    LckLog.Log("User allowed mic permission from settings");
                    _controller.ToggleMicrophoneRecording(true);
                    SetMicPermissionOnVisuals();
                    _micLckToggle.RestoreToggleState();
                    _hasMicPermission = true;
                }
                else if (_hasMicPermission == true && Permission.HasUserAuthorizedPermission(Permission.Microphone) == false)
                {
                    LckLog.Log("User disabled mic permission from settings");
                    _controller.ToggleMicrophoneRecording(false);
                    SetMicPermissionOffVisuals();
                    _micLckToggle.SetDisabledState();
                    _hasMicPermission = false;
                }
            }    
        }

        private void SetToggleIconAlpha(float alpha)
        {
            Color newColor = _micToggleIcon.color;
            newColor.a = alpha;
            _micToggleIcon.color = newColor;
        }
    }
}
