using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Импорт меша из .glb без внешних пакетов.
///
/// Unity не открывает glb сама, а ставить glTFast значило бы завести первую внешнюю
/// зависимость в проекте, который принципиально собирается собственным кодом. Формат
/// же простой: заголовок, чанк JSON и чанк с бинарным буфером, из которого читаются
/// позиции, нормали и индексы по описанию аксессоров.
///
/// Поддерживается ровно то, что встречается в моделях, ради которых это писалось:
/// индексированные треугольники, POSITION и NORMAL, несколько примитивов на меш.
/// Скиннинг сознательно игнорируется — вершины берутся как есть, то есть в позе
/// покоя, а вся анимация в этой игре делается кодом.
/// </summary>
public static class GlbImport
{
    [System.Serializable] class Gltf
    {
        public Accessor[] accessors;
        public BufferView[] bufferViews;
        public Mesh[] meshes;
        public Material[] materials;
        public Node[] nodes;
        public Skin[] skins;
    }

    [System.Serializable] class Node
    {
        public string name;
        public int[] children;
        public float[] translation;
        public float[] rotation;
        public float[] scale;
    }

    [System.Serializable] class Skin
    {
        public int inverseBindMatrices = -1;
        public int[] joints;
    }

    [System.Serializable] class Accessor
    {
        public int bufferView = -1;
        public int byteOffset;
        public int componentType;
        public int count;
        public string type;
    }

    [System.Serializable] class BufferView
    {
        public int byteOffset;
        public int byteLength;
        public int byteStride;
    }

    [System.Serializable] class Mesh
    {
        public string name;
        public Primitive[] primitives;
    }

    [System.Serializable] class Primitive
    {
        public Attributes attributes;
        public int indices = -1;
        public int material = -1;
    }

    [System.Serializable] class Attributes
    {
        public int POSITION = -1;
        public int NORMAL = -1;
        public int JOINTS_0 = -1;
        public int WEIGHTS_0 = -1;
    }

    [System.Serializable] class Material
    {
        public string name;
        public Pbr pbrMetallicRoughness;
    }

    [System.Serializable] class Pbr
    {
        public float[] baseColorFactor;
    }

    /// <summary>
    /// Разобранная модель: меш с подмешами по числу материалов и сами цвета.
    /// Подмеши идут в том же порядке, что материалы, — на этом держится перекраска.
    /// </summary>
    public sealed class Result
    {
        public UnityEngine.Mesh Mesh;
        public string[] MaterialNames;
        public Color[] MaterialColors;
    }

    /// <summary>
    /// Доворот костей поверх позы покоя: имя кости — дополнительный поворот.
    ///
    /// Модель приходит в позе покоя, с разведёнными лапами. Повернуть её целиком
    /// мало: сверху выходит распластанная фигура шире вагонетки. Поза сводит лапы
    /// и кладёт кисти на борт, и запекается один раз при импорте — в игре никакого
    /// скиннинга не остаётся, только обычный меш.
    /// </summary>
    public static Dictionary<string, Quaternion> Pose;

