using System;
using System.Collections.Generic;
using NW.Combat.Domain;
using UnityEngine;

namespace NW.App
{
    /// <summary>One row of the daily-board leaderboard for a level. Produced by
    /// <see cref="RankLadder.Standings(IEnumerable{GhostRecord},int,int,string)"/> (local pool)
    /// or an <c>ILeaderboardBackend</c> (remote); the RANKS panel renders it either way.</summary>
    [Serializable]
    public struct LeaderboardEntry
    {
        public int    rank;
        public string pilot;
        public string pilotId;
        public int    mmr;
        public bool   won;
        public int    durationTicks;
        public long   recordedUtc;
        public bool   isYou;
        public string ghostId;     // local: GhostStore id · remote: entry doc id
        public bool   isRemote;

        public float    DurationSeconds => durationTicks * CombatSim.TickDelta;
        public DateTime SetAt           => recordedUtc != 0 ? DateTime.FromBinary(recordedUtc) : DateTime.MinValue;
    }

    /// <summary>One completed ranked match, kept for the RANKS → HISTORY tab.</summary>
    [Serializable]
    public struct MatchOutcome
    {
        public long   utc;
        public bool   won;
        public int    mmrBefore;
        public int    mmrAfter;
        public int    oppMmr;
        public string oppPilot;
        public int    level;

        public int    Delta   => mmrAfter - mmrBefore;
        public DateTime When   => utc != 0 ? DateTime.FromBinary(utc) : DateTime.MinValue;
    }

    /// <summary>
    /// MMR tiers, the Elo update, the season/weekly-seed identifiers, and the local
    /// match-history log. The number itself lives in <see cref="PlayerProgress.MMR"/>;
    /// this is everything wrapped around it.
    /// </summary>
    public static class RankLadder
    {
        // ── tiers ────────────────────────────────────────────────────────────────
        //  Six 200-wide bands from the 1200 start. Cosmetic only — matchmaking is by
        //  raw MMR, not tier.
        static readonly (int floor, string name)[] Tiers =
        {
            (   0, "BRONZE"),
            (1000, "SILVER"),
            (1200, "GOLD"),
            (1400, "PLATINUM"),
            (1600, "DIAMOND"),
            (1800, "APEX"),
        };

        public static string TierName(int mmr)
        {
            string n = Tiers[0].name;
            foreach (var t in Tiers) if (mmr >= t.floor) n = t.name;
            return n;
        }

        /// <summary>0..1 progress from this tier's floor to the next (1 at the top tier).</summary>
        public static float TierProgress(int mmr)
        {
            for (int i = 0; i < Tiers.Length; i++)
            {
                if (mmr < Tiers[i].floor) continue;
                if (i == Tiers.Length - 1) return 1f;
                int lo = Tiers[i].floor, hi = Tiers[i + 1].floor;
                if (mmr < hi) return Mathf.Clamp01((mmr - lo) / (float)(hi - lo));
            }
            return 0f;
        }

        public static int NextTierAt(int mmr)
        {
            foreach (var t in Tiers) if (mmr < t.floor) return t.floor;
            return Tiers[Tiers.Length - 1].floor;
        }

        // ── Elo update ───────────────────────────────────────────────────────────

        const int   K          = 32;
        const int   MinSwing    = 6;   // a win/loss always moves the needle
        const int   MaxSwing    = 40;

        /// <summary>Signed MMR change for the player against an opponent of <paramref name="oppMmr"/>.</summary>
        public static int Delta(bool won, int myMmr, int oppMmr)
        {
            double expected = 1.0 / (1.0 + Math.Pow(10.0, (oppMmr - myMmr) / 400.0));
            double raw      = K * ((won ? 1.0 : 0.0) - expected);
            int d = (int)Math.Round(raw);
            int mag = Mathf.Clamp(Mathf.Abs(d), MinSwing, MaxSwing);
            return won ? mag : -mag;
        }

        /// <summary>Apply a ranked result: move MMR, log the outcome, return the delta.</summary>
        public static int ApplyRanked(bool won, int oppMmr, string oppPilot, int level)
        {
            int before = PlayerProgress.MMR;
            int delta  = Delta(won, before, oppMmr);
            PlayerProgress.AdjustMMR(delta);
            int after  = PlayerProgress.MMR;

            var log = Load();
            log.Insert(0, new MatchOutcome
            {
                utc = DateTime.UtcNow.ToBinary(),
                won = won, mmrBefore = before, mmrAfter = after,
                oppMmr = oppMmr, oppPilot = string.IsNullOrEmpty(oppPilot) ? "GHOST" : oppPilot,
                level = level,
            });
            if (log.Count > 30) log.RemoveRange(30, log.Count - 30);
            Save(log);
            return delta;
        }

        // ── history persistence ──────────────────────────────────────────────────

        [Serializable] class Blob { public List<MatchOutcome> items = new List<MatchOutcome>(); }
        const string HistoryKey = "rl_history";

        public static List<MatchOutcome> History() => Load();

        // ── daily-board leaderboard ──────────────────────────────────────────────

        /// <summary>
        /// Rank every ghost recorded on today's board for <paramref name="level"/>.
        /// Wins rank above losses; within wins the faster clear leads, within losses the
        /// longer survival leads; ties break to whoever set it first. Pure — the caller
        /// passes the pool (normally <c>GhostStore.All()</c>) so this stays testable.
        /// </summary>
        public static List<LeaderboardEntry> Standings(IEnumerable<GhostRecord> pool, int level,
                                                       string mePilot, DateTime? nowUtc = null)
            => Standings(pool, level, DailySeed(level, nowUtc), mePilot);

