using System.Collections.Generic;
using NW.Board.Domain;
using NW.Combat.Domain;
using NW.Domain;
using NW.Data;

namespace NW.App
{
    /// <summary>One battlefield effect fired by the Compiler (doc 12 §4); views drain these.</summary>
    public enum CrossoverKind : byte { LaserLane, Orbital, Emp, Shockwave }

    /// <summary>Crossover event that must be transmitted to the opponent in competitive mode.</summary>
    public readonly struct NetPayload
    {
        public readonly CrossoverKind Kind;
        public readonly int           Lane;    // -1 when not lane-specific
        public readonly float         Damage;  // core damage the opponent should receive
        public NetPayload(CrossoverKind kind, int lane, float damage)
        { Kind = kind; Lane = lane; Damage = damage; }
    }

    public readonly struct CrossoverFx
    {
        public readonly CrossoverKind Kind;
        public readonly int Lane;       // -1 when not lane-specific
        public CrossoverFx(CrossoverKind kind, int lane = -1) { Kind = kind; Lane = lane; }
    }

    /// <summary>
    /// Owns BoardModel + CombatSim for one battle. Routes resource payouts and
    /// crossover strikes (lasers/orbital/EMP) from the board tape into the sim;
    /// exposes lane-targeted deploys with per-card compile cooldowns.
    /// </summary>
    public sealed class BattleSession
    {
        public const float LaserDamage = 15f;
        public const float OrbitalDamage = 40f;

        public BoardModel Board { get; }
        public CombatSim Combat { get; }
        public int[] Resources { get; } = new int[BoardModel.GemKindCount];
        public int PeakCombo { get; private set; }
        public int LiveCombo { get; private set; }

        const float ComboDecaySec = 3.5f;
        float _comboDecayTimer;

        public readonly List<(string name, UnitSpec spec, int[] cost, float cooldown)> DeployOptions = new();
        public readonly List<CrossoverFx>  PendingFx          = new();
        public readonly List<NetPayload>   PendingNetPayloads  = new();

        /// <summary>Fires (card, lane) after a player deploy succeeds — the ghost recorder
        /// stamps it against <see cref="CombatSim.TickCount"/>.</summary>
        public System.Action<int, int> OnPlayerDeploy;
        /// <summary>Fires once per internal <see cref="CombatSim.Tick"/> so a ghost driver can
        /// step in lockstep with the sim rather than once per rendered frame.</summary>
        public System.Action OnCombatStepped;

        private readonly float[] _cardCooldown = new float[10]; // supports up to 10 deploy cards
        private float _pendingCombatTime;
        private readonly float _regenRate; // player core HP/sec; 0 on low levels

        public BattleSession(GameDatabase db, float difficultyMult = 1f, int seed = 0, int allowedGemCount = 5, int level = 1,
                             bool enemyDirector = true)
        {
            int boardCols = LevelConfig.BoardCols(level);
            int boardRows = LevelConfig.BoardRows(level);
            Board = new BoardModel(boardCols, boardRows, new Rng(seed), allowedGemCount);

            var director = new DirectorConfig
            {
                BaseBudgetPerSecond = 1.0f,
                WaveThreshold = 14f,
                DifficultyMult = difficultyMult,
            };

            if (db != null && db.units != null && db.units.Count > 0)
            {
                foreach (var def in db.units)
                {
                    if (def == null) continue;
                    if (enemyDirector) director.SpawnTable.Add((def.ToSpec(), 1f));
                    var c = new int[BoardModel.GemKindCount];
                    c[(int)GemKind.Energy]  = def.cost.energy;
                    c[(int)GemKind.Plasma]  = def.cost.plasma;
                    c[(int)GemKind.Nano]    = def.cost.nano;
                    c[(int)GemKind.Quantum] = def.cost.quantum;
                    c[(int)GemKind.Data]    = def.cost.data;
                    bool allZero = true;
                    for (int i = 0; i < c.Length; i++) if (c[i] > 0) { allZero = false; break; }
                    if (allZero) c[(int)GemKind.Energy] = 5;
                    DeployOptions.Add((def.displayName, def.ToSpec(), c, def.compileCooldown));
                }
            }
            else
            {
                AddFallbackUnits(director, level, enemyDirector);
            }

            float coreHp = LevelConfig.CoreHp(level);
            Combat = new CombatSim(coreHp, coreHp, director, new Rng(seed + 1));
            Resources[(int)GemKind.Energy] = 15;
            Resources[(int)GemKind.Plasma] = 8;
            Resources[(int)GemKind.Nano]   = 8;
            _regenRate = level >= 15 ? 2.0f : level >= 10 ? 1.0f : 0f;
        }

