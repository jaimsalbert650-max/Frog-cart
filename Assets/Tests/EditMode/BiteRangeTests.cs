using NUnit.Framework;
using UnityEngine;
using FrogCart.Core;
using FrogCart.Data;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Радиус укуса обязан покрывать всю доску с контура.
    ///
    /// Ограничение радиуса — правка подачи: язык через всю картинку читался как
    /// выстрел из ниоткуда. Но у неё есть цена, и она не косметическая. Контур
    /// окружает доску снаружи, поэтому середина картинки — самая далёкая от
    /// рельса точка. Стоит радиусу опуститься ниже этого расстояния, и центр
    /// нельзя съесть ни с одной точки контура: победа недостижима, проигрыш не
    /// наступает, уровень просто встаёт.
    ///
    /// Проверка арифметическая и потому дешёвая: контур сэмплируется часто,
    /// расстояние берётся до ближайшей точки.
    /// </summary>
    public class BiteRangeTests
    {
        GameConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<GameConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void BiteRangeReachesBoardCentre()
        {
            // Центр площадки под картинку — строго худшая точка: картинка
            // вписывается в площадку и центрируется в ней, значит дальше от
            // рельса, чем центр площадки, не окажется ни одна клетка.
            var centre = new Vector2(Grid3DView.AreaX + Grid3DView.AreaW * 0.5f,
                                     Grid3DView.AreaY + Grid3DView.AreaH * 0.5f);

            float distance = DistanceToLoop(centre);

            Assert.LessOrEqual(distance, _config.biteRange,
                $"центр доски в {distance:F1} от ближайшей точки контура, а радиус "
              + $"укуса {_config.biteRange:F1}. Середину картинки не достать ни одной "
              + "вагонеткой: уровень встанет, не дойдя до победы");
        }

        [Test]
        public void EveryCornerOfTheBoardIsReachable()
        {
            var corners = new[]
            {
                new Vector2(Grid3DView.AreaX, Grid3DView.AreaY),
                new Vector2(Grid3DView.AreaX + Grid3DView.AreaW, Grid3DView.AreaY),
                new Vector2(Grid3DView.AreaX, Grid3DView.AreaY + Grid3DView.AreaH),
                new Vector2(Grid3DView.AreaX + Grid3DView.AreaW,
                            Grid3DView.AreaY + Grid3DView.AreaH),
            };

            foreach (var corner in corners)
                Assert.LessOrEqual(DistanceToLoop(corner), _config.biteRange,
                    $"угол доски {corner} вне досягаемости");
        }

        [Test]
        public void RangeStaysShorterThanTheBoardItself()
        {
            // Смысл правки в том, что язык больше не тянется через всю фигуру.
            // Если радиус дорастёт до диагонали доски, ограничение перестанет
            // что-либо значить, и проверка об этом скажет.
            float diagonal = new Vector2(Grid3DView.AreaW, Grid3DView.AreaH).magnitude;

            Assert.Less(_config.biteRange, diagonal * 0.5f,
                $"радиус {_config.biteRange:F1} против диагонали доски {diagonal:F1}: "
              + "так язык снова дотянется через всю картинку");
        }

        /// <summary>Расстояние до ближайшей точки контура.</summary>
        static float DistanceToLoop(Vector2 point)
        {
            var loop = new LoopPath();
            float best = float.MaxValue;

            // Шаг в половину spec-единицы: контур длиной около 1700, то есть
            // порядка трёх с половиной тысяч проб — для редакторного теста даром.
            for (float s = 0f; s < loop.Perimeter; s += 0.5f)
            {
                loop.Sample(s, out var pos, out _);
                best = Mathf.Min(best, Vector2.Distance(point, pos));
            }

            return best;
        }
    }
}
