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
        Transform _root;
        Transform _body;
        MeshRenderer _headRenderer;
        Transform _mouth;
        Transform _pupilL, _pupilR;
        Camera _camera;

        float _squash;
        float _clap;
        float _mouthOpen;

        /// <summary>Диаметр глаза. Вынесен в константу: он нужен и при сборке, и при смене цвета.</summary>
        static float EyeSize => Space3D.Size(10f);

        public Vector2 MouthSpecPos { get; private set; }

        public void Build(Transform parent, Camera camera)
        {
            _camera = camera;

            _root = new GameObject("Frog3D").transform;
            _root.SetParent(parent, false);

            _body = new GameObject("Body").transform;
            _body.SetParent(_root, false);

            float head = Space3D.Size(30f);

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

            var mouth = NewSphere("Mouth", _body, Space3D.Size(13f));
            mouth.transform.localPosition = new Vector3(0f, head * 0.32f, -head * 0.42f);
            mouth.transform.localScale = new Vector3(Space3D.Size(15f), Space3D.Size(6f), Space3D.Size(6f));
            mouth.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Glossy(ProcSprite.Hex("77293B"), "mat_frogMouth", 0.3f);
            _mouth = mouth.transform;
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
            _headRenderer.sharedMaterial =
                ProcMesh.Glossy(palette.Get(colorId).baseColor, $"mat_frogHead{colorId}");

            float head = Space3D.Size(30f);
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

            Vector2 bodyC = new Vector2(
                railPos.x + (18.5f - lift) * Mathf.Sin(ar),
                railPos.y - (18.5f - lift) * Mathf.Cos(ar));

            // Посадка 28, а не 20: борт вагонетки поднимается до 34.8, и на дальней
            // стороне контура он срезал жабе всё ниже глаз — включая рот, из которого
            // вылетает язык. С 28 над бортом остаётся голова целиком.
            _root.position = Space3D.ToWorld(bodyC, Space3D.Size(28f));

            // Лицом к камере, но строго вертикально: наклонять жабу нельзя.
            if (_camera != null)
            {
                Vector3 toCamera = _camera.transform.position - _root.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.0001f)
                    _root.rotation = Quaternion.LookRotation(-toCamera, Vector3.up);
            }

            float sx = (1f + _squash * 0.30f + _clap) * scale;
            float sy = (1f - _squash * 0.26f + _clap) * scale;
            _body.localScale = new Vector3(sx, sy, sx);
            _body.localRotation = Quaternion.Euler(0f, 0f, _clap * 55f);

            _mouth.localScale = new Vector3(Space3D.Size(15f),
                                            Space3D.Size(Mathf.Lerp(6f, 11f, _mouthOpen)),
                                            Space3D.Size(6f));

            MouthSpecPos = new Vector2(bodyC.x, bodyC.y - 11f);
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
