namespace Liv.Lck
{
    /// <summary>
    /// Interface for querying / changing the currently active <see cref="ILckCamera"/>
    /// </summary>
    internal interface ILckActiveCameraConfigurer
    {
        /// <summary>
        /// Gets the currently active camera
        /// </summary>
        /// <returns>
        /// <see cref="LckResult"/> indicating success / failure, providing a reference to the active
        /// <see cref="ILckCamera"/>
        /// </returns>
        LckResult<ILckCamera> GetActiveCamera();
        
        /// <summary>
        /// Activates an <see cref="ILckCamera"/> with the given id, optionally showing the camera's view on the
        /// <see cref="ILckMonitor"/> with the given id
        /// </summary>
        /// <param name="cameraId">The id of the <see cref="ILckCamera"/></param>
        /// <param name="monitorId">The id of the <see cref="ILckMonitor"/></param>
        /// <returns><see cref="LckResult"/> inidicating success / failure</returns>
        LckResult ActivateCameraById(string cameraId, string monitorId = null);

        /// <summary>
        /// Deactivates the currently-active camera
        /// </summary>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        LckResult StopActiveCamera();
    }
} 