using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using UnityEngine;
using AOT;
using Liv.Lck.Core.FFI;

namespace Liv.Lck.Core
{
    namespace FFI
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct GameInfo
        {
            public IntPtr GameName;
            public IntPtr GameVersion;
            public IntPtr ProjectName;
            public IntPtr CompanyName;
            public IntPtr EngineVersion;
            public IntPtr RenderPipeline;
            public IntPtr GraphicsAPI;
            public IntPtr Platform;
            public IntPtr PersistentDataPath;
            public IntPtr InteractionSystems;

            /// <summary>
            /// Create an FFI <see cref="GameInfo"/> struct from a <see cref="Liv.Lck.Core.GameInfo"/>, allocating
            /// unmanaged memory for each field.
            /// </summary>
            /// <param name="gameInfo">The <see cref="Core.GameInfo"/> to use to populate the struct</param>
            /// <returns>A <see cref="GameInfo"/> suitable for FFI use</returns>
            /// <remarks>
            /// IMPORTANT: Free allocated unmanaged memory by calling <see cref="Free"/> on the returned struct.
            /// </remarks>
            public static GameInfo AllocateFromGameInfo(Liv.Lck.Core.GameInfo gameInfo)
            {
                return new GameInfo(gameInfo);
            }
            
            public void Free()
            {
                InteropUtilities.Free(GameName);
                InteropUtilities.Free(GameVersion);
                InteropUtilities.Free(ProjectName);
                InteropUtilities.Free(CompanyName);
                InteropUtilities.Free(EngineVersion);
                InteropUtilities.Free(RenderPipeline);
                InteropUtilities.Free(GraphicsAPI);
                InteropUtilities.Free(Platform);
                InteropUtilities.Free(PersistentDataPath);
                InteropUtilities.Free(InteractionSystems);
            }
            
            private GameInfo(Liv.Lck.Core.GameInfo gameInfo)
            {
                GameName = InteropUtilities.StringToUTF8Pointer(gameInfo.GameName);
                GameVersion = InteropUtilities.StringToUTF8Pointer(gameInfo.GameVersion);
                ProjectName = InteropUtilities.StringToUTF8Pointer(gameInfo.ProjectName);
                CompanyName = InteropUtilities.StringToUTF8Pointer(gameInfo.CompanyName);
                EngineVersion = InteropUtilities.StringToUTF8Pointer(gameInfo.EngineVersion);
                RenderPipeline = InteropUtilities.StringToUTF8Pointer(gameInfo.RenderPipeline);
                GraphicsAPI = InteropUtilities.StringToUTF8Pointer(gameInfo.GraphicsAPI);
                Platform = InteropUtilities.StringToUTF8Pointer(gameInfo.Platform);
                PersistentDataPath = InteropUtilities.StringToUTF8Pointer(gameInfo.PersistentDataPath);
                InteractionSystems = InteropUtilities.StringToUTF8Pointer(gameInfo.InteractionSystems);
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct LckInfo
        {
            public IntPtr Version;
            public int BuildNumber;
            
            /// <summary>
            /// Create an FFI <see cref="LckInfo"/> struct from a <see cref="Liv.Lck.Core.LckInfo"/>, allocating
            /// unmanaged memory for each field.
            /// </summary>
            /// <param name="lckInfo">The <see cref="Core.LckInfo"/> to use to populate the struct</param>
            /// <returns>A <see cref="LckInfo"/> suitable for FFI use</returns>
            /// <remarks>
            /// IMPORTANT: Free allocated unmanaged memory by calling <see cref="Free"/> on the returned struct.
            /// </remarks>
            public static LckInfo AllocateFromLckInfo(Liv.Lck.Core.LckInfo lckInfo)
            {
                return new LckInfo(lckInfo);
            }
            
            public void Free()
            {
                InteropUtilities.Free(Version);
            }
            
            private LckInfo(Liv.Lck.Core.LckInfo lckInfo)
            {
                Version = InteropUtilities.StringToUTF8Pointer(lckInfo.Version);
                BuildNumber = lckInfo.BuildNumber;
            }
        }
    }
    
    public struct GameInfo
    {
        public string GameName;
        public string GameVersion;
        public string ProjectName;
        public string CompanyName;
        public string EngineVersion;
        public string RenderPipeline;
        public string GraphicsAPI;
        public string Platform;
        public string PersistentDataPath;
        public string InteractionSystems;
    }

    public struct LckInfo
    {
        public string Version;
        public int BuildNumber;
    }

    /// <summary>
    /// Specifies the verbosity of the LCK SDK's internal logging.
    /// Use this to control the amount of diagnostic information printed to the console.
    /// </summary>
    public enum LevelFilter
    {
        /// <summary>Disables all logging from the SDK.</summary>
        Off,
        /// <summary>Logs only critical errors.</summary>
        Error,
        /// <summary>Logs errors and warnings.</summary>
        Warn,
        /// <summary>Logs errors, warnings, and informational messages.</summary>
        Info,
        /// <summary>Logs all messages, including detailed debug information.</summary>
        Debug,
        /// <summary>Logs all messages, including highly verbose trace information for deep debugging.</summary>
        Trace,
    }

    /// <summary>
    /// Specifies the type of log message being recorded by the LCK SDK.
    /// Used internally for categorizing log entries.
    /// </summary>
    public enum LogType
    {
        Error,
        Warning,
        Info,
        Trace,
    }

    /// <summary>
    /// Represents high-level error categories that can be returned by SDK operations.
    /// Check the 'Err' property on a failed Result to identify the cause of failure.
    /// </summary>
    public enum CoreError
    {
        /// <summary>An unexpected internal error occurred within the SDK.</summary>
        InternalError = 0,
        /// <summary>The 'trackingId' provided during initialisation was missing or invalid.</summary>
        MissingTrackingId = 1,
        /// <summary>An invalid argument was passed to an SDK method.</summary>
        InvalidArgument = 2,
        /// <summary>The operation could not be completed because the user is not logged in.</summary>
        UserNotLoggedIn = 3,
        /// <summary>Failed to cache cosmetics</summary>
        FailedToCacheCosmetics = 4,
        /// <summary>The LIV backend is currently unavailable.</summary>
        ServiceUnavailable = 5,
        /// <summary>Errors received from backend, client is now in exponential backoff.</summary>
        RateLimiterBackoff = 6,
        /// <summary>The login attempt has expired and can no longer be completed.</summary>
        LoginAttemptExpired = 7,
        /// <summary>The provided tracking ID is invalid.</summary>
        InvalidTrackingId = 8,
    }

    /// <summary>
    /// A generic class that encapsulates the result of an SDK operation. Operations can either
    /// succeed and return a value, or fail and return an error. This object cleanly represents
    /// both outcomes without using exceptions for control flow.
    /// </summary>
    /// <typeparam name="T">The type of the value returned on success.</typeparam>
    public class Result<T>
    {
        private readonly bool _success;
        private readonly string _message;
        private readonly CoreError? _error;
        private readonly T _result;

        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// **Always check this property first.** If this is <c>true</c>, you can safely access the <see cref="Ok"/> property.
        /// If this is <c>false</c>, check the <see cref="Err"/> and <see cref="Message"/> properties for failure details.
        /// </summary>
        public bool IsOk => _success;

        /// <summary>
        /// Gets the detailed error message if the operation failed.
        /// This property is only meaningful when <see cref="IsOk"/> is <c>false</c>.
        /// </summary>
        public string Message => _message;

        /// <summary>
        /// Gets the high-level error category if the operation failed.
        /// This property is only meaningful when <see cref="IsOk"/> is <c>false</c>.
        /// </summary>
        public CoreError? Err => _error;

        /// <summary>
        /// Gets the successful result of the operation.
        /// **Warning:** Only access this property after confirming that <see cref="IsOk"/> is <c>true</c>.
        /// Accessing it on a failed result will return the default value for type <typeparamref name="T"/> (e.g., null, 0, or false).
        /// </summary>
        public T Ok => _result;

        private Result(bool success, string message, CoreError? error, T result)
        {
            _success = success;
            _message = message;
            _error = error;
            _result = result;
        }

        /// <summary>
        /// Creates a new <see cref="Result{T}"/> object representing a successful operation.
        /// </summary>
        /// <param name="result">The value to wrap.</param>
        public static Result<T> NewSuccess(T result)
        {
            return new Result<T>(true, null, null, result);
        }

        /// <summary>
        /// Creates a new <see cref="Result{T}"/> object representing a failed operation.
        /// </summary>
        /// <param name="error">The error category.</param>
        /// <param name="message">The detailed error message.</param>
        public static Result<T> NewError(CoreError error, string message)
        {
            return new Result<T>(false, message, error, default(T));
        }
    }

    /// <summary>
    /// The primary static class for interacting with the LCK Core SDK.
    /// It provides methods for initialisation, authentication, and querying user status.
    /// Generally, it would not be recommended to modify or re-implement this class.
    /// Should you desire a custom UX flow for streaming, see <see cref="Streaming.LckStreamingController"/>.
    /// </summary>
    public static class LckCore
    {
        private static readonly object _loginLock = new object();
        private static ReturnCode _lastReturnCode;
        private static string _loginCode;

        [MonoPInvokeCallback(typeof(LckCoreNative.start_login_attempt_callback_delegate))]
        private static void StartLoginAttemptCallback(ReturnCode returnCode, IntPtr loginCodePtr)
        {
            lock (_loginLock)
            {
                _lastReturnCode = returnCode;
                if (returnCode == ReturnCode.Ok)
                {
                    _loginCode = InteropUtilities.UTF8PointerToString(loginCodePtr);
                }
            }
        }

        public static void SetMaxLogLevel(LevelFilter levelFilter)
        {
            LckCoreNative.set_max_log_level(levelFilter);
        }

        /// <summary>
        /// Initialises the LCK SDK. This method must be called once, typically during your
        /// game's startup sequence, before any other LCK SDK methods are used.
        /// Currently called by a 'RuntimeInitializeOnLoadMethod' through <see cref="LckCoreHandler"/>.
        /// </summary>
        /// <param name="trackingId">Your unique application tracking ID provided by LIV.</param>
        /// <param name="gameInfo">A struct containing details about your game and its engine configuration.</param>
        /// <param name="lckInfo">A struct containing details about the LCK package being used.</param>
        /// <returns>A <see cref="Result{T}"/> of type <c>bool</c> indicating if initialisation was successful.</returns>
        public static Result<bool> Initialize(string trackingId, GameInfo gameInfo, LckInfo lckInfo)
        {
            if (string.IsNullOrEmpty(trackingId))
            {
                return Result<bool>.NewError(CoreError.MissingTrackingId, "Tracking ID cannot be null or empty.");
            }

#if UNITY_ANDROID && !UNITY_EDITOR
            using(var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using(var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity")) {
                IntPtr ctxPtr = activity.GetRawObject();
                var androidResult = LckCoreNative.initialize_android(ctxPtr);

                if (androidResult != ReturnCode.Ok) {
                    return Result<bool>.NewError(CoreError.InternalError, $"Failed to initialize LckCore for Android: {androidResult}");
                }
            }
#endif

            var trackingIdPtr = InteropUtilities.StringToUTF8Pointer(trackingId);
            FFI.GameInfo ffiGameInfo = FFI.GameInfo.AllocateFromGameInfo(gameInfo);
            FFI.LckInfo ffiLckInfo = FFI.LckInfo.AllocateFromLckInfo(lckInfo);
            ReturnCode result;
            try
            {
                result = LckCoreNative.initialize(trackingIdPtr, ffiGameInfo, ffiLckInfo);
            }
            finally
            {
                InteropUtilities.Free(trackingIdPtr);
                ffiGameInfo.Free();
                ffiLckInfo.Free();
            }

            switch (result)
            {
                case ReturnCode.Ok:
                    return Result<bool>.NewSuccess(true);
                case ReturnCode.InvalidArgument:
                    return Result<bool>.NewError(CoreError.InvalidArgument, "Invalid argument provided to initialize LckCore.");
                case ReturnCode.InvalidTrackingId:
                    return Result<bool>.NewError(CoreError.InvalidTrackingId, "Provided Tracking ID is not valid.");
                default:
                    return Result<bool>.NewError(CoreError.InternalError, $"Failed to initialize LckCore: {result}");
            }
        }

        public static async Task<Result<bool>> HasUserConfiguredStreaming()
        {
            var returnCode = ReturnCode.Ok;
            var hasConfigured = false;

            IntPtr hasConfiguredPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(bool)));
            Marshal.WriteByte(hasConfiguredPtr, 0);

            await Task.Run(() =>
            {
                returnCode = LckCoreNative.has_user_configured_streaming(hasConfiguredPtr);
            });

            if (returnCode == ReturnCode.Ok)
            {
                hasConfigured = Marshal.ReadByte(hasConfiguredPtr) != 0;
            }

            Marshal.FreeHGlobal(hasConfiguredPtr);

            if (returnCode != ReturnCode.Ok)
            {
                var (coreError, message) = MapReturnCodeToCoreError(returnCode);
                return Result<bool>.NewError(coreError, message);
            }

            return Result<bool>.NewSuccess(hasConfigured);
        }

        private static (CoreError, string) MapReturnCodeToCoreError(ReturnCode returnCode)
        {
            switch (returnCode)
            {
                case ReturnCode.UserNotLoggedIn:
                    return (CoreError.UserNotLoggedIn, "User is not logged in.");
                case ReturnCode.BackendUnavailable:
                    return (CoreError.ServiceUnavailable, "LIV backend service is unavailable.");
                case ReturnCode.RateLimiterBackoff:
                    return (CoreError.RateLimiterBackoff, "Client is in rate limiter backoff due to previous errors.");
                default:
                    return (CoreError.InternalError, $"Operation failed with return code: {returnCode}");
            }
        }

        public static async Task<Result<bool>> IsUserSubscribed()
        {
            var returnCode = ReturnCode.Ok;
            var isSubscribed = false;

            IntPtr isSubscribedPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(bool)));
            Marshal.WriteByte(isSubscribedPtr, 0);

