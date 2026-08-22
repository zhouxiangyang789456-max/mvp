using System;
using System.Collections.Generic;

namespace Mvp.Progression
{
    public static class TraitEffectCatalogExtensions
    {
        /// <summary>True when the card carries the given gameplay tag.</summary>
        public static bool HasTag(this TraitCardDefinition def, string tag)
        {
            if (def == null || def.Tags == null || string.IsNullOrEmpty(tag)) return false;
            for (int i = 0; i < def.Tags.Count; i++)
                if (def.Tags[i] == tag) return true;
            return false;
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
                string effectLine = EffectLine(e);
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

        static string EffectLine(TraitEffect e)
        {
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
                case TraitTriggerKind.WhileGroupHealthBelowPercent:
                    return "编队生命低于 " + pct + "% 时生效";
                case TraitTriggerKind.WhileGroupHealthAbovePercent:
                    return "编队生命高于 " + pct + "% 时生效";
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
                default:
                    return null;
            }
        }

        static string Percent(float value)
        {
            int p = (int)Math.Round(value * 100f);
            return p >= 0 ? "+" + p + "%" : p + "%";
        }
    }
}
