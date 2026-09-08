// =============================================================================
//  Firebase (Firestore + Anonymous Auth) leaderboard backend — OPTIONAL.
//
//  This file compiles to NOTHING until you:
//    1. Create a Firebase project and an app for the Android package
//       `com.zeusengine.neonwarfare` (and an iOS app for the bundle id).
//    2. Enable  Firestore (Native mode)  and  Authentication → Anonymous.
//    3. Import the Firebase Unity SDK — FirebaseAuth + FirebaseFirestore
//       (+ External Dependency Manager). Do NOT import FirebaseAnalytics.
//    4. Drop  google-services.json  into  Assets/  (and GoogleService-Info.plist for iOS).
//    5. Add  NW_FIREBASE  to  Player Settings → Scripting Define Symbols  for the
//       platforms you want it on.
//    6. Paste the security rules and create the composite index — both are in
//       docs/design/18-leaderboard-firebase.md.
//
//  Firestore layout (one row per pilot per daily board, overwrite-if-better):
//    leaderboards/{level}_{dailySeed}/entries/{pilotId}
//        pilot pilotId mmr result durationTicks recordedUtc
//        score : long   -- RankLadder.SortKey, so the board ranks with one ORDER BY
//        ghost : string -- the GhostRecord JSON, pulled only on CHALLENGE
//        updatedAt : serverTimestamp
//
//  API shape below targets Firebase Unity SDK 12.x — sanity-check names against
//  your imported version if the compiler complains.
// =============================================================================
#if NW_FIREBASE
using System;
using System.Collections.Generic;
using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using UnityEngine;

namespace NW.App
{
    public sealed class FirebaseLeaderboardBackend : ILeaderboardBackend
    {
        const int  GhostByteCap = 60_000;   // matches the security rule
        const string Root        = "leaderboards";

        readonly FirebaseFirestore _db;
        string _status = "starting";

        public string Name     => "Firebase";
        public bool   IsRemote => true;
        public string Status   => _status;

        FirebaseLeaderboardBackend(FirebaseFirestore db) { _db = db; _status = "ready"; }

        // ---- bootstrap ----------------------------------------------------------

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void TryInstall()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(depTask =>
            {
                if (depTask.Result != DependencyStatus.Available)
                {
                    Debug.LogWarning($"[Leaderboard] Firebase unavailable ({depTask.Result}); staying local.");
                    return;
                }
                FirebaseAuth.DefaultInstance.SignInAnonymouslyAsync().ContinueWithOnMainThread(authTask =>
                {
                    if (authTask.IsFaulted || authTask.IsCanceled)
                    {
                        Debug.LogWarning("[Leaderboard] anon sign-in failed; staying local.");
                        return;
                    }
                    Leaderboard.Configure(new FirebaseLeaderboardBackend(FirebaseFirestore.DefaultInstance));
                    Debug.Log("[Leaderboard] Firebase backend online.");
                });
            });
        }

        // ---- paths ------------------------------------------------------------

        CollectionReference Entries(int level, int dailySeed)
            => _db.Collection(Root).Document($"{level}_{dailySeed}").Collection("entries");

        // ---- submit ---------------------------------------------------------

        public void SubmitBest(GhostRecord run)
        {
            if (run == null) return;
            string json = JsonUtility.ToJson(run);
            if (System.Text.Encoding.UTF8.GetByteCount(json) > GhostByteCap) return; // too big to carry
            string pilotId = string.IsNullOrEmpty(run.pilotId) ? PlayerProgress.PilotId : run.pilotId;

            var doc = Entries(run.level, run.seed).Document(pilotId);
            doc.GetSnapshotAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) return;
                GhostRecord incumbent = t.Result.Exists ? Rehydrate(t.Result) : null;
                if (!RankLadder.Beats(run, incumbent)) return;

