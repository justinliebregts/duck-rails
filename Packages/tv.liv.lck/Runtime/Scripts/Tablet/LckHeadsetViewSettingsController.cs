using Liv.Lck.UI;
using UnityEngine;

namespace Liv.Lck.Tablet
{
    /// <summary>
    /// Controls the headset camera settings UI on the tablet.
    /// Uses LckChoiceButton instances for eye selection and crop mode.
    /// </summary>
    public class LckHeadsetViewSettingsController : MonoBehaviour
    {
        [SerializeField]
        private LckHeadsetCamera _headsetCamera;

        [Header("Choice Buttons")]
        [SerializeField]
        private LckChoiceButton _eyeChoice;
        [SerializeField]
        private LckChoiceButton _cropModeChoice;

        private void OnEnable()
        {
            if (_eyeChoice != null) _eyeChoice.OnSelectionChanged += OnEyeChanged;
            if (_cropModeChoice != null) _cropModeChoice.OnSelectionChanged += OnCropModeChanged;
            SyncVisuals();
        }

        private void OnDisable()
        {
            if (_eyeChoice != null) _eyeChoice.OnSelectionChanged -= OnEyeChanged;
            if (_cropModeChoice != null) _cropModeChoice.OnSelectionChanged -= OnCropModeChanged;
        }

        private void OnEyeChanged(int index)
        {
            if (_headsetCamera == null) return;
            _headsetCamera.Eye = index == 0 ? EyeSelection.Left : EyeSelection.Right;
        }

        private void OnCropModeChanged(int index)
        {
            if (_headsetCamera == null) return;
            _headsetCamera.CropMode = index == 0 ? HeadsetCropMode.Fit : HeadsetCropMode.ZoomFill;
        }

        private void SyncVisuals()
        {
            if (_headsetCamera == null) return;
            _eyeChoice?.SetSelectedIndex(_headsetCamera.Eye == EyeSelection.Left ? 0 : 1);
            _cropModeChoice?.SetSelectedIndex(_headsetCamera.CropMode == HeadsetCropMode.Fit ? 0 : 1);
        }
    }
}
