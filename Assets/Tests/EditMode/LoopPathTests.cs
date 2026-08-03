using NUnit.Framework;
using UnityEngine;
using FrogCart.Core;

namespace FrogCart.Tests
{
    public class LoopPathTests
    {
        const float Tol = 0.01f;

        [Test]
        public void Perimeter_MatchesRoundedRectangleFormula()
        {
            var path = new LoopPath();

            // 264 + 464 прямых на сторону, обе стороны, плюс четыре четверти дуги R = 48.
            float expected = 1456f + 2f * Mathf.PI * 48f;

            Assert.AreEqual(expected, path.Perimeter, Tol);
        }

        [Test]
        public void StartOfPath_IsTopRightCornerGoingLeft()
        {
            var path = new LoopPath();
            path.Sample(0f, out var pos, out float angle);

            Assert.AreEqual(327f, pos.x, Tol);
            Assert.AreEqual(68f, pos.y, Tol);
            Assert.AreEqual(180f, Mathf.Abs(angle), Tol, "верхний сегмент едет влево");
        }

        [Test]
        public void SamplingWrapsAroundPerimeter()
        {
            var path = new LoopPath();
            path.Sample(12f, out var a, out float angleA);
            path.Sample(12f + path.Perimeter, out var b, out float angleB);

            Assert.AreEqual(a.x, b.x, Tol);
            Assert.AreEqual(a.y, b.y, Tol);
            Assert.AreEqual(angleA, angleB, Tol);
        }

        [Test]
        public void LeftSegment_GoesDownward()
        {
            var path = new LoopPath();
            float s = 264f + 48f * Mathf.PI * 0.5f + 100f;
            path.Sample(s, out var pos, out float angle);

            Assert.AreEqual(15f, pos.x, Tol, "левый сегмент идёт по X = RL");
            Assert.AreEqual(216f, pos.y, Tol);
            Assert.AreEqual(90f, angle, Tol, "движение вниз по экрану");
        }

        [Test]
        public void SegmentSeamDoesNotJumpTheAngle()
        {
            var path = new LoopPath();

            // Ровно на стыке прямой и дуги угол обязан совпадать с углом чуть раньше.
            path.Sample(264f - 0.001f, out _, out float before);
            path.Sample(264f, out _, out float atSeam);

            // Через DeltaAngle: 180 и -180 — один и тот же угол, а наивное вычитание
            // даёт 360 и ложную тревогу.
            Assert.AreEqual(0f, Mathf.DeltaAngle(before, atSeam), 0.5f,
                "на границе сегментов угол не скачет");
        }

        [Test]
        public void InwardNormalPointsAtThePicture()
        {
            var path = new LoopPath();

            path.Sample(30f, out _, out float top);
            Assert.Greater(Inward(top).y, 0.9f, "сверху внутрь — это вниз по экрану");

            path.Sample(264f + 48f * Mathf.PI * 0.5f + 100f, out _, out float left);
            Assert.Greater(Inward(left).x, 0.9f, "слева внутрь — это вправо по экрану");
        }

        // Локальная -Y вагонетки в spec-координатах.
        // Оси: +X = (cos a, sin a) вдоль движения, +Y = (-sin a, cos a),
        // значит -Y = (sin a, -cos a). Спека проверяет это на верхнем сегменте:
        // a = 180° → -Y = (0, 1), то есть вниз по экрану, внутрь контура.
        static Vector2 Inward(float angleDeg)
        {
            float a = angleDeg * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), -Mathf.Cos(a));
        }
    }
}
