using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using NW.Board.Domain;
using NW.Combat.Domain;

namespace NW.App
{
    /// <summary>
    /// HUD v3 — top bar (minimal), currency wallet sidebar, command strip.
    ///
    /// Sidebar layout (left of match board):
    ///   ┌─────────────┐
    ///   │ ◈ WALLET ◈  │
    ///   │ [E] ████░░  │  count
    ///   │ [P] ██░░░░  │  count
    ///   │ ...         │
    ///   └─────────────┘
    ///
    /// Top bar: level indicator + act label + combo — no gem chips (moved to sidebar).
    /// Command strip: unit cards with ready pulsing border and gem-icon cost display.
    /// </summary>
    public sealed class HUDView : MonoBehaviour
    {
        // Gem colors are theme-aware: use ThemeLocale.GemColor(i) at call-site
        static Color GC(int i) => ThemeLocale.GemColor(i);

        const int   FILL_MAX = 20;   // resources to fill bar 100%
        const float PULSE_HZ = 3.8f; // card-ready pulse frequency

        public System.Action<int> OnCardClicked;
        public System.Action<int> OnLaneDeploy;    // set by BattleScene, used by drag-drop

        BattleSession _session;
        Font          _font;

        // Sidebar: one row per gem
        readonly RawImage[]      _sidebarIcons    = new RawImage[BoardModel.GemKindCount];
        readonly Image[]         _sidebarFills    = new Image[BoardModel.GemKindCount];
        readonly Text[]          _sidebarCounts   = new Text[BoardModel.GemKindCount];
        readonly Image[]         _sidebarRows     = new Image[BoardModel.GemKindCount];
        readonly RectTransform[] _sidebarRowRts   = new RectTransform[BoardModel.GemKindCount];
        readonly float[]         _sidebarFlash    = new float[BoardModel.GemKindCount];
        readonly float[]         _sidebarErrFlash = new float[BoardModel.GemKindCount];
        readonly int[]           _lastRes         = new int[BoardModel.GemKindCount];

        // Top bar labels
        Text _levelLabel, _actLabel, _comboLabel;

        // Command strip
        readonly List<CardWidget> _cards = new();
        int _selectedCard = -1;

        sealed class CardWidget
        {
            public Transform Root;
            public Image     Bg, Cooldown, Selected, ReadyBorder;
            public Text      NameLbl, StatLbl, CostLbl;
            public RawImage  Icon;
            public RectTransform CdBar;   // bright cooldown meter along the card bottom
            public Image     CdRing;      // radial cooldown meter around the icon
            public Text          CdNum;   // seconds-remaining countdown
        }

        int    _lastCombo;
        bool[] _wasOnCooldown = System.Array.Empty<bool>();

        // Win overlay
        GameObject  _winPanel;
        CanvasGroup _winBgCg;
        CanvasGroup _winCardCg;
        Text        _winTitleText;
        Text        _winStatsText;

        // ── init ────────────────────────────────────────────────────────────────

        public void Init(BattleSession session, RectTransform topBar,
                         RectTransform strip, RectTransform sidebar, Font font)
        {
            _session = session;
            _font    = font;
            BuildTopBar(topBar);
            BuildSidebar(sidebar);
            BuildCommandStrip(strip);
            BuildWinOverlay(strip);
        }

        // ── top bar (minimal) ────────────────────────────────────────────────────

        void BuildTopBar(RectTransform parent)
        {
            var t = NeonTheme.Active;
            parent.GetComponent<Image>().color = t.BgPanel;

            // Level indicator (left)
            var lvPanel = MakePanel(parent, "LvPanel", new Vector2(0f, 0f), new Vector2(0.14f, 1f));
            _levelLabel = AddText(lvPanel, $"LV {GameSettings.SelectedLevel}", UIScale.FontBody,
                t.TextMid, TextAnchor.MiddleCenter);

            // Act label (center)
            var act = MakePanel(parent, "Act", new Vector2(0.14f, 0f), new Vector2(0.58f, 1f));
            _actLabel = AddText(act, "ACT 1  SKIRMISH", UIScale.FontBody, t.TextBright, TextAnchor.MiddleCenter);

            // Combo label (right)
            var combo = MakePanel(parent, "Combo", new Vector2(0.58f, 0f), new Vector2(1f, 1f));
            _comboLabel = AddText(combo, "COMBO ×0", UIScale.FontBody, t.Accent, TextAnchor.MiddleCenter);
        }

        // ── currency sidebar ─────────────────────────────────────────────────────

        void BuildSidebar(RectTransform parent)
        {
            var t = NeonTheme.Active;
            parent.GetComponent<Image>().color = t.BgPanel;
            // Portrait: the wallet is a full-width top strip → lay the 5 gem cells horizontally
            // (icon over a thin fill bar, count beneath the icon). Landscape: vertical column.
            bool portrait = Screen.height > Screen.width;

            // Vertical separator line (right edge)
            var sep = new GameObject("sep"); sep.transform.SetParent(parent, false);
            var sepRt = sep.AddComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(1f, 0f); sepRt.anchorMax = new Vector2(1f, 1f);
            sepRt.pivot = new Vector2(1f, 0.5f);
            sepRt.offsetMin = new Vector2(-2, 0); sepRt.offsetMax = Vector2.zero;
            sep.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.35f);

            // Header
            var hdrGo = new GameObject("hdr"); hdrGo.transform.SetParent(parent, false);
            var hdrRt = hdrGo.AddComponent<RectTransform>();
            hdrRt.anchorMin = new Vector2(0f, 0.92f); hdrRt.anchorMax = Vector2.one;
            hdrRt.offsetMin = hdrRt.offsetMax = Vector2.zero;
            var hdrTxt = hdrGo.AddComponent<Text>();
            hdrTxt.font = _font; hdrTxt.fontSize = UIScale.FontSmall; hdrTxt.fontStyle = FontStyle.Bold;
            hdrTxt.color = t.TextMid;
            hdrTxt.alignment = TextAnchor.MiddleCenter;
            hdrTxt.text = "◈ WALLET ◈"; hdrTxt.supportRichText = false; hdrTxt.raycastTarget = false;
            hdrGo.SetActive(!portrait); // no room for the header in the horizontal top strip

