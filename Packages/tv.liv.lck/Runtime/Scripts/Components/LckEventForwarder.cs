using System;
using System.Collections.Generic;
using static Liv.Lck.LckEvents;

namespace Liv.Lck
{
    /// <summary>
    /// Helper to forward internal eventBus events to public C# events
    /// </summary>
    /// <typeparam name="TEvent"></typeparam>
    /// <typeparam name="TResult"></typeparam>
    internal class LckEventForwarder<TEvent, TResult> : IDisposable
    {
        private readonly ILckEventBus _eventBus;
        private readonly Func<TEvent, TResult> _selector;
        private readonly Action<TResult> _forwardingAction;

        internal LckEventForwarder(ILckEventBus eventBus, Func<TEvent, TResult> selector, Action<TResult> forwardingAction)
        {
            _eventBus = eventBus;
            _selector = selector;
            _forwardingAction = forwardingAction;

            _eventBus.AddListener<TEvent>(OnEventReceived);
        }

        private void OnEventReceived(TEvent evt)
        {
            var result = _selector(evt);
            _forwardingAction(result);
        }

        public void Dispose()
        {
            _eventBus.RemoveListener<TEvent>(OnEventReceived);
        }
    }

    internal class LckPublicApiEventBridge : IDisposable
    {
        private readonly ILckEventBus _eventBus;
        private readonly List<IDisposable> _forwarders = new List<IDisposable>();

        internal LckPublicApiEventBridge(ILckEventBus eventBus)
        {
            _eventBus = eventBus;
        }

        public void Forward<TEvent, TResult>(Action<TResult> publicEventInvoker) 
            where TEvent : IEventWithResult<TResult> where TResult : ILckResult
        {
            _forwarders.Add(new LckEventForwarder<TEvent, TResult>(
                _eventBus,
                evt => evt.Result,
                publicEventInvoker
            ));
        }

        public void Forward<TEvent, TResult>(Func<TEvent, TResult> selector, Action<TResult> publicEventInvoker)
        {
            _forwarders.Add(new LckEventForwarder<TEvent, TResult>(
                _eventBus,
                selector,
                publicEventInvoker
            ));
        }

        public void Dispose()
        {
            foreach (var forwarder in _forwarders)
            {
                forwarder.Dispose();
            }
            _forwarders.Clear();
        }
    }
}