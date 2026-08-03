using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using FrogCart.Core;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Собирает всю сцену кодом: иерархия из docs/unity-spec/06-unity-implementation.md,
    /// геометрия и цвета — из 01-layout.md и 02-art.md.
    ///
    /// Почему кодом, а не префабами: числа спеки тогда лежат в одном месте рядом со ссылкой
    /// на документ, а не растворяются по инспекторам, где их не найти и не сверить.
    /// Порядок создания = порядок отрисовки.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] GameConfig config;
        [SerializeField] ColorPalette palette;
        [SerializeField] LevelData level;

        [Tooltip("Уровень с заведомо недостаточной ёмкостью — проверка механики проигрыша " +
                 "из 07-checklist.md. Выбирается ключом -uselevel losetest.")]
        [SerializeField] LevelData loseTestLevel;

        [Tooltip("Витринный уровень — например, картинка из Food Hunt. Ключ -uselevel showcase.")]
        [SerializeField] LevelData showcaseLevel;

#if UNITY_EDITOR
        /// <summary>
        /// Присваивание ассетов из editor-скрипта сборки сцены.
        /// Прямое, а не через SerializedObject: тот молча оставлял поля пустыми,
        /// и сцена падала на старте с NullReferenceException.
        /// </summary>
        public void EditorAssign(GameConfig gameConfig, ColorPalette colorPalette,
                                 LevelData levelData, LevelData loseTest, LevelData showcase)
        {
            config = gameConfig;
            palette = colorPalette;
            level = levelData;
            loseTestLevel = loseTest;
            showcaseLevel = showcase;
        }

        public bool EditorHasAllRefs => config != null && palette != null && level != null;
