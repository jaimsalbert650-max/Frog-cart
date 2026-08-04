using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Выбранная вагонетка выезжает из очереди на контур, и это видно.
    ///
    /// Проверка тестом, а не кадром, намеренно. Выезд длится 0.3 c, снималка берёт
    /// один кадр, и попасть им в середину хода не вышло ни разу за несколько
    /// попыток — при том что логи показывали и позицию, и включённые рендереры.
    /// Счётчик и координата надёжнее: они говорят ровно то, что нужно знать.
    /// </summary>
    public class QueueDepartTests
    {
        GameController _controller;
        Queue3DView _queue;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game3D", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _controller = Object.FindAnyObjectByType<GameController>();
            _queue = Object.FindAnyObjectByType<Queue3DView>();
        }

        [UnityTest]
        public IEnumerator TakenCartLeavesTheQueueTowardsTheLoop()
        {
            yield return null;

            Assert.IsNotNull(_queue, "объёмная очередь не построена");
            Assert.AreEqual(0, _queue.DepartingCount, "до хода никто ехать не должен");

            Assert.IsTrue(_controller.SendCart(0), "первая вагонетка обязана запускаться");
            yield return null;

            Assert.AreEqual(1, _queue.DepartingCount,
                "после тапа вагонетка обязана поехать. Без выезда ход игрока не виден: "
              + "вагонетка просто исчезает из очереди и появляется на рельсе");

            Vector3 start = _queue.DepartingPos;

            // Полкадра мало, чтобы что-то сдвинулось заметно: ждём треть выезда.
            float wait = _controller.Config.cartEnter * 0.4f;
            for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;

            Vector3 mid = _queue.DepartingPos;

            Assert.Greater(Vector3.Distance(start, mid), 0.5f,
                "вагонетка не сдвинулась с места");

            Assert.Greater(mid.y, start.y,
                "выезд идёт дугой вверх: путь к дальнему месту проходит над доской, "
              + "и по земле вагонетка ушла бы под неё");
        }

        [UnityTest]
        public IEnumerator DepartureEndsAndCleansUpAfterItself()
        {
            yield return null;

            Assert.IsTrue(_controller.SendCart(0));
            yield return null;

            float wait = _controller.Config.cartEnter + 0.3f;
            for (float t = 0f; t < wait; t += Time.unscaledDeltaTime) yield return null;

            Assert.AreEqual(0, _queue.DepartingCount,
                "выезд не завершился: уехавшая вагонетка осталась висеть в сцене");
        }
    }
}
