using System;
using System.Collections.Generic;

namespace SP.Core
{
    // Bus de eventos desacoplado: los emisores publican, los oyentes se suscriben.
    // Ninguno de los dos se conoce entre sí.
    public sealed class EventBus
    {
        public static readonly EventBus Instance = new EventBus();

        readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>();

        public IDisposable Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (handlers.TryGetValue(type, out var existing))
                handlers[type] = Delegate.Combine(existing, handler);
            else
                handlers[type] = handler;

            return new ActionDisposable(() => Unsubscribe(handler));
        }

        public void Publish<T>(T evt)
        {
            if (handlers.TryGetValue(typeof(T), out var existing) && existing is Action<T> action)
                action.Invoke(evt);
        }

        void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!handlers.TryGetValue(type, out var existing)) return;
            var result = Delegate.Remove(existing, handler);
            if (result == null) handlers.Remove(type);
            else handlers[type] = result;
        }

        // Solo para tests/reinicios de escena: vacía todas las suscripciones.
        public void ClearAll() => handlers.Clear();

        sealed class ActionDisposable : IDisposable
        {
            Action onDispose;
            public ActionDisposable(Action onDispose) => this.onDispose = onDispose;
            public void Dispose()
            {
                onDispose?.Invoke();
                onDispose = null;
            }
        }
    }
}
