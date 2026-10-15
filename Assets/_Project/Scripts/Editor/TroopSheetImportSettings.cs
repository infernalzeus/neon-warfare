using UnityEditor;
using UnityEngine;

namespace NW.Editor
{
    /// <summary>
    /// Forces the sprite-pack PNGs under Resources/TroopSheets to import as plain 2D
    /// textures (not Sprite/2D-and-UI), matching NeonArt's own procedural unit textures
    /// (Bilinear, Clamp). Mipmaps are OFF here specifically -- these are multi-frame grids
    /// read via RawImage.uvRect, and a generated mip chain blends neighbouring frames into
    /// the edges of whichever cell is selected once the draw size drops below the full
    /// texture resolution, a different failure mode than NeonArt's own single-image
    /// textures (which mipmap safely because there's only one frame to bleed into).
    /// Compressed (not Uncompressed): each sheet is 1536x1024 -- uncompressed RGBA32 that's
    /// ~6MB per texture, ~575MB across all 96 if a session ends up touching most of them.
    /// Block compression (ASTC/ETC2/DXT, whatever the platform picks) is safe here because
    /// mipmaps are off -- compression artifacts stay within a block, not across mip levels,
    /// so they can't bleed into a neighbouring grid cell the way a mip chain would.
    /// Run via -executeMethod NW.Editor.TroopSheetImportSettings.Apply
    /// </summary>
    public static class TroopSheetImportSettings
    {
        [MenuItem("NW/Fix Troop Sheet Import Settings")]
        public static void Apply() => ApplyToFolder("Assets/_Project/Resources/TroopSheets", 0);

        /// <summary>Currency art masters ship at 1254x1254 (per the pack's own manifest) but
        /// only ever render as a small board/HUD icon -- 256 keeps the same visual budget as
        /// every other UI icon instead of shipping a multi-megapixel source for a ~64px use.</summary>
        [MenuItem("NW/Fix Currency Art Import Settings")]
        public static void ApplyCurrency() => ApplyToFolder("Assets/_Project/Resources/Currency", 256);

        static void ApplyToFolder(string dir, int maxSize)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { dir });
            int n = 0;
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;
                importer.textureType     = TextureImporterType.Default;
                importer.mipmapEnabled   = false;
                importer.filterMode      = FilterMode.Bilinear;
                importer.wrapMode        = TextureWrapMode.Clamp;
                importer.alphaIsTransparency = true;
                importer.textureCompression = TextureImporterCompression.Compressed;
                if (maxSize > 0) importer.maxTextureSize = maxSize;
                importer.SaveAndReimport();
                n++;
            }
            Debug.Log($"[TroopSheetImportSettings] Reimported {n} texture(s) under {dir}");
        }
    }
}
