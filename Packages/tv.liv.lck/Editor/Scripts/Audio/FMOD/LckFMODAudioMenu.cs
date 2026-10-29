// ReSharper disable InconsistentNaming (allow underscores in enum names to represent FMOD version numbers)

using UnityEditor;
using UnityEngine;

namespace Liv.Lck.Audio.FMOD
{
    internal static class LckFMODAudioMenu
    {
        private const string MenuPath = LckAudioMenu.MenuPath + "FMOD/";
        
        #region Configure Project
        private const string ConfigureForFMOD2_03AndAboveMenuPath = MenuPath + "Configure Project for FMOD 2.03+";
        [MenuItem(ConfigureForFMOD2_03AndAboveMenuPath, false, 0)]
        public static void ConfigureForFMOD2_03AndAbove()
        {
            VerifyConfigurationShouldContinue(out var shouldContinue);
            if (!shouldContinue)
            {
                DisplayCancelledConfigurationDialog();
                return;
            }
            
            var fmodOptions = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            fmodOptions.ShouldCapture = true;
            fmodOptions.Compatability = LckFMODCompatabilityType.Latest;
            
            LckAudioConfigurationUtils.FMODConfigurer.Configure(fmodOptions);
            Debug.Log("Configured LCK for FMOD (2.03+) audio capture");
        }

        private const string ConfigureForFMODPre2_03MenuPath = MenuPath + "Configure Project for FMOD <2.03";
        [MenuItem(ConfigureForFMODPre2_03MenuPath, false, 1)]
        public static void ConfigureForFMODPre2_03()
        {
            VerifyConfigurationShouldContinue(out var shouldContinue);
            if (!shouldContinue)
            {
                DisplayCancelledConfigurationDialog();
                return;
            }

            var fmodOptions = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            fmodOptions.ShouldCapture = true;
            fmodOptions.Compatability = LckFMODCompatabilityType.Pre2_03;
            
            LckAudioConfigurationUtils.FMODConfigurer.Configure(fmodOptions);
            Debug.Log("Configured LCK for FMOD (<2.03) audio capture");
        }

        private static void DisplayCancelledConfigurationDialog()
        {
            const string cancelledMsg = "Cancelled FMOD audio capture configuration.";
            EditorUtility.DisplayDialog("FMOD Audio Capture Configuration", cancelledMsg, "OK");
            Debug.Log(cancelledMsg);
        }
        
        private static void DisplaySwapFromWwiseToFMODDialog(out bool userSelectedYes)
        {
            var userChoiceIndex = EditorUtility.DisplayDialogComplex("Cannot Configure FMOD Audio Capture",
                "LCK cannot currently capture FMOD and Wwise audio at the same time. " +
                "Do you want to swap from Wwise to FMOD audio capture?",
                "Yes",
                "No",
                null);

            const int yesIndex = 0;
            userSelectedYes = userChoiceIndex == yesIndex;
        }
        
        private static void VerifyConfigurationShouldContinue(out bool shouldContinue)
        {
            // If Wwise is already configured, display a dialog to confirm whether to swap from Wwise to FMOD
            var wwiseOptions = LckAudioConfigurationUtils.WwiseConfigurer.GetCurrentOptions();
            if (wwiseOptions.ShouldCapture)
            {
                DisplaySwapFromWwiseToFMODDialog(out var shouldSwapFromWwiseToFmod);
                if (!shouldSwapFromWwiseToFmod)
                {
                    shouldContinue = false;
                    return;
                }

                // Swap from Wwise to FMOD audio capture (disable Wwise audio capture and continue)
                wwiseOptions.ShouldCapture = false;
                LckAudioConfigurationUtils.WwiseConfigurer.Configure(wwiseOptions, false);
            }
            
            shouldContinue = true;
        }
        #endregion
        
        #region Combined Unity Audio
        private const string CombineWithUnityAudioMenuPath = MenuPath + "Combine with Unity Audio";
        [MenuItem(CombineWithUnityAudioMenuPath, false, 100)]
        public static void ToggleCombineWithUnityAudio()
        {
            var options = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            options.CombineWithUnityAudio = !options.CombineWithUnityAudio;
            
            LckAudioConfigurationUtils.FMODConfigurer.Configure(options);
            
            Debug.Log(options.CombineWithUnityAudio
                ? "Configured LCK to combine FMOD audio with Unity audio"
                : "Configured LCK to capture FMOD audio only, excluding Unity audio");
        }
        
        [MenuItem(CombineWithUnityAudioMenuPath, true)]
        public static bool CombineWithUnityAudio_Validate()
        {
            var options = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            Menu.SetChecked(CombineWithUnityAudioMenuPath, options.ShouldCapture && options.CombineWithUnityAudio);
            return options.ShouldCapture;
        }
        #endregion
        
        #region Disable FMOD Capture
        private const string DisableFMODCaptureMenuPath = MenuPath + "Disable FMOD Audio Capture for Project";
        [MenuItem(DisableFMODCaptureMenuPath, false, 200)]
        public static void DisableFMODAudioCapture()
        {
            var options = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            options.ShouldCapture = false;
            
            LckAudioConfigurationUtils.FMODConfigurer.Configure(options);
            Debug.Log("Disabled FMOD audio capture in LCK");
        }
        
        [MenuItem(DisableFMODCaptureMenuPath, true)]
        public static bool DisableFMODAudioCapture_Validate()
        {
            var options = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            return options.ShouldCapture;
        }
        #endregion
    }
}
