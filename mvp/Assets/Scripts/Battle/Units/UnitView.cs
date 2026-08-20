using UnityEngine;
using Mvp.Battle.Map;
using Mvp.Shared;
using Mvp.Battle.Commanders;
using Mvp.Battle.Vision;

namespace Mvp.Battle.Units
{
    /// <summary>
    /// Visual facade for one spawned unit. Builds a placeholder model per the
    /// 回退策略 (正式模型未完成：先用胶囊体/方块占位) — Infantry = capsule,
    /// Tank = box — tinted by team, plus a pooled world-space health bar.
    /// Exposes the anchors that movement/combat/facing controllers drive later.
    /// </summary>
    public sealed class UnitView : MonoBehaviour
    {
        public const float PlayerColorR = 0.30f;
        public const float PlayerColorG = 0.62f;
        public const float PlayerColorB = 1.00f;
        public const float EnemyColorR = 1.00f;
        public const float EnemyColorG = 0.35f;
        public const float EnemyColorB = 0.35f;

        public UnitRuntimeData Data { get; private set; }
        public Transform ModelRoot { get; private set; }
        public Transform HealthAnchor { get; private set; }

        UnitHealthBar _healthBar;
        Renderer _modelRenderer;
        Color _baseColor;
        float _flashTimer;
        bool _removedFromBattle;

        public bool IsFlashing { get; private set; }

        /// <summary>Sets up the placeholder model and health bar at <paramref name="worldPos"/>.</summary>
        public void Spawn(UnitRuntimeData data, Vector3 worldPos)
        {
            Data = data;
            data.WorldPosition = worldPos;
            var grid = BattleGridController.Instance;
            if (grid != null) data.GridPosition = grid.WorldToGrid(worldPos);

            transform.position = worldPos;

            if (UnitSelectionController.Instance != null)
            {
                UnitSelectionController.Instance.Register(this);
            }
            if (BattleSpatialIndex.Instance != null) BattleSpatialIndex.Instance.Register(this);

            ModelRoot = new GameObject("ModelRoot").transform;
            ModelRoot.SetParent(transform, false);
            ModelRoot.localScale = Vector3.one * 0.72f;

            BuildPlaceholder(data.Definition.Type, data.Team);

            HealthAnchor = new GameObject("HealthBarAnchor").transform;
            HealthAnchor.SetParent(transform, false);
            // Clearance above the tallest placeholder body so the bar never clips
            // into it (the default capsule primitive is 2 units tall when unscaled).
            HealthAnchor.localPosition = new Vector3(0f, ModelRoot.childCount > 0
                ? 0.95f
                : 0.9f, 0f);
        }

        public void AttachHealthBar()
        {
            if (_healthBar != null) return;
            if (HealthAnchor == null) return;
            var mgr = UnitHealthBarManager.Instance;
            if (mgr == null) return;

            var team = Data != null ? Data.Team : TeamId.Player;
            var barColor = team == TeamId.Player
                ? new Color(0.35f, 1f, 0.35f)
                : new Color(1f, 0.40f, 0.40f);
            _healthBar = mgr.Acquire(HealthAnchor, Vector3.zero, barColor);
            if (_healthBar != null && Data != null)
            {
                _healthBar.SetFill(Data.Definition != null && Data.Definition.MaxHealth > 0
                    ? (float)Data.CurrentHealth / Data.Definition.MaxHealth
                    : 1f);
            }
        }

        public void RefreshHealthBar()
        {
            if (_healthBar == null || Data == null || Data.Definition == null) return;
            _healthBar.SetFill((float)Data.CurrentHealth / Data.Definition.MaxHealth);
        }

        public void ReleaseHealthBar()
        {
            if (_healthBar == null) return;
            var mgr = UnitHealthBarManager.Instance;
            if (mgr != null) mgr.Release(_healthBar);
            _healthBar = null;
        }

        void OnDestroy()
        {
            RemoveFromBattleServices();
        }

        void BuildPlaceholder(UnitType type, TeamId team)
        {
            GameObject prim;
            float baseY;
            if (type == UnitType.Tank)
            {
                prim = GameObject.CreatePrimitive(PrimitiveType.Cube);
                prim.name = "TankBody";
                prim.transform.SetParent(ModelRoot, false);
                prim.transform.localScale = new Vector3(0.9f, 0.4f, 1.2f);
                baseY = 0.2f;
            }
            else
            {
                prim = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                prim.name = "Infantry";
                prim.transform.SetParent(ModelRoot, false);
                // The unscaled capsule is 2 tall; 0.5 on Y makes it a ~1-unit soldier.
                prim.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                baseY = 0.5f;
            }

            var renderer = prim.GetComponent<Renderer>();
            if (renderer != null)
            {
                _modelRenderer = renderer;
                _baseColor = team == TeamId.Player
                    ? new Color(PlayerColorR, PlayerColorG, PlayerColorB)
                    : new Color(EnemyColorR, EnemyColorG, EnemyColorB);
                renderer.material.color = _baseColor;
            }

            prim.transform.localPosition = new Vector3(0f, baseY, 0f);
        }

        /// <summary>Brief red flash used as "invalid target" feedback.</summary>
        public void FlashInvalid()
        {
            if (IsFlashing) return;
            IsFlashing = true;
            _flashTimer = 0.35f;
            if (_modelRenderer != null) _modelRenderer.material.color = new Color(1f, 0.25f, 0.25f);
        }

        /// <summary>Brief red flash shown when the unit takes a hit.</summary>
        public void FlashHit()
        {
            FlashInvalid();
        }

        /// <summary>Teardown when the unit dies: frees pooled UI and removes the view.</summary>
        public void Die()
        {
            ReleaseHealthBar();
            RemoveFromBattleServices();
            if (gameObject != null) Destroy(gameObject);
        }

        void RemoveFromBattleServices()
        {
            if (_removedFromBattle) return;
            _removedFromBattle = true;
            if (BattleSpatialIndex.Instance != null) BattleSpatialIndex.Instance.Unregister(this);
            if (CommanderGroupRegistry.Instance != null)
                CommanderGroupRegistry.Instance.NotifyUnitRemoved(this);
            if (UnitSelectionController.Instance != null)
                UnitSelectionController.Instance.Unregister(this);
        }

        void Update()
        {
            if (!IsFlashing) return;
            _flashTimer -= Time.deltaTime;
            if (_flashTimer > 0f) return;
            IsFlashing = false;
            if (_modelRenderer != null) _modelRenderer.material.color = _baseColor;
        }
    }
}
