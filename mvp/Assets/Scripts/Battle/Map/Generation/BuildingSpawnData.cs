using System;
using UnityEngine;
using Mvp.Shared;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Serializable spawn hint for one building (see 建筑与兵工厂系统设计文档 §14 / §18.2).
    /// Consumed by <see cref="Buildings.BuildingRegistry"/> when a generated map carries
    /// building data; the hand-authored TestMap has none and falls back to runtime defaults.
    /// Anchors are in the same grid convention as everything else (bottom-left cell of the
    /// footprint).
    /// </summary>
    [Serializable]
    public sealed class BuildingSpawnData
    {
        /// <summary>Definition id, e.g. "building_house" / "building_armory".</summary>
        public string DefinitionId;

        /// <summary>Bottom-left cell of the building footprint.</summary>
        public Vector2Int AnchorCell;

        /// <summary>Optional starting owner (default Neutral).</summary>
        public BuildingOwner InitialOwner = BuildingOwner.Neutral;
    }
}
