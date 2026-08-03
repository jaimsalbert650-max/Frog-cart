using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Протяжка пальцем: удерживая, игрок ведёт по картинке и съедает по одному блоку
    /// не чаще чем раз в chainDelay (по умолчанию 90 мс).
    ///
    /// Проверяется через настоящие обработчики указателя, а не через GameController
    /// напрямую: троттлинг живёт именно в GridInput, и чтение кода его не доказывает.
    /// </summary>
    public class DragChainTests
    {
        GameController _controller;
        GridInput _input;
        GridView _grid;
        RectTransform _gridRect;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _controller = Object.FindAnyObjectByType<GameController>();
            _grid = Object.FindAnyObjectByType<GridView>();

            var catcher = GameObject.Find("GridInput");
            _input = catcher.GetComponent<GridInput>();
            _gridRect = (RectTransform)catcher.transform;
        }

        /// <summary>Экранная точка центра ячейки картинки.</summary>
        Vector2 ScreenPointOf(int r, int c)
        {
            // Пивот приёмника — левый верхний угол, Y вниз, как во всей spec-математике.
            var local = new Vector2(c * _grid.CellW + _grid.CellW * 0.5f,
                                    -(r * _grid.CellH + _grid.CellH * 0.5f));

            Vector3 world = _gridRect.TransformPoint(local);
            return RectTransformUtility.WorldToScreenPoint(null, world);
        }

        PointerEventData At(int r, int c)
            => new PointerEventData(EventSystem.current) { position = ScreenPointOf(r, c) };

        [UnityTest]
        public IEnumerator PointerDownEatsOneBlock()
        {
            int before = _controller.Eaten;

            // Ряд 0, столбцы 6 и 7 — синий флажок на верхушке шара: он на самом краю
            // картинки, поэтому доступен снаружи с первого кадра.
            _input.OnPointerDown(At(0, 6));
            yield return null;

            Assert.AreEqual(before + 1, _controller.Eaten, "тап не съел блок");
        }

        [UnityTest]
        public IEnumerator DragIsThrottledByChainDelay()
        {
            _input.OnPointerDown(At(0, 6));
            yield return null;

            int afterFirst = _controller.Eaten;

            // Сразу же — второй блок протяжкой. Должен быть отклонён троттлингом.
            _input.OnDrag(At(0, 7));
            yield return null;

            Assert.AreEqual(afterFirst, _controller.Eaten,
                "протяжка съела второй блок, не выждав chainDelay");

            // Выждали задержку — теперь тот же жест обязан сработать.
            yield return new WaitForSecondsRealtime(_controller.ChainDelay + 0.05f);

            _input.OnDrag(At(0, 7));
            yield return null;

            Assert.AreEqual(afterFirst + 1, _controller.Eaten,
                "после chainDelay протяжка обязана съесть следующий блок");
        }

        [UnityTest]
        public IEnumerator PointerUpResetsTheChain()
        {
            _input.OnPointerDown(At(0, 6));
            yield return null;

            int afterFirst = _controller.Eaten;

            _input.OnPointerUp(At(0, 6));
            yield return null;

            // Отпустили — счётчик задержки сброшен, следующая протяжка проходит сразу.
            _input.OnDrag(At(0, 7));
            yield return null;

            Assert.AreEqual(afterFirst + 1, _controller.Eaten,
                "после отпускания протяжка должна срабатывать без ожидания");
        }

        [UnityTest]
        public IEnumerator TapOutsideTheGridChangesNothing()
        {
            int before = _controller.Eaten;

            _input.OnPointerDown(new PointerEventData(EventSystem.current)
            {
                position = new Vector2(-500f, -500f),
            });
            yield return null;

            Assert.AreEqual(before, _controller.Eaten);
        }
    }
}
