using System.Collections.Generic;
using UnityEngine;

namespace FrogCart.Core
{
    /// <summary>
    /// Замкнутый рельсовый контур вокруг картинки — скруглённый прямоугольник,
    /// заданный осевой линией рельсов (docs/unity-spec/01-layout.md).
    ///
    /// Все координаты — spec-координаты: начало в левом верхнем углу экрана, Y вниз.
    /// Обход против часовой стрелки визуально: верх едет влево, левый бок — вниз,
    /// низ — вправо, правый бок — вверх.
    /// </summary>
    public sealed class LoopPath
    {
        public const float RL = 15f;
        public const float RR = 375f;
        public const float RT = 68f;
        public const float RB = 628f;
        public const float RAD = 48f;

        struct Seg
        {
            public bool Arc;
            public Vector2 A, B, C;
            public float A0, A1;
            public float Len;
        }

        readonly List<Seg> _segs = new List<Seg>(8);

        public float Perimeter { get; }

        public LoopPath()
        {
            AddLine(new Vector2(RR - RAD, RT), new Vector2(RL + RAD, RT));
            AddArc(new Vector2(RL + RAD, RT + RAD), 270f, 180f);
            AddLine(new Vector2(RL, RT + RAD), new Vector2(RL, RB - RAD));
            AddArc(new Vector2(RL + RAD, RB - RAD), 180f, 90f);
            AddLine(new Vector2(RL + RAD, RB), new Vector2(RR - RAD, RB));
            AddArc(new Vector2(RR - RAD, RB - RAD), 90f, 0f);
            AddLine(new Vector2(RR, RB - RAD), new Vector2(RR, RT + RAD));
            AddArc(new Vector2(RR - RAD, RT + RAD), 0f, -90f);

            float sum = 0f;
            foreach (var s in _segs) sum += s.Len;
            Perimeter = sum;
        }

        void AddLine(Vector2 a, Vector2 b)
            => _segs.Add(new Seg { Arc = false, A = a, B = b, Len = Vector2.Distance(a, b) });

        void AddArc(Vector2 c, float a0, float a1)
            => _segs.Add(new Seg { Arc = true, C = c, A0 = a0, A1 = a1, Len = RAD * Mathf.PI * 0.5f });

        /// <summary>
        /// Точка контура на расстоянии s от начала и угол касательной в градусах.
        /// Угол — в экранной системе: положительный поворот выглядит как по часовой стрелке.
        /// </summary>
        public void Sample(float s, out Vector2 pos, out float angleDeg)
        {
            s = Mathf.Repeat(s, Perimeter);

            for (int i = 0; i < _segs.Count; i++)
            {
                var g = _segs[i];

                // Строго >=, иначе точка ровно на стыке уходит в следующий сегмент
                // с u = 1 вместо u = 0, и угол скачет на границе.
                if (s >= g.Len)
                {
                    s -= g.Len;
                    continue;
                }

                float u = g.Len > 0f ? s / g.Len : 0f;

                if (!g.Arc)
                {
                    pos = Vector2.Lerp(g.A, g.B, u);
                    Vector2 d = g.B - g.A;
                    angleDeg = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                }
                else
                {
                    float th = Mathf.Lerp(g.A0, g.A1, u) * Mathf.Deg2Rad;
                    pos = g.C + new Vector2(Mathf.Cos(th), Mathf.Sin(th)) * RAD;
                    // Касательная при убывающем θ: (sin θ, -cos θ).
                    angleDeg = Mathf.Atan2(-Mathf.Cos(th), Mathf.Sin(th)) * Mathf.Rad2Deg;
                }

                return;
            }

            // Досюда доходим только при s, равном периметру с точностью до float.
            pos = new Vector2(RR - RAD, RT);
            angleDeg = 180f;
        }
    }
}
