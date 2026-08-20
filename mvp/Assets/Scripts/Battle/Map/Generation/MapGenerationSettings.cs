using System;

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
        public int Factories = 2;
        public int Cities = 5;

        public bool Ocean = true;
        public bool Beach = true;
        public bool River = true;
        public bool Forest = true;
        public bool Mountain = true;

        public MapGenerationSettings Clone()
        {
            return (MapGenerationSettings)MemberwiseClone();
        }
    }
}
