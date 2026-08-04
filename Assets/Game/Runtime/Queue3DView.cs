using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Очередь вагонеток в объёме: они стоят на отдельном коротком рельсе перед доской,
    /// ближе к камере, и ждут своей очереди выехать на контур.
    ///
    /// Плоский док на Canvas был последним, что оставалось нарисованным, а не построенным.
    /// Здесь те же числа раскладки из 01-layout.md — шаг 88, пятая уезжает за край, —
    /// только в мировых координатах.
    /// </summary>
    public sealed class Queue3DView : MonoBehaviour, IQueueView
    {
        /// <summary>
        /// Мест в ряду очереди. Было пять — по числу вагонеток на контуре из спеки.
        /// Теперь вагонетки уровня целиком лежат в очереди, и на уровне 106 их семь,
        /// поэтому ряд рассчитан на девять: столько же, сколько цветов в палитре.
        /// </summary>
        /// <summary>
        /// Очередь стоит столбиками, а не одной шеренгой.
        ///
        /// Шеренгой она упиралась в ширину экрана: девять мест по 42 занимали
        /// почти всю его ширину, и больше уже не помещалось. Столбиками на том же
        /// месте помещается пятнадцать, и видно, что за передними кто-то стоит —
        /// когда переднюю забирают, задние подъезжают ближе.
        /// </summary>
        const int Columns = 5;
        const int Depth = 3;
        const int Visible = Columns * Depth;

        const float SlotY = 664f;    // spec-координата переднего ряда очереди
        const float Step = 56f;      // шаг между столбиками
        /// <summary>
        /// Шаг вглубь. Считается не от корпуса вагонетки, а от таблички с ёмкостью:
        /// она висит над вагонеткой, и при шаге по корпусу (42) её закрывала соседка
        /// из ближнего ряда — числа читались только у последнего ряда, то есть
        /// очередь переставала быть выбором.
        /// </summary>
        const float RowStep = 56f;
        const float DockCenterX = 195f;

        /// <summary>Цвет ледяной таблички — тот же, что у вагонеток на контуре.</summary>
        static readonly Color IceColor = new Color(0.71f, 0.90f, 0.98f, 1f);

        /// <summary>
        /// Место слота на доке. Ряд центрируется по числу занятых слотов, а не
        /// выкладывается от левого края: на уровне с тремя вагонетками в очереди
        /// ряд жался к левому краю и выглядел обрубленным.
        /// </summary>
        /// <summary>
        /// Место слота в spec-координатах. Заполнение идёт **по столбикам**:
        /// первые <see cref="Depth"/> мест — передний столбик сверху вниз, затем
        /// следующий. Так забранная вагонетка освобождает место в своём столбике,
        /// и задние подъезжают вперёд, а не разъезжаются по всей ширине.
        /// </summary>
        static Vector2 SlotPos(int index, int count)
        {
            int column = index / Depth;
            int row = index % Depth;

            return new Vector2(DockCenterX - (UsedColumns(count) - 1) * Step * 0.5f + column * Step,
                               SlotY + row * RowStep);
        }

        /// <summary>Сколько столбиков занято — очередь центруется по ним, а не по пустым.</summary>
        static int UsedColumns(int count)
            => Mathf.Clamp(Mathf.CeilToInt(count / (float)Depth), 1, Columns);

        /// <summary>Сколько мест занято в конкретном столбике.</summary>
        static int RowsInColumn(int column, int count)
            => Mathf.Clamp(count - column * Depth, 0, Depth);

        static int VisibleCount(List<LevelData.CartDef> queue, int startIndex)
            => Mathf.Clamp(queue.Count - startIndex, 0, Visible);

        sealed class Mini
        {
            public Transform Root;
            public MeshRenderer Stripe;
            public MeshRenderer Head;
            public Text Count;
            public GameObject Ice;
        }

        readonly Mini[] _minis = new Mini[Visible];
        readonly Dictionary<Text, Image> _plateImages = new Dictionary<Text, Image>();
        ColorPalette _palette;
        Tweener _tween;
        Camera _camera;
        Transform _root;

        public void Build(Transform parent, ColorPalette palette, Tweener tween, Camera camera)
        {
            _palette = palette;
            _tween = tween;
            _camera = camera;

            _root = new GameObject("Queue3D").transform;
            _root.SetParent(parent, false);

            BuildDockRail();

            for (int i = 0; i < Visible; i++) _minis[i] = BuildMini(i);
        }

        /// <summary>
        /// Короткий прямой рельс под очередью — чтобы вагонетки не висели в воздухе.
        ///
        /// Меши строятся на условную длину 400, а реальная длина задаётся масштабом
        /// в <see cref="FitDock"/>: док обязан быть ровно под занятыми слотами.
        /// Фиксированные 400 торчали из-под контура рельсов пустой доской.
        /// </summary>
        const float DockMeshLength = 400f;

        /// <summary>Части дока по рядам: свой отрезок пути под каждым рядом очереди.</summary>
        readonly List<Transform>[] _dockRows = new List<Transform>[Depth];

        void BuildDockRail()
        {
            for (int row = 0; row < Depth; row++)
            {
                _dockRows[row] = new List<Transform>();
                float y = SlotY + row * RowStep + 6f;

                var plank = NewPiece($"DockPlank{row}", _root,
                    ProcMesh.RoundedBox(Space3D.Size(DockMeshLength), Space3D.Size(4f), Space3D.Size(34f),
                                        Space3D.Size(3f), "dockPlank3D"),
                    ProcMesh.Glossy(ProcSprite.Hex("9A6A38"), "mat_dockPlank", 0.1f));
                plank.transform.position = Space3D.ToWorld(DockCenterX, y, Space3D.Size(1f));
                _dockRows[row].Add(plank.transform);

                foreach (float offset in new[] { -8f, 8f })
                {
                    var rail = NewPiece($"DockRail{row}_{offset}", _root,
                        ProcMesh.RoundedBox(Space3D.Size(DockMeshLength), Space3D.Size(3f), Space3D.Size(3f),
                                            Space3D.Size(1f), "dockRail3D"),
                        ProcMesh.Metal(ProcSprite.Hex("CFD6DA"), "mat_rail"));
                    rail.transform.position =
                        Space3D.ToWorld(DockCenterX, y + offset, Space3D.Size(3f));
                    _dockRows[row].Add(rail.transform);
                }
            }

            FitDock(Visible);
        }

        /// <summary>Сколько столбиков дотягиваются до этого ряда.</summary>
        static int ColumnsInRow(int row, int count)
            => count > row ? (count - 1 - row) / Depth + 1 : 0;

        /// <summary>
        /// Каждый ряд дока подгоняется под свои занятые места и прячется, когда их
        /// нет. Фиксированная длина торчала бы из-под очереди пустой доской —
        /// на неполном ряду это особенно заметно.
        /// </summary>
        void FitDock(int count)
        {
            for (int row = 0; row < Depth; row++)
            {
                int columns = ColumnsInRow(row, count);
                bool has = columns > 0;

                float length = has ? (columns - 1) * Step + 68f : 0f;
                float centerX = has
                    ? (SlotPos(row, count).x + SlotPos(row + (columns - 1) * Depth, count).x) * 0.5f
                    : DockCenterX;

                foreach (var part in _dockRows[row])
                {
                    part.gameObject.SetActive(has);
                    part.localScale = new Vector3(length / DockMeshLength, 1f, 1f);

                    var position = part.position;
                    position.x = Space3D.ToWorld(centerX, 0f).x;
                    part.position = position;
                }
            }
        }

        Mini BuildMini(int index)
        {
            var mini = new Mini();

            var root = new GameObject($"QueueCart_{index}").transform;
            root.SetParent(_root, false);
            root.position = Space3D.ToWorld(SlotPos(index, Visible));
            mini.Root = root;

            // Корпус ужат под шаг 42: при прежних 1.2 вагонетка шириной 53
            // налезала бы на соседнюю, а очередь теперь длиннее.
            float bulk = 0.82f;

            var body = NewPiece("Body", root,
                ProcMesh.RoundedBox(Space3D.Size(44f * bulk), Space3D.Size(20f * bulk),
                                    Space3D.Size(27f * bulk), Space3D.Size(6f), "queueBody3D"),
                ProcMesh.Glossy(ProcSprite.Hex("9C6231"), "mat_cartBody"));
            body.transform.localPosition = new Vector3(0f, Space3D.Size(11f * bulk), 0f);

            var stripe = NewPiece("Stripe", root,
                ProcMesh.RoundedBox(Space3D.Size(38f * bulk), Space3D.Size(3f),
                                    Space3D.Size(28f * bulk), Space3D.Size(2f), "queueStripe3D"),
                ProcMesh.Glossy(Color.white, "mat_cartStripe"));
            stripe.transform.localPosition = new Vector3(0f, Space3D.Size(2.5f * bulk), 0f);
            mini.Stripe = stripe.GetComponent<MeshRenderer>();

            foreach (float x in new[] { -13f, 13f })
            foreach (float z in new[] { -8f, 8f })
                NewWheel(root, new Vector3(Space3D.Size(x * bulk), Space3D.Size(6f * bulk),
                                           Space3D.Size(z * bulk)), bulk);

            // Голова жабы над корпусом — по ней цвет очереди читается сразу.
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, Space3D.Size(30f * bulk), Space3D.Size(-4f));
            head.transform.localScale = new Vector3(Space3D.Size(26f), Space3D.Size(22f), Space3D.Size(24f));
            mini.Head = head.GetComponent<MeshRenderer>();

            // Глаза и усики: рядом с жабами на контуре, у которых есть и то и другое,
            // голый шар читался заготовкой. Очередь — это те же персонажи, просто
            // ещё не выехавшие.
            foreach (float side in new[] { -1f, 1f })
            {
                BuildQueueEye(root, side, bulk);
                BuildQueueAntenna(root, side, bulk);
            }

            // Ледяная глыба поверх корпуса — та же, что у вагонеток на контуре.
            var ice = NewPiece("Ice", root,
                ProcMesh.RoundedBox(Space3D.Size(50f * bulk), Space3D.Size(40f * bulk),
                                    Space3D.Size(32f * bulk), Space3D.Size(5f), "queueIce3D"),
                ProcMesh.Glossy(ProcSprite.Hex("BEE6F5"), "mat_cartIce", 0.55f));
            ice.transform.localPosition = new Vector3(0f, Space3D.Size(4f), 0f);
            ice.SetActive(false);
            mini.Ice = ice;

            mini.Count = BuildPlate(root, bulk);

            return mini;
        }

        Text BuildPlate(Transform parent, float bulk)
        {
            var plateGo = new GameObject("Plate", typeof(Canvas));
            plateGo.transform.SetParent(parent, false);
            // Табличка наклонена на 50°, поэтому её нижняя кромка уходит назад примерно
            // на 15 единиц. При посадке вплотную к корпусу та кромка тонула в нём,
            // и нижняя половина цифры пропадала. Выносим вперёд с запасом.
            plateGo.transform.localPosition = new Vector3(0f, Space3D.Size(12f * bulk), Space3D.Size(-29f));

            plateGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)plateGo.transform;
            rt.sizeDelta = new Vector2(40f, 28f);
            rt.localScale = Vector3.one * Space3D.Scale * 1.05f;
            rt.localRotation = Quaternion.Euler(50f, 0f, 0f);

            // Тот же зажим-язычок, что у счётчиков на контуре: счётчик обязан
            // выглядеть одинаково, где бы он ни стоял.
            var clip = new GameObject("Clip", typeof(RectTransform), typeof(Image));
            clip.transform.SetParent(plateGo.transform, false);
            var clipRt = (RectTransform)clip.transform;
            clipRt.anchorMin = new Vector2(0.5f, 1f);
            clipRt.anchorMax = new Vector2(0.5f, 1f);
            clipRt.pivot = new Vector2(0.5f, 0f);
            clipRt.sizeDelta = new Vector2(15f, 9f);
            clipRt.anchoredPosition = new Vector2(0f, -4f);
            var clipImage = clip.GetComponent<Image>();
            clipImage.sprite = ProcSprite.Make(
                ProcSprite.Rounded.Flat(15, 9, 3f, Color.white, "queueClip3D"));
            clipImage.color = ProcSprite.Hex("4A4F57");

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(plateGo.transform, false);
            var backRt = (RectTransform)background.transform;
            backRt.anchorMin = Vector2.zero;
            backRt.anchorMax = Vector2.one;
            backRt.offsetMin = Vector2.zero;
            backRt.offsetMax = Vector2.zero;

            // Белый спрайт красится в цвет вагонетки через Image.color: на референсе
            // счётчик — цветной «мешок» с крупной белой цифрой, а не белая табличка
            // с мелкой цветной. Цвет назначается в Rebuild вместе с остальным.
            var image = background.GetComponent<Image>();
            image.sprite = ProcSprite.Make(
                ProcSprite.Rounded.Flat(40, 28, 9f, Color.white, "queuePlate3D"));

            // Текст с полями внутри таблички: без них у крупного кегля подрезало
            // нижние выносные элементы краем фона.
            var text = UiText.Create("Count", rt, 22, Color.white);
            var textRt = (RectTransform)text.transform;
            textRt.offsetMin = new Vector2(3f, 4f);
            textRt.offsetMax = new Vector2(-3f, -2f);

            // Обводка: белая цифра на жёлтом и на бежевом иначе теряется.
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            _plateImages.Add(text, image);
            return text;
        }

        /// <summary>Глаз с зрачком и бликом — те же три сферы, что у жаб на контуре.</summary>
        static void BuildQueueEye(Transform parent, float side, float bulk)
        {
            var eye = NewSphere("Eye", parent, Space3D.Size(9f));
            eye.transform.localPosition = new Vector3(
                side * Space3D.Size(5.5f), Space3D.Size(38f * bulk / 1.2f), Space3D.Size(-13f));
            eye.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Glossy(Color.white, "mat_frogEye", 0.8f);

            var pupil = NewSphere("Pupil", eye.transform, 1f);
            pupil.transform.localPosition = new Vector3(0f, 0f, -0.32f);
            pupil.transform.localScale = Vector3.one * 0.5f;
            pupil.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Glossy(ProcSprite.Hex("1D2127"), "mat_frogPupil", 0.9f);

            var glint = NewSphere("Glint", pupil.transform, 1f);
            glint.transform.localPosition = new Vector3(-0.24f, 0.26f, -0.34f);
            glint.transform.localScale = Vector3.one * 0.42f;
            glint.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Emissive(Color.white, "mat_frogGlint");
        }

        static void BuildQueueAntenna(Transform parent, float side, float bulk)
        {
            var root = new GameObject("Antenna").transform;
            root.SetParent(parent, false);
            root.localPosition = new Vector3(
                side * Space3D.Size(7f), Space3D.Size(42f * bulk / 1.2f), Space3D.Size(-2f));
            root.localRotation = Quaternion.Euler(-16f, 0f, -side * 32f);

            var stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stalk.name = "Stalk";
            Destroy(stalk.GetComponent<Collider>());
            stalk.transform.SetParent(root, false);
            stalk.transform.localScale =
                new Vector3(Space3D.Size(2.4f), Space3D.Size(8f), Space3D.Size(2.4f));
            stalk.transform.localPosition = new Vector3(0f, Space3D.Size(8f), 0f);
            stalk.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Glossy(ProcSprite.Hex("4A4038"), "mat_queueAntenna", 0.2f);

            var tip = NewSphere("Tip", root, Space3D.Size(8f));
            tip.transform.localPosition = new Vector3(0f, Space3D.Size(17f), 0f);
            tip.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Glossy(ProcSprite.Hex("4A4038"), "mat_queueAntenna", 0.2f);
        }

        static GameObject NewSphere(string name, Transform parent, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Destroy(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;

            return go;
        }

        static void NewWheel(Transform parent, Vector3 localPosition, float bulk)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Wheel";
            Destroy(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            go.transform.localScale = new Vector3(Space3D.Size(11f * bulk), Space3D.Size(4f),
                                                  Space3D.Size(11f * bulk));

            go.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Metal(ProcSprite.Hex("6A7178"), "mat_wheel");
        }

        /// <summary>Сколько слотов сейчас занято — нужно вводу для попадания.</summary>
        int _shown;

        /// <summary>
        /// Номер слота под точкой, или -1. Считается по расстоянию до центра слота:
        /// коллайдеров у очереди нет, а слоты стоят ровным рядом с известным шагом.
        /// </summary>
        public int SlotAt(Vector2 spec, float radius)
        {
            for (int i = 0; i < _shown; i++)
            {
                var center = SlotPos(i, _shown);
                if (Vector2.SqrMagnitude(spec - center) <= radius * radius) return i;
            }

            return -1;
        }

        public void Rebuild(List<LevelData.CartDef> queue, int startIndex)
        {
            int count = VisibleCount(queue, startIndex);
            _shown = count;
            FitDock(count);

            for (int i = 0; i < Visible; i++)
            {
                int source = startIndex + i;
                bool has = source < queue.Count;

                _minis[i].Root.gameObject.SetActive(has);
                _minis[i].Root.position = Space3D.ToWorld(SlotPos(i, count));

                if (!has) continue;

                var def = queue[source];
                var entry = _palette.Get(def.colorId);

                _minis[i].Stripe.sharedMaterial =
                    ProcMesh.Glossy(entry.baseColor, $"mat_stripe{def.colorId}");
                _minis[i].Head.sharedMaterial =
                    ProcMesh.Glossy(entry.baseColor, $"mat_frogHead{def.colorId}");
                // Замороженная вагонетка обязана быть видна ещё в очереди: вся суть
                // механики в том, чтобы игрок заранее знал, что подкрепление придёт
                // не сразу. Показываем счётчик льда вместо ёмкости, как на контуре.
                bool frosted = def.frozenCount > 0;

                _minis[i].Count.text = frosted
                    ? def.frozenCount.ToString()
                    : def.capacity.ToString();

                _minis[i].Count.color = frosted ? ProcSprite.Hex("13415C") : Color.white;

                if (_plateImages.TryGetValue(_minis[i].Count, out var plate))
                    plate.color = frosted ? IceColor : entry.baseColor;

                _minis[i].Ice.SetActive(frosted);
            }
        }

        /// <summary>Сдвиг влево за 0.30 c с ease back, затем пересборка — как в спеке.</summary>
        public void Shift(List<LevelData.CartDef> queue, int startIndex, float duration)
        {
            var from = new Vector3[Visible];
            for (int i = 0; i < Visible; i++) from[i] = _minis[i].Root.position;

            int count = VisibleCount(queue, startIndex);

            _tween.Run(duration, Tweener.QueueShift,
                t =>
                {
                    for (int i = 0; i < Visible; i++)
                    {
                        // Слот уезжает на место предыдущего: нулевой уходит вперёд
                        // с дока, остальные подтягиваются — задние в своём столбике
                        // подъезжают ближе, а первый из следующего столбика
                        // перебирается в освободившийся хвост предыдущего.
                        Vector3 target = Space3D.ToWorld(SlotPos(i - 1, count));
                        _minis[i].Root.position = Vector3.Lerp(from[i], target, t);
                    }
                },
                () => Rebuild(queue, startIndex));
        }

        static GameObject NewPiece(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            return go;
        }
    }
}
