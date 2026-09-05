using UnityEngine;
using Mvp.Battle.Units;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>
    /// Pure range math for special skills (远攻). Ranges use Chebyshev grid distance
    /// (the same metric as attack range) and scale by SkillDefinition.RangeMultiplier
    /// (战斗技能系统开发文档 §7.2).
    /// </summary>
    public static class SkillRangeMath
    {
        public static int Chebyshev(Vector2Int a, Vector2Int b)
        {
            return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }

        /// <summary>Scaled min/max attack range for one member using the skill multiplier.</summary>
        public static void ComputeMemberRanges(UnitView member, SkillDefinition def,
            out int minRange, out int maxRange)
        {
            minRange = 0;
            maxRange = 0;
            if (member == null || member.Data == null || member.Data.Definition == null || def == null)
                return;
            float mult = def.RangeMultiplier > 0f ? def.RangeMultiplier : 1f;
            minRange = Mathf.RoundToInt(member.Data.Definition.AttackRangeMin * mult);
            maxRange = Mathf.Max(minRange,
                Mathf.RoundToInt(member.Data.Definition.AttackRangeMax * mult));
        }

        public static bool IsCellInRange(Vector2Int cell, UnitView member, SkillDefinition def)
        {
            if (member == null || member.Data == null || def == null) return false;
            int minRange, maxRange;
            ComputeMemberRanges(member, def, out minRange, out maxRange);
            int dist = Chebyshev(cell, member.Data.GridPosition);
            return dist >= minRange && dist <= maxRange;
        }

        /// <summary>True when <paramref name="cell"/> lies inside any eligible member's scaled range.</summary>
        public static bool IsCellCoveredByAny(Vector2Int cell, System.Collections.Generic.IReadOnlyList<UnitView> members,
            SkillDefinition def)
        {
            if (members == null) return false;
            for (int i = 0; i < members.Count; i++)
                if (members[i] != null && IsCellInRange(cell, members[i], def)) return true;
            return false;
        }
    }
}
