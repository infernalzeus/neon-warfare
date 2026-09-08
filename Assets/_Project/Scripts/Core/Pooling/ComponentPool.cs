using System.Collections.Generic;
using UnityEngine;

namespace NW.Core.Pooling
{
    /// <summary>
    /// Prefab pool for views (gems, units, particles, damage numbers). Nothing
    /// gameplay-visible is instantiated at runtime steady-state (doc 07 §9).
    /// </summary>
    public sealed class ComponentPool<T> where T : Component
    {
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly Stack<T> _free = new();

        public ComponentPool(T prefab, Transform parent, int prewarm = 0)
        {
            _prefab = prefab;
            _parent = parent;
            for (int i = 0; i < prewarm; i++) Release(CreateNew());
        }

        public T Acquire()
        {
            T item = _free.Count > 0 ? _free.Pop() : CreateNew();
            item.gameObject.SetActive(true);
            return item;
        }

        public void Release(T item)
        {
            item.gameObject.SetActive(false);
            item.transform.SetParent(_parent, worldPositionStays: false);
            _free.Push(item);
        }

        private T CreateNew()
        {
            T item = Object.Instantiate(_prefab, _parent);
            item.gameObject.SetActive(false);
            return item;
        }
    }
}
