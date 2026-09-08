using System.Collections.Generic;
using NW.Domain;

namespace NW.Board.Domain
{
    /// <summary>
    /// Pure-C# Compiler board: grid state, swap validation, shaped-match detection
    /// (4 → line laser, L/T → cross, 5 → singularity), special detonation chains,
    /// cascade resolve with combo multipliers, deadlock reshuffle.
    ///
    /// Emits an ordered event tape (BoardEvents.cs) that the view layer animates.
    /// No UnityEngine references — runs headless in tests and the balance simulator.
    /// Gravity: +Y is down; tiles collapse toward y = Rows-1.
    /// </summary>
    public sealed class BoardModel
    {
        public const int GemKindCount = 5;

        public struct Tile
        {
            public GemKind Gem;
            public SpecialKind Special;
            public bool Empty;
        }

        public int Cols { get; }
        public int Rows { get; }
        /// <summary>Highest combo reached during the last resolve.</summary>
        public int PeakCombo { get; private set; }

        private readonly Tile[,] _grid;
        private readonly Rng _rng;
        private readonly List<BoardEvent> _tape = new(128);

        // Scratch collections, reused across resolves (zero steady-state alloc).
        private readonly List<Run> _runs = new(16);
        private readonly HashSet<int> _clearSet = new(64);
        private readonly Queue<int> _detonations = new(8);
        private readonly int[] _gemCounts = new int[GemKindCount];

        private struct Run
        {
            public GemKind Gem;
            public bool Horizontal;
            public int X, Y, Length; // origin cell + extent along the axis
        }

        private int _allowedGemCount = GemKindCount;

        public BoardModel(int cols, int rows, Rng rng, int allowedGemCount = GemKindCount)
        {
            Cols = cols; Rows = rows; _rng = rng;
            _allowedGemCount = System.Math.Clamp(allowedGemCount, 2, GemKindCount);
            _grid = new Tile[cols, rows];
            FillInitial();
        }

        /// <summary>Test/sim constructor: explicit grid, no initial-fill rules applied.</summary>
        public static BoardModel FromGrid(GemKind[,] gems, Rng rng)
        {
            var m = new BoardModel(gems.GetLength(0), gems.GetLength(1), rng, skipFill: true);
            for (int x = 0; x < m.Cols; x++)
                for (int y = 0; y < m.Rows; y++)
                    m._grid[x, y] = new Tile { Gem = gems[x, y] };
            return m;
        }

        private BoardModel(int cols, int rows, Rng rng, bool skipFill)
        {
            Cols = cols; Rows = rows; _rng = rng;
            _grid = new Tile[cols, rows];
        }

        public Tile GetTile(int x, int y) => _grid[x, y];

        public void SetSpecialForTest(int x, int y, SpecialKind special) => _grid[x, y].Special = special;

        // ---------------------------------------------------------------- swap

        /// <summary>
        /// Attempt to swap two adjacent cells. Returns the event tape for the view.
        /// The tape is invalidated by the next call — consume it immediately.
        /// </summary>
        public SwapResult TrySwap(Cell a, Cell b)
        {
            _tape.Clear();
            PeakCombo = 0;
            if (!InBounds(a) || !InBounds(b) || !AreAdjacent(a, b))
                return new SwapResult(false, _tape);

            Tile ta = _grid[a.X, a.Y];
            Tile tb = _grid[b.X, b.Y];

            // Special+special recipes resolve immediately and are always valid.
            if (ta.Special != SpecialKind.None && tb.Special != SpecialKind.None)
            {
                Swap(a, b);
                Emit(BoardEventType.Swapped, a, b);
                ResolveSpecialCombo(a, b);
                ResolveCascades(startCombo: 1);
                EnsureMoves();
                Emit(BoardEventType.ResolveEnded, combo: PeakCombo);
                return new SwapResult(true, _tape);
            }

            // Singularity swapped with a normal gem: clear all of that gem.
            if (ta.Special == SpecialKind.Singularity || tb.Special == SpecialKind.Singularity)
            {
                Swap(a, b);
                Emit(BoardEventType.Swapped, a, b);
                Cell singCell = ta.Special == SpecialKind.Singularity ? b : a;
                GemKind target = ta.Special == SpecialKind.Singularity ? tb.Gem : ta.Gem;
                DetonateSingularity(singCell, target, combo: 1);
                FinishClearStep(combo: 1);
                ResolveCascades(startCombo: 2);
                EnsureMoves();
                Emit(BoardEventType.ResolveEnded, combo: PeakCombo);
                return new SwapResult(true, _tape);
            }

            // Plain swap: must produce at least one run, else revert.
            Swap(a, b);
            FindRuns();
            if (_runs.Count == 0)
            {
                Swap(a, b); // revert
                Emit(BoardEventType.SwapRejected, a, b);
                return new SwapResult(false, _tape);
            }

            Emit(BoardEventType.Swapped, a, b);
            ResolveCascades(startCombo: 1, specialAnchorA: a, specialAnchorB: b);
            EnsureMoves();
            Emit(BoardEventType.ResolveEnded, combo: PeakCombo);
            return new SwapResult(true, _tape);
        }

