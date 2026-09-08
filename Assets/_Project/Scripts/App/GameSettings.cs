using UnityEngine;

namespace NW.App
{
    /// <summary>Persistent player preferences saved to PlayerPrefs.</summary>
    public static class GameSettings
    {
        public static bool CameraShake   = true;
        public static int  SelectedLevel = 1;

        // ── theme ───────────────────────────────────────────────────────────────
        // 0=Cyber Blue  1=Synthwave Purple  2=Biopunk Green  3=Medieval Crimson
        // 4=Industrial Ghost  5=Sakura Dusk  6=Solar Forge  7=Dawn Light
        // (was "3=BloodRed 4=Ghost" -- those themes were renamed; NeonTheme._all is the
        //  authority and ThemeLocale keys its art + name tables off the same order)

        public static int ThemeIndex
        {
            get => PlayerPrefs.GetInt("gs_theme", 0);
            set { PlayerPrefs.SetInt("gs_theme", Mathf.Clamp(value, 0, NeonTheme.Count - 1)); PlayerPrefs.Save(); }
        }

        // ── cosmetics ───────────────────────────────────────────────────────────

        public static int ActiveVfxSkin
        {
            get => PlayerPrefs.GetInt("gs_vfxskin", 0);
            set { PlayerPrefs.SetInt("gs_vfxskin", value); PlayerPrefs.Save(); }
        }

        public static int ActiveDeployFx
        {
            get => PlayerPrefs.GetInt("gs_deployfx", 0);
            set { PlayerPrefs.SetInt("gs_deployfx", value); PlayerPrefs.Save(); }
        }

        public static int ActiveBoardSkin
        {
            get => PlayerPrefs.GetInt("gs_boardskin", 0);
            set { PlayerPrefs.SetInt("gs_boardskin", value); PlayerPrefs.Save(); }
        }

        public static int ActiveLaneScene
        {
            get => PlayerPrefs.GetInt("gs_lanescene", 0);
            set { PlayerPrefs.SetInt("gs_lanescene", value); PlayerPrefs.Save(); }
        }

        public static int ActiveGemTrail
        {
            get => PlayerPrefs.GetInt("gs_gemtrail", 0);
            set { PlayerPrefs.SetInt("gs_gemtrail", value); PlayerPrefs.Save(); }
        }

        public static int ActiveCardFrame
        {
            get => PlayerPrefs.GetInt("gs_cardframe", 0);
            set { PlayerPrefs.SetInt("gs_cardframe", value); PlayerPrefs.Save(); }
        }

        public static int ActiveTroopSkin
        {
            get => PlayerPrefs.GetInt("gs_troopskin", 0);
            set { PlayerPrefs.SetInt("gs_troopskin", value); PlayerPrefs.Save(); }
        }

        public static int ActiveSkinColor
        {
            get => PlayerPrefs.GetInt("gs_skincolor", 0);
            set { PlayerPrefs.SetInt("gs_skincolor", value); PlayerPrefs.Save(); }
        }

        public static int ActiveSkinGlow
        {
            get => PlayerPrefs.GetInt("gs_skinglow", 0);
            set { PlayerPrefs.SetInt("gs_skinglow", value); PlayerPrefs.Save(); }
        }

        // ── board background opacity ─────────────────────────────────────────
        // Alpha of the background image itself (0.10–0.40).
        // 0.10 = very dim; 0.40 = moderately visible; board tiles always 100% opaque.
        public static float BoardBgOpacity
        {
            get => PlayerPrefs.GetFloat("gs_board_bg_opacity", 0.25f);
            set { PlayerPrefs.SetFloat("gs_board_bg_opacity", Mathf.Clamp01(value)); PlayerPrefs.Save(); }
        }

        // ── battle speed ─────────────────────────────────────────────────────

        public static float BattleSpeed
        {
            get => PlayerPrefs.GetFloat("gs_battlespeed", 1f);
            set { PlayerPrefs.SetFloat("gs_battlespeed", value); PlayerPrefs.Save(); }
        }

        // ── competitive / VS mode ─────────────────────────────────────────────

        public static bool CompetitiveMode
        {
            get => PlayerPrefs.GetInt("gs_comp_mode", 0) == 1;
            set { PlayerPrefs.SetInt("gs_comp_mode", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Last successfully connected peer IP — pre-fills the join field.</summary>
        public static string LastPeerIP
        {
            get => PlayerPrefs.GetString("gs_peer_ip", "127.0.0.1");
            set { PlayerPrefs.SetString("gs_peer_ip", value); PlayerPrefs.Save(); }
        }
    }
}
