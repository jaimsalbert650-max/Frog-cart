using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using FrogCart.Runtime;

namespace FrogCart.Tests
{
    /// <summary>
    /// Тесты мини-твинера. Появились из-за конкретной поломки: очередь в доке
    /// доезжала влево, но не перерисовывалась под новый индекс — то есть обратный
    /// вызов завершения либо не срабатывал, либо срабатывал не тогда.
    /// </summary>
    public class TweenerTests
    {
        GameObject _host;
        Tweener _tweener;

        [SetUp]
        public void SetUp()
        {
            _host = new GameObject("TweenerHost");
            _tweener = _host.AddComponent<Tweener>();
        }

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_host);

        [UnityTest]
        public IEnumerator CallsDoneAfterDuration()
        {
            bool done = false;
            float last = -1f;

            _tweener.Run(0.1f, Tweener.Linear, t => last = t, () => done = true);

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.IsTrue(done, "обратный вызов завершения не сработал");
            Assert.AreEqual(1f, last, 0.001f, "последнее значение обязано быть ровно 1");
        }

        [UnityTest]
        public IEnumerator CallsDoneForEveryTweenInTheSameFrame()
        {
            int done = 0;

            for (int i = 0; i < 5; i++)
                _tweener.Run(0.1f, Tweener.Linear, _ => { }, () => done++);

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.AreEqual(5, done, "часть завершений потерялась");
        }

        [UnityTest]
        public IEnumerator SurvivesTweenStartedFromDoneCallback()
        {
            // Именно так и работает очередь: по завершении сдвига запускается
            // пересборка, которая может стартовать новый твин.
            bool secondDone = false;

            _tweener.Run(0.05f, Tweener.Linear, _ => { },
                () => _tweener.Run(0.05f, Tweener.Linear, _ => { }, () => secondDone = true));

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.IsTrue(secondDone, "твин, запущенный из завершения, не доиграл");
        }

        [UnityTest]
        public IEnumerator ZeroDurationCompletesImmediately()
        {
            bool done = false;
            float value = -1f;

            _tweener.Run(0f, Tweener.Linear, t => value = t, () => done = true);

            Assert.IsTrue(done, "нулевая длительность обязана завершаться сразу");
            Assert.AreEqual(1f, value, 0.001f);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CancelAllStopsPendingTweens()
        {
            bool done = false;

            _tweener.Run(0.2f, Tweener.Linear, _ => { }, () => done = true);
            _tweener.CancelAll();

            yield return new WaitForSecondsRealtime(0.4f);

            Assert.IsFalse(done, "отменённый твин не должен вызывать завершение");
        }

        [UnityTest]
        public IEnumerator EasingEndsExactlyAtOne()
        {
            // Overshoot и QueueShift по дороге выходят за единицу; важно, что в конце
            // они в неё возвращаются, иначе элемент застынет мимо цели.
            float overshoot = -1f;
            float queue = -1f;

            _tweener.Run(0.1f, Tweener.Overshoot, t => overshoot = t);
            _tweener.Run(0.1f, Tweener.QueueShift, t => queue = t);

            yield return new WaitForSecondsRealtime(0.3f);

            Assert.AreEqual(1f, overshoot, 0.01f);
            Assert.AreEqual(1f, queue, 0.01f);
        }
    }
}
