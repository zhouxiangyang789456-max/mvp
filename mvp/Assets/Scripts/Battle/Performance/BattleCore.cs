using UnityEngine;
using Mvp.Battle.Commanders;
using Mvp.Battle.Buildings;
using Mvp.Battle.Economy;
using Mvp.Battle.Vision;
using Mvp.Battle.AI;
using Mvp.Battle.Skills;
using Mvp.Battle.Outcome;

namespace Mvp.Battle
{
    /// <summary>
    /// Creates/owns the "BattleCore" GameObject holding all performance
    /// infrastructure singletons. BattleBootstrap calls Ensure() at battle start;
    /// the object is destroyed automatically when the battle scene unloads.
    /// </summary>
    public static class BattleCore
    {
        static GameObject _root;

        public static void Ensure()
        {
            // Unity overloaded null-check returns true for destroyed objects.
            if (_root != null) return;

            var go = new GameObject("BattleCore");
            go.AddComponent<BattleTickService>();
            var effects = go.AddComponent<EffectPool>();
            var ui = go.AddComponent<UiPool>();
            go.AddComponent<PathRequestQueue>();
            go.AddComponent<UnitHealthBarManager>();
            go.AddComponent<CommanderGroupRegistry>();
            go.AddComponent<FormationReservationService>();
            go.AddComponent<BattleSpatialIndex>();
            go.AddComponent<BattleVisionService>();
            go.AddComponent<CommanderGroupCommandController>();
            go.AddComponent<EnemyGroupAiController>();
            go.AddComponent<BattleOutcomeController>();
            go.AddComponent<ExtractionObjectiveController>();
            go.AddComponent<BattleGmController>();
            // 阶段B: buildings must be registered before any unit spawns place units.
            go.AddComponent<BuildingRegistry>();
            go.AddComponent<BuildingCaptureController>();
            go.AddComponent<BattleEconomyController>();
            // 战斗技能系统: range preview / targeting / skill command entry + hidden hosts.
            go.AddComponent<SkillRangePreview>();
            go.AddComponent<SkillTargetingController>();
            go.AddComponent<GroupSkillController>();
            ConcealmentService.EnsureHost();
            SprintEffectService.EnsureHost();
            TauntEffectService.EnsureHost();
            effects.RegisterDefaultEffects();
            ui.RegisterDefaultUi();
            _root = go;
        }

        public static void Teardown()
        {
            TacticalDecoyService.Shutdown();
            TauntEffectService.Shutdown();
            if (_root != null)
            {
                Object.Destroy(_root);
                _root = null;
            }
        }

        public static void DestroyImmediateNow()
        {
            TacticalDecoyService.Shutdown();
            TauntEffectService.Shutdown();
            if (_root != null)
            {
                Object.DestroyImmediate(_root);
                _root = null;
            }
        }
    }
}
