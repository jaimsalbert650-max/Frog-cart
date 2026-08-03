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
        const int Visible = 5;
        const float SlotY = 664f;    // spec-координата рельса очереди
        const float Step = 78f;
        const float FirstX = 45f;

        sealed class Mini
        {
            public Transform Root;
            public MeshRenderer Stripe;
            public MeshRenderer Head;
            public Text Count;
        }

        readonly Mini[] _minis = new Mini[Visible];
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

        /// <summary>Короткий прямой рельс под очередью — чтобы вагонетки не висели в воздухе.</summary>
        void BuildDockRail()
        {
            var plank = NewPiece("DockPlank", _root,
                ProcMesh.RoundedBox(Space3D.Size(400f), Space3D.Size(4f), Space3D.Size(34f),
                                    Space3D.Size(3f), "dockPlank3D"),
                ProcMesh.Glossy(ProcSprite.Hex("7A4D28"), "mat_dockPlank", 0.1f));
            plank.transform.position = Space3D.ToWorld(195f, SlotY + 6f, Space3D.Size(1f));

            foreach (float offset in new[] { -8f, 8f })
            {
                var rail = NewPiece($"DockRail{offset}", _root,
                    ProcMesh.RoundedBox(Space3D.Size(380f), Space3D.Size(3f), Space3D.Size(3f),
                                        Space3D.Size(1f), "dockRail3D"),
                    ProcMesh.Metal(ProcSprite.Hex("CFD6DA"), "mat_rail"));
                rail.transform.position = Space3D.ToWorld(195f, SlotY + 6f + offset, Space3D.Size(3f));
            }
        }

        Mini BuildMini(int index)
        {
            var mini = new Mini();

            var root = new GameObject($"QueueCart_{index}").transform;
            root.SetParent(_root, false);
            root.position = Space3D.ToWorld(FirstX + index * Step, SlotY);
            mini.Root = root;

            float bulk = 1.2f;

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

            var background = new GameObject("Background", typeof(RectTransform), typeof(Image));
            background.transform.SetParent(plateGo.transform, false);
            var backRt = (RectTransform)background.transform;
            backRt.anchorMin = Vector2.zero;
            backRt.anchorMax = Vector2.one;
            backRt.offsetMin = Vector2.zero;
            backRt.offsetMax = Vector2.zero;
            background.GetComponent<Image>().sprite = ProcSprite.Make(
                ProcSprite.Rounded.Flat(40, 28, 7f, ProcSprite.Hex("FFFAEE"), "queuePlate3D"));

            // Текст с полями внутри таблички: без них у 16-го кегля подрезало
            // нижние выносные элементы краем фона.
            var text = UiText.Create("Count", rt, 16, Color.black);
            var textRt = (RectTransform)text.transform;
            textRt.offsetMin = new Vector2(3f, 4f);
            textRt.offsetMax = new Vector2(-3f, -2f);
            return text;
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

        public void Rebuild(List<LevelData.CartDef> queue, int startIndex)
        {
            for (int i = 0; i < Visible; i++)
            {
                int source = startIndex + i;
                bool has = source < queue.Count;

                _minis[i].Root.gameObject.SetActive(has);
                _minis[i].Root.position = Space3D.ToWorld(FirstX + i * Step, SlotY);

                if (!has) continue;

                var def = queue[source];
                var entry = _palette.Get(def.colorId);

                _minis[i].Stripe.sharedMaterial =
                    ProcMesh.Glossy(entry.baseColor, $"mat_stripe{def.colorId}");
                _minis[i].Head.sharedMaterial =
                    ProcMesh.Glossy(entry.baseColor, $"mat_frogHead{def.colorId}");
                _minis[i].Count.text = def.capacity.ToString();
                _minis[i].Count.color = entry.dark;
            }
        }

        /// <summary>Сдвиг влево за 0.30 c с ease back, затем пересборка — как в спеке.</summary>
        public void Shift(List<LevelData.CartDef> queue, int startIndex, float duration)
        {
            var from = new Vector3[Visible];
            for (int i = 0; i < Visible; i++) from[i] = _minis[i].Root.position;

            _tween.Run(duration, Tweener.QueueShift,
                t =>
                {
                    for (int i = 0; i < Visible; i++)
                    {
                        Vector3 target = Space3D.ToWorld(FirstX + i * Step - Step, SlotY);
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