            // One row per gem type — stacked top-to-bottom in the lower 92%
            float rowH = 0.92f / BoardModel.GemKindCount;
            for (int i = 0; i < BoardModel.GemKindCount; i++)
            {
                float y0 = 0.92f - (i + 1) * rowH;
                float y1 = 0.92f - i * rowH;

                var rowGo = new GameObject($"Row{i}"); rowGo.transform.SetParent(parent, false);
                var rowRt = rowGo.AddComponent<RectTransform>();
                if (portrait)
                {
                    float x0 = i / (float)BoardModel.GemKindCount, x1 = (i + 1) / (float)BoardModel.GemKindCount;
                    rowRt.anchorMin = new Vector2(x0 + 0.006f, 0.08f);
                    rowRt.anchorMax = new Vector2(x1 - 0.006f, 0.94f);
                }
                else
                {
                    rowRt.anchorMin = new Vector2(0.04f, y0 + 0.005f);
                    rowRt.anchorMax = new Vector2(0.96f, y1 - 0.005f);
                }
                rowRt.offsetMin = rowRt.offsetMax = Vector2.zero;
                Color gc = GC(i);
                var rowBg = rowGo.AddComponent<Image>();
                rowBg.color = new Color(gc.r * 0.06f, gc.g * 0.06f, gc.b * 0.06f);
                _sidebarRows[i]   = rowBg;
                _sidebarRowRts[i] = rowRt;

                // Gem icon — fixed square, no stretching
                var iconGo = new GameObject("icon"); iconGo.transform.SetParent(rowRt, false);
                var iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.13f, 0.5f);
                iconRt.pivot     = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(UIScale.IconGemHUD, UIScale.IconGemHUD);
                var iconImg = iconGo.AddComponent<RawImage>();
                iconImg.texture = NeonArt.Gem(i, NeonTheme.Active.GemStyle);
                iconImg.color   = Color.white; iconImg.raycastTarget = false;
                _sidebarIcons[i] = iconImg;

                // Fill bar background (26%–82%)
                var barBgGo = new GameObject("barBg"); barBgGo.transform.SetParent(rowRt, false);
                var barBgRt = barBgGo.AddComponent<RectTransform>();
                barBgRt.anchorMin = new Vector2(0.27f, 0.22f);
                barBgRt.anchorMax = new Vector2(0.80f, 0.78f);
                barBgRt.offsetMin = barBgRt.offsetMax = Vector2.zero;
                barBgGo.AddComponent<Image>().color = new Color(gc.r * 0.06f, gc.g * 0.06f, gc.b * 0.06f, 1f);

                // Fill bar actual fill (stretches left-to-right via anchorMax.x)
                var fillGo = new GameObject("fill"); fillGo.transform.SetParent(barBgGo.transform, false);
                var fillRt = fillGo.AddComponent<RectTransform>();
                fillRt.anchorMin = Vector2.zero;
                fillRt.anchorMax = new Vector2(0f, 1f);  // x animated each frame, y always 1
                fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
                var fillImg = fillGo.AddComponent<Image>();
                fillImg.color = new Color(gc.r * 0.7f, gc.g * 0.7f, gc.b * 0.7f);
                fillImg.raycastTarget = false;
                _sidebarFills[i] = fillImg;

                // Fill bar border/glow
                var barBrd = new GameObject("brd"); barBrd.transform.SetParent(barBgGo.transform, false);
                barBrd.transform.SetAsFirstSibling();
                var barBrdRt = barBrd.AddComponent<RectTransform>();
                barBrdRt.anchorMin = Vector2.zero; barBrdRt.anchorMax = Vector2.one;
                barBrdRt.offsetMin = new Vector2(-1, -1); barBrdRt.offsetMax = new Vector2(1, 1);
                barBrd.AddComponent<Image>().color = new Color(gc.r * 0.3f, gc.g * 0.3f, gc.b * 0.3f, 0.6f);

