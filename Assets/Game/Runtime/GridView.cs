using UnityEngine;
using UnityEngine.UI;
using FrogCart.Core;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Отрисовка картинки. Ячейки создаются один раз и дальше только меняют спрайт
    /// и цвет — ни одного Instantiate во время игры (замечание о производительности
    /// из docs/unity-spec/06-unity-implementation.md).
    ///
    /// Размер сетки берётся из уровня, а не зашит числами. Спека описывает 14x16 с
    /// ячейкой 22x27, и для своих уровней это остаётся ровно так; но картинка может быть
    /// любой, вплоть до 35x35 из Food Hunt, и тогда ячейка считается под доступную
    /// площадь рамки. Клетки остаются квадратными, сетка центрируется в рамке —
    /// иначе квадратная картинка растянулась бы в портретный прямоугольник.
    /// </summary>
    public sealed class GridView : MonoBehaviour
    {
        /// <summary>Область внутри кремовой рамки, отведённая под картинку.</summary>
        public const float AreaX = 41f;
        public const float AreaY = 132f;
        public const float AreaW = 308f;
        public const float AreaH = 432f;

        /// <summary>Раскладка спеки: 14x16 по 22x27. Ниже используется как эталон пропорции.</summary>
        public const float SpecCellW = 22f;
        public const float SpecCellH = 27f;

        ColorPalette _palette;
        RectTransform _root;
        Tweener _tweener;

        Image[,] _blocks;
        Sprite _socketSprite;
        Sprite[] _blockSprites;

        public int Rows { get; private set; }
        public int Cols { get; private set; }
        public float CellW { get; private set; }
        public float CellH { get; private set; }
        public float OriginX { get; private set; }
        public float OriginY { get; private set; }

        /// <summary>Центр ячейки в spec-координатах.</summary>
        public Vector2 CellCenter(int r, int c)
            => new Vector2(OriginX + c * CellW + CellW * 0.5f,
                           OriginY + r * CellH + CellH * 0.5f);

        public void Build(RectTransform root, ColorPalette palette, Tweener tweener,
                          int rows, int cols)
        {
            _root = root;
            _palette = palette;
            _tweener = tweener;

            Rows = rows;
            Cols = cols;
            Layout();
            BuildSprites();

            _blocks = new Image[Rows, Cols];

            float blockW = CellW - 2f;
            float blockH = CellH - 2f;
            float socketW = blockW * 0.8f;
            float socketH = blockH * 0.76f;

            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                // Гнездо лежит под блоком и видно, когда блок съеден.
                var socket = NewImage($"Socket_{r}_{c}", _root);
                SpecRect.PlaceCentered((RectTransform)socket.transform,
                                       CellCenter(r, c).x, CellCenter(r, c).y, socketW, socketH);
                socket.sprite = _socketSprite;

                var block = NewImage($"Block_{r}_{c}", _root);
                SpecRect.PlaceCentered((RectTransform)block.transform,
                                       CellCenter(r, c).x, CellCenter(r, c).y, blockW, blockH);
                _blocks[r, c] = block;
            }
        }

        /// <summary>
        /// Квадратные клетки, вписанные в область рамки и центрированные в ней.
        /// Для 14x16 это даёт ровно 22x27 из спеки, потому что пропорция та же.
        /// </summary>
        void Layout()
        {
            bool specShape = Rows == 16 && Cols == 14;

            if (specShape)
            {
                CellW = SpecCellW;
                CellH = SpecCellH;
            }
            else
            {
                float cell = Mathf.Min(AreaW / Cols, AreaH / Rows);
                CellW = cell;
                CellH = cell;
            }

            OriginX = AreaX + (AreaW - CellW * Cols) * 0.5f;
            OriginY = AreaY + (AreaH - CellH * Rows) * 0.5f;
        }

        void BuildSprites()
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(CellW - 2f));
            int h = Mathf.Max(4, Mathf.RoundToInt(CellH - 2f));
            float radius = Mathf.Max(2f, Mathf.Min(w, h) * 0.32f);

            _socketSprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = Mathf.Max(4, Mathf.RoundToInt(w * 0.8f)),
                h = Mathf.Max(4, Mathf.RoundToInt(h * 0.76f)),
                radii = new Vector4(radius * 0.7f, radius * 0.7f, radius * 0.7f, radius * 0.7f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("D9C097"),
                c1 = ProcSprite.Hex("E0CAA1"),
                c2 = ProcSprite.Hex("E6D2AC"),
                midStop = 0.45f,
                insetTop = Mathf.Max(1f, h * 0.12f),
                insetTopColor = new Color(0.478f, 0.337f, 0.157f, 0.45f),
                insetBottom = Mathf.Max(1f, h * 0.08f),
                insetBottomColor = new Color(1f, 1f, 1f, 0.6f),
                key = $"socket{w}x{h}",
            });

            _blockSprites = new Sprite[GridModel.MaxColor + 1];

            for (int color = 1; color <= _palette.Count && color <= GridModel.MaxColor; color++)
            {
                var entry = _palette.Get(color);

                _blockSprites[color] = ProcSprite.Make(new ProcSprite.Rounded
                {
                    w = w, h = h,
                    radii = new Vector4(radius, radius, radius, radius),
                    gradientAngleDeg = 168f,
                    c0 = entry.light,
                    c1 = entry.baseColor,
                    c2 = entry.dark,
                    midStop = 0.46f,
                    insetTop = Mathf.Max(1f, h * 0.08f),
                    insetTopColor = new Color(1f, 1f, 1f, 0.5f),
                    insetBottom = Mathf.Max(1f, h * 0.12f),
                    insetBottomColor = new Color(0f, 0f, 0f, 0.22f),
                    key = $"block{color}_{w}x{h}",
                });
            }
        }

        public void SetCell(int r, int c, int colorId)
        {
            var block = _blocks[r, c];

            if (colorId == 0 || colorId >= _blockSprites.Length || _blockSprites[colorId] == null)
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
        /// Негативное дрожание: тап по цвету, для которого нет вагонетки на контуре,
        /// либо по замурованному блоку. 0.34 c, ±3 px и ±5° (05-feel-anim.md).
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

        /// <summary>Силуэт на победе: плоские блоки без градиента.</summary>
        public void ShowSilhouette(string[] rows, bool visible)
        {
            int w = Mathf.Max(4, Mathf.RoundToInt(CellW - 2f));
            int h = Mathf.Max(4, Mathf.RoundToInt(CellH - 2f));
            float radius = Mathf.Max(2f, Mathf.Min(w, h) * 0.32f);

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

                if (colorId == 0 || colorId > _palette.Count) continue;

                block.enabled = true;
                block.sprite = ProcSprite.Make(ProcSprite.Rounded.Flat(
                    w, h, radius, _palette.Get(colorId).baseColor, $"flat{colorId}_{w}x{h}"));
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
