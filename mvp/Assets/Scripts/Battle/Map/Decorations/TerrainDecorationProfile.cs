using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle.Map.Decorations
{
    [CreateAssetMenu(menuName = "Mvp/Battle/Terrain Decoration Profile")]
    public sealed class TerrainDecorationProfile : ScriptableObject
    {
        public bool Enabled = true;
        [Min(1)] public int DecorationVersion = 1;
        [Range(0f, 1f)] public float GlobalDensity = 1f;
        [Min(0)] public int DeploymentClearance = 1;
        [Min(0)] public int PortalClearance = 1;
        [Min(0)] public int BuildingClearance = 1;
        public List<TerrainDecorationRule> Rules = new List<TerrainDecorationRule>();

        public TerrainDecorationRule FindRule(TerrainType terrain)
        {
            for (int i = 0; i < Rules.Count; i++)
            {
                var rule = Rules[i];
                if (rule != null && rule.Terrain == terrain) return rule;
            }
            return null;
        }
    }
}
