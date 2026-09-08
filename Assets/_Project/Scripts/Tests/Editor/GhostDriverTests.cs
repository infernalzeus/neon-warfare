using System;
using System.Collections.Generic;
using NUnit.Framework;
using NW.App;
using NW.Combat.Domain;
using NW.Domain;

namespace NW.Tests
{
    /// <summary>
    /// Covers the deterministic half of the ghost system: <see cref="GhostDriver"/> replay
    /// into a live <see cref="CombatSim"/>, and the pure <see cref="RankLadder"/> maths
    /// (Elo delta, daily-board standings). Nothing here touches the filesystem or PlayerPrefs.
    /// </summary>
    public class GhostDriverTests
    {
        static CombatSim NewSim() =>
            new CombatSim(playerCoreHp: 500, enemyCoreHp: 500, new DirectorConfig(), new Rng(11));

        static GhostRecord Rec(params GhostAction[] actions)
        {
            var r = new GhostRecord { level = 1, seed = 123, pilot = "GHOST", mmr = 1200 };
            r.actions = new List<GhostAction>(actions);
            return r;
        }

        static GhostAction Deploy(int tick, string unit, int lane) => new GhostAction
        { tick = tick, type = (int)GhostActionType.Deploy, unit = unit, lane = lane, amount = 0f };

        static GhostAction Cross(int tick, GhostActionType kind, float dmg) => new GhostAction
        { tick = tick, type = (int)kind, unit = "", lane = -1, amount = dmg };

        static int AliveEnemies(CombatSim sim)
        {
            int n = 0;
            foreach (var u in sim.Units) if (u.Alive && u.Team == Team.Enemy) n++;
            return n;
        }

        // ── replay ──────────────────────────────────────────────────────────────

        [Test]
        public void Deploy_SpawnsEnemy_OnRecordedTick()
        {
            var sim = NewSim();
            var driver = new GhostDriver(Rec(Deploy(tick: 5, "trooper", lane: 0)), sim);

            for (int i = 0; i < 4; i++) { sim.Tick(); driver.Step(sim.TickCount); }
            Assert.AreEqual(0, AliveEnemies(sim), "nothing before the action's tick");

            sim.Tick(); driver.Step(sim.TickCount);   // tick 5
            Assert.AreEqual(1, AliveEnemies(sim), "enemy spawns exactly on tick 5");
        }

        [Test]
        public void Crossover_DamagesPlayerCore_ByRecordedAmount()
        {
            var sim = NewSim();
            float before = sim.PlayerCoreHp;
            var driver = new GhostDriver(Rec(Cross(3, GhostActionType.Orbital, 40f)), sim);

            for (int i = 0; i < 3; i++) { sim.Tick(); driver.Step(sim.TickCount); }

            Assert.AreEqual(before - 40f, sim.PlayerCoreHp, 0.001f);
        }

        [Test]
        public void Crossover_FiresViewCallback_WithMappedKind()
        {
            var sim = NewSim();
            CrossoverKind? seen = null;
            var driver = new GhostDriver(Rec(Cross(2, GhostActionType.Emp, 25f)), sim)
            {
                OnGhostCrossover = (k, l, d) => seen = k,
            };
            for (int i = 0; i < 2; i++) { sim.Tick(); driver.Step(sim.TickCount); }

            Assert.AreEqual(CrossoverKind.Emp, seen, "GhostActionType.Emp maps to CrossoverKind.Emp");
        }

        [Test]
        public void Replay_IsDeterministic_AcrossTwoRuns()
        {
            var rec = Rec(
                Deploy(2, "drone", 0), Deploy(2, "trooper", 1), Deploy(8, "mech", 2),
                Cross(5, GhostActionType.Laser, 15f), Cross(12, GhostActionType.Orbital, 40f));

            (float p, float e, int alive) Play()
            {
                var sim = NewSim();
                var d = new GhostDriver(rec, sim);
                for (int i = 0; i < 400 && !sim.Finished; i++) { sim.Tick(); d.Step(sim.TickCount); }
                return (sim.PlayerCoreHp, sim.EnemyCoreHp, sim.Units.Count);
            }

            Assert.AreEqual(Play(), Play(), "same record + same seed ⇒ identical battle");
        }

