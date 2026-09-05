using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Map;
using Mvp.Battle.Units;
using Mvp.Shared;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>
    /// Group-level skill eligibility: member tags, terrain, cooldown and active-group
    /// rules (战斗技能系统开发文档 §4.1 / §6). Pure static queries; the skill bar,
    /// GroupSkillController and tests all share this one source of truth.
    /// </summary>
    public static class SkillEligibilityService
    {
        /// <summary>Basic command eligibility shared by every skill: an active, alive
        /// group during the combat phase.</summary>
        public static bool IsGroupEligible(CommanderGroupRuntime group, out string reason)
        {
            reason = null;
            if (group == null)
            {
                reason = "未激活编队";
                return false;
            }
            if (group.IsDefeated)
            {
                reason = "编队已被消灭";
                return false;
            }
            if (BattlePhaseState.Current != BattlePhase.Combat)
            {
                reason = "仅在战斗阶段";
                return false;
            }
            return true;
        }

        /// <summary>Skill activation eligibility, including skill-specific reasons for
        /// the tooltip / grey-out. <paramref name="now"/> defaults to Time.time.</summary>
        public static bool CanActivate(CommanderGroupRuntime group, SkillDefinition def,
            out string reason, float? now = null)
        {
            reason = null;
            float time = now ?? Time.time;
            if (def == null)
            {
                reason = "技能不存在";
                return false;
            }
            if (!IsGroupEligible(group, out reason)) return false;

            if (def.Category == SkillCategory.Persistent)
            {
                if (def.Id == SkillIds.Guard) return true;
                if (def.Id == SkillIds.Concealment)
                {
                    if (ConcealmentService.IsInRevealLock(group, time))
                    {
                        reason = "暴露后短时间内禁止再次隐蔽";
                        return false;
                    }
                    if (!ConcealmentService.MeetsConcealmentPrerequisites(group))
                    {
                        reason = "需全员静止且位于森林格";
                        return false;
                    }
                    return true;
                }
                reason = "技能不可用";
                return false;
            }

            // ---- special skills ----
            if (def.Id == SkillIds.LongRange)
            {
                if (!HasRangedMember(group))
                {
                    reason = "编队无远程单位";
                    return false;
                }
                return true;
            }
            if (def.Id == SkillIds.Sprint)
            {
                if (!SprintEffectService.IsAllTankGroup(group))
                {
                    reason = "编队不全是坦克单位";
                    return false;
                }
                if (SprintEffectService.IsActive(group, time))
                {
                    reason = "冲刺已在进行";
                    return false;
                }
                if (SprintEffectService.GetRemainingCooldown(group, time) > 0.01f)
                {
                    reason = "冲刺冷却中";
                    return false;
                }
                return true;
            }
            if (def.Id == SkillIds.Taunt)
            {
                if (TauntEffectService.GetRemainingCooldown(group, time) > 0.01f)
                {
                    reason = "嘲讽冷却中";
                    return false;
                }
                return true;
            }
            if (def.Id == SkillIds.Decoy)
            {
                if (TacticalDecoyService.GetRemainingCooldown(group, time) > 0.01f)
                {
                    reason = "疑兵冷却中";
                    return false;
                }
                return true;
            }
            reason = "技能不可用";
            return false;
        }

        /// <summary>True when the group has at least one alive member carrying the
        /// skill's required tags (远攻 → Ranged).</summary>
        public static bool HasRangedMember(CommanderGroupRuntime group)
        {
            if (group == null) return false;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead ||
                    member.Data.Definition == null) continue;
                if ((member.Data.Definition.Tags & UnitTag.Ranged) != 0) return true;
            }
            return false;
        }

        /// <summary>Per-member eligibility: alive, carries the skill's required tags,
        /// and not currently on cooldown for that skill.</summary>
        public static bool IsMemberEligible(CommanderGroupRuntime group, UnitView member,
            SkillDefinition def, float now)
        {
            if (member == null || member.Data == null || member.Data.State == UnitState.Dead ||
                member.Data.Definition == null || def == null) return false;
            if (def.RequiredUnitTags != UnitTag.None &&
                (member.Data.Definition.Tags & def.RequiredUnitTags) == 0) return false;
            if (group != null)
            {
                var state = group.Skills.GetOrCreate(member.Data.Id, def.Id);
                if (state.State == SkillRuntimeState.Cooldown && state.CooldownUntil > now)
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Maps a skill terrain requirement to the grid terrain. 草丛 is currently
        /// represented by TerrainType.Forest (SkillTerrainKind doc), so the mapping
        /// lives here and not scattered through the skill logic.
        /// </summary>
        public static bool TerrainMatches(SkillTerrainKind kind, TerrainType terrain)
        {
            switch (kind)
            {
                case SkillTerrainKind.Forest:
                    return terrain == TerrainType.Forest;
                default:
                    return true;
            }
        }

        /// <summary>Max remaining cooldown (seconds) across alive members for a special skill.</summary>
        public static float GetMaxRemainingCooldown(CommanderGroupRuntime group, SkillDefinition def, float now)
        {
            if (group == null || def == null) return 0f;
            if (def.Id == SkillIds.Taunt)
                return TauntEffectService.GetRemainingCooldown(group, now);
            if (def.Id == SkillIds.Decoy)
                return TacticalDecoyService.GetRemainingCooldown(group, now);
            float max = 0f;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (member == null || member.Data == null || member.Data.State == UnitState.Dead) continue;
                var state = group.Skills.GetOrCreate(member.Data.Id, def.Id);
                if (state.SkillId != def.Id || state.State != SkillRuntimeState.Cooldown) continue;
                if (state.CooldownUntil > now) max = Mathf.Max(max, state.CooldownUntil - now);
            }
            return max;
        }

        /// <summary>Count of alive members that can currently cast a special skill.</summary>
        public static int GetEligibleMemberCount(CommanderGroupRuntime group, SkillDefinition def, float now)
        {
            if (group == null || def == null) return 0;
            int count = 0;
            for (int i = 0; i < group.Members.Count; i++)
            {
                var member = group.Members[i];
                if (IsMemberEligible(group, member, def, now)) count++;
            }
            return count;
        }
    }
}
