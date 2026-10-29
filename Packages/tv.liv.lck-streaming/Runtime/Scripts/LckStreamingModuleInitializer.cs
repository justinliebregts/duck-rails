using UnityEngine;

namespace Liv.Lck.Streaming
{
    /// <summary>
    /// Automatically initialised by Unity when the app loads.
    /// Registers the streaming services with the core LCK DI container.
    /// </summary>
    internal static class StreamingModuleInitializer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            LckModuleLoader.RegisterModule(container => 
            {
                container.AddSingleton<ILckNativeStreamingService, LckNativeStreamingService>();
                container.AddSingleton<ILckStreamer, LckStreamer>();
                LckLog.Log($"LCK: Loaded module - Liv.Lck.Streaming");

            }, "Liv.Lck.Streaming");
        }
    }
}