    public static Result Read(string path, bool normalizeHeight = true)
    {
        byte[] bytes = File.ReadAllBytes(path);

        if (System.Text.Encoding.ASCII.GetString(bytes, 0, 4) != "glTF")
            throw new System.InvalidOperationException($"Не glb: {path}");

        int jsonLength = System.BitConverter.ToInt32(bytes, 12);
        string json = System.Text.Encoding.UTF8.GetString(bytes, 20, jsonLength);

        // Второй чанк — двоичный буфер. Его смещение считается от конца первого,
        // с выравниванием по четырём байтам, как велит формат.
        int binHeader = 20 + jsonLength;
        int binOffset = binHeader + 8;

        var g = JsonUtility.FromJson<Gltf>(json);

        // Матрицы скиннинга: для каждой вершины — куда её унести из позы покоя.
        // Считаются один раз на файл, дальше применяются к каждому примитиву.
        Matrix4x4[] skinning = BuildSkinning(bytes, binOffset, g);

        var vertices = new List<Vector3>();
        var normals = new List<Vector3>();
        var subMeshes = new List<int[]>();
        var partNames = new List<string>();
        var partMaterials = new List<int>();

        // Генераторы формы по картинке отдают голую геометрию, без нормалей.
        // Их придётся считать самим, иначе модель выйдет равномерно засвеченной.
        bool anyNormals = false;

        // Все меши файла, а не только первый: у моделей вроде вагона корпус и колёса
        // разложены по отдельным мешам, и по первому пришли бы одни борта без колёс.
        foreach (var source in g.meshes)
        foreach (var prim in source.primitives)
        {
            int baseVertex = vertices.Count;

            var pos = ReadVec3(bytes, binOffset, g, prim.attributes.POSITION);

            var nrm = prim.attributes.NORMAL >= 0
                ? ReadVec3(bytes, binOffset, g, prim.attributes.NORMAL)
                : null;

            // Скиннинг применяется здесь же: вершина уносится в позу суммой матриц
            // своих костей с их весами — стандартная формула glTF.
            if (skinning != null && prim.attributes.JOINTS_0 >= 0 && prim.attributes.WEIGHTS_0 >= 0)
            {
                var joints = ReadJoints(bytes, binOffset, g, prim.attributes.JOINTS_0);
                var weights = ReadVec4(bytes, binOffset, g, prim.attributes.WEIGHTS_0);

                for (int i = 0; i < pos.Length; i++)
                {
                    var skinned = Vector3.zero;
                    var skinnedNormal = Vector3.zero;
                    float total = 0f;

                    for (int k = 0; k < 4; k++)
                    {
                        float w = weights[i][k];
                        if (w <= 0f) continue;

                        int joint = joints[i][k];
                        if (joint < 0 || joint >= skinning.Length) continue;

                        skinned += skinning[joint].MultiplyPoint3x4(pos[i]) * w;
                        if (nrm != null) skinnedNormal += skinning[joint].MultiplyVector(nrm[i]) * w;
                        total += w;
                    }

                    if (total > 0.0001f)
                    {
                        pos[i] = skinned / total;
                        if (nrm != null) nrm[i] = (skinnedNormal / total).normalized;
                    }
                }
            }

            // Позиции добавляются после скиннинга: до него они лежат в позе покоя.
            vertices.AddRange(pos);

            if (nrm != null) { normals.AddRange(nrm); anyNormals = true; }
            else for (int i = 0; i < pos.Length; i++) normals.Add(Vector3.up);

            var indices = ReadIndices(bytes, binOffset, g, prim.indices, pos.Length);
            for (int i = 0; i < indices.Length; i++) indices[i] += baseVertex;

            // glTF задаёт треугольники против часовой стрелки, Unity — по часовой.
            // Без разворота модель вывернется наизнанку: снаружи будут видны
            // внутренние грани, и освещение станет неправильным.
            for (int i = 0; i + 2 < indices.Length; i += 3)
                (indices[i + 1], indices[i + 2]) = (indices[i + 2], indices[i + 1]);

            subMeshes.Add(indices);
            partNames.Add(source.name);
            partMaterials.Add(prim.material);
        }

        // glTF — правая система координат, Unity — левая. Зеркалим по X.
        for (int i = 0; i < vertices.Count; i++)
        {
            vertices[i] = new Vector3(-vertices[i].x, vertices[i].y, vertices[i].z);
            normals[i] = new Vector3(-normals[i].x, normals[i].y, normals[i].z);
        }

        // Модель может быть собрана «Z вверх» — так делает Blender, и FBX2glTF это
        // сохраняет. Определяется по тому, какая ось начинается ровно с нуля: модель
        // стоит на земле, и её низ лежит в нуле по вертикали. Без разворота жаба
        // лежит плашмя, и сверху видна лепёшка вместо персонажа.
        if (IsZUp(vertices))
        {
            for (int i = 0; i < vertices.Count; i++)
            {
                vertices[i] = new Vector3(vertices[i].x, vertices[i].z, -vertices[i].y);
                normals[i] = new Vector3(normals[i].x, normals[i].z, -normals[i].y);
            }
        }

        if (normalizeHeight) Normalize(vertices);

        var mesh = new UnityEngine.Mesh { name = Path.GetFileNameWithoutExtension(path) };
        mesh.indexFormat = vertices.Count > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.SetVertices(vertices);
        mesh.SetNormals(normals);
        mesh.subMeshCount = subMeshes.Count;

        for (int i = 0; i < subMeshes.Count; i++) mesh.SetTriangles(subMeshes[i], i);

        if (!anyNormals) mesh.RecalculateNormals();

        mesh.RecalculateBounds();

        var names = new string[subMeshes.Count];
        var colors = new Color[subMeshes.Count];

        for (int i = 0; i < subMeshes.Count; i++)
        {
            int m = partMaterials[i];

            // Имя части важнее имени материала: у моделей с одним общим материалом
            // (типичный атлас) различить корпус и колёса можно только по мешу.
            names[i] = $"{partNames[i]}/{(m >= 0 ? g.materials[m].name : "—")}";

            var f = m >= 0 ? g.materials[m].pbrMetallicRoughness?.baseColorFactor : null;
            colors[i] = f != null && f.Length >= 3 ? new Color(f[0], f[1], f[2]) : Color.white;
        }

        return new Result { Mesh = mesh, MaterialNames = names, MaterialColors = colors };
    }

