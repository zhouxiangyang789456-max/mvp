using Mvp.Battle.Map.Generation;
using UnityEditor;
using UnityEngine;

namespace Mvp.Editor.HandMapBuilder
{
    public static class HandMapPrefabRefillTool
    {
        [MenuItem("Tools/HandMapBuilder/Refill Runtime Prefab References")]
        public static void RefillAll()
        {
            int maps = 0, filled = 0, missing = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:HandAuthoredMapData"))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var map = AssetDatabase.LoadAssetAtPath<HandAuthoredMapData>(path);
                if (map == null) continue;
                maps++;
                Undo.RecordObject(map, "回填 HandMap Prefab 引用");
                for (int i = 0; i < map.Tiles.Count; i++)
                {
                    var tile = map.Tiles[i];
                    if (tile.Prefab != null) continue;
                    tile.Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(tile.PrefabPath);
                    if (tile.Prefab == null) { missing++; continue; }
                    map.Tiles[i] = tile;
                    filled++;
                }
                EditorUtility.SetDirty(map);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[HandMapPrefabRefill] maps=" + maps + " filled=" + filled + " missing=" + missing);
        }
    }
}
