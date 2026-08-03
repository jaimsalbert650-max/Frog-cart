using NUnit.Framework;
using UnityEngine;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public class LevelImageTests
    {
        static readonly Color32[] Palette =
        {
            new Color32(0x34, 0x39, 0x41, 255),   // 1 black
            new Color32(0xEF, 0x41, 0x36, 255),   // 2 red
            new Color32(0xFF, 0x90, 0x12, 255),   // 3 orange
            new Color32(0xFF, 0xD5, 0x2E, 255),   // 4 yellow
            new Color32(0x2E, 0x93, 0xE6, 255),   // 5 blue
        };

        static Color32 Transparent => new Color32(0, 0, 0, 0);

        [Test]
        public void ExactPaletteColorMapsToItsOwnIndex()
        {
            for (int i = 0; i < Palette.Length; i++)
                Assert.AreEqual(i + 1, LevelImage.ColorIdOf(Palette[i], Palette),
                    $"цвет {i + 1} не узнал сам себя");
        }

        [Test]
        public void TransparentPixelIsEmptyCell()
        {
            Assert.AreEqual(0, LevelImage.ColorIdOf(Transparent, Palette));
        }

        [Test]
        public void NearlyTransparentPixelIsAlsoEmpty()
        {
            var almost = new Color32(0xEF, 0x41, 0x36, 100);   // ниже порога 128
            Assert.AreEqual(0, LevelImage.ColorIdOf(almost, Palette));
        }

        [Test]
        public void OffPaletteColorSnapsToTheNearestOne()
        {
            // Слегка мимо красного — обязан прилипнуть к красному, а не к оранжевому.
            var almostRed = new Color32(0xE0, 0x50, 0x40, 255);
            Assert.AreEqual(2, LevelImage.ColorIdOf(almostRed, Palette));

            // Ближе к жёлтому, чем к оранжевому.
            var almostYellow = new Color32(0xFA, 0xD0, 0x40, 255);
            Assert.AreEqual(4, LevelImage.ColorIdOf(almostYellow, Palette));
        }

        [Test]
        public void RowsAreFlippedBecausePixelsStartAtTheBottom()
        {
            // Картинка 2x2: низ красный, верх синий.
            var pixels = new[]
            {
                Palette[1], Palette[1],   // нижний ряд пикселей
                Palette[4], Palette[4],   // верхний ряд пикселей
            };

            var rows = LevelImage.ToRows(pixels, 2, 2, Palette);

            Assert.AreEqual(2, rows.Length);
            Assert.AreEqual("55", rows[0], "верхняя строка уровня — верхний ряд картинки");
            Assert.AreEqual("22", rows[1], "нижняя строка уровня — нижний ряд картинки");
        }

        [Test]
        public void TransparentPixelsBecomeZeros()
        {
            var pixels = new[]
            {
                Transparent, Palette[0],
                Palette[0], Transparent,
            };

            var rows = LevelImage.ToRows(pixels, 2, 2, Palette);

            Assert.AreEqual("10", rows[0]);
            Assert.AreEqual("01", rows[1]);
        }

        [Test]
        public void CountsBlocksByColor()
        {
            var rows = new[] { "1120", "0034" };

            var counts = LevelImage.CountByColor(rows, 5);

            Assert.AreEqual(2, counts[1]);
            Assert.AreEqual(1, counts[2]);
            Assert.AreEqual(1, counts[3]);
            Assert.AreEqual(1, counts[4]);
            Assert.AreEqual(0, counts[5]);
        }

        [Test]
        public void RejectsMismatchedSize()
        {
            Assert.Throws<System.ArgumentException>(
                () => LevelImage.ToRows(new Color32[3], 2, 2, Palette));
        }

        [Test]
        public void RejectsEmptyPalette()
        {
            Assert.Throws<System.ArgumentException>(
                () => LevelImage.ToRows(new Color32[4], 2, 2, new Color32[0]));
        }

        [Test]
        public void ImportedBalloonMatchesTheHandwrittenLevel()
        {
            // Обратная проверка: рисуем картинку по строкам уровня 1 и импортируем назад.
            // Если пайплайн честный, строки обязаны совпасть до символа.
            string[] original = GridModelTests.Balloon;
            int height = original.Length;
            int width = original[0].Length;

            var pixels = new Color32[width * height];

            for (int row = 0; row < height; row++)
            for (int x = 0; x < width; x++)
            {
                int id = original[row][x] - '0';
                int sourceY = height - 1 - row;

                pixels[sourceY * width + x] = id == 0 ? Transparent : Palette[id - 1];
            }

            var rows = LevelImage.ToRows(pixels, width, height, Palette);

            CollectionAssert.AreEqual(original, rows);
        }
    }
}
