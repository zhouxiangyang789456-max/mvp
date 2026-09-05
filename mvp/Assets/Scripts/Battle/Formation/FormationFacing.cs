using UnityEngine;

namespace Mvp.Battle.Formation
{
    /// <summary>
    /// 8-direction formation facing helpers (《占领朝向与临时排兵布阵改造方案》阶段A).
    /// Grid convention: cell.x maps to world X, cell.y maps to world Z.
    /// Canonical frame: front = +Z (grid (0,1)), right = +X (grid (1,0)).
    /// </summary>
    public static class FormationFacing
    {
        public static readonly Vector2Int Default = new Vector2Int(0, 1);

        /// <summary>
        /// Quantizes a grid direction to one of the 8 compass directions.
        /// Zero vector returns zero; a 2:1 threshold decides diagonal vs axial.
        /// </summary>
        public static Vector2Int Quantize(Vector2Int dir)
        {
            int ax = Mathf.Abs(dir.x), az = Mathf.Abs(dir.y);
            if (ax == 0 && az == 0) return Vector2Int.zero;
            int sx = dir.x >= 0 ? 1 : -1, sz = dir.y >= 0 ? 1 : -1;
            if (ax * 2 >= az && az * 2 >= ax) return new Vector2Int(sx, sz); // diagonal
            return ax > az ? new Vector2Int(sx, 0) : new Vector2Int(0, sz);
        }

        /// <summary>
        /// Rotates a canonical slot offset to world space for a given facing:
        /// world = localX*Right + localZ*Facing, where Right = (Facing.y, -Facing.x)
        /// is Facing rotated 90° clockwise.
        /// </summary>
        public static Vector2Int RotateOffset(Vector2Int local, Vector2Int facing)
        {
            var right = new Vector2Int(facing.y, -facing.x);
            return new Vector2Int(local.x * right.x + local.y * facing.x,
                                  local.x * right.y + local.y * facing.y);
        }

        /// <summary>Grid facing → world-space direction (for the unit's final heading).</summary>
        public static Vector3 WorldDirection(Vector2Int facing) => new Vector3(facing.x, 0f, facing.y);
    }
}
