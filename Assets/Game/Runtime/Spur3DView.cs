using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Выдвижная ветка от контура к очереди с тупиковым упором на конце.
    ///
    /// Нужна не для украшения. Вагонетка выходит из очереди в одну точку контура и,
    /// если вход занят, ждёт своей секунды. Без ветки это ожидание происходит с
    /// невидимой вагонеткой — она уже ушла из очереди, но на рельсе её ещё нет, и со
    /// стороны ход просто пропадает. На ветке ждущую видно, и ожидание читается тем,
    /// чем оно и является: вагонетка стоит у упора и пропускает встречную.
    ///
    /// Ветка глухая, поэтому у неё упор — как на настоящих тупиках.
    /// </summary>
    public sealed class Spur3DView : MonoBehaviour
    {
        /// <summary>Длина ветки: от нижней нитки контура (628) до переднего ряда очереди.</summary>
        public const float Length = 76f;

        /// <summary>Сколько занимает выдвижение и уборка.</summary>
        public const float MoveTime = 0.16f;

        Transform _root;
        Tweener _tween;
        float _shown;

        /// <summary>Начало ветки — точка въезда на контур.</summary>
        Vector2 _entry;

        public void Build(Transform parent, Tweener tween, Vector2 entrySpec)
        {
            _tween = tween;
            _entry = entrySpec;

            _root = new GameObject("Spur3D").transform;
            _root.SetParent(parent, false);

            var bedMaterial = ProcMesh.Glossy(ProcSprite.Hex("B98A57"), "mat_railBed", 0.05f);
            var sleeperMaterial = ProcMesh.Glossy(ProcSprite.Hex("9A6A38"), "mat_sleeper", 0.1f);
            var railMaterial = ProcMesh.Metal(ProcSprite.Hex("CFD6DA"), "mat_rail");

            // Полотно во всю длину ветки. Шпалы поперёк, нитки вдоль — те же размеры,
            // что у контура, иначе ветка читалась бы деталью из другой игры.
            var bed = Piece("SpurBed", ProcMesh.RoundedBox(
                Space3D.Size(48f), Space3D.Size(2f), Space3D.Size(Length),
                Space3D.Size(2f), "spurBed3D"), bedMaterial);
            bed.localPosition = Along(Length * 0.5f, Space3D.Size(0.5f));

            for (float s = 6f; s < Length; s += 9f)
            {
                var sleeper = Piece("SpurSleeper", ProcMesh.RoundedBox(
                    Space3D.Size(42f), Space3D.Size(2.5f), Space3D.Size(4f),
                    Space3D.Size(1f), "spurSleeper3D"), sleeperMaterial);
                sleeper.localPosition = Along(s, Space3D.Size(1f));
            }

            foreach (float across in new[] { -14f, 14f })
            {
                var rail = Piece("SpurRail", ProcMesh.RoundedBox(
                    Space3D.Size(3.5f), Space3D.Size(3f), Space3D.Size(Length),
                    Space3D.Size(1f), "spurRail3D"), railMaterial);
                rail.localPosition = Along(Length * 0.5f, Space3D.Size(3f))
                                   + new Vector3(across * Space3D.Scale, 0f, 0f);
            }

            BuildStop(sleeperMaterial, railMaterial);

            _root.localScale = new Vector3(1f, 1f, 0f);
            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Упор на дальнем конце: поперечный брус и два столбика. Он и делает ветку
        /// тупиком — по ней некуда ехать дальше, только назад на контур.
        /// </summary>
        void BuildStop(Material beam, Material posts)
        {
            var cross = Piece("StopBeam", ProcMesh.RoundedBox(
                Space3D.Size(44f), Space3D.Size(10f), Space3D.Size(6f),
                Space3D.Size(2f), "spurStop3D"), beam);
            cross.localPosition = Along(Length - 3f, Space3D.Size(5f));

            foreach (float across in new[] { -14f, 14f })
            {
                var post = Piece("StopPost", ProcMesh.RoundedBox(
                    Space3D.Size(5f), Space3D.Size(14f), Space3D.Size(5f),
                    Space3D.Size(1.5f), "spurPost3D"), posts);
                post.localPosition = Along(Length - 3f, Space3D.Size(7f))
                                   + new Vector3(across * Space3D.Scale, 0f, 0f);
            }
        }

        /// <summary>
        /// Точка на ветке в местных координатах. Ветка уходит от контура вниз по
        /// экрану, то есть в spec-координатах вдоль +Y, а в мире это -Z.
        /// </summary>
        static Vector3 Along(float distance, float height)
            => new Vector3(0f, height, -distance * Space3D.Scale);

        Transform Piece(string name, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(_root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go.transform;
        }

        /// <summary>Точка на ветке в spec-координатах: 0 — вход на контур, 1 — упор.</summary>
        public Vector2 PointAt(float t) => new Vector2(_entry.x, _entry.y + Length * t);

        /// <summary>Выдвинуть ветку от контура вниз.</summary>
        public void Deploy()
        {
            _root.gameObject.SetActive(true);
            _root.position = Space3D.ToWorld(_entry);
            Slide(1f);
        }

        /// <summary>Убрать ветку. Прячется только когда сложилась полностью.</summary>
        public void Retract() => Slide(0f);

        void Slide(float target)
        {
            float from = _shown;
            _shown = target;

            _tween.Run(MoveTime, null,
                t =>
                {
                    if (_root == null) return;

                    float value = Mathf.Lerp(from, target, t);
                    _root.localScale = new Vector3(1f, 1f, value);
                },
                () =>
                {
                    if (_root != null && target <= 0f) _root.gameObject.SetActive(false);
                });
        }
    }
}
