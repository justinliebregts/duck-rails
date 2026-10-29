using System;
using UnityEngine;

namespace Liv.Lck.DependencyInjection
{
    public class LckDiContainer : MonoBehaviour
    {
        private static LckDiContainer _instance = null;
        public static LckDiContainer Instance {
            get
            {
                if (_instance == null)
                {
                    _instance = FindObjectOfType<LckDiContainer>();
                    if (_instance == null)
                    {
                        GameObject singletonObject = new GameObject();
                        DontDestroyOnLoad(singletonObject);
                        _instance = singletonObject.AddComponent<LckDiContainer>();
                        singletonObject.name = "LCK Service Singleton";
                        LckLog.Log("LCK: Created LCK Dependency Injection Service Singleton");
                    }
                }
                return _instance;
            }
        }
        
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }
      
        public void AddTransient<TService, TImplementation>() where TImplementation : TService where TService : class
        {
            LckDiRegistry.Instance.AddTransient<TService, TImplementation>();
        }

        public void AddSingleton<TService, TImplementation>() where TImplementation : TService where TService : class
        {
            LckDiRegistry.Instance.AddSingleton<TService, TImplementation>();
        }
        
        public void AddTransientFactory<TService>(Func<LckServiceProvider, TService> factory) where TService : class
        {
            LckDiRegistry.Instance.AddTransientFactory(factory);
        }
        
        public void AddSingletonFactory<TService>(Func<LckServiceProvider, TService> factory) where TService : class
        {
            LckDiRegistry.Instance.AddSingletonFactory(factory);
        }

        public void AddSingleton<TService>(TService instance) where TService : class
        {
            LckDiRegistry.Instance.AddSingleton(instance);
        }
        
        public void AddSingletonForward<TService, TForwardTo>() where TService : class where TForwardTo : class, TService
        {
            LckDiRegistry.Instance.AddSingletonForward<TService,TForwardTo>();
        }

        public T GetService<T>() where T : class
        {
            return LckDiRegistry.Instance.GetService<T>();
        }

        public bool HasService<T>() where T : class
        {
            return LckDiRegistry.Instance.HasService<T>();
        }

        public LckMonoBehaviourDependencyInjector GetInjector()
        {
            return LckDiRegistry.Instance.GetInjector();
        }
        
        private void OnDestroy()
        {
            if (_instance == this)
            {
                LckDiRegistry.Instance.Reset();
                _instance = null;
            }
        }

        public void Build()
        {
            LckDiRegistry.Instance.Build();
        }
    }
}
