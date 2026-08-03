namespace FrogCart.Core
{
    /// <summary>
    /// Проигрыш по docs/unity-spec/04-gameplay.md: для любого цвета остаток блоков
    /// больше суммарной ёмкости вагонеток этого цвета — на контуре и в очереди вместе.
    /// </summary>
    public static class LoseCheck
    {
        /// <param name="grid">текущая картинка</param>
        /// <param name="capacityPerColor">count живых вагонеток контура плюс вся очередь</param>
        /// <param name="reservedPerColor">блоки, по которым язык уже летит; null — если таких нет</param>
        public static bool IsLost(GridModel grid, int[] capacityPerColor, int[] reservedPerColor)
        {
            for (int color = 1; color <= GridModel.MaxColor; color++)
            {
                int remaining = grid.CountOfColor(color);
                if (reservedPerColor != null) remaining -= reservedPerColor[color];
                if (remaining <= 0) continue;

                if (remaining > capacityPerColor[color]) return true;
            }

            return false;
        }
    }
}
