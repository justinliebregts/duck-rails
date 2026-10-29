namespace Liv.Lck
{
    /// <summary>
    /// Interface that all LCK plugins must implement
    /// </summary>
    public interface ILCKPlugin
    {
        /// <summary>
        /// The unique name of the plugin
        /// </summary>
        string PluginName { get; }

        /// <summary>
        /// The version of the plugin
        /// </summary>
        string PluginVersion { get; }

        /// <summary>
        /// Initialize the plugin with the LCK service
        /// </summary>
        /// <param name="lckService">The LCK service instance</param>
        void Initialize(LckService lckService);

        /// <summary>
        /// Called when the plugin should be shutdown/cleaned up
        /// </summary>
        void Shutdown();
    }
} 