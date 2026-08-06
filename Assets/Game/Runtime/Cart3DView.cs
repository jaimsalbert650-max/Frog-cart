using UnityEngine;
using UnityEngine.UI;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Вагонетка объёмом: корпус-брусок, четыре колеса-цилиндра, табличка с числом.
    ///
    /// Число живёт на маленьком мировом Canvas: рисовать цифры геометрией — отдельная
    /// работа без выигрыша, а табличка всё равно обязана смотреть на камеру, иначе
    /// на дальней стороне контура число читалось бы зеркально.
    /// </summary>
    public sealed class Cart3DView : MonoBehaviour, ICartView
    {
        Transform _root;

        /// <summary>
        /// Сама вагонетка — всё, что скрывается, когда место стоит пустым.
        ///
        /// Отдельный слой под <see cref="_root"/> нужен из-за метки. Раньше
        /// прятали `_root`, но на нём же держится и метка свободного места:
        /// гасить их вместе означало бы, что свободное место не видно вовсе.
        /// Теперь `_root` только едет по контуру, а выключается этот слой.
        /// </summary>
        Transform _solid;

        /// <summary>Метка свободного места: венец из светящихся точек.</summary>
        Transform _marker;
        Transform[] _markerDots;

        /// <summary>Видна ли сейчас метка свободного места. Открыто ради проверки.</summary>
        public bool ShowingEmptyPlace => _marker != null && _marker.gameObject.activeSelf;

        Transform _body;
        Transform _plate;
        Transform _stripe;
        MeshRenderer _stripeRenderer;
        Image _plateImage;
        Text _countText;
        Transform _ice;
        Transform _link;
        Color _plateColor = Color.white;
        int _colorId;
        int _count;
        int _frozen;
        Camera _camera;
        Tweener _tweener;

        readonly Renderer[] _fadeTargets = new Renderer[8];
        int _fadeCount;

        /// <summary>Собрана ли вагонетка из готовой модели вместо процедурных коробок.</summary>
        bool _fromModel;

        /// <summary>
        /// Корпус из готовой модели.
        ///
        /// Модель приведена импортёром к единице по наибольшей стороне и опорой вниз,
        /// поэтому здесь остаётся задать только длину — ту же, что у процедурного
        /// корпуса, чтобы вагонетка не выросла относительно рельса и жабы.
        /// </summary>
        void BuildFromModel(Mesh mesh)
        {
            var go = new GameObject("CartModel", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_solid, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.transform.localScale = Vector3.one * Space3D.Size(44f * 1.5f);

            // Разворот на 90: у вагона длинная сторона — глубина, а у нашей вагонетки
            // длина идёт вдоль рельса. Без него вагон ложится поперёк пути доской.
            go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            // Материала в файле один и он текстурный, а текстуры импортёр не берёт.
            // Цвета назначаются по частям: корпус деревянный, колёса тёмные. Части
            // идут в порядке мешей файла — корпус, передние колёса, задние.
            var wood = ProcMesh.Glossy(ProcSprite.Hex("9C6231"), "mat_cartModelBody");
            var metal = ProcMesh.Metal(ProcSprite.Hex("6A7178"), "mat_wheel");

            var materials = new Material[mesh.subMeshCount];
            for (int i = 0; i < materials.Length; i++) materials[i] = i == 0 ? wood : metal;

            go.GetComponent<MeshRenderer>().sharedMaterials = materials;
        }

        public void Build(Transform parent, Tweener tweener, Camera camera)
        {
            _tweener = tweener;
            _camera = camera;

            _root = new GameObject("Cart3D").transform;
            _root.SetParent(parent, false);

            _solid = new GameObject("Solid").transform;
            _solid.SetParent(_root, false);

            BuildMarker();

            // Готовая модель, если она лежит в Resources. Как и у жабы, это подмена,
            // а не замена: файла нет — собирается прежняя вагонетка из коробок.
            var model = Resources.Load<Mesh>("CartModel");
            if (model != null) BuildFromModel(model);

            _fromModel = model != null;

            // Корпус 44x27 из спеки, вглубь контура — та же 27. В объёме вагонетка
            // стоит дальше от камеры, чем доска, и в исходном размере читалась мелкой,
            // поэтому вся сборка увеличена в полтора раза.
            const float Bulk = 1.5f;
            float bodyW = Space3D.Size(44f * Bulk);
            float bodyH = Space3D.Size(20f * Bulk);
            float bodyD = Space3D.Size(27f * Bulk);

            // Корпус модели заменяет процедурный, но не всё остальное: цветной полосы
            // и таблички с числом в модели нет, а они несут смысл — цвет вагонетки и
            // её ёмкость. Поэтому подменяются только корпус и колёса.
            var body = NewPiece("Body", _solid,
                ProcMesh.RoundedBox(bodyW, bodyH, bodyD, Space3D.Size(6f), "cartBody3D"),
                ProcMesh.Glossy(ProcSprite.Hex("9C6231"), "mat_cartBody"));
            body.transform.localPosition = new Vector3(0f, Space3D.Size(9f * Bulk), 0f);
            _body = body.transform;

            if (_fromModel) body.GetComponent<MeshRenderer>().enabled = false;

            // Колёса вынесены за борта корпуса.
            //
            // Стояли внутри его габарита, на 13*Bulk от оси при полуширине корпуса
            // 33, и вылезали сквозь него тёмными плитами по бокам от жабы — на
            // скриншотах их принимали за посторонние квадраты. Проверено сборкой с
            // колёсами, выкрашенными в пурпурный: плиты покраснели вместе с ними.
            // Снаружи они и не пересекают корпус, и наконец читаются как колёса.
            // Колёса стоят на нитях.
            //
            // Локальный X — вдоль пути, Z — поперёк: корпус 44 в длину и 27 вглубь
            // контура. Нити рельсов разведены поперёк на +-6, значит по Z колесо
            // обязано попасть ровно туда, а разнесены колёса по X — это колёсная база.
            //
            // Раньше здесь было ровно наоборот: база задавалась по X через 35, а Z
            // стоял 8 и 29 — обе пары оказывались по одну сторону от осевой и мимо
            // нитей. Вагонетка ехала рядом с путём, а не по нему.
            //
            // Колея 20, а не 6. При узкой колее колёса прятались под корпусом
            // шириной 40 поперёк — и при взгляде сверху выглядывали из-под его
            // передней стенки тёмными квадратами, потому что луч до них проходил
            // над её верхней кромкой. При колее 20 вагонетка стоит верхом на путях,
            // и колёса видно там, где им и место, — на нитях.
            const float Base = 22f;    // половина колёсной базы, вдоль пути
            const float Gauge = 20f;   // половина колеи, поперёк — совпадает с нитями

            if (!_fromModel)
                foreach (float along in new[] { -Base, Base })
                foreach (float across in new[] { -Gauge, Gauge })
                    NewWheel($"Wheel{along}_{across}",
                        new Vector3(Space3D.Size(along), Space3D.Size(7f), Space3D.Size(across)));

            // Цветная полоса по низу корпуса — по ней цвет читается издалека.
            var stripe = NewPiece("Stripe", _solid,
                ProcMesh.RoundedBox(bodyW * 0.85f, Space3D.Size(3f), bodyD * 1.02f,
                                    Space3D.Size(2f), "cartStripe3D"),
                ProcMesh.Glossy(Color.white, "mat_cartStripe"));
            stripe.transform.localPosition = new Vector3(0f, Space3D.Size(2f * Bulk), 0f);
            _stripe = stripe.transform;
            _stripeRenderer = stripe.GetComponent<MeshRenderer>();

            BuildIce(bodyW, bodyH, bodyD);
            BuildLinkMark(bodyW, bodyD);
            BuildPlate();
            CollectFadeTargets();
        }

        /// <summary>
        /// Ледяная глыба поверх корпуса. Полупрозрачной не делаю намеренно:
        /// прозрачность в Built-in RP тянет за собой отдельную очередь отрисовки и
        /// сортировку, а нужен всего лишь понятный знак «эта не работает».
        /// Светлый голубой куб чуть больше корпуса решает ту же задачу.
        /// </summary>
        void BuildIce(float bodyW, float bodyH, float bodyD)
        {
            var ice = NewPiece("Ice", _solid,
                ProcMesh.RoundedBox(bodyW * 1.12f, bodyH * 1.9f, bodyD * 1.12f,
                                    Space3D.Size(5f), "cartIce3D"),
                ProcMesh.Glossy(ProcSprite.Hex("BEE6F5"), "mat_cartIce", 0.55f));

            ice.transform.localPosition = new Vector3(0f, Space3D.Size(4f), 0f);
            _ice = ice.transform;
            _ice.gameObject.SetActive(false);
        }

        /// <summary>
        /// Метка связки: светлое кольцо под вагонеткой. Обе вагонетки пары получают
        /// одинаковое, поэтому связь видно сверху одним взглядом, без стрелок и линий.
        /// </summary>
        void BuildLinkMark(float bodyW, float bodyD)
        {
            var link = NewPiece("LinkMark", _solid,
                ProcMesh.RoundedBox(bodyW * 1.5f, Space3D.Size(1.5f), bodyD * 1.5f,
                                    bodyW * 0.7f, "cartLink3D"),
                ProcMesh.Emissive(ProcSprite.Hex("FFF1B8"), "mat_cartLink"));

            link.transform.localPosition = new Vector3(0f, Space3D.Size(0.5f), 0f);
            _link = link.transform;
            _link.gameObject.SetActive(false);
        }

        void BuildPlate()
        {
            var plateGo = new GameObject("Plate", typeof(Canvas), typeof(CanvasScaler));
            plateGo.transform.SetParent(_solid, false);
            _plate = plateGo.transform;
            // Табличка над головой, а не на её высоте. На 30 она стояла ровно там же,
            // где голова жабы, и цифру закрывала сама жаба: из-за головы торчали
            // только белые уголки таблички, и счётчик на контуре нельзя было прочесть.
            _plate.localPosition = new Vector3(0f, Space3D.Size(98f), 0f);

            var canvas = plateGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)plateGo.transform;
            rt.sizeDelta = new Vector2(26f, 18f);
            rt.localScale = Vector3.one * Space3D.Scale * 1.5f;

            // Зажим-язычок над счётчиком: на референсе счётчик — мешочек с тёмной
            // защёлкой сверху, и именно она отличает его от простой таблички.
            // Кладётся первым, чтобы уйти за фон и торчать только верхушкой.
            var clip = new GameObject("Clip", typeof(RectTransform), typeof(Image));
            clip.transform.SetParent(plateGo.transform, false);
            var clipRt = (RectTransform)clip.transform;
            clipRt.anchorMin = new Vector2(0.5f, 1f);
            clipRt.anchorMax = new Vector2(0.5f, 1f);
            clipRt.pivot = new Vector2(0.5f, 0f);
            clipRt.sizeDelta = new Vector2(11f, 7f);
            clipRt.anchoredPosition = new Vector2(0f, -3f);
            var clipImage = clip.GetComponent<Image>();
            clipImage.sprite = ProcSprite.Make(
                ProcSprite.Rounded.Flat(11, 7, 2f, Color.white, "plateClip3D"));
            clipImage.color = ProcSprite.Hex("4A4F57");

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(plateGo.transform, false);
            var backRt = (RectTransform)background.transform;
            backRt.anchorMin = Vector2.zero;
            backRt.anchorMax = Vector2.one;
            backRt.offsetMin = Vector2.zero;
            backRt.offsetMax = Vector2.zero;
            // Белый спрайт красится в цвет вагонетки через Image.color — как счётчики
            // в очереди, чтобы вся игра считала одинаково.
            _plateImage = background.GetComponent<Image>();
            _plateImage.sprite = ProcSprite.Make(
                ProcSprite.Rounded.Flat(26, 18, 6f, Color.white, "plate3D"));

            _countText = UiText.Create("Count", (RectTransform)plateGo.transform, 16, Color.white);

            var outline = _countText.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.45f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        void NewWheel(string name, Vector3 localPosition)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Destroy(go.GetComponent<Collider>());

            go.transform.SetParent(_solid, false);
            go.transform.localPosition = localPosition;
            // Цилиндр Unity стоит вдоль Y. Ось колеса идёт поперёк пути, то есть
            // вдоль локального Z, — поворот вокруг X, а не вокруг Z. При повороте
            // вокруг Z ось смотрела вдоль движения, и колесо каталось боком.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            // Диаметр 14, ширина 6: снаружи корпуса колесо не обязано быть крупным,
            // а прежние 12*Bulk = 18 в диаметре смотрелись бочками.
            go.transform.localScale = new Vector3(Space3D.Size(14f), Space3D.Size(3f), Space3D.Size(14f));

            go.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Metal(ProcSprite.Hex("6A7178"), "mat_wheel");
        }

        void CollectFadeTargets()
        {
            _fadeCount = 0;
            foreach (var renderer in _solid.GetComponentsInChildren<Renderer>())
            {
                if (_fadeCount >= _fadeTargets.Length) break;
                _fadeTargets[_fadeCount++] = renderer;
            }
        }

        /// <summary>Сколько точек в венце свободного места.</summary>
        const int MarkerDots = 10;

        /// <summary>
        /// Поперечник точки. 16, а не 11: на первой сборке восемь точек по 11
        /// стояли с просветом в свой же поперечник, и сверху, где эллипс к тому же
        /// сплющен перспективой, венец читался рассыпанными крошками, а не меткой.
        /// Десять точек по 16 смыкаются в кольцо.
        /// </summary>
        const float DotSize = 16f;

        /// <summary>
        /// Метка свободного места — венец из светящихся точек по габариту вагонетки.
        ///
        /// Первый заход был плоской пунктирной рамкой в цвет полотна, лежавшей
        /// на путях. Он провалился ровно потому, что был скромным: тёмное на
        /// тёмном, вровень с рельсом, длинные стороны рамки легли на нитки и
        /// слились с ними, а на экране остались четыре точки непонятно чего.
        ///
        /// Отсюда три решения, и все три об одном — метку видно только тогда,
        /// когда она не пытается быть частью путей:
        ///
        /// 1. Светится. Не тёмное по дереву, а горячий жёлтый со свечением.
        /// 2. Поднята над рельсом на 12 — парит, а не лежит.
        /// 3. Живёт. Венец медленно поворачивается, по точкам бежит волна.
        ///    Неподвижное пятно глаз отбрасывает как узор стола, движущееся —
        ///    нет. Это и делает метку заметной, а не размер.
        /// </summary>
        void BuildMarker()
        {
            _marker = new GameObject("EmptyPlace").transform;
            _marker.SetParent(_root, false);

            const float Bulk = 1.5f;
            float radiusAlong = Space3D.Size(44f * Bulk * 0.5f);
            float radiusAcross = Space3D.Size(27f * Bulk * 0.5f);
            float lift = Space3D.Size(12f);

            var material = ProcMesh.Emissive(ProcSprite.Hex("FFD34F"), "mat_placeDot");

            _markerDots = new Transform[MarkerDots];

            for (int i = 0; i < MarkerDots; i++)
            {
                float angle = i * Mathf.PI * 2f / MarkerDots;

                var dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = $"Dot{i}";
                Destroy(dot.GetComponent<Collider>());

                dot.transform.SetParent(_marker, false);
                dot.transform.localPosition = new Vector3(
                    Mathf.Cos(angle) * radiusAlong, lift, Mathf.Sin(angle) * radiusAcross);
                dot.transform.localScale = Vector3.one * Space3D.Size(DotSize);

                var renderer = dot.GetComponent<MeshRenderer>();
                renderer.sharedMaterial = material;

                // Тени точки не отбрасывают: восемь теней на шпалах превратили бы
                // ясную метку в грязное пятно.
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

                _markerDots[i] = dot.transform;
            }

            _marker.gameObject.SetActive(false);
        }

        /// <summary>
        /// Поворот венца и волна по точкам.
        ///
        /// Время неигровое: метка обязана жить и в паузе. Замершая метка читалась
        /// бы поломкой ровно там, где игрок и остановился подумать, сколько ещё
        /// вагонеток он может поставить.
        /// </summary>
        void AnimateMarker()
        {
            float t = Time.unscaledTime;

            // 18°/с, а не 45: под камерой сверху эллипс сплющен, и на быстром
            // вращении точки то сбегались, то разбегались — кольцо рябило вместо
            // того, чтобы просто жить.
            _marker.localRotation = Quaternion.Euler(0f, t * 18f, 0f);

            float size = Space3D.Size(DotSize);

            for (int i = 0; i < _markerDots.Length; i++)
            {
                // Сдвиг фазы по номеру точки — это и есть волна: точки дышат не
                // разом, а по кругу, и венец читается бегущим, а не мигающим.
                float pulse = 1f + Mathf.Sin(t * 3.2f - i * 0.55f) * 0.28f;
                _markerDots[i].localScale = Vector3.one * size * pulse;
            }
        }

        public void Place(Vector2 railPos, float railAngle, float scale, float alpha)
        {
            _root.position = Space3D.ToWorld(railPos);
            _root.rotation = Space3D.RotationFromSpecAngle(railAngle);
            _root.localScale = Vector3.one * Mathf.Max(0.0001f, scale);

            // Табличка всегда лицом к камере: иначе на дальней стороне контура
            // число читалось бы зеркально или с торца.
            if (_camera != null)
                _plate.rotation = Quaternion.LookRotation(_plate.position - _camera.transform.position);

            _solid.gameObject.SetActive(alpha > 0.02f);
            _marker.gameObject.SetActive(false);
        }

        /// <summary>
        /// Место без вагонетки: едет по контуру и показывает метку.
        ///
        /// Отдельный метод, а не Place с нулевой прозрачностью. Place и PlaceEmpty
        /// задают взаимно исключающие состояния места и каждый определяет его
        /// целиком; через альфу это читалось бы «вагонетка есть, но невидима» — а
        /// её здесь нет. Масштаб сбрасывается в единицу: место не приезжает и не
        /// уезжает, оно просто свободно.
        /// </summary>
        public void PlaceEmpty(Vector2 railPos, float railAngle)
        {
            _root.position = Space3D.ToWorld(railPos);
            _root.rotation = Space3D.RotationFromSpecAngle(railAngle);
            _root.localScale = Vector3.one;

            _solid.gameObject.SetActive(false);
            _marker.gameObject.SetActive(true);

            AnimateMarker();
        }

        public void SetColor(ColorPalette palette, int colorId)
        {
            var entry = palette.Get(colorId);
            _colorId = colorId;
            _stripeRenderer.sharedMaterial = ProcMesh.Glossy(entry.baseColor, $"mat_stripe{colorId}");
            _plateColor = entry.baseColor;
            _countText.color = Color.white;

            ApplyPlate();
        }

        /// <summary>
        /// Замороженная вагонетка: ледяная глыба поверх корпуса и счётчик льда
        /// на табличке вместо ёмкости.
        ///
        /// Показывается именно счётчик льда, а не ёмкость: пока вагонетка во льду,
        /// ёмкость игроку не нужна и не интересна, а вот сколько осталось сколоть —
        /// единственное, что определяет его следующий ход.
        /// </summary>
        public void SetFrozen(int count)
        {
            _frozen = Mathf.Max(0, count);
            _ice.gameObject.SetActive(_frozen > 0);
            ApplyPlate();
        }

        public void SetLinked(bool linked)
        {
            _link.gameObject.SetActive(linked);
        }

        void ApplyPlate()
        {
            if (_frozen > 0)
            {
                _plateImage.color = IceColor;
                _countText.text = _frozen.ToString();
                _countText.color = ProcSprite.Hex("13415C");
                return;
            }

            _plateImage.color = _plateColor;
            _countText.color = Color.white;
            _countText.text = _count.ToString();
        }

        static readonly Color IceColor = new Color(0.71f, 0.90f, 0.98f, 1f);

        public void SetCount(int count)
        {
            _count = count;
            ApplyPlate();
        }

        public void PopNumber(float duration)
        {
            _tweener.Run(duration, Tweener.Linear, t =>
            {
                float scale = t < 0.35f
                    ? Mathf.Lerp(1f, 1.42f, t / 0.35f)
                    : Mathf.Lerp(1.42f, 1f, (t - 0.35f) / 0.65f);
                _plate.localScale = Vector3.one * Space3D.Scale * 1.5f * scale;
            }, () => _plate.localScale = Vector3.one * Space3D.Scale * 1.5f);
        }

        public void SetVisible(bool visible) => _solid.gameObject.SetActive(visible);

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
