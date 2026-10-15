using System.IO;
using NW.App;
using UnityEditor;
using UnityEngine;

namespace NW.Editor
{
    /// <summary>One-off diagnostic: crops a few frames out of a sprite-pack sheet using the
    /// exact same uvRect math as SpriteSheetView, so the crops can be eyeballed against the
    /// full sheet before trusting the slicing across all 32 units. Run via -executeMethod
    /// NW.Editor.SpriteSheetFrameCheck.Run -exportDir <path></summary>
    public static class SpriteSheetFrameCheck
    {
        [MenuItem("NW/Check Sprite Sheet Frame Slicing")]
        public static void Run()
        {
            string dir = GetArg("-exportDir");
            if (string.IsNullOrEmpty(dir)) dir = Path.Combine(Application.dataPath, "..", "_troop_export");
            Directory.CreateDirectory(dir);

            foreach (var (id, motion, frame) in new[] {
                ("cybtrooper", "walk", 0), ("cybtrooper", "walk", 12), ("cybtrooper", "walk", 23),
                ("paladin", "attack", 0), ("paladin", "attack", 12), ("paladin", "attack", 23),
                ("stinger", "walk", 0), ("stinger", "walk", 12),
                ("hive", "walk", 0), ("hive", "walk", 12),
                ("golem", "attack", 12), ("phoenix", "walk", 12), ("oracle", "walk", 12),
            })
            {
                // Read the raw PNG file directly (bypasses the AssetDatabase import pipeline,
                // and its non-readable shipped setting) rather than Resources.Load, so this
                // check needs no runtime-memory tradeoff (isReadable) just to verify slicing.
                string pngPath = Path.Combine(Application.dataPath, "_Project", "Resources",
                    "TroopSheets", $"{id}__standard__{motion}.png");
                if (!File.Exists(pngPath)) { Debug.LogError($"[SpriteSheetFrameCheck] missing file {pngPath}"); continue; }
                var readable = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                readable.LoadImage(File.ReadAllBytes(pngPath));

                int col = frame % NeonArt.SpriteSheetCols;
                int row = frame / NeonArt.SpriteSheetCols;
                int vRow = NeonArt.SpriteSheetRows - 1 - row;
                int cw = readable.width  / NeonArt.SpriteSheetCols;
                int ch = readable.height / NeonArt.SpriteSheetRows;

                var crop = new Texture2D(cw, ch, TextureFormat.RGBA32, false);
                crop.SetPixels(readable.GetPixels(col * cw, vRow * ch, cw, ch));
                crop.Apply();
                File.WriteAllBytes(Path.Combine(dir, $"{id}_{motion}_f{frame}.png"), crop.EncodeToPNG());
            }
            Debug.Log($"[SpriteSheetFrameCheck] Wrote crops to {dir}");
        }

        static string GetArg(string name)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
