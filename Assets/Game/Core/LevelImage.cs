using System;
using UnityEngine;

namespace FrogCart.Core
{
    /// <summary>
    /// Превращение картинки в строки уровня.
    ///
    /// Это тот самый пайплайн, который в Food Hunt оказался сильнейшим решением
    /// (`ImportPixelImage` в их редакторе): художник рисует PNG, а дизайнер расставляет
    /// поверх механики. Скорость появления контента упирается в художника, а не
    /// в программиста.
    ///
    /// Логика лежит в ядре и работает с обычным массивом пикселей, поэтому проверяется
    /// тестами без редактора и без картинок на диске.
    /// </summary>
    public static class LevelImage
    {
        /// <summary>Пиксель прозрачнее этого считается пустой клеткой.</summary>
        public const byte AlphaThreshold = 128;

        /// <summary>
        /// Строки уровня из пикселей. Пиксели идут снизу вверх, как их отдаёт Unity,
        /// а строки уровня — сверху вниз, поэтому порядок переворачивается.
        /// </summary>
        /// <param name="pixels">width * height пикселей, начало в левом нижнем углу</param>
        /// <param name="palette">цвета 1..N по порядку; индекс в массиве плюс один</param>
        public static string[] ToRows(Color32[] pixels, int width, int height, Color32[] palette)
        {
            if (pixels == null) throw new ArgumentNullException(nameof(pixels));
            if (palette == null || palette.Length == 0)
                throw new ArgumentException("Палитра пуста");
            if (palette.Length > 9)
                throw new ArgumentException("Больше девяти цветов не влезет в одну цифру");
            if (pixels.Length != width * height)
                throw new ArgumentException(
                    $"Пикселей {pixels.Length}, а размер обещан {width}x{height}");

            var rows = new string[height];
            var buffer = new char[width];

            for (int row = 0; row < height; row++)
            {
                // row 0 — верхняя строка уровня, а в пикселях верх лежит последним.
                int sourceY = height - 1 - row;

                for (int x = 0; x < width; x++)
                    buffer[x] = (char)('0' + ColorIdOf(pixels[sourceY * width + x], palette));

                rows[row] = new string(buffer);
            }

            return rows;
        }

        /// <summary>Ближайший цвет палитры, или 0 для прозрачного пикселя.</summary>
        public static int ColorIdOf(Color32 pixel, Color32[] palette)
        {
            if (pixel.a < AlphaThreshold) return 0;

            int best = 0;
            int bestDistance = int.MaxValue;

            for (int i = 0; i < palette.Length; i++)
            {
                int distance = SquaredDistance(pixel, palette[i]);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = i + 1;
            }

            return best;
        }

        static int SquaredDistance(Color32 a, Color32 b)
        {
            int dr = a.r - b.r;
            int dg = a.g - b.g;
            int db = a.b - b.b;
            return dr * dr + dg * dg + db * db;
        }

        /// <summary>Сколько блоков каждого цвета получилось — для подсказки дизайнеру.</summary>
        public static int[] CountByColor(string[] rows, int colorCount)
        {
            var counts = new int[colorCount + 1];

            foreach (string row in rows)
            foreach (char c in row)
            {
                int id = c - '0';
                if (id > 0 && id <= colorCount) counts[id]++;
            }

            return counts;
        }
    }
}