        private void AddFallbackUnits(DirectorConfig dir, int level, bool enemyDirector = true)
        {
            // ── all unit specs ────────────────────────────────────────────────────
            // These were nine local UnitSpec objects declared right here, unreachable from
            // anywhere else -- so TroopStats and the level-select demo each kept their own
            // copy of the numbers, and all three had drifted apart. They live in UnitCatalog
            // now, and everything reads the same values.
            var drone       = UnitCatalog.Get("drone");
            var trooper     = UnitCatalog.Get("trooper");
            var sniper      = UnitCatalog.Get("sniper");
            var mech        = UnitCatalog.Get("mech");
            var shieldBot   = UnitCatalog.Get("shield-bot");
            var interceptor = UnitCatalog.Get("interceptor");
            var hacker      = UnitCatalog.Get("hacker");
            var titan       = UnitCatalog.Get("titan");
            var turret      = UnitCatalog.Get("turret");            // ── deploy option catalogue (id → display, spec, cost, cooldown) ──────
            var catalogue = new Dictionary<string, (string display, UnitSpec spec, int[] cost, float cd)>
            {
                ["drone"]       = ("DRONE",    drone,      new[] { 3, 0, 0, 0, 0 }, 2.0f),
                ["trooper"]     = ("TROOPER",  trooper,    new[] { 0, 5, 0, 0, 0 }, 3.0f),
                ["sniper"]      = ("SNIPER",   sniper,     new[] { 0, 8, 0, 0, 0 }, 4.0f),
                ["mech"]        = ("MECH",     mech,       new[] { 0, 8, 0, 0, 0 }, 5.0f),
                ["shield-bot"]  = ("SHIELD",   shieldBot,  new[] { 4, 0, 6, 0, 0 }, 4.0f),
                ["interceptor"] = ("INTRCPTR", interceptor,new[] { 0, 0, 5, 0, 0 }, 3.0f),
                ["hacker"]      = ("HACKER",   hacker,     new[] { 0, 0, 0, 8, 0 }, 5.0f),
                ["titan"]       = ("TITAN",    titan,      new[] { 0, 0, 0, 0, 10}, 15.0f),
                ["turret"]      = ("TURRET",   turret,     new[] { 2, 0, 6, 0, 0 }, 6.0f),
            };

            // ── enemy spawn weights (higher = more common) ────────────────────────
            var spawnWeights = new Dictionary<string, float>
            {
                ["drone"]       = 3.0f,
                ["trooper"]     = 2.0f,
                ["sniper"]      = 1.2f,
                ["mech"]        = 1.0f,
                ["interceptor"] = 1.0f,
                ["hacker"]      = 0.8f,
                ["titan"]       = 0.4f,
            };
            // Turret and shield-bot excluded from enemy roster
            var enemyExclude = new HashSet<string> { "turret", "shield-bot" };

            // ── build filtered lists based on current level ───────────────────────
            var available = LevelConfig.AvailableTroops(level);
            foreach (var id in available)
            {
                if (!catalogue.TryGetValue(id, out var d)) continue;
                DeployOptions.Add((d.display, d.spec, d.cost, d.cd));
                if (enemyDirector && !enemyExclude.Contains(id) && spawnWeights.TryGetValue(id, out float w))
                    dir.SpawnTable.Add((d.spec, w));
            }
        }

        // ------------------------------------------------------------ board ---

        public SwapResult TrySwap(int ax, int ay, int bx, int by)
        {
            var result = Board.TrySwap(new Cell(ax, ay), new Cell(bx, by));
            if (!result.Valid) return result;

            foreach (var evt in result.Tape)
            {
                switch (evt.Type)
                {
                    case BoardEventType.ResourcePayout:
                        Resources[(int)evt.Gem] += evt.Amount;
                        if (Resources[(int)evt.Gem] > 99) Resources[(int)evt.Gem] = 99;
                        break;

                    case BoardEventType.SpecialDetonated:
                        FireSpecial(evt);
                        break;

                    case BoardEventType.ComboMilestone:
                        if (evt.Combo >= 8)
                        {
                            Combat.StunEnemies(2f);
                            PendingFx.Add(new CrossoverFx(CrossoverKind.Emp));
                            PendingNetPayloads.Add(new NetPayload(CrossoverKind.Emp, -1, 20f));
                        }
                        else if (evt.Combo >= 5)
                        {
                            Combat.DamageStrongestEnemy(OrbitalDamage);
                            PendingFx.Add(new CrossoverFx(CrossoverKind.Orbital));
                            PendingNetPayloads.Add(new NetPayload(CrossoverKind.Orbital, -1, OrbitalDamage));
                        }
                        break;

                    case BoardEventType.EmpTriggered:
                        Combat.StunEnemies(3f);
                        PendingFx.Add(new CrossoverFx(CrossoverKind.Emp));
                        PendingNetPayloads.Add(new NetPayload(CrossoverKind.Emp, -1, 25f));
                        break;

                    case BoardEventType.ResolveEnded:
                        if (evt.Combo > PeakCombo) PeakCombo = evt.Combo;
                        if (evt.Combo > 0)
                        {
                            LiveCombo++;
                            _comboDecayTimer = ComboDecaySec;
                        }
                        break;
                }
            }
            return result;
        }

