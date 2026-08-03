namespace FrogCart.Core
{
    /// <summary>
    /// Обрезка картинки уровня по границам самого рисунка.
    ///
    /// Уровни приходят на сетке фиксированного размера, и рисунок в ней часто
    /// занимает меньшую часть — вокруг остаются пустые ряды и столбцы. На доске
    /// это выглядит так, будто картинку положили в угол большого пустого листа:
    /// в оригинале рисунок занимает почти всю доску.
    ///
    /// Обрезка отсекает только сплошные пустые полосы по краям. Пустоты внутри
    /// рисунка не трогаются — они часть изображения, и на них же держится правило
    /// «съедаем снаружи внутрь» из Exposure.
    /// </summary>
    public static class LevelCrop
    {
        /// <summary>
        /// Тот же рисунок без пустых полей по краям.
        ///
        /// Возвращает исходный массив, если обрезать нечего или если рисунка нет
        /// вовсе: пустая сетка — это испорченные данные уровня, и превращать её
        /// в массив нулевого размера значит уронить игру дальше по цепочке, вместо
        /// того чтобы показать пустую доску.
        /// </summary>
        public static string[] Trim(string[] rows)
        {
            if (rows == null || rows.Length == 0) return rows;

            int top = -1, bottom = -1, left = int.MaxValue, right = -1;

            for (int r = 0; r < rows.Length; r++)
            {
                string row = rows[r];
                if (row == null) continue;

                for (int c = 0; c < row.Length; c++)
                {
                    if (row[c] == '0') continue;

                    if (top < 0) top = r;
                    bottom = r;
                    if (c < left) left = c;
                    if (c > right) right = c;
                }
            }

            if (top < 0) return rows;   // ни одного блока

            int height = bottom - top + 1;
            int width = right - left + 1;

            if (top == 0 && left == 0 && height == rows.Length && width == rows[0].Length)
                return rows;            // обрезать нечего

            var cropped = new string[height];

            for (int r = 0; r < height; r++)
            {
                string row = rows[top + r] ?? string.Empty;
                var chars = new char[width];

                for (int c = 0; c < width; c++)
                {
                    int source = left + c;
                    // Строки уровня бывают короче остальных: недостающие клетки — пустые.
                    chars[c] = source < row.Length ? row[source] : '0';
                }

                cropped[r] = new string(chars);
            }

            return cropped;
        }
    }
}
