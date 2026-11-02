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
            Stream
        }

        [SerializeField]
        private GameObject _topButtonsControllerGameObject;

        [SerializeField]
        private LckNotificationController _notificationController;
        [SerializeField]
        private LckPhotoModeController _photoModeController;
        [SerializeField]
        private List<GameObject> _cameraPageButtons = new List<GameObject>();
        [SerializeField]
        private List<GameObject> _streamPageButtons = new List<GameObject>();
        [Header("Top Button Events")]
        [SerializeField]
        private UnityEvent _onCameraPageOpened = new UnityEvent();
        [SerializeField]
        private UnityEvent _onStreamPageOpened = new UnityEvent();

        private ILckTopButtons _topButtonsHelper;
        private TopButtonPage _currentPage = TopButtonPage.Null;
        bool _buttonsDisabled = false;

        internal TopButtonPage CurrentPage => _currentPage;

        private void Start()
        {
            if (Application.platform != RuntimePlatform.Android && Application.isEditor == false)
            {
                if (_topButtonsControllerGameObject)
                {
                    _topButtonsControllerGameObject.SetActive(false);
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

            _lckService.SetActiveCaptureType(LckCaptureType.Recording);
            _onCameraPageOpened.Invoke();
        }

        // called from Stream Toggle OnValueChanged unity event
        public void ToggleStreamPage(bool state)
        {
            if (_currentPage == TopButtonPage.Stream || state == false || _buttonsDisabled == true) return;

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
            
            _lckService.SetActiveCaptureType(LckCaptureType.Streaming);
            _onStreamPageOpened.Invoke();
        }

        public void SetCameraPageVisualsManually()
        {
            _topButtonsHelper.SetCameraPageVisualsManually();
        }
    }
}