#endif

        RectTransform _game;
        GameController _controller;

        void Awake()
        {
            if (config == null || palette == null || level == null)
            {
                Debug.LogError("[GameBootstrap] Не заданы ссылки на ассеты " +
                               $"(config={config}, palette={palette}, level={level}). " +
                               "Пересобрать: меню Frog Cart → Rebuild Assets And Scene.");
                enabled = false;
                return;
            }

            ApplyLevelOverride();

            BuildCanvas();
            BuildScene();
        }

        /// <summary>
        /// Ключ -uselevel losetest подменяет уровень на заведомо непроходимый.
        /// Нужен ради пункта чек-листа «тест механики проигрыша»: подстроить настоящее
        /// поражение обычной игрой нельзя, а вызывать панель напрямую — значит проверить
        /// картинку, но не правило.
        /// </summary>
        void ApplyLevelOverride()
        {
            var args = System.Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length - 1; i++)
            {
                if (args[i] != "-uselevel") continue;
                var picked = args[i + 1] == "losetest" ? loseTestLevel
                           : args[i + 1] == "showcase" ? showcaseLevel
                           : null;

                if (picked == null)
                {
                    Debug.LogError($"[GameBootstrap] Уровень '{args[i + 1]}' не задан.");
                    return;
                }

                level = picked;
                Debug.Log($"[GameBootstrap] Уровень подменён на '{args[i + 1]}'.");
                return;
            }
        }

        void BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas",
                typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(390f, 844f);

            // Expand масштабирует по меньшему из двух отношений, то есть вписывает
            // всю область 390x844 целиком и делает её настолько крупной, насколько
            // позволяет окно. Привязка к одной только ширине (Match = 0) на непортретном
            // окне либо режет низ, либо оставляет игру крошечной в углу.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

            if (FindAnyObjectByType<EventSystem>() == null)
            {
                var events = new GameObject("EventSystem",
                    typeof(EventSystem), typeof(StandaloneInputModule));
                events.transform.SetParent(transform, false);
            }

            // Подложка на весь холст: при вписывании по короткой стороне остаются поля,
            // и пусть они будут того же дерева, а не пустотой рядом с картинкой.
            var backdropGo = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            backdropGo.transform.SetParent(canvasGo.transform, false);

            var backdropRt = (RectTransform)backdropGo.transform;
            backdropRt.anchorMin = Vector2.zero;
            backdropRt.anchorMax = Vector2.one;
            backdropRt.offsetMin = Vector2.zero;
            backdropRt.offsetMax = Vector2.zero;

            var backdrop = backdropGo.GetComponent<Image>();
            backdrop.sprite = ProcSprite.White();
            backdrop.color = ProcSprite.Hex("6D4322");
            backdrop.raycastTarget = false;

            var gameGo = new GameObject("Game", typeof(RectTransform));
            gameGo.transform.SetParent(canvasGo.transform, false);

            // Игровая область висит по центру холста, а не в его левом верхнем углу:
            // при Expand холст в одном измерении крупнее опорных 390x844, и якорь
            // в углу увёл бы всю игру в сторону. Пивот остаётся в левом верхнем углу —
            // на нём держится вся арифметика spec-координат.
            _game = (RectTransform)gameGo.transform;
            _game.anchorMin = new Vector2(0.5f, 0.5f);
            _game.anchorMax = new Vector2(0.5f, 0.5f);
            _game.pivot = new Vector2(0f, 1f);
            _game.sizeDelta = new Vector2(390f, 844f);
            _game.anchoredPosition = new Vector2(-195f, 422f);
        }

        void BuildScene()
        {
            var tween = gameObject.AddComponent<Tweener>();
            var shake = gameObject.AddComponent<ScreenShake>();

            BuildWoodBackground();
            BuildRails();
            BuildFrame();

            var gridRoot = NewNode("GridRoot", _game, 0f, 0f, 390f, 844f);

            // Размер сетки диктует уровень: 14x16 своих или 35x35 из Food Hunt.
            var grid = gameObject.AddComponent<GridView>();
            grid.Build(gridRoot, palette, tween, level.Rows.Length, level.Rows[0].Length);

            var flash = NewImage("FlashOverlay", _game, 37f, 100f, 316f, 496f);
            flash.sprite = ProcSprite.Make(ProcSprite.Rounded.Flat(316, 496, 20f, Color.white, "flash"));
            flash.color = new Color(1f, 1f, 1f, 0f);

            var frogLayer = NewNode("FrogLayer", _game, 0f, 0f, 390f, 844f);
            var cartLayer = NewNode("CartLayer", _game, 0f, 0f, 390f, 844f);
            var tongueLayer = NewNode("TongueLayer", _game, 0f, 0f, 390f, 844f);

            var frogs = new FrogView[5];
            var carts = new CartView[5];
            var tongues = new TongueView[5];

            for (int i = 0; i < 5; i++)
            {
                var frogGo = new GameObject($"FrogView_{i}");
                frogGo.transform.SetParent(transform, false);
                frogs[i] = frogGo.AddComponent<FrogView>();
                frogs[i].Build(frogLayer);

                var cartGo = new GameObject($"CartView_{i}");
                cartGo.transform.SetParent(transform, false);
                carts[i] = cartGo.AddComponent<CartView>();
                carts[i].Build(cartLayer, tween);

                var tongueGo = new GameObject($"TongueView_{i}");
                tongueGo.transform.SetParent(transform, false);
                tongues[i] = tongueGo.AddComponent<TongueView>();
                tongues[i].Build(tongueLayer);
            }

            var queue = gameObject.AddComponent<QueueView>();
            queue.Build(_game, palette, tween);

            var hud = gameObject.AddComponent<HudView>();

            var confettiLayer = NewNode("ConfettiLayer", _game, 0f, 0f, 390f, 844f);
            var confetti = gameObject.AddComponent<ConfettiBurst>();
            confetti.Build(confettiLayer, palette);

            var panel = gameObject.AddComponent<PanelView>();

            _controller = gameObject.AddComponent<GameController>();
            _controller.Config = config;
            _controller.Palette = palette;
            _controller.Level = level;
            _controller.Grid = grid;
            _controller.Hud = hud;
            _controller.Panel = panel;
            _controller.Queue = queue;
            _controller.Shake = shake;
            _controller.Confetti = confetti;
            _controller.Tween = tween;
            _controller.Flash = new ImageFlash(flash);
            _controller.Carts = carts;
            _controller.Frogs = frogs;
            _controller.Tongues = tongues;

            hud.Build(_game, tween, () => _controller.TogglePause());
            panel.Build(_game, palette, tween, () => _controller.Restart(), () => _controller.Resume());

            // Приёмник ввода ложится ровно на сетку, а не на всю область рамки:
            // при некратном размере картинка центрируется, и промах был бы на полклетки.
            var inputCatcher = NewImage("GridInput", _game,
                                        grid.OriginX, grid.OriginY,
                                        grid.CellW * grid.Cols, grid.CellH * grid.Rows);
            inputCatcher.color = new Color(0f, 0f, 0f, 0f);
            inputCatcher.raycastTarget = true;
            var input = inputCatcher.gameObject.AddComponent<GridInput>();
            input.Setup(_controller, grid, inputCatcher.rectTransform);
            inputCatcher.transform.SetSiblingIndex(tongueLayer.GetSiblingIndex());

            shake.Setup(_game);
            _controller.StartLevel();
        }

        void BuildWoodBackground()
        {
            var wood = NewImage("WoodBackground", _game, 0f, 0f, 390f, 844f);
            wood.sprite = ProcSprite.White();
            wood.color = ProcSprite.Hex("8A5A30");

            // Доски полосами 54 / 4 / 2 по вертикали.
            float y = 0f;
            int index = 0;
            while (y < 844f)
            {
                AddPlankStripe(wood.rectTransform, y, 54f, ProcSprite.Hex("8F5E33"), index++);
                y += 54f;
                AddPlankStripe(wood.rectTransform, y, 4f, ProcSprite.Hex("7A4D28"), index++);
                y += 4f;
                AddPlankStripe(wood.rectTransform, y, 2f, ProcSprite.Hex("6D4322"), index++);
                y += 2f;
            }
        }

        void AddPlankStripe(RectTransform parent, float y, float h, Color color, int index)
        {
            if (y >= 844f) return;

            var stripe = NewImage($"Plank_{index}", parent, 0f, y, 390f, Mathf.Min(h, 844f - y));
            stripe.sprite = ProcSprite.White();
            stripe.color = color;
        }

        void BuildFrame()
        {
            // Внешняя рамка X 29 Y 92 W 332 H 512 R 26.
            var outer = NewImage("FrameOuter", _game, 29f, 92f, 332f, 512f);
            outer.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 332, h = 512,
                radii = new Vector4(26f, 26f, 26f, 26f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("FDF5E2"),
                c1 = ProcSprite.Hex("F8ECD2"),
                c2 = ProcSprite.Hex("F3E3C2"),
                midStop = 0.5f,
                insetTop = 2f,
                insetTopColor = new Color(1f, 1f, 1f, 0.9f),
                insetBottom = 8f,
                insetBottomColor = new Color(90f / 255f, 54f / 255f, 20f / 255f, 0.35f),
                key = "frameOuter",
            });

            // Внутренняя вдавленная панель X 37 Y 100 W 316 H 496 R 20.
            var panel = NewImage("FramePanel", _game, 37f, 100f, 316f, 496f);
            panel.sprite = ProcSprite.Make(new ProcSprite.Rounded
            {
                w = 316, h = 496,
                radii = new Vector4(20f, 20f, 20f, 20f),
                gradientAngleDeg = 180f,
                c0 = ProcSprite.Hex("ECDCBB"),
                c1 = ProcSprite.Hex("ECDCBB"),
                c2 = ProcSprite.Hex("E5D3AE"),
                midStop = 0.5f,
                insetTop = 4f,
                insetTopColor = new Color(120f / 255f, 86f / 255f, 40f / 255f, 0.35f),
                key = "framePanel",
            });
        }

        /// <summary>Восемь слоёв рельсов по одной осевой — снизу вверх, как в 01-layout.md.</summary>
        void BuildRails()
        {
            var railLayer = NewNode("RailLayer", _game, 0f, 0f, 390f, 844f);

            const int CenterW = (int)(LoopPath.RR - LoopPath.RL);   // 360
            const int CenterH = (int)(LoopPath.RB - LoopPath.RT);   // 560

            AddRing(railLayer, LoopPath.RL, LoopPath.RT, CenterW, CenterH, 48f, 40f,
                    new Color(50f / 255f, 24f / 255f, 6f / 255f, 0.40f), 0f, 0f, "railShadow");

            AddRing(railLayer, LoopPath.RL, LoopPath.RT, CenterW, CenterH, 48f, 30f,
                    ProcSprite.Hex("8D5B2D"), 0f, 0f, "railDark");

            AddRing(railLayer, LoopPath.RL, LoopPath.RT, CenterW, CenterH, 48f, 26f,
                    ProcSprite.Hex("A5713B"), 0f, 0f, "railLight");

            AddSleepers(railLayer);

            // Внутренний контур: X 23, Y 76, W 344, H 544, RAD 40.
            AddRing(railLayer, 23f, 76f, 344, 544, 40f, 5f, ProcSprite.Hex("5F3919"), 0f, 0f, "railInnerBase");
            AddRing(railLayer, 23f, 76f, 344, 544, 40f, 3f, ProcSprite.Hex("CFD6DA"), 0f, 0f, "railInnerMetal");

            // Внешний контур: X 7, Y 60, W 376, H 576, RAD 56.
            AddRing(railLayer, 7f, 60f, 376, 576, 56f, 5f, ProcSprite.Hex("5F3919"), 0f, 0f, "railOuterBase");
            AddRing(railLayer, 7f, 60f, 376, 576, 56f, 3f, ProcSprite.Hex("CFD6DA"), 0f, 0f, "railOuterMetal");
        }

        /// <summary>
        /// Шпалы — отдельные поперечины вдоль пути, а не пунктир в текстуре кольца.
        /// Пунктиром они получались диагональными: фаза считалась грубой формулой от
        /// координат, а не от длины дуги. Здесь каждая шпала ставится по LoopPath и
        /// поворачивается по касательной, то есть ложится поперёк рельсов, как ей и
        /// положено. Шаг 5/15 из 01-layout.md, ширина 26.
        /// </summary>
        void AddSleepers(RectTransform parent)
        {
            const float Step = 20f;      // 5 штрих + 15 пропуск
            const float Length = 5f;
            const float Width = 26f;

            var path = new LoopPath();
            int count = Mathf.RoundToInt(path.Perimeter / Step);
            var sprite = ProcSprite.Make(ProcSprite.Rounded.Flat(
                Mathf.RoundToInt(Length), Mathf.RoundToInt(Width), 1f, Color.white, "sleeper"));

            for (int i = 0; i < count; i++)
            {
                path.Sample(i * Step, out var pos, out float angle);

                var go = new GameObject($"Sleeper_{i}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(parent, false);

                var rt = (RectTransform)go.transform;
                SpecRect.AnchorCenter(rt);
                rt.sizeDelta = new Vector2(Length, Width);
                SpecRect.MoveTo(rt, pos);
                SpecRect.RotateTo(rt, angle);

                var image = go.GetComponent<Image>();
                image.sprite = sprite;
                image.color = ProcSprite.Hex("794922", 0.59f);
                image.raycastTarget = false;
            }
        }

        void AddRing(RectTransform parent, float x, float y, int w, int h,
                     float radius, float stroke, Color color, float dash, float gap, string key)
        {
            int pad = Mathf.CeilToInt(stroke * 0.5f) + 1;

            var image = NewImage(key, parent, x - pad, y - pad, w + pad * 2, h + pad * 2);
            image.sprite = ProcSprite.Ring(w, h, radius, stroke, color, dash, gap, key);
        }

        static RectTransform NewNode(string name, RectTransform parent,
                                     float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SpecRect.Place((RectTransform)go.transform, x, y, w, h);
            return (RectTransform)go.transform;
        }

        static Image NewImage(string name, RectTransform parent,
                              float x, float y, float w, float h)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            SpecRect.Place((RectTransform)go.transform, x, y, w, h);

            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }
    }
}
