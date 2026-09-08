using System;
using UnityEngine;
#if NW_FIREBASE
using System.Collections.Generic;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
#endif

namespace NW.App
{
    /// <summary>
    /// Claims a globally-unique pilot name in Firestore (<c>usernames/{key}</c>) so no two players
    /// share a leaderboard identity. The name itself lives in <see cref="PlayerProgress.PilotName"/>.
    ///
    /// Degrades cleanly: without the Firebase backend (or while offline) a name is still set
    /// locally — just not reserved — and can be claimed on the next successful attempt while online.
    /// </summary>
    public static class UsernameService
    {
        public enum Status { Ok, Taken, Invalid, Offline, Error }

        public const int MinLen = 3;
        public const int MaxLen = 16;
        const string ClaimedKeyPref = "pp_username_key";

        /// <summary>Trim, collapse internal whitespace, keep only letters / digits / _ / - / space.</summary>
        public static string Normalize(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            var sb = new System.Text.StringBuilder(raw.Length);
            bool lastWasSpace = false;
            foreach (char c in raw.Trim())
            {
                if (char.IsLetterOrDigit(c) || c == '_' || c == '-') { sb.Append(c); lastWasSpace = false; }
                else if (char.IsWhiteSpace(c) && !lastWasSpace && sb.Length > 0) { sb.Append(' '); lastWasSpace = true; }
            }
            return sb.ToString().Trim();
        }

        public static bool IsValid(string display)
        {
            int n = Normalize(display).Length;
            return n >= MinLen && n <= MaxLen;
        }

        static string KeyOf(string display) => Normalize(display).ToLowerInvariant().Replace(' ', '_');

        /// <summary>Attempt to make <paramref name="desired"/> this player's unique name. The
        /// callback fires on the main thread with the outcome and the accepted display string
        /// (unchanged from the current name on failure).</summary>
        public static void TryClaim(string desired, Action<Status, string> done)
        {
            string display = Normalize(desired);
            if (!IsValid(display)) { done?.Invoke(Status.Invalid, PlayerProgress.PilotName); return; }

#if NW_FIREBASE
            if (Leaderboard.IsOnline) { ClaimRemote(display, done); return; }
#endif
            PlayerProgress.PilotName = display;                 // local, unreserved
            done?.Invoke(Status.Offline, display);
        }

#if NW_FIREBASE
        static void ClaimRemote(string display, Action<Status, string> done)
        {
            var db  = FirebaseFirestore.DefaultInstance;
            var usr = FirebaseAuth.DefaultInstance.CurrentUser;
            string uid = usr != null ? usr.UserId : "";
            string newKey = KeyOf(display);
            string oldKey = PlayerPrefs.GetString(ClaimedKeyPref, "");
            var newRef = db.Collection("usernames").Document(newKey);

            newRef.GetSnapshotAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) { done?.Invoke(Status.Error, PlayerProgress.PilotName); return; }

                bool ownedByOther = t.Result.Exists
                    && (!t.Result.TryGetValue("uid", out string owner) || owner != uid);
                if (ownedByOther) { done?.Invoke(Status.Taken, PlayerProgress.PilotName); return; }

                var data = new Dictionary<string, object>
                {
                    ["pilotId"]   = PlayerProgress.PilotId,
                    ["uid"]       = uid,
                    ["name"]      = display,
                    ["claimedAt"] = FieldValue.ServerTimestamp,
                };
                newRef.SetAsync(data).ContinueWithOnMainThread(w =>
                {
                    if (w.IsFaulted) { done?.Invoke(Status.Error, PlayerProgress.PilotName); return; }
                    if (!string.IsNullOrEmpty(oldKey) && oldKey != newKey)
                        db.Collection("usernames").Document(oldKey).DeleteAsync();   // release the old name
                    PlayerProgress.PilotName = display;
                    PlayerPrefs.SetString(ClaimedKeyPref, newKey);
                    PlayerPrefs.Save();
                    done?.Invoke(Status.Ok, display);
                });
            });
        }
#endif
    }
}
