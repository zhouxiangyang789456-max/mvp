using System;
using System.Collections.Generic;

namespace Mvp.Progression
{
    public static class TraitEffectCatalogExtensions
    {
        /// <summary>True when the card carries the given gameplay tag.</summary>
        public static bool HasTag(this TraitCardDefinition def, string tag)
        {
            if (def == null || string.IsNullOrEmpty(tag)) return false;
            return def.EnsureTagSet().Contains(tag);
        }

        /// <summary>
        /// Local Chinese summary of the card's real, phase-1-visible effects, e.g.
        /// "攻击力 +20%\n编队生命低于 35% 时生效\n作用范围：本编队全体". Returns null
        /// when the card has no structured effects.
        /// </summary>
        public static string BuildEffectSummary(this TraitCardDefinition def)
        {
            if (def == null || def.Effects == null || def.Effects.Count == 0) return null;
            var lines = new List<string>();
            for (int i = 0; i < def.Effects.Count; i++)
            {
                var e = def.Effects[i];
                if (e == null) continue;
                string effectLine = EffectSummaryLine(e);
                string triggerLine = TriggerLine(e);
                string scopeLine = ScopeLine(e);
                if (effectLine != null) lines.Add(effectLine);
                if (triggerLine != null) lines.Add(triggerLine);
                if (scopeLine != null) lines.Add(scopeLine);
                if (i < def.Effects.Count - 1) lines.Add(string.Empty);
            }
            return lines.Count > 0 ? string.Join("\n", lines.ToArray()) : null;
        }

        /// <summary>True when the card's effects all belong to the phase-1 whitelist.</summary>
        public static bool IsSupportedInPhase1(this TraitCardDefinition def)
        {
            if (def == null || def.Effects == null || def.Effects.Count == 0) return false;
            for (int i = 0; i < def.Effects.Count; i++)
            {
                if (def.Effects[i] == null) continue;
                if (!IsEffectSupported(def.Effects[i])) return false;
            }
            return true;
        }

        /// <summary>True when a single effect is within the phase-1 whitelist.</summary>
        public static bool IsEffectSupported(TraitEffect e)
        {
            if (e == null) return false;
            switch (e.Kind)
            {
                case TraitEffectKind.ModifyMaxHealth:
                case TraitEffectKind.ModifyAttackPower:
                case TraitEffectKind.ModifyAttackCooldown:
                case TraitEffectKind.ModifyMoveSpeed:
                case TraitEffectKind.ReduceIncomingDamage:
                    break;
                default:
                    return false;
            }
            switch (e.Trigger)
            {
                case TraitTriggerKind.Always:
                case TraitTriggerKind.WhileGroupHealthBelowPercent:
                case TraitTriggerKind.WhileGroupHealthAbovePercent:
                    break;
                default:
                    return false;
            }
            return e.Scope == TraitTargetScope.AllGroupMembers;
        }

        /// <summary>单条效果的中文描述,如 "攻击力 +20%" / "敌人移速 -30%" / "眩晕 1.5 秒"。</summary>
        public static string EffectSummaryLine(TraitEffect e)
        {
            if (e == null) return null;
            switch (e.Kind)
            {
                case TraitEffectKind.ModifyMaxHealth:
                    return "最大生命 " + Percent(e.Value);
                case TraitEffectKind.ModifyAttackPower:
                    return "攻击力 " + Percent(e.Value);
                case TraitEffectKind.ModifyAttackCooldown:
                    return "攻击冷却 " + Percent(-e.Value);
                case TraitEffectKind.ModifyMoveSpeed:
                    return "移动速度 " + Percent(e.Value);
                case TraitEffectKind.ReduceIncomingDamage:
                    return "所受伤害 " + Percent(-e.Value);
                case TraitEffectKind.ModifyHealingReceived:
                    return "治疗量 " + Percent(e.Value);
                case TraitEffectKind.GrantOpeningShield:
                    return "开场护盾 " + Percent(e.Value);
                case TraitEffectKind.ModifyCommanderMorale:
                    return "指挥官士气 " + Percent(e.Value);
                case TraitEffectKind.ModifyAttackRange:
                    return "攻击范围 " + Percent(e.Value);
                case TraitEffectKind.ModifyCriticalChance:
                    return "暴击率 " + Percent(e.Value);
                case TraitEffectKind.ModifyCriticalDamage:
                    return "暴击伤害 " + Percent(e.Value);
                case TraitEffectKind.ModifyLifeSteal:
                    return "吸血 " + Percent(e.Value);
                case TraitEffectKind.GrantShield:
                    return "护盾 " + Percent(e.Value);
                case TraitEffectKind.ApplyBurn:
                    return "灼烧 每秒 " + Percent(e.Value) + " 伤害" +
                        (e.DurationSeconds > 0f ? " / " + Seconds(e.DurationSeconds) : string.Empty);
                case TraitEffectKind.SlowEnemy:
                    return "敌人移速 " + Percent(-e.Value);
                case TraitEffectKind.ModifyDamageVsSlowed:
                    return "对减速敌增伤 " + Percent(e.Value);
                case TraitEffectKind.ExecuteBonus:
                    return "斩杀增伤 " + Percent(e.Value);
                case TraitEffectKind.ReflectDamage:
                    return "反弹伤害 " + Percent(e.Value);
                case TraitEffectKind.EconomyModifier:
                    return "金币获取 " + Percent(e.Value);
                case TraitEffectKind.GoldOnWin:
                    return "胜利额外金币 " + Amount(e.Value);
                case TraitEffectKind.GoldOnKill:
                    return "击杀额外金币 " + Amount(e.Value);
                case TraitEffectKind.FirstStrikeBonus:
                    return "先手攻击 " + Percent(e.Value);
                case TraitEffectKind.ReduceEnemyDefense:
                    return "破甲 " + Percent(e.Value);
                case TraitEffectKind.StunEnemy:
                    return "眩晕 " + Seconds(e.Value);
                case TraitEffectKind.RegenerateHealth:
                    return "每秒回复生命 " + Percent(e.Value);
                default:
                    return null;
            }
        }

