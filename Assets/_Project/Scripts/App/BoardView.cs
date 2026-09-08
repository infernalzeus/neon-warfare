using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using NW.Board.Domain;

namespace NW.App
{
    /// <summary>
    /// Dynamic match-3 grid. Size is determined at runtime from BoardModel (cols × 8 rows).
    /// Cell pixel size is computed to fill the available container height while keeping cells square.
    /// Supports 8–16 columns for level-based board expansion.
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        const float GAP        = 4f;
        const float SWAP_DUR   = 0.18f;
        const float CLEAR_DUR  = 0.22f;
        const float ANIM_TIMEOUT = 3f;

        static readonly Color[] GemColor =
        {
            new Color(1.00f, 0.85f, 0.10f),
            new Color(0.90f, 0.10f, 0.80f),
            new Color(0.10f, 0.90f, 0.40f),
            new Color(0.10f, 0.70f, 1.00f),
            new Color(0.85f, 0.85f, 0.90f),
        };

        static Color SpecialColor(SpecialKind s) => s switch
        {
            SpecialKind.LaserH      => new Color(1f, 0.95f, 0.1f),
            SpecialKind.LaserV      => new Color(0.1f, 0.85f, 1f),
            SpecialKind.Cross       => new Color(1f, 0.35f, 0.05f),
            SpecialKind.Singularity => new Color(0.7f, 0.1f, 1f),
            _                       => Color.white,
        };

        // ── per-cell drag handler ───────────────────────────────────────────────
        sealed class CellDragHandler : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
        {
            internal int CX, CY;
            internal System.Action<int, int, int, int> OnDragSwap;
            bool _fired;
            public void OnBeginDrag(PointerEventData e) => _fired = false;
            public void OnDrag(PointerEventData e)
            {
                if (_fired) return;
                Vector2 d = e.position - e.pressPosition;
                if (d.magnitude < 12f) return;
                _fired = true;
                int tx = CX, ty = CY;
                if (Mathf.Abs(d.x) >= Mathf.Abs(d.y)) tx += d.x > 0 ? 1 : -1;
                else                                    ty += d.y > 0 ? -1 : 1;
                OnDragSwap?.Invoke(CX, CY, tx, ty);
            }
            public void OnEndDrag(PointerEventData e) => _fired = false;
        }

        // ── state ───────────────────────────────────────────────────────────────
        BattleSession _session;
        RectTransform _container;
        Font          _font;

        int   _cols, _rows;
        float _cell;   // computed square cell size in pixels

        RawImage[,] _cells;
        RawImage[,] _borders;
        Image[,]    _overlays;

        int   _selX = -1, _selY = -1;
        bool  _animating;
        float _animatingTimer;
        int   _lastBoardSkin = -1;
        readonly Queue<GameObject> _floaterPool = new();

        // ── init ────────────────────────────────────────────────────────────────

        public void Init(BattleSession session, RectTransform container, Font font,
                         float availW = 0f, float availH = 0f)
        {
            _session   = session;
            _container = container;
            _font      = font;
            _cols      = session.Board.Cols;
            _rows      = session.Board.Rows;

            // Compute square cell size from available space.
            // availW/availH come from BattleScene at reference resolution.
            // Fallback: use container sizeDelta if not provided.
            if (availW <= 0f) availW = Mathf.Abs(container.sizeDelta.x) + 1920f * (container.anchorMax.x - container.anchorMin.x);
            if (availH <= 0f) availH = container.sizeDelta.y > 0f ? container.sizeDelta.y : 460f;

            const float PAD_H = 36f;  // title + top/bottom padding
            const float PAD_W = 24f;  // side padding
            float usableH = availH - PAD_H;
            float usableW = availW - PAD_W;
            float cellByH = (usableH - (_rows - 1) * GAP) / _rows;
            float cellByW = (usableW - (_cols - 1) * GAP) / _cols;
            _cell = Mathf.Floor(Mathf.Min(cellByH, cellByW));
            _cell = Mathf.Clamp(_cell, 24f, 88f);

            _cells   = new RawImage[_cols, _rows];
            _borders = new RawImage[_cols, _rows];
            _overlays = new Image[_cols, _rows];

            float step   = _cell + GAP;
            float totalW = _cols * step - GAP;
            float totalH = _rows * step - GAP;

            // Board title
            var titleGo = new GameObject("BoardTitle");
            titleGo.transform.SetParent(container, false);
            var titleRt = titleGo.AddComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0, 1); titleRt.anchorMax = new Vector2(1, 1);
            titleRt.pivot = new Vector2(0.5f, 1);
            titleRt.anchoredPosition = new Vector2(0, -1);
            titleRt.sizeDelta = new Vector2(0, 18);
            var titleTxt = titleGo.AddComponent<Text>();
            titleTxt.font = font; titleTxt.fontSize = 12;
            titleTxt.color = new Color(0.4f, 0.7f, 1f);
            titleTxt.alignment = TextAnchor.MiddleCenter;
            titleTxt.text = $"COMPILER  ·  {_cols}×{_rows}  ·  MATCH ROWS TO STRIKE LANES";
            titleTxt.supportRichText = false;

