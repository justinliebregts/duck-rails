using System;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;

namespace Liv.Lck.Util
{
    /// <summary>
    /// Utility class for checking the latest LCK SDK version from the backend API.
    /// Uses the JSON-RPC endpoint to query dev.get_latest_sdk_version.
    /// </summary>
    public static class LckVersionChecker
    {
        // The backend API endpoint for RPC calls
        private const string BackendRpcUrl = "https://api.obi.gg/api/rpc";

        // Shared HttpClient instance (recommended pattern for HttpClient)
        private static readonly HttpClient _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        /// <summary>
        /// Contains the result of a version check.
        /// </summary>
        public class VersionCheckResult
        {
            /// <summary>
            /// Whether the check succeeded.
            /// </summary>
            public bool Success { get; set; }

            /// <summary>
            /// The latest available version string (e.g., "1.5.0").
            /// </summary>
            public string LatestVersion { get; set; }

            /// <summary>
            /// Optional download URL for the update.
            /// </summary>
            public string DownloadUrl { get; set; }

            /// <summary>
            /// Error message if the check failed.
            /// </summary>
            public string Error { get; set; }
        }

        /// <summary>
        /// Checks for the latest SDK version asynchronously.
        /// Uses HttpClient which is more appropriate for editor scripts.
        /// </summary>
        /// <param name="onComplete">Callback invoked when the check completes.</param>
        public static void CheckForUpdate(Action<VersionCheckResult> onComplete)
        {
            // Run the async operation and handle the result
            CheckForUpdateAsync().ContinueWith(task =>
            {
                // Must dispatch back to main thread for Unity API calls
                EditorApplication.delayCall += () =>
                {
                    if (task.IsFaulted)
                    {
                        onComplete?.Invoke(new VersionCheckResult
                        {
                            Success = false,
                            Error = task.Exception?.InnerException?.Message ?? "Unknown error"
                        });
                    }
                    else
                    {
                        onComplete?.Invoke(task.Result);
                    }
                };
            });
        }

        private static async Task<VersionCheckResult> CheckForUpdateAsync()
        {
            var result = new VersionCheckResult();

            try
            {
                // Build the JSON-RPC request body
                // Format: {"id": 1, "method": "dev.get_latest_sdk_version", "params": {"data": {"sdk_name": "lck", "engine": "unity"}}}
                string jsonBody = "{\"id\":1,\"method\":\"dev.get_latest_sdk_version\",\"params\":{\"data\":{\"sdk_name\":\"lck\",\"engine\":\"unity\"}}}";

                using (var content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json"))
                {
                    // Add user agent header
                    if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
                    {
                        _httpClient.DefaultRequestHeaders.Add("User-Agent", "lck-unity-editor");
                    }

                    var response = await _httpClient.PostAsync(BackendRpcUrl, content);

                    if (response.IsSuccessStatusCode)
                    {
                        string responseText = await response.Content.ReadAsStringAsync();

                        // Parse the response using Unity's JsonUtility
                        // The API returns {"id":1,"jsonrpc":"2.0","result":"1.4.3"} where result is a string
                        var rpcResponse = JsonUtility.FromJson<RpcResponse>(responseText);

                        if (rpcResponse != null && !string.IsNullOrEmpty(rpcResponse.result))
                        {
                            result.Success = true;
                            result.LatestVersion = rpcResponse.result;
                            // Download URL is not provided by this endpoint, will use fallback
                            result.DownloadUrl = null;
                        }
                        else
                        {
                            result.Success = false;
                            result.Error = "Invalid response format";
                        }
                    }
                    else
                    {
                        result.Success = false;
                        result.Error = $"HTTP {(int)response.StatusCode}: {response.ReasonPhrase}";
                    }
                }
            }
            catch (HttpRequestException ex)
            {
                result.Success = false;
                result.Error = $"Network error: {ex.Message}";
            }
            catch (TaskCanceledException)
            {
                result.Success = false;
                result.Error = "Request timed out";
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Error = $"Error: {ex.Message}";
            }

            return result;
        }

        /// <summary>
        /// Compares two semantic version strings.
        /// </summary>
        /// <returns>
        /// -1 if version1 is less than version2,
        /// 0 if they are equal,
        /// 1 if version1 is greater than version2
        /// </returns>
        public static int CompareVersions(string version1, string version2)
        {
            if (string.IsNullOrEmpty(version1) && string.IsNullOrEmpty(version2))
                return 0;
            if (string.IsNullOrEmpty(version1))
                return -1;
            if (string.IsNullOrEmpty(version2))
                return 1;

            var parts1 = version1.Split('.');
            var parts2 = version2.Split('.');

            int maxLength = Math.Max(parts1.Length, parts2.Length);

            for (int i = 0; i < maxLength; i++)
            {
                int v1 = i < parts1.Length && int.TryParse(parts1[i], out int p1) ? p1 : 0;
                int v2 = i < parts2.Length && int.TryParse(parts2[i], out int p2) ? p2 : 0;

                if (v1 < v2) return -1;
                if (v1 > v2) return 1;
            }

            return 0;
        }

        /// <summary>
        /// Checks if an update is available by comparing the current version to the latest.
        /// </summary>
        public static bool IsUpdateAvailable(string currentVersion, string latestVersion)
        {
            return CompareVersions(currentVersion, latestVersion) < 0;
        }

        // JSON serialization class for the RPC response
        // The API returns: {"id":1,"jsonrpc":"2.0","result":"1.4.3"}
        // where result is the version string directly

        [Serializable]
        private class RpcResponse
        {
            public string result;
        }
    }
}
