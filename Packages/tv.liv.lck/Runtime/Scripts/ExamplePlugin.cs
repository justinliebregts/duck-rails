using UnityEngine;

namespace Liv.Lck
{
    /// <summary>
    /// Example plugin that demonstrates how to use the LCK plugin system
    /// </summary>
    public class ExamplePlugin : LCKPluginBase
    {
        public override string PluginName => "ExamplePlugin";
        public override string PluginVersion => "1.0.0";

        protected override void OnInitialize()
        {
            // Example: Check if another plugin is available
            if (HasPlugin<AnotherExamplePlugin>())
            {
                var otherPlugin = GetPlugin<AnotherExamplePlugin>();
                LckLog.Log($"Found another plugin: {otherPlugin.PluginName}");
            }

            // Example: Check if a plugin exists by name
            if (HasPlugin("SomeOtherPlugin"))
            {
                var otherPlugin = GetPlugin("SomeOtherPlugin");
                LckLog.Log($"Found plugin by name: {otherPlugin.PluginName}");
            }

            // Example: Subscribe to LCK service events
            LckService.OnRecordingStarted += OnRecordingStarted;
            LckService.OnRecordingStopped += OnRecordingStopped;

            LckLog.Log($"ExamplePlugin initialized with LCK service");
        }

        protected override void OnShutdown()
        {
            // Example: Unsubscribe from LCK service events
            if (LckService != null)
            {
                LckService.OnRecordingStarted -= OnRecordingStarted;
                LckService.OnRecordingStopped -= OnRecordingStopped;
            }

            LckLog.Log($"ExamplePlugin shutdown");
        }

        private void OnRecordingStarted(LckResult result)
        {
            if (result.Success)
            {
                LckLog.Log("ExamplePlugin: Recording started successfully");
            }
            else
            {
                LckLog.LogError($"ExamplePlugin: Recording failed to start: {result.Message}");
            }
        }

        private void OnRecordingStopped(LckResult result)
        {
            if (result.Success)
            {
                LckLog.Log("ExamplePlugin: Recording stopped successfully");
            }
            else
            {
                LckLog.LogError($"ExamplePlugin: Recording failed to stop: {result.Message}");
            }
        }

        /// <summary>
        /// Example method that demonstrates plugin functionality
        /// </summary>
        public void DoSomething()
        {
            if (!IsInitialized)
            {
                LckLog.LogWarning("ExamplePlugin is not initialized");
                return;
            }

            LckLog.Log("ExamplePlugin is doing something!");
        }
    }

    /// <summary>
    /// Another example plugin to demonstrate plugin interaction
    /// </summary>
    public class AnotherExamplePlugin : LCKPluginBase
    {
        public override string PluginName => "AnotherExamplePlugin";
        public override string PluginVersion => "1.0.0";

        protected override void OnInitialize()
        {
            LckLog.Log($"AnotherExamplePlugin initialized");
        }

        protected override void OnShutdown()
        {
            LckLog.Log($"AnotherExamplePlugin shutdown");
        }

        public void DoSomethingElse()
        {
            LckLog.Log("AnotherExamplePlugin is doing something else!");
        }
    }
} 