using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NW.Combat.Domain;   // CombatSim.DeploySpawnX -- the demo derives its spawn points from the sim
using UnityEngine.EventSystems;
using NW.Board.Domain;
using NW.Net;

namespace NW.App
{
    /// <summary>
    /// Level-select screen shown before each battle.
    /// Shows 20 level buttons (5×4 grid), earned currency, troop/gem info, camera shake toggle.
    /// Fires OnLevelSelected(level) when player confirms.
    /// </summary>
    public sealed class LevelSelectScreen : MonoBehaviour
    {
        public Action<int> OnLevelSelected;

        Font          _font;
        // Portrait layout adjustments (landscape = 0/false → desktop untouched). Set in Init.
        //  _menuShiftY : lifts the whole center-anchored cluster toward the top.
        //  _portrait   : true when the viewport is taller than wide.
        //  _detailDy   : extra downward shift applied ONLY to the detail panel, so it stacks
        //                BELOW the (centered) level grid instead of beside it.
        float         _menuShiftY;
        bool          _portrait;
        float         _detailDy;
        GameObject    _speedRoot;      // container for the speed picker — hidden while VS mode is active
        GameObject    _settingsPanel;  // gear-toggled settings overlay (menu pass)
        GameObject    _settingsCard;   // the centred card inside that overlay
        RawImage      _bgPhoto;        // level-select background photo, dimmed by the BG DIM slider

        /// <summary>Called by BattleScene after Attach() so the BG DIM slider can dim this screen's bg.</summary>
        public void SetBgPhoto(RawImage p)
        {
            _bgPhoto = p;
            // Default to most-dim on every open so text stays readable over the background;
            // the BG DIM slider can brighten it during the session.
            if (p != null) p.color = new Color(1f, 1f, 1f, 0.16f);
        }
        int           _hoveredLevel = 1;
        Text          _detailName;
        Text          _detailGems;    // section header "GEM TYPES"
        Text          _detailTroops;  // section header "TROOPS"
        Text          _detailDiff;
        Text          _currencyLabel;
        Text          _shakeVal;
        Image         _shakeBtnBg;
        Text          _musicVal;
        Image         _musicBtnBg;
        GameObject    _unlockBtnGo;
        Text          _unlockBtnText;
        Text          _playBtnText;
        Text          _ranksBtnText;
        GameObject    _infoModal;

        // RANKS modal (progress + ghost ladder)
        GameObject    _ranksModal;
        GameObject    _ranksListGo;     // content column, cleared and rebuilt per tab
        Text          _ranksHeaderTxt;
        Image         _ranksTierFill;
        int           _ranksTab;        // 0 = LADDER, 1 = BREACH, 2 = HISTORY
        readonly Image[] _ranksTabBg  = new Image[3];
        readonly Text[]  _ranksTabTxt = new Text[3];
        bool          _refreshingRanks;               // re-entrancy guard for async standings repaint
        List<LeaderboardEntry> _ladderRows;           // cached standings for _hoveredLevel (null = fetch)
        int           _ladderRowsLevel = -1;
        Image[]       _speedBgs  = new Image[3];
        Text[]        _speedTxts = new Text[3];

        // Visual detail panel elements
        readonly RawImage[]   _gemIcons    = new RawImage[5];
        readonly GameObject[] _troopPanels = new GameObject[10];
        readonly RawImage[]   _troopIcons  = new RawImage[10];
        readonly Text[]       _troopNames  = new Text[10];
        Image _diffBarFill;
        Text  _rewardLbl;
        // (theme picker is now ThemePickerDropdown component)

        // Board opacity picker chips
        Image[] _boardOpacityBgs  = new Image[4];
        Text[]  _boardOpacityTxts = new Text[4];

        // Troop detail modal (click on troop card to open)
        GameObject  _troopModal;
        Coroutine   _demoCoroutine;
        Transform   _demoArena;
        string      _demoLeftId;
        int         _demoRightIdx;
        GameObject  _boardPreviewPopup;
        Coroutine   _previewHideCoroutine;
        static readonly string[] _troopIdList = {
            "drone","trooper","sniper","mech","shield-bot",
            "interceptor","hacker","titan","turret"
        };

        // VS / competitive panel
        GameObject  _vsPanel;
        Image       _vsBtnBg;
        Text        _vsBtnTxt;
        Text        _vsStatusLbl;
        Text        _mmrLbl;
        InputField  _ipField;
        Text        _vsModeLbl;
        readonly List<(Image bg, Text lbl, int level)> _levelBtns = new();

        // Everything on this screen was reading translucent and muddy. Three causes:
        // BgCard sits almost on top of BgDeep in several themes (Medieval BgCard 0.173/0.055/0.055
        // against BgDeep 0.055/0.008/0.016), the border colour was a hardcoded blue-grey at alpha
        // 0.7 that ignored the theme entirely, and the root fill was alpha 0.98 so the background
        // art bled through everything. Buttons now lift off the ground with an accent-tinted face
        // and a fully opaque border.
        static Color ColLocked    => Lift(NeonTheme.Active.BgDeep, 0.04f);
        static Color ColUnlocked  => Lift(NeonTheme.Active.BgCard, 0.26f);   // 1.7-2.4 vs ground

        /// <summary>Raise a surface toward the theme accent so it reads as a raised face.</summary>
        static Color Lift(Color b, float k)
        {
            var a = NeonTheme.Active.Accent;
            return new Color(b.r + a.r * k, b.g + a.g * k, b.b + a.b * k, 1f);
        }
        static Color ColSelected  => NeonTheme.Active.Accent * 0.62f + NeonTheme.Active.BgPanel;
        static Color ColHover     => NeonTheme.Active.BgCard + new Color(0.02f, 0.03f, 0.04f);
        static Color ColBorderOn  => NeonTheme.Active.Accent;
        static Color ColBorderOff => new Color(
            NeonTheme.Active.Accent.r * 0.30f + 0.10f,
            NeonTheme.Active.Accent.g * 0.30f + 0.11f,
            NeonTheme.Active.Accent.b * 0.30f + 0.13f, 1f);   // opaque, follows the theme

        // Gem colors are theme-aware — routed through ThemeLocale at call-site
        static Color GemCol(int i) => ThemeLocale.GemColor(i);

        public void Init(Font font)
        {
            _font = font;
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            gameObject.AddComponent<Image>().color = new Color(
                NeonTheme.Active.BgDeep.r, NeonTheme.Active.BgDeep.g,
                NeonTheme.Active.BgDeep.b, 1f);   // was 0.98 -- the bleed-through read as haze
            // Portrait: lift the center-anchored cluster so its top (title, y≈316) sits ~150 units
            // below the screen top. Canvas is match-width, so ref-height = 1080 * (h/w).
            _portrait = Screen.width > 0 && Screen.height > Screen.width;
            if (_portrait)
            {
                float refH = 1080f * Screen.height / Screen.width;
                // 150 put the title 150 units below the screen top, which is directly on top of
                // the currency row -- the two were overlapping in the top-right. 250 gives the
                // currency its own strip between the buttons and the title.
                _menuShiftY = refH * 0.5f - 316f - 250f;
                _detailDy   = -440f; // panel sits just under the grid; verified to fit unscrolled
            }

            BuildScanlines();
            BuildLayout();
            RefreshCurrency();
            SelectLevel(GameSettings.SelectedLevel);
        }

        // ────────────────────────────────────────────── layout ──────────────────

        void BuildScanlines()
        {
            var bg = NeonTheme.Active.BgDeep;
            float lum = bg.r * 0.299f + bg.g * 0.587f + bg.b * 0.114f;
            // Light themes (Dawn etc.): scanlines are nearly invisible to avoid ruining readability
            Color scanCol = lum > 0.35f
                ? new Color(0f, 0f, 0f, 0.04f)
                : new Color(0.04f, 0.09f, 0.2f, 0.18f);

            for (int i = 0; i < 54; i++)
            {
                var go = new GameObject("sl"); go.transform.SetParent(transform, false);
                var sr = go.AddComponent<RectTransform>();
                sr.anchorMin = new Vector2(0f, i / 54f); sr.anchorMax = new Vector2(1f, i / 54f);
                sr.pivot = new Vector2(0.5f, 0f); sr.sizeDelta = new Vector2(0f, 1f);
                go.AddComponent<Image>().color = scanCol;
            }
        }

        void BuildThemeBorder(NeonTheme.Theme theme)
        {
            Color ac  = theme.Accent;
            Color acf = new Color(ac.r, ac.g, ac.b, 0.18f);

            // Full-perimeter thin glow border
            void MkEdge(Vector2 aMin, Vector2 aMax, float offX0, float offY0, float offX1, float offY1, float alpha)
            {
                var g = new GameObject("border"); g.transform.SetParent(transform, false);
                var r = g.AddComponent<RectTransform>();
                r.anchorMin = aMin; r.anchorMax = aMax;
                r.offsetMin = new Vector2(offX0, offY0); r.offsetMax = new Vector2(offX1, offY1);
                g.AddComponent<Image>().color = new Color(ac.r, ac.g, ac.b, alpha);
            }
            MkEdge(new Vector2(0f, 0f), new Vector2(1f, 0f), 0f, 0f,  0f, 2f, 0.55f);   // bottom
            MkEdge(new Vector2(0f, 1f), new Vector2(1f, 1f), 0f, -2f, 0f, 0f, 0.55f);   // top
            MkEdge(new Vector2(0f, 0f), new Vector2(0f, 1f), 0f, 0f,  2f, 0f, 0.55f);   // left
            MkEdge(new Vector2(1f, 0f), new Vector2(1f, 1f), -2f, 0f, 0f, 0f, 0.55f);   // right

            // Corner L-brackets: 30px arms, 3px thick — each entry is (anchorMin, anchorMax, offsetMin, offsetMax)
            (Vector2 an, Vector2 ax, float ox0, float oy0, float ox1, float oy1)[] corners =
            {
                (new Vector2(0f,1f), new Vector2(0f,1f),  0f,  -3f, 30f,  0f),  // TL horiz
                (new Vector2(0f,1f), new Vector2(0f,1f),  0f, -30f,  3f,  0f),  // TL vert
                (new Vector2(1f,1f), new Vector2(1f,1f), -30f,  -3f, 0f,  0f),  // TR horiz
                (new Vector2(1f,1f), new Vector2(1f,1f),  -3f, -30f, 0f,  0f),  // TR vert
                (new Vector2(0f,0f), new Vector2(0f,0f),  0f,   0f, 30f,  3f),  // BL horiz
                (new Vector2(0f,0f), new Vector2(0f,0f),  0f,   0f,  3f, 30f),  // BL vert
                (new Vector2(1f,0f), new Vector2(1f,0f), -30f,   0f, 0f,  3f),  // BR horiz
                (new Vector2(1f,0f), new Vector2(1f,0f),  -3f,   0f, 0f, 30f),  // BR vert
            };
            foreach (var (an, ax, ox0, oy0, ox1, oy1) in corners)
                MkEdge(an, ax, ox0, oy0, ox1, oy1, 0.80f);

            // Inner accent glow strip at top
            var glowGo = new GameObject("topglow"); glowGo.transform.SetParent(transform, false);
            var glowRt = glowGo.AddComponent<RectTransform>();
            glowRt.anchorMin = new Vector2(0f, 1f); glowRt.anchorMax = new Vector2(1f, 1f);
            glowRt.offsetMin = new Vector2(40f, -18f); glowRt.offsetMax = new Vector2(-40f, -2f);
            glowGo.AddComponent<Image>().color = acf;
        }

        void BuildLayout()
        {
            var theme = NeonTheme.Active;

            // ── Decorative themed border corners ─────────────────────────────
            BuildThemeBorder(theme);

            // ── top bar: title centered, controls anchored to edges ──────────
            // The font raise took the title from 62 to 78 units and the subtitle from 23
            // to 40, so their combined half-heights (59) overran the 46-unit baseline gap
            // by 13. 78 units apart clears both with room to breathe.
            MkLabel("NEON  WARFARE", 0f, 326f, UIScale.FontTitle, Color.white);
            MkLabel("SELECT MISSION", 0f, 248f, UIScale.FontBody,
                    new Color(theme.Accent.r * 0.7f + 0.3f, theme.Accent.g * 0.7f + 0.3f,
                              theme.Accent.b * 0.7f + 0.3f));
            MkHRule(254f);

            // Currency display (top-right, anchored to right edge — no y overlap with title)
            var currGo = new GameObject("currency"); currGo.transform.SetParent(transform, false);
            var currRt = currGo.AddComponent<RectTransform>();
            currRt.anchorMin = new Vector2(1f, 1f); currRt.anchorMax = new Vector2(1f, 1f);
            currRt.pivot = new Vector2(1f, 1f);
            currRt.anchoredPosition = new Vector2(-18f, _portrait ? -152f : -44f);
            // Five currencies at 30pt need roughly 1155 units; the row was 1030 wide, so the
            // last one ran off the right edge. Best-fit lets it shrink to whatever the widest
            // theme's names actually need instead of guessing a size that works for one of them.
            currRt.sizeDelta = new Vector2(_portrait ? 1044f : 520f, _portrait ? 44f : 22f);
            _currencyLabel = currGo.AddComponent<Text>();
            // FontSmall put four currencies in ~20 units of height at the very top of a phone,
            // where the notch and the two buttons already compete for the same strip.
            _currencyLabel.font = _font; _currencyLabel.fontSize = _portrait ? 30 : UIScale.FontSmall;
            _currencyLabel.resizeTextForBestFit = true;
            _currencyLabel.resizeTextMinSize = 18;
            _currencyLabel.resizeTextMaxSize = _portrait ? 30 : UIScale.FontSmall;
            _currencyLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            _currencyLabel.color = new Color(1f, 0.88f, 0.30f);
            _currencyLabel.alignment = TextAnchor.MiddleRight;
            _currencyLabel.supportRichText = false;

            // ── Settings behind a gear button (the old top-left column was cramped in portrait) ──
            var settingsGo = new GameObject("SettingsPanel"); settingsGo.transform.SetParent(transform, false);
            _settingsPanel = settingsGo;
            var spRt = settingsGo.AddComponent<RectTransform>();
            spRt.anchorMin = Vector2.zero; spRt.anchorMax = Vector2.one; spRt.offsetMin = spRt.offsetMax = Vector2.zero;
            var spBg = settingsGo.AddComponent<Image>();
            spBg.color = new Color(theme.BgDeep.r * 0.4f, theme.BgDeep.g * 0.4f,
                                   theme.BgDeep.b * 0.4f, 0.92f);
            var spBtn = settingsGo.AddComponent<Button>(); spBtn.transition = Selectable.Transition.None;
            spBtn.onClick.AddListener(() => _settingsPanel.SetActive(false)); // tap backdrop to close
            // The rows used to sit loose in the top-left at x=18, y=-18..-202 -- exactly where
            // the gear button lives (x 18..218, y -12..-128). The gear is a LATER sibling, so it
            // drew straight over the first three rows. They now live in a centred card, matching
            // the theme picker, which also gets them off the currency strip.
            var spCard = new GameObject("card"); spCard.transform.SetParent(settingsGo.transform, false);
            var cardRt = spCard.AddComponent<RectTransform>();
            cardRt.anchorMin = cardRt.anchorMax = new Vector2(0.5f, 0.5f);
            cardRt.pivot = new Vector2(0.5f, 0.5f);
            cardRt.anchoredPosition = Vector2.zero;
            cardRt.sizeDelta = new Vector2(_portrait ? 760f : 520f, _portrait ? 700f : 470f);

            var cardBrdGo = new GameObject("brd"); cardBrdGo.transform.SetParent(settingsGo.transform, false);
            var cbRt = cardBrdGo.AddComponent<RectTransform>();
            cbRt.anchorMin = cbRt.anchorMax = new Vector2(0.5f, 0.5f);
            cbRt.pivot = new Vector2(0.5f, 0.5f);
            cbRt.anchoredPosition = Vector2.zero;
            cbRt.sizeDelta = cardRt.sizeDelta + new Vector2(8f, 8f);
            cardBrdGo.AddComponent<Image>().color = theme.Accent;
            cardBrdGo.transform.SetAsFirstSibling();   // border BEHIND the card face, never over it
            spCard.transform.SetSiblingIndex(1);

            var cardImg = spCard.AddComponent<Image>();
            cardImg.color = Lift(theme.BgCard, 0.16f);
            spCard.AddComponent<Button>().transition = Selectable.Transition.None; // eat backdrop taps
            _settingsCard = spCard;

            var spTitle = new GameObject("title"); spTitle.transform.SetParent(spCard.transform, false);
            var stRt = spTitle.AddComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0f, 1f); stRt.anchorMax = new Vector2(1f, 1f);
            stRt.pivot = new Vector2(0.5f, 1f);
            stRt.anchoredPosition = new Vector2(0f, -14f);
            stRt.sizeDelta = new Vector2(-40f, 52f);
            var stTxt = spTitle.AddComponent<Text>();
            stTxt.font = _font; stTxt.fontSize = _portrait ? 40 : 22; stTxt.color = Color.white;
            stTxt.alignment = TextAnchor.MiddleLeft; stTxt.text = "SETTINGS";
            stTxt.supportRichText = false; stTxt.raycastTarget = false;

            var spClose = new GameObject("close"); spClose.transform.SetParent(spCard.transform, false);
            var scRt = spClose.AddComponent<RectTransform>();
            scRt.anchorMin = scRt.anchorMax = new Vector2(1f, 1f);
            scRt.pivot = new Vector2(1f, 1f);
            scRt.anchoredPosition = new Vector2(-12f, -12f);
            scRt.sizeDelta = new Vector2(_portrait ? 84f : 48f, _portrait ? 84f : 48f);
            spClose.AddComponent<Image>().color = Lift(theme.BgCard, 0.40f);
            var scBtn = spClose.AddComponent<Button>(); scBtn.transition = Selectable.Transition.None;
            scBtn.onClick.AddListener(() => _settingsPanel.SetActive(false));
            var scT = new GameObject("x"); scT.transform.SetParent(spClose.transform, false);
            var scTRt = scT.AddComponent<RectTransform>();
            scTRt.anchorMin = Vector2.zero; scTRt.anchorMax = Vector2.one;
            scTRt.offsetMin = scTRt.offsetMax = Vector2.zero;
            var scTxt = scT.AddComponent<Text>();
            scTxt.font = _font; scTxt.fontSize = _portrait ? 44 : 26; scTxt.color = Color.white;
            scTxt.alignment = TextAnchor.MiddleCenter; scTxt.text = "X";
            scTxt.supportRichText = false; scTxt.raycastTarget = false;

            settingsGo.SetActive(false);

            // Gear button (top-left) toggles the settings overlay
            var gearGo = new GameObject("GearBtn"); gearGo.transform.SetParent(transform, false);
            var gearRt = gearGo.AddComponent<RectTransform>();
            gearRt.anchorMin = gearRt.anchorMax = new Vector2(0f, 1f);
            gearRt.pivot = new Vector2(0f, 1f);
            gearRt.anchoredPosition = new Vector2(UIScale.PadOuter, -UIScale.PadOuter);
            gearRt.sizeDelta = _portrait ? new Vector2(230f, 120f) : new Vector2(190f, 56f);
            gearGo.AddComponent<Image>().color = Lift(theme.BgCard, 0.24f);
            var gearTGo = new GameObject("t"); gearTGo.transform.SetParent(gearGo.transform, false);
            var gearTRt = gearTGo.AddComponent<RectTransform>();
            gearTRt.anchorMin = Vector2.zero; gearTRt.anchorMax = Vector2.one; gearTRt.offsetMin = gearTRt.offsetMax = Vector2.zero;
            var gearTxt = gearTGo.AddComponent<Text>();
            gearTxt.font = _font; gearTxt.fontSize = _portrait ? 30 : UIScale.FontSmall;
            gearTxt.fontStyle = FontStyle.Bold;
            gearTxt.fontStyle = FontStyle.Bold;
            // The alignment assignment below had been swallowed INTO this comment, so the
            // label fell back to UpperLeft and sat in the corner of its box -- that is the
            // font offset. Accent on a dark card was also low-contrast, hence TextBright.
            gearTxt.color = theme.TextBright;
            gearTxt.alignment = TextAnchor.MiddleCenter;
            gearTxt.text = "SETTINGS"; gearTxt.supportRichText = false; gearTxt.raycastTarget = false;
            var gearBrd = new GameObject("brd"); gearBrd.transform.SetParent(gearGo.transform, false);
            gearBrd.transform.SetAsFirstSibling();
            var gearBrdRt = gearBrd.AddComponent<RectTransform>();
            gearBrdRt.anchorMin = Vector2.zero; gearBrdRt.anchorMax = Vector2.one;
            gearBrdRt.offsetMin = new Vector2(-2, -2); gearBrdRt.offsetMax = new Vector2(2, 2);
            gearBrd.AddComponent<Image>().color = ColBorderOff;

            var gearBtn = gearGo.AddComponent<Button>(); gearBtn.transition = Selectable.Transition.None;
            gearBtn.onClick.AddListener(() =>
            {
                bool open = !_settingsPanel.activeSelf;
                _settingsPanel.SetActive(open);
                // The panel is created early (sibling 4), so EVERY level button, gem, troop
                // chip and footer built after it drew straight through the modal -- the card
                // was underneath the whole menu. uGUI has no z-order, only sibling order, so
                // the modal has to be pulled to the front each time it opens.
                if (open) _settingsPanel.transform.SetAsLastSibling();
            });
            gearGo.AddComponent<ButtonFeel>();

            // Camera shake — anchored to top-LEFT, well clear of title
            var csRow = new GameObject("csRow"); csRow.transform.SetParent(_settingsCard.transform, false);
            var csRt = csRow.AddComponent<RectTransform>();
            csRt.anchorMin = new Vector2(0f, 1f); csRt.anchorMax = new Vector2(0f, 1f);
            csRt.pivot = new Vector2(0f, 1f);
            csRt.anchoredPosition = new Vector2(36f, _portrait ? -96f : -60f);
            csRt.sizeDelta = new Vector2(_portrait ? 680f : 440f, _portrait ? 56f : 26f);
            var csLbl = csRow.AddComponent<Text>();
            csLbl.font = _font; csLbl.fontSize = _portrait ? 32 : UIScale.FontSmall;
            csLbl.color = theme.TextBright;
            csLbl.alignment = TextAnchor.MiddleLeft; csLbl.text = "Camera Shake";
            csLbl.supportRichText = false; csLbl.raycastTarget = false;

