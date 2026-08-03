using System;

namespace FrogCart.Core
{
    /// <summary>
    /// Текущая картинка: [ряд, столбец], 0 — пустая ячейка.
    /// Счётчики по цветам ведутся инкрементально: проверка проигрыша идёт после
    /// каждого съеденного блока, и пересчитывать 224 ячейки каждый раз незачем.
    /// </summary>
    public sealed class GridModel
    {
        public const int MaxColor = 9;   // одна цифра на клетку в строках уровня

        readonly int[,] _cells;
        readonly int[] _perColor = new int[MaxColor + 1];

        public int Rows { get; }
        public int Cols { get; }
        public int TotalBlocks { get; private set; }

        GridModel(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            _cells = new int[rows, cols];
        }

        public static GridModel FromRows(string[] rows)
        {
            if (rows == null || rows.Length == 0)
                throw new ArgumentException("Уровень пуст");

            int cols = rows[0].Length;
            var grid = new GridModel(rows.Length, cols);

            for (int r = 0; r < rows.Length; r++)
            {
                if (rows[r].Length != cols)
                    throw new ArgumentException(
                        $"Ряд {r}: длина {rows[r].Length}, ожидалось {cols}");

                for (int c = 0; c < cols; c++)
                {
                    int color = rows[r][c] - '0';
                    if (color < 0 || color > MaxColor)
                        throw new ArgumentException(
                            $"Ряд {r}, столбец {c}: недопустимый символ '{rows[r][c]}'");

                    grid._cells[r, c] = color;
                    if (color == 0) continue;

                    grid._perColor[color]++;
                    grid.TotalBlocks++;
                }
            }

            return grid;
        }

        public int Get(int r, int c) => _cells[r, c];

        public bool InBounds(int r, int c) => r >= 0 && c >= 0 && r < Rows && c < Cols;

        public int CountOfColor(int color) => _perColor[color];

        public void Clear(int r, int c)
        {
            int color = _cells[r, c];
            if (color == 0) return;

            _cells[r, c] = 0;
            _perColor[color]--;
            TotalBlocks--;
        }
    }
}
