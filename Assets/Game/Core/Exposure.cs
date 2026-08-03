using System.Collections.Generic;

namespace FrogCart.Core
{
    /// <summary>
    /// Доступность блока: съесть можно только тот, до которого язык дотянется снаружи.
    ///
    /// Заливка от края картинки по пустым клеткам в четырёх направлениях. Блок доступен,
    /// если хотя бы одна соседняя клетка либо за границей поля, либо пустая и связана
    /// с внешним краем. Замурованная пустота внутри картинки доступа не даёт.
    ///
    /// Это тот самый BFS проходимости, что лежит в основе Food Hunt.
    ///
    /// Раньше здесь стоял полный пересчёт с оговоркой «224 клетки дешевле, чем код,
    /// который его избегает». Оговорка перестала быть верной: доска выросла до
    /// 35x35 = 1225 клеток, и полная заливка шла после каждого съеденного блока —
    /// больше миллиона проходов на партию.
    ///
    /// Теперь как у оригинала (`DetectNewPixelCanReach` в их `PixelBoardDetector`):
    /// снятие блока может только **добавить** воздуха, поэтому вместо полной заливки
    /// делается короткий BFS от освободившейся клетки. Полный проход остался для
    /// старта уровня (`FirstDetect` у них) и для тестов, которые сверяют одно с другим.
    /// </summary>
    public sealed class Exposure
    {
        static readonly int[] DirR = { -1, 1, 0, 0 };
        static readonly int[] DirC = { 0, 0, -1, 1 };

        readonly GridModel _grid;
        readonly bool[] _outsideAir;
        readonly Queue<int> _queue = new Queue<int>();

        public Exposure(GridModel grid)
        {
            _grid = grid;
            _outsideAir = new bool[grid.Rows * grid.Cols];
            Recompute();
        }

        /// <summary>Пересчёт после любого изменения картинки.</summary>
        public void Recompute()
        {
            System.Array.Clear(_outsideAir, 0, _outsideAir.Length);
            _queue.Clear();

            for (int r = 0; r < _grid.Rows; r++)
            for (int c = 0; c < _grid.Cols; c++)
            {
                bool onBorder = r == 0 || c == 0 || r == _grid.Rows - 1 || c == _grid.Cols - 1;
                if (!onBorder || _grid.Get(r, c) != 0) continue;

                Enqueue(r, c);
            }

            Flood();
        }

        /// <summary>Разбор очереди: общий для полного пересчёта и для дозаливки.</summary>
        void Flood()
        {
            while (_queue.Count > 0)
            {
                int index = _queue.Dequeue();
                int r = index / _grid.Cols;
                int c = index % _grid.Cols;

                for (int d = 0; d < 4; d++)
                {
                    int nr = r + DirR[d];
                    int nc = c + DirC[d];

                    if (!_grid.InBounds(nr, nc)) continue;
                    if (_grid.Get(nr, nc) != 0) continue;

                    Enqueue(nr, nc);
                }
            }
        }

        /// <summary>
        /// Клетка освободилась. Дозаливка от неё вместо полного пересчёта.
        ///
        /// Снятие блока не может отнять воздух ни у одной клетки — только добавить,
        /// поэтому достаточно проверить, вышла ли сама освободившаяся клетка наружу,
        /// и если да, растечься от неё по соседним пустым. Если не вышла (карман
        /// внутри картинки), не меняется вообще ничего.
        ///
        /// Вызывать строго **после** того, как клетка снята с модели: метод читает
        /// её текущее состояние.
        /// </summary>
        public void OnCleared(int r, int c)
        {
            if (!_grid.InBounds(r, c)) return;
            if (_grid.Get(r, c) != 0) return;   // блок ещё на месте — снимать нечего

            if (!TouchesOutside(r, c)) return;

            _queue.Clear();
            Enqueue(r, c);
            Flood();
        }

        /// <summary>Клетка граничит с внешним воздухом: край поля или уже открытая пустота.</summary>
        bool TouchesOutside(int r, int c)
        {
            if (r == 0 || c == 0 || r == _grid.Rows - 1 || c == _grid.Cols - 1) return true;

            for (int d = 0; d < 4; d++)
            {
                int nr = r + DirR[d];
                int nc = c + DirC[d];

                if (!_grid.InBounds(nr, nc)) return true;
                if (_outsideAir[nr * _grid.Cols + nc]) return true;
            }

            return false;
        }

        void Enqueue(int r, int c)
        {
            int index = r * _grid.Cols + c;
            if (_outsideAir[index]) return;

            _outsideAir[index] = true;
            _queue.Enqueue(index);
        }

        /// <summary>Доступен ли блок для языка.</summary>
        public bool IsExposed(int r, int c)
        {
            if (!_grid.InBounds(r, c)) return false;
            if (_grid.Get(r, c) == 0) return false;

            for (int d = 0; d < 4; d++)
            {
                int nr = r + DirR[d];
                int nc = c + DirC[d];

                // За краем поля — открытый воздух.
                if (!_grid.InBounds(nr, nc)) return true;

                if (_outsideAir[nr * _grid.Cols + nc]) return true;
            }

            return false;
        }

        /// <summary>Сколько блоков сейчас доступно — для отладки и будущих подсказок.</summary>
        public int CountExposed()
        {
            int count = 0;

            for (int r = 0; r < _grid.Rows; r++)
            for (int c = 0; c < _grid.Cols; c++)
                if (IsExposed(r, c)) count++;

            return count;
        }
    }
}
