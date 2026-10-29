using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Liv.Lck.Audio
{
    public static class LckAudioConfigurationValidator
    {
        private const string ValidConfigurationMessage = "LCK audio configuration is valid.";
        private const string ValidationFailReasonPrefix = "\n  - ";
        
        public static bool Validate(out IList<string> failReasons)
        {
            failReasons = new List<string>();
            
            var fmodOptions = LckAudioConfigurationUtils.FMODConfigurer.GetCurrentOptions();
            var wwiseOptions = LckAudioConfigurationUtils.WwiseConfigurer.GetCurrentOptions();

            if (fmodOptions.ShouldCapture && wwiseOptions.ShouldCapture)
            {
                failReasons.Add("FMOD and Wwise audio capture cannot both be enabled at the same time. " +
                                "Please disable one of them.");
            }
            
            return !failReasons.Any();
        }
        
        public static void ValidateAndShowDialog(string titleOverride = null, string messagePrefix = null)
        {
            ValidateAndBuildMessage(out var isValid, out var validationMsg);
            LogValidationMessage(isValid, validationMsg);
            
            EditorUtility.DisplayDialog(
                titleOverride ?? "LCK Audio Configuration Validation",
                messagePrefix == null ? validationMsg : string.Join("\n", messagePrefix, validationMsg),
                "OK");
        }

        public static void ValidateAndLog()
        {
            ValidateAndBuildMessage(out var isValid, out var validationMsg);
            LogValidationMessage(isValid, validationMsg);
        }

        private static void LogValidationMessage(bool isValid, string validationMessage)
        {
            if (isValid)
                Debug.Log(validationMessage);
            else
                Debug.LogError(validationMessage);
        }

        private static void ValidateAndBuildMessage(out bool isValid, out string validationMsg)
        {
            isValid = Validate(out var failReasons);
            validationMsg = isValid ? 
                ValidConfigurationMessage : 
                BuildValidationFailureMessage(failReasons);
        }

        private static string BuildValidationFailureMessage(IEnumerable<string> failReasons)
        {
            return $"LCK audio configuration is invalid:{ValidationFailReasonPrefix}" +
                   $"{string.Join(ValidationFailReasonPrefix, failReasons)}";
        }
    }
}
