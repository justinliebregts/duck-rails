using System;
using System.Collections.Generic;
using UnityEngine.Scripting;

namespace Liv.Lck
{
    internal class LckEventBus : ILckEventBus
    {
        private readonly Dictionary<Type, Delegate> _delegates = new Dictionary<Type, Delegate>();
        
        [Preserve]
        public LckEventBus() {}

        public void AddListener<T>(Action<T> listener)
        {
            if (!_delegates.ContainsKey(typeof(T)))
            {
                _delegates[typeof(T)] = null;
            }

            _delegates[typeof(T)] = (Action<T>)_delegates[typeof(T)] + listener;
        }

        public void RemoveListener<T>(Action<T> listener)
        {
            if (_delegates.TryGetValue(typeof(T), out var currentDelegate))
            {
                var newDelegate = (Action<T>)currentDelegate - listener;

                if (newDelegate == null)
                {
                    _delegates.Remove(typeof(T));
                }
                else
                {
                    _delegates[typeof(T)] = newDelegate;
                }
            }
        }

        public void Trigger<T>(T eventData)
        {
            if (_delegates.TryGetValue(typeof(T), out var currentDelegate))
            {
                (currentDelegate as Action<T>)?.Invoke(eventData);
            }
        }
    }
}