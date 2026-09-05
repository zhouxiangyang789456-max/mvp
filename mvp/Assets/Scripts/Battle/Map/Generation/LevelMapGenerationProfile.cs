using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Level -> map rule config asset (随机地图生成接入方案 §7). The level-select / battle
    /// startup reads FindRule for the current level, then BuildRequest hands a reproducible
    /// BattleMapRequest to ProceduralBattleMapProvider. Pure rule matching lives in
    /// <see cref="LevelRuleResolver"/> so it is unit-testable.
    ///
    /// Assets live under Assets/Resources/MapGeneration or Assets/ScriptableObjects/MapGeneration.
    /// </summary>
    [CreateAssetMenu(fileName = "MapGenerationProfile", menuName = "Battle/Map Generation Profile")]
    public sealed class LevelMapGenerationProfile : ScriptableObject
    {
        /// <summary>Stable ID, independent of the asset file name.</summary>
        public string ProfileId;

        /// <summary>Increment when rules change.</summary>
        public int ProfileVersion = 1;

        /// <summary>Generator algorithm version the profile expects.</summary>
        public int GeneratorVersion = ProceduralMapGenerator.GeneratorVersion;

        /// <summary>Participates in deterministic seed derivation.</summary>
        public uint ProfileSalt;

        [Tooltip("Optional map built with HandMapBuilder. Used when BattleMapSource is HandAuthored.")]
        public HandAuthoredMapData HandMapOverride;

        [Tooltip("Rules must not overlap; gaps fall back to default settings.")]
        public List<LevelMapGenerationRule> Rules = new List<LevelMapGenerationRule>();

        public LevelMapGenerationRule FindRule(int levelIndex)
        {
            return LevelRuleResolver.FindRule(Rules, levelIndex);
        }

        /// <summary>Null when the configuration is valid, else a user-facing error.</summary>
        public string ValidateConfiguration()
        {
            return LevelRuleResolver.ValidateConfiguration(Rules);
        }

        /// <summary>
        /// Builds the battle request for a level. When no rule matches (or the profile is
        /// empty) it falls back to the default settings + LevelBased seed so a battle still starts.
        /// </summary>
        public BattleMapRequest BuildRequest(int levelIndex)
        {
            var rule = FindRule(levelIndex);
            var settings = rule != null && rule.Settings != null
                ? rule.Settings.Clone()
                : new MapGenerationSettings();
            var validation = rule != null && rule.Validation != null
                ? rule.Validation
                : new MapValidationSettings();

            return new BattleMapRequest
            {
                ProfileId = ProfileId,
                ProfileVersion = ProfileVersion,
                RuleId = rule != null ? rule.RuleId : null,
                LevelIndex = levelIndex,
                SeedMode = rule != null ? rule.SeedMode : SeedMode.LevelBased,
                FixedSeed = rule != null ? rule.FixedSeed : 20260818u,
                ProfileSalt = ProfileSalt,
                RetryCount = rule != null ? rule.RetryCount : 10,
                Settings = settings,
                MinWalkableRatio = validation.MinWalkableRatio,
                MaxWalkableRatio = validation.MaxWalkableRatio,
                MinWalkableComponentRatio = validation.MinWalkableComponentRatio,
            };
        }
    }
}
