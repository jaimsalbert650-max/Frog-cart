using NUnit.Framework;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public sealed class CellLayersTests
    {
        static readonly string[] Picture =
        {
            "0120",
            "3400",
            "0056",
        };

        [Test]
        public void DefaultsToOneHpAndNothingHidden()
        {
            var layers = CellLayers.Build(Picture, null, null);

            Assert.AreEqual(1, layers.Hp(0, 1), "у обычной клетки прочность 1");
            Assert.IsFalse(layers.IsHidden(0, 1));
            Assert.IsFalse(layers.HasArmoured, "прочных клеток нет — флаг обязан молчать");
            Assert.IsFalse(layers.HasHidden);
        }

        [Test]
        public void EmptyCellHasNoHp()
        {
            // У пустой клетки прочность 0, а не 1: иначе проверка «клетка ещё жива»
            // считала бы пустоту живой.
            var layers = CellLayers.Build(Picture, new[] { "5555", "5555", "5555" }, null);

            Assert.AreEqual(0, layers.Hp(0, 0), "пустая клетка не может быть прочной");
            Assert.AreEqual(5, layers.Hp(0, 1));
        }

        [Test]
        public void ReadsArmourAndHiddenLayers()
        {
            var hp = new[] { "0300", "1100", "0021" };
            var hidden = new[] { "0010", "0100", "0000" };

            var layers = CellLayers.Build(Picture, hp, hidden);

            Assert.AreEqual(3, layers.Hp(0, 1));
            Assert.IsTrue(layers.HasArmoured);

            Assert.IsTrue(layers.IsHidden(0, 2), "клетка (0,2) помечена скрытой");
            Assert.IsFalse(layers.IsHidden(0, 1));
            Assert.IsTrue(layers.HasHidden);
        }

        [Test]
        public void HiddenIsIgnoredOnEmptyCell()
        {
            var hidden = new[] { "1000", "0000", "0000" };
            var layers = CellLayers.Build(Picture, null, hidden);

            Assert.IsFalse(layers.IsHidden(0, 0), "прятать пустую клетку нечего");
            Assert.IsFalse(layers.HasHidden);
        }

        [Test]
        public void ShortAndMissingLayerRowsFallBackToDefaults()
        {
            // Слой короче картинки — законная ситуация: недостающее значит «по умолчанию».
            var hp = new[] { "03" };
            var layers = CellLayers.Build(Picture, hp, null);

            Assert.AreEqual(3, layers.Hp(0, 1));
            Assert.AreEqual(1, layers.Hp(0, 2), "клетка за концом строки слоя — обычная");
            Assert.AreEqual(1, layers.Hp(2, 2), "ряда в слое нет вовсе — обычная");
        }

        [Test]
        public void DamageBreaksOnlyAtZero()
        {
            var layers = CellLayers.Build(Picture, new[] { "0300", "0000", "0000" }, null);

            Assert.IsFalse(layers.Damage(0, 1), "после первого удара клетка ещё цела");
            Assert.AreEqual(2, layers.Hp(0, 1));

            Assert.IsFalse(layers.Damage(0, 1));
            Assert.AreEqual(1, layers.Hp(0, 1));

            Assert.IsTrue(layers.Damage(0, 1), "третий удар обязан разрушить клетку");
            Assert.AreEqual(0, layers.Hp(0, 1));
        }

        [Test]
        public void OrdinaryCellBreaksFromOneHit()
        {
            var layers = CellLayers.Build(Picture, null, null);

            Assert.IsTrue(layers.Damage(0, 1));
        }

        [Test]
        public void RevealReportsOnlyTheFirstTime()
        {
            var layers = CellLayers.Build(Picture, null, new[] { "0100", "0000", "0000" });

            Assert.IsTrue(layers.Reveal(0, 1), "первый вызов открывает клетку");
            Assert.IsFalse(layers.IsHidden(0, 1));

            Assert.IsFalse(layers.Reveal(0, 1), "повторный вызов не должен просить перерисовку");
        }

        [Test]
        public void HpIsClampedToOneDigit()
        {
            // В оригинале прочность доходит до 10, а в строке слоя на клетку одна цифра.
            var layers = CellLayers.Build(new[] { "1" }, new[] { "9" }, null);
            Assert.AreEqual(CellLayers.MaxHp, layers.Hp(0, 0));
        }
    }
}
