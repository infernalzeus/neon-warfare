using System;
using System.Collections.Generic;

namespace NW.App
{
    /// <summary>
    /// The single entry point the game uses for daily-board standings. Defaults to
    /// <see cref="LocalLeaderboardBackend"/>; <see cref="FirebaseLeaderboardBackend"/> swaps
    /// itself in at startup when the <c>NW_FIREBASE</c> define is set and sign-in succeeds.
    ///
    /// Pattern for the RANKS panel: paint <see cref="LocalTop"/> instantly, then call
    /// <see cref="RefreshTop"/> and repaint when (if) the shared store answers.
    /// </summary>
    public static class Leaderboard
    {
        static ILeaderboardBackend _backend = new LocalLeaderboardBackend();

        public static ILeaderboardBackend Backend => _backend;
        public static bool   IsOnline => _backend.IsRemote && _backend.Status == "ready";
        public static string StatusLabel => _backend.IsRemote ? $"{_backend.Name} · {_backend.Status}" : "Local";

        /// <summary>Install a backend (Firebase calls this after anon sign-in). Passing null
        /// restores the local backend.</summary>
        public static void Configure(ILeaderboardBackend backend)
            => _backend = backend ?? new LocalLeaderboardBackend();

        /// <summary>Submit a just-finished run. No-op locally; the online backend writes only if
        /// it beats the pilot's current row.</summary>
        public static void SubmitBest(GhostRecord run)
        {
            if (run == null) return;
            try { _backend.SubmitBest(run); } catch { /* leaderboard writes never break a match */ }
        }

        /// <summary>Instant, synchronous standings from the on-device pool — for the first paint.</summary>
        public static IReadOnlyList<LeaderboardEntry> LocalTop(int level, int dailySeed, int limit)
        {
            var all = RankLadder.Standings(GhostStore.All(), level, dailySeed, PlayerProgress.CurrentSlotName());
            if (limit > 0 && all.Count > limit) all.RemoveRange(limit, all.Count - limit);
            return all;
        }

        public static void RefreshTop(int level, int dailySeed, int limit,
                                      Action<IReadOnlyList<LeaderboardEntry>> done)
        {
            try { _backend.FetchTop(level, dailySeed, limit, done); }
            catch { done?.Invoke(LocalTop(level, dailySeed, limit)); }
        }

        public static void FetchGhost(int level, int dailySeed, LeaderboardEntry entry, Action<GhostRecord> done)
        {
            try { _backend.FetchGhost(level, dailySeed, entry, done); }
            catch { done?.Invoke(GhostStore.ById(entry.ghostId)); }
        }
    }
}
