using System;
using System.Collections.Generic;

namespace NW.App
{
    /// <summary>
    /// One replayable action inside a <see cref="GhostRecord"/>.
    ///
    /// A ghost is the EFFECT-level trace of a match, not a raw input log: only the
    /// things a pilot's board pushed into the war are stored — enemy-side deploys and
    /// board→war crossover strikes — each stamped with the <see cref="NW.Combat.Domain.CombatSim"/>
    /// tick it happened on. Replaying these against a fresh sim (with the Enemy Director
    /// off) reproduces the pilot as an opponent. Gem swaps are never recorded; nothing
    /// replays a whole board.
    /// </summary>
    [Serializable]
    public struct GhostAction
    {
        public int    tick;    // CombatSim.TickCount when the action fired
        public int    type;    // GhostActionType
        public string unit;    // Deploy: unit spec id ("drone"…). Crossover: ""
        public int    lane;    // Deploy / Laser: ground lane 0..2. Otherwise -1
        public float  amount;  // Crossover: core damage dealt to the receiver. Deploy: 0
    }

    public enum GhostActionType
    {
        Deploy    = 0,
        Laser     = 1,   // maps to CrossoverKind.LaserLane
        Orbital   = 2,   // maps to CrossoverKind.Orbital
        Emp       = 3,   // maps to CrossoverKind.Emp
        Shockwave = 4,   // maps to CrossoverKind.Shockwave
    }

    /// <summary>
    /// A serialisable recording of one ranked/solo match. Small enough (a few KB) to
    /// bundle dozens with the game and to write one per match played.
    /// </summary>
    [Serializable]
    public sealed class GhostRecord
    {
        public int    version   = 1;
        public string id        = "";     // filename stem, assigned by GhostStore on save
        public string pilot     = "PILOT"; // display name (the save-slot name)
        public string pilotId   = "";     // PlayerProgress.PilotId — stable, the leaderboard key
        public int    mmr       = 1200;   // the recording pilot's MMR at record time
        public int    level     = 1;
        public int    theme     = 0;
        public int    seed      = 0;      // board + combat seed this match used
        public int    gemCount  = 5;
        public float  difficulty = 1f;
        public int    result    = 0;      // 1 = recording pilot won, 0 = lost
        public int    durationTicks = 0;
        public long   recordedUtc = 0;    // DateTime.UtcNow.ToBinary()
        public bool   bundled   = false;  // shipped with the build (read-only pool)
        public List<GhostAction> actions = new List<GhostAction>();

        public DateTime RecordedAt =>
            recordedUtc != 0 ? DateTime.FromBinary(recordedUtc) : DateTime.MinValue;

        public bool PilotWon => result == 1;
    }
}