        private void FireSpecial(in BoardEvent evt)
        {
            switch (evt.Special)
            {
                case SpecialKind.LaserH:
                    StrikeLane(evt.A.Y % CombatSim.GroundLanes);
                    break;
                case SpecialKind.LaserV:
                    StrikeLane(Combat.BusiestEnemyLane());
                    break;
                case SpecialKind.Cross:
                    StrikeLane(evt.A.Y % CombatSim.GroundLanes);
                    StrikeLane(Combat.BusiestEnemyLane());
                    break;
                case SpecialKind.Singularity:
                    Combat.SlowEnemies(1.5f);
                    PendingFx.Add(new CrossoverFx(CrossoverKind.Shockwave));
                    PendingNetPayloads.Add(new NetPayload(CrossoverKind.Shockwave, -1, 10f));
                    break;
            }
        }

        private void StrikeLane(int lane)
        {
            Combat.DamageLaneEnemies(lane, LaserDamage);
            PendingFx.Add(new CrossoverFx(CrossoverKind.LaserLane, lane));
            PendingNetPayloads.Add(new NetPayload(CrossoverKind.LaserLane, lane, LaserDamage));
        }

        // ----------------------------------------------------------- deploy ---

        public bool CanAfford(int[] cost)
        {
            for (int i = 0; i < BoardModel.GemKindCount; i++)
                if (Resources[i] < cost[i]) return false;
            return true;
        }

        public float CardCooldown(int card) => card < _cardCooldown.Length ? _cardCooldown[card] : 0f;

        public bool CanDeploy(int card) =>
            card >= 0 && card < DeployOptions.Count && card < _cardCooldown.Length
            && _cardCooldown[card] <= 0f
            && CanAfford(DeployOptions[card].cost);

        /// <summary>Deploy card into a ground lane (air units auto-route to the air lane).</summary>
        public bool Deploy(int card, int lane)
        {
            if (!CanDeploy(card)) return false;
            var opt = DeployOptions[card];
            // Clone spec and apply permanent shop boosts so originals stay unchanged
            var o = opt.spec;
            var boostedSpec = new UnitSpec
            {
                Id = o.Id, Class = o.Class,
                MaxHp = o.MaxHp,
                Damage = o.Damage * PlayerProgress.ActiveDamageMult,
                AttackCooldown = o.AttackCooldown,
                Speed = o.Speed * PlayerProgress.ActiveSpeedMult,
                Range = o.Range, AoeRadius = o.AoeRadius,
                IsAir = o.IsAir, TargetsAir = o.TargetsAir, TargetsGround = o.TargetsGround,
                CanCapture = o.CanCapture, DirectorCost = o.DirectorCost,
                SpawnAtPylon = o.SpawnAtPylon,
            };
            if (Combat.Spawn(boostedSpec, Team.Player, lane) < 0) return false;
            for (int i = 0; i < BoardModel.GemKindCount; i++)
                Resources[i] -= opt.cost[i];
            _cardCooldown[card] = opt.cooldown;
            OnPlayerDeploy?.Invoke(card, lane);
            return true;
        }

        // ------------------------------------------------------------- tick ---

        public void Tick(float deltaTime)
        {
            for (int i = 0; i < _cardCooldown.Length; i++)
                if (_cardCooldown[i] > 0f) _cardCooldown[i] -= deltaTime;

            if (_comboDecayTimer > 0f)
            {
                _comboDecayTimer -= deltaTime;
                if (_comboDecayTimer <= 0f) LiveCombo = 0;
            }

            if (Combat.Finished) return;
            if (_regenRate > 0f) Combat.HealPlayerCore(_regenRate * deltaTime);
            _pendingCombatTime += deltaTime;
            while (_pendingCombatTime >= CombatSim.TickDelta && !Combat.Finished)
            {
                _pendingCombatTime -= CombatSim.TickDelta;
                Combat.Tick();
                OnCombatStepped?.Invoke();
            }
        }
    }
}
