using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Док с очередью внизу экрана. Пять мини-вагонеток, шаг 88 px —
    /// пятая намеренно уезжает за правый край, очередь «обрезана» экраном
    /// (docs/unity-spec/01-layout.md).
    /// </summary>
    public sealed class QueueView : MonoBehaviour, IQueueView
    {
        const int Visible = 5;
        const float SlotY = 704f;
        const float Step = 88f;
        const float FirstX = 8f;

        sealed class Mini
        {
            public RectTransform Root;
            public Image Stripe;
            public Image Head;
            public Text Count;
        }

        readonly Mini[] _minis = new Mini[Visible];
        ColorPalette _palette;
        Tweener _tween;

        public void Build(RectTransform parent, ColorPalette palette, Tweener tween)
        {
            _palette = palette;
            _tween = tween;

            // Панель дока: X 0, Y 648, W 390, H 196, верхние углы 26.
            var panel = NewImage("DockPanel", parent, 0f, 648f, 390f, 196f);
            panel.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 390, h = 196,
                radii = new Vector4(26f, 26f, 0f, 0f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("9A6537"),
                c1 = ProcSprite.Hex("8A5A30"),
                c2 = ProcSprite.Hex("70482A"),
                midStop = 0.4f,
                insetTop = 3f,
                insetTopColor = new Color(1f, 1f, 1f, 0.18f),
                key = "dockPanel",
            });

            // Рельс дока: планка Y 798 H 9 и металл Y 802 X 14 W 362 H 4.
            var plank = NewImage("DockPlank", parent, 0f, 798f, 390f, 9f);
            plank.sprite = ProcSprite.White();
            plank.color = ProcSprite.Hex("5F3919");

            var metal = NewImage("DockMetal", parent, 14f, 802f, 362f, 4f);
            metal.sprite = ProcSprite.White();
            metal.color = ProcSprite.Hex("CFD6DA");

            for (int i = 0; i < Visible; i++)
                _minis[i] = BuildMini(parent, i);
        }

        Mini BuildMini(RectTransform parent, int index)
        {
            var root = NewImage($"QueueCart_{index}", parent,
                                FirstX + index * Step, SlotY, 78f, 96f);
            root.enabled = false;

            var mini = new Mini { Root = root.rectTransform };

            var shadow = NewChild("Shadow", mini.Root, 12f, 88f, 56f, 11f);
            shadow.sprite = ProcSprite.SoftEllipse(56, 11,
                new Color(45f / 255f, 22f / 255f, 6f / 255f, 0.4f), 3f, "miniShadow");

            NewWheel(mini.Root, 8f);
            NewWheel(mini.Root, 46f);

            // Жаба сверху: X 15, Y 14, бокс 40x42; голова 38x31 при X 1, Y 11.
            mini.Head = NewChild("Head", mini.Root, 16f, 25f, 38f, 31f);
            NewEye(mini.Root, 15f);
            NewEye(mini.Root, 37f);

            var body = NewChild("Body", mini.Root, 4f, 52f, 62f, 34f);
            body.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 62, h = 34,
                radii = new Vector4(7f, 7f, 12f, 12f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("C68F52"),
                c1 = ProcSprite.Hex("9C6231"),
                c2 = ProcSprite.Hex("6F4321"),
                midStop = 0.55f,
                insetTop = 2f,
                insetTopColor = new Color(1f, 1f, 1f, 0.4f),
                insetBottom = 3f,
                insetBottomColor = new Color(0f, 0f, 0f, 0.28f),
                key = "miniBody",
            });

            var plate = NewChild("Plate", mini.Root, 13f, 58f, 36f, 23f);
            plate.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 36, h = 23,
                radii = new Vector4(6f, 6f, 6f, 6f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("FFFAEE"),
                c1 = ProcSprite.Hex("F5E9CE"),
                c2 = ProcSprite.Hex("EAD9B4"),
                midStop = 0.5f,
                key = "miniPlate",
            });

            mini.Count = UiText.Create("Count", plate.rectTransform, 18, Color.black);

            mini.Stripe = NewChild("Stripe", mini.Root, 6f, 80f, 50f, 5f);
            mini.Stripe.sprite = ProcSprite.Make(ProcSprite.Rounded.Flat(50, 5, 2f, Color.white, "miniStripe"));

            return mini;
        }

        static void NewWheel(RectTransform parent, float x)
        {
            var wheel = NewChild("Wheel", parent, x, 78f, 15f, 15f);
            wheel.sprite = ProcSprite.Circle(15, ProcSprite.Hex("8D949B"), ProcSprite.Hex("3D434A"),
                                             2f, ProcSprite.Hex("23272C"), "miniWheel");
        }

        static void NewEye(RectTransform parent, float x)
        {
            var eye = NewChild("Eye", parent, x, 14f, 18f, 18f);
            eye.sprite = ProcSprite.Circle(18, Color.white, ProcSprite.Hex("E0E4E9"), 0f, Color.clear, "miniEye");

            var pupil = NewChild("Pupil", eye.rectTransform, 5f, 5f, 8f, 8f);
            pupil.sprite = ProcSprite.Circle(8, ProcSprite.Hex("1D2127"), ProcSprite.Hex("1D2127"),
                                             0f, Color.clear, "frogPupil");
        }

        public void Rebuild(List<LevelData.CartDef> queue, int startIndex)
        {
            for (int i = 0; i < Visible; i++)
            {
                int source = startIndex + i;
                bool has = source < queue.Count;

                _minis[i].Root.gameObject.SetActive(has);
                SpecRect.Place(_minis[i].Root, FirstX + i * Step, SlotY, 78f, 96f);

                if (!has) continue;

                var def = queue[source];
                var entry = _palette.Get(def.colorId);

                _minis[i].Stripe.color = entry.baseColor;
                _minis[i].Count.color = entry.dark;
                _minis[i].Count.text = def.capacity.ToString();
                _minis[i].Head.sprite = ProcSprite.Make(new ProcSprite.Rounded
                {
                    w = 38, h = 31,
                    radii = new Vector4(16f, 16f, 12f, 12f),
                    gradientAngleDeg = 170f,
                    c0 = entry.light, c1 = entry.baseColor, c2 = entry.dark,
                    midStop = 0.55f,
                    insetTop = 3f,
                    insetTopColor = new Color(1f, 1f, 1f, 0.4f),
                    outline = 2f,
                    outlineColor = new Color(46f / 255f, 26f / 255f, 10f / 255f, 0.5f),
                    key = $"miniHead{def.colorId}",
                });
            }
        }

        /// <summary>
        /// Плоская версия выезд не показывает.
        ///
        /// Пустой метод здесь честнее заглушки с анимацией: в плоской раскладке
        /// очередь и контур лежат в одной плоскости и вагонетка «выезжала» бы
        /// скольжением по экрану, а не выездом. Объём для того и делался.
        /// </summary>
        public float Depart(int index, Vector2 toSpec, float toAngleDeg, float duration) => 0f;

        /// <summary>Сдвиг очереди влево за 0.30 c с ease back, затем пересборка.</summary>
        public void Shift(List<LevelData.CartDef> queue, int startIndex, float duration)
        {
            var from = new float[Visible];
            for (int i = 0; i < Visible; i++) from[i] = _minis[i].Root.anchoredPosition.x;

            _tween.Run(duration, Tweener.QueueShift,
                t =>
                {
                    for (int i = 0; i < Visible; i++)
                    {
                        float target = FirstX + i * Step - Step;
                        float x = Mathf.Lerp(from[i], target, t);
                        _minis[i].Root.anchoredPosition = new Vector2(x, _minis[i].Root.anchoredPosition.y);
                    }
                },
                () => Rebuild(queue, startIndex));
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

        static Image NewChild(string name, RectTransform parent, float x, float y, float w, float h)
            => NewImage(name, parent, x, y, w, h);

    }
}
