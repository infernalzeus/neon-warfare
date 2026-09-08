using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Shop card preview for a VFX skin. Draws a small stand-in troop with the effect running
    /// beside it, so the card shows the thing it is selling — and, just as importantly, shows
    /// that the effect does NOT cover the unit.
    /// </summary>
    public sealed class VfxSkinPreviewAnim : MonoBehaviour
    {
        public int   SkinIdx;
        public Color ThemeAccent = Color.cyan;

        RectTransform _root, _troop;
        RectTransform[] _bits;
        Image[]         _imgs;
        float _t;
        const int N = 6;

        public void Setup()
        {
            _root = GetComponent<RectTransform>();
            if (_root == null) return;

            // the stand-in troop, so you can see the effect stays clear of it
            var tg = new GameObject("troop");
            tg.transform.SetParent(_root, false);
            _troop = tg.AddComponent<RectTransform>();
            _troop.anchorMin = _troop.anchorMax = new Vector2(0.5f, 0.5f);
            _troop.pivot = new Vector2(0.5f, 0.5f);
            _troop.sizeDelta = new Vector2(14f, 34f);
            var ti = tg.AddComponent<Image>();
            ti.color = new Color(0.36f, 0.40f, 0.47f);
            ti.raycastTarget = false;

            _bits = new RectTransform[N];
            _imgs = new Image[N];
            for (int i = 0; i < N; i++)
            {
                var go = new GameObject("fx");
                go.transform.SetParent(_root, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(5f, 5f);
                var im = go.AddComponent<Image>();
                im.raycastTarget = false;
                _bits[i] = rt; _imgs[i] = im;
            }
            if (SkinIdx == 0) for (int i = 0; i < N; i++) _imgs[i].enabled = false;
        }

        void Update()
        {
            if (_bits == null || SkinIdx == 0) return;
            _t += Time.unscaledDeltaTime * 0.7f;
            float h = _root.rect.height, w = _root.rect.width;
            if (h <= 1f) return;
            Color c = Pal();

            for (int i = 0; i < N; i++)
            {
                var rt = _bits[i]; var im = _imgs[i];
                if (rt == null || im == null) continue;
                float f = i / (float)N;
                switch (SkinIdx)
                {
                    case 1:   // ember orbit — motes circling at hip height
                    {
                        if (i >= 3) { im.enabled = false; break; }
                        im.enabled = true;
                        float a = _t * Mathf.PI * 2f + i * 2.094f;
                        bool front = Mathf.Sin(a) > 0f;
                        rt.anchoredPosition = new Vector2(Mathf.Cos(a) * w * 0.22f,
                                                          -2f + Mathf.Sin(a) * h * 0.05f);
                        rt.sizeDelta = Vector2.one * (front ? 7f : 4.5f);
                        im.color = new Color(c.r, c.g, c.b, front ? 0.95f : 0.42f);
                        break;
                    }
                    case 2:   // static arc — ticks at the feet
                    {
                        if (i >= 4) { im.enabled = false; break; }
                        im.enabled = true;
                        bool on = Mathf.Repeat(_t, 0.5f) < 0.18f;
                        rt.anchoredPosition = new Vector2(-9f + i * 6f, -h * 0.20f + (i % 2) * 4f);
                        rt.sizeDelta = new Vector2(2f, 8f);
                        im.color = new Color(c.r, c.g, c.b, on ? 0.9f : 0f);
                        break;
                    }
                    case 3:   // frost trail — crystals in the wake
                    {
                        im.enabled = true;
                        float age = Mathf.Repeat(_t * 1.4f + f, 1f);
                        rt.anchoredPosition = new Vector2(-w * 0.14f - age * w * 0.3f, -h * 0.20f);
                        rt.sizeDelta = Vector2.one * Mathf.Lerp(6f, 2f, age);
                        im.color = new Color(c.r, c.g, c.b, 1f - age);
                        break;
                    }
                    default:  // halo ring — points tracing a flat ellipse at the feet
                    {
                        im.enabled = true;
                        float a = _t * Mathf.PI * 2f + f * Mathf.PI * 2f;
                        rt.anchoredPosition = new Vector2(Mathf.Cos(a) * w * 0.26f,
                                                          -h * 0.21f + Mathf.Sin(a) * h * 0.045f);
                        rt.sizeDelta = Vector2.one * 4.2f;
                        im.color = new Color(c.r, c.g, c.b, 0.85f);
                        break;
                    }
                }
            }
        }

        Color Pal() => SkinIdx switch
        {
            1 => new Color(1.00f, 0.60f, 0.25f),
            2 => new Color(0.66f, 0.88f, 1.00f),
            3 => new Color(0.62f, 0.91f, 1.00f),
            _ => ThemeAccent,
        };
    }
}
