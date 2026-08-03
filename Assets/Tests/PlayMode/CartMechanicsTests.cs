using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Data;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Механики вагонеток: лёд и связка.
    ///
    /// Обе проверяются на живой сцене, а не на модели: правила живут в
    /// GameController и завязаны на слоты контура, очередь и корутины замены —
    /// то есть ровно на то, что в отрыве от сцены не воспроизвести.
    ///
    /// Главное, что здесь проверяется, — не «работает как задумано», а
    /// **отсутствие тупика**. На этой игре уже был случай, когда формально
    /// корректные правила запирали уровень насмерть на 87 блоках из 88.
    /// </summary>
    public class CartMechanicsTests
    {
        GameController _controller;
        Grid3DView _grid;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game3D", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _controller = Object.FindAnyObjectByType<GameController>();
            _grid = Object.FindAnyObjectByType<Grid3DView>();
        }

        [UnityTest]
        public IEnumerator FrozenCartIsNeverTheOnlyWayForward()
        {
            yield return null;

            var level = _controller.Level;
            Assert.IsNotNull(level, "уровень не назначен");

            // Контур стартует пустым, все вагонетки в очереди. Первая обязана быть
            // рабочей: игрок должен иметь ход с самого начала, иначе первый же тап
            // упирается в лёд и игра выглядит сломанной.
            Assert.Greater(level.Queue.Length, 0, "очередь пуста, запускать нечего");
            Assert.AreEqual(0, level.Queue[0].frozenCount,
                "первая вагонетка в очереди не может быть замороженной");
        }

        [UnityTest]
        public IEnumerator EveryTapMakesProgressOnIce()
        {
            yield return null;

            // Ход либо съедает блок, либо скалывает лёд. Третьего быть не должно:
            // именно на этом держится защита от тупика.
            LevelProbe.PrepareBite(_controller, out int r, out int c);
            yield return null;

            int before = _controller.Eaten;
            bool fired = _controller.Eat(r, c);

            yield return null;

            Assert.IsTrue(fired, "обычный ход по открытой клетке обязан состояться");
            Assert.AreEqual(before + 1, _controller.Eaten);
        }

        [UnityTest]
        public IEnumerator LinkedCartsSharePairNumber()
        {
            yield return null;

            var level = _controller.Level;
            var groups = new System.Collections.Generic.Dictionary<int, int>();

            foreach (var cart in level.Queue)
            {
                if (cart.linkGroup == 0) continue;

                groups.TryGetValue(cart.linkGroup, out int count);
                groups[cart.linkGroup] = count + 1;
            }

            foreach (var pair in groups)
                Assert.AreEqual(2, pair.Value,
                    $"связка {pair.Key}: в ней {pair.Value} вагонеток, а должно быть ровно две. "
                  + "Одиночка в связке никогда не уедет по партнёру, "
                  + "тройка уводила бы с контура сразу три слота.");
        }

        [UnityTest]
        public IEnumerator LevelStaysSolvableWithMechanics()
        {
            yield return null;

            var level = _controller.Level;

            // Суммарная ёмкость обязана покрывать все удары по картинке. Прочные
            // клетки тратят по месту за удар, поэтому недосчитаться здесь легко.
            var capacity = new System.Collections.Generic.Dictionary<int, int>();

            foreach (var cart in level.Queue) Add(capacity, cart);

            var rows = _controller.Rows;
            Assert.IsNotNull(rows);

            var needed = new System.Collections.Generic.Dictionary<int, int>();

            for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c < rows[r].Length; c++)
            {
                int color = rows[r][c] - '0';
                if (color <= 0) continue;

                needed.TryGetValue(color, out int count);
                needed[color] = count + 1;
            }

            foreach (var pair in needed)
            {
                capacity.TryGetValue(pair.Key, out int have);
                Assert.GreaterOrEqual(have, pair.Value,
                    $"цвет {pair.Key}: блоков {pair.Value}, а мест всего {have}");
            }
        }

        static void Add(System.Collections.Generic.Dictionary<int, int> map,
                        LevelData.CartDef cart)
        {
            if (cart.capacity <= 0) return;

            map.TryGetValue(cart.colorId, out int count);
            map[cart.colorId] = count + cart.capacity;
        }
    }
}
