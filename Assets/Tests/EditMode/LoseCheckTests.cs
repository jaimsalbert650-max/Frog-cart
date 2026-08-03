using NUnit.Framework;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public class LoseCheckTests
    {
        static GridModel TwoBlacks() => GridModel.FromRows(new[] { "11", "00" });

        static int[] Capacity(int color, int amount)
        {
            var capacity = new int[GridModel.MaxColor + 1];
            capacity[color] = amount;
            return capacity;
        }

        [Test]
        public void NotLost_WhenCapacityCoversRemaining()
        {
            Assert.IsFalse(LoseCheck.IsLost(LoseCheck.CountHits(TwoBlacks(), null), Capacity(1, 2), null));
        }

        [Test]
        public void Lost_WhenCapacityIsShort()
        {
            Assert.IsTrue(LoseCheck.IsLost(LoseCheck.CountHits(TwoBlacks(), null), Capacity(1, 1), null));
        }

        [Test]
        public void ReservedBlocksDoNotCountAsRemaining()
        {
            // Блок уже летит на языке: он оплачен ёмкостью и из остатка вычитается.
            var reserved = new int[GridModel.MaxColor + 1];
            reserved[1] = 1;

            Assert.IsFalse(LoseCheck.IsLost(LoseCheck.CountHits(TwoBlacks(), null), Capacity(1, 1), reserved));
        }

        [Test]
        public void ChecksEveryColorIndependently()
        {
            var grid = GridModel.FromRows(new[] { "12", "00" });
            var capacity = new int[GridModel.MaxColor + 1];
            capacity[1] = 5;
            capacity[2] = 0;

            Assert.IsTrue(LoseCheck.IsLost(LoseCheck.CountHits(grid, null), capacity, null),
                "красному не хватает, хотя чёрного в избытке");
        }

        [Test]
        public void NotLost_WhenBoardIsEmpty()
        {
            var grid = GridModel.FromRows(new[] { "00", "00" });

            Assert.IsFalse(LoseCheck.IsLost(LoseCheck.CountHits(grid, null), new int[GridModel.MaxColor + 1], null));
        }

        [Test]
        public void LoseTestLevel_FromChecklistIsLostImmediately()
        {
            // 07-checklist.md: очередь black 1 / black 1 вместо 8 / 6 —
            // ёмкость чёрного 5 + 1 + 1 = 7 против 14 блоков на картинке.
            var grid = GridModel.FromRows(GridModelTests.Balloon);

            var capacity = new int[GridModel.MaxColor + 1];
            capacity[1] = 5 + 1 + 1;
            capacity[2] = 28 + 4;
            capacity[3] = 50 + 8;
            capacity[4] = 24 + 6;
            capacity[5] = 2;

            Assert.IsTrue(LoseCheck.IsLost(LoseCheck.CountHits(grid, null), capacity, null));
        }

        [Test]
        public void RealLevelOne_IsNotLostAtStart()
        {
            var grid = GridModel.FromRows(GridModelTests.Balloon);

            var capacity = new int[GridModel.MaxColor + 1];
            capacity[1] = 5 + 8 + 6;
            capacity[2] = 28 + 4;
            capacity[3] = 50 + 8;
            capacity[4] = 24 + 6;
            capacity[5] = 2;

            Assert.IsFalse(LoseCheck.IsLost(LoseCheck.CountHits(grid, null), capacity, null),
                "уровень 1 по построению проходим");
        }

        [Test]
        public void ArmouredCellCostsAsManyPlacesAsItHasLives()
        {
            // Одна прочная клетка на пять жизней. Мест в вагонетке четыре.
            var grid = GridModel.FromRows(new[] { "1" });
            var layers = CellLayers.Build(new[] { "1" }, new[] { "5" }, null);

            var capacity = new int[GridModel.MaxColor + 1];
            capacity[1] = 4;

            var hits = LoseCheck.CountHits(grid, layers);

            Assert.AreEqual(5, hits[1], "остаток обязан считаться в ударах, а не в блоках");
            Assert.IsTrue(LoseCheck.IsLost(hits, capacity, null),
                "пяти ударов на четыре места не хватит, а счёт по блокам этого не видел");

            capacity[1] = 5;
            Assert.IsFalse(LoseCheck.IsLost(LoseCheck.CountHits(grid, layers), capacity, null));
        }

        [Test]
        public void WithoutLayersEveryCellCostsOneHit()
        {
            // Уровни без слоя прочности должны считаться ровно как раньше.
            var grid = GridModel.FromRows(new[] { "121", "212" });

            var hits = LoseCheck.CountHits(grid, null);

            Assert.AreEqual(grid.CountOfColor(1), hits[1]);
            Assert.AreEqual(grid.CountOfColor(2), hits[2]);
        }

        [Test]
        public void EmptyCellsAddNothingEvenWithArmourLayer()
        {
            // Слой прочности покрывает всю сетку, включая пустые клетки. Их
            // прочность не должна попадать в остаток: иначе проигрыш объявлялся бы
            // на ровном месте.
            var grid = GridModel.FromRows(new[] { "100" });
            var layers = CellLayers.Build(new[] { "100" }, new[] { "333" }, null);

            var hits = LoseCheck.CountHits(grid, layers);

            Assert.AreEqual(3, hits[1], "считаться должна только живая клетка");
        }
    }
}
