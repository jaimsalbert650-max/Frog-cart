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
        /// Очередь стоит вразброс и целиком, а не строем и не первыми пятнадцатью.
        ///
        /// Шеренгой она упиралась в ширину экрана, столбиками помещалось пятнадцать,
        /// но и то и другое читалось как склад, а не как толпа ждущих. А главное —
        /// остальные были не видны вовсе, хотя выбор игрока в том и состоит, чтобы
        /// смотреть, кто есть в запасе.
        ///
        /// Поэтому мест столько, сколько вагонеток на уровне, а раскладка —
        /// подрагивающая сетка: строгая сетка внутри, случайное на вид смещение
        /// поверх. Настоящей случайности здесь нет, смещение считается от номера
        /// места, иначе уровень выглядел бы по-разному при каждом запуске.
        /// </summary>
        /// <summary>
        /// Три вагонетки на всю ширину экрана — от неё и считается их размер.
        ///
        /// Прошлый заход растягивал под очередь кадр камеры, чтобы вместить все
        /// ряды сразу, и доска от этого теряла пятую часть размера. Здесь наоборот:
        /// кадр не трогаем, ряды уходят вниз за экран, а игрок видит ближние. Когда
        /// одну забирают, остальные подтягиваются — так очередь может быть любой
        /// длины и ничего не мельчает.
        /// </summary>
        const float AreaLeft = 18f;
        const float AreaRight = 372f;
        /// <summary>
        /// Передний ряд. Отодвинут от контура: на 664 крупные вагонетки садились
        /// прямо на нижнюю нитку рельса и жабы налезали на шпалы. Нижний край
        /// контура — 628, вагонетка вместе с жабой занимает около 50 вверх от
        /// своей точки, отсюда 700.
        /// </summary>
        const float AreaTop = 700f;

        /// <summary>
        /// Шаг вглубь. Взят под размер вагонетки, а не под высоту площадки: площадки
        /// с фиксированной высотой здесь больше нет, ряды уходят за экран.
        /// </summary>
        const float RowStep = 112f;

        /// <summary>
        /// Потолок на число мини-вагонеток. Не про вкус, а про здравый смысл:
        /// на 116 вагонетках уровня 283 их и так не разглядеть, а строить тысячу
        /// объектов из-за кривых данных не нужно.
        /// </summary>
        const int MaxMinis = 128;


        /// <summary>Цвет ледяной таблички — тот же, что у вагонеток на контуре.</summary>
        static readonly Color IceColor = new Color(0.71f, 0.90f, 0.98f, 1f);

        /// <summary>
        /// Столбиков всегда три, вглубь уходит столько рядов, сколько нужно.
        ///
        /// Закреплена именно ширина, а не глубина: ширина — это то, что игрок видит
        /// и к чему привыкает, а число вагонеток на уровне гуляет от 22 до 116.
        /// Раньше обе стороны считались из пропорций площадки, и раскладка менялась
        /// от уровня к уровню.
        /// </summary>
        const int Columns = 3;

        static void GridFor(int places, out int columns, out int rows)
        {
            places = Mathf.Max(places, 1);

            columns = Columns;
            rows = Mathf.Max(1, Mathf.CeilToInt(places / (float)columns));
        }

        /// <summary>
        /// Место в spec-координатах — центр клетки ровной сетки.
        ///
        /// Пробовал разброс: живее, но таблички с ёмкостью наезжали друг на друга,
        /// а числа на них и есть весь смысл очереди. Ровные ряды читаются лучше и
        /// совпадают с подачей оригинала.
        ///
        /// Раскладка берётся от **начального** числа вагонеток уровня, а не от
        /// текущего. Иначе каждый забранный кубик пересчитывал бы всю сетку, и она
        /// перекладывалась бы целиком на каждый ход; так же места просто сдвигаются
        /// на одно, и задние подходят ближе.
        /// </summary>
        static Vector2 SlotPos(int index, int places)
        {
            GridFor(places, out int columns, out _);

            float cellW = (AreaRight - AreaLeft) / columns;

            int column = index % columns;
            int row = index / columns;

            return new Vector2(AreaLeft + (column + 0.5f) * cellW,
                               AreaTop + row * RowStep);
        }

        /// <summary>
        /// Мельче ли делать вагонетки, чтобы они помещались. На двух десятках
        /// масштаб остаётся единичным, на сотне с лишним — падает, иначе толпа
        /// слипается в кашу.
        /// </summary>
        /// <summary>
        /// Размер вагонетки. Задаётся шириной столбика, а не подгоняется под число
        /// вагонеток: их размер не должен зависеть от того, длинная очередь или
        /// короткая. Верхний предел снят — раньше стояла единица, и вагонетки не
        /// могли вырасти, сколько бы места им ни дали.
        /// </summary>
        static float MiniScale(int places)
        {
            GridFor(places, out int columns, out _);

            float cellW = (AreaRight - AreaLeft) / columns;

            // 56 на 52 — габарит мини-вагонетки вместе с табличкой.
            return Mathf.Clamp(Mathf.Min(cellW / 56f, RowStep / 52f), 0.35f, 3f);
        }

        static int VisibleCount(List<LevelData.CartDef> queue, int startIndex)
            => Mathf.Clamp(queue.Count - startIndex, 0, MaxMinis);

        sealed class Mini
        {
            public Transform Root;
            public MeshRenderer Stripe;
            public MeshRenderer Head;

            // Какие слоты материалов головы красятся цветом уровня. У шара слот
            // один, у модели — тело и рожки: белки и зрачки обязаны остаться
            // белыми и чёрными при любом цвете.
            public int HeadBodySlot;
            public int HeadHornSlot = -1;

            public Text Count;
            public GameObject Ice;
        }

        readonly List<Mini> _minis = new List<Mini>();

        /// <summary>
        /// Сколько мест в раскладке. Берётся по самой длинной очереди, какую
        /// показывали, и больше не уменьшается: раскладка обязана быть устойчивой,
        /// иначе толпа перетасовывается на каждый ход.
        /// </summary>
        int _places;
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

            // Дока больше нет: короткий рельс имел смысл под шеренгой, а под толпой
            // вразброс он превращался в доску, торчащую из-под ног у половины.
            // Вагонетки стоят прямо на столе, как и полагается ждущим.
        }

        /// <summary>
        /// Мини-вагонетки строятся под фактическую длину очереди, а не под
        /// фиксированное число мест: сколько вагонеток у уровня, столько и видно.
        /// </summary>
        void EnsurePool(int count)
        {
            while (_minis.Count < count) _minis.Add(BuildMini(_minis.Count));
        }

        Mini BuildMini(int index)
        {
            var mini = new Mini();

            var root = new GameObject($"QueueCart_{index}").transform;
            root.SetParent(_root, false);
            root.position = Space3D.ToWorld(SlotPos(index, Mathf.Max(_places, index + 1)));
            mini.Root = root;

            // Корпус ужат под шаг 42: при прежних 1.2 вагонетка шириной 53
            // налезала бы на соседнюю, а очередь теперь длиннее.
            float bulk = 0.82f;

            // Корпус из готовой модели, если она есть, — иначе очередь выглядела бы
            // иначе, чем контур, где модель уже стоит.
            var model = Resources.Load<Mesh>("CartModel");

            if (model != null)
            {
                var go = new GameObject("CartModel", typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(root, false);
                go.GetComponent<MeshFilter>().sharedMesh = model;
                go.transform.localScale = Vector3.one * Space3D.Size(44f * bulk);

                // Тот же разворот, что на контуре: у вагона длинная сторона — глубина.
                go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

                var wood = ProcMesh.Glossy(ProcSprite.Hex("9C6231"), "mat_cartModelBody");
                var metal = ProcMesh.Metal(ProcSprite.Hex("6A7178"), "mat_wheel");

                var parts = new Material[model.subMeshCount];
                for (int i = 0; i < parts.Length; i++) parts[i] = i == 0 ? wood : metal;

                go.GetComponent<MeshRenderer>().sharedMaterials = parts;
            }

            var body = NewPiece("Body", root,
                ProcMesh.RoundedBox(Space3D.Size(44f * bulk), Space3D.Size(20f * bulk),
                                    Space3D.Size(27f * bulk), Space3D.Size(6f), "queueBody3D"),
                ProcMesh.Glossy(ProcSprite.Hex("9C6231"), "mat_cartBody"));
            body.transform.localPosition = new Vector3(0f, Space3D.Size(11f * bulk), 0f);

            if (model != null) body.GetComponent<MeshRenderer>().enabled = false;

            var stripe = NewPiece("Stripe", root,
                ProcMesh.RoundedBox(Space3D.Size(38f * bulk), Space3D.Size(3f),
                                    Space3D.Size(28f * bulk), Space3D.Size(2f), "queueStripe3D"),
                ProcMesh.Glossy(Color.white, "mat_cartStripe"));
            stripe.transform.localPosition = new Vector3(0f, Space3D.Size(2.5f * bulk), 0f);
            mini.Stripe = stripe.GetComponent<MeshRenderer>();

            // Колёса рисуются только у процедурного корпуса: у модели они свои.
            if (model == null)
                foreach (float x in new[] { -13f, 13f })
                foreach (float z in new[] { -8f, 8f })
                    NewWheel(root, new Vector3(Space3D.Size(x * bulk), Space3D.Size(6f * bulk),
                                               Space3D.Size(z * bulk)), bulk);

            // Голова жабы над корпусом — по ней цвет очереди читается сразу.
            //
            // Та же голова, что на контуре, если она лежит в Resources. Очередь —
            // это те же персонажи, просто ещё не выехавшие, и шар с приделанными
            // глазами рядом с вылепленной головой читался не стилизацией, а
            // недоделкой. Файла нет — собирается прежний шар, как и у вагонетки:
            // проект обязан оставаться работоспособным без внешних ассетов.
            var headModel = Resources.Load<Mesh>("FrogHeadModel");

            if (headModel != null) BuildModelHead(mini, root, headModel, bulk);
            else BuildSphereHead(mini, root, bulk);

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

        /// <summary>
        /// Голова из модели — та же, что у жаб на контуре.
        ///
        /// Размер взят не на глаз, а от контура: там голова 46 при вагонетке 66,
        /// здесь вагонетка 44*bulk, и та же доля даёт голову. Иначе один и тот же
        /// персонаж стоял бы в очереди и на рельсе в разных пропорциях.
        ///
        /// Высота — по низу прежнего шара, а не по его центру: у модели начало
        /// координат внизу головы, и посадка по центру подняла бы её над корпусом.
        ///
        /// Разворот на 180 — тот же, что в <see cref="Frog3DView"/>: у модели лицо
        /// смотрит в +Z, а перёд в этом проекте — это -Z.
        /// </summary>
        static void BuildModelHead(Mini mini, Transform root, Mesh mesh, float bulk)
        {
            var go = new GameObject("Head", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            go.transform.localScale = Vector3.one * Space3D.Size(46f * bulk / 1.5f);
            go.transform.localPosition =
                new Vector3(0f, Space3D.Size(30f * bulk) - Space3D.Size(11f), Space3D.Size(-4f));
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            // Порядок подмешей задаёт импортёр: тело, белки глаз, тёмное — зрачки,
            // рожки и полоса рта, — и сами рожки.
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterials = new[]
            {
                ProcMesh.Glossy(Color.green, "mat_frogHeadBody"),
                ProcMesh.Glossy(Color.white, "mat_frogHeadEye", 0.8f),
                ProcMesh.Glossy(ProcSprite.Hex("1D2127"), "mat_frogHeadDark", 0.9f),
                ProcMesh.Glossy(Color.green, "mat_frogHeadHorn"),
            };

            mini.Head = renderer;
            mini.HeadBodySlot = 0;
            mini.HeadHornSlot = 3;
        }

        /// <summary>
        /// Прежняя голова из шара с приделанными глазами и усиками — запасной
        /// вариант на случай, когда модели в Resources нет.
        /// </summary>
        static void BuildSphereHead(Mini mini, Transform root, float bulk)
        {
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(root, false);
            head.transform.localPosition = new Vector3(0f, Space3D.Size(30f * bulk), Space3D.Size(-4f));
            head.transform.localScale = new Vector3(Space3D.Size(26f), Space3D.Size(22f), Space3D.Size(24f));

            mini.Head = head.GetComponent<MeshRenderer>();
            mini.HeadBodySlot = 0;
            mini.HeadHornSlot = -1;

            // Глаза и усики: рядом с жабами на контуре, у которых есть и то и другое,
            // голый шар читался заготовкой.
            foreach (float side in new[] { -1f, 1f })
            {
                BuildQueueEye(root, side, bulk);
                BuildQueueAntenna(root, side, bulk);
            }
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
        /// Номер слота под точкой, или -1. Коллайдеров у очереди нет, поэтому
        /// попадание считается по расстоянию до места.
        ///
        /// Ищем ближайшее, а не первое подходящее: места стоят вразброс и на плотной
        /// толпе перекрываются, и «первое в списке» отдавало бы соседа, а не того,
        /// по кому ткнули.
        /// </summary>
        public int SlotAt(Vector2 spec, float radius)
        {
            int best = -1;
            float bestDistance = radius * radius;

            for (int i = 0; i < _shown; i++)
            {
                float distance = Vector2.SqrMagnitude(spec - SlotPos(i, _places));
                if (distance > bestDistance) continue;

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        public void Rebuild(List<LevelData.CartDef> queue, int startIndex)
        {
            int count = VisibleCount(queue, startIndex);
            _shown = count;

            // Раскладка запоминается по самой длинной очереди: она не должна
            // пересчитываться на каждый забранный кубик, иначе толпа перетасуется.
            _places = Mathf.Max(_places, count);
            EnsurePool(count);

            float scale = MiniScale(_places);

            for (int i = 0; i < _minis.Count; i++)
            {
                int source = startIndex + i;
                bool has = source < queue.Count && i < count;

                _minis[i].Root.gameObject.SetActive(has);
                _minis[i].Root.position = Space3D.ToWorld(SlotPos(i, _places));
                _minis[i].Root.localScale = Vector3.one * scale;

                if (!has) continue;

                var def = queue[source];
                var entry = _palette.Get(def.colorId);

                _minis[i].Stripe.sharedMaterial =
                    ProcMesh.Glossy(entry.baseColor, $"mat_stripe{def.colorId}");
                // sharedMaterials отдаёт копию массива, и правка элемента на месте
                // до рендерера не доходит — массив нужно положить обратно целиком.
                var headMaterial = ProcMesh.Glossy(entry.baseColor, $"mat_frogHead{def.colorId}");
                var headParts = _minis[i].Head.sharedMaterials;

                headParts[_minis[i].HeadBodySlot] = headMaterial;

                if (_minis[i].HeadHornSlot >= 0)
                    headParts[_minis[i].HeadHornSlot] = headMaterial;

                _minis[i].Head.sharedMaterials = headParts;
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
        /// <summary>
        /// Выезд выбранной вагонетки из сетки на контур.
        ///
        /// Место в сетке освобождается сразу, поэтому едет не сам слот, а его копия:
        /// иначе выезжающая вагонетка и подтягивающиеся соседи спорили бы за один и
        /// тот же объект, и на середине хода она прыгнула бы на чужое место.
        /// </summary>
        /// <summary>
        /// Сколько вагонеток сейчас в пути из очереди на контур.
        ///
        /// Открыто ради проверки. Поймать выезд снимком экрана не вышло: он длится
        /// 0.3 c, а снималка берёт один кадр, и попасть в него не удалось ни разу
        /// за несколько попыток. Счётчик проверяется тестом, и это надёжнее кадра.
        /// </summary>
        public int DepartingCount { get; private set; }

        public float Depart(int index, Vector2 toSpec, float toAngleDeg, float duration)
        {
            if (index < 0 || index >= _minis.Count) return 0f;

            // Уезжает сама мини-вагонетка, а не её копия: копию пришлось бы держать
            // поверх пула, а пул тут же перекладывается — два объекта спорили бы за
            // одно место. Выведенная из списка вагонетка Rebuild-у больше не видна,
            // поэтому спокойно доигрывает свой выезд.
            var leaving = _minis[index];
            _minis.RemoveAt(index);

            Vector3 from = leaving.Root.position;
            Vector3 to = Space3D.ToWorld(toSpec);
            float fromScale = leaving.Root.localScale.x;

            DepartingCount++;
            _departingRoot = leaving.Root;

            // Сглаживание здесь линейное, и это не мелочь. QueueShift — кривая с
            // перелётом (cubic-bezier(.3, 1.4, .5, 1)), она заходит за единицу.
            // Для сдвига в ряду перелёт и нужен, а здесь по нему считается высота
            // дуги: sin(t*PI) при t > 1 уходит в минус, дуга схлопывается, и
            // вагонетка вместо полёта ныряет под доску. На снимках это выглядело
            // так, будто выезда нет вовсе.
            var toRotation = Space3D.RotationFromSpecAngle(toAngleDeg);

            _tween.Run(duration, null,
                t =>
                {
                    if (leaving.Root == null) return;

                    // По горизонтали — с замедлением к концу: бросок начинается
                    // резко и мягко доводится до места. Равномерное движение
                    // читалось перемещением фишки, а не выездом.
                    float travel = 1f - (1f - t) * (1f - t);

                    // Высота дуги зажата с двух сторон, и обе границы настоящие.
                    // Снизу — доска: путь к дальнему месту проходит прямо над ней.
                    // Сверху — кадрируемый объём, у него потолок 112 (11.2 мира),
                    // и выше этого вагонетка уходит за верхний край экрана.
                    Vector3 point = Vector3.Lerp(from, to, travel);
                    point.y += Mathf.Sin(t * Mathf.PI) * Space3D.Size(90f);
                    leaving.Root.position = point;

                    // Размер сводится к размеру вагонетки на контуре, а не к нулю.
                    // Раньше она истончалась до 0.15 — это пряталось от того, что
                    // на рельсе уже стояла вторая такая же. Теперь та появляется
                    // только в момент приземления, и прятать нечего.
                    float scale = Mathf.Lerp(fromScale, 1f, travel);

                    // Приседание на взлёте и на приземлении: по вертикали сжимается,
                    // по горизонтали расходится. Это и читается как «оттолкнулась».
                    float crouch = Mathf.Sin(t * Mathf.PI * 2f) * 0.12f;
                    leaving.Root.localScale = new Vector3(scale * (1f + crouch),
                                                          scale * (1f - crouch),
                                                          scale * (1f + crouch));

                    // Доворот к направлению рельса — вагонетка приземляется вдоль
                    // пути, а не поперёк. Плюс задранный нос в полёте, который
                    // выравнивается к посадке.
                    leaving.Root.rotation =
                        Quaternion.Slerp(Quaternion.identity, toRotation, travel)
                      * Quaternion.Euler(-Mathf.Sin(t * Mathf.PI) * 16f, 0f, 0f);
                },
                () =>
                {
                    DepartingCount--;
                    if (leaving.Root != null) Object.Destroy(leaving.Root.gameObject);
                });

            return duration;
        }

        /// <summary>Где сейчас уезжающая вагонетка — для проверки, что она правда едет.</summary>
        Transform _departingRoot;

        public Vector3 DepartingPos => _departingRoot != null ? _departingRoot.position : Vector3.zero;

        public void Shift(List<LevelData.CartDef> queue, int startIndex, float duration)
        {
            int pool = _minis.Count;
            var from = new Vector3[pool];
            for (int i = 0; i < pool; i++) from[i] = _minis[i].Root.position;

            _tween.Run(duration, Tweener.QueueShift,
                t =>
                {
                    for (int i = 0; i < pool; i++)
                    {
                        // Каждый занимает место предыдущего: забранный уходит вперёд,
                        // остальные подтягиваются на одно. Раскладка при этом не
                        // пересчитывается, поэтому подходят ближе именно соседи, а не
                        // перетасовывается вся толпа.
                        Vector3 target = Space3D.ToWorld(SlotPos(i - 1, _places));
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
