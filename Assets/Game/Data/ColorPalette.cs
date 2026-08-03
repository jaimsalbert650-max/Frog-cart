using UnityEngine;

namespace FrogCart.Data
{
    /// <summary>
    /// Пять максимально различимых цветов из docs/unity-spec/02-art.md.
    /// colorId 1..5; 0 — пустая ячейка, за палитрой для неё обращаться нельзя.
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

        [SerializeField] Entry[] entries = new Entry[5];

        public int Count => entries.Length;

        public Entry Get(int colorId) => entries[colorId - 1];

        public void SetAll(Entry[] value) => entries = value;
    }
}
