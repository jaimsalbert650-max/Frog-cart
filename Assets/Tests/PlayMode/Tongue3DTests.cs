using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Язык растёт изо рта жабы, а не откуда-то рядом.
    ///
    /// Проверка числовая, потому что глазами это ловится плохо: на общем плане
    /// язык «примерно оттуда» выглядит приемлемо, и промах виден только вблизи.
    /// Прежняя ошибка была именно такой — корень языка стоял на доске под головой,
    /// на 11 единиц вглубь доски и на два с лишним ниже рта, и язык шёл поверх морды.
    /// </summary>
    public class Tongue3DTests
    {
        GameController _controller;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game3D", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _controller = Object.FindAnyObjectByType<GameController>();
        }

        [UnityTest]
        public IEnumerator TongueGrowsOutOfTheMouth()
        {
            yield return null;

            LevelProbe.PrepareBite(_controller, out int r, out int c);
            yield return null;

            Assert.IsTrue(_controller.Eat(r, c), "укус не состоялся, проверять нечего");

            // Кадр на отрисовку: язык раскладывается в Update контроллера.
            yield return null;

            bool checkedAny = false;

            for (int i = 0; i < _controller.Tongues.Length; i++)
            {
                if (!(_controller.Tongues[i] is Tongue3DView tongue)) continue;
                if (!tongue.Active) continue;
                if (!(_controller.Frogs[i] is Frog3DView frog)) continue;

                float distance = Vector3.Distance(tongue.RootWorldPos, frog.MouthWorldPos);
                checkedAny = true;

                // Порог с запасом на один кадр хода вагонетки. Прежняя ошибка давала
                // больше двух с половиной единиц, так что различает он уверенно.
                Assert.Less(distance, 0.35f,
                    $"корень языка в {distance:F2} единиц от рта жабы. Язык обязан "
                  + "начинаться во рту: высота головы теряется, если точку рта считать "
                  + "плоской формулой от вагонетки");
            }

            Assert.IsTrue(checkedAny, "ни один язык не оказался в полёте");
        }
    }
}
