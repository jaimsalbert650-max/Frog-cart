using UnityEngine;
using UnityEngine.EventSystems;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Ввод по сетке. Мышь и палец обрабатываются одним кодом: IDragHandler покрывает оба
    /// и сам держит захват указателя, поэтому протяжка не теряется при выходе за границу.
    /// </summary>
    public sealed class GridInput : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        GameController _controller;
        RectTransform _gridRect;
        float _lastChainTime;

        public void Setup(GameController controller, RectTransform gridRect)
        {
            _controller = controller;
            _gridRect = gridRect;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (!TryHit(e, out int r, out int c)) return;

            _controller.Eat(r, c);
            _lastChainTime = Time.unscaledTime;
        }

        public void OnDrag(PointerEventData e)
        {
            if (Time.unscaledTime - _lastChainTime < _controller.ChainDelay) return;
            if (!TryHit(e, out int r, out int c)) return;

            _controller.Eat(r, c);
            _lastChainTime = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData e) => _lastChainTime = 0f;

        bool TryHit(PointerEventData e, out int r, out int c)
        {
            r = -1;
            c = -1;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    _gridRect, e.position, e.pressEventCamera, out var lp))
                return false;

            c = Mathf.FloorToInt(lp.x / GridView.CW);
            r = Mathf.FloorToInt(-lp.y / GridView.CH);

            return r >= 0 && c >= 0 && r < GridView.Rows && c < GridView.Cols;
        }
    }
}
