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
    /// share a leaderboard identity. Names are per save slot — one device holds up to 3 — so every
    /// call is scoped to a slot; the name itself is stored via <see cref="PlayerProgress.SetSlotName"/>.
    ///
    /// Degrades cleanly: without the Firebase backend (or while offline) a name is still set
    /// locally — just not reserved — and can be claimed on the next successful attempt while online.
    /// </summary>
    public static class UsernameService
    {
        public enum Status { Ok, Taken, Invalid, Offline, Error }

        public const int MinLen = 3;
        public const int MaxLen = 16;
        static string ClaimedKeyPref(int slot) => $"pp_s{slot}_username_key";

        static string SlotName(int slot) => PlayerProgress.GetSlotPreview(slot).name;

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

        /// <summary>Attempt to make <paramref name="desired"/> the unique name of save slot
        /// <paramref name="slot"/>. The callback fires on the main thread with the outcome and the
        /// accepted display string (unchanged from the slot's current name on failure).</summary>
        public static void TryClaim(string desired, int slot, Action<Status, string> done)
        {
            string display = Normalize(desired);
            if (!IsValid(display)) { done?.Invoke(Status.Invalid, SlotName(slot)); return; }

#if NW_FIREBASE
            if (Leaderboard.IsOnline) { ClaimRemote(display, slot, done); return; }
#endif
            PlayerProgress.SetSlotName(slot, display);          // local, unreserved
            done?.Invoke(Status.Offline, display);
        }

#if NW_FIREBASE
        static void ClaimRemote(string display, int slot, Action<Status, string> done)
        {
            var db  = FirebaseFirestore.DefaultInstance;
            var usr = FirebaseAuth.DefaultInstance.CurrentUser;
            string uid = usr != null ? usr.UserId : "";
            string newKey = KeyOf(display);
            string oldKey = PlayerPrefs.GetString(ClaimedKeyPref(slot), "");
            var newRef = db.Collection("usernames").Document(newKey);

            newRef.GetSnapshotAsync().ContinueWithOnMainThread(t =>
            {
                if (t.IsFaulted) { done?.Invoke(Status.Error, SlotName(slot)); return; }

                bool ownedByOther = t.Result.Exists
                    && (!t.Result.TryGetValue("uid", out string owner) || owner != uid);
                if (ownedByOther) { done?.Invoke(Status.Taken, SlotName(slot)); return; }

                var data = new Dictionary<string, object>
                {
                    ["pilotId"]   = PlayerProgress.PilotIdForSlot(slot),
                    ["uid"]       = uid,
                    ["name"]      = display,
                    ["claimedAt"] = FieldValue.ServerTimestamp,
                };
                newRef.SetAsync(data).ContinueWithOnMainThread(w =>
                {
                    if (w.IsFaulted) { done?.Invoke(Status.Error, SlotName(slot)); return; }
                    if (!string.IsNullOrEmpty(oldKey) && oldKey != newKey)
                        db.Collection("usernames").Document(oldKey).DeleteAsync();   // release the old name
                    PlayerProgress.SetSlotName(slot, display);
                    PlayerPrefs.SetString(ClaimedKeyPref(slot), newKey);
                    PlayerPrefs.Save();
                    done?.Invoke(Status.Ok, display);
                });
            });
        }
#endif
    }
}
