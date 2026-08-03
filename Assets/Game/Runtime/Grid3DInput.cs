using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Ввод в объёмной сцене. Коллайдеров на блоках нет и не нужно: доска остаётся
    /// плоской сеткой, поэтому луч из камеры пересекается с горизонтальной плоскостью
    /// доски, а клетка считается арифметикой — ровно как в плоской версии.
    ///
    /// Это заодно снимает 1225 коллайдеров со сцены и всю возню с их обновлением,
    /// когда блок съеден.
    /// </summary>
    public sealed class Grid3DInput : MonoBehaviour
    {
        GameController _controller;
        Grid3DView _grid;
        Camera _camera;

        float _lastChainTime;
        bool _dragging;

        public void Setup(GameController controller, Grid3DView grid, Camera camera)
        {
            _controller = controller;
            _grid = grid;
            _camera = camera;
        }

        void Update()
        {
            if (Input.GetMouseButtonDown(0))
            {
                _dragging = true;
                TryEat(Input.mousePosition);
                _lastChainTime = Time.unscaledTime;
                return;
            }

            if (Input.GetMouseButtonUp(0))
            {
                _dragging = false;
                _lastChainTime = 0f;
                return;
            }

            if (!_dragging) return;
            if (Time.unscaledTime - _lastChainTime < _controller.ChainDelay) return;

            if (TryEat(Input.mousePosition)) _lastChainTime = Time.unscaledTime;
        }

        bool TryEat(Vector3 screenPosition)
        {
            if (!TryHit(screenPosition, out int r, out int c)) return false;

            _controller.Eat(r, c);
            return true;
        }

        /// <summary>Луч из камеры в плоскость доски, затем деление на размер клетки.</summary>
        bool TryHit(Vector3 screenPosition, out int r, out int c)
        {
            r = -1;
            c = -1;

            var ray = _camera.ScreenPointToRay(screenPosition);
            var board = new Plane(Vector3.up, Vector3.zero);

            if (!board.Raycast(ray, out float distance)) return false;

            Vector3 hit = ray.GetPoint(distance);

            // Обратное преобразование Space3D: world (x, _, z) → spec (x, -z).
            float specX = hit.x / Space3D.Scale;
            float specY = -hit.z / Space3D.Scale;

            c = Mathf.FloorToInt((specX - _grid.OriginX) / _grid.CellW);
            r = Mathf.FloorToInt((specY - _grid.OriginY) / _grid.CellH);

            return r >= 0 && c >= 0 && r < _grid.Rows && c < _grid.Cols;
        }
    }
}
