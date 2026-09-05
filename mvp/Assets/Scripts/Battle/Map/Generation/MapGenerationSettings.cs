using System;
using UnityEngine.Serialization;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Input parameters for <see cref="ProceduralMapGenerator"/>. Pure C# so the
    /// generator can run in tests / editor tools without a live scene. The same
    /// settings (including seed) always produce the same map.
    /// </summary>
    [Serializable]
    public sealed class MapGenerationSettings
    {
        public int Width = 16;
        public int Height = 14;
        public uint Seed = 20260818u;

        public float SeaLevel = 0.36f;
        public float MountainLevel = 0.68f;
        public float ForestMoisture = 0.60f;

        public int Rivers = 1;
        public int BridgeSpan = 3;
        public int SmoothRounds = 2;

        public bool Mirror = false;
        public bool Buildings = false;
        public bool Roads = true;

        /// <summary>
        /// 楼房数量 (free maps) / 每侧楼房数量 (mirror maps). Formerly "Cities".
        /// </summary>
        [FormerlySerializedAs("Cities")]
        public int HouseCount = 5;

        /// <summary>
        /// 兵工厂数量 (free maps) / 每侧兵工厂数量 (mirror maps). Formerly "Factories".
        /// </summary>
        [FormerlySerializedAs("Factories")]
        public int ArmoryCount = 2;

        public bool Ocean = true;
        public bool Beach = true;
        public bool River = true;
        public bool Forest = true;
        public bool Mountain = true;

        // ---- 限时传送门撤离关卡 (timed extraction objective) -----------------
        // When true the generated map carries a seed-stable extraction portal and the
        // battle runs as a TimedExtraction objective; when false it stays Elimination.

        /// <summary>Enable the timed-extraction portal for this map (关卡目标类型).</summary>
        public bool EnableExtractionPortal = false;

        /// <summary>Countdown length once combat starts, in seconds.</summary>
        public int ExtractionTimeLimitSeconds = 180;

        /// <summary>Extraction footprint width in cells (clamped to 1..4).</summary>
        public int ExtractionZoneWidth = 2;

        /// <summary>Extraction footprint height in cells (clamped to 1..4).</summary>
        public int ExtractionZoneHeight = 2;

        /// <summary>Minimum shortest-path distance from a player deployment zone to the portal.</summary>
        public int MinPortalPathDistanceFromPlayer = 6;

        /// <summary>Maximum shortest-path distance from a player deployment zone to the portal.</summary>
        public int MaxPortalPathDistanceFromPlayer = 22;

        /// <summary>Portal opening delay after combat starts (units cannot enter before this).</summary>
        public float PortalOpeningDelaySeconds = 1f;

        public MapGenerationSettings Clone()
        {
            return (MapGenerationSettings)MemberwiseClone();
        }
    }
}
