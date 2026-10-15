using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Drives a RawImage through one of the sprite-pack's 24-frame sheets via uvRect
    /// grid-stepping -- no Sprite asset, no Animator, matching the one existing flipbook
    /// precedent in this project (the Zeus-intro sheets) instead of introducing a new
    /// asset pipeline. See NeonArt.UsesSpriteSheet/SpriteSheetTexture/SpriteSheetTiming.
    /// </summary>
    public static class SpriteSheetView
    {
        /// <summary>Sets img's texture (if the motion changed) and uvRect (every call) for
        /// running clock time t. Returns the frame index actually shown, in case a caller
        /// needs to know when an attack's last frame has been reached. <paramref name="mirror"/>
        /// flips the cell horizontally for enemy units, same convention as the body/limb
        /// uvRect mirroring already used everywhere else in BattlefieldView.</summary>
        public static int SetFrame(RawImage img, string id, string motion, float t, bool mirror)
        {
            NeonArt.SpriteSheetTiming(motion, out float duration, out bool loop);
            float u = t / duration;
            float frac = loop ? Mathf.Repeat(u, 1f) : Mathf.Clamp01(u);
            int frame = Mathf.Clamp((int)(frac * NeonArt.SpriteSheetFrames), 0, NeonArt.SpriteSheetFrames - 1);

            img.texture = NeonArt.SpriteSheetTexture(id, motion);

            int col = frame % NeonArt.SpriteSheetCols;
            int row = frame / NeonArt.SpriteSheetCols;
            float cellW = 1f / NeonArt.SpriteSheetCols;
            float cellH = 1f / NeonArt.SpriteSheetRows;
            // PNG rows run top-to-bottom; uvRect.y is the rect's BOTTOM edge in UV space
            // (v=0 bottom, v=1 top), so row 0 (top of the sheet) sits at the top of the UV
            // range, not the bottom -- flip the row index.
            int vRow = NeonArt.SpriteSheetRows - 1 - row;
            float x = col * cellW;
            float w = cellW;
            if (mirror) { x += cellW; w = -cellW; }   // same negative-width mirror as v.Body.uvRect
            img.uvRect = new Rect(x, vRow * cellH, w, cellH);
            return frame;
        }

        /// <summary>True once an attack sheet has reached and held its last frame -- callers
        /// driving a windup/strike sequence can use this instead of re-deriving the timing.</summary>
        public static bool AttackFinished(string motion, float t)
        {
            if (motion != "attack") return false;
            NeonArt.SpriteSheetTiming(motion, out float duration, out _);
            return t >= duration;
        }
    }
}
