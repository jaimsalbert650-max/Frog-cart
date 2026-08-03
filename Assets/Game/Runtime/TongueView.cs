using System;
using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Язык жабы — главный герой экрана (docs/unity-spec/05-feel-anim.md).
    /// Наружу 0.20 c с перелётом до ≈1.05, обратно 0.13 c по p = 1 - u².
    ///
    /// Рот пересчитывается каждый кадр: вагонетка едет, и язык обязан тянуться за ртом,
    /// а не висеть в точке старта.
    /// </summary>
    public sealed class TongueView : MonoBehaviour
    {
        public event Action OnStick;   // долетел: блок пора убирать из сетки
        public event Action OnDone;    // вернулся: блок проглочен

        UILineRenderer _line;
        UILineRenderer _glint;
        Image _tip;
        Image _carried;

        Vector2 _target;
        float _side;
        float _t;
        float _outDuration = 0.20f;
        float _backDuration = 0.13f;
        bool _stuck;

        public bool Active { get; private set; }

        public void Build(RectTransform parent)
        {
            var go = new GameObject("Tongue", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(390f, 844f);

            _line = NewLine("Line", rt, ProcSprite.Hex("E04F6D"));
            _glint = NewLine("Glint", rt, ProcSprite.Hex("FF93AA"));

            _tip = NewImage("Tip", rt, 11f, 11f);
            _tip.sprite = ProcSprite.Circle(11, ProcSprite.Hex("F2657F"),
                                            ProcSprite.Hex("F2657F"), 0f, Color.clear, "tongueTip");

            _carried = NewImage("Carried", rt, 20f, 25f);

            gameObject.SetActive(true);
            Hide();
        }

        public void Fire(Vector2 target, float side, float outDuration, float backDuration)
        {
            _target = target;
            _side = side;
            _outDuration = outDuration;
            _backDuration = backDuration;
            _t = 0f;
            _stuck = false;
            Active = true;

            _line.enabled = true;
            _glint.enabled = true;
            _tip.enabled = true;
        }

        public void SetCarriedBlock(ColorPalette palette, int colorId)
        {
            _carried.sprite = ProcSprite.Make(ProcSprite.Rounded.Flat(
                20, 25, 6f, palette.Get(colorId).baseColor, $"carried{colorId}"));
        }

        public void Stop() => Hide();

        public void UpdateFrame(float dt, Vector2 mouth)
        {
            if (!Active) return;

            _t += dt;
            float p;

            if (_t < _outDuration)
            {
                p = Tweener.EaseOutBack(_t / _outDuration);
            }
            else
            {
                if (!_stuck)
                {
                    _stuck = true;
                    _carried.enabled = true;
                    OnStick?.Invoke();
                }

                float u = (_t - _outDuration) / _backDuration;

                if (u >= 1f)
                {
                    Hide();
                    OnDone?.Invoke();
                    return;
                }

                p = 1f - u * u;
            }

            Draw(mouth, p);
        }

        void Draw(Vector2 mouth, float p)
        {
            Vector2 d = _target - mouth;
            Vector2 perp = new Vector2(-d.y, d.x).normalized;
            Vector2 ctrl = (mouth + _target) * 0.5f + perp * d.magnitude * 0.17f * _side;

            // Рисуется часть кривой от 0 до p — де Кастельжо.
            Vector2 ctrl1 = Vector2.Lerp(mouth, ctrl, p);
            Vector2 tip = Quad(mouth, ctrl, _target, p);

            float thickness = 8f - 2f * p;
            _line.SetCurve(mouth, ctrl1, tip, thickness);
            _glint.SetCurve(mouth, ctrl1, tip, 3f);

            SpecRect.MoveTo(_tip.rectTransform, tip);

            if (_stuck)
            {
                SpecRect.MoveTo(_carried.rectTransform, tip);
                _carried.rectTransform.localScale = Vector3.one * Mathf.Max(0.25f, p);
            }
        }

        void Hide()
        {
            Active = false;
            _stuck = false;

            _line.Hide();
            _glint.Hide();
            _line.enabled = false;
            _glint.enabled = false;
            _tip.enabled = false;
            _carried.enabled = false;
        }

        static Vector2 Quad(Vector2 a, Vector2 ctrl, Vector2 b, float t)
        {
            float mt = 1f - t;
            return mt * mt * a + 2f * mt * t * ctrl + t * t * b;
        }

        static UILineRenderer NewLine(string name, RectTransform parent, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(390f, 844f);

            var line = go.AddComponent<UILineRenderer>();
            line.color = color;
            line.raycastTarget = false;
            return line;
        }

        static Image NewImage(string name, RectTransform parent, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            SpecRect.AnchorCenter(rt);
            rt.sizeDelta = new Vector2(w, h);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
