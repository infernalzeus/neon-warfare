using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Shared UI foundation: display font + text effects.
    /// LoadDisplayFont picks the best installed OS display face (zero asset imports);
    /// Shadow/Title attach UGUI effects so text pops against busy backgrounds.
    /// </summary>
    public static class NeonUI
    {
        static Font _font;

        /// <summary>
        /// Military/DIN-style display font from the OS, falling back to the Unity built-in.
        /// Cached after first call — safe to call from every screen's Init.
        /// </summary>
        public static Font LoadDisplayFont()
        {
            if (_font != null) return _font;

            // SemiBold first: heavier ink renders crisp; the variable-font default is too light
            string[] preferred = { "Bahnschrift SemiBold", "Bahnschrift SemiBold Condensed",
                                   "Bahnschrift", "Segoe UI Semibold", "Verdana" };
            var installed = Font.GetOSInstalledFontNames();
            foreach (string want in preferred)
            {
                foreach (string have in installed)
                {
                    if (have == want)
                    {
                        _font = Font.CreateDynamicFontFromOSFont(want, 16);
                        if (_font != null) return _font;
                    }
                }
            }
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return _font;
        }

        /// <summary>Drop shadow for body text. Skipped below 13px — effects smear tiny glyphs.</summary>
        public static void Shadow(Text t)
        {
            if (t == null || t.fontSize < 13 || t.GetComponent<Shadow>() != null) return;
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor    = new Color(0f, 0f, 0f, 0.55f);
            sh.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>Hard 1px outline for titles — crisp edge, no halo spread.</summary>
        public static void Title(Text t)
        {
            if (t == null) return;
            if (t.GetComponent<Outline>() == null)
            {
                var ol = t.gameObject.AddComponent<Outline>();
                ol.effectColor    = new Color(0f, 0f, 0f, 0.95f);
                ol.effectDistance = new Vector2(1f, -1f);
            }
        }

        // ── beveled panel/button factory (the CosmeticsScreen card treatment) ──

        public static GameObject Rect(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        /// <summary>Border ring: image behind the target, expanded outward by px.</summary>
        public static void Stroke(Transform t, Color c, float px)
        {
            var go = new GameObject("stroke");
            go.transform.SetParent(t, false);
            go.transform.SetAsFirstSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-px, -px); rt.offsetMax = new Vector2(px, px);
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        }

        /// <summary>Top highlight + bottom shade on an existing panel.</summary>
        public static void Bevel(Transform t)
        {
            Edge(t, new Color(1f, 1f, 1f, 0.14f), 2.5f, top: true);
            Edge(t, new Color(0f, 0f, 0f, 0.45f), 3f, top: false);
        }

        static void Edge(Transform t, Color c, float h, bool top)
        {
            var go = new GameObject(top ? "hl" : "sh");
            go.transform.SetParent(t, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, top ? 1f : 0f);
            rt.anchorMax = new Vector2(1f, top ? 1f : 0f);
            rt.pivot = new Vector2(0.5f, top ? 1f : 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(0f, h);
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        }

        /// <summary>Layered panel: dark outer stroke → accent line → fill → top bevel + bottom shade.</summary>
        public static GameObject BevelPanel(Transform parent, string name, Vector2 aMin, Vector2 aMax,
            Color fill, Color line)
        {
            var go = Rect(parent, name, aMin, aMax);
            go.AddComponent<Image>().color = fill;
            Stroke(go.transform, new Color(0.01f, 0.01f, 0.03f, 1f), 2.5f);
            Stroke(go.transform, line, 1f);
            Edge(go.transform, new Color(1f, 1f, 1f, 0.14f), 2.5f, top: true);
            Edge(go.transform, new Color(0f, 0f, 0f, 0.45f), 3f, top: false);
            return go;
        }

        /// <summary>
        /// Labeled 0–100% slider: label (left 32%) · track+fill+handle (mid 52%) · live % (right).
        /// Placed by anchor fractions of the parent, so it works inside any panel or row.
        /// </summary>
        public static Slider SliderBar(Transform parent, Font font, string label,
            Vector2 aMin, Vector2 aMax, float initial01, Color accent, System.Action<float> onChange)
        {
            var row = Rect(parent, "slider_" + label, aMin, aMax);

            var lGo = Rect(row.transform, "lbl", new Vector2(0f, 0f), new Vector2(0.32f, 1f));
            var lt = lGo.AddComponent<Text>();
            lt.font = font; lt.fontSize = UIScale.FontSmall; lt.color = new Color(0.72f, 0.78f, 0.88f);
            lt.alignment = TextAnchor.MiddleLeft; lt.text = label;
            lt.supportRichText = false; lt.raycastTarget = false;

            var vGo = Rect(row.transform, "val", new Vector2(0.88f, 0f), new Vector2(1f, 1f));
            var vt = vGo.AddComponent<Text>();
            vt.font = font; vt.fontSize = UIScale.FontSmall; vt.color = accent;
            vt.alignment = TextAnchor.MiddleRight;
            vt.text = Mathf.RoundToInt(initial01 * 100f) + "%";
            vt.supportRichText = false; vt.raycastTarget = false;

            var sGo = Rect(row.transform, "bar", new Vector2(0.34f, 0.5f), new Vector2(0.86f, 0.5f));
            var sRt = sGo.GetComponent<RectTransform>();
            sRt.sizeDelta = new Vector2(0f, 16f); // fixed height strip centered vertically

            // Track
            var track = Rect(sGo.transform, "track", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            var trackRt = track.GetComponent<RectTransform>();
            trackRt.sizeDelta = new Vector2(0f, 5f);
            track.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 1f);
            Stroke(track.transform, new Color(0f, 0f, 0f, 0.8f), 1f);

            // Fill
            var fillArea = Rect(sGo.transform, "fillArea", new Vector2(0f, 0.5f), new Vector2(1f, 0.5f));
            var faRt = fillArea.GetComponent<RectTransform>();
            faRt.sizeDelta = new Vector2(0f, 5f);
            var fill = Rect(fillArea.transform, "fill", Vector2.zero, Vector2.one);
            fill.AddComponent<Image>().color = new Color(accent.r, accent.g, accent.b, 0.85f);

            // Handle
            var handleArea = Rect(sGo.transform, "handleArea", Vector2.zero, Vector2.one);
            var handle = Rect(handleArea.transform, "handle", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            var hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(10f, 16f);
            var hImg = handle.AddComponent<Image>();
            hImg.color = Color.white;
            Stroke(handle.transform, new Color(0f, 0f, 0f, 0.9f), 1f);

            var slider = sGo.AddComponent<Slider>();
            slider.fillRect      = fill.GetComponent<RectTransform>();
            slider.handleRect    = hRt;
            slider.targetGraphic = hImg;
            slider.minValue = 0f; slider.maxValue = 1f;
            slider.value = Mathf.Clamp01(initial01);
            slider.onValueChanged.AddListener(v =>
            {
                vt.text = Mathf.RoundToInt(v * 100f) + "%";
                onChange?.Invoke(v);
            });
            return slider;
        }

        /// <summary>Full beveled button with ButtonFeel, label, and click action.</summary>
        public static GameObject BevelButton(Transform parent, string label, Font font, int fontSize,
            Vector2 aMin, Vector2 aMax, Color fill, Color line, Color txtCol, System.Action onClick)
        {
            var go = BevelPanel(parent, "btn_" + label, aMin, aMax, fill, line);
            var tGo = Rect(go.transform, "txt", Vector2.zero, Vector2.one);
            var t = tGo.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.fontStyle = FontStyle.Bold;
            t.color = txtCol; t.alignment = TextAnchor.MiddleCenter;
            t.text = label; t.supportRichText = false; t.raycastTarget = false;
            Title(t);
            var btn = go.GetComponent<Image>() != null ? go.AddComponent<Button>() : null;
            if (btn != null)
            {
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { AudioManager.Play(AudioManager.Sfx.Click); onClick?.Invoke(); });
                go.AddComponent<ButtonFeel>();
            }
            return go;
        }
    }
}
