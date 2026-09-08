using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Global text-overflow protection.
    ///
    /// Raising the font scale to clear the 11pt platform floor is the right call — body text at
    /// 8.5pt was never readable — but on its own it would overflow every fixed-width label in
    /// the game. Hence the old comment in UIScale warning that higher multipliers truncated the
    /// gem abbreviations, SPEED and the hints.
    ///
    /// The answer is not a smaller font. It is letting a label SHRINK TO FIT its own box, so the
    /// size we ask for is a maximum rather than a demand. Every label then renders as large as
    /// its space allows and never spills onto its neighbours.
    ///
    /// Sits on the canvas root and sweeps every Text beneath it. Re-runnable, because these
    /// screens rebuild their UI at runtime.
    /// </summary>
    public sealed class TextAutoFit : MonoBehaviour
    {
        /// <summary>Never shrink below this fraction of the requested size — past that the
        /// label is better clipped than rendered at a size nobody can read.</summary>
        public float MinFraction = 0.62f;

        /// <summary>Absolute floor in canvas units. 30 units is ~11pt on a 400pt phone, which
        /// is the platform minimum; below it we would be undoing the whole point of the raise.</summary>
        public int MinUnits = 24;

        float _next;

        void OnEnable() => Apply();

        void Update()
        {
            // These screens tear down and rebuild their labels (SelectTab, RefreshPanel, and so
            // on), so a one-shot pass at startup would only cover the first build.
            if (Time.unscaledTime < _next) return;
            _next = Time.unscaledTime + 0.5f;
            Apply();
        }

        public void Apply()
        {
            var texts = GetComponentsInChildren<Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t == null || t.resizeTextForBestFit) continue;   // already handled explicitly

                var rt = t.rectTransform;
                if (rt == null) continue;
                float w = rt.rect.width, h = rt.rect.height;
                if (w < 4f || h < 4f) continue;                      // layout not resolved yet

                int max = t.fontSize;
                if (max <= 0) continue;

                // A single-line label in a short box is the overflow risk. Give it best-fit so
                // it scales down into the space instead of running past the edge.
                t.resizeTextForBestFit = true;
                t.resizeTextMaxSize    = max;
                t.resizeTextMinSize    = Mathf.Max(MinUnits, Mathf.RoundToInt(max * MinFraction));

                // Wrapping only helps where there is vertical room for a second line; forcing
                // it on a single-line-height box just hides the overflow instead of fixing it.
                if (h >= max * 1.9f && t.horizontalOverflow == HorizontalWrapMode.Overflow)
                    t.horizontalOverflow = HorizontalWrapMode.Wrap;
            }
        }
    }
}
