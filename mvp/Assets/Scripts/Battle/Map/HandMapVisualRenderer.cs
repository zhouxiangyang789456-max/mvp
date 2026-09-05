using Mvp.Battle.Map.Generation;
using UnityEngine;

namespace Mvp.Battle.Map
{
    public static class HandMapVisualRenderer
    {
        public static int Render(HandAuthoredMapData map, Transform parent)
        {
            if (map == null || map.Tiles == null || parent == null) return 0;
            var root = new GameObject("HandMapVisuals").transform;
            root.SetParent(parent, false);
            int rendered = 0;
            for (int i = 0; i < map.Tiles.Count; i++)
            {
                var tile = map.Tiles[i];
                if (tile.Prefab == null || tile.Category == HandTileCategory.Erase) continue;
                if (tile.X < 0 || tile.Y < 0 || tile.X >= map.Width || tile.Y >= map.Height) continue;
                var instance = Object.Instantiate(tile.Prefab, root, false);
                instance.name = "HandTile_" + tile.X + "_" + tile.Y + "_Z" + tile.Z;
                instance.transform.localPosition = new Vector3(tile.X,
                    tile.Z * map.LayerHeightScale + tile.HeightOffset, tile.Y);
                instance.transform.localRotation = Quaternion.Euler(0f, tile.RotationY, 0f);
                TerrainGeometryHelper.PrepareHandAuthored(instance, map.DefaultPrefabScale);
                rendered++;
            }
            Debug.Log("[HandMapVisualRenderer] rendered=" + rendered + " source=" + map.name);
            return rendered;
        }
    }
}
