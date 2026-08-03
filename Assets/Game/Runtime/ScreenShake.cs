using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Тряска всего игрового слоя. Смещения и длительность — из 05-feel-anim.md:
    /// 0.28 c, три кадра (-4,2), (3,-3), (-2,-1).
    /// </summary>
    public sealed class ScreenShake : MonoBehaviour
    {
        static readonly Vector2[] Steps =
        {
            new Vector2(-4f, 2f),
            new Vector2(3f, -3f),
            new Vector2(-2f, -1f),
        };

        RectTransform _target;
        Vector2 _home;
        float _duration;
        float _elapsed;
        bool _running;

        public void Setup(RectTransform target)
        {
            _target = target;
            _home = target.anchoredPosition;
        }

        public void Shake(float duration)
        {
            if (_target == null) return;

            _duration = duration;
            _elapsed = 0f;
            _running = true;
        }

        void Update()
        {
            if (!_running) return;

            _elapsed += Time.unscaledDeltaTime;

            if (_elapsed >= _duration)
            {
                _running = false;
                _target.anchoredPosition = _home;
                return;
            }

            float t = _elapsed / _duration;
            int index = Mathf.Clamp(Mathf.FloorToInt(t * Steps.Length), 0, Steps.Length - 1);

            // Затухание к концу, иначе тряска обрывается рывком.
            float fade = 1f - t;
            _target.anchoredPosition = _home + Steps[index] * fade;
        }
    }
}
