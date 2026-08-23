using UnityEditor;
using UnityEngine;

namespace Mvp.EditorTools.UI
{
    public sealed class SettlementShopSpriteImportProcessor : AssetPostprocessor
    {
        const string ShopSpriteRoot = "Assets/Resources/SettlementShop/Generated/";

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(ShopSpriteRoot, System.StringComparison.Ordinal)) return;

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.maxTextureSize = assetPath.EndsWith("shop_panel_v2.png") ? 2048 : 1024;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            importer.SetTextureSettings(settings);
        }
    }
}
