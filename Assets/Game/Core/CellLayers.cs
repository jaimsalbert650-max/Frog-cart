using System;

namespace FrogCart.Core
{
    /// <summary>
    /// Дополнительные слои клетки поверх цвета: прочность и скрытость.
    ///
    /// В Food Hunt у клетки пять полей — `position, color, size, hp, specialStatus`,
    /// и статусы там битовая маска (`AddSpecialStatus` / `RemoveSpecialStatus`),
    /// то есть комбинируются. Здесь взяты два, которые действительно встречаются
    /// в данных уровней и ложатся на нашу механику:
    ///
    /// - **hp** — клетку надо пробить несколько раз. В оригинале это `bigHp`,
    ///   на 71-м уровне 16 клеток с `bigHp: 10`.
    /// - **hidden** — цвет неизвестен, пока клетка не откроется снаружи.
    ///   На 21-м уровне таких 31.
    ///
    /// Слои хранятся строками цифр той же формы, что и картинка: формат остаётся
    /// текстовым, диффается в git и читается глазом. Отсутствующий слой означает
    /// значения по умолчанию — старые уровни продолжают работать без правок.
    /// </summary>
    public sealed class CellLayers
    {
        public const int MaxHp = 9;

        readonly int[,] _hp;
        readonly bool[,] _hidden;

        public int Rows { get; }
        public int Cols { get; }

        /// <summary>Есть ли на уровне хоть одна прочная клетка — чтобы не искать зря.</summary>
        public bool HasArmoured { get; private set; }

        /// <summary>Есть ли на уровне хоть одна скрытая клетка.</summary>
        public bool HasHidden { get; private set; }

        CellLayers(int rows, int cols)
        {
            Rows = rows;
            Cols = cols;
            _hp = new int[rows, cols];
            _hidden = new bool[rows, cols];
        }

        /// <summary>
        /// Слои под уже разобранную картинку. Пустые строки допустимы и означают
        /// «всё по умолчанию»: прочность 1, ничего не скрыто.
        /// </summary>
        public static CellLayers Build(string[] picture, string[] hpRows, string[] hiddenRows)
        {
            if (picture == null || picture.Length == 0)
                throw new ArgumentException("Картинка пуста");

            int rows = picture.Length;
            int cols = picture[0].Length;
            var layers = new CellLayers(rows, cols);

            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                bool solid = picture[r][c] != '0';

                int hp = ReadDigit(hpRows, r, c, fallback: 1);
                if (hp < 1) hp = 1;
                if (hp > MaxHp) hp = MaxHp;

                // Прочность имеет смысл только у существующей клетки: у пустой она
                // сбила бы проверку «клетка ещё жива» на ровном месте.
                layers._hp[r, c] = solid ? hp : 0;
                if (solid && hp > 1) layers.HasArmoured = true;

                bool hidden = solid && ReadDigit(hiddenRows, r, c, fallback: 0) != 0;
                layers._hidden[r, c] = hidden;
                if (hidden) layers.HasHidden = true;
            }

            return layers;
        }

        static int ReadDigit(string[] rows, int r, int c, int fallback)
        {
            if (rows == null || r >= rows.Length) return fallback;

            string row = rows[r];
            if (row == null || c >= row.Length) return fallback;

            char symbol = row[c];
            if (symbol < '0' || symbol > '9') return fallback;

            return symbol - '0';
        }

        public int Hp(int r, int c) => _hp[r, c];

        public bool IsHidden(int r, int c) => _hidden[r, c];

        /// <summary>Удар по клетке. Возвращает true, если после него она разрушена.</summary>
        public bool Damage(int r, int c)
        {
            if (_hp[r, c] <= 0) return true;

            _hp[r, c]--;
            return _hp[r, c] <= 0;
        }

        /// <summary>
        /// Открыть цвет клетки. Возвращает true, если она действительно была скрыта:
        /// вызывающему это нужно, чтобы не перерисовывать доску впустую.
        /// </summary>
        public bool Reveal(int r, int c)
        {
            if (!_hidden[r, c]) return false;

            _hidden[r, c] = false;
            return true;
        }
    }
}