            await Task.Run(() =>
            {
                returnCode = LckCoreNative.is_user_subscribed(isSubscribedPtr);
            });

            if (returnCode == ReturnCode.Ok)
            {
                isSubscribed = Marshal.ReadByte(isSubscribedPtr) != 0;
            }

            Marshal.FreeHGlobal(isSubscribedPtr);

            if (returnCode != ReturnCode.Ok)
            {
                var (coreError, message) = MapReturnCodeToCoreError(returnCode);
                return Result<bool>.NewError(coreError, message);
            }

            return Result<bool>.NewSuccess(isSubscribed);
        }

        public static async Task<Result<string>> StartLoginAttemptAsync()
        {
            ReturnCode returnCode;
            string loginCode;

            Debug.Log("LCK: Starting login attempt task...");
            await Task.Run(() =>
            {
                lock (_loginLock)
                {
                    _loginCode = null;
                    _lastReturnCode = ReturnCode.Ok;
                }
                LckCoreNative.start_login_attempt(StartLoginAttemptCallback);
            });

            lock (_loginLock)
            {
                returnCode = _lastReturnCode;
                loginCode = _loginCode;
            }

            Debug.Log($"LCK: Login attempt task completed with return code: {returnCode}");

            if (returnCode != ReturnCode.Ok || loginCode == null)
            {
                var (coreError, message) = MapReturnCodeToCoreError(returnCode);
                return Result<string>.NewError(coreError, message);
            }
            else {
                return Result<string>.NewSuccess(loginCode);
            }
        }

        public static async Task<Result<bool>> CheckLoginCompletedAsync()
        {
            var returnCode = ReturnCode.Ok;
            var isComplete = false;

            Debug.Log("LCK: Starting check login completed task...");
            await Task.Run(() =>
            {
                IntPtr completePtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(bool)));
                Marshal.WriteByte(completePtr, 0);

                var result = LckCoreNative.check_login_attempt_completed(completePtr);

                if (result != ReturnCode.Ok)
                {
                    returnCode = result;
                }
                else
                {
                    isComplete = Marshal.ReadByte(completePtr) != 0;
                }

                Marshal.FreeHGlobal(completePtr);
            });

            Debug.Log($"LCK: Check login completed task finished with return code: {returnCode}, isComplete: {isComplete}");

            if (returnCode != ReturnCode.Ok)
            {
                var (coreError, message) = MapReturnCodeToCoreError(returnCode);
                return Result<bool>.NewError(coreError, message);
            }

            return Result<bool>.NewSuccess(isComplete);
        }

        public static async Task<Result<float>> GetRemainingBackoffTimeSeconds()
        {
            var returnCode = ReturnCode.Error;
            float remainingTime = 0f;

            await Task.Run(() =>
            {
                IntPtr remainingPtr = Marshal.AllocHGlobal(Marshal.SizeOf(typeof(float)));
                Marshal.WriteInt32(remainingPtr, 0);

                var result = LckCoreNative.get_remaining_backoff_time_seconds(remainingPtr);

                if (result != ReturnCode.Ok)
                {
                    returnCode = result;
                }
                else
                {
                    remainingTime = Marshal.ReadInt32(remainingPtr);
                }

                Marshal.FreeHGlobal(remainingPtr);
            });

            if (returnCode != ReturnCode.Ok)
            {
                var (coreError, message) = MapReturnCodeToCoreError(returnCode);
                return Result<float>.NewError(coreError, message);
            }

            return Result<float>.NewSuccess(remainingTime);
        }

        public static void Log(LogType level, string message, string memberName = "", string filePath = "", int lineNumber = 0)
        {
            var msgPtr = InteropUtilities.StringToUTF8Pointer(message);
            var memberPtr = InteropUtilities.StringToUTF8Pointer(memberName);
            var filePtr = InteropUtilities.StringToUTF8Pointer(filePath);
            try
            {
                LckCoreNative.log(level, msgPtr, memberPtr, filePtr, lineNumber);
            }
            finally
            {
                InteropUtilities.Free(msgPtr);
                InteropUtilities.Free(memberPtr);
                InteropUtilities.Free(filePtr);
            }
        }

        // Called from Editor only
        public static void Dispose()
        {
            var result = LckCoreNative.dispose();

            if (result != ReturnCode.Ok)
            {
                throw new InvalidOperationException($"Failed to dispose LckCore: {result}");
            }
        }
    }
}
