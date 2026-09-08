using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using NW.Combat.Domain;

namespace NW.App
{
    /// <summary>
    /// Renders the five-lane battlefield (doc 11): 4 ground lanes with ownership
    /// tint + frontline seam + capture pylons, an air lane above, and both cores
    /// as discrete HP-battery columns. Unit views are pooled and keyed by stable
    /// unit Id. Includes Clash-Royale-grade animations: spawn drop, idle bob,
    /// directional attack lunge or projectile, fragment-particle death explosion,
    /// and per-core hit flash. Core HP is shown as 10 discrete battery segments.
    /// </summary>
    public sealed class BattlefieldView : MonoBehaviour
    {
        const float CORE_W    = 40f;
        const int   CORE_SEGS = 10;

        // Lane background colors per scene (DEFAULT, EMBER, FROST, NEON, VOID) × 4 lanes
        static readonly Color[][] SceneLaneColors =
        {
            new Color[] { new Color(0.05f,0.06f,0.09f), new Color(0.05f,0.06f,0.09f), new Color(0.05f,0.06f,0.09f), new Color(0.05f,0.06f,0.09f) },
            new Color[] { new Color(0.18f,0.05f,0.02f), new Color(0.14f,0.04f,0.01f), new Color(0.16f,0.06f,0.02f), new Color(0.12f,0.03f,0.01f) },
            new Color[] { new Color(0.05f,0.12f,0.22f), new Color(0.04f,0.09f,0.17f), new Color(0.06f,0.13f,0.24f), new Color(0.04f,0.10f,0.19f) },
            new Color[] { new Color(0.10f,0.02f,0.18f), new Color(0.02f,0.10f,0.14f), new Color(0.08f,0.02f,0.20f), new Color(0.02f,0.05f,0.16f) },
            new Color[] { new Color(0.07f,0.02f,0.14f), new Color(0.05f,0.02f,0.10f), new Color(0.09f,0.02f,0.16f), new Color(0.05f,0.02f,0.11f) },
        };

        // Troop size multiplier over the original authored art scale. 3.2 puts a trooper at
        // ~110 units (~44% of a 249-unit lane), where animation polish is actually legible.
        const float UnitScale = 3.2f;
        const float TerritoryStrip = 0.07f;   // captured-ground strip height, fraction of lane
        static readonly Color PlayerTint  = new Color(0.0f, 0.9f, 1f, 0.55f);
        static readonly Color EnemyTint   = new Color(1f, 0.25f, 0.1f, 0.55f);
        static readonly Color PlayerSolid = new Color(0.0f, 0.9f, 1f);
        static readonly Color EnemySolid  = new Color(1f, 0.30f, 0.15f);
        static readonly Color NeutralGrey = new Color(0.52f, 0.52f, 0.52f);

        public System.Action<int> OnLaneClicked;   // 0..3 ground, 4 = air

        BattleSession _session;
        Font _font;
        RectTransform _container;

        readonly RectTransform[] _laneRects      = new RectTransform[CombatSim.LaneCount];
        RectTransform            _fieldOverlay;   // battlefield-space layer for cross-lane projectiles (P0)
        readonly Image[]         _playerFloor    = new Image[CombatSim.GroundLanes];
        readonly Image[]         _enemyFloor     = new Image[CombatSim.GroundLanes];
        readonly RectTransform[] _seam           = new RectTransform[CombatSim.GroundLanes];
        readonly Image[]         _laneHighlight  = new Image[CombatSim.LaneCount];
        readonly Image[]         _waveWarn       = new Image[CombatSim.GroundLanes];
        readonly float[]         _waveWarnT      = new float[CombatSim.GroundLanes];
        readonly Image[,]        _pylonBody      = new Image[CombatSim.GroundLanes, 2];
        readonly Image[,]        _pylonBorder    = new Image[CombatSim.GroundLanes, 2];
        readonly Image[,]        _pylonRing      = new Image[CombatSim.GroundLanes, 2];
        readonly Image[,]        _pylonLine      = new Image[CombatSim.GroundLanes, 2];
        readonly Image[]         _laneBg         = new Image[CombatSim.GroundLanes];
        readonly Image[]         _laneLaser      = new Image[CombatSim.GroundLanes];

        // Cores — battery segments replace the old fill bar
        Image   _playerCoreBody, _enemyCoreBody;
        Image[] _playerSegs = new Image[CORE_SEGS];
        Image[] _enemySegs  = new Image[CORE_SEGS];
        Image   _playerCoreFlash, _enemyCoreFlash;
        float   _lastCoreSfx;                       // throttles the core-hit thud
        float   _pCoreAccum, _eCoreAccum;           // damage pooled between popups
        float   _pCoreNext,  _eCoreNext;            // next time a popup may spawn
        Text    _playerCoreTxt, _enemyCoreTxt;

        Image  _fieldFlash;
        Text   _banner;
        float  _bannerT;
        readonly Queue<(string msg, Color col)> _bannerQueue = new();

        float   _trauma;
        float   _lastUnitHitTime;
        Vector2 _basePos;
        bool[]  _validLanes; // null = all lanes valid; turret mode sets per-lane ownership mask

        // ── unit view ────────────────────────────────────────────────────────

        // Per-unit animation state: motion in RefreshUnits derives from this, attack
        // coroutines request transitions. March = moving (bob active); combat states
        // freeze the walk so units never slide while striking.
        enum UAnim { March, Windup, Strike, Recover, Stagger }

        sealed class UnitView
        {
            public RectTransform Rt;
            public RawImage      Body;
            public RawImage      Weapon;      // component rig: independent weapon layer (sniper/mech/turret/titan)
            public RawImage      Glow;
            public Image         Hp;
            public Image         HpBg;        // HP bar track — its border flashes on damage
            public Image         HitFlash;
            // TroopSkin VFX overlays (A–E) — animated per-frame in AnimateSkinVfx
            // Render order: A→B→C→D→E are siblings below the HP bar (flames under health bar)
            public Image         VfxA;        // base overlay / glow
            public Image         VfxB;        // flame tongue L / chrome stripe 1 / ghost trail 1
            public Image         VfxC;        // flame tongue C / chrome stripe 2 / ghost trail 2
            public Image         VfxD;        // flame tongue R / phantom aura
            public Image         VfxE;        // ember core / golden burst / phantom ring
            // Per-troop attack flash — weapon/body part that lights up during attack
            public Image         AttackFlash;
            public int           ActiveSkin;  // NeonCosmetics.TroopSkin cast to int
            // Limb rig: 0 far thigh, 1 far shin, 2 near thigh, 3 near shin, 4 far arm, 5 near arm.
            // Null for every unit that is not rigged, which is all of them except the ids in
            // NeonArt._rigs -- those keep the baked path untouched.
            public RawImage[]    Limbs;
            public bool          Rigged;
            public bool          RigOn;       // rig visible right now (walking), vs baked pose
            public bool          PendingDeployFx;  // spawn flourish owed, fired once placed
            public Image[]       SkinFx;           // VFX-skin elements, beside the unit
            public float         SkinFxPhase;
            public float         VfxPhase;    // per-unit phase offset / running accumulator
            public float         SpawnTime;
            public bool          Dying;
            public bool          Lunging;
            public bool          ScaleAnim;   // a coroutine owns localScale — RefreshUnits must not write it
            public bool          IsPlayer;
            public Color         BaseBody;    // body tint set in StyleView — damage darkening derives from this
            public UAnim         Anim;        // current animation state
            public float         BobW;        // walk-cycle weight: 1 marching, blends to 0 in combat states
            public float         PrevHop;     // last frame's hop height — detects ground contact for stomp dust
            // Character layer (C3–C6)
            public float         NextFidget;  // next idle micro-action time
            public float         NextWound;   // next damage-spark time when critical
            public float         VoicePitch;  // per-class chirp pitch
            public float         BobPhase;
            public float         LungeOffset;
            public float         SpawnYOffset;
            public float         VisualX;    // smoothed pixel position — interpolated each frame
            public float         VelX;       // SmoothDamp velocity — accel/brake motion profile
            // Per-troop attack animation state
            public string        SpecId;     // cached from UnitSpec.Id for quick dispatch
            public string        ArtId;      // themed art id — pose/part lookups
            public float         UScale = 1f; // rect size / 52 baseline — scales VFX offsets,
                                              // lunges and hops so motion keeps its proportion
                                              // to the body at any unit size
            public int           PoseFrame;  // current walk-cycle frame (0 neutral, 1 stride, 2 pass)
            public int           AttackPose = -1; // >=0 overrides the walk frame (3 windup, 4 strike)
        }

        readonly Dictionary<ulong, UnitView> _views      = new();
        readonly Stack<UnitView>             _pool       = new();
        readonly List<ulong>                 _toRemove   = new();
        readonly HashSet<ulong>              _pendingSpawnDrop = new();

        // ─────────────────────────────────────────────────── init ────────────

        public void Init(BattleSession session, RectTransform container, Font font)
        {
            _session   = session;
            _container = container;
            _font      = font;
            _basePos   = container.anchoredPosition;

            BuildAirLane();
            for (int lane = 0; lane < CombatSim.GroundLanes; lane++) BuildGroundLane(lane);
            ApplyLaneScene();

            BuildCore(isPlayer: true);
            BuildCore(isPlayer: false);

            BuildFieldOverlay();

            _fieldFlash = MakeImage(container, "Flash", Vector2.zero, Vector2.one, new Color(1, 1, 1, 0));
            _fieldFlash.raycastTarget = false;

            var bGo = new GameObject("Banner");
            bGo.transform.SetParent(container, false);
            var bRt = bGo.AddComponent<RectTransform>();
            bRt.anchorMin = new Vector2(0.5f, 1f); bRt.anchorMax = new Vector2(0.5f, 1f);
            bRt.pivot     = new Vector2(0.5f, 1f);
            bRt.anchoredPosition = new Vector2(0, -26);
            bRt.sizeDelta = new Vector2(700, 34);
            _banner = bGo.AddComponent<Text>();
            _banner.font = font; _banner.fontSize = 22; _banner.fontStyle = FontStyle.Bold;
            _banner.alignment = TextAnchor.MiddleCenter;
            _banner.raycastTarget = false;
            _banner.text = "";
        }

        // ──────────────────────────────────────── builders ───────────────────

        void BuildAirLane()
        {
            var rt = MakeLaneRect("Lane_AIR", 0.84f, 1f);
            _laneRects[CombatSim.AirLane] = rt;
            rt.GetComponent<Image>().color = new Color(0.04f, 0.04f, 0.13f);
            AddLaneLabel(rt, "AIR  [A]", new Color(0.45f, 0.55f, 1f, 0.8f));
            _laneHighlight[CombatSim.AirLane] = AddHighlight(rt);
            AddLaneButton(rt, CombatSim.AirLane);
            AddLaneTooltip(rt, CombatSim.AirLane);
        }

        void BuildGroundLane(int lane)
        {
            // Ground lanes split everything under the air lane evenly, derived from
            // GroundLanes so the band height follows the lane count (3 lanes => ~27% each,
            // +35% vs the old 4-lane layout — the headroom that lets units be legible).
            const float GROUND_TOP = 0.84f;
            float gh  = GROUND_TOP / CombatSim.GroundLanes;
            float top = GROUND_TOP - lane * gh;
            var rt = MakeLaneRect($"Lane_{lane}", top - gh + 0.005f, top - 0.005f);
            _laneRects[lane] = rt;
            var laneBgImg = rt.GetComponent<Image>();
            laneBgImg.color = new Color(0.05f, 0.06f, 0.09f);
            _laneBg[lane] = laneBgImg;

            // Deploy pads. Units used to materialise at x=2 of a 100-unit lane -- inside the
            // core sprite -- so a deploy effect played somewhere the player could not see and
            // there was no mark on the lane saying where troops arrive. CombatSim.DeploySpawnX
            // now puts them clear of the core, and this draws the spot they land on.
            BuildDeployPad(rt, lane, true);
            BuildDeployPad(rt, lane, false);

            // Territory is a thin strip along the lane floor, NOT a full-height fill. As a
            // full-height block it read as a stray "deployment box" sitting in the lane and
            // competed with the units; a baseline strip carries the same ownership info quietly.
            _playerFloor[lane] = MakeImage(rt, "pFloor", Vector2.zero,
                                           new Vector2(0f, TerritoryStrip), PlayerTint);
            _enemyFloor[lane]  = MakeImage(rt, "eFloor", new Vector2(1f, 0f),
                                           new Vector2(1f, TerritoryStrip), EnemyTint);
            _playerFloor[lane].raycastTarget = false;
            _enemyFloor[lane].raycastTarget  = false;

            // Seam is kept as a hidden placeholder so existing array accesses are safe
            var seamGo = new GameObject("Seam");
            seamGo.transform.SetParent(rt, false);
            var seamRt = seamGo.AddComponent<RectTransform>();
            seamRt.anchorMin = new Vector2(0.5f, 0.06f); seamRt.anchorMax = new Vector2(0.5f, 0.94f);
            seamRt.pivot = new Vector2(0.5f, 0.5f);
            seamRt.sizeDelta = new Vector2(3, 0);
            var seamImg = seamGo.AddComponent<Image>();
            seamImg.color = Color.clear;   // hidden — replaced by pylon blips
            seamImg.raycastTarget = false;
            _seam[lane] = seamRt;

            for (int i = 0; i < 2; i++) BuildPylon(rt, lane, i);

            _waveWarn[lane] = MakeImage(rt, "WaveWarn", new Vector2(0.965f, 0f), Vector2.one,
                new Color(1f, 0.2f, 0.1f, 0f));
            _waveWarn[lane].raycastTarget = false;

            _laneLaser[lane] = MakeImage(rt, "Laser", new Vector2(0f, 0.42f), new Vector2(1f, 0.58f),
                new Color(0.4f, 1f, 1f, 0f));
            _laneLaser[lane].raycastTarget = false;

            AddLaneLabel(rt, $"LANE {lane + 1}", new Color(0.5f, 0.7f, 0.9f, 0.55f));
            _laneHighlight[lane] = AddHighlight(rt);
            AddLaneButton(rt, lane);
            AddLaneTooltip(rt, lane);
        }

