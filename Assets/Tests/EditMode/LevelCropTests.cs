using NUnit.Framework;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public sealed class LevelCropTests
    {
        [Test]
        public void RemovesEmptyBorder()
        {
            var rows = new[]
            {
                "00000",
                "00120",
                "00340",
                "00000",
            };

            var cropped = LevelCrop.Trim(rows);

            Assert.AreEqual(2, cropped.Length, "лишние ряды не отрезаны");
            Assert.AreEqual("12", cropped[0]);
            Assert.AreEqual("34", cropped[1]);
        }

        [Test]
        public void KeepsHolesInsideThePicture()
        {
            // Пустота внутри рисунка — часть изображения, и на ней держится
            // правило «съедаем снаружи внутрь». Отрезать её нельзя.
            var rows = new[]
            {
                "0000",
                "0110",
                "0100",
                "0110",
                "0000",
            };

            var cropped = LevelCrop.Trim(rows);

            Assert.AreEqual(3, cropped.Length);
            Assert.AreEqual("11", cropped[0]);
            Assert.AreEqual("10", cropped[1]);
            Assert.AreEqual("11", cropped[2]);
        }

        [Test]
        public void ReturnsInputWhenAlreadyTight()
        {
            var rows = new[] { "12", "34" };

            Assert.AreSame(rows, LevelCrop.Trim(rows), "плотную картинку трогать незачем");
        }

        [Test]
        public void ReturnsInputWhenNothingToCrop()
        {
            // Пустая сетка — испорченные данные уровня. Массив нулевого размера
            // уронил бы сборку доски, поэтому отдаём исходный.
            var rows = new[] { "000", "000" };

            Assert.AreSame(rows, LevelCrop.Trim(rows));
        }

        [Test]
        public void HandlesNullAndEmpty()
        {
            Assert.IsNull(LevelCrop.Trim(null));

            var empty = new string[0];
            Assert.AreSame(empty, LevelCrop.Trim(empty));
        }

        [Test]
        public void PadsShortRows()
        {
            // Строки уровня бывают разной длины; недостающие клетки — пустые.
            var rows = new[]
            {
                "0011",
                "001",
                "0011",
            };

            var cropped = LevelCrop.Trim(rows);

            Assert.AreEqual(3, cropped.Length);
            Assert.AreEqual("11", cropped[0]);
            Assert.AreEqual("10", cropped[1], "короткая строка не добита нулями");
            Assert.AreEqual("11", cropped[2]);
        }

        [Test]
        public void CroppedPictureKeepsEveryBlock()
        {
            var rows = new[]
            {
                "000000",
                "012000",
                "000300",
                "000000",
                "000000",
            };

            int before = CountBlocks(rows);
            int after = CountBlocks(LevelCrop.Trim(rows));

            Assert.AreEqual(before, after, "обрезка потеряла блоки");
        }

        static int CountBlocks(string[] rows)
        {
            int count = 0;

            foreach (var row in rows)
            foreach (var cell in row)
                if (cell != '0') count++;

            return count;
        }
    }
}
