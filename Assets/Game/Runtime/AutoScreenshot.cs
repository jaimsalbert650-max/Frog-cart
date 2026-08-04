using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Снимок экрана из собранного билда по аргументам командной строки:
    ///   FrogCart.exe -autoshot &lt;путь.png&gt; [-shotdelay 1.5] [-shottaps 8]
    ///                [-shotafter 0.25] [-shotpanel win|lose|pause]
    ///
    /// Нужен, чтобы вообще иметь возможность посмотреть на игру без редактора:
    /// раскладку, палитру, рельсы, язык и панели иначе не проверить ничем, кроме глаз,
    /// а глаз у batchmode нет.
    /// </summary>
    public sealed class AutoScreenshot : MonoBehaviour
    {
        string _path;
        string _panel;
        float _delay = 1.5f;
        int _taps;
        float _afterLastTap = 0.25f;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoshot" && i + 1 < args.Length) _path = args[i + 1];
                if (args[i] == "-shotpanel" && i + 1 < args.Length) _panel = args[i + 1];

                if (args[i] == "-shotdelay" && i + 1 < args.Length)
                    ParseFloat(args[i + 1], ref _delay);
                if (args[i] == "-shotafter" && i + 1 < args.Length)
                    ParseFloat(args[i + 1], ref _afterLastTap);
                if (args[i] == "-shottaps" && i + 1 < args.Length)
                    int.TryParse(args[i + 1], out _taps);

                if (args[i] == "-shotsend" && i + 1 < args.Length)
                    int.TryParse(args[i + 1], out _send);
            }

            if (string.IsNullOrEmpty(_path)) { enabled = false; return; }

            // Без этого плеер замирает, когда окно теряет фокус: кадры не идут,
            // корутина не тикает, снимок не делается — и снаружи это выглядит так,
            // будто скрипт вообще не запустился.
            Application.runInBackground = true;

            Debug.Log($"[AutoScreenshot] Ждём {_delay} c, тапов {_taps}, панель '{_panel}', " +
                      $"снимок через {_afterLastTap} c: {_path}");

            StartCoroutine(Capture());
        }

        static void ParseFloat(string text, ref float target)
            => float.TryParse(text, System.Globalization.NumberStyles.Float,
                              System.Globalization.CultureInfo.InvariantCulture, out target);

        /// <summary>
        /// Сколько вагонеток запустить перед снимком.
        ///
        /// В объёмной сцене контур стартует пустым, и без запуска на кадре не
        /// происходит ничего: игрок теперь тапает вагонетки, а не блоки.
        /// </summary>
        int _send;

        IEnumerator SendCarts(GameController controller)
        {
            for (int i = 0; i < _send; i++)
            {
                controller.SendCart(0);
                yield return new WaitForSecondsRealtime(0.15f);
            }
        }

        IEnumerator Capture()
        {
            yield return new WaitForSecondsRealtime(_delay);

            if (_send > 0)
            {
                var controller = FindAnyObjectByType<GameController>();
                if (controller != null) yield return SendCarts(controller);
            }

            if (_taps > 0) yield return Play();
            if (!string.IsNullOrEmpty(_panel)) ShowPanel();

            yield return new WaitForSecondsRealtime(_afterLastTap);

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            ScreenCapture.CaptureScreenshot(_path);
            Debug.Log($"[AutoScreenshot] Снимок записан: {_path}");

            yield return new WaitForSecondsRealtime(1.0f);
            Application.Quit();
        }

        /// <summary>
        /// Проходы по картинке, пока не съедено нужное число блоков или пока проход
        /// перестанет что-то менять. Один проход не годится: правило доступности
        /// отклоняет замурованные блоки, и они открываются только на следующем круге.
        /// </summary>
        IEnumerator Play()
        {
            var controller = FindAnyObjectByType<GameController>();
            if (controller == null) yield break;

            // Размер доски спрашиваем у контроллера. Раньше здесь стояли 16 и 14 —
            // размеры уровня 1. Уровни давно режутся из картинок и после обрезки
            // бывают меньше, и выход за массив ронял эту корутину исключением. Молча:
            // Capture ждала её на yield и не возобновлялась, поэтому снимок не писался,
            // Application.Quit не вызывался, и плеер висел до тех пор, пока его не убьют.
            var board = controller.Rows;
            if (board == null) yield break;

            int done = 0;
            int guard = 0;

            while (done < _taps && guard++ < 40)
            {
                int atPassStart = done;

                for (int r = 0; r < board.Length && done < _taps; r++)
                for (int c = 0; c < board[r].Length && done < _taps; c++)
                {
                    // Считаем только состоявшиеся ходы: тап по пустой или замурованной
                    // клетке языка не выпускает.
                    if (!controller.Eat(r, c)) continue;

                    done++;
                    yield return new WaitForSecondsRealtime(0.05f);
                }

                if (done == atPassStart) break;   // проход ничего не изменил
            }

            Debug.Log($"[AutoScreenshot] Съедено блоков: {done}");
        }

        void ShowPanel()
        {
            var panel = FindAnyObjectByType<PanelView>();
            if (panel == null) return;

            switch (_panel)
            {
                case "win": panel.ShowWin(); break;
                case "lose": panel.ShowLose(); break;

                // Паузу нельзя изображать одной панелью. Раньше здесь рисовалась
                // только она, а игра под ней продолжала идти — на снимке выходила
                // не пауза, а панель поверх живого хода, и проверить по такому
                // кадру было нечего. Просим паузу у контроллера, панель он покажет сам.
                case "pause": TogglePauseOnController(); break;
            }
        }

        void TogglePauseOnController()
        {
            var controller = FindAnyObjectByType<GameController>();
            if (controller != null) controller.TogglePause();
        }
    }
}
