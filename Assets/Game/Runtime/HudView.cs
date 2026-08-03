using System;
using UnityEngine;
using UnityEngine.UI;

namespace FrogCart.Runtime
{
    /// <summary>HUD: номер уровня слева, прогресс с процентом по центру, пауза справа.</summary>
    public sealed class HudView : MonoBehaviour, IHudView
    {
        Text _levelText;
        Text _percentText;
        RectTransform _fill;
        Tweener _tween;

        float _shown;

        const float TrackX = 64f;
        const float TrackW = 262f;

        public void Build(RectTransform parent, Tweener tween, Action onPause)
        {
            _tween = tween;

            var strip = NewImage("HudStrip", parent, 0f, 0f, 390f, 60f);
            strip.sprite = ProcSprite.White();
            strip.color = new Color(40f / 255f, 20f / 255f, 6f / 255f, 0.42f);

            // Бейдж уровня 38x38 R 12.
            var badge = NewImage("LevelBadge", parent, 14f, 11f, 38f, 38f);
            badge.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 38, h = 38,
                radii = new Vector4(12f, 12f, 12f, 12f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("FDF5E2"),
                c1 = ProcSprite.Hex("F4E4C4"),
                c2 = ProcSprite.Hex("E9D3A9"),
                midStop = 0.5f,
                insetBottom = 3f,
                insetBottomColor = new Color(90f / 255f, 54f / 255f, 20f / 255f, 0.5f),
                key = "hudBadge",
            });
            _levelText = UiText.Create("LevelText", badge.rectTransform, 19, ProcSprite.Hex("7A4D1F"));

            // Прогресс-бар: дорожка и заливка, высота 22, радиус 11.
            var track = NewImage("Track", parent, TrackX, 19f, TrackW, 22f);
            track.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 262, h = 22,
                radii = new Vector4(11f, 11f, 11f, 11f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("5D3A19"),
                c1 = ProcSprite.Hex("6C4620"),
                c2 = ProcSprite.Hex("7C5027"),
                midStop = 0.5f,
                insetTop = 3f,
                insetTopColor = new Color(0f, 0f, 0f, 0.35f),
                key = "hudTrack",
            });

            var fill = NewImage("Fill", parent, TrackX, 19f, 0f, 22f);
            fill.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 262, h = 22,
                radii = new Vector4(11f, 11f, 11f, 11f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("8FE05A"),
                c1 = ProcSprite.Hex("6FC744"),
                c2 = ProcSprite.Hex("4FAE2C"),
                midStop = 0.5f,
                insetTop = 2f,
                insetTopColor = new Color(1f, 1f, 1f, 0.45f),
                key = "hudFill",
            });
            fill.type = Image.Type.Sliced;
            _fill = fill.rectTransform;

            _percentText = UiText.Create("Percent", track.rectTransform, 12, Color.white);

            // Кнопка паузы 38x38 с двумя полосками 5x16.
            var pause = NewImage("PauseButton", parent, 338f, 11f, 38f, 38f);
            pause.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 38, h = 38,
                radii = new Vector4(12f, 12f, 12f, 12f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("FDF5E2"),
                c1 = ProcSprite.Hex("F4E4C4"),
                c2 = ProcSprite.Hex("E9D3A9"),
                midStop = 0.5f,
                insetBottom = 3f,
                insetBottomColor = new Color(90f / 255f, 54f / 255f, 20f / 255f, 0.5f),
                key = "hudBadge",
            });
            pause.raycastTarget = true;

            NewBar(pause.rectTransform, -3.5f);
            NewBar(pause.rectTransform, 3.5f);

            var button = pause.gameObject.AddComponent<Button>();
            button.targetGraphic = pause;
            button.onClick.AddListener(() => onPause?.Invoke());
        }

        static void NewBar(RectTransform parent, float offsetX)
        {
            var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(parent, false);

            var rt = (RectTransform)bar.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(5f, 16f);
            rt.anchoredPosition = new Vector2(offsetX, 0f);

            var image = bar.GetComponent<Image>();
            image.sprite = ProcSprite.White();
            image.color = ProcSprite.Hex("7A4D1F");
            image.raycastTarget = false;
        }

        public void SetLevel(int level) => _levelText.text = level.ToString();

        public void SetProgress(float value, bool instant)
        {
            value = Mathf.Clamp01(value);

            if (instant)
            {
                _shown = value;
                Apply(value);
                return;
            }

            float from = _shown;
            _shown = value;

            _tween.Run(0.18f, Tweener.EaseOut, t => Apply(Mathf.Lerp(from, value, t)));
        }

        void Apply(float value)
        {
            _fill.sizeDelta = new Vector2(TrackW * value, 22f);
            _percentText.text = Mathf.RoundToInt(value * 100f) + "%";
        }

        static Image NewImage(string name, RectTransform parent, float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            SpecRect.Place((RectTransform)go.transform, x, y, w, h);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

    }
}
