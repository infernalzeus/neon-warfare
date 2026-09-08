using UnityEngine;

namespace NW.App
{
    /// <summary>
    /// Review tool: scroll-wheel zoom toward the cursor, anywhere in the game.
    /// Middle-mouse drag pans while zoomed; Home (or zooming back out) resets.
    ///
    /// Works by inserting a [ZoomRoot] under the canvas and adopting every canvas
    /// child into it each frame, so all screens — battlefield, menus, armory —
    /// zoom uniformly. UGUI raycasting respects the transform, so the game stays
    /// fully interactive while zoomed.
    /// </summary>
    public sealed class ZoomInspector : MonoBehaviour
    {
        const float MIN_ZOOM = 1f, MAX_ZOOM = 4f, STEP = 1.15f;

        RectTransform _canvasRt, _root;
        float   _zoom = 1f;
        Vector2 _offset;
        Vector3 _lastMouse;

        void Awake()
        {
            _canvasRt = (RectTransform)transform;
            var go = new GameObject("[ZoomRoot]");
            go.transform.SetParent(transform, false);
            _root = go.AddComponent<RectTransform>();
            _root.anchorMin = Vector2.zero; _root.anchorMax = Vector2.one;
            _root.offsetMin = _root.offsetMax = Vector2.zero;
        }

        void LateUpdate()
        {
            AdoptChildren();
            HandleInput();
            Apply();
        }

        // Any GO added directly to the canvas gets folded into the zoom container.
        // Ascending order keeps the original sibling draw order intact.
        void AdoptChildren()
        {
            while (transform.childCount > 1)
            {
                int idx = transform.GetChild(0) == _root.transform ? 1 : 0;
                var c = transform.GetChild(idx);
                c.SetParent(_root, false); // root rect == canvas rect, layout unchanged
            }
        }

        void HandleInput()
        {
            // Scroll: zoom toward the cursor
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                float target = Mathf.Clamp(_zoom * (scroll > 0f ? STEP : 1f / STEP), MIN_ZOOM, MAX_ZOOM);
                if (!Mathf.Approximately(target, _zoom))
                {
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRt, Input.mousePosition, null, out Vector2 cursor);
                    // Keep the canvas point under the cursor fixed through the zoom change
                    _offset = cursor - (cursor - _offset) * (target / _zoom);
                    _zoom   = target;
                }
            }

            // Middle-mouse drag: pan while zoomed
            if (Input.GetMouseButton(2) && _zoom > 1.001f)
            {
                Vector2 d = Input.mousePosition - _lastMouse;
                _offset += d * (_canvasRt.rect.height / Screen.height); // screen px → canvas units
            }
            _lastMouse = Input.mousePosition;

            // Home resets instantly
            if (Input.GetKeyDown(KeyCode.Home)) { _zoom = 1f; _offset = Vector2.zero; }
        }

        void Apply()
        {
            if (_zoom <= 1.001f) _offset = Vector2.zero;
            // Never pan past the content edge
            Vector2 max = _canvasRt.rect.size * 0.5f * (_zoom - 1f);
            _offset = new Vector2(Mathf.Clamp(_offset.x, -max.x, max.x),
                                  Mathf.Clamp(_offset.y, -max.y, max.y));
            _root.localScale       = new Vector3(_zoom, _zoom, 1f);
            _root.anchoredPosition = _offset;
        }
    }
}