        // ------------------------------------------------------------- resolve

        /// <summary>Clear → collapse → refill until the board is stable.</summary>
        private void ResolveCascades(int startCombo, Cell? specialAnchorA = null, Cell? specialAnchorB = null)
        {
            int combo = startCombo;
            for (int cascadeGuard = 0; cascadeGuard < 30; cascadeGuard++)
            {
                FindRuns();
                if (_runs.Count == 0) break;

                PeakCombo = combo > PeakCombo ? combo : PeakCombo;
                Emit(BoardEventType.CascadeStep, combo: combo);
                if (combo == 5 || combo == 8)
                    Emit(BoardEventType.ComboMilestone, combo: combo);

                ClearRunsAndCreateSpecials(combo,
                    combo == startCombo ? specialAnchorA : null,
                    combo == startCombo ? specialAnchorB : null);
                FinishClearStep(combo);
                combo++;
            }
        }

        /// <summary>
        /// Clears all current runs, paying per-run, transforming one cell per
        /// qualifying shape into a special tile, and chaining any specials caught
        /// in the blast.
        /// </summary>
        private void ClearRunsAndCreateSpecials(int combo, Cell? anchorA, Cell? anchorB)
        {
            _clearSet.Clear();
            float mult = ComboMultiplier(combo);

            // Cross detection: cell shared by an H-run and a V-run of the same gem.
            // Key every run cell so intersections can be found cheaply.
            var hCells = new Dictionary<int, int>(); // cellKey -> run index
            for (int i = 0; i < _runs.Count; i++)
            {
                Run r = _runs[i];
                if (!r.Horizontal) continue;
                for (int k = 0; k < r.Length; k++) hCells[Key(r.X + k, r.Y)] = i;
            }

            var specialAt = new Dictionary<int, SpecialKind>();
            for (int i = 0; i < _runs.Count; i++)
            {
                Run r = _runs[i];

                // Payout: (3 base + 2 per tile beyond 3) × combo multiplier (doc 04 §1.1).
                int amount = (int)((3 + 2 * (r.Length - 3)) * mult);
                Emit(BoardEventType.ResourcePayout, gem: r.Gem, amount: amount, combo: combo);

                for (int k = 0; k < r.Length; k++)
                {
                    int x = r.Horizontal ? r.X + k : r.X;
                    int y = r.Horizontal ? r.Y : r.Y + k;
                    _clearSet.Add(Key(x, y));

                    // L/T shape: this vertical run crosses a horizontal run of the same gem.
                    if (!r.Horizontal && hCells.TryGetValue(Key(x, y), out int hi) && _runs[hi].Gem == r.Gem)
                        specialAt[Key(x, y)] = SpecialKind.Cross;
                }

                if (r.Length >= 5)
                    specialAt[AnchorKey(r, anchorA, anchorB)] = SpecialKind.Singularity;
                else if (r.Length == 4)
                {
                    int key = AnchorKey(r, anchorA, anchorB);
                    if (!specialAt.ContainsKey(key)) // cross wins over laser
                        specialAt[key] = r.Horizontal ? SpecialKind.LaserH : SpecialKind.LaserV;
                }
            }

            // Chain detonations for any pre-existing special caught in the clear.
            _detonations.Clear();
            foreach (int key in _clearSet)
                if (_grid[KeyX(key), KeyY(key)].Special != SpecialKind.None)
                    _detonations.Enqueue(key);
            ExpandDetonations(combo);

            // Transform special-creation cells instead of clearing them.
            foreach (var kv in specialAt)
            {
                int x = KeyX(kv.Key), y = KeyY(kv.Key);
                _clearSet.Remove(kv.Key);
                _grid[x, y].Special = kv.Value;
                Emit(BoardEventType.SpecialCreated, new Cell(x, y), gem: _grid[x, y].Gem, special: kv.Value);
            }
        }

