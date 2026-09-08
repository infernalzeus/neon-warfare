using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Centralised UI sizing constants. All font sizes, icon dimensions, and padding values
    /// live here so that the whole UI can be tuned from one file.
    /// </summary>
    public static class UIScale
    {
        // ── Typography ────────────────────────────────────────────────────────
        // Orientation-aware: portrait (phones) scales fonts up for readability at arm's length;
        // landscape (desktop) keeps the original tuned sizes. Callers use UIScale.FontBody
        // exactly as before — these are get-only properties, not consts. Base = landscape size.
        // Smaller styles get the largest boost (they were the least legible on a phone).
        static bool Portrait => Screen.height > Screen.width;
        static int  F(int baseSize, float portraitMul)
            => Portrait ? Mathf.RoundToInt(baseSize * portraitMul) : baseSize;

        // Portrait matches the WIDTH of a 1080-unit reference, so on a ~400pt phone one canvas
        // unit is 0.37pt. Measured against that, the old multipliers put body text at 8.5pt,
        // small at 7.4 and tiny at 6.3 -- all three under the 11pt platform floor, and body
        // text at barely half the 15-17pt it should be. That is why the smaller styles were
        // always hard to read.
        //
        // The previous note said higher multipliers truncated fixed-width labels, and it was
        // right: the fix is not a smaller font, it is letting text SHRINK TO FIT rather than
        // overflow. TextAutoFit does that globally, which is what makes these sizes safe.
        //
        //                                        portrait   pt on a 400pt phone
        public static int FontTitle => F(52, 1.50f);   //  78     28.9
        public static int FontH1    => F(28, 2.04f);   //  57     21.1
        public static int FontH2    => F(22, 2.09f);   //  46     17.0
        public static int FontBody  => F(16, 2.50f);   //  40     14.8
        public static int FontSmall => F(14, 2.50f);   //  35     13.0
        public static int FontTiny  => F(12, 2.50f);   //  30     11.1  <- the floor

        // ── Icon sizes — use sizeDelta, never anchor percentages for icons ───
        public const float IconGemBoard   = 52f;   // Gem icon on match board cells
        public const float IconGemHUD     = 22f;   // Gem icon in sidebar / cost row
        public const float IconUnitCard   = 48f;   // Unit icon in deploy card strip
        public const float IconUnitDetail = 40f;   // Unit icon in level-select detail panel

        // ── Padding & spacing ─────────────────────────────────────────────────
        public const float PadOuter   = 20f;   // Distance from screen edges
        public const float PadSection = 16f;   // Between major UI sections
        public const float PadInner   = 10f;   // Padding inside panels / cards
        public const float PadItem    =  8f;   // Gap between list items

        // ── Card dimensions ───────────────────────────────────────────────────
        public const float CardW   = 100f;   // Deploy card width (was ~13% canvas = too variable)
        public const float CardH   = 120f;   // Deploy card height

        // ── Level grid ────────────────────────────────────────────────────────
        public const float LvBtnW  = 90f;    // Level select button width  (was 84)
        public const float LvBtnH  = 58f;    // Level select button height (was 50)
        public const float LvBtnGap = 10f;   // Gap between level buttons  (was 8)
    }
}
