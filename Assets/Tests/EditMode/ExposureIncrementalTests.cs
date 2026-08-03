using System.Text;
using NUnit.Framework;
using FrogCart.Core;

namespace FrogCart.Tests
{
    /// <summary>
    /// Дозаливка обязана давать ровно то же, что полный пересчёт.
    ///
    /// Оптимизация проходимости — это ровно тот случай, когда «выглядит правильно»
    /// ничего не значит: ошибка проявится не сразу, а на конкретной картинке через
    /// сто ходов, когда внутри останется карман. Поэтому здесь не примеры руками,
    /// а сверка двух реализаций на случайных партиях с фиксированным seed.
    /// </summary>
    public sealed class ExposureIncrementalTests
    {
        [Test]
        public void IncrementalMatchesFullRecompute()
        {
            var random = new System.Random(20260803);

            for (int attempt = 0; attempt < 40; attempt++)
            {
                string[] rows = RandomPicture(random, 12, 14);

                var incremental = GridModel.FromRows(rows);
                var reference = GridModel.FromRows(rows);

                var fast = new Exposure(incremental);
                var slow = new Exposure(reference);

                for (int step = 0; step < 60; step++)
                {
                    if (!PickExposed(fast, incremental, random, out int r, out int c)) break;

                    incremental.Clear(r, c);
                    fast.OnCleared(r, c);

                    reference.Clear(r, c);
                    slow.Recompute();

                    AssertSameExposure(fast, slow, incremental, attempt, step);
                }
            }
        }

        [Test]
        public void ClearingSealedPocketChangesNothingOutside()
        {
            // Кольцо с пустотой внутри: снятие блока со внутренней стенки открывает
            // карман, но наружу он по-прежнему не выходит.
            var rows = new[]
            {
                "1111111",
                "1000001",
                "1011101",
                "1010101",
                "1011101",
                "1000001",
                "1111111",
            };

            var grid = GridModel.FromRows(rows);
            var exposure = new Exposure(grid);

            Assert.IsFalse(exposure.IsExposed(3, 3), "центр кольца не может быть доступен снаружи");

            grid.Clear(2, 3);
            exposure.OnCleared(2, 3);

            Assert.IsFalse(exposure.IsExposed(3, 3),
                "карман внутри кольца открылся наружу, хотя стенка цела");

            var reference = new Exposure(grid);
            AssertSameExposure(exposure, reference, grid, -1, -1);
        }

        [Test]
        public void ClearingBlockThatDoesNotTouchOutsideLeavesMapIntact()
        {
            var rows = new[]
            {
                "11111",
                "11111",
                "11111",
                "11111",
                "11111",
            };

            var grid = GridModel.FromRows(rows);
            var exposure = new Exposure(grid);

            // Сплошной прямоугольник: центр недоступен, снимать его напрямую нельзя,
            // но модель это позволяет — проверяем, что карта воздуха не сломается.
            grid.Clear(2, 2);
            exposure.OnCleared(2, 2);

            var reference = new Exposure(grid);
            AssertSameExposure(exposure, reference, grid, -1, -1);
        }

        static void AssertSameExposure(Exposure fast, Exposure slow, GridModel grid,
                                       int attempt, int step)
        {
            for (int r = 0; r < grid.Rows; r++)
            for (int c = 0; c < grid.Cols; c++)
            {
                if (fast.IsExposed(r, c) == slow.IsExposed(r, c)) continue;

                Assert.Fail($"расхождение на попытке {attempt}, ходе {step}, клетке ({r},{c}): "
                          + $"дозаливка={fast.IsExposed(r, c)}, полный пересчёт={slow.IsExposed(r, c)}");
            }
        }

        /// <summary>Случайная картинка с дырами — на сплошном прямоугольнике карманов не бывает.</summary>
        static string[] RandomPicture(System.Random random, int rows, int cols)
        {
            var picture = new string[rows];
            var line = new StringBuilder(cols);

            for (int r = 0; r < rows; r++)
            {
                line.Clear();

                for (int c = 0; c < cols; c++)
                {
                    bool filled = random.Next(100) < 72;
                    line.Append(filled ? (char)('1' + random.Next(4)) : '0');
                }

                picture[r] = line.ToString();
            }

            return picture;
        }

        static bool PickExposed(Exposure exposure, GridModel grid, System.Random random,
                                out int row, out int col)
        {
            var candidates = new System.Collections.Generic.List<int>();

            for (int r = 0; r < grid.Rows; r++)
            for (int c = 0; c < grid.Cols; c++)
                if (exposure.IsExposed(r, c)) candidates.Add(r * grid.Cols + c);

            if (candidates.Count == 0)
            {
                row = col = -1;
                return false;
            }

            int index = candidates[random.Next(candidates.Count)];
            row = index / grid.Cols;
            col = index % grid.Cols;
            return true;
        }
    }
}
