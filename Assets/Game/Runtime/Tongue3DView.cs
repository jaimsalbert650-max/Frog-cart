using System;
using UnityEngine;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Язык объёмом: LineRenderer по той же кривой Безье, что и в плоской версии,
    /// только точки разворачиваются в мировые координаты и приподнимаются над доской,
    /// чтобы язык не тонул в блоках.
    ///
    /// Тайминги и форма кривой — из docs/unity-spec/05-feel-anim.md, без изменений.
    /// </summary>
    public sealed class Tongue3DView : MonoBehaviour, ITongueView
    {
        const int Segments = 20;
        const float Height = 0.55f;   // над плоскостью доски, в единицах мира

        public event Action OnStick;
        public event Action OnDone;

        LineRenderer _line;
        Transform _tip;
        Transform _carried;
        MeshRenderer _carriedRenderer;

        Vector2 _target;
        float _side;
        float _t;
        float _outDuration = 0.20f;
        float _backDuration = 0.13f;
        bool _stuck;

        public bool Active { get; private set; }

        /// <summary>
        /// Жаба, которой принадлежит этот язык. Нужна ради одного — истинной точки
        /// рта в мире. Через общий контракт `ITongueView` её не передать: там рот
        /// приходит плоской точкой, а плоская точка не знает высоты головы.
        /// </summary>
        Frog3DView _frog;

        public void SetMouth(Frog3DView frog) => _frog = frog;

        /// <summary>Начало нарисованного языка — для проверок, что он растёт изо рта.</summary>
        public Vector3 RootWorldPos => _line.GetPosition(0);

        public void Build(Transform parent)
        {
            var go = new GameObject("Tongue3D");
            go.transform.SetParent(parent, false);

            _line = go.AddComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.positionCount = Segments + 1;
            _line.numCapVertices = 6;
            _line.numCornerVertices = 4;
            _line.material = ProcMesh.Unlit(ProcSprite.Hex("E04F6D"), "mat_tongue");
            _line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            tip.name = "Tip";
            Destroy(tip.GetComponent<Collider>());
            tip.transform.SetParent(go.transform, false);
            tip.transform.localScale = Vector3.one * Space3D.Size(11f);
            tip.GetComponent<MeshRenderer>().sharedMaterial =
                ProcMesh.Emissive(ProcSprite.Hex("F2657F"), "mat_tongueTip");
            _tip = tip.transform;

            var carried = new GameObject("Carried", typeof(MeshFilter), typeof(MeshRenderer));
            carried.transform.SetParent(go.transform, false);
            carried.GetComponent<MeshFilter>().sharedMesh = ProcMesh.RoundedBox(
                Space3D.Size(20f), Space3D.Size(16f), Space3D.Size(20f),
                Space3D.Size(6f), "carried3D");
            _carriedRenderer = carried.GetComponent<MeshRenderer>();
            _carried = carried.transform;

            Hide();
        }

        public void Fire(Vector2 target, float side, float outDuration, float backDuration)
        {
            _target = target;
            _side = side;
            _outDuration = outDuration;
            _backDuration = backDuration;
            _t = 0f;
            _stuck = false;
            Active = true;

            // Жаба доворачивается ртом к цели на время выстрела: иначе её голова
            // закрывает начало языка, когда цель за спиной.
            if (_frog != null) _frog.AimAt(target);

            _line.enabled = true;
            _tip.gameObject.SetActive(true);
        }

        public void SetCarriedBlock(ColorPalette palette, int colorId)
            => _carriedRenderer.sharedMaterial =
                   ProcMesh.Glossy(palette.Get(colorId).baseColor, $"mat_block{colorId}");

        public void Stop() => Hide();

        public void UpdateFrame(float dt, Vector2 mouth)
        {
            // Доворот жабы считается и после того, как язык убран: ей надо плавно
            // вернуться лицом к камере, а не щёлкнуть обратно в кадре завершения.
            if (_frog != null) _frog.AdvanceAim(dt);

            if (!Active) return;

            _t += dt;
            float p;

            if (_t < _outDuration)
            {
                p = Tweener.EaseOutBack(_t / _outDuration);
            }
            else
            {
                if (!_stuck)
                {
                    _stuck = true;
                    _carried.gameObject.SetActive(true);
                    OnStick?.Invoke();
                }

                float u = (_t - _outDuration) / _backDuration;

                if (u >= 1f)
                {
                    Hide();
                    OnDone?.Invoke();
                    return;
                }

                p = 1f - u * u;
            }

            Draw(mouth, p);
        }

        void Draw(Vector2 mouth, float p)
        {
            // Язык растёт изо рта, а рот у жабы поднят над доской. Плоскую точку из
            // контракта используем только как запасную: без жабы взять высоту негде.
            Vector3 mouthWorld = _frog != null ? _frog.MouthWorldPos : Space3D.ToWorld(mouth, Height);

            Vector2 mouthSpec = new Vector2(mouthWorld.x / Space3D.Scale,
                                           -mouthWorld.z / Space3D.Scale);
            float mouthHeight = mouthWorld.y;

            Vector2 d = _target - mouthSpec;
            Vector2 perp = new Vector2(-d.y, d.x).normalized;
            Vector2 ctrl = (mouthSpec + _target) * 0.5f + perp * d.magnitude * 0.17f * _side;

            Vector2 ctrl1 = Vector2.Lerp(mouthSpec, ctrl, p);
            Vector2 tipSpec = Quad(mouthSpec, ctrl, _target, p);

            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                Vector2 point = Quad(mouthSpec, ctrl1, tipSpec, t);

                // Высота спускается ото рта к доске. Считать её надо по доле **всей**
                // кривой, а не нарисованного куска: `t` пробегает 0..1 по тому, что
                // видно сейчас, и на коротком языке спуск до доски случился бы весь
                // сразу — язык нырял бы вниз, едва высунувшись.
                float lift = Mathf.Lerp(mouthHeight, Height, t * p)
                           + Mathf.Sin(t * Mathf.PI) * 0.25f;

                _line.SetPosition(i, Space3D.ToWorld(point, lift));
            }

            float width = Space3D.Size(8f - 2f * p);
            _line.startWidth = width;
            _line.endWidth = width * 0.75f;

            float tipHeight = Mathf.Lerp(mouthHeight, Height, p);
            _tip.position = Space3D.ToWorld(tipSpec, tipHeight);

            if (_stuck)
            {
                _carried.position = Space3D.ToWorld(tipSpec, tipHeight);
                _carried.localScale = Vector3.one * Mathf.Max(0.25f, p);
            }
        }

        void Hide()
        {
            Active = false;
            _stuck = false;

            // Null здесь настоящий: Build зовёт Hide до того, как жабу назначили.
            if (_frog != null) _frog.ReleaseAim();

            _line.enabled = false;
            _tip.gameObject.SetActive(false);
            _carried.gameObject.SetActive(false);
        }

        static Vector2 Quad(Vector2 a, Vector2 ctrl, Vector2 b, float t)
        {
            float mt = 1f - t;
            return mt * mt * a + 2f * mt * t * ctrl + t * t * b;
        }
    }
}
