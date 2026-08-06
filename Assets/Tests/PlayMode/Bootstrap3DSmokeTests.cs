using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Дымовые тесты объёмной сцены. Проверяют не красоту — её видно только глазом, —
    /// а то, что дороже всего чинить потом: сцена поднимается без исключений, камера
    /// действительно видит доску, геометрия построена, и логика к ней подключена.
    ///
    /// Отдельный набор от плоской версии: сцены разные, а вот GameController один и тот же,
    /// и это ровно то, что тесты обязаны подтвердить.
    /// </summary>
    public class Bootstrap3DSmokeTests
    {
        GameObject _root;
        GameController _controller;
        Grid3DView _grid;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game3D", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _root = GameObject.Find("Game3DBootstrap");
            _controller = Object.FindAnyObjectByType<GameController>();
            _grid = Object.FindAnyObjectByType<Grid3DView>();
        }

        [UnityTest]
        public IEnumerator SceneBuildsWithoutErrors()
        {
            yield return null;

            Assert.IsNotNull(_root, "объект Game3DBootstrap не найден");
            Assert.IsNotNull(_controller, "контроллер не создан");
            Assert.IsNotNull(_grid, "объёмная доска не создана");
        }

        [UnityTest]
        public IEnumerator ControllerStartsInPlayState()
        {
            yield return null;
            Assert.AreEqual(GameState.Play, _controller.State);
        }

        [UnityTest]
        public IEnumerator BoardHasEveryCellAsMesh()
        {
            yield return null;

            var board = GameObject.Find("Board");
            Assert.IsNotNull(board, "корень доски не найден");

            int blocks = 0;
            int sockets = 0;

            foreach (Transform child in board.transform)
            {
                if (child.name.StartsWith("Block_")) blocks++;
                if (child.name.StartsWith("Socket_")) sockets++;
            }

            Assert.AreEqual(_grid.Rows * _grid.Cols, blocks, "блоков меньше, чем клеток");

            // Гнёзд быть не должно: пустая клетка на объёмной доске — это чистая
            // поверхность площадки, как на референсе, а не вдавленная ячейка.
            Assert.AreEqual(0, sockets, "гнёзда вернулись, доска снова выглядит перфорированной");
        }

        [UnityTest]
        public IEnumerator CameraSeesTheWholeBoard()
        {
            yield return null;

            var camera = Camera.main;
            Assert.IsNotNull(camera, "камеры нет");

            // Четыре угла доски обязаны попадать в кадр. Именно этого не было
            // на первом заходе: камера стояла внутри доски.
            var corners = new[]
            {
                _grid.CellCenter(0, 0),
                _grid.CellCenter(0, _grid.Cols - 1),
                _grid.CellCenter(_grid.Rows - 1, 0),
                _grid.CellCenter(_grid.Rows - 1, _grid.Cols - 1),
            };

            foreach (var corner in corners)
            {
                Vector3 viewport = camera.WorldToViewportPoint(Space3D.ToWorld(corner));

                Assert.Greater(viewport.z, 0f, "угол доски за спиной камеры");
                Assert.That(viewport.x, Is.InRange(0f, 1f), "угол доски вне кадра по горизонтали");
                Assert.That(viewport.y, Is.InRange(0f, 1f), "угол доски вне кадра по вертикали");
            }
        }

        [UnityTest]
        public IEnumerator LoopHasFourPlaces()
        {
            yield return null;

            // Четыре, а не пять: мест на контуре меньше, чем вагонеток в очереди,
            // и выбор игрока держится именно на этом. Число задано одной константой
            // в Game3DBootstrap — если она разъедется с числом жаб или языков,
            // слот на контуре останется без своей жабы, и это упадёт здесь.
            Assert.AreEqual(4, Object.FindObjectsByType<Cart3DView>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(4, Object.FindObjectsByType<Frog3DView>(FindObjectsSortMode.None).Length);
            Assert.AreEqual(4, Object.FindObjectsByType<Tongue3DView>(FindObjectsSortMode.None).Length);
        }

        [UnityTest]
        public IEnumerator EatingWorksInThreeDimensions()
        {
            yield return null;

            int before = _controller.Eaten;

            // Контур стартует пустым: сперва запускаем вагонетку нужного цвета.
            LevelProbe.PrepareBite(_controller, out int r, out int c);
            yield return null;

            Assert.IsTrue(_controller.Eat(r, c), "ход не состоялся");
            yield return null;

            Assert.AreEqual(before + 1, _controller.Eaten);
        }

        [UnityTest]
        public IEnumerator RailsAreBuilt()
        {
            yield return null;

            var rails = GameObject.Find("Rails");
            Assert.IsNotNull(rails, "рельсы не построены");
            Assert.Greater(rails.transform.childCount, 100, "шпал и нитей подозрительно мало");
        }
    }
}
