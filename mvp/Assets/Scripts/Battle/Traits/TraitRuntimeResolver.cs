using System.Collections.Generic;
using Mvp.Progression;
using Mvp.Shared;

namespace Mvp.Battle.Traits
{
    /// <summary>
    /// Builds CommanderTraitRuntime from the player's current Progression loadouts.
    /// Invalid / duplicate / missing-definition / empty-slot entries are warned and
    /// skipped without interrupting the build (计划文档 14.4).
    /// </summary>
    public static class TraitRuntimeResolver
    {
        public static bool TryBuildRuntime(IReadOnlyList<ExpeditionCommanderEntry> roster,
            Dictionary<string, CommanderTraitRuntime> output, List<string> warnings)
        {
            if (output == null) return false;
            output.Clear();
            if (roster == null || roster.Count == 0) return false;

            var progression = PlayerProgressionStore.Current;
            for (int i = 0; i < roster.Count; i++)
            {
                var entry = roster[i];
                if (entry == null) continue;
                string groupId = "player_group_" + entry.RosterIndex;
                string commanderId = entry.CommanderId;
                var runtime = new CommanderTraitRuntime
                {
                    GroupId = groupId,
                    CommanderId = commanderId
                };
                output[groupId] = runtime;

                var loadout = FindLoadout(progression, commanderId);
                if (loadout == null)
                {
                    if (warnings != null)
                        warnings.Add("[TraitRuntime] No progression loadout for commander " + commanderId);
                    continue;
                }

                var seenDefinitions = new HashSet<string>();
                for (int s = 0; s < loadout.TraitCardInstanceIds.Length; s++)
                {
                    string instanceId = loadout.TraitCardInstanceIds[s];
                    if (string.IsNullOrEmpty(instanceId))
                    {
                        continue;
                    }
                    var card = FindCard(progression, instanceId);
                    if (card == null)
                    {
                        if (warnings != null)
                            warnings.Add("[TraitRuntime] Missing card instance " + instanceId +
                                " for commander " + commanderId);
                        continue;
                    }
                    var def = TraitCatalog.Get(card.DefinitionId);
                    if (def == null)
                    {
                        if (warnings != null)
                            warnings.Add("[TraitRuntime] Missing trait definition " +
                                card.DefinitionId + " for commander " + commanderId);
                        continue;
                    }
                    if (!seenDefinitions.Add(def.Id))
                    {
                        if (warnings != null)
                            warnings.Add("[TraitRuntime] Duplicate equipped card " + def.Id +
                                " for commander " + commanderId + "; skipping");
                        continue;
                    }
                    if (def.Effects == null) continue;
                    for (int e = 0; e < def.Effects.Count; e++)
                    {
                        var effect = def.Effects[e];
                        if (effect == null) continue;
                        if (!TraitEffectCatalogExtensions.IsEffectSupported(effect)) continue;
                        runtime.Effects.Add(new RuntimeTraitEffect
                        {
                            DefinitionId = def.Id,
                            Effect = effect
                        });
                    }
                }
            }
            return true;
        }

        static CommanderLoadoutSnapshot FindLoadout(
            PlayerProgressionSnapshot progression, string commanderId)
        {
            if (progression == null || string.IsNullOrEmpty(commanderId)) return null;
            for (int i = 0; i < progression.CommanderLoadouts.Count; i++)
                if (progression.CommanderLoadouts[i].CommanderId == commanderId)
                    return progression.CommanderLoadouts[i];
            return null;
        }

        static TraitCardInstance FindCard(
            PlayerProgressionSnapshot progression, string instanceId)
        {
            if (progression == null || string.IsNullOrEmpty(instanceId)) return null;
            for (int i = 0; i < progression.TraitCards.Count; i++)
                if (progression.TraitCards[i].InstanceId == instanceId)
                    return progression.TraitCards[i];
            return null;
        }
    }
}
