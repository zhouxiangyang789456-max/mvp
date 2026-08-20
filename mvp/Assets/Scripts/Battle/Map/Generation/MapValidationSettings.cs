using System;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Validation thresholds for one generated map. Pure C# so rules and tools can
    /// carry the same checks the runtime uses without depending on UnityEngine.
    /// See 随机地图生成接入方案 §11.
    /// </summary>
    [Serializable]
    public sealed class MapValidationSettings
    {
        /// <summary>Walkable-cell ratio must land within [Min, Max].</summary>
        public float MinWalkableRatio = 0.50f;
        public float MaxWalkableRatio = 0.90f;

        /// <summary>Largest 8-dir connected walkable component must cover this share of walkable cells.</summary>
        public float MinWalkableComponentRatio = 0.90f;

        /// <summary>Minimum contiguous walkable slots a deployment area must offer (3x3 = 9).</summary>
        public int MinDeploymentArea = 9;

        public MapValidationSettings Clone()
        {
            return (MapValidationSettings)MemberwiseClone();
        }
    }
}
