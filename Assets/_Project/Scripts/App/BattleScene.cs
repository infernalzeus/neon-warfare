using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using NW.Combat.Domain;
using NW.Core;
using NW.Data;
using NW.Net;

namespace NW.App
{
    /// <summary>
    /// Entry point for the Battle scene.
    ///
    /// v3 layout — vertical stack (doc 09/11) with currency sidebar:
    ///   ┌──────────────────────────────────┐
    ///   │ TOP BAR (36) — act · combo       │
    ///   ├──────────────────────────────────┤
    ///   │ BATTLEFIELD — air + 4 lanes      │
    ///   ├──────────────────────────────────┤
    ///   │ COMMAND STRIP (92) — deploy      │
    ///   ├─────────┬────────────────────────┤
    ///   │ WALLET  │ COMPILER BOARD (372)   │
    ///   │ SIDEBAR │ 8×8 match grid         │
    ///   └─────────┴────────────────────────┘
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public sealed class BattleScene : MonoBehaviour
    {
        const string SCENE_NAME = "Battle";
        // Battle layout bands, in the active CanvasScaler reference space (pixels).
        // Landscape defaults shown; ComputeResponsiveLayout() overrides these for portrait.
        int TOP_H    = 36;
        int STRIP_H  = 136;   // taller cards
        int BOARD_H  = 460;   // bigger match grid
        int SIDEBAR_W = 132;
        int WALLET_H  = 0;   // portrait: height of the top wallet strip; landscape: 0 (wallet is a side column)
        bool _portrait;      // true when the battle viewport is taller than wide
        // Board panel horizontal anchors (fraction of width): landscape = centered column,
        // portrait = near-full-width beside the wallet. Set in ComputeResponsiveLayout().
        float _boardMinX = 0.269f;
        float _boardMaxX = 0.800f;

        BattleSession    _session;
        BoardView        _boardView;
        BattlefieldView  _bfView;
        HUDView          _hudView;
        Font             _font;
        GameDatabase     _db;
        GameObject       _cameraGo;
        GameObject       _canvasGo;
        bool             _battleStarted;
        bool             _postGameDone;

        RectTransform _topRt, _bfRt, _stripRt, _boardRt, _sidebarRt;

        // In-game controls state
        bool _paused;
        bool _muted;
        GameObject _controlsPanel;
        GameObject _ctrlGear;   // opens the in-game menu
        GameObject _pauseOverlay;
        bool _endArmed;   // END BATTLE requires a 2nd tap to confirm
        readonly Text[] _ctrlBtnTexts = new Text[4];

        // Competitive overlay
        GameObject _compBar;
        Text       _compDotTxt;
        Text       _compMmrTxt;
        GameObject _hitFlashGo;
        Text       _hitFlashTxt;
        float      _hitFlashTimer;
        float      _dotPulseT;
        GameObject _matchResultGo;
        Image      _boardOverlayImg;
        RawImage   _boardBgImg;
        Text       _matchResultLbl;
        Text       _matchResultSub;
        const float HitFlashDur = 1.5f;

        // Ghost / ranked ladder
        /// <summary>Set by LevelSelectScreen.ChallengeGhost just before the level launch;
        /// consumed (and cleared) once by StartBattle.</summary>
        public static GhostRecord PendingGhost;
        GhostRecorder _recorder;
        GhostDriver   _ghostDriver;
        bool          _ghostMode;
        int           _ghostOppMmr;
        string        _ghostOppPilot;
        int           _ghostLevel;

        // ------------------------------------------------- auto-bootstrap ---

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoStart()
        {
            if (SceneManager.GetActiveScene().name != SCENE_NAME) return;
            var go = new GameObject("[BattleScene]");
            go.AddComponent<BattleScene>();
        }

        // ------------------------------------------------- lifecycle ---------

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);

            _font = NeonUI.LoadDisplayFont();

            Services.TryGet(out _db);
            if (_db == null) _db = Resources.Load<GameDatabase>("GameDatabase");

