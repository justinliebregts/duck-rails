using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static Liv.Lck.DependencyInjection.LckDiServiceRegistration.ServiceLifetime;

namespace Liv.Lck.DependencyInjection
{
    public class LckServiceProvider : IDisposable
    {
        private readonly Dictionary<Type, LckDiServiceRegistration> _registrations;
        private bool _disposed = false;
        
        internal LckServiceProvider(Dictionary<Type, LckDiServiceRegistration> registrations)
        {
            _registrations = registrations;
        }

        public T GetService<T>() where T : class
        {
            if (_disposed) throw new ObjectDisposedException(nameof(LckServiceProvider));
            
            try
            {
                return (T)ProvideService(typeof(T));
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error: Failed to get service of type {typeof(T).Name}. Exception: {ex.Message}");
                return null;
            }
        }

        public object GetService(Type serviceType)
        {
            try
            {
                return ProvideService(serviceType);
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error: Failed to get service of type {serviceType.Name}. Exception: {ex.Message}");
                return null;
            }
        }
        
        private object ProvideService(Type serviceType)
        {
            if (!_registrations.TryGetValue(serviceType, out var registration))
            {
                LckLog.LogError($"LCK Error: Service of type {serviceType.Name} has not been registered.");
                throw new InvalidOperationException($"Service of type {serviceType.Name} has not been registered.");
            }

            try
            {
                if (registration.ForwardToServiceType != null)
                {
                    return ProvideService(registration.ForwardToServiceType);
                }

                if (registration.Instance != null)
                {
                    return registration.Instance;
                }

                if (registration.Factory != null)
                {
                    var instanceFromFactory = registration.Factory(this);
                    if (registration.Lifetime == Singleton)
                    {
                        registration.SetInstance(instanceFromFactory);
                    }
                    return instanceFromFactory;
                }

                var constructors = registration.ImplementationType.GetConstructors(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (constructors.Length < 1)
                {
                    LckLog.LogError($"LCK Error: {registration.ImplementationType} has no public constructors.");
                    return null;
                }

                return CreateInstance(constructors, registration);
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error: Failed to provide service {serviceType.Name}. Exception: {ex.Message}");
                throw;
            }
        }

        private object CreateInstance(ConstructorInfo[] constructors, LckDiServiceRegistration registration)
        {
            var constructor = constructors.OrderByDescending(c => c.GetParameters().Length).First();
            var parameters = constructor.GetParameters();
            var parameterInstances = new List<object>();

            try
            {
                for (var index = 0; index < parameters.Length; index++)
                {
                    var parameter = parameters[index];
                    var parameterInstance = ProvideService(parameter.ParameterType);
                    if (parameterInstance == null)
                    {
                        LckLog.LogError($"LCK Error: Failed to resolve parameter '{parameter.Name}' of type '{parameter.ParameterType.Name}' for '{registration.ImplementationType.Name}'.");
                        return null;
                    }
                    parameterInstances.Add(parameterInstance);
                }

                var serviceInstance = constructor.Invoke(parameterInstances.ToArray());

                if (registration.Lifetime == Singleton)
                {
                    registration.SetInstance(serviceInstance);
                }
                
                LckLog.Log($"Successfully instantiated {registration.ImplementationType.Name}.");
                return serviceInstance;
            }
            catch (Exception ex)
            {
                LckLog.LogError($"LCK Error: {registration.ImplementationType} failed to instantiate. Exception: {ex.InnerException?.Message ?? ex.Message}");
                return null;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;

            LckLog.Log("Disposing LCK Service Provider and all disposable singleton services.");
            
            foreach (var registration in _registrations.Values)
            {
                if (registration.Lifetime == Singleton && registration.Instance != null)
                {
                    if (registration.Instance is IDisposable disposableService)
                    {
                        try
                        {
                            disposableService.Dispose();
                        }
                        catch (Exception ex)
                        {
                            LckLog.LogError($"LCK Error: Failed to dispose service of type {registration.Instance.GetType().Name}. Exception: {ex.Message}");
                        }
                    }
                }
            }
            _disposed = true;
        }
    }
}
