using UnityEngine;
using UnityEngine.UI;
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
    public sealed class CartView : MonoBehaviour, ICartView
    {
        RectTransform _root;
        RectTransform _plate;
        Text _countText;
        Image _stripe;
        Image _plateRing;
        CanvasGroup _group;
        Tweener _tweener;

        public RectTransform Root => _root;

        public void Build(RectTransform parent, Tweener tweener)
        {
            _tweener = tweener;

            var go = new GameObject("Cart", typeof(RectTransform), typeof(CanvasGroup));
            go.transform.SetParent(parent, false);
            _root = (RectTransform)go.transform;
            SpecRect.AnchorCenter(_root);
            _root.sizeDelta = new Vector2(44f, 44f);
            _group = go.GetComponent<CanvasGroup>();

            // Смещения даны в спеке в её системе (Y вниз, -Y внутрь контура),
            // а здесь локальные координаты uGUI, где Y вверх. Поэтому знак Y
            // инвертирован: «внутрь контура» становится положительным Y.

            // Контактная тень: X -21, Y -5, 42x10 → центр по Y на нуле.
            var shadow = NewImage("Shadow", _root, 42f, 10f, Vector2.zero);
            shadow.sprite = ProcSprite.SoftEllipse(42, 10,
                new Color(45f / 255f, 22f / 255f, 6f / 255f, 0.4f), 3f, "cartShadow");

            // Колёса 12x12 при X -17 и +5, Y -11 → центр по Y на -5 в спеке, +5 здесь.
            NewCircleImage("WheelL", _root, 12f, new Vector2(-11f, 5f));
            NewCircleImage("WheelR", _root, 12f, new Vector2(11f, 5f));

            // Корпус 44x27 при X -22, Y -32 → центр на -18.5 в спеке, +18.5 здесь.
            var body = NewImage("Body", _root, 44f, 27f, new Vector2(0f, 18.5f));
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

            _countText = UiText.Create("Count", _plate, 14, Color.black);
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

        /// <summary>
        /// Пусто намеренно. В плоской раскладке контур занят с самого начала и
        /// пустых мест на нём не бывает — показывать нечего. Та же причина, по
        /// которой плоская сцена не строит табличку отказа.
        /// </summary>
        public void PlaceEmpty(Vector2 railPos, float railAngle) { }

        public void SetColor(ColorPalette palette, int colorId)
        {
            var entry = palette.Get(colorId);
            _stripe.color = entry.baseColor;
            _plateRing.color = entry.baseColor;
            _countText.color = entry.dark;
        }

        public void SetCount(int count)
        {
            _count = count;
            ApplyPlate();
        }

        /// <summary>
        /// Лёд на плоской вагонетке — голубая табличка со счётчиком сколов.
        /// Глыбы, как в объёме, здесь не построить, а цвет таблички читается так же
        /// однозначно: голубая значит не работает.
        /// </summary>
        public void SetFrozen(int count)
        {
            _frozen = Mathf.Max(0, count);
            ApplyPlate();
        }

        public void SetLinked(bool linked) => _linked = linked;

        void ApplyPlate()
        {
            if (_frozen > 0)
            {
                _countText.text = _frozen.ToString();
                _countText.color = ProcSprite.Hex("13415C");
                return;
            }

            _countText.text = _count.ToString();
            _countText.color = _linked ? ProcSprite.Hex("8A6A00") : Color.black;
        }

        int _count;
        int _frozen;
        bool _linked;

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

    }
}
