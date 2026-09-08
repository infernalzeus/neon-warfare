using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace NW.App
{
    /// <summary>
    /// Attached to each deploy card. Handles pointer-drag onto the battlefield
    /// to deploy a troop without keyboard selection.
    ///
    /// Flow:
    ///   PointerDown → select card (same as button tap).
    ///   Drag        → show ghost icon under cursor; pass pos to BattlefieldView for lane highlight.
    ///   PointerUp   → if over a lane, deploy; otherwise cancel selection.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class CardDragDeploy : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        int _cardIdx;
        System.Action<int>          _onCardClicked;
        System.Action<int, Vector2> _onDragUpdate;
        System.Action<int>          _onLaneDeploy;

        bool      _dragging;
        RawImage  _ghost;
        Canvas    _rootCanvas;

        public void Init(int cardIdx,
            System.Action<int>          onCardClicked,
            System.Action<int, Vector2> onDragUpdate,
            System.Action<int>          onLaneDeploy)
        {
            _cardIdx       = cardIdx;
            _onCardClicked = onCardClicked;
            _onDragUpdate  = onDragUpdate;
            _onLaneDeploy  = onLaneDeploy;
        }

        // ── pointer events ───────────────────────────────────────────────────────

        public void OnPointerDown(PointerEventData e)
        {
            _onCardClicked?.Invoke(_cardIdx);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!_dragging)
            {
                if (Vector2.Distance(e.pressPosition, e.position) < 10f) return;
                _dragging = true;
                SpawnGhost();
            }

            if (_ghost != null) MoveGhostTo(e.position);
            _onDragUpdate?.Invoke(_cardIdx, e.position);
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (!_dragging)
            {
                DestroyGhost();
                return;
            }

            var bf = FindAnyObjectByType<BattlefieldView>();
            if (bf != null)
            {
                int lane = bf.LaneAtScreenPos(e.position);
                if (lane >= 0)
                {
                    _onLaneDeploy?.Invoke(lane);
                }
                else
                {
                    // Cancelled — clear selection
                    FindAnyObjectByType<HUDView>()?.SetSelectedCard(-1);
                    bf.SetLaneTargeting(false, false);
                }
            }

            _dragging = false;
            DestroyGhost();
        }

        // ── ghost icon ───────────────────────────────────────────────────────────

        void SpawnGhost()
        {
            _rootCanvas = GetComponentInParent<Canvas>();
            while (_rootCanvas != null && _rootCanvas.transform.parent != null
                   && _rootCanvas.transform.parent.GetComponent<Canvas>() != null)
                _rootCanvas = _rootCanvas.transform.parent.GetComponent<Canvas>();

            if (_rootCanvas == null) return;

            var sourceIcon = GetComponentInChildren<RawImage>();

            var go = new GameObject("DragGhost");
            go.transform.SetParent(_rootCanvas.transform, false);
            var rt = go.AddComponent<RectTransform>();
            rt.sizeDelta = new Vector2(56, 56);
            rt.pivot = new Vector2(0.5f, 0.5f);
            _ghost = go.AddComponent<RawImage>();
            _ghost.texture       = sourceIcon != null ? sourceIcon.texture : null;
            _ghost.color         = new Color(1f, 1f, 1f, 0.72f);
            _ghost.raycastTarget = false;

            var brd = new GameObject("brd"); brd.transform.SetParent(go.transform, false);
            var brdRt = brd.AddComponent<RectTransform>();
            brdRt.anchorMin = Vector2.zero; brdRt.anchorMax = Vector2.one;
            brdRt.offsetMin = new Vector2(-3, -3); brdRt.offsetMax = new Vector2(3, 3);
            brd.AddComponent<Image>().color = new Color(0f, 0.85f, 1f, 0.6f);
            brd.transform.SetAsFirstSibling();
        }

        void MoveGhostTo(Vector2 screenPos)
        {
            if (_ghost == null || _rootCanvas == null) return;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                _rootCanvas.GetComponent<RectTransform>(),
                screenPos, _rootCanvas.worldCamera,
                out Vector2 local);
            _ghost.GetComponent<RectTransform>().anchoredPosition = local;
        }

        void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost.gameObject);
            _ghost = null;
        }
    }
}
