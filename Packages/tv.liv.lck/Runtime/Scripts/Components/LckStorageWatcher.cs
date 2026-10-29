using System;
using System.Collections;
using System.IO;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Scripting;
using static Liv.Lck.LckEvents;

namespace Liv.Lck
{
    internal class LckStorageWatcher : ILckStorageWatcher
    {
        private readonly ILckEventBus _eventBus;

        private const long DefaultStorageThreshold = 500 * 1024 * 1024; // 500MB - used when not recording
        private const long SafetyBufferBytes = 50 * 1024 * 1024; // 50MB safety buffer
        private const float PollIntervalInSeconds = 5f;
        private long _freeSpace = long.MaxValue;

        // Recording context for dynamic threshold calculation
        private bool _isRecordingActive;
        private CameraTrackDescriptor _recordingDescriptor;
        private Func<float> _getDurationSeconds;

        [Preserve]
        public LckStorageWatcher(ILckEventBus eventBus)
        {
            _eventBus = eventBus;
            LckMonoBehaviourMediator.StartCoroutine("LckStorageWatcher:Update", Update());
        }
        
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);
        
        private IEnumerator Update()
        {
            while (true)
            {
                yield return new WaitForSeconds(PollIntervalInSeconds);
                CheckStorageSpace();
            }
        }
        
        private void CheckStorageSpace()
        {
            _freeSpace = GetAvailableStorageSpace();
            var threshold = GetCurrentStorageThreshold();

            if (_freeSpace < threshold)
            {
                _eventBus.Trigger(new LowStorageSpaceDetectedEvent(LckResult.NewSuccess()));
            }
        }

        private long GetCurrentStorageThreshold()
        {
            if (!_isRecordingActive)
            {
                return DefaultStorageThreshold;
            }

            // Calculate estimated file size based on bitrate and recording duration
            var estimatedFileSize = CalculateEstimatedRecordingSize();
            return estimatedFileSize + SafetyBufferBytes;
        }

        private long CalculateEstimatedRecordingSize()
        {
            var durationSeconds = _getDurationSeconds?.Invoke() ?? 0f;
            if (durationSeconds <= 0)
            {
                return 0;
            }

            // Video bytes = (video_bitrate_bps * duration_seconds) / 8
            // Audio bytes = (audio_bitrate_bps * duration_seconds) / 8
            var videoBitsPerSecond = _recordingDescriptor.Bitrate;
            var audioBitsPerSecond = _recordingDescriptor.AudioBitrate;
            var totalBitsPerSecond = videoBitsPerSecond + audioBitsPerSecond;

            return (long)((totalBitsPerSecond * durationSeconds) / 8);
        }

        public void SetRecordingContext(CameraTrackDescriptor descriptor, Func<float> getDurationSeconds)
        {
            _recordingDescriptor = descriptor;
            _getDurationSeconds = getDurationSeconds;
            _isRecordingActive = true;
        }

        public void ClearRecordingContext()
        {
            _isRecordingActive = false;
        }
        
        private long GetAvailableStorageSpace()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            return GetAndroidAvailableStorageSpace();
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            return GetWindowsAvailableStorageSpace();
#else
            // For other platforms
            return long.MaxValue;
#endif
        }
        
#if UNITY_ANDROID && !UNITY_EDITOR
        private long GetAndroidAvailableStorageSpace()
        {
            try
            {
                using (AndroidJavaClass statFsClass = new AndroidJavaClass("android.os.StatFs"))
                using (AndroidJavaObject statFs = new AndroidJavaObject("android.os.StatFs", Application.temporaryCachePath))
                {
                    long blockSize = statFs.Call<long>("getBlockSizeLong");
                    long availableBlocks = statFs.Call<long>("getAvailableBlocksLong");
                    return blockSize * availableBlocks;
                }
            }
            catch (Exception e)
            {
                LckLog.LogError("LCK Failed to get Android storage space: " + e.Message);
                return -1;
            }
        }
#endif

        public long GetWindowsAvailableStorageSpace()
        {
            try
            {
                string driveRoot = Path.GetPathRoot(Application.temporaryCachePath);
                ulong freeBytesAvailable, totalNumberOfBytes, totalNumberOfFreeBytes;

                if (GetDiskFreeSpaceEx(driveRoot, out freeBytesAvailable, out totalNumberOfBytes, out totalNumberOfFreeBytes))
                {
                    return (long)freeBytesAvailable;
                }
                else
                {
                    LckLog.LogError("Failed to get Windows storage space: " + Marshal.GetLastWin32Error());
                    return -1;
                }
            }
            catch (Exception e)
            {
                LckLog.LogError("Failed to get Windows storage space: " + e.Message);
                return -1;
            }
        }

        public bool HasEnoughFreeStorage()
        {
            return _freeSpace > GetCurrentStorageThreshold();
        }

        public void Dispose()
        {
            LckMonoBehaviourMediator.StopCoroutineByName("LckStorageWatcher:Update");
        }
    }
}