                // Count label (82%–100%)
                var cntGo = new GameObject("cnt"); cntGo.transform.SetParent(rowRt, false);
                var cntRt = cntGo.AddComponent<RectTransform>();
                cntRt.anchorMin = new Vector2(0.82f, 0f);
                cntRt.anchorMax = Vector2.one;
                cntRt.offsetMin = cntRt.offsetMax = Vector2.zero;
                var cntTxt = cntGo.AddComponent<Text>();
                cntTxt.font = _font; cntTxt.fontSize = UIScale.FontBody; cntTxt.fontStyle = FontStyle.Bold;
                cntTxt.color = gc; cntTxt.alignment = TextAnchor.MiddleCenter;
                cntTxt.text = "0"; cntTxt.supportRichText = false; cntTxt.raycastTarget = false;
                _sidebarCounts[i] = cntTxt;
            }
        }

        // ── command strip ────────────────────────────────────────────────────────

        void BuildCommandStrip(RectTransform parent)
        {
            var t = NeonTheme.Active;
            parent.GetComponent<Image>().color = t.BgPanel;

            int count = Mathf.Min(_session.DeployOptions.Count, 10);
            if (count == 0) return;
            float cardW = Mathf.Min(0.13f, 0.88f / count);
            float x0    = 0.5f - cardW * count * 0.5f;

            // Hint panel on left
            var hint = MakePanel(parent, "Hint", new Vector2(0f, 0f), new Vector2(x0 - 0.01f, 1f));
            AddText(hint, "TAP a card\nthen a lane\nto deploy",
                UIScale.FontTiny, t.TextDim, TextAnchor.MiddleCenter);

            for (int i = 0; i < count; i++)
            {
                int idx = i;
                var (name, spec, cost, cooldown) = _session.DeployOptions[i];

                var card = MakePanel(parent, $"Card_{name}",
                    new Vector2(x0 + i * cardW + 0.003f, 0.06f),
                    new Vector2(x0 + (i + 1) * cardW - 0.003f, 0.94f));
                var bg = card.GetComponent<Image>(); bg.color = t.BgCard;

                // Static card frame decoration based on active cosmetic
                ApplyCardFrame(card, t.Accent, NeonCosmetics.ActiveCardFrame);

                // Ready border (pulsing neon outline when deployable)
                var rdyGo = new GameObject("rdy"); rdyGo.transform.SetParent(card, false);
                rdyGo.transform.SetAsFirstSibling();
                var rdyRt = rdyGo.AddComponent<RectTransform>();
                rdyRt.anchorMin = Vector2.zero; rdyRt.anchorMax = Vector2.one;
                rdyRt.offsetMin = new Vector2(-3, -3); rdyRt.offsetMax = new Vector2(3, 3);
                var rdyImg = rdyGo.AddComponent<Image>(); rdyImg.color = new Color(0, 0, 0, 0);

                // Selection highlight
                var sel = MakeOverlay(card, "sel", new Color(0, 0, 0, 0));

                // Unusable-state dim. This used to double as the progress read via a top-down
                // vertical fill, which hid the sprite exactly while the player was deciding what
                // to play next. It is now a flat, lighter veil; the ring below carries the timing.
                var cd = MakeOverlay(card, "cd", new Color(0, 0, 0, 0.42f));
                cd.type = Image.Type.Simple;
                cd.enabled = false;

                // Radial cooldown ring — reads instantly and leaves the icon visible.
                var cdRingGo = new GameObject("cdRing"); cdRingGo.transform.SetParent(card, false);
                var cdRingRt = cdRingGo.AddComponent<RectTransform>();
                cdRingRt.anchorMin = cdRingRt.anchorMax = new Vector2(0.5f, 0.62f);
                cdRingRt.pivot = new Vector2(0.5f, 0.5f);
                cdRingRt.sizeDelta = new Vector2(UIScale.IconUnitCard * 1.42f,
                                                 UIScale.IconUnitCard * 1.42f);
                var cdRingImg = cdRingGo.AddComponent<Image>();
                cdRingImg.sprite = NeonArt.CooldownRing();
                cdRingImg.color  = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.95f);
                cdRingImg.type = Image.Type.Filled;
                cdRingImg.fillMethod = Image.FillMethod.Radial360;
                cdRingImg.fillOrigin = (int)Image.Origin360.Top;
                cdRingImg.fillClockwise = true;
                cdRingImg.fillAmount = 0f;
                cdRingImg.raycastTarget = false;
                cdRingGo.SetActive(false);

                // Bright cooldown meter bar along the card bottom (width = cooldown fraction)
                var cdBarGo = new GameObject("cdBar"); cdBarGo.transform.SetParent(card, false);
                var cdBarRt = cdBarGo.AddComponent<RectTransform>();
                cdBarRt.anchorMin = new Vector2(0.06f, 0.02f);
                cdBarRt.anchorMax = new Vector2(0.06f, 0.075f); // max.x driven each frame
                cdBarRt.offsetMin = cdBarRt.offsetMax = Vector2.zero;
                var cdBarImg = cdBarGo.AddComponent<Image>();
                cdBarImg.color = t.Accent; cdBarImg.raycastTarget = false;
                cdBarGo.SetActive(false);

                // Seconds-remaining countdown, centred on the card
                var cdNumGo = new GameObject("cdNum"); cdNumGo.transform.SetParent(card, false);
                var cdNumRt = cdNumGo.AddComponent<RectTransform>();
                cdNumRt.anchorMin = Vector2.zero; cdNumRt.anchorMax = Vector2.one;
                cdNumRt.offsetMin = cdNumRt.offsetMax = Vector2.zero;
                var cdNumTxt = cdNumGo.AddComponent<Text>();
                cdNumTxt.font = _font; cdNumTxt.fontSize = UIScale.FontH1; cdNumTxt.fontStyle = FontStyle.Bold;
                cdNumTxt.color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.95f);
                cdNumTxt.alignment = TextAnchor.MiddleCenter;
                cdNumTxt.supportRichText = false; cdNumTxt.raycastTarget = false;
                cdNumGo.SetActive(false);

                // Key label [1]…[5] — top-left badge
                var keyGo = new GameObject("key"); keyGo.transform.SetParent(card, false);
                var keyRt = keyGo.AddComponent<RectTransform>();
                keyRt.anchorMin = new Vector2(0f, 0.88f); keyRt.anchorMax = new Vector2(0.32f, 1f);
                keyRt.offsetMin = new Vector2(4, 0); keyRt.offsetMax = Vector2.zero;
                var keyTxt = keyGo.AddComponent<Text>();
                keyTxt.font = _font; keyTxt.fontSize = UIScale.FontSmall;
                keyTxt.color = t.TextDim;
                keyTxt.alignment = TextAnchor.UpperLeft;
                keyTxt.text = $"[{i + 1}]"; keyTxt.supportRichText = false; keyTxt.raycastTarget = false;

                // Unit icon — fixed square at card center-top, no stretching
                var iconGo = new GameObject("icon"); iconGo.transform.SetParent(card, false);
                var iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.72f);
                iconRt.pivot     = new Vector2(0.5f, 0.5f);
                iconRt.anchoredPosition = Vector2.zero;
                iconRt.sizeDelta = new Vector2(UIScale.IconUnitCard, UIScale.IconUnitCard);
                var iconImg = iconGo.AddComponent<RawImage>();
                iconImg.texture  = NeonArt.Unit(ThemeLocale.ArtId(spec.Id), true);
                iconImg.color    = NeonCosmetics.GetTroopTint();
                iconImg.raycastTarget = false;

                // Name — themed display name (PIGEON, KNIGHT etc. for medieval theme)
                // The card is 106 canvas units wide; FontH2 renders at 31pt in portrait, which
                // fits five characters. FORGEWALKER, PYROMANCER and KUNOICHI all clipped, and
                // every theme has at least one name that does. Best-fit across two lines solves
                // it generically -- whatever the theme names a troop, it shrinks to fit rather
                // than losing its tail.
                var nameLbl = MakeLabelInCard(card, new Vector2(0f, 0.28f), new Vector2(1f, 0.50f),
                    ThemeLocale.TroopName(spec.Id), UIScale.FontH2, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                nameLbl.horizontalOverflow = HorizontalWrapMode.Wrap;
                nameLbl.verticalOverflow   = VerticalWrapMode.Truncate;
                nameLbl.resizeTextForBestFit = true;
                nameLbl.resizeTextMinSize = 12;
                nameLbl.resizeTextMaxSize = UIScale.FontH2;

                // Stats
                var statLbl = MakeLabelInCard(card, new Vector2(0f, 0.16f), new Vector2(1f, 0.31f),
                    $"HP {(int)spec.MaxHp}  ATK {(int)spec.Damage}", UIScale.FontSmall,
                    t.TextMid, FontStyle.Normal, TextAnchor.MiddleCenter);

                // Cost row with gem icons + number
                var costLbl = BuildCostRow(card, cost);

                var btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => OnCardClicked?.Invoke(idx));
                card.gameObject.AddComponent<ButtonFeel>();

                // Tooltip
                var cardTT = card.gameObject.AddComponent<TooltipTarget>();
                (cardTT.Title, cardTT.Body) = TooltipSystem.TroopTooltip(spec.Id);

                // Drag-and-drop troop deploy support
                var dragger = card.gameObject.AddComponent<CardDragDeploy>();
                dragger.Init(idx, OnCardClicked, OnDeployDrag, lane => OnLaneDeploy?.Invoke(lane));

                _cards.Add(new CardWidget
                {
                    Root = card.gameObject.transform,
                    Bg = bg, Cooldown = cd, Selected = sel, ReadyBorder = rdyImg,
                    NameLbl = nameLbl, StatLbl = statLbl, CostLbl = costLbl,
                    Icon = iconImg, CdBar = cdBarRt, CdNum = cdNumTxt, CdRing = cdRingImg,
                });
            }
            _wasOnCooldown = new bool[_cards.Count];
        }

        /// <summary>
        /// Builds a cost row with gem RawImage icons + number labels instead of just text.
        /// Returns the first text label for color updates.
        /// </summary>
        Text BuildCostRow(RectTransform card, int[] cost)
        {
            // Count active cost slots
            int slots = 0;
            for (int i = 0; i < cost.Length; i++) if (cost[i] > 0) slots++;
            if (slots == 0)
                return MakeLabelInCard(card, new Vector2(0f, 0.01f), new Vector2(1f, 0.19f),
                    "FREE", 11, new Color(0.4f, 1f, 0.4f), FontStyle.Bold, TextAnchor.MiddleCenter);

            // Cost area parent
            var costArea = new GameObject("costArea"); costArea.transform.SetParent(card, false);
            var caRt = costArea.AddComponent<RectTransform>();
            caRt.anchorMin = new Vector2(0f, 0.01f); caRt.anchorMax = new Vector2(1f, 0.19f);
            caRt.offsetMin = caRt.offsetMax = Vector2.zero;
            costArea.AddComponent<Image>().color = new Color(0, 0, 0, 0);

            float slotW = 1f / slots;
            int slot = 0;
            Text firstLabel = null;
            for (int i = 0; i < cost.Length; i++)
            {
                if (cost[i] <= 0) continue;
                float x0 = slot * slotW;
                float x1 = x0 + slotW;
                Color gc = GC(i);

                // Gem icon — fixed square at slot left, no stretching
                var iGo = new GameObject($"gi{i}"); iGo.transform.SetParent(caRt, false);
                var iRt = iGo.AddComponent<RectTransform>();
                iRt.anchorMin = iRt.anchorMax = new Vector2(x0 + slotW * 0.22f, 0.5f);
                iRt.pivot     = new Vector2(0.5f, 0.5f);
                iRt.anchoredPosition = Vector2.zero;
                iRt.sizeDelta = new Vector2(13f, 13f); // cost row is ~15px tall — keep icon smaller than FontH2 (22)
                var iImg = iGo.AddComponent<RawImage>();
                iImg.texture = NeonArt.Gem(i, NeonTheme.Active.GemStyle); iImg.color = Color.white; iImg.raycastTarget = false;

                // Number label (right 55% of slot)
                var nGo = new GameObject($"gcnt{i}"); nGo.transform.SetParent(caRt, false);
                var nRt = nGo.AddComponent<RectTransform>();
                nRt.anchorMin = new Vector2(x0 + slotW * 0.46f, 0f);
                nRt.anchorMax = new Vector2(x1 - 0.02f, 1f);
                nRt.offsetMin = nRt.offsetMax = Vector2.zero;
                var nTxt = nGo.AddComponent<Text>();
                nTxt.font = _font; nTxt.fontSize = UIScale.FontTiny; nTxt.fontStyle = FontStyle.Bold;
                nTxt.color = gc; nTxt.alignment = TextAnchor.MiddleLeft;
                nTxt.text = cost[i].ToString(); nTxt.supportRichText = false; nTxt.raycastTarget = false;
                if (firstLabel == null) firstLabel = nTxt;

                slot++;
            }
            return firstLabel;
        }

        void BuildWinOverlay(RectTransform anyChild)
        {
            // Walk to canvas root
            Transform root = anyChild;
            while (root.parent != null && root.GetComponent<Canvas>() == null)
                root = root.parent;

            var t = NeonTheme.Active;

            // Full-screen scrim
            _winPanel = new GameObject("WinPanel");
            _winPanel.transform.SetParent(root, false);
            var scrimRt = _winPanel.AddComponent<RectTransform>();
            scrimRt.anchorMin = Vector2.zero; scrimRt.anchorMax = Vector2.one;
            scrimRt.offsetMin = scrimRt.offsetMax = Vector2.zero;
            _winPanel.AddComponent<Image>().color = new Color(t.BgDeep.r, t.BgDeep.g, t.BgDeep.b, 0.92f);
            _winBgCg = _winPanel.AddComponent<CanvasGroup>();

            // Centered result card
            var card = new GameObject("Card");
            card.transform.SetParent(_winPanel.transform, false);
            var cardRt = card.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.30f, 0.26f); cardRt.anchorMax = new Vector2(0.70f, 0.78f);
            cardRt.offsetMin = cardRt.offsetMax = Vector2.zero;
            card.AddComponent<Image>().color = new Color(t.BgPanel.r, t.BgPanel.g, t.BgPanel.b, 1f);
            _winCardCg = card.AddComponent<CanvasGroup>();

            // Card border (themed accent)
            var brd = new GameObject("brd"); brd.transform.SetParent(card.transform, false);
            brd.transform.SetAsFirstSibling();
            var brdRt = brd.AddComponent<RectTransform>();
            brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
            brdRt.offsetMin = new Vector2(-2, -2); brdRt.offsetMax = new Vector2(2, 2);
            brd.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.60f);
            NeonUI.Stroke(card.transform, new Color(0.01f, 0.01f, 0.03f, 1f), 4f);
            NeonUI.Bevel(card.transform);

            // Title — VICTORY / DEFEAT
            var titleGo = new GameObject("title"); titleGo.transform.SetParent(card.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.56f); titleRt.anchorMax = new Vector2(1f, 0.96f);
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;
            _winTitleText = titleGo.AddComponent<Text>();
            _winTitleText.font = _font; _winTitleText.fontSize = UIScale.FontTitle;
            _winTitleText.fontStyle = FontStyle.Bold;
            _winTitleText.alignment = TextAnchor.MiddleCenter; _winTitleText.raycastTarget = false;
            NeonUI.Title(_winTitleText);

            // Thin divider
            var div = new GameObject("div"); div.transform.SetParent(card.transform, false);
            var divRt = div.AddComponent<RectTransform>();
            divRt.anchorMin = new Vector2(0.08f, 0.53f); divRt.anchorMax = new Vector2(0.92f, 0.53f);
            divRt.offsetMin = new Vector2(0, -1); divRt.offsetMax = new Vector2(0, 1);
            div.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);

            // Stats row — gems earned, level
            var statsGo = new GameObject("stats"); statsGo.transform.SetParent(card.transform, false);
            var statsRt = statsGo.AddComponent<RectTransform>();
            statsRt.anchorMin = new Vector2(0f, 0.24f); statsRt.anchorMax = new Vector2(1f, 0.53f);
            statsRt.offsetMin = statsRt.offsetMax = Vector2.zero;
            _winStatsText = statsGo.AddComponent<Text>();
            _winStatsText.font = _font; _winStatsText.fontSize = UIScale.FontH2;
            _winStatsText.color = new Color(0.65f, 0.80f, 0.92f);
            _winStatsText.alignment = TextAnchor.MiddleCenter; _winStatsText.raycastTarget = false;

            // Back to menu button
            var btnGo = new GameObject("backBtn"); btnGo.transform.SetParent(card.transform, false);
            var btnRt = btnGo.AddComponent<RectTransform>();
            btnRt.anchorMin = new Vector2(0.18f, 0.05f); btnRt.anchorMax = new Vector2(0.82f, 0.21f);
            btnRt.offsetMin = btnRt.offsetMax = Vector2.zero;
            btnGo.AddComponent<Image>().color = new Color(0.04f, 0.14f, 0.26f);

            var btnBrd = new GameObject("brd"); btnBrd.transform.SetParent(btnGo.transform, false);
            btnBrd.transform.SetAsFirstSibling();
            var btnBrdRt = btnBrd.AddComponent<RectTransform>();
            btnBrdRt.anchorMin = Vector2.zero; btnBrdRt.anchorMax = Vector2.one;
            btnBrdRt.offsetMin = new Vector2(-2, -2); btnBrdRt.offsetMax = new Vector2(2, 2);
            btnBrd.AddComponent<Image>().color = new Color(0f, 0.60f, 0.90f, 0.55f);

            var btnTGo = new GameObject("txt"); btnTGo.transform.SetParent(btnGo.transform, false);
            var btnTRt = btnTGo.AddComponent<RectTransform>();
            btnTRt.anchorMin = Vector2.zero; btnTRt.anchorMax = Vector2.one;
            btnTRt.offsetMin = btnTRt.offsetMax = Vector2.zero;
            var btnTxt = btnTGo.AddComponent<Text>();
            btnTxt.font = _font; btnTxt.fontSize = UIScale.FontH2; btnTxt.fontStyle = FontStyle.Bold;
            btnTxt.color = new Color(0.3f, 0.9f, 1f); btnTxt.alignment = TextAnchor.MiddleCenter;
            btnTxt.text = "→  BACK TO MENU"; btnTxt.supportRichText = false; btnTxt.raycastTarget = false;

            var btn = btnGo.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => FindAnyObjectByType<BattleScene>()?.Restart());
            btnGo.AddComponent<ButtonFeel>();

            _winPanel.SetActive(false);
        }

        // ── public API ───────────────────────────────────────────────────────────

        public int SelectedCard => _selectedCard;

        public void SetSelectedCard(int card)
        {
            _selectedCard = card;
            for (int i = 0; i < _cards.Count; i++)
            {
                _cards[i].Selected.color = i == card
                    ? new Color(0.3f, 0.95f, 1f, 0.20f)
                    : new Color(0, 0, 0, 0);
                if (i == card && _cards[i].Root != null)
                    Tween.Punch(_cards[i].Root, 0.10f, 0.22f);
            }
        }

        public void FlashAffordError(int cardIdx)
        {
            if (cardIdx < 0 || cardIdx >= _session.DeployOptions.Count) return;
            var cost = _session.DeployOptions[cardIdx].cost;
            for (int i = 0; i < cost.Length && i < BoardModel.GemKindCount; i++)
            {
                if (_session.Resources[i] < cost[i])
                {
                    _sidebarErrFlash[i] = 0.55f;
                    if (_sidebarRowRts[i] != null) Tween.ShakeH(_sidebarRowRts[i], 4f, 0.25f);
                }
            }
            // Also shake the card itself
            if (cardIdx < _cards.Count && _cards[cardIdx].Root != null)
                Tween.ShakeH(_cards[cardIdx].Root as RectTransform, 5f, 0.25f);
        }

        // Shakes only the deploy card — for lane-full / turret-blocked failures where
        // gems aren't the issue, so the wallet rows should NOT flash.
        public void FlashDeployError(int cardIdx)
        {
            if (cardIdx < 0 || cardIdx >= _cards.Count) return;
            if (_cards[cardIdx].Root != null)
                Tween.ShakeH(_cards[cardIdx].Root as RectTransform, 5f, 0.25f);
        }

        // ── refresh ─────────────────────────────────────────────────────────────

        public void Refresh()
        {
            if (_session == null) return;

            RefreshSidebar();

            // Top bar
            var sim = _session.Combat;
            _actLabel.text = sim.Act == 3 ? "ACT 3  OVERRUN"
                           : sim.Act == 2 ? "ACT 2  ESCALATION" : "ACT 1  SKIRMISH";

            int live = _session.LiveCombo;
            _comboLabel.text  = live > 0 ? $"COMBO ×{live}" : "COMBO ×0";
            _comboLabel.color = live >= 7 ? new Color(1f, 0.25f, 0.15f)
                              : live >= 4 ? new Color(1f, 0.62f, 0.10f)
                              : live >= 2 ? new Color(1f, 0.92f, 0.20f)
                              : NeonTheme.Active.Accent;
            if (live > _lastCombo && live > 0 && _comboLabel.rectTransform != null)
                Tween.Punch(_comboLabel.rectTransform, 0.14f, 0.26f);
            _lastCombo = live;

            // Cards
            float pulse = Mathf.Sin(Time.time * PULSE_HZ) * 0.5f + 0.5f;
            for (int i = 0; i < _cards.Count; i++)
            {
                var opt       = _session.DeployOptions[i];
                bool affordable = _session.CanAfford(opt.cost);
                bool onCooldown = _session.CardCooldown(i) > 0f;
                bool ready      = affordable && !onCooldown;
                float cd = onCooldown ? Mathf.Clamp01(_session.CardCooldown(i) / opt.cooldown) : 0f;

                var th = NeonTheme.Active;
                _cards[i].Icon.color = NeonCosmetics.GetTroopTint();
                // Flat dim while unusable; the ring carries the timing.
                if (_cards[i].Cooldown.enabled != onCooldown)
                    _cards[i].Cooldown.enabled = onCooldown;

                // Radial ring sweeps from full to empty as the card recharges.
                if (_cards[i].CdRing != null)
                {
                    _cards[i].CdRing.fillAmount = cd;
                    var rc = NeonTheme.Active.Accent;
                    _cards[i].CdRing.color = new Color(rc.r, rc.g, rc.b, 0.95f);
                    if (_cards[i].CdRing.gameObject.activeSelf != (cd > 0f))
                        _cards[i].CdRing.gameObject.SetActive(cd > 0f);
                }

                // The bottom bar was the second progress read. Two meters for one value is
                // noise, so it stays hidden now that the ring exists.
                if (_cards[i].CdBar != null && _cards[i].CdBar.gameObject.activeSelf)
                    _cards[i].CdBar.gameObject.SetActive(false);
                if (_cards[i].CdNum != null)
                {
                    if (onCooldown)
                    {
                        _cards[i].CdNum.text = Mathf.Ceil(_session.CardCooldown(i)).ToString();
                        if (!_cards[i].CdNum.gameObject.activeSelf) _cards[i].CdNum.gameObject.SetActive(true);
                    }
                    else if (_cards[i].CdNum.gameObject.activeSelf)
                        _cards[i].CdNum.gameObject.SetActive(false);
                }
                _cards[i].Bg.color = ready
                    ? new Color(th.Accent.r * 0.20f, th.Accent.g * 0.20f, th.Accent.b * 0.20f) + th.BgCard
                    : th.BgDeep;
                _cards[i].NameLbl.color = ready ? th.TextBright : th.TextDim;
                _cards[i].StatLbl.color = ready ? th.TextMid    : th.TextDim;
                if (_cards[i].CostLbl != null)
                    _cards[i].CostLbl.color = affordable
                        ? CostColor(opt.cost)
                        : new Color(0.5f, 0.25f, 0.25f);

                // Pulsing accent border when ready to deploy
                if (_cards[i].ReadyBorder != null)
                    _cards[i].ReadyBorder.color = ready
                        ? new Color(th.Accent.r, th.Accent.g, th.Accent.b, 0.28f + pulse * 0.55f)
                        : new Color(0, 0, 0, 0);

                // Chime + pop-in when card comes off cooldown
                if (i < _wasOnCooldown.Length)
                {
                    if (_wasOnCooldown[i] && !onCooldown)
                    {
                        AudioManager.Play(AudioManager.Sfx.Click, 0.55f, 1.25f);
                        if (_cards[i].Root != null)
                            Tween.PopIn(_cards[i].Root, 0.18f);
                    }
                    _wasOnCooldown[i] = onCooldown;
                }
            }

            // Win/defeat overlay — staged cinematic reveal
            if (sim.Finished && _winPanel != null && !_winPanel.activeSelf)
                StartCoroutine(ResultSequence(sim.Winner == Team.Player));
        }

        // Banner slam → divider sweep → reward ticker → button pop, with
        // confetti on victory / red vignette on defeat. Music ducks under the stinger.
        IEnumerator ResultSequence(bool won)
        {
            // Core-destruction spectacle plays out before the card appears
            var bf = FindAnyObjectByType<BattlefieldView>();
            if (bf != null) yield return bf.CoreDestructionFinale(won);

            _winPanel.SetActive(true);
            var card  = _winCardCg.transform;
            var title = _winTitleText.rectTransform;
            var div   = card.Find("div");
            var btn   = card.Find("backBtn");

            int    energy  = PlayerProgress.Currency[(int)GemKind.Energy];
            string gemName = ThemeLocale.GemName(0);
            _winTitleText.text  = won ? "VICTORY" : "DEFEAT";
            _winTitleText.color = won ? new Color(0.25f, 1f, 0.45f) : new Color(1f, 0.28f, 0.28f);

            // Hide everything, then reveal in stages
            _winBgCg.alpha   = 0f;
            _winCardCg.alpha = 0f;
            card.localScale  = Vector3.one * 0.90f;
            _winTitleText.enabled = false;
            _winStatsText.text    = "";
            if (div != null) div.localScale = new Vector3(0f, 1f, 1f);
            if (btn != null) btn.localScale = Vector3.zero;

            AudioManager.DuckMusic();
            AudioManager.Play(won ? AudioManager.Sfx.Victory : AudioManager.Sfx.Defeat);

            // 1 — scrim, then card shell
            Tween.Fade(_winBgCg, 0f, 1f, 0.25f);
            yield return new WaitForSeconds(0.10f);
            Tween.Fade(_winCardCg, 0f, 1f, 0.18f);
            Tween.Scale(card, 0.90f, 1f, 0.25f, Tween.Ease.EaseOutBack);
            yield return new WaitForSeconds(0.20f);

            // 2 — banner slam: falls from 2.6× to 1× and lands with a card punch
            _winTitleText.enabled = true;
            const float SLAM = 0.16f;
            for (float t = 0f; t < SLAM; t += Time.deltaTime)
            {
                float e = t / SLAM;
                float s = Mathf.Lerp(2.6f, 1f, e * e);
                title.localScale = new Vector3(s, s, 1f);
                yield return null;
            }
            title.localScale = Vector3.one;
            Tween.Punch(card, 0.05f, 0.20f);
            if (won) StartCoroutine(ConfettiBurst());
            else     StartCoroutine(DefeatVignette());
            yield return new WaitForSeconds(0.08f);

            // 3 — divider sweep
            if (div != null)
            {
                const float DIV = 0.18f;
                for (float t = 0f; t < DIV; t += Time.deltaTime)
                {
                    div.localScale = new Vector3(1f - (1f - t / DIV) * (1f - t / DIV), 1f, 1f);
                    yield return null;
                }
                div.localScale = Vector3.one;
            }

            // 4 — reward ticker: gem count rolls up with rising ticks
            const float TICK = 0.65f;
            float lastBeep = -1f;
            for (float t = 0f; t < TICK; t += Time.deltaTime)
            {
                float p = t / TICK;
                int n = Mathf.RoundToInt(Mathf.Lerp(0, energy, 1f - (1f - p) * (1f - p)));
                _winStatsText.text = won
                    ? $"{gemName} EARNED:  {n}"
                    : $"{gemName} TOTAL:  {n}";
                if (t - lastBeep > 0.07f && energy > 0)
                {
                    AudioManager.Play(AudioManager.Sfx.Click, 0.22f, 1f + p * 0.5f);
                    lastBeep = t;
                }
                yield return null;
            }
            _winStatsText.text = won
                ? $"{gemName} EARNED:  {energy}\n\nLEVEL {GameSettings.SelectedLevel} COMPLETE"
                : $"{gemName} TOTAL:  {energy}\n\nBETTER LUCK NEXT TIME";

            // 5 — button pop, restore music
            if (btn != null)
            {
                btn.localScale = Vector3.one;
                Tween.PopIn(btn, 0.22f);
            }
            yield return new WaitForSeconds(1.1f);
            AudioManager.RestoreMusic();
        }

        // Celebration confetti: gem-colored slips rain over the result card
        IEnumerator ConfettiBurst()
        {
            const int COUNT = 26;
            var rts  = new RectTransform[COUNT];
            var imgs = new Image[COUNT];
            var vel  = new Vector2[COUNT];
            var rot  = new float[COUNT];
            for (int i = 0; i < COUNT; i++)
            {
                var go = new GameObject("confetti");
                go.transform.SetParent(_winPanel.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.78f);
                rt.anchoredPosition = new Vector2(Random.Range(-330f, 330f), Random.Range(0f, 60f));
                rt.sizeDelta = new Vector2(7f, 13f);
                rt.localRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
                var img = go.AddComponent<Image>();
                img.color = GC(Random.Range(0, BoardModel.GemKindCount));
                img.raycastTarget = false;
                vel[i]  = new Vector2(Random.Range(-40f, 40f), Random.Range(-140f, -60f));
                rot[i]  = Random.Range(-260f, 260f);
                rts[i]  = rt; imgs[i] = img;
            }
            const float DUR = 1.5f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                float alpha = 1f - Mathf.Clamp01((t - 0.9f) / 0.6f);
                for (int i = 0; i < COUNT; i++)
                {
                    if (rts[i] == null) continue;
                    vel[i].y -= 260f * Time.deltaTime; // gravity
                    rts[i].anchoredPosition += vel[i] * Time.deltaTime;
                    rts[i].localRotation = Quaternion.Euler(0, 0, rts[i].localEulerAngles.z + rot[i] * Time.deltaTime);
                    var c = imgs[i].color; imgs[i].color = new Color(c.r, c.g, c.b, alpha);
                }
                yield return null;
            }
            for (int i = 0; i < COUNT; i++)
                if (rts[i] != null) Destroy(rts[i].gameObject);
        }

        // Defeat: two dark-red vignette pulses over the whole screen
        IEnumerator DefeatVignette()
        {
            var go = new GameObject("vignette");
            go.transform.SetParent(_winPanel.transform, false);
            go.transform.SetAsFirstSibling(); // behind the card, over the scrim
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            const float DUR = 0.9f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                float a = Mathf.Abs(Mathf.Sin(t / DUR * Mathf.PI * 2f)) * 0.28f * (1f - t / DUR);
                img.color = new Color(0.75f, 0.05f, 0.08f, a);
                yield return null;
            }
            Destroy(go);
        }

        void RefreshSidebar()
        {
            for (int i = 0; i < BoardModel.GemKindCount; i++)
            {
                int v = _session.Resources[i];
                _sidebarCounts[i].text = v.ToString();
                Color gc = GC(i);

                // Animate fill bar
                float targetFill = Mathf.Clamp01((float)v / FILL_MAX);
                var fillRt = _sidebarFills[i].GetComponent<RectTransform>();
                float curFill = fillRt.anchorMax.x;
                float nextFill = Mathf.MoveTowards(curFill, targetFill, Time.deltaTime * 3f);
                fillRt.anchorMax = new Vector2(nextFill, 1f);

                Color rowBase = new Color(gc.r * 0.06f, gc.g * 0.06f, gc.b * 0.06f);

                if (v > _lastRes[i])
                {
                    _sidebarFlash[i] = 0.4f;
                    SpawnFloat(i, v - _lastRes[i]);
                    SpawnResourceStream(i, v - _lastRes[i]);
                }
                _lastRes[i] = v;

                if (_sidebarErrFlash[i] > 0f)
                {
                    _sidebarErrFlash[i] -= Time.deltaTime;
                    float f = _sidebarErrFlash[i] / 0.55f;
                    _sidebarRows[i].color = Color.Lerp(rowBase, new Color(0.25f, 0.02f, 0.02f), f);
                    _sidebarCounts[i].color = Color.Lerp(gc, Color.white, f);
                }
                else if (_sidebarFlash[i] > 0f)
                {
                    _sidebarFlash[i] -= Time.deltaTime;
                    float f = _sidebarFlash[i] / 0.4f;
                    _sidebarRows[i].color = Color.Lerp(rowBase,
                        new Color(gc.r * 0.28f, gc.g * 0.28f, gc.b * 0.28f), f);
                    _sidebarCounts[i].color = GC(i);
                }
                else
                {
                    _sidebarRows[i].color   = rowBase;
                    _sidebarCounts[i].color = gc;
                }
            }
        }

        // Spawns a "+N" label on the sidebar row that floats up and fades out.
        void SpawnFloat(int gemIdx, int delta)
        {
            if (delta <= 0 || gemIdx >= _sidebarRowRts.Length || _sidebarRowRts[gemIdx] == null) return;
            var rowRt = _sidebarRowRts[gemIdx];

            var go = new GameObject("float");
            go.transform.SetParent(rowRt, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.75f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(52f, 18f);

            var txt = go.AddComponent<Text>();
            txt.font = _font; txt.fontSize = UIScale.FontSmall; txt.fontStyle = FontStyle.Bold;
            txt.color = GC(gemIdx); txt.alignment = TextAnchor.MiddleCenter;
            txt.text = $"+{delta}"; txt.supportRichText = false; txt.raycastTarget = false;

            Tween.MoveY(rt, 0f, 46f, 0.78f, Tween.Ease.EaseOut);
            Tween.FadeImg(txt, 1f, 0f, 0.78f, done: () => { if (go != null) Destroy(go); });
        }

        // ── resource supply-line (doc 09 §4.2 — "the single highest-value juice item") ─────
        // On every resource gain, fling a few colored motes from the board region up into the
        // gem's meter cell, so the player physically watches income flow board → wallet.
        void SpawnResourceStream(int gemIdx, int delta)
        {
            if (delta <= 0 || gemIdx >= _sidebarRowRts.Length || _sidebarRowRts[gemIdx] == null) return;
            var canvas = _sidebarRowRts[gemIdx].GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var canvasRt = canvas.transform as RectTransform;
            if (canvasRt == null) return;

            // Target = the meter cell centre in canvas-local space. Source = board region (lower centre).
            Vector2 target = canvasRt.InverseTransformPoint(_sidebarRowRts[gemIdx].position);
            Vector2 source = new Vector2(0f, -canvasRt.rect.height * 0.28f);

            int motes = Mathf.Clamp(delta, 3, 7);
            Color c = GC(gemIdx);
            for (int m = 0; m < motes; m++)
            {
                var go = new GameObject("mote"); go.transform.SetParent(canvasRt, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(14f, 14f);
                var img = go.AddComponent<Image>();
                img.color = c; img.raycastTarget = false;

                Vector2 start = source + new Vector2(UnityEngine.Random.Range(-90f, 90f),
                                                     UnityEngine.Random.Range(-40f, 40f));
                rt.anchoredPosition = start;

                float dur   = 0.46f + UnityEngine.Random.Range(0f, 0.14f);
                float delay = m * 0.035f;
                var moteRt = rt; var moteGo = go; var moteImg = img;

                Tween.Delay(delay, () =>
                {
                    if (moteGo == null) return;
                    Tween.Float(u =>
                    {
                        if (moteRt == null) return;
                        moteRt.anchoredPosition = Vector2.LerpUnclamped(start, target, u);
                        moteRt.localScale = Vector3.one * Mathf.Lerp(1f, 0.4f, u);
                    }, 0f, 1f, dur, Tween.Ease.EaseIn, done: () =>
                    {
                        if (gemIdx < _sidebarFlash.Length) _sidebarFlash[gemIdx] = 0.3f; // pulse on arrival
                        if (moteGo != null) Destroy(moteGo);
                    });
                    Tween.FadeImg(moteImg, 0.95f, 0.3f, dur, Tween.Ease.EaseIn);
                });
            }
        }

        // Called by BattleScene for drag-and-drop targeting
        void OnDeployDrag(int cardIdx, Vector2 screenPos)
        {
            // Forward drag position to BattlefieldView for lane highlighting
            var bf = FindAnyObjectByType<BattlefieldView>();
            bf?.OnDragOver(screenPos);
        }

        // ── helpers ─────────────────────────────────────────────────────────────

        // Card frame decoration — static visuals applied at build time per cosmetic.
        // ReadyBorder handles game-state pulsing separately; this is always-on decoration.
        static void ApplyCardFrame(RectTransform card, Color accent, NeonCosmetics.CardFrame frame)
        {
            Color dim  = new Color(accent.r, accent.g, accent.b, 0.30f);
            Color mid  = new Color(accent.r, accent.g, accent.b, 0.50f);

            switch (frame)
            {
                case NeonCosmetics.CardFrame.Standard:
                    // Thin always-on 1px border
                    AddFrameBorder(card, dim, -1, 1);
                    break;

                case NeonCosmetics.CardFrame.Circuit:
                {
                    // 2px border + 4 corner squares
                    AddFrameBorder(card, dim, -2, 2);
                    float[] xs = { 0f, 1f, 0f, 1f };
                    float[] ys = { 0f, 0f, 1f, 1f };
                    for (int c = 0; c < 4; c++)
                    {
                        var sq = new GameObject("corner"); sq.transform.SetParent(card, false);
                        var sqRt = sq.AddComponent<RectTransform>();
                        sqRt.anchorMin = sqRt.anchorMax = new Vector2(xs[c], ys[c]);
                        sqRt.pivot     = new Vector2(xs[c], ys[c]);
                        sqRt.sizeDelta = new Vector2(5f, 5f);
                        sq.AddComponent<Image>().color = mid;
                    }
                    break;
                }

                case NeonCosmetics.CardFrame.Hexagon:
                    // Outer 1px + inner inset border
                    AddFrameBorder(card, dim, -1, 1);
                    AddFrameBorder(card, new Color(accent.r, accent.g, accent.b, 0.18f), 4, -4);
                    break;

                case NeonCosmetics.CardFrame.Razor:
                {
                    // Top-edge strip (full width, 2px)
                    var top = new GameObject("razorTop"); top.transform.SetParent(card, false);
                    var topRt = top.AddComponent<RectTransform>();
                    topRt.anchorMin = new Vector2(0f, 1f); topRt.anchorMax = new Vector2(1f, 1f);
                    topRt.offsetMin = new Vector2(0, -2); topRt.offsetMax = Vector2.zero;
                    top.AddComponent<Image>().color = mid;

                    // Right-edge strip (full height, 2px)
                    var right = new GameObject("razorRight"); right.transform.SetParent(card, false);
                    var rightRt = right.AddComponent<RectTransform>();
                    rightRt.anchorMin = new Vector2(1f, 0f); rightRt.anchorMax = new Vector2(1f, 1f);
                    rightRt.offsetMin = new Vector2(-2, 0); rightRt.offsetMax = Vector2.zero;
                    right.AddComponent<Image>().color = mid;
                    break;
                }
            }
        }

        static Image AddFrameBorder(RectTransform parent, Color col, float inset, float outset)
        {
            var go = new GameObject("frame"); go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(inset, inset); rt.offsetMax = new Vector2(outset, outset);
            var img = go.AddComponent<Image>(); img.color = col; img.raycastTarget = false;
            return img;
        }

        static Color CostColor(int[] cost)
        {
            for (int i = 0; i < cost.Length; i++)
                if (cost[i] > 0) return ThemeLocale.GemColor(i);
            return Color.white;
        }

        Text MakeLabelInCard(RectTransform parent, Vector2 aMin, Vector2 aMax,
            string text, int size, Color col, FontStyle style, TextAnchor align)
        {
            var go = new GameObject("lbl"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.color = col; t.alignment = align; t.text = text;
            t.supportRichText = false; t.raycastTarget = false;
            return t;
        }

        RectTransform MakePanel(RectTransform parent, string name, Vector2 amin, Vector2 amax)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = amin; rt.anchorMax = amax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            return rt;
        }

        static Image MakeOverlay(RectTransform parent, string name, Color col)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = col; img.raycastTarget = false;
            return img;
        }

        Text AddText(RectTransform parent, string txt, int size, Color col, TextAnchor align)
        {
            var go = new GameObject("txt"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = col;
            t.alignment = align; t.text = txt;
            t.supportRichText = false; t.raycastTarget = false;
            return t;
        }
    }
}
