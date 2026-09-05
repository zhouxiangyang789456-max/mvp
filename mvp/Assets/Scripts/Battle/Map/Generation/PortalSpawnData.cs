using System;
using UnityEngine;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Seed-stable spawn hint for the timed-extraction portal (限时传送门撤离关卡).
    /// Produced by <see cref="PortalPlacementPlanner"/> during map generation and
    /// consumed by <see cref="Battle.Outcome.ExtractionObjectiveController"/> at battle
    /// start. A generated map with a null <c>Portal</c> is an Elimination objective.
    /// The hand-authored TestMap carries no generated data and falls back to the
    /// controller's legacy runtime scan.
    /// </summary>
    [Serializable]
    public sealed class PortalSpawnData
    {
        /// <summary>Bottom-left cell of the extraction footprint (grid convention).</summary>
        public Vector2Int AnchorCell;

        /// <summary>Footprint width in cells. Defaults to the 2x2 MVP zone.</summary>
        public int Width = 2;

        /// <summary>Footprint height in cells. Defaults to the 2x2 MVP zone.</summary>
        public int Height = 2;

        /// <summary>Countdown length once combat starts, in seconds.</summary>
        public int TimeLimitSeconds = 180;

        /// <summary>Portal opening delay after combat starts (units cannot enter before this).</summary>
        public float OpeningDelaySeconds = 1f;
    }
}
