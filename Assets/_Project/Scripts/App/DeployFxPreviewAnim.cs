using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Shop card preview for a deploy effect or a gem trail. Draws the effect on a loop so the
    /// card shows what the thing actually does, rather than a static swatch.
    /// </summary>
    public sealed class DeployFxPreviewAnim : MonoBehaviour
    {
        public int   FxIdx;
        public Color ThemeAccent = Color.cyan;
        public bool  TrailMode;          // true = gem trail, false = deploy effect

        RectTransform _root;
        RectTransform[] _bits;
        Image[]         _imgs;
        float _t;

        const int N = 8;

        public void Setup()
        {
            _root = GetComponent<RectTransform>();
            if (_root == null) return;

            _bits = new RectTransform[N];
            _imgs = new Image[N];
            for (int i = 0; i < N; i++)
            {
                var go = new GameObject("bit");
                go.transform.SetParent(_root, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(4f, 4f);
                var im = go.AddComponent<Image>();
                im.raycastTarget = false;
                _bits[i] = rt;
                _imgs[i] = im;
            }
            // index 0 is always NONE -- show nothing moving, so the absence reads as a choice
            if (FxIdx == 0)
                for (int i = 0; i < N; i++) _imgs[i].enabled = false;
        }

        void Update()
        {
            if (_bits == null || FxIdx == 0) return;
            _t = (_t + Time.deltaTime * 0.85f) % 1f;
            float w = _root.rect.width, h = _root.rect.height;
            if (w <= 1f || h <= 1f) return;

            Color c = PaletteFor(FxIdx);
            for (int i = 0; i < N; i++)
            {
                var rt = _bits[i]; var im = _imgs[i];
                if (rt == null || im == null) continue;
                float f = (i + 1f) / N;
                Vector2 pos; float size; float alpha;

                if (TrailMode)
                {
                    // trails burst outward from the centre and fade
                    float a  = i * Mathf.PI * 2f / N;
                    float d  = _t * h * 0.34f;
                    pos   = new Vector2(Mathf.Cos(a) * d, Mathf.Sin(a) * d);
                    size  = Mathf.Lerp(7f, 2.5f, _t);
                    alpha = 1f - _t;
                }
                else if (FxIdx == 1)              // lightning: a bolt, top to bottom
                {
                    float k = Mathf.Clamp01(_t * 2.2f - f * 0.25f);
                    pos   = new Vector2(Mathf.Sin(i * 2.1f) * (w * 0.06f), h * (0.42f - f * 0.85f));
                    size  = 3.4f;
                    alpha = k < 0.02f ? 0f : Mathf.Max(0f, 1f - (_t * 1.9f));
                }
                else if (FxIdx == 2)              // drop pod: shell halves fly apart
                {
                    float k = _t;
                    float side = (i % 2 == 0) ? -1f : 1f;
                    pos   = new Vector2(side * k * w * 0.24f, h * 0.06f - k * h * 0.1f);
                    size  = Mathf.Lerp(9f, 3f, k);
                    alpha = 1f - k;
                }
                else if (FxIdx == 3)              // phase in: scan lines rising
                {
                    float ph = Mathf.Repeat(_t * 1.6f + f, 1f);
                    pos   = new Vector2(0f, h * (-0.34f + ph * 0.7f));
                    size  = 2.4f;
                    alpha = 1f - ph;
                    rt.sizeDelta = new Vector2(w * 0.46f, size);
                    im.color = new Color(c.r, c.g, c.b, alpha);
                    rt.anchoredPosition = pos;
                    continue;
                }
                else                              // banner: a ring pushing out from the base
                {
                    float k = _t;
                    float a = i * Mathf.PI * 2f / N;
                    pos   = new Vector2(Mathf.Cos(a) * k * w * 0.3f,
                                        -h * 0.26f + Mathf.Sin(a) * k * h * 0.09f);
                    size  = Mathf.Lerp(6f, 2f, k);
                    alpha = 1f - k;
                }

                rt.sizeDelta        = new Vector2(size, size);
                rt.anchoredPosition = pos;
                im.color            = new Color(c.r, c.g, c.b, Mathf.Clamp01(alpha));
            }
        }

        Color PaletteFor(int idx)
        {
            if (TrailMode) return ThemeAccent;
            switch (idx)
            {
                case 1: return new Color(0.72f, 0.88f, 1.00f);   // lightning
                case 2: return new Color(1.00f, 0.78f, 0.42f);   // drop pod
                case 3: return new Color(0.56f, 0.94f, 0.86f);   // phase in
                default: return new Color(1.00f, 0.86f, 0.62f);  // banner
            }
        }
    }
}
