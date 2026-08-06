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

        /// <summary>
        /// Партнёр по связке уходит с контура, но места свои не теряет — они
        /// возвращаются в очередь.
        ///
        /// Это не украшение, а условие проходимости. Ёмкости уровня считаются по
        /// числу ударов место в место, запаса нет ни у одного цвета, и вагонетка,
        /// уведённая с контура непустой, уносит свой цвет с уровня насовсем.
        /// На 106-м уровне так и было: связка забирала партнёра со 117
        /// несъеденными блоками из 735, и партия кончалась поражением при
        /// балансе, который сходился на бумаге.
        ///
        /// Уровень для проверки собирается здесь же, а не берётся из пака: связка
        /// в паке одна, на 106-м, и доигрывать до неё пришлось бы сотнями укусов.
        /// Картинка делается ровно того размера, что уже построило представление —
        /// доска в сцене собрана под свой уровень и другого размера не переживёт.
        /// </summary>
        [UnityTest]
        public IEnumerator LinkedPartnerReturnsItsSeatsToTheQueue()
        {
            yield return null;

            int rows = _controller.Grid.Rows;
            int cols = _controller.Grid.Cols;

            Assert.Greater(cols, 2, "для проверки нужна доска шире двух клеток");

            // Два блока первого цвета в углу, весь остальной лист — второго.
            // Углы заняты намеренно: обрезка пустых полей оставит картинку
            // как есть, и она сойдётся с размерами доски.
            int partnerSeats = rows * cols - 2;
            var picture = new string[rows];

            for (int r = 0; r < rows; r++)
                picture[r] = r == 0
                    ? "11" + new string('2', cols - 2)
                    : new string('2', cols);

            var level = ScriptableObject.CreateInstance<LevelData>();
            level.Fill(1, picture, new LevelData.CartDef[0], new[]
            {
                new LevelData.CartDef { colorId = 1, capacity = 2, linkGroup = 1 },
                new LevelData.CartDef { colorId = 2, capacity = partnerSeats, linkGroup = 1 },
            });

            _controller.Level = level;
            _controller.Restart();

            // Автоукус выключается до первого кадра: связанная вагонетка второго
            // цвета иначе начнёт есть сама, и «сколько мест вернулось» станет
            // числом, зависящим от того, когда именно сработал тест.
            _controller.AutoBiteEnabled = false;
            yield return null;

            Assert.IsTrue(_controller.SendCart(0), "первая связанная обязана выехать");
            Assert.IsTrue(_controller.SendCart(0), "вторая связанная обязана выехать");
            Assert.AreEqual(0, _controller.QueueCarts.Count,
                "обе вагонетки уровня стоят на контуре, очередь пуста");

            // Доедаем первый цвет: его вагонетка опустеет и уведёт партнёра,
            // который не съел ещё ничего.
            Assert.IsTrue(_controller.Eat(0, 0), "угловой блок обязан быть съедобен");
            Assert.IsTrue(_controller.Eat(0, 1), "соседний блок тоже");

            float limit = _controller.Config.tongueOut + 2f;

            for (float waited = 0f; waited < limit && !QueueHas(2, partnerSeats); )
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(QueueHas(2, partnerSeats),
                $"партнёр ушёл с контура и унёс с собой {partnerSeats} мест. "
              + "Цвет 2 доесть больше нечем: на уровне ровно столько мест, "
              + "сколько блоков.");

            // Дожидаемся конца выезда: ёмкость слота пропадает только там, и
            // проверка проигрыша до этого мгновения права в любом случае.
            float exit = _controller.Config.emptyToExit
                       + _controller.Config.cartExit
                       + _controller.Config.exitToDock + 0.5f;

            for (float waited = 0f; waited < exit; )
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.AreEqual(GameState.Play, _controller.State,
                "места вернулись в очередь, значит цвет 2 ещё доедаем "
              + "и объявлять поражение не за что");
        }

        bool QueueHas(int color, int capacity)
        {
            foreach (var cart in _controller.QueueCarts)
                if (cart.colorId == color && cart.capacity == capacity) return true;

            return false;
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
