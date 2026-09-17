namespace NW.App
{
    /// <summary>
    /// Accumulates a <see cref="GhostRecord"/> from a live match. BattleScene feeds it
    /// two things: every successful player deploy (as an enemy-side action) and every
    /// board→war crossover payload, each with the current <see cref="NW.Combat.Domain.CombatSim"/>
    /// tick. <see cref="Finish"/> seals it; GhostStore writes it to disk.
    /// </summary>
    public sealed class GhostRecorder
    {
        readonly GhostRecord _rec = new GhostRecord();

        public GhostRecorder(int level, int theme, int seed, int gemCount, float difficulty)
        {
            _rec.pilot      = PlayerProgress.CurrentSlotName();
            _rec.pilotId    = PlayerProgress.PilotId;
            _rec.mmr        = PlayerProgress.MMR;
            _rec.level      = level;
            _rec.theme      = theme;
            _rec.seed       = seed;
            _rec.gemCount   = gemCount;
            _rec.difficulty = difficulty;
        }

        public void RecordDeploy(int tick, string unitId, int lane)
        {
            _rec.actions.Add(new GhostAction
            {
                tick   = tick,
                type   = (int)GhostActionType.Deploy,
                unit   = string.IsNullOrEmpty(unitId) ? "trooper" : unitId,
                lane   = lane,
                amount = 0f,
            });
        }

        public void RecordCrossover(int tick, NetPayload p)
        {
            GhostActionType t = p.Kind switch
            {
                CrossoverKind.LaserLane => GhostActionType.Laser,
                CrossoverKind.Orbital   => GhostActionType.Orbital,
                CrossoverKind.Emp       => GhostActionType.Emp,
                CrossoverKind.Shockwave => GhostActionType.Shockwave,
                _                       => GhostActionType.Orbital,
            };
            _rec.actions.Add(new GhostAction
            {
                tick   = tick,
                type   = (int)t,
                unit   = "",
                lane   = p.Lane,
                amount = p.Damage,
            });
        }

        /// <summary>Seal the recording. A match with no actions (instant loss, rage-quit)
        /// returns null — a ghost that does nothing is not worth storing.
        /// <paramref name="speed"/> is the BattleSpeed the match ran at; the board ranks on
        /// <c>scoreTicks = durationTicks / speed</c> so a faster clear places higher.</summary>
        public GhostRecord Finish(bool pilotWon, int durationTicks, float speed = 1f)
        {
            if (_rec.actions.Count == 0) return null;
            _rec.result        = pilotWon ? 1 : 0;
            _rec.durationTicks = durationTicks;
            _rec.speed         = speed <= 0.05f ? 1f : speed;
            _rec.scoreTicks    = System.Math.Max(1,
                                     (int)System.Math.Round(durationTicks / (double)_rec.speed));
            _rec.recordedUtc   = System.DateTime.UtcNow.ToBinary();
            return _rec;
        }
    }
}
