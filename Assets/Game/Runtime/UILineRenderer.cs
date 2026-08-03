using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Толстая линия по квадратичной кривой Безье с круглыми торцами.
    /// Готового аналога в uGUI нет, а язык жабы — главный герой экрана
    /// (docs/unity-spec/05-feel-anim.md), рисовать его чем-то другим нельзя.
    ///
    /// Точки задаются в spec-координатах; преобразование в локальные — здесь же,
    /// поэтому владелец о системе координат uGUI не думает.
    /// </summary>
    public sealed class UILineRenderer : MaskableGraphic
    {
        const int Segments = 16;

        readonly List<Vector2> _points = new List<Vector2>(Segments + 1);

        float _thickness = 8f;
        bool _visible;

        public override Texture mainTexture => s_WhiteTexture;

        /// <summary>Кривая от a к b с контрольной точкой ctrl, все — в spec-координатах.</summary>
        public void SetCurve(Vector2 a, Vector2 ctrl, Vector2 b, float thickness)
        {
            _points.Clear();

            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                float mt = 1f - t;
                Vector2 p = mt * mt * a + 2f * mt * t * ctrl + t * t * b;
                _points.Add(new Vector2(p.x, -p.y));
            }

            _thickness = thickness;
            _visible = true;
            SetVerticesDirty();
        }

        public void Hide()
        {
            _visible = false;
            _points.Clear();
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            if (!_visible || _points.Count < 2) return;

            float half = _thickness * 0.5f;

            for (int i = 0; i < _points.Count - 1; i++)
            {
                Vector2 p0 = _points[i];
                Vector2 p1 = _points[i + 1];

                Vector2 dir = p1 - p0;
                if (dir.sqrMagnitude < 1e-8f) continue;

                Vector2 normal = new Vector2(-dir.y, dir.x).normalized * half;

                int baseIndex = vh.currentVertCount;

                vh.AddVert(p0 - normal, color, Vector2.zero);
                vh.AddVert(p0 + normal, color, Vector2.zero);
                vh.AddVert(p1 + normal, color, Vector2.zero);
                vh.AddVert(p1 - normal, color, Vector2.zero);

                vh.AddTriangle(baseIndex + 0, baseIndex + 1, baseIndex + 2);
                vh.AddTriangle(baseIndex + 2, baseIndex + 3, baseIndex + 0);
            }

            // Круглые торцы: веер треугольников на обоих концах, чтобы стык сегментов
            // не давал зазубрин на изгибе.
            AddCap(vh, _points[0], half);
            AddCap(vh, _points[_points.Count - 1], half);
        }

        void AddCap(VertexHelper vh, Vector2 center, float radius)
        {
            const int Steps = 8;

            int centerIndex = vh.currentVertCount;
            vh.AddVert(center, color, Vector2.zero);

            for (int i = 0; i <= Steps; i++)
            {
                float a = Mathf.PI * 2f * i / Steps;
                vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius,
                           color, Vector2.zero);
            }

            for (int i = 0; i < Steps; i++)
                vh.AddTriangle(centerIndex, centerIndex + 1 + i, centerIndex + 2 + i);
        }
    }
}