        [Test]
        public void NullRecord_IsInertNoOp()
        {
            var sim = NewSim();
            var driver = new GhostDriver(null, sim);
            Assert.DoesNotThrow(() => { for (int i = 0; i < 20; i++) { sim.Tick(); driver.Step(sim.TickCount); } });
            Assert.AreEqual(0, AliveEnemies(sim));
            Assert.AreEqual(500f, sim.PlayerCoreHp);
            Assert.IsTrue(driver.Exhausted);
        }

        [Test]
        public void UnknownUnitId_FallsBackToTrooper_StillSpawns()
        {
            var sim = NewSim();
            var driver = new GhostDriver(Rec(Deploy(1, "not-a-real-unit", 0)), sim);
            sim.Tick(); driver.Step(sim.TickCount);
            Assert.AreEqual(1, AliveEnemies(sim), "an unmapped id must not silently drop the deploy");
        }

        // ── RankLadder.Delta ────────────────────────────────────────────────────

        [Test]
        public void Delta_WinIsPositive_LossIsNegative()
        {
            Assert.Greater(RankLadder.Delta(won: true,  myMmr: 1200, oppMmr: 1200), 0);
            Assert.Less   (RankLadder.Delta(won: false, myMmr: 1200, oppMmr: 1200), 0);
        }

        [Test]
        public void Delta_UpsetBeatsExpectedWin()
        {
            int upset    = RankLadder.Delta(true, myMmr: 1000, oppMmr: 1600); // beat someone far above
            int expected = RankLadder.Delta(true, myMmr: 1600, oppMmr: 1000); // beat someone far below
            Assert.Greater(upset, expected);
        }

        [Test]
        public void Delta_IsClampedToSwingBounds()
        {
            for (int opp = 0; opp <= 3000; opp += 250)
            {
                int w = Math.Abs(RankLadder.Delta(true,  1200, opp));
                int l = Math.Abs(RankLadder.Delta(false, 1200, opp));
                Assert.That(w, Is.InRange(6, 40));
                Assert.That(l, Is.InRange(6, 40));
            }
        }

        // ── RankLadder.Standings ────────────────────────────────────────────────

        [Test]
        public void Standings_RanksWinsThenSpeed_ExcludesOtherDays()
        {
            var day = new DateTime(2026, 9, 5, 12, 0, 0, DateTimeKind.Utc);
            int seed = RankLadder.DailySeed(3, day);

            GhostRecord G(string who, bool won, int ticks, int s) => new GhostRecord
            {
                id = who, pilot = who, level = 3, seed = s, mmr = 1200,
                result = won ? 1 : 0, durationTicks = ticks,
                recordedUtc = day.ToBinary(), actions = new List<GhostAction> { Deploy(1, "drone", 0) },
            };

            var pool = new List<GhostRecord>
            {
                G("A", won: true,  ticks: 200, s: seed),
                G("B", won: true,  ticks: 150, s: seed),   // faster win → rank 1
                G("C", won: false, ticks: 300, s: seed),   // any loss ranks below any win
                G("OLD", won: true, ticks: 10, s: seed + 999), // different board → excluded
            };

            var table = RankLadder.Standings(pool, level: 3, mePilot: "B", nowUtc: day);

            Assert.AreEqual(3, table.Count, "the other-day ghost is not on this board");
            Assert.AreEqual("B", table[0].pilot);
            Assert.AreEqual("A", table[1].pilot);
            Assert.AreEqual("C", table[2].pilot);
            Assert.AreEqual(1, table[0].rank);
            Assert.IsTrue(table[0].isYou, "mePilot match flags the row");
            Assert.AreEqual(1, RankLadder.YourRank(pool, 3, "B", day));
        }

        [Test]
        public void TierName_ClimbsWithMmr()
        {
            Assert.AreEqual("BRONZE",   RankLadder.TierName(800));
            Assert.AreEqual("GOLD",     RankLadder.TierName(1200));
            Assert.AreEqual("APEX",     RankLadder.TierName(2000));
            Assert.That(RankLadder.TierProgress(1300), Is.InRange(0f, 1f));
        }
    }
}