        /// <summary>As above but for an explicit board seed — the online backend already knows it.</summary>
        public static List<LeaderboardEntry> Standings(IEnumerable<GhostRecord> pool, int level,
                                                       int seed, string mePilot)
        {
            var todays = new List<GhostRecord>();
            if (pool != null)
                foreach (var g in pool)
                    if (g != null && g.level == level && g.seed == seed) todays.Add(g);

            todays.Sort(CompareForBoard);

            var list = new List<LeaderboardEntry>(todays.Count);
            for (int i = 0; i < todays.Count; i++)
            {
                var g = todays[i];
                list.Add(new LeaderboardEntry
                {
                    rank = i + 1, pilot = g.pilot, pilotId = g.pilotId, mmr = g.mmr, won = g.PilotWon,
                    durationTicks = g.durationTicks, recordedUtc = g.recordedUtc,
                    isYou = !string.IsNullOrEmpty(mePilot) && g.pilot == mePilot,
                    ghostId = g.id, isRemote = false,
                });
            }
            return list;
        }

        /// <summary>Board rank order: wins before losses; within wins fastest clear leads;
        /// within losses longest survival leads; ties break to whoever set it first.</summary>
        static int CompareForBoard(GhostRecord a, GhostRecord b)
        {
            if (a.PilotWon != b.PilotWon) return a.PilotWon ? -1 : 1;
            int c = a.PilotWon ? a.durationTicks.CompareTo(b.durationTicks)
                               : b.durationTicks.CompareTo(a.durationTicks);
            return c != 0 ? c : a.recordedUtc.CompareTo(b.recordedUtc);
        }

        /// <summary>Does <paramref name="candidate"/> rank ahead of <paramref name="incumbent"/>
        /// on the same board? The rule for overwriting a leaderboard entry.</summary>
        public static bool Beats(GhostRecord candidate, GhostRecord incumbent)
        {
            if (incumbent == null) return true;
            if (candidate == null) return false;
            return CompareForBoard(candidate, incumbent) < 0;
        }

        /// <summary>One monotonic key so a remote store ranks a board with a single ORDER BY desc.
        /// Wins sit a full band above losses; within wins faster is higher, within losses longer is
        /// higher. durationTicks is minutes-scale (&lt;&lt; 1e9) so the bands never overlap.</summary>
        public static long SortKey(bool won, int durationTicks)
        {
            const long BAND = 1_000_000_000L;
            return (won ? BAND : 0L) + (won ? BAND - durationTicks : durationTicks);
        }
        public static long SortKey(GhostRecord g) => g != null && SortKeyValid(g)
            ? SortKey(g.PilotWon, g.durationTicks) : 0L;
        static bool SortKeyValid(GhostRecord g) => g.durationTicks >= 0 && g.durationTicks < 1_000_000_000;

        /// <summary>The player's best rank on today's board for a level, or 0 if they have no entry.</summary>
        public static int YourRank(IEnumerable<GhostRecord> pool, int level, string mePilot, DateTime? nowUtc = null)
        {
            foreach (var e in Standings(pool, level, mePilot, nowUtc))
                if (e.isYou) return e.rank;
            return 0;
        }

        static List<MatchOutcome> Load()
        {
            string raw = PlayerPrefs.GetString(HistoryKey, "");
            if (string.IsNullOrEmpty(raw)) return new List<MatchOutcome>();
            try
            {
                var b = JsonUtility.FromJson<Blob>(raw);
                return b?.items ?? new List<MatchOutcome>();
            }
            catch { return new List<MatchOutcome>(); }
        }

        static void Save(List<MatchOutcome> items)
        {
            try
            {
                PlayerPrefs.SetString(HistoryKey, JsonUtility.ToJson(new Blob { items = items }));
                PlayerPrefs.Save();
            }
            catch { /* history is non-critical */ }
        }

        // ── season / weekly seed ─────────────────────────────────────────────────

        /// <summary>Board + combat seed for a level on a given calendar day (UTC). Every pilot
        /// who plays level N today gets the same board, so ghosts recorded today are a true
        /// head-to-head. Ghosts from other days still replay fine — the recording is
        /// board-independent — they are just not a like-for-like race.</summary>
        public static int DailySeed(int level, DateTime? nowUtc = null)
        {
            var n = (nowUtc ?? DateTime.UtcNow).Date;
            return ((n.Year * 10000 + n.Month * 100 + n.Day) * 31 + level) & 0x7FFFFFFF;
        }

        public static string SeasonId(DateTime? nowUtc = null)
        {
            var n = nowUtc ?? DateTime.UtcNow;
            int q = (n.Month - 1) / 3 + 1;
            return $"S{(n.Year - 2026) * 4 + q}";
        }

        /// <summary>Deterministic seed for this ISO week's Breach run — same for everyone.</summary>
        public static int BreachSeed(DateTime? nowUtc = null)
        {
            var n = nowUtc ?? DateTime.UtcNow;
            var cal  = System.Globalization.CultureInfo.InvariantCulture.Calendar;
            int week = cal.GetWeekOfYear(n, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday);
            return n.Year * 100 + week;
        }
    }
}
