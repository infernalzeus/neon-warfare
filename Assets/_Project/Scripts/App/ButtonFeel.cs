using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Drop on any Button to get hover-brighten + press-shrink + release-punch feedback.
    /// Requires a Button component on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public sealed class ButtonFeel : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        Image _bg;
        Color _baseColor;
        bool  _pressed;
        bool  _ownColorTint; // true when button.transition == None (we own colors)

        void Start()
        {
            var btn = GetComponent<Button>();
            _ownColorTint = btn.transition == Selectable.Transition.None;
            _bg = GetComponent<Image>();
            if (_bg != null) _baseColor = _bg.color;
        }

        public void OnPointerEnter(PointerEventData _)
        {
            if (!GetComponent<Button>().interactable) return;
            Tween.Scale(transform, transform.localScale.x, 1.06f, 0.09f, Tween.Ease.EaseOut);
            if (_ownColorTint && _bg != null)
                Tween.Tint(_bg, _bg.color, Brighten(_baseColor, 0.14f), 0.09f);
        }

        public void OnPointerExit(PointerEventData _)
        {
            _pressed = false;
            Tween.Scale(transform, transform.localScale.x, 1.00f, 0.14f, Tween.Ease.EaseOut);
            if (_ownColorTint && _bg != null)
                Tween.Tint(_bg, _bg.color, _baseColor, 0.14f);
        }

        public void OnPointerDown(PointerEventData _)
        {
            if (!GetComponent<Button>().interactable) return;
            _pressed = true;
            Tween.Scale(transform, transform.localScale.x, 0.92f, 0.06f, Tween.Ease.EaseIn);
        }

        public void OnPointerUp(PointerEventData _)
        {
            if (!_pressed) return;
            _pressed = false;
            Tween.Punch(transform, 0.06f, 0.18f);
        }

        static Color Brighten(Color c, float amt)
            => new Color(Mathf.Clamp01(c.r + amt), Mathf.Clamp01(c.g + amt), Mathf.Clamp01(c.b + amt), c.a);
    }
}
