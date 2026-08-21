using UnityEditor;
using UnityEngine;

namespace Mvp.Editor.MapGeneration
{
    /// <summary>Applies one import contract to all generated isometric terrain sprites.</summary>
    public sealed class TerrainSpriteImportProcessor : AssetPostprocessor
    {
        const string TerrainRoot = "Assets/Resources/Battle/Terrain/Generated/";
        const string SessionImportKey = "Mvp.TerrainSprites.Imported";

        [InitializeOnLoadMethod]
        static void EnsureTerrainSpritesAreImported()
        {
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool(SessionImportKey, false))
                    return;

                SessionState.SetBool(SessionImportKey, true);
                var guids = AssetDatabase.FindAssets(
                    "t:Texture2D",
                    new[] { TerrainRoot.TrimEnd('/') });

                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                }
            };
        }

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(TerrainRoot, System.StringComparison.OrdinalIgnoreCase))
                return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.spritePixelsPerUnit = 1024f;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = new Vector2(0.5f, 0.35f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
        }
    }
}
