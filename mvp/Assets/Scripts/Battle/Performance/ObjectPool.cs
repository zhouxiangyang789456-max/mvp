using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Simple allocation-free component pool. Used by EffectPool and UiPool.
    /// Items are pre-warmed at scene init; Get/Release never allocates.
    /// </summary>
    public sealed class ObjectPool<T> where T : Component
    {
        readonly Stack<T> _pool = new Stack<T>();
        readonly Func<T> _factory;
        readonly Action<T> _onGet;
        readonly Action<T> _onRelease;

        public int Count => _pool.Count;

        public ObjectPool(Func<T> factory, Action<T> onGet, Action<T> onRelease)
        {
            _factory = factory;
            _onGet = onGet;
            _onRelease = onRelease;
        }

        public void Prewarm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                T item = _factory();
                _onRelease?.Invoke(item);
                _pool.Push(item);
            }
        }

        public T Get()
        {
            T item = _pool.Count > 0 ? _pool.Pop() : _factory();
            _onGet?.Invoke(item);
            return item;
        }

        public void Release(T item)
        {
            _onRelease?.Invoke(item);
            _pool.Push(item);
        }
    }
}
