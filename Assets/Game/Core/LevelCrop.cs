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
        /// <summary>Прямоугольник, оставшийся от сетки после обрезки.</summary>
        public struct Window
        {
            public int Top, Left, Height, Width;

            /// <summary>Обрезать нечего: окно совпадает с исходной сеткой.</summary>
            public bool IsWhole;
        }

        /// <summary>
        /// Границы рисунка. Вынесено отдельно от <see cref="Apply"/>, потому что
        /// слоёв у уровня теперь несколько — прочность и скрытость идут такими же
        /// строками, — и обрезать их надо ровно тем же окном. Считать окно по
        /// каждому слою нельзя: у слоя прочности своя форма, и картинка разъехалась бы.
        /// </summary>
        public static Window Measure(string[] rows)
        {
            var window = new Window { IsWhole = true };

            if (rows == null || rows.Length == 0) return window;

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

            if (top < 0) return window;   // ни одного блока

            int height = bottom - top + 1;
            int width = right - left + 1;

            if (top == 0 && left == 0 && height == rows.Length && width == rows[0].Length)
                return window;

            return new Window
            {
                Top = top,
                Left = left,
                Height = height,
                Width = width,
                IsWhole = false,
            };
        }

        /// <summary>
        /// Вырезать окно из любого слоя. Недостающие клетки добиваются нулями:
        /// строки слоёв бывают короче картинки, и это законно — значит «по умолчанию».
        /// </summary>
        public static string[] Apply(string[] rows, Window window)
        {
            if (rows == null || rows.Length == 0 || window.IsWhole) return rows;

            var cropped = new string[window.Height];

            for (int r = 0; r < window.Height; r++)
            {
                int source = window.Top + r;
                string row = source < rows.Length ? rows[source] ?? string.Empty : string.Empty;

                var chars = new char[window.Width];

                for (int c = 0; c < window.Width; c++)
                {
                    int index = window.Left + c;
                    chars[c] = index < row.Length ? row[index] : '0';
                }

                cropped[r] = new string(chars);
            }

            return cropped;
        }

        /// <summary>
        /// Тот же рисунок без пустых полей по краям.
        ///
        /// Возвращает исходный массив, если обрезать нечего или если рисунка нет
        /// вовсе: пустая сетка — это испорченные данные уровня, и превращать её
        /// в массив нулевого размера значит уронить игру дальше по цепочке, вместо
        /// того чтобы показать пустую доску.
        /// </summary>
        public static string[] Trim(string[] rows) => Apply(rows, Measure(rows));
    }
}
