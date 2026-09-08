using NUnit.Framework;
using NW.Combat.Domain;
using NW.Domain;

namespace NW.Tests
{
    public class CombatSimTests
    {
        private static UnitSpec Melee(float hp = 100, float dmg = 10, UnitClass cls = UnitClass.Assault) => new()
        {
            Id = "melee", Class = cls, MaxHp = hp, Damage = dmg,
            AttackCooldown = 0.5f, Speed = 10f, Range = 2f,
        };

        private static UnitSpec Ranged(float range = 20f) => new()
        {
            Id = "ranged", Class = UnitClass.Assault, MaxHp = 50, Damage = 8,
            AttackCooldown = 0.5f, Speed = 8f, Range = range, TargetsAir = true,
        };

        private static CombatSim NewSim(DirectorConfig director = null) =>
            new(playerCoreHp: 500, enemyCoreHp: 500, director ?? new DirectorConfig(), new Rng(11));

        private static void Run(CombatSim sim, float seconds)
        {
            int ticks = (int)(seconds / CombatSim.TickDelta);
            for (int i = 0; i < ticks && !sim.Finished; i++) sim.Tick();
        }

        [Test]
        public void OpposingMelees_MeetAndFight_OneDies()
        {
            CombatSim sim = NewSim();
            sim.Spawn(Melee(hp: 100, dmg: 10), Team.Player, lane: 0);
            sim.Spawn(Melee(hp: 50, dmg: 5), Team.Enemy, lane: 0);
            Run(sim, 30f);
            Assert.IsFalse(sim.Units[1].Alive, "weaker enemy must die");
            Assert.IsTrue(sim.Units[0].Alive, "stronger player unit survives");
        }

        [Test]
        public void UnopposedUnit_ReachesCore_AndWins()
        {
            CombatSim sim = NewSim();
            sim.Spawn(Melee(dmg: 100), Team.Player, lane: 1);
            Run(sim, 60f);
            Assert.IsTrue(sim.Finished);
            Assert.AreEqual(Team.Player, sim.Winner);
            Assert.LessOrEqual(sim.EnemyCoreHp, 0f);
        }

        [Test]
        public void GroundMelee_CannotHitAir()
        {
            CombatSim sim = NewSim();
            var air = new UnitSpec
            {
                Id = "air", Class = UnitClass.Air, MaxHp = 80, Damage = 0,
                AttackCooldown = 1f, Speed = 0.01f, Range = 1f, IsAir = true,
                TargetsGround = false, TargetsAir = false,
            };
            sim.Spawn(Melee(), Team.Player, lane: 0);   // cannot target air
            sim.Spawn(air, Team.Enemy);
            Run(sim, 5f);
            Assert.IsTrue(sim.Units[1].Alive, "melee without TargetsAir must ignore air units");
        }

        [Test]
        public void AntiAirRanged_KillsAir()
        {
            CombatSim sim = NewSim();
            sim.Spawn(Ranged(), Team.Player, lane: 0);
            var air = new UnitSpec
            {
                Id = "air", Class = UnitClass.Air, MaxHp = 40, Damage = 0,
                AttackCooldown = 1f, Speed = 5f, Range = 1f, IsAir = true,
                TargetsGround = false,
            };
            sim.Spawn(air, Team.Enemy);
            Run(sim, 30f);
            Assert.IsFalse(sim.Units[1].Alive, "anti-air ranged must shoot down the flyer");
        }

        [Test]
        public void ClassTriangle_GrantsBonus()
        {
            Assert.AreEqual(CombatSim.ClassTriangleBonus, CombatSim.TriangleMult(UnitClass.Assault, UnitClass.Special));
            Assert.AreEqual(CombatSim.ClassTriangleBonus, CombatSim.TriangleMult(UnitClass.Special, UnitClass.Heavy));
            Assert.AreEqual(CombatSim.ClassTriangleBonus, CombatSim.TriangleMult(UnitClass.Heavy, UnitClass.Assault));
            Assert.AreEqual(1f, CombatSim.TriangleMult(UnitClass.Assault, UnitClass.Heavy), "reverse direction: no bonus");
            Assert.AreEqual(1f, CombatSim.TriangleMult(UnitClass.Air, UnitClass.Heavy), "air sits outside the triangle");
        }

        [Test]
        public void Director_SpawnsWithinBudget_Waves()
        {
            var director = new DirectorConfig { BaseBudgetPerSecond = 10f, DifficultyMult = 1f };
            director.SpawnTable.Add((Melee(hp: 10), 1f));
            CombatSim sim = NewSim(director);
            Run(sim, 10f);
            int enemySpawns = 0;
            foreach (CombatEvent e in sim.Events)
                if (e.Type == CombatEventType.Spawned && e.Team == Team.Enemy) enemySpawns++;
            Assert.Greater(enemySpawns, 0, "director must spawn");
            // 10 budget/s Ã— 10s â‰ˆ 100+ramp budget at cost 10 â‡’ roughly 10â€“14 spawns, never more than bank allows.
            Assert.LessOrEqual(enemySpawns, 20, "director must respect its budget");
        }

        [Test]
        public void Determinism_SameSeed_SameOutcome()
        {
            float Play(int seed)
            {
                var director = new DirectorConfig { BaseBudgetPerSecond = 5f };
                director.SpawnTable.Add((Melee(hp: 30), 1f));
                var sim = new CombatSim(500, 500, director, new Rng(seed));
                sim.Spawn(Melee(hp: 300, dmg: 20), Team.Player, 0);
                for (int i = 0; i < 2000 && !sim.Finished; i++) sim.Tick();
                return sim.PlayerCoreHp + sim.EnemyCoreHp * 1000f;
            }
            Assert.AreEqual(Play(99), Play(99), "fixed seed â‡’ identical battle");
        }
    }
}
