using System;
using System.Collections.Generic;

namespace NW.App
{
    /// <summary>
    /// The always-present default: standings come from the on-device ghost pool
    /// (<see cref="GhostStore"/>) ranked by <see cref="RankLadder.Standings(IEnumerable{GhostRecord},int,int,string)"/>.
    /// Synchronous under the hood; the callbacks still fire so call sites are identical to the
    /// online backend. Nothing to submit — every finished match is already saved locally.
    /// </summary>
    public sealed class LocalLeaderboardBackend : ILeaderboardBackend
    {
        public string Name     => "Local";
        public bool   IsRemote => false;
        public string Status   => "ready";

        public void SubmitBest(GhostRecord run) { /* the local pool is the store */ }

        public void FetchTop(int level, int dailySeed, int limit, Action<IReadOnlyList<LeaderboardEntry>> done)
        {
            var all = RankLadder.Standings(GhostStore.All(), level, dailySeed, PlayerProgress.CurrentSlotName());
            if (limit > 0 && all.Count > limit) all.RemoveRange(limit, all.Count - limit);
            done?.Invoke(all);
        }

        public void FetchAroundMe(int level, int dailySeed, int span, Action<IReadOnlyList<LeaderboardEntry>> done)
        {
            var all = RankLadder.Standings(GhostStore.All(), level, dailySeed, PlayerProgress.CurrentSlotName());
            int me = all.FindIndex(e => e.isYou);
            if (me < 0) { done?.Invoke(all); return; }
            int from = Math.Max(0, me - span);
            int to   = Math.Min(all.Count, me + span + 1);
            done?.Invoke(all.GetRange(from, to - from));
        }

        public void FetchGhost(int level, int dailySeed, LeaderboardEntry entry, Action<GhostRecord> done)
            => done?.Invoke(GhostStore.ById(entry.ghostId));
    }
}
