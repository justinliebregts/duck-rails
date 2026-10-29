using Liv.Lck;

internal interface ILckCaptureStateProvider
{
    /// <summary>
    /// The current capture state
    /// </summary>
    public LckCaptureState CurrentCaptureState { get; }
    
    /// <summary>
    /// Whether the <see cref="ILckCaptureStateProvider"/> is currently paused
    /// </summary>
    /// <returns>
    /// <see cref="LckResult"/> indicating success or failure, with a value of <c>true</c> when paused, or <c>false</c>
    /// when unpaused
    /// </returns>
    public LckResult<bool> IsPaused();
}