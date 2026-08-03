using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// 46 частиц конфетти по параметрам docs/unity-spec/02-art.md.
    /// Пул создаётся один раз; во время игры Instantiate не вызывается.
    /// </summary>
    public sealed class ConfettiBurst : MonoBehaviour
    {
        const int Count = 46;

        struct Particle
        {
            public RectTransform Rt;
            public Vector2 Start;
            public float Dx;
            public float Rotation;
            public float Duration;
            public float Delay;
            public float Elapsed;
        }

        readonly Particle[] _particles = new Particle[Count];
        bool _playing;

        public void Build(RectTransform parent, ColorPalette palette)
        {
            var random = new System.Random(20260803);

            for (int i = 0; i < Count; i++)
            {
                var go = new GameObject($"Confetti_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);

                float w = 8f + (float)random.NextDouble() * 7f;
                float h = 9f + (float)random.NextDouble() * 10f;
                float radius = random.Next(2) == 0 ? 2f : 5f;

                var rt = (RectTransform)go.transform;
                SpecRect.AnchorCenter(rt);
                rt.sizeDelta = new Vector2(w, h);

                var image = go.GetComponent<Image>();
                image.raycastTarget = false;
                image.sprite = ProcSprite.Make(ProcSprite.Rounded.Flat(
                    Mathf.RoundToInt(w), Mathf.RoundToInt(h), radius, Color.white,
                    $"confetti{Mathf.RoundToInt(w)}x{Mathf.RoundToInt(h)}r{radius}"));
                image.color = PickColor(palette, random);

                _particles[i] = new Particle
                {
                    Rt = rt,
                    Start = new Vector2((float)random.NextDouble() * 370f, -30f),
                    Dx = -90f + (float)random.NextDouble() * 180f,
                    Rotation = -540f + (float)random.NextDouble() * 1080f,
                    Duration = 1.5f + (float)random.NextDouble() * 1.1f,
                    Delay = (float)random.NextDouble() * 0.5f,
                };

                go.SetActive(false);
            }
        }

        static Color PickColor(ColorPalette palette, System.Random random)
        {
            int roll = random.Next(11);
            if (roll == 10) return ProcSprite.Hex("FFF5D6");

            var entry = palette.Get(roll / 2 + 1);
            return roll % 2 == 0 ? entry.baseColor : entry.light;
        }

        public void Play()
        {
            for (int i = 0; i < Count; i++)
            {
                _particles[i].Elapsed = 0f;
                _particles[i].Rt.gameObject.SetActive(true);
                SpecRect.MoveTo(_particles[i].Rt, _particles[i].Start);
            }

            _playing = true;
        }

        public void StopAndHide()
        {
            _playing = false;
            for (int i = 0; i < Count; i++) _particles[i].Rt.gameObject.SetActive(false);
        }

        void Update()
        {
            if (!_playing) return;

            bool anyAlive = false;

            for (int i = 0; i < Count; i++)
            {
                ref var p = ref _particles[i];
                p.Elapsed += Time.unscaledDeltaTime;

                float local = p.Elapsed - p.Delay;
                if (local < 0f) { anyAlive = true; continue; }

                float t = local / p.Duration;
                if (t >= 1f) { p.Rt.gameObject.SetActive(false); continue; }

                anyAlive = true;

                float eased = Tweener.CubicBezier(t, 0.35f, 0.6f, 0.6f, 1f);
                SpecRect.MoveTo(p.Rt, p.Start + new Vector2(p.Dx * eased, 640f * eased));
                p.Rt.localEulerAngles = new Vector3(0f, 0f, p.Rotation * eased);
            }

            if (!anyAlive) _playing = false;
        }
    }
}
