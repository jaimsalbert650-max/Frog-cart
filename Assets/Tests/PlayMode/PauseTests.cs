using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Пауза обязана останавливать ход целиком, а не только контур.
    ///
    /// Проверять это пришлось живой сценой: укус разнесён во времени между тапом
    /// и прилётом языка, и держит его корутина — в отрыве от сцены такого не
    /// воспроизвести.
    ///
    /// Смысл проверок не в состоянии флага, а в том, что игрок увидит. Пока
    /// висит панель паузы, доска обязана стоять: блок, за которым язык уже
    /// вылетел, не может исчезнуть у игрока за спиной.
    /// </summary>
    public class PauseTests
    {
        GameController _controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game3D", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _controller = Object.FindAnyObjectByType<GameController>();
        }

        /// <summary>Сколько ждать, чтобы язык заведомо успел долететь.</summary>
        float TongueFlight => _controller.Config.tongueOut + 0.3f;

        [UnityTest]
        public IEnumerator PauseKeepsTheBlockTongueIsFlyingTo()
        {
            yield return null;

            LevelProbe.PrepareBite(_controller, out int r, out int c);
            yield return null;

            Assert.AreNotEqual(0, _controller.Cell(r, c), "клетка пуста ещё до укуса");
            Assert.IsTrue(_controller.Eat(r, c), "укус по открытой клетке обязан состояться");

            _controller.TogglePause();
            Assert.AreEqual(GameState.Pause, _controller.State, "пауза не включилась");

            yield return new WaitForSecondsRealtime(TongueFlight);

            Assert.AreNotEqual(0, _controller.Cell(r, c),
                "блок исчез с доски во время паузы: язык долетел под панелью. "
              + "Снятие блока висит на таймере, и таймер обязан стоять вместе с игрой");
        }

        [UnityTest]
        public IEnumerator ResumeFinishesTheBitePauseHeld()
        {
            yield return null;

            LevelProbe.PrepareBite(_controller, out int r, out int c);
            yield return null;

            Assert.IsTrue(_controller.Eat(r, c));

            _controller.TogglePause();
            yield return new WaitForSecondsRealtime(TongueFlight);

            _controller.Resume();
            Assert.AreEqual(GameState.Play, _controller.State, "пауза не снялась");

            yield return new WaitForSecondsRealtime(TongueFlight);

            Assert.AreEqual(0, _controller.Cell(r, c),
                "после снятия паузы укус обязан доиграть. Придержать его пауза может, "
              + "потерять — нет: клетка осталась бы помеченной «в полёте» навсегда");
        }

        [UnityTest]
        public IEnumerator ResumeDoesNotFireEveryCartAtOnce()
        {
            yield return null;

            // Здесь автоукус нужен: проверяется именно он.
            LevelProbe.FindEdibleCell(_controller, out int r, out int c);
            LevelProbe.SendCartFor(_controller, _controller.Rows[r][c] - '0',
                                   disableAutoBite: false);

            _controller.TogglePause();

            // Пауза заметно длиннее интервала между укусами. Если отсчёт ведётся по
            // абсолютным часам, за это время он протухнет весь.
            yield return new WaitForSecondsRealtime(2f);

            int before = _controller.Eaten;
            _controller.Resume();
            yield return null;

            Assert.AreEqual(before, _controller.Eaten,
                "вагонетка выстрелила в первом же кадре после паузы. Пауза не идёт "
              + "в зачёт ожидания: простояв минуту, контур не должен разряжаться залпом");
        }
    }
}