        static string TriggerLine(TraitEffect e)
        {
            int pct = (int)Math.Round(e.TriggerValue * 100f);
            switch (e.Trigger)
            {
                case TraitTriggerKind.Always:
                    return "始终生效";
                case TraitTriggerKind.OnBattleStart:
                    return "开战时生效";
                case TraitTriggerKind.WhileGroupHealthBelowPercent:
                    return "编队生命低于 " + pct + "% 时生效";
                case TraitTriggerKind.WhileGroupHealthAbovePercent:
                    return "编队生命高于 " + pct + "% 时生效";
                case TraitTriggerKind.WhileGroupIdle:
                    return "编队静止时生效";
                case TraitTriggerKind.OnFirstHitTaken:
                    return "首次受击时生效";
                case TraitTriggerKind.OnFirstAttack:
                    return "首次攻击时生效";
                case TraitTriggerKind.WhileFormationIntact:
                    return "阵型完整时生效";
                case TraitTriggerKind.AfterRegroup:
                    return "重新集结后生效";
                case TraitTriggerKind.OnEnemyDeath:
                    return "击杀敌人时生效";
                case TraitTriggerKind.OnHit:
                    return "攻击命中时生效";
                case TraitTriggerKind.OnReceiveHit:
                    return "受到攻击时生效";
                case TraitTriggerKind.WhileEnemyBurning:
                    return "敌人处于灼烧时生效";
                case TraitTriggerKind.WhileEnemySlowed:
                    return "敌人处于减速时生效";
                case TraitTriggerKind.WhileEnemyLowHealth:
                    return "敌人生命低于 " + pct + "% 时生效";
                case TraitTriggerKind.BasedOnHealthLost:
                    return "按损失生命比例生效";
                case TraitTriggerKind.BasedOnGold:
                    return "按当前金币生效";
                case TraitTriggerKind.OnFriendlyUnitLost:
                    return "我方损失单位时生效";
                default:
                    return null;
            }
        }

        static string ScopeLine(TraitEffect e)
        {
            switch (e.Scope)
            {
                case TraitTargetScope.AllGroupMembers:
                    return "作用范围：本编队全体";
                case TraitTargetScope.CommanderOnly:
                    return "作用范围：指挥官";
                case TraitTargetScope.FrontlineMembers:
                    return "作用范围：前排成员";
                case TraitTargetScope.RangedMembers:
                    return "作用范围：远程成员";
                case TraitTargetScope.LowestHealthMember:
                    return "作用范围：生命最低成员";
                case TraitTargetScope.EnemyAll:
                    return "作用范围：全体敌人";
                case TraitTargetScope.EnemyFrontline:
                    return "作用范围：敌方前排";
                case TraitTargetScope.EnemyLowestHealth:
                    return "作用范围：敌方生命最低";
                case TraitTargetScope.EnemyBurning:
                    return "作用范围：灼烧中的敌人";
                case TraitTargetScope.EnemySlowed:
                    return "作用范围：减速中的敌人";
                case TraitTargetScope.NearbyEnemies:
                    return "作用范围：编队周围敌人";
                default:
                    return null;
            }
        }

        static string Percent(float value)
        {
            int p = (int)Math.Round(value * 100f);
            return p >= 0 ? "+" + p + "%" : p + "%";
        }

        static string Seconds(float value)
        {
            return value.ToString("0.#") + " 秒";
        }

        static string Amount(float value)
        {
            int v = (int)Math.Round(value);
            return "+" + v;
        }
    }
}
