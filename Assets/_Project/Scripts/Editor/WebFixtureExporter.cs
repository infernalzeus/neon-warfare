using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using NW.App;
using NW.Board.Domain;
using NW.Combat.Domain;
using NW.Data;
using NW.Domain;
using UnityEditor;
using UnityEngine;

namespace NW.Editor
{
    /// <summary>
    /// Golden-master fixture exporter for the TypeScript web port. Runs the REAL C# sim, board
    /// and session with fixed inputs and writes what they did to docs/web-port/fixtures/*.json.
    /// The port is correct only when it reproduces every fixture exactly. Run headless:
    ///   Unity.exe -batchmode -nographics -quit -projectPath &lt;repo&gt; -executeMethod NW.Editor.WebFixtureExporter.Export
    /// Floats are written with "R" (round-trip) so a JS reader recovers the exact float32 via
    /// Math.fround(Number(text)). State hashes use FNV-1a over 32-bit words (see docs/design/19-web-port.md).
    /// </summary>
    public static class WebFixtureExporter
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Fixed order: index in this list is the "specIdx" used by event/state rows.
        static readonly string[] CatalogOrder =
            { "drone", "trooper", "sniper", "mech", "shield-bot", "interceptor", "hacker", "titan", "turret" };

        static readonly Dictionary<string, int> Coverage = new Dictionary<string, int>();

        [MenuItem("NW/Export Web Port Fixtures")]
        public static void Export()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string dir = Path.Combine(root, "docs", "web-port", "fixtures");
            Directory.CreateDirectory(dir);
            Coverage.Clear();

            Write(dir, "rng.json", RngFixture());
            Write(dir, "tables.json", TablesFixture());
            Write(dir, "formulas.json", FormulaFixture());

            var combat = new StringBuilder("[");
            var cs = CombatScenarios();
            for (int i = 0; i < cs.Count; i++)
            {
                if (i > 0) combat.Append(',');
                string json = RunCombat(cs[i]);
                combat.Append('\n').Append(json);
                File.WriteAllText(Path.Combine(dir, "combat_" + cs[i].Name + ".json"), json);
            }
            // (individual files are the fixtures; the array is only used for the size report)
            Debug.Log($"[WebFixtures] combat scenarios: {cs.Count}");

            var boards = BoardScenarios();
            foreach (var b in boards) File.WriteAllText(Path.Combine(dir, "board_" + b.name + ".json"), b.json);
            Debug.Log($"[WebFixtures] board scenarios: {boards.Count}");

            foreach (var s in SessionScenarios())
                File.WriteAllText(Path.Combine(dir, "session_" + s.name + ".json"), s.json);

