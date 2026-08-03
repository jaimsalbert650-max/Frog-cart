namespace FrogCart.Core
{
    /// <summary>
    /// Проигрыш по docs/unity-spec/04-gameplay.md: для любого цвета остаток больше
    /// суммарной ёмкости вагонеток этого цвета — на контуре и в очереди вместе.
    ///
    /// **Остаток считается в ударах, а не в блоках.** Пока все клетки ломались
    /// с одного языка, разницы не было. С появлением прочных клеток она есть:
    /// клетка на девять жизней съедает девять мест в вагонетке, и счёт по блокам
    /// давал вдевятеро заниженную потребность. Проверка молчала бы на уровне,
    /// который уже нельзя пройти, и игрок доигрывал бы до упора вслепую.
    /// </summary>
    public static class LoseCheck
    {
        /// <param name="hitsPerColor">
        /// сколько ударов ещё нужно каждому цвету — сумма прочности живых клеток
        /// </param>
        /// <param name="capacityPerColor">count живых вагонеток контура плюс вся очередь</param>
        /// <param name="reservedPerColor">блоки, по которым язык уже летит; null — если таких нет</param>
        public static bool IsLost(int[] hitsPerColor, int[] capacityPerColor, int[] reservedPerColor)
        {
            for (int color = 1; color <= GridModel.MaxColor; color++)
            {
                int remaining = hitsPerColor[color];
                if (reservedPerColor != null) remaining -= reservedPerColor[color];
                if (remaining <= 0) continue;

                if (remaining > capacityPerColor[color]) return true;
            }

            return false;
        }

        /// <summary>
        /// Остаток ударов по цветам. Слои могут быть null — тогда каждая клетка
        /// стоит один удар, как было до появления прочности.
        /// </summary>
        public static int[] CountHits(GridModel grid, CellLayers layers)
        {
            var hits = new int[GridModel.MaxColor + 1];

            for (int r = 0; r < grid.Rows; r++)
            for (int c = 0; c < grid.Cols; c++)
            {
                int color = grid.Get(r, c);
                if (color <= 0) continue;

                hits[color] += layers == null ? 1 : System.Math.Max(1, layers.Hp(r, c));
            }

            return hits;
        }
    }
}
