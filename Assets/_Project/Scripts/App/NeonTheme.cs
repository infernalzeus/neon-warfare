using UnityEngine;

namespace NW.App
{
    public static class NeonTheme
    {
        public readonly struct Theme
        {
            public readonly string Name;
            public readonly Color  BgDeep;           // camera clear / deepest background
            public readonly Color  BgPanel;          // panel / topbar / board backgrounds
            public readonly Color  BgCard;           // card / item surfaces
            public readonly Color  Accent;           // primary neon — CTAs, titles, glows
            public readonly Color  AccentSecondary;  // complementary warm/cool — button fills, supports
            public readonly Color  AccentDim;        // borders, inactive states (includes alpha)
            public readonly Color  TextBright;       // titles, important values
            public readonly Color  TextMid;          // labels, descriptions
            public readonly Color  TextDim;          // hints, inactive elements
            public readonly Color  AtmosphereColor;  // ambient particle tint (includes alpha)

            // Cosmetics
            public readonly string AestheticTag;
            public readonly Color  TroopTint;
            public readonly Color  EnemyTint;
            public readonly int    BoardSkin;
            public readonly int    GemStyle;

            public Theme(string name,
                         Color bgDeep,    Color bgPanel,   Color bgCard,
                         Color accent,    Color accentSec, Color accentDim,
                         Color textBright, Color textMid,  Color textDim,
                         Color atmosphere,
                         string aestheticTag,
                         Color troopTint,  Color enemyTint,
                         int boardSkin,    int gemStyle)
            {
                Name            = name;
                BgDeep          = bgDeep;
                BgPanel         = bgPanel;
                BgCard          = bgCard;
                Accent          = accent;
                AccentSecondary = accentSec;
                AccentDim       = accentDim;
                TextBright      = textBright;
                TextMid         = textMid;
                TextDim         = textDim;
                AtmosphereColor = atmosphere;
                AestheticTag    = aestheticTag;
                TroopTint       = troopTint;
                EnemyTint       = enemyTint;
                BoardSkin       = boardSkin;
                GemStyle        = gemStyle;
            }
        }

