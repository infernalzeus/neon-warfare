using System;
using System.Collections.Generic;

namespace NW.App
{
    /// <summary>
    /// Where daily-board standings come from. The game only ever talks to <see cref="Leaderboard"/>,
    /// which holds one of these. <see cref="LocalLeaderboardBackend"/> (the default) reads the
    /// on-device ghost pool; an online backend (Firebase) reads a shared store. Callbacks fire on
    /// the Unity main thread; a backend that is offline or still starting returns local data or
    /// an empty list rather than throwing.
    /// </summary>
    public interface ILeaderboardBackend
    {
        /// <summary>Short label for the UI — "Local", "Firebase".</summary>
        string Name { get; }

        /// <summary>True if this backend talks to a shared store other players also write to.</summary>
        bool IsRemote { get; }

        /// <summary>"ready" when queries will hit the shared store; anything else means
        /// results fall back to the local pool.</summary>
        string Status { get; }

        /// <summary>Push this run to the board if it ranks ahead of the pilot's current entry
        /// (<see cref="RankLadder.Beats"/>). Fire-and-forget — never blocks the result screen.
        /// The local backend is a no-op: the on-device pool already holds every match.</summary>
        void SubmitBest(GhostRecord run);

        /// <summary>Best-first standings for a level's daily board. <paramref name="dailySeed"/> is
        /// <see cref="RankLadder.DailySeed"/> for the day being shown.</summary>
        void FetchTop(int level, int dailySeed, int limit, Action<IReadOnlyList<LeaderboardEntry>> done);

        /// <summary>The pilot's neighbours on the board — up to <paramref name="span"/> either side of
        /// their own row, plus their row. Ranks are relative when the absolute position is unknown.</summary>
        void FetchAroundMe(int level, int dailySeed, int span, Action<IReadOnlyList<LeaderboardEntry>> done);

        /// <summary>Pull the full <see cref="GhostRecord"/> for one row — called only when the player
        /// hits CHALLENGE, never while listing. The panel supplies the board it is showing.
        /// <paramref name="done"/> gets null on failure.</summary>
        void FetchGhost(int level, int dailySeed, LeaderboardEntry entry, Action<GhostRecord> done);
    }
}