        /// <summary>BFS over specials hit by the current clear set, expanding it.</summary>
        private void ExpandDetonations(int combo)
        {
            for (int i = 0; i < _gemCounts.Length; i++) _gemCounts[i] = 0;

            while (_detonations.Count > 0)
            {
                int key = _detonations.Dequeue();
                int x = KeyX(key), y = KeyY(key);
                Tile t = _grid[x, y];
                if (t.Special == SpecialKind.None || t.Empty) continue;
                _grid[x, y].Special = SpecialKind.None; // consumed
                Emit(BoardEventType.SpecialDetonated, new Cell(x, y), gem: t.Gem, special: t.Special);

                switch (t.Special)
                {
                    case SpecialKind.LaserH: AddLine(y, horizontal: true); break;
                    case SpecialKind.LaserV: AddLine(x, horizontal: false); break;
                    case SpecialKind.Cross: AddLine(y, true); AddLine(x, false); break;
                    case SpecialKind.Singularity: AddAllOfGem(MostCommonGem()); break;
                }
            }

            // Tiles newly added by detonations (counted in AddLine/AddAllOfGem)
            // pay 1/tile × multiplier, grouped per gem. Run tiles were already
            // paid per-run, so only detonation surplus is paid here.
            float mult = ComboMultiplier(combo);
            for (int g = 0; g < GemKindCount; g++)
                if (_gemCounts[g] > 0)
                {
                    Emit(BoardEventType.ResourcePayout, gem: (GemKind)g,
                        amount: (int)(_gemCounts[g] * mult), combo: combo);
                    _gemCounts[g] = 0;
                }
        }

        private void AddLine(int index, bool horizontal)
        {
            int len = horizontal ? Cols : Rows;
            for (int k = 0; k < len; k++)
            {
                int x = horizontal ? k : index;
                int y = horizontal ? index : k;
                if (_grid[x, y].Empty) continue;
                if (_clearSet.Add(Key(x, y)))
                {
                    _gemCounts[(int)_grid[x, y].Gem]++;
                    if (_grid[x, y].Special != SpecialKind.None)
                        _detonations.Enqueue(Key(x, y));
                }
            }
        }

        private void AddAllOfGem(GemKind gem)
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                {
                    if (_grid[x, y].Empty || _grid[x, y].Gem != gem) continue;
                    if (_clearSet.Add(Key(x, y)))
                    {
                        _gemCounts[(int)_grid[x, y].Gem]++;
                        if (_grid[x, y].Special != SpecialKind.None)
                            _detonations.Enqueue(Key(x, y));
                    }
                }
        }

        /// <summary>Singularity swapped with a normal gem: clear every tile of that gem.</summary>
        private void DetonateSingularity(Cell singCell, GemKind target, int combo)
        {
            PeakCombo = combo > PeakCombo ? combo : PeakCombo;
            _clearSet.Clear();
            _detonations.Clear();
            _grid[singCell.X, singCell.Y].Special = SpecialKind.None;
            _clearSet.Add(Key(singCell.X, singCell.Y));
            Emit(BoardEventType.SpecialDetonated, singCell, gem: target, special: SpecialKind.Singularity);

            int count = 0;
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    if (!_grid[x, y].Empty && _grid[x, y].Gem == target)
                    {
                        count++;
                        if (_clearSet.Add(Key(x, y)) && _grid[x, y].Special != SpecialKind.None)
                            _detonations.Enqueue(Key(x, y));
                    }
            ExpandDetonations(combo);
            Emit(BoardEventType.ResourcePayout, gem: target,
                amount: (int)(count * ComboMultiplier(combo)), combo: combo);
        }

