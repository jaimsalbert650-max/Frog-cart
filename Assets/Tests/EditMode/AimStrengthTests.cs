using NUnit.Framework;
using UnityEngine;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Сила доворота жабы к цели.
    ///
    /// Правило родилось из двух противоположных жалоб. Слабый доворот на все
    /// стороны — рот смотрит в одну сторону, язык летит в другую, и выстрел
    /// читается как «боком». Полный доворот на все стороны — при цели за спиной
    /// камера сверху видит затылок, а корень языка прячется за головой.
    ///
    /// Компромисс держится на знаке одной скалярной величины, и перепутать в нём
    /// направления — вопрос одного знака минус. Отсюда эти проверки: они дешёвые,
    /// а сломанный знак даёт ровно ту беду, от которой правило и защищает.
    /// </summary>
    public sealed class AimStrengthTests
    {
        static readonly Vector3 ToCamera = new Vector3(0f, 0f, -10f);

        [Test]
        public void Цель_со_стороны_камеры_доворачивает_сильнее_чем_за_спиной()
        {
            float front = Frog3DView.AimStrength(ToCamera, ToCamera);
            float back = Frog3DView.AimStrength(-ToCamera, ToCamera);

            Assert.Greater(front, back,
                "Цель со стороны камеры обязана доворачивать сильнее: там поворот "
              + "ничего не прячет, а за спиной он показывает затылок.");
        }

        [Test]
        public void Доворот_всегда_остаётся_в_своих_пределах()
        {
            // Полный круг направлений: пределы не должны нарушаться ни на одном.
            for (int deg = 0; deg < 360; deg += 15)
            {
                var toTarget = Quaternion.Euler(0f, deg, 0f) * Vector3.forward * 7f;
                float strength = Frog3DView.AimStrength(toTarget, ToCamera);

                Assert.That(strength, Is.InRange(0.55f, 0.95f),
                    $"Направление {deg}° выпало из пределов доворота.");
            }
        }

        [Test]
        public void Нижний_предел_держит_рот_рядом_с_языком()
        {
            // Самый тяжёлый случай — цель точно за спиной. Даже там между ртом и
            // языком должно оставаться меньше прямого угла, иначе выстрел снова
            // читается как «боком». Полный оборот равен 180°, отсюда и пересчёт.
            float worst = Frog3DView.AimStrength(-ToCamera, ToCamera);
            float leftover = 180f * (1f - worst);

            Assert.Less(leftover, 90f,
                $"При цели за спиной рот отстаёт от языка на {leftover:F0}° — "
              + "это и есть выстрел вбок.");
        }

        [Test]
        public void Высота_цели_на_доворот_не_влияет()
        {
            // Поворот идёт только вокруг вертикали, поэтому подъём цели обязан
            // оставить силу прежней — иначе жаба задирала бы морду вверх.
            var flat = new Vector3(3f, 0f, -4f);
            var lifted = new Vector3(3f, 25f, -4f);

            Assert.AreEqual(
                Frog3DView.AimStrength(flat, ToCamera),
                Frog3DView.AimStrength(lifted, ToCamera),
                1e-4f);
        }

        [Test]
        public void Без_направления_берётся_нижний_предел()
        {
            // Камеры может не быть вовсе — в тестах и на сборке без вида. Тогда
            // выбирается осторожный вариант, а не случайный поворот.
            Assert.AreEqual(0.55f, Frog3DView.AimStrength(Vector3.forward, Vector3.zero), 1e-4f);
            Assert.AreEqual(0.55f, Frog3DView.AimStrength(Vector3.zero, ToCamera), 1e-4f);
        }
    }
}
