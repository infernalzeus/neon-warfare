using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Inserts a three-layer atmospheric background (gradient + radial vignette + accent tint strip)
    /// behind any screen root. Call Attach() right after creating the screen's root RectTransform.
    /// Uses SetSiblingIndex so layers always sit behind existing content.
    /// </summary>
    public static class ScreenBackground
    {
        /// <param name="bgTex">Optional AI-generated background image rendered behind the procedural layers.</param>
        /// <returns>The photo RawImage (so callers can drive its alpha, e.g. a BG-DIM slider), or null.</returns>
        public static RawImage Attach(RectTransform parent, Texture2D bgTex = null)
        {
            var theme = NeonTheme.Active;
            int baseIdx = 0;
            RawImage photoImgOut = null;

            // --- Optional layer: AI-generated photo background (behind procedural layers) ---
            if (bgTex != null)
            {
                var photoGo = new GameObject("ScrnBg_Photo");
                photoGo.transform.SetParent(parent, false);
                photoGo.transform.SetSiblingIndex(0);
                var photoRt = photoGo.AddComponent<RectTransform>();
                photoRt.anchorMin = Vector2.zero;
                photoRt.anchorMax = Vector2.one;
                photoRt.offsetMin = photoRt.offsetMax = Vector2.zero;
                var photoImg = photoGo.AddComponent<RawImage>();
                photoImg.texture = bgTex;
                photoImg.color   = new Color(1f, 1f, 1f, 0.9f); // shows through the (now translucent) gradient
                photoImg.raycastTarget = false;
                photoImgOut = photoImg;
                baseIdx = 1;
            }

            // --- Layer 0: vertical gradient (BgDeep bottom → slightly lighter top) ---
            var bgGo = new GameObject("ScrnBg_Gradient");
            bgGo.transform.SetParent(parent, false);
            bgGo.transform.SetSiblingIndex(baseIdx);
            var bgRt = bgGo.AddComponent<RectTransform>();   // must AddComponent, not GetComponent
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = bgRt.offsetMax = Vector2.zero;
            var bgImg = bgGo.AddComponent<RawImage>();
            bgImg.texture       = MakeGradientTex(theme.BgDeep, LightenSlight(theme.BgDeep, 0.07f));
            // Opaque when there's no photo (it IS the background); translucent when a photo sits
            // behind it, so the theme image actually shows through instead of being covered.
            bgImg.color         = new Color(1f, 1f, 1f, bgTex != null ? 0.4f : 1f);
            bgImg.raycastTarget = false;

            // --- Layer 1: radial vignette (dark corners, transparent center) ---
            var vigGo = new GameObject("ScrnBg_Vignette");
            vigGo.transform.SetParent(parent, false);
            vigGo.transform.SetSiblingIndex(baseIdx + 1);
            var vigRt = vigGo.AddComponent<RectTransform>();
            vigRt.anchorMin = Vector2.zero;
            vigRt.anchorMax = Vector2.one;
            vigRt.offsetMin = vigRt.offsetMax = Vector2.zero;
            var vigImg = vigGo.AddComponent<RawImage>();
            vigImg.texture       = MakeVignetteTex(64);
            vigImg.color         = new Color(theme.BgDeep.r, theme.BgDeep.g, theme.BgDeep.b, 0.55f);
            vigImg.raycastTarget = false;

            // --- Layer 2: subtle atmosphere tint strip at bottom third ---
            var atmGo = new GameObject("ScrnBg_Atmo");
            atmGo.transform.SetParent(parent, false);
            atmGo.transform.SetSiblingIndex(baseIdx + 2);
            var atmRt = atmGo.AddComponent<RectTransform>();
            atmRt.anchorMin = new Vector2(0f, 0f);
            atmRt.anchorMax = new Vector2(1f, 0.35f);
            atmRt.offsetMin = atmRt.offsetMax = Vector2.zero;
            var atmImg = atmGo.AddComponent<RawImage>();
            atmImg.texture       = MakeGradientTex(
                                       theme.AtmosphereColor,
                                       new Color(theme.AtmosphereColor.r, theme.AtmosphereColor.g,
                                                 theme.AtmosphereColor.b, 0f));
            atmImg.color         = Color.white;
            atmImg.raycastTarget = false;

            return photoImgOut;
        }

        // ---- texture helpers --------------------------------------------------------

        static Texture2D MakeGradientTex(Color bottom, Color top)
        {
            const int H = 64;
            var tex = new Texture2D(1, H, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp
            };
            for (int y = 0; y < H; y++)
                tex.SetPixel(0, y, Color.Lerp(bottom, top, y / (float)(H - 1)));
            tex.Apply();
            return tex;
        }

        static Texture2D MakeVignetteTex(int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode   = TextureWrapMode.Clamp
            };
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx   = (x - half) / half;
                float dy   = (y - half) / half;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float a    = Mathf.Clamp01((dist - 0.35f) / 0.65f);
                a = a * a;  // quadratic: soft center, stronger edges
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            return tex;
        }

        static Color LightenSlight(Color c, float amount) =>
            new Color(Mathf.Clamp01(c.r + amount),
                      Mathf.Clamp01(c.g + amount),
                      Mathf.Clamp01(c.b + amount),
                      c.a);
    }
}