        void BuildPylon(RectTransform lane, int laneIdx, int idx)
        {
            float x = idx == 0 ? 1f / 3f : 2f / 3f;

            // Thin vertical post spanning most of the lane height
            var lineGo = new GameObject($"PylonPost{idx}");
            lineGo.transform.SetParent(lane, false);
            var lineRt = lineGo.AddComponent<RectTransform>();
            lineRt.anchorMin = new Vector2(x, 0.06f);
            lineRt.anchorMax = new Vector2(x, 0.94f);
            lineRt.pivot     = new Vector2(0.5f, 0.5f);
            lineRt.sizeDelta = new Vector2(2f, 0f);
            var lineImg = lineGo.AddComponent<Image>();
            lineImg.color         = new Color(NeutralGrey.r, NeutralGrey.g, NeutralGrey.b, 0.55f);
            lineImg.raycastTarget = false;
            _pylonLine[laneIdx, idx] = lineImg;

            // Circular glowing blip — sits upper-mid of the lane
            var go = new GameObject($"Pylon{idx}");
            go.transform.SetParent(lane, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(x, 0.68f);
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(22f, 22f);

            // Capture progress ring — radial fill just outside the blip
            var ringGo = new GameObject("ring");
            ringGo.transform.SetParent(go.transform, false);
            var ringRt = ringGo.AddComponent<RectTransform>();
            ringRt.anchorMin = Vector2.zero; ringRt.anchorMax = Vector2.one;
            ringRt.offsetMin = new Vector2(-8, -8); ringRt.offsetMax = new Vector2(8, 8);
            var ring = ringGo.AddComponent<Image>();
            ring.color         = NeutralGrey;
            ring.type          = Image.Type.Filled;
            ring.fillMethod    = Image.FillMethod.Radial360;
            ring.fillAmount    = 0f;
            ring.raycastTarget = false;
            _pylonRing[laneIdx, idx] = ring;

            // Capture node reads as a BOX: strong border, weak fill. A fully-saturated solid
            // blip dominated the lane (the "super blue" checkpoint) and competed with the units
            // for attention; an outline holds the same ownership read much quieter.
            // The node's own Image is the border; an inset child paints the weak interior over
            // it (children draw above the parent graphic), leaving a crisp 2px frame.
            var border = go.AddComponent<Image>();
            border.color         = NeutralGrey;
            border.raycastTarget = false;
            _pylonBorder[laneIdx, idx] = border;

            var fillGo = new GameObject("fill");
            fillGo.transform.SetParent(go.transform, false);
            var fillRt = fillGo.AddComponent<RectTransform>();
            fillRt.anchorMin = Vector2.zero; fillRt.anchorMax = Vector2.one;
            fillRt.offsetMin = new Vector2(2f, 2f); fillRt.offsetMax = new Vector2(-2f, -2f);
            var body = fillGo.AddComponent<Image>();
            body.color         = new Color(NeutralGrey.r, NeutralGrey.g, NeutralGrey.b, 0.16f);
            body.raycastTarget = false;
            _pylonBody[laneIdx, idx] = body;
        }

        void ApplyLaneScene()
        {
            int scene = GameSettings.ActiveLaneScene;
            if (scene < 0 || scene >= SceneLaneColors.Length) scene = 0;
            var colors = SceneLaneColors[scene];
            for (int lane = 0; lane < CombatSim.GroundLanes; lane++)
                if (_laneBg[lane] != null) _laneBg[lane].color = colors[lane];
        }

        void BuildCore(bool isPlayer)
        {
            var go = new GameObject(isPlayer ? "NexusCore" : "EnemyCore");
            go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            float xMin = isPlayer ? 0f : 1f;
            rt.anchorMin = new Vector2(xMin, 0f); rt.anchorMax = new Vector2(xMin, 0.83f);
            rt.pivot = new Vector2(isPlayer ? 0f : 1f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(CORE_W, 0);
            var body = go.AddComponent<Image>();
            body.color = isPlayer ? new Color(0.07f, 0.18f, 0.3f) : new Color(0.3f, 0.08f, 0.06f);
            body.raycastTarget = false;

            // 10 discrete battery segments stacked bottom-to-top
            var segs = new Image[CORE_SEGS];
            float botPad = 0.08f, topPad = 0.08f;
            float totalH = 1f - botPad - topPad;
            float segH   = totalH / CORE_SEGS;
            float segPad = 0.008f;
            Color segFull = isPlayer ? PlayerSolid : EnemySolid;
            Color segEmpty = isPlayer
                ? new Color(0.02f, 0.12f, 0.20f)
                : new Color(0.20f, 0.04f, 0.02f);

            for (int i = 0; i < CORE_SEGS; i++)
            {
                float yMin = botPad + i * segH + segPad;
                float yMax = botPad + (i + 1) * segH - segPad;
                var segGo = new GameObject($"seg{i}");
                segGo.transform.SetParent(go.transform, false);
                var segRt = segGo.AddComponent<RectTransform>();
                segRt.anchorMin = new Vector2(0.12f, yMin);
                segRt.anchorMax = new Vector2(0.88f, yMax);
                segRt.offsetMin = segRt.offsetMax = Vector2.zero;
                var segImg = segGo.AddComponent<Image>();
                segImg.color = segFull;
                segImg.raycastTarget = false;
                segs[i] = segImg;
            }

            // HP numeric label at bottom
            var txtGo = new GameObject("txt");
            txtGo.transform.SetParent(go.transform, false);
            var txtRt = txtGo.AddComponent<RectTransform>();
            txtRt.anchorMin = new Vector2(0f, 0f); txtRt.anchorMax = new Vector2(1f, botPad);
            txtRt.offsetMin = txtRt.offsetMax = Vector2.zero;
            var txt = txtGo.AddComponent<Text>();
            txt.font = _font; txt.fontSize = 10; txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;

            // Per-core hit flash overlay
            var cfGo = new GameObject("coreFlash");
            cfGo.transform.SetParent(go.transform, false);
            var cfRt = cfGo.AddComponent<RectTransform>();
            cfRt.anchorMin = Vector2.zero; cfRt.anchorMax = Vector2.one;
            cfRt.offsetMin = new Vector2(-8, -8); cfRt.offsetMax = new Vector2(8, 8);
            var cfImg = cfGo.AddComponent<Image>();
            cfImg.color = new Color(1f, 1f, 1f, 0f);
            cfImg.raycastTarget = false;

            if (isPlayer)
            {
                _playerCoreBody  = body;
                _playerSegs      = segs;
                _playerCoreTxt   = txt;
                _playerCoreFlash = cfImg;
            }
            else
            {
                _enemyCoreBody   = body;
                _enemySegs       = segs;
                _enemyCoreTxt    = txt;
                _enemyCoreFlash  = cfImg;
            }
        }

        // ─────────────────────────────────────────────── public API ──────────

        /// <summary>
        /// Activates lane-targeting highlights. Pass validLanes (bool[5]) to restrict which lanes
        /// glow — used for turret deployment that requires a player-owned pylon.
        /// </summary>
        public void SetLaneTargeting(bool active, bool isAirUnit, bool[] validLanes = null)
        {
            _validLanes = active ? validLanes : null;
            for (int i = 0; i < CombatSim.LaneCount; i++)
            {
                if (_laneHighlight[i] == null) continue;
                bool eligible = active && (isAirUnit
                    ? i == CombatSim.AirLane
                    : i < CombatSim.GroundLanes);
                bool valid    = validLanes == null || (i < validLanes.Length && validLanes[i]);
                _laneHighlight[i].color = (eligible && valid)
                    ? new Color(0.2f, 0.9f, 1f, 0.10f)
                    : (eligible && !valid)
                        ? new Color(1f, 0.2f, 0.1f, 0.06f)  // dim red = blocked lane
                        : new Color(0, 0, 0, 0);
            }
        }

        /// <summary>Called during card drag-over to brighten the hovered lane.</summary>
        public void OnDragOver(Vector2 screenPos)
        {
            for (int i = 0; i < CombatSim.LaneCount; i++)
            {
                if (_laneRects[i] == null || _laneHighlight[i] == null) continue;
                bool valid   = _validLanes == null || (i < _validLanes.Length && _validLanes[i]);
                bool hovered = RectTransformUtility.RectangleContainsScreenPoint(
                    _laneRects[i], screenPos, null);
                if (hovered && valid)
                    _laneHighlight[i].color = new Color(0.4f, 1f, 0.6f, 0.22f);
                else if (!hovered && _laneHighlight[i].color.a > 0.15f && valid)
                    _laneHighlight[i].color = new Color(0.2f, 0.9f, 1f, 0.10f);
                else if (!valid)
                    _laneHighlight[i].color = new Color(1f, 0.2f, 0.1f, 0.06f);
            }
        }

        /// <summary>Returns the lane index (ground 0..GroundLanes-1, or AirLane) under a screen position, or -1.</summary>
        public int LaneAtScreenPos(Vector2 screenPos)
        {
            for (int i = 0; i < CombatSim.LaneCount; i++)
            {
                if (_laneRects[i] == null) continue;
                if (RectTransformUtility.RectangleContainsScreenPoint(_laneRects[i], screenPos, null))
                    return i;
            }
            return -1;
        }

        public void PlayFx(CrossoverFx fx)
        {
            switch (fx.Kind)
            {
                case CrossoverKind.LaserLane:
                    if (fx.Lane >= 0 && fx.Lane < CombatSim.GroundLanes)
                        StartCoroutine(LaserSweep(fx.Lane));
                    break;
                case CrossoverKind.Orbital:
                    StartCoroutine(FieldFlash(new Color(1f, 0.7f, 0.2f, 0.30f), 0.3f));
                    QueueBanner("ORBITAL STRIKE", new Color(1f, 0.7f, 0.2f));
                    AddTrauma(0.25f);
                    break;
                case CrossoverKind.Emp:
                    StartCoroutine(FieldFlash(new Color(0.5f, 0.9f, 1f, 0.45f), 0.4f));
                    QueueBanner("E M P", new Color(0.5f, 0.95f, 1f));
                    AddTrauma(0.5f);
                    break;
                case CrossoverKind.Shockwave:
                    StartCoroutine(FieldFlash(new Color(0.6f, 0.3f, 1f, 0.25f), 0.3f));
                    AddTrauma(0.15f);
                    break;
            }
        }

        public void QueueBanner(string msg, Color col) => _bannerQueue.Enqueue((msg, col));
        int   _traumaFrame;      // frame the current shake budget belongs to
        float _traumaFrameMax;   // strongest shake event already applied this frame

        /// <summary>
        /// Raise screen shake. A busy melee fires many shake events in a single frame (several
        /// hits, a death, a capture) and summing them pins trauma at maximum, so the battlefield
        /// never stops vibrating. Only the STRONGEST event in a frame contributes.
        /// </summary>
        public void AddTrauma(float t)
        {
            if (Time.frameCount != _traumaFrame) { _traumaFrame = Time.frameCount; _traumaFrameMax = 0f; }
            if (t <= _traumaFrameMax) return;
            _trauma = Mathf.Min(1f, _trauma - _traumaFrameMax + t);
            _traumaFrameMax = t;
        }

        // ──────────────────────────────────────────────── refresh ─────────────

        public void Refresh()
        {
            if (_session == null) return;
            var sim = _session.Combat;
            DrainEvents(sim);
            RefreshLanes(sim);
            RefreshCores(sim);
            RefreshUnits(sim);
            RefreshBanner();
            RefreshShake();
        }

        void DrainEvents(CombatSim sim)
        {
            var events = sim.Events;
            for (int i = 0; i < events.Count; i++)
            {
                var e = events[i];
                switch (e.Type)
                {
                    case CombatEventType.Spawned:
                        if (e.Unit >= 0 && e.Unit < sim.Units.Count)
                            _pendingSpawnDrop.Add(sim.Units[e.Unit].Id);
                        break;

                    case CombatEventType.Died:
                        if (e.Unit >= 0 && e.Unit < sim.Units.Count)
                        {
                            var u = sim.Units[e.Unit];
                            if (_views.TryGetValue(u.Id, out var v) && !v.Dying)
                            {
                                v.Dying = true;
                                AudioManager.Play(AudioManager.Sfx.UnitDeath);
                                StartCoroutine(DeathExplosion(u.Id, v));
                                // C4: nearest living enemy in the lane claims the kill and celebrates
                                UnitView killer = null; float best = 110f;
                                foreach (var kv2 in _views)
                                {
                                    var cand = kv2.Value;
                                    if (cand.Dying || cand.IsPlayer == v.IsPlayer || cand.Rt == null) continue;
                                    if (cand.Rt.parent != v.Rt.parent) continue;
                                    float d = Mathf.Abs(cand.Rt.anchoredPosition.x - v.Rt.anchoredPosition.x);
                                    if (d < best) { best = d; killer = cand; }
                                }
                                if (killer != null) StartCoroutine(KillHop(killer));
                            }
                        }
                        if (e.Flag) AddTrauma(0.08f);
                        break;

                    case CombatEventType.Hit:
                    {
                        UnitState au = null, tu = null;
                        UnitView  atkV = null, defV = null;
                        if (e.Unit >= 0 && e.Unit < sim.Units.Count)
                        { au = sim.Units[e.Unit]; _views.TryGetValue(au.Id, out atkV); }
                        if (e.Target >= 0 && e.Target < sim.Units.Count)
                        { tu = sim.Units[e.Target]; _views.TryGetValue(tu.Id, out defV); }
                        if (defV != null && defV.Dying) defV = null;

                        bool ranged = au != null && au.Spec.Range > 4f;

                        if (au != null && atkV != null && !atkV.Dying)
                        {
                            if (ranged)
                            {
                                // Ranged: fire the shot now; the target reacts when it LANDS,
                                // not when the trigger pulls — the flinch follows the projectile.
                                if (defV != null && tu != null && atkV.Rt != null && defV.Rt != null)
                                {
                                    Color pc = au.Team == Team.Player ? PlayerSolid : EnemySolid;
                                    Vector2 pSize = au.Spec.Id switch {
                                        "sniper"  => new Vector2(22f, 2f),
                                        "titan"   => new Vector2(8f,  7f),
                                        "hacker"  => new Vector2(7f,  7f),
                                        "turret"  => new Vector2(16f, 2f),
                                        _         => new Vector2(10f, 3f),
                                    };
                                    // Cross-plane fire (anything touching the air lane) draws on the
                                    // battlefield overlay so the tracer spans the gap between lanes.
                                    bool crossPlane = au.Spec.IsAir || tu.Spec.IsAir;
                                    if (crossPlane && _fieldOverlay != null)
                                        StartCoroutine(FieldTracer(
                                            FieldPoint(atkV.Rt), FieldPoint(defV.Rt), pc, pSize,
                                            dive: au.Spec.IsAir));
                                    else
                                    {
                                        int li = au.Spec.IsAir ? CombatSim.AirLane : Mathf.Clamp(au.Lane, 0, CombatSim.GroundLanes - 1);
                                        StartCoroutine(ProjectileShot(
                                            atkV.Rt.anchoredPosition, defV.Rt.anchoredPosition,
                                            _laneRects[li], pc, pSize));
                                    }
                                    StartCoroutine(DelayedDefenderReaction(defV, atkV, 0.13f, e.Flag, midpoint: false));
                                }
                                StartCoroutine(TroopAttackAnim(atkV, ranged: true));
                            }
                            else
                            {
                                // Melee: attacker plays its strike, and the defender reacts on the
                                // CONTACT frame (~windup + drive) so the exchange reads as one hit
                                // between two units instead of both twitching at the same instant.
                                StartCoroutine(TroopAttackAnim(atkV, ranged: false));
                                if (defV != null)
                                    StartCoroutine(DelayedDefenderReaction(defV, atkV, 0.11f, e.Flag, midpoint: true));
                            }
                        }
                        else if (defV != null)
                        {
                            // Attacker view unavailable — still register the impact on the defender.
                            DoDefenderReaction(defV, atkV, e.Flag, midpoint: false);
                        }
                        break;
                    }

                    case CombatEventType.CoreHit:
                    {
                        bool ownCore = e.Team == Team.Player;
                        // Core damage ticks constantly once a unit is in range, so a full-screen
                        // red flash on every tick strobes the whole battle. Reserve the screen
                        // flash for the moment it actually matters -- the last 10% of core HP --
                        // and communicate ordinary chip damage with a number on the core itself.
                        float coreFrac = ownCore
                            ? (sim.PlayerCoreMax > 0f ? sim.PlayerCoreHp / sim.PlayerCoreMax : 1f)
                            : (sim.EnemyCoreMax  > 0f ? sim.EnemyCoreHp  / sim.EnemyCoreMax  : 1f);
                        bool critical = ownCore && coreFrac <= 0.10f;

                        AddTrauma(critical ? 0.22f : (ownCore ? 0.06f : 0.03f));
                        var coreImg = ownCore ? _playerCoreFlash : _enemyCoreFlash;
                        if (coreImg != null) StartCoroutine(CoreFlash(coreImg, ownCore));
                        if (Time.time - _lastCoreSfx >= 0.18f)
                        {
                            _lastCoreSfx = Time.time;
                            AudioManager.Play(AudioManager.Sfx.CoreHit, ownCore ? 1f : 0.6f);
                        }
                        if (critical)
                            StartCoroutine(FieldFlash(new Color(1f, 0.08f, 0.04f, 0.45f), 0.4f));
                        SpawnCoreDamage(ownCore, e.Amount);

                        // Animate the attacker + core retaliation
                        bool hitEnemyCore = e.Team == Team.Enemy;
                        if (e.Unit >= 0 && e.Unit < sim.Units.Count)
                        {
                            var att = sim.Units[e.Unit];
                            if (_views.TryGetValue(att.Id, out var av) && !av.Dying)
                            {
                                if (!av.Lunging) StartCoroutine(AttackLunge(av));
                                int li  = att.Spec.IsAir ? CombatSim.AirLane : Mathf.Clamp(att.Lane, 0, CombatSim.GroundLanes - 1);
                                float lw = _laneRects[li].rect.width;
                                if (lw > 0f)
                                {
                                    Vector2 unitPos  = av.Rt.anchoredPosition;
                                    Vector2 coreEdge = new Vector2(hitEnemyCore ? lw : 0f, unitPos.y);
                                    Color   pCol     = hitEnemyCore ? PlayerSolid : EnemySolid;
                                    StartCoroutine(ProjectileShot(unitPos, coreEdge, _laneRects[li], pCol));
                                    StartCoroutine(CoreReturnFire(li, hitEnemyCore, 0.20f));
                                }
                            }
                        }
                        break;
                    }

                    case CombatEventType.PylonCaptured:
                        int pylLane = e.Target / 2;
                        QueueBanner(
                            $"LANE {pylLane + 1} — NODE {(e.Team == Team.Player ? "SECURED" : "LOST")}",
                            e.Team == Team.Player ? PlayerSolid : EnemySolid);
                        AudioManager.Play(AudioManager.Sfx.PylonCapture);
                        AddTrauma(0.12f);
                        break;

                    case CombatEventType.LaneControlChanged:
                        if (e.Flag)
                            QueueBanner(
                                $"LANE {e.Target + 1} {(e.Team == Team.Player ? "CONTROLLED" : "OVERRUN")}",
                                e.Team == Team.Player ? PlayerSolid : EnemySolid);
                        break;

                    case CombatEventType.SurgeChanged:
                        if (e.Flag)
                        {
                            QueueBanner(
                                e.Team == Team.Player ? "SURGE — ENEMY CORE EXPOSED" : "ENEMY SURGE — DEFEND",
                                e.Team == Team.Player ? PlayerSolid : EnemySolid);
                            AudioManager.Play(AudioManager.Sfx.SurgeActivate);
                            AddTrauma(0.3f);
                        }
                        break;

                    case CombatEventType.WaveTelegraph:
                        if (e.Target >= 0 && e.Target < CombatSim.GroundLanes) _waveWarnT[e.Target] = e.Amount;
                        break;

                    case CombatEventType.ActChanged:
                        QueueBanner(
                            $"ACT {(int)e.Amount} — {((int)e.Amount == 2 ? "ESCALATION" : "OVERRUN")}",
                            new Color(1f, 0.85f, 0.3f));
                        AudioManager.Play(AudioManager.Sfx.ActChange);
                        break;
                }
            }
            events.Clear();
        }

        void RefreshLanes(CombatSim sim)
        {
            for (int lane = 0; lane < CombatSim.GroundLanes; lane++)
            {
                // Floor coverage driven by pylon ownership (starts 0% each side)
                float playerReach = 0f;
                float enemyReach  = 1f;
                for (int i = 0; i < 2; i++)
                {
                    var p = sim.Pylons[lane * 2 + i];
                    float nx = p.X / CombatSim.LaneLength;
                    if (p.Owner == Owner.Player) playerReach = Mathf.Max(playerReach, nx);
                    if (p.Owner == Owner.Enemy)  enemyReach  = Mathf.Min(enemyReach,  nx);
                }
                _playerFloor[lane].rectTransform.anchorMax = new Vector2(playerReach, TerritoryStrip);
                _enemyFloor[lane].rectTransform.anchorMin  = new Vector2(enemyReach,  0f);
                _enemyFloor[lane].rectTransform.anchorMax  = new Vector2(1f, TerritoryStrip);

                for (int i = 0; i < 2; i++)
                {
                    var p = sim.Pylons[lane * 2 + i];
                    Color owned = p.Owner == Owner.Player ? PlayerSolid
                                : p.Owner == Owner.Enemy  ? EnemySolid : NeutralGrey;
                    if (p.Contested)
                        owned = Color.Lerp(owned, Color.white, Mathf.PingPong(Time.time * 4f, 1f));
                    _pylonBorder[lane, i].color = new Color(owned.r, owned.g, owned.b,
                                                            p.Contested ? 1f : 0.95f);
                    _pylonBody[lane, i].color   = new Color(owned.r, owned.g, owned.b,
                                                            p.Contested ? 0.30f : 0.16f);
                    _pylonLine[lane, i].color = new Color(owned.r, owned.g, owned.b,
                                                          p.Contested ? 0.90f : 0.55f);
                    _pylonRing[lane, i].fillAmount = Mathf.Abs(p.Progress) / 100f;
                    _pylonRing[lane, i].color = p.Progress > 0 ? PlayerSolid
                                             : p.Progress < 0 ? EnemySolid : NeutralGrey;
                }

                if (_waveWarnT[lane] > 0f)
                {
                    _waveWarnT[lane] -= Time.deltaTime;
                    float a = Mathf.PingPong(Time.time * 5f, 0.55f);
                    _waveWarn[lane].color = new Color(1f, 0.2f, 0.1f, a);
                }
                else if (_waveWarn[lane].color.a > 0f)
                    _waveWarn[lane].color = new Color(1f, 0.2f, 0.1f, 0f);
            }
        }

        void RefreshCores(CombatSim sim)
        {
            UpdateCore(_playerCoreBody, _playerSegs, _playerCoreTxt,
                sim.PlayerCoreHp, sim.PlayerCoreMax, true);
            UpdateCore(_enemyCoreBody, _enemySegs, _enemyCoreTxt,
                sim.EnemyCoreHp, sim.EnemyCoreMax, false);
        }

        void UpdateCore(Image body, Image[] segs, Text txt, float hp, float max, bool isPlayer)
        {
            float frac    = Mathf.Clamp01(hp / max);
            int litCount  = Mathf.CeilToInt(frac * CORE_SEGS);
            txt.text      = Mathf.CeilToInt(Mathf.Max(0, hp)).ToString();

            Color fullCol  = isPlayer ? PlayerSolid  : EnemySolid;
            Color warnCol  = new Color(1f, 0.55f, 0.1f);
            Color critCol  = isPlayer ? new Color(1f, 0.15f, 0.05f) : new Color(1f, 0.3f, 0.08f);
            Color emptyCol = isPlayer ? new Color(0.02f, 0.12f, 0.20f) : new Color(0.20f, 0.04f, 0.02f);

            Color baseBody = isPlayer ? new Color(0.07f, 0.18f, 0.3f) : new Color(0.3f, 0.08f, 0.06f);
            body.color = frac < 0.33f
                ? Color.Lerp(baseBody, critCol, 0.3f + Mathf.PingPong(Time.time * 3f, 0.35f))
                : frac < 0.66f ? Color.Lerp(baseBody, warnCol, 0.25f)
                : baseBody;

            for (int i = 0; i < CORE_SEGS; i++)
            {
                if (segs[i] == null) continue;
                if (i >= litCount)
                {
                    segs[i].color = emptyCol;
                    continue;
                }
                if (frac < 0.30f)
                    // Critical: staggered red strobe per segment
                    segs[i].color = Color.Lerp(critCol, Color.white,
                        Mathf.PingPong(Time.time * 5f + i * 0.25f, 0.5f));
                else if (frac < 0.60f)
                    segs[i].color = Color.Lerp(fullCol, warnCol, (0.60f - frac) / 0.30f);
                else
                    segs[i].color = fullCol;
            }
        }

        bool _ambientOn;

        // Two drifting light motes per lane — the battlefield idles alive
        void EnsureAmbient()
        {
            if (_ambientOn || _laneRects == null || _laneRects[0] == null) return;
            _ambientOn = true;
            for (int i = 0; i < _laneRects.Length; i++)
                for (int m = 0; m < 2; m++)
                    StartCoroutine(AmbientMote(_laneRects[i], i * 2 + m));
        }

        IEnumerator AmbientMote(RectTransform lane, int seed)
        {
            if (lane == null) yield break;
            var go = new GameObject("mote");
            go.transform.SetParent(lane, false);
            go.transform.SetAsFirstSibling(); // behind units
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(3f, 3f);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            Color ac = NeonTheme.Active.Accent;
            float speed = 9f + (seed % 3) * 4f;
            float phase = seed * 137.3f;
            while (lane != null && rt != null)
            {
                float laneW = lane.rect.width; if (laneW <= 0f) laneW = 900f;
                float t = Time.time;
                float mx = Mathf.Repeat(phase + t * speed, laneW);
                float my = Mathf.Sin(t * 0.6f + seed * 1.7f) * 12f;
                rt.anchoredPosition = new Vector2(mx, my);
                float a = 0.10f + 0.08f * Mathf.Sin(t * 1.3f + seed);
                img.color = new Color(ac.r, ac.g, ac.b, a);
                yield return null;
            }
        }

        float _musicInt = 0.3f;
        float _lastDistress;

        void RefreshUnits(CombatSim sim)
        {
            float laneW = _laneRects[0].rect.width;
            if (laneW <= 0) laneW = 900f;
            EnsureAmbient();

            // Music swells when troops are on the field, settles during build-up
            if (!sim.Finished)
            {
                _musicInt = Mathf.MoveTowards(_musicInt, sim.Units.Count > 0 ? 1f : 0.25f, Time.deltaTime * 0.6f);
                AudioManager.SetMusicIntensity(_musicInt);
            }

            _toRemove.Clear();
            foreach (var kv in _views)
                if (!kv.Value.Dying) _toRemove.Add(kv.Key);

            var units = sim.Units;
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                if (!u.Alive) continue;
                _toRemove.Remove(u.Id);

                if (!_views.TryGetValue(u.Id, out var v))
                {
                    v = AcquireView(u);
                    _views[u.Id] = v;
                    // Deploy effect: player units only -- an enemy spawn is not a moment the
                    // player chose, so decorating it would be noise.
                    // It CANNOT fire here. At this point the view has only just been acquired
                    // from the pool: its parent lane is set a few lines below and its
                    // anchoredPosition not until much later in this same loop, so an effect
                    // spawned now lands at whatever position the pooled view last held --
                    // usually off-screen. Flag it and fire once the unit is actually placed.
                    v.PendingDeployFx = v.IsPlayer;
                }

                int laneIdx = u.Spec.IsAir ? CombatSim.AirLane : Mathf.Clamp(u.Lane, 0, CombatSim.GroundLanes - 1);
                var lane = _laneRects[laneIdx];
                if (v.Rt.parent != lane) v.Rt.SetParent(lane, false);

                float targetPx = (u.X / CombatSim.LaneLength) * laneW;
                float jitter   = ((u.Id * 37) % 14) - 7f;
                float xStagger = (u.Team == Team.Player ? -1f : 1f) * (int)(u.Id % 3) * 8f;

                // Velocity-smoothed motion: units accelerate from rest, cruise, and brake
                // into position instead of sliding at a constant lerp.
                if (v.VisualX < 0f) { v.VisualX = targetPx; v.VelX = 0f; }
                float smoothT = 0.11f / Mathf.Max(0.25f, GameSettings.BattleSpeed);
                v.VisualX = Mathf.SmoothDamp(v.VisualX, targetPx, ref v.VelX, smoothT,
                    float.PositiveInfinity, Time.deltaTime);

                bool isAir = v.SpecId == "drone" || v.SpecId == "interceptor";
                float hpFrac = Mathf.Clamp01(u.Hp / u.Spec.MaxHp);

                // State auto-recovery: when no coroutine owns the unit, it is marching.
                if (!v.Lunging && v.Anim != UAnim.March && v.Anim != UAnim.Stagger)
                    v.Anim = UAnim.March;
                // Release the attack pose once no coroutine owns the unit. Done here rather than
                // at each coroutine's tail so every early-exit path (death, interrupt) recovers.
                if (!v.Lunging && v.AttackPose >= 0) SetAttackPose(v, -1);
                // Walk weight: combat states settle the walk cycle instead of cutting it
                bool marching = v.Anim == UAnim.March && !v.Dying;
                v.BobW = Mathf.MoveTowards(v.BobW, marching ? 1f : 0f, Time.deltaTime * 9f);

                // Gait phase. Air hovers on a clock; ground legs step from ACTUAL velocity —
                // one step per stride-length of travel, so cadence always matches movement:
                // blocked units plant, slow units amble, fast units hustle.
                float moveW = 1f;
                if (isAir)
                {
                    v.BobPhase += Time.deltaTime * 4.0f;
                }
                else if (v.SpecId != "turret")
                {
                    // Stride lengths are tuned for READABILITY, not physical scale. The titan is the
                    // slowest unit (Speed 1.5) and had the longest stride (26), which worked out to
                    // one step every 3.4 seconds -- it slid down the lane with its legs almost
                    // frozen. Heavies now step visibly; a cadence floor keeps anything that is
                    // moving at all from looking like it is gliding.
                    float stride = v.SpecId switch { "titan" => 11f, "mech" => 14f, "shield-bot" => 13f, "sniper" => 15f, _ => 13f };
                    stride *= 1f + ((int)((u.Id * 17UL) % 7UL) - 3) * 0.03f; // C9: gait variance
                    // C5 wounded gait: rhythm turns irregular below 30%
                    float wob = hpFrac < 0.3f
                        ? 1f + 0.18f * Mathf.Sin(Time.time * 3.3f + v.BobPhase * 0.7f) : 1f;
                    float cadence = Mathf.Abs(v.VelX) * (Mathf.PI / stride);
                    // Gait weight is measured against the unit's OWN cruising speed, so a slow
                    // heavy reaches a full stride instead of hovering under the walk threshold.
                    float refVel = Mathf.Max(6f, u.Spec.Speed * (laneW / CombatSim.LaneLength));
                    moveW = Mathf.Clamp01(Mathf.Abs(v.VelX) / (refVel * 0.7f));
                    // The floor was 4.0, which put a whole stride at 1.57s -- with only two
                    // frames that was ~1.3 swaps a second, the exact rate at which a two-state
                    // toggle reads as a blinking light. Four frames want a slightly brisker
                    // clock: 5.5 gives a 1.14s stride, two body bounces, and ~3.5 frames/sec.
                    if (moveW > 0.1f) cadence = Mathf.Max(cadence, 5.5f);
                    v.BobPhase += cadence * Time.deltaTime * wob;
                }

                float bob;
                float sqX = 1f, sqY = 1f;
                if (isAir)
                {
                    bob = Mathf.Sin(v.BobPhase) * 2.0f; // smooth hover — air never fully settles
                    // Rotor blur. Air units never ran the pose swapper, so a quadrotor slid
                    // through the sky with frozen blades. Cycle its frames on a fast clock
                    // that is independent of the hover bob.
                    if (NeonArt.HasPoses(v.ArtId) && v.AttackPose < 0)
                    {
                        // 26Hz was authored for a quadrotor whose two frames differ only by a
                        // rotor smear -- at that rate it reads as blur. The authored flyers
                        // (pigeon, rogue, phoenix, ember) move a whole wing or the entire body
                        // between frames, so alternating them 26 times a second reads as the
                        // sprite glitching rather than flapping. They get a wing-beat cadence,
                        // offset per unit so a flight does not strobe in lockstep.
                        float flapHz = NeonArt.IsAuthoredArt(v.ArtId) ? 5.5f : 26f;
                        float phase  = (u.Id % 7UL) * 0.09f;
                        int rf = ((int)((Time.time + phase) * flapHz) & 1) + 1;
                        if (rf != v.PoseFrame)
                        {
                            v.PoseFrame = rf;
                            v.Body.texture = NeonArt.HasWeaponPart(v.ArtId)
                                ? NeonArt.UnitBody(v.ArtId, v.IsPlayer, rf)
                                : NeonArt.Unit(v.ArtId, v.IsPlayer, rf);
                        }
                    }
                }
                else if (v.SpecId == "turret")
                {
                    bob = 0f;
                }
                else
                {
                    // Hop walk-cycle: |sin| bounce with subtle squash at ground contact.
                    // Weighted by BobW (combat settle) AND moveW (actual speed).
                    float walkW  = v.BobW * moveW;
                    float hop    = Mathf.Abs(Mathf.Sin(v.BobPhase));
                    float hopAmt = v.SpecId switch { "titan" or "mech" => 1.0f, "shield-bot" => 1.3f, _ => 1.4f };
                    bob = hop * hopAmt * walkW;
                    float ground = 1f - hop;
                    sqX = Mathf.Lerp(1f, 1f + ground * 0.03f, walkW);
                    sqY = Mathf.Lerp(1f, 1f - ground * 0.04f, walkW);

                    // Heavy classes kick up dust on each ground contact — only when moving
                    if ((v.SpecId == "titan" || v.SpecId == "mech")
                        && v.PrevHop > 0.15f && hop <= 0.06f && walkW > 0.4f && !v.Dying)
                        SpawnStompDust(v);
                    v.PrevHop = hop;

                    // Sniper: permanent low firing stance
                    if (v.SpecId == "sniper") { sqY *= 0.95f; sqX *= 1.04f; }

                    // C5 wounded hunch
                    if (hpFrac < 0.3f) sqY *= 0.975f;

                    // Ambient breathing — a slow whole-body swell that is strongest when the
                    // unit is settled and fades out under the walk cycle. The baked sprite has
                    // no separate torso to move, so idle life reads as a subtle full-body
                    // squash; without it a stalled frontline sits frozen.
                    float settle  = 1f - moveW;
                    float breathe = Mathf.Sin(Time.time * 1.9f + (float)((u.Id * 13UL) % 628UL) * 0.01f)
                                    * 0.02f * settle;
                    sqY += breathe;
                    sqX -= breathe * 0.5f;

                    // Iteration 3: 2-frame walk cycle — legs alternate stride/pass each
                    // half-step; units plant into the neutral stance when settled OR stopped.
                    if (NeonArt.HasPoses(v.ArtId))
                    {
                        // An attack pose owns the body while it is set; otherwise the gait drives it.
                        // Four-beat walk. The old test picked between two frames on the SIGN
                        // of sin, so the cycle was contact / pass and then straight back to
                        // contact -- two poses swapping, with no beat between them, which is
                        // why it read as a light blinking rather than a leg travelling.
                        // BobPhase now indexes quarter-turns, giving the full loop:
                        // contact, pass, contact on the other side, pass.
                        // A rigged unit drives its limbs from the CONTINUOUS phase while it is
                        // walking, and falls back to the baked frames for idle, windup, strike
                        // and flinch -- those poses are authored per unit and read better hand
                        // placed than interpolated.
                        bool wantRig = v.Rigged && v.AttackPose < 0 && walkW > 0.4f;
                        if (v.Rigged) DriveLimbRig(v, wantRig, walkW);

                        int frame = v.AttackPose >= 0
                            ? v.AttackPose
                            : (walkW > 0.4f
                               ? Walk4[(int)(v.BobPhase / (Mathf.PI * 0.5f)) & 3]
                               : 0);
                        if (wantRig) frame = -1;          // body-only texture, limbs are live
                        if (frame != v.PoseFrame)
                        {
                            v.PoseFrame = frame;
                            v.Body.texture = frame < 0
                                ? NeonArt.UnitNoLimbs(v.ArtId, v.IsPlayer)
                                : (NeonArt.HasWeaponPart(v.ArtId)
                                    ? NeonArt.UnitBody(v.ArtId, v.IsPlayer, frame)
                                    : NeonArt.Unit(v.ArtId, v.IsPlayer, frame));
                        }
                    }
                }

                // C5 wounded sparks — occasional pop of damage particles
                if (hpFrac < 0.3f && !v.Dying && Time.time >= v.NextWound)
                {
                    v.NextWound = Time.time + Random.Range(1.2f, 2.4f);
                    var wparent = v.Rt.parent as RectTransform;
                    if (wparent != null)
                        StartCoroutine(DustPuff(wparent,
                            v.Rt.anchoredPosition + new Vector2(Random.Range(-6f, 6f), Random.Range(-4f, 6f)) * v.UScale,
                            new Color(1f, 0.62f, 0.25f, 0.6f)));
                    if (Time.time - _lastDistress > 1.4f)
                    {
                        _lastDistress = Time.time;
                        AudioManager.Play(AudioManager.Sfx.UnitDistress, 0.30f, v.VoicePitch);
                    }
                }

                // Body displacement is damped well below the size multiplier. Lunge and
                // knockback were authored for ~34px units; scaling them by the full 3.2x threw
                // every fighting unit 20-30px per hit, so a melee read as a mass of shaking
                // sprites. The ARM now carries the attack (poses 3/4), so the body only needs a
                // hint of follow-through. Gait bob keeps the full scale.
                if (!v.Dying)
                {
                    float lungeK  = v.UScale * 0.38f;
                    float jitterK = v.UScale * 0.5f;
                    v.Rt.anchoredPosition = new Vector2(
                        v.VisualX + v.LungeOffset * lungeK + xStagger * v.UScale,
                        jitter * jitterK + (bob + v.SpawnYOffset) * v.UScale);
                }

                // The unit now has its real lane and position, so the deploy effect can land
                // where the troop actually is.
                if (v.PendingDeployFx)
                {
                    v.PendingDeployFx = false;
                    PlayDeployFx(v);
                }

                if (v.IsPlayer) DriveVfxSkin(v);

                // Idle rotation life + smooth auto-uprighting after attack leans
                if (!v.Dying && !v.Lunging)
                {
                    if (v.SpecId == "turret")
                    {
                        // Barrels sweep on the weapon layer; the base never rotates
                        var scanRt = v.Weapon != null && v.Weapon.enabled ? v.Weapon.rectTransform : v.Rt;
                        scanRt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(Time.time * 0.9f + v.BobPhase) * 4f);
                    }
                    else if (isAir)
                        v.Rt.localRotation = Quaternion.Slerp(v.Rt.localRotation,
                            Quaternion.Euler(0f, 0f, (v.IsPlayer ? -1f : 1f) * Mathf.Sin(v.BobPhase * 0.8f) * 2.5f),
                            Time.deltaTime * 10f);
                    else
                    {
                        // Walkers lean into their march. The side-profile art swings its legs
                        // fore/aft, so a small forward tilt (proportional to actual gait weight)
                        // is what sells momentum; it eases back to upright when they stop.
                        float leanW = v.BobW * moveW;
                        float lean  = (v.IsPlayer ? -1f : 1f) * 4f * leanW;
                        var want = Quaternion.Euler(0f, 0f, lean);
                        if (v.Rt.localRotation != want)
                            v.Rt.localRotation = Quaternion.Slerp(v.Rt.localRotation, want, Time.deltaTime * 12f);
                    }
                }

                // Materialize scale-in × walk squash. Never write scale while a coroutine
                // owns it (attack anims, landing squash, hit punch) — that stomps the effect.
                float age = Time.time - v.SpawnTime;
                float s   = age >= 0.25f ? 1f : EaseOut(age / 0.25f);
                if (!v.Dying && !v.Lunging && !v.ScaleAnim)
                    v.Rt.localScale = new Vector3(s * sqX, s * sqY, 1f);

                // C3 idle fidgets — a micro-action every few seconds while marching
                if (marching && !v.Lunging && Time.time >= v.NextFidget)
                {
                    v.NextFidget = Time.time + Random.Range(3.5f, 7f);
                    if ((v.SpecId == "titan" || v.SpecId == "mech" || v.SpecId == "shield-bot") && !v.ScaleAnim)
                        StartCoroutine(ChestPulse(v)); // heavies flex
                    else if (v.SpecId == "sniper" && v.AttackFlash != null)
                        StartCoroutine(ScopeGlint(v)); // lens catches the light
                    else if (!v.ScaleAnim && v.SpecId != "turret" && !isAir)
                        StartCoroutine(IdleShift(v)); // light troops: a subtle weight-shift so they read as alive
                }

                // EMP stun is REAL — an 8+ combo or the EMP gem sets UnitState.Stun and the sim
                // stops the unit dead — but nothing ever rendered it, so a stunned unit looked
                // identical to one standing still. Arc-flash the body for as long as it holds.
                if (v.HitFlash != null)
                {
                    if (u.Stun > 0f && !v.Dying)
                    {
                        float sp = 0.30f + 0.34f * Mathf.Abs(Mathf.Sin(Time.time * 13f));
                        v.HitFlash.color = new Color(0.58f, 0.86f, 1f, sp * 0.55f);
                    }
                    else if (v.HitFlash.color.a > 0.001f)
                    {
                        v.HitFlash.color = Color.clear;
                    }
                }

                v.Hp.fillAmount = hpFrac;                       // harmless if a sprite is added later
                v.Hp.rectTransform.anchorMax = new Vector2(hpFrac, 1f);   // the bar that actually shrinks
                // Team-coded health, read at a glance without checking which side a unit is on:
                // OURS  green -> amber -> red as it drops (the classic "my guy is in trouble" ramp)
                // THEIRS violet -> magenta -> hot red (never green, so a healthy enemy is never
                //        mistaken for one of ours in a crowded lane).
                Color hpCol = v.IsPlayer
                    ? (hpFrac > 0.5f
                        ? Color.Lerp(new Color(1f, 0.72f, 0.10f), new Color(0.20f, 0.95f, 0.28f), (hpFrac - 0.5f) * 2f)
                        : Color.Lerp(new Color(0.95f, 0.15f, 0.10f), new Color(1f, 0.72f, 0.10f), hpFrac * 2f))
                    : (hpFrac > 0.5f
                        ? Color.Lerp(new Color(0.94f, 0.26f, 0.72f), new Color(0.62f, 0.32f, 0.98f), (hpFrac - 0.5f) * 2f)
                        : Color.Lerp(new Color(1f, 0.22f, 0.26f), new Color(0.94f, 0.26f, 0.72f), hpFrac * 2f));
                v.Hp.color = hpCol;

                // The bar is a CHILD of the unit, so it inherited every lean and squash the body
                // does -- which is why health bars sat permanently tilted. Cancel the parent's
                // rotation and squash so the bar always reads level.
                if (v.HpBg != null)
                {
                    var hpRt = v.HpBg.rectTransform;
                    hpRt.localRotation = Quaternion.Inverse(v.Rt.localRotation);
                    var ps = v.Rt.localScale;
                    hpRt.localScale = new Vector3(
                        Mathf.Approximately(ps.x, 0f) ? 1f : 1f / ps.x,
                        Mathf.Approximately(ps.y, 0f) ? 1f : 1f / ps.y, 1f);
                }

                // Damage darkening — body dims steadily below 35% HP (no strobe).
                // Hits do NOT recolor the troop: damage feedback is the HP-bar border
                // flash + scale punch in HitFlashCoroutine.
                float dark = hpFrac < 0.35f ? Mathf.Lerp(0.55f, 1f, hpFrac / 0.35f) : 1f;
                var dmgCol = new Color(v.BaseBody.r * dark, v.BaseBody.g * dark, v.BaseBody.b * dark, v.BaseBody.a);
                if (v.Body.color != dmgCol)
                {
                    v.Body.color = dmgCol;
                    if (v.Weapon.enabled) v.Weapon.color = dmgCol;
                }

                if (v.ActiveSkin > 0) AnimateSkinVfx(v);

                // Idle ambient glow pulse — per-class breathing, only when no skin VFX running
                if (v.ActiveSkin == 0)
                {
                    float baseAlpha = v.SpecId switch
                    {
                        "titan"                  => 0.80f,
                        "mech"                   => 0.70f,
                        "shield-bot"             => 0.68f,
                        "turret"                 => 0.65f,
                        "trooper" or "hacker"    => 0.60f,
                        "sniper"                 => 0.52f,
                        "drone" or "interceptor" => 0.48f,
                        _                        => 0.55f,
                    };
                    float idleT = Time.time + v.BobPhase * 0.5f;
                    float pulse = v.SpecId switch
                    {
                        "titan"   => 0.08f * Mathf.Sin(idleT * 0.7f),
                        "hacker"  => 0.10f * Mathf.Sin(idleT * 2.2f),
                        "drone"   => 0.06f * Mathf.Sin(idleT * 3.5f),
                        _         => 0.05f * Mathf.Sin(idleT * 1.5f),
                    };
                    v.Glow.color = new Color(1f, 1f, 1f, Mathf.Clamp01(baseAlpha + pulse));
                }

                // Critical HP: shift glow toward red below 20%
                if (hpFrac < 0.2f && v.Glow != null)
                {
                    float danger = 1f - hpFrac / 0.2f; // 0→1 as hp→0
                    var gc = v.Glow.color;
                    v.Glow.color = new Color(
                        Mathf.Lerp(gc.r, 1.0f, danger * 0.6f),
                        Mathf.Lerp(gc.g, 0.15f, danger * 0.6f),
                        Mathf.Lerp(gc.b, 0.15f, danger * 0.6f),
                        gc.a);
                }
            }

            foreach (ulong id in _toRemove)
            {
                ReleaseView(_views[id]);
                _views.Remove(id);
            }
        }

