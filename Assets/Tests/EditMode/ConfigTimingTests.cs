using NUnit.Framework;
using UnityEngine;
using FrogCart.Data;

namespace FrogCart.Tests
{
    /// <summary>
    /// Тайминги из docs/unity-spec/05-feel-anim.md, закреплённые тестом.
    ///
    /// Смотреть их «на глаз» бесполезно: разницу между 0.20 и 0.28 секунды никто не
    /// увидит, а правило «ничего медленнее 0.3 c» — это ощущение игры, и оно ломается
    /// молча. Пусть ломается громко.
    /// </summary>
    public class ConfigTimingTests
    {
        GameConfig _config;

        [SetUp]
        public void SetUp() => _config = ScriptableObject.CreateInstance<GameConfig>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_config);

        [Test]
        public void TongueTimingsMatchTheSpec()
        {
            Assert.AreEqual(0.42f, _config.tongueOut, 0.001f, "язык наружу");
            Assert.AreEqual(0.20f, _config.tongueBack, 0.001f, "язык обратно");
        }

        [Test]
        public void CartTimingsMatchTheSpec()
        {
            Assert.AreEqual(0.34f, _config.cartExit, 0.001f, "выезд вагонетки");
            Assert.AreEqual(0.30f, _config.cartEnter, 0.001f, "въезд вагонетки");
            Assert.AreEqual(0.30f, _config.queueShift, 0.001f, "сдвиг очереди");
            Assert.AreEqual(0.34f, _config.exitToDock, 0.001f, "пауза до стыковки");
            Assert.AreEqual(0.38f, _config.emptyToExit, 0.001f, "пауза до выезда");
        }

        [Test]
        public void FeedbackTimingsMatchTheSpec()
        {
            Assert.AreEqual(0.28f, _config.shakeDuration, 0.001f, "тряска экрана");
            Assert.AreEqual(0.34f, _config.wobbleDuration, 0.001f, "дрожание ячейки");
            Assert.AreEqual(0.22f, _config.numberPop, 0.001f, "поп числа");
            Assert.AreEqual(0.18f, _config.progressTween, 0.001f, "прогресс-бар");
            Assert.AreEqual(0.30f, _config.panelAppear, 0.001f, "появление панели");
        }

        [Test]
        public void MovementAndInputMatchTheSpec()
        {
            Assert.AreEqual(52f, _config.railSpeed, 0.001f, "скорость по рельсам");
            Assert.AreEqual(0.09f, _config.chainDelay, 0.001f, "задержка цепочки");
            Assert.AreEqual(10, _config.shakeEvery, "тряска каждый N-й блок");
        }

        [Test]
        public void NothingIsSlowerThanThreeTenthsOfASecond()
        {
            // Правило номер один из 05-feel-anim.md. Исключения там же оговорены:
            // конфетти, «дыхание» панели и сам язык наружу. Выезд вагонетки — 0.34 c,
            // тоже сверх правила, но это число прямо написано в таблице спеки,
            // поэтому проверяется отдельно.
            //
            // Языка наружу в списке больше нет намеренно. Правило писалось под
            // механику, где выстрел был редким событием по тапу; теперь вагонетка
            // стреляет непрерывно, и короткий язык читался дрожанием. Снимать его
            // из-под правила пришлось явно — иначе тест пришлось бы отключить целиком
            // и он перестал бы сторожить остальные восемь чисел.
            var fast = new (string name, float value)[]
            {
                ("язык обратно", _config.tongueBack),
                ("въезд вагонетки", _config.cartEnter),
                ("сдвиг очереди", _config.queueShift),
                ("тряска экрана", _config.shakeDuration),
                ("поп числа", _config.numberPop),
                ("прогресс-бар", _config.progressTween),
                ("появление панели", _config.panelAppear),
            };

            foreach (var (name, value) in fast)
                Assert.LessOrEqual(value, 0.30f, $"{name} длиннее 0.3 c");
        }

        [Test]
        public void OutcomeDelaysMatchTheSpec()
        {
            Assert.AreEqual(0.26f, _config.winDelay, 0.001f);
            Assert.AreEqual(0.34f, _config.loseDelay, 0.001f);
            Assert.AreEqual(0.40f, _config.startLoseCheckDelay, 0.001f);
            Assert.AreEqual(0.50f, _config.winFlash, 0.001f);
            Assert.AreEqual(1.20f, _config.winSilhouette, 0.001f);
            Assert.AreEqual(1.10f, _config.winPanelDelay, 0.001f);
        }
    }
}