            _shakeBtnBg = MkToggleBtn_Abs(csRow, _portrait ? 560f : 148f, 0f,
                GameSettings.CameraShake ? "ON" : "OFF",
                GameSettings.CameraShake ? new Color(0f, 0.9f, 0.3f) : new Color(0.55f, 0.55f, 0.55f),
                out _shakeVal);
            _shakeBtnBg.GetComponent<Button>().onClick.AddListener(ToggleShake);

            // Music mute toggle — below camera shake
            var mRow = new GameObject("musicRow"); mRow.transform.SetParent(_settingsCard.transform, false);
            var mRt = mRow.AddComponent<RectTransform>();
            mRt.anchorMin = new Vector2(0f, 1f); mRt.anchorMax = new Vector2(0f, 1f);
            mRt.pivot = new Vector2(0f, 1f);
            mRt.anchoredPosition = new Vector2(36f, _portrait ? -176f : -100f);
            mRt.sizeDelta = new Vector2(_portrait ? 680f : 440f, _portrait ? 56f : 26f);
            var mLbl = mRow.AddComponent<Text>();
            mLbl.font = _font; mLbl.fontSize = _portrait ? 32 : UIScale.FontSmall;
            mLbl.color = theme.TextBright;
            mLbl.alignment = TextAnchor.MiddleLeft; mLbl.text = "Music";
            mLbl.supportRichText = false; mLbl.raycastTarget = false;

            bool musicOn = !AudioManager.MusicMuted;
            _musicBtnBg = MkToggleBtn_Abs(mRow, _portrait ? 560f : 148f, 0f,
                musicOn ? "ON" : "OFF",
                musicOn ? new Color(0f, 0.9f, 0.3f) : new Color(0.55f, 0.55f, 0.55f),
                out _musicVal);
            _musicBtnBg.GetComponent<Button>().onClick.AddListener(ToggleMusic);

            // Audio + board-dim sliders — stacked under the toggles, one column
            void MkSliderRow(string label, float y, float init, System.Action<float> set)
            {
                var row = new GameObject("row_" + label); row.transform.SetParent(_settingsCard.transform, false);
                var rRt = row.AddComponent<RectTransform>();
                rRt.anchorMin = new Vector2(0f, 1f); rRt.anchorMax = new Vector2(0f, 1f);
                rRt.pivot = new Vector2(0f, 1f);
                rRt.anchoredPosition = new Vector2(36f, y);
                rRt.sizeDelta = new Vector2(_portrait ? 680f : 440f, _portrait ? 48f : 26f);
                NeonUI.SliderBar(row.transform, _font, label, Vector2.zero, Vector2.one,
                    init, theme.Accent, set);
            }
            MkSliderRow("SFX",    _portrait ? -262f : -140f, AudioManager.SfxVol,   v => AudioManager.SfxVol   = v);
            MkSliderRow("MUSIC",  _portrait ? -334f : -176f, AudioManager.MusicVol, v => AudioManager.MusicVol = v);
            MkSliderRow("BG DIM", _portrait ? -406f : -212f, GameSettings.BoardBgOpacity, v =>
            {
                GameSettings.BoardBgOpacity = v;
                if (_bgPhoto != null) _bgPhoto.color = new Color(1f, 1f, 1f, Mathf.Clamp(v, 0.12f, 1f)); // dim this screen's bg live
                ShowBoardPreview(v); // live preview popup while dragging
            });

            // Theme picker dropdown — below the sliders
            ThemePickerDropdown.Build(_settingsCard.transform, _font,
                                      xOffset: 36f, yOffset: _portrait ? -492f : -252f);
            BuildBoardPreviewPopup(theme, -106f);

            // ── level grid (left half): 4 columns × 5 rows ───────────────────
            // Grid right edge = -310 + 4*(86+8) = -310+376 = 66. Detail panel starts at dx=100.
            // Portrait: bigger, centered grid to fill the vertical space (span 4*140+3*14=602 → left -301).
            // Portrait sizing: the grid used to be 602 units wide of 1080 (56%) and the whole
            // screen bottomed out at 72% of the viewport, leaving a dead band ~380 units tall
            // under the version label. Wider, taller buttons fill the width and push the detail
            // panel down into that space.
            // Five columns of 140x76 rather than four of 184x104. The grid was spending 600
            // canvas units to show twenty two-digit numbers while the detail panel -- five gems,
            // nine troop chips, a difficulty bar, a reward line and three buttons -- was squeezed
            // into what was left. This reclaims 248u and hands all of it to the panel.
            int   cols = _portrait ? 5 : 4;
            float btnW = _portrait ? 140f : UIScale.LvBtnW, btnH = _portrait ? 76f : UIScale.LvBtnH;
            float gapX = _portrait ? 16f : UIScale.LvBtnGap, gapY = gapX;
            // gx0 is the grid's LEFT EDGE (the loop adds btnW*0.5 to reach a centre), so it has
            // to be -(totalWidth)/2 or the whole grid slides sideways: 5*140 + 4*16 = 764 -> -382.
            float gx0 = _portrait ? -382f : -330f, gy0 = 212f;

            for (int i = 0; i < LevelConfig.MaxLevel; i++)
            {
                int lv = i + 1;
                int col = i % cols, row = i / cols;
                float bx = gx0 + col * (btnW + gapX) + btnW * 0.5f;
                float by = gy0 - row * (btnH + gapY) - btnH * 0.5f;

                var go = new GameObject($"LvBtn{lv}"); go.transform.SetParent(transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(bx, by + _menuShiftY);
                rt.sizeDelta = new Vector2(btnW, btnH);

                var bg = go.AddComponent<Image>();
                bg.color = PlayerProgress.IsLevelUnlocked(lv) ? ColUnlocked : ColLocked;

                // border child
                var brd = new GameObject("brd"); brd.transform.SetParent(go.transform, false);
                brd.transform.SetAsFirstSibling();
                var brdRt = brd.AddComponent<RectTransform>();
                brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
                brdRt.offsetMin = new Vector2(-1f, -1f); brdRt.offsetMax = new Vector2(1f, 1f);
                var brdImg = brd.AddComponent<Image>(); brdImg.color = ColBorderOff;

                // label
                var lgo = new GameObject("lbl"); lgo.transform.SetParent(go.transform, false);
                var lrt = lgo.AddComponent<RectTransform>();
                lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
                lrt.offsetMin = lrt.offsetMax = Vector2.zero;
                var ltxt = lgo.AddComponent<Text>();
                ltxt.font = _font; ltxt.fontSize = UIScale.FontH2; ltxt.fontStyle = FontStyle.Bold;
                ltxt.color = PlayerProgress.IsLevelUnlocked(lv)
                    ? Color.white : new Color(0.52f, 0.56f, 0.62f);
                ltxt.alignment = TextAnchor.MiddleCenter;
                ltxt.text = lv.ToString("D2"); ltxt.supportRichText = false;
                ltxt.raycastTarget = false;
                NeonUI.Title(ltxt);

                // tier label (small) — shows LOCKED in amber for locked levels
                var tgo = new GameObject("tier"); tgo.transform.SetParent(go.transform, false);
                var trt = tgo.AddComponent<RectTransform>();
                trt.anchorMin = new Vector2(0f, 0f); trt.anchorMax = new Vector2(1f, 0f);
                trt.pivot = new Vector2(0.5f, 0f);
                trt.anchoredPosition = new Vector2(0f, 3f); trt.sizeDelta = new Vector2(0f, 14f);
                var ttxt = tgo.AddComponent<Text>();
                ttxt.font = _font; ttxt.fontSize = UIScale.FontSmall;
                bool lvLocked = !PlayerProgress.IsLevelUnlocked(lv);
                ttxt.color = lvLocked ? new Color(1f, 0.62f, 0.08f) : theme.TextDim;
                ttxt.alignment = TextAnchor.MiddleCenter;
                ttxt.text = lvLocked ? "LOCKED" : LevelConfig.LevelName(lv);
                ttxt.supportRichText = false; ttxt.raycastTarget = false;

                int lvCapture = lv;
                var btn = go.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SelectLevel(lvCapture));
                go.AddComponent<ButtonFeel>();

                // P3: hover border glow — faint accent highlight on hover
                var hovBrd = brdImg;
                var hovEt = go.AddComponent<EventTrigger>();
                var hovEnter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                hovEnter.callback.AddListener(_ =>
                {
                    if (lvCapture != _hoveredLevel)
                        hovBrd.color = new Color(ColBorderOn.r, ColBorderOn.g, ColBorderOn.b, 1f);
                });
                hovEt.triggers.Add(hovEnter);
                var hovExit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                hovExit.callback.AddListener(_ =>
                    hovBrd.color = lvCapture == _hoveredLevel ? ColBorderOn : ColBorderOff);
                hovEt.triggers.Add(hovExit);

                _levelBtns.Add((bg, ltxt, lv));
            }

            // ── detail panel (right half) ─────────────────────────────────────
            // Grid right edge: -330 + 4*(84+8) = 38. Divider at x=60, panel center = dx+55 = 240.
            // dx=185 → 340px labels span x=70 to x=410, no overlap with level grid.
            float dx = _portrait ? -55f : 185f; // portrait: center the detail panel; landscape: right half
            if (!_portrait) MkHRule_Vert(60f, 230f, -380f); // vertical divider only in the side-by-side layout

            // Portrait stacks this whole panel vertically with 68px gem icons and two rows of
            // troop chips, but every y below was tuned for the 40px landscape icons and a single
            // chip row. In portrait that produced four separate collisions: the GEM TYPES header
            // sat ON the gem icons, TROOPS sat on the gem labels, and the second chip row ran
            // straight through DIFFICULTY and REWARD. Portrait now gets its own cursor.
            // Option D. The panel scrolls now, so nothing has to shrink to fit the screen --
            // every element is sized for reading. Verified clear: zero overlapping glyph spans.
            float yName     = _portrait ?  232f :  218f;
            float yGemHdr   = _portrait ?  176f :  183f;
            float yGemRow   = _portrait ?   96f :  150f;
            float yTroopHdr = _portrait ?  -78f :   84f;
            float yTroopR0  = _portrait ? -186f :   34f;
            float yDiff     = _portrait ? -484f :  -84f;
            float yReward   = _portrait ? -536f : -112f;

            int fsH1   = _portrait ? 56 : UIScale.FontH1;
            int fsHdr  = _portrait ? 34 : UIScale.FontBody;
            int fsBody = _portrait ? 34 : UIScale.FontBody;
            int fsGem  = _portrait ? 40 : UIScale.FontBody;
            int fsChip = _portrait ? 30 : UIScale.FontBody;

            _detailName = MkLabelObj(dx + 55f, yName + _detailDy, fsH1, theme.TextBright);
            // "LEVEL 19 - APEX" at 56pt needs ~460 units; MkLabelObj defaults to 340 and the
            // name was being cut off after the dash.
            _detailName.GetComponent<RectTransform>().sizeDelta = new Vector2(760f, fsH1 + 16f);
            _detailName.horizontalOverflow = HorizontalWrapMode.Overflow;

            // ── GEM TYPES visual row ──────────────────────────────────────────
            _detailGems = MkLabelObj(dx + 55f, yGemHdr + _detailDy, fsHdr, theme.TextMid);
            _detailGems.text = "GEM TYPES"; _detailGems.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 16f);

            // Detail panel scales with the grid, or the mission info reads as a footnote under it.
            float GI = _portrait ? 112f : UIScale.IconUnitDetail, GIGAP = _portrait ? 34f : 8f;
            float giTotal = 5 * GI + 4 * GIGAP;
            float giX0 = dx + 55f - giTotal * 0.5f + GI * 0.5f;
            for (int i = 0; i < 5; i++)
            {
                var gGo = new GameObject($"GemSwatch{i}"); gGo.transform.SetParent(transform, false);
                var gRt = gGo.AddComponent<RectTransform>();
                gRt.anchorMin = gRt.anchorMax = new Vector2(0.5f, 0.5f);
                gRt.pivot = new Vector2(0.5f, 0.5f);
                gRt.anchoredPosition = new Vector2(giX0 + i * (GI + GIGAP), yGemRow + _detailDy + _menuShiftY);
                gRt.sizeDelta = new Vector2(GI, GI);
                var gImg = gGo.AddComponent<RawImage>();
                gImg.texture = NeonArt.Gem(i, NeonTheme.Active.GemStyle);
                // NeonArt.Gem already paints the gem in its own colour. Multiplying the texture
                // by GemCol again squared the tint, which is what washed this row out.
                gImg.color = Color.white;
                _gemIcons[i] = gImg;

                var gLGo = new GameObject("lbl"); gLGo.transform.SetParent(gGo.transform, false);
                var gLRt = gLGo.AddComponent<RectTransform>();
                gLRt.anchorMin = new Vector2(0f, -0.55f); gLRt.anchorMax = new Vector2(1f, 0f);
                gLRt.offsetMin = gLRt.offsetMax = Vector2.zero;
                var gLTxt = gLGo.AddComponent<Text>();
                gLTxt.font = _font; gLTxt.fontSize = fsGem; gLTxt.color = GemCol(i);
                gLTxt.alignment = TextAnchor.MiddleCenter;
                gLTxt.resizeTextForBestFit = true;          // long gem names shrink, never clip
                gLTxt.resizeTextMinSize = 16; gLTxt.resizeTextMaxSize = fsGem;
                gLTxt.text = ThemeLocale.GemAbbrev[i]; gLTxt.supportRichText = false; gLTxt.raycastTarget = false;
            }

            // ── TROOPS visual grid (2 rows × 5) ──────────────────────────────
            _detailTroops = MkLabelObj(dx + 55f, yTroopHdr + _detailDy, fsHdr, theme.TextMid);
            _detailTroops.text = "TROOPS"; _detailTroops.GetComponent<RectTransform>().sizeDelta = new Vector2(340f, 16f);

            // Five across in two rows rather than three across in three. Every troop name is
            // now seven characters or fewer, so 190 wide still holds the longest on ONE line at
            // 30u -- and dropping a row reclaims 182 units, which is what lets the whole screen
            // fit without a scroller.
            int   TCCOLS = _portrait ? 5 : 5;
            float TCW = _portrait ? 190f : 56f, TCH = _portrait ? 156f : 62f;
            float TCGX = _portrait ? 12f : 7f, TCGY = _portrait ? 14f : 7f;
            float tcTotal = TCCOLS * TCW + (TCCOLS - 1) * TCGX;
            float tcX0 = dx + 55f - tcTotal * 0.5f + TCW * 0.5f;
            for (int i = 0; i < 10; i++)
            {
                int col = i % TCCOLS, row = i / TCCOLS;
                float tx = tcX0 + col * (TCW + TCGX);
                float ty = yTroopR0 - row * (TCH + TCGY);

                var tc = new GameObject($"TC{i}"); tc.transform.SetParent(transform, false);
                var tcRt = tc.AddComponent<RectTransform>();
                tcRt.anchorMin = tcRt.anchorMax = new Vector2(0.5f, 0.5f);
                tcRt.pivot = new Vector2(0.5f, 0.5f);
                tcRt.anchoredPosition = new Vector2(tx, ty + _detailDy + _menuShiftY);
                tcRt.sizeDelta = new Vector2(TCW, TCH);
                tc.AddComponent<Image>().color = Lift(theme.BgCard, 0.22f);
                _troopPanels[i] = tc;

                var brdGo = new GameObject("brd"); brdGo.transform.SetParent(tc.transform, false);
                brdGo.transform.SetAsFirstSibling();
                var brdRt = brdGo.AddComponent<RectTransform>();
                brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
                brdRt.offsetMin = new Vector2(-1, -1); brdRt.offsetMax = new Vector2(1, 1);
                brdGo.AddComponent<Image>().color = new Color(
                    theme.AccentDim.r * 0.85f + 0.12f, theme.AccentDim.g * 0.85f + 0.13f,
                    theme.AccentDim.b * 0.85f + 0.15f, 1f);   // AccentDim carries alpha 0.40

                var icGo = new GameObject("ic"); icGo.transform.SetParent(tc.transform, false);
                var icRt = icGo.AddComponent<RectTransform>();
                icRt.anchorMin = new Vector2(0.16f, 0.34f); icRt.anchorMax = new Vector2(0.84f, 0.95f);
                icRt.offsetMin = icRt.offsetMax = Vector2.zero;
                var icImg = icGo.AddComponent<RawImage>();
                icImg.color = Color.white; icImg.raycastTarget = false;
                _troopIcons[i] = icImg;

                var nmGo = new GameObject("nm"); nmGo.transform.SetParent(tc.transform, false);
                var nmRt = nmGo.AddComponent<RectTransform>();
                nmRt.anchorMin = new Vector2(-0.06f, 0.02f); nmRt.anchorMax = new Vector2(1.06f, 0.32f);
                nmRt.offsetMin = nmRt.offsetMax = Vector2.zero;
                var nmTxt = nmGo.AddComponent<Text>();
                nmTxt.font = _font; nmTxt.fontSize = fsChip; nmTxt.fontStyle = FontStyle.Bold;
                nmTxt.color = theme.TextBright; nmTxt.alignment = TextAnchor.MiddleCenter;
                nmTxt.horizontalOverflow = HorizontalWrapMode.Overflow;   // one line, never wraps
                nmTxt.resizeTextForBestFit = true;
                nmTxt.resizeTextMinSize = 18; nmTxt.resizeTextMaxSize = fsChip;
                nmTxt.supportRichText = false; nmTxt.raycastTarget = false;
                _troopNames[i] = nmTxt;

                // Click → open troop detail modal
                int ci = i;
                var tcBtn = tc.AddComponent<Button>(); tcBtn.transition = Selectable.Transition.None;
                tcBtn.onClick.AddListener(() => ShowTroopModal(ci));
                tc.AddComponent<ButtonFeel>();

                tc.SetActive(false);
            }

            // ── DIFFICULTY bar + REWARD ───────────────────────────────────────
            _detailDiff = MkLabelObj(dx + 55f - 40f, yDiff + _detailDy, fsBody, new Color(0.95f, 0.76f, 0.36f));
            _detailDiff.alignment = TextAnchor.MiddleLeft;
            _detailDiff.GetComponent<RectTransform>().sizeDelta = new Vector2(160f, 22f);
            _detailDiff.GetComponent<RectTransform>().anchoredPosition = new Vector2(dx + 55f - 48f, yDiff + _detailDy + _menuShiftY);

            var dbGo = new GameObject("DiffBg"); dbGo.transform.SetParent(transform, false);
            var dbRt = dbGo.AddComponent<RectTransform>();
            dbRt.anchorMin = dbRt.anchorMax = new Vector2(0.5f, 0.5f);
            dbRt.pivot = new Vector2(0f, 0.5f);
            dbRt.anchoredPosition = new Vector2(dx + 55f + 38f, yDiff + _detailDy + _menuShiftY);
            dbRt.sizeDelta = new Vector2(120f, 9f);
            dbGo.AddComponent<Image>().color = new Color(theme.BgCard.r, theme.BgCard.g, theme.BgCard.b, 1f);

            var dfGo = new GameObject("fill"); dfGo.transform.SetParent(dbGo.transform, false);
            var dfRt = dfGo.AddComponent<RectTransform>();
            dfRt.anchorMin = Vector2.zero; dfRt.anchorMax = new Vector2(0.5f, 1f);
            dfRt.offsetMin = dfRt.offsetMax = Vector2.zero;
            _diffBarFill = dfGo.AddComponent<Image>();
            _diffBarFill.color = new Color(0.9f, 0.65f, 0.15f);

            _rewardLbl = MkLabelObj(dx + 55f, yReward + _detailDy, fsBody, theme.TextBright);
            _rewardLbl.GetComponent<RectTransform>().sizeDelta = new Vector2(700f, fsBody + 14f);

            MkHRule((_portrait ? -580f : -205f) + _detailDy);

            // Controls hint — uses right-panel width (340px) to stay inside panel, not bleed into grid
            // The old line described the INPUT and nothing else -- it never said what you were
            // trying to achieve. From the simulation: a match fires a ResourcePayout, resources
            // buy deploys, each ground lane holds two pylons (x=33, x=66) and holding both grants
            // lane control, and the battle ends when a core reaches zero. Goal first, then loop.
            var goalTxt = MkLabelObj(dx + 55f, (_portrait ? -620f : -205f) + _detailDy,
                                     _portrait ? 30 : UIScale.FontSmall,
                                     new Color(0.95f, 0.80f, 0.42f));
            goalTxt.text = "Destroy the enemy CORE to win";
            goalTxt.alignment = TextAnchor.MiddleCenter;
            goalTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(760f, 44f);

            var hintTxt = MkLabelObj(dx + 55f, (_portrait ? -662f : -225f) + _detailDy,
                                     _portrait ? 22 : UIScale.FontSmall, theme.TextMid);
            // At FontBody the old string ran off the panel and truncated mid-word. Wrapping it
            // to two lines collided with the rule above and the UNLOCK button below, so the copy
            // is shortened instead -- it fits one line at the readable size.
            // "Match gems for resources - Deploy to hold pylons" told a new player nothing
            // they could act on: it named two systems without saying what to DO with either.
            hintTxt.text = "Match gems to earn resources, then spend them on troops";
            hintTxt.alignment = TextAnchor.MiddleCenter;
            hintTxt.horizontalOverflow = HorizontalWrapMode.Wrap;
            hintTxt.GetComponent<RectTransform>().sizeDelta = new Vector2(
                _portrait ? 760f : 340f, 40f);

