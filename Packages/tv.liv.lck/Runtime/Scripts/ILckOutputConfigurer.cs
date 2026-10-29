namespace Liv.Lck
{
    internal interface ILckOutputConfigurer
    {
        /// <summary>
        /// Configures output track information based on a given <see cref="QualityOption"/>
        /// </summary>
        /// <param name="qualityOption">The <see cref="QualityOption"/> whose configuration options will be used</param>
        /// <returns>An <see cref="LckResult"/> indicating success / failure</returns>
        LckResult ConfigureFromQualityConfig(QualityOption qualityOption);

        LckResult<LckCaptureType> GetActiveCaptureType();
        LckResult SetActiveCaptureType(LckCaptureType captureType);
        
        LckResult SetActiveVideoFramerate(uint framerate);
        LckResult SetActiveVideoBitrate(uint bitrate);
        LckResult SetActiveAudioBitrate(uint bitrate);
        LckResult SetActiveResolution(CameraResolutionDescriptor resolution);
        
        /// <summary>
        /// Sets the <see cref="LckCameraOrientation"/> for all capture types
        /// </summary>
        /// <param name="orientation">The new orientation type</param>
        /// <returns><see cref="LckResult"/> indicating success / failure</returns>
        LckResult SetCameraOrientation(LckCameraOrientation orientation);
        
        LckResult<CameraTrackDescriptor> GetCameraTrackDescriptor(LckCaptureType captureType);
        LckResult SetCameraTrackDescriptor(LckCaptureType captureType, CameraTrackDescriptor trackDescriptor);
        
        LckResult<CameraTrackDescriptor> GetActiveCameraTrackDescriptor();
        LckResult SetActiveCameraTrackDescriptor(CameraTrackDescriptor trackDescriptor);
        
        LckResult<uint> GetNumberOfAudioChannels();
        LckResult<uint> GetAudioSampleRate();
    }
}