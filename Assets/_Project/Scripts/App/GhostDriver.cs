using NW.Combat.Domain;

namespace NW.App
{
    /// <summary>
    /// Replays a <see cref="GhostRecord"/> into a live <see cref="CombatSim"/> as the enemy.
    ///
    /// The session this drives MUST be built with <c>enemyDirector: false</c> — otherwise
    /// the pilot faces the ghost AND the AI at once. Deploys become <see cref="Team.Enemy"/>
    /// spawns; crossover actions collapse to raw core damage plus a view callback, which is
    /// exactly how a networked opponent's strikes already resolve on the receiving side.
    /// </summary>
    public sealed class GhostDriver
    {
        readonly GhostRecord _rec;
        readonly CombatSim   _sim;
        int _next;

        /// <summary>(kind, lane, damage) for each replayed crossover — BattleScene flashes it.</summary>
        public System.Action<CrossoverKind, int, float> OnGhostCrossover;

        public GhostDriver(GhostRecord rec, CombatSim sim)
        {
            _rec = rec;
            _sim = sim;
        }

        public int  DurationTicks => _rec != null ? _rec.durationTicks : 0;
        public bool Exhausted     => _rec == null || _next >= _rec.actions.Count;

        /// <summary>Call once per <see cref="CombatSim.Tick"/>, passing the sim's TickCount.
        /// Applies every recorded action whose tick has been reached.</summary>
        public void Step(int tick)
        {
            if (_rec == null) return;
            while (_next < _rec.actions.Count && _rec.actions[_next].tick <= tick)
            {
                Apply(_rec.actions[_next]);
                _next++;
            }
        }

        void Apply(in GhostAction a)
        {
            if ((GhostActionType)a.type == GhostActionType.Deploy)
            {
                var spec = UnitCatalog.Get(a.unit) ?? UnitCatalog.Get("trooper");
                if (spec != null) _sim.Spawn(spec, Team.Enemy, a.lane);
                return;
            }

            // Laser / Orbital / Emp / Shockwave → GhostActionType n maps to CrossoverKind n-1.
            if (a.amount > 0f) _sim.DamagePlayerCore(a.amount);
            OnGhostCrossover?.Invoke((CrossoverKind)(a.type - 1), a.lane, a.amount);
        }
    }
}
