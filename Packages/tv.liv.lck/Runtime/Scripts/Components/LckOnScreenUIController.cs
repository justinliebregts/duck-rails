using Liv.Lck.UI;
using System.Collections.Generic;
using Liv.Lck.DependencyInjection;
using UnityEngine;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// Controls the state of the on-screen elements of the tablet (Photo Mode, Selfie mode etc.)
    /// </summary>
    public class LckOnScreenUIController : MonoBehaviour
    {
        [InjectLck]
        private ILckService _lckService;

        [SerializeField]
        private List<GameObject> _allOnscreenUI = new List<GameObject>();

        private void OnEnable()
        {
            _lckService.OnRecordingStarted += OnRecordingStarted;
        }

        private void OnDisable()
        {
            _lckService.OnRecordingStarted -= OnRecordingStarted;

            SetAllOnscreenButtonsState(true);
        }

        private void OnRecordingStarted(LckResult result)
        {
            if (!result.Success)
                return;
            
            SetAllOnscreenButtonsState(true);
        }

        public void OnNotificationStarted()
        {
            SetAllOnscreenButtonsState(false);
        }

        public void OnNotificationEnded()
        {
            SetAllOnscreenButtonsState(true);
            SetAllOnscreenButtonsToDefaultVisual(_allOnscreenUI);
        }

        private void SetAllOnscreenButtonsState(bool state)
        {
            SetObjectsState(_allOnscreenUI, state);
        }

        private void SetObjectsState(List<GameObject> objectList, bool state)
        {
            foreach (GameObject gameObj in objectList)
            {
                gameObj.SetActive(state);
            }
        }

        private void SetAllOnscreenButtonsToDefaultVisual(List<GameObject> objectList)
        {
            foreach (GameObject gameObj in objectList)
            {
                if (gameObj.TryGetComponent<LckScreenButton>(out LckScreenButton screenButton))
                {
                    screenButton.SetDefaultButtonColors();
                }
            }
        }    
    }
}
