using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Keeps a CanvasScaler correct for BOTH portrait (phone) and landscape (desktop) from a
    /// single responsive build. Watches the viewport aspect and swaps the reference resolution
    /// + match axis so UI never stretches:
    ///   • portrait  → reference 1080×1920, match WIDTH  (fill the narrow axis)
    ///   • landscape → reference 1920×1080, match HEIGHT (fill the short axis)
    ///
    /// Attach next to the Canvas's CanvasScaler. Presentation-agnostic and additive — survives
    /// the doc 09 P0 view rebuild. See LLM Wiki "2026-07-25-neon-warfare-portrait-redesign".
    /// </summary>
    [RequireComponent(typeof(CanvasScaler))]
    [DisallowMultipleComponent]
    public sealed class ResponsiveCanvas : MonoBehaviour
    {
        public Vector2 portraitReference  = new Vector2(1080f, 1920f);
        public Vector2 landscapeReference = new Vector2(1920f, 1080f);

        CanvasScaler _scaler;
        bool _lastWasPortrait;
        bool _initialised;

        void Awake()
        {
            _scaler = GetComponent<CanvasScaler>();
            _scaler.uiScaleMode     = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            _scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            Apply(IsPortrait());
        }

        void Update()
        {
            bool portrait = IsPortrait();
            if (!_initialised || portrait != _lastWasPortrait)
                Apply(portrait);
        }

        static bool IsPortrait() => Screen.height >= Screen.width;

        void Apply(bool portrait)
        {
            _initialised     = true;
            _lastWasPortrait = portrait;

            if (portrait)
            {
                _scaler.referenceResolution = portraitReference;
                _scaler.matchWidthOrHeight  = 0f; // match width — the constrained axis in portrait
            }
            else
            {
                _scaler.referenceResolution = landscapeReference;
                _scaler.matchWidthOrHeight  = 1f; // match height — the constrained axis in landscape
            }
        }
    }
}
