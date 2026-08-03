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

            var carts = controller.Level.LoopCarts;

            for (int r = 0; r < rows.Length; r++)
            {
                if (IsEmptyRow(rows[r])) continue;

                for (int c = 0; c + 1 < rows[r].Length; c++)
                {
                    int left = rows[r][c] - '0';
                    int right = rows[r][c + 1] - '0';

                    if (left == 0 || right == 0) continue;
                    if (!HasCart(carts, left) || !HasCart(carts, right)) continue;

                    row = r;
                    col = c;
                    return;
                }

                // Верхний ряд есть, но пары в нём нет — глубже искать нельзя,
                // там блоки уже закрыты и правило доступности их не пропустит.
                break;
            }

            row = col = -1;
            Assert.Fail("в верхнем ряду картинки нет пары соседних блоков, "
                      + "чей цвет есть у вагонеток на контуре");
        }

        /// <summary>Один съедобный блок — когда пара не нужна.</summary>
        public static void FindEdibleCell(GameController controller, out int row, out int col)
        {
            FindEdiblePair(controller, out row, out col);
        }

        static bool IsEmptyRow(string row)
        {
            if (string.IsNullOrEmpty(row)) return true;

            foreach (var cell in row)
                if (cell != '0') return false;

            return true;
        }

        static bool HasCart(LevelData.CartDef[] carts, int colorId)
        {
            foreach (var cart in carts)
                if (cart.colorId == colorId && cart.capacity > 0) return true;

            return false;
        }
    }
}