            // Grid cells — anchored to container center
            for (int x = 0; x < _cols; x++)
            {
                for (int y = 0; y < _rows; y++)
                {
                    int cx = x, cy = y;

                    var go = new GameObject($"Cell{x}_{y}");
                    go.transform.SetParent(container, false);

                    var rt = go.AddComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    float px = -totalW * 0.5f + x * step + _cell * 0.5f;
                    float py = -totalH * 0.5f + (_rows - 1 - y) * step + _cell * 0.5f;
                    rt.anchoredPosition = new Vector2(px, py);
                    rt.sizeDelta = new Vector2(_cell, _cell);

                    var img = go.AddComponent<RawImage>();
                    img.color = Color.white;
                    _cells[x, y] = img;

                    // Special border overlay (full cell, transparent center)
                    var brdGo = new GameObject("Brd");
                    brdGo.transform.SetParent(go.transform, false);
                    var brdRt = brdGo.AddComponent<RectTransform>();
                    brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
                    brdRt.offsetMin = brdRt.offsetMax = Vector2.zero;
                    var brdImg = brdGo.AddComponent<RawImage>();
                    brdImg.raycastTarget = false;
                    brdImg.color = Color.clear;
                    _borders[x, y] = brdImg;

                    // Selection highlight
                    var ovGo = new GameObject("Ov");
                    ovGo.transform.SetParent(go.transform, false);
                    var ovRt = ovGo.AddComponent<RectTransform>();
                    ovRt.anchorMin = new Vector2(0.1f, 0.1f);
                    ovRt.anchorMax = new Vector2(0.9f, 0.9f);
                    ovRt.offsetMin = ovRt.offsetMax = Vector2.zero;
                    var ovImg = ovGo.AddComponent<Image>();
                    ovImg.color = new Color(0, 0, 0, 0);
                    _overlays[x, y] = ovImg;

                    var drag = go.AddComponent<CellDragHandler>();
                    drag.CX = cx; drag.CY = cy;
                    drag.OnDragSwap = OnDragSwap;

                    var btn = go.AddComponent<Button>();
                    btn.transition = Selectable.Transition.None;
                    btn.onClick.AddListener(() => OnCellClick(cx, cy));
                }
            }

