using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Acquires pooled health bars for units and tracks them for release.
    /// Bars are parented to the unit anchor and follow automatically.
    /// </summary>
    public sealed class UnitHealthBarManager : MonoBehaviour
    {
        public static UnitHealthBarManager Instance
        {
            get
            {
                if (_instance != null) return _instance;
                if (BattleCore.IsShuttingDown) return null;
                if (!Application.isPlaying) return null;
                BattleCore.Ensure();
                return _instance;
            }
        }
        static UnitHealthBarManager _instance;

        readonly List<ActiveBar> _active = new List<ActiveBar>();

        void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
        }

        void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>Acquires a health bar attached to <paramref name="anchor"/> with a local offset.</summary>
        public UnitHealthBar Acquire(Transform anchor, Vector3 localOffset, Color color)
        {
            var ui = UiPool.Instance.Get(UiPoolType.UnitHealthBar, Vector3.zero, Quaternion.identity, Vector3.one);
            if (ui == null || ui.Bar == null) return null;

            ui.transform.SetParent(anchor, false);
            ui.transform.localPosition = localOffset;
            ui.transform.localRotation = Quaternion.identity;
            ui.transform.localScale = Vector3.one;
            ui.Bar.SetColor(color);

            _active.Add(new ActiveBar(ui));
            return ui.Bar;
        }

        public void Release(UnitHealthBar bar)
        {
            if (bar == null) return;
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].Ui.Bar != bar) continue;
                var ui = _active[i].Ui;
                _active.RemoveAt(i);
                UiPool.Instance.Release(ui);
                return;
            }
        }

        public void ReleaseAll()
        {
            // Do not lazily re-create the pool during scene teardown; the OnDestroy
            // chains of UnitSelectionController / FormationController / SkillRangePreview
            // can fire after BattleCore has been destroyed, and accessing
            // UiPool.Instance would otherwise spawn ~200 fresh UI GameObjects.
            if (BattleCore.IsShuttingDown || !Application.isPlaying)
            {
                _active.Clear();
                return;
            }
            for (int i = 0; i < _active.Count; i++)
            {
                UiPool.Instance.Release(_active[i].Ui);
            }
            _active.Clear();
        }

        struct ActiveBar
        {
            public PoolableUi Ui;
            public ActiveBar(PoolableUi ui) { Ui = ui; }
        }
    }
}
