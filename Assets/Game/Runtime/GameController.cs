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
        }

        // ── зависимости, выставляются бутстрапом ────────────────────────────────────
        public GameConfig Config;
        public ColorPalette Palette;
        public LevelData Level;

        public GridView Grid;
        public HudView Hud;
        public PanelView Panel;
        public QueueView Queue;
        public ScreenShake Shake;
        public ConfettiBurst Confetti;
        public Tweener Tween;
        public UnityEngine.UI.Image FlashOverlay;

        public CartView[] Carts;
        public FrogView[] Frogs;
        public TongueView[] Tongues;

        // ── состояние ───────────────────────────────────────────────────────────────
        LoopPath _path;
        GridModel _grid;
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

        public GameState State { get; private set; } = GameState.Play;
        public float ChainDelay => Config.chainDelay;

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

            _grid = GridModel.FromRows(Level.Rows);
            _total = _grid.TotalBlocks;
            _eaten = 0;
            _dist = 0f;
            _queueIndex = 0;
            _clapPhase = 0f;

            _reserved.Clear();
            System.Array.Clear(_reservedPerColor, 0, _reservedPerColor.Length);

            _queue = new List<LevelData.CartDef>(Level.Queue);

            _slots = new Slot[Carts.Length];
            for (int i = 0; i < _slots.Length; i++)
            {
                var def = Level.LoopCarts[i];
                _slots[i] = new Slot { ColorId = def.colorId, Count = def.capacity, Live = true };

                Carts[i].SetVisible(true);
                Carts[i].SetColor(Palette, def.colorId);
                Carts[i].SetCount(def.capacity);

                Frogs[i].SetVisible(true);
                Frogs[i].SetColor(Palette, def.colorId);
                Frogs[i].SetGaze(0f);
                Frogs[i].SetAlpha(1f);

                Tongues[i].Stop();
            }

            for (int r = 0; r < GridView.Rows; r++)
            for (int c = 0; c < GridView.Cols; c++)
                Grid.SetCell(r, c, _grid.Get(r, c));

            Queue.Rebuild(_queue, _queueIndex);
            Confetti.StopAndHide();
            Panel.Hide();
            FlashOverlay.color = new Color(1f, 1f, 1f, 0f);

            Hud.SetLevel(Level.LevelNumber);
            Hud.SetProgress(0f, instant: true);

            State = GameState.Play;

            // Один раз через 0.4 c после старта — проверка заведомо непроходимого уровня.
            StartCoroutine(DelayedStartLoseCheck());
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

        // ── ход игрока ──────────────────────────────────────────────────────────────

        /// <summary>Алгоритм Eat из docs/unity-spec/04-gameplay.md, шаг в шаг.</summary>
        public void Eat(int r, int c)
        {
            if (State != GameState.Play) return;

            int color = _grid.Get(r, c);
            if (color == 0) return;

            int key = r * GridView.Cols + c;
            if (_reserved.Contains(key)) return;

            Vector2 target = GridView.CellCenter(r, c);
            int slot = FindNearestCart(color, target);

            // Подходящей вагонетки на контуре нет: ячейка дрожит, счётчики не меняются.
            // Наличие такой вагонетки в очереди не помогает — это оговорено в спеке.
            if (slot < 0)
            {
                Grid.Wobble(r, c, Config.wobbleDuration);
                return;
            }

            _reserved.Add(key);
            _reservedPerColor[color]++;

            _slots[slot].Recoil = Config.recoilImpulse;
            _slots[slot].Count--;

            Carts[slot].SetCount(_slots[slot].Count);
            Carts[slot].PopNumber(Config.numberPop);

            Tongues[slot].SetCarriedBlock(Palette, color);
            Tongues[slot].Fire(target, SideFor(Frogs[slot].MouthSpecPos, target),
                               Config.tongueOut, Config.tongueBack);

            _eaten++;
            Hud.SetProgress(_eaten / (float)_total, instant: false);

            if (_eaten % Config.shakeEvery == 0)
            {
                Shake.Shake(Config.shakeDuration);
                if (Config.vibrate) Vibrate();
            }

            int capturedSlot = slot;
            int capturedColor = color;

            Tongues[slot].OnStick += HandleStick;

            void HandleStick()
            {
                Tongues[capturedSlot].OnStick -= HandleStick;

                _grid.Clear(r, c);
                Grid.SetCell(r, c, 0);
                _reserved.Remove(key);
                _reservedPerColor[capturedColor]--;

                _slots[capturedSlot].Squash = 1f;

                if (_slots[capturedSlot].Count == 0 && _slots[capturedSlot].Live)
                    StartCoroutine(ReplaceCart(capturedSlot));

                if (_eaten >= _total) StartCoroutine(WinSequence());
                else CheckLose();
            }
        }

        int FindNearestCart(int color, Vector2 target)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Live || _slots[i].Exiting) continue;
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

            if (_queueIndex < _queue.Count)
            {
                var def = _queue[_queueIndex];
                _queueIndex++;

                _slots[slot].ColorId = def.colorId;
                _slots[slot].Count = def.capacity;
                _slots[slot].Exiting = false;
                _slots[slot].ExitT = 0f;
                _slots[slot].EnterT = 1f;

                Carts[slot].SetColor(Palette, def.colorId);
                Carts[slot].SetCount(def.capacity);
                Frogs[slot].SetColor(Palette, def.colorId);

                Queue.Shift(_queue, _queueIndex, Config.queueShift);

                float enter = 0f;
                while (enter < Config.cartEnter)
                {
                    enter += Time.unscaledDeltaTime;
                    _slots[slot].EnterT = 1f - Mathf.Clamp01(enter / Config.cartEnter);
                    yield return null;
                }

                _slots[slot].EnterT = 0f;
            }
            else
            {
                _slots[slot].Live = false;
                Carts[slot].SetVisible(false);
                Frogs[slot].SetVisible(false);
            }

            if (State == GameState.Play) CheckLose();
        }

        // ── исходы ──────────────────────────────────────────────────────────────────

        void CheckLose()
        {
            System.Array.Clear(_capacity, 0, _capacity.Length);

            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Live) _capacity[_slots[i].ColorId] += _slots[i].Count;

            for (int i = _queueIndex; i < _queue.Count; i++)
                _capacity[_queue[i].colorId] += _queue[i].capacity;

            if (!LoseCheck.IsLost(_grid, _capacity, _reservedPerColor)) return;

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
                FlashOverlay.color = new Color(1f, 1f, 1f, alpha);
            });

            // 2. Силуэт: 1.2 c плоскими блоками.
            Grid.ShowSilhouette(Level.Rows, true);

            // 4. Конфетти.
            Confetti.Play();

            yield return new WaitForSecondsRealtime(Config.winSilhouette);
            Grid.ShowSilhouette(Level.Rows, false);

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

            if (State == GameState.Win) _clapPhase += dt;

            for (int i = 0; i < _slots.Length; i++)
            {
                if (!_slots[i].Live) continue;

                _slots[i].Recoil = Decay(_slots[i].Recoil, Config.recoilDecay, dt);
                _slots[i].Squash = Decay(_slots[i].Squash, Config.squashDecay, dt);

                Sample(i, out var pos, out float angle);

                float exit = _slots[i].ExitT;
                float enter = _slots[i].EnterT;

                float scale = (1f - exit * 0.5f) * (1f - enter * 0.45f);
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

        void Sample(int slot, out Vector2 pos, out float angle)
        {
            float offset = slot * _path.Perimeter / _slots.Length + 60f;
            float exitExtra = _slots[slot].ExitT * 90f;
            _path.Sample(_dist + offset - _slots[slot].Recoil + exitExtra, out pos, out angle);
        }

        static float Decay(float value, float rate, float dt)
            => value <= 0f ? 0f : Mathf.Max(0f, value - value * rate * dt - 0.001f);
    }
}