        void RefreshBanner()
        {
            if (_bannerT > 0f)
            {
                _bannerT -= Time.deltaTime;
                var c = _banner.color;
                c.a = Mathf.Clamp01(_bannerT / 0.4f);
                _banner.color = c;
                if (_bannerT > 0f) return;
            }
            if (_bannerQueue.Count > 0)
            {
                var (msg, col) = _bannerQueue.Dequeue();
                _banner.text = msg;
                _banner.color = col;
                _bannerT = 1.4f;
            }
            else _banner.text = "";
        }

        void RefreshShake()
        {
            if (!GameSettings.CameraShake || _trauma <= 0f)
            {
                _trauma = 0f;
                if (_container.anchoredPosition != _basePos) _container.anchoredPosition = _basePos;
                return;
            }
            _trauma = Mathf.Max(0f, _trauma - 2.8f * Time.deltaTime);
            float amp = _trauma * _trauma * 7f;
            _container.anchoredPosition = _basePos + new Vector2(
                (Mathf.PerlinNoise(Time.time * 25f, 0f) - 0.5f) * 2f * amp,
                (Mathf.PerlinNoise(0f, Time.time * 25f) - 0.5f) * 2f * amp);
        }

        // ────────────────────────────────────────────── unit views ────────────

        UnitView AcquireView(UnitState u)
        {
            UnitView v = _pool.Count > 0 ? _pool.Pop() : CreateView();
            v.SpawnTime    = Time.time;
            v.Dying        = false;
            v.Lunging      = false;
            v.IsPlayer     = u.Team == Team.Player;
            v.BobPhase     = Random.Range(0f, Mathf.PI * 2f); // randomize so units don't sync
            v.VfxPhase     = Random.Range(0f, Mathf.PI * 2f);
            v.LungeOffset  = 0f;
            v.SpawnYOffset = 0f;
            v.VisualX      = -1f;   // sentinel — initialized to real position on first refresh
            v.Rt.gameObject.SetActive(true);
            StyleView(v, u);
            SetupSkinVfx(v);

            // Trigger spawn-drop if the Spawned event was received
            if (_pendingSpawnDrop.Remove(u.Id))
                StartCoroutine(SpawnDrop(v));

            return v;
        }

        void ReleaseView(UnitView v)
        {
            // VFX-skin elements are siblings of the unit, not children, so they do NOT die with
            // it -- they have to be destroyed explicitly. This used to sit at one of the two
            // ReleaseView call sites; the other released without it, and every unit that took
            // that path left its particles behind on the lane forever. Cleaning up here means
            // both paths are covered by construction.
            if (v.SkinFx != null)
            {
                for (int i = 0; i < v.SkinFx.Length; i++)
                    if (v.SkinFx[i]) Destroy(v.SkinFx[i].gameObject);
                v.SkinFx = null;
            }
            v.SkinFxPhase = 0f;
            v.PendingDeployFx = false;
            if (v.Body)     v.Body.color     = Color.white;
            if (v.Glow)     v.Glow.color     = new Color(1f, 1f, 1f, 0.55f);
            if (v.HitFlash) v.HitFlash.color = new Color(1f, 1f, 1f, 0f);
            if (v.VfxA)     { v.VfxA.color = Color.clear; v.VfxA.sprite = null; }
            if (v.VfxB)     { v.VfxB.color = Color.clear; v.VfxB.sprite = null; }
            if (v.VfxC)     { v.VfxC.color = Color.clear; v.VfxC.sprite = null; }
            if (v.VfxD)     { v.VfxD.color = Color.clear; v.VfxD.sprite = null; }
            if (v.VfxE)        { v.VfxE.color = Color.clear; v.VfxE.sprite = null; }
            if (v.AttackFlash) { v.AttackFlash.color = Color.clear; v.AttackFlash.rectTransform.localScale = Vector3.one; }
            // Reset rotations from Inferno flame tongues
            if (v.VfxB)     v.VfxB.rectTransform.localRotation = Quaternion.identity;
            if (v.VfxC)     v.VfxC.rectTransform.localRotation = Quaternion.identity;
            if (v.VfxD)     v.VfxD.rectTransform.localRotation = Quaternion.identity;
            v.ActiveSkin = 0;
            v.ScaleAnim = false;
            if (v.HpBg) v.HpBg.color = HpTrackDark;
            if (v.Hp)
            {
                v.Hp.rectTransform.anchorMax = Vector2.one;
                v.Hp.fillAmount = 1f;
            }
            if (v.Weapon)
            {
                v.Weapon.rectTransform.anchoredPosition = Vector2.zero;
                v.Weapon.rectTransform.localRotation    = Quaternion.identity;
                v.Weapon.enabled = false;
            }
            v.Rt.localRotation = Quaternion.identity;
            v.Rt.localScale = Vector3.one;
            v.Rt.gameObject.SetActive(false);
            _pool.Push(v);
        }

        UnitView CreateView()
        {
            var go = new GameObject("Unit");
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            var glowGo = new GameObject("glow");
            glowGo.transform.SetParent(go.transform, false);
            var glowRt = glowGo.AddComponent<RectTransform>();
            glowRt.anchorMin = Vector2.zero; glowRt.anchorMax = Vector2.one;
            glowRt.offsetMin = new Vector2(-10, -10); glowRt.offsetMax = new Vector2(10, 10);
            var glowImg = glowGo.AddComponent<RawImage>();
            glowImg.raycastTarget = false;

            var bodyGo = new GameObject("body");
            bodyGo.transform.SetParent(go.transform, false);
            var bodyRt = bodyGo.AddComponent<RectTransform>();
            bodyRt.anchorMin = Vector2.zero; bodyRt.anchorMax = Vector2.one;
            bodyRt.offsetMin = bodyRt.offsetMax = Vector2.zero;
            var bodyImg = bodyGo.AddComponent<RawImage>();
            bodyImg.raycastTarget = false;

            // Weapon layer — same canvas as body, moves/rotates independently for recoil & aim
            var wpnGo = new GameObject("weapon");
            wpnGo.transform.SetParent(go.transform, false);
            var wpnRt = wpnGo.AddComponent<RectTransform>();
            wpnRt.anchorMin = Vector2.zero; wpnRt.anchorMax = Vector2.one;
            wpnRt.offsetMin = wpnRt.offsetMax = Vector2.zero;
            var wpnImg = wpnGo.AddComponent<RawImage>();
            wpnImg.raycastTarget = false;
            wpnImg.enabled = false;

            // VFX images inserted between body and HP bar so flames render above body but under HP
            // VfxA: full-stretch overlay (base glow / metallic coat / ghost tint)
            var vaGo = new GameObject("vfxA"); vaGo.transform.SetParent(go.transform, false);
            var vaRt = vaGo.AddComponent<RectTransform>();
            vaRt.anchorMin = Vector2.zero; vaRt.anchorMax = Vector2.one;
            vaRt.offsetMin = vaRt.offsetMax = Vector2.zero;
            var vaImg = vaGo.AddComponent<Image>(); vaImg.raycastTarget = false; vaImg.color = Color.clear;

            // VfxB–E: point-anchored particles (flame tongues, shine stripes, ghost trails, embers)
            Image vbImg = null, vcImg = null, vdImg = null, veImg = null;
            string[] vfxNames = { "vfxB", "vfxC", "vfxD", "vfxE" };
            Image[]  vfxOut   = new Image[4];
            for (int vi = 0; vi < 4; vi++)
            {
                var pGo = new GameObject(vfxNames[vi]);
                pGo.transform.SetParent(go.transform, false);
                var pRt = pGo.AddComponent<RectTransform>();
                pRt.anchorMin = pRt.anchorMax = new Vector2(0.5f, 0.5f);
                pRt.pivot = new Vector2(0.5f, 0.5f);
                pRt.sizeDelta = new Vector2(6f, 9f);
                var pImg = pGo.AddComponent<Image>(); pImg.raycastTarget = false; pImg.color = Color.clear;
                vfxOut[vi] = pImg;
            }
            vbImg = vfxOut[0]; vcImg = vfxOut[1]; vdImg = vfxOut[2]; veImg = vfxOut[3];

            // HP bar: dark track above unit + fill inside it (battery-bar style)
            var hpBg = new GameObject("hpBg");
            hpBg.transform.SetParent(go.transform, false);
            var hpBgRt = hpBg.AddComponent<RectTransform>();
            hpBgRt.anchorMin = new Vector2(0, 1); hpBgRt.anchorMax = new Vector2(1, 1);
            hpBgRt.pivot = new Vector2(0, 0);
            hpBgRt.anchoredPosition = new Vector2(0, 4);
            hpBgRt.sizeDelta = new Vector2(0, 8);
            var hpBgImg = hpBg.AddComponent<Image>();
            hpBgImg.color = new Color(0.08f, 0.08f, 0.08f, 0.96f);

            // HP fill is a child of the track so it shrinks visibly inside the dark frame
            var hpGo = new GameObject("hp");
            hpGo.transform.SetParent(hpBg.transform, false);
            var hpRt = hpGo.AddComponent<RectTransform>();
            hpRt.anchorMin = Vector2.zero; hpRt.anchorMax = Vector2.one;
            hpRt.offsetMin = new Vector2(1, 1); hpRt.offsetMax = new Vector2(-1, -1);
            var hpImg = hpGo.AddComponent<Image>();
            // Image.Type.Filled needs a SPRITE to compute its fill geometry. This bar has none,
            // so fillAmount was silently ignored and the bar only ever changed colour. Drive the
            // width from the anchor instead, which works on a plain sprite-less Image.
            hpImg.type = Image.Type.Simple;
            hpImg.color = Color.green;
            hpImg.raycastTarget = false;

            // Attack-part flash — per-troop weapon / body-part highlight during attack
            var atkGo = new GameObject("atkFlash");
            atkGo.transform.SetParent(go.transform, false);
            var atkRt = atkGo.AddComponent<RectTransform>();
            atkRt.anchorMin = atkRt.anchorMax = new Vector2(0.5f, 0.5f);
            atkRt.pivot = new Vector2(0.5f, 0.5f);
            atkRt.sizeDelta = new Vector2(12f, 12f);
            var atkImg = atkGo.AddComponent<Image>();
            // A spriteless Image renders as a hard-edged SQUARE. Every muzzle flash, spark and
            // dust puff in here was one, which read as chunky blocks once units scaled up --
            // most obviously as a big square "explosion" off the sniper's barrel. Soft radial
            // sprites make them read as light instead of geometry.
            atkImg.sprite = NeonArt.GlowCircle();
            atkImg.color = new Color(1f, 1f, 1f, 0f);
            atkImg.raycastTarget = false;

            // Hit flash overlay — topmost sibling so it always shows above everything
            var flashGo = new GameObject("flash");
            flashGo.transform.SetParent(go.transform, false);
            var flashRt = flashGo.AddComponent<RectTransform>();
            flashRt.anchorMin = Vector2.zero; flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = flashRt.offsetMax = Vector2.zero;
            var flashImg = flashGo.AddComponent<Image>();
            flashImg.color = new Color(1f, 1f, 1f, 0f);
            flashImg.raycastTarget = false;

            return new UnitView { Rt = rt, Body = bodyImg, Weapon = wpnImg, Glow = glowImg, Hp = hpImg,
                                   HpBg = hpBgImg, HitFlash = flashImg, AttackFlash = atkImg,
                                   VfxA = vaImg, VfxB = vbImg, VfxC = vcImg, VfxD = vdImg, VfxE = veImg };
        }

