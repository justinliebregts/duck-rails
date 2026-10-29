using System;

namespace Liv.Lck
{
    /// <summary>
    /// Base class for LCK plugins that provides automatic registration with the plugin manager
    /// </summary>
    public abstract class LCKPluginBase : ILCKPlugin
    {
        /// <summary>
        /// The LCK service instance passed during initialization
        /// </summary>
        protected LckService LckService { get; private set; }

        /// <summary>
        /// Whether the plugin has been initialized
        /// </summary>
        protected bool IsInitialized { get; private set; }

        /// <summary>
        /// The unique name of the plugin - must be overridden by derived classes
        /// </summary>
        public abstract string PluginName { get; }

        /// <summary>
        /// The version of the plugin - must be overridden by derived classes
        /// </summary>
        public abstract string PluginVersion { get; }

        /// <summary>
        /// Constructor that automatically registers the plugin with the plugin manager
        /// </summary>
        protected LCKPluginBase()
        {
            // Auto-register with the plugin manager
            LCKPlugins.Instance.RegisterPlugin(this);
        }

        /// <summary>
        /// Initialize the plugin with the LCK service
        /// </summary>
        /// <param name="lckService">The LCK service instance</param>
        public void Initialize(LckService lckService)
        {
            if (IsInitialized)
            {
                LckLog.LogWarning($"Plugin {PluginName} is already initialized");
                return;
            }

            LckService = lckService;
            
            try
            {
                OnInitialize();
                IsInitialized = true;
                LckLog.Log($"Plugin {PluginName} initialized successfully");
            }
            catch (Exception ex)
            {
                LckLog.LogError($"Failed to initialize plugin {PluginName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Called when the plugin should be shutdown/cleaned up
        /// </summary>
        public void Shutdown()
        {
            if (!IsInitialized)
            {
                LckLog.LogWarning($"Plugin {PluginName} is not initialized");
                return;
            }

            try
            {
                OnShutdown();
                IsInitialized = false;
                LckService = null;
                LckLog.Log($"Plugin {PluginName} shutdown successfully");
            }
            catch (Exception ex)
            {
                LckLog.LogError($"Failed to shutdown plugin {PluginName}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Override this method to perform plugin-specific initialization
        /// </summary>
        protected virtual void OnInitialize()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Override this method to perform plugin-specific shutdown
        /// </summary>
        protected virtual void OnShutdown()
        {
            // Override in derived classes
        }

        /// <summary>
        /// Check if another plugin is available by type
        /// </summary>
        /// <typeparam name="T">The plugin type to check for</typeparam>
        /// <returns>True if the plugin is available, false otherwise</returns>
        protected bool HasPlugin<T>() where T : class, ILCKPlugin
        {
            return LCKPlugins.Instance.HasPlugin<T>();
        }

        /// <summary>
        /// Check if another plugin is available by name
        /// </summary>
        /// <param name="pluginName">The name of the plugin to check for</param>
        /// <returns>True if the plugin is available, false otherwise</returns>
        protected bool HasPlugin(string pluginName)
        {
            return LCKPlugins.Instance.HasPlugin(pluginName);
        }

        /// <summary>
        /// Get another plugin by type
        /// </summary>
        /// <typeparam name="T">The plugin type to retrieve</typeparam>
        /// <returns>The plugin instance or null if not found</returns>
        protected T GetPlugin<T>() where T : class, ILCKPlugin
        {
            return LCKPlugins.Instance.GetPlugin<T>();
        }

        /// <summary>
        /// Get another plugin by name
        /// </summary>
        /// <param name="pluginName">The name of the plugin to retrieve</param>
        /// <returns>The plugin instance or null if not found</returns>
        protected ILCKPlugin GetPlugin(string pluginName)
        {
            return LCKPlugins.Instance.GetPlugin(pluginName);
        }
    }
} 