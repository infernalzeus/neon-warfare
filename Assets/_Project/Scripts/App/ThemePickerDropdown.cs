using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Theme picker. Was a 205x28 dropdown that expanded into a list of 28-unit rows -- on a
    /// phone that is a ~10pt tall strip of ~10pt rows, which is well under the 44pt minimum tap
    /// target and impossible to read. It is now a compact header button that opens a CENTRED
    /// MODAL: full-width rows, a large swatch per theme, the current theme framed in an accent
    /// border, and a close button in the top-right corner.
    ///
    /// Usage is unchanged:
    ///   ThemePickerDropdown.Build(transform, font, xOffset: 18f, yOffset: -202f);
    /// </summary>
    public class ThemePickerDropdown : MonoBehaviour
    {
        Font       _font;
        Image      _headerSwatch;
        Text       _headerName;
        GameObject _modal;

        readonly List<Image> _rowBorder = new();

        // Canvas is 1080 units wide and maps to roughly 400pt on a phone, so one unit is about
        // 0.37pt. 44pt of tap target is therefore ~120 units; rows are sized against that.
        const float RowH   = 104f;
        const float PanelW = 720f;

        public static ThemePickerDropdown Build(Transform parent, Font font,
                                                float xOffset, float yOffset)
        {
            var go = new GameObject("[ThemePicker]");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(xOffset, yOffset);
            rt.sizeDelta = new Vector2(250f, 60f);

            var w = go.AddComponent<ThemePickerDropdown>();
            w._font = font;
            w.BuildHeader(rt);
            return w;
        }

        static Color Lift(Color b, float k)
        {
            var a = NeonTheme.Active.Accent;
            return new Color(b.r + a.r * k, b.g + a.g * k, b.b + a.b * k, 1f);
        }

        // ── the small button that lives in the settings panel ────────────────
        void BuildHeader(RectTransform rt)
        {
            var t = NeonTheme.Active;

            var bg = gameObject.AddComponent<Image>();
            bg.color = Lift(t.BgCard, 0.22f);

            var brd = new GameObject("brd"); brd.transform.SetParent(transform, false);
            brd.transform.SetAsFirstSibling();
            var brdRt = brd.AddComponent<RectTransform>();
            brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
            brdRt.offsetMin = new Vector2(-2, -2); brdRt.offsetMax = new Vector2(2, 2);
            brd.AddComponent<Image>().color = new Color(
                t.Accent.r * 0.45f + 0.10f, t.Accent.g * 0.45f + 0.11f, t.Accent.b * 0.45f + 0.13f, 1f);

            var swGo = new GameObject("sw"); swGo.transform.SetParent(transform, false);
            var swRt = swGo.AddComponent<RectTransform>();
            swRt.anchorMin = new Vector2(0f, 0.5f); swRt.anchorMax = new Vector2(0f, 0.5f);
            swRt.pivot = new Vector2(0f, 0.5f);
            swRt.anchoredPosition = new Vector2(10f, 0f);
            swRt.sizeDelta = new Vector2(26f, 26f);
            _headerSwatch = swGo.AddComponent<Image>();
            _headerSwatch.color = t.Accent;
            _headerSwatch.raycastTarget = false;

            var nGo = new GameObject("n"); nGo.transform.SetParent(transform, false);
            var nRt = nGo.AddComponent<RectTransform>();
            nRt.anchorMin = Vector2.zero; nRt.anchorMax = Vector2.one;
            nRt.offsetMin = new Vector2(44f, 0f); nRt.offsetMax = new Vector2(-10f, 0f);
            _headerName = nGo.AddComponent<Text>();
            _headerName.font = _font; _headerName.fontSize = 24; _headerName.fontStyle = FontStyle.Bold;
            _headerName.color = t.TextBright;
            _headerName.alignment = TextAnchor.MiddleLeft;
            _headerName.supportRichText = false; _headerName.raycastTarget = false;
            _headerName.resizeTextForBestFit = true;
            _headerName.resizeTextMinSize = 14; _headerName.resizeTextMaxSize = 24;
            _headerName.text = t.Name;

            var btn = gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Open);
        }

        // ── the centred modal ────────────────────────────────────────────────
        void Open()
        {
            if (_modal != null) { Close(); return; }
            var t = NeonTheme.Active;

            // Parented to the canvas root, not to this widget, so the modal is centred on the
            // screen rather than on a button sitting in the corner of a settings panel.
            Transform root = transform;
            while (root.parent != null && root.parent.GetComponent<Canvas>() == null)
                root = root.parent;

            _modal = new GameObject("ThemeModal");
            _modal.transform.SetParent(root, false);
            var mRt = _modal.AddComponent<RectTransform>();
            mRt.anchorMin = Vector2.zero; mRt.anchorMax = Vector2.one;
            mRt.offsetMin = mRt.offsetMax = Vector2.zero;
            var dim = _modal.AddComponent<Image>();
            dim.color = new Color(0.02f, 0.02f, 0.04f, 0.82f);
            var dimBtn = _modal.AddComponent<Button>();
            dimBtn.transition = Selectable.Transition.None;
            dimBtn.onClick.AddListener(Close);          // tap outside dismisses

            int n = NeonTheme.Count;
            float panelH = 132f + n * (RowH + 10f);

            var panel = new GameObject("panel"); panel.transform.SetParent(_modal.transform, false);
            var pRt = panel.AddComponent<RectTransform>();
            pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
            pRt.pivot = new Vector2(0.5f, 0.5f);
            pRt.anchoredPosition = Vector2.zero;
            pRt.sizeDelta = new Vector2(PanelW, panelH);
            panel.AddComponent<Image>().color = Lift(t.BgPanel, 0.06f);
            panel.AddComponent<Button>().transition = Selectable.Transition.None;  // swallow taps

            var pBrd = new GameObject("brd"); pBrd.transform.SetParent(panel.transform, false);
            pBrd.transform.SetAsFirstSibling();
            var pbRt = pBrd.AddComponent<RectTransform>();
            pbRt.anchorMin = Vector2.zero; pbRt.anchorMax = Vector2.one;
            pbRt.offsetMin = new Vector2(-3, -3); pbRt.offsetMax = new Vector2(3, 3);
            pBrd.AddComponent<Image>().color = t.Accent;

            MkText(panel.transform, "THEME", new Vector2(0f, 1f), new Vector2(1f, 1f),
                   new Vector2(28f, -56f), new Vector2(300f, 48f), 38, t.TextBright,
                   TextAnchor.MiddleLeft);

            // close button, top-right
            var xGo = new GameObject("close"); xGo.transform.SetParent(panel.transform, false);
            var xRt = xGo.AddComponent<RectTransform>();
            xRt.anchorMin = xRt.anchorMax = new Vector2(1f, 1f);
            xRt.pivot = new Vector2(1f, 1f);
            xRt.anchoredPosition = new Vector2(-16f, -16f);
            xRt.sizeDelta = new Vector2(76f, 76f);
            xGo.AddComponent<Image>().color = Lift(t.BgCard, 0.20f);
            var xBrd = new GameObject("brd"); xBrd.transform.SetParent(xGo.transform, false);
            xBrd.transform.SetAsFirstSibling();
            var xbRt = xBrd.AddComponent<RectTransform>();
            xbRt.anchorMin = Vector2.zero; xbRt.anchorMax = Vector2.one;
            xbRt.offsetMin = new Vector2(-2, -2); xbRt.offsetMax = new Vector2(2, 2);
            xBrd.AddComponent<Image>().color = new Color(0.72f, 0.24f, 0.20f);
            MkText(xGo.transform, "×", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
                   46, new Color(1f, 0.86f, 0.84f), TextAnchor.MiddleCenter);
            var xBtn = xGo.AddComponent<Button>();
            xBtn.transition = Selectable.Transition.None;
            xBtn.onClick.AddListener(Close);

            _rowBorder.Clear();
            for (int i = 0; i < n; i++)
            {
                var th = NeonTheme.Get(i);
                bool cur = i == GameSettings.ThemeIndex;

                var row = new GameObject($"row{i}"); row.transform.SetParent(panel.transform, false);
                var rRt = row.AddComponent<RectTransform>();
                rRt.anchorMin = rRt.anchorMax = new Vector2(0.5f, 1f);
                rRt.pivot = new Vector2(0.5f, 1f);
                rRt.anchoredPosition = new Vector2(0f, -(110f + i * (RowH + 10f)));
                rRt.sizeDelta = new Vector2(PanelW - 44f, RowH);
                row.AddComponent<Image>().color = Lift(th.BgCard, cur ? 0.30f : 0.14f);

                // the current theme is framed rather than merely tinted
                var rb = new GameObject("brd"); rb.transform.SetParent(row.transform, false);
                rb.transform.SetAsFirstSibling();
                var rbRt = rb.AddComponent<RectTransform>();
                rbRt.anchorMin = Vector2.zero; rbRt.anchorMax = Vector2.one;
                float bw = cur ? 4f : 2f;
                rbRt.offsetMin = new Vector2(-bw, -bw); rbRt.offsetMax = new Vector2(bw, bw);
                var rbImg = rb.AddComponent<Image>();
                rbImg.color = cur ? th.Accent
                                  : new Color(th.Accent.r*0.30f+0.09f, th.Accent.g*0.30f+0.10f,
                                              th.Accent.b*0.30f+0.12f, 1f);
                _rowBorder.Add(rbImg);

                // swatch: three bands of the theme's own palette
                var sw = new GameObject("sw"); sw.transform.SetParent(row.transform, false);
                var sRt = sw.AddComponent<RectTransform>();
                sRt.anchorMin = new Vector2(0f, 0.5f); sRt.anchorMax = new Vector2(0f, 0.5f);
                sRt.pivot = new Vector2(0f, 0.5f);
                sRt.anchoredPosition = new Vector2(18f, 0f);
                sRt.sizeDelta = new Vector2(84f, 64f);
                sw.AddComponent<Image>().color = th.BgDeep;
                MkBand(sw.transform, th.Accent,    0.00f, 0.46f);
                MkBand(sw.transform, th.TroopTint, 0.50f, 0.74f);
                MkBand(sw.transform, th.EnemyTint, 0.78f, 1.00f);

                MkText(row.transform, th.Name, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                       new Vector2(118f, 14f), new Vector2(420f, 40f), 32,
                       cur ? th.TextBright : th.TextMid, TextAnchor.MiddleLeft);
                MkText(row.transform, th.AestheticTag, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                       new Vector2(118f, -22f), new Vector2(420f, 30f), 22,
                       th.TextDim, TextAnchor.MiddleLeft);

                if (cur)
                    MkText(row.transform, "ACTIVE", new Vector2(1f, 0.5f), new Vector2(1f, 0.5f),
                           new Vector2(-96f, 0f), new Vector2(170f, 34f), 22,
                           th.Accent, TextAnchor.MiddleRight);

                int capture = i;
                var rowBtn = row.AddComponent<Button>();
                rowBtn.transition = Selectable.Transition.None;
                rowBtn.onClick.AddListener(() => SelectTheme(capture));
            }
        }

        static void MkBand(Transform parent, Color c, float x0, float x1)
        {
            var g = new GameObject("b"); g.transform.SetParent(parent, false);
            var r = g.AddComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, 0f); r.anchorMax = new Vector2(x1, 1f);
            r.offsetMin = new Vector2(2f, 2f); r.offsetMax = new Vector2(-2f, -2f);
            var im = g.AddComponent<Image>();
            im.color = new Color(c.r, c.g, c.b, 1f);
            im.raycastTarget = false;
        }

        void MkText(Transform parent, string txt, Vector2 aMin, Vector2 aMax,
                    Vector2 pos, Vector2 size, int fs, Color col, TextAnchor align)
        {
            var g = new GameObject("t"); g.transform.SetParent(parent, false);
            var r = g.AddComponent<RectTransform>();
            r.anchorMin = aMin; r.anchorMax = aMax;
            if (aMin == Vector2.zero && aMax == Vector2.one)
            {
                r.offsetMin = r.offsetMax = Vector2.zero;
            }
            else
            {
                r.pivot = new Vector2(aMin.x, 0.5f);
                r.anchoredPosition = pos; r.sizeDelta = size;
            }
            var tx = g.AddComponent<Text>();
            tx.font = _font; tx.fontSize = fs; tx.fontStyle = FontStyle.Bold;
            tx.color = col; tx.alignment = align;
            tx.supportRichText = false; tx.raycastTarget = false;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow;
            tx.text = txt;
        }

        void Close()
        {
            if (_modal != null) Destroy(_modal);
            _modal = null;
            _rowBorder.Clear();
        }

        void SelectTheme(int index)
        {
            if (GameSettings.ThemeIndex == index) { Close(); return; }
            GameSettings.ThemeIndex = index;
            Close();
            FindAnyObjectByType<BattleScene>()?.Restart();
        }
    }
}
