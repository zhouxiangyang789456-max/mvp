using System;
using UnityEngine;

namespace Mvp.Battle.Map.Decorations
{
    [Serializable]
    public sealed class TerrainDecorationRule
    {
        public TerrainType Terrain;
        public GameObject[] Prefabs = Array.Empty<GameObject>();
        [Range(0f, 1f)] public float SpawnChance = 0.5f;
        [Min(0)] public int MinCount = 1;
        [Min(0)] public int MaxCount = 1;
        [Range(0f, 0.4f)] public float PositionJitter = 0.15f;
        [Min(0.05f)] public float TargetHeight = 0.8f;
        [Min(0.05f)] public float MaxFootprint = 0.75f;
        public float VerticalOffset;
        public bool RandomYaw = true;
        public bool CastShadows = true;
        public bool UseDecorationBase = true;
        public Color Tint = Color.white;

        public bool IsUsable
        {
            get { return Prefabs != null && Prefabs.Length > 0 && MaxCount > 0; }
        }
    }
}
