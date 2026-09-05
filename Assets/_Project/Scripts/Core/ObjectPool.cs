using System.Collections.Generic;
using UnityEngine;

namespace SP.Core
{
    // Un objeto agrupable sabe reiniciarse al salir del pool y limpiarse al volver.
    public interface IPoolable
    {
        void OnSpawn();
        void OnDespawn();
    }

    // Pool genérico: nunca instancia en caliente salvo que se agote.
    public class ObjectPool<T> where T : Component, IPoolable
    {
        readonly T prefab;
        readonly Transform parent;
        readonly Stack<T> free = new Stack<T>();
        readonly HashSet<T> freeSet = new HashSet<T>();

        public ObjectPool(T prefab, int prewarm, Transform parent)
        {
            this.prefab = prefab;
            this.parent = parent;
            for (int i = 0; i < prewarm; i++)
            {
                var instance = Object.Instantiate(prefab, parent);
                instance.gameObject.SetActive(false);
                free.Push(instance);
                freeSet.Add(instance);
            }
        }

        public T Get()
        {
            T instance = null;

            // Se descartan las entradas MUERTAS antes de devolver nada. Una
            // instancia del pool puede haber sido destruida por fuera (una
            // recarga de escena, una limpieza de Editor, alguien que borro
            // el objeto a mano): en Unity eso deja un "null falso" que
            // sobrevive dentro de la pila, y devolverlo reventaba en la
            // linea siguiente con un NullReferenceException al tocar
            // .gameObject -- lejos de la causa y dificil de leer.
            while (free.Count > 0)
            {
                var candidata = free.Pop();
                freeSet.Remove(candidata);
                if (candidata != null) { instance = candidata; break; }
            }

            if (instance == null) instance = Object.Instantiate(prefab, parent);

            instance.gameObject.SetActive(true);
            instance.OnSpawn();
            prestadasCount++;
            return instance;
        }

        public void Release(T instance)
        {
            if (instance == null) return;
            if (!freeSet.Add(instance))
            {
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Release() llamado dos veces sobre la misma instancia ({instance.name}); se ignora la segunda liberacion para no duplicarla en el pool.");
                return;
            }
            instance.OnDespawn();
            instance.gameObject.SetActive(false);
            free.Push(instance);
            if (prestadasCount > 0) prestadasCount--;
        }

        // OJO con lo que Clear() puede y no puede hacer: destruye lo que
        // esta LIBRE. Las instancias prestadas (las que estan en uso) no
        // las conoce el pool -- salen por Get() y no vuelven hasta
        // Release() -- asi que llamar Clear() en medio de una partida deja
        // vivas todas las que esten en vuelo. Se avisa en vez de dar la
        // impresion de que el pool quedo vacio.
        public void Clear()
        {
            int prestadas = prestadasCount;
            while (free.Count > 0)
            {
                var instance = free.Pop();
                if (instance != null) Object.Destroy(instance.gameObject);
            }
            freeSet.Clear();

            if (prestadas > 0)
                Debug.LogWarning($"[ObjectPool<{typeof(T).Name}>] Clear() destruyo las libres, pero quedan {prestadas} " +
                                 "instancias prestadas que nadie devolvio: esas siguen vivas en la escena.");
            prestadasCount = 0;
        }

        // Cuantas salieron por Get() y todavia no volvieron por Release().
        int prestadasCount;
        public int PrestadasCount => prestadasCount;

        public int FreeCount => free.Count;
    }
}
