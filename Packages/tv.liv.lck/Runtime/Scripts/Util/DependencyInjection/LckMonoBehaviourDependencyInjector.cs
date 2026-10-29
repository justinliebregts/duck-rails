using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Liv.Lck.DependencyInjection
{
    public class LckMonoBehaviourDependencyInjector
    {
        private const BindingFlags _bindingFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
        private readonly LckServiceProvider _lckServiceProvider;

        public LckMonoBehaviourDependencyInjector(LckServiceProvider lckServiceProvider)
        {
            _lckServiceProvider = lckServiceProvider;
        }

        /// <summary>
        /// Inject dependencies into a MonoBehaviour instance.
        /// </summary>
        public void Inject(MonoBehaviour instance)
        {
            if (!IsInjectable(instance))
                return;

            var type = instance.GetType();

            // Traverse the hierarchy up to MonoBehaviour
            while (type != null && type != typeof(MonoBehaviour))
            {
                // Get all fields declared on the current type
                var fields = type.GetFields(_bindingFlags);
                foreach (var field in fields)
                {
                    if (Attribute.IsDefined(field, typeof(InjectLckAttribute)))
                    {
                        var dependency = _lckServiceProvider.GetService(field.FieldType);
                        if (dependency != null)
                        {
                            field.SetValue(instance, dependency);
                        }
                    }
                }

                // Get all properties declared on the current type
                var properties = type.GetProperties(_bindingFlags);
                
                // Inject into properties
                foreach (var property in properties)
                {
                    if (Attribute.IsDefined(property, typeof(InjectLckAttribute)) && property.CanWrite)
                    {
                        var dependency = _lckServiceProvider.GetService(property.PropertyType);
                        if (dependency != null)
                        {
                            property.SetValue(instance, dependency);
                        }
                    }
                }

                // Get all methods with the InjectLckAttribute
                var injectableMethods = type.GetMethods(_bindingFlags)
                    .Where(member => Attribute.IsDefined(member, typeof(InjectLckAttribute)));

                foreach (var injectableMethod in injectableMethods)
                {
                    var requiredParameters = injectableMethod.GetParameters()
                        .Select(parameter => parameter.ParameterType)
                        .ToArray();

                    // Inject into methods
                    var arguments = new List<object>();
                    foreach (var parameter in requiredParameters)
                    {
                        var dependency = _lckServiceProvider.GetService(parameter);
                        if (dependency != null)
                        {
                            arguments.Add(dependency);
                        }
                        else
                        {
                            throw new Exception($"Failed to inject dependency {parameter} into method '{injectableMethod.Name}' of class '{type.Name}'.");
                        }
                    }
                    injectableMethod.Invoke(instance, arguments.ToArray());
                }

                type = type.BaseType;
            }
        }

        private static bool IsInjectable(object obj)
        {
            var type = obj.GetType();
            while (type != null && type != typeof(object))
            {
                var members = type.GetMembers(_bindingFlags);
                if (members.Any(member => Attribute.IsDefined(member, typeof(InjectLckAttribute))))
                {
                    return true;
                }
                type = type.BaseType;
            }
            return false;
        }
    }
}