        void StyleView(UnitView v, UnitState u)
        {
            bool isPlayer = u.Team == Team.Player;
            Color teamColor = isPlayer ? NeonCosmetics.GetTroopTint() : NeonTheme.Active.EnemyTint;

            // SQUARE rects only — the art is a square 128×128 canvas; any non-square
            // rect stretches the whole unit (the "squashed troops" bug).
            // Sizes bumped ~1.5x alongside the 3-lane layout: at the old scale a trooper was
            // 34 units (~14 real px on a phone), where even a 2% animation swell is sub-pixel.
            // The taller lanes give the headroom for these to read without crowding.
            // Base sizes are the ORIGINAL authored art scale -- every hardcoded VFX offset in
            // this file (muzzle tips, sights) was derived from the 128-texture mapped onto these
            // rects, e.g. mech's cannon tip: texture x108 -> (108-64)/128 * 38 = +13.
            // UnitScale multiplies them uniformly, so the whole roster keeps its silhouette
            // hierarchy and every offset stays proportional. One constant tunes troop size.
            float w, h;
            switch (u.Spec.Id)
            {
                case "drone":       w = 30; break;
                case "trooper":     w = 34; break;
                case "sniper":      w = 38; break;
                case "mech":        w = 38; break;
                case "shield-bot":  w = 36; break;
                case "interceptor": w = 34; break;
                case "hacker":      w = 28; break;
                case "titan":       w = 50; break;
                case "turret":      w = 34; break;
                default:            w = 32; break;
            }
            w *= UnitScale;
            h = w;

            // C9 squad individuality — deterministic per-unit variance from the id
            float sizeVar = 1f + ((int)((u.Id * 73UL) % 13UL) - 6) * 0.01f;  // ±6% size
            float hueVar  = ((int)((u.Id * 31UL) % 9UL) - 4) * 0.012f;       // ±4.8% warm/cool shift
            w *= sizeVar; h *= sizeVar;

            v.Rt.sizeDelta  = new Vector2(w, h);
            v.Rt.localScale = Vector3.one;
            // All the per-unit VFX offsets below (muzzle flashes, sights, lunge/hop distances)
            // were authored against a ~52-unit body. Keep them proportional at any size.
            v.UScale = UnitScale;
            // Face the direction of travel: weapons are baked pointing right, so
            // enemies (marching left) get a horizontal uv mirror. All dir-based VFX
            // offsets (muzzles, sights) then land on the drawn features.
            var uv = isPlayer ? new Rect(0f, 0f, 1f, 1f) : new Rect(1f, 0f, -1f, 1f);
            string artId = ThemeLocale.ArtId(u.Spec.Id);
            v.Body.uvRect = uv;
            if (NeonArt.HasWeaponPart(artId))
            {
                // Component rig: body and weapon render as separate layers
                v.Body.texture   = NeonArt.UnitBody(artId, isPlayer);
                v.Weapon.texture = NeonArt.UnitWeapon(artId, isPlayer);
                v.Weapon.uvRect  = uv;
                v.Weapon.enabled = true;
            }
            else
            {
                v.Body.texture   = NeonArt.Unit(artId, isPlayer);
                v.Weapon.enabled = false;
            }

            // ── limb rig ─────────────────────────────────────────────────────
            // Same idea as the weapon layer above, extended to the limbs: each one is
            // baked once hanging straight down from its own joint on a full-size canvas,
            // so the layers stack with zero offset and the PIVOT alone places the joint.
            // Shins parent to thighs, so a knee bend composes with the hip swing.
            v.Rigged = NeonArt.HasLimbRig(artId);
            if (v.Rigged)
            {
                if (v.Limbs == null) v.Limbs = new RawImage[NeonArt.LimbSegCount];
                for (int li = 0; li < NeonArt.LimbSegCount; li++)
                {
                    var info = NeonArt.LimbInfo(artId, li);
                    if (v.Limbs[li] == null)
                    {
                        var lgo = new GameObject("limb" + li);
                        lgo.transform.SetParent(v.Rt, false);
                        var lr = lgo.AddComponent<RectTransform>();
                        lr.anchorMin = lr.anchorMax = new Vector2(0.5f, 0.5f);
                        v.Limbs[li] = lgo.AddComponent<RawImage>();
                        v.Limbs[li].raycastTarget = false;
                    }
                    var img = v.Limbs[li];
                    var rt2 = img.rectTransform;
                    // reparent shins onto their thigh so the knee composes with the hip
                    var parent = info.Parent >= 0 ? v.Limbs[info.Parent].rectTransform : v.Rt;
                    rt2.SetParent(parent, false);
                    rt2.sizeDelta = v.Rt.sizeDelta;
                    rt2.pivot     = info.Joint;
                    // a child's rect must sit exactly on top of the body rect: shifting by
                    // (pivot - centre) puts the full canvas back in register after the pivot move
                    rt2.anchorMin = rt2.anchorMax = new Vector2(0.5f, 0.5f);
                    rt2.anchoredPosition = info.Parent >= 0
                        ? new Vector2((info.Joint.x - NeonArt.LimbInfo(artId, info.Parent).Joint.x) * v.Rt.sizeDelta.x,
                                      (info.Joint.y - NeonArt.LimbInfo(artId, info.Parent).Joint.y) * v.Rt.sizeDelta.y)
                        : new Vector2((info.Joint.x - 0.5f) * v.Rt.sizeDelta.x,
                                      (info.Joint.y - 0.5f) * v.Rt.sizeDelta.y);
                    rt2.localRotation = Quaternion.identity;
                    img.texture = NeonArt.UnitLimb(artId, isPlayer, li);
                    img.uvRect  = uv;
                    img.color   = v.Body.color;
                    img.enabled = false;                 // off until the unit actually walks
                    // near limbs in front of the body, far limbs behind it
                    if (info.Parent < 0)
                        rt2.SetSiblingIndex(info.Near ? v.Rt.childCount - 1 : 0);
                }
            }
            else if (v.Limbs != null)
            {
                for (int li = 0; li < v.Limbs.Length; li++)
                    if (v.Limbs[li]) v.Limbs[li].enabled = false;
            }
            v.RigOn = false;
            v.Weapon.rectTransform.anchoredPosition = Vector2.zero;
            v.Weapon.rectTransform.localRotation    = Quaternion.identity;
            v.ArtId     = artId;
            v.PoseFrame = 0;
            // Enemy faction tint shift — red-violet hue to distinguish from player palette
            Color bodyColor = teamColor;
            if (!isPlayer) bodyColor *= new Color(1.0f, 0.82f, 0.92f, 1f);
            bodyColor = new Color(
                Mathf.Clamp01(bodyColor.r * (1f + hueVar)),
                bodyColor.g,
                Mathf.Clamp01(bodyColor.b * (1f - hueVar)), 1f);
            // Authored-art units colour themselves. Multiplying a medieval knight by the full
            // enemy tint (1.00, 0.42, 0.24) crushes green and blue on EVERY pixel -- steel, gold,
            // leather and stone all turn red -- which is what made the enemy knight a solid red
            // shape even after the texture switched to livery. Keep a light team bias only; the
            // cape, plume and heraldry in the texture carry the allegiance read, and the glow
            // halo below still uses the full team colour.
            if (NeonArt.IsAuthoredArt(artId))
                bodyColor = Color.Lerp(Color.white, bodyColor, 0.22f);
            v.Body.color    = bodyColor;
            if (v.Weapon.enabled) v.Weapon.color = bodyColor;
            v.BaseBody      = bodyColor;

            v.NextFidget = Time.time + Random.Range(3f, 6f);
            v.NextWound  = 0f;
            // C6 per-class voice register
            v.VoicePitch = u.Spec.Id switch
            {
                "titan" => 0.40f, "mech" => 0.55f, "shield-bot" => 0.65f, "turret" => 0.80f,
                "trooper" => 1.0f, "sniper" => 1.2f, "interceptor" => 1.5f, "hacker" => 1.65f,
                "drone" => 1.85f, _ => 1.0f,
            };
            v.Glow.texture  = NeonArt.Glow(teamColor);

            // Per-class glow bleed — heavier units project a wider halo
            float bleed = u.Spec.Id switch
            {
                "titan"                    => 16f,
                "mech"                     => 14f,
                "shield-bot"               => 13f,
                "turret"                   => 12f,
                "trooper" or "hacker"      => 11f,
                "drone" or "interceptor"   => 9f,
                _                          => 10f,
            };
            v.Glow.rectTransform.offsetMin = new Vector2(-bleed, -bleed);
            v.Glow.rectTransform.offsetMax = new Vector2( bleed,  bleed);

            // Per-class glow alpha — matches unit weight/presence
            float glowAlpha = u.Spec.Id switch
            {
                "titan"                    => 0.80f,
                "mech"                     => 0.70f,
                "shield-bot"               => 0.68f,
                "turret"                   => 0.65f,
                "trooper" or "hacker"      => 0.60f,
                "sniper"                   => 0.52f,
                "drone" or "interceptor"   => 0.48f,
                _                          => 0.55f,
            };
            v.Glow.color    = new Color(1f, 1f, 1f, glowAlpha);
            v.SpecId        = u.Spec.Id;
            v.Anim          = UAnim.March;
            v.BobW          = 1f;
            v.PrevHop       = 0f;
        }

        // ─────────────────────────────────────────────── coroutines ──────────

