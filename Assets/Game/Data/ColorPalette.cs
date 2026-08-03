using UnityEngine;

namespace FrogCart.Data
{
    /// <summary>
    /// Девять максимально различимых цветов. Первые пять — из
    /// docs/unity-spec/02-art.md, остальные добавлены под уровни Food Hunt,
    /// где красок до четырнадцати. Девять — предел GridModel.MaxColor: на клетку
    /// в строках уровня отведена одна цифра.
    ///
    /// colorId 1..9; 0 — пустая ячейка, за палитрой для неё обращаться нельзя.
    /// </summary>
    [CreateAssetMenu(menuName = "Frog Cart/Color Palette", fileName = "Palette")]
    public sealed class ColorPalette : ScriptableObject
    {
        [System.Serializable]
        public struct Entry
        {
            public string name;
            public Color baseColor;
            public Color light;
            public Color dark;
        }

        [SerializeField] Entry[] entries = new Entry[9];

        public int Count => entries.Length;

        static bool _warned;

        /// <summary>
        /// Цвет по номеру. Номер за пределами палитры не роняет игру, а один раз
        /// пишет ошибку и берёт ближайший существующий.
        ///
        /// Так это стоило сделать сразу. Палитра в справочном проекте отстала на
        /// три цвета, уровень требовал семь — и `entries[colorId - 1]` бросал
        /// IndexOutOfRange прямо посреди сборки уровня. Исключение обрывало
        /// BuildLevel, поэтому очередь оставалась ненастроенной, HUD пустым,
        /// а блоки лишних цветов просто не появлялись на доске. По виду это
        /// выглядело тремя разными поломками, и на поиск ушёл час.
        /// </summary>
        public Entry Get(int colorId)
        {
            if (entries == null || entries.Length == 0)
                return new Entry { name = "missing", baseColor = Color.magenta,
                                   light = Color.magenta, dark = Color.magenta };

            int index = colorId - 1;

            if (index < 0 || index >= entries.Length)
            {
                if (!_warned)
                {
                    _warned = true;
                    Debug.LogError($"[Palette] Запрошен цвет {colorId}, а в палитре "
                                 + $"{entries.Length}. Пересоберите её: "
                                 + "Frog Cart → Rebuild Assets And Scene.");
                }

                index = Mathf.Clamp(index, 0, entries.Length - 1);
            }

            return entries[index];
        }

        public void SetAll(Entry[] value) => entries = value;
    }
}
