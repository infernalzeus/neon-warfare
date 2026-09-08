using System.Collections.Generic;
using NW.Board.Domain;
using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Cross-game persistent progress with 3 save-slot profiles.
    /// Each slot stores its own currency and unlocked levels independently.
    /// Tokens and cosmetics are global (not per-slot) since they represent progression.
    /// No MTX: tokens earned by playing only.
    /// </summary>
    public static class PlayerProgress
    {
        public const int SlotCount = 3;

        // Currently active slot (-1 = no slot loaded yet)
        public static int CurrentSlot { get; private set; } = -1;

        public static readonly int[] Currency = new int[BoardModel.GemKindCount];
        public static readonly HashSet<int> UnlockedLevels = new();

        // ── pilot identity (global, stable across sessions) ──────────────────────

        /// <summary>A stable per-install id. This is the pilot's display-side identity and the
        /// leaderboard document key; a Firebase anon uid (if the online backend is enabled) is
        /// mapped onto it at sign-in. Survives slot switches; only a full app-data wipe resets it.</summary>
        public static string PilotId
        {
            get
            {
                string id = PlayerPrefs.GetString("pp_pilot_id", "");
                if (string.IsNullOrEmpty(id))
                {
                    id = System.Guid.NewGuid().ToString("N");
                    PlayerPrefs.SetString("pp_pilot_id", id);
                    PlayerPrefs.Save();
                }
                return id;
            }
        }

        /// <summary>The player's chosen display name — one per install, shown on every slot card
        /// and every leaderboard row. Claimed as globally-unique via <see cref="UsernameService"/>.
        /// Empty until the player sets one (then the game shows "PILOT").</summary>
        public static string PilotName
        {
            get => PlayerPrefs.GetString("pp_pilotname", "");
            set { PlayerPrefs.SetString("pp_pilotname", (value ?? "").Trim()); PlayerPrefs.Save(); }
        }

        public static bool HasPilotName => !string.IsNullOrEmpty(PilotName);

        // ── dev conveniences (testing only) ─────────────────────────────────────

        /// <summary>When true: the dev slot starts fully unlocked and the pilot-select screen shows
        /// a "TESTING BUILD" notice. On in the editor, and in any build compiled with the
        /// <c>NW_DEV</c> scripting define. **Remove <c>NW_DEV</c> from Player Settings before
        /// building the Production track** so real players get a clean start.</summary>
        public static bool DevMode
        {
            get
            {
#if NW_DEV
                return true;
#else
                return Application.isEditor;
#endif
            }
        }

        /// <summary>Under <see cref="DevMode"/>, only this slot starts unlocked; the others start
        /// clean, exactly like a shipped build.</summary>
        public const int DevSlot = 2;

        const string FreshCurrency = "0,0,0,0,0";
        const string FreshUnlocks  = "1";
        const string DevCurrency   = "9999,9999,9999,9999,9999";
        const string DevUnlocks    = "1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20";

        // ── MMR (competitive rating, global) ─────────────────────────────────────

        public const int DefaultMMR = 1200;

        public static int MMR
        {
            get => PlayerPrefs.GetInt("pp_mmr", DefaultMMR);
            private set { PlayerPrefs.SetInt("pp_mmr", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        /// <summary>
        /// Legacy flat delta (Win +25 / Lose −20), still used by the LAN VS path where the
        /// opponent's rating is unknown. Ghost matches use <see cref="RankLadder.ApplyRanked"/>,
        /// which computes a real Elo swing and goes through <see cref="AdjustMMR"/>.
        /// </summary>
        public static void ApplyMatchResult(bool won) => MMR += won ? 25 : -20;

        /// <summary>Move MMR by a signed amount (the Elo delta from <see cref="RankLadder"/>).</summary>
        public static void AdjustMMR(int delta) => MMR += delta;

        // ── tokens (global, earned by winning levels) ──────────────────────────

        public static int Tokens
        {
            get => PlayerPrefs.GetInt("pp_tokens", 0);
            private set { PlayerPrefs.SetInt("pp_tokens", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static void AddTokens(int amount) => Tokens += Mathf.Max(0, amount);

        /// <summary>Gems earned on the match board and armory tokens are deliberately separate
        /// currencies -- one is match income, the other is what cosmetics cost. Until now there
        /// was no bridge between them, so a player could sit on 9,999 of every gem and still be
        /// unable to buy anything in the shop. This is that bridge.</summary>
        // A flat 40 -> 25 for every gem was wrong: the five gems are not worth the same.
        // The unit catalogue prices them by scarcity -- gem 0 buys a 3-cost drone, while gem 4
        // buys nothing but the 10-cost titan -- so the exchange follows the same ladder.
        // Rarer gems are worth more per unit, which is the whole point of holding them.
        //                                       0    1    2    3    4
        public static readonly int[] GemCost  = { 60,  50,  40,  30,  20 };
        public static readonly int[] GemYield = { 20,  20,  22,  25,  30 };
        public static readonly string[] GemTier =
            { "COMMON", "COMMON", "UNCOMMON", "RARE", "EPIC" };

        public static int ConvertCostOf(int gemIndex)
            => gemIndex >= 0 && gemIndex < GemCost.Length ? GemCost[gemIndex] : 40;
        public static int ConvertYieldOf(int gemIndex)
            => gemIndex >= 0 && gemIndex < GemYield.Length ? GemYield[gemIndex] : 25;
        public static string ConvertTierOf(int gemIndex)
            => gemIndex >= 0 && gemIndex < GemTier.Length ? GemTier[gemIndex] : "";

        public static bool CanConvert(int gemIndex)
            => gemIndex >= 0 && gemIndex < Currency.Length
               && Currency[gemIndex] >= ConvertCostOf(gemIndex);

        /// <summary>Spend gems of one kind for armory tokens. Returns false if short.</summary>
        public static bool ConvertToTokens(int gemIndex)
        {
            if (!CanConvert(gemIndex)) return false;
            Currency[gemIndex] -= ConvertCostOf(gemIndex);
            Save();
            AddTokens(ConvertYieldOf(gemIndex));
            return true;
        }

        // ── permanent battle boosts (global, purchased with tokens) ───────────
        // Levels 0-3: 0 = none, 1 = +5%, 2 = +10%, 3 = +20%

        public static readonly int[] BoostDamageCost  = { 0, 150, 300, 600 };  // cost to reach each level
        public static readonly int[] BoostSpeedCost   = { 0, 150, 300, 600 };
        public static readonly float[] BoostDamageMult = { 1.00f, 1.05f, 1.10f, 1.20f };
        public static readonly float[] BoostSpeedMult  = { 1.00f, 1.05f, 1.10f, 1.20f };

        public static int DamageBoostLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("pp_boost_dmg", 0), 0, 3);
            private set { PlayerPrefs.SetInt("pp_boost_dmg", Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
        }

        public static int SpeedBoostLevel
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt("pp_boost_spd", 0), 0, 3);
            private set { PlayerPrefs.SetInt("pp_boost_spd", Mathf.Clamp(value, 0, 3)); PlayerPrefs.Save(); }
        }

        public static float ActiveDamageMult => BoostDamageMult[DamageBoostLevel];
        public static float ActiveSpeedMult  => BoostSpeedMult[SpeedBoostLevel];

        public static bool TryUpgradeDamageBoost()
        {
            int next = DamageBoostLevel + 1;
            if (next > 3) return false;
            if (!SpendTokens(BoostDamageCost[next])) return false;
            DamageBoostLevel = next;
            return true;
        }

        public static bool TryUpgradeSpeedBoost()
        {
            int next = SpeedBoostLevel + 1;
            if (next > 3) return false;
            if (!SpendTokens(BoostSpeedCost[next])) return false;
            SpeedBoostLevel = next;
            return true;
        }

        /// <summary>Marks the session as having no active slot without wiping save data.
        /// The next ShowStartScreen() will route through ProfileSelectScreen again.</summary>
        public static void ClearCurrentSlot() => CurrentSlot = -1;

        public static bool SpendTokens(int amount)
        {
            if (Tokens < amount) return false;
            Tokens -= amount;
            return true;
        }

        // ── cosmetics (global bitmask per category, stored in PlayerPrefs) ──────

        public static bool HasCosmetic(int category, int index)
        {
            string raw = PlayerPrefs.GetString($"pp_cosm_{category}", "");
            foreach (var s in raw.Split(','))
                if (int.TryParse(s, out int v) && v == index) return true;
            return false;
        }

        public static void UnlockCosmetic(int category, int index)
        {
            if (HasCosmetic(category, index)) return;
            string raw = PlayerPrefs.GetString($"pp_cosm_{category}", "");
            string next = string.IsNullOrEmpty(raw) ? index.ToString() : raw + "," + index;
            PlayerPrefs.SetString($"pp_cosm_{category}", next);
            PlayerPrefs.Save();
        }

        // ─────────────────────────────────────────── slot discovery ────────

        public static bool SlotExists(int slot)
            => PlayerPrefs.HasKey(CurrencyKey(slot));

        /// <summary>Returns preview data without altering active state.</summary>
        public static (string name, int highestLevel, int[] currency) GetSlotPreview(int slot)
        {
            if (!SlotExists(slot))
                return ($"PILOT {slot + 1}", 0, null);

            var c = new int[BoardModel.GemKindCount];
            string raw = PlayerPrefs.GetString(CurrencyKey(slot), "");
            if (!string.IsNullOrEmpty(raw))
            {
                var parts = raw.Split(',');
                for (int i = 0; i < parts.Length && i < c.Length; i++)
                    if (int.TryParse(parts[i], out int v)) c[i] = v;
            }

            int highest = 1;
            string unlockRaw = PlayerPrefs.GetString(UnlockKey(slot), "");
            if (!string.IsNullOrEmpty(unlockRaw))
                foreach (var s in unlockRaw.Split(','))
                    if (int.TryParse(s, out int lv) && lv > highest) highest = lv;

            string name = PlayerPrefs.GetString(NameKey(slot), $"PILOT {slot + 1}");
            return (name, highest, c);
        }

        // ─────────────────────────────────────────── slot operations ───────

        public static void LoadSlot(int slot)
        {
            CurrentSlot = slot;
            for (int i = 0; i < BoardModel.GemKindCount; i++) Currency[i] = 0;
            UnlockedLevels.Clear();
            UnlockedLevels.Add(1);

            string raw = PlayerPrefs.GetString(CurrencyKey(slot), "");
            if (!string.IsNullOrEmpty(raw))
            {
                var parts = raw.Split(',');
                for (int i = 0; i < parts.Length && i < Currency.Length; i++)
                    if (int.TryParse(parts[i], out int v)) Currency[i] = v;
            }

            string unlockRaw = PlayerPrefs.GetString(UnlockKey(slot), "");
            if (!string.IsNullOrEmpty(unlockRaw))
                foreach (var s in unlockRaw.Split(','))
                    if (int.TryParse(s, out int lv)) UnlockedLevels.Add(lv);
        }

        /// <summary>Create a brand-new slot. A real player starts with an empty wallet and only
        /// level 1 open. Under <see cref="DevMode"/> the <see cref="DevSlot"/> starts fully
        /// unlocked for review; every other slot still starts clean.</summary>
        public static void NewSlot(int slot)
        {
            bool dev = DevMode && slot == DevSlot;
            PlayerPrefs.SetString(CurrencyKey(slot), dev ? DevCurrency : FreshCurrency);
            PlayerPrefs.SetString(UnlockKey(slot),   dev ? DevUnlocks  : FreshUnlocks);
            PlayerPrefs.SetString(NameKey(slot), $"PILOT {slot + 1}");
            PlayerPrefs.Save();
            LoadSlot(slot);
        }

        public static void DeleteSlot(int slot)
        {
            PlayerPrefs.DeleteKey(CurrencyKey(slot));
            PlayerPrefs.DeleteKey(UnlockKey(slot));
            PlayerPrefs.DeleteKey(NameKey(slot));
            PlayerPrefs.Save();
            if (CurrentSlot == slot)
            {
                CurrentSlot = -1;
                for (int i = 0; i < BoardModel.GemKindCount; i++) Currency[i] = 0;
                UnlockedLevels.Clear();
                UnlockedLevels.Add(1);
            }
        }

        // ─────────────────────────────────────────────── query ────────────

        public static bool IsLevelUnlocked(int level)
            => level == 1 || UnlockedLevels.Contains(level);

        public static bool CanUnlockLevel(int level)
        {
            if (IsLevelUnlocked(level)) return false;
            var cost = LevelConfig.UnlockCosts(level);
            for (int i = 0; i < cost.Length && i < Currency.Length; i++)
                if (Currency[i] < cost[i]) return false;
            return true;
        }

        // ─────────────────────────────────────────────── mutate ───────────

        public static void AwardPostGame(int[] earned, int level)
        {
            for (int i = 0; i < BoardModel.GemKindCount && i < earned.Length; i++)
                Currency[i] += earned[i];
            for (int i = 0; i < BoardModel.GemKindCount; i++)
                if (Currency[i] > 9999) Currency[i] = 9999;
            // Token reward scales with level
            AddTokens(LevelConfig.BaseTokenReward(level));
            if (CurrentSlot >= 0) Save();
        }

        public static bool TryUnlockLevel(int level)
        {
            if (!CanUnlockLevel(level)) return false;
            var cost = LevelConfig.UnlockCosts(level);
            for (int i = 0; i < cost.Length && i < Currency.Length; i++)
                Currency[i] -= cost[i];
            UnlockedLevels.Add(level);
            if (CurrentSlot >= 0) Save();
            return true;
        }

        public static void Reset()
        {
            for (int i = 0; i < BoardModel.GemKindCount; i++) Currency[i] = 0;
            UnlockedLevels.Clear();
            UnlockedLevels.Add(1);
            if (CurrentSlot >= 0) Save();
        }

        // ─────────────────────────────────────────────────── I/O ─────────

        static void Save()
        {
            if (CurrentSlot < 0) return;
            PlayerPrefs.SetString(CurrencyKey(CurrentSlot), string.Join(",", Currency));
            var sb = new System.Text.StringBuilder();
            foreach (int lv in UnlockedLevels)
            {
                if (sb.Length > 0) sb.Append(',');
                sb.Append(lv);
            }
            PlayerPrefs.SetString(UnlockKey(CurrentSlot), sb.ToString());
            PlayerPrefs.Save();
        }

        static string CurrencyKey(int slot) => $"pp_s{slot}_currency";
        static string UnlockKey(int slot)   => $"pp_s{slot}_unlocked";
        static string NameKey(int slot)     => $"pp_s{slot}_name";

        /// <summary>The name shown for this player everywhere (ghost authorship, leaderboard rows):
        /// the globally-unique <see cref="PilotName"/> if they've set one, otherwise the active
        /// slot's local name, otherwise "PILOT".</summary>
        public static string CurrentSlotName()
        {
            if (HasPilotName) return PilotName;
            return CurrentSlot >= 0
                ? PlayerPrefs.GetString(NameKey(CurrentSlot), $"PILOT {CurrentSlot + 1}")
                : "PILOT";
        }

        /// <summary>Rename a save slot. The key was always written as "PILOT n" and never
        /// changed, so the field existed but nothing could set it.</summary>
        public static void SetSlotName(int slot, string name)
        {
            name = (name ?? "").Trim();
            if (name.Length > 12) name = name.Substring(0, 12);
            if (name.Length == 0) name = $"PILOT {slot + 1}";
            PlayerPrefs.SetString(NameKey(slot), name);
            PlayerPrefs.Save();
        }
    }
}