        IEnumerator SpawnDrop(UnitView v)
        {
            // C6/C7: power-on chirp in the unit's voice register as it drops in
            AudioManager.Play(AudioManager.Sfx.UnitChirp, 0.35f, v.VoicePitch);
            const float DUR = 0.35f;
            const float START_Y = 65f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) yield break;
                float ease = 1f - (1f - t / DUR) * (1f - t / DUR);
                v.SpawnYOffset = START_Y * (1f - ease);
                yield return null;
            }
            if (v.Dying || v.Rt == null) yield break;
            v.SpawnYOffset = 0f;
            // Landing squash: briefly wide and flat, then spring back
            v.ScaleAnim = true;
            v.Rt.localScale = new Vector3(1.12f, 0.88f, 1f);
            const float SQUASH_DUR = 0.14f;
            for (float t = 0f; t < SQUASH_DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.ScaleAnim = false; yield break; }
                float e = t / SQUASH_DUR;
                v.Rt.localScale = new Vector3(Mathf.Lerp(1.12f, 1f, e), Mathf.Lerp(0.88f, 1f, e), 1f);
                yield return null;
            }
            if (!v.Dying && v.Rt != null) v.Rt.localScale = Vector3.one;
            v.ScaleAnim = false;
        }

        // Drive the body texture straight to an attack frame. The rig draws a real arm action
        // (wind the arm back, then drive it through), so a strike is no longer just the whole
        // body sliding forward from the waist with a static pose.
        void SetAttackPose(UnitView v, int pose)
        {
            if (v == null || v.Body == null) return;
            if (!NeonArt.HasAttackPoses(v.ArtId)) return;
            if (v.AttackPose == pose) return;
            v.AttackPose = pose;
            int f = pose >= 0 ? pose : v.PoseFrame;
            v.Body.texture = NeonArt.HasWeaponPart(v.ArtId)
                ? NeonArt.UnitBody(v.ArtId, v.IsPlayer, f)
                : NeonArt.Unit(v.ArtId, v.IsPlayer, f);
        }

        // Shared pre-strike anticipation: crouch + pull back (+ optional lean-back).
        // Caller must have set Lunging. The snap out of this pose IS the strike.
        IEnumerator Anticipate(UnitView v, float squashX, float squashY, float pullPx, float leanDeg, float dur)
        {
            v.Anim = UAnim.Windup;
            SetAttackPose(v, 3);   // arm cocks back
            float dir = v.IsPlayer ? 1f : -1f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) yield break;
                float e = EaseOut(t / dur);
                v.LungeOffset   = -dir * pullPx * e;
                v.Rt.localScale = new Vector3(Mathf.Lerp(1f, squashX, e), Mathf.Lerp(1f, squashY, e), 1f);
                if (leanDeg != 0f)
                    v.Rt.localRotation = Quaternion.Euler(0f, 0f, dir * leanDeg * e);
                yield return null;
            }
            if (!v.Dying && v.Rt != null) v.Rt.localScale = Vector3.one;
            v.Anim = UAnim.Strike;
            SetAttackPose(v, 4);   // arm drives through
        }

        // Ground-contact dust for heavy walkers
        /// <summary>Walk phase order. 1 and 2 are the first step (near foot leads, then
        /// passes); 6 and 7 are the same two beats with the far foot leading. Numbered this
        /// way so 3/4/5 keep meaning windup / strike / flinch.</summary>
        static readonly int[] Walk4 = { 1, 2, 6, 7 };

        /// <summary>Rotates the limb layers from a continuous walk phase. The angles are the
        /// same curves the baked poses sampled -- a hip swing, a knee that folds only on the
        /// back half, and arms counter-swinging the legs -- but evaluated every frame instead
        /// of four times a stride, so the limb travels rather than snapping.</summary>
        void DriveLimbRig(UnitView v, bool on, float walkW)
        {
            if (v.Limbs == null) return;

            if (!on)
            {
                if (v.RigOn)
                {
                    for (int i = 0; i < v.Limbs.Length; i++)
                        if (v.Limbs[i]) v.Limbs[i].enabled = false;
                    v.RigOn = false;
                }
                return;
            }

            if (!v.RigOn)
            {
                for (int i = 0; i < v.Limbs.Length; i++)
                    if (v.Limbs[i]) { v.Limbs[i].enabled = true; v.Limbs[i].color = v.Body.color; }
                v.RigOn = true;
            }

            float t    = v.BobPhase / (Mathf.PI * 2f);
            float mirr = v.IsPlayer ? 1f : -1f;    // enemies march the other way
            float w    = Mathf.Clamp01(walkW);

            // COS, not sin, so the legs are at full spread when BobPhase is a multiple of PI --
            // which is exactly where the body bounce |sin(BobPhase)| is at its LOWEST. Feet apart
            // means the hips are closer to the ground; feet together means the body rides up. Using
            // sin here put the bounce exactly out of phase with the stride, so the unit rose as its
            // legs spread, which reads as floating.
            float a     = t * Mathf.PI * 2f;
            float hipN  =  Mathf.Cos(a) * 29f * w;
            float hipF  = -Mathf.Cos(a) * 29f * w;
            // the knee only folds while that leg is swinging FORWARD through the pass
            float kneeN = Mathf.Max(0f, -Mathf.Sin(a)) * 44f * w;
            float kneeF = Mathf.Max(0f,  Mathf.Sin(a)) * 44f * w;
            // arms hang and counter-swing their own side's leg. Never raised above the shoulder:
            // the segment is baked pointing straight down and swings at most 24 degrees either way.
            float armN  = -Mathf.Cos(a) * 24f * w;
            float armF  =  Mathf.Cos(a) * 24f * w;

            Set(0, hipF); Set(1, -kneeF); Set(2, hipN); Set(3, -kneeN); Set(4, armF); Set(5, armN);

            void Set(int i, float deg)
            {
                var img = v.Limbs[i];
                if (img == null) return;
                // +deg swings the limb forward: (0,-1) rotated CCW by +deg -> (sin, -cos), i.e. +x
                img.rectTransform.localRotation = Quaternion.Euler(0f, 0f, deg * mirr);
            }
        }

        /// <summary>One-shot flourish at the moment a troop is committed to a lane.
        /// Replaces the troop-skin coatings: those sat on the unit for the whole match and
        /// covered the theme art underneath, where this marks the instant the player acted
        /// and then gets out of the way.</summary>
        void PlayDeployFx(UnitView v)
        {
            var fx = NeonCosmetics.ActiveDeployFx;
            if (fx == NeonCosmetics.DeployFx.None) return;
            // Two failed attempts are worth recording. Firing into the LANE on the spawn frame
            // put the effect in the wrong place, because several of the unit's values are still
            // settling that frame. Making it a child of the UNIT fixed the placement but made
            // it invisible: children inherit the unit's localScale, which runs up to ~3.2x, so
            // every element ballooned far past the screen.
            // So: lane parent (no inherited scale) and a one-frame wait, after which the unit's
            // position is final and can simply be read.
            StartCoroutine(DeployFxNextFrame(v, fx));
        }

        IEnumerator DeployFxNextFrame(UnitView v, NeonCosmetics.DeployFx fx)
        {
            yield return null;
            if (v == null || v.Rt == null) yield break;
            var parent = v.Rt.parent as RectTransform;
            if (parent == null) yield break;
            Vector2 at = v.Rt.anchoredPosition;

            // Every offset below is a multiple of h, and the largest is the drop pod's 2.2h.
            // At a real unit height (120-166 units) that put the pod 264-365 units above the
            // troop while a ground lane is only ~498 tall -- so it spawned PAST the lane edge
            // and played in the lane above. Capping h against the lane's own half-height keeps
            // all four effects inside the bay they belong to, and scales them together.
            // rect.height can still be 0 here if the lane's layout has not resolved. Without
            // a guard that made budget 0, h 0, and every element zero-sized -- which is exactly
            // how the animation "disappeared" after the last fix. Fall back to the unit's own
            // height, which is always valid, so a bad layout costs clamping rather than the
            // whole effect.
            float laneH  = parent.rect.height > 1f ? parent.rect.height
                                                   : v.Rt.sizeDelta.y * 4f;
            float budget = laneH * 0.44f;                 // furthest any element may travel
            float h      = Mathf.Min(v.Rt.sizeDelta.y, budget / 2.2f);
            switch (fx)
            {
                case NeonCosmetics.DeployFx.Lightning: StartCoroutine(FxLightning(parent, at, h)); break;
                case NeonCosmetics.DeployFx.DropPod:   StartCoroutine(FxDropPod  (parent, at, h)); break;
                case NeonCosmetics.DeployFx.PhaseIn:   StartCoroutine(FxPhaseIn  (parent, at, h, v)); break;
                case NeonCosmetics.DeployFx.Banner:    StartCoroutine(FxBanner   (parent, at, h)); break;
            }
        }

        RectTransform FxQuad(RectTransform parent, Vector2 at, Vector2 size, Color col, out Image img)
        {
            var go = new GameObject("dfx");
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);   // lane space, same as the unit
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = size;
            img = go.AddComponent<Image>();
            img.sprite = NeonArt.SoftDot();   // was an untextured rect -- a hard block of colour
            img.color = col;
            img.raycastTarget = false;
            return rt;
        }

        IEnumerator FxLightning(RectTransform parent, Vector2 at, float h)
        {
            var c = new Color(0.72f, 0.88f, 1f, 1f);
            // the bolt: a stack of short segments jittered off the vertical
            const int SEG = 7;
            var rts = new RectTransform[SEG];
            for (int i = 0; i < SEG; i++)
            {
                float y  = at.y - h * 0.4f + (h * 1.1f) * i / SEG;
                float dx = Mathf.Sin(i * 2.1f + 1f) * (7f - i * 0.7f);
                rts[i] = FxQuad(parent, new Vector2(at.x + dx, y), new Vector2(3.2f, h * 1.15f / SEG), c, out _);
            }
            var ring = FxQuad(parent, new Vector2(at.x, at.y - h * 0.42f), new Vector2(6f, 3f),
                              new Color(c.r, c.g, c.b, 0.8f), out var ringImg);
            for (float t = 0f; t < 0.42f; t += Time.deltaTime)
            {
                float k = t / 0.42f;
                for (int i = 0; i < SEG; i++)
                {
                    if (rts[i] == null) continue;
                    var im = rts[i].GetComponent<Image>();
                    if (im) im.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, 1f - k * 2.4f));
                }
                if (ring != null)
                {
                    ring.sizeDelta = new Vector2(6f + k * 90f, 3f + k * 26f);
                    ringImg.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, 0.8f - k));
                }
                yield return null;
            }
            for (int i = 0; i < SEG; i++) if (rts[i]) Destroy(rts[i].gameObject);
            if (ring) Destroy(ring.gameObject);
        }

        IEnumerator FxDropPod(RectTransform parent, Vector2 at, float h)
        {
            var shell = new Color(0.52f, 0.42f, 0.34f, 1f);
            var hot   = new Color(1f, 0.78f, 0.42f, 0.85f);
            var pod   = FxQuad(parent, new Vector2(at.x, at.y + h * 2.2f),
                               new Vector2(h * 0.42f, h * 0.55f), shell, out var podImg);
            for (float t = 0f; t < 0.24f; t += Time.deltaTime)     // fall
            {
                if (pod == null) yield break;
                pod.anchoredPosition = Vector2.Lerp(new Vector2(at.x, at.y + h * 2.2f), at, t / 0.24f);
                yield return null;
            }
            if (pod) Destroy(pod.gameObject);
            var lRt = FxQuad(parent, at, new Vector2(h * 0.3f, h * 0.5f), shell, out var lImg);
            var rRt = FxQuad(parent, at, new Vector2(h * 0.3f, h * 0.5f), shell, out var rImg);
            var flash = FxQuad(parent, new Vector2(at.x, at.y - h * 0.38f), new Vector2(h, 8f), hot, out var fImg);
            for (float t = 0f; t < 0.36f; t += Time.deltaTime)      // shell splits
            {
                float k = t / 0.36f;
                if (lRt) { lRt.anchoredPosition = at + new Vector2(-k * h * 0.9f, 0f);
                           lRt.localRotation = Quaternion.Euler(0, 0, k * 40f);
                           lImg.color = new Color(shell.r, shell.g, shell.b, 1f - k); }
                if (rRt) { rRt.anchoredPosition = at + new Vector2(k * h * 0.9f, 0f);
                           rRt.localRotation = Quaternion.Euler(0, 0, -k * 40f);
                           rImg.color = new Color(shell.r, shell.g, shell.b, 1f - k); }
                if (flash) { flash.sizeDelta = new Vector2(h + k * h * 1.6f, 8f);
                             fImg.color = new Color(hot.r, hot.g, hot.b, 0.85f * (1f - k)); }
                yield return null;
            }
            if (lRt) Destroy(lRt.gameObject);
            if (rRt) Destroy(rRt.gameObject);
            if (flash) Destroy(flash.gameObject);
        }

        IEnumerator FxPhaseIn(RectTransform parent, Vector2 at, float h, UnitView v)
        {
            var c = new Color(0.56f, 0.94f, 0.86f, 1f);
            const int LINES = 6;
            var rts = new RectTransform[LINES];
            var ims = new Image[LINES];
            for (int i = 0; i < LINES; i++)
                rts[i] = FxQuad(parent, new Vector2(at.x, at.y - h * 0.4f + h * i / LINES),
                                new Vector2(h * 0.7f, 2.2f), c, out ims[i]);
            if (v.Body) v.Body.color = new Color(v.Body.color.r, v.Body.color.g, v.Body.color.b, 0.2f);
            for (float t = 0f; t < 0.34f; t += Time.deltaTime)
            {
                float k = t / 0.34f;
                for (int i = 0; i < LINES; i++)
                {
                    float ph = Mathf.Clamp01(k * 1.9f - i * 0.1f);
                    if (ims[i]) ims[i].color = new Color(c.r, c.g, c.b, 1f - ph);
                }
                if (v.Body) v.Body.color = new Color(v.Body.color.r, v.Body.color.g, v.Body.color.b,
                                                     Mathf.Lerp(0.2f, 1f, k));
                yield return null;
            }
            if (v.Body) v.Body.color = new Color(v.Body.color.r, v.Body.color.g, v.Body.color.b, 1f);
            for (int i = 0; i < LINES; i++) if (rts[i]) Destroy(rts[i].gameObject);
        }

        IEnumerator FxBanner(RectTransform parent, Vector2 at, float h)
        {
            var pole  = new Color(0.60f, 0.50f, 0.36f, 1f);
            var cloth = new Color(0.78f, 0.24f, 0.18f, 1f);
            Vector2 baseAt = at + new Vector2(-h * 0.42f, 0f);
            var pRt = FxQuad(parent, baseAt + new Vector2(0f, h * 0.2f), new Vector2(3f, h * 0.8f), pole, out var pImg);
            var cRt = FxQuad(parent, baseAt + new Vector2(h * 0.2f, h * 0.46f), new Vector2(0f, h * 0.26f), cloth, out var cImg);
            var ring = FxQuad(parent, baseAt + new Vector2(0f, -h * 0.4f), new Vector2(6f, 3f),
                              new Color(1f, 0.86f, 0.62f, 0.8f), out var rImg);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                float k = Mathf.Clamp01(t / 0.5f);
                if (cRt) cRt.sizeDelta = new Vector2(Mathf.Lerp(0f, h * 0.42f, Mathf.Clamp01(k * 2.2f)), h * 0.26f);
                if (ring)
                {
                    ring.sizeDelta = new Vector2(6f + k * 78f, 3f + k * 22f);
                    rImg.color = new Color(1f, 0.86f, 0.62f, Mathf.Max(0f, 0.8f - k * 1.1f));
                }
                if (k > 0.65f)
                {
                    float f = (k - 0.65f) / 0.35f;
                    if (pImg) pImg.color = new Color(pole.r, pole.g, pole.b, 1f - f);
                    if (cImg) cImg.color = new Color(cloth.r, cloth.g, cloth.b, 1f - f);
                }
                yield return null;
            }
            if (pRt) Destroy(pRt.gameObject);
            if (cRt) Destroy(cRt.gameObject);
            if (ring) Destroy(ring.gameObject);
        }

        /// <summary>The lit plate a troop materialises on. Purely cosmetic, but it turns the
        /// deploy effect from "something flashed near the edge" into "the troop arrived HERE".</summary>
        /// <summary>VFX skins. The old SkinGlow scaled an aura up BEHIND the unit, which washed
        /// over the silhouette and fought whichever theme's art was underneath. These are small
        /// elements placed AROUND the troop -- motes at hip height, arcs at the feet, a wake, a
        /// ring on the floor -- so the unit and its theme stay readable.</summary>
        void DriveVfxSkin(UnitView v)
        {
            var skin = NeonCosmetics.ActiveVfxSkin;
            if (skin == NeonCosmetics.VfxSkin.None)
            {
                if (v.SkinFx != null)
                    for (int i = 0; i < v.SkinFx.Length; i++)
                        if (v.SkinFx[i]) v.SkinFx[i].enabled = false;
                return;
            }

            const int N = 4;
            if (v.SkinFx == null)
            {
                v.SkinFx = new Image[N];
                for (int i = 0; i < N; i++)
                {
                    var go = new GameObject("skinfx");
                    // Sibling of the unit, not a child: children inherit the unit's localScale
                    // (up to ~3.2x) and every element would balloon off-screen.
                    go.transform.SetParent(v.Rt.parent, false);
                    var rt = go.AddComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    var im = go.AddComponent<Image>();
                    im.sprite = NeonArt.SoftDot();
                    im.raycastTarget = false;
                    v.SkinFx[i] = im;
                }
            }

            v.SkinFxPhase += Time.deltaTime;
            float t  = v.SkinFxPhase;
            float sc = v.UScale <= 0f ? 1f : v.UScale;
            Vector2 at = v.Rt.anchoredPosition;            // lane space
            float   hh = v.Rt.sizeDelta.y * 0.5f;
            Color   c  = SkinFxColor(skin);

            for (int i = 0; i < N; i++)
            {
                var im = v.SkinFx[i];
                if (im == null) continue;
                var rt = im.rectTransform;
                if (rt.parent != v.Rt.parent) rt.SetParent(v.Rt.parent, false);
                im.enabled = true;
                float f = i / (float)N;

                switch (skin)
                {
                    case NeonCosmetics.VfxSkin.EmberOrbit:
                    {
                        if (i >= 3) { im.enabled = false; break; }
                        float a = t * 2.2f + i * 2.094f;
                        bool front = Mathf.Sin(a) > 0f;
                        rt.anchoredPosition = at + new Vector2(Mathf.Cos(a) * 15f * sc,
                                                               Mathf.Sin(a) * 4f * sc);
                        rt.sizeDelta = Vector2.one * (front ? 7f : 4.5f) * sc;
                        im.color = new Color(c.r, c.g, c.b, front ? 0.95f : 0.40f);
                        break;
                    }
                    case NeonCosmetics.VfxSkin.StaticArc:
                    {
                        bool on = Mathf.Repeat(t, 0.55f) < 0.16f;
                        rt.anchoredPosition = at + new Vector2((-6f + i * 4f) * sc, -hh * 0.86f);
                        rt.sizeDelta = new Vector2(1.8f, 8f) * sc;
                        im.color = new Color(c.r, c.g, c.b, on ? 0.85f : 0f);
                        break;
                    }
                    case NeonCosmetics.VfxSkin.FrostTrail:
                    {
                        float age = Mathf.Repeat(t * 1.2f + f, 1f);
                        float dir = v.IsPlayer ? -1f : 1f;      // trails behind, whichever way it faces
                        rt.anchoredPosition = at + new Vector2(dir * (8f + age * 26f) * sc, -hh * 0.86f);
                        rt.sizeDelta = Vector2.one * Mathf.Lerp(6f, 2f, age) * sc;
                        im.color = new Color(c.r, c.g, c.b, (1f - age) * 0.9f);
                        break;
                    }
                    default:   // HaloRing
                    {
                        float a = t * 1.6f + f * Mathf.PI * 2f;
                        rt.anchoredPosition = at + new Vector2(Mathf.Cos(a) * 17f * sc,
                                                               -hh * 0.84f + Mathf.Sin(a) * 4f * sc);
                        rt.sizeDelta = Vector2.one * 4.2f * sc;
                        im.color = new Color(c.r, c.g, c.b, 0.85f);
                        break;
                    }
                }
            }
        }

        static Color SkinFxColor(NeonCosmetics.VfxSkin s) => s switch
        {
            NeonCosmetics.VfxSkin.EmberOrbit => new Color(1.00f, 0.60f, 0.25f),
            NeonCosmetics.VfxSkin.StaticArc  => new Color(0.66f, 0.88f, 1.00f),
            NeonCosmetics.VfxSkin.FrostTrail => new Color(0.62f, 0.91f, 1.00f),
            _                                => NeonTheme.Active.Accent,
        };

        void BuildDeployPad(RectTransform lane, int laneIdx, bool player)
        {
            var t = NeonTheme.Active;
            float nx = (player ? CombatSim.DeploySpawnX
                               : CombatSim.LaneLength - CombatSim.DeploySpawnX)
                       / CombatSim.LaneLength;
            Color c = player ? t.TroopTint : t.EnemyTint;

            var go = new GameObject(player ? $"pad_p{laneIdx}" : $"pad_e{laneIdx}");
            go.transform.SetParent(lane, false);
            go.transform.SetAsFirstSibling();          // under the units, over the lane floor
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(nx, 0f); rt.anchorMax = new Vector2(nx, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 2f);
            rt.sizeDelta = new Vector2(34f, 7f);
            var img = go.AddComponent<Image>();
            img.color = new Color(c.r, c.g, c.b, 0.30f);
            img.raycastTarget = false;

            // two ticks either side, so the pad reads as a marked bay rather than a smear
            for (int i = 0; i < 2; i++)
            {
                var tick = new GameObject("tick");
                tick.transform.SetParent(go.transform, false);
                var trt = tick.AddComponent<RectTransform>();
                trt.anchorMin = trt.anchorMax = new Vector2(i == 0 ? 0f : 1f, 0.5f);
                trt.pivot = new Vector2(0.5f, 0.5f);
                trt.sizeDelta = new Vector2(2.5f, 13f);
                var ti = tick.AddComponent<Image>();
                ti.color = new Color(c.r, c.g, c.b, 0.55f);
                ti.raycastTarget = false;
            }
        }

        void SpawnStompDust(UnitView v)
        {
            var parent = v.Rt.parent as RectTransform;
            if (parent == null) return;
            Vector2 feet = v.Rt.anchoredPosition + new Vector2(0f, -v.Rt.sizeDelta.y * 0.4f);
            StartCoroutine(DustPuff(parent, feet));
        }

        IEnumerator DustPuff(RectTransform parent, Vector2 at, Color? tint = null)
        {
            Color baseCol = tint ?? new Color(0.75f, 0.78f, 0.85f, 0.35f);
            const int N = 3;
            var rts  = new RectTransform[N];
            var imgs = new Image[N];
            var vel  = new Vector2[N];
            for (int i = 0; i < N; i++)
            {
                var go = new GameObject("dust");
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = at + new Vector2(Random.Range(-6f, 6f), 0f);
                float sz = Random.Range(4f, 7f);
                rt.sizeDelta = new Vector2(sz, sz);
                var img = go.AddComponent<Image>();
                img.sprite = NeonArt.SparkDot();
                img.color = baseCol;
                img.raycastTarget = false;
                vel[i] = new Vector2(Random.Range(-30f, 30f), Random.Range(8f, 22f));
                rts[i] = rt; imgs[i] = img;
            }
            const float DUR = 0.30f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                float a = baseCol.a * (1f - t / DUR);
                for (int i = 0; i < N; i++)
                {
                    if (rts[i] == null) continue;
                    rts[i].anchoredPosition += vel[i] * Time.deltaTime;
                    vel[i] *= 1f - Time.deltaTime * 5f;
                    imgs[i].color = new Color(baseCol.r, baseCol.g, baseCol.b, a);
                }
                yield return null;
            }
            for (int i = 0; i < N; i++)
                if (rts[i] != null) Destroy(rts[i].gameObject);
        }

        // C3: heavies flex — chest swell owned via ScaleAnim
        IEnumerator ChestPulse(UnitView v)
        {
            if (v.ScaleAnim || v.Lunging || v.Dying) yield break;
            v.ScaleAnim = true;
            const float DUR = 0.35f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Lunging || v.Rt == null) break;
                float p = Mathf.Sin(t / DUR * Mathf.PI);
                v.Rt.localScale = new Vector3(1f + 0.025f * p, 1f + 0.025f * p, 1f);
                yield return null;
            }
            if (!v.Dying && v.Rt != null && !v.Lunging) v.Rt.localScale = Vector3.one;
            v.ScaleAnim = false;
        }

        // Ambient weight-shift for light troops that have no dedicated fidget — a brief
        // settle (squash down + slight widen) with a tiny dip, owned via ScaleAnim so the
        // per-frame scale write yields to it. Keeps a stalled frontline from looking frozen.
        IEnumerator IdleShift(UnitView v)
        {
            if (v.ScaleAnim || v.Lunging || v.Dying || v.Rt == null) yield break;
            v.ScaleAnim = true;
            const float DUR = 0.5f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Lunging || v.Rt == null) break;
                float p = Mathf.Sin(t / DUR * Mathf.PI);          // ease-in-out bump
                v.Rt.localScale = new Vector3(1f + 0.02f * p, 1f - 0.025f * p, 1f);
                v.SpawnYOffset  = -0.8f * p;                       // settle down onto the feet
                yield return null;
            }
            if (!v.Dying && v.Rt != null)
            {
                if (!v.Lunging) v.Rt.localScale = Vector3.one;
                v.SpawnYOffset = 0f;
            }
            v.ScaleAnim = false;
        }

        // C3: sniper lens glint — 1-frame white flash at the scope
        IEnumerator ScopeGlint(UnitView v)
        {
            if (v.Lunging || v.AttackFlash == null) yield break;
            float sx = v.IsPlayer ? 16f : -16f;
            v.AttackFlash.rectTransform.anchoredPosition = new Vector2(sx, -2f) * v.UScale;
            v.AttackFlash.rectTransform.sizeDelta = new Vector2(5f, 5f) * v.UScale;
            v.AttackFlash.color = new Color(1f, 1f, 1f, 0.9f);
            yield return null;
            yield return null;
            if (v.AttackFlash != null && !v.Lunging) v.AttackFlash.color = Color.clear;
        }

        // C4: killer's victory hop when its target goes down
        IEnumerator KillHop(UnitView v)
        {
            if (v.Lunging || v.ScaleAnim || v.Dying || v.Rt == null) yield break;
            v.ScaleAnim = true;
            AudioManager.Play(AudioManager.Sfx.UnitChirp, 0.30f, v.VoicePitch * 1.15f);
            const float DUR = 0.24f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Lunging || v.Rt == null) break;
                float p = Mathf.Sin(t / DUR * Mathf.PI);
                v.Rt.localScale = new Vector3(1f + 0.05f * p, 1f + 0.06f * p, 1f);
                v.SpawnYOffset  = 2.5f * p;
                yield return null;
            }
            if (!v.Dying && v.Rt != null) { if (!v.Lunging) v.Rt.localScale = Vector3.one; v.SpawnYOffset = 0f; }
            v.ScaleAnim = false;
        }

        // Victim knockback the frame a hit lands — pushed back toward its own side
        IEnumerator StaggerKick(UnitView v)
        {
            if (v.Lunging || v.Dying || v.Rt == null) yield break;
            v.Anim = UAnim.Stagger;
            float dir = v.IsPlayer ? -1f : 1f;
            const float DUR = 0.12f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Lunging || v.Dying || v.Rt == null) { v.Anim = UAnim.March; yield break; }
                v.LungeOffset = dir * 3f * (1f - t / DUR);
                yield return null;
            }
            if (!v.Lunging) v.LungeOffset = 0f;
            v.Anim = UAnim.March;
        }

        // ── synced hit reaction (P1: 1-on-1 coordination) ───────────────────────
        // The defender's flinch, knockback and spark, fired together on the attacker's
        // CONTACT frame. Melee routes here after ~windup+drive; ranged after the shot lands.
        void DoDefenderReaction(UnitView def, UnitView atk, bool counter, bool midpoint)
        {
            if (Time.time - _lastUnitHitTime >= 0.08f)
            {
                AudioManager.Play(AudioManager.Sfx.UnitHit);
                _lastUnitHitTime = Time.time;
            }
            if (def == null || def.Rt == null || def.Dying) return;
            Vector2 at = def.Rt.anchoredPosition;
            if (midpoint && atk != null && atk.Rt != null && atk.Rt.parent == def.Rt.parent)
                at = (atk.Rt.anchoredPosition + def.Rt.anchoredPosition) * 0.5f;
            StartCoroutine(HitFlashCoroutine(def));
            StartCoroutine(KnockBack(def, atk, counter ? 5f : 3.5f));
            StartCoroutine(SparkBurst(def, at));
        }

        IEnumerator DelayedDefenderReaction(UnitView def, UnitView atk, float delay, bool counter, bool midpoint)
        {
            yield return new WaitForSeconds(delay);
            DoDefenderReaction(def, atk, counter, midpoint);
        }

        // Victim shoved directly AWAY from the attacker on contact — an instant push that
        // decays, so momentum visibly transfers from striker to struck.
        IEnumerator KnockBack(UnitView v, UnitView from, float amt)
        {
            if (v == null || v.Rt == null || v.Lunging || v.Dying) yield break;
            v.Anim = UAnim.Stagger;
            // Taking damage is its own action, not just a shove: the body braces back onto the
            // rear foot with both arms thrown up. Held through the knockback, then released.
            bool flinch = NeonArt.HasAttackPoses(v.ArtId) && v.AttackPose < 0;
            if (flinch) SetAttackPose(v, 5);
            float push = v.IsPlayer ? -1f : 1f;
            if (from != null && from.Rt != null && from.Rt.parent == v.Rt.parent)
            {
                float d = v.Rt.anchoredPosition.x - from.Rt.anchoredPosition.x;
                if (Mathf.Abs(d) > 0.01f) push = Mathf.Sign(d);
            }
            // Hitstop: two frames frozen at full displacement give the blow weight before the
            // knockback rides out. Without it a hit reads as a slide with no moment of contact.
            v.LungeOffset = push * amt;
            yield return null;
            yield return null;
            const float DUR = 0.14f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Lunging || v.Dying || v.Rt == null)
                {
                    if (flinch && v.AttackPose == 5) SetAttackPose(v, -1);
                    v.Anim = UAnim.March; yield break;
                }
                v.LungeOffset = push * amt * (1f - t / DUR);
                yield return null;
            }
            if (!v.Lunging) v.LungeOffset = 0f;
            if (flinch && v.AttackPose == 5) SetAttackPose(v, -1);
            v.Anim = UAnim.March;
        }

        IEnumerator AttackLunge(UnitView v)
        {
            if (v.Lunging) yield break;
            v.Lunging = true;
            yield return Anticipate(v, 1.03f, 0.97f, 3f, 2f, 0.06f);
            const float DUR = 0.22f;
            const float AMT = 5f;   // the arm pose carries the strike; this is follow-through only
            float dir = v.IsPlayer ? 1f : -1f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                float k = Strike(t / DUR);
                v.LungeOffset      = dir * AMT * k;
                v.Rt.localRotation = Quaternion.Euler(0f, 0f, -dir * 3f * k); // lean into the strike
                yield return null;
            }
            if (!v.Dying && v.Rt != null)
            {
                v.LungeOffset      = 0f;
                v.Rt.localRotation = Quaternion.identity;
            }
            v.Lunging = false;
        }

        // Trooper: quick double-jab — first punch then a heavier follow-through
        IEnumerator AnimTrooperPunch(UnitView v)
        {
            if (v.Lunging) yield break;
            v.Lunging = true;
            yield return Anticipate(v, 1.03f, 0.96f, 3f, 2f, 0.06f);
            float dir = v.IsPlayer ? 1f : -1f;
            for (float t = 0f; t < 0.10f; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                v.LungeOffset = dir * 3.5f * Strike(t / 0.10f); // jab — arm does the reaching
                yield return null;
            }
            if (!v.Dying && v.Rt != null) v.LungeOffset = 0f;
            yield return new WaitForSeconds(0.05f);
            for (float t = 0f; t < 0.14f; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                float k = Strike(t / 0.14f);
                v.LungeOffset      = dir * 5f * k;   // follow-through
                v.Rt.localRotation = Quaternion.Euler(0f, 0f, -dir * 3f * k); // heavy follow-through lean
                yield return null;
            }
            if (!v.Dying && v.Rt != null)
            {
                v.LungeOffset      = 0f;
                v.Rt.localRotation = Quaternion.identity;
            }
            v.Lunging = false;
        }

        IEnumerator ProjectileShot(Vector2 from, Vector2 to, RectTransform laneParent, Color col, Vector2 size = default)
        {
            if (size == Vector2.zero) size = new Vector2(10f, 3f);

            Vector2 delta = to - from;
            float dist = delta.magnitude;
            float ang  = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            // Point-blank: attacker and target are on top of each other — there's no room for a
            // readable tracer, so a flying bullet just flickers as a stray blip. Resolve it as a
            // plain impact instead.
            if (dist < 22f) { SpawnProjectileImpact(laneParent, to, col); yield break; }

            var pgo = new GameObject("proj");
            pgo.transform.SetParent(laneParent, false);
            var prt = pgo.AddComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.anchoredPosition = from;
            prt.sizeDelta = size;
            var pimg = pgo.AddComponent<Image>();
            pimg.color = new Color(col.r * 0.75f, col.g * 0.75f, col.b * 0.75f, col.a);
            pimg.raycastTarget = false;

            var tipGo = new GameObject("t"); tipGo.transform.SetParent(pgo.transform, false);
            var tipRt = tipGo.AddComponent<RectTransform>();
            tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 0.5f);
            tipRt.pivot = new Vector2(0.5f, 0.5f);
            tipRt.anchoredPosition = new Vector2(size.x * 0.5f, 0f);
            tipRt.sizeDelta = new Vector2(size.y + 2f, size.y + 2f);
            // Bright team-tinted core (not pure white) — keeps the tracer readable while staying
            // on-palette, so shots don't read as colorless flickers.
            var tipImg2 = tipGo.AddComponent<Image>();
            tipImg2.sprite = NeonArt.SparkDot();
            tipImg2.color = new Color(Mathf.Min(col.r * 1.5f + 0.25f, 1f),
                                      Mathf.Min(col.g * 1.5f + 0.25f, 1f),
                                      Mathf.Min(col.b * 1.5f + 0.25f, 1f), 1f);
            tipImg2.raycastTarget = false;

            prt.localRotation = Quaternion.Euler(0, 0, ang);

            float spd  = Mathf.Max(700f, dist * 5f);
            float dur  = Mathf.Clamp(dist / spd, 0.05f, 0.22f);

            float trailClock = 0f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (prt == null) yield break;
                prt.anchoredPosition = Vector2.Lerp(from, to, t / dur);
                // Glow trail: drop a fading ghost every few ms behind the bullet
                trailClock += Time.deltaTime;
                if (trailClock >= 0.018f)
                {
                    trailClock = 0f;
                    StartCoroutine(TrailGhost(laneParent, prt.anchoredPosition, pimg.color, size.y + 1f, ang));
                }
                yield return null;
            }
            if (pgo != null) Destroy(pgo);

            SpawnProjectileImpact(laneParent, to, col);
        }

        // Impact burst: 4 sparks scatter from the strike point. Shared by the normal
        // projectile end and the point-blank early-out in ProjectileShot.
        void SpawnProjectileImpact(RectTransform laneParent, Vector2 to, Color col)
        {
            for (int i = 0; i < 4; i++)
            {
                var sgo = new GameObject("pimp");
                sgo.transform.SetParent(laneParent, false);
                var srt = sgo.AddComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = to;
                srt.sizeDelta = new Vector2(4f, 4f);
                var simg = sgo.AddComponent<Image>();
                simg.sprite = NeonArt.SparkDot();
                simg.color = new Color(Mathf.Min(col.r * 1.4f, 1f), Mathf.Min(col.g * 1.4f, 1f), Mathf.Min(col.b * 1.4f, 1f));
                simg.raycastTarget = false;
                StartCoroutine(ImpactSpark(srt, simg,
                    new Vector2(Random.Range(-70f, 70f), Random.Range(-55f, 75f))));
            }
        }

        // ── battlefield-space projectile overlay (P0) ────────────────────────────
        // One full-field layer above every lane. Cross-lane fire (interceptor→ground,
        // turret→air, or any shot touching the air lane) is drawn here in a single shared
        // coordinate frame, so a tracer can span the vertical gap between lane bands instead
        // of sliding flat inside the attacker's own lane and dying in mid-air.
        void BuildFieldOverlay()
        {
            var go = new GameObject("ProjectileOverlay");
            go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.pivot     = new Vector2(0.5f, 0.5f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            rt.SetAsLastSibling();           // draw over lanes + units
            _fieldOverlay = rt;
        }

        // Live world position of a unit → overlay-local coordinates. World-space round-trip
        // is scale/anchor safe regardless of which lane the unit currently sits in.
        Vector2 FieldPoint(RectTransform unitRt)
        {
            if (_fieldOverlay == null || unitRt == null) return Vector2.zero;
            Vector3 local = _fieldOverlay.InverseTransformPoint(unitRt.position);
            return new Vector2(local.x, local.y);
        }

        // Overlay tracer between two field points. `dive` adds a mild ballistic sag so the
        // interceptor's shot reads as a strafing dive rather than a flat laser.
        IEnumerator FieldTracer(Vector2 from, Vector2 to, Color col, Vector2 size, bool dive = false)
        {
            if (_fieldOverlay == null) yield break;
            if (size == Vector2.zero) size = new Vector2(11f, 3f);

            Vector2 delta = to - from;
            float dist = delta.magnitude;
            if (dist < 6f) { SpawnFieldImpact(to, col); yield break; }
            float sag = dive ? Mathf.Min(dist * 0.16f, 34f) : 0f;

            var pgo = new GameObject("fproj");
            pgo.transform.SetParent(_fieldOverlay, false);
            var prt = pgo.AddComponent<RectTransform>();
            prt.anchorMin = prt.anchorMax = new Vector2(0.5f, 0.5f);
            prt.pivot = new Vector2(0.5f, 0.5f);
            prt.sizeDelta = size;
            prt.anchoredPosition = from;
            var pimg = pgo.AddComponent<Image>();
            pimg.color = new Color(col.r * 0.8f, col.g * 0.8f, col.b * 0.8f, col.a);
            pimg.raycastTarget = false;

            var tipGo = new GameObject("t"); tipGo.transform.SetParent(pgo.transform, false);
            var tipRt = tipGo.AddComponent<RectTransform>();
            tipRt.anchorMin = tipRt.anchorMax = new Vector2(0.5f, 0.5f);
            tipRt.pivot = new Vector2(0.5f, 0.5f);
            tipRt.anchoredPosition = new Vector2(size.x * 0.5f, 0f);
            tipRt.sizeDelta = new Vector2(size.y + 2f, size.y + 2f);
            var tipImg = tipGo.AddComponent<Image>();
            tipImg.sprite = NeonArt.SparkDot();
            tipImg.color = new Color(Mathf.Min(col.r * 1.5f + 0.25f, 1f),
                                     Mathf.Min(col.g * 1.5f + 0.25f, 1f),
                                     Mathf.Min(col.b * 1.5f + 0.25f, 1f), 1f);
            tipImg.raycastTarget = false;

            float spd = Mathf.Max(760f, dist * 5f);
            float dur = Mathf.Clamp(dist / spd, 0.06f, 0.26f);
            float trailClock = 0f;
            Vector2 prev = from;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (prt == null) yield break;
                float p = t / dur;
                Vector2 pos = Vector2.Lerp(from, to, p);
                pos.y -= Mathf.Sin(p * Mathf.PI) * sag;          // belly of the dive
                prt.anchoredPosition = pos;
                Vector2 tang = pos - prev; prev = pos;
                if (tang.sqrMagnitude > 0.01f)
                    prt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(tang.y, tang.x) * Mathf.Rad2Deg);
                trailClock += Time.deltaTime;
                if (trailClock >= 0.018f)
                {
                    trailClock = 0f;
                    StartCoroutine(FieldTrailGhost(pos, pimg.color, size.y + 1f,
                        prt.localRotation.eulerAngles.z));
                }
                yield return null;
            }
            if (pgo != null) Destroy(pgo);
            SpawnFieldImpact(to, col);
        }

        // Overlay-anchored fading trail segment (mirrors TrailGhost in field space).
        IEnumerator FieldTrailGhost(Vector2 at, Color col, float size, float angDeg)
        {
            if (_fieldOverlay == null) yield break;
            var go = new GameObject("ftrail");
            go.transform.SetParent(_fieldOverlay, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(size * 2.2f, size * 0.8f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angDeg);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            const float DUR = 0.13f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float a = 0.45f * (1f - t / DUR);
                img.color = new Color(col.r, col.g, col.b, a);
                rt.localScale = new Vector3(1f - t / DUR * 0.5f, 1f - t / DUR * 0.5f, 1f);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // Overlay-anchored impact burst (mirrors SpawnProjectileImpact in field space).
        void SpawnFieldImpact(Vector2 at, Color col)
        {
            if (_fieldOverlay == null) return;
            for (int i = 0; i < 4; i++)
            {
                var sgo = new GameObject("fimp");
                sgo.transform.SetParent(_fieldOverlay, false);
                var srt = sgo.AddComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = new Vector2(0.5f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = at;
                srt.sizeDelta = new Vector2(4f, 4f);
                var simg = sgo.AddComponent<Image>();
                simg.sprite = NeonArt.SparkDot();
                simg.color = new Color(Mathf.Min(col.r * 1.4f, 1f), Mathf.Min(col.g * 1.4f, 1f), Mathf.Min(col.b * 1.4f, 1f));
                simg.raycastTarget = false;
                StartCoroutine(ImpactSpark(srt, simg,
                    new Vector2(Random.Range(-70f, 70f), Random.Range(-55f, 75f))));
            }
        }

        /// <summary>
        /// Floating damage number on a core. Core damage arrives as many small ticks, so the
        /// amounts are POOLED and flushed a few times a second -- otherwise the screen fills
        /// with "-1" spam. Own core counts down in red, enemy core in the player accent.
        /// </summary>
        void SpawnCoreDamage(bool ownCore, float amount)
        {
            if (amount <= 0f) return;
            if (ownCore) _pCoreAccum += amount; else _eCoreAccum += amount;

            float next = ownCore ? _pCoreNext : _eCoreNext;
            if (Time.time < next) return;
            if (ownCore) _pCoreNext = Time.time + 0.35f; else _eCoreNext = Time.time + 0.35f;

            float pooled = ownCore ? _pCoreAccum : _eCoreAccum;
            if (ownCore) _pCoreAccum = 0f; else _eCoreAccum = 0f;
            int shown = Mathf.Max(1, Mathf.RoundToInt(pooled));

            var anchorImg = ownCore ? _playerCoreFlash : _enemyCoreFlash;
            if (anchorImg == null || _container == null) return;

            var go = new GameObject("coreDmg");
            go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(ownCore ? 0f : 1f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(ownCore ? 34f : -34f, 0f);
            rt.sizeDelta = new Vector2(120f, 34f);

            var txt = go.AddComponent<Text>();
            txt.font = _font;
            txt.fontSize = ownCore ? 26 : 22;
            txt.fontStyle = FontStyle.Bold;
            txt.alignment = TextAnchor.MiddleCenter;
            txt.raycastTarget = false;
            txt.supportRichText = false;
            txt.text = "-" + shown;
            txt.color = ownCore ? new Color(1f, 0.30f, 0.22f) : PlayerSolid;
            StartCoroutine(FloatCoreDamage(rt, txt, ownCore));
        }

        IEnumerator FloatCoreDamage(RectTransform rt, Text txt, bool ownCore)
        {
            Vector2 from = rt.anchoredPosition;
            const float DUR = 0.85f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float k = t / DUR;
                rt.anchoredPosition = from + new Vector2(0f, 46f * EaseOut(k));
                float pop = k < 0.14f ? 1f + (0.14f - k) * 2.2f : 1f;
                rt.localScale = new Vector3(pop, pop, 1f);
                var c = txt.color; c.a = 1f - k * k;
                txt.color = c;
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // Dark burn mark under a death site — fades in, lingers, fades out
        IEnumerator ScorchDecal(RectTransform lane, Vector2 at, float size)
        {
            if (lane == null) yield break;
            var go = new GameObject("scorch");
            go.transform.SetParent(lane, false);
            go.transform.SetAsFirstSibling(); // under units, over the lane background
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at + new Vector2(0f, -size * 0.28f);
            rt.sizeDelta = new Vector2(size * 1.3f, size * 0.45f);
            var img = go.AddComponent<RawImage>();
            img.texture = NeonArt.Glow(Color.white); // radial falloff shape, tinted black below
            img.raycastTarget = false;
            const float IN = 0.08f, HOLD = 2.2f, OUT = 1.0f;
            for (float t = 0f; t < IN; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                img.color = new Color(0f, 0f, 0f, 0.50f * (t / IN));
                yield return null;
            }
            img.color = new Color(0f, 0f, 0f, 0.50f);
            yield return new WaitForSeconds(HOLD);
            for (float t = 0f; t < OUT; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                img.color = new Color(0f, 0f, 0f, 0.50f * (1f - t / OUT));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // Post-shot reposition: ranged infantry take a small lateral shuffle between shots
        IEnumerator StrafeStep(UnitView v)
        {
            if (v.Dying || v.Rt == null || v.Lunging || v.ScaleAnim) yield break;
            float dy = (Random.value < 0.5f ? -1f : 1f) * Random.Range(1.2f, 2.2f);
            const float DUR = 0.20f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null || v.Lunging || v.ScaleAnim) yield break;
                v.SpawnYOffset = dy * Mathf.Sin(t / DUR * Mathf.PI);
                yield return null;
            }
            if (!v.Dying && v.Rt != null) v.SpawnYOffset = 0f;
        }

        // Tiny ejected casing: pops up-backward, tumbles under gravity, fades
        IEnumerator ShellCasing(RectTransform lane, Vector2 at, bool isPlayer)
        {
            if (lane == null) yield break;
            var go = new GameObject("shell");
            go.transform.SetParent(lane, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(2.5f, 4f);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.85f, 0.75f, 0.35f, 0.9f);
            img.raycastTarget = false;
            float back = isPlayer ? -1f : 1f;
            Vector2 vel = new Vector2(back * Random.Range(18f, 34f), Random.Range(38f, 55f));
            float spin = Random.Range(-540f, 540f);
            const float DUR = 0.42f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                vel.y -= 290f * Time.deltaTime;
                rt.anchoredPosition += vel * Time.deltaTime;
                rt.localRotation = Quaternion.Euler(0f, 0f, rt.localEulerAngles.z + spin * Time.deltaTime);
                img.color = new Color(0.85f, 0.75f, 0.35f, 0.9f * (1f - t / DUR));
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        // One segment of a projectile's light trail — stretches along flight, fades fast
        IEnumerator TrailGhost(RectTransform lane, Vector2 at, Color col, float size, float angDeg)
        {
            if (lane == null) yield break;
            var go = new GameObject("trail");
            go.transform.SetParent(lane, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            rt.sizeDelta = new Vector2(size * 2.2f, size * 0.8f);
            rt.localRotation = Quaternion.Euler(0f, 0f, angDeg);
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            const float DUR = 0.13f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float a = 0.45f * (1f - t / DUR);
                img.color = new Color(col.r, col.g, col.b, a);
                rt.localScale = new Vector3(1f - t / DUR * 0.5f, 1f - t / DUR * 0.5f, 1f);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        IEnumerator ImpactSpark(RectTransform rt, Image img, Vector2 vel)
        {
            const float DUR = 0.18f;
            Color c = img.color;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                rt.anchoredPosition += vel * Time.deltaTime;
                vel *= 1f - Time.deltaTime * 7f;
                img.color = new Color(c.r, c.g, c.b, 1f - t / DUR);
                yield return null;
            }
            if (rt != null) Destroy(rt.gameObject);
        }

        static readonly Color HpTrackDark = new Color(0.08f, 0.08f, 0.08f, 0.96f);

        IEnumerator HitFlashCoroutine(UnitView v)
        {
            // Impact scale punch — quick pop that settles back, sells the hit physically
            bool punch = !v.Lunging && !v.ScaleAnim && !v.Dying && v.Rt != null;
            if (punch) v.ScaleAnim = true;
            const float FDUR = 0.20f;
            for (float t = 0f; t < FDUR; t += Time.deltaTime)
            {
                float p = t / FDUR;
                // HP bar border flashes with the damage — white contact → red → dark track
                if (v.HpBg != null)
                    v.HpBg.color = t < 0.045f
                        ? Color.white
                        : Color.Lerp(new Color(1f, 0.30f, 0.15f, 1f), HpTrackDark, p);
                if (punch && v.Rt != null && !v.Dying)
                {
                    float k = 1f + 0.035f * (1f - p) * (1f - p);
                    v.Rt.localScale = new Vector3(k, k, 1f);
                }
                yield return null;
            }
            if (punch)
            {
                if (v.Rt != null && !v.Dying) v.Rt.localScale = Vector3.one;
                v.ScaleAnim = false;
            }
            if (v.HpBg != null) v.HpBg.color = HpTrackDark;
        }

        IEnumerator SparkBurst(UnitView v, Vector2? at = null)
        {
            if (v == null || v.Rt == null) yield break;
            var parent = v.Rt.parent as RectTransform;
            if (parent == null) yield break;
            Vector2 origin = at ?? v.Rt.anchoredPosition;
            Color col = v.IsPlayer ? PlayerSolid : EnemySolid;
            const int COUNT = 6;
            var imgs = new Image[COUNT];
            var rts  = new RectTransform[COUNT];
            var vels = new Vector2[COUNT];
            for (int i = 0; i < COUNT; i++)
            {
                var go = new GameObject("spark");
                go.transform.SetParent(parent, false);
                var rt = go.AddComponent<RectTransform>();
                // Match the lane's left-anchored / vertically-centered frame like every other
                // effect. Without this the sparks landed in a default anchor frame and rendered
                // detached from the clash — the stray "blips" seen during melee fights.
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = origin;
                rt.sizeDelta = new Vector2(5f, 5f);
                var img = go.AddComponent<Image>();
                img.sprite = NeonArt.SparkDot();
                img.color = col;
                img.raycastTarget = false;
                float angle = i * (360f / COUNT) + Random.Range(-15f, 15f);
                float speed = Random.Range(55f, 105f);
                vels[i] = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad)) * speed;
                imgs[i] = img; rts[i] = rt;
            }
            float dur = 0.26f;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float alpha = 1f - t / dur;
                for (int i = 0; i < COUNT; i++)
                {
                    if (rts[i] == null) continue;
                    rts[i].anchoredPosition += vels[i] * Time.deltaTime;
                    vels[i] *= 1f - Time.deltaTime * 6f;
                    imgs[i].color = new Color(col.r, col.g, col.b, alpha);
                }
                yield return null;
            }
            for (int i = 0; i < COUNT; i++)
                if (imgs[i] != null) Destroy(imgs[i].gameObject);
        }

        // ── per-troop attack animations ──────────────────────────────────────────

        IEnumerator TroopAttackAnim(UnitView v, bool ranged)
        {
            if (v == null || v.Dying) yield break;
            // Per-class attack voice — quiet layer under the impact SFX
            var atkSfx = v.SpecId switch
            {
                "sniper" or "turret"       => AudioManager.Sfx.AttackLaser,
                "mech" or "titan"          => AudioManager.Sfx.AttackCannon,
                "drone" or "hacker"
                    or "interceptor"       => AudioManager.Sfx.AttackZap,
                _                          => AudioManager.Sfx.AttackMelee,
            };
            AudioManager.Play(atkSfx, v.SpecId == "titan" ? 0.55f : 0.38f);
            switch (v.SpecId)
            {
                case "drone":       yield return StartCoroutine(AnimDroneDive(v));          break;
                case "trooper":     yield return StartCoroutine(AnimTrooperPunch(v));      break;
                case "sniper":      yield return StartCoroutine(AnimSniperRecoil(v));      break;
                case "mech":        yield return StartCoroutine(AnimMechFire(v));          break;
                case "shield-bot":  yield return StartCoroutine(AnimShieldBash(v, ranged)); break;
                case "interceptor": yield return StartCoroutine(AnimInterceptorStrafe(v)); break;
                case "hacker":      yield return StartCoroutine(AnimHackerTendrils(v));    break;
                case "titan":       yield return StartCoroutine(AnimTitanAttack(v, ranged)); break;
                case "turret":      yield return StartCoroutine(AnimTurretFire(v));        break;
                default:            yield return StartCoroutine(AttackLunge(v));           break;
            }

            // Choreography: ranged infantry shuffle to a new firing position between shots
            if (ranged && !v.Dying
                && v.SpecId != "turret" && v.SpecId != "titan"
                && v.SpecId != "drone" && v.SpecId != "interceptor")
                StartCoroutine(StrafeStep(v));
        }

        IEnumerator AnimMuzzleFlash(UnitView v, Vector2 localOffset, float size, Color col, float dur = 0.14f, bool gun = false)
        {
            if (v?.AttackFlash == null) yield break;
            // Offsets/sizes are authored in 52-unit body space; scale them onto this unit so
            // flashes still land on the drawn muzzle rather than drifting toward the centre.
            localOffset *= v.UScale;
            size        *= v.UScale;
            // Gunshots leave residue: smoke puff + ejected shell casing at the muzzle
            if (gun && v.Rt != null && v.Rt.parent is RectTransform gLane)
            {
                Vector2 world = v.Rt.anchoredPosition + localOffset;
                StartCoroutine(DustPuff(gLane, world, new Color(0.55f, 0.58f, 0.66f, 0.28f)));
                StartCoroutine(ShellCasing(gLane, world, v.IsPlayer));
            }
            var rt = v.AttackFlash.rectTransform;
            rt.anchoredPosition = localOffset;
            rt.sizeDelta = new Vector2(size, size);
            v.AttackFlash.color = col;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (v.AttackFlash == null) yield break;
                float a = 1f - t / dur;
                rt.sizeDelta = new Vector2(size * (1f + t / dur * 0.5f), size * (1f + t / dur * 0.5f));
                v.AttackFlash.color = new Color(col.r, col.g, col.b, col.a * a);
                yield return null;
            }
            if (v.AttackFlash != null) v.AttackFlash.color = Color.clear;
        }

        // Drone: whole body dives toward target (SpawnYOffset dip), impact flash, recoil up
        IEnumerator AnimDroneDive(UnitView v)
        {
            if (v.Lunging) yield break;
            v.Lunging = true;
            const float DUR = 0.22f;
            float dir = v.IsPlayer ? 1f : -1f;
            // Dive forward and down
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                float p = t / DUR;
                v.LungeOffset  = dir * 20f * Mathf.Sin(p * Mathf.PI * 0.65f);
                v.SpawnYOffset = -10f * Mathf.Sin(p * Mathf.PI);
                yield return null;
            }
            // Impact flash
            StartCoroutine(AnimMuzzleFlash(v, Vector2.zero, 16f, new Color(1f, 0.85f, 0.3f, 0.90f)));
            // Recoil back and up
            for (float t = 0f; t < 0.15f; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                float p = t / 0.15f;
                v.LungeOffset  = dir * 20f * (1f - p);
                v.SpawnYOffset = 8f * (1f - p);
                yield return null;
            }
            if (!v.Dying && v.Rt != null) { v.LungeOffset = 0f; v.SpawnYOffset = 0f; }
            v.Lunging = false;
        }

        // Sniper: scope pulse → barrel recoil BACKWARD, then return
        IEnumerator AnimSniperRecoil(UnitView v)
        {
            if (v.Lunging) yield break;
            v.Lunging = true;
            v.Anim = UAnim.Windup;
            // Laser sight: thin targeting line sweeps out toward the enemy while aiming
            var laneRt = v.Rt.parent as RectTransform;
            GameObject sight = null;
            if (laneRt != null)
            {
                sight = new GameObject("sight");
                sight.transform.SetParent(laneRt, false);
                var srt = sight.AddComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot = new Vector2(v.IsPlayer ? 0f : 1f, 0.5f);
                srt.anchoredPosition = v.Rt.anchoredPosition + new Vector2(v.IsPlayer ? 16f : -16f, -2f) * v.UScale;
                srt.sizeDelta = new Vector2(0f, 1.5f);
                var simg = sight.AddComponent<Image>();
                Color sc = v.IsPlayer ? PlayerSolid : EnemySolid;
                simg.color = new Color(sc.r, sc.g, sc.b, 0.45f);
                simg.raycastTarget = false;
                const float AIM = 0.14f;
                for (float t = 0f; t < AIM; t += Time.deltaTime)
                {
                    if (v.Dying || v.Rt == null) { if (sight != null) Destroy(sight); v.Lunging = false; yield break; }
                    srt.sizeDelta = new Vector2(150f * EaseOut(t / AIM), 1.5f);
                    yield return null;
                }
            }
            v.Anim = UAnim.Strike;
            // 1-frame scope charge-up dot
            if (v.AttackFlash != null)
            {
                float sx = v.IsPlayer ? 16f : -16f;
                v.AttackFlash.rectTransform.anchoredPosition = new Vector2(sx, -2f) * v.UScale;
                v.AttackFlash.rectTransform.sizeDelta = new Vector2(6f, 6f) * v.UScale;
                v.AttackFlash.color = Color.white;
                yield return null;
                if (v.AttackFlash != null) v.AttackFlash.color = Color.clear;
            }
            // SHOT: muzzle flash, sight-off, and recoil all begin THIS frame —
            // any gap between the flash and the kick reads as broken.
            Color scopeCol = v.IsPlayer ? PlayerSolid : EnemySolid;
            float muzzleX = v.IsPlayer ? 16f : -16f;
            StartCoroutine(AnimMuzzleFlash(v, new Vector2(muzzleX, -2f), 12f,
                new Color(scopeCol.r, scopeCol.g, scopeCol.b, 0.85f), 0.14f, gun: true));
            if (sight != null) Destroy(sight);
            // Recoil BACKWARD — the rifle slides hardest, the body follows softer
            float dir = v.IsPlayer ? -1f : 1f;  // sniper kicks back
            var rifle = v.Weapon != null && v.Weapon.enabled ? v.Weapon.rectTransform : null;
            const float DUR = 0.18f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                float k = Strike(t / DUR);
                v.LungeOffset = dir * 7f * k; // sharp kick, slow settle
                if (rifle != null) rifle.anchoredPosition = new Vector2(dir * 4f * k, 0f);
                yield return null;
            }
            if (!v.Dying && v.Rt != null) v.LungeOffset = 0f;
            if (rifle != null) rifle.anchoredPosition = Vector2.zero;
            v.Lunging = false;
        }

        // Mech: cannon arm extends toward target (forward lunge) + muzzle flash at side
        IEnumerator AnimMechFire(UnitView v)
        {
            if (v.Lunging) yield break;
            v.Lunging = true;
            // Plant: feet stop, slight settle before the cannon fires
            yield return Anticipate(v, 1.03f, 0.97f, 2f, 0f, 0.08f);
            float dir   = v.IsPlayer ? 1f : -1f;
            float armX  = dir * 13f; // drawn cannon tip: texture (108,95) → rect +13, +8
            Color armCol = v.IsPlayer ? PlayerSolid : EnemySolid;
            // Extend arm + flash
            for (float t = 0f; t < 0.20f; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                v.LungeOffset = dir * 10f * Strike(t / 0.20f);
                yield return null;
            }
            // Triple burst — the cannon ARM ratchets back per shot, body stays planted
            var arm = v.Weapon != null && v.Weapon.enabled ? v.Weapon.rectTransform : null;
            // Weapon-layer offsets live in the unit's local pixel space, so they need the same
            // scale as everything else or the recoil vanishes on a 3.2x body.
            float kick = (v.IsPlayer ? -3f : 3f) * v.UScale;
            for (int shot = 0; shot < 3; shot++)
            {
                StartCoroutine(AnimMuzzleFlash(v, new Vector2(armX, 8f), 14f - shot * 2f,
                    new Color(armCol.r * 1.2f, armCol.g * 0.8f, armCol.b * 0.3f, 0.95f), 0.09f, gun: shot == 0));
                if (arm != null) arm.anchoredPosition = new Vector2(kick, 0f);
                else if (!v.Dying && v.Rt != null) v.LungeOffset = kick;
                yield return new WaitForSeconds(0.055f);
                if (arm != null) arm.anchoredPosition = Vector2.zero;
                else if (!v.Dying && v.Rt != null) v.LungeOffset = 0f;
            }
            if (!v.Dying && v.Rt != null) v.LungeOffset = 0f;
            if (arm != null) arm.anchoredPosition = Vector2.zero;
            v.Lunging = false;
        }

        // Titan arms: haul back through the windup, drive through on the strike, then settle.
        // Runs alongside the body coroutine so the weapon layer and the pose stay in step.
        IEnumerator TitanArmSwing(UnitView v, RectTransform arms, float dir, float reach)
        {
            const float WIND = 0.40f, SLAM = 0.16f, SETTLE = 0.26f;
            for (float t = 0f; t < WIND; t += Time.deltaTime)
            {
                if (v.Dying || arms == null) yield break;
                float k = EaseOut(t / WIND);
                arms.anchoredPosition = new Vector2(-dir * reach * 0.55f * k, 4f * v.UScale * k);
                yield return null;
            }
            for (float t = 0f; t < SLAM; t += Time.deltaTime)
            {
                if (v.Dying || arms == null) yield break;
                float k = Strike(t / SLAM);
                arms.anchoredPosition = new Vector2(
                    Mathf.Lerp(-dir * reach * 0.55f, dir * reach, k), Mathf.Lerp(4f * v.UScale, -2f * v.UScale, k));
                yield return null;
            }
            for (float t = 0f; t < SETTLE; t += Time.deltaTime)
            {
                if (v.Dying || arms == null) yield break;
                float k = 1f - EaseOut(t / SETTLE);
                arms.anchoredPosition = new Vector2(dir * reach * k, -2f * v.UScale * k);
                yield return null;
            }
            if (arms != null) arms.anchoredPosition = Vector2.zero;
        }

        // Shield Bot: melee = big bash lunge; ranged = small recoil + counter-shot flash
        IEnumerator AnimShieldBash(UnitView v, bool ranged)
        {
            if (v.Lunging) yield break;
            v.Lunging = true;
            float dir = v.IsPlayer ? 1f : -1f;
            if (!ranged)
            {
                // Drag back before the slam
                yield return Anticipate(v, 1.05f, 0.95f, 4f, 3f, 0.09f);
                // Shield bash: strong forward lunge with body lean
                for (float t = 0f; t < 0.20f; t += Time.deltaTime)
                {
                    if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                    float k = Strike(t / 0.20f);
                    v.LungeOffset      = dir * 6f * k;   // shield drives forward on the pose, not the body
                    v.Rt.localRotation = Quaternion.Euler(0f, 0f, -dir * 4f * k);
                    yield return null;
                }
                if (!v.Dying && v.Rt != null) v.Rt.localRotation = Quaternion.identity;
                StartCoroutine(AnimMuzzleFlash(v, new Vector2(dir * 14f, 0f), 18f,
                    new Color(0.4f, 0.8f, 1f, 0.80f)));
            }
            else
            {
                // Deflect + counter-fire: small backward jerk then flash
                v.LungeOffset = -dir * 5f;
                yield return new WaitForSeconds(0.05f);
                v.LungeOffset = 0f;
                StartCoroutine(AnimMuzzleFlash(v, new Vector2(dir * 10f, 4f), 12f,
                    new Color(0.5f, 1f, 0.6f, 0.85f)));
            }
            if (!v.Dying && v.Rt != null) v.LungeOffset = 0f;
            v.Lunging = false;
        }

        // Interceptor: bank-tilt rotation + brief strafing sweep pass
        IEnumerator AnimInterceptorStrafe(UnitView v)
        {
            if (v.Lunging || v.Rt == null) yield break;
            v.Lunging = true;
            float dir   = v.IsPlayer ? 1f : -1f;
            float bank  = dir * -22f; // roll angle degrees
            float sweep = dir * 30f;  // strafing LungeOffset distance
            const float DUR = 0.28f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) yield break;
                float p = t / DUR;
                float s = Mathf.Sin(p * Mathf.PI);
                v.Rt.localRotation = Quaternion.Euler(0f, 0f, bank * s);
                v.LungeOffset      = sweep * s;
                yield return null;
            }
            if (!v.Dying && v.Rt != null)
            {
                v.Rt.localRotation = Quaternion.identity;
                v.LungeOffset = 0f;
            }
            v.Lunging = false;
        }

        // Hacker: tendrils pulse — VfxA brightens (scale swell) and fades
        IEnumerator AnimHackerTendrils(UnitView v)
        {
            if (v.Lunging || v.AttackFlash == null) yield break;
            v.Lunging = true;
            // Scale-pulse the whole unit briefly (tendrils extending outward)
            const float DUR = 0.30f;
            Color tendCol = v.IsPlayer ? PlayerSolid : EnemySolid;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) yield break;
                float p = t / DUR;
                float pulse = 1f + 0.22f * Mathf.Sin(p * Mathf.PI);
                v.Rt.localScale = new Vector3(pulse, pulse, 1f);
                // Flash at tendril tips (positioned outward)
                if (v.AttackFlash != null)
                {
                    v.AttackFlash.rectTransform.anchoredPosition = Vector2.zero;
                    v.AttackFlash.rectTransform.sizeDelta = new Vector2(pulse * 28f, pulse * 28f) * v.UScale;
                    v.AttackFlash.color = new Color(tendCol.r, tendCol.g, tendCol.b, 0.55f * Mathf.Sin(p * Mathf.PI));
                }
                yield return null;
            }
            if (!v.Dying && v.Rt != null) v.Rt.localScale = Vector3.one;
            if (v.AttackFlash != null) v.AttackFlash.color = Color.clear;
            v.Lunging = false;
        }

        // Titan: melee = massive lunge + double flash; ranged = dual cannon simultaneous fire
        IEnumerator AnimTitanAttack(UnitView v, bool ranged)
        {
            if (v.Lunging) yield break;
            v.Lunging = true;
            float dir = v.IsPlayer ? 1f : -1f;
            Color cannon = v.IsPlayer ? PlayerSolid : EnemySolid;
            if (!ranged)
            {
                // The ARMS were never animated here: only the ranged branch touched the weapon
                // layer, and a Range-4 titan never takes that branch, so its barrels sat frozen
                // through every swing. They now haul back with the windup and slam through.
                var arms = v.Weapon != null && v.Weapon.enabled ? v.Weapon.rectTransform : null;
                float reach = 26f * v.UScale;

                // Long telegraphed windup — a 500 HP siege hammer should be readable and
                // avoidable, so this is deliberately slow (0.13s was faster than infantry).
                if (arms != null) StartCoroutine(TitanArmSwing(v, arms, dir, reach));
                yield return Anticipate(v, 1.10f, 0.90f, 8f, 5f, 0.40f);

                // Heavy arm swing: wide lunge, screen impact
                for (float t = 0f; t < 0.24f; t += Time.deltaTime)
                {
                    if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                    v.LungeOffset = dir * 20f * Strike(t / 0.24f);
                    yield return null;
                }
                StartCoroutine(AnimMuzzleFlash(v, new Vector2(dir * 20f, -4f), 20f,
                    new Color(cannon.r, cannon.g, cannon.b, 0.90f)));
                var lp = v.Rt.parent as RectTransform;
                if (lp != null) StartCoroutine(ShakeLane(lp, 3f, 0.14f));
            }
            else
            {
                // Dual cannons: flashes at the drawn muzzle tips (texture 6/122, 80 → rect ±23.6, +5.2)
                Color warm = new Color(1f, 0.72f, 0.18f, 0.95f);
                StartCoroutine(AnimMuzzleFlash(v, new Vector2(-24f, 5f), 16f, warm, 0.18f, gun: true));
                StartCoroutine(AnimMuzzleFlash(v, new Vector2(+24f, 5f), 16f, warm, 0.18f));
                // Artillery recoil: the cannon assembly dips, then re-seats
                var cannons = v.Weapon != null && v.Weapon.enabled ? v.Weapon.rectTransform : null;
                // The strike pose rocks the hull back ~9 canvas units. The barrels live on the
                // weapon layer and cannot be posed, so recoil them by the same amount in rect
                // space or they hang in the air where the hull used to be.
                if (cannons != null)
                    cannons.anchoredPosition = new Vector2(
                        -(v.IsPlayer ? 1f : -1f) * 9f / 128f * v.Rt.sizeDelta.x, -3f * v.UScale);
                v.SpawnYOffset = -3f;
                yield return new WaitForSeconds(0.10f);
                if (cannons != null)
                {
                    for (float t = 0f; t < 0.12f; t += Time.deltaTime)
                    {
                        if (v.Rt == null) break;
                        cannons.anchoredPosition = new Vector2(0f, -3f * (1f - t / 0.12f));
                        yield return null;
                    }
                    cannons.anchoredPosition = Vector2.zero;
                }
                if (!v.Dying && v.Rt != null) v.SpawnYOffset = 0f;
            }
            if (!v.Dying && v.Rt != null) v.LungeOffset = 0f;
            v.Lunging = false;
        }

        IEnumerator ShakeLane(RectTransform lane, float amp, float dur)
        {
            if (lane == null) yield break;
            Vector2 orig = lane.anchoredPosition;
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (lane == null) yield break;
                float str = (1f - t / dur) * amp;
                lane.anchoredPosition = new Vector2(orig.x, orig.y + Mathf.Sin(t * 80f) * str);
                yield return null;
            }
            if (lane != null) lane.anchoredPosition = orig;
        }

        // Turret: barrel rotates toward target, fires, recoils
        IEnumerator AnimTurretFire(UnitView v)
        {
            if (v.Rt == null || v.Lunging) yield break;
            v.Lunging = true; // owns the weapon layer — pauses the idle scan
            float dir = v.IsPlayer ? 1f : -1f;
            Color barrelCol = v.IsPlayer ? PlayerSolid : EnemySolid;
            var wpn = v.Weapon != null && v.Weapon.enabled ? v.Weapon.rectTransform : null;
            // Barrels track toward the target (base stays planted) + charge glow builds
            float aimAngle = dir * 10f;
            for (float t = 0f; t < 0.10f; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                if (wpn != null) wpn.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, aimAngle, t / 0.10f));
                if (v.AttackFlash != null)
                {
                    float cp = t / 0.10f;
                    v.AttackFlash.rectTransform.anchoredPosition = new Vector2(dir * 10f, -1f) * v.UScale;
                    v.AttackFlash.rectTransform.sizeDelta = new Vector2(2f + 7f * cp, 2f + 7f * cp) * v.UScale;
                    v.AttackFlash.color = new Color(barrelCol.r, barrelCol.g, barrelCol.b, 0.55f * cp);
                }
                yield return null;
            }
            // Fire: flash at the barrel tips + barrel assembly kicks straight back
            StartCoroutine(AnimMuzzleFlash(v, new Vector2(dir * 10f, -1f), 13f,
                new Color(barrelCol.r * 1.3f, barrelCol.g, barrelCol.b, 0.90f), gun: true));
            if (wpn != null) wpn.anchoredPosition = new Vector2(-dir * 3f, 0f);
            yield return new WaitForSeconds(0.04f);
            // Recoil: barrels slide home and rotation returns to rest
            for (float t = 0f; t < 0.12f; t += Time.deltaTime)
            {
                if (v.Dying || v.Rt == null) { v.Lunging = false; yield break; }
                float p = t / 0.12f;
                if (wpn != null)
                {
                    wpn.localRotation    = Quaternion.Euler(0f, 0f, Mathf.Lerp(aimAngle, 0f, p));
                    wpn.anchoredPosition = new Vector2(-dir * 3f * (1f - p), 0f);
                }
                yield return null;
            }
            if (!v.Dying && wpn != null)
            {
                wpn.localRotation    = Quaternion.identity;
                wpn.anchoredPosition = Vector2.zero;
            }
            v.Lunging = false;
        }

        IEnumerator DeathExplosion(ulong id, UnitView v)
        {
            Color col    = v.IsPlayer ? PlayerSolid : EnemySolid;
            var   parent = v.Rt.parent as RectTransform;

            bool big = v.SpecId == "titan" || v.SpecId == "mech" || v.SpecId == "shield-bot";
            bool air = v.SpecId == "drone" || v.SpecId == "interceptor";

            // Air units spin out and fall before exploding
            if (air && v.Rt != null)
            {
                float spinDir = v.IsPlayer ? -1f : 1f;
                Vector2 start = v.Rt.anchoredPosition;
                const float FALL = 0.30f;
                for (float t = 0f; t < FALL; t += Time.deltaTime)
                {
                    if (v.Rt == null) break;
                    float p = t / FALL;
                    v.Rt.anchoredPosition = start + new Vector2(spinDir * 18f * p, -34f * p * p);
                    v.Rt.localRotation    = Quaternion.Euler(0f, 0f, spinDir * 540f * p);
                    if (v.Body) v.Body.color = new Color(v.Body.color.r, v.Body.color.g, v.Body.color.b, 1f - p * 0.3f);
                    yield return null;
                }
            }

            // Heavies go out with a pre-flash and a lane shake
            if (big)
            {
                StartCoroutine(AnimMuzzleFlash(v, Vector2.zero, 34f, new Color(1f, 1f, 1f, 0.85f), 0.16f));
                if (parent != null) StartCoroutine(ShakeLane(parent, 4f, 0.20f));
            }

            Vector2 origin = v.Rt != null ? v.Rt.anchoredPosition : Vector2.zero;

            // Lingering smoke where the unit died — explosion → debris → smoke → fade
            if (parent != null)
            {
                StartCoroutine(DustPuff(parent, origin, new Color(0.30f, 0.31f, 0.38f, 0.45f)));
                if (big)
                {
                    StartCoroutine(DustPuff(parent, origin + new Vector2(6f, 5f), new Color(0.24f, 0.25f, 0.32f, 0.40f)));
                    AddTrauma(0.10f);
                }
                // Scorch mark stays on the lane after the debris settles
                StartCoroutine(ScorchDecal(parent, origin, v.Rt != null ? v.Rt.sizeDelta.x : 30f));
            }

            // Fragment particles — heavier classes shed more debris
            int FRAGS = big ? 14 : air ? 6 : 8;
            var fragRt   = new RectTransform[FRAGS];
            var fragImg  = new Image[FRAGS];
            var fragVel  = new Vector2[FRAGS];

            for (int fi = 0; fi < FRAGS; fi++)
            {
                float ang  = fi / (float)FRAGS * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                float spd  = Random.Range(45f, 95f);
                fragVel[fi] = new Vector2(Mathf.Cos(ang) * spd, Mathf.Sin(ang) * spd);

                var fgo = new GameObject("frag");
                fgo.transform.SetParent(parent, false);
                var frt = fgo.AddComponent<RectTransform>();
                frt.anchorMin = frt.anchorMax = new Vector2(0f, 0.5f);
                frt.pivot = new Vector2(0.5f, 0.5f);
                frt.anchoredPosition = origin;
                float sz = Random.Range(3f, 7f);
                frt.sizeDelta = new Vector2(sz, sz);
                var fimg = fgo.AddComponent<Image>();
                fimg.color = col;
                fimg.raycastTarget = false;
                fragRt[fi]  = frt;
                fragImg[fi] = fimg;
            }

            float tiltDir = v.IsPlayer ? -1f : 1f; // topple in direction of advance
            const float DUR = 0.45f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                float alpha = 1f - t / DUR;
                for (int fi = 0; fi < FRAGS; fi++)
                {
                    if (fragRt[fi] == null) continue;
                    fragRt[fi].anchoredPosition += fragVel[fi] * Time.deltaTime;
                    fragVel[fi] *= 1f - Time.deltaTime * 4f; // friction
                    fragImg[fi].color = new Color(col.r, col.g, col.b, alpha);
                }
                v.Rt.localScale    = new Vector3(1f + (1f - alpha) * 0.2f, 1f + (1f - alpha) * 0.2f, 1f);
                v.Rt.localRotation = Quaternion.Euler(0f, 0f, tiltDir * 10f * (1f - alpha));
                if (v.Body)     v.Body.color     = new Color(1f, 1f, 1f, alpha);
                if (v.Glow)     v.Glow.color     = new Color(1f, 1f, 1f, alpha * 0.55f);
                yield return null;
            }

            foreach (var frt in fragRt)
                if (frt != null) Destroy(frt.gameObject);

            v.Rt.localScale = Vector3.one;
            ReleaseView(v);
            _views.Remove(id);
        }

        /// <summary>
        /// Battle-end spectacle: slow-mo + staggered explosions on the losing side +
        /// white field flash. HUDView yields on this before showing the result card.
        /// </summary>
        public IEnumerator CoreDestructionFinale(bool playerWon)
        {
            if (_laneRects == null || _laneRects[0] == null) yield break;
            float laneW = _laneRects[0].rect.width;
            if (laneW <= 0f) laneW = 900f;
            float x   = playerWon ? laneW - 45f : 45f;
            Color col = playerWon ? EnemySolid : PlayerSolid;

            Time.timeScale = 0.35f; // explosions play out in slow motion
            for (int i = 0; i < 4; i++)
            {
                var lane = _laneRects[Random.Range(0, CombatSim.GroundLanes)];
                Vector2 at = new Vector2(x + Random.Range(-28f, 28f), Random.Range(-14f, 14f));
                StartCoroutine(FinaleExplosion(lane, at, col, 26f + i * 8f));
                AddTrauma(0.30f);
                AudioManager.Play(AudioManager.Sfx.CoreHit, 0.9f, 1f + i * 0.07f);
                yield return new WaitForSecondsRealtime(0.17f);
            }
            StartCoroutine(FieldFlash(new Color(1f, 1f, 1f, 0.50f), 0.45f));
            yield return new WaitForSecondsRealtime(0.40f);
            Time.timeScale = 1f;
        }

        IEnumerator FinaleExplosion(RectTransform lane, Vector2 at, Color col, float size)
        {
            if (lane == null) yield break;
            // Core flash quad that balloons and fades
            var go = new GameObject("boom");
            go.transform.SetParent(lane, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = at;
            var img = go.AddComponent<Image>();
            img.raycastTarget = false;
            // Scatter sparks
            for (int i = 0; i < 8; i++)
            {
                var sgo = new GameObject("bs");
                sgo.transform.SetParent(lane, false);
                var srt = sgo.AddComponent<RectTransform>();
                srt.anchorMin = srt.anchorMax = new Vector2(0f, 0.5f);
                srt.pivot = new Vector2(0.5f, 0.5f);
                srt.anchoredPosition = at;
                srt.sizeDelta = new Vector2(5f, 5f);
                var simg = sgo.AddComponent<Image>();
                simg.color = Color.white; simg.raycastTarget = false;
                float ang = Random.Range(0f, Mathf.PI * 2f);
                StartCoroutine(ImpactSpark(srt, simg,
                    new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * Random.Range(90f, 220f)));
            }
            const float DUR = 0.40f;
            for (float t = 0f; t < DUR; t += Time.deltaTime)
            {
                if (rt == null) yield break;
                float p = t / DUR;
                float s = size * (0.3f + p * 1.7f);
                rt.sizeDelta = new Vector2(s, s);
                img.color = Color.Lerp(Color.white, col, p) * new Color(1f, 1f, 1f, 1f - p);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        IEnumerator CoreFlash(Image img, bool isPlayerCore)
        {
            Color col = isPlayerCore
                ? new Color(1f, 0.15f, 0.05f, 0.90f)
                : new Color(0.15f, 0.9f, 1f,   0.80f);
            img.color = col;
            for (float t = 0f; t < 0.40f; t += Time.deltaTime)
            {
                if (img == null) yield break;
                img.color = new Color(col.r, col.g, col.b, col.a * (1f - t / 0.40f));
                yield return null;
            }
            if (img != null) img.color = new Color(col.r, col.g, col.b, 0f);
        }

        IEnumerator LaserSweep(int lane)
        {
            AudioManager.Play(AudioManager.Sfx.LaneStrike);
            var img = _laneLaser[lane];
            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                img.color = new Color(0.4f, 1f, 1f, 0.85f * (1f - t / 0.3f));
                yield return null;
            }
            img.color = new Color(0.4f, 1f, 1f, 0f);
            AddTrauma(0.12f);
        }

        IEnumerator FieldFlash(Color col, float dur)
        {
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                _fieldFlash.color = new Color(col.r, col.g, col.b, col.a * (1f - t / dur));
                yield return null;
            }
            _fieldFlash.color = new Color(col.r, col.g, col.b, 0f);
        }

        // ──────────────────────────────────────────────── helpers ─────────────

        RectTransform MakeLaneRect(string name, float yMin, float yMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, yMin); rt.anchorMax = new Vector2(1f, yMax);
            rt.offsetMin = new Vector2(CORE_W + 2, 0);
            rt.offsetMax = new Vector2(-(CORE_W + 2), 0);
            go.AddComponent<Image>();
            return rt;
        }

        Image MakeImage(RectTransform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color col)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>();
            img.color = col;
            return img;
        }

        Image AddHighlight(RectTransform lane)
        {
            var img = MakeImage(lane, "Highlight", Vector2.zero, Vector2.one, new Color(0, 0, 0, 0));
            img.raycastTarget = false;
            return img;
        }

        void AddLaneButton(RectTransform lane, int index)
        {
            var btn = lane.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(() => OnLaneClicked?.Invoke(index));
        }

        void AddLaneTooltip(RectTransform lane, int index)
        {
            // Restrict hit zone to the left label area — hovering enemy units on the right must not trigger it
            var hitGo = new GameObject("tooltipHit");
            hitGo.transform.SetParent(lane, false);
            var hitRt = hitGo.AddComponent<RectTransform>();
            hitRt.anchorMin = Vector2.zero;
            hitRt.anchorMax = new Vector2(0.20f, 1f);
            hitRt.offsetMin = hitRt.offsetMax = Vector2.zero;
            hitGo.AddComponent<Image>().color = new Color(0, 0, 0, 0);
            var tt = hitGo.AddComponent<TooltipTarget>();
            (tt.Title, tt.Body) = TooltipSystem.LaneTooltip(index);
        }

        void AddLaneLabel(RectTransform lane, string text, Color col)
        {
            var go = new GameObject("lbl");
            go.transform.SetParent(lane, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(0, 1); rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(6, -2);
            rt.sizeDelta = new Vector2(160, 16);
            var t = go.AddComponent<Text>();
            t.font = _font; t.fontSize = 11; t.color = col;
            t.alignment = TextAnchor.UpperLeft; t.text = text;
            t.raycastTarget = false;
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        // Snappy attack profile: fast cubic strike in first 30%, smooth settle back over 70%
        static float Strike(float p)
        {
            if (p < 0.30f) { float q = 1f - p / 0.30f; return 1f - q * q * q; }
            float r = (p - 0.30f) / 0.70f;
            return 1f - r * r * (3f - 2f * r);
        }

        // ─────────────────────────────── troop skin VFX ──────────────────────

        void SetupSkinVfx(UnitView v)
        {
            int skin = v.IsPlayer ? GameSettings.ActiveTroopSkin : 0;
            v.ActiveSkin = skin;
            if (v.VfxA == null) return;
            float w = v.Rt.sizeDelta.x, h = v.Rt.sizeDelta.y;

            // Clear all slots first, then configure per-skin
            v.VfxA.color = v.VfxB.color = v.VfxC.color = Color.clear;
            if (v.VfxD != null) v.VfxD.color = Color.clear;
            if (v.VfxE != null) v.VfxE.color = Color.clear;

            switch ((NeonCosmetics.TroopSkin)skin)
            {
                case NeonCosmetics.TroopSkin.Default:
                    break;

                case NeonCosmetics.TroopSkin.Golden:
                {
                    var glowSpr  = NeonArt.GlowCircle();
                    var flameSpr = NeonArt.FlameLick();

                    // VfxA: ambient golden body shimmer
                    v.VfxA.rectTransform.anchorMin = Vector2.zero;
                    v.VfxA.rectTransform.anchorMax = Vector2.one;
                    v.VfxA.rectTransform.offsetMin = new Vector2(-5f, -5f);
                    v.VfxA.rectTransform.offsetMax = new Vector2(5f, 5f);
                    v.VfxA.sprite = glowSpr;

                    // VfxB: left golden flame tongue — rises from bottom-left quarter
                    v.VfxB.rectTransform.anchorMin = v.VfxB.rectTransform.anchorMax = new Vector2(0.25f, 0f);
                    v.VfxB.rectTransform.pivot     = new Vector2(0.5f, 0f);
                    v.VfxB.rectTransform.sizeDelta = new Vector2(w * 0.55f, h * 1.15f);
                    v.VfxB.rectTransform.anchoredPosition = Vector2.zero;
                    v.VfxB.sprite = flameSpr;

                    // VfxC: right golden flame tongue — slightly smaller, out of phase
                    v.VfxC.rectTransform.anchorMin = v.VfxC.rectTransform.anchorMax = new Vector2(0.75f, 0f);
                    v.VfxC.rectTransform.pivot     = new Vector2(0.5f, 0f);
                    v.VfxC.rectTransform.sizeDelta = new Vector2(w * 0.42f, h * 0.92f);
                    v.VfxC.rectTransform.anchoredPosition = Vector2.zero;
                    v.VfxC.sprite = flameSpr;

                    // VfxD: bright crown glow at the top of the unit
                    if (v.VfxD != null)
                    {
                        v.VfxD.rectTransform.anchorMin = v.VfxD.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                        v.VfxD.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
                        v.VfxD.rectTransform.sizeDelta = new Vector2(14f, 14f);
                        v.VfxD.rectTransform.anchoredPosition = new Vector2(0f, 5f);
                        v.VfxD.sprite = glowSpr;
                    }

                    // VfxE: wide base glow disc at feet
                    if (v.VfxE != null)
                    {
                        v.VfxE.rectTransform.anchorMin = v.VfxE.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                        v.VfxE.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
                        v.VfxE.rectTransform.sizeDelta = new Vector2(w * 1.65f, h * 0.32f);
                        v.VfxE.rectTransform.anchoredPosition = Vector2.zero;
                        v.VfxE.sprite = glowSpr;
                    }
                    break;
                }

                case NeonCosmetics.TroopSkin.Chrome:
                {
                    // VfxA: subtle metallic base coat
                    v.VfxA.rectTransform.anchorMin = Vector2.zero;
                    v.VfxA.rectTransform.anchorMax = Vector2.one;
                    v.VfxA.rectTransform.offsetMin = v.VfxA.rectTransform.offsetMax = Vector2.zero;
                    // VfxB/C: two sweeping smooth shine stripes
                    var sweepSpr = NeonArt.ChromeSweep();
                    foreach (var img in new[] { v.VfxB, v.VfxC })
                    {
                        if (img == null) continue;
                        img.rectTransform.anchorMin = img.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        img.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        img.rectTransform.sizeDelta = new Vector2(w + 8f, 10f);
                        img.sprite = sweepSpr;
                    }
                    break;
                }

                case NeonCosmetics.TroopSkin.Inferno:
                {
                    var glowSpr  = NeonArt.GlowCircle();
                    var sparkSpr = NeonArt.SparkDot();

                    // VfxA: full-body heat glow — deep orange-red radial, expands just beyond unit
                    v.VfxA.rectTransform.anchorMin = Vector2.zero;
                    v.VfxA.rectTransform.anchorMax = Vector2.one;
                    v.VfxA.rectTransform.offsetMin = new Vector2(-5f, -5f);
                    v.VfxA.rectTransform.offsetMax = new Vector2(5f, 5f);
                    v.VfxA.sprite = glowSpr;

                    // VfxB: primary lava crack — wide bright glow stripe, left of center
                    v.VfxB.rectTransform.anchorMin = v.VfxB.rectTransform.anchorMax = new Vector2(0.34f, 0.5f);
                    v.VfxB.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    v.VfxB.rectTransform.sizeDelta = new Vector2(8f, h * 0.72f);
                    v.VfxB.rectTransform.anchoredPosition = Vector2.zero;
                    v.VfxB.rectTransform.localRotation = Quaternion.identity;
                    v.VfxB.sprite = glowSpr;

                    // VfxC: secondary crack — right of center, shorter, slightly offset
                    v.VfxC.rectTransform.anchorMin = v.VfxC.rectTransform.anchorMax = new Vector2(0.66f, 0.5f);
                    v.VfxC.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    v.VfxC.rectTransform.sizeDelta = new Vector2(6f, h * 0.52f);
                    v.VfxC.rectTransform.anchoredPosition = new Vector2(0f, h * 0.06f);
                    v.VfxC.rectTransform.localRotation = Quaternion.identity;
                    v.VfxC.sprite = glowSpr;

                    // VfxD: ember drip — spark that falls from crack top to base pool
                    if (v.VfxD != null)
                    {
                        v.VfxD.rectTransform.anchorMin = v.VfxD.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        v.VfxD.rectTransform.pivot = new Vector2(0.5f, 0.5f);
                        v.VfxD.rectTransform.sizeDelta = new Vector2(6f, 6f);
                        v.VfxD.rectTransform.anchoredPosition = new Vector2(-w * 0.15f, h * 0.22f);
                        v.VfxD.rectTransform.localRotation = Quaternion.identity;
                        v.VfxD.sprite = sparkSpr;
                    }

                    // VfxE: base lava pool — wide flat glow at feet of the unit
                    if (v.VfxE != null)
                    {
                        v.VfxE.rectTransform.anchorMin = v.VfxE.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                        v.VfxE.rectTransform.pivot = new Vector2(0.5f, 0f);
                        v.VfxE.rectTransform.sizeDelta = new Vector2(w * 1.15f, h * 0.30f);
                        v.VfxE.rectTransform.anchoredPosition = Vector2.zero;
                        v.VfxE.sprite = glowSpr;
                    }
                    break;
                }

                case NeonCosmetics.TroopSkin.Phantom:
                {
                    float off     = v.IsPlayer ? -14f : 14f;
                    var glowSpr   = NeonArt.GlowCircle();
                    var flameSpr  = NeonArt.FlameLick();

                    // VfxA: ghostly spectral tint over body
                    v.VfxA.rectTransform.anchorMin = Vector2.zero;
                    v.VfxA.rectTransform.anchorMax = Vector2.one;
                    v.VfxA.rectTransform.offsetMin = v.VfxA.rectTransform.offsetMax = Vector2.zero;
                    v.VfxA.sprite = glowSpr;

                    // VfxB: first shadow echo — full-size glow silhouette behind unit
                    v.VfxB.rectTransform.anchorMin = v.VfxB.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    v.VfxB.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
                    v.VfxB.rectTransform.sizeDelta = v.Rt.sizeDelta * 1.05f;
                    v.VfxB.rectTransform.anchoredPosition = new Vector2(off, 0f);
                    v.VfxB.sprite = glowSpr;

                    // VfxC: second shadow echo — slightly smaller, farther behind
                    v.VfxC.rectTransform.anchorMin = v.VfxC.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    v.VfxC.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
                    v.VfxC.rectTransform.sizeDelta = v.Rt.sizeDelta * 0.82f;
                    v.VfxC.rectTransform.anchoredPosition = new Vector2(off * 1.9f, 0f);
                    v.VfxC.sprite = glowSpr;

                    // VfxD: shadow wisp flame rising from head upward
                    if (v.VfxD != null)
                    {
                        v.VfxD.rectTransform.anchorMin = v.VfxD.rectTransform.anchorMax = new Vector2(0.5f, 1f);
                        v.VfxD.rectTransform.pivot     = new Vector2(0.5f, 0f);
                        v.VfxD.rectTransform.sizeDelta = new Vector2(w * 0.55f, h * 0.85f);
                        v.VfxD.rectTransform.anchoredPosition = Vector2.zero;
                        v.VfxD.sprite = flameSpr;
                    }

                    // VfxE: shadow pool spreading at feet
                    if (v.VfxE != null)
                    {
                        v.VfxE.rectTransform.anchorMin = v.VfxE.rectTransform.anchorMax = new Vector2(0.5f, 0f);
                        v.VfxE.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
                        v.VfxE.rectTransform.sizeDelta = new Vector2(w * 1.45f, h * 0.30f);
                        v.VfxE.rectTransform.anchoredPosition = Vector2.zero;
                        v.VfxE.sprite = glowSpr;
                    }
                    break;
                }

                case NeonCosmetics.TroopSkin.Cosmic:
                {
                    var glowSpr = NeonArt.GlowCircle();

                    // VfxA: deep cosmic nebula body glow
                    v.VfxA.rectTransform.anchorMin = Vector2.zero;
                    v.VfxA.rectTransform.anchorMax = Vector2.one;
                    v.VfxA.rectTransform.offsetMin = new Vector2(-7f, -7f);
                    v.VfxA.rectTransform.offsetMax = new Vector2(7f, 7f);
                    v.VfxA.sprite = glowSpr;

                    // VfxB: outer orbital ellipse (wide, flattened — fakes a tilted ring)
                    v.VfxB.rectTransform.anchorMin = v.VfxB.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    v.VfxB.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
                    v.VfxB.rectTransform.sizeDelta = new Vector2(w * 1.8f, h * 0.44f);
                    v.VfxB.sprite = glowSpr;

                    // VfxC: inner orbital ellipse (narrower, counterphase)
                    v.VfxC.rectTransform.anchorMin = v.VfxC.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    v.VfxC.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
                    v.VfxC.rectTransform.sizeDelta = new Vector2(w * 1.3f, h * 0.28f);
                    v.VfxC.sprite = glowSpr;

                    // VfxD: small orbiting star dot
                    if (v.VfxD != null)
                    {
                        v.VfxD.rectTransform.anchorMin = v.VfxD.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                        v.VfxD.rectTransform.pivot     = new Vector2(0.5f, 0.5f);
                        v.VfxD.rectTransform.sizeDelta = new Vector2(9f, 9f);
                        v.VfxD.sprite = glowSpr;
                    }

                    // VfxE: large outer cosmic aura
                    if (v.VfxE != null)
                    {
                        v.VfxE.rectTransform.anchorMin = Vector2.zero;
                        v.VfxE.rectTransform.anchorMax = Vector2.one;
                        v.VfxE.rectTransform.offsetMin = new Vector2(-15f, -15f);
                        v.VfxE.rectTransform.offsetMax = new Vector2(15f, 15f);
                        v.VfxE.sprite = glowSpr;
                    }
                    break;
                }
            }
        }

        void AnimateSkinVfx(UnitView v)
        {
            float h = v.Rt.sizeDelta.y;
            float w = v.Rt.sizeDelta.x;
            var theme = NeonTheme.Active;

            switch ((NeonCosmetics.TroopSkin)v.ActiveSkin)
            {
                case NeonCosmetics.TroopSkin.Golden:
                {
                    v.VfxPhase += Time.deltaTime * 2.5f;
                    float fp = v.VfxPhase;

                    // Ambient body shimmer — gentle gold glow
                    float bodyA = 0.12f + 0.10f * Mathf.Abs(Mathf.Sin(fp * 0.9f));
                    v.VfxA.color = new Color(1f, 0.78f, 0.08f, bodyA);

                    // Left flame — fast flicker toward white-hot on peaks
                    float fa1 = 0.52f + 0.34f * Mathf.Abs(Mathf.Sin(fp * 3.1f));
                    float sz1 = 1f + 0.14f * Mathf.Sin(fp * 4.4f);
                    v.VfxB.rectTransform.sizeDelta = new Vector2(w * 0.55f * sz1, h * 1.15f * sz1);
                    Color fc1 = Color.Lerp(new Color(1f, 0.70f, 0.08f), new Color(1f, 0.96f, 0.55f),
                                           Mathf.Abs(Mathf.Sin(fp * 7.2f)));
                    v.VfxB.color = new Color(fc1.r, fc1.g, fc1.b, fa1);

                    // Right flame — out of phase, cools to deeper orange
                    float fa2 = 0.40f + 0.28f * Mathf.Abs(Mathf.Sin(fp * 2.7f + 1.1f));
                    float sz2 = 1f + 0.11f * Mathf.Sin(fp * 3.8f + 0.8f);
                    v.VfxC.rectTransform.sizeDelta = new Vector2(w * 0.42f * sz2, h * 0.92f * sz2);
                    Color fc2 = Color.Lerp(new Color(1f, 0.55f, 0.04f), new Color(1f, 0.88f, 0.38f),
                                           Mathf.Abs(Mathf.Sin(fp * 5.5f + 0.4f)));
                    v.VfxC.color = new Color(fc2.r, fc2.g, fc2.b, fa2);

                    // Crown glow — sharp hot flash at unit top
                    if (v.VfxD != null)
                    {
                        float crA = Mathf.Max(0f, Mathf.Sin(fp * 5.8f)) * Mathf.Max(0f, Mathf.Sin(fp * 3.1f + 0.3f));
                        float crSz = 12f + 9f * crA;
                        v.VfxD.rectTransform.sizeDelta = new Vector2(crSz, crSz);
                        v.VfxD.color = new Color(1f, 0.94f, 0.45f, 0.40f + 0.52f * crA);
                    }

                    // Base disc — slow golden pulse
                    if (v.VfxE != null)
                    {
                        float baseA = 0.25f + 0.18f * Mathf.Abs(Mathf.Sin(fp * 0.55f + 0.4f));
                        v.VfxE.color = new Color(1f, 0.72f, 0.08f, baseA);
                    }
                    break;
                }

                case NeonCosmetics.TroopSkin.Chrome:
                {
                    v.VfxPhase += Time.deltaTime * 0.78f;

                    // Subtle metallic sheen — sweeping stripes carry the effect, not body tint
                    float baseA = 0.10f + 0.08f * Mathf.Sin(v.VfxPhase * 1.5f);
                    v.VfxA.color = new Color(0.88f, 0.94f, 1f, baseA);

                    // Primary shine sweep — wide bright streak
                    float ty1 = Mathf.PingPong(v.VfxPhase, 1f);
                    float y1  = -h * 0.5f + ty1 * h;
                    v.VfxB.rectTransform.anchoredPosition = new Vector2(0f, y1);
                    v.VfxB.rectTransform.sizeDelta = new Vector2(w + 8f, 6f); // wider stripe (was 3.5px)
                    v.VfxB.color = new Color(1f, 1f, 1f, Mathf.Sin(ty1 * Mathf.PI) * 0.85f); // was 0.65

                    // Secondary shine sweep (offset phase)
                    float ty2 = Mathf.PingPong(v.VfxPhase + 0.38f, 1f);
                    float y2  = -h * 0.5f + ty2 * h;
                    v.VfxC.rectTransform.anchoredPosition = new Vector2(0f, y2);
                    v.VfxC.rectTransform.sizeDelta = new Vector2(w * 0.62f, 3f); // was 2px
                    v.VfxC.color = new Color(0.72f, 0.86f, 1f, Mathf.Sin(ty2 * Mathf.PI) * 0.60f); // was 0.38

                    if (v.VfxD != null) v.VfxD.color = Color.clear;
                    if (v.VfxE != null) v.VfxE.color = Color.clear;
                    break;
                }

                case NeonCosmetics.TroopSkin.Inferno:
                {
                    v.VfxPhase += Time.deltaTime * 3.5f;
                    float fp = v.VfxPhase;

                    // Full-body heat aura — breathing orange-red shimmer
                    float heatA = 0.22f + 0.16f * Mathf.Sin(fp * 1.8f);
                    v.VfxA.color = new Color(1f, 0.26f, 0.02f, heatA);

                    // Lava surge pulse — occasionally flashes white-hot (simulates pressure)
                    float surgeB = Mathf.Max(0f, Mathf.Sin(fp * 2.3f)) * Mathf.Max(0f, Mathf.Sin(fp * 7.1f));
                    float surgeC = Mathf.Max(0f, Mathf.Sin(fp * 1.9f + 1.4f)) * Mathf.Max(0f, Mathf.Sin(fp * 5.8f + 2.1f));

                    // Primary crack — yellow-orange with occasional white-hot surge
                    float crAB = 0.72f + 0.24f * Mathf.Sin(fp * 2.3f) + 0.22f * surgeB;
                    Color crBCol = Color.Lerp(new Color(1f, 0.70f, 0.10f), new Color(1f, 0.96f, 0.80f), surgeB * 1.2f);
                    v.VfxB.color = new Color(crBCol.r, crBCol.g, crBCol.b, Mathf.Clamp01(crAB));

                    // Secondary crack — cooler orange, out of phase, surges less
                    float crAC = 0.58f + 0.30f * Mathf.Sin(fp * 1.9f + 1.4f) + 0.16f * surgeC;
                    Color crCCol = Color.Lerp(new Color(1f, 0.46f, 0.06f), new Color(1f, 0.82f, 0.40f), surgeC);
                    v.VfxC.color = new Color(crCCol.r, crCCol.g, crCCol.b, Mathf.Clamp01(crAC));

                    // Molten drip — falls from primary crack, elongates and cools as it falls
                    if (v.VfxD != null)
                    {
                        float dripCycle = (fp * 1.4f) % 1f;
                        float dripY = Mathf.Lerp(h * 0.24f, -h * 0.46f, dripCycle);
                        // Elongates as it falls (molten drop stretches under gravity)
                        float dripW = 7f - 3f * dripCycle;
                        float dripH = 7f + 12f * dripCycle;
                        v.VfxD.rectTransform.anchoredPosition = new Vector2(-w * 0.16f, dripY);
                        v.VfxD.rectTransform.sizeDelta = new Vector2(dripW, dripH);
                        float dripA = 0.92f * Mathf.Sin(dripCycle * Mathf.PI);
                        // Cools from yellow-white at crack exit to deep red at base
                        Color dripCol = Color.Lerp(new Color(1f, 0.92f, 0.35f), new Color(0.85f, 0.18f, 0.01f), dripCycle);
                        v.VfxD.color = new Color(dripCol.r, dripCol.g, dripCol.b, dripA);
                    }

                    // Base lava pool — hot glow at feet, bubbles with rapid micro-pulses
                    if (v.VfxE != null)
                    {
                        float bubble = Mathf.Max(0f, Mathf.Sin(fp * 5.8f)) * Mathf.Max(0f, Mathf.Sin(fp * 8.3f + 1.2f));
                        float poolA = 0.40f + 0.22f * Mathf.Sin(fp * 1.4f + 0.5f) + 0.18f * bubble;
                        v.VfxE.color = new Color(1f, 0.22f, 0.02f, Mathf.Clamp01(poolA));
                    }
                    break;
                }

                case NeonCosmetics.TroopSkin.Phantom:
                {
                    v.VfxPhase += Time.deltaTime;
                    float fp        = v.VfxPhase;
                    float pulse     = Mathf.Sin(fp * 2.0f);
                    float slowPulse = Mathf.Sin(fp * 0.78f);

                    Color ghostBase = Color.Lerp(new Color(0.65f, 0.40f, 1f), theme.Accent, 0.28f);
                    Color trailCol1 = Color.Lerp(new Color(0.55f, 0.30f, 0.95f), theme.Accent, 0.22f);
                    Color trailCol2 = Color.Lerp(new Color(0.45f, 0.22f, 0.80f), theme.Accent, 0.18f);

                    // Ghostly body tint
                    v.VfxA.color = new Color(ghostBase.r, ghostBase.g, ghostBase.b,
                                             0.16f + 0.08f * pulse);

                    // First shadow echo
                    v.VfxB.color = new Color(trailCol1.r, trailCol1.g, trailCol1.b,
                                             0.52f + 0.13f * pulse);

                    // Second shadow echo
                    v.VfxC.color = new Color(trailCol2.r, trailCol2.g, trailCol2.b,
                                             0.32f + 0.13f * Mathf.Abs(slowPulse));

                    // Shadow wisp flame rising from head — flicker width + height with sizeDelta
                    if (v.VfxD != null)
                    {
                        float swayX  = 1f + 0.12f * Mathf.Sin(fp * 4.8f);
                        float swayY  = 1f + 0.18f * Mathf.Abs(Mathf.Sin(fp * 3.2f));
                        v.VfxD.rectTransform.sizeDelta = new Vector2(w * 0.55f * swayX, h * 0.85f * swayY);
                        float wispA  = 0.45f + 0.28f * Mathf.Abs(Mathf.Sin(fp * 2.9f));
                        Color wispCol = Color.Lerp(new Color(0.28f, 0.05f, 0.55f), new Color(0.55f, 0.20f, 0.90f),
                                                   Mathf.Abs(Mathf.Sin(fp * 1.8f)));
                        v.VfxD.color = new Color(wispCol.r, wispCol.g, wispCol.b, wispA);
                    }

                    // Shadow pool at feet — dark spreading pulse
                    if (v.VfxE != null)
                    {
                        float poolScale = 1f + 0.10f * Mathf.Abs(slowPulse);
                        v.VfxE.rectTransform.sizeDelta = new Vector2(w * 1.45f * poolScale, h * 0.30f);
                        float poolA = 0.35f + 0.18f * Mathf.Abs(slowPulse);
                        v.VfxE.color = new Color(0.20f, 0.04f, 0.45f, poolA);
                    }
                    break;
                }

                case NeonCosmetics.TroopSkin.Cosmic:
                {
                    v.VfxPhase += Time.deltaTime * 1.4f;
                    float fp = v.VfxPhase;

                    // Nebula body glow — slow deep indigo breathe
                    float nebulaA = 0.20f + 0.12f * Mathf.Abs(Mathf.Sin(fp * 0.85f));
                    v.VfxA.color = new Color(0.30f, 0.10f, 0.72f, nebulaA);

                    // Outer orbital ellipse — width breathes (fakes ring tilt oscillation)
                    float outerPulse = 1f + 0.10f * Mathf.Sin(fp * 1.1f);
                    v.VfxB.rectTransform.sizeDelta = new Vector2(w * 1.8f * outerPulse, h * 0.44f);
                    float outerA = 0.38f + 0.18f * Mathf.Abs(Mathf.Sin(fp * 1.1f));
                    Color outerCol = Color.Lerp(new Color(0.45f, 0.18f, 1.0f), new Color(0.70f, 0.40f, 1.0f),
                                                Mathf.Abs(Mathf.Sin(fp * 0.7f)));
                    v.VfxB.color = new Color(outerCol.r, outerCol.g, outerCol.b, outerA);

                    // Inner orbital ellipse — counterphase
                    float innerPulse = 1f + 0.10f * Mathf.Sin(fp * 1.1f + Mathf.PI);
                    v.VfxC.rectTransform.sizeDelta = new Vector2(w * 1.3f * innerPulse, h * 0.28f);
                    float innerA = 0.28f + 0.15f * Mathf.Abs(Mathf.Sin(fp * 1.1f + Mathf.PI));
                    v.VfxC.color = new Color(0.60f, 0.30f, 1.0f, innerA);

                    // Orbiting star dot — elliptical orbit around center
                    if (v.VfxD != null)
                    {
                        float orbitAngle = fp * 1.8f;
                        float orbitX = Mathf.Cos(orbitAngle) * w * 0.82f;
                        float orbitY = Mathf.Sin(orbitAngle) * h * 0.20f;
                        v.VfxD.rectTransform.anchoredPosition = new Vector2(orbitX, orbitY);
                        float starA = 0.72f + 0.24f * Mathf.Abs(Mathf.Sin(orbitAngle * 2f));
                        v.VfxD.color = new Color(0.88f, 0.72f, 1.0f, starA);
                    }

                    // Outer cosmic aura — slow deep pulse
                    if (v.VfxE != null)
                    {
                        float auraA = 0.12f + 0.10f * Mathf.Abs(Mathf.Sin(fp * 0.65f));
                        float ep    = 15f + 5f * Mathf.Abs(Mathf.Sin(fp * 0.65f));
                        v.VfxE.color = new Color(0.35f, 0.08f, 0.80f, auraA);
                        v.VfxE.rectTransform.offsetMin = new Vector2(-ep, -ep);
                        v.VfxE.rectTransform.offsetMax = new Vector2(ep, ep);
                    }
                    break;
                }
            }

            // SkinGlow — independent aura; overrides VfxE for player units when active
            if (v.IsPlayer && v.VfxE != null)
            {
                var sg = NeonCosmetics.ActiveSkinGlow;
                if (sg != NeonCosmetics.SkinGlow.None)
                {
                    if (v.ActiveSkin == 0) v.VfxPhase += Time.deltaTime; // Default skin doesn't increment above
                    Color gc = NeonCosmetics.GetSkinGlowColor();
                    float gp = v.VfxPhase;
                    float ga = sg == NeonCosmetics.SkinGlow.Soft
                                 ? 0.25f + 0.12f * Mathf.Abs(Mathf.Sin(gp * 1.5f))
                             : sg == NeonCosmetics.SkinGlow.Intense
                                 ? 0.55f + 0.18f * Mathf.Abs(Mathf.Sin(gp * 2.0f))
                             :   Mathf.Abs(Mathf.Sin(gp * 3.2f)) * 0.70f;
                    float ep = sg == NeonCosmetics.SkinGlow.Soft
                                 ? 14f + 6f  * Mathf.Abs(Mathf.Sin(gp * 1.5f))
                             : sg == NeonCosmetics.SkinGlow.Intense
                                 ? 20f + 8f  * Mathf.Abs(Mathf.Sin(gp * 2.0f))
                             :   12f + 14f * Mathf.Abs(Mathf.Sin(gp * 3.2f));
                    v.VfxE.color  = new Color(gc.r, gc.g, gc.b, ga);
                    v.VfxE.sprite = NeonArt.GlowCircle();
                    v.VfxE.rectTransform.anchorMin = Vector2.zero;
                    v.VfxE.rectTransform.anchorMax = Vector2.one;
                    v.VfxE.rectTransform.offsetMin = new Vector2(-ep, -ep);
                    v.VfxE.rectTransform.offsetMax = new Vector2(ep,  ep);
                }
            }
        }

        // ─────────────────────────────── core combat ─────────────────────────

        // Fires a "retaliation" projectile from the core edge partway into the attacking lane.
        // fromRight=true  → enemy core (right side) fires back at player units
        // fromRight=false → player core (left side) fires back at enemy units
        IEnumerator CoreReturnFire(int laneIdx, bool fromRight, float delay)
        {
            yield return new WaitForSeconds(delay);
            if (laneIdx < 0 || laneIdx >= _laneRects.Length || _laneRects[laneIdx] == null) yield break;
            var lane = _laneRects[laneIdx];
            float lw = lane.rect.width;
            if (lw <= 0f) yield break;

            float originX = fromRight ? lw       : 0f;
            float destX   = fromRight ? lw * 0.68f : lw * 0.32f;
            float yOff    = Random.Range(-9f, 9f);
            Color col     = fromRight ? EnemySolid : PlayerSolid;

            for (int shot = 0; shot < 2; shot++)
            {
                float yJitter = Random.Range(-5f, 5f);
                StartCoroutine(ProjectileShot(
                    new Vector2(originX, yOff),
                    new Vector2(destX,   yOff + yJitter),
                    lane, col));
                yield return new WaitForSeconds(0.10f);
            }
        }
    }
}
