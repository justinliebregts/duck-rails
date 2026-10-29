using UnityEditor;
using UnityEngine;

namespace Liv.Lck.Audio.Wwise
{
    internal static class LckWwiseAudioMenu
    {
        private const string MenuPath = LckAudioMenu.MenuPath + "Wwise/";
        
        #region Configure Project
        private const string ConfigureForWwiseMenuPath = MenuPath + "Configure Project for Wwise";
        [MenuItem(ConfigureForWwiseMenuPath, false, 0)]
        public static void ConfigureForWwiseAudioCapture()
        {
            VerifyConfigurationShouldContinue(out var shouldContinue);
            if (!shouldContinue)
            {
                DisplayCancelledConfigurationDialog();
                return;
            }

            var wwiseOptions = LckAudioConfigurationUtils.WwiseConfigurer.GetCurrentOptions();
            wwiseOptions.ShouldCapture = true;

            LckAudioConfigurationUtils.WwiseConfigurer.Configure(wwiseOptions);
            Debug.Log("Configured LCK for Wwise audio capture");
        }
        
        private static void DisplayCancelledConfigurationDialog()
        {
            const string cancelledMsg = "Cancelled Wwise audio capture configuration.";
            EditorUtility.DisplayDialog("Wwise Audio Capture Configuration", cancelledMsg, "OK");
            Debug.Log(cancelledMsg);
        }
        
        private static void DisplaySwapFromFMODToWwiseDialog(out bool userSelectedYes)
        {
            var userChoiceIndex = EditorUtility.DisplayDialogComplex("Cannot Configure Wwise Audio Capture",
                "LCK cannot currently capture FMOD and Wwise audio at the same time. " +
                "Do you want to swap from FMOD to Wwise audio capture?",
                "Yes",
                "No",
                null);

            const int yesIndex = 0;
            userSelectedYes = userChoiceIndex == yesIndex;
        }
        
        private static void VerifyConfigurationShouldContinue(out bool shouldContinue)
        {
            // If FMOD is already configured, display a dialog to confirm whether to swap from FMOD to Wwise
            var fmodOptions = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            if (fmodOptions.ShouldCapture)
            {
                DisplaySwapFromFMODToWwiseDialog(out var shouldSwapFromFMODToWwise);
                if (!shouldSwapFromFMODToWwise)
                {
                    shouldContinue = false;
                    return;
                }

                // Swap from FMOD to Wwise audio capture (disable FMOD audio capture and continue)
                fmodOptions.ShouldCapture = false;
                LckAudioConfigurationUtils.FMODConfigurer.Configure(fmodOptions, false);
            }
            
            shouldContinue = true;
        }
        #endregion
        
        #region Disable Wwise Capture
        private const string DisableWwiseCaptureMenuPath = MenuPath + "Disable Wwise Audio Capture for Project";
        [MenuItem(DisableWwiseCaptureMenuPath, false, 200)]
        public static void DisableWwiseAudioCapture()
        {
            var options = LckAudioConfigurationUtils.WwiseConfigurer.GetCurrentOptions();
            options.ShouldCapture = false;
            
            LckAudioConfigurationUtils.WwiseConfigurer.Configure(options);
            Debug.Log("Disabled Wwise audio capture in LCK");
        }
        
        [MenuItem(DisableWwiseCaptureMenuPath, true)]
        public static bool DisableWwiseAudioCapture_Validate()
        {
            var options = LckAudioConfigurationUtils.WwiseConfigurer.GetCurrentOptions();
            return options.ShouldCapture;
        }
        #endregion
    }
}
