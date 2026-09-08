using System;
using System.Collections.Generic;
using NUnit.Framework;
using NW.App;

namespace NW.Tests
{
    /// <summary>
    /// The leaderboard's pure, backend-agnostic core: the overwrite rule (`Beats`), the single
    /// monotonic `SortKey` a remote store ranks by, the explicit-seed `Standings`, and the
    /// `Leaderboard` facade's backend swap. Nothing here touches the filesystem or Firebase.
    /// </summary>
    public class LeaderboardTests
    {
        static GhostRecord G(string pilot, int seed, bool won, int ticks, long utc = 0) => new GhostRecord
        {
            id = pilot, pilot = pilot, pilotId = pilot, level = 4, seed = seed, mmr = 1300,
            result = won ? 1 : 0, durationTicks = ticks,
            recordedUtc = utc, actions = new List<GhostAction> { new GhostAction { tick = 1, type = 0, unit = "drone", lane = 0 } },
        };

        // ── Beats: the overwrite rule ──────────────────────────────────────────

        [Test]
        public void Beats_NullIncumbent_AlwaysTrue()
            => Assert.IsTrue(RankLadder.Beats(G("a", 1, won: true, 200), null));

        [Test]
        public void Beats_WinOverLoss_FasterWin_LongerLoss()
        {
            var win  = G("w", 1, won: true,  ticks: 300);
            var loss = G("l", 1, won: false, ticks: 900);
            Assert.IsTrue (RankLadder.Beats(win, loss),  "any win beats any loss");
            Assert.IsFalse(RankLadder.Beats(loss, win));

            var fastWin = G("f", 1, won: true, ticks: 150);
            Assert.IsTrue (RankLadder.Beats(fastWin, win), "faster clear wins");
            Assert.IsFalse(RankLadder.Beats(win, fastWin));

            var longLoss = G("L", 1, won: false, ticks: 1200);
            Assert.IsTrue (RankLadder.Beats(longLoss, loss), "lasting longer wins a losing board");
        }

        // ── SortKey: one number the remote store orders by ────────────────────

        [Test]
        public void SortKey_OrdersLikeBeats()
        {
            var records = new[]
            {
                G("a", 1, won: true,  ticks: 400),
                G("b", 1, won: true,  ticks: 180),
                G("c", 1, won: false, ticks: 250),
                G("d", 1, won: false, ticks: 950),
            };
            foreach (var x in records)
                foreach (var y in records)
                {
                    if (ReferenceEquals(x, y)) continue;
                    bool beats  = RankLadder.Beats(x, y);
                    bool higher = RankLadder.SortKey(x) > RankLadder.SortKey(y);
                    // ties (equal key) only when neither beats the other
                    if (RankLadder.SortKey(x) == RankLadder.SortKey(y))
                        Assert.IsFalse(beats, "equal key implies neither ranks ahead");
                    else
                        Assert.AreEqual(beats, higher, $"{x.pilot} vs {y.pilot}: Beats and SortKey disagree");
                }
        }

        [Test]
        public void SortKey_EveryWinOutranksEveryLoss()
        {
            long slowestWin = RankLadder.SortKey(won: true,  durationTicks: 900_000);
            long fastestLoss = RankLadder.SortKey(won: false, durationTicks: 0);
            Assert.Greater(slowestWin, fastestLoss);
        }

        // ── Standings with an explicit seed ──────────────────────────────────

        [Test]
        public void Standings_ExplicitSeed_FiltersAndRanks()
        {
            var pool = new List<GhostRecord>
            {
                G("A", seed: 77, won: true,  ticks: 300),
                G("B", seed: 77, won: true,  ticks: 220),
                G("C", seed: 77, won: false, ticks: 500),
                G("X", seed: 88, won: true,  ticks: 10),   // different board
            };
            var table = RankLadder.Standings(pool, level: 4, seed: 77, mePilot: "A");

            Assert.AreEqual(3, table.Count);
            Assert.AreEqual("B", table[0].pilot);
            Assert.AreEqual("A", table[1].pilot);
            Assert.AreEqual("C", table[2].pilot);
            Assert.IsTrue(table[1].isYou);
            Assert.AreEqual("A", table[1].pilotId);
            Assert.IsFalse(table[0].isRemote);
        }

        // ── Leaderboard facade: backend swap ────────────────────────────────

        sealed class FakeBackend : ILeaderboardBackend
        {
            public readonly List<GhostRecord> Submitted = new List<GhostRecord>();
            public string Name => "Fake";
            public bool IsRemote => true;
            public string Status => "ready";
            public void SubmitBest(GhostRecord run) => Submitted.Add(run);
            public void FetchTop(int level, int dailySeed, int limit, Action<IReadOnlyList<LeaderboardEntry>> done)
                => done(new List<LeaderboardEntry> { new LeaderboardEntry { rank = 1, pilot = "REMOTE", isRemote = true } });
            public void FetchAroundMe(int level, int dailySeed, int span, Action<IReadOnlyList<LeaderboardEntry>> done)
                => done(new List<LeaderboardEntry>());
            public void FetchGhost(int level, int dailySeed, LeaderboardEntry entry, Action<GhostRecord> done)
                => done(null);
        }

        [TearDown]
        public void RestoreLocalBackend() => Leaderboard.Configure(null);

        [Test]
        public void Facade_RoutesToConfiguredBackend_ThenRestores()
        {
            Assert.IsFalse(Leaderboard.IsOnline, "default backend is local");

            var fake = new FakeBackend();
            Leaderboard.Configure(fake);
            Assert.IsTrue(Leaderboard.IsOnline);

            Leaderboard.SubmitBest(G("me", 1, won: true, 200));
            Assert.AreEqual(1, fake.Submitted.Count);

            IReadOnlyList<LeaderboardEntry> got = null;
            Leaderboard.RefreshTop(4, 77, 8, r => got = r);
            Assert.AreEqual(1, got.Count);
            Assert.AreEqual("REMOTE", got[0].pilot);

            Leaderboard.Configure(null);
            Assert.IsFalse(Leaderboard.IsOnline, "null restores the local backend");
        }
    }
}