        /// <summary>Special+special swap recipes (GDD §3.3).</summary>
        private void ResolveSpecialCombo(Cell a, Cell b)
        {
            Tile ta = _grid[a.X, a.Y];
            Tile tb = _grid[b.X, b.Y];
            _clearSet.Clear();
            _detonations.Clear();

            bool bothSing = ta.Special == SpecialKind.Singularity && tb.Special == SpecialKind.Singularity;
            if (bothSing)
            {
                // EMP: full board wipe + battlefield stun hook.
                _grid[a.X, a.Y].Special = SpecialKind.None;
                _grid[b.X, b.Y].Special = SpecialKind.None;
                Emit(BoardEventType.EmpTriggered, a, b);
                for (int x = 0; x < Cols; x++)
                    for (int y = 0; y < Rows; y++)
                        _clearSet.Add(Key(x, y));
                for (int g = 0; g < GemKindCount; g++) _gemCounts[g] = 0;
                foreach (int key in _clearSet) _gemCounts[(int)_grid[KeyX(key), KeyY(key)].Gem]++;
                for (int g = 0; g < GemKindCount; g++)
                    if (_gemCounts[g] > 0)
                        Emit(BoardEventType.ResourcePayout, gem: (GemKind)g, amount: _gemCounts[g], combo: 1);
                FinishClearStep(combo: 1);
                ResolveCascades(startCombo: 2);
                return;
            }

            if (ta.Special == SpecialKind.Singularity || tb.Special == SpecialKind.Singularity)
            {
                // Singularity + Laser/Cross: every tile of the laser's gem becomes a
                // laser (alternating H/V) and detonates.
                Cell sing = ta.Special == SpecialKind.Singularity ? a : b;
                Tile laser = ta.Special == SpecialKind.Singularity ? tb : ta;
                _grid[sing.X, sing.Y].Special = SpecialKind.None;
                Emit(BoardEventType.SpecialDetonated, sing, gem: laser.Gem, special: SpecialKind.Singularity);
                bool flip = false;
                for (int x = 0; x < Cols; x++)
                    for (int y = 0; y < Rows; y++)
                        if (!_grid[x, y].Empty && _grid[x, y].Gem == laser.Gem && _grid[x, y].Special == SpecialKind.None)
                        {
                            _grid[x, y].Special = flip ? SpecialKind.LaserH : SpecialKind.LaserV;
                            flip = !flip;
                        }
                _clearSet.Add(Key(a.X, a.Y));
                _clearSet.Add(Key(b.X, b.Y));
                foreach (int key in new[] { Key(a.X, a.Y), Key(b.X, b.Y) })
                    if (_grid[KeyX(key), KeyY(key)].Special != SpecialKind.None)
                        _detonations.Enqueue(key);
                AddAllOfGem(laser.Gem);
                ExpandDetonations(combo: 1);
                FinishClearStep(combo: 1);
                ResolveCascades(startCombo: 2);
                return;
            }

            // Laser/Cross + Laser/Cross: double cross centered on the swap.
            _detonations.Enqueue(Key(a.X, a.Y));
            _detonations.Enqueue(Key(b.X, b.Y));
            _grid[a.X, a.Y].Special = SpecialKind.Cross;
            _grid[b.X, b.Y].Special = SpecialKind.Cross;
            _clearSet.Add(Key(a.X, a.Y));
            _clearSet.Add(Key(b.X, b.Y));
            ExpandDetonations(combo: 1);
            FinishClearStep(combo: 1);
            ResolveCascades(startCombo: 2);
        }