        static readonly Theme[] _all =
        {
            // 0 — CYBER BLUE  (#050D1C rich navy, not pure black)
            new Theme("CYBER BLUE",
                bgDeep:      new Color(0.020f, 0.051f, 0.110f),
                bgPanel:     new Color(0.043f, 0.094f, 0.157f),
                bgCard:      new Color(0.063f, 0.133f, 0.204f),
                accent:      new Color(0.000f, 0.784f, 1.000f),   // #00C8FF
                accentSec:   new Color(0.000f, 0.267f, 0.400f),   // #004466
                accentDim:   new Color(0.000f, 0.500f, 0.800f, 0.40f),
                textBright:  new Color(0.816f, 0.933f, 1.000f),   // #D0EEFF
                textMid:     new Color(0.416f, 0.667f, 0.733f),   // #6AAABB
                textDim:     new Color(0.312f, 0.540f, 0.653f),
                atmosphere:  new Color(0.000f, 0.600f, 1.000f, 0.08f),
                aestheticTag: "CYBER",
                troopTint:   new Color(0.55f, 0.95f, 1.00f),
                enemyTint:   new Color(1.00f, 0.40f, 0.20f),
                boardSkin:   0,
                gemStyle:    0),

            // 1 — SYNTHWAVE PURPLE  (#08040E deep void purple)
            new Theme("SYNTHWAVE PURPLE",
                bgDeep:      new Color(0.031f, 0.016f, 0.055f),
                bgPanel:     new Color(0.071f, 0.039f, 0.125f),
                bgCard:      new Color(0.110f, 0.063f, 0.196f),
                accent:      new Color(0.800f, 0.267f, 1.000f),   // #CC44FF
                accentSec:   new Color(0.400f, 0.078f, 0.667f),   // #6614AA
                accentDim:   new Color(0.550f, 0.100f, 0.750f, 0.40f),
                textBright:  new Color(0.933f, 0.878f, 1.000f),   // #EEE0FF
                textMid:     new Color(0.600f, 0.400f, 0.800f),   // #9966CC
                textDim:     new Color(0.627f, 0.380f, 0.855f),
                atmosphere:  new Color(0.700f, 0.200f, 1.000f, 0.08f),
                aestheticTag: "SYNTHWAVE",
                // Player was light purple against a hot-pink enemy: 0.65 channel separation, the
                // weakest pairing in the game. Synthwave already owns cyan, so use it.
                troopTint:   new Color(0.40f, 0.90f, 1.00f),
                enemyTint:   new Color(1.00f, 0.28f, 0.62f),
                boardSkin:   1,
                gemStyle:    1),

            // 2 — BIOPUNK GREEN  (#020804 near-void dark green)
            new Theme("BIOPUNK GREEN",
                bgDeep:      new Color(0.008f, 0.031f, 0.016f),
                bgPanel:     new Color(0.027f, 0.071f, 0.031f),
                bgCard:      new Color(0.047f, 0.118f, 0.063f),
                accent:      new Color(0.133f, 1.000f, 0.267f),   // #22FF44
                accentSec:   new Color(0.039f, 0.353f, 0.094f),   // #0A5A18
                accentDim:   new Color(0.050f, 0.650f, 0.250f, 0.40f),
                textBright:  new Color(0.800f, 1.000f, 0.867f),   // #CCFFDD
                textMid:     new Color(0.267f, 0.667f, 0.333f),   // #44AA55
                textDim:     new Color(0.211f, 0.567f, 0.275f),
                atmosphere:  new Color(0.100f, 1.000f, 0.300f, 0.07f),
                aestheticTag: "BIOPUNK",
                troopTint:   new Color(0.50f, 1.00f, 0.60f),
                enemyTint:   new Color(0.90f, 0.50f, 0.10f),
                boardSkin:   2,
                gemStyle:    2),

            // 3 — MEDIEVAL CRIMSON  (#0E0204 blood-dark)
            new Theme("MEDIEVAL CRIMSON",
                bgDeep:      new Color(0.055f, 0.008f, 0.016f),
                bgPanel:     new Color(0.110f, 0.024f, 0.031f),
                bgCard:      new Color(0.173f, 0.055f, 0.055f),
                accent:      new Color(1.000f, 0.200f, 0.200f),   // #FF3333
                accentSec:   new Color(0.667f, 0.400f, 0.000f),   // #AA6600 gold
                accentDim:   new Color(0.750f, 0.120f, 0.060f, 0.40f),
                textBright:  new Color(1.000f, 0.910f, 0.910f),   // #FFE8E8
                textMid:     new Color(0.800f, 0.467f, 0.400f),   // #CC7766
                textDim:     new Color(0.791f, 0.368f, 0.313f),
                atmosphere:  new Color(1.000f, 0.200f, 0.050f, 0.07f),
                aestheticTag: "MEDIEVAL",
                // Team readability: every other theme reads cool-player vs warm-enemy. This one
                // had salmon vs cream -- both warm and pale, so the two armies looked identical
                // on the blood-dark ground. Steel-blue crusaders vs crimson gives hue AND value
                // separation while staying medieval.
                troopTint:   new Color(0.62f, 0.78f, 0.95f),
                enemyTint:   new Color(1.00f, 0.42f, 0.24f),
                boardSkin:   3,
                gemStyle:    3),

            // 4 — INDUSTRIAL GHOST  (#060708 soot)
            // Darker and colder. The old values were steel-BLUE, which put it in the same family
            // as Cyber and Dawn; soot and iron should read as a true neutral grey so it sits
            // clearly apart from Solar's brass on one side and Dawn's pale linen on the other.
            new Theme("INDUSTRIAL GHOST",
                bgDeep:      new Color(0.024f, 0.027f, 0.031f),   // #060708
                bgPanel:     new Color(0.063f, 0.071f, 0.078f),   // #101214
                bgCard:      new Color(0.110f, 0.122f, 0.133f),   // #1C1F22
                accent:      new Color(0.576f, 0.635f, 0.686f),   // #939FAF -> neutral steel
                accentSec:   new Color(0.220f, 0.255f, 0.286f),   // #384149
                accentDim:   new Color(0.560f, 0.610f, 0.660f, 0.40f),
                textBright:  new Color(0.929f, 0.941f, 0.949f),   // #EDF0F2
                textMid:     new Color(0.612f, 0.659f, 0.694f),   // #9CA8B1
                textDim:     new Color(0.451f, 0.494f, 0.529f),
                atmosphere:  new Color(0.560f, 0.600f, 0.640f, 0.05f),
                aestheticTag: "INDUSTRIAL",
                troopTint:   new Color(0.80f, 0.88f, 1.00f),
                enemyTint:   new Color(0.70f, 0.40f, 0.25f),
                boardSkin:   4,
                gemStyle:    4),

            // 5 — SAKURA DUSK  (#0C0410 deep violet-pink)
            new Theme("SAKURA DUSK",
                bgDeep:      new Color(0.047f, 0.016f, 0.063f),
                bgPanel:     new Color(0.094f, 0.039f, 0.133f),
                bgCard:      new Color(0.141f, 0.063f, 0.188f),
                accent:      new Color(1.000f, 0.400f, 0.667f),   // #FF66AA
                accentSec:   new Color(0.533f, 0.133f, 0.290f),   // #88224A
                accentDim:   new Color(0.800f, 0.350f, 0.550f, 0.40f),
                textBright:  new Color(1.000f, 0.910f, 0.949f),   // #FFE8F2
                textMid:     new Color(0.733f, 0.533f, 0.667f),   // #BB88AA
                textDim:     new Color(0.684f, 0.414f, 0.594f),
                atmosphere:  new Color(1.000f, 0.400f, 0.650f, 0.07f),
                aestheticTag: "KAWAII",
                // Pale pink vs violet sat only 1.08 apart. Keep the soft pink player and push
                // the enemy to a deep warm crimson for hue and value separation.
                troopTint:   new Color(1.00f, 0.74f, 0.88f),
                enemyTint:   new Color(0.80f, 0.12f, 0.30f),
                boardSkin:   1,
                gemStyle:    1),

            // 6 — SOLAR FORGE  (#0C0800 molten dark amber)
            new Theme("SOLAR FORGE",
                bgDeep:      new Color(0.047f, 0.031f, 0.000f),
                bgPanel:     new Color(0.102f, 0.063f, 0.000f),
                bgCard:      new Color(0.157f, 0.094f, 0.000f),
                accent:      new Color(1.000f, 0.667f, 0.133f),   // #FFAA22
                accentSec:   new Color(0.533f, 0.267f, 0.000f),   // #884400
                accentDim:   new Color(0.800f, 0.600f, 0.100f, 0.40f),
                textBright:  new Color(1.000f, 0.949f, 0.800f),   // #FFF2CC
                textMid:     new Color(0.800f, 0.533f, 0.200f),   // #CC8833
                textDim:     new Color(0.688f, 0.448f, 0.144f),
                atmosphere:  new Color(1.000f, 0.600f, 0.100f, 0.08f),
                aestheticTag: "SOLAR",
                troopTint:   new Color(1.00f, 0.90f, 0.45f),
                enemyTint:   new Color(0.75f, 0.25f, 0.05f),
                boardSkin:   0,
                gemStyle:    3),

            // 7 — DAWN LIGHT  (#B9BEC8 pale linen — the ONLY light theme in the game)
            // Everything else is dark, and the art pipeline assumes it. Two consequences worth
            // watching on the battlefield: P3DEllipseGlow ADDS light, so lanterns, wards and rune
            // seams lose punch against a pale ground; and the near-black sprite outline becomes
            // the dominant mark, which sharpens silhouettes but flattens interior shading.
            // If the battlefield reads wrong, only these three background values need to go back
            // dark -- the text and accent values below are already tuned for either ground.
            new Theme("DAWN LIGHT",
                bgDeep:      new Color(0.725f, 0.745f, 0.784f),   // #B9BEC8
                bgPanel:     new Color(0.894f, 0.906f, 0.925f),   // #E4E7EC
                bgCard:      new Color(0.961f, 0.969f, 0.980f),   // #F5F7FA
                accent:      new Color(0.243f, 0.376f, 0.596f),   // #3E6098 — darkened to survive on white
                accentSec:   new Color(0.686f, 0.737f, 0.827f),   // #AFBCD3
                accentDim:   new Color(0.300f, 0.420f, 0.620f, 0.40f),
                textBright:  new Color(0.086f, 0.098f, 0.125f),   // #161920 — inverted
                textMid:     new Color(0.255f, 0.286f, 0.337f),   // #414956 -- 4.87 on the pale ground
                textDim:     new Color(0.435f, 0.463f, 0.510f),   // #6F7682
                atmosphere:  new Color(0.400f, 0.500f, 0.700f, 0.05f),
                aestheticTag: "DAWN",
                // Both tints darkened so the two armies still separate against a pale ground --
                // the old pale-blue player tint would have vanished into it.
                // First pass put these 1.14 apart -- below the 1.30 floor, so the two armies
                // would have read as one colour, the exact bug fixed for Medieval and Sakura.
                // Deep indigo against warm brick separates them 1.68 and both hold on pale ground.
                troopTint:   new Color(0.20f, 0.34f, 0.62f),
                enemyTint:   new Color(0.78f, 0.36f, 0.24f),
                boardSkin:   0,
                gemStyle:    0),
        };

        public static int    Count      => _all.Length;
        public static Theme  Active     => _all[Mathf.Clamp(GameSettings.ThemeIndex, 0, _all.Length - 1)];
        public static string ActiveName => Active.Name;

        public static Color  Accent(int i) => _all[Mathf.Clamp(i, 0, _all.Length - 1)].Accent;

        public static void   Next()        => GameSettings.ThemeIndex = (GameSettings.ThemeIndex + 1) % Count;
        public static string NameOf(int i) => _all[Mathf.Clamp(i, 0, _all.Length - 1)].Name;
        public static Theme  Get(int i)    => _all[Mathf.Clamp(i, 0, _all.Length - 1)];
    }
}
