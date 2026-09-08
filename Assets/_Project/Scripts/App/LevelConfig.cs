using System.Collections.Generic;

namespace NW.App
{
    /// <summary>
    /// Static configuration for each of the 20 levels.
    /// Levels 1–5: 3 gem colors. 6–12: 4 colors. 13–20: 5 colors.
    /// Director multiplier ramps linearly from 0.45 (lvl 1) to 2.0 (lvl 20).
    /// Troops unlock progressively; mech+interceptor available from lvl 5+.
    /// </summary>
    public static class LevelConfig
    {
        public const int MaxLevel = 20;

        public static int GemColorCount(int level)
        {
            if (level <= 5)  return 3;
            if (level <= 12) return 4;
            return 5;
        }

        /// <summary>
        /// Board edge length. Boards are SQUARE and grow 5×5 → 8×8 across the campaign,
        /// capped at 8 so the grid stays thumb-legible in portrait (8 cols ≈ 360px at 45px/cell).
        /// Curve (tunable): 1–4 = 5, 5–9 = 6, 10–14 = 7, 15–20 = 8.
        /// </summary>
        public static int BoardEdge(int level)
        {
            if (level <= 4)  return 5;
            if (level <= 9)  return 6;
            if (level <= 14) return 7;
            return 8;
        }

        /// <summary>Board columns for this level (square board — equals rows).</summary>
        public static int BoardCols(int level) => BoardEdge(level);

        /// <summary>Board rows for this level (square board — equals cols).</summary>
        public static int BoardRows(int level) => BoardEdge(level);

        public static float DirectorMult(int level)
            => 0.45f + (level - 1) * 0.082f; // 0.45 → ~2.0

        /// <summary>
        /// Multi-gem cost to unlock a level. Index = GemKind (0=Energy,1=Plasma,2=Nano,3=Quantum,4=Data).
        /// Cost tiers mirror gem count: low levels cost only Energy; mid adds Plasma; high adds Nano+Quantum.
        /// </summary>
        public static int[] UnlockCosts(int level)
        {
            var c = new int[5];
            if (level <= 1) return c;
            int t = level - 1; // 1..19
            if (t <= 4)       { c[0] = t * 10; }
            else if (t <= 8)  { c[0] = 50; c[1] = (t - 4) * 10; }
            else if (t <= 12) { c[0] = 50; c[1] = 40; c[2] = (t - 8) * 10; }
            else if (t <= 16) { c[0] = 50; c[1] = 40; c[2] = 40; c[3] = (t - 12) * 10; }
            else              { c[0] = 50; c[1] = 40; c[2] = 40; c[3] = 40; c[4] = (t - 16) * 10; }
            return c;
        }

        /// <summary>Which troop IDs are available for deployment at this level.</summary>
        public static List<string> AvailableTroops(int level)
        {
            var list = new List<string> { "drone", "trooper", "turret" };
            if (level >= 3)  list.Add("sniper");
            if (level >= 5)  list.Add("mech");
            if (level >= 6)  list.Add("shield-bot");
            if (level >= 8)  list.Add("interceptor");
            if (level >= 10) list.Add("hacker");
            if (level >= 15) list.Add("titan");
            return list;
        }

        /// <summary>
        /// Core HP for BOTH cores at this level. Tuned so level 1 is a brisk ~2–3 min match and
        /// higher levels scale up with troop power. Was a flat 500 (level 1 dragged on).
        /// L1 ≈ 220 → L20 ≈ 1208. Cross-check vs TroopStats DPS when tuning.
        /// </summary>
        public static float CoreHp(int level) => 220f + (level - 1) * 52f;

        /// <summary>Tokens rewarded per game on this level (base; multiplied by score).</summary>
        public static int BaseTokenReward(int level) => 10 + level * 5;

        public static string LevelName(int level)
        {
            if (level <= 3)  return "TRAINING";
            if (level <= 7)  return "SKIRMISH";
            if (level <= 12) return "CAMPAIGN";
            if (level <= 17) return "WARFRONT";
            return "APEX";
        }
    }
}
