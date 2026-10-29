using System;
using System.Collections.Generic;
using Liv.Lck.DependencyInjection;
using UnityEngine;
using UnityEngine.Events;

namespace Liv.Lck.UI
{
    public class LckQualitySelector : MonoBehaviour
    {
        [InjectLck]
        private ILckService _lckService;
        
        private QualityOption _currentQualityOption;
        private int _currentQualityIndex = 0;
        private List<QualityOption> _qualityOptions = new List<QualityOption>();

        #region Obsolete
        [Obsolete("Only provides recording parameters for the selected quality option, and does not affect " + 
                  " streaming - Use OnQualityOptionChanged instead")]
        public Action<CameraTrackDescriptor> OnQualityOptionSelected;
        #endregion
        
        public Action<QualityOption> OnQualityOptionChanged;
        [SerializeField]
        private UnityEvent<string> _onQualityOptionChanged;
        [SerializeField]
        private UnityEvent<bool> _onSetQualityButtonIsDisabledState;

        public void InitializeOptions(List<QualityOption> qualityOptions)
        {
            _qualityOptions = qualityOptions;

            var defaultOption = _qualityOptions.FindIndex(x => x.IsDefault);

            if (defaultOption != -1)
            {
                _currentQualityIndex = defaultOption;
            }
            else
            {
                _currentQualityIndex = 0;
            }

            UpdateCurrentTrackDescriptor(_currentQualityIndex);
        }

        public void GoToNextOption()
        {
            if (_currentQualityIndex == _qualityOptions.Count - 1)
            {
                _currentQualityIndex = 0;
            }
            else
            {
                _currentQualityIndex++;
            }

            UpdateCurrentTrackDescriptor(_currentQualityIndex);

        }

        private void UpdateCurrentTrackDescriptor(int index)
        {
            if (_qualityOptions.Count > index)
            {
                _currentQualityOption = _qualityOptions[_currentQualityIndex];
                
                // For backwards compatability, invoke old OnQualityOptionSelected event which only affects recording
#pragma warning disable CS0618 // Type or member is obsolete
                OnQualityOptionSelected?.Invoke(_currentQualityOption.RecordingCameraTrackDescriptor);
#pragma warning restore CS0618 // Type or member is obsolete
                
                OnQualityOptionChanged?.Invoke(_currentQualityOption);

                //NOTE: not great having another event but using this for GT tablet button
                _onQualityOptionChanged.Invoke(_currentQualityOption.Name);
            }
        }

        public void SetQualityButtonIsDisabledState(bool state)
        {
            //NOTE: also using this for GT tablet compatibility
            _onSetQualityButtonIsDisabledState.Invoke(state);
        }
    }       
}
