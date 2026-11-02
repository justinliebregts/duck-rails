using System;

namespace Liv.Lck
{
    /// <summary>
    /// Defines a common contract for all result objects within the LCK system.
    /// This interface ensures that any method returning a "result" will consistently provide
    /// information about the success or failure of the operation.
    /// </summary>
    public interface ILckResult
    {
        /// <summary>
        /// Gets a value indicating whether the operation completed successfully.
        /// This should always be the first property you check.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// Gets a human-readable message, typically providing more detail about a failure.
        /// This may be null on success.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// Gets a specific, machine-readable error code if the operation failed.
        /// This is null if Success is true. Use this for programmatic error handling (e.g., a switch statement).
        /// </summary>
        Nullable<LckError> Error { get; }
    }

    /// <summary>
    /// Provides a comprehensive set of specific, categorized error codes that can be returned by LCK operations.
    /// Using this enum allows for robust, type-safe error handling instead of relying on parsing string messages.
    /// </summary>
    public enum LckError
    {
        ServiceNotCreated = 1,
        ServiceDisposed = 2,
        InvalidDescriptor = 3,
        CameraIdNotFound = 4,
        MonitorIdNotFound = 5,
        MicrophonePermissionDenied = 6,
        CaptureAlreadyStarted = 7,
        NotCurrentlyRecording = 8,
        NotPaused = 9,
        RecordingError = 10,
        PhotoCaptureError = 11,
        CantEditSettingsWhileCapturing = 12,
        NotEnoughStorageSpace = 13,
        FailedToCopyRecordingToGallery = 14,
        FailedToCopyPhotoToGallery = 15,
        UnsupportedGraphicsApi = 16,
        UnsupportedPlatform = 17,
        MicrophoneError = 18,
        StreamerNotImplemented = 19,
        StreamingError = 20,
        EncodingError = 21,
        UnknownError = 22
    }
    
    /// <summary>
    /// Represents the result of an operation that is expected to return a value of type <typeparamref name="T"/> upon success.
    /// This is the generic implementation of the Result Pattern.
    /// </summary>
    /// <example>
    /// A method like `_lckService.GetDescriptor()` would return an `LckResult<CameraTrackDescriptor>`.
    /// <code>
    /// var descriptorResult = _lckService.GetDescriptor();
    /// if (descriptorResult.Success)
    /// {
    ///     var descriptor = descriptorResult.Result;
    ///     // Use the descriptor...
    /// }
    /// else
    /// {
    ///     Debug.LogError($"Failed to get descriptor: {descriptorResult.Message}");
    /// }
    /// </code>
    /// </example>
    /// <typeparam name="T">The type of the value returned on success.</typeparam>
    public class LckResult<T> : ILckResult
    {
        private readonly bool _success;
        private readonly string _message;
        private readonly Nullable<LckError> _error;
        private readonly T _result;

        public bool Success => _success;
        public string Message => _message;
        public Nullable<LckError> Error => _error;
        
        /// <summary>
        /// Gets the resulting value of the operation if it was successful.
        /// Returns `default(T)` (e.g., null for reference types) if the operation failed.
        /// </summary>
        public T Result => _result;

        /// <summary>
        /// Private constructor to enforce object creation through the static factory methods.
        /// This ensures that every LckResult instance is in a valid state (either success with a result, or failure with an error).
        /// </summary>
        private LckResult(bool success, string message, Nullable<LckError> error, T result)
        {
            _success = success;
            _message = message;
            _error = error;
            _result = result;
        }

        /// <summary>
        /// Creates a new successful result object, containing the operation's return value.
        /// This is intended for internal use within the LCK assembly.
        /// </summary>
        /// <param name="result">The value produced by the successful operation.</param>
        internal static LckResult<T> NewSuccess(T result)
        {
            return new LckResult<T>(true, null, null, result);
        }

        /// <summary>
        /// Creates a new failed result object, containing the error details.
        /// This is intended for internal use within the LCK assembly.
        /// </summary>
        /// <param name="error">The specific error code.</param>
        /// <param name="message">A human-readable message describing the error.</param>
        internal static LckResult<T> NewError(LckError error, string message)
        {
            return new LckResult<T>(false, message, error, default(T));
        }
    }

    /// <summary>
    /// Represents the result of an operation that does not return a value upon success (similar to a `void` method that can fail).
    /// This is the non-generic implementation of the Result Pattern.
    /// </summary>
    /// <example>
    /// A method like `_lckService.StartRecording()` would return a non-generic `LckResult`.
    /// <code>
    /// LckResult result = _lckService.StartRecording();
    /// if (!result.Success)
    /// {
    ///     Debug.LogError($"Could not start recording. Error: {result.Error}");
    /// }
    /// </code>
    /// </example>
    public class LckResult : ILckResult
    {
        private readonly bool _success;
        private readonly string _message;
        private readonly Nullable<LckError> _error;

        public bool Success => _success;
        public string Message => _message;
        public Nullable<LckError> Error => _error;

        /// <summary>
        /// Private constructor to enforce object creation through the static factory methods.
        /// </summary>
        private LckResult(bool success, string message, Nullable<LckError> error)
        {
            _success = success;
            _message = message;
            _error = error;
        }

        /// <summary>
        /// Creates a new successful result object for an operation that does not have a return value.
        /// This is intended for internal use within the LCK assembly.
        /// </summary>
        internal static LckResult NewSuccess()
        {
            return new LckResult(true, null, null);
        }

        /// <summary>
        /// Creates a new failed result object, containing the error details.
        /// This is intended for internal use within the LCK assembly.
        /// </summary>
        /// <param name="error">The specific error code.</param>
        /// <param name="message">A human-readable message describing the error.</param>
        internal static LckResult NewError(LckError error, string message)
        {
            return new LckResult(false, message, error);
        }
    }
}
