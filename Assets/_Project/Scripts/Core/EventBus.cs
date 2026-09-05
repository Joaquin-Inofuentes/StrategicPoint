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
            if (!handlers.TryGetValue(typeof(T), out var existing)) return;
            if (!(existing is Action<T> action)) return;

            // BUG REAL: antes esto era un unico action.Invoke(evt). Un
            // delegado multicast se recorre en orden y la primera excepcion
            // CORTA el recorrido: todos los suscriptores que venian despues
            // se quedaban sin el evento, sin que nada lo dijera.
            //
            // Importa mucho aca porque los eventos de este juego los
            // escuchan quince o veinte vistas a la vez (barras de vida,
            // numeros flotantes, feed de bajas, viñeta de daño, minimapa,
            // condicion de victoria). Que una sola vista rota -- por
            // ejemplo una que quedo apuntando a un objeto ya destruido --
            // dejara sin EntityDiedEvent al BattleManager significa una
            // partida que no termina nunca, y el sintoma no se parece en
            // nada a la causa.
            //
            // Ahora cada suscriptor se invoca por separado: el que falla se
            // reporta con nombre y los demas reciben su evento igual.
            var lista = action.GetInvocationList();
            if (lista.Length == 1)
            {
                // Caso comun: un solo suscriptor, sin recorrer ni capturar.
                action.Invoke(evt);
                return;
            }

            for (int i = 0; i < lista.Length; i++)
            {
                try
                {
                    ((Action<T>)lista[i]).Invoke(evt);
                }
                catch (Exception e)
                {
                    var destino = lista[i].Target;
                    UnityEngine.Debug.LogError(
                        $"[EventBus] Un suscriptor de {typeof(T).Name} lanzo una excepcion y se lo salteo " +
                        $"({(destino != null ? destino.GetType().Name : "estatico")}). Los demas reciben el evento igual.\n{e}");
                }
            }
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