    /// <summary>
    /// Приводит модель к росту 1 и ставит опору в низ по центру.
    ///
    /// Свои единицы у каждой модели свои: у этой габарит меньше пяти сотых, потому
    /// что масштаб 100 живёт в узле, а не в вершинах. Нормализация избавляет от
    /// разбора узлов и делает размер предсказуемым — дальше его задаёт игра.
    /// </summary>
    /// <summary>
    /// Собрана ли модель «Z вверх». Вертикальная ось у стоящей на земле модели
    /// начинается с нуля, у остальных диапазон идёт по обе стороны от него.
    /// </summary>
    static bool IsZUp(List<Vector3> vertices)
    {
        var min = Vector3.one * float.MaxValue;
        var max = Vector3.one * float.MinValue;

        foreach (var v in vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }

        float scale = Mathf.Max(max.x - min.x, Mathf.Max(max.y - min.y, max.z - min.z));
        if (scale <= 0.0001f) return false;

        float tolerance = scale * 0.02f;
        return Mathf.Abs(min.z) < tolerance && Mathf.Abs(min.y) > tolerance;
    }

    static void Normalize(List<Vector3> vertices)
    {
        var min = Vector3.one * float.MaxValue;
        var max = Vector3.one * float.MinValue;

        foreach (var v in vertices) { min = Vector3.Min(min, v); max = Vector3.Max(max, v); }

        Vector3 size = max - min;
        float height = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        if (height <= 0.0001f) return;

        float scale = 1f / height;
        var center = new Vector3((min.x + max.x) * 0.5f, min.y, (min.z + max.z) * 0.5f);

        for (int i = 0; i < vertices.Count; i++) vertices[i] = (vertices[i] - center) * scale;
    }

    /// <summary>
    /// Матрица на каждую кость: перенос вершины из позы покоя в заданную позу.
    ///
    /// Формула glTF: глобальная матрица кости в новой позе, умноженная на обратную
    /// привязочную. Обратная привязочная переводит вершину в систему кости, вторая
    /// возвращает её обратно уже повёрнутой.
    /// </summary>
    static Matrix4x4[] BuildSkinning(byte[] bytes, int binOffset, Gltf g)
    {
        if (g.skins == null || g.skins.Length == 0 || g.nodes == null) return null;

        var skin = g.skins[0];
        if (skin.joints == null || skin.joints.Length == 0) return null;

        var global = new Matrix4x4[g.nodes.Length];
        var known = new bool[g.nodes.Length];

        // Корни — узлы, которые никому не дети. С них и обходим дерево.
        var isChild = new bool[g.nodes.Length];
        foreach (var n in g.nodes)
            if (n.children != null)
                foreach (int c in n.children) isChild[c] = true;

        for (int i = 0; i < g.nodes.Length; i++)
            if (!isChild[i]) Walk(g, i, Matrix4x4.identity, global, known);

        var inverseBind = skin.inverseBindMatrices >= 0
            ? ReadMat4(bytes, binOffset, g, skin.inverseBindMatrices)
            : null;

        var result = new Matrix4x4[skin.joints.Length];

        for (int i = 0; i < skin.joints.Length; i++)
        {
            var jointGlobal = global[skin.joints[i]];
            result[i] = inverseBind != null ? jointGlobal * inverseBind[i] : jointGlobal;
        }

        return result;
    }

    static void Walk(Gltf g, int index, Matrix4x4 parent, Matrix4x4[] global, bool[] known)
    {
        var n = g.nodes[index];

        var t = n.translation != null && n.translation.Length >= 3
            ? new Vector3(n.translation[0], n.translation[1], n.translation[2]) : Vector3.zero;

        var r = n.rotation != null && n.rotation.Length >= 4
            ? new Quaternion(n.rotation[0], n.rotation[1], n.rotation[2], n.rotation[3])
            : Quaternion.identity;

        var s = n.scale != null && n.scale.Length >= 3
            ? new Vector3(n.scale[0], n.scale[1], n.scale[2]) : Vector3.one;

        // Доворот позы применяется к местному повороту кости, до её детей: рука
        // поворачивается вместе с предплечьем и кистью, как ей и полагается.
        if (Pose != null && n.name != null && Pose.TryGetValue(n.name, out var extra)) r *= extra;

        global[index] = parent * Matrix4x4.TRS(t, r, s);
        known[index] = true;

        if (n.children == null) return;
        foreach (int c in n.children) Walk(g, c, global[index], global, known);
    }

