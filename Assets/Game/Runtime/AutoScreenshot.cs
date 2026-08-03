using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace FrogCart.Runtime
{
    /// <summary>
    /// Снимок экрана из собранного билда по аргументу командной строки:
    ///   FrogCart.exe -autoshot &lt;путь.png&gt; [-shotdelay 1.5] [-shottaps 8]
    ///
    /// Нужен, чтобы вообще иметь возможность посмотреть на игру без редактора:
    /// раскладку, палитру и рельсы иначе не проверить ничем, кроме глаз,
    /// а глаз у batchmode нет.
    /// </summary>
    public sealed class AutoScreenshot : MonoBehaviour
    {
        string _path;
        float _delay = 1.5f;
        int _taps;

        void Start()
        {
            var args = Environment.GetCommandLineArgs();

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-autoshot" && i + 1 < args.Length) _path = args[i + 1];
                if (args[i] == "-shotdelay" && i + 1 < args.Length)
                    float.TryParse(args[i + 1], System.Globalization.NumberStyles.Float,
                                   System.Globalization.CultureInfo.InvariantCulture, out _delay);
                if (args[i] == "-shottaps" && i + 1 < args.Length)
                    int.TryParse(args[i + 1], out _taps);
            }

            if (string.IsNullOrEmpty(_path)) { enabled = false; return; }

            StartCoroutine(Capture());
        }

        IEnumerator Capture()
        {
            yield return new WaitForSecondsRealtime(_delay);

            // Опционально «сыграть»: съесть несколько блоков, чтобы на снимке
            // были видны язык, пустые гнёзда и сдвинувшийся прогресс.
            if (_taps > 0)
            {
                var controller = FindAnyObjectByType<GameController>();
                if (controller != null)
                {
                    int done = 0;
                    for (int r = 0; r < 16 && done < _taps; r++)
                    for (int c = 0; c < 14 && done < _taps; c++)
                    {
                        controller.Eat(r, c);
                        done++;
                        yield return new WaitForSecondsRealtime(0.12f);
                    }
                }

                yield return new WaitForSecondsRealtime(0.25f);
            }

            var directory = Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            ScreenCapture.CaptureScreenshot(_path);
            Debug.Log($"[AutoScreenshot] Снимок записан: {_path}");

            yield return new WaitForSecondsRealtime(1.0f);
            Application.Quit();
        }
    }
}
