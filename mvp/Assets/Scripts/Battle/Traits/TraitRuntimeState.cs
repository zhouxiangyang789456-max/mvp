using System.Collections.Generic;
using Mvp.Progression;

namespace Mvp.Battle.Traits
{
    /// <summary>
    /// One resolved effect of an equipped card, with an Active flag recomputed by
    /// TraitEffectService.RefreshConditions every MediumTick. Keeping per-effect
    /// active state (instead of fixed buckets) lets one commander run different
    /// thresholds, e.g. 勇敢 (35%) alongside 回光反照 (15%).
    /// </summary>
    public sealed class RuntimeTraitEffect
    {
        public string DefinitionId;
        public TraitEffect Effect;
        public bool Active;
    }

    /// <summary>Runtime trait state of one player commander group for a battle.</summary>
    public sealed class CommanderTraitRuntime
    {
        public string GroupId;
        public string CommanderId;
        public readonly List<RuntimeTraitEffect> Effects = new List<RuntimeTraitEffect>();
    }

    /// <summary>
    /// Cached condition summary of a commander group, refreshed on MediumTick so
    /// trigger evaluation never re-walks the group for every shot (计划文档 14.3).
    /// </summary>
    public sealed class GroupTraitConditionState
    {
        public int AliveCount;
        public int CurrentHealthTotal;
        public int MaxHealthTotal;

        public float HealthRatio
        {
            get { return MaxHealthTotal > 0 ? (float)CurrentHealthTotal / MaxHealthTotal : 1f; }
        }
    }
}
