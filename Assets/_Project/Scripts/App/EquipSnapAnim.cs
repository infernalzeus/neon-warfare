using UnityEngine;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Confirmation that a shop card took. Equipping used to change a colour and play a sound —
    /// nothing moved, so on a six-card grid it was easy to miss which card you actually hit.
    /// The card kicks up and settles while a ring pushes out from its centre: two cues, both
    /// motion, neither of which depends on noticing a colour change.
    /// </summary>
    public sealed class EquipSnapAnim : MonoBehaviour
    {
        public Color RingColor = new Color(0.25f, 0.82f, 0.55f);

        RectTransform _rt;
        RectTransform _ring;
        Image         _ringImg;
        float         _t = -1f;          // <0 = idle

        const float DUR   = 0.52f;
        const float KICK  = 0.06f;       // 6% overshoot
        const float RISE  = 0.30f;       // fraction of DUR spent going up

        void Awake() => _rt = GetComponent<RectTransform>();

        public void Play()
        {
            if (_rt == null) _rt = GetComponent<RectTransform>();
            if (_rt == null) return;
            _t = 0f;

            if (_ring == null)
            {
                var go = new GameObject("equipRing");
                go.transform.SetParent(_rt, false);
                _ring = go.AddComponent<RectTransform>();
                _ring.anchorMin = _ring.anchorMax = new Vector2(0.5f, 0.5f);
                _ring.pivot = new Vector2(0.5f, 0.5f);
                _ring.anchoredPosition = Vector2.zero;
                _ringImg = go.AddComponent<Image>();
                _ringImg.sprite = NeonArt.SoftDot();
                _ringImg.raycastTarget = false;
            }
            _ring.gameObject.SetActive(true);
        }

        void Update()
        {
            if (_t < 0f || _rt == null) return;
            _t += Time.unscaledDeltaTime;
            float k = _t / DUR;

            if (k >= 1f)
            {
                _rt.localScale = Vector3.one;
                if (_ring) _ring.gameObject.SetActive(false);
                _t = -1f;
                return;
            }

            // up fast, settle slow — an ease that reads as a physical snap rather than a pulse
            float kick = k < RISE
                ? Mathf.Sin(k / RISE * Mathf.PI * 0.5f)
                : 1f - Mathf.Pow((k - RISE) / (1f - RISE), 0.55f);
            _rt.localScale = Vector3.one * (1f + KICK * kick);

            if (_ring != null && _ringImg != null)
            {
                float w = Mathf.Max(40f, _rt.rect.width);
                _ring.sizeDelta = Vector2.one * (w * 0.25f + w * 1.15f * k);
                _ringImg.color = new Color(RingColor.r, RingColor.g, RingColor.b,
                                           Mathf.Max(0f, 0.55f * (1f - k * 1.25f)));
            }
        }
    }
}
