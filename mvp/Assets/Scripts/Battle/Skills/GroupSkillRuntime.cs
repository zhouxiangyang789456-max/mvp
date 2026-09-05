using System.Collections.Generic;
using Mvp.Shared.Skills;

namespace Mvp.Battle.Skills
{
    /// <summary>
    /// Skill runtime held by a commander group (战斗技能系统开发文档 §4.2).
    /// SkillSequence works like the group command sequence: async targeting, animation
    /// events or delayed attacks must verify it so an old skill result can never
    /// overwrite a newer command.
    /// </summary>
    public sealed class GroupSkillRuntime
    {
        public PersistentSkillMode PersistentMode = PersistentSkillMode.None;
        public string TargetingSkillId;
        public long SkillSequence;
        public readonly Dictionary<string, UnitSkillRuntime> UnitStates =
            new Dictionary<string, UnitSkillRuntime>();

        /// <summary>Returns (creating on demand) the per-unit state for a skill.</summary>
        public UnitSkillRuntime GetOrCreate(string unitId, string skillId)
        {
            UnitSkillRuntime state;
            if (!UnitStates.TryGetValue(unitId, out state))
            {
                state = new UnitSkillRuntime { SkillId = skillId };
                UnitStates[unitId] = state;
                return state;
            }
            if (state.SkillId != skillId)
            {
                state.SkillId = skillId;
                state.State = SkillRuntimeState.Ready;
                state.ActiveUntil = 0f;
                state.CooldownUntil = 0f;
                state.IsEligible = false;
            }
            return state;
        }

        /// <summary>Clears all active modes and invalidates any in-flight skill result.</summary>
        public void ResetModes()
        {
            PersistentMode = PersistentSkillMode.None;
            TargetingSkillId = null;
            SkillSequence++;
        }
    }
}
