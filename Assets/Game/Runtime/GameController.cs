using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using FrogCart.Core;
using FrogCart.Data;

namespace FrogCart.Runtime
{
    public enum GameState { Play, Pause, Win, Lose }

    /// <summary>
    /// Вся логика игры. Представления только рисуют — так велит
    /// docs/unity-spec/06-unity-implementation.md, и это же делает поведение отлаживаемым:
    /// один Update в детерминированном порядке двигает dist, слоты, жаб и языки.
    /// </summary>
    public sealed class GameController : MonoBehaviour
    {
        struct Slot
        {
            public int ColorId;
            public int Count;
            public bool Live;
            public bool Exiting;
            public float Recoil;
            public float ExitT;
            public float EnterT;
            public float Squash;

            /// <summary>Осталось сколоть льда. Ноль — вагонетка работает.</summary>
            public int Frozen;

            /// <summary>Номер связки: вагонетки с одним номером уезжают вместе.</summary>
            public int LinkGroup;

            public bool Frosted => Frozen > 0;
        }

        // ── зависимости, выставляются бутстрапом ────────────────────────────────────
        public GameConfig Config;
        public ColorPalette Palette;
        public LevelData Level;

        public IGridView Grid;
        public IHudView Hud;
        public IPanelView Panel;
        public IQueueView Queue;
        public ScreenShake Shake;
        public ConfettiBurst Confetti;
        public Tweener Tween;
        public IFlashOverlay Flash;

        public ICartView[] Carts;
        public IFrogView[] Frogs;
        public ITongueView[] Tongues;

        // ── состояние ───────────────────────────────────────────────────────────────
        LoopPath _path;
        GridModel _grid;
        Exposure _exposure;

        /// <summary>Картинка уровня после обрезки пустых полей. Именно она на доске.</summary>
        string[] _rows;

        /// <summary>Прочность и скрытость клеток — механики, взятые из оригинала.</summary>
        CellLayers _layers;

        /// <summary>
        /// Картинка после обрезки. Открыта наружу, потому что координаты клетки на
        /// доске больше не совпадают с координатами в ассете уровня, и тестам нужен
        /// способ узнать, где на самом деле лежит блок.
        /// </summary>
        public string[] Rows => _rows;
        Slot[] _slots;
        List<LevelData.CartDef> _queue;
        int _queueIndex;

        readonly HashSet<int> _reserved = new HashSet<int>();
        readonly int[] _reservedPerColor = new int[GridModel.MaxColor + 1];
        readonly int[] _capacity = new int[GridModel.MaxColor + 1];

        float _dist;
        int _eaten;
        int _total;
        float _clapPhase;

        /// <summary>Очередь как она есть сейчас — нужна вводу и тестам.</summary>
        public System.Collections.Generic.IReadOnlyList<LevelData.CartDef> QueueCarts => _queue;

        /// <summary>
        /// Автоукус можно выключить. Нужно тестам: они проверяют один конкретный
        /// укус по конкретной клетке, а вагонетка, кусающая сама раз в 0.28 с,
        /// добавляет к счётчику лишнее и делает проверку недостоверной.
        /// </summary>
        public bool AutoBiteEnabled { get; set; } = true;

        public GameState State { get; private set; } = GameState.Play;
        public float ChainDelay => Config.chainDelay;

        /// <summary>Съедено блоков и сколько всего — для HUD, тестов и аналитики.</summary>
        public int Eaten => _eaten;
        public int Total => _total;

        // ── жизненный цикл ──────────────────────────────────────────────────────────

        public void StartLevel()
        {
            _path = new LoopPath();
            BuildLevel();
        }

