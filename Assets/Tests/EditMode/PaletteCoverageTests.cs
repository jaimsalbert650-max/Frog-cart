using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using FrogCart.Core;
using FrogCart.Data;

namespace FrogCart.Tests
{
    /// <summary>
    /// Палитра обязана покрывать все допустимые цвета.
    ///
    /// Тест написан по следу реальной поломки. Палитра в справочном проекте
    /// отстала на четыре цвета, уровень требовал семь — и `Get(6)` бросал
    /// IndexOutOfRange посреди сборки уровня. Исключение обрывало BuildLevel:
    /// очередь оставалась ненастроенной белой, HUD пустым, а блоки лишних цветов
    /// не появлялись на доске вовсе. По виду это выглядело тремя независимыми
    /// поломками, и час ушёл на то, чтобы связать их с одной причиной.
    ///
    /// Проверка дешёвая и ловит расхождение до запуска игры.
    /// </summary>
    public sealed class PaletteCoverageTests
    {
        static ColorPalette LoadPalette()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:ColorPalette"))
            {
                var palette = AssetDatabase.LoadAssetAtPath<ColorPalette>(
                    AssetDatabase.GUIDToAssetPath(guid));

                if (palette != null) return palette;
            }

            return null;
        }

        [Test]
        public void PaletteCoversEveryAllowedColor()
        {
            var palette = LoadPalette();
            Assert.IsNotNull(palette, "палитра не найдена в проекте");

            Assert.GreaterOrEqual(palette.Count, GridModel.MaxColor,
                $"в палитре {palette.Count} цветов, а уровень может требовать "
              + $"{GridModel.MaxColor}. Недостающие цвета сделают блоки невидимыми, "
              + "а обращение за ними оборвёт сборку уровня.");
        }

        [Test]
        public void EveryColorIsDistinct()
        {
            var palette = LoadPalette();
            Assert.IsNotNull(palette);

            for (int a = 1; a <= palette.Count; a++)
            for (int b = a + 1; b <= palette.Count; b++)
            {
                Color first = palette.Get(a).baseColor;
                Color second = palette.Get(b).baseColor;

                float distance = Mathf.Abs(first.r - second.r)
                               + Mathf.Abs(first.g - second.g)
                               + Mathf.Abs(first.b - second.b);

                Assert.Greater(distance, 0.25f,
                    $"цвета {a} и {b} почти одинаковы: игрок не отличит их на доске");
            }
        }

        [Test]
        public void OutOfRangeColorDoesNotThrow()
        {
            var palette = LoadPalette();
            Assert.IsNotNull(palette);

            // Get на неверном номере обязан написать ошибку — в этом и смысл,
            // чтобы расхождение было заметным. Раннер считает любую незаявленную
            // ошибку провалом, поэтому здесь она разрешена явно. Ждать конкретный
            // текст нельзя: предупреждение выводится один раз на домен, и порядок
            // тестов решает, достанется оно этому тесту или соседнему.
            UnityEngine.TestTools.LogAssert.ignoreFailingMessages = true;

            try
            {
                // Ронять сборку уровня нельзя даже на испорченных данных: пусть цвет
                // будет неверным, но игра обязана подняться и сказать, что не так.
                Assert.DoesNotThrow(() => palette.Get(palette.Count + 5));
                Assert.DoesNotThrow(() => palette.Get(0));
                Assert.DoesNotThrow(() => palette.Get(-3));
            }
            finally
            {
                UnityEngine.TestTools.LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
