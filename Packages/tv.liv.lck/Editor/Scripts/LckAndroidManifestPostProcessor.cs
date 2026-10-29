using UnityEngine;
using UnityEditor;
using UnityEditor.Android;
using System.IO;
using Liv.Lck.Settings;
using System.Text.RegularExpressions;
using System;

#if LCK_UNITY_XR_HAS_FORCE_REMOVE_INTERNET_PERMISSION
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.MetaQuestSupport;
#endif

namespace Liv.Lck
{
    public class LckAndroidManifestPostProcessor : IPostGenerateGradleAndroidProject
    {
        private const string CONTROL_CENTER_STRING = "<package android:name=\"tv.liv.controlcenter\" />";
        public int callbackOrder => 0;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            if (LckSettings.Instance.AddInternetPermissionsToAndroidManifest)
            {
                AddInternetPermissions(path);
            }
            else
            {
                Debug.Log("LCK Not adding Internet permissions to AndroidManifest.xml");
            }

            if (LckSettings.Instance.AddMicPermissionsToAndroidManifest)
            {
                AddMicPermissions(path);
            }
            else
            {
                Debug.Log("LCK Not adding microphone permissions to AndroidManifest.xml");
            }

            if (LckSettings.Instance.AddControlCenterPermissionsToAndroidManifest)
            {
                AddControlCenterPermissions(path);
            }
            else
            {
                RemoveStringFromManifest(CONTROL_CENTER_STRING, path);
                Debug.Log("LCK Not adding Control Center permissions to AndroidManifest.xml");
            }
        }

        private void AddInternetPermissions(string path)
        {
#if LCK_UNITY_XR_HAS_FORCE_REMOVE_INTERNET_PERMISSION
            try
            {
                var androidOpenXRSettings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);

                if (androidOpenXRSettings != null)
                {
                    var questFeature = androidOpenXRSettings.GetFeature<MetaQuestFeature>();

                    if (questFeature != null)
                    {
                        if(questFeature.ForceRemoveInternetPermission)
                            Debug.Log("LCK: Ensured MetaQuestFeature 'ForceRemoveInternetPermission' is set to false.");

                        questFeature.ForceRemoveInternetPermission = false;
                    }
                }
            }
            catch
            {
                Debug.LogWarning("LCK: MetaQuestFeature not found. Failed to ensure 'ForceRemoveInternetPermission' is set to false.");
            }
#endif
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");

            if (File.Exists(manifestPath))
            {
                var manifest = File.ReadAllText(manifestPath);

                if (!manifest.Contains("android.permission.INTERNET"))
                {
                    int insertPosition = manifest.IndexOf("<application");
                    if (insertPosition > 0)
                    {
                        manifest = manifest.Insert(insertPosition,
                                "    <uses-permission android:name=\"android.permission.INTERNET\" />\n");
                        File.WriteAllText(manifestPath, manifest);
                        Debug.Log("LCK Internet permission added to AndroidManifest.xml");
                    }
                }
                else
                {
                    Debug.Log("LCK Internet permission already present in AndroidManifest.xml");
                }
            }
            else
            {
                Debug.LogError("LCK Could not add permissions to android manifest. AndroidManifest.xml not found at path: " + manifestPath
                    + " Please go to: Project Settings -> Player -> Publishing Settings -> Build and enable 'Custom Main Manifest'");
            }
        }

        private void AddMicPermissions(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");

            if (File.Exists(manifestPath))
            {
                var manifest = File.ReadAllText(manifestPath);

                if (!manifest.Contains("android.permission.RECORD_AUDIO"))
                {
                    int insertPosition = manifest.IndexOf("<application");
                    if (insertPosition > 0)
                    {
                        manifest = manifest.Insert(insertPosition,
                                "    <uses-permission android:name=\"android.permission.RECORD_AUDIO\" />\n");
                        File.WriteAllText(manifestPath, manifest);
                        Debug.Log("LCK Microphone permission added to AndroidManifest.xml");
                    }
                }
                else
                {
                    Debug.Log("LCK Microphone permission already present in AndroidManifest.xml");
                }
            }
            else
            {
                Debug.LogError("LCK Could not add permissions to android manifest. AndroidManifest.xml not found at path: " + manifestPath
                    + " Please go to: Project Settings -> Player -> Publishing Settings -> Build and enable 'Custom Main Manifest'");
            }
        }

        private void AddControlCenterPermissions(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");

            if (File.Exists(manifestPath))
            {
                var manifest = File.ReadAllText(manifestPath);

                if (!manifest.Contains(CONTROL_CENTER_STRING))
                {
                    if (manifest.Contains("<queries>"))
                    {
                        int queriesStringLength = 9;
                        int insertPosition = queriesStringLength + manifest.IndexOf("<queries>");
                        if (insertPosition < manifest.Length)
                        {
                            manifest = manifest.Insert(insertPosition,
                                "\n        " + CONTROL_CENTER_STRING);
                            File.WriteAllText(manifestPath, manifest);
                            Debug.Log("LCK Control Center permission added to AndroidManifest.xml");
                        }
                    }
                    else
                    {
                        int insertPosition = manifest.IndexOf("<application");
                        if (insertPosition > 0)
                        {
                            manifest = manifest.Insert(insertPosition,
                                "    <queries>\n" +
                                "        " + CONTROL_CENTER_STRING + "\n" +
                                "    </queries>\n");
                            File.WriteAllText(manifestPath, manifest);
                            Debug.Log("LCK Control Center permission added to AndroidManifest.xml");
                        }
                    }
                }
                else
                {
                    Debug.Log("LCK Control Center permission already present in AndroidManifest.xml");
                }
            }
            else
            {
                Debug.LogError("LCK Could not add Control Center permissions to android manifest. AndroidManifest.xml not found at path: " + manifestPath
                    + " Please go to: Project Settings -> Player -> Publishing Settings -> Build and enable 'Custom Main Manifest'");
            }
        }

        private void RemoveStringFromManifest(string toRemove, string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");

            if (File.Exists(manifestPath))
            {
                var manifest = File.ReadAllText(manifestPath);

                if (manifest.Contains(toRemove))
                {
                    manifest = manifest.Replace(toRemove, string.Empty);
                    manifest = Regex.Replace(manifest, @"\r?\n\s*\r?\n", Environment.NewLine);

                    File.WriteAllText(manifestPath, manifest);
                    Debug.Log("LCK removed permissions line: " + toRemove + " from Android Manifest");
                }
            }
        }
    }
}
