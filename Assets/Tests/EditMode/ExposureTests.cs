using NUnit.Framework;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public class ExposureTests
    {
        [Test]
        public void BlockOnGridBorderIsExposed()
        {
            var grid = GridModel.FromRows(new[]
            {
                "11",
                "11",
            });
            var exposure = new Exposure(grid);

            Assert.IsTrue(exposure.IsExposed(0, 0), "за краем поля — открытый воздух");
        }

        [Test]
        public void EnclosedBlockIsNotExposed()
        {
            // Центральный блок закрыт со всех четырёх сторон.
            var grid = GridModel.FromRows(new[]
            {
                "00000",
                "01110",
                "01210",
                "01110",
                "00000",
            });
            var exposure = new Exposure(grid);

            Assert.IsFalse(exposure.IsExposed(2, 2), "блок в середине замурован");
            Assert.IsTrue(exposure.IsExposed(1, 2), "верхний слой открыт");
        }

        [Test]
        public void ClearingOuterBlockExposesTheOneBehind()
        {
            var grid = GridModel.FromRows(new[]
            {
                "00000",
                "01110",
                "01210",
                "01110",
                "00000",
            });
            var exposure = new Exposure(grid);

            Assert.IsFalse(exposure.IsExposed(2, 2));

            grid.Clear(1, 2);
            exposure.Recompute();

            Assert.IsTrue(exposure.IsExposed(2, 2), "объели сверху — середина открылась");
        }

        [Test]
        public void SealedPocketDoesNotExposeAnything()
        {
            // Пустая клетка в центре не связана с внешним краем: она замурована,
            // и окружающие её блоки через неё недоступны.
            var grid = GridModel.FromRows(new[]
            {
                "00000",
                "01110",
                "01012",
                "01110",
                "00000",
            });
            var exposure = new Exposure(grid);

            // Главное утверждение теста: блок (2,3) со всех сторон закрыт блоками,
            // а слева от него — замурованная пустота (2,2), не связанная с краем.
            // Через неё доступа нет, значит блок недоступен.
            Assert.IsFalse(exposure.IsExposed(2, 3),
                "замурованная пустота не открывает доступ к соседнему блоку");

            // Для контраста — соседи, открытые настоящим внешним воздухом.
            Assert.IsTrue(exposure.IsExposed(2, 1), "слева от него открытый край поля");
            Assert.IsTrue(exposure.IsExposed(1, 2), "сверху пустой ряд, связанный с краем");
            Assert.IsTrue(exposure.IsExposed(2, 4), "стоит на самом краю картинки");
        }

        [Test]
        public void EmptyCellIsNeverExposed()
        {
            var grid = GridModel.FromRows(new[] { "10", "00" });
            var exposure = new Exposure(grid);

            Assert.IsFalse(exposure.IsExposed(0, 1), "пустая клетка не блок");
        }

        [Test]
        public void BalloonLevelStartsWithOnlyItsOutlineExposed()
        {
            var grid = GridModel.FromRows(GridModelTests.Balloon);
            var exposure = new Exposure(grid);

            int exposed = exposure.CountExposed();

            Assert.Less(exposed, grid.TotalBlocks,
                "на старте доступна не вся картинка, а только её контур");
            Assert.Greater(exposed, 0, "что-то съесть всё-таки можно");

            // Верхушка шара — синий флажок в ряду 0, он на самом краю картинки.
            Assert.IsTrue(exposure.IsExposed(0, 6));

            // Сердцевина оболочки закрыта со всех сторон.
            Assert.IsFalse(exposure.IsExposed(4, 6), "внутренность шара недоступна сразу");
        }

        [Test]
        public void PeelingTheBalloonEventuallyReachesTheCore()
        {
            var grid = GridModel.FromRows(GridModelTests.Balloon);
            var exposure = new Exposure(grid);

            int guard = 0;

            while (grid.TotalBlocks > 0 && guard++ < 500)
            {
                bool ateSomething = false;

                for (int r = 0; r < grid.Rows && !ateSomething; r++)
                for (int c = 0; c < grid.Cols && !ateSomething; c++)
                {
                    if (!exposure.IsExposed(r, c)) continue;

                    grid.Clear(r, c);
                    exposure.Recompute();
                    ateSomething = true;
                }

                Assert.IsTrue(ateSomething, "картинка перестала открываться — остался запертый блок");
            }

            Assert.AreEqual(0, grid.TotalBlocks, "уровень разбирается до конца слой за слоем");
        }
    }
}
