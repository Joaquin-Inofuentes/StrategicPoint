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

        public ObjectPool(T prefab, int prewarm, Transform parent)
        {
            this.prefab = prefab;
            this.parent = parent;
            for (int i = 0; i < prewarm; i++)
            {
                var instance = Object.Instantiate(prefab, parent);
                instance.gameObject.SetActive(false);
                free.Push(instance);
            }
        }

        public T Get()
        {
            T instance = free.Count > 0 ? free.Pop() : Object.Instantiate(prefab, parent);
            instance.gameObject.SetActive(true);
            instance.OnSpawn();
            return instance;
        }

        public void Release(T instance)
        {
            instance.OnDespawn();
            instance.gameObject.SetActive(false);
            free.Push(instance);
        }

        public int FreeCount => free.Count;
    }
}
