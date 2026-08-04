using UnityEngine;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Жаба объёмом: голова-сфера, два глаза на ней, зрачки, рот.
    ///
    /// Как и в плоской версии, жаба не вращается вместе с вагонеткой — она стоит
    /// вертикально и разворачивается лицом к камере. Иначе на дальней стороне
    /// контура игрок видел бы затылок и не понимал, какого она цвета.
    /// </summary>
    public sealed class Frog3DView : MonoBehaviour, IFrogView
    {
        struct Antenna
        {
            public MeshRenderer Stalk;
            public MeshRenderer Tip;
        }

        Transform _root;
        Transform _body;
        MeshRenderer _headRenderer;
        MeshRenderer _muzzleRenderer;
        Antenna _antennaL, _antennaR;
        Transform _mouth;
        Transform _pupilL, _pupilR;
        Camera _camera;

        float _squash;
        float _clap;
        float _mouthOpen;

        /// <summary>Диаметр глаза. Вынесен в константу: он нужен и при сборке, и при смене цвета.</summary>
        static float EyeSize => Space3D.Size(10f);

        public Vector2 MouthSpecPos { get; private set; }

        /// <summary>
        /// Сколько секунд занимает доворот к цели и сколько — возврат.
        ///
        /// Времена разные, и это не прихоть. Доворот начинается в тот же миг, что и
        /// выстрел, а язык летит 0.42 с. Пока бросок занимал те же 0.34 с, голова
        /// всё ещё поворачивалась, когда язык был уже вытянут, и почти весь полёт
        /// он шёл вбок — сколько ни правь силу доворота.
        ///
        /// Поэтому бросок укладывается в первую треть полёта. Возврат остаётся
        /// медленным: он и виден чаще, и именно быстрый возврат когда-то читался
        /// дёрганьем — укус идёт раз в 0.67 с.
        /// </summary>
        const float AimTime = 0.12f;
        const float ReturnTime = 0.34f;

        /// <summary>
        /// Пределы доворота к цели.
        ///
        /// Одинаковый доворот на все стороны не годится, и обе крайности уже
        /// проверены. Полный ставит рот точно в цель — язык выходит прямо, — но
        /// когда цель за спиной, камера сверху видит затылок, а корень языка
        /// прячется за головой. Слабый бережёт морду, зато рот смотрит в одну
        /// сторону, а язык летит в другую, и выстрел читается как «боком».
        ///
        /// Поэтому сила зависит от того, куда цель. К игроку — доворачиваемся почти
        /// полностью, там поворот ничего не прячет. За спину — остаёмся вполоборота,
        /// чтобы рот держался на кромке силуэта.
        /// </summary>
        const float MinTurn = 0.55f;
        const float MaxTurn = 0.95f;

        Vector2 _aimSpec;
        bool _aiming;
        float _aimWeight;

        /// <summary>
        /// Целиться ртом в клетку, пока язык в полёте.
        ///
        /// Без этого корень языка формально во рту, а видно его не всегда: жаба
        /// развёрнута лицом к камере, и когда цель у неё за спиной, голова —
        /// сплошной шар — закрывает начало языка. Снаружи читается, будто язык
        /// растёт из-за затылка.
        ///
        /// Разворот именно временный. Постоянный сделал бы жабу осмысленной, но
        /// половину партии игрок смотрел бы ей в затылок, а лицо — это весь
        /// характер персонажа.
        /// </summary>
        public void AimAt(Vector2 specTarget)
        {
            _aimSpec = specTarget;
            _aiming = true;
        }

        /// <summary>Отпустить цель — жаба поедет обратно лицом к камере.</summary>
        public void ReleaseAim() => _aiming = false;

        /// <summary>
        /// Ход доворота. Отдельно от <see cref="PlaceOnRail"/>, потому что тому
        /// неоткуда взять dt: он приходит из общего контракта видов без времени.
        /// Зовётся из языка — у него dt уже правильный, замирающий на паузе.
        /// </summary>
        public void AdvanceAim(float dt)
            => _aimWeight = Mathf.MoveTowards(
                   _aimWeight, _aiming ? 1f : 0f, dt / (_aiming ? AimTime : ReturnTime));

        /// <summary>
        /// Доля доворота для цели в заданном направлении, от <see cref="MinTurn"/> до
        /// <see cref="MaxTurn"/>. Оба вектора берутся от жабы: один к цели, другой к
        /// камере; высота отбрасывается, поворот идёт только вокруг вертикали.
        /// </summary>
        public static float AimStrength(Vector3 toTarget, Vector3 toCamera)
        {
            toTarget.y = 0f;
            toCamera.y = 0f;

            if (toTarget.sqrMagnitude < 1e-6f || toCamera.sqrMagnitude < 1e-6f)
                return MinTurn;

            // Единица — цель ровно со стороны камеры, минус единица — точно за спиной.
            float alignment = Vector3.Dot(toTarget.normalized, toCamera.normalized);
            return Mathf.Lerp(MinTurn, MaxTurn, (alignment + 1f) * 0.5f);
        }

        /// <summary>
        /// Рот в мировых координатах — точка, из которой обязан выходить язык.
        /// Плоской проекции для этого мало: она теряет высоту головы, а именно
        /// на высоте вся разница между «язык вылетает изо рта» и «язык лежит
        /// на доске рядом с вагонеткой».
        /// </summary>
        public Vector3 MouthWorldPos => _mouth != null ? _mouth.position : _root.position;

        /// <summary>
        /// Готовая модель вместо процедурной жабы, если она лежит в Resources.
        ///
        /// Не заменяет процедурную, а подменяет: файла нет — собирается прежняя из
        /// шаров. Так проект остаётся работоспособным без внешних ассетов, а модель
        /// можно попробовать и убрать, ничего не ломая.
        /// </summary>
        bool _fromModel;

        // Какой слот материалов красится цветом уровня. У полной модели тело идёт
        // вторым подмешем, у головы — первым.
        int _bodyMaterialSlot = 1;

        // Разворачивается ли модель лицом к камере вместо разворота по рельсу.
        bool _facesCamera;

        // Слот рожек. Минус один — рожек отдельным подмешем нет.
        int _hornMaterialSlot = -1;

        // Размер головы и её разворот вокруг вертикали вынесены в константы: и то,
        // и другое подбирается по кадру, а лезть за ними в глубину сборки неудобно.
        const float HeadSize = 46f;
        const float HeadTurn = 180f;

        public void Build(Transform parent, Camera camera, int slot = 0)
        {
            _camera = camera;

            _root = new GameObject("Frog3D").transform;
            _root.SetParent(parent, false);

            _body = new GameObject("Body").transform;
            _body.SetParent(_root, false);

            // Голова, сгенерированная по картинке, старше остальных вариантов: если
            // она лежит в Resources, жаба собирается из неё одной. Тела у неё нет —
            // из вагонетки торчит голова, как на референсе, — поэтому это не поза
            // модели, а отдельная сборка.
            var head3D = Resources.Load<Mesh>("FrogHeadModel");
            if (head3D != null) { BuildFromHead(head3D); return; }

            // Раскладка поз: если в Resources лежат FrogPose0..N, каждое место
            // контура получает свой вариант, и все они видны в одном кадре. Это
            // временный режим для выбора позы — обычная сборка берёт FrogModel.
            var model = Resources.Load<Mesh>($"FrogPose{slot}") ?? Resources.Load<Mesh>("FrogModel");
            if (model != null) { BuildFromModel(model); return; }

            float head = Space3D.Size(34f);

            var headGo = NewSphere("Head", _body, head);
            headGo.transform.localPosition = new Vector3(0f, head * 0.5f, 0f);
            headGo.transform.localScale = new Vector3(head, head * 0.86f, head * 0.9f);
            _headRenderer = headGo.GetComponent<MeshRenderer>();

            // Глаз 10 против прежних 15. При 15 диаметр глаза был половиной головы,
            // два белых шара забивали цвет, и жаба с любого расстояния читалась как
            // белое пятно с зрачками. На референсе персонаж прежде всего цветной.
            float eye = EyeSize;
            _pupilL = BuildEye("EyeL", new Vector3(-head * 0.21f, head * 0.78f, -head * 0.26f), eye);
            _pupilR = BuildEye("EyeR", new Vector3( head * 0.21f, head * 0.78f, -head * 0.26f), eye);

            // Светлая мордочка под глазами. Без неё голова была одноцветным шаром,
            // и лицо читалось только по глазам.
            var muzzle = NewSphere("Muzzle", _body, 1f);
            muzzle.transform.localPosition = new Vector3(0f, head * 0.50f, -head * 0.36f);
            muzzle.transform.localScale =
                new Vector3(head * 0.52f, head * 0.30f, head * 0.28f);
            _muzzleRenderer = muzzle.GetComponent<MeshRenderer>();

            var mouth = NewSphere("Mouth", _body, Space3D.Size(13f));
            mouth.transform.localPosition = new Vector3(0f, head * 0.42f, -head * 0.50f);
            mouth.transform.localScale = new Vector3(Space3D.Size(15f), Space3D.Size(6f), Space3D.Size(6f));
            mouth.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Glossy(ProcSprite.Hex("77293B"), "mat_frogMouth", 0.3f);
            _mouth = mouth.transform;

            // Усики. Это та самая деталь, по которой персонажи оригинала читаются
            // живыми существами, а не цветными шарами: силуэт перестаёт быть
            // окружностью и получает характер.
            _antennaL = BuildAntenna("AntennaL", -1f, head);
            _antennaR = BuildAntenna("AntennaR", 1f, head);
        }

        /// <summary>
        /// Сборка из головы, сгенерированной по картинке.
        ///
        /// От готовой модели отличается всем: подмеш один, материалов и глаз в файле
        /// нет — только поверхность. Поэтому перекрашивается нулевой слот, а не
        /// первый, и никаких белков с зрачками отдельно не задаётся: глаза вылеплены
        /// в самой геометрии.
        ///
        /// Размер меньше, чем у полной фигуры: там единица нормализации приходилась
        /// на рост со сложенными лапами, здесь — на одну голову, и то же число дало
        /// бы жабу выше вагонетки.
        /// </summary>
        void BuildFromHead(Mesh mesh)
        {
            _fromModel = true;
            _bodyMaterialSlot = 0;
            _facesCamera = true;

            var go = new GameObject("FrogHead", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_body, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            go.transform.localScale = Vector3.one * Space3D.Size(HeadSize);
            go.transform.localPosition = new Vector3(0f, 0f, -Space3D.Size(4f));
            go.transform.localRotation = Quaternion.Euler(0f, HeadTurn, 0f);

            // Порядок подмешей задаёт импортёр: тело, белки глаз, тёмное — зрачки,
            // рожки и полоса рта. Цветом уровня красится только тело, остальное
            // обязано остаться белым и чёрным при любом цвете.
            _modelRenderer = go.GetComponent<MeshRenderer>();
            _modelRenderer.sharedMaterials = new[]
            {
                ProcMesh.Glossy(Color.green, "mat_frogHeadBody"),
                ProcMesh.Glossy(Color.white, "mat_frogHeadEye", 0.8f),
                ProcMesh.Glossy(ProcSprite.Hex("1D2127"), "mat_frogHeadDark", 0.9f),
                ProcMesh.Glossy(Color.green, "mat_frogHeadHorn"),
            };

            _hornMaterialSlot = 3;

            // Рот в геометрии есть, но точки выхода языка в файле нет. Якорь ставится
            // по пропорциям головы: складка рта приходится примерно на треть высоты
            // и на передний край по глубине.
            var mouth = new GameObject("MouthAnchor").transform;
            mouth.SetParent(_body, false);
            mouth.localPosition = new Vector3(0f, Space3D.Size(HeadSize) * 0.34f,
                                                  -Space3D.Size(HeadSize) * 0.44f);
            _mouth = mouth;
        }

        /// <summary>
        /// Сборка из готовой модели.
        ///
        /// Подмеши идут в порядке материалов исходного файла — Frog_Secondary,
        /// Frog_Main, Eye_Black, Eye_White, — и перекрашивается только Frog_Main:
        /// глаза обязаны остаться белыми с чёрными зрачками при любом цвете уровня.
        /// </summary>
        void BuildFromModel(Mesh mesh)
        {
            _fromModel = true;

            var go = new GameObject("FrogModel", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_body, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;

            // Размер, посадка и наклон.
            //
            // На 34 — размере процедурной головы — модель терялась: у процедурной
            // жабы это диаметр шара, а у этой то же число растягивается на всю
            // фигуру с лапами, и на голову остаётся малая доля. Отсюда 58.
            //
            // Наклон вперёд и сдвиг к переднему борту сажают её так, будто она
            // держится за край вагонетки, а не стоит в ней столбиком. Настоящей
            // позы рук это не даёт — для неё нужна поза скелета, — но силуэт
            // читается как «свесилась через борт».
            // После запекания позы жаба сидит, а не стоит враскоряку: единица
            // нормализации приходится теперь на рост, а не на размах лап, и то же
            // число даёт вдвое более крупную фигуру.
            go.transform.localScale = Vector3.one * Space3D.Size(58f);
            // Разворот на 180 — не украшение, а исправление.
            //
            // В этом проекте лицо жабы смотрит в локальный -Z: так устроена
            // процедурная, и оттуда же берётся точка выхода языка. У модели после
            // зеркалирования по X и разворота «Z вверх» лицо оказалось в +Z, то есть
            // она сидела к камере спиной, а язык вылетал из зада.
            go.transform.localPosition = new Vector3(0f, 0f, -Space3D.Size(5f));
            go.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            _modelRenderer = go.GetComponent<MeshRenderer>();
            _modelRenderer.sharedMaterials = new[]
            {
                ProcMesh.Glossy(ProcSprite.Hex("2C2F36"), "mat_frogModelSecondary"),
                ProcMesh.Glossy(Color.green, "mat_frogModelBody"),
                ProcMesh.Glossy(ProcSprite.Hex("1D2127"), "mat_frogModelPupil", 0.9f),
                ProcMesh.Glossy(Color.white, "mat_frogModelEye", 0.8f),
            };

            // Рот моделью не задан, а языку нужна точка выхода. Якорь висит на теле,
            // а не на модели: он задан в системе жабы, где лицо всегда в -Z, и не
            // зависит от того, как модель развёрнута внутри.
            //
            // Числа от роста: модель нормализована в единицу по высоте, голова
            // приходится примерно на две трети, передний край — на треть глубины.
            var mouth = new GameObject("MouthAnchor").transform;
            mouth.SetParent(_body, false);
            mouth.localPosition = new Vector3(0f, Space3D.Size(58f) * 0.62f,
                                                  -Space3D.Size(58f) * 0.26f);
            _mouth = mouth;
        }

        MeshRenderer _modelRenderer;

        /// <summary>Усик: тонкий стебель от макушки и шарик на конце.</summary>
        Antenna BuildAntenna(string name, float side, float head)
        {
            var root = new GameObject(name).transform;
            root.SetParent(_body, false);
            root.localPosition = new Vector3(side * head * 0.22f, head * 0.80f, -head * 0.04f);
            // Знак поворота отрицательный: при положительном оба усика заваливались
            // внутрь и складывались на лбу крестиком вместо того, чтобы расходиться.
            root.localRotation = Quaternion.Euler(-16f, 0f, -side * 32f);

            var stalk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            stalk.name = "Stalk";
            Destroy(stalk.GetComponent<Collider>());
            stalk.transform.SetParent(root, false);
            // Цилиндр Unity высотой 2, поэтому половина длины идёт в масштаб,
            // а центр поднимается на неё же — стебель растёт вверх от макушки.
            stalk.transform.localScale =
                new Vector3(Space3D.Size(2.4f), Space3D.Size(10f), Space3D.Size(2.4f));
            stalk.transform.localPosition = new Vector3(0f, Space3D.Size(10f), 0f);

            var tip = NewSphere("Tip", root, Space3D.Size(9f));
            tip.transform.localPosition = new Vector3(0f, Space3D.Size(21f), 0f);

            return new Antenna
            {
                Stalk = stalk.GetComponent<MeshRenderer>(),
                Tip = tip.GetComponent<MeshRenderer>(),
            };
        }

        Transform BuildEye(string name, Vector3 localPosition, float size)
        {
            var eye = NewSphere(name, _body, size);
            eye.transform.localPosition = localPosition;
            eye.transform.localScale = Vector3.one * size;
            eye.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Glossy(Color.white, "mat_frogEye", 0.8f);

            var pupil = NewSphere("Pupil", eye.transform, size * 0.45f);
            pupil.transform.localPosition = new Vector3(0f, 0f, -0.32f);
            pupil.transform.localScale = Vector3.one * 0.5f;
            pupil.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Glossy(ProcSprite.Hex("1D2127"), "mat_frogPupil", 0.9f);

            // Блик в зрачке. У персонажей оригинала он есть у всех, и именно он
            // отличает живой глаз от чёрной точки. Материал самосветящийся: жаба
            // часто оказывается в тени доски, и освещённый блик там гаснет.
            var glint = NewSphere("Glint", pupil.transform, 1f);
            glint.transform.localPosition = new Vector3(-0.24f, 0.26f, -0.34f);
            glint.transform.localScale = Vector3.one * 0.42f;
            glint.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Emissive(Color.white, "mat_frogGlint");
            glint.GetComponent<MeshRenderer>().shadowCastingMode =
                UnityEngine.Rendering.ShadowCastingMode.Off;

            return pupil.transform;
        }

        /// <summary>
        /// Цвет и форма. Форма тоже: пять одинаковых шаров, отличающихся только
        /// окраской, на доске путаются между собой, особенно у дальних вагонеток.
        /// Пропорции головы и глаз выводятся из номера цвета, поэтому одна и та же
        /// жаба всегда выглядит одинаково.
        /// </summary>
        public void SetColor(ColorPalette palette, int colorId)
        {
            var entry = palette.Get(colorId);

            if (_fromModel)
            {
                // Перекрашивается только тело. Материалы копируются массивом целиком:
                // sharedMaterials отдаёт копию массива, и правка элемента на месте
                // до рендерера не доходит.
                var materials = _modelRenderer.sharedMaterials;
                materials[_bodyMaterialSlot] =
                    ProcMesh.Glossy(entry.baseColor, $"mat_frogModelBody{colorId}");

                if (_hornMaterialSlot >= 0)
                    materials[_hornMaterialSlot] =
                        ProcMesh.Glossy(entry.dark, $"mat_frogModelHorn{colorId}");

                _modelRenderer.sharedMaterials = materials;
                return;
            }

            _headRenderer.sharedMaterial =
                ProcMesh.Glossy(entry.baseColor, $"mat_frogHead{colorId}");

            // Мордочка светлее тела, усики темнее: три оттенка одного цвета вместо
            // одной заливки, и голова перестаёт быть плоским пятном.
            _muzzleRenderer.sharedMaterial = ProcMesh.Glossy(
                Color.Lerp(entry.baseColor, Color.white, 0.5f), $"mat_frogMuzzle{colorId}");

            var stalkMaterial = ProcMesh.Glossy(entry.dark, $"mat_frogAntenna{colorId}");
            var tipMaterial = ProcMesh.Glossy(entry.baseColor, $"mat_frogHead{colorId}");

            _antennaL.Stalk.sharedMaterial = _antennaR.Stalk.sharedMaterial = stalkMaterial;
            _antennaL.Tip.sharedMaterial = _antennaR.Tip.sharedMaterial = tipMaterial;

            float head = Space3D.Size(34f);
            var shape = HeadShape(colorId);

            _headRenderer.transform.localScale =
                new Vector3(head * shape.x, head * shape.y, head * shape.z);

            float eyeScale = 0.85f + (colorId % 3) * 0.16f;
            _pupilL.parent.localScale = Vector3.one * EyeSize * eyeScale;
            _pupilR.parent.localScale = Vector3.one * EyeSize * eyeScale;
        }

        /// <summary>Пропорции головы по номеру цвета: приземистая, круглая, вытянутая.</summary>
        static Vector3 HeadShape(int colorId)
        {
            switch (colorId % 5)
            {
                case 0:  return new Vector3(1.18f, 0.72f, 0.95f);   // приземистая, широкая
                case 1:  return new Vector3(1.00f, 0.86f, 0.90f);   // обычная
                case 2:  return new Vector3(0.86f, 1.05f, 0.86f);   // вытянутая вверх
                case 3:  return new Vector3(1.10f, 0.80f, 1.05f);   // пухлая
                default: return new Vector3(0.92f, 0.95f, 0.80f);   // узкая
            }
        }

        /// <summary>
        /// Формулы посадки те же, что в плоской версии: жаба «вырастает» из центра
        /// корпуса вагонетки внутрь контура. Разница только в том, что результат
        /// разворачивается в мировые координаты.
        /// </summary>
        public void PlaceOnRail(Vector2 railPos, float railAngleDeg, float lift, float scale)
        {
            float ar = railAngleDeg * Mathf.Deg2Rad;

            // Жаба сидит в вагонетке, а вагонетка теперь стоит на осевой линии пути,
            // а не в 18.5 внутрь контура. Смещение осталось только у анимации
            // въезда-выезда: lift отводит жабу наружу, когда вагонетка уходит.
            Vector2 bodyC = new Vector2(
                railPos.x - lift * Mathf.Sin(ar),
                railPos.y + lift * Mathf.Cos(ar));

            // Посадка 28, а не 20: борт вагонетки поднимается до 34.8, и на дальней
            // стороне контура он срезал жабе всё ниже глаз — включая рот, из которого
            // вылетает язык. С 28 над бортом остаётся голова целиком.
            _root.position = Space3D.ToWorld(bodyC, Space3D.Size(28f));

            // Как жаба развёрнута.
            //
            // Процедурная всегда смотрит в камеру: шар симметричен, поворот на нём не
            // читается, зато на дальней стороне контура видно морду и цвет.
            //
            // Модель так не может. У фигуры с лапами и мордой видно, что она стоит
            // прямо, пока вагонетка под ней уходит в поворот, — и это первое, что
            // бросается в глаза. Поэтому модель едет вместе с вагонеткой, как ей и
            // положено в ней сидеть. Цена известна: на дальней стороне будет виден
            // полуоборот, а не лицо.
            //
            // Голова без тела — исключение, и по той же причине. Смотреть на ней не
            // на что, кроме лица: затылок у неё гладкий одноцветный шар, и на дальней
            // половине контура персонаж просто исчезал. Стоять «прямо, пока вагонетка
            // в повороте» она при этом не может — у неё нет ни плеч, ни лап, по
            // которым это было бы заметно.
            bool haveFacing = false;
            var facing = Quaternion.identity;

            if (_fromModel && !_facesCamera)
            {
                facing = Space3D.RotationFromSpecAngle(railAngleDeg);
                haveFacing = true;
            }
            else if (_camera != null)
            {
                Vector3 toCamera = _camera.transform.position - _root.position;
                toCamera.y = 0f;

                if (toCamera.sqrMagnitude > 0.0001f)
                {
                    // Рот смотрит в локальный -Z, поэтому forward берётся «от камеры».
                    facing = Quaternion.LookRotation(-toCamera, Vector3.up);
                    haveFacing = true;
                }
            }

            if (haveFacing)
            {
                // Доворот к цели поверх основной посадки. Слерп, а не подмена: на
                // середине укуса жаба стоит вполоборота, и это единственное
                // положение, в котором видно и морду, и язык.
                if (_aimWeight > 0.001f)
                {
                    Vector3 fromTarget = _root.position - Space3D.ToWorld(_aimSpec);
                    fromTarget.y = 0f;

                    if (fromTarget.sqrMagnitude > 0.0001f)
                    {
                        var toCamera = _camera != null
                            ? _camera.transform.position - _root.position
                            : Vector3.zero;

                        facing = Quaternion.Slerp(
                            facing,
                            Quaternion.LookRotation(fromTarget, Vector3.up),
                            _aimWeight * AimStrength(-fromTarget, toCamera));
                    }
                }

                _root.rotation = facing;
            }

            // Приседание при глотке. У модели оно втрое слабее: шар, сжимающийся на
            // четверть, читается глотком, а фигура с лапами и мордой — резиновой.
            float squashX = _fromModel ? 0.10f : 0.30f;
            float squashY = _fromModel ? 0.09f : 0.26f;

            float sx = (1f + _squash * squashX + _clap) * scale;
            float sy = (1f - _squash * squashY + _clap) * scale;
            _body.localScale = new Vector3(sx, sy, sx);
            _body.localRotation = Quaternion.Euler(0f, 0f, _clap * 55f);

            // У модели рот — не отдельный объект, а точка привязки: масштабировать
            // её незачем, языку нужна только позиция.
            if (!_fromModel)
                _mouth.localScale = new Vector3(Space3D.Size(15f),
                                                Space3D.Size(Mathf.Lerp(6f, 11f, _mouthOpen)),
                                                Space3D.Size(6f));

            // Точка рта берётся у самого рта, а не считается заново от вагонетки.
            //
            // Раньше здесь стояло `bodyC.y - 11` — формула плоской версии, где spec-Y
            // это экранный Y и «минус 11» означает «на 11 пикселей выше по экрану».
            // В объёме spec-Y уходит в Z доски, поэтому та же строка сдвигала корень
            // языка на 11 единиц вглубь доски, а высоту не задавала вовсе: язык висел
            // на постоянной высоте 0.55, тогда как голова поднята на 2.8. Корень
            // языка и рот оказывались разными точками и по глубине, и по высоте —
            // язык шёл поверх морды и обрывался у корпуса вагонетки.
            Vector3 mouthWorld = _mouth.position;
            MouthSpecPos = new Vector2(mouthWorld.x / Space3D.Scale, -mouthWorld.z / Space3D.Scale);
        }

        public void SetSquash(float value)
        {
            _squash = value;
            _mouthOpen = value;
        }

        public void SetClap(float value) => _clap = value;

        public void SetAlpha(float value) => _root.gameObject.SetActive(value > 0.02f);

        public void SetGaze(float offsetY)
        {
            // У модели зрачки — часть меша, двигать нечего.
            if (_fromModel) return;

            float shift = -offsetY * 0.02f;
            _pupilL.localPosition = new Vector3(_pupilL.localPosition.x, shift, _pupilL.localPosition.z);
            _pupilR.localPosition = new Vector3(_pupilR.localPosition.x, shift, _pupilR.localPosition.z);
        }

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

        static GameObject NewSphere(string name, Transform parent, float size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Destroy(go.GetComponent<Collider>());

            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * size;

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            renderer.receiveShadows = true;

            return go;
        }
    }
}