        void BuildLevel()
        {
            StopAllCoroutines();
            Tween.CancelAll();

            // Картинка обрезается по своим же границам: в данных уровня вокруг
            // рисунка обычно остаются пустые ряды и столбцы, и на доске он лежал
            // бы в углу большого пустого листа.
            //
            // Окно считается один раз по картинке и применяется ко всем слоям:
            // у слоя прочности своя форма, и посчитай его границы отдельно —
            // слои разъехались бы относительно картинки на пару клеток.
            var window = LevelCrop.Measure(Level.Rows);
            _rows = LevelCrop.Apply(Level.Rows, window);

            _layers = CellLayers.Build(_rows,
                LevelCrop.Apply(Level.HpRows, window),
                LevelCrop.Apply(Level.HiddenRows, window));

            _grid = GridModel.FromRows(_rows);
            _exposure = new Exposure(_grid);
            _total = _grid.TotalBlocks;
            _eaten = 0;
            _dist = 0f;
            _queueIndex = 0;
            _clapPhase = 0f;

            _reserved.Clear();
            System.Array.Clear(_reservedPerColor, 0, _reservedPerColor.Length);

            // Очередь — это все вагонетки уровня: и те, что в данных лежат
            // в loopCarts, и те, что в queue. Контур стартует пустым, поэтому
            // деление на «стартовые» и «запасные» потеряло смысл, а собирать оба
            // списка вместе дешевле, чем переделывать каждый уровень.
            //
            // Пустые отсекаются: конвертер добивает loopCarts заглушками с нулевой
            // ёмкостью до пяти, и они показались бы в очереди пустыми вагонетками.
            _queue = new List<LevelData.CartDef>();

            foreach (var def in Level.LoopCarts)
                if (def.capacity > 0) _queue.Add(def);

            foreach (var def in Level.Queue)
                if (def.capacity > 0) _queue.Add(def);

            // Контур стартует пустым.
            //
            // Игра теперь работает как оригинал: игрок тапает не по блоку, а по
            // вагонетке в очереди, она выезжает на свободное место и дальше ест
            // свой цвет сама. По блокам тапать нельзя вовсе, а решение игрока —
            // кого и когда запустить.
            //
            // Вагонетки уровня целиком лежат в очереди; loopCarts остался в данных
            // ради старых уровней и плоской версии, но здесь не используется.
            _slots = new Slot[Carts.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = new Slot { Live = false };

                Carts[i].SetVisible(false);
                Frogs[i].SetVisible(false);
                Tongues[i].Stop();
            }

            for (int r = 0; r < Grid.Rows; r++)
            for (int c = 0; c < Grid.Cols; c++)
            {
                Grid.SetCell(r, c, _grid.Get(r, c));
                Grid.SetCellArmour(r, c, _layers.Hp(r, c));
                Grid.SetCellHidden(r, c, _layers.IsHidden(r, c));
            }

            // Скрытые клетки, оказавшиеся у края с самого начала, открываются сразу:
            // прятать то, до чего язык дотягивается первым же ходом, бессмысленно.
            RevealExposedCells();

            Queue.Rebuild(_queue, _queueIndex);
            Confetti.StopAndHide();
            Panel.Hide();
            Flash.SetAlpha(0f);

            Hud.SetLevel(Level.LevelNumber);
            Hud.SetProgress(0f, instant: true);

            State = GameState.Play;

            // Один раз через 0.4 c после старта — проверка заведомо непроходимого уровня.
            StartCoroutine(DelayedStartLoseCheck());
        }

        /// <summary>
        /// Открыть скрытые клетки, до которых язык теперь дотягивается.
        ///
        /// В оригинале скрытый пиксель показывает цвет, когда становится достижим —
        /// это и есть смысл механики: игрок не может планировать наперёд ту часть
        /// картинки, до которой ещё не добрался.
        ///
        /// Проход по всей доске, а не по соседям снятой клетки: снятие одного блока
        /// иногда вскрывает целый карман, и скрытые клетки на его дальней стенке
        /// тоже стали видимыми. Проход стоит четыре проверки на клетку и делается
        /// только на уровнях, где скрытые клетки вообще есть.
        /// </summary>
        void RevealExposedCells()
        {
            if (!_layers.HasHidden) return;

            for (int r = 0; r < _grid.Rows; r++)
            for (int c = 0; c < _grid.Cols; c++)
            {
                if (!_layers.IsHidden(r, c)) continue;
                if (!_exposure.IsExposed(r, c)) continue;

                _layers.Reveal(r, c);
                Grid.SetCellHidden(r, c, false);
                Grid.SetCell(r, c, _grid.Get(r, c));
                Grid.SetCellArmour(r, c, _layers.Hp(r, c));
            }
        }

