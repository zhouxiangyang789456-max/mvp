using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Functional category for a hand-placed tile (3D 等距地图建造工具开发计划 §3.2).
    /// Distinct from <see cref="TerrainType"/>: this enum groups prefabs by *function*
    /// (base / decoration / building / water / bridge / erase) so the editor palette
    /// can paginate by intent. The procedural runtime maps these to
    /// <see cref="GeneratedTerrain"/> when <see cref="ProceduralBattleMapProvider"/>
    /// consumes the data.
    ///
    /// 阶段 2 (HandMapBuilder阶段2分类与层级改造方案): 扩展为 12 类。
    /// 旧值 Road→Path、Hill→Mountain、Forest/Plant 拆分、新增 Ramp/Effect。
    /// 旧 .asset 反序列化时通过 OnAfterDeserialize 自动迁移兼容。
    /// </summary>
    public enum HandTileCategory
    {
        Base,
        Path,        // 原 Road
        Forest,
        Plant,       // 新增（拆分自 Decoration）
        Water,
        Ramp,        // 新增
        Bridge,
        Mountain,    // 合并 Hill
        Building,
        Decoration,
        Effect,      // 新增
        Erase
    }

    /// <summary>
    /// One hand-placed prefab on the map grid. Coordinates use the project's 1-unit
    /// logical cell space; X grows east, Y grows north. Z is reserved for stacked
    /// geometry (bridges / ramps over water).
    /// </summary>
    [Serializable]
    public struct HandPlacedTile
    {
        public int X;
        public int Y;
        public int Z;
        /// <summary>Project-relative asset path. Survives GUID renames better than fileID.</summary>
        public string PrefabPath;
        /// <summary>Runtime-safe asset reference. PrefabPath remains for migration/editor lookup.</summary>
        public GameObject Prefab;
        /// <summary>Y-axis rotation in degrees. 阶段4开始真正使用，支持任意角度。</summary>
        public float RotationY;
        /// <summary>
        /// 阶段4新增：单 tile 的高度微调（[-2, +2]）。让用户能在 Z 内细调放置高度，
        /// 避免石块陷进地面或塔身悬空。和全局 LayerHeightScale 叠加生效。
        /// </summary>
        public float HeightOffset;
        public HandTileCategory Category;
    }

    /// <summary>
    /// Hand-authored 3D isometric map (3D 等距地图建造工具开发计划 §4.1).
    /// A ScriptableObject so it can be referenced from
    /// <see cref="LevelMapGenerationProfile.HandMapOverride"/> and consumed by the
    /// runtime via <see cref="ProceduralBattleMapProvider"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "HandMap_New", menuName = "Battle/Map Generation/Hand Authored Map")]
    public sealed class HandAuthoredMapData : ScriptableObject
    {
        [Min(1)] public int Width = 16;
        [Min(1)] public int Height = 14;
        [Range(0.5f, 2f)] public float LayerHeightScale = 1f;
        [Min(0.001f)] public float DefaultPrefabScale = 0.5f;

        /// <summary>All hand-placed tiles. Stored as a flat list to keep the asset
        /// diff-friendly and to allow multiple decorations on the same cell.</summary>
        public List<HandPlacedTile> Tiles = new List<HandPlacedTile>();

        /// <summary>Returns the first tile placed at (x,y,z). Null if none.</summary>
        public HandPlacedTile? FindTile(int x, int y, int z = 0)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                var t = Tiles[i];
                if (t.X == x && t.Y == y && t.Z == z) return t;
            }
            return null;
        }

        public bool HasGroundVisual(int x, int y)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                var t = Tiles[i];
                if (t.X != x || t.Y != y || t.Z != 0 || t.Prefab == null) continue;
                // Compatibility with maps saved before Tile1_Base was classified as Base.
                if (!string.IsNullOrEmpty(t.PrefabPath) &&
                    t.PrefabPath.IndexOf("tile1_base", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                switch (t.Category)
                {
                    case HandTileCategory.Base:
                    case HandTileCategory.Path:
                    case HandTileCategory.Forest:
                    case HandTileCategory.Water:
                    case HandTileCategory.Ramp:
                    case HandTileCategory.Bridge:
                    case HandTileCategory.Mountain:
                        return true;
                }
            }
            return false;
        }

        /// <summary>Removes the first tile at (x,y,z). Returns true if something was removed.</summary>
        public bool RemoveTile(int x, int y, int z = 0)
        {
            for (int i = 0; i < Tiles.Count; i++)
            {
                var t = Tiles[i];
                if (t.X == x && t.Y == y && t.Z == z)
                {
                    Tiles.RemoveAt(i);
                    return true;
                }
            }
            return false;
        }

        /// <summary>Compatibility migration for old assets (Road→Path, Hill→Mountain).</summary>
        [SerializeField, HideInInspector] bool _legacyEnumMigrated;

        void OnEnable()
        {
            LayerHeightScale = Mathf.Clamp(LayerHeightScale <= 0f ? 1f : LayerHeightScale, 0.5f, 2f);
            DefaultPrefabScale = Mathf.Max(0.001f, DefaultPrefabScale <= 0f ? 0.5f : DefaultPrefabScale);
            if (_legacyEnumMigrated) return;
            _legacyEnumMigrated = true;
            if (Tiles == null) return;
            for (int i = 0; i < Tiles.Count; i++)
            {
                var t = Tiles[i];
                string name = t.Category.ToString();
                if (name == "Road") t.Category = HandTileCategory.Path;
                else if (name == "Hill") t.Category = HandTileCategory.Mountain;
                Tiles[i] = t;
            }
            // Save back if anything changed.
            if (Tiles.Count > 0)
            {
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this);
#endif
            }
        }
    }
}
