using Liv.Lck.UI;
using System.Collections.Generic;
using System.Collections;
using Liv.Lck.DependencyInjection;
using UnityEngine;
using UnityEngine.Events;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// Manages the behavior of the tabs about the top of the tablet, and what
    /// action takes place when they're pressed.
    /// </summary>
    public class LckTopButtonsController : MonoBehaviour
    {
        [InjectLck]
        private ILckService _lckService;

        internal enum TopButtonPage
        {
            Null,
            Camera,
            Stream,
            Echo
        }

        [SerializeField]
        private GameObject _streamToggleGameObject;
        [SerializeField]
        private RectTransform _echoToggleRectTransform;

        [SerializeField]
        private LckNotificationController _notificationController;
        [SerializeField]
        private LckPhotoModeController _photoModeController;
        [SerializeField]
        private List<GameObject> _cameraPageButtons = new List<GameObject>();
        [SerializeField]
        private List<GameObject> _streamPageButtons = new List<GameObject>();
        [SerializeField]
        private List<GameObject> _echoPageButtons = new List<GameObject>();
        [Header("Top Button Events")]
        [SerializeField]
        private UnityEvent _onCameraPageOpened = new UnityEvent();
        [SerializeField]
        private UnityEvent _onStreamPageOpened = new UnityEvent();
        [SerializeField]
        private UnityEvent _onEchoPageOpened = new UnityEvent();

        private ILckTopButtons _topButtonsHelper;
        private TopButtonPage _currentPage = TopButtonPage.Null;
        bool _buttonsDisabled = false;

        internal TopButtonPage CurrentPage => _currentPage;

        private void Start()
        {
            // Streaming is not yet supported on Windows, so hide the Stream tab
            // and slide the Echo tab into its place to avoid a gap.
            if (Application.platform != RuntimePlatform.Android && Application.isEditor == false)
            {
                if (_streamToggleGameObject)
                {
                    if (_echoToggleRectTransform && _streamToggleGameObject.TryGetComponent<RectTransform>(out var streamRect))
                    {
                        _echoToggleRectTransform.anchoredPosition = streamRect.anchoredPosition;
                    }
                    _streamToggleGameObject.SetActive(false);
                }
            }

            _topButtonsHelper = GetComponent<ILckTopButtons>();
            ToggleCameraPage(true);
        }

        private void OnApplicationFocus(bool focus)
        {
            if (focus == true)
            {
                StartCoroutine(ResetAfterApplicationFocus());
            }
        }

        private IEnumerator ResetAfterApplicationFocus()
        {
            // skip a frame to wait for Lck Toggle OnApplicationFocus to end
            yield return 0;

            // if currently recording or streaming, make sure the top button visuals are still disabled 
            if (_buttonsDisabled)
            {
                SetTopButtonsIsDisabledState(true);
            }       
        }

        public void SetTopButtonsIsDisabledState(bool isDisabled)
        {
            _buttonsDisabled = isDisabled;

            if(_topButtonsHelper == null)
                GetComponent<ILckTopButtons>();
            
            if (isDisabled == true)
            {
                _topButtonsHelper?.HideButtons();
            }
            else
            {
                _topButtonsHelper?.ShowButtons();
            }
        }

        // called from Camera Toggle OnValueChanged unity event
        public void ToggleCameraPage(bool state)
        {
            if (_currentPage == TopButtonPage.Camera || state == false || _buttonsDisabled == true) return;

            DisableEchoIfActive();

            _currentPage = TopButtonPage.Camera;

            _notificationController.HideNotifications();
            _photoModeController.StopAndResetSequence();

            foreach (var button in _cameraPageButtons)
            {
                button.SetActive(true);
            }

            foreach (var button in _streamPageButtons)
            {
                button.SetActive(false);
            }

            foreach (var button in _echoPageButtons)
            {
                button.SetActive(false);
            }

            _lckService.SetActiveCaptureType(LckCaptureType.Recording);
            _onCameraPageOpened.Invoke();
        }

        // called from Stream Toggle OnValueChanged unity event
        public void ToggleStreamPage(bool state)
        {
            if (_currentPage == TopButtonPage.Stream || state == false || _buttonsDisabled == true) return;

            DisableEchoIfActive();

            _currentPage = TopButtonPage.Stream;

            _photoModeController.StopAndResetSequence();

            foreach (var button in _streamPageButtons)
            {
                button.SetActive(true);
            }

            foreach (var button in _cameraPageButtons)
            {
                button.SetActive(false);
            }

            foreach (var button in _echoPageButtons)
            {
                button.SetActive(false);
            }

            _lckService.SetActiveCaptureType(LckCaptureType.Streaming);
            _onStreamPageOpened.Invoke();
        }

        // called from Echo Toggle OnValueChanged unity event
        public void ToggleEchoPage(bool state)
        {
            if (_currentPage == TopButtonPage.Echo || state == false || _buttonsDisabled == true) return;

            _currentPage = TopButtonPage.Echo;

            _notificationController.HideNotifications();
            _photoModeController.StopAndResetSequence();

            foreach (var button in _echoPageButtons)
            {
                button.SetActive(true);
            }

            foreach (var button in _cameraPageButtons)
            {
                button.SetActive(false);
            }

            foreach (var button in _streamPageButtons)
            {
                button.SetActive(false);
            }

            _lckService.SetActiveCaptureType(LckCaptureType.Recording);

            var echoResult = _lckService.SetEchoEnabled(true);
            if (!echoResult.Success)
            {
                LckLog.LogError($"Failed to enable echo: {echoResult.Message}");
            }

            _notificationController.ShowNotification(NotificationType.EchoInfo);
            _onEchoPageOpened.Invoke();
        }

        private void DisableEchoIfActive()
        {
            var echoResult = _lckService.SetEchoEnabled(false);
            if (!echoResult.Success)
            {
                LckLog.LogError($"Failed to disable echo: {echoResult.Message}");
            }
        }

        public void SetCameraPageVisualsManually()
        {
            _topButtonsHelper.SetCameraPageVisualsManually();
        }
    }
}