        IEnumerator DelayedStartLoseCheck()
        {
            yield return new WaitForSecondsRealtime(Config.startLoseCheckDelay);
            if (State == GameState.Play) CheckLose();
        }

        public void Restart() => BuildLevel();

        public void TogglePause()
        {
            if (State == GameState.Play) { State = GameState.Pause; Panel.ShowPause(); }
            else if (State == GameState.Pause) { State = GameState.Play; Panel.Hide(); }
        }

        public void Resume()
        {
            if (State != GameState.Pause) return;
            State = GameState.Play;
            Panel.Hide();
        }

        // ── ход игрока: запуск вагонетки ────────────────────────────────────────────

        /// <summary>Пауза между укусами работающей вагонетки.</summary>
        const float BiteInterval = 0.28f;

        readonly float[] _nextBite = new float[8];

        /// <summary>
        /// Отправить вагонетку из очереди на контур. Это единственное действие игрока.
        ///
        /// Возвращает false, если запускать нечего или некуда: очередь кончилась,
        /// свободного места нет, вагонетка ещё во льду. Отказ намеренно тихий —
        /// вызывающему достаточно знать, что хода не было.
        /// </summary>
        public bool SendCart(int queueOffset)
        {
            if (State != GameState.Play) return false;

            int index = _queueIndex + queueOffset;
            if (index < 0 || index >= _queue.Count) return false;

            var def = _queue[index];

            // Замороженную запустить нельзя, но тап по ней скалывает лёд: игрок
            // видит, что действие не пропало даром.
            if (def.frozenCount > 0)
            {
                def.frozenCount--;
                _queue[index] = def;
                Queue.Rebuild(_queue, _queueIndex);
                return false;
            }

            int slot = FindFreeSlot();
            if (slot < 0) return false;

            // Из середины очереди брать можно: игрок сам решает, какой цвет нужен
            // сейчас. Взятая вагонетка выпадает из списка, остальные сдвигаются.
            _queue.RemoveAt(index);

            DockCart(slot, def);
            Queue.Rebuild(_queue, _queueIndex);
            return true;
        }

        int FindFreeSlot()
        {
            for (int i = 0; i < _slots.Length; i++)
                if (!_slots[i].Live && !_slots[i].Exiting) return i;

            return -1;
        }

        void DockCart(int slot, LevelData.CartDef def)
        {
            _slots[slot] = new Slot
            {
                ColorId = def.colorId,
                Count = def.capacity,
                Live = true,
                LinkGroup = def.linkGroup,
                EnterT = 1f,
            };

            _nextBite[slot] = Time.unscaledTime + BiteInterval;

            Carts[slot].SetVisible(true);
            Carts[slot].SetColor(Palette, def.colorId);
            Carts[slot].SetCount(def.capacity);
            Carts[slot].SetFrozen(0);
            Carts[slot].SetLinked(def.linkGroup != 0);

            Frogs[slot].SetVisible(true);
            Frogs[slot].SetColor(Palette, def.colorId);
            Frogs[slot].SetGaze(0f);
            Frogs[slot].SetAlpha(1f);

            Tongues[slot].Stop();

            StartCoroutine(EnterSlot(slot));
        }