            BuildCanvas();
            ShowStartScreen();
        }

        public void Restart()
        {
            Time.timeScale = 1f;
            _paused = false;
            _muted  = false;
            AudioManager.Reset();
            StopAllCoroutines();
            if (_cameraGo) Destroy(_cameraGo);
            if (_canvasGo) Destroy(_canvasGo);
            _session        = null;
            _battleStarted  = false;
            _postGameDone   = false;
            _recorder       = null;
            _ghostDriver    = null;
            _ghostMode      = false;
            _hitFlashTimer  = 0f;
            _dotPulseT      = 0f;
            _compBar        = null;
            _hitFlashGo     = null;
            _matchResultGo  = null;
            _boardBgImg     = null;
            BuildCanvas();
            ShowStartScreen();
        }

        private void Update()
        {
            // Debug: Ctrl+T grants 500 tokens for cosmetics testing (works anywhere)
            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKeyDown(KeyCode.T))
                PlayerProgress.AddTokens(Mathf.Max(0, 10000 - PlayerProgress.Tokens));

            // Pause toggle while in battle
            if (_battleStarted && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P)))
                TogglePause();

            // Competitive / ghost overlay — ticks even while paused so dot pulses and flash fades
            if ((GameSettings.CompetitiveMode || _ghostMode) && _compBar != null)
                TickCompOverlay();

            if (!_battleStarted || _session == null || _paused) return;

            _session.Tick(Mathf.Min(Time.deltaTime * GameSettings.BattleSpeed, 0.08f));

            for (int i = 0; i < _session.PendingFx.Count; i++)
                _bfView.PlayFx(_session.PendingFx[i]);
            _session.PendingFx.Clear();

            // Drain crossover payloads this tick: transmit to a live peer, and/or record
            // them into the ghost (they become the opponent's strikes on replay).
            bool live = GameSettings.CompetitiveMode && NWNet.Inst != null && NWNet.Inst.IsConnected;
            foreach (var p in _session.PendingNetPayloads)
            {
                if (live) NWNet.Inst.SendCrossover(p.Kind, p.Lane, p.Damage);
                _recorder?.RecordCrossover(_session.Combat.TickCount, p);
            }
            _session.PendingNetPayloads.Clear();

            _boardView.Refresh();
            _bfView.Refresh();
            _hudView.Refresh();

            if (_session.Combat.Finished && !_postGameDone)
            {
                _postGameDone = true;
                bool playerWon = _session.Combat.Winner == Team.Player;
                FinalizeGhostRecording(playerWon);

                if (_ghostMode)
                {
                    int prev = PlayerProgress.MMR;
                    RankLadder.ApplyRanked(playerWon, _ghostOppMmr, _ghostOppPilot, _ghostLevel);
                    ShowMatchResult(playerWon, prev, PlayerProgress.MMR);
                }
                else if (GameSettings.CompetitiveMode)
                {
                    if (NWNet.Inst != null && !playerWon) NWNet.Inst.SendCoreDead();
                    int prev = PlayerProgress.MMR;
                    PlayerProgress.ApplyMatchResult(playerWon);
                    ShowMatchResult(playerWon, prev, PlayerProgress.MMR);
                }
                else
                {
                    PlayerProgress.AwardPostGame(_session.Resources, GameSettings.SelectedLevel);
                }
            }

            HandleHotkeys();
        }

        private void HandleHotkeys()
        {
            for (int i = 0; i < Mathf.Min(5, _session.DeployOptions.Count); i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) OnCardClicked(i);

            // Ground lanes Q/W/E, air on A. (R was the 4th ground lane before the
            // 3-lane layout; air now sits at CombatSim.AirLane == 3.)
            if (Input.GetKeyDown(KeyCode.Q)) OnLaneClicked(0);
            if (Input.GetKeyDown(KeyCode.W)) OnLaneClicked(1);
            if (Input.GetKeyDown(KeyCode.E)) OnLaneClicked(2);
            if (Input.GetKeyDown(KeyCode.A)) OnLaneClicked(CombatSim.AirLane);

            if (Input.GetKeyDown(KeyCode.M)) ToggleMusicMute();

            if (Input.GetKeyDown(KeyCode.F5) ||
                (_session.Combat.Finished && Input.GetKeyDown(KeyCode.Space)))
                Restart();
        }

        // ------------------------------------------------- deploy flow -------

        private void OnCardClicked(int card)
        {
            if (_session == null || _session.Combat.Finished) return;
            if (_hudView.SelectedCard == card)
            {
                _hudView.SetSelectedCard(-1);
                _bfView.SetLaneTargeting(false, false);
                AudioManager.Play(AudioManager.Sfx.Click);
                return;
            }
            if (!_session.CanAfford(_session.DeployOptions[card].cost))
            {
                _hudView.FlashAffordError(card);
                AudioManager.Play(AudioManager.Sfx.Error);
                return;
            }
            if (_session.CardCooldown(card) > 0f)
            {
                AudioManager.Play(AudioManager.Sfx.Error);
                return;
            }
            AudioManager.Play(AudioManager.Sfx.Click);
            _hudView.SetSelectedCard(card);

            var spec = _session.DeployOptions[card].spec;
            bool[] validLanes = null;
            // Turret/hacker: only highlight lanes where the player owns at least one pylon.
            if (spec.SpawnAtPylon && !spec.IsAir)
            {
                validLanes = new bool[CombatSim.LaneCount];
                for (int l = 0; l < CombatSim.GroundLanes; l++)
                    validLanes[l] = _session.Combat.PlayerOwnsAnyPylon(l);
            }
            _bfView.SetLaneTargeting(true, spec.IsAir, validLanes);
        }

        private void OnLaneClicked(int lane)
        {
            if (_session == null) return;
            int card = _hudView.SelectedCard;
            if (card < 0 || _session.Combat.Finished) return;

            bool isAir = _session.DeployOptions[card].spec.IsAir;
            if (isAir) lane = 0;
            else if (lane == 4) return;

            if (_session.Deploy(card, lane))
            {
                AudioManager.Play(AudioManager.Sfx.Deploy);
                _hudView.SetSelectedCard(-1);
                _bfView.SetLaneTargeting(false, false);
            }
            else
            {
                // Deploy failed (field full, turret blocked) — shake card only, not gem wallet.
                _hudView.FlashDeployError(card);
                AudioManager.Play(AudioManager.Sfx.Error);
                StartCoroutine(ShowDeployToast("LANE FULL"));
                _hudView.SetSelectedCard(-1);
                _bfView.SetLaneTargeting(false, false);
            }
        }

        // ----------------------------------------- canvas construction -------

        private void BuildCanvas()
        {
            var theme = NeonTheme.Active;

            _cameraGo = new GameObject("Camera");
            var cam = _cameraGo.AddComponent<Camera>();
            cam.clearFlags      = CameraClearFlags.SolidColor;
            cam.backgroundColor = theme.BgDeep;
            cam.orthographic    = true;
            cam.depth           = -1;
            _cameraGo.AddComponent<AudioListener>();

            if (FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }

            _canvasGo = new GameObject("Canvas");
            var canvas = _canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = _canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight  = 0.5f;
            // Responsive-both: adapt reference resolution + match axis to portrait/landscape at runtime.
            // (Overrides the static scaler values above; see ResponsiveCanvas.cs / portrait port.)
            _canvasGo.AddComponent<ResponsiveCanvas>();
            _canvasGo.AddComponent<GraphicRaycaster>();
            // Sweeps every label under the canvas and lets it shrink to fit its own box.
            // This is what makes the raised font scale safe -- the size becomes a maximum
            // rather than a demand, so nothing overflows onto its neighbours.
            _canvasGo.AddComponent<TextAutoFit>();
            _canvasGo.AddComponent<ZoomInspector>(); // scroll = zoom to cursor, MMB = pan, Home = reset

            ComputeResponsiveLayout(); // sets layout bands for portrait vs landscape before first use

            var bg = MakeStretch(_canvasGo.transform, "BG");
            bg.GetComponent<Image>().color = theme.BgDeep;
            // Atmospheric depth: gradient + vignette behind all game UI
            ScreenBackground.Attach(bg.GetComponent<RectTransform>());

            // Battlefield background image — bottom panel area only, full width, always 100% opacity.
            // Visible in the left/right margins beside the board grid and anywhere the board panel is clear.
            // Does NOT cover the battlefield unit area above the command strip.
            {
                var bgNames = new[] {
                    "battlefield","battlefield_purple","battlefield_green","battlefield_crimson",
                    "battlefield_ghost","battlefield_sakura","battlefield_solar","battlefield_dawn",
                };
                var boardBgTex = ArtLoader.BgTex(bgNames[Mathf.Clamp(GameSettings.ThemeIndex, 0, 7)]);
                if (boardBgTex != null)
                {
                    var bbGo = new GameObject("Board_BgImage");
                    bbGo.transform.SetParent(_canvasGo.transform, false);
                    bbGo.transform.SetSiblingIndex(1);
                    var bbRt = bbGo.AddComponent<RectTransform>();
                    // Anchor to bottom, full width, exactly board panel height
                    bbRt.anchorMin = new Vector2(0f, 0f); bbRt.anchorMax = new Vector2(1f, 0f);
                    bbRt.pivot = new Vector2(0.5f, 0f);
                    bbRt.anchoredPosition = Vector2.zero;
                    bbRt.sizeDelta = new Vector2(0f, BOARD_H);
                    var bbImg = bbGo.AddComponent<RawImage>();
                    bbImg.texture = boardBgTex; bbImg.raycastTarget = false;
                    bbImg.color   = new Color(1f, 1f, 1f, Mathf.Clamp(GameSettings.BoardBgOpacity, 0.05f, 1f));
                    _boardBgImg   = bbImg;
                    // Crop texture to fill width without distortion (fill width, crop height from top)
                    float texAR    = 1536f / 640f;
                    float dispAR   = 1920f / BOARD_H;
                    float uvH      = texAR / dispAR;
                    float uvV      = (1f - uvH) * 0.55f; // show lower portion (ground level)
                    bbImg.uvRect   = new Rect(0f, uvV, 1f, Mathf.Min(uvH, 1f));
                }
            }

            // Top bar (minimal — just act/combo)
            var topGo = new GameObject("TopBar");
            topGo.transform.SetParent(_canvasGo.transform, false);
            var topRt = topGo.AddComponent<RectTransform>();
            topRt.anchorMin = new Vector2(0, 1); topRt.anchorMax = Vector2.one;
            topRt.pivot = new Vector2(0.5f, 1);
            topRt.anchoredPosition = Vector2.zero;
            topRt.sizeDelta = new Vector2(0, TOP_H);
            topGo.AddComponent<Image>().color = new Color(theme.BgPanel.r, theme.BgPanel.g, theme.BgPanel.b, 0.96f);
            // Accent bottom edge
            var topEdge = new GameObject("edge"); topEdge.transform.SetParent(topGo.transform, false);
            var topEdgeRt = topEdge.AddComponent<RectTransform>();
            topEdgeRt.anchorMin = new Vector2(0f, 0f); topEdgeRt.anchorMax = new Vector2(1f, 0f);
            topEdgeRt.pivot = new Vector2(0.5f, 0f); topEdgeRt.anchoredPosition = Vector2.zero;
            topEdgeRt.sizeDelta = new Vector2(0f, 1f);
            topEdge.AddComponent<Image>().color = new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.50f);

            // Currency wallet sidebar (bottom-left)
            var sidebarGo = new GameObject("CurrencySidebar");
            sidebarGo.transform.SetParent(_canvasGo.transform, false);
            var sidebarRt = sidebarGo.AddComponent<RectTransform>();
            if (_portrait)
            {
                // Portrait: wallet is a full-width strip directly under the top bar.
                sidebarRt.anchorMin = new Vector2(0f, 1f); sidebarRt.anchorMax = new Vector2(1f, 1f);
                sidebarRt.pivot = new Vector2(0.5f, 1f);
                sidebarRt.anchoredPosition = new Vector2(0f, -TOP_H);
                sidebarRt.sizeDelta = new Vector2(0f, WALLET_H);
            }
            else
            {
                // Landscape: wallet is a column in the bottom-left margin.
                sidebarRt.anchorMin = Vector2.zero; sidebarRt.anchorMax = Vector2.zero;
                sidebarRt.pivot = Vector2.zero;
                sidebarRt.anchoredPosition = Vector2.zero;
                sidebarRt.sizeDelta = new Vector2(SIDEBAR_W, BOARD_H);
            }
            sidebarGo.AddComponent<Image>().color = theme.BgPanel;

            // Match board — centered in the non-wallet area, leaving ~20% margins on each side
            // so the background image shows beside the grid. Board tiles are always 100% opaque.
            // Layout (reference 1920): wallet 7% | left-bg 20% | board 53% | right-bg 20%
            var boardGo = new GameObject("BoardPanel");
            boardGo.transform.SetParent(_canvasGo.transform, false);
            var boardRt = boardGo.AddComponent<RectTransform>();
            boardRt.anchorMin = new Vector2(_boardMinX, 0f); boardRt.anchorMax = new Vector2(_boardMaxX, 0f);
            boardRt.pivot = new Vector2(0.5f, 0f);
            boardRt.anchoredPosition = Vector2.zero;
            boardRt.sizeDelta = new Vector2(0f, BOARD_H);
            boardGo.AddComponent<Image>().color = Color.clear;

            // No color overlay on the board — the background image shows in the margins,
            // and tile cells carry their own solid backgrounds.

            // Command strip (above board)
            var stripGo = new GameObject("CommandStrip");
            stripGo.transform.SetParent(_canvasGo.transform, false);
            var stripRt = stripGo.AddComponent<RectTransform>();
            stripRt.anchorMin = new Vector2(0, 0); stripRt.anchorMax = new Vector2(1, 0);
            stripRt.pivot = new Vector2(0.5f, 0);
            stripRt.anchoredPosition = new Vector2(0, BOARD_H);
            stripRt.sizeDelta = new Vector2(0, STRIP_H);
            // An Image with no sprite AND no colour renders as opaque WHITE. This strip is
            // created before the HUD draws its cards over it, so it flashed as a white box
            // across the bottom of the screen on every load.
            stripGo.AddComponent<Image>().color =
                new Color(theme.BgPanel.r, theme.BgPanel.g, theme.BgPanel.b, 0.95f);

            // Battlefield (fills remaining space between top bar and command strip)
            var bfGo = new GameObject("BattlefieldPanel");
            bfGo.transform.SetParent(_canvasGo.transform, false);
            var bfRt = bfGo.AddComponent<RectTransform>();
            bfRt.anchorMin = Vector2.zero; bfRt.anchorMax = Vector2.one;
            bfRt.offsetMin = new Vector2(0, BOARD_H + STRIP_H + 2);
            bfRt.offsetMax = new Vector2(0, -TOP_H - WALLET_H - 2); // portrait: clear the top wallet strip
            bfGo.AddComponent<Image>().color = new Color(theme.BgPanel.r, theme.BgPanel.g, theme.BgPanel.b, 0.42f);

            // Themed accent border around the battlefield
            AddThemeBorder(bfGo.transform, theme.Accent);

            _bfView               = bfGo.AddComponent<BattlefieldView>();
            _bfView.OnLaneClicked = OnLaneClicked;

            _hudView               = stripGo.AddComponent<HUDView>();
            _hudView.OnCardClicked = OnCardClicked;

            _boardView = boardGo.AddComponent<BoardView>();

            _topRt     = topRt;
            _bfRt      = bfRt;
            _stripRt   = stripRt;
            _boardRt   = boardRt;
            _sidebarRt = sidebarRt;

            BuildIngameControls();
        }

        // ---------------------------------------- in-game controls ----------

        static void AddThemeBorder(Transform parent, Color accent)
        {
            Color thin = new Color(accent.r, accent.g, accent.b, 0.60f);

            // Four thin edge lines
            (Vector2 aMin, Vector2 aMax, Vector2 offMin, Vector2 offMax)[] edges =
            {
                (new Vector2(0,1), new Vector2(1,1), new Vector2(0,-2), new Vector2(0, 0)), // top
                (new Vector2(0,0), new Vector2(1,0), new Vector2(0, 0), new Vector2(0, 2)), // bottom
                (new Vector2(0,0), new Vector2(0,1), new Vector2(0, 0), new Vector2(2, 0)), // left
                (new Vector2(1,0), new Vector2(1,1), new Vector2(-2,0), new Vector2(0, 0)), // right
            };
            foreach (var (aMin, aMax, offMin, offMax) in edges)
            {
                var g = new GameObject("bord"); g.transform.SetParent(parent, false);
                g.transform.SetAsFirstSibling();
                var r = g.AddComponent<RectTransform>();
                r.anchorMin = aMin; r.anchorMax = aMax;
                r.offsetMin = offMin; r.offsetMax = offMax;
                g.AddComponent<Image>().color = thin;
            }

            // Corner L-brackets: 20px horizontal + vertical lines
            (float px, float py, float hx0, float hy0, float hx1, float hy1,
                                  float vx0, float vy0, float vx1, float vy1)[] brackets =
            {
                (0,1,  0,-3,  20,-3,  0,-3,  0,-23),   // TL
                (1,1, -20,-3,  0,-3, -3,-3, -3,-23),   // TR
                (0,0,  0, 3,  20, 3,  0, 3,  0, 23),   // BL
                (1,0, -20, 3,  0, 3, -3, 3, -3, 23),   // BR
            };
            foreach (var b in brackets)
            {
                void Mk(float x0, float y0, float x1, float y1)
                {
                    var g = new GameObject("brk"); g.transform.SetParent(parent, false);
                    g.transform.SetAsFirstSibling();
                    var r = g.AddComponent<RectTransform>();
                    r.anchorMin = r.anchorMax = new Vector2(b.px, b.py);
                    r.pivot = new Vector2(b.px, b.py);
                    r.offsetMin = new Vector2(Mathf.Min(x0,x1), Mathf.Min(y0,y1));
                    r.offsetMax = new Vector2(Mathf.Max(x0,x1)+2, Mathf.Max(y0,y1)+2);
                    g.AddComponent<Image>().color = thin;
                }
                Mk(b.hx0, b.hy0, b.hx1, b.hy1);
                Mk(b.vx0, b.vy0, b.vx1, b.vy1);
            }
        }

        private void BuildIngameControls()
        {
            var theme = NeonTheme.Active;
            // These were four 54x28 buttons with 9pt labels crammed into the top-right corner --
            // about 20pt on a phone, less than half a tap target, and they sat on top of the
            // battlefield. They now live in a centred modal behind one gear button, the same
            // pattern the level select, settings and theme picker all use.
            _ctrlGear = new GameObject("IngameGear");
            _ctrlGear.transform.SetParent(_canvasGo.transform, false);
            var gRt = _ctrlGear.AddComponent<RectTransform>();
            gRt.anchorMin = gRt.anchorMax = new Vector2(1, 1);
            gRt.pivot = new Vector2(1, 1);
            // TOP_H is 36 and the wallet strip below it is another 92, so -(TOP_H + 10)
            // put this button straight through the currency chips. It sits under the
            // wallet now, floating over the top-right of the battlefield.
            gRt.anchoredPosition = new Vector2(-12, -(TOP_H + WALLET_H + 8));
            gRt.sizeDelta = new Vector2(120, 110);          // 44pt tap target
            var gImg = _ctrlGear.AddComponent<Image>();
            gImg.color = new Color(theme.BgCard.r, theme.BgCard.g, theme.BgCard.b, 1f);
            var gBrd = new GameObject("brd"); gBrd.transform.SetParent(_ctrlGear.transform, false);
            gBrd.transform.SetAsFirstSibling();
            var gBrdRt = gBrd.AddComponent<RectTransform>();
            gBrdRt.anchorMin = Vector2.zero; gBrdRt.anchorMax = Vector2.one;
            gBrdRt.offsetMin = new Vector2(-2, -2); gBrdRt.offsetMax = new Vector2(2, 2);
            gBrd.AddComponent<Image>().color = theme.Accent;
            var gTxtGo = new GameObject("txt"); gTxtGo.transform.SetParent(_ctrlGear.transform, false);
            var gTxtRt = gTxtGo.AddComponent<RectTransform>();
            gTxtRt.anchorMin = Vector2.zero; gTxtRt.anchorMax = Vector2.one;
            gTxtRt.offsetMin = gTxtRt.offsetMax = Vector2.zero;
            var gTxt = gTxtGo.AddComponent<Text>();
            gTxt.font = _font; gTxt.fontSize = 30; gTxt.fontStyle = FontStyle.Bold;
            gTxt.color = theme.TextBright; gTxt.alignment = TextAnchor.MiddleCenter;
            gTxt.text = "MENU"; gTxt.supportRichText = false; gTxt.raycastTarget = false;
            var gBtn = _ctrlGear.AddComponent<Button>();
            gBtn.transition = Selectable.Transition.None;
            gBtn.onClick.AddListener(() =>
            {
                bool open = !_controlsPanel.activeSelf;
                _controlsPanel.SetActive(open);
                if (open) _controlsPanel.transform.SetAsLastSibling();
            });

            _controlsPanel = new GameObject("IngameControls");
            _controlsPanel.transform.SetParent(_canvasGo.transform, false);
            var ctrlRt = _controlsPanel.AddComponent<RectTransform>();
            ctrlRt.anchorMin = Vector2.zero; ctrlRt.anchorMax = Vector2.one;
            ctrlRt.offsetMin = ctrlRt.offsetMax = Vector2.zero;
            var ctrlScrim = _controlsPanel.AddComponent<Image>();
            ctrlScrim.color = new Color(theme.BgDeep.r * 0.4f, theme.BgDeep.g * 0.4f,
                                        theme.BgDeep.b * 0.4f, 0.92f);
            var ctrlScrimBtn = _controlsPanel.AddComponent<Button>();
            ctrlScrimBtn.transition = Selectable.Transition.None;
            ctrlScrimBtn.onClick.AddListener(() => _controlsPanel.SetActive(false));

            var cCardBrd = new GameObject("cardbrd");
            cCardBrd.transform.SetParent(_controlsPanel.transform, false);
            var cbRt = cCardBrd.AddComponent<RectTransform>();
            cbRt.anchorMin = cbRt.anchorMax = new Vector2(0.5f, 0.5f);
            cbRt.pivot = new Vector2(0.5f, 0.5f);
            cbRt.anchoredPosition = Vector2.zero;
            cbRt.sizeDelta = new Vector2(468, 428);
            cCardBrd.AddComponent<Image>().color = theme.Accent;

            var cCard = new GameObject("card");
            cCard.transform.SetParent(_controlsPanel.transform, false);
            var ccRt = cCard.AddComponent<RectTransform>();
            ccRt.anchorMin = ccRt.anchorMax = new Vector2(0.5f, 0.5f);
            ccRt.pivot = new Vector2(0.5f, 0.5f);
            ccRt.anchoredPosition = Vector2.zero;
            ccRt.sizeDelta = new Vector2(460, 420);
            cCard.AddComponent<Image>().color = new Color(theme.BgCard.r * 1.2f,
                                                          theme.BgCard.g * 1.2f,
                                                          theme.BgCard.b * 1.2f, 1f);
            cCard.AddComponent<Button>().transition = Selectable.Transition.None;

            var cTitleGo = new GameObject("title"); cTitleGo.transform.SetParent(cCard.transform, false);
            var cTitleRt = cTitleGo.AddComponent<RectTransform>();
            cTitleRt.anchorMin = new Vector2(0f, 1f); cTitleRt.anchorMax = new Vector2(1f, 1f);
            cTitleRt.pivot = new Vector2(0.5f, 1f);
            cTitleRt.anchoredPosition = new Vector2(0f, -10f);
            cTitleRt.sizeDelta = new Vector2(-32f, 44f);
            var cTitle = cTitleGo.AddComponent<Text>();
            cTitle.font = _font; cTitle.fontSize = 30; cTitle.fontStyle = FontStyle.Bold;
            cTitle.color = theme.TextBright; cTitle.alignment = TextAnchor.MiddleLeft;
            cTitle.text = "MENU"; cTitle.supportRichText = false; cTitle.raycastTarget = false;

            var cClose = new GameObject("close"); cClose.transform.SetParent(cCard.transform, false);
            var cClRt = cClose.AddComponent<RectTransform>();
            cClRt.anchorMin = cClRt.anchorMax = new Vector2(1f, 1f);
            cClRt.pivot = new Vector2(1f, 1f);
            cClRt.anchoredPosition = new Vector2(-10f, -8f);
            cClRt.sizeDelta = new Vector2(64f, 60f);
            cClose.AddComponent<Image>().color = new Color(theme.BgCard.r * 1.8f,
                                                           theme.BgCard.g * 1.8f,
                                                           theme.BgCard.b * 1.8f, 1f);
            var cClBtn = cClose.AddComponent<Button>();
            cClBtn.transition = Selectable.Transition.None;
            cClBtn.onClick.AddListener(() => _controlsPanel.SetActive(false));
            var cX = new GameObject("x"); cX.transform.SetParent(cClose.transform, false);
            var cXRt = cX.AddComponent<RectTransform>();
            cXRt.anchorMin = Vector2.zero; cXRt.anchorMax = Vector2.one;
            cXRt.offsetMin = cXRt.offsetMax = Vector2.zero;
            var cXTxt = cX.AddComponent<Text>();
            cXTxt.font = _font; cXTxt.fontSize = 32; cXTxt.color = theme.TextBright;
            cXTxt.alignment = TextAnchor.MiddleCenter; cXTxt.text = "X";
            cXTxt.supportRichText = false; cXTxt.raycastTarget = false;

            string[] labels = { "|| PAUSE", "[ END ]", ">> MUTE",
                                AudioManager.MusicMuted ? "♪ OFF" : "♪ MUSIC" };
            UnityEngine.Events.UnityAction[] actions =
            {
                TogglePause, ConfirmEnd, ToggleMute, ToggleMusicMute
            };
            float btnW = 396f, btnH = 72f, gap = 12f;
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                var bGo = new GameObject($"CBtn{i}");
                bGo.transform.SetParent(cCard.transform, false);
                var bRt = bGo.AddComponent<RectTransform>();
                bRt.anchorMin = bRt.anchorMax = new Vector2(0.5f, 1f);
                bRt.pivot = new Vector2(0.5f, 1f);
                bRt.anchoredPosition = new Vector2(0f, -(66f + i * (btnH + gap)));
                bRt.sizeDelta = new Vector2(btnW, btnH);
                var bImg = bGo.AddComponent<Image>(); bImg.color = new Color(theme.BgCard.r, theme.BgCard.g, theme.BgCard.b, 0.92f);

                // border
                var brd = new GameObject("brd"); brd.transform.SetParent(bGo.transform, false);
                brd.transform.SetAsFirstSibling();
                var brdRt = brd.AddComponent<RectTransform>();
                brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
                brdRt.offsetMin = new Vector2(-1, -1); brdRt.offsetMax = new Vector2(1, 1);
                brd.AddComponent<Image>().color = theme.AccentDim;

                // label
                var tGo = new GameObject("txt"); tGo.transform.SetParent(bGo.transform, false);
                var tRt = tGo.AddComponent<RectTransform>();
                tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
                tRt.offsetMin = tRt.offsetMax = Vector2.zero;
                var txt = tGo.AddComponent<Text>();
                txt.font = _font; txt.fontSize = 28; txt.fontStyle = FontStyle.Bold;
                txt.color = theme.TextBright; txt.alignment = TextAnchor.MiddleCenter;
                txt.text = labels[i]; txt.supportRichText = false; txt.raycastTarget = false;
                _ctrlBtnTexts[i] = txt;

                var btn = bGo.AddComponent<Button>(); btn.transition = Selectable.Transition.None;
                btn.onClick.AddListener(actions[idx]);
                bGo.AddComponent<ButtonFeel>();
            }

            // Pause overlay
            _pauseOverlay = new GameObject("PauseOverlay");
            _pauseOverlay.transform.SetParent(_canvasGo.transform, false);
            var pRt = _pauseOverlay.AddComponent<RectTransform>();
            pRt.anchorMin = Vector2.zero; pRt.anchorMax = Vector2.one;
            pRt.offsetMin = pRt.offsetMax = Vector2.zero;
            _pauseOverlay.AddComponent<Image>().color = new Color(theme.BgDeep.r, theme.BgDeep.g, theme.BgDeep.b, 0.82f);

            // Beveled pause panel: title + audio/bg sliders + resume + end battle
            var pPanel = NeonUI.BevelPanel(_pauseOverlay.transform, "panel",
                new Vector2(0.34f, 0.20f), new Vector2(0.66f, 0.80f),
                new Color(theme.BgPanel.r, theme.BgPanel.g, theme.BgPanel.b, 1f),
                new Color(theme.Accent.r, theme.Accent.g, theme.Accent.b, 0.75f));

            var pTxtGo = NeonUI.Rect(pPanel.transform, "txt", new Vector2(0f, 0.84f), new Vector2(1f, 0.98f));
            var pTxt = pTxtGo.AddComponent<Text>();
            pTxt.font = _font; pTxt.fontSize = 40; pTxt.fontStyle = FontStyle.Bold;
            pTxt.color = theme.Accent; pTxt.alignment = TextAnchor.MiddleCenter;
            pTxt.text = "PAUSED"; pTxt.supportRichText = false; pTxt.raycastTarget = false;
            NeonUI.Title(pTxt);

            var subTxtGo = NeonUI.Rect(pPanel.transform, "sub", new Vector2(0f, 0.77f), new Vector2(1f, 0.84f));
            var subTxt = subTxtGo.AddComponent<Text>();
            subTxt.font = _font; subTxt.fontSize = 13; subTxt.color = theme.TextDim;
            subTxt.alignment = TextAnchor.MiddleCenter; subTxt.raycastTarget = false;
            subTxt.text = "TAP RESUME TO CONTINUE"; subTxt.supportRichText = false;

            // Settings sliders — live-applied, persisted
            NeonUI.SliderBar(pPanel.transform, _font, "SFX",
                new Vector2(0.08f, 0.64f), new Vector2(0.92f, 0.72f),
                AudioManager.SfxVol, theme.Accent, v => AudioManager.SfxVol = v);
            NeonUI.SliderBar(pPanel.transform, _font, "MUSIC",
                new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.62f),
                AudioManager.MusicVol, theme.Accent, v => AudioManager.MusicVol = v);
            NeonUI.SliderBar(pPanel.transform, _font, "BG DIM",
                new Vector2(0.08f, 0.44f), new Vector2(0.92f, 0.52f),
                GameSettings.BoardBgOpacity, theme.Accent, v =>
                {
                    GameSettings.BoardBgOpacity = v;
                    if (_boardBgImg != null)
                        _boardBgImg.color = new Color(1f, 1f, 1f, Mathf.Clamp(v, 0.05f, 1f));
                });

            NeonUI.BevelButton(pPanel.transform, "▶  RESUME", _font, 20,
                new Vector2(0.12f, 0.24f), new Vector2(0.88f, 0.38f),
                new Color(0.06f, 0.30f, 0.14f), new Color(0.20f, 0.95f, 0.40f, 0.9f),
                Color.white, TogglePause);

            NeonUI.BevelButton(pPanel.transform, "✗  END BATTLE", _font, 16,
                new Vector2(0.12f, 0.06f), new Vector2(0.88f, 0.20f),
                new Color(0.26f, 0.08f, 0.08f), new Color(1f, 0.35f, 0.35f, 0.85f),
                new Color(1f, 0.65f, 0.65f), ConfirmEnd);

            _controlsPanel.SetActive(false);
            if (_ctrlGear != null) _ctrlGear.SetActive(false);   // revealed when the battle starts
            _pauseOverlay.SetActive(false);
        }

        private void TogglePause()
        {
            _paused = !_paused;
            _pauseOverlay.SetActive(_paused);
            if (_ctrlBtnTexts[0] != null)
                _ctrlBtnTexts[0].text = _paused ? "> RESUME" : "|| PAUSE";
        }

        private void ConfirmEnd()
        {
            // Require a second tap within 3s so the battle can't be ended by a single stray tap.
            if (!_endArmed)
            {
                _endArmed = true;
                StartCoroutine(ShowDeployToast("Tap END BATTLE again to confirm"));
                Tween.Delay(3f, () => _endArmed = false);
                return;
            }
            _endArmed = false;
            _paused = false;
            _pauseOverlay.SetActive(false);
            Restart();
        }

        private void ToggleMute()
        {
            _muted = !_muted;
            AudioListener.volume = _muted ? 0f : 1f;
            if (_ctrlBtnTexts[2] != null)
                _ctrlBtnTexts[2].text = _muted ? ">> UNMUTE" : ">> MUTE";
        }

        private void ToggleMusicMute()
        {
            AudioManager.MusicMuted = !AudioManager.MusicMuted;
            if (_ctrlBtnTexts[3] != null)
                _ctrlBtnTexts[3].text = AudioManager.MusicMuted ? "♪ OFF" : "♪ MUSIC";
        }

        // ---------------------------------------- screens -------------------

        static bool _welcomeShown;

        private void ShowStartScreen()
        {
            TooltipSystem.Init(_font, _canvasGo.transform);

            // Branded welcome splash once per app launch, then proceed.
            if (!_welcomeShown)
            {
                _welcomeShown = true;
                ShowWelcome(ProceedToStart);
                return;
            }
            ProceedToStart();
        }

        private void ProceedToStart()
        {
            // Skip profile select if a slot is already active (e.g. after theme change / level end)
            if (PlayerProgress.CurrentSlot >= 0)
            {
                ShowLevelSelect();
                return;
            }
            ShowProfileSelect();
        }

        // Branded boot splash: icon + title + tagline, fade in → hold → fade out → onDone.
        private void ShowWelcome(System.Action onDone)
        {
            var theme = NeonTheme.Active;
            var go = new GameObject("[Welcome]");
            go.transform.SetParent(_canvasGo.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.transform.SetAsLastSibling();
            go.AddComponent<Image>().color = theme.BgDeep;
            var cg = go.AddComponent<CanvasGroup>();

            // Transparent-background emblem (AppIconGlyph) so it blends into the splash instead
            // of showing the opaque icon square.
            var tex = Resources.Load<Texture2D>("AppIconGlyph");
            if (tex != null)
            {
                var iconGo = new GameObject("icon"); iconGo.transform.SetParent(go.transform, false);
                var iconRt = iconGo.AddComponent<RectTransform>();
                iconRt.anchorMin = iconRt.anchorMax = new Vector2(0.5f, 0.58f);
                iconRt.pivot = new Vector2(0.5f, 0.5f);
                iconRt.sizeDelta = new Vector2(420f, 420f);
                var iconImg = iconGo.AddComponent<RawImage>();
                iconImg.texture = tex; iconImg.raycastTarget = false;
                Tween.Scale(iconGo.transform, 0.82f, 1f, 0.6f, Tween.Ease.EaseOutBack);
            }

            var titleGo = new GameObject("title"); titleGo.transform.SetParent(go.transform, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 0.30f); titleRt.anchorMax = new Vector2(1f, 0.42f);
            titleRt.offsetMin = titleRt.offsetMax = Vector2.zero;
            var titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = _font; titleTxt.fontSize = UIScale.FontTitle; titleTxt.fontStyle = FontStyle.Bold;
            titleTxt.color = theme.TextBright; titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.text = "NEON  WARFARE"; titleTxt.supportRichText = false; titleTxt.raycastTarget = false;

            var tagGo = new GameObject("tag"); tagGo.transform.SetParent(go.transform, false);
            var tagRt = tagGo.AddComponent<RectTransform>();
            tagRt.anchorMin = new Vector2(0f, 0.25f); tagRt.anchorMax = new Vector2(1f, 0.30f);
            tagRt.offsetMin = tagRt.offsetMax = Vector2.zero;
            var tagTxt = tagGo.AddComponent<Text>();
            tagTxt.font = _font; tagTxt.fontSize = UIScale.FontBody;
            tagTxt.color = theme.Accent; tagTxt.alignment = TextAnchor.MiddleCenter;
            tagTxt.text = "MATCH · COMMAND · CONQUER"; tagTxt.supportRichText = false; tagTxt.raycastTarget = false;

            cg.alpha = 0f;
            Tween.Fade(cg, 0f, 1f, 0.4f);
            Tween.Delay(1.7f, () =>
                Tween.Fade(cg, 1f, 0f, 0.4f, done: () =>
                {
                    if (go != null) Destroy(go);
                    onDone?.Invoke();
                }));
        }

        // Called directly by the "SWITCH PILOT" button on the level select screen
        public void ShowProfileSelect()
        {
            var psGo = new GameObject("[ProfileSelect]");
            psGo.transform.SetParent(_canvasGo.transform, false);
            var psRt = psGo.AddComponent<RectTransform>();
            psRt.anchorMin = Vector2.zero; psRt.anchorMax = Vector2.one;
            psRt.offsetMin = psRt.offsetMax = Vector2.zero;
            ScreenBackground.Attach(psRt);
            var psCg = psGo.AddComponent<CanvasGroup>();
            var ps = psGo.AddComponent<ProfileSelectScreen>();
            ps.Init(_font);
            ps.OnSlotSelected = slot =>
            {
                PlayerProgress.LoadSlot(slot);
                // Destroy any existing level select (includeInactive: true — it may be hidden by SwitchPilot)
                var oldLs = psGo.transform.parent.GetComponentInChildren<LevelSelectScreen>(true);
                if (oldLs != null) Destroy(oldLs.gameObject);
                ShowLevelSelect();
            };
            Tween.Fade(psCg, 0f, 1f, 0.22f);
            Tween.PopIn(psGo.transform);
        }

        /// <summary>
        /// Sets the battle layout bands for the current orientation. Landscape keeps the tuned
        /// desktop values (centered board column); portrait stacks proportional bands (≈ doc 09
        /// §3.1 percentages) with a near-full-width board. Called once at UI build, before any
        /// band value is read. Part of the phone/portrait port (doc 09 P0 vertical stack).
        /// </summary>
        private void ComputeResponsiveLayout()
        {
            bool portrait = Screen.height > Screen.width;
            _portrait = portrait;
            if (portrait)
            {
                // Reference space 1080×1920 (see ResponsiveCanvas). Bands ≈ doc 09 §3.1.
                TOP_H     = 44;    // minimal act/combo bar
                BOARD_H   = 700;   // ~36% of 1920 — compiler board, thumb zone
                STRIP_H   = 176;   // ~9% — command strip
                WALLET_H  = 92;    // wallet strip under the top bar (Phase 3)
                _boardMinX = 0.02f; // board is full-width; wallet moved to the top strip
                _boardMaxX = 0.98f;
            }
            else
            {
                TOP_H     = 36;
                BOARD_H   = 460;
                STRIP_H   = 136;
                SIDEBAR_W = 132;
                _boardMinX = 0.269f;
                _boardMaxX = 0.800f;
            }
        }

        private void ShowLevelSelect()
        {
            AudioManager.PlayMusic(GameSettings.ThemeIndex);

            var ssGo = new GameObject("[LevelSelect]");
            ssGo.transform.SetParent(_canvasGo.transform, false);
            var ssRt = ssGo.AddComponent<RectTransform>();
            ssRt.anchorMin = Vector2.zero; ssRt.anchorMax = Vector2.one;
            ssRt.offsetMin = ssRt.offsetMax = Vector2.zero;
            // Every theme gets a background (was theme 0 only) — theme 0 uses the level-select art,
            // the rest borrow their battlefield atmosphere image.
            string[] lsBg = { "level_select", "battlefield_purple", "battlefield_green", "battlefield_crimson",
                              "battlefield_ghost", "battlefield_sakura", "battlefield_solar", "battlefield_dawn" };
            var ssPhoto = ScreenBackground.Attach(ssRt, ArtLoader.BgTex(lsBg[Mathf.Clamp(GameSettings.ThemeIndex, 0, 7)]));
            var ssCg = ssGo.AddComponent<CanvasGroup>();
            var ls = ssGo.AddComponent<LevelSelectScreen>();
            ls.Init(_font);
            ls.SetBgPhoto(ssPhoto); // let the BG DIM slider dim this screen's background
            ls.OnLevelSelected = lv => StartCoroutine(WipeIntoBattle(lv));
            Tween.Fade(ssCg, 0f, 1f, 0.22f);
            Tween.PopIn(ssGo.transform);
        }

        IEnumerator ShowDeployToast(string msg)
        {
            if (_canvasGo == null) yield break;
            var go = new GameObject("deployToast");
            go.transform.SetParent(_canvasGo.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.25f, 0f); rt.anchorMax = new Vector2(0.75f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, BOARD_H + STRIP_H * 0.55f);
            rt.sizeDelta = new Vector2(0f, 36f);
            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.80f, 0.14f, 0.10f, 0.88f);
            var tGo = new GameObject("t"); tGo.transform.SetParent(go.transform, false);
            var tRt = tGo.AddComponent<RectTransform>();
            tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one;
            tRt.offsetMin = tRt.offsetMax = Vector2.zero;
            var txt = tGo.AddComponent<Text>();
            txt.font = _font; txt.fontSize = 14; txt.fontStyle = FontStyle.Bold;
            txt.color = Color.white; txt.alignment = TextAnchor.MiddleCenter;
            txt.text = msg; txt.supportRichText = false; txt.raycastTarget = false;

            float elapsed = 0f;
            while (elapsed < 1.8f)
            {
                elapsed += Time.deltaTime;
                float a = 1f - Mathf.Clamp01((elapsed - 1.1f) / 0.7f);
                if (bg  != null) bg.color  = new Color(0.80f, 0.14f, 0.10f, 0.88f * a);
                if (txt != null) txt.color = new Color(1f, 1f, 1f, a);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // Fade-through-black into the battle — menu never hard-cuts away
        private IEnumerator WipeIntoBattle(int level)
        {
            var go = new GameObject("wipe");
            go.transform.SetParent(_canvasGo.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0f); // blocks input while fading

            const float IN = 0.18f, OUT = 0.28f;
            for (float t = 0f; t < IN; t += Time.unscaledDeltaTime)
            {
                if (img == null) yield break;
                img.color = new Color(0f, 0f, 0f, t / IN);
                yield return null;
            }
            img.color = Color.black;
            StartBattle(level);
            go.transform.SetAsLastSibling(); // stay above the freshly built battle UI
            yield return null;
            for (float t = 0f; t < OUT; t += Time.unscaledDeltaTime)
            {
                if (img == null) yield break;
                img.color = new Color(0f, 0f, 0f, 1f - t / OUT);
                yield return null;
            }
            Destroy(go);
        }

        private void StartBattle(int level)
        {
            AudioManager.StopMusic();
            // Re-apply opacity now that the user has made their selection in LevelSelectScreen
            if (_boardBgImg != null)
                _boardBgImg.color = new Color(1f, 1f, 1f, Mathf.Clamp(GameSettings.BoardBgOpacity, 0.05f, 1f));

            var ghost = PendingGhost;
            PendingGhost = null;
            _ghostMode = ghost != null;

            float dm  = LevelConfig.DirectorMult(level);
            int gems  = LevelConfig.GemColorCount(level);
            // Daily board: every pilot playing this level today shares one seed, so a ghost
            // recorded today is a true head-to-head. Older ghosts still replay (their action
            // trace is board-independent); they just are not a like-for-like race.
            int seed  = RankLadder.DailySeed(level);
            _session  = new BattleSession(_db, dm,
                seed: seed, allowedGemCount: gems, level: level,
                enemyDirector: ghost == null);   // a ghost challenge replaces the AI, not adds to it

            // Every match records its own ghost — that is how the local pool fills.
            _recorder = new GhostRecorder(level, GameSettings.ThemeIndex, seed, gems, dm);
            _session.OnPlayerDeploy += (card, lane) =>
                _recorder?.RecordDeploy(
                    _session.Combat.TickCount,
                    card >= 0 && card < _session.DeployOptions.Count ? _session.DeployOptions[card].spec.Id : "trooper",
                    lane);

            if (ghost != null)
            {
                _ghostOppMmr   = ghost.mmr;
                _ghostOppPilot = ghost.pilot;
                _ghostLevel    = level;
                _ghostDriver   = new GhostDriver(ghost, _session.Combat)
                                     { OnGhostCrossover = (k, l, dmg) => ShowHitFlash(dmg) };
                _session.OnCombatStepped += () => _ghostDriver?.Step(_session.Combat.TickCount);
            }

            _bfView.Init(_session, _bfRt, _font);
            _hudView.Init(_session, _topRt, _stripRt, _sidebarRt, _font);
            _hudView.OnLaneDeploy = OnLaneClicked;
            // Board occupies anchors _boardMinX.._boardMaxX (orientation-aware: centered column
            // in landscape, near-full-width in portrait); compute px width at runtime so board
            // content scales correctly on any display, not just the 1920px reference.
            int boardAvailW = Mathf.Max(240, Mathf.RoundToInt(Screen.width * (_boardMaxX - _boardMinX)));
            _boardView.Init(_session, _boardRt, _font, availW: boardAvailW, availH: BOARD_H);
            _battleStarted = true;

            if (GameSettings.CompetitiveMode || _ghostMode)
            {
                BuildCompOverlay();
                if (GameSettings.CompetitiveMode && NWNet.Inst != null) SubscribeCompetitive();
            }

            // Show the MENU button when the battle starts -- NOT the panel. _controlsPanel
            // is now the modal itself, so activating it here would pop the menu open on top
            // of the battlefield the moment the match began.
            if (_ctrlGear != null) _ctrlGear.SetActive(true);
            if (_controlsPanel != null) _controlsPanel.SetActive(false);

            AudioManager.PlayMusic(GameSettings.ThemeIndex);
            StartCoroutine(BattleCountdown());
        }

        // 3 · 2 · 1 · DEPLOY! — sim is frozen via timeScale until the horn
        private IEnumerator BattleCountdown()
        {
            Time.timeScale = 0f;
            var go = new GameObject("countdown");
            go.transform.SetParent(_canvasGo.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.35f, 0.40f); rt.anchorMax = new Vector2(0.65f, 0.62f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var txt = go.AddComponent<Text>();
            txt.font = _font; txt.fontSize = 84; txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter; txt.supportRichText = false;
            txt.raycastTarget = false;
            NeonUI.Title(txt);

            var theme = NeonTheme.Active;
            string[] steps = { "3", "2", "1", "DEPLOY!" };
            for (int i = 0; i < steps.Length; i++)
            {
                bool horn = i == 3;
                txt.text  = steps[i];
                txt.color = horn ? new Color(0.25f, 1f, 0.45f) : theme.Accent;
                AudioManager.Play(AudioManager.Sfx.Click, horn ? 1f : 0.7f, horn ? 1.6f : 1f + i * 0.12f);
                float dur = horn ? 0.5f : 0.62f;
                for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
                {
                    float p = t / dur;
                    float s = horn ? 1f + p * 0.35f : Mathf.Lerp(1.7f, 1f, Mathf.Min(1f, p * 3.5f));
                    go.transform.localScale = new Vector3(s, s, 1f);
                    txt.color = new Color(txt.color.r, txt.color.g, txt.color.b, horn ? 1f - p * p : 1f);
                    yield return null;
                }
            }
            Destroy(go);
            Time.timeScale = 1f;
        }

        void SubscribeCompetitive()
        {
            NWNet.Inst.OnCrossoverReceived += msg =>
            {
                if (_session != null && !_session.Combat.Finished)
                    _session.Combat.DamagePlayerCore(msg.Damage);
                ShowHitFlash(msg.Damage);
            };
            NWNet.Inst.OnOpponentDead += () =>
            {
                if (_session == null || _postGameDone) return;
                _postGameDone = true;
                FinalizeGhostRecording(true);
                int prev = PlayerProgress.MMR;
                PlayerProgress.ApplyMatchResult(true);
                ShowMatchResult(true, prev, PlayerProgress.MMR);
            };
        }

        /// <summary>Seal the in-progress recording and write it to the local ghost pool.
        /// Safe to call once per match; a no-op if there is nothing to record.</summary>
        void FinalizeGhostRecording(bool playerWon)
        {
            if (_recorder == null) return;
            var rec = _recorder.Finish(playerWon, _session != null ? _session.Combat.TickCount : 0);
            _recorder = null;
            if (rec != null)
            {
                GhostStore.Save(rec);
                Leaderboard.SubmitBest(rec);   // no-op locally; online backend writes only if it beats the pilot's row
            }
        }

        // ─────────────────────────── competitive overlay ──────────────────────────

        void BuildCompOverlay()
        {
            var t = NeonTheme.Active;

            // ── top-bar left badge ────────────────────────────────────────────────
            _compBar = new GameObject("CompBar");
            _compBar.transform.SetParent(_canvasGo.transform, false);
            var cbRt = _compBar.AddComponent<RectTransform>();
            cbRt.anchorMin = new Vector2(0f, 1f); cbRt.anchorMax = new Vector2(0f, 1f);
            cbRt.pivot = new Vector2(0f, 1f);
            cbRt.anchoredPosition = Vector2.zero;
            cbRt.sizeDelta = new Vector2(240f, TOP_H);
            _compBar.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.80f);

            // right edge separator
            var sep = new GameObject("sep"); sep.transform.SetParent(_compBar.transform, false);
            var sepRt = sep.AddComponent<RectTransform>();
            sepRt.anchorMin = new Vector2(1f, 0f); sepRt.anchorMax = new Vector2(1f, 1f);
            sepRt.pivot = new Vector2(1f, 0.5f); sepRt.anchoredPosition = Vector2.zero; sepRt.sizeDelta = new Vector2(1f, 0f);
            sep.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.42f);

            Text BarTxt(string name, string text, float x, int sz, FontStyle fs, Color col, float w)
            {
                var go = new GameObject(name); go.transform.SetParent(_compBar.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f); rt.pivot = new Vector2(0f, 0.5f);
                rt.anchoredPosition = new Vector2(x, 0f); rt.sizeDelta = new Vector2(w, TOP_H);
                var tx = go.AddComponent<Text>();
                tx.font = _font; tx.fontSize = sz; tx.fontStyle = fs;
                tx.color = col; tx.alignment = TextAnchor.MiddleLeft;
                tx.text = text; tx.supportRichText = false; tx.raycastTarget = false;
                return tx;
            }

            BarTxt("rank", "⚡  RANKED", 8f,  9,  FontStyle.Bold,   t.Accent,                          80f);
            _compDotTxt = BarTxt("dot", "● LIVE", 90f, 11, FontStyle.Bold, new Color(0.25f, 0.95f, 0.38f), 66f);
            _compMmrTxt = BarTxt("mmr", $"MMR {PlayerProgress.MMR}", 158f, 10, FontStyle.Normal,
                                  new Color(0.66f, 0.70f, 0.88f), 120f);

            // ── incoming-hit flash card ────────────────────────────────────────────
            _hitFlashGo = new GameObject("HitFlash");
            _hitFlashGo.transform.SetParent(_canvasGo.transform, false);
            var hfRt = _hitFlashGo.AddComponent<RectTransform>();
            hfRt.anchorMin = new Vector2(0.28f, 0.59f); hfRt.anchorMax = new Vector2(0.72f, 0.72f);
            hfRt.offsetMin = hfRt.offsetMax = Vector2.zero;
            _hitFlashGo.AddComponent<Image>().color = new Color(0.55f, 0.04f, 0.04f, 0.88f);

            var hfBrd = new GameObject("brd"); hfBrd.transform.SetParent(_hitFlashGo.transform, false);
            hfBrd.transform.SetAsFirstSibling();
            var hfBrdRt = hfBrd.AddComponent<RectTransform>();
            hfBrdRt.anchorMin = Vector2.zero; hfBrdRt.anchorMax = Vector2.one;
            hfBrdRt.offsetMin = new Vector2(-2f, -2f); hfBrdRt.offsetMax = new Vector2(2f, 2f);
            hfBrd.AddComponent<Image>().color = new Color(1f, 0.12f, 0.12f, 0.85f);

            var hfTGo = new GameObject("txt"); hfTGo.transform.SetParent(_hitFlashGo.transform, false);
            var hfTRt = hfTGo.AddComponent<RectTransform>();
            hfTRt.anchorMin = Vector2.zero; hfTRt.anchorMax = Vector2.one;
            hfTRt.offsetMin = hfTRt.offsetMax = Vector2.zero;
            _hitFlashTxt = hfTGo.AddComponent<Text>();
            _hitFlashTxt.font = _font; _hitFlashTxt.fontSize = 26; _hitFlashTxt.fontStyle = FontStyle.Bold;
            _hitFlashTxt.color = Color.white; _hitFlashTxt.alignment = TextAnchor.MiddleCenter;
            _hitFlashTxt.supportRichText = false; _hitFlashTxt.raycastTarget = false;
            _hitFlashGo.SetActive(false);

            // ── post-match result card ─────────────────────────────────────────────
            _matchResultGo = new GameObject("MatchResult");
            _matchResultGo.transform.SetParent(_canvasGo.transform, false);
            var mrRt = _matchResultGo.AddComponent<RectTransform>();
            mrRt.anchorMin = Vector2.zero; mrRt.anchorMax = Vector2.one;
            mrRt.offsetMin = mrRt.offsetMax = Vector2.zero;
            _matchResultGo.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            var card = new GameObject("card"); card.transform.SetParent(_matchResultGo.transform, false);
            var cardRt = card.AddComponent<RectTransform>();
            cardRt.anchorMin = new Vector2(0.25f, 0.34f); cardRt.anchorMax = new Vector2(0.75f, 0.70f);
            cardRt.offsetMin = cardRt.offsetMax = Vector2.zero;
            card.AddComponent<Image>().color = new Color(0.06f, 0.07f, 0.10f, 0.97f);

            var cardBrd = new GameObject("brd"); cardBrd.transform.SetParent(card.transform, false);
            cardBrd.transform.SetAsFirstSibling();
            var cardBrdRt = cardBrd.AddComponent<RectTransform>();
            cardBrdRt.anchorMin = Vector2.zero; cardBrdRt.anchorMax = Vector2.one;
            cardBrdRt.offsetMin = new Vector2(-2f, -2f); cardBrdRt.offsetMax = new Vector2(2f, 2f);
            cardBrd.AddComponent<Image>().color = new Color(t.Accent.r, t.Accent.g, t.Accent.b, 0.55f);

            Text CardTxt(string name, float y0, float y1, int sz, FontStyle fs, Color col)
            {
                var go = new GameObject(name); go.transform.SetParent(card.transform, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, y0); rt.anchorMax = new Vector2(1f, y1);
                rt.offsetMin = rt.offsetMax = Vector2.zero;
                var tx = go.AddComponent<Text>();
                tx.font = _font; tx.fontSize = sz; tx.fontStyle = fs;
                tx.color = col; tx.alignment = TextAnchor.MiddleCenter;
                tx.supportRichText = false; tx.raycastTarget = false;
                if (sz >= 22) NeonUI.Title(tx); else NeonUI.Shadow(tx);
                return tx;
            }

            _matchResultLbl = CardTxt("lbl", 0.50f, 1.00f, 58, FontStyle.Bold,   Color.white);
            _matchResultSub = CardTxt("sub", 0.20f, 0.50f, 20, FontStyle.Normal, new Color(0.72f, 0.76f, 0.90f));
            var hint        = CardTxt("hint",0.02f, 0.20f, 13, FontStyle.Normal, new Color(0.36f, 0.38f, 0.48f));
            hint.text = "[SPACE] or [F5]  to continue";

            _matchResultGo.SetActive(false);
        }

        void TickCompOverlay()
        {
            _dotPulseT += Time.deltaTime * 2.6f;

            if (_ghostMode && _compDotTxt != null)
            {
                float gp = (Mathf.Sin(_dotPulseT) + 1f) * 0.5f;
                _compDotTxt.text  = "◆ GHOST";
                _compDotTxt.color = new Color(0.95f, 0.72f + gp * 0.12f, 0.20f);
            }
            else if (_compDotTxt != null && NWNet.Inst != null)
            {
                bool conn   = NWNet.Inst.IsConnected;
                float pulse = (Mathf.Sin(_dotPulseT) + 1f) * 0.5f;
                _compDotTxt.text  = conn ? "● LIVE" : "○ WAIT";
                _compDotTxt.color = conn
                    ? new Color(0.18f + pulse * 0.12f, 0.88f + pulse * 0.12f, 0.28f + pulse * 0.08f)
                    : new Color(0.62f, 0.30f, 0.22f);
            }

            if (_hitFlashTimer > 0f)
            {
                _hitFlashTimer -= Time.deltaTime;
                float a = Mathf.Clamp01(_hitFlashTimer / HitFlashDur);
                if (_hitFlashGo != null)
                {
                    _hitFlashGo.GetComponent<Image>().color = new Color(0.55f, 0.04f, 0.04f, a * 0.88f);
                    if (_hitFlashTxt != null) _hitFlashTxt.color = new Color(1f, 1f, 1f, a);
                    _hitFlashGo.SetActive(_hitFlashTimer > 0f);
                }
            }
        }

        void ShowHitFlash(float damage)
        {
            if (_hitFlashGo == null) return;
            _hitFlashTxt.text = $"CORE HIT  -{damage:0}";
            _hitFlashTimer = HitFlashDur;
            _hitFlashGo.SetActive(true);
        }

        void ShowMatchResult(bool won, int mmrPrev, int mmrNow)
        {
            if (_matchResultGo == null) return;
            int delta = mmrNow - mmrPrev;
            _matchResultLbl.text  = won ? "VICTORY" : "DEFEAT";
            _matchResultLbl.color = won ? new Color(0.22f, 1f, 0.45f) : new Color(1f, 0.22f, 0.22f);
            _matchResultSub.text  = $"MMR  {mmrPrev}  →  {mmrNow}   ({(delta >= 0 ? "+" : "")}{delta})";
            _matchResultGo.SetActive(true);
        }

        // ─────────────────────────────────────────────── helpers ───────────

        static RectTransform MakeStretch(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            return rt;
        }
    }
}
