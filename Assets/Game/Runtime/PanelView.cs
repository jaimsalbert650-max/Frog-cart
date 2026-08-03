using System;
using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Одна панель, три состояния: победа, поражение, пауза.
    /// В панелях нет текста — только иконки (docs/unity-spec/02-art.md, 07-checklist.md).
    /// </summary>
    public sealed class PanelView : MonoBehaviour, IPanelView
    {
        RectTransform _root;
        RectTransform _card;
        Image _dim;
        Image[] _stars;
        Image _heroHead;
        RectTransform _heroMouth;
        RectTransform _heroPupilL, _heroPupilR;
        Image _banBadge;
        RectTransform _retryButton;
        RectTransform _resumeButton;
        Tweener _tween;
        ColorPalette _palette;

        float _bobPhase;

        public void Build(RectTransform parent, ColorPalette palette, Tweener tween,
                          Action onRetry, Action onResume)
        {
            _palette = palette;
            _tween = tween;

            var go = new GameObject("PanelLayer", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            _root = (RectTransform)go.transform;
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            _dim = NewImage("Dim", _root, 0f, 0f, 390f, 844f);
            _dim.sprite = ProcSprite.White();
            _dim.color = new Color(35f / 255f, 18f / 255f, 4f / 255f, 0.62f);
            _dim.raycastTarget = true;

            // Затемнение обязано накрыть весь экран, а не макет.
            //
            // Родитель растянут по прямоугольнику 390x844 — это опорный макет, а не
            // экран. На экране, который шире по пропорции, затемнение выходило узкой
            // вертикальной полосой посередине, и по бокам оставалось яркое дерево.
            // Растягиваем по родителю и добавляем по 1500 единиц во все стороны:
            // столько не бывает ни на одном соотношении сторон.
            var dimRect = _dim.rectTransform;
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.pivot = new Vector2(0.5f, 0.5f);
            dimRect.offsetMin = new Vector2(-1500f, -1500f);
            dimRect.offsetMax = new Vector2(1500f, 1500f);

            var card = NewImage("Card", _root, 50f, 250f, 290f, 340f);
            card.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 290, h = 340,
                radii = new Vector4(30f, 30f, 30f, 30f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("FDF5E2"),
                c1 = ProcSprite.Hex("F7EACE"),
                c2 = ProcSprite.Hex("F0DFBA"),
                midStop = 0.5f,
                insetBottom = 10f,
                insetBottomColor = new Color(90f / 255f, 54f / 255f, 20f / 255f, 0.5f),
                key = "panelCard",
            });
            _card = card.rectTransform;
            SpecRect.AnchorCenter(_card);
            _card.anchoredPosition = new Vector2(195f, -420f);

            // Внутри карточки координаты uGUI: Y вверх, начало в её центре.
            // Порядок сверху вниз — звёзды, герой, кнопки.
            _stars = new Image[3];
            _stars[0] = NewStar(_card, -70f, 104f, 44f);
            _stars[1] = NewStar(_card, 0f, 116f, 58f);
            _stars[2] = NewStar(_card, 70f, 104f, 44f);

            BuildHero();

            _retryButton = NewRoundButton(_card, -46f, -112f, ProcSprite.Hex("FFD52E"),
                                         ProcSprite.Hex("E9A600"), onRetry);
            _retryButton.name = "Retry";

            // Иконка «повторить»: кольцо с разрывом, наклонённое как в спеке.
            var retryIcon = NewChild("Icon", _retryButton, 30f, 30f, Vector2.zero);
            retryIcon.sprite = ProcSprite.Arc(30, 6f, Color.white, 90f, 0f, "iconRetry");
            retryIcon.color = ProcSprite.Hex("7A4D1F");
            retryIcon.rectTransform.localEulerAngles = new Vector3(0f, 0f, -40f);

            // Наконечник на конце дуги. Без него разомкнутое кольцо читается буквой
            // «C», а не стрелкой: направление задаёт именно остриё, и на кнопке
            // в 30 пикселей это единственное, что отличает «повторить» от значка.
            var retryHead = NewChild("Head", _retryButton, 13f, 13f, new Vector2(11f, 7f));
            retryHead.sprite = ProcSprite.Triangle(13, 13, Color.white, "iconRetryHead");
            retryHead.color = ProcSprite.Hex("7A4D1F");
            retryHead.rectTransform.localEulerAngles = new Vector3(0f, 0f, 118f);

            _resumeButton = NewRoundButton(_card, 46f, -112f, ProcSprite.Hex("8FE05A"),
                                           ProcSprite.Hex("4FAE2C"), onResume);
            _resumeButton.name = "Resume";

            var resumeIcon = NewChild("Icon", _resumeButton, 24f, 30f, new Vector2(3f, 0f));
            resumeIcon.sprite = ProcSprite.Triangle(24, 30, Color.white, "iconResume");

            _root.gameObject.SetActive(false);
        }

        void BuildHero()
        {
            var hero = new GameObject("Hero", typeof(RectTransform));
            hero.transform.SetParent(_card, false);

            var rt = (RectTransform)hero.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(96f, 86f);
            rt.anchoredPosition = new Vector2(0f, -6f);

            _heroHead = NewChild("Head", rt, 96f, 86f, Vector2.zero);

            var eyeL = NewChild("EyeL", rt, 30f, 30f, new Vector2(-20f, 18f));
            eyeL.sprite = ProcSprite.Circle(30, Color.white, ProcSprite.Hex("E0E4E9"), 0f, Color.clear, "heroEye");
            _heroPupilL = NewChild("Pupil", eyeL.rectTransform, 14f, 14f, Vector2.zero).rectTransform;
            _heroPupilL.GetComponent<Image>().sprite =
                ProcSprite.Circle(14, ProcSprite.Hex("1D2127"), ProcSprite.Hex("1D2127"), 0f, Color.clear, "heroPupil");

            var eyeR = NewChild("EyeR", rt, 30f, 30f, new Vector2(20f, 18f));
            eyeR.sprite = ProcSprite.Circle(30, Color.white, ProcSprite.Hex("E0E4E9"), 0f, Color.clear, "heroEye");
            _heroPupilR = NewChild("Pupil", eyeR.rectTransform, 14f, 14f, Vector2.zero).rectTransform;
            _heroPupilR.GetComponent<Image>().sprite =
                ProcSprite.Circle(14, ProcSprite.Hex("1D2127"), ProcSprite.Hex("1D2127"), 0f, Color.clear, "heroPupil");

            _heroMouth = NewChild("Mouth", rt, 44f, 22f, new Vector2(0f, -18f)).rectTransform;

            // Знак запрета с белой обводкой.
            //
            // Без обводки он был красным кружком на красной голове — а герой на
            // поражении именно красный, цвет 2. На кадре от него оставался один
            // белый штрих, висящий сбоку от рта непонятно чем. Обводка отделяет
            // значок от головы любого цвета, а не только этого.
            _banBadge = NewChild("Ban", rt, 38f, 38f, new Vector2(34f, -24f));
            _banBadge.sprite = ProcSprite.Circle(38, ProcSprite.Hex("FF5B4E"), ProcSprite.Hex("C9261C"),
                                                 3f, Color.white, "banBadge");

            var slash = NewChild("Slash", _banBadge.rectTransform, 20f, 5f, Vector2.zero);
            slash.sprite = ProcSprite.White();
            slash.color = Color.white;
            slash.rectTransform.localEulerAngles = new Vector3(0f, 0f, 45f);
        }

        public void ShowWin()
        {
            Configure(gold: true, hero: 4, gazeY: -6f, mouthW: 44f, mouthH: 22f,
                      mouthRadii: new Vector4(6f, 6f, 24f, 24f), ban: false, resume: false);
            Appear();
        }

        public void ShowLose()
        {
            Configure(gold: false, hero: 2, gazeY: 5f, mouthW: 36f, mouthH: 12f,
                      mouthRadii: new Vector4(18f, 18f, 4f, 4f), ban: true, resume: false);
            Appear();
        }

        public void ShowPause()
        {
            Configure(gold: true, hero: 4, gazeY: 0f, mouthW: 40f, mouthH: 16f,
                      mouthRadii: new Vector4(10f, 10f, 16f, 16f), ban: false, resume: true);
            Appear();
        }

        public void Hide() => _root.gameObject.SetActive(false);

        void Configure(bool gold, int hero, float gazeY, float mouthW, float mouthH,
                       Vector4 mouthRadii, bool ban, bool resume)
        {
            Color starColor = gold ? ProcSprite.Hex("FFD52E") : ProcSprite.Hex("CBB78F");
            foreach (var star in _stars) star.color = starColor;

            var entry = _palette.Get(hero);
            _heroHead.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 96, h = 86,
                radii = new Vector4(46f, 46f, 36f, 36f),
                gradientAngleDeg = 170f,
                c0 = entry.light, c1 = entry.baseColor, c2 = entry.dark,
                midStop = 0.55f,
                insetTop = 6f,
                insetTopColor = new Color(1f, 1f, 1f, 0.4f),
                outline = 3f,
                outlineColor = new Color(46f / 255f, 26f / 255f, 10f / 255f, 0.5f),
                key = $"heroHead{hero}",
            });

            _heroPupilL.anchoredPosition = new Vector2(0f, -gazeY);
            _heroPupilR.anchoredPosition = new Vector2(0f, -gazeY);

            _heroMouth.sizeDelta = new Vector2(mouthW, mouthH);
            _heroMouth.GetComponent<Image>().sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = Mathf.RoundToInt(mouthW), h = Mathf.RoundToInt(mouthH),
                radii = mouthRadii,
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("5C2130"), c1 = ProcSprite.Hex("77293B"), c2 = ProcSprite.Hex("8D3346"),
                midStop = 0.5f,
                key = $"heroMouth{mouthW}x{mouthH}",
            });

            _banBadge.gameObject.SetActive(ban);
            _resumeButton.gameObject.SetActive(resume);

            // Одна кнопка — по центру, две — по бокам. Иначе Retry на победе и поражении
            // висит слева, будто рядом что-то забыли нарисовать.
            _retryButton.anchoredPosition = new Vector2(resume ? -46f : 0f, -112f);
        }

        void Appear()
        {
            _root.gameObject.SetActive(true);
            _bobPhase = 0f;

            _tween.Run(0.30f, Tweener.Overshoot,
                       t => _card.localScale = Vector3.one * Mathf.Lerp(0.7f, 1f, t));
        }

        void Update()
        {
            if (!_root.gameObject.activeSelf) return;

            // «Дыхание» героя: ±4 px за 1.6 c, бесконечно.
            _bobPhase += Time.unscaledDeltaTime;
            float bob = Mathf.Sin(_bobPhase * Mathf.PI * 2f / 1.6f) * 4f;
            _heroHead.transform.parent.localPosition = new Vector3(0f, bob, 0f);
        }

        static Image NewStar(RectTransform parent, float x, float y, float size)
        {
            var star = NewChild("Star", parent, size, size, new Vector2(x, y));
            int px = Mathf.RoundToInt(size);

            // Белая звезда, которую владелец красит в золото или в серое.
            star.sprite = ProcSprite.Star(px, Color.white, new Color(0.86f, 0.86f, 0.86f, 1f),
                                          2f, new Color(0f, 0f, 0f, 0.22f), $"star{px}");
            return star;
        }

        static RectTransform NewRoundButton(RectTransform parent, float x, float y,
                                            Color top, Color bottom, Action onClick)
        {
            var button = NewChild("Button", parent, 64f, 64f, new Vector2(x, y));
            button.sprite = ProcSprite.Circle(64, top, bottom, 0f, Color.clear,
                                              $"btn{ColorUtility.ToHtmlStringRGB(top)}");
            button.raycastTarget = true;

            var component = button.gameObject.AddComponent<Button>();
            component.targetGraphic = button;
            component.onClick.AddListener(() => onClick?.Invoke());

            return button.rectTransform;
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

        static Image NewChild(string name, RectTransform parent, float w, float h, Vector2 offset)
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
