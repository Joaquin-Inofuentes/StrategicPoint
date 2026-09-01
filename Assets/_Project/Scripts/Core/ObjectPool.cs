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
            T instance;
            if (free.Count > 0)
            {
                instance = free.Pop();
                freeSet.Remove(instance);
            }
            else
            {
                instance = Object.Instantiate(prefab, parent);
            }
            instance.gameObject.SetActive(true);
            instance.OnSpawn();
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
        }

        public void Clear()
        {
            while (free.Count > 0)
            {
                var instance = free.Pop();
                if (instance != null) Object.Destroy(instance.gameObject);
            }
            freeSet.Clear();
        }

        public int FreeCount => free.Count;
    }
}
