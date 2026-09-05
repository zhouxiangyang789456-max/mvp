using System.Collections.Generic;
using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Formation;
using Mvp.Battle.UI;
using Mvp.Battle.Units;
using Mvp.Battle.Vision;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>
    /// Skill command entry point (战斗技能系统开发文档 §3). Every skill is released from
    /// the currently active commander group; there is no player-unit-level control.
    ///
    /// - Persistent (坚守, 隐蔽): flips PersistentSkillMode and hands the group to
    ///   the matching service. Guard auto-attack ticks here (units in Holding fire at
    ///   enemies in range, never chase).
    /// - Special (远攻, 冲刺): 远攻 enters targeting; 冲刺 applies the move-speed buff.
    ///
    /// Activation bumps SkillSequence so any in-flight skill result from a previous mode
    /// is invalidated. Commander switching must call CancelGroupSkillState.
    /// </summary>
    public sealed class GroupSkillController : MonoBehaviour
    {
        public static GroupSkillController Instance { get; private set; }

        readonly List<UnitView> _guardBuffer = new List<UnitView>(8);

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        void OnEnable()
        {
            BattleTickService.MediumTick += OnMediumTick;
        }

        void OnDisable()
        {
            BattleTickService.MediumTick -= OnMediumTick;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>Activates a skill for the active commander group.</summary>
        public bool TryActivate(CommanderGroupRuntime group, string skillId, out string reason)
        {
            reason = null;
            var def = SkillCatalog.Get(skillId);
            if (def == null) { reason = "技能不存在"; return false; }

            // A new skill release always clears any in-flight targeting / persistent mode
            // so the two input paths can never overlap.
            var targeting = SkillTargetingController.Instance;
            if (targeting != null && targeting.IsTargeting) targeting.CancelTargeting();

            if (!SkillEligibilityService.CanActivate(group, def, out reason)) return false;
            switch (def.Category)
            {
                case SkillCategory.Persistent:
                    return ActivatePersistent(group, def, out reason);
                default:
                    return ActivateSpecial(group, def, out reason);
            }
        }

        /// <summary>Exits the currently active persistent mode (if it matches <paramref name="skillId"/>).</summary>
        public bool TryExitPersistentMode(CommanderGroupRuntime group, string skillId, out string reason)
        {
            reason = null;
            if (group == null || group.Skills.PersistentMode == PersistentSkillMode.None)
            {
                reason = "当前无常驻技能";
                return false;
            }
            bool matches = (skillId == SkillIds.Guard &&
                            group.Skills.PersistentMode == PersistentSkillMode.Guard) ||
                           (skillId == SkillIds.Concealment &&
                            group.Skills.PersistentMode == PersistentSkillMode.Concealment);
            if (!matches)
            {
                reason = "该技能未激活";
                return false;
            }
            var commands = CommanderGroupCommandController.Instance;
            if (commands != null) commands.InterruptPersistentModes(group);
            var status = BattleUiStatusText.Instance;
            if (status != null) status.SetStatus("已退出" + (skillId == SkillIds.Guard ? "坚守" : "隐蔽"));
            return true;
        }

        /// <summary>Cleans up all skill state for a group (commander switch / battle teardown).</summary>
        public void CancelGroupSkillState(CommanderGroupRuntime group)
        {
            if (group == null) return;
            var targeting = SkillTargetingController.Instance;
            if (targeting != null && targeting.ActiveGroup == group) targeting.CancelTargeting();
            var commands = CommanderGroupCommandController.Instance;
            if (commands != null) commands.InterruptPersistentModes(group);
        }

        bool ActivatePersistent(CommanderGroupRuntime group, SkillDefinition def, out string reason)
        {
            reason = null;
            var commands = CommanderGroupCommandController.Instance;
            if (def.Id == SkillIds.Guard)
            {
                var formation = FormationController.Instance;
                if (commands == null || formation == null)
                {
                    reason = "阵型或指挥系统不可用";
                    return false;
                }
                if (!formation.BeginCombatEdit(group, true, out reason)) return false;
                var selection = UnitSelectionController.Instance;
                if (selection != null) selection.ClearSelection();
                var battleUi = BattleUiController.Instance;
                if (battleUi != null) battleUi.RefreshCombatFormationControls();
                var status = BattleUiStatusText.Instance;
                if (status != null) status.SetStatus("坚守：调整阵型，完成后点击确认坚守");
                return true;
            }
            if (def.Id == SkillIds.Concealment)
            {
                group.Skills.ResetModes();
                group.Skills.PersistentMode = PersistentSkillMode.Concealment;
                ConcealmentService.BeginConcealment(group);
                var status = BattleUiStatusText.Instance;
                if (status != null) status.SetStatus("隐蔽：全员静止且位于森林格时进入隐蔽");
                return true;
            }
            reason = "技能不可用";
            return false;
        }

        bool ActivateSpecial(CommanderGroupRuntime group, SkillDefinition def, out string reason)
        {
            reason = null;
            if (def.Id == SkillIds.LongRange || def.Id == SkillIds.Decoy)
            {
                if (def.Id == SkillIds.LongRange &&
                    SkillEligibilityService.GetEligibleMemberCount(group, def, Time.time) == 0)
                {
                    reason = "无远程单位可用（冷却中或已阵亡）";
                    return false;
                }
                group.Skills.ResetModes();
                group.Skills.TargetingSkillId = def.Id;
                var targeting = SkillTargetingController.Instance;
                if (targeting == null || !targeting.BeginTargeting(group, def, out reason))
                {
                    group.Skills.ResetModes();
                    if (reason == null) reason = "瞄准系统不可用";
                    return false;
                }
                return true;
            }
            if (def.Id == SkillIds.Sprint)
            {
                group.Skills.ResetModes(); // special skills must not leave a persistent mode on
                if (!SprintEffectService.TryActivate(group, Time.time, out reason)) return false;
                var status = BattleUiStatusText.Instance;
                if (status != null) status.SetStatus("冲刺：全坦克编队加速移动");
                return true;
            }
            if (def.Id == SkillIds.Taunt)
            {
                int affectedCount;
                if (!TauntEffectService.TryActivate(group, Time.time,
                    out affectedCount, out reason)) return false;
                var status = BattleUiStatusText.Instance;
                if (status != null)
                    status.SetStatus("嘲讽生效：激活 " + affectedCount + " 支敌方编队，持续 5 秒");
                return true;
            }
            reason = "技能不可用";
            return false;
        }

        // ---- 坚守 auto attack ---------------------------------------------------

        void OnMediumTick()
        {
            if (BattlePhaseState.Current != BattlePhase.Combat) return;
            TickGuardAutoAttack();
        }

        /// <summary>
        /// Groups in Holding (Guard mode) fire at the nearest enemy inside each member's
        /// attack range. Uses per-unit CommandAttack with allowPursuit=false so the group
        /// command sequence and formation are untouched, and the group never chases.
        /// </summary>
        void TickGuardAutoAttack()
        {
            var registry = CommanderGroupRegistry.Instance;
            var commands = CommanderGroupCommandController.Instance;
            var spatial = BattleSpatialIndex.Instance;
            var combat = UnitCombatController.Instance;
            if (registry == null || commands == null || spatial == null || combat == null) return;

            for (int i = 0; i < registry.Groups.Count; i++)
            {
                var group = registry.Groups[i];
                if (group == null || group.IsDefeated) continue;
                if (group.Skills.PersistentMode != PersistentSkillMode.Guard) continue;
                if (group.State != CommanderGroupState.Holding) continue;

                for (int m = 0; m < group.Members.Count; m++)
                {
                    var member = group.Members[m];
                    if (member == null || member.Data == null || member.Data.State == UnitState.Dead ||
                        member.Data.Definition == null) continue;
                    if (combat.IsEngaged(member)) continue;
                    if (member.Data.State == UnitState.Moving ||
                        member.Data.State == UnitState.Chasing) continue;

                    int range = Mathf.RoundToInt(member.Data.Definition.AttackRangeMax);
                    if (range <= 0) continue;
                    _guardBuffer.Clear();
                    spatial.QueryEnemiesChebyshev(member.Data.GridPosition, range, group.Team, _guardBuffer);

                    UnitView best = null;
                    int bestDist = int.MaxValue;
                    for (int b = 0; b < _guardBuffer.Count; b++)
                    {
                        var enemy = _guardBuffer[b];
                        if (enemy == null || enemy.Data == null || enemy.Data.State == UnitState.Dead)
                            continue;
                        var enemyGroup = registry.Find(enemy);
                        if (enemyGroup != null && ConcealmentService.IsConcealed(enemyGroup)) continue;
                        int dist = SkillRangeMath.Chebyshev(member.Data.GridPosition,
                            enemy.Data.GridPosition);
                        if (dist >= bestDist) continue;
                        best = enemy;
                        bestDist = dist;
                    }
                    if (best != null) combat.CommandAttack(member, best, false);
                }
            }
        }
    }
}
