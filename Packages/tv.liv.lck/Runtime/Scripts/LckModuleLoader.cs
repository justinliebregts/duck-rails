using System;
using System.Collections.Generic;
using Liv.Lck.DependencyInjection;

namespace Liv.Lck
{
    /// <summary>
    /// Allows external LCK modules to register with the DI container.
    /// </summary>
    public static class LckModuleLoader
    {
        private static readonly List<Action<LckDiContainer>> _moduleConfigurators = new List<Action<LckDiContainer>>();

        public static void RegisterModule(Action<LckDiContainer> configure, string name)
        {
            LckLog.Log($"LCK: Registered module - {name}");
            _moduleConfigurators.Add(configure);
        }
   
        internal static void Configure(LckDiContainer container)
        {
            foreach (var configure in _moduleConfigurators)
            {
                configure(container);
            }
        }
    }
}