using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Fits a RectTransform to the device safe area (notch / punch-hole / home indicator).
    /// Attach to a full-screen child of the Canvas and parent your portrait UI under it.
    /// Re-applies on orientation / resolution / safe-area change, so it is correct in the
    /// Device Simulator and on hardware. No-op effect on desktop (safe area == full screen).
    ///
    /// Part of the portrait/phone port — see the LLM Wiki page
    /// "2026-07-25-neon-warfare-portrait-redesign". Additive, presentation-agnostic:
    /// survives the doc 09 P0 view-layer rebuild.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public sealed class SafeArea : MonoBehaviour
    {
        [Tooltip("Ignore the top inset (e.g. if a full-bleed background should reach the notch).")]
        public bool ignoreTop;
        [Tooltip("Ignore the bottom inset (home indicator).")]
        public bool ignoreBottom;
        [Tooltip("Ignore the left inset.")]
        public bool ignoreLeft;
        [Tooltip("Ignore the right inset.")]
        public bool ignoreRight;

        RectTransform _rt;
        Rect _lastSafe = new Rect(0, 0, 0, 0);
        Vector2Int _lastRes = new Vector2Int(0, 0);
        ScreenOrientation _lastOrient;

        void Awake()
        {
            _rt = GetComponent<RectTransform>();
            Apply();
        }

        void Update()
        {
            // Cheap guards: only re-layout when the environment actually changed.
            if (Screen.safeArea != _lastSafe ||
                Screen.width != _lastRes.x || Screen.height != _lastRes.y ||
                Screen.orientation != _lastOrient)
            {
                Apply();
            }
        }

        void Apply()
        {
            _lastSafe   = Screen.safeArea;
            _lastRes    = new Vector2Int(Screen.width, Screen.height);
            _lastOrient = Screen.orientation;

            if (Screen.width <= 0 || Screen.height <= 0) return;

            Rect safe = Screen.safeArea;

            float xMin = ignoreLeft   ? 0f            : safe.xMin;
            float xMax = ignoreRight  ? Screen.width  : safe.xMax;
            float yMin = ignoreBottom ? 0f            : safe.yMin;
            float yMax = ignoreTop    ? Screen.height : safe.yMax;

            Vector2 anchorMin = new Vector2(xMin / Screen.width, yMin / Screen.height);
            Vector2 anchorMax = new Vector2(xMax / Screen.width, yMax / Screen.height);

            _rt.anchorMin = anchorMin;
            _rt.anchorMax = anchorMax;
            _rt.offsetMin = Vector2.zero;
            _rt.offsetMax = Vector2.zero;
        }
    }
}
