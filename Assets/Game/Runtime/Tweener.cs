using System;
using System.Collections.Generic;
using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Мини-твинер на float. Animator не используется: по 06-unity-implementation.md
    /// все трансформы обновляются вручную, а коротких твинов много и они однотипные.
    /// </summary>
    public sealed class Tweener : MonoBehaviour
    {
        sealed class Tween
        {
            public float Duration;
            public float Elapsed;
            public Func<float, float> Ease;
            public Action<float> Apply;
            public Action Done;
            public bool Unscaled;
        }

        readonly List<Tween> _tweens = new List<Tween>();
        readonly List<Tween> _finished = new List<Tween>();

        public void Run(float duration, Func<float, float> ease, Action<float> apply,
                        Action done = null, bool unscaled = true)
        {
            if (duration <= 0f)
            {
                apply?.Invoke(1f);
                done?.Invoke();
                return;
            }

            _tweens.Add(new Tween
            {
                Duration = duration,
                Ease = ease ?? Linear,
                Apply = apply,
                Done = done,
                Unscaled = unscaled,
            });

            apply?.Invoke(0f);
        }

        public void CancelAll() => _tweens.Clear();

        void Update()
        {
            if (_tweens.Count == 0) return;

            _finished.Clear();

            for (int i = 0; i < _tweens.Count; i++)
            {
                var t = _tweens[i];
                t.Elapsed += t.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;

                float u = Mathf.Clamp01(t.Elapsed / t.Duration);
                t.Apply?.Invoke(t.Ease(u));

                if (u >= 1f) _finished.Add(t);
            }

            for (int i = 0; i < _finished.Count; i++)
            {
                _tweens.Remove(_finished[i]);
                _finished[i].Done?.Invoke();
            }
        }

        // ── кривые из docs/unity-spec/05-feel-anim.md ────────────────────────────────

        public static float Linear(float x) => x;

        public static float EaseOut(float x) => 1f - (1f - x) * (1f - x);

        public static float EaseIn(float x) => x * x;

        /// <summary>Перелёт до ≈1.05 — формула приведена в спеке дословно.</summary>
        public static float EaseOutBack(float x)
        {
            float k = x - 1f;
            return 1f + 2.1f * k * k * k + 1.05f * k * k;
        }

        /// <summary>Появление панели и въезд вагонетки: 0.7 → 1.04 → 1.</summary>
        public static float Overshoot(float x)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float k = x - 1f;
            return 1f + c3 * k * k * k + c1 * k * k;
        }

        /// <summary>cubic-bezier(.3, 1.4, .5, 1) — сдвиг очереди.</summary>
        public static float QueueShift(float x) => CubicBezier(x, 0.3f, 1.4f, 0.5f, 1f);

        public static float CubicBezier(float x, float x1, float y1, float x2, float y2)
        {
            // Ньютон по X, затем значение по Y. Восьми итераций хватает с запасом.
            float t = x;
            for (int i = 0; i < 8; i++)
            {
                float cx = BezierAxis(t, x1, x2) - x;
                if (Mathf.Abs(cx) < 1e-4f) break;

                float d = BezierSlope(t, x1, x2);
                if (Mathf.Abs(d) < 1e-6f) break;

                t -= cx / d;
            }

            return BezierAxis(Mathf.Clamp01(t), y1, y2);
        }

        static float BezierAxis(float t, float a1, float a2)
        {
            float mt = 1f - t;
            return 3f * mt * mt * t * a1 + 3f * mt * t * t * a2 + t * t * t;
        }

        static float BezierSlope(float t, float a1, float a2)
        {
            float mt = 1f - t;
            return 3f * mt * mt * a1 + 6f * mt * t * (a2 - a1) + 3f * t * t * (1f - a2);
        }
    }
}
