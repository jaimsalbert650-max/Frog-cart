using NUnit.Framework;
using FrogCart.Data;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Поиск блока, который заведомо можно съесть прямо сейчас.
    ///
    /// Раньше тесты держали координаты руками: «ряд 0, столбец 6 — синий флажок».
    /// Обрезка пустых полей сдвинула картинку, и пять тестов упали разом, хотя игра
    /// была исправна. Координаты в ассете уровня больше не координаты на доске,
    /// поэтому их надо не помнить, а находить.
    /// </summary>
    public static class LevelProbe
    {
        /// <summary>
        /// Пара соседних блоков в самом верхнем непустом ряду картинки.
        ///
        /// Верхний ряд выбран не случайно: над ним пусто, значит оба блока доступны
        /// снаружи с первого кадра — это требование правила «съедаем снаружи внутрь».
        /// Цвета обоих обязаны быть у вагонеток на контуре, иначе ход не состоится
        /// по причине, к проверяемому поведению отношения не имеющей.
        /// </summary>
        public static void FindEdiblePair(GameController controller, out int row, out int col)
        {
            var rows = controller.Rows;
            Assert.IsNotNull(rows, "картинка уровня ещё не построена");

            // Цвет ищем в живой очереди: контур стартует пустым, и вагонеток
            // на нём нет ни одной, пока игрок не запустит.
            var carts = controller.QueueCarts;

            for (int r = 0; r < rows.Length; r++)
            for (int c = 0; c + 1 < rows[r].Length; c++)
            {
                int left = rows[r][c] - '0';
                int right = rows[r][c + 1] - '0';

                if (left == 0 || right == 0) continue;
                if (!HasCart(carts, left) || !HasCart(carts, right)) continue;

                // Оба блока обязаны быть открыты сверху: над ними в своих столбцах
                // пусто до самого края, значит язык до них дотянется. Ограничиваться
                // верхним рядом нельзя — на настоящем уровне он может оказаться
                // целиком того цвета, чья вагонетка ещё стоит в очереди.
                if (!ClearAbove(rows, r, c) || !ClearAbove(rows, r, c + 1)) continue;

                row = r;
                col = c;
                return;
            }

            row = col = -1;
            Assert.Fail("на картинке нет открытой сверху пары соседних блоков, "
                      + "чей цвет есть в очереди");
        }

        /// <summary>Один съедобный блок — когда пара не нужна.</summary>
        public static void FindEdibleCell(GameController controller, out int row, out int col)
        {
            FindEdiblePair(controller, out row, out col);
        }

        /// <summary>
        /// Запустить из очереди вагонетку нужного цвета и выключить автоукус.
        ///
        /// Нужно почти каждому тесту: контур теперь стартует пустым, вагонетки
        /// запускает игрок, и без запуска ни один укус не состоится. Автоукус при
        /// этом мешает — вагонетка кусает сама раз в 0.28 с и добавляет к счётчику
        /// лишнее, поэтому тест, проверяющий один конкретный укус, его выключает.
        /// </summary>
        public static void SendCartFor(GameController controller, int color,
                                       bool disableAutoBite = true)
        {
            var queue = controller.QueueCarts;
            Assert.IsNotNull(queue, "очередь ещё не построена");

            for (int i = 0; i < queue.Count; i++)
            {
                if (queue[i].colorId != color) continue;
                if (queue[i].frozenCount > 0) continue;

                Assert.IsTrue(controller.SendCart(i),
                    $"вагонетка цвета {color} есть в очереди, но не запустилась");

                if (disableAutoBite) controller.AutoBiteEnabled = false;
                return;
            }

            Assert.Fail($"в очереди нет рабочей вагонетки цвета {color}");
        }

        /// <summary>Пара соседних блоков плюс запущенная под них вагонетка.</summary>
        public static void PrepareBite(GameController controller, out int row, out int col)
        {
            FindEdiblePair(controller, out row, out col);
            SendCartFor(controller, controller.Rows[row][col] - '0');
        }

        /// <summary>Над клеткой в её столбце пусто до края картинки.</summary>
        static bool ClearAbove(string[] rows, int row, int col)
        {
            for (int r = 0; r < row; r++)
            {
                if (col >= rows[r].Length) continue;
                if (rows[r][col] != '0') return false;
            }

            return true;
        }

        static bool HasCart(System.Collections.Generic.IReadOnlyList<LevelData.CartDef> carts,
                            int colorId)
        {
            foreach (var cart in carts)
                if (cart.colorId == colorId && cart.capacity > 0 && cart.frozenCount == 0)
                    return true;

            return false;
        }
    }
}
