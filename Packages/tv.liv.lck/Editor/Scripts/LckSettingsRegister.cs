using UnityEditor;
using UnityEngine;

namespace Liv.Lck.Settings
{
    /// <summary>
    /// Menu items for LCK.
    /// Settings pages are registered via SettingsProvider attributes in the Pages folder.
    /// </summary>
    public static class LckSettingsMenu
    {
        public const string DocumentationURL = "https://liv.mintlify.app/";

        [MenuItem("LCK/Overview", priority = 0)]
        public static void OpenOverview()
        {
            SettingsService.OpenProjectSettings("Project/LCK/1 Overview");
        }

        [MenuItem("LCK/Setup", priority = 1)]
        public static void OpenSetup()
        {
            SettingsService.OpenProjectSettings("Project/LCK/2 Setup");
        }

        [MenuItem("LCK/Configuration", priority = 2)]
        public static void OpenConfiguration()
        {
            SettingsService.OpenProjectSettings("Project/LCK/3 Configuration");
        }

        [MenuItem("LCK/Validation", priority = 3)]
        public static void OpenValidation()
        {
            SettingsService.OpenProjectSettings("Project/LCK/4 Validation");
        }

        [MenuItem("LCK/Updates", priority = 4)]
        public static void OpenUpdates()
        {
            SettingsService.OpenProjectSettings("Project/LCK/5 Updates");
        }

        [MenuItem("LCK/Documentation", priority = 100)]
        public static void OpenDocumentation()
        {
            Application.OpenURL(DocumentationURL);
        }
    }
}
