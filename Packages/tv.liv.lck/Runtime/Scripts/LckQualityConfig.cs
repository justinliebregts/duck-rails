using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Liv.Lck
{
    [System.Serializable]
    public enum DeviceModel
    {
        Quest2 = 0,
        Quest3 = 1,
        Quest3s = 3,
    }
    
    [System.Serializable]
    public struct QualityOption
    {
        public string Name;
        public bool IsDefault;
        
        [FormerlySerializedAs("CameraTrackDescriptor")]
        public CameraTrackDescriptor RecordingCameraTrackDescriptor;
        public CameraTrackDescriptor StreamingCameraTrackDescriptor;
        
        public QualityOption(string name, bool isDefault, CameraTrackDescriptor recordingCameraTrackDescriptor, CameraTrackDescriptor streamingCameraTrackDescriptor)
        {
            Name = name;
            IsDefault = isDefault;
            RecordingCameraTrackDescriptor = recordingCameraTrackDescriptor;
            StreamingCameraTrackDescriptor = streamingCameraTrackDescriptor;
        }

        #region Obsolete
        [Obsolete("Provides the RecordingCameraTrackDescriptor and only exists for backwards compability - " + 
                  "Use RecordingCameraTrackDescriptor or StreamingCameraTrackDescriptor instead")]
        public CameraTrackDescriptor CameraTrackDescriptor => RecordingCameraTrackDescriptor;
        #endregion
    }

    [System.Serializable]
    public struct QualityOptionOverride
    {
        public DeviceModel DeviceModel;
        public List<QualityOption> QualityOptions;
    }

    public interface ILckQualityConfig
    {
        List<QualityOption> GetQualityOptionsForSystem();
    }

    [CreateAssetMenu(fileName = "LckQualityConfig", menuName = "LIV/LCK/QualityConfig")]
    public class LckQualityConfig : ScriptableObject, ILckQualityConfig
    {
        [Header("Android")]
        public List<QualityOption> BaseAndroidQualityOptions = new List<QualityOption>();
        public List<QualityOptionOverride> AndroidOptionsDeviceOverrides = new List<QualityOptionOverride>();

        [Header("Desktop")]
        public List<QualityOption> DesktopQualityOptions = new List<QualityOption>();

        public List<QualityOption> GetQualityOptionsForSystem()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.Android:
                    var deviceModel = GetCurrentDeviceModel();
                    if (deviceModel != null)
                    {
                        foreach (var overrideOption in AndroidOptionsDeviceOverrides)
                        {
                            if (overrideOption.DeviceModel == deviceModel)
                            {
                                return overrideOption.QualityOptions;
                            }
                        }
                    }

                    return BaseAndroidQualityOptions;
                case RuntimePlatform.WindowsPlayer:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxPlayer:
                case RuntimePlatform.LinuxEditor:
                case RuntimePlatform.OSXPlayer:
                case RuntimePlatform.OSXEditor:
                    return DesktopQualityOptions;
                default:
                    throw new System.NotImplementedException(
                        $"LCK does not support {Application.platform} platform"
                    );
            }
        }

        private DeviceModel? GetCurrentDeviceModel()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            var buildinfo = new AndroidJavaClass("android.os.Build");
            var device = buildinfo.GetStatic<string>("DEVICE");

            switch (device)
            {
                case "panther":
                    return DeviceModel.Quest3s;
                case "eureka":
                    return DeviceModel.Quest3;
                case "hollywood":
                    return DeviceModel.Quest2;
                default:
                    return null;
            }
#endif
            return null;
        }
    }
}