        IEnumerator EnterSlot(int slot)
        {
            float elapsed = 0f;

            while (elapsed < Config.cartEnter)
            {
                elapsed += Time.unscaledDeltaTime;
                _slots[slot].EnterT = 1f - Mathf.Clamp01(elapsed / Config.cartEnter);
                yield return null;
            }

            _slots[slot].EnterT = 0f;
        }

        /// <summary>
        /// Работающие вагонетки едят сами. По одному укусу за BiteInterval,
        /// цель — ближайший к вагонетке открытый блок её цвета.
        ///
        /// Пауза нужна не для баланса, а чтобы ход было видно: без неё вагонетка
        /// на двести мест снесла бы свой цвет за один кадр, и игрок не понял бы,
        /// что вообще произошло.
        /// </summary>
        void AutoBite()
        {
            if (!AutoBiteEnabled) return;
            if (State != GameState.Play) return;

            for (int slot = 0; slot < _slots.Length; slot++)
            {
                if (!_slots[slot].Live || _slots[slot].Exiting) continue;
                if (_slots[slot].Count <= 0) continue;
                if (Time.unscaledTime < _nextBite[slot]) continue;

                _nextBite[slot] = Time.unscaledTime + BiteInterval;

                if (!TryBite(slot)) continue;
            }
        }

        /// <summary>Ближайший открытый блок цвета вагонетки. false — есть нечего.</summary>
        bool TryBite(int slot)
        {
            Sample(slot, out var cartPos, out _);

            int color = _slots[slot].ColorId;
            int bestR = -1, bestC = -1;
            float bestDistance = float.MaxValue;

            for (int r = 0; r < _grid.Rows; r++)
            for (int c = 0; c < _grid.Cols; c++)
            {
                if (_grid.Get(r, c) != color) continue;
                if (_layers.IsHidden(r, c)) continue;
                if (_reserved.Contains(r * Grid.Cols + c)) continue;
                if (!_exposure.IsExposed(r, c)) continue;

                float distance = Vector2.SqrMagnitude(Grid.CellCenter(r, c) - cartPos);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                bestR = r;
                bestC = c;
            }

            if (bestR < 0) return false;

            Bite(slot, bestR, bestC);
            return true;
        }

        // ── ход игрока ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Алгоритм Eat из docs/unity-spec/04-gameplay.md, шаг в шаг.
        /// Возвращает true, если язык действительно вылетел: отличать состоявшийся ход
        /// от тапа по пустоте нужно инструментам вроде AutoScreenshot, чтобы поймать кадр.
        /// </summary>
        public bool Eat(int r, int c)
        {
            if (State != GameState.Play) return false;

            int color = _grid.Get(r, c);
            if (color == 0) return false;

            int key = r * Grid.Cols + c;
            if (_reserved.Contains(key)) return false;

            // Язык дотягивается только снаружи: замурованный блок сначала надо открыть,
            // объев то, что его закрывает. Картинка разбирается слоями, а не с середины.
            if (!_exposure.IsExposed(r, c))
            {
                Grid.Wobble(r, c, Config.wobbleDuration);
                return false;
            }

            // По скрытой клетке жаба не бьёт: цвет ещё не известен, значит и вагонетку
            // выбрать нельзя. На деле сюда почти не попадают — скрытая клетка
            // открывается ровно в тот момент, когда выходит наружу, — но правило нужно
            // как страховка, если порядок вызовов когда-нибудь поменяется.
            if (_layers.IsHidden(r, c))
            {
                Grid.Wobble(r, c, Config.wobbleDuration);
                return false;
            }

            Vector2 target = Grid.CellCenter(r, c);
            int slot = FindNearestCart(color, target);

            if (slot < 0)
            {
                // Работающей вагонетки нет, но может стоять замороженная этого цвета.
                // Тогда тап не пропадает даром: он скалывает с неё лёд.
                //
                // Это и есть защита от тупика. Лёд обязан таять от чего-то, что игрок
                // может сделать всегда; привяжи его только к съеденным блокам — и
                // уровень, где замороженная вагонетка единственная подходящая,
                // встал бы намертво, как уже вставал на бесполезных вагонетках.
                if (ChipIce(color, target))
                {
                    Grid.Wobble(r, c, Config.wobbleDuration);
                    return false;
                }

                // Подходящей вагонетки на контуре нет вовсе: ячейка дрожит, счётчики
                // не меняются. Наличие такой вагонетки в очереди не помогает —
                // это оговорено в спеке.
                Grid.Wobble(r, c, Config.wobbleDuration);
                return false;
            }

            Bite(slot, r, c);
            return true;
        }

