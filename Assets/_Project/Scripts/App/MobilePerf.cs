using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Mobile performance baseline. Runs automatically at startup (before any scene loads),
    /// so it applies however the game is entered. Requests 60 fps — mobile platforms default
    /// to 30 fps, which makes tweens and unit animation look choppy / "blocky". Part of the
    /// phone/portrait port (Phase 4).
    /// </summary>
    public static class MobilePerf
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            QualitySettings.vSyncCount  = 0;   // let targetFrameRate govern, not the display's vSync
            Application.targetFrameRate = 60;  // mobile defaults to 30 → animation looks choppy
        }
    }
}
