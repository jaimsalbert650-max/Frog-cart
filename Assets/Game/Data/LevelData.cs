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
        }

        [SerializeField] int levelNumber = 1;

        [Tooltip("16 строк по 14 символов; цифра — индекс цвета, 0 — пусто")]
        [SerializeField] string[] rows;

        [Tooltip("Вагонетки на контуре, слоты 0..4")]
        [SerializeField] CartDef[] loopCarts = new CartDef[5];

        [Tooltip("Очередь в порядке подачи")]
        [SerializeField] CartDef[] queue;

        public int LevelNumber => levelNumber;
        public string[] Rows => rows;
        public CartDef[] LoopCarts => loopCarts;
        public CartDef[] Queue => queue;

        public void Fill(int number, string[] levelRows, CartDef[] loop, CartDef[] queueDefs)
        {
            levelNumber = number;
            rows = levelRows;
            loopCarts = loop;
            queue = queueDefs;
        }
    }
}