        /// <summary>
        /// Сам укус: язык летит к клетке, место в вагонетке тратится.
        ///
        /// Выделено из Eat, потому что путей теперь два. Игрок больше не тапает
        /// блоки — работающая вагонетка выбирает цель сама, — но Eat остался:
        /// им пользуется плоская версия и тесты, где важно ударить по конкретной
        /// клетке, а не по той, которую выберет вагонетка.
        /// </summary>
        void Bite(int slot, int r, int c)
        {
            int color = _grid.Get(r, c);
            int key = r * Grid.Cols + c;
            Vector2 target = Grid.CellCenter(r, c);

            _reserved.Add(key);
            _reservedPerColor[color]++;

            _slots[slot].Recoil = Config.recoilImpulse;
            _slots[slot].Count--;

            Carts[slot].SetCount(_slots[slot].Count);
            Carts[slot].PopNumber(Config.numberPop);

            Tongues[slot].SetCarriedBlock(Palette, color);
            Tongues[slot].Fire(target, SideFor(Frogs[slot].MouthSpecPos, target),
                               Config.tongueOut, Config.tongueBack);

            // Прочную клетку язык за раз не уносит. Ёмкость вагонетки тратится на
            // каждый удар, а прогресс двигается только когда блок действительно
            // исчезнет — иначе полоса дошла бы до конца при целой картинке.
            //
            // Проверять прочность можно прямо здесь, до попадания языка: пока клетка
            // в _reserved, второй удар по ней не пройдёт, и значение не изменится.
            bool willBreak = _layers.Hp(r, c) <= 1;

            if (willBreak)
            {
                _eaten++;
                Hud.SetProgress(_eaten / (float)_total, instant: false);

                if (_eaten % Config.shakeEvery == 0)
                {
                    Shake.Shake(Config.shakeDuration);
                    if (Config.vibrate) Vibrate();
                }
            }

            StartCoroutine(RemoveBlockWhenTongueArrives(r, c, key, color, slot));
        }

        /// <summary>
        /// Снятие блока с доски по таймеру, как и написано в спеке (04-gameplay.md:
        /// «через OUT_T блок исчезает из сетки»).
        ///
        /// Привязывать это к событию прилипания языка нельзя, хотя соблазн есть. Язык в
        /// слоте один и переиспользуется: выстрел по тому же слоту раньше, чем долетел
        /// предыдущий, сбрасывает первый, и его событие не наступает никогда. Клетка
        /// остаётся помеченной «в полёте» навсегда, блок больше не тапается, а победа
        /// не наступает — ровно это и случилось на 87 блоках из 88. Таймер потерять нельзя.
        /// </summary>
        IEnumerator RemoveBlockWhenTongueArrives(int r, int c, int key, int color, int slot)
        {
            yield return new WaitForSecondsRealtime(Config.tongueOut);

            bool broken = _layers.Damage(r, c);

            _reserved.Remove(key);
            _reservedPerColor[color]--;
            _slots[slot].Squash = 1f;

            if (!broken)
            {
                // Клетка выстояла: она осталась на месте, картинка не изменилась,
                // проходимость тоже. Показываем только новую прочность.
                Grid.SetCellArmour(r, c, _layers.Hp(r, c));
                Grid.Wobble(r, c, Config.wobbleDuration);

                if (State == GameState.Play) CheckLose();
                yield break;
            }

            _grid.Clear(r, c);
            Grid.SetCell(r, c, 0);

            // Убрали блок — открылось то, что было за ним. Дозаливка от этой клетки,
            // а не полный пересчёт: на доске 35x35 полная заливка после каждого хода
            // давала больше миллиона проходов на партию.
            _exposure.OnCleared(r, c);

            RevealExposedCells();

            // Проверка Exiting обязательна. Счётчик уменьшается в момент тапа, а сюда
            // управление приходит 0.20 c спустя. Два блока, съеденные подряд быстрее
            // этого, без флага запускали замену дважды: очередь теряла лишнюю вагонетку,
            // ёмкость цвета падала, и игра объявляла ложный проигрыш.
            if (_slots[slot].Count <= 0 && _slots[slot].Live && !_slots[slot].Exiting)
            {
                RetireLinkedPartners(slot);
                StartCoroutine(ReplaceCart(slot));
            }

            if (State != GameState.Play) yield break;

            if (_eaten >= _total)
            {
                StartCoroutine(WinSequence());
                yield break;
            }

            // Лёд тает от любого хода, не только от тапа по своему цвету: иначе
            // замороженная вагонетка редкого цвета простояла бы всю партию.
            ThawAfterMove();
            RetireUselessCarts();
            CheckLose();
        }

