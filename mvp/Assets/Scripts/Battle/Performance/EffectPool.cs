using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Pooled world-space effects (性能文档：EffectPool - MuzzleFlash/HitSpark/BulletTracer/InvalidClickFlash).
    /// Register a factory per EffectType then Get/Release. Pre-warmed at scene init.
    /// </summary>
    public sealed class EffectPool : MonoBehaviour
    {
        public static EffectPool Instance
        {
            get
            {
                if (_instance == null) BattleCore.Ensure();
                return _instance;
            }
        }
        static EffectPool _instance;

        readonly Dictionary<EffectType, ObjectPool<PoolableEffect>> _pools =
            new Dictionary<EffectType, ObjectPool<PoolableEffect>>();

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void RegisterFactory(EffectType type, Func<PoolableEffect> factory, int prewarm)
        {
            var pool = new ObjectPool<PoolableEffect>(factory, OnGet, OnRelease);
            pool.Prewarm(prewarm);
            _pools[type] = pool;
        }

        public PoolableEffect Get(EffectType type, Vector3 position, Quaternion rotation, float autoRelease = 0f)
        {
            if (!_pools.TryGetValue(type, out var pool))
            {
                Debug.LogWarning("[EffectPool] No factory registered for " + type);
                return null;
            }
            var fx = pool.Get();
            var t = fx.transform;
            t.SetParent(transform, true);
            t.SetPositionAndRotation(position, rotation);
            fx.Spawn(type, autoRelease);
            return fx;
        }

        public void Release(PoolableEffect fx)
        {
            if (fx == null) return;
            if (_pools.TryGetValue(fx.Type, out var pool))
            {
                fx.Despawn();
                pool.Release(fx);
            }
            else
            {
                fx.Despawn();
            }
        }

        /// <summary>
        /// Registers simple colored-quad placeholder factories for every effect type.
        /// BattleBootstrap calls this at battle start. Visual-only; art can replace later.
        /// </summary>
        public void RegisterDefaultEffects()
        {
            RegisterFactory(EffectType.MuzzleFlash,
                () => CreateEffect(EffectType.MuzzleFlash, new Color(1f, 0.92f, 0.4f)), 4);
            RegisterFactory(EffectType.HitSpark,
                () => CreateEffect(EffectType.HitSpark, new Color(1f, 0.45f, 0.25f)), 6);
            RegisterFactory(EffectType.BulletTracer,
                () => CreateEffect(EffectType.BulletTracer, new Color(1f, 0.95f, 0.6f)), 6);
            RegisterFactory(EffectType.InvalidClickFlash,
                () => CreateEffect(EffectType.InvalidClickFlash, new Color(1f, 0.3f, 0.3f)), 4);
        }

        static PoolableEffect CreateEffect(EffectType type, Color color)
        {
            var go = new GameObject("Fx_" + type);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = SharedSprites.White;
            r.color = color;
            r.sortingOrder = 90;
            var fx = go.AddComponent<PoolableEffect>();
            fx.Type = type;
            return fx;
        }

        static void OnGet(PoolableEffect fx) => fx.gameObject.SetActive(true);
        static void OnRelease(PoolableEffect fx) => fx.gameObject.SetActive(false);
    }
}
