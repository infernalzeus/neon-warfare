using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using NW.Board.Domain;   // BoardModel.GemKindCount, used by the gem converter

namespace NW.App
{
    /// <summary>
    /// Full-screen cosmetics shop — tab layout with animated previews and purchase ceremony.
    /// Four tabs: COLOR | EFFECT | GLOW | BOARD. Two-column card grid per tab.
    /// </summary>
    public sealed class CosmeticsScreen : MonoBehaviour
    {
        Font   _font;
        Text   _tokenLabel;
        int    _tab;                           // 0=DEPLOY 1=GLOW 2=SCENE 3=TRAILS
        Transform _cardArea;
        Image[]   _tabBgs  = new Image[4];
        TokenIconAnim _tokenIcon;
        Text[]    _tabTxts = new Text[4];

        // ── palette ─────────────────────────────────────────────────────────────
        static readonly Color Gold       = new Color(0.95f, 0.80f, 0.20f);
        static readonly Color RareCol    = new Color(0.35f, 0.78f, 1.00f);
        static readonly Color EpicCol    = new Color(0.80f, 0.32f, 1.00f);
        static readonly Color LegCol     = new Color(1.00f, 0.72f, 0.08f);
        static readonly Color CtaGreen   = new Color(0.18f, 0.92f, 0.45f);  // "TAP TO EQUIP" bright green
        static readonly Color CtaGold    = new Color(1.00f, 0.78f, 0.12f);  // "UNLOCK" gold
        static readonly Color GrayDim    = new Color(0.32f, 0.35f, 0.42f);

        // Rarity per item [tab][itemIndex]
        // one row per TAB, in tab order: DEPLOY, GLOW, SCENE, TRAILS
        static readonly string[][] _rarities =
        {
            new[] { "", "RARE",  "EPIC",  "RARE",  "EPIC" },              // DEPLOY 0-4
            new[] { "", "RARE",  "RARE",  "EPIC",  "RARE" },              // VFX 0-4
            new[] { "", "RARE",  "RARE",  "EPIC",  "LEGENDARY" },         // SCENE
            new[] { "", "RARE", "EPIC", "EPIC", "RARE", "EPIC", "EPIC", "LEGENDARY" },  // TRAILS 0-7
        };

        public void Init(Font font)
        {
            _font = font;
            var rt = GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var bg = gameObject.AddComponent<Image>();
            var d  = NeonTheme.Active.BgDeep;
            bg.color = new Color(d.r, d.g, d.b, 0.95f);

            var closeBack = gameObject.AddComponent<Button>();
            closeBack.transition = Selectable.Transition.None;
            closeBack.onClick.AddListener(() => Destroy(gameObject));

            BuildPanel();
        }

        // ── panel ───────────────────────────────────────────────────────────────

        void BuildPanel()
        {
            var theme = NeonTheme.Active;

            var panelGo = Mk(transform, "panel", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f));
            // Was a literal navy, so the armory looked identical in all eight themes.
            panelGo.AddComponent<Image>().color = new Color(theme.BgCard.r * 0.55f,
                                                            theme.BgCard.g * 0.55f,
                                                            theme.BgCard.b * 0.55f, 1f);
            BdrGo(panelGo.transform, new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.90f), 3f);
            panelGo.AddComponent<Button>().transition = Selectable.Transition.None;

            // Title bar background
            var titleBg = Mk(panelGo.transform, "titlebg", new Vector2(0f, 0.90f), new Vector2(1f, 1f));
            titleBg.AddComponent<Image>().color = new Color(theme.BgDeep.r * 1.1f,
                                                            theme.BgDeep.g * 1.1f,
                                                            theme.BgDeep.b * 1.1f, 1f);

            // Title
            Txt(panelGo.transform, "◈  ARMORY  ◈", 0, 1, 0.91f, 0.99f, 34, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, false);

