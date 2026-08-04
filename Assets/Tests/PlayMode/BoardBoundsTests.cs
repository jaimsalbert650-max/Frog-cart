using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Ход мимо доски — отказ, а не исключение.
    ///
    /// Проверка выглядит формальной, но стоила зависшего плеера. Снималка обходила
    /// доску циклом с зашитыми 16 и 14 — размерами уровня 1. Уровни давно режутся из
    /// картинок и после обрезки бывают меньше, `Eat` уходил за массив, исключение
    /// убивало корутину, и снимок не делался никогда: процесс висел, пока его не
    /// убьют по таймауту.
    ///
    /// Координаты в `Eat` приходят снаружи — от ввода, тестов и инструментов, — и ни
    /// один из них не обязан знать размер доски заранее.
    /// </summary>
    public class BoardBoundsTests
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

        [UnityTest]
        public IEnumerator EatOutsideTheBoardIsARefusalNotACrash()
        {
            yield return null;

            var board = _controller.Rows;
            Assert.IsNotNull(board, "картинка уровня ещё не построена");

            int rows = board.Length;
            int cols = board[0].Length;

            Assert.IsFalse(_controller.Eat(rows, 0), "ряд за нижним краем доски");
            Assert.IsFalse(_controller.Eat(0, cols), "столбец за правым краем доски");
            Assert.IsFalse(_controller.Eat(-1, 0), "ряд до верхнего края доски");
            Assert.IsFalse(_controller.Eat(0, -1), "столбец до левого края доски");
            Assert.IsFalse(_controller.Eat(999, 999), "заведомо далеко за доской");
        }

        [UnityTest]
        public IEnumerator CellOutsideTheBoardReadsAsEmpty()
        {
            yield return null;

            var board = _controller.Rows;
            Assert.IsNotNull(board);

            Assert.AreEqual(0, _controller.Cell(board.Length, 0));
            Assert.AreEqual(0, _controller.Cell(-1, -1));
        }
    }
}