            RefreshImmediate();
            StartCoroutine(EntryCascade());
        }

        // Battle-start flourish: every column of gems rains in from above the frame,
        // left to right, landing with the same gravity used for match falls.
        private IEnumerator EntryCascade()
        {
            _animating = true;
            float step = _cell + GAP;
            float topEdgeY = (_rows * step - GAP) * 0.5f;
            float dropPx = (_rows + 1) * step;
            var basePos = new Vector2[_cols, _rows];
            for (int x = 0; x < _cols; x++)
                for (int y = 0; y < _rows; y++)
                {
                    basePos[x, y] = _cells[x, y].rectTransform.anchoredPosition;
                    _cells[x, y].rectTransform.anchoredPosition = basePos[x, y] + new Vector2(0f, dropPx);
                    _cells[x, y].enabled = false;
                }

            const float DUR = 0.34f;
            const float STAG = 0.05f;
            float total = DUR + STAG * (_cols - 1) + 0.05f;
            for (float t = 0f; t < total;)
            {
                t = Mathf.Min(t + Time.deltaTime, total);
                for (int x = 0; x < _cols; x++)
                {
                    float p = Mathf.Clamp01((t - x * STAG) / DUR);
                    float e = p * p;
                    for (int y = 0; y < _rows; y++)
                    {
                        var rt = _cells[x, y].rectTransform;
                        rt.anchoredPosition = basePos[x, y] + new Vector2(0f, (1f - e) * dropPx);
                        _cells[x, y].enabled = rt.anchoredPosition.y <= topEdgeY + _cell * 0.45f;
                    }
                }
                yield return null;
            }
            for (int x = 0; x < _cols; x++)
                for (int y = 0; y < _rows; y++)
                {
                    _cells[x, y].rectTransform.anchoredPosition = basePos[x, y];
                    _cells[x, y].enabled = true;
                }
            AudioManager.Play(AudioManager.Sfx.GemSwap, 0.5f, 0.8f);
            _animating = false;
        }

        // ── input ───────────────────────────────────────────────────────────────

        private void Update()
        {
            if (_cells == null) return;

            if (_animating)
            {
                _animatingTimer += Time.deltaTime;
                if (_animatingTimer >= ANIM_TIMEOUT)
                {
                    _animating = false;
                    _animatingTimer = 0f;
                    RefreshImmediate();
                }
            }
            else _animatingTimer = 0f;

            // Board skin change detection — triggers immediate cell texture refresh
            int curSkin = (int)NeonCosmetics.ActiveBoardSkin == 0
                ? 4 + NeonTheme.Active.BoardSkin
                : (int)NeonCosmetics.ActiveBoardSkin;
            if (curSkin != _lastBoardSkin)
            {
                _lastBoardSkin = curSkin;
                RefreshImmediate();
            }

            // Pulse selected gem overlay
            if (_selX >= 0 && _selY >= 0 && _overlays != null)
            {
                float selPulse = 0.22f + Mathf.Abs(Mathf.Sin(Time.time * 5.5f)) * 0.20f;
                _overlays[_selX, _selY].color = new Color(1f, 1f, 1f, selPulse);
            }

            // Pulse special-tile borders — absolute time keeps it smooth at any frame rate
            float pulse = 0.68f + Mathf.Sin(Time.time * 3.2f) * 0.32f;
            for (int x = 0; x < _cols; x++)
                for (int y = 0; y < _rows; y++)
                    if (_borders[x, y] != null && _borders[x, y].texture != null)
                        _borders[x, y].color = new Color(1f, 1f, 1f, pulse);
        }

        private void OnDragSwap(int sx, int sy, int tx, int ty)
        {
            if (_session == null || _animating || _session.Combat.Finished) return;
            if (tx < 0 || tx >= _cols || ty < 0 || ty >= _rows) return;
            if (Mathf.Abs(sx - tx) + Mathf.Abs(sy - ty) != 1) return;
            _selX = _selY = -1;
            AudioManager.Play(AudioManager.Sfx.GemSwap, 1f, Random.Range(0.93f, 1.07f));
            StartCoroutine(DoSwap(sx, sy, tx, ty));
        }

        private void OnCellClick(int x, int y)
        {
            if (_session == null || _session.Combat.Finished || _animating) return;

            if (_selX < 0)
            {
                _selX = x; _selY = y;
                AudioManager.Play(AudioManager.Sfx.Click);
                RefreshImmediate();
                return;
            }
            if (_selX == x && _selY == y)
            {
                _selX = _selY = -1;
                RefreshImmediate();
                return;
            }

            int ax = _selX, ay = _selY;
            _selX = _selY = -1;

            if (Mathf.Abs(ax - x) + Mathf.Abs(ay - y) != 1)
            {
                _selX = x; _selY = y;
                RefreshImmediate();
                return;
            }

            AudioManager.Play(AudioManager.Sfx.GemSwap);
            StartCoroutine(DoSwap(ax, ay, x, y));
        }

        // ── animation ───────────────────────────────────────────────────────────

        private IEnumerator DoSwap(int ax, int ay, int bx, int by)
        {
            _animating = true;
            _animatingTimer = 0f;

            var rtA = _cells[ax, ay].rectTransform;
            var rtB = _cells[bx, by].rectTransform;
            Vector2 posA = rtA.anchoredPosition;
            Vector2 posB = rtB.anchoredPosition;

            for (float t = 0f; t < SWAP_DUR;)
            {
                t = Mathf.Min(t + Time.deltaTime, SWAP_DUR);
                float e = EaseOut(t / SWAP_DUR);
                rtA.anchoredPosition = Vector2.Lerp(posA, posB, e);
                rtB.anchoredPosition = Vector2.Lerp(posB, posA, e);
                yield return null;
            }

            var result = _session.TrySwap(ax, ay, bx, by);

            rtA.anchoredPosition = posA;
            rtB.anchoredPosition = posB;

            if (!result.Valid)
            {
                for (float t = 0f; t < SWAP_DUR;)
                {
                    t = Mathf.Min(t + Time.deltaTime, SWAP_DUR);
                    float e = EaseOut(t / SWAP_DUR);
                    rtA.anchoredPosition = Vector2.Lerp(posB, posA, e);
                    rtB.anchoredPosition = Vector2.Lerp(posA, posB, e);
                    yield return null;
                }
                rtA.anchoredPosition = posA;
                rtB.anchoredPosition = posB;
                AudioManager.Play(AudioManager.Sfx.GemInvalid);
                var cA = _cells[ax, ay]; var cB = _cells[bx, by];
                Color red = new Color(1f, 0.22f, 0.12f);
                Tween.Tint(cA, Color.white, red, 0.07f, Tween.Ease.EaseOut);
                Tween.Tint(cB, Color.white, red, 0.07f, Tween.Ease.EaseOut);
                Tween.Delay(0.07f, () =>
                {
                    Tween.Tint(cA, red, Color.white, 0.18f, Tween.Ease.EaseOut);
                    Tween.Tint(cB, red, Color.white, 0.18f, Tween.Ease.EaseOut);
                });
                Tween.ShakeH(rtA, 4f, 0.22f);
                Tween.ShakeH(rtB, 4f, 0.22f);
                _animating = false;
                yield break;
            }

            var cleared   = new List<(int x, int y, GemKind gem)>();
            var detonated = new List<(Cell pos, SpecialKind kind)>();
            bool empBurst = false;
            GemKind payGem = GemKind.Energy;
            int payTotal   = 0;
            bool anyMatch  = false;

            foreach (var evt in result.Tape)
            {
                switch (evt.Type)
                {
                    case BoardEventType.TileCleared:
                        cleared.Add((evt.A.X, evt.A.Y, evt.Gem));
                        break;
                    case BoardEventType.ResourcePayout when evt.Amount > 0:
                        payTotal += evt.Amount;
                        payGem    = evt.Gem;
                        anyMatch  = true;
                        break;
                    case BoardEventType.SpecialDetonated:
                        detonated.Add((evt.A, evt.Special));
                        break;
                    case BoardEventType.EmpTriggered:
                        empBurst = true;
                        break;
                }
            }

            if (anyMatch)
            {
                // Bigger clears ring higher — escalation you can hear
                float mPitch = 1f + Mathf.Clamp(cleared.Count - 3, 0, 8) * 0.05f;
                AudioManager.Play(cleared.Count >= 5 ? AudioManager.Sfx.GemMatchBig : AudioManager.Sfx.GemMatch,
                    1f, mPitch);
                if (cleared.Count >= 6)
                    SpawnFloater($"×{cleared.Count} CLEAR!", (posA + posB) * 0.5f + new Vector2(0f, 30f),
                        new Color(1f, 0.85f, 0.2f));
            }

            // Pop cleared gems out before board state updates
            if (cleared.Count > 0)
            {
                const float POP_OUT = 0.09f;
                for (float t = 0f; t < POP_OUT;)
                {
                    t = Mathf.Min(t + Time.deltaTime, POP_OUT);
                    float s = 1f - EaseOut(t / POP_OUT);
                    foreach (var (cx, cy, _) in cleared)
                        _cells[cx, cy].rectTransform.localScale = new Vector3(s, s, 1f);
                    yield return null;
                }
                // Was hardcoded to RingBurst, so the equipped trail was ignored entirely --
                // ActiveGemTrail was read by nothing in the whole project and every trail in
                // the shop was purchasable content that did nothing.
                foreach (var (cx, cy, gem) in cleared)
                    StartCoroutine(GemTrailBurst(_cells[cx, cy].rectTransform.anchoredPosition,
                                                 GemColor[(int)gem]));
            }

            RefreshImmediate();

            if (empBurst) StartCoroutine(EmpFlash());
            foreach (var (pos, kind) in detonated)
                StartCoroutine(SpecialDetonateVfx(pos.X, pos.Y, kind));

            if (cleared.Count > 0)
                yield return StartCoroutine(AnimateColumnFall(cleared));

            if (payTotal > 0)
                SpawnFloater($"+{payTotal}", (posA + posB) * 0.5f, GemColor[(int)payGem]);

            _animating = false;
        }

        // ── falling-gem choreography ─────────────────────────────────────────────
        // The model collapses columns downward (+Y down) and refills from the top.
        // Reconstruct per-cell drop distances from the cleared set, then animate every
        // moved gem falling into its slot under gravity, with a landing squash.

        private IEnumerator AnimateColumnFall(List<(int x, int y, GemKind gem)> cleared)
        {
            float step = _cell + GAP;

            // Cleared rows per column (dedup: cascade tapes can clear a coordinate twice)
            var holes = new HashSet<int>[_cols];
            foreach (var (cx, cy, _) in cleared)
                (holes[cx] ??= new HashSet<int>()).Add(cy);

            // Per-slot drop distance in cells (0 = didn't move)
            var drop = new int[_cols, _rows];
            for (int x = 0; x < _cols; x++)
            {
                var hs = holes[x];
                if (hs == null) continue;
                int nH = hs.Count;
                // Survivors keep order and stack to the bottom
                int newY = _rows - 1;
                for (int oldY = _rows - 1; oldY >= 0; oldY--)
                {
                    if (hs.Contains(oldY)) continue;
                    drop[x, newY] = newY - oldY;
                    newY--;
                }
                // Fresh gems descend as one block from above the top edge
                for (int y = 0; y <= newY; y++)
                    drop[x, y] = nH;
            }

            // Collect movers
            var mv = new List<(RectTransform rt, RawImage img, Vector2 basePos, int cells, float dur)>();
            float maxDur = 0f;
            float topEdgeY = (_rows * step - GAP) * 0.5f; // container-local top of the board
            for (int x = 0; x < _cols; x++)
                for (int y = 0; y < _rows; y++)
                {
                    if (drop[x, y] <= 0) continue;
                    var rt = _cells[x, y].rectTransform;
                    float dur = 0.11f + 0.055f * drop[x, y];
                    maxDur = Mathf.Max(maxDur, dur);
                    mv.Add((rt, _cells[x, y], rt.anchoredPosition, drop[x, y], dur));
                }
            if (mv.Count == 0) yield break;

            // Cleared slots were shrunk to 0 by the pop-out — their new occupants must be visible
            foreach (var m in mv) m.rt.localScale = Vector3.one;

            for (float t = 0f; t < maxDur;)
            {
                t = Mathf.Min(t + Time.deltaTime, maxDur);
                foreach (var m in mv)
                {
                    float p = Mathf.Clamp01(t / m.dur);
                    float e = p * p; // gravity: accelerate downward
                    float off = (1f - e) * m.cells * step;
                    m.rt.anchoredPosition = m.basePos + new Vector2(0f, off);
                    // Hide gems while they're still above the board frame
                    m.img.enabled = m.rt.anchoredPosition.y <= topEdgeY + _cell * 0.45f;
                }
                yield return null;
            }

            // Land: restore slots, soft tick, squash the long-fallers
            AudioManager.Play(AudioManager.Sfx.GemSwap, 0.45f, 0.82f);
            foreach (var m in mv)
            {
                m.rt.anchoredPosition = m.basePos;
                m.img.enabled = true;
            }
            const float SQ = 0.09f;
            for (float t = 0f; t < SQ;)
            {
                t = Mathf.Min(t + Time.deltaTime, SQ);
                float e = EaseOut(t / SQ);
                foreach (var m in mv)
                {
                    if (m.cells < 2) continue;
                    m.rt.localScale = new Vector3(Mathf.Lerp(1.12f, 1f, e), Mathf.Lerp(0.86f, 1f, e), 1f);
                }
                yield return null;
            }
            foreach (var m in mv) m.rt.localScale = Vector3.one;
        }

        // ── special detonation VFX ───────────────────────────────────────────────

        private IEnumerator SpecialDetonateVfx(int cx, int cy, SpecialKind kind)
        {
            Color col  = SpecialColor(kind);
            float step = _cell + GAP;
            float totalW = _cols * step - GAP, totalH = _rows * step - GAP;
            Vector2 cellPos = new(
                -totalW * 0.5f + cx * step + _cell * 0.5f,
                -totalH * 0.5f + (_rows - 1 - cy) * step + _cell * 0.5f);

            StartCoroutine(BigRingBurst(cellPos, col));

            switch (kind)
            {
                case SpecialKind.LaserH:    StartCoroutine(StripeFlash(cy, true,  col)); break;
                case SpecialKind.LaserV:    StartCoroutine(StripeFlash(cx, false, col)); break;
                case SpecialKind.Cross:
                    StartCoroutine(StripeFlash(cy, true,  col));
                    StartCoroutine(StripeFlash(cx, false, col));
                    break;
                case SpecialKind.Singularity: StartCoroutine(SingularityRipple(cellPos, col)); break;
            }
            yield break;
        }

        private IEnumerator BigRingBurst(Vector2 pos, Color col)
        {
            var go = new GameObject("bigring"); go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(_cell * 1.8f, _cell * 1.8f);
            var img = go.AddComponent<Image>(); img.raycastTarget = false;
            for (float t = 0f; t < 0.50f; t += Time.deltaTime)
            {
                float f = t / 0.50f;
                rt.sizeDelta = new Vector2(_cell * (1.8f + f * 3.5f), _cell * (1.8f + f * 3.5f));
                img.color = new Color(col.r, col.g, col.b, (1f - f) * 0.90f);
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator StripeFlash(int index, bool horizontal, Color col)
        {
            float step   = _cell + GAP;
            float totalW = _cols * step - GAP;
            float totalH = _rows * step - GAP;
            var go = new GameObject("stripe"); go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            if (horizontal)
            {
                float py = -totalH * 0.5f + (_rows - 1 - index) * step + _cell * 0.5f;
                rt.anchoredPosition = new Vector2(0f, py);
                rt.sizeDelta = new Vector2(totalW + _cell * 2f, _cell * 1.1f);
            }
            else
            {
                float px = -totalW * 0.5f + index * step + _cell * 0.5f;
                rt.anchoredPosition = new Vector2(px, 0f);
                rt.sizeDelta = new Vector2(_cell * 1.1f, totalH + _cell * 2f);
            }
            var img = go.AddComponent<Image>(); img.raycastTarget = false;
            for (float t = 0f; t < 0.40f; t += Time.deltaTime)
            {
                img.color = new Color(col.r, col.g, col.b, (1f - (t / 0.40f) * (t / 0.40f)) * 0.65f);
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator SingularityRipple(Vector2 pos, Color col)
        {
            float totalW = _cols * (_cell + GAP) - GAP;
            for (int ring = 0; ring < 3; ring++)
            {
                float delay = ring * 0.09f;
                for (float w = 0f; w < delay; w += Time.deltaTime) yield return null;

                var go = new GameObject("ripple"); go.transform.SetParent(_container, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = pos;
                rt.sizeDelta = new Vector2(_cell, _cell);
                var img = go.AddComponent<Image>(); img.raycastTarget = false;
                float maxSz = totalW * 1.6f;
                StartCoroutine(AnimRipple(go, rt, img, col, maxSz));
            }
        }

        private IEnumerator AnimRipple(GameObject go, RectTransform rt, Image img, Color col, float maxSz)
        {
            for (float t = 0f; t < 0.60f; t += Time.deltaTime)
            {
                float f = t / 0.60f;
                float sz = Mathf.Lerp(_cell, maxSz, EaseOut(f));
                rt.sizeDelta = new Vector2(sz, sz);
                img.color = new Color(col.r, col.g, col.b, (1f - f) * 0.60f);
                yield return null;
            }
            Destroy(go);
        }

        private IEnumerator EmpFlash()
        {
            var go = new GameObject("empflash"); go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(-0.1f, -0.1f); rt.anchorMax = new Vector2(1.1f, 1.1f);
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            var img = go.AddComponent<Image>(); img.raycastTarget = false;
            for (float t = 0f; t < 0.40f; t += Time.deltaTime)
            {
                img.color = new Color(0.8f, 0.4f, 1f, (1f - t / 0.40f) * 0.60f);
                yield return null;
            }
            Destroy(go);
        }

        /// <summary>Plays whichever trail is equipped. Ring is the original burst; the rest
        /// are the alternatives the armory sells.</summary>
        private IEnumerator GemTrailBurst(Vector2 pos, Color col)
        {
            switch (NeonCosmetics.ActiveGemTrail)
            {
                case NeonCosmetics.GemTrail.None:    yield break;
                case NeonCosmetics.GemTrail.Spark:   yield return TrailSpark(pos, col);   break;
                case NeonCosmetics.GemTrail.Pulse:   yield return TrailPulse(pos, col);   break;
                case NeonCosmetics.GemTrail.Shatter: yield return TrailShatter(pos, col); break;
                case NeonCosmetics.GemTrail.Bloom:   yield return TrailBloom(pos, col);   break;
                case NeonCosmetics.GemTrail.Cascade: yield return TrailCascade(pos, col); break;
                case NeonCosmetics.GemTrail.Implode: yield return TrailImplode(pos, col); break;
                default:                             yield return RingBurst(pos, col);    break;
            }
        }

        private Image TrailBit(Vector2 pos, float size, Color col)
        {
            var go = new GameObject("trail"); go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(size, size);
            var img = go.AddComponent<Image>();
            img.sprite = NeonArt.SoftDot();   // soft point of light, not a square
            img.color = col; img.raycastTarget = false;
            return img;
        }

        private IEnumerator TrailSpark(Vector2 pos, Color col)
        {
            const int N = 7;
            var bits = new Image[N]; var dir = new Vector2[N];
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2f / N + Random.Range(-0.2f, 0.2f);
                dir[i]  = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bits[i] = TrailBit(pos, _cell * 0.115f, col);
            }
            for (float t = 0f; t < 0.34f; t += Time.deltaTime)
            {
                float f = t / 0.34f;
                for (int i = 0; i < N; i++)
                {
                    if (bits[i] == null) continue;
                    bits[i].rectTransform.anchoredPosition = pos + dir[i] * (_cell * 0.9f * f);
                    bits[i].color = new Color(col.r, col.g, col.b, (1f - f) * 0.72f);
                }
                yield return null;
            }
            foreach (var b in bits) if (b) Destroy(b.gameObject);
        }

        private IEnumerator TrailPulse(Vector2 pos, Color col)
        {
            var a = TrailBit(pos, _cell, col);
            var b = TrailBit(pos, _cell, col);
            for (float t = 0f; t < 0.44f; t += Time.deltaTime)
            {
                float f = t / 0.44f;
                float g = Mathf.Clamp01(f * 1.6f - 0.3f);
                if (a) { a.rectTransform.sizeDelta = Vector2.one * _cell * (1f + f * 1.5f);
                         a.color = new Color(col.r, col.g, col.b, (1f - f) * 0.34f); }
                if (b) { b.rectTransform.sizeDelta = Vector2.one * _cell * (1f + g * 1.9f);
                         b.color = new Color(col.r, col.g, col.b, (1f - g) * 0.22f); }
                yield return null;
            }
            if (a) Destroy(a.gameObject);
            if (b) Destroy(b.gameObject);
        }

        private IEnumerator TrailShatter(Vector2 pos, Color col)
        {
            const int N = 6;
            var bits = new Image[N]; var dir = new Vector2[N]; var spin = new float[N];
            for (int i = 0; i < N; i++)
            {
                float ang = i * Mathf.PI * 2f / N;
                dir[i]  = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang));
                spin[i] = Random.Range(-260f, 260f);
                bits[i] = TrailBit(pos, _cell * 0.20f, col);
            }
            for (float t = 0f; t < 0.40f; t += Time.deltaTime)
            {
                float f = t / 0.40f;
                for (int i = 0; i < N; i++)
                {
                    if (bits[i] == null) continue;
                    var rt = bits[i].rectTransform;
                    rt.anchoredPosition = pos + dir[i] * (_cell * 0.75f * f);
                    rt.localRotation = Quaternion.Euler(0f, 0f, spin[i] * f);
                    rt.sizeDelta = Vector2.one * _cell * 0.20f * (1f - f * 0.5f);
                    bits[i].color = new Color(col.r, col.g, col.b, (1f - f) * 0.72f);
                }
                yield return null;
            }
            foreach (var b in bits) if (b) Destroy(b.gameObject);
        }

        private IEnumerator TrailBloom(Vector2 pos, Color col)
        {
            const int N = 5;
            var bits = new Image[N];
            for (int i = 0; i < N; i++) bits[i] = TrailBit(pos, _cell * 0.225f, col);
            for (float t = 0f; t < 0.46f; t += Time.deltaTime)
            {
                float f = t / 0.46f;
                for (int i = 0; i < N; i++)
                {
                    if (bits[i] == null) continue;
                    float a = i * Mathf.PI * 2f / N - f * 0.7f;
                    var rt = bits[i].rectTransform;
                    rt.anchoredPosition = pos + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * (_cell * (0.16f + f * 0.52f));
                    rt.localRotation = Quaternion.Euler(0f, 0f, a * Mathf.Rad2Deg);
                    rt.sizeDelta = new Vector2(_cell * 0.25f * (1f - f * 0.45f), _cell * 0.14f * (1f - f * 0.3f));
                    bits[i].color = new Color(col.r, col.g, col.b, (1f - f * 0.92f) * 0.70f);
                }
                yield return null;
            }
            foreach (var b in bits) if (b) Destroy(b.gameObject);
        }

        private IEnumerator TrailCascade(Vector2 pos, Color col)
        {
            const int N = 6;
            var bits = new Image[N]; var off = new float[N];
            for (int i = 0; i < N; i++)
            {
                off[i]  = (i - (N - 1) * 0.5f) * (_cell * 0.16f);
                bits[i] = TrailBit(pos + new Vector2(off[i], 0f), _cell * 0.095f, col);
            }
            for (float t = 0f; t < 0.52f; t += Time.deltaTime)
            {
                for (int i = 0; i < N; i++)
                {
                    if (bits[i] == null) continue;
                    float f = Mathf.Clamp01(t / 0.52f + i * 0.06f);
                    float drop = _cell * 1.15f * f;
                    float bounce = f > 0.72f ? Mathf.Sin((f - 0.72f) / 0.28f * Mathf.PI) * _cell * 0.16f : 0f;
                    bits[i].rectTransform.anchoredPosition = pos + new Vector2(off[i], -drop + bounce);
                    bits[i].color = new Color(col.r, col.g, col.b, (1f - f) * 0.72f);
                }
                yield return null;
            }
            foreach (var b in bits) if (b) Destroy(b.gameObject);
        }

        private IEnumerator TrailImplode(Vector2 pos, Color col)
        {
            const int N = 8;
            var bits = new Image[N]; var dir = new Vector2[N];
            for (int i = 0; i < N; i++)
            {
                float a = i * Mathf.PI * 2f / N;
                dir[i]  = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                bits[i] = TrailBit(pos + dir[i] * _cell * 0.8f, _cell * 0.115f, col);
            }
            for (float t = 0f; t < 0.30f; t += Time.deltaTime)   // rush inward
            {
                float f = t / 0.30f;
                for (int i = 0; i < N; i++)
                {
                    if (bits[i] == null) continue;
                    bits[i].rectTransform.anchoredPosition = pos + dir[i] * (_cell * (1f - f));
                    bits[i].color = new Color(col.r, col.g, col.b, 0.55f + f * 0.45f);
                }
                yield return null;
            }
            foreach (var b in bits) if (b) Destroy(b.gameObject);
            var flash = TrailBit(pos, _cell * 0.2f, Color.white);   // then the flash
            for (float t = 0f; t < 0.22f; t += Time.deltaTime)
            {
                float f = t / 0.22f;
                if (flash == null) break;
                flash.rectTransform.sizeDelta = Vector2.one * _cell * (0.15f + f * 1.1f);
                flash.color = new Color(1f, 1f, 1f, (1f - f) * 0.50f);
                yield return null;
            }
            if (flash) Destroy(flash.gameObject);
        }

        private IEnumerator RingBurst(Vector2 pos, Color col)
        {
            var go = new GameObject("ring"); go.transform.SetParent(_container, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(_cell, _cell);
            var img = go.AddComponent<Image>(); img.raycastTarget = false;
            img.sprite = NeonArt.SoftDot();
            for (float t = 0f; t < 0.38f; t += Time.deltaTime)
            {
                float f = t / 0.38f;
                rt.sizeDelta = new Vector2(_cell * (1f + f * 1.05f), _cell * (1f + f * 1.05f));
                img.color = new Color(col.r, col.g, col.b, (1f - f) * 0.50f);
                yield return null;
            }
            Destroy(go);
        }

        // ── floaters ────────────────────────────────────────────────────────────

        private void SpawnFloater(string text, Vector2 anchorPos, Color col)
        {
            GameObject go;
            if (_floaterPool.Count > 0) { go = _floaterPool.Dequeue(); go.SetActive(true); }
            else
            {
                go = new GameObject("Floater");
                go.transform.SetParent(_container, false);
                var rt = go.AddComponent<RectTransform>();
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(80, 28);
                var txt = go.AddComponent<Text>();
                txt.font = _font; txt.fontSize = 22; txt.fontStyle = FontStyle.Bold;
                txt.alignment = TextAnchor.MiddleCenter; txt.supportRichText = false;
            }
            go.GetComponent<RectTransform>().anchoredPosition = anchorPos;
            var label = go.GetComponent<Text>();
            label.text = text; label.color = col;
            StartCoroutine(AnimateFloater(go, anchorPos, col));
        }

        private IEnumerator AnimateFloater(GameObject go, Vector2 start, Color startCol)
        {
            var rt  = go.GetComponent<RectTransform>();
            var txt = go.GetComponent<Text>();
            const float dur = 0.85f;
            for (float t = 0f; t < dur;)
            {
                t = Mathf.Min(t + Time.deltaTime, dur);
                float f = t / dur;
                rt.anchoredPosition = start + new Vector2(0, f * 54f);
                txt.color = new Color(startCol.r, startCol.g, startCol.b, 1f - f * f);
                yield return null;
            }
            go.SetActive(false);
            _floaterPool.Enqueue(go);
        }

        // ── display ─────────────────────────────────────────────────────────────

        public void Refresh()
        {
            if (_animating) return;
            RefreshImmediate();
        }

        private void RefreshImmediate()
        {
            if (_session == null || _cells == null) return;
            var board = _session.Board;
            int boardSkin = (int)NeonCosmetics.ActiveBoardSkin == 0
                ? 4 + NeonTheme.Active.BoardSkin
                : (int)NeonCosmetics.ActiveBoardSkin;
            int gemStyle = NeonTheme.Active.GemStyle;
            for (int x = 0; x < _cols; x++)
            {
                for (int y = 0; y < _rows; y++)
                {
                    var tile = board.GetTile(x, y);
                    _cells[x, y].texture = tile.Empty
                        ? NeonArt.EmptyCell(boardSkin)
                        : NeonArt.Gem((int)tile.Gem, gemStyle);
                    _cells[x, y].color   = Color.white;

                    if (tile.Special != SpecialKind.None)
                    {
                        _borders[x, y].texture = NeonArt.SpecialBorder(tile.Special);
                        if (_borders[x, y].color.a < 0.01f)
                            _borders[x, y].color = Color.white;
                    }
                    else
                    {
                        _borders[x, y].texture = null;
                        _borders[x, y].color   = Color.clear;
                    }

                    bool sel = _selX == x && _selY == y;
                    _overlays[x, y].color = sel
                        ? new Color(1f, 1f, 1f, 0.32f)
                        : new Color(0, 0, 0, 0);
                }
            }
        }

        static float EaseOut(float t) => 1f - (1f - t) * (1f - t);
    }
}