                var data = new Dictionary<string, object>
                {
                    ["pilot"]         = run.pilot,
                    ["pilotId"]       = pilotId,
                    ["mmr"]           = run.mmr,
                    ["result"]        = run.result,
                    ["durationTicks"] = run.durationTicks,
                    ["recordedUtc"]   = run.recordedUtc,
                    ["score"]         = RankLadder.SortKey(run),
                    ["ghost"]         = json,
                    ["updatedAt"]     = FieldValue.ServerTimestamp,
                };
                doc.SetAsync(data).ContinueWithOnMainThread(w =>
                {
                    if (w.IsFaulted) Debug.LogWarning("[Leaderboard] submit failed: " + w.Exception?.Message);
                });
            });
        }

        // ---- read -----------------------------------------------------------

        public void FetchTop(int level, int dailySeed, int limit, Action<IReadOnlyList<LeaderboardEntry>> done)
        {
            Entries(level, dailySeed)
                .OrderByDescending("score")
                .Limit(Mathf.Clamp(limit, 1, 100))
                .GetSnapshotAsync()
                .ContinueWithOnMainThread(t =>
                {
                    if (t.IsFaulted) { _status = "offline"; done?.Invoke(Local(level, dailySeed, limit)); return; }
                    var me = PlayerProgress.PilotId;
                    var rows = new List<LeaderboardEntry>();
                    int rank = 1;
                    foreach (var d in t.Result.Documents)
                        rows.Add(ToEntry(d, rank++, me));
                    done?.Invoke(rows);
                });
        }

        public void FetchAroundMe(int level, int dailySeed, int span, Action<IReadOnlyList<LeaderboardEntry>> done)
        {
            var col = Entries(level, dailySeed);
            var me  = PlayerProgress.PilotId;
            col.Document(me).GetSnapshotAsync().ContinueWithOnMainThread(selfTask =>
            {
                if (selfTask.IsFaulted || !selfTask.Result.Exists) { FetchTop(level, dailySeed, span * 2, done); return; }
                long myScore = selfTask.Result.TryGetValue("score", out long s) ? s : 0L;
                var self = ToEntry(selfTask.Result, 0, me);

                col.WhereGreaterThan("score", myScore).OrderBy("score").Limit(span).GetSnapshotAsync()
                   .ContinueWithOnMainThread(aboveTask =>
                {
                    col.WhereLessThan("score", myScore).OrderByDescending("score").Limit(span).GetSnapshotAsync()
                       .ContinueWithOnMainThread(belowTask =>
                    {
                        var rows = new List<LeaderboardEntry>();
                        if (!aboveTask.IsFaulted)
                        {
                            var a = aboveTask.Result.Documents;
                            var buf = new List<LeaderboardEntry>();
                            foreach (var d in a) buf.Add(ToEntry(d, 0, me));
                            buf.Reverse();                     // ascending score → worst-of-the-better first
                            rows.AddRange(buf);
                        }
                        rows.Add(self);
                        if (!belowTask.IsFaulted)
                            foreach (var d in belowTask.Result.Documents) rows.Add(ToEntry(d, 0, me));
                        done?.Invoke(rows);
                    });
                });
            });
        }

        public void FetchGhost(int level, int dailySeed, LeaderboardEntry entry, Action<GhostRecord> done)
        {
            // entry.ghostId is the entry doc id (== pilotId) for remote rows.
            Entries(level, dailySeed).Document(entry.ghostId)
               .GetSnapshotAsync()
               .ContinueWithOnMainThread(t =>
               {
                   if (t.IsFaulted || !t.Result.Exists) { done?.Invoke(null); return; }
                   done?.Invoke(t.Result.TryGetValue("ghost", out string j) ? JsonUtility.FromJson<GhostRecord>(j) : null);
               });
        }

        // ---- mapping ------------------------------------------------------

        static LeaderboardEntry ToEntry(DocumentSnapshot d, int rank, string mePilotId)
        {
            d.TryGetValue("result", out long result);
            d.TryGetValue("durationTicks", out long dur);
            d.TryGetValue("recordedUtc", out long rec);
            d.TryGetValue("mmr", out long mmr);
            d.TryGetValue("pilot", out string pilot);
            return new LeaderboardEntry
            {
                rank = rank, pilot = string.IsNullOrEmpty(pilot) ? "PILOT" : pilot,
                pilotId = d.Id, mmr = (int)mmr, won = result == 1,
                durationTicks = (int)dur, recordedUtc = rec,
                isYou = d.Id == mePilotId, ghostId = d.Id, isRemote = true,
            };
        }

        static GhostRecord Rehydrate(DocumentSnapshot d)
        {
            d.TryGetValue("result", out long result);
            d.TryGetValue("durationTicks", out long dur);
            d.TryGetValue("recordedUtc", out long rec);
            return new GhostRecord { result = (int)result, durationTicks = (int)dur, recordedUtc = rec };
        }

        List<LeaderboardEntry> Local(int level, int dailySeed, int limit)
        {
            var all = RankLadder.Standings(GhostStore.All(), level, dailySeed, PlayerProgress.CurrentSlotName());
            if (limit > 0 && all.Count > limit) all.RemoveRange(limit, all.Count - limit);
            return all;
        }
    }
}
#endif