            var sb = new StringBuilder();
            foreach (var kv in Coverage) sb.Append(kv.Key).Append('=').Append(kv.Value).Append("; ");
            Debug.Log("[WebFixtures] coverage: " + sb);
            File.WriteAllText(Path.Combine(dir, "_coverage.txt"), sb.ToString().Replace("; ", "\n"));
            Debug.Log("[WebFixtures] DONE -> " + dir);
        }

        static void Write(string dir, string name, string json) => File.WriteAllText(Path.Combine(dir, name), json);

        // ------------------------------------------------------------------ helpers

        static string F(float f) => f.ToString("R", Inv);
        static int Bits(float f) => BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
        static string B(bool b) => b ? "1" : "0";
        static string Q(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char ch in s)
            {
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '"') sb.Append("\\\"");
                else if (ch < 0x20) sb.Append("\\u").Append(((int)ch).ToString("x4"));
                else sb.Append(ch);
            }
            return sb.Append('"').ToString();
        }

        static Dictionary<string, int> SnapCov() => new Dictionary<string, int>(Coverage);
        static void RestoreCov(Dictionary<string, int> b) { Coverage.Clear(); foreach (var kv in b) Coverage[kv.Key] = kv.Value; }

        static void Cov(string key) { Coverage.TryGetValue(key, out int n); Coverage[key] = n + 1; }

        sealed class Hasher
        {
            public uint H = 2166136261u;
            public void W(int w) { unchecked { H = (H ^ (uint)w) * 16777619u; } }
            public void Fl(float f) { W(Bits(f)); }
        }

        sealed class Lcg
        {
            uint _s;
            public Lcg(uint seed) { _s = seed; }
            public int Next(int n) { unchecked { _s = _s * 1664525u + 1013904223u; return (int)((_s >> 8) % (uint)n); } }
        }

        static readonly FieldInfo FBank = typeof(CombatSim).GetField("_directorBank", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FNext = typeof(CombatSim).GetField("_nextId", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FDir  = typeof(CombatSim).GetField("_director", BindingFlags.NonPublic | BindingFlags.Instance);

        static int SpecIdx(UnitSpec s)
        {
            if (s == null) return -1;
            for (int i = 0; i < CatalogOrder.Length; i++) if (UnitCatalog.Get(CatalogOrder[i]) == s) return i;
            return -1;
        }

        static uint CombatHash(CombatSim s, Func<UnitSpec, int> specIdx)
        {
            var h = new Hasher();
            h.W(s.TickCount); h.W(s.Act); h.W((int)s.SurgeOwner); h.W(s.TelegraphLane);
            h.Fl(s.TelegraphRemaining); h.Fl(s.Time); h.Fl(s.PlayerCoreHp); h.Fl(s.EnemyCoreHp);
            h.Fl((float)FBank.GetValue(s));
            unchecked { h.W((int)(uint)(ulong)FNext.GetValue(s)); }
            h.W(s.Finished ? 1 : 0); h.W((int)s.Winner);
            for (int i = 0; i < 4; i++) h.Fl(s.Frontline[i]);
            foreach (var p in s.Pylons) { h.W((int)p.Owner); h.Fl(p.Progress); h.W(p.Contested ? 1 : 0); }
            h.W(s.Units.Count);
            foreach (var u in s.Units)
            {
                h.W(u.Alive ? 1 : 0);
                unchecked { h.W((int)(uint)u.Id); }
                h.W(specIdx(u.Spec)); h.W((int)u.Team); h.W(u.Lane);
                h.Fl(u.X); h.Fl(u.Hp); h.Fl(u.Cooldown); h.Fl(u.Stun); h.Fl(u.Slow); h.W(u.TargetIndex);
            }
            return h.H;
        }

        static string Snapshot(CombatSim s, Func<UnitSpec, int> specIdx)
        {
            var sb = new StringBuilder();
            sb.Append("{\"tick\":").Append(s.TickCount)
              .Append(",\"time\":").Append(F(s.Time))
              .Append(",\"act\":").Append(s.Act)
              .Append(",\"surge\":").Append((int)s.SurgeOwner)
              .Append(",\"tele\":").Append(s.TelegraphLane)
              .Append(",\"teleRem\":").Append(F(s.TelegraphRemaining))
              .Append(",\"pCore\":").Append(F(s.PlayerCoreHp))
              .Append(",\"eCore\":").Append(F(s.EnemyCoreHp))
              .Append(",\"bank\":").Append(F((float)FBank.GetValue(s)))
              .Append(",\"nextId\":").Append((ulong)FNext.GetValue(s))
              .Append(",\"finished\":").Append(B(s.Finished))
              .Append(",\"winner\":").Append((int)s.Winner)
              .Append(",\"frontline\":[");
            for (int i = 0; i < 4; i++) { if (i > 0) sb.Append(','); sb.Append(F(s.Frontline[i])); }
            sb.Append("],\"pylons\":[");
            bool first = true;
            foreach (var p in s.Pylons)
            {
                if (!first) sb.Append(','); first = false;
                sb.Append('[').Append((int)p.Owner).Append(',').Append(F(p.Progress)).Append(',').Append(B(p.Contested)).Append(']');
            }
            sb.Append("],\"units\":[");
            for (int i = 0; i < s.Units.Count; i++)
            {
                var u = s.Units[i];
                if (i > 0) sb.Append(',');
                sb.Append('[').Append(B(u.Alive)).Append(',').Append(u.Id).Append(',').Append(specIdx(u.Spec))
                  .Append(',').Append((int)u.Team).Append(',').Append(u.Lane)
                  .Append(',').Append(F(u.X)).Append(',').Append(F(u.Hp)).Append(',').Append(F(u.Cooldown))
                  .Append(',').Append(F(u.Stun)).Append(',').Append(F(u.Slow)).Append(',').Append(u.TargetIndex).Append(']');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static void DrainEvents(CombatSim sim, int tick, int phase, StringBuilder rows, ref int count)
        {
            foreach (var e in sim.Events)
            {
                if (count++ > 0) rows.Append(',');
                rows.Append('[').Append(tick).Append(',').Append(phase).Append(',').Append((int)e.Type)
                    .Append(',').Append(e.Unit).Append(',').Append(e.Target).Append(',').Append(F(e.Amount))
                    .Append(',').Append((int)e.Team).Append(',').Append(B(e.Flag)).Append(']');
                Cov("combat.event." + e.Type);
            }
            sim.Events.Clear();
        }

        // ------------------------------------------------------------------ rng

        static string RngFixture()
        {
            int[] seeds = { 0, 1, -1, 2, 42, 12345, 628091837, 2147483647, -2147483648 };
            var sb = new StringBuilder("{\"algorithm\":\"xorshift128, see Rng.cs\",\"cases\":[");
            for (int si = 0; si < seeds.Length; si++)
            {
                if (si > 0) sb.Append(',');
                var r = new Rng(seeds[si]);
                sb.Append("{\"seed\":").Append(seeds[si]).Append(",\"nextUInt\":[");
                for (int i = 0; i < 64; i++) { if (i > 0) sb.Append(','); sb.Append(r.NextUInt()); }
                sb.Append("],\"next7\":[");
                var r2 = new Rng(seeds[si]);
                for (int i = 0; i < 24; i++) { if (i > 0) sb.Append(','); sb.Append(r2.Next(7)); }
                sb.Append("],\"range3to9\":[");
                var r3 = new Rng(seeds[si]);
                for (int i = 0; i < 24; i++) { if (i > 0) sb.Append(','); sb.Append(r3.Range(3, 9)); }
                sb.Append("],\"next01\":[");
                var r4 = new Rng(seeds[si]);
                for (int i = 0; i < 24; i++) { if (i > 0) sb.Append(','); sb.Append(F(r4.Next01())); }
                sb.Append("],\"next0\":").Append(new Rng(seeds[si]).Next(0));
                sb.Append('}');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ tables

        static string SpecJson(UnitSpec u)
        {
            return "{\"id\":" + Q(u.Id) + ",\"class\":" + (int)u.Class + ",\"maxHp\":" + F(u.MaxHp) + ",\"damage\":" + F(u.Damage)
                 + ",\"attackCooldown\":" + F(u.AttackCooldown) + ",\"speed\":" + F(u.Speed) + ",\"range\":" + F(u.Range)
                 + ",\"aoeRadius\":" + F(u.AoeRadius) + ",\"isAir\":" + B(u.IsAir) + ",\"targetsAir\":" + B(u.TargetsAir)
                 + ",\"targetsGround\":" + B(u.TargetsGround) + ",\"canCapture\":" + B(u.CanCapture)
                 + ",\"spawnAtPylon\":" + B(u.SpawnAtPylon) + ",\"directorCost\":" + u.DirectorCost + "}";
        }

        static string TablesFixture()
        {
            var sb = new StringBuilder("{\n\"constants\":{");
            sb.Append("\"tickDelta\":").Append(F(CombatSim.TickDelta))
              .Append(",\"groundLanes\":").Append(CombatSim.GroundLanes)
              .Append(",\"airLane\":").Append(CombatSim.AirLane)
              .Append(",\"laneLength\":").Append(F(CombatSim.LaneLength))
              .Append(",\"deploySpawnX\":").Append(F(CombatSim.DeploySpawnX))
              .Append(",\"classTriangleBonus\":").Append(F(CombatSim.ClassTriangleBonus))
              .Append(",\"maxActivePerTeam\":").Append(CombatSim.MaxActivePerTeam)
              .Append(",\"coreDefenseRange\":").Append(F(CombatSim.CoreDefenseRange))
              .Append(",\"coreDamagePerSec\":").Append(F(CombatSim.CoreDamagePerSec))
              .Append(",\"laneSpeedBonus\":").Append(F(CombatSim.LaneSpeedBonus))
              .Append(",\"surgeDamageMult\":").Append(F(CombatSim.SurgeDamageMult))
              .Append(",\"slowMult\":").Append(F(CombatSim.SlowMult))
              .Append(",\"act2Time\":").Append(F(CombatSim.Act2Time))
              .Append(",\"act3Time\":").Append(F(CombatSim.Act3Time))
              .Append(",\"laserDamage\":").Append(F(BattleSession.LaserDamage))
              .Append(",\"orbitalDamage\":").Append(F(BattleSession.OrbitalDamage))
              .Append("},\n\"enumNotes\":{\"UnitClass\":\"0 Assault,1 Heavy,2 Air,3 Special\",\"Team\":\"0 Player,1 Enemy\",\"Owner\":\"0 Neutral,1 Player,2 Enemy\",")
              .Append("\"CombatEventType\":\"0 Spawned,1 Hit,2 Died,3 CoreHit,4 WaveTelegraph,5 WaveSpawned,6 PylonCaptured,7 LaneControlChanged,8 SurgeChanged,9 ActChanged,10 Finished\",")
              .Append("\"GemKind\":\"0 Energy,1 Plasma,2 Nano,3 Quantum,4 Data\",\"SpecialKind\":\"0 None,1 LaserH,2 LaserV,3 Cross,4 Singularity\",")
              .Append("\"BoardEventType\":\"0 Swapped,1 SwapRejected,2 TileCleared,3 SpecialCreated,4 SpecialDetonated,5 ResourcePayout,6 TileFell,7 TileSpawned,8 CascadeStep,9 ComboMilestone,10 EmpTriggered,11 BoardReshuffled,12 ResolveEnded\"},\n");

            sb.Append("\"catalogOrder\":[");
            for (int i = 0; i < CatalogOrder.Length; i++) { if (i > 0) sb.Append(','); sb.Append(Q(CatalogOrder[i])); }
            sb.Append("],\n\"catalog\":[");
            for (int i = 0; i < CatalogOrder.Length; i++) { if (i > 0) sb.Append(",\n"); sb.Append(SpecJson(UnitCatalog.Get(CatalogOrder[i]))); }
            sb.Append("],\n");

            // The game database that the shipping BattleScene loads (Resources/GameDatabase).
            var db = Resources.Load<GameDatabase>("GameDatabase");
            bool dbPath = db != null && db.units != null && db.units.Count > 0;
            sb.Append("\"gameDatabase\":{\"loaded\":").Append(B(db != null)).Append(",\"unitCount\":").Append(db != null && db.units != null ? db.units.Count : 0)
              .Append(",\"battleSessionUsesThisPath\":").Append(B(dbPath)).Append(",\"units\":[");
            if (db != null && db.units != null)
                for (int i = 0; i < db.units.Count; i++)
                {
                    var d = db.units[i]; if (d == null) continue;
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"def\":").Append(SpecJson(d.ToSpec())).Append(",\"displayName\":").Append(Q(d.displayName ?? ""))
                      .Append(",\"cost\":[").Append(d.cost.energy).Append(',').Append(d.cost.plasma).Append(',').Append(d.cost.nano)
                      .Append(',').Append(d.cost.quantum).Append(',').Append(d.cost.data).Append("],\"compileCooldown\":").Append(F(d.compileCooldown)).Append('}');
                }
            sb.Append("]},\n");

            // Level tables 1..20 and what BattleSession really builds for them.
            sb.Append("\"levels\":[");
            for (int lv = 1; lv <= LevelConfig.MaxLevel; lv++)
            {
                if (lv > 1) sb.Append(",\n");
                sb.Append("{\"level\":").Append(lv)
                  .Append(",\"boardEdge\":").Append(LevelConfig.BoardEdge(lv))
                  .Append(",\"gemColorCount\":").Append(LevelConfig.GemColorCount(lv))
                  .Append(",\"directorMult\":").Append(F(LevelConfig.DirectorMult(lv)))
                  .Append(",\"coreHp\":").Append(F(LevelConfig.CoreHp(lv)))
                  .Append(",\"activeGroundLanes\":").Append(LevelConfig.ActiveGroundLanes(lv))
                  .Append(",\"baseTokenReward\":").Append(LevelConfig.BaseTokenReward(lv))
                  .Append(",\"availableTroops\":[");
                var av = LevelConfig.AvailableTroops(lv);
                for (int i = 0; i < av.Count; i++) { if (i > 0) sb.Append(','); sb.Append(Q(av[i])); }
                var sess = new BattleSession(db, LevelConfig.DirectorMult(lv), 1, LevelConfig.GemColorCount(lv), lv, true);
                sb.Append("],\"session\":{\"deployOptions\":[");
                for (int i = 0; i < sess.DeployOptions.Count; i++)
                {
                    var o = sess.DeployOptions[i];
                    if (i > 0) sb.Append(',');
                    sb.Append("{\"name\":").Append(Q(o.name)).Append(",\"unit\":").Append(Q(o.spec.Id)).Append(",\"cost\":[");
                    for (int c = 0; c < o.cost.Length; c++) { if (c > 0) sb.Append(','); sb.Append(o.cost[c]); }
                    sb.Append("],\"cooldown\":").Append(F(o.cooldown)).Append('}');
                }
                sb.Append("],\"enemySpawnTable\":[");
                var dir = (DirectorConfig)FDir.GetValue(sess.Combat);
                for (int i = 0; i < dir.SpawnTable.Count; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append('[').Append(Q(dir.SpawnTable[i].spec.Id)).Append(',').Append(F(dir.SpawnTable[i].weight)).Append(']');
                }
                sb.Append("],\"director\":{\"baseBudgetPerSecond\":").Append(F(dir.BaseBudgetPerSecond))
                  .Append(",\"waveThreshold\":").Append(F(dir.WaveThreshold)).Append(",\"rampPer90Seconds\":").Append(F(dir.RampPer90Seconds))
                  .Append(",\"maxBank\":").Append(F(dir.MaxBank)).Append(",\"telegraphSeconds\":").Append(F(dir.TelegraphSeconds))
                  .Append("},\"startResources\":[");
                for (int i = 0; i < sess.Resources.Length; i++) { if (i > 0) sb.Append(','); sb.Append(sess.Resources[i]); }
                sb.Append("]}}");
            }
            sb.Append("],\n");
            sb.Append("\"comboMultiplier\":[");
            for (int c = 0; c <= 10; c++) { if (c > 0) sb.Append(','); sb.Append(F(BoardModel.ComboMultiplier(c))); }
            sb.Append("],\n\"shopBoostMultsByLevel\":{\"damage\":[");
            for (int i = 0; i < PlayerProgress.BoostDamageMult.Length; i++) { if (i > 0) sb.Append(','); sb.Append(F(PlayerProgress.BoostDamageMult[i])); }
            sb.Append("],\"speed\":[");
            for (int i = 0; i < PlayerProgress.BoostSpeedMult.Length; i++) { if (i > 0) sb.Append(','); sb.Append(F(PlayerProgress.BoostSpeedMult[i])); }
            sb.Append("]}\n}");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ pure formulas

        static string FormulaFixture()
        {
            var sb = new StringBuilder("{\n\"dailySeed\":[");
            var dates = new[] { new DateTime(2026, 1, 1), new DateTime(2026, 10, 27), new DateTime(2026, 12, 31), new DateTime(2027, 2, 28), new DateTime(2024, 2, 29), new DateTime(2030, 6, 15) };
            bool first = true;
            foreach (var d in dates)
                foreach (int lv in new[] { 1, 4, 10, 20 })
                {
                    if (!first) sb.Append(','); first = false;
                    sb.Append("{\"y\":").Append(d.Year).Append(",\"m\":").Append(d.Month).Append(",\"d\":").Append(d.Day)
                      .Append(",\"level\":").Append(lv).Append(",\"seed\":").Append(RankLadder.DailySeed(lv, DateTime.SpecifyKind(d, DateTimeKind.Utc))).Append('}');
                }
            sb.Append("],\n\"sortKey\":[");
            first = true;
            foreach (bool won in new[] { false, true })
                foreach (int t in new[] { -1, 0, 1, 2, 100, 20000, 999999999, 1000000000 })
                {
                    if (!first) sb.Append(','); first = false;
                    sb.Append("{\"won\":").Append(B(won)).Append(",\"ticks\":").Append(t).Append(",\"key\":").Append(RankLadder.SortKey(won, t)).Append('}');
                }
            sb.Append("],\n\"scoreTicks\":[");
            first = true;
            foreach (float spd in new[] { 0.04f, 0.05f, 0.051f, 0.5f, 1f, 1.5f, 2.5f, 3f, -1f })
                foreach (int dur in new[] { 1, 2, 3, 5, 7, 10, 11, 15, 100, 101, 1235, 12345 })
                {
                    var rec = new GhostRecorder(1, 0, 1, 3, 1f);
                    rec.RecordDeploy(1, "trooper", 0);
                    var g = rec.Finish(true, dur, spd);
                    if (!first) sb.Append(','); first = false;
                    sb.Append("{\"duration\":").Append(dur).Append(",\"speedIn\":").Append(F(spd)).Append(",\"speedStored\":").Append(F(g.speed))
                      .Append(",\"scoreTicks\":").Append(g.scoreTicks).Append('}');
                }
            sb.Append("],\n\"eloDelta\":[");
            first = true;
            foreach (bool won in new[] { false, true })
                foreach (int my in new[] { 800, 1200, 1800 })
                    foreach (int opp in new[] { 600, 1200, 1700, 2200 })
                    {
                        if (!first) sb.Append(','); first = false;
                        sb.Append("{\"won\":").Append(B(won)).Append(",\"my\":").Append(my).Append(",\"opp\":").Append(opp)
                          .Append(",\"delta\":").Append(RankLadder.Delta(won, my, opp)).Append('}');
                    }
            sb.Append("],\n\"tiers\":[");
            first = true;
            foreach (int m in new[] { 0, 500, 999, 1000, 1199, 1200, 1399, 1400, 1599, 1600, 1799, 1800, 2400 })
            {
                if (!first) sb.Append(','); first = false;
                sb.Append("{\"mmr\":").Append(m).Append(",\"tier\":").Append(Q(RankLadder.TierName(m)))
                  .Append(",\"progress\":").Append(F(RankLadder.TierProgress(m))).Append(",\"next\":").Append(RankLadder.NextTierAt(m)).Append('}');
            }
            sb.Append("],\n\"usernameKey\":[");
            first = true;
            var keyOf = typeof(UsernameService).GetMethod("KeyOf", BindingFlags.NonPublic | BindingFlags.Static);
            foreach (string raw in new[] { "Bob", "bob", "  Neo  Matrix ", "A_B-c", "x", "Zeus!!", "ZEUS 99", "café", "Über", "i̇x", "TITLE", "a\tb", "name with   spaces", "ab", "abcdefghijklmnopq", "😀smile", "日本語", "o'neil", "ALL_CAPS_NAME", "\u0130STANBUL", "SS\u00df", "\u03a3IGMA", "\u00c5ngstr\u00f6m", "\u0661\u0662\u0663abc", "x\u2167", "\u00bd half", "a\u00a0b", "a\u2003b", "\ufeffabc", "ab\u200bcd", "  \t  lead", "trail  \n" })
            {
                if (!first) sb.Append(','); first = false;
                string n = UsernameService.Normalize(raw);
                string k = (string)keyOf.Invoke(null, new object[] { raw });
                sb.Append("{\"raw\":").Append(Q(raw)).Append(",\"normalized\":").Append(Q(n)).Append(",\"key\":").Append(Q(k))
                  .Append(",\"valid\":").Append(B(UsernameService.IsValid(raw))).Append('}');
            }
            sb.Append("]\n}");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ combat scenarios

        sealed class Act { public int At; public string Kind; public string Unit; public int Team; public int Lane; public float Amt; }

        sealed class CScen
        {
            public string Name, Note;
            public int RngSeed = 1;
            public float CoreHp = 500f;
            public int Lanes = 3;
            public float DirBase = 1f, DirRamp = 1f, DirMult = 1f, DirMax = 90f, DirWave = 14f, DirTele = 2f;
            public string[] DirTable; public float[] DirWeights;
            public int Ticks = 600, SnapEvery = 100;
            public List<Act> Acts = new List<Act>();
            public void Spawn(int at, string unit, int team, int lane) => Acts.Add(new Act { At = at, Kind = "spawn", Unit = unit, Team = team, Lane = lane });
            public void Do(int at, string kind, float amt = 0f, int lane = 0) => Acts.Add(new Act { At = at, Kind = kind, Amt = amt, Lane = lane });
        }

        static List<CScen> CombatScenarios()
        {
            var L = new List<CScen>();

            var s = new CScen { Name = "duel_melee", Note = "two troopers, one lane", Ticks = 800 };
            s.Spawn(0, "trooper", 0, 0); s.Spawn(0, "trooper", 1, 0); L.Add(s);

            s = new CScen { Name = "triangle_and_blocked_pylon_spawn", Note = "class triangle in 3 lanes; player hacker needs an owned pylon so spawn returns -1", Ticks = 1200 };
            s.Spawn(0, "trooper", 0, 0); s.Spawn(0, "hacker", 1, 0);
            s.Spawn(0, "hacker", 0, 1); s.Spawn(0, "trooper", 1, 1);
            s.Spawn(0, "mech", 0, 2); s.Spawn(0, "sniper", 1, 2);
            s.Spawn(40, "shield-bot", 0, 1); s.Spawn(40, "mech", 1, 1);
            L.Add(s);

            s = new CScen { Name = "air_and_kite", Note = "air lane, anti-air, interceptor kiting", Ticks = 1000 };
            s.Spawn(0, "interceptor", 0, 0); s.Spawn(0, "interceptor", 1, 0);
            s.Spawn(0, "sniper", 0, 0); s.Spawn(0, "sniper", 1, 1);
            s.Spawn(30, "drone", 0, 2); s.Spawn(30, "interceptor", 1, 2);
            L.Add(s);

            s = new CScen { Name = "aoe_titan", Note = "titan splash against a stacked group", Ticks = 1500, CoreHp = 100000f };
            s.Spawn(0, "titan", 0, 1);
            for (int i = 0; i < 5; i++) s.Spawn(i * 25, "trooper", 1, 1);
            s.Spawn(60, "drone", 1, 1);
            L.Add(s);

            foreach (int lanes in new[] { 1, 2, 3 })
            {
                s = new CScen { Name = "pylons_surge_" + lanes + "lane", Note = "player march across the active lanes; pylon capture, lane control, SURGE majority", Lanes = lanes, CoreHp = 400f, Ticks = 2600 };
                for (int i = 0; i < 12; i++) s.Spawn(i * 30, "trooper", 0, i % 3);
                s.Spawn(500, "shield-bot", 0, 0);
                L.Add(s);
            }

            s = new CScen { Name = "field_cap_and_slot_reuse", Note = "MaxActivePerTeam=24 returns -1; dead slots are reused", Ticks = 500, CoreHp = 100000f };
            for (int i = 0; i < 30; i++) s.Spawn(0, "drone", 0, i % 3);
            for (int i = 0; i < 6; i++) s.Spawn(120 + i * 10, "sniper", 1, i % 3);
            s.Spawn(300, "trooper", 0, 0); s.Spawn(300, "trooper", 0, 1);
            L.Add(s);

            s = new CScen { Name = "pylon_spawn_allowed", Note = "after the player owns a pylon, turret and hacker may deploy there", Ticks = 1100, CoreHp = 100000f };
            s.Spawn(0, "trooper", 0, 0); s.Spawn(0, "trooper", 0, 1);
            s.Spawn(300, "turret", 0, 0); s.Spawn(420, "hacker", 0, 0); s.Spawn(420, "turret", 0, 2); s.Spawn(500, "turret", 0, 1);
            s.Spawn(600, "sniper", 1, 0); s.Spawn(600, "drone", 1, 1);
            L.Add(s);

            s = new CScen { Name = "board_crossovers", Note = "laser lane, orbital, stun, slow, direct core hit, heal", Ticks = 700, CoreHp = 100000f };
            s.Spawn(0, "trooper", 1, 0); s.Spawn(0, "mech", 1, 1); s.Spawn(0, "titan", 1, 2); s.Spawn(0, "interceptor", 1, 0);
            s.Spawn(0, "drone", 0, 0);
            s.Do(100, "laser", 15f, 1); s.Do(120, "orbital", 40f); s.Do(140, "stun", 2f); s.Do(260, "slow", 1.5f);
            s.Do(280, "corehit", 25f); s.Do(300, "heal", 40f); s.Do(310, "laser", 15f, 0); s.Do(320, "laser", 15f, 2);
            s.Do(330, "orbital", 40f); s.Do(340, "orbital", 40f); s.Do(350, "orbital", 40f);
            L.Add(s);

            // Melee units reach the core, land ONE hit, then die to the core's defense zone (8 units, 50 dmg/s):
            // a trooper arrives with ~5 HP. Snipers (range 25) shoot from outside the zone, so they end a match cleanly.
            s = new CScen { Name = "unopposed_win_then_noop", Note = "enemy core dies to a sniper; further ticks and actions do nothing", CoreHp = 25f, Ticks = 900 };
            s.Spawn(0, "sniper", 0, 0); s.Spawn(0, "sniper", 0, 1);
            s.Spawn(850, "trooper", 0, 1); s.Do(860, "corehit", 5f); s.Do(870, "heal", 5f);
            L.Add(s);

            s = new CScen { Name = "player_core_dies", Note = "enemy sniper reaches firing range of the player core", CoreHp = 25f, Ticks = 900 };
            s.Spawn(0, "sniper", 1, 2); s.Spawn(0, "sniper", 1, 0);
            L.Add(s);

            s = new CScen { Name = "melee_core_siege_and_defense", Note = "melee units land exactly one hit on the core, then die to core defense; the shield-bot survives long enough to finish it (documents the real balance)", CoreHp = 25f, Ticks = 900 };
            s.Spawn(0, "trooper", 0, 0); s.Spawn(0, "trooper", 0, 0); s.Spawn(0, "shield-bot", 0, 1);
            L.Add(s);

            // Level-shaped director scenarios (parameters taken from BattleSession.AddFallbackUnits + LevelConfig).
            s = new CScen { Name = "director_level1", Note = "1 active lane, level-1 director, enemy drone/trooper table", Lanes = 1, CoreHp = 220f, RngSeed = 12345, DirMult = 0.45f, Ticks = 4000 };
            s.DirTable = new[] { "drone", "trooper" }; s.DirWeights = new[] { 3.0f, 2.0f };
            var g = new Lcg(7);
            for (int t = 20; t < 3800; t += 90) s.Spawn(t, g.Next(2) == 0 ? "drone" : "trooper", 0, 0);
            L.Add(s);

            s = new CScen { Name = "director_level8_long", Note = "3 lanes, all acts (3 starts at tick 6600), crossovers, many waves", Lanes = 3, CoreHp = 100000f, RngSeed = 777, DirMult = 1.024f, Ticks = 7400, SnapEvery = 200 };
            s.DirTable = new[] { "drone", "trooper", "sniper", "mech", "interceptor" }; s.DirWeights = new[] { 3.0f, 2.0f, 1.2f, 1.0f, 1.0f };
            g = new Lcg(99);
            string[] pool = { "drone", "trooper", "sniper", "mech", "shield-bot", "interceptor", "trooper", "drone" };
            for (int t = 10; t < 7200; t += 70) s.Spawn(t, pool[g.Next(pool.Length)], 0, g.Next(3));
            for (int t = 400; t < 7200; t += 900) { s.Do(t, "laser", 15f, g.Next(3)); s.Do(t + 5, "orbital", 40f); }
            s.Do(3000, "stun", 2f); s.Do(5000, "slow", 1.5f);
            L.Add(s);

            s = new CScen { Name = "director_level20_stress", Note = "full roster, high multiplier, titan and hacker in the table", Lanes = 3, CoreHp = 100000f, RngSeed = 4242, DirMult = 2.008f, Ticks = 3600, SnapEvery = 150 };
            s.DirTable = new[] { "drone", "trooper", "sniper", "mech", "interceptor", "hacker", "titan" }; s.DirWeights = new[] { 3.0f, 2.0f, 1.2f, 1.0f, 1.0f, 0.8f, 0.4f };
            g = new Lcg(5);
            string[] pool2 = { "drone", "trooper", "sniper", "mech", "titan", "interceptor", "shield-bot" };
            for (int t = 5; t < 3500; t += 45) s.Spawn(t, pool2[g.Next(pool2.Length)], 0, g.Next(3));
            L.Add(s);

            s = new CScen { Name = "director_two_lane_level5", Note = "2 active lanes", Lanes = 2, CoreHp = 100000f, RngSeed = 31337, DirMult = 0.778f, Ticks = 3000 };
            s.DirTable = new[] { "drone", "trooper", "sniper", "mech" }; s.DirWeights = new[] { 3.0f, 2.0f, 1.2f, 1.0f };
            g = new Lcg(21);
            for (int t = 15; t < 2900; t += 80) s.Spawn(t, pool[g.Next(4)], 0, g.Next(3));
            L.Add(s);

            return L;
        }

        static string RunCombat(CScen c)
        {
            var dir = new DirectorConfig
            {
                BaseBudgetPerSecond = c.DirBase, RampPer90Seconds = c.DirRamp, DifficultyMult = c.DirMult,
                MaxBank = c.DirMax, WaveThreshold = c.DirWave, TelegraphSeconds = c.DirTele,
            };
            if (c.DirTable != null)
                for (int i = 0; i < c.DirTable.Length; i++) dir.SpawnTable.Add((UnitCatalog.Get(c.DirTable[i]), c.DirWeights[i]));

            var sim = new CombatSim(c.CoreHp, c.CoreHp, dir, new Rng(c.RngSeed), c.Lanes);
            Func<UnitSpec, int> idx = SpecIdx;

            var events = new StringBuilder(); int evCount = 0;
            var hashes = new StringBuilder(); var snaps = new StringBuilder(); int snapCount = 0;
            var acts = new StringBuilder(); int actCount = 0;
            var ordered = new List<Act>(); // ordered by tick, ties keep insertion order
            {
                var byAt = new SortedDictionary<int, List<Act>>();
                foreach (var a in c.Acts) { if (!byAt.TryGetValue(a.At, out var l)) byAt[a.At] = l = new List<Act>(); l.Add(a); }
                foreach (var kv in byAt) ordered.AddRange(kv.Value);
            }
            int ai = 0, ticksRun = 0, finishedAt = -1;

            for (int step = 0; step <= c.Ticks; step++)
            {
                while (ai < ordered.Count && ordered[ai].At == step)
                {
                    var a = ordered[ai++];
                    int ret = 0;
                    switch (a.Kind)
                    {
                        case "spawn": ret = sim.Spawn(UnitCatalog.Get(a.Unit), a.Team == 0 ? Team.Player : Team.Enemy, a.Lane); break;
                        case "laser": sim.DamageLaneEnemies(a.Lane, a.Amt); break;
                        case "orbital": sim.DamageStrongestEnemy(a.Amt); break;
                        case "stun": sim.StunEnemies(a.Amt); break;
                        case "slow": sim.SlowEnemies(a.Amt); break;
                        case "corehit": sim.DamagePlayerCore(a.Amt); break;
                        case "heal": sim.HealPlayerCore(a.Amt); break;
                    }
                    if (actCount++ > 0) acts.Append(',');
                    acts.Append("{\"at\":").Append(a.At).Append(",\"kind\":").Append(Q(a.Kind));
                    if (a.Unit != null) acts.Append(",\"unit\":").Append(Q(a.Unit)).Append(",\"team\":").Append(a.Team);
                    acts.Append(",\"lane\":").Append(a.Lane).Append(",\"amt\":").Append(F(a.Amt)).Append(",\"ret\":").Append(ret).Append('}');
                }
                DrainEvents(sim, step, 0, events, ref evCount);
                if (step == c.Ticks) break;
                if (sim.Finished && finishedAt < 0) finishedAt = step;
                sim.Tick();
                ticksRun = step + 1;
                DrainEvents(sim, step + 1, 1, events, ref evCount);
                if (step > 0) hashes.Append(',');
                hashes.Append(CombatHash(sim, idx));
                if ((step + 1) % c.SnapEvery == 0) { if (snapCount++ > 0) snaps.Append(",\n"); snaps.Append(Snapshot(sim, idx)); }
            }
            if (snapCount++ > 0) snaps.Append(",\n");
            snaps.Append(Snapshot(sim, idx));

            Cov("combat.scenarios");
            if (sim.Finished) Cov("combat.finished." + sim.Winner);
            if (sim.SurgeOwner != Owner.Neutral) Cov("combat.surge.end");

            var sb = new StringBuilder();
            sb.Append("{\"name\":").Append(Q(c.Name)).Append(",\"note\":").Append(Q(c.Note))
              .Append(",\"rngSeed\":").Append(c.RngSeed).Append(",\"coreHp\":").Append(F(c.CoreHp)).Append(",\"activeGroundLanes\":").Append(c.Lanes)
              .Append(",\"director\":{\"baseBudgetPerSecond\":").Append(F(c.DirBase)).Append(",\"rampPer90Seconds\":").Append(F(c.DirRamp))
              .Append(",\"difficultyMult\":").Append(F(c.DirMult)).Append(",\"maxBank\":").Append(F(c.DirMax))
              .Append(",\"waveThreshold\":").Append(F(c.DirWave)).Append(",\"telegraphSeconds\":").Append(F(c.DirTele)).Append(",\"table\":[");
            if (c.DirTable != null)
                for (int i = 0; i < c.DirTable.Length; i++) { if (i > 0) sb.Append(','); sb.Append('[').Append(Q(c.DirTable[i])).Append(',').Append(F(c.DirWeights[i])).Append(']'); }
            sb.Append("]},\"ticksRequested\":").Append(c.Ticks).Append(",\"ticksRun\":").Append(ticksRun)
              .Append(",\"eventColumns\":[\"tick\",\"phase(0=after actions,1=after tick)\",\"type\",\"unit\",\"target\",\"amount\",\"team\",\"flag\"]")
              .Append(",\"actions\":[").Append(acts).Append("],\n\"events\":[").Append(events).Append("],\n\"hashEveryTickFrom1\":[").Append(hashes)
              .Append("],\n\"snapshots\":[\n").Append(snaps).Append("\n]}");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ board scenarios

        static readonly string Letters = "EPNQD";

        static string GridJson(BoardModel m)
        {
            var sb = new StringBuilder("{\"cols\":" + m.Cols + ",\"rows\":" + m.Rows + ",\"gems\":[");
            for (int y = 0; y < m.Rows; y++)
            {
                if (y > 0) sb.Append(',');
                sb.Append('"'); for (int x = 0; x < m.Cols; x++) sb.Append(m.GetTile(x, y).Empty ? '.' : (char)('0' + (int)m.GetTile(x, y).Gem)); sb.Append('"');
            }
            sb.Append("],\"specials\":[");
            for (int y = 0; y < m.Rows; y++)
            {
                if (y > 0) sb.Append(',');
                sb.Append('"'); for (int x = 0; x < m.Cols; x++) sb.Append((char)('0' + (int)m.GetTile(x, y).Special)); sb.Append('"');
            }
            sb.Append("]}");
            return sb.ToString();
        }

        static uint BoardHash(BoardModel m)
        {
            var h = new Hasher();
            for (int x = 0; x < m.Cols; x++)
                for (int y = 0; y < m.Rows; y++) { var t = m.GetTile(x, y); h.W((int)t.Gem); h.W((int)t.Special); h.W(t.Empty ? 1 : 0); }
            return h.H;
        }

        static void TapeRow(StringBuilder sb, BoardEvent e)
        {
            sb.Append('[').Append((int)e.Type).Append(',').Append(e.A.X).Append(',').Append(e.A.Y).Append(',').Append(e.B.X).Append(',').Append(e.B.Y)
              .Append(',').Append((int)e.Gem).Append(',').Append((int)e.Special).Append(',').Append(e.Amount).Append(',').Append(e.Combo).Append(']');
            Cov("board.event." + e.Type);
            if (e.Type == BoardEventType.SpecialCreated || e.Type == BoardEventType.SpecialDetonated) Cov("board." + e.Type + "." + e.Special);
            if (e.Type == BoardEventType.ComboMilestone) Cov("board.milestone." + e.Combo);
        }

        sealed class BOut { public string name, json; public bool hit; }

        // Runs one board scenario. grid==null -> random fill from the seed. swaps==null -> bot with `attempts` tries.
        static BOut RunBoard(string name, string note, int cols, int rows, int seed, int allowed, GemKind[,] grid,
                             List<(int x, int y, SpecialKind k)> specials, List<int[]> swaps, int attempts, int botSeed,
                             Func<int, bool> hitTest = null)
        {
            var rng = new Rng(seed);
            var m = grid != null ? BoardModel.FromGrid(grid, rng) : new BoardModel(cols, rows, rng, allowed);
            if (specials != null) foreach (var sp in specials) m.SetSpecialForTest(sp.x, sp.y, sp.k);

            var sb = new StringBuilder();
            sb.Append("{\"name\":").Append(Q(name)).Append(",\"note\":").Append(Q(note)).Append(",\"cols\":").Append(m.Cols).Append(",\"rows\":").Append(m.Rows)
              .Append(",\"rngSeed\":").Append(seed).Append(",\"allowedGemCount\":").Append(allowed).Append(",\"constructed\":")
              .Append(Q(grid != null ? "FromGrid(initialGrid) then SetSpecialForTest(specials)" : "new BoardModel(cols,rows,new Rng(seed),allowedGemCount)"))
              .Append(",\"tapeColumns\":[\"type\",\"ax\",\"ay\",\"bx\",\"by\",\"gem\",\"special\",\"amount\",\"combo\"]")
              .Append(",\"initial\":").Append(GridJson(m)).Append(",\"initialHash\":").Append(BoardHash(m)).Append(",\"steps\":[");

            var list = swaps;
            var bot = new Rng(botSeed);
            int total = swaps != null ? swaps.Count : attempts;
            int maxPeak = 0; bool reshuffled = false;
            for (int i = 0; i < total; i++)
            {
                int ax, ay, bx, by;
                if (list != null) { ax = list[i][0]; ay = list[i][1]; bx = list[i][2]; by = list[i][3]; }
                else
                {
                    ax = bot.Next(m.Cols); ay = bot.Next(m.Rows);
                    bool right = bot.Next(2) == 0;
                    bx = right ? ax + 1 : ax; by = right ? ay : ay + 1;
                }
                var res = m.TrySwap(new Cell(ax, ay), new Cell(bx, by));
                if (i > 0) sb.Append(",\n");
                sb.Append("{\"a\":[").Append(ax).Append(',').Append(ay).Append("],\"b\":[").Append(bx).Append(',').Append(by)
                  .Append("],\"valid\":").Append(B(res.Valid)).Append(",\"peakCombo\":").Append(m.PeakCombo).Append(",\"tape\":[");
                for (int t = 0; t < res.Tape.Count; t++)
                {
                    if (t > 0) sb.Append(',');
                    TapeRow(sb, res.Tape[t]);
                    if (res.Tape[t].Type == BoardEventType.BoardReshuffled) reshuffled = true;
                }
                sb.Append("],\"hash\":").Append(BoardHash(m)).Append('}');
                if (m.PeakCombo > maxPeak) maxPeak = m.PeakCombo;
                Cov(res.Valid ? "board.swap.valid" : "board.swap.rejected");
            }
            sb.Append("],\"final\":").Append(GridJson(m)).Append(",\"finalHash\":").Append(BoardHash(m)).Append('}');
            Cov("board.scenarios");
            var o = new BOut { name = name, json = sb.ToString() };
            if (hitTest != null) o.hit = hitTest(maxPeak) || (reshuffled && name.StartsWith("search_reshuffle"));
            return o;
        }

        // Background with no 3-runs anywhere: colour = (x + 2y) % 5.
        static GemKind[,] Bg(int cols, int rows)
        {
            var g = new GemKind[cols, rows];
            for (int x = 0; x < cols; x++) for (int y = 0; y < rows; y++) g[x, y] = (GemKind)((x + 2 * y) % 5);
            return g;
        }

        static List<BOut> BoardScenarios()
        {
            var outs = new List<BOut>();
            var specials0 = new List<(int, int, SpecialKind)>();

            // 1) random playouts, several board sizes and gem counts (level shapes: 5x5/3 colours up to 8x8/5 colours)
            outs.Add(RunBoard("playout_5x5_3c_seed3", "bot playout, level-1 shape", 5, 5, 3, 3, null, null, null, 160, 101));
            outs.Add(RunBoard("playout_6x6_4c_seed11", "bot playout, level-5 shape", 6, 6, 11, 4, null, null, null, 160, 202));
            outs.Add(RunBoard("playout_7x7_5c_seed29", "bot playout", 7, 7, 29, 5, null, null, null, 180, 303));
            outs.Add(RunBoard("playout_8x8_5c_seed628091837", "bot playout, level-4 daily seed", 8, 8, 628091837, 5, null, null, null, 220, 404));
            outs.Add(RunBoard("playout_8x8_5c_seed8", "bot playout", 8, 8, 8, 5, null, null, null, 220, 505));

            // 2) handcrafted shapes on the no-run background (swap list is explicit)
            // 4-run -> LaserH: row 3 -> E E E E at x1..4 after swapping (3,2)<->(3,3)
            var g = Bg(8, 8); g[1, 3] = GemKind.Energy; g[2, 3] = GemKind.Energy; g[4, 3] = GemKind.Energy; g[3, 2] = GemKind.Energy;
            outs.Add(RunBoard("shape_four_run_laser_h", "4 in a row creates a horizontal laser at the swap cell", 8, 8, 5, 5, g, null, new List<int[]> { new[] { 3, 2, 3, 3 } }, 0, 0));
            // same but the vertical laser: column 3
            g = Bg(8, 8); g[3, 1] = GemKind.Plasma; g[3, 2] = GemKind.Plasma; g[3, 4] = GemKind.Plasma; g[2, 3] = GemKind.Plasma;
            outs.Add(RunBoard("shape_four_run_laser_v", "4 in a column creates a vertical laser", 8, 8, 6, 5, g, null, new List<int[]> { new[] { 2, 3, 3, 3 } }, 0, 0));
            // 5-run -> Singularity
            g = Bg(8, 8); g[0, 3] = GemKind.Nano; g[1, 3] = GemKind.Nano; g[3, 3] = GemKind.Nano; g[4, 3] = GemKind.Nano; g[2, 2] = GemKind.Nano;
            outs.Add(RunBoard("shape_five_run_singularity", "5 in a row creates a singularity", 8, 8, 7, 5, g, null, new List<int[]> { new[] { 2, 2, 2, 3 } }, 0, 0));
            // L/T -> Cross
            g = Bg(8, 8); g[3, 2] = GemKind.Quantum; g[4, 3] = GemKind.Quantum; g[5, 3] = GemKind.Quantum; g[3, 4] = GemKind.Quantum; g[3, 5] = GemKind.Quantum;
            outs.Add(RunBoard("shape_cross", "L/T crossing runs create a cross special", 8, 8, 8, 5, g, null, new List<int[]> { new[] { 3, 2, 3, 3 } }, 0, 0));
            // special caught in a clear: LaserV preplaced inside the 4-run, then Cross inside a 5-run, Singularity in a 3-run
            g = Bg(8, 8); g[1, 3] = GemKind.Energy; g[2, 3] = GemKind.Energy; g[4, 3] = GemKind.Energy; g[3, 2] = GemKind.Energy;
            outs.Add(RunBoard("chain_laser_in_run", "a pre-existing laser inside a cleared run detonates (chain)", 8, 8, 9, 5, g,
                new List<(int, int, SpecialKind)> { (2, 3, SpecialKind.LaserV) }, new List<int[]> { new[] { 3, 2, 3, 3 } }, 0, 0));
            g = Bg(8, 8); g[0, 3] = GemKind.Nano; g[1, 3] = GemKind.Nano; g[3, 3] = GemKind.Nano; g[4, 3] = GemKind.Nano; g[2, 2] = GemKind.Nano;
            outs.Add(RunBoard("chain_cross_in_run", "a cross inside a 5-run", 8, 8, 10, 5, g,
                new List<(int, int, SpecialKind)> { (1, 3, SpecialKind.Cross) }, new List<int[]> { new[] { 2, 2, 2, 3 } }, 0, 0));
            g = Bg(8, 8); g[1, 3] = GemKind.Plasma; g[2, 3] = GemKind.Plasma; g[4, 3] = GemKind.Plasma; g[3, 2] = GemKind.Plasma;
            outs.Add(RunBoard("chain_singularity_in_run", "a singularity inside a cleared run", 8, 8, 12, 5, g,
                new List<(int, int, SpecialKind)> { (4, 3, SpecialKind.Singularity) }, new List<int[]> { new[] { 3, 2, 3, 3 } }, 0, 0));

            // 3) special + special recipes on random boards (always valid)
            var pairs = new[]
            {
                ("laserh_laserv", SpecialKind.LaserH, SpecialKind.LaserV), ("cross_laserh", SpecialKind.Cross, SpecialKind.LaserH),
                ("cross_cross", SpecialKind.Cross, SpecialKind.Cross), ("sing_laserh", SpecialKind.Singularity, SpecialKind.LaserH),
                ("sing_cross", SpecialKind.Cross, SpecialKind.Singularity), ("sing_sing_emp", SpecialKind.Singularity, SpecialKind.Singularity),
            };
            int ps = 40;
            foreach (var (n, k1, k2) in pairs)
                outs.Add(RunBoard("combo_" + n, "special+special swap", 8, 8, ps++, 5, null,
                    new List<(int, int, SpecialKind)> { (3, 3, k1), (4, 3, k2) }, new List<int[]> { new[] { 3, 3, 4, 3 } }, 0, 0));
            // singularity swapped with a normal gem
            outs.Add(RunBoard("combo_sing_plus_normal", "singularity + normal gem clears all of that gem", 8, 8, 55, 5, null,
                new List<(int, int, SpecialKind)> { (3, 3, SpecialKind.Singularity) }, new List<int[]> { new[] { 3, 3, 4, 3 }, new[] { 0, 0, 1, 0 }, new[] { 20, 20, 21, 20 }, new[] { 0, 0, 2, 0 } }, 0, 0));

            // 4) searched scenarios: find seeds that reach combo milestones / reshuffles (seed is recorded in the fixture)
            BOut found = null;
            for (int seed = 1; seed <= 600 && found == null; seed++)
            {
                var bk = SnapCov(); var o = RunBoard("search_emp_peak5_seed" + seed, "singularity pair wipe that cascades to combo >= 5", 8, 8, seed, 5, null,
                    new List<(int, int, SpecialKind)> { (3, 3, SpecialKind.Singularity), (4, 3, SpecialKind.Singularity) },
                    new List<int[]> { new[] { 3, 3, 4, 3 } }, 0, 0, p => p >= 5);
                if (o.hit) found = o; else RestoreCov(bk);
            }
            if (found != null) outs.Add(found); else Debug.LogWarning("[WebFixtures] no seed reached combo>=5 in the search range");
            found = null;
            for (int seed = 1; seed <= 1500 && found == null; seed++)
            {
                var bk = SnapCov(); var o = RunBoard("search_combo8_seed" + seed, "singularity pair wipe that cascades to combo >= 8", 8, 8, seed, 5, null,
                    new List<(int, int, SpecialKind)> { (3, 3, SpecialKind.Singularity), (4, 3, SpecialKind.Singularity) },
                    new List<int[]> { new[] { 3, 3, 4, 3 } }, 0, 0, p => p >= 8);
                if (o.hit) found = o; else RestoreCov(bk);
            }
            if (found != null) outs.Add(found); else Debug.LogWarning("[WebFixtures] no seed reached combo>=8 in the search range");
            found = null;
            // A 3x3 board with exactly one legal swap: after the clear, the random refill almost never leaves
            // a move, so the deadlock reshuffle loop fires. Seed is searched until it does.
            for (int seed = 1; seed <= 300 && found == null; seed++)
            {
                var rowsDef = new[]
                {
                    new[] { GemKind.Plasma, GemKind.Nano, GemKind.Quantum },
                    new[] { GemKind.Energy, GemKind.Energy, GemKind.Nano },
                    new[] { GemKind.Data, GemKind.Data, GemKind.Energy },
                };
                var gr = new GemKind[3, 3];
                for (int yy = 0; yy < 3; yy++) for (int xx = 0; xx < 3; xx++) gr[xx, yy] = rowsDef[yy][xx];
                var bk = SnapCov();
                var o = RunBoard("search_reshuffle_3x3_seed" + seed, "3x3 board, one legal swap; the refill deadlocks and the board reshuffles", 3, 3, seed, 5, gr, null,
                    new List<int[]> { new[] { 2, 1, 2, 2 }, new[] { 0, 0, 1, 0 }, new[] { 1, 1, 1, 2 } }, 0, 0, p => false);
                if (o.json.Contains("[11,")) { o.hit = true; found = o; } else RestoreCov(bk);
            }
            if (found != null) outs.Add(found); else Debug.LogWarning("[WebFixtures] no seed produced a reshuffle in the search range");

            // 5) bad input
            outs.Add(RunBoard("rejects_bad_swaps", "out of bounds, non-adjacent, same cell, diagonal, no-match", 5, 5, 2, 3, null, null,
                new List<int[]> { new[] { -1, 0, 0, 0 }, new[] { 0, 0, 2, 0 }, new[] { 1, 1, 1, 1 }, new[] { 1, 1, 2, 2 }, new[] { 4, 4, 5, 4 }, new[] { 0, 0, 0, 1 } }, 0, 0));
            return outs;
        }

        // ------------------------------------------------------------------ session scenarios

        static List<BOut> SessionScenarios()
        {
            var outs = new List<BOut>();
            // Shop boosts live in PlayerPrefs, so pin them per scenario (and restore the dev's own values) to keep
            // fixtures reproducible on any machine. level12 deliberately runs with boosts to exercise that path.
            int oldD = PlayerProgress.DamageBoostLevel, oldS = PlayerProgress.SpeedBoostLevel;
            try
            {
                foreach (var (lv, steps, name, dl, sl) in new[] { (1, 3200, "level1", 0, 0), (5, 3200, "level5", 0, 0), (12, 3600, "level12_boosted", 3, 2) })
                {
                    PlayerPrefs.SetInt("pp_boost_dmg", dl); PlayerPrefs.SetInt("pp_boost_spd", sl);
                    outs.Add(new BOut { name = name, json = RunSession(lv, steps, name) });
                }
            }
            finally { PlayerPrefs.SetInt("pp_boost_dmg", oldD); PlayerPrefs.SetInt("pp_boost_spd", oldS); PlayerPrefs.Save(); }
            return outs;
        }

        static readonly FieldInfo FCards = typeof(BattleSession).GetField("_cardCooldown", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FDecay = typeof(BattleSession).GetField("_comboDecayTimer", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FPend  = typeof(BattleSession).GetField("_pendingCombatTime", BindingFlags.NonPublic | BindingFlags.Instance);
        static readonly FieldInfo FRegen = typeof(BattleSession).GetField("_regenRate", BindingFlags.NonPublic | BindingFlags.Instance);

        static string RunSession(int level, int steps, string name)
        {
            var db = Resources.Load<GameDatabase>("GameDatabase");
            int seed = RankLadder.DailySeed(level, new DateTime(2026, 10, 27, 12, 0, 0, DateTimeKind.Utc));
            float dm = LevelConfig.DirectorMult(level);
            int gems = LevelConfig.GemColorCount(level);
            var s = new BattleSession(db, dm, seed, gems, level, true);
            Func<UnitSpec, int> idx = u => u == null ? -1 : Array.IndexOf(CatalogOrder, u.Id);

            var bot = new Lcg((uint)(seed ^ 0x5bd1e995));
            var log = new StringBuilder(); int logCount = 0;
            var events = new StringBuilder(); int evCount = 0;
            var hashes = new StringBuilder(); var snaps = new StringBuilder(); int snapCount = 0;
            int cols = s.Board.Cols, rows = s.Board.Rows;
            int usesDb = db != null && db.units != null && db.units.Count > 0 ? 1 : 0;
            int stepsRun = 0;

            var head = new StringBuilder();
            head.Append("{\"name\":").Append(Q(name)).Append(",\"level\":").Append(level).Append(",\"seed\":").Append(seed)
                .Append(",\"directorMult\":").Append(F(dm)).Append(",\"gemColorCount\":").Append(gems).Append(",\"enemyDirector\":true")
                .Append(",\"usesGameDatabasePath\":").Append(usesDb)
                .Append(",\"playerDamageMult\":").Append(F(PlayerProgress.ActiveDamageMult)).Append(",\"playerSpeedMult\":").Append(F(PlayerProgress.ActiveSpeedMult))
                .Append(",\"regenRate\":").Append(F((float)FRegen.GetValue(s)))
                .Append(",\"stepSeconds\":").Append(F(0.05f))
                .Append(",\"boardRngSeed\":").Append(seed).Append(",\"combatRngSeed\":").Append(seed + 1)
                .Append(",\"cols\":").Append(cols).Append(",\"rows\":").Append(rows)
                .Append(",\"initialBoard\":").Append(GridJson(s.Board)).Append(",\"deployOptions\":[");
            for (int i = 0; i < s.DeployOptions.Count; i++)
            {
                var o = s.DeployOptions[i];
                if (i > 0) head.Append(',');
                head.Append("{\"name\":").Append(Q(o.name)).Append(",\"spec\":").Append(SpecJson(o.spec)).Append(",\"cost\":[");
                for (int c = 0; c < o.cost.Length; c++) { if (c > 0) head.Append(','); head.Append(o.cost[c]); }
                head.Append("],\"cooldown\":").Append(F(o.cooldown)).Append('}');
            }
            head.Append("],\"enemySpawnTable\":[");
            var dir = (DirectorConfig)FDir.GetValue(s.Combat);
            for (int i = 0; i < dir.SpawnTable.Count; i++)
            {
                if (i > 0) head.Append(',');
                head.Append('[').Append(Q(dir.SpawnTable[i].spec.Id)).Append(',').Append(F(dir.SpawnTable[i].weight)).Append(']');
            }
            head.Append("],\"startResources\":[");
            for (int i = 0; i < s.Resources.Length; i++) { if (i > 0) head.Append(','); head.Append(s.Resources[i]); }
            head.Append(']');

            for (int step = 0; step < steps && !s.Combat.Finished; step++)
            {
                // scripted player behaviour, fully recorded below so a port needs no bot of its own
                if (step % 37 == 5)
                {
                    int ax = bot.Next(cols), ay = bot.Next(rows);
                    bool right = bot.Next(2) == 0;
                    int bx = right ? ax + 1 : ax, by = right ? ay : ay + 1;
                    var res = s.TrySwap(ax, ay, bx, by);
                    if (logCount++ > 0) log.Append(",\n");
                    log.Append("{\"step\":").Append(step).Append(",\"kind\":\"swap\",\"a\":[").Append(ax).Append(',').Append(ay).Append("],\"b\":[").Append(bx).Append(',').Append(by)
                       .Append("],\"valid\":").Append(B(res.Valid)).Append(",\"tape\":[");
                    for (int t = 0; t < res.Tape.Count; t++) { if (t > 0) log.Append(','); TapeRow(log, res.Tape[t]); }
                    log.Append("],\"resources\":[");
                    for (int r = 0; r < s.Resources.Length; r++) { if (r > 0) log.Append(','); log.Append(s.Resources[r]); }
                    log.Append("],\"liveCombo\":").Append(s.LiveCombo).Append(",\"peakCombo\":").Append(s.PeakCombo)
                       .Append(",\"fx\":").Append(s.PendingFx.Count).Append(",\"net\":").Append(s.PendingNetPayloads.Count).Append('}');
                    s.PendingFx.Clear(); s.PendingNetPayloads.Clear();
                }
                if (step % 53 == 11 && s.DeployOptions.Count > 0)
                {
                    int card = bot.Next(s.DeployOptions.Count), lane = bot.Next(CombatSim.GroundLanes);
                    bool ok = s.Deploy(card, lane);
                    if (logCount++ > 0) log.Append(",\n");
                    log.Append("{\"step\":").Append(step).Append(",\"kind\":\"deploy\",\"card\":").Append(card).Append(",\"lane\":").Append(lane).Append(",\"ok\":").Append(B(ok)).Append('}');
                }
                DrainEvents(s.Combat, s.Combat.TickCount, 0, events, ref evCount);
                s.Tick(0.05f);
                DrainEvents(s.Combat, s.Combat.TickCount, 1, events, ref evCount);

                var h = new Hasher();
                h.W((int)CombatHash(s.Combat, idx));
                foreach (int r in s.Resources) h.W(r);
                h.W(s.LiveCombo); h.W(s.PeakCombo);
                foreach (float cd in (float[])FCards.GetValue(s)) h.Fl(cd);
                h.Fl((float)FDecay.GetValue(s)); h.Fl((float)FPend.GetValue(s));
                h.W((int)BoardHash(s.Board));
                if (step > 0) hashes.Append(',');
                hashes.Append(h.H);
                stepsRun = step + 1;
                if ((step + 1) % 400 == 0)
                {
                    if (snapCount++ > 0) snaps.Append(",\n");
                    snaps.Append("{\"step\":").Append(step + 1).Append(",\"combat\":").Append(Snapshot(s.Combat, idx)).Append(",\"board\":").Append(GridJson(s.Board)).Append(",\"resources\":[");
                    for (int r = 0; r < s.Resources.Length; r++) { if (r > 0) snaps.Append(','); snaps.Append(s.Resources[r]); }
                    snaps.Append("],\"liveCombo\":").Append(s.LiveCombo).Append(",\"peakCombo\":").Append(s.PeakCombo).Append(",\"cards\":[");
                    var cds = (float[])FCards.GetValue(s);
                    for (int r = 0; r < cds.Length; r++) { if (r > 0) snaps.Append(','); snaps.Append(F(cds[r])); }
                    snaps.Append("],\"decay\":").Append(F((float)FDecay.GetValue(s))).Append(",\"pend\":").Append(F((float)FPend.GetValue(s))).Append('}');
                }
            }
            Cov("session.scenarios");
            head.Append(",\"stepsRun\":").Append(stepsRun)
                .Append(",\"inputs\":[\n").Append(log).Append("\n],\"eventColumns\":[\"tick\",\"phase\",\"type\",\"unit\",\"target\",\"amount\",\"team\",\"flag\"],\"combatEvents\":[")
                .Append(events).Append("],\n\"hashEveryStepFrom1\":[").Append(hashes).Append("],\n\"snapshots\":[\n").Append(snaps).Append("\n]}");
            return head.ToString();
        }
    }
}
