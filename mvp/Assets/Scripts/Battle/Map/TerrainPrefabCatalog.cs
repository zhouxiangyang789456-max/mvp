using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mvp.Battle.Map
{
    public enum TerrainScaleMode { KeepPrefabScale, FixedScale, FitFootprint }

    [Serializable]
    public sealed class TerrainPrefabVariant
    {
        public GameObject Prefab;
        [Min(1)] public int Weight = 1;
        public float Yaw;
    }

    [Serializable]
    public sealed class TerrainConnectionVariant
    {
        [Range(0, 15)] public int Mask;
        public TerrainPrefabVariant Variant = new TerrainPrefabVariant();
    }

    [Serializable]
    public sealed class TerrainPrefabEntry
    {
        public TerrainType Terrain;
        public bool Enabled = true;
        public TerrainScaleMode ScaleMode = TerrainScaleMode.FixedScale;
        [Min(0.001f)] public float FixedScale = 0.5f;
        [Min(0.001f)] public float MaxFootprint = 1f;
        public float GroundOffset;
        // Prefab pivot is preserved by default, particularly for composite tiles.
        public bool AlignBoundsBottom;
        public bool IncludesDecorations;
        public List<TerrainPrefabVariant> Variants = new List<TerrainPrefabVariant>();
        public List<TerrainConnectionVariant> Connections = new List<TerrainConnectionVariant>();
    }

    [CreateAssetMenu(menuName = "Battle/Terrain Prefab Catalog")]
    public sealed class TerrainPrefabCatalog : ScriptableObject
    {
        public const string ResourcePath = "Battle/Terrain/Generated/TerrainPrefabCatalog";
        public int CatalogVersion = 1;
        public List<TerrainPrefabEntry> Entries = new List<TerrainPrefabEntry>();

        public bool TryPick(TerrainType terrain, uint seed, Vector2Int cell, int mask,
            out TerrainPrefabEntry entry, out TerrainPrefabVariant variant)
        {
            entry = null;
            variant = null;
            if (Entries == null) return false;
            foreach (var candidate in Entries)
                if (candidate != null && candidate.Terrain == terrain)
                {
                    if (entry != null) return false; // Ambiguous catalog: use fallback.
                    entry = candidate;
                }
            if (entry == null || !entry.Enabled) return false;
            if (terrain == TerrainType.Road || terrain == TerrainType.Bridge)
            {
                if (mask < 0 || mask > 15) return false;
                // A complete connection table wins. Until dedicated corner/T/cross
                // meshes are assigned, a generic 3D tile still replaces the legacy sprite.
                if (entry.Connections == null || entry.Connections.Count == 0)
                    return TryPickWeighted(entry, seed, cell, terrain, CatalogVersion, out variant);
                int covered = 0;
                foreach (var connection in entry.Connections)
                {
                    if (connection == null || connection.Mask < 0 || connection.Mask > 15 ||
                        connection.Variant == null || connection.Variant.Prefab == null) return false;
                    int bit = 1 << connection.Mask;
                    if ((covered & bit) != 0) return false;
                    covered |= bit;
                    if (connection.Mask == mask) variant = connection.Variant;
                }
                return covered == 65535 && variant != null;
            }
            return TryPickWeighted(entry, seed, cell, terrain, CatalogVersion, out variant);
        }

        static bool TryPickWeighted(TerrainPrefabEntry entry, uint seed, Vector2Int cell,
            TerrainType terrain, int catalogVersion, out TerrainPrefabVariant variant)
        {
            variant = null;
            if (entry.Variants == null) return false;
            ulong total = 0;
            foreach (var item in entry.Variants)
                if (item != null && item.Prefab != null && item.Weight > 0) total += (uint)item.Weight;
            if (total == 0) return false;
            uint hash = seed ^ 2166136261u;
            unchecked
            {
                hash = (hash ^ (uint)cell.x) * 16777619u;
                hash = (hash ^ (uint)cell.y) * 16777619u;
                hash = (hash ^ (uint)terrain) * 16777619u;
                hash = (hash ^ (uint)catalogVersion) * 16777619u;
                hash ^= hash >> 16;
                hash *= 2246822519u;
                hash ^= hash >> 13;
            }
            ulong pick = hash % total;
            foreach (var item in entry.Variants)
            {
                if (item == null || item.Prefab == null || item.Weight <= 0) continue;
                if (pick < (uint)item.Weight) { variant = item; return true; }
                pick -= (uint)item.Weight;
            }
            return false;
        }
    }
}
