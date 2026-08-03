using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Вагонетка на контуре. Размеры и цвета — docs/unity-spec/02-art.md.
    /// Локальные координаты: (0,0) — точка касания рельса, -Y — внутрь контура.
    ///
    /// Корпус рисуется поверх жабы, а табличка контр-вращается: число обязано читаться
    /// горизонтально на всех сегментах и никогда не перекрываться (07-checklist.md).
    /// </summary>
    public sealed class CartView : MonoBehaviour
    {
        RectTransform _root;
        RectTransform _plate;
        TMP_Text _countText;
        Image _stripe;
        Image _plateRing;
        CanvasGroup _group;
        Tweener _tweener;

        public RectTransform Root => _root;

        public void Build(RectTransform parent, Tweener tweener, TMP_FontAsset font)
        {
            _tweener = tweener;

            var go = new GameObject("Cart", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            _root = (RectTransform)go.transform;
            SpecRect.AnchorCenter(_root);
            _root.sizeDelta = new Vector2(44f, 44f);
            _group = go.GetComponent<CanvasGroup>();

            // Контактная тень: X -21, Y -5, 42x10.
            var shadow = NewImage("Shadow", _root, 42f, 10f, new Vector2(0f, 5f));
            shadow.sprite = ProcSprite.SoftEllipse(42, 10,
                new Color(45f / 255f, 22f / 255f, 6f / 255f, 0.4f), 3f, "cartShadow");

            // Колёса 12x12 при X -17 и +5, Y -11 — в локальных координатах спеки
            // это смещения от точки касания рельса.
            NewCircleImage("WheelL", _root, 12f, new Vector2(-11f, -5f));
            NewCircleImage("WheelR", _root, 12f, new Vector2(11f, -5f));

            // Корпус 44x27 при X -22, Y -32 → центр на (0, -18.5) от точки касания.
            var body = NewImage("Body", _root, 44f, 27f, new Vector2(0f, -18.5f));
            body.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 44, h = 27,
                radii = new Vector4(6f, 6f, 10f, 10f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("C68F52"),
                c1 = ProcSprite.Hex("9C6231"),
                c2 = ProcSprite.Hex("6F4321"),
                midStop = 0.55f,
                insetTop = 2f,
                insetTopColor = new Color(1f, 1f, 1f, 0.4f),
                insetBottom = 3f,
                insetBottomColor = new Color(0f, 0f, 0f, 0.28f),
                key = "cartBody",
            });

            // Цветная полоса 36x4 внизу корпуса.
            _stripe = NewImage("Stripe", body.rectTransform, 36f, 4f, new Vector2(0f, -9.5f));
            _stripe.sprite = ProcSprite.Make(ProcSprite.Rounded.Flat(36, 4, 2f, Color.white, "stripe"));

            // Табличка 26x18 внутри корпуса.
            _plate = NewImage("Plate", body.rectTransform, 26f, 18f, new Vector2(0f, 1.5f)).rectTransform;
            var plateImage = _plate.GetComponent<Image>();
            plateImage.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 26, h = 18,
                radii = new Vector4(5f, 5f, 5f, 5f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("FFFAEE"),
                c1 = ProcSprite.Hex("F5E9CE"),
                c2 = ProcSprite.Hex("EAD9B4"),
                midStop = 0.5f,
                insetTop = 2f,
                insetTopColor = new Color(1f, 1f, 1f, 0.85f),
                insetBottom = 2f,
                insetBottomColor = new Color(0f, 0f, 0f, 0.15f),
                key = "cartPlate",
            });

            _plateRing = NewImage("PlateRing", _plate, 26f, 18f, Vector2.zero);
            _plateRing.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 26, h = 18,
                radii = new Vector4(5f, 5f, 5f, 5f),
                gradientAngleDeg = 0f,
                c0 = Color.clear, c1 = Color.clear, c2 = Color.clear, midStop = 0.5f,
                outline = 2.5f,
                outlineColor = Color.white,
                key = "cartPlateRing",
            });

            _countText = NewText("Count", _plate, 14f);
        }

        Image NewCircleImage(string name, RectTransform parent, float size, Vector2 offset)
        {
            var image = NewImage(name, parent, size, size, offset);
            image.sprite = ProcSprite.Circle(
                Mathf.RoundToInt(size),
                ProcSprite.Hex("8D949B"), ProcSprite.Hex("3D434A"),
                2f, ProcSprite.Hex("23272C"), $"wheel{size}");
            return image;
        }

        /// <summary>Размещение на контуре. Все аргументы — в spec-координатах.</summary>
        public void Place(Vector2 railPos, float railAngle, float scale, float alpha)
        {
            SpecRect.MoveTo(_root, railPos);
            SpecRect.RotateTo(_root, railAngle);
            _root.localScale = Vector3.one * scale;
            _group.alpha = alpha;

            // Контр-вращение таблички: число всегда горизонтально.
            _plate.localEulerAngles = new Vector3(0f, 0f, railAngle);
        }

        public void SetColor(ColorPalette palette, int colorId)
        {
            var entry = palette.Get(colorId);
            _stripe.color = entry.baseColor;
            _plateRing.color = entry.baseColor;
            _countText.color = entry.dark;
        }

        public void SetCount(int count) => _countText.text = count.ToString();

        /// <summary>«Поп» числа: 1 → 1.42 → 1 за 0.22 c.</summary>
        public void PopNumber(float duration)
        {
            _tweener.Run(duration, Tweener.Linear, t =>
            {
                float scale = t < 0.35f
                    ? Mathf.Lerp(1f, 1.42f, t / 0.35f)
                    : Mathf.Lerp(1.42f, 1f, (t - 0.35f) / 0.65f);
                _plate.localScale = Vector3.one * scale;
            }, () => _plate.localScale = Vector3.one);
        }

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        static Image NewImage(string name, RectTransform parent, float w, float h, Vector2 offset)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = offset;

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        static TMP_Text NewText(string name, RectTransform parent, float size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(26f, 18f);
            rt.anchoredPosition = Vector2.zero;

            var text = go.AddComponent<TextMeshProUGUI>();
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = TextAlignmentOptions.Center;
            text.raycastTarget = false;
            text.textWrappingMode = TextWrappingModes.NoWrap;
            return text;
        }
    }
}