    static Matrix4x4[] ReadMat4(byte[] bytes, int binOffset, Gltf g, int accessorIndex)
    {
        var a = g.accessors[accessorIndex];
        var view = g.bufferViews[a.bufferView];

        int stride = view.byteStride > 0 ? view.byteStride : 64;
        int start = binOffset + view.byteOffset + a.byteOffset;

        var result = new Matrix4x4[a.count];

        for (int i = 0; i < a.count; i++)
        {
            int o = start + i * stride;
            var m = new Matrix4x4();

            // glTF хранит матрицы по столбцам.
            for (int col = 0; col < 4; col++)
            for (int row = 0; row < 4; row++)
                m[row, col] = System.BitConverter.ToSingle(bytes, o + (col * 4 + row) * 4);

            result[i] = m;
        }

        return result;
    }

    static Vector4[] ReadVec4(byte[] bytes, int binOffset, Gltf g, int accessorIndex)
    {
        var a = g.accessors[accessorIndex];
        var view = g.bufferViews[a.bufferView];

        int stride = view.byteStride > 0 ? view.byteStride : 16;
        int start = binOffset + view.byteOffset + a.byteOffset;

        var result = new Vector4[a.count];

        for (int i = 0; i < a.count; i++)
        {
            int o = start + i * stride;
            result[i] = new Vector4(System.BitConverter.ToSingle(bytes, o),
                                    System.BitConverter.ToSingle(bytes, o + 4),
                                    System.BitConverter.ToSingle(bytes, o + 8),
                                    System.BitConverter.ToSingle(bytes, o + 12));
        }

        return result;
    }

    /// <summary>Номера костей вершины. Хранятся байтами или ushort, а не float.</summary>
    static int[][] ReadJoints(byte[] bytes, int binOffset, Gltf g, int accessorIndex)
    {
        var a = g.accessors[accessorIndex];
        var view = g.bufferViews[a.bufferView];

        int size = a.componentType == 5121 ? 1 : 2;
        int stride = view.byteStride > 0 ? view.byteStride : size * 4;
        int start = binOffset + view.byteOffset + a.byteOffset;

        var result = new int[a.count][];

        for (int i = 0; i < a.count; i++)
        {
            int o = start + i * stride;
            result[i] = new int[4];

            for (int k = 0; k < 4; k++)
                result[i][k] = size == 1
                    ? bytes[o + k]
                    : System.BitConverter.ToUInt16(bytes, o + k * 2);
        }

        return result;
    }

    static Vector3[] ReadVec3(byte[] bytes, int binOffset, Gltf g, int accessorIndex)
    {
        var a = g.accessors[accessorIndex];
        var view = g.bufferViews[a.bufferView];

        int stride = view.byteStride > 0 ? view.byteStride : 12;
        int start = binOffset + view.byteOffset + a.byteOffset;

        var result = new Vector3[a.count];

        for (int i = 0; i < a.count; i++)
        {
            int o = start + i * stride;
            result[i] = new Vector3(System.BitConverter.ToSingle(bytes, o),
                                    System.BitConverter.ToSingle(bytes, o + 4),
                                    System.BitConverter.ToSingle(bytes, o + 8));
        }

        return result;
    }

    static int[] ReadIndices(byte[] bytes, int binOffset, Gltf g, int accessorIndex, int vertexCount)
    {
        if (accessorIndex < 0)
        {
            var seq = new int[vertexCount];
            for (int i = 0; i < vertexCount; i++) seq[i] = i;
            return seq;
        }

        var a = g.accessors[accessorIndex];
        var view = g.bufferViews[a.bufferView];
        int start = binOffset + view.byteOffset + a.byteOffset;

        var result = new int[a.count];

        for (int i = 0; i < a.count; i++)
        {
            // 5121 — байт, 5123 — ushort, 5125 — uint. Другого в моделях не встречалось.
            result[i] = a.componentType switch
            {
                5121 => bytes[start + i],
                5123 => System.BitConverter.ToUInt16(bytes, start + i * 2),
                _ => (int)System.BitConverter.ToUInt32(bytes, start + i * 4),
            };
        }

        return result;
    }

