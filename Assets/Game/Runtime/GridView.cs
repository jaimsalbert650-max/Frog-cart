using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Отрисовка картинки. 224 ячейки создаются один раз и дальше только меняют спрайт
    /// и цвет — ни одного Instantiate во время игры (замечание о производительности
    /// из docs/unity-spec/06-unity-implementation.md).
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        public const int Cols = 14;
        public const int Rows = 16;
        public const float CW = 22f;
        public const float CH = 27f;
        public const float GX = 41f;
        public const float GY = 132f;

        ColorPalette _palette;
        RectTransform _root;
        Tweener _tweener;

        Image[,] _blocks;
        Image[,] _sockets;
        Sprite _socketSprite;
        readonly Sprite[] _blockSprites = new Sprite[GridModelColors + 1];

        const int GridModelColors = 5;

        /// <summary>Центр ячейки в spec-координатах.</summary>
        public static Vector2 CellCenter(int r, int c)
            => new Vector2(GX + c * CW + CW * 0.5f, GY + r * CH + CH * 0.5f);

        public void Build(RectTransform root, ColorPalette palette, Tweener tweener)
        {
            _root = root;
            _palette = palette;
            _tweener = tweener;

            BuildSprites();

            _blocks = new Image[Rows, Cols];
            _sockets = new Image[Rows, Cols];

            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                // Гнездо лежит под блоком и видно, когда блок съеден.
                var socket = NewImage($"Socket_{r}_{c}", _root);
                SpecRect.Place((RectTransform)socket.transform,
                               c * CW + 3f, r * CH + 4f, 16f, 19f);
                socket.sprite = _socketSprite;
                _sockets[r, c] = socket;

                var block = NewImage($"Block_{r}_{c}", _root);
                SpecRect.PlaceCentered((RectTransform)block.transform,
                                       c * CW + 1f + 10f, r * CH + 1f + 12.5f, 20f, 25f);
                _blocks[r, c] = block;
            }
        }

        void BuildSprites()
        {
            _socketSprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 16, h = 19,
                radii = new Vector4(5f, 5f, 5f, 5f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("D9C097"),
                c1 = ProcSprite.Hex("E0CAA1"),
                c2 = ProcSprite.Hex("E6D2AC"),
                midStop = 0.45f,
                insetTop = 3f,
                insetTopColor = new Color(0.478f, 0.337f, 0.157f, 0.45f),
                insetBottom = 2f,
                insetBottomColor = new Color(1f, 1f, 1f, 0.6f),
                key = "socket",
            });

            for (int color = 1; color <= GridModelColors; color++)
            {
                var entry = _palette.Get(color);

                _blockSprites[color] = ProcSprite.Make(new ProcSprite.Rounded
                {
                    w = 20, h = 25,
                    radii = new Vector4(7f, 7f, 7f, 7f),
                    gradientAngleDeg = 168f,
                    c0 = entry.light,
                    c1 = entry.baseColor,
                    c2 = entry.dark,
                    midStop = 0.46f,
                    insetTop = 2f,
                    insetTopColor = new Color(1f, 1f, 1f, 0.5f),
                    insetBottom = 3f,
                    insetBottomColor = new Color(0f, 0f, 0f, 0.22f),
                    key = $"block{color}",
                });
            }
        }

        public void SetCell(int r, int c, int colorId)
        {
            var block = _blocks[r, c];

            if (colorId == 0)
            {
                block.enabled = false;
                return;
            }

            block.enabled = true;
            block.sprite = _blockSprites[colorId];
            block.color = Color.white;
            ((RectTransform)block.transform).localScale = Vector3.one;
            ((RectTransform)block.transform).localEulerAngles = Vector3.zero;
        }

        /// <summary>
        /// Негативное дрожание: тап по цвету, для которого нет вагонетки на контуре.
        /// 0.34 c, ±3 px и ±5° туда-обратно (05-feel-anim.md).
        /// </summary>
        public void Wobble(int r, int c, float duration)
        {
            var rt = (RectTransform)_blocks[r, c].transform;
            Vector2 home = rt.anchoredPosition;

            _tweener.Run(duration, Tweener.Linear, t =>
            {
                float wave = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t);
                rt.anchoredPosition = home + new Vector2(3f * wave, 0f);
                rt.localEulerAngles = new Vector3(0f, 0f, 5f * wave);
            }, () =>
            {
                rt.anchoredPosition = home;
                rt.localEulerAngles = Vector3.zero;
            });
        }

        /// <summary>Силуэт на победе: плоские блоки без градиента, с внутренней обводкой.</summary>
        public void ShowSilhouette(string[] rows, bool visible)
        {
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                int colorId = rows[r][c] - '0';
                var block = _blocks[r, c];

                if (!visible)
                {
                    if (colorId != 0) block.enabled = false;
                    continue;
                }

                if (colorId == 0) continue;

                block.enabled = true;
                block.sprite = ProcSprite.Make(ProcSprite.Rounded.Flat(
                    20, 25, 7f, _palette.Get(colorId).baseColor, $"flat{colorId}"));
                block.color = Color.white;
            }
        }

        static Image NewImage(string name, RectTransform parent)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
