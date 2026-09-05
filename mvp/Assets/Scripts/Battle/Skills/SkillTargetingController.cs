using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Map;
using Mvp.Battle.UI;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>
    /// Special-skill targeting input (远攻, 战斗技能系统开发文档 §7). UnitSelectionController
    /// funnels every left-click to TryHandleLeftClick while IsTargeting; right-click / Esc
    /// cancels. Confirm builds a SkillAttackPlan carrying the group's SkillSequence and
    /// hands it to CommanderGroupCommandController.CommandSkillAttack.
    /// </summary>
    public sealed class SkillTargetingController : MonoBehaviour
    {
        public static SkillTargetingController Instance { get; private set; }

        public bool IsTargeting { get { return _group != null; } }
        public CommanderGroupRuntime ActiveGroup { get { return _group; } }

        CommanderGroupRuntime _group;
        SkillDefinition _def;
        long _planSequence;

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnDestroy()
        {
            CancelTargeting();
            if (Instance == this) Instance = null;
        }

        /// <summary>Enters targeting for a special skill. Clears any prior targeting.</summary>
        public bool BeginTargeting(CommanderGroupRuntime group, SkillDefinition def, out string reason)
        {
            reason = null;
            if (group == null) { reason = "未激活编队"; return false; }
            if (def == null) { reason = "技能不存在"; return false; }
            CancelTargeting();
            _group = group;
            _def = def;
            _planSequence = group.Skills.SkillSequence;
            var preview = SkillRangePreview.Instance;
            if (preview != null) preview.ShowRange(group, def);
            var cursor = BattleCursorController.Instance;
            if (cursor != null) cursor.SetRangeHover(false);
            var status = BattleUiStatusText.Instance;
            if (status != null) status.SetStatus(def.DisplayName +
                (def.Id == SkillIds.Decoy
                    ? "：点击4格内空地放置分身，右键/ESC 取消"
                    : "：点击射程内目标格确认，右键/ESC 取消"));
            return true;
        }

        /// <summary>Exits targeting, clears the range highlight and invalidates any in-flight plan.</summary>
        public void CancelTargeting()
        {
            if (_group != null && !string.IsNullOrEmpty(_group.Skills.TargetingSkillId))
                _group.Skills.ResetModes(); // bumps SkillSequence so stale plans can never fire
            _group = null;
            _def = null;
            _planSequence = 0;
            var preview = SkillRangePreview.Instance;
            if (preview != null) preview.Hide();
            var cursor = BattleCursorController.Instance;
            if (cursor != null) cursor.SetRangeHover(false);
        }

        void Update()
        {
            if (!IsTargeting) return;
            var cam = Camera.main;
            var grid = BattleGridController.Instance;
            var preview = SkillRangePreview.Instance;
            if (cam == null || grid == null || preview == null) return;
            Vector2Int? hover = null;
            Vector2Int cell;
            if (grid.RayToGrid(cam.ScreenPointToRay(Input.mousePosition), out cell))
            {
                if (grid.InBounds(cell) && preview.Covers(cell)) hover = cell;
            }
            preview.SetHover(hover);
            var cursor = BattleCursorController.Instance;
            if (cursor != null) cursor.SetRangeHover(hover.HasValue);
        }

        /// <summary>Confirms a targeting click at a screen position (consumed by the selection controller).</summary>
        public void TryHandleLeftClick(Vector2 mousePosition)
        {
            if (!IsTargeting || _group == null) return;
            var cam = Camera.main;
            var grid = BattleGridController.Instance;
            var status = BattleUiStatusText.Instance;
            if (cam == null || grid == null) { CancelTargeting(); return; }

            Vector2Int cell;
            if (!grid.RayToGrid(cam.ScreenPointToRay(mousePosition), out cell))
            {
                if (status != null) status.SetStatus("请点击地图上的目标格");
                return;
            }

            var preview = SkillRangePreview.Instance;
            if (preview != null && !preview.Covers(cell))
            {
                if (status != null) status.SetStatus("目标格超出射程");
                return;
            }

            if (_def != null && _def.Id == SkillIds.Decoy)
            {
                string placementReason;
                int affectedCount;
                if (!TacticalDecoyService.TryPlace(_group, cell, Time.time,
                    out affectedCount, out placementReason))
                {
                    if (status != null) status.SetStatus(placementReason ?? "疑兵放置失败");
                    return;
                }
                if (status != null) status.SetStatus("疑兵部署完成：吸引 " + affectedCount + " 支敌方编队");
                CancelTargeting();
                return;
            }

            var selection = UnitSelectionController.Instance;
            UnitView occupant = selection != null ? selection.FindAtCell(cell) : null;
            if (occupant != null && occupant.Data != null &&
                occupant.Data.Team == _group.Team && occupant.Data.State != UnitState.Dead)
            {
                if (status != null) status.SetStatus("不能对友军所在格开火");
                return;
            }
            UnitView enemyOnCell = occupant;
            if (enemyOnCell != null && enemyOnCell.Data != null &&
                enemyOnCell.Data.State == UnitState.Dead) enemyOnCell = null;

            if (enemyOnCell != null)
            {
                var registry = CommanderGroupRegistry.Instance;
                var enemyGroup = registry != null ? registry.Find(enemyOnCell) : null;
                if (enemyGroup != null && ConcealmentService.IsConcealed(enemyGroup))
                {
                    if (status != null) status.SetStatus("该目标处于隐蔽状态");
                    return;
                }
            }

            var plan = BuildPlan(cell, enemyOnCell);
            if (plan == null)
            {
                if (status != null) status.SetStatus("无远程单位可及该目标");
                return;
            }

            var commands = CommanderGroupCommandController.Instance;
            if (commands == null || !commands.CommandSkillAttack(_group, plan))
            {
                if (status != null) status.SetStatus("远攻执行失败，请重试");
                return;
            }
            ApplyCooldown(plan);
            string name = _def != null ? _def.DisplayName : "远攻";
            if (status != null) status.SetStatus(name + " 完成");
            CancelTargeting();
        }

        SkillAttackPlan BuildPlan(Vector2Int cell, UnitView enemyOnCell)
        {
            if (_group == null || _def == null) return null;
            float now = Time.time;
            var plan = new SkillAttackPlan
            {
                SkillId = _def.Id,
                SkillSequence = _planSequence,
                TargetCell = cell
            };
            bool anyArea = false;
            for (int i = 0; i < _group.Members.Count; i++)
            {
                var member = _group.Members[i];
                if (!SkillEligibilityService.IsMemberEligible(_group, member, _def, now)) continue;
                if (!SkillRangeMath.IsCellInRange(cell, member, _def)) continue;
                plan.MemberIds.Add(member.Data.Id);
                if (member.Data.Definition != null && member.Data.Definition.AreaRadius > 0f)
                    anyArea = true;
            }
            if (plan.MemberIds.Count == 0) return null;
            plan.IsAreaAttack = enemyOnCell == null;
            if (plan.IsAreaAttack && !anyArea)
                return null; // ground strike but nobody can do area damage
            if (enemyOnCell != null && enemyOnCell.Data != null)
            {
                var registry = CommanderGroupRegistry.Instance;
                var enemyGroup = registry != null ? registry.Find(enemyOnCell) : null;
                plan.TargetGroupId = enemyGroup != null ? enemyGroup.GroupId : null;
            }
            return plan;
        }

        void ApplyCooldown(SkillAttackPlan plan)
        {
            if (_group == null || _def == null || _def.CooldownSeconds <= 0f) return;
            float until = Time.time + _def.CooldownSeconds;
            for (int i = 0; i < plan.MemberIds.Count; i++)
            {
                var st = _group.Skills.GetOrCreate(plan.MemberIds[i], _def.Id);
                st.State = SkillRuntimeState.Cooldown;
                st.ActiveUntil = 0f;
                st.CooldownUntil = until;
            }
        }
    }
}
