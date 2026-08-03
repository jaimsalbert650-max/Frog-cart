using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Жаба. Бокс 34x37, pivot — низ-центр (docs/unity-spec/02-art.md).
    ///
    /// Ключевое: жаба НЕ дочерняя к повороту вагонетки. Она всегда стоит вертикально,
    /// иначе на верхнем сегменте контура оказалась бы вверх ногами.
    /// </summary>
    public sealed class FrogView : MonoBehaviour, IFrogView
    {
        RectTransform _root;
        Image _head;
        RectTransform _mouth;
        RectTransform _pupilL, _pupilR;
        CanvasGroup _group;

        float _squash;
        float _clap;
        float _mouthOpen;

        public Vector2 MouthSpecPos { get; private set; }
        public RectTransform Root => _root;

        public void Build(RectTransform parent)
        {
            var go = new GameObject("Frog", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);

            _root = (RectTransform)go.transform;
            SpecRect.AnchorBottomCenter(_root);
            _root.sizeDelta = new Vector2(34f, 37f);
            _group = go.GetComponent<CanvasGroup>();

            // Голова 30x27 при X 2, Y 10 в боксе 34x37 (Y вниз).
            _head = NewImage("Head", _root, 30f, 27f, new Vector2(0f, 13.5f));

            // Глаза 17x17 при X 0 и 17, Y 0 — то есть по верху бокса.
            BuildEye("EyeL", new Vector2(-8.5f, 28.5f), out _pupilL);
            BuildEye("EyeR", new Vector2(8.5f, 28.5f), out _pupilR);

            // Рот 15x9 при X 10, Y 22.
            var mouth = NewImage("Mouth", _root, 15f, 9f, new Vector2(0f, 10.5f));
            mouth.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 15, h = 9,
                radii = new Vector4(2f, 2f, 10f, 10f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("5C2130"),
                c1 = ProcSprite.Hex("77293B"),
                c2 = ProcSprite.Hex("8D3346"),
                midStop = 0.5f,
                key = "frogMouth",
            });
            _mouth = mouth.rectTransform;
        }

        void BuildEye(string name, Vector2 offset, out RectTransform pupil)
        {
            var eye = NewImage(name, _root, 17f, 17f, offset);
            eye.sprite = ProcSprite.Circle(17, Color.white, ProcSprite.Hex("E0E4E9"),
                                           0f, Color.clear, "frogEye");

            var pupilImage = NewImage("Pupil", eye.rectTransform, 8f, 8f, new Vector2(-0.5f, -0.5f));
            pupilImage.sprite = ProcSprite.Circle(8, ProcSprite.Hex("1D2127"),
                                                  ProcSprite.Hex("1D2127"), 0f, Color.clear, "frogPupil");
            pupil = pupilImage.rectTransform;

            var glint = NewImage("Glint", pupil, 6f, 5f, new Vector2(-0.5f, 1.5f));
            glint.sprite = ProcSprite.Circle(6, new Color(1f, 1f, 1f, 0.95f),
                                             new Color(1f, 1f, 1f, 0.95f), 0f, Color.clear, "frogGlint");
        }

        public void SetColor(ColorPalette palette, int colorId)
        {
            var entry = palette.Get(colorId);

            _head.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 30, h = 27,
                radii = new Vector4(15f, 15f, 12f, 12f),
                gradientAngleDeg = 170f,
                c0 = entry.light,
                c1 = entry.baseColor,
                c2 = entry.dark,
                midStop = 0.55f,
                insetTop = 3f,
                insetTopColor = new Color(1f, 1f, 1f, 0.4f),
                insetBottom = 4f,
                insetBottomColor = new Color(0f, 0f, 0f, 0.2f),
                outline = 2f,
                outlineColor = new Color(46f / 255f, 26f / 255f, 10f / 255f, 0.5f),
                key = $"frogHead{colorId}",
            });
        }

        /// <summary>
        /// Позиционирование на рельсе. Формулы — дословно из
        /// docs/unity-spec/06-unity-implementation.md, раздел «Позиционирование жабы».
        /// </summary>
        public void PlaceOnRail(Vector2 railPos, float railAngleDeg, float lift, float scale)
        {
            float ar = railAngleDeg * Mathf.Deg2Rad;
            float sa = Mathf.Abs(Mathf.Sin(ar));
            float ca = Mathf.Abs(Mathf.Cos(ar));

            Vector2 bodyC = new Vector2(
                railPos.x + (18.5f - lift) * Mathf.Sin(ar),
                railPos.y - (18.5f - lift) * Mathf.Cos(ar));

            float halfY = (44f * sa + 27f * ca) * 0.5f;
            Vector2 bottom = new Vector2(bodyC.x, bodyC.y - halfY + 8f);

            SpecRect.MoveTo(_root, bottom);

            // Squash с пивотом по низу: жаба приседает при глотке.
            float sx = (1f + _squash * 0.30f + _clap) * scale;
            float sy = (1f - _squash * 0.26f + _clap) * scale;
            _root.localScale = new Vector3(sx, sy, 1f);
            _root.localEulerAngles = new Vector3(0f, 0f, _clap * 55f);

            _mouth.sizeDelta = new Vector2(15f, Mathf.Lerp(9f, 15f, _mouthOpen));

            MouthSpecPos = new Vector2(bodyC.x, bottom.y - 11f);
        }

        public void SetSquash(float value)
        {
            _squash = value;
            _mouthOpen = value;
        }

        public void SetClap(float value) => _clap = value;

        public void SetAlpha(float value) => _group.alpha = value;

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        /// <summary>Взгляд: 0 — прямо, положительное — вниз, отрицательное — вверх.</summary>
        public void SetGaze(float offsetY)
        {
            _pupilL.anchoredPosition = new Vector2(_pupilL.anchoredPosition.x, -offsetY);
            _pupilR.anchoredPosition = new Vector2(_pupilR.anchoredPosition.x, -offsetY);
        }

        static Image NewImage(string name, RectTransform parent, float w, float h, Vector2 offsetFromBottom)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = offsetFromBottom;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
