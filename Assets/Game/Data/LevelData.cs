using UnityEngine;

namespace FrogCart.Data
{
    /// <summary>Уровень: картинка строками плюс вагонетки контура и очередь.</summary>
    [CreateAssetMenu(menuName = "Frog Cart/Level", fileName = "Level")]
    public sealed class LevelData : ScriptableObject
    {
        [System.Serializable]
        public struct CartDef
        {
            public int colorId;
            public int capacity;

            /// <summary>
            /// Сколько ходов вагонетка проведёт во льду. Ноль — обычная.
            /// В Food Hunt это `frozenCount` у коробки со `specialStatus: 2`.
            /// </summary>
            public int frozenCount;

            /// <summary>
            /// Номер связки. Ноль — сама по себе, одинаковые номера — уезжают вместе.
            /// В Food Hunt связка задаётся `combineTarget` — ссылкой на вторую коробку.
            /// </summary>
            public int linkGroup;
        }

        [SerializeField] int levelNumber = 1;

        [Tooltip("Строки картинки; цифра — индекс цвета, 0 — пусто")]
        [SerializeField] string[] rows;

        [Tooltip("Прочность клетки той же формы, что картинка. Пусто — везде 1")]
        [SerializeField] string[] hpRows;

        [Tooltip("Скрытые клетки: 1 — цвет не показан, пока не откроется. Пусто — нет таких")]
        [SerializeField] string[] hiddenRows;

        [Tooltip("Вагонетки на контуре, слоты 0..4")]
        [SerializeField] CartDef[] loopCarts = new CartDef[5];

        [Tooltip("Очередь в порядке подачи")]
        [SerializeField] CartDef[] queue;

        public int LevelNumber => levelNumber;
        public string[] Rows => rows;

        /// <summary>Слой прочности. Может быть пустым — тогда прочность везде 1.</summary>
        public string[] HpRows => hpRows;

        /// <summary>Слой скрытости. Может быть пустым — тогда скрытых клеток нет.</summary>
        public string[] HiddenRows => hiddenRows;

        public CartDef[] LoopCarts => loopCarts;
        public CartDef[] Queue => queue;

        public void Fill(int number, string[] levelRows, CartDef[] loop, CartDef[] queueDefs)
        {
            levelNumber = number;
            rows = levelRows;
            loopCarts = loop;
            queue = queueDefs;
        }

        /// <summary>Заполнение вместе со слоями механик.</summary>
        public void FillLayers(string[] hp, string[] hidden)
        {
            hpRows = hp;
            hiddenRows = hidden;
        }
    }
}
