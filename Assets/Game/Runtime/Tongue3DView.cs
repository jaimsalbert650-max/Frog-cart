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

            _line.enabled = true;
            _tip.gameObject.SetActive(true);
        }

        public void SetCarriedBlock(ColorPalette palette, int colorId)
            => _carriedRenderer.sharedMaterial =
                   ProcMesh.Glossy(palette.Get(colorId).baseColor, $"mat_block{colorId}");

        public void Stop() => Hide();

        public void UpdateFrame(float dt, Vector2 mouth)
        {
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
            Vector2 d = _target - mouth;
            Vector2 perp = new Vector2(-d.y, d.x).normalized;
            Vector2 ctrl = (mouth + _target) * 0.5f + perp * d.magnitude * 0.17f * _side;

            Vector2 ctrl1 = Vector2.Lerp(mouth, ctrl, p);
            Vector2 tipSpec = Quad(mouth, ctrl, _target, p);

            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                Vector2 point = Quad(mouth, ctrl1, tipSpec, t);

                // Дуга по высоте: язык выгибается над доской, а не скребёт по ней.
                float lift = Height + Mathf.Sin(t * Mathf.PI) * 0.25f;
                _line.SetPosition(i, Space3D.ToWorld(point, lift));
            }

            float width = Space3D.Size(8f - 2f * p);
            _line.startWidth = width;
            _line.endWidth = width * 0.75f;

            _tip.position = Space3D.ToWorld(tipSpec, Height);

            if (_stuck)
            {
                _carried.position = Space3D.ToWorld(tipSpec, Height);
                _carried.localScale = Vector3.one * Mathf.Max(0.25f, p);
            }
        }

        void Hide()
        {
            Active = false;
            _stuck = false;

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
