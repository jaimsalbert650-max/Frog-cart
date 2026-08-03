using NUnit.Framework;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public class GridModelTests
    {
        // Уровень 1 из docs/unity-spec/03-level-data.md, дословно.
        public static readonly string[] Balloon =
        {
            "00000055000000",
            "00000322300000",
            "00034322343000",
            "00234322343200",
            "00234322343200",
            "00234322343200",
            "00234322343200",
            "00034322343000",
            "00004322340000",
            "00000322300000",
            "00000022000000",
            "00000100100000",
            "00000111100000",
            "00000111100000",
            "00000111100000",
            "00000000000000",
        };

        [Test]
        public void ParsesRowsIntoGrid()
        {
            var grid = GridModel.FromRows(Balloon);

            Assert.AreEqual(16, grid.Rows);
            Assert.AreEqual(14, grid.Cols);
            Assert.AreEqual(5, grid.Get(0, 6), "синий флажок на верхушке");
            Assert.AreEqual(0, grid.Get(15, 0), "нижний ряд пустой");
            Assert.AreEqual(1, grid.Get(12, 5), "корзина");
        }

        [Test]
        public void CountsBlocksPerColorAsInSpec()
        {
            var grid = GridModel.FromRows(Balloon);

            Assert.AreEqual(14, grid.CountOfColor(1), "black");
            Assert.AreEqual(28, grid.CountOfColor(2), "red");
            Assert.AreEqual(30, grid.CountOfColor(3), "orange");
            Assert.AreEqual(14, grid.CountOfColor(4), "yellow");
            Assert.AreEqual(2, grid.CountOfColor(5), "blue");
            Assert.AreEqual(88, grid.TotalBlocks, "всего блоков на уровне 1");
        }

        [Test]
        public void SideColumnsAndLastRowAreEmpty()
        {
            var grid = GridModel.FromRows(Balloon);

            for (int r = 0; r < grid.Rows; r++)
            {
                Assert.AreEqual(0, grid.Get(r, 0), $"ряд {r}, столбец 0");
                Assert.AreEqual(0, grid.Get(r, 1), $"ряд {r}, столбец 1");
                Assert.AreEqual(0, grid.Get(r, 12), $"ряд {r}, столбец 12");
                Assert.AreEqual(0, grid.Get(r, 13), $"ряд {r}, столбец 13");
            }

            for (int c = 0; c < grid.Cols; c++)
                Assert.AreEqual(0, grid.Get(15, c), $"ряд 15, столбец {c}");
        }

        [Test]
        public void ClearingCellDropsTheCount()
        {
            var grid = GridModel.FromRows(Balloon);
            grid.Clear(0, 6);

            Assert.AreEqual(0, grid.Get(0, 6));
            Assert.AreEqual(1, grid.CountOfColor(5));
            Assert.AreEqual(87, grid.TotalBlocks);
        }

        [Test]
        public void ClearingEmptyCellChangesNothing()
        {
            var grid = GridModel.FromRows(Balloon);
            grid.Clear(15, 0);

            Assert.AreEqual(88, grid.TotalBlocks);
        }

        [Test]
        public void RejectsRaggedRows()
        {
            Assert.Throws<System.ArgumentException>(
                () => GridModel.FromRows(new[] { "00", "000" }));
        }
    }
}