        /// <summary>
        /// Отправляет с контура вагонетки, которым больше нечего возить: блоков их цвета
        /// на картинке не осталось.
        ///
        /// Без этого игра запирается насмерть. Очередь двигается только когда вагонетка
        /// опустеет, а опустеть может лишь та, чей цвет ещё есть на доске. Стоит объесть
        /// цвета неравномерно — и контур занимают вагонетки с запасом мест и без работы:
        /// они не опустеют никогда, очередь стоит, а блок, чья вагонетка ждёт в очереди,
        /// становится недостижим. Проверка проигрыша при этом молчит и формально права —
        /// ёмкость есть, просто до неё не добраться.
        ///
        /// Ёмкости в уровне 1 заданы с запасом (чёрного 19 мест на 14 блоков), так что
        /// случай не теоретический: именно на нём игра встала на 87 блоках из 88.
        /// </summary>
        void RetireUselessCarts()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Live || _slots[i].Exiting) continue;

                // Пустая уедет сама обычным путём.
                if (_slots[i].Count <= 0) continue;

                int color = _slots[i].ColorId;
                int remaining = _grid.CountOfColor(color) - _reservedPerColor[color];
                if (remaining > 0) continue;

                StartCoroutine(ReplaceCart(i));
            }
        }

        /// <summary>
        /// Связанные вагонетки уезжают вместе: опустела одна — вторая уходит следом,
        /// даже с местами в запасе.
        ///
        /// Направление выбрано именно такое, а не «обе ждут, пока опустеют обе».
        /// Второй вариант выглядит симметричнее, но он запирает контур: пара стоит,
        /// пока не доедена самая редкая из двух красок, и если её на картинке уже
        /// нет — стоит навсегда. Досрочный уход только освобождает место, поэтому
        /// новых тупиков не создаёт.
        /// </summary>
        void RetireLinkedPartners(int slot)
        {
            int group = _slots[slot].LinkGroup;
            if (group == 0) return;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (i == slot) continue;
                if (!_slots[i].Live || _slots[i].Exiting) continue;
                if (_slots[i].LinkGroup != group) continue;

                StartCoroutine(ReplaceCart(i));
            }
        }

        /// <summary>
        /// Сколоть лёд с ближайшей замороженной вагонетки нужного цвета.
        /// Возвращает true, если лёд действительно сколот.
        /// </summary>
        bool ChipIce(int color, Vector2 target)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Live || _slots[i].Exiting) continue;
                if (!_slots[i].Frosted) continue;
                if (_slots[i].ColorId != color || _slots[i].Count <= 0) continue;

                Sample(i, out var pos, out _);
                float distance = Vector2.Distance(pos, target);

                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = i;
            }

            if (best < 0) return false;

            _slots[best].Frozen--;
            _slots[best].Recoil = Config.recoilImpulse;
            Carts[best].SetFrozen(_slots[best].Frozen);

            return true;
        }

        /// <summary>Лёд подтаивает и сам собой — от каждого съеденного блока.</summary>
        void ThawAfterMove()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Live || !_slots[i].Frosted) continue;

                _slots[i].Frozen--;
                Carts[i].SetFrozen(_slots[i].Frozen);
            }
        }

        int FindNearestCart(int color, Vector2 target)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Live || _slots[i].Exiting) continue;

                // Замороженная вагонетка стоит на контуре, но не работает.
                if (_slots[i].Frosted) continue;

                if (_slots[i].ColorId != color || _slots[i].Count <= 0) continue;

                Sample(i, out var pos, out _);
                float distance = Vector2.Distance(pos, target);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>Знак изгиба языка: наружу, а не через всю картинку.</summary>
        static float SideFor(Vector2 mouth, Vector2 target)
        {
            const float ScreenCenterX = 195f;
            float side = Mathf.Sign(mouth.x - ScreenCenterX);
            return Mathf.Approximately(side, 0f) ? 1f : side;
        }

        static void Vibrate()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (!Application.isEditor) Handheld.Vibrate();
