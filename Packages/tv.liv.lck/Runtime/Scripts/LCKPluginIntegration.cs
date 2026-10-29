using UnityEngine;
using System.Linq;
using Liv.Lck.DependencyInjection;

namespace Liv.Lck
{
    /// <summary>
    /// Helper class for integrating the plugin system with LCK service
    /// </summary>
    public static class LCKPluginIntegration
    {
        /// <summary>
        /// Initialize all plugins after creating the LCK service
        /// </summary>
        /// <param name="lckService">The created LCK service instance</param>
        public static void InitializePlugins(LckService lckService)
        {
            if (lckService == null)
            {
                LckLog.LogError("Cannot initialize plugins with null LCK service");
                return;
            }

            LCKPlugins.Instance.Initialize(lckService);
        }

        /// <summary>
        /// Shutdown all plugins before destroying the LCK service
        /// </summary>
        public static void ShutdownPlugins()
        {
            var plugins = LCKPlugins.Instance.GetAllPlugins();
            foreach (var plugin in plugins)
            {
                try
                {
                    plugin.Shutdown();
                }
                catch (System.Exception ex)
                {
                    LckLog.LogError($"Failed to shutdown plugin {plugin.PluginName}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Get a plugin by type with null checking
        /// </summary>
        /// <typeparam name="T">The plugin type to retrieve</typeparam>
        /// <returns>The plugin instance or null if not found</returns>
        public static T GetPlugin<T>() where T : class, ILCKPlugin
        {
            return LCKPlugins.Instance.GetPlugin<T>();
        }

        /// <summary>
        /// Check if a plugin is available by type
        /// </summary>
        /// <typeparam name="T">The plugin type to check for</typeparam>
        /// <returns>True if the plugin is available, false otherwise</returns>
        public static bool HasPlugin<T>() where T : class, ILCKPlugin
        {
            return LCKPlugins.Instance.HasPlugin<T>();
        }

        /// <summary>
        /// Log information about all registered plugins
        /// </summary>
        public static void LogPluginInfo()
        {
            var plugins = LCKPlugins.Instance.GetAllPlugins();
            LckLog.Log($"Registered plugins ({plugins.Count()}):");
            
            foreach (var plugin in plugins)
            {
                LckLog.Log($"  - {plugin.PluginName} v{plugin.PluginVersion} ({plugin.GetType().Name})");
            }
        }
    }

    /// <summary>
    /// MonoBehaviour that demonstrates how to use the plugin system
    /// </summary>
    public class LCKPluginManager : MonoBehaviour
    {
        [InjectLck] 
        private ILckService _lckService;
        
        [Header("Plugin Management")]
        [SerializeField] private bool autoInitializePlugins = true;
        [SerializeField] private bool logPluginInfo = true;

        private void Start()
        {
            if (autoInitializePlugins)
            {
                // Get the LCK service and initialize plugins
                if (_lckService != null)
                {
                    // Get the LCK service and initialize plugins
                    LCKPluginIntegration.InitializePlugins((LckService)_lckService);
                    
                    if (logPluginInfo)
                    {
                        LCKPluginIntegration.LogPluginInfo();
                    }
                }
                else
                {
                    LckLog.LogError($"LCK service has not been injected.");
                }
            }
        }

        private void OnDestroy()
        {
            // Shutdown plugins when this component is destroyed
            LCKPluginIntegration.ShutdownPlugins();
        }

        /// <summary>
        /// Example of how to use plugins in your game code
        /// </summary>
        [ContextMenu("Test Plugin Access")]
        public void TestPluginAccess()
        {
            // Check if a specific plugin is available
            if (LCKPluginIntegration.HasPlugin<ExamplePlugin>())
            {
                var examplePlugin = LCKPluginIntegration.GetPlugin<ExamplePlugin>();
                examplePlugin.DoSomething();
            }
            else
            {
                LckLog.LogWarning("ExamplePlugin not found");
            }

            // Check if another plugin is available
            if (LCKPluginIntegration.HasPlugin<AnotherExamplePlugin>())
            {
                var anotherPlugin = LCKPluginIntegration.GetPlugin<AnotherExamplePlugin>();
                anotherPlugin.DoSomethingElse();
            }
            else
            {
                LckLog.LogWarning("AnotherExamplePlugin not found");
            }
        }
    }
} 