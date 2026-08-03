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
    /// </summary>
    public sealed class Grid3DView : MonoBehaviour, IGridView
    {
        public const float AreaX = 41f;
        public const float AreaY = 132f;
        public const float AreaW = 308f;
        public const float AreaH = 432f;

        Transform _root;
        ColorPalette _palette;
        Tweener _tweener;

        Transform[,] _blocks;
        MeshRenderer[,] _renderers;
        Material[] _materials;
        Material _socketMaterial;
        Mesh _blockMesh;
        Mesh _socketMesh;
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

                // Гнездо — плоская вдавленная площадка под блоком.
                var socket = NewPiece($"Socket_{r}_{c}", _root, _socketMesh, _socketMaterial);
                socket.transform.position = Space3D.ToWorld(spec, 0.002f);

                var block = NewPiece($"Block_{r}_{c}", _root, _blockMesh, _materials[1]);
                block.transform.position = Space3D.ToWorld(spec, 0f);

                _blocks[r, c] = block.transform;
                _renderers[r, c] = block.GetComponent<MeshRenderer>();
            }
        }

        void Layout()
        {
            bool specShape = Rows == 16 && Cols == 14;

            if (specShape)
            {
                CellW = 22f;
                CellH = 27f;
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

        void BuildAssets()
        {
            float w = Space3D.Size(CellW - 2f);
            float d = Space3D.Size(CellH - 2f);

            // Высота блока — примерно половина меньшей стороны: брусок читается
            // объёмным, но не превращается в башню и не заслоняет соседей сзади.
            _blockHeight = Mathf.Min(w, d) * 0.5f;

            _blockMesh = ProcMesh.RoundedBox(w, _blockHeight, d, Mathf.Min(w, d) * 0.28f,
                                             $"block{Rows}x{Cols}");

            _socketMesh = ProcMesh.RoundedBox(w * 0.82f, Space3D.Size(1.2f), d * 0.82f,
                                              Mathf.Min(w, d) * 0.2f, $"socket{Rows}x{Cols}");

            _socketMaterial = ProcMesh.Glossy(ProcSprite.Hex("B8A07C"), "mat_socket", 0.04f);

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
