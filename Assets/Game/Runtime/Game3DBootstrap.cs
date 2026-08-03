using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FrogCart.Core;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Объёмная сборка сцены. Логика игры та же самая — GameController не знает,
    /// плоско его рисуют или объёмом, потому что общается с представлениями через
    /// договоры из ViewContracts.
    ///
    /// Что осталось плоским намеренно: HUD, панели и вспышка победы. Номер уровня
    /// и процент — это интерфейс, а не мир; выносить их в геометрию значит потерять
    /// читаемость ради принципа.
    /// </summary>
    public sealed class Game3DBootstrap : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] ColorPalette palette;
        [SerializeField] LevelData level;
        [SerializeField] LevelData showcaseLevel;

#if UNITY_EDITOR
        public void EditorAssign(GameConfig gameConfig, ColorPalette colorPalette,
                                 LevelData levelData, LevelData showcase)
        {
            config = gameConfig;
            palette = colorPalette;
            level = levelData;
            showcaseLevel = showcase;
        }

        public bool EditorHasAllRefs => config != null && palette != null && level != null;
#endif

        Camera _camera;
        Transform _world;
        GameController _controller;
        Grid3DView _grid;

        void Awake()
        {
            if (config == null || palette == null || level == null)
            {
                Debug.LogError("[Game3D] Не заданы ссылки на ассеты.");
                enabled = false;
                return;
            }

            ApplyLevelOverride();

            BuildCameraAndLight();
            BuildTable();
            BuildRails();
            BuildGameplay();
        }

        void ApplyLevelOverride()
        {
            var args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-uselevel") continue;
                if (args[i + 1] != "showcase" || showcaseLevel == null) continue;

                level = showcaseLevel;
                Debug.Log("[Game3D] Уровень подменён на витринный.");
                return;
            }
        }

        void BuildCameraAndLight()
        {
            var cameraGo = new GameObject("MainCamera", typeof(Camera));
            cameraGo.tag = "MainCamera";
            cameraGo.transform.SetParent(transform, false);

            _camera = cameraGo.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = ProcSprite.Hex("2A1A0C");
            // Узкий угол вместо широкого: перспектива почти не заваливает дальний край,
            // и доска остаётся читаемой. Головоломке важнее ровная сетка, чем глубина.
            _camera.fieldOfView = 24f;

            FrameWorld();

            var lightGo = new GameObject("Sun", typeof(Light));
            lightGo.transform.SetParent(transform, false);

            var light = lightGo.GetComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.96f, 0.9f);
            light.intensity = 0.95f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.45f;
            lightGo.transform.rotation = Quaternion.Euler(52f, -35f, 0f);

            // Ровный неяркий ambient вместо трёхцветного: тот подсвечивал верхние грани
            // и вместе с бликом Standard выбеливал блоки до неразличимости цвета.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = ProcSprite.Hex("746D62");
        }

        /// <summary>Область мира, которая обязана быть видна: контур рельсов и очередь.</summary>
        static readonly Vector2 WorldMin = new Vector2(2f, 52f);
        static readonly Vector2 WorldMax = new Vector2(388f, 712f);

        /// <summary>
        /// Наводка камеры подбором, а не формулой.
        ///
        /// Считать отлёт тригонометрией пришлось бы отдельно по горизонтали и по
        /// вертикали, с поправкой на наклон и на соотношение сторон окна — и любая
        /// ошибка вылезала бы обрезанным краем на чьём-то экране. Проще отодвигать
        /// камеру, пока все четыре угла области не окажутся внутри кадра: это верно
        /// для любого окна, включая те, которых я не видел.
        /// </summary>
        Vector2Int _framedFor;

        /// <summary>Окно поменяли — наводку надо пересчитать, иначе снова обрежет.</summary>
        void Update()
        {
            var size = new Vector2Int(Screen.width, Screen.height);
            if (size == _framedFor || _camera == null) return;

            FrameWorld();
        }

        void FrameWorld()
        {
            const float Tilt = 50f;
            const float Margin = 0.03f;   // доля кадра, оставленная по краям

            Vector3 center = Space3D.ToWorld((WorldMin + WorldMax) * 0.5f);

            var corners = new[]
            {
                Space3D.ToWorld(WorldMin.x, WorldMin.y),
                Space3D.ToWorld(WorldMax.x, WorldMin.y),
                Space3D.ToWorld(WorldMin.x, WorldMax.y),
                Space3D.ToWorld(WorldMax.x, WorldMax.y),
            };

            _camera.transform.rotation = Quaternion.Euler(Tilt, 0f, 0f);
            Vector3 back = -_camera.transform.forward;
            _framedFor = new Vector2Int(Screen.width, Screen.height);

            float distance = 20f;
            for (int step = 0; step < 400; step++)
            {
                _camera.transform.position = center + back * distance;

                if (AllCornersVisible(corners, Margin)) return;

                distance *= 1.04f;
            }

            Debug.LogWarning("[Game3D] Не удалось вписать сцену в кадр — окно слишком узкое.");
        }

        bool AllCornersVisible(Vector3[] corners, float margin)
        {
            foreach (var corner in corners)
            {
                Vector3 viewport = _camera.WorldToViewportPoint(corner);

                if (viewport.z <= 0f) return false;
                if (viewport.x < margin || viewport.x > 1f - margin) return false;
                if (viewport.y < margin || viewport.y > 1f - margin) return false;
            }

            return true;
        }

        /// <summary>Деревянный стол и кремовая площадка под картинкой.</summary>
        void BuildTable()
        {
            _world = new GameObject("World").transform;
            _world.SetParent(transform, false);

            // Стол намеренно много больше кадра: на широком окне камера отходит дальше,
            // и при скромном размере в кадр попадали его края с пустотой за ними.
            var table = NewBox("Table", _world,
                Space3D.Size(6000f), Space3D.Size(6f), Space3D.Size(6000f),
                ProcMesh.Glossy(ProcSprite.Hex("6D4322"), "mat_table", 0.12f));
            table.transform.position = Space3D.ToWorld(195f, 422f, -Space3D.Size(3f));

            var panel = NewBox("Panel", _world,
                Space3D.Size(316f), Space3D.Size(4f), Space3D.Size(496f),
                ProcMesh.Glossy(ProcSprite.Hex("C9B694"), "mat_panel", 0.05f));
            panel.transform.position = Space3D.ToWorld(195f, 348f, -Space3D.Size(1f));

            var frame = NewBox("Frame", _world,
                Space3D.Size(332f), Space3D.Size(6f), Space3D.Size(512f),
                ProcMesh.Glossy(ProcSprite.Hex("DCCBA9"), "mat_frame", 0.05f));
            frame.transform.position = Space3D.ToWorld(195f, 348f, -Space3D.Size(3f));
        }

        /// <summary>Рельсовый контур: шпалы и две металлические нити по LoopPath.</summary>
        void BuildRails()
        {
            var path = new LoopPath();
            var rails = new GameObject("Rails").transform;
            rails.SetParent(_world, false);

            // Полотно под рельсами: куски шире шага и потому смыкаются в сплошную
            // ленту. Без него между шпалами просвечивал стол.
            var bedMesh = ProcMesh.RoundedBox(Space3D.Size(9f), Space3D.Size(2f),
                                              Space3D.Size(38f), Space3D.Size(1f), "railBed3D");
            var bedMaterial = ProcMesh.Glossy(ProcSprite.Hex("8D5B2D"), "mat_railBed", 0.05f);

            var sleeperMesh = ProcMesh.RoundedBox(Space3D.Size(5f), Space3D.Size(3f),
                                                  Space3D.Size(32f), Space3D.Size(1f), "sleeper3D");
            var sleeperMaterial = ProcMesh.Glossy(ProcSprite.Hex("794922"), "mat_sleeper", 0.1f);

            var railMesh = ProcMesh.RoundedBox(Space3D.Size(3f), Space3D.Size(3f),
                                               Space3D.Size(3f), Space3D.Size(1f), "railPiece3D");
            var railMaterial = ProcMesh.Metal(ProcSprite.Hex("CFD6DA"), "mat_rail");

            const float step = 6f;
            int count = Mathf.RoundToInt(path.Perimeter / step);

            for (int i = 0; i < count; i++)
            {
                path.Sample(i * step, out var pos, out float angle);
                var rotation = Space3D.RotationFromSpecAngle(angle);

                var bed = NewPiece($"Bed_{i}", rails, bedMesh, bedMaterial);
                bed.transform.position = Space3D.ToWorld(pos, Space3D.Size(0.5f));
                bed.transform.rotation = rotation;

                if (i % 2 == 0)
                {
                    var sleeper = NewPiece($"Sleeper_{i}", rails, sleeperMesh, sleeperMaterial);
                    sleeper.transform.position = Space3D.ToWorld(pos, Space3D.Size(1f));
                    sleeper.transform.rotation = rotation;
                }

                // Две нити: смещение поперёк пути на ±8 в spec-единицах.
                foreach (float offset in new[] { -8f, 8f })
                {
                    var ar = angle * Mathf.Deg2Rad;
                    var across = new Vector2(Mathf.Sin(ar), -Mathf.Cos(ar)) * offset;

                    var rail = NewPiece($"Rail_{i}_{offset}", rails, railMesh, railMaterial);
                    rail.transform.position = Space3D.ToWorld(pos + across, Space3D.Size(3f));
                    rail.transform.rotation = rotation;
                }
            }
        }

        void BuildGameplay()
        {
            var tween = gameObject.AddComponent<Tweener>();
            var shake = gameObject.AddComponent<ScreenShake>();

            _grid = gameObject.AddComponent<Grid3DView>();
            _grid.Build(_world, palette, tween, level.Rows.Length, level.Rows[0].Length);

            var carts = new Cart3DView[5];
            var frogs = new Frog3DView[5];
            var tongues = new Tongue3DView[5];

            for (int i = 0; i < 5; i++)
            {
                var cartGo = new GameObject($"Cart3D_{i}");
                cartGo.transform.SetParent(transform, false);
                carts[i] = cartGo.AddComponent<Cart3DView>();
                carts[i].Build(_world, tween, _camera);

                var frogGo = new GameObject($"Frog3D_{i}");
                frogGo.transform.SetParent(transform, false);
                frogs[i] = frogGo.AddComponent<Frog3DView>();
                frogs[i].Build(_world, _camera);

                var tongueGo = new GameObject($"Tongue3D_{i}");
                tongueGo.transform.SetParent(transform, false);
                tongues[i] = tongueGo.AddComponent<Tongue3DView>();
                tongues[i].Build(_world);
            }

            var canvas = BuildOverlayCanvas(out var flash);

            var hud = gameObject.AddComponent<HudView>();
            var panel = gameObject.AddComponent<PanelView>();
            // Очередь теперь тоже объёмная: стоит на своём рельсе перед доской.
            var queue = gameObject.AddComponent<Queue3DView>();
            queue.Build(_world, palette, tween, _camera);

            _controller = gameObject.AddComponent<GameController>();
            _controller.Config = config;
            _controller.Palette = palette;
            _controller.Level = level;
            _controller.Grid = _grid;
            _controller.Hud = hud;
            _controller.Panel = panel;
            _controller.Queue = queue;
            _controller.Shake = shake;
            _controller.Confetti = gameObject.AddComponent<ConfettiBurst>();
            _controller.Tween = tween;
            _controller.Flash = new ImageFlash(flash);
            _controller.Carts = carts;
            _controller.Frogs = frogs;
            _controller.Tongues = tongues;

            var confettiLayer = new GameObject("ConfettiLayer", typeof(RectTransform));
            confettiLayer.transform.SetParent(canvas, false);
            ((ConfettiBurst)_controller.Confetti).Build((RectTransform)confettiLayer.transform, palette);

            hud.Build(canvas, tween, () => _controller.TogglePause());
            panel.Build(canvas, palette, tween, () => _controller.Restart(), () => _controller.Resume());

            var input = gameObject.AddComponent<Grid3DInput>();
            input.Setup(_controller, _grid, _camera);

            shake.Setup(canvas);
            _controller.StartLevel();
        }

        RectTransform BuildOverlayCanvas(out Image flash)
        {
            var canvasGo = new GameObject("Overlay",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }

            var game = new GameObject("Game", typeof(RectTransform));
            game.transform.SetParent(canvasGo.transform, false);

            var rt = (RectTransform)game.transform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = new Vector2(390f, 844f);
            rt.anchoredPosition = new Vector2(-195f, 422f);

            var flashGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(rt, false);
            var flashRt = (RectTransform)flashGo.transform;
            flashRt.anchorMin = Vector2.zero;
            flashRt.anchorMax = Vector2.one;
            flashRt.offsetMin = Vector2.zero;
            flashRt.offsetMax = Vector2.zero;

            flash = flashGo.GetComponent<Image>();
            flash.sprite = ProcSprite.White();
            flash.color = new Color(1f, 1f, 1f, 0f);
            flash.raycastTarget = false;

            return rt;
        }

        static GameObject NewBox(string name, Transform parent,
                                 float w, float h, float d, Material material)
        {
            var mesh = ProcMesh.RoundedBox(w, h, d, Mathf.Min(w, d) * 0.02f, $"box_{name}");
            return NewPiece(name, parent, mesh, material);
        }

        static GameObject NewPiece(string name, Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);

            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;

            return go;
        }
    }
}
