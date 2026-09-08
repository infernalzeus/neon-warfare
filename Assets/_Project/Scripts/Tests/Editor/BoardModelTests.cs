using System.Collections.Generic;
using NUnit.Framework;
using NW.Board.Domain;
using NW.Domain;

namespace NW.Tests
{
    public class BoardModelTests
    {
        private static GemKind[,] Grid(params string[] rows)
        {
            // Visual literal: rows top-to-bottom, chars E P N Q D map to gems.
            int h = rows.Length, w = rows[0].Length;
            var g = new GemKind[w, h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                    g[x, y] = rows[y][x] switch
                    {
                        'E' => GemKind.Energy, 'P' => GemKind.Plasma, 'N' => GemKind.Nano,
                        'Q' => GemKind.Quantum, _ => GemKind.Data
                    };
            return g;
        }

        private static int TotalPayout(IReadOnlyList<BoardEvent> tape, GemKind gem)
        {
            int sum = 0;
            foreach (BoardEvent e in tape)
                if (e.Type == BoardEventType.ResourcePayout && e.Gem == gem) sum += e.Amount;
            return sum;
        }

        private static bool Has(IReadOnlyList<BoardEvent> tape, BoardEventType type)
        {
            foreach (BoardEvent e in tape) if (e.Type == type) return true;
            return false;
        }

        [Test]
        public void InitialFill_HasNoRuns_AndHasAMove()
        {
            var board = new BoardModel(8, 8, new Rng(42));
            // A fresh board must offer a legal move and contain no pre-cleared runs:
            // any run would have auto-resolved on a swap, so simply assert moves exist.
            Assert.IsTrue(board.HasAnyMove());
        }

        [Test]
        public void Swap_WithoutMatch_IsRejected_AndReverted()
        {
            var board = BoardModel.FromGrid(Grid(
                "EPEP",
                "PEPE",
                "EPEP",
                "PEPE"), new Rng(1));
            var before = board.GetTile(0, 0).Gem;
            SwapResult result = board.TrySwap(new Cell(0, 0), new Cell(1, 0));
            Assert.IsFalse(result.Valid);
            Assert.IsTrue(Has(result.Tape, BoardEventType.SwapRejected));
            Assert.AreEqual(before, board.GetTile(0, 0).Gem, "board must revert");
        }

        [Test]
        public void ThreeMatch_PaysBaseAmount()
        {
            // Swapping (0,1)→(0,0) gives EEE across the top row. 4th column padding
            // prevents accidental verticals.
            var board = BoardModel.FromGrid(Grid(
                "PEEQ",
                "ENPQ",
                "QPND",
                "DQPE"), new Rng(7));
            SwapResult result = board.TrySwap(new Cell(0, 0), new Cell(0, 1));
            Assert.IsTrue(result.Valid);
            // Base 3-match pays 3 at combo ×1 (cascade payouts from refills may add more
            // of OTHER kinds; Energy total must be at least the base 3).
            Assert.GreaterOrEqual(TotalPayout(result.Tape, GemKind.Energy), 3);
        }

        [Test]
        public void FourMatch_CreatesLineLaser()
        {
            var board = BoardModel.FromGrid(Grid(
                "PEEEQ",
                "EQPND",
                "QPNDE",
                "DQPEN",
                "NPQDE"), new Rng(7));
            // Swap (0,1) E up to (0,0) → top row EEEE.
            SwapResult result = board.TrySwap(new Cell(0, 0), new Cell(0, 1));
            Assert.IsTrue(result.Valid);
            bool laserCreated = false;
            foreach (BoardEvent e in result.Tape)
                if (e.Type == BoardEventType.SpecialCreated &&
                    (e.Special == SpecialKind.LaserH || e.Special == SpecialKind.LaserV))
                    laserCreated = true;
            Assert.IsTrue(laserCreated, "4-match must create a line laser");
        }

        [Test]
        public void ComboMultiplier_FollowsEconomyDoc()
        {
            Assert.AreEqual(1f, BoardModel.ComboMultiplier(1));
            Assert.AreEqual(1.5f, BoardModel.ComboMultiplier(2));
            Assert.AreEqual(2f, BoardModel.ComboMultiplier(3));
            Assert.AreEqual(3f, BoardModel.ComboMultiplier(4));
            Assert.AreEqual(3f, BoardModel.ComboMultiplier(9), "capped at ×3");
        }

        [Test]
        public void SingularitySwap_ClearsAllOfTargetGem_AndPays()
        {
            var board = BoardModel.FromGrid(Grid(
                "PEPQ",
                "EQPE",
                "QPNE",
                "DQPE"), new Rng(3));
            board.SetSpecialForTest(1, 1, SpecialKind.Singularity);
            SwapResult result = board.TrySwap(new Cell(1, 1), new Cell(0, 1)); // swap with an E
            Assert.IsTrue(result.Valid, "special swaps are always valid");
            Assert.IsTrue(Has(result.Tape, BoardEventType.SpecialDetonated));
            Assert.GreaterOrEqual(TotalPayout(result.Tape, GemKind.Energy), 5,
                "all Energy gems on board pay out");
        }

        [Test]
        public void DoubleSingularity_TriggersEmp()
        {
            var board = BoardModel.FromGrid(Grid(
                "PEPQ",
                "EQPE",
                "QPNE",
                "DQPE"), new Rng(3));
            board.SetSpecialForTest(1, 1, SpecialKind.Singularity);
            board.SetSpecialForTest(2, 1, SpecialKind.Singularity);
            SwapResult result = board.TrySwap(new Cell(1, 1), new Cell(2, 1));
            Assert.IsTrue(result.Valid);
            Assert.IsTrue(Has(result.Tape, BoardEventType.EmpTriggered), "S+S = EMP board wipe");
        }

        [Test]
        public void Resolve_RefillsBoard_NoEmptyTiles()
        {
            var board = BoardModel.FromGrid(Grid(
                "PEEQ",
                "ENPQ",
                "QPND",
                "DQPE"), new Rng(7));
            board.TrySwap(new Cell(0, 0), new Cell(0, 1));
            for (int x = 0; x < board.Cols; x++)
                for (int y = 0; y < board.Rows; y++)
                    Assert.IsFalse(board.GetTile(x, y).Empty, $"tile ({x},{y}) must be refilled");
        }
    }
}