#endif
        }

        // ── замена вагонетки ────────────────────────────────────────────────────────

        IEnumerator ReplaceCart(int slot)
        {
            _slots[slot].Exiting = true;

            // Жаба «сыто рыгает» — поп-масштаб 0.3 c.
            yield return new WaitForSecondsRealtime(Config.emptyToExit);

            float elapsed = 0f;
            while (elapsed < Config.cartExit)
            {
                elapsed += Time.unscaledDeltaTime;
                _slots[slot].ExitT = Mathf.Clamp01(elapsed / Config.cartExit);
                yield return null;
            }

            _slots[slot].ExitT = 1f;

            yield return new WaitForSecondsRealtime(Config.exitToDock - Config.cartExit > 0f
                ? Config.exitToDock - Config.cartExit
                : 0f);

            // Место просто освобождается. Следующую вагонетку из очереди сюда
            // не тянем: кого запускать, решает игрок — в этом теперь вся игра.
            _slots[slot] = new Slot { Live = false };

            Carts[slot].SetVisible(false);
            Frogs[slot].SetVisible(false);
            Tongues[slot].Stop();

            if (State != GameState.Play) yield break;

            RetireUselessCarts();
            CheckLose();
        }

        // ── исходы ──────────────────────────────────────────────────────────────────

        void CheckLose()
        {
            System.Array.Clear(_capacity, 0, _capacity.Length);

            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Live) _capacity[_slots[i].ColorId] += _slots[i].Count;

            // Очередь теперь расходуется вынимаем, а не индексом: игрок берёт
            // вагонетку из любого места, и _queueIndex всегда ноль.
            for (int i = 0; i < _queue.Count; i++)
                _capacity[_queue[i].colorId] += _queue[i].capacity;

            // Остаток в ударах, а не в блоках: прочная клетка стоит нескольких.
            // Пересчёт по всей доске, а не инкрементально — он идёт после хода,
            // на 735 клетках это доли миллисекунды, а инкрементальный счётчик
            // пришлось бы держать в согласии с ударами, снятием и перезапуском.
            var hits = LoseCheck.CountHits(_grid, _layers);

            if (!LoseCheck.IsLost(hits, _capacity, _reservedPerColor)) return;

            foreach (var tongue in Tongues) tongue.Stop();

            State = GameState.Lose;
            StartCoroutine(ShowLoseAfter());
        }

        IEnumerator ShowLoseAfter()
        {
            yield return new WaitForSecondsRealtime(Config.loseDelay);
            Panel.ShowLose();
        }

        IEnumerator WinSequence()
        {
            yield return new WaitForSecondsRealtime(Config.winDelay);

            State = GameState.Win;

            // 1. Вспышка по области картинки: 0 → .85 → 0 за 0.5 c.
            Tween.Run(Config.winFlash, Tweener.Linear, t =>
            {
                float alpha = t < 0.4f
                    ? Mathf.Lerp(0f, 0.85f, t / 0.4f)
                    : Mathf.Lerp(0.85f, 0f, (t - 0.4f) / 0.6f);
                Flash.SetAlpha(alpha);
            });

            // 2. Силуэт: 1.2 c плоскими блоками.
            Grid.ShowSilhouette(_rows, true);

            // 4. Конфетти.
            Confetti.Play();

            yield return new WaitForSecondsRealtime(Config.winSilhouette);
            Grid.ShowSilhouette(_rows, false);

            yield return new WaitForSecondsRealtime(
                Mathf.Max(0f, Config.winPanelDelay - Config.winSilhouette));

            Panel.ShowWin();
        }

        // ── кадр ────────────────────────────────────────────────────────────────────

        void Update()
        {
            if (_slots == null) return;

            float dt = Time.unscaledDeltaTime;

            // dist растёт только в Play: в паузе и на исходах контур стоит.
            if (State == GameState.Play) _dist += Config.railSpeed * dt;

            // Работающие вагонетки едят сами — в этом теперь весь игровой процесс.
            AutoBite();

            if (State == GameState.Win) _clapPhase += dt;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Live) continue;

                _slots[i].Recoil = Decay(_slots[i].Recoil, Config.recoilDecay, dt);
                _slots[i].Squash = Decay(_slots[i].Squash, Config.squashDecay, dt);

                Sample(i, out var pos, out float angle);

                float exit = _slots[i].ExitT;
                float enter = _slots[i].EnterT;

                float scale = (1f - exit * 0.5f) * (1f - enter * 0.45f) * RecoilScale(i);
                float alpha = (1f - exit) * (1f - enter);
                float lift = exit * 30f - enter * 46f;

                Carts[i].Place(pos, angle, scale, alpha);

                Frogs[i].SetSquash(_slots[i].Squash);
                Frogs[i].SetClap(State == GameState.Win ? Mathf.Sin(_clapPhase * 11f) * 0.12f : 0f);
                Frogs[i].SetAlpha(alpha);
                Frogs[i].PlaceOnRail(pos, angle, lift, scale);

                Tongues[i].UpdateFrame(dt, Frogs[i].MouthSpecPos);
            }
        }

        /// <summary>
        /// Точка вагонетки на контуре.
        ///
        /// Отдача выстрела здесь больше не участвует. Раньше она вычиталась из
        /// пройденного пути, то есть буквально толкала вагонетку назад по рельсу.
        /// При старой механике выстрел был редким, и толчок читался акцентом;
        /// с автоукусом раз в 0.28 с отдача не успевает погаснуть между укусами,
        /// вагонетка постоянно висит позади своего места и дёргается. Теперь
        /// отдача — сжатие корпуса, а не сдвиг: см. RecoilScale.
        /// </summary>
        void Sample(int slot, out Vector2 pos, out float angle)
        {
            float offset = slot * _path.Perimeter / _slots.Length + 60f;
            float exitExtra = _slots[slot].ExitT * 90f;
            _path.Sample(_dist + offset + exitExtra, out pos, out angle);
        }

        /// <summary>Отдача как приседание корпуса: 1 в покое, меньше — сразу после выстрела.</summary>
        float RecoilScale(int slot)
            => 1f - Mathf.Clamp01(_slots[slot].Recoil / Mathf.Max(1f, Config.recoilImpulse)) * 0.12f;

        static float Decay(float value, float rate, float dt)
            => value <= 0f ? 0f : Mathf.Max(0f, value - value * rate * dt - 0.001f);
    }
}
