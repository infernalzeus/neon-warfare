using System.Collections.Generic;
using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Runtime cache for PNG assets in Assets/_Project/Resources/Art/.
    ///
    /// Unit portraits and gem textures are processed through RemoveDarkBg() so the
    /// AI-generated black backgrounds become transparent — leaving only the neon-lit
    /// character/crystal visible on top of any dark game surface.
    ///
    /// Background images are returned raw (full opacity, callers control alpha).
    /// </summary>
    public static class ArtLoader
    {
        static readonly Dictionary<string, Texture2D> _raw       = new();
        static readonly Dictionary<string, Texture2D> _processed = new();

        static readonly string[] GemNames = { "energy", "plasma", "nano", "quantum", "data" };

        // ── public API ────────────────────────────────────────────────────────

        /// <summary>Returns unit portrait with dark background removed. artId: "drone", "shield-bot" …</summary>
        public static Texture2D UnitPortraitTex(string artId)
        {
            string slug = artId.Replace('-', '_');
            return Processed($"Art/troop_{slug}", lo: 0.08f, hi: 0.22f);
        }

        /// <summary>Returns gem texture with dark background removed. kind 0-4 = energy/plasma/nano/quantum/data.</summary>
        public static Texture2D GemTex(int kind)
        {
            if ((uint)kind >= (uint)GemNames.Length) return null;
            return Processed($"Art/gem_{GemNames[kind]}", lo: 0.04f, hi: 0.16f);
        }

        /// <summary>Returns raw background texture (no alpha processing). name: "battlefield", "level_select".</summary>
        public static Texture2D BgTex(string name) => Raw($"Art/bg_{name}");

        // ── internal ──────────────────────────────────────────────────────────

        static Texture2D Raw(string path)
        {
            if (_raw.TryGetValue(path, out var t)) return t;
            t = Resources.Load<Texture2D>(path);
            _raw[path] = t;
            return t;
        }

        static Texture2D Processed(string path, float lo, float hi)
        {
            string key = $"{path}|{lo:F2}";
            if (_processed.TryGetValue(key, out var cached)) return cached;

            var src = Resources.Load<Texture2D>(path);
            if (src == null) { _processed[key] = null; return null; }

            var result = RemoveDarkBg(src, lo, hi);
            _processed[key] = result;
            return result;
        }

        /// <summary>
        /// Creates a CPU-readable copy of any texture (even import-locked ones) via
        /// RenderTexture, then converts pixel brightness to alpha so near-black
        /// pixels become transparent.
        /// lo/hi control the fade band: pixels with luma below lo → fully transparent,
        /// above hi → fully opaque.
        /// </summary>
        static Texture2D RemoveDarkBg(Texture2D src, float lo, float hi)
        {
            int w = src.width, h = src.height;

            // Blit to RenderTexture so ReadPixels works regardless of import settings
            var rt   = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(src, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;

            var dst = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp,
            };
            dst.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            dst.Apply();

            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);

            // Brightness → alpha: dark pixels transparent, bright pixels opaque
            var px = dst.GetPixels();
            for (int i = 0; i < px.Length; i++)
            {
                var c  = px[i];
                float luma = c.r * 0.30f + c.g * 0.59f + c.b * 0.11f;
                c.a = Mathf.SmoothStep(lo, hi, luma);
                px[i] = c;
            }
            dst.SetPixels(px);
            dst.Apply();
            return dst;
        }
    }
}
