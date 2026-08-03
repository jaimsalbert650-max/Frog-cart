using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Ввод объёмной сцены: игрок тапает по вагонетке в очереди, и она выезжает
    /// на свободное место контура. По блокам тапать нельзя — работающая вагонетка
    /// выбирает цель сама.
    ///
    /// Коллайдеров нет, как и на доске: луч из камеры пересекается с горизонтальной
    /// плоскостью, а слот очереди находится по расстоянию до его точки. Так же
    /// дёшево, и ничего не надо обновлять, когда очередь сдвигается.
    /// </summary>
    public sealed class Queue3DInput : MonoBehaviour
    {
        /// <summary>
        /// Радиус попадания в spec-единицах — меньше половины шага между слотами
        /// (42), иначе один тап попадал бы сразу в две вагонетки и запускал не ту.
        /// </summary>
        const float HitRadius = 20f;

        GameController _controller;
        Queue3DView _queue;
        Camera _camera;

        public void Setup(GameController controller, Queue3DView queue, Camera camera)
        {
            _controller = controller;
            _queue = queue;
            _camera = camera;
        }

        void Update()
        {
            if (!Input.GetMouseButtonDown(0)) return;

            TrySend(Input.mousePosition);
        }

        void TrySend(Vector3 screenPosition)
        {
            if (_controller == null || _queue == null || _camera == null) return;

            var ray = _camera.ScreenPointToRay(screenPosition);
            var ground = new Plane(Vector3.up, Vector3.zero);

            if (!ground.Raycast(ray, out float distance)) return;

            Vector3 world = ray.GetPoint(distance);
            var spec = new Vector2(world.x / Space3D.Scale, -world.z / Space3D.Scale);

            int slot = _queue.SlotAt(spec, HitRadius);
            if (slot < 0) return;

            _controller.SendCart(slot);
        }
    }
}
