using UnityEngine;

namespace Mvp.Battle
{
    /// <summary>
    /// Logical grid access used by pathfinding and movement. Implemented by
    /// BattleGridController (map milestone) so pathfinding never depends on Unity scene types.
    /// </summary>
    public interface IGridDataProvider
    {
        int Width { get; }
        int Height { get; }
        bool InBounds(Vector2Int cell);
        bool IsWalkable(Vector2Int cell);
        bool IsOccupied(Vector2Int cell);
    }
}
