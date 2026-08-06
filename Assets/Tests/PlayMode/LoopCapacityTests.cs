using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Ёмкость контура: мест ровно четыре, пятую вагонетку запустить нельзя,
    /// и отказ виден игроку.
    ///
    /// Проверяется на живой сцене, потому что правило держится сразу на трёх
    /// вещах: сколько слотов создал бутстрап, что вернул SendCart и что показало
    /// представление. Порознь эти три ничего не доказывают — уже был случай,
    /// когда ход не проходил молча и выглядел так, будто игра не заметила тапа.
    /// </summary>
    public class LoopCapacityTests
    {
        GameController _controller;
        NoticeView _notice;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            SceneManager.LoadScene("Game3D", LoadSceneMode.Single);
            yield return null;
            yield return null;

            _controller = Object.FindAnyObjectByType<GameController>();
            _notice = Object.FindAnyObjectByType<NoticeView>();
        }

        [UnityTest]
        public IEnumerator FifthCartIsRefusedAndTheRefusalIsVisible()
        {
            yield return null;

            Assert.IsNotNull(_controller, "контроллер не создан");
            Assert.IsNotNull(_notice, "табличка отказа не создана");
            Assert.IsFalse(_notice.Visible, "табличка висит до первого отказа");

            Assert.GreaterOrEqual(_controller.QueueCarts.Count, 5,
                "для проверки нужна очередь длиннее контура");

            int sent = FillLoop();

            Assert.AreEqual(4, sent, "на контур обязаны встать ровно четыре вагонетки");
            Assert.IsFalse(_notice.Visible, "успешный запуск не должен показывать отказ");

            Assert.IsFalse(_controller.SendCart(0), "пятая вагонетка не может выехать");
            Assert.IsTrue(_notice.Visible, "отказ обязан быть виден игроку");
        }

        [UnityTest]
        public IEnumerator RefusalFadesOnItsOwn()
        {
            yield return null;

            FillLoop();
            _controller.SendCart(0);

            Assert.IsTrue(_notice.Visible);

            // Табличка уходит сама: убирать её тапом нечестно — тап здесь и есть
            // действие игры, и игрок потерял бы ход на закрытии сообщения.
            for (float waited = 0f; waited < 2f && _notice.Visible; )
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsFalse(_notice.Visible, "табличка обязана растаять сама");
        }

        /// <summary>
        /// Меток на контуре ровно столько, сколько мест ещё свободно.
        ///
        /// Это и есть ответ на «сколько я могу поставить», и он обязан быть верным
        /// на каждом шаге, а не только на пустом контуре. Табличка отказа говорит
        /// после хода, метки — до него.
        /// </summary>
        [UnityTest]
        public IEnumerator EmptyPlacesAreMarkedBeforeTheFirstTap()
        {
            yield return null;

            Assert.AreEqual(4, MarkedPlaces(),
                "на старте контур пуст: игрок обязан увидеть все четыре места "
              + "до первого тапа, а не пустой рельс");

            Assert.IsTrue(SendOne(), "первая вагонетка обязана уехать");
            yield return null;

            Assert.AreEqual(3, MarkedPlaces(),
                "занятое место обязано перестать быть меткой сразу по тапу, "
              + "не дожидаясь конца полёта");

            FillLoop();
            yield return null;

            Assert.AreEqual(0, MarkedPlaces(), "на полном контуре меток не остаётся");
        }

        /// <summary>Сколько мест на контуре помечено свободными.</summary>
        static int MarkedPlaces()
        {
            int count = 0;

            foreach (var cart in Object.FindObjectsByType<Cart3DView>(FindObjectsSortMode.None))
                if (cart.ShowingEmptyPlace) count++;

            return count;
        }

        /// <summary>
        /// Отправить ровно одну вагонетку, сколько бы льда ни пришлось сколоть.
        /// Голова очереди бывает замороженной, и тогда тап скалывает лёд вместо
        /// запуска — та же причина, что и у <see cref="FillLoop"/>.
        /// </summary>
        bool SendOne()
        {
            for (int guard = 0; guard < 32; guard++)
                if (_controller.SendCart(0)) return true;

            return false;
        }

        /// <summary>
        /// Занять контур целиком.
        ///
        /// Голова очереди бывает замороженной, и тогда тап скалывает лёд вместо
        /// запуска. Поэтому не четыре вызова подряд, а попытки до заполнения:
        /// каждая либо отправляет вагонетку, либо скалывает лёд, третьего нет.
        /// </summary>
        int FillLoop()
        {
            int sent = 0;

            for (int guard = 0; guard < 32 && sent < 4; guard++)
                if (_controller.SendCart(0)) sent++;

            return sent;
        }
    }
}