        /// <summary>Apply the accumulated clear set: clear, collapse, refill.</summary>
        private void FinishClearStep(int combo)
        {
            foreach (int key in _clearSet)
            {
                int x = KeyX(key), y = KeyY(key);
                if (_grid[x, y].Empty) continue;
                Emit(BoardEventType.TileCleared, new Cell(x, y), gem: _grid[x, y].Gem, combo: combo);
                _grid[x, y].Empty = true;
                _grid[x, y].Special = SpecialKind.None;
            }
            _clearSet.Clear();
            CollapseAndRefill();
        }

        private void CollapseAndRefill()
        {
            for (int x = 0; x < Cols; x++)
            {
                int write = Rows - 1;
                for (int y = Rows - 1; y >= 0; y--)
                {
                    if (_grid[x, y].Empty) continue;
                    if (write != y)
                    {
                        _grid[x, write] = _grid[x, y];
                        _grid[x, y].Empty = true;
                        Emit(BoardEventType.TileFell, new Cell(x, y), new Cell(x, write));
                    }
                    write--;
                }
                // Fill top-down (y=0 first) so RandomGemAvoidingRuns can check already-placed
                // cells above and prevent horizontal/vertical runs in spawned tiles.
                int spawnCount = write + 1;
                for (int y = 0; y <= write; y++)
                {
                    var gem = RandomGemAvoidingRuns(x, y);
                    _grid[x, y] = new Tile { Gem = gem };
                    Emit(BoardEventType.TileSpawned, b: new Cell(x, y), gem: gem, amount: spawnCount);
                }
            }
        }

        // -------------------------------------------------------- run scanning

        /// <summary>Scan rows and columns for runs of 3+ identical gems into _runs.</summary>
        private void FindRuns()
        {
            _runs.Clear();
            for (int y = 0; y < Rows; y++)
            {
                int start = 0;
                for (int x = 1; x <= Cols; x++)
                {
                    bool same = x < Cols
                        && !_grid[x, y].Empty && !_grid[start, y].Empty
                        && _grid[x, y].Gem == _grid[start, y].Gem;
                    if (same) continue;
                    if (!_grid[start, y].Empty && x - start >= 3)
                        _runs.Add(new Run { Gem = _grid[start, y].Gem, Horizontal = true, X = start, Y = y, Length = x - start });
                    start = x;
                }
            }
            for (int x = 0; x < Cols; x++)
            {
                int start = 0;
                for (int y = 1; y <= Rows; y++)
                {
                    bool same = y < Rows
                        && !_grid[x, y].Empty && !_grid[x, start].Empty
                        && _grid[x, y].Gem == _grid[x, start].Gem;
                    if (same) continue;
                    if (!_grid[x, start].Empty && y - start >= 3)
                        _runs.Add(new Run { Gem = _grid[x, start].Gem, Horizontal = false, X = x, Y = start, Length = y - start });
                    start = y;
                }
            }
        }

        // ----------------------------------------------------- moves/deadlock

        public bool HasAnyMove()
        {
            // Any special on board is always a legal move.
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    if (_grid[x, y].Special != SpecialKind.None)
                        return true;

            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                {
                    if (x + 1 < Cols && SwapMakesRun(x, y, x + 1, y)) return true;
                    if (y + 1 < Rows && SwapMakesRun(x, y, x, y + 1)) return true;
                }
            return false;
        }

        private bool SwapMakesRun(int ax, int ay, int bx, int by)
        {
            (_grid[ax, ay], _grid[bx, by]) = (_grid[bx, by], _grid[ax, ay]);
            FindRuns();
            bool found = _runs.Count > 0;
            (_grid[ax, ay], _grid[bx, by]) = (_grid[bx, by], _grid[ax, ay]);
            return found;
        }

        private void EnsureMoves()
        {
            int guard = 0;
            while (!HasAnyMove() && guard++ < 30)
            {
                ReassignRandom();
                Emit(BoardEventType.BoardReshuffled);
            }
        }

