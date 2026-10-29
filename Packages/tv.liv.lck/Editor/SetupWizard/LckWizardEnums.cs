namespace Liv.Lck
{
    /// <summary>
    /// Page identifiers for the LCK Setup Wizard navigation.
    /// </summary>
    public enum WizardPage
    {
        UpdateAvailable,
        Welcome,
        TrackingID,
        InteractionSetup,
        UnityXRInteractionMethod,
        UnityXRSetup,
        MetaXRSetup,
        CustomSetup,
        DefaultTablet,
        DirectTablet,
        AudioSetup,
        FMODSetup,
        WwiseSetup,
        OtherAudioSetup,
        ProjectValidation,
        AdditionalHelp,
    }

    /// <summary>
    /// Supported interaction toolkit types for LCK integration.
    /// </summary>
    public enum InteractionToolkitType
    {
        None,
        UnityXR,
        MetaXR,
        Custom
    }

    /// <summary>
    /// Unity XR interaction method options.
    /// </summary>
    public enum UnityXRInteractionMethodType
    {
        None,
        RayBased,
        DirectTouch
    }

    /// <summary>
    /// Supported audio system types for LCK audio capture.
    /// </summary>
    public enum AudioSystemType
    {
        None,
        DefaultUnityAudio,
        FMOD,
        Wwise,
        Other
    }

    /// <summary>
    /// Validation severity levels for project setup checks.
    /// </summary>
    public enum ValidationSeverity
    {
        Required,
        Suggested
    }
}
