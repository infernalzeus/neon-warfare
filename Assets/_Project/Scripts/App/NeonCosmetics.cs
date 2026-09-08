using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Cosmetic unlocks — skin color, effect VFX, glow aura, board display.
    /// All unlocked with tokens earned from gameplay. Zero MTX.
    /// Default / None (index 0) is always free and pre-unlocked.
    /// </summary>
    public static class NeonCosmetics
    {
        // ── category enums ───────────────────────────────────────────────────────

        public enum SkinColor { Default = 0, Crimson = 1, Cobalt  = 2, Forest = 3, Shadow  = 4, Gold    = 5, Violet  = 6 }
        public enum TroopSkin { Default = 0, Golden = 1, Chrome = 2, Inferno = 3, Phantom = 4, Cosmic = 5,
                               Blizzard = 6, Neon = 7, Venom = 8, Shadow = 9, Solar = 10, Storm = 11, Bloodmoon = 12 }
        public enum LaneScene  { Default = 0, Ember   = 1, Frost   = 2, Neon   = 3, Void    = 4 }
        public enum SkinGlow  { None    = 0, Soft    = 1, Intense = 2, Pulse  = 3 }

        // ── deploy effects ───────────────────────────────────────────────────────
        // These replace the troop skins. A skin coated the unit for the whole match and hid
        // the theme art underneath; a deploy effect fires at the instant the player commits a
        // troop to a lane -- the one moment in the match they actually chose. Seven of the
        // thirteen troop skins also had no implementation at all and were still purchasable,
        // so retiring them removes 1,580 tokens of content that took currency and did nothing.
        public enum DeployFx  { None = 0, Lightning = 1, DropPod = 2, PhaseIn = 3, Banner = 4 }

        // ── VFX skins ────────────────────────────────────────────────────────────
        // Replaces SkinGlow. A glow was an aura scaled up BEHIND the unit -- it washed over
        // the silhouette and fought whichever theme's art was underneath. These sit beside
        // the troop instead: motes orbiting at hip height, arcs at the feet, a wake, a ring
        // on the floor. The unit stays readable and the theme keeps its look.
        public enum VfxSkin   { None = 0, EmberOrbit = 1, StaticArc = 2, FrostTrail = 3, HaloRing = 4 }
        public enum BoardSkin { Classic = 0, Crystal = 1, Pixel   = 2, Glitch = 3 }

        // Internal — still functional for card frames and gem trails wired in HUDView / BoardView
        // Trails fire on a match CLEAR -- the right shape for an effect, and the reason this
        // category survived the armory rebuild. Four more in the same spirit.
        public enum GemTrail  { None = 0, Ring = 1, Spark = 2, Pulse = 3,
                                Shatter = 4, Bloom = 5, Cascade = 6, Implode = 7 }
        public enum CardFrame { Standard= 0, Circuit = 1, Hexagon = 2, Razor  = 3 }

        // ── display names ────────────────────────────────────────────────────────

        public static readonly string[] SkinColorNames = { "DEFAULT", "CRIMSON", "COBALT",  "FOREST",  "SHADOW",  "GOLD",    "VIOLET"  };
        public static readonly string[] TroopSkinNames = {
            "DEFAULT", "GOLDEN", "CHROME", "INFERNO", "PHANTOM", "COSMIC",
            "BLIZZARD", "NEON", "VENOM", "SHADOW", "SOLAR", "STORM", "BLOODMOON"
        };
        public static readonly string[] LaneSceneNames  = { "DEFAULT", "EMBER",   "FROST",   "NEON",    "VOID"    };
        public static readonly string[] SkinGlowNames  = { "NONE",    "SOFT",    "INTENSE", "PULSE"   };
        public static readonly string[] DeployFxNames  = { "NONE", "LIGHTNING", "DROP POD", "PHASE IN", "BANNER" };
        public static readonly string[] VfxSkinNames   = { "NONE", "EMBER ORBIT", "STATIC ARC", "FROST TRAIL", "HALO RING" };
        public static readonly string[] BoardSkinNames  = { "CLASSIC", "CRYSTAL", "PIXEL",   "GLITCH"  };
        public static readonly string[] GemTrailNames   = { "NONE", "RING", "SPARK", "PULSE",
                                                            "SHATTER", "BLOOM", "CASCADE", "IMPLODE" };
        public static readonly string[] CardFrameNames  = { "STANDARD","CIRCUIT", "HEXAGON", "RAZOR"   };

        // ── descriptions ─────────────────────────────────────────────────────────

        public static readonly string[] SkinColorDesc = {
            "Use the active theme's troop tint",
            "Aggressive blood-red warrior",
            "Electric cobalt blue strike force",
            "Verdant forest ranger camouflage",
            "Shadow operative near-invisible",
            "Gilded champion elite paint",
            "Void-violet spectral coating",
        };
        public static readonly string[] TroopSkinDesc = {
            "Standard-issue tactical unit",
            "Molten gold alloy — sparks trail every move",
            "Mirror-polished chrome — reflects the battlefield",
            "Volcanic rock shell — lava seeps through the cracks",
            "Spectral void form — unseen until it strikes",
            "Void-forged matter — cosmic energy bleeds the edges",
            "Arctic ice shell — frost crystals swirl and shatter",
            "Electric neon grid — crackling arcs trace every edge",
            "Toxic acid coat — caustic drips corrode the ground",
            "Living darkness — shadow tendrils writhe and coil",
            "Solar-forged titan — corona erupts on every strike",
            "Thunder-clad warrior — lightning surges across the hull",
            "Crimson tide shell — lunar seal bleeds raw power",
        };
        public static readonly string[] LaneSceneDesc = {
            "Default dark battlefield",
            "Volcanic ember lanes — warm crimson glow",
            "Arctic frost lanes — cold blue shimmer",
            "Cyber neon lanes — vivid purple and teal",
            "Deep void lanes — cosmic darkness",
        };
        public static readonly string[] VfxSkinDesc = {
            "No effect",
            "Three fire motes circling at hip height",
            "Short arcs snapping between the feet and the ground",
            "Crystals settling in the troop's wake, then fading",
            "A single flat ring turning at the feet",
        };
        public static readonly string[] DeployFxDesc = {
            "No deployment effect",
            "A bolt strikes the lane and the troop is standing where it hit",
            "A pod slams down, the shell splits and the troop steps out",
            "The troop resolves out of scan-lines from the ground up",
            "A standard drives into the ground and the troop forms beside it",
        };
        public static readonly string[] SkinGlowDesc = {
            "No outer aura",
            "Subtle pulsing aura halo",
            "Blazing high-intensity radiance",
            "Rhythmic strobe glow pulse",
        };
        public static readonly string[] BoardSkinDesc = {
            "Default flat-color gem style",
            "Translucent gem with inner glow",
            "8-bit pixelated gem tiles",
            "Corrupted scanline glitch effect",
        };
        public static readonly string[] GemTrailDesc = {
            "No trail effect",
            "Expanding ring on match clear",
            "Spark burst on match clear",
            "Pulsing wave on match clear",
            "The gem breaks into wedges that fly apart",
            "Petals open outward, then curl away",
            "Sparks fall from the cleared cell and bounce",
            "Everything rushes inward, then one flash",
        };
        public static readonly string[] CardFrameDesc = {
            "Standard panel border",
            "Circuit-board trace border",
            "Hexagonal corner insets",
            "Razor-edge angled accent",
        };

        // ── token costs ──────────────────────────────────────────────────────────

        public static readonly int[] SkinColorCost = { 0,  80,  80,  80, 100, 110, 120 };
        public static readonly int[] TroopSkinCost  = { 0, 60, 110, 130, 200, 250, 150, 180, 200, 220, 280, 300, 350 };
        public static readonly int[] LaneSceneCost  = { 0,  80, 100, 120, 150 };
        public static readonly int[] SkinGlowCost  = { 0,  60, 110,  90 };
        public static readonly int[] DeployFxCost  = { 0, 120, 140,  90, 110 };
        public static readonly int[] VfxSkinCost   = { 0,  90,  90, 100,  70 };
        public static readonly int[] BoardSkinCost  = { 0, 100, 150, 200 };
        public static readonly int[] GemTrailCost   = { 0, 75, 125, 175, 90, 110, 120, 140 };
        public static readonly int[] CardFrameCost  = { 0,  80, 120, 160 };

        // ── tint tables ──────────────────────────────────────────────────────────

        static readonly Color[] SkinColorTints =
        {
            Color.white,                           // 0 DEFAULT  — from theme at runtime
            new Color(1.00f, 0.22f, 0.15f),        // 1 CRIMSON
            new Color(0.15f, 0.55f, 1.00f),        // 2 COBALT
            new Color(0.20f, 0.88f, 0.32f),        // 3 FOREST
            new Color(0.20f, 0.22f, 0.28f),        // 4 SHADOW
            new Color(1.00f, 0.82f, 0.12f),        // 5 GOLD
            new Color(0.72f, 0.18f, 1.00f),        // 6 VIOLET
        };

        static readonly Color[] TroopTints =
        {
            Color.white,                           // 0 DEFAULT   — from theme
            new Color(1.00f, 0.78f, 0.08f),        // 1 GOLDEN    — rich molten gold
            new Color(0.68f, 0.76f, 0.90f),        // 2 CHROME    — polished steel-blue
            new Color(0.48f, 0.07f, 0.02f),        // 3 INFERNO   — dark volcanic rock
            new Color(0.30f, 0.10f, 0.55f),        // 4 PHANTOM   — deep spectral indigo
            new Color(0.20f, 0.05f, 0.45f),        // 5 COSMIC    — deep void purple
            new Color(0.60f, 0.85f, 1.00f),        // 6 BLIZZARD  — arctic ice blue
            new Color(0.12f, 1.00f, 0.88f),        // 7 NEON      — electric cyan
            new Color(0.30f, 0.90f, 0.15f),        // 8 VENOM     — toxic green
            new Color(0.22f, 0.10f, 0.42f),        // 9 SHADOW    — dark indigo
            new Color(1.00f, 0.65f, 0.10f),        // 10 SOLAR    — solar orange
            new Color(0.55f, 0.70f, 1.00f),        // 11 STORM    — storm blue
            new Color(0.90f, 0.10f, 0.15f),        // 12 BLOODMOON — crimson
        };

        // ── tint accessors ───────────────────────────────────────────────────────

        /// <summary>Body tint for player units: TroopSkin (unified skin) takes priority, then SkinColor, then theme.</summary>
        public static Color GetTroopTint()
        {
            int ts = GameSettings.ActiveTroopSkin;
            if (ts > 0) return TroopTints[Mathf.Clamp(ts, 0, TroopTints.Length - 1)];
            int sc = GameSettings.ActiveSkinColor;
            if (sc > 0) return SkinColorTints[Mathf.Clamp(sc, 0, SkinColorTints.Length - 1)];
            return NeonTheme.Active.TroopTint;
        }

        public static Color GetTroopTint(int skinIndex)
        {
            if (skinIndex <= 0) return NeonTheme.Active.TroopTint;
            return TroopTints[Mathf.Clamp(skinIndex, 0, TroopTints.Length - 1)];
        }

        public static Color GetSkinColorTint(int colorIndex)
        {
            if (colorIndex <= 0) return NeonTheme.Active.TroopTint;
            return SkinColorTints[Mathf.Clamp(colorIndex, 0, SkinColorTints.Length - 1)];
        }

        /// <summary>Glow aura color: matches SkinColor when set, else theme accent.</summary>
        public static Color GetSkinGlowColor()
        {
            int sc = GameSettings.ActiveSkinColor;
            if (sc > 0) return SkinColorTints[Mathf.Clamp(sc, 0, SkinColorTints.Length - 1)];
            return NeonTheme.Active.Accent;
        }

        // ── active selections ────────────────────────────────────────────────────

        public static SkinColor ActiveSkinColor => (SkinColor)GameSettings.ActiveSkinColor;
        public static TroopSkin ActiveTroopSkin => (TroopSkin)GameSettings.ActiveTroopSkin;
        public static SkinGlow  ActiveSkinGlow  => (SkinGlow) GameSettings.ActiveSkinGlow;
        public static BoardSkin ActiveBoardSkin => (BoardSkin)GameSettings.ActiveBoardSkin;
        public static DeployFx ActiveDeployFx => (DeployFx)GameSettings.ActiveDeployFx;
        // categories are integer-indexed: 0 board, 1 trail, 2 frame, 3 troop skin,
        // 4 colour, 5 glow, 6 lane. Deploy effects take 7.
        public static bool IsUnlocked(DeployFx d) => (int)d == 0 || PlayerProgress.HasCosmetic(7, (int)d);
        public static bool TryUnlock(DeployFx d)
        {
            if (IsUnlocked(d)) return true;
            if (!PlayerProgress.SpendTokens(DeployFxCost[(int)d])) return false;
            PlayerProgress.UnlockCosmetic(7, (int)d);
            return true;
        }
        public static void SetActive(DeployFx d) { if (IsUnlocked(d)) GameSettings.ActiveDeployFx = (int)d; }

        // category 8 -- see the note on DeployFx for the index map
        public static VfxSkin ActiveVfxSkin => (VfxSkin)GameSettings.ActiveVfxSkin;
        public static bool IsUnlocked(VfxSkin v) => (int)v == 0 || PlayerProgress.HasCosmetic(8, (int)v);
        public static bool TryUnlock(VfxSkin v)
        {
            if (IsUnlocked(v)) return true;
            if (!PlayerProgress.SpendTokens(VfxSkinCost[(int)v])) return false;
            PlayerProgress.UnlockCosmetic(8, (int)v);
            return true;
        }
        public static void SetActive(VfxSkin v) { if (IsUnlocked(v)) GameSettings.ActiveVfxSkin = (int)v; }
        public static GemTrail  ActiveGemTrail  => (GemTrail) GameSettings.ActiveGemTrail;
        public static CardFrame ActiveCardFrame => (CardFrame)GameSettings.ActiveCardFrame;
        public static LaneScene ActiveLaneScene => (LaneScene)GameSettings.ActiveLaneScene;

        // ── unlock query ─────────────────────────────────────────────────────────

        public static bool IsUnlocked(SkinColor c) => (int)c == 0 || PlayerProgress.HasCosmetic(4, (int)c);
        public static bool IsUnlocked(TroopSkin t) => (int)t == 0 || PlayerProgress.HasCosmetic(3, (int)t);
        public static bool IsUnlocked(SkinGlow  g) => (int)g == 0 || PlayerProgress.HasCosmetic(5, (int)g);
        public static bool IsUnlocked(BoardSkin s) => (int)s == 0 || PlayerProgress.HasCosmetic(0, (int)s);
        public static bool IsUnlocked(GemTrail  t) => (int)t == 0 || PlayerProgress.HasCosmetic(1, (int)t);
        public static bool IsUnlocked(CardFrame f) => (int)f == 0 || PlayerProgress.HasCosmetic(2, (int)f);
        public static bool IsUnlocked(LaneScene s) => (int)s == 0 || PlayerProgress.HasCosmetic(6, (int)s);

        // ── purchase ─────────────────────────────────────────────────────────────

        public static bool TryUnlock(SkinColor c)
        {
            if (IsUnlocked(c)) return false;
            if (!PlayerProgress.SpendTokens(SkinColorCost[(int)c])) return false;
            PlayerProgress.UnlockCosmetic(4, (int)c);
            return true;
        }

        public static bool TryUnlock(TroopSkin t)
        {
            if (IsUnlocked(t)) return false;
            if (!PlayerProgress.SpendTokens(TroopSkinCost[(int)t])) return false;
            PlayerProgress.UnlockCosmetic(3, (int)t);
            return true;
        }

        public static bool TryUnlock(SkinGlow g)
        {
            if (IsUnlocked(g)) return false;
            if (!PlayerProgress.SpendTokens(SkinGlowCost[(int)g])) return false;
            PlayerProgress.UnlockCosmetic(5, (int)g);
            return true;
        }

        public static bool TryUnlock(BoardSkin s)
        {
            if (IsUnlocked(s)) return false;
            if (!PlayerProgress.SpendTokens(BoardSkinCost[(int)s])) return false;
            PlayerProgress.UnlockCosmetic(0, (int)s);
            return true;
        }

        public static bool TryUnlock(GemTrail t)
        {
            if (IsUnlocked(t)) return false;
            if (!PlayerProgress.SpendTokens(GemTrailCost[(int)t])) return false;
            PlayerProgress.UnlockCosmetic(1, (int)t);
            return true;
        }

        public static bool TryUnlock(CardFrame f)
        {
            if (IsUnlocked(f)) return false;
            if (!PlayerProgress.SpendTokens(CardFrameCost[(int)f])) return false;
            PlayerProgress.UnlockCosmetic(2, (int)f);
            return true;
        }

        public static bool TryUnlock(LaneScene s)
        {
            if (IsUnlocked(s)) return false;
            if (!PlayerProgress.SpendTokens(LaneSceneCost[(int)s])) return false;
            PlayerProgress.UnlockCosmetic(6, (int)s);
            return true;
        }

        // ── equip ────────────────────────────────────────────────────────────────

        public static void SetActive(SkinColor c) { if (IsUnlocked(c)) GameSettings.ActiveSkinColor = (int)c; }
        public static void SetActive(TroopSkin t) { if (IsUnlocked(t)) GameSettings.ActiveTroopSkin = (int)t; }
        public static void SetActive(SkinGlow  g) { if (IsUnlocked(g)) GameSettings.ActiveSkinGlow  = (int)g; }
        public static void SetActive(BoardSkin s) { if (IsUnlocked(s)) GameSettings.ActiveBoardSkin = (int)s; }
        public static void SetActive(GemTrail  t) { if (IsUnlocked(t)) GameSettings.ActiveGemTrail  = (int)t; }
        public static void SetActive(CardFrame f) { if (IsUnlocked(f)) GameSettings.ActiveCardFrame = (int)f; }
        public static void SetActive(LaneScene s) { if (IsUnlocked(s)) GameSettings.ActiveLaneScene = (int)s; }
    }
}