        private void ReassignRandom()
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                {
                    if (_grid[x, y].Special != SpecialKind.None) continue; // specials stay
                    _grid[x, y].Gem = RandomGemAvoidingRuns(x, y);
                }
        }

        private void FillInitial()
        {
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    _grid[x, y] = new Tile { Gem = RandomGemAvoidingRuns(x, y) };
            EnsureMoves();
        }

        private GemKind RandomGemAvoidingRuns(int x, int y)
        {
            // Try random first for variety; fall back to exhaustive scan to avoid
            // an infinite loop on boards with very few gem colors (e.g., 2-color early levels).
            for (int attempt = 0; attempt < 20; attempt++)
            {
                var g = (GemKind)_rng.Next(_allowedGemCount);
                if (!WouldCreateRun(x, y, g)) return g;
            }
            for (int i = 0; i < _allowedGemCount; i++)
                if (!WouldCreateRun(x, y, (GemKind)i)) return (GemKind)i;
            return (GemKind)_rng.Next(_allowedGemCount); // accept a run rather than loop forever
        }

        private bool WouldCreateRun(int x, int y, GemKind g) =>
            (x >= 2 && !_grid[x-1,y].Empty && _grid[x-1,y].Gem == g
                     && !_grid[x-2,y].Empty && _grid[x-2,y].Gem == g) ||
            (y >= 2 && !_grid[x,y-1].Empty && _grid[x,y-1].Gem == g
                     && !_grid[x,y-2].Empty && _grid[x,y-2].Gem == g);

        // -------------------------------------------------------------- utils

        public static float ComboMultiplier(int combo) =>
            combo <= 1 ? 1f : combo == 2 ? 1.5f : combo == 3 ? 2f : 3f;

        private GemKind MostCommonGem()
        {
            // Local scratch — _gemCounts is in use as the detonation-payout
            // accumulator whenever this is called from the BFS.
            System.Span<int> counts = stackalloc int[GemKindCount];
            for (int x = 0; x < Cols; x++)
                for (int y = 0; y < Rows; y++)
                    if (!_grid[x, y].Empty) counts[(int)_grid[x, y].Gem]++;
            int best = 0;
            for (int i = 1; i < counts.Length; i++)
                if (counts[i] > counts[best]) best = i;
            return (GemKind)best;
        }

        private int AnchorKey(Run r, Cell? a, Cell? b)
        {
            // Specials spawn at the swapped cell when it sits inside the run,
            // otherwise at the run's center.
            if (a.HasValue && RunContains(r, a.Value)) return Key(a.Value.X, a.Value.Y);
            if (b.HasValue && RunContains(r, b.Value)) return Key(b.Value.X, b.Value.Y);
            return r.Horizontal ? Key(r.X + r.Length / 2, r.Y) : Key(r.X, r.Y + r.Length / 2);
        }

        private static bool RunContains(Run r, Cell c) =>
            r.Horizontal
                ? c.Y == r.Y && c.X >= r.X && c.X < r.X + r.Length
                : c.X == r.X && c.Y >= r.Y && c.Y < r.Y + r.Length;

        private void Swap(Cell a, Cell b) =>
            (_grid[a.X, a.Y], _grid[b.X, b.Y]) = (_grid[b.X, b.Y], _grid[a.X, a.Y]);

        private static bool AreAdjacent(Cell a, Cell b)
        {
            int dx = a.X - b.X, dy = a.Y - b.Y;
            return (dx == 0 && (dy == 1 || dy == -1)) || (dy == 0 && (dx == 1 || dx == -1));
        }

        private bool InBounds(Cell c) => c.X >= 0 && c.X < Cols && c.Y >= 0 && c.Y < Rows;

        private int Key(int x, int y) => y * Cols + x;
        private int KeyX(int key) => key % Cols;
        private int KeyY(int key) => key / Cols;

        private void Emit(BoardEventType type, Cell a = default, Cell b = default,
            GemKind gem = default, SpecialKind special = SpecialKind.None, int amount = 0, int combo = 0)
            => _tape.Add(new BoardEvent(type, a, b, gem, special, amount, combo));
    }
}
