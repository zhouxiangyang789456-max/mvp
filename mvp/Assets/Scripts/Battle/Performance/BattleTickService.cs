using System;
using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Central tick scheduler (性能文档：中央 Tick 调度).
    ///
    /// FastTick   every frame   - input, smooth unit movement, camera
    /// MediumTick every 0.1s    - attack range checks, health bar billboard
    /// SlowTick   every 0.3s    - chase re-path, minimap refresh
    ///
    /// Subscribers use the static events. Never do heavy work in per-system Update().
    /// </summary>
    public sealed class BattleTickService : MonoBehaviour
    {
        public static BattleTickService Instance
        {
            get
            {
                if (_instance == null) BattleCore.Ensure();
                return _instance;
            }
        }
        static BattleTickService _instance;

        [SerializeField] float _mediumInterval = 0.1f;
        [SerializeField] float _slowInterval = 0.3f;

        float _mediumTimer;
        float _slowTimer;

        /// <summary>Fires every frame.</summary>
        public static event Action FastTick;
        /// <summary>Fires every 0.1s.</summary>
        public static event Action MediumTick;
        /// <summary>Fires every 0.3s.</summary>
        public static event Action SlowTick;

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        void Update()
        {
            FastTick?.Invoke();

            _mediumTimer += Time.deltaTime;
            if (_mediumTimer >= _mediumInterval)
            {
                _mediumTimer = 0f;
                MediumTick?.Invoke();
            }

            _slowTimer += Time.deltaTime;
            if (_slowTimer >= _slowInterval)
            {
                _slowTimer = 0f;
                SlowTick?.Invoke();
            }
        }
    }
}
