using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Единственный мост между spec-координатами (начало слева-сверху, Y вниз)
    /// и uGUI (Y вверх). Вся математика игры ведётся в spec; сюда попадает только вывод.
    /// </summary>
    public static class SpecRect
    {
        /// <summary>Якорь в левом верхнем углу родителя, пивот там же.</summary>
        public static void AnchorTopLeft(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
        }

        /// <summary>Якорь в левом верхнем углу, пивот в центре объекта.</summary>
        public static void AnchorCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
        }

        /// <summary>Якорь в левом верхнем углу, пивот по низу-центру — для жаб.</summary>
        public static void AnchorBottomCenter(RectTransform rt)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0.5f, 0f);
        }

        public static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            AnchorTopLeft(rt);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(x, -y);
        }

        /// <summary>Размещение по центру объекта: x, y — центр в spec-координатах.</summary>
        public static void PlaceCentered(RectTransform rt, float cx, float cy, float w, float h)
        {
            AnchorCenter(rt);
            rt.sizeDelta = new Vector2(w, h);
            rt.anchoredPosition = new Vector2(cx, -cy);
        }

        public static void MoveTo(RectTransform rt, Vector2 specPos)
            => rt.anchoredPosition = new Vector2(specPos.x, -specPos.y);

        public static void RotateTo(RectTransform rt, float specAngleDeg)
            => rt.localEulerAngles = new Vector3(0f, 0f, -specAngleDeg);
    }
}
