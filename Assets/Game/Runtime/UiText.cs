using UnityEngine;
using UnityEngine.UI;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Единая фабрика текстовых полей.
    ///
    /// Почему не TextMeshPro, хотя спека его упоминает: TMP требует импорта
    /// «Essential Resources» в проект. В свежем проекте их нет, TMP_Settings равен null,
    /// и каждый TextMeshProUGUI падает NullReferenceException прямо в Awake — билд
    /// не запускается вообще. Импортировать их из batchmode надёжно нельзя: импорт
    /// пакета асинхронный. Текста в игре ровно два вида — цифры на вагонетках и
    /// процент в HUD, — и встроенный шрифт рисует их не хуже.
    /// </summary>
    public static class UiText
    {
        static Font _font;

        public static Font Font
        {
            get
            {
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        /// <summary>Текст, растянутый по родителю, по центру.</summary>
        public static Text Create(string name, RectTransform parent, int size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var text = go.AddComponent<Text>();
            text.font = Font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return text;
        }
    }
}
