using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// The armory currency, animated. Cycles eight pre-baked shine angles so the coin catches
    /// light as it turns, breathes a little, and flashes when the balance goes up — so a
    /// conversion is something you SEE happen rather than a number that quietly changes.
    /// </summary>
    public sealed class TokenIconAnim : MonoBehaviour
    {
        RawImage      _img;
        RectTransform _rt;
        float         _t;
        float         _flash;          // >0 while celebrating a gain
        int           _lastPhase = -1;

        const int PHASES = 8;

        void Awake()
        {
            _rt  = GetComponent<RectTransform>();
            _img = GetComponent<RawImage>();
            if (_img == null) _img = gameObject.AddComponent<RawImage>();
            _img.raycastTarget = false;
            _img.texture = NeonArt.TokenIcon(0);
        }

        /// <summary>Call when tokens are gained: the coin flares and kicks once.</summary>
        public void Celebrate() => _flash = 1f;

        void Update()
        {
            if (_img == null || _rt == null) return;

            _t += Time.unscaledDeltaTime;

            // shine angle
            int phase = Mathf.FloorToInt(_t * 6f) % PHASES;
            if (phase != _lastPhase)
            {
                _lastPhase = phase;
                _img.texture = NeonArt.TokenIcon(phase);
            }

            // idle breath, plus a sharper kick while flashing
            float breathe = 1f + Mathf.Sin(_t * 2.4f) * 0.04f;
            if (_flash > 0f)
            {
                _flash = Mathf.Max(0f, _flash - Time.unscaledDeltaTime * 2.2f);
                float k = _flash * _flash;
                _rt.localScale = Vector3.one * (breathe + k * 0.55f);
                _rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_flash * 18f) * 14f * k);
                _img.color = Color.Lerp(Color.white, new Color(1f, 1f, 0.75f), k);
            }
            else
            {
                _rt.localScale = Vector3.one * breathe;
                _rt.localRotation = Quaternion.identity;
                _img.color = Color.white;
            }
        }
    }
}
