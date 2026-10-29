using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Liv.Lck
{
    /// <summary>
    /// Plugin Manager for LCK that manages all registered plugins
    /// </summary>
    public class LCKPlugins
    {
        private static LCKPlugins _instance;
        private static readonly object _lock = new object();
        
        private Dictionary<Type, ILCKPlugin> _pluginsByType;
        private Dictionary<string, ILCKPlugin> _pluginsByName;
        private bool _isInitialized;

        /// <summary>
        /// Singleton instance of LCKPlugins
        /// </summary>
        public static LCKPlugins Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new LCKPlugins();
                        }
                    }
                }
                return _instance;
            }
        }

        private LCKPlugins()
        {
            _pluginsByType = new Dictionary<Type, ILCKPlugin>();
            _pluginsByName = new Dictionary<string, ILCKPlugin>();
            _isInitialized = false;
        }

        /// <summary>
        /// Initialize all registered plugins with the LCK service
        /// </summary>
        /// <param name="lckService">The LCK service instance</param>
        public void Initialize(LckService lckService)
        {
            if (_isInitialized)
            {
                LckLog.LogWarning("LCKPlugins already initialized");
                return;
            }

            LckLog.Log($"Initializing {_pluginsByType.Count} plugins");

            foreach (var plugin in _pluginsByType.Values)
            {
                try
                {
                    plugin.Initialize(lckService);
                    LckLog.Log($"Initialized plugin: {plugin.GetType().Name}");
                }
                catch (Exception ex)
                {
                    LckLog.LogError($"Failed to initialize plugin {plugin.GetType().Name}: {ex.Message}");
                }
            }

            _isInitialized = true;
            LckLog.Log("LCKPlugins initialization complete");
        }

        /// <summary>
        /// Register a plugin with the plugin manager
        /// </summary>
        /// <param name="plugin">The plugin to register</param>
        internal void RegisterPlugin(ILCKPlugin plugin)
        {
            if (plugin == null)
            {
                LckLog.LogError("Attempted to register null plugin");
                return;
            }

            var pluginType = plugin.GetType();
            var pluginName = plugin.PluginName;

            if (_pluginsByType.ContainsKey(pluginType))
            {
                LckLog.LogWarning($"Plugin of type {pluginType.Name} is already registered");
                return;
            }

            if (_pluginsByName.ContainsKey(pluginName))
            {
                LckLog.LogWarning($"Plugin with name '{pluginName}' is already registered");
                return;
            }

            _pluginsByType[pluginType] = plugin;
            _pluginsByName[pluginName] = plugin;

            LckLog.Log($"Registered plugin: {pluginName} ({pluginType.Name})");
        }

        /// <summary>
        /// Check if a plugin of the specified type is registered
        /// </summary>
        /// <typeparam name="T">The plugin type to check for</typeparam>
        /// <returns>True if the plugin is registered, false otherwise</returns>
        public bool HasPlugin<T>() where T : class, ILCKPlugin
        {
            return _pluginsByType.ContainsKey(typeof(T));
        }

        /// <summary>
        /// Check if a plugin with the specified name is registered
        /// </summary>
        /// <param name="pluginName">The name of the plugin to check for</param>
        /// <returns>True if the plugin is registered, false otherwise</returns>
        public bool HasPlugin(string pluginName)
        {
            return _pluginsByName.ContainsKey(pluginName);
        }

        /// <summary>
        /// Get a plugin by type
        /// </summary>
        /// <typeparam name="T">The plugin type to retrieve</typeparam>
        /// <returns>The plugin instance or null if not found</returns>
        public T GetPlugin<T>() where T : class, ILCKPlugin
        {
            if (_pluginsByType.TryGetValue(typeof(T), out var plugin))
            {
                return plugin as T;
            }
            return null;
        }

        /// <summary>
        /// Get a plugin by name
        /// </summary>
        /// <param name="pluginName">The name of the plugin to retrieve</param>
        /// <returns>The plugin instance or null if not found</returns>
        public ILCKPlugin GetPlugin(string pluginName)
        {
            _pluginsByName.TryGetValue(pluginName, out var plugin);
            return plugin;
        }

        /// <summary>
        /// Get all registered plugins
        /// </summary>
        /// <returns>Enumerable of all registered plugins</returns>
        public IEnumerable<ILCKPlugin> GetAllPlugins()
        {
            return _pluginsByType.Values;
        }

        /// <summary>
        /// Get all plugins of a specific type
        /// </summary>
        /// <typeparam name="T">The plugin type to filter by</typeparam>
        /// <returns>Enumerable of plugins of the specified type</returns>
        public IEnumerable<T> GetPluginsOfType<T>() where T : class, ILCKPlugin
        {
            return _pluginsByType.Values.OfType<T>();
        }

        /// <summary>
        /// Unregister a plugin
        /// </summary>
        /// <param name="plugin">The plugin to unregister</param>
        public void UnregisterPlugin(ILCKPlugin plugin)
        {
            if (plugin == null) return;

            var pluginType = plugin.GetType();
            var pluginName = plugin.PluginName;

            _pluginsByType.Remove(pluginType);
            _pluginsByName.Remove(pluginName);

            LckLog.Log($"Unregistered plugin: {pluginName} ({pluginType.Name})");
        }

        /// <summary>
        /// Clear all registered plugins
        /// </summary>
        public void Clear()
        {
            _pluginsByType.Clear();
            _pluginsByName.Clear();
            _isInitialized = false;
            LckLog.Log("Cleared all registered plugins");
        }

        /// <summary>
        /// Get the count of registered plugins
        /// </summary>
        public int PluginCount => _pluginsByType.Count;

        /// <summary>
        /// Check if the plugin manager has been initialized
        /// </summary>
        public bool IsInitialized => _isInitialized;
    }
} 