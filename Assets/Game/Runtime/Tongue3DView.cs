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

        // ── физика языка ────────────────────────────────────────────────────────
        //
        // Язык больше не выкладывается по кривой целиком. По кривой идут только его
        // концы: корень сидит во рту, кончик — там, где ему положено по расписанию
        // выстрела. Всё между ними — верёвка, которую считает Верле.
        //
        // Концы кинематические не ради простоты, а потому что от них зависит игра.
        // Момент прилипания и весь темп автоукуса завязаны на tongueOut и
        // tongueBack (GameController.BiteInterval), и кончик обязан приходить в
        // цель ровно тогда же, когда приходил раньше. Физике оставлено то, что
        // ни на что не влияет, — форма языка между ртом и целью.

        /// <summary>
        /// Притяжение. Заметно слабее настоящего: язык летит две десятых секунды,
        /// и при 9.8 провис за это время не успел бы прочитаться, зато на возврате,
        /// когда верёвка провисает, лишний вес утянул бы её в доску.
        /// </summary>
        const float Gravity = -6.5f;

        /// <summary>
        /// Сколько скорости точка уносит в следующий шаг. Ниже — язык вялый и
        /// висит тряпкой, выше — звенит и не успокаивается за время возврата.
        /// </summary>
        const float Damping = 0.87f;

        /// <summary>Проходов натяжения за шаг. Меньше пяти верёвка тянется.</summary>
        const int RelaxPasses = 8;

        /// <summary>
        /// Насколько верёвка длиннее прямой от рта до кончика. Ровно по прямой она
        /// была бы строго натянута и физика ничего бы не дала — весь провис и всё
        /// хлёсткое движение живут в этих восьми процентах запаса.
        /// </summary>
        const float Slack = 1.08f;

        /// <summary>
        /// Потолок шага. Кадр может провалиться — на загрузке уровня или на
        /// первом кадре после паузы, — и Верле от большого шага взрывается:
        /// точки разлетаются, язык на кадр становится звездой. Длинный кадр
        /// дробится на несколько коротких.
        /// </summary>
        const float MaxStep = 1f / 60f;

        readonly Vector3[] _points = new Vector3[Segments + 1];
        readonly Vector3[] _prev = new Vector3[Segments + 1];

        /// <summary>
        /// Разложена ли верёвка. На выстреле она собирается в точку рта, иначе
        /// первый кадр протянул бы её от старой цели к новой через пол-экрана.
        /// </summary>
        bool _ropeReady;

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
            _ropeReady = false;
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

            Draw(mouth, p, dt);
        }

        void Draw(Vector2 mouth, float p, float dt)
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

            // Кривая осталась ровно для одного — пути кончика. Форму языка она
            // больше не задаёт, ею занят Simulate. Боковой вынос по `_side` тем
            // самым сохранён: язык по-прежнему уходит к цели дугой, а не по прямой.
            Vector2 tipSpec = Quad(mouthSpec, ctrl, _target, p);

            float tipLift = Mathf.Lerp(mouthHeight, Height, p);
            Vector3 tipWorld = Space3D.ToWorld(tipSpec, tipLift);

            // На выстреле верёвка собрана в точку рта: язык выстреливает изо рта,
            // а не проявляется натянутым от старой цели к новой.
            if (!_ropeReady)
            {
                for (int i = 0; i <= Segments; i++)
                    _prev[i] = _points[i] = mouthWorld;

                _ropeReady = true;
            }

            Simulate(mouthWorld, tipWorld, dt);

            for (int i = 0; i <= Segments; i++) _line.SetPosition(i, _points[i]);

            float width = Space3D.Size(8f - 2f * p);
            _line.startWidth = width;
            _line.endWidth = width * 0.75f;

            _tip.position = tipWorld;

            if (_stuck)
            {
                _carried.position = tipWorld;
                _carried.localScale = Vector3.one * Mathf.Max(0.25f, p);
            }
        }

        /// <summary>
        /// Шаг верёвки: свободный полёт точек, потом натяжение звеньев.
        ///
        /// Хлёст берётся не из отдельного правила, а отсюда же. Кончик срывается
        /// с места быстрее, чем середина успевает за ним: у середины есть инерция,
        /// и звенья дотягивают её следом уже после того, как кончик ушёл. На
        /// возврате наоборот — кончик идёт домой, верёвка обвисает, и её ведёт
        /// по дуге. Обе повадки настоящего языка получаются даром, их не пишут.
        /// </summary>
        void Simulate(Vector3 mouth, Vector3 tip, float dt)
        {
            if (dt <= 0f) { PinEnds(mouth, tip); return; }

            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / MaxStep), 1, 4);
            float h = dt / steps;

            // Длина звена считается от текущей прямой рот-кончик, а не от полной
            // длины языка: язык и правда становится короче, когда его втягивают,
            // и верёвка обязана укорачиваться вместе с ним. Иначе на возврате
            // остался бы моток, которому некуда деться.
            float rest = Vector3.Distance(mouth, tip) * Slack / Segments;

            for (int step = 0; step < steps; step++)
            {
                for (int i = 1; i < Segments; i++)
                {
                    Vector3 velocity = (_points[i] - _prev[i]) * Damping;

                    _prev[i] = _points[i];
                    _points[i] += velocity + new Vector3(0f, Gravity, 0f) * h * h;
                }

                for (int pass = 0; pass < RelaxPasses; pass++)
                {
                    PinEnds(mouth, tip);

                    for (int i = 0; i < Segments; i++)
                    {
                        Vector3 delta = _points[i + 1] - _points[i];
                        float length = delta.magnitude;

                        if (length < 1e-5f) continue;

                        Vector3 shift = delta * ((length - rest) / length) * 0.5f;

                        _points[i] += shift;
                        _points[i + 1] -= shift;
                    }
                }

                PinEnds(mouth, tip);

                // Пол. Провисшая верёвка иначе уходит под доску и язык на середине
                // пропадает: блоки рисуются поверх, и снаружи это выглядит разрывом.
                for (int i = 1; i < Segments; i++)
                    if (_points[i].y < Height) _points[i].y = Height;
            }
        }

        void PinEnds(Vector3 mouth, Vector3 tip)
        {
            _points[0] = mouth;
            _points[Segments] = tip;
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
