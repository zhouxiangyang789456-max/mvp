using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Pooled effect object. Carries its EffectType so EffectPool can route it back
    /// to the correct pool. Auto-releases after <see cref="Spawn"/>'s autoRelease time.
    /// </summary>
    public sealed class PoolableEffect : MonoBehaviour
    {
        public EffectType Type;

        float _timer;
        bool _auto;

        public void Spawn(EffectType type, float autoRelease)
        {
            Type = type;
            _auto = autoRelease > 0f;
            _timer = autoRelease;
            gameObject.SetActive(true);
        }

        void Update()
        {
            if (!_auto) return;
            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _auto = false;
                var pool = EffectPool.Instance;
                if (pool != null) pool.Release(this);
                else gameObject.SetActive(false);
            }
        }

        public void Despawn()
        {
            _auto = false;
            gameObject.SetActive(false);
        }
    }
}
