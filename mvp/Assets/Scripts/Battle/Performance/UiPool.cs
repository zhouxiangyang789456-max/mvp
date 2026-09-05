using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Pooled world-space UI elements (性能文档：UiPool - UnitHealthBar/SelectionRing/
    /// MoveTargetMarker/DeploymentCellHighlight). Pre-warmed at scene init.
    /// </summary>
    public sealed class UiPool : MonoBehaviour
    {
        public static UiPool Instance
        {
            get
            {
                if (_instance == null) BattleCore.Ensure();
                return _instance;
            }
        }
        static UiPool _instance;

        readonly Dictionary<UiPoolType, ObjectPool<PoolableUi>> _pools =
            new Dictionary<UiPoolType, ObjectPool<PoolableUi>>();

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        public void RegisterFactory(UiPoolType type, Func<PoolableUi> factory, int prewarm)
        {
            var pool = new ObjectPool<PoolableUi>(factory, OnGet, OnRelease);
            pool.Prewarm(prewarm);
            _pools[type] = pool;
        }

        public PoolableUi Get(UiPoolType type, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            if (!_pools.TryGetValue(type, out var pool))
            {
                Debug.LogWarning("[UiPool] No factory registered for " + type);
                return null;
            }
            var ui = pool.Get();
            var t = ui.transform;
            t.SetParent(transform, true);
            t.SetPositionAndRotation(position, rotation);
            t.localScale = scale;
            ui.Spawn(type);
            return ui;
        }

        public void Release(PoolableUi ui)
        {
            if (ui == null) return;
            if (_pools.TryGetValue(ui.Type, out var pool))
            {
                ui.Despawn();
                pool.Release(ui);
            }
            else
            {
                ui.Despawn();
            }
        }

        /// <summary>
        /// Registers placeholder factories for every UiPoolType. BattleBootstrap calls
        /// this at battle start. Deployment highlight pre-warms 25 (5x5 max range).
        /// </summary>
        public void RegisterDefaultUi()
        {
            RegisterFactory(UiPoolType.UnitHealthBar, CreateHealthBar, 8);
            RegisterFactory(UiPoolType.SelectionRing,
                () => CreateQuad(UiPoolType.SelectionRing, new Color(0.949f, 0.788f, 0.298f, 0.7f)), 4); // #F2C94C
            RegisterFactory(UiPoolType.MoveTargetMarker,
                () => CreateQuad(UiPoolType.MoveTargetMarker, new Color(0.4f, 1f, 0.5f, 0.9f)), 4);
            RegisterFactory(UiPoolType.DeploymentCellHighlight,
                () => CreateQuad(UiPoolType.DeploymentCellHighlight, new Color(0.35f, 1f, 0.45f, 0.45f)), 25);
            RegisterFactory(UiPoolType.AttackRangeHighlight,
                () => CreateQuad(UiPoolType.AttackRangeHighlight, new Color(1f, 0.56f, 0.18f, 0.32f)), 81);
            RegisterFactory(UiPoolType.SkillRangeHighlight,
                () => CreateQuad(UiPoolType.SkillRangeHighlight, new Color(0.2f, 0.6f, 1f, 0.25f)), 121);
            RegisterFactory(UiPoolType.SkillRangeCursor,
                () => CreateQuad(UiPoolType.SkillRangeCursor, new Color(1f, 0.2f, 0.2f, 0.55f)), 4);
            RegisterFactory(UiPoolType.SkillBlindZone,
                () => CreateQuad(UiPoolType.SkillBlindZone, new Color(0.3f, 0.3f, 0.3f, 0.45f)), 8);
        }

        static PoolableUi CreateQuad(UiPoolType type, Color color)
        {
            var go = new GameObject("Ui_" + type);
            var r = go.AddComponent<SpriteRenderer>();
            r.sprite = SharedSprites.White;
            r.color = color;
            r.sortingOrder = 80;
            var ui = go.AddComponent<PoolableUi>();
            ui.Type = type;
            return ui;
        }

        static PoolableUi CreateHealthBar()
        {
            var go = new GameObject("Ui_UnitHealthBar");
            var ui = go.AddComponent<PoolableUi>();
            ui.Type = UiPoolType.UnitHealthBar;
            var bar = go.AddComponent<UnitHealthBar>();
            bar.Build();
            ui.Bar = bar;
            return ui;
        }

        static void OnGet(PoolableUi ui) => ui.gameObject.SetActive(true);
        static void OnRelease(PoolableUi ui) => ui.gameObject.SetActive(false);
    }
}
