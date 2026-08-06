using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using FrogCart.Core;
using FrogCart.Data;

namespace FrogCart.Tests
{
    /// <summary>
    /// Баланс уровня: мест в вагонетках цвета не меньше, чем ударов по блокам
    /// этого цвета. Проверяется на **всех** ассетах пака сразу, до запуска игры.
    ///
    /// Такая проверка уже была, но только в PlayMode
    /// (`CartMechanicsTests.LevelStaysSolvableWithMechanics`) и только на том
    /// уровне, который загрузила сцена, — то есть на одном из двадцати четырёх.
    /// Считала она к тому же блоки, а не удары: уровень с прочными клетками мог
    /// быть недосчитан вдевятеро и тест бы промолчал.
    ///
    /// Числа берутся не своим счётом, а той же дорогой, что и в игре: обрезка
    /// LevelCrop, слои CellLayers, удары LoseCheck.CountHits. Иначе тест начал бы
    /// расходиться с игрой ровно в тот день, когда правила сменятся.
    /// </summary>
    public sealed class LevelBalanceTests
    {
        /// <summary>
        /// Уровень для проверки поражения. Он обязан быть непроходимым — на нём
        /// снимают экран проигрыша, — поэтому из общей проверки исключён и
        /// разобран отдельным тестом.
        /// </summary>
        const string LoseFixture = "Level01_LoseTest";

        struct Balance
        {
            public int[] Hits;
            public int[] Capacity;
        }

        static IEnumerable<LevelData> AllLevels()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:LevelData"))
            {
                var level = AssetDatabase.LoadAssetAtPath<LevelData>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (level != null && level.Rows != null && level.Rows.Length > 0)
                    yield return level;
            }
        }

        static string NameOf(LevelData level)
        {
            string path = AssetDatabase.GetAssetPath(level);

            return string.IsNullOrEmpty(path)
                ? level.name
                : System.IO.Path.GetFileNameWithoutExtension(path);
        }

        /// <summary>Разбор уровня ровно так, как его строит GameController.BuildLevel.</summary>
        static Balance Measure(LevelData level)
        {
            var window = LevelCrop.Measure(level.Rows);
            string[] rows = LevelCrop.Apply(level.Rows, window);

            var layers = CellLayers.Build(rows,
                LevelCrop.Apply(level.HpRows, window),
                LevelCrop.Apply(level.HiddenRows, window));

            var grid = GridModel.FromRows(rows);

            var capacity = new int[GridModel.MaxColor + 1];
            AddCarts(capacity, level.LoopCarts);
            AddCarts(capacity, level.Queue);

            return new Balance
            {
                Hits = LoseCheck.CountHits(grid, layers),
                Capacity = capacity,
            };
        }

        static void AddCarts(int[] capacity, LevelData.CartDef[] carts)
        {
            if (carts == null) return;

            foreach (var cart in carts)
            {
                // Ёмкость 0 — заглушка конвертера, добивающая loopCarts до пяти.
                if (cart.capacity <= 0) continue;
                if (cart.colorId < 1 || cart.colorId > GridModel.MaxColor) continue;

                capacity[cart.colorId] += cart.capacity;
            }
        }

        [Test]
        public void EveryLevelHasSeatsForEveryHit()
        {
            int checkedLevels = 0;

            foreach (var level in AllLevels())
            {
                string name = NameOf(level);
                if (name == LoseFixture) continue;

                var balance = Measure(level);
                checkedLevels++;

                for (int color = 1; color <= GridModel.MaxColor; color++)
                {
                    if (balance.Hits[color] == 0) continue;

                    Assert.GreaterOrEqual(balance.Capacity[color], balance.Hits[color],
                        $"{name}, цвет {color}: ударов {balance.Hits[color]}, "
                      + $"а мест всего {balance.Capacity[color]}. Уровень непроходим "
                      + "с первого кадра, и проверка проигрыша объявит поражение "
                      + "раньше, чем игрок успеет что-нибудь сделать.");
                }
            }

            Assert.Greater(checkedLevels, 0, "в проекте не нашлось ни одного уровня");
        }

        /// <summary>
        /// Цвет, на который вагонеток нет вовсе, — тот же непроходимый уровень,
        /// только счёт мест даёт ноль, и разница видна не сразу. Отдельная
        /// проверка нужна ради внятного сообщения.
        /// </summary>
        [Test]
        public void EveryColorOnTheBoardHasACart()
        {
            foreach (var level in AllLevels())
            {
                string name = NameOf(level);
                if (name == LoseFixture) continue;

                var balance = Measure(level);

                for (int color = 1; color <= GridModel.MaxColor; color++)
                {
                    if (balance.Hits[color] == 0) continue;

                    Assert.Greater(balance.Capacity[color], 0,
                        $"{name}: цвет {color} есть на картинке, "
                      + "а вагонеток этого цвета нет ни одной");
                }
            }
        }

        /// <summary>
        /// Уровень поражения обязан оставаться непроходимым. Иначе кадр «Lose»
        /// снимался бы с уровня, который на самом деле выигрывается, и проверять
        /// он перестал бы что бы то ни было.
        /// </summary>
        [Test]
        public void LoseFixtureStaysUnwinnable()
        {
            LevelData fixture = null;

            foreach (var level in AllLevels())
                if (NameOf(level) == LoseFixture) fixture = level;

            Assert.IsNotNull(fixture, $"уровень {LoseFixture} не найден");

            var balance = Measure(fixture);
            bool someColorIsShort = false;

            for (int color = 1; color <= GridModel.MaxColor; color++)
                if (balance.Hits[color] > balance.Capacity[color]) someColorIsShort = true;

            Assert.IsTrue(someColorIsShort,
                $"{LoseFixture} стал проходимым: на нём снимают поражение, "
              + "и хотя бы одному цвету обязано не хватать мест");
        }

        /// <summary>
        /// Запаса мест у конвертированных уровней нет: ёмкость считается по числу
        /// ударов ровно. Тест не требует запаса — он фиксирует, что запаса нет, и
        /// объясняет, почему из этого следует.
        ///
        /// Следствие простое: **потерянное место равно проигрышу**. Любая правка,
        /// после которой вагонетка уходит с контура непустой, обязана возвращать
        /// её места в очередь — как это делает `GameController.ReturnSeatsToQueue`.
        /// Ровно на этом сломался 106-й уровень: связка уводила партнёра со 117
        /// несъеденными блоками, и баланс сходился ровно до того мгновения.
        /// </summary>
        [Test]
        public void ConvertedLevelsHaveNoSpareSeats()
        {
            int tight = 0;

            foreach (var level in AllLevels())
            {
                string name = NameOf(level);
                if (name == LoseFixture) continue;

                var balance = Measure(level);
                bool exact = true;

                for (int color = 1; color <= GridModel.MaxColor; color++)
                {
                    if (balance.Hits[color] == 0) continue;
                    if (balance.Capacity[color] != balance.Hits[color]) exact = false;
                }

                if (exact) tight++;
            }

            Assert.Greater(tight, 0,
                "ни у одного уровня ёмкость не совпала с числом ударов в точности. "
              + "Либо конвертер стал давать запас, либо счёт разошёлся с игрой — "
              + "и в том, и в другом случае это стоит увидеть глазами, а не "
              + "узнать однажды на непроходимом уровне.");
        }
    }
}
