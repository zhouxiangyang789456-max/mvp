using UnityEngine;

namespace Mvp.Battle.Map.Generation
{
    /// <summary>
    /// Stores the map/profile last applied by HandMapBuilder. Keeping this under Resources
    /// makes direct BattleScene play use the same authored map as the normal level flow.
    /// </summary>
    public sealed class HandMapRuntimeConfig : ScriptableObject
    {
        public const string ResourcePath = "Battle/Map/HandMapRuntimeConfig";

        public HandAuthoredMapData ActiveMap;
        public LevelMapGenerationProfile ActiveProfile;

        public static HandMapRuntimeConfig Load()
        {
            return Resources.Load<HandMapRuntimeConfig>(ResourcePath);
        }
    }
}
