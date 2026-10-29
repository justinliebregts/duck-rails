using Liv.Lck.DependencyInjection;
using UnityEngine;

namespace Liv.Lck
{
    /// <summary>
    /// Central place to decide whether LCK is supported on the current runtime platform, and to
    /// disable it cleanly where it is not.
    ///
    /// LCK ships native binaries only for Windows (x64) and Android (arm64). The macOS Unity Editor
    /// has no native libraries, so any P/Invoke into LCK Core or the capture pipeline throws
    /// <see cref="System.DllNotFoundException"/> and blocks developers from entering Play Mode to test
    /// their own game. On unsupported platforms this guard registers a <see cref="DisabledLckService"/>
    /// before scene load, which makes <see cref="LckServiceInitializer"/> skip building the real
    /// (native-backed) service chain. Native LCK Core initialization is separately short-circuited in
    /// <see cref="Core.LckCoreHandler"/> via <see cref="IsLckSupportedOnCurrentPlatform"/>.
    /// </summary>
    public static class LckPlatformGuard
    {
        /// <summary>
        /// True on platforms where LCK's native binaries are available. Currently false only on the
        /// macOS Unity Editor.
        /// </summary>
        public static bool IsLckSupportedOnCurrentPlatform => Application.platform != RuntimePlatform.OSXEditor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterDisabledServiceBeforeSceneLoad()
        {
            EnsureDisabledLckServiceRegistered();
        }

        /// <summary>
        /// On unsupported platforms, registers a <see cref="DisabledLckService"/> as the
        /// <see cref="ILckService"/> implementation if one is not already registered. Safe to call
        /// multiple times. No-op on supported platforms.
        /// </summary>
        public static void EnsureDisabledLckServiceRegistered()
        {
            if (IsLckSupportedOnCurrentPlatform)
            {
                return;
            }

            if (LckDiRegistry.Instance.GetRegistrations().ContainsKey(typeof(ILckService)))
            {
                return;
            }

            var container = LckDiContainer.Instance;
            container.AddSingleton<ILckService>(new DisabledLckService());
            container.Build();

            LckLog.Log("LCK is not supported on the macOS Unity Editor; registered a disabled LCK service so the game can still run.");
        }
    }
}
