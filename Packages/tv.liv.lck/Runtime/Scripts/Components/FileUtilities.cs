using System;
using System.IO;
using System.Threading.Tasks;
using Liv.Lck.Settings;
using Liv.NativeGalleryBridge;
using UnityEngine;

namespace Liv.Lck.Utilities
{
    public static class FileUtility
    {
        public static bool IsFileLocked(string filePath)
        {
            FileStream stream = null;

            try
            {
                stream = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            }
            catch (IOException)
            {
                return true;
            }
            finally
            {
                stream?.Close();
            }
            return false;
        }

        public static async Task CopyToGallery(string sourceFilePath, string albumName, Action<bool, string> callback)
        {
            if (File.Exists(sourceFilePath))
            {
                var mimeTypeIsVideo = Path.GetExtension(sourceFilePath) == ".mp4";

                try
                {
                    var fileName = Path.GetFileName(sourceFilePath);

                    if (Application.platform == RuntimePlatform.Android)
                    {
                        async void WrappedMediaSaveCallback(bool success, string path)
                        {
                            callback.Invoke(success, path);

                            if (success)
                            {
                                await DeleteMatchingFilesAsync(sourceFilePath);
                            }
                        }

                        var permission = mimeTypeIsVideo
                            ? await NativeGallery.SaveVideoToGallery(
                                sourceFilePath,
                                albumName,
                                fileName,
                                new NativeGallery.MediaSaveCallback(WrappedMediaSaveCallback)
                            ) : await NativeGallery.SaveImageToGallery(
                                sourceFilePath,
                                albumName,
                                fileName,
                                new NativeGallery.MediaSaveCallback(WrappedMediaSaveCallback));

                        if (permission != NativeGallery.Permission.Granted)
                        {
                            callback.Invoke(false, sourceFilePath);
                            LckLog.LogError($"LCK Gallery permission not granted: {permission}");
                        }
                    }
                    else
                    {
                        var folderPath = Environment.GetFolderPath(mimeTypeIsVideo ? Environment.SpecialFolder.MyVideos : Environment.SpecialFolder.MyPictures);
                        var albumPath = Path.Combine(folderPath, albumName);

                        if (!Directory.Exists(albumPath))
                        {
                            Directory.CreateDirectory(albumPath);
                        }

                        var destinationFilePath = Path.Combine(albumPath, fileName);

                        // Use Task.Run to perform file copying asynchronously
                        await Task.Run(() => File.Copy(sourceFilePath, destinationFilePath, overwrite: true));
                        await DeleteMatchingFilesAsync(sourceFilePath);

                        // Invoke the callback on the main thread if necessary
                        callback.Invoke(true, destinationFilePath);
                    }
                }
                catch (Exception ex)
                {
                    callback.Invoke(false, sourceFilePath);
                    LckLog.LogError($"LCK Error reading file: {ex.Message}");
                }
            }
            else
            {
                callback.Invoke(false, sourceFilePath);
                LckLog.LogError($"LCK Source file does not exist: {sourceFilePath}");
            }
        }
        
        public static string GenerateFilename(string extension)
        {
            string date = DateTime.Now.ToString(LckSettings.Instance.RecordingDateSuffixFormat);
            string filename = $"{LckSettings.Instance.RecordingFilenamePrefix}_{date}.{extension}";
            return filename;
        }

        public static string GenerateEchoFilename(string extension)
        {
            string date = DateTime.Now.ToString(LckSettings.Instance.RecordingDateSuffixFormat);
            string filename = $"{LckSettings.Instance.RecordingFilenamePrefix}_Echo_{date}.{extension}";
            return filename;
        }

        private const string EchoFileMarker = "_Echo_";

        private static bool IsEchoFile(string filePath)
        {
            return Path.GetFileName(filePath).Contains(EchoFileMarker);
        }

        /// <summary>
        /// Deletes all files in the same folder that share the same extension and category
        /// (echo vs recording) as the given file. This prevents echo saves from deleting
        /// in-progress recording temp files and vice versa.
        /// </summary>
        private static async Task DeleteMatchingFilesAsync(string filePath)
        {
            try
            {
                var folderPath = Path.GetDirectoryName(filePath);
                var fileExtension = Path.GetExtension(filePath);
                var sourceIsEcho = IsEchoFile(filePath);

                if (folderPath != null)
                {
                    await Task.Run(() =>
                    {
                        var candidates = Directory.GetFiles(folderPath, $"*{fileExtension}");
                        for (var index = 0; index < candidates.Length; index++)
                        {
                            var file = candidates[index];
                            if (IsEchoFile(file) != sourceIsEcho)
                                continue;

                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception ex)
                            {
                                LckLog.LogError($"LCK Error deleting file {file}: {ex.Message}");
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error during file deletion: {ex.Message}");
            }
        }

    }
}