    /// <summary>
    /// Прореживание меша слиянием вершин по кубической сетке.
    ///
    /// Форма из генератора по картинке приходит с миллионом треугольников: столько
    /// не нужно и одной жабе, а их на контуре десятки. Упрощение на стороне сервиса
    /// не сработало — вернуло файл тяжелее исходного, — поэтому режем у себя.
    ///
    /// Пространство делится на кубики со стороной «габарит / resolution». Все вершины
    /// одного кубика сливаются в одну, среднюю; треугольники, у которых после слияния
    /// совпали хотя бы два угла, выбрасываются как выродившиеся.
    ///
    /// Способ грубый, зато без внешних библиотек и предсказуемый: чем крупнее кубик,
    /// тем меньше остаётся. Для округлой головы, которую видно мелко и сверху,
    /// разницы с честным упрощением по ошибке не разглядеть.
    /// </summary>
    public static void Cluster(UnityEngine.Mesh mesh, int resolution)
    {
        var verts = mesh.vertices;
        var size = mesh.bounds.size;

        float cell = Mathf.Max(size.x, size.y, size.z) / Mathf.Max(1, resolution);
        if (cell <= 0f) return;

        // Ключ кубика — целочисленные координаты его угла. Словарь заодно нумерует
        // кубики в порядке первой встречи, и этот номер становится новой вершиной.
        var cells = new Dictionary<Vector3Int, int>();
        var sums = new List<Vector3>();
        var counts = new List<int>();
        var remap = new int[verts.Length];

        for (int i = 0; i < verts.Length; i++)
        {
            var key = new Vector3Int(
                Mathf.FloorToInt(verts[i].x / cell),
                Mathf.FloorToInt(verts[i].y / cell),
                Mathf.FloorToInt(verts[i].z / cell));

            if (!cells.TryGetValue(key, out int index))
            {
                index = sums.Count;
                cells.Add(key, index);
                sums.Add(Vector3.zero);
                counts.Add(0);
            }

            remap[i] = index;
            sums[index] += verts[i];
            counts[index]++;
        }

        var merged = new Vector3[sums.Count];
        for (int i = 0; i < merged.Length; i++) merged[i] = sums[i] / counts[i];

        var kept = new List<int[]>();

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var tris = mesh.GetTriangles(s);
            var left = new List<int>(tris.Length);

            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                int a = remap[tris[i]], b = remap[tris[i + 1]], c = remap[tris[i + 2]];
                if (a == b || b == c || a == c) continue;

                left.Add(a);
                left.Add(b);
                left.Add(c);
            }

