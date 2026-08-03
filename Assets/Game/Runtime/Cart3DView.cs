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
        Transform _body;
        Transform _plate;
        Transform _stripe;
        MeshRenderer _stripeRenderer;
        Image _plateImage;
        Text _countText;
        Camera _camera;
        Tweener _tweener;

        readonly Renderer[] _fadeTargets = new Renderer[8];
        int _fadeCount;

        public void Build(Transform parent, Tweener tweener, Camera camera)
        {
            _tweener = tweener;
            _camera = camera;

            _root = new GameObject("Cart3D").transform;
            _root.SetParent(parent, false);

            // Корпус 44x27 из спеки, вглубь контура — та же 27. В объёме вагонетка
            // стоит дальше от камеры, чем доска, и в исходном размере читалась мелкой,
            // поэтому вся сборка увеличена в полтора раза.
            const float Bulk = 1.5f;
            float bodyW = Space3D.Size(44f * Bulk);
            float bodyH = Space3D.Size(20f * Bulk);
            float bodyD = Space3D.Size(27f * Bulk);

            var body = NewPiece("Body", _root,
                ProcMesh.RoundedBox(bodyW, bodyH, bodyD, Space3D.Size(6f), "cartBody3D"),
                ProcMesh.Glossy(ProcSprite.Hex("9C6231"), "mat_cartBody"));
            body.transform.localPosition = new Vector3(0f, Space3D.Size(9f * Bulk), Space3D.Size(18.5f));
            _body = body.transform;

            // Колёса вынесены за борта корпуса.
            //
            // Стояли внутри его габарита, на 13*Bulk от оси при полуширине корпуса
            // 33, и вылезали сквозь него тёмными плитами по бокам от жабы — на
            // скриншотах их принимали за посторонние квадраты. Проверено сборкой с
            // колёсами, выкрашенными в пурпурный: плиты покраснели вместе с ними.
            // Снаружи они и не пересекают корпус, и наконец читаются как колёса.
            const float WheelX = 35f;
            NewWheel("WheelFL", new Vector3(-Space3D.Size(WheelX), Space3D.Size(7f), Space3D.Size(8f)));
            NewWheel("WheelFR", new Vector3( Space3D.Size(WheelX), Space3D.Size(7f), Space3D.Size(8f)));
            NewWheel("WheelBL", new Vector3(-Space3D.Size(WheelX), Space3D.Size(7f), Space3D.Size(29f)));
            NewWheel("WheelBR", new Vector3( Space3D.Size(WheelX), Space3D.Size(7f), Space3D.Size(29f)));

            // Цветная полоса по низу корпуса — по ней цвет читается издалека.
            var stripe = NewPiece("Stripe", _root,
                ProcMesh.RoundedBox(bodyW * 0.85f, Space3D.Size(3f), bodyD * 1.02f,
                                    Space3D.Size(2f), "cartStripe3D"),
                ProcMesh.Glossy(Color.white, "mat_cartStripe"));
            stripe.transform.localPosition = new Vector3(0f, Space3D.Size(2f * Bulk), Space3D.Size(18.5f));
            _stripe = stripe.transform;
            _stripeRenderer = stripe.GetComponent<MeshRenderer>();

            BuildPlate();
            CollectFadeTargets();
        }

        void BuildPlate()
        {
            var plateGo = new GameObject("Plate", typeof(Canvas), typeof(CanvasScaler));
            plateGo.transform.SetParent(_root, false);
            _plate = plateGo.transform;
            // Табличка над головой, а не на её высоте. На 30 она стояла ровно там же,
            // где голова жабы, и цифру закрывала сама жаба: из-за головы торчали
            // только белые уголки таблички, и счётчик на контуре нельзя было прочесть.
            _plate.localPosition = new Vector3(0f, Space3D.Size(76f), Space3D.Size(18.5f));

            var canvas = plateGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            var rt = (RectTransform)plateGo.transform;
            rt.sizeDelta = new Vector2(26f, 18f);
            rt.localScale = Vector3.one * Space3D.Scale * 1.5f;

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

            go.transform.SetParent(_root, false);
            go.transform.localPosition = localPosition;
            // Цилиндр Unity стоит вдоль Y, колесо должно лежать вдоль X.
            go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            // Диаметр 14, ширина 6: снаружи корпуса колесо не обязано быть крупным,
            // а прежние 12*Bulk = 18 в диаметре смотрелись бочками.
            go.transform.localScale = new Vector3(Space3D.Size(14f), Space3D.Size(3f), Space3D.Size(14f));

            go.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Metal(ProcSprite.Hex("6A7178"), "mat_wheel");
        }

        void CollectFadeTargets()
        {
            _fadeCount = 0;
            foreach (var renderer in _root.GetComponentsInChildren<Renderer>())
            {
                if (_fadeCount >= _fadeTargets.Length) break;
                _fadeTargets[_fadeCount++] = renderer;
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

            _root.gameObject.SetActive(alpha > 0.02f);
        }

        public void SetColor(ColorPalette palette, int colorId)
        {
            var entry = palette.Get(colorId);
            _stripeRenderer.sharedMaterial = ProcMesh.Glossy(entry.baseColor, $"mat_stripe{colorId}");
            _plateImage.color = entry.baseColor;
            _countText.color = Color.white;
        }

        public void SetCount(int count) => _countText.text = count.ToString();

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

        public void SetVisible(bool visible) => _root.gameObject.SetActive(visible);

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
