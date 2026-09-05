#if UNITY_EDITOR
using System.Collections.Generic;
using Mvp.Battle.Map;
using Mvp.Battle.Map.Decorations;
using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools
{
    public static class TerrainDecorationSetup
    {
        const string Folder = "Assets/Resources/Battle/TerrainDecorations";
        const string ProfilePath = Folder + "/DefaultTerrainDecorationProfile.asset";
        const string SessionKey = "Mvp.TerrainDecorationSetup.v1";
        const string Props = "Assets/Isometric Pack 3d/Props/";

        [InitializeOnLoadMethod]
        static void Schedule()
        {
            if (SessionState.GetBool(SessionKey, false)) return;
            SessionState.SetBool(SessionKey, true);
            EditorApplication.delayCall += Build;
        }

        [MenuItem("Mvp/Terrain/Build 2.5D Decoration Profile")]
        public static void Build()
        {
            EnsureFolder(Folder);
            var profile = AssetDatabase.LoadAssetAtPath<TerrainDecorationProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<TerrainDecorationProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.Enabled = true;
            profile.DecorationVersion = 1;
            profile.GlobalDensity = 1f;
            profile.DeploymentClearance = 1;
            profile.PortalClearance = 1;
            profile.BuildingClearance = 1;
            profile.Rules = new List<TerrainDecorationRule>
            {
                Rule(TerrainType.Plain, 0.14f, 1, 1, 0.18f, 0.18f, 0.28f, false, false,
                    Color.white, "Plants_05", "Plants_06", "Plants_09"),
                Rule(TerrainType.Forest, 0.82f, 1, 2, 0.16f, 0.86f, 0.62f, true, true,
                    Color.white, "Tree1_1", "Tree1_2", "Tree2_1", "Tree4_01"),
                Rule(TerrainType.Hill, 0.68f, 1, 2, 0.18f, 0.48f, 0.68f, true, true,
                    Color.white, "Rock_01", "Rock_02", "Rock_03", "Plants_10"),
                Rule(TerrainType.Mountain, 0.96f, 1, 2, 0.12f, 0.78f, 0.88f, true, true,
                    Color.white, "Rock_04", "Rock_05", "Rock_06", "Rock_07", "Rock_08"),
                Rule(TerrainType.SnowMountain, 0.96f, 1, 2, 0.12f, 0.78f, 0.88f, true, true,
                    new Color(0.92f, 0.96f, 1f), "Rock_08", "Rock_09", "Rock_10", "Rock_11"),
                Rule(TerrainType.Desert, 0.38f, 1, 1, 0.18f, 0.48f, 0.62f, true, true,
                    new Color(0.95f, 0.76f, 0.42f), "Rock_01", "Rock_02", "Tree3_01_aut"),
                Rule(TerrainType.Bridge, 1f, 1, 1, 0f, 0.24f, 0.96f, false, false,
                    Color.white, "Bridge1", "Bridge2")
            };

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[TerrainDecorationSetup] Built profile with " + profile.Rules.Count +
                " rules at " + ProfilePath);
        }

        static TerrainDecorationRule Rule(TerrainType terrain, float chance,
            int minCount, int maxCount, float jitter, float height, float footprint,
            bool randomYaw, bool castShadows, Color tint, params string[] prefabNames)
        {
            var prefabs = new List<GameObject>();
            for (int i = 0; i < prefabNames.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Props + prefabNames[i] + ".prefab");
                if (prefab != null) prefabs.Add(prefab);
                else Debug.LogWarning("[TerrainDecorationSetup] Missing prefab: " + prefabNames[i]);
            }
            return new TerrainDecorationRule
            {
                Terrain = terrain,
                Prefabs = prefabs.ToArray(),
                SpawnChance = chance,
                MinCount = minCount,
                MaxCount = maxCount,
                PositionJitter = jitter,
                TargetHeight = height,
                MaxFootprint = footprint,
                RandomYaw = randomYaw,
                CastShadows = castShadows,
                UseDecorationBase = terrain != TerrainType.Plain && terrain != TerrainType.Bridge,
                Tint = tint
            };
        }

        static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }
    }
}
#endif
