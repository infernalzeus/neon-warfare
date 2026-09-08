using System.Collections.Generic;

namespace NW.Board.Domain
{
    public enum GemKind : byte { Energy, Plasma, Nano, Quantum, Data }

    public enum SpecialKind : byte { None, LaserH, LaserV, Cross, Singularity }

    public readonly struct Cell
    {
        public readonly int X, Y;
        public Cell(int x, int y) { X = x; Y = y; }
        public override string ToString() => $"({X},{Y})";
    }

    public enum BoardEventType : byte
    {
        Swapped,          // A↔B (valid swap)
        SwapRejected,     // A↔B (no match; view plays swap-and-back)
        TileCleared,      // A, Gem, Combo
        SpecialCreated,   // A, Gem, Special
        SpecialDetonated, // A, Gem, Special  (laser sweep / singularity implosion)
        ResourcePayout,   // Gem, Amount, Combo (one per cleared run)
        TileFell,         // A=from, B=to (existing tile collapsing)
        TileSpawned,      // B=landing cell, Gem, Amount=spawn row offset above board
        CascadeStep,      // Combo (a new resolve iteration began)
        ComboMilestone,   // Combo (crossover hooks: 5=orbital strike, 8=EMP)
        EmpTriggered,     // Singularity+Singularity swap (full wipe + battlefield stun)
        BoardReshuffled,  // deadlock auto-reshuffle
        ResolveEnded      // Combo = peak combo of the resolve
    }

    /// <summary>
    /// One entry on the event tape. The domain emits these in order; the view
    /// layer consumes the tape to schedule animation. Alloc-light: plain struct,
    /// tape list is reused between resolves.
    /// </summary>
    public readonly struct BoardEvent
    {
        public readonly BoardEventType Type;
        public readonly Cell A;
        public readonly Cell B;
        public readonly GemKind Gem;
        public readonly SpecialKind Special;
        public readonly int Amount;
        public readonly int Combo;

        public BoardEvent(BoardEventType type, Cell a = default, Cell b = default,
            GemKind gem = default, SpecialKind special = SpecialKind.None,
            int amount = 0, int combo = 0)
        {
            Type = type; A = a; B = b; Gem = gem; Special = special;
            Amount = amount; Combo = combo;
        }
    }

    /// <summary>Result of a swap attempt: whether it resolved, and the event tape.</summary>
    public readonly struct SwapResult
    {
        public readonly bool Valid;
        public readonly IReadOnlyList<BoardEvent> Tape;
        public SwapResult(bool valid, IReadOnlyList<BoardEvent> tape) { Valid = valid; Tape = tape; }
    }
}