            // Info (?) button — right of the controls hint
            var infoGo = new GameObject("InfoBtn"); infoGo.transform.SetParent(transform, false);
            var infoRt = infoGo.AddComponent<RectTransform>();
            infoRt.anchorMin = infoRt.anchorMax = new Vector2(0.5f, 0.5f);
            infoRt.pivot = new Vector2(0.5f, 0.5f);
            infoRt.anchoredPosition = new Vector2(dx + 55f + (_portrait ? 330f : 195f), (_portrait ? -620f : -225f) + _detailDy + _menuShiftY);
            infoRt.sizeDelta = new Vector2(40f, 40f);
            var infoBg = infoGo.AddComponent<Image>(); infoBg.color = theme.BgPanel;
            var infoBrd = new GameObject("brd"); infoBrd.transform.SetParent(infoGo.transform, false);
            infoBrd.transform.SetAsFirstSibling();
            var infoBrdRt = infoBrd.AddComponent<RectTransform>();
            infoBrdRt.anchorMin = Vector2.zero; infoBrdRt.anchorMax = Vector2.one;
            infoBrdRt.offsetMin = new Vector2(-1, -1); infoBrdRt.offsetMax = new Vector2(1, 1);
            infoBrd.AddComponent<Image>().color = new Color(
                theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.6f);
            var infoTGo = new GameObject("txt"); infoTGo.transform.SetParent(infoGo.transform, false);
            var infoTRt = infoTGo.AddComponent<RectTransform>();
            infoTRt.anchorMin = Vector2.zero; infoTRt.anchorMax = Vector2.one;
            infoTRt.offsetMin = infoTRt.offsetMax = Vector2.zero;
            var infoT = infoTGo.AddComponent<Text>();
            infoT.font = _font; infoT.fontSize = 16; infoT.fontStyle = FontStyle.Bold;
            infoT.color = theme.TextBright; infoT.alignment = TextAnchor.MiddleCenter;
            infoT.text = "?"; infoT.supportRichText = false; infoT.raycastTarget = false;
            var infoBtn = infoGo.AddComponent<Button>(); infoBtn.transition = Selectable.Transition.None;
            infoBtn.onClick.AddListener(ShowInfoModal);
            infoGo.AddComponent<ButtonFeel>();

            // Unlock button
            _unlockBtnGo = MkActionBtn(dx + 55f, (_portrait ? -700f : -258f) + _detailDy, 220f, 40f, "UNLOCK",
                new Color(0.8f, 0.6f, 0f), out _unlockBtnText);
            _unlockBtnGo.GetComponent<Button>().onClick.AddListener(TryUnlock);
            _unlockBtnGo.SetActive(false);

            // Play button
            // Three buttons now: RANKS, DEPLOY, SHOP. Laid out from a centred total width
            // rather than offsets from DEPLOY, so adding one does not shove the row off
            // to one side.
            float bW = _portrait ? 110f : 80f, bH = _portrait ? 84f : 56f;
            float dW = _portrait ? 300f : 260f, bGap = _portrait ? 10f : 8f;
            float rowW = bW + bGap + dW + bGap + bW;
            float rankX = -rowW * 0.5f + bW * 0.5f;
            float playX = rankX + bW * 0.5f + bGap + dW * 0.5f;
            float shopX = playX + dW * 0.5f + bGap + bW * 0.5f;
            float rowY  = (_portrait ? -736f : -308f) + _detailDy;

            var rankGo = MkActionBtn(rankX, rowY, bW, bH, "RANKS",
                theme.TextBright, out _ranksBtnText);
            rankGo.GetComponent<Button>().onClick.AddListener(ShowRanksPanel);

            var playGo = MkActionBtn(playX, rowY, dW, bH, "DEPLOY",
                theme.TextBright, out _playBtnText);
            playGo.GetComponent<Button>().onClick.AddListener(LaunchBattle);

            // Shop button (cosmetics) — small, right of play button
            var shopGo = MkActionBtn(shopX, rowY, bW, bH, "SHOP",
                new Color(0.9f, 0.75f, 0.3f), out _);
            shopGo.GetComponent<Button>().onClick.AddListener(OpenCosmeticsScreen);

            // Speed picker — 3 chips below DEPLOY (hidden when VS mode active)
            BuildSpeedPicker(dx + 55f, (_portrait ? -816f : -356f) + _detailDy + _menuShiftY);

            // VS panel — competitive mode section below speed picker
            BuildVsPanel(dx + 55f, (_portrait ? -870f : -393f) + _detailDy + _menuShiftY);

