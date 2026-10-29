using System;
using System.Collections.Generic;
using static Liv.Lck.DependencyInjection.LckDiServiceRegistration.ServiceLifetime;

namespace Liv.Lck.DependencyInjection
{
    public class LckDiCollection
    {
        private readonly Dictionary<Type, LckDiServiceRegistration> _registrations = new Dictionary<Type, LckDiServiceRegistration>();

        /// <summary>
        /// Registers a service which will be created every time it is requested.
        /// </summary>
        public void AddTransient<TService, TImplementation>() where TService : class where TImplementation : TService
        {
            try
            {
                _registrations[typeof(TService)] = new LckDiServiceRegistration(typeof(TService), Transient, typeof(TImplementation));
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error adding transient {typeof(TService).Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers a service which will be created every time it is requested by the supplied factory.
        /// This will not have any dependencies injected.
        /// </summary>
        public void AddTransientFactory<TService>(Func<LckServiceProvider, TService> factory) where TService : class
        {
            try
            {
                _registrations[typeof(TService)] = new LckDiServiceRegistration(typeof(TService), Transient, p => factory(p));
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error adding transient factory {typeof(TService).Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers a service which will be created only once and shared across all requests.
        /// </summary>
        public void AddSingleton<TService, TImplementation>() where TService : class where TImplementation : TService
        {
            try
            {
                _registrations[typeof(TService)] = new LckDiServiceRegistration(typeof(TService), Singleton, typeof(TImplementation));
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error adding singleton {typeof(TService).Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers a service which will be created only once and shared across all requests by the supplied factory.
        /// This will not have any dependencies injected.
        /// </summary>
        public void AddSingletonFactory<TService>(Func<LckServiceProvider, TService> factory) where TService : class
        {
            try
            {
                _registrations[typeof(TService)] = new LckDiServiceRegistration(typeof(TService), Singleton, p => factory(p));
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error adding singleton factory {typeof(TService).Name}: {ex.Message}");
            }
        }

        /// <summary>
        /// Registers a service with a pre constructed instance.
        /// </summary>
        public void AddSingleton<TService>(TService instance) where TService : class
        {
            try
            {
                _registrations[typeof(TService)] = new LckDiServiceRegistration(typeof(TService), instance);
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error adding singleton instance {typeof(TService).Name}: {ex.Message}");
            }
        }
        
        /// <summary>
        /// Registers a type to be forwarded to another types service.
        /// </summary>
        public void AddSingletonForward<TService, TForwardTo>() where TService : class where TForwardTo : class, TService
        {
            try
            {
                _registrations[typeof(TService)] = new LckDiServiceRegistration(typeof(TService), typeof(TForwardTo));
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error adding singleton forward for {typeof(TService).Name}: {ex.Message}");
            }
        }
        
        public Dictionary<Type, LckDiServiceRegistration> GetRegistrations()
        {
            return _registrations;
        }
        
        public LckDiServiceRegistration GetRegistration(Type serviceType)
        {
            _registrations.TryGetValue(serviceType, out var registration);
            return registration;
        }

        public LckServiceProvider Build()
        {
            LckLog.Log("Building LCK Service Provider.");
            return new LckServiceProvider(new Dictionary<Type, LckDiServiceRegistration>(_registrations));
        }
    }
}
