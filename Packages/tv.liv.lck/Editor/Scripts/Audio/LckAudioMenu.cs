using Liv.Lck.Util;
using UnityEditor;

namespace Liv.Lck.Audio
{
    internal static class LckAudioMenu
    {
        public const string MenuPath = "LCK/Audio/";
        
        private const string ResetMenuPath = MenuPath + "Reset LCK Audio Configuration to Defaults";
        [MenuItem(ResetMenuPath)]
        public static void ResetAudioConfigurationToDefaults()
        {
            ScriptingDefineUtils.RemoveDefines(LckAudioScriptingDefines.GetAllLckAudioScriptingDefines());
            EditorUtility.DisplayDialog(
                "LCK Audio Configuration Reset",
                "LCK audio configuration has been reset to defaults.",
                "OK");
        }
        
        [MenuItem(MenuPath + "Reset LCK Audio Configuration to Defaults", true)]
        public static bool ResetLckAudioConfigurationToDefaults_Validate()
        {
            return ScriptingDefineUtils.HasAny(LckAudioScriptingDefines.GetAllLckAudioScriptingDefines());
        }

        private const string ValidateMenuPath = MenuPath + "Validate Audio Configuration";
        [MenuItem(ValidateMenuPath)]
        public static void ValidateAudioConfiguration()
        {
            LckAudioConfigurationValidator.ValidateAndShowDialog();
        }
    }
}