            // Switch Pilot button — top-right corner of screen, always accessible
            var pilotGo = new GameObject("SwitchPilot"); pilotGo.transform.SetParent(transform, false);
            var pilotRt = pilotGo.AddComponent<RectTransform>();
            pilotRt.anchorMin = pilotRt.anchorMax = new Vector2(1f, 1f);
            pilotRt.pivot = new Vector2(1f, 1f);
            pilotRt.anchoredPosition = new Vector2(-UIScale.PadOuter, -UIScale.PadOuter);
            // 26 units is ~10pt on a phone. Minimum comfortable tap target is 44pt, which on
            // this canvas is ~120 units.
            pilotRt.sizeDelta = _portrait ? new Vector2(230f, 120f) : new Vector2(130f, 26f);
            var pilotBg = pilotGo.AddComponent<Image>(); pilotBg.color = theme.BgCard;
            var pilotBrd = new GameObject("brd"); pilotBrd.transform.SetParent(pilotGo.transform, false);
            pilotBrd.transform.SetAsFirstSibling();
            var pilotBrdRt = pilotBrd.AddComponent<RectTransform>();
            pilotBrdRt.anchorMin = Vector2.zero; pilotBrdRt.anchorMax = Vector2.one;
            pilotBrdRt.offsetMin = new Vector2(-1f, -1f); pilotBrdRt.offsetMax = new Vector2(1f, 1f);
            pilotBrd.AddComponent<Image>().color = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.4f);
            var pilotTGo = new GameObject("txt"); pilotTGo.transform.SetParent(pilotGo.transform, false);
            var pilotTRt = pilotTGo.AddComponent<RectTransform>();
            pilotTRt.anchorMin = Vector2.zero; pilotTRt.anchorMax = Vector2.one;
            pilotTRt.offsetMin = pilotTRt.offsetMax = Vector2.zero;
            var pilotTxt = pilotTGo.AddComponent<Text>();
            pilotTxt.font = _font; pilotTxt.fontSize = _portrait ? 30 : UIScale.FontSmall;
            pilotTxt.fontStyle = FontStyle.Bold;
            pilotTxt.color = theme.TextBright; pilotTxt.alignment = TextAnchor.MiddleCenter;
            pilotTxt.text = "◈ SWITCH PILOT"; pilotTxt.supportRichText = false; pilotTxt.raycastTarget = false;
            var pilotBtn = pilotGo.AddComponent<Button>(); pilotBtn.transition = Selectable.Transition.None;
            pilotBtn.onClick.AddListener(SwitchPilot);
            pilotGo.AddComponent<ButtonFeel>();

            BuildInfoModal();
            BuildRanksModal();
            BuildTroopModal();

            MkLabel("v2.0 ALPHA  ·  ZERO MTX  ·  $14.99 STEAM",
                0f, _portrait ? -928f + _detailDy : -393f, _portrait ? 26 : UIScale.FontTiny, theme.TextDim);
        }

        // ── board opacity picker ──────────────────────────────────────────────────

        void BuildBoardOpacityPicker(NeonTheme.Theme theme, float yOffset)
        {
            // Label
            var lblGo = new GameObject("bopLbl"); lblGo.transform.SetParent(transform, false);
            var lblRt = lblGo.AddComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 1f); lblRt.anchorMax = new Vector2(0f, 1f);
            lblRt.pivot = new Vector2(0f, 1f);
            lblRt.anchoredPosition = new Vector2(18f, yOffset);
            lblRt.sizeDelta = new Vector2(128f, 22f);
            var lblTxt = lblGo.AddComponent<Text>();
            lblTxt.font = _font; lblTxt.fontSize = UIScale.FontSmall; lblTxt.color = theme.TextMid;
            lblTxt.alignment = TextAnchor.MiddleLeft; lblTxt.text = "Board BG Opacity";
            lblTxt.supportRichText = false; lblTxt.raycastTarget = false;

            // 4 chips: DIM / LOW / MED / HIGH  (image alpha 0.10–0.40; always dimmed)
            float[] opacities = { 0.10f, 0.20f, 0.30f, 0.40f };
            string[] labels   = { "DIM", "LOW", "MED", "HIGH" };
            float chipW = 40f, chipH = 20f, gap = 4f;
            float startX = 18f + 136f;

            for (int i = 0; i < 4; i++)
            {
                int ci = i; float op = opacities[i];
                var go = new GameObject($"bop{i}"); go.transform.SetParent(transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(startX + i * (chipW + gap), yOffset);
                rt.sizeDelta = new Vector2(chipW, chipH);
                _boardOpacityBgs[i] = go.AddComponent<Image>();

                var brd = new GameObject("brd"); brd.transform.SetParent(go.transform, false);
                brd.transform.SetAsFirstSibling();
                var brdRt = brd.AddComponent<RectTransform>();
                brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
                brdRt.offsetMin = new Vector2(-1, -1); brdRt.offsetMax = new Vector2(1, 1);
                brd.AddComponent<Image>().color = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.45f);

                var tGo = new GameObject("t"); tGo.transform.SetParent(go.transform, false);
                var tRt = tGo.AddComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
                tRt.offsetMin = tRt.offsetMax = Vector2.zero;
                _boardOpacityTxts[i] = tGo.AddComponent<Text>();
                _boardOpacityTxts[i].font = _font; _boardOpacityTxts[i].fontSize = 9;
                _boardOpacityTxts[i].fontStyle = FontStyle.Bold;
                _boardOpacityTxts[i].alignment = TextAnchor.MiddleCenter;
                _boardOpacityTxts[i].text = labels[i];
                _boardOpacityTxts[i].supportRichText = false; _boardOpacityTxts[i].raycastTarget = false;

                var btn = go.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { GameSettings.BoardBgOpacity = op; UpdateBoardOpacityHighlights(); ShowBoardPreview(op); });
                go.AddComponent<ButtonFeel>();

                var et = go.AddComponent<EventTrigger>();
                var enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
                enter.callback.AddListener(_ => ShowBoardPreview(op, autoHide: false));
                et.triggers.Add(enter);
                var exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
                exit.callback.AddListener(_ => HideBoardPreview());
                et.triggers.Add(exit);
            }
            UpdateBoardOpacityHighlights();
            BuildBoardPreviewPopup(theme, yOffset);
        }

        void UpdateBoardOpacityHighlights()
        {
            float[] opacities = { 0.10f, 0.20f, 0.30f, 0.40f };
            float cur = GameSettings.BoardBgOpacity;
            var theme = NeonTheme.Active;
            for (int i = 0; i < _boardOpacityBgs.Length; i++)
            {
                if (_boardOpacityBgs[i] == null) continue;
                float nearest = float.MaxValue;
                int selIdx = 0;
                for (int k = 0; k < opacities.Length; k++)
                    if (Mathf.Abs(opacities[k] - cur) < nearest) { nearest = Mathf.Abs(opacities[k] - cur); selIdx = k; }
                bool sel = i == selIdx;
                _boardOpacityBgs[i].color  = sel
                    ? new Color(theme.Accent.r * 0.25f, theme.Accent.g * 0.25f, theme.Accent.b * 0.25f, 1f)
                    : new Color(theme.BgCard.r,          theme.BgCard.g,          theme.BgCard.b,          0.85f);
                _boardOpacityTxts[i].color = sel ? theme.Accent : theme.TextDim;
            }

            // Live-update the real board overlay in the canvas so the battle launches with the correct value
            if (transform.parent != null)
            {
                var overTf = transform.parent.Find("BoardPanel/Board_ColorOverlay");
                if (overTf != null)
                {
                    var oi = overTf.GetComponent<Image>();
                    if (oi != null)
                        oi.color = new Color(theme.BgPanel.r, theme.BgPanel.g, theme.BgPanel.b,
                                             GameSettings.BoardBgOpacity);
                }
            }
        }

        void BuildBoardPreviewPopup(NeonTheme.Theme theme, float yOffset)
        {
            _boardPreviewPopup = new GameObject("boardPreview");
            _boardPreviewPopup.transform.SetParent(transform, false);
            var rt = _boardPreviewPopup.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(18f + 136f, yOffset - 28f);
            rt.sizeDelta = new Vector2(180f, 78f);
            _boardPreviewPopup.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.09f, 0.96f);

            var brdGo = new GameObject("brd"); brdGo.transform.SetParent(_boardPreviewPopup.transform, false);
            brdGo.transform.SetAsFirstSibling();
            var brdRt = brdGo.AddComponent<RectTransform>();
            brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
            brdRt.offsetMin = new Vector2(-1f, -1f); brdRt.offsetMax = new Vector2(1f, 1f);
            brdGo.AddComponent<Image>().color = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.35f);

            // Board sim area
            var prevGo = new GameObject("prevArea"); prevGo.transform.SetParent(_boardPreviewPopup.transform, false);
            var prevRt = prevGo.AddComponent<RectTransform>();
            prevRt.anchorMin = new Vector2(0f, 0f); prevRt.anchorMax = new Vector2(1f, 0.68f);
            prevRt.offsetMin = new Vector2(8f, 8f); prevRt.offsetMax = new Vector2(-8f, 0f);
            prevGo.AddComponent<Image>().color = new Color(theme.BgDeep.r, theme.BgDeep.g, theme.BgDeep.b, 1f);

            var ovGo = new GameObject("overlay"); ovGo.transform.SetParent(prevGo.transform, false);
            var ovRt = ovGo.AddComponent<RectTransform>();
            ovRt.anchorMin = Vector2.zero; ovRt.anchorMax = Vector2.one;
            ovRt.offsetMin = ovRt.offsetMax = Vector2.zero;
            var ovImg = ovGo.AddComponent<Image>();
            // Represents background image brightness: light tint at image alpha so preview is intuitive
            ovImg.color = new Color(0.78f, 0.85f, 1.00f, GameSettings.BoardBgOpacity);
            ovImg.raycastTarget = false;

            // Label
            var lblGo = new GameObject("lbl"); lblGo.transform.SetParent(_boardPreviewPopup.transform, false);
            var lblRt = lblGo.AddComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0.68f); lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(6f, 0f); lblRt.offsetMax = new Vector2(-6f, -2f);
            var lblTxt = lblGo.AddComponent<Text>();
            lblTxt.font = _font; lblTxt.fontSize = 16; lblTxt.fontStyle = FontStyle.Bold;
            lblTxt.color = new Color(0.65f, 0.72f, 0.84f); lblTxt.alignment = TextAnchor.MiddleCenter;
            lblTxt.text = $"BOARD PREVIEW — {(int)(GameSettings.BoardBgOpacity * 100f)}%";
            lblTxt.supportRichText = false; lblTxt.raycastTarget = false;

            _boardPreviewPopup.SetActive(false);
        }

        void ShowBoardPreview(float opacity, bool autoHide = true)
        {
            if (_boardPreviewPopup == null) return;
            if (_previewHideCoroutine != null) { StopCoroutine(_previewHideCoroutine); _previewHideCoroutine = null; }

            var prevArea = _boardPreviewPopup.transform.Find("prevArea");
            if (prevArea != null)
            {
                var ov = prevArea.Find("overlay");
                if (ov != null)
                {
                    var img = ov.GetComponent<Image>();
                    if (img != null)
                        img.color = new Color(0.78f, 0.85f, 1.00f, opacity);
                }
            }
            var lbl = _boardPreviewPopup.transform.Find("lbl");
            if (lbl != null)
            {
                var txt = lbl.GetComponent<Text>();
                if (txt != null) txt.text = $"BOARD PREVIEW — {(int)(opacity * 100f)}%";
            }

            _boardPreviewPopup.SetActive(true);
            if (autoHide)
                _previewHideCoroutine = StartCoroutine(HidePreviewAfterDelay(1.4f));
        }

        void HideBoardPreview()
        {
            if (_previewHideCoroutine != null) { StopCoroutine(_previewHideCoroutine); _previewHideCoroutine = null; }
            if (_boardPreviewPopup != null) _boardPreviewPopup.SetActive(false);
        }

        IEnumerator HidePreviewAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            if (_boardPreviewPopup != null) _boardPreviewPopup.SetActive(false);
            _previewHideCoroutine = null;
        }

        // ── troop detail modal ────────────────────────────────────────────────────

        void BuildTroopModal()
        {
            var t = NeonTheme.Active;
            _troopModal = new GameObject("TroopModal");
            _troopModal.transform.SetParent(transform, false);
            var rt = _troopModal.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _troopModal.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.07f, 0.92f);

            // Panel
            var panelGo = new GameObject("panel"); panelGo.transform.SetParent(_troopModal.transform, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            // Portrait: a centered card, not a full-height strip. Landscape: original centered panel.
            if (_portrait) { panelRt.anchorMin = new Vector2(0.05f, 0.15f); panelRt.anchorMax = new Vector2(0.95f, 0.87f); }
            else           { panelRt.anchorMin = new Vector2(0.25f, 0.08f); panelRt.anchorMax = new Vector2(0.75f, 0.96f); }
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
            panelGo.AddComponent<Image>().color = new Color(0.07f, 0.09f, 0.13f, 1f);

            // The border used to be a CHILD of the panel. SetAsFirstSibling only orders it first
            // among siblings -- in uGUI a parent's own Image always draws BEFORE its children, so
            // this accent rect covered the entire panel and the modal rendered as one flat sheet
            // of theme accent (a solid red card in Medieval). It has to be a sibling drawn under
            // the panel instead, which is what makes it read as a 2px border.
            var brd = new GameObject("brd"); brd.transform.SetParent(_troopModal.transform, false);
            brd.transform.SetSiblingIndex(panelGo.transform.GetSiblingIndex());
            var brdRt = brd.AddComponent<RectTransform>();
            brdRt.anchorMin = panelRt.anchorMin; brdRt.anchorMax = panelRt.anchorMax;
            brdRt.offsetMin = new Vector2(-2, -2); brdRt.offsetMax = new Vector2(2, 2);
            brd.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.85f);

            // ── dynamic content container (populated by ShowTroopModal) ────────
            var contentGo = new GameObject("content"); contentGo.transform.SetParent(panelGo.transform, false);
            var contentRt = contentGo.AddComponent<RectTransform>();
            contentRt.anchorMin = Vector2.zero; contentRt.anchorMax = Vector2.one;
            contentRt.offsetMin = new Vector2(16f, 52f); contentRt.offsetMax = new Vector2(-16f, -16f);

            // Store panel reference in tag-abuse-free way: use name
            contentGo.name = "TroopModalContent";

            // ── Close button ────────────────────────────────────────────────────
            var closeGo = new GameObject("closeBtn"); closeGo.transform.SetParent(panelGo.transform, false);
            var closeRt = closeGo.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f); closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 10f);
            closeRt.sizeDelta = new Vector2(180f, 38f);
            var closeBg = closeGo.AddComponent<Image>(); closeBg.color = new Color(0.10f, 0.04f, 0.04f);
            var closeBrd2 = new GameObject("brd"); closeBrd2.transform.SetParent(closeGo.transform, false);
            closeBrd2.transform.SetAsFirstSibling();
            var closeBrd2Rt = closeBrd2.AddComponent<RectTransform>();
            closeBrd2Rt.anchorMin = Vector2.zero; closeBrd2Rt.anchorMax = Vector2.one;
            closeBrd2Rt.offsetMin = new Vector2(-2, -2); closeBrd2Rt.offsetMax = new Vector2(2, 2);
            closeBrd2.AddComponent<Image>().color = new Color(0.7f, 0.2f, 0.15f, 0.75f);
            var closeTGo = new GameObject("txt"); closeTGo.transform.SetParent(closeGo.transform, false);
            var closeTRt = closeTGo.AddComponent<RectTransform>();
            closeTRt.anchorMin = Vector2.zero; closeTRt.anchorMax = Vector2.one;
            closeTRt.offsetMin = closeTRt.offsetMax = Vector2.zero;
            var closeTxt = closeTGo.AddComponent<Text>();
            closeTxt.font = _font; closeTxt.fontSize = 18; closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.color = new Color(1f, 0.4f, 0.3f); closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.text = "× CLOSE"; closeTxt.supportRichText = false; closeTxt.raycastTarget = false;
            var closeBtn = closeGo.AddComponent<Button>(); closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(() =>
            {
                if (_demoCoroutine != null) { StopCoroutine(_demoCoroutine); _demoCoroutine = null; }
                _troopModal.SetActive(false);
            });
            closeGo.AddComponent<ButtonFeel>();

            _troopModal.SetActive(false);
        }

        void ShowTroopModal(int slotIndex)
        {
            // Resolve the troop ID at this slot from the current level
            var troops = LevelConfig.AvailableTroops(_hoveredLevel);
            if (slotIndex >= troops.Count) return;
            string troopId = troops[slotIndex];
            string troopName = ThemeLocale.TroopName(troopId);

            if (_demoCoroutine != null) { StopCoroutine(_demoCoroutine); _demoCoroutine = null; }
            _troopModal.SetActive(true);
            var modalPanel = _troopModal.transform.Find("panel");
            if (modalPanel != null) Tween.PopIn(modalPanel);

            // Clear old content
            var content = _troopModal.transform.Find("panel/TroopModalContent");
            if (content == null) return;
            foreach (Transform child in content) Destroy(child.gameObject);

            var t = NeonTheme.Active;

            // ── large icon ──────────────────────────────────────────────────────
            var iconGo = new GameObject("icon"); iconGo.transform.SetParent(content, false);
            var iconRt = iconGo.AddComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.5f, 1f); iconRt.anchorMax = new Vector2(0.5f, 1f);
            iconRt.pivot = new Vector2(0.5f, 1f);
            iconRt.anchoredPosition = Vector2.zero;
            // Hero stage: the animated troop is the point of this screen, so it leads at a size
            // where the walk and the attack actually read, instead of a thumbnail above a wall
            // of stats with the animation buried in a separate box further down.
            iconRt.sizeDelta = new Vector2(150f, 150f);
            var iconImg = iconGo.AddComponent<RawImage>();
            string heroArt = ThemeLocale.ArtId(troopId);
            iconImg.texture = NeonArt.Unit(heroArt, true);
            iconImg.color = t.Accent;
            var heroAnim   = iconGo.AddComponent<TroopPoseAnimator>();
            heroAnim.Target   = iconImg;
            heroAnim.ArtId    = heroArt;
            heroAnim.IsPlayer = true;

            // Glowing backdrop behind icon
            var glowGo = new GameObject("glow"); glowGo.transform.SetParent(iconGo.transform, false);
            glowGo.transform.SetAsFirstSibling();
            var glowRt = glowGo.AddComponent<RectTransform>();
            glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-12f, -12f); glowRt.offsetMax = new Vector2(12f, 12f);
            var glowImg = glowGo.AddComponent<Image>();
            glowImg.color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.18f);

            // ── troop name ──────────────────────────────────────────────────────
            var nameGo = new GameObject("name"); nameGo.transform.SetParent(content, false);
            var nameRt = nameGo.AddComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 1f); nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.pivot = new Vector2(0.5f, 1f);
            nameRt.anchoredPosition = new Vector2(0f, -158f);
            nameRt.sizeDelta = new Vector2(0f, 30f);
            var nameTxt = nameGo.AddComponent<Text>();
            nameTxt.font = _font; nameTxt.fontSize = 30; nameTxt.fontStyle = FontStyle.Bold;
            nameTxt.color = Color.white; nameTxt.alignment = TextAnchor.MiddleCenter;
            nameTxt.text = troopName.ToUpper(); nameTxt.supportRichText = false; nameTxt.raycastTarget = false;
            var nameSh = nameGo.AddComponent<Shadow>();
            nameSh.effectColor = new Color(0f, 0f, 0f, 0.65f);
            nameSh.effectDistance = new Vector2(2f, -2f);

            // Separator
            var sep1Go = new GameObject("sep"); sep1Go.transform.SetParent(content, false);
            var sep1Rt = sep1Go.AddComponent<RectTransform>();
            sep1Rt.anchorMin = new Vector2(0f, 1f); sep1Rt.anchorMax = new Vector2(1f, 1f);
            sep1Rt.pivot = new Vector2(0.5f, 1f);
            sep1Rt.anchoredPosition = new Vector2(0f, -196f);
            sep1Rt.sizeDelta = new Vector2(0f, 1f);
            sep1Go.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.35f);

            if (TroopStats.TryGet(troopId, out var info))
            {
                float aps = TroopStats.APS(info);

                // ── stats row ───────────────────────────────────────────────────
                string[] statLabels = { "HP", "SPEED", "APS", "RANGE" };
                string[] statValues = {
                    $"{info.MaxHp:0}",
                    $"{info.Speed:0.#}",
                    $"{aps:0.##}/s",
                    TroopStats.RangeLabel(info.Range)
                };
                Color[] statColors = {
                    new Color(0.25f, 1f, 0.45f),
                    new Color(0.4f, 0.8f, 1f),
                    new Color(1f, 0.75f, 0.2f),
                    new Color(1f, 0.45f, 0.65f)
                };

                for (int si = 0; si < 4; si++)
                {
                    float sx = -0.5f * 3f * (148f + 4f) + si * (148f + 4f) + 74f;  // centered 4-col layout
                    // Actually use anchors for even spacing
                    var statGo = new GameObject($"stat{si}"); statGo.transform.SetParent(content, false);
                    var statRt = statGo.AddComponent<RectTransform>();
                    statRt.anchorMin = new Vector2(si / 4f, 1f);
                    statRt.anchorMax = new Vector2((si + 1) / 4f, 1f);
                    statRt.pivot = new Vector2(0.5f, 1f);
                    statRt.anchoredPosition = new Vector2(0f, -206f);
                    statRt.sizeDelta = new Vector2(-6f, 66f);
                    statGo.AddComponent<Image>().color = new Color(0.07f, 0.10f, 0.16f, 1f);

                    var valGo = new GameObject("val"); valGo.transform.SetParent(statGo.transform, false);
                    var valRt = valGo.AddComponent<RectTransform>();
                    valRt.anchorMin = new Vector2(0f, 0.5f); valRt.anchorMax = Vector2.one;
                    valRt.offsetMin = valRt.offsetMax = Vector2.zero;
                    var valTxt = valGo.AddComponent<Text>();
                    valTxt.font = _font; valTxt.fontSize = 29; valTxt.fontStyle = FontStyle.Bold;
                    valTxt.color = statColors[si]; valTxt.alignment = TextAnchor.MiddleCenter;
                    valTxt.text = statValues[si]; valTxt.supportRichText = false; valTxt.raycastTarget = false;

                    var lblGo = new GameObject("lbl"); lblGo.transform.SetParent(statGo.transform, false);
                    var lblRt = lblGo.AddComponent<RectTransform>();
                    lblRt.anchorMin = Vector2.zero; lblRt.anchorMax = new Vector2(1f, 0.5f);
                    lblRt.offsetMin = lblRt.offsetMax = Vector2.zero;
                    var lblTxt = lblGo.AddComponent<Text>();
                    lblTxt.font = _font; lblTxt.fontSize = 13; lblTxt.fontStyle = FontStyle.Bold;
                    lblTxt.color = new Color(0.64f, 0.72f, 0.84f); lblTxt.alignment = TextAnchor.MiddleCenter;
                    lblTxt.text = statLabels[si]; lblTxt.supportRichText = false; lblTxt.raycastTarget = false;
                }

                // ── attack type badge ────────────────────────────────────────────
                var badgeGo = new GameObject("badge"); badgeGo.transform.SetParent(content, false);
                var badgeRt = badgeGo.AddComponent<RectTransform>();
                badgeRt.anchorMin = new Vector2(0.5f, 1f); badgeRt.anchorMax = new Vector2(0.5f, 1f);
                badgeRt.pivot = new Vector2(0.5f, 1f);
                badgeRt.anchoredPosition = new Vector2(0f, -282f);
                badgeRt.sizeDelta = new Vector2(300f, 30f);
                badgeGo.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.11f, 1f);
                var badgeTxtGo = new GameObject("txt"); badgeTxtGo.transform.SetParent(badgeGo.transform, false);
                var badgeTxtRt = badgeTxtGo.AddComponent<RectTransform>();
                badgeTxtRt.anchorMin = Vector2.zero; badgeTxtRt.anchorMax = Vector2.one;
                badgeTxtRt.offsetMin = badgeTxtRt.offsetMax = Vector2.zero;
                var badgeTxt = badgeTxtGo.AddComponent<Text>();
                badgeTxt.font = _font; badgeTxt.fontSize = 21; badgeTxt.fontStyle = FontStyle.Bold;
                badgeTxt.color = t.Accent; badgeTxt.alignment = TextAnchor.MiddleCenter;
                badgeTxt.text = info.AttackType; badgeTxt.supportRichText = false; badgeTxt.raycastTarget = false;

                // ── animation description ────────────────────────────────────────
                var animLblGo = new GameObject("animlbl"); animLblGo.transform.SetParent(content, false);
                var animLblRt = animLblGo.AddComponent<RectTransform>();
                animLblRt.anchorMin = new Vector2(0f, 1f); animLblRt.anchorMax = new Vector2(1f, 1f);
                animLblRt.pivot = new Vector2(0.5f, 1f);
                animLblRt.anchoredPosition = new Vector2(0f, -320f);
                animLblRt.sizeDelta = new Vector2(0f, 14f);
                var animLblTxt = animLblGo.AddComponent<Text>();
                animLblTxt.font = _font; animLblTxt.fontSize = 14; animLblTxt.fontStyle = FontStyle.Bold;
                animLblTxt.color = new Color(0.68f, 0.76f, 0.90f); animLblTxt.alignment = TextAnchor.MiddleCenter;
                animLblTxt.text = "ATTACK ANIMATION"; animLblTxt.supportRichText = false; animLblTxt.raycastTarget = false;

                var animGo = new GameObject("anim"); animGo.transform.SetParent(content, false);
                var animRt = animGo.AddComponent<RectTransform>();
                animRt.anchorMin = new Vector2(0f, 1f); animRt.anchorMax = new Vector2(1f, 1f);
                animRt.pivot = new Vector2(0.5f, 1f);
                animRt.anchoredPosition = new Vector2(0f, -340f);
                animRt.sizeDelta = new Vector2(0f, 74f);
                var animTxt = animGo.AddComponent<Text>();
                animTxt.font = _font; animTxt.fontSize = 20;
                animTxt.color = new Color(0.90f, 0.94f, 1.00f, 1f);
                animTxt.alignment = TextAnchor.UpperCenter;
                animTxt.text = TooltipSystem.TroopAttackAnim(troopId); animTxt.supportRichText = false; animTxt.raycastTarget = false;
                animTxt.lineSpacing = 1.2f;

                // Separator 2
                var sep2Go = new GameObject("sep2"); sep2Go.transform.SetParent(content, false);
                var sep2Rt = sep2Go.AddComponent<RectTransform>();
                sep2Rt.anchorMin = new Vector2(0f, 1f); sep2Rt.anchorMax = new Vector2(1f, 1f);
                sep2Rt.pivot = new Vector2(0.5f, 1f);
                sep2Rt.anchoredPosition = new Vector2(0f, -416f);
                sep2Rt.sizeDelta = new Vector2(0f, 1f);
                sep2Go.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.20f);

                // ── flavor / tactical description ────────────────────────────────
                var descGo = new GameObject("desc"); descGo.transform.SetParent(content, false);
                var descRt = descGo.AddComponent<RectTransform>();
                descRt.anchorMin = new Vector2(0f, 1f); descRt.anchorMax = new Vector2(1f, 1f);
                descRt.pivot = new Vector2(0.5f, 1f);
                descRt.anchoredPosition = new Vector2(0f, -426f);
                descRt.sizeDelta = new Vector2(0f, 150f);
                var descTxt = descGo.AddComponent<Text>();
                descTxt.font = _font; descTxt.fontSize = 21;
                descTxt.color = new Color(0.86f, 0.91f, 0.98f); descTxt.alignment = TextAnchor.UpperCenter;
                // TooltipSystem already carries per-theme copy; the modal was still printing the
                // science-fiction blurb over medieval art ("swept delta wings" on a bat-winged Rogue).
                descTxt.text = TooltipSystem.TroopTooltip(troopId).body;
                descTxt.supportRichText = false; descTxt.raycastTarget = false;
                descTxt.lineSpacing = 1.25f;

                // ── combat demo ───────────────────────────────────────────────────
                var sep3Go = new GameObject("sep3"); sep3Go.transform.SetParent(content, false);
                var sep3Rt = sep3Go.AddComponent<RectTransform>();
                sep3Rt.anchorMin = new Vector2(0f, 1f); sep3Rt.anchorMax = new Vector2(1f, 1f);
                sep3Rt.pivot = new Vector2(0.5f, 1f);
                sep3Rt.anchoredPosition = new Vector2(0f, -588f);
                sep3Rt.sizeDelta = new Vector2(0f, 1f);
                sep3Go.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.20f);

                var demoLblGo = new GameObject("demolbl"); demoLblGo.transform.SetParent(content, false);
                var demoLblRt = demoLblGo.AddComponent<RectTransform>();
                demoLblRt.anchorMin = new Vector2(0f, 1f); demoLblRt.anchorMax = new Vector2(1f, 1f);
                demoLblRt.pivot = new Vector2(0.5f, 1f);
                demoLblRt.anchoredPosition = new Vector2(0f, -618f);
                demoLblRt.sizeDelta = new Vector2(0f, 14f);
                var demoLblTxt = demoLblGo.AddComponent<Text>();
                demoLblTxt.font = _font; demoLblTxt.fontSize = 11; demoLblTxt.fontStyle = FontStyle.Bold;
                demoLblTxt.color = new Color(0.50f, 0.60f, 0.75f); demoLblTxt.alignment = TextAnchor.MiddleCenter;
                demoLblTxt.text = "COMBAT DEMO"; demoLblTxt.supportRichText = false; demoLblTxt.raycastTarget = false;

                var arenaGo = new GameObject("demoArena"); arenaGo.transform.SetParent(content, false);
                var arenaRt = arenaGo.AddComponent<RectTransform>();
                arenaRt.anchorMin = new Vector2(0f, 1f); arenaRt.anchorMax = new Vector2(1f, 1f);
                arenaRt.pivot = new Vector2(0.5f, 1f);
                arenaRt.anchoredPosition = new Vector2(0f, -618f);
                arenaRt.sizeDelta = new Vector2(0f, 190f);   // room for a full-size troop + HP bar
                arenaGo.AddComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.80f);

                _demoLeftId   = troopId;
                _demoRightIdx = 1; // trooper
                _demoArena    = arenaGo.transform;
                RebuildDemoArena();
            }

            Tween.PopIn(_troopModal.transform, 0.18f);
        }

        void RebuildDemoArena()
        {
            if (_demoArena == null) return;
            if (_demoCoroutine != null) { StopCoroutine(_demoCoroutine); _demoCoroutine = null; }
            foreach (Transform child in _demoArena) Destroy(child.gameObject);

            var t = NeonTheme.Active;
            string rightId   = _troopIdList[_demoRightIdx];
            string rightName = ThemeLocale.TroopName(rightId).ToUpper();

            // The lane itself, drawn first so everything else sits on it.
            BuildDemoLane(_demoArena, t);

            MakeDemoSlot(_demoArena, t, true, _demoLeftId,
                ThemeLocale.TroopName(_demoLeftId).ToUpper(),
                out var lFlash, out var lHpFill, out var lMaxHp, out var lSpr);
            MakeDemoSlot(_demoArena, t, false, rightId, rightName,
                out var rFlash, out var rHpFill, out var rMaxHp, out var rSpr);

            // Matchup switcher lives ABOVE the lane now — it used to sit in the middle of the
            // fight and physically block the space the two units close into.
            var hdr = new GameObject("switchRow"); hdr.transform.SetParent(_demoArena, false);
            var hRt = hdr.AddComponent<RectTransform>();
            hRt.anchorMin = new Vector2(0.30f, 1f); hRt.anchorMax = new Vector2(0.70f, 1f);
            hRt.pivot = new Vector2(0.5f, 0f);
            hRt.anchoredPosition = new Vector2(0f, 2f);
            hRt.sizeDelta = new Vector2(0f, 22f);

            var vsGo = new GameObject("vs"); vsGo.transform.SetParent(hdr.transform, false);
            var vsRt = vsGo.AddComponent<RectTransform>();
            vsRt.anchorMin = new Vector2(0.28f, 0f); vsRt.anchorMax = new Vector2(0.72f, 1f);
            vsRt.offsetMin = vsRt.offsetMax = Vector2.zero;
            var vsTxt = vsGo.AddComponent<Text>();
            vsTxt.font = _font; vsTxt.fontSize = 16; vsTxt.fontStyle = FontStyle.Bold;
            vsTxt.color = new Color(1f, 0.84f, 0.30f, 1f);
            vsTxt.alignment = TextAnchor.MiddleCenter;
            vsTxt.resizeTextForBestFit = true;
            vsTxt.resizeTextMinSize = 11; vsTxt.resizeTextMaxSize = 16;
            // Say why the fight is one-sided when it is, rather than letting it look like a bug.
            bool demoAirGap = (DemoIsAir(_demoLeftId) && !DemoTargetsAir(rightId))
                           || (DemoIsAir(rightId)     && !DemoTargetsAir(_demoLeftId));
            vsTxt.text = demoAirGap
                ? "VS  " + rightName + "   ·   CANNOT REACH AIR"
                : "VS  " + rightName; vsTxt.supportRichText = false; vsTxt.raycastTarget = false;

            MakeDemoArrow(hdr.transform, -1);
            MakeDemoArrow(hdr.transform, +1);

            _demoCoroutine = StartCoroutine(TroopDemoLoop(
                _demoLeftId, lFlash, lHpFill, lMaxHp, lSpr,
                rightId, rFlash, rHpFill, rMaxHp, rSpr));
        }

        void MakeDemoArrow(Transform parent, int dir)
        {
            var go = new GameObject(dir < 0 ? "arrL" : "arrR");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = dir < 0 ? new Vector2(0f,    0.05f) : new Vector2(0.76f, 0.05f);
            rt.anchorMax = dir < 0 ? new Vector2(0.24f, 0.95f) : new Vector2(1f,    0.95f);
            rt.offsetMin = new Vector2(2f, 0f); rt.offsetMax = new Vector2(-2f, 0f);
            go.AddComponent<Image>().color = new Color(0.10f, 0.14f, 0.20f, 0.90f);

            var tGo = new GameObject("t"); tGo.transform.SetParent(go.transform, false);
            var tRt = tGo.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;
            var tTxt = tGo.AddComponent<Text>();
            tTxt.font = _font; tTxt.fontSize = 11; tTxt.fontStyle = FontStyle.Bold;
            tTxt.color = NeonTheme.Active.Accent; tTxt.alignment = TextAnchor.MiddleCenter;
            tTxt.text = dir < 0 ? "◄" : "►"; tTxt.supportRichText = false; tTxt.raycastTarget = false;

            var btn = go.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => {
                _demoRightIdx = (_demoRightIdx + dir + _troopIdList.Length) % _troopIdList.Length;
                RebuildDemoArena();
            });
            go.AddComponent<ButtonFeel>();
        }

        /// <summary>
        /// One combatant on the demo LANE. Unlike the old two-slot widget these are free to move
        /// across the whole arena, are drawn at battlefield proportion, and render with their own
        /// team's livery — the enemy is no longer painted as an ally.
        /// </summary>
        void MakeDemoSlot(Transform parent, NeonTheme.Theme t,
            bool isPlayer, string troopId, string displayName,
            out Graphic flashImg, out Image hpFill, out float maxHp, out RectTransform sprRtOut)
        {
            maxHp = TroopStats.TryGet(troopId, out var info) ? info.MaxHp : 100f;

            var slotGo = new GameObject($"lane_{troopId}"); slotGo.transform.SetParent(parent, false);
            var slotRt = slotGo.AddComponent<RectTransform>();
            slotRt.anchorMin = new Vector2(0f, 0f); slotRt.anchorMax = new Vector2(0f, 0f);
            slotRt.pivot = new Vector2(0.5f, 0f);
            slotRt.sizeDelta = new Vector2(DemoUnitPx, DemoUnitPx);

            var sprGo = new GameObject("spr"); sprGo.transform.SetParent(slotGo.transform, false);
            var sprRt = sprGo.AddComponent<RectTransform>();
            sprRt.anchorMin = Vector2.zero; sprRt.anchorMax = Vector2.one;
            sprRt.offsetMin = sprRt.offsetMax = Vector2.zero;
            if (!isPlayer) sprRt.localScale = new Vector3(-1f, 1f, 1f);   // enemy faces left
            sprRtOut = slotRt;                                            // the LANE mover
            var sprImg = sprGo.AddComponent<RawImage>();
            sprImg.texture = NeonArt.Unit(ThemeLocale.ArtId(troopId), isPlayer);
            sprImg.color = Color.white;                                   // livery lives in the art now
            sprImg.raycastTarget = false;

            // HP bar above the unit, battlefield style
            var hpBgGo = new GameObject("hpBg"); hpBgGo.transform.SetParent(slotGo.transform, false);
            var hpBgRt = hpBgGo.AddComponent<RectTransform>();
            hpBgRt.anchorMin = new Vector2(0.5f, 1f); hpBgRt.anchorMax = new Vector2(0.5f, 1f);
            hpBgRt.pivot = new Vector2(0.5f, 0f);
            hpBgRt.anchoredPosition = new Vector2(0f, -6f);
            hpBgRt.sizeDelta = new Vector2(DemoUnitPx * 0.52f, 7f);
            hpBgGo.AddComponent<Image>().color = new Color(0.03f, 0.04f, 0.06f, 0.95f);

            var hpFillGo = new GameObject("fill"); hpFillGo.transform.SetParent(hpBgGo.transform, false);
            var hpFillRt = hpFillGo.AddComponent<RectTransform>();
            hpFillRt.anchorMin = Vector2.zero; hpFillRt.anchorMax = Vector2.one;
            hpFillRt.offsetMin = new Vector2(1f, 1f); hpFillRt.offsetMax = new Vector2(-1f, -1f);
            hpFill = hpFillGo.AddComponent<Image>();
            hpFill.color = isPlayer ? new Color(0.20f, 0.95f, 0.28f) : new Color(0.62f, 0.32f, 0.98f);

            var nmGo = new GameObject("nm"); nmGo.transform.SetParent(slotGo.transform, false);
            var nmRt = nmGo.AddComponent<RectTransform>();
            nmRt.anchorMin = new Vector2(0.5f, 0f); nmRt.anchorMax = new Vector2(0.5f, 0f);
            nmRt.pivot = new Vector2(0.5f, 1f);
            nmRt.anchoredPosition = new Vector2(0f, -2f);
            nmRt.sizeDelta = new Vector2(120f, 13f);
            var nmTxt = nmGo.AddComponent<Text>();
            nmTxt.font = _font; nmTxt.fontSize = 11; nmTxt.fontStyle = FontStyle.Bold;
            nmTxt.color = isPlayer ? new Color(0.62f, 0.80f, 1f) : new Color(1f, 0.58f, 0.44f);
            nmTxt.alignment = TextAnchor.MiddleCenter;
            nmTxt.text = displayName; nmTxt.supportRichText = false; nmTxt.raycastTarget = false;

            var flashGo = new GameObject("flash"); flashGo.transform.SetParent(sprGo.transform, false);
            var flashRt = flashGo.AddComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero; flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;
            var flashRaw = flashGo.AddComponent<RawImage>();
            flashRaw.texture = sprImg.texture;
            flashRaw.color = Color.clear;
            flashRaw.raycastTarget = false;
            flashImg = flashRaw;
        }

        const float DemoUnitPx = 118f;   // battlefield proportion, not a thumbnail

        /// <summary>Lane furniture: cores at each end, floor, captured strips, pylon node.</summary>
        void BuildDemoLane(Transform parent, NeonTheme.Theme t)
        {
            Image Strip(string nm, Vector2 aMin, Vector2 aMax, Color col)
            {
                var go = new GameObject(nm); go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = aMin; rt.anchorMax = aMax;
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var img = go.AddComponent<Image>(); img.color = col; img.raycastTarget = false;
                return img;
            }
            // The demo already drew cores, territory and a pylon, so it was closer to the
            // battlefield than it looked. What it lacked was the lane BACKGROUND and the deploy
            // pads, and its spawn points were hand-picked numbers rather than the sim's -- so
            // troops appeared somewhere the real game would never put them.
            //
            // BuildGroundLane paints the lane (0.05, 0.06, 0.09) and the floor strip over it;
            // matching those exactly is what makes this read as a slice of the real thing.
            Strip("laneBg", new Vector2(0.045f, 0f), new Vector2(0.955f, 1f),
                  new Color(0.05f, 0.06f, 0.09f, 1f));
            Strip("floor", new Vector2(0.045f, 0f), new Vector2(0.955f, 0.20f),
                  new Color(0.055f, 0.085f, 0.14f, 1f));
            Strip("floorLine", new Vector2(0.045f, 0.198f), new Vector2(0.955f, 0.205f),
                  new Color(0.23f, 0.82f, 1f, 0.22f));
            // captured ground, both sides
            Strip("capP", new Vector2(0.045f, 0.20f), new Vector2(0.33f, 0.222f), new Color(0.35f, 0.63f, 1f, 0.32f));
            Strip("capE", new Vector2(0.67f, 0.20f), new Vector2(0.955f, 0.222f), new Color(1f, 0.42f, 0.24f, 0.32f));
            // cores
            Strip("coreP", new Vector2(0f, 0.10f), new Vector2(0.045f, 0.78f), new Color(0.35f, 0.63f, 1f, 0.80f));
            Strip("coreE", new Vector2(0.955f, 0.10f), new Vector2(1f, 0.78f), new Color(1f, 0.42f, 0.24f, 0.80f));
            // pylon node mid-lane
            Strip("pylon", new Vector2(0.494f, 0.20f), new Vector2(0.506f, 0.30f), new Color(0.62f, 0.70f, 0.82f, 0.45f));

            // Deploy pads, at the SAME fraction of the lane the sim spawns at, so a troop in
            // the demo stands exactly where it would stand in a real match.
            float padX = CombatSim.DeploySpawnX / CombatSim.LaneLength;
            void Pad(float nx, Color c)
            {
                Strip("pad", new Vector2(nx - 0.032f, 0.205f), new Vector2(nx + 0.032f, 0.245f),
                      new Color(c.r, c.g, c.b, 0.30f));
                Strip("padL", new Vector2(nx - 0.034f, 0.20f), new Vector2(nx - 0.028f, 0.27f),
                      new Color(c.r, c.g, c.b, 0.55f));
                Strip("padR", new Vector2(nx + 0.028f, 0.20f), new Vector2(nx + 0.034f, 0.27f),
                      new Color(c.r, c.g, c.b, 0.55f));
            }
            Pad(0.045f + padX * 0.91f,          t.TroopTint);
            Pad(0.955f - padX * 0.91f,          t.EnemyTint);
        }

        /// <summary>
        /// Swap a lane combatant to a pose frame. The mover is the slot rect; the artwork lives
        /// on a child "spr" with a "flash" overlay inside it that shares the same texture, so
        /// both have to change together or the tint ghosts the previous frame.
        /// </summary>
        static void DemoPose(RectTransform slotRt, string artId, int pose, bool isPlayer)
        {
            if (slotRt == null) return;
            var sprT = slotRt.Find("spr");
            if (sprT == null) return;
            var tex = NeonArt.Unit(artId, isPlayer, pose);
            var main = sprT.GetComponent<RawImage>();
            if (main != null) main.texture = tex;
            var fl = sprT.Find("flash");
            if (fl != null)
            {
                var f = fl.GetComponent<RawImage>();
                if (f != null) f.texture = tex;
            }
        }

        /// <summary>Move a lane combatant to a normalised lane position.</summary>
        static void DemoPlace(RectTransform slotRt, float nx, float ground, float lift)
        {
            if (slotRt == null) return;
            slotRt.anchorMin = new Vector2(nx, ground);
            slotRt.anchorMax = new Vector2(nx, ground);
            slotRt.anchoredPosition = new Vector2(0f, lift);
        }

        /// <summary>
        /// Runs the preview as a miniature LANE rather than two portraits trading numbers:
        /// both troops march in from their own side with the walk cycle running, meet, trade
        /// real attack and flinch poses while HP drains, the loser topples, and it resets.
        /// Same pose frames and the same order of events as the battlefield.
        /// </summary>
        /// <summary>True for units that fight from the air lane rather than the ground.</summary>
        // Was a hardcoded string test. IsAir is a real field on UnitSpec, and hardcoding it
        // here meant any future air unit would silently fight on the ground in the demo.
        static bool DemoIsAir(string canonicalId)
            => UnitCatalog.Get(canonicalId)?.IsAir ?? (canonicalId == "interceptor");

        /// <summary>
        /// Mirrors CombatSim: a target in the air can only be hit by an attacker with
        /// TargetsAir, and in the runtime specs that is the interceptor and the turret alone.
        /// Every other unit has TargetsAir = false and literally cannot damage a flier.
        /// </summary>
        // Same story as DemoIsAir: TargetsAir is a real UnitSpec field, and duplicating it
        // as a string test here meant the demo and the battle could disagree about who can
        // even reach an air unit.
        static bool DemoTargetsAir(string canonicalId)
            => UnitCatalog.Get(canonicalId)?.TargetsAir
               ?? (canonicalId == "interceptor" || canonicalId == "turret");

        /// <summary>
        /// The demo lane now honours HOW a unit fights, not just that it fights. A melee troop
        /// closes to contact; a ranged troop holds its stand-off and looses a projectile; an air
        /// troop hovers above the lane and strikes down at an angle instead of brawling on the
        /// floor. Showing a Rogue toe-to-toe with a Knight misrepresented the whole unit.
        /// </summary>
        IEnumerator TroopDemoLoop(
            string leftId,  Graphic leftFlash,  Image leftHpFill,  float leftMaxHp,  RectTransform leftSpr,
            string rightId, Graphic rightFlash, Image rightHpFill, float rightMaxHp, RectTransform rightSpr)
        {
            TroopStats.TryGet(leftId, out var leftInfo);
            TroopStats.TryGet(rightId, out var rightInfo);
            string lArt = ThemeLocale.ArtId(leftId), rArt = ThemeLocale.ArtId(rightId);

            float leftCd  = Mathf.Max(0.65f, leftInfo.AttackCooldown);
            float rightCd = Mathf.Max(0.65f, rightInfo.AttackCooldown);
            // Was rightMaxHp/6 and leftMaxHp/6 -- every duel took exactly six hits no matter
            // what the units actually hit for, so a titan and a drone looked equally deadly.
            // Real damage now, from the same catalogue the battle runs on.
            var lSpec = UnitCatalog.Get(leftId);
            var rSpec = UnitCatalog.Get(rightId);
            float leftDmg  = lSpec != null ? lSpec.Damage : Mathf.Max(6f, rightMaxHp / 6f);
            float rightDmg = rSpec != null ? rSpec.Damage : Mathf.Max(6f, leftMaxHp  / 6f);

            // A drone needs a hundred hits to fell a titan. That is TRUE and worth showing --
            // the bar barely moves -- but at real cadence the loop would run for two minutes.
            // So the PACING compresses on a lopsided duel and the damage never does: each hit
            // still removes the real fraction of the bar, which is the part that has to be
            // honest. A fast, weak attacker now reads as fast and weak.
            float lHits = leftDmg  > 0f ? rightMaxHp / leftDmg  : 99f;
            float rHits = rightDmg > 0f ? leftMaxHp  / rightDmg : 99f;
            float demoPace = Mathf.Clamp(10f / Mathf.Max(4f, Mathf.Max(lHits, rHits)), 0.16f, 1f);
            leftCd  = Mathf.Max(0.10f, leftCd  * demoPace);
            rightCd = Mathf.Max(0.10f, rightCd * demoPace);
            var hitOnLeft  = NeonTheme.Active.AccentSecondary;
            var hitOnRight = NeonTheme.Active.Accent;

            bool lAir = DemoIsAir(leftId), rAir = DemoIsAir(rightId);
            // The demo was letting a Knight trade blows with a Rogue. In the real game a
            // ground unit has TargetsAir = false, so it can never land a hit on the air lane --
            // the Rogue attacks for as long as it likes and takes nothing back. Showing an even
            // melee misrepresented the unit completely.
            bool lCanHit = !rAir || DemoTargetsAir(leftId);
            bool rCanHit = !lAir || DemoTargetsAir(rightId);
            bool lRanged = leftInfo.Range  > 4f || lAir;
            bool rRanged = rightInfo.Range > 4f || rAir;

            // Were 0.10 / 0.90 by eye. Derived from the sim now, so the demo starts its
            // troops on the deploy pads rather than near them.
            float SPAWN_L = 0.045f + (CombatSim.DeploySpawnX / CombatSim.LaneLength) * 0.91f;
            float SPAWN_R = 0.955f - (CombatSim.DeploySpawnX / CombatSim.LaneLength) * 0.91f;
            const float GROUND  = 0.17f, AIRLINE = 0.52f;
            // MARCH was a single constant shared by both sides, so a titan (Speed 1.5) and an
            // interceptor (Speed 6) crossed the lane in exactly the same time. Each side now
            // gets its own march duration from its real Speed, normalised around the trooper
            // (Speed 4) so the demo keeps a watchable pace -- a fast unit arrives first, which
            // is the whole point of the stat.
            float SpeedOf(string cid) => UnitCatalog.Get(cid)?.Speed ?? 4f;
            float MarchTime(string cid) =>
                Mathf.Clamp(1.6f * (4f / Mathf.Max(0.8f, SpeedOf(cid))), 0.75f, 3.0f);
            float marchL = MarchTime(leftId), marchR = MarchTime(rightId);
            float MARCH = Mathf.Max(marchL, marchR);

            float lGround = lAir ? AIRLINE : GROUND;
            float rGround = rAir ? AIRLINE : GROUND;
            // Ranged units hold a stand-off scaled to their reach; melee closes to contact.
            float lStop = lRanged ? Mathf.Lerp(0.40f, 0.24f, Mathf.Clamp01(leftInfo.Range  / 25f)) : 0.415f;
            float rStop = rRanged ? Mathf.Lerp(0.60f, 0.76f, Mathf.Clamp01(rightInfo.Range / 25f)) : 0.585f;

            while (leftFlash != null && rightFlash != null && leftSpr != null && rightSpr != null)
            {
                float leftHp = leftMaxHp, rightHp = rightMaxHp;
                if (leftHpFill  != null) leftHpFill.rectTransform.anchorMax  = Vector2.one;
                if (rightHpFill != null) rightHpFill.rectTransform.anchorMax = Vector2.one;
                leftSpr.localRotation = Quaternion.identity;
                rightSpr.localRotation = Quaternion.identity;
                leftFlash.color = Color.clear; rightFlash.color = Color.clear;

                // advance into position — walkers step, fliers bob
                for (float tm = 0f; tm < MARCH; tm += Time.deltaTime)
                {
                    if (leftSpr == null || rightSpr == null) yield break;
                    float prL = Mathf.Clamp01(tm / marchL);
                    float prR = Mathf.Clamp01(tm / marchR);
                    float gait = tm * 8f;
                    float bobL = lAir ? Mathf.Sin(gait * 0.6f) * 4f : Mathf.Abs(Mathf.Sin(gait)) * 3f;
                    float bobR = rAir ? Mathf.Sin(gait * 0.6f + 1f) * 4f : Mathf.Abs(Mathf.Sin(gait + Mathf.PI)) * 3f;
                    DemoPlace(leftSpr,  Mathf.Lerp(SPAWN_L, lStop, prL), lGround, bobL);
                    DemoPlace(rightSpr, Mathf.Lerp(SPAWN_R, rStop, prR), rGround, bobR);
                    int f = Mathf.Sin(gait) > 0f ? 1 : 2;
                    DemoPose(leftSpr,  lArt, f, true);
                    DemoPose(rightSpr, rArt, f == 1 ? 2 : 1, false);
                    yield return null;
                }
                DemoPlace(leftSpr, lStop, lGround, 0f); DemoPlace(rightSpr, rStop, rGround, 0f);
                DemoPose(leftSpr, lArt, 0, true); DemoPose(rightSpr, rArt, 0, false);

                float lT = leftCd * 0.35f, rT = rightCd * 0.8f, hover = 0f;
                float stale = 0f;
                while (leftHp > 0f && rightHp > 0f)
                {
                    if (!lCanHit && !rCanHit && (stale += Time.deltaTime) > 4f) break;
                    if (leftSpr == null || rightSpr == null) yield break;
                    float dt = Time.deltaTime; hover += dt;
                    if (lCanHit) lT -= dt;   // no windup animation against a target it cannot reach
                    if (rCanHit) rT -= dt;

                    // fliers never stand still
                    if (lAir) DemoPlace(leftSpr,  lStop, lGround, Mathf.Sin(hover * 3.2f) * 4f);
                    if (rAir) DemoPlace(rightSpr, rStop, rGround, Mathf.Sin(hover * 3.2f + 1.4f) * 4f);

                    if (lT <= 0f && lCanHit)
                    {
                        lT = leftCd;
                        rightHp = Mathf.Max(0f, rightHp - leftDmg);
                        if (rightHpFill != null)
                            rightHpFill.rectTransform.anchorMax = new Vector2(rightHp / rightMaxHp, 1f);
                        if (rightFlash != null) StartCoroutine(DemoFlash(rightFlash, hitOnRight));
                        if (lRanged)
                        {
                            StartCoroutine(DemoLoose(lArt, leftSpr, lStop, lGround, true));
                            StartCoroutine(DemoBolt(lStop, lGround, rStop, rGround, true));
                        }
                        else StartCoroutine(DemoStrike(lArt, leftSpr, lStop, +1f, true, lGround));
                        StartCoroutine(DemoFlinch(rArt, rightSpr, rStop, +1f, false, rGround));
                    }
                    if (rT <= 0f && rCanHit)
                    {
                        rT = rightCd;
                        leftHp = Mathf.Max(0f, leftHp - rightDmg);
                        if (leftHpFill != null)
                            leftHpFill.rectTransform.anchorMax = new Vector2(leftHp / leftMaxHp, 1f);
                        if (leftFlash != null) StartCoroutine(DemoFlash(leftFlash, hitOnLeft));
                        if (rRanged)
                        {
                            StartCoroutine(DemoLoose(rArt, rightSpr, rStop, rGround, false));
                            StartCoroutine(DemoBolt(rStop, rGround, lStop, lGround, false));
                        }
                        else StartCoroutine(DemoStrike(rArt, rightSpr, rStop, -1f, false, rGround));
                        StartCoroutine(DemoFlinch(lArt, leftSpr, lStop, -1f, true, lGround));
                    }
                    yield return null;
                }

                var dead = leftHp <= 0f ? leftSpr : rightSpr;
                float tilt = leftHp <= 0f ? -72f : 72f;
                for (float tm = 0f; tm < 0.5f; tm += Time.deltaTime)
                {
                    if (dead == null) break;
                    float pr = Mathf.Clamp01(tm / 0.5f);
                    dead.localRotation = Quaternion.Euler(0f, 0f, tilt * pr * pr);
                    yield return null;
                }
                yield return new WaitForSeconds(0.55f);
            }
        }

        // Ranged attacker: windup then release, WITHOUT closing the distance.
        IEnumerator DemoLoose(string artId, RectTransform spr, float homeNx, float ground, bool isPlayer)
        {
            if (spr == null) yield break;
            DemoPose(spr, artId, 3, isPlayer);
            yield return new WaitForSeconds(0.16f);
            if (spr == null) yield break;
            DemoPose(spr, artId, 4, isPlayer);
            yield return new WaitForSeconds(0.16f);
            if (spr != null) DemoPose(spr, artId, 0, isPlayer);
        }

        // A bolt / arrow / zap crossing the lane between two normalised positions.
        IEnumerator DemoBolt(float fromNx, float fromGround, float toNx, float toGround, bool isPlayer)
        {
            if (_demoArena == null) yield break;
            var go = new GameObject("bolt"); go.transform.SetParent(_demoArena, false);
            var rt = go.AddComponent<RectTransform>();
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(16f, 3.4f);
            var img = go.AddComponent<Image>();
            img.color = isPlayer ? new Color(0.62f, 0.86f, 1f) : new Color(1f, 0.62f, 0.42f);
            img.raycastTarget = false;

            float y0 = fromGround + 0.30f, y1 = toGround + 0.30f;
            float ang = Mathf.Atan2((y1 - y0) * 0.35f, toNx - fromNx) * Mathf.Rad2Deg;
            rt.localRotation = Quaternion.Euler(0f, 0f, ang);

            const float DUR = 0.24f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float k = t / DUR;
                rt.anchorMin = rt.anchorMax = new Vector2(Mathf.Lerp(fromNx, toNx, k), Mathf.Lerp(y0, y1, k));
                rt.anchoredPosition = Vector2.zero;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // Attacker: windup pose, drive down the lane, strike pose, settle back to the meeting
        // point. Positions are normalised lane coordinates now, not pixels in a slot.
        IEnumerator DemoStrike(string artId, RectTransform spr, float homeNx, float dir,
            bool isPlayer, float ground)
        {
            if (spr == null) yield break;
            const float PULL = 0.018f, PUSH = 0.034f;
            DemoPose(spr, artId, 3, isPlayer);
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                if (spr == null) yield break;
                DemoPlace(spr, homeNx - dir * PULL * (t / 0.14f), ground, 0f);
                yield return null;
            }
            DemoPose(spr, artId, 4, isPlayer);
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                if (spr == null) yield break;
                DemoPlace(spr, homeNx + dir * PUSH * (t / 0.15f), ground, 0f);
                yield return null;
            }
            for (float t = 0f; t < 0.20f; t += Time.deltaTime)
            {
                if (spr == null) yield break;
                DemoPlace(spr, homeNx + dir * PUSH * (1f - t / 0.20f), ground, 0f);
                yield return null;
            }
            if (spr != null) { DemoPlace(spr, homeNx, ground, 0f); DemoPose(spr, artId, 0, isPlayer); }
        }

        // Defender: flinch frame held through a short knockback away from the blow.
        IEnumerator DemoFlinch(string artId, RectTransform spr, float homeNx, float dir,
            bool isPlayer, float ground)
        {
            if (spr == null) yield break;
            const float KNOCK = 0.022f;
            DemoPose(spr, artId, 5, isPlayer);
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                if (spr == null) yield break;
                DemoPlace(spr, homeNx + dir * KNOCK * (1f - t / 0.22f), ground, 0f);
                yield return null;
            }
            if (spr != null) { DemoPlace(spr, homeNx, ground, 0f); DemoPose(spr, artId, 0, isPlayer); }
        }

        IEnumerator DemoFlash(Graphic flash, Color col)
        {
            if (flash == null) yield break;
            flash.color = col;
            float elapsed = 0f, dur = 0.30f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                if (flash == null) yield break;
                flash.color = Color.Lerp(col, new Color(col.r, col.g, col.b, 0f), elapsed / dur);
                yield return null;
            }
            if (flash != null) flash.color = Color.clear;
        }

        IEnumerator AnimPos(RectTransform rt, Vector2 target, float dur)
        {
            if (rt == null) yield break;
            Vector2 start = rt.anchoredPosition;
            float elapsed = 0f;
            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                if (rt == null) yield break;
                rt.anchoredPosition = Vector2.Lerp(start, target, Mathf.Clamp01(elapsed / dur));
                yield return null;
            }
            if (rt != null) rt.anchoredPosition = target;
        }

        // Per-troop attack animations for the demo arena.
        // xDir = +1 for left attacker (moves right/toward enemy), -1 for right attacker.
        // Melee troops physically collide (big lunge + bounce). Ranged troops spawn a projectile.
        IEnumerator DemoAttack(string troopId, RectTransform spr, float xDir, Graphic myFlash, Graphic targetFlash)
        {
            if (spr == null) yield break;
            var home   = new Vector2(0f, -4f);
            var hitCol = new Color(1f, 0.28f, 0.08f, 0.92f);

            switch (troopId)
            {
                case "drone":
                    // Dive-bomb: whole body flies diagonally into the enemy, explodes, recoils up
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 52f, -28f), 0.11f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, new Color(1f, 0.7f, 0.12f, 1f)));
                    StartCoroutine(DemoProjectile(xDir, new Color(1f, 0.8f, 0.2f, 0.75f), 6f, 220f));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 10f, 22f), 0.08f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.24f));
                    break;

                case "trooper":
                    // Hard fist punch — sprite physically reaches the enemy, bounces back on impact
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 54f, 0f), 0.07f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, hitCol));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 38f, 0f), 0.04f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.22f));
                    break;

                case "sniper":
                    // Scope glow → fast bullet fires → sharp recoil backward (fires from range, never lunges)
                    if (myFlash != null) StartCoroutine(DemoFlash(myFlash, new Color(1f, 1f, 0.35f, 0.70f)));
                    yield return new WaitForSeconds(0.22f);
                    if (spr == null) yield break;
                    StartCoroutine(DemoProjectile(xDir, new Color(1f, 1f, 0.55f, 0.95f), 4f, 800f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, hitCol));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(-xDir * 16f, 0f), 0.05f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.24f));
                    break;

                case "mech":
                    // Cannon arm extends slowly → fires heavy shell → sharp recoil
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 12f, 0f), 0.20f));
                    StartCoroutine(DemoProjectile(xDir, new Color(1f, 0.42f, 0.08f, 0.90f), 8f, 350f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, hitCol));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(-xDir * 14f, 0f), 0.07f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.22f));
                    break;

                case "shield-bot":
                    // Hex shield slam — massive charge, hard collision, bounce back
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 56f, 0f), 0.08f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, new Color(0.55f, 0.78f, 1f, 1f)));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 40f, 0f), 0.05f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.24f));
                    break;

                case "interceptor":
                    // Banking diagonal strafe — fires burst during sweep
                    StartCoroutine(DemoProjectile(xDir, new Color(0.75f, 0.3f, 1f, 0.82f), 5f, 500f));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 24f, -20f), 0.09f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, hitCol));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(-xDir * 10f, 16f), 0.09f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.16f));
                    break;

                case "hacker":
                    // Slow lean in, 3 pulsed purple drain flashes (sustained over time)
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 10f, -6f), 0.22f));
                    for (int p = 0; p < 3 && spr != null; p++)
                    {
                        if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, new Color(0.55f, 0f, 0.90f, 0.85f)));
                        yield return new WaitForSeconds(0.16f);
                    }
                    yield return StartCoroutine(AnimPos(spr, home, 0.22f));
                    break;

                case "titan":
                    // Heavy arm arc slam — physically contacts enemy, massive orange blast, bounces
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 46f, -32f), 0.13f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, new Color(1f, 0.42f, 0f, 1f)));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 30f, -18f), 0.05f));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(0f, 12f), 0.09f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.24f));
                    break;

                case "turret":
                    // Brief tracking nudge → fires projectile → sharp recoil backward
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 6f, 0f), 0.12f));
                    StartCoroutine(DemoProjectile(xDir, new Color(0.25f, 1f, 0.38f, 0.92f), 5f, 600f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, hitCol));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(-xDir * 14f, 0f), 0.05f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.22f));
                    break;

                default:
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 50f, 0f), 0.09f));
                    if (targetFlash != null) StartCoroutine(DemoFlash(targetFlash, hitCol));
                    yield return StartCoroutine(AnimPos(spr, home + new Vector2(xDir * 36f, 0f), 0.04f));
                    yield return StartCoroutine(AnimPos(spr, home, 0.22f));
                    break;
            }
        }

        // Spawns an elongated projectile dot that travels from the attacker's slot to the target's slot.
        // Uses _demoArena rect.width for pixel-accurate positioning.
        IEnumerator DemoProjectile(float xDir, Color col, float size, float pixelsPerSec)
        {
            if (_demoArena == null) yield break;
            var arenaRt = _demoArena.GetComponent<RectTransform>();
            if (arenaRt == null) yield break;

            float w = arenaRt.rect.width;
            float h = arenaRt.rect.height;
            if (w <= 0f) w = 200f;
            if (h <= 0f) h = 118f;

            var go = new GameObject("proj"); go.transform.SetParent(_demoArena, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size * 2.6f, size * 0.55f);
            var img = go.AddComponent<Image>();
            img.color = col; img.raycastTarget = false;

            float startX = xDir > 0f ? -w * 0.29f :  w * 0.29f;
            float endX   = xDir > 0f ?  w * 0.29f : -w * 0.29f;
            float yPos   = -h * 0.36f; // sprite vertical zone

            rt.anchoredPosition = new Vector2(startX, yPos);

            float dist = Mathf.Abs(endX - startX);
            float dur  = Mathf.Max(0.05f, dist / pixelsPerSec);
            float elapsed = 0f;

            while (elapsed < dur)
            {
                elapsed += Time.deltaTime;
                if (go == null) yield break;
                rt.anchoredPosition = new Vector2(Mathf.Lerp(startX, endX, Mathf.Clamp01(elapsed / dur)), yPos);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ─────────────────────────────────────────── interaction ─────────────────

        void SelectLevel(int level)
        {
            _hoveredLevel = level;
            GameSettings.SelectedLevel = level;
            _ladderRows = null;   // standings are per-level; refetch on next RANKS open

            bool unlocked = PlayerProgress.IsLevelUnlocked(level);
            bool canUnlock = PlayerProgress.CanUnlockLevel(level);

            // Update button visuals
            foreach (var (bg, lbl, lv) in _levelBtns)
            {
                bool sel = lv == level;
                bool open = PlayerProgress.IsLevelUnlocked(lv);
                bg.color = sel ? ColSelected : open ? ColUnlocked : ColLocked;
                // border glow
                var brd = bg.transform.GetChild(0).GetComponent<Image>();
                brd.color = sel ? ColBorderOn : ColBorderOff;
            }

            // Detail panel
            int gems = LevelConfig.GemColorCount(level);
            float diff = LevelConfig.DirectorMult(level);

            _detailName.text = $"LEVEL {level:D2} — {LevelConfig.LevelName(level)}";
            Tween.FadeImg(_detailName, 0.25f, 1f, 0.14f, Tween.Ease.EaseOut);

            // Gem swatches: active = theme color, inactive = dimmed
            for (int i = 0; i < _gemIcons.Length; i++)
            {
                var gc = GemCol(i);
                _gemIcons[i].color = i < gems ? gc : new Color(gc.r, gc.g, gc.b, 0.15f);
            }

            // Troop mini-cards
            var troops = LevelConfig.AvailableTroops(level);
            for (int i = 0; i < _troopPanels.Length; i++)
            {
                bool show = i < troops.Count;
                _troopPanels[i].SetActive(show);
                if (show)
                {
                    // This loop used to be unguarded: the panel was activated FIRST and the icon
                    // built second, so a single art id that threw left chip 0 showing as a blank
                    // white RawImage, every later chip inactive, and the whole rest of the refresh
                    // -- difficulty, reward, buttons -- never running. One bad texture took out
                    // the screen. Now a failure costs exactly one icon and names itself in the log.
                    string aid = ThemeLocale.ArtId(troops[i]);
                    try
                    {
                        _troopIcons[i].texture = NeonArt.Unit(aid, true);
                        _troopNames[i].text = ThemeLocale.TroopName(troops[i]).Replace(" ", "\n");
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogError($"[NW] troop chip {i} failed to build art id '{aid}' " +
                                       $"(canonical '{troops[i]}', theme {GameSettings.ThemeIndex}): {e}");
                        _troopIcons[i].texture = null;
                        _troopNames[i].text = troops[i];
                    }
                }
            }

            // Difficulty bar — tween from current fill to new value
            float diffFill = Mathf.Clamp01((diff - 0.4f) / 1.65f);
            if (_diffBarFill != null)
            {
                var dfRt = _diffBarFill.GetComponent<RectTransform>();
                float fromFill = dfRt.anchorMax.x;
                Tween.Float(v =>
                {
                    if (dfRt == null) return;
                    var am = dfRt.anchorMax; am.x = v; dfRt.anchorMax = am;
                }, fromFill, diffFill, 0.22f, Tween.Ease.EaseOut);
            }
            int pct = Mathf.RoundToInt(diff * 100f);
            _detailDiff.text = $"DIFFICULTY  {pct}%";

            // Reward
            if (_rewardLbl != null)
                _rewardLbl.text = $"REWARD  +{LevelConfig.BaseTokenReward(level)} TOKENS";

            // Unlock button
            if (!unlocked && canUnlock)
            {
                _unlockBtnGo.SetActive(true);
                _unlockBtnText.text = $"UNLOCK  [{CostStr(LevelConfig.UnlockCosts(level))}]";
            }
            else
            {
                _unlockBtnGo.SetActive(false);
            }

            // Play button text — amber cost visible when locked, not invisible grey
            _playBtnText.text  = unlocked ? "DEPLOY" : "LOCKED";
            _playBtnText.color = unlocked ? new Color(0f, 0.9f, 1f) : new Color(0.55f, 0.55f, 0.58f);

            // Difficulty line doubles as unlock-cost hint when level is locked
            if (!unlocked)
                _detailDiff.text = $"UNLOCK COST  {CostStr(LevelConfig.UnlockCosts(level))}";
        }

        void TryUnlock()
        {
            if (PlayerProgress.TryUnlockLevel(_hoveredLevel))
            {
                RefreshCurrency();
                SelectLevel(_hoveredLevel);
                var theme = NeonTheme.Active;
                foreach (var (bg, lbl, lv) in _levelBtns)
                {
                    bool open = PlayerProgress.IsLevelUnlocked(lv);
                    lbl.color = open ? Color.white : new Color(0.4f, 0.45f, 0.52f);
                    // Also refresh tier label text and color
                    var tierTf = bg.transform.Find("tier");
                    if (tierTf != null)
                    {
                        var tierTxt = tierTf.GetComponent<Text>();
                        if (tierTxt != null)
                        {
                            tierTxt.text  = open ? LevelConfig.LevelName(lv) : "LOCKED";
                            tierTxt.color = open ? theme.TextDim : new Color(1f, 0.62f, 0.08f);
                        }
                    }
                }
            }
        }

        // ─────────────────────────── VS / competitive panel ──────────────────────

        void BuildVsPanel(float cx, float y)
        {
            var t = NeonTheme.Active;

            // [VS] toggle chip — sits at the right of the footer area
            var vsToggleGo = new GameObject("VSToggle"); vsToggleGo.transform.SetParent(transform, false);
            var vsTRt = vsToggleGo.AddComponent<RectTransform>();
            vsTRt.anchorMin = vsTRt.anchorMax = new Vector2(0.5f, 0.5f);
            vsTRt.pivot     = new Vector2(0.5f, 0.5f);
            vsTRt.anchoredPosition = new Vector2(cx + 145f, y + 12f);
            vsTRt.sizeDelta = new Vector2(72f, 26f);
            _vsBtnBg  = vsToggleGo.AddComponent<Image>();
            var vsBrd = new GameObject("brd"); vsBrd.transform.SetParent(vsToggleGo.transform, false);
            vsBrd.transform.SetAsFirstSibling();
            var vsBrdRt = vsBrd.AddComponent<RectTransform>();
            vsBrdRt.anchorMin = Vector2.zero; vsBrdRt.anchorMax = Vector2.one;
            vsBrdRt.offsetMin = new Vector2(-1, -1); vsBrdRt.offsetMax = new Vector2(1, 1);
            vsBrd.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.6f);
            var vsTxtGo = new GameObject("txt"); vsTxtGo.transform.SetParent(vsToggleGo.transform, false);
            var vsTxtRt = vsTxtGo.AddComponent<RectTransform>();
            vsTxtRt.anchorMin = Vector2.zero; vsTxtRt.anchorMax = Vector2.one;
            vsTxtRt.offsetMin = vsTxtRt.offsetMax = Vector2.zero;
            _vsBtnTxt = vsTxtGo.AddComponent<Text>();
            _vsBtnTxt.font = _font; _vsBtnTxt.fontSize = UIScale.FontSmall; _vsBtnTxt.fontStyle = FontStyle.Bold;
            _vsBtnTxt.alignment = TextAnchor.MiddleCenter; _vsBtnTxt.supportRichText = false; _vsBtnTxt.raycastTarget = false;
            var vsBtn = vsToggleGo.AddComponent<Button>(); vsBtn.transition = Selectable.Transition.None;
            vsBtn.onClick.AddListener(ToggleVsMode);
            vsToggleGo.AddComponent<ButtonFeel>();

            // Expandable VS detail panel
            _vsPanel = new GameObject("VSPanel"); _vsPanel.transform.SetParent(transform, false);
            // Was a 340x82 box pinned into the footer, which ran off the top of the screen the
            // moment it opened. It is a centred modal now -- same shape as the settings card and
            // the theme picker, so all three overlays behave identically.
            var vpRt = _vsPanel.AddComponent<RectTransform>();
            vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
            vpRt.offsetMin = vpRt.offsetMax = Vector2.zero;
            var vpScrim = _vsPanel.AddComponent<Image>();
            vpScrim.color = new Color(t.BgDeep.r * 0.4f, t.BgDeep.g * 0.4f, t.BgDeep.b * 0.4f, 0.92f);
            var vpScrimBtn = _vsPanel.AddComponent<Button>();
            vpScrimBtn.transition = Selectable.Transition.None;
            vpScrimBtn.onClick.AddListener(() => _vsPanel.SetActive(false));

            var vsBrdGo = new GameObject("cardbrd"); vsBrdGo.transform.SetParent(_vsPanel.transform, false);
            var vsBrdRt2 = vsBrdGo.AddComponent<RectTransform>();
            vsBrdRt2.anchorMin = vsBrdRt2.anchorMax = new Vector2(0.5f, 0.5f);
            vsBrdRt2.pivot = new Vector2(0.5f, 0.5f);
            vsBrdRt2.anchoredPosition = Vector2.zero;
            vsBrdRt2.sizeDelta = (_portrait ? new Vector2(720f, 420f) : new Vector2(480f, 260f))
                                 + new Vector2(8f, 8f);
            vsBrdGo.AddComponent<Image>().color = t.Accent;

            var vsCard = new GameObject("card"); vsCard.transform.SetParent(_vsPanel.transform, false);
            var vsCardRt = vsCard.AddComponent<RectTransform>();
            vsCardRt.anchorMin = vsCardRt.anchorMax = new Vector2(0.5f, 0.5f);
            vsCardRt.pivot = new Vector2(0.5f, 0.5f);
            vsCardRt.anchoredPosition = Vector2.zero;
            vsCardRt.sizeDelta = _portrait ? new Vector2(720f, 420f) : new Vector2(480f, 260f);
            vsCard.AddComponent<Image>().color = Lift(t.BgCard, 0.16f);
            vsCard.AddComponent<Button>().transition = Selectable.Transition.None; // eat taps

            var vsTitleGo = new GameObject("title"); vsTitleGo.transform.SetParent(vsCard.transform, false);
            var vsTitleRt = vsTitleGo.AddComponent<RectTransform>();
            vsTitleRt.anchorMin = new Vector2(0f, 1f); vsTitleRt.anchorMax = new Vector2(1f, 1f);
            vsTitleRt.pivot = new Vector2(0.5f, 1f);
            vsTitleRt.anchoredPosition = new Vector2(0f, -14f);
            vsTitleRt.sizeDelta = new Vector2(-40f, 52f);
            var vsTitle = vsTitleGo.AddComponent<Text>();
            vsTitle.font = _font; vsTitle.fontSize = _portrait ? 40 : 22;
            vsTitle.color = Color.white; vsTitle.alignment = TextAnchor.MiddleLeft;
            vsTitle.text = "VERSUS"; vsTitle.supportRichText = false; vsTitle.raycastTarget = false;

            var vsClose = new GameObject("close"); vsClose.transform.SetParent(vsCard.transform, false);
            var vsCloseRt = vsClose.AddComponent<RectTransform>();
            vsCloseRt.anchorMin = vsCloseRt.anchorMax = new Vector2(1f, 1f);
            vsCloseRt.pivot = new Vector2(1f, 1f);
            vsCloseRt.anchoredPosition = new Vector2(-12f, -12f);
            vsCloseRt.sizeDelta = new Vector2(_portrait ? 84f : 48f, _portrait ? 84f : 48f);
            vsClose.AddComponent<Image>().color = Lift(t.BgCard, 0.40f);
            var vsCloseBtn = vsClose.AddComponent<Button>();
            vsCloseBtn.transition = Selectable.Transition.None;
            vsCloseBtn.onClick.AddListener(() => _vsPanel.SetActive(false));
            _vsPanel.SetActive(false);   // opened only by the VS button
            var vsX = new GameObject("x"); vsX.transform.SetParent(vsClose.transform, false);
            var vsXRt = vsX.AddComponent<RectTransform>();
            vsXRt.anchorMin = Vector2.zero; vsXRt.anchorMax = Vector2.one;
            vsXRt.offsetMin = vsXRt.offsetMax = Vector2.zero;
            var vsXTxt = vsX.AddComponent<Text>();
            vsXTxt.font = _font; vsXTxt.fontSize = _portrait ? 44 : 26; vsXTxt.color = Color.white;
            vsXTxt.alignment = TextAnchor.MiddleCenter; vsXTxt.text = "X";
            vsXTxt.supportRichText = false; vsXTxt.raycastTarget = false;
            // The old panel background and its 1px border used to live here. When this method
            // became a modal the scrim took over the background, but these were left behind --
            // a SECOND Image on _vsPanel, which Unity refuses to add, so AddComponent returned
            // null and the next line threw. That exception aborted the rest of BuildLayout,
            // which is why the whole lower half of the menu disappeared with it.

            // MMR label (top-left of panel)
            var mmrGo = new GameObject("mmr"); mmrGo.transform.SetParent(vsCard.transform, false);
            var mmrRt = mmrGo.AddComponent<RectTransform>();
            mmrRt.anchorMin = new Vector2(0f, 1f); mmrRt.anchorMax = new Vector2(0.5f, 1f);
            mmrRt.pivot = new Vector2(0f, 1f);
            mmrRt.anchoredPosition = new Vector2(28f, -78f);
            mmrRt.sizeDelta = new Vector2(-40f, 44f);
            _mmrLbl = mmrGo.AddComponent<Text>();
            _mmrLbl.font = _font; _mmrLbl.fontSize = _portrait ? 30 : UIScale.FontSmall;
            _mmrLbl.color = new Color(1f, 0.82f, 0.18f); _mmrLbl.supportRichText = false; _mmrLbl.raycastTarget = false;

            // Status text (top-right)
            var stGo = new GameObject("status"); stGo.transform.SetParent(vsCard.transform, false);
            var stRt = stGo.AddComponent<RectTransform>();
            stRt.anchorMin = new Vector2(0.5f, 1f); stRt.anchorMax = new Vector2(1f, 1f);
            stRt.pivot = new Vector2(1f, 1f);
            stRt.anchoredPosition = new Vector2(-28f, -78f);
            stRt.sizeDelta = new Vector2(-40f, 44f);
            _vsStatusLbl = stGo.AddComponent<Text>();
            _vsStatusLbl.font = _font; _vsStatusLbl.fontSize = _portrait ? 30 : UIScale.FontSmall;
            _vsStatusLbl.color = t.TextMid; _vsStatusLbl.alignment = TextAnchor.MiddleRight;
            _vsStatusLbl.supportRichText = false; _vsStatusLbl.raycastTarget = false;

            // IP input field
            var ipGo = new GameObject("IPField"); ipGo.transform.SetParent(vsCard.transform, false);
            var ipRt = ipGo.AddComponent<RectTransform>();
            ipRt.anchorMin = new Vector2(0.06f, 0.44f); ipRt.anchorMax = new Vector2(0.94f, 0.60f);
            ipRt.offsetMin = new Vector2(8f, 8f); ipRt.offsetMax = new Vector2(-4f, -4f);
            ipGo.AddComponent<Image>().color = new Color(t.BgDeep.r, t.BgDeep.g, t.BgDeep.b, 1f);
            _ipField = ipGo.AddComponent<InputField>();
            _ipField.text = GameSettings.LastPeerIP;
            _ipField.characterLimit = 40;

            var ipBrd = new GameObject("brd"); ipBrd.transform.SetParent(ipGo.transform, false);
            ipBrd.transform.SetAsFirstSibling();
            var ipBrdRt = ipBrd.AddComponent<RectTransform>();
            ipBrdRt.anchorMin = Vector2.zero; ipBrdRt.anchorMax = Vector2.one;
            ipBrdRt.offsetMin = new Vector2(-1, -1); ipBrdRt.offsetMax = new Vector2(1, 1);
            ipBrd.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.35f);

            var ipTxtGo = new GameObject("txt"); ipTxtGo.transform.SetParent(ipGo.transform, false);
            var ipTxtRt = ipTxtGo.AddComponent<RectTransform>();
            ipTxtRt.anchorMin = Vector2.zero; ipTxtRt.anchorMax = Vector2.one;
            ipTxtRt.offsetMin = new Vector2(6, 0); ipTxtRt.offsetMax = new Vector2(-6, 0);
            var ipTxt = ipTxtGo.AddComponent<Text>();
            ipTxt.font = _font; ipTxt.fontSize = _portrait ? 28 : UIScale.FontSmall; ipTxt.color = t.TextBright;
            ipTxt.alignment = TextAnchor.MiddleLeft; ipTxt.supportRichText = false;
            _ipField.textComponent = ipTxt;

            var ipPHGo = new GameObject("ph"); ipPHGo.transform.SetParent(ipGo.transform, false);
            var ipPHRt = ipPHGo.AddComponent<RectTransform>();
            ipPHRt.anchorMin = Vector2.zero; ipPHRt.anchorMax = Vector2.one;
            ipPHRt.offsetMin = new Vector2(6, 0); ipPHRt.offsetMax = new Vector2(-6, 0);
            var ipPHTxt = ipPHGo.AddComponent<Text>();
            ipPHTxt.font = _font; ipPHTxt.fontSize = _portrait ? 28 : UIScale.FontSmall;
            ipPHTxt.color = t.TextDim; ipPHTxt.alignment = TextAnchor.MiddleLeft;
            ipPHTxt.text = "opponent IP…"; ipPHTxt.supportRichText = false; ipPHTxt.raycastTarget = false;
            _ipField.placeholder = ipPHTxt;

            // [HOST] button
            var hostGo = new GameObject("HostBtn"); hostGo.transform.SetParent(vsCard.transform, false);
            var hRt = hostGo.AddComponent<RectTransform>();
            // Was 17% wide and 54% tall -- a sliver. Two equal buttons on one row now.
            hRt.anchorMin = new Vector2(0.06f, 0.07f); hRt.anchorMax = new Vector2(0.48f, 0.37f);
            hRt.offsetMin = new Vector2(4, 0); hRt.offsetMax = new Vector2(-4, 0);
            var hBg = hostGo.AddComponent<Image>(); hBg.color = new Color(0.08f, 0.22f, 0.40f);
            var hBrd2 = new GameObject("brd2"); hBrd2.transform.SetParent(hostGo.transform, false);
            hBrd2.transform.SetAsFirstSibling();
            var hBrd2Rt = hBrd2.AddComponent<RectTransform>();
            hBrd2Rt.anchorMin = Vector2.zero; hBrd2Rt.anchorMax = Vector2.one;
            hBrd2Rt.offsetMin = new Vector2(-1, -1); hBrd2Rt.offsetMax = new Vector2(1, 1);
            hBrd2.AddComponent<Image>().color = new Color(0.30f, 0.72f, 1f, 0.70f);
            var hTxtGo = new GameObject("txt"); hTxtGo.transform.SetParent(hostGo.transform, false);
            var hTxtRt = hTxtGo.AddComponent<RectTransform>();
            hTxtRt.anchorMin = Vector2.zero; hTxtRt.anchorMax = Vector2.one; hTxtRt.offsetMin = hTxtRt.offsetMax = Vector2.zero;
            var hTxt = hTxtGo.AddComponent<Text>();
            hTxt.font = _font; hTxt.fontSize = _portrait ? 30 : UIScale.FontSmall;
            hTxt.fontStyle = FontStyle.Bold;
            hTxt.color = Color.white; hTxt.alignment = TextAnchor.MiddleCenter; hTxt.text = "HOST"; hTxt.supportRichText = false; hTxt.raycastTarget = false;
            var hostBtn = hostGo.AddComponent<Button>(); hostBtn.transition = Selectable.Transition.None;
            hostBtn.onClick.AddListener(OnVsHostClicked);
            hostGo.AddComponent<ButtonFeel>();

            // [JOIN] button
            var joinGo = new GameObject("JoinBtn"); joinGo.transform.SetParent(vsCard.transform, false);
            var jRt = joinGo.AddComponent<RectTransform>();
            jRt.anchorMin = new Vector2(0.52f, 0.07f); jRt.anchorMax = new Vector2(0.94f, 0.37f);
            jRt.offsetMin = new Vector2(4, 0); jRt.offsetMax = new Vector2(-8f, 0);
            var jBg = joinGo.AddComponent<Image>(); jBg.color = new Color(0.06f, 0.20f, 0.10f);
            var jBrd2 = new GameObject("brd2"); jBrd2.transform.SetParent(joinGo.transform, false);
            jBrd2.transform.SetAsFirstSibling();
            var jBrd2Rt = jBrd2.AddComponent<RectTransform>();
            jBrd2Rt.anchorMin = Vector2.zero; jBrd2Rt.anchorMax = Vector2.one;
            jBrd2Rt.offsetMin = new Vector2(-1, -1); jBrd2Rt.offsetMax = new Vector2(1, 1);
            jBrd2.AddComponent<Image>().color = new Color(0.20f, 1f, 0.45f, 0.60f);
            var jTxtGo = new GameObject("txt"); jTxtGo.transform.SetParent(joinGo.transform, false);
            var jTxtRt = jTxtGo.AddComponent<RectTransform>();
            jTxtRt.anchorMin = Vector2.zero; jTxtRt.anchorMax = Vector2.one; jTxtRt.offsetMin = jTxtRt.offsetMax = Vector2.zero;
            var jTxt = jTxtGo.AddComponent<Text>();
            jTxt.font = _font; jTxt.fontSize = _portrait ? 30 : UIScale.FontSmall;
            jTxt.fontStyle = FontStyle.Bold;
            jTxt.color = new Color(0.22f, 1f, 0.48f); jTxt.alignment = TextAnchor.MiddleCenter; jTxt.text = "JOIN"; jTxt.supportRichText = false; jTxt.raycastTarget = false;
            var joinBtn = joinGo.AddComponent<Button>(); joinBtn.transition = Selectable.Transition.None;
            joinBtn.onClick.AddListener(OnVsJoinClicked);
            joinGo.AddComponent<ButtonFeel>();

            RefreshVsPanel();
        }

        void RefreshVsPanel()
        {
            bool on = GameSettings.CompetitiveMode;
            _vsBtnBg.color  = on ? new Color(0.05f, 0.14f, 0.30f) : new Color(0.10f, 0.10f, 0.14f);
            _vsBtnTxt.text  = on ? "▶ VS ON" : "VS OFF";
            _vsBtnTxt.color = on ? new Color(0.30f, 0.72f, 1f) : new Color(0.45f, 0.48f, 0.55f);
            // Was _vsPanel.SetActive(on) -- so the panel appeared on its own whenever
            // CompetitiveMode happened to be saved as true. That was fine while it was an
            // inline strip; a MODAL has to open on a deliberate click and nothing else.
            // This method now only refreshes contents; visibility belongs to the button.
            // Speed picker and the VS panel share the band under DEPLOY — only one at a time.
            if (_speedRoot != null) _speedRoot.SetActive(!on);

            if (on)
            {
                _mmrLbl.text = $"MMR  {PlayerProgress.MMR}";
                var net = NWNet.Inst;
                _vsStatusLbl.text = net != null ? net.StatusText : "Disconnected";
                _vsStatusLbl.color = (net != null && net.IsConnected)
                    ? new Color(0.25f, 1f, 0.45f)
                    : new Color(0.55f, 0.58f, 0.65f);
            }
        }

        void ToggleVsMode()
        {
            GameSettings.CompetitiveMode = !GameSettings.CompetitiveMode;
            if (!GameSettings.CompetitiveMode) NWNet.Inst?.Disconnect();
            RefreshVsPanel();
            // The button is the toggle; turning VS ON also opens the panel so the host/join
            // controls are right there. Turning it off closes it. Dismissing the panel with
            // the X leaves VS in whatever state it was -- closing a sheet is not a decision.
            if (_vsPanel != null)
            {
                _vsPanel.SetActive(GameSettings.CompetitiveMode);
                if (GameSettings.CompetitiveMode) _vsPanel.transform.SetAsLastSibling();
            }
        }

        void OnVsHostClicked()
        {
            EnsureNet();
            NWNet.Inst.StartHost();
            NWNet.Inst.OnConnected    += OnVsConnected;
            NWNet.Inst.OnDisconnected += OnVsDisconnected;
            RefreshVsPanel();
        }

        void OnVsJoinClicked()
        {
            string ip = _ipField != null ? _ipField.text.Trim() : GameSettings.LastPeerIP;
            if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
            GameSettings.LastPeerIP = ip;
            EnsureNet();
            NWNet.Inst.JoinHost(ip);
            NWNet.Inst.OnConnected    += OnVsConnected;
            NWNet.Inst.OnDisconnected += OnVsDisconnected;
            RefreshVsPanel();
        }

        void OnVsConnected()   => RefreshVsPanel();
        void OnVsDisconnected() => RefreshVsPanel();

        static void EnsureNet()
        {
            if (NWNet.Inst == null)
            {
                var go = new GameObject("[NWNet]");
                go.AddComponent<NWNet>();
            }
        }

        void Update()
        {
            // Keep VS status text current while the screen is open
            if (GameSettings.CompetitiveMode && _vsStatusLbl != null && NWNet.Inst != null)
            {
                _vsStatusLbl.text = NWNet.Inst.StatusText;
                _vsStatusLbl.color = NWNet.Inst.IsConnected
                    ? new Color(0.25f, 1f, 0.45f)
                    : new Color(0.55f, 0.58f, 0.65f);
            }
        }

        // ─────────────────────────────────────────── interaction ─────────────────

        /// <summary>Opens the progress / ghost-ladder sheet.</summary>
        void ShowRanksPanel()
        {
            AudioManager.Play(AudioManager.Sfx.Click, 0.7f);
            _infoModal?.SetActive(false);
            if (_ranksModal == null) return;
            _ranksModal.SetActive(true);
            _ranksModal.transform.SetAsLastSibling();
            RefreshRanksPanel();
        }

        /// <summary>Launch the selected level with <paramref name="rec"/> replaying as the
        /// opponent instead of the AI Director. Mirrors <see cref="LaunchBattle"/>'s hand-off.</summary>
        void ChallengeGhost(GhostRecord rec)
        {
            if (rec == null) return;
            AudioManager.Play(AudioManager.Sfx.Click);
            GameSettings.CompetitiveMode = false;   // ghost mode owns the result path, not LAN VS
            BattleScene.PendingGhost   = rec;
            GameSettings.SelectedLevel = rec.level;
            _hoveredLevel              = rec.level;
            if (_ranksModal != null) _ranksModal.SetActive(false);
            gameObject.SetActive(false);
            OnLevelSelected?.Invoke(rec.level);
        }

        void LaunchBattle()
        {
            if (!PlayerProgress.IsLevelUnlocked(_hoveredLevel)) return;
            if (GameSettings.CompetitiveMode && (NWNet.Inst == null || !NWNet.Inst.IsConnected))
            {
                // VS mode is on but no opponent is connected. Dead-ending here silently traps
                // anyone who just wants to play solo (the flag persists across sessions). A VS
                // host/joiner connects via the HOST/JOIN buttons first, so DEPLOY-while-
                // disconnected always means "play solo" — drop VS mode and launch the level.
                GameSettings.CompetitiveMode = false;
                RefreshVsPanel();
            }
            gameObject.SetActive(false);
            OnLevelSelected?.Invoke(_hoveredLevel);
        }

        void ShowInfoModal() => _infoModal?.SetActive(true);

        void BuildInfoModal()
        {
            var t = NeonTheme.Active;
            _infoModal = new GameObject("InfoModal");
            _infoModal.transform.SetParent(transform, false);
            var rt = _infoModal.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            _infoModal.AddComponent<Image>().color = new Color(t.BgDeep.r, t.BgDeep.g, t.BgDeep.b, 0.92f);

            // Panel
            var panelGo = new GameObject("panel"); panelGo.transform.SetParent(_infoModal.transform, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.18f, 0.14f); panelRt.anchorMax = new Vector2(0.82f, 0.90f);
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
            panelGo.AddComponent<Image>().color = new Color(t.BgPanel.r, t.BgPanel.g, t.BgPanel.b, 1f);

            var brd = new GameObject("brd"); brd.transform.SetParent(panelGo.transform, false);
            brd.transform.SetAsFirstSibling();
            var brdRt = brd.AddComponent<RectTransform>();
            brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
            brdRt.offsetMin = new Vector2(-2, -2); brdRt.offsetMax = new Vector2(2, 2);
            brd.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.55f);

            // Title
            var titleGo = new GameObject("title"); titleGo.transform.SetParent(panelGo.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.86f); titleRt.anchorMax = Vector2.one;
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;
            var titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = _font; titleTxt.fontSize = 24; titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.color = t.Accent; titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.text = "◈  HOW TO PLAY  ◈"; titleTxt.supportRichText = false; titleTxt.raycastTarget = false;

            // This was ONE centre-aligned block at fontSize 13 -- about 4.8pt on a phone,
            // the smallest text in the game -- covering seven topics including keyboard
            // shortcuts, in an order that opened with jargon. A first-time player could not
            // read it, and if they could, it did not tell them what to do first.
            //
            // Now: five sections, each a header and a body, left-aligned (centred paragraphs
            // are markedly harder to read), at a size taken from UIScale rather than a
            // literal. Keyboard controls and boosts are gone -- controls are discoverable and
            // boosts live in the armory, so neither belongs in a first-run explainer.
            var t2 = NeonTheme.Active;
            (string head, string body)[] sections =
            {
                ("YOUR GOAL",
                 "Destroy the enemy CORE at the far end of the lanes.\n" +
                 "They are trying to destroy yours."),

                ("1 — MATCH GEMS TO EARN",
                 "Swap two touching gems on the board to line up three or more.\n" +
                 "Every gem you clear is added to your wallet at the top."),

                ("2 — SPEND GEMS ON TROOPS",
                 "Cards along the middle show what each troop costs.\n" +
                 "Tap a card, then tap a lane to send that troop in."),

                ("3 — HOLD THE PYLONS",
                 "Each lane has capture points. Park a Trooper, Shield-bot or\n" +
                 "Hacker on one to take it — your troops then start further\n" +
                 "forward, and holding several speeds your whole army up."),

                ("BIGGER MATCHES, BIGGER HITS",
                 "Four in a row makes a Laser that clears a line.\n" +
                 "Five makes a Singularity. An L or T shape makes a Cross.\n" +
                 "These damage the enemy as well as the board."),
            };

            float yTop = 0.87f, block = 0.155f;
            for (int si = 0; si < sections.Length; si++)
            {
                float y1 = yTop - si * block, y0 = y1 - block + 0.012f;

                var hGo = new GameObject($"h{si}"); hGo.transform.SetParent(panelGo.transform, false);
                var hRt = hGo.AddComponent<RectTransform>();
                hRt.anchorMin = new Vector2(0.07f, y1 - 0.042f); hRt.anchorMax = new Vector2(0.95f, y1);
                hRt.offsetMin = hRt.offsetMax = Vector2.zero;
                var hTx = hGo.AddComponent<Text>();
                hTx.font = _font; hTx.fontSize = UIScale.FontSmall; hTx.fontStyle = FontStyle.Bold;
                hTx.color = t2.Accent; hTx.alignment = TextAnchor.MiddleLeft;
                hTx.text = sections[si].head;
                hTx.supportRichText = false; hTx.raycastTarget = false;

                var bGo = new GameObject($"b{si}"); bGo.transform.SetParent(panelGo.transform, false);
                var bRt = bGo.AddComponent<RectTransform>();
                bRt.anchorMin = new Vector2(0.07f, y0); bRt.anchorMax = new Vector2(0.95f, y1 - 0.046f);
                bRt.offsetMin = bRt.offsetMax = Vector2.zero;
                var bTx = bGo.AddComponent<Text>();
                bTx.font = _font; bTx.fontSize = UIScale.FontSmall;
                bTx.color = t2.TextBright; bTx.alignment = TextAnchor.UpperLeft;
                bTx.horizontalOverflow = HorizontalWrapMode.Wrap;
                bTx.verticalOverflow   = VerticalWrapMode.Truncate;
                bTx.lineSpacing = 1.12f;
                bTx.text = sections[si].body;
                bTx.supportRichText = false; bTx.raycastTarget = false;
            }

            // Close button — anchored to bottom of panel
            var closeGo = new GameObject("closeBtn"); closeGo.transform.SetParent(panelGo.transform, false);
            var closeRt = closeGo.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f); closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 12f);
            closeRt.sizeDelta = new Vector2(180f, 38f);
            var closeBg = closeGo.AddComponent<Image>(); closeBg.color = new Color(0.10f, 0.04f, 0.04f);
            var closeBrd = new GameObject("brd"); closeBrd.transform.SetParent(closeGo.transform, false);
            closeBrd.transform.SetAsFirstSibling();
            var closeBrdRt = closeBrd.AddComponent<RectTransform>();
            closeBrdRt.anchorMin = Vector2.zero; closeBrdRt.anchorMax = Vector2.one;
            closeBrdRt.offsetMin = new Vector2(-2, -2); closeBrdRt.offsetMax = new Vector2(2, 2);
            closeBrd.AddComponent<Image>().color = new Color(0.7f, 0.2f, 0.15f, 0.75f);
            var closeTGo = new GameObject("txt"); closeTGo.transform.SetParent(closeGo.transform, false);
            var closeTRt = closeTGo.AddComponent<RectTransform>();
            closeTRt.anchorMin = Vector2.zero; closeTRt.anchorMax = Vector2.one;
            closeTRt.offsetMin = closeTRt.offsetMax = Vector2.zero;
            var closeTxt = closeTGo.AddComponent<Text>();
            closeTxt.font = _font; closeTxt.fontSize = 18; closeTxt.fontStyle = FontStyle.Bold;
            closeTxt.color = new Color(1f, 0.4f, 0.3f); closeTxt.alignment = TextAnchor.MiddleCenter;
            closeTxt.text = "× CLOSE"; closeTxt.supportRichText = false; closeTxt.raycastTarget = false;
            var closeBtn = closeGo.AddComponent<Button>(); closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(() => _infoModal.SetActive(false));
            closeGo.AddComponent<ButtonFeel>();

            _infoModal.SetActive(false);
        }

        // ─────────────────────────────────────────── RANKS modal ─────────────────

        void BuildRanksModal()
        {
            var t = NeonTheme.Active;
            _ranksModal = new GameObject("RanksModal");
            _ranksModal.transform.SetParent(transform, false);
            var rt = _ranksModal.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var scrim = _ranksModal.AddComponent<Image>();
            scrim.color = new Color(t.BgDeep.r, t.BgDeep.g, t.BgDeep.b, 0.92f);
            // Tap outside the panel to dismiss.
            var scrimBtn = _ranksModal.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(() => _ranksModal.SetActive(false));

            var panelGo = new GameObject("panel"); panelGo.transform.SetParent(_ranksModal.transform, false);
            var panelRt = panelGo.AddComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0.13f, 0.09f); panelRt.anchorMax = new Vector2(0.87f, 0.93f);
            panelRt.offsetMin = panelRt.offsetMax = Vector2.zero;
            panelGo.AddComponent<Image>().color = new Color(t.BgPanel.r, t.BgPanel.g, t.BgPanel.b, 1f);
            // Eat clicks so tapping the panel does not fall through to the scrim's close.
            panelGo.AddComponent<Button>().transition = Selectable.Transition.None;

            var brd = new GameObject("brd"); brd.transform.SetParent(panelGo.transform, false);
            brd.transform.SetAsFirstSibling();
            var brdRt = brd.AddComponent<RectTransform>();
            brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
            brdRt.offsetMin = new Vector2(-2, -2); brdRt.offsetMax = new Vector2(2, 2);
            brd.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.55f);

            Text Label(Transform parent, string name, Vector2 aMin, Vector2 aMax, int size,
                       TextAnchor anchor, Color col, FontStyle fs = FontStyle.Normal)
            {
                var go = new GameObject(name); go.transform.SetParent(parent, false);
                var r = go.AddComponent<RectTransform>();
                r.anchorMin = aMin; r.anchorMax = aMax; r.offsetMin = r.offsetMax = Vector2.zero;
                var tx = go.AddComponent<Text>();
                tx.font = _font; tx.fontSize = size; tx.fontStyle = fs; tx.color = col;
                tx.alignment = anchor; tx.supportRichText = false; tx.raycastTarget = false;
                tx.horizontalOverflow = HorizontalWrapMode.Overflow;
                return tx;
            }

            Label(panelGo.transform, "title", new Vector2(0f, 0.90f), new Vector2(1f, 0.985f),
                  24, TextAnchor.MiddleCenter, t.Accent, FontStyle.Bold).text = "◈  RANKS  ◈";

            _ranksHeaderTxt = Label(panelGo.transform, "header", new Vector2(0.06f, 0.845f),
                new Vector2(0.94f, 0.90f), UIScale.FontSmall, TextAnchor.MiddleCenter, t.TextBright, FontStyle.Bold);

            // tier progress bar
            var trackGo = new GameObject("tierTrack"); trackGo.transform.SetParent(panelGo.transform, false);
            var trackRt = trackGo.AddComponent<RectTransform>();
            trackRt.anchorMin = new Vector2(0.12f, 0.815f); trackRt.anchorMax = new Vector2(0.88f, 0.832f);
            trackRt.offsetMin = trackRt.offsetMax = Vector2.zero;
            trackGo.AddComponent<Image>().color = new Color(t.BgDeep.r, t.BgDeep.g, t.BgDeep.b, 1f);
            var fillGo = new GameObject("tierFill"); fillGo.transform.SetParent(trackGo.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = new Vector2(0f, 0f); fillRt.anchorMax = new Vector2(0.5f, 1f);
            fillRt.offsetMin = fillRt.offsetMax = Vector2.zero;
            _ranksTierFill = fillGo.AddComponent<Image>();
            _ranksTierFill.color = t.Accent;

            // tab row
            string[] tabs = { "LADDER", "BREACH", "HISTORY" };
            for (int i = 0; i < 3; i++)
            {
                int idx = i;
                var tabGo = new GameObject("tab" + i); tabGo.transform.SetParent(panelGo.transform, false);
                var tRt = tabGo.AddComponent<RectTransform>();
                tRt.anchorMin = new Vector2(0.06f + i * 0.297f, 0.735f);
                tRt.anchorMax = new Vector2(0.06f + i * 0.297f + 0.277f, 0.795f);
                tRt.offsetMin = tRt.offsetMax = Vector2.zero;
                _ranksTabBg[i] = tabGo.AddComponent<Image>();
                _ranksTabBg[i].color = Lift(t.BgCard, 0.2f);
                _ranksTabTxt[i] = Label(tabGo.transform, "t", Vector2.zero, Vector2.one,
                    UIScale.FontSmall, TextAnchor.MiddleCenter, t.TextMid, FontStyle.Bold);
                _ranksTabTxt[i].text = tabs[i];
                var tb = tabGo.AddComponent<Button>(); tb.transition = Selectable.Transition.None;
                tb.onClick.AddListener(() => { _ranksTab = idx; AudioManager.Play(AudioManager.Sfx.Click, 0.6f); RefreshRanksPanel(); });
                tabGo.AddComponent<ButtonFeel>();
            }

            // content column — rebuilt on every RefreshRanksPanel()
            _ranksListGo = new GameObject("list"); _ranksListGo.transform.SetParent(panelGo.transform, false);
            var listRt = _ranksListGo.AddComponent<RectTransform>();
            listRt.anchorMin = new Vector2(0.055f, 0.085f); listRt.anchorMax = new Vector2(0.945f, 0.715f);
            listRt.offsetMin = listRt.offsetMax = Vector2.zero;

            // close
            var closeGo = new GameObject("closeBtn"); closeGo.transform.SetParent(panelGo.transform, false);
            var closeRt = closeGo.AddComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(0.5f, 0f); closeRt.anchorMax = new Vector2(0.5f, 0f);
            closeRt.pivot = new Vector2(0.5f, 0f);
            closeRt.anchoredPosition = new Vector2(0f, 12f);
            closeRt.sizeDelta = new Vector2(180f, 36f);
            closeGo.AddComponent<Image>().color = new Color(0.10f, 0.04f, 0.04f);
            var closeBrd = new GameObject("brd"); closeBrd.transform.SetParent(closeGo.transform, false);
            closeBrd.transform.SetAsFirstSibling();
            var closeBrdRt = closeBrd.AddComponent<RectTransform>();
            closeBrdRt.anchorMin = Vector2.zero; closeBrdRt.anchorMax = Vector2.one;
            closeBrdRt.offsetMin = new Vector2(-2, -2); closeBrdRt.offsetMax = new Vector2(2, 2);
            closeBrd.AddComponent<Image>().color = new Color(0.7f, 0.2f, 0.15f, 0.75f);
            Label(closeGo.transform, "txt", Vector2.zero, Vector2.one, 18,
                  TextAnchor.MiddleCenter, new Color(1f, 0.4f, 0.3f), FontStyle.Bold).text = "× CLOSE";
            var closeBtn = closeGo.AddComponent<Button>(); closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(() => _ranksModal.SetActive(false));
            closeGo.AddComponent<ButtonFeel>();

            _ranksModal.SetActive(false);
        }

        void RefreshRanksPanel()
        {
            if (_ranksModal == null || _ranksListGo == null) return;
            _refreshingRanks = true;
            var t = NeonTheme.Active;

            int mmr = PlayerProgress.MMR;
            string net = Leaderboard.IsOnline ? "● ONLINE" : "○ LOCAL";
            _ranksHeaderTxt.text =
                $"{RankLadder.TierName(mmr)}     ·     MMR {mmr}     ·     {net}";
            if (_ranksTierFill != null)
                _ranksTierFill.rectTransform.anchorMax =
                    new Vector2(Mathf.Clamp01(RankLadder.TierProgress(mmr)), 1f);

            for (int i = 0; i < 3; i++)
            {
                bool on = _ranksTab == i;
                _ranksTabBg[i].color  = on ? new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.28f)
                                           : Lift(t.BgCard, 0.2f);
                _ranksTabTxt[i].color = on ? t.Accent : t.TextMid;
            }

            for (int i = _ranksListGo.transform.childCount - 1; i >= 0; i--)
                Destroy(_ranksListGo.transform.GetChild(i).gameObject);

            const float step = 0.108f, rowH = 0.094f;
            void Row(int i, string left, string right, string btn, System.Action onBtn, Color? leftCol = null)
            {
                float yMax = 1f - i * step, yMin = yMax - rowH;
                if (yMin < 0f) return;
                var go = new GameObject("row" + i); go.transform.SetParent(_ranksListGo.transform, false);
                var r = go.AddComponent<RectTransform>();
                r.anchorMin = new Vector2(0f, yMin); r.anchorMax = new Vector2(1f, yMax);
                r.offsetMin = r.offsetMax = Vector2.zero;
                go.AddComponent<Image>().color = Lift(t.BgCard, 0.16f);

                bool hasBtn = !string.IsNullOrEmpty(btn) && onBtn != null;

                var lGo = new GameObject("l"); lGo.transform.SetParent(go.transform, false);
                var lRt = lGo.AddComponent<RectTransform>();
                lRt.anchorMin = new Vector2(0.03f, 0f); lRt.anchorMax = new Vector2(hasBtn ? 0.66f : 0.6f, 1f);
                lRt.offsetMin = lRt.offsetMax = Vector2.zero;
                var lTx = lGo.AddComponent<Text>();
                lTx.font = _font; lTx.fontSize = UIScale.FontSmall; lTx.color = leftCol ?? t.TextBright;
                lTx.alignment = TextAnchor.MiddleLeft; lTx.supportRichText = false; lTx.raycastTarget = false;
                lTx.horizontalOverflow = HorizontalWrapMode.Overflow;
                lTx.text = left;

                if (!string.IsNullOrEmpty(right))
                {
                    var rGo = new GameObject("r"); rGo.transform.SetParent(go.transform, false);
                    var rRt = rGo.AddComponent<RectTransform>();
                    rRt.anchorMin = new Vector2(0.6f, 0f); rRt.anchorMax = new Vector2(hasBtn ? 0.68f : 0.97f, 1f);
                    rRt.offsetMin = rRt.offsetMax = Vector2.zero;
                    var rTx = rGo.AddComponent<Text>();
                    rTx.font = _font; rTx.fontSize = UIScale.FontSmall; rTx.color = t.TextMid;
                    rTx.alignment = TextAnchor.MiddleRight; rTx.supportRichText = false; rTx.raycastTarget = false;
                    rTx.horizontalOverflow = HorizontalWrapMode.Overflow;
                    rTx.text = right;
                }

                if (hasBtn)
                {
                    var bGo = new GameObject("b"); bGo.transform.SetParent(go.transform, false);
                    var bRt = bGo.AddComponent<RectTransform>();
                    bRt.anchorMin = new Vector2(0.70f, 0.16f); bRt.anchorMax = new Vector2(0.98f, 0.84f);
                    bRt.offsetMin = bRt.offsetMax = Vector2.zero;
                    bGo.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.22f);
                    var bTx = new GameObject("t"); bTx.transform.SetParent(bGo.transform, false);
                    var bTxRt = bTx.AddComponent<RectTransform>();
                    bTxRt.anchorMin = Vector2.zero; bTxRt.anchorMax = Vector2.one;
                    bTxRt.offsetMin = bTxRt.offsetMax = Vector2.zero;
                    var bTxT = bTx.AddComponent<Text>();
                    bTxT.font = _font; bTxT.fontSize = UIScale.FontTiny; bTxT.fontStyle = FontStyle.Bold;
                    bTxT.color = t.Accent; bTxT.alignment = TextAnchor.MiddleCenter;
                    bTxT.text = btn; bTxT.supportRichText = false; bTxT.raycastTarget = false;
                    var bb = bGo.AddComponent<Button>(); bb.transition = Selectable.Transition.None;
                    bb.onClick.AddListener(() => onBtn());
                    bGo.AddComponent<ButtonFeel>();
                }
            }

            if (_ranksTab == 0)      BuildLadderRows(Row, mmr);
            else if (_ranksTab == 1) BuildBreachRows(Row);
            else                     BuildHistoryRows(Row);

            _refreshingRanks = false;
        }

        static string FmtDur(float seconds)
        {
            int s = Mathf.Max(0, Mathf.RoundToInt(seconds));
            return $"{s / 60}:{s % 60:00}";
        }

        string WhenLabel(System.DateTime d)
        {
            if (d == System.DateTime.MinValue) return "";
            var loc = d.ToLocalTime();
            return loc.Date == System.DateTime.Now.Date ? loc.ToString("HH:mm") : loc.ToString("MMM d");
        }

        void BuildLadderRows(System.Action<int, string, string, string, System.Action, Color?> Row, int myMmr)
        {
            int seed = RankLadder.DailySeed(_hoveredLevel);

            // Paint from cache; on first view of a level, snapshot the local pool now and ask the
            // backend for the shared board (repaints when it lands — Firebase only; local is sync).
            if (_ladderRows == null || _ladderRowsLevel != _hoveredLevel)
            {
                _ladderRows = new List<LeaderboardEntry>(Leaderboard.LocalTop(_hoveredLevel, seed, 8));
                _ladderRowsLevel = _hoveredLevel;
                int lvl = _hoveredLevel;
                Leaderboard.RefreshTop(lvl, seed, 8, remote =>
                {
                    if (remote == null) return;
                    _ladderRows = new List<LeaderboardEntry>(remote);
                    _ladderRowsLevel = lvl;
                    if (!_refreshingRanks && _ranksModal != null && _ranksModal.activeSelf && _ranksTab == 0)
                        RefreshRanksPanel();
                });
            }

            var list = _ladderRows;
            if (list.Count == 0)
            {
                Row(0, $"No entries on today's board — Lv {_hoveredLevel}.", "", null, null, null);
                Row(1, Leaderboard.IsOnline ? "Finish it and you're first on the board."
                                            : "Finish it to bank a ghost locally.", "", null, null, null);
                return;
            }

            int n = Mathf.Min(7, list.Count);
            for (int i = 0; i < n; i++)
            {
                var e = list[i];
                string left  = $"#{e.rank}   {e.pilot}{(e.isYou ? "  (you)" : "")}   MMR {e.mmr}";
                string right = (e.won ? "WIN " : "LOST ") + FmtDur(e.DurationSeconds);
                Color col = e.won ? new Color(0.55f, 0.95f, 0.68f) : new Color(0.95f, 0.6f, 0.62f);
                if (e.isYou)
                {
                    string w = WhenLabel(e.SetAt);
                    Row(i, left, right + (string.IsNullOrEmpty(w) ? "" : "   " + w), null, null, col);
                }
                else
                {
                    var ee = e;
                    Row(i, left, right, "CHALLENGE", () => ChallengeEntry(ee), col);
                }
            }
        }

        void ChallengeEntry(LeaderboardEntry e)
        {
            AudioManager.Play(AudioManager.Sfx.Click, 0.7f);
            int seed = RankLadder.DailySeed(_hoveredLevel);
            Leaderboard.FetchGhost(_hoveredLevel, seed, e, g => { if (g != null) ChallengeGhost(g); });
        }

        void BuildBreachRows(System.Action<int, string, string, string, System.Action, Color?> Row)
        {
            Row(0, $"TODAY'S BOARD  ·  Lv {_hoveredLevel}", $"seed #{RankLadder.DailySeed(_hoveredLevel)}", null, null, null);
            Row(1, $"WEEKLY BREACH  #{RankLadder.BreachSeed()}  —  coming soon", RankLadder.SeasonId(), null, null, null);

            var all = GhostStore.All();
            GhostRecord mine = all.Find(g => !g.bundled && g.level == _hoveredLevel)
                            ?? all.Find(g => g.level == _hoveredLevel);
            if (mine != null)
                Row(3, $"Race your ghost — Lv {_hoveredLevel}",
                    mine.RecordedAt == System.DateTime.MinValue ? "" : mine.RecordedAt.ToLocalTime().ToString("MMM d HH:mm"),
                    "RACE", () => ChallengeGhost(mine), null);
            else
                Row(3, $"No ghost for Lv {_hoveredLevel} yet — play it once", "", null, null, null);
        }

        void BuildHistoryRows(System.Action<int, string, string, string, System.Action, Color?> Row)
        {
            var h = RankLadder.History();
            if (h.Count == 0)
            {
                Row(0, "No ranked matches yet.", "", null, null, null);
                Row(1, "CHALLENGE a ghost from the LADDER tab.", "", null, null, null);
                return;
            }
            int rows = Mathf.Min(7, h.Count);
            for (int i = 0; i < rows; i++)
            {
                var m = h[i];
                string left  = $"{(m.won ? "WON " : "LOST")}  vs {m.oppPilot}   Lv {m.level}";
                string right = $"{(m.Delta >= 0 ? "+" : "")}{m.Delta}  ({m.mmrAfter})";
                Color col = m.won ? new Color(0.35f, 0.95f, 0.5f) : new Color(1f, 0.42f, 0.4f);
                Row(i, left, right, null, null, col);
            }
        }

        void ToggleShake()
        {
            GameSettings.CameraShake = !GameSettings.CameraShake;
            bool on = GameSettings.CameraShake;
            _shakeVal.text  = on ? "ON" : "OFF";
            _shakeVal.color = on ? new Color(0f, 0.9f, 0.3f) : new Color(0.55f, 0.55f, 0.55f);
            _shakeBtnBg.color = on ? new Color(0f, 0.12f, 0.06f) : new Color(0.06f, 0.06f, 0.08f);
        }

        void ToggleMusic()
        {
            AudioManager.MusicMuted = !AudioManager.MusicMuted;
            bool on = !AudioManager.MusicMuted;
            _musicVal.text  = on ? "ON" : "OFF";
            _musicVal.color = on ? new Color(0f, 0.9f, 0.3f) : new Color(0.55f, 0.55f, 0.55f);
            _musicBtnBg.color = on ? new Color(0f, 0.12f, 0.06f) : new Color(0.06f, 0.06f, 0.08f);
        }

        void SwitchPilot()
        {
            PlayerProgress.ClearCurrentSlot();
            FindAnyObjectByType<BattleScene>()?.ShowProfileSelect();
            gameObject.SetActive(false);
        }

        void OpenCosmeticsScreen()
        {
            var csGo = new GameObject("[Cosmetics]");
            csGo.transform.SetParent(transform.parent, false);
            var csRt = csGo.AddComponent<RectTransform>();
            csRt.anchorMin = Vector2.zero; csRt.anchorMax = Vector2.one;
            csRt.offsetMin = csRt.offsetMax = Vector2.zero;
            ScreenBackground.Attach(csRt);
            var csCg = csGo.AddComponent<CanvasGroup>();
            var cs = csGo.AddComponent<CosmeticsScreen>();
            cs.Init(_font);
            Tween.Fade(csCg, 0f, 1f, 0.20f);
            Tween.PopIn(csGo.transform, 0.20f);
        }

        void RefreshCurrency()
        {
            var sb = new System.Text.StringBuilder();
            var names = ThemeLocale.GemNames;
            for (int i = 0; i < 5; i++)
            {
                if (i > 0) sb.Append("  ·  ");
                sb.Append(names[i]);
                sb.Append(": ");
                sb.Append(PlayerProgress.Currency[i]);
            }
            string newText = sb.ToString();
            bool gained = newText != _currencyLabel.text;
            _currencyLabel.text = newText;
            if (gained)
                Tween.Tint(_currencyLabel, new Color(1f, 1f, 0.5f), new Color(1f, 0.85f, 0.1f), 0.45f, Tween.Ease.EaseOut);
        }

        static string CostStr(int[] cost)
        {
            var sb = new System.Text.StringBuilder();
            string[] s = ThemeLocale.GemShort;
            for (int i = 0; i < cost.Length; i++)
                if (cost[i] > 0) { if (sb.Length > 0) sb.Append('+'); sb.Append(cost[i]); sb.Append(s[i]); }
            return sb.Length > 0 ? sb.ToString() : "FREE";
        }

        // ── speed picker ─────────────────────────────────────────────────────────

        void BuildSpeedPicker(float cx, float y)
        {
            var theme  = NeonTheme.Active;
            float[] speeds = { 0.5f, 1f, 1.5f };
            string[] labels = { "0.5×", "1×", "1.5×" };
            float chipW = 52f, chipH = 24f, gap = 6f;
            float totalW = speeds.Length * chipW + (speeds.Length - 1) * gap;
            float startX = cx - totalW * 0.5f;

            // Container so the whole picker can be hidden in one call when VS mode is active.
            // Full-stretch, so child (0.5,0.5)-anchored coords stay in the same frame as before.
            _speedRoot = new GameObject("SpeedPicker");
            _speedRoot.transform.SetParent(transform, false);
            var srRt = _speedRoot.AddComponent<RectTransform>();
            srRt.anchorMin = Vector2.zero; srRt.anchorMax = Vector2.one;
            srRt.offsetMin = srRt.offsetMax = Vector2.zero;

            var rowLbl = new GameObject("speedLbl"); rowLbl.transform.SetParent(_speedRoot.transform, false);
            var lblRt  = rowLbl.AddComponent<RectTransform>();
            lblRt.anchorMin = lblRt.anchorMax = new Vector2(0.5f, 0.5f);
            lblRt.pivot = new Vector2(0.5f, 0.5f);
            lblRt.anchoredPosition = new Vector2(cx - totalW * 0.5f - 38f, y);
            lblRt.sizeDelta = new Vector2(62f, chipH);
            var rowTxt = rowLbl.AddComponent<Text>();
            rowTxt.font = _font; rowTxt.fontSize = UIScale.FontTiny; rowTxt.color = theme.TextDim;
            rowTxt.alignment = TextAnchor.MiddleRight; rowTxt.text = "SPEED";
            rowTxt.supportRichText = false; rowTxt.raycastTarget = false;

            for (int i = 0; i < speeds.Length; i++)
            {
                int ci = i;
                float s = speeds[i];
                var go = new GameObject($"spd{i}"); go.transform.SetParent(_speedRoot.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(startX + i * (chipW + gap) + chipW * 0.5f, y);
                rt.sizeDelta = new Vector2(chipW, chipH);
                _speedBgs[i] = go.AddComponent<Image>();

                var brdGo = new GameObject("brd"); brdGo.transform.SetParent(go.transform, false);
                brdGo.transform.SetAsFirstSibling();
                var brdRt = brdGo.AddComponent<RectTransform>();
                brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
                brdRt.offsetMin = new Vector2(-1, -1); brdRt.offsetMax = new Vector2(1, 1);
                brdGo.AddComponent<Image>().color = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.45f);

                var tGo = new GameObject("t"); tGo.transform.SetParent(go.transform, false);
                var tRt = tGo.AddComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
                tRt.offsetMin = tRt.offsetMax = Vector2.zero;
                _speedTxts[i] = tGo.AddComponent<Text>();
                _speedTxts[i].font = _font; _speedTxts[i].fontSize = UIScale.FontSmall;
                _speedTxts[i].fontStyle = FontStyle.Bold; _speedTxts[i].alignment = TextAnchor.MiddleCenter;
                _speedTxts[i].text = labels[i]; _speedTxts[i].supportRichText = false; _speedTxts[i].raycastTarget = false;

                var btn = go.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => { GameSettings.BattleSpeed = s; UpdateSpeedHighlights(); });
                go.AddComponent<ButtonFeel>();
            }
            UpdateSpeedHighlights();
        }

        void UpdateSpeedHighlights()
        {
            var theme = NeonTheme.Active;
            float cur = GameSettings.BattleSpeed;
            float[] speeds = { 0.5f, 1f, 1.5f };
            for (int i = 0; i < _speedBgs.Length; i++)
            {
                if (_speedBgs[i] == null) continue;
                bool sel = Mathf.Approximately(cur, speeds[i]);
                _speedBgs[i].color  = sel ? new Color(theme.Accent.r * 0.25f, theme.Accent.g * 0.25f, theme.Accent.b * 0.25f, 1f)
                                          : new Color(theme.BgCard.r,          theme.BgCard.g,          theme.BgCard.b,          0.85f);
                _speedTxts[i].color = sel ? theme.Accent : theme.TextDim;
            }
        }

        // ──────────────────────────────────────────────── helpers ─────────────────

        Text MkLabelObj(float x, float y, int size, Color col)
        {
            var go = new GameObject("lbl"); go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y + _menuShiftY);
            rt.sizeDelta = new Vector2(340f, size + 14f);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.color = col;
            t.alignment = TextAnchor.MiddleCenter;
            t.supportRichText = false; t.raycastTarget = false;
            if (size >= UIScale.FontH2) NeonUI.Title(t); else NeonUI.Shadow(t);
            return t;
        }

        void MkLabel(string text, float x, float y, int size, Color col,
            TextAnchor align = TextAnchor.MiddleCenter)
        {
            var t = MkLabelObj(x, y, size, col);
            t.text = text; t.alignment = align;
            var rt = t.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(740f, size + 10f);
        }

        void MkHRule(float y)
        {
            var go = new GameObject("hr"); go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0f, y + _menuShiftY);
            rt.sizeDelta = new Vector2(620f, 1f);
            go.AddComponent<Image>().color = NeonTheme.Active.AccentDim;
        }

        void MkHRule_Vert(float x, float top, float bottom)
        {
            var go = new GameObject("vr"); go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            float midY = (top + bottom) * 0.5f;
            rt.anchoredPosition = new Vector2(x, midY + _menuShiftY);
            rt.sizeDelta = new Vector2(1f, top - bottom);
            var c = NeonTheme.Active.AccentDim; c.a *= 0.65f;
            go.AddComponent<Image>().color = c;
        }

        // Toggle button placed relative to canvas center
        Image MkToggleBtn(float x, float y, string val, Color col, out Text valText)
        {
            var go = new GameObject("tog"); go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(90f, 24f);
            return BuildToggle(go, val, col, out valText);
        }

        // Toggle button placed relative to a specific parent (for edge-anchored elements)
        Image MkToggleBtn_Abs(GameObject parent, float xOffset, float yOffset,
            string val, Color col, out Text valText)
        {
            var go = new GameObject("tog"); go.transform.SetParent(parent.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0f, 0.5f);
            rt.anchoredPosition = new Vector2(xOffset, yOffset);
            rt.sizeDelta = new Vector2(68f, 22f);
            return BuildToggle(go, val, col, out valText);
        }

        Image BuildToggle(GameObject go, string val, Color col, out Text valText)
        {
            var th = NeonTheme.Active;
            var bg = go.AddComponent<Image>();
            bg.color = new Color(th.BgCard.r, th.BgCard.g, th.BgCard.b, 0.90f);
            // 1px accent left edge as visual marker
            var edgeGo = new GameObject("edge"); edgeGo.transform.SetParent(go.transform, false);
            edgeGo.transform.SetAsFirstSibling();
            var edgeRt = edgeGo.AddComponent<RectTransform>();
            edgeRt.anchorMin = new Vector2(0f, 0f); edgeRt.anchorMax = new Vector2(0f, 1f);
            edgeRt.pivot = new Vector2(0f, 0.5f);
            edgeRt.anchoredPosition = Vector2.zero; edgeRt.sizeDelta = new Vector2(2f, 0f);
            edgeGo.AddComponent<Image>().color = new Color(th.Accent.r, th.Accent.g, th.Accent.b, 0.80f);
            var lgo = new GameObject("val"); lgo.transform.SetParent(go.transform, false);
            var lrt = lgo.AddComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(4f, 0f); lrt.offsetMax = Vector2.zero;
            var lt = lgo.AddComponent<Text>();
            lt.font = _font; lt.fontSize = 11; lt.fontStyle = FontStyle.Bold;
            lt.color = col; lt.alignment = TextAnchor.MiddleCenter;
            lt.text = val; lt.supportRichText = false; lt.raycastTarget = false;
            valText = lt;
            var btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
            var cs = btn.colors;
            cs.normalColor      = new Color(th.BgCard.r,          th.BgCard.g,          th.BgCard.b,          0.90f);
            cs.highlightedColor = new Color(th.AccentSecondary.r, th.AccentSecondary.g, th.AccentSecondary.b, 0.55f);
            cs.pressedColor     = new Color(th.Accent.r,          th.Accent.g,          th.Accent.b,          0.30f);
            cs.selectedColor    = cs.normalColor;
            btn.colors = cs;
            go.AddComponent<ButtonFeel>();
            return bg;
        }

        GameObject MkActionBtn(float x, float y, float w, float h, string label, Color textCol,
            out Text labelText)
        {
            var go = new GameObject("btn"); go.transform.SetParent(transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y + _menuShiftY);
            rt.sizeDelta = new Vector2(w, h);
            var th = NeonTheme.Active;

            // Layer 0 — drop shadow (behind fill, offset down+right)
            var shGo = new GameObject("shadow"); shGo.transform.SetParent(go.transform, false);
            shGo.transform.SetAsFirstSibling();
            var shRt = shGo.AddComponent<RectTransform>();
            shRt.anchorMin = Vector2.zero; shRt.anchorMax = Vector2.one;
            shRt.offsetMin = new Vector2(2f, -3f); shRt.offsetMax = new Vector2(2f, -3f);
            shGo.AddComponent<Image>().color = new Color(th.BgDeep.r, th.BgDeep.g, th.BgDeep.b, 1f);

            // Layer 1 — button fill (themed BgCard)
            var bg = go.AddComponent<Image>();
            // DEPLOY and SHOP were reading as bare text: a 94%-alpha card colour that sits a
            // hair above the ground is not a button. Lift it toward the accent like the level
            // buttons, fully opaque.
            bg.color = Lift(th.BgCard, 0.52f);   // DEPLOY has to be the loudest thing here

            // Layer 2 — 2px accent strip at top edge
            var acGo = new GameObject("accentStrip"); acGo.transform.SetParent(go.transform, false);
            var acRt = acGo.AddComponent<RectTransform>();
            acRt.anchorMin = new Vector2(0f, 1f); acRt.anchorMax = new Vector2(1f, 1f);
            acRt.pivot = new Vector2(0.5f, 1f);
            acRt.anchoredPosition = Vector2.zero;
            acRt.sizeDelta = new Vector2(0f, 2f);
            acGo.AddComponent<Image>().color = new Color(th.Accent.r, th.Accent.g, th.Accent.b, 0.90f);

            // Layer 3 — subtle white top highlight (material depth cue)
            var hlGo = new GameObject("highlight"); hlGo.transform.SetParent(go.transform, false);
            var hlRt = hlGo.AddComponent<RectTransform>();
            hlRt.anchorMin = new Vector2(0f, 0.5f); hlRt.anchorMax = Vector2.one;
            hlRt.offsetMin = hlRt.offsetMax = Vector2.zero;
            var hlImg = hlGo.AddComponent<Image>();
            hlImg.color = new Color(1f, 1f, 1f, 0.05f);
            hlImg.raycastTarget = false;

            // Layer 4 — label
            var tgo = new GameObject("txt"); tgo.transform.SetParent(go.transform, false);
            var trt = tgo.AddComponent<RectTransform>();
            trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one;
            trt.offsetMin = trt.offsetMax = Vector2.zero;
            var txt = tgo.AddComponent<Text>();
            txt.font = _font; txt.fontSize = Mathf.RoundToInt(h * 0.44f);
            txt.fontStyle = FontStyle.Bold; txt.color = textCol;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow; // never wrap "DEPLOY" → "DEPLO\nY"
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            txt.text = label; txt.supportRichText = false;
            labelText = txt;

            // Button — ColorTint on fill, themed hover/press
            var btn = go.AddComponent<Button>(); btn.targetGraphic = bg;
            var cs = btn.colors;
            cs.normalColor      = new Color(th.BgCard.r,          th.BgCard.g,          th.BgCard.b,          0.94f);
            cs.highlightedColor = new Color(th.AccentSecondary.r, th.AccentSecondary.g, th.AccentSecondary.b, 0.60f);
            cs.pressedColor     = new Color(th.Accent.r,          th.Accent.g,          th.Accent.b,          0.35f);
            cs.colorMultiplier  = 1f;
            btn.colors = cs;
            go.AddComponent<ButtonFeel>();
            return go;
        }
    }
}
