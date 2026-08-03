using System.Collections.Generic;
using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Меши и материалы для объёмной отрисовки, сделанные кодом.
    ///
    /// Моделей и текстур у проекта нет и взять их неоткуда, поэтому вся геометрия
    /// собирается из примитивов: скруглённый брусок для блоков и корпусов, сфера
    /// для голов и глаз, цилиндр для колёс, тор-подобная лента для рельсов.
    ///
    /// Меши кешируются по ключу: блоков на доске до 1225 штук, и каждому свой меш
    /// был бы расточительством. Материалы тоже — один на цвет, потому что в
    /// Built-in RP MaterialPropertyBlock работает, но менять цвет на каждом
    /// экземпляре без нужды незачем.
    /// </summary>
    public static class ProcMesh
    {
        static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();

        static Shader _shader;

        static Shader Standard
        {
            get
            {
                if (_shader == null) _shader = Shader.Find("Standard");
                return _shader;
            }
        }

        // ── материалы ────────────────────────────────────────────────────────────────

        /// <summary>Глянцевый материал под casual-3D: мягкий блик, лёгкая металличность.</summary>
        public static Material Glossy(Color color, string key, float smoothness = 0.45f)
        {
            if (Materials.TryGetValue(key, out var cached)) return cached;

            var material = new Material(Standard);
            material.color = color;
            material.SetFloat("_Glossiness", smoothness);
            material.SetFloat("_Metallic", 0f);

            Materials[key] = material;
            return material;
        }

        public static Material Metal(Color color, string key)
        {
            if (Materials.TryGetValue(key, out var cached)) return cached;

            var material = new Material(Standard);
            material.color = color;
            material.SetFloat("_Glossiness", 0.75f);
            material.SetFloat("_Metallic", 0.85f);

            Materials[key] = material;
            return material;
        }

        // ── меши ─────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Брусок со скруглёнными вертикальными рёбрами: блок картинки и корпус вагонетки.
        /// Скругление делается фаской по углам, а не сглаживанием — так дешевле
        /// и читается как «пухлая» форма из спеки.
        /// </summary>
        public static Mesh RoundedBox(float width, float height, float depth, float bevel,
                                      string key)
        {
            if (Meshes.TryGetValue(key, out var cached)) return cached;

            bevel = Mathf.Min(bevel, Mathf.Min(width, depth) * 0.45f);

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            float hw = width * 0.5f;
            float hd = depth * 0.5f;

            // Скруглённый профиль: по четыре сегмента на угол. Срезанные углы из
            // восьмиугольника читались гранёными, а спека просит пухлую форму.
            var profile = RoundedProfile(hw, hd, bevel, segmentsPerCorner: 4);

            AddPrism(vertices, triangles, profile, 0f, height);

            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Meshes[key] = mesh;
            return mesh;
        }

        /// <summary>Контур скруглённого прямоугольника против часовой стрелки.</summary>
        static Vector2[] RoundedProfile(float hw, float hd, float radius, int segmentsPerCorner)
        {
            var points = new List<Vector2>((segmentsPerCorner + 1) * 4);

            // Центры четырёх угловых дуг и стартовый угол каждой.
            var corners = new[]
            {
                (center: new Vector2( hw - radius, -hd + radius), start: -90f),
                (center: new Vector2( hw - radius,  hd - radius), start:   0f),
                (center: new Vector2(-hw + radius,  hd - radius), start:  90f),
                (center: new Vector2(-hw + radius, -hd + radius), start: 180f),
            };

            foreach (var corner in corners)
            for (int i = 0; i <= segmentsPerCorner; i++)
            {
                float angle = (corner.start + 90f * i / segmentsPerCorner) * Mathf.Deg2Rad;
                points.Add(corner.center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius);
            }

            return points.ToArray();
        }

        static void AddPrism(List<Vector3> vertices, List<int> triangles,
                             Vector2[] profile, float bottom, float top)
        {
            int n = profile.Length;

            // Боковые грани: каждая своя пара треугольников с собственными вершинами,
            // иначе нормали усреднятся и фаска перестанет читаться.
            for (int i = 0; i < n; i++)
            {
                Vector2 a = profile[i];
                Vector2 b = profile[(i + 1) % n];

                int baseIndex = vertices.Count;
                vertices.Add(new Vector3(a.x, bottom, a.y));
                vertices.Add(new Vector3(b.x, bottom, b.y));
                vertices.Add(new Vector3(b.x, top, b.y));
                vertices.Add(new Vector3(a.x, top, a.y));

                triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 2); triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 0); triangles.Add(baseIndex + 3); triangles.Add(baseIndex + 2);
            }

            AddCap(vertices, triangles, profile, top, up: true);
            AddCap(vertices, triangles, profile, bottom, up: false);
        }

        static void AddCap(List<Vector3> vertices, List<int> triangles,
                           Vector2[] profile, float y, bool up)
        {
            int center = vertices.Count;
            vertices.Add(new Vector3(0f, y, 0f));

            int first = vertices.Count;
            foreach (var p in profile) vertices.Add(new Vector3(p.x, y, p.y));

            int n = profile.Length;
            for (int i = 0; i < n; i++)
            {
                int a = first + i;
                int b = first + (i + 1) % n;

                if (up) { triangles.Add(center); triangles.Add(a); triangles.Add(b); }
                else    { triangles.Add(center); triangles.Add(b); triangles.Add(a); }
            }
        }

        /// <summary>Лента вдоль контура: из неё делаются рельсы и шпалы.</summary>
        public static Mesh Ribbon(Vector3[] points, float width, float height, string key)
        {
            if (Meshes.TryGetValue(key, out var cached)) return cached;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            for (int i = 0; i < points.Length; i++)
            {
                Vector3 current = points[i];
                Vector3 next = points[(i + 1) % points.Length];
                Vector3 dir = (next - current).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, dir) * width * 0.5f;

                vertices.Add(current - side + Vector3.up * height);
                vertices.Add(current + side + Vector3.up * height);
            }

            int count = points.Length;
            for (int i = 0; i < count; i++)
            {
                int a = i * 2;
                int b = i * 2 + 1;
                int c = ((i + 1) % count) * 2;
                int d = ((i + 1) % count) * 2 + 1;

                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }

            var mesh = new Mesh { name = key };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            Meshes[key] = mesh;
            return mesh;
        }

        public static void ClearCache()
        {
            Meshes.Clear();
            Materials.Clear();
        }
    }
}