            kept.Add(left.ToArray());
        }

        mesh.Clear();
        mesh.indexFormat = merged.Length > 65000
            ? UnityEngine.Rendering.IndexFormat.UInt32
            : UnityEngine.Rendering.IndexFormat.UInt16;

        mesh.SetVertices(merged);
        mesh.subMeshCount = kept.Count;

        for (int s = 0; s < kept.Count; s++) mesh.SetTriangles(kept[s], s);

        // Нормали старых вершин слиянию не пережили — считаем заново по новым граням.
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }

    /// <summary>
    /// Разобрать glb из Downloads и положить меш в Resources, чтобы он был доступен
    /// и в редакторе, и в собранном плеере без ссылок в сцене.
    /// </summary>
    /// <summary>
    /// Поза жабы, сидящей в вагонетке и держащейся за борт.
    ///
    /// Модель приходит стоящей на четырёх лапах с разведёнными в стороны конечностями.
    /// В таком виде она шире вагонетки, и сверху видна распластанная фигура. Поза
    /// подбирает лапы под корпус, а руки выносит вперёд и вниз — на борт.
    ///
    /// Имена костей взяты из самой модели: Shoulder/UpperArm/LowerArm и
    /// UpperLeg/LowerLeg с суффиксами .L и .R.
    /// </summary>
    static Dictionary<string, Quaternion> HoldingPose() => new Dictionary<string, Quaternion>
    {
        // Вариант 4 из раскладки поз — единственный, где лапы легли на борта, а не
        // повисли мимо или задрались вверх.
        //
        // Решает сгиб локтя, а не вынос руки: при почти прямом локте (-20, -30)
        // кисть проносится над бортом, при сильном (-62) опускается на него.
        ["Shoulder.L"] = Quaternion.Euler(0f, 0f, -35f),
        ["Shoulder.R"] = Quaternion.Euler(0f, 0f, 35f),
        ["UpperArm.L"] = Quaternion.Euler(-45f, 0f, -38f),
        ["UpperArm.R"] = Quaternion.Euler(-45f, 0f, 38f),
        ["LowerArm.L"] = Quaternion.Euler(-62f, 0f, 0f),
        ["LowerArm.R"] = Quaternion.Euler(-62f, 0f, 0f),

        // Ноги подбираются под себя, чтобы не торчали за габарит вагонетки.
        ["UpperLeg.L"] = Quaternion.Euler(55f, 0f, 20f),
        ["UpperLeg.R"] = Quaternion.Euler(55f, 0f, -20f),
        ["LowerLeg.L"] = Quaternion.Euler(-70f, 0f, 0f),
        ["LowerLeg.R"] = Quaternion.Euler(-70f, 0f, 0f),

        // Корпус чуть вперёд, голова поднята к игроку — иначе сверху виден загривок.
        ["Torso"] = Quaternion.Euler(18f, 0f, 0f),
        ["Neck"] = Quaternion.Euler(-25f, 0f, 0f),
        ["Head"] = Quaternion.Euler(-30f, 0f, 0f),
    };

    [MenuItem("Frog Cart/Import Frog Model From Downloads")]
    public static void ImportFrog()
    {
        Pose = HoldingPose();
        ImportFromDownloads("Frog by Quaternius - 37wofOCOzG.glb", "FrogModel");
        Pose = null;
    }

    /// <summary>
    /// Пять поз рук разом, по одной на каждое место контура.
    ///
    /// Подбирать углы по одному кадру за пересборку — ходить кругами: осей костей
    /// этой модели я не знаю, и шаг может уйти в любую сторону. Пять вариантов в
    /// одном кадре, в настоящих вагонетках и в настоящем размере, дают выбор за
    /// один заход. Выбранный переносится в HoldingPose, ассеты вариантов удаляются.
    /// </summary>
    [MenuItem("Frog Cart/Import Frog Pose Sheet")]
    public static void ImportPoseSheet()
    {
        // Меняются вынос руки вперёд, сгиб локтя и разведение в стороны — три числа,
        // от которых и зависит, ляжет кисть на борт или пройдёт мимо.
        var variants = new[]
        {
            (arm: -30f, elbow: -50f, spread: 20f),
            (arm: -45f, elbow: -40f, spread: 25f),
            (arm: -55f, elbow: -30f, spread: 30f),
            (arm: -65f, elbow: -20f, spread: 30f),
            (arm: -45f, elbow: -62f, spread: 38f),
        };

        for (int i = 0; i < variants.Length; i++)
        {
            var v = variants[i];
            var pose = HoldingPose();

            pose["UpperArm.L"] = Quaternion.Euler(v.arm, 0f, -v.spread);
            pose["UpperArm.R"] = Quaternion.Euler(v.arm, 0f, v.spread);
            pose["LowerArm.L"] = Quaternion.Euler(v.elbow, 0f, 0f);
            pose["LowerArm.R"] = Quaternion.Euler(v.elbow, 0f, 0f);

            Pose = pose;
            ImportFromDownloads("Frog by Quaternius - 37wofOCOzG.glb", $"FrogPose{i}");
            Debug.Log($"[GlbImport] вариант {i}: рука {v.arm}, локоть {v.elbow}, разлёт {v.spread}");
        }

        Pose = null;
    }

    [MenuItem("Frog Cart/Import Cart Model From Downloads")]
    public static void ImportCart()
        => ImportFromDownloads("train-carriage-box.glb", "CartModel");

    /// <summary>
    /// Голова жабы, сгенерированная по картинке.
    ///
    /// В отличие от готовых моделей, эта приходит без скелета, без материалов и без
    /// нормалей — одна сплошная поверхность на миллион треугольников. Поэтому
    /// единственная с прореживанием: 40 кубиков на габарит оставляют тонкие рожки
    /// и складку рта, но убирают почти всю лишнюю плотность — из миллиона
    /// треугольников остаётся около двенадцати тысяч.
    /// </summary>
    [MenuItem("Frog Cart/Import Frog Head From Downloads")]
    public static void ImportFrogHead()
        => ImportFromDownloads("frog-head-hunyuan.glb", "FrogHeadModel", 40, headParts: true);

    /// <summary>
    /// Разбор головы на выступающие части.
    ///
    /// В файле нет ни материалов, ни костей, ни развёртки — только поверхность.
    /// Значит, глаза и рожки можно найти лишь по форме: голова в основе своей
    /// эллипсоид, а глаза и рожки из него торчат. Вершина считается выступом,
    /// если она дальше от центра, чем поверхность вписанного эллипсоида в том же
    /// направлении.
    ///
    /// Выступы затем собираются в группы по близости. Куда смотрит лицо, эта же
    /// разметка и отвечает: глаза всегда на лицевой стороне.
    /// </summary>
    public static List<Lump> FindLumps(UnityEngine.Mesh mesh, float threshold)
    {
        var verts = mesh.vertices;
        var b = mesh.bounds;
        var c = b.center;

        var radius = new Vector3(
            Mathf.Max(b.extents.x, 1e-5f),
            Mathf.Max(b.extents.y, 1e-5f),
            Mathf.Max(b.extents.z, 1e-5f));

        var sticking = new List<Vector3>();

        foreach (var v in verts)
        {
            var d = v - c;
            float f = Mathf.Sqrt(d.x * d.x / (radius.x * radius.x)
                               + d.y * d.y / (radius.y * radius.y)
                               + d.z * d.z / (radius.z * radius.z));

            if (f > threshold) sticking.Add(v);
        }

        // Жадная группировка: вершина идёт в первую группу, к центру которой она
        // ближе шага сетки прореживания. Групп мало и они далеко друг от друга,
        // поэтому ничего умнее не нужно.
        var lumps = new List<Lump>();
        float reach = Mathf.Max(radius.x, radius.y) * 0.45f;

        foreach (var v in sticking)
        {
            Lump found = null;
            foreach (var l in lumps)
                if ((l.Center - v).magnitude < reach) { found = l; break; }

            if (found == null) { found = new Lump(); lumps.Add(found); }

            found.Sum += v;
            found.Count++;
            found.Center = found.Sum / found.Count;
        }

        lumps.Sort((a, z) => z.Count.CompareTo(a.Count));
        return lumps;
    }

    public sealed class Lump
    {
        public Vector3 Sum;
        public Vector3 Center;
        public int Count;
    }

    /// <summary>
    /// Раскладка головы на подмеши: тело, белки глаз и тёмное.
    ///
    /// Материалов в файле нет, поэтому голова красилась целиком в один цвет и
    /// читалась как гладкий шар. Части ищутся по форме — глаза и рожки торчат из
    /// эллипсоида, — и каждый треугольник по своему центру попадает в один из трёх
    /// подмешей. Дальше игра красит только первый, цветом уровня.
    ///
    /// Рот геометрией почти не выражен: это тонкая складка, которую прореживание
    /// съедает. Поэтому он задаётся полосой по высоте на лицевой стороне — так же,
    /// как у процедурной жабы, где рот тоже был отдельной деталью, а не рельефом.
    ///
    /// Возвращает false, если глаз не нашлось: тогда лучше оставить один подмеш,
    /// чем раскрасить голову наугад.
    /// </summary>
    public static bool SplitHeadParts(UnityEngine.Mesh mesh)
    {
        var lumps = FindLumps(mesh, 1.05f);
        var height = Mathf.Max(mesh.bounds.size.y, 1e-5f);

        // Глаза — выступы на лицевой стороне выше середины, но ниже макушки:
        // рожки сидят ещё выше и по глубине приходятся на центр.
        var eyes = lumps.FindAll(l => l.Center.y > height * 0.60f
                                   && l.Center.y < height * 0.86f
                                   && Mathf.Abs(l.Center.z) > height * 0.18f);

        if (eyes.Count < 2) { Debug.LogWarning("[GlbImport] Глаза не найдены"); return false; }
        if (eyes.Count > 2) eyes = eyes.GetRange(0, 2);

        var horns = lumps.FindAll(l => l.Center.y > height * 0.86f);

        // Лицо смотрит туда же, куда глаза: знак глубины у обоих выступов один.
        float faceSide = Mathf.Sign(eyes[0].Center.z);

        float eyeReach = height * 0.20f;
        float pupilReach = height * 0.10f;
        float hornReach = height * 0.16f;

        var verts = mesh.vertices;
        var body = new List<int>();
        var white = new List<int>();
        var dark = new List<int>();
        var horn = new List<int>();

        for (int s = 0; s < mesh.subMeshCount; s++)
        {
            var tris = mesh.GetTriangles(s);

            for (int i = 0; i + 2 < tris.Length; i += 3)
            {
                var p = (verts[tris[i]] + verts[tris[i + 1]] + verts[tris[i + 2]]) / 3f;
                var target = body;

                foreach (var eye in eyes)
                {
                    if ((p - eye.Center).magnitude > eyeReach) continue;

                    // Зрачок — шапочка на внешней макушке глаза, обращённая к лицу.
                    var tip = eye.Center + new Vector3(0f, 0f, faceSide * eyeReach * 0.55f);
                    target = (p - tip).magnitude < pupilReach ? dark : white;
                    break;
                }

                // Рожки идут своим подмешем, а не в общее тёмное: в чёрном они
                // читаются как пятна на лбу, а не как усики. Игра красит их тёмным
                // оттенком того же цвета уровня — как у процедурной жабы.
                if (target == body)
                    foreach (var h in horns)
                        if ((p - h.Center).magnitude < hornReach) { target = horn; break; }

                // Полоса рта: треть высоты, лицевая сторона, середина по ширине.
                if (target == body
                    && p.y > height * 0.26f && p.y < height * 0.38f
                    && p.z * faceSide > height * 0.20f
                    && Mathf.Abs(p.x) < height * 0.30f)
                    target = dark;

                target.Add(tris[i]);
                target.Add(tris[i + 1]);
                target.Add(tris[i + 2]);
            }
        }

        mesh.subMeshCount = 4;
        mesh.SetTriangles(body, 0);
        mesh.SetTriangles(white, 1);
        mesh.SetTriangles(dark, 2);
        mesh.SetTriangles(horn, 3);

        Debug.Log($"[GlbImport] Голова разобрана: тело {body.Count / 3}, "
                + $"белки {white.Count / 3}, тёмное {dark.Count / 3}, "
                + $"рожки {horn.Count / 3} треугольников; "
                + $"лицо смотрит в {(faceSide > 0 ? "+Z" : "-Z")}");

        return true;
    }

    [MenuItem("Frog Cart/Analyse Frog Head")]
    public static void AnalyseFrogHead()
    {
        var mesh = AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(
            $"{FrogCartPaths.Root()}/Resources/FrogHeadModel.asset");

        if (mesh == null) { Debug.LogError("[GlbImport] Нет FrogHeadModel"); return; }

        var b = mesh.bounds;
        Debug.Log($"[Анализ] габарит {b.size}, центр {b.center}, вершин {mesh.vertexCount}");

        foreach (float t in new[] { 1.00f, 1.02f, 1.05f, 1.10f })
        {
            var lumps = FindLumps(mesh, t);
            var top = lumps.Count > 6 ? lumps.GetRange(0, 6) : lumps;
            string text = string.Join("  ", top.ConvertAll(l =>
                $"[{l.Count} шт {l.Center.x:F2};{l.Center.y:F2};{l.Center.z:F2}]"));

            Debug.Log($"[Анализ] порог {t:F2}: групп {lumps.Count} -> {text}");
        }
    }

    /// <summary>
    /// Меш кладётся в Resources, чтобы он был доступен и в редакторе, и в собранном
    /// плеере без ссылок в сцене: сцена собирается кодом, и ссылку туда пришлось бы
    /// присваивать отдельным шагом.
    /// </summary>
    static void ImportFromDownloads(string fileName, string assetName,
                                    int clusterResolution = 0, bool headParts = false)
    {
        string source = System.IO.Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
            "Downloads", fileName);

        if (!File.Exists(source))
        {
            Debug.LogError($"[GlbImport] Нет файла: {source}");
            return;
        }

        var result = Read(source);

        int before = result.Mesh.vertexCount;
        if (clusterResolution > 0) Cluster(result.Mesh, clusterResolution);
        if (headParts) SplitHeadParts(result.Mesh);

        Directory.CreateDirectory($"{FrogCartPaths.Root()}/Resources");
        string output = $"{FrogCartPaths.Root()}/Resources/{assetName}.asset";

        if (AssetDatabase.LoadAssetAtPath<UnityEngine.Mesh>(output) != null)
            AssetDatabase.DeleteAsset(output);

        result.Mesh.name = assetName;
        AssetDatabase.CreateAsset(result.Mesh, output);
        AssetDatabase.SaveAssets();

        var bounds = result.Mesh.bounds;
        if (clusterResolution > 0)
            Debug.Log($"[GlbImport] {assetName}: прорежен {before} -> {result.Mesh.vertexCount} вершин, "
                    + $"треугольников {result.Mesh.triangles.Length / 3}");

        Debug.Log($"[GlbImport] {assetName}: вершин {result.Mesh.vertexCount}, "
                + $"подмешей {result.Mesh.subMeshCount}, габарит {bounds.size}, "
                + $"части: {string.Join(" | ", result.MaterialNames)}. Сохранено: {output}");
    }
}
