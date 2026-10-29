using System;
using System.Collections.Generic;

namespace Liv.Lck.DependencyInjection
{
    public class LckDiRegistry
    {
        private static LckDiRegistry _instance;
        public static LckDiRegistry Instance => _instance ??= new LckDiRegistry();

        private LckDiCollection _collection = new LckDiCollection();
        private LckServiceProvider _provider;
        private LckMonoBehaviourDependencyInjector _lckMonoBehaviourDependencyInjector;
        
        public void AddTransient<TService, TImplementation>() where TService : class where TImplementation : TService
        {
            _collection.AddTransient<TService, TImplementation>();
        }
        
        public void AddTransientFactory<TService>(Func<LckServiceProvider, TService> factory) where TService : class
        {
            _collection.AddTransientFactory(factory);
        }

        public void AddSingleton<TService, TImplementation>() where TService : class where TImplementation : TService
        {
            _collection.AddSingleton<TService, TImplementation>();
        }

        public void AddSingleton<TService>(TService instance) where TService : class
        {
            _collection.AddSingleton(instance);
        }
        
        public void AddSingletonFactory<TService>(Func<LckServiceProvider, TService> factory) where TService : class
        {
            _collection.AddSingletonFactory(factory);
        }
        
        public void AddSingletonForward<TService, TForwardTo>() where TService : class where TForwardTo : class, TService
        {
            _collection.AddSingletonForward<TService,TForwardTo>();
        }

        public T GetService<T>() where T : class
        {
            try
            {
                if (_provider == null)
                {
                    LckLog.Log("Service provider not built yet, building now.");
                    Build();
                }

                return _provider?.GetService<T>();
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error: GetService failed for type {typeof(T).Name}. Exception: {ex.Message}");
                return null;
            }
        }

        public bool HasService<T>() where T : class
        {
            try
            {
                return _provider?.GetService<T>() != null;
            }
            catch
            {
                return false;
            }
        }

        public LckMonoBehaviourDependencyInjector GetInjector()
        {
            return _lckMonoBehaviourDependencyInjector;
        }

        public void Build()
        {
            try
            {
                _provider = _collection.Build();
                _lckMonoBehaviourDependencyInjector = new LckMonoBehaviourDependencyInjector(_provider);
                LckLog.Log("LCK DI provider built successfully.");
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error: Failed to build the service provider. Exception: {ex.Message}");
            }
        }

        public Dictionary<Type, LckDiServiceRegistration> GetRegistrations()
        {
            return _collection.GetRegistrations();
        }

        public void Reset()
        {
            try
            {
                _provider?.Dispose();

                _collection = new LckDiCollection();
                _provider = null;
                _lckMonoBehaviourDependencyInjector = null;
                LckLog.Log("LCK DI registry has been reset.");
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error: Failed to reset the DI registry. Exception: {ex.Message}");
            }
        }
    }
}
