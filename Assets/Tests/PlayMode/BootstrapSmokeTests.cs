using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Дымовой тест сцены. Визуальную достоверность он не проверяет — на это есть
    /// чек-лист 07 и глаза, — но ловит то, что дороже всего чинить позже:
    /// исключение в Awake, недостроенную иерархию и мёртвый ввод.
    /// </summary>
    public class BootstrapSmokeTests
    {
        GameObject _root;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _root = GameObject.Find("GameBootstrap");
        }

        [UnityTest]
        public IEnumerator SceneBuildsWithoutErrors()
        {
            yield return null;

            Assert.IsNotNull(_root, "объект GameBootstrap не найден в сцене");
            Assert.IsNotNull(_root.GetComponent<GameController>(), "контроллер не создан");
        }

        [UnityTest]
        public IEnumerator ControllerStartsInPlayState()
        {
            yield return null;

            var controller = _root.GetComponent<GameController>();
            Assert.AreEqual(GameState.Play, controller.State);
        }

        [UnityTest]
        public IEnumerator GridHasEveryCell()
        {
            yield return null;

            var gridRoot = GameObject.Find("GridRoot");
            Assert.IsNotNull(gridRoot, "GridRoot не создан");

            int blocks = 0;
            int sockets = 0;

            foreach (Transform child in gridRoot.transform)
            {
                if (child.name.StartsWith("Block_")) blocks++;
                if (child.name.StartsWith("Socket_")) sockets++;
            }

            // Не 224: картинка обрезается по своим границам, и размер доски теперь
            // задаёт сам рисунок. Сверяем с тем, что доска о себе заявляет.
            var grid = Object.FindAnyObjectByType<GridView>();
            int cells = grid.Rows * grid.Cols;

            Assert.AreEqual(cells, blocks, "блоков меньше, чем клеток");
            Assert.AreEqual(cells, sockets, "и столько же гнёзд под ними");
        }

        [UnityTest]
        public IEnumerator FiveCartsAndFiveFrogsExist()
        {
            yield return null;

            var cartLayer = GameObject.Find("CartLayer");
            var frogLayer = GameObject.Find("FrogLayer");

            Assert.IsNotNull(cartLayer);
            Assert.IsNotNull(frogLayer);
            Assert.AreEqual(5, cartLayer.transform.childCount);
            Assert.AreEqual(5, frogLayer.transform.childCount);
        }

        [UnityTest]
        public IEnumerator CartsMoveAlongTheRail()
        {
            // В batchmode -nographics Unity за время ожидания пишет
            // «No graphic device is available to initialize the view», и раннер считает
            // любой Error провалом. К движению вагонетки это отношения не имеет.
            LogAssert.ignoreFailingMessages = true;

            yield return null;

            var cart = GameObject.Find("CartLayer").transform.GetChild(0) as RectTransform;
            Vector2 before = cart.anchoredPosition;

            // 19 px/с — за полсекунды сдвиг обязан быть заметным.
            float waited = 0f;
            while (waited < 0.5f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            float moved = Vector2.Distance(before, cart.anchoredPosition);
            LogAssert.ignoreFailingMessages = false;

            Assert.Greater(moved, 1f, "вагонетка стоит на месте — dist не растёт");
        }

        [UnityTest]
        public IEnumerator EatingABlockAdvancesProgress()
        {
            yield return null;

            var controller = _root.GetComponent<GameController>();

            var percent = GameObject.Find("Percent");
            Assert.IsNotNull(percent, "текст процента не найден");

            int before = controller.Eaten;

            LevelProbe.FindEdibleCell(controller, out int r, out int c);

            // Раньше тест звал Eat по захардкоженной клетке и не проверял результат:
            // после обрезки картинки он бы молча остался зелёным, ничего не проверяя.
            Assert.IsTrue(controller.Eat(r, c), "ход не состоялся");

            yield return null;

            Assert.AreEqual(before + 1, controller.Eaten, "прогресс не сдвинулся");
        }

        [UnityTest]
        public IEnumerator InputCatcherCoversTheGrid()
        {
            yield return null;

            var catcher = GameObject.Find("GridInput");
            Assert.IsNotNull(catcher, "приёмник ввода не создан");

            var image = catcher.GetComponent<Image>();
            Assert.IsTrue(image.raycastTarget, "приёмник ввода не ловит тапы");
            Assert.IsNotNull(catcher.GetComponent<GridInput>());
        }
    }
}