            // Thick accent underline
            var line = Mk(panelGo.transform, "line", new Vector2(0f, 0.895f), new Vector2(1f, 0.902f));
            line.AddComponent<Image>().color = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 1f);

            // Token balance (left)
            var tokBg = Mk(panelGo.transform, "tokbg", new Vector2(0.02f, 0.836f), new Vector2(0.50f, 0.910f));
            tokBg.AddComponent<Image>().color = new Color(0.14f, 0.11f, 0.04f);
            BdrGo(tokBg.transform, new Color(CtaGold.r, CtaGold.g, CtaGold.b, 0.80f), 1.5f);
            // The balance used to be a bare number with a hex glyph in the string. It is the
            // shop's currency, so it gets an actual coin that turns and flashes when it grows.
            var tokIcoGo = Mk(tokBg.transform, "tokico", new Vector2(0.04f, 0.16f), new Vector2(0.22f, 0.84f));
            _tokenIcon = tokIcoGo.AddComponent<TokenIconAnim>();
            _tokenLabel = Txt(tokBg.transform, TokenStr(), 0.24f, 0.97f, 0f, 1f,
                              UIScale.FontBody, FontStyle.Bold, CtaGold, TextAnchor.MiddleCenter, false);
            Outline(_tokenLabel, 1.4f);

            // Boosts button (right) — beveled purple button
            var boostBtnGo = Mk(panelGo.transform, "boostBtn", new Vector2(0.52f, 0.836f), new Vector2(0.745f, 0.910f));
            Bdr(boostBtnGo.transform, new Color(0.01f, 0.01f, 0.03f, 1f), 2.5f);
            Bdr(boostBtnGo.transform, new Color(0.72f, 0.38f, 1f, 0.85f), 1f);
            boostBtnGo.AddComponent<Image>().color = new Color(0.12f, 0.05f, 0.24f);
            TopStrip(boostBtnGo.transform, new Color(0.88f, 0.65f, 1f, 0.22f), 3f);
            BotStrip(boostBtnGo.transform, new Color(0f, 0f, 0f, 0.45f), 3f);
            Txt(boostBtnGo.transform, "⚡ BOOSTS", 0, 1, 0, 1, UIScale.FontSmall, FontStyle.Bold,
                new Color(0.90f, 0.65f, 1f), TextAnchor.MiddleCenter, false);
            var boostBtn = boostBtnGo.AddComponent<Button>(); boostBtn.transition = Selectable.Transition.None;
            boostBtn.onClick.AddListener(() => ShowBoostsOverlay(panelGo.transform));
            boostBtnGo.AddComponent<ButtonFeel>();

            // Convert button — gems into armory tokens.
            // The shop spends pp_tokens (level rewards); the match board pays out GEMS. Nothing
            // connected the two, so a player could hold 9,999 of every gem and still be unable
            // to buy a single cosmetic -- every purchase just played the error sound.
            var convBtnGo = Mk(panelGo.transform, "convBtn", new Vector2(0.765f, 0.836f), new Vector2(0.98f, 0.910f));
            Bdr(convBtnGo.transform, new Color(0.01f, 0.02f, 0.02f, 1f), 2.5f);
            Bdr(convBtnGo.transform, new Color(0.30f, 0.90f, 0.72f, 0.85f), 1f);
            convBtnGo.AddComponent<Image>().color = new Color(0.04f, 0.18f, 0.15f);
            TopStrip(convBtnGo.transform, new Color(0.60f, 1f, 0.90f, 0.22f), 3f);
            BotStrip(convBtnGo.transform, new Color(0f, 0f, 0f, 0.45f), 3f);
            Txt(convBtnGo.transform, "⇄ CONVERT", 0, 1, 0, 1, UIScale.FontSmall, FontStyle.Bold,
                new Color(0.55f, 1f, 0.86f), TextAnchor.MiddleCenter, false);
            var convBtn = convBtnGo.AddComponent<Button>(); convBtn.transition = Selectable.Transition.None;
            convBtn.onClick.AddListener(() => ShowConvertOverlay(panelGo.transform));
            convBtnGo.AddComponent<ButtonFeel>();

            // Tab bar
            var tabBar = Mk(panelGo.transform, "tabbar", new Vector2(0f, 0.770f), new Vector2(1f, 0.858f));
            tabBar.AddComponent<Image>().color = new Color(theme.BgDeep.r * 1.25f,
                                                           theme.BgDeep.g * 1.25f,
                                                           theme.BgDeep.b * 1.25f, 1f);
            // Tab separator line bottom
            var tabLine = Mk(panelGo.transform, "tabline", new Vector2(0f, 0.789f), new Vector2(1f, 0.792f));
            tabLine.AddComponent<Image>().color = theme.AccentDim;

            // Was three tabs for seven categories, and the class comment above promised
            // four. DEPLOY leads because it is what replaced the troop skins.
            string[] tabNames = { "DEPLOY", "VFX", "SCENE", "TRAILS" };
            float    tw       = 1f / 4f;
            for (int i = 0; i < 4; i++)
            {
                int ti = i;
                var tGo = Mk(tabBar.transform, $"tab{i}", new Vector2(i * tw, 0f), new Vector2(i * tw + tw, 1f));
                _tabBgs[i]  = tGo.AddComponent<Image>();
                _tabTxts[i] = Txt(tGo.transform, tabNames[i], 0, 1, 0, 1, UIScale.FontBody, FontStyle.Bold, GrayDim, TextAnchor.MiddleCenter, false);
                var btn = tGo.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() => SelectTab(ti));
                tGo.AddComponent<ButtonFeel>();
            }

            // Card area
            var cardAreaGo = Mk(panelGo.transform, "cards", new Vector2(0f, 0.075f), new Vector2(1f, 0.766f));
            _cardArea = cardAreaGo.transform;

            // Close button — CoC-style chunky red button
            var closeGo = Mk(panelGo.transform, "close", new Vector2(0.22f, 0.008f), new Vector2(0.78f, 0.075f));
            Bdr(closeGo.transform, new Color(0.01f, 0f, 0f, 1f), 3f);
            Bdr(closeGo.transform, new Color(1f, 0.18f, 0.12f, 0.88f), 1.5f);
            closeGo.AddComponent<Image>().color = new Color(0.28f, 0.05f, 0.05f);
            TopStrip(closeGo.transform, new Color(1f, 0.50f, 0.40f, 0.28f), 3f);
            BotStrip(closeGo.transform, new Color(0f, 0f, 0f, 0.55f), 3f);
            Txt(closeGo.transform, "✕  CLOSE", 0, 1, 0, 1, UIScale.FontH2, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, false);
            var closeBtn = closeGo.AddComponent<Button>(); closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(() => Destroy(gameObject));
            closeGo.AddComponent<ButtonFeel>();

            SelectTab(0);
        }

        // ── boosts overlay ──────────────────────────────────────────────────────

        void ShowBoostsOverlay(Transform panelParent)
        {
            var theme = NeonTheme.Active;
            var ov = Mk(panelParent, "boostsOv", Vector2.zero, Vector2.one);
            ov.AddComponent<Image>().color = new Color(0.02f, 0.01f, 0.06f, 0.96f);
            var ovBtn = ov.AddComponent<Button>(); ovBtn.transition = Selectable.Transition.None;

            // Title
            Txt(ov.transform, "⚡  BATTLE BOOSTS  ⚡", 0, 1, 0.88f, 0.98f, 28, FontStyle.Bold,
                new Color(0.88f, 0.60f, 1f), TextAnchor.MiddleCenter, false);
            var tline = Mk(ov.transform, "tl", new Vector2(0.05f, 0.875f), new Vector2(0.95f, 0.878f));
            tline.AddComponent<Image>().color = new Color(0.72f, 0.38f, 1f, 0.5f);

            // Subtitle
            Txt(ov.transform, "True permanent enhancements applied to every deployed troop.",
                0.05f, 0.95f, 0.82f, 0.875f, UIScale.FontTiny, FontStyle.Normal,
                theme.TextMid, TextAnchor.MiddleCenter, false);

            // Two boost cards side-by-side
            BuildBoostCard(ov.transform, "DAMAGE BOOST", "⚔",
                PlayerProgress.DamageBoostLevel,
                PlayerProgress.BoostDamageCost,
                PlayerProgress.BoostDamageMult,
                () => {
                    if (PlayerProgress.TryUpgradeDamageBoost())
                    {
                        _tokenLabel.text = TokenStr();
                        Destroy(ov);
                        ShowBoostsOverlay(panelParent);
                    }
                },
                new Vector2(0.04f, 0.15f), new Vector2(0.48f, 0.80f));

            BuildBoostCard(ov.transform, "SPEED BOOST", "⚡",
                PlayerProgress.SpeedBoostLevel,
                PlayerProgress.BoostSpeedCost,
                PlayerProgress.BoostSpeedMult,
                () => {
                    if (PlayerProgress.TryUpgradeSpeedBoost())
                    {
                        _tokenLabel.text = TokenStr();
                        Destroy(ov);
                        ShowBoostsOverlay(panelParent);
                    }
                },
                new Vector2(0.52f, 0.15f), new Vector2(0.96f, 0.80f));

            // Info line
            Txt(ov.transform, "Boosts stack multiplicatively. Max +20% per stat at level 3.",
                0.05f, 0.95f, 0.08f, 0.14f, UIScale.FontTiny, FontStyle.Normal,
                theme.TextDim, TextAnchor.MiddleCenter, false);

            // Close — beveled red back button
            var clGo = Mk(ov.transform, "cl", new Vector2(0.22f, 0.008f), new Vector2(0.78f, 0.075f));
            Bdr(clGo.transform, new Color(0.01f, 0f, 0f, 1f), 3f);
            Bdr(clGo.transform, new Color(1f, 0.18f, 0.12f, 0.88f), 1.5f);
            clGo.AddComponent<Image>().color = new Color(0.28f, 0.05f, 0.05f);
            TopStrip(clGo.transform, new Color(1f, 0.50f, 0.40f, 0.28f), 3f);
            BotStrip(clGo.transform, new Color(0f, 0f, 0f, 0.55f), 3f);
            Txt(clGo.transform, "← BACK", 0, 1, 0, 1, UIScale.FontH2, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, false);
            var clBtn = clGo.AddComponent<Button>(); clBtn.transition = Selectable.Transition.None;
            clBtn.onClick.AddListener(() => Destroy(ov));
            clGo.AddComponent<ButtonFeel>();
        }

        void BuildBoostCard(Transform parent, string label, string icon, int currentLevel,
                            int[] costs, float[] mults, Action onUpgrade, Vector2 aMin, Vector2 aMax)
        {
            var theme = NeonTheme.Active;
            var card = Mk(parent, label, aMin, aMax);
            card.AddComponent<Image>().color = new Color(0.06f, 0.04f, 0.12f);
            BdrGo(card.transform, new Color(0.72f, 0.38f, 1f, 0.45f), 1f);

            // Icon + name
            Txt(card.transform, icon + "  " + label, 0.05f, 0.95f, 0.78f, 0.95f,
                UIScale.FontH2, FontStyle.Bold, new Color(0.88f, 0.60f, 1f), TextAnchor.MiddleCenter, false);

            // Level pip strip
            for (int lv = 1; lv <= 3; lv++)
            {
                bool filled = currentLevel >= lv;
                float x0 = 0.08f + (lv - 1) * 0.30f;
                var pip = Mk(card.transform, $"p{lv}", new Vector2(x0, 0.62f), new Vector2(x0 + 0.25f, 0.74f));
                pip.AddComponent<Image>().color = filled
                    ? new Color(0.78f, 0.42f, 1f)
                    : new Color(0.18f, 0.12f, 0.28f);
                BdrGo(pip.transform, new Color(0.72f, 0.38f, 1f, 0.6f), 1f);
                string pct = lv == 1 ? "+5%" : lv == 2 ? "+10%" : "+20%";
                Txt(pip.transform, pct, 0, 1, 0, 1, UIScale.FontTiny, FontStyle.Bold,
                    filled ? Color.white : new Color(0.55f, 0.45f, 0.65f), TextAnchor.MiddleCenter, false);
            }

            // Current effect
            string effStr = currentLevel == 0 ? "No boost active"
                          : $"Active: ×{mults[currentLevel]:F2}  (+{Mathf.RoundToInt((mults[currentLevel]-1f)*100)}%)";
            Txt(card.transform, effStr, 0.05f, 0.95f, 0.45f, 0.60f,
                UIScale.FontSmall, FontStyle.Normal,
                currentLevel > 0 ? new Color(0.55f, 0.95f, 0.55f) : theme.TextDim,
                TextAnchor.MiddleCenter, false);

            bool maxed = currentLevel >= 3;
            if (maxed)
            {
                var mxGo = Mk(card.transform, "mx", new Vector2(0.08f, 0.06f), new Vector2(0.92f, 0.38f));
                mxGo.AddComponent<Image>().color = new Color(0.10f, 0.22f, 0.10f);
                BdrGo(mxGo.transform, new Color(0.25f, 0.90f, 0.25f, 0.55f), 1f);
                Txt(mxGo.transform, "✓  MAX LEVEL", 0, 1, 0, 1, UIScale.FontBody, FontStyle.Bold,
                    new Color(0.25f, 0.95f, 0.35f), TextAnchor.MiddleCenter, false);
            }
            else
            {
                int nextCost  = costs[currentLevel + 1];
                bool canAfford = PlayerProgress.Tokens >= nextCost;
                string nextPct = currentLevel + 1 == 1 ? "+5%" : currentLevel + 1 == 2 ? "+10%" : "+20%";

                Txt(card.transform, $"Next: {nextPct}  ·  ⬡ {nextCost}",
                    0.05f, 0.95f, 0.33f, 0.44f,
                    UIScale.FontTiny, FontStyle.Normal,
                    canAfford ? Gold : new Color(0.5f, 0.45f, 0.38f),
                    TextAnchor.MiddleCenter, false);

                var upGo = Mk(card.transform, "up", new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.32f));
                Bdr(upGo.transform, new Color(0.01f, 0.01f, 0.03f, 1f), 2.5f);
                Bdr(upGo.transform, canAfford
                    ? new Color(0.72f, 0.38f, 1f, 0.85f)
                    : new Color(0.28f, 0.24f, 0.34f, 0.55f), 1f);
                upGo.AddComponent<Image>().color = canAfford
                    ? new Color(0.16f, 0.07f, 0.30f)
                    : new Color(0.08f, 0.07f, 0.11f);
                TopStrip(upGo.transform, new Color(canAfford ? 0.88f : 0.4f, canAfford ? 0.65f : 0.4f, canAfford ? 1f : 0.45f, 0.25f), 3f);
                BotStrip(upGo.transform, new Color(0f, 0f, 0f, 0.45f), 3f);
                Txt(upGo.transform, canAfford ? "⬡  UPGRADE" : "INSUFFICIENT TOKENS",
                    0, 1, 0, 1, UIScale.FontSmall, FontStyle.Bold,
                    canAfford ? new Color(0.90f, 0.65f, 1f) : new Color(0.38f, 0.36f, 0.44f),
                    TextAnchor.MiddleCenter, false);

                if (canAfford)
                {
                    var upBtn = upGo.AddComponent<Button>(); upBtn.transition = Selectable.Transition.None;
                    upBtn.onClick.AddListener(() => onUpgrade());
                    upGo.AddComponent<ButtonFeel>();
                }
            }
        }

        // ── tab selection ────────────────────────────────────────────────────────

        void SelectTab(int tab)
        {
            _tab = tab;
            var theme = NeonTheme.Active;
            string[] tabBase = { "DEPLOY", "VFX", "SCENE", "TRAILS" };
            bool[] equipped  = { GameSettings.ActiveDeployFx > 0, GameSettings.ActiveVfxSkin > 0,
                                 GameSettings.ActiveLaneScene > 0, GameSettings.ActiveGemTrail > 0 };
            for (int i = 0; i < 4; i++)
            {
                bool sel = i == tab;
                _tabTxts[i].text = equipped[i] ? tabBase[i] + " ●" : tabBase[i];
                _tabBgs[i].color  = sel
                    ? new Color(theme.Accent.r * 0.28f, theme.Accent.g * 0.28f, theme.Accent.b * 0.28f, 1f)
                    : new Color(0f, 0f, 0f, 0f);
                _tabTxts[i].color = sel ? Color.white : GrayDim;
                _tabTxts[i].fontStyle = sel ? FontStyle.Bold : FontStyle.Normal;
            }

            for (int i = _cardArea.childCount - 1; i >= 0; i--)
                Destroy(_cardArea.GetChild(i).gameObject);

            BuildCards(_cardArea, BuildCatData(tab));
        }

        // ── category data ────────────────────────────────────────────────────────

        CatData BuildCatData(int tab)
        {
            var theme = NeonTheme.Active;

            switch (tab)
            {
                case 0: // DEPLOY — fires once, when the troop lands
                    return new CatData
                    {
                        Names = NeonCosmetics.DeployFxNames, Descs = NeonCosmetics.DeployFxDesc,
                        Rarities = _rarities[0], Costs = NeonCosmetics.DeployFxCost,
                        IsUnlocked = i => NeonCosmetics.IsUnlocked((NeonCosmetics.DeployFx)i),
                        TryUnlock  = i => NeonCosmetics.TryUnlock ((NeonCosmetics.DeployFx)i),
                        SetActive  = i => NeonCosmetics.SetActive ((NeonCosmetics.DeployFx)i),
                        GetActive  = ()  => (int)NeonCosmetics.ActiveDeployFx,
                        BuildPreview = (idx, p) =>
                        {
                            var a = p.gameObject.AddComponent<DeployFxPreviewAnim>();
                            a.FxIdx = idx; a.ThemeAccent = theme.Accent;
                            a.Setup();
                        }
                    };
                case 1: // VFX — sits beside the troop, never over it
                    return new CatData
                    {
                        Names = NeonCosmetics.VfxSkinNames, Descs = NeonCosmetics.VfxSkinDesc,
                        Rarities = _rarities[1], Costs = NeonCosmetics.VfxSkinCost,
                        IsUnlocked = i => NeonCosmetics.IsUnlocked((NeonCosmetics.VfxSkin)i),
                        TryUnlock  = i => NeonCosmetics.TryUnlock ((NeonCosmetics.VfxSkin)i),
                        SetActive  = i => NeonCosmetics.SetActive ((NeonCosmetics.VfxSkin)i),
                        GetActive  = ()  => (int)NeonCosmetics.ActiveVfxSkin,
                        BuildPreview = (idx, p) =>
                        {
                            var a = p.gameObject.AddComponent<VfxSkinPreviewAnim>();
                            a.SkinIdx = idx; a.ThemeAccent = theme.Accent;
                            a.Setup();
                        }
                    };
                case 2: // SCENE — battlefield lane backgrounds
                    return new CatData
                    {
                        Names = NeonCosmetics.LaneSceneNames, Descs = NeonCosmetics.LaneSceneDesc,
                        Rarities = _rarities[2], Costs = NeonCosmetics.LaneSceneCost,
                        IsUnlocked = i => NeonCosmetics.IsUnlocked((NeonCosmetics.LaneScene)i),
                        TryUnlock  = i => NeonCosmetics.TryUnlock ((NeonCosmetics.LaneScene)i),
                        SetActive  = i => NeonCosmetics.SetActive  ((NeonCosmetics.LaneScene)i),
                        GetActive  = ()  => (int)NeonCosmetics.ActiveLaneScene,
                        BuildPreview = (idx, p) =>
                        {
                            // Mini battlefield lane strip showing the scene colour
                            Color[] ScenePrev = {
                                new Color(0.05f,0.06f,0.09f),  // DEFAULT
                                new Color(0.32f,0.10f,0.04f),  // EMBER
                                new Color(0.10f,0.22f,0.40f),  // FROST
                                new Color(0.20f,0.04f,0.32f),  // NEON
                                new Color(0.14f,0.04f,0.24f),  // VOID
                            };
                            var bgImg = p.gameObject.AddComponent<Image>();
                            Color bc = idx < ScenePrev.Length ? ScenePrev[idx] : ScenePrev[0];
                            bgImg.color = bc;
                            // Lane divider lines to suggest battlefield
                            for (int li = 1; li < 4; li++)
                            {
                                var ln = Mk(p, $"div{li}", new Vector2(0f, li * 0.25f), new Vector2(1f, li * 0.25f + 0.008f));
                                ln.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
                            }
                            // Unit silhouettes in lanes — shows what the scene looks like with troops
                            string[] silIds = { "drone", "trooper", "titan" };
                            float[]  silY   = { 0.62f, 0.37f, 0.12f };
                            float[]  silSz  = { 0.18f, 0.22f, 0.30f };
                            for (int si = 0; si < 3; si++)
                            {
                                var sGo = Mk(p, $"sil{si}", new Vector2(0.30f, silY[si]), new Vector2(0.30f + silSz[si], silY[si] + silSz[si]));
                                var sImg = sGo.AddComponent<RawImage>();
                                sImg.texture = NeonArt.Unit(ThemeLocale.ArtId(silIds[si]), true);
                                sImg.color = new Color(1f, 1f, 1f, 0.55f); sImg.raycastTarget = false;
                            }
                        }
                    };
                default: // TRAILS — fires on a match clear
                    return new CatData
                    {
                        Names = NeonCosmetics.GemTrailNames, Descs = NeonCosmetics.GemTrailDesc,
                        Rarities = _rarities[3], Costs = NeonCosmetics.GemTrailCost,
                        IsUnlocked = i => NeonCosmetics.IsUnlocked((NeonCosmetics.GemTrail)i),
                        TryUnlock  = i => NeonCosmetics.TryUnlock ((NeonCosmetics.GemTrail)i),
                        SetActive  = i => NeonCosmetics.SetActive ((NeonCosmetics.GemTrail)i),
                        GetActive  = ()  => (int)NeonCosmetics.ActiveGemTrail,
                        BuildPreview = (idx, p) =>
                        {
                            var a = p.gameObject.AddComponent<DeployFxPreviewAnim>();
                            a.FxIdx = idx; a.ThemeAccent = theme.Accent; a.TrailMode = true;
                            a.Setup();
                        }
                    };
            }
        }

        // ── card grid ────────────────────────────────────────────────────────────

        void BuildCards(Transform parent, CatData cat)
        {
            int   count = cat.Names.Length;
            int   rows  = Mathf.CeilToInt(count / 2f);
            float rowH  = 1f / rows;

            for (int i = 0; i < count; i++)
            {
                int   col = i % 2, row = i / 2;
                float x0  = col * 0.5f + 0.012f, x1 = x0 + 0.478f;
                float y1  = 1f - row * rowH, y0 = y1 - rowH + 0.014f;
                BuildCard(parent, i, x0, y0, x1, y1, cat);
            }
        }

        void BuildCard(Transform parent, int idx, float x0, float y0, float x1, float y1, CatData cat)
        {
            var    theme    = NeonTheme.Active;
            bool   unlocked = cat.IsUnlocked(idx);
            bool   active   = cat.GetActive() == idx;
            string rarity   = idx < cat.Rarities.Length ? cat.Rarities[idx] : "";
            bool   isLeg    = rarity == "LEGENDARY";
            bool   isEpic   = rarity == "EPIC";
            bool   isRare   = rarity == "RARE";

            Color rarityCol = isLeg ? LegCol : isEpic ? EpicCol : isRare ? RareCol : Color.clear;

            // ── Root container — no Image, layers added as children ──
            var cardGo = Mk(parent, $"card{idx}", new Vector2(x0, y0), new Vector2(x1, y1));

            // Layer 1: Outermost dark stroke
            Bdr(cardGo.transform, new Color(0.02f, 0.02f, 0.04f, 1f), 4f);

            // Layer 2: RARITY border — item quality as a colored ring, dimmed when locked
            if (active)
                Bdr(cardGo.transform, new Color(CtaGreen.r, CtaGreen.g, CtaGreen.b, 0.95f), 2.5f);
            else if (!string.IsNullOrEmpty(rarity))
                Bdr(cardGo.transform, new Color(rarityCol.r, rarityCol.g, rarityCol.b, unlocked ? 0.88f : 0.38f), 2.5f);

            // Layer 3: THEME accent border — always present as the inner ring
            float themeA = active ? 0.65f : unlocked ? 0.50f : 0.28f;
            Bdr(cardGo.transform, new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, themeA), 1.2f);

            // Layer 4: Neutral grey background — rarity identity lives in the border rings, not the bg
            Color cardBgCol = active
                ? new Color(0.10f, 0.14f, 0.11f)   // very subtle greenish hint only
                : !unlocked ? new Color(0.07f, 0.07f, 0.09f)  // slightly darker neutral when locked
                : new Color(0.10f, 0.11f, 0.15f);   // clean neutral dark grey for all rarities
            var bgGo = Mk(cardGo.transform, "bg", Vector2.zero, Vector2.one);
            bgGo.AddComponent<Image>().color = cardBgCol;

            // Layer 5: Bevel strips — subtle depth
            TopStrip(cardGo.transform, new Color(1f, 1f, 1f, active ? 0.16f : 0.09f), 3f);
            LeftStrip(cardGo.transform, new Color(1f, 1f, 1f, 0.05f), 2f);
            BotStrip(cardGo.transform, new Color(0f, 0f, 0f, active ? 0.52f : 0.40f), 4f);
            RightStrip(cardGo.transform, new Color(0f, 0f, 0f, 0.22f), 2f);

            // Animated pulsing glow on active card border
            if (active)
            {
                var glowBdr = BdrGo(cardGo.transform, CtaGreen, 2f);
                var ab = glowBdr.AddComponent<ActiveBorderAnim>();
                ab.AccentColor = CtaGreen;
            }

            // ── Preview box (recessed, left 42%) ──
            // Outer frame of preview (very dark border = sunken look)
            var prevFrame = Mk(cardGo.transform, "prevFrame", new Vector2(0.015f, 0.20f), new Vector2(0.515f, 0.975f));
            prevFrame.AddComponent<Image>().color = new Color(0.03f, 0.03f, 0.06f);
            // Inner sunken highlights: shadow on top/left (light from below = recessed)
            TopStrip(prevFrame.transform, new Color(0f, 0f, 0f, 0.65f), 3f);
            LeftStrip(prevFrame.transform, new Color(0f, 0f, 0f, 0.45f), 2f);
            BotStrip(prevFrame.transform, new Color(1f, 1f, 1f, 0.06f), 2f);
            RightStrip(prevFrame.transform, new Color(1f, 1f, 1f, 0.04f), 2f);

            // Actual preview content inside the frame
            var prevGo = Mk(prevFrame.transform, "prev", new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f));
            cat.BuildPreview(idx, prevGo.transform);

            // Lock overlay
            if (!unlocked)
            {
                var lockOv = Mk(prevFrame.transform, "lock", Vector2.zero, Vector2.one);
                lockOv.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.38f);
                Txt(lockOv.transform, "🔒", 0, 1, 0.28f, 0.72f, UIScale.FontH2, FontStyle.Bold,
                    new Color(0.55f, 0.58f, 0.65f), TextAnchor.MiddleCenter, false);
            }

            // Equipped micro-badge — top-left corner of preview frame
            if (active)
            {
                var eqGo = Mk(prevFrame.transform, "eq_badge", new Vector2(0.01f, 0.82f), new Vector2(0.72f, 0.99f));
                eqGo.AddComponent<Image>().color = new Color(0.04f, 0.22f, 0.08f, 0.92f);
                Bdr(eqGo.transform, new Color(CtaGreen.r, CtaGreen.g, CtaGreen.b, 0.80f), 1f);
                Txt(eqGo.transform, "✓ EQUIPPED", 0, 1, 0, 1, UIScale.FontTiny, FontStyle.Bold,
                    CtaGreen, TextAnchor.MiddleCenter, false);
            }

            // ── Right panel content ──
            // Name (top-right, bold white)
            Color nameCol = active ? CtaGreen : unlocked ? Color.white : new Color(0.38f, 0.40f, 0.48f);
            Txt(cardGo.transform, cat.Names[idx], 0.535f, 0.99f, 0.77f, 0.975f,
                UIScale.FontBody, FontStyle.Bold, nameCol, TextAnchor.MiddleLeft, false);

            // Rarity ribbon (below name) — solid colored pill, highly visible
            if (!string.IsNullOrEmpty(rarity))
            {
                Color rc = rarityCol;
                var ribbonGo = Mk(cardGo.transform, "ribbon", new Vector2(0.535f, 0.625f), new Vector2(0.90f, 0.765f));
                // Dark outer stroke on ribbon
                Bdr(ribbonGo.transform, new Color(0.01f, 0.01f, 0.02f, 1f), 2f);
                // Ribbon fill (saturated rarity color, not too dark)
                var ribBg = Mk(ribbonGo.transform, "rbg", Vector2.zero, Vector2.one);
                ribBg.AddComponent<Image>().color = new Color(rc.r * 0.22f, rc.g * 0.22f, rc.b * 0.22f, 1f);
                // Rarity color inner line
                Bdr(ribbonGo.transform, new Color(rc.r, rc.g, rc.b, 0.88f), 1f);
                // Top bevel highlight on ribbon
                TopStrip(ribbonGo.transform, new Color(rc.r * 0.6f + 0.4f, rc.g * 0.6f + 0.4f, rc.b * 0.6f + 0.4f, 0.30f), 2f);
                // Rarity text — bright, high contrast
                Txt(ribbonGo.transform, rarity, 0, 1, 0, 1, UIScale.FontTiny, FontStyle.Bold,
                    new Color(Mathf.Min(rc.r * 1.5f, 1f), Mathf.Min(rc.g * 1.5f, 1f), Mathf.Min(rc.b * 1.5f, 1f)),
                    TextAnchor.MiddleCenter, false);
            }

            // Description — solid dark bg behind text for max readability
            float descY1 = string.IsNullOrEmpty(rarity) ? 0.76f : 0.62f;
            var descBg = Mk(cardGo.transform, "descBg", new Vector2(0.525f, 0.30f), new Vector2(0.99f, descY1));
            descBg.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.40f);
            Color descCol = unlocked ? new Color(0.82f, 0.86f, 0.92f) : new Color(0.42f, 0.44f, 0.52f);
            Txt(descBg.transform, cat.Descs[idx], 0.04f, 0.97f, 0.05f, 0.94f,
                UIScale.FontSmall, FontStyle.Normal, descCol, TextAnchor.UpperLeft, false);

            // ── CTA button (bottom) — full CoC beveled button ──
            Color ctaBg, ctaTopHL, ctaBotSH, ctaLine, ctaTxt;
            string ctaStr;
            if (active)
            {
                ctaBg   = idx == 0 ? new Color(0.05f, 0.24f, 0.11f) : new Color(0.22f, 0.06f, 0.06f);
                ctaTopHL= new Color(0.25f, 1f,  0.45f, 0.22f);
                ctaBotSH= new Color(0f,    0f,   0f,   0.55f);
                ctaLine = idx == 0
                    ? new Color(CtaGreen.r, CtaGreen.g, CtaGreen.b, 0.88f)
                    : new Color(1f, 0.32f, 0.32f, 0.88f);
                ctaTxt  = idx == 0 ? CtaGreen : new Color(1f, 0.42f, 0.42f);
                ctaStr  = idx == 0 ? "✓  EQUIPPED" : "✗  UNEQUIP";
            }
            else if (unlocked)
            {
                ctaBg   = new Color(0.07f, 0.34f, 0.15f);
                ctaTopHL= new Color(0.50f, 1f,  0.60f, 0.30f);
                ctaBotSH= new Color(0f,    0f,   0f,   0.50f);
                ctaLine = new Color(CtaGreen.r, CtaGreen.g, CtaGreen.b, 1f);
                ctaTxt  = Color.white;
                ctaStr  = "▶  TAP TO EQUIP";
            }
            else
            {
                ctaBg   = new Color(0.24f, 0.16f, 0.03f);
                ctaTopHL= new Color(1f,   0.90f, 0.40f, 0.28f);
                ctaBotSH= new Color(0f,    0f,   0f,   0.50f);
                ctaLine = new Color(CtaGold.r, CtaGold.g, CtaGold.b, 0.92f);
                ctaTxt  = CtaGold;
                // The glyph comes out -- an animated coin goes in its place below, so the
                // price is stated in the same currency the player sees in the header.
                ctaStr  = $"UNLOCK   {cat.Costs[idx]}";
            }
            // 0.015..0.192 of the card was a thin strip. 0.26 of the card height clears a 44pt
            // target on a phone, which is what these have to be -- they are the only
            // controls on the screen that spend currency.
            var ctaGo = Mk(cardGo.transform, "cta", new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.28f));
            // CTA: dark outer stroke → color line → fill → bevel
            Bdr(ctaGo.transform, new Color(0.01f, 0.01f, 0.03f, 1f), 2.5f);
            Bdr(ctaGo.transform, new Color(ctaLine.r, ctaLine.g, ctaLine.b, ctaLine.a), 1f);
            var ctaBgGo = Mk(ctaGo.transform, "ctabg", Vector2.zero, Vector2.one);
            ctaBgGo.AddComponent<Image>().color = ctaBg;
            TopStrip(ctaGo.transform, ctaTopHL, 3f);
            BotStrip(ctaGo.transform, ctaBotSH, 3f);
            bool priced = ctaStr.StartsWith("UNLOCK");
            // A priced button carries the coin; EQUIPPED / UNEQUIP do not, so the label centres
            // on the whole button in those states rather than sitting off to one side.
            var ctaLbl = Txt(ctaGo.transform, ctaStr,
                             priced ? 0.20f : 0f, priced ? 0.94f : 1f, 0, 1,
                             UIScale.FontBody, FontStyle.Bold,
                             ctaTxt, TextAnchor.MiddleCenter, false);
            Outline(ctaLbl, 1.6f);   // the label sits on a saturated fill; an outline keeps it legible
            if (priced)
            {
                var coin = Mk(ctaGo.transform, "coin", new Vector2(0.055f, 0.20f), new Vector2(0.185f, 0.80f));
                coin.AddComponent<TokenIconAnim>();
            }

            if (active && idx > 0)
            {
                // UNEQUIP: reset to default skin (idx 0)
                var btn = cardGo.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    AudioManager.Play(AudioManager.Sfx.Equip, 0.8f, 0.85f);
                    Snap(cardGo);
                    cat.SetActive(0); SelectTab(_tab);
                });
                cardGo.AddComponent<ButtonFeel>();
            }
            else if (!active)
            {
                var btn = cardGo.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(() =>
                {
                    bool wasLocked = !cat.IsUnlocked(idx);
                    if (!cat.IsUnlocked(idx) && !cat.TryUnlock(idx))
                    {
                        AudioManager.Play(AudioManager.Sfx.Error, 0.7f);
                        return;
                    }
                    AudioManager.Play(wasLocked ? AudioManager.Sfx.Purchase : AudioManager.Sfx.Equip);
                    Snap(cardGo);
                    _tokenLabel.text = TokenStr();
                    cat.SetActive(idx);
                    var rt2 = cardGo.GetComponent<RectTransform>();
                    Vector2 burst = transform.InverseTransformPoint(rt2.TransformPoint(rt2.rect.center));
                    SelectTab(_tab);
                    if (wasLocked) StartCoroutine(PurchaseBurst(burst));
                });
                cardGo.AddComponent<ButtonFeel>();
            }
        }

        // ── purchase ceremony ────────────────────────────────────────────────────

        IEnumerator PurchaseBurst(Vector2 localPos)
        {
            // Gold screen flash
            var flashGo = Mk(transform, "flash", Vector2.zero, Vector2.one);
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.color = new Color(1f, 0.88f, 0.2f, 0f);
            flashImg.raycastTarget = false;

            float t = 0f;
            while (t < 0.35f)
            {
                t += Time.deltaTime;
                float a = t < 0.10f ? t / 0.10f * 0.38f : Mathf.Lerp(0.38f, 0f, (t - 0.10f) / 0.25f);
                flashImg.color = new Color(1f, 0.88f, 0.2f, Mathf.Clamp01(a));
                yield return null;
            }
            Destroy(flashGo);

            // Particle burst
            int pCount = 14;
            var burstRoot = Mk(transform, "burst", Vector2.zero, Vector2.one);

            var pGos = new GameObject[pCount];
            var vels  = new Vector2[pCount];
            var cols  = new Color[pCount];

            for (int i = 0; i < pCount; i++)
            {
                float ang   = i / (float)pCount * Mathf.PI * 2f + UnityEngine.Random.Range(-0.3f, 0.3f);
                float spd   = UnityEngine.Random.Range(60f, 140f);
                vels[i]     = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd);
                cols[i]     = Color.HSVToRGB(UnityEngine.Random.Range(0f, 1f), 0.3f, 1f);

                var pGo = new GameObject($"p{i}"); pGo.transform.SetParent(burstRoot.transform, false);
                var pRt = pGo.AddComponent<RectTransform>();
                pRt.anchorMin = pRt.anchorMax = Vector2.zero;
                pRt.pivot = new Vector2(0.5f, 0.5f);
                pRt.anchoredPosition = localPos;
                float sz = UnityEngine.Random.Range(4f, 11f);
                pRt.sizeDelta = new Vector2(sz, sz);
                var pImg = pGo.AddComponent<Image>();
                pImg.sprite = NeonArt.GlowCircle();
                pImg.color  = cols[i];
                pImg.raycastTarget = false;
                pGos[i] = pGo;
            }

            t = 0f;
            while (t < 0.60f)
            {
                t += Time.deltaTime;
                float prog = t / 0.60f;
                for (int i = 0; i < pCount; i++)
                {
                    if (pGos[i] == null) continue;
                    var pRt = pGos[i].GetComponent<RectTransform>();
                    pRt.anchoredPosition += vels[i] * Time.deltaTime;
                    vels[i] *= 1f - Time.deltaTime * 4f;
                    float alpha = Mathf.Clamp01(1f - prog * prog);
                    pGos[i].GetComponent<Image>().color = new Color(cols[i].r, cols[i].g, cols[i].b, alpha);
                }
                yield return null;
            }
            Destroy(burstRoot);
        }

        // ── static helpers ───────────────────────────────────────────────────────

        /// <summary>Gems to armory tokens. One rate, one tap per gem type, and the balance
        /// updates live so the effect of a conversion is immediate and obvious.</summary>
        void ShowConvertOverlay(Transform parent)
        {
            var theme = NeonTheme.Active;
            var ov = Mk(parent, "convOverlay", Vector2.zero, Vector2.one);
            var ovBg = ov.AddComponent<Image>();
            ovBg.color = new Color(0.01f, 0.02f, 0.03f, 0.94f);
            var ovBtn = ov.AddComponent<Button>(); ovBtn.transition = Selectable.Transition.None;
            ovBtn.onClick.AddListener(() => Destroy(ov));

            var card = Mk(ov.transform, "card", new Vector2(0.08f, 0.24f), new Vector2(0.92f, 0.76f));
            card.AddComponent<Image>().color = new Color(theme.BgCard.r * 1.25f,
                                                        theme.BgCard.g * 1.25f,
                                                        theme.BgCard.b * 1.25f, 1f);
            Bdr(card.transform, theme.Accent, 2.5f);
            card.AddComponent<Button>().transition = Selectable.Transition.None;  // eat taps

            Txt(card.transform, "CONVERT GEMS", 0.04f, 0.96f, 0.87f, 0.99f,
                UIScale.FontH2, FontStyle.Bold, theme.TextBright, TextAnchor.MiddleLeft, false);

            Txt(card.transform, "Rarer gems are worth more. Rate is per gem type.",
                0.04f, 0.96f, 0.79f, 0.87f, UIScale.FontSmall, FontStyle.Normal,
                new Color(0.62f, 0.72f, 0.80f), TextAnchor.MiddleLeft, false);

            var balIco = Mk(card.transform, "balico", new Vector2(0.36f, 0.025f), new Vector2(0.44f, 0.115f));
            balIco.AddComponent<TokenIconAnim>();
            Text balTxt = Txt(card.transform, TokenStr(), 0.45f, 0.96f, 0.02f, 0.12f,
                              UIScale.FontBody, FontStyle.Bold, CtaGold, TextAnchor.MiddleLeft, false);
            Outline(balTxt, 1.4f);

            var closeGo = Mk(card.transform, "x", new Vector2(0.88f, 0.86f), new Vector2(0.99f, 0.99f));
            closeGo.AddComponent<Image>().color = new Color(theme.BgCard.r * 2f,
                                                           theme.BgCard.g * 2f,
                                                           theme.BgCard.b * 2f, 1f);
            Txt(closeGo.transform, "X", 0, 1, 0, 1, UIScale.FontBody, FontStyle.Bold,
                theme.TextBright, TextAnchor.MiddleCenter, false);
            var clB = closeGo.AddComponent<Button>(); clB.transition = Selectable.Transition.None;
            clB.onClick.AddListener(() => Destroy(ov));

            // one row per gem kind
            int kinds = BoardModel.GemKindCount;
            var rowTexts = new Text[kinds];
            for (int i = 0; i < kinds; i++)
            {
                int gi = i;
                float y1 = 0.74f - i * 0.125f, y0 = y1 - 0.105f;
                var row = Mk(card.transform, $"conv{i}", new Vector2(0.05f, y0), new Vector2(0.95f, y1));
                row.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.30f);

                var icoGo = Mk(row.transform, "ic", new Vector2(0.02f, 0.15f), new Vector2(0.12f, 0.85f));
                var ico = icoGo.AddComponent<RawImage>();
                ico.texture = NeonArt.Gem(gi, NeonTheme.Active.GemStyle);
                ico.raycastTarget = false;

                // name + how many you hold
                rowTexts[gi] = Txt(row.transform, "", 0.145f, 0.50f, 0.42f, 1f,
                                   UIScale.FontSmall, FontStyle.Bold,
                                   theme.TextBright, TextAnchor.MiddleLeft, false);
                Outline(rowTexts[gi], 1.3f);
                // the rate for THIS gem, stated plainly
                var rateLbl = Txt(row.transform,
                    $"{PlayerProgress.ConvertCostOf(gi)}  \u2192  {PlayerProgress.ConvertYieldOf(gi)}",
                    0.145f, 0.50f, 0f, 0.46f, UIScale.FontSmall, FontStyle.Normal,
                    new Color(0.58f, 0.70f, 0.78f), TextAnchor.MiddleLeft, false);
                // rarity chip
                var tierGo = Mk(row.transform, "tier", new Vector2(0.505f, 0.24f), new Vector2(0.645f, 0.76f));
                tierGo.AddComponent<Image>().color = TierCol(gi) * new Color(1f, 1f, 1f, 0.22f);
                var tierTxt = Txt(tierGo.transform, PlayerProgress.ConvertTierOf(gi), 0, 1, 0, 1,
                                  UIScale.FontTiny, FontStyle.Bold, TierCol(gi),
                                  TextAnchor.MiddleCenter, false);

                var goBtn = Mk(row.transform, "go", new Vector2(0.66f, 0.14f), new Vector2(0.97f, 0.86f));
                goBtn.AddComponent<Image>().color = new Color(0.06f, 0.28f, 0.22f);
                Bdr(goBtn.transform, new Color(0.30f, 0.90f, 0.72f, 0.9f), 1.5f);
                var goTxt = Txt(goBtn.transform, $"+{PlayerProgress.ConvertYieldOf(gi)}", 0, 1, 0, 1,
                                UIScale.FontBody, FontStyle.Bold,
                                new Color(0.62f, 1f, 0.88f), TextAnchor.MiddleCenter, false);
                Outline(goTxt, 1.4f);
                var gb = goBtn.AddComponent<Button>(); gb.transition = Selectable.Transition.None;
                goBtn.AddComponent<ButtonFeel>();
                gb.onClick.AddListener(() =>
                {
                    if (PlayerProgress.ConvertToTokens(gi))
                    {
                        AudioManager.Play(AudioManager.Sfx.Purchase);
                        RefreshConvRows(rowTexts, balTxt);
                        if (_tokenLabel != null) _tokenLabel.text = TokenStr();
                        // Without this the only sign anything happened was a number quietly
                        // changing behind an overlay you cannot see. Now the row flashes, a
                        // +N floats up off it, and the coin in the header kicks.
                        _tokenIcon?.Celebrate();
                        StartCoroutine(FloatGain(row.transform,
                                                 $"+{PlayerProgress.ConvertYieldOf(gi)}"));
                        StartCoroutine(FlashRow(row.GetComponent<Image>()));
                        SelectTab(_tab);            // re-price every card against the new balance
                    }
                    else AudioManager.Play(AudioManager.Sfx.Error, 0.7f);
                });
            }
            RefreshConvRows(rowTexts, balTxt);
        }

        /// <summary>A "+25" that rises off the row it came from and fades.</summary>
        IEnumerator FloatGain(Transform parent, string label)
        {
            var go = new GameObject("gain");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.62f, 0.5f); rt.anchorMax = new Vector2(0.62f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(120f, 30f);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = UIScale.FontBody; t.fontStyle = FontStyle.Bold;
            t.alignment = TextAnchor.MiddleCenter; t.text = label;
            t.color = CtaGold; t.raycastTarget = false;
            Outline(t, 1.6f);

            for (float e = 0f; e < 0.85f; e += Time.unscaledDeltaTime)
            {
                float k = e / 0.85f;
                if (rt == null) yield break;
                rt.anchoredPosition = new Vector2(0f, k * 42f);
                t.color = new Color(CtaGold.r, CtaGold.g, CtaGold.b, 1f - k * k);
                rt.localScale = Vector3.one * (1f + (1f - k) * 0.25f);
                yield return null;
            }
            if (go) Destroy(go);
        }

        /// <summary>The converted row pulses once so the eye knows which one paid out.</summary>
        IEnumerator FlashRow(Image img)
        {
            if (img == null) yield break;
            Color from = new Color(0.30f, 0.90f, 0.72f, 0.55f);
            Color to   = new Color(0f, 0f, 0f, 0.30f);
            for (float e = 0f; e < 0.45f; e += Time.unscaledDeltaTime)
            {
                if (img == null) yield break;
                img.color = Color.Lerp(from, to, e / 0.45f);
                yield return null;
            }
            if (img != null) img.color = to;
        }

        /// <summary>Rarity colour for the gem tier chips — the same ladder the shop uses
        /// for item rarity, so COMMON/RARE/EPIC mean one thing across the whole screen.</summary>
        static Color TierCol(int gemIndex) => gemIndex switch
        {
            0 or 1 => GrayDim,
            2      => RareCol,
            3      => EpicCol,
            _      => LegCol,
        };

        void RefreshConvRows(Text[] rows, Text balance)
        {
            for (int i = 0; i < rows.Length; i++)
            {
                if (rows[i] == null) continue;
                int have = i < PlayerProgress.Currency.Length ? PlayerProgress.Currency[i] : 0;
                bool can  = PlayerProgress.CanConvert(i);   // per-gem threshold now
                rows[i].text  = $"{ThemeLocale.GemName(i)}   {have}";
                rows[i].color = can ? NeonTheme.Active.TextBright
                                    : new Color(0.45f, 0.48f, 0.55f);
            }
            if (balance != null) balance.text = TokenStr();
        }

        /// <summary>A 1px dark outline. Every label in this screen sits on a saturated fill
        /// or a busy preview, where a flat colour alone is hard to read -- this is the cheapest
        /// fix that works on all eight themes without picking a different text colour per theme.</summary>
        static void Outline(Text t, float px = 1.4f)
        {
            if (t == null) return;
            var o = t.gameObject.AddComponent<UnityEngine.UI.Outline>();
            o.effectColor    = new Color(0f, 0f, 0f, 0.85f);
            o.effectDistance = new Vector2(px, -px);
            o.useGraphicAlpha = false;
        }

        // the coin is drawn beside it now, so the glyph comes out of the string
        /// <summary>Fire the equip confirmation on a card. SelectTab rebuilds the grid right
        /// after, so the component is added to the card that was actually tapped and plays out
        /// on it before the rebuild replaces it.</summary>
        static void Snap(GameObject card)
        {
            if (card == null) return;
            var a = card.GetComponent<EquipSnapAnim>() ?? card.AddComponent<EquipSnapAnim>();
            a.RingColor = NeonTheme.Active.Accent;
            a.Play();
        }

        static string TokenStr() => $"{PlayerProgress.Tokens}  TOKENS";

        static void MkPreviewImg(Transform parent, Texture2D tex, Color tint, bool unlocked)
        {
            var go = new GameObject("trooper"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.1f, 0.06f); rt.anchorMax = new Vector2(0.9f, 0.94f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.color   = unlocked ? tint : new Color(tint.r * 0.3f, tint.g * 0.3f, tint.b * 0.3f, 0.5f);
            img.raycastTarget = false;
            var arf = go.AddComponent<AspectRatioFitter>();
            arf.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = 0.65f;
        }

        static void MkPreview3DImg(Transform parent, Texture2D tex, bool unlocked)
        {
            var go = new GameObject("model3d"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<RawImage>();
            img.texture = tex;
            img.color = unlocked ? Color.white : new Color(0.22f, 0.22f, 0.28f, 0.55f);
            img.raycastTarget = false;
            var arf = go.AddComponent<AspectRatioFitter>();
            arf.aspectMode  = AspectRatioFitter.AspectMode.FitInParent;
            arf.aspectRatio = 1f; // 128×128 square texture
        }

        static void AttachEffectVfx(int skin, Transform parent)
        {
            var root = new GameObject("vfx"); root.transform.SetParent(parent, false);
            var rRt  = root.AddComponent<RectTransform>();
            rRt.anchorMin = Vector2.zero; rRt.anchorMax = Vector2.one;
            // Expand 14px past the preview frame so effects bleed naturally past the border
            rRt.offsetMin = new Vector2(-14f, -14f); rRt.offsetMax = new Vector2(14f, 14f);

            Image MkImg(string n)
            {
                var g = new GameObject(n); g.transform.SetParent(root.transform, false);
                var r = g.AddComponent<RectTransform>();
                r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
                r.pivot = new Vector2(0.5f, 0.5f); r.sizeDelta = Vector2.zero;
                var im = g.AddComponent<Image>();
                im.color = Color.clear; im.raycastTarget = false;
                return im;
            }

            var anim = root.AddComponent<EffectPreviewAnim>();
            anim.Skin = skin;
            anim.VfxA = MkImg("A"); anim.VfxB = MkImg("B"); anim.VfxC = MkImg("C");
            anim.VfxD = MkImg("D"); anim.VfxE = MkImg("E");
            anim.VfxF = MkImg("F"); anim.VfxG = MkImg("G");
            anim.Phase = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
            anim.Setup();
        }

        static GameObject Mk(Transform parent, string name, Vector2 aMin, Vector2 aMax)
        {
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = aMin; rt.anchorMax = aMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            return go;
        }

        // Border that expands outward — SetAsFirstSibling so it draws behind children
        static GameObject BdrGo(Transform parent, Color col, float px = 1f)
        {
            var go = new GameObject("bdr"); go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-px, -px); rt.offsetMax = new Vector2(px, px);
            go.AddComponent<Image>().color = col;
            return go;
        }

        // Border without reordering — inserts at current child position (used for layered borders)
        static void Bdr(Transform parent, Color col, float px)
        {
            var go = new GameObject("b"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-px, -px); rt.offsetMax = new Vector2(px, px);
            go.AddComponent<Image>().color = col;
        }

        // Edge strips for bevel depth effect
        static void TopStrip(Transform p, Color c, float h)
        {
            var go = new GameObject("hl"); go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f); rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(0f, -h); rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        }
        static void BotStrip(Transform p, Color c, float h)
        {
            var go = new GameObject("sh"); go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(1f, 0f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(0f, h);
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        }
        static void LeftStrip(Transform p, Color c, float w)
        {
            var go = new GameObject("ll"); go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = new Vector2(0f, 1f);
            rt.offsetMin = Vector2.zero; rt.offsetMax = new Vector2(w, 0f);
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        }
        static void RightStrip(Transform p, Color c, float w)
        {
            var go = new GameObject("rl"); go.transform.SetParent(p, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 0f); rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(-w, 0f); rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.color = c; img.raycastTarget = false;
        }

        Text Txt(Transform parent, string text,
            float xMin, float xMax, float yMin, float yMax,
            int size, FontStyle style, Color col, TextAnchor align, bool raycast = true)
        {
            var go = new GameObject("t"); go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(xMin, yMin); rt.anchorMax = new Vector2(xMax, yMax);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = size; t.fontStyle = style;
            t.color = col; t.alignment = align; t.text = text;
            t.supportRichText = false; t.raycastTarget = raycast;
            if (size >= UIScale.FontH2) NeonUI.Title(t); else NeonUI.Shadow(t);
            return t;
        }
    }

    // ── Data container ────────────────────────────────────────────────────────────

    struct CatData
    {
        public string[]           Names, Descs, Rarities;
        public int[]              Costs;
        public Func<int, bool>    IsUnlocked, TryUnlock;
        public Action<int>        SetActive;
        public Func<int>          GetActive;
        public Action<int, Transform> BuildPreview;
    }

    // ── Animated active-card border ───────────────────────────────────────────────

    sealed class ActiveBorderAnim : MonoBehaviour
    {
        public Color AccentColor;
        Image _img;
        float _t;
        void Start()  { _img = GetComponent<Image>(); }
        void Update()
        {
            if (_img == null) return;
            _t += Time.deltaTime;
            float a = 0.50f + 0.50f * Mathf.Abs(Mathf.Sin(_t * 2.8f));
            _img.color = new Color(AccentColor.r, AccentColor.g, AccentColor.b, a);
        }
    }

    // ── Skin-color preview: orbiting sparks + glow pulse ─────────────────────────

    sealed class SkinColorPreviewAnim : MonoBehaviour
    {
        public Color Tint;
        public float Phase;
        Image   _glowBg;
        Image[] _sparks = new Image[5];
        float   _t;

        public void Setup()
        {
            var bgGo = new GameObject("gl"); bgGo.transform.SetParent(transform, false);
            bgGo.transform.SetAsFirstSibling();
            var bgRt = bgGo.AddComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one;
            bgRt.offsetMin = new Vector2(-6, -6); bgRt.offsetMax = new Vector2(6, 6);
            _glowBg = bgGo.AddComponent<Image>();
            _glowBg.sprite = NeonArt.GlowCircle();
            _glowBg.color = new Color(Tint.r, Tint.g, Tint.b, 0.06f);
            _glowBg.raycastTarget = false;

            for (int i = 0; i < 5; i++)
            {
                var sGo = new GameObject($"sp{i}"); sGo.transform.SetParent(transform, false);
                var sRt = sGo.AddComponent<RectTransform>();
                sRt.anchorMin = sRt.anchorMax = new Vector2(0.5f, 0.5f);
                sRt.pivot = new Vector2(0.5f, 0.5f);
                sRt.sizeDelta = new Vector2(5f, 5f);
                _sparks[i] = sGo.AddComponent<Image>();
                _sparks[i].sprite = NeonArt.SparkDot();
                _sparks[i].color  = Color.clear;
                _sparks[i].raycastTarget = false;
            }
        }

        void Update()
        {
            _t += Time.deltaTime;
            float fp = _t * 1.4f + Phase;
            _glowBg.color = new Color(Tint.r, Tint.g, Tint.b, 0.05f + 0.12f * Mathf.Abs(Mathf.Sin(fp * 1.8f)));
            for (int i = 0; i < 5; i++)
            {
                if (_sparks[i] == null) continue;
                float angle  = fp * 0.85f + i * (Mathf.PI * 2f / 5f);
                float radius = 17f + 4f * Mathf.Sin(fp * 1.2f + i);
                _sparks[i].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius * 0.55f);
                float cycle = (_t * 1.8f + i * 0.38f) % 1.2f / 1.2f;
                float sa = cycle < 0.4f ? cycle / 0.4f : Mathf.Clamp01(2f - cycle / 0.4f);
                float sz = 3f + 3f * Mathf.Sin(fp * 2.5f + i);
                _sparks[i].rectTransform.sizeDelta = new Vector2(sz, sz);
                _sparks[i].color = new Color(Mathf.Min(Tint.r * 1.3f, 1f), Mathf.Min(Tint.g * 1.3f, 1f), Mathf.Min(Tint.b * 1.3f, 1f), sa * 0.85f);
            }
        }
    }

    // ── Glow preview: pulsing rings ───────────────────────────────────────────────

    sealed class SkinGlowPreviewAnim : MonoBehaviour
    {
        public int   GlowIdx;
        public Color ThemeAccent;
        public float Phase;
        Image   _core;
        Image[] _rings = new Image[4];
        float   _t;
        static readonly float[] RS = { 14f, 24f, 36f, 50f };

        public void Setup()
        {
            // Trooper silhouette for spatial context — shows where rings sit on a unit
            var silGo = new GameObject("sil"); silGo.transform.SetParent(transform, false);
            silGo.transform.SetAsFirstSibling();
            var silRt = silGo.AddComponent<RectTransform>();
            silRt.anchorMin = new Vector2(0.22f, 0.15f); silRt.anchorMax = new Vector2(0.78f, 0.85f);
            silRt.offsetMin = silRt.offsetMax = Vector2.zero;
            var silImg = silGo.AddComponent<RawImage>();
            silImg.texture = NeonArt.Unit(ThemeLocale.ArtId("trooper"), true);
            silImg.color = new Color(1f, 1f, 1f, 0.35f); silImg.raycastTarget = false;

            var cGo = new GameObject("core"); cGo.transform.SetParent(transform, false);
            var cRt = cGo.AddComponent<RectTransform>();
            cRt.anchorMin = cRt.anchorMax = new Vector2(0.5f, 0.5f);
            cRt.pivot = new Vector2(0.5f, 0.5f); cRt.sizeDelta = new Vector2(9f, 9f);
            _core = cGo.AddComponent<Image>();
            _core.sprite = NeonArt.GlowCircle(); _core.raycastTarget = false;

            for (int i = 0; i < 4; i++)
            {
                var rGo = new GameObject($"r{i}"); rGo.transform.SetParent(transform, false);
                var rRt = rGo.AddComponent<RectTransform>();
                rRt.anchorMin = rRt.anchorMax = new Vector2(0.5f, 0.5f);
                rRt.pivot = new Vector2(0.5f, 0.5f); rRt.sizeDelta = new Vector2(RS[i], RS[i]);
                _rings[i] = rGo.AddComponent<Image>();
                _rings[i].sprite = NeonArt.GlowCircle();
                _rings[i].color  = Color.clear; _rings[i].raycastTarget = false;
            }
        }

        void Update()
        {
            if (_core == null) return;
            _t += Time.deltaTime;
            float fp = _t + Phase;
            Color gc = GlowIdx == 3 ? new Color(1f, 0.68f, 0.10f) : ThemeAccent;

            switch (GlowIdx)
            {
                case 0: // NONE
                    _core.color = new Color(0.28f, 0.28f, 0.28f, 0.5f);
                    for (int i = 0; i < 4; i++) _rings[i].color = Color.clear;
                    break;
                case 1: // SOFT
                    float sa = 0.38f + 0.25f * Mathf.Sin(fp * 1.5f);
                    _core.color = new Color(gc.r, gc.g, gc.b, 0.9f);
                    _rings[0].color = new Color(gc.r, gc.g, gc.b, sa * 0.5f);
                    _rings[1].color = new Color(gc.r, gc.g, gc.b, sa * 0.22f);
                    _rings[2].color = Color.clear; _rings[3].color = Color.clear;
                    break;
                case 2: // INTENSE
                    _core.color = Color.white;
                    for (int i = 0; i < 4; i++)
                    {
                        float ia  = (0.70f - i * 0.12f) + 0.22f * Mathf.Sin(fp * 2.2f + i * 0.8f);
                        float isc = 1f + 0.06f * Mathf.Sin(fp * 1.9f + i);
                        _rings[i].rectTransform.sizeDelta = new Vector2(RS[i] * isc, RS[i] * isc);
                        _rings[i].color = new Color(gc.r, gc.g, gc.b, Mathf.Clamp01(ia));
                    }
                    break;
                case 3: // PULSE
                    _core.color = new Color(gc.r, gc.g, gc.b, 0.88f);
                    for (int i = 0; i < 3; i++)
                    {
                        float c2 = (_t * 1.2f + i / 3f) % 1f;
                        _rings[i].rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(9f, 62f, c2);
                        _rings[i].color = new Color(gc.r, gc.g, gc.b, Mathf.Sin(c2 * Mathf.PI) * 0.7f);
                    }
                    _rings[3].color = Color.clear;
                    break;
            }
        }
    }

    // ── Board skin preview: 2×2 tile grid with shimmer ───────────────────────────

    sealed class BoardSkinPreviewAnim : MonoBehaviour
    {
        public int   SkinId;
        public float Phase;
        RawImage[] _tiles   = new RawImage[4];
        Image[]    _shimmer = new Image[2];
        float      _t;

        public void Setup()
        {
            var tex = NeonArt.EmptyCell(SkinId);
            float[] xs = { 0f, 0.5f, 0f,   0.5f };
            float[] ys = { 0.5f, 0.5f, 0f, 0f   };

            for (int i = 0; i < 4; i++)
            {
                var tGo = new GameObject($"t{i}"); tGo.transform.SetParent(transform, false);
                var tRt = tGo.AddComponent<RectTransform>();
                tRt.anchorMin = new Vector2(xs[i] + 0.025f, ys[i] + 0.025f);
                tRt.anchorMax = new Vector2(xs[i] + 0.475f, ys[i] + 0.475f);
                tRt.offsetMin = tRt.offsetMax = Vector2.zero;
                _tiles[i] = tGo.AddComponent<RawImage>();
                _tiles[i].texture = tex; _tiles[i].color = Color.white; _tiles[i].raycastTarget = false;
            }

            for (int i = 0; i < 2; i++)
            {
                var sGo = new GameObject($"sh{i}"); sGo.transform.SetParent(transform, false);
                var sRt = sGo.AddComponent<RectTransform>();
                sRt.anchorMin = new Vector2(-0.2f, 0f); sRt.anchorMax = new Vector2(0f, 1f);
                sRt.offsetMin = sRt.offsetMax = Vector2.zero;
                _shimmer[i] = sGo.AddComponent<Image>();
                _shimmer[i].sprite = NeonArt.ChromeSweep();
                _shimmer[i].color  = Color.clear; _shimmer[i].raycastTarget = false;
            }
        }

        void Update()
        {
            if (_tiles[0] == null) return;
            _t += Time.deltaTime;
            for (int i = 0; i < 4; i++)
            {
                float b = 0.82f + 0.18f * Mathf.Sin(_t * 0.8f + Phase + i * 0.7f);
                _tiles[i].color = new Color(b, b, b, 1f);
            }
            for (int i = 0; i < 2; i++)
            {
                float ph = (_t * 0.55f + i * 0.55f) % 1.3f;
                if (ph < 1f)
                {
                    float x = Mathf.Lerp(-0.2f, 1f, ph);
                    _shimmer[i].rectTransform.anchorMin = new Vector2(x, 0f);
                    _shimmer[i].rectTransform.anchorMax = new Vector2(x + 0.15f, 1f);
                    _shimmer[i].color = new Color(1f, 1f, 1f, Mathf.Sin(ph * Mathf.PI) * 0.28f);
                }
                else _shimmer[i].color = Color.clear;
            }
        }
    }

    // ── Effect VFX preview ────────────────────────────────────────────────────────

    sealed class EffectPreviewAnim : MonoBehaviour
    {
        public int   Skin;
        public Image VfxA, VfxB, VfxC, VfxD, VfxE, VfxF, VfxG;
        public float Phase;
        float _t;
        float W = 28f, H = 44f;

        System.Collections.IEnumerator Start()
        {
            yield return null;
            var rt = GetComponent<RectTransform>();
            float pw = rt.rect.width, ph = rt.rect.height;
            if (pw > 4f && ph > 4f)
            {
                W = pw * 0.50f;
                H = ph * 0.75f;
                Setup();
            }
        }

        public void Setup()
        {
            var gl = NeonArt.GlowCircle();
            var sp = NeonArt.SparkDot();
            var sw = NeonArt.ChromeSweep();

            switch ((NeonCosmetics.TroopSkin)Skin)
            {
                // ── originals ─────────────────────────────────────────────────
                case NeonCosmetics.TroopSkin.Inferno:
                    Str(VfxA); VfxA.sprite = gl;
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(8f, H*0.72f), new Vector2(-W*0.16f, 0f)); VfxB.sprite = gl;
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(6f, H*0.52f), new Vector2( W*0.16f, H*0.06f)); VfxC.sprite = gl;
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(6f, 6f), new Vector2(-W*0.15f, H*0.22f)); VfxD.sprite = sp;
                    PP(VfxE, new Vector2(0.5f,0f), new Vector2(W*1.1f, H*0.30f), new Vector2(0f, -H*0.50f)); VfxE.sprite = gl;
                    break;

                case NeonCosmetics.TroopSkin.Golden:
                    Str(VfxA); VfxA.sprite = gl;
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(10f,10f), Vector2.zero); VfxB.sprite = sp;
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(10f,10f), Vector2.zero); VfxC.sprite = sp;
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(10f,10f), Vector2.zero); VfxD.sprite = sp;
                    VfxE.rectTransform.anchorMin = Vector2.zero; VfxE.rectTransform.anchorMax = Vector2.one;
                    VfxE.rectTransform.offsetMin = new Vector2(-8,-8); VfxE.rectTransform.offsetMax = new Vector2(8,8);
                    VfxE.sprite = gl;
                    PP(VfxF, new Vector2(0.5f,0.5f), new Vector2(10f,10f), Vector2.zero); VfxF.sprite = sp;
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(10f,10f), Vector2.zero); VfxG.sprite = sp;
                    break;

                case NeonCosmetics.TroopSkin.Chrome:
                    Str(VfxA);                                                                      // silver body shimmer
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(7f,7f), Vector2.zero); VfxB.sprite = sp;  // specular glint 1
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(5f,5f), Vector2.zero); VfxC.sprite = sp;  // specular glint 2
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(W*0.8f,H*0.8f), Vector2.zero); VfxD.sprite = gl;  // inner aura
                    PP(VfxF, new Vector2(0.5f,0.5f), new Vector2(6f,6f), Vector2.zero); VfxF.sprite = sp;  // star flash
                    break;

                case NeonCosmetics.TroopSkin.Phantom:
                    Str(VfxA); VfxA.sprite = gl;
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(W*1.1f,H*1.1f), new Vector2(-14f,0f)); VfxB.sprite = gl;
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(W*0.9f,H*0.9f), new Vector2(-24f,0f)); VfxC.sprite = gl;
                    VfxD.rectTransform.anchorMin = Vector2.zero; VfxD.rectTransform.anchorMax = Vector2.one;
                    VfxD.rectTransform.offsetMin = new Vector2(-10,-10); VfxD.rectTransform.offsetMax = new Vector2(10,10);
                    VfxD.sprite = gl;
                    break;

                case NeonCosmetics.TroopSkin.Cosmic:
                    Str(VfxA); VfxA.sprite = gl;
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(10f,10f), Vector2.zero); VfxB.sprite = sp;
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(8f,8f),   Vector2.zero); VfxC.sprite = sp;
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(6f,6f),   Vector2.zero); VfxD.sprite = sp;
                    PP(VfxE, new Vector2(0.5f,0.5f), new Vector2(W*1.4f,H*1.4f), Vector2.zero); VfxE.sprite = gl;
                    PP(VfxF, new Vector2(0.5f,0.5f), new Vector2(8f,8f),   Vector2.zero); VfxF.sprite = sp;
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(6f,6f),   Vector2.zero); VfxG.sprite = sp;
                    break;

                // ── new skins ─────────────────────────────────────────────────
                case NeonCosmetics.TroopSkin.Blizzard:
                    Str(VfxA); VfxA.sprite = gl;  // full-body ice glow
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(8f,8f),   Vector2.zero); VfxB.sprite = sp;  // crystal 1
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(6f,6f),   Vector2.zero); VfxC.sprite = sp;  // crystal 2
                    PP(VfxD, new Vector2(0.5f,0f),   new Vector2(W*1.2f,H*0.18f), new Vector2(0f,-H*0.5f)); VfxD.sprite = gl;  // frost breath
                    PP(VfxE, new Vector2(0.5f,0.5f), new Vector2(6f,6f),   new Vector2(0f, H*0.55f)); VfxE.sprite = sp;  // snowflake above
                    PP(VfxF, new Vector2(0.5f,0.5f), new Vector2(7f,7f),   Vector2.zero); VfxF.sprite = sp;  // crystal 3
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(W*1.6f,H*1.6f), Vector2.zero); VfxG.sprite = gl;  // outer mist
                    break;

                case NeonCosmetics.TroopSkin.Neon:
                    Str(VfxA); VfxA.sprite = gl;  // neon body glow
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(W*1.1f,5f), Vector2.zero); VfxB.sprite = sw;  // horiz arc
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(5f,H*0.9f), Vector2.zero); VfxC.sprite = gl;  // vert arc
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(8f,8f),   Vector2.zero); VfxD.sprite = sp;  // spark node
                    PP(VfxE, new Vector2(0.5f,0.5f), new Vector2(W*1.3f,W*1.3f), new Vector2(0f,-H*0.4f)); VfxE.sprite = gl;  // base ring
                    PP(VfxF, new Vector2(0.5f,0.5f), new Vector2(5f,H*0.7f), new Vector2(W*0.5f,0f)); VfxF.sprite = gl;  // side arc
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(7f,7f),   new Vector2(0f, H*0.6f)); VfxG.sprite = sp;  // top spark
                    break;

                case NeonCosmetics.TroopSkin.Venom:
                    Str(VfxA); VfxA.sprite = gl;  // toxic body glow
                    PP(VfxB, new Vector2(0.5f,1f), new Vector2(5f,8f), new Vector2(-W*0.18f, H*0.3f)); VfxB.sprite = gl;  // drip 1
                    PP(VfxC, new Vector2(0.5f,1f), new Vector2(4f,6f), new Vector2( W*0.12f, H*0.25f)); VfxC.sprite = gl;  // drip 2
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(8f,8f), Vector2.zero); VfxD.sprite = gl;  // bubble pop
                    PP(VfxE, new Vector2(0.5f,0f), new Vector2(W*1.3f,H*0.20f), new Vector2(0f,-H*0.50f)); VfxE.sprite = gl;  // acid pool
                    PP(VfxF, new Vector2(0.5f,1f), new Vector2(4f,5f), new Vector2( W*0.22f, H*0.35f)); VfxF.sprite = gl;  // drip 3
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(W*0.9f,H*0.4f), new Vector2(0f, H*0.45f)); VfxG.sprite = gl;  // toxic cloud
                    break;

                case NeonCosmetics.TroopSkin.Shadow:
                    Str(VfxA); VfxA.sprite = gl;  // shadow aura
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(6f,H*0.55f), new Vector2(-W*0.3f,0f)); VfxB.sprite = gl;  // tendril L
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(6f,H*0.45f), new Vector2( W*0.3f,0f)); VfxC.sprite = gl;  // tendril R
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(W*0.85f,H*0.85f), new Vector2(-W*0.25f,0f)); VfxD.sprite = gl;  // shadow clone
                    PP(VfxE, new Vector2(0.5f,0f), new Vector2(W*1.8f,H*0.22f), new Vector2(0f,-H*0.50f)); VfxE.sprite = gl;  // floor mist
                    PP(VfxF, new Vector2(0.5f,0.5f), new Vector2(5f,H*0.35f), new Vector2(0f, H*0.55f)); VfxF.sprite = gl;  // tendril top
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(12f,12f), new Vector2(W*0.4f, H*0.3f)); VfxG.sprite = gl;  // dark orb
                    break;

                case NeonCosmetics.TroopSkin.Solar:
                    Str(VfxA); VfxA.sprite = gl;  // corona glow
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(6f,H*0.80f), new Vector2(-W*0.7f,0f)); VfxB.sprite = gl;  // flare L
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(6f,H*0.80f), new Vector2( W*0.7f,0f)); VfxC.sprite = gl;  // flare R
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(9f,9f), Vector2.zero); VfxD.sprite = sp;  // orbiting hot dot
                    PP(VfxE, new Vector2(0.5f,0f), new Vector2(W*1.4f,H*0.28f), new Vector2(0f,-H*0.50f)); VfxE.sprite = gl;  // magma pool
                    PP(VfxF, new Vector2(0.5f,0.5f), new Vector2(8f,8f), Vector2.zero); VfxF.sprite = sp;  // second hot dot
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(5f,H*0.55f), new Vector2(0f, H*0.55f)); VfxG.sprite = gl;  // top corona spike
                    break;

                case NeonCosmetics.TroopSkin.Storm:
                    Str(VfxA); VfxA.sprite = gl;  // electric aura
                    PP(VfxB, new Vector2(0.5f,0.5f), new Vector2(3f,H*0.70f), new Vector2(-W*0.45f,0f)); VfxB.sprite = gl;  // bolt L
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(3f,H*0.70f), new Vector2( W*0.45f,0f)); VfxC.sprite = gl;  // bolt R
                    PP(VfxD, new Vector2(0.5f,0.5f), new Vector2(W*1.5f,W*1.5f), Vector2.zero); VfxD.sprite = gl;  // thunder ring
                    PP(VfxE, new Vector2(0.5f,0f), new Vector2(W*1.1f,H*0.15f), new Vector2(0f,-H*0.50f)); VfxE.sprite = gl;  // static discharge
                    PP(VfxF, new Vector2(0.5f,0.5f), new Vector2(3f,H*0.50f), new Vector2(0f, H*0.60f)); VfxF.sprite = gl;  // bolt top
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(W*2f,W*2f), Vector2.zero); VfxG.sprite = gl;  // charge ring 2
                    break;

                case NeonCosmetics.TroopSkin.Bloodmoon:
                    PP(VfxA, new Vector2(0.5f,0.5f), new Vector2(W*2.4f,W*2.4f), new Vector2(0f,-H*0.1f)); VfxA.sprite = gl;  // moon glow
                    PP(VfxB, new Vector2(0.5f,1f), new Vector2(5f,9f), new Vector2(-W*0.18f, H*0.30f)); VfxB.sprite = gl;  // blood drip 1
                    PP(VfxC, new Vector2(0.5f,0.5f), new Vector2(10f,10f), new Vector2( W*0.35f, H*0.25f)); VfxC.sprite = gl;  // crimson orb
                    PP(VfxD, new Vector2(0.5f,0f), new Vector2(W*1.3f,H*0.22f), new Vector2(0f,-H*0.50f)); VfxD.sprite = gl;  // blood pool
                    Str(VfxE); VfxE.sprite = gl;  // red mist aura
                    PP(VfxF, new Vector2(0.5f,1f), new Vector2(4f,7f), new Vector2( W*0.20f, H*0.35f)); VfxF.sprite = gl;  // blood drip 2
                    PP(VfxG, new Vector2(0.5f,0.5f), new Vector2(W*1.6f,W*1.6f), Vector2.zero); VfxG.sprite = gl;  // lunar seal ring
                    break;
            }
        }

        void PP(Image img, Vector2 pivot, Vector2 sz, Vector2 pos)
        {
            img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            img.rectTransform.pivot     = pivot;
            img.rectTransform.sizeDelta = sz;
            img.rectTransform.anchoredPosition = pos;
        }

        void Str(Image img)
        {
            img.rectTransform.anchorMin = Vector2.zero; img.rectTransform.anchorMax = Vector2.one;
            img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
        }

        void Update()
        {
            if (VfxA == null) return;
            _t += Time.deltaTime;
            float fp = _t * 3.5f + Phase;

            switch ((NeonCosmetics.TroopSkin)Skin)
            {
                case NeonCosmetics.TroopSkin.Inferno:
                {
                    VfxA.color = new Color(1f, 0.26f, 0.02f, 0.22f + 0.16f * Mathf.Sin(fp * 1.8f));
                    float sbB = Mathf.Max(0f, Mathf.Sin(fp*2.3f)) * Mathf.Max(0f, Mathf.Sin(fp*7.1f));
                    Color crB = Color.Lerp(new Color(1f,0.70f,0.10f), new Color(1f,0.96f,0.80f), sbB*1.2f);
                    VfxB.color = new Color(crB.r,crB.g,crB.b, Mathf.Clamp01(0.72f+0.24f*Mathf.Sin(fp*2.3f)+0.22f*sbB));
                    float sbC = Mathf.Max(0f, Mathf.Sin(fp*1.9f+1.4f)) * Mathf.Max(0f, Mathf.Sin(fp*5.8f+2.1f));
                    Color crC = Color.Lerp(new Color(1f,0.46f,0.06f), new Color(1f,0.82f,0.40f), sbC);
                    VfxC.color = new Color(crC.r,crC.g,crC.b, Mathf.Clamp01(0.58f+0.30f*Mathf.Sin(fp*1.9f+1.4f)+0.16f*sbC));
                    float dc = (_t * 1.4f) % 1f;
                    VfxD.rectTransform.anchoredPosition = new Vector2(-W*0.16f, Mathf.Lerp(H*0.24f,-H*0.46f,dc));
                    VfxD.rectTransform.sizeDelta = new Vector2(7f-3f*dc, 7f+12f*dc);
                    Color dripC = Color.Lerp(new Color(1f,0.92f,0.35f), new Color(0.85f,0.18f,0.01f), dc);
                    VfxD.color = new Color(dripC.r,dripC.g,dripC.b, 0.92f*Mathf.Sin(dc*Mathf.PI));
                    float bub = Mathf.Max(0f,Mathf.Sin(fp*5.8f))*Mathf.Max(0f,Mathf.Sin(fp*8.3f+1.2f));
                    VfxE.color = new Color(1f,0.22f,0.02f, Mathf.Clamp01(0.40f+0.22f*Mathf.Sin(fp*1.4f+0.5f)+0.18f*bub));
                    break;
                }
                case NeonCosmetics.TroopSkin.Golden:
                {
                    VfxA.color = new Color(1f,0.82f,0.18f, 0.08f+0.20f*Mathf.Abs(Mathf.Sin(fp*4.2f)));
                    Image[] sps = { VfxB, VfxC, VfxD, VfxF, VfxG };
                    float[] off = { 0f, 0.20f, 0.40f, 0.60f, 0.80f };
                    for (int k = 0; k < 5; k++)
                    {
                        if (sps[k] == null) continue;
                        float cycle = (fp + off[k]) % 0.65f / 0.65f;
                        float sa = cycle < 0.35f ? cycle/0.35f : Mathf.Clamp01(2f-cycle/0.35f);
                        float sx = Mathf.Sin(fp*0.9f+k*1.26f)*W*0.42f;
                        float sy = Mathf.Cos(fp*0.7f+k*1.26f)*H*0.35f;
                        float sz = 4f+4f*Mathf.Sin(fp*3.2f+k);
                        sps[k].rectTransform.anchoredPosition = new Vector2(sx, sy);
                        sps[k].rectTransform.sizeDelta        = new Vector2(sz, sz);
                        sps[k].color = new Color(1f,0.92f,0.35f, sa*0.88f);
                    }
                    VfxE.color = new Color(1f,0.75f,0.10f, 0.04f+0.12f*Mathf.Abs(Mathf.Sin(fp*1.5f)));
                    break;
                }
                case NeonCosmetics.TroopSkin.Chrome:
                {
                    // Silver body shimmer
                    VfxA.color = new Color(0.85f,0.92f,1f, 0.06f+0.04f*Mathf.Sin(fp*1.5f));
                    // Cool inner aura
                    VfxD.color = new Color(0.72f,0.86f,1f, 0.04f+0.05f*Mathf.Abs(Mathf.Sin(fp*0.9f)));
                    // Two specular glints orbiting around the model — flash brightest when facing light
                    float g1 = fp * 1.1f;
                    VfxB.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(g1)*W*0.40f, Mathf.Sin(g1)*H*0.25f);
                    float flashA = Mathf.Max(0f, Mathf.Sin(g1*4f));
                    VfxB.rectTransform.sizeDelta = new Vector2(5f+7f*flashA, 5f+7f*flashA);
                    VfxB.color = new Color(1f,1f,1f, 0.22f+0.72f*flashA);
                    float g2 = fp * 1.1f + Mathf.PI;
                    VfxC.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(g2)*W*0.35f, Mathf.Sin(g2)*H*0.22f);
                    float flashB = Mathf.Max(0f, Mathf.Sin(g2*4f+0.9f));
                    VfxC.rectTransform.sizeDelta = new Vector2(4f+5f*flashB, 4f+5f*flashB);
                    VfxC.color = new Color(0.88f,0.94f,1f, 0.18f+0.58f*flashB);
                    // Brief star flash — teleports to a random model surface position each cycle
                    if (VfxF != null) {
                        float gsc = (_t*3.0f)%1f;
                        if (gsc < 0.04f) VfxF.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-W*0.35f,W*0.35f), UnityEngine.Random.Range(-H*0.35f,H*0.35f));
                        float starA = gsc < 0.18f ? Mathf.Sin(gsc/0.18f*Mathf.PI)*0.90f : 0f;
                        VfxF.rectTransform.sizeDelta = Vector2.one*(6f+8f*starA);
                        VfxF.color = new Color(1f,1f,1f, starA);
                    }
                    break;
                }
                case NeonCosmetics.TroopSkin.Phantom:
                {
                    float p  = Mathf.Sin(Time.time*2.0f+Phase);
                    float sp2 = Mathf.Sin(Time.time*1.3f+Phase*0.6f);
                    VfxA.color = new Color(0.65f,0.40f,1f, 0.12f+0.10f*p);
                    VfxB.color = new Color(0.55f,0.30f,0.95f, 0.24f+0.12f*p);
                    VfxC.color = new Color(0.45f,0.22f,0.80f, 0.12f+0.08f*sp2);
                    VfxD.color = new Color(0.68f,0.45f,1f, 0.07f+0.12f*Mathf.Abs(sp2));
                    VfxE.color = Color.clear;
                    break;
                }
                case NeonCosmetics.TroopSkin.Cosmic:
                {
                    VfxA.color = new Color(0.20f,0.05f,0.45f, 0.18f+0.12f*Mathf.Sin(fp*1.3f));
                    VfxE.color = new Color(0.35f,0.10f,0.65f, 0.06f+0.08f*Mathf.Sin(fp*0.7f));
                    Image[] stars = { VfxB, VfxC, VfxD, VfxF, VfxG };
                    float[] phases = { 0f, 0.4f, 0.8f, 1.2f, 1.6f };
                    float[] radii  = { W*0.5f, W*0.38f, W*0.6f, W*0.45f, W*0.3f };
                    for (int k = 0; k < 5; k++) {
                        if (stars[k] == null) continue;
                        float ang = fp*0.6f + phases[k];
                        float r   = radii[k] * (1f + 0.12f*Mathf.Sin(fp*1.4f+k));
                        float sz  = 4f+3f*Mathf.Sin(fp*2f+k);
                        stars[k].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang)*r, Mathf.Sin(ang)*r*0.5f);
                        stars[k].rectTransform.sizeDelta = new Vector2(sz,sz);
                        float cyc = (_t*1.2f+phases[k]*0.3f)%1f;
                        stars[k].color = new Color(0.75f,0.45f,1f, Mathf.Sin(cyc*Mathf.PI)*0.90f);
                    }
                    break;
                }
                // ── new skins ─────────────────────────────────────────────────
                case NeonCosmetics.TroopSkin.Blizzard:
                {
                    // Ice glow — cold blue-white pulse
                    VfxA.color = new Color(0.65f,0.88f,1f, 0.14f+0.12f*Mathf.Sin(fp*1.4f));
                    // Outer mist
                    if (VfxG != null) VfxG.color = new Color(0.80f,0.95f,1f, 0.04f+0.05f*Mathf.Sin(fp*0.8f));
                    // Three orbiting crystal shards
                    Image[] crystals = { VfxB, VfxC, VfxF };
                    for (int k = 0; k < 3; k++) {
                        if (crystals[k] == null) continue;
                        float ang = fp*0.9f + k*2.09f;
                        float r   = W*0.45f+W*0.10f*Mathf.Sin(fp*1.6f+k);
                        float sz  = 5f+2f*Mathf.Sin(fp*2.4f+k);
                        crystals[k].rectTransform.anchoredPosition = new Vector2(Mathf.Cos(ang)*r, Mathf.Sin(ang)*r*0.55f);
                        crystals[k].rectTransform.sizeDelta = new Vector2(sz,sz);
                        crystals[k].color = new Color(0.80f,0.95f,1f, 0.75f+0.20f*Mathf.Sin(fp*3f+k));
                    }
                    // Frost breath at feet — puffing width
                    float breath = 0.60f+0.40f*Mathf.Abs(Mathf.Sin(fp*0.9f));
                    VfxD.rectTransform.sizeDelta = new Vector2(W*1.2f*breath, H*0.18f*breath);
                    VfxD.color = new Color(0.75f,0.92f,1f, 0.30f*Mathf.Abs(Mathf.Sin(fp*0.9f)));
                    // Snowflake glint above — teleports to new spot each cycle
                    float sc = (_t*1.8f)%1f;
                    if (sc < 0.05f) VfxE.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-W*0.3f,W*0.3f), H*0.55f+UnityEngine.Random.Range(-4f,4f));
                    VfxE.color = new Color(1f,1f,1f, Mathf.Sin(sc*Mathf.PI)*0.90f);
                    break;
                }
                case NeonCosmetics.TroopSkin.Neon:
                {
                    // Electric flicker on body glow
                    float flicker = Mathf.Round(Mathf.Sin(fp*18f))*0.5f+0.5f;
                    VfxA.color = new Color(0.12f,1f,0.88f, 0.12f+0.10f*flicker);
                    // Horizontal arc sweeping up/down
                    float arc1 = Mathf.PingPong(_t*1.8f, 1f);
                    VfxB.rectTransform.anchoredPosition = new Vector2(0f, -H*0.5f+arc1*H);
                    VfxB.color = new Color(0.20f,1f,0.90f, Mathf.Sin(arc1*Mathf.PI)*0.65f*flicker);
                    // Vertical arc
                    VfxC.color = new Color(0.10f,0.90f,1f, 0.30f+0.40f*Mathf.Abs(Mathf.Sin(fp*4f))*flicker);
                    // Spark node — rapid teleport around body
                    float sn = (_t*4.5f)%1f;
                    if (sn < 0.1f) VfxD.rectTransform.anchoredPosition = new Vector2(UnityEngine.Random.Range(-W*0.4f,W*0.4f), UnityEngine.Random.Range(-H*0.4f,H*0.4f));
                    VfxD.color = new Color(1f,1f,0.80f, flicker*0.90f);
                    // Base energy circle pulse
                    float ringS = 1f+0.15f*Mathf.Sin(fp*3f);
                    VfxE.rectTransform.sizeDelta = new Vector2(W*1.3f*ringS,W*1.3f*ringS);
                    VfxE.color = new Color(0.10f,0.95f,1f, 0.12f+0.18f*Mathf.Abs(Mathf.Sin(fp*2f)));
                    // Side arc
                    if (VfxF != null) VfxF.color = new Color(0.10f,0.85f,0.95f, 0.25f*flicker+0.20f*Mathf.Abs(Mathf.Sin(fp*3.5f)));
                    // Overhead spark flash
                    if (VfxG != null) { float gsc = (_t*3.2f)%1f; VfxG.color = new Color(1f,1f,0.90f, gsc < 0.12f ? Mathf.Sin(gsc/0.12f*Mathf.PI)*0.85f : 0f); }
                    break;
                }
                case NeonCosmetics.TroopSkin.Venom:
                {
                    // Sickly green pulse
                    float bbl = Mathf.Max(0f,Mathf.Sin(fp*4.2f))*Mathf.Max(0f,Mathf.Sin(fp*6.8f+1f));
                    VfxA.color = new Color(0.20f,0.80f,0.08f, 0.16f+0.14f*Mathf.Sin(fp*1.2f));
                    // Acid drips — fall and fade
                    float dc1 = (_t*1.1f)%1f;
                    VfxB.rectTransform.anchoredPosition = new Vector2(-W*0.18f, Mathf.Lerp(H*0.30f,-H*0.50f,dc1));
                    VfxB.rectTransform.sizeDelta = new Vector2(5f-2f*dc1, 5f+10f*dc1);
                    VfxB.color = new Color(0.25f,0.90f,0.05f, 0.85f*Mathf.Sin(dc1*Mathf.PI));
                    float dc2 = ((_t+0.45f)*1.1f)%1f;
                    VfxC.rectTransform.anchoredPosition = new Vector2(W*0.12f, Mathf.Lerp(H*0.25f,-H*0.50f,dc2));
                    VfxC.rectTransform.sizeDelta = new Vector2(4f-1f*dc2, 4f+8f*dc2);
                    VfxC.color = new Color(0.30f,0.95f,0.08f, 0.75f*Mathf.Sin(dc2*Mathf.PI));
                    // Bubble pop at ground
                    float bpc = (_t*2.5f)%1f;
                    float bpsz = Mathf.Lerp(4f,14f,bpc);
                    VfxD.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(fp*0.7f)*W*0.25f, -H*0.45f);
                    VfxD.rectTransform.sizeDelta = new Vector2(bpsz,bpsz);
                    VfxD.color = new Color(0.40f,1f,0.10f, Mathf.Sin(bpc*Mathf.PI)*0.70f+0.18f*bbl);
                    // Acid pool
                    VfxE.color = new Color(0.20f,0.78f,0.04f, 0.30f+0.20f*Mathf.Sin(fp*1.5f)+0.15f*bbl);
                    // Third drip
                    if (VfxF != null) {
                        float dc3 = ((_t+0.80f)*1.1f)%1f;
                        VfxF.rectTransform.anchoredPosition = new Vector2(W*0.22f, Mathf.Lerp(H*0.35f,-H*0.50f,dc3));
                        VfxF.rectTransform.sizeDelta = new Vector2(3f, 4f+7f*dc3);
                        VfxF.color = new Color(0.28f,0.92f,0.06f, 0.65f*Mathf.Sin(dc3*Mathf.PI));
                    }
                    // Toxic cloud puff
                    if (VfxG != null) VfxG.color = new Color(0.18f,0.72f,0.05f, 0.08f+0.10f*Mathf.Abs(Mathf.Sin(fp*0.6f)));
                    break;
                }
                case NeonCosmetics.TroopSkin.Shadow:
                {
                    // Dark shadow aura — breathes in and out
                    VfxA.color = new Color(0.22f,0.08f,0.44f, 0.20f+0.15f*Mathf.Sin(fp*1.0f));
                    // Tendrils — sway slowly
                    float wL = Mathf.Sin(fp*0.6f)*W*0.08f;
                    float wR = Mathf.Sin(fp*0.7f+1.2f)*W*0.08f;
                    VfxB.rectTransform.anchoredPosition = new Vector2(-W*0.3f+wL, 0f);
                    VfxB.color = new Color(0.30f,0.10f,0.55f, 0.40f+0.20f*Mathf.Sin(fp*0.8f));
                    VfxC.rectTransform.anchoredPosition = new Vector2( W*0.3f+wR, 0f);
                    VfxC.color = new Color(0.28f,0.08f,0.50f, 0.35f+0.18f*Mathf.Sin(fp*0.9f+0.5f));
                    // Shadow clone — drifts left and fades
                    float drift = Mathf.Sin(fp*0.4f)*W*0.05f;
                    VfxD.rectTransform.anchoredPosition = new Vector2(-W*0.25f+drift, 0f);
                    VfxD.color = new Color(0.15f,0.05f,0.30f, 0.12f+0.08f*Mathf.Sin(fp*0.7f));
                    // Floor mist
                    VfxE.color = new Color(0.20f,0.06f,0.40f, 0.18f+0.12f*Mathf.Abs(Mathf.Sin(fp*0.5f)));
                    // Top tendril
                    if (VfxF != null) { VfxF.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(fp*0.5f)*W*0.10f, H*0.55f); VfxF.color = new Color(0.32f,0.10f,0.58f, 0.30f*Mathf.Abs(Mathf.Sin(fp*0.6f))); }
                    // Dark orb — floats and pulses
                    if (VfxG != null) { float orbA = fp*0.4f; VfxG.rectTransform.anchoredPosition = new Vector2(W*0.4f+Mathf.Sin(orbA)*W*0.1f, H*0.3f+Mathf.Cos(orbA)*H*0.08f); VfxG.color = new Color(0.45f,0.15f,0.70f, 0.55f+0.25f*Mathf.Sin(fp*1.4f)); }
                    break;
                }
                case NeonCosmetics.TroopSkin.Solar:
                {
                    // White-orange corona — intense pulse
                    float corona = 0.22f+0.18f*Mathf.Abs(Mathf.Sin(fp*2.1f));
                    VfxA.color = new Color(1f,0.68f,0.12f, corona);
                    // Flares — spike height oscillates
                    float flareH = 1f+0.30f*Mathf.Abs(Mathf.Sin(fp*1.7f));
                    VfxB.rectTransform.sizeDelta = new Vector2(6f, H*0.80f*flareH);
                    VfxB.color = new Color(1f,0.80f,0.20f, 0.55f+0.30f*Mathf.Sin(fp*1.7f));
                    VfxC.rectTransform.sizeDelta = new Vector2(6f, H*0.80f*(2f-flareH));
                    VfxC.color = new Color(1f,0.70f,0.10f, 0.50f+0.25f*Mathf.Sin(fp*1.7f+0.8f));
                    // Two hot dots orbit fast
                    float dotA1 = fp*1.8f;
                    VfxD.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(dotA1)*W*0.6f, Mathf.Sin(dotA1)*H*0.35f);
                    VfxD.color = new Color(1f,0.98f,0.80f, 0.90f+0.10f*Mathf.Sin(fp*4f));
                    if (VfxF != null) {
                        float dotA2 = fp*1.8f+Mathf.PI;
                        VfxF.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(dotA2)*W*0.6f, Mathf.Sin(dotA2)*H*0.35f);
                        VfxF.color = new Color(1f,0.92f,0.60f, 0.80f+0.15f*Mathf.Sin(fp*4f+1f));
                    }
                    // Magma pool
                    VfxE.color = new Color(1f,0.28f,0.04f, 0.35f+0.20f*Mathf.Sin(fp*1.2f));
                    // Top corona spike — flicker
                    if (VfxG != null) { VfxG.rectTransform.sizeDelta = new Vector2(5f, H*0.55f*(1f+0.25f*Mathf.Sin(fp*2.5f))); VfxG.color = new Color(1f,0.92f,0.40f, 0.45f+0.30f*Mathf.Abs(Mathf.Sin(fp*2.5f))); }
                    break;
                }
                case NeonCosmetics.TroopSkin.Storm:
                {
                    // Electric flicker
                    float bolt = Mathf.Round(Mathf.Sin(fp*14f)*0.5f+0.5f);
                    VfxA.color = new Color(0.65f,0.80f,1f, 0.14f+0.10f*bolt);
                    // Lightning bolts — strobe in/out
                    VfxB.color = new Color(0.75f,0.88f,1f, bolt*(0.70f+0.25f*Mathf.Sin(fp*5f)));
                    VfxC.color = new Color(0.70f,0.84f,1f, bolt*(0.60f+0.25f*Mathf.Sin(fp*5f+1f)));
                    // Thunder ring — expands outward rapidly
                    float ring1 = (_t*2.2f)%1f;
                    float ringSz1 = Mathf.Lerp(W*0.4f, W*2.0f, ring1);
                    VfxD.rectTransform.sizeDelta = new Vector2(ringSz1,ringSz1);
                    VfxD.color = new Color(0.72f,0.88f,1f, Mathf.Sin(ring1*Mathf.PI)*0.60f);
                    // Static discharge at feet
                    VfxE.color = new Color(0.60f,0.80f,1f, 0.18f+0.22f*Mathf.Abs(Mathf.Sin(fp*3f))*bolt);
                    // Top bolt
                    if (VfxF != null) VfxF.color = new Color(0.80f,0.92f,1f, bolt*(0.55f+0.30f*Mathf.Sin(fp*6f+0.5f)));
                    // Second charge ring — delayed
                    if (VfxG != null) {
                        float ring2 = ((_t+0.45f)*2.2f)%1f;
                        float ringSz2 = Mathf.Lerp(W*0.4f, W*2.0f, ring2);
                        VfxG.rectTransform.sizeDelta = new Vector2(ringSz2,ringSz2);
                        VfxG.color = new Color(0.68f,0.84f,1f, Mathf.Sin(ring2*Mathf.PI)*0.40f);
                    }
                    break;
                }
                case NeonCosmetics.TroopSkin.Bloodmoon:
                {
                    // Crimson moon glow — slow pulse
                    VfxA.color = new Color(0.80f,0.05f,0.10f, 0.22f+0.16f*Mathf.Sin(fp*0.7f));
                    // Blood drips
                    float bd1 = (_t*0.9f)%1f;
                    VfxB.rectTransform.anchoredPosition = new Vector2(-W*0.18f, Mathf.Lerp(H*0.30f,-H*0.50f,bd1));
                    VfxB.rectTransform.sizeDelta = new Vector2(5f-2f*bd1, 5f+14f*bd1);
                    VfxB.color = new Color(0.85f,0.04f,0.08f, 0.88f*Mathf.Sin(bd1*Mathf.PI));
                    // Orbiting crimson orb
                    float orbA = fp*0.7f;
                    VfxC.rectTransform.anchoredPosition = new Vector2(Mathf.Cos(orbA)*W*0.55f, Mathf.Sin(orbA)*H*0.30f);
                    float orbSz = 8f+3f*Mathf.Sin(fp*1.8f);
                    VfxC.rectTransform.sizeDelta = new Vector2(orbSz,orbSz);
                    VfxC.color = new Color(1f,0.10f,0.15f, 0.70f+0.20f*Mathf.Sin(fp*1.8f));
                    // Blood pool
                    VfxD.color = new Color(0.65f,0.02f,0.06f, 0.38f+0.18f*Mathf.Sin(fp*0.8f));
                    // Red mist aura
                    VfxE.color = new Color(0.70f,0.04f,0.08f, 0.06f+0.06f*Mathf.Sin(fp*0.6f));
                    // Second blood drip
                    if (VfxF != null) {
                        float bd2 = ((_t+0.55f)*0.9f)%1f;
                        VfxF.rectTransform.anchoredPosition = new Vector2(W*0.20f, Mathf.Lerp(H*0.35f,-H*0.50f,bd2));
                        VfxF.rectTransform.sizeDelta = new Vector2(4f-1.5f*bd2, 4f+11f*bd2);
                        VfxF.color = new Color(0.80f,0.04f,0.08f, 0.78f*Mathf.Sin(bd2*Mathf.PI));
                    }
                    // Lunar seal ring — thin red ring, slow expand
                    if (VfxG != null) {
                        float seal = ((_t*0.5f))%2f;
                        float sealSz = seal < 1f ? Mathf.Lerp(W*0.6f,W*1.8f,seal) : W*1.8f;
                        VfxG.rectTransform.sizeDelta = new Vector2(sealSz,sealSz);
                        VfxG.color = new Color(0.90f,0.08f,0.12f, seal < 1f ? Mathf.Sin(seal*Mathf.PI)*0.55f : 0f);
                    }
                    break;
                }
            }
        }
    }
}
