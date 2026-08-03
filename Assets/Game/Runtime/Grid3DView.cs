using UnityEngine;
using FrogCart.Core;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Картинка объёмом: каждая клетка — брусок со скруглёнными рёбрами, стоящий
    /// на плоскости XZ. Объём даёт настоящая геометрия и свет, а не имитация
    /// градиентами, как в плоской версии.
    ///
    /// Меш один на всю доску и берётся из кеша, материал один на цвет — иначе
    /// на 1225 блоках это были бы 1225 мешей и столько же материалов.
    ///
    /// Пустых гнёзд под клетками нет намеренно. Раньше под каждой клеткой лежала
    /// плоская площадка, и вся доска читалась как перфорированная панель, а на
    /// референсе пустая клетка — просто поверхность площадки. Съеденный блок теперь
    /// не оставляет следа, и картинка разбирается «до чистого листа».
    /// </summary>
    public sealed class Grid3DView : MonoBehaviour, IGridView
    {
        /// <summary>
        /// Область под картинку. Подобрана так, чтобы белая карточка целиком
        /// помещалась внутри контура рельсов и не уезжала под него краями.
        ///
        /// Контур идёт по x 15 и 375, полотно шириной 26 занимает по 13 в каждую
        /// сторону, значит свободно x от 28 до 362. Карточка — это картинка плюс
        /// поле 24 с каждой стороны, отсюда предел ширины картинки в 276.
        /// По вертикали запас больше: контур сверху 68, снизу 628.
        /// </summary>
        public const float AreaX = 57f;
        public const float AreaY = 111f;
        public const float AreaW = 276f;
        public const float AreaH = 474f;

        Transform _root;
        ColorPalette _palette;
        Tweener _tweener;

        Transform[,] _blocks;
        MeshRenderer[,] _renderers;
        Material[] _materials;
        Mesh _blockMesh;
        float _blockHeight;

        public int Rows { get; private set; }
        public int Cols { get; private set; }
        public float CellW { get; private set; }
        public float CellH { get; private set; }
        public float OriginX { get; private set; }
        public float OriginY { get; private set; }

        public Vector2 CellCenter(int r, int c)
            => new Vector2(OriginX + c * CellW + CellW * 0.5f,
                           OriginY + r * CellH + CellH * 0.5f);

        public void Build(Transform parent, ColorPalette palette, Tweener tweener,
                          int rows, int cols)
        {
            _root = new GameObject("Board").transform;
            _root.SetParent(parent, false);

            _palette = palette;
            _tweener = tweener;
            Rows = rows;
            Cols = cols;

            Layout();
            BuildAssets();

            _blocks = new Transform[Rows, Cols];
            _renderers = new MeshRenderer[Rows, Cols];

            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                Vector2 spec = CellCenter(r, c);

                var block = NewPiece($"Block_{r}_{c}", _root, _blockMesh, _materials[1]);
                block.transform.position = Space3D.ToWorld(spec, 0f);

                _blocks[r, c] = block.transform;
                _renderers[r, c] = block.GetComponent<MeshRenderer>();
            }
        }

        /// <summary>Габариты картинки на доске в spec-координатах.</summary>
        public struct Placement
        {
            public float Cell;
            public float OriginX, OriginY;
            public float Width, Height;

            public float CenterX => OriginX + Width * 0.5f;
            public float CenterY => OriginY + Height * 0.5f;
        }

        /// <summary>
        /// Раскладка картинки: клетка квадратная, картинка вписана в область и
        /// отцентрована в ней.
        ///
        /// Квадратная клетка здесь не вкусовщина: это пиксель-арт, и неквадратный
        /// пиксель его перекашивает — на 35x35 растяжка по вертикали превращала
        /// круглое в овальное. Прежний вариант растягивал клетку на всю область
        /// ради того, чтобы картинка её заполняла; теперь под картинку подгоняется
        /// сама доска, и заполнять нечего.
        ///
        /// Статический метод, потому что размеры площадки нужны сцене раньше, чем
        /// построена доска: белая карточка выкраивается под картинку, а не наоборот.
        /// </summary>
        public static Placement Measure(int rows, int cols)
        {
            float cell = Mathf.Min(AreaW / cols, AreaH / rows);

            var placement = new Placement
            {
                Cell = cell,
                Width = cell * cols,
                Height = cell * rows,
            };

            placement.OriginX = AreaX + (AreaW - placement.Width) * 0.5f;
            placement.OriginY = AreaY + (AreaH - placement.Height) * 0.5f;

            return placement;
        }

        void Layout()
        {
            var placement = Measure(Rows, Cols);

            CellW = CellH = placement.Cell;
            OriginX = placement.OriginX;
            OriginY = placement.OriginY;
        }

        void BuildAssets()
        {
            // Зазор долей от клетки, а не в единицах. Фиксированная единица на клетке
            // 22 давала щель в 4%, а на клетке 8.8 — уже 11%, и мелкий пиксель-арт
            // рассыпался на отдельные кубики. Восемь процентов держат тонкую светлую
            // линию между пикселями при любом размере картинки.
            float w = Space3D.Size(CellW * 0.92f);
            float d = Space3D.Size(CellH * 0.92f);

            // Высота блока — треть стороны, а не половина. На 14x16 крупных кирпичей
            // половина читалась объёмом; на 35x35 мелких она превращала картинку в
            // щётку, где рисунка не видно за торцами. У оригинала пиксель — плоская
            // плитка с намёком на толщину.
            _blockHeight = Mathf.Min(w, d) * 0.24f;

            // Скругление 0.18 вместо 0.28: угол должен читаться как пиксель со снятой
            // фаской, а не как окатыш.
            //
            // Верх уже низа на 14% — плитка, а не коробочка. Наклонная кромка ловит
            // свет иначе, чем верхняя грань, и по каждому пикселю идёт светлый кант.
            // На прямом бруске все пиксели были плоскими заливками, и картинка
            // выглядела напечатанной, а не выложенной.
            _blockMesh = ProcMesh.RoundedBox(w, _blockHeight, d, Mathf.Min(w, d) * 0.18f,
                                             $"block{Rows}x{Cols}", topScale: 0.86f);

            _materials = new Material[GridModel.MaxColor + 1];
            for (int color = 1; color <= _palette.Count && color <= GridModel.MaxColor; color++)
                _materials[color] = ProcMesh.Glossy(_palette.Get(color).baseColor, $"mat_block{color}", 0.06f);
        }

        public void SetCell(int r, int c, int colorId)
        {
            var block = _blocks[r, c];

            if (colorId <= 0 || colorId >= _materials.Length || _materials[colorId] == null)
            {
                block.gameObject.SetActive(false);
                return;
            }

            block.gameObject.SetActive(true);
            block.localScale = Vector3.one;
            block.localRotation = Quaternion.identity;
            _renderers[r, c].sharedMaterial = _materials[colorId];
        }

        /// <summary>Отказ: блок подпрыгивает и качается, оставаясь на месте.</summary>
        public void Wobble(int r, int c, float duration)
        {
            var block = _blocks[r, c];
            Vector3 home = block.position;

            _tweener.Run(duration, Tweener.Linear, t =>
            {
                float wave = Mathf.Sin(t * Mathf.PI * 4f) * (1f - t);
                block.position = home + new Vector3(0f, _blockHeight * 0.35f * Mathf.Abs(wave), 0f);
                block.localRotation = Quaternion.Euler(0f, 0f, 6f * wave);
            }, () =>
            {
                block.position = home;
                block.localRotation = Quaternion.identity;
            });
        }

        /// <summary>Силуэт на победе: блоки возвращаются плоскими лепёшками.</summary>
        public void ShowSilhouette(string[] rows, bool visible)
        {
            for (int r = 0; r < Rows; r++)
            for (int c = 0; c < Cols; c++)
            {
                int colorId = rows[r][c] - '0';
                var block = _blocks[r, c];

                if (!visible)
                {
                    if (colorId != 0) block.gameObject.SetActive(false);
                    continue;
                }

                if (colorId == 0 || colorId >= _materials.Length) continue;

                block.gameObject.SetActive(true);
                block.localScale = new Vector3(1f, 0.12f, 1f);
                _renderers[r, c].sharedMaterial = _materials[colorId];
            }
        }

        static GameObject NewPiece(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            return go;
        }
    }
}
