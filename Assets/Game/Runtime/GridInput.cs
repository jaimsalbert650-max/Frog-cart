using UnityEngine;
using UnityEngine.EventSystems;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Ввод по сетке. Мышь и палец обрабатываются одним кодом: IDragHandler покрывает оба
    /// и сам держит захват указателя, поэтому протяжка не теряется при выходе за границу.
    ///
    /// Размер ячейки берётся у GridView, а не из констант: сетка бывает и 14x16, и 35x35.
    /// </summary>
    public sealed class GridInput : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        GameController _controller;
        GridView _grid;
        RectTransform _gridRect;
        float _lastChainTime;

        public void Setup(GameController controller, GridView grid, RectTransform gridRect)
        {
            _controller = controller;
            _grid = grid;
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

            c = Mathf.FloorToInt(lp.x / _grid.CellW);
            r = Mathf.FloorToInt(-lp.y / _grid.CellH);

            return r >= 0 && c >= 0 && r < _grid.Rows && c < _grid.Cols;
        }
    }
}
